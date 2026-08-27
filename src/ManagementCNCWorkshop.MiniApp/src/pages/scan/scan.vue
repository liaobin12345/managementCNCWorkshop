<template>
  <view class="page">
    <view class="desc">
      扫描产品/设备二维码后自动带入信息，选择报工工序并填写数量提交。
      若该产品有工艺流转卡，报工会自动关联到具体工序；已完工批次不可再报工。
    </view>

    <view class="scan-box" @click="startScan">
      <view class="corner c1"></view>
      <view class="corner c2"></view>
      <view class="corner c3"></view>
      <view class="corner c4"></view>
      <view class="scan-line"></view>
      <view class="scan-inner">
        <view class="i1">📷</view>
        <view class="i2">点击扫码</view>
        <view class="i3">支持 PROD:P001 / EQ:CNC-01</view>
      </view>
    </view>

    <view class="manual-row">
      <input class="manual-input" v-model="qrInput" placeholder="或手动输入二维码内容" placeholder-class="ph" />
      <view class="manual-btn" @click="resolveQr">识别</view>
    </view>

    <view class="card" v-if="scannedProduct || scannedEquipment">
      <view class="card-title">扫码结果</view>
      <view class="list-item" v-if="scannedProduct">
        <view class="list-av amber">P</view>
        <view class="list-bd">
          <view class="list-n1">{{ scannedProduct.name }}</view>
          <view class="list-n2">编码 {{ scannedProduct.code }} · 规格 {{ scannedProduct.specification || '-' }}</view>
        </view>
        <view class="list-rt"><text class="tag blue">产品</text></view>
      </view>
      <view class="list-item" v-if="scannedEquipment">
        <view class="list-av">E</view>
        <view class="list-bd">
          <view class="list-n1">{{ scannedEquipment.name }}</view>
          <view class="list-n2">编码 {{ scannedEquipment.code }}</view>
        </view>
        <view class="list-rt"><text class="tag blue">设备</text></view>
      </view>
      <view v-if="!scannedProduct" class="empty" style="padding:20rpx 0">仅识别到设备，请选择产品</view>
    </view>

    <!-- 工艺流转卡工序进度 -->
    <view class="card" v-if="progressList.length && activeCard">
      <view class="card-title">
        工艺流转卡
        <view class="card-tabs" v-if="progressList.length > 1">
          <view
            v-for="(c, i) in progressList"
            :key="c.cardId"
            class="card-tab"
            :class="{ active: i === cardIndex }"
            @click="selectCard(i)"
          >
            {{ c.cardCode }}
          </view>
        </view>
      </view>

      <view class="card-head">
        <text class="code">{{ activeCard.cardCode }}</text>
        <text class="tag" :class="activeCard.finished ? 'green' : 'amber'">{{ activeCard.statusText }}</text>
      </view>
      <view class="card-meta">
        <text>工艺 {{ activeCard.flowName || '-' }}</text>
        <text>材料 {{ activeCard.materialSpec || '-' }}</text>
        <text>表面处理 {{ activeCard.surfaceTreatment || '-' }}</text>
        <text>批次 {{ fmtQty(activeCard.quantity) }} 件 · 工序 {{ activeCard.completedSteps }}/{{ activeCard.totalSteps }}</text>
      </view>
      <view class="progress-bar">
        <view class="progress-inner" :style="{ width: cardProgress + '%' }"></view>
      </view>

      <view class="step-list">
        <view
          v-for="s in activeCard.steps"
          :key="s.stepNo"
          class="step"
          :class="[s.status, { selected: s.stepNo === selectedStepNo }]"
          @click="selectStep(s)"
        >
          <view class="step-dot">{{ s.stepNo }}</view>
          <view class="step-txt">
            <view class="step-name">{{ s.stepName }}</view>
            <view class="step-qty">
              <text v-if="s.completedQty > 0">已报 {{ fmtQty(s.completedQty) }} 件</text>
              <text v-else>尚未报工</text>
              <text v-if="s.qualifiedQty > 0" class="qty-ok"> · 合格 {{ fmtQty(s.qualifiedQty) }}</text>
              <text v-if="s.defectQty > 0" class="qty-bad"> · 不良 {{ fmtQty(s.defectQty) }}</text>
            </view>
          </view>
          <text class="step-status" :class="s.status">{{ s.statusText }}</text>
        </view>
      </view>

      <view class="finished-tip" v-if="activeCard.finished">✅ 该批次所有工序已完成，不可继续报工</view>
      <view class="running-tip" v-else>请选择要报工的工序（默认当前工序）</view>
    </view>
    <view class="card" v-else-if="form.productId">
      <view class="card-title">提示</view>
      <view class="empty">该产品暂无工艺流转卡，本次报工将不关联工序</view>
    </view>

    <!-- 报工信息 -->
    <view class="card">
      <view class="card-title">报工信息</view>

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
        <text class="field-label">报工员工</text>
        <view class="field-picker readonly">{{ me.name }}（{{ me.employeeNo }}）</view>
      </view>

      <view class="field">
        <text class="field-label">使用设备（可选）</text>
        <picker mode="selector" :range="equipments" range-key="name" @change="onEquipmentChange">
          <view class="field-picker">
            <text :class="{ ph: !form.equipmentId }">{{ selectedEquipmentText || '不选设备' }}</text>
            <text class="arrow">▾</text>
          </view>
        </picker>
      </view>

      <view class="field" v-if="activeCard && !activeCard.finished">
        <text class="field-label">报工工序 *</text>
        <picker :range="stepNames" @change="onStepChange">
          <view class="field-picker">
            <text :class="{ ph: !selectedStepNo }">{{ selectedStepName || '请选择工序' }}</text>
            <text class="arrow">▾</text>
          </view>
        </picker>
      </view>
      <view class="field" v-if="activeCard?.finished">
        <text class="field-label">报工工序</text>
        <view class="field-picker readonly">本批次已完工，禁止报工</view>
      </view>

      <view class="field">
        <text class="field-label">总产量 *</text>
        <input class="field-input" type="number" v-model.number="form.quantity" placeholder="请输入总产量" placeholder-class="ph" />
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
        <text class="field-label">备注</text>
        <textarea class="field-textarea" v-model="form.remark" placeholder="选填，如：夜班生产" placeholder-class="ph" :maxlength="200" />
      </view>

      <button class="btn" :loading="submitting" @click="submit">提交报工</button>
    </view>
  </view>
