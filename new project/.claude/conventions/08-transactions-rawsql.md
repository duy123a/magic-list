# 08. Transactions & Raw SQL

## 1. Cross-Module Transactions

**Purpose:** Đảm bảo ACID khi cập nhật dữ liệu qua nhiều module (Sales, Stock, Master, ...).

- Mỗi module có DbContext riêng → cần đồng bộ commit/rollback
- Dùng `TransactionScope` (DTC) để quản lý cross-module transaction
- `UnitOfWorkBase` tự động phát hiện ambient transaction và enlist
- Contract service đóng vai trò interface giao tiếp giữa module

### 1.1. Cấu trúc folder

```
Module/
├── Sales/
│   ├── Business/Services/
│   │   └── CrossTransactionSampleService.cs       # Service cross-module
│   └── Contract/                                   # Contract interface
│
└── Stock/
    ├── Business/Contracts/
    │   └── StockContractSampleService.cs           # Contract impl
    └── Contract/
        └── IStockContractSampleService.cs          # Contract interface
```

### 1.2. Code Example

```csharp
public CrossTransactionSampleService(
    ISalesUnitOfWork salesUnitOfWork,
    IStockContractSampleService stockContractService,
    ITInquiryEstimationNoteRepository inquiryEstimationNoteRepository)
{
    _salesUnitOfWork = salesUnitOfWork;
    _stockContractService = stockContractService;
    _inquiryEstimationNoteRepository = inquiryEstimationNoteRepository;
}

public async Task<bool> UpdateAsync(CrossTransactionRequestModel model)
{
    using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
    {
        try
        {
            // 1. Sales module — update sales data
            await UpdateInquiryEstimationNoteAsync(
                model.InquiryEstimationNo,
                model.SalesNoteString1,
                model.SalesNoteString2);

            // 2. Stock module — update stock data qua contract
            await _stockContractService.UpdateStockTransferMenoAsync(
                model.StockTransferNo,
                model.StockTransferMemo);

            // Commit all
            scope.Complete();
            return true;
        }
        catch (Exception)
        {
            // Rollback automatically (Complete chưa gọi)
            throw;
        }
    }
}
```

### 1.3. Sequence Flow

```
TransactionScope (Ambient Transaction)

1. UpdateInquiryEstimationNoteAsync()
   └─> SalesUnitOfWork.BeginTransactionAsync()
       ├─> Detect Transaction.Current != null
       ├─> EnlistTransaction(Transaction.Current)
       ├─> Update Sales DB
       └─> SaveChangesAsync() + CommitAsync()

2. _stockContractService.UpdateStockTransfer...()
   └─> StockUnitOfWork.BeginTransactionAsync()
       ├─> Detect Transaction.Current != null
       ├─> EnlistTransaction(Transaction.Current)
       ├─> Update Stock DB
       └─> SaveChangesAsync() + CommitAsync()

3. scope.Complete() → Commit ALL
   Nếu có exception hoặc Complete() không gọi → Rollback ALL
```

**Lưu ý:**
- Distributed transaction (DTC) có overhead — chỉ dùng khi cần thiết
- Nếu chỉ trong 1 module → dùng single transaction, không DTC
- Tránh long-running transaction

✅ Luôn `TransactionScopeAsyncFlowOption.Enabled` cho cross-module
✅ Phải gọi `scope.Complete()` để commit
✅ Contract service đăng ký DI ở module tương ứng
✅ Module chỉ expose contract interface, không expose impl
✅ Contract đơn giản, focus vào business logic
❌ Contract KHÔNG expose internal entity — dùng DTO

## 2. Raw SQL — Patterns

**Purpose:** Thực thi raw SQL an toàn và đồng nhất, tận dụng helper trong `IRepositoryBase` và `IUnitOfWorkBase`.

### 2.1. Query trả về Entity qua Repository cụ thể

Dùng khi: Query phức tạp trên 1 entity, EF LINQ không đủ.

```csharp
public async Task<List<DisplayUserModel>> ExcuteSqlAsync()
{
    var parameters = new Dictionary<string, object>
    {
        ["enabled"] = true,
    };

    // QueryAsync qua _userRepository (extends IRepositoryBase<User>)
    var users = await _userRepository.QueryAsync(
        "SELECT * FROM \"user\" WHERE enabled = @enabled",
        parameters
    );

    return users.Select(u => u.ToDisplayModel()).ToList();
}
```

✅ Dùng `Dictionary` cho parameter (parameterized query, ngừa SQL injection)
✅ Dùng khi LINQ không đủ cho query phức tạp trên entity
❌ String interpolation `$"... {userId}"` — SQL Injection
❌ Sai repository: dùng `IUserRepository` để query bảng `Role`
❌ Chọn thừa data: `SELECT a, b FROM ...` map vào `User` với đa số field null → dùng DTO

### 2.2. Query phức tạp / Map sang DTO qua UnitOfWork

Dùng khi: JOIN nhiều bảng, GROUP BY, window function, cần map sang DTO bất kỳ.

```csharp
public async Task<DisplayUserModel?> Sample2ExcuteSqlAsync(User user)
{
    var roleParameters = new Dictionary<string, object>
    {
        ["user_id"] = user.Id,
    };

    // Map sang DTO bất kỳ
    var roles = await _unitOfWork.QueryAsync<DisplayRoleModel>(
        @"SELECT id, name, name_display as nameDisplay FROM role
          LEFT JOIN user_role ON role.id = user_role.role_id
          WHERE user_role.user_id = @user_id",
        roleParameters
    );

    var userModel = user.ToDisplayModel();
    userModel.Roles = roles;
    return userModel;
}
```

✅ Dùng UoW khi map sang DTO (không phải entity gốc)
✅ Alias cột (`SELECT name_display AS nameDisplay`) khớp property DTO
❌ Tránh map JOIN thủ công bằng nhiều vòng for thay vì JOIN trên SQL

### 2.3. Query Scalar / Single-row

Dùng khi: COUNT, MAX, MIN, function, single-row.

```csharp
public async Task CheckPostgresVersionAsync()
{
    string postgresVersion = await _unitOfWork.ExecuteSingleAsync<string>("SELECT version()");
}
```

### 2.4. Execute SQL Command (INSERT/UPDATE/DELETE)

Dùng khi: Bulk update hiệu năng cao, không track theo entity.

```csharp
public async Task ExcuteSqlAsync()
{
    var parameters = new Dictionary<string, object>
    {
        ["user_name_kana"] = "Admin",
        ["login_id"] = "admin",
    };

    var affectedRows = await _unitOfWork.ExecuteSqlCommandAsync(
        "UPDATE \"user\" SET user_name_kana = @user_name_kana WHERE login_id = @login_id",
        parameters
    );
}
```

**Đảm bảo transaction:** `ExecuteSqlCommandAsync` chia sẻ chung `DbConnection` với EF Core trên cùng UoW. Nếu gọi giữa `BeginTransactionAsync()` và `CommitAsync()`, raw SQL DML cũng sẽ rollback an toàn.

✅ Luôn parameter binding bằng `Dictionary` để ngừa SQL injection
✅ Khi UPDATE/DELETE nhiều dòng + cần lấy schema metadata → `_unitOfWork.GetEntityMetadata<T>()` thay vì hardcode tên table/schema
