# PRO 企业管理系统

> 面向多分公司企业的原生 Windows 桌面管理系统，整合客户、订单、库存、配送、财务、工作计划等核心业务流程，支持企业微信集成与 RESTful API 扩展。

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows_|_Android_|_iOS-0078D6?logo=dotnet)](https://dotnet.microsoft.com/)
[![MAUI](https://img.shields.io/badge/MAUI-8.0-blue?logo=dotnet)](https://dotnet.microsoft.com/apps/maui)
[![PWA](https://img.shields.io/badge/PWA-ready-5A0FC8?logo=pwa)](https://web.dev/progressive-web-apps/)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Build](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/fwzm/PRO-order-customer-management/actions)

---

## 目录

- [1. 项目简介](#1-项目简介)
- [2. 功能特性](#2-功能特性)
- [3. 环境要求与安装](#3-环境要求与安装)
- [4. 使用说明](#4-使用说明)
- [5. 项目结构](#5-项目结构)
- [6. 技术架构](#6-技术架构)
- [7. 配置说明](#7-配置说明)
- [8. 数据库迁移](#8-数据库迁移)
- [9. 构建与发布](#9-构建与发布)
- [10. 测试](#10-测试)
- [11. 贡献指南](#11-贡献指南)
- [12. 常见问题](#12-常见问题)
- [13. 版本历史](#13-版本历史)
- [14. 开源协议](#14-开源协议)

---

## 1. 项目简介

### 背景

在多分公司、多层级的中小型贸易或零售企业中，日常运营涉及客户拜访、订单配送、库存管理、收款结算等多个环节。传统的人工记录或单一 Excel 管理方式，在数据同步、权限控制、流程追溯方面存在严重瓶颈。

PRO 是为这类企业打造的**一站式原生桌面管理系统**。它以订单工作流为核心，串联客户、产品、库存、配送、财务等环节，帮助企业实现：

- **流程标准化**：订单从草稿到完成严格流转，减少人为错误
- **数据集中化**：所有数据统一存储，自动关联，消除信息孤岛
- **权限精细化**：总部/分公司/区域三级权限模型，按角色分配功能
- **操作可追溯**：完整的审计日志与操作撤销，满足内控要求

### 核心价值

| 维度 | 价值 |
|------|------|
| **效率提升** | 订单模板、批量操作、快捷键，日常操作提效 50%+ |
| **数据质量** | 自动检测重复客户、空数据、地址不规范，数据完整性 > 95% |
| **财务精准** | 收款登记 + 应收核销 + 账龄分析，账实一致 |
| **移动扩展** | 企业微信集成 + .NET MAUI 原生 App (Android/iOS) + PWA Web 端，全平台覆盖 |
| **安全合规** | 分层权限、密码策略、操作审计、敏感信息加密存储 |

---

## 2. 功能特性

### 客户管理

| 功能 | 说明 |
|------|------|
| 客户 CRUD | 新增、编辑、删除、查询，支持按名称/手机号/标签搜索 |
| 客户层级 | 大客户 → 细分客户二级结构，支持归属关系管理 |
| 客户档案 | 一屏展示完整客户画像：订单历史、应收款、拜访记录、风险提示 |
| 数据质量检测 | 自动检测重复客户、空号码、地址不规范、企微未绑定等问题 |
| 客户查重 | 录入时自动模糊匹配，提示疑似重复客户 |
| 客户合并 | 支持合并重复客户，订单与财务数据自动迁移 |
| 标签管理 | 自定义客户标签，支持多标签分类 |
| 企微绑定 | 绑定企业微信客户，同步外部联系人信息 |

### 订单管理

| 功能 | 说明 |
|------|------|
| 订单生命周期 | 草稿 → 待分配 → 已分配 → 配送中 → 已完成 / 已取消 |
| 状态流转 | 严格的状态机规则，禁非法跳转，取消必须填写原因 |
| 草稿管理 | 支持自动保存草稿（30 秒间隔），草稿 7 天自动过期 |
| 订单模板 | 保存常用订单为模板，一键从模板创建新订单 |
| 复制订单 | 一键复制历史订单为新草稿，减少重复录入 |
| 批量操作 | 批量分配配送员、批量修改状态 |
| 收款状态 | 未收款 / 部分收款 / 已收款 / 法律追收四级追踪 |
| 修改记录 | 订单每次修改留痕，支持查看变更历史 |
| 配送地址 | 支持文本地址 + 经纬度坐标，为路径规划提供数据基础 |

### 产品与库存

| 功能 | 说明 |
|------|------|
| 产品管理 | SKU、规格、参考价、平均售价、多图片支持 |
| 产品分类 | 树形分类结构，无限层级 |
| 客户专属价格 | 按客户设置不同的产品价格 |
| 阶梯价格 | 按购买数量阶梯定价 |
| 库存管理 | 实时库存数量，自动扣减与回增 |
| 仓库管理 | 多仓库支持，仓库间库存调拨 |
| 库存盘点 | 手动盘点，系统自动计算差异 |
| 库存变动日志 | 每次库存变动记录原因（订单/盘点/调拨），完整可追溯 |
| 低库存预警 | 低于安全库存阈值时自动预警 |

### 配送管理

| 功能 | 说明 |
|------|------|
| 配送员管理 | 配送员状态（在线/忙碌/离线）、服务区域、车辆信息 |
| 订单分配 | 手动分配订单给配送员 |
| 自动分配 | 基于服务区域和负载的智能分配算法 |
| 路径规划 | 对接高德地图 API，生成配送路径并导出 |

### 财务管理

| 功能 | 说明 |
|------|------|
| 结算管理 | 批量结算订单，生成结算单 |
| 收款登记 | 记录每笔收款（现金/微信/支付宝/银行转账） |
| 收款核销 | 指定收款对应订单，精准核销应收账款 |
| 应收账龄 | 30 天 / 60 天 / 90 天以上分级分析 |
| 结算异常标记 | 标记异常结算单并提供备注说明 |

### 工作计划

| 功能 | 说明 |
|------|------|
| 排班管理 | 创建排班模板，管理员工出勤 |
| 个人计划 | 员工每日/每周工作安排 |
| 计划草稿 | 支持草稿模式，发布前可多次修改 |

### 企业微信集成

| 功能 | 说明 |
|------|------|
| 企微客户同步 | 将企业微信外部联系人同步至系统客户 |
| 企微绑定 | 员工账号与企微账号绑定，实现扫码登录 |
| 拜访记录同步 | 企微拜访记录自动同步至系统 |
| 配置向导 | 引导式配置企业微信 CorpId / CorpSecret / AgentId |

### 系统管理

| 功能 | 说明 |
|------|------|
| 组织架构 | 总部 → 分公司 → 部门三级组织管理 |
| 员工管理 | 员工 CRUD、角色分配、密码策略、锁定保护 |
| 角色权限 | 四级角色（总部管理员/分公司管理员/区域管理员/员工）+ 自定义权限 |
| 数据隔离 | 分公司间数据完全隔离，员工只能查看所属分公司数据 |
| 操作日志 | 记录所有关键操作，支持按时间/操作人/模块筛选与导出 |
| 操作撤销 | 关键操作 5 分钟内可撤销 |
| 审计日志 | 订单创建、状态变更、分配、删除等自动审计 |
| 导出管理 | 异步批量导出 Excel/PDF，支持大数据量 |
| 数据健康 | 实时数据质量评分（客户/订单/产品三维度） |
| 系统备份 | 手动/定时备份至 PostgreSQL dump 文件 |

### 桌面体验

| 功能 | 说明 |
|------|------|
| 纯文本侧边栏 | 清晰的功能导航，折叠/展开支持 |
| 顶部标签页 | 多页面并行操作，不丢失上下文 |
| 全局搜索 | `Ctrl+F` 跨模块快速搜索客户/订单/产品 |
| 系统托盘 | 关闭窗口可选最小化到托盘 |
| 快捷键 | 覆盖新建、保存、搜索、导出等核心操作 |
| 自动保存 | 新建/编辑表单自动保存草稿 |
| 视图记忆 | 筛选条件和列宽自动保存，下次打开自动恢复 |
| 自适应布局 | 支持 1366×768 至 4K 分辨率 |

### 界面预览

> 📸 **提示**：以下为应用界面截图占位。请将实际截图放置于 `docs/screenshots/` 目录。

<details>
<summary><b>📷 点击查看界面截图</b></summary>

#### 主界面 - 工作台

![主界面](docs/screenshots/dashboard.png)

*工作台展示：今日待办、快捷入口、数据概览*

#### 订单管理

![订单列表](docs/screenshots/order-list.png)

*订单列表：支持筛选、排序、批量操作、状态流转*

#### 客户管理

![客户列表](docs/screenshots/customer-list.png)

*客户列表：客户层级、标签、搜索、数据质量检测*

#### 库存管理

![库存管理](docs/screenshots/inventory.png)

*库存管理：实时库存、变动日志、盘点、预警*

#### 应收款管理

![应收款](docs/screenshots/receivables.png)

*应收款：收款登记、核销、账龄分析*

#### 系统设置

![系统设置](docs/screenshots/settings.png)

*系统设置：组织架构、角色权限、数据脱敏、备份管理*

</details>

---

## 3. 环境要求与安装

### 必须环境

| 组件 | 版本要求 | 说明 |
|------|----------|------|
| 操作系统 | Windows 10 1809+ / Windows 11 | 桌面应用仅支持 Windows |
| .NET SDK | 9.0+ (或 8.0) | 项目目标 `net8.0`，兼容 SDK 8.0/9.0 |
| PostgreSQL | 13+ | 主数据库 |
| 推荐分辨率 | 1920×1080+ | 低于 1366×768 部分页面可能裁剪 |

### 可选组件

| 组件 | 说明 |
|------|------|
| Docker Desktop | 快速启动 PostgreSQL 和 WebApi |
| 企业微信管理后台 | 使用企微集成功能需配置 |
| 高德地图 API Key | 使用地图选点和路径规划功能需配置 |
| 腾讯地图 API Key | 备用地图服务 |

### 安装步骤

#### 方式一：快速启动（推荐使用 Docker）

```powershell
# 1. 克隆项目
git clone <repository-url>
cd PRO

# 2. 启动 PostgreSQL + WebApi（Docker）
docker compose up -d

# 3. 配置桌面端数据库连接
# 编辑 src/PRO.Desktop/appsettings.json 中的连接字符串

# 4. 运行桌面客户端
dotnet run --project src/PRO.Desktop
```

#### 方式二：手动安装

```powershell
# 1. 克隆项目
git clone <repository-url>
cd PRO

# 2. 确保 PostgreSQL 服务已启动，创建数据库
psql -U postgres -c "CREATE DATABASE pro;"

# 3. 配置数据库连接
# 编辑 src/PRO.Desktop/appsettings.json：
```

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=pro;Username=postgres;Password=你的密码;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=20"
  },
  "TencentMap": {
    "ApiKey": ""
  }
}
```

或使用环境变量：

```powershell
$env:PRO_ConnectionStrings__PostgreSQL="Host=localhost;Port=5432;Database=pro;Username=postgres;Password=你的密码"
$env:PRO_TencentMap__ApiKey="你的腾讯地图Key"
```

```powershell
# 4. 还原依赖
dotnet restore PRO.sln

# 5. 编译
dotnet build PRO.sln -c Release

# 6. 运行桌面应用
dotnet run --project src/PRO.Desktop -c Release
```

### 首次登录

系统预置三个测试账号，**首次登录强制修改密码**：

| 角色 | 工号 | 默认密码 | 权限范围 |
|------|------|----------|----------|
| 总部管理员 | `ADMIN` | `admin123` | 全部功能与数据 |
| 分公司管理员 | `BJ001` | `admin123` | 所属分公司数据 |
| 普通员工 | `BJ002` | `admin123` | 个人数据与基础功能 |

---

## 4. 使用说明

### 启动应用

```powershell
# 开发模式（含详细日志）
dotnet run --project src/PRO.Desktop

# 发布后直接运行
.\publish\PRO.exe
```

### 核心操作流程

#### 订单完整流程

```
新建订单 → 选择客户 → 选择产品 → 填写配送地址 → 保存草稿
   ↓
待分配 → 分配配送员 → 配送中 → 配送完成
   ↓            ↓            ↓
  取消        退回        配送失败 → 重新分配
```

**快捷操作：**
- `Ctrl+N` — 新建订单
- `Ctrl+Shift+N` — 新建客户
- `Ctrl+S` — 保存
- `Ctrl+D` — 复制所选订单
- `F5` — 刷新列表
- `Ctrl+E` — 导出

#### 收款与核销流程

```
创建订单 → 配送完成 → 生成应收款
                         ↓
                     收款登记 → 收款核销 → 应收清零
```

#### 数据质量检测

```
系统设置 → 数据质量 → 开始检测 → 查看报告 → 逐项修复
                                          → 合并重复客户
                                          → 补全空字段
```

### 关键配置

#### 配置文件位置

| 文件 | 路径 | 用途 |
|------|------|------|
| 桌面端配置 | `src/PRO.Desktop/appsettings.json` | 桌面应用主配置 |
| WebApi 配置 | `src/PRO.WebApi/appsettings.json` | WebApi 配置 |
| 环境变量覆盖 | 任意 | 所有配置项支持 `PRO_` 前缀环境变量覆盖 |

#### 配置项说明

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "数据库连接字符串"
  },
  "Jwt": {
    "Key": "JWT签名密钥（至少32字节）",
    "Issuer": "PRO-System",
    "ExpireHours": 24
  },
  "WeChat": {
    "CorpId": "企业微信企业ID",
    "CorpSecret": "企业微信应用Secret",
    "AgentId": "应用AgentId",
    "Enabled": false
  },
  "TencentMap": {
    "ApiKey": "腾讯地图API密钥"
  },
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
```

> **安全提示**：生产环境中请勿在 `appsettings.json` 中写入真实密钥，应使用环境变量或密钥管理服务。

### 日志

日志默认写入 `logs/pro-*.log`，保留最近 30 天。日志级别：
- `Information` — 正常业务流程
- `Warning` — 潜在问题
- `Error` — 业务异常或外部调用失败
- `Fatal` — 应用级致命错误

---

## 5. 项目结构

```
PRO/
├── PRO.sln                           # 解决方案文件
├── global.json                       # SDK 版本配置
├── docker-compose.yml                # Docker 编排（PostgreSQL + WebApi）
├── publish.bat                       # 一键发布脚本
├── README.md                         # 项目文档
│
├── docs/                             # 文档
│   ├── admin-configuration.md        # 管理员配置指南
│   ├── api-documentation.md          # API 接口文档
│   ├── database-migration.md         # 数据库迁移指南
│   ├── delivery-report.md            # 交付验证报告
│   ├── user-acceptance-test.md       # 用户验收测试用例
│   └── user-manual.md                # 用户操作手册
│
├── scripts/                          # 辅助脚本
│   ├── package-delivery.ps1          # 打包发布脚本
│   ├── phase2_migration.sql          # 数据迁移 SQL
│   └── phase3_indexes.sql            # 索引优化 SQL
│
├── src/
│   ├── PRO.Domain/                   # 领域层
│   │   ├── Entities/                 # 实体类（30+ 实体）
│   │   │   ├── BusinessEntities.cs   # 核心业务实体
│   │   │   ├── OrganizationEntities.cs # 组织架构实体
│   │   │   ├── SystemEntities.cs     # 系统管理实体
│   │   │   └── WorkPlanEntities.cs   # 工作计划实体
│   │   ├── Enums/                    # 枚举定义
│   │   └── Services/                 # 领域服务
│   │       └── OrderStatusManager.cs # 订单状态流转规则
│   │
│   ├── PRO.Application/              # 应用层（DTO 复用 — WPF/Mobile/PWA 共享）
│   │   ├── DTOs/                     # 数据传输对象（15+ 文件）
│   │   ├── Interfaces/               # 服务接口定义
│   │   │   ├── IServices.cs          # 核心服务接口
│   │   │   ├── IServicesExtended.cs  # 扩展服务接口
│   │   │   └── IDataQualityService.cs # 数据质量服务接口
│   │   └── DependencyInjection.cs    # 应用层 DI 注册
│   │
│   ├── PRO.Infrastructure/           # 基础设施层
│   │   ├── Data/
│   │   │   ├── ProDbContext.cs       # EF Core 数据库上下文
│   │   │   └── Migrations/           # EF Core 迁移文件
│   │   ├── Services/                 # 服务实现
│   │   │   ├── CustomerService.cs    # 客户服务
│   │   │   ├── OrderService.cs       # 订单服务
│   │   │   ├── DashboardService.cs   # 工作台聚合服务
│   │   │   ├── EmployeeService.cs    # 员工服务
│   │   │   └── WeChatService.cs      # 企业微信服务
│   │   ├── Repositories/             # 仓储模式实现
│   │   └── DependencyInjection.cs    # 基础设施层 DI 注册
│   │
│   ├── PRO.Desktop/                  # WPF 桌面应用（Windows）
│   │   ├── Views/                    # XAML 视图（30+ 页面）
│   │   │   ├── MainWindow.xaml       # 主窗口
│   │   │   ├── LoginWindow.xaml      # 登录窗口
│   │   │   ├── OrderListView.xaml    # 订单列表页
│   │   │   └── OrderEditWindow.xaml  # 订单编辑窗口
│   │   │   ├── CustomerEditWindow.xaml # 客户编辑窗口
│   │   │   ├── MapPickerWindow.xaml  # 地图选点窗口
│   │   │   └── ReportCenterView.xaml # 报表中心
│   │   ├── ViewModels/               # MVVM ViewModel
│   │   ├── Services/                 # 桌面端专属服务
│   │   ├── Converters/               # XAML 值转换器
│   │   ├── appsettings.json          # 桌面端配置
│   │   └── App.xaml                  # 应用入口
│   │
│   │
│   ├── PRO.Mobile/                   # .NET MAUI 手机 App (Android/iOS)
│   │   ├── Services/                 # 移动端专属服务
│   │   │   ├── ApiClient.cs          # HTTP 客户端 (JWT + 401 + 状态码)
│   │   │   ├── AuthServiceProxy.cs   # 登录/登出/改密/刷新 Token
│   │   │   ├── MobileServices.cs     # 工作台/客户/订单/产品/分公司
│   │   │   ├── PlatformServices.cs   # 网络检测/SecureStorage/Toast
│   │   │   └── Interfaces.cs         # 移动端全部服务接口
│   │   ├── Stores/                   # 状态管理
│   │   │   ├── TokenStore.cs         # Token 内存 + 持久化
│   │   │   └── UserStore.cs          # 当前用户信息
│   │   ├── ViewModels/               # MVVM ViewModel (8 个)
│   │   ├── Views/                    # XAML 页面 (8 个)
│   │   ├── Platforms/Android/         # Android 入口
│   │   ├── Platforms/iOS/             # iOS 入口
│   │   ├── MauiProgram.cs            # DI + HttpClient 配置
│   │   ├── App.xaml                  # 全局样式 + 转换器
│   │   ├── AppShell.xaml             # TabBar 导航
│   │   └── Converters.cs             # 值转换器
│   │
│   ├── PRO.Admin.Web/                # PWA Web 管理端 (Vue 3 + Element Plus)
│   │   ├── src/views/                # 15+ 业务页面
│   │   ├── src/api/                  # API 封装层
│   │   ├── src/layouts/              # 布局组件
│   │   └── vite.config.js            # 构建优化（按需加载/ECharts 动态导入）
│   │
│   └── PRO.WebApi/                   # RESTful API
│       ├── Controllers/              # API 控制器 (12+ 控制器)
│       │   ├── BaseApiController.cs  # 基类（统一 BranchForbidden 等公共方法）
│       │   ├── AuthController.cs     # 认证 (登录/登出/改密/刷新)
│       │   ├── DashboardController.cs # 移动端工作台 (GET /api/dashboard/workbench)
│       │   ├── UserController.cs     # 当前用户信息 (GET /api/user/profile)
│       │   └── ...
│       ├── Middleware/               # 中间件
│       ├── appsettings.json          # API 配置
│       └── Program.cs                # 启动配置
│
├── .github/workflows/                # CI/CD
│   └── ci.yml                        # 构建→测试→审计→发布（含 mobile-build）
│
└── tests/
    └── PRO.WebApi.Tests/             # API 集成测试 (255 用例)
        ├── BranchIsolationTests.cs   # 分公司数据隔离测试
        └── AuditTests.cs             # 审计日志测试
```

---

## 6. 技术架构

### 架构模式：Clean Architecture（整洁架构）

```
┌───────────────────────────────────────────────────┐
│  src/PRO.Desktop (WPF) │ PRO.Mobile (MAUI)        │  ← 表现层
│  src/PRO.Admin.Web (PWA/Vue)  │  src/PRO.WebApi   │
├───────────────────────────────────────────────────┤
│              src/PRO.Application                   │  ← 应用层（DTO 复用）
├───────────────────────────────────────────────────┤
│            src/PRO.Infrastructure                  │  ← 基础设施（EF Core）
├───────────────────────────────────────────────────┤
│     src/PRO.Domain                 │  ← 领域层（实体 / 枚举）
└────────────────────────────────────┘
```

**依赖方向**：表现层 → 应用层 → 基础设施层 → 领域层（内层不依赖外层）

### 技术栈

| 层级 | 技术选型 | 版本 |
|------|----------|------|
| **运行时** | .NET | 8.0 (SDK 9.0 兼容) |
| **桌面 UI** | WPF + XAML | .NET 8 |
| **MVVM** | CommunityToolkit.Mvvm | 8.2.2 |
| **DI 容器** | Microsoft.Extensions.DependencyInjection | 8.0 |
| **数据库** | PostgreSQL | 13+ |
| **ORM** | Entity Framework Core + Npgsql | 8.0 |
| **WebAPI** | ASP.NET Core | 8.0 |
| **认证** | JWT Bearer Token | — |
| **密码哈希** | BCrypt.Net-Next | — |
| **图表** | LiveChartsCore (SkiaSharp) | 2.0.0-rc2 |
| **UI 组件** | MaterialDesignInXAML + HandyControl | 4.9 / 3.4 |
| **PDF** | QuestPDF | 2024.3 |
| **Excel** | ClosedXML | 0.102.2 |
| **日志** | Serilog | 8.0 |
| **重试** | Polly | 8.0 |
| **测试** | xUnit + FluentAssertions | — |
| **Docker** | Docker Compose | — |

### 设计模式

| 模式 | 应用位置 | 说明 |
|------|----------|------|
| MVVM | 桌面应用 | View ↔ ViewModel 双向绑定 |
| 仓储模式 | 基础设施层 | 数据访问抽象 |
| 依赖注入 | 全局 | 基于 Microsoft DI 容器 |
| 工厂模式 | 基础设施层 | `IHttpClientFactory` HTTP 客户端管理 |
| 策略模式 | 基础设施层 | 订单分配算法 |
| 状态机 | 领域层 | 订单状态流转管理 |

---

## 7. 配置说明

### 配置优先级（从高到低）

1. 环境变量（`PRO_` 前缀，分隔符 `__` 映射嵌套）
2. `appsettings.json` 文件
3. 代码中的硬编码默认值

### 环境变量映射

```
JSON 路径                            → 环境变量
ConnectionStrings:PostgreSQL         → PRO_ConnectionStrings__PostgreSQL
Jwt:Key                              → PRO_Jwt__Key
WeChat:CorpId                        → PRO_WeChat__CorpId
TencentMap:ApiKey                    → PRO_TencentMap__ApiKey
Serilog:MinimumLevel                 → PRO_Serilog__MinimumLevel
```

### 企业微信配置示例

```json
{
  "WeChat": {
    "CorpId": "ww1234567890abcdef",
    "CorpSecret": "your-secret",
    "AgentId": "1000002",
    "Enabled": true
  }
}
```

获取方式：
1. 登录 [企业微信管理后台](https://work.weixin.qq.com/)
2. 在"我的企业 → 企业信息"获取 `CorpId`
3. 在"应用管理 → 自建应用"创建应用，获取 `AgentId` 和 `Secret`

### 地图服务配置

```json
{
  "TencentMap": {
    "ApiKey": "你的腾讯地图 Key"
  }
}
```

> 腾讯地图 API Key 可通过 [腾讯位置服务控制台](https://lbs.qq.com/) 申请。

---

## 8. 数据库迁移

### 生成新迁移

```powershell
cd src/PRO.Infrastructure
$env:PRO_ConnectionStrings__PostgreSQL="你的连接字符串"
dotnet ef migrations add <迁移名称> --context ProDbContext
```

### 执行迁移

```powershell
dotnet ef database update --context ProDbContext
```

### 回滚最近一次迁移

```powershell
dotnet ef migrations remove --context ProDbContext
```

### 生成 SQL 脚本

```powershell
dotnet ef migrations script --context ProDbContext -o migration.sql
```

**详细迁移文档：** [`docs/database-migration.md`](docs/database-migration.md)

---

## 9. 构建与发布

### 本地构建

```powershell
# 还原依赖
dotnet restore PRO.sln

# Debug 构建
dotnet build PRO.sln

# Release 构建
dotnet build PRO.sln -c Release
```

### 桌面端发布

```powershell
# 自包含发布（无需安装 .NET 运行时）
dotnet publish src/PRO.Desktop -c Release -r win-x64 --self-contained true -o ./publish

# 或使用一键脚本
.\publish.bat
```

### WebApi 发布

```powershell
dotnet publish src/PRO.WebApi -c Release -o ./publish-api
```

### Docker 部署 WebApi

```powershell
docker compose up -d --build
```

服务默认监听 `http://localhost:8080`。

---

## 10. 测试

### 运行测试

```powershell
# 运行全部测试
dotnet test PRO.sln

# 运行特定测试类
dotnet test PRO.sln --filter "BranchIsolationTests"

# 详细输出
dotnet test PRO.sln --logger "console;verbosity=detailed"
```

### 测试覆盖范围

- 分公司数据隔离验证（26 个测试用例）
- 权限控制验证
- 审计日志完整性验证
- 订单状态流转规则验证

---

## 11. 贡献指南

我们欢迎任何形式的贡献！请遵循以下流程：

### 贡献流程

1. **Fork 本仓库**并克隆至本地

   ```powershell
   git clone https://github.com/<你的用户名>/PRO.git
   cd PRO
   ```

2. **创建特性分支**

   ```powershell
   git checkout -b feature/你的功能名称
   ```

3. **开发与测试**

   - 遵循项目现有代码风格
   - 保持 Clean Architecture 分层架构
   - ViewModel 使用 CommunityToolkit.Mvvm 源生成器
   - 确保 `dotnet build PRO.sln` 零错误零警告
   - 为新增功能编写测试用例

4. **提交代码**

   ```powershell
   git add .
   git commit -m "feat: 新增XXX功能"
   ```

   提交信息规范（参考 Conventional Commits）：
   - `feat:` — 新功能
   - `fix:` — Bug 修复
   - `docs:` — 文档更新
   - `style:` — 代码格式调整
   - `refactor:` — 代码重构
   - `test:` — 测试相关
   - `chore:` — 构建/工具链变更

5. **推送并创建 Pull Request**

   ```powershell
   git push origin feature/你的功能名称
   ```

6. **Code Review 与合并**

   - PR 标题描述清晰，关联对应 Issue
   - 通过 CI 构建检查
   - 至少一位 Reviewer 审批通过

### 开发环境配置

推荐使用以下 IDE：

| IDE | 说明 |
|-----|------|
| JetBrains Rider 2024+ | .NET 开发首选，内置 WPF 预览支持 |
| Visual Studio 2022 | 社区版以上，需要安装 .NET 桌面开发工作负载 |
| VS Code + C# Dev Kit | 轻量级选择 |

### 项目规范

- **命名**：使用 PascalCase 命名类、方法、属性；camelCase 命名局部变量
- **分层**：严格遵守 Clean Architecture 依赖方向
- **注释**：公共 API 和复杂逻辑添加 XML 文档注释
- **日志**：使用 Serilog 结构化日志，避免 `Console.WriteLine`
- **异常**：不吞异常，WebApi 和 MVVM 层统一捕获并返回友好错误信息
- **安全**：敏感信息通过环境变量注入，不提交至代码仓库

---

## 12. 常见问题

<details>
<summary><b>Q: 登录时提示"密码错误"？</b></summary>

- 检查工号是否输入正确（区分大小写）
- 首次登录使用默认密码 `admin123`
- 忘记密码请联系管理员在员工管理中重置
- 连续 5 次错误密码账号将被锁定 30 分钟
</details>

<details>
<summary><b>Q: 订单保存失败？</b></summary>

- 必须选择客户后才可保存订单
- 必须添加至少一个产品
- 检查数据库连接是否正常
- 查看 `logs/pro-*.log` 日志获取详细错误信息
</details>

<details>
<summary><b>Q: 库存数量不准确？</b></summary>

- 查看「库存变动日志」追溯每次变动原因
- 执行「库存盘点」手动校准库存数量
- 检查是否有并发操作（同一产品同时被多个订单扣减）
</details>

<details>
<summary><b>Q: 收款后订单状态没更新？</b></summary>

- 部分收款时订单仍保持原状态
- 需手动确认收款完成后更新状态
- 刷新页面后查看最新状态
</details>

<details>
<summary><b>Q: 导出 Excel 失败？</b></summary>

- 检查导出路径写入权限
- 检查磁盘可用空间
- 尝试减少导出数据量（添加筛选条件缩小范围）
- 大数据量导出建议使用异步导出功能
</details>

<details>
<summary><b>Q: 系统运行缓慢？</b></summary>

- 检查网络连接（数据库远程部署时）
- 减少同时打开的标签页数量
- 检查数据库是否有慢查询，运行 `scripts/phase3_indexes.sql` 优化索引
- 关闭不必要的企业微信同步任务
</details>

---

## 13. 版本历史

### v2.1（2026-06-17）

**新增功能：**
- .NET MAUI 手机 App 骨架（Android/iOS），MVVM 架构，8 页面完整导航
- 移动端 API 控制器: Dashboard 工作台 (`GET /api/dashboard/workbench`)、用户信息 (`GET /api/user/profile`)、健康检查 (`GET /health`)
- PWA 健康检查 API 集成、Element Plus 按需加载、ECharts 动态导入
- MAUI 移动端全功能服务层：`DashboardMobileService`、`HealthCheckService`、`ProductMobileService`、`BranchMobileService`
- App 品牌图标 (蓝底白P) — `Resources/AppIcon/appicon.svg`
- 值转换器：`InvertBoolConverter`、`IsNotNullConverter`、`StringToColorConverter`

**构建验证：**
- ✅ MAUI Android 编译通过: `dotnet build -f net8.0-android` — **0 错误 0 警告**
- ✅ 完整解决方案: `dotnet build PRO.sln` — **0 错误 0 警告**
- 安装 MAUI workload (Android/iOS SDK，包含 Java 21 + Android SDK 集成)

**编译修复 (5 errors → 0)：**
- `App.xaml.cs`：`PRO.Application` 命名空间冲突 → 使用 `Microsoft.Maui.Controls.Application`
- `AppShell`：DI 注入 `TokenStore` 依赖 → 通过 `IServiceProvider` 解析
- `PlatformServices.SetAsync`：void 返回赋值错误修复
- `LoginViewModel`：添加 `ErrorMessage` 属性匹配 XAML 绑定
- `MauiProgram`：添加 `Microsoft.Extensions.Logging.Debug` 包 + `AppShell` DI 注册
- 消除 2 个 CS8604 nullable 警告

**重构优化：**
- `BranchForbidden` 统一提取到 `BaseApiController`，消除 8 个控制器中的重复代码
- 5 个 `ControllerBase` 子类统一改为 `BaseApiController`
- MAUI 移动端服务层完善：健康检查、修改密码、完整 CRUD、防重复点击
- ProfileViewModel 修复（伪健康检查、UserStore 事件订阅）
- ApiClient 增强非 2xx 状态码处理、403 响应识别
- ConnectivityService 实现 IDisposable 防止内存泄漏
- CI 新增 `pwa-build` 和条件 `mobile-build` (tags/mobile-*) 作业

**文档：**
- `docs/native-mobile-app-plan.md` — MAUI vs Flutter 技术评估
- `docs/frontend-performance.md` — PWA 包体积优化策略
- API 文档更新至 v2.1

### v2.0（2026-06-09）

**新增功能：**
- 订单模板与一键复制
- 客户档案（完整客户画像）
- 数据质量检测与自动评分
- 收款登记与应收核销
- 库存变动日志与盘点
- 操作撤销（关键操作 5 分钟内可撤回）
- 完整审计日志
- 全局搜索与快捷键系统
- 视图记忆（筛选条件/列宽自动保存）

**优化改进：**
- 客户列表增加最近下单时间、累计金额、应收余额列
- 订单状态流转严格化，拒绝非法跳转
- 自动保存机制增强（30 秒间隔草稿保存）
- 页面加载性能优化
- 修复多个已知 Bug

### v1.0（2026-01-01）

- 初始版本发布
- 基础客户 CRUD、客户层级管理
- 订单全生命周期管理
- 产品管理与库存管理
- 配送员管理与订单分配
- 结算管理
- 工作计划与排班
- 企业微信集成（客户同步、扫码登录）
- 组织架构与角色权限
- 操作日志记录

---

## 14. 开源协议

本项目基于 [MIT License](LICENSE) 开源。

**关键条款：**
- ✅ 允许商业使用
- ✅ 允许修改源代码
- ✅ 允许分发和私有使用
- ✅ 无需承担法律责任
- ⚠️ 需保留原作者版权声明

---

## 文档索引

| 文档 | 路径 | 说明 |
|------|------|------|
| 用户操作手册 | [`docs/user-manual.md`](docs/user-manual.md) | 完整功能操作指南 |
| API 接口文档 | [`docs/api-documentation.md`](docs/api-documentation.md) | RESTful API 规范说明 |
| 数据库迁移指南 | [`docs/database-migration.md`](docs/database-migration.md) | EF Core 迁移操作 |
| 管理员配置指南 | [`docs/admin-configuration.md`](docs/admin-configuration.md) | 系统初次部署与运维配置 |
| 用户验收测试 | [`docs/user-acceptance-test.md`](docs/user-acceptance-test.md) | 17 项 UAT 测试用例 |
| 交付验证报告 | [`docs/delivery-report.md`](docs/delivery-report.md) | 发布前质量验证报告 |

---

<p align="center">
  <b>PRO 企业管理系统</b> · 让企业运营更高效<br>
  <sub>Made with ❤️ by PRO Team · © 2026</sub>
</p>
