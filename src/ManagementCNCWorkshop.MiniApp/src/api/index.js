import { get, post } from './request'

// ── 认证 ──
export const apiLogin = (data) => post('/auth/login', data)
export const apiWechatLogin = (code) => post('/auth/wechat-login', { code })
export const apiWechatBind = (data) => post('/auth/wechat-bind', data)

// ── 基础查询 ──
export const apiMe = () => get('/worker/me')
export const apiWorkshops = () => get('/worker/workshops')
export const apiProducts = () => get('/worker/products')
export const apiProductByQr = (code) => get('/worker/products/by-qrcode', { code })
export const apiEquipments = () => get('/worker/equipments')
export const apiEquipmentByQr = (code) => get('/worker/equipments/by-qrcode', { code })

// ── 扫码报工 ──
export const apiScanReport = (data) => post('/worker/work-reports/scan', data)
export const apiMyReports = (data) => get('/worker/work-reports/my', data)

// ── 统计 ──
export const apiSummary = (workshopId) => get('/worker/stats/summary', workshopId ? { workshopId } : {})
export const apiProductionDaily = (params) => get('/worker/stats/production/daily', params)
export const apiProductionWeekly = (params) => get('/worker/stats/production/weekly', params)
export const apiProductionMonthly = (params) => get('/worker/stats/production/monthly', params)
export const apiProductionTrend = (days = 7) => get('/worker/stats/production/trend', { days })
export const apiProductionProcess = (params) => get('/worker/stats/production/process', params)
export const apiQualityDaily = (params) => get('/worker/stats/quality/daily', params)
export const apiQualityWeekly = (params) => get('/worker/stats/quality/weekly', params)
export const apiQualityMonthly = (params) => get('/worker/stats/quality/monthly', params)
export const apiQualityProcessStats = (params) => get('/worker/stats/quality/process', params)

// ── 质检 ──
export const apiCreateQuality = (data) => post('/worker/quality', data)

// ── 设备时间 ──
export const apiGetEquipmentTime = (equipmentId, date) => get('/worker/equipment-time', { equipmentId, date })
export const apiRecentEquipmentTime = (equipmentId, days = 7) => get('/worker/equipment-time/recent', { equipmentId, days })
export const apiSaveEquipmentTime = (data) => post('/worker/equipment-time', data)

// ── 保养 ──
export const apiRemindersPending = (params) => get('/worker/maintenance/reminders/pending', params)
export const apiMaintenancePlans = () => get('/worker/maintenance/plans')
export const apiCompleteReminder = (id) => post(`/worker/maintenance/reminders/${id}/complete`)

// ── 按设备统计 ──
export const apiEquipmentDaily = (params) => get('/worker/stats/equipment/daily', params)

// ── 工艺流转 ──
export const apiProcessFlows = (params) => get('/worker/process/flows', params)
export const apiProcessCards = (params) => get('/worker/process/cards', params)
export const apiProcessCardDetail = (id) => get(`/worker/process/cards/${id}`)
export const apiAdvanceProcessCard = (id) => post(`/worker/process/cards/${id}/advance`)
export const apiProcessCardProgress = (params) => get('/worker/process/progress', params)

// ── 设备点检 ──
export const apiInspectionTemplate = () => get('/worker/equipment-inspections/template')
export const apiInspectionList = (params) => get('/worker/equipment-inspections', params)
export const apiInspectionDetail = (id) => get(`/worker/equipment-inspections/${id}`)
export const apiInspectionSubmit = (data) => post('/worker/equipment-inspections', data)
export const apiInspectionReport = (params) => get('/worker/equipment-inspections/report', params)
