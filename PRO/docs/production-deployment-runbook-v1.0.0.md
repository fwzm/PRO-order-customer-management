# PRO 企业管理系统 — 生产部署执行卡 v1.0.0

> **文档编号：** PRO-DEPLOY-RUNBOOK-v1.0.0  
> **生成日期：** 2026-06-12 00:48  
> **适用版本：** v1.0.0 (Commit: 679218f)  
> **执行环境：** Production (ASPNETCORE_ENVIRONMENT=Production)

---

## 一、版本与基线

| 项目 | 值 |
|------|-----|
| **版本号** | v1.0.0 |
| **Commit** | `679218f` |
| **Tag** | `v1.0.0` (annotated) |
| **前序Tag** | `v1.0.0-uat` → `3e34837` |
| **与UAT差异** | CacheServiceTests编译警告修复 + UAT执行报告 + 生产配置 + /metrics IP白名单 |
| **构建** | 0 错误 0 警告 (6/6 项目) |
| **测试** | 255/255 通过 |
| **配置文件** | `appsettings.Production.json` (新增) |

---

## 二、必填环境变量

> **严禁将以下值写入任何文件或提交Git！仅通过服务器环境变量或密钥管理系统注入。**

| 变量名 | 用途 | 格式/示例 | 优先级 |
|--------|------|-----------|--------|
| `ASPNETCORE_ENVIRONMENT` | 环境标识 | `Production` | 系统级 |
| `PRO_ConnectionStrings__DefaultConnection` | 生产数据库连接 | `Host=<IP>;Port=5432;Database=pro_prod;Username=<USER>;Password=<PWD>;Pooling=true;Min Pool Size=5;Max Pool Size=50` | 🔴 必填 |
| `PRO_Jwt__Key` | JWT 签名密钥 | `openssl rand -base64 48` 生成 (≥32字节) | 🔴 必填 |
| `PRO_Cors__AllowedOrigins__0` | Web前端域名 | `https://pro.example.com` | 🔴 必填 |
| `PRO_Cors__AllowedOrigins__1` | 第二个域名 (可选) | `https://pro-admin.example.com` | 🟡 按需 |
| `PRO_WeChat__CorpId` | 企业微信 CorpId | `<企业微信CorpId>` | 🟡 按需 |
| `PRO_WeChat__CorpSecret` | 企业微信 Secret | `<企业微信CorpSecret>` | 🟡 按需 |

### JWT Key 生成方法

```bash
# Linux / macOS / WSL
openssl rand -base64 48

# Windows PowerShell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))

# Python
python -c "import secrets; print(secrets.token_urlsafe(48))"
```

---

## 三、部署前检查清单

```
□ 生产数据库已可用 (PostgreSQL 12+)
□ 生产数据库连接串已验证可连接
□ JWT Key 已生成 (≥32字节随机值)
□ CORS 域名已确认 (无通配符/无localhost)
□ 部署目标服务器已就绪 (Windows Server 2016+ / Linux)
□ .NET 8.0 Runtime 已安装 (或使用自包含发布)
□ 部署窗口已通知 (建议凌晨 2:00-4:00)
□ 上版本备份已准备 (如有)
□ 回滚方案已确认
```

---

## 四、数据库备份与迁移

### 4.1 备份生产库

```bash
# PostgreSQL 备份 (在数据库服务器上执行)
pg_dump -h <PROD_DB_HOST> -U <PROD_DB_USER> -d pro_prod \
  -F c -v -f /backups/pro_prod_pre_v1.0.0_$(date +%Y%m%d_%H%M%S).dump

# 验证备份
pg_restore -l /backups/pro_prod_pre_v1.0.0_*.dump | head -20
```

### 4.2 执行 EF Core 迁移

```bash
# 切换到发布包目录
cd <DEPLOY_DIR>\PRO.WebApi

# 设置环境变量
set ASPNETCORE_ENVIRONMENT=Production
set PRO_ConnectionStrings__DefaultConnection=<生产库连接串>

# 执行迁移 (InitialCreate — 首次部署)
dotnet ef database update --context ProDbContext --project ..\PRO.Infrastructure

# 预期: 创建 50+ 张表，无错误输出
```

### 4.3 执行手动 SQL 脚本 (幂等)

