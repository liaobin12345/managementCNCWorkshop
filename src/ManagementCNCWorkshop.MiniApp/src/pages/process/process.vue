<template>
  <view class="page">
    <view class="tabs">
      <view class="tab" :class="{ active: tab === 'InProgress' }" @click="switchTab('InProgress')">加工中</view>
      <view class="tab" :class="{ active: tab === 'Completed' }" @click="switchTab('Completed')">已完成</view>
    </view>

    <!-- 流转卡列表 -->
    <view class="card" v-for="c in cards" :key="c.id" @click="toggleDetail(c.id)">
      <view class="card-head">
        <view class="head-l">
          <text class="code">{{ c.code }}</text>
          <text class="tag" :class="c.status === 'Completed' ? 'green' : 'amber'">
            {{ c.status === 'Completed' ? '已完成' : '加工中' }}
          </text>
        </view>
        <view class="head-r">{{ fmtDate(c.createdAt) }}</view>
      </view>
      <view class="card-body">
        <view class="prod">{{ c.productName }} {{ c.productSpec }}</view>
        <view class="flow">工艺：{{ c.flowName }} · 批次 {{ fmtQty(c.quantity) }} 件</view>
        <view class="flow" v-if="c.materialSpec || c.surfaceTreatment">
          材料 {{ c.materialSpec || '-' }} · 表面处理 {{ c.surfaceTreatment || '-' }}
        </view>
      </view>
      <view class="progress-row">
        <text class="progress-txt">工序进度 {{ c.currentStepNo }}/{{ stepCount(c) }}</text>
        <view class="progress-bar">
          <view class="progress-inner" :style="{ width: progress(c) + '%' }"></view>
        </view>
      </view>

      <!-- 展开详情 -->
      <view v-if="expandedId === c.id" class="detail" @click.stop>
        <view class="detail-steps" v-if="detail">
          <view
            class="step"
            v-for="s in detail.cardSteps"
            :key="s.stepNo"
            :class="s.status"
          >
            <view class="step-left">
              <view class="step-dot">{{ s.stepNo }}</view>
              <view class="step-txt">
                <view class="step-name">{{ s.stepName }}</view>
                <view class="step-meta">
                  <text v-if="s.completedQty > 0" class="qty-ok">已报 {{ fmtQty(s.completedQty) }} 件</text>
                  <text v-else>未报工</text>
                  <text v-if="s.qualifiedQty > 0" class="qty-ok"> · 合格 {{ fmtQty(s.qualifiedQty) }}</text>
                  <text v-if="s.defectQty > 0" class="qty-bad"> · 不良 {{ fmtQty(s.defectQty) }}</text>
                  <text v-if="s.machineNo"> · 机台 {{ s.machineNo }}</text>
                  <text v-if="s.shift"> · {{ s.shift }}</text>
                </view>
                <view class="step-meta" v-if="s.operatorName || s.inspectorName || s.workDate">
                  <text v-if="s.workDate">{{ s.workDate }}</text>
                  <text v-if="s.operatorName"> · 操作 {{ s.operatorName }}</text>
                  <text v-if="s.inspectorName"> · 检查 {{ s.inspectorName }}</text>
                </view>
              </view>
            </view>
            <text class="step-status" :class="s.status">
              {{ s.status === 'Completed' ? '✓' : s.status === 'Running' ? '●' : '○' }}
            </text>
          </view>
        </view>

        <view v-if="detail?.status === 'InProgress'" class="advance-area">
          <button class="advance-btn" :loading="advancing" @click="advance(c.id)">
            {{ detail.currentStepNo === 0 ? '开始第一道工序' : detail.currentStepNo >= detail.cardSteps.length ? '完成最后一道工序' : `完成「${currentStepName(detail)}」，流转下一步` }}
          </button>
        </view>
        <view v-else class="done-tip">✅ 本批次所有工序已完成</view>
      </view>
    </view>

    <view v-if="!cards.length" class="empty">暂无{{ tab === 'InProgress' ? '加工中' : '已完成' }}的流转卡</view>
  </view>
</template>

<script setup>
import { ref } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { requireAuth, fmtDate, fmtQty } from '../../utils/format'
import { apiProcessCards, apiProcessCardDetail, apiAdvanceProcessCard } from '../../api'

const tab = ref('InProgress')
const cards = ref([])
const detail = ref(null)
const expandedId = ref(null)
const advancing = ref(false)

onShow(() => {
  if (!requireAuth()) return
  loadData()
})

function switchTab(t) {
  tab.value = t
  expandedId.value = null
  detail.value = null
  loadData()
}

