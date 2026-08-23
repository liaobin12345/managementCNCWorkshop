import http from './http'

export const authApi = {
  login: (data) => http.post('/auth/login', data)
}

export const adminApi = {
  demoInfo: () => http.get('/admin/master/demo-info'),
  workshops: () => http.get('/admin/master/workshops'),
  createWorkshop: (data) => http.post('/admin/master/workshops', data),
  employees: (params) => http.get('/admin/master/employees', { params }),
  createEmployee: (data) => http.post('/admin/master/employees', data),
  products: () => http.get('/admin/master/products'),
  createProduct: (data) => http.post('/admin/master/products', data),
  equipments: (params) => http.get('/admin/master/equipments', { params }),
  createEquipment: (data) => http.post('/admin/master/equipments', data),
  workReports: (params) => http.get('/admin/work-reports', { params }),
  productionDaily: (params) => http.get('/admin/stats/production/daily', { params }),
  productionWeekly: (params) => http.get('/admin/stats/production/weekly', { params }),
  productionMonthly: (params) => http.get('/admin/stats/production/monthly', { params }),
  qualityRecords: (params) => http.get('/admin/quality', { params }),
  qualityStats: (type, params) => http.get(`/admin/quality/stats/${type}`, { params }),
  maintenancePlans: () => http.get('/admin/maintenance/plans'),
  createMaintenancePlan: (data) => http.post('/admin/maintenance/plans', data),
  maintenanceReminders: (params) => http.get('/admin/maintenance/reminders/pending', { params }),
  generateReminders: () => http.post('/admin/maintenance/reminders/generate')
}
