<template>
  <div class="system-config">
    <el-tabs v-model="activeTab">
      <!-- 企业微信配置 -->
      <el-tab-pane label="企业微信配置" name="wechat">
        <el-card shadow="never">
          <el-form :model="wechatForm" label-width="140px">
            <el-form-item label="企业 ID">
              <el-input v-model="wechatForm.corpId" />
            </el-form-item>
            <el-form-item label="Secret">
              <el-input v-model="wechatForm.corpSecret" type="password" show-password />
            </el-form-item>
            <el-form-item label="AgentId">
              <el-input-number v-model="wechatForm.agentId" :min="0" />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" @click="handleSaveWeChat">保存</el-button>
              <el-button @click="handleTestWeChat">测试连接</el-button>
            </el-form-item>
          </el-form>
        </el-card>
      </el-tab-pane>

      <!-- Webhook 管理 -->
      <el-tab-pane label="Webhook" name="webhook">
        <el-card shadow="never">
          <div class="action-bar">
            <el-button type="primary" @click="showWebhookDialog = true">新增 Webhook</el-button>
          </div>
          <el-table :data="webhooks" stripe>
            <el-table-column prop="name" label="名称" />
            <el-table-column prop="webhookUrl" label="URL" min-width="300" show-overflow-tooltip />
            <el-table-column prop="isEnabled" label="状态" width="80">
              <template #default="{ row }">
                <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '停用' }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="120">
              <template #default="{ row }">
                <el-button text type="primary" @click="handleTestWebhook(row)">测试</el-button>
                <el-button text type="danger">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-tab-pane>

      <!-- 数据同步 -->
      <el-tab-pane label="数据同步" name="sync">
        <el-card shadow="never">
          <div class="sync-info">
            <el-descriptions :column="2" border>
              <el-descriptions-item label="同步状态">
                <el-tag :type="syncStatus?.status === 2 ? 'success' : 'warning'">{{ syncStatus?.isOnline ? '在线' : '离线' }}</el-tag>
              </el-descriptions-item>
              <el-descriptions-item label="上次同步">{{ syncStatus?.lastSyncTime || '从未同步' }}</el-descriptions-item>
              <el-descriptions-item label="待同步数量">{{ syncStatus?.pendingCount || 0 }}</el-descriptions-item>
              <el-descriptions-item label="冲突数量">{{ syncStatus?.conflictCount || 0 }}</el-descriptions-item>
            </el-descriptions>
            <el-button type="primary" class="sync-btn" :loading="syncing" @click="handleStartSync">立即同步</el-button>
          </div>
        </el-card>
      </el-tab-pane>

      <!-- 数据备份 -->
      <el-tab-pane label="数据备份" name="backup">
        <el-card shadow="never">
          <div class="action-bar">
            <el-button type="primary" :loading="backingUp" @click="handleBackup">创建备份</el-button>
          </div>
          <el-table :data="backups" stripe>
            <el-table-column prop="fileName" label="文件名" min-width="200" />
            <el-table-column prop="backupType" label="类型" width="80" />
            <el-table-column prop="fileSize" label="大小" width="80" />
            <el-table-column prop="backupTime" label="备份时间" width="170" />
            <el-table-column label="操作" width="120">
              <template #default="{ row }">
                <el-button text type="primary" @click="handleRestore(row)">恢复</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="showWebhookDialog" title="新增 Webhook" width="500px">
      <el-form :model="webhookForm" label-width="100px">
        <el-form-item label="名称"><el-input v-model="webhookForm.name" /></el-form-item>
        <el-form-item label="URL"><el-input v-model="webhookForm.webhookUrl" /></el-form-item>
        <el-form-item label="备注"><el-input v-model="webhookForm.remark" type="textarea" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showWebhookDialog = false">取消</el-button>
        <el-button type="primary" @click="handleCreateWebhook">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { systemApi } from '@/api'

const activeTab = ref('wechat')
const syncing = ref(false)
const backingUp = ref(false)
const showWebhookDialog = ref(false)

const wechatForm = reactive({ corpId: '', corpSecret: '', agentId: 0 })
const webhookForm = reactive({ name: '', webhookUrl: '', remark: '' })
const webhooks = ref([])
const backups = ref([])
const syncStatus = ref(null)

async function handleSaveWeChat() {
  const res = await systemApi.saveWeChatConfig(wechatForm)
  ElMessage.success(res.message || '保存成功')
}

async function handleTestWeChat() {
  const res = await systemApi.testWeChat()
  ElMessage.success(res.success ? '连接成功' : '连接失败')
}

async function handleCreateWebhook() {
  const res = await systemApi.createWebhook(webhookForm)
  if (res.success) { ElMessage.success('创建成功'); showWebhookDialog.value = false }
}

async function handleTestWebhook(row) { ElMessage.info('测试中...') }

async function handleStartSync() {
  syncing.value = true
  try { const res = await systemApi.startSync(); ElMessage.success(res.message || '同步完成') }
  finally { syncing.value = false }
}

async function handleBackup() {
  backingUp.value = true
  try {
    const res = await systemApi.createBackup()
    ElMessage.success(res.success ? '备份成功' : '备份失败')
  } finally { backingUp.value = false }
}

function handleRestore(row) { ElMessage.info('恢复功能开发中') }

onMounted(async () => {
  const [wRes, bRes, sRes] = await Promise.all([
    systemApi.getWebhooks({ pageSize: 100 }),
    systemApi.getBackups({ pageSize: 100 }),
    systemApi.getSyncStatus(),
  ])
  if (wRes.success) webhooks.value = wRes.data?.items || []
  if (bRes.success) backups.value = bRes.data?.items || []
  if (sRes.success) syncStatus.value = sRes.data
})
</script>

<style scoped>
.action-bar { margin-bottom: 16px; }
.sync-btn { margin-top: 16px; }
</style>