</template>

<script setup>
import { ref, computed } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { requireAuth, currentEmployee, fmtQty } from '../../utils/format'
import { apiWorkshops, apiProducts, apiEquipments, apiProductByQr, apiEquipmentByQr, apiScanReport, apiProcessCardProgress } from '../../api'

const me = ref({ name: '', employeeNo: '' })
const workshops = ref([])
const products = ref([])
const equipments = ref([])
const qrInput = ref('')
const scannedProduct = ref(null)
const scannedEquipment = ref(null)
const submitting = ref(false)

// 工艺流转卡进度
const progressList = ref([])
const cardIndex = ref(0)
const selectedStepNo = ref(0)

const form = ref({
  productId: 0,
  workshopId: 0,
  equipmentId: 0,
  quantity: '',
  qualifiedQty: '',
  defectQty: '',
  remark: '',
})

const selectedProductText = computed(() => products.value.find((p) => p.id === form.value.productId)?.name || '')
const selectedWorkshopText = computed(() => workshops.value.find((w) => w.id === form.value.workshopId)?.name || '')
const selectedEquipmentText = computed(() => equipments.value.find((e) => e.id === form.value.equipmentId)?.name || '')

const activeCard = computed(() => progressList.value[cardIndex.value] || null)
const cardProgress = computed(() => {
  const c = activeCard.value
  if (!c || !c.totalSteps) return 0
  return Math.min(100, Math.round((c.completedSteps / c.totalSteps) * 100))
})
const stepNames = computed(() => {
  const c = activeCard.value
  if (!c) return []
  return c.steps.filter((s) => !s.finished).map((s) => `${s.stepNo}. ${s.stepName}（${s.statusText}）`)
})
const selectedStepName = computed(() => {
  const c = activeCard.value
  if (!c || !selectedStepNo.value) return ''
  const s = c.steps.find((x) => x.stepNo === selectedStepNo.value)
  return s ? `${s.stepNo}. ${s.stepName}` : ''
})

