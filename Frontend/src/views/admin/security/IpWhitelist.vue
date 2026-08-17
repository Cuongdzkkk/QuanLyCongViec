<template>
  <AdminLayout>
    <div class="admin-page-container">
      <div class="page-header">
        <div class="breadcrumb">
          <i class="fa-solid fa-shield-halved"></i> {{ t('Security / IP Whitelist', 'Bảo mật / Danh sách IP cho phép') }}
        </div>
        <h1 class="page-title">{{ t('Allowed IP List', 'Danh sách IP cho phép') }}</h1>
        <p class="page-subtitle">{{ t('Advanced security control: Limit system access only from trusted networks.', 'Kiểm soát bảo mật nâng cao: Giới hạn truy cập hệ thống chỉ từ các mạng được tin tưởng.') }}</p>
      </div>

      <div class="settings-card mb-24">
         <div class="header-flex">
            <div>
              <h3 class="card-title">{{ t('Manage Whitelisted IPs', 'Quản lý IP Danh Sách Trắng (Whitelisted IPs)') }}</h3>
              <p class="section-desc">{{ t('When this feature is enabled, any login from an IP not in the list will be denied.', 'Khi tính năng này bật, bất kỳ đăng nhập nào từ IP không có trong danh sách sẽ bị từ chối.') }}</p>
            </div>
            <div class="switch-wrapper">
               <span class="switch-label">{{ t('Enable IP protection:', 'Kích hoạt bảo vệ bằng IP:') }}</span>
               <el-switch v-model="isEnabled" @change="saveAndApplyIpWhitelist" active-color="#10b981" inactive-color="#475569" />
            </div>
         </div>

         <div class="divider"></div>

         <div class="ip-action-bar mb-24">
            <div class="current-ip-info">
               {{ t('Your current IP:', 'IP hiện tại của bạn:') }}
               <strong v-if="currentIp" class="text-highlight">{{ currentIp }}</strong>
               <span v-else-if="currentIpError" class="current-ip-error">{{ t('Unavailable', 'Không khả dụng') }}</span>
               <span v-else>{{ t('Loading...', 'Đang tải...') }}</span>
            </div>
            <div class="action-buttons">
               <el-button @click="addCurrentIp" type="default" plain :disabled="!currentIp">
                 <i class="fa-solid fa-laptop-house mr-2"></i> {{ t('Add Current IP', 'Thêm IP Hiện Tại') }}
               </el-button>
               <el-button type="primary">
                 <i class="fa-solid fa-plus mr-2"></i> {{ t('Add New IP', 'Thêm IP Mới') }}
               </el-button>
            </div>
         </div>

         <el-table :data="whitelistedIps" class="glass-table" :class="{ 'disabled-table': !isEnabled }" style="width: 100%">
            <el-table-column prop="ip" :label="t('IP Address', 'Địa chỉ IP')" width="200" />
            <el-table-column prop="note" :label="t('Note', 'Ghi chú')" />
            <el-table-column prop="addedBy" :label="t('Added by', 'Người thêm')" width="200" />
            <el-table-column prop="date" :label="t('Date saved', 'Ngày lưu')" width="180" />
            <el-table-column :label="t('Actions', 'Thao tác')" width="120" align="right">
              <template #default="scope">
                <el-button type="danger" link @click="removeIp(scope.$index)">{{ t('Delete', 'Xóa') }}</el-button>
              </template>
            </el-table-column>
         </el-table>
      </div>

      <div class="settings-card">
         <h3 class="card-title">{{ t('Recent Access Log', 'Nhật ký truy cập (Recent Access Log)') }}</h3>
         <p class="section-desc">{{ t('Recent login activity for your account.', 'Hoạt động đăng nhập gần đây của tài khoản của bạn.') }}</p>

         <div v-if="accessLogsLoading" class="access-log-state">
            {{ t('Loading recent login activity...', 'Đang tải hoạt động đăng nhập gần đây...') }}
         </div>
         <div v-else-if="accessLogsError" class="access-log-state access-log-error">
            <span>{{ t('Unable to load recent login activity.', 'Không thể tải hoạt động đăng nhập gần đây.') }}</span>
            <el-button type="primary" link @click="fetchAccessLogs">
              {{ t('Retry', 'Thử lại') }}
            </el-button>
         </div>
         <div v-else-if="accessLogs.length === 0" class="access-log-state">
            {{ t('No recent login activity.', 'Chưa có hoạt động đăng nhập gần đây.') }}
         </div>
         <el-table v-else :data="accessLogs" class="glass-table" style="width: 100%">
            <el-table-column prop="time" :label="t('Time', 'Thời gian')" width="180" />
            <el-table-column prop="ip" :label="t('IP Address', 'Địa chỉ IP')" width="180">
              <template #default="{ row }">
                 <span class="ip-font">{{ row.ip }}</span>
              </template>
            </el-table-column>
            <el-table-column prop="method" :label="t('Method', 'Phương thức')" width="160" />
            <el-table-column prop="userAgent" :label="t('User Agent', 'Trình duyệt / thiết bị')" />
            <el-table-column :label="t('Status', 'Trạng thái')" width="150" align="center">
              <template #default="{ row }">
                 <el-tag effect="plain" size="small">
                   {{ row.status }}
                 </el-tag>
              </template>
            </el-table-column>
         </el-table>
      </div>
    </div>
  </AdminLayout>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import AdminLayout from '@/components/layout/AdminLayout.vue'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'
