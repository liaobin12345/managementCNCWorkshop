<template>
  <view class="wizard-page">
    <!-- 自定义导航栏 -->
    <view class="nav-bar" :style="{ paddingTop: statusBarHeight + 'px' }">
      <view class="nav-inner">
        <view class="nav-back" @click="goBack">‹</view>
        <text class="nav-title">新建程序</text>
        <view class="nav-back" style="visibility: hidden">‹</view>
      </view>
      <view class="stepper">
        <view v-for="(s, i) in steps" :key="i" class="step" :class="{ active: step >= i + 1 }" @click="jump(i)">
          <view class="step-dot">{{ step > i + 1 ? '✓' : i + 1 }}</view>
          <text class="step-label">{{ s }}</text>
        </view>
      </view>
    </view>

    <view class="body">
      <!-- ═══════ Step 1：拍照/上传图纸 ═══════ -->
      <block v-if="step === 1">
        <view class="card">
          <view class="upload-box" @click="handleImageTap">
            <image v-if="imagePath" :src="imagePath" class="preview" mode="aspectFit" />
            <block v-else>
              <view class="upload-icon">📷</view>
              <view class="upload-text">拍照 / 从相册选图纸</view>
              <view class="upload-hint">图纸上的尺寸标注将自动识别</view>
            </block>
          </view>
          <view class="scan-hint" v-if="imagePath">
            当前已保留上传入口，DXF 自动识别还在接入中；先手动录入坐标也可以继续往下走
          </view>

          <view v-if="imagePath" class="img-actions">
            <button class="btn-sm plain" @click="showImagePreview">放大预览图纸</button>
          </view>

          <view class="dxf-zone" @click="pickDxf">
            <view class="dxf-icon">📄</view>
            <view class="dxf-text">上传 DXF 图纸，自动提取轮廓坐标</view>
            <view class="dxf-hint">支持 .dxf 文件（微信端从聊天记录选文件，H5 直接选文件）</view>
          </view>
          <view class="scan-hint" v-if="dxfMsg">{{ dxfMsg }}</view>

          <view v-if="dxfPreviewImage" class="dxf-drawing" @click="showDxfPreview">
            <view class="dxf-drawing-title">DXF 图纸预览（点击放大）</view>
            <image :src="dxfPreviewImage" class="dxf-drawing-img" mode="aspectFit" />
          </view>

          <!-- DXF 解析后坐标预览（第 2 步才是完整编辑表） -->
          <view v-if="dxfPoints.length > 0" class="dxf-preview">
            <view class="dxf-preview-title">已提取 {{ dxfPoints.length }} 个轮廓点</view>
            <view class="dxf-preview-row head">
              <text>#</text><text>类型</text><text>X 直径</text><text>Z</text><text>备注</text>
            </view>
            <scroll-view scroll-y class="dxf-preview-body">
              <view v-for="(p, i) in dxfPoints" :key="i" class="dxf-preview-row">
                <text>{{ i + 1 }}</text>
                <text>{{ p.type }}</text>
                <text>{{ p.x }}</text>
                <text>{{ p.z }}</text>
                <text class="note">{{ p.note }}</text>
              </view>
            </scroll-view>
            <view class="dxf-preview-tip">点下方「下一步」后在坐标页逐点核对 / 修改</view>
          </view>
        </view>

        <view class="card">
          <view class="field">
            <text class="field-label">零件名称 *</text>
            <input class="field-input" v-model="form.partName" placeholder="如：阶梯轴" />
          </view>
          <view class="field">
            <text class="field-label">图号</text>
            <input class="field-input" v-model="form.drawingNo" placeholder="选填" />
          </view>
          <view class="field">
            <text class="field-label">材料</text>
            <picker :range="materialNames" @change="onMaterialPick">
              <view class="field-picker">{{ form.material || '选择材料（自动配切削参数）' }}<text class="ph">▾</text></view>
            </picker>
            <view class="mat-params" v-if="pickedMaterial">
              <view class="mat-param-row">
                <text>粗车 Vc {{ pickedMaterial.vcRough }} m/min · ap {{ pickedMaterial.apRough }}mm · F {{ pickedMaterial.feedRough }}</text>
              </view>
              <view class="mat-param-row">
                <text>精车 Vc {{ pickedMaterial.vcFinish }} · F {{ pickedMaterial.feedFinish }} · 端面 F {{ pickedMaterial.feedFace }} · 切槽 F {{ pickedMaterial.feedGroove }}</text>
              </view>
              <view class="mat-param-row tip" v-if="pickedMaterial.remark">
                <text>{{ pickedMaterial.remark }}</text>
              </view>
            </view>
          </view>
          <view class="field">
            <text class="field-label">基准</text>
            <picker :range="datumList" :value="datumIdx" @change="(e) => { datumIdx = +e.detail.value; form.datum = datumList[datumIdx] }">
              <view class="field-picker">{{ form.datum }}<text class="ph">▾</text></view>
            </picker>
          </view>
        </view>

        <view class="foot-bar">
          <button class="btn" :class="{ disabled: !form.partName.trim() }" :disabled="!form.partName.trim()" @click="createAndNext">
            下一步：输入轮廓坐标 ›
          </button>
        </view>
      </block>

      <!-- ═══════ Step 2：确认轮廓坐标 ═══════ -->
      <block v-else-if="step === 2">
        <view class="card">
          <view class="coords-tip">
            按加工方向（右→左）输入轮廓坐标点。X 为直径值，Z 为轴向坐标（基准右端面 Z0 时通常 ≤0）。
          </view>
          <view class="coord-header">
            <text class="c type">类型</text>
            <text class="c">X 直径</text>
            <text class="c">Z</text>
            <text class="c note">备注</text>
            <text class="c op"></text>
          </view>
          <view v-for="(p, idx) in points" :key="idx" class="coord-row">
            <picker class="c type" :range="typeList" :value="typeList.indexOf(p.type)" @change="(e) => onType(p, typeList[e.detail.value])">
              <view class="type-text">{{ p.type }}</view>
            </picker>
            <input class="c num" type="digit" v-model="p.x" placeholder="50" />
            <input class="c num" type="digit" v-model="p.z" placeholder="0" />
            <input class="c note" v-model="p.note" placeholder="C2/R3/…" />
            <text class="c op" @click="removePoint(idx)">✕</text>
          </view>
          <view class="add-row">
            <button class="btn-sm plain" @click="addPoint">＋ 添加坐标点</button>
          </view>
        </view>

        <view class="foot-bar">
          <button class="btn gray" style="flex: 1; margin-right: 16rpx" @click="step = 1">上一步</button>
          <button class="btn" style="flex: 2" :class="{ disabled: points.length < 2 }" :disabled="points.length < 2" @click="saveAndConfirm">
            确认并下一步 ›
          </button>
        </view>
      </block>

      <!-- ═══════ Step 3：加工参数 ═══════ -->
      <block v-else-if="step === 3">
        <view class="card">
          <view class="field">
            <text class="field-label">刀架类型</text>
            <view class="seg-row">
              <view class="seg-item" :class="{ active: params.toolPost === 'turret' }" @click="params.toolPost = 'turret'">刀塔机</view>
              <view class="seg-item" :class="{ active: params.toolPost === 'gang' }" @click="params.toolPost = 'gang'">排刀机</view>
            </view>
            <text class="field-tip">刀塔机每次换刀前自动 Z 退 150 安全距离</text>
          </view>
          <view class="field">
            <text class="field-label">目标数控系统</text>
            <picker v-if="postProfiles.length" :range="postProfileNames" :value="postProfileIdx" @change="onPostProfilePick">
              <view class="field-picker">{{ params.controlSystem || '选择数控系统' }}<text class="ph">▾</text></view>
            </picker>
            <input v-else class="field-input" v-model="params.controlSystem" placeholder="如：Fanuc 0i-TF" />
            <view v-if="pickedPostProfile" class="mat-params">
              <view class="mat-param-row"><text>初始化 {{ pickedPostProfile.initBlock }} · 结束 {{ pickedPostProfile.endBlock.replace(/\n/g, ' ') }}</text></view>
              <view class="mat-param-row"><text>刀塔换刀 Z{{ pickedPostProfile.safeZTurret }} · 排刀 Z{{ pickedPostProfile.safeZGang }}</text></view>
            </view>
            <view v-if="postProfileCustom" class="field" style="margin-top: 12rpx">
              <input class="field-input" v-model="params.controlSystem" placeholder="手输系统型号，如 Mitsubishi M70" />
            </view>
          </view>
          <view class="field-row">
            <view class="field half">
              <text class="field-label">棒料直径 *</text>
              <input class="field-input" type="digit" v-model="params.stockDia" placeholder="Φ" />
            </view>
            <view class="field half">
              <text class="field-label">毛坯长度</text>
              <input class="field-input" type="digit" v-model="params.stockLen" placeholder="mm" />
            </view>
          </view>
          <text class="field-tip">棒料直径必填。两次加工调头时仍按此直径做棒料倒角去毛刺，避免刮手</text>
          <view class="field-row">
            <view class="field half">
              <text class="field-label">每刀吃刀量(单边)</text>
              <input class="field-input" type="digit" v-model="params.perCutDepth" placeholder="2.0" />
            </view>
            <view class="field half">
              <text class="field-label">精车余量(单边)</text>
              <input class="field-input" type="digit" v-model="params.roughAllowance" placeholder="0.25" />
            </view>
          </view>
          <view class="field-row">
            <view class="field half">
              <text class="field-label">粗车转速 rpm</text>
              <input class="field-input" type="number" v-model="params.rpm" placeholder="800" />
            </view>
            <view class="field half">
              <text class="field-label">进给 mm/r</text>
              <input class="field-input" type="digit" v-model="params.feed" placeholder="0.2" />
            </view>
          </view>
          <view class="field-row">
            <view class="field half">
              <text class="field-label">刀号</text>
              <input class="field-input" type="number" v-model="params.toolNo" placeholder="01" />
            </view>
            <view class="field half">
              <text class="field-label">刀尖半径 R</text>
              <input class="field-input" type="digit" v-model="params.toolTipR" placeholder="0.4" />
            </view>
          </view>
          <view class="field">
            <text class="field-label">切槽刀宽 mm（有窄槽时必填）</text>
            <input class="field-input" type="digit" v-model="params.grooveWidth" placeholder="如 3.0" />
          </view>
          <view class="field">
            <text class="field-label">未注倒角 C mm（未标注处去毛刺）</text>
            <input class="field-input" type="digit" v-model="params.chamferC" placeholder="0.2" />
          </view>
        </view>

        <view class="foot-bar">
          <button class="btn gray" style="flex: 1; margin-right: 16rpx" @click="step = 2">上一步</button>
          <button class="btn" style="flex: 2" @click="saveParamsAndNext">保存并下一步 ›</button>
        </view>
      </block>

      <!-- ═══════ Step 4：工艺预判 + 生成程序 ═══════ -->
      <block v-else-if="step === 4">
        <view class="card">
          <view class="plan-title">工艺方案预判</view>
          <view v-if="planLoading" class="gen-msg">正在分析轮廓特征…</view>
          <block v-else-if="plan">
            <view class="plan-mode">{{ plan.modeText }}</view>
            <view class="plan-meta">总长 {{ plan.totalLen }} mm ｜ 最大直径 Φ{{ plan.maxDia }} ｜ 长径比 {{ plan.lwrRatio }}</view>
            <view class="plan-sub">推荐工步顺序</view>
            <view v-for="(s, i) in plan.steps" :key="'s' + i" class="plan-step">{{ s }}</view>
            <view class="plan-sub">给编程人员的建议</view>
            <view v-for="(a, i) in plan.advice" :key="'a' + i" class="plan-advice">· {{ a }}</view>
            <view class="plan-confirm-tip">确认以上方案合理后再生成程序；如需调整（换刀 / 分段），先回参数页修改。</view>
          </block>
        </view>

        <view class="card center-card">
          <view class="gen-icon">⚙️</view>
          <view class="gen-title">Fanuc 加工程序生成</view>
          <view class="gen-desc">
            根据已确认的 {{ pointCount }} 个轮廓坐标点<br />
            按上述工步顺序生成程序初稿
          </view>
          <button class="btn" :class="{ disabled: planLoading }" :disabled="planLoading" @click="generate">确认方案并生成程序</button>
          <view v-if="generateMsg" class="gen-msg">{{ generateMsg }}</view>
          <view v-for="(part, idx) in gcodeParts" :key="'g' + idx" class="gcode-box">
            <view class="gcode-head">
              <text class="gcode-title">{{ gcodeParts.length > 1 ? (idx === 0 ? '正面程序（先加工）' : '反面程序（调头）') : '加工程序' }}</text>
              <view class="copy-btn" @click="copyPart(idx)">复制</view>
            </view>
            <scroll-view scroll-y class="gcode-scroll"><text class="gcode-text">{{ part }}</text></scroll-view>
          </view>
        </view>

        <view class="foot-bar">
          <button class="btn gray" style="flex: 1; margin-right: 16rpx" @click="step = 3">上一步</button>
          <button class="btn" style="flex: 2" :disabled="!gcode" :class="{ disabled: !gcode }" @click="step = 5">下一步：导出 ›</button>
        </view>
      </block>

      <!-- ═══════ Step 5：导出 ═══════ -->
      <block v-else>
        <view class="card center-card">
          <view class="gen-icon">📤</view>
          <view class="gen-title">导出 / 传送程序</view>
          <view class="gen-desc">
            程序可复制文本、保存为 .NC 文件<br />
            或通过 U 盘 / 微信发送到指定机床
          </view>

          <view class="export-actions">
            <view v-if="gcodeParts.length > 1" class="export-btn" @click="copyPart(0)">
              <view class="e-icon">📋</view><text>复制正面</text>
            </view>
            <view v-if="gcodeParts.length > 1" class="export-btn" @click="copyPart(1)">
              <view class="e-icon">📋</view><text>复制反面</text>
            </view>
            <view v-if="gcodeParts.length <= 1" class="export-btn" @click="copyPart(0)">
              <view class="e-icon">📋</view><text>复制程序</text>
            </view>
            <view class="export-btn" @click="downloadNc">
              <view class="e-icon">💾</view><text>保存 .NC</text>
            </view>
            <view class="export-btn" @click="shareWechat">
              <view class="e-icon">💬</view><text>微信发送</text>
            </view>
          </view>

          <view class="safety-note">
            ⚠️ 程序为初稿，上机前请核对刀补参数，首件务必单段 / 空运行！
          </view>
        </view>

        <view class="foot-bar">
          <button class="btn gray" style="flex: 1; margin-right: 16rpx" @click="step = 4">上一步</button>
          <button class="btn" style="flex: 2" @click="finish">完成（返回工作台）</button>
        </view>
      </block>
    </view>
  </view>
