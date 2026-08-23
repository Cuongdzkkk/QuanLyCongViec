<template>
  <AdminLayout>
    <div class="admin-page">
      <div class="page-header">
        <div class="breadcrumb">
          <i class="fa-solid fa-server"></i>
          <span>INSTANCE / GENERAL</span>
        </div>
        <h1 class="text-hero">Instance general settings</h1>
        <p class="text-desc">
          Central place for system-wide identity, admin contact rules, and rollout defaults. 
          Manage your branding and global access policies from one professional dashboard.
        </p>
      </div>

      <div class="settings-grid">
        <!-- Identity Section -->
        <section class="settings-card">
          <div class="section-head">
            <div>
              <h2 class="text-section">Identity</h2>
              <p class="text-small">Branding values used across instance-level pages and emails.</p>
            </div>
            <button type="button" class="primary-btn" @click="saveSettings">Save Changes</button>
          </div>

          <div class="field-grid">
            <div class="field">
              <span class="field-label">Instance name</span>
              <input v-model="settings.instanceName" type="text" placeholder="SprintA" />
            </div>
            <div class="field">
              <span class="field-label">Primary domain</span>
              <input v-model="settings.primaryDomain" type="text" placeholder="app.example.com" />
            </div>
            <div class="field">
              <span class="field-label">Support URL</span>
              <input v-model="settings.supportUrl" type="text" placeholder="https://status.example.com" />
            </div>
            <div class="field">
              <span class="field-label">Default timezone</span>
              <input v-model="settings.defaultTimezone" type="text" placeholder="Asia/Saigon" />
            </div>
          </div>
        </section>

        <!-- Admin Contacts -->
        <section class="settings-card">
          <div class="section-head">
            <div>
              <h2 class="text-section">Admin contacts</h2>
              <p class="text-small">Fallback contacts shown in system banners, invitations, and escalation flows.</p>
            </div>
          </div>

          <div class="field-grid">
            <div class="field">
              <span class="field-label">Operations email</span>
              <input v-model="settings.operationsEmail" type="email" placeholder="ops@example.com" />
            </div>
            <div class="field">
              <span class="field-label">Security email</span>
              <input v-model="settings.securityEmail" type="email" placeholder="security@example.com" />
            </div>
            <div class="field full">
              <span class="field-label">Incident banner</span>
              <textarea v-model="settings.incidentBanner" rows="4" placeholder="Scheduled maintenance notifications or global alerts"></textarea>
            </div>
          </div>
        </section>

        <!-- Rollout Defaults -->
        <section class="settings-card">
          <div class="section-head">
            <div>
              <h2 class="text-section">Rollout defaults</h2>
              <p class="text-small">Safe defaults for onboarding and self-service access.</p>
            </div>
          </div>

          <div class="toggle-list">
            <label class="toggle-row">
              <input v-model="settings.allowWorkspaceSignup" type="checkbox" />
              <span>Allow self-service workspace signup</span>
            </label>
            <label class="toggle-row">
              <input v-model="settings.requireInviteApproval" type="checkbox" />
              <span>Require admin approval for org invites</span>
            </label>
            <label class="toggle-row">
              <input v-model="settings.showPublicStatusPage" type="checkbox" />
              <span>Expose public status page link</span>
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

const STORAGE_KEY = 'admin-instance-general-settings'

const settings = ref({
  instanceName: 'SprintA',
  primaryDomain: 'localhost',
  supportUrl: '',
  defaultTimezone: 'Asia/Saigon',
  operationsEmail: '',
  securityEmail: '',
  incidentBanner: '',
  allowWorkspaceSignup: false,
  requireInviteApproval: true,
  showPublicStatusPage: false
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
    ElMessage.warning('Could not restore saved instance settings.')
  }
}

const saveSettings = () => {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(settings.value))
  ElMessage.success('Instance general settings saved locally.')
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

@media (max-width: 900px) {
  .field-grid {
    grid-template-columns: 1fr;
  }
}
</style>