onShow(() => {
  if (!requireAuth()) return
  me.value = currentEmployee() || { name: '', employeeNo: '' }
  if (!form.value.workshopId && me.value.workshopId) {
    form.value.workshopId = me.value.workshopId
  }
  loadOptions()
})

function loadOptions() {
  apiWorkshops().then((r) => (workshops.value = r || [])).catch(() => {})
  apiProducts().then((r) => (products.value = r || [])).catch(() => {})
  apiEquipments().then((r) => (equipments.value = r || [])).catch(() => {})
}

function startScan() {
  // #ifdef H5
  uni.showToast({ title: 'H5 端请手动输入二维码', icon: 'none' })
  // #endif
  // #ifndef H5
  uni.scanCode({
    success: (res) => {
      qrInput.value = res.result || ''
      resolveQr()
    },
    fail: (err) => {
      uni.showToast({ title: '扫码失败：' + (err.errMsg || '请重试'), icon: 'none' })
    },
  })
  // #endif
}

function resolveQr() {
  const code = (qrInput.value || '').trim()
  if (!code) {
    uni.showToast({ title: '请输入二维码内容', icon: 'none' })
    return
  }
  if (code.startsWith('PROD:')) {
    queryProduct(code)
  } else if (code.startsWith('EQ:')) {
    queryEquipment(code)
  } else {
    uni.showModal({
      title: '无法识别二维码',
      content: `"${code}" 不是有效的产品/设备码（支持 PROD:xxx / EQ:xxx）`,
      showCancel: false,
    })
  }
}

function queryProduct(code) {
  apiProductByQr(code)
    .then((p) => {
      scannedProduct.value = p
      form.value.productId = p.id
      loadProgress(p.id)
      uni.showToast({ title: `识别到产品：${p.name}`, icon: 'none' })
    })
    .catch(() => {})
}

function queryEquipment(code) {
  apiEquipmentByQr(code)
    .then((e) => {
      scannedEquipment.value = e
      form.value.equipmentId = e.id
      uni.showToast({ title: `识别到设备：${e.name}`, icon: 'none' })
    })
    .catch(() => {})
}

function onProductChange(e) {
  const id = products.value[Number(e.detail.value)]?.id || 0
  form.value.productId = id
  if (id) {
    loadProgress(id)
  } else {
    resetProgress()
  }
}

async function loadProgress(productId) {
  try {
    const list = (await apiProcessCardProgress({ productId })) || []
    progressList.value = list
    // 默认选中加工中的卡，否则第一张
    const idx = list.findIndex((c) => !c.finished)
    cardIndex.value = idx >= 0 ? idx : 0
    resetSelectedStep()
  } catch (e) {
    resetProgress()
  }
}

function resetProgress() {
  progressList.value = []
  cardIndex.value = 0
  selectedStepNo.value = 0
}

function selectCard(i) {
  cardIndex.value = i
  resetSelectedStep()
}

function resetSelectedStep() {
  const c = activeCard.value
  if (!c) {
    selectedStepNo.value = 0
    return
  }
  if (c.finished) {
    selectedStepNo.value = 0
    return
  }
  // 默认当前工序；当前工序已完成则选下一个未完成
  const cur = c.steps.find((s) => s.stepNo === c.currentStepNo)
  const target =
    cur && !cur.finished
      ? cur
      : c.steps.find((s) => !s.finished)
  selectedStepNo.value = target?.stepNo || 0
}

function selectStep(s) {
  if (s.finished) {
    uni.showToast({ title: `「${s.stepName}」已完成，请选择未完成工序`, icon: 'none' })
    return
  }
  selectedStepNo.value = s.stepNo
}

function onStepChange(e) {
  const list = stepNames.value
  const item = list[Number(e.detail.value)]
  if (item) {
    selectedStepNo.value = Number(item.split('.')[0])
  }
}

function onWorkshopChange(e) {
  form.value.workshopId = workshops.value[Number(e.detail.value)]?.id || 0
}

