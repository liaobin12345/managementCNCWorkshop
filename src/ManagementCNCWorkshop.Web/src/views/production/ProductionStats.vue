<template>
  <div class="dashboard-page">
    <div class="board-header">
      <div>
        <div class="eyebrow">PRODUCTION COMMAND CENTER</div>
        <h1>生产看板</h1>
        <p>实时掌握工序推进、操作工完成率与机台产量</p>
      </div>
      <div class="board-actions">
        <el-select v-model="workshopId" placeholder="全部车间" clearable @change="load">
          <el-option v-for="w in workshops" :key="w.id" :label="w.name" :value="w.id" />
        </el-select>
        <el-date-picker v-model="date" type="date" value-format="YYYY-MM-DD" @change="load" />
        <el-button type="primary" :loading="loading" @click="load">刷新看板</el-button>
      </div>
    </div>

    <div class="kpi-grid">
      <div class="kpi-card kpi-blue"><div class="kpi-label">今日总产量</div><div class="kpi-value">{{ fmt(totalQty) }}</div><div class="kpi-foot">件 · 全车间</div></div>
      <div class="kpi-card kpi-green"><div class="kpi-label">整体完成率</div><div class="kpi-value">{{ completionRate }}%</div><div class="kpi-foot">合格 {{ fmt(qualifiedQty) }} 件</div></div>
      <div class="kpi-card kpi-orange"><div class="kpi-label">活跃操作工</div><div class="kpi-value">{{ operatorRankings.length }}</div><div class="kpi-foot">人 · {{ machineCount }} 台机床</div></div>
      <div class="kpi-card kpi-purple"><div class="kpi-label">工序报工</div><div class="kpi-value">{{ processSteps.length }}</div><div class="kpi-foot">个产品工序有产出</div></div>
    </div>

    <div class="board-grid">
      <el-card class="ranking-panel" shadow="never">
        <template #header><div class="panel-title"><span>操作工完成率排名</span><el-tag type="success" size="small">TOP 10</el-tag></div></template>
        <div v-for="(row, index) in topOperators" :key="row.employeeId" class="ranking-row">
          <div class="rank-badge" :class="{ champion: index === 0 }">{{ index + 1 }}</div>
          <div class="operator-info"><div class="operator-name">{{ row.employeeName || `员工${row.employeeId}` }}</div><div class="operator-sub">{{ row.employeeNo || '-' }} · {{ row.machineCount }} 台机床 · {{ fmt(row.totalQty) }} 件</div></div>
          <div class="rate-box"><strong>{{ operatorRate(row) }}%</strong><el-progress :percentage="operatorRate(row)" :show-text="false" :stroke-width="7" status="success" /></div>
        </div>
        <el-empty v-if="!topOperators.length" description="暂无报工数据" :image-size="70" />
      </el-card>

      <el-card class="ranking-panel" shadow="never">
        <template #header><div class="panel-title"><span>操作工产量排名</span><el-tag type="warning" size="small">多机台汇总</el-tag></div></template>
        <div v-for="(row, index) in topOperators" :key="`qty-${row.employeeId}`" class="quantity-row">
          <div class="rank-badge" :class="{ champion: index === 0 }">{{ index + 1 }}</div>
          <div class="operator-info"><div class="operator-name">{{ row.employeeName || `员工${row.employeeId}` }}</div><div class="machine-list"><span v-for="m in row.machines" :key="`${row.employeeId}-${m.equipmentId}`">{{ m.equipmentName || m.equipmentCode || '未指定机台' }} {{ fmt(m.totalQty) }}件</span></div></div>
          <strong class="qty-value">{{ fmt(row.totalQty) }}<small> 件</small></strong>
        </div>
        <el-empty v-if="!topOperators.length" description="暂无报工数据" :image-size="70" />
      </el-card>
    </div>

    <div class="board-grid lower-grid">
      <el-card shadow="never">
        <template #header><div class="panel-title"><span>产品工序完成情况</span><el-tag type="primary" size="small">按流转卡</el-tag></div></template>
        <el-table :data="processSteps" size="small" stripe max-height="360">
          <el-table-column prop="productName" label="产品" min-width="110" />
          <el-table-column label="工序" min-width="135"><template #default="{ row }">工序{{ row.stepNo }} · {{ row.stepName }}</template></el-table-column>
          <el-table-column prop="cardCode" label="流转卡" min-width="145" />
          <el-table-column prop="totalQty" label="完成数" width="90" align="right" />
          <el-table-column prop="qualifiedQty" label="合格" width="80" align="right" />
          <el-table-column label="完成率" width="100"><template #default="{ row }"><el-progress :percentage="stepRate(row)" :stroke-width="8" :format="() => `${stepRate(row)}%`" /></template></el-table-column>
        </el-table>
        <el-empty v-if="!processSteps.length" description="暂无工序报工数据" :image-size="70" />
      </el-card>

      <el-card shadow="never">
        <template #header><div class="panel-title"><span>机台产量排行</span><el-tag type="info" size="small">今日</el-tag></div></template>
        <div v-for="(row, index) in topMachines" :key="row.equipmentId || index" class="machine-row"><span class="machine-rank">{{ index + 1 }}</span><span class="machine-name">{{ row.equipmentName || row.equipmentCode || '未指定机台' }}</span><el-progress :percentage="machinePercent(row)" :show-text="false" :stroke-width="9" /><strong>{{ fmt(row.totalQty) }}</strong></div>
        <el-empty v-if="!topMachines.length" description="暂无机台数据" :image-size="70" />
      </el-card>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { adminApi } from '../../api'

