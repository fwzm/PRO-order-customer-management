<template>
  <div class="operation-logs">
    <el-card shadow="never">
      <el-form :inline="true" class="search-bar">
        <el-form-item label="模块">
          <el-select placeholder="全部" clearable style="width: 120px">
            <el-option label="客户" value="客户" />
            <el-option label="订单" value="订单" />
            <el-option label="系统" value="系统" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary">查询</el-button>
        </el-form-item>
      </el-form>

      <el-table :data="logs" stripe v-loading="loading" style="width: 100%">
        <el-table-column prop="operatorName" label="操作人" width="120" />
        <el-table-column prop="module" label="模块" width="80" />
        <el-table-column prop="operationType" label="类型" width="80" />
        <el-table-column prop="content" label="操作内容" min-width="300" show-overflow-tooltip />
        <el-table-column prop="operatedAt" label="时间" width="170" />
      </el-table>

      <div class="pagination">
        <el-pagination
          v-model:current-page="pageIndex" v-model:page-size="pageSize"
          :total="totalCount" layout="total, prev, pager, next"
          @current-change="loadData"
        />
      </div>
    </el-card>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { systemApi } from '@/api'

const logs = ref([])
const loading = ref(false)
const pageIndex = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)

async function loadData() {
  loading.value = true
  try {
    const res = await systemApi.getLogs({ pageIndex: pageIndex.value, pageSize: pageSize.value })
    if (res.success) { logs.value = res.data?.items || []; totalCount.value = res.data?.totalCount || 0 }
  } finally { loading.value = false }
}

onMounted(() => loadData())
</script>

<style scoped>
.search-bar { padding-bottom: 16px; border-bottom: 1px solid var(--el-border-color); margin-bottom: 16px; }
.pagination { margin-top: 16px; display: flex; justify-content: flex-end; }
</style>
