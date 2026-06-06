<template>
  <div class="dashboard">
    <!-- 统计卡片 -->
    <el-row :gutter="20" class="stat-cards">
      <el-col :span="6" v-for="card in statCards" :key="card.label">
        <el-card shadow="hover" class="stat-card">
          <div class="stat-card-inner">
            <div class="stat-info">
              <div class="stat-label">{{ card.label }}</div>
              <div class="stat-value">{{ card.value }}</div>
              <div class="stat-trend" :class="card.trend > 0 ? 'up' : 'down'">
                {{ card.trend > 0 ? '+' : '' }}{{ card.trend }}%
              </div>
            </div>
            <el-icon :size="48" :color="card.color">
              <component :is="card.icon" />
            </el-icon>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 图表 -->
    <el-row :gutter="20" class="chart-row">
      <el-col :span="16">
        <el-card shadow="hover">
          <template #header>
            <span>近7天订单趋势</span>
          </template>
          <div ref="orderTrendRef" style="height: 350px"></div>
        </el-card>
      </el-col>
      <el-col :span="8">
        <el-card shadow="hover">
          <template #header>
            <span>订单状态分布</span>
          </template>
          <div ref="orderStatusRef" style="height: 350px"></div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 最近订单 -->
    <el-card shadow="hover" class="recent-orders">
      <template #header>
        <div class="card-header">
          <span>最近订单</span>
          <el-button text type="primary" @click="$router.push('/orders')">查看全部</el-button>
        </div>
      </template>
      <el-table :data="recentOrders" stripe style="width: 100%">
        <el-table-column prop="orderNo" label="订单号" width="180" />
        <el-table-column prop="customerName" label="客户" width="150" />
        <el-table-column prop="totalAmount" label="金额" width="120">
          <template #default="{ row }">
            ¥{{ row.totalAmount?.toFixed(2) }}
          </template>
        </el-table-column>
        <el-table-column prop="statusName" label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="statusType(row.status)">{{ row.statusName }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="创建时间" />
      </el-table>
    </el-card>
  </div>
</template>

<script setup>
import { ref, onMounted, nextTick } from 'vue'
import * as echarts from 'echarts'
import { orderApi } from '@/api'

const statCards = ref([
  { label: '今日订单', value: '0', trend: 0, icon: 'ShoppingCart', color: '#409EFF' },
  { label: '本月营收', value: '¥0', trend: 0, icon: 'Money', color: '#67C23A' },
  { label: '待处理订单', value: '0', trend: 0, icon: 'Clock', color: '#E6A23C' },
  { label: '客户总数', value: '0', trend: 0, icon: 'UserFilled', color: '#F56C6C' },
])

const recentOrders = ref([])
const orderTrendRef = ref(null)
const orderStatusRef = ref(null)

function statusType(status) {
  const map = { 0: 'info', 1: 'warning', 2: 'primary', 3: 'success', 4: 'info', 5: 'danger', 6: 'info' }
  return map[status] || 'info'
}

function initCharts() {
  // 订单趋势图
  if (orderTrendRef.value) {
    const trendChart = echarts.init(orderTrendRef.value)
    trendChart.setOption({
      tooltip: { trigger: 'axis' },
      xAxis: { type: 'category', data: ['周一', '周二', '周三', '周四', '周五', '周六', '周日'] },
      yAxis: { type: 'value' },
      series: [
        {
          name: '订单数',
          type: 'line',
          smooth: true,
          data: [0, 0, 0, 0, 0, 0, 0],
          areaStyle: { opacity: 0.3 },
          itemStyle: { color: '#409EFF' },
        },
        {
          name: '营收(千元)',
          type: 'bar',
          data: [0, 0, 0, 0, 0, 0, 0],
          itemStyle: { color: '#67C23A' },
        },
      ],
      legend: { data: ['订单数', '营收(千元)'] },
    })
  }

  // 订单状态分布
  if (orderStatusRef.value) {
    const statusChart = echarts.init(orderStatusRef.value)
    statusChart.setOption({
      tooltip: { trigger: 'item' },
      series: [
        {
          type: 'pie',
          radius: ['40%', '70%'],
          data: [
            { value: 0, name: '待处理' },
            { value: 0, name: '配送中' },
            { value: 0, name: '已完成' },
            { value: 0, name: '已取消' },
          ],
          label: { show: true, formatter: '{b}: {c}' },
        },
      ],
    })
  }
}

onMounted(async () => {
  nextTick(initCharts)
  try {
    const res = await orderApi.getList({ pageSize: 10 })
    if (res.success) {
      recentOrders.value = res.data?.items || []
    }
  } catch (e) {
    // 忽略初始加载错误
  }
})
</script>

<style scoped>
.dashboard {
  max-width: 1400px;
  margin: 0 auto;
}
.stat-cards {
  margin-bottom: 20px;
}
.stat-card-inner {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.stat-label {
  font-size: 14px;
  color: #909399;
  margin-bottom: 8px;
}
.stat-value {
  font-size: 28px;
  font-weight: bold;
  color: #303133;
  margin-bottom: 4px;
}
.stat-trend {
  font-size: 12px;
}
.stat-trend.up { color: #67C23A; }
.stat-trend.down { color: #F56C6C; }
.chart-row {
  margin-bottom: 20px;
}
.recent-orders {
  margin-bottom: 20px;
}
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
</style>
