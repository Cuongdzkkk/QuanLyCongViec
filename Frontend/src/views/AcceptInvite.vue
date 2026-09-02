<template>
  <div class="invite-page">
    <!-- Blurred skeleton background to mimic dashboard -->
    <div class="skeleton-bg">
      <div class="sk-topbar"></div>
      <div class="sk-body">
        <div class="sk-sidebar"></div>
        <div class="sk-content">
          <div class="sk-header"></div>
          <div class="sk-board">
            <div class="sk-col">
              <div class="sk-card"></div>
              <div class="sk-card"></div>
              <div class="sk-card"></div>
            </div>
            <div class="sk-col">
              <div class="sk-card"></div>
            </div>
            <div class="sk-col">
              <div class="sk-card"></div>
              <div class="sk-card"></div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Main Modal -->
    <div class="invite-card">
      <div class="brand-row">
        <SprintaBrand size="dialog" />
      </div>

      <div v-if="loading" class="state-block">
        <i class="fa-solid fa-spinner fa-spin"></i>
        <h1>Đang kiểm tra thông tin...</h1>
        <p>SprintA đang xác minh liên kết của bạn.</p>
      </div>

      <div v-else-if="error" class="state-block">
        <i class="fa-solid fa-circle-xmark danger"></i>
        <h1>Liên kết không hợp lệ</h1>
        <p>{{ error }}</p>
        <button type="button" class="primary-btn" @click="router.push('/login')">
          Về trang đăng nhập
        </button>
      </div>

      <div v-else-if="success" class="state-block">
        <i class="fa-solid fa-circle-check success"></i>
        <h1>Đã thiết lập tài khoản</h1>
        <p v-if="requiresLogin">
          Tài khoản đã sẵn sàng cho <strong>{{ invite.email }}</strong>.
          Hãy đăng nhập để tiếp tục.
        </p>
        <p v-else>
          Tài khoản của bạn đã sẵn sàng. Bạn sẽ được chuyển tới bảng điều khiển.
        </p>
        <button type="button" class="primary-btn mt-24" @click="continueAfterSuccess">
          {{ requiresLogin ? 'Đăng nhập' : 'Vào SprintA' }}
        </button>
      </div>

      <template v-else>
        <!-- STEP 1: OTP VERIFICATION -->
        <div v-if="step === 1" class="step-container text-center">
          <h1>Mã xác thực đã được gửi</h1>
          <p class="subtitle">
            Để hoàn tất việc thiết lập tài khoản, vui lòng nhập mã xác thực gồm 6 chữ số mà chúng tôi đã gửi tới:<br/>
            <strong>{{ maskedEmail }}</strong>
          </p>

          <div class="otp-inputs">
            <input 
              v-for="(digit, index) in otpDigits" 
              :key="index" 
              ref="otpRefs"
              v-model="otpDigits[index]" 
              maxlength="1" 
              class="otp-box"
              @input="onOtpInput(index)"
              @keydown.delete="onOtpDelete($event, index)"
              @paste.prevent="onOtpPaste"
            />
          </div>

          <button
            type="button"
            class="primary-btn"
            :disabled="!isOtpComplete || isVerifyingOtp"
            @click="verifyOtp"
          >
            <i v-if="isVerifyingOtp" class="fa-solid fa-spinner fa-spin"></i>
            <span>Xác thực</span>
          </button>

          <div class="resend-link">
            <span v-if="isSendingOtp" class="resend-loading">
              <i class="fa-solid fa-spinner fa-spin"></i> Đang gửi lại...
            </span>
            <span v-else-if="otpCooldown > 0" class="resend-cooldown">
              Gửi lại sau {{ otpCooldown }}s
            </span>
            <a v-else href="#" @click.prevent="sendOtp">Chưa nhận được mã? Gửi lại email</a>
          </div>
        </div>

        <!-- STEP 2: FINISH SETTING UP ACCOUNT / REGISTERED USERS -->
        <div v-if="step === 2" class="step-container">
          <h1 class="text-center">{{ invite.requiresAccountSetup ? 'Hoàn tất thiết lập tài khoản' : 'Tài khoản đã tồn tại' }}</h1>

          <div class="verified-email">
            <span class="label">Địa chỉ email</span> <i class="fa-solid fa-circle-check text-success"></i>
            <br>
            <strong>{{ invite.email }}</strong>
          </div>

          <p v-if="!invite.requiresAccountSetup" class="subtitle text-center">
            Bạn đã có tài khoản trên hệ thống. Chỉ cần chấp nhận lời mời này để tham gia dự án.
          </p>

          <div v-if="accountMismatch" class="account-mismatch" role="alert">
            Lời mời này dành cho <strong>{{ invite.email }}</strong>. Bạn đang đăng nhập bằng tài khoản khác.
            <button type="button" class="secondary-btn" @click="switchAccount">Đăng xuất và chuyển tài khoản</button>
          </div>

          <div v-else-if="invite.requiresAccountSetup" class="invite-form">
            <div class="field">
              <span class="field-label">Họ và tên *</span>
              <input v-model="form.fullName" type="text" placeholder="Nhập họ tên của bạn" />
            </div>

            <div class="field">
              <span class="field-label">Mật khẩu *</span>
              <input v-model="form.password" type="password" placeholder="Tạo mật khẩu" />
              <p class="password-hint">Ít nhất 6 ký tự, bao gồm chữ hoa, số và ký tự đặc biệt.</p>
            </div>

            <div class="terms-note">
              Bằng cách nhấp vào "Tiếp tục", bạn đồng ý với các <a href="#">Điều khoản Dịch vụ</a> của SprintA.
            </div>
          </div>

          <button
            type="button"
            class="primary-btn"
            :disabled="isSubmitting || accountMismatch"
            @click="acceptInvite"
          >
            <i v-if="isSubmitting" class="fa-solid fa-spinner fa-spin"></i>
            <span>{{ invite.requiresAccountSetup ? 'Tiếp tục' : 'Chấp nhận lời mời' }}</span>
          </button>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup>
  import { onMounted, reactive, ref, computed, nextTick } from 'vue'
  import { useRoute, useRouter } from 'vue-router'
  import { ElMessage } from 'element-plus'
  import axiosClient from '@/api/axiosClient'