```bash
# Phase 2: 表结构补充 (PaymentAllocations, OrderStatusHistories 等)
psql -h <PROD_DB_HOST> -U <PROD_DB_USER> -d pro_prod -f scripts/phase2_migration.sql

# Phase 3: 性能索引 (50+ 索引)
psql -h <PROD_DB_HOST> -U <PROD_DB_USER> -d pro_prod -f scripts/phase3_indexes.sql

# 维护
psql -h <PROD_DB_HOST> -U <PROD_DB_USER> -d pro_prod -c "VACUUM ANALYZE;"
```

### 4.4 验证数据表

```bash
psql -h <PROD_DB_HOST> -U <PROD_DB_USER> -d pro_prod -c "\dt"
# 预期: 显示 50+ 张表 (Branches, Employees, Customers, Orders, Products, Payments, ...)
```

---

## 五、WebApi 部署

### 5.1 停止旧版服务 (如有)

```bash
# Windows 服务
sc stop PROWebApi

# 或直接停止进程
taskkill /f /im PRO.WebApi.exe 2>nul
```

### 5.2 备份旧版 (如有)

```bash
robocopy C:\PRO\WebApi C:\PRO\WebApi_backup_%DATE:~0,4%%DATE:~5,2%%DATE:~8,2%_%TIME:~0,2%%TIME:~3,2% /MIR /XD logs exports backups
```

### 5.3 部署新版文件

```bash
# 创建目标目录
mkdir C:\PRO\WebApi 2>nul

# 复制发布产物 (排除 logs/exports/backups)
robocopy <发布包>\app\PRO.WebApi C:\PRO\WebApi /MIR /XD logs exports backups

# 确保目录权限
mkdir C:\PRO\WebApi\logs 2>nul
mkdir C:\PRO\WebApi\exports 2>nul
mkdir C:\PRO\WebApi\backups 2>nul
```

### 5.4 配置环境变量

```powershell
# Windows (系统环境变量)
[System.Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Production', 'Machine')
[System.Environment]::SetEnvironmentVariable('PRO_ConnectionStrings__DefaultConnection', '<生产库连接串>', 'Machine')
[System.Environment]::SetEnvironmentVariable('PRO_Jwt__Key', '<JWT密钥>', 'Machine')
[System.Environment]::SetEnvironmentVariable('PRO_Cors__AllowedOrigins__0', 'https://pro.example.com', 'Machine')
```

### 5.5 安装/启动 Windows 服务

```bash
# 安装服务
sc create PROWebApi binPath="C:\PRO\WebApi\PRO.WebApi.exe" start=auto
sc description PROWebApi "PRO 订单与客户管理系统 WebApi"

# 启动服务
sc start PROWebApi
```

---

## 六、启动验证

### 6.1 健康检查

```bash
curl http://localhost:5000/health
```

**预期响应：**
```json
{
  "status": "Healthy",
  "checks": [
    {"name": "database", "status": "Healthy", "duration": "XXms"},
    {"name": "memory_cache", "status": "Healthy", "duration": "XXms"},
    {"name": "background_services", "status": "Healthy", "duration": "XXms"}
  ],
  "totalDuration": "XXms"
}
```

### 6.2 Prometheus 指标 (仅内网可访问)

```bash
# 内网机器
curl http://<内网IP>:5000/metrics

# 公网访问应返回 403 Forbidden
curl http://<公网IP>:5000/metrics
```

### 6.3 Swagger 禁用确认

```bash
curl http://localhost:5000/swagger
# 预期: 404 Not Found (Production 环境不启用 Swagger)
```

### 6.4 CORS 验证

```bash
# 从允许的域名访问
curl -H "Origin: https://pro.example.com" -I http://localhost:5000/api/customers
# 预期: Access-Control-Allow-Origin: https://pro.example.com

# 从非允许的域名访问
curl -H "Origin: https://evil.com" -I http://localhost:5000/api/customers
# 预期: 无 Access-Control-Allow-Origin 头
```

### 6.5 登录验证

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"employeeNo":"<真实工号>","password":"<密码>"}'
# 预期: 200 OK, 返回 JWT Token
```

---

## 七、Desktop 客户端部署

```bash
# 创建安装目录
mkdir "C:\Program Files\PRO" 2>nul

# 复制客户端文件
robocopy <发布包>\app\PRO.Desktop "C:\Program Files\PRO" /MIR

