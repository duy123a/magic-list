# Hướng dẫn Xử lý Logics Update Stock cho AI Agent - Màn hình AC03001

## 1. Tổng quan & Mục tiêu
Tài liệu này hướng dẫn AI Agent thực hiện cài đặt logic **Cập nhật tồn kho (Stock Update)** cho tính năng **Nhập chỉ thị xuất hàng (AC03001)** dựa trên bảng ma trận nghiệp vụ (các cột **AB đến AI**, No.27 đến No.34).

Trong class `AC03001StockUpdateService`, logic update stock sẽ được gọi thông qua phương thức private:
```csharp
private async Task InvokeStockUpdateAsync(IEnumerable<UpdateStockDTO> dtoList)
```

Khi thực hiện các action **Đăng ký (Create)**, **Cập nhật (Update)**, và **Xóa (Delete)** chỉ thị xuất hàng tại AC03001, hệ thống cần khởi tạo danh sách `UpdateStockDTO` tương ứng với từng cột hành động và gọi phương thức `InvokeStockUpdateAsync`.

---

## 2. Quy tắc chung & Mapping dữ liệu (`UpdateStockDTO`)

Mỗi DTO đại diện cho một giao dịch biến động tồn kho. Các thông tin cấu thành DTO tuân thủ theo rule:
- **`Common Rules`**: Các giá trị mặc định (`EADUnitPrice = 0`, `OriAmt = 0`, `EADAmt = 0`, `CompletedFlg = false`, `ProcessCd = ""`, v.v.).
- **`Templates`**: Sử dụng 2 Template chính:
  1. **SalesOrder Template** (Dựa trên dữ liệu đơn hàng: `t_sales_order`, `t_sales_order_dtl`, `t_sales_order_dtl_ctrl`).
  2. **ShipmentAssign Template** (Dựa trên dữ liệu chỉ thị xuất hàng: `t_shipment_assign_dtl`, `t_shipment_allocation_dtl`, `t_sales_order_dtl_ctrl`).

---

## 3. Ma trận Mapping Chi tiết từng Action (Cột AB -> AI)

### 3.1. Chế độ Đăng ký (Create) — Nhập chỉ thị xuất hàng
Mỗi dòng chỉ thị xuất hàng được đăng ký mới sẽ tạo **2 DTOs** và gọi `InvokeStockUpdateAsync` theo thứ tự:

| STT | Cột | Tên Action / Mục đích | Template | VoucherType | EADClassSec | ScheduleSec | Nguồn Mapper Dữ liệu Chính |
|:---:|:---:|:---|:---:|:---:|:---:|:---:|:---|
| **1** | **AB** (No.27) | Xóa ĐK đơn hàng dự kiến | `SalesOrder` | `A02` | `DEL` | `01` (Chưa chỉ thị) | - `VoucherNo`: `t_sales_order_dtl.sales_order_no`<br>- `Date`: `t_sales_order_dtl.shipment_scheduled_date`<br>- `Article`: `t_sales_order_dtl.article_cd` |
| **2** | **AC** (No.28) | Đăng ký ĐK chỉ thị dự kiến | `ShipmentAssign` | `A03` | `ADD` | `02` (Đã chỉ thị) | - `VoucherNo`: `t_shipment_assign_dtl.shipment_assign_no`<br>- `Date`: `t_shipment_assign_dtl.shipment_date`<br>- `Article`: `t_shipment_allocation_dtl.article_cd` |

---

### 3.2. Chế độ Cập nhật (Update) — Chỉnh sửa chỉ thị xuất hàng
Khi chỉnh sửa thông tin chỉ thị xuất hàng (hoặc điều chỉnh số lượng/phân bổ), tạo **4 DTOs** ứng với các cột **AD, AE, AF, AG**:

| STT | Cột | Tên Action / Mục đích | Template | VoucherType | EADClassSec | ScheduleSec | Nguồn Mapper Dữ liệu Chính |
|:---:|:---:|:---|:---:|:---:|:---:|:---:|:---|
| **1** | **AD** (No.29) | Xóa ĐK chỉ thị (Trước khi sửa) | `ShipmentAssign` | `A03` | `UP1` | `02` (Đã chỉ thị) | Dữ liệu chỉ thị xuất hàng **CŨ** (trước khi chỉnh sửa) |
| **2** | **AE** (No.30) | Đăng ký ĐK chỉ thị (Sau khi sửa) | `ShipmentAssign` | `A03` | `UP2` | `02` (Đã chỉ thị) | Dữ liệu chỉ thị xuất hàng **MỚI** (sau khi chỉnh sửa) |
| **3** | **AF** (No.31) | Điều chỉnh ĐK đơn hàng (Đảo ngược - Sau) | `SalesOrder` | `A02` | `UP1` | `01` (Chưa chỉ thị) | Dữ liệu đơn hàng điều chỉnh tương ứng trạng thái trước cập nhật |
| **4** | **AG** (No.32) | Điều chỉnh ĐK đơn hàng (Đảo ngược - Trước) | `SalesOrder` | `A02` | `UP2` | `01` (Chưa chỉ thị) | Dữ liệu đơn hàng điều chỉnh tương ứng trạng thái sau cập nhật |

