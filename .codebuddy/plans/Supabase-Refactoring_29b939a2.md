---
name: Supabase-Refactoring
overview: 将项目中的 NocoDB 数据库+后端代码改造为使用 Supabase，并保留本地 SQLite 作为离线缓存。目标是让 WPF 桌面端 + Web 管理后台都能通过 Supabase 操作数据，离线时自动使用本地数据。
todos:
  - id: explore-code-details
    content: 使用 [subagent:code-explorer] 全面探索现有仓储、服务、同步模块代码，精确标注每处修改位置
    status: completed
  - id: add-supabase-packages
    content: 添加 supabase-csharp NuGet 包，创建 SupabaseClient 配置类
    status: completed
    dependencies:
      - explore-code-details
  - id: create-supabase-repository
    content: 创建 ISupabaseRepository 和 SupabaseRepository
    status: completed
    dependencies:
      - add-supabase-packages
---

## 需求分析

将 PRO 企业管理系统的数据库和后端代码全部改造为使用 Supabase，并支持离线时数据保存本地。

### 产品概述

改造后的系统架构：

- **Supabase** 作为主要云后端（内置 PostgreSQL + REST API + Auth + Studio 管理后台）
- **本机 SQLite** 作为离线缓存层
- **自动切换**：在线时读写 Supabase，离线时读写 SQLite，恢复网络后自动同步
- **行级权限**：通过 Supabase RLS (Row Level Security) 实现分公司数据隔离

### 核心改造范围

1. **PRO.Infrastructure** -- 替换 NocoDBClient 为 SupabaseClient，新增离线检测和同步机制
2. **PRO.Desktop (WPF)** -- 在线走 Supabase，离线走 SQLite，支持恢复后自动同步
3. **PRO.Api** -- 数据源从 NocoDB 切换为 Supabase (保留所有 API 端点不变)
4. **PRO.Admin.Web** -- 无需改动，继续调 PRO.Api
5. **配置系统** -- 添加 Supabase URL/AnonKey/ServiceKey 等配置项
6. **Supabase 自部署** -- 服务器上部署 Supabase 容器，迁移现有数据

## 技术方案

### 技术栈选择

| 组件 | 技术 | 说明 |
| --- | --- | --- |
| 云后端 | **Supabase** (自部署 Docker) | PostgreSQL + PostgREST + GoTrue + Studio |
| C# SDK | **supabase-csharp** (NuGet) | 官方 C# 客户端，基于 PostgREST 协议 |
| 离线缓存 | **SQLite + EF Core** (已有) | 保留现有本地数据库不变 |
| 联机检测 | **HttpClient 心跳检测** | 定时 ping Supabase 健康检查端点 |
| 同步引擎 | **改造 NocoDBSyncService** | 目标端从 NocoDB 改为 Supabase，复用冲突处理逻辑 |
| 认证 | **Supabase Auth** (JWT) | 替代自建 JWT 认证，或保留 PRO.Api 的 JWT 作为代理层 |


### 整体架构

改造后的数据流：

```
WPF Desktop App
  ├── 在线模式 --> SupabaseClient --> Supabase PostgREST --> PostgreSQL
  │                                        │
  │                                  Supabase Auth (JWT)
  │                                        │
  │                                  Supabase Studio (管理后台)
  └── 离线模式 --> SQLite (EF Core) --> 本地缓存
       ↕ 网络恢复后自动同步

PRO.Api (Web API) -- 数据源从 NocoDB 改为 Supabase
  └── SupabaseClient --> Supabase PostgREST --> PostgreSQL
       (JWT 认证保留，作为前端到后端的代理)

PRO.Admin.Web (Vue3) -- 无需改动
  └── 继续调 PRO.Api
```

### 关键设计决策

#### 1. 分层改造策略（最小改动原则）

```
                    ┌──────────────┐
                    │  IRepository<T>  │ ← 接口不变
                    └──────┬───────┘
               ┌───────────┼───────────┐
               ▼           ▼           ▼
        ┌──────────┐ ┌──────────┐ ┌──────────┐
        │ NocoDB    │ │ Supabase │ │ SQLite   │
        │ Repo      │ │ Repo     │ │ Repo     │ ← 新增
        └──────────┘ └──────────┘ └──────────┘
               │           │           │
               ▼           ▼           ▼
        ┌──────────┐ ┌──────────┐ ┌──────────┐
        │NocoDB    │ │Supabase  │ │EF Core   │
        │Client    │ │Client    │ │DbCtx     │
        └──────────┘ └──────────┘ └──────────┘
```

- **IRepository/ICustomerRepository 等接口完全不变**，只新增实现
- 新增 `SupabaseRepository<T>` 基类封装通用 CRUD
- 新增 `OnlineOfflineRepository<T>` 包装类，自动路由在线/离线请求
- **NocoDBClient 及相关代码删除**

