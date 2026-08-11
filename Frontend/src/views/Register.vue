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
          <router-link class="nav-link" to="/login">{{ t('auth.nav.login') }}</router-link>
          <router-link class="nav-primary" to="/register">{{ t('auth.nav.register') }}</router-link>
        </div>
      </div>
    </header>

    <main class="auth-container">
      <section class="auth-marketing">
        <div class="eyebrow">
          <Sparkles :size="16" />
          Start sprinting
        </div>
        <h1>Tạo tài khoản và biến workspace đầu tiên thành một bảng quản lý thật nổi bật.</h1>
        <p>
          SprintA đưa khách mới đi từ email, OTP đến hồ sơ cá nhân trong một luồng sáng rõ,
          đồng thời show ngay các giá trị như sprint, board, reports và AI.
        </p>

        <div class="journey-card">
          <div v-for="item in journey" :key="item.title" class="journey-step" :class="{ active: item.step === step }">
            <span>{{ item.step }}</span>
            <div>
              <strong>{{ item.title }}</strong>
              <small>{{ item.text }}</small>
            </div>
          </div>
        </div>

        <div class="benefit-grid">
          <span><KanbanSquare :size="16" /> Board trực quan</span>
          <span><CalendarCheck :size="16" /> Sprint rõ ràng</span>
          <span><BarChart3 :size="16" /> Report tức thì</span>
        </div>
      </section>

      <section class="auth-card">
        <div class="card-head">
          <span class="card-kicker">Step {{ step }} / 3</span>
          <h2>
            {{ step === 1 ? t('auth.register.titleStep1') : step === 2 ? t('auth.register.titleStep2') : t('auth.register.titleStep3') }}
          </h2>
          <p>
            {{ 
              step === 1 ? t('auth.register.subtitleStep1') :
              step === 2 ? t('auth.register.subtitleStep2') :
              t('auth.register.subtitleStep3')
            }}
          </p>
        </div>

        <div class="step-track" aria-hidden="true">
          <span :class="{ active: step >= 1 }"></span>
          <span :class="{ active: step >= 2 }"></span>
          <span :class="{ active: step >= 3 }"></span>
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
          <div class="entry-card register-entry">
            <div class="entry-icon">
              <UserPlus :size="22" />
            </div>
            <div>
              <strong>Bắt đầu workspace mới</strong>
              <span>Nhập email để nhận OTP và tạo tài khoản SprintA.</span>
            </div>
          </div>

          <el-form-item :label="t('auth.register.emailLabel')" prop="email">
            <el-input v-model.trim="form.email" :placeholder="t('auth.register.emailPlaceholder')" size="large" />
          </el-form-item>
          
          <el-button type="primary" native-type="submit" class="auth-btn" size="large" :loading="isLoading">
            {{ t('auth.register.sendOtpBtn') }}
          </el-button>
          
          <p class="auth-footer-text">
            {{ t('auth.register.hasAccount') }} <router-link to="/login">{{ t('auth.nav.login') }}</router-link>
          </p>
        </el-form>

        <el-form v-else-if="step === 2" class="auth-form" @submit.prevent="handleVerifyOtp" label-position="top">
          <div class="otp-instruction" v-html="t('auth.register.otpInstructionHtml', { email: '<strong>' + form.email + '</strong>' })">
          </div>
          <el-form-item :label="t('auth.register.otpLabel')">
            <el-input v-model="form.otp" :placeholder="t('auth.register.otpPlaceholder')" size="large" maxlength="6" class="otp-input" />
          </el-form-item>
          
          <el-button type="primary" native-type="submit" class="auth-btn" size="large" :loading="isLoading">
            {{ t('auth.register.verifyOtpBtn') }}
          </el-button>
          
          <p class="auth-footer-text">
            {{ t('auth.register.noOtpCode') }}
            <a href="#" @click.prevent="step = 1">{{ t('auth.register.changeEmail') }}</a>
            {{ t('auth.register.or') }}
            <a href="#" @click.prevent="handleSendOtp">{{ t('auth.register.resendOtp') }}</a>
          </p>
        </el-form>

        <el-form 
          v-else 
          ref="profileFormRef"
          :model="form"
          :rules="rules"
          class="auth-form" 
          label-position="top"
          @submit.prevent="handleRegister"
        >
          <el-form-item :label="t('auth.register.fullNameLabel')" prop="fullName">
            <el-input v-model="form.fullName" :placeholder="t('auth.register.fullNamePlaceholder')" size="large" />
          </el-form-item>

          <el-form-item :label="t('auth.register.passwordLabel')" prop="password">
            <el-input v-model="form.password" type="password" :placeholder="t('auth.register.passwordPlaceholder')" size="large" show-password />
          </el-form-item>
          
          <el-button type="primary" native-type="submit" class="auth-btn" size="large" :loading="isLoading">
            {{ t('auth.register.submitBtn') }}
          </el-button>
        </el-form>

        <div class="auth-small-links">
          <a href="#">{{ t('auth.register.terms') }}</a>
          <span>&bull;</span>
          <a href="#">{{ t('auth.register.privacy') }}</a>
        </div>
      </section>
    </main>
  </div>
</template>

<script setup>
import { ref, reactive, computed } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import {
  BarChart3,
  CalendarCheck,
  KanbanSquare,
  Moon,
  Sparkles,
  Sun,
  UserPlus
} from 'lucide-vue-next'
import axiosClient from '../api/axiosClient'
import { useI18n } from '@/composables/useI18n'
import { currentTheme, toggleTheme } from '@/utils/theme'

const router = useRouter()
const { t } = useI18n()

