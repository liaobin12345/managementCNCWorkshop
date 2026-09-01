// 纯前端二维码编码器（ISO/IEC 18004，byte 字节模式，版本 1-6）
// 零依赖、离线可用。接口：qrcodeMatrix(text) 返回二维布尔矩阵（true=黑），
// QRCodeImage 组件基于它渲染 canvas。
//
// 算法结构参考公共领域 QR 编码实现，已用 qrcode-reader 解码器做过端到端自测。

// 各版本原始码字数（不经 ECC，版本 1~6）
const NUM_RAW_CODE_WORDS = [26, 44, 70, 100, 134, 172]

// 每个版本的纠错码字数，列序：L, M, Q, H（索引 0~3）
const ECC_CODE_WORDS_PER_BLOCK = [
  [7, 10, 13, 17], // v1
  [10, 16, 22, 28], // v2
  [15, 26, 18, 22], // v3
  [20, 18, 26, 16], // v4
  [26, 24, 18, 22], // v5
  [18, 16, 24, 28], // v6
]

// 每个版本的纠错分块数
const NUM_ERROR_CORRECTION_BLOCKS = [
  [1, 1, 1, 1], // v1
  [1, 1, 1, 1], // v2
  [1, 1, 2, 2], // v3
  [1, 2, 2, 4], // v4
  [1, 2, 4, 4], // v5
  [2, 4, 4, 4], // v6
]

// 版本 -> 校正图形中心坐标（v1 无）
const ALIGNMENT_POSITIONS = [
  [],
  [6, 18],
  [6, 22],
  [6, 26],
  [6, 30],
  [6, 34],
]

// ECC 级别对应的格式信息位（L=1, M=0, Q=3, H=2）
const ECL_FORMAT_BITS = [1, 0, 3, 2]

// --- GF(256) 有限域（本原多项式 0x11D） ---
const GF_EXP = new Array(512).fill(0)
const GF_LOG = new Array(256).fill(0)
;(() => {
  let x = 1
  for (let i = 0; i < 255; i++) {
    GF_EXP[i] = x
    GF_LOG[x] = i
    x <<= 1
    if (x & 0x100) x ^= 0x11d
  }
  for (let i = 255; i < 512; i++) GF_EXP[i] = GF_EXP[i - 255]
})()

function gfMul(a, b) {
  if (a === 0 || b === 0) return 0
  return GF_EXP[GF_LOG[a] + GF_LOG[b]]
}

// 生成多项式 g(x) = Π(x - α^i)，返回系数数组（次方从高到低）
function computeGeneratorPoly(degree) {
  let poly = [1]
  for (let i = 0; i < degree; i++) {
    const prev = poly
    poly = new Array(prev.length + 1).fill(0)
    for (let j = 0; j < prev.length; j++) {
      poly[j] ^= prev[j]
      poly[j + 1] ^= gfMul(prev[j], GF_EXP[i])
    }
  }
  return poly // 长度为 degree+1，poly[0] 为最高次系数（=1）
}

// Reed-Solomon：求 data 除以生成多项式后的余数（即纠错码字节）
function reedSolomon(data, degree) {
  const gen = computeGeneratorPoly(degree)
  const result = new Array(degree).fill(0)
  for (const b of data) {
    const factor = b ^ result[0]
    result.shift()
    result.push(0)
    for (let i = 0; i < degree; i++) {
      result[i] ^= gfMul(factor, gen[i + 1])
    }
  }
  return result
}

// 数据码字数（version 1~6）
function getNumDataCodewords(version, ecl) {
  return (
    NUM_RAW_CODE_WORDS[version - 1] -
    ECC_CODE_WORDS_PER_BLOCK[version - 1][ecl] *
      NUM_ERROR_CORRECTION_BLOCKS[version - 1][ecl]
  )
}

function toUtf8Bytes(text) {
  if (typeof TextEncoder !== 'undefined') return Array.from(new TextEncoder().encode(text))
  // 兜底：手写 UTF-8
  const bytes = []
  for (const ch of text) {
    const cp = ch.codePointAt(0)
    if (cp < 0x80) bytes.push(cp)
    else if (cp < 0x800) bytes.push(0xc0 | (cp >> 6), 0x80 | (cp & 0x3f))
    else if (cp < 0x10000)
      bytes.push(0xe0 | (cp >> 12), 0x80 | ((cp >> 6) & 0x3f), 0x80 | (cp & 0x3f))
    else
      bytes.push(
        0xf0 | (cp >> 18),
        0x80 | ((cp >> 12) & 0x3f),
        0x80 | ((cp >> 6) & 0x3f),
        0x80 | (cp & 0x3f)
      )
  }
  return bytes
}

