<template>
  <AdminLayout>
    <div class="admin-page">
      <div class="page-header">
        <div class="breadcrumb">
          <i class="fa-solid fa-shield-halved"></i>
          <span>SECURITY / TWO-FACTOR AUTH</span>
        </div>
        <h1 class="text-hero">{{ t('Two-Factor Authentication (2FA)', 'Xác thực 2 bước (2FA)') }}</h1>
        <p class="text-desc">{{ t('Protect your account with an additional security layer.', 'Bảo vệ tài khoản với lớp bảo mật bổ sung phòng chống đánh cắp dữ liệu.') }}</p>
      </div>

      <div class="form-container">
        <!-- Status Card -->
        <div class="settings-card security-status" :class="is2faEnabled ? 'status-active' : 'status-inactive'">
          <div class="status-icon">
            <i class="fa-solid" :class="is2faEnabled ? 'fa-shield-halved' : 'fa-shield'"></i>
          </div>
          <div class="status-info">
            <h2 class="status-title">{{ is2faEnabled ? t('Account Protected', 'Tài khoản Đã Bảo vệ') : t('2FA Not Enabled', 'Chưa Kích Hoạt 2FA') }}</h2>
            <p class="status-desc">{{ is2faEnabled ? t('Two-factor authentication is active.', 'Xác thực 2 bước đang được bật. Kẻ gian không thể đăng nhập dù biết mật khẩu.') : t('Turn on 2FA to reduce password leak risks.', 'Hãy bật Xác thực 2 bước để giảm thiểu rủi ro lộ mật khẩu.') }}</p>
          </div>
          <div class="status-action" v-loading="isLoadingToggle">
             <el-switch v-model="is2faEnabled" active-color="#10b981" inactive-color="#475569" @change="toggle2fa" />
          </div>
        </div>

        <div class="settings-card mt-24" :class="{ 'disabled-section': !is2faEnabled }">
          <h3 class="card-title">{{ t('Authentication Methods', 'Phương thức xác thực') }}</h3>
          <p class="section-desc">{{ t('Choose how you want to receive verification codes.', 'Lựa chọn cách thức bạn muốn lấy mã xác thực trong quá trình đăng nhập.') }}</p>

          <div class="auth-method">
            <div class="method-icon"><i class="fa-solid fa-mobile-screen"></i></div>
            <div class="method-content">
               <h4 class="method-title">{{ t('Authenticator App (Recommended)', 'Ứng dụng xác thực (Khuyên dùng)') }}</h4>
               <p class="method-desc">{{ t('Use Google Authenticator or Microsoft Authenticator to generate secure OTP codes.', 'Sử dụng Google Authenticator hoặc Microsoft Authenticator để tạo mã OTP an toàn.') }}</p>
            </div>
            <div class="method-action">
              <el-button type="primary" plain @click="handleNotImplemented(t('Configure Authenticator App', 'Cấu hình Ứng dụng xác thực'))">{{ t('Configure', 'Cấu hình') }}</el-button>
            </div>
          </div>

          <div class="divider"></div>

          <div class="auth-method">
            <div class="method-icon"><i class="fa-solid fa-envelope-open-text"></i></div>
            <div class="method-content">
               <h4 class="method-title">{{ t('SMS / Email Message', 'Tin nhắn SMS / Email') }}</h4>
               <p class="method-desc">{{ t('Receive a 6-digit verification code via SMS or email.', 'Nhận mã xác nhận gồm 6 chữ số gửi qua số điện thoại hoặc email. Thiết lập dễ dàng nhưng kém an toàn hơn Ứng dụng.') }}</p>
            </div>
            <div class="method-action">
              <el-button type="default" :type="is2faEnabled ? 'danger' : 'success'" :plain="true" @click="is2faEnabled = !is2faEnabled; toggle2fa(is2faEnabled)">
                 {{ is2faEnabled ? t('Disable', 'Tắt') : t('Enable', 'Bật') }}
              </el-button>
            </div>
          </div>

          <div class="divider"></div>

          <div class="auth-method">
            <div class="method-icon"><i class="fa-solid fa-key"></i></div>
            <div class="method-content">
               <h4 class="method-title">{{ t('Backup Codes', 'Mã dự phòng (Backup Codes)') }}</h4>
               <p class="method-desc">{{ t('Get a list of 10 single-use codes in case you lose your phone.', 'Nhận danh sách 10 mã (mỗi mã dùng 1 lần duy nhất) phòng trường hợp bạn làm mất điện thoại.') }}</p>
            </div>
            <div class="method-action">
              <el-button type="default" @click="handleNotImplemented(t('Generate new backup codes', 'Tạo Mã dự phòng mới'))">{{ t('Generate', 'Tạo mã') }}</el-button>
            </div>
          </div>
        </div>

      </div>
    </div>
  </AdminLayout>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import AdminLayout from '@/components/layout/AdminLayout.vue'
import axiosClient from '@/api/axiosClient'
import { ElMessage } from 'element-plus'
import { useLocale } from '@/composables/useLocale'

const { t } = useLocale()
const is2faEnabled = ref(false)
const isLoadingToggle = ref(false)

const loadProfile = async () => {
  try {
    const { data } = await axiosClient.get('/users/me')
    is2faEnabled.value = data.data.is2FaEnabled || false
  } catch (err) {
    console.error(err)
  }
}

const toggle2fa = async (newValue) => {
  isLoadingToggle.value = true
  try {
    const { data } = await axiosClient.post('/users/toggle-2fa', { enable: newValue })
    is2faEnabled.value = data.is2FaEnabled
    ElMessage.success(data.message)
  } catch (err) {
    console.error(err)
    ElMessage.error(t('Unable to update 2FA configuration', 'Không thể cập nhật cấu hình 2FA'))
    is2faEnabled.value = !newValue // revert UI
  } finally {
    isLoadingToggle.value = false
  }
}

const handleNotImplemented = (featureName) => {
  ElMessage.info(t(`Feature [${featureName}] will be available in a future update.`, `Tính năng [${featureName}] sẽ sớm được hoàn thiện ở bản cập nhật sau.`))
}

onMounted(() => {
  loadProfile()
})
</script>

<style scoped>


.settings-card {
  background-color: var(--color-surface);
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 24px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.05);
  transition: all 0.3s ease;
}

.security-status {
  display: flex;
  align-items: center;
  gap: 20px;
}

.status-active .status-icon {
  background: rgba(16, 185, 129, 0.15);
  color: #10b981;
}

.status-inactive .status-icon {
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
}

.status-icon {
  width: 56px;
  height: 56px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 24px;
  flex-shrink: 0;
}

.status-info {
  flex: 1;
}

.status-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin-bottom: 4px;
}

.status-desc {
  font-size: 14px;
  color: var(--color-text-muted);
  line-height: 1.5;
}

.mt-24 {
  margin-top: 24px;
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
  margin-bottom: 24px;
}

.auth-method {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 12px 0;
}

.method-icon {
  width: 48px;
  height: 48px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  color: var(--color-text-primary);
  flex-shrink: 0;
}

.method-content {
  flex: 1;
}

.method-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin-bottom: 4px;
}

.method-desc {
  font-size: 13px;
  color: var(--color-text-muted);
  line-height: 1.4;
}

.divider {
  height: 1px;
  background-color: var(--color-border);
  margin: 8px 0;
}

.disabled-section {
  opacity: 0.5;
  pointer-events: none;
}
</style>



