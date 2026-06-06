---
name: Hide NocoDB Database Tab
overview: 隐藏"数据库"标签页，使所有用户无法查看和编辑 NocoDB 连接配置信息，同时保留测试连接功能入口。
todos:
  - id: remove-database-tab
    content: 从 HeadquartersAdminView.xaml 删除"数据库" TabItem
    status: pending
  - id: add-test-button
    content: 在"同步配置"标签页添加 NocoDB 连接测试区域
    status: pending
    dependencies:
      - remove-database-tab
---

## 用户需求

- 完全隐藏"数据库"标签页，禁止所有人访问
- 保留测试连接功能供管理员使用
- 数据库配置信息不可查看、不可编辑

## 现有代码位置

- **XAML**: `d:/PRO/src/PRO.Desktop/Views/HeadquartersAdminView.xaml` 第 188-230 行（数据库标签页）
- **ViewModel**: `d:/PRO/src/PRO.Desktop/ViewModels/OtherViewModels.cs` 第 612-1090 行

## 问题分析

当前"数据库"标签页包含：

1. API地址输入框（已绑定 NocoDBApiUrl）
2. API密钥输入框（PasswordBox，但不支持绑定）
3. 保存按钮
4. 测试连接按钮

需要完全隐藏此标签页，同时保留测试连接功能。

## 技术方案

- **修改文件**: `d:/PRO/src/PRO.Desktop/Views/HeadquartersAdminView.xaml`
- **操作**: 移除"数据库" TabItem（第188-230行）
- **替代方案**: 在"同步配置"标签页添加一个独立的"NocoDB连接测试"按钮区域

## 实现步骤

1. 删除 HeadquartersAdminView.xaml 中的数据库 TabItem（TabItem Header="数据库"）
2. 在"同步配置"标签页添加新的测试区域，包含连接状态显示和测试按钮
3. ViewModel 无需修改，保留 TestNocoDBConnectionAsync 方法