#### 2. 在线/离线自动切换核心机制

```
public class OnlineOfflineRepository<T> : IRepository<T> where T : class
{
    private readonly SupabaseRepository<T> _online;  // Supabase 实现
    private readonly EfRepository<T> _offline;       // SQLite 实现
    private readonly INetworkService _network;

    public async Task<T?> GetByIdAsync(int id)
    {
        if (await _network.IsOnlineAsync())
            return await _online.GetByIdAsync(id);  // 在线读云
        return await _offline.GetByIdAsync(id);      // 离线读本地
    }

    // 写入：在线写云+本地缓存，离线写本地+标记待同步
    public async Task<T> AddAsync(T entity)
    {
        if (await _network.IsOnlineAsync())
        {
            var result = await _online.AddAsync(entity);
            await _offline.AddAsync(entity);        // 同时缓存到本地
            return result;
        }
        // 离线写入本地 + 标记 SyncStatus = Pending
        SetSyncPending(entity);
        return await _offline.AddAsync(entity);
    }
}
```

#### 3. supabase-csharp 使用模式

```
// 初始化
var options = new Supabase.SupabaseOptions { AutoConnectRealtime = false };
var client = new Supabase.Client(supabaseUrl, supabaseAnonKey, options);
await client.InitializeAsync();

// 认证
var session = await client.Auth.SignIn(email, password);

// 查询（强类型，自动映射到 PostgREST）
var customers = await client.From<Customer>()
    .Where(c => c.BranchId == currentBranchId)
    .Order(c => c.CreatedAt, Postgrest.Constants.Ordering.Descending)
    .Get();

// 增删改
await client.From<Customer>().Insert(newCustomer);
await client.From<Customer>().Update(updatedCustomer);
await client.From<Customer>().Delete(customer);
```

#### 4. 差异对照表

| 维度 | 改造前 (NocoDB) | 改造后 (Supabase) |
| --- | --- | --- |
| 远程 API | 自定义 REST 调用 (xc-token) | PostgREST 标准协议 (JWT) |
| SDK | 手写 NocoDBClient | supabase-csharp NuGet 包 |
| 认证 | 自建 JWT + 用户表 | Supabase Auth (内置) |
| 权限控制 | 代码层 BranchId 过滤 | RLS 行级策略 (数据库层) |
| 管理后台 | PRO.Admin.Web | Supabase Studio (内置) |
| 离线同步 | 本地 SQLite ↔ NocoDB | 本地 SQLite ↔ Supabase |
| 部署 | API + Nginx + NocoDB | 只需 Supabase (单组 Docker) |


### 性能与可靠性

- **Supabase PostgREST** 查询延迟约 50-200ms（同机房），本地 SQLite < 1ms
- 批量操作使用 PostgREST 批量端点（`POST /rest/v1/xxx` 传数组）
- 同步冲突策略复用现有 `ConflictResolution` 枚举（时间戳优先/总部优先/手动）
- 网络检测使用定时心跳（每 30 秒 ping 一次 Supabase 健康检查端点）
- 待同步队列存储在 SQLite 的 `SyncQueue` 表中，断网不丢数据

### 实现注意事项

1. **实体映射要求**：Supabase 的 PostgREST 使用 snake_case 字段名，C# 实体用 PascalCase，需在实体上加 `[Table("snake_case_name")]` 和 `[Column("snake_case")]` 属性
2. **认证兼容**：WPF 用户登录可继续用现有用户名密码，后端通过 Supabase Auth 的 `signIn` API 验证，返回的 JWT 可同时用于 PostgREST 调用
3. **数据迁移**：需要在 Supabase 中创建与当前 NocoDB 对应的表结构，并从 NocoDB 的 PostgreSQL 迁移数据
4. **日志**：复用 Serilog，新增 `SupabaseService` 的日志事件 Category
5. **回滚安全**：所有新增代码不影响现有功能，NocoDB 相关代码在验证通过后再删除

## Agent Extensions

### Skill

- **sql-queries**
- 用途：编写 Supabase RLS 策略 SQL 和 PostgreSQL 表结构创建/迁移脚本
- 预期产出：分公司数据隔离的 RLS 策略、表结构 SQL、数据迁移脚本

### MCP

- **CloudBase AI ToolKit** (searchWeb)
- 用途：查询 supabase-csharp NuGet 包最新版本和使用文档
- 预期产出：确认 NuGet 包版本号和 API 用法

### SubAgent

- **code-explorer**
- 用途：在执行阶段详细探索所有需要修改的文件的具体代码内容
- 预期产出：精确定位每处代码的修改位置和内容