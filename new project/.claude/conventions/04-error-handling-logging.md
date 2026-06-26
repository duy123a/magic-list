# 04. Error Handling & Logging

## 1. Error Handling

**Purpose:** Xử lý exception tập trung, response nhất quán, log chi tiết.

**Trách nhiệm:**
1. Middleware bắt mọi exception trong pipeline
2. Phân loại Business vs System
3. Log với context đầy đủ
4. Trả về HTTP response phù hợp

### 1.1. Global Exception Middleware

```csharp
// OpassFab.Api/Middlewares/OpassFabExceptionMiddleware.cs
public class OpassFabExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Serilog.ILogger _logger;

    public OpassFabExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
        _logger = Log.ForContext<OpassFabExceptionMiddleware>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var request = context.Request;
            _logger.LogError("サーバー内部で例外発生", ex);

            if (ex is UnauthorizedAccessException)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }

            if (ex is OpassFabBaseException iex)
            {
                if (!string.IsNullOrEmpty(iex.MessageKey) && !iex.MessageKey.StartsWith("E"))
                {
                    _logger.LogDebug("エラー以外はDebugログ出力", iex);
                }
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync("Internal Server Error");
        }
    }
}
```

**Đăng ký middleware ĐẦU TIÊN trong pipeline:**
```csharp
app.UseMiddleware<OpassFabExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseRouting();
// ... các middleware khác
```

✅ Register middleware đầu pipeline
✅ Log mọi exception với context (path, user, ...)
✅ HTTP status code phù hợp
✅ KHÔNG expose stack trace cho client trong Production
❌ KHÔNG bỏ qua exception (luôn log + handle)

### 1.2. Business vs System Exception

**Business Exception** — `OpassFabApplicationException`:

```csharp
public class OpassFabApplicationException : OpassFabBaseException
{
    public OpassFabApplicationException(string messageKey, params object[] arguments)
        : base(messageKey, arguments) { }
}

// Use
throw new OpassFabApplicationException("E00001", messageCode);
```

**System Exception** — DB/network/null reference, ...

| Exception Type | HTTP Status | Response |
|---|---|---|
| `OpassFabApplicationException` | 400 / 422 | Error message từ resource |
| `UnauthorizedAccessException` | 401 | Unauthorized |
| `NotFoundException` | 404 | Not Found |
| System Exception | 500 | Internal Server Error |

✅ Business → 4xx
✅ System → 5xx
✅ Dùng message key từ resource file
❌ KHÔNG throw generic `Exception`

### 1.3. Validation Error Handling

Validation error trả về theo format `ValidationError` (`MessageKey` + `Params`), dùng `ValidationErrorParam` để truyền tham số message.

**Common validation error** — trong custom validation attribute (`Shared/OpassFab.Shared.Module/Validations/`):

```csharp
// OpassFabStringLengthAttribute.cs
var error = CreateValidationErrorResult("WZ00080", "WZ00270", validationContext,
    new ValidationErrorParam(MaximumLength.ToString(), ValidationErrorParam.TypeText));
return new ValidationResult(error.ToString(), new[] { validationContext.MemberName! });
```

**Custom validation error** — trong DTO (`IValidatableObject` / custom validation):

```csharp
// Module/System/OpassFab.Module.System/Dtos/UserModel.cs
var error = new ValidationError()
{
    MessageKey = "WZ00020",
    Params = new[]
    {
        new ValidationErrorParam(context.DisplayName, ValidationErrorParam.TypeLabel)
    }
};
return new ValidationResult(error.ToString(), new[] { context.MemberName! });
```

### 1.4. Business Validation Error Handling

Khi cần trả về **danh sách lỗi business** theo format giống validation (không throw exception), dùng `ServiceResult<T>` + `AddValidationError`. Service gom lỗi → Controller convert sang `BadRequest` qua `ValidationErrorResponseFactory`.

**Service** — gom lỗi vào `ServiceResult`:

