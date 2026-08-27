import { BASE_URL } from '../config'

export function request(options) {
  return new Promise((resolve, reject) => {
    const token = uni.getStorageSync('token')
    uni.request({
      url: BASE_URL + options.url,
      method: options.method || 'GET',
      data: options.data || {},
      header: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...(options.header || {}),
      },
      success: (res) => {
        const { statusCode, data } = res
        if (statusCode >= 200 && statusCode < 300) {
          resolve(data)
        } else if (statusCode === 401) {
          uni.removeStorageSync('token')
          uni.removeStorageSync('employee')
          uni.showToast({ title: (data && data.message) || '登录已过期', icon: 'none' })
          setTimeout(() => uni.reLaunch({ url: '/pages/login/login' }), 600)
          reject(data)
        } else {
          uni.showToast({
            title: (data && data.message) || (data && data.title) || `请求失败(${statusCode})`,
            icon: 'none',
          })
          reject(data)
        }
      },
      fail: (err) => {
        uni.showToast({ title: '网络异常，请确认后端服务已启动', icon: 'none' })
        reject(err)
      },
    })
  })
}

export const get = (url, data) => request({ url, method: 'GET', data })
export const post = (url, data) => request({ url, method: 'POST', data })
