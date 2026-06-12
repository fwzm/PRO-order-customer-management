<template>
  <div class="visit-list">
    <el-card shadow="never">
      <el-form :inline="true" :model="searchForm" class="search-bar">
        <el-form-item label="客户名称">
          <el-input v-model="searchForm.customerName" placeholder="请输入" clearable />
        </el-form-item>
        <el-form-item label="拜访人">
          <el-input v-model="searchForm.visitor" placeholder="请输入" clearable />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="handleSearch">查询</el-button>
          <el-button @click="handleReset">重置</el-button>
        </el-form-item>
      </el-form>

      <div class="action-bar">
        <el-button type="primary" @click="showAddDialog = true">
          <el-icon><Plus /></el-icon>新增拜访
        </el-button>
      </div>

      <el-table :data="visitList" stripe style="width: 100%">
        <el-table-column prop="customerName" label="客户名称" min-width="140" />
        <el-table-column prop="visitor" label="拜访人(客户担当)" width="140" />
        <el-table-column prop="visitTime" label="拜访时间" width="170" />
        <el-table-column prop="content" label="内容" min-width="240" show-overflow-tooltip />
        <el-table-column prop="createdByName" label="创建人" width="100" />
        <el-table-column prop="createdAt" label="创建时间" width="170" />
        <el-table-column label="操作" width="120" fixed="right">
          <template #default="{ row }">
            <el-button text type="primary" @click="handleViewDetail(row)">详情</el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-empty v-if="!visitList.length" description="暂无拜访记录" />
    </el-card>

    <el-dialog v-model="showAddDialog" title="新增拜访记录" width="600px">
      <el-form :model="addForm" label-width="110px">
        <el-form-item label="客户名称">
          <el-input v-model="addForm.customerName" />
        </el-form-item>
        <el-form-item label="拜访人(客户担当)">
          <el-input v-model="addForm.visitor" />
        </el-form-item>
        <el-form-item label="拜访时间">
          <el-date-picker v-model="addForm.visitTime" type="datetime" placeholder="选择日期时间" style="width: 100%" />
        </el-form-item>
        <el-form-item label="内容">
          <el-input v-model="addForm.content" type="textarea" :rows="4" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showAddDialog = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>

    <el-drawer v-model="showDetail" title="拜访详情" size="500px">
      <template v-if="detailData">
        <el-descriptions :column="1" border style="margin-bottom: 20px;">
          <el-descriptions-item label="客户名称">{{ detailData.customerName }}</el-descriptions-item>
          <el-descriptions-item label="拜访人">{{ detailData.visitor }}</el-descriptions-item>
          <el-descriptions-item label="拜访时间">{{ detailData.visitTime }}</el-descriptions-item>
          <el-descriptions-item label="创建人">{{ detailData.createdByName }}</el-descriptions-item>
          <el-descriptions-item label="创建时间">{{ detailData.createdAt }}</el-descriptions-item>
        </el-descriptions>
        <el-card shadow="never">
          <template #header><span>拜访内容</span></template>
          <div class="detail-content">{{ detailData.content }}</div>
        </el-card>
        <el-card shadow="never" class="section">
          <template #header><span>时间线</span></template>
          <el-timeline>
            <el-timeline-item
              :timestamp="detailData.createdAt"
              placement="top"
              type="primary"
            >
              <div>{{ detailData.content }}</div>
              <div class="timeline-operator">创建人：{{ detailData.createdByName }}</div>
            </el-timeline-item>
            <el-timeline-item
              v-if="detailData.updatedAt"
              :timestamp="detailData.updatedAt"
              placement="top"
            >
              <div>更新拜访记录</div>
              <div class="timeline-operator">更新人：{{ detailData.createdByName }}</div>
            </el-timeline-item>
          </el-timeline>
        </el-card>
      </template>
    </el-drawer>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'

const searchForm = reactive({ customerName: '', visitor: '' })
const addForm = reactive({ customerName: '', visitor: '', visitTime: '', content: '' })
const visitList = ref([])
const saving = ref(false)
const showAddDialog = ref(false)
const showDetail = ref(false)
const detailData = ref(null)

let idCounter = 3

function handleSearch() {
  const keyword = searchForm.customerName.toLowerCase()
  const visitorKeyword = searchForm.visitor.toLowerCase()
  visitList.value = mockData.filter(item => {
    const matchCustomer = !keyword || item.customerName.toLowerCase().includes(keyword)
    const matchVisitor = !visitorKeyword || item.visitor.toLowerCase().includes(visitorKeyword)
    return matchCustomer && matchVisitor
  })
}

function handleReset() {
  searchForm.customerName = ''
  searchForm.visitor = ''
  visitList.value = [...mockData]
}

function handleViewDetail(row) {
  detailData.value = row
  showDetail.value = true
}

function formatDate(d) {
  const date = new Date(d)
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  const h = String(date.getHours()).padStart(2, '0')
  const min = String(date.getMinutes()).padStart(2, '0')
  return `${y}-${m}-${day} ${h}:${min}`
}

function handleSave() {
  if (!addForm.customerName || !addForm.visitor || !addForm.visitTime || !addForm.content) {
    ElMessage.warning('请填写完整信息')
    return
  }
  saving.value = true
  setTimeout(() => {
    const now = formatDate(new Date())
    const newRecord = {
      id: idCounter++,
      customerName: addForm.customerName,
      visitor: addForm.visitor,
      visitTime: formatDate(addForm.visitTime),
      content: addForm.content,
      createdByName: '当前用户',
      createdAt: now,
    }
    mockData.unshift(newRecord)
    visitList.value = [...mockData]
    showAddDialog.value = false
    addForm.customerName = ''
    addForm.visitor = ''
    addForm.visitTime = ''
    addForm.content = ''
    saving.value = false
    ElMessage.success('拜访记录已添加')
  }, 300)
}

const mockData = [
  { id: 1, customerName: '张三科技有限公司', visitor: '王小明', visitTime: '2026-05-28 09:30:00', content: '拜访客户洽谈新产品合作事宜，双方初步达成合作意向。', createdByName: '李经理', createdAt: '2026-05-28 10:00:00' },
  { id: 2, customerName: '四海贸易有限公司', visitor: '赵小红', visitTime: '2026-05-27 14:00:00', content: '回访老客户，了解产品使用情况，客户反馈良好。', createdByName: '李经理', createdAt: '2026-05-27 15:30:00', updatedAt: '2026-05-27 16:00:00' },
]

onMounted(() => {
  visitList.value = [...mockData]
})
</script>

<style scoped>
.search-bar {
  padding-bottom: 16px;
  border-bottom: 1px solid #eee;
  margin-bottom: 16px;
}
.action-bar {
  margin-bottom: 16px;
}
.detail-content {
  font-size: 14px;
  color: #1d1d1f;
  line-height: 1.6;
  white-space: pre-wrap;
}
.section {
  margin-top: 16px;
}
.timeline-operator {
  font-size: 12px;
  color: #909399;
}
</style>