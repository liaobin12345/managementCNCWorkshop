<script>
export default {}
</script>

<style>
/* 全局样式 */
page {
  background-color: #eef2f7;
  color: #0f1f33;
  font-size: 28rpx;
  font-family: -apple-system, BlinkMacSystemFont, 'PingFang SC', 'Microsoft YaHei', sans-serif;
}

/* 通用卡片 */
.card {
  background: #ffffff;
  border-radius: 24rpx;
  box-shadow: 0 4rpx 16rpx rgba(14, 34, 56, 0.06);
  padding: 28rpx;
  margin-bottom: 24rpx;
}

.card-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 30rpx;
  font-weight: 700;
  color: #0f1f33;
  margin-bottom: 20rpx;
}

.card-more {
  font-size: 24rpx;
  color: #38bdf8;
  font-weight: 400;
}

/* 通用列表项 */
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
  width: 72rpx;
  height: 72rpx;
  border-radius: 20rpx;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 30rpx;
  font-weight: 700;
  color: #ffffff;
  background: linear-gradient(135deg, #16334f, #1e4468);
}

.list-av.amber {
  background: linear-gradient(135deg, #f59e0b, #fb923c);
  color: #0e2238;
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
  color: #5c6b82;
  margin-top: 4rpx;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.list-rt {
  text-align: right;
  flex-shrink: 0;
}

.list-r1 {
  font-size: 28rpx;
  font-weight: 700;
}

.list-r2 {
  font-size: 22rpx;
  color: #5c6b82;
  margin-top: 4rpx;
}

/* 通用标签 */
.tag {
  font-size: 22rpx;
  padding: 6rpx 16rpx;
  border-radius: 999rpx;
}

.tag.red {
  background: #fef2f2;
  color: #ef4444;
}

.tag.amber {
  background: #fffbeb;
  color: #b45309;
}

.tag.green {
  background: #f0fdf4;
  color: #15803d;
}

.tag.blue {
  background: #eff6ff;
  color: #1d4ed8;
}

.tag.gray {
  background: #f1f5f9;
  color: #64748b;
}

/* 通用表单 */
.field {
  margin-bottom: 24rpx;
}

.field-label {
  display: block;
  font-size: 24rpx;
  color: #5c6b82;
  margin-bottom: 12rpx;
  font-weight: 600;
}

.field-input {
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

.field-picker {
  width: 100%;
  box-sizing: border-box;
  padding: 22rpx 24rpx;
  border: 1rpx solid #e4e9f0;
  border-radius: 20rpx;
  font-size: 28rpx;
  background: #fbfcfe;
  color: #0f1f33;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.field-picker .ph {
  color: #94a3b8;
}

.field-hint {
  font-size: 22rpx;
  color: #94a3b8;
  margin-top: 8rpx;
}

/* 主按钮 */
.btn {
  width: 100%;
  text-align: center;
  padding: 26rpx 0;
  border: none;
  border-radius: 24rpx;
  font-size: 30rpx;
  font-weight: 700;
  color: #ffffff;
  background: linear-gradient(135deg, #f59e0b, #fb923c);
  box-shadow: 0 12rpx 32rpx rgba(245, 158, 11, 0.35);
  margin-top: 12rpx;
}

.btn::after {
  border: none;
}

.btn.blue {
  background: linear-gradient(135deg, #16334f, #1e4468);
  box-shadow: 0 12rpx 32rpx rgba(30, 68, 104, 0.3);
}

/* Tab 切换 */
.tabs {
  display: flex;
  background: #e8edf4;
  border-radius: 20rpx;
  padding: 6rpx;
  margin-bottom: 24rpx;
}

.tabs .tab {
  flex: 1;
  text-align: center;
  padding: 14rpx 0;
  font-size: 26rpx;
  border-radius: 16rpx;
  color: #5c6b82;
  font-weight: 600;
}

.tabs .tab.active {
  background: #ffffff;
  color: #16334f;
  box-shadow: 0 2rpx 8rpx rgba(14, 34, 56, 0.12);
}

/* 统计卡行 */
.stats-row {
  display: flex;
  gap: 16rpx;
  margin: 24rpx 0;
}

.stat {
  flex: 1;
  background: #ffffff;
  border-radius: 24rpx;
  padding: 24rpx 20rpx;
  text-align: center;
  box-shadow: 0 4rpx 16rpx rgba(14, 34, 56, 0.06);
}

.stat .v {
  font-size: 40rpx;
  font-weight: 800;
  color: #16334f;
}

.stat .v.green {
  color: #22c55e;
}

.stat .v.red {
  color: #ef4444;
}

.stat .v.amber {
  color: #f59e0b;
}

.stat .l {
  font-size: 22rpx;
  color: #5c6b82;
  margin-top: 6rpx;
}

/* 空状态 */
.empty {
  text-align: center;
  color: #94a3b8;
  font-size: 26rpx;
  padding: 60rpx 0;
}
</style>