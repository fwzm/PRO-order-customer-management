# PRO 订单与客户管理系统

> 面向多分公司企业的 Windows 桌面管理系统，围绕客户、订单、库存、配送、收款、应收账款和企业微信协同，帮助业务团队把日常经营流程标准化、可追溯、可统计。

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Release](https://img.shields.io/badge/release-v2.0.0-blue)](https://github.com/fwzm/PRO-order-customer-management/releases/tag/v2.0.0)

## 适合谁使用

PRO 适合需要统一管理订单、客户、库存和应收款的中小型贸易、批发、配送、区域经营团队。典型角色包括：

| 角色 | 主要关注点 |
|------|------------|
| 业务员 | 快速新建订单、查询客户、查看历史订单和拜访记录 |
| 配送主管 | 分配订单、查看配送员负载、跟踪配送状态 |
| 财务人员 | 登记收款、核销应收、查看账龄和结算异常 |
| 分公司管理员 | 管理本公司客户、员工、订单、库存和报表 |
| 总部管理员 | 管理组织架构、权限、系统设置、审计日志和跨分公司数据 |
| 开发/运维人员 | 二次开发 WebApi、部署 PostgreSQL、维护更新和备份 |

## 你可以用它做什么

| 模块 | 能力 |
|------|------|
| 工作台 | 今日待办、业务告警、快捷入口、最近订单、关键指标 |
| 客户管理 | 客户档案、客户查重、客户合并、标签、拜访记录、企微绑定 |
| 订单管理 | 草稿、模板、复制订单、状态流转、批量分配、批量确认、操作追踪 |
| 产品库存 | 产品/SKU、客户价格、库存扣减、库存日志、低库存预警、盘点 |
| 配送管理 | 配送员状态、手动分配、自动分配、配送失败重分配 |
| 收款应收 | 收款登记、应收核销、账龄分析、逾期提醒、结算异常处理 |
| 报表导出 | Excel/PDF 导出、导出历史、在线基础趋势图 |
| 系统管理 | 员工、部门、角色权限、分公司隔离、系统配置、操作日志 |
| 企业微信 | 配置向导、通讯录/客户同步、拜访数据同步、回调配置 |
| WebApi | RESTful API、JWT 认证、细粒度权限、Swagger、审计与监控 |

## 下载即用

### Windows 电脑端

业务电脑优先使用 GitHub Release 的 Windows 压缩包：

1. 打开 [v2.0.0 Release](https://github.com/fwzm/PRO-order-customer-management/releases/tag/v2.0.0)。
2. 下载 `PRO-desktop-v2.0.0-win-x64.zip`。
3. 解压后运行 `app\PRO.exe`。
4. 首次使用前配置 PostgreSQL 连接串，或连接已部署好的公司数据库。

压缩包是自包含发布包，业务电脑无需安装 .NET Runtime。数据库仍需要提前准备；多台电脑连接同一个 PostgreSQL 数据库后，客户、订单、库存和应收数据会互通。

### 手机端

手机端当前以 PWA 形式提供，适合 Android、iPhone、iPad 和微信/浏览器访问：

1. 部署 `PRO.WebApi`。
2. 部署 `src/PRO.Admin.Web` 构建产物，或下载 Release 中的 `PRO-mobile-pwa-v2.0.0.zip` 后部署到 Web 服务器。
3. 手机浏览器访问部署地址。
4. 在浏览器菜单中选择 `添加到主屏幕`，即可像手机应用一样打开。

手机端不是原生 APK 或 App Store 应用；它通过 WebApi 与桌面端共享同一套业务数据。后续如果需要原生 App，可基于现有 WebApi 开发 .NET MAUI 或 Flutter 客户端。

## 快速开始

### 方式一：本地源码运行

适合开发、测试或第一次体验。

```powershell
git clone https://github.com/fwzm/PRO-order-customer-management.git
cd PRO-order-customer-management

dotnet restore PRO.sln
dotnet build PRO.sln -c Release
dotnet run --project src/PRO.Desktop -c Release
```

桌面端是 WPF 应用，仅支持 Windows。项目目标框架为 .NET 8，建议安装 .NET 8 SDK 或更高版本。

### 方式二：Docker 启动 PostgreSQL 和 WebApi

适合需要同时体验 API 的场景。

```powershell
docker compose up -d
dotnet run --project src/PRO.Desktop -c Release
```

`docker-compose.yml` 默认启动：

| 服务 | 地址 |
|------|------|
| PostgreSQL | `localhost:5432` |
| WebApi | `http://localhost:8080` |

默认 Docker 连接参数仅用于本地体验，生产环境请替换为强密码和独立密钥。

### 方式三：本地生成 Windows 交付包

适合给业务电脑安装使用。

```powershell
.\publish.bat
```

执行完成后会生成：

| 路径 | 内容 |
|------|------|
| `publish\PRO.exe` | 桌面客户端主程序 |
| `artifacts\PRO-delivery-*.zip` | 可分发压缩包 |

### 方式四：构建手机端 PWA

```powershell
cd src/PRO.Admin.Web
npm ci
npm run build
```

构建产物位于 `src/PRO.Admin.Web/dist/`。生产环境建议与 WebApi 同域部署，或通过 Nginx/IIS 反向代理让 `/api` 和 `/health` 转发到 WebApi。

## 首次使用

### 1. 配置数据库

桌面端读取：

```text
src/PRO.Desktop/appsettings.json
```

关键配置示例：

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=pro_db;Username=pro_user;Password=pro_password;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=20"
  },
  "WeChat": {
    "Enabled": false
  },
  "TencentMap": {
    "ApiKey": ""
  }
}
```

也可以使用环境变量覆盖配置，生产环境推荐这种方式：

```powershell
$env:PRO_ConnectionStrings__PostgreSQL="Host=localhost;Port=5432;Database=pro_db;Username=pro_user;Password=你的密码"
$env:PRO_TencentMap__ApiKey="你的腾讯地图Key"
```

### 2. 登录系统

初始化数据包含以下演示账号，首次登录会要求修改默认密码：

| 角色 | 工号 | 默认密码 | 权限范围 |
|------|------|----------|----------|
| 总部管理员 | `ADMIN` | `admin123` | 全部功能、全部分公司 |
| 北京管理员 | `BJ001` | `admin123` | 北京分公司 |
| 北京员工 | `BJ002` | `admin123` | 北京分公司个人业务 |
| 上海管理员 | `SH001` | `admin123` | 上海分公司 |
| 上海员工 | `SH002` | `admin123` | 上海分公司个人业务 |

生产环境上线前请完成：

- 修改所有默认密码。
- 设置强数据库密码。
- 使用环境变量配置真实连接串、JWT Key、企业微信 Secret 和地图 Key。
- 按实际组织录入分公司、部门、员工和角色权限。

## 常用操作说明

### 新建订单

1. 打开 `订单管理`。
2. 点击 `新建订单`。
3. 选择客户；如果客户不存在，先在客户管理中新建。
4. 添加产品、数量、价格和配送地址。
5. 保存为草稿，确认无误后提交为待分配订单。
6. 分配配送员，配送完成后进入收款或结算流程。

订单状态遵循固定流转规则：

```text
草稿 -> 待分配 -> 已分配 -> 配送中 -> 已完成
            \        \          \
             取消     退回待分配  配送失败 -> 重新分配/取消
