# 06. Data Access — Repository, UnitOfWork, DI

## 1. Repository Pattern

**Purpose:** Tách data access khỏi business logic, dễ test/maintain.

```
IRepositoryBase<TEntity>           (Interface — Shared.Module)
        ↓
RepositoryBase<TContext, TEntity>  (Base impl — Shared.Infrastructure)
        ↓
CustomRepository                   (Specific impl — {Domain}.Infrastructure)
```

### 1.1. Base Repository Interface

```csharp
// Shared/OpassFab.Shared.Module/Repositories/IRepositoryBase.cs
public interface IRepositoryBase<TEntity> where TEntity : OpassFabDataModelBase, new()
{
    Task<List<TEntity>> GetAllAsync();
    Task<TEntity?> FindAsync(params object[] keyValues);
    Task<List<TEntity>> QueryAsync(Expression<Func<TEntity, bool>> condition);
    Task UpdateAsync(TEntity model);
    Task RemoveAsync(TEntity model);
    // ...
}
```

### 1.2. Base Implementation

```csharp
// Shared/OpassFab.Shared.Infrastructure/Repositories/RepositoryBase.cs
public abstract class RepositoryBase<TContext, TEntity> : IRepositoryBase<TEntity>
    where TEntity : OpassFabDataModelBase, new()
    where TContext : OpassFabContextBase
{
    protected readonly TContext _dbContext;
    protected readonly DbSet<TEntity> _dbSet;

    public RepositoryBase(TContext dbContext)
    {
        _dbContext = dbContext;
        _dbSet = dbContext.Set<TEntity>();
    }

    public virtual async Task<List<TEntity>> GetAllAsync()
        => await _dbSet.ToListAsync();

    public virtual async Task<TEntity?> FindAsync(params object[] keyValues)
        => await _dbSet.FindAsync(keyValues);

    public virtual async Task<List<TEntity>> QueryAsync(
        Expression<Func<TEntity, bool>> condition)
    {
        var query = _dbSet.AsQueryable().Where(condition);
        return await query.ToListAsync();
    }

    public virtual async Task UpdateAsync(TEntity model)
    {
        // Complex update logic (Added/Modified/Deleted)
        // Auto-set audit fields (CreatedAt, UpdateAt, ...)
        await UpdateAsync([model]);
    }
}
```

### 1.3. Custom Repository

```csharp
// Module/System/OpassFab.Module.System/Repositories/IUserRepository.cs
public interface IUserRepository : IRepositoryBase<User>
{
    Task<User?> GetUserAsync(string loginId, bool includeRoles = false);
    Task<bool> HasExistAsync(string loginId);
}

// Module/System/OpassFab.Module.System.Infrastructure/Repositories/UserRepository.cs
public class UserRepository : RepositoryBase<IdentityContext, User>, IUserRepository
{
    public UserRepository(IdentityContext context) : base(context) { }

    public async Task<User?> GetUserAsync(string loginId, bool includeRoles = false)
    {
        var query = _dbSet.AsQueryable();
        if (includeRoles)
            query = query.Include(u => u.UserRoles).ThenInclude(ur => ur.Role);
        return await query.FirstOrDefaultAsync(u => u.LoginId == loginId);
    }

    public async Task<bool> HasExistAsync(string loginId)
        => await _dbSet.AnyAsync(u => u.LoginId == loginId);
}
```

### 1.4. Service Usage

```csharp
public class UserService : IUserService
{
    private readonly IIdentityUnitOfWork _unitOfWork;

    public async Task<UserDto> GetUserAsync(string loginId)
    {
        var user = await _unitOfWork.User.GetUserAsync(loginId, includeRoles: true);
        if (user == null)
            throw new OpassFabApplicationException("E00001", loginId);
        return user.ToDto();
    }
}
```

✅ Repository chỉ data access logic
✅ Async/await cho mọi DB operation
✅ Override base method khi cần custom
✅ `Include()` cho eager loading
❌ KHÔNG chứa business logic trong Repository
❌ KHÔNG access DbContext trực tiếp trong Service (qua UoW)

## 2. UnitOfWork Pattern

**Purpose:** Quản lý transaction, đảm bảo consistency, tái sử dụng DbContext.

```
IUnitOfWorkBase                (Base interface — Shared.Module)
        ↓
UnitOfWorkBase<TContext>       (Base impl — Shared.Infrastructure)
        ↓
CustomUnitOfWork               (Module-specific impl)
```

### 2.1. Base Interface

```csharp
// Shared/OpassFab.Shared.Module/Persistences/IUnitOfWorkBase.cs
public interface IUnitOfWorkBase
{
    IRepositoryBase<TEntity> Repository<TEntity>() where TEntity : OpassFabDataModelBase, new();
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
}
```

### 2.2. Custom UnitOfWork

```csharp
// Module/System/OpassFab.Module.System/Persistences/IIdentityUnitOfWork.cs
public interface IIdentityUnitOfWork : IUnitOfWorkBase
{
    IUserRepository User { get; }
    IRoleRepository Role { get; }
    IUserAuthorityRepository UserAuthority { get; }
}

// Module/System/OpassFab.Module.System.Infrastructure/Persistences/IdentityUnitOfWork.cs
public class IdentityUnitOfWork : UnitOfWorkBase<IdentityContext>, IIdentityUnitOfWork
{
    private IUserRepository? _userRepository;
    private IRoleRepository? _roleRepository;
    private IUserAuthorityRepository? _userAuthorityRepository;

    public IdentityUnitOfWork(IdentityContext context) : base(context) { }

    public IUserRepository User =>
        _userRepository ??= new UserRepository(_dbContext);
    public IRoleRepository Role =>
        _roleRepository ??= new RoleRepository(_dbContext);
    public IUserAuthorityRepository UserAuthority =>
        _userAuthorityRepository ??= new UserAuthorityRepository(_dbContext);
}
```

### 2.3. Service Usage — Transaction Pattern

```csharp
public class UserService : IUserService
{
    private readonly IIdentityUnitOfWork _unitOfWork;

    public async Task<bool> CreateUserAsync(CreateUserModel model)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync();

            var user = model.ToEntity();
            await _unitOfWork.User.UpdateAsync(user);

            await _unitOfWork.SaveChangesAsync();      // auto-set audit fields
            await _unitOfWork.CommitAsync();

            return true;
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}
```

✅ UnitOfWork quản lý transaction
✅ Mỗi request 1 UoW instance (Scoped)
✅ Luôn commit hoặc rollback
✅ `SaveChangesAsync()` để auto-set audit field
❌ KHÔNG tạo nhiều UoW instance trong cùng request
❌ KHÔNG quên commit/rollback

## 3. Dependency Injection

**Purpose:** Loose coupling, testable, dễ mở rộng.

### 3.1. Lifetime

| Lifetime | Khi dùng | Ví dụ |
|---|---|---|
| `Singleton` | Stateless service, config | `ICacheService`, `IConfiguration`, `MutilTenantContextFactory` |
| `Scoped` | Per-request service | `DbContext`, UoW, Service, Repository |
| `Transient` | Lightweight, stateless | HttpClient (qua factory), utility |

### 3.2. Registration

```csharp
// Manual
services.AddScoped<IUserService, UserService>();
services.AddScoped<IUserRepository, UserRepository>();

// Auto-registration cho toàn module
services.ModuleServiceConfigure(typeof(IMasterUnitOfWork));
```

✅ Interface-based DI
✅ Scoped cho service & repository
✅ Singleton cho shared service
❌ KHÔNG inject `DbContext` trực tiếp (dùng UoW)
❌ KHÔNG `new` service instance manual