async function loadData() {
  try {
    cards.value = await apiProcessCards({ status: tab.value })
  } catch (e) {}
}

function stepCount(c) {
  return c.cardSteps?.length || c.totalSteps || 0
}

function progress(c) {
  const total = stepCount(c)
  if (!total) return 0
  return Math.min(100, Math.round((c.currentStepNo / total) * 100))
}

async function toggleDetail(id) {
  if (expandedId.value === id) {
    expandedId.value = null
    detail.value = null
    return
  }
  expandedId.value = id
  detail.value = null
  try {
    detail.value = await apiProcessCardDetail(id)
  } catch (e) {}
}

function currentStepName(d) {
  const step = d.cardSteps.find((s) => s.stepNo === d.currentStepNo)
  return step ? step.stepName : ''
}

async function advance(id) {
  advancing.value = true
  try {
    const d = await apiAdvanceProcessCard(id)
    detail.value = d
    uni.showToast({ title: d.status === 'Completed' ? '流转完成' : '已流转到下一步', icon: 'success' })
    loadData()
  } catch (e) {
  } finally {
    advancing.value = false
  }
}
</script>

<style scoped>
.page {
  padding: 16rpx 24rpx 40rpx;
  background: #eef2f7;
  min-height: 100vh;
}

.tabs {
  display: flex;
  background: #fff;
  border-radius: 12rpx;
  margin: 16rpx 0;
  padding: 6rpx;
  gap: 6rpx;
}

.tab {
  flex: 1;
  text-align: center;
  padding: 14rpx 0;
  font-size: 28rpx;
  color: #64748b;
  border-radius: 8rpx;
}

.tab.active {
  background: #0e2238;
  color: #fff;
  font-weight: 600;
}

.card {
  background: #fff;
  border-radius: 16rpx;
  padding: 24rpx;
  margin-bottom: 20rpx;
  box-shadow: 0 2rpx 8rpx rgba(15, 23, 42, 0.05);
}

.card-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.head-l {
  display: flex;
  align-items: center;
  gap: 12rpx;
}

.code {
  font-size: 30rpx;
  font-weight: 700;
  color: #0f172a;
}

.tag {
  font-size: 20rpx;
  padding: 4rpx 14rpx;
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

.head-r {
  font-size: 24rpx;
  color: #94a3b8;
}

.card-body {
  margin-top: 16rpx;
}

.prod {
  font-size: 30rpx;
  color: #1e293b;
  font-weight: 600;
}

.flow {
  font-size: 24rpx;
  color: #64748b;
  margin-top: 6rpx;
}

.progress-row {
  margin-top: 16rpx;
  display: flex;
  align-items: center;
  gap: 16rpx;
}

.progress-txt {
  font-size: 22rpx;
  color: #64748b;
  flex-shrink: 0;
}

.progress-bar {
  flex: 1;
  height: 10rpx;
  background: #e2e8f0;
  border-radius: 10rpx;
  overflow: hidden;
}

.progress-inner {
  height: 100%;
  background: linear-gradient(90deg, #f59e0b, #fbbf24);
  border-radius: 10rpx;
  transition: width 0.3s;
}

.detail {
  margin-top: 20rpx;
  border-top: 2rpx solid #f1f5f9;
  padding-top: 20rpx;
}

.step {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14rpx 0;
  border-bottom: 2rpx dashed #f1f5f9;
}

.step:last-child {
  border-bottom: none;
}

.step-left {
  display: flex;
  align-items: center;
  gap: 16rpx;
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

.step-meta {
  font-size: 22rpx;
  color: #94a3b8;
  margin-top: 4rpx;
}

.qty-ok {
  color: #059669;
}

.qty-bad {
  color: #dc2626;
}

.step-status {
  font-size: 30rpx;
}

.step-status.Completed {
  color: #10b981;
}

.step-status.Running {
  color: #f59e0b;
}

.step-status.Pending {
  color: #cbd5e1;
}

.advance-area {
  margin-top: 20rpx;
}

.advance-btn {
  background: linear-gradient(135deg, #f59e0b, #fbbf24);
  color: #fff;
  font-size: 30rpx;
  font-weight: 600;
  border-radius: 12rpx;
  padding: 20rpx 0;
}

.advance-btn::after {
  border: none;
}

.done-tip {
  text-align: center;
  padding: 20rpx;
  color: #059669;
  font-size: 26rpx;
}

.empty {
  text-align: center;
  color: #94a3b8;
  font-size: 26rpx;
  padding: 80rpx 0;
}
</style>
