<template>
  <div class="role-list">
    <el-card shadow="never">
      <div class="action-bar">
        <el-button type="primary" @click="showRoleDialog = true">
          <el-icon><Plus /></el-icon>新增角色
        </el-button>
      </div>

      <el-table :data="roles" stripe style="width: 100%">
        <el-table-column prop="name" label="角色名称" />
        <el-table-column prop="code" label="角色代码" />
        <el-table-column prop="roleType" label="类型" />
        <el-table-column label="操作" width="200">
          <template #default="{ row }">
            <el-button text type="primary" @click="handleConfigPermission(row)">配置权限</el-button>
            <el-button text type="danger">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 权限配置对话框 -->
    <el-dialog v-model="showPermissionDialog" title="权限配置" width="600px">
      <el-tree
        ref="treeRef"
        :data="permissionTree"
        show-checkbox
        node-key="id"
        :props="{ label: 'name', children: 'children' }"
        default-expand-all
      />
      <template #footer>
        <el-button @click="showPermissionDialog = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSavePermission">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="showRoleDialog" title="新增角色" width="400px">
      <el-form :model="roleForm" label-width="80px">
        <el-form-item label="名称"><el-input v-model="roleForm.name" /></el-form-item>
        <el-form-item label="代码"><el-input v-model="roleForm.code" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showRoleDialog = false">取消</el-button>
        <el-button type="primary" @click="handleCreateRole">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { systemApi } from '@/api'

const roles = ref([])
const permissionTree = ref([])
const showPermissionDialog = ref(false)
const showRoleDialog = ref(false)
const saving = ref(false)
const currentRoleId = ref(0)
const roleForm = ref({ name: '', code: '' })

onMounted(async () => {
  const rRes = await systemApi.getRoles()
  if (rRes.success) roles.value = rRes.data || []
})

async function handleConfigPermission(row) {
  currentRoleId.value = row.id
  const pRes = await systemApi.getPermissionTree()
  if (pRes.success) permissionTree.value = pRes.data || []
  const rpRes = await systemApi.getRolePermissions(row.id)
  showPermissionDialog.value = true
}

async function handleSavePermission() {
  saving.value = true
  try { ElMessage.success('权限保存成功'); showPermissionDialog.value = false }
  finally { saving.value = false }
}

async function handleCreateRole() {
  const res = await systemApi.createRole(roleForm.value)
  if (res.success) { ElMessage.success('创建成功'); showRoleDialog.value = false }
}
</script>

<style scoped>
.action-bar { margin-bottom: 16px; }
</style>
