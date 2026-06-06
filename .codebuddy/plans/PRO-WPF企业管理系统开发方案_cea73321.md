---
name: PRO-WPF企业管理系统开发方案
overview: 从零开发名为"PRO"的Windows原生WPF桌面应用，适配自部署Nocodb数据库，实现客户管理、订单管理、产品库存、物流配送、财务结算、工作计划等核心功能，支持完整企业微信API对接、精细化权限控制、离线优先架构与实时双向同步，达到生产级标准。
design:
  fontSystem:
    fontFamily: system-ui
    heading:
      size: 18px
      weight: 600
    subheading:
      size: 14px
      weight: 500
    body:
      size: 13px
      weight: 400
  colorSystem:
    primary:
      - "#1E3A5F"
      - "#4A90D9"
    background:
      - "#F5F7FA"
      - "#FFFFFF"
      - "#EEF2F7"
    text:
      - "#2C3E50"
      - "#FFFFFF"
      - "#7F8C8D"
    functional:
      - "#27AE60"
      - "#F39C12"
      - "#E74C3C"
todos:
  - id: create-solution-structure
    content: 创建.NET 8 WPF解决方案结构，配置项目引用和NuGet包
    status: completed
  - id: design-database-schema
    content: 设计SQLite本地缓存和NocoDB远程表结构，创建数据库初始化脚本
    status: completed
    dependencies:
      - create-solution-structure
  - id: implement-domain-entities
    content: 实现Domain层实体、枚举、值对象定义
    status: completed
    dependencies:
      - create-solution-structure
  - id: implement-infrastructure
    content: 实现Infrastructure层：SQLite持久化、NocoDB客户端、数据加密服务
    status: completed
    dependencies:
      - implement-domain-entities
  - id: implement-auth-permission
    content: 实现权限系统：角色+页面+按钮+字段四级权限控制
    status: completed
    dependencies:
      - implement-infrastructure
  - id: implement-main-shell
    content: 实现主窗体：侧边栏导航、标签栏、页面路由系统
    status: completed
    dependencies:
      - implement-auth-permission
  - id: implement-system-settings
    content: 实现系统设置模块：普通设置和总部管理功能
    status: completed
    dependencies:
      - implement-main-shell
  - id: implement-customer-module
    content: 实现客户管理模块：大客户层级、查重合并、企业微信同步
    status: completed
    dependencies:
      - implement-main-shell
  - id: implement-order-module
    content: 实现订单管理模块：状态流转、草稿机制、修改记录
    status: completed
    dependencies:
      - implement-main-shell
  - id: implement-product-module
    content: 实现产品与库存管理模块
    status: completed
    dependencies:
      - implement-main-shell
  - id: implement-logistics-module
    content: 实现物流管理模块：配送员管理、高德路径规划Excel导出
    status: completed
    dependencies:
      - implement-order-module
  - id: implement-settlement-module
    content: 实现财务结算模块：PDF标准化模板生成
    status: completed
    dependencies:
      - implement-order-module
      - implement-product-module
  - id: implement-workplan-module
    content: 实现工作计划模块：排班管理、详细计划、草稿机制
    status: completed
    dependencies:
      - implement-auth-permission
  - id: implement-sync-engine
    content: 实现数据同步引擎：离线优先、时间戳冲突解决、WeChat同步
    status: completed
    dependencies:
      - implement-infrastructure
  - id: implement-wechat-integration
    content: 集成企业微信SDK：通讯录同步、客户管理、消息推送
    status: completed
    dependencies:
      - implement-sync-engine
  - id: performance-optimization
    content: 性能优化：虚拟化、懒加载、缓存策略
    status: completed
    dependencies:
      - implement-sync-engine
  - id: build-deployment-package
    content: 构建部署包：安装程序、一键部署脚本、配置向导
    status: completed
    dependencies:
      - performance-optimization
---

## 项目概述

PRO是一款面向多分公司企业的原生Windows桌面应用，基于WPF技术栈开发，深度集成自部署Nocodb数据库，实现企业全业务流程的数字化管理。

## 核心功能需求

### 1. 系统设置模块

- **普通设置**（所有用户可见）：组织架构管理（部门/员工基础管理）、应用关闭行为配置
- **总部管理**（仅管理员）：企业微信配置、Webhook管理、数据备份恢复、同步配置、数据库与API配置、员工排班管理、权限分配

### 2. 客户管理模块

- 基础操作：新增、编辑（软删除）、查询、详情查看、Excel导出
- 查重合并：按手机号、名称+地址、法人+手机号匹配
- 大客户层级：大客户→分公司/分店层级关系
- 企业微信双向同步

