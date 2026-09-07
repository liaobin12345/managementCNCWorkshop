// 后端 API 地址配置
// ─ H5 构建（含外网 UAT 隧道）：相对路径 /api，走同源
// ─ 微信小程序 / 模拟器（本机）：http://localhost:5219/api
// ─ 真机调试：必须用电脑的局域网 IP
// #ifdef H5
export const BASE_URL = '/api'
// #endif
// #ifndef H5
export const BASE_URL = 'http://127.0.0.1:5219/api'
// #endif