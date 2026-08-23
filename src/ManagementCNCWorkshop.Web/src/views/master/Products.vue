<template>
  <el-card shadow="hover">
    <div class="toolbar">
      <div class="toolbar-left"></div>
      <el-button type="primary" @click="openCreate">
        <el-icon><Plus /></el-icon> 新增产品
      </el-button>
    </div>

    <el-table :data="list" stripe v-loading="loading">
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column prop="code" label="产品编码" width="120" />
      <el-table-column prop="name" label="产品名称" width="160" />
      <el-table-column prop="specification" label="规格型号" width="140" />
      <el-table-column prop="qrCode" label="二维码内容">
        <template #default="{ row }">
          <el-tag type="info" size="small">{{ row.qrCode }}</el-tag>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" title="新增产品" width="480px">
      <el-form :model="form" label-width="90px">
        <el-form-item label="产品编码" required>
          <el-input v-model="form.code" placeholder="如 P003" />
        </el-form-item>
        <el-form-item label="产品名称" required>
          <el-input v-model="form.name" placeholder="如 传动轴" />
        </el-form-item>
        <el-form-item label="规格型号">
          <el-input v-model="form.specification" placeholder="如 Φ40×100" />
        </el-form-item>
        <el-form-item label="二维码内容">
          <el-input v-model="form.qrCode" placeholder="如 PROD:P003，扫码报工按此匹配" />
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

const list = ref([])
const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const form = reactive({ code: '', name: '', specification: '', qrCode: '' })

async function load() {
  loading.value = true
  try {
    list.value = await adminApi.products()
  } finally {
    loading.value = false
  }
}

function openCreate() {
  Object.assign(form, { code: '', name: '', specification: '', qrCode: '' })
  dialogVisible.value = true
}

async function onSubmit() {
  if (!form.code || !form.name) return ElMessage.warning('请填写产品编码和名称')
  saving.value = true
  try {
    await adminApi.createProduct({ ...form })
    dialogVisible.value = false
    ElMessage.success('新增成功')
    await load()
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>
