import axios from 'axios'
import { ElMessage } from 'element-plus'
import router from '@/router'

const request = axios.create({
  baseURL: '/api',
  timeout: 30000,
})

// Token 刷新防抖 — 避免多个401并发触发多次刷新
let isRefreshing = false
let refreshSubscribers = []

function subscribeTokenRefresh(cb) {
  refreshSubscribers.push(cb)
}
function onTokenRefreshed(newToken) {
  refreshSubscribers.forEach(cb => cb(newToken))
  refreshSubscribers = []
}

// 请求拦截器 - 自动添加 Token
request.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => Promise.reject(error)
)

// 响应拦截器 - 统一处理
request.interceptors.response.use(
  (response) => {
    return response.data
  },
  async (error) => {
    const { config, response } = error

    if (response) {
      switch (response.status) {
        case 401:
          // Token 过期 — 尝试静默刷新
          const refreshToken = localStorage.getItem('refreshToken')
          if (refreshToken && !config._retry) {
            if (isRefreshing) {
              // 等待已有刷新完成
              return new Promise(resolve => {
                subscribeTokenRefresh(newToken => {
                  config.headers.Authorization = `Bearer ${newToken}`
                  resolve(request(config))
                })
              })
            }

            isRefreshing = true
            config._retry = true
            try {
              const { data } = await axios.post('/api/auth/refresh-token', JSON.stringify(refreshToken), {
                headers: { 'Content-Type': 'application/json' },
              })
              const refreshed = data?.data
              if (!data?.success || !refreshed?.token) {
                throw new Error(data?.message || 'Token刷新失败')
              }

              localStorage.setItem('token', refreshed.token)
              if (refreshed.refreshToken) localStorage.setItem('refreshToken', refreshed.refreshToken)
              onTokenRefreshed(refreshed.token)
              isRefreshing = false
              config.headers.Authorization = `Bearer ${refreshed.token}`
              return request(config)
            } catch {
              isRefreshing = false
              refreshSubscribers = []
              localStorage.removeItem('token')
              localStorage.removeItem('refreshToken')
              localStorage.removeItem('user')
              router.push('/login')
              ElMessage.error('登录已过期，请重新登录')
            }
          } else {
            // 无 refreshToken — 直接登出
            localStorage.removeItem('token')
            localStorage.removeItem('refreshToken')
            localStorage.removeItem('user')
            router.push('/login')
            ElMessage.error('登录已过期，请重新登录')
          }
          break
        case 403:
          ElMessage.error('权限不足')
          break
        case 500:
          ElMessage.error('服务器错误，请稍后重试')
          break
        default:
          ElMessage.error(response.data?.message || '请求失败')
      }
    } else {
      ElMessage.error('网络错误，请检查连接')
    }
    return Promise.reject(error)
  }
)

export default request