import { clearAuthSession, getStoredAccessToken, getStoredUserSession, saveAuthSession } from '@/utils/authSession'
  import SprintaBrand from '@/components/branding/SprintaBrand.vue'

const route = useRoute()
const router = useRouter()

const token = ref('')
const invite = ref({})
const loading = ref(true)
const error = ref('')
const success = ref(false)
const requiresLogin = ref(false)
const accountMismatch = ref(false)
const redirectPath = ref('/dashboard')
const step = ref(0) 

const otpDigits = ref(['', '', '', '', '', ''])
const otpRefs = ref([])
const isVerifyingOtp = ref(false)
const isSendingOtp = ref(false)
const otpCooldown = ref(0)
let cooldownTimer = null

const startCooldown = () => {
  otpCooldown.value = 60
  cooldownTimer = setInterval(() => {
    otpCooldown.value--
    if (otpCooldown.value <= 0) {
      clearInterval(cooldownTimer)
      cooldownTimer = null
    }
  }, 1000)
}

const maskedEmail = computed(() => {
  const email = invite.value?.email
  if (!email) return ''
  const parts = email.split('@')
  if (parts.length !== 2) return email
  const name = parts[0]
  if (name.length <= 2) return email
  return name.substring(0, 2) + '*'.repeat(name.length - 2) + '@' + parts[1]
})

const isOtpComplete = computed(() => otpDigits.value.every(d => d.trim() !== ''))

const onOtpInput = (index) => {
  const val = otpDigits.value[index]
  if (val && index < 5) {
    otpRefs.value[index + 1]?.focus()
  }
}

const onOtpDelete = (e, index) => {
  if (!otpDigits.value[index] && index > 0) {
    otpRefs.value[index - 1]?.focus()
    otpDigits.value[index - 1] = ''
  }
}

const onOtpPaste = (e) => {
  const paste = (e.clipboardData || window.clipboardData).getData('text').trim()
  if (!paste) return
  for (let i = 0; i < 6; i++) {
    if (paste[i]) {
      otpDigits.value[i] = paste[i]
    }
  }
  const lastIndex = Math.min(paste.length - 1, 5)
  otpRefs.value[lastIndex]?.focus()
}