const loading = ref(false)
const date = ref(new Date().toISOString().slice(0, 10))
const workshopId = ref(null)
const workshops = ref([])
const processSteps = ref([])
const operatorRankings = ref([])
const machineRows = ref([])

const topOperators = computed(() => operatorRankings.value.slice(0, 10))
const topMachines = computed(() => machineRows.value.slice(0, 8))
const totalQty = computed(() => processSteps.value.reduce((sum, row) => sum + Number(row.totalQty || 0), 0))
const qualifiedQty = computed(() => processSteps.value.reduce((sum, row) => sum + Number(row.qualifiedQty || 0), 0))
const machineCount = computed(() => new Set(operatorRankings.value.flatMap((row) => row.machines || []).map((machine) => machine.equipmentId).filter(Boolean)).size)
const completionRate = computed(() => totalQty.value ? ((qualifiedQty.value / totalQty.value) * 100).toFixed(1) : '0.0')

function fmt(value) {
  return Number(value || 0).toLocaleString('zh-CN', { maximumFractionDigits: 2 })
}

function operatorRate(row) {
  return row.totalQty ? Math.min(100, Math.round((Number(row.qualifiedQty || 0) / Number(row.totalQty)) * 100)) : 0
}

function stepRate(row) {
  const target = Number(row.targetQty || 0)
  return target ? Math.min(100, Math.round((Number(row.totalQty || 0) / target) * 100)) : (row.totalQty ? 100 : 0)
}

function machinePercent(row) {
  const max = Math.max(...machineRows.value.map((item) => Number(item.totalQty || 0)), 1)
  return Math.round((Number(row.totalQty || 0) / max) * 100)
}

async function loadBase() {
  workshops.value = await adminApi.workshops()
}

async function load() {
  loading.value = true
  try {
    const params = { date: date.value, workshopId: workshopId.value || undefined }
    const data = await adminApi.productionProcess(params)
    processSteps.value = data?.processSteps || []
    operatorRankings.value = data?.operatorRankings || []
    machineRows.value = operatorRankings.value.flatMap((operator) => operator.machines || []).reduce((all, row) => {
      const existing = all.find((item) => item.equipmentId === row.equipmentId)
      if (existing) {
        existing.totalQty += Number(row.totalQty || 0)
        existing.qualifiedQty += Number(row.qualifiedQty || 0)
      } else {
        all.push({ ...row, totalQty: Number(row.totalQty || 0), qualifiedQty: Number(row.qualifiedQty || 0) })
      }
      return all
    }, []).sort((a, b) => b.totalQty - a.totalQty)
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await loadBase()
  await load()
})
</script>

