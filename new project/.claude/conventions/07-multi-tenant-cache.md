# 07. Multi-tenant & Cache

## 1. Multi-Tenant Architecture

**Purpose:** Isolation dữ liệu giữa các tenant, đảm bảo security & compliance.

**Cách hoạt động:**
1. Mỗi tenant có 1 schema riêng trong cùng DB
2. Connection string inject `SearchPath={realmName}` → tự động route query đến đúng schema
3. Tenant xác định từ JWT token (realm name)

### 1.1. `MutilTenantContextFactory`

```csharp
// Shared/OpassFab.Shared.Infrastructure/Persistences/MutilTenantContextFactory.cs
public class MutilTenantContextFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MutilTenantContextFactory(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // Connection cho current tenant (per-request)
    public NpgsqlConnection CreateNpgsqlConnection()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var realm = principal?.GetRealmName();   // Extract from JWT

        if (realm is null)
            throw new UnauthorizedAccessException("Tenant not found in token");

        var conn = FormulaConnectionString(realm);
        return new NpgsqlConnection(conn);
    }

    // DbContext cho tenant cụ thể (admin operation)
    public TContext CreateDbContextForTenant<TContext>(string realmName)
        where TContext : DbContext
    {
        var conn = FormulaConnectionString(realmName);
        var options = new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(conn)
            .Options;
        return (TContext)Activator.CreateInstance(typeof(TContext), options)!;
    }

    private string FormulaConnectionString(string realmName)
    {
        var baseConn = AppConfigProvider.Instance.Database.ConnectionString;
        // PostgreSQL SearchPath route query đến schema cụ thể
        return string.Concat(baseConn, $"SearchPath={realmName};");
    }
}
```

### 1.2. DI Registration

```csharp
// Shared/OpassFab.Shared.Infrastructure/Startup.cs
public static IServiceCollection ConfigureSharedService(
    this IServiceCollection services,
    IWebHostEnvironment env)
{
    // Singleton factory (stateless)
    services.AddSingleton<MutilTenantContextFactory>();

    // Scoped connection (per-request, per-tenant)
    services.AddScoped<DbConnection>(sp =>
    {
        var factory = sp.GetRequiredService<MutilTenantContextFactory>();
        return factory.CreateNpgsqlConnection();
    });

    return services;
}
```

✅ Validate tenant từ JWT token
✅ `SearchPath` để isolate (PostgreSQL feature)
✅ KHÔNG cho phép cross-tenant data access
✅ Cache key include tenant: `{tenantCode}:{cacheKey}`
✅ Validate tenant trong business logic
❌ KHÔNG hardcode tenant code
❌ KHÔNG trust client-provided tenant
❌ KHÔNG query mà không có tenant context

## 2. Cache (Redis)

**Purpose:** Tối ưu performance, giảm DB load, cải thiện response time.

### 2.1. ICacheService

```csharp
// Shared/OpassFab.Shared.Module/Services/ICacheService.cs
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, Func<Task<T>>? fallback = null);
    Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task<T> GetOrSetCacheAsync<T>(string key, Func<Task<T>> fetchFromDb, TimeSpan? expiration = null);
    Task<bool> RemoveAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task<bool> RemoveByPatternAsync(string pattern);
    Task<bool> ClearAllAsync();
}
```

### 2.2. DI Registration

```csharp
private static IServiceCollection AddRedisCache(this IServiceCollection services)
{
    var redisSettings = AppConfigProvider.Instance.RedisCache;

    // Singleton connection multiplexer (shared)
    services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var options = ConfigurationOptions.Parse(redisSettings.ConnectionString);
        return ConnectionMultiplexer.Connect(options);
    });

    services.AddSingleton<ICacheService, RedisCacheService>();

    return services;
}
```

### 2.3. Sử dụng trong Repository (`ReadableRepositoryBase`)

