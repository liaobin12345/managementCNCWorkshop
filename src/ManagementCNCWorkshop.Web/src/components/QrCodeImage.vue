<template>
  <el-image
    v-if="dataUrl"
    :src="dataUrl"
    :preview-src-list="[dataUrl]"
    preview-teleported
    fit="contain"
    class="qr-thumb"
    :style="{ width: displaySize + 'px', height: displaySize + 'px' }"
    alt="二维码"
  />
  <span v-else class="no-qr">-</span>
</template>

<script setup>
import { computed } from 'vue'
import { drawQrCode } from '../utils/qrcode.js'

const props = defineProps({
  /** 二维码内容 */
  text: { type: String, default: '' },
  /** 表格里的展示尺寸 */
  displaySize: { type: Number, default: 48 },
  /** 生成图片的清晰度（点击放大后依然清晰，便于手机扫码） */
  renderSize: { type: Number, default: 600 },
})

const dataUrl = computed(() => {
  if (!props.text) return ''
  const canvas = document.createElement('canvas')
  canvas.width = props.renderSize
  canvas.height = props.renderSize
  drawQrCode(canvas, props.text, { size: props.renderSize })
  return canvas.toDataURL('image/png')
})
</script>

<style scoped>
.qr-thumb {
  border-radius: 4px;
  border: 1px solid #e4e7ed;
  background: #fff;
  cursor: zoom-in;
}

.no-qr {
  color: #c0c4cc;
}
</style>
