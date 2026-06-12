# 数据库迁移文档

## 迁移名称
AddP3P4Entities

## 迁移内容摘要

### 新增表

#### 1. OrderTemplates (订单模板)
```sql
CREATE TABLE "OrderTemplates" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(200) NOT NULL,
    "CustomerId" INTEGER NOT NULL,
    "DeliveryAddress" VARCHAR(500),
    "Remark" VARCHAR(1000),
    "CreatedById" INTEGER NOT NULL,
    "IsPublic" BOOLEAN NOT NULL DEFAULT FALSE,
    "UseCount" INTEGER NOT NULL DEFAULT 0,
    "LastUsedAt" TIMESTAMP,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    
    CONSTRAINT "FK_OrderTemplates_Customers_CustomerId" 
        FOREIGN KEY ("CustomerId") REFERENCES "Customers"("Id"),
    CONSTRAINT "FK_OrderTemplates_Employees_CreatedById" 
        FOREIGN KEY ("CreatedById") REFERENCES "Employees"("Id")
);

CREATE INDEX "IX_OrderTemplates_CustomerId" ON "OrderTemplates"("CustomerId");
CREATE INDEX "IX_OrderTemplates_CreatedById" ON "OrderTemplates"("CreatedById");
```

#### 2. OrderTemplateItems (订单模板明细)
```sql
CREATE TABLE "OrderTemplateItems" (
    "Id" SERIAL PRIMARY KEY,
    "OrderTemplateId" INTEGER NOT NULL,
    "ProductId" INTEGER NOT NULL,
    "Quantity" INTEGER NOT NULL,
    "UnitPrice" DECIMAL(18,2) NOT NULL,
    
    CONSTRAINT "FK_OrderTemplateItems_OrderTemplates_OrderTemplateId" 
        FOREIGN KEY ("OrderTemplateId") REFERENCES "OrderTemplates"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_OrderTemplateItems_Products_ProductId" 
        FOREIGN KEY ("ProductId") REFERENCES "Products"("Id")
);

CREATE INDEX "IX_OrderTemplateItems_OrderTemplateId" ON "OrderTemplateItems"("OrderTemplateId");
CREATE INDEX "IX_OrderTemplateItems_ProductId" ON "OrderTemplateItems"("ProductId");
```

#### 3. InventoryChangeLogs (库存变动日志)
```sql
CREATE TABLE "InventoryChangeLogs" (
    "Id" SERIAL PRIMARY KEY,
    "ProductId" INTEGER NOT NULL,
    "ProductName" VARCHAR(200) NOT NULL,
    "ProductSku" VARCHAR(50),
    "BeforeQuantity" INTEGER NOT NULL,
    "AfterQuantity" INTEGER NOT NULL,
    "ChangeQuantity" INTEGER NOT NULL,
    "ChangeType" VARCHAR(50) NOT NULL,
    "ChangeReason" VARCHAR(500) NOT NULL,
    "RelatedOrderId" INTEGER,
    "RelatedOrderNo" VARCHAR(50),
    "OperatorId" INTEGER NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    
    CONSTRAINT "FK_InventoryChangeLogs_Products_ProductId" 
        FOREIGN KEY ("ProductId") REFERENCES "Products"("Id"),
    CONSTRAINT "FK_InventoryChangeLogs_Orders_RelatedOrderId" 
        FOREIGN KEY ("RelatedOrderId") REFERENCES "Orders"("Id") ON DELETE SET NULL
);

CREATE INDEX "IX_InventoryChangeLogs_ProductId" ON "InventoryChangeLogs"("ProductId");
CREATE INDEX "IX_InventoryChangeLogs_ChangeType" ON "InventoryChangeLogs"("ChangeType");
CREATE INDEX "IX_InventoryChangeLogs_CreatedAt" ON "InventoryChangeLogs"("CreatedAt");
CREATE INDEX "IX_InventoryChangeLogs_RelatedOrderId" ON "InventoryChangeLogs"("RelatedOrderId");
```