const sendOtp = async () => {
  if (isSendingOtp.value || otpCooldown.value > 0) return
  isSendingOtp.value = true
  try {
    await axiosClient.post('/auth/send-otp', {
      email: invite.value.email,
      purpose: 'invite'
    })
    ElMessage.success('Đã gửi mã xác nhận đến email của bạn.')
    otpDigits.value = ['', '', '', '', '', '']
    nextTick(() => otpRefs.value[0]?.focus())
    startCooldown()
  } catch (err) {
    ElMessage.error(err.response?.data?.message || 'Không thể gửi mã xác nhận.')
  } finally {
    isSendingOtp.value = false
  }
}

const verifyOtp = async () => {
  if (!isOtpComplete.value) return
  isVerifyingOtp.value = true
  const otpCode = otpDigits.value.join('')
  try {
    await axiosClient.post('/auth/verify-otp', {
      email: invite.value.email,
      otpCode: otpCode
    })
    step.value = 2
  } catch (err) {
    ElMessage.error(err.response?.data?.message || 'Mã xác nhận không hợp lệ hoặc đã hết hạn.')
    otpDigits.value = ['', '', '', '', '', '']
    nextTick(() => otpRefs.value[0]?.focus())
  } finally {
    isVerifyingOtp.value = false
  }
}

const isSubmitting = ref(false)
const form = reactive({
  fullName: '',
  password: ''
})

const hasMatchingAuthenticatedUser = () => {
  const session = getStoredUserSession()
  return Boolean(getStoredAccessToken() && session.email && invite.value.email &&
    session.email.toLowerCase() === invite.value.email.toLowerCase())
}

const loadInvite = async () => {
  token.value = String(route.query.token || '')
  if (!token.value) {
    error.value = 'Thiếu mã xác nhận trong đường dẫn.'
    loading.value = false
    return
  }

  loading.value = true
  try {
    const response = await axiosClient.get('/auth/invite-info', {
      params: { token: token.value }
    })
    invite.value = response.data?.data || {}
    form.fullName = invite.value.fullName || ''
    
    if (invite.value.requiresAccountSetup) {
      await sendOtp()
      step.value = 1
    } else {
      step.value = 2
      if (getStoredAccessToken()) {
        if (hasMatchingAuthenticatedUser()) {
          await acceptInvite()
        } else {
          accountMismatch.value = true
        }
      }
    }
  } catch (err) {
    error.value = err.response?.data?.message || 'Liên kết mời không tồn tại hoặc đã hết hạn.'
  } finally {
    loading.value = false
  }
}

const acceptInvite = async () => {
  if (!invite.value.requiresAccountSetup && !getStoredAccessToken()) {
    router.push({ path: '/login', query: { redirect: route.fullPath } })
    return
  }
  if (!invite.value.requiresAccountSetup && !hasMatchingAuthenticatedUser()) {
    accountMismatch.value = true
    return
  }
  if (invite.value.requiresAccountSetup) {
    if (!form.fullName || !form.password) {
      ElMessage.warning('Vui lòng nhập đầy đủ thông tin.')
      return
    }
  }

  isSubmitting.value = true
  try {
    const payload = {
      token: token.value,
      fullName: invite.value.requiresAccountSetup ? form.fullName : null,
      password: invite.value.requiresAccountSetup ? form.password : null
    }

    const response = await axiosClient.post('/auth/accept-invite-token', payload)
    const data = response.data?.data || {}
    requiresLogin.value = data.requiresLogin === true
    redirectPath.value = data.redirectPath || '/dashboard'

    if (data.auth?.accessToken) {
      saveAuthSession(data.auth)
    }

    success.value = true
  } catch (err) {
    error.value = err.response?.data?.message || 'Không thể thiết lập tài khoản lúc này.'
  } finally {
    isSubmitting.value = false
  }
}

const switchAccount = () => {
  clearAuthSession()
  router.push({ path: '/login', query: { redirect: route.fullPath } })
}

const continueAfterSuccess = () => {
  if (requiresLogin.value) {
    router.push({
      path: '/login',
      query: { redirect: redirectPath.value }
    })
    return
  }
  router.push(redirectPath.value)
}

onMounted(loadInvite)
</script>