</template>

<script>
import {
  apiCreateProgram,
  apiUpdateProgram,
  apiSavePoints,
  apiConfirmProgram,
  apiProcessPlan,
  apiGenerateProgram,
  apiParseDxf,
  apiMaterials,
  apiPostProfiles,
} from '../../api/index'

const STEPS = ['图纸', '坐标', '参数', '生成', '导出']
const TYPES = ['step', 'face', 'chamfer', 'arc', 'thread', 'groove', 'bore', 'cutoff']
const DATUMS = ['right_face', 'left_face']

export default {
  data() {
    return {
      steps: STEPS,
      typeList: TYPES,
      datumList: DATUMS,
      statusBarHeight: 20,
      step: 1,
      programId: 0,
      imagePath: '',
      dxfMsg: '',
      dxfPoints: [],
      dxfPreviewImage: '',
      datumIdx: 0,
      materialList: [],
      postProfiles: [],
      postProfileCustom: false,
      form: { partName: '', drawingNo: '', material: '', datum: 'right_face' },
      points: [
        { type: 'face', x: '', z: '0', note: '端面' },
        { type: 'step', x: '', z: '', note: '' },
      ],
      params: {
        toolPost: 'turret',
        controlSystem: 'Fanuc 0i-TF',
        stockDia: '',
        stockLen: '',
        perCutDepth: '',
        roughAllowance: '',
        rpm: '',
        feed: '',
        toolNo: '01',
        toolTipR: '',
        grooveWidth: '',
        chamferC: '',
      },
      gcode: '',
      gcodeParts: [],
      generateMsg: '',
      pointCount: 0,
      plan: null,
      planLoading: false,
      imagePreviewOpen: false,
    }
  },
  onLoad() {
    const info = uni.getSystemInfoSync()
    this.statusBarHeight = info.statusBarHeight || 20
    this.loadMaterials()
    this.loadPostProfiles()
  },
  computed: {
    materialNames() {
      return this.materialList.map((m) => m.name)
    },
    pickedMaterial() {
      return this.materialList.find((m) => m.name === this.form.material) || null
    },
    postProfileNames() {
      return [...this.postProfiles.map((p) => p.displayName), '其他（手输）']
    },
    postProfileIdx() {
      const idx = this.postProfiles.findIndex((p) => p.displayName === this.params.controlSystem)
      return idx >= 0 ? idx : this.postProfiles.length
    },
    pickedPostProfile() {
      return this.postProfiles.find((p) => p.displayName === this.params.controlSystem) || null
    },
  },
  methods: {
    async loadMaterials() {
      try {
        this.materialList = await apiMaterials()
      } catch (e) {
        this.materialList = []
      }
    },
    async loadPostProfiles() {
      try {
        const list = await apiPostProfiles()
        this.postProfiles = Array.isArray(list) ? list : []
      } catch (e) {
        this.postProfiles = []
      }
    },
    onPostProfilePick(e) {
      const idx = +e.detail.value
      if (idx < this.postProfiles.length) {
        this.params.controlSystem = this.postProfiles[idx].displayName
        this.postProfileCustom = false
      } else {
        this.postProfileCustom = true
        this.params.controlSystem = ''
      }
    },
    onMaterialPick(e) {
      this.form.material = this.materialNames[+e.detail.value] || ''
      // 选中材料即把推荐参数填进参数页（所见即所得，用户仍可手改）
      const m = this.pickedMaterial
      if (m) {
        this.params.perCutDepth = String(m.apRough)
        this.params.feed = String(m.feedRough)
      }
    },
    // 进入参数页时：转速/吃刀量为空则按材料和轮廓首段直径自动填推荐值，保证页面所见=程序所得
    autoFillParams() {
      const m = this.pickedMaterial
      if (!m) return
      if (this.params.perCutDepth === '') this.params.perCutDepth = String(m.apRough)
      if (this.params.feed === '') this.params.feed = String(m.feedRough)
      const first = this.points.find((p) => p.x !== '' && !isNaN(parseFloat(p.x)))
      const dia = first ? parseFloat(first.x) : 0
      if (this.params.rpm === '' && dia > 0) {
        this.params.rpm = String(Math.min(3000, Math.round((1000 * m.vcRough) / (3.1416 * dia))))
      }
    },
    goBack() {
      if (this.step > 1) {
        this.step -= 1
      } else {
        uni.navigateBack({ fail: () => uni.switchTab({ url: '/pages/home/home' }) })
      }
    },
    jump(i) {
      // 只允许回退到已完成步骤
      if (i + 1 < this.step) this.step = i + 1
    },
    chooseImage() {
      uni.chooseImage({
        count: 1,
        sizeType: ['compressed'],
        success: (res) => {
          this.imagePath = res.tempFilePaths[0]
          this.imagePreviewOpen = false
        },
      })
    },
    handleImageTap() {
      if (!this.imagePath) return this.chooseImage()
      this.showImagePreview()
    },
    showImagePreview() {
      if (!this.imagePath) return
      this.imagePreviewOpen = true
      uni.previewImage({ urls: [this.imagePath] })
    },
    showDxfPreview() {
      if (!this.dxfPreviewImage) return
      uni.previewImage({ urls: [this.dxfPreviewImage] })
    },
    buildDxfPreviewDataUrl(points) {
      const w = 900
      const h = 620
      const pad = 48
      const xs = points.map((p) => Number(p.z))
      const ys = points.map((p) => Number(p.x))
      const minX = Math.min(...xs)
      const maxX = Math.max(...xs)
      const minY = Math.min(...ys)
      const maxY = Math.max(...ys)
      const rangeX = Math.max(1, maxX - minX)
      const rangeY = Math.max(1, maxY - minY)
      const scale = Math.min((w - pad * 2) / rangeX, (h - pad * 2) / rangeY)
      const mapX = (v) => pad + (v - minX) * scale
      const mapY = (v) => h - pad - (v - minY) * scale
      const poly = points.map((p) => `${mapX(Number(p.z)).toFixed(1)},${mapY(Number(p.x)).toFixed(1)}`).join(' ')
      const labels = points
        .map((p, i) => {
          const x = mapX(Number(p.z)).toFixed(1)
          const y = mapY(Number(p.x)).toFixed(1)
          return `<text x="${x}" y="${y - 8}" font-size="18" fill="#1d4ed8">${i + 1}</text>`
        })
        .join('')
      const svg = `
        <svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">
          <rect width="100%" height="100%" fill="#ffffff"/>
          <rect x="${pad}" y="${pad}" width="${w - pad * 2}" height="${h - pad * 2}" fill="#f8fbff" stroke="#dbe4f0" stroke-width="2" rx="16"/>
          <polyline points="${poly}" fill="none" stroke="#1d4ed8" stroke-width="4" stroke-linejoin="round" stroke-linecap="round"/>
          ${labels}
        </svg>
      `.trim()
      return 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg)
    },
    pickDxf() {
      const parse = async (filePath) => {
        if (!filePath) return
        try {
          const parsed = await apiParseDxf(filePath)
          if (parsed && parsed.success && Array.isArray(parsed.points) && parsed.points.length > 0) {
            this.dxfPoints = parsed.points.map((p) => ({
              type: p.type || 'step',
              x: String(p.x ?? ''),
              z: String(p.z ?? ''),
              note: p.note || '',
            }))
            this.points = this.dxfPoints.map((p) => ({ ...p }))
            this.pointCount = this.points.length
            const warns = Array.isArray(parsed.warnings) && parsed.warnings.length
              ? parsed.warnings.join('；') : ''
            this.dxfMsg = `解析成功 ${this.points.length} 点` + (warns ? `：⚠ ${warns}` : '，请核对坐标后继续')
            this.dxfPreviewImage = this.buildDxfPreviewDataUrl(this.dxfPoints)
            uni.showToast({ title: warns ? '已解析（有告警）' : 'DXF 已解析', icon: 'none' })
          } else {
            this.dxfMsg = (parsed && parsed.message) || 'DXF 解析失败，请确认图纸为单外轮廓'
            this.dxfPreviewImage = ''
            uni.showToast({ title: this.dxfMsg, icon: 'none' })
          }
        } catch (_) {
          this.dxfMsg = '解析失败，请确认后端服务已启动'
          this.dxfPreviewImage = ''
        }
      }
      // #ifdef MP-WEIXIN
      uni.chooseMessageFile({
        count: 1,
        type: 'file',
        extension: ['dxf'],
        success: (res) => parse(res.tempFiles && res.tempFiles[0] && res.tempFiles[0].path),
      })
      // #endif
      // #ifndef MP-WEIXIN
      uni.chooseFile({
        count: 1,
        extension: ['.dxf'],
        success: (res) => parse(res.tempFilePaths && res.tempFilePaths[0]),
      })
      // #endif
    },
    num(v) {
      const n = parseFloat(v)
      return isNaN(n) ? null : n
    },
    toPointInput(p) {
      return {
        seq: 0,
        type: p.type,
        x: this.num(p.x) ?? 0,
        z: this.num(p.z) ?? 0,
        note: p.note,
        verified: false,
      }
    },
    async createAndNext() {
      const body = {
        partName: this.form.partName.trim(),
        drawingNo: this.form.drawingNo,
        material: this.form.material,
        datum: this.form.datum,
        source: this.imagePath ? 'vision' : 'manual',
      }
      try {
        const res = await apiCreateProgram(body)
        this.programId = res.id
        this.step = 2
      } catch (_) {}
    },
    addPoint() {
      this.points.push({ type: 'step', x: '', z: '', note: '' })
    },
    removePoint(idx) {
      if (this.points.length <= 2) {
        uni.showToast({ title: '至少保留 2 个坐标点', icon: 'none' })
        return
      }
      this.points.splice(idx, 1)
    },
    onType(p, type) {
      p.type = type
      if (type === 'chamfer' && !p.note) p.note = 'C1'
      if (type === 'arc' && !p.note) p.note = 'R2'
      if (type === 'thread' && !p.note) p.note = 'M20x1.5'
      if (type === 'face') p.note = '端面'
    },
    validatePoints() {
      for (let i = 0; i < this.points.length; i++) {
        const p = this.points[i]
        if (p.x === '' || p.z === '' || isNaN(parseFloat(p.x)) || isNaN(parseFloat(p.z))) {
          uni.showToast({ title: `第 ${i + 1} 个点 X/Z 未填写完整`, icon: 'none' })
          return false
        }
      }
      return true
    },
    async saveAndConfirm() {
      if (!this.validatePoints()) return
      const list = this.points.map((p, i) => ({ ...this.toPointInput(p), seq: i + 1 }))
      try {
        await apiSavePoints(this.programId, list)
        await apiConfirmProgram(this.programId)
        this.pointCount = list.length
        uni.showToast({ title: '坐标已确认', icon: 'success' })
        this.autoFillParams()
        this.step = 3
      } catch (e) {
        uni.showToast({ title: '确认失败，请先保存坐标', icon: 'none' })
      }
    },
    async saveParamsAndNext() {
      const stockDia = this.num(this.params.stockDia)
      if (stockDia == null || stockDia <= 0) {
        uni.showToast({ title: '请填写棒料直径', icon: 'none' })
        return
      }
      const body = {
        partName: this.form.partName.trim(),
        material: this.form.material.trim(),
        toolPost: this.params.toolPost,
        controlSystem: this.params.controlSystem,
        machineNo: '',
        stockDia,
        stockLen: this.num(this.params.stockLen),
        perCutDepth: this.num(this.params.perCutDepth),
        roughAllowance: this.num(this.params.roughAllowance),
        rpm: this.num(this.params.rpm),
        feed: this.num(this.params.feed),
        toolNo: this.params.toolNo ? parseInt(this.params.toolNo) : null,
        toolTipR: this.num(this.params.toolTipR),
        grooveWidth: this.num(this.params.grooveWidth),
        chamferC: this.num(this.params.chamferC),
      }
      try {
        await apiUpdateProgram(this.programId, body)
        await apiConfirmProgram(this.programId)
        uni.showToast({ title: '参数已保存并确认', icon: 'success' })
        await this.loadPlan()
        this.step = 4
      } catch (e) {
        uni.showToast({ title: '参数保存失败', icon: 'none' })
      }
    },
    async loadPlan() {
      this.planLoading = true
      this.plan = null
      try {
        this.plan = await apiProcessPlan(this.programId)
      } catch (_) {
        this.plan = null
      } finally {
        this.planLoading = false
      }
    },
    async generate() {
      this.generateMsg = ''
      this.gcode = ''
      this.gcodeParts = []
      try {
        await apiConfirmProgram(this.programId)
        const res = await apiGenerateProgram(this.programId)
        const parts = (res.program && Array.isArray(res.program.gcodeParts) && res.program.gcodeParts.length)
          ? res.program.gcodeParts
          : (res.program && res.program.gcode ? [res.program.gcode] : [])
        this.gcodeParts = parts
        this.gcode = parts.join('\n\n')
        this.generateMsg = res.message || (res.generated ? '生成完成' : '生成未完成')
      } catch (e) {
        this.generateMsg = (e && e.message) || '请先确认轮廓坐标再生成程序'
      }
    },
    copyPart(idx) {
      const text = this.gcodeParts[idx]
      if (!text) return uni.showToast({ title: '暂无可复制的程序', icon: 'none' })
      uni.setClipboardData({ data: text })
    },
    downloadNc() {
      uni.showToast({ title: '导出引擎将在后续版本接入', icon: 'none' })
    },
    shareWechat() {
      uni.showToast({ title: '微信发送将在后续版本接入', icon: 'none' })
    },
    finish() {
      uni.showToast({ title: '程序已保存到程序库', icon: 'success' })
      setTimeout(() => uni.switchTab({ url: '/pages/home/home' }), 500)
    },
  },
}
</script>

