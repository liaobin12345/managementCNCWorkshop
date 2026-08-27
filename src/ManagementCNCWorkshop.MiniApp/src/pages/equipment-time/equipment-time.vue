<template>
  <view class="page">
    <view class="card">
      <view class="card-title">设备时间录入</view>

      <view class="field">
        <text class="field-label">设备 *</text>
        <picker mode="selector" :range="equipments" range-key="name" @change="onEquipmentChange">
          <view class="field-picker">
            <text :class="{ ph: !form.EquipmentId }">{{ selectedEquipmentText || '请选择设备' }}</text>
            <text class="arrow">▾</text>
          </view>
        </picker>
      </view>

      <view class="field">
        <text class="field-label">记录日期</text>
        <picker mode="date" :value="form.RecordDate" @change="onDateChange">
          <view class="field-picker">
            <text>{{ form.RecordDate || '选择日期' }}</text>
            <text class="arrow">▾</text>
          </view>
        </picker>
      </view>

      <view class="grid">
        <view class="field">
          <text class="field-label">调试时间（小时）</text>
          <input class="field-input" type="digit" v-model="form.SetupHours" placeholder="如 1.5" placeholder-class="ph" />
        </view>
        <view class="field">
          <text class="field-label">正常开机时间（小时）</text>
          <input class="field-input" type="digit" v-model="form.RunningHours" placeholder="如 6.5" placeholder-class="ph" />
        </view>
        <view class="field">
          <text class="field-label">待机时间（小时）</text>
          <input class="field-input" type="digit" v-model="form.IdleHours" placeholder="如 0.5" placeholder-class="ph" />
        </view>
      </view>

      <view class="field">
        <text class="field-label">备注</text>
        <textarea class="field-textarea" v-model="form.Remark" placeholder="如：首件调试、换刀、等待工单" placeholder-class="ph" :maxlength="200" />
      </view>

      <button class="btn blue" :loading="saving" @click="submit">保存记录</button>
    </view>

    <view class="card" v-if="currentRecord">
      <view class="card-title">今日记录</view>
      <view class="list-item">
        <view class="list-av">{{ currentRecord.equipmentCode?.slice(-2) || 'EQ' }}</view>
        <view class="list-bd">
          <view class="list-n1">{{ currentRecord.equipmentName || '-' }}</view>
          <view class="list-n2">{{ currentRecord.recordDate }} · {{ currentRecord.employeeName || '系统录入' }}</view>
        </view>
        <view class="list-rt">
          <view class="list-r1">{{ currentRecord.runningHours }}h</view>
          <view class="list-r2">开机</view>
        </view>
      </view>
      <view class="time-grid">
        <view class="time-box">
          <view class="time-v">{{ currentRecord.setupHours }}h</view>
          <view class="time-l">调试</view>
        </view>
        <view class="time-box">
          <view class="time-v">{{ currentRecord.runningHours }}h</view>
          <view class="time-l">开机</view>
        </view>
        <view class="time-box">
          <view class="time-v">{{ currentRecord.idleHours }}h</view>
          <view class="time-l">待机</view>
        </view>
      </view>
      <view class="remark" v-if="currentRecord.remark">备注：{{ currentRecord.remark }}</view>
    </view>

    <view class="card" v-if="recent.length">
      <view class="card-title">最近 7 天</view>
      <view class="list-item" v-for="item in recent" :key="item.id">
        <view class="list-av">{{ item.recordDate.slice(5, 7) }}</view>
        <view class="list-bd">
          <view class="list-n1">{{ item.recordDate }}</view>
          <view class="list-n2">调试 {{ item.setupHours }}h · 开机 {{ item.runningHours }}h · 待机 {{ item.idleHours }}h</view>
        </view>
        <view class="list-rt"><text class="tag blue">{{ item.equipmentCode }}</text></view>
      </view>
    </view>
  </view>
</template>

<script setup>
import { computed, ref } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { requireAuth, currentEmployee, fmtDate } from '../../utils/format'
import { apiEquipments, apiGetEquipmentTime, apiRecentEquipmentTime, apiSaveEquipmentTime } from '../../api'

const me = ref({})
const equipments = ref([])
const recent = ref([])
const currentRecord = ref(null)
const saving = ref(false)
const form = ref({
  EquipmentId: 0,
  RecordDate: fmtDate(new Date()),
  SetupHours: '0',
  RunningHours: '0',
  IdleHours: '0',
  Remark: '',
})

const selectedEquipmentText = computed(() => equipments.value.find((e) => e.id === form.value.EquipmentId)?.name || '')

onShow(() => {
  if (!requireAuth()) return
  me.value = currentEmployee() || {}
  loadOptions()
})

function loadOptions() {
  apiEquipments().then((r) => {
    equipments.value = r || []
    if (!form.value.EquipmentId && equipments.value.length) {
      form.value.EquipmentId = equipments.value[0].id
      loadCurrent()
    }
  }).catch(() => {})
}

function onEquipmentChange(e) {
  const idx = Number(e.detail.value)
  form.value.EquipmentId = equipments.value[idx]?.id || 0
  loadCurrent()
}

function onDateChange(e) {
  form.value.RecordDate = e.detail.value
  loadCurrent()
}

function loadCurrent() {
  if (!form.value.EquipmentId || !form.value.RecordDate) return
  apiGetEquipmentTime(form.value.EquipmentId, form.value.RecordDate)
    .then((r) => {
      currentRecord.value = r
    })
    .catch(() => {
      currentRecord.value = null
    })
  apiRecentEquipmentTime(form.value.EquipmentId, 7)
    .then((r) => (recent.value = r || []))
    .catch(() => {})
}

function submit() {
  if (!form.value.EquipmentId) return uni.showToast({ title: '请选择设备', icon: 'none' })
  const setup = Number(form.value.SetupHours)
  const running = Number(form.value.RunningHours)
  const idle = Number(form.value.IdleHours)
  if ([setup, running, idle].some((n) => Number.isNaN(n) || n < 0)) {
    return uni.showToast({ title: '时间必须是非负数字', icon: 'none' })
  }

  saving.value = true
  apiSaveEquipmentTime({
    equipmentId: form.value.EquipmentId,
    recordDate: form.value.RecordDate,
    setupHours: setup,
    runningHours: running,
    idleHours: idle,
    remark: form.value.Remark,
  })
    .then((r) => {
      currentRecord.value = r
      uni.showToast({ title: '保存成功', icon: 'success' })
      loadCurrent()
    })
    .finally(() => {
      saving.value = false
    })
}
</script>

<style scoped>
.page {
  padding: 24rpx;
}

.grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 8rpx;
}

.time-grid {
  display: flex;
  gap: 16rpx;
  margin-top: 12rpx;
}

.time-box {
  flex: 1;
  background: #f8fbff;
  border: 1rpx solid #e4e9f0;
  border-radius: 20rpx;
  padding: 20rpx;
  text-align: center;
}

.time-v {
  font-size: 34rpx;
  font-weight: 800;
  color: #16334f;
}

.time-l {
  font-size: 22rpx;
  color: #5c6b82;
  margin-top: 6rpx;
}

.remark {
  margin-top: 16rpx;
  font-size: 24rpx;
  color: #5c6b82;
}
</style>