<style scoped>
.invite-page {
  position: relative;
  min-height: 100vh;
  display: grid;
  place-items: center;
  background: var(--color-bg);
  padding: 24px;
  overflow: hidden;
}

.skeleton-bg {
  position: absolute;
  inset: 0;
  z-index: 0;
  display: flex;
  flex-direction: column;
  background: var(--color-bg);
  pointer-events: none;
  filter: blur(8px);
  opacity: 0.4;
}
.sk-topbar { height: 56px; background: var(--color-surface); border-bottom: 1px solid var(--color-border); }
.sk-body { display: flex; flex: 1; }
.sk-sidebar { width: 240px; background: var(--color-surface); border-right: 1px solid var(--color-border); }
.sk-content { flex: 1; padding: 32px 40px; }
.sk-header { height: 40px; width: 300px; background: var(--color-surface-hover); border-radius: 4px; margin-bottom: 24px; }
.sk-board { display: flex; gap: 16px; }
.sk-col { flex: 1; background: var(--color-surface-hover); border-radius: 6px; padding: 12px; min-height: 400px; }
.sk-card { height: 80px; background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 4px; margin-bottom: 12px; }

.invite-card {
  position: relative;
  z-index: 10;
  width: min(480px, 100%);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 48px;
  box-shadow: var(--shadow-xl);
  display: flex;
  flex-direction: column;
}

.brand-row {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  margin-bottom: 32px;
  color: var(--color-text-primary);
  font-size: 26px;
  font-weight: 800;
}

.text-center { text-align: center; }

h1 {
  margin: 0 0 12px;
  color: var(--color-text-primary);
  font-size: 24px;
  font-weight: 700;
  line-height: 1.25;
}

.subtitle {
  color: var(--color-text-secondary);
  font-size: 14px;
  line-height: 1.6;
  margin-bottom: 32px;
}

.otp-inputs {
  display: flex;
  justify-content: center;
  gap: 12px;
  margin-bottom: 36px;
}

.otp-box {
  width: 52px;
  height: 60px;
  font-size: 28px;
  font-weight: 700;
  text-align: center;
  border: 2px solid var(--color-border);
  border-radius: 8px;
  outline: none;
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
  transition: all 0.2s;
}

.otp-box:focus {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-accent) 20%, transparent);
}

.resend-link { margin-top: 32px; text-align: center; }
.resend-link a { color: var(--color-accent); font-size: 14px; text-decoration: none; font-weight: 600; }
.resend-link a:hover { text-decoration: underline; }
.resend-loading, .resend-cooldown { color: var(--color-text-muted); font-size: 14px; }

.verified-email { margin-bottom: 32px; font-size: 14px; }
.verified-email .label { color: var(--color-text-muted); font-weight: 600; }
.verified-email strong { display: block; margin-top: 6px; color: var(--color-text-primary); font-size: 16px; font-weight: 700; }

.text-success { color: #10b981; margin-left: 4px; }
.mt-24 { margin-top: 24px; }

.invite-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
  margin-bottom: 24px;
}

.field { display: flex; flex-direction: column; gap: 4px; }
.field-label { font-size: 13px; font-weight: 600; color: var(--color-text-secondary); }

.password-hint { font-size: 11px; color: var(--color-text-muted); margin-top: 4px; }

.terms-note {
  font-size: 12px;
  color: var(--color-text-muted);
  line-height: 1.5;
  margin-bottom: 24px;
  text-align: center;
}

.terms-note a { color: var(--color-accent); text-decoration: none; }

.state-block { text-align: center; }
.state-block i { color: var(--color-accent); font-size: 48px; margin-bottom: 24px; }
.state-block i.success { color: #10b981; }
.state-block i.danger { color: #ef4444; }

.account-mismatch {
  margin-bottom: 24px;
  padding: 14px;
  border: 1px solid #f2b8b5;
  border-radius: 8px;
  color: #9f2d28;
  background: #fff5f4;
  font-size: 13px;
  line-height: 1.5;
}

.secondary-btn {
  display: block;
  margin: 12px auto 0;
  border: 0;
  background: transparent;
  color: var(--color-accent);
  cursor: pointer;
  font-weight: 700;
}

@media (max-width: 560px) {
  .invite-card { padding: 32px 24px; }
  .otp-box { width: 44px; height: 52px; font-size: 24px; }
}
</style>
