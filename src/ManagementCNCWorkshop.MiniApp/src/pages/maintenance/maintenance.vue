<template>
  <view class="page">
    <view class="tabs">
      <view class="tab" :class="{ active: tab === 'pending' }" @click="switchTab('pending')">待处理提醒</view>
      <view class="tab" :class="{ active: tab === 'plans' }" @click="switchTab('plans')">保养计划</view>
    </view>

    <!-- 待处理提醒 -->
    <view v-if="tab === 'pending'">
      <view class="card">
        <view class="card-title">待处理</view>
        <view class="todo-item" v-for="r in reminders" :key="r.id">
          <view class="dot" :class="r.status === 'Overdue' ? 'red' : 'amber'"></view>
          <view class="todo-txt">
            <view class="t1">{{ r.equipment?.name || '设备' }}</view>
            <view class="t2">{{ r.plan?.planName || '保养' }} · 截止 {{ fmtDate(r.dueDate) }}（{{ dueText(r.dueDate) }}）</view>
            <view class="t2" v-if="r.plan?.content">内容：{{ r.plan.content }}</view>
          </view>
          <view class="todo-right">
            <text class="tag" :class="reminderStatusTag(r.status)">{{ reminderStatus(r.status) }}</text>
            <button class="complete-btn" size="mini" @tap="completeReminder(r)">完成保养</button>
          </view>
        </view>
        <view v-if="!reminders.length" class="empty">暂无待处理提醒</view>
      </view>
    </view>

    <!-- 保养计划 -->
    <view v-else>
      <view class="card">
        <view class="card-title">保养计划</view>
        <view class="list-item" v-for="(p, i) in plans" :key="i">
          <view class="list-av">{{ (p.equipment?.code || 'EQ').slice(-4) }}</view>
          <view class="list-bd">
            <view class="list-n1">{{ p.planName }}</view>
            <view class="list-n2">{{ p.equipment?.name || '设备' }} · 周期 {{ p.cycleDays }} 天 · 提前 {{ p.remindDaysBefore }} 天提醒</view>
          </view>
          <view class="list-rt">
            <view class="list-r1" :style="{ color: dueColor(p.nextDueDate) }">{{ fmtDate(p.nextDueDate) }}</view>
            <view class="list-r2">{{ overdue(p.nextDueDate) ? '已逾期' : '下次保养' }}</view>
          </view>
        </view>
        <view v-if="!plans.length" class="empty">暂无保养计划</view>
      </view>
    </view>
  </view>
</template>

<script setup>
import { ref } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { requireAuth, fmtDate, reminderStatus, reminderStatusTag } from '../../utils/format'
import { apiRemindersPending, apiMaintenancePlans, apiCompleteReminder } from '../../api'

const tab = ref('pending')
const reminders = ref([])
const plans = ref([])

onShow(() => {
  if (!requireAuth()) return
  loadData()
})

function switchTab(t) {
  tab.value = t
  loadData()
}

function loadData() {
  if (tab.value === 'pending') {
    apiRemindersPending().then((r) => (reminders.value = r || [])).catch(() => {})
  } else {
    apiMaintenancePlans().then((r) => (plans.value = r || [])).catch(() => {})
  }
}

function completeReminder(r) {
  uni.showModal({
    title: '完成保养',
    content: `确认已完成「${r.equipment?.name || '设备'}」的${r.plan?.planName || '保养'}？\n完成后将自动安排下一次保养日期。`,
    success: (res) => {
      if (!res.confirm) return
      apiCompleteReminder(r.id)
        .then(() => {
          uni.showToast({ title: '保养已完成 ✓', icon: 'success' })
          loadData()
        })
        .catch(() => {})
    },
  })
}

function daysLeft(dt) {
  const due = new Date(dt).getTime()
  const now = Date.now()
  return Math.ceil((due - now) / 86400000)
}

function dueText(dt) {
  const n = daysLeft(dt)
  if (n < 0) return `已逾期 ${-n} 天`
  if (n === 0) return '今天到期'
  return `还有 ${n} 天`
}

function overdue(dt) {
  return daysLeft(dt) < 0
}

function dueColor(dt) {
  return overdue(dt) ? '#ef4444' : '#b45309'
}
</script>

<style scoped>
.page {
  padding: 24rpx;
}

.todo-item {
  display: flex;
  align-items: center;
  gap: 20rpx;
  padding: 20rpx 0;
  border-bottom: 1rpx dashed #e4e9f0;
}

.todo-item:last-child {
  border-bottom: none;
  padding-bottom: 0;
}

.dot {
  width: 20rpx;
  height: 20rpx;
  border-radius: 50%;
  flex-shrink: 0;
}

.dot.red {
  background: #ef4444;
  box-shadow: 0 0 0 8rpx rgba(239, 68, 68, 0.15);
}

.dot.amber {
  background: #f59e0b;
  box-shadow: 0 0 0 8rpx rgba(245, 158, 11, 0.18);
}

.todo-txt {
  flex: 1;
}

.todo-right {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 12rpx;
  flex-shrink: 0;
}

.complete-btn {
  background: linear-gradient(135deg, #22c55e, #4ade80);
  color: #fff;
  font-size: 24rpx;
  font-weight: 600;
  border-radius: 999rpx;
  padding: 0 28rpx;
  line-height: 56rpx;
  min-height: 56rpx;
  margin: 0;
}

.complete-btn::after {
  border: none;
}

.complete-btn.button-hover {
  opacity: 0.85;
}

.t1 {
  font-size: 26rpx;
  font-weight: 600;
}

.t2 {
  font-size: 22rpx;
  color: #5c6b82;
  margin-top: 4rpx;
}
</style>