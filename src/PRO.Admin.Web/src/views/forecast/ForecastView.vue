<template>
  <div class="forecast-view">
    <el-row :gutter="20" class="stat-cards">
      <el-col :span="6">
        <el-card shadow="hover" class="stat-card">
          <div class="stat-label">本期实际销售额</div>
          <div class="stat-value">¥{{ actualTotal.toFixed(2) }}</div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover" class="stat-card">
          <div class="stat-label">本期预测销售额</div>
          <div class="stat-value">¥{{ forecastTotal.toFixed(2) }}</div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover" class="stat-card">
          <div class="stat-label">预测偏差</div>
          <div class="stat-value" :class="deviation >= 0 ? 'up' : 'down'">{{ deviation >= 0 ? '+' : '' }}{{ deviation }}%</div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover" class="stat-card">
          <div class="stat-label">预测准确率</div>
          <div class="stat-value">{{ accuracy }}%</div>
        </el-card>
      </el-col>
    </el-row>

    <el-card shadow="hover" class="chart-card">
      <template #header>
        <div class="card-header">
          <span>销售趋势预测</span>
          <div class="header-right">
            <el-radio-group v-model="forecastPeriod" size="small" @change="initChart">
              <el-radio-button :value="7">近7天</el-radio-button>
              <el-radio-button :value="14">近14天</el-radio-button>
              <el-radio-button :value="30">近30天</el-radio-button>
            </el-radio-group>
          </div>
        </div>
      </template>
      <div ref="forecastChartRef" style="height: 420px"></div>
    </el-card>

    <el-card shadow="hover" class="detail-table">
      <template #header>
        <span>预测明细</span>
      </template>
      <el-table :data="forecastDetails" stripe style="width: 100%">
        <el-table-column prop="date" label="日期" width="120" />
        <el-table-column prop="actualSales" label="实际销售额" width="140">
          <template #default="{ row }">¥{{ row.actualSales?.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column prop="forecastSales" label="预测销售额" width="140">
          <template #default="{ row }">¥{{ row.forecastSales?.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column prop="deviation" label="偏差" width="100">
          <template #default="{ row }">
            <span :class="row.deviation >= 0 ? 'up' : 'down'">{{ row.deviation >= 0 ? '+' : '' }}{{ row.deviation }}%</span>
          </template>
        </el-table-column>
        <el-table-column prop="accuracy" label="准确率" width="100">
          <template #default="{ row }">{{ row.accuracy }}%</template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, nextTick } from 'vue'
import * as echarts from 'echarts'

const forecastChartRef = ref(null)
const forecastPeriod = ref(7)

const actualTotal = computed(() => forecastDetails.value.reduce((s, r) => s + r.actualSales, 0))
const forecastTotal = computed(() => forecastDetails.value.reduce((s, r) => s + r.forecastSales, 0))
const deviation = computed(() => {
  if (forecastTotal.value === 0) return 0
  return Number(((forecastTotal.value - actualTotal.value) / forecastTotal.value * 100).toFixed(1))
})
const accuracy = computed(() => {
  const totalDev = forecastDetails.value.reduce((s, r) => s + Math.abs(r.deviation), 0)
  return forecastDetails.value.length ? Number((100 - totalDev / forecastDetails.value.length).toFixed(1)) : 100
})

const forecastDetails = ref([])

function generateMockData(days) {
  const data = []
  const now = new Date()
  for (let i = days - 1; i >= 0; i--) {
    const d = new Date(now)
    d.setDate(d.getDate() - i)
    const dateStr = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
    const base = 8000 + Math.random() * 12000
    const actual = base + (Math.random() - 0.5) * 3000
    const forecast = base + (Math.random() - 0.5) * 2000
    const dev = forecast !== 0 ? Number(((actual - forecast) / forecast * 100).toFixed(1)) : 0
    data.push({
      date: dateStr,
      actualSales: Number(actual.toFixed(2)),
      forecastSales: Number(forecast.toFixed(2)),
      deviation: dev,
      accuracy: Number((100 - Math.abs(dev)).toFixed(1)),
    })
  }
  return data
}

let chartInstance = null

function initChart() {
  forecastDetails.value = generateMockData(forecastPeriod.value)
  nextTick(() => {
    if (!forecastChartRef.value) return
    if (chartInstance) chartInstance.dispose()
    chartInstance = echarts.init(forecastChartRef.value)
    const dates = forecastDetails.value.map(d => d.date)
    const actualData = forecastDetails.value.map(d => d.actualSales)
    const forecastData = forecastDetails.value.map(d => d.forecastSales)
    chartInstance.setOption({
      tooltip: {
        trigger: 'axis',
        formatter: function (params) {
          let result = params[0].axisValue + '<br/>'
          params.forEach(p => {
            result += p.marker + ' ' + p.seriesName + '：¥' + Number(p.value).toFixed(2) + '<br/>'
          })
          return result
        },
      },
      legend: { data: ['实际销售额', '预测销售额'], top: 0 },
      grid: { left: 60, right: 30, bottom: 40, top: 40 },
      xAxis: { type: 'category', data: dates, axisLabel: { rotate: 30 } },
      yAxis: { type: 'value', axisLabel: { formatter: '¥{value}' } },
      series: [
        {
          name: '实际销售额',
          type: 'line',
          smooth: true,
          data: actualData,
          symbol: 'circle',
          symbolSize: 6,
          lineStyle: { width: 2, color: '#409EFF' },
          itemStyle: { color: '#409EFF' },
          areaStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: 'rgba(64, 158, 255, 0.25)' },
              { offset: 1, color: 'rgba(64, 158, 255, 0.02)' },
            ]),
          },
        },
        {
          name: '预测销售额',
          type: 'line',
          smooth: true,
          data: forecastData,
          symbol: 'diamond',
          symbolSize: 8,
          lineStyle: { width: 2, type: 'dashed', color: '#E6A23C' },
          itemStyle: { color: '#E6A23C' },
        },
      ],
    })
  })
}

onMounted(() => {
  initChart()
})
</script>

<style scoped>
.stat-cards {
  margin-bottom: 20px;
}
.stat-card {
  margin-bottom: 0;
}
.stat-label {
  font-size: 14px;
  color: #909399;
  margin-bottom: 8px;
}
.stat-value {
  font-size: 24px;
  font-weight: bold;
  color: #303133;
}
.stat-value.up {
  color: #F56C6C;
}
.stat-value.down {
  color: #67C23A;
}
.chart-card {
  margin-bottom: 20px;
}
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.detail-table {
  margin-bottom: 20px;
}
.up {
  color: #F56C6C;
}
.down {
  color: #67C23A;
}
</style>