<template>
  <view class="page">
    <view class="tabs">
      <view class="tab" :class="{ active: tab === 'daily' }" @click="switchTab('daily')">日</view>
      <view class="tab" :class="{ active: tab === 'weekly' }" @click="switchTab('weekly')">周</view>
      <view class="tab" :class="{ active: tab === 'monthly' }" @click="switchTab('monthly')">月</view>
    </view>

    <view class="field" style="margin-bottom: 16rpx;">
      <text class="field-label">设备筛选</text>
      <picker mode="selector" :range="equipmentOptions" range-key="name" @change="onEquipmentChange">
        <view class="field-picker">
          <text :class="{ ph: !equipmentId }">{{ selectedEquipmentText || '全部设备' }}</text>
          <text class="arrow">▾</text>
        </view>
      </picker>
    </view>

    <!-- 顶部看板：完成率 + 产量 -->
    <view class="hero-board">
      <view class="hero-grid">
        <view><text class="hero-num">{{ completionRate }}%</text><text class="hero-sub">整体完成率</text></view>
        <view><text class="hero-num">{{ fmtQty(summary.totalQty) }}</text><text class="hero-sub">总产量/件</text></view>
        <view><text class="hero-num">{{ processStats.operatorRankings.length }}</text><text class="hero-sub">活跃操作工</text></view>
      </view>
    </view>

    <!-- 操作工直接排名 -->
    <view class="card">
      <view class="card-title">
        操作工排名
        <text class="card-sub">按总产量</text>
      </view>
      <view
        v-for="(it, i) in processStats.operatorRankings"
        :key="it.employeeId"
        class="op-row"
        @click="toggleOp(it.employeeId)"
      >
        <view class="rank" :class="{ top: i < 3 }">{{ i + 1 }}</view>
        <view class="op-main">
          <view class="op-name">{{ it.employeeName || '操作工' + it.employeeId }}</view>
          <view class="op-sub">{{ it.employeeNo || '-' }} · {{ it.machineCount }} 台机床 · 合格 {{ fmtQty(it.qualifiedQty) }} · 不良 {{ fmtQty(it.defectQty) }}</view>
          <view class="rate-line">
            <view class="rate-bg"><view class="rate-fill" :style="{ width: operatorRate(it) + '%' }"></view></view>
            <text class="rate-txt">{{ operatorRate(it) }}%</text>
          </view>
        </view>
        <view class="op-qty">{{ fmtQty(it.totalQty) }}<text class="qty-unit"> 件</text></view>
        <view class="op-arrow">{{ expandedOp === it.employeeId ? '▴' : '▾' }}</view>
      </view>
      <view v-if="!processStats.operatorRankings.length" class="empty">暂无操作工报工数据</view>

      <!-- 展开：操作工 → 机台 → 工序 -->
      <view v-if="expandedOp" class="op-detail">
        <view
          v-for="m in expandedOperator.machines"
          :key="expandKey(expandedOperator.employeeId, m.equipmentId)"
          class="machine-block"
        >
          <view class="machine-head">
            <view class="machine-name">{{ m.equipmentName || m.equipmentCode || '未指定机台' }}</view>
            <view class="machine-qty">合计 {{ fmtQty(m.totalQty) }} 件 · 合格 {{ fmtQty(m.qualifiedQty) }} · 不良 {{ fmtQty(m.defectQty) }}</view>
          </view>
          <view class="sub-step-list">
            <view v-for="(s, si) in (m.steps || [])" :key="`${expandKey(expandedOperator.employeeId, m.equipmentId)}-${s.processCardId}-${s.stepNo}-${si}`" class="sub-step">
              <text class="sub-step-name">工序{{ s.stepNo }} {{ s.stepName || '-' }}</text>
              <text class="sub-step-card" v-if="s.cardCode">{{ s.cardCode }}</text>
              <view class="sub-step-qty">
                {{ fmtQty(s.totalQty) }}件 · 合格 {{ fmtQty(s.qualifiedQty) }} · 不良 {{ fmtQty(s.defectQty) }}
              </view>
            </view>
            <view v-if="!(m.steps || []).length" class="sub-step empty">该机台暂无工序明细</view>
          </view>
        </view>
      </view>
    </view>

    <!-- 产品工序完成量 -->
    <view class="card">
      <view class="card-title">产品工序完成量</view>
      <view class="list-item" v-for="(it, i) in processStats.processSteps" :key="`${it.processCardId}-${it.stepNo}-${i}`">
        <view class="list-av amber">{{ it.stepNo || '工' }}</view>
        <view class="list-bd">
          <view class="list-n1">{{ it.productName || '产品' + it.productId }} · 工序{{ it.stepNo }} {{ it.stepName || '-' }}</view>
          <view class="list-n2">流转卡 {{ it.cardCode || '-' }} · 报工 {{ it.reportCount }} 次</view>
        </view>
        <view class="list-rt">
          <view class="list-r1">{{ fmtQty(it.totalQty) }} 件</view>
          <view class="list-r2">合格 {{ fmtQty(it.qualifiedQty) }} · 不良 {{ fmtQty(it.defectQty) }}</view>
        </view>
      </view>
      <view v-if="!processStats.processSteps.length" class="empty">暂无关联工序的报工数据</view>
    </view>
  </view>
