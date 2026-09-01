// 二维码编码器端到端自测：生成 -> 渲染 RGBA -> 用 qrcode-reader 解码比对
import { createRequire } from 'node:module'
import { qrcodeMatrix } from '../src/ManagementCNCWorkshop.Web/src/utils/qrcode.js'

const require = createRequire(import.meta.url)
const QrCode = require('../src/ManagementCNCWorkshop.MiniApp/node_modules/qrcode-reader/dist/index.js')

function toImageData(modules, scale = 8, border = 4) {
  const n = modules.length
  const size = (n + border * 2) * scale
  const data = new Uint8ClampedArray(size * size * 4)
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const mx = Math.floor(x / scale) - border
      const my = Math.floor(y / scale) - border
      const dark = mx >= 0 && my >= 0 && mx < n && my < n && modules[my][mx]
      const i = (y * size + x) * 4
      const v = dark ? 0 : 255
      data[i] = v
      data[i + 1] = v
      data[i + 2] = v
      data[i + 3] = 255
    }
  }
  return { data, width: size, height: size }
}

function decode(text) {
  return new Promise((resolve, reject) => {
    const qr = new QrCode()
    qr.callback = (err, value) => (err ? reject(err) : resolve(value ? value.result : null))
    qr.decode(toImageData(qrcodeMatrix(text)))
  })
}

const cases = [
  'PROD:P001',
  'PROD:P003',
  'EQ:CNC-01',
  'https://example.com/qr?x=1&y=2',
  '测试中文二维码ABC123',
  'A'.repeat(30),
  'LZ-20260825-001',
]

let pass = 0
for (const c of cases) {
  try {
    const out = await decode(c)
    const ok = out === c
    if (ok) pass++
    console.log(`${ok ? 'PASS' : 'FAIL'}  ${JSON.stringify(c).slice(0, 40)} -> ${JSON.stringify(out).slice(0, 40)}`)
  } catch (e) {
    console.log(`ERR   ${JSON.stringify(c).slice(0, 40)} -> ${e.message || e}`)
  }
}
console.log(`\n${pass}/${cases.length} passed`)
process.exit(pass === cases.length ? 0 : 1)
