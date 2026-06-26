# 13. Job System

**Purpose:**
- Layered Architecture, tách Domain (abstraction) khỏi Infrastructure (implementation)
- 2 process riêng biệt: **Async Job** (bất đồng bộ, trigger qua API) và **Schedule Job** (chạy định kỳ qua cron/AKS)

## 1. Architecture

### 1.1. Project Layout

```
backend/
├── Job/
│   ├── Base/
│   │   ├── OpassFab.JobBase/                       # Domain (interfaces, entities, consts)
│   │   └── OpassFab.JobBase.Infrastructure/        # Infrastructure (impl)
│   │
│   ├── OpassFab.Job.Async/                         # Process: Async Job Worker
│   ├── OpassFab.Job.Schedule/                      # Process: Scheduled Job Runner
│   │
│   └── Implementation/                             # Business Job Implementations
│       └── OpassFab.Job.Implement.Sample/
│
├── OpassFab.Api/                                    # Web API (trigger job qua API)
└── Shared/
    ├── OpassFab.Shared.Module/                     # IJobContext, ICacheService, JobSetting
    └── OpassFab.Shared.Infrastructure/             # RedisCacheService, Logging, EntityAudit
```

### 1.2. Layer Responsibility

#### A. Domain — `OpassFab.JobBase`

**Trách nhiệm:**
- **Dependency Inversion** — chỉ interface & abstraction, không impl
- **Rich Domain Model** — entity chứa đầy đủ business state
- **SoC** — mỗi interface 1 nhiệm vụ

✅ Thêm enum value mới vào cuối, giữ giá trị cũ
✅ Kế thừa `OpassFabJobException` cho exception riêng module
✅ Define / kế thừa `JobInput` / `JobResult` khi cần data riêng
❌ KHÔNG reference infrastructure project từ domain
❌ KHÔNG đặt business logic vào entity (entity là data holder)
❌ KHÔNG thay đổi giá trị số của enum đã có (break DB data)

#### B. Infrastructure — `OpassFab.JobBase.Infrastructure`

**Trách nhiệm:**
- **Template Method Pattern** — `JobExcutionService` là abstract, impl chỉ override `ExecuteAsync()`
- **Scoped Context** — mỗi job execution 1 DI scope mới (isolation)
- **Read/Write Separation** — `OpassFabJobDbContext` hỗ trợ read-only mode (no tracking)

#### C. Process Layer

| Module | Trigger by | Kiểu chạy | Mechanism |
|---|---|---|---|
| `OpassFab.Job.Async` | API | Long-running service | `BackgroundService` lắng nghe Redis queue |
| `OpassFab.Job.Schedule` | Cron / AKS | One-shot | Chạy 1 lần, exec all registered jobs rồi thoát |

#### D. Implementation Layer

Chứa business logic. Mỗi module là "plugin" đăng ký vào hệ thống qua `RegisterAssemblies`. Khi implement job mới, code chính ở đây.

### 1.3. Hạ tầng

**Redis Queue:**
- Message broker giữa API và Async Worker
- `ICacheService.EnqueueAsync()` / `DequeueAsync()` (Redis List)
- Queue name: `Job:QueueName`
- Timeout: `Job:QueueTimeoutSec`

```
API → EnqueueAsync("OpassFabJobQueue", JobArguments) → Redis List
Worker → DequeueAsync("OpassFabJobQueue") ← Redis List (blocking pop)
```

> Schedule Job KHÔNG dùng queue.

**PostgreSQL:**
- Persistent storage cho `job_info` table
- Multi-tenant: schema-based per tenant
- JSONB: `job_input`, `job_result`
- DbContext mode:
  - `DatabaseMode.Write` (default) — full change tracking
  - `DatabaseMode.Read` — no tracking, optimize cho query

## 2. Configuration

### 2.1. AppSettings

| Key | Type | Mô tả |
|---|---|---|
| `Job:QueueName` | string | Tên Redis queue |
| `Job:QueueTimeoutSec` | int | Blocking wait timeout (seconds) |
| `Job:MaxRetryCount` | int | Số lần retry tối đa khi `RetryJobException` |
| `Job:RegisterAssemblies` | string[] | Assembly chứa `IJobExcutionService` impl sẽ chạy |
| `RedisCache:ConnectionString` | string | Redis connection |
| `Database:ConnectionString` | string | PostgreSQL connection |

✅ Dùng `appsettings.{Env}.json` cho per-env config
✅ `RegisterAssemblies` chỉ chứa assembly cần thiết cho process hiện tại
✅ `QueueTimeoutSec` hợp lý (3-10s) — cân bằng latency vs resource
❌ KHÔNG hardcode connection string
❌ KHÔNG `QueueTimeoutSec = 0` (busy-loop)
❌ KHÔNG đăng ký assembly không tồn tại (job bị bỏ qua silently)

