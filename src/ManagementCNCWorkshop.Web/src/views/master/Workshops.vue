<template>
  <el-card shadow="hover">
    <div class="toolbar">
      <div class="toolbar-left"></div>
      <el-button type="primary" @click="openCreate">
        <el-icon><Plus /></el-icon> 新增车间
      </el-button>
    </div>

    <el-table :data="list" stripe v-loading="loading">
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column prop="code" label="车间编码" width="160" />
      <el-table-column prop="name" label="车间名称" />
      <el-table-column label="操作" width="140" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button link type="danger" @click="onDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑车间' : '新增车间'" width="440px">
      <el-form :model="form" label-width="90px">
        <el-form-item label="车间编码" required>
          <el-input v-model="form.code" placeholder="如 WS02" />
        </el-form-item>
        <el-form-item label="车间名称" required>
          <el-input v-model="form.name" placeholder="如 CNC二车间" />
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
import { ElMessage, ElMessageBox } from 'element-plus'
import { adminApi } from '../../api'

const list = ref([])
const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const editingId = ref(null)
const form = reactive({ code: '', name: '' })

async function load() {
  loading.value = true
  try {
    list.value = await adminApi.workshops()
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = null
  form.code = ''
  form.name = ''
  dialogVisible.value = true
}

function openEdit(row) {
  editingId.value = row.id
  form.code = row.code
  form.name = row.name
  dialogVisible.value = true
}

async function onSubmit() {
  if (!form.code || !form.name) return ElMessage.warning('请填写编码和名称')
  saving.value = true
  try {
    if (editingId.value) {
      await adminApi.updateWorkshop(editingId.value, { ...form })
      ElMessage.success('保存成功')
    } else {
      await adminApi.createWorkshop({ ...form })
      ElMessage.success('新增成功')
    }
    dialogVisible.value = false
    await load()
  } finally {
    saving.value = false
  }
}

async function onDelete(row) {
  try {
    await ElMessageBox.confirm(`确定删除车间「${row.name}」？`, '删除车间', { type: 'error' })
  } catch {
    return
  }
  try {
    await adminApi.deleteWorkshop(row.id)
    ElMessage.success('已删除')
    await load()
  } catch {
    /* http 层已提示 */
  }
}

onMounted(load)
</script>
