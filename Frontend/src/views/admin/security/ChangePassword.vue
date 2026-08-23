<template>
  <AdminLayout>
    <div class="admin-page">
      <div class="page-header">
        <div class="breadcrumb">
          <i class="fa-solid fa-shield-halved"></i>
          <span>SECURITY / CHANGE PASSWORD</span>
        </div>
        <h1 class="text-hero">{{ t('Change Password', 'Đổi mật khẩu') }}</h1>
        <p class="text-desc">{{ t('Keep your account safe by creating a strong and unique password.', 'Đảm bảo an toàn tài khoản bằng cách tạo một mật khẩu mạnh và duy nhất.') }}</p>
      </div>

      <div class="form-container">
        <div class="settings-card mx-auto" style="max-width: 650px;">
          <div style="margin: 0 auto;">

            <!-- ======== STEP 1: Email Verification ======== -->
            <div v-if="step === 1" class="step-section">
              <div class="step-indicator">
                <div class="step-badge active">1</div>
                <div class="step-line"></div>
                <div class="step-badge">2</div>
              </div>
              <h3 class="step-title"><i class="fa-solid fa-envelope-circle-check"></i> {{ t('Verify Identity', 'Xác minh danh tính') }}</h3>
              <p class="step-desc">{{ t('Enter your account email to receive an OTP verification code.', 'Nhập email tài khoản của bạn để nhận mã xác nhận OTP.') }}</p>

              <div class="form-group">
                <label class="form-label">{{ t('Email Address', 'Địa chỉ Email') }}</label>
                <el-input
                  v-model="form.email"
                  :placeholder="t('Enter your login email...', 'Nhập email đăng nhập của bạn...')"
                  class="glass-input"
                  prefix-icon=""
                >
                  <template #prefix>
                    <i class="fa-solid fa-at" style="color: var(--color-text-muted);"></i>
                  </template>
                </el-input>
              </div>

              <div class="form-group mt-24" v-if="otpSent">
                <label class="form-label">{{ t('Verification Code (OTP)', 'Mã xác nhận (OTP)') }}</label>
                <div style="display: flex; gap: 12px; align-items: flex-start;">
                  <el-input
                    v-model="form.otpCode"
                    :placeholder="t('Enter 6-digit OTP code...', 'Nhập mã OTP 6 ký tự...')"
                    class="glass-input"
                    maxlength="6"
                    style="flex: 1"
                  >
                    <template #prefix>
                      <i class="fa-solid fa-key" style="color: var(--color-text-muted);"></i>
                    </template>
                  </el-input>
                </div>
                <div class="otp-hint mt-8">
                  <i class="fa-solid fa-circle-info"></i>
                  {{ t('OTP code has been sent to your email. Code is valid for 5 minutes.', 'Mã OTP đã được gửi đến email của bạn. Mã có hiệu lực trong 5 phút.') }}
                  <a href="#" @click.prevent="sendOtp" v-if="!isSendingOtp && canResend" class="resend-link">{{ t('Resend code', 'Gửi lại mã') }}</a>
                  <span v-if="!canResend" class="countdown-text">({{ countdownText }})</span>
                </div>
              </div>

              <div class="action-footer mt-32" style="display: flex; gap: 12px; justify-content: flex-end;">
                <el-button
                  v-if="!otpSent"
                  type="primary"
                  :disabled="!form.email"
                  :loading="isSendingOtp"
                  @click="sendOtp"
                  style="padding: 20px 24px; font-weight: 500;"
                >
                  <i class="fa-solid fa-paper-plane" style="margin-right: 6px;"></i> {{ t('Send verification code', 'Gửi mã xác nhận') }}
                </el-button>
                <el-button
                  v-if="otpSent"
                  type="primary"
                  :disabled="!form.otpCode || form.otpCode.length < 6"
                  :loading="isVerifying"
                  @click="verifyAndProceed"
                  style="padding: 20px 24px; font-weight: 500;"
                >
                  <i class="fa-solid fa-arrow-right" style="margin-right: 6px;"></i> {{ t('Verify & Continue', 'Xác nhận & Tiếp tục') }}
                </el-button>
              </div>
            </div>

            <!-- ======== STEP 2: New Password ======== -->
            <div v-if="step === 2" class="step-section">
              <div class="step-indicator">
                <div class="step-badge done"><i class="fa-solid fa-check"></i></div>
                <div class="step-line done"></div>
                <div class="step-badge active">2</div>
              </div>

              <div class="verified-badge">
                <i class="fa-solid fa-circle-check"></i>
                <span>{{ t('Email verified:', 'Email đã xác minh:') }} <strong>{{ form.email }}</strong></span>
              </div>

              <h3 class="step-title mt-24"><i class="fa-solid fa-lock"></i> {{ t('Create new password', 'Tạo mật khẩu mới') }}</h3>
              <p class="step-desc">{{ t('Create a new secure password for your account.', 'Tạo mật khẩu mới an toàn cho tài khoản của bạn.') }}</p>

              <div class="form-group mt-24">
                <label class="form-label">{{ t('New password', 'Mật khẩu mới') }}</label>
                <el-input type="password" v-model="form.newPassword" @input="checkStrength" show-password :placeholder="t('Create a new password...', 'Tạo mật khẩu mới...')" class="glass-input" />
                
                <!-- Password Strength Indicator -->
                <div class="password-strength mt-12" v-if="form.newPassword">
                  <div class="strength-bars">
                     <div class="bar" :class="strength > 0 ? strengthClass : ''"></div>
                     <div class="bar" :class="strength > 1 ? strengthClass : ''"></div>
                     <div class="bar" :class="strength > 2 ? strengthClass : ''"></div>
                     <div class="bar" :class="strength > 3 ? strengthClass : ''"></div>
                  </div>
                  <span class="strength-text" :class="strengthClass">{{ strengthLabel }}</span>
                </div>

                <div class="password-hints mt-12">
                   <ul>
                     <li :class="{ 'passed': hints.length }"><i class="fa-solid" :class="hints.length ? 'fa-check' : 'fa-circle-dot'"></i> {{ t('At least 8 characters', 'Ít nhất 8 ký tự') }}</li>
                     <li :class="{ 'passed': hints.uppercase }"><i class="fa-solid" :class="hints.uppercase ? 'fa-check' : 'fa-circle-dot'"></i> {{ t('At least one uppercase letter', 'Ít nhất một chữ hoa') }}</li>
                     <li :class="{ 'passed': hints.number }"><i class="fa-solid" :class="hints.number ? 'fa-check' : 'fa-circle-dot'"></i> {{ t('At least one number', 'Ít nhất một số') }}</li>
                     <li :class="{ 'passed': hints.special }"><i class="fa-solid" :class="hints.special ? 'fa-check' : 'fa-circle-dot'"></i> {{ t('Contains a special character', 'Có chứa ký tự đặc biệt') }}</li>
                   </ul>
                </div>
              </div>

              <div class="form-group mt-24">
                <label class="form-label">{{ t('Confirm new password', 'Xác nhận mật khẩu mới') }}</label>
                <el-input type="password" v-model="form.confirmPassword" show-password :placeholder="t('Re-enter password to confirm...', 'Nhập lại mật khẩu để xác nhận...')" class="glass-input" />
                <div v-if="form.confirmPassword && form.newPassword !== form.confirmPassword" class="error-msg mt-2 text-danger">
                  {{ t('Passwords do not match.', 'Mật khẩu xác nhận không khớp.') }}
                </div>
              </div>

              <div class="divider mt-24 mb-24"></div>

              <!-- Remote Logout Option -->
              <div class="form-group checkbox-group">
                <el-checkbox v-model="form.logoutOthers" class="logout-checkbox">
                  <div class="checkbox-content">
                    <span class="checkbox-title">{{ t('Log out from all other devices', 'Đăng xuất khỏi tất cả các thiết bị khác') }}</span>
                    <span class="checkbox-desc">{{ t('Remove all login sessions on other computers and mobile devices except your current one.', 'Loại bỏ mọi phiên đăng nhập trên các máy tính và thiết bị di động khác ngoài thiết bị hiện tại của bạn.') }}</span>
                  </div>
                </el-checkbox>
              </div>

              <div class="action-footer mt-32" style="display: flex; justify-content: space-between;">
                <el-button @click="goBack" style="padding: 20px 24px; font-weight: 500;">
                  <i class="fa-solid fa-arrow-left" style="margin-right: 6px;"></i> {{ t('Go back', 'Quay lại') }}
                </el-button>
                <el-button type="primary" :disabled="!isValid" :loading="isSaving" @click="submitPassword" style="padding: 20px 24px; font-weight: 500;">
                  <i class="fa-solid fa-floppy-disk" style="margin-right: 6px;"></i> {{ t('Update password', 'Cập nhật mật khẩu') }}
                </el-button>
              </div>
            </div>

            <!-- ======== SUCCESS State ======== -->
            <div v-if="step === 3" class="step-section success-section">
              <div class="success-icon-wrapper">
                <i class="fa-solid fa-circle-check"></i>
              </div>
              <h3 class="success-title">{{ t('Password changed successfully!', 'Đổi mật khẩu thành công!') }}</h3>
              <p class="success-desc">{{ t('Your password has been updated securely. You can log in with your new password.', 'Mật khẩu của bạn đã được cập nhật an toàn. Bạn có thể đăng nhập với mật khẩu mới.') }}</p>
              <el-button type="primary" @click="resetForm" style="padding: 20px 24px; font-weight: 500; margin-top: 24px;">
                <i class="fa-solid fa-rotate-left" style="margin-right: 6px;"></i> {{ t('Change another password', 'Đổi mật khẩu khác') }}
              </el-button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </AdminLayout>