### 3. 订单管理模块

- 订单全流程：待分配→已分配→配送中→已完成/配送失败/已取消
- 收款状态：已收款、未收款/部分收款、移交法务处理
- 订单草稿：30分钟自动转正
- 修改记录追溯

### 4. 产品与库存模块

- SKU唯一标识、名称、规格、单价、成本价（仅管理员）、参考价
- 库存为0仍可开单

### 5. 物流管理模块

- 配送员管理：姓名、手机号、服务区域、车辆、微信号、负载、状态
- 订单分配：手动分配 + AI分配接口
- 高德路径规划Excel导出（固定格式）

### 6. 财务结算模块

- 时间范围结算、累积汇总
- PDF标准化模板：公司信息、明细表、汇总、签字区
- 近30天记录、批量下载

### 7. 工作计划模块

- 管理员：排班设置、总工时计算
- 普通员工：每日详细计划（5分钟为单位）
- 草稿机制：本地保存，不同步Nocodb

## 数据安全与权限

- 敏感数据加密存储
- 角色+页面+按钮+字段四级权限控制
- 操作日志：本地存储，结算时同步

## 性能要求

- 单页≤50条数据响应<2秒
- 10万+订单分页加载，首屏<3秒
- 支持Windows 10 1909+/Win11，64位系统

## 技术选型

### 核心框架

- **.NET 8** - 最新LTS版本，性能最优
- **WPF** - 原生Windows桌面框架，无Web套壳
- **MVVM架构** - CommunityToolkit.Mvvm

### 数据层

- **SQLite** (本地缓存) - 轻量级本地数据库
- **NocoDB SDK** (远程同步) - REST API v3对接
- **Dapper** - 高效ORM

### 企业微信

- **企业微信SDK** - 通讯录同步、客户管理、消息推送
- **WorkArea.WeChat** - 开源组件库

### PDF生成

- **QuestPDF** - .NET原生PDF生成，支持模板化设计

### UI组件

- **MaterialDesignInXAML** - 现代化UI控件库
- **HandyControl** - 补充控件集

### 安全

- **DPAPI** - Windows原生数据加密
- **BCrypt.Net** - 密码哈希

## 架构设计

### 分层架构

```
┌─────────────────────────────────────────┐
│           Presentation Layer            │
│    (Views, ViewModels, Converters)      │
├─────────────────────────────────────────┤
│           Application Layer             │
│   (Services, DTOs, Commands/Queries)    │
├─────────────────────────────────────────┤
│            Domain Layer                 │
│    (Entities, Value Objects, Enums)     │
├─────────────────────────────────────────┤
│         Infrastructure Layer            │
│  (SQLite, NocoDB, WeChat, Encryption)   │
└─────────────────────────────────────────┘
```

### 数据同步机制

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   SQLite    │◄───►│ Sync Engine │◄───►│   NocoDB    │
│  (Local)    │     │  (Conflict  │     │  (Remote)   │
│             │     │ Resolution) │     │             │
└─────────────┘     └─────────────┘     └─────────────┘
                          │
                          ▼
                   ┌─────────────┐
                   │   WeChat    │
                   │    API      │
                   └─────────────┘
