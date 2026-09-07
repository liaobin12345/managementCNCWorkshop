<template>
  <view class="profile-page">
    <view class="user-card">
      <view class="avatar">{{ (user && user.name || '编').slice(0, 1) }}</view>
      <view class="user-info">
        <view class="user-name">{{ (user && user.name) || '编程师傅' }}</view>
        <view class="user-meta">{{ (user && user.employeeNo) || '' }} · {{ (user && user.role) || '' }}</view>
      </view>
    </view>

    <view class="card">
      <view class="menu-item" @click="goMachines">
        <text>机床配置</text>
        <text class="arrow">›</text>
      </view>
      <view class="menu-item" @click="about">
        <text>关于本工具</text>
        <text class="arrow">›</text>
      </view>
    </view>

    <view class="card">
      <view class="sub-card-title">订阅状态</view>
      <view v-if="subscribed" class="sub-row">
        <text class="tag green">已订阅</text>
        <text class="sub-text">识图 {{ visionUsed }}/{{ visionQuota }} 次</text>
      </view>
      <view v-else class="sub-row">
        <text class="tag gray">未开通</text>
        <text class="sub-text">识图与生成需订阅（¥999/年）</text>
      </view>
      <button v-if="!subscribed" class="btn-sm" style="margin-top: 16rpx" @click="subscribe">订阅 ¥999/年</button>
    </view>

    <button class="btn plain logout" @click="logout">退出登录</button>
  </view>
</template>

<script>
export default {
  data() {
    return {
      user: uni.getStorageSync('prog_user') || null,
      subscribed: false,
      visionUsed: 0,
      visionQuota: 0,
    }
  },
  methods: {
    goMachines() {
      uni.navigateTo({ url: '/pages/machines/machines' })
    },
    about() {
      uni.showModal({
        title: '数控车编程助手',
        content: 'v0.1 预览版\n拍照识图 → 轮廓确认 → 自动出程序\n\n下一步将支持：DXF 精确解析、Fanuc 程序生成',
        showCancel: false,
      })
    },
    subscribe() {
      uni.showToast({ title: '微信支付订阅将在后续版本开放', icon: 'none' })
    },
    logout() {
      uni.removeStorageSync('prog_token')
      uni.removeStorageSync('prog_user')
      uni.reLaunch({ url: '/pages/login/login' })
    },
  },
}
</script>

<style scoped>
.user-card {
  display: flex;
  align-items: center;
  gap: 24rpx;
  background: linear-gradient(135deg, #14326b, #2b5cff);
  padding: 48rpx 32rpx;
  color: #ffffff;
}

.avatar {
  width: 100rpx;
  height: 100rpx;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.2);
  border: 2rpx solid rgba(255, 255, 255, 0.4);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 44rpx;
  font-weight: 700;
}

.user-name {
  font-size: 34rpx;
  font-weight: 700;
}

.user-meta {
  font-size: 24rpx;
  color: rgba(255, 255, 255, 0.75);
  margin-top: 8rpx;
}

.card {
  margin-top: 24rpx;
}

.menu-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 28rpx 0;
  border-bottom: 1rpx dashed #e4e9f0;
  font-size: 28rpx;
}

.menu-item:last-child {
  border-bottom: none;
}

.arrow {
  color: #9aa1ad;
  font-size: 32rpx;
}

.sub-card-title {
  font-size: 28rpx;
  font-weight: 700;
  color: #14326b;
  margin-bottom: 20rpx;
}

.sub-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 26rpx;
}

.sub-text {
  color: #6b7280;
}

.logout {
  margin: 40rpx 24rpx 60rpx;
  width: calc(100% - 48rpx);
}
</style>
