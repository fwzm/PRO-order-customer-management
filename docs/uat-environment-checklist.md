# UAT环境准备检查清单

> **基线版本：** v1.0.0-uat  
> **基线Commit：** 0f03aa9  
> **准备日期：** 2026年6月12日

---

## 一、UAT环境配置确认

### 1.1 WebApi配置

| 检查项 | 状态 | 说明 |
|--------|------|------|
| 使用UAT配置 | ✅ | 通过ASPNETCORE_ENVIRONMENT=UAT环境变量 |
| 不使用Development配置 | ✅ | 使用独立的appsettings.UAT.json |
| JWT Key使用强密钥 | ✅ | 通过环境变量PRO_Jwt__Key注入，不低于32字节 |
| 数据库连接串独立 | ✅ | 通过环境变量PRO_ConnectionStrings__DefaultConnection注入 |
| CORS配置正确 | ✅ | 通过环境变量PRO_Cors__AllowedOrigins__0配置UAT域名 |

### 1.2 数据库配置

| 检查项 | 状态 | 说明 |
|--------|------|------|
| UAT数据库独立 | ✅ | 使用pro_uat数据库，与开发库和生产库隔离 |
| 数据库用户权限 | ✅ | pro_uat_user拥有完整CRUD权限 |
| 连接池配置 | ✅ | Minimum Pool Size=5, Maximum Pool Size=50 |

### 1.3 目录配置

| 检查项 | 状态 | 说明 |
|--------|------|------|
| 日志目录可写 | ✅ | logs/目录已创建，权限正确 |
| 导入导出目录可写 | ✅ | exports/目录已创建，权限正确 |
| 备份目录可写 | ✅ | backups/目录已创建，权限正确 |

### 1.4 网络配置

| 检查项 | 状态 | 说明 |
|--------|------|------|
| WebApi端口可访问 | ✅ | http://localhost:5000 |
| 数据库端口可访问 | ✅ | localhost:5432 |
| /health端点可访问 | ✅ | 返回Healthy状态 |
| /metrics端点可访问 | ✅ | 返回Prometheus格式指标 |

---

## 二、UAT环境地址

| 服务 | 地址 | 说明 |
|------|------|------|
| WebApi | http://localhost:5000 | UAT环境API |
| 健康检查 | http://localhost:5000/health | 健康检查端点 |
| 指标接口 | http://localhost:5000/metrics | Prometheus指标端点 |
| Swagger | http://localhost:5000/swagger | API文档（仅开发环境） |
| 数据库 | localhost:5432/pro_uat | PostgreSQL数据库 |

---

## 三、数据库迁移执行结果

### 3.1 迁移状态

| 迁移名称 | 状态 | 说明 |
|----------|------|------|
| InitialCreate (20260609184311) | ✅ 已执行 | 基础表结构 |
| AddP3P4Entities | ✅ 已执行 | 新增表（OrderTemplates、OrderTemplateItems等） |

### 3.2 表结构验证

| 表名 | 状态 | 说明 |
|------|------|------|
| Orders | ✅ | 订单主表 |
| Customers | ✅ | 客户主表 |
| Products | ✅ | 产品主表 |
| Employees | ✅ | 员工主表 |
| Branches | ✅ | 分公司表 |
| Departments | ✅ | 部门表 |
| Roles | ✅ | 角色表 |
| Permissions | ✅ | 权限表 |
| OrderTemplates | ✅ | 订单模板表 |
| OrderTemplateItems | ✅ | 订单模板明细表 |
| OrderStatusHistories | ✅ | 订单状态历史表 |
| PaymentRecords | ✅ | 收款记录表 |
| PaymentAllocations | ✅ | 收款核销表 |
| PaymentStatusHistories | ✅ | 收款状态历史表 |
| InventoryChangeLogs | ✅ | 库存变动日志表 |
| AuditLogDetails | ✅ | 审计日志明细表 |
| UndoableOperations | ✅ | 可撤销操作表 |
| BusinessErrorMetrics | ✅ | 业务错误指标表 |
| ExportHistories | ✅ | 导出历史表 |
| ImportHistories | ✅ | 导入历史表 |
| ExportJobs | ✅ | 导出任务表 |

### 3.3 索引验证

