# 11. API Design Guidelines

## 1. Request Flow (End-to-End)

```
1. HTTP Request
        ↓
2. Middleware Pipeline
   ├── OpassFabExceptionMiddleware     (catch exceptions)
   ├── UseHttpsRedirection
   ├── UseRouting
   ├── UseCors
   ├── UseAuthentication               (JWT validation)
   ├── UseAuthorization                (Permission check)
   ├── ApplicationContextMiddleware    (set UserContext)
   └── ApiLoggingMiddleware            (log request)
        ↓
3. Controller Action
   ├── [Validation] Attribute
   │   └── ValidationAttribute.OnActionExecuting()
   │       ├── Check ModelState.IsValid
   │       └── Return BadRequest if invalid
   ├── DI (ISalesOrderService)
   └── Action method execution
        ↓
4. Service Layer
   ├── Business validation
   ├── Access Repository qua UnitOfWork
   ├── Business logic + DTO mapping
   └── _unitOfWork.SaveChangesAsync()
        ↓
5. Repository Layer
   ├── Build query (Include, filter)
   └── Execute (FirstOrDefaultAsync, ...)
        ↓
6. Database (PostgreSQL)
   ├── SQL với SearchPath (multi-tenant)
   └── Return result
        ↓
7. Response
   ├── Map Entity → DTO
   ├── Return IActionResult
   └── Serialize to JSON
        ↓
8. Exception Handling (nếu có)
   ├── OpassFabApplicationException → 400/422
   ├── UnauthorizedAccessException  → 401
   └── System Exception              → 500
```

## 2. RESTful Conventions

**Nguyên tắc:** Resource-based URL, stateless, HTTP methods chuẩn.

### 2.1. HTTP Methods

| Method | Mục đích | Idempotent | Example |
|---|---|---|---|
| `GET` | Lấy dữ liệu | Yes | `GET /api/management/user?status=` |
| `POST` | Tạo mới | No | `POST /api/management` |
| `PUT` | Cập nhật toàn bộ | Yes | `PUT /api/management/user/123` |
| `PATCH` | Cập nhật một phần | No | `PATCH /api/management/user/123` |
| `DELETE` | Xóa | Yes | `DELETE /api/management/user/123` |

### 2.2. HTTP Status Code

| Code | Meaning | Use Case |
|---|---|---|
| 200 | OK | GET, PUT, PATCH thành công, có data |
| 201 | Created | POST thành công, trả resource mới + Location header |
| 204 | No Content | DELETE thành công, không cần data |
| 400 | Bad Request | Sai syntax, missing field, invalid format |
| 401 | Unauthorized | Token invalid/expired/missing |
| 403 | Forbidden | Authenticated nhưng không có quyền |
| 404 | Not Found | Resource không tồn tại |
| 422 | Unprocessable Entity | Hợp lệ nhưng vi phạm business rule |
| 429 | Too Many Requests | Vượt rate limit |
| 500 | Internal Server Error | Unexpected exception |

✅ 200 cho GET thành công
✅ 201 cho POST tạo mới
✅ 400 cho validation error
✅ 404 cho resource không tồn tại
❌ KHÔNG dùng 200 cho error
❌ KHÔNG dùng 500 cho business error

## 3. URL Naming

### 3.1. Standard Structure

```
/api/{Module}/{Resource}            → GET all
/api/{Module}/{Resource}/{Id}       → GET by ID
/api/{Module}/{Resource}            → POST (create)
/api/{Module}/{Resource}/{Id}       → PUT/PATCH (update)
/api/{Module}/{Resource}/{Id}       → DELETE
```

Example: `/api/Sales/Orders/12345`, `/api/Admin/Management/Authority/User`

### 3.2. Conventions