// 计算放入数据区所需的总 bit 数（byte 模式 + 字符计数头，版本 <= 9 时计数占 8 bit）
function dataBitsNeeded(byteLen) {
  return 4 + 8 + 8 * byteLen
}

// 构造数据码字：头 + 数据 + 终止符 + 补齐，凑满 numDataCodewords
function makeDataCodewords(bytes, version, ecl) {
  const numData = getNumDataCodewords(version, ecl)
  let bits = []

  const pushBits = (value, width) => {
    for (let i = width - 1; i >= 0; i--) bits.push((value >>> i) & 1)
  }

  pushBits(0b0100, 4) // byte 模式
  pushBits(bytes.length, 8) // 字符计数字段（版本 1~9）
  for (const b of bytes) pushBits(b, 8)

  const capacity = numData * 8
  // 终止符（最多 4 bit）
  const remaining = capacity - bits.length
  const terminator = Math.min(4, remaining)
  for (let i = 0; i < terminator; i++) bits.push(0)
  // 补零到字节边界
  while (bits.length % 8 !== 0) bits.push(0)
  // 交替补齐字节 0xEC / 0x11
  const pads = [0xec, 0x11]
  let pi = 0
  while (bits.length < capacity) {
    pushBits(pads[pi % 2], 8)
    pi++
  }

  const out = []
  for (let i = 0; i < bits.length; i += 8) {
    let b = 0
    for (let j = 0; j < 8; j++) b = (b << 1) | bits[i + j]
    out.push(b)
  }
  return out
}

// 数据按块交错并在每块末尾追加纠错码
function addEccAndInterleave(dataCodewords, version, ecl) {
  const numBlocks = NUM_ERROR_CORRECTION_BLOCKS[version - 1][ecl]
  const blockEccLen = ECC_CODE_WORDS_PER_BLOCK[version - 1][ecl]

  const shortLen = Math.floor(dataCodewords.length / numBlocks)
  const shortCount = numBlocks - (dataCodewords.length % numBlocks)

  const blocks = []
  let k = 0
  for (let i = 0; i < numBlocks; i++) {
    const datLen = shortLen + (i < shortCount ? 0 : 1)
    const dat = dataCodewords.slice(k, k + datLen)
    k += datLen
    blocks.push({ data: dat, ecc: reedSolomon(dat, blockEccLen) })
  }

  const result = []
  const maxBlockLen = blocks[numBlocks - 1].data.length
  for (let i = 0; i < maxBlockLen; i++) {
    for (let j = 0; j < numBlocks; j++) {
      if (i < blocks[j].data.length) result.push(blocks[j].data[i])
    }
  }
  for (let i = 0; i < blockEccLen; i++) {
    for (let j = 0; j < numBlocks; j++) result.push(blocks[j].ecc[i])
  }
  return result
}

function boolGrid(size) {
  return Array.from({ length: size }, () => new Array(size).fill(false))
}

// 8 种掩码：返回 true 表示该模块需要翻转
function getMaskBit(mask, x, y) {
  switch (mask) {
    case 0: return (x + y) % 2 === 0
    case 1: return y % 2 === 0
    case 2: return x % 3 === 0
    case 3: return (x + y) % 3 === 0
    case 4: return (Math.floor(x / 3) + Math.floor(y / 2)) % 2 === 0
    case 5: return ((x * y) % 2 + (x * y) % 3) === 0
    case 6: return ((x * y) % 2 + (x * y) % 3) % 2 === 0
    case 7: return (((x + y) % 2) + ((x * y) % 3)) % 2 === 0
    default: return false
  }
}

// 编码格式化信息（5 bit 数据 + BCH 纠错(15,5) + 掩码 0x5412），返回 15 bit
function getFormatBits(eclFormat) {
  let rem = eclFormat
  for (let i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >>> 9) * 0x537)
  const bch = (eclFormat << 10) | rem
  return bch ^ 0x5412
}

