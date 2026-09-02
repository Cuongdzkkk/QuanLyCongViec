<template>
  <div class="auth-page">
    <header class="auth-navbar">
      <div class="container nav-content">
        <router-link to="/" class="logo">
          <SprintaBrand size="compact" />
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
          SprintA workspace
        </div>
        <h1>Đăng nhập để tiếp tục quản lý sprint, báo cáo và công việc trong một bảng điều khiển.</h1>
        <p>
          Một trải nghiệm gọn hơn cho đội nhóm: theo dõi việc đang mở, chu kỳ, tải công việc và cảnh báo quá hạn
          với giao diện thích ứng sáng tối.
        </p>

        <div class="mini-dashboard">
          <div class="mini-header">
            <span></span>
            <strong>Today overview</strong>
          </div>
          <div class="mini-metrics">
            <article>
              <CheckCircle2 :size="18" />
              <strong>13</strong>
              <span>Tasks</span>
            </article>
            <article>
              <KanbanSquare :size="18" />
              <strong>4</strong>
              <span>Boards</span>
            </article>
            <article>
              <BarChart3 :size="18" />
              <strong>86%</strong>
              <span>Health</span>
            </article>
          </div>
          <div class="chart-lines">
            <span></span>
            <span></span>
            <span></span>
          </div>
        </div>

        <div class="benefit-grid">
          <span><ShieldCheck :size="16" /> OTP & bảo mật</span>
          <span><Zap :size="16" /> Tạo việc nhanh</span>
          <span><BarChart3 :size="16" /> Báo cáo rõ</span>
        </div>
      </section>

      <section class="auth-card">
        <div class="card-head">
          <span class="card-kicker">{{ requires2FA ? 'Two-factor' : 'Welcome back' }}</span>
          <h2>{{ requires2FA ? t('auth.otp.title') : t('auth.login.title') }}</h2>
          <p>
            {{ requires2FA
              ? t('auth.otp.subtitle')
              : t('auth.login.subtitle') }}
          </p>
        </div>

        <el-form v-if="requires2FA" class="auth-form" @submit.prevent="handleLogin2FA" label-position="top">
          <el-form-item :label="t('auth.otp.codeLabel')">
            <el-input v-model="otpCode" :placeholder="t('auth.otp.codePlaceholder')" size="large" />
          </el-form-item>

          <el-button type="primary" native-type="submit" class="auth-btn" size="large" :loading="isLoading">
            {{ t('auth.otp.verify') }}
          </el-button>

          <p class="auth-footer-text">
            {{ t('auth.otp.backPrompt') }}
            <button type="button" class="link-btn" @click="requires2FA = false">{{ t('auth.otp.backLink') }}</button>
          </p>
        </el-form>

        <el-form v-else class="auth-form" @submit.prevent="handleLogin" label-position="top">
          <el-form-item :label="t('auth.login.emailLabel')">
            <el-input v-model="form.email" name="email" autocomplete="email" :placeholder="t('auth.login.emailPlaceholder')" size="large" />
          </el-form-item>

          <el-form-item :label="t('auth.login.passwordLabel')">
            <el-input
              v-model="form.password"
              name="password"
              autocomplete="current-password"
              type="password"
              :placeholder="t('auth.login.passwordPlaceholder')"
              size="large"
              show-password
            />
          </el-form-item>

          <div class="remember-action">
            <router-link to="/forgot-password" class="forgot-link">{{ t('auth.forgotPassword.title') }}?</router-link>
          </div>

          <el-button
            type="primary"
            native-type="submit"
            class="auth-btn"
            size="large"
            :loading="isLoading"
            :disabled="googleVerifying"
          >
            {{ t('auth.login.submit') }}
          </el-button>
        </el-form>

        <div v-if="!requires2FA" class="social-shell">
          <div class="divider"><span>{{ t('auth.login.orContinueWith') }}</span></div>

          <div class="social-login">
            <div
              v-if="shouldShowGoogleIdentityShell"
              class="google-identity-shell"
              :class="{ 'is-busy': googleVerifying || isLoading }"
              :aria-busy="googleVerifying || isLoading"
            >
              <button
                type="button"
                class="social-btn google-btn"
                aria-label="Đăng nhập bằng Google"
                :disabled="googleSdkState !== 'ready' || googleVerifying || isLoading"
                @click="startGoogleAuthorization"
              >
                <img :src="googleIcon" alt="" class="social-icon" />
                <span>Google</span>
              </button>
              <div v-if="googleVerifying" class="google-verifying-overlay" aria-live="polite">
                <i class="fa-solid fa-spinner fa-spin" aria-hidden="true"></i>
                <span>{{ tr('Verifying...', 'Đang xác minh...') }}</span>
              </div>
            </div>
            <el-button
              v-else
              native-type="button"
              class="social-btn google-btn"
              aria-label="Đăng nhập bằng Google"
              @click="handleGoogleLoginNotConfigured"
            >
              <img :src="googleIcon" alt="" class="social-icon" /> Google
            </el-button>

            <el-button
              native-type="button"
              class="social-btn github-btn"
              aria-label="Đăng nhập bằng GitHub"
              @click="handleGitHubLogin"
            >
              <img :src="githubIcon" alt="" class="social-icon" /> GitHub
            </el-button>
          </div>

          <div v-if="googleError" class="google-auth-error" role="alert">
            <i class="fa-solid fa-circle-exclamation" aria-hidden="true"></i>
            <span>{{ googleError.message }}</span>
            <button v-if="googleError.retryable" type="button" @click="retryGoogleSetup">
              {{ tr('Retry', 'Thử lại') }}
            </button>
          </div>

          <p class="auth-footer-text">
            {{ t('auth.login.noAccount') }} <router-link to="/register">{{ t('auth.nav.register') }}</router-link>
          </p>
        </div>
      </section>
    </main>

    <footer class="auth-bottom">
      <p>&copy; 2026 SprintA. {{ t('auth.footer.rights') }}</p>
    </footer>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import {
  BarChart3,
  CheckCircle2,
  KanbanSquare,
  Moon,
  ShieldCheck,
  Sparkles,
  Sun,
  Zap
} from 'lucide-vue-next'
import axiosClient from '../api/axiosClient'
import {
  loginWithGoogleAuthorizationCode,
  startGoogleAuthorizationCodeLogin
} from '@/api/authApi'
import { saveAuthSession } from '@/utils/authSession'
import SprintaBrand from '@/components/branding/SprintaBrand.vue'
import { useI18n } from '@/composables/useI18n'
import { currentTheme, toggleTheme } from '@/utils/theme'
import { registerGoogleAuthorizationCodeClient } from '@/services/googleIdentityService'
import googleIcon from '../assets/Icongoogle.png'
import githubIcon from '../assets/Icongithub.png'

