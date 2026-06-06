# PRO 订单与客户管理系统

PRO 是基于 .NET 8 WPF 的 Windows 原生桌面应用，用于多分公司客户、订单、产品、库存、物流、结算、工作计划和系统配置管理。

当前版本按修订版需求调整为 PostgreSQL / 自部署 Supabase 兼容架构：桌面端直接连接 PostgreSQL 数据库，不依赖独立后端服务。

## 核心能力

- 客户管理：新增、编辑、查询、详情、客户担当、客户层级、拜访与商机数据关联。
- 订单管理：订单创建、草稿、状态流转、收款状态、修改记录、客户与产品选择。
- 产品与库存：SKU、规格、参考价、平均售价、库存、仓库与盘点管理。
- 物流管理：配送员管理、订单分配、面向高德路径规划的 Excel 导出基础。
- 财务管理：应收账款、结算记录、结算 PDF 生成。
- 工作计划：排班、个人计划、草稿和变更标识。
- 系统设置：普通设置、组织架构、总部高级设置、企业微信配置、同步日志。
- 桌面体验：纯文字侧边栏、顶部标签页、关闭到托盘、日志记录和异常兜底。

## 技术架构

```text
PRO/
├── src/PRO.Domain          # 领域实体、枚举
├── src/PRO.Application     # DTO 与服务接口
├── src/PRO.Infrastructure  # PostgreSQL EF Core、加密、企业微信、初始化
└── src/PRO.Desktop         # WPF 界面、ViewModel、桌面服务
```

## 技术栈

| 组件 | 技术 |
|------|------|
| 运行时 | .NET 8 |
| UI | WPF |
| MVVM | CommunityToolkit.Mvvm |
| 数据库 | PostgreSQL / 自部署 Supabase PostgreSQL |
| ORM | Entity Framework Core + Npgsql |
| PDF | QuestPDF |
| Excel | ClosedXML |
| UI 组件 | MaterialDesignInXAML、HandyControl |
| 日志 | Serilog |
| 密码 | BCrypt.Net |

## 配置

桌面端启动时读取 `src/PRO.Desktop/appsettings.json` 中的连接串，也支持用环境变量覆盖，便于部署时避免把数据库密码写入项目文件。

推荐环境变量：

```powershell
$env:PRO_ConnectionStrings__PostgreSQL="Host=数据库地址;Port=5432;Database=pro;Username=用户名;Password=密码;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=20"
```

也可使用标准 .NET 配置名：

```powershell
$env:ConnectionStrings__PostgreSQL="Host=数据库地址;Port=5432;Database=pro;Username=用户名;Password=密码"
```

企业微信配置支持以下环境变量覆盖：

```powershell
$env:PRO_WeChat__CorpId="企业ID"
$env:PRO_WeChat__CorpSecret="应用Secret"
$env:PRO_WeChat__AgentId="AgentId"
$env:PRO_WeChat__Enabled="true"
```

## 构建与运行

```powershell
dotnet restore
dotnet build PRO.sln -c Release
dotnet run --project src/PRO.Desktop -c Release
```

## 发布

```powershell
dotnet publish src/PRO.Desktop -c Release -r win-x64 --self-contained true -o ./publish
```

或运行项目根目录的：

```powershell
.\publish.bat
```

## 默认登录

初始化空数据库时会创建默认账号：

| 角色 | 工号 | 密码 |
|------|------|------|
| 总部管理员 | ADMIN | admin123 |
| 分公司管理员 | BJ001 | admin123 |
| 普通员工 | BJ002 | admin123 |

首次上线后请立即修改默认密码。

## 验证清单

1. `dotnet build PRO.sln -c Release` 应为 0 警告、0 错误。
2. 启动桌面端后日志应显示 PostgreSQL 连接和数据库初始化完成。
3. 使用默认账号登录，检查客户、订单、产品、库存、结算、工作计划和系统设置页面可打开。
4. 新增或编辑数据后确认 PostgreSQL 中对应表有记录。
5. 关闭主窗口时确认托盘行为正常。

日志默认写入运行目录下的 `logs/pro-*.log`。
