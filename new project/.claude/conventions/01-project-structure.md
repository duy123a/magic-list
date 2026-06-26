# 01. Project Structure

## Architecture — Modular Monolith + Clean Architecture

```
PRESENTATION LAYER
  OpassFab.Api
  Controllers, Filters, Middlewares, OpenApi, Policies
                    ↓
BUSINESS LOGIC LAYER
  Module/{Domain}
    {Domain}                  {Domain}.Contract           {Domain}.Infrastructure
    Entities, Services, DTOs  Public Interfaces           DbContext, Implementations
    Repositories (interfaces)
                    ↓
CROSS-CUTTING CONCERNS
  Shared
    Shared.Module                                     Shared.Infrastructure
    Base Entities, Utils, Validations                 DbContext Base, Cache, Services
```

**Nguyên tắc:**
- **SoC** — mỗi module độc lập theo domain
- **Dependency Inversion** — Infrastructure phụ thuộc Domain, không ngược lại
- **Single Responsibility** — mỗi layer trách nhiệm rõ ràng
- **DRY** — Shared component được tái sử dụng

## Layer Detail

### A. `OpassFab.Api` (Presentation)

```
OpassFab.Api/
├── Controller/         # API Controllers (Admin, Management, Master, Sales)
├── Filters/            # Action filters (validation, ...)
├── Middlewares/        # Exception, logging, context
├── OpenApi/            # Swagger config
├── Policies/           # Authorization policies
├── appsettings.json
├── Program.cs
└── Startup.cs
```

**Trách nhiệm:**
- Route HTTP request → service
- Validate input
- Convert DTO ↔ HTTP response
- Auth/AuthZ
- Global error handling

✅ Controller đơn giản, ủy thác cho Service
✅ Attribute routing `[HttpGet]`, `[HttpPost]`, ...
✅ HTTP status code phù hợp
❌ KHÔNG implement business logic
❌ KHÔNG access DB trực tiếp

```csharp
[Route("api/Sales")]
[ApiController]
public class SalesController : ControllerBase
{
    private readonly ISalesService _salesService;
    public SalesController(ISalesService salesService)
    {
        _salesService = salesService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var salesInfos = await _salesService.GetAllAsync();
        return Ok(salesInfos);
    }
}
```

### B. `Module/{Domain}` (Business Domain)

```
Module/{Domain}/
├── OpassFab.Module.{Domain}/              # Core Domain Layer
│   ├── Business/
│   │   ├── Services/                      # Service implementations
│   │   ├── Interfaces/                    # IService interfaces
│   │   └── Contract/                      # (optional) cross-module service contracts
│   ├── Entities/                          # Domain entities → DB tables
│   ├── Dtos/                              # DTOs
│   ├── Repositories/                      # IRepository (interfaces only)
│   ├── Persistences/                      # IUnitOfWork (interfaces only)
│   └── Manager/                           # IManager (orchestration interfaces)
│
├── OpassFab.Module.{Domain}.Contract/     # Public contracts cho module khác
│   └── I{Feature}ContractService.cs
│
└── OpassFab.Module.{Domain}.Infrastructure/
    ├── Persistences/                      # DbContext + UoW impl
    ├── Repositories/                      # Repository impl
    ├── Manager/                           # Manager impl + external integration
    └── Startup.cs                         # DI registration
```

#### Service vs Manager

| | Service | Manager |
|---|---|---|
| Ý nghĩa | Logic nghiệp vụ + DTO mapping | Điều phối nhiều service nghiệp vụ phức tạp |
| Khi dùng | CRUD, business rules cho 1 feature | Quy trình phức tạp, tích hợp external (eg. Keycloak sync) |
| Example | `FAB01001Service` xử lý SalesOrder | `UserManager` CRUD DB + sync Keycloak |

✅ Service chỉ gọi Manager/Repository, KHÔNG access DbContext trực tiếp
✅ Business validation ở Service
✅ DTO mapping ở Service
✅ Dùng UnitOfWork để save changes
❌ Truy cập DB trực tiếp
❌ Chứa data access code
❌ HTTP-related code

#### Entities/

