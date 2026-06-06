import request from './request'

export const authApi = {
  login: (data) => request.post('/auth/login', data),
  logout: () => request.post('/auth/logout'),
  changePassword: (data) => request.post('/auth/change-password', data),
  getProfile: () => request.get('/auth/profile'),
}
