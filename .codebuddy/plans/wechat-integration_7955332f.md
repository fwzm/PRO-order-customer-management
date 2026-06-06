---
name: wechat-integration
overview: 根据开发手册完善 PRO 系统的企业微信功能，包括企业微信配置、Webhook 管理、组织架构同步和消息推送。
todos:
  - id: create-wechat-config
    content: 创建 WeChatConfig.cs 企业微信配置模型
    status: completed
  - id: create-webhook-model
    content: 创建 WebhookItem.cs Webhook数据模型
    status: completed
    dependencies:
      - create-wechat-config
  - id: create-wechat-service
    content: 创建 WeChatService.cs 企业微信服务实现
    status: completed
    dependencies:
      - create-wechat-config
      - create-webhook-model
  - id: update-viewmodel-observables
    content: 更新 HeadquartersAdminViewModel 添加企业微信相关ObservableProperty
    status: completed
    dependencies:
      - create-wechat-service
  - id: update-viewmodel-commands
    content: 更新 HeadquartersAdminViewModel 实现企业微信命令
    status: completed
    dependencies:
      - update-viewmodel-observables
  - id: update-xaml-bindings
    content: 更新 HeadquartersAdminView.xaml 数据绑定
    status: completed
    dependencies:
      - update-viewmodel-commands
  - id: register-service
    content: 在 App.xaml.cs 注册 WeChatService 到 DI 容器
    status: completed
    dependencies:
      - create-wechat-service
---

## 产品概述

PRO订单与客户管理系统 - 企业微信功能完善

## 核心需求

根据需求文档完善企业微信集成功能，实现以下能力：

1. **企业微信配置管理**

- 企业ID、应用Secret、应用ID配置
- Webhook地址配置
- 配置信息加密存储，仅总部管理员可查看/修改

2. **Webhook管理**

- 支持添加无限个Webhook
- 列表展示（名称、URL、最后测试时间、状态）
- 支持备注、编辑、测试功能
- 向企业微信群推送消息

3. **组织架构与企业微信同步**

- 同步频率由总部管理配置（每日0点以后同步一次）
- 同步内容可配置
- 同步日志查看

4. **企业微信双向同步**

- 企业微信新增客户自动同步至系统
- 系统编辑客户信息同步至企业微信
- 联网后自动同步数据

5. **消息通知**

- 工作计划变更通知员工
- 订单状态变更通知相关人员
- 结算完成后通知管理员

## 技术栈

- .NET 8.0 / C#
- WPF (Windows Presentation Foundation)
- Entity Framework Core + SQLite
- CommunityToolkit.Mvvm (MVVM模式)
- HttpClient (企业微信API调用)
- Newtonsoft.Json (JSON序列化)

## 实现架构

采用分层架构，在Infrastructure层实现企业微信服务：

```
PRO.Infrastructure/
├── WeChat/
│   ├── WeChatConfig.cs          # 企业微信配置模型
│   ├── WeChatService.cs         # 企业微信服务实现
│   └── Models/
│       ├── WebhookItem.cs       # Webhook配置项
│       └── SyncLogItem.cs       # 同步日志项
```

## 关键设计决策

### 1. 配置加密存储

企业微信配置（企业ID、Secret等敏感信息）通过现有的EncryptionService加密存储在LocalSettings表中，确保安全。

### 2. Webhook管理

- Webhook配置存储在LocalSettings表中，JSON格式序列化
- 支持触发条件配置（如订单创建、状态变更等）
- 测试功能通过发送测试消息验证Webhook有效性

### 3. 同步机制

- 后台定时任务执行同步（基于Timer）
- 同步状态记录到本地数据库
- 冲突处理策略：时间优先/总部优先/手动处理

### 4. API调用

使用HttpClient调用企业微信API：

- 通讯录同步：https://qyapi.weixin.qq.com/cgi-bin/user/simplelist
- 发送消息：https://qyapi.weixin.qq.com/cgi-bin/message/send
- 获取企业token：https://qyapi.weixin.qq.com/cgi-bin/gettoken