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

    <!-- 设备时间利用率 -->
    <el-card shadow="hover" class="page-card mt">
      <template #header>
        <div class="card-header">
          <span>设备时间与利用率</span>
          <el-date-picker v-model="date" type="date" placeholder="选择日期" :clearable="false"
            value-format="YYYY-MM-DD" size="small" @change="loadEquipmentTime" />
        </div>
      </template>
      <el-table :data="equipmentSummary" stripe>
        <el-table-column prop="equipmentCode" label="设备编码" width="110" />
        <el-table-column prop="equipmentName" label="设备名称" min-width="150" />
        <el-table-column prop="setupHours" label="调试(h)" width="90" align="right" />
        <el-table-column prop="runningHours" label="开机(h)" width="90" align="right" />
        <el-table-column prop="idleHours" label="待机(h)" width="90" align="right" />
        <el-table-column prop="totalHours" label="总时长(h)" width="100" align="right" />
        <el-table-column prop="availability" label="开机率" width="90" align="right">
          <template #default="{ row }">{{ fmtPercent(row.availability) }}</template>
        </el-table-column>
        <el-table-column prop="passRate" label="良品率" width="90" align="right">
          <template #default="{ row }">{{ fmtPercent(row.passRate) }}</template>
        </el-table-column>
        <el-table-column prop="oee" label="OEE" width="90" align="right">
          <template #default="{ row }">{{ fmtPercent(row.oee) }}</template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 操作工产量排名 -->
    <el-card shadow="hover" class="page-card mt">
      <template #header>
        <div class="card-header">
          <span>操作工产量排名（展开看机台 · 工序明细）</span>
          <el-date-picker v-model="date" type="date" placeholder="选择日期" :clearable="false"
            value-format="YYYY-MM-DD" size="small" @change="loadProcessStats" />
        </div>
      </template>
      <el-table :data="operatorRankings" stripe>
        <el-table-column type="expand">
          <template #default="{ row }">
            <div class="op-expand">
              <div v-for="m in (row.machines || [])" :key="`${row.employeeId}-${m.equipmentId}`" class="machine-block">
                <div class="machine-head">
                  <span class="machine-name">{{ m.equipmentName || m.equipmentCode || '未指定机台' }}</span>
                  <span class="machine-qty">合计 {{ fmtNum(m.totalQty) }} 件 · 合格 {{ fmtNum(m.qualifiedQty) }} · 不良 {{ fmtNum(m.defectQty) }}</span>
                </div>
                <div v-if="(m.steps || []).length" class="step-list">
                  <div v-for="(s, si) in (m.steps || [])" :key="`${row.employeeId}-${m.equipmentId}-${s.processCardId}-${s.stepNo}-${si}`" class="step-row">
                    <span class="step-name">工序{{ s.stepNo }} {{ s.stepName || '-' }}</span>
                    <span v-if="s.cardCode" class="step-card">{{ s.cardCode }}</span>
                    <span class="step-qty">完成 {{ fmtNum(s.totalQty) }} · 合格 {{ fmtNum(s.qualifiedQty) }} · 不良 {{ fmtNum(s.defectQty) }}</span>
                  </div>
                </div>
                <div v-else class="step-empty">该机台暂无工序明细</div>
              </div>
              <div v-if="!(row.machines || []).length" class="step-empty">该操作工暂无机台数据</div>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="排名" width="70" align="center">
          <template #default="{ $index }">{{ $index + 1 }}</template>
        </el-table-column>
        <el-table-column label="操作工" min-width="150">
          <template #default="{ row }">{{ row.employeeName || `员工${row.employeeId}` }}（{{ row.employeeNo || '-' }}）</template>
        </el-table-column>
        <el-table-column prop="machineCount" label="机台数" width="90" align="center" />
        <el-table-column label="总产量" width="110" align="right">
          <template #default="{ row }">{{ fmtNum(row.totalQty) }}</template>
        </el-table-column>
        <el-table-column label="合格" width="100" align="right">
          <template #default="{ row }">{{ fmtNum(row.qualifiedQty) }}</template>
        </el-table-column>
        <el-table-column label="不良" width="100" align="right">
          <template #default="{ row }">{{ fmtNum(row.defectQty) }}</template>
        </el-table-column>
        <el-table-column label="完成率" min-width="150">
          <template #default="{ row }">
            <el-progress :percentage="opRate(row)" :stroke-width="9" status="success"
              :format="() => `${opRate(row)}%`" />
          </template>
        </el-table-column>
      </el-table>
      <el-empty v-if="!operatorRankings.length" description="当日暂无操作工报工数据" :image-size="70" />
    </el-card>

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

    <el-card shadow="hover" class="page-card mt">
      <template #header>
        <div class="card-header">
          <span>质量趋势</span>
          <el-date-picker v-model="date" type="date" placeholder="选择日期" :clearable="false"
            value-format="YYYY-MM-DD" size="small" @change="loadTrend" />
        </div>
      </template>
      <BaseChart :option="qualityOption" :height="320" />
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
        <el-table-column label="工序" width="150">
          <template #default="{ row }">
            <template v-if="row.processStepNo && row.processStepName">
              <el-tag size="small" type="primary">工序{{ row.processStepNo }}</el-tag>
              <span class="step-name">{{ row.processStepName }}</span>
            </template>
            <span v-else class="muted">-</span>
          </template>
        </el-table-column>
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
import { formatDateTime, fmtPercent } from '../utils/format'