```

系统会阻止非法跳转，例如已取消订单不能重新确认，已完成订单不能再次分配。

### 批量处理订单

1. 在订单列表勾选多条订单。
2. 选择批量操作，例如批量确认草稿、批量分配、批量改状态。
3. 系统逐条校验状态流转规则。
4. 操作完成后查看成功/失败明细，失败项会显示订单号和原因。

这种设计适合真实业务场景：部分订单可处理时继续执行，异常订单单独反馈，不会因为一条失败阻塞全部操作。

### 管理客户

1. 在 `客户管理` 新增客户，填写名称、电话、地址、标签和所属分公司。
2. 打开客户档案查看订单、应收款、拜访记录和风险提示。
3. 使用全局搜索快速定位客户，支持中文、手机号和拼音首字母。
4. 发现重复客户时，使用客户合并功能迁移订单和财务数据。

### 管理库存

1. 在 `产品管理` 维护 SKU、规格、参考价格和安全库存。
2. 新建订单时系统会校验库存。
3. 保存订单后库存自动扣减，并记录库存变动日志。
4. 库存不足时系统会提示，避免超卖和负库存。
5. 管理员可通过盘点、调拨或手动调整修正库存。

### 登记收款和核销应收

1. 进入 `收款管理` 或订单详情。
2. 选择客户、订单和收款方式。
3. 输入收款金额和凭证备注。
4. 保存后系统自动更新订单收款状态。
5. 在 `应收账款` 页面查看未收、部分收款、逾期和账龄分析。

### 使用工作台

工作台适合每天打开后先看：

- 今日订单、待分配、配送中、已完成。
- 超期应收、待结算、待回访。
- 配送员满载、同步失败、库存预警。
- 新建订单、客户查询、自动分配、导出配送单等快捷入口。

### 查看操作日志

管理员可在 `系统设置 -> 操作日志` 查看关键操作：

- 订单创建、删除、取消、状态变更。
- 批量分配、自动分配。
- 客户创建、合并、删除。
- 结算、收款、系统配置变更。

日志支持按时间、操作人、模块、关键词筛选，用于追溯问题和内部审计。

### 企业微信配置

1. 在企业微信管理后台创建自建应用。
2. 获取 `CorpId`、`AgentId`、`Secret`。
3. 在系统设置中打开企业微信配置向导。
4. 按向导完成应用信息、回调地址、Token、通讯录同步配置。
5. 测试通过后再启用同步。

未配置企业微信前，请保持：

```json
{
  "WeChat": {
    "Enabled": false
  }
}
```

## 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+N` | 新建订单 |
| `Ctrl+Shift+N` | 新建客户 |
| `Ctrl+S` | 保存当前表单 |
| `Ctrl+F` | 全局搜索 |
| `F5` | 刷新列表 |
| `Ctrl+E` | 导出 |
| `Esc` | 关闭弹窗或取消当前操作 |

