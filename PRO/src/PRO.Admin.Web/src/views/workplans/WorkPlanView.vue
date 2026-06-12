<template>
  <div>
    <el-tabs v-model="activeTab">
      <el-tab-pane label="排班管理" name="schedule">
        <el-card shadow="never">
          <div class="action-bar">
            <el-button type="primary" @click="showScheduleDialog = true">
              <el-icon><Plus /></el-icon>新建排班
            </el-button>
          </div>
          <el-form :inline="true" :model="scheduleSearch" class="search-bar">
            <el-form-item label="员工">
              <el-select v-model="scheduleSearch.employeeId" filterable placeholder="选择员工" clearable style="width: 200px">
                <el-option v-for="e in employees" :key="e.id" :label="e.name" :value="e.id" />
              </el-select>
            </el-form-item>
            <el-form-item label="日期">
              <el-date-picker v-model="scheduleSearch.dateRange" type="daterange" range-separator="至" start-placeholder="开始日期" end-placeholder="结束日期" style="width: 240px" />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" @click="loadSchedules">查询</el-button>
              <el-button @click="scheduleSearch.employeeId = null; scheduleSearch.dateRange = null; loadSchedules()">重置</el-button>
            </el-form-item>
          </el-form>
          <el-table :data="schedules" stripe>
            <el-table-column prop="employeeName" label="员工" width="100" />
            <el-table-column prop="scheduleDate" label="日期" width="120" />
            <el-table-column prop="workStartTime" label="上班时间" width="100" />
            <el-table-column prop="workEndTime" label="下班时间" width="100" />
            <el-table-column label="工作时长" width="100">
              <template #default="{ row }">
                {{ row.totalWorkMinutes ? `${Math.floor(row.totalWorkMinutes / 60)}小时${row.totalWorkMinutes % 60}分` : '-' }}
              </template>
            </el-table-column>
            <el-table-column prop="scheduleType" label="排班类型" width="100" />
            <el-table-column label="操作" width="100">
              <template #default="{ row }">
                <el-button text type="danger" @click="handleDeleteSchedule(row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-tab-pane>
      <el-tab-pane label="工作计划" name="plan">
        <el-card shadow="never">
          <div class="action-bar">
            <el-button type="primary" @click="showPlanDialog = true">
              <el-icon><Plus /></el-icon>新增计划
            </el-button>
            <el-date-picker v-model="planDate" type="date" placeholder="选择日期" @change="loadPlans" style="width: 160px" />
          </div>
          <el-table :data="plans" stripe>
            <el-table-column prop="employeeName" label="员工" width="100" />
            <el-table-column prop="planDate" label="日期" width="120" />
            <el-table-column prop="startTime" label="开始" width="90" />
            <el-table-column prop="endTime" label="结束" width="90" />
            <el-table-column prop="content" label="内容" min-width="240" show-overflow-tooltip />
            <el-table-column label="类型" width="80">
              <template #default="{ row }">
                <el-tag size="small">{{ row.planType === 'Work' ? '工作' : row.planType === 'Meeting' ? '会议' : '其他' }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="执行状态" width="100">
              <template #default="{ row }">
                <el-tag :type="row.executionStatus === 1 ? 'success' : row.executionStatus === 2 ? 'danger' : 'info'" size="small">
                  {{ row.executionStatus === 1 ? '已完成' : row.executionStatus === 2 ? '未完成' : '待执行' }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="100">
              <template #default="{ row }">
                <el-button text type="danger" @click="handleDeletePlan(row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="showScheduleDialog" title="新建排班" width="550px">
      <el-form :model="scheduleForm" label-width="100px">
        <el-form-item label="员工">
          <el-select v-model="scheduleForm.employeeId" filterable style="width: 100%">
            <el-option v-for="e in employees" :key="e.id" :label="e.name" :value="e.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="日期">
          <el-date-picker v-model="scheduleForm.scheduleDate" type="date" style="width: 100%" />
        </el-form-item>
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="上班时间">
              <el-time-picker v-model="scheduleForm.workStartTime" style="width: 100%" format="HH:mm" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="下班时间">
              <el-time-picker v-model="scheduleForm.workEndTime" style="width: 100%" format="HH:mm" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="排班类型">
          <el-select v-model="scheduleForm.scheduleType" style="width: 100%">
            <el-option label="早班" value="早班" />
            <el-option label="中班" value="中班" />
            <el-option label="晚班" value="晚班" />
            <el-option label="全天" value="全天" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showScheduleDialog = false">取消</el-button>
        <el-button type="primary" :loading="savingSchedule" @click="handleSaveSchedule">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="showPlanDialog" title="新增工作计划" width="600px">
      <el-form :model="planForm" label-width="100px">
        <el-form-item label="员工">
          <el-select v-model="planForm.employeeId" filterable style="width: 100%">
            <el-option v-for="e in employees" :key="e.id" :label="e.name" :value="e.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="日期">
          <el-date-picker v-model="planForm.planDate" type="date" style="width: 100%" />
        </el-form-item>
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="开始时间">
              <el-time-picker v-model="planForm.startTime" style="width: 100%" format="HH:mm" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="结束时间">
              <el-time-picker v-model="planForm.endTime" style="width: 100%" format="HH:mm" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="计划类型">
          <el-select v-model="planForm.planType" style="width: 100%">
            <el-option label="工作" value="Work" />
            <el-option label="会议" value="Meeting" />
            <el-option label="其他" value="Other" />
          </el-select>
        </el-form-item>
        <el-form-item label="内容">
          <el-input v-model="planForm.content" type="textarea" :rows="3" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showPlanDialog = false">取消</el-button>
        <el-button type="primary" :loading="savingPlan" @click="handleSavePlan">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { workPlanApi, employeeApi } from '@/api'

const activeTab = ref('schedule')
const schedules = ref([])
const plans = ref([])
const employees = ref([])
const savingSchedule = ref(false)
const savingPlan = ref(false)
const showScheduleDialog = ref(false)
const showPlanDialog = ref(false)
const planDate = ref(null)
const scheduleSearch = reactive({ employeeId: null, dateRange: null })

const scheduleForm = reactive({
  employeeId: null, scheduleDate: '', workStartTime: '', workEndTime: '', scheduleType: '早班',
})

const planForm = reactive({
  employeeId: null, planDate: '', startTime: '', endTime: '', content: '', planType: 'Work',
})

async function loadSchedules() {
  const params = { pageSize: 200 }
  if (scheduleSearch.employeeId) params.employeeId = scheduleSearch.employeeId
  const res = await workPlanApi.getSchedules(params)
  if (res.success) schedules.value = res.data?.items || []
}

async function loadPlans() {
  const params = { pageSize: 200 }
  if (planDate.value) params.planDate = planDate.value
  const res = await workPlanApi.getPlans(params)
  if (res.success) plans.value = res.data?.items || []
}

async function handleSaveSchedule() {
  if (!scheduleForm.employeeId || !scheduleForm.scheduleDate) {
    ElMessage.warning('请填写完整信息')
    return
  }
  savingSchedule.value = true
  try {
    const res = await workPlanApi.createSchedule(scheduleForm)
    if (res.success) {
      ElMessage.success('创建成功')
      showScheduleDialog.value = false
      scheduleForm.employeeId = null
      scheduleForm.scheduleDate = ''
      scheduleForm.workStartTime = ''
      scheduleForm.workEndTime = ''
      scheduleForm.scheduleType = '早班'
      loadSchedules()
    } else {
      ElMessage.error(res.message)
    }
  } finally { savingSchedule.value = false }
}

async function handleSavePlan() {
  if (!planForm.employeeId || !planForm.planDate || !planForm.content) {
    ElMessage.warning('请填写完整信息')
    return
  }
  savingPlan.value = true
  try {
    const res = await workPlanApi.createPlan(planForm)
    if (res.success) {
      ElMessage.success('创建成功')
      showPlanDialog.value = false
      planForm.employeeId = null
      planForm.planDate = ''
      planForm.startTime = ''
      planForm.endTime = ''
      planForm.content = ''
      planForm.planType = 'Work'
      loadPlans()
    } else {
      ElMessage.error(res.message)
    }
  } finally { savingPlan.value = false }
}

function handleDeleteSchedule(row) {
  ElMessage.info('删除功能需后端支持')
}

function handleDeletePlan(row) {
  ElMessage.info('删除功能需后端支持')
}

onMounted(async () => {
  const eRes = await employeeApi.getList({ pageSize: 500 })
  if (eRes.success) employees.value = eRes.data?.items || []
  loadSchedules()
  loadPlans()
})
</script>

<style scoped>
.action-bar {
  margin-bottom: 16px;
  display: flex;
  gap: 12px;
  align-items: center;
}
.search-bar {
  padding-bottom: 16px;
  border-bottom: 1px solid #eee;
  margin-bottom: 16px;
}
</style>