const date = ref(new Date().toISOString().slice(0, 10))
const dailyStats = ref([])
const recentReports = ref([])
const reminders = ref([])
const productMap = ref({})
const equipmentSummary = ref([])
const qualityTrend = ref([])
const operatorRankings = ref([])

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

const qualityOption = computed(() => {
  const labels = qualityTrend.value.map((x) => x.date.slice(5))
  return {
    tooltip: { trigger: 'axis' },
    legend: { data: ['抽检数', '合格数', '合格率'] },
    grid: { left: 45, right: 45, top: 40, bottom: 30 },
    xAxis: { type: 'category', data: labels },
    yAxis: [
      { type: 'value', name: '数量' },
      { type: 'value', name: '合格率', max: 100, axisLabel: { formatter: '{value}%' } }
    ],
    series: [
      { name: '抽检数', type: 'bar', data: qualityTrend.value.map((x) => x.sampleQty), itemStyle: { color: '#409eff' } },
      { name: '合格数', type: 'bar', data: qualityTrend.value.map((x) => x.qualifiedQty), itemStyle: { color: '#67c23a' } },
      { name: '合格率', type: 'line', yAxisIndex: 1, data: qualityTrend.value.map((x) => Number(x.passRate || 0) * 100), itemStyle: { color: '#e6a23c' }, smooth: true }
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

async function loadEquipmentTime() {
  const rows = await adminApi.equipmentTimeSummary({ startDate: date.value, endDate: date.value })
  equipmentSummary.value = (rows || []).map((r) => ({
    ...r,
    totalHours: Number(r.totalHours ?? (Number(r.setupHours || 0) + Number(r.runningHours || 0) + Number(r.idleHours || 0))),
    availability: Number(r.availability ?? (Number(r.totalHours || 0) > 0 ? Number(r.runningHours || 0) / Number(r.totalHours || 0) : 0)),
    passRate: Number(r.passRate ?? 1),
    oee: Number(r.oee ?? ((Number(r.totalHours || 0) > 0 ? Number(r.runningHours || 0) / Number(r.totalHours || 0) : 0) * Number(r.passRate ?? 1)))
  }))
}

async function loadTrend() {
  const rows = await adminApi.qualityTrend({ days: 7 })
  qualityTrend.value = rows || []
}

function fmtNum(v) {
  return Number(v || 0).toLocaleString('zh-CN', { maximumFractionDigits: 2 })
}

function opRate(row) {
  const total = Number(row.totalQty || 0)
  if (!total) return 0
  return Math.min(100, Math.round((Number(row.qualifiedQty || 0) / total) * 100))
}

async function loadProcessStats() {
  const data = await adminApi.productionProcess({ date: date.value })
  operatorRankings.value = (data && data.operatorRankings) || []
}

async function load() {
  await Promise.all([loadDaily(), loadEquipmentTime(), loadTrend(), loadProcessStats()])
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

.op-expand {
  padding: 6px 16px 6px 48px;
  background: #f8fafc;
  border-radius: 6px;
}

.machine-block {
  border-bottom: 1px dashed #e2e8f0;
  padding: 10px 0;
}

.machine-block:last-child {
  border-bottom: none;
}

.machine-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
}

.machine-name {
  font-weight: 700;
  color: #1e3a5f;
}

.machine-qty {
  color: #64748b;
  font-size: 13px;
}

.step-list {
  margin-top: 8px;
  background: #fff;
  border-radius: 6px;
  padding: 2px 12px;
}

.step-row {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
  padding: 8px 0;
  border-bottom: 1px solid #f1f5f9;
  font-size: 13px;
}

.step-row:last-child {
  border-bottom: none;
}

.step-name {
  color: #334155;
  min-width: 120px;
}

.step-card {
  background: #eef4fb;
  color: #3f78b6;
  font-size: 12px;
  padding: 1px 8px;
  border-radius: 4px;
}

.step-qty {
  color: #64748b;
  margin-left: auto;
}

.step-empty {
  color: #94a3b8;
  font-size: 13px;
  padding: 4px 0;
}

.step-name {
  margin-left: 6px;
  color: #303133;
}

.muted {
  color: #c0c4cc;
}
</style>
