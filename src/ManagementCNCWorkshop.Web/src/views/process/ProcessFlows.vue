<template>
  <el-card shadow="hover">
    <div class="toolbar">
      <div class="toolbar-left">
        <el-radio-group v-model="statusFilter" @change="load">
          <el-radio-button value="">全部</el-radio-button>
          <el-radio-button value="Draft">草稿</el-radio-button>
          <el-radio-button value="Active">已发布</el-radio-button>
          <el-radio-button value="Inactive">停用</el-radio-button>
        </el-radio-group>
      </div>
      <div class="toolbar-right">
        <el-button type="primary" @click="openCreate">
          <el-icon><Plus /></el-icon> 设计新工艺
        </el-button>
      </div>
    </div>

    <el-table :data="list" stripe v-loading="loading">
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column prop="code" label="工艺编号" width="150" />
      <el-table-column prop="name" label="工艺名称" min-width="160" />
      <el-table-column label="产品" min-width="150">
        <template #default="{ row }">
          <div>{{ row.productName }}</div>
          <div class="sub">{{ row.productSpec }}</div>
        </template>
      </el-table-column>
      <el-table-column prop="version" label="版本" width="70" align="center" />
      <el-table-column prop="stepCount" label="工序数" width="80" align="center" />
      <el-table-column label="状态" width="100" align="center">
        <template #default="{ row }">
          <el-tag :type="statusType[row.status]" size="small">{{ statusLabel[row.status] }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="creatorName" label="设计工程师" width="100" />
      <el-table-column prop="createdAt" label="创建时间" width="160">
        <template #default="{ row }">{{ fmtTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="230" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openDetail(row)">查看</el-button>
          <el-button v-if="row.status !== 'Active'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button v-if="row.status === 'Draft'" link type="success" @click="activate(row)">发布</el-button>
          <el-button v-if="row.status === 'Active'" link type="warning" @click="deactivate(row)">停用</el-button>
          <el-button v-if="row.status === 'Draft'" link type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 工艺详情抽屉 -->
    <el-drawer v-model="detailVisible" :title="detail?.name || ''" size="620px">
      <template v-if="detail">
        <div class="detail-header">
          <el-tag :type="statusType[detail.status]" size="small">{{ statusLabel[detail.status] }}</el-tag>
          <el-tag size="small" type="info">{{ detail.code }}</el-tag>
          <el-tag size="small">版本 V{{ detail.version }}</el-tag>
          <el-tag size="small" type="success">{{ detail.productName }} {{ detail.productSpec }}</el-tag>
          <div class="detail-desc">{{ detail.description || '暂无工艺说明' }}</div>
          <div class="detail-meta">设计工程师：{{ detail.creatorName || '-' }} · {{ fmtTime(detail.createdAt) }}</div>
        </div>

        <el-steps direction="vertical" :active="detail.stepCount" class="flow-steps">
          <el-step
            v-for="step in detail.steps"
            :key="step.stepNo"
            :title="`工序${step.stepNo} · ${step.name}`"
            :description="stepDesc(step)"
          >
            <template #icon>
              <div class="step-no">{{ step.stepNo }}</div>
            </template>
          </el-step>
        </el-steps>
      </template>
    </el-drawer>

    <!-- 新增/编辑工艺对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="editingId ? '编辑工艺路线' : '设计新工艺路线'"
      width="720px"
      top="6vh"
    >
      <el-form :model="form" label-width="90px">
        <el-row :gutter="12">
          <el-col :span="12">
            <el-form-item label="工艺名称" required>
              <el-input v-model="form.name" placeholder="如 传动轴加工工艺" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="产品" required>
              <el-select v-model="form.productId" filterable placeholder="选择产品" style="width: 100%">
                <el-option
                  v-for="p in products"
                  :key="p.id"
                  :label="`${p.name}（${p.code}）`"
                  :value="p.id"
                />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="12">
          <el-col :span="12">
            <el-form-item label="工艺编号">
              <el-input v-model="form.code" placeholder="留空自动生成，如 GY-P001-01" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="设计工程师">
              <el-select v-model="form.createdById" filterable clearable placeholder="选择工程师" style="width: 100%">
                <el-option
                  v-for="e in engineers"
                  :key="e.id"
                  :label="`${e.name}（${e.employeeNo}）`"
                  :value="e.id"
                />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="工艺说明">
          <el-input v-model="form.description" type="textarea" :rows="2" placeholder="工艺总体要求、材料、注意点等" />
        </el-form-item>

        <el-divider content-position="left">工序列表（按顺序加工）</el-divider>

        <div class="steps-editor">
          <div v-for="(step, i) in form.steps" :key="i" class="step-row">
            <div class="step-no-cell">{{ i + 1 }}</div>
            <el-input v-model="step.name" placeholder="工序名称（如下料/车削/铣削/质检）" class="step-name" />
            <el-input v-model="step.description" placeholder="工艺要求" class="step-desc" />
            <el-select v-model="step.equipmentId" clearable filterable placeholder="设备(可选)" class="step-equip">
              <el-option v-for="e in equipments" :key="e.id" :label="e.name" :value="e.id" />
            </el-select>
            <el-input-number v-model="step.durationMinutes" :min="0" placeholder="工时" class="step-dur" controls-position="right" />
            <el-checkbox v-model="step.requiresInspection" class="step-qc">需质检</el-checkbox>
            <el-button link type="danger" :disabled="form.steps.length <= 1" @click="form.steps.splice(i, 1)">
              <el-icon><Delete /></el-icon>
            </el-button>
          </div>
          <el-button class="add-step" @click="addStep">
            <el-icon><Plus /></el-icon> 添加工序
          </el-button>
        </div>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="onSubmit">保存</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup>
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { adminApi } from '../../api'

const list = ref([])
const products = ref([])
const equipments = ref([])
const engineers = ref([])
const loading = ref(false)
const saving = ref(false)
const statusFilter = ref('')
const dialogVisible = ref(false)
const detailVisible = ref(false)
const editingId = ref(null)
const detail = ref(null)

const statusLabel = { Draft: '草稿', Active: '已发布', Inactive: '停用' }
const statusType = { Draft: 'info', Active: 'success', Inactive: 'warning' }

const emptyStep = () => ({ name: '', description: '', equipmentId: null, durationMinutes: null, requiresInspection: false })
const form = reactive({ name: '', code: '', productId: null, createdById: null, description: '', steps: [] })

function fmtTime(t) {
  if (!t) return '-'
  return new Date(t).toLocaleString('zh-CN', { hour12: false })
}

function stepDesc(step) {
  const parts = []
  if (step.description) parts.push(step.description)
  if (step.equipmentName) parts.push(`设备：${step.equipmentName}`)
  if (step.durationMinutes) parts.push(`标准工时：${step.durationMinutes} 分钟`)
  if (step.requiresInspection) parts.push('完成后需质检')
  return parts.join(' · ')
}

async function load() {
  loading.value = true
  try {
    list.value = await adminApi.processFlows({ status: statusFilter.value || undefined })
  } finally {
    loading.value = false
  }
}

async function loadBase() {
  products.value = await adminApi.products()
  equipments.value = await adminApi.equipments()
  const employees = await adminApi.employees({})
  engineers.value = (employees.items || employees).filter((e) => ['Programmer', 'Admin'].includes(e.role))
}

function openCreate() {
  editingId.value = null
  Object.assign(form, {
    name: '',
    code: '',
    productId: null,
    createdById: null,
    description: '',
    steps: [emptyStep(), emptyStep()]
  })
  dialogVisible.value = true
}

async function openEdit(row) {
  editingId.value = row.id
  const d = await adminApi.processFlowDetail(row.id)
  Object.assign(form, {
    name: d.name,
    code: d.code,
    productId: d.productId,
    createdById: d.createdById,
    description: d.description,
    steps: d.steps.map((s) => ({
      name: s.name,
      description: s.description,
      equipmentId: s.equipmentId,
      durationMinutes: s.durationMinutes,
      requiresInspection: s.requiresInspection
    }))
  })
  dialogVisible.value = true
}

async function openDetail(row) {
  detail.value = await adminApi.processFlowDetail(row.id)
  detailVisible.value = true
}

function addStep() {
  form.steps.push(emptyStep())
}

async function activate(row) {
  try {
    await ElMessageBox.confirm(`发布工艺「${row.name}」？同产品的其他已发布工艺将自动停用。`, '发布工艺', {
      type: 'warning'
    })
  } catch {
    return
  }
  await adminApi.activateProcessFlow(row.id)
  ElMessage.success('工艺已发布')
  await load()
}

async function deactivate(row) {
  try {
    await ElMessageBox.confirm(`确定停用工艺「${row.name}」？`, '停用工艺', { type: 'warning' })
  } catch {
    return
  }
  await adminApi.deactivateProcessFlow(row.id)
  ElMessage.success('工艺已停用')
  await load()
}

async function remove(row) {
  try {
    await ElMessageBox.confirm(`确定删除工艺「${row.name}」？`, '删除工艺', { type: 'error' })
  } catch {
    return
  }
  await adminApi.deleteProcessFlow(row.id)
  ElMessage.success('已删除')
  await load()
}

async function onSubmit() {
  if (!form.name) return ElMessage.warning('请填写工艺名称')
  if (!form.productId) return ElMessage.warning('请选择产品')
  const steps = form.steps
    .map((s, i) => ({ ...s, stepNo: i + 1 }))
    .filter((s) => s.name.trim())
  if (!steps.length) return ElMessage.warning('至少添加一道工序并填写工序名称')

  saving.value = true
  try {
    const payload = {
      name: form.name,
      code: form.code,
      productId: form.productId,
      createdById: form.createdById,
      description: form.description,
      steps
    }
    if (editingId.value) {
      await adminApi.updateProcessFlow(editingId.value, payload)
      ElMessage.success('保存成功')
    } else {
      await adminApi.createProcessFlow(payload)
      ElMessage.success('工艺创建成功')
    }
    dialogVisible.value = false
    await load()
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  await loadBase()
  await load()
})
</script>

<style scoped>
.toolbar {
  display: flex;
  justify-content: space-between;
  margin-bottom: 16px;
}

.sub {
  font-size: 12px;
  color: #909399;
}

.detail-header {
  margin-bottom: 20px;
}

.detail-header .el-tag {
  margin-right: 8px;
  margin-bottom: 8px;
}

.detail-desc {
  margin-top: 12px;
  color: #606266;
  font-size: 13px;
  background: #f5f7fa;
  padding: 10px 12px;
  border-radius: 6px;
}

.detail-meta {
  margin-top: 10px;
  color: #909399;
  font-size: 12px;
}

.flow-steps {
  margin-top: 8px;
}

.step-no {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  background: #409eff;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
}

.steps-editor {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.step-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.step-no-cell {
  width: 26px;
  height: 26px;
  border-radius: 50%;
  background: #ecf5ff;
  color: #409eff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  flex-shrink: 0;
}

.step-name {
  width: 130px;
  flex-shrink: 0;
}

.step-desc {
  flex: 1;
}

.step-equip {
  width: 130px;
  flex-shrink: 0;
}

.step-dur {
  width: 110px;
  flex-shrink: 0;
}

.step-qc {
  flex-shrink: 0;
}

.add-step {
  align-self: flex-start;
}
</style>
