<template>
  <view class="login-page">
    <view class="brand">
      <view class="logo">CNC</view>
      <view class="title">CNC 车间管理系统</view>
      <view class="sub">工人端 · 扫码报工 / 点检 / 产量 / 质量 / 保养</view>
    </view>

    <view class="content">
      <view class="card">
      <!-- 微信一键登录 -->
      <button class="btn wx-btn" :loading="wxLoading" :disabled="wxLoading" @tap="handleWechatLogin">
        <text class="wx-icon">微信</text> 一键登录
      </button>

      <view class="divider"><text class="divider-text">或使用工号密码登录</text></view>

      <!-- 首次使用：绑定员工账号 -->
      <template v-if="mode === 'bind'">
        <view class="bind-tip">
          <text class="bind-title">首次使用，请绑定员工账号</text>
          <text class="bind-sub">绑定成功后，下次打开小程序将自动登录</text>
        </view>
        <view class="field">
          <text class="label">工号</text>
          <input
            class="input"
            v-model="bindForm.employeeNo"
            placeholder="请输入工号"
            placeholder-class="ph"
            confirm-type="done"
            cursor-spacing="120"
            @confirm="handleBind"
          />
        </view>
        <view class="field">
          <text class="label">密码</text>
          <input
            class="input"
            v-model="bindForm.password"
            password
            placeholder="请输入密码"
            placeholder-class="ph"
            confirm-type="done"
            cursor-spacing="120"
            @confirm="handleBind"
          />
        </view>
        <button class="btn" :loading="loading" @tap="handleBind">绑定并登录</button>
        <view class="back-link" @tap="mode = 'wechat'">‹ 返回</view>
        <view v-if="statusText" class="status-text">{{ statusText }}</view>
      </template>

      <!-- 普通账号密码登录 -->
      <template v-else>
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
      </template>
    </view>
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
import { apiLogin, apiWechatLogin, apiWechatBind } from '../../api'

const mode = ref('wechat') // wechat：密码登录 / bind：首次微信绑定
const form = reactive({ employeeNo: '', password: '' })
const bindForm = reactive({ employeeNo: '', password: '' })
const bindOpenId = ref('')
const loading = ref(false)
const wxLoading = ref(false)
const statusText = ref('')

function saveLogin(res) {
  uni.setStorageSync('token', res.token)
  uni.setStorageSync('employee', res.employee)
  statusText.value = ''
  uni.showToast({ title: `欢迎，${res.employee.name}`, icon: 'success' })
  setTimeout(() => uni.switchTab({ url: '/pages/home/home' }), 600)
}

function handleLogin() {
  if (!form.employeeNo.trim() || !form.password.trim()) {
    statusText.value = '请输入工号和密码'
    uni.showToast({ title: '请输入工号和密码', icon: 'none' })
    return
  }

  statusText.value = '正在登录...'
  loading.value = true
  apiLogin({ employeeNo: form.employeeNo.trim(), password: form.password })
    .then(saveLogin)
    .catch((err) => {
      statusText.value = err?.message || '登录失败，请检查账号密码或后端地址'
    })
    .finally(() => {
      loading.value = false
    })
}

// 微信一键登录：uni.login 拿 code → 后端换 openid
function handleWechatLogin() {
  statusText.value = ''
  wxLoading.value = true
  uni.login({
    provider: 'weixin',
    success: (res) => {
      apiWechatLogin(res.code)
        .then((r) => {
          wxLoading.value = false
          if (r.needsBind) {
            // 首次使用：进入绑定页
            bindOpenId.value = r.openid
            bindForm.employeeNo = ''
            bindForm.password = ''
            mode.value = 'bind'
            statusText.value = ''
            return
          }
          saveLogin(r)
        })
        .catch((err) => {
          wxLoading.value = false
          statusText.value = err?.message || '微信登录失败'
        })
    },
    fail: () => {
      wxLoading.value = false
      statusText.value = '微信登录不可用，请使用工号密码登录'
      uni.showToast({ title: '请在真机或已配置 AppID 的环境使用微信登录', icon: 'none' })
    },
  })
}

// 首次绑定：工号密码校验后绑定微信
function handleBind() {
  if (!bindForm.employeeNo.trim() || !bindForm.password.trim()) {
    statusText.value = '请输入工号和密码'
    uni.showToast({ title: '请输入工号和密码', icon: 'none' })
    return
  }

  statusText.value = '正在绑定...'
  loading.value = true
  apiWechatBind({
    openid: bindOpenId.value,
    employeeNo: bindForm.employeeNo.trim(),
    password: bindForm.password,
  })
    .then((res) => {
      mode.value = 'wechat'
      form.employeeNo = bindForm.employeeNo
      saveLogin(res)
    })
    .catch((err) => {
      statusText.value = err?.message || '绑定失败'
    })
    .finally(() => {
      loading.value = false
    })
}
</script>

<style scoped>
.login-page {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: linear-gradient(160deg, #0e2238 0%, #16334f 60%, #1e4468 100%);
  box-sizing: border-box;
}

.brand {
  padding-top: 80rpx;
  text-align: center;
}

/* 用绝对定位把卡片（含两个按钮）钉在屏幕垂直正中，浏览器/小程序都稳定 */
.content {
  position: absolute;
  top: 50%;
  left: 56rpx;
  right: 56rpx;
  transform: translateY(-50%);
}

.tips {
  position: absolute;
  bottom: 60rpx;
  left: 0;
  right: 0;
  text-align: center;
  font-size: 24rpx;
  color: rgba(255, 255, 255, 0.55);
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
  width: 100%;
  box-sizing: border-box;
  flex-shrink: 0;
}

.tips {
  flex-shrink: 0;
  padding-bottom: 40rpx;
}

.btn {
  width: 100%;
  height: 92rpx;
  border-radius: 24rpx;
  font-size: 30rpx;
  font-weight: 700;
  background: linear-gradient(135deg, #0e2238, #1e4468);
  color: #fff;
  border: none;
  margin: 0;
  padding: 0;
  box-sizing: border-box;
  /* 用 flex 让按钮文字/图标水平和垂直都真正居中（line-height 遇子元素会偏移） */
  display: flex;
  align-items: center;
  justify-content: center;
}

.btn::after {
  border: none;
}

.btn.button-hover {
  opacity: 0.88;
}

.wx-btn {
  background: #07c160;
  box-shadow: 0 12rpx 40rpx rgba(7, 193, 96, 0.35);
}

.wx-icon {
  margin-right: 8rpx;
}

.divider {
  display: flex;
  align-items: center;
  gap: 20rpx;
  margin: 36rpx 0 8rpx;
}

.divider::before,
.divider::after {
  content: '';
  flex: 1;
  height: 1rpx;
  background: #e4e9f0;
}

.divider-text {
  font-size: 22rpx;
  color: #94a3b8;
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

.bind-tip {
  margin-bottom: 28rpx;
  background: #f0fdf4;
  border: 1rpx solid #bbf7d0;
  border-radius: 16rpx;
  padding: 20rpx 24rpx;
}

.bind-title {
  display: block;
  font-size: 26rpx;
  font-weight: 700;
  color: #15803d;
}

.bind-sub {
  display: block;
  font-size: 22rpx;
  color: #4ade80;
  margin-top: 6rpx;
}

.back-link {
  margin-top: 20rpx;
  text-align: center;
  font-size: 24rpx;
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