```

### 离线优先策略

1. 所有操作先写入本地SQLite
2. 标记同步状态：Pending/Synced/Conflict
3. 联网时按时间戳自动解决冲突
4. 结算时批量同步操作日志

## 目录结构

```
PRO/
├── PRO.sln
├── src/
│   ├── PRO.Domain/                 # [NEW] 领域层
│   │   ├── Entities/               # 实体定义
│   │   ├── Enums/                  # 枚举类型
│   │   └── ValueObjects/           # 值对象
│   ├── PRO.Application/            # [NEW] 应用层
│   │   ├── Services/               # 应用服务
│   │   ├── DTOs/                   # 数据传输对象
│   │   └── Interfaces/              # 接口定义
│   ├── PRO.Infrastructure/         # [NEW] 基础设施层
│   │   ├── Persistence/            # SQLite实现
│   │   ├── NocoDB/                 # NocoDB客户端
│   │   ├── WeChat/                 # 企业微信集成
│   │   └── Security/               # 加密服务
│   └── PRO.Desktop/               # [NEW] 桌面端
│       ├── Views/                  # 窗体和页面
│       ├── ViewModels/             # 视图模型
│       ├── Resources/              # 资源文件
│       ├── Converters/             # 值转换器
│       └── Controls/               # 自定义控件
├── tests/
│   └── PRO.Tests/                  # [NEW] 单元测试
└── deployment/                     # [NEW] 部署相关
```

## 数据库表设计 (SQLite本地缓存)

### 组织架构

- `Branches` - 分公司表
- `Departments` - 部门表（支持树形）
- `Employees` - 员工表
- `Roles` - 角色表
- `Permissions` - 权限表
- `RolePermissions` - 角色权限关联

### 业务数据

- `Customers` - 客户表（大客户/细分客户）
- `Products` - 产品表
- `Orders` - 订单表
- `OrderItems` - 订单明细
- `DeliveryPersons` - 配送员表
- `Settlements` - 结算表

### 工作计划

- `WorkSchedules` - 排班表
- `WorkPlans` - 详细计划表
- `PlanDrafts` - 计划草稿

### 系统

- `OperationLogs` - 操作日志
- `SyncRecords` - 同步记录
- `LocalSettings` - 本地设置

## NocoDB表结构 (远程)

### 需创建的表

1. `sys_branches` - 分公司
2. `sys_departments` - 部门
3. `sys_employees` - 员工
4. `sys_roles` - 角色
5. `sys_permissions` - 权限
6. `sys_role_permissions` - 角色权限关联
7. `biz_customers` - 客户
8. `biz_products` - 产品
9. `biz_orders` - 订单
10. `biz_delivery_persons` - 配送员
11. `fin_settlements` - 结算
12. `work_schedules` - 排班
13. `work_plans` - 工作计划
14. `sys_operation_logs` - 操作日志
15. `sys_sync_config` - 同步配置
16. `sys_wechat_config` - 企业微信配置

## 关键接口设计

### ISyncService - 同步服务

```
public interface ISyncService
{
    Task<SyncResult> SyncAllAsync(CancellationToken ct);
    Task<SyncResult> SyncEntityAsync<T>(int id, CancellationToken ct) where T : class;
    Task ResolveConflictAsync(int syncRecordId, ConflictResolution resolution);
    event EventHandler<SyncProgressEventArgs> SyncProgressChanged;
}
```

### IPermissionService - 权限服务

```
public interface IPermissionService
{
    bool HasPermission(string permissionKey);
    bool HasFieldPermission(string entity, string field, PermissionType type);
    IEnumerable<string> GetUserPermissions();
}
```

### INocoDBClient - NocoDB客户端

```
public interface INocoDBClient
{
    Task<IEnumerable<T>> ListAsync<T>(FilterOptions options);
    Task<T> GetByIdAsync<T>(string table, string id);
    Task<string> CreateAsync<T>(string table, T entity);
    Task UpdateAsync<T>(string table, string id, T entity);
    Task DeleteAsync(string table, string id);
}
```

## 界面设计风格

采用现代化商务简约设计，以深蓝色为主色调，配合灰色系背景，营造专业、稳重的企业级应用氛围。

### 布局结构

- **主窗体**：左侧固定侧边栏 + 顶部标签栏 + 中央内容区
- **侧边栏**：纯文字导航，层级清晰，无图标干扰
- **标签栏**：浏览器式标签设计，同一功能不可重复打开
- **内容区**：卡片式布局，数据表格+详情面板

### 配色方案

- **主色**：#1E3A5F（深海蓝）
- **辅色**：#4A90D9（天际蓝）
- **背景**：#F5F7FA（云雾灰）
- **文字**：#2C3E50（墨玉黑）
- **成功**：#27AE60
- **警告**：#F39C12
- **错误**：#E74C3C

### 字体系统

- **标题**：微软雅黑 Bold 18px
- **副标题**：微软雅黑 Medium 14px
- **正文**：微软雅黑 Regular 13px
- **辅助**：微软雅黑 Light 12px

### 交互设计

- 鼠标悬停状态变化
- 平滑过渡动画
- 表单验证即时反馈
- 数据加载骨架屏

### 响应式适配

- 最小支持：1366×768
- 最佳显示：1920×1080
- 自动适配窗口缩放

## Agent Extensions

### Skill

- **NocoDB API**
- 用途：实现NocoDB REST API v3的完整对接，包括表结构创建、记录CRUD操作
- 预期结果：生成NocoDB表结构SQL脚本和应用层API调用代码

- **企业微信套件**
- 用途：实现企业微信通讯录同步、客户管理、消息推送功能
- 预期结果：企业微信集成模块代码，实现双向数据同步

- **PDF 文档生成**
- 用途：生成财务结算PDF标准化模板
- 预期结果：包含签字区、明细表、汇总的PDF模板和生成代码