<template>
  <div class="login-page">
    <!-- Animated background mesh circles -->
    <div class="mesh-bg">
      <div class="circle circle-1"></div>
      <div class="circle circle-2"></div>
      <div class="circle circle-3"></div>
    </div>

    <div class="login-container">
      <div class="login-header">
        <h1 class="login-title">PRO Studio</h1>
        <p class="login-subtitle">请使用您的企业工号登录系统</p>
      </div>
      <el-form
        ref="formRef"
        :model="form"
        :rules="rules"
        class="login-form"
        @keyup.enter="handleLogin"
      >
        <el-form-item prop="employeeNo">
          <div class="input-label">工号</div>
          <el-input
            v-model="form.employeeNo"
            placeholder="员工工号"
            :prefix-icon="User"
            size="large"
          />
        </el-form-item>
        <el-form-item prop="password">
          <div class="input-label">密码</div>
          <el-input
            v-model="form.password"
            type="password"
            placeholder="密码"
            :prefix-icon="Lock"
            show-password
            size="large"
          />
        </el-form-item>
        <el-form-item class="btn-item">
          <el-button
            type="primary"
            size="large"
            class="login-btn"
            :loading="loading"
            @click="handleLogin"
          >
            登 录
          </el-button>
        </el-form-item>
      </el-form>

      <div class="version-info">
        <span class="version-tag">{{ versionText }}</span>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { User, Lock } from '@element-plus/icons-vue'
import { useAuthStore } from '@/stores/auth'
import dayjs from 'dayjs'

const versionText = computed(() => {
  return dayjs().format('YYMM') + '01'
})

const router = useRouter()
const authStore = useAuthStore()
const formRef = ref(null)
const loading = ref(false)

const form = reactive({
  employeeNo: '',
  password: '',
})

const rules = {
  employeeNo: [{ required: true, message: '请输入工号', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
}

async function handleLogin() {
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return

  loading.value = true
  try {
    const res = await authStore.login(form.employeeNo, form.password)
    if (res.success) {
      ElMessage.success('登录成功')
      router.push('/dashboard')
    } else {
      ElMessage.error(res.message || '登录失败')
    }
  } catch (err) {
    ElMessage.error('登录失败，请检查网络')
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-page {
  position: relative;
  height: 100vh;
  width: 100vw;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: #f5f5f7;
  overflow: hidden;
}

/* Fluid Apple Gradient background */
.mesh-bg {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 1;
  background: radial-gradient(circle at 50% 50%, #f5f5f7 0%, #e3e4e9 100%);
}

.circle {
  position: absolute;
  border-radius: 50%;
  filter: blur(100px);
  opacity: 0.45;
  mix-blend-mode: multiply;
  animation: float 25s infinite alternate ease-in-out;
}

.circle-1 {
  width: 500px;
  height: 500px;
  background: #0071e3;
  top: -10%;
  left: -10%;
  animation-delay: 0s;
}

.circle-2 {
  width: 600px;
  height: 600px;
  background: #34c759;
  bottom: -20%;
  right: -10%;
  animation-delay: -5s;
}

.circle-3 {
  width: 450px;
  height: 450px;
  background: #ff9500;
  top: 40%;
  left: 30%;
  animation-delay: -10s;
}

@keyframes float {
  0% {
    transform: translate(0, 0) scale(1) rotate(0deg);
  }
  50% {
    transform: translate(80px, 40px) scale(1.15) rotate(180deg);
  }
  100% {
    transform: translate(-40px, -60px) scale(0.9) rotate(360deg);
  }
}

.login-container {
  position: relative;
  z-index: 10;
  width: 380px;
  padding: 40px 32px;
  background: rgba(255, 255, 255, 0.55);
  backdrop-filter: blur(40px) saturate(210%);
  -webkit-backdrop-filter: blur(40px) saturate(210%);
  border: 1px solid rgba(255, 255, 255, 0.4);
  border-radius: 24px;
  box-shadow: 
    0 4px 30px rgba(0, 0, 0, 0.03),
    0 1px 1px rgba(255, 255, 255, 0.8) inset;
  transition: var(--apple-transition);
}

.login-header {
  text-align: center;
  margin-bottom: 32px;
}

.login-title {
  font-size: 26px;
  font-weight: 700;
  letter-spacing: -0.5px;
  color: #1d1d1f;
  margin-bottom: 8px;
}

.login-subtitle {
  color: #86868b;
  font-size: 13px;
  font-weight: 400;
}

.login-form {
  margin-top: 10px;
}

.input-label {
  font-size: 12px;
  font-weight: 600;
  color: #1d1d1f;
  margin-bottom: 6px;
  padding-left: 2px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.btn-item {
  margin-top: 32px;
}

.login-btn {
  width: 100%;
  height: 44px !important;
  font-size: 15px !important;
  font-weight: 600 !important;
  border-radius: 12px !important;
  background: #0071e3 !important;
  border: none !important;
  box-shadow: 0 4px 12px rgba(0, 113, 227, 0.25) !important;
  cursor: pointer;
}

.login-btn:hover {
  background: #147ce5 !important;
  box-shadow: 0 6px 16px rgba(0, 113, 227, 0.35) !important;
}

.login-btn:active {
  transform: scale(0.97);
}

.version-info {
  text-align: center;
  margin-top: 20px;
  padding-top: 16px;
  border-top: 1px solid rgba(0, 0, 0, 0.06);
}

.version-tag {
  font-size: 11px;
  font-weight: 500;
  color: #b6b6bb;
  letter-spacing: 0.3px;
  font-family: -apple-system, BlinkMacSystemFont, "SF Pro Text", "SF Pro Icons", "Helvetica Neue", Helvetica, Arial, sans-serif;
}
</style>
