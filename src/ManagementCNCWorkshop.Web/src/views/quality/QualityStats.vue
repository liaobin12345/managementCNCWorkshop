<template>
  <div>
    <el-card shadow="hover">
      <div class="toolbar">
        <div class="toolbar-left">
          <el-radio-group v-model="mode" @change="load">
            <el-radio-button value="daily">按日</el-radio-button>
            <el-radio-button value="weekly">按周</el-radio-button>
            <el-radio-button value="monthly">按月</el-radio-button>
          </el-radio-group>

          <el-date-picker v-if="mode === 'daily'" v-model="date" type="date" placeholder="选择日期"
            value-format="YYYY-MM-DD" @change="load" />

          <template v-if="mode === 'weekly'">
            <el-input-number v-model="week.year" :min="2020" :max="2035" controls-position="right" />
            <span class="sep">年</span>
            <el-input-number v-model="week.week" :min="1" :max="53" controls-position="right" />
            <span class="sep">周</span>
          </template>

          <template v-if="mode === 'monthly'">
            <el-input-number v-model="month.year" :min="2020" :max="2035" controls-position="right" />
            <span class="sep">年</span>
            <el-input-number v-model="month.month" :min="1" :max="12" controls-position="right" />
            <span class="sep">月</span>
          </template>

          <el-select v-model="workshopId" placeholder="全部车间" clearable style="width: 150px" @change="load">
            <el-option v-for="w in workshops" :key="w.id" :label="w.name" :value="w.id" />
          </el-select>

          <el-button @click="load">查询</el-button>
        </div>
      </div>
    </el-card>

    <el-card shadow="hover" class="page-card mt">
      <template #header>合格率与抽检量</template>
      <BaseChart :option="chartOption" :height="340" />
    </el-card>

    <el-card shadow="hover">
      <template #header>统计明细（按产品）</template>
      <el-table :data="rows" stripe v-loading="loading">
        <el-table-column label="产品" min-width="180">
          <template #default="{ row }">{{ productMap[row.productId] || `产品 #${row.productId}` }}</template>
        </el-table-column>
        <el-table-column prop="sampleQty" label="抽检数" align="right" width="110" />
        <el-table-column prop="qualifiedQty" label="合格" align="right" width="110" />
        <el-table-column prop="defectQty" label="不良" align="right" width="110" />
        <el-table-column label="合格率" align="right" width="110">
          <template #default="{ row }">{{ passRate(row) }}%</template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<script setup>
import { computed, onMounted, reactive, ref } from 'vue'
import BaseChart from '../../components/BaseChart.vue'
import { adminApi } from '../../api'
import { isoWeek } from '../../utils/format'

const mode = ref('daily')
const date = ref(new Date().toISOString().slice(0, 10))
const week = reactive(isoWeek(new Date()))
const month = reactive({ year: new Date().getFullYear(), month: new Date().getMonth() + 1 })
const workshopId = ref(null)
const loading = ref(false)
const rows = ref([])
const productMap = ref({})
const workshops = ref([])

function passRate(row) {
  return (Number(row.passRate || 0) * 100).toFixed(2)
}

const chartOption = computed(() => {
  const names = rows.value.map((r) => productMap.value[r.productId] || `产品 #${r.productId}`)
  return {
    tooltip: { trigger: 'axis' },
    legend: { data: ['抽检数', '合格数', '合格率'] },
    grid: { left: 50, right: 50, top: 40, bottom: 30 },
    xAxis: { type: 'category', data: names },
    yAxis: [
      { type: 'value', name: '数量' },
      { type: 'value', name: '合格率', max: 100, axisLabel: { formatter: '{value}%' } }
    ],
    series: [
      {
        name: '抽检数', type: 'bar', data: rows.value.map((r) => r.sampleQty),
        itemStyle: { color: '#409eff' }
      },
      {
        name: '合格数', type: 'bar', data: rows.value.map((r) => r.qualifiedQty),
        itemStyle: { color: '#67c23a' }
      },
      {
        name: '合格率', type: 'line', yAxisIndex: 1,
        data: rows.value.map((r) => Number(r.passRate || 0) * 100),
        itemStyle: { color: '#e6a23c' }, smooth: true
      }
    ]
  }
})

async function load() {
  loading.value = true
  try {
    const params = { workshopId: workshopId.value || undefined }
    if (mode.value === 'daily') {
      rows.value = await adminApi.qualityStats('daily', { ...params, date: date.value })
    } else if (mode.value === 'weekly') {
      rows.value = await adminApi.qualityStats('weekly', { ...params, year: week.year, week: week.week })
    } else {
      rows.value = await adminApi.qualityStats('monthly', { ...params, year: month.year, month: month.month })
    }
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  workshops.value = await adminApi.workshops()
  productMap.value = Object.fromEntries((await adminApi.products()).map((p) => [p.id, `${p.name}（${p.code}）`]))
  await load()
})
</script>

<style scoped>
.sep {
  margin: 0 4px;
  color: #909399;
}
</style>