---

### 3.3. Chế độ Xóa (Delete) — Hủy chỉ thị xuất hàng
Khi hủy/xóa chỉ thị xuất hàng đã đăng ký, tạo **2 DTOs** ứng với cột **AH, AI** để hoàn trả tồn kho về trạng thái chờ chỉ thị:

| STT | Cột | Tên Action / Mục đích | Template | VoucherType | EADClassSec | ScheduleSec | Nguồn Mapper Dữ liệu Chính |
|:---:|:---:|:---|:---:|:---:|:---:|:---:|:---|
| **1** | **AH** (No.33) | Xóa ĐK chỉ thị dự kiến | `ShipmentAssign` | `A03` | `DEL` | `02` (Đã chỉ thị) | Dữ liệu chỉ thị xuất hàng bị xóa |
| **2** | **AI** (No.34) | Khôi phục ĐK đơn hàng dự kiến | `SalesOrder` | `A02` | `ADD` | `01` (Chưa chỉ thị) | Dữ liệu đơn hàng được khôi phục về trạng thái chờ chỉ thị |

---

## 4. Bảng Tra cứu Mã Trường Dữ liệu (Property Mapping Table)

Khi map từ Entity/Model nguồn sang `UpdateStockDTO`, tham chiếu theo bảng sau:

### 4.1. DTO từ SalesOrder Template (Cột AB, AF, AG, AI)
- `EADVoucherTypeSec` = `"A02"`
- `EADVoucherNo` = `t_sales_order_dtl.sales_order_no`
- `EADVoucherRowNo` = `t_sales_order_dtl.sales_order_row_no`
- `EADDate` = `t_sales_order_dtl.shipment_scheduled_date`
- `ArticleCd` = `t_sales_order_dtl.article_cd`
- `BaseCd` = `t_sales_order.base_cd`
- `DepositoryCd` = `t_sales_order_dtl.depository_cd`
- `StockLocationCd` = `t_sales_order_dtl.stock_location_cd`
- `LotNo` = `""`
- `TransactionSec` = `t_sales_order.transaction_sec`
- `OwnerSec` = Tra cứu theo Common Rule (1 -> "1", 2 -> "2")
- `OwnerCd` = Tra cứu theo Common Rule (Nếu OwnerSec="2" -> `t_sales_order.client_cd`, ngược lại `""`)
- `EADDataSec` = `"4"` (Scheduled Outbound)
- `OriQtty` = Số lượng thay đổi/chỉ thị quy đổi theo unit đơn hàng
- `OriUnitCd` = `t_sales_order_dtl.sales_order_qtty_unit_cd`
- `EADQtty` = Số lượng quy đổi ra đơn vị quản lý tồn kho cơ bản (`basic_qtty`)
- `UnitCd` = `t_sales_order_dtl.basic_unit_cd`
- `StockAllocationNo` = `t_sales_order_dtl_ctrl.stock_allocation_no`

### 4.2. DTO từ ShipmentAssign Template (Cột AC, AD, AE, AH)
- `EADVoucherTypeSec` = `"A03"`
- `EADVoucherNo` = `t_shipment_assign_dtl.shipment_assign_no`
- `EADVoucherRowNo` = `t_shipment_assign_dtl.shipment_assign_row_no`
- `EADDate` = `t_shipment_assign_dtl.shipment_date`
- `ArticleCd` = `t_shipment_allocation_dtl.article_cd`
- `BaseCd` = `t_shipment_assign_dtl.base_cd`
- `DepositoryCd` = `t_shipment_allocation_dtl.depository_cd`
- `StockLocationCd` = `t_shipment_allocation_dtl.stock_location_cd`
- `LotNo` = `t_shipment_allocation_dtl.lot_no`
- `TransactionSec` = `t_shipment_allocation_dtl.transaction_sec`
- `OwnerSec` = `t_shipment_allocation_dtl.owner_sec`
- `OwnerCd` = `t_shipment_allocation_dtl.owner_cd`
- `EADDataSec` = `"4"`
- `OriQtty` = `t_shipment_assign_dtl.shipment_assign_qtty`
- `OriUnitCd` = `t_shipment_assign_dtl.shipment_assign_qtty_unit_cd`
- `EADQtty` = `t_shipment_allocation_dtl.shipment_assign_basic_qtty`
- `UnitCd` = `t_shipment_allocation_dtl.shipment_assign_basic_unit_cd`
- `StockAllocationNo` = `t_sales_order_dtl_ctrl.stock_allocation_no`