- Kế thừa `OpassFabDataModelBase` (audit fields)
- Map trực tiếp với DB table
- Là ORM entity (không chứa logic phức tạp)

#### Dtos/

- Tách khỏi entity để không lộ cấu trúc internal
- Là data shape cho view/update qua API

#### Repositories/ và Persistences/ (Core Domain)

- **Chỉ chứa interface** — implementation nằm ở Infrastructure
- `IRepositoryBase<TEntity>` — base contract
- `IUnitOfWork` — transaction + repository registry

### C. `{Domain}.Contract` (Public Contracts)

- Định nghĩa public interface cho module khác sử dụng
- KHÔNG chứa implementation
- KHÔNG reference layer nào

**Ví dụ — Sales cần gọi Stock:**
1. Module Stock: định nghĩa `IStockContractService` + DTO ở `OpassFab.Module.Stock.Contract`
2. Module Stock: implement trong `OpassFab.Module.Stock/Business`
3. Module Sales: reference `IStockContractService` và gọi qua DI
4. Nếu Stock chưa implement → mock data tại Sales (theo interface) để không bế tắc

### D. `Shared` (Cross-cutting Concerns)

```
Shared/
├── OpassFab.Shared.Module/             # Shared Domain
│   ├── BaseEntities/                   # OpassFabDataModelBase
│   ├── Configuration/                  # Config models
│   ├── Consts/                         # Constants & resource keys
│   ├── Persistences/                   # Base persistence interfaces
│   ├── Repositories/                   # Base repository interfaces
│   ├── Services/                       # Shared service interfaces
│   ├── Utils/                          # Utility classes
│   └── Validations/                    # Validation rules
│
└── OpassFab.Shared.Infrastructure/     # Shared Infrastructure
    ├── Persistences/                   # Base DbContext, UoW
    ├── Repositories/                   # Base repository impl
    ├── Services/                       # Cache impl, logging, ...
    └── Utils/
```

✅ Chỉ chứa code thực sự dùng chung
✅ Tránh circular dependency Shared ↔ Module
❌ KHÔNG đặt business logic cụ thể vào Shared

### Master Module — Shared Business Logic Layer

Master data (dữ liệu chung) sẽ được expose từ `Module/Master`. Các module nghiệp vụ ĐƯỢC PHÉP reference Master Module (đây là exception cho dependency rule, vì lịch sử thiết kế từ iPLAET không theo DDD pattern).

## File Naming Rules

### Class Names — PascalCase, suffix theo convention

| Suffix | Example |
|---|---|
| Controller | `UserController` |
| Service | `UserService` |
| Manager | `UserManager` |
| Repository | `UserRepository` |
| Context | `IdentityContext` |
| UnitOfWork | `IdentityUnitOfWork` |
| Exception | `OpassFabApplicationException` |
| Middleware | `OpassFabExceptionMiddleware` |
| Filter | `ValidationFilter` |

### File Names
- PascalCase
- 1 file = 1 class (trừ nested)
- File name = class name; namespace = folder path

### Interface Names
- Bắt đầu `I` prefix: `IUserService`, `IProductRepository`

## Folder Responsibility — bảng cấm/cho

| Folder | Trách nhiệm | KHÔNG được chứa |
|---|---|---|
| `Controller/` | HTTP request/response | Business logic, DB access |
| `Business/Services/` | Core business logic | Direct DB query, HTTP concern |
| `Business/Interfaces/` | Service interfaces | Implementations |
| `Repositories/` (Domain) | Data access abstraction | Business validation, complex query |
| `Entities/` | Domain models, business rules | DTO, view model, API contract |
| `Dtos/` | Data transfer | Business logic, DB mapping |
| `Manager/` | Multi-service orchestration | Simple CRUD (dùng Service) |
| `Persistences/` | DbContext, UoW, configuration | Business logic, repositories |
| `Infrastructure/` | Implementation của interface | Domain logic, entity |
| `Shared/` | Cross-cutting utility | Domain-specific logic |

## Dependency Rules

