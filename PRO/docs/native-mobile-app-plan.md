# PRO 原生手机 App 技术评估与实施计划

> **版本：** v1.0  
> **日期：** 2026-06-17  
> **作者：** PRO Team  
> **状态：** 已评审

---

## 目录

1. [概述](#概述)
2. [技术选型对比：.NET MAUI vs Flutter](#技术选型对比net-maui-vs-flutter)
3. [推荐路线](#推荐路线)
4. [禁止直连数据库的原因](#禁止直连数据库的原因)
5. [App 与 WebApi 交互架构](#app-与-webapi-交互架构)
6. [MVP 功能范围](#mvp-功能范围)
7. [二期增强功能](#二期增强功能)
8. [打包签名与发布流程](#打包签名与发布流程)
9. [风险与成本](#风险与成本)
10. [时间线](#时间线)

---

## 概述

PRO 当前已有 WPF 桌面端（`PRO.Desktop`）、PWA 前端（`PRO.Admin.Web`）及 RESTful WebApi（`PRO.WebApi`）。为覆盖移动办公场景（配送员现场操作、业务员外出拜访、管理层移动审批），需引入原生手机 App。

**核心约束：**
- 绝不会破坏现有 WPF 桌面端、WebApi、PWA 及 CI
- 移动端必须通过 `PRO.WebApi` 访问数据，严禁直连数据库
- 团队背景：.NET / WPF / C# 为主

---

## 技术选型对比：.NET MAUI vs Flutter

| 评估维度 | .NET MAUI | Flutter | 备注 |
|---------|-----------|---------|------|
| **开发语言** | C# / XAML | Dart | 团队 C# 经验丰富，Dart 零基础 |
| **开发效率** | ⭐⭐⭐⭐⭐ 高。共享 PRO.Domain / PRO.Application DTOs 和服务接口，View → ViewModel → Service 三层与现有 MVVM 一致 | ⭐⭐⭐ 中。需重写所有 DTO 和 API 调用层，无法直接复用 .NET 代码 | MAUI 可引用 `PRO.Application.csproj` 直接复用全部 DTO、ApiResponse、PagedResult 等 |
| **.NET/WebApi/DTO 复用度** | ⭐⭐⭐⭐⭐ 100% 复用 DTO、枚举、部分验证逻辑。`ApiResponse<T>`、`PagedResult<T>`、`LoginRequest/Response` 等零改造直接使用 | ⭐⭐ 低。需手写 JSON 模型类，接口变更需两端同步 | 这是 .NET 团队选 MAUI 的最大理由 |
| **UI 开发模式** | XAML + MVVM（CommunityToolkit.Mvvm）— 与 WPF 桌面端完全一致 | Widget 树 + Provider/BLoC — 全新范式 | 团队可零学习成本从 WPF 迁移到 MAUI |
| **APK 打包** | `dotnet publish -f net8.0-android` 一键出包。AAB（Google Play）同流程 | `flutter build apk --release` / `flutter build appbundle` | 均可 |
| **iOS 上架** | 需 Mac + Xcode（签名、证书、Provisioning Profile）。C# 编译为 ARM64 原生代码，需 mac 构建宿主 | 需 Mac + Xcode。Dart → 原生 ARM64，体验成熟 | iOS 上架流程两者几乎相同 |
| **离线支持** | SQLite（EF Core 或直接）、SecureStorage for token | SQLite（sqflite）、SharedPreferences | 均可 |
| **推送通知** | 需集成 Firebase / APNs 原生 SDK 通过平台通道 | Firebase Messaging 插件生态成熟 | Flutter 插件生态更丰富，但 MAUI 有 Microsoft 支持 |
| **扫码/拍照** | Camera.MAUI / ZXing.Net.Maui 社区插件 | camera / qr_code_scanner 成熟插件 | Flutter 生态更成熟，MAUI 基本够用 |
| **维护成本** | ⭐⭐⭐⭐⭐ 低。与桌面端共享 80%+ 代码（DTO、枚举、服务接口、业务规则），升级 SDK 统一 | ⭐⭐⭐ 中。需维护两套独立的 API 模型和调用层，接口变更需两侧同步 | 随着系统增长，双语言维护成本将显著攀升 |
| **成熟度** | ⭐⭐⭐ .NET 8 MAUI 已生产可用。部分社区插件仍在完善中 | ⭐⭐⭐⭐⭐ 非常成熟，Google 主力维护，大量成功案例 | 
| **学习曲线** | ⭐⭐⭐⭐⭐ 极低（对团队） | ⭐⭐ 高（Dart + Widget 树全新概念） | 
| **CI/CD** | Azure Pipelines / GitHub Actions 原生支持，与现有 `dotnet build` CI 无缝融合 | 需要额外 Dart/Flutter SDK 注入 | MAUI 构建可复用现有 CI 基础设施 |
| **招聘难度** | .NET/C# 开发者充足 | Flutter 开发者相对稀缺 | 团队现有成员可直接上手 |

### 综合评价

| 方案 | 团队匹配度 | 代码复用 | 长期维护 | 推荐优先级 |
|------|-----------|---------|---------|-----------|
| **.NET MAUI** | 🟢 极高 | 🟢 80%+ | 🟢 低 | ⭐ **强烈推荐** |
| Flutter | 🟡 中等 | 🔴 <10% | 🟡 中等 | ⭐⭐ 备选 |

---

## 推荐路线

### 首选：.NET MAUI（强烈推荐）

**推荐理由（按优先级排列）：**

1. **零学习成本**：XAML + MVVM + CommunityToolkit.Mvvm 与现有 WPF 桌面端 100% 一致，团队所有成员可直接投入开发
2. **最大化代码复用**：直接引用 `PRO.Application.csproj`，复用全部 DTO（`ApiResponse<T>`、`PagedResult<T>`、`LoginRequest/Response`、`CustomerListItem`、`OrderListItem` 等 18+ DTO 文件）、枚举、Service 接口定义（`IAuthService`、`ICustomerService`、`IOrderService` 等）
3. **统一 CI**：`dotnet build` 即可编译 MAUI 项目，与现有 GitHub Actions CI 完全兼容
4. **统一技术栈**：减少团队认知负担，同一套技术支撑 WPF桌面端 + WebApi + MAUI移动端
5. **长期维护成本最低**：业务逻辑变更时，DTO/枚举/校验规则三端自动一致

**技术架构：**
```
src/PRO.Mobile/                    ← MAUI 项目（新增）
  ├── Views/                       ← XAML 页面
  ├── ViewModels/                  ← CommunityToolkit.Mvvm ViewModel
  ├── Services/                    ← 移动端专属服务（API Client、Auth、Token存储）
  ├── Models/                      ← 移动端专属模型（极少，主要在 PRO.Application.DTOs 中）
  ├── Stores/                      ← Token/用户状态管理
  └── PRO.Mobile.csproj            ← 引用 PRO.Application.csproj
```

**不引入 PRO.sln 的原因：** MAUI 项目需要特定工作负载（`dotnet workload install maui`），未安装该工作负载的 CI 环境无法还原 MAUI 项目。独立 csproj 文件 + 独立构建脚本，不破坏现有 `dotnet build PRO.sln`。

### 备选：Flutter（如 MAUI 遇到不可逾越障碍时启用）

保留 Flutter 作为备选方案。如果 MAUI 在以下方面遇到无法解决的难题：
- iOS 构建稳定性问题
- 特定平台 API 支持不足
- 团队对 MAUI 性能不满意

则切换到 Flutter 时需额外成本：
- 全部 DTO 需 Dart 重写（约 18 个文件）
- API 调用层完全重写
- 需招聘或培训 Flutter 开发人员

---

## 禁止直连数据库的原因

移动 App **严禁**直连 PostgreSQL 数据库，必须通过 `PRO.WebApi` 访问数据。原因如下：

| 原因 | 详细说明 |
|------|---------|
| **安全风险** | 移动设备丢失/被盗后，数据库连接字符串暴露 → 攻击者直接操作数据库，删除数据、窃取客户信息。WebApi 作为网关可保持连接字符串仅存在于服务端 |
| **权限不可控** | 直连数据库无法依靠 WebApi 的 JWT 认证、BranchDataFilter 分公司隔离、PermissionHandler 权限校验。数据库连接只识别 PostgreSQL 用户，无法区分 EmployeeId/RoleId/BranchId |
| **审计日志缺失** | WebApi 通过 `AuditLogFilter` 自动记录所有 API 操作到审计日志。直连数据库绕过该机制，操作无法追溯，违反内控要求 |
| **数据隔离失效** | 分公司数据隔离依赖 `BranchDataFilter` 中间件在应用层实现，数据库层不感知。直连将使任何用户看到全部数据，严重越权 |
| **业务规则绕过** | 订单状态机（`OrderStatusManager`）、库存扣减逻辑、客户合并规则等均在应用层。直连操作将直接修改数据库，跳过所有业务规则，导致数据损坏 |
| **API 限流失效** | WebApi 的 IP 限流（`AspNetCoreRateLimit`）无法保护直连数据库的移动端。恶意用户可无限轮询拖垮数据库 |
| **版本兼容** | WebApi 是数据模型的唯一入口，可通过路由废弃（`[Obsolete]`）和版本演进管理接口兼容性。直连则在每次表结构变更时需同步更新所有客户端 |

**结论：App 必须通过 `PRO.WebApi` 使用 JWT Bearer Token 认证访问所有数据端点，架构如下。**

---

## App 与 WebApi 交互架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        PRO.Mobile (MAUI)                        │
│                                                                 │
│  ┌──────────┐  ┌───────────┐  ┌──────────┐  ┌───────────────┐ │
│  │  Views   │  │ ViewModels │  │ Services │  │    Stores     │ │
│  │ (XAML)   │◄─┤(Observable)│◄─┤(IApiCli..)│◄─┤(Token/User)  │ │
│  └──────────┘  └───────────┘  └────┬─────┘  └───────────────┘ │
│                                    │                            │
│              ┌─────────────────────┼────────────────────┐       │
│              │   IApiClient        │ SecureTokenStore    │       │
│              │   IAuthService      │ ConnectivityService │       │
│              │   ICustomerMobile.. │ MobileToastService  │       │
│              └─────────────────────┴─────────────────────┘       │
└────────────────────────────────┬────────────────────────────────┘
                                 │ HTTPS + JWT Bearer Token
                                 │ (Authorization: Bearer eyJ...)
                                 ▼
┌─────────────────────────────────────────────────────────────────┐
│                        PRO.WebApi (ASP.NET Core)                 │
│                                                                 │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────────────┐  │
│  │ JWT Auth    │  │ BranchFilter │  │  PermissionHandler    │  │
│  │ Middleware  │──┤ Middleware    │──┤  (Policy-based)      │  │
│  └─────────────┘  └──────────────┘  └───────────────────────┘  │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  Controllers                                            │   │
│  │  AuthController → CustomersController → OrdersController│   │
│  │  DashboardController → HealthController                 │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                    │                            │
│  ┌─────────────────────────────────┼──────────────────────┐     │
│  │  PRO.Application (DTOs / Service Interfaces)           │     │
│  │  PRO.Infrastructure (Service Implementations + EF Core)│     │
│  │  PRO.Domain (Entities / Enums / Domain Services)       │     │
│  └───────────────────────────────┬────────────────────────┘     │
└──────────────────────────────────┼──────────────────────────────┘
                                   │
                                   ▼
                          ┌────────────────┐
                          │  PostgreSQL    │
                          │  (PRO Database)│
                          └────────────────┘
```

**交互流程示例（登录 → 获取客户列表）：**

```
1. App 用户输入 工号+密码
2. App 调用 POST /api/auth/login → 返回 JWT Token + 用户信息
3. App 将 Token 存入 SecureStorage（iOS Keychain / Android KeyStore）
4. App 调用 GET /api/customers?pageIndex=1&pageSize=20
   Header: Authorization: Bearer {token}
5. WebApi 认证中间件校验 JWT → 提取 EmployeeId/BranchId/RoleId
6. BranchDataFilter 自动注入 branchId = 用户所属分公司
7. CustomerService 查询 scoped 数据 → 返回分页结果
8. 401 → App 自动清除 Token → 跳转登录页
```

---

## MVP 功能范围

| 优先级 | 功能模块 | 功能点 | 依赖 API |
|--------|---------|--------|----------|
| P0 | **登录认证** | 工号+密码登录、JWT Token 持久化（SecureStorage）、Auto Bearer Header | `POST /api/auth/login` |
| P0 | **Token 刷新** | Access Token 过期前自动刷新、Refresh Token 机制 | `POST /api/auth/refresh-token` |
| P0 | **首页工作台** | 今日概览（订单数、营收、待处理数）、快捷入口、告警列表 | `GET /api/dashboard/workbench`（新增） |
| P0 | **客户列表** | 分页列表、关键字搜索、下拉刷新、客户类型筛选 | `GET /api/customers` |
| P0 | **客户详情** | 完整客户画像（基本信息 + 最近订单 + 应收款） | `GET /api/customers/{id}` |
| P0 | **订单列表** | 分页列表、状态筛选、关键字搜索、下拉刷新 | `GET /api/orders` |
| P0 | **订单详情** | 订单完整信息 + 订单明细 + 状态历史 | `GET /api/orders/{id}` |
| P0 | **新建订单(轻量)** | 选择客户 → 选择产品 → 填写数量和地址 → 保存草稿/提交 | `POST /api/orders` |
| P0 | **个人信息/退出** | 查看个人信息、安全退出（清除 Token） | `POST /api/auth/logout` |
| P0 | **API 健康检查** | App 启动时检测 WebApi 连通性 | `GET /health` |
| P0 | **全局错误处理** | 网络错误 Toast、加载中 Loading、空状态 EmptyView、401 自动退出 | 所有 API |
| P1 | **收款登记** | 现场收款记录（微信/支付宝/现金） | `POST /api/payments` |
| P1 | **扫码找客户** | 扫描客户二维码快速定位 | 本地 + API |
| P1 | **配送签收** | 配送员确认送达、拍照存证 | `POST /api/orders/{id}/status` |
| P1 | **离线草稿** | 无网络时可创建订单草稿，恢复网络后自动同步 | SQLite 本地存储 |

---

## 二期增强功能

| 功能 | 描述 | 技术要点 |
|------|------|---------|
| **推送通知** | 新订单分配推送、结算通知、异常告警 | Firebase Cloud Messaging (Android) + APNs (iOS) |
| **离线模式** | 客户/订单/产品数据本地缓存，断网可浏览 | SQLite + EF Core 本地同步，HttpClient + Polly 重试 |
| **拍照上传** | 配送签收拍照、客户拜访现场拍照 | Camera.MAUI / ZXing.Net.Maui |
| **地图导航** | 配送路径导航、客户位置标注 | 高德地图 / 腾讯地图 SDK |
| **企微集成** | 扫码登录、客户绑定、拜访记录同步 | `IWeChatService` 现有接口 |
| **生物识别** | 指纹/面容快速登录 | BiometricAuthentication (MAUI) |
| **暗黑模式** | 夜间配送友好 UI | MAUI AppThemeBinding |

---

## 打包签名与发布流程

### Android APK/AAB 发布

```powershell
# 1. 生成签名密钥（首次）
keytool -genkey -v -keystore pro-release.keystore -alias pro -keyalg RSA -keysize 2048 -validity 10000

# 2. Release 构建（APK）
dotnet publish src/PRO.Mobile -c Release -f net8.0-android -o ./publish/mobile

# 3. 构建 AAB（Google Play）
dotnet publish src/PRO.Mobile -c Release -f net8.0-android -p:AndroidPackageFormat=aab

# 4. 签名（如未在 csproj 中配置）
jarsigner -verbose -sigalg SHA256withRSA -digestalg SHA-256 -keystore pro-release.keystore ./publish/mobile/com.pro.app-Signed.apk pro
```

**csproj 签名配置：**
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <AndroidKeyStore>True</AndroidKeyStore>
  <AndroidSigningKeyStore>pro-release.keystore</AndroidSigningKeyStore>
  <AndroidSigningKeyAlias>pro</AndroidSigningKeyAlias>
  <AndroidSigningKeyPass>${PRO_KEYSTORE_PASS}</AndroidSigningKeyPass>
  <AndroidSigningStorePass>${PRO_KEYSTORE_PASS}</AndroidSigningStorePass>
</PropertyGroup>
```

### iOS IPA 发布

```powershell
# 在 Mac 构建机上执行
dotnet publish src/PRO.Mobile -c Release -f net8.0-ios -r ios-arm64 -o ./publish/ios

# Xcode Archive → Distribute App → App Store Connect
```

**必需的 iOS 配置：**
- Apple Developer Program 账号（$99/年）
- Bundle Identifier（如 `com.pro.mobile`）
- 分发证书（Distribution Certificate）
- Provisioning Profile（App Store）
- App Store Connect 元数据（截图、描述、隐私政策 URL）

### 应用商店 Checklist

| 项目 | Android | iOS |
|------|---------|-----|
| 开发账号 | Google Play Console ($25 一次) | Apple Developer ($99/年) |
| 应用图标 | 1024×1024 PNG | 1024×1024 PNG（无 Alpha） |
| 截图 | 至少 2 张，多种尺寸 | 6.7'' + 6.5'' + 5.5'' 至少各 2 张 |
| 隐私政策 URL | 必需 | 必需 |
| 内容分级问卷 | 必需 | 必需 |
| 内部测试 | Google Play Internal Testing | TestFlight |

---

## 风险与成本

### 技术风险

| 风险 | 等级 | 缓解措施 |
|------|------|---------|
| MAUI iOS 构建不稳定 | 🟡 中 | 先用 Android MVP 验证，iOS 二期再上线。构建用 Mac agent |
| 第三方插件不足 | 🟢 低 | Camera/Zxing/Map 均有成熟社区方案，核心需求不依赖未成熟插件 |
| XAML 渲染性能 | 🟢 低 | CollectionView 虚拟化、数据分页、图片懒加载 |
| 与现有 WebApi 兼容 | 🟢 低 | WebApi 已支持 JWT + 分页 + 统一 ApiResponse，无需改造即可对接 |
| CI 构建变慢 | 🟡 中 | MAUI 项目独立 csproj，不加入 PRO.sln，按需本地构建 |

### 人员成本

| 角色 | 人数 | 周期 | 说明 |
|------|------|------|------|
| MAUI 开发 | 1-2 人 | MVP 4 周 | 现有 WPF 开发人员可直接担任 |
| WebApi 扩充 | 1 人 | 1 周 | 补齐移动端专属接口（DashboardController 等） |
| QA 测试 | 1 人 | 1 周 | 功能测试 + 真机兼容测试 |
| DevOps | 0.5 人 | 3 天 | 签名配置 + 打包脚本 + CI 调整 |

### 云成本（估算）

| 资源 | 月成本 | 说明 |
|------|--------|------|
| WebApi 服务器 | 已有 | 复用现有 |
| PostgreSQL | 已有 | 复用现有 |
| Apple Developer | $8.25/月 | $99/年 |
| Google Play | $2/月 | $25 一次性 |

---

## 时间线

```
Week 1:  技术评估确认 + 项目骨架搭建 + WebApi 接口补齐
Week 2:  登录/Token 持久化 + 首页工作台 + 客户列表/详情
Week 3:  订单列表/详情 + 新建订单 + 个人信息/退出
Week 4:  全局错误处理 + 健康检查 + Android 打包签名 + 内部测试
Week 5:  收款登记 + 扫码功能 + 真机测试 + 问题修复
Week 6:  iOS 适配 + TestFlight 发布 (可选，视需求)
```

---

## 决策记录

- [x] 选择 .NET MAUI 作为原生手机 App 框架
- [x] MVVM + CommunityToolkit.Mvvm（与 WPF 一致）
- [x] 通过 PRO.WebApi 访问全部数据（不直连数据库）
- [x] MAUI 项目独立 csproj（不加入 PRO.sln），避免影响现有 CI
- [x] MVP 优先 Android（内部测试分发便捷），iOS 二期上线
- [x] 复用 PRO.Application DTOs（零改造）

---

**文档版本：** v1.0  
**下次评审：** MVP 发布后复审
