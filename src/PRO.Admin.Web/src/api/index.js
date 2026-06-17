import request from './request'
import axios from 'axios'

export const healthApi = {
  check: async () => {
    const response = await axios.get('/health', { timeout: 5000 })
    return response.data
  },
}

export const authApi = {
  login: (data) => request.post('/auth/login', data),
  logout: () => request.post('/auth/logout'),
  changePassword: (data) => request.post('/auth/change-password', data),
  getProfile: () => request.get('/auth/profile'),
}

export const customerApi = {
  getList: (params) => request.get('/customers', { params }),
  getById: (id) => request.get(`/customers/${id}`),
  create: (data) => request.post('/customers', data),
  update: (id, data) => request.put(`/customers/${id}`, data),
  delete: (id) => request.delete(`/customers/${id}`),
  checkDuplicates: (data) => request.post('/customers/check-duplicates', data),
  merge: (data) => request.post('/customers/merge', data),
  createVisit: (data) => request.post('/customers/visits', data),
}

export const orderApi = {
  getList: (params) => request.get('/orders', { params }),
  getById: (id) => request.get(`/orders/${id}`),
  create: (data) => request.post('/orders', data),
  update: (id, data) => request.put(`/orders/${id}`, data),
  delete: (id) => request.delete(`/orders/${id}`),
  assign: (id, data) => request.post(`/orders/${id}/assign`, data),
  updateStatus: (id, data) => request.put(`/orders/${id}/status`, data),
  confirmDraft: (id) => request.post(`/orders/${id}/confirm-draft`),
}

export const productApi = {
  getList: (params) => request.get('/products', { params }),
  getById: (id) => request.get(`/products/${id}`),
  getCategories: () => request.get('/products/categories'),
  create: (data) => request.post('/products', data),
  update: (id, data) => request.put(`/products/${id}`, data),
  updateStock: (id, data) => request.put(`/products/${id}/stock`, data),
  delete: (id) => request.delete(`/products/${id}`),
}

export const branchApi = {
  getList: (params) => request.get('/branches', { params }),
  getById: (id) => request.get(`/branches/${id}`),
  create: (data) => request.post('/branches', data),
  update: (id, data) => request.put(`/branches/${id}`, data),
  delete: (id) => request.delete(`/branches/${id}`),
}

export const employeeApi = {
  getList: (params) => request.get('/employees', { params }),
  getById: (id) => request.get(`/employees/${id}`),
  create: (data) => request.post('/employees', data),
  update: (id, data) => request.put(`/employees/${id}`, data),
  resetPassword: (id, data) => request.put(`/employees/${id}/password`, data),
}

export const departmentApi = {
  getTree: (params) => request.get('/departments/tree', { params }),
}

export const logisticsApi = {
  getDeliveryPersons: (params) => request.get('/logistics/delivery-persons', { params }),
  getAvailable: (params) => request.get('/logistics/delivery-persons/available', { params }),
  createDeliveryPerson: (data) => request.post('/logistics/delivery-persons', data),
  getPendingOrders: (params) => request.get('/logistics/pending-orders', { params }),
  assign: (data) => request.post('/logistics/assign', data),
  autoAssign: (params) => request.post('/logistics/auto-assign', null, { params }),
}

export const settlementApi = {
  getList: (params) => request.get('/settlements', { params }),
  getById: (id) => request.get(`/settlements/${id}`),
  preview: (data) => request.post('/settlements/preview', data),
  create: (data) => request.post('/settlements', data),
  downloadPdf: (id) => request.get(`/settlements/${id}/pdf`, { responseType: 'blob' }),
}

export const workPlanApi = {
  getSchedules: (params) => request.get('/workplans/schedules', { params }),
  createSchedule: (data) => request.post('/workplans/schedules', data),
  getPlans: (params) => request.get('/workplans/plans', { params }),
  createPlan: (data) => request.post('/workplans/plans', data),
  getDailyPlan: (params) => request.get('/workplans/plans/daily', { params }),
}

export const receivableApi = {
  getList: (params) => request.get('/orders', { params }),
  getById: (id) => request.get(`/orders/${id}`),
}

export const systemApi = {
  getRoles: () => request.get('/system/roles'),
  createRole: (data) => request.post('/system/roles', data),
  getPermissionTree: () => request.get('/system/permissions/tree'),
  getRolePermissions: (roleId) => request.get(`/system/permissions/role/${roleId}`),
  updateRolePermissions: (data) => request.put('/system/permissions/role', data),
  getWeChatConfig: () => request.get('/system/settings/wechat'),
  saveWeChatConfig: (data) => request.put('/system/settings/wechat', data),
  testWeChat: () => request.post('/system/settings/wechat/test'),
  getLogs: (params) => request.get('/system/logs', { params }),
  getWebhooks: (params) => request.get('/system/webhooks', { params }),
  createWebhook: (data) => request.post('/system/webhooks', data),
  getBackups: (params) => request.get('/system/backups', { params }),
  createBackup: () => request.post('/system/backups'),
  getSyncStatus: () => request.get('/system/sync/status'),
  startSync: () => request.post('/system/sync/start'),
}