```
OpassFab.Api (highest layer)
    ↓ references all modules
{Domain}.Infrastructure
    ↓ references Core Domain + Shared
{Domain} (Core)
    ↓ references Shared.Module + (optional) Contract
Shared.Infrastructure
    ↓ references Shared.Module only
Shared.Module (lowest layer)
    ↓ references nothing
```

| Layer | CAN reference | CANNOT reference |
|---|---|---|
| `OpassFab.Api` | All modules (Domain + Infrastructure) | — |
| `{Domain}.Infrastructure` | Core Domain, Shared | Other Infrastructure layers, API |
| `{Domain}` (Core) | `Shared.Module`, optionally `{OtherDomain}.Contract` | Infrastructure, API, other Domains |
| `Shared.Infrastructure` | `Shared.Module` only | Domain modules, API |
| `Shared.Module` | None | Everything |

## Hướng dẫn thêm Module mới

1. **Tạo folder structure:**
   ```
   Module/NewDomain/
   ├── OpassFab.Module.NewDomain/
   │   ├── Business/{Services,Contract}/
   │   ├── Entities/
   │   ├── Dtos/
   │   ├── Repositories/
   │   └── Persistences/
   ├── OpassFab.Module.NewDomain.Contract/
   │   └── INewDomainContractService.cs
   └── OpassFab.Module.NewDomain.Infrastructure/
       ├── Persistences/
       ├── Repositories/
       └── Startup.cs
   ```

2. **Set project references** (.csproj):
   - `Module → Shared.Module`
   - `Module.Infrastructure → Module, Shared.Infrastructure`

3. **Tạo `Startup.cs` trong Infrastructure:**
   ```csharp
   public static class Startup
   {
       public static IServiceCollection ConfigureNewDomainService(
           this IServiceCollection services,
           IWebHostEnvironment env)
       {
           services.DbContextConfigure<NewDomainContext>();
           services.PersistencesConfigure<INewDomainUnitOfWork, NewDomainUnitOfWork>();
           services.ModuleServiceConfigure(typeof(INewDomainUnitOfWork));
           return services;
       }
   }
   ```

4. **Đăng ký vào `Program.cs`:**
   ```csharp
   builder.Services.ConfigureNewDomainService(builder.Environment);
   ```

5. **Tạo Controller trong Api:**
   ```csharp
   [Route("api/NewDomain")]
   public class NewDomainController : ControllerBase { }
   ```

## Hướng dẫn thêm Feature mới

1. **Xác định module** — eg. "Sales Order Management" → `Module.Sales`

2. **Entity** (nếu cần):
   ```csharp
   // Module/Sales/OpassFab.Module.Sales/Entities/SalesOrder.cs
   public class SalesOrder : OpassFabDataModelBase
   {
       public string Code { get; set; }
       public string Name { get; set; }
   }
   ```

3. **Repository Interface:**
   ```csharp
   // Module/Sales/OpassFab.Module.Sales/Repositories/ISalesOrderRepository.cs
   public interface ISalesOrderRepository : IRepositoryBase<SalesOrder>
   {
       Task<SalesOrder?> GetSalesOrderAsync(string code);
   }
   ```

4. **Repository Implementation:**
   ```csharp
   // Module/Sales/OpassFab.Module.Sales.Infrastructure/Repositories/SalesOrderRepository.cs
   public class SalesOrderRepository : RepositoryBase<SalesContext, SalesOrder>, ISalesOrderRepository
   {
       public SalesOrderRepository(SalesContext context) : base(context) { }

       public async Task<SalesOrder?> GetSalesOrderAsync(string code)
           => await _dbSet.FirstOrDefaultAsync(p => p.Code == code);
   }
   ```

5. **Service:**
   ```csharp
   // Module/Sales/OpassFab.Module.Sales/Business/Services/SalesOrderService.cs
   public class SalesOrderService : ISalesOrderService
   {
       private readonly ISalesOrderRepository _salesOrderRepository;

       public SalesOrderService(ISalesOrderRepository salesOrderRepository)
       {
           _salesOrderRepository = salesOrderRepository;
       }

       public async Task<SalesOrderDto> GetSalesOrderAsync(string code)
       {
           var salesOrder = await _salesOrderRepository.GetSalesOrderAsync(code);
           if (salesOrder == null)
               throw new OpassFabApplicationException("E00001", code);
           return new SalesOrderDto { /* mapping */ };
       }
   }
   ```

