<template>
  <div class="field-manage">
    <el-card shadow="never">
      <el-tabs v-model="activeTab">
        <el-tab-pane label="商圈" name="district">
          <div class="action-bar">
            <el-button type="primary" @click="openDistrictDialog()">
              <el-icon><Plus /></el-icon>新增商圈
            </el-button>
          </div>
          <el-table :data="businessDistricts" stripe style="width: 100%">
            <el-table-column prop="name" label="名称" />
            <el-table-column label="城市" width="160">
              <template #default="{ row }">
                <el-select v-model="row.city" placeholder="选择城市" filterable size="small" style="width: 120px" @change="handleCityChange(row)">
                  <el-option v-for="c in cities" :key="c" :label="c" :value="c" />
                </el-select>
              </template>
            </el-table-column>
            <el-table-column label="状态" width="100">
              <template #default="{ row }">
                <el-switch v-model="row.status" active-value="启用" inactive-value="停用" />
              </template>
            </el-table-column>
            <el-table-column label="操作" width="160">
              <template #default="{ row }">
                <el-button text type="primary" @click="openDistrictDialog(row)">编辑</el-button>
                <el-button text type="danger" @click="handleDelete(businessDistricts, row.id)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-tab-pane>

        <el-tab-pane label="标签" name="tag">
          <div class="action-bar">
            <el-button type="primary" @click="openTagDialog()">
              <el-icon><Plus /></el-icon>新增标签
            </el-button>
          </div>
          <el-table :data="tags" stripe style="width: 100%">
            <el-table-column prop="name" label="标签名称" />
            <el-table-column label="颜色" width="120">
              <template #default="{ row }">
                <el-tag :color="row.color" style="color: #fff; border: none;">{{ row.name }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="160">
              <template #default="{ row }">
                <el-button text type="primary" @click="openTagDialog(row)">编辑</el-button>
                <el-button text type="danger" @click="handleDelete(tags, row.id)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-tab-pane>

        <el-tab-pane label="收款状态" name="payment">
          <div class="action-bar">
            <el-button type="primary" @click="openPaymentDialog()">
              <el-icon><Plus /></el-icon>新增收款状态
            </el-button>
          </div>
          <el-table :data="paymentStatuses" stripe style="width: 100%">
            <el-table-column prop="name" label="状态名称" />
            <el-table-column prop="sort" label="排序" width="100" />
            <el-table-column label="操作" width="160">
              <template #default="{ row }">
                <el-button text type="primary" @click="openPaymentDialog(row)">编辑</el-button>
                <el-button text type="danger" @click="handleDelete(paymentStatuses, row.id)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-tab-pane>

        <el-tab-pane label="产品分类" name="category">
          <div class="action-bar">
            <el-button type="primary" @click="openCategoryDialog()">
              <el-icon><Plus /></el-icon>新增产品分类
            </el-button>
          </div>
          <el-table :data="productCategories" stripe style="width: 100%">
            <el-table-column prop="name" label="分类名称" />
            <el-table-column prop="sort" label="排序" width="100" />
            <el-table-column label="操作" width="160">
              <template #default="{ row }">
                <el-button text type="primary" @click="openCategoryDialog(row)">编辑</el-button>
                <el-button text type="danger" @click="handleDelete(productCategories, row.id)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-tab-pane>
      </el-tabs>
    </el-card>

    <el-dialog v-model="districtDialog.visible" :title="districtDialog.isEdit ? '编辑商圈' : '新增商圈'" width="500px">
      <el-form :model="districtForm" label-width="80px">
        <el-form-item label="名称">
          <el-input v-model="districtForm.name" />
        </el-form-item>
        <el-form-item label="城市">
          <el-select v-model="districtForm.city" placeholder="请选择城市" filterable style="width: 100%">
            <el-option v-for="c in cities" :key="c" :label="c" :value="c" />
          </el-select>
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="districtForm.status">
            <el-radio value="启用">启用</el-radio>
            <el-radio value="停用">停用</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="districtDialog.visible = false">取消</el-button>
        <el-button type="primary" @click="handleSaveDistrict">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="tagDialog.visible" :title="tagDialog.isEdit ? '编辑标签' : '新增标签'" width="500px">
      <el-form :model="tagForm" label-width="80px">
        <el-form-item label="标签名称">
          <el-input v-model="tagForm.name" />
        </el-form-item>
        <el-form-item label="颜色">
          <el-color-picker v-model="tagForm.color" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="tagDialog.visible = false">取消</el-button>
        <el-button type="primary" @click="handleSaveTag">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="paymentDialog.visible" :title="paymentDialog.isEdit ? '编辑收款状态' : '新增收款状态'" width="500px">
      <el-form :model="paymentForm" label-width="100px">
        <el-form-item label="状态名称">
          <el-input v-model="paymentForm.name" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="paymentForm.sort" :min="1" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="paymentDialog.visible = false">取消</el-button>
        <el-button type="primary" @click="handleSavePayment">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="categoryDialog.visible" :title="categoryDialog.isEdit ? '编辑产品分类' : '新增产品分类'" width="500px">
      <el-form :model="categoryForm" label-width="100px">
        <el-form-item label="分类名称">
          <el-input v-model="categoryForm.name" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="categoryForm.sort" :min="1" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="categoryDialog.visible = false">取消</el-button>
        <el-button type="primary" @click="handleSaveCategory">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { ElMessage } from 'element-plus'

const activeTab = ref('district')

const cities = [
  '北京', '上海', '广州', '深圳', '杭州', '成都', '武汉', '南京', '重庆', '西安',
  '苏州', '天津', '长沙', '郑州', '东莞', '青岛', '沈阳', '宁波', '昆明', '大连',
  '厦门', '合肥', '佛山', '福州', '哈尔滨', '济南', '温州', '长春', '石家庄', '常州',
  '泉州', '南宁', '贵阳', '南昌', '太原', '烟台', '嘉兴', '南通', '金华', '珠海',
  '惠州', '徐州', '海口', '乌鲁木齐', '绍兴', '中山', '台州', '兰州',
]

let nextId = 1

const businessDistricts = ref([
  { id: nextId++, name: '国贸商圈', city: '北京', status: '启用' },
  { id: nextId++, name: '陆家嘴商圈', city: '上海', status: '启用' },
  { id: nextId++, name: '天河商圈', city: '广州', status: '启用' },
  { id: nextId++, name: '春熙路商圈', city: '成都', status: '停用' },
])

const tags = ref([
  { id: nextId++, name: 'VIP', color: '#F56C6C' },
  { id: nextId++, name: '重要', color: '#E6A23C' },
  { id: nextId++, name: '新客户', color: '#409EFF' },
  { id: nextId++, name: '优质', color: '#67C23A' },
])

const paymentStatuses = ref([
  { id: nextId++, name: '未付款', sort: 1 },
  { id: nextId++, name: '部分付款', sort: 2 },
  { id: nextId++, name: '已付款', sort: 3 },
  { id: nextId++, name: '挂账', sort: 4 },
])

const productCategories = ref([
  { id: nextId++, name: '电子产品', sort: 1 },
  { id: nextId++, name: '服装鞋帽', sort: 2 },
  { id: nextId++, name: '食品饮料', sort: 3 },
  { id: nextId++, name: '家居用品', sort: 4 },
  { id: nextId++, name: '美妆护肤', sort: 5 },
])

const districtDialog = reactive({ visible: false, isEdit: false, editId: null })
const districtForm = reactive({ name: '', city: '', status: '启用' })

const tagDialog = reactive({ visible: false, isEdit: false, editId: null })
const tagForm = reactive({ name: '', color: '#409EFF' })

const paymentDialog = reactive({ visible: false, isEdit: false, editId: null })
const paymentForm = reactive({ name: '', sort: 1 })

const categoryDialog = reactive({ visible: false, isEdit: false, editId: null })
const categoryForm = reactive({ name: '', sort: 1 })

function resetForm(form, defaults) {
  Object.assign(form, defaults)
}

function openDistrictDialog(row) {
  resetForm(districtForm, { name: '', city: '', status: '启用' })
  districtDialog.isEdit = false
  districtDialog.editId = null
  if (row) {
    districtForm.name = row.name
    districtForm.city = row.city
    districtForm.status = row.status
    districtDialog.isEdit = true
    districtDialog.editId = row.id
  }
  districtDialog.visible = true
}

function openTagDialog(row) {
  resetForm(tagForm, { name: '', color: '#409EFF' })
  tagDialog.isEdit = false
  tagDialog.editId = null
  if (row) {
    tagForm.name = row.name
    tagForm.color = row.color
    tagDialog.isEdit = true
    tagDialog.editId = row.id
  }
  tagDialog.visible = true
}

function openPaymentDialog(row) {
  resetForm(paymentForm, { name: '', sort: 1 })
  paymentDialog.isEdit = false
  paymentDialog.editId = null
  if (row) {
    paymentForm.name = row.name
    paymentForm.sort = row.sort
    paymentDialog.isEdit = true
    paymentDialog.editId = row.id
  }
  paymentDialog.visible = true
}

function openCategoryDialog(row) {
  resetForm(categoryForm, { name: '', sort: 1 })
  categoryDialog.isEdit = false
  categoryDialog.editId = null
  if (row) {
    categoryForm.name = row.name
    categoryForm.sort = row.sort
    categoryDialog.isEdit = true
    categoryDialog.editId = row.id
  }
  categoryDialog.visible = true
}

function handleCityChange(row) {
  ElMessage.success(`已切换至: ${row.city}`)
}

function handleSaveDistrict() {
  if (!districtForm.name || !districtForm.city) {
    ElMessage.warning('请填写完整信息')
    return
  }
  if (districtDialog.isEdit) {
    const item = businessDistricts.value.find(d => d.id === districtDialog.editId)
    if (item) {
      item.name = districtForm.name
      item.city = districtForm.city
      item.status = districtForm.status
    }
    ElMessage.success('编辑成功')
  } else {
    businessDistricts.value.push({
      id: nextId++,
      name: districtForm.name,
      city: districtForm.city,
      status: districtForm.status,
    })
    ElMessage.success('新增成功')
  }
  districtDialog.visible = false
}

function handleSaveTag() {
  if (!tagForm.name || !tagForm.color) {
    ElMessage.warning('请填写完整信息')
    return
  }
  if (tagDialog.isEdit) {
    const item = tags.value.find(t => t.id === tagDialog.editId)
    if (item) {
      item.name = tagForm.name
      item.color = tagForm.color
    }
    ElMessage.success('编辑成功')
  } else {
    tags.value.push({
      id: nextId++,
      name: tagForm.name,
      color: tagForm.color,
    })
    ElMessage.success('新增成功')
  }
  tagDialog.visible = false
}

function handleSavePayment() {
  if (!paymentForm.name) {
    ElMessage.warning('请填写状态名称')
    return
  }
  if (paymentDialog.isEdit) {
    const item = paymentStatuses.value.find(p => p.id === paymentDialog.editId)
    if (item) {
      item.name = paymentForm.name
      item.sort = paymentForm.sort
    }
    ElMessage.success('编辑成功')
  } else {
    paymentStatuses.value.push({
      id: nextId++,
      name: paymentForm.name,
      sort: paymentForm.sort,
    })
    ElMessage.success('新增成功')
  }
  paymentDialog.visible = false
}

function handleSaveCategory() {
  if (!categoryForm.name) {
    ElMessage.warning('请填写分类名称')
    return
  }
  if (categoryDialog.isEdit) {
    const item = productCategories.value.find(c => c.id === categoryDialog.editId)
    if (item) {
      item.name = categoryForm.name
      item.sort = categoryForm.sort
    }
    ElMessage.success('编辑成功')
  } else {
    productCategories.value.push({
      id: nextId++,
      name: categoryForm.name,
      sort: categoryForm.sort,
    })
    ElMessage.success('新增成功')
  }
  categoryDialog.visible = false
}

function handleDelete(list, id) {
  const index = list.value.findIndex(item => item.id === id)
  if (index !== -1) {
    list.value.splice(index, 1)
    ElMessage.success('删除成功')
  }
}
</script>

<style scoped>
.action-bar {
  margin-bottom: 16px;
}
</style>