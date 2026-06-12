# PRO系统管理员配置说明

> **更新日期：** 2026年6月10日  
> **适用版本：** PRO 订单与客户管理系统

---

## 订单草稿有效期

### 配置入口
桌面客户端：系统设置 → 应用设置 → 订单草稿 → 草稿有效期（分钟）

### 配置键
`LocalSettings.SettingKey = OrderDraftExpireMinutes`

### 默认与回退规则
- 未配置：默认 30 分钟
- 配置为空、非数字、小于等于 0：自动回退 30 分钟
- 兼容旧配置键：`DraftExpireMinutes`

### 验证 SQL
```sql
SELECT "SettingKey", "SettingValue"
FROM "LocalSettings"
WHERE "SettingKey" IN ('OrderDraftExpireMinutes', 'DraftExpireMinutes');
```

---

## 订单号唯一性

订单号由 `IOrderNumberService` 统一生成，格式保持兼容：`DyyyyMMdd{branchCode}{sequence}`。

保护机制：
- 订单编辑界面只做预览，不直接拼接复杂订单号。
- 订单创建服务在保存前检查唯一性。
- 数据库唯一索引 `IX_Orders_OrderNo` 作为最终保护。
- 发生唯一约束冲突时进行有限重试，重试失败后返回明确错误。

验证 SQL：
```sql
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'Orders' AND indexname = 'IX_Orders_OrderNo';
```

---

## 库存并发控制

库存扣减由订单服务和库存服务统一处理。产品表使用 `RowVersion` 乐观并发字段。

处理规则：
- 创建正式订单前校验库存是否足够。
- 库存扣减、订单保存、库存变动日志写入在同一事务内完成。
- 发生并发冲突时回滚订单保存和库存日志，提示用户重新检查库存。
- 取消非草稿订单时恢复库存并写入 `OrderCancel` 日志。

排查建议：
- 查看库存变动日志确认 `BeforeQuantity`、`ChangeQuantity`、`AfterQuantity` 是否连续。
- 若出现库存不足提示，先刷新产品库存再重新提交订单。

---

## 批量订单操作

批量分配、批量确认草稿、批量状态修改统一走订单服务。

管理员需要关注：
- 所有批量状态变更均经过订单状态机校验。
- 合法订单继续处理，非法订单跳过。
- 操作结果会显示成功数量、失败数量、失败订单号和失败原因。
- 已完成、已取消等终态订单不会被强行覆盖状态。

---

## 全局搜索

全局搜索支持中文精确匹配、中文包含匹配和拼音首字母匹配。

示例：
- 输入 `张三`：优先返回精确匹配
- 输入 `张`：返回中文包含匹配
- 输入 `zs`：返回"张三"等拼音首字母匹配结果

当前拼音首字母采用内置常用汉字映射，适合常见客户、员工和产品名。极少见汉字如搜索不到，可继续使用中文名称、手机号、订单号或产品 SKU 搜索。
