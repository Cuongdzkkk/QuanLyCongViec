<template>
  <div class="auth-page">
    <header class="auth-navbar">
      <div class="container nav-content">
        <router-link to="/" class="logo">
          <span role="img" aria-label="SprintA logo" class="custom-logo"></span>
          <span>SprintA</span>
        </router-link>
        <div class="nav-actions">
          <button class="theme-toggle" type="button" aria-label="Toggle theme" @click="toggleTheme()">
            <Moon v-if="currentTheme === 'dark'" :size="18" />
            <Sun v-else :size="18" />
          </button>
          <router-link class="nav-link" to="/login">{{ t('Sign In', 'Đăng nhập') }}</router-link>
          <router-link class="nav-primary" to="/register">{{ t('Sign Up', 'Đăng ký') }}</router-link>
        </div>
      </div>
    </header>

    <main class="auth-container">
      <section class="auth-marketing">
        <div class="eyebrow">
          <ShieldCheck :size="16" />
          Secure recovery
        </div>
        <h1>Lấy lại quyền truy cập mà vẫn giữ cảm giác cao cấp, rõ ràng và an toàn.</h1>
        <p>
          Luồng reset mật khẩu được chia thành email, OTP và mật khẩu mới để người dùng hiểu họ đang ở bước nào,
          đồng thời giao diện vẫn nổi bật trong cả nền sáng lẫn nền tối.
        </p>

        <div class="recovery-panel">
          <div class="recovery-ring">
            <LockKeyhole :size="34" />
          </div>
          <div class="recovery-list">
            <span :class="{ active: step === 1 }">Email xác thực</span>
            <span :class="{ active: step === 2 }">Mã OTP</span>
            <span :class="{ active: step === 3 }">Mật khẩu mới</span>
          </div>
        </div>
      </section>

      <section class="auth-card">
        <div class="card-head">
          <span class="card-kicker">Recovery step {{ step }} / 3</span>
          <h2>
            {{ step === 1 ? t('Reset Password', 'Đặt lại mật khẩu') : step === 2 ? t('Verify Email', 'Xác thực Email') : t('New Password', 'Tạo mật khẩu mới') }}
          </h2>
          <p>
            {{
              step === 1 ? t('Enter your email address to receive a verification OTP code.', 'Nhập email của bạn để nhận mã OTP xác thực.') :
              step === 2 ? t('Enter the 6-digit OTP code sent to your email.', 'Nhập mã xác thực gồm 6 chữ số đã được gửi tới email của bạn.') :
              t('Create a new secure password for your account.', 'Tạo mật khẩu an toàn mới cho tài khoản của bạn.')
            }}
          </p>
        </div>

        <el-form
          v-if="step === 1"
          ref="emailFormRef"
          :model="form"
          :rules="rules"
          class="auth-form"
          label-position="top"
          @submit.prevent="handleSendOtp"
        >
          <div class="entry-card recovery-entry">
            <div class="entry-icon">
              <MailCheck :size="22" />
            </div>
            <div>
              <strong>Khôi phục bằng OTP</strong>
              <span>Chúng tôi sẽ gửi mã xác thực để bạn đặt lại mật khẩu an toàn.</span>
            </div>
          </div>

          <el-form-item :label="t('Email Address', 'Địa chỉ Email')" prop="email">
            <el-input v-model.trim="form.email" placeholder="name@company.com" size="large" />
          </el-form-item>

          <el-button type="primary" native-type="submit" class="auth-btn" size="large" :loading="isLoading">
            {{ t('Send OTP Code', 'Gửi mã OTP') }}
          </el-button>

          <p class="auth-footer-text">
            {{ t('Remember password?', 'Đã nhớ mật khẩu?') }} <router-link to="/login">{{ t('Sign In', 'Đăng nhập') }}</router-link>
          </p>
        </el-form>

        <el-form v-else-if="step === 2" class="auth-form" @submit.prevent="handleVerifyOtp" label-position="top">
          <div class="otp-instruction">
            {{ t('We have sent a verification code to', 'Chúng tôi đã gửi mã xác thực tới') }} <strong>{{ form.email }}</strong>.
            {{ t('Please check your inbox.', 'Vui lòng kiểm tra hộp thư của bạn.') }}
          </div>
          <el-form-item :label="t('Verification Code', 'Mã xác thực OTP')">
            <el-input v-model="form.otp" placeholder="123456" size="large" maxlength="6" class="otp-input" />
          </el-form-item>

          <el-button type="primary" native-type="submit" class="auth-btn" size="large" :loading="isLoading">
            {{ t('Verify OTP', 'Xác thực mã') }}
          </el-button>

          <p class="auth-footer-text">
            {{ t('Did not receive code?', 'Không nhận được mã?') }}
            <a href="#" @click.prevent="step = 1">{{ t('Change email', 'Đổi email') }}</a>
            {{ t('or', 'hoặc') }}
            <a href="#" @click.prevent="handleSendOtp">{{ t('Resend OTP', 'Gửi lại OTP') }}</a>
          </p>
        </el-form>

        <el-form
          v-else
          ref="passwordFormRef"
          :model="form"
          :rules="rules"
          class="auth-form"
          label-position="top"
          @submit.prevent="handleResetPassword"
        >
          <el-form-item :label="t('New Password', 'Mật khẩu mới')" prop="password">
            <el-input v-model="form.password" type="password" :placeholder="t('Enter new password', 'Nhập mật khẩu mới')" size="large" show-password />
          </el-form-item>

          <el-form-item :label="t('Confirm Password', 'Xác nhận mật khẩu')" prop="confirmPassword">
            <el-input v-model="form.confirmPassword" type="password" :placeholder="t('Re-enter new password', 'Nhập lại mật khẩu mới')" size="large" show-password />
          </el-form-item>

          <el-button type="primary" native-type="submit" class="auth-btn" size="large" :loading="isLoading">
            {{ t('Reset Password', 'Đặt lại mật khẩu') }}
          </el-button>
        </el-form>

        <div class="auth-small-links">
          <a href="#">{{ t('Terms of Service', 'Điều khoản Dịch vụ') }}</a>
          <span>&bull;</span>
          <a href="#">{{ t('Privacy Policy', 'Chính sách Bảo mật') }}</a>
        </div>
      </section>
    </main>
  </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { LockKeyhole, MailCheck, Moon, ShieldCheck, Sun } from 'lucide-vue-next'