// 惩罚评分：评估 8 种掩码选择最优。规则简化版本也保证合法，仅影响抗噪能力。
function getPenaltyScore(modules, size) {
  let result = 0
  // 规则1：同行/列的连续同色
  const runPenalty = (row) => {
    let runColor = row[0]
    let runLen = 1
    let score = 0
    for (let i = 1; i < row.length; i++) {
      if (row[i] === runColor) {
        runLen++
      } else {
        if (runLen >= 5) score += 3 + (runLen - 5)
        runColor = row[i]
        runLen = 1
      }
    }
    if (runLen >= 5) score += 3 + (runLen - 5)
    return score
  }
  for (let y = 0; y < size; y++) result += runPenalty(modules[y])
  for (let x = 0; x < size; x++) {
    const col = []
    for (let y = 0; y < size; y++) col.push(modules[y][x])
    result += runPenalty(col)
  }
  // 规则2：2x2 同色块
  for (let y = 0; y < size - 1; y++) {
    for (let x = 0; x < size - 1; x++) {
      const c = modules[y][x]
      if (
        c === modules[y][x + 1] &&
        c === modules[y + 1][x] &&
        c === modules[y + 1][x + 1]
      ) {
        result += 3
      }
    }
  }
  // 规则3：1011101 模式（前后各有 ≥4 个浅色）
  const finderPattern = [true, false, true, true, true, false, true]
  const matchAt = (row, col) => {
    if (col + 6 >= size) return false
    for (let i = 0; i < 7; i++) if (row[col + i] !== finderPattern[i]) return false
    // 前面 >=4 浅
    for (let i = 1; i <= 4; i++) if (col - i >= 0 && row[col - i]) return false
    // 后面 >=4 浅
    for (let i = 7; i <= 10; i++) if (col + i < size && row[col + i]) return false
    return true
  }
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      if (matchAt(modules[y], x)) result += 40
    }
  }
  for (let x = 0; x < size; x++) {
    const col = []
    for (let y = 0; y < size; y++) col.push(modules[y][x])
    for (let y = 0; y < size; y++) {
      if (matchAt(col, y)) result += 40
    }
  }
  // 规则4：暗模块比例偏差
  let dark = 0
  for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) if (modules[y][x]) dark++
  const total = size * size
  const percent = (dark * 100) / total
  const prev = Math.floor(percent / 5) * 5
  const next = prev + 5
  result += Math.min(Math.abs(prev - 50), Math.abs(next - 50)) / 5 * 10
  return result
}

// 选择在给定 ECC 下能容纳数据的最小版本
function pickVersion(bytes, ecl, minVersion, maxVersion) {
  const needed = dataBitsNeeded(bytes.length)
  for (let v = minVersion; v <= maxVersion; v++) {
    if (getNumDataCodewords(v, ecl) * 8 >= needed) return v
  }
  throw new Error('二维码内容过长，超出支持容量')
}