<style scoped>
.wizard-page {
  min-height: 100vh;
  background: #f3f5f9;
}

.nav-bar {
  background: linear-gradient(135deg, #14326b, #2b5cff);
}

.nav-inner {
  display: flex;
  align-items: center;
  height: 88rpx;
  padding: 0 24rpx;
}

.nav-back {
  width: 88rpx;
  font-size: 48rpx;
  color: #ffffff;
  line-height: 88rpx;
}

.nav-title {
  flex: 1;
  text-align: center;
  color: #ffffff;
  font-size: 32rpx;
  font-weight: 700;
}

.stepper {
  display: flex;
  padding: 0 20rpx 24rpx;
}

.step {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8rpx;
}

.step-dot {
  width: 44rpx;
  height: 44rpx;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.25);
  color: rgba(255, 255, 255, 0.7);
  font-size: 22rpx;
  display: flex;
  align-items: center;
  justify-content: center;
}

.step.active .step-dot {
  background: #ffffff;
  color: #2b5cff;
  font-weight: 800;
}

.step-label {
  font-size: 20rpx;
  color: rgba(255, 255, 255, 0.6);
}

.step.active .step-label {
  color: #ffffff;
  font-weight: 600;
}

.body {
  padding-bottom: 160rpx;
}

.foot-bar {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  display: flex;
  padding: 20rpx 24rpx calc(20rpx + constant(safe-area-inset-bottom));
  padding: 20rpx 24rpx calc(20rpx + env(safe-area-inset-bottom));
  background: #ffffff;
  box-shadow: 0 -4rpx 20rpx rgba(0, 0, 0, 0.06);
}