import { useLocale } from '@/composables/useLocale'

const { t, locale: currentLocale } = useLocale()
const isEnabled = ref(false)
const whitelistedIps = ref([])
const currentIp = ref('')
const currentIpError = ref(false)
const accessLogs = ref([])
const accessLogsLoading = ref(false)
const accessLogsError = ref(false)

onMounted(async () => {
  await Promise.all([fetchIpWhitelist(), fetchCurrentIp(), fetchAccessLogs()]);
});

const parseLoginDetails = (details) => {
  if (!details) return {}

  if (typeof details === 'object') {
    return details
  }

  if (typeof details !== 'string' || !details.trim()) {
    return {}
  }

  try {
    const parsed = JSON.parse(details)
    return parsed && typeof parsed === 'object' ? parsed : {}
  } catch (err) {
    return {}
  }
}

const formatAccessLogTime = (createdAt) => {
  if (!createdAt) return 'Unknown'

  const date = new Date(createdAt)
  if (Number.isNaN(date.getTime())) return 'Unknown'

  return date.toLocaleString(currentLocale.value === 'vi' ? 'vi-VN' : 'en-US')
}

const fetchAccessLogs = async () => {
  accessLogsLoading.value = true
  accessLogsError.value = false

  try {
    const res = await axiosClient.get('/users/login-activity')
    const items = Array.isArray(res.data?.data) ? res.data.data : []

    accessLogs.value = items.map(item => {
      const details = parseLoginDetails(item.details)
      const method = typeof details.method === 'string' && details.method.trim() ? details.method : 'Unknown'
      const userAgent = typeof details.userAgent === 'string' && details.userAgent.trim() ? details.userAgent : 'Unknown'

      return {
        time: formatAccessLogTime(item.createdAt),
        ip: typeof item.ipAddress === 'string' && item.ipAddress.trim() ? item.ipAddress : 'Unknown',
        status: typeof item.status === 'string' && item.status.trim() ? item.status : 'Unknown',
        method,
        userAgent
      }
    })
  } catch (err) {
    accessLogs.value = []
    accessLogsError.value = true
  } finally {
    accessLogsLoading.value = false
  }
}

const fetchCurrentIp = async () => {
  currentIpError.value = false;
  currentIp.value = '';
  try {
    const res = await axiosClient.get('/security/current-ip');
    const ipAddress = res.data?.ipAddress;
    if (typeof ipAddress !== 'string' || !ipAddress.trim()) {
      throw new Error('Current IP was not returned by the server');
    }
    currentIp.value = ipAddress;
  } catch (err) {
    currentIpError.value = true;
    ElMessage.error(t('Unable to determine your current IP address', 'Không thể xác định địa chỉ IP hiện tại của bạn'));
  }
}

const fetchIpWhitelist = async () => {
  try {
    const res = await axiosClient.get('/security/ip-whitelist');
    if (res.data && res.data.data) {
      isEnabled.value = res.data.data.isEnabled;
      whitelistedIps.value = res.data.data.ips || [];
    }
  } catch (err) {
    ElMessage.error(t('Unable to load IP Whitelist configuration', 'Không thể tải cấu hình IP Whitelist'));
  }
}

