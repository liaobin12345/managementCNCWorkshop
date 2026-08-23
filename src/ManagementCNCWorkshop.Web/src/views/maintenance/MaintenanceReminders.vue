<template>
  <el-card shadow="hover">
    <div class="toolbar">
      <div class="toolbar-left">
        <el-button type="primary" :loading="generating" @click="onGenerate">
          <el-icon><Refresh /></el-icon> 生成提醒
        </el-button>
        <span class="hint">根据保养计划的“下次保养日期 - 提前提醒天数”自动生成待处理提醒</span>
      </div>
    </div>

    <el-alert v-if="overdueCount > 0" type="warning" :closable="false" class="page-card"
      :title="`有 ${overdueCount} 条提醒已逾期，请尽快安排保养！`" />

    <el-table :data="list" stripe v-loading="loading">
      <el-table-column prop="equipment.name" label="设备" width="170" />
      <el-table-column prop="plan.planName" label="计划" width="150" />
      <el-table-column label="应保养日期" width="120">
        <template #default="{ row }">{{ formatDate(row.dueDate) }}</template>
      </el-table-column>
      <el-table-column label="提醒日期" width="120">
        <template #default="{ row }">{{ formatDate(row.remindDate) }}</template>
      </el-table-column>
      <el-table-column label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="reminderStatusType[row.status] || 'info'" size="small">
            {{ reminderStatusLabel[row.status] || row.status }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="plan.content" label="保养内容" min-width="220" show-overflow-tooltip />
    </el-table>
  </el-card>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { adminApi } from '../../api'
import { formatDate, reminderStatusLabel, reminderStatusType } from '../../utils/format'

const list = ref([])
const loading = ref(false)
const generating = ref(false)

const overdueCount = computed(() => list.value.filter((x) => x.status === 'Overdue').length)

async function load() {
  loading.value = true
  try {
    list.value = await adminApi.maintenanceReminders()
  } finally {
    loading.value = false
  }
}

async function onGenerate() {
  generating.value = true
  try {
    const res = await adminApi.generateReminders()
    ElMessage.success(`已生成 ${res.created} 条保养提醒`)
    await load()
  } finally {
    generating.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.hint {
  color: #909399;
  font-size: 13px;
}
</style>
