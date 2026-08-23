<template>
  <AdminLayout>
    <div class="admin-page">
      <div class="page-header">
        <div class="breadcrumb">
          <i class="fa-solid fa-envelope"></i>
          <span>INSTANCE / EMAIL</span>
        </div>
        <h1 class="text-hero">Email and SMTP management</h1>
        <p class="text-desc">
          Manage sender identity, SMTP transport, and outbound mail delivery. 
          Ensure reliable notification delivery and operational alerts for your instance.
        </p>
      </div>

      <div class="settings-grid">
        <!-- Sender Identity -->
        <section class="settings-card">
          <div class="section-head">
            <div>
              <h2 class="text-section">Sender identity</h2>
              <p class="text-small">Values used in invitation emails, OTP messages, and operational notices.</p>
            </div>
            <button type="button" class="primary-btn" @click="saveSettings">Save Changes</button>
          </div>

          <div class="field-grid">
            <div class="field">
              <span class="field-label">From name</span>
              <input v-model="settings.fromName" type="text" placeholder="SprintA Notifications" />
            </div>
            <div class="field">
              <span class="field-label">From email</span>
              <input v-model="settings.fromEmail" type="email" placeholder="no-reply@example.com" />
            </div>
            <div class="field full">
              <span class="field-label">Reply-to email</span>
              <input v-model="settings.replyToEmail" type="email" placeholder="support@example.com" />
            </div>
          </div>
        </section>

        <!-- SMTP Transport -->
        <section class="settings-card">
          <div class="section-head">
            <div>
              <h2 class="text-section">SMTP transport</h2>
              <p class="text-small">Connection settings for outbound mail delivery services.</p>
            </div>
          </div>

          <div class="field-grid">
            <div class="field">
              <span class="field-label">SMTP host</span>
              <input v-model="settings.smtpHost" type="text" placeholder="smtp.example.com" />
            </div>
            <div class="field">
              <span class="field-label">Port</span>
              <input v-model="settings.smtpPort" type="number" min="1" placeholder="587" />
            </div>
            <div class="field">
              <span class="field-label">Username</span>
              <input v-model="settings.smtpUsername" type="text" placeholder="smtp-user" />
            </div>
            <div class="field">
              <span class="field-label">Password</span>
              <input v-model="settings.smtpSecretLabel" type="password" placeholder="••••••••" />
            </div>
          </div>

          <div class="toggle-list" style="margin-top: 24px;">
            <label class="toggle-row">
              <input v-model="settings.useTls" type="checkbox" />
              <span>Use TLS</span>
            </label>
            <label class="toggle-row">
              <input v-model="settings.useStartTls" type="checkbox" />
              <span>Use STARTTLS</span>
            </label>
          </div>
        </section>

        <!-- Test Delivery -->
        <section class="settings-card">
          <div class="section-head">
            <div>
              <h2 class="text-section">Test delivery</h2>
              <p class="text-small">Verify your SMTP configuration by sending a test email.</p>
            </div>
            <button type="button" class="secondary-btn" @click="runTest">Run Test Delivery</button>
          </div>

          <div class="field-grid">
            <div class="field full">
              <span class="field-label">Recipient email</span>
              <input v-model="settings.testRecipient" type="email" placeholder="qa@example.com" />
            </div>
          </div>

          <div class="status-banner">
            <strong>Configuration Status</strong>
            <p>SMTP settings are currently stored in local configuration. Ensure your firewall allows outbound traffic on the specified port.</p>
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

const STORAGE_KEY = 'admin-instance-email-settings'

const settings = ref({
  fromName: 'SprintA Notifications',
  fromEmail: '',
  replyToEmail: '',
  smtpHost: '',
  smtpPort: 587,
  smtpUsername: '',
  smtpSecretLabel: '',
  useTls: true,
  useStartTls: true,
  testRecipient: ''
})

const loadSettings = () => {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return
    settings.value = {
      ...settings.value,
      ...JSON.parse(raw)
    }
  } catch {
    ElMessage.warning('Could not restore saved email settings.')
  }
}

const saveSettings = () => {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(settings.value))
  ElMessage.success('Email settings saved locally.')
}

const runTest = () => {
  if (!settings.value.smtpHost || !settings.value.fromEmail || !settings.value.testRecipient) {
    ElMessage.warning('Fill SMTP host, sender email, and test recipient first.')
    return
  }
  ElMessage.success(`SMTP test queued for ${settings.value.testRecipient} (scaffold mode).`)
}

onMounted(loadSettings)
</script>

<style scoped>


.field {
  display: flex;
  flex-direction: column;
}

.field.full {
  grid-column: 1 / -1;
}

.status-banner {
  margin-top: 24px;
  padding: 16px;
  background: var(--color-surface-hover);
  border: 1px solid var(--color-border);
  border-radius: 2px;
}

.status-banner strong {
  display: block;
  color: var(--color-text-primary);
  margin-bottom: 4px;
}

.status-banner p {
  font-size: 13px;
  color: var(--color-text-secondary);
  margin: 0;
}

@media (max-width: 900px) {
  .field-grid {
    grid-template-columns: 1fr;
  }
}
</style>
