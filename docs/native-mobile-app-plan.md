# PRO 原生 APK 与 App Store 应用实施方案

> 当前项目已有 `src/PRO.Admin.Web` Vue/Vite PWA、`src/PRO.WebApi` ASP.NET Core WebApi、JWT、权限、分公司隔离和业务接口。第一阶段建议使用 Capacitor 将现有 PWA 包装为 Android/iOS 原生应用；第二阶段再评估 .NET MAUI 或 Flutter 重写核心移动端。

## 1. 总体路线

```text
Vue/Vite PWA -> Capacitor Native Shell -> Android Gradle / iOS Xcode -> APK/AAB / App Store
                       |
                       +-> JS Bridge: Camera / Push / Location / Secure Storage / Network
                       |
                       +-> WebApi: JWT / Customers / Orders / Inventory / Dashboard
```

### 核心原则

- 手机端只访问 `PRO.WebApi`，不能直连 PostgreSQL。
- 认证继续使用 `/api/auth/login` 返回的 JWT。
- 原生能力通过 Capacitor 插件桥接到现有 Vue 页面。
- 离线能力只缓存查询数据和草稿，不做离线库存扣减、收款核销等高风险写操作。
- 热更新仅限 Web 资源，不能绕过 App Store/应用市场审核来变更原生能力或重大业务逻辑。

## 2. 原生项目构建配置

### 需求清单

| 需求 | Android | iOS |
|------|---------|-----|
| 包名/Bundle ID | `com.pro.management` | `com.pro.management` |
| 构建产物 | Debug APK、Release AAB | Archive、TestFlight、App Store |
| 签名 | keystore + `signing.properties` | Apple Developer Team + Provisioning Profile |
| 环境配置 | `VITE_API_BASE_URL` | `VITE_API_BASE_URL` |
| 权限声明 | Manifest | Info.plist |
| CI | Windows 可构建 Web；Android 推荐 Ubuntu；iOS 必须 macOS |

### `src/PRO.Admin.Web/package.json`

```json
{
  "scripts": {
    "mobile:add:android": "cap add android",
    "mobile:add:ios": "cap add ios",
    "mobile:sync": "npm run build && cap sync",
    "mobile:open:android": "cap open android",
    "mobile:open:ios": "cap open ios",
    "mobile:copy": "npm run build && cap copy"
  },
  "dependencies": {
    "@capacitor/android": "^8.4.0",
    "@capacitor/app": "^8.4.0",
    "@capacitor/camera": "^8.4.0",
    "@capacitor/core": "^8.4.0",
    "@capacitor/filesystem": "^8.4.0",
    "@capacitor/geolocation": "^8.4.0",
    "@capacitor/ios": "^8.4.0",
    "@capacitor/network": "^8.4.0",
    "@capacitor/preferences": "^8.4.0",
    "@capacitor/push-notifications": "^8.4.0"
  },
  "devDependencies": {
    "@capacitor/cli": "^8.4.0"
  }
}
```

### `src/PRO.Admin.Web/capacitor.config.ts`

```ts
import type { CapacitorConfig } from '@capacitor/cli'

const config: CapacitorConfig = {
  appId: 'com.pro.management',
  appName: 'PRO订单与客户管理系统',
  webDir: 'dist',
  bundledWebRuntime: false,
  server: {
    androidScheme: 'https',
    iosScheme: 'https',
    cleartext: false
  },
  plugins: {
    Camera: {
      permissions: ['camera', 'photos']
    },
    Geolocation: {
      permissions: ['location']
    },
    PushNotifications: {
      presentationOptions: ['badge', 'sound', 'alert']
    }
  }
}

export default config
```

### `android/signing.properties`，不要提交真实文件

```properties
storeFile=../keystore/pro-release.jks
storePassword=${PRO_ANDROID_STORE_PASSWORD}
keyAlias=pro
keyPassword=${PRO_ANDROID_KEY_PASSWORD}
```

### `android/app/build.gradle` 核心配置

```gradle
def signingProps = new Properties()
def signingFile = rootProject.file("signing.properties")
if (signingFile.exists()) {
    signingProps.load(new FileInputStream(signingFile))
}

android {
    namespace "com.pro.management"
    compileSdk 35

    defaultConfig {
        applicationId "com.pro.management"
        minSdk 24
        targetSdk 35
        versionCode 20000
        versionName "2.0.0"
    }

    signingConfigs {
        release {
            if (signingFile.exists()) {
                storeFile file(signingProps["storeFile"])
                storePassword System.getenv("PRO_ANDROID_STORE_PASSWORD") ?: signingProps["storePassword"]
                keyAlias signingProps["keyAlias"]
                keyPassword System.getenv("PRO_ANDROID_KEY_PASSWORD") ?: signingProps["keyPassword"]
            }
        }
    }

    buildTypes {
        release {
            minifyEnabled true
            shrinkResources true
            proguardFiles getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro"
            signingConfig signingConfigs.release
        }
        debug {
            applicationIdSuffix ".debug"
            versionNameSuffix "-debug"
        }
    }
}
```

