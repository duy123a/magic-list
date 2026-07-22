# Stock Update Processing Guide for AI Agent - AC03001

## 1. Overview & Purpose
This document provides technical instructions for AI Agents to implement the **Stock Update** logic for the **Shipment Assignment (AC03001)** feature, based on the business rule matrix (Columns **AB to AI**, No. 27 to No. 34).

In `AC03001StockUpdateService` (named may changed depend on the service), stock update logic is invoked via the private method:
```csharp
private async Task InvokeStockUpdateAsync(IEnumerable<UpdateStockDTO> dtoList)
```

When executing **Create**, **Update**, or **Delete** actions for shipment instructions in AC03001, the system must initialize a list of `UpdateStockDTO` corresponding to each action column and call `InvokeStockUpdateAsync`.

---

## 2. General Rules & Mapping Specifications (`UpdateStockDTO`)

Each DTO represents an inventory transaction movement. Properties must comply with:
- **`Common Rules`**: Default values (`EADUnitPrice = 0`, `OriAmt = 0`, `EADAmt = 0`, `CompletedFlg = false`, `ProcessCd = ""`, etc.).
- **`Templates`**: Uses two primary templates:
  1. **SalesOrder Template** (Based on sales order data: `t_sales_order`, `t_sales_order_dtl`, `t_sales_order_dtl_ctrl`).
  2. **ShipmentAssign Template** (Based on shipment assignment data: `t_shipment_assign_dtl`, `t_shipment_allocation_dtl`, `t_sales_order_dtl_ctrl`).

---

## 3. Detailed Mapping Matrix by Action (Columns AB -> AI)

### 3.1. Registration Mode (Create) (belong to section 6 of AC03001_API.md, api/sales/shipment-assign/ac03001, method POST)
Each newly registered shipment assignment row generates **2 DTOs** passed to `InvokeStockUpdateAsync` in sequence:

| No. | Column | Action Name / Purpose | Template | VoucherType | EADClassSec | ScheduleSec | Primary Data Mapping Source |
|:---:|:---:|:---|:---:|:---:|:---:|:---:|:---|
| **1** | **AB** (No.27) | Delete Scheduled Sales Order | `SalesOrder` | `A02` | `DEL` | `1` (Not Assigned) | - `VoucherNo`: `t_sales_order_dtl.sales_order_no`<br>- `Date`: `t_sales_order_dtl.shipment_scheduled_date`<br>- `Article`: `t_sales_order_dtl.article_cd` |
| **2** | **AC** (No.28) | Register Scheduled Shipment | `ShipmentAssign` | `A03` | `ADD` | `2` (Assigned) | - `VoucherNo`: `t_shipment_assign_dtl.shipment_assign_no`<br>- `Date`: `t_shipment_assign_dtl.shipment_date`<br>- `Article`: `t_shipment_allocation_dtl.article_cd` |

---

### 3.2. Update Mode (Update) (belong to section 7 of AC03001_API.md, update mode ShipmentAssignDtlList[].ModelState = Modified, api/sales/shipment-assign/ac03001, method PUT)
When modifying shipment instruction details or allocations, generate **4 DTOs** corresponding to columns **AD, AE, AF, AG**:

| No. | Column | Action Name / Purpose | Template | VoucherType | EADClassSec | ScheduleSec | Primary Data Mapping Source |
|:---:|:---:|:---|:---:|:---:|:---:|:---:|:---|
| **1** | **AD** (No.29) | Delete Scheduled Shipment (Pre-update) | `ShipmentAssign` | `A03` | `UP1` | `2` (Assigned) | **OLD** shipment instruction data (before edits) |
| **2** | **AE** (No.30) | Register Scheduled Shipment (Post-update) | `ShipmentAssign` | `A03` | `UP2` | `2` (Assigned) | **NEW** shipment instruction data (after edits) |
| **3** | **AF** (No.31) | Adjust Scheduled Sales Order (Reverse - Post) | `SalesOrder` | `A02` | `UP1` | `1` (Not Assigned) | Adjusted sales order data corresponding to state before update |
| **4** | **AG** (No.32) | Adjust Scheduled Sales Order (Reverse - Pre) | `SalesOrder` | `A02` | `UP2` | `1` (Not Assigned) | Adjusted sales order data corresponding to state after update |

---

### 3.3. Deletion Mode (Delete) (belong to section 7 of AC03001_API.md, delete mode ShipmentAssignDtlList[].ModelState = Deleted, api/sales/shipment-assign/ac03001, method PUT)
When canceling/deleting registered shipment instructions, generate **2 DTOs** for columns **AH, AI** to return inventory back to the unassigned state:

| No. | Column | Action Name / Purpose | Template | VoucherType | EADClassSec | ScheduleSec | Primary Data Mapping Source |
|:---:|:---:|:---|:---:|:---:|:---:|:---:|:---|
| **1** | **AH** (No.33) | Delete Scheduled Shipment | `ShipmentAssign` | `A03` | `DEL` | `2` (Assigned) | Deleted shipment instruction data |
| **2** | **AI** (No.34) | Restore Scheduled Sales Order | `SalesOrder` | `A02` | `ADD` | `1` (Not Assigned) | Restored sales order data returned to pending instruction state |

