// 登录守卫：页面 onShow 时调用，未登录跳回登录页
export function requireAuth() {
  const token = uni.getStorageSync('token')
  if (!token) {
    uni.reLaunch({ url: '/pages/login/login' })
    return false
  }
  return true
}

export function currentEmployee() {
  return uni.getStorageSync('employee') || null
}

// 数字千分位
export function fmtQty(n) {
  const num = Number(n || 0)
  return num.toLocaleString('zh-CN', { maximumFractionDigits: 2 })
}

// 0~1 比例转百分比显示，如 0.98 -> 98.2%
export function fmtPercent(p) {
  const num = Number(p || 0)
  return (num * 100).toFixed(num === 1 ? 0 : 1) + '%'
}

// 日期格式化
export function fmtDate(dt) {
  if (!dt) return '-'
  const d = new Date(dt)
  if (isNaN(d.getTime())) return String(dt).slice(0, 10)
  const p = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

export function fmtDateTime(dt) {
  if (!dt) return '-'
  const d = new Date(dt)
  if (isNaN(d.getTime())) return String(dt)
  const p = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`
}

export function fmtMonthDay(dt) {
  if (!dt) return '-'
  const d = new Date(dt)
  if (isNaN(d.getTime())) return String(dt).slice(5, 10)
  const p = (n) => String(n).padStart(2, '0')
  return `${p(d.getMonth() + 1)}-${p(d.getDate())}`
}

// 时分格式化
export function fmtTime(dt) {
  if (!dt) return '-'
  const d = new Date(dt)
  if (isNaN(d.getTime())) return String(dt)
  const p = (n) => String(n).padStart(2, '0')
  return `${p(d.getHours())}:${p(d.getMinutes())}`
}

// 角色名称
export function roleName(role) {
  const map = { Admin: '管理员', Worker: '操作工', Inspector: '质检员', Programmer: '技术员' }
  return map[role] || role || '-'
}

// 设备状态
export function equipStatus(status) {
  const map = { Running: '运行中', Idle: '空闲', Fault: '故障' }
  return map[status] || status || '-'
}

export function equipStatusTag(status) {
  const map = { Running: 'green', Idle: 'blue', Fault: 'red' }
  return map[status] || 'gray'
}

// 提醒状态
export function reminderStatus(status) {
  const map = { Pending: '待处理', Overdue: '已逾期', Completed: '已完成' }
  return map[status] || status || '-'
}

export function reminderStatusTag(status) {
  const map = { Pending: 'amber', Overdue: 'red', Completed: 'green' }
  return map[status] || 'gray'
}

// 今天是周几
export function todayText() {
  const d = new Date()
  const week = ['日', '一', '二', '三', '四', '五', '六'][d.getDay()]
  return `${d.getFullYear()}年${d.getMonth() + 1}月${d.getDate()}日 周${week}`
}

// 当前 ISO 周
export function currentISOWeek() {
  const d = new Date()
  const day = (d.getDay() + 6) % 7
  const target = new Date(d.getFullYear(), d.getMonth(), d.getDate() + 3 - day)
  const first = new Date(target.getFullYear(), 0, 1)
  const diff = Math.round((target - first) / 86400000)
  const week = Math.floor((diff + first.getDay() - 1) / 7) + 1
  return { year: target.getFullYear(), week }
}
