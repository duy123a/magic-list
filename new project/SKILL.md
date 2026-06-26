---
name: gen-unit-test
description: Sinh đầy đủ Unit Test cho một chức năng theo Screen ID (vd AA01001) — đọc tài liệu nghiệp vụ, phân tích branch, viết test xUnit/Moq/FluentAssertions và lặp đến khi đạt 100% line + branch coverage theo file coverage-{SCREEN_ID}.runsettings.
argument-hint: <SCREEN_ID> [đường dẫn thư mục tài liệu] [đường dẫn runsettings]
---

# Sinh Unit Test đạt 100% coverage cho một Screen ID

Bạn là Senior .NET Developer, Unit Test Specialist và Code Coverage Reviewer.

Input từ args:
- `SCREEN_ID` (bắt buộc): vd `AA01001`, `AA03001`, `XB01001`.
- Thư mục tài liệu (tuỳ chọn): mặc định tìm trong `/e/deha/resource/bsnipa01/bsnipa01/requirements/**/{SCREEN_ID}` (dùng `find` vì thư mục cha có tên tiếng Nhật; Git Bash path tương đương `E:\deha\resource\bsnipa01\bsnipa01\requirements`).
- File runsettings (tuỳ chọn): mặc định `Module/{Domain}/OpassFab.Module.{Domain}.Tests/coverage-{SCREEN_ID}.runsettings`.

**KHÔNG được viết test trước khi đọc hết tài liệu và toàn bộ source code liên quan. Không giả định logic chưa đọc.**

## Chiến lược tiết kiệm token (BẮT BUỘC áp dụng)

Mỗi tool call gửi lại toàn bộ context hội thoại → file đọc vào context bị tính phí lặp lại ở mọi bước sau. **KHÔNG dùng subagent cho bất kỳ bước nào** (đã đo thực tế: subagent có context riêng, trả phí đọc lại từ đầu và không dùng chung cache với main agent → tổng token TĂNG, thời gian chạy tăng). Mọi việc làm trực tiếp, nhưng đọc tằn tiện:

1. **Đọc full chỉ những file bắt buộc nguyên văn**: source của Service (phân tích branch), 3 file tài liệu `_BE/_API/_FE.md`, file DTO của screen. Hết.
2. **Dependency chỉ lấy đúng dòng cần bằng `grep`, KHÔNG `Read` cả file**: signature method của repo/UoW/combo service (`grep -n -B2 -A5 "TenMethod"`), property entity (`grep -n "public.*{ get"`), giá trị resource (`grep -n -A2 'name="WZ00020"'`), hằng enum (`grep -n "AllocationMethodSec"`). **Gộp nhiều grep vào 1 lần Bash** (nối bằng `;` hoặc `echo ---`). Controller chỉ cần route + tên method → grep, không đọc full.
3. **KHÔNG đọc file test mẫu** (`AA01001ServiceTests.cs`, 900+ dòng) — pattern đã mô tả đủ trong skill này. Chỉ mở khi module Tests chưa có test nào.
4. **Đo coverage bằng `--configuration Release` ngay từ Bước 7** — CI dùng Release; Debug đạt 100% vẫn hụt trên Release (bẫy switch lowering) → tránh lặp 2 vòng.
5. **Viết file test 1 lần bằng Write sau khi phân tích xong** — không viết từng phần rồi Edit nhiều vòng; mỗi vòng Edit + build là một lần trả phí context. Mục tiêu: build pass ngay lần đầu, ≤3 vòng lặp coverage.
6. **File evidence (TSV + report) main agent tự Write trực tiếp** — nội dung test đã nằm sẵn trong context, không cần đọc lại gì.
7. **Báo cáo chat NGẮN**: chỉ 3 số nghiệm thu (Bước 9) + Gap Report tóm tắt vài dòng + link file. Chi tiết Branch Analysis / Test Matrix ghi vào `docs/{SCREEN_ID}/test/` chứ KHÔNG lặp lại trong chat.
8. **Khuyên user chạy mỗi screen trong session mới** (`/clear` trước khi gọi skill) — lịch sử screen trước bị tính phí lại ở mọi tool call của screen sau.

