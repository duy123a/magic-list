# 02. App Configuration

**Purpose:** Quản lý cấu hình theo environment, đảm bảo bảo mật và dễ maintain.

**Nguyên tắc:**
- **Environment-based** — mỗi env có file config riêng
- **Hierarchical** — `appsettings.json` → `appsettings.{Env}.json` → Environment Variables
- **Security** — sensitive data KHÔNG commit vào git
- **Type-safe** — dùng strongly-typed configuration class

## Configuration Hierarchy

```
OpassFab.Api/
├── appsettings.json                  # Base config — default values
├── appsettings.Development.json      # Dev overrides
├── appsettings.Staging.json          # Staging overrides
└── appsettings.Production.json       # Production overrides
```

| Priority | Source | Description |
|---|---|---|
| 1 (highest) | Environment Variables | Runtime, dùng cho sensitive (DB password, API key) |
| 2 | `appsettings.{Environment}.json` | Per-env override |
| 3 | `appsettings.json` | Default values |

## Environment Variable Format

```
Section__SubSection__Key
```

Examples:
```
Database__ConnectionString
JwtSettings__SecretKey
RedisCache__ConnectionString
```

### `appsettings.json` template

```json
{
  "SystemName": "OpassFab",
  "Version": "0.0.0.1",
  "OpenApi": {
    "EnableApiDocument": true,
    "WebOpenApiUrl": "/swagger/v1/swagger.json"
  },
  "Database": {
    "ConnectionString": ""
  },
  "JwtToken": {
    "Issuer": "OpassFab",
    "Audience": "OpassFab",
    "IssuerSigningKey": ""
  },
  "RedisCache": {
    "ConnectionString": "",
    "DefaultExpirationMinutes": 60
  }
}
```

> Sensitive field (`ConnectionString`, `IssuerSigningKey`, ...) để rỗng trong file → load từ env var.

## Strongly-typed Configuration

### 1. Định nghĩa config class

```csharp
// Shared/OpassFab.Shared.Module/Configuration/Application/ApplicationConfig.cs
public class ApplicationConfig
{
    public string SystemName { get; set; } = string.Empty;
    public OpenApiConfig OpenApi { get; set; } = new();
    public DatabaseConfig Database { get; set; } = new();
    public JwtTokenConfig JwtToken { get; set; } = new();
    public RedisCacheConfig RedisCache { get; set; } = new();
}
```

### 2. Load config trong `Program.cs`

```csharp
var appConfig = builder.Configuration.Get<ApplicationConfig>();
AppConfigProvider.Initialize(appConfig);
```

### 3. Sử dụng

```csharp
var config = AppConfigProvider.Instance;
var connectionString = config.Database.ConnectionString;
```

✅ Dùng strongly-typed config class
✅ Validate required field
✅ Provide default value
❌ KHÔNG hardcode config trong code
❌ KHÔNG đọc `IConfiguration` trực tiếp (trừ khi cần dynamic)

## Environment Variables — Sensitive Data

```bash
# Windows PowerShell
$env:Database__ConnectionString = "Host=localhost;Database=OpassFab;..."

# Linux / macOS
export Database__ConnectionString="Host=localhost;Database=OpassFab;..."
```

```yaml
# docker-compose.yml
environment:
  - Database__ConnectionString=Host=db;Database=OpassFab;...
  - JwtToken__IssuerSigningKey=your-secret-key
```

✅ Password, key, secret → env var
✅ Connection string → env var
✅ API key → env var
❌ KHÔNG commit `.env` vào git
❌ KHÔNG log sensitive config value
