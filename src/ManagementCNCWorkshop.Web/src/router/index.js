import { createRouter, createWebHistory } from 'vue-router'

const routes = [
  {
    path: '/login',
    name: 'login',
    component: () => import('../views/Login.vue'),
    meta: { public: true, title: '登录' }
  },
  {
    path: '/',
    component: () => import('../layout/MainLayout.vue'),
    redirect: '/dashboard',
    children: [
      { path: 'dashboard', component: () => import('../views/Dashboard.vue'), meta: { title: '首页看板' } },
      { path: 'master/workshops', component: () => import('../views/master/Workshops.vue'), meta: { title: '车间管理' } },
      { path: 'master/employees', component: () => import('../views/master/Employees.vue'), meta: { title: '员工管理' } },
      { path: 'master/products', component: () => import('../views/master/Products.vue'), meta: { title: '产品管理' } },
      { path: 'master/equipments', component: () => import('../views/master/Equipments.vue'), meta: { title: '设备管理' } },
      { path: 'production/work-reports', component: () => import('../views/production/WorkReports.vue'), meta: { title: '报工记录' } },
      { path: 'production/production-stats', component: () => import('../views/production/ProductionStats.vue'), meta: { title: '生产看板' } },
      { path: 'quality/records', component: () => import('../views/quality/QualityRecords.vue'), meta: { title: '质检记录' } },
      { path: 'quality/stats', component: () => import('../views/quality/QualityStats.vue'), meta: { title: '质量统计' } },
      { path: 'maintenance/plans', component: () => import('../views/maintenance/MaintenancePlans.vue'), meta: { title: '保养计划' } },
      { path: 'maintenance/reminders', component: () => import('../views/maintenance/MaintenanceReminders.vue'), meta: { title: '保养提醒' } },
      { path: 'process/flows', component: () => import('../views/process/ProcessFlows.vue'), meta: { title: '工艺路线' } },
      { path: 'process/cards', component: () => import('../views/process/ProcessCards.vue'), meta: { title: '工艺流转卡' } },
      { path: 'equipment-time', component: () => import('../views/equipment-time/Index.vue'), meta: { title: '设备时间管理' } }
    ]
  },
  { path: '/:pathMatch(.*)*', redirect: '/dashboard' }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.beforeEach((to) => {
  const token = localStorage.getItem('token')
  if (!to.meta.public && !token) return '/login'
  if (to.path === '/login' && token) return '/dashboard'
})

export default router