const saveAndApplyIpWhitelist = async () => {
  try {
    await axiosClient.put('/security/ip-whitelist', {
      isEnabled: isEnabled.value,
      ips: whitelistedIps.value
    });
    ElMessage.success(t('IP Whitelist configuration saved', 'Đã lưu cấu hình IP Whitelist'));
  } catch (err) {
    ElMessage.error(t('Error saving configuration.', 'Lỗi khi lưu cấu hình.'));
  }
}

const addCurrentIp = () => {
  if (!currentIp.value) {
    ElMessage.error(t('Current IP is unavailable. Please try again later.', 'IP hiện tại không khả dụng. Vui lòng thử lại sau.'));
    return;
  }
  if (!isEnabled.value) {
    ElMessage.warning(t('Please enable IP Whitelisting first.', 'Vui lòng kích hoạt tính năng IP Whitelisting trước.'));
    return;
  }
  const exists = whitelistedIps.value.find(x => x.ip === currentIp.value);
  if(!exists){
    whitelistedIps.value.push({
      ip: currentIp.value,
      note: t('Auto-added (Current device)', 'Thêm tự động (Thiết bị hiện tại)'),
      addedBy: t('You', 'Bạn'),
      date: new Date().toLocaleDateString(currentLocale.value === 'vi' ? 'vi-VN' : 'en-US')
    })
    saveAndApplyIpWhitelist();
  } else {
    ElMessage.info(t('Current IP is already in the list.', 'IP hiện tại đã có trong danh sách.'));
  }
}

const removeIp = (idx) => {
  whitelistedIps.value.splice(idx, 1);
  saveAndApplyIpWhitelist();
}
</script>

<style scoped>
.page-header {
  margin-bottom: 24px;
}

.breadcrumb {
  font-size: 13px;
  color: var(--color-text-muted);
  margin-bottom: 8px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.page-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin-bottom: 4px;
}

.page-subtitle {
  font-size: 14px;
  color: var(--color-text-muted);
}

.settings-card {
  background-color: var(--color-surface);
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 32px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.05);
}

.mb-24 { margin-bottom: 24px; }
.mr-2 { margin-right: 8px; }

.header-flex {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
}

.card-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin-bottom: 8px;
}

.section-desc {
  font-size: 13px;
  color: var(--color-text-muted);
}

.switch-wrapper {
  display: flex;
  gap: 12px;
  align-items: center;
  background: var(--color-surface);
  padding: 12px 16px;
  border-radius: 8px;
  border: 1px solid var(--color-border);
}

.switch-label {
  font-weight: 500;
  font-size: 14px;
  color: var(--color-text-primary);
}

.divider {
  height: 1px;
  background-color: var(--color-border);
  margin: 24px 0;
}

.ip-action-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.current-ip-info {
  font-size: 14px;
  color: var(--color-text-primary);
  background: rgba(13, 148, 136, 0.1);
  border-left: 3px solid #0d9488;
  padding: 8px 16px;
  border-radius: 4px;
}

.text-highlight {
  color: #0d9488;
  font-family: monospace;
  font-size: 15px;
}

.current-ip-error {
  color: #dc2626;
  font-weight: 600;
}

.action-buttons {
  display: flex;
  gap: 12px;
}

:deep(.glass-table) {
  background-color: transparent !important;
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid var(--color-border);
}

:deep(.glass-table th.el-table__cell) {
  background-color: rgba(0,0,0,0.02) !important;
  color: var(--color-text-secondary);
  border-bottom: 1px solid var(--color-border);
}

:deep(.glass-table td.el-table__cell) {
  border-bottom: 1px solid var(--color-border);
  background-color: transparent !important;
}

:deep(.glass-table .el-table__row:hover > td) {
  background-color: var(--color-surface-hover) !important;
}

.disabled-table {
  opacity: 0.5;
  pointer-events: none;
}

.ip-font {
  font-family: monospace;
  font-weight: 500;
  font-size: 14px;
}

.access-log-state {
  margin-top: 16px;
  padding: 20px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  color: var(--color-text-muted);
  text-align: center;
}

.access-log-error {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}
</style>

