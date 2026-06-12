<template>
  <div class="report-center">
    <el-row :gutter="20" class="chart-row">
      <el-col :span="12">
        <el-card shadow="hover">
          <template #header>
            <div class="card-header">
              <span>订单统计</span>
              <el-select v-model="orderStatPeriod" size="small" style="width: 120px">
                <el-option label="本月" :value="1" />
                <el-option label="本季度" :value="2" />
                <el-option label="本年" :value="3" />
              </el-select>
            </div>
          </template>
          <div ref="orderChartRef" style="height: 350px"></div>
        </el-card>
      </el-col>
      <el-col :span="12">
        <el-card shadow="hover">
          <template #header>
            <div class="card-header">
              <span>营收趋势</span>
              <el-select v-model="revenuePeriod" size="small" style="width: 120px">
                <el-option label="近7天" :value="7" />
                <el-option label="近30天" :value="30" />
                <el-option label="近90天" :value="90" />
              </el-select>
            </div>
          </template>
          <div ref="revenueChartRef" style="height: 350px"></div>
        </el-card>
      </el-col>
    </el-row>

    <el-card shadow="hover" class="summary-table">
      <template #header>
        <span>数据汇总</span>
      </template>
      <el-table :data="summaryData" stripe style="width: 100%">
        <el-table-column prop="month" label="月份" width="100" />
        <el-table-column prop="orderCount" label="订单数量" width="100" />
        <el-table-column prop="revenue" label="营收(元)" width="140">
          <template #default="{ row }">¥{{ row.revenue?.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column prop="cost" label="成本(元)" width="140">
          <template #default="{ row }">¥{{ row.cost?.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column prop="profit" label="利润(元)" width="140">
          <template #default="{ row }">
            <span :style="{ color: row.profit >= 0 ? '#67C23A' : '#F56C6C' }">
              ¥{{ row.profit?.toFixed(2) }}
            </span>
          </template>
        </el-table-column>
        <el-table-column prop="profitRate" label="利润率" width="100">
          <template #default="{ row }">{{ row.profitRate }}%</template>
        </el-table-column>
        <el-table-column prop="avgOrderValue" label="客单价(元)" width="120">
          <template #default="{ row }">¥{{ row.avgOrderValue?.toFixed(2) }}</template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<script setup>
import { ref, onMounted, watch, nextTick } from 'vue'
import * as echarts from 'echarts'

const orderChartRef = ref(null)
const revenueChartRef = ref(null)
const orderStatPeriod = ref(1)
const revenuePeriod = ref(7)
const summaryData = ref([])

let orderChartInstance = null
let revenueChartInstance = null

const orderCategories = ['1月', '2月', '3月', '4月', '5月', '6月', '7月', '8月', '9月', '10月', '11月', '12月']
const orderStatData = [120, 200, 150, 80, 70, 110, 130, 90, 160, 180, 140, 210]
const revenueCategories = ['第1天', '第2天', '第3天', '第4天', '第5天', '第6天', '第7天']
const revenueTrendData = [12000, 18000, 15000, 22000, 19000, 26000, 21000]

function initOrderChart() {
  if (!orderChartRef.value) return
  if (orderChartInstance) orderChartInstance.dispose()
  orderChartInstance = echarts.init(orderChartRef.value)
  orderChartInstance.setOption({
    tooltip: { trigger: 'axis' },
    xAxis: { type: 'category', data: orderCategories, axisLabel: { rotate: 30 } },
    yAxis: { type: 'value' },
    grid: { left: 50, right: 20, bottom: 60 },
    series: [
      {
        name: '订单数',
        type: 'bar',
        data: orderStatData,
        itemStyle: {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: '#409EFF' },
            { offset: 1, color: '#79bbff' },
          ]),
          borderRadius: [4, 4, 0, 0],
        },
        barWidth: 20,
      },
    ],
  })
}

function initRevenueChart() {
  if (!revenueChartRef.value) return
  if (revenueChartInstance) revenueChartInstance.dispose()
  revenueChartInstance = echarts.init(revenueChartRef.value)
  revenueChartInstance.setOption({
    tooltip: { trigger: 'axis' },
    xAxis: { type: 'category', data: revenueCategories },
    yAxis: { type: 'value', axisLabel: { formatter: '¥{value}' } },
    grid: { left: 60, right: 20, bottom: 30 },
    series: [
      {
        name: '营收',
        type: 'line',
        smooth: true,
        data: revenueTrendData,
        symbol: 'circle',
        symbolSize: 8,
        lineStyle: { width: 3, color: '#67C23A' },
        itemStyle: { color: '#67C23A' },
        areaStyle: {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: 'rgba(103, 194, 58, 0.3)' },
            { offset: 1, color: 'rgba(103, 194, 58, 0.05)' },
          ]),
        },
      },
    ],
  })
}

function initCharts() {
  nextTick(() => {
    initOrderChart()
    initRevenueChart()
  })
}

watch(orderStatPeriod, () => {
  nextTick(initOrderChart)
})

watch(revenuePeriod, () => {
  nextTick(initRevenueChart)
})

onMounted(() => {
  summaryData.value = [
    { month: '1月', orderCount: 120, revenue: 120000, cost: 84000, profit: 36000, profitRate: 30, avgOrderValue: 1000 },
    { month: '2月', orderCount: 200, revenue: 200000, cost: 140000, profit: 60000, profitRate: 30, avgOrderValue: 1000 },
    { month: '3月', orderCount: 150, revenue: 150000, cost: 105000, profit: 45000, profitRate: 30, avgOrderValue: 1000 },
    { month: '4月', orderCount: 80, revenue: 80000, cost: 56000, profit: 24000, profitRate: 30, avgOrderValue: 1000 },
    { month: '5月', orderCount: 70, revenue: 70000, cost: 49000, profit: 21000, profitRate: 30, avgOrderValue: 1000 },
    { month: '6月', orderCount: 110, revenue: 110000, cost: 77000, profit: 33000, profitRate: 30, avgOrderValue: 1000 },
    { month: '7月', orderCount: 130, revenue: 130000, cost: 91000, profit: 39000, profitRate: 30, avgOrderValue: 1000 },
    { month: '8月', orderCount: 90, revenue: 90000, cost: 63000, profit: 27000, profitRate: 30, avgOrderValue: 1000 },
    { month: '9月', orderCount: 160, revenue: 160000, cost: 112000, profit: 48000, profitRate: 30, avgOrderValue: 1000 },
    { month: '10月', orderCount: 180, revenue: 180000, cost: 126000, profit: 54000, profitRate: 30, avgOrderValue: 1000 },
    { month: '11月', orderCount: 140, revenue: 140000, cost: 98000, profit: 42000, profitRate: 30, avgOrderValue: 1000 },
    { month: '12月', orderCount: 210, revenue: 210000, cost: 147000, profit: 63000, profitRate: 30, avgOrderValue: 1000 },
  ]
  initCharts()
})
</script>

<style scoped>
.chart-row {
  margin-bottom: 20px;
}
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.summary-table {
  margin-bottom: 20px;
}
</style>