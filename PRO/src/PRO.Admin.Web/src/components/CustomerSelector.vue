<template>
  <div class="customer-selector">
    <el-input
      v-model="searchKeyword"
      :placeholder="placeholder"
      clearable
      @input="handleSearchInput"
      @focus="showPopover = true"
    >
      <template #append>
        <el-button @click="showDialog = true">
          <el-icon><Search /></el-icon>
        </el-button>
      </template>
    </el-input>

    <el-dialog v-model="showDialog" title="选择客户" width="760px" top="8vh">
      <div class="selector-search">
        <el-input v-model="keyword" placeholder="搜索客户名称/编号/电话" clearable @input="handleSearch" style="width: 300px" />
        <el-button type="primary" @click="showNewCustomer = true">
          <el-icon><Plus /></el-icon>新建客户
        </el-button>
      </div>
      <el-table :data="customerList" v-loading="loading" stripe @row-click="handleSelectRow" height="400">
        <el-table-column prop="name" label="客户名称" min-width="140">
          <template #default="{ row }">
            <el-link type="primary" @click.stop="openDetail(row)">{{ row.name }}</el-link>
          </template>
        </el-table-column>
        <el-table-column prop="customerNo" label="客户编号" width="120" />
        <el-table-column prop="customerManagerName" label="客户担当" width="100">
          <template #default="{ row }">{{ row.customerManagerName || '未分配' }}</template>
        </el-table-column>
        <el-table-column prop="phone" label="电话" width="120" />
        <el-table-column label="操作" width="60" fixed="right">
          <template #default="{ row }">
            <el-button text type="primary" size="small" @click.stop="openDetail(row)">
              <el-icon><View /></el-icon>
            </el-button>
          </template>
        </el-table-column>
      </el-table>
      <div class="selector-pagination">
        <el-pagination
          v-model:current-page="pageIndex" v-model:page-size="pageSize"
          :total="totalCount" layout="total, prev, pager, next" small
          @current-change="loadCustomers"
        />
      </div>
      <template #footer>
        <el-button @click="showDialog = false">取消</el-button>
        <el-button type="primary" :disabled="!selectedCustomer" @click="confirmSelect">确 定</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="showNewCustomer" title="新建客户" width="600px" append-to-body>
      <el-form :model="newForm" label-width="100px">
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="客户名称" required>
              <el-input v-model="newForm.name" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="客户类型">
              <el-select v-model="newForm.customerType" style="width: 100%">
                <el-option label="大客户" :value="1" />
                <el-option label="细分客户" :value="2" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="电话">
          <el-input v-model="newForm.phone" />
        </el-form-item>
        <el-form-item label="地址">
          <el-input v-model="newForm.address" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showNewCustomer = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleNewCustomer">保存并选择</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { customerApi } from '@/api'
import { useRouter } from 'vue-router'

const props = defineProps({
  modelValue: { type: [Number, String], default: null },
  placeholder: { type: String, default: '搜索选择客户' },
})

const emit = defineEmits(['update:modelValue', 'change'])

const router = useRouter()
const showDialog = ref(false)
const showNewCustomer = ref(false)
const loading = ref(false)
const saving = ref(false)
const keyword = ref('')
const searchKeyword = ref('')
const customerList = ref([])
const pageIndex = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)
const selectedCustomer = ref(null)
const showPopover = ref(false)

const newForm = ref({
  name: '', customerType: 2, phone: '', address: '',
})

const displayLabel = ref('')

async function loadCustomers() {
  loading.value = true
  try {
    const res = await customerApi.getList({ pageIndex: pageIndex.value, pageSize: pageSize.value, keyword: keyword.value })
    if (res.success) {
      customerList.value = res.data?.items || []
      totalCount.value = res.data?.totalCount || 0
    }
  } finally { loading.value = false }
}

function handleSearchInput(val) {
  if (!val) {
    keyword.value = ''
    loadCustomers()
  }
}

function handleSearch() {
  pageIndex.value = 1
  showDialog.value = true
  loadCustomers()
}

function handleSelectRow(row) {
  selectedCustomer.value = row
}

function confirmSelect() {
  if (selectedCustomer.value) {
    emit('update:modelValue', selectedCustomer.value.id)
    emit('change', selectedCustomer.value)
    displayLabel.value = selectedCustomer.value.name
    searchKeyword.value = selectedCustomer.value.name
    showDialog.value = false
    selectedCustomer.value = null
  }
}

function openDetail(row) {
  const route = router.resolve({ path: `/customers/${row.id}` })
  window.open(route.href, '_blank')
}

async function handleNewCustomer() {
  if (!newForm.value.name) {
    ElMessage.warning('请输入客户名称')
    return
  }
  saving.value = true
  try {
    const res = await customerApi.create(newForm.value)
    if (res.success) {
      ElMessage.success('创建成功')
      showNewCustomer.value = false
      emit('update:modelValue', res.data.id)
      emit('change', { id: res.data.id, name: newForm.value.name })
      searchKeyword.value = newForm.value.name
      newForm.value = { name: '', customerType: 2, phone: '', address: '' }
      loadCustomers()
    }
  } finally { saving.value = false }
}

watch(() => props.modelValue, (val) => {
  if (!val) {
    searchKeyword.value = ''
    displayLabel.value = ''
  }
})
</script>

<style scoped>
.customer-selector {
  width: 100%;
}
.selector-search {
  display: flex;
  gap: 12px;
  align-items: center;
  margin-bottom: 16px;
}
.selector-pagination {
  margin-top: 12px;
  display: flex;
  justify-content: flex-end;
}
</style>