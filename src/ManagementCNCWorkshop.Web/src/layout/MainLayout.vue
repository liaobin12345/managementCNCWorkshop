<template>
  <el-container class="layout">
    <el-aside width="220px" class="aside">
      <div class="logo">
        <span class="logo-icon">⚙️</span>
        <span>CNC 车间运营后台</span>
      </div>
      <el-menu :default-active="$route.path" router class="menu">
        <el-menu-item index="/dashboard">
          <el-icon><DataAnalysis /></el-icon>
          <span>首页看板</span>
        </el-menu-item>
        <el-sub-menu index="master">
          <template #title>
            <el-icon><OfficeBuilding /></el-icon>
            <span>主数据</span>
          </template>
          <el-menu-item index="/master/workshops">车间管理</el-menu-item>
          <el-menu-item index="/master/employees">员工管理</el-menu-item>
          <el-menu-item index="/master/products">产品管理</el-menu-item>
          <el-menu-item index="/master/equipments">设备管理</el-menu-item>
        </el-sub-menu>
        <el-sub-menu index="production">
          <template #title>
            <el-icon><Odometer /></el-icon>
            <span>生产管理</span>
          </template>
          <el-menu-item index="/production/work-reports">报工记录</el-menu-item>
          <el-menu-item index="/production/production-stats">产量统计</el-menu-item>
        </el-sub-menu>
        <el-sub-menu index="quality">
          <template #title>
            <el-icon><Stamp /></el-icon>
            <span>质量管理</span>
          </template>
          <el-menu-item index="/quality/records">质检记录</el-menu-item>
          <el-menu-item index="/quality/stats">质量统计</el-menu-item>
        </el-sub-menu>
        <el-sub-menu index="maintenance">
          <template #title>
            <el-icon><Tools /></el-icon>
            <span>设备保养</span>
          </template>
          <el-menu-item index="/maintenance/plans">保养计划</el-menu-item>
          <el-menu-item index="/maintenance/reminders">保养提醒</el-menu-item>
        </el-sub-menu>
      </el-menu>
    </el-aside>

    <el-container>
      <el-header class="header">
        <div class="header-title">{{ route.meta.title || '' }}</div>
        <el-dropdown @command="onCommand">
          <span class="user">
            <el-avatar :size="28" class="avatar">
              {{ (auth.employee?.name || '?').charAt(0) }}
            </el-avatar>
            <span class="user-name">{{ auth.employee?.name }}</span>
            <el-tag size="small" :type="roleType[auth.employee?.role] || 'info'">
              {{ roleLabel[auth.employee?.role] || auth.employee?.role }}
            </el-tag>
            <el-icon><ArrowDown /></el-icon>
          </span>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="logout">退出登录</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </el-header>

      <el-main class="main">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup>
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../store/auth'
import { roleLabel, roleType } from '../utils/format'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

function onCommand(cmd) {
  if (cmd === 'logout') {
    auth.logout()
    router.push('/login')
  }
}
</script>

<style scoped>
.layout {
  height: 100vh;
}

.aside {
  background: #fff;
  border-right: 1px solid #e4e7ed;
}

.logo {
  height: 60px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  font-size: 16px;
  font-weight: 600;
  color: #303133;
  border-bottom: 1px solid #e4e7ed;
}

.logo-icon {
  font-size: 20px;
}

.menu {
  border-right: none;
}

.header {
  background: #fff;
  border-bottom: 1px solid #e4e7ed;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.header-title {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.user {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  color: #606266;
}

.avatar {
  background: #409eff;
  color: #fff;
}

.main {
  overflow: auto;
}
</style>