## Bước 1 — Xác định coverage scope

1. Tìm service: `find . -iname "*{SCREEN_ID}*" -not -path "*/bin/*" -not -path "*/obj/*"` → xác định Domain module (Sale/Master/Stock/...).
2. Đọc file `coverage-{SCREEN_ID}.runsettings` trong project `OpassFab.Module.{Domain}.Tests`. Nếu chưa có thì tạo theo template bên dưới.
3. **Bẫy namespace**: service nằm trong folder con nên FQN là `OpassFab.Module.{Domain}.Business.Services.{SCREEN_ID}.{SCREEN_ID}Service`. Filter `Include` đúng là:
   `[OpassFab.Module.{Domain}]OpassFab.Module.{Domain}.Business.Services.{SCREEN_ID}.*`
   (pattern `...Services.{SCREEN_ID}Service*` sẽ KHÔNG match). Chỉ include thêm namespace khác nếu service thực sự gọi tới (kiểm tra bằng using/constructor — không copy mù từ runsettings của màn hình khác).
4. Kết luận rõ: class nào được tính coverage; DTO/Entity/Controller/Shared helper nằm ngoài scope (bị `ExcludeByFile`/khác assembly) → không cần test riêng.

Template runsettings:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>cobertura</Format>
          <Include>[OpassFab.Module.{Domain}]OpassFab.Module.{Domain}.Business.Services.{SCREEN_ID}.*</Include>
          <Exclude>[*.Tests]*,[*]*.Migrations.*</Exclude>
          <ExcludeByFile>**/Entities/**/*.cs;**/Models/**/*.cs;**/Dtos/**/*.cs</ExcludeByFile>
          <!-- Do not exclude CompilerGeneratedAttribute: async/iterator state machines also use this attribute. -->
          <ExcludeByAttribute>GeneratedCodeAttribute,ExcludeFromCodeCoverageAttribute</ExcludeByAttribute>
          <SkipAutoProps>true</SkipAutoProps>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

## Bước 2 — Đọc TOÀN BỘ tài liệu nghiệp vụ

Đọc mọi file trong thư mục tài liệu (thường `{SCREEN_ID}_BE.md`, `{SCREEN_ID}_API.md`, `{SCREEN_ID}_FE.md`; có thể có Excel/PDF/ảnh — không bỏ file nào). Tổng hợp thành bảng: Business Rule, Validation Rule, Error Case, Input/Output, bảng DB truy cập, sequence flow.

## Bước 3 — Đọc toàn bộ source code liên quan (trực tiếp, theo quy tắc grep ở mục Chiến lược tiết kiệm token)

Nắm được: Controller, Service, Interface, mọi DTO của screen, và **mọi dependency mà service chạm tới** — chỉ Service/DTO đọc full, còn lại grep đúng dòng cần:
- Constants/enum trong `Shared/OpassFab.Shared.Module/Consts/**` (vd `ReferenceData`, `RadioApproval`, `Sec.ApprovalStateSec`, `SecCd`, `CtrlApproval`, `RadioProductionCombination`).
- Extension: `Shared/OpassFab.Shared.Module/Utils/LambdaExpression/OpassFabExpression.cs` (`WhereLike`, `AndAlso` — Invoke-chain, short-circuit trái→phải).
- `I{Domain}UnitOfWork` + các repository interface service gọi (đọc đúng signature từng overload).
- Các sub-repo của `IMasterReadableService` được dùng (`Ctrl`, `Sec`, `Division`, `Ctrler`, `Unit`, `MaxSearchCountConfig`...) và model trả về (`SecModel`, `M*LocalizedDto` — chú ý property `*Locale`).
- Base class nếu DTO dùng (`OpassFabModelBase.CopyModelValue`, `IsExist`).

