# Global Instructions

Ponytail Skill (Code Compression): Before writing any code, walk the six-rung laziness ladder (YAGNI, standard library, native platform, installed dependency, single-line) to write only the absolute minimum necessary code without skipping safety or validation.

Caveman Skill (Prose Compression): Compress every single response into ultra-terse, caveman-style prose by stripping all conversational filler, introductory remarks, and unnecessary explanations to maximize token savings.


# OPASSFAB BACKEND — CODING GUIDELINE

> **MANDATORY:** Trước khi viết/sửa bất kỳ code, BẮT BUỘC: (1) tra bảng "Index" để xác định file convention liên quan, (2) **đọc file đó** (không đoán từ tóm tắt), (3) tuân thủ nghiêm ngặt. Nếu user yêu cầu mâu thuẫn với convention → hỏi lại trước khi code.

---

## Stack

- **Framework:** .NET / ASP.NET Core (10.0.x)
- **Architecture:** Modular Monolith + Clean Architecture
- **Database:** PostgreSQL + Entity Framework Core (Npgsql provider)
- **Cache:** Redis (StackExchange.Redis)
- **Auth:** JWT Bearer (Keycloak integration)
- **Logging:** Serilog (structured)
- **Background:** Custom Job System (Async + Schedule)
- **Multi-tenant:** PostgreSQL schema-based isolation (SearchPath)

---

## Critical Rules — không được vi phạm

1. **Layer Dependency** ([01](.claude/conventions/01-project-structure.md))
   - `OpassFab.Api` → có thể tham chiếu Domain + Infrastructure
   - `{Domain}.Infrastructure` → chỉ tham chiếu Core Domain + Shared
   - `{Domain}` (Core) → CHỈ Shared.Module hoặc Contract; KHÔNG được biết Infrastructure/API
   - `Shared.Module` → KHÔNG tham chiếu gì (base layer)

2. **Controller chỉ là entry point** ([01](.claude/conventions/01-project-structure.md), [11](.claude/conventions/11-api-design.md))
   - KHÔNG chứa business logic; KHÔNG access DbContext trực tiếp
   - Chỉ gọi Service qua interface

3. **Service KHÔNG access DbContext trực tiếp** ([06](.claude/conventions/06-data-access.md))
   - Phải qua Repository hoặc UnitOfWork
   - Mọi DB operation đều `async`

4. **Repository KHÔNG chứa business logic** ([06](.claude/conventions/06-data-access.md))
   - Chỉ data access; business validation thuộc Service layer

5. **Đa ngôn ngữ KHÔNG hardcode lang code** ([09](.claude/conventions/09-mapping-i18n.md))
   - Dùng `_userContext.LangCd` + `OpassFabLangCd`; KHÔNG dùng magic string `"en-US"`/`"ja-JP"`

6. **Multi-tenant — luôn validate tenant từ JWT** ([07](.claude/conventions/07-multi-tenant-cache.md))
   - KHÔNG trust client-provided tenant; KHÔNG cross-tenant access
   - Cache key bắt buộc include `{TenantCode}`

7. **Concurrency — luôn return + gửi RV** ([11](.claude/conventions/11-api-design.md))
   - Mọi GET trả về `RV`; mọi PUT/PATCH/DELETE phải kèm `RV`
   - KHÔNG raw SQL UPDATE bypass concurrency

8. **Sensitive data → Environment Variables, KHÔNG commit** ([02](.claude/conventions/02-configuration.md))
   - Connection strings, JWT keys, API keys

9. **SQL Injection — luôn parameterized** ([08](.claude/conventions/08-transactions-rawsql.md))
   - KHÔNG string interpolation trong raw SQL; dùng `Dictionary<string, object>` parameters

10. **Exceptions** ([04](.claude/conventions/04-error-handling-logging.md))
    - Business error → `OpassFabApplicationException` với messageKey
    - Không throw generic `Exception`; không swallow exceptions

---

## File Naming Convention

| Suffix | Example |
|---|---|
| Controller | `XB01001Controller.cs` |
| Service / Interface | `XB01001Service.cs` / `IXB01001Service.cs` |
| Manager | `UserManager.cs` |
| Repository | `ProcessRepository.cs` |
| Context | `MasterContext.cs` |
| UnitOfWork | `MasterUnitOfWork.cs` |
| Exception | `OpassFabApplicationException.cs` |
| Middleware | `OpassFabExceptionMiddleware.cs` |
| Filter | `ValidationFilter.cs` |
| DTO | `XB01001Model.cs`, `XB01001UpdateModel.cs`, `XB01001ConditionModel.cs` |

