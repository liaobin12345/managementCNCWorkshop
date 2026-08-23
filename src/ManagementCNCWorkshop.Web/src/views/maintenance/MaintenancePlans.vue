<template>
  <el-card shadow="hover">
    <div class="toolbar">
      <div class="toolbar-left"></div>
      <el-button type="primary" @click="openCreate">
        <el-icon><Plus /></el-icon> 新增保养计划
      </el-button>
    </div>

    <el-table :data="list" stripe v-loading="loading">
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column prop="equipment.name" label="设备" width="170" />
      <el-table-column prop="planName" label="计划名称" width="160" />
      <el-table-column prop="cycleDays" label="周期(天)" width="90" align="right" />
      <el-table-column label="下次保养" width="120">
        <template #default="{ row }">{{ formatDate(row.nextDueDate) }}</template>
      </el-table-column>
      <el-table-column prop="remindDaysBefore" label="提前提醒(天)" width="120" align="right" />
      <el-table-column prop="content" label="保养内容" min-width="200" show-overflow-tooltip />
    </el-table>

    <el-dialog v-model="dialogVisible" title="新增保养计划" width="500px">
      <el-form :model="form" label-width="100px">
        <el-form-item label="关联设备" required>
          <el-select v-model="form.equipmentId" placeholder="选择设备" style="width: 100%">
            <el-option v-for="e in equipments" :key="e.id" :label="`${e.name}（${e.code}）`" :value="e.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="计划名称" required>
          <el-input v-model="form.planName" placeholder="如 月度润滑保养" />
        </el-form-item>
        <el-form-item label="保养周期(天)" required>
          <el-input-number v-model="form.cycleDays" :min="1" :max="3650" />
        </el-form-item>
        <el-form-item label="下次保养日期" required>
          <el-date-picker v-model="form.nextDueDate" type="date" placeholder="选择日期"
            value-format="YYYY-MM-DD" style="width: 100%" />
        </el-form-item>
        <el-form-item label="提前提醒(天)">
          <el-input-number v-model="form.remindDaysBefore" :min="0" :max="90" />
        </el-form-item>
        <el-form-item label="保养内容">
          <el-input v-model="form.content" type="textarea" :rows="2" placeholder="保养内容说明" />
        </el-form-item>
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
import { ElMessage } from 'element-plus'
import { adminApi } from '../../api'
import { formatDate } from '../../utils/format'

const list = ref([])
const equipments = ref([])
const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)

const form = reactive({
  equipmentId: null,
  planName: '',
  cycleDays: 30,
  nextDueDate: '',
  remindDaysBefore: 3,
  content: ''
})

async function load() {
  loading.value = true
  try {
    list.value = await adminApi.maintenancePlans()
  } finally {
    loading.value = false
  }
}

function openCreate() {
  Object.assign(form, {
    equipmentId: null,
    planName: '',
    cycleDays: 30,
    nextDueDate: '',
    remindDaysBefore: 3,
    content: ''
  })
  dialogVisible.value = true
}

async function onSubmit() {
  if (!form.equipmentId) return ElMessage.warning('请选择关联设备')
  if (!form.planName) return ElMessage.warning('请填写计划名称')
  if (!form.nextDueDate) return ElMessage.warning('请选择下次保养日期')
  saving.value = true
  try {
    await adminApi.createMaintenancePlan({ ...form })
    dialogVisible.value = false
    ElMessage.success('新增成功')
    await load()
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  equipments.value = await adminApi.equipments()
  await load()
})
</script>