```csharp
// Module/Master/OpassFab.Module.Master/Business/Services/Business/XB25001Service.cs
var result = new ServiceResult<bool>();

if (model.ModelState == ModelStateEnum.Deleted)
{
    var parameters = new Dictionary<string, object> { ["param1"] = model.ProcessCd };
    var isUsed = await _masterUnitOfWork.ExecuteSingleAsync<bool>(
        "SELECT ipf_is_used_process_cd(@param1)", parameters);

    if (isUsed)
    {
        result.AddValidationError(nameof(model.ProcessCd), "WX00290",
            new ValidationErrorParam(nameof(model.ProcessCd), ValidationErrorParam.TypeLabel));
    }
}

result.AddValidationError(nameof(model.ProcessCd), "WX00200",
    new ValidationErrorParam(nameof(model.ProcessCd), ValidationErrorParam.TypeLabel));
result.AddValidationError(nameof(model.ProcessName), "WX00200",
    new ValidationErrorParam(nameof(model.ProcessName), ValidationErrorParam.TypeLabel));

if (!result.IsValid)
    return result;
```

**Controller** — convert `ServiceResult` → response:

```csharp
// OpassFab.Api/Controller/Master/Business/XB25001Controller.cs
var result = await _IXB25001Service.SampleUpdate(model);

if (!result.IsValid)
    return BadRequest(ValidationErrorResponseFactory.FromValidationResults(result.Errors));

return Ok(result.Data);
```

✅ Dùng `ServiceResult<T>` khi trả về nhiều lỗi business cùng lúc (không throw)
✅ `ValidationErrorParam` với type phù hợp (`TypeLabel`, `TypeText`, ...)
✅ Controller check `result.IsValid` → `ValidationErrorResponseFactory.FromValidationResults`
❌ KHÔNG throw `OpassFabApplicationException` khi cần trả về danh sách lỗi field-level

## 2. Logging

### 2.1. Log Level Guideline

| Level | Khi dùng | Ví dụ |
|---|---|---|
| `Debug` | Dev, troubleshooting | "Processing request for user {UserId}" |
| `Info` | Normal operation, business event quan trọng | "User {UserId} logged in" |
| `Warning` | Unexpected nhưng đã handle | "Cache miss for key {Key}" |
| `Error` | Exception, failure | "Failed to save user", exception detail |
| `Fatal` | Critical, app cannot continue | "DB connection lost" |

✅ Info cho business event quan trọng
✅ Warning cho recoverable error
✅ Error cho exception
❌ KHÔNG log quá nhiều ở Info
❌ KHÔNG log sensitive data

### 2.2. Structured Logging với Serilog

Custom enricher tự động thêm context (tenant, user, request, ...):

```csharp
// Shared/OpassFab.Shared.Infrastructure/Utils/SeriLogProvider.cs
public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
{
    var httpContext = HttpContextProvider.Current;
    var userContext = HttpContextProvider.UserContext;

    var tenant = userContext?.RealmName ?? string.Empty;
    var loginId = userContext?.LoginId ?? string.Empty;
    var clientIp = userContext?.ClientIpAddress ?? httpContext?.GetClientIp() ?? string.Empty;
    var requestId = httpContext?.TraceIdentifier ?? string.Empty;
    var method = string.Format("{0,-6}{1}",
        httpContext?.Request.Method,
        httpContext?.Request.Path + httpContext?.Request.QueryString);

    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("SystemName", _systemName));
    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("ServerName", _serverName));
    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("ProcessId", _processId));
    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("Version", _version));
    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("LogType", "Application Log"));
    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("LogDataType", ELogDataType.Data));
    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("ClientIp", clientIp));
    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("Tenant", tenant));
    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("LoginId", loginId));
    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("RequestId", requestId));
    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("Method", method));
}
```

**Output template:**
```
{Timestamp:yyyy-MM-dd HH:mm:ss.fff} | System={SystemName} | Server={ServerName} |
ProcessId={ProcessId} | Version={Version} | LogType={LogType} | LogDataType={LogDataType} |
Level=[{Level:u3}] | ClientIp={ClientIp} | Tenant={Tenant} | User={LoginId} |
Id={RequestId} | Method={Method} | Elapsed={ElapsedTime} | Message={Message} |
Exception={ExceptionMessage} | ExceptionTrace={ExceptionTrace}{NewLine}
```

**Initialize:**
```csharp
SeriLogProvider.Initialize(builder.Environment);
```

✅ Structured logging với property
✅ Include context (UserId, Tenant, RequestId, ...)
✅ Consistent property name
❌ KHÔNG dùng string interpolation trong log message — dùng template `"{UserId}"`