Liệt kê đầy đủ constructor dependency → tất cả phải mock (kể cả dependency inject nhưng không dùng — vẫn cần truyền mock vào ctor; ghi nhận là dead dependency trong báo cáo).

## Bước 4 — Phân tích branch

Với từng method trong scope, liệt kê hết: if/else, switch (từng case + default), ternary (cả 2 vế), `||`/`&&` (đủ tổ hợp điều kiện), null check, early return, LINQ join + `DefaultIfEmpty` (matched/unmatched), vòng lặp, throw/exception path. Lập bảng `| Method | Branch | Test sẽ cover |`.

**Bẫy hay gặp trong codebase này:**
- Enum có giá trị default trùng giá trị nghiệp vụ (vd `RadioProductionCombination.NotCombine = 0`): điều kiện "rỗng" phải set giá trị trung tính (`All`) tường minh, nếu không branch if vẫn true.
- Service search thường không có null-guard cho condition → test null input assert `NullReferenceException` (document hành vi hiện tại).
- Pattern max search count: `maxSearchCount.HasValue && Count == max + 1` → cần 3 test: null / bằng max / tràn (repo trả max+1).
- **Nhánh defensive không thể chạm qua public API** (vd `GetValue(null)` khi mọi caller đã loop trên row non-null; `AddValidationMessage` nhánh key-đã-tồn-tại khi mỗi field chỉ add tối đa 1 lỗi; điều kiện so sánh field trùng key lookup trước đó): vẫn phải cover để đạt 100% → test private method qua reflection, gom vào region `defensive branches (reflection)` riêng, comment giải thích vì sao unreachable, và ghi nhận trong Gap Report nếu là dead code.
- **Code service bị comment out / TODO** (vd `// TODO BSN20260610` quanh đoạn gọi `GetSecAsync`): test phải theo hành vi HIỆN TẠI (assert giá trị default/rỗng, KHÔNG mock call đã bị comment out — constant tham chiếu có thể không tồn tại → lỗi compile CS0117). Ghi comment trỏ về TODO trong test + đưa vào Gap Report; phần hành vi theo spec chuyển sang nhóm Spec Compliance.
- Service dùng `ResourceManager` static (message thật, không mock được): set `CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture` trong ctor test class để luôn lấy neutral resource (tiếng Nhật) deterministic, không phụ thuộc locale máy; assert bằng `Contain` fragment (vd "必ず入力してください") thay vì so khớp cả chuỗi.
- **`switch` trên tuple `(string, int)` build Release lower khác Debug**: Release sinh cây so sánh ký tự + null-check với nhiều branch hơn (vd 43 vs ít hơn ở Debug) → 100% trên Debug vẫn có thể hụt trên Release. Phải đo coverage trên ĐÚNG `--configuration` mà CI dùng. Để phủ hết: ngoài 1 key lạ hoàn toàn, cần thêm row (a) mỗi secCd hợp lệ với seqNo lạ, (b) chuỗi fail ở từng vị trí so sánh — sai ký tự đầu/giữa/cuối theo từng bucket ký tự cuối (vd có case "002"/"102" thì cần "112"; có "004" cần "104"), (c) chuỗi khác độ dài (ngắn hơn/dài hơn), (d) chuỗi null (`null!`). Lặp đo → đọc `condition-coverage` còn thiếu → bổ sung probe cho đến 43/43.

## Bước 5 — Thiết kế test case

Ma trận tối thiểu: happy path, từng nhánh routing, từng case switch (+default qua `[Theory]` null/""/giá trị lạ), zero result, max count 3 tổ hợp, name-resolution matched/unmatched (cả 2 vế mỗi ternary), repository/config exception propagation, null condition, predicate: điều kiện rỗng (mọi if false), full điều kiện (mọi if true + evaluate match/non-match), boundary From/To (inclusive `>=`/`<=`), từng tổ hợp `||`.

