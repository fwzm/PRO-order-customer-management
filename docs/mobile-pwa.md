# PRO 手机端 PWA 部署说明

> 适用版本：v2.0.0  
> 适用对象：管理员、实施人员、运维人员

## 定位

PRO 手机端当前采用 PWA 形式交付。它不是原生 APK 或 App Store 应用，但可以在 Android、iPhone、iPad 和桌面浏览器中访问，并支持添加到主屏幕。

手机端通过 `PRO.WebApi` 读写业务数据。只要桌面端、WebApi 和手机端连接同一个 PostgreSQL 数据库或同一套 WebApi，客户、订单、库存、收款和应收数据就是互通的。

```text
Windows 桌面端  ─┐
                ├── PostgreSQL / WebApi ── 手机端 PWA
WebApi 管理端   ─┘
```

## 构建

```powershell
cd src/PRO.Admin.Web
npm ci
npm run build
```

构建产物位于：

```text
src/PRO.Admin.Web/dist/
```

Release 中的 `PRO-mobile-pwa-v2.0.0.zip` 已包含构建后的 `dist` 内容，可直接部署到 Web 服务器。

## 部署方式

### 推荐：与 WebApi 同域部署

将 `dist` 目录作为站点静态文件根目录，并把以下路径反向代理到 WebApi：

| 路径 | 目标 |
|------|------|
| `/api` | `PRO.WebApi` |
| `/health` | `PRO.WebApi` |

这种方式最简单，浏览器不会遇到跨域问题。

### Nginx 示例

```nginx
server {
    listen 443 ssl;
    server_name pro.example.com;

    root /opt/pro/mobile/dist;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location /api/ {
        proxy_pass http://127.0.0.1:8080/api/;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }

    location /health {
        proxy_pass http://127.0.0.1:8080/health;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### IIS 示例

1. 创建网站，物理路径指向 `dist`。
2. 安装 URL Rewrite。
3. 将非文件请求重写到 `index.html`。
4. 将 `/api` 和 `/health` 反向代理到 WebApi。

## 手机安装

### Android / Chrome

1. 用 Chrome 打开系统地址。
2. 点击右上角菜单。
3. 选择 `安装应用` 或 `添加到主屏幕`。
4. 桌面出现 `PRO系统` 图标后即可打开。

### iPhone / Safari

1. 用 Safari 打开系统地址。
2. 点击分享按钮。
3. 选择 `添加到主屏幕`。
4. 从桌面图标打开。

## 注意事项

- 正式环境请使用 HTTPS；多数手机浏览器要求 HTTPS 才能安装 PWA。
- 手机端依赖 WebApi，离线时只能使用有限缓存，不能保证新订单、收款等写操作完全离线可用。
- 图表和管理页在手机屏幕上可用，但复杂批量操作仍建议在电脑端完成。
- 企业微信内打开时，部分浏览器能力受企业微信容器限制；需要安装到桌面时建议使用系统浏览器。

## 后续原生 App 路线

如果需要真正的安卓 APK 或 iOS App，可复用现有 WebApi，单独开发：

| 方案 | 优点 | 代价 |
|------|------|------|
| .NET MAUI | 与现有 .NET 技术栈一致，适合内部应用 | UI 适配和打包签名需要额外维护 |
| Flutter | 移动端体验好，生态成熟 | 需要新增 Dart/Flutter 技术栈 |
| 原生 Android/iOS | 性能和系统能力最好 | 开发和维护成本最高 |

当前建议先使用 PWA 满足手机访问和互通需求，等核心业务流程稳定后再投入原生 App。