/* 上传 */
.upload-box {
  width: 100%;
  height: 340rpx;
  border: 2rpx dashed #c7cdd8;
  border-radius: 20rpx;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  background: #fafbfd;
}

.upload-icon {
  font-size: 64rpx;
}

.upload-text {
  font-size: 28rpx;
  color: #14326b;
  font-weight: 600;
  margin-top: 12rpx;
}

.upload-hint {
  font-size: 22rpx;
  color: #9aa1ad;
  margin-top: 6rpx;
}

.preview {
  width: 100%;
  height: 340rpx;
  border-radius: 20rpx;
}

.img-actions {
  display: flex;
  justify-content: center;
  margin-top: 16rpx;
}

.scan-hint {
  font-size: 22rpx;
  color: #b45309;
  background: #fff6e6;
  border-radius: 12rpx;
  padding: 12rpx 16rpx;
  margin-top: 16rpx;
}

/* DXF 上传 */
.dxf-zone {
  margin-top: 20rpx;
  border: 2rpx dashed #2b5cff;
  border-radius: 20rpx;
  background: #f2f5ff;
  padding: 28rpx 24rpx;
  display: flex;
  flex-direction: column;
  align-items: center;
}

.dxf-icon {
  font-size: 56rpx;
}

.dxf-text {
  font-size: 28rpx;
  color: #14326b;
  font-weight: 600;
  margin-top: 10rpx;
}

