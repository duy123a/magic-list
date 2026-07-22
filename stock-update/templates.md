# Stock Update Templates

## Overview

Most stock update patterns reuse the same field mapping.

To avoid duplication, all patterns are grouped into reusable templates.

Each pattern only changes:

- EADClassSec
- ScheduleSec

All remaining mappings follow the template definition.

---

# SalesOrder Template

Use this template when the inventory transaction is based on **Sales Order** data.

## Source Tables

Main

- `t_sales_order`
- `t_sales_order_dtl`
- `t_sales_order_dtl_ctrl`

## Field Mapping

| UpdateStockDTO | Source |
|----------------|--------|
| EADVoucherNo | `t_sales_order_dtl.sales_order_no` |
| EADVoucherRowNo | `t_sales_order_dtl.sales_order_row_no` |
| EADDate | `t_sales_order_dtl.shipment_scheduled_date` |
| ArticleCd | `t_sales_order_dtl.article_cd` |
| BaseCd | `t_sales_order.base_cd` |
| DepositoryCd | `t_sales_order_dtl.depository_cd` |
| StockLocationCd | `t_sales_order_dtl.stock_location_cd` |
| LotNo | `""` |
| ProcessCd | `""` |
| TransactionSec | `t_sales_order.transaction_sec` |
| OwnerSec | Apply Common Rule A |
| OwnerCd | Apply Common Rule B |
| EADDataSec | `"4"` |
| OriQtty | `t_sales_order_dtl.sales_order_qtty` *(See Note 1)* |
| OriUnitCd | `t_sales_order_dtl.sales_order_qtty_unit_cd` |
| EADQtty | `t_sales_order_dtl.basic_qtty` |
| UnitCd | `t_sales_order_dtl.basic_unit_cd` |
| EADUnitPrice | `0` |
| OriAmt | `0` |
| EADAmt | `0` |
| StockAllocationNo | `t_sales_order_dtl_ctrl.stock_allocation_no` |

ScheduleSec and EADClassSec are determined by the Pattern.

---

# ShipmentAssign Template

Use this template when the inventory transaction is based on **Shipment Assignment** data.

## Source Tables

Main

- `t_shipment_assign_dtl`
- `t_shipment_allocation_dtl`
- `t_sales_order_dtl_ctrl`

## Field Mapping

| UpdateStockDTO | Source |
|----------------|--------|
| EADVoucherNo | `t_shipment_assign_dtl.shipment_assign_no` |
| EADVoucherRowNo | `t_shipment_assign_dtl.shipment_assign_row_no` |
| EADDate | `t_shipment_assign_dtl.shipment_date` |
| ArticleCd | `t_shipment_allocation_dtl.article_cd` |
| BaseCd | `t_shipment_assign_dtl.base_cd` |
| DepositoryCd | `t_shipment_allocation_dtl.depository_cd` |
| StockLocationCd | `t_shipment_allocation_dtl.stock_location_cd` |
| LotNo | `t_shipment_allocation_dtl.lot_no` |
| ProcessCd | `""` |
| TransactionSec | `t_shipment_allocation_dtl.transaction_sec` |
| OwnerSec | `t_shipment_allocation_dtl.owner_sec` |
| OwnerCd | `t_shipment_allocation_dtl.owner_cd` |
| EADDataSec | `"4"` |
| OriQtty | `t_shipment_assign_dtl.shipment_assign_qtty` *(See Note 1)* |
| OriUnitCd | `t_shipment_assign_dtl.shipment_assign_qtty_unit_cd` |
| EADQtty | `t_shipment_allocation_dtl.shipment_assign_basic_qtty` |
| UnitCd | `t_shipment_allocation_dtl.shipment_assign_basic_unit_cd` |
| EADUnitPrice | `0` |
| OriAmt | `0` |
| EADAmt | `0` |
| StockAllocationNo | `t_sales_order_dtl_ctrl.stock_allocation_no` |

ScheduleSec and EADClassSec are determined by the Pattern.

---

# Notes

## Note 1

`OriQtty` represents the quantity in the document unit before conversion.

`EADQtty` represents the quantity after conversion into the inventory management unit.

The source columns differ depending on the business document.

## Note 2

Owner information follows **Common Rules** unless explicitly mapped from the source table.

## Note 3

Fields not listed in this document should follow the default values defined in `common-rules.md`.