const journey = [
  { step: 1, title: 'Email', text: 'Nhận mã OTP bảo mật' },
  { step: 2, title: 'Xác thực', text: 'Kiểm tra đúng người dùng' },
  { step: 3, title: 'Workspace', text: 'Hoàn tất tài khoản' }
]

const isLoading = ref(false)
const emailFormRef = ref(null)
const profileFormRef = ref(null)
const step = ref(1)
const verifiedOtpToken = ref('')

const form = reactive({
  email: '',
  otp: '',
  fullName: '',
  password: ''
})

const rules = computed(() => ({
  email: [
    { required: true, message: t('auth.register.rules.emailRequired'), trigger: 'blur' },
    { type: 'email', message: t('auth.register.rules.emailInvalid'), trigger: ['blur', 'change'] }
  ],
  fullName: [
    { required: true, message: t('auth.register.rules.nameRequired'), trigger: 'blur' },
    { min: 2, message: t('auth.register.rules.nameMin'), trigger: 'blur' }
  ],
  password: [
    { required: true, message: t('auth.register.rules.passwordRequired'), trigger: 'blur' },
    { min: 6, message: t('auth.register.rules.passwordMin'), trigger: 'blur' },
    { 
      pattern: /^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{6,}$/, 
      message: t('auth.register.rules.passwordComplexity'),
      trigger: 'blur' 
    }
  ]
}))

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
          purpose: 'register'
        })
        ElMessage.success(response.data.message || t('auth.forgotPassword.otpSentMessage'))
        form.otp = ''
        step.value = 2
      } catch (error) {
        console.error('Send OTP error:', error)
        ElMessage.error(error.response?.data?.message || t('auth.register.messages.sendOtpFailed'))
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
    ElMessage.error(t('auth.register.messages.invalidOtpFormat'))
    return
  }
  
  isLoading.value = true
  try {
    const response = await axiosClient.post('/auth/verify-otp', { 
      email: form.email, 
      otpCode: form.otp 
    })
    
    if (response.data.verified) {
      ElMessage.success(t('auth.register.messages.otpVerified'))
      verifiedOtpToken.value = response.data.otpToken
      step.value = 3
    }
  } catch (error) {
    console.error('Verify OTP error:', error)
    ElMessage.error(error.response?.data?.message || t('auth.messages.otpInvalid'))
  } finally {
    isLoading.value = false
  }
}

const handleRegister = async () => {
  if (!profileFormRef.value) return
  
  await profileFormRef.value.validate(async (valid) => {
    if (valid) {
      isLoading.value = true
      try {
        await axiosClient.post('/auth/register', { 
          email: form.email,
          fullName: form.fullName,
          password: form.password,
          otpCode: verifiedOtpToken.value
        })
        
        ElMessage.success(t('auth.register.messages.registerSuccess'))
        router.push('/login')
      } catch (error) {
        console.error('Register error:', error)
        ElMessage.error(error.response?.data?.message || t('auth.register.messages.registerFailed'))
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
.auth-container,
.benefit-grid {
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
  font-size: clamp(28px, 2.6vw, 40px);
  line-height: 1.08;
  letter-spacing: 0;
}

.auth-marketing p,
.card-head p,
.auth-footer-text,
.auth-small-links {
  color: var(--auth-muted);
  line-height: 1.6;
}

.journey-card {
  display: grid;
  gap: 12px;
  margin-top: 24px;
  padding: 14px;
  border: 1px solid var(--auth-border);
  border-radius: 24px;
  background: color-mix(in srgb, var(--auth-surface-strong) 82%, transparent);
}

.journey-step {
  display: flex;
  gap: 12px;
  align-items: center;
  padding: 12px;
  border: 1px solid var(--auth-border);
  border-radius: 18px;
  background: color-mix(in srgb, var(--auth-surface) 70%, transparent);
}

.journey-step.active {
  border-color: rgba(14, 165, 233, 0.5);
  background: rgba(14, 165, 233, 0.12);
}

.journey-step span {
  width: 34px;
  height: 34px;
  display: grid;
  place-items: center;
  border-radius: 12px;
  color: #ffffff;
  font-weight: 900;
  background: linear-gradient(135deg, #0ea5e9, #7c3aed);
}

.journey-step strong,
.journey-step small {
  display: block;
}

.journey-step small {
  color: var(--auth-muted);
}

.benefit-grid {
  flex-wrap: wrap;
  gap: 10px;
  margin-top: 18px;
}

.benefit-grid span {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  border: 1px solid var(--auth-border);
  border-radius: 999px;
  color: var(--auth-text);
  background: var(--auth-surface-strong);
  font-weight: 850;
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

.step-track {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
  margin: 18px 0 20px;
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
    linear-gradient(135deg, color-mix(in srgb, var(--auth-surface-strong) 86%, transparent), color-mix(in srgb, #7c3aed 10%, transparent)),
}

.entry-icon {
  width: 42px;
  height: 42px;
  display: grid;
  place-items: center;
  flex: 0 0 auto;
  border-radius: 14px;
  color: #ffffff;
  background: linear-gradient(135deg, #0ea5e9, #7c3aed);
  box-shadow: 0 14px 28px rgba(59, 130, 246, 0.22);
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

.step-track span {
  height: 8px;
  border-radius: 999px;
  background: color-mix(in srgb, var(--auth-muted) 18%, transparent);
}

.step-track span.active {
  background: linear-gradient(90deg, #00c2ff, #3662ff);
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
  box-shadow: 0 0 0 1px #38bdf8 inset, 0 0 0 4px rgba(14, 165, 233, 0.12) !important;
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
  color: var(--auth-muted);
  background: color-mix(in srgb, var(--auth-surface-strong) 78%, transparent);
  line-height: 1.6;
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
}
</style>
