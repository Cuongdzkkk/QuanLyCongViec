<template>
  <div class="settings-card connected-accounts-card">
    <div class="card-header">
      <h2 class="card-title">{{ t('Connected Accounts', 'Tài khoản liên kết') }}</h2>
      <p class="card-subtitle">
        {{ t('Connect Google or GitHub after signing in to use them as additional login methods.', 'Liên kết Google hoặc GitHub sau khi đăng nhập để dùng làm phương thức đăng nhập bổ sung.') }}
      </p>
    </div>

    <div v-loading="isLoading" class="accounts-list">
      <div v-for="account in accounts" :key="account.provider" class="account-row">
        <div class="account-identity">
          <span class="provider-icon" :class="account.provider.toLowerCase()">
            <i :class="account.provider === 'Google' ? 'fa-brands fa-google' : 'fa-brands fa-github'"></i>
          </span>
          <div>
            <h3>{{ account.provider }}</h3>
            <p v-if="account.isLinked">{{ account.providerEmail }}</p>
            <p v-else>{{ t('Not linked', 'Chưa liên kết') }}</p>
          </div>
        </div>
        <div class="account-actions">
          <el-tag v-if="account.isLinked" type="success" effect="plain">
            {{ t('Linked', 'Đã liên kết') }}
          </el-tag>
          <el-button
            v-if="account.isLinked"
            type="danger"
            plain
            size="small"
            :loading="linkingProvider === `${account.provider}:unlink`"
            @click="unlink(account.provider)"
          >
            {{ t('Unlink', 'Ngắt liên kết') }}
          </el-button>
          <el-button
            v-else
            type="primary"
            plain
            size="small"
            :loading="linkingProvider === account.provider"
            @click="link(account.provider)"
          >
            {{ t('Link', 'Liên kết') }}
          </el-button>
        </div>
      </div>
    </div>

    <p class="security-note">
      <i class="fa-solid fa-shield-halved"></i>
      {{ t('You must already be signed in. SprintA never links an account only because its email matches.', 'Bạn phải đăng nhập sẵn. SprintA không tự liên kết chỉ vì email trùng khớp.') }}
    </p>
  </div>
</template>

<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useLocale } from '@/composables/useLocale'
import {
  getExternalLoginStatus,
  linkGoogleAccountWithAuthorizationCode,
  startGoogleAccountLink,
  startGitHubAccountLink,
  unlinkExternalLogin
} from '@/api/authApi'
import {
  registerGoogleAuthorizationCodeClient
} from '@/services/googleIdentityService'

const { t } = useLocale()
const accounts = ref([])
const isLoading = ref(false)
const linkingProvider = ref('')
const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID || ''
let googleAuthorizationClient = null

const loadAccounts = async () => {
  isLoading.value = true
  try {
    accounts.value = await getExternalLoginStatus()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('Could not load connected accounts.', 'Không thể tải tài khoản liên kết.'))
  } finally {
    isLoading.value = false
  }
}

const stopGoogleClient = () => {
  googleAuthorizationClient?.release()
  googleAuthorizationClient = null
  if (linkingProvider.value === 'Google') linkingProvider.value = ''
}

const linkGoogle = async () => {
  if (!googleClientId || googleClientId === 'CHANGE_ME_USE_LOCAL_ENV') {
    ElMessage.error(t('Google sign-in is not configured.', 'Google Sign-In chưa được cấu hình.'))
    return
  }

  linkingProvider.value = 'Google'
  try {
    const state = await startGoogleAccountLink()
    googleAuthorizationClient = await registerGoogleAuthorizationCodeClient({
      clientId: googleClientId,
      state,
      callback: async response => {
        try {
          await linkGoogleAccountWithAuthorizationCode(response?.code, response?.state || state)
          ElMessage.success(t('Google account linked.', 'Đã liên kết tài khoản Google.'))
          await loadAccounts()
        } catch (error) {
          ElMessage.error(error.response?.data?.message || t('Could not link Google.', 'Không thể liên kết Google.'))
        } finally {
          stopGoogleClient()
        }
      },
      errorCallback: () => {
        ElMessage.error(t('Google account link was cancelled or failed.', 'Liên kết Google bị hủy hoặc thất bại.'))
        stopGoogleClient()
      }
    })
    googleAuthorizationClient.requestCode()
  } catch (error) {
    stopGoogleClient()
    ElMessage.error(error.response?.data?.message || error.message || t('Could not start Google linking.', 'Không thể bắt đầu liên kết Google.'))
  }
}

