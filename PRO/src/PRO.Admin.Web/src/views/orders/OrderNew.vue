<template>
  <div class="order-new">
    <el-card shadow="never">
      <template #header>
        <span>新建订单</span>
      </template>

      <el-form :model="form" label-width="100px">
        <el-form-item label="客户" required>
          <CustomerSelector v-model="form.customerId" @change="handleCustomerChange" />
        </el-form-item>

        <el-card shadow="never" class="section">
          <template #header>
            <div class="section-header">
              <span>产品明细</span>
              <el-button type="primary" size="small" @click="addProduct">
                <el-icon><Plus /></el-icon>添加产品
              </el-button>
            </div>
          </template>

          <el-table :data="form.items" stripe>
            <el-table-column label="产品" min-width="220">
              <template #default="{ row }">
                <el-select
                  v-model="row.productId"
                  filterable
                  remote
                  :remote-method="(q) => handleProductSearch(q, row)"
                  placeholder="搜索选择产品"
                  style="width: 100%"
                  @change="(val) => handleProductSelect(val, row)"
                >
                  <el-option
                    v-for="p in productSearchResults.get(row.tempId) || []"
                    :key="p.id"
                    :label="p.name"
                    :value="p.id"
                  />
                </el-select>
              </template>
            </el-table-column>
            <el-table-column label="数量" width="140">
              <template #default="{ row }">
                <el-input-number v-model="row.quantity" :min="0" :max="999999" size="small" style="width: 120px" />
              </template>
            </el-table-column>
            <el-table-column label="单价" width="160">
              <template #default="{ row }">
                <el-input-number v-model="row.unitPrice" :min="0" :precision="2" size="small" style="width: 140px" />
              </template>
            </el-table-column>
            <el-table-column label="小计" width="120">
              <template #default="{ row }">
                ¥{{ ((row.quantity || 0) * (row.unitPrice || 0)).toFixed(2) }}
              </template>
            </el-table-column>
            <el-table-column label="优惠" width="70" align="center">
              <template #default="{ row }">
                <el-popover placement="left" :width="260" trigger="click">
                  <template #reference>
                    <el-button
                      size="small"
                      circle
                      :type="hasDiscount(row) ? 'warning' : 'default'"
                    >
                      <el-icon><Discount /></el-icon>
                    </el-button>
                  </template>
                  <div class="discount-popover">
                    <el-radio-group v-model="row.discountMode" size="small">
                      <el-radio-button value="percent">折扣率</el-radio-button>
                      <el-radio-button value="override">改价</el-radio-button>
                    </el-radio-group>
                    <div v-if="row.discountMode === 'percent'" class="discount-field">
                      <el-input-number v-model="row.discountPercent" :min="0" :max="100" size="small" />
                      <span class="discount-unit">%</span>
                      <div v-if="row.originalPrice > 0" class="discount-preview">
                        折后价: ¥{{ (row.originalPrice * ((row.discountPercent ?? 100) / 100)).toFixed(2) }}
                      </div>
                    </div>
                    <div v-else class="discount-field">
                      <el-input-number v-model="row.discountPrice" :min="0" :precision="2" size="small" />
                    </div>
                    <div class="discount-actions">
                      <el-button size="small" type="primary" @click="applyDiscount(row)">应用</el-button>
                      <el-button size="small" @click="clearDiscount(row)">取消优惠</el-button>
                    </div>
                  </div>
                </el-popover>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="70" align="center">
              <template #default="{ row }">
                <el-button size="small" type="danger" circle @click="removeProduct(row)">
                  <el-icon><Delete /></el-icon>
                </el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>

        <el-card shadow="never" class="section">
          <template #header><span>支付信息</span></template>
          <el-row :gutter="20">
            <el-col :span="8">
              <el-form-item label="订单总金额">
                <el-input :model-value="'¥' + totalAmount.toFixed(2)" readonly disabled />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="优惠金额">
                <el-input-number
                  v-model="form.discountAmount"
                  :min="0"
                  :precision="2"
                  style="width: 100%"
                />
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="收款金额">
                <el-input :model-value="'¥' + receivedAmount.toFixed(2)" readonly disabled />
              </el-form-item>
            </el-col>
          </el-row>
        </el-card>

        <el-form-item label="配送地址" class="top-form-item">
          <el-input v-model="form.deliveryAddress" type="textarea" :rows="2" placeholder="请输入配送地址" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.remark" type="textarea" :rows="2" placeholder="请输入备注" />
        </el-form-item>

        <el-form-item>
          <div class="form-actions">
            <el-button type="primary" :loading="saving" @click="handleSave(true)">保存草稿</el-button>
            <el-button type="success" :loading="submitting" @click="handleSave(false)">提交订单</el-button>
            <el-button @click="$router.back()">取消</el-button>
          </div>
        </el-form-item>
      </el-form>
    </el-card>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { orderApi, productApi } from '@/api'
