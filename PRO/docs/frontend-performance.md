# PRO PWA 前端性能优化报告

> **日期：** 2026-06-17  
> **版本：** v1.0  
> **关联：** `src/PRO.Admin.Web/`

---

## 1. 问题诊断

### 优化前问题
- **Element Plus 全量引入**：`main.js` 中 `import ElementPlus from 'element-plus'` 全量引入 ~800KB+（gzip 后）
- **Icons 全量注册**：`import * as ElementPlusIconsVue from '@element-plus/icons-vue'` 并循环注册 ~300KB+
- **ECharts 全量导入**：`import * as echarts from 'echarts'` 静默打包 ~950KB+（gzip 后 ~330KB），且首屏即加载
- **图表页面未做懒加载**：Dashboard、Report、Forecast 即使非首屏也强制打包 ECharts

### 目标
- 首屏 JS 总大小 <300KB（gzip）
- Element Plus chunk < 200KB（gzip）
- ECharts 仅图表页动态加载，不进首屏 bundle

---

## 2. 已实施优化

### 2.1 Element Plus 按需引入（已生效）

**修改文件：** `src/PRO.Admin.Web/src/main.js`

```diff
- import ElementPlus from 'element-plus'
- import 'element-plus/dist/index.css'
- import * as ElementPlusIconsVue from '@element-plus/icons-vue'
- for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
-   app.component(key, component)
- }
- app.use(ElementPlus, { locale: undefined })

+ import 'element-plus/dist/index.css'
```

**说明：**
- `unplugin-vue-components` + `ElementPlusResolver` 已经在 `vite.config.js` 中配置，自动按需导入组件
- `unplugin-auto-import` + `ElementPlusResolver` 自动按需导入 API（ElMessage、ElLoading 等）
- 移除全量注册后，仅需保留样式 `import 'element-plus/dist/index.css'`（约 200KB gzip）
- Icons 由 `unplugin-vue-components` 按需从 `@element-plus/icons-vue` 自动导入

**验证方法：** 构建后检查 `dist/stats.html`，`element-plus` chunk 应大幅缩小。

### 2.2 ECharts 按需加载（动态 import）

**修改文件：**
- `src/PRO.Admin.Web/src/views/DashboardView.vue`
- `src/PRO.Admin.Web/src/views/forecast/ForecastView.vue`
- `src/PRO.Admin.Web/src/views/reports/ReportCenter.vue`

```diff
- import * as echarts from 'echarts'
+ let echartsModule = null
+ async function loadEcharts() {
+   if (!echartsModule) echartsModule = await import('echarts')
+   return echartsModule
+ }
```

**效果：**
- ECharts (~950KB) 不再进入首屏 bundle，仅当用户导航到 Dashboard/Report/Forecast 时动态加载
- 每个图表页共享同一个动态 chunk（`echarts` 模块仅加载一次）
- 首屏无图表页面（如登录页、列表页）完全不受 ECharts 体积影响

### 2.3 Vite 手动分包策略

**修改文件：** `src/PRO.Admin.Web/vite.config.js`

```js
build: {
  rollupOptions: {
    output: {
      manualChunks: {
        'element-plus': ['element-plus'],
        'echarts': ['echarts', 'vue-echarts'],
      },
    },
  },
},
```

**分块策略：**

| chunk 名 | 内容 | 预期大小 (gzip) | 加载方式 |
|----------|------|----------------|---------|
| `vendor-vue` | vue, vue-router, pinia | ~60KB | 首屏（sync） |
| `element-plus` | Element Plus 组件（按需） | ~180KB | 首屏（sync） |
| `echarts` | ECharts + vue-echarts | ~330KB | 图表页（dynamic import） |
| `index` | 业务代码 + 路由 | ~80KB | 首屏（sync） |

### 2.4 Bundle 分析报告

**配置：** 安装 `rollup-plugin-visualizer`，构建后生成 `dist/stats.html`

```bash
npm run build
# 查看分析报告: dist/stats.html
```

打开报告可查看各 chunk 的体积树状图，验证优化效果。

---

## 3. 优化效果（预期）

| 指标 | 优化前 | 优化后 | 减少 |
|------|--------|--------|------|
| **首屏 JS (gzip)** | ~600KB | ~280KB | **-53%** |
| **Element Plus (gzip)** | ~350KB | ~180KB | **-49%** |
| **ECharts (gzip)** | 首屏加载 ~330KB | 延迟动态加载 | 首屏 0KB |
| **总构建产物 (gzip)** | ~1.2MB | ~750KB | **-37%** |
| **首次内容绘制 (FCP)** | ~2.5s | ~1.2s | **-52%** |

> **注意：** 上述为理论估算值。实际体积受业务代码规模影响，建议 `npm run build` 后查看 `dist/stats.html` 获取准确数值。

---

## 4. 未达标说明

如 `npm run build` 后 chunk 体积未达标：

1. **Element Plus chunk > 200KB**：检查是否有组件未被 unplugin 正确 tree-shake。可在 `vite.config.js` 中启用 `importStyle: 'css'` 确保 CSS 单独分块
2. **ECharts 仍出现在首屏 bundle**：检查是否仍有文件使用 `import * as echarts from 'echarts'`（非动态），全局搜索 `import.*echarts.*from`
3. **第三方库冗余**：检查 `dayjs` locale、vue-echarts 是否可进一步按需

---

## 5. 后续优化建议

| 优化项 | 优先级 | 预期收益 | 工作量 |
|--------|--------|---------|--------|
| **图片懒加载** | P1 | -100KB | 小 |
| **路由级代码分割完善** | P1 | -50KB/路由 | 小 |
| **Gzip/Brotli 压缩服务端** | P2 | -60% | 中（需 nginx 配置） |
| **Service Worker 预缓存策略** | P2 | FCP -30% | 中 |
| **dayjs 按需 locale** | P3 | -30KB | 小 |
| **移除未使用的 CSS** | P3 | -50KB | 中 |

---

## 6. 验证命令

```bash
cd src/PRO.Admin.Web

# 安装依赖（含新增 visualizer）
npm install

# 安全审计
npm audit

# 构建 + 生成分析报告
npm run build

# 查看报告
# 打开 dist/stats.html
```

**验收标准：**
- `npm audit` 零高危漏洞
- `npm run build` 零报错
- PWA 安装不受影响（检查 `dist/index.html` + manifest）
- 首屏无 echarts 强制加载（开发者工具 Network 标签验证）

---

**文档版本：** v1.0  
**下次审查：** 每次重大构建后
