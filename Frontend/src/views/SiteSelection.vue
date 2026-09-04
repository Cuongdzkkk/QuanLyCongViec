<template>
  <div class="start-page-wrapper">
    <header class="start-header">
      <div class="header-left">
        <div class="atlassian-brand-block">
          <svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor" xmlns="http://www.w3.org/2000/svg">
            <path d="M12 2L2 22H9L12 16L15 22H22L12 2Z"/>
          </svg>
        </div>
        <SprintaBrand size="site-selection" />
      </div>
      <div class="header-right">
        <button type="button" class="pill-btn blue" @click="router.push('/')">{{ t('siteSelection.goToSprintA') }}</button>
        <div class="user-profile" v-if="userName">
          <div class="user-avatar-circle">{{ userInitials }}</div>
          <span class="user-name-text">{{ userName }}</span>
        </div>
      </div>
    </header>

    <main class="start-content">
      <div class="welcome-container">
        <h1 class="welcome-title">
          {{ t('siteSelection.welcomeBack') }}<template v-if="userName"> <span class="highlight-wrapper">&nbsp;{{ userName }}.
            <svg class="squiggly-line" width="100%" height="12" viewBox="0 0 100 12" preserveAspectRatio="none" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0 6 Q 12 0, 25 6 T 50 6 T 75 6 T 100 6" stroke="var(--color-warning)" stroke-width="3" stroke-linecap="round" fill="none"/>
            </svg>
          </span></template>
        </h1>
      </div>

      <div class="card-section">
        <div class="card-header-row">
          <span class="pickup-text">
            {{ t('siteSelection.pickUpIn') }}
            <SprintaBrand size="inline" :show-name="false" />
            <strong>SprintA</strong>
          </span>
          <a href="#" class="create-site-link" @click.prevent="openCreateModal">{{ t('siteSelection.createNewSite') }}</a>
        </div>

        <!-- Loading state -->
        <div v-if="siteStore.loading" class="state-box loading-box">
          <i class="fa-solid fa-circle-notch fa-spin"></i>
          <span>{{ t('siteSelection.loadingSites') }}</span>
        </div>

        <!-- Error state -->
        <div v-else-if="siteStore.error" class="state-box error-box">
          <i class="fa-solid fa-triangle-exclamation"></i>
          <span>{{ t('siteSelection.fetchError') }}</span>
          <button class="retry-btn" @click="siteStore.fetchSites()">Thử lại</button>
        </div>

        <!-- No sites -->
        <div v-else-if="!recentSite" class="state-box empty-box">
          <i class="fa-regular fa-folder-open"></i>
          <span>{{ t('siteSelection.noSites') }}</span>
        </div>

        <!-- Recent site card -->
        <div class="recent-site-card" v-else>
          <div class="site-card-left">
            <div class="site-avatar-square" :style="{ backgroundColor: recentSite.color || 'var(--color-accent)' }">
              {{ siteAvatarText }}
            </div>
            <div class="site-info-stack">
              <span class="site-name-bold">{{ recentSite.name }}</span>
              <div class="member-avatars">
                <!-- Chỉ hiển thị người dùng hiện tại. Chưa có API danh sách member → không thêm avatar/"+N" giả. -->
                <div v-if="userInitials" class="member-circle member-circle-current">{{ userInitials }}</div>
              </div>
            </div>
          </div>
          <div class="site-card-right">
            <router-link
              class="pill-btn orange site-entry-link"
              to="/dashboard"
              :data-site-id="getSiteId(recentSite)"
              @click.stop.prevent="goToSpaceProject(getSiteId(recentSite))"
              @keydown.enter.stop.prevent="goToSpaceProject(getSiteId(recentSite))"
            >{{ t('siteSelection.goToSpace') }}</router-link>
          </div>
        </div>

        <div class="card-footer-row">
          <a href="#" class="different-site-link" @click.prevent="router.push('/home/for-you')">{{ t('siteSelection.lookingForDifferent') }} &rarr;</a>

          <div class="decorative-stars">
            <svg width="60" height="60" viewBox="0 0 60 60" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M40 10 L43 20 L53 23 L43 26 L40 36 L37 26 L27 23 L37 20 Z" stroke="var(--color-warning)" stroke-width="2" stroke-linejoin="round"/>
              <path d="M15 35 L17 40 L22 42 L17 44 L15 49 L13 44 L8 42 L13 40 Z" stroke="var(--color-warning)" stroke-width="2" stroke-linejoin="round"/>
            </svg>
          </div>
        </div>
      </div>

      <div class="explore-section">
        <p>{{ t('siteSelection.exploreQuestion') }}</p>
        <!-- Chưa có route/tài liệu thật cho tính năng này → disable rõ ràng, không để nút bấm im lặng. -->
        <button class="explore-btn" disabled :title="t('siteSelection.notSupportedTitle')">{{ t('siteSelection.exploreFeatures') }}</button>
      </div>
    </main>

    <Teleport to="body">
    <!-- Create Site Modal -->
    <div class="modal-overlay sa-data-modal-overlay" v-if="isCreateModalOpen" @click.self="closeCreateModal">
      <div class="jira-modal">
        <div class="jira-modal-header">
          <DataModalHeader icon="bi bi-building-add" :title="t('siteSelection.startNewSite')" :description="t('siteSelection.pickUpShort')" @close="closeCreateModal" />
        </div>
        <div class="jira-modal-body">
          <DataModalSection icon="bi bi-globe2" :title="t('siteSelection.yourSite')">
          <div class="form-group">
            <label class="jira-label">{{ t('siteSelection.yourSite') }}</label>
            <div class="jira-input-wrapper" :class="validationState">
              <input
                type="text"
                v-model="newSiteName"
                class="jira-input"
              />
              <div class="jira-input-suffix">
                <span class="domain-text">.sprinta.vn</span>
                <i class="fa-solid fa-circle-notch fa-spin" v-if="validationState === 'checking'"></i>
                <i class="fa-solid fa-circle-check" v-else-if="validationState === 'success'"></i>
                <i class="fa-solid fa-triangle-exclamation" v-else-if="validationState === 'error'"></i>
              </div>
            </div>
            <div class="jira-error-text" v-if="validationState === 'error'">
              {{ errorMessage }}
            </div>
          </div>
          </DataModalSection>

          <button
            class="pill-btn blue full-width jira-continue-btn"
            :disabled="isCreating || validationState !== 'success'"
            @click="submitCreateSite"
          >
            {{ isCreating ? t('siteSelection.creating') : t('siteSelection.continueBtn') }}
          </button>

          <div class="jira-modal-footer">
            <span class="or-text">{{ t('siteSelection.or') }}</span><a href="#" class="join-link" @click.prevent="switchToJoinModal">{{ t('siteSelection.joinExisting') }}</a>
          </div>
        </div>
      </div>
    </div>

    <!-- Join / Pick Site Modal -->
    <div class="modal-overlay sa-data-modal-overlay" v-if="isJoinModalOpen" @click.self="closeJoinModal">
      <div class="jira-modal join-modal">
        <div class="jira-modal-header">
          <DataModalHeader icon="bi bi-buildings" :title="t('siteSelection.joinExisting')" :description="t('siteSelection.pickUpShort')" @close="closeJoinModal" />
        </div>
        <div class="jira-modal-body">
          <p class="logged-in-text" v-if="userEmail">
            {{ t('siteSelection.loggedInAs') }} <strong>{{ userEmail }}</strong>.
            <!-- switchAccount chưa có flow thật → disabled rõ ràng -->
            <button
              class="switch-account-btn"
              disabled
              :title="t('siteSelection.notSupportedTitle')"
            >{{ t('siteSelection.switchAccount') }}</button>
          </p>

          <DataModalSection icon="bi bi-list-ul" :title="t('siteSelection.joinExisting')">
          <div class="site-list-container">
            <div v-if="siteStore.loading" class="state-box loading-box">
              <i class="fa-solid fa-circle-notch fa-spin"></i>
              <span>{{ t('siteSelection.loadingSites') }}</span>
            </div>
            <div v-else-if="siteStore.error" class="state-box error-box">
              <i class="fa-solid fa-triangle-exclamation"></i>
              <span>{{ t('siteSelection.fetchError') }}</span>
            </div>
            <div v-else-if="!sites.length" class="state-box empty-box">
              <span>{{ t('siteSelection.noSites') }}</span>
            </div>
            <div class="site-list-item" v-for="site in sites" :key="site.id" v-else>
              <div class="site-list-item-left">
                <div class="site-list-item-title">SprintA</div>
                <div class="site-list-item-url">{{ site.name }}</div>
              </div>
              <div class="site-list-item-right">
                <router-link
                  class="pill-btn blue small site-entry-link"
                  to="/dashboard"
                  :data-site-id="getSiteId(site)"
                  @click.stop.prevent="goToSpaceProject(getSiteId(site))"
                  @keydown.enter.stop.prevent="goToSpaceProject(getSiteId(site))"
                >{{ t('siteSelection.goToSprintA') }}</router-link>
              </div>
            </div>
          </div>
          </DataModalSection>

          <div class="jira-modal-footer">
            <span class="or-text">{{ t('siteSelection.or') }}</span><a href="#" class="join-link" @click.prevent="switchToCreateModal">{{ t('siteSelection.startNewSite') }}</a>
          </div>
        </div>
      </div>
    </div>
    </Teleport>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useSiteStore } from '@/store/useSiteStore'
