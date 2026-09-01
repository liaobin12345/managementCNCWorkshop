<template>
  <view class="page">
    <!-- 筛选 -->
    <view class="card">
      <view class="form-row">
        <text class="label">设备</text>
        <picker
          :range="equipments"
          range-key="name"
          :value="equipmentIndex"
          @change="onEquipmentChange"
        >
          <view class="picker-box" :class="{ placeholder: !currentEquipment }">
            {{ currentEquipment ? `${currentEquipment.name}（${currentEquipment.code}）` : '全部设备' }}
          </view>
        </picker>
      </view>
      <view class="form-row">
        <text class="label">月份</text>
        <picker mode="date" fields="month" :value="yearMonth" @change="onMonthChange">
          <view class="picker-box">{{ yearMonth }}</view>
        </picker>
      </view>
      <view class="form-row">
        <text class="label">班次</text>
        <view class="shift-group">
          <view class="shift-btn" :class="{ active: shift === 'Day' }" @click="switchShift('Day')">白班</view>
          <view class="shift-btn" :class="{ active: shift === 'Night' }" @click="switchShift('Night')">夜班</view>
        </view>
      </view>
    </view>

    <!-- 汇总 -->
    <view class="card">
      <view class="stats-row">
        <view class="stat"><view class="v">{{ report.checkedDays }}</view><view class="l">点检天数</view></view>
        <view class="stat"><view class="v red">{{ abnormalCount }}</view><view class="l">异常次数</view></view>
        <view class="stat"><view class="v amber">{{ restDays }}</view><view class="l">休息天数</view></view>
      </view>
    </view>

    <!-- 点检表网格 -->
    <view class="card">
      <view class="card-title">
        {{ yearMonth }} 点检表
        <text class="card-sub">✓ 正常 ✗ 异常 · 停 停机 · 休 休息 · 空 未点检</text>
      </view>
      <scroll-view scroll-x class="grid-scroll" :scroll-left="scrollLeft" scroll-with-animation>
        <view class="grid" :style="{ width: gridWidth }">
          <view class="grid-row head">
            <view class="cell-name">点检项</view>
            <view
              class="cell-day head-day"
              :class="{ 'is-today': d === todayDay }"
              v-for="d in report.daysInMonth"
              :key="d"
            >
              <text class="day-num">{{ d }}</text>
              <text v-if="d === todayDay" class="day-mark">今</text>
            </view>
            <view class="cell-spacer"></view>
          </view>
          <view class="grid-row" v-for="it in report.items" :key="it.itemNo">
            <view class="cell-name">{{ it.itemName }}</view>
            <view
              class="cell-day"
              :class="[cellCls((it.days || [])[d - 1]), { 'is-today': d === todayDay }]"
              v-for="d in report.daysInMonth"
              :key="d"
            >{{ cellText((it.days || [])[d - 1]) }}</view>
          </view>
        </view>
      </scroll-view>
      <view v-if="!report.items.length" class="empty">暂无点检数据</view>
    </view>

    <!-- 异常记录 -->
    <view class="card">
      <view class="card-title">异常记录</view>
      <view v-if="report.abnormalRecords && report.abnormalRecords.length">
        <view class="abn-item" v-for="r in report.abnormalRecords" :key="r.id">
          <view class="abn-head">
            <text class="abn-date">{{ (r.inspectDate || '').slice(0, 10) }}</text>
            <text class="abn-tag">异常</text>
            <text class="abn-inspector">{{ r.inspector || '' }}</text>
          </view>
          <view class="abn-items" v-if="r.abnormalItems && r.abnormalItems.length">
            <text class="chip" v-for="(a, i) in r.abnormalItems" :key="i">{{ a.itemNo }}.{{ a.itemName }}</text>
          </view>
          <view class="abn-note" v-if="r.abnormalNote">记录：{{ r.abnormalNote }}</view>
          <view class="abn-note" v-if="r.remark">备注：{{ r.remark }}</view>
        </view>
      </view>
      <view v-else class="empty">本月无异常记录</view>
    </view>
  </view>
</template>

<script setup>
import { ref, computed, getCurrentInstance, nextTick } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { requireAuth, fmtDate } from '../../utils/format'
import { apiEquipments, apiInspectionReport } from '../../api'