</template>

<script setup>
import { ref, computed } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { requireAuth, fmtQty, currentISOWeek } from '../../utils/format'
import { apiProductionDaily, apiProductionWeekly, apiProductionMonthly, apiEquipments, apiProductionProcess } from '../../api'

const tab = ref('daily')
const list = ref([])
const equipmentOptions = ref([])
const equipmentId = ref(null)
const expandedOp = ref(null)
const processStats = ref({ processSteps: [], operatorRankings: [] })
const selectedEquipmentText = computed(() => equipmentOptions.value.find((e) => e.id === equipmentId.value)?.name || '')

const expandedOperator = computed(() =>
  (processStats.value.operatorRankings || []).find((o) => o.employeeId === expandedOp.value) || null
)

onShow(() => {
  if (!requireAuth()) return
  loadOptions()
  loadData()
})

const summary = computed(() => {
  const steps = processStats.value.processSteps || []
  const total = steps.reduce((s, i) => s + Number(i.totalQty || 0), 0)
  const qualified = steps.reduce((s, i) => s + Number(i.qualifiedQty || 0), 0)
  const defect = steps.reduce((s, i) => s + Number(i.defectQty || 0), 0)
  return { totalQty: total, qualifiedQty: qualified, defectQty: defect }
})

const completionRate = computed(() => summary.value.totalQty ? ((summary.value.qualifiedQty / summary.value.totalQty) * 100).toFixed(1) : '0.0')

function operatorRate(row) {
  return row.totalQty ? Math.min(100, Math.round((Number(row.qualifiedQty || 0) / Number(row.totalQty)) * 100)) : 0
}

function toggleOp(id) {
  expandedOp.value = expandedOp.value === id ? null : id
}

function expandKey(opId, eqId) {
  return `${opId}-${eqId ?? 'null'}`
}

function switchTab(t) {
  tab.value = t
  loadData()
}

function loadOptions() {
  apiEquipments().then((r) => {
    equipmentOptions.value = [{ id: null, name: '全部设备' }, ...(r || [])]
  }).catch(() => {})
}

function onEquipmentChange(e) {
  const idx = Number(e.detail.value)
  equipmentId.value = equipmentOptions.value[idx]?.id ?? null
  expandedOp.value = null
  loadData()
}

function loadData() {
  const params = {}
  if (tab.value === 'weekly') {
    const { year, week } = currentISOWeek()
    const range = isoWeekRange(year, week)
    params.from = range.from
    params.to = range.to
  } else if (tab.value === 'monthly') {
    const d = new Date()
    const first = new Date(d.getFullYear(), d.getMonth(), 1)
    const last = new Date(d.getFullYear(), d.getMonth() + 1, 0)
    params.from = fmtDate(first)
    params.to = fmtDate(last)
  } else {
    params.date = new Date().toISOString().slice(0, 10)
  }
  const processParams = { ...params }
  expandedOp.value = null
  apiProductionProcess(processParams).then((r) => {
    const safe = r || {}
    processStats.value = {
      processSteps: Array.isArray(safe.processSteps) ? safe.processSteps : [],
      operatorRankings: Array.isArray(safe.operatorRankings) ? safe.operatorRankings : [],
    }
  }).catch(() => {
    processStats.value = { processSteps: [], operatorRankings: [] }
  })
}

