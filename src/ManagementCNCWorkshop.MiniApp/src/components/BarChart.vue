<template>
  <view class="bar-chart">
    <view v-for="(it, i) in items" :key="i" class="bar-col">
      <view class="bar" :class="{ warn: highlight === i }" :style="{ height: barHeight(it.value) }">
        <text class="bar-val">{{ fmt(it.value) }}</text>
      </view>
      <view class="bar-lbl">{{ it.label }}</view>
    </view>
  </view>
</template>

<script setup>
import { computed } from 'vue'

const props = defineProps({
  items: { type: Array, default: () => [] }, // [{ label, value }]
  highlight: { type: Number, default: -1 },
})

const maxValue = computed(() => Math.max(...props.items.map((i) => Number(i.value || 0)), 1))

function barHeight(value) {
  const pct = Math.max((Number(value || 0) / maxValue.value) * 100, 4)
  return pct + '%'
}

function fmt(n) {
  return Number(n || 0).toLocaleString('zh-CN')
}
</script>

<style scoped>
.bar-chart {
  display: flex;
  align-items: flex-end;
  gap: 12rpx;
  height: 260rpx;
  padding: 10rpx 0 0;
}

.bar-col {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10rpx;
  height: 100%;
  justify-content: flex-end;
}

.bar {
  width: 60%;
  border-radius: 12rpx 12rpx 4rpx 4rpx;
  background: linear-gradient(180deg, #1e4468, #16334f);
  min-height: 16rpx;
  display: flex;
  align-items: flex-start;
  justify-content: center;
}

.bar.warn {
  background: linear-gradient(180deg, #f59e0b, #fb923c);
}

.bar-val {
  font-size: 18rpx;
  color: #ffffff;
  padding-top: 6rpx;
  font-weight: 600;
}

.bar-lbl {
  font-size: 20rpx;
  color: #5c6b82;
}
</style>