const router = useRouter()
const route = useRoute()
const { t, language } = useI18n()
const tr = (en, vi) => {
  const fallback = 'Google Sign-In chưa được cấu hình.'
  if (language?.value === 'vi') return vi || en || fallback
  return en || vi || fallback
}

const form = reactive({
  email: '',
  password: ''
})

const isLoading = ref(false)
const requires2FA = ref(false)
const otpCode = ref('')
const googleSdkState = ref('idle')
const googleVerifying = ref(false)
const googleError = ref(null)
let googleAuthorizationClient = null
let googleLoginAbortController = null
let googleRequestId = 0
let componentActive = true

const getSafeRedirect = () => {
  const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : ''
  return redirect.startsWith('/') && !redirect.startsWith('//') ? redirect : '/site-selection'
}

const handleLogin = async () => {
  if (googleVerifying.value) return

  if (!form.email || !form.password) {
    ElMessage.warning(t('auth.messages.missingCredentials'))
    return
  }

  isLoading.value = true
  try {
    const response = await axiosClient.post('/auth/login', {
      email: form.email,
      password: form.password
    })

    if (response.data.requires2FA) {
      requires2FA.value = true
      ElMessage.success(t('auth.messages.requires2fa'))
      return
    }

    saveAuthSession(response.data.data)
    ElMessage.success(t('auth.messages.loginSuccess'))
    router.push(getSafeRedirect())
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('auth.messages.loginFailed'))
  } finally {
    isLoading.value = false
  }
}

