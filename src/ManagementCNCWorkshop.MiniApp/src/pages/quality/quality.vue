<template>
  <view class="page">
    <view class="tabs">
      <view class="tab" :class="{ active: tab === 'entry' }" @click="switchTab('entry')">录入质检</view>
      <view class="tab" :class="{ active: tab === 'report' }" @click="switchTab('report')">统计报表</view>
    </view>

    <!-- 录入质检 -->
    <view v-if="tab === 'entry'">
      <view v-if="!isInspector" class="desc">
        当前账号为操作工角色，无质检权限。请使用质检员账号（如 E003）登录后进行质检录入。
      </view>

      <view v-else class="card">
        <view class="card-title">质检记录</view>
        <view class="field">
          <text class="field-label">产品 *</text>
          <picker mode="selector" :range="products" range-key="name" @change="onProductChange">
            <view class="field-picker">
              <text :class="{ ph: !form.productId }">{{ selectedProductText || '请选择产品' }}</text>
              <text class="arrow">▾</text>
            </view>
          </picker>
        </view>
        <view class="field">
          <text class="field-label">车间 *</text>
          <picker mode="selector" :range="workshops" range-key="name" @change="onWorkshopChange">
            <view class="field-picker">
              <text :class="{ ph: !form.workshopId }">{{ selectedWorkshopText || '请选择车间' }}</text>
              <text class="arrow">▾</text>
            </view>
          </picker>
        </view>
        <view class="field">
          <text class="field-label">质检员</text>
          <view class="field-picker readonly">{{ me.name }}（{{ me.employeeNo }}）</view>
        </view>
        <view class="field">
          <text class="field-label">抽检总数 *</text>
          <input class="field-input" type="number" v-model.number="form.sampleQty" placeholder="请输入抽检总数" placeholder-class="ph" />
        </view>
        <view class="field">
          <text class="field-label">合格数量 *</text>
          <input class="field-input" type="number" v-model.number="form.qualifiedQty" placeholder="请输入合格数量" placeholder-class="ph" @input="autoDefect" />
        </view>
        <view class="field">
          <text class="field-label">不良数量 *</text>
          <input class="field-input" type="number" v-model.number="form.defectQty" placeholder="自动计算，可修改" placeholder-class="ph" />
        </view>
        <view class="field">
          <text class="field-label">不良类型</text>
          <picker mode="selector" :range="defectTypes" @change="onDefectTypeChange">
            <view class="field-picker">
              <text :class="{ ph: !form.defectType }">{{ form.defectType || '请选择不良类型' }}</text>
              <text class="arrow">▾</text>
            </view>
          </picker>
        </view>
        <view class="field">
          <text class="field-label">备注</text>
          <textarea class="field-textarea" v-model="form.remark" placeholder="选填" placeholder-class="ph" :maxlength="200" />
        </view>
        <button class="btn blue" :loading="submitting" @click="submit">提交质检记录</button>
      </view>
    </view>

    <!-- 统计报表 -->
    <view v-else>
      <view class="tabs">
        <view class="tab" :class="{ active: rtab === 'daily' }" @click="switchRtab('daily')">日</view>
        <view class="tab" :class="{ active: rtab === 'weekly' }" @click="switchRtab('weekly')">周</view>
        <view class="tab" :class="{ active: rtab === 'monthly' }" @click="switchRtab('monthly')">月</view>
      </view>

      <view class="card">
        <view class="card-title">抽检合格率</view>
        <view class="rate-block">
          <text class="rate-num">{{ fmtPercent(passRate) }}</text>
          <view class="rate-bar">
            <view class="rate-fill" :style="{ width: passRatePct + '%' }"></view>
          </view>
          <view class="rate-legend">
            <text class="lg">抽检 {{ fmtQty(sampleSummary.sampleQty) }}</text>
            <text class="lg">合格 {{ fmtQty(sampleSummary.qualifiedQty) }}</text>
            <text class="lg">不良 {{ fmtQty(sampleSummary.defectQty) }}</text>
          </view>
        </view>
      </view>

      <view class="card">
        <view class="card-title">按产品统计</view>
        <view class="list-item" v-for="(it, i) in list" :key="i">
          <view class="list-av amber">P</view>
          <view class="list-bd">
            <view class="list-n1">{{ it.productName || '产品' + it.productId }}</view>
            <view class="list-n2">抽检 {{ fmtQty(it.totalQty) }} · 合格 {{ fmtQty(it.qualifiedQty) }}</view>
          </view>
          <view class="list-rt"><text class="tag green">{{ fmtPercent(it.passRate) }}</text></view>
        </view>
        <view v-if="!list.length" class="empty">暂无数据</view>
      </view>
    </view>
  </view>
</template>

<script setup>
import { ref, computed } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { requireAuth, currentEmployee, fmtQty, fmtPercent, currentISOWeek } from '../../utils/format'
import { apiWorkshops, apiProducts, apiCreateQuality, apiQualityDaily, apiQualityWeekly, apiQualityMonthly } from '../../api'

const defectTypes = ['尺寸超差', '表面划伤', '毛刺未清', '配合不良', '其他']

const me = ref({ name: '', employeeNo: '', role: '' })
const workshops = ref([])
const products = ref([])
const tab = ref('entry')
const rtab = ref('daily')
const list = ref([])
const submitting = ref(false)

const form = ref({
  productId: 0,
  workshopId: 0,
  sampleQty: '',
  qualifiedQty: '',
  defectQty: '',
  defectType: '',
  remark: '',
})

