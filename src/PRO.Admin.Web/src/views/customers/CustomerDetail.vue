<template>
  <div class="customer-detail">
    <div v-if="loading" class="loading-container">
      <el-skeleton :rows="12" animated />
    </div>

    <template v-else-if="customer">
      <div class="detail-header">
        <div class="header-left">
          <h2>{{ customer.name }}</h2>
          <span class="customer-no">{{ customer.customerNo }}</span>
          <el-tag :type="customer.customerType === 1 ? 'danger' : 'warning'" size="small">
            {{ customer.customerTypeName }}
          </el-tag>
        </div>
        <div class="header-right">
          <el-button type="primary" @click="handleEdit">编辑</el-button>
          <el-button @click="$router.back()">返回</el-button>
        </div>
      </div>

      <el-tabs v-model="activeTab" class="detail-tabs">
        <el-tab-pane label="基础信息" name="basic">
          <el-card shadow="never">
            <el-descriptions :column="2" border>
              <el-descriptions-item label="客户名称">{{ customer.name }}</el-descriptions-item>
              <el-descriptions-item label="客户编号">{{ customer.customerNo }}</el-descriptions-item>
              <el-descriptions-item label="类型">{{ customer.customerTypeName }}</el-descriptions-item>
              <el-descriptions-item label="电话">{{ customer.phone || '未填写' }}</el-descriptions-item>
              <el-descriptions-item label="地址" :span="2">{{ customer.address || '未填写' }}</el-descriptions-item>
              <el-descriptions-item label="所属分公司">{{ customer.branchName || '未分配' }}</el-descriptions-item>
              <el-descriptions-item label="客户担当">{{ customer.customerManagerName || '未分配' }}</el-descriptions-item>
              <el-descriptions-item label="法人代表">{{ customer.legalPerson || '未填写' }}</el-descriptions-item>
              <el-descriptions-item label="注册地址">{{ customer.registerAddress || '未填写' }}</el-descriptions-item>
              <el-descriptions-item label="备注" :span="2">{{ customer.remark || '无' }}</el-descriptions-item>
            </el-descriptions>
          </el-card>
        </el-tab-pane>

        <el-tab-pane label="关联客户" name="related">
          <el-card shadow="never">
            <template v-if="customer.customerType === 1">
              <h4 style="margin-bottom: 12px;">细分客户（子客户）</h4>
              <el-table :data="subCustomers" stripe v-loading="subLoading">
                <el-table-column prop="name" label="客户名称" min-width="140">
                  <template #default="{ row }">
                    <el-link type="primary" @click="$router.push(`/customers/${row.id}`)">{{ row.name }}</el-link>
                  </template>
                </el-table-column>
                <el-table-column prop="customerNo" label="编号" width="140" />
                <el-table-column prop="phone" label="电话" width="140" />
                <el-table-column prop="address" label="地址" min-width="160" />
                <el-table-column prop="customerManagerName" label="客户担当" width="100" />
              </el-table>
              <el-empty v-if="!subCustomers.length && !subLoading" description="暂无细分客户" />
            </template>
            <template v-else>
              <h4 style="margin-bottom: 12px;">关联大客户</h4>
              <div v-if="customer.parentCustomerId">
                <el-descriptions :column="1" border>
                  <el-descriptions-item label="大客户名称">
                    <el-link type="primary" @click="$router.push(`/customers/${customer.parentCustomerId}`)">
                      {{ customer.parentCustomerName }}
                    </el-link>
                  </el-descriptions-item>
                  <el-descriptions-item label="大客户编号">{{ customer.parentCustomerNo || '-' }}</el-descriptions-item>
                  <el-descriptions-item label="大客户担当">{{ customer.parentCustomerManagerName || '未分配' }}</el-descriptions-item>
                </el-descriptions>
              </div>
              <el-empty v-else description="未关联大客户" />
            </template>
          </el-card>
        </el-tab-pane>

        <el-tab-pane label="订单记录" name="orders">
          <el-card shadow="never">
            <el-table :data="orderRecords" stripe v-loading="ordersLoading">
              <el-table-column prop="orderNo" label="订单号" width="180">
                <template #default="{ row }">
                  <el-link type="primary" @click="$router.push(`/orders/${row.id}`)">{{ row.orderNo }}</el-link>
                </template>
              </el-table-column>
              <el-table-column prop="totalAmount" label="金额" width="120">
                <template #default="{ row }">¥{{ row.totalAmount?.toFixed(2) }}</template>
              </el-table-column>
              <el-table-column prop="statusName" label="状态" width="100" />
              <el-table-column prop="paymentStatusName" label="收款状态" width="100" />
              <el-table-column prop="createdByName" label="创建人" width="100" />
              <el-table-column prop="createdAt" label="创建时间" min-width="160" />
            </el-table>
            <el-empty v-if="!orderRecords.length && !ordersLoading" description="暂无订单记录" />
          </el-card>
        </el-tab-pane>

        <el-tab-pane label="拜访跟进" name="visits">
          <el-card shadow="never">
            <div class="section-actions">
              <el-button type="primary" size="small" @click="showVisitDialog = true">
                <el-icon><Plus /></el-icon>新增拜访
              </el-button>
              <el-button size="small" @click="showFollowUpDialog = true">
                <el-icon><Plus /></el-icon>新增跟进
              </el-button>
            </div>
            <el-timeline>
              <el-timeline-item
                v-for="r in visitRecords"
                :key="r.id"
                :timestamp="r.createdAt"
                :type="r.recordType === 'visit' ? 'primary' : 'success'"
                placement="top"
              >
                <div class="timeline-header">
                  <el-tag :type="r.recordType === 'visit' ? '' : 'success'" size="small">
                    {{ r.recordType === 'visit' ? '拜访' : '跟进' }}
                  </el-tag>
                  <span class="timeline-person">{{ r.createdByName }}</span>
                </div>
                <div class="timeline-content">{{ r.content }}</div>
                <div class="timeline-meta">
                  创建时间：{{ r.createdAt }}
                  <span v-if="r.updatedAt"> | 更新时间：{{ r.updatedAt }}</span>
                </div>
              </el-timeline-item>
            </el-timeline>
            <el-empty v-if="!visitRecords.length" description="暂无拜访/跟进记录" />
          </el-card>
        </el-tab-pane>

        <el-tab-pane label="操作审计" name="audit">
          <el-card shadow="never">
            <el-descriptions :column="2" border>
              <el-descriptions-item label="创建人">{{ customer.createdByName || '-' }}</el-descriptions-item>
              <el-descriptions-item label="创建时间">{{ customer.createdAt || '-' }}</el-descriptions-item>
              <el-descriptions-item label="最后修改人">{{ customer.updatedByName || '-' }}</el-descriptions-item>
              <el-descriptions-item label="最后修改时间">{{ customer.updatedAt || '-' }}</el-descriptions-item>
              <el-descriptions-item label="客户担当">{{ customer.customerManagerName || '未分配' }}</el-descriptions-item>
              <el-descriptions-item label="开发担当">{{ customer.createdByName || '未分配' }}</el-descriptions-item>
            </el-descriptions>
          </el-card>
        </el-tab-pane>
      </el-tabs>
    </template>

    <el-dialog v-model="showVisitDialog" title="新增拜访记录" width="500px">
      <el-form :model="visitForm" label-width="80px">
        <el-form-item label="内容">
          <el-input v-model="visitForm.content" type="textarea" :rows="4" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showVisitDialog = false">取消</el-button>
        <el-button type="primary" :loading="savingVisit" @click="handleSaveVisit">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="showFollowUpDialog" title="新增跟进记录" width="500px">
      <el-form :model="followUpForm" label-width="80px">
        <el-form-item label="内容">
          <el-input v-model="followUpForm.content" type="textarea" :rows="4" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showFollowUpDialog = false">取消</el-button>
        <el-button type="primary" :loading="savingFollowUp" @click="handleSaveFollowUp">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { customerApi } from '@/api'