const handleLogin2FA = async () => {
  if (!otpCode.value) {
    ElMessage.warning(t('auth.messages.missingOtp'))
    return
  }

  isLoading.value = true
  try {
    const response = await axiosClient.post('/auth/login-2fa', {
      email: form.email,
      password: form.password,
      otpCode: otpCode.value
    })

    saveAuthSession(response.data.data)
    ElMessage.success(t('auth.messages.loginSuccess'))
    router.push(getSafeRedirect())
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('auth.messages.otpInvalid'))
  } finally {
    isLoading.value = false
  }
}

const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID || ''
const hasGoogleClientId = Boolean(googleClientId && googleClientId !== 'CHANGE_ME_USE_LOCAL_ENV')
const shouldShowGoogleIdentityShell = computed(() => hasGoogleClientId)

const handleGoogleLoginNotConfigured = () => {
  ElMessage.error(tr('Google sign-in is not configured.', 'Google Sign-In chưa được cấu hình.'))
}

const getGoogleError = (error) => {
  const status = Number(error?.response?.status || error?.status || 0)
  if (status === 400) {
    return {
      status,
      message: tr(
        'The Google sign-in request could not be processed.',
        'Không thể xử lý yêu cầu đăng nhập Google.'
      ),
      retryable: true
    }
  }
  if (status === 401) {
    return {
      status,
      message: tr(
        'Your Google credential is invalid or expired. Please sign in with Google again.',
        'Phiên xác thực Google không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập Google lại.'
      ),
      retryable: true
    }
  }
  if (status === 403) {
    return {
      status,
      message: tr(
        'This account is inactive or is not allowed to sign in.',
        'Tài khoản này đang bị khóa hoặc không được phép đăng nhập.'
      ),
      retryable: false
    }
  }
  if (status === 409) {
    return {
      status,
      message: tr(
        'This Google account is not safely linked to the existing account.',
        'Tài khoản Google này chưa được liên kết an toàn với tài khoản hiện có.'
      ),
      retryable: false
    }
  }
  if (status === 503) {
    return {
      status,
      message: tr(
        'Google sign-in is temporarily unavailable. Please try again.',
        'Đăng nhập Google tạm thời không khả dụng. Vui lòng thử lại.'
      ),
      retryable: true
    }
  }
  return {
    status,
    message: tr(
      'Google sign-in could not be completed. Please try again.',
      'Không thể hoàn tất đăng nhập Google. Vui lòng thử lại.'
    ),
    retryable: true
  }
}

const handleGoogleLogin = async (response) => {
  if (!componentActive || isLoading.value) return

  const code = typeof response?.code === 'string'
    ? response.code.trim()
    : ''
  const state = typeof response?.state === 'string'
    ? response.state.trim()
    : ''
  if (!code || !state) {
    googleError.value = getGoogleError({ status: 400 })
    ElMessage.error(googleError.value.message)
    return
  }

  googleVerifying.value = true
  googleError.value = null
  googleLoginAbortController?.abort()
  const controller = new AbortController()
  googleLoginAbortController = controller
  const requestId = ++googleRequestId
  let succeeded = false

  try {
    const authData = await loginWithGoogleAuthorizationCode(code, state, {
      signal: controller.signal
    })
    if (!componentActive || requestId !== googleRequestId) return

    saveAuthSession(authData)
    succeeded = true
    ElMessage.success(t('auth.messages.googleSuccess'))
    await router.push(getSafeRedirect())
  } catch (error) {
    if (
      error?.name === 'CanceledError' ||
      error?.name === 'AbortError' ||
      error?.code === 'ERR_CANCELED' ||
      !componentActive ||
      requestId !== googleRequestId
    ) {
      return
    }
    googleError.value = getGoogleError(error)
    ElMessage.error(googleError.value.message)
  } finally {
    if (requestId === googleRequestId && !succeeded) {
      googleVerifying.value = false
      setupGoogleIdentity()
    }
  }
}

