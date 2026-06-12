<template>
  <div class="department-manage">
    <div class="main-layout">
      <div class="tree-panel">
        <el-card shadow="never">
          <template #header>
            <div class="panel-header">
              <span>组织架构</span>
              <el-button text type="primary" size="small" @click="handleAddRootDept">
                <el-icon><Plus /></el-icon>添加根部门
              </el-button>
            </div>
          </template>
          <el-tree
            ref="treeRef"
            :data="departmentTree"
            :props="treeProps"
            node-key="id"
            default-expand-all
            highlight-current
            @node-click="handleNodeClick"
          >
            <template #default="{ node, data }">
              <div class="tree-node">
                <template v-if="data._editing">
                  <el-input
                    v-model="data._editName"
                    size="small"
                    style="width: 140px"
                    @keyup.enter="confirmEdit(node, data)"
                  />
                  <el-button text type="primary" size="small" @click="confirmEdit(node, data)">确定</el-button>
                  <el-button text size="small" @click="cancelEdit(data)">取消</el-button>
                </template>
                <template v-else>
                  <span class="node-label">{{ data.name }}</span>
                  <span class="node-actions">
                    <el-icon size="14" color="#409EFF" class="action-icon" @click.stop="handleAddChild(data)"><Plus /></el-icon>
                    <el-icon size="14" color="#E6A23C" class="action-icon" @click.stop="handleRename(data)"><Edit /></el-icon>
                    <el-icon size="14" color="#F56C6C" class="action-icon" @click.stop="handleDeleteDept(node, data)"><Delete /></el-icon>
                  </span>
                </template>
              </div>
            </template>
          </el-tree>
        </el-card>
      </div>

      <div class="user-panel">
        <el-card shadow="never">
          <template #header>
            <div class="panel-header">
              <span>{{ selectedDeptName ? selectedDeptName + ' - 部门成员' : '部门成员' }}</span>
              <el-button
                type="primary"
                size="small"
                :disabled="!selectedDeptId"
                @click="showUserDialog = true; userForm = { name: '', employeeNo: '', phone: '' }; isEditUser = false"
              >
                <el-icon><Plus /></el-icon>添加成员
              </el-button>
            </div>
          </template>
          <el-empty v-if="!selectedDeptId" description="请选择一个部门" />
          <el-table v-else :data="currentUsers" stripe style="width: 100%">
            <el-table-column prop="employeeNo" label="工号" width="120" />
            <el-table-column prop="name" label="姓名" width="120" />
            <el-table-column prop="phone" label="电话" width="140" />
            <el-table-column label="操作" width="160" fixed="right">
              <template #default="{ row, $index }">
                <el-button text type="primary" size="small" @click="handleEditUser(row, $index)">编辑</el-button>
                <el-button text type="danger" size="small" @click="handleDeleteUser($index)">移除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </div>
    </div>

    <el-dialog v-model="showUserDialog" :title="isEditUser ? '编辑成员' : '添加成员'" width="450px">
      <el-form :model="userForm" label-width="80px">
        <el-form-item label="姓名">
          <el-input v-model="userForm.name" />
        </el-form-item>
        <el-form-item label="工号">
          <el-input v-model="userForm.employeeNo" />
        </el-form-item>
        <el-form-item label="电话">
          <el-input v-model="userForm.phone" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showUserDialog = false">取消</el-button>
        <el-button type="primary" @click="handleSaveUser">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, nextTick } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Edit, Delete } from '@element-plus/icons-vue'

const treeRef = ref(null)
const selectedDeptId = ref(null)
const selectedDeptName = ref('')
const showUserDialog = ref(false)
const isEditUser = ref(false)
const editUserIndex = ref(-1)

const userForm = ref({ name: '', employeeNo: '', phone: '' })

const treeProps = {
  children: 'children',
  label: 'name',
}

const departmentData = ref([])

let deptIdCounter = 10
let userIdCounter = 100

const selectedDept = computed(() => {
  return findDept(departmentData.value, selectedDeptId.value)
})

const currentUsers = computed(() => {
  const dept = selectedDept.value
  return dept ? dept.users : []
})

const departmentTree = computed(() => departmentData.value)

function findDept(nodes, id) {
  for (const node of nodes) {
    if (node.id === id) return node
    if (node.children) {
      const found = findDept(node.children, id)
      if (found) return found
    }
  }
  return null
}

function findParent(nodes, id) {
  for (const node of nodes) {
    if (node.children && node.children.some(c => c.id === id)) return node
    if (node.children) {
      const found = findParent(node.children, id)
      if (found) return found
    }
  }
  return null
}

