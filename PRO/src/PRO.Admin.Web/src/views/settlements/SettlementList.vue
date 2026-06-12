<template>
  <div class="settlement-list">
    <el-card shadow="never">
      <div class="action-bar">
        <el-button type="primary" @click="showCreateDialog = true">
          <el-icon><Plus /></el-icon>创建结算单
        </el-button>
      </div>

      <el-table :data="tableData" stripe v-loading="loading" style="width: 100%">
        <el-table-column prop="settlementNo" label="结算单号" width="170" />
        <el-table-column prop="branchName" label="分公司" width="120" />
        <el-table-column prop="startDate" label="开始日期" width="110" />
        <el-table-column prop="endDate" label="结束日期" width="110" />
        <el-table-column prop="orderCount" label="订单数" width="70" />
        <el-table-column prop="totalAmount" label="总金额" width="120">
          <template #default="{ row }">¥{{ row.totalAmount?.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column prop="receivedAmount" label="已收" width="100">
          <template #default="{ row }">¥{{ row.receivedAmount?.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column prop="status" label="状态" width="80">
          <template #default="{ row }">
            <el-tag :type="row.status === 1 ? 'success' : 'info'">
              {{ row.status === 1 ? '已完成' : '草稿' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" width="170" />
        <el-table-column label="操作" width="150" fixed="right">
          <template #default="{ row }">
            <el-button text type="primary" @click="handleDownloadPdf(row)">下载PDF</el-button>
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

    <el-dialog v-model="showCreateDialog" title="创建结算单" width="500px">
      <el-form :model="settlementForm" label-width="100px">
        <el-form-item label="分公司">
          <el-select v-model="settlementForm.branchId" style="width: 100%">
            <el-option v-for="b in branches" :key="b.id" :label="b.name" :value="b.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="开始日期">
          <el-date-picker v-model="settlementForm.startDate" type="date" style="width: 100%" />
        </el-form-item>
        <el-form-item label="结束日期">
          <el-date-picker v-model="settlementForm.endDate" type="date" style="width: 100%" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showCreateDialog = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleCreate">创建</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { settlementApi, branchApi } from '@/api'

const tableData = ref([])
const branches = ref([])
const loading = ref(false)
const saving = ref(false)
const pageIndex = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)
const showCreateDialog = ref(false)
const settlementForm = reactive({ branchId: null, startDate: '', endDate: '' })

async function loadData() {
  loading.value = true
  try {
    const res = await settlementApi.getList({ pageIndex: pageIndex.value, pageSize: pageSize.value })
    if (res.success) { tableData.value = res.data?.items || []; totalCount.value = res.data?.totalCount || 0 }
  } finally { loading.value = false }
}

async function handleCreate() {
  saving.value = true
  try {
    const res = await settlementApi.create(settlementForm)
    if (res.success) { ElMessage.success('创建成功'); showCreateDialog.value = false; loadData() }
    else ElMessage.error(res.message)
  } finally { saving.value = false }
}

async function handleDownloadPdf(row) {
  try {
    const res = await settlementApi.downloadPdf(row.id)
    const url = window.URL.createObjectURL(new Blob([res]))
    const a = document.createElement('a')
    a.href = url; a.download = `${row.settlementNo}.pdf`; a.click()
    window.URL.revokeObjectURL(url)
  } catch { ElMessage.error('下载失败') }
}

onMounted(async () => {
  const bRes = await branchApi.getList({ pageSize: 100 })
  if (bRes.success) branches.value = bRes.data?.items || []
  loadData()
})
</script>

<style scoped>
.action-bar { margin-bottom: 16px; }
.pagination { margin-top: 16px; display: flex; justify-content: flex-end; }
</style>
