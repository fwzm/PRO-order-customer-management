<template>
  <el-container class="main-layout">
    <el-aside :width="isCollapse ? '68px' : '240px'" class="sidebar">
      <div class="logo">
        <span v-if="!isCollapse" class="logo-text">PRO Studio</span>
        <span v-else class="logo-text-collapsed">P</span>
      </div>
      <el-menu
        :default-active="route.path"
        :collapse="isCollapse"
        :router="true"
        class="apple-menu"
      >
        <el-menu-item index="/dashboard">
          <el-icon><DataAnalysis /></el-icon>
          <span>仪表盘</span>
        </el-menu-item>
        <el-menu-item index="/customers">
          <el-icon><UserFilled /></el-icon>
          <span>客户管理</span>
        </el-menu-item>
        <el-sub-menu index="/orders">
          <template #title>
            <el-icon><List /></el-icon>
            <span>订单管理</span>
          </template>
          <el-menu-item index="/orders/new">新建订单</el-menu-item>
          <el-menu-item index="/orders/draft">草稿订单</el-menu-item>
          <el-menu-item index="/orders/list">订单列表</el-menu-item>
        </el-sub-menu>
        <el-menu-item index="/receivables">
          <el-icon><Coin /></el-icon>
          <span>应收账款</span>
        </el-menu-item>
        <el-menu-item index="/products">
          <el-icon><Goods /></el-icon>
          <span>产品管理</span>
        </el-menu-item>
        <el-menu-item index="/inventory">
          <el-icon><FolderChecked /></el-icon>
          <span>库存盘点</span>
        </el-menu-item>
        <el-menu-item index="/logistics">
          <el-icon><Van /></el-icon>
          <span>物流管理</span>
        </el-menu-item>
        <el-menu-item index="/settlements">
          <el-icon><Money /></el-icon>
          <span>结算管理</span>
        </el-menu-item>
        <el-menu-item index="/workplans">
          <el-icon><Calendar /></el-icon>
          <span>工作计划</span>
        </el-menu-item>
        <el-menu-item index="/visits">
          <el-icon><Notebook /></el-icon>
          <span>拜访管理</span>
        </el-menu-item>
        <el-menu-item index="/reports">
          <el-icon><PieChart /></el-icon>
          <span>报表中心</span>
        </el-menu-item>
        <el-menu-item index="/forecast">
          <el-icon><TrendCharts /></el-icon>
          <span>智能预测</span>
        </el-menu-item>
        <el-sub-menu index="system">
          <template #title>
            <el-icon><Setting /></el-icon>
            <span>系统管理</span>
          </template>
          <el-menu-item index="/system/users">用户管理</el-menu-item>
          <el-menu-item index="/system/departments">部门管理</el-menu-item>
          <el-menu-item index="/system/roles">角色权限</el-menu-item>
          <el-menu-item index="/system/fields">字段管理</el-menu-item>
          <el-menu-item index="/system/config">系统配置</el-menu-item>
          <el-menu-item index="/system/logs">操作日志</el-menu-item>
        </el-sub-menu>
      </el-menu>
    </el-aside>

    <el-container class="content-container">
      <el-header class="header">
        <div class="header-left">
          <el-icon class="collapse-btn" @click="isCollapse = !isCollapse">
            <Fold v-if="!isCollapse" />
            <Expand v-else />
          </el-icon>
          <el-breadcrumb class="apple-breadcrumb">
            <el-breadcrumb-item :to="{ path: '/dashboard' }">首页</el-breadcrumb-item>
            <el-breadcrumb-item v-if="route.meta.title">{{ route.meta.title }}</el-breadcrumb-item>
          </el-breadcrumb>
        </div>
        <div class="header-right">
          <el-dropdown @command="handleCommand" trigger="click" class="apple-dropdown">
            <span class="user-info">
              <el-avatar :size="32" icon="UserFilled" class="user-avatar" />
              <span class="username">{{ authStore.user?.name || '管理员' }}</span>
              <el-icon class="dropdown-arrow"><ArrowDown /></el-icon>
            </span>
            <template #dropdown>
              <el-dropdown-menu class="apple-dropdown-menu">
                <el-dropdown-item command="profile">
                  <el-icon><User /></el-icon>个人中心
                </el-dropdown-item>
                <el-dropdown-item command="logout" divided class="logout-item">
                  <el-icon><SwitchButton /></el-icon>退出登录
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </el-header>

      <div class="tab-bar" v-if="tabs.length > 0">
        <div class="tab-scroll">
          <div
            v-for="tab in tabs"
            :key="tab.path"
            class="tab-item"
            :class="{ active: tab.path === route.path }"
            @click="switchTab(tab)"
          >
            <span class="tab-title">{{ tab.title }}</span>
            <el-icon class="tab-close" @click.stop="closeTab(tab)"><Close /></el-icon>
          </div>
        </div>
      </div>

      <el-main class="main-content">
        <router-view />
      </el-main>

      <div class="status-bar">
        <div class="status-item">
          <span class="status-dot" :class="syncStatus"></span>
          <span class="status-label">云同步状态</span>
          <span class="status-text">{{ syncStatusText }}</span>
        </div>
      </div>
    </el-container>
  </el-container>