.dxf-hint {
  font-size: 20rpx;
  color: #9aa1ad;
  margin-top: 6rpx;
  text-align: center;
}

.dxf-drawing {
  margin-top: 20rpx;
  border: 1rpx solid #dbe4f0;
  border-radius: 16rpx;
  background: #ffffff;
  padding: 16rpx;
}

.dxf-drawing-title {
  font-size: 24rpx;
  font-weight: 700;
  color: #14326b;
  margin-bottom: 12rpx;
}

.dxf-drawing-img {
  width: 100%;
  height: 300rpx;
  border-radius: 12rpx;
  background: #f8fbff;
}

.dxf-preview {
  margin-top: 20rpx;
  border: 1rpx solid #e3e7ee;
  border-radius: 16rpx;
  background: #ffffff;
  padding: 16rpx;
}

.dxf-preview-title {
  font-size: 24rpx;
  font-weight: 700;
  color: #14326b;
  margin-bottom: 12rpx;
}

.dxf-preview-row {
  display: flex;
  gap: 8rpx;
  font-size: 20rpx;
  color: #374151;
  padding: 8rpx 0;
  border-bottom: 1rpx solid #f1f3f7;
}

.dxf-preview-row text {
  flex: 0 0 60rpx;
  text-align: center;
}

.dxf-preview-row text:nth-child(2) {
  flex: 0 0 110rpx;
}