import axiosClient from '../api/axiosClient'
import { useLocale } from '@/composables/useLocale'
import { currentTheme, toggleTheme } from '@/utils/theme'

const router = useRouter()
const { t } = useLocale()

const isLoading = ref(false)
const emailFormRef = ref(null)
const passwordFormRef = ref(null)
const step = ref(1)
const verifiedOtpToken = ref('')

const form = reactive({
  email: '',
  otp: '',
  password: '',
  confirmPassword: ''
})

const validateConfirmPassword = (rule, value, callback) => {
  if (value === '') {
    callback(new Error(t('Please confirm your password', 'Vui lòng xác nhận mật khẩu')))
  } else if (value !== form.password) {
    callback(new Error(t('Passwords do not match', 'Mật khẩu xác nhận không khớp')))
  } else {
    callback()
  }
}

const rules = reactive({
  email: [
    { required: true, message: t('Please input email address', 'Vui lòng nhập email'), trigger: 'blur' },
    { type: 'email', message: t('Invalid email format', 'Email không đúng định dạng'), trigger: ['blur', 'change'] }
  ],
  password: [
    { required: true, message: t('Please create a password', 'Vui lòng tạo mật khẩu'), trigger: 'blur' },
    { min: 6, message: t('Password must be at least 6 characters', 'Mật khẩu phải có ít nhất 6 ký tự'), trigger: 'blur' },
    {
      pattern: /^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{6,}$/,
      message: t('Must contain at least 1 uppercase letter, 1 number, and 1 special character', 'Mật khẩu phải chứa ít nhất 1 chữ hoa, 1 số và 1 ký tự đặc biệt'),
      trigger: 'blur'
    }
  ],
  confirmPassword: [
    { required: true, message: t('Please confirm your password', 'Vui lòng xác nhận mật khẩu'), trigger: 'blur' },
    { validator: validateConfirmPassword, trigger: 'blur' }
  ]
})

