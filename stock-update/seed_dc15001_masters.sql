INSERT INTO tenant1.m_base (base_cd, sort, base_name, base_abbr_name, language_cd, calendar_cd, tax_cd, running_hour)
VALUES ('BASE-A', 1, '{"ja-JP": "拠点A"}', '{"ja-JP": "拠点A"}', 'ja-JP', '01', '01', 8)
ON CONFLICT (base_cd) DO NOTHING;

INSERT INTO tenant1.m_depository (depository_cd, depository_name, depository_abbr_name, del_flg)
VALUES ('DEP-B1', '{"ja-JP": "倉庫B1"}', '{"ja-JP": "倉庫B1"}', false),
       ('DEP-TRANS', '{"ja-JP": "移送中倉庫"}', '{"ja-JP": "移送中倉庫"}', false)
ON CONFLICT (depository_cd) DO NOTHING;

INSERT INTO tenant1.m_stock_location (stock_location_cd, stock_location_name, stock_location_abbr_name, del_flg)
VALUES ('LOC-B1', '{"ja-JP": "位置B1"}', '{"ja-JP": "位置B1"}', false)
ON CONFLICT (stock_location_cd) DO NOTHING;

INSERT INTO tenant1.m_form_universal_value (form_id, seq_no, universal_string, universal_value_description)
VALUES ('DC14001', 10, 'DEP-TRANS', '移送中倉庫コード')
ON CONFLICT (form_id, seq_no) DO UPDATE SET universal_string = 'DEP-TRANS';

INSERT INTO tenant1.t_stock (
    article_cd, depository_cd, stock_location_cd, lot_no, 
    owner_sec, owner_cd, transaction_sec, 
    current_stock_qtty, converted_current_stock_qtty, converted_stock_unit, 
    allocated_qtty, allocation_check_flg, created_at
) VALUES (
    'ART-001', 'DEP-TRANS', '', 'LOT-A-001', 
    '1', 'OWN-1', '1', 
    1000.0, 1000.0, '個', 
    0, false, NOW()
) 
ON CONFLICT (article_cd, depository_cd, stock_location_cd, lot_no, owner_sec, owner_cd, transaction_sec) 
DO UPDATE SET current_stock_qtty = 1000.0, converted_current_stock_qtty = 1000.0;

INSERT INTO tenant1.t_stock_process (
    article_cd, depository_cd, stock_location_cd, lot_no, process_cd,
    owner_sec, owner_cd, transaction_sec, current_stock_qtty, created_at
) VALUES (
    'ART-001', 'DEP-TRANS', '', 'LOT-A-001', 'PROC-A',
    '1', 'OWN-1', '1', 1000.0, NOW()
) ON CONFLICT (article_cd, depository_cd, stock_location_cd, lot_no, process_cd, owner_sec, owner_cd, transaction_sec)
DO UPDATE SET current_stock_qtty = 1000.0;