#### 4. UndoableOperations (可撤销操作)
```sql
CREATE TABLE "UndoableOperations" (
    "Id" SERIAL PRIMARY KEY,
    "OperationType" VARCHAR(50) NOT NULL,
    "EntityType" VARCHAR(50) NOT NULL,
    "EntityId" INTEGER NOT NULL,
    "EntityName" VARCHAR(200) NOT NULL,
    "Description" VARCHAR(500) NOT NULL,
    "SnapshotData" TEXT,
    "OperatorId" INTEGER NOT NULL,
    "OperatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "UndoDeadline" TIMESTAMP NOT NULL,
    "IsUndone" BOOLEAN NOT NULL DEFAULT FALSE,
    "UndoneAt" TIMESTAMP,
    
    CONSTRAINT "FK_UndoableOperations_Employees_OperatorId" 
        FOREIGN KEY ("OperatorId") REFERENCES "Employees"("Id")
);

CREATE INDEX "IX_UndoableOperations_OperatorId" ON "UndoableOperations"("OperatorId");
CREATE INDEX "IX_UndoableOperations_UndoDeadline" ON "UndoableOperations"("UndoDeadline");
CREATE INDEX "IX_UndoableOperations_EntityType_EntityId" ON "UndoableOperations"("EntityType", "EntityId");
```

#### 5. OrderStatusHistories (订单状态历史)
```sql
CREATE TABLE "OrderStatusHistories" (
    "Id" SERIAL PRIMARY KEY,
    "OrderId" INTEGER NOT NULL,
    "FromStatus" INTEGER NOT NULL,
    "ToStatus" INTEGER NOT NULL,
    "ChangedById" INTEGER NOT NULL,
    "ChangedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "Reason" VARCHAR(500),
    
    CONSTRAINT "FK_OrderStatusHistories_Orders_OrderId" 
        FOREIGN KEY ("OrderId") REFERENCES "Orders"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_OrderStatusHistories_Employees_ChangedById" 
        FOREIGN KEY ("ChangedById") REFERENCES "Employees"("Id")
);

CREATE INDEX "IX_OrderStatusHistories_OrderId" ON "OrderStatusHistories"("OrderId");
CREATE INDEX "IX_OrderStatusHistories_ChangedAt" ON "OrderStatusHistories"("ChangedAt");
```

#### 6. PaymentStatusHistories (支付状态历史)
```sql
CREATE TABLE "PaymentStatusHistories" (
    "Id" SERIAL PRIMARY KEY,
    "OrderId" INTEGER NOT NULL,
    "FromStatus" INTEGER NOT NULL,
    "ToStatus" INTEGER NOT NULL,
    "ChangedById" INTEGER NOT NULL,
    "ChangedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "Reason" VARCHAR(500),
    
    CONSTRAINT "FK_PaymentStatusHistories_Orders_OrderId" 
        FOREIGN KEY ("OrderId") REFERENCES "Orders"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_PaymentStatusHistories_Employees_ChangedById" 
        FOREIGN KEY ("ChangedById") REFERENCES "Employees"("Id")
);

CREATE INDEX "IX_PaymentStatusHistories_OrderId" ON "PaymentStatusHistories"("OrderId");
CREATE INDEX "IX_PaymentStatusHistories_ChangedAt" ON "PaymentStatusHistories"("ChangedAt");
```

#### 7. AuditLogDetails (审计日志详情)
```sql
CREATE TABLE "AuditLogDetails" (
    "Id" SERIAL PRIMARY KEY,
    "OperationLogId" INTEGER NOT NULL,
    "FieldName" VARCHAR(100) NOT NULL,
    "OldValue" TEXT,
    "NewValue" TEXT,
    
    CONSTRAINT "FK_AuditLogDetails_OperationLogs_OperationLogId" 
        FOREIGN KEY ("OperationLogId") REFERENCES "OperationLogs"("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_AuditLogDetails_OperationLogId" ON "AuditLogDetails"("OperationLogId");
```

## 迁移风险点