const handleGoogleAuthorizationError = () => {
  googleVerifying.value = false
  googleError.value = getGoogleError({ status: 400 })
}

const startGoogleAuthorization = () => {
  if (googleSdkState.value !== 'ready' || !googleAuthorizationClient || googleVerifying.value || isLoading.value) {
    return
  }

  googleVerifying.value = true
  googleError.value = null
  try {
    googleAuthorizationClient.requestCode()
  } catch (error) {
    googleVerifying.value = false
    googleError.value = getGoogleError(error)
    setupGoogleIdentity()
  }
}

const setupGoogleIdentity = async () => {
  if (!hasGoogleClientId || !componentActive) return
  googleSdkState.value = 'loading'
  googleError.value = null

  try {
    googleAuthorizationClient?.release()
    googleAuthorizationClient = null
    const state = await startGoogleAuthorizationCodeLogin()
    googleAuthorizationClient = await registerGoogleAuthorizationCodeClient({
      clientId: googleClientId,
      state,
      callback: handleGoogleLogin,
      errorCallback: handleGoogleAuthorizationError
    })
    if (!componentActive) {
      googleAuthorizationClient?.release()
      googleAuthorizationClient = null
      return
    }

    googleSdkState.value = 'ready'
  } catch {
    if (!componentActive) return
    googleAuthorizationClient?.release()
    googleAuthorizationClient = null
    googleSdkState.value = 'error'
    googleError.value = {
      status: 0,
      message: tr(
        'Google sign-in could not be loaded. Email and password sign-in is still available.',
        'Không thể tải đăng nhập Google. Bạn vẫn có thể đăng nhập bằng email và mật khẩu.'
      ),
      retryable: true
    }
  }
}

const retryGoogleSetup = () => {
  if (googleVerifying.value) return
  googleError.value = null
  setupGoogleIdentity()
}

onMounted(setupGoogleIdentity)

onBeforeUnmount(() => {
  componentActive = false
  googleRequestId += 1
  googleLoginAbortController?.abort()
  googleAuthorizationClient?.release()
  googleAuthorizationClient = null
})