具体可用快捷键以当前页面按钮和命令为准。

## 系统配置

### 常用配置项

| 配置 | 推荐方式 | 说明 |
|------|----------|------|
| 数据库连接串 | 环境变量 | `PRO_ConnectionStrings__PostgreSQL` 或 `PRO_ConnectionStrings__DefaultConnection` |
| JWT Key | 环境变量 | `PRO_Jwt__Key`，至少 32 字节 |
| 企业微信 Secret | 环境变量/系统配置 | `PRO_WeChat__CorpSecret` |
| 腾讯地图 Key | 环境变量 | `PRO_TencentMap__ApiKey` |
| 草稿有效期 | 系统设置 | `OrderDraftExpireMinutes`，默认 30 分钟 |
| 密码有效期 | 系统设置 | 默认 90 天 |

### 配置优先级

1. 环境变量。
2. `appsettings.json`。
3. 数据库中的系统设置。
4. 代码默认值。

不要把真实数据库密码、JWT Key、企业微信 Secret 或地图 Key 提交到 GitHub。

## 开发者指南

### 解决方案结构

```text
PRO-order-customer-management/
├── PRO.sln
├── src/
│   ├── PRO.Domain/          # 领域实体、枚举、状态规则
│   ├── PRO.Application/     # DTO、接口、应用契约
│   ├── PRO.Infrastructure/  # EF Core、服务实现、迁移、外部集成
│   ├── PRO.Desktop/         # WPF 桌面端
│   ├── PRO.WebApi/          # ASP.NET Core Web API
│   └── PRO.Admin.Web/       # 管理端前端资源
├── tests/
│   └── PRO.WebApi.Tests/    # WebApi 和服务层测试
├── docs/                    # 用户、部署、API、验收文档
├── scripts/                 # 打包和数据库脚本
└── .github/                 # CI 和 Issue 模板
```

### 本地验证