# 创建桌面快捷方式 (PowerShell)
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:USERPROFILE\Desktop\PRO.lnk")
$Shortcut.TargetPath = "C:\Program Files\PRO\PRO.exe"
$Shortcut.WorkingDirectory = "C:\Program Files\PRO"
$Shortcut.Save()
```

**客户端配置：** 修改 `C:\Program Files\PRO\appsettings.json` 中 API 地址指向生产 WebApi。

---

## 八、上线冒烟测试 (15分钟)

| # | 测试项 | 操作 | 预期 | ✓ |
|---|--------|------|------|---|
| 1 | 健康检查 | GET /health | 200, Healthy | |
| 2 | 指标端点 | GET /metrics (内网) | 200, Prometheus格式 | |
| 3 | 指标端点(公网) | GET /metrics (公网) | 403 Forbidden | |
| 4 | Swagger禁用 | GET /swagger | 404 | |
| 5 | 响应压缩 | GET /api/orders -H "Accept-Encoding: gzip" | Content-Encoding: gzip | |
| 6 | 真实登录 | 工号+密码 | 200, JWT Token | |
| 7 | Dashboard | 登录后首页 | 数据显示 | |
| 8 | 新建客户 | 填写→保存 | 201, 自动编号 | |
| 9 | 新建订单 | 选客户→选产品→保存 | 201, 订单号 | |
| 10 | 状态流转 | 草稿→待分配→已分配 | 状态变更+库存扣减 | |
| 11 | 分公司隔离 | 跨公司访问 | 403/数据过滤 | |
| 12 | 拼音搜索 | "zs" 搜索 "张三" | 返回匹配 | |
| 13 | 批量操作 | 批量确认 | 进度+成功计数 | |
| 14 | 数据导出 | 导出订单 | 异步任务创建 | |
| 15 | 收款登记 | 收款→保存 | 201, 应收更新 | |
| 16 | 日志脱敏 | 查看log文件 | 密码/手机号脱敏 | |
| 17 | DB写入 | 创建测试客户后查库 | 记录存在 | |
| 18 | 缓存命中 | 两次查Dashboard | 第二次更快 | |

---

## 九、回滚条件与步骤

### 9.1 P0 立即回滚条件

```
□ 系统无法启动
□ 大面积无法登录 (>50%)
□ 核心流程不可用 (订单/客户/收款)
□ 分公司数据隔离失效
□ 权限绕过
□ 数据丢失
□ 金额计算错误
□ 明文凭据泄露
```

### 9.2 回滚步骤

```bash
# 1. 停止服务
sc stop PROWebApi

# 2. 恢复备份
robocopy C:\PRO\WebApi_backup_<timestamp> C:\PRO\WebApi /MIR /XD logs exports backups

# 3. 回滚数据库 (如执行了迁移)
dotnet ef database update 0 --context ProDbContext

# 4. 启动服务
sc start PROWebApi

# 5. 验证
curl http://localhost:5000/health
```

---

## 十、首日监控指标

| 指标 | 来源 | 告警阈值 | 首小时 | 首日 |
|------|------|----------|--------|------|
| API 错误率 | /metrics | > 5% | | |
| 慢请求 | /metrics | > 10/min | | |
| 登录失败 | /metrics | > 20/min | | |
| 权限拒绝 | /metrics | > 10/min | | |
| 隔离拒绝 | /metrics | 异常增长 | | |
| DB 错误 | /metrics | > 5/min | | |
| 缓存命中率 | /metrics | < 50% | | |
| 内存 | /metrics | > 1GB | | |
| 线程数 | /metrics | > 200 | | |

---

## 十一、签字确认

| 角色 | 姓名 | 签字 | 日期 |
|------|------|------|------|
| 部署执行人 | | | |
| 技术负责人 | | | |
| 业务方确认 | | | |

---

## 附录：发布包内容清单

```
PRO-delivery-v1.0.0/
├── app/PRO.WebApi/          # WebApi Release 产物
│   ├── PRO.WebApi.dll
│   ├── PRO.WebApi.exe
│   ├── appsettings.json
│   ├── appsettings.Production.json
│   └── ...
├── app/PRO.Desktop/         # Desktop Release 产物
│   ├── PRO.exe
│   └── ...
├── scripts/                 # 迁移/部署脚本
│   ├── phase2_migration.sql
│   ├── phase3_indexes.sql
│   └── package-delivery.ps1
├── docs/                    # 文档
│   ├── deployment.md
│   ├── rollback-plan.md
│   ├── monitoring-verification.md
│   ├── uat-checklist-v3.md
│   ├── uat-execution-report-v1.0.0.md
│   ├── go-live-report-v1.0.0.md
│   └── production-deployment-runbook-v1.0.0.md (本文档)
└── 启动说明.txt
```

---

**本执行卡确认后即执行生产部署。**