import { getStoredUser } from '@/utils/permissions'
import { useI18n } from '@/composables/useI18n'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import SprintaBrand from '@/components/branding/SprintaBrand.vue'

const router = useRouter()
const siteStore = useSiteStore()
const { t } = useI18n()

// ── User thật – không có fallback fake ──────────────────────────────────────
const currentUser = getStoredUser()
const userName = currentUser?.username || ''
const userEmail = currentUser?.email || ''
const userInitials = userName ? userName.substring(0, 1).toUpperCase() : ''

// ── Sites từ store (API thật) ───────────────────────────────────────────────
const recentSite = computed(() => siteStore.recentSite)
const sites = computed(() => siteStore.sites)

// Avatar text tính từ tên site thật (không có fallback cứng)
const siteAvatarText = computed(() => {
  if (!recentSite.value?.name) return '?'
  return recentSite.value.name.substring(0, 2).toUpperCase()
})

const getSiteId = (site) => site?.id || site?.Id || null

onMounted(async () => {
  await siteStore.fetchSites()
})

// ── Modal state ─────────────────────────────────────────────────────────────
const isCreateModalOpen = ref(false)
const isJoinModalOpen = ref(false)
const newSiteName = ref('')
const isCreating = ref(false)
const errorMessage = ref('')
const validationState = ref('idle') // idle, checking, success, error
let debounceTimer = null