- PascalCase, file name = class name, namespace = folder structure
- Interface bắt đầu bằng `I` prefix

---

## Module Structure (mỗi domain mới)

```
Module/{Domain}/
├── OpassFab.Module.{Domain}/                  # Core Domain
│   ├── Business/Services/                     # Service implementations
│   ├── Business/Interfaces/                   # Service interfaces
│   ├── Manager/                               # Manager interfaces (multi-service orchestration)
│   ├── Entities/                              # Domain entities (extend OpassFabDataModelBase)
│   ├── Dtos/                                  # DTOs (theo Screen ID)
│   ├── Repositories/                          # Repository interfaces
│   └── Persistences/                          # IUnitOfWork interfaces
│
├── OpassFab.Module.{Domain}.Contract/         # Public contracts cho module khác
│   └── I{Feature}ContractService.cs
│
└── OpassFab.Module.{Domain}.Infrastructure/   # Implementations
    ├── Persistences/                          # DbContext + UnitOfWork impl
    ├── Repositories/                          # Repository impl (Readable/ + Write qua UoW)
    ├── Manager/                               # Manager impl
    └── Startup.cs                             # DI registration
```

---

## Quick Reference — Patterns Hay Dùng

### Controller Template

```csharp
[ApiController]
[Route("api/sales/inquiry-estimation/aa03001")]   // lowercase + hyphen
[Authorize]
public class AA03001Controller(IAA03001Service service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AA03001ConditionModel condition)
        => Ok(await service.GetAsync(condition));

    [HttpPost]
    [Validation]
    public async Task<IActionResult> Register([FromBody] AA03001UpdateModel model)
        => Ok(await service.RegisterAsync(model));

    [HttpPut]
    [Validation]
    public async Task<IActionResult> Modify([FromBody] AA03001UpdateModel model)
        => Ok(await service.ModifyAsync(model));

    [HttpDelete]
    [Validation]
    public async Task<IActionResult> Delete([FromBody] AA03001DeleteModel model)
        => Ok(await service.DeleteAsync(model));
}
```

### Route Pattern

```
api/{module}/{feature}/{screen-id}
api/master/business/xb01001
api/sales/inquiry-estimation/aa03001
```

### DTO Convention

| DTO | Dùng cho | Vị trí |
|---|---|---|
| `{SCREEN_ID}Model` / `InitializeModel` | GET response | `Dtos/{SCREEN_ID}/` |
| `{SCREEN_ID}ConditionModel` | Query params (GET) | `Dtos/{SCREEN_ID}/` |
| `{SCREEN_ID}UpdateModel` | POST / PUT request body | `Dtos/{SCREEN_ID}/` |
| `{SCREEN_ID}DeleteModel` | DELETE request body | `Dtos/{SCREEN_ID}/` |

`UpdateModel` kế thừa `Model` và bổ sung field chỉ cần cho CUD (ví dụ: `AsDateOld` cho PUT). `RV` tự động có qua `OpassFabDataModelBase`.

### Transaction Pattern

```csharp
await _unitOfWork.BeginTransactionAsync();
try
{
    await _unitOfWork.Repository<TEntity>().UpdateAsync(entity);
    await _unitOfWork.SaveChangesAsync();
    await _unitOfWork.CommitAsync();
}
catch
{
    await _unitOfWork.RollbackAsync();
    throw;
}
```

### Cache Pattern

```csharp
var data = await _cacheService.GetOrSetCacheAsync(
    $"{_userContext.TenantCode}:m_country:{_userContext.LangCd}",
    async () => await _repository.GetAllAsync(),
    TimeSpan.FromMinutes(60));
```

### i18n Pattern

```csharp
// Entity
public LocalizedDictionary CountryName { get; set; } = new();

// DTO
[Localized(nameof(MCountry.CountryName))]
public string CountryNameLocale { get; set; } = string.Empty;

// Query
var results = query.ProjectToLocalized<MCountry, CountryModel>(_userContext.LangCd);
```

### Error & Logging Pattern

```csharp
// Business error
throw new OpassFabApplicationException("E00001", param);

// Logging — template, không interpolation
_logger.LogInfo("Processing {ClientCd}", clientCd);
_logger.LogError("Failed to save {OrderCd}", orderCd, exception);
```

### Raw SQL Pattern

