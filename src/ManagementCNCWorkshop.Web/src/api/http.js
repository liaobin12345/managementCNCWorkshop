import axios from 'axios'
import { ElMessage } from 'element-plus'

const http = axios.create({
  baseURL: '/api',
  timeout: 15000
})

http.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

http.interceptors.response.use(
  (res) => res.data,
  (err) => {
    const status = err.response?.status
    const data = err.response?.data
    if (status === 401) {
      if (location.pathname !== '/login') {
        ElMessage.error(data?.message || '登录已过期，请重新登录')
        localStorage.removeItem('token')
        localStorage.removeItem('employee')
        location.href = '/login'
      } else {
        ElMessage.error(data?.message || '账号或密码错误')
      }
    } else {
      ElMessage.error(data?.message || data?.title || `请求失败（${status || '网络错误'}）`)
    }
    return Promise.reject(err)
  }
)

export default http
