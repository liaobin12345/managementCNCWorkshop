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
      <el-table-column label="现场照片" width="90">
        <template #default="{ row }">
          <el-image
            v-if="row.imageUrl"
            :src="row.imageUrl"
            :preview-src-list="[row.imageUrl]"
            preview-teleported
            fit="cover"
            class="thumb-img"
          />
          <span v-else class="no-img">-</span>
        </template>
      </el-table-column>
      <el-table-column label="二维码" width="120" align="center">
        <template #default="{ row }">
          <div class="qr-cell">
            <QrCodeImage v-if="row.qrCode" :text="row.qrCode" />
            <span v-else class="no-img">-</span>
            <div class="qr-text" :title="row.qrCode">{{ row.qrCode }}</div>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="130" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button link type="danger" @click="onDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑产品' : '新增产品'" width="480px">
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
        <el-form-item label="现场照片">
          <div class="img-upload">
            <el-image
              v-if="form.imageUrl"
              :src="form.imageUrl"
              :preview-src-list="[form.imageUrl]"
              preview-teleported
              fit="cover"
              class="img-preview"
            />
            <el-upload
              :show-file-list="false"
              :http-request="onUpload"
              :before-upload="beforeUpload"
              accept="image/*"
            >
              <el-button size="small">{{ form.imageUrl ? '更换照片' : '上传照片' }}</el-button>
            </el-upload>
            <el-button v-if="form.imageUrl" size="small" text type="danger" @click="form.imageUrl = ''">
              移除
            </el-button>
          </div>
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
import QrCodeImage from '../../components/QrCodeImage.vue'

const list = ref([])
const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const editingId = ref(null)
const form = reactive({ code: '', name: '', specification: '', qrCode: '', imageUrl: '' })

async function load() {
  loading.value = true
  try {
    list.value = await adminApi.products()
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = null
  Object.assign(form, { code: '', name: '', specification: '', qrCode: '', imageUrl: '' })
  dialogVisible.value = true
}

function openEdit(row) {
  editingId.value = row.id
  Object.assign(form, {
    code: row.code,
    name: row.name,
    specification: row.specification,
    qrCode: row.qrCode,
    imageUrl: row.imageUrl
  })
  dialogVisible.value = true
}

function beforeUpload(file) {
  if (!file.type.startsWith('image/')) {
    ElMessage.warning('只能上传图片文件')
    return false
  }
  if (file.size > 5 * 1024 * 1024) {
    ElMessage.warning('图片大小不能超过 5MB')
    return false
  }
  return true
}

async function onUpload({ file }) {
  try {
    const res = await adminApi.uploadImage(file)
    form.imageUrl = res.url
    ElMessage.success('上传成功')
  } catch {
    ElMessage.error('上传失败，请重试')
  }
}

async function onSubmit() {
  if (!form.code || !form.name) return ElMessage.warning('请填写产品编码和名称')
  saving.value = true
  try {
    if (editingId.value) {
      await adminApi.updateProduct(editingId.value, { ...form })
      ElMessage.success('保存成功')
    } else {
      await adminApi.createProduct({ ...form })
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
    await ElMessageBox.confirm(`确定删除产品「${row.name}（${row.code}）」？`, '删除产品', { type: 'error' })
  } catch {
    return
  }
  try {
    await adminApi.deleteProduct(row.id)
    ElMessage.success('已删除')
    await load()
  } catch {
    /* http 层已提示 */
  }
}

onMounted(load)
</script>

<style scoped>
.thumb-img {
  width: 48px;
  height: 48px;
  border-radius: 6px;
  cursor: pointer;
  border: 1px solid #eee;
}

.qr-cell {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}

.qr-text {
  max-width: 110px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 12px;
  color: #909399;
}

.no-img {
  color: #c0c4cc;
}

.img-upload {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.img-preview {
  width: 64px;
  height: 64px;
  border-radius: 6px;
  border: 1px solid #eee;
}
</style>
