<template>
  <div class="login-page">
    <el-card class="login-card">
      <div class="login-logo">⚙️</div>
      <h2 class="login-title">CNC 车间运营后台</h2>
      <p class="login-sub">数据驱动的车间生产管理系统</p>

      <el-form :model="form" @keyup.enter="onSubmit" size="large">
        <el-form-item>
          <el-input v-model="form.employeeNo" placeholder="工号（如 E900）" clearable>
            <template #prefix><el-icon><User /></el-icon></template>
          </el-input>
        </el-form-item>
        <el-form-item>
          <el-input v-model="form.password" type="password" placeholder="密码" show-password>
            <template #prefix><el-icon><Lock /></el-icon></template>
          </el-input>
        </el-form-item>
        <el-button type="primary" size="large" class="login-btn" :loading="loading" @click="onSubmit">
          登 录
        </el-button>
      </el-form>

      <el-alert type="info" :closable="false" class="login-tip">
        <template #title>演示账号：管理员 E900 / admin123；编程技术员 E004 / 123456</template>
      </el-alert>
    </el-card>
  </div>
</template>

<script setup>
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { authApi } from '../api'
import { useAuthStore } from '../store/auth'

const router = useRouter()
const auth = useAuthStore()
const loading = ref(false)

const form = reactive({
  employeeNo: 'E900',
  password: 'admin123'
})

async function onSubmit() {
  if (!form.employeeNo || !form.password) {
    ElMessage.warning('请输入工号和密码')
    return
  }
  loading.value = true
  try {
    const res = await authApi.login(form)
    auth.setLogin(res.token, res.employee)
    ElMessage.success(`欢迎回来，${res.employee.name}`)
    router.push('/dashboard')
  } catch {
    // 错误提示已由 http 拦截器处理
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-page {
  height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #1f3a5f 0%, #2d6a9f 50%, #3b8cc6 100%);
}

.login-card {
  width: 400px;
  padding: 8px 12px;
  border-radius: 10px;
}

.login-logo {
  font-size: 48px;
  text-align: center;
}

.login-title {
  text-align: center;
  margin: 8px 0 4px;
  color: #303133;
}

.login-sub {
  text-align: center;
  color: #909399;
  font-size: 13px;
  margin-bottom: 24px;
}

.login-btn {
  width: 100%;
}

.login-tip {
  margin-top: 20px;
}
</style>