.dxf-preview-row text:nth-child(3),
.dxf-preview-row text:nth-child(4) {
  flex: 0 0 120rpx;
}

.dxf-preview-row text.note {
  flex: 1;
  text-align: left;
  color: #9aa1ad;
}

.dxf-preview-row.head {
  color: #9aa1ad;
  font-weight: 600;
}

.dxf-preview-body {
  max-height: 360rpx;
}

.dxf-preview-tip {
  font-size: 20rpx;
  color: #b45309;
  background: #fff6e6;
  border-radius: 10rpx;
  padding: 10rpx 14rpx;
  margin-top: 12rpx;
}

/* 坐标列表 */
.coords-tip {
  font-size: 24rpx;
  color: #6b7280;
  background: #eef2ff;
  border-radius: 12rpx;
  padding: 16rpx;
  margin-bottom: 20rpx;
  line-height: 1.6;
}

.coord-header,
.coord-row {
  display: flex;
  align-items: center;
  gap: 8rpx;
  padding: 12rpx 0;
}

.coord-header {
  color: #9aa1ad;
  font-size: 20rpx;
  border-bottom: 1rpx solid #eef0f4;
}

.c {
  flex: 0 0 128rpx;
  text-align: center;
  font-size: 24rpx;
}

.c.type {
  flex: 0 0 150rpx;
}

