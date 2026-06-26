# 03. Libraries & Packages

Tất cả package được quản lý qua NuGet.

## Core Packages

| # | Package | Version | Category | Purpose |
|---|---|---|---|---|
| 1 | `Serilog.AspNetCore` | 10.0.0 | Logging | Structured logging — Console, File, DB sink |
| 2 | `Microsoft.EntityFrameworkCore` | 10.0.1 | ORM | EF Core — LINQ over DB |
| 2 | `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.0 | Database | PostgreSQL provider cho EF Core |
| 3 | `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.1 | Security | JWT bearer authentication |
| 4 | `StackExchange.Redis` (qua `NRedisStack`) | 1.1.1 | Caching | Redis client — distributed cache |
| 5 | `Swashbuckle.AspNetCore` | 10.1.0 | API Docs | OpenAPI/Swagger gen |
| 6 | `Microsoft.Extensions.Http.Polly` | 10.0.1 | Resilience | HTTP retry & resilience policy |

## 3.1. Serilog (Logging)

| Feature | Purpose |
|---|---|
| Structured logging | Log dạng JSON, dễ query/analyze |
| Multiple sinks | Console, File, DB, Elasticsearch, ... |
| Log levels | Trace, Debug, Information, Warning, Error, Fatal |
| Enrichers | Tự động thêm ThreadId, MachineName, Environment |

```csharp
using Serilog;

// Initialize trong Program.cs
SeriLogProvider.Initialize(builder.Environment);

// Usage
var logger = Log.ForContext<MyClass>();
logger.LogInfo("Message");
logger.LogError("Error occurred", exception);
```

✅ Structured logging với context
✅ Log level phù hợp tình huống
❌ KHÔNG log sensitive data
❌ KHÔNG log quá nhiều (perf impact)

## 3.2. Entity Framework Core + PostgreSQL

| Feature | Purpose |
|---|---|
| Code Configuration `{Entity}Configuration.cs` | Định nghĩa entity bằng C#, mapping schema |
| LINQ | Type-safe query, IntelliSense |
| Change Tracking | Track changes, `SaveChanges()` apply |
| Relationship Mapping | One-to-One, One-to-Many, Many-to-Many |

```csharp
// DbContext config
services.DbContextConfigure<MasterContext>();

// Repository pattern
public class ProductRepository : RepositoryBase<MasterContext, Product>
{
    public async Task<Product?> GetByCodeAsync(string code)
        => await _dbSet.FirstOrDefaultAsync(p => p.Code == code);
}
```

✅ Repository pattern (KHÔNG dùng DbContext trực tiếp trong Service)
✅ Async cho mọi DB operation
✅ UnitOfWork cho transaction
❌ Synchronous method
❌ Query quá nhiều data (phải pagination)

## 3.3. JWT Authentication

| Feature | Purpose |
|---|---|
| Token-based | Stateless, không session server-side |
| Claims-based | User/tenant info trong payload |
| Signature verification | Verify integrity bằng secret key |
| Expiration control | Auto-validate lifetime |

```csharp
// Configure trong Startup.cs
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* config */ });

// Apply
[Authorize]
public class MyController : ControllerBase { }
```

✅ Validate token trong middleware
✅ Policy-based authorization cho fine-grained
❌ KHÔNG hardcode secret

## 3.4. Cache (Redis)

| Feature | Purpose |
|---|---|
| Distributed cache | Tối ưu performance, giảm DB load |

```csharp
private readonly ICacheService _cacheService;

// Get
var cached = await _cacheService.GetAsync<string>("key");

// Set
await _cacheService.SetAsync("key", value, TimeSpan.FromMinutes(60));
```

✅ Cache data hay đọc, ít thay đổi
✅ Set expiration hợp lý
✅ Invalidate khi data thay đổi
❌ KHÔNG cache sensitive data
❌ KHÔNG cache quá lớn

Chi tiết → [07. Multi-tenant & Cache](07-multi-tenant-cache.md).

## 3.5. OpenAPI / Swagger

```csharp
if (appConfig.OpenApi.EnableApiDocument)
{
    services.AddSwaggerGen(/* config */);
}
// Truy cập: /swagger or /swagger/redoc
```

✅ Enable trong Dev, disable trong Production
✅ XML comment cho controller + DTO
✅ Configure security scheme (Bearer token)

## 3.6. HTTP Client & Retry Policy

```csharp
services.AddHttpClient<MyService>()
    .AddRetryPolicy(retryCount: 3, duration: TimeSpan.FromSeconds(2));
```

✅ Dùng `HttpClientFactory` (KHÔNG `new HttpClient()`)
✅ Configure retry cho external API
❌ KHÔNG retry cho non-idempotent op

Chi tiết → [11. API Design — Tích hợp API bên thứ 3](11-api-design.md#tích-hợp-api-bên-thứ-3).

## Rule Upgrade Version

Scan vulnerability:
```bash
dotnet list package --vulnerable
```

| Version Type | Behavior |
|---|---|
| **Major** (x.0.0) | ⚠ Có thể breaking change → đọc release notes, test kỹ, upgrade từng package |
| **Minor** (0.x.0) | Thường backward compatible — vẫn nên test |
| **Patch** (0.0.x) | Bug fix, security — upgrade ngay khi có |

**Upgrade flow:**
1. Đọc release notes
2. Test trên Development
3. Test trên Staging
4. Deploy lên Production
