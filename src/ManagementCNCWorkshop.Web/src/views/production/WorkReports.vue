<template>
  <el-card shadow="hover">
    <div class="toolbar">
      <div class="toolbar-left">
        <el-date-picker v-model="date" type="date" placeholder="选择日期" clearable @change="load" />
        <el-select v-model="workshopId" placeholder="全部车间" clearable style="width: 160px" @change="load">
          <el-option v-for="w in workshops" :key="w.id" :label="w.name" :value="w.id" />
        </el-select>
      </div>
    </div>

    <el-table :data="list" stripe v-loading="loading">
      <el-table-column label="报工时间" width="160">
        <template #default="{ row }">{{ formatDateTime(row.reportDate) }}</template>
      </el-table-column>
      <el-table-column prop="workshop.name" label="车间" width="100" />
      <el-table-column prop="product.name" label="产品" width="140" />
      <el-table-column label="工序" width="150">
        <template #default="{ row }">
          <template v-if="row.processStepNo && row.processStepName">
            <el-tag size="small" type="primary">工序{{ row.processStepNo }}</el-tag>
            <span class="step-name">{{ row.processStepName }}</span>
          </template>
          <span v-else class="muted">-</span>
        </template>
      </el-table-column>
      <el-table-column prop="employee.name" label="员工" width="90" />
      <el-table-column prop="equipment.name" label="设备" width="120">
        <template #default="{ row }">{{ row.equipment?.name || '-' }}</template>
      </el-table-column>
      <el-table-column prop="quantity" label="总产量" width="90" align="right" />
      <el-table-column prop="qualifiedQty" label="合格" width="90" align="right" />
      <el-table-column prop="defectQty" label="不良" width="90" align="right" />
      <el-table-column prop="remark" label="备注" min-width="120" show-overflow-tooltip />
      <el-table-column label="操作" width="80" fixed="right">
        <template #default="{ row }">
          <el-button link type="danger" @click="onDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <div class="pagination">
      <el-pagination background layout="prev, pager, next" :total="total" :page-size="pageSize"
        v-model:current-page="page" @current-change="load" />
    </div>
  </el-card>
</template>

<style scoped>
.step-name {
  margin-left: 6px;
  color: #303133;
}

.muted {
  color: #c0c4cc;
}
</style>

<script setup>
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { adminApi } from '../../api'
import { formatDateTime } from '../../utils/format'

const list = ref([])
const workshops = ref([])
const loading = ref(false)
const date = ref(null)
const workshopId = ref(null)
const page = ref(1)
const pageSize = 20
const total = ref(0)

async function load() {
  loading.value = true
  try {
    const params = { page: page.value, pageSize }
    if (date.value) params.date = date.value
    if (workshopId.value) params.workshopId = workshopId.value
    const res = await adminApi.workReports(params)
    list.value = res.items
    total.value = res.total
  } finally {
    loading.value = false
  }
}

async function onDelete(row) {
  try {
    await ElMessageBox.confirm(
      `确定删除该报工记录？\n产品：${row.product?.name || '-'} · 员工：${row.employee?.name || '-'} · 数量：${row.quantity}\n删除后相关统计会自动重新计算。`,
      '删除报工记录',
      { type: 'error', confirmButtonText: '确认删除', cancelButtonText: '取消' }
    )
  } catch {
    return
  }
  try {
    await adminApi.deleteWorkReport(row.id)
    ElMessage.success('已删除')
    await load()
  } catch {
    /* http 层已提示 */
  }
}

onMounted(async () => {
  workshops.value = await adminApi.workshops()
  await load()
})
</script>