### `ios/App/App/Info.plist` 权限说明

```xml
<key>NSCameraUsageDescription</key>
<string>用于拍摄客户拜访、配送签收和订单附件照片。</string>
<key>NSPhotoLibraryUsageDescription</key>
<string>用于选择客户、订单、收款凭证相关图片。</string>
<key>NSLocationWhenInUseUsageDescription</key>
<string>用于记录拜访位置、配送位置和客户地址定位。</string>
<key>NSUserNotificationsUsageDescription</key>
<string>用于接收待办、配送、应收和系统告警通知。</string>
```

## 3. 原生功能与 JS 桥接通信

### 需求清单

- 相机：客户拜访照片、签收照片、收款凭证。
- 定位：客户地址定位、配送员当前位置、拜访签到。
- 推送：待分配订单、配送失败、超期应收、系统告警。
- 安全存储：JWT、刷新 token、WebApi 地址。
- 网络状态：离线提示、自动重试。
- 文件系统：附件临时缓存、导出文件保存。

### `src/PRO.Admin.Web/src/native/nativeBridge.ts`

```ts
import { Capacitor } from '@capacitor/core'
import { Camera, CameraResultType, CameraSource } from '@capacitor/camera'
import { Geolocation } from '@capacitor/geolocation'
import { Preferences } from '@capacitor/preferences'
import { Network } from '@capacitor/network'
import { PushNotifications } from '@capacitor/push-notifications'

export const isNative = () => Capacitor.isNativePlatform()

export async function saveSecureValue(key: string, value: string) {
  await Preferences.set({ key, value })
}

export async function getSecureValue(key: string) {
  const result = await Preferences.get({ key })
  return result.value
}

export async function removeSecureValue(key: string) {
  await Preferences.remove({ key })
}

export async function takeBusinessPhoto() {
  if (!isNative()) throw new Error('当前环境不支持原生相机')

  const photo = await Camera.getPhoto({
    quality: 80,
    allowEditing: false,
    resultType: CameraResultType.DataUrl,
    source: CameraSource.Prompt
  })

  return {
    dataUrl: photo.dataUrl,
    format: photo.format
  }
}

export async function getCurrentLocation() {
  if (!isNative()) throw new Error('当前环境不支持原生定位')

  const permission = await Geolocation.requestPermissions()
  if (permission.location === 'denied') {
    throw new Error('定位权限被拒绝')
  }

  const position = await Geolocation.getCurrentPosition({
    enableHighAccuracy: true,
    timeout: 10000
  })

  return {
    latitude: position.coords.latitude,
    longitude: position.coords.longitude,
    accuracy: position.coords.accuracy
  }
}

export async function getNetworkState() {
  return Network.getStatus()
}

export async function registerPush(onToken: (token: string) => Promise<void>, onMessage: (payload: unknown) => void) {
  if (!isNative()) return

  const permission = await PushNotifications.requestPermissions()
  if (permission.receive !== 'granted') {
    throw new Error('通知权限未授权')
  }

  await PushNotifications.register()

  PushNotifications.addListener('registration', async token => {
    await onToken(token.value)
  })

  PushNotifications.addListener('pushNotificationReceived', notification => {
    onMessage(notification)
  })

  PushNotifications.addListener('pushNotificationActionPerformed', action => {
    onMessage(action.notification)
  })
}
```

### 与现有 Axios 认证集成

```ts
// src/PRO.Admin.Web/src/native/mobileAuthStore.ts
import { getSecureValue, saveSecureValue, removeSecureValue } from './nativeBridge'

const TOKEN_KEY = 'pro.jwt.token'
const USER_KEY = 'pro.current.user'

export async function setNativeSession(token: string, user: unknown) {
  await saveSecureValue(TOKEN_KEY, token)
  await saveSecureValue(USER_KEY, JSON.stringify(user))
  localStorage.setItem('token', token)
  localStorage.setItem('user', JSON.stringify(user))
}

export async function restoreNativeSession() {
  const token = await getSecureValue(TOKEN_KEY)
  const user = await getSecureValue(USER_KEY)
  if (token) localStorage.setItem('token', token)
  if (user) localStorage.setItem('user', user)
}

export async function clearNativeSession() {
  await removeSecureValue(TOKEN_KEY)
  await removeSecureValue(USER_KEY)
  localStorage.removeItem('token')
  localStorage.removeItem('user')
}
```