### 2.2. DI Registration

**Cho Web API:**
```csharp
// OpassFab.Api/Program.cs
builder.Services.ConfigureJobBaseServiceForWebApi(builder.Environment);
```
Đăng ký:
- `OpassFabJobDbContext` — DbContext
- `IOpassFabJobUnitOfWork` → `OpassFabJobUnitOfWork`
- `IJobTriggerService` → `JobTriggerService`

> ⚠ KHÔNG đăng ký `IJobRunnerService` — API chỉ trigger, không chạy job.

**Cho Job Process (Async / Schedule):**
```csharp
// OpassFab.Job.Async/Program.cs hoặc OpassFab.Job.Schedule/Program.cs
services.ConfigureJobBaseService(context.HostingEnvironment);
```
Đăng ký:
- Shared services
- `OpassFabJobDbContext`
- `IOpassFabJobUnitOfWork`
- `IJobRunnerService` → `JobRunnerService`
- `IApplicationContext` ← từ `IJobContext`

> ⚠ KHÔNG đăng ký `IJobTriggerService` — Job process không trigger job khác.

## 3. Common Components

### 3.1. Job Context System

**Purpose:** Cung cấp context (tenant, user, job metadata) cho stack trong 1 lần exec job. Tương tự `HttpContext` cho Web API.

**Flow:**

**Async Job:**
1. Worker dequeue `JobArguments` từ Redis
2. Tạo DI scope mới
3. `JobContextAccessor.Args = args` (trong scope)
4. Resolve `IJobContext` → `AsyncJobContextProvider.ResolveContext()`
5. `JobContextBase` được tạo từ Args

**Schedule Job:**
1. Process start
2. Tạo DI scope
3. Resolve `IJobContext` → `ScheduleJobContextProvider.ResolveContext()`
4. `JobContextBase` được tạo với giá trị mặc định

**Usage:**

```csharp
public class SampleExcutionService : JobExcutionService
{
    private readonly ILogger _logger;

    public SampleExcutionService(
        IJobContext jobContext,
        IOpassFabJobUnitOfWork unitOfWork) : base(jobContext, unitOfWork)
    {
        _logger = Log.ForContext<SampleExcutionService>();
    }

    public override async Task ExecuteAsync()
    {
        _logger.Information($"{_jobContext.JobName} is executing.");
        await Task.Delay(1000);
    }
}
```

✅ Inject `IJobContext` (KHÔNG inject `JobContextBase` trực tiếp)
✅ Context immutable sau khi tạo
❌ KHÔNG `new JobContextBase()` — luôn dùng DI
❌ KHÔNG modify `JobContextAccessor.Args` sau khi scope đã resolve

### 3.2. JobExcutionService — Abstract Base

```csharp
public abstract class JobExcutionService : IJobExcutionService
{
    protected readonly IJobContext _jobContext;
    protected readonly IOpassFabJobUnitOfWork _jobUnitOfWork;

    public JobExcutionService(IJobContext jobContext, IOpassFabJobUnitOfWork unitOfWork)
    {
        _jobContext = jobContext;
        _jobUnitOfWork = unitOfWork;
    }

    public IJobContext JobContext => _jobContext;
    public JobTriggerType Type => (_jobContext as IJobContextBase)!.TriggerType;

    // Override for business logic
    public abstract Task ExecuteAsync();

    // Update job status (skip nếu Scheduled)
    public async Task UpdateJobStatusAsync(JobStatus status, bool hasRetry = false)
    {
        if (Type == JobTriggerType.Scheduled) return;

        var job = await _jobUnitOfWork.JobRepository.FindAsync(_jobContext.JobId);
        job!.Status = status;

        if (hasRetry && job.RetryCount <= AppSettingProvider.Instance.Job.MaxRetryCount)
            job.RetryCount += 1;

        await _jobUnitOfWork.JobRepository.UpdateAsync(job);
        await _jobUnitOfWork.SaveChangesAsync();
    }
}
```

✅ Mỗi job 1 `IJobExcutionService` impl
✅ Inject thêm repo/service riêng qua constructor
✅ Log đầu/cuối `ExecuteAsync` để debug
✅ Throw `RetryJobException` khi muốn retry (transient error)
✅ Throw `OpassFabJobException` cho lỗi nghiệp vụ
❌ KHÔNG gọi `UpdateJobStatusAsync` trong `ExecuteAsync` (`JobRunnerService` đã quản lý)
❌ KHÔNG swallow exception (base system xử lý retry/log)
❌ KHÔNG tạo DI scope mới trong `ExecuteAsync` (dùng scope hiện tại)

### 3.3. JobRunnerService — Orchestrator

**Purpose:** Điều phối exec all registered job. Quản lý lifecycle: filter → run → update status → handle errors. **Fail-fast:** 1 job fail → cancel toàn bộ jobs còn lại.

