<template>
  <div>
    <el-tabs v-model="activeTab">
      <el-tab-pane label="配送员管理" name="delivery">
        <el-card shadow="never">
          <div class="action-bar">
            <el-button type="primary" @click="showPersonDialog = true">
              <el-icon><Plus /></el-icon>新增配送员
            </el-button>
          </div>
          <el-table :data="deliveryPersons" stripe v-loading="loading">
            <el-table-column prop="name" label="姓名" />
            <el-table-column prop="phone" label="电话" />
            <el-table-column prop="branchName" label="分公司" />
            <el-table-column prop="currentLoad" label="当前负载" />
            <el-table-column prop="maxLoad" label="最大负载" />
            <el-table-column prop="statusName" label="状态" />
            <el-table-column label="操作">
              <template #default="{ row }">
                <el-button text type="danger" @click="handleDeletePerson(row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-tab-pane>
      <el-tab-pane label="订单分配" name="assign">
        <el-card shadow="never">
          <el-row :gutter="20">
            <el-col :span="12">
              <h4>待分配订单</h4>
              <el-table :data="pendingOrders" stripe max-height="500">
                <el-table-column prop="orderNo" label="订单号" />
                <el-table-column prop="customerName" label="客户" />
                <el-table-column label="操作">
                  <template #default="{ row }">
                    <el-select v-model="assignMap[row.id]" placeholder="选择配送员" size="small" style="width: 140px">
                      <el-option v-for="p in deliveryPersons" :key="p.id" :label="p.name" :value="p.id" />
                    </el-select>
                    <el-button size="small" type="primary" @click="doAssign(row.id)">分配</el-button>
                  </template>
                </el-table-column>
              </el-table>
            </el-col>
            <el-col :span="12">
              <h4>配送员负载</h4>
              <el-table :data="deliveryPersons" stripe>
                <el-table-column prop="name" label="姓名" />
                <el-table-column prop="currentLoad" label="当前负载" />
                <el-table-column prop="maxLoad" label="最大负载" />
                <el-table-column prop="statusName" label="状态" />
              </el-table>
            </el-col>
          </el-row>
          <el-button type="primary" class="auto-btn" @click="handleAutoAssign">智能分配</el-button>
        </el-card>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="showPersonDialog" title="新增配送员" width="500px">
      <el-form :model="personForm" label-width="100px">
        <el-form-item label="姓名"><el-input v-model="personForm.name" /></el-form-item>
        <el-form-item label="电话"><el-input v-model="personForm.phone" /></el-form-item>
        <el-form-item label="分公司">
          <el-select v-model="personForm.branchId" style="width: 100%">
            <el-option v-for="b in branches" :key="b.id" :label="b.name" :value="b.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="最大负载"><el-input-number v-model="personForm.maxLoad" :min="1" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showPersonDialog = false">取消</el-button>
        <el-button type="primary" @click="savePerson">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { logisticsApi, branchApi } from '@/api'

const activeTab = ref('delivery')
const deliveryPersons = ref([])
const pendingOrders = ref([])
const branches = ref([])
const loading = ref(false)
const showPersonDialog = ref(false)
const assignMap = reactive({})

const personForm = reactive({ name: '', phone: '', branchId: null, maxLoad: 10 })

async function loadDeliveryPersons() {
  const res = await logisticsApi.getDeliveryPersons({ pageSize: 200 })
  if (res.success) deliveryPersons.value = res.data?.items || []
}

async function loadPendingOrders() {
  const res = await logisticsApi.getPendingOrders({ branchId: 1 })
  if (res.success) pendingOrders.value = res.data || []
}

async function savePerson() {
  const res = await logisticsApi.createDeliveryPerson(personForm)
  if (res.success) { ElMessage.success('添加成功'); showPersonDialog.value = false; loadDeliveryPersons() }
}

async function doAssign(orderId) {
  const personId = assignMap[orderId]
  if (!personId) { ElMessage.warning('请选择配送员'); return }
  const res = await logisticsApi.assign({ orderId, deliveryPersonId: personId })
  if (res.success) { ElMessage.success('分配成功'); loadPendingOrders(); loadDeliveryPersons() }
}

async function handleAutoAssign() {
  const res = await logisticsApi.autoAssign({ branchId: 1 })
  ElMessage.success(res.message || '分配完成')
  loadPendingOrders()
  loadDeliveryPersons()
}

function handleDeletePerson(row) {
  ElMessage.info('删除功能开发中')
}

onMounted(async () => {
  const bRes = await branchApi.getList({ pageSize: 100 })
  if (bRes.success) branches.value = bRes.data?.items || []
  loadDeliveryPersons()
  loadPendingOrders()
})
</script>

<style scoped>
.action-bar { margin-bottom: 16px; }
.auto-btn { margin-top: 16px; }
</style>