const handleSendOtp = async () => {
  if (step.value === 1 && !emailFormRef.value) return

  const validatePromise = step.value === 1
    ? emailFormRef.value.validate()
    : Promise.resolve(true)

  try {
    const valid = await validatePromise
    if (valid) {
      isLoading.value = true
      try {
        const response = await axiosClient.post('/auth/send-otp', {
          email: form.email,
          purpose: 'forgot-password'
        })
        ElMessage.success(response.data?.message || t('OTP code has been sent to your email', 'Đã gửi mã OTP đến email của bạn'))
        form.otp = ''
        step.value = 2
      } catch (error) {
        console.error('Send OTP error:', error)
        ElMessage.error(error.response?.data?.message || t('Failed to send OTP code', 'Có lỗi xảy ra khi gửi OTP'))
      } finally {
        isLoading.value = false
      }
    }
  } catch (err) {
    // validation failed
  }
}

const handleVerifyOtp = async () => {
  if (!form.otp || form.otp.length !== 6) {
    ElMessage.error(t('Please input 6-digit OTP code', 'Vui lòng nhập mã OTP 6 ký tự'))
    return
  }

  isLoading.value = true
  try {
    const response = await axiosClient.post('/auth/verify-otp', {
      email: form.email,
      otpCode: form.otp
    })

    if (response.data?.verified) {
      ElMessage.success(t('Email verification succeeded', 'Xác thực email thành công'))
      verifiedOtpToken.value = response.data?.otpToken || ''
      step.value = 3
    } else {
      ElMessage.error(t('Invalid OTP code', 'Mã OTP không hợp lệ hoặc đã hết hạn'))
    }
  } catch (error) {
    console.error('Verify OTP error:', error)
    ElMessage.error(error.response?.data?.message || t('Invalid OTP code', 'Mã OTP không hợp lệ hoặc đã hết hạn'))
  } finally {
    isLoading.value = false
  }
}

const handleResetPassword = async () => {
  if (!passwordFormRef.value) return

  await passwordFormRef.value.validate(async (valid) => {
    if (valid) {
      isLoading.value = true
      try {
        const response = await axiosClient.post('/auth/reset-password', {
          email: form.email,
          otpToken: verifiedOtpToken.value,
          newPassword: form.password
        })

        ElMessage.success(response.data?.message || t('Password reset succeeded. Please sign in.', 'Đặt lại mật khẩu thành công! Vui lòng đăng nhập.'))
        router.push('/login')
      } catch (error) {
        console.error('Reset password error:', error)
        ElMessage.error(error.response?.data?.message || t('Failed to reset password. API endpoint might be missing.', 'Lỗi khi đặt lại mật khẩu. API endpoint có thể chưa được hỗ trợ.'))
      } finally {
        isLoading.value = false
      }
    }
  })
}
</script>

