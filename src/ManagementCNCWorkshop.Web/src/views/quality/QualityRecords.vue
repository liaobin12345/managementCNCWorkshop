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
      <el-table-column label="检验时间" width="160">
        <template #default="{ row }">{{ formatDateTime(row.recordDate) }}</template>
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
      <el-table-column prop="inspector.name" label="质检员" width="90" />
      <el-table-column prop="sampleQty" label="抽检数" width="90" align="right" />
      <el-table-column prop="qualifiedQty" label="合格" width="90" align="right" />
      <el-table-column prop="defectQty" label="不良" width="90" align="right" />
      <el-table-column label="合格率" width="90" align="right">
        <template #default="{ row }">
          {{ row.sampleQty ? (Number(row.qualifiedQty) / Number(row.sampleQty) * 100).toFixed(1) : '0.0' }}%
        </template>
      </el-table-column>
      <el-table-column prop="defectType" label="不良类型" width="130" />
      <el-table-column prop="remark" label="备注" min-width="120" show-overflow-tooltip />
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
    const res = await adminApi.qualityRecords(params)
    list.value = res.items
    total.value = res.total
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  workshops.value = await adminApi.workshops()
  await load()
})
</script>