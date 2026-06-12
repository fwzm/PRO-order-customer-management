<template>
  <div class="product-detail">
    <div v-if="loading" class="loading-container">
      <el-skeleton :rows="12" animated />
    </div>

    <template v-else-if="product">
      <div class="detail-header">
        <div class="header-left">
          <h2>{{ product.name }}</h2>
          <span class="product-sku">{{ product.sku }}</span>
          <el-tag :type="product.status === 1 ? 'success' : 'danger'" size="small">
            {{ product.status === 1 ? '启用' : '停用' }}
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
              <el-descriptions-item label="产品名称">{{ product.name }}</el-descriptions-item>
              <el-descriptions-item label="SKU">{{ product.sku }}</el-descriptions-item>
              <el-descriptions-item label="参考价格">¥{{ product.price?.toFixed(2) }}</el-descriptions-item>
              <el-descriptions-item label="平均售价">¥{{ product.avgPrice?.toFixed(2) }}</el-descriptions-item>
              <el-descriptions-item label="分类">{{ product.categoryName || '未分类' }}</el-descriptions-item>
              <el-descriptions-item label="状态">
                <el-tag :type="product.status === 1 ? 'success' : 'danger'" size="small">
                  {{ product.status === 1 ? '启用' : '停用' }}
                </el-tag>
              </el-descriptions-item>
              <el-descriptions-item label="当前库存">{{ product.stock ?? 0 }}</el-descriptions-item>
              <el-descriptions-item label="单位">{{ product.unit || '-' }}</el-descriptions-item>
              <el-descriptions-item label="规格">{{ product.specification || '-' }}</el-descriptions-item>
              <el-descriptions-item label="成本价">¥{{ product.costPrice?.toFixed(2) }}</el-descriptions-item>
              <el-descriptions-item label="创建人">{{ product.createdByName || '-' }}</el-descriptions-item>
              <el-descriptions-item label="创建时间">{{ product.createdAt || '-' }}</el-descriptions-item>
              <el-descriptions-item label="更新人">{{ product.updatedByName || '-' }}</el-descriptions-item>
              <el-descriptions-item label="更新时间">{{ product.updatedAt || '-' }}</el-descriptions-item>
              <el-descriptions-item label="备注" :span="2">{{ product.remark || '无' }}</el-descriptions-item>
            </el-descriptions>
          </el-card>
        </el-tab-pane>

        <el-tab-pane label="库存记录" name="stock">
          <el-card shadow="never">
            <el-table :data="stockRecords" stripe v-loading="stockLoading">
              <el-table-column prop="warehouseName" label="仓库" width="140" />
              <el-table-column prop="batchNo" label="批次号" width="140" />
              <el-table-column prop="quantity" label="数量" width="80" />
              <el-table-column prop="operationType" label="操作类型" width="100" />
              <el-table-column prop="remark" label="备注" min-width="160" />
              <el-table-column prop="createdAt" label="时间" width="170" />
            </el-table>
            <el-empty v-if="!stockRecords.length && !stockLoading" description="暂无库存记录" />
          </el-card>
        </el-tab-pane>
      </el-tabs>
    </template>

    <el-dialog v-model="showEditDialog" title="编辑产品" width="600px">
      <el-form :model="editForm" label-width="100px">
        <el-form-item label="产品名称">
          <el-input v-model="editForm.name" />
        </el-form-item>
        <el-form-item label="SKU">
          <el-input v-model="editForm.sku" />
        </el-form-item>
        <el-form-item label="参考价格">
          <el-input-number v-model="editForm.price" :precision="2" :min="0" style="width: 100%" />
        </el-form-item>
        <el-form-item label="平均售价">
          <el-input-number v-model="editForm.avgPrice" :precision="2" :min="0" style="width: 100%" />
        </el-form-item>
        <el-form-item label="分类">
          <el-select v-model="editForm.categoryId" placeholder="选择分类" clearable style="width: 100%">
            <el-option v-for="c in categories" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="editForm.status">
            <el-radio :value="1">启用</el-radio>
            <el-radio :value="0">停用</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="当前库存">
          <el-input-number v-model="editForm.stock" :min="0" style="width: 100%" />
        </el-form-item>
        <el-form-item label="单位">
          <el-input v-model="editForm.unit" />
        </el-form-item>
        <el-form-item label="规格">
          <el-input v-model="editForm.specification" />
        </el-form-item>
        <el-form-item label="成本价">
          <el-input-number v-model="editForm.costPrice" :precision="2" :min="0" style="width: 100%" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="editForm.remark" type="textarea" :rows="3" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showEditDialog = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { productApi } from '@/api'

const route = useRoute()
const router = useRouter()
const product = ref(null)
const loading = ref(true)
const stockLoading = ref(false)
const stockRecords = ref([])
const categories = ref([])
const activeTab = ref('basic')
const showEditDialog = ref(false)
const saving = ref(false)

const editForm = reactive({
  name: '', sku: '', price: 0, avgPrice: 0, categoryId: null,
  status: 1, stock: 0, unit: '', specification: '', costPrice: 0, remark: '',
})

function handleEdit() {
  editForm.name = product.value.name
  editForm.sku = product.value.sku
  editForm.price = product.value.price
  editForm.avgPrice = product.value.avgPrice
  editForm.categoryId = product.value.categoryId
  editForm.status = product.value.status
  editForm.stock = product.value.stock
  editForm.unit = product.value.unit
  editForm.specification = product.value.specification
  editForm.costPrice = product.value.costPrice
  editForm.remark = product.value.remark || ''
  showEditDialog.value = true
}

async function handleSave() {
  saving.value = true
  try {
    const res = await productApi.update(product.value.id, editForm)
    if (res.success) {
      ElMessage.success('保存成功')
      showEditDialog.value = false
      loadDetail()
    } else {
      ElMessage.error(res.message)
    }
  } finally {
    saving.value = false
  }
}

async function loadDetail() {
  loading.value = true
  try {
    const res = await productApi.getById(route.params.id)
    if (res.success) {
      product.value = res.data
      stockRecords.value = res.data.stockRecords || []
    }
  } finally {
    loading.value = false
  }
}

async function loadCategories() {
  const res = await productApi.getCategories()
  if (res.success) categories.value = res.data || []
}

onMounted(() => {
  loadCategories()
  loadDetail()
})
</script>

<style scoped>
.product-detail {
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
.product-sku {
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
</style>