const isInspector = computed(() => me.value.role === 'Inspector')

const selectedProductText = computed(() => products.value.find((p) => p.id === form.value.productId)?.name || '')
const selectedWorkshopText = computed(() => workshops.value.find((w) => w.id === form.value.workshopId)?.name || '')

const sampleSummary = computed(() => {
  const total = list.value.reduce((s, i) => s + Number(i.totalQty || 0), 0)
  const qualified = list.value.reduce((s, i) => s + Number(i.qualifiedQty || 0), 0)
  return { sampleQty: total, qualifiedQty: qualified, defectQty: total - qualified }
})

const passRate = computed(() => {
  const { sampleQty, qualifiedQty } = sampleSummary.value
  return sampleQty > 0 ? qualifiedQty / sampleQty : 1
})

const passRatePct = computed(() => Math.min(100, Math.max(0, Math.round(passRate.value * 100))))

onShow(() => {
  if (!requireAuth()) return
  me.value = currentEmployee() || { name: '', employeeNo: '', role: '' }
  if (!form.value.workshopId && me.value.workshopId) {
    form.value.workshopId = me.value.workshopId
  }
  loadOptions()
})

function loadOptions() {
  apiWorkshops().then((r) => (workshops.value = r || [])).catch(() => {})
  apiProducts().then((r) => (products.value = r || [])).catch(() => {})
}

function switchTab(t) {
  tab.value = t
  if (t === 'report') loadReport()
}

function switchRtab(t) {
  rtab.value = t
  loadReport()
}

function loadReport() {
  let api
  const params = {}
  if (rtab.value === 'daily') {
    api = apiQualityDaily
  } else if (rtab.value === 'weekly') {
    const { year, week } = currentISOWeek()
    params.year = year
    params.week = week
    api = apiQualityWeekly
  } else {
    const d = new Date()
    params.year = d.getFullYear()
    params.month = d.getMonth() + 1
    api = apiQualityMonthly
  }
  api(params).then((r) => (list.value = r || [])).catch(() => {})
}

function onProductChange(e) {
  form.value.productId = products.value[Number(e.detail.value)]?.id || 0
}

function onWorkshopChange(e) {
  form.value.workshopId = workshops.value[Number(e.detail.value)]?.id || 0
}

function onDefectTypeChange(e) {
  form.value.defectType = defectTypes[Number(e.detail.value)] || ''
}

function autoDefect() {
  const q = Number(form.value.sampleQty) || 0
  const qu = Number(form.value.qualifiedQty) || 0
  form.value.defectQty = Math.max(0, q - qu)
}

function submit() {
  if (!form.value.productId) return uni.showToast({ title: '请选择产品', icon: 'none' })
  if (!form.value.workshopId) return uni.showToast({ title: '请选择车间', icon: 'none' })
  const q = Number(form.value.sampleQty)
  const qu = Number(form.value.qualifiedQty)
  if (!q || q <= 0) return uni.showToast({ title: '请输入抽检总数', icon: 'none' })
  if (!qu || qu < 0) return uni.showToast({ title: '请输入合格数量', icon: 'none' })
  if (qu > q) return uni.showToast({ title: '合格数量不能大于抽检总数', icon: 'none' })

  submitting.value = true
  apiCreateQuality({
    productId: form.value.productId,
    workshopId: form.value.workshopId,
    sampleQty: q,
    qualifiedQty: qu,
    defectQty: Math.max(0, Number(form.value.defectQty) || 0),
    defectType: form.value.defectType || null,
    remark: form.value.remark || null,
  })
    .then(() => {
      uni.showToast({ title: '质检记录已保存 ✓', icon: 'success' })
      form.value.productId = 0
      form.value.sampleQty = ''
      form.value.qualifiedQty = ''
      form.value.defectQty = ''
      form.value.defectType = ''
      form.value.remark = ''
    })
    .catch(() => {})
    .finally(() => {
      submitting.value = false
    })
}
</script>

<style scoped>
.page {
  padding: 24rpx;
}

.desc {
  background: #eef6ff;
  border: 1rpx dashed #bcd6f5;
  border-radius: 20rpx;
  padding: 20rpx 24rpx;
  font-size: 24rpx;
  color: #1e4976;
  line-height: 1.7;
  margin-bottom: 24rpx;
}

.rate-block {
  padding: 10rpx 0 4rpx;
}

.rate-num {
  font-size: 72rpx;
  font-weight: 800;
  color: #22c55e;
  display: block;
  text-align: center;
}

.rate-bar {
  margin-top: 24rpx;
  height: 28rpx;
  border-radius: 999rpx;
  background: #e8edf4;
  overflow: hidden;
}

.rate-fill {
  height: 100%;
  border-radius: 999rpx;
  background: linear-gradient(90deg, #22c55e, #4ade80);
  transition: width 0.4s;
}

.rate-legend {
  display: flex;
  justify-content: space-between;
  margin-top: 20rpx;
}

.rate-legend .lg {
  font-size: 22rpx;
  color: #5c6b82;
}

.ph {
  color: #94a3b8;
}

.arrow {
  color: #94a3b8;
}

.readonly {
  color: #0f1f33;
  background: #f1f5f9;
}

.field-textarea {
  width: 100%;
  box-sizing: border-box;
  padding: 22rpx 24rpx;
  border: 1rpx solid #e4e9f0;
  border-radius: 20rpx;
  font-size: 28rpx;
  background: #fbfcfe;
  color: #0f1f33;
  height: 140rpx;
}
</style>