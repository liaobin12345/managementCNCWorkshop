<template>
  <div>
    <!-- 顶部统计卡片 -->
    <el-row :gutter="16">
      <el-col :span="6" v-for="card in cards" :key="card.label">
        <el-card shadow="hover" class="stat-card">
          <div class="stat-icon" :style="{ background: card.bg, color: card.color }">
            <el-icon :size="26"><component :is="card.icon" /></el-icon>
          </div>
          <div>
            <div class="stat-label">{{ card.label }}</div>
            <div class="stat-value" :style="{ color: card.color }">{{ card.value }}</div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 今日产量图表 -->
    <el-card shadow="hover" class="page-card mt">
      <template #header>
        <div class="card-header">
          <span>今日产量统计（按产品）</span>
          <el-date-picker v-model="date" type="date" placeholder="选择日期" :clearable="false"
            value-format="YYYY-MM-DD" size="small" @change="loadDaily" />
        </div>
      </template>
      <BaseChart :option="productionOption" :height="320" />
    </el-card>

    <!-- 最近报工 -->
    <el-card shadow="hover" class="page-card">
      <template #header>最近报工记录</template>
      <el-table :data="recentReports" stripe size="default">
        <el-table-column prop="reportDate" label="报工时间" width="160">
          <template #default="{ row }">{{ formatDateTime(row.reportDate) }}</template>
        </el-table-column>
        <el-table-column prop="workshop.name" label="车间" width="120" />
        <el-table-column prop="product.name" label="产品" />
        <el-table-column prop="employee.name" label="员工" width="100" />
        <el-table-column prop="equipment.name" label="设备" width="140">
          <template #default="{ row }">{{ row.equipment?.name || '-' }}</template>
        </el-table-column>
        <el-table-column prop="quantity" label="总产量" width="90" align="right" />
        <el-table-column prop="qualifiedQty" label="合格" width="90" align="right" />
        <el-table-column prop="defectQty" label="不良" width="90" align="right" />
      </el-table>
    </el-card>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import BaseChart from '../components/BaseChart.vue'
import { adminApi } from '../api'
import { formatDateTime } from '../utils/format'

const date = ref(new Date().toISOString().slice(0, 10))
const dailyStats = ref([])
const recentReports = ref([])
const reminders = ref([])
const productMap = ref({})

const cards = computed(() => {
  const today = dailyStats.value.reduce(
    (acc, x) => {
      acc.total += Number(x.totalQty || 0)
      acc.qualified += Number(x.qualifiedQty || 0)
      acc.defect += Number(x.defectQty || 0)
      return acc
    },
    { total: 0, qualified: 0, defect: 0 }
  )
  const overdue = reminders.value.filter((x) => x.status === 'Overdue').length
  return [
    { label: '今日总产量', value: today.total, icon: 'Box', color: '#409eff', bg: '#ecf5ff' },
    { label: '今日合格数', value: today.qualified, icon: 'CircleCheck', color: '#67c23a', bg: '#f0f9eb' },
    { label: '今日不良数', value: today.defect, icon: 'Warning', color: '#f56c6c', bg: '#fef0f0' },
    { label: '逾期保养提醒', value: overdue, icon: 'Tools', color: '#e6a23c', bg: '#fdf6ec' }
  ]
})

const productionOption = computed(() => {
  const byProduct = new Map()
  for (const s of dailyStats.value) {
    const name = productMap.value[s.productId] || `产品 #${s.productId}`
    const cur = byProduct.get(name) || { total: 0, qualified: 0, defect: 0 }
    cur.total += Number(s.totalQty || 0)
    cur.qualified += Number(s.qualifiedQty || 0)
    cur.defect += Number(s.defectQty || 0)
    byProduct.set(name, cur)
  }
  const names = [...byProduct.keys()]
  return {
    tooltip: { trigger: 'axis' },
    legend: { data: ['总产量', '合格', '不良'] },
    grid: { left: 40, right: 20, top: 40, bottom: 30 },
    xAxis: { type: 'category', data: names },
    yAxis: { type: 'value' },
    series: [
      { name: '总产量', type: 'bar', data: names.map((n) => byProduct.get(n).total), itemStyle: { color: '#409eff' } },
      { name: '合格', type: 'bar', data: names.map((n) => byProduct.get(n).qualified), itemStyle: { color: '#67c23a' } },
      { name: '不良', type: 'bar', data: names.map((n) => byProduct.get(n).defect), itemStyle: { color: '#f56c6c' } }
    ]
  }
})

async function loadDaily() {
  const [stats, products] = await Promise.all([
    adminApi.productionDaily({ date: date.value }),
    adminApi.products()
  ])
  dailyStats.value = stats
  productMap.value = Object.fromEntries(products.map((p) => [p.id, `${p.name}（${p.code}）`]))
}

async function load() {
  await loadDaily()
  const [reports, rems] = await Promise.all([
    adminApi.workReports({ page: 1, pageSize: 8 }),
    adminApi.maintenanceReminders()
  ])
  recentReports.value = reports.items
  reminders.value = rems
}

onMounted(load)
</script>

<style scoped>
.stat-card {
  margin-bottom: 16px;
}

.stat-card :deep(.el-card__body) {
  display: flex;
  align-items: center;
  gap: 12px;
}

.stat-icon {
  width: 48px;
  height: 48px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 24px;
}

.stat-label {
  color: #909399;
  font-size: 13px;
}

.stat-value {
  font-size: 26px;
  font-weight: 700;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
</style>
