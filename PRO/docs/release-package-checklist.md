# PRO系统发布包检查清单

> **版本：** v1.0  
> **更新日期：** 2026年6月11日

---

## 一、发布包内容

### WebApi 发布文件
- [ ] `PRO.WebApi.dll`
- [ ] `PRO.WebApi.deps.json`
- [ ] `PRO.WebApi.runtimeconfig.json`
- [ ] `appsettings.json`
- [ ] `appsettings.Production.json`（模板）
- [ ] `web.config`（如使用IIS）
- [ ] `Dockerfile`
- [ ] `docker-compose.yml`

### Desktop 客户端发布文件
- [ ] `PRO.Desktop.exe`
- [ ] `PRO.Desktop.dll`
- [ ] `PRO.Desktop.deps.json`
- [ ] `PRO.Desktop.runtimeconfig.json`
- [ ] 依赖项DLL
- [ ] `appsettings.json`

### 配置文件
- [ ] `appsettings.Production.json` 模板
- [ ] 环境变量说明
- [ ] 连接字符串配置说明

### 数据库迁移脚本
- [ ] EF Core Migration 文件
- [ ] `phase2_migration.sql`
- [ ] `phase3_indexes.sql`
- [ ] 初始化数据脚本

### 启动脚本
- [ ] Windows 服务安装脚本
- [ ] Docker 启动脚本
- [ ] 健康检查脚本

### 目录说明
- [ ] `logs/` 日志目录
- [ ] `exports/` 导出目录
- [ ] `backups/` 备份目录

### 文档
- [ ] `README.md`
- [ ] `版本号和更新日志`
- [ ] `用户手册`
- [ ] `管理员手册`
- [ ] `API文档`
- [ ] `部署文档`
- [ ] `回滚文档`
- [ ] `监控指标说明`

---

## 二、版本号管理

### 版本号来源
- **当前版本：** v3.0.0
- **版本号格式：** 主版本.次版本.修订号
- **版本号位置：**
  - `PRO.Desktop.csproj` → `<Version>3.0.0</Version>`
  - `README.md` → 版本历史章节
  - 发布包文件名 → `PRO-v3.0.0.zip`

### 版本号规则
- **主版本：** 重大功能变更或架构调整
- **次版本：** 新增功能或重要修复
- **修订号：** Bug修复或小改动

---

## 三、发布包生成

### 使用 publish.bat
```bash
# 生成发布包
publish.bat

# 输出位置
artifacts/PRO-delivery-{timestamp}/
artifacts/PRO-delivery-{timestamp}.zip
```

### 手动生成
```bash
# 清理
rd /s /q publish

# 构建
dotnet build PRO.sln -c Release

# 发布 WebApi
dotnet publish src/PRO.WebApi -c Release -r win-x64 --self-contained -o publish/webapi

# 发布 Desktop
dotnet publish src/PRO.Desktop -c Release -r win-x64 --self-contained -o publish/desktop

# 复制文档
xcopy docs publish\docs /E /I

# 创建ZIP
powershell Compress-Archive -Path publish/* -DestinationPath PRO-v3.0.0.zip
```

---

## 四、发布包验证

### 文件完整性
- [ ] 所有必要文件存在
- [ ] 文件大小合理
- [ ] 无损坏文件

### 配置文件
- [ ] `appsettings.json` 格式正确
- [ ] 连接字符串为占位符
- [ ] JWT Key 为占位符
- [ ] CORS 配置正确

### 依赖项
- [ ] 所有DLL存在
- [ ] 版本号一致
- [ ] 无冲突依赖

---

## 五、发布包清单

| 文件/目录 | 说明 | 必需 |
|----------|------|------|
| `app/` | 应用程序文件 | ✅ |
| `app/PRO.WebApi.dll` | WebApi主程序 | ✅ |
| `app/PRO.Desktop.exe` | Desktop主程序 | ✅ |
| `app/appsettings.json` | 配置文件 | ✅ |
| `docs/` | 文档目录 | ✅ |
| `docs/README.md` | 项目说明 | ✅ |
| `docs/user-manual.md` | 用户手册 | ✅ |
| `docs/api-documentation.md` | API文档 | ✅ |
| `docs/database-migration.md` | 数据库迁移文档 | ✅ |
| `docs/rollback-plan.md` | 回滚方案 | ✅ |
| `docs/smoke-test.md` | 冒烟测试脚本 | ✅ |
| `scripts/` | 脚本目录 | ✅ |
| `scripts/phase2_migration.sql` | 迁移脚本 | ✅ |
| `scripts/phase3_indexes.sql` | 索引脚本 | ✅ |
| `启动说明.txt` | 快速启动说明 | ✅ |

---

## 六、发布包大小检查

| 组件 | 预期大小 | 实际大小 |
|------|---------|---------|
| WebApi | ~50-100 MB | ___ |
| Desktop | ~50-100 MB | ___ |
| 文档 | ~1-5 MB | ___ |
| 总计 | ~100-200 MB | ___ |

---

## 七、发布包签名（可选）

```bash
# 使用 signtool 签名
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com PRO.Desktop.exe
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com PRO.WebApi.dll
```

---

## 八、发布包分发

### 分发方式
- [ ] 内部文件服务器
- [ ] 共享目录
- [ ] 邮件发送
- [ ] USB拷贝

### 分发记录
| 接收人 | 分发时间 | 方式 | 确认 |
|--------|---------|------|------|
| | | | |
| | | | |
