<template>
  <div class="customer-list">
    <el-card shadow="never">
      <el-form :inline="true" :model="searchForm" class="search-bar">
        <el-form-item label="关键词">
          <el-input v-model="searchForm.keyword" placeholder="客户名称/电话" clearable />
        </el-form-item>
        <el-form-item label="分公司">
          <el-select v-model="searchForm.branchId" placeholder="全部" clearable style="width: 160px">
            <el-option v-for="b in branches" :key="b.id" :label="b.name" :value="b.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="客户担当">
          <el-input v-model="searchForm.customerManagerName" placeholder="跟进人" clearable style="width: 140px" />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="handleSearch">查询</el-button>
          <el-button @click="handleReset">重置</el-button>
        </el-form-item>
      </el-form>

      <div class="action-bar">
        <el-button type="primary" @click="showDialog = true; isEdit = false; resetForm()">
          <el-icon><Plus /></el-icon>新增客户
        </el-button>
      </div>

      <el-table :data="tableData" stripe v-loading="loading" style="width: 100%">
        <el-table-column prop="name" label="客户名称" min-width="160">
          <template #default="{ row }">
            <el-link type="primary" @click="$router.push(`/customers/${row.id}`)">{{ row.name }}</el-link>
          </template>
        </el-table-column>
        <el-table-column prop="address" label="地址" min-width="200" show-overflow-tooltip />
        <el-table-column prop="customerManagerName" label="客户担当" width="120">
          <template #default="{ row }">{{ row.customerManagerName || '未分配' }}</template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" width="170" show-overflow-tooltip />
        <el-table-column label="操作" width="160" fixed="right">
          <template #default="{ row }">
            <el-button text type="primary" @click="$router.push(`/customers/${row.id}`)">详情</el-button>
            <el-button text type="primary" @click="handleEdit(row)">编辑</el-button>
            <el-button text type="danger" @click="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>

      <div class="pagination">
        <el-pagination
          v-model:current-page="pageIndex" v-model:page-size="pageSize"
          :total="totalCount" :page-sizes="[20, 50, 100]"
          layout="total, sizes, prev, pager, next"
          @size-change="loadData" @current-change="loadData"
        />
      </div>
    </el-card>

    <el-dialog v-model="showDialog" :title="isEdit ? '编辑客户' : '新增客户'" width="700px">
      <el-form :model="form" :rules="rules" ref="formRef" label-width="100px">
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="客户名称" prop="name">
              <el-input v-model="form.name" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="客户类型" prop="customerType">
              <el-select v-model="form.customerType" style="width: 100%">
                <el-option label="大客户" :value="1" />
                <el-option label="细分客户" :value="2" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="电话">
              <el-input v-model="form.phone" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="所属分公司">
              <el-select v-model="form.branchId" style="width: 100%">
                <el-option v-for="b in branches" :key="b.id" :label="b.name" :value="b.id" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="地址">
          <el-input v-model="form.address" type="textarea" :rows="2" />
        </el-form-item>
        <el-form-item label="客户担当">
          <el-select v-model="form.customerManagerId" filterable clearable style="width: 100%">
            <el-option v-for="e in employees" :key="e.id" :label="e.name" :value="e.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="关联大客户">
          <el-select v-model="form.parentCustomerId" filterable clearable style="width: 100%">
            <el-option v-for="c in parentCustomers" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="法人代表">
          <el-input v-model="form.legalPerson" />
        </el-form-item>
        <el-form-item label="注册地址">
          <el-input v-model="form.registerAddress" type="textarea" :rows="2" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.remark" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showDialog = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { customerApi, branchApi, employeeApi } from '@/api'

const tableData = ref([])
const branches = ref([])
const employees = ref([])
const parentCustomers = ref([])
const loading = ref(false)
const saving = ref(false)
const pageIndex = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)

const searchForm = reactive({ keyword: '', branchId: null, customerManagerName: '' })
const showDialog = ref(false)
const isEdit = ref(false)
const formRef = ref(null)

const form = reactive({
  id: 0, name: '', customerType: 2, phone: '', address: '',
  customerManagerId: null, parentCustomerId: null,
  legalPerson: '', registerAddress: '', branchId: null, remark: '',
})

const rules = {
  name: [{ required: true, message: '请输入客户名称' }],
  customerType: [{ required: true, message: '请选择客户类型' }],
}

function resetForm() {
  Object.assign(form, {
    id: 0, name: '', customerType: 2, phone: '', address: '',
    customerManagerId: null, parentCustomerId: null,
    legalPerson: '', registerAddress: '', branchId: null, remark: '',
  })
}

async function loadData() {
  loading.value = true
  try {
    const res = await customerApi.getList({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      ...searchForm,
    })
    if (res.success) {
      const items = res.data?.items || []
      tableData.value = items.filter(c => c.customerType !== 1)
      totalCount.value = res.data?.totalCount || 0
    }
  } finally {
    loading.value = false
  }
}

async function loadBranches() {
  const res = await branchApi.getList({ pageSize: 100 })
  if (res.success) branches.value = res.data?.items || []
}

async function loadEmployees() {
  const res = await employeeApi.getList({ pageSize: 500 })
  if (res.success) employees.value = res.data?.items || []
}

async function loadParentCustomers() {
  const res = await customerApi.getList({ pageSize: 200, customerType: 1 })
  if (res.success) parentCustomers.value = res.data?.items || []
}

function handleSearch() {
  pageIndex.value = 1
  loadData()
}

function handleReset() {
  searchForm.keyword = ''
  searchForm.branchId = null
  searchForm.customerManagerName = ''
  handleSearch()
}

function handleEdit(row) {
  Object.assign(form, {
    id: row.id, name: row.name, customerType: row.customerType,
    phone: row.phone, address: row.address,
    customerManagerId: row.customerManagerId || null,
    parentCustomerId: row.parentCustomerId || null,
    legalPerson: row.legalPerson || '',
    registerAddress: row.registerAddress || '',
    branchId: row.branchId, remark: row.remark || '',
  })
  isEdit.value = true
  showDialog.value = true
}

async function handleSave() {
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return
  saving.value = true
  try {
    const res = isEdit.value
      ? await customerApi.update(form.id, form)
      : await customerApi.create(form)
    if (res.success) {
      ElMessage.success(isEdit.value ? '更新成功' : '创建成功')
      showDialog.value = false
      loadData()
    } else {
      ElMessage.error(res.message)
    }
  } finally {
    saving.value = false
  }
}

async function handleDelete(row) {
  await ElMessageBox.confirm(`确定删除客户"${row.name}"？`, '提示')
  const res = await customerApi.delete(row.id)
  if (res.success) {
    ElMessage.success('删除成功')
    loadData()
  }
}

onMounted(() => {
  loadBranches()
  loadEmployees()
  loadParentCustomers()
  loadData()
})
</script>

<style scoped>
.search-bar {
  padding-bottom: 16px;
  border-bottom: 1px solid #eee;
  margin-bottom: 16px;
}
.action-bar {
  margin-bottom: 16px;
}
.pagination {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
</style>