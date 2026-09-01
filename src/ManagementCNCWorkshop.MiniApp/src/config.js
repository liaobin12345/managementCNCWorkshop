// 后端 API 地址配置
// ─ H5 构建（含外网 UAT 隧道）：相对路径 /api，走同源（隧道域名或部署域名）
// ─ 微信小程序 / 模拟器（本机）：http://localhost:5219/api
// ─ 真机调试：必须用电脑的局域网 IP，如 http://192.168.101.242:5219/api
//   （后端需已绑定 0.0.0.0，见 scripts/dev-api-watch.sh）
// ─ 生产环境：改为正式域名
// #ifdef H5
export const BASE_URL = '/api'
// #endif
// #ifndef H5
export const BASE_URL = 'http://192.168.101.242:5219/api'
// #endif
