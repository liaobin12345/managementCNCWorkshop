<template>
  <view class="page">
    <!-- 点检信息 -->
    <view class="card">
      <view class="card-title">点检信息</view>
      <view class="form-row">
        <text class="label">设备</text>
        <picker
          :range="equipments"
          range-key="name"
          :value="equipmentIndex"
          @change="onEquipmentChange"
        >
          <view class="picker-box" :class="{ placeholder: !currentEquipment }">
            {{ currentEquipment ? `${currentEquipment.name}（${currentEquipment.code}）` : '请选择设备' }}
          </view>
        </picker>
      </view>
      <view class="form-row">
        <text class="label">日期</text>
        <picker mode="date" :value="inspectDate" @change="onDateChange">
          <view class="picker-box">{{ inspectDate }}</view>
        </picker>
      </view>
      <view class="form-row">
        <text class="label">班次</text>
        <view class="shift-group">
          <view
            class="shift-btn"
            :class="{ active: shift === 'Day' }"
            @click="switchShift('Day')"
          >白班</view>
          <view
            class="shift-btn"
            :class="{ active: shift === 'Night' }"
            @click="switchShift('Night')"
          >夜班</view>
        </view>
      </view>
      <view v-if="existingId" class="already-tip">已有点检记录，修改后重新提交即覆盖</view>
    </view>

    <!-- 点检项 -->
    <view class="card">
      <view class="card-title">
        点检内容
        <text class="card-sub">正常打 ✓ · 异常打 ✗ · 停机选停 · 休息选休</text>
      </view>
      <view class="item" v-for="it in items" :key="it.itemNo">
        <view class="item-info">
          <text class="item-no">{{ it.itemNo }}</text>
          <text class="item-name">{{ it.itemName }}</text>
        </view>
        <view class="status-group">
          <view
            v-for="opt in statusOptions"
            :key="opt.value"
            class="status-btn"
            :class="[opt.cls, { selected: it.status === opt.value }]"
            @click="it.status = opt.value"
          >{{ opt.text }}</view>
        </view>
      </view>
    </view>

    <!-- 异常记录 -->
    <view class="card" v-if="hasAbnormal">
      <view class="card-title">异常记录 <text class="card-sub required">有异常必填</text></view>
      <textarea
        v-model="abnormalNote"
        class="textarea"
        placeholder="请填写异常的具体情况，如：导轨油油位低于下限、开动时异响等"
        maxlength="500"
      />
    </view>

    <!-- 备注 -->
    <view class="card">
      <view class="card-title">备注</view>
      <textarea v-model="remark" class="textarea" placeholder="选填，如停机原因、其他说明" maxlength="500" />
    </view>

    <button class="submit-btn" :loading="submitting" @click="onSubmit">提交点检</button>
  </view>
</template>

<script setup>
import { ref, computed } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { requireAuth, fmtDate } from '../../utils/format'
import {
  apiEquipments,
  apiInspectionTemplate,
  apiInspectionList,
  apiInspectionSubmit,
} from '../../api'

const equipments = ref([])
const equipmentIndex = ref(-1)
const inspectDate = ref(fmtDate(new Date()))
const shift = ref('Day')
const items = ref([])
const abnormalNote = ref('')
const remark = ref('')
const existingId = ref(null)
const submitting = ref(false)

const statusOptions = [
  { value: 'Ok', text: '✓ 正常', cls: 'ok' },
  { value: 'Abnormal', text: '✗ 异常', cls: 'abn' },
  { value: 'Stopped', text: '停', cls: 'sto' },
  { value: 'Rest', text: '休', cls: 'res' },
]

const currentEquipment = computed(() => equipments.value[equipmentIndex.value] || null)
const hasAbnormal = computed(() => items.value.some((i) => i.status === 'Abnormal'))

onShow(() => {
  if (!requireAuth()) return
  loadBase()
})

async function loadBase() {
  try {
    const [eqs, tmpl] = await Promise.all([apiEquipments(), apiInspectionTemplate()])
    equipments.value = eqs || []
    items.value = (tmpl || []).map((t) => ({ itemNo: t.itemNo, itemName: t.itemName, status: 'Ok' }))
    if (equipments.value.length && equipmentIndex.value < 0) {
      equipmentIndex.value = 0
    }
    loadExisting()
  } catch {
    /* 接口层已提示 */
  }
}