## Bước 6 — Viết test

- Vị trí: `Module/{Domain}/OpassFab.Module.{Domain}.Tests/Business/Services/{SCREEN_ID}ServiceTests.cs`. (File mẫu chuẩn `AA01001ServiceTests.cs` cùng thư mục — CHỈ mở khi module Tests chưa có test nào; bình thường không đọc để tiết kiệm token.)
- Framework: xUnit + Moq + FluentAssertions. Nếu csproj test chưa có FluentAssertions thì thêm `<PackageReference Include="FluentAssertions" Version="6.12.*" />` (giữ 6.x — 8.x đổi license).
- Naming `MethodName_Condition_ExpectedResult`, AAA pattern, không TODO/pseudo code, mock toàn bộ — không DB/API thật.
- **Comment trong code test viết bằng TIẾNG ANH** (kể cả XML doc, region, because-message của FluentAssertions); thuật ngữ tiếng Nhật của nghiệp vụ (引合見積...) giữ nguyên.
- Setup mặc định trong constructor (master rỗng, maxSearchCount null, wildcard true, repo trả list rỗng); từng test override phần cần.
- **Kỹ thuật capture predicate** để test `BuildModelCondition` thật sự (không chỉ chạm branch):

```csharp
Expression<Func<TResultModel, bool>>? captured = null;
_repoMock.Setup(r => r.GetXxxSearchAsync(
        It.IsAny<Expression<Func<TResultModel, bool>>>(), It.IsAny<int?>()))
    .Callback<Expression<Func<TResultModel, bool>>, int?>((p, _) => captured = p)
    .ReturnsAsync(new List<TResultModel>());
// ... await service.SearchAsync(condition);
var compiled = captured!.Compile();
compiled(matchingRow).Should().BeTrue();
compiled(nonMatchingRow).Should().BeFalse();
```

  - Row "matching" phải set **non-null cho mọi field bị WhereLike** (Contains trên null → NRE).
  - Row "non-matching" chỉ cần sai field ĐẦU TIÊN trong chuỗi predicate (short-circuit bỏ qua phần sau, tránh NRE).

### Nhóm Spec Compliance — test theo TÀI LIỆU, được phép FAIL (KHÔNG gắn Skip)

Khi Bước 2-3 phát hiện code lệch tài liệu, ngoài test regression (ghim hành vi hiện tại, phải PASS), thêm region `Spec Compliance` ở CUỐI cùng file test:

- Assertion viết theo **tài liệu**, không theo code → FAIL là kết quả mong đợi cho đến khi dev implement. KHÔNG sửa assertion cho pass.
- **KHÔNG gắn `Skip`** (quyết định dự án: fail phải hiện ra fail để báo cáo trung thực trạng thái so với spec). Hệ quả: `dotnet test` không filter sẽ đỏ — đó là expected, KHÔNG phải lỗi build; gate phải dùng filter bên dưới.
- Mỗi test gắn `[Trait("Category", "SpecCompliance")]` để tách khỏi gate regression:
  - Gate CI (phải xanh): `dotnet test --filter "FullyQualifiedName~{SCREEN_ID}&Category!=SpecCompliance"`
  - Báo cáo đối chiếu spec: `dotnet test --filter "FullyQualifiedName~{SCREEN_ID}"` (fail của nhóm spec là expected)
- XML doc của test ghi rõ mục tài liệu (vd "Spec AA01001_BE.md section 1-2") và nguyên nhân lệch.
- Test FAIL **không ảnh hưởng coverage** (coverage đếm dòng được thực thi, assert trượt vẫn đã chạy qua service) — nhưng chỉ tin số liệu khi nhóm regression vẫn phủ 100% độc lập.
- Spec không thể viết test (thiếu endpoint/field trong model → không compile được): KHÔNG cố viết, đưa vào báo cáo/TSV (xem Bước 8).
- Quy ước bàn giao: khi dev implement spec → test spec chuyển PASS; test regression cũ ghim hành vi cũ sẽ FAIL → cập nhật/xóa theo spec.