```csharp
// ✅ Parameterized
var result = await _unitOfWork.QueryAsync<MyDto>(
    "SELECT * FROM my_table WHERE code = @code",
    new Dictionary<string, object> { ["code"] = code });

// ❌ KHÔNG BAO GIỜ — SQL Injection
var result = await _unitOfWork.QueryAsync<MyDto>($"SELECT * FROM my_table WHERE code = '{code}'");
```

---

## Index — đọc theo thứ tự khi cần

| # | File | Khi nào đọc |
|---|---|---|
| 01 | [Project Structure](.claude/conventions/01-project-structure.md) | Thêm module/feature/entity mới; quyết định đặt code ở đâu |
| 02 | [Configuration](.claude/conventions/02-configuration.md) | Thêm setting; cần đọc env var hoặc strongly-typed config |
| 03 | [Libraries & Packages](.claude/conventions/03-packages.md) | Chọn package; nâng version |
| 04 | [Error Handling & Logging](.claude/conventions/04-error-handling-logging.md) | Throw exception; viết log |
| 05 | [Middleware & OpenAPI](.claude/conventions/05-middleware-openapi.md) | Thêm middleware; cấu hình Swagger |
| 06 | [Data Access (Repo + UoW + DI)](.claude/conventions/06-data-access.md) | Mọi tác vụ DB; đăng ký service |
| 07 | [Multi-tenant & Cache](.claude/conventions/07-multi-tenant-cache.md) | Đụng đến tenant context; thêm Redis cache |
| 08 | [Transactions & Raw SQL](.claude/conventions/08-transactions-rawsql.md) | Cross-module transaction; cần raw SQL |
| 09 | [Mapping & i18n](.claude/conventions/09-mapping-i18n.md) | Map Entity ↔ DTO; field đa ngôn ngữ |
| 10 | [Reports & File Storage](.claude/conventions/10-reports-storage.md) | Generate report; upload/download file |
| 11 | [API Design](.claude/conventions/11-api-design.md) | Thiết kế endpoint; validation; row version |
| 12 | [Security](.claude/conventions/12-security.md) | Auth/AuthZ; rate limiting |
| 13 | [Job System](.claude/conventions/13-job-system.md) | Tạo background job (async / scheduled) |

---

## Workflow khi thêm code mới

1. **Xác định loại task** — module mới? feature mới? entity mới? sửa logic? → đọc [01](.claude/conventions/01-project-structure.md)
2. **Định vị layer đúng** — Controller / Service / Repository / Entity / DTO?
3. **Kiểm tra rule layer dependency** — không được reference ngược chiều
4. **Tham chiếu file convention tương ứng** từ index trên
5. **Áp dụng pattern có sẵn** — copy structure từ module/feature đã có (eg. `Module/Master` cho reference)
6. **Validate trước khi báo done:** naming, async/await, exception handling, concurrency (RV), tenant isolation, log

---

## Common Anti-patterns — TUYỆT ĐỐI tránh

- ❌ Inject `DbContext` trực tiếp vào Service (phải qua UnitOfWork)
- ❌ Synchronous DB calls (`.Result`, `.Wait()`) — luôn `async/await`
- ❌ Business logic trong Controller hoặc Repository
- ❌ Hardcode connection string, JWT key, tenant code, language code
- ❌ String interpolation trong raw SQL
- ❌ `new` service instance (luôn qua DI)
- ❌ Throw generic `Exception` hoặc swallow exceptions
- ❌ Log password, token, PII
- ❌ Quên `scope.Complete()` trong cross-module transaction
- ❌ Tự ý gán `RV` khi tạo entity mới
- ❌ Comment giải thích WHAT (code đã tự nói); chỉ comment WHY khi thực sự non-obvious

---

## Checklist Trước Khi Submit Code

- [ ] Không có `DbContext` inject trực tiếp vào Service
- [ ] Mọi DB call là `async/await` — không `.Result` / `.Wait()`
- [ ] Route lowercase + hyphen
- [ ] Controller không chứa business logic
- [ ] `RV` có trong GET response; PUT/DELETE gửi kèm `RV`
- [ ] Tenant lấy từ `_userContext` — không trust client
- [ ] Cache key có `TenantCode`
- [ ] Không hardcode lang code — dùng `_userContext.LangCd`
- [ ] Exception là `OpassFabApplicationException` — không throw `Exception` chung
- [ ] Không log sensitive data
- [ ] Parameterized query cho raw SQL — không string interpolation
- [ ] `[Authorize]` trên Controller
- [ ] File name = class name; namespace = folder path
- [ ] DI đã đăng ký trong `Startup.cs` của module
