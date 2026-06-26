# 10. Reports & File Storage

## 1. Get Template Report

**Purpose:** Cấu hình & truy xuất Report Template từ Azure Blob Storage theo cấu trúc phân tầng (Multi-tenant + Multi-language).

**Trách nhiệm:**
- **Multi-tenant config** — ưu tiên template chuyên biệt per tenant
- **Localization** — tự động tìm template theo lang code
- **Tối ưu storage** — trừu tượng hóa Cloud (Azure Blob), code không phụ thuộc path vật lý

### 1.1. Hierarchical Fallback (4 cấp)

Cơ chế fallback ưu tiên từ trên xuống:

1. **Tenant Localized** — file theo lang trong container của riêng Tenant
2. **Common Localized** — file theo lang trong container `common`
3. **Tenant Default** — file mặc định trong container Tenant (không lang)
4. **Common Default** — file gốc mặc định trong container `common`

**Database Driven:**
- Path tương đối lưu trong DB (`MReportConfig`)
- `ReportConfigReadableRepository` lấy thông tin → quyết định path nào

**Stream-based:**
- API trả `System.IO.Stream` (`MemoryStream`) — không trung chuyển qua disk

### 1.2. Service Flow

**Business Layer:**

```csharp
// Module/Master/OpassFab.Module.Master/Business/Services/ReportSampleService.cs
public class ReportSampleService : IReportSampleService
{
    private readonly IUserContext _userContext;
    private readonly IReportTemplateService _reportTemplateService;
    private readonly IReportConfigReadableRepository _reportConfigRepository;

    public ReportSampleService(
        IUserContext userContext,
        IReportTemplateService reportTemplateService,
        IReportConfigReadableRepository reportConfigRepository)
    {
        _userContext = userContext;
        _reportTemplateService = reportTemplateService;
        _reportConfigRepository = reportConfigRepository;
    }

    public async Task<(ReportConfigModel? ReportConfig, Stream? FileStream)> GetReportAsync(string reportId)
    {
        // 1. Fetch config từ DB
        var reportConfig = await _reportConfigRepository.GetReportConfigAsync(reportId);

        // 2. Fetch file stream — auto fallback (Tenant+Lang → Common+Lang → Tenant → Common)
        var fileStream = await _reportTemplateService.GetReportTemplateAsync(
            _userContext.RealmName,
            reportConfig.ReportTemplateFilePath,
            _userContext.LangCd);

        return (reportConfig, fileStream);
    }
}
```

**Controller — Trả về File:**

```csharp
[ApiController]
[Route("api/ReportSample")]
public class ReportSampleController : ControllerBase
{
    private readonly IReportSampleService _reportService;

    public ReportSampleController(IReportSampleService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet]
    public async Task<IActionResult> GetReportTemplate([FromQuery] string reportId)
    {
        var report = await _reportService.GetReportAsync(reportId);

        if (report.FileStream == null)
            return NotFound("Report template has not been configured or uploaded.");

        var fileName = Path.GetFileName(report.ReportConfig!.ReportTemplateFilePath);
        string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        // ASP.NET Core auto-dispose stream khi request kết thúc
        return File(report.FileStream, contentType, fileName);
    }
}
```

✅ Dùng `RealmName` + `LangCd` từ `UserContext` — service tự fallback
✅ **Dispose Stream:**
   - `return File()` → Framework auto-dispose
   - Nếu giữ để modify nội dung → bọc `using var fileStream = await ...;`
✅ Storage naming convention:
   - Default: `template-name.extension` (eg. `summary.xlsx`)
   - Localized: `template-name_{langCd}.extension` (eg. `summary_en-US.xlsx`)

❌ KHÔNG dùng path vật lý `C:\path\to\report...`
❌ KHÔNG để `ReportTemplateFilePath` rỗng trong `MReportConfig`

## 2. File Storage

**Purpose:**
- Tạo file báo cáo từ data + template
- Lưu file vào Azure Blob Storage tại folder `download`
- Cung cấp `filePath` để download qua API

### 2.1. Create Report Background

**Trách nhiệm:**
- **SoC** — tách rời `IReportTemplateService` (đọc template) và `IReportDownloadService` (ghi file)
- **Tenant Isolation** — mọi file thuộc Root Container của Tenant
- **Unique Identification** — append timestamp (Unix time) tránh ghi đè

```csharp
public async Task<string> CreateDownloadReportAsync(string reportId)
{
    // 1. Get config
    var reportConfig = await _reportConfigRepository.GetReportConfigAsync(reportId);

    // 2. Load template — isolate by RealmName + LangCd
    var fileStream = await _reportTemplateService.GetReportTemplateAsync(
        _userContext.RealmName,
        reportConfig!.ReportTemplateFilePath,
        _userContext.LangCd);

    // TODO: Handle data binding hoặc modify fileStream

    // 3. Lưu file vào Blob — timeStampFlg = true tự động append timestamp
    var savedFilePath = await _reportDownloadService.CreateFileAsync(
        _userContext.RealmName,
        reportConfig.ReportTemplateFilePath,
        fileStream!,
        timeStampFlg: true);

    return savedFilePath;
}
```

