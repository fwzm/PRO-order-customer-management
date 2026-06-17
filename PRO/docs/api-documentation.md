# PRO系统 API 文档

> **版本：** v2.0  
> **更新日期：** 2026年6月10日  
> **Base URL：** `http://localhost:5000/api`

---

## 目录

1. [概述](#概述)
2. [认证](#认证)
3. [订单管理](#订单管理)
4. [订单模板](#订单模板)
5. [客户管理](#客户管理)
6. [库存管理](#库存管理)
7. [收款管理](#收款管理)
8. [应收账款与账龄分析](#应收账款与账龄分析)
9. [数据质量检测](#数据质量检测)
10. [审计日志与状态追溯](#审计日志与状态追溯)
11. [报表导出](#报表导出)
12. [错误码与兼容性](#错误码与兼容性)

---

## 概述

### 基础信息
- **Base URL：** `http://localhost:5000/api`
- **认证方式：** Bearer Token (JWT)
- **数据格式：** JSON
- **字符编码：** UTF-8
- **日期时间：** ISO 8601 (`2026-06-09T10:00:00`)

### 请求头
```
Content-Type: application/json
Authorization: Bearer {your_token}
```

### 分页参数
所有列表接口支持：
- `pageIndex`：页码（从1开始）
- `pageSize`：每页条数（默认50，最大200）

### 分公司隔离规则
- 分公司用户只能访问本分公司数据
- 总部管理员（HEADQUARTERS_ADMIN）可访问所有分公司数据
- 创建数据时，`branchId` 自动设为当前用户分公司
- 跨分公司访问返回 `403 FORBIDDEN`

---

## 认证

### 登录
**POST** `/api/auth/login`

**权限要求：** 无

**请求体：**
```json
{
  "employeeNo": "string",
  "password": "string"
}
```

**响应（200）：**
```json
{
  "success": true,
  "data": {
    "employeeId": 1,
    "employeeNo": "EMP001",
    "name": "张三",
    "branchId": 1,
    "branchName": "北京分公司",
    "roleId": 1,
    "roleName": "管理员",
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "permissions": ["Customer.View", "Order.Create", ...]
  }
}
```

---

## 订单管理

> **Controller：** `OrdersController`  
> **Route：** `api/orders`  
> **废弃路由：** `api/order`（已标记 `[Obsolete]`，Swagger 中隐藏，请使用 `api/orders`）

### 获取订单列表
**GET** `/api/orders`

**权限：** `Order.View`

**查询参数：**
| 参数 | 类型 | 说明 |
|------|------|------|
| pageIndex | int | 页码 |
| pageSize | int | 每页条数 |
| keyword | string | 搜索关键词（订单号、客户名） |
| status | OrderStatus | 订单状态（Draft=0/Pending=1/Assigned=2/Delivering=3/Completed=4/Failed=5/Cancelled=6） |
| paymentStatus | PaymentStatus | 收款状态（Unpaid=0/PartialPaid=1/Paid=2/Legal=3） |

**分公司隔离：** 是

**响应（200）：**
```json
{
  "success": true,
  "data": {
    "items": [{
      "id": 1,
      "orderNo": "D202606090001",
      "customerId": 1,
      "customerName": "客户A",
      "branchId": 1,
      "branchName": "北京分公司",
      "totalAmount": 1000.00,
      "receivedAmount": 500.00,
      "discountAmount": 0,
      "paymentStatus": "PartialPaid",
      "status": "Pending",
      "deliveryPersonName": "配送员A",
      "deliveryAddress": "北京市朝阳区xxx",
      "createdAt": "2026-06-09T10:00:00",
      "validNextStatuses": ["Assigned", "Cancelled"]
    }],
    "totalCount": 100,
    "pageIndex": 1,
    "pageSize": 50
  }
}
```

---

### 获取订单详情
**GET** `/api/orders/{id}`

**权限：** `Order.View`  
**分公司隔离：** 是（403 如果订单不属于当前分公司）

**响应（200）：**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "orderNo": "D202606090001",
    "customerId": 1,
    "customerName": "客户A",
    "customerPhone": "13800138000",
    "branchId": 1,
    "deliveryAddress": "北京市朝阳区xxx",
    "totalAmount": 1000.00,
    "receivedAmount": 500.00,
    "discountAmount": 0,
    "paymentStatus": "PartialPaid",
    "status": "Pending",
    "items": [{
      "id": 1,
      "productId": 1,
      "productName": "产品A",
      "quantity": 10,
      "unitPrice": 100.00,
      "amount": 1000.00
    }],
    "modificationRecords": [{
      "id": 1,
      "content": "创建订单",
      "modifiedByName": "张三",
      "modifiedAt": "2026-06-09T10:00:00"
    }]
  }
}
```

---

### 创建订单
**POST** `/api/orders`

**权限：** `Order.Create`  
**分公司隔离：** 是（客户必须属于当前分公司）

**请求体：**
```json
{
  "customerId": 1,
  "deliveryAddress": "北京市朝阳区xxx",
  "deliveryLongitude": 116.4074,
  "deliveryLatitude": 39.9042,
  "deliveryTime": "2026-06-10T14:00:00",
  "deliveryPersonId": null,
  "remark": "尽快送达",
  "isDraft": false,
  "items": [
    {
      "productId": 1,
      "quantity": 10,
      "unitPrice": 100.00,
      "remark": "string"
    }
  ]
}
```

**响应（201）：**
```json
{
  "success": true,
  "data": 1,
  "message": "订单创建成功"
}
```

---

### 更新订单
**PUT** `/api/orders/{id}`

**权限：** `Order.Edit`  
**分公司隔离：** 是

**请求体：** 同创建订单

**响应（200）：**
```json
{
  "success": true,
  "message": "订单更新成功"
}
```

---

### 删除订单
**DELETE** `/api/orders/{id}`

**权限：** `Order.Delete`  
**分公司隔离：** 是  
**限制：** 已结算订单不可删除

---

### 克隆订单（复制订单）
**POST** `/api/orders/{id}/clone`

**权限：** `Order.Create`  
**分公司隔离：** 是

**说明：** 基于已有订单创建草稿副本，保留客户、产品、地址，备注自动标注"克隆自订单 xxx"

**响应（201）：**
```json
{
  "success": true,
  "data": 2,
  "message": "订单创建成功"
}
```

---

### 分配配送员
**POST** `/api/orders/{id}/assign`

**权限：** `Delivery.ManualAssign`  
**分公司隔离：** 是（配送员必须与订单同一分公司）

**请求体：**
```json
{
  "deliveryPersonId": 1
}
```

**响应（200）：**
```json
{
  "success": true,
  "message": "订单分配成功"
}
```

---

### 更新订单状态
**POST** `/api/orders/{id}/status`

**权限：** `Order.Edit`（取消需要 `Order.Cancel`）  
**分公司隔离：** 是

**请求体：**
```json
{
  "newStatus": 2,
  "reason": "客户要求"
}
```

**状态转换规则：**
| 当前状态 | 可转换到 |
|----------|----------|
| Draft(0) | Pending(1), Cancelled(6) |
| Pending(1) | Assigned(2), Cancelled(6) |
| Assigned(2) | Delivering(3), Pending(1), Cancelled(6) |
| Delivering(3) | Completed(4), Failed(5) |
| Failed(5) | Pending(1), Assigned(2), Cancelled(6) |

---

### 确认草稿
**POST** `/api/orders/{id}/confirm-draft`

**权限：** `Order.Edit`  
**分公司隔离：** 是

---

### 批量分配
**POST** `/api/orders/batch-assign`

**权限：** `Order.BatchAssign`  
**分公司隔离：** 是

**请求体：**
```json
{
  "orderIds": [1, 2, 3],
  "deliveryPersonId": 1
}
```

**响应（200）：**
```json
{
  "success": true,
  "data": {
    "totalCount": 3,
    "successCount": 2,
    "failureCount": 1,
    "items": [
      {
        "entityId": 1,
        "entityNo": "D202606100101",
        "success": true,
        "message": "分配成功",
        "failureReason": null
      },
      {
        "entityId": 3,
        "entityNo": "D202606100103",
        "success": false,
        "message": "分配失败",
        "failureReason": "已完成订单不能重新分配"
      }
    ]
  }
}
```

**说明：** 批量分配会逐条执行订单状态流转校验。合法订单继续分配，非法订单跳过并返回失败原因；失败订单不会被直接覆盖状态。

---

### 批量状态变更
**POST** `/api/orders/batch-status`

**权限：** `Order.BatchStatusChange`（取消需要 `Order.Cancel`）

**请求体：**
```json
{
  "orderIds": [1, 2, 3],
  "newStatus": 6,
  "reason": "客户取消"
}
```

**响应（200）：** 同 `BatchOperationResult`。返回 `totalCount`、`successCount`、`failureCount` 和逐条 `items` 明细。

**说明：** 批量确认草稿、批量取消、批量改状态均复用同一状态流转规则。取消操作必须填写原因。

### 批量操作结果模型
```json
{
  "totalCount": 3,
  "successCount": 2,
  "failureCount": 1,
  "items": [
    {
      "entityId": 1,
      "entityNo": "D202606100101",
      "success": true,
      "message": "处理成功",
      "failureReason": null
    }
  ]
}
```

---

### 获取待分配订单
**GET** `/api/orders/pending?branchId=1`

**权限：** `Order.View`  
**分公司隔离：** 是

---

### 自动分配订单
**POST** `/api/orders/auto-assign?branchId=1&algorithm=region_load_distance`

**权限：** `Delivery.AutoAssign`  
**分公司隔离：** 是  
**算法：** `region_load_distance`（默认）

---

### 获取最优分配方案（预览）
**GET** `/api/orders/optimal-assignment?branchId=1`

**权限：** `Delivery.View`  
**分公司隔离：** 是

---

## 订单模板

> **Controller：** `OrderTemplateController`  
> **Route：** `api/order-template`

### 获取模板列表
**GET** `/api/order-template?customerId=1`

**权限：** 登录即可（自动按当前用户筛选）  
**说明：** 返回当前用户创建的模板和公开模板

---

### 获取热门模板
**GET** `/api/order-template/popular?limit=10`

**权限：** 登录即可  
**说明：** 按使用次数降序排列

---

### 创建模板
**POST** `/api/order-template`

**请求体：**
```json
{
  "name": "每周补货模板",
  "customerId": 1,
  "deliveryAddress": "北京市朝阳区xxx",
  "isPublic": false,
  "items": [
    {
      "productId": 1,
      "quantity": 10,
      "unitPrice": 100.00
    }
  ]
}
```

---

### 从订单创建模板
**POST** `/api/order-template/from-order`

**请求体：**
```json
{
  "orderId": 1,
  "name": "每周补货模板",
  "isPublic": false
}
```

---

### 更新模板
**PUT** `/api/order-template/{id}`

**请求体：** 同创建模板

---

### 启用/停用模板
**POST** `/api/order-template/{id}/toggle`

**请求体：**
```json
{
  "isActive": false
}
```

**说明：** 停用后模板不在创建列表中显示（软删除）

---

### 删除模板
**DELETE** `/api/order-template/{id}`

**说明：** 软删除，将 `IsActive` 设为 false

---

### 从模板创建订单
**POST** `/api/order-template/{id}/create-order?deliveryTime=2026-06-10T14:00:00`

**权限：** `Order.Create`  
**说明：** 使用模板快速创建订单，模板使用次数+1

---

## 客户管理

> **Controller：** `CustomersController`  
> **Route：** `api/customers`

### 获取客户列表
**GET** `/api/customers`

**权限：** `Customer.View`  
**分公司隔离：** 是

**查询参数：**
| 参数 | 类型 | 说明 |
|------|------|------|
| pageIndex | int | 页码 |
| pageSize | int | 每页条数 |
| keyword | string | 搜索（名称、手机号） |
| customerType | CustomerType | Major=1/Sub=2 |
| branchId | int | 分公司ID |

**响应（200）：**
```json
{
  "success": true,
  "data": {
    "items": [{
      "id": 1,
      "name": "客户A",
      "customerNo": "K202606090001",
      "customerType": "Major",
      "phone": "13800138000",
      "address": "北京市朝阳区xxx",
      "branchId": 1,
      "branchName": "北京分公司",
      "orderCount": 10,
      "totalOrderAmount": 5000.00,
      "lastOrderDate": "2026-06-09",
      "receivableAmount": 1000.00,
      "createdAt": "2026-01-01T00:00:00"
    }],
    "totalCount": 100,
    "pageIndex": 1,
    "pageSize": 50
  }
}
```

---

### 获取客户详情
**GET** `/api/customers/{id}`

**权限：** `Customer.View`  
**分公司隔离：** 是

---

### 创建客户
**POST** `/api/customers`

**权限：** `Customer.Create`  
**分公司隔离：** 是（branchId 自动设为当前用户分公司）

**请求体：**
```json
{
  "name": "客户A",
  "customerType": 1,
  "phone": "13800138000",
  "province": "北京",
  "city": "北京",
  "district": "朝阳区",
  "address": "朝阳路xxx号",
  "legalPerson": "张三",
  "remark": "重要客户",
  "parentCustomerId": null
}
```

---

### 更新客户
**PUT** `/api/customers/{id}`

**权限：** `Customer.Edit`  
**分公司隔离：** 是

---

### 删除客户
**DELETE** `/api/customers/{id}`

**权限：** `Customer.Delete`  
**分公司隔离：** 是  
**限制：** 已关联订单或已绑定企微的客户不可删除

---

### 客户查重
**POST** `/api/customers/check-duplicates`

**权限：** `Customer.View`  
**分公司隔离：** 是（只显示当前分公司重复）

**请求体：**
```json
{
  "phone": "13800138000",
  "name": "客户A",
  "address": "北京市朝阳区xxx",
  "legalPerson": "张三"
}
```

**响应（200）：**
```json
{
  "success": true,
  "data": {
    "hasDuplicates": true,
    "duplicates": [{
      "id": 2,
      "name": "客户A",
      "phone": "13800138000",
      "address": "北京市朝阳区xxx",
      "matchType": "phone",
      "similarity": 1.0
    }]
  }
}
```

---

### 合并客户
**POST** `/api/customers/merge`

**权限：** `Customer.Merge`  
**分公司隔离：** 是（只能合并同分公司客户）

**请求体：**
```json
{
  "mainCustomerId": 1,
  "mergedCustomerIds": [2, 3]
}
```

---

## 库存管理

> **Controller：** `InventoryController`  
> **Route：** `api/inventory`

### 获取库存变动日志
**GET** `/api/inventory/change-logs`

**权限：** 登录即可

**查询参数：**
| 参数 | 类型 | 说明 |
|------|------|------|
| pageIndex | int | 页码 |
| pageSize | int | 每页条数 |
| productId | int | 产品ID |
| changeType | string | 变动类型（OrderCreate/OrderCancel/ManualAdjust） |
| startDate | DateTime | 开始日期 |
| endDate | DateTime | 结束日期 |

**响应（200）：**
```json
{
  "success": true,
  "data": {
    "items": [{
      "id": 1,
      "productId": 1,
      "productName": "矿泉水",
      "productSku": "SKU001",
      "beforeQuantity": 100,
      "afterQuantity": 90,
      "changeQuantity": -10,
      "changeType": "OrderCreate",
      "changeReason": "订单 D202606090001 创建",
      "relatedOrderId": 1,
      "relatedOrderNo": "D202606090001",
      "operatorId": 1,
      "createdAt": "2026-06-09T10:00:00"
    }],
    "totalCount": 100
  }
}
```

---

### 获取库存盘点数据
**GET** `/api/inventory/stock`

**权限：** 登录即可

**响应（200）：**
```json
{
  "success": true,
  "data": [{
    "productId": 1,
    "productName": "矿泉水",
    "productSku": "SKU001",
    "systemStock": 90,
    "physicalStock": null,
    "difference": 0,
    "lastChangeAt": "2026-06-09T10:00:00",
    "lastChangeReason": "订单创建"
  }]
}
```

---

### 手动调整库存
**POST** `/api/inventory/adjust`

**权限：** `Product.Edit`

**请求体：**
```json
{
  "productId": 1,
  "newQuantity": 95,
  "reason": "盘点调整"
}
```

---

### 批量盘点
**POST** `/api/inventory/batch-stock-take`

**权限：** `Product.Edit`

**请求体：**
```json
[
  {
    "productId": 1,
    "newQuantity": 95,
    "reason": "月度盘点"
  },
  {
    "productId": 2,
    "newQuantity": 480,
    "reason": "月度盘点"
  }
]
```

---

### 获取库存变动统计
**GET** `/api/inventory/stats?startDate=2026-06-01&endDate=2026-06-09`

**权限：** 登录即可  
**说明：** 统计指定时间范围内的库存变动概要和排行

---

## 收款管理

> **Controller：** `PaymentsController`  
> **Route：** `api/payments`

### 获取收款列表
**GET** `/api/payments?pageIndex=1&pageSize=20&branchId=1&customerId=1&startDate=2026-06-01&endDate=2026-06-09`

**权限：** 登录即可  
**分公司隔离：** 是

**响应（200）：**
```json
{
  "success": true,
  "data": {
    "items": [{
      "id": 1,
      "paymentNo": "PAY202606090001",
      "orderId": 1,
      "orderNo": "D202606090001",
      "customerId": 1,
      "customerName": "客户A",
      "amount": 500.00,
      "allocatedAmount": 500.00,
      "paymentMethod": "WeChat",
      "transactionNo": "WX202606090001",
      "reference": "部分收款",
      "remark": "备注",
      "receivedById": 1,
      "receivedByName": "张三",
      "paymentDate": "2026-06-09T10:00:00",
      "createdAt": "2026-06-09T10:00:00"
    }],
    "totalCount": 100
  }
}
```

---

### 登记收款
**POST** `/api/payments`

**权限：** 登录即可

**请求体：**
```json
{
  "orderId": 1,
  "customerId": 1,
  "amount": 500.00,
  "paymentMethod": "WeChat",
  "transactionNo": "WX202606090001",
  "reference": "部分收款",
  "remark": "备注",
  "paymentDate": "2026-06-09T10:00:00"
}
```

**说明：**
- `orderId` 可空（支持未绑定订单的预收款）
- `customerId` 必填
- 全额收款后订单收款状态自动更新

---

### 批量收款
**POST** `/api/payments/batch`

**请求体：**
```json
{
  "payments": [
    {
      "orderId": 1,
      "customerId": 1,
      "amount": 500.00,
      "paymentMethod": "WeChat",
      "paymentDate": "2026-06-09T10:00:00"
    },
    {
      "orderId": 2,
      "customerId": 1,
      "amount": 300.00,
      "paymentMethod": "Cash",
      "paymentDate": "2026-06-09T10:00:00"
    }
  ]
}
```

---

### 收款核销（多对多）
**POST** `/api/payments/allocate`

**权限：** 登录即可

**请求体：**
```json
{
  "paymentRecordId": 1,
  "allocations": [
    {
      "orderId": 1,
      "amount": 300.00
    },
    {
      "orderId": 2,
      "amount": 200.00
    }
  ]
}
```

**说明：** 一笔收款可分多笔核销到不同订单，核销总金额不能超过收款金额

---

### 获取核销明细
**GET** `/api/payments/{id}/allocations`

**权限：** 登录即可

---

### 获取订单收款记录
**GET** `/api/payments/order/{orderId}`

**权限：** 登录即可

---

### 获取客户收款记录
**GET** `/api/payments/customer/{customerId}`

**权限：** 登录即可

---

### 收款统计
**GET** `/api/payments/stats?branchId=1&startDate=2026-06-01&endDate=2026-06-09`

**权限：** 登录即可  
**分公司隔离：** 是

**响应（200）：**
```json
{
  "success": true,
  "data": {
    "totalReceived": 5000.00,
    "totalCount": 10,
    "cashAmount": 1000.00,
    "weChatAmount": 2000.00,
    "alipayAmount": 1000.00,
    "bankTransferAmount": 1000.00,
    "dailyPayments": [{
      "date": "2026-06-09",
      "amount": 500.00,
      "count": 1
    }],
    "topCustomers": [{
      "customerId": 1,
      "customerName": "客户A",
      "totalAmount": 2000.00,
      "paymentCount": 5
    }]
  }
}
```

---

### 删除收款（退款）
**DELETE** `/api/payments/{id}?reason=客户退款`

**权限：** 登录即可  
**说明：** 删除收款会回退订单收款金额

---

## 应收账款与账龄分析

> **Controller：** `ReceivablesController`  
> **Route：** `api/receivables`

### 获取应收账款列表
**GET** `/api/receivables?pageIndex=1&pageSize=50&branchId=1&customerId=1&salespersonId=1&minBalance=100&sortField=balance&sortOrder=desc`

**权限：** 登录即可  
**分公司隔离：** 是

**查询参数：**
| 参数 | 类型 | 说明 |
|------|------|------|
| pageIndex | int | 页码 |
| pageSize | int | 每页条数 |
| customerId | int | 客户ID |
| branchId | int | 分公司ID |
| salespersonId | int | 业务员ID |
| minBalance | decimal | 最低应收余额 |
| sortField | string | 排序字段（balance/aging等） |
| sortOrder | string | asc/desc |

---

### 获取客户应收明细
**GET** `/api/receivables/{customerId}`

**权限：** 登录即可

---

### 账龄分析
**GET** `/api/receivables/aging?branchId=1&customerId=1&salespersonId=1&asOfDate=2026-06-09`

**权限：** 登录即可  
**分公司隔离：** 是

**响应（200）：**
```json
{
  "success": true,
  "data": {
    "agingBuckets": [
      {
        "label": "0-30天",
        "minDays": 0,
        "maxDays": 30,
        "amount": 5000.00,
        "count": 10,
        "percentage": 50.0
      },
      {
        "label": "31-60天",
        "minDays": 31,
        "maxDays": 60,
        "amount": 3000.00,
        "count": 5,
        "percentage": 30.0
      },
      {
        "label": "61-90天",
        "minDays": 61,
        "maxDays": 90,
        "amount": 1500.00,
        "count": 3,
        "percentage": 15.0
      },
      {
        "label": "90天以上",
        "minDays": 91,
        "maxDays": null,
        "amount": 500.00,
        "count": 2,
        "percentage": 5.0
      }
    ],
    "totalReceivable": 10000.00,
    "totalCount": 20
  }
}
```

---

## 数据质量检测

> **Controller：** `DataQualityController`  
> **Route：** `api/data-quality`

### 获取数据质量报告
**GET** `/api/data-quality/report?branchId=1`

**权限：** 登录即可  
**分公司隔离：** 是（`branchId` 自动限制为当前用户分公司）

**响应（200）：**
```json
{
  "success": true,
  "data": {
    "generatedAt": "2026-06-09T10:00:00",
    "branchId": 1,
    "branchName": "北京分公司",
    "customerQuality": {
      "totalCount": 100,
      "duplicateCount": 5,
      "emptyPhoneCount": 10,
      "emptyAddressCount": 15,
      "inactiveCustomerCount": 20,
      "score": 75
    },
    "orderQuality": {
      "totalCount": 500,
      "staleDraftCount": 3,
      "longPendingCount": 5,
      "unsettledCompletedCount": 50,
      "score": 85
    },
    "productQuality": {
      "totalCount": 50,
      "duplicateSkuCount": 0,
      "negativeStockCount": 2,
      "score": 90
    },
    "overallScore": 83,
    "overallLevel": "Good"
  }
}
```

---

## 审计日志与状态追溯

> **Controller：** `AuditController`  
> **Route：** `api/audit`

### 查询审计日志
**GET** `/api/audit/logs?pageIndex=1&pageSize=20&entityType=Order&entityId=1&fieldName=Status&actionType=StatusChange&operatorId=1&branchId=1&startDate=2026-06-01&endDate=2026-06-09`

**权限：** 登录即可

**查询参数：**
| 参数 | 类型 | 说明 |
|------|------|------|
| pageIndex | int | 页码 |
| pageSize | int | 每页条数 |
| entityType | string | 对象类型（Order/Customer/Product/Payment/Inventory等） |
| entityId | int | 对象ID |
| fieldName | string | 变更字段名 |
| actionType | string | 操作类型（Create/Update/Delete/StatusChange/Allocate等） |
| operatorId | int | 操作人ID |
| branchId | int | 分公司ID |
| startDate | DateTime | 开始日期 |
| endDate | DateTime | 结束日期 |

**响应（200）：**
```json
{
  "success": true,
  "data": {
    "items": [{
      "id": 1,
      "entityType": "Order",
      "entityId": 1,
      "entityName": "D202606090001",
      "fieldName": "Status",
      "oldValue": "1",
      "newValue": "2",
      "actionType": "StatusChange",
      "reason": "分配配送员",
      "operatorId": 1,
      "operatorName": "张三",
      "branchId": 1,
      "createdAt": "2026-06-09T10:00:00"
    }],
    "totalCount": 100
  }
}
```

---

### 订单状态变更历史
**GET** `/api/audit/order/{orderId}/status-history`

**权限：** 登录即可

---

### 收款状态变更历史
**GET** `/api/audit/{entityType}/{entityId}/payment-status-history`

**权限：** 登录即可  
**entityType：** `Order` 或 `Delivery`

---

### 订单状态回滚
**POST** `/api/audit/order/rollback-status`

**权限：** 登录即可

**请求体：**
```json
{
  "entityId": 1,
  "entityType": "Order",
  "reason": "误操作回滚"
}
```

**说明：** 原因（reason）为必填

---

### 收款状态回滚
**POST** `/api/audit/payment/rollback-status`

**权限：** 登录即可

**请求体：**
```json
{
  "entityId": 1,
  "entityType": "Order",
  "reason": "误操作回滚"
}
```

---

## 报表导出

> **Controller：** `ExportController`  
> **Route：** `api/exports`

### 提交导出任务
**POST** `/api/exports`

**权限：** `Order.Export`

**请求体：**
```json
{
  "exportType": "Order",
  "filterCriteria": {
    "status": 4,
    "startDate": "2026-06-01",
    "endDate": "2026-06-09"
  }
}
```

**响应（200）：**
```json
{
  "success": true,
  "data": 1,
  "message": "导出任务已提交"
}
```

---

### 获取导出进度
**GET** `/api/exports/{jobId}/progress`

**权限：** `Order.Export`  
**分公司隔离：** 是

**响应（200）：**
```json
{
  "success": true,
  "data": {
    "jobId": 1,
    "status": "Processing",
    "totalRecords": 1000,
    "processedRecords": 500
  }
}
```

---

### 下载导出文件
**GET** `/api/exports/{jobId}/download`

**权限：** `Order.Export`  
**分公司隔离：** 是  
**响应：** CSV 文件流（`text/csv; charset=utf-8`）

---

### 获取导出历史
**GET** `/api/exports/history?pageIndex=1&pageSize=20`

**权限：** `Order.Export`  
**分公司隔离：** 是

---

### 重试失败的导出
**POST** `/api/exports/{jobId}/retry`

**权限：** `Order.Export`

---

### 取消导出
**POST** `/api/exports/{jobId}/cancel`

**权限：** `Order.Export`

---

## 移动端工作台

> **Controller：** `DashboardController`  
> **Route：** `api/dashboard`

### 获取移动端工作台数据
**GET** `/api/dashboard/workbench?forceRefresh=true`

**权限：** 登录即可  
**分公司隔离：** 是

**响应（200）：**
```json
{
  "success": true,
  "data": {
    "todayOrderCount": 5,
    "todayOrderAmount": 12000.00,
    "pendingOrderCount": 12,
    "deliveringOrderCount": 3,
    "completedOrderCount": 25,
    "unassignedOrderCount": 8,
    "draftOrderCount": 2,
    "overduePaymentCount": 3,
    "overduePaymentAmount": 5000.00,
    "pendingSettlementCount": 10,
    "pendingSettlementAmount": 35000.00,
    "todayDeliveryCount": 15,
    "deliveryFailedCount": 1,
    "overloadedDeliveryPersons": 2,
    "newCustomerCount": 3,
    "visitReminderCount": 5,
    "duplicateCustomerCount": 0,
    "syncFailedCount": 0,
    "recentErrorCount": 2,
    "alerts": [
      {
        "type": "Warning",
        "title": "待分配订单积压",
        "message": "当前有 12 个订单待分配配送员",
        "actionText": "去分配",
        "actionRoute": "order",
        "createdAt": "2026-06-17T09:00:00"
      }
    ],
    "recentOrders": [],
    "todayTasks": [],
    "shortcuts": []
  }
}
```

---

## 用户信息

> **Controller：** `UserController`  
> **Route：** `api/user`

### 获取当前用户信息
**GET** `/api/user/profile`

**权限：** 登录即可

**响应（200）：**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "name": "张三",
    "employeeNo": "ADMIN",
    "departmentName": "技术部",
    "departmentId": 1,
    "branchName": "总部",
    "branchId": 1,
    "roleName": "总部管理员",
    "phone": "13800138000",
    "status": 1,
    "statusName": "正常",
    "createdAt": "2026-01-01T00:00:00"
  }
}
```

---

## 健康检查

> 无需 Controller，由 `Program.cs` 中 `MapHealthChecks` 直接提供

### API 健康检查
**GET** `/health`

**权限：** 无

**响应（200）：**
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "database",
      "status": "Healthy",
      "description": null,
      "duration": "15ms"
    },
    {
      "name": "memory_cache",
      "status": "Healthy",
      "description": null,
      "duration": "1ms"
    },
    {
      "name": "background_services",
      "status": "Healthy",
      "description": null,
      "duration": "1ms"
    }
  ],
  "totalDuration": "17ms"
}
```

> **App 使用说明：** 移动 App 启动时调用 `/health` 检测 API 连通性。若不可达则显示"网络连接失败"提示。

---

## 错误码与兼容性

### HTTP 状态码
| 状态码 | 说明 |
|--------|------|
| 200 | 成功 |
| 201 | 创建成功 |
| 400 | 请求参数错误 |
| 401 | 未认证 |
| 403 | 权限不足或跨分公司访问 |
| 404 | 资源不存在 |
| 429 | 请求过于频繁（限流） |
| 500 | 服务器内部错误 |

### 错误响应格式
```json
{
  "success": false,
  "message": "订单不存在",
  "errors": []
}
```

### 分公司隔离错误
```json
{
  "success": false,
  "message": "无权访问其他分公司的订单"
}
```

### 数据格式约定
- **日期时间：** ISO 8601，存储为 `timestamp without time zone`
- **金额：** Decimal，精度由数据库决定
- **枚举：** 数据库中存储为整数，API 响应中可能为整数或字符串（取决于控制器）
- **分页：** 所有列表接口统一返回 `{ items, totalCount, pageIndex, pageSize }`

### 版本兼容性
- 废弃接口标记 `[Obsolete]` 并在 Swagger 中隐藏（`[ApiExplorerSettings(IgnoreApi = true)]`）
- 废弃路由：`api/order`（请使用 `api/orders`）、`api/customer`（请使用 `api/customers`）、`api/product`（请使用 `api/products`）

---

**文档版本：** v2.1  
**最后更新：** 2026年6月17日（新增 Dashboard、User、Health 移动端端点）
