# Tasks

## Phase 1: 全局 UI 框架改造（基础层，无依赖）
- [ ] Task 1: 美化侧边栏滚动条 + 顶部标签页可关闭/居中/色块
  - [ ] 修改 Styles.xaml ScrollBar 样式为圆角窄条
  - [ ] 实现标签页关闭按钮、文字居中、选中色块
  - [ ] 删除右下角个人菜单，优化布局
  - [ ] 验证所有列表页滚动丝滑

## Phase 2: 客户模块重构（核心模块）
- [ ] Task 2: 客户列表字段精简 + 客户担当
  - [ ] CustomerListView.xaml 重列字段：名称、地址、创建时间、客户担当
  - [ ] CustomerListViewModel 查询增加 CustomerManager 关联
  - [ ] CustomerListItem DTO 增加 CustomerManagerName
  - [ ] 移除默认只显示大客户的过滤

- [ ] Task 3: 客户详情页重构为真正详情页
  - [ ] CustomerDetailWindow.xaml 全新布局：基本信息 + 担当变更历史 + 关联客户 + Tab(订单/拜访/商机/价格)
  - [ ] CustomerDetailViewModel 完善：加载完整关联数据、历史记录
  - [ ] 详情页内直接切换编辑模式（不弹窗）
  - [ ] 所有子记录显示创建人/修改人/创建时间/修改时间

- [ ] Task 4: 客户选择器统一复用
  - [ ] CustomerPickerWindow.xaml 重构：名称、编号、客户担当三列
  - [ ] 增加新建客户按钮（在选择器内打开新建）
  - [ ] 增加客户详情按钮（双击打开详情）
  - [ ] 所有调用处（订单编辑、合并客户等）统一使用

## Phase 3: 订单模块重构
- [ ] Task 5: 订单二级菜单 + 新建/草稿/列表
  - [ ] MainViewModel 订单分类下增加3个子项
  - [ ] 新建订单、草稿订单 ViewModel/View
  - [ ] 验证导航切换正常

- [ ] Task 6: 订单编辑重构（客户搜索/产品明细/优惠）
  - [ ] 修复 OrderEditWindow 客户搜索模糊匹配
  - [ ] 产品明细改为可无限添加行模式
  - [ ] 每行增加优惠按钮（打折/改价）
  - [ ] 收款信息重构：订单总金额(只读)+优惠金额(可改)+应收金额+收款金额
  - [ ] OrderItem 实体新增 DiscountType, DiscountValue 字段

## Phase 4: 产品模块
- [ ] Task 7: 产品价格字段重组
  - [ ] Product 实体：移除 Price/CostPrice，新增 AverageSalePrice
  - [ ] 更新 ProductEditView/ViewModel
  - [ ] 产品列表调整列
  - [ ] 产品详情补全 SKU
  - [ ] 数据库迁移脚本

## Phase 5: 财务应收 + 库存
- [ ] Task 8: 应收账款优化
  - [ ] 订单号列宽自适应（按18位优化）
  - [ ] 新增客户担当字段
  - [ ] 新增搜索过滤功能

- [ ] Task 9: 库存盘点 + 仓库管理
  - [ ] 新增 Warehouse 实体
  - [ ] InventoryView 增加仓库选择
  - [ ] 盘点按仓库进行
  - [ ] 列宽自适应

## Phase 6: 拜访/商机
- [ ] Task 10: 拜访/商机字段调整
  - [ ] 拜访人改为客户担当
  - [ ] 方式字段隐藏(列表)
  - [ ] 内容字段隐藏(列表)
  - [ ] 列表增加详情按钮
  - [ ] 详情页含完整历史记录

## Phase 7: 工作计划 + 报表/预测
- [ ] Task 11: 工作计划功能修复
  - [ ] 修复新建排班 CreateScheduleCommand
  - [ ] 修复新建计划 CreatePlanCommand
  - [ ] 排班界面布局调整（周一~周日横向 + 员工纵向）

- [ ] Task 12: 报表中心与智能预测修复
  - [ ] 修复 ReportCenterView 打不开的问题
  - [ ] 修复 PredictionDashboardView 打不开的问题
  - [ ] 图表字段完整显示

## Phase 8: 系统设置 + 组织架构
- [ ] Task 13: 系统设置重构
  - [ ] 合并应用设置和高级设置为单一页面
  - [ ] 新增"字段管理"二级菜单
  - [ ] 迁移商圈/标签/产品分类/收款状态等到字段管理
  - [ ] 城市选择器：AutoComplete + 手动输入

- [ ] Task 14: 组织架构精简
  - [ ] DepartmentView 直接编辑/新增/删除
  - [ ] EmployeeView 直接编辑/新增/删除
  - [ ] 移除弹窗模式
  - [ ] 数据隔离：仅本分公司

## Phase 9: 企微 + 筛选器中文
- [ ] Task 15: 企微SCRM精简 + 筛选器中文化
  - [ ] WeChatScrmView 移除关联客户字段
  - [ ] 所有日期筛选器（今日/本周/本月）功能正常
  - [ ] 所有状态筛选器中文化

# Task Dependencies
- Task 2-4 依赖 Task 1
- Task 5-6 依赖 Task 1,4
- Task 7 依赖 Task 1
- Task 8-9 依赖 Task 1
- Task 10-12 依赖 Task 1
- Task 13-14 依赖 Task 1
- Task 15 依赖 Task 1
- Phase 2-9 可并行执行（各自独立模块）
