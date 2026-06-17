# PRO系统部署文档

> **版本：** v1.0  
> **更新日期：** 2026年6月11日

---

## 一、环境要求

### 服务器环境
- **操作系统：** Windows Server 2016+ 或 Linux（Docker部署）
- **运行时：** .NET 8.0 Runtime
- **数据库：** PostgreSQL 12+
- **内存：** 最低 4GB，推荐 8GB
- **磁盘：** 最低 50GB，推荐 100GB

### 客户端环境
- **操作系统：** Windows 10/11
- **运行时：** .NET 8.0 Desktop Runtime
- **内存：** 最低 2GB，推荐 4GB
- **分辨率：** 推荐 1920x1080

### 手机端环境
- **访问方式：** 现代手机浏览器或安装到主屏幕的 PWA
- **推荐浏览器：** Android Chrome、iOS Safari
- **网络：** 可访问 WebApi 服务
- **生产要求：** HTTPS，用于启用 PWA 安装和缓存能力

---

## 二、数据库准备

### 1. 创建数据库
```sql
CREATE DATABASE pro_db
    WITH 
    OWNER = postgres
    ENCODING = 'UTF8'
    CONNECTION LIMIT = -1;
```

### 2. 创建用户（可选）
```sql
CREATE USER pro_user WITH PASSWORD 'your_password';
GRANT ALL PRIVILEGES ON DATABASE pro_db TO pro_user;
```

### 3. 执行迁移
```bash
# 设置连接字符串
export PRO_ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=pro_db;Username=pro_user;Password=your_password"

# 执行迁移
dotnet ef database update --context ProDbContext
```

### 4. 执行索引优化
```bash
psql -h localhost -U pro_user -d pro_db -f scripts/phase3_indexes.sql
```

---

## 三、WebApi 部署

### 方式一：Windows 服务

#### 1. 发布应用
```bash
dotnet publish src/PRO.WebApi -c Release -r win-x64 --self-contained -o C:\PRO\WebApi
```

#### 2. 安装服务
```powershell
# 使用 sc 命令
sc create PROWebApi binPath="C:\PRO\WebApi\PRO.WebApi.exe" start=auto
sc description PROWebApi "PRO WebApi Service"

# 或使用 NSSM
nssm install PROWebApi C:\PRO\WebApi\PRO.WebApi.exe
nssm set PROWebApi AppDirectory C:\PRO\WebApi
nssm set PROWebApi DisplayName PRO WebApi
nssm set PROWebApi Description PRO WebApi Service
nssm set PROWebApi Start SERVICE_AUTO_START
```

#### 3. 配置环境变量
```powershell
# 设置连接字符串
[Environment]::SetEnvironmentVariable("PRO_ConnectionStrings__DefaultConnection", "Host=localhost;Port=5432;Database=pro_db;Username=pro_user;Password=your_password", "Machine")

# 设置JWT Key
[Environment]::SetEnvironmentVariable("PRO_Jwt__Key", "your-super-secret-key-at-least-32-chars", "Machine")
```

#### 4. 启动服务
```powershell
sc start PROWebApi
```

#### 5. 验证服务
```bash
curl http://localhost:5000/health
```

---

### 方式二：Docker 部署

#### 1. 构建镜像
```bash
docker build -t pro-webapi -f src/PRO.WebApi/Dockerfile .
```

#### 2. 使用 Docker Compose
```bash
# 修改 docker-compose.yml 中的环境变量
# 启动服务
docker-compose up -d

# 查看日志
docker-compose logs -f webapi

# 停止服务
docker-compose down
```

#### 3. 单独运行 Docker
```bash
docker run -d \
  --name pro-webapi \
  -p 5000:8080 \
  -e PRO_ConnectionStrings__DefaultConnection="Host=postgres;Port=5432;Database=pro_db;Username=postgres;Password=postgres" \
  -e PRO_Jwt__Key="your-super-secret-key-at-least-32-chars" \
  pro-webapi
```

---

### 方式三：IIS 部署

#### 1. 发布应用
```bash
dotnet publish src/PRO.WebApi -c Release -o C:\PRO\WebApi
```

#### 2. 配置 IIS
1. 安装 ASP.NET Core Hosting Bundle
2. 创建网站，指向发布目录
3. 配置应用程序池为"无托管代码"
4. 配置绑定（端口、域名）

#### 3. 配置 web.config
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet" arguments=".\PRO.WebApi.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess" />
    </system.webServer>
  </location>
</configuration>
```

---

## 四、Desktop 客户端部署

### 方式一：下载 Release 压缩包

1. 打开 `https://github.com/fwzm/PRO-order-customer-management/releases/tag/v2.0.0`
2. 下载 `PRO-desktop-v2.0.0-win-x64.zip`
3. 解压到业务电脑，例如 `C:\PRO\Desktop`
4. 配置数据库连接串
5. 运行 `app\PRO.exe`

该压缩包为自包含发布包，业务电脑无需额外安装 .NET Runtime。

