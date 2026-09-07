<template>
  <view class="login-page">
    <view class="brand">
      <view class="brand-logo">NC</view>
      <view class="brand-title">数控车编程助手</view>
      <view class="brand-sub">拍照识图 · 轮廓确认 · 自动出程序</view>
    </view>

    <view class="card login-card">
      <view class="field">
        <text class="field-label">工号</text>
        <input class="field-input" v-model="employeeNo" placeholder="请输入工号（如 E004）" />
      </view>
      <view class="field">
        <text class="field-label">密码</text>
        <input class="field-input" password v-model="password" placeholder="请输入密码" />
      </view>

      <button class="btn" :class="{ disabled: loading }" :disabled="loading" @click="doLogin">
        {{ loading ? '登录中…' : '登录' }}
      </button>

      <view class="divider"><text>或</text></view>
      <button class="btn plain" @click="wechatLogin">微信一键登录</button>
      <view class="login-hint">未开通账号请联系管理员 / 在 Web 后台添加员工</view>
    </view>
  </view>
</template>

<script>
import { apiLogin } from '../../api/index'

export default {
  data() {
    return {
      employeeNo: '',
      password: '',
      loading: false,
    }
  },
  onLoad() {
    if (uni.getStorageSync('prog_token')) uni.reLaunch({ url: '/pages/home/home' })
  },
  methods: {
    async doLogin() {
      if (!this.employeeNo.trim() || !this.password) {
        return uni.showToast({ title: '请输入工号和密码', icon: 'none' })
      }
      this.loading = true
      try {
        const res = await apiLogin({ employeeNo: this.employeeNo.trim(), password: this.password })
        uni.setStorageSync('prog_token', res.token)
        uni.setStorageSync('prog_user', res.user || res)
        uni.showToast({ title: '登录成功', icon: 'success' })
        setTimeout(() => uni.switchTab({ url: '/pages/home/home' }), 400)
      } catch (e) {
        /* request.js 已提示错误 */
      } finally {
        this.loading = false
      }
    },
    wechatLogin() {
      uni.showToast({ title: '微信登录待接入正式 AppID（暂用工号登录）', icon: 'none' })
    },
  },
}
</script>

<style scoped>
.login-page {
  min-height: 100vh;
  background: linear-gradient(160deg, #14326b 0%, #2b5cff 70%, #4f7bff 100%);
  padding: 40rpx;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  justify-content: center;
}

.brand {
  text-align: center;
  margin-bottom: 60rpx;
  color: #ffffff;
}

.brand-logo {
  width: 140rpx;
  height: 140rpx;
  line-height: 140rpx;
  border-radius: 40rpx;
  background: rgba(255, 255, 255, 0.18);
  border: 2rpx solid rgba(255, 255, 255, 0.4);
  font-size: 64rpx;
  font-weight: 800;
  margin: 0 auto 24rpx;
}

.brand-title {
  font-size: 42rpx;
  font-weight: 800;
  letter-spacing: 2rpx;
}

.brand-sub {
  font-size: 24rpx;
  color: rgba(255, 255, 255, 0.75);
  margin-top: 12rpx;
}

.login-card {
  margin: 0;
  border-radius: 32rpx;
  padding: 40rpx;
}

.divider {
  display: flex;
  align-items: center;
  gap: 20rpx;
  color: #9aa1ad;
  font-size: 24rpx;
  margin: 28rpx 0;
}

.divider::before,
.divider::after {
  content: '';
  flex: 1;
  height: 1rpx;
  background: #e3e7ee;
}

.login-hint {
  text-align: center;
  font-size: 22rpx;
  color: #9aa1ad;
  margin-top: 24rpx;
}
</style>
