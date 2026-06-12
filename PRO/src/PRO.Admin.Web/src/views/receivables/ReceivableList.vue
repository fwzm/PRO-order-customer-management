<template>
  <div class="receivable-list">
    <el-card shadow="never">
      <el-form :inline="true" :model="searchForm" class="search-bar">
        <el-form-item label="关键词">
          <el-input v-model="searchForm.keyword" placeholder="订单号 / 客户名称 / 客户担当" clearable style="width: 260px" />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="handleSearch">查询</el-button>
          <el-button @click="handleReset">重置</el-button>
        </el-form-item>
      </el-form>

      <el-table :data="tableData" stripe v-loading="loading" style="width: 100%">
        <el-table-column prop="orderNo" label="订单号" width="180" show-overflow-tooltip>
          <template #default="{ row }">
            <el-link type="primary" @click="$router.push(`/orders/${row.id}`)">{{ row.orderNo }}</el-link>
          </template>
        </el-table-column>
        <el-table-column prop="customerName" label="客户名称" width="150" show-overflow-tooltip />
        <el-table-column prop="customerManagerName" label="客户担当" width="120" show-overflow-tooltip />
        <el-table-column label="总金额" width="130">
          <template #default="{ row }">¥{{ row.totalAmount?.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column label="已收金额" width="130">
          <template #default="{ row }">¥{{ row.receivedAmount?.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column label="未收金额" width="130">
          <template #default="{ row }">
            <span style="color: #E74C3C; font-weight: 600;">¥{{ (row.totalAmount - row.receivedAmount).toFixed(2) }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="paymentStatusName" label="收款状态" width="110">
          <template #default="{ row }">
            <el-tag :type="paymentStatusType(row.paymentStatus)">{{ row.paymentStatusName }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" width="175" />
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
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { receivableApi } from '@/api'

const tableData = ref([])
const loading = ref(false)
const pageIndex = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)

const searchForm = reactive({
  keyword: '',
})

function paymentStatusType(status) {
  return { 0: 'danger', 1: 'warning', 2: 'success', 3: 'info' }[status] || 'info'
}

async function loadData() {
  loading.value = true
  try {
    const params = { pageIndex: pageIndex.value, pageSize: pageSize.value }
    if (searchForm.keyword) params.keyword = searchForm.keyword
    const res = await receivableApi.getList(params)
    if (res.success) {
      tableData.value = res.data?.items || []
      totalCount.value = res.data?.totalCount || 0
    }
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  pageIndex.value = 1
  loadData()
}

function handleReset() {
  searchForm.keyword = ''
  handleSearch()
}

onMounted(() => {
  loadData()
})
</script>

<style scoped>
.search-bar {
  padding-bottom: 16px;
  border-bottom: 1px solid var(--el-border-color);
  margin-bottom: 16px;
}
.pagination {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
</style>