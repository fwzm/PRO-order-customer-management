<template>
  <div class="order-list">
    <el-card shadow="never">
      <el-form :inline="true" :model="searchForm" class="search-bar">
        <el-form-item label="关键词">
          <el-input v-model="searchForm.keyword" placeholder="订单号/客户" clearable />
        </el-form-item>
        <el-form-item label="状态" v-if="!isDraftView">
          <el-select v-model="searchForm.status" placeholder="全部" clearable style="width: 130px">
            <el-option label="待处理" :value="1" />
            <el-option label="已分配" :value="2" />
            <el-option label="配送中" :value="3" />
            <el-option label="已完成" :value="4" />
            <el-option label="已取消" :value="5" />
          </el-select>
        </el-form-item>
        <el-form-item label="付款" v-if="!isDraftView">
          <el-select v-model="searchForm.paymentStatus" placeholder="全部" clearable style="width: 130px">
            <el-option label="未付款" :value="0" />
            <el-option label="部分付款" :value="1" />
            <el-option label="已付款" :value="2" />
            <el-option label="挂账" :value="3" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="handleSearch">查询</el-button>
          <el-button @click="handleReset">重置</el-button>
        </el-form-item>
      </el-form>

      <div class="action-bar">
        <el-button type="primary" @click="$router.push('/orders/new')">
          <el-icon><Plus /></el-icon>新增订单
        </el-button>
      </div>

      <el-table :data="tableData" stripe v-loading="loading" style="width: 100%">
        <el-table-column prop="orderNo" label="订单号" width="170">
          <template #default="{ row }">
            <el-link type="primary" @click="$router.push(`/orders/${row.id}`)">{{ row.orderNo }}</el-link>
          </template>
        </el-table-column>
        <el-table-column prop="customerName" label="客户" min-width="120" />
        <el-table-column prop="totalAmount" label="金额" width="120">
          <template #default="{ row }">¥{{ row.totalAmount?.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column prop="receivedAmount" label="已收" width="100">
          <template #default="{ row }">¥{{ row.receivedAmount?.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column prop="statusName" label="状态" width="90">
          <template #default="{ row }">
            <el-tag :type="statusType(row.status)">{{ row.statusName }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="paymentStatusName" label="付款" width="90" />
        <el-table-column prop="deliveryPersonName" label="配送员" width="100" />
        <el-table-column prop="customerManagerName" label="客户担当" width="100" />
        <el-table-column prop="createdAt" label="创建时间" width="170" />
        <el-table-column label="操作" width="180" fixed="right">
          <template #default="{ row }">
            <el-button text type="primary" @click="$router.push(`/orders/${row.id}`)">详情</el-button>
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
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { orderApi } from '@/api'

const route = useRoute()
const tableData = ref([])
const loading = ref(false)
const pageIndex = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)

const isDraftView = computed(() => route.path === '/orders/draft')

const searchForm = reactive({ keyword: '', status: null, paymentStatus: null })

function statusType(s) {
  return { 0: 'info', 1: 'warning', 2: 'primary', 3: '', 4: 'success', 5: 'danger', 6: 'info' }[s] || 'info'
}

async function loadData() {
  loading.value = true
  try {
    const params = { pageIndex: pageIndex.value, pageSize: pageSize.value, ...searchForm }
    if (isDraftView.value) params.status = 0
    const res = await orderApi.getList(params)
    if (res.success) {
      let items = res.data?.items || []
      if (isDraftView.value) items = items.filter(o => o.status === 0 || o.isDraft)
      tableData.value = items
      totalCount.value = res.data?.totalCount || 0
    }
  } finally { loading.value = false }
}

function handleSearch() { pageIndex.value = 1; loadData() }
function handleReset() { searchForm.keyword = ''; searchForm.status = null; searchForm.paymentStatus = null; handleSearch() }

function handleEdit(row) {
  ElMessage.info('编辑功能跳转到订单详情')
}

async function handleDelete(row) {
  await ElMessageBox.confirm(`确定删除订单"${row.orderNo}"？`, '提示')
  const res = await orderApi.delete(row.id)
  if (res.success) { ElMessage.success('删除成功'); loadData() }
}

onMounted(() => {
  loadData()
})
</script>

<style scoped>
.search-bar { padding-bottom: 16px; border-bottom: 1px solid #eee; margin-bottom: 16px; }
.action-bar { margin-bottom: 16px; }
.pagination { margin-top: 16px; display: flex; justify-content: flex-end; }
</style>