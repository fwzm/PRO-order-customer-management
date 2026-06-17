# PRO.Mobile — 原生手机 App

基于 .NET MAUI 的 PRO 订单与客户管理系统移动端。

## 前置条件

```powershell
# 1. 安装 .NET SDK 8.0+
# 2. 安装 MAUI 工作负载
dotnet workload install maui

# 3. Android 开发需安装 Android SDK（通过 Visual Studio Installer 或手动）
```

## 本地构建

```powershell
# 还原依赖
dotnet restore src/PRO.Mobile/PRO.Mobile.csproj

# 构建（Android）
dotnet build src/PRO.Mobile/PRO.Mobile.csproj -c Debug -f net8.0-android

# 发布 APK（Release）
dotnet publish src/PRO.Mobile/PRO.Mobile.csproj -c Release -f net8.0-android -o ./publish/mobile/android

# 构建（iOS — 仅 macOS）
dotnet build src/PRO.Mobile/PRO.Mobile.csproj -c Debug -f net8.0-ios
```

## 配置 WebApi 地址

开发环境（Android 模拟器 → 宿主机）：自动使用 `http://10.0.2.2:5000`  
生产环境：设置环境变量 `PRO_API_BASE_URL` 指向 WebApi 地址

```powershell
# 示例
$env:PRO_API_BASE_URL = "https://api.example.com"
```

## 项目结构

```
PRO.Mobile/
├── PRO.Mobile.csproj          # 项目文件（TargetFrameworks: net8.0-android;net8.0-ios）
├── MauiProgram.cs              # MAUI 启动 + DI 注册
├── App.xaml / App.xaml.cs      # Application
├── AppShell.xaml / .cs         # Shell 导航
├── AppConfig.cs                # 全局配置
│
├── Services/
│   ├── Interfaces.cs           # IApiClient, IAuthService, ICustomerMobileService, IOrderMobileService, etc.
│   ├── ApiClient.cs            # HTTP 客户端（自动 Auth Header, 401 处理, 错误提示）
│   ├── AuthServiceProxy.cs     # 登录/登出/Token 管理
│   ├── MobileServices.cs       # CustomerMobileService, OrderMobileService
│   └── PlatformServices.cs     # ConnectivityService, SecureTokenStore, MobileToastService
│
├── Stores/
│   ├── TokenStore.cs           # JWT Token 内存存储 + 401 事件
│   └── UserStore.cs            # 当前用户信息
│
├── ViewModels/
│   ├── LoginViewModel.cs       # 登录逻辑
│   ├── WorkbenchViewModel.cs   # 首页工作台
│   ├── CustomerListViewModel.cs # 客户列表（分页/搜索/下拉刷新）
│   ├── CustomerDetailViewModel.cs
│   ├── OrderListViewModel.cs   # 订单列表
│   ├── OrderDetailViewModel.cs
│   ├── OrderNewViewModel.cs    # 新建轻量订单
│   └── ProfileViewModel.cs     # 个人信息/退出
│
├── Views/                      # XAML 页面
│   ├── LoginPage.xaml
│   ├── WorkbenchPage.xaml
│   ├── CustomerListPage.xaml
│   ├── CustomerDetailPage.xaml
│   ├── OrderListPage.xaml
│   ├── OrderDetailPage.xaml
│   ├── OrderNewPage.xaml
│   └── ProfilePage.xaml
│
└── Platforms/
    ├── Android/                # Android 入口
    └── iOS/                    # iOS 入口
```

## 架构说明

- 所有数据访问通过 `PRO.WebApi`（JWT Bearer Token），**不直连数据库**
- 共享 `PRO.Application.DTOs`（零成本复用 `ApiResponse<T>`、`PagedResult<T>`、`LoginResponse` 等）
- MVVM 模式（CommunityToolkit.Mvvm），与 WPF 桌面端一致
- DI 容器管理服务生命周期
- 401 自动退出 → 跳转登录页

## 不加入 PRO.sln 的原因

MAUI 项目需要 `maui` workload。将该 workload 作为可选依赖，避免破坏现有 CI（GitHub Actions 默认不安装 maui workload）。MAUI 构建通过独立 `csproj` 和命令行脚本控制。
