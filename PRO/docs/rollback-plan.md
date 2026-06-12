# PRO系统回滚方案

> **版本：** v1.0  
> **更新日期：** 2026年6月11日  
> **适用场景：** 发布后发现问题需要回滚

---

## 回滚决策标准

### 立即回滚（P0）
- 系统无法启动
- 登录功能完全失效
- 数据库连接失败
- 数据丢失风险

### 评估后回滚（P1）
- 核心功能异常（订单创建、客户管理）
- 性能严重下降（响应时间 > 10秒）
- 数据隔离失效

### 不回滚，热修复（P2）
- 非核心功能异常
- UI显示问题
- 边缘场景bug

---

## 一、WebApi 回滚

### 步骤
1. **停止当前服务**
   ```bash
   # Windows 服务
   sc stop PROWebApi
   
   # Docker
   docker-compose down webapi
   ```

2. **备份当前版本**
   ```bash
   # 备份当前发布文件
   cp -r /opt/pro/webapi /opt/pro/webapi_backup_$(date +%Y%m%d%H%M%S)
   ```

3. **恢复上一版本**
   ```bash
   # 从备份恢复
   cp -r /opt/pro/webapi_previous /opt/pro/webapi
   ```

4. **启动服务**
   ```bash
   # Windows 服务
   sc start PROWebApi
   
   # Docker
   docker-compose up -d webapi
   ```

5. **验证回滚**
   ```bash
   curl http://localhost:5000/health
   ```

### 验证清单
- [ ] 服务启动成功
- [ ] 健康检查返回正常
- [ ] 登录功能正常
- [ ] 核心功能正常

---

## 二、Desktop 客户端回滚

### 步骤
1. **通知用户退出客户端**

2. **备份当前版本**
   ```bash
   # 备份当前安装目录
   cp -r "C:\Program Files\PRO" "C:\Program Files\PRO_backup_$(date +%Y%m%d%H%M%S)"
   ```

3. **恢复上一版本**
   ```bash
   # 从备份恢复
   cp -r "C:\Program Files\PRO_previous" "C:\Program Files\PRO"
   ```

4. **通知用户重新启动客户端**

### 验证清单
- [ ] 客户端启动成功
- [ ] 登录功能正常
- [ ] 核心功能正常

---

## 三、数据库迁移回滚

### 自动回滚（推荐）
```bash
# 回滚到指定迁移
dotnet ef database update PreviousMigrationName --context ProDbContext

# 或回滚最后一个迁移
dotnet ef migrations remove --context ProDbContext
dotnet ef database update --context ProDbContext
```

### 手动回滚
```sql
-- 回滚新增表
DROP TABLE IF EXISTS "AuditLogDetails";
DROP TABLE IF EXISTS "PaymentStatusHistories";
DROP TABLE IF EXISTS "OrderStatusHistories";
DROP TABLE IF EXISTS "UndoableOperations";
DROP TABLE IF EXISTS "InventoryChangeLogs";
DROP TABLE IF EXISTS "OrderTemplateItems";
DROP TABLE IF EXISTS "OrderTemplates";

-- 回滚新增字段（谨慎使用）
-- ALTER TABLE "TableName" DROP COLUMN "ColumnName";
```

### 数据库回滚风险说明

| 变更类型 | 风险等级 | 说明 |
|---------|---------|------|
| 新增表 | 低 | 可安全删除，不影响现有数据 |
| 新增字段 | 低 | 可安全删除，不影响现有数据 |
| 修改字段 | 中 | 可能导致数据丢失，需谨慎 |
| 删除字段 | 高 | 不可逆，需从备份恢复 |
| 删除表 | 高 | 不可逆，需从备份恢复 |

### 验证清单
- [ ] 迁移回滚成功
- [ ] 现有数据完整
- [ ] 应用程序可正常启动
- [ ] 核心功能正常

---

## 四、配置文件回滚

### 步骤
1. **备份当前配置**
   ```bash
   cp appsettings.json appsettings.json.backup_$(date +%Y%m%d%H%M%S)
   cp appsettings.Production.json appsettings.Production.json.backup_$(date +%Y%m%d%H%M%S)
   ```

2. **恢复上一版本配置**
   ```bash
   cp appsettings.json.previous appsettings.json
   cp appsettings.Production.json.previous appsettings.Production.json
   ```

3. **重启服务**

### 验证清单
- [ ] 配置文件恢复成功
- [ ] 服务启动成功
- [ ] 功能正常

---

## 五、日志和导出目录保留

### 说明
回滚时**不要**删除以下目录：
- `logs/` - 日志文件
- `exports/` - 导出文件
- `backups/` - 备份文件

### 操作
```bash
# 只保留，不删除
ls -la logs/
ls -la exports/
ls -la backups/
```

---

## 六、用户数据保护

### 保护措施
1. **回滚前备份数据库**
   ```bash
   pg_dump -h localhost -U postgres -d pro > backup_before_rollback.sql
   ```

2. **回滚后验证数据**
   ```sql
   -- 检查核心表数据
   SELECT COUNT(*) FROM "Orders";
   SELECT COUNT(*) FROM "Customers";
   SELECT COUNT(*) FROM "Products";
   SELECT COUNT(*) FROM "Payments";
   ```

3. **不要删除用户数据**
   - 订单数据
   - 客户数据
   - 收款数据
   - 操作日志

---

## 七、回滚后验证步骤

### 快速验证（5分钟）
1. [ ] 服务启动成功
2. [ ] 健康检查返回正常
3. [ ] 登录功能正常
4. [ ] 订单列表加载正常
5. [ ] 客户列表加载正常

### 完整验证（15分钟）
1. [ ] 执行冒烟测试脚本
2. [ ] 检查日志无异常
3. [ ] 检查数据库连接正常
4. [ ] 检查缓存服务正常

---

## 八、回滚通知模板

```
【回滚通知】

尊敬的用户：

由于发现 [问题描述]，我们决定对系统进行回滚操作。

回滚时间：[开始时间] - [结束时间]
影响范围：[影响的功能]
数据影响：无数据丢失

回滚期间：
- 系统将暂时不可用
- 请勿进行重要操作
- 已保存的数据不受影响

回滚完成后：
- 系统将恢复到上一版本
- 所有数据完整保留
- 请重新登录系统

如有问题，请联系技术支持。

[技术团队]
```

---

## 九、回滚检查清单

### 回滚前
- [ ] 确认回滚决策
- [ ] 通知相关人员
- [ ] 备份当前版本
- [ ] 备份数据库
- [ ] 准备上一版本文件

### 回滚中
- [ ] 停止当前服务
- [ ] 恢复上一版本
- [ ] 恢复配置文件
- [ ] 回滚数据库迁移（如需要）
- [ ] 启动服务

### 回滚后
- [ ] 验证服务启动
- [ ] 验证健康检查
- [ ] 验证登录功能
- [ ] 验证核心功能
- [ ] 检查日志无异常
- [ ] 通知用户回滚完成

---

## 十、回滚记录

| 时间 | 操作 | 结果 | 操作人 |
|------|------|------|--------|
| | | | |
| | | | |
| | | | |

---

## 联系方式

- **技术负责人：** ________________
- **运维负责人：** ________________
- **紧急联系人：** ________________
