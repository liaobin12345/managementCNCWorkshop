<template>
  <view class="page">
    <view class="tabs">
      <view class="tab" :class="{ active: sub === 'workshops' }" @click="switchSub('workshops')">车间</view>
      <view class="tab" :class="{ active: sub === 'products' }" @click="switchSub('products')">产品</view>
      <view class="tab" :class="{ active: sub === 'equipments' }" @click="switchSub('equipments')">设备</view>
    </view>

    <!-- 车间 -->
    <view v-if="sub === 'workshops'">
      <view class="card">
        <view class="card-title">车间列表</view>
        <view class="list-item" v-for="(w, i) in workshops" :key="i">
          <view class="list-av amber">{{ (w.code || 'WS').slice(0, 2) }}</view>
          <view class="list-bd">
            <view class="list-n1">{{ w.name }}</view>
            <view class="list-n2">编码 {{ w.code }}</view>
          </view>
        </view>
        <view v-if="!workshops.length" class="empty">暂无数据</view>
      </view>
    </view>

    <!-- 产品 -->
    <view v-if="sub === 'products'">
      <view class="card">
        <view class="card-title">产品列表</view>
        <view class="list-item" v-for="(p, i) in products" :key="i">
          <view class="list-av amber">P</view>
          <view class="list-bd">
            <view class="list-n1">{{ p.name }}</view>
            <view class="list-n2">{{ p.code }} · {{ p.specification || '-' }} · {{ p.qrCode || '-' }}</view>
          </view>
          <view class="list-rt" v-if="p.imageUrl">
            <image class="list-thumb" :src="fullUrl(p.imageUrl)" mode="aspectFill" />
          </view>
        </view>
        <view v-if="!products.length" class="empty">暂无数据</view>
      </view>
    </view>

    <!-- 设备 -->
    <view v-if="sub === 'equipments'">
      <view class="card">
        <view class="card-title">设备列表</view>
        <view class="list-item" v-for="(e, i) in equipments" :key="i">
          <view class="list-av">E</view>
          <view class="list-bd">
            <view class="list-n1">{{ e.name }}</view>
            <view class="list-n2">{{ e.code }} · {{ e.qrCode || '-' }}</view>
          </view>
          <view class="list-rt"><text class="tag" :class="equipStatusTag(e.status)">{{ equipStatus(e.status) }}</text></view>
        </view>
        <view v-if="!equipments.length" class="empty">暂无数据</view>
      </view>
    </view>
  </view>
</template>

<script setup>
import { ref } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { requireAuth, equipStatus, equipStatusTag } from '../../utils/format'
import { apiWorkshops, apiProducts, apiEquipments } from '../../api'
import { BASE_URL } from '../../config'

const sub = ref('workshops')
const workshops = ref([])
const products = ref([])
const equipments = ref([])

onShow(() => {
  if (!requireAuth()) return
  loadData()
})

function switchSub(s) {
  sub.value = s
  loadData()
}

function loadData() {
  apiWorkshops().then((r) => (workshops.value = r || [])).catch(() => {})
  apiProducts().then((r) => (products.value = r || [])).catch(() => {})
  apiEquipments().then((r) => (equipments.value = r || [])).catch(() => {})
}

function fullUrl(url) {
  if (!url) return ''
  if (url.startsWith('http')) return url
  return BASE_URL.replace('/api', '') + url
}
</script>

<style scoped>
.page {
  padding: 24rpx;
}

.list-thumb {
  width: 80rpx;
  height: 80rpx;
  border-radius: 12rpx;
  background: #f1f5f9;
}
</style>