**Cơ chế:**
1. Lấy all `IJobExcutionService` từ DI
2. Filter theo `RegisterAssemblies`
3. Chạy tuần tự:
   a. Status → `Running`
   b. Gọi `ExecuteAsync()`
   c. Status → `Succeeded`
4. Nếu lỗi:
   a. Cancel `CancellationTokenSource` (dừng job tiếp theo)
   b. Status → `Failed`
   c. Nếu `RetryJobException` → status `Pending` + `RetryCount++`
   d. Re-throw

**Assembly filtering:**
```csharp
var jobServices = jobExcutions
    .Where(service =>
    {
        var exc = service.GetType();
        return _jobSetting.RegisterAssemblies.Contains(exc.Assembly.GetName().Name);
    }).ToList();
```

> Assembly name phải khớp **chính xác** tên assembly (không phải namespace), eg. `"OpassFab.Job.Implement.Sample"`.

✅ Đăng ký đúng assembly name
✅ Khi debug: check log `"Executing job {jobName}"` xác nhận job được chạy
❌ KHÔNG inject `IJobRunnerService` trong Web API
❌ KHÔNG thay đổi execution order nếu có dependency (chạy tuần tự theo DI registration order)

### 3.4. JobTriggerService — API Job Trigger

**Purpose:** API tạo job + enqueue Redis cho Worker xử lý. Hỗ trợ cancel job `Pending`.

**Trách nhiệm:**
- `CreateJobAsync` auto resolve tenant từ `IApplicationContext`
- `TriggerBy` auto lấy từ `IUserContext.LoginId`
- Status ban đầu = `Pending`

**Tạo job:**
```csharp
public class MyService
{
    private readonly IJobTriggerService _jobTriggerService;

    public async Task CreateReportJobAsync()
    {
        var jobInfo = new JobInfo
        {
            Name = "GenerateMonthlyReport",
            TriggerType = JobTriggerType.Manual,
            // JobInput, CronExpression, ...
        };

        await _jobTriggerService.CreateJobAsync(jobInfo);
        // → Job record saved (status = Pending)
        // → JobArguments enqueued vào Redis
    }
}
```

**Cancel job:**
```csharp
var success = await _jobTriggerService.CancelJobAsync(jobId);
// true → Cancelled
// false → Job không tồn tại hoặc đã chạy/xong
```

❌ KHÔNG set `JobStatus` trước `CreateJobAsync` (service tự set Pending)
❌ KHÔNG `CancelJobAsync` cho job đang `Running` (chỉ cancel `Pending`)

### 3.5. OpassFabJobDbContext

**Read/Write Mode:**
```csharp
// Write mode — DI với DatabaseMode.Write
// Read mode — no tracking, optimize cho query — DI với DatabaseMode.Read

readCtx.SaveChanges();   // ❌ throws InvalidOperationException
```

**JSONB Columns:**
```csharp
builder.Property(e => e.JobInput)
    .HasColumnName("job_input")
    .HasColumnType("jsonb")
    .HasConversion(
        e => JsonSerializer.Serialize(e, (JsonSerializerOptions?)null),
        e => JsonSerializer.Deserialize<JobInput>(e, (JsonSerializerOptions?)null)!);
```

✅ `DatabaseMode.Read` cho query (perf tốt hơn)
✅ Kế thừa `OpassFabJobDbContext` trong implementation module
✅ JSONB cho phép query trực tiếp trong PostgreSQL
❌ KHÔNG `SaveChanges` trên Read-mode context
❌ KHÔNG `new` DbContext (dùng DI)

### 3.6. Exception Handling & Retry

```
System.Exception
└── OpassFabBaseException (Shared)
    └── OpassFabJobException
        └── RetryJobException
```

| Exception | Hành vi |
|---|---|
| `RetryJobException` | Status → Failed → Pending + `RetryCount++` → Re-enqueue Redis |
| `OpassFabJobException` | Status → Failed → Cancel all jobs → Log → Re-throw |
| `Exception` (unexpected) | Cancel all → Log → Re-throw |

✅ `RetryJobException` cho lỗi transient (network timeout, lock contention)
✅ `OpassFabJobException` cho business logic error (validation, data inconsistency)
✅ Truyền `messageKey` có ý nghĩa (cho resource localization)
❌ KHÔNG swallow exception
❌ KHÔNG `RetryJobException` cho lỗi logic (retry vô nghĩa)

### 3.7. Logging

`JobLogEventEnricher` tự động thêm property:

