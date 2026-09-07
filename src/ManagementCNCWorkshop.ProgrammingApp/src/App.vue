<script>
export default {}
</script>

<style>
/* 全局样式：数控车编程助手 */
page {
  background-color: #f3f5f9;
  color: #1a2233;
  font-size: 28rpx;
  font-family: -apple-system, BlinkMacSystemFont, 'PingFang SC', 'Microsoft YaHei', sans-serif;
}

.card {
  background: #ffffff;
  border-radius: 24rpx;
  box-shadow: 0 4rpx 16rpx rgba(20, 50, 107, 0.05);
  padding: 28rpx;
  margin: 24rpx;
}

.card-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 30rpx;
  font-weight: 700;
  color: #14326b;
  margin-bottom: 20rpx;
}

.card-more {
  font-size: 24rpx;
  color: #2b5cff;
  font-weight: 400;
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
  background: linear-gradient(135deg, #2b5cff, #4f7bff);
  box-shadow: 0 12rpx 32rpx rgba(43, 92, 255, 0.28);
  margin-top: 12rpx;
}

.btn::after {
  border: none;
}

.btn.plain {
  background: #eef2ff;
  color: #2b5cff;
  box-shadow: none;
}

.btn.gray {
  background: #e5e9f0;
  color: #6b7280;
  box-shadow: none;
}

.btn.disabled {
  background: #c7cbd6;
  color: #ffffff;
  box-shadow: none;
}

/* 小按钮 */
.btn-sm {
  display: inline-block;
  text-align: center;
  padding: 14rpx 30rpx;
  border-radius: 16rpx;
  font-size: 26rpx;
  font-weight: 600;
  color: #ffffff;
  background: #2b5cff;
  border: none;
}

.btn-sm::after {
  border: none;
}

.btn-sm.plain {
  background: #eef2ff;
  color: #2b5cff;
}

.btn-sm.gray {
  background: #e5e9f0;
  color: #6b7280;
}

/* 表单 */
.field {
  margin-bottom: 24rpx;
}

.field-label {
  display: block;
  font-size: 24rpx;
  color: #6b7280;
  margin-bottom: 12rpx;
  font-weight: 600;
}

.field-input {
  width: 100%;
  box-sizing: border-box;
  min-height: 88rpx;
  padding: 0 24rpx;
  border: 1rpx solid #dde2ea;
  border-radius: 20rpx;
  font-size: 28rpx;
  background: #fbfcfe;
  color: #1a2233;
}

.field-picker {
  width: 100%;
  box-sizing: border-box;
  padding: 20rpx 24rpx;
  border: 1rpx solid #dde2ea;
  border-radius: 20rpx;
  font-size: 28rpx;
  background: #fbfcfe;
  color: #1a2233;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.field-picker .ph {
  color: #9aa1ad;
}

.field-hint {
  font-size: 22rpx;
  color: #9aa1ad;
  margin-top: 8rpx;
}

/* 标签 */
.tag {
  display: inline-block;
  font-size: 20rpx;
  padding: 4rpx 16rpx;
  border-radius: 999rpx;
}

.tag.green {
  background: #e9f9ef;
  color: #17a34a;
}

.tag.amber {
  background: #fff6e6;
  color: #b45309;
}

.tag.red {
  background: #fdecec;
  color: #d64545;
}

.tag.blue {
  background: #e8efff;
  color: #2b5cff;
}

.tag.gray {
  background: #f0f2f5;
  color: #6b7280;
}

/* 空状态 */
.empty {
  text-align: center;
  color: #9aa1ad;
  font-size: 26rpx;
  padding: 60rpx 0;
}
</style>
