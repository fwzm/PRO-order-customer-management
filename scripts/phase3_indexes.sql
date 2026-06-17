-- ============================================================
-- PRO 系统 - Phase 3 数据库索引优化建议
-- 目标：支持客户>1万、订单>10万大数据量场景
-- ============================================================

-- ==================== 客户表索引 ====================

-- 客户列表查询（按分公司+类型+名称/电话搜索）
CREATE INDEX IF NOT EXISTS idx_customers_branch_type_status ON "Customers" ("BranchId", "CustomerType", "Status");
CREATE INDEX IF NOT EXISTS idx_customers_name_search ON "Customers" USING gin (to_tsvector('simple', "Name"));
CREATE INDEX IF NOT EXISTS idx_customers_phone ON "Customers" ("Phone") WHERE "Phone" IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_customers_created_at ON "Customers" ("CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_customers_branch_created ON "Customers" ("BranchId", "CreatedAt" DESC);

-- ==================== 订单表索引 ====================

-- 订单列表查询（按分公司+状态+支付状态）
CREATE INDEX IF NOT EXISTS idx_orders_branch_status ON "Orders" ("BranchId", "Status", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_orders_payment_status ON "Orders" ("BranchId", "PaymentStatus", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_orders_customer ON "Orders" ("CustomerId", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_orders_order_no ON "Orders" ("OrderNo");
CREATE INDEX IF NOT EXISTS idx_orders_delivery_person ON "Orders" ("DeliveryPersonId") WHERE "DeliveryPersonId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_orders_status_transition ON "Orders" ("Status", "UpdatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_orders_branch_created ON "Orders" ("BranchId", "CreatedAt" DESC);

-- ==================== 收款记录索引 ====================

CREATE INDEX IF NOT EXISTS idx_payment_records_customer ON "PaymentRecords" ("CustomerId", "PaymentDate" DESC);
CREATE INDEX IF NOT EXISTS idx_payment_records_order ON "PaymentRecords" ("OrderId") WHERE "OrderId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_payment_records_date ON "PaymentRecords" ("PaymentDate" DESC);
CREATE INDEX IF NOT EXISTS idx_payment_records_branch ON "PaymentRecords" ("CustomerId", "PaymentDate" DESC);

-- ==================== 收款核销索引 ====================

CREATE INDEX IF NOT EXISTS idx_payment_allocations_payment ON "PaymentAllocations" ("PaymentRecordId");
CREATE INDEX IF NOT EXISTS idx_payment_allocations_order ON "PaymentAllocations" ("OrderId");
CREATE INDEX IF NOT EXISTS idx_payment_allocations_payment_order ON "PaymentAllocations" ("PaymentRecordId", "OrderId");

-- ==================== 库存变动日志索引 ====================

CREATE INDEX IF NOT EXISTS idx_inventory_change_logs_product ON "InventoryChangeLogs" ("ProductId", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_inventory_change_logs_type ON "InventoryChangeLogs" ("ChangeType", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_inventory_change_logs_date ON "InventoryChangeLogs" ("CreatedAt" DESC);

-- ==================== 审计日志索引 ====================

CREATE INDEX IF NOT EXISTS idx_audit_log_details_entity ON "AuditLogDetails" ("EntityType", "EntityId", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_audit_log_details_action ON "AuditLogDetails" ("ActionType", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_audit_log_details_operator ON "AuditLogDetails" ("OperatorId", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_audit_log_details_branch ON "AuditLogDetails" ("BranchId", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_audit_log_details_field ON "AuditLogDetails" ("EntityType", "FieldName", "CreatedAt" DESC);

-- ==================== 导出任务索引 ====================

CREATE INDEX IF NOT EXISTS idx_export_jobs_status ON "ExportJobs" ("Status", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_export_jobs_user ON "ExportJobs" ("RequestedById", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_export_jobs_branch ON "ExportJobs" ("BranchId", "CreatedAt" DESC);

-- ==================== 操作日志索引 ====================

CREATE INDEX IF NOT EXISTS idx_operation_logs_time ON "OperationLogs" ("OperatedAt" DESC);
CREATE INDEX IF NOT EXISTS idx_operation_logs_entity ON "OperationLogs" ("EntityType", "EntityId");
CREATE INDEX IF NOT EXISTS idx_operation_logs_operator ON "OperationLogs" ("OperatorId", "OperatedAt" DESC);

-- ==================== 订单状态历史索引 ====================

CREATE INDEX IF NOT EXISTS idx_order_status_histories_order ON "OrderStatusHistories" ("OrderId", "CreatedAt" DESC);

-- ==================== 配送表索引 ====================

CREATE INDEX IF NOT EXISTS idx_delivery_persons_branch ON "DeliveryPersons" ("BranchId", "Status");
CREATE INDEX IF NOT EXISTS idx_delivery_persons_active ON "DeliveryPersons" ("Status") WHERE "Status" = 0;

-- ==================== 员工表索引 ====================

CREATE INDEX IF NOT EXISTS idx_employees_branch ON "Employees" ("BranchId", "Status");
CREATE INDEX IF NOT EXISTS idx_employees_no ON "Employees" ("EmployeeNo");

-- ============================================================
-- 性能查询建议
-- ============================================================

-- 1. 启用 PostgreSQL 查询计划分析
-- ALTER DATABASE pro SET auto_explain.log_min_duration = '200ms';
-- ALTER DATABASE pro SET auto_explain.log_analyze = true;

-- 2. 建议配置连接池
-- 在 appsettings.json 的 ConnectionStrings 添加：
-- "Pooling=true;Minimum Pool Size=5;Maximum Pool Size=50"

-- 3. 定期维护
-- VACUUM ANALYZE;  -- 每天执行
-- REINDEX TABLE "Orders";  -- 索引碎片化严重时

-- 4. 监控慢查询
-- SELECT query, calls, mean_exec_time, total_exec_time 
-- FROM pg_stat_statements 
-- ORDER BY mean_exec_time DESC LIMIT 20;
