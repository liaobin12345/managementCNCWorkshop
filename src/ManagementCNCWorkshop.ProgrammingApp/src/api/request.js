import { BASE_URL } from '../config'

export function request(options) {
  return new Promise((resolve, reject) => {
    const token = uni.getStorageSync('prog_token')
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
          uni.removeStorageSync('prog_token')
          uni.removeStorageSync('prog_user')
          uni.showToast({ title: '登录已过期，请重新登录', icon: 'none' })
          setTimeout(() => uni.reLaunch({ url: '/pages/login/login' }), 600)
          reject(data)
        } else {
          const msg = (data && data.message) || (data && data.title) || `请求失败(${statusCode})`
          uni.showToast({ title: msg, icon: 'none' })
          reject(data)
        }
      },
      fail: () => {
        uni.showToast({ title: '网络异常，请确认后端服务已启动', icon: 'none' })
        reject(new Error('network'))
      },
    })
  })
}

export const get = (url, data) => request({ url, method: 'GET', data })
export const post = (url, data) => request({ url, method: 'POST', data })
export const put = (url, data) => request({ url, method: 'PUT', data })
export const del = (url, data) => request({ url, method: 'DELETE', data })

// 文件上传（multipart/form-data），返回后端 JSON
export function upload(url, filePath, name = 'file') {
  return new Promise((resolve, reject) => {
    const token = uni.getStorageSync('prog_token')
    uni.uploadFile({
      url: BASE_URL + url,
      filePath,
      name,
      header: token ? { Authorization: `Bearer ${token}` } : {},
      success: (res) => {
        const { statusCode, data } = res
        try {
          const parsed = typeof data === 'string' ? JSON.parse(data) : data
          if (statusCode >= 200 && statusCode < 300) resolve(parsed)
          else {
            const msg = (parsed && parsed.message) || `请求失败(${statusCode})`
            uni.showToast({ title: msg, icon: 'none' })
            reject(parsed)
          }
        } catch (e) {
          uni.showToast({ title: '上传返回异常', icon: 'none' })
          reject(e)
        }
      },
      fail: () => {
        uni.showToast({ title: '网络异常，请确认后端服务已启动', icon: 'none' })
        reject(new Error('network'))
      },
    })
  })
}
