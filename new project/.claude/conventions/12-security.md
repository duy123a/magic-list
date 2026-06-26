# 12. Security Guidelines

**Purpose:** Bảo mật ứng dụng qua Authentication, Authorization, Rate Limiting, HTTPS enforcement.

## 1. Rate Limiting

**Purpose:** Ngăn abuse, DDoS attack.

```csharp
// Startup.cs
services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsync("Too Many Requests", token);
    };
});

// Pipeline
app.UseRateLimiter();
```

✅ Configure rate limit phù hợp
✅ Limit khác nhau cho endpoint khác nhau (nếu cần)
✅ Return 429
✅ Log rate limit violation
❌ KHÔNG set quá thấp (ảnh hưởng user hợp lệ)

## 2. Authentication — JWT Bearer

**Purpose:** Stateless authentication, không session server-side. Phù hợp API/Microservice.

```csharp
var jwt = appConfig.JwtToken;

services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.IssuerSigningKey))
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            // Additional validation
            var tokenStorageManager = context.HttpContext.RequestServices
                .GetRequiredService<ITokenStorageManager>();
            var isVerify = await tokenStorageManager.VerifyTokenAsync(context.Principal!);
            if (!isVerify)
                context.Fail("Unauthorized");
        }
    };
});
```

✅ Access token ngắn hạn (giảm thiệt hại nếu lộ)
✅ Dùng Refresh Token cho session dài
✅ Secret/key đủ mạnh — tối thiểu 256-bit, KHÔNG hardcode
✅ Scope/permission rõ ràng
❌ KHÔNG dùng access token vài ngày/tháng
❌ KHÔNG JWT vĩnh viễn
❌ KHÔNG nhét sensitive data vào JWT (password, ...)

## 3. Authorization

### 3.1. Role-Based

**Purpose:** Kiểm soát quyền dựa trên role.

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]   // Requires authentication
public class SampleController : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetProtectedDatas()
    {
        // Chỉ Admin truy cập
    }
}
```

✅ Giữ số role ít
✅ Role mang ý nghĩa chức năng
❌ KHÔNG check role rải rác trong code
❌ KHÔNG dùng role để biểu diễn permission chi tiết → dùng Policy

### 3.2. Policy-Based

**Purpose:** Kiểm soát quyền dựa trên rule/nghiệp vụ phức tạp.

**Pattern:** `Module:Screen:FormAuthoritySec`

| Check level | Pattern | Ý nghĩa |
|---|---|---|
| Controller-level | `Module:Screen` | User access màn hình |
| Action-level | `Module:Screen:FormAuthoritySec` | User access action cụ thể |

```csharp
[Route("api/Management/User")]
[Authorize(Policy = "User:FXA00301")]   // Access permission cho FXA00301
public class UserController : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "User:FXA00301:1")]   // Full control cho FXA00301
    public async Task<IActionResult> CreateUser([FromBody] CreateUserModel model)
    {
        await _userService.CreateUserAsync(model);
        return Ok();
    }
}
```

✅ Policy cho permission chi tiết
✅ Decentralize check qua attribute
❌ KHÔNG check permission rải rác trong code