import CustomerSelector from '@/components/CustomerSelector.vue'

const router = useRouter()
const saving = ref(false)
const submitting = ref(false)
const productSearchResults = reactive(new Map())

const form = reactive({
  customerId: null,
  customerName: '',
  deliveryAddress: '',
  remark: '',
  discountAmount: 0,
  items: [],
})

function createItem() {
  return {
    tempId: Date.now() + Math.random(),
    productId: null,
    productName: '',
    quantity: 1,
    unitPrice: 0,
    originalPrice: 0,
    discountMode: 'percent',
    discountPercent: 100,
    discountPrice: 0,
  }
}

const totalAmount = computed(() => {
  return form.items.reduce((sum, item) => sum + (item.quantity || 0) * (item.unitPrice || 0), 0)
})

const receivedAmount = computed(() => {
  const total = totalAmount.value
  const discount = form.discountAmount || 0
  return Math.max(0, total - discount)
})

function hasDiscount(row) {
  return row.discountPercent !== null || row.discountPrice !== null
}

function handleCustomerChange(customer) {
  if (customer) {
    form.customerName = customer.name || ''
  }
}

async function handleProductSearch(query, row) {
  if (!query) {
    productSearchResults.set(row.tempId, [])
    return
  }
  try {
    const res = await productApi.getList({ keyword: query, pageSize: 20 })
    if (res.success) {
      productSearchResults.set(row.tempId, res.data?.items || [])
    }
  } catch {
    // ignore
  }
}

function handleProductSelect(val, row) {
  const results = productSearchResults.get(row.tempId) || []
  const product = results.find(p => p.id === val)
  if (product) {
    row.productName = product.name || ''
    const price = product.price || 0
    row.unitPrice = price
    row.originalPrice = price
    row.discountPercent = null
    row.discountPrice = null
    row.discountMode = 'percent'
  }
}

function applyDiscount(row) {
  if (row.discountMode === 'percent') {
    const pct = row.discountPercent != null ? row.discountPercent : 100
    row.unitPrice = +((row.originalPrice * pct) / 100).toFixed(2)
    row.discountPercent = pct
    row.discountPrice = null
  } else {
    const price = row.discountPrice != null ? row.discountPrice : row.originalPrice
    row.unitPrice = +(+price).toFixed(2)
    row.discountPrice = row.unitPrice
    row.discountPercent = null
  }
}

function clearDiscount(row) {
  row.unitPrice = row.originalPrice
  row.discountPercent = null
  row.discountPrice = null
  row.discountMode = 'percent'
}

function addProduct() {
  form.items.push(createItem())
}

function removeProduct(row) {
  const idx = form.items.indexOf(row)
  if (idx > -1) {
    form.items.splice(idx, 1)
    productSearchResults.delete(row.tempId)
  }
}

async function handleSave(isDraft) {
  if (isDraft) {
    saving.value = true
  } else {
    submitting.value = true
  }
  try {
    if (!form.customerId) {
      ElMessage.warning('请选择客户')
      return
    }
    if (form.items.length === 0) {
      ElMessage.warning('请添加至少一个产品')
      return
    }
    for (const item of form.items) {
      if (!item.productId) {
        ElMessage.warning('请选择完整的产品信息')
        return
      }
    }

    const payload = {
      customerId: form.customerId,
      deliveryAddress: form.deliveryAddress,
      remark: form.remark,
      discountAmount: form.discountAmount || 0,
      isDraft,
      items: form.items.map(item => ({
        productId: item.productId,
        quantity: item.quantity,
        unitPrice: item.unitPrice,
      })),
    }

    const res = await orderApi.create(payload)
    if (res.success) {
      ElMessage.success(isDraft ? '草稿已保存' : '订单已提交')
      router.push('/orders/list')
    } else {
      ElMessage.error(res.message || '操作失败')
    }
  } finally {
    saving.value = false
    submitting.value = false
  }
}

onMounted(() => {
  addProduct()
})
</script>

<style scoped>
.order-new {
  max-width: 1000px;
}

.section {
  margin-top: 16px;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.top-form-item {
  margin-top: 16px;
}

.discount-popover {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.discount-field {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}

.discount-unit {
  font-size: 13px;
  color: #606266;
}

.discount-preview {
  width: 100%;
  font-size: 12px;
  color: #909399;
}

.discount-actions {
  display: flex;
  gap: 8px;
}

.form-actions {
  display: flex;
  gap: 12px;
}
</style>