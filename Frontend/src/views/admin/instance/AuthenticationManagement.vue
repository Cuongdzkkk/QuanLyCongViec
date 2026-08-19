<template>
  <AdminLayout>
    <div class="admin-page">
      <div class="page-header">
        <div class="breadcrumb">
          <i class="fa-solid fa-lock"></i>
          <span>INSTANCE / AUTHENTICATION</span>
        </div>
        <h1 class="text-hero">Authentication management</h1>
        <p class="text-desc">
          Control which sign-in methods are exposed at the instance level and keep provider settings organized in one place. 
          Manage OAuth flows and security policies for all users.
        </p>
      </div>

      <div class="layout-grid">
        <!-- Login Methods Section -->
        <section class="settings-card">
          <div class="section-head">
            <div>
              <h2 class="text-section">Login methods</h2>
              <p class="text-small">Enable or disable authentication providers for your instance.</p>
            </div>
            <button type="button" class="primary-btn" @click="saveSettings">Save Changes</button>
          </div>

          <div class="toggle-list">
            <label class="toggle-row">
              <input v-model="settings.emailPassword" type="checkbox" />
              <span>Email and password</span>
            </label>
            <label class="toggle-row">
              <input v-model="settings.loginOtp" type="checkbox" />
              <span>Email OTP login</span>
            </label>
            <label class="toggle-row">
              <input v-model="settings.github.enabled" type="checkbox" />
              <span>GitHub OAuth</span>
            </label>
            <label class="toggle-row">
              <input v-model="settings.google.enabled" type="checkbox" />
              <span>Google OAuth</span>
            </label>
          </div>
        </section>

        <!-- GitHub Configuration -->
        <section class="settings-card" v-if="settings.github.enabled">
          <div class="section-head">
            <div>
              <h2 class="text-section">GitHub provider</h2>
              <p class="text-small">Configure OAuth credentials for GitHub authentication.</p>
            </div>
          </div>

          <div class="field-grid">
            <div class="field">
              <span class="field-label">Client ID</span>
              <input v-model="settings.github.clientId" type="text" placeholder="github-client-id" />
            </div>
            <div class="field">
              <span class="field-label">Callback URL</span>
              <input v-model="settings.github.callbackUrl" type="text" placeholder="https://app.example.com/auth/github/callback" />
            </div>
          </div>
        </section>

        <!-- Google Configuration -->
        <section class="settings-card" v-if="settings.google.enabled">
          <div class="section-head">
            <div>
              <h2 class="text-section">Google provider</h2>
              <p class="text-small">Configure OAuth credentials for Google authentication.</p>
            </div>
          </div>

          <div class="field-grid">
            <div class="field">
              <span class="field-label">Client ID</span>
              <input v-model="settings.google.clientId" type="text" placeholder="google-client-id" />
            </div>
            <div class="field">
              <span class="field-label">Callback URL</span>
              <input v-model="settings.google.callbackUrl" type="text" placeholder="https://app.example.com/auth/google/callback" />
            </div>
          </div>
        </section>

        <!-- Security Policy -->
        <section class="settings-card">
          <div class="section-head">
            <div>
              <h2 class="text-section">Security policy</h2>
              <p class="text-small">High-level controls for login safety and tenant onboarding.</p>
            </div>
          </div>

          <div class="toggle-list">
            <label class="toggle-row">
              <input v-model="settings.requireEmailVerification" type="checkbox" />
              <span>Require verified email before first workspace access</span>
            </label>
            <label class="toggle-row">
              <input v-model="settings.allowInvitationOnly" type="checkbox" />
              <span>Invitation-only onboarding</span>
            </label>
            <label class="toggle-row">
              <input v-model="settings.enforceSsoForAdmins" type="checkbox" />
              <span>Force OAuth or OTP for admin users</span>
            </label>
          </div>
        </section>
      </div>
    </div>
  </AdminLayout>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import AdminLayout from '@/components/layout/AdminLayout.vue'

const STORAGE_KEY = 'admin-instance-auth-settings'

const settings = ref({
  emailPassword: true,
  loginOtp: true,
  requireEmailVerification: true,
  allowInvitationOnly: false,
  enforceSsoForAdmins: false,
  github: {
    enabled: true,
    clientId: '',
    callbackUrl: ''
  },
  google: {
    enabled: false,
    clientId: '',
    callbackUrl: ''
  }
})

const loadSettings = () => {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return
    const parsed = JSON.parse(raw)
    settings.value = {
      ...settings.value,
      ...parsed,
      github: { ...settings.value.github, ...(parsed.github || {}) },
      google: { ...settings.value.google, ...(parsed.google || {}) }
    }
  } catch {
    ElMessage.warning('Could not restore saved authentication settings.')
  }
}

const saveSettings = () => {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(settings.value))
  ElMessage.success('Authentication settings saved locally.')
}

onMounted(loadSettings)
</script>

<style scoped>


.layout-grid {
  display: grid;
  gap: 20px;
}

.toggle-text {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.toggle-title {
  font-size: 14px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.field {
  display: flex;
  flex-direction: column;
}

@media (max-width: 900px) {
  .field-grid {
    grid-template-columns: 1fr;
  }
}
</style>
