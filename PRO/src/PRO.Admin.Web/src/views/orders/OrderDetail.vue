<template>
  <div class="order-detail">
    <el-card v-if="order" shadow="never">
      <template #header>
        <div class="detail-header">
          <span>订单详情 - {{ order.orderNo }}</span>
          <div>
            <el-button @click="showEdit = true" v-if="order.isDraft">确认订单</el-button>
            <el-button @click="$router.back()">返回</el-button>
          </div>
        </div>
      </template>

      <el-descriptions :column="3" border>
        <el-descriptions-item label="订单号">{{ order.orderNo }}</el-descriptions-item>
        <el-descriptions-item label="客户">{{ order.customerName }}</el-descriptions-item>
        <el-descriptions-item label="状态">
          <el-tag :type="statusType(order.status)">{{ order.statusName }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="总金额">¥{{ order.totalAmount?.toFixed(2) }}</el-descriptions-item>
        <el-descriptions-item label="已收金额">¥{{ order.receivedAmount?.toFixed(2) }}</el-descriptions-item>
        <el-descriptions-item label="付款状态">{{ order.paymentStatusName }}</el-descriptions-item>
        <el-descriptions-item label="配送员">{{ order.deliveryPersonName || '未分配' }}</el-descriptions-item>
        <el-descriptions-item label="配送地址" :span="2">{{ order.deliveryAddress }}</el-descriptions-item>
        <el-descriptions-item label="创建人">{{ order.createdByName }}</el-descriptions-item>
        <el-descriptions-item label="创建时间">{{ order.createdAt }}</el-descriptions-item>
        <el-descriptions-item label="更新时间">{{ order.updatedAt }}</el-descriptions-item>
      </el-descriptions>
    </el-card>

    <el-card shadow="never" class="section">
      <template #header><span>订单明细</span></template>
      <el-table :data="order?.items || []" stripe>
        <el-table-column prop="productName" label="产品" />
        <el-table-column prop="productSku" label="SKU" width="120" />
        <el-table-column prop="quantity" label="数量" width="80" />
        <el-table-column prop="unitPrice" label="单价" width="120">
          <template #default="{ row }">¥{{ row.unitPrice?.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column prop="amount" label="小计" width="120">
          <template #default="{ row }">¥{{ row.amount?.toFixed(2) }}</template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-card shadow="never" class="section">
      <template #header><span>操作记录</span></template>
      <el-timeline>
        <el-timeline-item
          v-for="r in order?.modificationRecords || []"
          :key="r.id"
          :timestamp="r.modifiedAt"
          placement="top"
        >
          <div>{{ r.content }}</div>
          <div class="timeline-operator">操作人：{{ r.modifiedByName }}</div>
        </el-timeline-item>
      </el-timeline>
    </el-card>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { orderApi } from '@/api'

const route = useRoute()
const order = ref(null)
const showEdit = ref(false)

function statusType(s) {
  return { 0: 'info', 1: 'warning', 2: 'primary', 3: '', 4: 'success', 5: 'danger' }[s] || 'info'
}

onMounted(async () => {
  const res = await orderApi.getById(route.params.id)
  if (res.success) order.value = res.data
})
</script>

<style scoped>
.detail-header { display: flex; justify-content: space-between; align-items: center; }
.section { margin-top: 16px; }
.timeline-operator { font-size: 12px; color: #909399; }
</style>