| Property | Source | Example |
|---|---|---|
| `SystemName` | appsettings | `"OpassFab_BATCH"` |
| `ServerName` | Config / `MachineName` | `"Development"` |
| `ProcessId` | `Environment.ProcessId` | — |
| `Tenant` | `JobContextProvider.RealmName` | `"tenant_2"` |
| `LoginId` | `JobContextProvider.TriggerBy` | `"admin"` |
| `RequestId` | `JobContextProvider.JobId` | Guid |
| `Method` | `JobContextProvider.JobName` | `"SampleJob.ExcuteAsync"` |

✅ Dùng `Log.ForContext<T>()` (auto source context)
✅ Extension `LogInfo`, `LogError` từ `OpassFab.Shared.Infrastructure.Utils.Logging`
✅ Log đầu/cuối mỗi bước quan trọng
❌ KHÔNG `Console.WriteLine` (bypass structured logging)
❌ KHÔNG log sensitive data (password, token, PII)

## 4. Hướng dẫn tạo Job mới

### 4.1. Tạo Project

```
Job/
└── Implementation/
    └── OpassFab.Job.Implementation.MyFeature/
        ├── OpassFab.Job.Implementation.MyFeature.csproj
        ├── Persistences/
        │   └── MyFeatureJobDbContext.cs
        ├── Services/
        │   └── MyFeatureExcutionService.cs
        └── Startup.cs
```

### 4.2. DbContext (optional)

```csharp
using Microsoft.EntityFrameworkCore;
using OpassFab.JobBase.Infrastructure.Persistences;
using OpassFab.Shared.Module.Configuration;

namespace OpassFab.Job.Implementation.MyFeature.Persistences;

public class MyFeatureJobDbContext : OpassFabJobDbContext
{
    public MyFeatureJobDbContext(
        IJobContext context,
        DbContextOptions<OpassFabJobDbContext> options) : base(context, options) { }

    // public DbSet<MyEntity> MyEntities { get; set; }
}
```

### 4.3. ExecutionService

```csharp
using OpassFab.JobBase.Infrastructure.Services;
using OpassFab.JobBase.Persistences;
using OpassFab.Shared.Module.Configuration;
using Serilog;

namespace OpassFab.Job.Implementation.MyFeature.Services;

public class MyFeatureExcutionService : JobExcutionService
{
    private readonly ILogger _logger;

    public MyFeatureExcutionService(
        IJobContext jobContext,
        IOpassFabJobUnitOfWork unitOfWork) : base(jobContext, unitOfWork)
    {
        _logger = Log.ForContext<MyFeatureExcutionService>();
    }

    public override async Task ExecuteAsync()
    {
        _logger.Information($"{_jobContext.JobName} is executing.");

        // ── Business logic ──

        _logger.Information($"{_jobContext.JobName} completed.");
    }
}
```

### 4.4. Startup.cs

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpassFab.Job.Implementation.MyFeature.Persistences;
using OpassFab.Job.Implementation.MyFeature.Services;
using OpassFab.JobBase.Infrastructure.Persistences;
using OpassFab.JobBase.Services;
using OpassFab.Shared.Infrastructure.Utils;

namespace OpassFab.Job.Implementation.MyFeature;

public static class Startup
{
    public static IServiceCollection ConfigureMyFeatureJobService(
        this IServiceCollection services, IHostEnvironment env)
    {
        services.AddScoped<IJobExcutionService, MyFeatureExcutionService>();

        services.DbContextConfigure<MyFeatureJobDbContext>();
        services.AddScoped<OpassFabJobDbContext, MyFeatureJobDbContext>();

        return services;
    }
}
```

### 4.5. Đăng ký vào Process

```csharp
// OpassFab.Job.Async/Startup.cs hoặc OpassFab.Job.Schedule/Startup.cs
public static IServiceCollection AddAsyncJobServices(
    this IServiceCollection services, IHostEnvironment env)
{
    // ... existing ...
    services.ConfigureMyFeatureJobService(env);   // ← Add this
    return services;
}
```

### 4.6. Cập nhật `appsettings.json`

```json
{
  "Job": {
    "RegisterAssemblies": [
      "OpassFab.Job.Implement.Sample",
      "OpassFab.Job.Implementation.MyFeature"
    ]
  }
}
```

### 4.7. Checklist khi tạo Job mới

1. ✅ Tạo project trong `Job/Implementation/`
2. ✅ Reference `OpassFab.JobBase.Infrastructure`
3. ✅ Tạo DbContext kế thừa `OpassFabJobDbContext`
4. ✅ Implement `JobExcutionService` (override `ExecuteAsync`)
5. ✅ Tạo `Startup.cs` với extension method DI
6. ✅ Đăng ký vào Process Startup (Async và/hoặc Schedule)
7. ✅ Thêm assembly name vào `RegisterAssemblies` trong `appsettings.json`
8. ✅ Thêm project reference vào Process `.csproj`
9. ✅ Test chạy job — confirm status
10. ✅ Verify output (log có đầy đủ context: tenant, jobId, jobName)
