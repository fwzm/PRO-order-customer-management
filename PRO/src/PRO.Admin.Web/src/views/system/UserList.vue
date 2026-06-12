<template>
  <div class="user-list">
    <el-card shadow="never">
      <el-form :inline="true" :model="searchForm" class="search-bar">
        <el-form-item label="关键词">
          <el-input v-model="searchForm.keyword" placeholder="姓名/工号" clearable />
        </el-form-item>
        <el-form-item label="分公司">
          <el-select v-model="searchForm.branchId" placeholder="全部" clearable style="width: 140px">
            <el-option v-for="b in branches" :key="b.id" :label="b.name" :value="b.id" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="handleSearch">查询</el-button>
          <el-button @click="handleReset">重置</el-button>
        </el-form-item>
      </el-form>

      <div class="action-bar">
        <el-button type="primary" @click="showDialog = true; isEdit = false; resetUserForm()">
          <el-icon><Plus /></el-icon>新增用户
        </el-button>
      </div>

      <el-table :data="tableData" stripe v-loading="loading" style="width: 100%">
        <el-table-column prop="employeeNo" label="工号" width="110" />
        <el-table-column prop="name" label="姓名" width="110">
          <template #default="{ row }">
            <el-link type="primary" @click="showUserDetail(row)">{{ row.name }}</el-link>
          </template>
        </el-table-column>
        <el-table-column prop="branchName" label="分公司" width="110" />
        <el-table-column prop="departmentName" label="部门" width="110" />
        <el-table-column prop="roleName" label="角色" width="90" />
        <el-table-column prop="phone" label="电话" width="130" />
        <el-table-column prop="status" label="状态" width="80">
          <template #default="{ row }">
            <el-tag :type="row.status === 1 ? 'success' : 'danger'">
              {{ row.status === 1 ? '启用' : '停用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" width="170" />
        <el-table-column label="操作" width="200" fixed="right">
          <template #default="{ row }">
            <el-button text type="primary" @click="showUserDetail(row)">详情</el-button>
            <el-button text type="primary" @click="handleEditUser(row)">编辑</el-button>
            <el-button text type="primary" @click="handleResetPwd(row)">重置密码</el-button>
            <el-button text type="danger" @click="handleToggleStatus(row)">
              {{ row.status === 1 ? '禁用' : '启用' }}
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-drawer v-model="showDetailDrawer" :title="'用户详情 - ' + (detailUser?.name || '')" size="500px">
      <template v-if="detailUser">
        <el-descriptions :column="1" border style="margin: 16px;">
          <el-descriptions-item label="姓名">{{ detailUser.name }}</el-descriptions-item>
          <el-descriptions-item label="工号">{{ detailUser.employeeNo }}</el-descriptions-item>
          <el-descriptions-item label="电话">{{ detailUser.phone }}</el-descriptions-item>
          <el-descriptions-item label="邮箱">{{ detailUser.email || '-' }}</el-descriptions-item>
          <el-descriptions-item label="分公司">{{ detailUser.branchName }}</el-descriptions-item>
          <el-descriptions-item label="部门">{{ detailUser.departmentName }}</el-descriptions-item>
          <el-descriptions-item label="角色">{{ detailUser.roleName }}</el-descriptions-item>
          <el-descriptions-item label="状态">
            <el-tag :type="detailUser.status === 1 ? 'success' : 'danger'">
              {{ detailUser.status === 1 ? '启用' : '停用' }}
            </el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="创建时间">{{ detailUser.createdAt }}</el-descriptions-item>
          <el-descriptions-item label="最后修改时间">{{ detailUser.updatedAt || '-' }}</el-descriptions-item>
        </el-descriptions>
      </template>
      <template v-else>
        <el-skeleton :rows="10" animated />
      </template>
    </el-drawer>

    <el-dialog v-model="showDialog" :title="isEdit ? '编辑用户' : '新增用户'" width="600px">
      <el-form :model="userForm" ref="userFormRef" label-width="90px">
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="姓名" prop="name">
              <el-input v-model="userForm.name" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="工号" prop="employeeNo">
              <el-input v-model="userForm.employeeNo" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="电话">
              <el-input v-model="userForm.phone" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="邮箱">
              <el-input v-model="userForm.email" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="分公司">
          <el-select v-model="userForm.branchId" filterable style="width: 100%">
            <el-option v-for="b in branches" :key="b.id" :label="b.name" :value="b.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="角色">
          <el-select v-model="userForm.roleId" filterable style="width: 100%">
            <el-option v-for="r in roles" :key="r.id" :label="r.name" :value="r.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="userForm.status">
            <el-radio :value="1">启用</el-radio>
            <el-radio :value="0">停用</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showDialog = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSaveUser">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { employeeApi, branchApi, systemApi } from '@/api'

const tableData = ref([])
const branches = ref([])
const roles = ref([])
const loading = ref(false)
const saving = ref(false)
const showDialog = ref(false)
const isEdit = ref(false)
const showDetailDrawer = ref(false)
const detailUser = ref(null)
const userFormRef = ref(null)

const searchForm = reactive({ keyword: '', branchId: null })

const userForm = reactive({
  id: 0, name: '', employeeNo: '', phone: '', email: '',
  branchId: null, roleId: null, status: 1,
})

function resetUserForm() {
  Object.assign(userForm, {
    id: 0, name: '', employeeNo: '', phone: '', email: '',
    branchId: null, roleId: null, status: 1,
  })
}

async function loadData() {
  loading.value = true
  try {
    const res = await employeeApi.getList({ pageSize: 200, ...searchForm })
    if (res.success) tableData.value = res.data?.items || []
  } finally { loading.value = false }
}

function handleSearch() { loadData() }
function handleReset() { searchForm.keyword = ''; searchForm.branchId = null; loadData() }

function showUserDetail(row) {
  detailUser.value = row
  showDetailDrawer.value = true
}

function handleEditUser(row) {
  isEdit.value = true
  Object.assign(userForm, {
    id: row.id, name: row.name, employeeNo: row.employeeNo,
    phone: row.phone, email: row.email || '',
    branchId: row.branchId, roleId: row.roleId, status: row.status ?? 1,
  })
  showDialog.value = true
}

async function handleSaveUser() {
  saving.value = true
  try {
    const res = isEdit.value
      ? await employeeApi.update(userForm.id, userForm)
      : await employeeApi.create(userForm)
    if (res.success) { ElMessage.success(isEdit.value ? '更新成功' : '创建成功'); showDialog.value = false; loadData() }
    else ElMessage.error(res.message)
  } finally { saving.value = false }
}

async function handleResetPwd(row) {
  const res = await employeeApi.resetPassword(row.id, { newPassword: '123456' })
  if (res.success) ElMessage.success(`已重置 ${row.name} 的密码为 123456`)
}

async function handleToggleStatus(row) {
  const newStatus = row.status === 1 ? 0 : 1
  const res = await employeeApi.update(row.id, { ...row, status: newStatus })
  if (res.success) { ElMessage.success(newStatus === 1 ? '已启用' : '已禁用'); loadData() }
}

onMounted(async () => {
  const [bRes, rRes] = await Promise.all([
    branchApi.getList({ pageSize: 100 }),
    systemApi.getRoles(),
  ])
  if (bRes.success) branches.value = bRes.data?.items || []
  if (rRes.success) roles.value = rRes.data || []
  loadData()
})
</script>

<style scoped>
.search-bar { padding-bottom: 16px; border-bottom: 1px solid #eee; margin-bottom: 16px; }
.action-bar { margin-bottom: 16px; }
</style>