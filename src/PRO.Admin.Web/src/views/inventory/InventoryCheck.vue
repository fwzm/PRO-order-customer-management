<template>
  <div class="inventory-check">
    <el-tabs v-model="activeTab">
      <el-tab-pane label="仓库管理" name="warehouse">
        <el-card shadow="never">
          <div class="action-bar">
            <el-button type="primary" @click="showWarehouseDialog = true; warehouseForm = { name: '', address: '', contactPerson: '' }; isEditWarehouse = false">
              <el-icon><Plus /></el-icon>新增仓库
            </el-button>
          </div>
          <el-table :data="warehouses" stripe v-loading="warehouseLoading" style="width: 100%">
            <el-table-column prop="name" label="仓库名称" min-width="160" />
            <el-table-column prop="address" label="地址" min-width="240" show-overflow-tooltip />
            <el-table-column prop="contactPerson" label="联系人" width="140" />
            <el-table-column label="操作" width="160" fixed="right">
              <template #default="{ row, $index }">
                <el-button text type="primary" @click="handleEditWarehouse(row, $index)">编辑</el-button>
                <el-button text type="danger" @click="handleDeleteWarehouse($index)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
          <el-empty v-if="!warehouses.length && !warehouseLoading" description="暂无仓库数据" />
        </el-card>
      </el-tab-pane>

      <el-tab-pane label="库存盘点" name="check">
        <el-card shadow="never">
          <div class="action-bar">
            <el-button type="primary" @click="showCheckDialog = true; checkForm = { warehouseId: null, productName: '', currentStock: 0, actualStock: 0 }">
              <el-icon><Plus /></el-icon>新增盘点
            </el-button>
          </div>
          <el-table :data="checkRecords" stripe v-loading="checkLoading" style="width: 100%">
            <el-table-column prop="productName" label="产品名称" min-width="160" />
            <el-table-column prop="warehouseName" label="仓库" width="140" />
            <el-table-column prop="currentStock" label="当前库存" width="100" />
            <el-table-column prop="actualStock" label="实际库存" width="100" />
            <el-table-column prop="difference" label="差异" width="100">
              <template #default="{ row }">
                <span :class="row.difference !== 0 ? 'diff-negative' : ''">
                  {{ row.difference }}
                </span>
              </template>
            </el-table-column>
            <el-table-column prop="createdAt" label="盘点时间" width="170" />
            <el-table-column label="操作" width="120" fixed="right">
              <template #default="{ row, $index }">
                <el-button text type="danger" @click="handleDeleteCheck($index)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
          <el-empty v-if="!checkRecords.length && !checkLoading" description="暂无盘点记录" />
        </el-card>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="showWarehouseDialog" :title="isEditWarehouse ? '编辑仓库' : '新增仓库'" width="500px">
      <el-form :model="warehouseForm" label-width="100px">
        <el-form-item label="仓库名称">
          <el-input v-model="warehouseForm.name" />
        </el-form-item>
        <el-form-item label="地址">
          <el-input v-model="warehouseForm.address" />
        </el-form-item>
        <el-form-item label="联系人">
          <el-input v-model="warehouseForm.contactPerson" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showWarehouseDialog = false">取消</el-button>
        <el-button type="primary" @click="handleSaveWarehouse">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="showCheckDialog" title="新增盘点" width="500px">
      <el-form :model="checkForm" label-width="100px">
        <el-form-item label="产品名称">
          <el-input v-model="checkForm.productName" />
        </el-form-item>
        <el-form-item label="仓库">
          <el-select v-model="checkForm.warehouseId" placeholder="选择仓库" style="width: 100%">
            <el-option v-for="w in warehouses" :key="w.id" :label="w.name" :value="w.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="当前库存">
          <el-input-number v-model="checkForm.currentStock" :min="0" style="width: 100%" />
        </el-form-item>
        <el-form-item label="实际库存">
          <el-input-number v-model="checkForm.actualStock" :min="0" style="width: 100%" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showCheckDialog = false">取消</el-button>
        <el-button type="primary" @click="handleSaveCheck">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'

const activeTab = ref('warehouse')
const warehouseLoading = ref(false)
const checkLoading = ref(false)
const showWarehouseDialog = ref(false)
const showCheckDialog = ref(false)
const isEditWarehouse = ref(false)
const editWarehouseIndex = ref(-1)

const warehouses = ref([])
const checkRecords = ref([])

const warehouseForm = reactive({ name: '', address: '', contactPerson: '' })
const checkForm = reactive({ warehouseId: null, productName: '', currentStock: 0, actualStock: 0 })

let warehouseIdCounter = 1
let checkIdCounter = 1

function handleEditWarehouse(row, index) {
  isEditWarehouse.value = true
  editWarehouseIndex.value = index
  warehouseForm.name = row.name
  warehouseForm.address = row.address
  warehouseForm.contactPerson = row.contactPerson
  showWarehouseDialog.value = true
}

function handleSaveWarehouse() {
  if (!warehouseForm.name) {
    ElMessage.warning('请输入仓库名称')
    return
  }
  if (isEditWarehouse.value) {
    const idx = editWarehouseIndex.value
    warehouses.value[idx].name = warehouseForm.name
    warehouses.value[idx].address = warehouseForm.address
    warehouses.value[idx].contactPerson = warehouseForm.contactPerson
  } else {
    warehouses.value.push({
      id: warehouseIdCounter++,
      name: warehouseForm.name,
      address: warehouseForm.address,
      contactPerson: warehouseForm.contactPerson,
    })
  }
  showWarehouseDialog.value = false
  ElMessage.success(isEditWarehouse.value ? '仓库已更新' : '仓库已添加')
}

async function handleDeleteWarehouse(index) {
  await ElMessageBox.confirm(`确定删除仓库"${warehouses.value[index].name}"？`, '提示')
  warehouses.value.splice(index, 1)
  ElMessage.success('仓库已删除')
}

function handleSaveCheck() {
  if (!checkForm.productName || !checkForm.warehouseId) {
    ElMessage.warning('请填写完整信息')
    return
  }
  const warehouse = warehouses.value.find(w => w.id === checkForm.warehouseId)
  checkRecords.value.push({
    id: checkIdCounter++,
    productName: checkForm.productName,
    warehouseName: warehouse ? warehouse.name : '未知',
    currentStock: checkForm.currentStock,
    actualStock: checkForm.actualStock,
    difference: checkForm.actualStock - checkForm.currentStock,
    createdAt: new Date().toLocaleString('zh-CN', { hour12: false }),
  })
  showCheckDialog.value = false
  ElMessage.success('盘点记录已添加')
}

function handleDeleteCheck(index) {
  checkRecords.value.splice(index, 1)
  ElMessage.success('盘点记录已删除')
}

onMounted(() => {
  warehouses.value = [
    { id: warehouseIdCounter++, name: '主仓库', address: '北京市朝阳区XX路1号', contactPerson: '张三' },
    { id: warehouseIdCounter++, name: '二号仓库', address: '上海市浦东新区XX路2号', contactPerson: '李四' },
  ]
  checkRecords.value = [
    { id: checkIdCounter++, productName: '产品A', warehouseName: '主仓库', currentStock: 100, actualStock: 98, difference: -2, createdAt: '2026-05-28 10:00:00' },
    { id: checkIdCounter++, productName: '产品B', warehouseName: '二号仓库', currentStock: 50, actualStock: 52, difference: 2, createdAt: '2026-05-28 10:30:00' },
  ]
})
</script>

<style scoped>
.action-bar {
  margin-bottom: 16px;
}
.diff-negative {
  color: #F56C6C;
  font-weight: 600;
}
</style>