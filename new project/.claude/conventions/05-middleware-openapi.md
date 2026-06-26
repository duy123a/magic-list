# 05. Middleware & OpenAPI

## 1. Middleware Pipeline Order

```csharp
public static void ConfigurePipeline(this WebApplication app, ApplicationConfig settings)
{
    // 1. Forward headers (proxy/load balancer)
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });

    // 2. Global exception (PHẢI ĐẦU TIÊN)
    app.UseMiddleware<OpassFabExceptionMiddleware>();

    // 3. HTTPS redirection
    app.UseHttpsRedirection();

    // 4. Routing + CORS
    app.UseRouting();
    app.UseCors(_cors);

    // 5. Authentication → Authorization (Auth phải trước AuthZ)
    app.UseAuthentication();
    app.UseAuthorization();

    // 6. Application middlewares
    app.UseMiddleware<ApplicationContextMiddleware>();
    app.UseMiddleware<ApiLoggingMiddleware>();

    // 7. Rate limiter
    app.UseRateLimiter();

    // 8. Map controllers
    app.MapControllers();

    // 9. OpenAPI (chỉ Dev/Staging)
    if (settings.OpenApi.EnableApiDocument)
    {
        app.AddOpenApiDocument(settings.OpenApi);
    }
}
```

## 2. Custom Middleware Pattern

```csharp
public class MyMiddleware
{
    private readonly RequestDelegate _next;

    public MyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Before request
        await _next(context);
        // After request
    }
}
```

✅ Exception middleware ĐẦU TIÊN
✅ Authentication TRƯỚC Authorization
✅ Middleware lightweight, không blocking
❌ KHÔNG xử lý business logic trong middleware

## 3. OpenAPI / Swagger

```csharp
// OpassFab.Api/OpenApi/OpenApiConfiguration.cs
services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "OpassFab.Api" });
    options.AddSecurityDefinition("Bearer", /* JWT config */);
    options.IncludeXmlComments(xmlFile);
});
```

✅ Enable Dev, disable Production
✅ Thêm XML comment cho Controller + DTO
✅ Configure security scheme (Bearer)
✅ Group API theo tag
