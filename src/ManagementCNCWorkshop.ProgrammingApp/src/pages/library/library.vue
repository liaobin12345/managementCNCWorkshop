<template>
  <view class="lib-page">
    <view class="tabs-wrap">
      <view class="tabs">
        <view class="tab" :class="{ active: mine }" @click="setMine(true)">我的程序</view>
        <view class="tab" :class="{ active: !mine }" @click="setMine(false)">本厂程序</view>
      </view>
    </view>

    <view class="card">
      <view v-if="programs.length === 0" class="empty">暂无程序</view>
      <view v-for="p in programs" :key="p.id" class="list-item" @click="goDetail(p.id)">
        <view class="list-av">{{ p.partName.slice(0, 1) }}</view>
        <view class="list-bd">
          <view class="list-n1">{{ p.partName }}</view>
          <view class="list-n2">{{ p.drawingNo || '无图号' }} · {{ p.updatedAt ? fmtTime(p.updatedAt) : '' }}</view>
        </view>
        <view class="list-rt">
          <text class="tag" :class="statusTag(p.status)">{{ statusLabel(p.status) }}</text>
        </view>
      </view>
    </view>

    <button v-if="mine" class="btn" style="margin: 0 24rpx 40rpx; width: calc(100% - 48rpx);" @click="goNew">
      新建程序
    </button>
  </view>
</template>

<script>
import { apiPrograms } from '../../api/index'

export default {
  data() {
    return { programs: [], mine: true }
  },
  onShow() {
    this.load()
  },
  onPullDownRefresh() {
    this.load().finally(() => uni.stopPullDownRefresh())
  },
  methods: {
    async load() {
      try {
        this.programs = await apiPrograms(this.mine)
      } catch (_) {}
    },
    setMine(v) {
      if (this.mine === v) return
      this.mine = v
      this.load()
    },
    goNew() {
      uni.navigateTo({ url: '/pages/new/new' })
    },
    goDetail(id) {
      uni.navigateTo({ url: '/pages/program/program?id=' + id })
    },
    fmtTime(s) {
      return s ? String(s).slice(0, 16).replace('T', ' ') : ''
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
.lib-page {
  padding-bottom: 40rpx;
}

.tabs-wrap {
  margin: 24rpx;
}

.tabs {
  display: flex;
  background: #e8ecf3;
  border-radius: 20rpx;
  padding: 6rpx;
}

.tabs .tab {
  flex: 1;
  text-align: center;
  padding: 16rpx 0;
  font-size: 26rpx;
  border-radius: 16rpx;
  color: #6b7280;
  font-weight: 600;
}

.tabs .tab.active {
  background: #ffffff;
  color: #2b5cff;
  box-shadow: 0 2rpx 8rpx rgba(43, 92, 255, 0.15);
}

.card {
  margin-top: 0;
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
}
</style>