const handleGitHubLogin = () => {
  const clientId = import.meta.env.VITE_GITHUB_CLIENT_ID
  if (!clientId || clientId === 'CHANGE_ME_USE_LOCAL_ENV') {
    ElMessage.error('GitHub OAuth chưa được cấu hình.')
    return
  }
  const redirectUri = import.meta.env.VITE_GITHUB_REDIRECT_URI || `${window.location.origin}/auth/github/callback`
  const githubAuthUrl = `https://github.com/login/oauth/authorize?client_id=${clientId}&redirect_uri=${encodeURIComponent(redirectUri)}&scope=user:email`
  window.location.href = githubAuthUrl
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
  display: flex;
  flex-direction: column;
  overflow-x: hidden;
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
.benefit-grid,
.social-login {
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
  flex: 1;
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
  position: relative;
  flex: 1;
  overflow: hidden;
  padding: 30px;
}

.auth-marketing::after {
  content: '';
  position: absolute;
  width: 280px;
  height: 280px;
  right: -90px;
  top: -80px;
  border-radius: 50%;
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
  position: relative;
  z-index: 1;
  max-width: 620px;
  margin: 18px 0 12px;
  color: var(--auth-text);
  font-size: clamp(28px, 2.6vw, 40px);
  line-height: 1.08;
  letter-spacing: 0;
}

.auth-marketing p,
.card-head p,
.auth-footer-text {
  color: var(--auth-muted);
  line-height: 1.6;
}

.auth-marketing p {
  position: relative;
  z-index: 1;
  max-width: 600px;
  margin: 0;
  font-size: 16px;
}

.mini-dashboard {
  position: relative;
  z-index: 1;
  margin-top: 24px;
  padding: 14px;
  border: 1px solid var(--auth-border);
  border-radius: 24px;
  background: color-mix(in srgb, var(--auth-surface-strong) 82%, transparent);
}

.mini-header {
  display: flex;
  align-items: center;
  gap: 10px;
  color: var(--auth-text);
  font-weight: 900;
}

.mini-header span {
  width: 12px;
  height: 12px;
  border-radius: 999px;
  background: #22c55e;
  box-shadow: 0 0 0 6px rgba(34, 197, 94, 0.12);
}

.mini-metrics {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;
  margin-top: 18px;
}

.mini-metrics article {
  padding: 14px;
  border-radius: 18px;
  color: var(--auth-text);
  border: 1px solid var(--auth-border);
  background: linear-gradient(135deg, rgba(14, 165, 233, 0.12), rgba(99, 102, 241, 0.08));
}

.mini-metrics strong,
.mini-metrics span {
  display: block;
}

.mini-metrics strong {
  margin-top: 8px;
  font-size: 24px;
}

.mini-metrics span {
  color: var(--auth-muted);
  font-weight: 800;
}

.chart-lines {
  display: grid;
  gap: 10px;
  margin-top: 18px;
}

.chart-lines span {
  height: 10px;
  border-radius: 999px;
  background: linear-gradient(90deg, #22d3ee, #3662ff 62%, transparent 62%);
}

.chart-lines span:nth-child(2) {
  background: linear-gradient(90deg, #f59e0b, #8b5cf6 48%, transparent 48%);
}

.chart-lines span:nth-child(3) {
  background: linear-gradient(90deg, #22c55e, #22d3ee 78%, transparent 78%);
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

.auth-card {
  width: min(100%, 470px);
  padding: 30px;
}

.card-head h2 {
  margin: 8px 0 8px;
  color: var(--auth-text);
  font-size: 28px;
  line-height: 1.1;
}

.card-kicker {
  color: #0ea5e9;
  font-size: 12px;
  font-weight: 900;
  text-transform: uppercase;
}

.auth-form,
.social-shell {
  margin-top: 24px;
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

.remember-action {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 18px;
}

.forgot-link,
.auth-footer-text a,
.link-btn {
  color: #0ea5e9;
  font-weight: 850;
  text-decoration: none;
}

.auth-btn {
  width: 100%;
  min-height: 46px;
  border: 0;
  border-radius: 14px;
  font-weight: 900;
  background: linear-gradient(135deg, #00c2ff, #3662ff);
}

.divider {
  position: relative;
  margin: 22px 0 18px;
  text-align: center;
  color: var(--auth-muted);
  font-size: 12px;
  font-weight: 900;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.divider::before {
  content: '';
  position: absolute;
  left: 0;
  right: 0;
  top: 50%;
  height: 1px;
  background: var(--auth-border);
}

.divider span {
  position: relative;
  z-index: 1;
  padding: 0 10px;
  background: var(--auth-surface);
}

.social-login {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
  align-items: stretch;
}

.social-btn-wrapper,
.social-btn {
  flex: 1;
}

.google-identity-shell {
  position: relative;
  min-width: 0;
  flex: 1;
  width: 100%;
  display: flex;
  align-items: stretch;
}

.google-identity-shell.is-busy {
  pointer-events: none;
}

.google-verifying-overlay {
  position: absolute;
  inset: 0;
  z-index: 2;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border: 1px solid var(--auth-border);
  border-radius: 10px;
  color: var(--auth-text);
  background: color-mix(in srgb, var(--auth-surface-strong) 94%, transparent);
  font-size: 13px;
  font-weight: 800;
}

.google-auth-error {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  margin-top: 12px;
  padding: 10px 12px;
  border: 1px solid color-mix(in srgb, #dc2626 35%, var(--auth-border));
  border-radius: 8px;
  color: color-mix(in srgb, #dc2626 78%, var(--auth-text));
  background: color-mix(in srgb, #dc2626 8%, var(--auth-surface-strong));
  font-size: 12px;
  line-height: 1.5;
}

.google-auth-error span {
  min-width: 0;
  flex: 1;
}

.google-auth-error button {
  flex: 0 0 auto;
  border: 0;
  color: inherit;
  background: transparent;
  font: inherit;
  font-weight: 850;
  cursor: pointer;
}

.google-auth-error button:focus-visible {
  border-radius: 4px;
  outline: 3px solid color-mix(in srgb, currentColor 28%, transparent);
  outline-offset: 2px;
}

.social-btn {
  width: 100%;
  height: 44px;
  min-height: 44px;
  border-radius: 14px;
}

button.social-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0 14px;
  font: inherit;
  font-weight: 800;
  cursor: pointer;
}

button.social-btn:disabled {
  cursor: not-allowed;
  opacity: .72;
  transform: none;
}

.social-icon {
  width: 18px;
  height: 18px;
  flex: 0 0 18px;
  margin-right: 8px;
}

.auth-footer-text {
  margin: 20px 0 0;
  text-align: center;
}

.link-btn {
  border: none;
  background: transparent;
  cursor: pointer;
  font: inherit;
  padding: 0;
}

.auth-bottom {
  padding: 0 16px 26px;
  text-align: center;
  color: var(--auth-muted);
  font-size: 14px;
}

/* Keep saved credentials visually inside the product theme. */
.auth-card :deep(.el-form-item__label) {
  margin-bottom: 7px;
  color: var(--auth-muted);
  font-size: 12px;
  font-weight: 750;
  letter-spacing: .025em;
}

.auth-card :deep(.el-input__wrapper) {
  min-height: 46px;
  padding-inline: 14px;
  border-radius: 10px;
  background: color-mix(in srgb, var(--auth-card) 92%, var(--sp-blue-600) 8%) !important;
  box-shadow: 0 0 0 1px var(--auth-border) inset !important;
}

.auth-card :deep(.el-input__wrapper:hover) {
  box-shadow: 0 0 0 1px color-mix(in srgb, var(--sp-sky-400) 65%, var(--auth-border)) inset !important;
}

.auth-card :deep(.el-input__inner) {
  color: var(--auth-text) !important;
  font-size: 14px;
  font-weight: 600;
}

.auth-card :deep(input:-webkit-autofill),
.auth-card :deep(input:-webkit-autofill:hover),
.auth-card :deep(input:-webkit-autofill:focus) {
  -webkit-text-fill-color: var(--auth-text) !important;
  caret-color: var(--auth-text);
  transition: background-color 9999s ease-out 0s;
  box-shadow: 0 0 0 1000px color-mix(in srgb, var(--auth-card) 92%, var(--sp-blue-600) 8%) inset !important;
}

.auth-btn {
  min-height: 44px;
  border-radius: 10px !important;
  background: var(--sp-blue-700) !important;
  box-shadow: 0 8px 20px color-mix(in srgb, var(--sp-blue-700) 24%, transparent) !important;
}

.social-btn {
  height: 44px;
  min-height: 44px;
  border: 1px solid var(--auth-border) !important;
  border-radius: 10px !important;
  color: var(--auth-text) !important;
  background: color-mix(in srgb, var(--auth-card) 96%, var(--sp-slate-500) 4%) !important;
  box-shadow: none !important;
}

.social-btn:hover {
  border-color: color-mix(in srgb, var(--sp-sky-400) 58%, var(--auth-border)) !important;
  background: color-mix(in srgb, var(--auth-card) 90%, var(--sp-blue-600) 10%) !important;
  transform: translateY(-1px);
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

  .mini-metrics,
  .social-login {
    grid-template-columns: 1fr;
    flex-direction: column;
  }

  .google-identity-shell,
  .google-identity-button {
    width: 100%;
  }

  .remember-action {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
