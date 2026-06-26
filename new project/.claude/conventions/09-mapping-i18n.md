# 09. Auto-Mapping & i18n (Localization)

**Purpose:**
- Tự động map data từ Entity (DB hoặc in-memory) sang DTO linh hoạt
- Cho phép truy vấn đa ngôn ngữ (`LocalizedDictionary`) qua LINQ mà không suy giảm performance

## Trách nhiệm

- Bind data `TSource` (Entity) → `TDest` (DTO) qua Expression Tree
- Mapping linh hoạt: cùng tên + cùng kiểu → tự động
- Tự động xử lý đa ngôn ngữ: lấy field từ `LocalizedDictionary` theo `langCd`
- Custom config qua `MapConfig`:
  - `Ignore` — bỏ qua property TDest
  - `MapFrom` — custom property mapping
- Che giấu tính toán đa ngôn ngữ thủ công, dùng attribute `[Localized]`

## Cách hoạt động

Mapper hoạt động trên Expression Tree:
1. **Khởi tạo** — Reflection lấy structure DTO
2. **Ráp biểu thức** — tạo expression assignment (auto-resolve multi-lang qua `[Localized]`)
3. **Thực thi**:
   - In-memory → `Compile()` thành Delegate
   - Database query (`ProjectToLocalized`) → EF xử lý expression tree

## Examples

### 1. Khai báo Entity (Domain layer)

```csharp
public class MMenu
{
    // ... other properties

    // Multilingual menu name (eg. "en-US": "Home", "ja-JP": "ホーム")
    public LocalizedDictionary MenuNames { get; set; } = new();
}
```

### 2. Localized DTO (Application layer)

```csharp
public class MenuModel : MMenu
{
    // [Localized] mapping với property "MenuNames"
    [Localized(nameof(MenuEntity.MenuNames))]
    public string MenuNameLocale { get; set; }
}
```

### 3. Query với multi-lang (database)

```csharp
// 1. Lấy lang code từ user context
string currentLang = _userContext.LangCd;

// 2. Định nghĩa query
var query =
    from master in _dbContext.Menus
    orderby master.Sort
    select master;

// 3. ProjectToLocalized — generate SQL + auto map sang DTO theo lang
var results = query.ProjectToLocalized<MMenu, MMenuModel>(langCd);
```

### 4. In-Memory Mapping

```csharp
// Single
var user = await _userManager.GetUserAsync(loginId);
var result = user.Map<User, DisplayUserModel>();

// List
var userList = await _userManager.GetAllUserAsync();
var result = userList.Map<User, DisplayUserModel>();
```

### 5. Custom Mapping

```csharp
var user = await _userManager.GetUserAsync(loginId);
var roles = user.UserRoles.Select(r => r.Role).ToList();
var defaultGroupCd = roles.FirstOrDefault(e => e.DefaultGroupFlg)?.Role.Name ?? string.Empty;

var result = user.Map<User, DisplayUserModel>(config =>
{
    // Custom map
    config.MapFrom(dest => dest.Roles, src => roles);
    config.MapFrom(dest => dest.DefaultGroupCd, src => defaultGroupCd);

    // Ignore
    config.Ignore(dest => dest.Status);
});
```

## Best Practices

✅ Lấy lang code từ HTTP Header → map qua `OpassFabLangCd` (constant class)
✅ `ProjectToLocalized` ở **bước cuối** trong LINQ context, trước `ToListAsync()` / `FirstOrDefaultAsync()`
✅ Field đa ngôn ngữ trong DB dùng `LocalizedDictionary`
✅ Tên field + kiểu nguồn ↔ đích phải khớp để auto-map

❌ KHÔNG dùng `ProjectToLocalized` sau khi đã execute (`ToList()` rồi project — sai)
❌ KHÔNG dùng "magic string" `"en-US"`, `"ja-JP"` — phải dùng `OpassFabLangCd.*`
❌ KHÔNG viết business logic trong `MapConfig`:
   ```csharp
   // ❌ BAD: Calculate sẽ là nút thắt giảm performance
   config.MapFrom(d => d.Value, s => Calculate(s));
   ```