## 4. App Store 与 Android 应用市场审核

### 需求清单

| 事项 | iOS | Android |
|------|-----|---------|
| 隐私政策 | 必须 | 必须 |
| 权限用途说明 | Info.plist 必填且真实 | Data Safety 表单必填 |
| 登录账号 | 提供审核账号 | 提供审核账号 |
| 推送 | APNs 证书/Key | FCM 配置 |
| 定位 | 不能过度采集 | 不能后台滥用 |
| 热更新 | 不得绕过审核改变核心功能 | 不得下载执行代码 |
| 支付 | 如无虚拟商品支付，说明只做企业业务系统 | 同左 |

### `docs/app-store-review-checklist.md` 建议内容

```md
# PRO 原生应用提审清单

- [ ] 隐私政策 URL 可公开访问
- [ ] 测试账号可登录，且有演示数据
- [ ] 权限说明与实际用途一致
- [ ] 不在未授权时采集定位、相册、相机
- [ ] 不包含真实客户隐私截图
- [ ] 不包含默认弱密码说明
- [ ] iOS `NSCameraUsageDescription` 等字段齐全
- [ ] Android Data Safety 填写数据类型、用途和加密传输
- [ ] 后端使用 HTTPS
- [ ] 若启用推送，提供通知用途说明和退订方式
```

## 5. 平台 UI 与交互深度适配

### 需求清单

- 异形屏安全区：顶部刘海、底部 Home Indicator。
- Android 返回键：关闭弹窗、返回上级、退出确认。
- iOS 手势：边缘返回、滚动回弹、安全区。
- 状态栏：深色/浅色适配。
- 底部导航：手机端优先 TabBar，不使用桌面侧边栏作为主入口。
- 表格：手机端改为卡片列表，避免横向滚动。

### 安全区 CSS

```css
:root {
  --safe-top: env(safe-area-inset-top, 0px);
  --safe-right: env(safe-area-inset-right, 0px);
  --safe-bottom: env(safe-area-inset-bottom, 0px);
  --safe-left: env(safe-area-inset-left, 0px);
}

.mobile-shell {
  min-height: 100vh;
  padding-top: var(--safe-top);
  padding-right: var(--safe-right);
  padding-bottom: var(--safe-bottom);
  padding-left: var(--safe-left);
  background: #f5f7fb;
}

.mobile-bottom-tabs {
  position: fixed;
  left: 0;
  right: 0;
  bottom: 0;
  height: calc(56px + var(--safe-bottom));
  padding-bottom: var(--safe-bottom);
  background: #fff;
  border-top: 1px solid #e5e7eb;
}
```

### Android 返回键

```ts
// src/PRO.Admin.Web/src/native/backButton.ts
import { App } from '@capacitor/app'
import router from '@/router'

export function registerAndroidBackButton() {
  App.addListener('backButton', async ({ canGoBack }) => {
    const modal = document.querySelector('.el-overlay, .el-dialog__wrapper')
    if (modal) {
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
      return
    }

    if (canGoBack && router.currentRoute.value.path !== '/dashboard') {
      router.back()
      return
    }

    await App.exitApp()
  })
}
```

## 6. 离线缓存与热更新限制

### 需求清单

- 离线只允许：
  - 最近客户缓存
  - 最近订单缓存
  - 草稿订单
  - 待上传照片
- 离线禁止：
  - 库存扣减
  - 收款核销
  - 订单完成
  - 批量状态变更
- 恢复网络后：
  - 先刷新 token
  - 再按 FIFO 上传 outbox
  - 每条写操作要有幂等 ID
  - 冲突时进入人工处理

### `src/PRO.Admin.Web/src/native/offlineOutbox.ts`

