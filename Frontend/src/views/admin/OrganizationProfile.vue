<template>
  <AdminLayout>
    <div class="admin-page">
      <div class="page-header">
        <div class="breadcrumb">
          <i class="fa-solid fa-building"></i>
          <span>ORGANIZATION / PROFILE</span>
        </div>
        <h1 class="text-hero">{{ t('Organization & Security Profile', 'Hồ sơ Tổ chức & Bảo mật (Tenant Settings)') }}</h1>
        <p class="text-desc">{{ t('Manage organization settings, IP Whitelist, and 2FA policies.', 'Quản lý cấu hình toàn tổ chức, IP Whitelist và chính sách 2FA.') }}</p>
      </div>

      <div class="admin-form-card" v-loading="isLoading">
        <el-tabs v-model="activeTab" class="custom-tabs">
          <el-tab-pane :label="t('Organization Info', 'Thông tin Tổ chức')" name="profile">
            <div class="form-group mt-12">
              <label>{{ t('Organization Name', 'Tên Tổ chức') }}</label>
              <el-input v-model="form.organizationName" class="neumorphic-input" />
            </div>

            <div class="form-group">
              <label>{{ t('Domain', 'Tên miền') }}</label>
              <el-input v-model="form.domain" class="neumorphic-input" />
            </div>

            <div class="form-group">
              <label>URL Logo</label>
              <el-input v-model="form.logoUrl" class="neumorphic-input" />
            </div>

            <div class="form-actions mt-24">
              <el-button type="primary" :loading="isSaving" @click="saveConfig">
                <i class="fa-solid fa-save mr-2"></i>
                {{ t('Save changes', 'Lưu thay đổi') }}
              </el-button>
            </div>
          </el-tab-pane>

          <el-tab-pane :label="t('System Security', 'Bảo vệ Hệ thống (Security)')" name="security">
            <div class="section-intro">
              <h3>
                <i class="fa-solid fa-shield-halved mr-2 text-warning"></i>
                {{ t('Global Security Policies', 'Chính sách Bảo mật Toàn cầu') }}
              </h3>
              <p>{{ t('Mandatory settings for all organization members.', 'Thiết lập bắt buộc đối với tất cả thành viên trong tổ chức.') }}</p>
            </div>

            <div class="form-group switch-group">
              <div>
                <h4>{{ t('Require 2FA (Two-Factor Auth)', 'Bắt buộc 2FA (Two-Factor Auth)') }}</h4>
                <span>
                  {{ t('Require all employees to setup Google Authenticator for login. Unauthenticated flows will be blocked.', 'Yêu cầu tất cả nhân viên phải cài đặt Google Authenticator để đăng nhập. Các luồng đăng nhập không có 2FA sẽ bị chặn lại.') }}
                </span>
              </div>
              <el-switch
                :model-value="form.require2FA"
                active-color="#10b981"
                inactive-color="#dc2626"
                @change="val => form.require2FA = val"
              />
            </div>

            <div class="section-intro mt-24">
              <h3>
                <i class="fa-solid fa-network-wired mr-2 text-blue-500"></i>
                IP Whitelist
              </h3>
              <p>{{ t('Restrict IP addresses allowed to access the system.', 'Giới hạn các địa chỉ IP được phép truy cập vào hệ thống.') }}</p>
            </div>

            <div class="form-group">
              <label>{{ t('Allowed IP List (IP Whitelist)', 'Danh sách IP hợp lệ (IP Whitelist)') }}</label>
              <el-input
                v-model="form.ipWhitelist"
                type="textarea"
                :rows="4"
                class="neumorphic-input"
                :placeholder="t('Example: 192.168.1.1, 203.0.113.50. Leave empty to allow all IPs.', 'Ví dụ: 192.168.1.1, 203.0.113.50. Để trống nếu cho phép mọi IP.')"
              />
            </div>

            <div class="form-actions mt-24">
              <el-button type="primary" :loading="isSaving" @click="saveConfig">
                <i class="fa-solid fa-shield-halved mr-2"></i>
                {{ t('Save Security Configuration', 'Lưu cấu hình Bảo mật') }}
              </el-button>
            </div>
          </el-tab-pane>
        </el-tabs>
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

