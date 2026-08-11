<template>
  <div class="profile-page">
    <div class="atlassian-settings-layout">
      <!-- Sidebar Column -->
      <ProfileSidebar
        :profileData="profileData"
        :activeTab="activeTab"
        @select-tab="selectTab"
      />

      <!-- Main Content Column -->
      <div class="settings-content-wrapper">
        <transition name="fade" mode="out-in">
          <!-- Profile & Visibility -->
          <ProfileCard
            v-if="activeTab === 'profile'"
            :profileData="profileData"
            :isLoading="isLoading"
            :isSaving="isSaving"
            @save-profile="saveProfile"
            @upload-avatar="uploadAvatar"
            @remove-avatar="removeAvatar"
          />

          <!-- Email Addresses -->
          <EmailCard
            v-else-if="activeTab === 'emails'"
            :profileData="profileData"
          />

          <!-- Security Overview / MFA / Password / Sessions -->
          <SecurityCard
            v-else
            :view="activeTab"
            :profileData="profileData"
            :passwordStep="passwordStep"
            :passwordOtpSent="passwordOtpSent"
            :isSendingPasswordOtp="isSendingPasswordOtp"
            :isChangingPassword="isChangingPassword"
            :isRequestingPasswordException="isRequestingPasswordException"
            :canResendPasswordOtp="canResendPasswordOtp"
            :passwordCountdownText="passwordCountdownText"
            :passwordForm="passwordForm"
            :passwordHints="passwordHints"
            :passwordStrength="passwordStrength"
            :passwordStrengthClass="passwordStrengthClass"
            :passwordStrengthLabel="passwordStrengthLabel"
            :canSubmitPassword="canSubmitPassword"
            :isPasswordChangeLocked="isPasswordChangeLocked"
            :passwordEligibleLabel="passwordEligibleLabel"
            @select-tab="selectTab"
            @send-otp="sendPasswordOtp"
            @request-exception="requestPasswordChangeException"
            @change-password="changePassword"
            @reset-password-flow="resetPasswordFlow"
            @toggle-2fa="toggle2FA"
            @go-to-step-1="passwordStep = 1"
            @go-to-step-2="passwordStep = 2"
            @check-password-strength="checkPasswordStrength"
          />
        </transition>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'

import axiosClient from '@/api/axiosClient'
import { ElMessage } from 'element-plus'
import { useLocale } from '@/composables/useLocale'

// Import components
import ProfileSidebar from '@/components/profile/ProfileSidebar.vue'
import ProfileCard from '@/components/profile/ProfileCard.vue'
import EmailCard from '@/components/profile/EmailCard.vue'
import SecurityCard from '@/components/profile/SecurityCard.vue'

const { t } = useLocale()

const activeTab = ref('profile')
const isLoading = ref(false)
const isSaving = ref(false)

const profileData = ref({
  fullName: '',
  publicName: '',
  jobTitle: '',
  department: '',
  organization: '',
  email: '',
  collaboration: '',
  avatarUrl: '',
  coverUrl: '',
  lastPasswordChangedAt: '',
  canChangePasswordAt: '',
  is2FaEnabled: false,
  coverPositionY: 50
})

const passwordStep = ref(1)
const passwordOtpSent = ref(false)
const isSendingPasswordOtp = ref(false)
const isChangingPassword = ref(false)
const isRequestingPasswordException = ref(false)
const canResendPasswordOtp = ref(true)
const passwordCountdown = ref(0)
const passwordStrength = ref(0)
const passwordStrengthClass = ref('')
const passwordStrengthLabel = ref('')
let passwordCountdownTimer = null

const passwordForm = reactive({
  otpCode: '',
  newPassword: '',
  confirmPassword: '',
  logoutOthers: true
})

const passwordHints = reactive({
  length: false,
  uppercase: false,
  number: false,
  special: false
})

const passwordCountdownText = computed(() => {
  const minutes = Math.floor(passwordCountdown.value / 60)
  const seconds = passwordCountdown.value % 60
  return `${minutes}:${seconds.toString().padStart(2, '0')}`
})

const canSubmitPassword = computed(() => (
  passwordStrength.value === 4 &&
  passwordForm.newPassword === passwordForm.confirmPassword &&
  passwordForm.otpCode.length >= 6
))

const isPasswordChangeLocked = computed(() => {
  if (!profileData.value.canChangePasswordAt) return false
  return new Date(profileData.value.canChangePasswordAt).getTime() > Date.now()
})

const passwordEligibleLabel = computed(() => {
  if (!profileData.value.canChangePasswordAt) return 'now'
  return new Date(profileData.value.canChangePasswordAt).toLocaleString()
})

const selectTab = (tab) => {
  activeTab.value = tab
}