## Bước 7 — Chạy và lặp đến 100% (Release ngay từ đầu)

Gộp build + test + parse vào 1 lần Bash để giảm round-trip:

```bash
dotnet build Module/{Domain}/OpassFab.Module.{Domain}.Tests/OpassFab.Module.{Domain}.Tests.csproj -c Release -v q 2>&1 | grep -cE "error" \
&& dotnet test Module/{Domain}/OpassFab.Module.{Domain}.Tests/OpassFab.Module.{Domain}.Tests.csproj -c Release --no-build \
  --filter "FullyQualifiedName~{SCREEN_ID}ServiceTests" \
  --settings Module/{Domain}/OpassFab.Module.{Domain}.Tests/coverage-{SCREEN_ID}.runsettings 2>&1 | tail -4
```

Parse cobertura (đường dẫn in ở cuối output `Attachments:` — lấy file mới nhất bằng `glob` + `os.path.getmtime`):

```bash
python - <<'EOF'
import xml.etree.ElementTree as ET, glob, os
files = glob.glob('Module/{Domain}/OpassFab.Module.{Domain}.Tests/TestResults/**/coverage.cobertura.xml', recursive=True)
t = ET.parse(max(files, key=os.path.getmtime))
r = t.getroot()
print("line-rate:", r.get('line-rate'), "branch-rate:", r.get('branch-rate'))
for cls in r.iter('class'):
    for line in cls.iter('line'):
        if line.get('hits') == '0':
            print(cls.get('name'), "UNCOVERED LINE", line.get('number'))
        cc = line.get('condition-coverage')
        if cc and not cc.startswith('100%'):
            print(cls.get('name'), "PARTIAL BRANCH line", line.get('number'), cc)
EOF
```

Nếu còn line hits=0 hoặc condition-coverage <100% → đối chiếu số dòng với source, viết thêm test, chạy lại. **Chỉ dừng khi line-rate=1 và branch-rate=1 trên mọi class node** (kể cả `<XxxAsync>d__N` async state machine và `<>c` lambda closure).

## Bước 8 — Báo cáo kết quả

Mục 1-5 ghi vào `docs/{SCREEN_ID}/test/{SCREEN_ID}_SpecComplianceReport.md` (KHÔNG dán đầy đủ vào chat — tiết kiệm token); chat chỉ chứa mục 6-9 dạng tóm tắt:

1. Coverage Scope Analysis
2. Requirement Analysis
3. Dependency Analysis (dependency tree)
4. Branch Analysis
5. Test Case Matrix (test ↔ branch/rule)
6. Unit Test Code (link file)
7. Coverage Verification (số liệu cobertura + lệnh chạy lại)
8. **Gap Report**: rule có trong tài liệu nhưng chưa có code (vd TODO hardcode, endpoint chưa implement, điều kiện tìm bị bỏ qua, bug tiềm ẩn như enum default) — tóm tắt mỗi gap 1 dòng trong chat, chi tiết trong report file; KHÔNG tự ý sửa service.
9. **File evidence** (mục dưới) — tạo xong mới được kết thúc.

### File evidence kèm theo (BẮT BUỘC — luôn tạo, không chờ user yêu cầu)

Tự Write trực tiếp (KHÔNG dùng subagent — nội dung test đã có sẵn trong context, subagent phải đọc lại từ đầu → tốn hơn).

Tạo `docs/{SCREEN_ID}/test/{SCREEN_ID}_EvidenceCases.tsv` — TSV 6 cột, mỗi dòng = 1 lần chạy test (mỗi `[InlineData]` của Theory 1 dòng riêng):

