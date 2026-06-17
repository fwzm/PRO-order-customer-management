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
import { orderApi } from '@/api'

// ECharts 动态加载 — 仅 Dashboard 首屏渲染时按需加载，不进入首屏 bundle
let echartsModule = null
async function loadEcharts() {
  if (!echartsModule) {
    echartsModule = await import('echarts')
  }
  return echartsModule
}

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

const statusNameMap = {
  Draft: '草稿',
  Pending: '待分配',
  Assigned: '已分配',
  Delivering: '配送中',
  Completed: '已完成',
  Failed: '配送失败',
  Cancelled: '已取消',
}

async function initCharts(orders) {
  const echarts = await loadEcharts()

  // 订单趋势图 — 近7天
  if (orderTrendRef.value) {
    const trendChart = echarts.init(orderTrendRef.value)
    const days = []
    const orderCounts = []
    const revenues = []
    const now = new Date()

    for (let i = 6; i >= 0; i--) {
      const d = new Date(now)
      d.setDate(d.getDate() - i)
      const dateStr = d.toISOString().split('T')[0]
      const dayLabel = d.toLocaleDateString('zh-CN', { month: 'short', day: 'numeric' })
      days.push(dayLabel)

      if (orders && orders.length > 0) {
        const dayOrders = orders.filter(o => {
          const created = o.createdAt
          return created && (typeof created === 'string' ? created.startsWith(dateStr) : false)
        })
        orderCounts.push(dayOrders.length)
        const dayRevenue = dayOrders.reduce((sum, o) => sum + (o.totalAmount || 0), 0)
        revenues.push(Math.round(dayRevenue / 1000 * 10) / 10)
      } else {
        orderCounts.push(0)
        revenues.push(0)
      }
    }

    trendChart.setOption({
      tooltip: { trigger: 'axis' },
      xAxis: { type: 'category', data: days },
      yAxis: { type: 'value' },
      series: [
        {
          name: '订单数',
          type: 'line',
          smooth: true,
          data: orderCounts,
          areaStyle: { opacity: 0.3 },
          itemStyle: { color: '#409EFF' },
        },
        {
          name: '营收(千元)',
          type: 'bar',
          data: revenues,
          itemStyle: { color: '#67C23A' },
        },
      ],
      legend: { data: ['订单数', '营收(千元)'] },
    })
  }

  // 订单状态分布
  if (orderStatusRef.value) {
    const statusChart = echarts.init(orderStatusRef.value)
    const statusCounts = {
      '待处理': 0,
      '配送中': 0,
      '已完成': 0,
      '已取消': 0,
    }

    if (orders && orders.length > 0) {
      orders.forEach(o => {
        if (o.statusName === 'Pending' || o.statusName === 'Assigned') {
          statusCounts['待处理']++
        } else if (o.statusName === 'Delivering') {
          statusCounts['配送中']++
        } else if (o.statusName === 'Completed') {
          statusCounts['已完成']++
        } else if (o.statusName === 'Cancelled' || o.statusName === 'Failed') {
          statusCounts['已取消']++
        }
      })
    }

    statusChart.setOption({
      tooltip: { trigger: 'item' },
      series: [
        {
          type: 'pie',
          radius: ['40%', '70%'],
          data: Object.entries(statusCounts).map(([name, value]) => ({ value, name })),
          label: { show: true, formatter: '{b}: {c}' },
        },
      ],
    })
  }

  // 更新统计卡片
  if (orders && orders.length > 0) {
    const today = new Date().toISOString().split('T')[0]
    const todayOrders = orders.filter(o => {
      const created = o.createdAt
      return created && (typeof created === 'string' ? created.startsWith(today) : false)
    })
    const pendingOrders = orders.filter(o => o.statusName === 'Pending' || o.statusName === 'Assigned')
    const monthRevenue = orders.reduce((sum, o) => sum + (o.totalAmount || 0), 0)

    statCards.value[0].value = String(todayOrders.length)
    statCards.value[1].value = `¥${monthRevenue.toLocaleString()}`
    statCards.value[2].value = String(pendingOrders.length)
  }
}

onMounted(async () => {
  try {
    const res = await orderApi.getList({ pageSize: 500, keyword: '' })
    if (res.success) {
      const orders = res.data?.items || []
      recentOrders.value = orders.slice(0, 10)
      statCards.value[3].value = res.data?.totalCount ? String(res.data.totalCount) : '—'
      await nextTick()
      initCharts(orders)
    }
  } catch (e) {
    await nextTick()
    initCharts([])
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