### 2.2. Download Report File

**Trách nhiệm:**
- Trả về `Stream` để Controller stream qua HTTP response
- Bảo mật: chỉ đọc từ folder `download/` của Tenant (`{Tenant}/download/{filePath}`)

```csharp
public async Task<Stream?> DownloadReportAsync(string filePath)
{
    var reportStream = await _reportDownloadService.DownloadFileAsync(
        _userContext.RealmName,
        filePath);

    return reportStream;
}
```

✅ Quản lý lifecycle Stream qua `using`
✅ Controller cung cấp đúng MIME type
❌ KHÔNG dùng `System.IO.File` ghi vào RAM/disk web server
❌ KHÔNG load toàn bộ file vào `byte[]` lớn — luôn stream
❌ KHÔNG bỏ qua `_userContext.RealmName` khi upload (phá vỡ tenant isolation)

### 2.3. Create Report bằng API (Synchronous)

**Trách nhiệm:**
- Tạo file report tức thì, trả về luôn
- Không lưu trữ trung gian

```csharp
[HttpPost]
public async Task<IActionResult> CreateReport([FromQuery] string reportId)
{
    var reportConfig = await _reportConfigRepository.GetReportConfigAsync(reportId);

    var fileStream = await _reportTemplateService.GetReportTemplateAsync(
        _userContext.RealmName,
        reportConfig!.ReportTemplateFilePath,
        _userContext.LangCd);

    var fileOutput = /* TODO: Handle create file using ExcelCreator */;

    string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    return File(fileOutput.FileStream, contentType, fileOutput.FileName);
}
```

✅ Dùng cho file đơn giản (< 10 giây) tránh timeout
✅ File phức tạp / lâu → chuyển sang background ([13. Job System](13-job-system.md))
❌ KHÔNG lưu file lại sau xử lý tại web service (cho synchronous)

## 3. Upload File — Two-Phase Commit (Azure Blob)

**Purpose:** Upload file an toàn với kiến trúc Two-Phase Commit (Tải tạm → Submit), tránh ghi đè và rác dữ liệu.

**Trách nhiệm:**
- **Tenant Isolation** — mỗi Tenant (`realm`) có Blob Container riêng
- **Chống xung đột** — file tải lên `temporary/` được chèn UUID vào tên, loại trừ ghi đè
- Trong container của tenant, data chia 2 root folder: `temporary/` và `upload/`

### 3.1. Phase 1 — Upload Temporary

```csharp
// OpassFab.Api/Controller/FileSampleController.cs
[HttpPost("upload")]
public async Task<IActionResult> UploadTempFile(IFormFile file)
{
    if (file == null || file.Length == 0)
        return BadRequest();

    var realm = AppSettingProvider.MasterRealm;
    using var stream = file.OpenReadStream();

    // Upload vào temporary folder của realm (eg: tenant1/temporary/<file-path>)
    var resultPath = await _fileUploadService.UploadTempFileAsync(realm, file.FileName, stream);
    return Ok(new { TempFilePath = resultPath });
}
```

### 3.2. Phase 2 — Submit (Move temporary → upload)

```csharp
[HttpPost("submit")]
public async Task<IActionResult> SubmitFile([FromBody] FileSubmitInfo fileInfo)
{
    // Move file từ temporary → upload, sau đó xóa file temporary
    await _fileUploadService.SubmitFileAsync(
        _userContext.RealmName,
        fileInfo.TempFilePath,
        fileInfo.OriginalFilePath);
    return Ok();
}
```

`SubmitFileAsync` xử lý 2 việc: copy file từ `temporary` vào `upload`, rồi xóa file trong `temporary`:

```csharp
public async Task SubmitFileAsync(string realm, string tempPath, string destPath)
{
    var normalizedTempPath = tempPath.Replace('\\', '/');
    var normalizedDestPath = destPath.Replace('\\', '/');

    await CopyTempToUploadAsync(realm, normalizedTempPath, normalizedDestPath);  // copy → upload
    await DeleteTempFileAsync(realm, normalizedTempPath);                        // cleanup temp
}
```

> Chỉ cần xóa file temporary (không submit) → dùng `FileUploadService.DeleteTempFileAsync`.

✅ Chỉ định `destPath` tại bước Submit (xóa GUID, gom nhóm, đổi tên theo ID record theo nghiệp vụ)
✅ Thiết lập `AllowedExtensions` để block file rác/độc hại
❌ KHÔNG ghi file thẳng vào `upload/` (bỏ qua bước temp)
❌ KHÔNG dùng back-slash `\` trong tên file — luôn dùng `/`
❌ KHÔNG lưu `tempPath` vào DB — chỉ lưu `destPath` chính thức
