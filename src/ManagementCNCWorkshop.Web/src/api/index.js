import http from './http'

export const authApi = {
  login: (data) => http.post('/auth/login', data)
}

export const adminApi = {
  demoInfo: () => http.get('/admin/master/demo-info'),
  workshops: () => http.get('/admin/master/workshops'),
  createWorkshop: (data) => http.post('/admin/master/workshops', data),
  updateWorkshop: (id, data) => http.put(`/admin/master/workshops/${id}`, data),
  deleteWorkshop: (id) => http.delete(`/admin/master/workshops/${id}`),
  employees: (params) => http.get('/admin/master/employees', { params }),
  createEmployee: (data) => http.post('/admin/master/employees', data),
  updateEmployee: (id, data) => http.put(`/admin/master/employees/${id}`, data),
  deleteEmployee: (id) => http.delete(`/admin/master/employees/${id}`),
  products: () => http.get('/admin/master/products'),
  createProduct: (data) => http.post('/admin/master/products', data),
  updateProduct: (id, data) => http.put(`/admin/master/products/${id}`, data),
  deleteProduct: (id) => http.delete(`/admin/master/products/${id}`),
  equipments: (params) => http.get('/admin/master/equipments', { params }),
  createEquipment: (data) => http.post('/admin/master/equipments', data),
  updateEquipment: (id, data) => http.put(`/admin/master/equipments/${id}`, data),
  deleteEquipment: (id) => http.delete(`/admin/master/equipments/${id}`),
  uploadImage: (file) => {
    const fd = new FormData()
    fd.append('file', file)
    return http.post('/admin/upload', fd)
  },
  workReports: (params) => http.get('/admin/work-reports', { params }),
  deleteWorkReport: (id) => http.delete(`/admin/work-reports/${id}`),
  productionDaily: (params) => http.get('/admin/stats/production/daily', { params }),
  productionWeekly: (params) => http.get('/admin/stats/production/weekly', { params }),
  productionMonthly: (params) => http.get('/admin/stats/production/monthly', { params }),
  productionProcess: (params) => http.get('/worker/stats/production/process', { params }),
  qualityRecords: (params) => http.get('/admin/quality', { params }),
  deleteQualityRecord: (id) => http.delete(`/admin/quality/${id}`),
  qualityStats: (type, params) => http.get(`/admin/quality/stats/${type}`, { params }),
  qualityProcessStats: (params) => http.get('/admin/quality/stats/process', { params }),
  qualityTrend: (params) => http.get('/worker/stats/quality/trend', { params }),
  equipmentTime: (params) => http.get('/admin/equipment-time', { params }),
  equipmentTimeSummary: (params) => http.get('/admin/equipment-time/summary', { params }),
  saveEquipmentTime: (data) => http.post('/admin/equipment-time', data),
  deleteEquipmentTime: (id) => http.delete(`/admin/equipment-time/${id}`),
  maintenancePlans: () => http.get('/admin/maintenance/plans'),
  createMaintenancePlan: (data) => http.post('/admin/maintenance/plans', data),
  maintenanceReminders: (params) => http.get('/admin/maintenance/reminders/pending', { params }),
  generateReminders: () => http.post('/admin/maintenance/reminders/generate'),
  completeMaintenanceReminder: (id) => http.post(`/admin/maintenance/reminders/${id}/complete`),
  processFlows: (params) => http.get('/admin/process-flows', { params }),
  processFlowDetail: (id) => http.get(`/admin/process-flows/${id}`),
  createProcessFlow: (data) => http.post('/admin/process-flows', data),
  updateProcessFlow: (id, data) => http.put(`/admin/process-flows/${id}`, data),
  deleteProcessFlow: (id) => http.delete(`/admin/process-flows/${id}`),
  activateProcessFlow: (id) => http.post(`/admin/process-flows/${id}/activate`),
  deactivateProcessFlow: (id) => http.post(`/admin/process-flows/${id}/deactivate`),
  processCards: (params) => http.get('/admin/process-cards', { params }),
  processCardDetail: (id) => http.get(`/admin/process-cards/${id}`),
  createProcessCard: (data) => http.post('/admin/process-cards', data),
  advanceProcessCard: (id, data) => http.post(`/admin/process-cards/${id}/advance`, data),
  completeProcessCard: (id, data) => http.post(`/admin/process-cards/${id}/complete`, data),
  rollbackProcessCard: (id) => http.post(`/admin/process-cards/${id}/rollback`),
  deleteProcessCard: (id) => http.delete(`/admin/process-cards/${id}`)
}