</template>

<script setup>
import { ref, watch, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { healthApi } from '@/api'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const isCollapse = ref(false)
const tabs = ref([])
const syncStatus = ref('connecting')
const syncStatusText = ref('连接中...')
let healthInterval = null

async function checkHealth() {
  try {
    const res = await healthApi.check()
    if (res.status === 'healthy' || (res.database === 'connected')) {
      syncStatus.value = 'connected'
      syncStatusText.value = '已连接'
    } else {
      syncStatus.value = 'disconnected'
      syncStatusText.value = '连接断开'
    }
  } catch {
    syncStatus.value = 'disconnected'
    syncStatusText.value = '连接断开'
  }
}

const tabTitles = {
  '/dashboard': '仪表盘',
  '/customers': '客户管理',
  '/orders/new': '新建订单',
  '/orders/draft': '草稿订单',
  '/orders/list': '订单列表',
  '/receivables': '应收账款',
  '/products': '产品管理',
  '/inventory': '库存盘点',
  '/logistics': '物流管理',
  '/settlements': '结算管理',
  '/workplans': '工作计划',
  '/visits': '拜访管理',
  '/reports': '报表中心',
  '/forecast': '智能预测',
  '/system/users': '用户管理',
  '/system/departments': '部门管理',
  '/system/roles': '角色权限',
  '/system/fields': '字段管理',
  '/system/config': '系统配置',
  '/system/logs': '操作日志',
}

const mainTabs = ['/dashboard', '/customers', '/orders/new', '/orders/draft', '/orders/list', '/receivables', '/products', '/inventory', '/logistics', '/settlements', '/workplans', '/visits', '/reports', '/forecast', '/system/users', '/system/departments', '/system/roles', '/system/fields', '/system/config', '/system/logs']

watch(
  () => route.path,
  (path) => {
    const key = Object.keys(tabTitles).find(k => path.startsWith(k))
    if (key && key !== '/dashboard' && path !== '/dashboard') {
      const baseKey = path.startsWith('/customers/') ? '/customers' :
                      path.startsWith('/orders/') && !['/orders/new', '/orders/draft', '/orders/list'].includes(path) ? '/orders/list' :
                      path
      const title = tabTitles[baseKey] || route.meta.title || '页面'
      const exists = tabs.value.find(t => t.path === baseKey)
      if (!exists) {
        tabs.value.push({ path: baseKey, title })
      }
    }
  },
  { immediate: true }
)

function switchTab(tab) {
  router.push(tab.path)
}

function closeTab(tab) {
  tabs.value = tabs.value.filter(t => t.path !== tab.path)
  if (route.path === tab.path || route.path.startsWith(tab.path + '/')) {
    const lastTab = tabs.value[tabs.value.length - 1]
    if (lastTab) {
      router.push(lastTab.path)
    } else {
      router.push('/dashboard')
    }
  }
}

function handleCommand(command) {
  if (command === 'profile') {
    router.push('/profile')
  } else if (command === 'logout') {
    authStore.logout()
    router.push('/login')
  }
}

onMounted(() => {
  checkHealth()
  healthInterval = setInterval(checkHealth, 30000)
})

onUnmounted(() => {
  if (healthInterval) {
    clearInterval(healthInterval)
  }
})
</script>

<style scoped>
.main-layout {
  height: 100vh;
  background-color: #f5f5f7;
}

.sidebar {
  background: rgba(246, 245, 246, 0.85) !important;
  backdrop-filter: blur(30px) saturate(190%);
  border-right: 1px solid rgba(0, 0, 0, 0.08) !important;
  transition: width 0.4s var(--apple-ease) !important;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.logo {
  height: 56px;
  display: flex;
  align-items: center;
  padding: 0 24px;
  border-bottom: 1px solid rgba(0, 0, 0, 0.06);
}

.logo-text {
  font-size: 16px;
  font-weight: 700;
  letter-spacing: -0.2px;
  color: #1d1d1f;
  background: linear-gradient(135deg, #1d1d1f 0%, #434348 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.logo-text-collapsed {
  font-size: 18px;
  font-weight: 800;
  color: #0071e3;
  margin: 0 auto;
}

.apple-menu {
  border-right: none !important;
  background: transparent !important;
  padding: 8px 6px !important;
  flex: 1;
  overflow-y: auto;
}

.apple-menu::-webkit-scrollbar {
  width: 3px;
}

:deep(.el-menu-item), :deep(.el-sub-menu__title) {
  height: 38px !important;
  line-height: 38px !important;
  margin: 2px 4px !important;
  border-radius: 8px !important;
  color: #424245 !important;
  font-size: 13px !important;
  font-weight: 500 !important;
  transition: var(--apple-transition) !important;
}

:deep(.el-menu-item:hover), :deep(.el-sub-menu__title:hover) {
  background-color: rgba(0, 0, 0, 0.04) !important;
  color: #1d1d1f !important;
}

:deep(.el-menu-item.is-active) {
  background-color: #0071e3 !important;
  color: #ffffff !important;
  font-weight: 600 !important;
  box-shadow: 0 4px 12px rgba(0, 113, 227, 0.2) !important;
}

:deep(.el-menu-item.is-active .el-icon) {
  color: #ffffff !important;
}

:deep(.el-menu--collapse .el-menu-item) {
  margin: 4px 8px !important;
}

.content-container {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.header {
  background: rgba(255, 255, 255, 0.8) !important;
  backdrop-filter: blur(20px) saturate(190%);
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid rgba(0, 0, 0, 0.06);
  padding: 0 24px;
  height: 52px !important;
  z-index: 10;
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 20px;
}

.collapse-btn {
  font-size: 18px;
  cursor: pointer;
  color: #86868b;
  transition: var(--apple-transition);
}

.collapse-btn:hover {
  color: #1d1d1f;
  transform: scale(1.05);
}

.apple-breadcrumb :deep(.el-breadcrumb__inner) {
  font-size: 13px;
  font-weight: 400;
  color: #86868b;
  transition: var(--apple-transition);
}

.apple-breadcrumb :deep(.el-breadcrumb__inner.is-link:hover) {
  color: #0071e3;
}

.apple-breadcrumb :deep(.el-breadcrumb__item:last-child .el-breadcrumb__inner) {
  color: #1d1d1f;
  font-weight: 600;
}

.header-right {
  display: flex;
  align-items: center;
}

.user-info {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  padding: 4px 12px;
  border-radius: 20px;
  transition: var(--apple-transition);
}

.user-info:hover {
  background-color: rgba(0, 0, 0, 0.03);
}

.user-avatar {
  border: 1px solid rgba(0, 0, 0, 0.05);
  background-color: #f5f5f7;
  color: #86868b;
}

.username {
  font-size: 13px;
  font-weight: 500;
  color: #1d1d1f;
}

.dropdown-arrow {
  font-size: 10px;
  color: #86868b;
}

.apple-dropdown-menu {
  border: 1px solid rgba(0, 0, 0, 0.08) !important;
  border-radius: 12px !important;
  padding: 6px !important;
  box-shadow: var(--el-box-shadow-dark) !important;
  background: rgba(255, 255, 255, 0.9) !important;
  backdrop-filter: blur(20px) !important;
}

.apple-dropdown-menu :deep(.el-dropdown-menu__item) {
  padding: 8px 12px !important;
  font-size: 13px !important;
  border-radius: 8px !important;
  color: #1d1d1f !important;
  gap: 8px;
}

.apple-dropdown-menu :deep(.el-dropdown-menu__item:hover) {
  background-color: #0071e3 !important;
  color: #ffffff !important;
}

.apple-dropdown-menu :deep(.logout-item:hover) {
  background-color: #ff3b30 !important;
  color: #ffffff !important;
}

.tab-bar {
  background: rgba(255, 255, 255, 0.7);
  backdrop-filter: blur(10px);
  border-bottom: 1px solid rgba(0, 0, 0, 0.06);
  padding: 0 12px;
  flex-shrink: 0;
  height: 36px;
  display: flex;
  align-items: center;
}

.tab-scroll {
  display: flex;
  gap: 2px;
  overflow-x: auto;
  overflow-y: hidden;
  height: 100%;
  align-items: center;
}

.tab-scroll::-webkit-scrollbar {
  height: 0;
}

.tab-item {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 10px;
  font-size: 12px;
  color: #86868b;
  cursor: pointer;
  border-radius: 6px;
  transition: var(--apple-transition);
  white-space: nowrap;
  flex-shrink: 0;
  height: 28px;
  user-select: none;
}

.tab-item:hover {
  background-color: rgba(0, 0, 0, 0.04);
  color: #424245;
}

.tab-item.active {
  background-color: #0071e3;
  color: #ffffff;
  font-weight: 500;
}

.tab-item.active .tab-close {
  color: rgba(255, 255, 255, 0.7);
}

.tab-item.active .tab-close:hover {
  color: #ffffff;
}

.tab-title {
  line-height: 1;
}

.tab-close {
  font-size: 10px;
  color: transparent;
  transition: var(--apple-transition);
  border-radius: 3px;
}

.tab-item:hover .tab-close {
  color: #86868b;
}

.tab-close:hover {
  background-color: rgba(0, 0, 0, 0.08);
  color: #424245 !important;
}

.main-content {
  background: #f5f5f7;
  padding: 20px;
  overflow-y: auto;
  flex: 1;
}

.main-content::-webkit-scrollbar {
  width: 6px;
}

.main-content::-webkit-scrollbar-thumb {
  background: rgba(0, 0, 0, 0.08);
  border-radius: 3px;
}

.main-content::-webkit-scrollbar-thumb:hover {
  background: rgba(0, 0, 0, 0.16);
}

.status-bar {
  height: 28px;
  background: rgba(255, 255, 255, 0.75);
  backdrop-filter: blur(10px);
  border-top: 1px solid rgba(0, 0, 0, 0.06);
  display: flex;
  align-items: center;
  justify-content: flex-end;
  padding: 0 20px;
  flex-shrink: 0;
  user-select: none;
}

.status-item {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
}

.status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  flex-shrink: 0;
  transition: background 0.3s, box-shadow 0.3s;
}

.status-dot.connected {
  background: #34c759;
  box-shadow: 0 0 6px rgba(52, 199, 89, 0.5);
}

.status-dot.disconnected {
  background: #ff3b30;
  box-shadow: 0 0 6px rgba(255, 59, 48, 0.5);
}

.status-dot.connecting {
  background: #ff9500;
  box-shadow: 0 0 6px rgba(255, 149, 0, 0.5);
  animation: pulse 1.5s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.4; }
}

.status-label {
  color: #86868b;
  font-weight: 500;
}

.status-text {
  color: #424245;
  font-weight: 400;
}
</style>