```powershell
dotnet restore PRO.sln
dotnet build PRO.sln -c Release --nologo
dotnet test PRO.sln -c Release --no-build --nologo
dotnet format PRO.sln --verify-no-changes --no-restore --verbosity minimal
```

### 数据库迁移

```powershell
$env:PRO_ConnectionStrings__PostgreSQL="Host=localhost;Port=5432;Database=pro_db;Username=pro_user;Password=pro_password"
dotnet ef migrations add <MigrationName> --project src/PRO.Infrastructure --startup-project src/PRO.WebApi --context ProDbContext
dotnet ef database update --project src/PRO.Infrastructure --startup-project src/PRO.WebApi --context ProDbContext
```

更多说明见 [数据库迁移说明](docs/database-migration.md)。

### WebApi

WebApi 提供 JWT 认证、权限策略、分公司数据隔离、审计日志和 Swagger。

```powershell
dotnet run --project src/PRO.WebApi -c Release
```

启动后访问：

```text
http://localhost:8080/swagger
```

如果本地端口不同，以控制台输出为准。

### 手机端联调

```powershell
cd src/PRO.Admin.Web
npm ci
npm run dev -- --host 0.0.0.0
```

手机与电脑在同一局域网时，可访问电脑 IP 对应的 Vite 地址。正式部署时请使用 HTTPS，否则部分浏览器不会允许 PWA 安装和后台缓存能力。

## 文档索引

| 文档 | 用途 |
|------|------|
| [用户手册](docs/user-manual.md) | 面向业务用户的详细操作说明 |
| [管理员配置说明](docs/admin-configuration.md) | 系统设置、业务规则、权限和配置项 |
| [部署文档](docs/deployment.md) | WebApi、Desktop、Docker、IIS、Windows 服务部署 |
| [移动端 PWA 部署说明](docs/mobile-pwa.md) | 手机端安装、部署、互通和限制说明 |
| [原生移动端实施方案](docs/native-mobile-app-plan.md) | APK/App Store、Capacitor、原生能力桥接、审核和离线限制 |
| [API 文档](docs/api-documentation.md) | 控制器、认证、请求响应和错误码 |
| [数据库迁移说明](docs/database-migration.md) | EF Core 迁移、SQL 脚本、回滚 |
| [冒烟测试](docs/smoke-test.md) | 发布前快速验证清单 |
| [UAT 清单](docs/uat-checklist-v3.md) | 用户验收测试步骤 |
| [发布包清单](docs/release-package-checklist.md) | 交付包检查项 |
| [安全策略](SECURITY.md) | 漏洞反馈和密钥处理要求 |
| [贡献指南](CONTRIBUTING.md) | 分支、提交、PR 和代码规范 |

## 常见问题

### 启动后提示数据库连接失败

检查 PostgreSQL 是否启动、连接串是否正确、数据库和用户是否存在。Docker 体验环境可先执行：

```powershell
docker compose ps
docker compose logs postgres
```

### 登录后要求修改密码

这是预期行为。系统检测到默认密码或密码过期时，会要求立即修改。

### 订单保存失败怎么办

优先检查：

- 客户是否存在且状态正常。
- 产品库存是否足够。
- 订单状态是否允许当前操作。
- 数据库连接是否正常。

错误提示会尽量给出业务原因和处理建议。

### 为什么批量操作有成功也有失败

系统会逐条校验订单状态和权限。合法订单继续执行，非法订单跳过并列出失败原因，避免一条异常数据阻塞整批操作。

### 如何避免敏感配置泄露

生产环境不要修改并提交 `appsettings.json` 中的真实密码或密钥。请使用环境变量或本地不入库的配置文件保存敏感信息。

### Release 没有安装包怎么办

Release 会优先附带 Windows 桌面端压缩包。如果后续某个版本没有安装包，也可以在本地运行：

```powershell
.\publish.bat
```

生成的压缩包会出现在 `artifacts/` 目录。

## 当前状态

- 当前版本：`v2.0.0`
- 默认分支：`main`
- CI：GitHub Actions 自动执行 restore/build/test
- 构建目标：.NET 8
- 许可证：MIT

## 许可证

本项目使用 [MIT License](LICENSE)。