function onEquipmentChange(e) {
  const idx = Number(e.detail.value)
  form.value.equipmentId = idx >= 0 ? (equipments.value[idx]?.id || 0) : 0
}

function autoDefect() {
  const q = Number(form.value.quantity) || 0
  const qu = Number(form.value.qualifiedQty) || 0
  form.value.defectQty = Math.max(0, q - qu)
}

function submit() {
  if (!form.value.productId) return uni.showToast({ title: '请选择产品', icon: 'none' })
  if (!form.value.workshopId) return uni.showToast({ title: '请选择车间', icon: 'none' })
  const c = activeCard.value
  if (c && c.finished) {
    uni.showToast({ title: '该批次已完工，不能报工', icon: 'none' })
    return
  }
  if (c && !selectedStepNo.value) {
    return uni.showToast({ title: '请选择报工工序', icon: 'none' })
  }
  const q = Number(form.value.quantity)
  const qu = Number(form.value.qualifiedQty)
  if (!q || q <= 0) return uni.showToast({ title: '请输入总产量', icon: 'none' })
  if (!qu || qu < 0) return uni.showToast({ title: '请输入合格数量', icon: 'none' })
  if (qu > q) return uni.showToast({ title: '合格数量不能大于总产量', icon: 'none' })

  submitting.value = true
  apiScanReport({
    productId: form.value.productId,
    workshopId: form.value.workshopId,
    equipmentId: form.value.equipmentId || null,
    quantity: q,
    qualifiedQty: qu,
    defectQty: Math.max(0, Number(form.value.defectQty) || 0),
    scanPayload: qrInput.value || null,
    processCardId: c ? c.cardId : null,
    processStepNo: c ? selectedStepNo.value : null,
    remark: form.value.remark || null,
  })
    .then(() => {
      uni.showToast({ title: '报工提交成功 ✓', icon: 'success' })
      resetForm()
    })
    .catch(() => {})
    .finally(() => {
      submitting.value = false
    })
}