function onEquipmentChange(e) {
  equipmentIndex.value = Number(e.detail.value)
  loadExisting()
}

function onDateChange(e) {
  inspectDate.value = e.detail.value
  loadExisting()
}

function switchShift(s) {
  shift.value = s
  loadExisting()
}

async function loadExisting() {
  const eq = currentEquipment.value
  if (!eq) return
  try {
    const list = await apiInspectionList({
      equipmentId: eq.id,
      date: inspectDate.value,
      shift: shift.value,
    })
    const rec = (list || [])[0]
    if (rec) {
      existingId.value = rec.id
      abnormalNote.value = rec.abnormalNote || ''
      remark.value = rec.remark || ''
      items.value = items.value.map((it) => {
        const found = (rec.items || []).find((x) => x.itemNo === it.itemNo)
        return { ...it, status: found ? found.status : it.status }
      })
    } else {
      existingId.value = null
      abnormalNote.value = ''
      remark.value = ''
      items.value = items.value.map((it) => ({ ...it, status: 'Ok' }))
    }
  } catch {
    /* 接口层已提示 */
  }
}

function validate() {
  if (!currentEquipment.value) return '请先选择设备'
  if (!inspectDate.value) return '请选择点检日期'
  if (hasAbnormal.value && !abnormalNote.value.trim()) return '存在异常项，请填写异常记录'
  return ''
}

async function onSubmit() {
  const err = validate()
  if (err) {
    uni.showToast({ title: err, icon: 'none' })
    return
  }
  const eq = currentEquipment.value
  submitting.value = true
  try {
    await apiInspectionSubmit({
      equipmentId: eq.id,
      inspectDate: inspectDate.value,
      shift: shift.value,
      items: items.value.map((i) => ({ itemNo: i.itemNo, status: i.status })),
      abnormalNote: abnormalNote.value.trim() || null,
      remark: remark.value.trim() || null,
    })
    uni.showToast({ title: '点检已提交 ✓', icon: 'success' })
    await loadExisting()
  } catch {
    /* 接口层已提示 */
  } finally {
    submitting.value = false
  }
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

.already-tip {
  margin-top: 12rpx;
  font-size: 22rpx;
  color: #b45309;
}

.card-sub {
  font-size: 20rpx;
  color: #94a3b8;
  font-weight: 400;
  margin-left: 12rpx;
}

.card-sub.required {
  color: #ef4444;
}

.item {
  padding: 20rpx 0;
  border-bottom: 1rpx dashed #e4e9f0;
}

.item:last-child {
  border-bottom: none;
  padding-bottom: 0;
}

.item-info {
  display: flex;
  align-items: flex-start;
  gap: 14rpx;
  margin-bottom: 14rpx;
}

.item-no {
  width: 40rpx;
  height: 40rpx;
  border-radius: 10rpx;
  background: #eef2f7;
  color: #0e2238;
  font-size: 22rpx;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.item-name {
  flex: 1;
  font-size: 27rpx;
  color: #0f1f33;
  line-height: 40rpx;
}

.status-group {
  display: flex;
  gap: 14rpx;
  padding-left: 54rpx;
}

.status-btn {
  flex: 1;
  text-align: center;
  padding: 10rpx 0;
  border-radius: 10rpx;
  font-size: 24rpx;
  border: 2rpx solid transparent;
  color: #5c6b82;
}

.status-btn.ok {
  background: #ecfdf5;
  color: #059669;
}

.status-btn.abn {
  background: #fef2f2;
  color: #dc2626;
}

.status-btn.sto {
  background: #f3f4f6;
  color: #6b7280;
}

.status-btn.res {
  background: #fffbeb;
  color: #b45309;
}

.status-btn.selected {
  border-color: currentColor;
  font-weight: 700;
  transform: scale(1.02);
}

.textarea {
  width: 100%;
  min-height: 140rpx;
  background: #f5f7fa;
  border-radius: 12rpx;
  padding: 20rpx;
  font-size: 26rpx;
  box-sizing: border-box;
  margin-top: 8rpx;
}

.submit-btn {
  margin-top: 32rpx;
  background: linear-gradient(135deg, #0e2238, #1e4468);
  color: #fff;
  font-size: 30rpx;
  font-weight: 600;
  border-radius: 999rpx;
  line-height: 88rpx;
  height: 88rpx;
}

.submit-btn::after {
  border: none;
}

.submit-btn.button-hover {
  opacity: 0.9;
}
</style>
