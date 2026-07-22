# Common Rules

## Purpose

This document defines common rules for constructing `UpdateStockDTO`.

These rules apply to all stock update patterns unless explicitly overridden by a specific pattern.

---

# Default Values

The following properties should use the default values below unless a pattern specifies otherwise.

| Property | Default Value |
|----------|---------------|
| ProcessCd | `""` (Empty string) |
| EADUnitPrice | `0` |
| OriAmt | `0` |
| EADAmt | `0` |
| CompletedFlg | `false` |

---

# OwnerSec

When `OwnerSec` is not directly provided by the source table, determine it from `TransactionSec`.

| TransactionSec | OwnerSec |
|---------------|----------|
| `1` (Normal) | `1` (Own Company) |
| `2` (Consignment) | `2` (Customer) |

If a pattern explicitly maps `OwnerSec` from a source column, use the mapped value instead of applying this rule.

---

# OwnerCd

When `OwnerCd` is not directly provided by the source table, determine it from `OwnerSec`.

| OwnerSec | OwnerCd |
|----------|---------|
| `1` (Own Company) | `""` (Empty string) |
| `2` (Customer) | Customer Code of the corresponding business document |

For Sales Order based patterns, use:

```
t_sales_order.client_cd
```

If a pattern explicitly maps `OwnerCd` from a source column, use the mapped value instead.

---

# EADDataSec

`EADDataSec` represents the inventory transaction type.

For Shipment Assignment stock updates described in this specification, always use:

```
4
```

Meaning:

```
Scheduled Outbound
```

---

# ProcessCd

Unless explicitly required by a business process,

```
ProcessCd = ""
```

---

# ScheduleSec

`ScheduleSec` is determined by the stock update pattern.

| Value | Meaning |
|-------|---------|
| `1` | Not Assigned |
| `2` | Assigned |

Do not hard-code this value.
Always follow the mapping defined in `patterns.md`.

---

# EADClassSec

`EADClassSec` is determined by the stock update pattern.

Possible values:

| Value | Meaning |
|-------|---------|
| `ADD` | Register inventory transaction |
| `UP1` | State before update |
| `UP2` | State after update |
| `DEL` | Delete inventory transaction |

Do not hard-code this value.
Always follow the mapping defined in `patterns.md`.

---

# Voucher Type

Some specifications describe voucher types in the following format:

| Specification | Actual Value |
|--------------|--------------|
| `A02:SalesOrder` | `A02` |
| `A03:ShipmentAssign` | `A03` |

Only the code before `:` should be assigned to `EADVoucherTypeSec`.

Examples:

```
A02:SalesOrder
```

↓

```
A02
```

---

# Quantity

`OriQtty`, `OriUnitCd`, `EADQtty`, and `UnitCd` depend on the business document.

Do not assume a fixed source column.

Always follow the mapping defined by the corresponding template and pattern.

---

# Source Priority

When both a Common Rule and a Pattern define the same property, the Pattern takes precedence.

When both a Template and a Pattern define the same property, the Pattern takes precedence.

Priority:

```
Pattern
    >
Template
    >
Common Rules
```

---
# Implementation Notes

When implementing stock update logic:

1. Determine the corresponding stock update pattern.
2. Select the appropriate template.
3. Populate `UpdateStockDTO`.
4. Apply the common rules defined in this document.
5. Override any properties required by the selected pattern.
6. Call `InvokeStockUpdateAsync(dtoList)`.