<template>
  <el-card shadow="hover">
    <div class="toolbar">
      <div class="toolbar-left">
        <el-radio-group v-model="statusFilter" @change="load">
          <el-radio-button value="">待处理</el-radio-button>
          <el-radio-button value="Completed">已完成</el-radio-button>
          <el-radio-button value="Overdue">已逾期</el-radio-button>
        </el-radio-group>
        <el-button type="primary" :loading="generating" @click="onGenerate">
          <el-icon><Refresh /></el-icon> 生成提醒
        </el-button>
        <span class="hint">根据保养计划的“下次保养日期 - 提前提醒天数”自动生成待处理提醒</span>
      </div>
    </div>

    <el-alert v-if="overdueCount > 0 && !statusFilter" type="warning" :closable="false" class="page-card"
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
      <el-table-column label="完成信息" width="150">
        <template #default="{ row }">
          <template v-if="row.completedAt">
            <div>{{ row.completedBy?.name || row.completedByName || '-' }}</div>
            <div class="sub">{{ formatDateTime(row.completedAt) }}</div>
          </template>
          <span v-else class="muted">-</span>
        </template>
      </el-table-column>
      <el-table-column prop="plan.content" label="保养内容" min-width="220" show-overflow-tooltip />
      <el-table-column label="操作" width="110" fixed="right">
        <template #default="{ row }">
          <el-button v-if="row.status !== 'Completed'" link type="success" :loading="completingId === row.id"
            @click="onComplete(row)">标记完成</el-button>
          <span v-else class="muted">-</span>
        </template>
      </el-table-column>
    </el-table>
  </el-card>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { adminApi } from '../../api'
import { formatDate, formatDateTime, reminderStatusLabel, reminderStatusType } from '../../utils/format'

const list = ref([])
const loading = ref(false)
const generating = ref(false)
const completingId = ref(null)
const statusFilter = ref('')

const overdueCount = computed(() => list.value.filter((x) => x.status === 'Overdue').length)

async function load() {
  loading.value = true
  try {
    const params = {}
    if (statusFilter.value) params.status = statusFilter.value
    list.value = await adminApi.maintenanceReminders(params)
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

async function onComplete(row) {
  try {
    await ElMessageBox.confirm(
      `确认已完成「${row.equipment?.name || '设备'}」的${row.plan?.planName || '保养'}？\n完成后将自动推进下次保养日期。`,
      '标记完成',
      { type: 'success', confirmButtonText: '确认完成', cancelButtonText: '取消' }
    )
  } catch {
    return
  }
  completingId.value = row.id
  try {
    await adminApi.completeMaintenanceReminder(row.id)
    ElMessage.success('已标记完成')
    await load()
  } finally {
    completingId.value = null
  }
}

onMounted(load)
</script>

<style scoped>
.hint {
  color: #909399;
  font-size: 13px;
}

.sub {
  font-size: 12px;
  color: #909399;
}

.muted {
  color: #c0c4cc;
}
</style>
