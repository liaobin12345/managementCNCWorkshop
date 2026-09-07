<template>
  <view class="machines-page">
    <view v-if="machines.length === 0" class="card empty">暂无机床配置，点击下方添加</view>
    <view class="card" v-for="m in machines" :key="m.id">
      <view class="m-row">
        <view class="m-info">
          <view class="m-name">{{ m.name }}（{{ m.code }}）</view>
          <view class="m-sys">{{ m.controlSystem }}</view>
        </view>
        <text class="tag" :class="m.isActive ? 'green' : 'gray'">{{ m.isActive ? '启用' : '停用' }}</text>
        <text class="m-del" @click="del(m.id)">✕</text>
      </view>
    </view>

    <view class="card add-card">
      <view class="card-title"><text>{{ editing ? '编辑机床' : '添加机床' }}</text></view>
      <view class="field">
        <text class="field-label">编码 *</text>
        <input class="field-input" v-model="form.code" placeholder="如 LATHE-01" />
      </view>
      <view class="field">
        <text class="field-label">名称</text>
        <input class="field-input" v-model="form.name" placeholder="数控车床" />
      </view>
      <view class="field">
        <text class="field-label">数控系统 *</text>
        <input class="field-input" v-model="form.controlSystem" placeholder="如 Fanuc 0i-TF" />
      </view>
      <view class="field">
        <text class="field-label">网络地址（DNC 用）</text>
        <input class="field-input" v-model="form.networkAddress" placeholder="选填" />
      </view>
      <view class="field">
        <label class="checkbox-label">
          <checkbox :checked="form.isActive" @change="form.isActive = !form.isActive" />
          <text>启用</text>
        </label>
      </view>
      <button class="btn" @click="save">保存</button>
    </view>
  </view>
</template>

<script>
import { apiMachines, apiCreateMachine, apiDeleteMachine } from '../../api/index'

export default {
  data() {
    return {
      machines: [],
      editing: false,
      form: { code: '', name: '', controlSystem: '', networkAddress: '', isActive: true },
    }
  },
  onShow() {
    this.load()
  },
  methods: {
    async load() {
      try {
        this.machines = await apiMachines()
      } catch (_) {}
    },
    async save() {
      if (!this.form.code.trim() || !this.form.controlSystem.trim()) {
        return uni.showToast({ title: '编码和系统不能为空', icon: 'none' })
      }
      try {
        await apiCreateMachine(this.form)
        uni.showToast({ title: '已保存', icon: 'success' })
        this.form = { code: '', name: '', controlSystem: '', networkAddress: '', isActive: true }
        this.load()
      } catch (_) {}
    },
    async del(id) {
      try {
        await apiDeleteMachine(id)
        this.load()
      } catch (_) {}
    },
  },
}
</script>

<style scoped>
.machines-page {
  padding-bottom: 40rpx;
}

.m-row {
  display: flex;
  align-items: center;
  gap: 16rpx;
  padding: 16rpx 0;
  border-bottom: 1rpx dashed #e4e9f0;
}

.m-row:last-child {
  border-bottom: none;
}

.m-info {
  flex: 1;
}

.m-name {
  font-size: 28rpx;
  font-weight: 600;
}

.m-sys {
  font-size: 22rpx;
  color: #6b7280;
  margin-top: 4rpx;
}

.m-del {
  color: #d64545;
  font-size: 28rpx;
  padding: 8rpx;
}

.add-card {
  margin-top: 24rpx;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 12rpx;
  font-size: 26rpx;
}
</style>