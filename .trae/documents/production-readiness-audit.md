# PRO 桌面应用生产环境就绪审计与修复计划

## 当前状态分析

基于对 10+ 核心文件的实际代码审查，发现以下关键问题分类：

### 发现的问题

| 类别 | 严重性 | 说明 |
|------|--------|------|
| 🔴 **DbContext 并发** | 高 | 已修复 OrderEdit/WorkSchedule/Settlement/Product/Inventory 构造函数中的多并发 `_ = LoadXxxAsync()`，但全应用仍有多处类似模式 |
| 🔴 **Fire-and-forget 异常丢失** | 高 | 大量 `_ = LoadDataAsync()` 无异常处理，异常会变成"未观察任务异常"（已在日志确认） |
| 🔴 **导航菜单重复** | 中 | "应用设置"和"高级设置"都指向 `SystemSettingsViewModel`，内容重复（Spec 要求合并但未完成） |
| 🔴 **XAML 绑定断裂** | 高 | `MainWindow.xaml` 仍绑定 `ManualSyncCommand`（已删除），`LogoutCommand`（需验证存在） |
| 🟡 **日志缺失** | 中 | 多个 catch 块只用 `ShowError(msg)` 不写日志，运行时错误无法追踪 |
| 🟡 **空引用风险** | 中 | `Services.GetService()` 多处无 null 检查，`CurrentSession.Current` 构造前调用会抛异常 |
| 🟡 **种子数据过期** | 中 | `InitDatabase.cs` 可能在 Product 实体字段变更后未同步更新 |
| 🟡 **DataGrid 全局 ToolTip** | 低 | ToolTip 绑定可能被模板列覆盖、ContextMenu 不触发、日期格式错误 |
| 🟢 **滚动条美化** | 低 | 已修复 |
| 🟢 **标签页关闭按钮** | 低 | 已修复 |

## 修复方案

### 1. 全应用 DbContext 并发 + 异步异常审计（高优先级）

**目标文件**: 所有 ViewModel 文件

**方案**: 创建统一审计任务，扫描每个 ViewModel 的构造函数和事件处理器，将：
- 构造函数的多个 `_ = LoadXxxAsync()` → 合并为单个 `_ = InitAsync()` 顺序 await
- 所有 `_ = LoadDataAsync()` → 包一层 try-catch + Log.Error

**涉及文件**:
- CustomerDetailViewModel.cs（`_ = LoadAsync()`）
- DashboardViewModel.cs（`_ = LoadDataAsync()` 在构造和事件中）
- WeChatScrmViewModel.cs（4个 `_ = LoadXxxAsync()` on tab changed）
- WeChatCustomerViewModel.cs
- PredictionDashboardViewModel.cs
- VisitRecordViewModel.cs
- AdditionalViewModels.cs（Employee/Product 多处）
- 其他 ViewModel

### 2. 修复导航菜单（中优先级）

**目标文件**: MainViewModel.cs

- 移除 system_advanced（指向 SystemSettingsViewModel 的重复项）
- 或者改为指向 "字段管理" 的新 ViewModel

### 3. 修复 XAML 绑定（高优先级）

**目标文件**: MainWindow.xaml + 所有 View 文件

- 扫描所有 `Command="{Binding Xxx}"` ，对照 ViewModel 检查是否存在
- 特别关注：ManualSyncCommand（已删除，需改回占位命令或移除按钮）
- LogoutCommand 等

### 4. 补全 Log 日志（中优先级）

**目标文件**: 所有 catch 块

- 所有 `catch (Exception ex) { ShowError(...) }` → 追加 `Serilog.Log.Error(ex, ...)`

### 5. 验证种子数据（中优先级）

**目标文件**: InitDatabase.cs

- 确认 Product 种子数据不再引用 Price/CostPrice
- 确认 Warehouse 种子数据存在
- 验证所有种子数据可以成功初始化

### 6. DataGrid ToolTip 验证（低优先级）

**目标文件**: Styles.xaml

- 验证 DataGridCell ToolTip="{Binding}" 在模板列中是否正常工作
- 确认 ToolTip 不阻塞 ContextMenu

## 验证步骤

1. `dotnet build -c Release` 编译通过
2. 启动应用→登录成功→所有页面可正常打开
3. 新建客户→列表显示→编辑→删除
4. 新建订单→选择客户→添加产品→保存
5. 产品管理→查看列表→编辑产品
6. 工作计划→新建排班→新建计划
7. 报表中心→正常打开→图表显示
8. 系统设置→字段管理→商圈/标签/产品分类 CRUD
9. 组织架构→查看/编辑员工
10. 应收账款→搜索→查看
11. 退出应用→日志正常

## 不在此计划内的
- 企业微信集成（需要真实企业微信账号测试）
- 高德地图导出功能
- 订单自动配送分配逻辑
- 数据库备份恢复功能（需要验证但非阻塞）
