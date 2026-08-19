<template>
  <AdminLayout>
    <div class="admin-page">
      <div class="page-header">
        <div class="breadcrumb">
          <i class="fa-solid fa-shield-halved"></i>
          <span>SECURITY / IP WHITELIST</span>
        </div>
        <h1 class="text-hero">{{ t('Allowed IP List', 'Danh sách IP cho phép') }}</h1>
        <p class="text-desc">{{ t('Advanced security control: Limit system access only from trusted networks.', 'Kiểm soát bảo mật nâng cao: Giới hạn truy cập hệ thống chỉ từ các mạng được tin tưởng.') }}</p>
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
               {{ t('Your current IP:', 'IP hiện tại của bạn:') }} <strong class="text-highlight">113.160.100.22</strong>
            </div>
            <div class="action-buttons">
               <el-button @click="addCurrentIp" type="default" plain>
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
         <p class="section-desc">{{ t('Login history for the last 30 days to detect suspicious devices and locations.', 'Lịch sử đăng nhập 30 ngày gần nhất để phát hiện thiết bị và địa điểm bất thường.') }}</p>
         
         <el-table :data="accessLogs" class="glass-table" style="width: 100%">
            <el-table-column prop="time" :label="t('Time', 'Thời gian')" width="180" />
            <el-table-column prop="ip" :label="t('IP Address', 'Địa chỉ IP')" width="180">
              <template #default="{ row }">
                 <span class="ip-font">{{ row.ip }}</span>
              </template>
            </el-table-column>
            <el-table-column prop="location" :label="t('Region (Estimated)', 'Khu vực (Ước tính)')" />
            <el-table-column prop="device" :label="t('Access from', 'Truy cập từ')" />
            <el-table-column :label="t('Status', 'Trạng thái')" width="150" align="center">
              <template #default="{ row }">
                 <el-tag :type="row.risk === 'An Toàn' ? 'success' : (row.risk === 'IP Mới' ? 'warning' : 'danger')" effect="dark" size="small">
                   {{ row.risk }}
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
const accessLogs = ref([]) // Still mock for now as requested or wait, I am just connecting IP config.

onMounted(async () => {
  await fetchIpWhitelist();
  // Keep mock logs for visualization
  accessLogs.value = [
    { time: '11/04/2026 08:30:12', ip: '113.160.100.22', location: 'Hanoi, VN', device: 'Chrome / Windows', risk: 'An Toàn' },
    { time: '10/04/2026 19:45:00', ip: '14.161.40.112', location: 'Hanoi, VN', device: 'Safari / MacOS', risk: 'An Toàn' },
    { time: '09/04/2026 02:11:05', ip: '43.224.23.11', location: 'Singapore, SG', device: 'Firefox / Linux', risk: 'IP Mới' }
  ];
});

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
  if (!isEnabled.value) {
    ElMessage.warning(t('Please enable IP Whitelisting first.', 'Vui lòng kích hoạt tính năng IP Whitelisting trước.'));
    return;
  }
  const exists = whitelistedIps.value.find(x => x.ip === '113.160.100.22');
  if(!exists){
    whitelistedIps.value.push({
      ip: '113.160.100.22',
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
</style>