function removeNode(nodes, id) {
  for (let i = 0; i < nodes.length; i++) {
    if (nodes[i].id === id) {
      nodes.splice(i, 1)
      return true
    }
    if (nodes[i].children) {
      if (removeNode(nodes[i].children, id)) return true
    }
  }
  return false
}

function handleNodeClick(data) {
  selectedDeptId.value = data.id
  selectedDeptName.value = data.name
}

function handleAddRootDept() {
  const newDept = {
    id: deptIdCounter++,
    name: '新部门',
    children: [],
    users: [],
    _editing: true,
    _editName: '',
  }
  departmentData.value.push(newDept)
  nextTick(() => {
    selectedDeptId.value = newDept.id
    selectedDeptName.value = newDept.name
  })
}

function handleAddChild(data) {
  if (!data.children) data.children = []
  const newDept = {
    id: deptIdCounter++,
    name: '新子部门',
    children: [],
    users: [],
    _editing: true,
    _editName: '',
  }
  data.children.push(newDept)
  selectedDeptId.value = newDept.id
  selectedDeptName.value = newDept.name
}

function handleRename(data) {
  data._editing = true
  data._editName = data.name
}

function confirmEdit(node, data) {
  if (!data._editName || !data._editName.trim()) {
    ElMessage.warning('部门名称不能为空')
    return
  }
  data.name = data._editName.trim()
  data._editing = false
  data._editName = undefined
  selectedDeptName.value = data.name
  ElMessage.success('部门名称已更新')
}

function cancelEdit(data) {
  data._editing = false
  data._editName = undefined
  if (!data.name) {
    removeNode(departmentData.value, data.id)
  }
}

async function handleDeleteDept(node, data) {
  await ElMessageBox.confirm(`确定删除部门"${data.name}"及其所有子部门？`, '提示')
  if (selectedDeptId.value === data.id) {
    selectedDeptId.value = null
    selectedDeptName.value = ''
  }
  removeNode(departmentData.value, data.id)
  ElMessage.success('部门已删除')
}

function handleEditUser(row, index) {
  isEditUser.value = true
  editUserIndex.value = index
  userForm.value = { name: row.name, employeeNo: row.employeeNo, phone: row.phone }
  showUserDialog.value = true
}

function handleSaveUser() {
  if (!userForm.value.name || !userForm.value.employeeNo) {
    ElMessage.warning('请填写完整信息')
    return
  }
  const dept = selectedDept.value
  if (!dept) return
  if (isEditUser.value) {
    const idx = editUserIndex.value
    dept.users[idx].name = userForm.value.name
    dept.users[idx].employeeNo = userForm.value.employeeNo
    dept.users[idx].phone = userForm.value.phone
    ElMessage.success('成员信息已更新')
  } else {
    dept.users.push({
      id: userIdCounter++,
      name: userForm.value.name,
      employeeNo: userForm.value.employeeNo,
      phone: userForm.value.phone,
    })
    ElMessage.success('成员已添加')
  }
  showUserDialog.value = false
}

function handleDeleteUser(index) {
  const dept = selectedDept.value
  if (!dept) return
  dept.users.splice(index, 1)
  ElMessage.success('成员已移除')
}

onMounted(() => {
  departmentData.value = [
    {
      id: 1, name: '总公司', children: [
        {
          id: 2, name: '技术部', children: [
            { id: 4, name: '前端组', children: [], users: [{ id: 101, name: '张三', employeeNo: 'EMP001', phone: '13800138001' }, { id: 102, name: '李四', employeeNo: 'EMP002', phone: '13800138002' }] },
            { id: 5, name: '后端组', children: [], users: [{ id: 103, name: '王五', employeeNo: 'EMP003', phone: '13800138003' }] },
          ], users: [{ id: 104, name: '赵六', employeeNo: 'EMP004', phone: '13800138004' }],
        },
        {
          id: 3, name: '市场部', children: [
            { id: 6, name: '销售组', children: [], users: [{ id: 105, name: '钱七', employeeNo: 'EMP005', phone: '13800138005' }] },
          ], users: [{ id: 106, name: '孙八', employeeNo: 'EMP006', phone: '13800138006' }],
        },
      ], users: [],
    },
  ]
})
</script>

<style scoped>
.main-layout {
  display: flex;
  gap: 16px;
  align-items: flex-start;
}
.tree-panel {
  width: 360px;
  flex-shrink: 0;
}
.user-panel {
  flex: 1;
  min-width: 0;
}
.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.tree-node {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
}
.node-label {
  font-size: 14px;
}
.node-actions {
  display: none;
  margin-left: auto;
}
.tree-node:hover .node-actions {
  display: inline-flex;
  gap: 4px;
}
.action-icon {
  cursor: pointer;
  padding: 2px;
}
.action-icon:hover {
  opacity: 0.7;
}
</style>