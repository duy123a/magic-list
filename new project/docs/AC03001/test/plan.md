# AC03001 Test Plan

## Phase 1: Unit Tests

### 1.1. Validation Tests
- [x] Header validation (CtrlerCd, ShipmentDate)
- [x] Detail validation (SalesOrderNo, SalesOrderRowNo, AssignCheck)
- [x] Conditional validation for lines with `AssignCheck=true`
- [x] Lot allocation validation

### 1.2. Service Tests
- [x] Success case (valid data)
- [x] Validation error case (invalid data)
- [x] Business rule error case (e.g., insufficient quantity)
- [x] Mock all dependencies (repositories, UnitOfWork, NumberingService, QuantityConversionService)
- [x] Cover all branches (if/else, switch, ternary, null checks)

### 1.3. Controller Tests
- [x] HTTP 201 (Created) for success case
- [x] HTTP 400 (Validation error)
- [x] HTTP 422 (Business rule error)

### 1.4. Test Evidence
- [x] `AC03001_EvidenceCases.tsv` generated

### 1.5. Coverage Report
- [x] HTML coverage report generated (testable logic only)

### 1.6. 100% Coverage (Testable Logic)
- [x] **Completed** (Validation, Service Flow, Controller)
- [ ] **Pending** (DB operations - requires entity types)

## Phase 2: Integration Tests
- [ ] API integration tests
- [ ] DB integration tests

## Phase 3: E2E Tests
- [ ] UI-driven tests
- [ ] Business workflow tests