// 核心：构造完整二维码矩阵
function buildMatrix(text, { eclIndex = 1, minVersion = 1, maxVersion = 6 } = {}) {
  const bytes = toUtf8Bytes(text)

  // 决定版本与 ECC 级别：优先最适合，再尽量提升纠错级别（boost）
  let version = pickVersion(bytes, eclIndex, minVersion, maxVersion)
  let ecl = eclIndex
  for (let e = eclIndex + 1; e <= 3; e++) {
    try {
      if (pickVersion(bytes, e, version, version) === version) ecl = e
    } catch {
      break
    }
  }

  const dataCodewords = makeDataCodewords(bytes, version, ecl)
  const allCodewords = addEccAndInterleave(dataCodewords, version, ecl)
  const size = version * 4 + 17

  const modules = boolGrid(size)
  const isFunction = boolGrid(size)
  const inBounds = (x, y) => x >= 0 && x < size && y >= 0 && y < size
  const setFunc = (x, y, dark) => {
    if (!inBounds(x, y)) return
    isFunction[y][x] = true
    modules[y][x] = dark
  }

  // 时序图形
  for (let i = 0; i < size; i++) {
    setFunc(i, 6, i % 2 === 0)
    setFunc(6, i, i % 2 === 0)
  }
  // 定位图形（3 个）
  const drawFinder = (cx, cy) => {
    for (let dy = -4; dy <= 4; dy++) {
      for (let dx = -4; dx <= 4; dx++) {
        const dist = Math.max(Math.abs(dx), Math.abs(dy))
        setFunc(cx + dx, cy + dy, dist !== 2 && dist !== 4)
      }
    }
  }
  drawFinder(3, 3)
  drawFinder(size - 4, 3)
  drawFinder(3, size - 4)
  // 校正图形
  const align = ALIGNMENT_POSITIONS[version - 1]
  const n = align.length
  for (let i = 0; i < n; i++) {
    for (let j = 0; j < n; j++) {
      if (!((i === 0 && j === 0) || (i === 0 && j === n - 1) || (i === n - 1 && j === 0))) {
        const cx = align[j]
        const cy = align[i]
        for (let dy = -2; dy <= 2; dy++) {
          for (let dx = -2; dx <= 2; dx++) {
            setFunc(cx + dx, cy + dy, Math.max(Math.abs(dx), Math.abs(dy)) !== 1)
          }
        }
      }
    }
  }
  // 计算放置格式信息
  const setFormatBits = (mask) => {
    const data = getFormatBits((ECL_FORMAT_BITS[ecl] << 3) | mask)
    const bit = (i) => ((data >>> i) & 1) === 1
    for (let i = 0; i <= 5; i++) setFunc(8, i, bit(i))
    setFunc(8, 7, bit(6))
    setFunc(8, 8, bit(7))
    setFunc(7, 8, bit(8))
    for (let i = 9; i < 15; i++) setFunc(14 - i, 8, bit(i))
    for (let i = 0; i < 8; i++) setFunc(size - 1 - i, 8, bit(i))
    for (let i = 8; i < 15; i++) setFunc(8, size - 15 + i, bit(i))
    setFunc(8, size - 8, true) // 固定暗模块
  }

  // 预留格式信息区（先用掩码 0 占位，末尾重画）
  setFormatBits(0)
  // 数据 zigzag 放置
  let bitIndex = 0
  for (let right = size - 1; right >= 1; right -= 2) {
    if (right === 6) right = 5
    for (let vert = 0; vert < size; vert++) {
      for (let j = 0; j < 2; j++) {
        const x = right - j
        const upward = ((right + 1) & 2) === 0
        const y = upward ? size - 1 - vert : vert
        if (!isFunction[y][x]) {
          if (bitIndex < allCodewords.length * 8) {
            const cw = allCodewords[bitIndex >>> 3]
            modules[y][x] = ((cw >>> (7 - (bitIndex & 7))) & 1) === 1
            bitIndex++
          } else {
            modules[y][x] = false
          }
        }
      }
    }
  }

  // 应用掩码并选最优
  let bestMask = 0
  let bestPenalty = Infinity
  for (let m = 0; m < 8; m++) {
    for (let y = 0; y < size; y++) {
      for (let x = 0; x < size; x++) {
        if (!isFunction[y][x] && getMaskBit(m, x, y)) modules[y][x] = !modules[y][x]
      }
    }
    if (m === 0) {
      bestPenalty = getPenaltyScore(modules, size)
      bestMask = 0
    } else {
      const p = getPenaltyScore(modules, size)
      if (p < bestPenalty) {
        bestPenalty = p
        bestMask = m
      }
    }
    // 还原该掩码，准备下一轮
    for (let y = 0; y < size; y++) {
      for (let x = 0; x < size; x++) {
        if (!isFunction[y][x] && getMaskBit(m, x, y)) modules[y][x] = !modules[y][x]
      }
    }
  }
  // 重放最优掩码
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      if (!isFunction[y][x] && getMaskBit(bestMask, x, y)) modules[y][x] = !modules[y][x]
    }
  }
  setFormatBits(bestMask)

  return { modules, size, version, ecl }
}

/**
 * 生成二维码布尔矩阵。
 * @param {string} text 内容
 * @param {object} options { eclIndex? , minVersion?, maxVersion? }
 * @returns {Array<Array<boolean>>} size×size，true=暗模块
 */
export function qrcodeMatrix(text, options) {
  if (text == null || text === '') return []
  return buildMatrix(String(text), options || {}).modules
}

/**
 * 把二维码绘制到 canvas（整幅，含白边）。
 * @param {HTMLCanvasElement} canvas
 * @param {string} text 内容
 * @param {object} options { size? 像素边长, border? 静区模块数(默认4), ecLevel? 'L'|'M'|'Q'|'H' }
 */
export function drawQrCode(canvas, text, options = {}) {
  const ecLevels = { L: 0, M: 1, Q: 2, H: 3 }
  const ecl = ecLevels[options.ecLevel || 'M']
  const modules = buildMatrix(String(text), { eclIndex: ecl }).modules
  if (modules.length === 0) return

  const size = options.size || Math.max(canvas && canvas.width ? canvas.width : 220, 220)
  const border = options.border || 4
  const count = modules.length + border * 2
  const cell = size / count
  const ctx = canvas.getContext('2d')
  ctx.fillStyle = '#ffffff'
  ctx.fillRect(0, 0, size, size)
  ctx.fillStyle = '#000000'
  for (let y = 0; y < modules.length; y++) {
    for (let x = 0; x < modules.length; x++) {
      if (modules[y][x]) {
        ctx.fillRect(Math.round((x + border) * cell), Math.round((y + border) * cell), Math.ceil(cell), Math.ceil(cell))
      }
    }
  }
}

export default { qrcodeMatrix, drawQrCode }