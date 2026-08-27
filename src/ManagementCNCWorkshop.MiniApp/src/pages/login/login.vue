<template>
  <view class="login-page">
    <view class="brand">
      <view class="logo">CNC</view>
      <view class="title">CNC 车间管理系统</view>
      <view class="sub">工人端 · 扫码报工 / 产量 / 质量 / 保养</view>
    </view>

    <view class="card">
      <view class="field">
        <text class="label">工号</text>
        <input
          class="input"
          v-model="form.employeeNo"
          placeholder="请输入工号"
          placeholder-class="ph"
          confirm-type="done"
          :adjust-position="true"
          cursor-spacing="120"
          @confirm="handleLogin"
        />
      </view>

      <view class="field">
        <text class="label">密码</text>
        <input
          class="input"
          v-model="form.password"
          password
          placeholder="请输入密码"
          placeholder-class="ph"
          confirm-type="done"
          :adjust-position="true"
          cursor-spacing="120"
          @confirm="handleLogin"
        />
      </view>

      <button class="btn" :loading="loading" @tap="handleLogin">登 录</button>
      <view v-if="statusText" class="status-text">{{ statusText }}</view>
    </view>

    <view class="tips">
      <view class="tips-title">演示账号</view>
      <view class="tips-row"><text class="role">操作工</text><text>E001 / 123456</text></view>
      <view class="tips-row"><text class="role">编程技术员</text><text>E004 / 123456</text></view>
      <view class="tips-row"><text class="role">质检员</text><text>E003 / 123456</text></view>
      <view class="tips-row"><text class="role">管理员</text><text>E900 / admin123</text></view>
    </view>
  </view>
</template>

<script setup>
import { reactive, ref } from 'vue'
import { apiLogin } from '../../api'

const form = reactive({ employeeNo: '', password: '' })
const loading = ref(false)
const statusText = ref('')

function handleLogin() {
  if (!form.employeeNo.trim() || !form.password.trim()) {
    statusText.value = '请输入工号和密码'
    uni.showToast({ title: '请输入工号和密码', icon: 'none' })
    return
  }

  statusText.value = '正在登录...'
  loading.value = true
  apiLogin({ employeeNo: form.employeeNo.trim(), password: form.password })
    .then((res) => {
      uni.setStorageSync('token', res.token)
      uni.setStorageSync('employee', res.employee)
      statusText.value = ''
      uni.showToast({ title: `欢迎，${res.employee.name}`, icon: 'success' })
      setTimeout(() => uni.switchTab({ url: '/pages/home/home' }), 600)
    })
    .catch((err) => {
      statusText.value = err?.message || '登录失败，请检查账号密码或后端地址'
    })
    .finally(() => {
      loading.value = false
    })
}
</script>

<style scoped>
.login-page {
  min-height: 100vh;
  background: linear-gradient(160deg, #0e2238 0%, #16334f 60%, #1e4468 100%);
  padding: 0 56rpx;
  box-sizing: border-box;
}

.brand {
  padding-top: 220rpx;
  text-align: center;
  margin-bottom: 80rpx;
}

.logo {
  width: 140rpx;
  height: 140rpx;
  margin: 0 auto 36rpx;
  border-radius: 36rpx;
  background: linear-gradient(135deg, #f59e0b, #fb923c);
  color: #0e2238;
  font-size: 64rpx;
  font-weight: 800;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 20rpx 60rpx rgba(245, 158, 11, 0.4);
}

.title {
  color: #ffffff;
  font-size: 44rpx;
  font-weight: 800;
  letter-spacing: 2rpx;
}

.sub {
  color: rgba(255, 255, 255, 0.6);
  font-size: 26rpx;
  margin-top: 16rpx;
}

.card {
  background: #ffffff;
  border-radius: 28rpx;
  padding: 44rpx 36rpx;
  box-shadow: 0 24rpx 80rpx rgba(14, 34, 56, 0.35);
}

.field {
  margin-bottom: 26rpx;
}

.label {
  display: block;
  font-size: 24rpx;
  color: #5c6b82;
  margin-bottom: 12rpx;
  font-weight: 600;
}

.input {
  width: 100%;
  box-sizing: border-box;
  min-height: 92rpx;
  padding: 0 24rpx;
  border: 1rpx solid #e4e9f0;
  border-radius: 20rpx;
  font-size: 28rpx;
  background: #fbfcfe;
  color: #0f1f33;
}

.input:focus {
  border-color: #f59e0b;
  box-shadow: 0 0 0 6rpx rgba(245, 158, 11, 0.12);
}

.ph {
  color: #94a3b8;
}

.status-text {
  margin-top: 18rpx;
  font-size: 24rpx;
  color: #ef4444;
  text-align: center;
}

.tips {
  margin-top: 48rpx;
  text-align: center;
  color: rgba(255, 255, 255, 0.55);
  font-size: 24rpx;
}

.tips-title {
  font-size: 26rpx;
  color: #f59e0b;
  font-weight: 700;
  margin-bottom: 16rpx;
}

.tips-row {
  margin-top: 10rpx;
}

.tips-row .role {
  color: #ffffff;
  margin-right: 20rpx;
  font-weight: 600;
}
</style>