-- SQL Scripts for AC03001 Database Schema

-- Create t_shipment_assign table
CREATE TABLE t_shipment_assign (
    shipment_assign_no VARCHAR(20) NOT NULL,
    shipment_assign_date TIMESTAMP NOT NULL,
    shipment_assign_ctrler_cd VARCHAR(10) NOT NULL,
    shipment_date TIMESTAMP NOT NULL,
    tenant_code VARCHAR(10) NOT NULL,
    CONSTRAINT pk_t_shipment_assign PRIMARY KEY (shipment_assign_no)
);

-- Create t_shipment_assign_dtl table
CREATE TABLE t_shipment_assign_dtl (
    shipment_assign_no VARCHAR(20) NOT NULL,
    shipment_assign_row_no INTEGER NOT NULL,
    sales_order_no VARCHAR(20) NOT NULL,
    sales_order_row_no INTEGER NOT NULL,
    shipment_assign_qtty DECIMAL(21, 6) NOT NULL,
    shipment_assign_qtty_unit_cd VARCHAR(10) NOT NULL,
    basic_qtty DECIMAL(21, 6) NOT NULL,
    basic_unit_cd VARCHAR(10) NOT NULL,
    pkg_qtty DECIMAL(21, 6),
    pkg_unit_cd VARCHAR(10),
    shipment_scheduled_date TIMESTAMP,
    delivery_scheduled_date TIMESTAMP,
    delivery_scheduled_time_sec VARCHAR(10),
    shipment_date TIMESTAMP NOT NULL,
    depository_cd VARCHAR(10),
    stock_location_cd VARCHAR(10),
    complete_an_order_flg BOOLEAN DEFAULT FALSE,
    tenant_code VARCHAR(10) NOT NULL,
    CONSTRAINT pk_t_shipment_assign_dtl PRIMARY KEY (shipment_assign_no, shipment_assign_row_no)
);

-- Create t_shipment_assign_dtl_note table
CREATE TABLE t_shipment_assign_dtl_note (
    shipment_assign_no VARCHAR(20) NOT NULL,
    shipment_assign_row_no INTEGER NOT NULL,
    carrier_cd VARCHAR(10),
    packing_spec VARCHAR(100),
    shipment_assign_doc_note TEXT,
    shipment_guide_note TEXT,
    bol_memo TEXT,
    tenant_code VARCHAR(10) NOT NULL,
    CONSTRAINT pk_t_shipment_assign_dtl_note PRIMARY KEY (shipment_assign_no, shipment_assign_row_no)
);

-- Create t_shipment_allocation table
CREATE TABLE t_shipment_allocation (
    shipment_allocation_no VARCHAR(20) NOT NULL,
    shipment_assign_no VARCHAR(20) NOT NULL,
    shipment_assign_row_no INTEGER NOT NULL,
    sales_order_no VARCHAR(20) NOT NULL,
    sales_order_row_no INTEGER NOT NULL,
    tenant_code VARCHAR(10) NOT NULL,
    CONSTRAINT pk_t_shipment_allocation PRIMARY KEY (shipment_allocation_no)
);

-- Create t_shipment_allocation_dtl table
CREATE TABLE t_shipment_allocation_dtl (
    shipment_allocation_no VARCHAR(20) NOT NULL,
    shipment_allocation_row_no INTEGER NOT NULL,
    depository_cd VARCHAR(10) NOT NULL,
    stock_location_cd VARCHAR(10) NOT NULL,
    lot_no VARCHAR(50),
    allocation_qtty DECIMAL(21, 6) NOT NULL,
    shipment_qtty DECIMAL(21, 6),
    shipment_unit_cd VARCHAR(10),
    shipment_basic_unit_cd VARCHAR(10),
    tenant_code VARCHAR(10) NOT NULL,
    CONSTRAINT pk_t_shipment_allocation_dtl PRIMARY KEY (shipment_allocation_no, shipment_allocation_row_no)
);

-- Add missing column to t_sales_order_dtl_ctrl
ALTER TABLE t_sales_order_dtl_ctrl
ADD COLUMN sum_total_shipment_assign_qtty DECIMAL(21, 6) DEFAULT 0;

-- Create indexes
CREATE INDEX idx_t_shipment_assign_shipment_assign_no ON t_shipment_assign (shipment_assign_no);
CREATE INDEX idx_t_shipment_assign_dtl_shipment_assign_no ON t_shipment_assign_dtl (shipment_assign_no);
CREATE INDEX idx_t_shipment_assign_dtl_sales_order_no ON t_shipment_assign_dtl (sales_order_no);
CREATE INDEX idx_t_shipment_allocation_shipment_assign_no ON t_shipment_allocation (shipment_assign_no);