function fmtDate(d) {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

function isoWeekRange(year, week) {
  const jan4 = new Date(year, 0, 4)
  const day = jan4.getDay() || 7
  const monday = new Date(jan4)
  monday.setDate(jan4.getDate() - day + 1 + (week - 1) * 7)
  const sunday = new Date(monday)
  sunday.setDate(monday.getDate() + 6)
  return { from: fmtDate(monday), to: fmtDate(sunday) }
}
</script>

<style scoped>
.page {
  padding: 24rpx;
}

.hero-board {
  margin-bottom: 24rpx;
  padding: 26rpx;
  border-radius: 24rpx;
  color: #fff;
  background: linear-gradient(135deg, #112d49, #24688a);
  box-shadow: 0 12rpx 28rpx rgba(16, 52, 79, .16);
}

.hero-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16rpx; text-align: center; }
.hero-num { display: block; font-size: 38rpx; font-weight: 700; }
.hero-sub { display: block; margin-top: 6rpx; font-size: 21rpx; opacity: .72; }

.card-sub { font-size: 20rpx; color: #94a3b8; font-weight: 400; }

.op-row { display: flex; align-items: center; gap: 14rpx; padding: 16rpx 0; border-bottom: 2rpx solid #f1f5f9; }
.rank { width: 42rpx; height: 42rpx; line-height: 42rpx; text-align: center; border-radius: 50%; background: #f1f5f9; color: #64748b; font-weight: 700; font-size: 24rpx; flex-shrink: 0; }
.rank.top { background: #fff7ed; color: #d97706; }
.op-main { min-width: 0; flex: 1; }
.op-name { color: #1e293b; font-size: 28rpx; font-weight: 650; }
.op-sub { color: #94a3b8; font-size: 22rpx; margin-top: 4rpx; }
.rate-line { display: flex; align-items: center; gap: 12rpx; margin-top: 8rpx; }
.rate-bg { height: 10rpx; flex: 1; overflow: hidden; border-radius: 10rpx; background: #e2e8f0; }
.rate-fill { height: 100%; border-radius: 10rpx; background: linear-gradient(90deg, #10b981, #34d399); }
.rate-txt { color: #059669; font-size: 22rpx; font-weight: 700; }
.op-qty { color: #1e4468; font-size: 30rpx; font-weight: 700; }
.qty-unit { font-size: 20rpx; font-weight: 400; }
.op-arrow { color: #94a3b8; font-size: 22rpx; }

.op-detail { background: #f8fafc; border-radius: 16rpx; padding: 8rpx 16rpx; margin-top: 6rpx; }
.machine-block { border-bottom: 2rpx dashed #eaf0f6; padding: 18rpx 0; }
.machine-block:last-child { border-bottom: none; }
.machine-head { display: flex; justify-content: space-between; align-items: center; }
.machine-name { color: #16334f; font-size: 26rpx; font-weight: 700; }
.machine-qty { color: #64748b; font-size: 22rpx; }
.sub-step-list { margin-top: 10rpx; background: #fff; border-radius: 12rpx; padding: 4rpx 14rpx; }
.sub-step { display: flex; align-items: center; flex-wrap: wrap; gap: 10rpx; padding: 12rpx 0; border-bottom: 2rpx solid #f5f7fa; }
.sub-step:last-child { border-bottom: none; }
.sub-step-name { color: #334155; font-size: 24rpx; }
.sub-step-card { background: #eef4fb; color: #3f78b6; font-size: 20rpx; padding: 2rpx 10rpx; border-radius: 6rpx; }
.sub-step-qty { color: #64748b; font-size: 22rpx; margin-left: auto; }
</style>