| Rule | ✅ Good | ❌ Bad |
|---|---|---|
| Nouns, not verbs | `/api/products` | `/api/getProducts` |
| Plural for collection | `/api/orders` | `/api/order` |
| Lowercase | `/api/user-profiles` | `/api/UserProfiles` |
| Hyphen for readability | `/api/order-items` | `/api/order_items` |
| Hierarchical relationship | `/api/orders/123/items` | — |
| Filter qua query param | `/api/products?category=electronics&price_min=100` | — |
| Versioning trong URL | `/api/v1/products`, `/api/v2/products` | — |

### 3.3. Controller Naming

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase { }
```

## 4. Validation — Data Annotation

### 4.1. Custom Attributes

| Attribute | Purpose |
|---|---|
| `[OpassFabRequired]` | Required field |
| `[OpassFabMaxLength(n)]` | Max length |
| `[OpassFabMinLength(n)]` | Min length |
| `[OpassFabNotZero]` | Must be non-zero |
| `[OpassFabRange(min, max)]` | Range |
| `[EmailAddress]` | Email format |
| `[CustomValidation]` | Custom logic |

### 4.2. DTO Model

```csharp
public class CreateSalesOrderModel : OpassFabModelBase
{
    [OpassFabRequired]
    [OpassFabMaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [OpassFabRequired]
    [OpassFabMaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [OpassFabMaxLength(500)]
    public string? Description { get; set; }

    [OpassFabRequired]
    [OpassFabNotZero]
    public decimal Price { get; set; }

    [OpassFabRequired]
    [OpassFabMaxLength(20)]
    public string CategoryCode { get; set; } = string.Empty;
}
```

### 4.3. Controller — `[Validation]` Filter

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    [HttpPost]
    [Validation]   // Custom validation filter
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
    {
        var product = await _productService.CreateAsync(request);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }
}
```

## 5. Row Version (Concurrency Control) với `xmin`

**Purpose:** Optimistic Concurrency Control (OCC) — ngăn xung đột data trong môi trường multi-user.

**Cơ chế:**
- Tất cả entity kế thừa `OpassFabDataModelBase` có property `RV` (uint)
- `RV` map với system column `xmin` của PostgreSQL (kiểu `xid`)
- `xmin` tự động thay đổi mỗi UPDATE/INSERT — không cần trigger
- Client/Frontend giữ `RV` (lúc GET) → gửi lại khi UPDATE

**Lợi ích:**
- Đảm bảo nhất quán data
- Giảm pessimistic locking → tăng performance & scalability

### 5.1. Entity Configuration

```csharp
// OpassFabDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity(entityType.ClrType)
        .Property<uint>(nameof(OpassFabDataModelBase.RV))
        .HasColumnName("xmin")          // PostgreSQL system column
        .HasColumnType("xid")
        .IsConcurrencyToken()           // EF Core concurrency token
        .ValueGeneratedOnAddOrUpdate();
}
```

### 5.2. Repository — Override `RV.OriginalValue`

```csharp
// RepositoryBase.cs
public virtual async Task UpdateAsync<TListModel>(List<TListModel> models)
    where TListModel : TEntity
{
    var entity = entityList.Find(MakeConditionPredicate(model));

    if (entity != null)
    {
        // 1. Copy data Client → Tracking Entity
        CopyModelValues(entity, model);

        // 2. Override OriginalValue = RV từ Client
        _dbContext.Entry(entity).Property(nameof(OpassFabDataModelBase.RV))
            .OriginalValue = model.RV;
    }
}
```

### 5.3. UnitOfWork — Catch Concurrency Error

```csharp
// UnitOfWorkBase.cs
public virtual async Task SaveChangesAsync(CancellationToken cancellationToken = default)
{
    try
    {
        await _dbContext.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException ex)   // xmin doesn't match
    {
        throw new OpassFabApplicationException(ex, "WZ00370");
    }
}
```

✅ Return `RV` trong **mọi** API GET
✅ Gửi `RV` cho **mọi** PUT/PATCH/DELETE
✅ Dùng `RepositoryBase.UpdateAsync` / `RemoveAsync` đã customize sẵn — auto load RV vào OriginalValue
❌ Tránh raw SQL UPDATE ở application layer (bypass concurrency)
❌ KHÔNG tự gán `RV` lúc tạo mới
❌ KHÔNG truy vấn lại entity ngay trước Update mà không override OriginalValue
❌ KHÔNG quên gửi `RV` (hoặc gửi `RV = 0`)

## 6. Tích hợp API bên thứ 3

**Purpose:** Common pattern khi tích hợp external API (eg. Keycloak) — error handling, retry, logging.

### 6.1. Base Class

```csharp
public abstract class RestApiServiceBase
{
    protected readonly HttpClient _httpClient;
    protected readonly JsonSerializerOptions _jsonOptions;

    protected async Task<TResponse> GetIntenalAsync<TResponse>(...)
    protected async Task<TResponse?> PostIntenalAsync<TResponse>(...)
    protected async Task<TResponse?> PutIntenalAsync<TResponse>(...)
    protected async Task<TResponse?> DeleteIntenalAsync<TResponse>(...)
}
```

**Trách nhiệm:**
- Dùng `HttpClient` từ DI
- JSON serialization với `JsonSerializerOptions`
- Auto `EnsureSuccessStatusCode()`
- Hỗ trợ `HttpStatusCode` làm response type

### 6.2. Generic Service

```csharp
public abstract class RestApiService<T> : RestApiServiceBase where T : class, new()
{
    protected virtual async Task<List<T>> QueryAsync(
        string url,
        Dictionary<string, string?> parameters,
        CancellationToken ct = default);

    protected virtual async Task<List<T>> GetAllAsync(string url, ...);
    protected virtual async Task<T?> GetAsync(string url, ...);

    protected virtual async Task<T> PostAsync(string url, object body, ...);
    protected virtual async Task<HttpStatusCode> TryPostAsync(...);

    protected virtual async Task<T?> PutAsync(string url, object body, ...);
    protected virtual async Task<HttpStatusCode> TryPutAsync(...);

    protected virtual async Task<HttpStatusCode> TryDeleteAsync(...);
}
```

### 6.3. DI Setup (eg. Keycloak)

```csharp
private static IServiceCollection AddKcService<TService>(this IServiceCollection services)
    where TService : class
{
    services.AddHttpClient<TService>(client =>
    {
        client.BaseAddress = new Uri(AppConfigProvider.Instance.Keycloak.BaseUrl);
    })
    .AddHttpMessageHandler<KcMessageHandler>()   // Custom handler
    .AddRetryPolicy();                            // Polly retry

    return services;
}
```

### 6.4. Custom Message Handler

```csharp
public class KcMessageHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await AddAuthorizationHeaderAsync(request, cancellationToken);

        _logger.LogInfo($"Start HttpRequest {request.Method.Method} {request.RequestUri}");
        var response = await base.SendAsync(request, cancellationToken);
        _logger.LogInfo("End HttpRequest", stopwatch.Elapsed);

        return response;
    }
}
```

### 6.5. Retry Pattern (Polly)

```csharp
public static void AddRetryPolicy(
    this IHttpClientBuilder builder,
    int retryCount = 3,
    TimeSpan? duration = null)
{
    builder.AddPolicyHandler(_ =>
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()                                          // 5xx, 408, HttpRequestException
            .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)   // 429
            .WaitAndRetryAsync(
                retryCount: retryCount,
                sleepDurationProvider: retryAttempt =>
                    duration ?? TimeSpan.FromSeconds(2));
    });
}
```

| HTTP Status | Retry? |
|---|---|
| 5xx | ✅ Yes |
| 408 (Request Timeout) | ✅ Yes |
| 429 (Too Many Requests) | ✅ Yes |
| `HttpRequestException` | ✅ Yes |
| 4xx (khác 408, 429) | ❌ No |
| 2xx | ❌ No |

**Default:** 3 retry, 2s delay.

✅ Dùng `HttpClientFactory` (KHÔNG `new HttpClient()`)
✅ Configure retry cho external API
❌ KHÔNG retry cho non-idempotent operation