6. **Controller:**
   ```csharp
   [Route("api/Sales/SalesOrder")]
   public class SalesOrderController : ControllerBase
   {
       private readonly ISalesOrderService _salesOrderService;

       public SalesOrderController(ISalesOrderService salesOrderService)
       {
           _salesOrderService = salesOrderService;
       }

       [HttpGet("{code}")]
       [Validation]
       public async Task<IActionResult> GetSalesOrder(string code)
       {
           var salesOrder = await _salesOrderService.GetSalesOrderAsync(code);
           return Ok(salesOrder);
       }
   }
   ```

## Hướng dẫn thêm Table / Entity

Dự án dùng EF Core ORM. Khi thêm bảng mới, phải tạo Entity class + Configuration.

### 1. Tạo bảng trong Postgres + dùng Scaffold

`Microsoft.EntityFrameworkCore.Design` đã được cài trong `OpassFab.Shared.Infrastructure` với `<PrivateAssets>all</PrivateAssets>` (chỉ dành cho tool, không publish output).

```bash
# Cài tool dotnet-ef nếu chưa có
dotnet tool install --global dotnet-ef

# Di chuyển vào project chứa OpassFab.Shared.Infrastructure.csproj
cd backend/Shared/OpassFab.Shared.Infrastructure

# Scaffold toàn bộ schema
dotnet ef dbcontext scaffold "Host=localhost;Database=postgres;Username=postgres;Password=postgres" \
  Npgsql.EntityFrameworkCore.PostgreSQL --schema tenant1 -o Entities

# Hoặc scaffold 1 bảng cụ thể
dotnet ef dbcontext scaffold "..." Npgsql.EntityFrameworkCore.PostgreSQL --table tenant1.m_menu -o Entities
```

### 2. Di chuyển Entity vào Module đúng

- Xác định Entity thuộc module nào → `OpassFab.Module.{ModuleName}/Entities`
- Đổi namespace tương ứng

### 3. Tạo Entity Configuration

Đặt tại `OpassFab.Module.{ModuleName}.Infrastructure/Persistences/Configuration/`:

```csharp
internal class MMenuConfiguration : IEntityTypeConfiguration<MMenu>
{
    public void Configure(EntityTypeBuilder<MMenu> builder)
    {
        builder.HasKey(e => e.MenuCd).HasName("pk_m_menu");
        builder.ToTable("m_menu");

        builder.Property(e => e.MenuCd).HasColumnName("menu_cd").HasMaxLength(6);
        builder.Property(e => e.BatchUpdatePg).HasColumnName("batch_update_pg").HasMaxLength(50);

        // JSONB localized field
        builder.Property(e => e.MenuName)
            .HasColumnName("menu_name")
            .HasColumnType("jsonb")
            .HasConversion(
                e => JsonSerializer.Serialize(e),
                e => JsonSerializer.Deserialize<LocalizedDictionary>(e)!);

        // Audit fields
        builder.Property(e => e.CreatedIP).HasColumnName("created_ip").HasMaxLength(50);
        builder.Property(e => e.CreatedPg).HasColumnName("created_pg").HasMaxLength(50);
        builder.Property(e => e.CreatedUserCd).HasColumnName("created_user_cd").HasMaxLength(10);
        builder.Property(e => e.UpdateIP).HasColumnName("update_ip").HasMaxLength(50);
        builder.Property(e => e.UpdatePg).HasColumnName("update_pg").HasMaxLength(50);
        builder.Property(e => e.UpdateUserCd).HasColumnName("update_user_cd").HasMaxLength(10);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
        builder.Property(e => e.UpdateAt).HasColumnName("update_at").HasColumnType("timestamp without time zone");
        builder.Property(e => e.BatchUpdateAt).HasColumnName("batch_update_at").HasColumnType("timestamp without time zone");
    }
}
```

### 4. Xóa output Scaffold trong Shared.Infrastructure

Xóa folder/file scaffold tạo ra trong `OpassFab.Shared.Infrastructure/Entities/` để tránh duplicate code.