---

## 5. Hướng dẫn Luồng Xử lý Code cho Agent (Implementation Steps)

### Bước 1: Kiểm tra sản phẩm có quản lý tồn kho hay không
Chỉ thực hiện tạo DTO và gọi Stock Update đối với các sản phẩm có `m_article.stock_mng_flg = true`.

### Bước 2: Xử lý theo Action Type

#### Trường hợp 1: Đăng ký (Create / POST)
```csharp
var stockDtos = new List<UpdateStockDTO>();

foreach (var item in requestItems)
{
    // 1. Tạo DTO cột AB (Xóa ĐK đơn hàng dự kiến)
    var dtoAB = BuildSalesOrderDto(item, eadClassSec: "DEL", scheduleSec: "01");
    stockDtos.Add(dtoAB);

    // 2. Tạo DTO cột AC (Đăng ký ĐK chỉ thị)
    var dtoAC = BuildShipmentAssignDto(item, eadClassSec: "ADD", scheduleSec: "02");
    stockDtos.Add(dtoAC);
}

// Gọi thực thi cập nhật tồn kho
await InvokeStockUpdateAsync(stockDtos);
```

#### Trường hợp 2: Cập nhật (Update / PUT)
```csharp
var stockDtos = new List<UpdateStockDTO>();

foreach (var item in modifiedItems)
{
    // 1. DTO Cột AD: Xóa ĐK chỉ thị cũ (Before UP1)
    var dtoAD = BuildShipmentAssignDto(item.OldData, eadClassSec: "UP1", scheduleSec: "02");
    
    // 2. DTO Cột AE: Đăng ký ĐK chỉ thị mới (After UP2)
    var dtoAE = BuildShipmentAssignDto(item.NewData, eadClassSec: "UP2", scheduleSec: "02");

    // 3. DTO Cột AF: Điều chỉnh ĐK đơn hàng (After Đảo ngược UP1)
    var dtoAF = BuildSalesOrderDto(item.OldData, eadClassSec: "UP1", scheduleSec: "01");

    // 4. DTO Cột AG: Điều chỉnh ĐK đơn hàng (Before Đảo ngược UP2)
    var dtoAG = BuildSalesOrderDto(item.NewData, eadClassSec: "UP2", scheduleSec: "01");

    stockDtos.AddRange(new[] { dtoAD, dtoAE, dtoAF, dtoAG });
}

await InvokeStockUpdateAsync(stockDtos);
```

#### Trường hợp 3: Xóa (Delete / DELETE)
```csharp
var stockDtos = new List<UpdateStockDTO>();

foreach (var item in deletedItems)
{
    // 1. DTO Cột AH: Xóa ĐK chỉ thị (DEL)
    var dtoAH = BuildShipmentAssignDto(item, eadClassSec: "DEL", scheduleSec: "02");

    // 2. DTO Cột AI: Khôi phục ĐK đơn hàng (ADD)
    var dtoAI = BuildSalesOrderDto(item, eadClassSec: "ADD", scheduleSec: "01");

    stockDtos.AddRange(new[] { dtoAH, dtoAI });
}

await InvokeStockUpdateAsync(stockDtos);
```

---

## 6. Lưu ý Quan trọng cho AI Agent
1. **Lắp ráp DTO theo đúng thứ tự**: Đảm bảo các DTO trong danh sách được thêm vào đúng trình tự nghiệp vụ (Ví dụ khi Đăng ký: DTO AB trước, DTO AC sau).
2. **Không hard-code `ProcessCd`**: Mặc định đặt là `""`.
3. **Cắt chuỗi VoucherType**: Nếu nhận dạng từ spec dạng `A02:SalesOrder` thì chỉ lấy phần code `A02`.
4. **Không bỏ qua Check Stock Flag**: Nếu `stock_mng_flg = false`, bỏ qua việc gọi `InvokeStockUpdateAsync` cho dòng đó.
5. **Tạo DTO cho CẢ CẤP DETAIL VÀ CẤP LOT**: Khi sinh các DTO ShipmentAssign (Cột AC, AD, AE, AH), bắt buộc phải sinh DTO ShipmentAssign cho cấp dòng chi tiết (`AC03001StockDetailModel`) VÀ sinh thêm DTO ShipmentAssign cho từng dòng phân bổ lot (`AC03001LotStockScheduleModel`) nếu có. TUYỆT ĐỐI KHÔNG chọn 1 trong 2 (không dùng if/else phân nhánh chọn duy nhất 1 nguồn); phải đưa cả 2 loại DTO này vào danh sách.