</template>

<script setup>
import { ref, computed, reactive, onMounted, onUnmounted } from 'vue'
import AdminLayout from '@/components/layout/AdminLayout.vue'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'
import { useLocale } from '@/composables/useLocale'

const { t } = useLocale()
const step = ref(1)
const isSaving = ref(false)
const isSendingOtp = ref(false)
const isVerifying = ref(false)
const otpSent = ref(false)

// Countdown for resend
const canResend = ref(true)
const countdown = ref(0)
const countdownText = computed(() => {
  const m = Math.floor(countdown.value / 60)
  const s = countdown.value % 60
  return `${m}:${s.toString().padStart(2, '0')}`
})
let countdownTimer = null

const startCountdown = () => {
  canResend.value = false
  countdown.value = 60
  countdownTimer = setInterval(() => {
    countdown.value--
    if (countdown.value <= 0) {
      clearInterval(countdownTimer)
      canResend.value = true
    }
  }, 1000)
}

onUnmounted(() => {
  if (countdownTimer) clearInterval(countdownTimer)
})

const form = reactive({
  email: '',
  otpCode: '',
  newPassword: '',
  confirmPassword: '',
  logoutOthers: true
})

const hints = reactive({
  length: false,
  uppercase: false,
  number: false,
  special: false
})

