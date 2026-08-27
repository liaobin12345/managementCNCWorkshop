<template>
  <el-card shadow="hover">
    <div class="toolbar">
      <div class="toolbar-left">
        <el-select v-model="filterWorkshopId" placeholder="全部车间" clearable style="width: 160px" @change="load">
          <el-option v-for="w in workshops" :key="w.id" :label="w.name" :value="w.id" />
        </el-select>
      </div>
      <el-button type="primary" @click="openCreate">
        <el-icon><Plus /></el-icon> 新增设备
      </el-button>
    </div>

    <el-table :data="list" stripe v-loading="loading">
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column prop="code" label="设备编码" width="110" />
      <el-table-column prop="name" label="设备名称" width="180" />
      <el-table-column prop="workshop.name" label="所属车间" width="130" />
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
      <el-table-column label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="equipmentStatusType[row.status] || 'info'" size="small">
            {{ equipmentStatusLabel[row.status] || row.status }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="上次保养" width="120">
        <template #default="{ row }">{{ formatDate(row.lastMaintenanceDate) }}</template>
      </el-table-column>
      <el-table-column prop="qrCode" label="二维码内容" />
      <el-table-column label="操作" width="90" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑设备' : '新增设备'" width="480px">
      <el-form :model="form" label-width="90px">
        <el-form-item label="设备编码" required>
          <el-input v-model="form.code" placeholder="如 CNC-02" />
        </el-form-item>
        <el-form-item label="设备名称" required>
          <el-input v-model="form.name" placeholder="如 数控车床" />
        </el-form-item>
        <el-form-item label="所属车间" required>
          <el-select v-model="form.workshopId" placeholder="选择车间" style="width: 100%">
            <el-option v-for="w in workshops" :key="w.id" :label="`${w.name}（${w.code}）`" :value="w.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="运行状态">
          <el-select v-model="form.status" style="width: 100%">
            <el-option label="运行中" value="Running" />
            <el-option label="空闲" value="Idle" />
            <el-option label="保养中" value="Maintenance" />
            <el-option label="故障" value="Fault" />
          </el-select>
        </el-form-item>
        <el-form-item label="二维码内容">
          <el-input v-model="form.qrCode" placeholder="如 EQ:CNC-02" />
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
import { ElMessage } from 'element-plus'
import { adminApi } from '../../api'
import { equipmentStatusLabel, equipmentStatusType, formatDate } from '../../utils/format'

const list = ref([])
const workshops = ref([])
const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const editingId = ref(null)
const filterWorkshopId = ref(null)

const form = reactive({
  code: '',
  name: '',
  workshopId: null,
  status: 'Idle',
  qrCode: '',
  imageUrl: ''
})

async function load() {
  loading.value = true
  try {
    list.value = await adminApi.equipments({ workshopId: filterWorkshopId.value || undefined })
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = null
  Object.assign(form, { code: '', name: '', workshopId: null, status: 'Idle', qrCode: '', imageUrl: '' })
  dialogVisible.value = true
}

function openEdit(row) {
  editingId.value = row.id
  Object.assign(form, {
    code: row.code,
    name: row.name,
    workshopId: row.workshopId,
    status: row.status,
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
  if (!form.code || !form.name) return ElMessage.warning('请填写设备编码和名称')
  if (!form.workshopId) return ElMessage.warning('请选择所属车间')
  saving.value = true
  try {
    if (editingId.value) {
      await adminApi.updateEquipment(editingId.value, { ...form })
      ElMessage.success('保存成功')
    } else {
      await adminApi.createEquipment({ ...form })
      ElMessage.success('新增成功')
    }
    dialogVisible.value = false
    await load()
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  workshops.value = await adminApi.workshops()
  await load()
})
</script>

<style scoped>
.thumb-img {
  width: 48px;
  height: 48px;
  border-radius: 6px;
  cursor: pointer;
  border: 1px solid #eee;
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