const instance = getCurrentInstance()
const equipments = ref([])
const equipmentIndex = ref(-1)
const yearMonth = ref(fmtDate(new Date()).slice(0, 7))
const shift = ref('Day')
const report = ref({ checkedDays: 0, items: [], abnormalRecords: [], daysInMonth: 30 })
const todayDay = ref(0)
const scrollLeft = ref(0)

const currentEquipment = computed(() => equipments.value[equipmentIndex.value] || null)
const abnormalCount = computed(() => (report.value.abnormalRecords || []).length)
const restDays = computed(() => {
  // 小程序运行环境对 flatMap 支持不稳定，用普通循环统计「休」的天数
  let count = 0
  const items = report.value.items || []
  for (let i = 0; i < items.length; i++) {
    const days = items[i].days || []
    for (let j = 0; j < days.length; j++) {
      if (days[j] === 'Rest') count++
    }
  }
  return count
})
const gridWidth = computed(() => `${180 + (report.value.daysInMonth || 30) * 44 + 44}rpx`)

onShow(() => {
  if (!requireAuth()) return
  loadBase()
})

async function loadBase() {
  try {
    const eqs = await apiEquipments()
    equipments.value = eqs || []
    if (equipments.value.length && equipmentIndex.value < 0) {
      equipmentIndex.value = 0
    }
    loadReport()
  } catch {
    /* 接口层已提示 */
  }
}

function onEquipmentChange(e) {
  equipmentIndex.value = Number(e.detail.value)
  loadReport()
}

function onMonthChange(e) {
  yearMonth.value = e.detail.value.slice(0, 7)
  loadReport()
}

function switchShift(s) {
  shift.value = s
  loadReport()
}

async function loadReport() {
  const eq = currentEquipment.value
  try {
    // 未选设备时不要传 undefined，避免被序列化成 "equipmentId=undefined" 导致后端 400
    const params = { yearMonth: yearMonth.value, shift: shift.value }
    if (eq) params.equipmentId = eq.id
    const r = await apiInspectionReport(params)
    report.value = r || { checkedDays: 0, items: [], abnormalRecords: [], daysInMonth: 30 }
    updateToday()
    scrollToToday()
  } catch {
    /* 接口层已提示 */
  }
}

// 查看的是本月时高亮今日列，跨月则不显示
function updateToday() {
  const currentMonth = fmtDate(new Date()).slice(0, 7)
  todayDay.value = report.value.yearMonth === currentMonth ? new Date().getDate() : 0
}

// 自动横滑让今日列贴近右侧可见，免去手动拖到底
function scrollToToday() {
  nextTick(() => {
    if (!todayDay.value) {
      scrollLeft.value = 0
      return
    }
    const sys = uni.getSystemInfoSync()
    const rpx2px = sys.windowWidth / 750
    const nameColPx = 180 * rpx2px
    const dayWidthPx = 44 * rpx2px
    // 目标：今日列右缘 - 可视宽度 + 右侧留白（让最后一天也不贴边）
    const target = nameColPx + todayDay.value * dayWidthPx
    const marginPx = 40 * rpx2px
    const measure = (width) => {
      scrollLeft.value = Math.max(0, target - width + marginPx)
    }
    if (instance && instance.proxy) {
      const q = uni.createSelectorQuery().in(instance.proxy)
      q.select('.grid-scroll').boundingClientRect((rect) => {
        measure((rect && rect.width) || sys.windowWidth - 48 * rpx2px)
      }).exec()
    } else {
      measure(sys.windowWidth - 48 * rpx2px)
    }
  })
}

function cellText(s) {
  if (s === 'Ok') return '✓'
  if (s === 'Abnormal') return '✗'
  if (s === 'Stopped') return '停'
  if (s === 'Rest') return '休'
  return ''
}

function cellCls(s) {
  if (s === 'Ok') return 'c-ok'
  if (s === 'Abnormal') return 'c-abn'
  if (s === 'Stopped') return 'c-sto'
  if (s === 'Rest') return 'c-res'
  return ''
}
</script>

<style scoped>
.page {
  padding: 24rpx;
  padding-bottom: 60rpx;
}

.form-row {
  display: flex;
  align-items: center;
  padding: 20rpx 0;
  border-bottom: 1rpx dashed #e4e9f0;
}

.form-row:last-child {
  border-bottom: none;
}

.label {
  width: 120rpx;
  font-size: 26rpx;
  color: #5c6b82;
  flex-shrink: 0;
}

