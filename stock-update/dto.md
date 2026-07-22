# UpdateStockDTO Specification

## Purpose

`UpdateStockDTO` is the input model used for inventory update processing.

When a business process requires updating inventory, the business service is responsible for:

1. Creating one or more `UpdateStockDTO` objects.
2. Populating all required properties according to the corresponding stock update pattern.
3. Calling the inventory update method.

The inventory update method always has the following signature:

```csharp
private async Task InvokeStockUpdateAsync(IEnumerable<UpdateStockDTO> dtoList)
```

Although the implementation may exist in different business services, the method signature and behavior are consistent.

`InvokeStockUpdateAsync()` is responsible for:

- Validating the input data.
- Performing inventory update processing.
- Persisting inventory transactions.

---

# DTO Definition

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| EADVoucherTypeSec | string | Yes | Inventory voucher type. Value comes from business module. |
| EADVoucherNo | string | Yes | Voucher number. |
| EADVoucherRowNo | int | Yes | Voucher row number. |
| EADClassSec | string | Yes | Inventory operation type. Possible values: `ADD`, `UP1`, `UP2`, `DEL`. |
| EADDate | DateOnly | Yes | Inventory date. |
| ArticleCd | string | Yes | Item code. |
| BaseCd | string | Yes | Base code. Empty string allowed. |
| DepositoryCd | string | Yes | Depository code. Empty string allowed. |
| StockLocationCd | string | Yes | Stock location code. Empty string allowed. |
| LotNo | string | Yes | Lot number. Empty string allowed. |
| ProcessCd | string | Yes | Process code. Unless specified otherwise, use empty string. |
| TransactionSec | string | Yes | Transaction type. |
| OwnerSec | string | Yes | Owner type. |
| OwnerCd | string | Yes | Owner code. |
| EADDataSec | string | Yes | Inventory data type. |
| OriQtty | decimal | No | Quantity before unit conversion. |
| OriUnitCd | string | No | Original unit. |
| EADQtty | decimal | Yes | Quantity after conversion to inventory unit. |
| UnitCd | string | No | Inventory unit. |
| EADUnitPrice | decimal | No | Inventory unit price. Usually 0. |
| OriAmt | decimal | No | Original amount. Usually 0. |
| EADAmt | decimal | No | Inventory amount. Usually 0. |
| StockAllocationNo | string | No | Stock allocation number. |
| ScheduleSec | string | Conditional | Required only for scheduled inventory. |
| CompletedFlg | bool | No | True only when completing the entire document. Usually false. |

---

# Property Details

## EADVoucherTypeSec

`EADVoucherTypeSec` identifies the business document type.

Some specifications describe the value in the following format:

| Specification | Actual Value |
|--------------|--------------|
| `A02:SalesOrder` | `A02` |
| `A03:ShipmentAssign` | `A03` |

**Only the code before the colon (`:`) should be assigned to `EADVoucherTypeSec`.**

Do **not** include the descriptive text after the colon.

Examples:

| Specification | DTO Value |
|--------------|-----------|
| `A02:SalesOrder` | `"A02"` |
| `A03:ShipmentAssign` | `"A03"` |

---

## EADVoucherNo

Business document number.

Examples

Sales Order

```
SO000001
```

Shipment Assignment

```
SA000001
```

Map from the corresponding source document.

---

## EADVoucherRowNo

Business document detail row number.

Must match the source detail row.

---

## EADClassSec

Represents inventory operation.

Possible values

| Value | Meaning |
|--------|----------|
| ADD | Register inventory movement |
| UP1 | Inventory state before update |
| UP2 | Inventory state after update |
| DEL | Delete inventory movement |

The required value is determined by the stock update pattern.

---

## EADDate

Inventory transaction date.

Example

Sales Order

```
shipment_scheduled_date
```

Shipment Assignment

```
shipment_date
```

---

## ArticleCd

Inventory item code.

Map directly from source.

---

## BaseCd

Warehouse base.

Map directly from source.

Empty string is allowed.

---

## DepositoryCd

Depository.

Map directly from source.

Empty string is allowed.

---

## StockLocationCd

Stock location.

Map directly from source.

Empty string is allowed.

---

## LotNo

Lot number.

Use source value when available.

Otherwise use

```
""
```

---

## ProcessCd

Unless specified by business logic

Always

```
""
```

---

## TransactionSec

Inventory transaction type.

Possible values

| Value | Meaning |
|--------|----------|
|1|Normal|
|2|Consignment|
|3|Customer Free Supply|
|4|Shipped Not Inspected|
|5|Received Not Inspected|

Map directly from source.

---

## OwnerSec

Owner classification.

Normally derived from TransactionSec.

See Common Rules.

---

## OwnerCd

Owner code.

Normally derived from OwnerSec.

See Common Rules.

---

## EADDataSec

Inventory data type.

Possible values

| Value | Meaning |
|--------|----------|
|1|Inbound|
|2|Scheduled Inbound|
|3|Outbound|
|4|Scheduled Outbound|
|5|Adjustment|

Shipment Assignment uses

```
4
```

---

## OriQtty

Quantity before conversion.

Usually document quantity.

Examples

Sales Order

```
sales_order_qtty
```

Shipment Assignment

```
shipment_assign_qtty
```

---

## OriUnitCd

Original unit code.

Example

```
sales_order_qtty_unit_cd
```

---

## EADQtty

Quantity after conversion.

Usually

```
basic_qtty
```

or

```
shipment_assign_basic_qtty
```

depending on the source.

---

## UnitCd

Inventory unit.

Usually

```
basic_unit_cd
```

or

```
shipment_assign_basic_unit_cd
```

---

## EADUnitPrice

Inventory unit price.

Shipment Assignment

Always

```
0
```

---

## OriAmt

Original amount.

Shipment Assignment

Always

```
0
```

---

## EADAmt

Inventory amount.

Shipment Assignment

Always

```
0
```

---

## StockAllocationNo

Inventory allocation number.

Map from

```
stock_allocation_no
```

---

## ScheduleSec

Only required for scheduled inventory.

Possible values

| Value | Meaning |
|--------|----------|
|01|Not Assigned|
|02|Assigned|

---

## CompletedFlg

Normally

```
false
```

Only set

```
true
```

when business logic completes the document.