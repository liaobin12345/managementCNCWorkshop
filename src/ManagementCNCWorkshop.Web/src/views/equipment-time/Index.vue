<template>
  <el-card shadow="hover">
    <div class="toolbar">
      <div class="toolbar-left">
        <el-select v-model="filters.workshopId" placeholder="全部车间" clearable style="width: 150px" @change="loadEquipments">
          <el-option v-for="w in workshops" :key="w.id" :label="w.name" :value="w.id" />
        </el-select>
        <el-select v-model="filters.equipmentId" placeholder="全部设备" clearable style="width: 180px" @change="load">
          <el-option v-for="e in equipments" :key="e.id" :label="`${e.name}（${e.code}）`" :value="e.id" />
        </el-select>
        <el-date-picker v-model="filters.startDate" type="date" placeholder="开始日期" value-format="YYYY-MM-DD" @change="load" />
        <el-date-picker v-model="filters.endDate" type="date" placeholder="结束日期" value-format="YYYY-MM-DD" @change="load" />
      </div>
      <el-button type="primary" @click="openCreate">
        <el-icon><Plus /></el-icon> 新增记录
      </el-button>
    </div>

    <el-table :data="list" stripe v-loading="loading">
      <el-table-column prop="recordDate" label="日期" width="120" />
      <el-table-column prop="equipmentCode" label="设备编码" width="110" />
      <el-table-column prop="equipmentName" label="设备名称" min-width="160" />
      <el-table-column prop="setupHours" label="调试时间" width="100" align="right" />
      <el-table-column prop="runningHours" label="正常开机" width="100" align="right" />
      <el-table-column prop="idleHours" label="待机时间" width="100" align="right" />
      <el-table-column prop="employeeName" label="记录人" width="100" />
      <el-table-column prop="remark" label="备注" min-width="180" />
      <el-table-column label="操作" width="100" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
        </template>
      </el-table-column>
    </el-table>
  </el-card>

  <el-dialog v-model="dialogVisible" :title="editingId ? '编辑设备时间' : '新增设备时间'" width="560px">
    <el-form :model="form" label-width="110px">
      <el-form-item label="设备" required>
        <el-select v-model="form.equipmentId" placeholder="选择设备" style="width: 100%">
          <el-option v-for="e in equipments" :key="e.id" :label="`${e.name}（${e.code}）`" :value="e.id" />
        </el-select>
      </el-form-item>
      <el-form-item label="记录日期">
        <el-date-picker v-model="form.recordDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" />
      </el-form-item>
      <el-form-item label="调试时间（h）">
        <el-input-number v-model="form.setupHours" :min="0" :step="0.5" :precision="1" style="width: 100%" />
      </el-form-item>
      <el-form-item label="正常开机（h）">
        <el-input-number v-model="form.runningHours" :min="0" :step="0.5" :precision="1" style="width: 100%" />
      </el-form-item>
      <el-form-item label="待机时间（h）">
        <el-input-number v-model="form.idleHours" :min="0" :step="0.5" :precision="1" style="width: 100%" />
      </el-form-item>
      <el-form-item label="备注">
        <el-input v-model="form.remark" type="textarea" :rows="3" maxlength="200" show-word-limit />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="dialogVisible = false">取消</el-button>
      <el-button type="primary" :loading="saving" @click="onSubmit">保存</el-button>
    </template>
  </el-dialog>
</template>

<script setup>
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { adminApi } from '../../api'

const list = ref([])
const workshops = ref([])
const equipments = ref([])
const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const editingId = ref(null)
const filters = reactive({ equipmentId: null, workshopId: null, startDate: '', endDate: '' })
const form = reactive({ equipmentId: null, recordDate: new Date().toISOString().slice(0, 10), setupHours: 0, runningHours: 0, idleHours: 0, remark: '' })

async function loadWorkshops() {
  workshops.value = await adminApi.workshops()
}

async function loadEquipments() {
  equipments.value = await adminApi.equipments({ workshopId: filters.workshopId || undefined })
  if (filters.equipmentId && !equipments.value.some((e) => e.id === filters.equipmentId)) filters.equipmentId = null
  await load()
}

async function load() {
  loading.value = true
  try {
    list.value = await adminApi.equipmentTime({
      equipmentId: filters.equipmentId || undefined,
      startDate: filters.startDate || undefined,
      endDate: filters.endDate || undefined,
      page: 1,
      pageSize: 50
    })
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = null
  Object.assign(form, { equipmentId: filters.equipmentId || equipments.value[0]?.id || null, recordDate: new Date().toISOString().slice(0, 10), setupHours: 0, runningHours: 0, idleHours: 0, remark: '' })
  dialogVisible.value = true
}

function openEdit(row) {
  editingId.value = row.id
  Object.assign(form, {
    equipmentId: row.equipmentId,
    recordDate: row.recordDate,
    setupHours: Number(row.setupHours || 0),
    runningHours: Number(row.runningHours || 0),
    idleHours: Number(row.idleHours || 0),
    remark: row.remark || ''
  })
  dialogVisible.value = true
}

async function onSubmit() {
  if (!form.equipmentId) return ElMessage.warning('请选择设备')
  saving.value = true
  try {
    await adminApi.saveEquipmentTime({
      equipmentId: form.equipmentId,
      recordDate: form.recordDate,
      setupHours: form.setupHours,
      runningHours: form.runningHours,
      idleHours: form.idleHours,
      remark: form.remark
    })
    ElMessage.success('保存成功')
    dialogVisible.value = false
    await load()
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  await loadWorkshops()
  await loadEquipments()
})
</script>
