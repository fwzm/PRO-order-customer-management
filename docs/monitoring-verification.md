# PRO系统监控与日志验证清单

> **版本：** v1.0  
> **更新日期：** 2026年6月11日

---

## 一、Prometheus 指标验证

### 1.1 API 请求耗时
- **指标名称：** `http_request_duration_ms`
- **验证方法：** 访问 `/metrics`，检查指标是否存在
- **预期结果：** 指标存在，包含 endpoint 和 quantile 标签
- **验证命令：**
  ```bash
  curl http://localhost:5000/metrics | grep http_request_duration_ms
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

### 1.2 错误请求统计
- **指标名称：** `http_errors_total`
- **验证方法：** 访问 `/metrics`，检查指标是否存在
- **预期结果：** 指标存在，包含 endpoint 和 status_code 标签
- **验证命令：**
  ```bash
  curl http://localhost:5000/metrics | grep http_errors_total
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

### 1.3 权限拒绝统计
- **指标名称：** `permission_denied_total`
- **验证方法：** 访问 `/metrics`，检查指标是否存在
- **预期结果：** 指标存在
- **验证命令：**
  ```bash
  curl http://localhost:5000/metrics | grep permission_denied_total
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

### 1.4 分公司隔离拒绝统计
- **指标名称：** `branch_isolation_denied_total`
- **验证方法：** 访问 `/metrics`，检查指标是否存在
- **预期结果：** 指标存在
- **验证命令：**
  ```bash
  curl http://localhost:5000/metrics | grep branch_isolation_denied_total
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

### 1.5 导出任务统计
- **指标名称：** `export_success_total`, `export_failed_total`
- **验证方法：** 访问 `/metrics`，检查指标是否存在
- **预期结果：** 指标存在
- **验证命令：**
  ```bash
  curl http://localhost:5000/metrics | grep export_
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

### 1.6 缓存命中率
- **指标名称：** `cache_hits_total`, `cache_misses_total`
- **验证方法：** 访问 `/metrics`，检查指标是否存在
- **预期结果：** 指标存在，可计算命中率
- **验证命令：**
  ```bash
  curl http://localhost:5000/metrics | grep cache_
  ```
- **命中率计算：**
  ```
  命中率 = cache_hits_total / (cache_hits_total + cache_misses_total) * 100%
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** 命中率：___%

---

## 二、日志脱敏验证

### 2.1 手机号脱敏
- **验证方法：** 执行包含手机号的操作，检查日志
- **预期结果：** 手机号显示为 138****8000
- **验证命令：**
  ```bash
  grep -i "138\*\*\*\*" logs/webapi-*.log | head -5
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

### 2.2 密码脱敏
- **验证方法：** 执行登录操作，检查日志
- **预期结果：** 密码未明文显示
- **验证命令：**
  ```bash
  grep -i "password" logs/webapi-*.log | grep -v "password.*\*\*\*" | head -5
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

### 2.3 Token 脱敏
- **验证方法：** 执行需要Token的操作，检查日志
- **预期结果：** Token 未明文显示
- **验证命令：**
  ```bash
  grep -i "token" logs/webapi-*.log | grep -v "token.*\*\*\*" | head -5
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

### 2.4 JWT Key 脱敏
- **验证方法：** 检查启动日志
- **预期结果：** JWT Key 未明文显示
- **验证命令：**
  ```bash
  grep -i "jwt.*key" logs/webapi-*.log | grep -v "key.*\*\*\*" | head -5
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

### 2.5 Authorization Header 脱敏
- **验证方法：** 检查请求日志
- **预期结果：** Authorization Header 未明文显示
- **验证命令：**
  ```bash
  grep -i "authorization" logs/webapi-*.log | grep -v "authorization.*\*\*\*" | head -5
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

## 三、健康检查验证

### 3.1 数据库连接检查
- **验证方法：** 访问 `/health`，检查 database 状态
- **预期结果：** 状态为 Healthy
- **验证命令：**
  ```bash
  curl http://localhost:5000/health | jq '.checks[] | select(.name=="database")'
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

### 3.2 缓存服务检查
- **验证方法：** 访问 `/health`，检查 memory_cache 状态
- **预期结果：** 状态为 Healthy
- **验证命令：**
  ```bash
  curl http://localhost:5000/health | jq '.checks[] | select(.name=="memory_cache")'
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

### 3.3 后台服务检查
- **验证方法：** 访问 `/health`，检查 background_services 状态
- **预期结果：** 状态为 Healthy
- **验证命令：**
  ```bash
  curl http://localhost:5000/health | jq '.checks[] | select(.name=="background_services")'
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

## 四、响应压缩验证

### 4.1 Gzip 压缩
- **验证方法：** 发送请求，设置 Accept-Encoding: gzip
- **预期结果：** 响应包含 Content-Encoding: gzip
- **验证命令：**
  ```bash
  curl -H "Accept-Encoding: gzip" -I http://localhost:5000/api/orders
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

### 4.2 Brotli 压缩
- **验证方法：** 发送请求，设置 Accept-Encoding: br
- **预期结果：** 响应包含 Content-Encoding: br
- **验证命令：**
  ```bash
  curl -H "Accept-Encoding: br" -I http://localhost:5000/api/orders
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

## 五、CORS 配置验证

### 5.1 生产环境 CORS
- **验证方法：** 检查 appsettings.Production.json
- **预期结果：** AllowedOrigins 不包含 *
- **验证命令：**
  ```bash
  grep -A5 "AllowedOrigins" appsettings.Production.json
  ```
- **结果：** [ ] 通过 [ ] 失败
- **记录：** ___

---

## 六、验证结果汇总

| 编号 | 验证项 | 结果 | 备注 |
|------|--------|------|------|
| 1.1 | API 请求耗时 | | |
| 1.2 | 错误请求统计 | | |
| 1.3 | 权限拒绝统计 | | |
| 1.4 | 分公司隔离拒绝统计 | | |
| 1.5 | 导出任务统计 | | |
| 1.6 | 缓存命中率 | | |
| 2.1 | 手机号脱敏 | | |
| 2.2 | 密码脱敏 | | |
| 2.3 | Token 脱敏 | | |
| 2.4 | JWT Key 脱敏 | | |
| 2.5 | Authorization Header 脱敏 | | |
| 3.1 | 数据库连接检查 | | |
| 3.2 | 缓存服务检查 | | |
| 3.3 | 后台服务检查 | | |
| 4.1 | Gzip 压缩 | | |
| 4.2 | Brotli 压缩 | | |
| 5.1 | 生产环境 CORS | | |

---

## 七、验证结论

**验证人员：** ________________

**验证日期：** ________________

**验证结论：**
- [ ] 全部通过
- [ ] 部分通过，需要修复
- [ ] 不通过，需要调查

**需要修复的问题：**

1. ________________________________
2. ________________________________
3. ________________________________