watch(newSiteName, (newVal) => {
  if (!newVal) {
    validationState.value = 'idle'
    errorMessage.value = ''
    return
  }

  validationState.value = 'checking'
  const formattedName = newVal.toLowerCase().replace(/[^a-z0-9-]/g, '')

  clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    if (formattedName.length < 3) {
      validationState.value = 'error'
      errorMessage.value = t('siteSelection.siteNameMinLength')
    } else {
      validationState.value = 'success'
      errorMessage.value = ''
    }
  }, 500)
})

const openCreateModal = () => {
  isCreateModalOpen.value = true
  const baseName = userEmail
    ? userEmail.split('@')[0]
    : userName.toLowerCase().replace(/[^a-z0-9]/g, '')
  const randomSuffix = Math.floor(1000 + Math.random() * 9000)
  newSiteName.value = baseName ? `${baseName}-${randomSuffix}` : ''
  validationState.value = 'idle'
  errorMessage.value = ''
}

const closeCreateModal = () => { isCreateModalOpen.value = false }
const closeJoinModal = () => { isJoinModalOpen.value = false }

const switchToJoinModal = () => {
  isCreateModalOpen.value = false
  isJoinModalOpen.value = true
}

const switchToCreateModal = () => {
  isJoinModalOpen.value = false
  openCreateModal()
}

const submitCreateSite = async () => {
  if (validationState.value !== 'success') return
  isCreating.value = true
  errorMessage.value = ''
  try {
    const site = await siteStore.createSite({ name: newSiteName.value })
    goToSpaceProject(site.id)
  } catch (error) {
    validationState.value = 'error'
    errorMessage.value = error.message || t('siteSelection.createSiteFailed')
  } finally {
    isCreating.value = false
  }
}

const setRecentSiteForNavigation = (site) => {
  const siteId = getSiteId(site)
  if (!siteId) return
  const matchedSite = siteStore.sites.find(s => getSiteId(s) === siteId)
  siteStore.setRecentSite(matchedSite || { ...site, id: siteId })
}

