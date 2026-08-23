export function pad(n) {
  return String(n).padStart(2, '0')
}

export function formatDateTime(v) {
  if (!v) return '-'
  const d = new Date(v)
  if (Number.isNaN(d.getTime())) return '-'
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export function formatDate(v) {
  if (!v) return '-'
  const d = new Date(v)
  if (Number.isNaN(d.getTime())) return '-'
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

// ISO 周号（与后端 ISOWeek 计算一致）
export function isoWeek(date) {
  const d = new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate()))
  const dayNum = d.getUTCDay() || 7
  d.setUTCDate(d.getUTCDate() + 4 - dayNum)
  const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1))
  const week = Math.ceil(((d - yearStart) / 86400000 + 1) / 7)
  return { year: d.getUTCFullYear(), week }
}

export const roleLabel = { Admin: '管理员', Worker: '操作工', Inspector: '质检员' }

export const roleType = { Admin: 'danger', Worker: 'primary', Inspector: 'warning' }

export const equipmentStatusLabel = { Running: '运行中', Idle: '空闲', Maintenance: '保养中', Fault: '故障' }

export const equipmentStatusType = { Running: 'success', Idle: 'info', Maintenance: 'warning', Fault: 'danger' }

export const reminderStatusLabel = { Pending: '待处理', Overdue: '已逾期', Completed: '已完成' }

export const reminderStatusType = { Pending: 'warning', Overdue: 'danger', Completed: 'success' }
