<template>
  <el-card shadow="hover">
    <div class="toolbar">
      <div class="toolbar-left">
        <el-select v-model="filterWorkshopId" placeholder="全部车间" clearable style="width: 160px" @change="load">
          <el-option v-for="w in workshops" :key="w.id" :label="w.name" :value="w.id" />
        </el-select>
        <el-select v-model="filterRole" placeholder="全部角色" clearable style="width: 130px" @change="load">
          <el-option v-for="(label, value) in roleLabel" :key="value" :label="label" :value="value" />
        </el-select>
      </div>
      <el-button type="primary" @click="openCreate">
        <el-icon><Plus /></el-icon> 新增员工
      </el-button>
    </div>

    <el-table :data="list" stripe v-loading="loading">
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column prop="employeeNo" label="工号" width="100" />
      <el-table-column prop="name" label="姓名" width="110" />
      <el-table-column prop="phone" label="手机号" width="130" />
      <el-table-column label="角色" width="100">
        <template #default="{ row }">
          <el-tag :type="roleType[row.role] || 'info'" size="small">{{ roleLabel[row.role] || row.role }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="workshop.name" label="所属车间" />
      <el-table-column label="操作" width="140" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button link type="danger" @click="onDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑员工' : '新增员工'" width="480px">
      <el-form :model="form" label-width="90px">
        <el-form-item label="工号" required>
          <el-input v-model="form.employeeNo" placeholder="如 E004" />
        </el-form-item>
        <el-form-item label="姓名" required>
          <el-input v-model="form.name" placeholder="员工姓名" />
        </el-form-item>
        <el-form-item label="手机号">
          <el-input v-model="form.phone" placeholder="选填" />
        </el-form-item>
        <el-form-item label="所属车间" required>
          <el-select v-model="form.workshopId" placeholder="选择车间" style="width: 100%">
            <el-option v-for="w in workshops" :key="w.id" :label="`${w.name}（${w.code}）`" :value="w.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="角色" required>
          <el-radio-group v-model="form.role">
            <el-radio value="Worker">操作工</el-radio>
            <el-radio value="Inspector">质检员</el-radio>
            <el-radio value="Programmer">编程技术员</el-radio>
            <el-radio value="Admin">管理员</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item v-if="editingId" label="重置密码">
          <el-input v-model="form.password" type="password" show-password placeholder="留空则不修改密码" />
        </el-form-item>
        <el-alert v-if="!editingId" type="info" :closable="false" title="初始密码固定为 123456，首次登录后建议修改" />
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
import { roleLabel, roleType } from '../../utils/format'

const list = ref([])
const workshops = ref([])
const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const editingId = ref(null)
const filterWorkshopId = ref(null)
const filterRole = ref(null)

const form = reactive({
  employeeNo: '',
  name: '',
  phone: '',
  workshopId: null,
  role: 'Worker',
  password: ''
})

async function load() {
  loading.value = true
  try {
    list.value = await adminApi.employees({
      workshopId: filterWorkshopId.value || undefined,
      role: filterRole.value || undefined
    })
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = null
  Object.assign(form, { employeeNo: '', name: '', phone: '', workshopId: null, role: 'Worker', password: '' })
  dialogVisible.value = true
}

function openEdit(row) {
  editingId.value = row.id
  Object.assign(form, {
    employeeNo: row.employeeNo,
    name: row.name,
    phone: row.phone,
    workshopId: row.workshopId,
    role: row.role,
    password: ''
  })
  dialogVisible.value = true
}

async function onSubmit() {
  if (!form.employeeNo || !form.name) return ElMessage.warning('请填写工号和姓名')
  if (!form.workshopId) return ElMessage.warning('请选择所属车间')
  saving.value = true
  try {
    if (editingId.value) {
      const data = { ...form }
      if (!data.password) delete data.password
      await adminApi.updateEmployee(editingId.value, data)
      ElMessage.success('保存成功')
    } else {
      await adminApi.createEmployee({ ...form })
      ElMessage.success('新增成功，初始密码 123456')
    }
    dialogVisible.value = false
    await load()
  } finally {
    saving.value = false
  }
}

async function onDelete(row) {
  try {
    await ElMessageBox.confirm(`确定删除员工「${row.name}（${row.employeeNo}）」？`, '删除员工', { type: 'error' })
  } catch {
    return
  }
  try {
    await adminApi.deleteEmployee(row.id)
    ElMessage.success('已删除')
    await load()
  } catch {
    /* http 层已提示 */
  }
}

onMounted(async () => {
  workshops.value = await adminApi.workshops()
  await load()
})
</script>