```ts
type OutboxStatus = 'pending' | 'syncing' | 'failed'

export interface OutboxItem {
  id: string
  method: 'POST' | 'PUT' | 'DELETE'
  url: string
  body?: unknown
  createdAt: number
  retryCount: number
  status: OutboxStatus
  lastError?: string
}

const DB_NAME = 'pro-mobile-cache'
const STORE = 'outbox'

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, 1)
    request.onupgradeneeded = () => {
      const db = request.result
      if (!db.objectStoreNames.contains(STORE)) {
        db.createObjectStore(STORE, { keyPath: 'id' })
      }
    }
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error)
  })
}

export async function enqueueOutbox(item: Omit<OutboxItem, 'createdAt' | 'retryCount' | 'status'>) {
  const db = await openDb()
  const tx = db.transaction(STORE, 'readwrite')
  tx.objectStore(STORE).put({
    ...item,
    createdAt: Date.now(),
    retryCount: 0,
    status: 'pending'
  })
}

export async function listOutbox(): Promise<OutboxItem[]> {
  const db = await openDb()
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readonly')
    const req = tx.objectStore(STORE).getAll()
    req.onsuccess = () => resolve(req.result.sort((a, b) => a.createdAt - b.createdAt))
    req.onerror = () => reject(req.error)
  })
}

export async function removeOutbox(id: string) {
  const db = await openDb()
  const tx = db.transaction(STORE, 'readwrite')
  tx.objectStore(STORE).delete(id)
}

export async function syncOutbox(apiBaseUrl: string, token: string) {
  const items = await listOutbox()
  for (const item of items) {
    try {
      const response = await fetch(`${apiBaseUrl}${item.url}`, {
        method: item.method,
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
          'X-Idempotency-Key': item.id
        },
        body: item.body ? JSON.stringify(item.body) : undefined
      })

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`)
      }

      await removeOutbox(item.id)
    } catch (error) {
      console.warn('同步失败，等待下次重试', item.id, error)
      break
    }
  }
}
```

### 热更新策略

```ts
// src/PRO.Admin.Web/src/native/updatePolicy.ts
export interface MobileVersionInfo {
  nativeVersion: string
  webVersion: string
  minSupportedNativeVersion: string
  forceUpgrade: boolean
  message?: string
  storeUrl?: string
}

export function shouldForceStoreUpgrade(info: MobileVersionInfo, currentNativeVersion: string) {
  if (info.forceUpgrade) return true
  return compareSemver(currentNativeVersion, info.minSupportedNativeVersion) < 0
}

function compareSemver(a: string, b: string) {
  const pa = a.split('.').map(Number)
  const pb = b.split('.').map(Number)
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const da = pa[i] ?? 0
    const db = pb[i] ?? 0
    if (da !== db) return da - db
  }
  return 0
}
```

## 7. WebApi 需要补齐的移动端接口

| 接口 | 用途 |
|------|------|
| `POST /api/mobile/devices/register` | 注册设备、推送 token、平台 |
| `DELETE /api/mobile/devices/{deviceId}` | 退出登录时解绑设备 |
| `GET /api/mobile/version` | 原生版本、Web 版本、强制升级策略 |
| `POST /api/mobile/attachments` | 上传相机照片/附件 |
| `POST /api/mobile/sync/outbox` | 服务器端幂等同步入口 |

### DTO 示例

```csharp
public sealed class RegisterMobileDeviceRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty; // ios/android
    public string PushToken { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
}

public sealed class MobileVersionResponse
{
    public string NativeVersion { get; set; } = "2.0.0";
    public string WebVersion { get; set; } = "2.0.0";
    public string MinSupportedNativeVersion { get; set; } = "2.0.0";
    public bool ForceUpgrade { get; set; }
    public string? Message { get; set; }
    public string? StoreUrl { get; set; }
}
```

### Controller 示例

```csharp
[ApiController]
[Route("api/mobile")]
[Authorize]
public sealed class MobileController : ControllerBase
{
    [HttpPost("devices/register")]
    public IActionResult RegisterDevice([FromBody] RegisterMobileDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId) || string.IsNullOrWhiteSpace(request.Platform))
            return BadRequest(ApiResponse<bool>.Fail("设备信息不完整"));

        return Ok(ApiResponse<bool>.Ok(true, "设备注册成功"));
    }

    [AllowAnonymous]
    [HttpGet("version")]
    public IActionResult GetVersion()
    {
        return Ok(ApiResponse<MobileVersionResponse>.Ok(new MobileVersionResponse
        {
            NativeVersion = "2.0.0",
            WebVersion = "2.0.0",
            MinSupportedNativeVersion = "2.0.0",
            ForceUpgrade = false
        }));
    }
}
```

## 8. 验收清单

- [ ] Android Debug APK 可安装。
- [ ] Android Release AAB 可签名。
- [ ] iOS Xcode 项目可 Archive。
- [ ] 登录后 JWT 可持久化。
- [ ] 相机权限拒绝时提示明确。
- [ ] 定位权限拒绝时不阻塞普通查询。
- [ ] 推送 token 能上报 WebApi。
- [ ] 手机端适配安全区和 Android 返回键。
- [ ] 离线草稿可保存，恢复网络后可重试。
- [ ] 库存、收款、完成订单等高风险操作不允许离线直接提交。
- [ ] 隐私政策、权限说明、审核账号、截图材料准备完成。