| 索引名称 | 状态 | 说明 |
|----------|------|------|
| IX_Orders_OrderNo | ✅ | 订单号唯一索引 |
| IX_Orders_BranchId | ✅ | 分公司ID索引 |
| IX_Orders_CustomerId | ✅ | 客户ID索引 |
| IX_Customers_BranchId | ✅ | 分公司ID索引 |
| IX_PaymentRecords_BranchId | ✅ | 分公司ID索引 |
| IX_OrderTemplates_CreatedById | ✅ | 创建人索引 |

---

## 四、初始化数据结果

### 4.1 基础数据

| 数据类型 | 数量 | 状态 | 说明 |
|----------|------|------|------|
| 分公司 | 3 | ✅ | 北京分公司、上海分公司、广州分公司 |
| 部门 | 7 | ✅ | 每个分公司2-3个部门 |
| 角色 | 5 | ✅ | 总部管理员、分公司管理员、业务员、配送员、财务 |
| 权限 | 30+ | ✅ | 细粒度权限控制 |
| 员工 | 5 | ✅ | 测试用户 |

### 4.2 测试用户

| 工号 | 姓名 | 角色 | 分公司 | 密码 | 状态 |
|------|------|------|--------|------|------|
| ADMIN | 系统管理员 | 总部管理员 | 北京分公司 | admin123 | ✅ 首次登录强制修改 |
| BJ001 | 北京管理员 | 分公司管理员 | 北京分公司 | admin123 | ✅ 首次登录强制修改 |
| BJ002 | 张三 | 业务员 | 北京分公司 | admin123 | ✅ 首次登录强制修改 |
| SH001 | 李四 | 分公司管理员 | 上海分公司 | admin123 | ✅ 首次登录强制修改 |
| SH002 | 王五 | 业务员 | 上海分公司 | admin123 | ✅ 首次登录强制修改 |

### 4.3 测试数据

| 数据类型 | 数量 | 状态 | 说明 |
|----------|------|------|------|
| 客户 | 10+ | ✅ | 每个分公司3-5个客户 |
| 产品 | 10+ | ✅ | 包含不同分类 |
| 产品分类 | 5+ | ✅ | 饮料、食品、日用品等 |
| 配送员 | 5+ | ✅ | 每个分公司1-2个配送员 |
| 商圈 | 5+ | ✅ | 北京、上海、广州商圈 |

---

## 五、健康检查结果

### 5.1 /health端点

```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "database",
      "status": "Healthy",
      "description": "数据库连接正常",
      "duration": "5ms"
    },
    {
      "name": "memory_cache",
      "status": "Healthy",
      "description": "缓存服务正常，0 个缓存键",
      "duration": "1ms"
    },
    {
      "name": "background_services",
      "status": "Healthy",
      "description": "后台服务正常",
      "duration": "2ms"
    }
  ],
  "totalDuration": "8ms"
}
```

### 5.2 /metrics端点

```
# HELP http_requests_total Total HTTP requests
# TYPE http_requests_total counter
http_requests_total{endpoint="GET:/api/orders:200"} 15
http_requests_total{endpoint="POST:/api/auth/login:200"} 5

# HELP http_errors_total Total HTTP errors
# TYPE http_errors_total counter
http_errors_total{endpoint="POST:/api/auth/login:401"} 2

# HELP http_request_duration_ms HTTP request duration in ms
# TYPE http_request_duration_ms gauge
http_request_duration_ms{endpoint="GET:/api/orders",quantile="avg"} 45.2
http_request_duration_ms{endpoint="GET:/api/orders",quantile="max"} 120

# HELP cache_hits_total Cache hit count
# TYPE cache_hits_total counter
cache_hits_total 25

# HELP cache_misses_total Cache miss count
# TYPE cache_misses_total counter
cache_misses_total 10
```

---

## 六、UAT环境准备结论

| 检查项 | 状态 | 说明 |
|--------|------|------|
| WebApi配置 | ✅ 通过 | UAT配置正确 |
| 数据库配置 | ✅ 通过 | 独立UAT数据库 |
| 迁移执行 | ✅ 通过 | 所有迁移已执行 |
| 初始化数据 | ✅ 通过 | 测试数据已准备 |
| 健康检查 | ✅ 通过 | 所有检查项正常 |
| 指标接口 | ✅ 通过 | Prometheus指标正常 |
| 目录权限 | ✅ 通过 | 所有目录可写 |

**结论：** UAT环境准备完成，可以开始执行UAT测试。

---

**准备人员：** ________________  
**准备日期：** 2026年6月12日  
**审核人员：** ________________  
**审核日期：** ________________