```csharp
public abstract class ReadableRepositoryBase<TContext, TEntity, TModel>
    : IReadableRepositoryBase<TEntity, TModel>
    where TContext : OpassFabReadableContextBase
{
    protected readonly ICacheService _cacheService;
    protected readonly string? _entityTable;
    protected readonly bool _hasCache = false;

    public ReadableRepositoryBase(TContext dbContext, ICacheService cacheService)
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
        _userContext = HttpContextProvider.UserContext!;

        // Check entity có cache trong config không
        var entityType = _dbContext.Model.FindEntityType(typeof(TEntity))!;
        _entityTable = entityType.GetTableName() ?? entityType.GetViewName();

        if (_entityTable != null &&
            AppConfigProvider.Instance.RedisCache.Tables.TryGetValue(_entityTable, out var hasCache))
            _hasCache = hasCache;
    }

    // Cache key: {TenantCode}:{Table}:{Lang}
    protected virtual string _cacheKey =>
        $"{_userContext.TenantCode}:{_entityTable}:{_userContext.LangCd}";

    public async Task<List<TModel>> QueryAsync(
        Expression<Func<TModel, bool>> condition,
        int? maxSearchCount = null)
    {
        var baseQuery = await GetCacheQuery();
        if (condition != null) baseQuery = baseQuery.Where(condition);
        if (maxSearchCount.HasValue) baseQuery = baseQuery.Take(maxSearchCount.Value);
        return await baseQuery.ToListAsync();
    }

    private async Task<IQueryable<TModel>> GetCacheQuery()
    {
        if (!_hasCache)
            return _dbContext.Set<TEntity>().Cast<TModel>().AsQueryable();

        var cached = await _cacheService.GetAsync<List<TModel>>(_cacheKey);
        if (cached != null)
            return cached.AsQueryable();

        var data = await _dbContext.Set<TEntity>().Cast<TModel>().ToListAsync();
        await _cacheService.SetAsync(_cacheKey, data);
        return data.AsQueryable();
    }
}
```

### 2.4. Cache Key Format

```
{TenantCode}:{TableName}:{LangCd}        // Master data — chủ yếu dùng case này
{TenantCode}:{Module}:{Entity}:{Id}      // Entity-specific
{TenantCode}:{Module}:{Feature}:{Key}    // Feature-specific
```

### 2.5. Examples

```csharp
// 1. Simple get/set
var cacheKey = $"Master:Product:{productCode}";
var product = await _cacheService.GetAsync<Product>(cacheKey);
if (product == null)
{
    product = await _repository.GetByCodeAsync(productCode);
    await _cacheService.SetAsync(cacheKey, product, TimeSpan.FromMinutes(60));
}

// 2. GetOrSet pattern (RECOMMENDED)
var product = await _cacheService.GetOrSetCacheAsync(
    cacheKey,
    async () => await _repository.GetByCodeAsync(productCode),
    TimeSpan.FromMinutes(60)
);

// 3. Invalidation
await _cacheService.RemoveByPatternAsync($"Master:Product:*");
```

### 2.6. Cache Configuration

```json
{
  "RedisCache": {
    "ConnectionString": "localhost:6379",
    "DefaultExpirationMinutes": 60,
    "ClearCacheOnStartup": false,
    "Tables": {
      "m_menu": true,
      "m_country": true,
      "m_sec": false
    }
  }
}
```

✅ Cache key format `{TenantCode}:{Module}:{Entity}:{Id}`
✅ Include tenant trong key (multi-tenant isolation)
✅ Expiration hợp lý (60 phút cho master data)
✅ Invalidate khi data thay đổi
✅ Dùng `GetOrSetCacheAsync` pattern
✅ Cache master data (ít thay đổi, hay đọc)
❌ KHÔNG cache sensitive data (password, ...)
❌ KHÔNG cache quá lớn (memory limit)
❌ KHÔNG cache user-specific data (dùng session)
❌ KHÔNG cache data thay đổi thường xuyên