const goToSpaceProject = async (siteId) => {
  if (!siteId) return
  const site = siteStore.sites.find(s => getSiteId(s) === siteId) || { id: siteId }
  siteStore.setRecentSite(site)
  isJoinModalOpen.value = false
  isCreateModalOpen.value = false
  await router.push('/dashboard')
}
</script>

<style scoped>
.start-page-wrapper {
  min-height: 100vh;
  background-color: var(--color-bg);
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
  display: flex;
  flex-direction: column;
}

.start-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  height: 64px;
  background-color: var(--color-surface);
  border-bottom: 1px solid var(--color-border);
  padding-right: 24px;
}

.header-left {
  display: flex;
  align-items: center;
  height: 100%;
}

.atlassian-brand-block {
  width: 64px;
  height: 100%;
  background-color: var(--sp-blue-700);
  color: var(--color-text-inverse);
  display: flex;
  align-items: center;
  justify-content: center;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 24px;
}

.pill-btn {
  border: none;
  border-radius: 24px;
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
  transition: background-color 0.2s;
  font-family: inherit;
}

.site-entry-link {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  text-decoration: none;
}

.pill-btn.blue {
  background-color: var(--color-accent);
  color: var(--color-text-inverse);
  padding: 8px 16px;
}
.pill-btn.blue:hover:not(:disabled) { background-color: var(--color-accent-hover); }

.pill-btn.orange {
  background-color: var(--color-accent);
  color: var(--color-text-inverse);
  padding: 8px 24px;
}
.pill-btn.orange:hover:not(:disabled) { background-color: var(--color-accent-hover); }

.user-profile {
  display: flex;
  align-items: center;
  gap: 8px;
  border-left: 1px solid var(--color-border);
  padding-left: 24px;
}

.user-avatar-circle {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background-color: var(--sp-blue-700);
  color: var(--color-text-inverse);
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: bold;
  font-size: 14px;
}

.user-name-text {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.start-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  padding-top: 64px;
}

.welcome-container {
  margin-bottom: 48px;
}

.welcome-title {
  font-size: 40px;
  font-weight: 800;
  color: var(--color-text-primary);
  margin: 0;
  letter-spacing: -1px;
}

.highlight-wrapper {
  position: relative;
  display: inline-block;
}

.squiggly-line {
  position: absolute;
  bottom: -4px;
  left: 0;
  width: 100%;
}

.card-section {
  width: 100%;
  max-width: 680px;
  position: relative;
}

.card-header-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.pickup-text {
  font-size: 14px;
  color: var(--color-text-primary);
  display: flex;
  align-items: center;
}

.create-site-link {
  font-size: 14px;
  font-weight: 600;
  color: color-mix(in srgb, var(--color-accent) 58%, var(--color-text-primary));
  text-decoration: none;
}
.create-site-link:hover { text-decoration: underline; }

/* Loading / Error / Empty states */
.state-box {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 20px 24px;
  border-radius: 4px;
  font-size: 14px;
  color: var(--color-text-secondary);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
}
.loading-box { color: color-mix(in srgb, var(--color-accent) 58%, var(--color-text-primary)); }
.error-box { color: var(--color-danger); border-color: var(--color-danger); }
.empty-box { color: var(--color-text-secondary); }
.retry-btn {
  margin-left: auto;
  background: none;
  border: 1px solid var(--color-danger);
  border-radius: 4px;
  color: var(--color-danger);
  font-size: 13px;
  padding: 4px 12px;
  cursor: pointer;
}
.retry-btn:hover { background: var(--color-danger-bg); }

.recent-site-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 4px;
  padding: 16px 24px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: 0 1px 1px color-mix(in srgb, var(--color-text-primary) 8%, transparent);
}

.site-card-left {
  display: flex;
  align-items: center;
  gap: 16px;
}

.site-avatar-square {
  width: 56px;
  height: 56px;
  border-radius: 4px;
  background-color: var(--color-accent);
  color: var(--color-text-inverse);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
  font-weight: bold;
}