const linkGitHub = async () => {
  linkingProvider.value = 'GitHub'
  try {
    window.location.href = await startGitHubAccountLink()
  } catch (error) {
    linkingProvider.value = ''
    ElMessage.error(error.response?.data?.message || t('Could not start GitHub linking.', 'Không thể bắt đầu liên kết GitHub.'))
  }
}

const link = provider => provider === 'Google' ? linkGoogle() : linkGitHub()

const unlink = async provider => {
  try {
    await ElMessageBox.confirm(
      t(`Unlink ${provider}? You may need another login method to access SprintA.`, `Ngắt liên kết ${provider}? Bạn cần còn một phương thức đăng nhập khác để truy cập SprintA.`),
      t('Confirm unlink', 'Xác nhận ngắt liên kết'),
      { confirmButtonText: t('Unlink', 'Ngắt liên kết'), cancelButtonText: t('Cancel', 'Hủy'), type: 'warning' }
    )
    linkingProvider.value = `${provider}:unlink`
    await unlinkExternalLogin(provider)
    ElMessage.success(t('Account unlinked.', 'Đã ngắt liên kết tài khoản.'))
    await loadAccounts()
  } catch (error) {
    if (error === 'cancel' || error === 'close') return
    ElMessage.error(error.response?.data?.message || t('Could not unlink account.', 'Không thể ngắt liên kết tài khoản.'))
  } finally {
    if (linkingProvider.value === `${provider}:unlink`) linkingProvider.value = ''
  }
}

onMounted(loadAccounts)
onBeforeUnmount(() => {
  stopGoogleClient()
})
</script>

<style scoped>
.connected-accounts-card {
  background-color: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 32px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
}

.card-header { margin-bottom: 24px; }
.card-title { margin: 0 0 8px; color: var(--color-text-primary); font-size: 24px; font-weight: 600; }
.card-subtitle, .security-note { color: var(--color-text-secondary); line-height: 1.5; }
.card-subtitle { margin: 0; font-size: 14px; }
.accounts-list { display: grid; gap: 12px; }
.account-row { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 16px 20px; border: 1px solid var(--color-border); border-radius: 8px; }
.account-identity, .account-actions { display: flex; align-items: center; gap: 12px; }
.account-identity { min-width: 0; }
.account-identity h3, .account-identity p { margin: 0; }
.account-identity h3 { color: var(--color-text-primary); font-size: 15px; }
.account-identity p { margin-top: 4px; color: var(--color-text-secondary); font-size: 13px; overflow-wrap: anywhere; }
.provider-icon { display: inline-flex; width: 40px; height: 40px; align-items: center; justify-content: center; border-radius: 50%; font-size: 18px; }
.provider-icon.google { color: #ea4335; background: rgba(234, 67, 53, .1); }
.provider-icon.github { color: var(--color-text-primary); background: var(--color-surface-hover); }
.security-note { display: flex; gap: 8px; margin: 20px 0 0; font-size: 13px; }
.security-note i { color: var(--color-success); }
@media (max-width: 640px) {
  .connected-accounts-card { padding: 22px 16px; }
  .account-row { align-items: flex-start; flex-direction: column; }
  .account-actions { align-self: stretch; justify-content: flex-end; }
}
</style>
