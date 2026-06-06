import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import MainLayout from '@/layouts/MainLayout.vue'
import LoginView from '@/views/LoginView.vue'
import DashboardView from '@/views/DashboardView.vue'

const routes = [
  {
    path: '/login',
    name: 'Login',
    component: LoginView,
    meta: { requiresAuth: false },
  },
  {
    path: '/',
    component: MainLayout,
    redirect: '/dashboard',
    meta: { requiresAuth: true },
    children: [
      { path: 'dashboard', name: 'Dashboard', component: DashboardView, meta: { title: '仪表盘' } },
      {
        path: 'customers',
        name: 'Customers',
        component: () => import('@/views/customers/CustomerList.vue'),
        meta: { title: '客户管理' },
      },
      {
        path: 'customers/:id',
        name: 'CustomerDetail',
        component: () => import('@/views/customers/CustomerDetail.vue'),
        meta: { title: '客户详情' },
      },
      {
        path: 'orders',
        redirect: '/orders/list',
        meta: { title: '订单管理' },
      },
      {
        path: 'orders/new',
        name: 'OrderNew',
        component: () => import('@/views/orders/OrderNew.vue'),
        meta: { title: '新建订单' },
      },
      {
        path: 'orders/draft',
        name: 'OrderDraft',
        component: () => import('@/views/orders/OrderList.vue'),
        meta: { title: '草稿订单' },
      },
      {
        path: 'orders/list',
        name: 'OrderList',
        component: () => import('@/views/orders/OrderList.vue'),
        meta: { title: '订单列表' },
      },
      {
        path: 'orders/:id',
        name: 'OrderDetail',
        component: () => import('@/views/orders/OrderDetail.vue'),
        meta: { title: '订单详情' },
      },
      {
        path: 'receivables',
        name: 'Receivables',
        component: () => import('@/views/receivables/ReceivableList.vue'),
        meta: { title: '应收账款' },
      },
      {
        path: 'products',
        name: 'Products',
        component: () => import('@/views/products/ProductList.vue'),
        meta: { title: '产品管理' },
      },
      {
        path: 'products/:id',
        name: 'ProductDetail',
        component: () => import('@/views/products/ProductDetail.vue'),
        meta: { title: '产品详情' },
      },
      {
        path: 'inventory',
        name: 'Inventory',
        component: () => import('@/views/inventory/InventoryCheck.vue'),
        meta: { title: '库存盘点' },
      },
      {
        path: 'logistics',
        name: 'Logistics',
        component: () => import('@/views/logistics/LogisticsView.vue'),
        meta: { title: '物流管理' },
      },
      {
        path: 'settlements',
        name: 'Settlements',
        component: () => import('@/views/settlements/SettlementList.vue'),
        meta: { title: '结算管理' },
      },
      {
        path: 'workplans',
        name: 'WorkPlans',
        component: () => import('@/views/workplans/WorkPlanView.vue'),
        meta: { title: '工作计划' },
      },
      {
        path: 'visits',
        name: 'Visits',
        component: () => import('@/views/visits/VisitList.vue'),
        meta: { title: '拜访管理' },
      },
      {
        path: 'reports',
        name: 'Reports',
        component: () => import('@/views/reports/ReportCenter.vue'),
        meta: { title: '报表中心' },
      },
      {
        path: 'forecast',
        name: 'Forecast',
        component: () => import('@/views/forecast/ForecastView.vue'),
        meta: { title: '智能预测' },
      },
      {
        path: 'system/users',
        name: 'SystemUsers',
        component: () => import('@/views/system/UserList.vue'),
        meta: { title: '用户管理' },
      },
      {
        path: 'system/roles',
        name: 'SystemRoles',
        component: () => import('@/views/system/RoleList.vue'),
        meta: { title: '角色权限' },
      },
      {
        path: 'system/departments',
        name: 'SystemDepartments',
        component: () => import('@/views/system/DepartmentManage.vue'),
        meta: { title: '部门管理' },
      },
      {
        path: 'system/fields',
        name: 'SystemFields',
        component: () => import('@/views/system/FieldManage.vue'),
        meta: { title: '字段管理' },
      },
      {
        path: 'system/config',
        name: 'SystemConfig',
        component: () => import('@/views/system/SystemConfig.vue'),
        meta: { title: '系统配置' },
      },
      {
        path: 'system/logs',
        name: 'SystemLogs',
        component: () => import('@/views/system/OperationLogs.vue'),
        meta: { title: '操作日志' },
      },
    ],
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach((to, from, next) => {
  const authStore = useAuthStore()
  if (to.meta.requiresAuth !== false && !authStore.isLoggedIn) {
    next('/login')
  } else if (to.path === '/login' && authStore.isLoggedIn) {
    next('/dashboard')
  } else {
    next()
  }
})

export default router