<style scoped>
.auth-page {
  --auth-bg: #f6f9ff;
  --auth-surface: rgba(255, 255, 255, 0.88);
  --auth-surface-strong: #ffffff;
  --auth-text: #07142f;
  --auth-muted: #607086;
  --auth-border: rgba(40, 65, 105, 0.14);
  --auth-shadow: 0 28px 70px rgba(23, 47, 91, 0.14);
  min-height: 100vh;
  color: var(--auth-text);
  background:
    linear-gradient(180deg, #ffffff 0%, var(--auth-bg) 100%);
}

:global([data-theme='dark'] .auth-page) {
  --auth-bg: #071222;
  --auth-surface: rgba(13, 25, 45, 0.78);
  --auth-surface-strong: #101c32;
  --auth-text: #f7fbff;
  --auth-muted: #a8b8cf;
  --auth-border: rgba(148, 181, 222, 0.2);
  --auth-shadow: 0 30px 80px rgba(0, 0, 0, 0.38);
  background:
    linear-gradient(180deg, #07101e 0%, var(--auth-bg) 100%);
}

.container {
  width: min(1240px, calc(100% - 40px));
  margin: 0 auto;
}

.auth-navbar {
  border-bottom: 1px solid var(--auth-border);
  background: color-mix(in srgb, var(--auth-surface-strong) 84%, transparent);
  backdrop-filter: blur(18px);
}

.nav-content,
.nav-actions,
.logo,
.auth-container {
  display: flex;
  align-items: center;
}

.nav-content {
  min-height: 68px;
  justify-content: space-between;
  gap: 20px;
}

.logo {
  gap: 8px;
  color: var(--auth-text);
  font-size: 22px;
  font-weight: 900;
  text-decoration: none;
}

.custom-logo {
  display: block;
  width: 12px;
  height: 11px;
  flex: 0 0 12px;
  background: center / contain no-repeat url('/sprinta-mark-light.png');
}

:global([data-theme='dark'] .custom-logo) { background-image: url('/sprinta-mark-dark.png'); filter: none; }

.nav-actions {
  gap: 10px;
}

.theme-toggle,
.nav-primary,
.nav-link {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 38px;
  border: 0;
  border-radius: 14px;
  text-decoration: none;
  font-weight: 850;
}

.theme-toggle {
  width: 38px;
  color: var(--auth-text);
  border: 1px solid var(--auth-border);
  background: var(--auth-surface);
  cursor: pointer;
}

.nav-link {
  color: var(--auth-muted);
  padding: 0 12px;
}

.nav-primary {
  color: #ffffff;
  padding: 0 18px;
  background: linear-gradient(135deg, #00c2ff, #3662ff);
  box-shadow: 0 14px 30px rgba(54, 98, 255, 0.26);
}

.auth-container {
  width: min(1240px, calc(100% - 40px));
  min-height: calc(100vh - 79px);
  align-items: stretch;
  gap: 24px;
  margin: 0 auto;
  padding: 36px 0;
}

.auth-marketing,
.auth-card {
  border: 1px solid var(--auth-border);
  border-radius: 28px;
  background: var(--auth-surface);
  box-shadow: var(--auth-shadow);
  backdrop-filter: blur(18px);
}

.auth-marketing {
  flex: 1;
  padding: 30px;
}

.auth-card {
  width: min(100%, 470px);
  padding: 30px;
}

.eyebrow {
  width: fit-content;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border-radius: 999px;
  color: #0369a1;
  background: rgba(14, 165, 233, 0.12);
  font-size: 12px;
  font-weight: 900;
  text-transform: uppercase;
}

:global([data-theme='dark'] .eyebrow) {
  color: #7dd3fc;
  background: rgba(14, 165, 233, 0.16);
}

.auth-marketing h1 {
  margin: 18px 0 12px;
  max-width: 660px;
  font-size: clamp(28px, 2.6vw, 40px);
  line-height: 1.08;
  letter-spacing: 0;
}

.auth-marketing p,
.card-head p,
.auth-footer-text,
.auth-small-links,
.otp-instruction {
  color: var(--auth-muted);
  line-height: 1.6;
}

.recovery-panel {
  display: grid;
  grid-template-columns: 150px minmax(0, 1fr);
  gap: 14px;
  margin-top: 24px;
  padding: 14px;
  border: 1px solid var(--auth-border);
  border-radius: 24px;
  background: color-mix(in srgb, var(--auth-surface-strong) 82%, transparent);
}

.recovery-ring {
  min-height: 150px;
  display: grid;
  place-items: center;
  border-radius: 24px;
  color: #ffffff;
  background:
    conic-gradient(from 90deg, #00c2ff, #3662ff, #10b981, #00c2ff);
}

.recovery-list {
  display: grid;
  gap: 10px;
  align-content: center;
}

.recovery-list span {
  padding: 12px;
  border: 1px solid var(--auth-border);
  border-radius: 16px;
  color: var(--auth-muted);
  font-weight: 850;
  background: color-mix(in srgb, var(--auth-surface) 70%, transparent);
}

.recovery-list span.active {
  color: var(--auth-text);
  border-color: rgba(14, 165, 233, 0.5);
  background: rgba(14, 165, 233, 0.12);
}

.card-head h2 {
  margin: 8px 0;
  font-size: 28px;
  line-height: 1.1;
}

.card-kicker {
  color: #0ea5e9;
  font-size: 12px;
  font-weight: 900;
  text-transform: uppercase;
}

.auth-form {
  margin-top: 22px;
}

.entry-card {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  padding: 14px;
  border: 1px solid var(--auth-border);
  border-radius: 18px;
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--auth-surface-strong) 86%, transparent), color-mix(in srgb, #10b981 10%, transparent)),
}

.entry-icon {
  width: 42px;
  height: 42px;
  display: grid;
  place-items: center;
  flex: 0 0 auto;
  border-radius: 14px;
  color: #ffffff;
  background: linear-gradient(135deg, #10b981, #0ea5e9);
  box-shadow: 0 14px 28px rgba(16, 185, 129, 0.22);
}

.entry-card strong,
.entry-card span {
  display: block;
}

.entry-card strong {
  color: var(--auth-text);
  font-size: 15px;
}

.entry-card span {
  margin-top: 2px;
  color: var(--auth-muted);
  font-size: 13px;
  line-height: 1.45;
}

:deep(.el-form-item__label) {
  color: var(--auth-text);
  font-weight: 800;
}

:deep(.el-input__wrapper),
.auth-card :deep(.el-input__wrapper) {
  min-height: 44px !important;
  padding: 0 14px !important;
  border: 0 !important;
  border-radius: 14px;
  background: color-mix(in srgb, var(--auth-surface-strong) 96%, transparent);
  box-shadow: 0 0 0 1px var(--auth-border) inset !important;
}

:deep(.el-input) {
  --el-input-bg-color: transparent;
  --el-input-border-color: transparent;
  --el-input-hover-border-color: transparent;
  --el-input-focus-border-color: transparent;
}

:deep(.el-input__wrapper.is-focus) {
  box-shadow: 0 0 0 1px #34d399 inset, 0 0 0 4px rgba(16, 185, 129, 0.12) !important;
}

:deep(.el-input__inner),
.auth-card :deep(.el-input__inner) {
  height: 44px !important;
  padding: 0 !important;
  border: 0 !important;
  outline: none !important;
  background: transparent !important;
  color: var(--auth-text);
  font-weight: 700;
}

.auth-btn {
  width: 100%;
  min-height: 46px;
  border: 0;
  border-radius: 14px;
  font-weight: 900;
  background: linear-gradient(135deg, #00c2ff, #3662ff);
}

.otp-instruction {
  margin-bottom: 18px;
  padding: 14px;
  border: 1px solid var(--auth-border);
  border-radius: 16px;
  background: color-mix(in srgb, var(--auth-surface-strong) 78%, transparent);
}

.otp-input :deep(input) {
  text-align: center;
  letter-spacing: 8px;
  font-size: 20px;
  font-weight: 900;
}

.auth-footer-text {
  margin: 20px 0 0;
  text-align: center;
}

.auth-footer-text a,
.auth-small-links a {
  color: #0ea5e9;
  font-weight: 850;
  text-decoration: none;
}

.auth-small-links {
  display: flex;
  justify-content: center;
  gap: 10px;
  margin-top: 22px;
  font-size: 13px;
}

@media (max-width: 960px) {
  .auth-container {
    flex-direction: column;
  }

  .auth-card {
    width: 100%;
  }
}

@media (max-width: 640px) {
  .container,
  .auth-container {
    width: min(100% - 24px, 1180px);
  }

  .logo span,
  .nav-link {
    display: none;
  }

  .nav-content {
    min-height: 66px;
  }

  .auth-container {
    padding: 28px 0;
  }

  .auth-marketing,
  .auth-card {
    padding: 22px;
    border-radius: 22px;
  }

  .recovery-panel {
    grid-template-columns: 1fr;
  }
}
</style>
