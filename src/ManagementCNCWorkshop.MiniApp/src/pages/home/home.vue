<template>
  <view class="page">
    <view class="hero">
      <view class="hello">早上好，{{ me.name }} 👋</view>
      <view class="date">{{ me.workshopName || '' }} · {{ todayText() }}</view>
    </view>

    <view class="stats-row">
      <view class="stat"><view class="v">{{ fmtQty(summary.totalQty) }}</view><view class="l">今日产量</view></view>
      <view class="stat"><view class="v green">{{ fmtPercent(summary.passRate) }}</view><view class="l">今日合格率</view></view>
      <view class="stat"><view class="v red">{{ fmtQty(summary.defectQty) }}</view><view class="l">不良数量</view></view>
    </view>

    <view class="card">
      <view class="card-title">快捷操作</view>
      <view class="quick-grid">
        <view class="quick" @click="go('/pages/scan/scan')">
          <view class="qi" style="background:linear-gradient(135deg,#f59e0b,#fb923c)">📷</view>
          <text class="qn">扫码报工</text>
        </view>
        <view class="quick" @click="go('/pages/equipment-time/equipment-time')">
          <view class="qi" style="background:linear-gradient(135deg,#38bdf8,#0284c7)">⏱️</view>
          <text class="qn">设备时间</text>
        </view>
        <view class="quick" @click="go('/pages/stats/stats')">
          <view class="qi" style="background:linear-gradient(135deg,#0e2238,#1e4468)">📈</view>
          <text class="qn">产量统计</text>
        </view>
        <view class="quick" @click="go('/pages/process/process')">
          <view class="qi" style="background:linear-gradient(135deg,#f97316,#ea580c)">📋</view>
          <text class="qn">工艺流转</text>
        </view>
        <view class="quick" @click="go('/pages/quality/quality')">
          <view class="qi" style="background:linear-gradient(135deg,#22c55e,#4ade80)">✅</view>
          <text class="qn">质量追踪</text>
        </view>
        <view class="quick" @click="go('/pages/maintenance/maintenance')">
          <view class="qi" style="background:linear-gradient(135deg,#a855f7,#7c3aed)">🔧</view>
          <text class="qn">设备保养</text>
        </view>
        <view class="quick" @click="go('/pages/master/master')">
          <view class="qi" style="background:linear-gradient(135deg,#8b5cf6,#6d28d9)">🗂️</view>
          <text class="qn">数据字典</text>
        </view>
        <view class="quick" @click="go('/pages/inspection/inspection')">
          <view class="qi" style="background:linear-gradient(135deg,#14b8a6,#0d9488)">✅</view>
          <text class="qn">设备点检</text>
        </view>
        <view class="quick" @click="go('/pages/inspection-report/inspection-report')">
          <view class="qi" style="background:linear-gradient(135deg,#0e7490,#155e75)">📅</view>
          <text class="qn">点检报表</text>
        </view>
      </view>
    </view>

    <view class="card">
      <view class="card-title">
        待办·保养提醒
        <text class="card-more" @click="go('/pages/maintenance/maintenance')">查看全部 ›</text>
      </view>
      <view class="todo-item" v-for="(r, i) in reminders.slice(0, 3)" :key="i">
        <view class="dot" :class="r.status === 'Overdue' ? 'red' : 'amber'"></view>
        <view class="todo-txt">
          <view class="t1">{{ r.equipment?.name || '设备' }}</view>
          <view class="t2">{{ r.plan?.planName || '保养' }} · 截止 {{ fmtDate(r.dueDate) }}</view>
        </view>
        <text class="tag" :class="reminderStatusTag(r.status)">{{ reminderStatus(r.status) }}</text>
      </view>
      <view v-if="!reminders.length" class="empty">暂无待办提醒</view>
    </view>

    <view class="card">
      <view class="card-title">近 7 日产量趋势</view>
      <BarChart :items="trend" :highlight="6" />
    </view>

    <view class="logout-btn" @click="handleLogout">退出登录</view>
  </view>
</template>

<script setup>
import { ref, computed } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { requireAuth, currentEmployee, fmtQty, fmtPercent, fmtDate, todayText, reminderStatus, reminderStatusTag } from '../../utils/format'
import { apiSummary, apiProductionTrend, apiRemindersPending } from '../../api'
import BarChart from '../../components/BarChart.vue'

const me = ref({ name: '', workshopName: '' })
const summary = ref({ totalQty: 0, qualifiedQty: 0, defectQty: 0, passRate: 1 })
const reminders = ref([])
const trend = ref([])

onShow(() => {
  if (!requireAuth()) return
  me.value = currentEmployee() || { name: '', workshopName: '' }
  loadData()
})

function loadData() {
  apiSummary().then((r) => (summary.value = r)).catch(() => {})
  apiRemindersPending().then((r) => (reminders.value = r || [])).catch(() => {})
  apiProductionTrend(7).then((r) => {
    trend.value = (r || []).map((d) => ({ label: d.date.slice(5), value: d.totalQty }))
  }).catch(() => {})
}

function go(url) {
  const tabs = ['/pages/home/home', '/pages/scan/scan', '/pages/stats/stats']
  if (tabs.includes(url)) {
    uni.switchTab({ url })
  } else {
    uni.navigateTo({ url })
  }
}

function handleLogout() {
  uni.showModal({
    title: '退出登录',
    content: '确定要退出当前账号吗？',
    success: (r) => {
      if (r.confirm) {
        uni.removeStorageSync('token')
        uni.removeStorageSync('employee')
        uni.reLaunch({ url: '/pages/login/login' })
      }
    },
  })
}
</script>

<style scoped>
.page {
  padding: 24rpx;
}

.hero {
  margin-bottom: 8rpx;
}

.hello {
  font-size: 40rpx;
  font-weight: 800;
  color: #0f1f33;
}

.date {
  font-size: 24rpx;
  color: #5c6b82;
  margin-top: 8rpx;
}

.quick-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 16rpx 0;
}

.quick {
  width: 20%;
  text-align: center;
  padding: 16rpx 8rpx;
}

.qi {
  width: 80rpx;
  height: 80rpx;
  margin: 0 auto 12rpx;
  border-radius: 24rpx;
  font-size: 38rpx;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #ffffff;
}

.qn {
  font-size: 24rpx;
  color: #0f1f33;
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

.t1 {
  font-size: 26rpx;
  font-weight: 600;
}

.t2 {
  font-size: 22rpx;
  color: #5c6b82;
  margin-top: 4rpx;
}

.logout-btn {
  text-align: center;
  color: #94a3b8;
  font-size: 26rpx;
  padding: 40rpx 0 60rpx;
}
</style>