const strength = ref(0)
const strengthClass = ref('')
const strengthLabel = ref('')

const checkStrength = () => {
  const p = form.newPassword;
  
  hints.length = p.length >= 8;
  hints.uppercase = /[A-Z]/.test(p);
  hints.number = /[0-9]/.test(p);
  hints.special = /[^A-Za-z0-9]/.test(p);
  
  let score = 0;
  if(hints.length) score++;
  if(hints.uppercase) score++;
  if(hints.number) score++;
  if(hints.special) score++;
  
  strength.value = score;
  
  if (score <= 1) {
    strengthClass.value = 'strength-weak';
    strengthLabel.value = t('Weak', 'Yếu');
  } else if (score === 2) {
    strengthClass.value = 'strength-fair';
    strengthLabel.value = t('Fair', 'Trung bình');
  } else if (score === 3) {
    strengthClass.value = 'strength-good';
    strengthLabel.value = t('Good', 'Khá');
  } else {
    strengthClass.value = 'strength-strong';
    strengthLabel.value = t('Very Strong', 'Rất Tốt');
  }
}

const isValid = computed(() => {
  return strength.value === 4 && form.newPassword === form.confirmPassword
})

const sendOtp = async () => {
  if (!form.email) {
    ElMessage.warning(t('Please enter your email.', 'Vui lòng nhập email.'))
    return
  }
  isSendingOtp.value = true;
  try {
    const { data } = await axiosClient.post('/users/send-change-password-otp', {
      email: form.email
    })
    ElMessage.success(data.message)
    otpSent.value = true
    startCountdown()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || t('Unable to send OTP code', 'Không thể gửi mã OTP'))
  } finally {
    isSendingOtp.value = false;
  }
}