.c.note {
  flex: 1;
  text-align: left;
  padding-left: 8rpx;
}

.c.op {
  flex: 0 0 48rpx;
  color: #d64545;
}

.coord-row .num,
.coord-row .note {
  background: #f6f8fb;
  border-radius: 10rpx;
  height: 64rpx;
  padding: 0 12rpx;
  box-sizing: border-box;
}

.type-text {
  background: #eef2ff;
  color: #2b5cff;
  border-radius: 10rpx;
  height: 64rpx;
  line-height: 64rpx;
  font-size: 22rpx;
}

.add-row {
  margin-top: 20rpx;
}

/* step4/5 */
.plan-title {
  font-size: 30rpx;
  font-weight: 800;
  color: #14326b;
  margin-bottom: 12rpx;
}

.plan-mode {
  font-size: 28rpx;
  font-weight: 700;
  color: #2b5cff;
  background: #eef2ff;
  border-radius: 12rpx;
  padding: 14rpx 18rpx;
}

.plan-meta {
  font-size: 22rpx;
  color: #6b7280;
  margin-top: 10rpx;
}

.plan-sub {
  font-size: 24rpx;
  font-weight: 700;
  color: #14326b;
  margin: 18rpx 0 8rpx;
}

.plan-step {
  font-size: 24rpx;
  color: #374151;
  background: #f6f8fb;
  border-radius: 10rpx;
  padding: 10rpx 14rpx;
  margin-bottom: 8rpx;
  line-height: 1.5;
}