const uploadAvatar = async (event) => {
  const avatarFile = event.target.files?.[0]
  if (!avatarFile) return

  // BUG-HOST-002: Validate file size (5MB limit)
  if (avatarFile.size > 5 * 1024 * 1024) {
    ElMessage.error(t('Profile photo size must be less than 5MB', 'Kích thước ảnh đại diện phải nhỏ hơn 5MB'))
    event.target.value = ''
    return
  }

  const formData = new FormData()
  formData.append('file', avatarFile)

  try {
    const res = await axiosClient.put('/users/avatar', formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    const newAvatarUrl = res.data?.data?.avatarUrl || ''
    profileData.value.avatarUrl = newAvatarUrl
    
    // Notify other components about avatar update
    window.dispatchEvent(new CustomEvent('user-avatar-updated', { detail: { avatarUrl: newAvatarUrl } }))
    
    ElMessage.success(t('Profile photo updated', 'Đã cập nhật ảnh đại diện thành công.'))
  } catch (error) {
    console.error('Avatar upload failed:', error)
    ElMessage.error(error.response?.data?.message || t('Could not upload profile photo. Please try a different image.', 'Không thể tải lên ảnh đại diện. Vui lòng thử lại bằng ảnh khác.'))
  } finally {
    event.target.value = ''
  }
}

const removeAvatar = async () => {
  try {
    isSaving.value = true
    await axiosClient.delete('/users/avatar')
    profileData.value.avatarUrl = ''
    window.dispatchEvent(new CustomEvent('user-avatar-updated', { detail: { avatarUrl: '' } }))
    ElMessage.success(t('Profile photo removed.', 'Đã gỡ ảnh đại diện thành công.'))
  } catch (error) {
    console.error('Avatar removal failed:', error)
    ElMessage.error(error.response?.data?.message || t('Could not remove profile photo.', 'Không thể gỡ ảnh đại diện.'))
  } finally {
    isSaving.value = false
  }
}

const fetchProfile = async () => {
  try {
    isLoading.value = true
    const response = await axiosClient.get('/users/me')
    const data = response.data?.data || {}
    profileData.value = {
      fullName: data.fullName || '',
      publicName: data.publicName || '',
      jobTitle: data.jobTitle || '',
      department: data.departmentName || '',
      organization: data.organizationName || '',
      email: data.email || '',
      collaboration: data.collaborationRules || '',
      avatarUrl: data.avatarUrl || '',
      avatarColor: data.avatarColor || '',
      coverUrl: data.coverUrl || '',
      lastPasswordChangedAt: data.lastPasswordChangedAt || '',
      canChangePasswordAt: data.canChangePasswordAt || '',
      is2FaEnabled: data.is2FaEnabled || false,
      coverPositionY: data.coverPositionY ?? 50
    }
  } catch (error) {
    console.error('Profile load failed', error)
    ElMessage.error(t('Could not load your profile.', 'Không thể tải thông tin hồ sơ của bạn.'))
  } finally {
    isLoading.value = false
  }
}

const saveProfile = async (updatedFields) => {
  if (updatedFields) {
    profileData.value.fullName = updatedFields.fullName
    profileData.value.publicName = updatedFields.publicName
    profileData.value.jobTitle = updatedFields.jobTitle
    profileData.value.department = updatedFields.department
    profileData.value.organization = updatedFields.organization
    profileData.value.collaboration = updatedFields.collaboration
    profileData.value.coverPositionY = updatedFields.coverPositionY
  }

  try {
    isSaving.value = true
    await axiosClient.put('/users/profile', {
      fullName: profileData.value.fullName,
      publicName: profileData.value.publicName,
      jobTitle: profileData.value.jobTitle,
      departmentName: profileData.value.department,
      organizationName: profileData.value.organization,
      collaborationRules: profileData.value.collaboration,
      coverPositionY: profileData.value.coverPositionY
    })
    ElMessage.success(t('Profile saved.', 'Đã lưu thông tin hồ sơ thành công.'))
    await fetchProfile()
  } catch (error) {
    console.error('Profile save failed', error)
    ElMessage.error(error.response?.data?.message || t('Could not save profile.', 'Không thể lưu hồ sơ.'))
  } finally {
    isSaving.value = false
  }
}

const toggle2FA = async (enable) => {
  try {
    const res = await axiosClient.post('/users/toggle-2fa', { enable })
    profileData.value.is2FaEnabled = res.data?.is2FaEnabled ?? enable
    ElMessage.success(res.data?.message || (enable ? t('2FA Enabled.', 'Đã bật 2FA.') : t('2FA Disabled.', 'Đã tắt 2FA.')))
  } catch (error) {
    console.error('Toggle 2FA failed:', error)
    ElMessage.error(error.response?.data?.message || t('Could not change 2FA status.', 'Không thể thay đổi trạng thái 2FA.'))
  }
}

const startPasswordCountdown = () => {
  canResendPasswordOtp.value = false
  passwordCountdown.value = 60
  if (passwordCountdownTimer) clearInterval(passwordCountdownTimer)
  passwordCountdownTimer = setInterval(() => {
    passwordCountdown.value -= 1
    if (passwordCountdown.value <= 0) {
      clearInterval(passwordCountdownTimer)
      passwordCountdownTimer = null
      canResendPasswordOtp.value = true
    }
  }, 1000)
}

const sendPasswordOtp = async () => {
  if (isPasswordChangeLocked.value) {
    ElMessage.warning(t('Password changes are limited to once every 7 days. Please request admin support if this is urgent.', 'Thay đổi mật khẩu bị giới hạn 7 ngày một lần. Vui lòng gửi yêu cầu hỗ trợ tới Quản trị viên nếu khẩn cấp.'))
    return
  }
  if (!profileData.value.email) {
    ElMessage.warning(t('Profile email is missing.', 'Thiếu địa chỉ email hồ sơ.'))
    return
  }

  isSendingPasswordOtp.value = true
  try {
    const { data } = await axiosClient.post('/users/send-change-password-otp', {
      email: profileData.value.email
    })
    passwordOtpSent.value = true
    passwordForm.otpCode = ''
    startPasswordCountdown()
    ElMessage.success(data?.message || t('OTP code sent to your email.', 'Mã OTP đã được gửi đến email của bạn.'))
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('Could not send OTP code.', 'Không thể gửi mã OTP.'))
  } finally {
    isSendingPasswordOtp.value = false
  }
}