---

## 4. Field Property Mapping Table

### 4.1. DTO from SalesOrder Template (Columns AB, AF, AG, AI)
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
- `OwnerSec` = Derived via Common Rule (1 -> "1", 2 -> "2")
- `OwnerCd` = Derived via Common Rule (If OwnerSec="2" -> `t_sales_order.client_cd`, else `""`)
- `EADDataSec` = `"4"` (Scheduled Outbound)
- `OriQtty` = Quantity converted to sales order unit
- `OriUnitCd` = `t_sales_order_dtl.sales_order_qtty_unit_cd`
- `EADQtty` = Quantity converted to basic inventory management unit (`basic_qtty`)
- `UnitCd` = `t_sales_order_dtl.basic_unit_cd`
- `StockAllocationNo` = `t_sales_order_dtl_ctrl.stock_allocation_no`

### 4.2. DTO from ShipmentAssign Template (Columns AC, AD, AE, AH)
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

## 5. Implementation Steps for AI Agent

### Step 1: Stock Management Check
Only process stock update DTO creation if the article has stock management enabled (`m_article.stock_mng_flg = true`).

### Step 2: Implementation by Action Type

#### Scenario 1: Registration (Create / POST)
```csharp
var stockDtos = new List<UpdateStockDTO>();

foreach (var item in requestItems)
{
    // Skip if article does not manage stock
    if (!item.StockMngFlg) continue;

    // 1. Create DTO for Column AB (Delete Scheduled Sales Order)
    var dtoAB = BuildSalesOrderDto(item, eadClassSec: "DEL", scheduleSec: "1");
    stockDtos.Add(dtoAB);

    // 2. Create DTO for Column AC (Register Scheduled Shipment)
    var dtoAC = BuildShipmentAssignDto(item, eadClassSec: "ADD", scheduleSec: "2");
    stockDtos.Add(dtoAC);
}

// Execute inventory update
await InvokeStockUpdateAsync(stockDtos);
```

#### Scenario 2: Update (Update / PUT)
```csharp
var stockDtos = new List<UpdateStockDTO>();

foreach (var item in modifiedItems)
{
    // Skip if article does not manage stock
    if (!item.StockMngFlg) continue;

    // 1. Create DTO for Column AD: Delete Scheduled Shipment (Before UP1)
    var dtoAD = BuildShipmentAssignDto(item.OldData, eadClassSec: "UP1", scheduleSec: "2");
    
    // 2. Create DTO for Column AE: Register Scheduled Shipment (After UP2)
    var dtoAE = BuildShipmentAssignDto(item.NewData, eadClassSec: "UP2", scheduleSec: "2");

    // 3. Create DTO for Column AF: Adjust Scheduled Sales Order (Reverse - After UP1)
    var dtoAF = BuildSalesOrderDto(item.OldData, eadClassSec: "UP1", scheduleSec: "1");

    // 4. Create DTO for Column AG: Adjust Scheduled Sales Order (Reverse - Before UP2)
    var dtoAG = BuildSalesOrderDto(item.NewData, eadClassSec: "UP2", scheduleSec: "1");

    stockDtos.AddRange(new[] { dtoAD, dtoAE, dtoAF, dtoAG });
}

// Execute inventory update
await InvokeStockUpdateAsync(stockDtos);
```

#### Scenario 3: Deletion (Delete / DELETE)
```csharp
var stockDtos = new List<UpdateStockDTO>();

foreach (var item in deletedItems)
{
    // Skip if article does not manage stock
    if (!item.StockMngFlg) continue;

    // 1. Create DTO for Column AH: Delete Scheduled Shipment (DEL)
    var dtoAH = BuildShipmentAssignDto(item, eadClassSec: "DEL", scheduleSec: "2");

    // 2. Create DTO for Column AI: Restore Scheduled Sales Order (ADD)
    var dtoAI = BuildSalesOrderDto(item, eadClassSec: "ADD", scheduleSec: "1");

    stockDtos.AddRange(new[] { dtoAH, dtoAI });
}

// Execute inventory update
await InvokeStockUpdateAsync(stockDtos);
```

---

## 6. Important Notes for AI Agent
1. **Maintain DTO Sequence**: Ensure DTOs are appended in the exact business order (e.g., Column AB before Column AC in Create mode).
2. **ScheduleSec Formatting**: Use single-digit strings (`"1"` for Not Assigned, `"2"` for Assigned) without leading zeros.
3. **Do not hard-code `ProcessCd`**: Default to `""`.
4. **Trim VoucherType**: Extract only the code prefix before the colon if given in format `A02:SalesOrder` -> `"A02"`.
5. **Always Verify Stock Management Flag**: If `stock_mng_flg = false`, skip generating stock DTOs for that item.
6. **Include BOTH Detail-Level and Lot-Level DTOs**: When generating ShipmentAssign DTOs (Columns AC, AD, AE, AH), generate the ShipmentAssign DTO from the detail line (`AC03001StockDetailModel`) AND ALSO generate ShipmentAssign DTOs for all matching lot allocations (`AC03001LotStockScheduleModel`) if present. Do NOT choose one over the other (do NOT do either/or); both detail-level and lot-level DTOs must be included in the DTO list.