.plan-advice {
  font-size: 22rpx;
  color: #b45309;
  line-height: 1.7;
}

.plan-confirm-tip {
  font-size: 22rpx;
  color: #6b7280;
  background: #f6f8fb;
  border-radius: 10rpx;
  padding: 12rpx 14rpx;
  margin-top: 14rpx;
  line-height: 1.6;
}

.center-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 48rpx 28rpx;
}

.gen-icon {
  font-size: 96rpx;
}

.gen-title {
  font-size: 34rpx;
  font-weight: 800;
  color: #14326b;
  margin-top: 16rpx;
}

.gen-desc {
  font-size: 24rpx;
  color: #6b7280;
  text-align: center;
  line-height: 1.8;
  margin: 20rpx 0 32rpx;
}

.gen-msg {
  font-size: 24rpx;
  color: #b45309;
  background: #fff6e6;
  border-radius: 12rpx;
  padding: 12rpx 20rpx;
  margin-top: 20rpx;
  text-align: center;
}

.gcode-box {
  width: 100%;
  margin-top: 24rpx;
  border: 1rpx solid #e3e7ee;
  border-radius: 16rpx;
  background: #0f1c2e;
  overflow: hidden;
}

.gcode-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14rpx 20rpx;
  background: #16263c;
}

.gcode-title {
  color: #cdd7e5;
  font-size: 24rpx;
  font-weight: 600;
}

.copy-btn {
  color: #7ee0a3;
  font-size: 24rpx;
  padding: 4rpx 20rpx;
  border: 1rpx solid #7ee0a3;
  border-radius: 10rpx;
}

.gcode-scroll {
  max-height: 480rpx;
  padding: 20rpx;
  box-sizing: border-box;
}

.gcode-text {
  color: #7ee0a3;
  font-size: 22rpx;
  font-family: Menlo, Consolas, monospace;
  line-height: 1.7;
  white-space: pre-wrap;
  word-break: break-all;
}

/* step5 导出 */
.export-actions {
  width: 100%;
  display: flex;
  gap: 16rpx;
  margin: 16rpx 0 8rpx;
}

.export-btn {
  flex: 1;
  background: #f6f8fb;
  border-radius: 16rpx;
  padding: 24rpx 8rpx;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8rpx;
  font-size: 24rpx;
  color: #14326b;
}

.e-icon {
  font-size: 44rpx;
}

.safety-note {
  width: 100%;
  box-sizing: border-box;
  margin-top: 20rpx;
  font-size: 22rpx;
  color: #b45309;
  background: #fff6e6;
  border-radius: 12rpx;
  padding: 16rpx;
  text-align: center;
  line-height: 1.6;
}

.field-row {
  display: flex;
  gap: 16rpx;
}

.field.half {
  flex: 1;
}

.seg-row {
  display: flex;
  gap: 16rpx;
}

.seg-item {
  flex: 1;
  text-align: center;
  padding: 16rpx 0;
  border: 2rpx solid #d5dbe6;
  border-radius: 10rpx;
  color: #6b7688;
  font-size: 27rpx;
  background: #fafbfd;
}

.seg-item.active {
  border-color: #2b5cff;
  color: #2b5cff;
  background: #eef3ff;
  font-weight: 600;
}

.field-tip {
  display: block;
  margin-top: 10rpx;
  font-size: 22rpx;
  color: #98a2b3;
}

.mat-params {
  margin-top: 12rpx;
  padding: 14rpx 16rpx;
  background: #f2f7ff;
  border-radius: 10rpx;
}

.mat-param-row {
  font-size: 22rpx;
  color: #2b5cff;
  line-height: 1.7;
}

.mat-param-row.tip {
  color: #98a2b3;
}
</style>