const route = useRoute()
const router = useRouter()
const customer = ref(null)
const loading = ref(true)
const ordersLoading = ref(false)
const subLoading = ref(false)
const subCustomers = ref([])
const orderRecords = ref([])
const visitRecords = ref([])
const activeTab = ref('basic')
const showVisitDialog = ref(false)
const showFollowUpDialog = ref(false)
const savingVisit = ref(false)
const savingFollowUp = ref(false)
const visitForm = reactive({ content: '' })
const followUpForm = reactive({ content: '' })

async function loadDetail() {
  loading.value = true
  try {
    const res = await customerApi.getById(route.params.id)
    if (res.success) {
      customer.value = res.data

      if (res.data.customerType === 1 && res.data.subCustomers) {
        subCustomers.value = res.data.subCustomers
      }

      orderRecords.value = res.data.recentOrders || []
      visitRecords.value = res.data.visitRecords || []
    }
  } finally {
    loading.value = false
  }
}

function handleEdit() {
  router.push({ path: `/customers/${customer.value.id}`, query: { edit: '1' } })
}

async function handleSaveVisit() {
  if (!visitForm.content) { ElMessage.warning('请输入内容'); return }
  savingVisit.value = true
  try {
    const res = await customerApi.createVisit({ customerId: route.params.id, content: visitForm.content, recordType: 'visit' })
    if (res.success) { ElMessage.success('保存成功'); showVisitDialog.value = false; visitForm.content = ''; loadDetail() }
  } finally { savingVisit.value = false }
}

async function handleSaveFollowUp() {
  if (!followUpForm.content) { ElMessage.warning('请输入内容'); return }
  savingFollowUp.value = true
  try {
    const res = await customerApi.createVisit({ customerId: route.params.id, content: followUpForm.content, recordType: 'followup' })
    if (res.success) { ElMessage.success('保存成功'); showFollowUpDialog.value = false; followUpForm.content = ''; loadDetail() }
  } finally { savingFollowUp.value = false }
}

onMounted(() => {
  loadDetail()
})
</script>

<style scoped>
.customer-detail {
  padding: 0;
}

.loading-container {
  padding: 40px;
}

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 24px;
  background: #fff;
  border-bottom: 1px solid #ebeef5;
  border-radius: 8px 8px 0 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.header-left h2 {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
}

.customer-no {
  color: #909399;
  font-size: 13px;
  font-family: monospace;
}

.header-right {
  display: flex;
  gap: 8px;
}

.detail-tabs {
  margin-top: 16px;
}

.detail-tabs :deep(.el-tabs__header) {
  padding: 0 24px;
  background: #fff;
  margin-bottom: 0;
}

.detail-tabs :deep(.el-tabs__content) {
  padding: 16px 24px;
}

.section-actions {
  display: flex;
  gap: 8px;
  margin-bottom: 16px;
}

.timeline-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}

.timeline-person {
  font-size: 13px;
  font-weight: 500;
  color: #424245;
}

.timeline-content {
  font-size: 14px;
  color: #1d1d1f;
  margin: 4px 0;
  line-height: 1.6;
}

.timeline-meta {
  font-size: 12px;
  color: #86868b;
  margin-top: 4px;
}
</style>