.site-info-stack {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.site-name-bold {
  font-size: 18px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.member-avatars {
  display: flex;
  align-items: center;
}

.member-circle {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  border: 2px solid var(--color-surface);
  color: var(--color-text-inverse);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  font-weight: bold;
  margin-left: -6px;
}
.member-circle:first-child { margin-left: 0; }
.member-circle-current { background-color: var(--sp-blue-700); }

.card-footer-row {
  margin-top: 16px;
  position: relative;
}

.different-site-link {
  font-size: 12px;
  color: color-mix(in srgb, var(--color-accent) 58%, var(--color-text-primary));
  text-decoration: none;
}
.different-site-link:hover { text-decoration: underline; }

.decorative-stars {
  position: absolute;
  right: -60px;
  top: 0;
}

.explore-section {
  margin-top: 80px;
  display: flex;
  flex-direction: column;
  align-items: center;
}

.explore-section p {
  font-size: 16px;
  color: var(--color-text-primary);
  margin-bottom: 16px;
}

.explore-btn {
  background: transparent;
  border: 1px solid var(--color-text-primary);
  border-radius: 24px;
  padding: 8px 24px;
  font-weight: 600;
  font-size: 14px;
  color: var(--color-text-primary);
  cursor: pointer;
  transition: background-color 0.2s;
}
.explore-btn:hover:not(:disabled) { background-color: var(--color-surface-hover); }
.explore-btn:disabled { opacity: 0.5; cursor: not-allowed; }

/* Modal Styles */
.modal-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background-color: color-mix(in srgb, var(--color-text-primary) 54%, transparent);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.jira-modal {
  background-color: var(--color-surface-elevated);
  border-radius: 8px;
  width: 540px;
  box-shadow: 0 8px 16px -4px color-mix(in srgb, var(--color-text-primary) 25%, transparent);
  padding: 64px 48px;
}

.jira-modal-body {
  display: flex;
  flex-direction: column;
  align-items: center;
}

.text-center { text-align: center; }

.jira-modal-title {
  font-size: 28px;
  font-weight: bold;
  color: var(--color-text-primary);
  margin: 0 0 8px 0;
  letter-spacing: -0.5px;
  line-height: 1.2;
}

.jira-subtitle {
  font-size: 14px;
  color: var(--color-text-secondary);
  margin: 0 0 48px 0;
}

.form-group {
  width: 100%;
  margin-bottom: 24px;
}

.jira-label {
  display: block;
  font-size: 12px;
  color: var(--color-text-secondary);
  margin-bottom: 8px;
  font-weight: 500;
}

.jira-input-wrapper {
  display: flex;
  align-items: center;
  width: 100%;
  border: 2px solid var(--color-input-border);
  border-radius: 24px;
  padding: 0 16px;
  height: 48px;
  box-sizing: border-box;
  transition: border-color 0.2s;
  background: var(--color-input-bg);
}

.jira-input-wrapper:focus-within { border-color: var(--color-accent-hover); }
.jira-input-wrapper.checking { border-color: var(--color-accent); }
.jira-input-wrapper.success { border-color: var(--color-success); }
.jira-input-wrapper.error { border-color: var(--color-danger); }

.jira-input {
  flex: 1;
  border: none !important;
  outline: none !important;
  font-size: 16px !important;
  color: var(--color-text-primary) !important;
  background: transparent !important;
  background-color: transparent !important;
  box-shadow: none !important;
  padding: 0 !important;
  width: 100%;
}

.jira-input-suffix {
  display: flex;
  align-items: center;
  gap: 8px;
}

.domain-text {
  color: var(--color-text-secondary);
  font-size: 16px;
}

.jira-input-wrapper.checking .fa-spin { color: color-mix(in srgb, var(--color-accent) 58%, var(--color-text-primary)); }
.jira-input-wrapper.success .fa-circle-check { color: var(--color-success); font-size: 18px; }
.jira-input-wrapper.error .fa-triangle-exclamation { color: var(--color-danger); font-size: 18px; }

.jira-error-text {
  color: var(--color-danger);
  font-size: 12px;
  margin-top: 8px;
  font-weight: 500;
}

.pill-btn.full-width.jira-continue-btn {
  width: 100%;
  height: 48px;
  font-size: 16px;
  margin-top: 8px;
}
.pill-btn:disabled {
  background-color: var(--color-surface-hover) !important;
  color: var(--color-text-disabled) !important;
  cursor: not-allowed;
}

.jira-modal-footer {
  margin-top: 48px;
  font-size: 14px;
  text-align: center;
}

.or-text { color: var(--color-text-secondary); }

.join-link {
  color: color-mix(in srgb, var(--color-accent) 58%, var(--color-text-primary));
  text-decoration: none;
  font-weight: 500;
}
.join-link:hover { text-decoration: underline; }

.join-modal {
  width: 480px;
  padding: 48px 40px;
}

.logged-in-text {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin-top: -32px;
  margin-bottom: 24px;
  text-align: center;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  flex-wrap: wrap;
}
.logged-in-text strong { color: var(--color-text-primary); }

/* switchAccount – chưa có flow → disabled dạng nút nhỏ */
.switch-account-btn {
  background: none;
  border: none;
  color: var(--color-text-disabled);
  font-size: 12px;
  cursor: not-allowed;
  text-decoration: underline;
  padding: 0;
  font-family: inherit;
}

.site-list-container {
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 12px;
  max-height: 350px;
  overflow-y: auto;
  margin-bottom: 8px;
  scrollbar-width: thin;
  scrollbar-color: var(--color-border) transparent;
}
.site-list-container::-webkit-scrollbar { width: 6px; }
.site-list-container::-webkit-scrollbar-track { background: transparent; }
.site-list-container::-webkit-scrollbar-thumb {
  background-color: var(--color-border);
  border-radius: 10px;
}

.site-list-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  border: 1px solid var(--color-border);
  border-radius: 4px;
  padding: 12px 16px;
  background-color: var(--color-surface);
  transition: background-color 0.2s, box-shadow 0.2s;
}
.site-list-item:hover { background-color: var(--color-surface-hover); }

.site-list-item-left {
  display: flex;
  flex-direction: column;
  gap: 4px;
  text-align: left;
}

.site-list-item-title {
  font-size: 12px;
  color: var(--color-text-secondary);
}

.site-list-item-url {
  font-size: 14px;
  color: var(--color-text-primary);
}

.pill-btn.small {
  padding: 6px 16px;
  font-size: 13px;
}

/* SprintA product controls — remove the mixed Jira/orange visual language. */
.start-content {
  width: min(920px, calc(100% - 32px));
  padding-top: clamp(42px, 7vh, 72px);
}

.welcome-title {
  font-size: clamp(38px, 4vw, 58px);
  line-height: 1.05;
  letter-spacing: -.045em;
}

.card-section,
.explore-section {
  border: 1px solid color-mix(in srgb, var(--sp-sky-400) 22%, var(--color-border));
  border-radius: 16px;
  background: color-mix(in srgb, var(--color-surface) 94%, var(--sp-blue-600) 6%);
  box-shadow: 0 14px 36px color-mix(in srgb, var(--color-text-primary) 12%, transparent);
}

.recent-site-card {
  min-height: 96px;
  border: 1px solid var(--color-border);
  border-radius: 14px;
  background: color-mix(in srgb, var(--color-surface) 96%, var(--sp-slate-500) 4%);
}

.pill-btn.orange,
.pill-btn.blue {
  min-height: 40px;
  padding: 9px 18px;
  border: 1px solid var(--sp-blue-700);
  border-radius: 10px;
  color: var(--color-text-inverse) !important;
  text-shadow: none;
  background: var(--sp-blue-700) !important;
  background-image: none !important;
  box-shadow: 0 8px 18px color-mix(in srgb, var(--sp-blue-700) 22%, transparent);
}

.pill-btn.orange:hover:not(:disabled),
.pill-btn.blue:hover:not(:disabled) {
  border-color: var(--sp-sky-400);
  background: var(--sp-blue-600) !important;
  background-image: none !important;
  transform: translateY(-1px);
}

.explore-btn:disabled {
  border: 1px solid var(--color-border);
  color: var(--color-text-muted);
  background: var(--color-surface-hover);
  opacity: .68;
  box-shadow: none;
}

.site-avatar-square {
  background: var(--sp-blue-700) !important;
  box-shadow: none;
}

@media (max-height: 760px) and (min-width: 721px) {
  .start-content { padding-top: 30px; }
  .welcome-title { font-size: 42px; }
  .explore-section { margin-top: 22px; padding-block: 18px; }
}
</style>