const verifyAndProceed = async () => {
  if (!form.otpCode || form.otpCode.length < 6) {
    ElMessage.warning(t('Please enter the full 6-digit OTP code.', 'Vui lòng nhập đủ mã OTP 6 ký tự.'))
    return
  }
  // Move to step 2 directly — OTP will be validated on final submission
  step.value = 2
}

const goBack = () => {
  step.value = 1
}

const submitPassword = async () => {
  try {
    isSaving.value = true;
    
    await axiosClient.put('/users/change-password', {
      otpCode: form.otpCode,
      newPassword: form.newPassword,
      logoutOthers: form.logoutOthers
    })
    
    step.value = 3

  } catch (err) {
    ElMessage.error(err.response?.data?.message || t('Password change failed. Please check your OTP code.', 'Đổi mật khẩu thất bại. Vui lòng kiểm tra lại mã OTP.'));
  } finally {
    isSaving.value = false;
  }
}

const resetForm = () => {
  step.value = 1
  otpSent.value = false
  form.email = ''
  form.otpCode = ''
  form.newPassword = ''
  form.confirmPassword = ''
  strength.value = 0
  hints.length = false
  hints.uppercase = false
  hints.number = false
  hints.special = false
}

// Pre-fill email from logged-in user
onMounted(() => {
  try {
    const user = JSON.parse(localStorage.getItem('user') || '{}')
    if (user.email) {
      form.email = user.email
    }
  } catch {}
})
</script>

<style scoped>


.settings-card {
  background-color: var(--color-surface);
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 32px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.2);
}

.form-label {
  display: block;
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin-bottom: 8px;
}

/* Step Indicator */
.step-indicator {
  display: flex;
  align-items: center;
  gap: 0;
  margin-bottom: 28px;
}

.step-badge {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: var(--color-border);
  color: var(--color-text-muted);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  font-weight: 700;
  transition: all 0.3s ease;
  flex-shrink: 0;
}

.step-badge.active {
  background: linear-gradient(135deg, #3b82f6, #2563eb);
  color: white;
  box-shadow: 0 4px 14px rgba(59, 130, 246, 0.4);
}

.step-badge.done {
  background: linear-gradient(135deg, #10b981, #059669);
  color: white;
  box-shadow: 0 4px 14px rgba(16, 185, 129, 0.4);
}

.step-line {
  flex: 1;
  height: 2px;
  background: var(--color-border);
  margin: 0 12px;
  max-width: 80px;
  transition: background 0.3s ease;
}

.step-line.done {
  background: linear-gradient(90deg, #10b981, #3b82f6);
}

.step-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--color-text-primary);
  display: flex;
  align-items: center;
  gap: 10px;
  margin: 0 0 6px 0;
}

.step-title i {
  color: #3b82f6;
}

.step-desc {
  font-size: 14px;
  color: var(--color-text-muted);
  margin: 0 0 24px 0;
  line-height: 1.5;
}

/* Verified Badge */
.verified-badge {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 10px 16px;
  background: rgba(16, 185, 129, 0.08);
  border: 1px solid rgba(16, 185, 129, 0.2);
  border-radius: 8px;
  font-size: 13px;
  color: #10b981;
}

.verified-badge i {
  font-size: 16px;
}

.verified-badge strong {
  color: var(--color-text-primary);
}

/* OTP hint */
.otp-hint {
  font-size: 12px;
  color: var(--color-text-muted);
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
}

.otp-hint i {
  color: #3b82f6;
}

.resend-link {
  color: #3b82f6;
  text-decoration: none;
  font-weight: 600;
  margin-left: 4px;
}

.resend-link:hover {
  text-decoration: underline;
}

.countdown-text {
  color: #f59e0b;
  font-weight: 600;
  margin-left: 4px;
}

/* Success section */
.success-section {
  text-align: center;
  padding: 48px 0 32px;
}

.success-icon-wrapper {
  width: 96px;
  height: 96px;
  border-radius: 50%;
  background: linear-gradient(135deg, rgba(16, 185, 129, 0.15), rgba(5, 150, 105, 0.08));
  border: 2px solid rgba(16, 185, 129, 0.3);
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0 auto 24px;
  animation: successPulse 2s ease-in-out infinite;
}

@keyframes successPulse {
  0%, 100% { box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.2); }
  50% { box-shadow: 0 0 0 12px rgba(16, 185, 129, 0); }
}

