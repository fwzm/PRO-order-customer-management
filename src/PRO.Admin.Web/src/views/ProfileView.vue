<template>
  <div class="profile">
    <el-card shadow="never">
      <template #header><span>个人中心</span></template>
      <el-descriptions :column="1" border>
        <el-descriptions-item label="姓名">{{ authStore.user?.name }}</el-descriptions-item>
        <el-descriptions-item label="工号">{{ authStore.user?.employeeNo }}</el-descriptions-item>
        <el-descriptions-item label="角色">{{ authStore.user?.roleName }}</el-descriptions-item>
        <el-descriptions-item label="分公司">{{ authStore.user?.branchName }}</el-descriptions-item>
      </el-descriptions>
    </el-card>

    <el-card shadow="never" class="section">
      <template #header><span>修改密码</span></template>
      <el-form :model="pwdForm" label-width="100px" style="max-width: 400px">
        <el-form-item label="旧密码">
          <el-input v-model="pwdForm.oldPassword" type="password" show-password />
        </el-form-item>
        <el-form-item label="新密码">
          <el-input v-model="pwdForm.newPassword" type="password" show-password />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="handleChangePwd">修改密码</el-button>
        </el-form-item>
      </el-form>
    </el-card>
  </div>
</template>

<script setup>
import { reactive } from 'vue'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '@/stores/auth'
import { authApi } from '@/api/auth'

const authStore = useAuthStore()
const pwdForm = reactive({ oldPassword: '', newPassword: '' })

async function handleChangePwd() {
  const res = await authApi.changePassword({
    employeeId: authStore.user?.employeeId,
    oldPassword: pwdForm.oldPassword,
    newPassword: pwdForm.newPassword,
  })
  if (res.success) {
    ElMessage.success('密码修改成功')
    pwdForm.oldPassword = ''
    pwdForm.newPassword = ''
  }
}
</script>

<style scoped>
.section { margin-top: 16px; }
</style>