<style scoped>
.dashboard-page { min-height: 100%; padding: 4px 0 28px; background: #f5f7fb; }
.board-header { display: flex; justify-content: space-between; align-items: flex-end; margin-bottom: 22px; }
.eyebrow { color: #4975a6; font-size: 11px; font-weight: 700; letter-spacing: 1.8px; }
h1 { margin: 5px 0 3px; color: #142b46; font-size: 28px; }
.board-header p { margin: 0; color: #8090a4; font-size: 13px; }
.board-actions { display: flex; gap: 10px; align-items: center; }
.board-actions .el-select { width: 145px; }
.kpi-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; margin-bottom: 18px; }
.kpi-card { position: relative; overflow: hidden; min-height: 108px; padding: 18px 20px; border-radius: 10px; color: #fff; box-shadow: 0 5px 18px rgba(25, 55, 90, .08); }
.kpi-card::after { position: absolute; right: -18px; bottom: -35px; width: 110px; height: 110px; border: 18px solid rgba(255, 255, 255, .12); border-radius: 50%; content: ''; }
.kpi-blue { background: linear-gradient(135deg, #2469a5, #438fc1); }
.kpi-green { background: linear-gradient(135deg, #218777, #42b69b); }
.kpi-orange { background: linear-gradient(135deg, #c97938, #e5a04f); }
.kpi-purple { background: linear-gradient(135deg, #6855a3, #8975c3); }
.kpi-label { font-size: 13px; opacity: .84; }
.kpi-value { margin-top: 12px; font-size: 30px; font-weight: 750; letter-spacing: .5px; }
.kpi-foot { margin-top: 3px; font-size: 12px; opacity: .75; }
.board-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 18px; margin-bottom: 18px; }
.lower-grid { grid-template-columns: 1.2fr .8fr; }
.ranking-panel :deep(.el-card__header), .board-grid :deep(.el-card__header) { padding: 15px 18px; }
.ranking-panel :deep(.el-card__body), .board-grid :deep(.el-card__body) { padding: 10px 18px 14px; }
.panel-title { display: flex; justify-content: space-between; align-items: center; color: #213b59; font-size: 15px; font-weight: 700; }
.ranking-row, .quantity-row { display: flex; gap: 12px; align-items: center; padding: 13px 0; border-bottom: 1px solid #f0f3f7; }
.ranking-row:last-child, .quantity-row:last-child { border-bottom: 0; }
.rank-badge { display: grid; place-items: center; width: 27px; height: 27px; flex: 0 0 auto; border-radius: 50%; background: #eef2f7; color: #708198; font-size: 12px; font-weight: 700; }
.rank-badge.champion { background: #f4b052; color: #fff; box-shadow: 0 2px 7px rgba(214, 141, 47, .35); }
.operator-info { min-width: 0; flex: 1; }
.operator-name { color: #29425e; font-size: 14px; font-weight: 650; }
.operator-sub { overflow: hidden; margin-top: 4px; color: #94a2b3; font-size: 11px; text-overflow: ellipsis; white-space: nowrap; }
.rate-box { width: 125px; }
.rate-box strong { display: block; margin-bottom: 5px; color: #299274; text-align: right; font-size: 13px; }
.machine-list { display: flex; gap: 5px; flex-wrap: wrap; margin-top: 5px; }
.machine-list span { padding: 2px 6px; border-radius: 3px; background: #f1f6fa; color: #6f8298; font-size: 10px; }
.qty-value { color: #df8b3f; font-size: 17px; }
.qty-value small { font-size: 11px; font-weight: 400; }
.machine-row { display: grid; grid-template-columns: 25px 100px 1fr 52px; gap: 10px; align-items: center; padding: 12px 0; border-bottom: 1px solid #f0f3f7; }
.machine-rank { color: #8c9aad; font-size: 12px; font-weight: 700; text-align: center; }
.machine-name { overflow: hidden; color: #3b516b; font-size: 13px; text-overflow: ellipsis; white-space: nowrap; }
.machine-row strong { color: #3975a4; font-size: 13px; text-align: right; }
@media (max-width: 900px) { .kpi-grid { grid-template-columns: repeat(2, 1fr); } .board-header { align-items: flex-start; flex-direction: column; gap: 15px; } .board-grid, .lower-grid { grid-template-columns: 1fr; } }
</style>
