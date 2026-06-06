<template>
  <div class="product-list">
    <el-card shadow="never">
      <el-form :inline="true" :model="searchForm" class="search-bar">
        <el-form-item label="关键词">
          <el-input v-model="searchForm.keyword" placeholder="名称/SKU" clearable />
        </el-form-item>
        <el-form-item label="分类">
          <el-select v-model="searchForm.categoryId" placeholder="全部" clearable style="width: 160px">
            <el-option v-for="c in categories" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="handleSearch">查询</el-button>
          <el-button @click="handleReset">重置</el-button>
        </el-form-item>
      </el-form>

      <div class="action-bar">
        <el-button type="primary" @click="showDialog = true; isEdit = false; resetForm()">
          <el-icon><Plus /></el-icon>新增产品
        </el-button>
      </div>

      <el-table :data="tableData" stripe v-loading="loading" style="width: 100%">
        <el-table-column prop="sku" label="SKU" width="140" />
        <el-table-column prop="name" label="产品名称" min-width="160">
          <template #default="{ row }">
            <el-link type="primary" @click="$router.push(`/products/${row.id}`)">{{ row.name }}</el-link>
          </template>
        </el-table-column>
        <el-table-column prop="specification" label="规格" width="120" />
        <el-table-column prop="referencePrice" label="参考价" width="100">
          <template #default="{ row }">¥{{ (row.referencePrice || 0)?.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column prop="avgSalesPrice" label="销售均价" width="100">
          <template #default="{ row }">¥{{ (row.avgSalesPrice || 0)?.toFixed(2) }}</template>
        </el-table-column>
        <el-table-column prop="stock" label="库存" width="80" />
        <el-table-column prop="unit" label="单位" width="60" />
        <el-table-column prop="categoryName" label="分类" width="120" />
        <el-table-column prop="status" label="状态" width="80">
          <template #default="{ row }">
            <el-tag :type="row.status === 1 ? 'success' : 'danger'">
              {{ row.status === 1 ? '启用' : '停用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="180" fixed="right">
          <template #default="{ row }">
            <el-button text type="primary" @click="$router.push(`/products/${row.id}`)">详情</el-button>
            <el-button text type="primary" @click="handleStock(row)">调库存</el-button>
            <el-button text type="danger" @click="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>

      <div class="pagination">
        <el-pagination
          v-model:current-page="pageIndex" v-model:page-size="pageSize"
          :total="totalCount" :page-sizes="[20, 50, 100]"
          layout="total, sizes, prev, pager, next"
          @size-change="loadData" @current-change="loadData"
        />
      </div>
    </el-card>

    <el-dialog v-model="showDialog" :title="isEdit ? '编辑产品' : '新增产品'" width="650px">
      <el-form :model="form" ref="formRef" label-width="100px">
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="产品名称" prop="name">
              <el-input v-model="form.name" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="SKU">
              <el-input v-model="form.sku" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="参考价">
              <el-input-number v-model="form.referencePrice" :min="0" :precision="2" style="width: 100%" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="单位">
              <el-input v-model="form.unit" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="20">
          <el-col :span="12">
            <el-form-item label="规格">
              <el-input v-model="form.specification" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="分类">
              <el-select v-model="form.categoryId" filterable style="width: 100%">
                <el-option v-for="c in categories" :key="c.id" :label="c.name" :value="c.id" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="备注">
          <el-input v-model="form.remark" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showDialog = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { productApi } from '@/api'

const tableData = ref([])
const categories = ref([])
const loading = ref(false)
const saving = ref(false)
const pageIndex = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)
const showDialog = ref(false)
const isEdit = ref(false)
const formRef = ref(null)
const searchForm = reactive({ keyword: '', categoryId: null })

const form = reactive({
  id: 0, name: '', sku: '', specification: '', referencePrice: 0,
  avgSalesPrice: 0, unit: '', categoryId: null, remark: '', status: 1,
})

function resetForm() {
  Object.assign(form, {
    id: 0, name: '', sku: '', specification: '', referencePrice: 0,
    avgSalesPrice: 0, unit: '', categoryId: null, remark: '', status: 1,
  })
}

async function loadData() {
  loading.value = true
  try {
    const res = await productApi.getList({ pageIndex: pageIndex.value, pageSize: pageSize.value, ...searchForm })
    if (res.success) { tableData.value = res.data?.items || []; totalCount.value = res.data?.totalCount || 0 }
  } finally { loading.value = false }
}

function handleSearch() { pageIndex.value = 1; loadData() }
function handleReset() { searchForm.keyword = ''; searchForm.categoryId = null; handleSearch() }

async function handleSave() {
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return
  saving.value = true
  try {
    const res = isEdit.value ? await productApi.update(form.id, form) : await productApi.create(form)
    if (res.success) { ElMessage.success(isEdit.value ? '更新成功' : '创建成功'); showDialog.value = false; loadData() }
    else ElMessage.error(res.message)
  } finally { saving.value = false }
}

function handleStock(row) { ElMessage.info('调库存功能开发中') }

async function handleDelete(row) {
  await ElMessageBox.confirm(`确定删除产品"${row.name}"？`, '提示')
  const res = await productApi.delete(row.id)
  if (res.success) { ElMessage.success('删除成功'); loadData() }
}

onMounted(async () => {
  const res = await productApi.getCategories()
  if (res.success) categories.value = res.data || []
  loadData()
})
</script>

<style scoped>
.search-bar { padding-bottom: 16px; border-bottom: 1px solid #eee; margin-bottom: 16px; }
.action-bar { margin-bottom: 16px; }
.pagination { margin-top: 16px; display: flex; justify-content: flex-end; }
</style>