### 1. 数据类型兼容性
- **金额字段**: 使用 `DECIMAL(18,2)` 精度，与现有表一致
- **时间字段**: 使用 `TIMESTAMP WITHOUT TIME ZONE`，与现有表一致
- **枚举字段**: 存储为整数，与现有表一致

### 2. 外键约束
- 所有外键都引用现有表的主键
- 使用 `ON DELETE CASCADE` 或 `ON DELETE SET NULL` 处理级联删除
- 不会破坏现有数据完整性

### 3. 索引策略
- 为常用查询字段创建索引
- 复合索引用于多条件查询
- 不会显著影响写入性能

### 4. 默认值
- 时间字段默认值为 `NOW()`
- 布尔字段默认值为 `FALSE`
- 整数字段默认值为 `0`

## 回滚方案

### 自动回滚
```bash
dotnet ef migrations remove --context ProDbContext
```

### 手动回滚
```sql
DROP TABLE IF EXISTS "AuditLogDetails";
DROP TABLE IF EXISTS "PaymentStatusHistories";
DROP TABLE IF EXISTS "OrderStatusHistories";
DROP TABLE IF EXISTS "UndoableOperations";
DROP TABLE IF EXISTS "InventoryChangeLogs";
DROP TABLE IF EXISTS "OrderTemplateItems";
DROP TABLE IF EXISTS "OrderTemplates";
```

## 执行迁移

### 开发环境
```bash
export PRO_ConnectionStrings__PostgreSQL="Host=localhost;Port=5432;Database=pro_dev;Username=postgres;Password=your_password"
dotnet ef database update --context ProDbContext
```

### 生产环境
```bash
# 1. 备份数据库
pg_dump -h your_host -U your_user -d pro > backup_before_migration.sql

# 2. 执行迁移
export PRO_ConnectionStrings__PostgreSQL="Host=prod_host;Port=5432;Database=pro;Username=prod_user;Password=prod_password"
dotnet ef database update --context ProDbContext

# 3. 验证迁移
psql -h prod_host -U prod_user -d pro -c "\dt"
```

## 2026-06-10 工程加固说明

本轮修复涉及订单状态流转、草稿有效期、库存并发、订单号生成和拼音首字母搜索。检查当前模型后，没有新增数据库结构变更：

- `Products.RowVersion` 已存在并在 `ProDbContext` 中配置为 `IsRowVersion()`，用于库存扣减的乐观并发控制。
- `Orders.OrderNo` 已存在唯一索引 `IX_Orders_OrderNo`，订单号服务以该索引作为最终唯一性保护并支持有限重试。
- `LocalSettings` 已存在唯一键配置表，本轮新增默认配置键 `OrderDraftExpireMinutes`，由数据库初始化器在新库和既有库启动时补齐。
- `InventoryChangeLogs` 已存在，用于订单创建、取消、库存调整等库存变动审计。
- `ExportJobs` 已存在，用于导出进度和导出历史。

因此未生成 `AddProductRowVersion`、`AddOrderDraftExpireSetting`、`AddOrderNumberSequence`、`AddExportTaskHistory` 等新增迁移。生产环境升级时仍需执行一次应用启动初始化，确保 `LocalSettings.OrderDraftExpireMinutes=30` 被补齐。

### 升级验证 SQL
```sql
SELECT "SettingKey", "SettingValue"
FROM "LocalSettings"
WHERE "SettingKey" IN ('OrderDraftExpireMinutes', 'DraftExpireMinutes');

SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'Orders' AND indexname = 'IX_Orders_OrderNo';
```

## 验证清单

- [ ] 所有新表已创建
- [ ] 外键关系正确
- [ ] 索引已创建
- [ ] 默认值正确
- [ ] `OrderDraftExpireMinutes` 默认配置已存在，值为正整数
- [ ] `IX_Orders_OrderNo` 唯一索引存在
- [ ] `Products.RowVersion` 并发字段存在
- [ ] 现有数据未受影响
- [ ] 应用程序可正常启动
- [ ] 核心功能可正常使用