```
No	Class	Test	Mục đích	Input	Output kỳ vọng
```

- Dòng regression: Input/Output ghi giá trị cụ thể (mock gì, assert gì) — không ghi chung chung "dữ liệu hợp lệ".
- Dòng spec có test: cột Test prefix `[Spec]`, cột "Output kỳ vọng" ghi giá trị theo spec + `— HIỆN TẠI FAIL (nguyên nhân)`. TSV là báo cáo đối chiếu spec, ghi trạng thái FAIL so với spec — KHÔNG đổi thành SKIP/PASS khi chưa implement.
- Dòng spec KHÔNG viết được test: cột Test ghi `[Spec] (chưa viết được test - thiếu code/field)`, Output ghi spec yêu cầu gì + `— HIỆN TẠI FAIL (...)`.
- Validate trước khi bàn giao: `awk -F'\t' '{print NF}' file.tsv | sort -u` phải ra đúng 1 con số (6).

Kèm `docs/{SCREEN_ID}/test/{SCREEN_ID}_SpecComplianceReport.md`: bảng tổng pass/fail, chi tiết từng fail (spec → test → nguyên nhân trong code), danh sách spec không test được, và quy ước xử lý khi dev implement.

## Bước 9 — Nghiệm thu cuối (BẮT BUỘC — luôn chạy sau Bước 8, không chờ user yêu cầu)

Chạy đúng chuỗi lệnh sau (Release = configuration CI dùng; dọn results dir trước để glob không gom coverage cũ):

```bash
# 1. Gate regression (phải xanh) + coverage Release
rm -rf TestResults/{Domain}
dotnet test "Module/{Domain}/OpassFab.Module.{Domain}.Tests/OpassFab.Module.{Domain}.Tests.csproj" \
  --configuration Release \
  --filter "FullyQualifiedName~{SCREEN_ID}ServiceTests&Category!=SpecCompliance" \
  --settings "Module/{Domain}/OpassFab.Module.{Domain}.Tests/coverage-{SCREEN_ID}.runsettings" \
  --results-directory "./TestResults/{Domain}"

# 2. Full run kèm SpecCompliance — chỉ để lấy số liệu Fail/Pass/Skip, fail của nhóm spec là expected nên KHÔNG để exit code chặn bước sau
dotnet test "Module/{Domain}/OpassFab.Module.{Domain}.Tests/OpassFab.Module.{Domain}.Tests.csproj" \
  --configuration Release --no-build \
  --filter "FullyQualifiedName~{SCREEN_ID}ServiceTests" || true

# 3. Sinh HTML report (KHÔNG cần --collect/-filefilters — runsettings đã lo)
rm -rf TestResults/CoverageReport-{SCREEN_ID}
reportgenerator \
  "-reports:TestResults/{Domain}/**/coverage.cobertura.xml" \
  "-targetdir:TestResults/CoverageReport-{SCREEN_ID}" \
  "-reporttypes:Html;TextSummary" \
  "-assemblyfilters:+OpassFab.Module.{Domain}"

# 4. Mở report cho user xem (Windows)
start TestResults/CoverageReport-{SCREEN_ID}/index.html
```

Sau đó báo cáo cho user 3 con số, tách bạch:
- **Gate regression** (bước 1): `Failed: x, Passed: y, Skipped: z` — Failed phải = 0; nếu ≠ 0 quay lại Bước 7, KHÔNG bàn giao.
- **Full run** (bước 2): `Failed: x, Passed: y, Skipped: z` — ghi rõ x fail đều thuộc nhóm SpecCompliance (expected); nếu có fail ngoài nhóm spec → quay lại Bước 7.
- **Coverage** (từ `TestResults/CoverageReport-{SCREEN_ID}/Summary.txt`): Line / Branch / Method — cả 3 phải 100% trên Release. 100% ở Debug chưa đủ (xem bẫy switch lowering ở Bước 4).