### 方式二：自行发布应用
```bash
dotnet publish src/PRO.Desktop -c Release -r win-x64 --self-contained -o C:\PRO\Desktop
```

### 分发安装
- 将发布目录打包为 ZIP
- 分发给用户
- 用户解压后运行 `PRO.exe`

### 配置连接字符串
编辑 `appsettings.json`：
```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=server;Port=5432;Database=pro_db;Username=pro_user;Password=your_password"
  }
}
```

---

## 五、手机端 PWA 部署

### 1. 构建或下载

本地构建：

```powershell
cd src/PRO.Admin.Web
npm ci
npm run build
```

也可以从 Release 下载 `PRO-mobile-pwa-v2.0.0.zip`，解压后得到可部署的静态文件。

### 2. 部署静态文件

将 `dist` 目录部署到 Nginx、IIS 或对象存储静态站点。推荐与 WebApi 同域部署。

### 3. 转发 API

手机端默认调用同域下的：

| 路径 | 说明 |
|------|------|
| `/api` | 业务接口 |
| `/health` | 健康检查 |

生产环境需要让这两个路径转发到 `PRO.WebApi`。如果跨域部署，需要在 WebApi CORS 中加入手机端域名。

### 4. 手机安装

- Android Chrome：打开系统地址 -> 菜单 -> 安装应用/添加到主屏幕
- iPhone Safari：打开系统地址 -> 分享 -> 添加到主屏幕

详细说明见 `docs/mobile-pwa.md`。

---

## 六、配置文件说明

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=pro_db;Username=pro_user;Password=your_password"
  },
  "Jwt": {
    "Key": "your-super-secret-key-at-least-32-chars",
    "Issuer": "PRO-System",
    "ExpiryMinutes": 1440
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "http://localhost:5173"]
  },
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
```

### 环境变量
| 变量名 | 说明 | 示例 |
|--------|------|------|
| `PRO_ConnectionStrings__DefaultConnection` | 数据库连接字符串 | `Host=localhost;Port=5432;Database=pro_db` |
| `PRO_Jwt__Key` | JWT 密钥 | `your-super-secret-key-at-least-32-chars` |
| `PRO_Jwt__Issuer` | JWT 发行者 | `PRO-System` |
| `ASPNETCORE_ENVIRONMENT` | 环境 | `Production` |

---

## 七、目录结构

```
/opt/pro/
├── webapi/
│   ├── PRO.WebApi.dll
│   ├── appsettings.json
│   ├── appsettings.Production.json
│   ├── logs/
│   ├── exports/
│   └── backups/
├── desktop/
│   ├── PRO.Desktop.exe
│   ├── appsettings.json
│   └── logs/
├── docs/
│   ├── README.md
│   ├── user-manual.md
│   ├── api-documentation.md
│   └── ...
└── scripts/
    ├── phase2_migration.sql
    └── phase3_indexes.sql
```

---

## 八、防火墙配置

### Windows
```powershell
# 开放端口
netsh advfirewall firewall add rule name="PRO WebApi" dir=in action=allow protocol=tcp localport=5000
```

### Linux
```bash
# 开放端口
sudo ufw allow 5000/tcp
```

---

## 九、SSL/TLS 配置

### 使用反向代理（推荐）
1. 配置 Nginx 或 Apache
2. 获取 SSL 证书
3. 配置 HTTPS 转发

### Nginx 配置示例
```nginx
server {
    listen 443 ssl;
    server_name pro.example.com;

    ssl_certificate /etc/ssl/certs/pro.crt;
    ssl_certificate_key /etc/ssl/private/pro.key;

    location / {
        proxy_pass http://localhost:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

---

## 十、部署验证清单

### WebApi 验证
- [ ] 服务启动成功
- [ ] 健康检查返回正常
- [ ] 登录功能正常
- [ ] API 文档可访问
- [ ] 日志文件正常生成

### Desktop 验证
- [ ] 客户端启动成功
- [ ] 登录功能正常
- [ ] 核心功能正常
- [ ] 快捷键正常

### 手机端验证
- [ ] 手机浏览器可访问系统地址
- [ ] 可登录并读取订单/客户列表
- [ ] 菜单抽屉在窄屏下可打开和关闭
- [ ] 可添加到主屏幕
- [ ] `/api` 和 `/health` 能正确转发到 WebApi

### 数据库验证
- [ ] 连接正常
- [ ] 表结构正确
- [ ] 初始数据存在
- [ ] 索引存在

---

## 十一、常见问题

### Q: 服务启动失败
**A:** 检查连接字符串、JWT Key、端口占用

### Q: 数据库连接失败
**A:** 检查数据库服务状态、连接字符串、防火墙

### Q: 客户端无法连接服务器
**A:** 检查服务器地址、端口、防火墙、CORS配置

### Q: 手机端无法安装到主屏幕
**A:** 检查是否使用 HTTPS、`manifest.json` 是否可访问、浏览器是否支持 PWA。企业微信内置浏览器可能限制安装能力，建议使用系统浏览器。

### Q: 日志文件未生成
**A:** 检查日志目录权限、磁盘空间
