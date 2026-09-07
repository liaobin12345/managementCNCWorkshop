<template>
  <view class="home-page">
    <view class="hero">
      <text class="hero-title">数控车编程助手</text>
      <text class="hero-sub">拍照识图 · 自动出程序 · 效率翻倍</text>
    </view>

    <view class="card">
      <button class="btn" @click="goNew">📷 新建程序（拍照 / 上传图纸）</button>
    </view>

    <view class="card">
      <view class="card-title">
        <text>最近程序</text>
        <text class="card-more" @click="switchTab(1)">查看全部 ›</text>
      </view>
      <view v-if="programs.length === 0" class="empty">暂无程序，点击上方按钮新建</view>
      <view v-for="p in programs" :key="p.id" class="list-item" @click="goDetail(p.id)">
        <view class="list-av">{{ p.partName.slice(0, 1) }}</view>
        <view class="list-bd">
          <view class="list-n1">{{ p.partName }}</view>
          <view class="list-n2">{{ p.drawingNo || '无图号' }} · {{ p.creatorName || '我' }}</view>
        </view>
        <view class="list-rt">
          <text class="tag" :class="statusTag(p.status)">{{ statusLabel(p.status) }}</text>
        </view>
      </view>
    </view>
  </view>
</template>

<script>
import { apiPrograms } from '../../api/index'

export default {
  data() {
    return { programs: [] }
  },
  onShow() {
    this.load()
  },
  methods: {
    async load() {
      try {
        this.programs = (await apiPrograms(true)).slice(0, 5)
      } catch (_) {}
    },
    goNew() {
      uni.navigateTo({ url: '/pages/new/new' })
    },
    goDetail(id) {
      uni.navigateTo({ url: '/pages/program/program?id=' + id })
    },
    switchTab(idx) {
      uni.switchTab({ url: '/pages/library/library' })
    },
    statusTag(s) {
      return { Draft: 'gray', Confirmed: 'blue', Generated: 'green', Exported: 'amber' }[s] || 'gray'
    },
    statusLabel(s) {
      return { Draft: '草稿', Confirmed: '已确认', Generated: '已生成', Exported: '已导出' }[s] || s
    },
  },
}
</script>

<style scoped>
.hero {
  background: linear-gradient(135deg, #14326b, #2b5cff);
  padding: 40rpx 24rpx;
  color: #ffffff;
}

.hero-title {
  font-size: 40rpx;
  font-weight: 800;
  display: block;
}

.hero-sub {
  font-size: 24rpx;
  color: rgba(255, 255, 255, 0.7);
  margin-top: 8rpx;
  display: block;
}

.list-item {
  display: flex;
  align-items: center;
  gap: 20rpx;
  padding: 22rpx 0;
  border-bottom: 1rpx dashed #e4e9f0;
}

.list-item:last-child {
  border-bottom: none;
  padding-bottom: 0;
}

.list-av {
  width: 64rpx;
  height: 64rpx;
  border-radius: 16rpx;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 28rpx;
  font-weight: 700;
  color: #ffffff;
  background: linear-gradient(135deg, #2b5cff, #4f7bff);
}

.list-bd {
  flex: 1;
  min-width: 0;
}

.list-n1 {
  font-size: 28rpx;
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.list-n2 {
  font-size: 22rpx;
  color: #6b7280;
  margin-top: 4rpx;
}

.list-rt {
  flex-shrink: 0;
  margin-left: 12rpx;
}
</style>