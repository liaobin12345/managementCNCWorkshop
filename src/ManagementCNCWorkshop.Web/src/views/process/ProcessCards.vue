<template>
  <el-card shadow="hover">
    <div class="toolbar">
      <div class="toolbar-left">
        <el-radio-group v-model="statusFilter" @change="load">
          <el-radio-button value="">全部</el-radio-button>
          <el-radio-button value="InProgress">加工中</el-radio-button>
          <el-radio-button value="Completed">已完成</el-radio-button>
        </el-radio-group>
      </div>
      <div class="toolbar-right">
        <el-button type="primary" @click="openCreate">
          <el-icon><Plus /></el-icon> 新建流转卡
        </el-button>
      </div>
    </div>

    <el-table :data="list" stripe v-loading="loading">
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column prop="code" label="流转卡编号" width="170" />
      <el-table-column label="产品" min-width="150">
        <template #default="{ row }">
          <div>{{ row.productName }}</div>
          <div class="sub">{{ row.productSpec }}</div>
        </template>
      </el-table-column>
      <el-table-column prop="flowName" label="工艺路线" min-width="140" />
      <el-table-column label="材料规格" min-width="110">
        <template #default="{ row }">{{ row.materialSpec || '-' }}</template>
      </el-table-column>
      <el-table-column label="表面处理" min-width="100">
        <template #default="{ row }">{{ row.surfaceTreatment || '-' }}</template>
      </el-table-column>
      <el-table-column prop="quantity" label="批次数量" width="90" align="right" />
      <el-table-column label="工序进度" width="130" align="center">
        <template #default="{ row }">
          <el-progress
            :percentage="progress(row)"
            :status="row.status === 'Completed' ? 'success' : ''"
            :stroke-width="10"
            :format="() => `${row.currentStepNo}/${totalSteps(row)}`"
          />
        </template>
      </el-table-column>
      <el-table-column label="状态" width="90" align="center">
        <template #default="{ row }">
          <el-tag :type="row.status === 'Completed' ? 'success' : 'primary'" size="small">
            {{ row.status === 'Completed' ? '已完成' : '加工中' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="creatorName" label="创建人" width="90" />
      <el-table-column label="创建时间" width="160">
        <template #default="{ row }">{{ fmtTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="180" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openDetail(row)">详情/流转</el-button>
          <el-button link type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 流转卡详情抽屉 -->
    <el-drawer v-model="detailVisible" :title="detail?.code || '流转卡'" size="640px">
      <template v-if="detail">
        <div class="card-head">
          <div class="card-title">
            <el-tag :type="detail.status === 'Completed' ? 'success' : 'primary'">
              {{ detail.status === 'Completed' ? '已完成' : '加工中' }}
            </el-tag>
            <span class="product">{{ detail.productName }} {{ detail.productSpec }}</span>
          </div>
          <div class="card-meta">
            工艺：{{ detail.flowName }} · 批次数量：{{ detail.quantity }} · 创建人：{{ detail.creatorName || '-' }} ·
            {{ fmtTime(detail.createdAt) }}
          </div>
          <div class="card-meta" v-if="detail.materialSpec || detail.surfaceTreatment">
            材料规格：{{ detail.materialSpec || '-' }} · 表面处理：{{ detail.surfaceTreatment || '-' }}
          </div>
          <div v-if="detail.remark" class="card-remark">备注：{{ detail.remark }}</div>
        </div>

        <el-timeline class="card-steps">
          <el-timeline-item
            v-for="step in detail.cardSteps"
            :key="step.stepNo"
            :type="stepType(step)"
            :hollow="step.status !== 'Running' && step.status !== 'Completed'"
            :timestamp="stepTime(step)"
            placement="top"
          >
            <div class="step-item" :class="{ current: step.status === 'Running' }">
              <div class="step-line">
                <span class="step-index">工序{{ step.stepNo }}</span>
                <span class="step-name">{{ step.stepName }}</span>
                <el-tag size="small" :type="stepTagType[step.status]">{{ stepStatusLabel[step.status] }}</el-tag>
                <el-tag v-if="step.completedQty > 0" size="small" type="success">
                  已报 {{ step.completedQty }} 件
                </el-tag>
                <el-tag v-if="step.defectQty > 0" size="small" type="danger">不良 {{ step.defectQty }}</el-tag>
              </div>
              <div class="step-op">
                <template v-if="step.workDate || step.machineNo || step.shift">
                  日期 {{ step.workDate || '-' }} · 机台 {{ step.machineNo || '-' }} · 班次 {{ step.shift || '-' }}
                </template>
              </div>
              <div class="step-op">
                <template v-if="step.operatorName || step.inspectorName">
                  操作员：{{ step.operatorName || '-' }} · 检查员：{{ step.inspectorName || '-' }}
                </template>
              </div>
              <div v-if="step.remark" class="step-op">备注：{{ step.remark }}</div>
            </div>
          </el-timeline-item>
        </el-timeline>

        <div class="card-actions">
          <template v-if="detail.status === 'InProgress'">
            <el-button type="primary" :loading="acting" @click="advance">
              {{ detail.currentStepNo === 0 ? '开始第一道工序' : detail.currentStepNo >= detail.cardSteps.length ? '完成最后工序' : `完成工序${detail.currentStepNo}，流转下一步` }}
            </el-button>
            <el-button v-if="detail.currentStepNo > 1" @click="rollback">回退上一工序</el-button>
            <el-button type="success" @click="completeAll">直接完成整张卡</el-button>
          </template>
          <el-button v-else type="success" disabled>流转已完成</el-button>
        </div>
      </template>
    </el-drawer>

    <!-- 新建流转卡对话框 -->
    <el-dialog v-model="createVisible" title="新建工艺流转卡" width="520px">
      <el-form :model="createForm" label-width="90px">
        <el-form-item label="工艺路线" required>
          <el-select v-model="createForm.flowId" filterable placeholder="选择已发布的工艺路线" style="width: 100%">
            <el-option
              v-for="f in activeFlows"
              :key="f.id"
              :label="`${f.productName} · ${f.name}（V${f.version}）`"
              :value="f.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="批次数量" required>
          <el-input-number v-model="createForm.quantity" :min="1" :precision="0" style="width: 100%" />
        </el-form-item>
        <el-form-item label="材料规格">
          <el-input v-model="createForm.materialSpec" placeholder="如 45#钢 Φ60" />
        </el-form-item>
        <el-form-item label="表面处理">
          <el-input v-model="createForm.surfaceTreatment" placeholder="如 发黑/镀锌" />
        </el-form-item>
        <el-form-item label="创建人">
          <el-select v-model="createForm.createdById" filterable clearable placeholder="选择创建人（默认管理员）" style="width: 100%">
            <el-option
              v-for="e in employees"
              :key="e.id"
              :label="`${e.name}（${e.employeeNo}）`"
              :value="e.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="createForm.remark" type="textarea" :rows="2" placeholder="批次说明，如 毛坯批次 202608-3" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="createVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="onCreate">生成流转卡</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup>
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { adminApi } from '../../api'

const list = ref([])
const activeFlows = ref([])
const employees = ref([])
const loading = ref(false)
const saving = ref(false)
const acting = ref(false)
const statusFilter = ref('')
const detailVisible = ref(false)
const createVisible = ref(false)
const detail = ref(null)

const stepStatusLabel = { Pending: '待加工', Running: '加工中', Completed: '已完成' }
const stepTagType = { Pending: 'info', Running: 'warning', Completed: 'success' }

const createForm = reactive({
  flowId: null,
  quantity: 100,
  materialSpec: '',
  surfaceTreatment: '',
  createdById: null,
  remark: ''
})

function fmtTime(t) {
  if (!t) return '-'
  return new Date(t).toLocaleString('zh-CN', { hour12: false })
}

function totalSteps(row) {
  return row.cardSteps?.length || 0
}

function progress(row) {
  const total = totalSteps(row)
  if (!total) return 0
  return Math.round((row.currentStepNo / total) * 100)
}

function stepType(step) {
  if (step.status === 'Completed') return 'success'
  if (step.status === 'Running') return 'warning'
  return 'info'
}

function stepTime(step) {
  const parts = []
  if (step.startedAt) parts.push(`开始 ${fmtTime(step.startedAt)}`)
  if (step.completedAt) parts.push(`完成 ${fmtTime(step.completedAt)}`)
  return parts.join(' | ') || ''
}

async function load() {
  loading.value = true
  try {
    list.value = await adminApi.processCards({ status: statusFilter.value || undefined })
  } finally {
    loading.value = false
  }
}

async function loadBase() {
  const flows = await adminApi.processFlows({ status: 'Active' })
  activeFlows.value = flows
  const emps = await adminApi.employees({})
  employees.value = emps
}

function openCreate() {
  Object.assign(createForm, {
    flowId: null,
    quantity: 100,
    materialSpec: '',
    surfaceTreatment: '',
    createdById: null,
    remark: ''
  })
  createVisible.value = true
}

async function openDetail(row) {
  detail.value = null
  detailVisible.value = true
  detail.value = await adminApi.processCardDetail(row.id)
}

async function onCreate() {
  if (!createForm.flowId) return ElMessage.warning('请选择工艺路线')
  saving.value = true
  try {
    const d = await adminApi.createProcessCard({
      processFlowId: createForm.flowId,
      quantity: createForm.quantity,
      materialSpec: createForm.materialSpec || null,
      surfaceTreatment: createForm.surfaceTreatment || null,
      createdById: createForm.createdById,
      remark: createForm.remark
    })
    ElMessage.success(`流转卡 ${d.code} 已生成`)
    createVisible.value = false
    await load()
  } finally {
    saving.value = false
  }
}

async function advance() {
  acting.value = true
  try {
    await adminApi.advanceProcessCard(detail.value.id, {})
    ElMessage.success('流转成功')
    detail.value = await adminApi.processCardDetail(detail.value.id)
    await load()
  } finally {
    acting.value = false
  }
}

async function rollback() {
  try {
    await ElMessageBox.confirm('确定回退到上一道工序？', '回退工序', { type: 'warning' })
  } catch {
    return
  }
  await adminApi.rollbackProcessCard(detail.value.id)
  ElMessage.success('已回退')
  detail.value = await adminApi.processCardDetail(detail.value.id)
  await load()
}

async function completeAll() {
  try {
    await ElMessageBox.confirm('确定直接完成整张流转卡？所有未完成工序将标记为完成。', '完成流转卡', {
      type: 'warning'
    })
  } catch {
    return
  }
  await adminApi.completeProcessCard(detail.value.id, {})
  ElMessage.success('流转卡已完成')
  detail.value = await adminApi.processCardDetail(detail.value.id)
  await load()
}

async function remove(row) {
  try {
    await ElMessageBox.confirm(`确定删除流转卡「${row.code}」？`, '删除流转卡', { type: 'error' })
  } catch {
    return
  }
  await adminApi.deleteProcessCard(row.id)
  ElMessage.success('已删除')
  await load()
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

.card-head {
  margin-bottom: 20px;
}

.card-title {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 15px;
  font-weight: 600;
}

.card-meta {
  margin-top: 10px;
  color: #909399;
  font-size: 13px;
}

.card-remark {
  margin-top: 8px;
  color: #606266;
  font-size: 13px;
  background: #f5f7fa;
  padding: 8px 12px;
  border-radius: 6px;
}

.step-item {
  padding: 4px 0;
}

.step-item.current .step-name {
  font-weight: 700;
  color: #e6a23c;
}

.step-line {
  display: flex;
  align-items: center;
  gap: 10px;
}

.step-index {
  color: #909399;
  font-size: 12px;
  flex-shrink: 0;
}

.step-name {
  font-size: 14px;
}

.step-op {
  margin-top: 4px;
  color: #909399;
  font-size: 12px;
}

.card-actions {
  margin-top: 24px;
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}
</style>
