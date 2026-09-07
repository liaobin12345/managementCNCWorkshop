<template>
  <view class="detail-page">
    <view class="card" v-if="program">
      <view class="detail-head">
        <view>
          <view class="d-name">{{ program.partName }}</view>
          <view class="d-sub">{{ program.drawingNo || '无图号' }} · {{ program.material || '未填材料' }}</view>
        </view>
        <text class="tag" :class="statusTag(program.status)">{{ statusLabel(program.status) }}</text>
      </view>
    </view>

    <view class="card">
      <view class="card-title"><text>轮廓坐标</text><text class="card-more">{{ program.pointCount }} 个点</text></view>
      <view v-if="!program || program.pointCount === 0" class="empty">还没有坐标点</view>
      <view v-for="p in (program ? program.contourPoints : [])" :key="p.id" class="pt-row">
        <text class="tag blue">{{ p.type }}</text>
        <text class="pt-val">X {{ p.x }}</text>
        <text class="pt-val">Z {{ p.z }}</text>
        <text class="pt-note">{{ p.note || '' }}</text>
        <text v-if="p.verified" class="tag green">✓</text>
        <text v-else class="tag gray">未确认</text>
      </view>
    </view>

    <view class="card">
      <view class="card-title"><text>加工参数</text></view>
      <view class="kv-grid">
        <view class="kv" v-for="item in kvItems" :key="item.k">
          <text class="k">{{ item.k }}</text>
          <text class="v">{{ item.v }}</text>
        </view>
      </view>
    </view>

    <view class="card" v-if="program && program.gcode">
      <view class="card-title"><text>生成的程序</text></view>
      <scroll-view scroll-y class="gcode-box"><text class="gcode-text">{{ program.gcode }}</text></scroll-view>
    </view>

    <view class="actions">
      <button class="btn plain" @click="deleteProgram">删除</button>
      <button v-if="program" class="btn" style="flex: 2" @click="editProgram">编辑 / 继续</button>
    </view>
  </view>
</template>

<script>
import { apiProgramDetail, apiDeleteProgram } from '../../api/index'

export default {
  data() {
    return { id: 0, program: null }
  },
  onLoad(query) {
    this.id = Number(query.id || 0)
    this.load()
  },
  computed: {
    kvItems() {
      const p = this.program || {}
      return [
        { k: '数控系统', v: p.controlSystem || '—' },
        { k: '棒料直径', v: p.stockDia ? 'Φ' + p.stockDia : '—' },
        { k: '毛坯长度', v: p.stockLen ? p.stockLen + ' mm' : '—' },
        { k: '每刀吃刀量', v: p.perCutDepth ? p.perCutDepth + ' mm' : '—' },
        { k: '精车余量', v: p.roughAllowance ? p.roughAllowance + ' mm' : '—' },
        { k: '转速', v: p.rpm ? p.rpm + ' rpm' : '—' },
        { k: '进给', v: p.feed ? p.feed + ' mm/r' : '—' },
        { k: '刀号', v: p.toolNo || '—' },
      ]
    },
  },
  methods: {
    async load() {
      try {
        this.program = await apiProgramDetail(this.id)
      } catch (_) {
        setTimeout(() => uni.navigateBack(), 600)
      }
    },
    editProgram() {
      uni.navigateTo({ url: '/pages/program/program?id=' + this.id })
    },
    deleteProgram() {
      uni.showModal({
        title: '删除程序',
        content: '确认删除该程序？不可恢复',
        success: async (r) => {
          if (r.confirm) {
            try {
              await apiDeleteProgram(this.id)
              uni.showToast({ title: '已删除', icon: 'success' })
              setTimeout(() => uni.navigateBack(), 400)
            } catch (_) {}
          }
        },
      })
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
.detail-page {
  padding-bottom: 160rpx;
}

.detail-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.d-name {
  font-size: 34rpx;
  font-weight: 800;
  color: #14326b;
}

.d-sub {
  font-size: 24rpx;
  color: #6b7280;
  margin-top: 8rpx;
}

.pt-row {
  display: flex;
  align-items: center;
  gap: 16rpx;
  padding: 18rpx 0;
  border-bottom: 1rpx dashed #e4e9f0;
}

.pt-row:last-child {
  border-bottom: none;
}

.pt-val {
  font-size: 26rpx;
  font-weight: 600;
}

.pt-note {
  flex: 1;
  font-size: 24rpx;
  color: #6b7280;
  text-align: right;
}

.kv-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20rpx;
}

.kv {
  background: #f6f8fb;
  border-radius: 14rpx;
  padding: 16rpx 20rpx;
}

.kv .k {
  display: block;
  font-size: 20rpx;
  color: #9aa1ad;
}

.kv .v {
  display: block;
  font-size: 28rpx;
  font-weight: 700;
  color: #14326b;
  margin-top: 6rpx;
}

.gcode-box {
  max-height: 480rpx;
  padding: 20rpx;
  box-sizing: border-box;
  background: #0f1c2e;
  border-radius: 14rpx;
}

.gcode-text {
  color: #7ee0a3;
  font-size: 22rpx;
  font-family: Menlo, Consolas, monospace;
  line-height: 1.7;
  white-space: pre-wrap;
  word-break: break-all;
}

.actions {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  display: flex;
  gap: 16rpx;
  padding: 20rpx 24rpx calc(20rpx + constant(safe-area-inset-bottom));
  padding: 20rpx 24rpx calc(20rpx + env(safe-area-inset-bottom));
  background: #ffffff;
  box-shadow: 0 -4rpx 20rpx rgba(0, 0, 0, 0.06);
}

.actions .btn {
  flex: 1;
}
</style>