const requestPasswordChangeException = async () => {
  isRequestingPasswordException.value = true
  try {
    const { data } = await axiosClient.post('/users/request-password-change-exception')
    ElMessage.success(data?.message || t('Your request has been sent to the system admin.', 'Yêu cầu của bạn đã được gửi tới Quản trị viên hệ thống.'))
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('Could not send request to system admin.', 'Không thể gửi yêu cầu tới Quản trị viên hệ thống.'))
  } finally {
    isRequestingPasswordException.value = false
  }
}

const checkPasswordStrength = () => {
  const password = passwordForm.newPassword
  passwordHints.length = password.length >= 8
  passwordHints.uppercase = /[A-Z]/.test(password)
  passwordHints.number = /[0-9]/.test(password)
  passwordHints.special = /[^A-Za-z0-9]/.test(password)

  let score = 0
  if (passwordHints.length) score += 1
  if (passwordHints.uppercase) score += 1
  if (passwordHints.number) score += 1
  if (passwordHints.special) score += 1
  passwordStrength.value = score

  if (score <= 1) {
    passwordStrengthClass.value = 'strength-weak'
    passwordStrengthLabel.value = t('Weak', 'Yếu')
  } else if (score === 2) {
    passwordStrengthClass.value = 'strength-fair'
    passwordStrengthLabel.value = t('Fair', 'Trung bình')
  } else if (score === 3) {
    passwordStrengthClass.value = 'strength-good'
    passwordStrengthLabel.value = t('Good', 'Mạnh')
  } else {
    passwordStrengthClass.value = 'strength-strong'
    passwordStrengthLabel.value = t('Very strong', 'Rất mạnh')
  }
}

const changePassword = async () => {
  if (!canSubmitPassword.value) return

  isChangingPassword.value = true
  try {
    await axiosClient.put('/users/change-password', {
      otpCode: passwordForm.otpCode,
      newPassword: passwordForm.newPassword,
      logoutOthers: passwordForm.logoutOthers
    })
    passwordStep.value = 3
    await fetchProfile()
    ElMessage.success(t('Password changed successfully.', 'Đổi mật khẩu thành công.'))
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('Password change failed. Please check the OTP code.', 'Đổi mật khẩu thất bại. Vui lòng kiểm tra lại mã OTP.'))
  } finally {
    isChangingPassword.value = false
  }
}

const resetPasswordFlow = () => {
  passwordStep.value = 1
  passwordOtpSent.value = false
  passwordForm.otpCode = ''
  passwordForm.newPassword = ''
  passwordForm.confirmPassword = ''
  passwordStrength.value = 0
  passwordStrengthClass.value = ''
  passwordStrengthLabel.value = ''
  passwordHints.length = false
  passwordHints.uppercase = false
  passwordHints.number = false
  passwordHints.special = false
}

onMounted(fetchProfile)

onUnmounted(() => {
  if (passwordCountdownTimer) clearInterval(passwordCountdownTimer)
})
</script>

<style scoped>
.profile-page {
  height: 100%;
  background-color: #F4F5F7;
}

.dark .profile-page {
  background-color: var(--color-bg);
}

.atlassian-settings-layout {
  display: grid;
  grid-template-columns: 280px 1fr;
  gap: 40px;
  max-width: 1140px;
  margin: 40px auto;
  padding: 0 24px;
  width: 100%;
}

.settings-content-wrapper {
  min-height: 500px;
}

/* Page transitions */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.15s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

@media (max-width: 992px) {
  .atlassian-settings-layout {
    grid-template-columns: 1fr;
    gap: 24px;
    margin: 20px auto;
    padding: 0 16px;
  }
}
</style>