const { t } = useLocale()
const activeTab = ref('profile')
const isLoading = ref(false)
const isSaving = ref(false)

const defaultTenantProfile = {
  id: '',
  organizationName: 'Global Organization',
  domain: 'acme.com',
  logoUrl: '',
  require2FA: false,
  ipWhitelist: ''
}

const form = ref({ ...defaultTenantProfile })

const fetchConfig = async () => {
  isLoading.value = true
  try {
    const res = await axiosClient.get('/settings/TenantProfile')
    const data = res.data?.data || {}
    form.value = {
      id: data.id || '',
      organizationName: data.organizationName || defaultTenantProfile.organizationName,
      domain: data.domain || defaultTenantProfile.domain,
      logoUrl: data.logoUrl || '',
      require2FA: data.require2FA === 'true',
      ipWhitelist: data.ipWhitelist || ''
    }
  } catch (err) {
    console.error(err)
    form.value = { ...defaultTenantProfile }
  } finally {
    isLoading.value = false
  }
}

const saveConfig = async () => {
  isSaving.value = true
  try {
    await axiosClient.put('/settings/TenantProfile', {
      Settings: {
        id: form.value.id || '',
        organizationName: form.value.organizationName || defaultTenantProfile.organizationName,
        domain: form.value.domain || defaultTenantProfile.domain,
        logoUrl: form.value.logoUrl || '',
        require2FA: form.value.require2FA ? 'true' : 'false',
        ipWhitelist: form.value.ipWhitelist || ''
      }
    })
    ElMessage.success(t('Tenant & Security settings saved successfully.', 'Đã lưu cấu hình Tenant & Bảo mật thành công.'))
  } catch (err) {
    console.error(err)
    ElMessage.error(t('Error saving configuration.', 'Lỗi khi lưu cấu hình.'))
  } finally {
    isSaving.value = false
  }
}

onMounted(fetchConfig)
</script>

<style scoped>
.admin-page {
}

.admin-form-card {
  padding: 32px;
  border: 1px solid var(--color-border);
  border-radius: var(--border-radius-xl, 12px);
  background: var(--color-surface);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
}

.form-group {
  margin-bottom: 20px;
}

.form-group label {
  display: block;
  margin-bottom: 8px;
  color: var(--color-text-primary);
  font-size: 14px;
  font-weight: 500;
}

.switch-group {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  padding: 16px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.2);
}

.switch-group h4 {
  margin: 0;
  color: var(--color-text-primary);
}

.switch-group span {
  display: block;
  margin-top: 6px;
  color: var(--color-text-muted);
  font-size: 12px;
  line-height: 1.5;
}

.section-intro {
  margin: 16px 0 24px;
}

.section-intro h3 {
  margin: 0 0 8px;
  color: var(--color-text-primary);
  font-size: 15px;
}

.section-intro p {
  margin: 0;
  color: var(--color-text-secondary);
  font-size: 13px;
}

.mt-12 {
  margin-top: 12px;
}

.mt-24 {
  margin-top: 24px;
}

.mr-2 {
  margin-right: 8px;
}

.text-warning {
  color: #f59e0b;
}

.text-blue-500 {
  color: #3b82f6;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
}

:deep(.neumorphic-input .el-input__wrapper),
:deep(.neumorphic-input .el-textarea__inner) {
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background-color: var(--color-surface-hover) !important;
  box-shadow: none !important;
}

:deep(.neumorphic-input .el-input__wrapper) {
  padding: 8px 16px;
}

:deep(.neumorphic-input .el-textarea__inner) {
  padding: 12px 16px;
  color: var(--color-text-primary) !important;
  font-family: inherit;
}

:deep(.neumorphic-input .el-input__inner) {
  color: var(--color-text-primary) !important;
}
</style>