.picker-box {
  flex: 1;
  font-size: 28rpx;
  color: #0f1f33;
  padding: 10rpx 20rpx;
  background: #f5f7fa;
  border-radius: 12rpx;
}

.picker-box.placeholder {
  color: #a0aebf;
}

.shift-group {
  display: flex;
  gap: 16rpx;
}

.shift-btn {
  padding: 10rpx 36rpx;
  border-radius: 999rpx;
  font-size: 26rpx;
  background: #f1f4f8;
  color: #5c6b82;
  border: 2rpx solid transparent;
}

.shift-btn.active {
  background: #0e2238;
  color: #fff;
}

.stats-row {
  display: flex;
  gap: 16rpx;
}

.stat {
  flex: 1;
  text-align: center;
  padding: 16rpx 0;
  background: #f8fafc;
  border-radius: 16rpx;
}

.v {
  font-size: 40rpx;
  font-weight: 800;
  color: #0f1f33;
}

.v.red {
  color: #dc2626;
}

.v.amber {
  color: #b45309;
}

.l {
  font-size: 22rpx;
  color: #5c6b82;
  margin-top: 4rpx;
}

.card-sub {
  font-size: 20rpx;
  color: #94a3b8;
  font-weight: 400;
  margin-left: 12rpx;
}

.grid-scroll {
  margin-top: 16rpx;
  white-space: nowrap;
}

.grid {
  display: table;
  border-collapse: collapse;
}

.grid-row {
  display: table-row;
}

.grid-row.head .cell-name,
.grid-row.head .cell-day {
  background: #0e2238;
  color: #fff;
  font-weight: 600;
}

.grid-row.head .cell-day.is-today {
  background: #f59e0b;
}

.day-num {
  display: block;
  line-height: 1.2;
}

.day-mark {
  display: block;
  font-size: 16rpx;
  background: #fff;
  color: #b45309;
  border-radius: 6rpx;
  width: 26rpx;
  margin: 2rpx auto 0;
  line-height: 22rpx;
  font-weight: 700;
}

.cell-day.is-today:not(.head-day) {
  border-color: #fcd34d;
  background: #fff8e1;
}

.cell-spacer {
  display: table-cell;
  width: 44rpx;
  min-width: 44rpx;
  border: none;
  background: transparent;
}

.cell-name {
  display: table-cell;
  width: 180rpx;
  min-width: 180rpx;
  padding: 12rpx 10rpx;
  font-size: 22rpx;
  color: #0f1f33;
  background: #f8fafc;
  border: 1rpx solid #e4e9f0;
  position: sticky;
  left: 0;
  z-index: 2;
  white-space: normal;
  word-break: break-all;
  line-height: 1.3;
}

.cell-day {
  display: table-cell;
  width: 44rpx;
  min-width: 44rpx;
  height: 60rpx;
  text-align: center;
  vertical-align: middle;
  font-size: 22rpx;
  color: #0f1f33;
  border: 1rpx solid #e4e9f0;
}

.c-ok {
  background: #ecfdf5;
  color: #059669;
  font-weight: 700;
}

.c-abn {
  background: #fef2f2;
  color: #dc2626;
  font-weight: 700;
}

.c-sto {
  background: #f3f4f6;
  color: #6b7280;
}

.c-res {
  background: #fffbeb;
  color: #b45309;
}

.abn-item {
  padding: 20rpx 0;
  border-bottom: 1rpx dashed #e4e9f0;
}

.abn-item:last-child {
  border-bottom: none;
  padding-bottom: 0;
}

.abn-head {
  display: flex;
  align-items: center;
  gap: 14rpx;
}

.abn-date {
  font-size: 26rpx;
  font-weight: 700;
  color: #0f1f33;
}

.abn-tag {
  font-size: 20rpx;
  color: #dc2626;
  background: #fef2f2;
  border-radius: 6rpx;
  padding: 2rpx 12rpx;
}

.abn-inspector {
  font-size: 22rpx;
  color: #94a3b8;
}

.abn-items {
  display: flex;
  flex-wrap: wrap;
  gap: 10rpx;
  margin-top: 12rpx;
}

.chip {
  font-size: 20rpx;
  color: #b91c1c;
  background: #fef2f2;
  border-radius: 6rpx;
  padding: 4rpx 12rpx;
}

.abn-note {
  font-size: 22rpx;
  color: #5c6b82;
  margin-top: 10rpx;
  line-height: 1.5;
}
</style>