function resetForm() {
  form.value.productId = 0
  form.value.equipmentId = 0
  form.value.quantity = ''
  form.value.qualifiedQty = ''
  form.value.defectQty = ''
  form.value.remark = ''
  scannedProduct.value = null
  scannedEquipment.value = null
  qrInput.value = ''
  resetProgress()
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

.scan-box {
  height: 320rpx;
  border-radius: 28rpx;
  margin-bottom: 20rpx;
  position: relative;
  background: linear-gradient(135deg, #0e2238, #1e4468);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #ffffff;
  overflow: hidden;
}

.corner {
  position: absolute;
  width: 60rpx;
  height: 60rpx;
  border: 6rpx solid #f59e0b;
}

.c1 { top: 40rpx; left: 40rpx; border-right: none; border-bottom: none; border-radius: 16rpx 0 0 0; }
.c2 { top: 40rpx; right: 40rpx; border-left: none; border-bottom: none; border-radius: 0 16rpx 0 0; }
.c3 { bottom: 40rpx; left: 40rpx; border-right: none; border-top: none; border-radius: 0 0 0 16rpx; }
.c4 { bottom: 40rpx; right: 40rpx; border-left: none; border-top: none; border-radius: 0 0 16rpx 0; }

.scan-line {
  position: absolute;
  left: 80rpx;
  right: 80rpx;
  height: 4rpx;
  top: 70rpx;
  background: linear-gradient(90deg, transparent, #f59e0b, transparent);
  animation: scanmove 2.2s ease-in-out infinite;
}

@keyframes scanmove {
  0%, 100% { top: 70rpx; }
  50% { top: 250rpx; }
}

.scan-inner {
  text-align: center;
  z-index: 1;
}

.scan-inner .i1 { font-size: 64rpx; }
.scan-inner .i2 { font-size: 28rpx; margin-top: 12rpx; font-weight: 600; }
.scan-inner .i3 { font-size: 22rpx; margin-top: 8rpx; opacity: 0.6; }

.manual-row {
  display: flex;
  gap: 16rpx;
  margin-bottom: 24rpx;
}

.manual-input {
  flex: 1;
  background: #ffffff;
  border-radius: 20rpx;
  min-height: 92rpx;
  padding: 0 24rpx;
  font-size: 26rpx;
  border: 1rpx solid #e4e9f0;
}

.manual-btn {
  padding: 20rpx 32rpx;
  background: #16334f;
  color: #ffffff;
  border-radius: 20rpx;
  font-size: 26rpx;
  font-weight: 600;
  display: flex;
  align-items: center;
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

.card-title {
  font-size: 26rpx;
  font-weight: 700;
  color: #0f1f33;
  margin-bottom: 16rpx;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.card-tabs {
  display: flex;
  gap: 8rpx;
}

.card-tab {
  font-size: 20rpx;
  padding: 4rpx 16rpx;
  border-radius: 20rpx;
  background: #f1f5f9;
  color: #64748b;
}

.card-tab.active {
  background: #0e2238;
  color: #fff;
  font-weight: 600;
}

.card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.code {
  font-size: 30rpx;
  font-weight: 700;
  color: #0f172a;
}

.tag {
  font-size: 20rpx;
  padding: 4rpx 16rpx;
  border-radius: 20rpx;
}

.tag.green {
  background: #ecfdf5;
  color: #059669;
}

.tag.amber {
  background: #fff7ed;
  color: #d97706;
}

.tag.blue {
  background: #eff6ff;
  color: #2563eb;
}

.card-meta {
  margin-top: 12rpx;
  font-size: 22rpx;
  color: #64748b;
  display: flex;
  flex-direction: column;
  gap: 4rpx;
}

.progress-bar {
  height: 10rpx;
  background: #e2e8f0;
  border-radius: 10rpx;
  overflow: hidden;
  margin-top: 14rpx;
}

.progress-inner {
  height: 100%;
  background: linear-gradient(90deg, #10b981, #34d399);
  border-radius: 10rpx;
  transition: width 0.3s;
}

.step-list {
  margin-top: 20rpx;
  border-top: 2rpx solid #f1f5f9;
}

.step {
  display: flex;
  align-items: center;
  padding: 16rpx 0;
  border-bottom: 2rpx dashed #f1f5f9;
}

.step.selected {
  background: #f0fdf4;
  margin: 0 -16rpx;
  padding-left: 16rpx;
  padding-right: 16rpx;
  border-radius: 12rpx;
}

.step-dot {
  width: 40rpx;
  height: 40rpx;
  border-radius: 50%;
  background: #e2e8f0;
  color: #64748b;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 22rpx;
  flex-shrink: 0;
}

.step.Running .step-dot {
  background: #f59e0b;
  color: #fff;
}

.step.Completed .step-dot {
  background: #10b981;
  color: #fff;
}

.step-txt {
  flex: 1;
  margin-left: 16rpx;
}

.step-name {
  font-size: 28rpx;
  color: #1e293b;
}

.step.Running .step-name {
  font-weight: 700;
  color: #d97706;
}

.step.Completed .step-name {
  color: #64748b;
  text-decoration: line-through;
}

.step-qty {
  font-size: 22rpx;
  color: #94a3b8;
  margin-top: 4rpx;
}

.step-qty .qty-ok {
  color: #059669;
}

.step-qty .qty-bad {
  color: #dc2626;
}

.step-status {
  font-size: 22rpx;
  padding: 4rpx 14rpx;
  border-radius: 20rpx;
  flex-shrink: 0;
}

.step-status.Pending {
  background: #f1f5f9;
  color: #64748b;
}

.step-status.Running {
  background: #fff7ed;
  color: #d97706;
}

.step-status.Completed {
  background: #ecfdf5;
  color: #059669;
}

.finished-tip {
  margin-top: 20rpx;
  text-align: center;
  padding: 20rpx;
  background: #ecfdf5;
  color: #059669;
  font-size: 26rpx;
  border-radius: 12rpx;
}

.running-tip {
  margin-top: 20rpx;
  text-align: center;
  padding: 16rpx;
  background: #fff7ed;
  color: #b45309;
  font-size: 24rpx;
  border-radius: 12rpx;
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