.success-icon-wrapper i {
  font-size: 44px;
  color: #10b981;
}

.success-title {
  font-size: 22px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0 0 12px 0;
}

.success-desc {
  font-size: 14px;
  color: var(--color-text-secondary);
  max-width: 400px;
  margin: 0 auto;
  line-height: 1.7;
}

/* Utility classes */
.mt-24 { margin-top: 24px; }
.mt-12 { margin-top: 12px; }
.mt-8 { margin-top: 8px; }
.mt-2 { margin-top: 4px; }
.mb-24 { margin-bottom: 24px; }
.mt-32 { margin-top: 32px; }
.text-danger { color: #ef4444; font-size: 13px;}

.divider {
  height: 1px;
  background-color: var(--color-border);
  margin: 24px 0;
}

.password-strength {
  display: flex;
  align-items: center;
  gap: 12px;
}

.strength-bars {
  display: flex;
  gap: 4px;
  flex: 1;
  max-width: 200px;
}

.strength-bars .bar {
  flex: 1;
  height: 6px;
  background-color: var(--color-border);
  border-radius: 3px;
  transition: background-color 0.3s ease;
}

.bar.strength-weak { background-color: #ef4444; }
.bar.strength-fair { background-color: #f59e0b; }
.bar.strength-good { background-color: #3b82f6; }
.bar.strength-strong { background-color: #10b981; }

.strength-text {
  font-size: 12px;
  font-weight: 600;
}

.strength-text.strength-weak { color: #ef4444; }
.strength-text.strength-fair { color: #f59e0b; }
.strength-text.strength-good { color: #3b82f6; }
.strength-text.strength-strong { color: #10b981; }

.password-hints ul {
  list-style: none;
  padding: 0;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}

.password-hints li {
  font-size: 13px;
  color: var(--color-text-muted);
  display: flex;
  align-items: center;
  gap: 6px;
}

.password-hints li i {
  font-size: 11px;
}

.password-hints li.passed {
  color: #10b981;
}

:deep(.logout-checkbox) {
  display: block;
  height: auto;
  white-space: normal;
}

:deep(.logout-checkbox .el-checkbox__label) {
  padding-left: 12px;
}

.checkbox-content {
  display: flex;
  flex-direction: column;
}

.checkbox-title {
  font-weight: 600;
  color: var(--color-text-primary);
  font-size: 14px;
}

.checkbox-desc {
  font-size: 13px;
  color: var(--color-text-muted);
  margin-top: 2px;
}

:deep(.glass-input .el-input__wrapper) {
  background-color: rgba(255,255,255,0.02);
  border-radius: 8px;
  padding: 8px 12px;
}

/* Fix browser autofill stuck white background */
:deep(.el-input__inner:-webkit-autofill),
:deep(.el-input__inner:-webkit-autofill:hover),
:deep(.el-input__inner:-webkit-autofill:focus),
:deep(.el-input__inner:-webkit-autofill:active) {
  -webkit-text-fill-color: var(--color-text-primary) !important;
  transition: background-color 5000s ease-in-out 0s;
}

/* Animation */
.step-section {
  animation: fadeSlideIn 0.4s ease;
}

@keyframes fadeSlideIn {
  from {
    opacity: 0;
    transform: translateY(12px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
</style>




