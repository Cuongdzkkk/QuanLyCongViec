<template>
  <header class="app-topbar">
    <div class="nav-left">
      <div class="app-launcher-icon">
        <span class="grid-icon">⋮⋮⋮</span>
      </div>

      <div class="sprinta-logo" @click="router.push('/site-selection')">
        <img src="@/assets/logo_QLCV.png" alt="SprintA Logo" class="sprinta-logo-img" />
        <span class="logo-text">SprintA</span>
      </div>

      <!-- Home Site Context Navigation -->
      <nav v-if="isHomeContext" class="topbar-nav">
        <!-- Removed Teams, Goals, Projects, People links as requested by user -->
      </nav>

      <!-- Space Project Context Navigation -->
      <div v-else-if="isSpaceContext" class="space-nav">
        <div class="workspace-switcher" @click="router.push('/spaces')">
          <div class="ws-icon">{{ workspaceBadge }}</div>
          <span class="ws-name">{{ workspaceName }}</span>
          <i class="fa-solid fa-chevron-down ms-1"></i>
        </div>
        <button class="menu-toggle" @click="emit('toggle-sidebar')">
          <i class="fa-solid fa-bars-staggered"></i>
        </button>
      </div>
    </div>

    <div class="nav-center" ref="searchWrapperRef">
      <div class="search-input-wrapper">
        <i class="fa-solid fa-magnifying-glass search-icon"></i>
        <input type="text" :placeholder="isSpaceContext ? t('Search work items...') : t('Search')" v-model="searchQuery" @input="handleSearchInput" />
        <div v-if="showSearchDropdown" class="search-dropdown">
          <div v-if="searching" class="search-state">{{ t('Searching...', 'Đang tìm kiếm...') }}</div>
          <template v-else-if="searchResults.length">
            <button v-for="result in searchResults" :key="result.id" type="button" class="search-result" @click="openSearchResult(result)">
              <strong>{{ result.sequenceId || demoText(result.title) || demoText(result.name) }}</strong>
              <span>{{ demoText(result.title) || demoText(result.description) || result.type }}</span>
              <small v-if="result.projectName">{{ demoText(result.projectName) }}</small>
            </button>
          </template>
          <div v-else class="search-state">{{ t('No items found.', 'Không tìm thấy kết quả.') }}</div>
        </div>
      </div>
      <button v-if="isHomeContext" class="topbar-btn create-btn" @click="handleGlobalCreate">{{ t('Create', 'Tạo') }}</button>
    </div>

    <div class="nav-right">
      <!-- Language Flags -->
      <div class="lang-selector hidden-mobile">
        <button class="flag-btn" :class="{ active: i18nStore.locale === 'vi' }" @click="i18nStore.setLocale('vi')" title="Tiếng Việt">
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 3 2" class="flag-svg">
            <rect width="3" height="2" fill="#da251d"/>
            <polygon points="1.5,0.4 1.62,0.85 2.1,0.85 1.71,1.13 1.86,1.6 1.5,1.31 1.14,1.6 1.29,1.13 0.9,0.85 1.38,0.85" fill="#ffff00"/>
          </svg>
        </button>
        <button class="flag-btn" :class="{ active: i18nStore.locale === 'en' }" @click="i18nStore.setLocale('en')" title="English">
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 60 30" class="flag-svg">
            <clipPath id="s">
              <path d="M0,0 v30 h60 v-30 z"/>
            </clipPath>
            <clipPath id="t">
              <path d="M0,0 L30,15 L0,30 L60,30 L30,15 L60,0 z"/>
            </clipPath>
            <g clip-path="url(#s)">
              <rect width="60" height="30" fill="#012169"/>
              <path d="M0,0 L60,30 M0,30 L60,0" stroke="#fff" stroke-width="6"/>
              <path d="M0,0 L60,30 M0,30 L60,0" stroke="#c8102e" stroke-width="4" clip-path="url(#t)"/>
              <path d="M30,0 v30 M0,15 h60" stroke="#fff" stroke-width="10"/>
              <path d="M30,0 v30 M0,15 h60" stroke="#c8102e" stroke-width="6"/>
            </g>
          </svg>
        </button>
      </div>

      <NotificationsDropdown v-if="isSpaceContext" />
      <button class="icon-btn" @click="goToNotifications" v-else>
        <i class="fa-regular fa-bell"></i>
      </button>

      <button
        class="icon-btn theme-toggle-btn hidden-mobile"
        :class="{ active: currentTheme === 'dark' }"
        @click="toggleTheme()"
        :title="currentTheme === 'dark' ? 'Dang dung dark mode - bam de sang light mode' : 'Dang dung light mode - bam de sang dark mode'"
        :aria-label="currentTheme === 'dark' ? 'Dang dung dark mode - bam de sang light mode' : 'Dang dung light mode - bam de sang dark mode'"
      >
        <i :class="currentTheme === 'dark' ? 'fa-solid fa-moon' : 'fa-solid fa-sun'"></i>
      </button>

      <button class="icon-btn ai-topbar-btn hidden-mobile" @click="emit('toggle-ai')" title="SprintA AI">
        <img src="/ai-sprinta/idle.png" alt="" aria-hidden="true" />
      </button>

      <div class="hidden-mobile">
        <SettingsDropdown />
      </div>
      <UserDropdown class="nav-item" />
    </div>
  </header>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import UserDropdown from '@/components/UserDropdown.vue'
import NotificationsDropdown from '@/components/NotificationsDropdown.vue'
import SettingsDropdown from '@/components/SettingsDropdown.vue'
import { useProjectStore } from '@/store/useProjectStore'
import { usePeopleStore } from '@/store/usePeopleStore'
import { subscribeAdminRealtime } from '@/utils/adminRealtime'
import { getScopedCurrentProjectId, setScopedCurrentProjectId } from '@/utils/projectContext'
import { useI18nStore } from '@/store/useI18nStore'
import { toggleTheme, currentTheme } from '@/utils/theme'
import { translateDemoText } from '@/utils/demoContentLocale'

const emit = defineEmits(['toggle-sidebar', 'toggle-ai', 'toggle-create'])

const handleGlobalCreate = () => {
  window.dispatchEvent(new CustomEvent('global-create-click'))
}

const router = useRouter()
const route = useRoute()
const projectStore = useProjectStore()
const peopleStore = usePeopleStore()
const i18nStore = useI18nStore()
const t = (key) => i18nStore.t(key)
const demoText = (value) => translateDemoText(value, i18nStore.locale)

const isHomeContext = computed(() => route.path.startsWith('/home') || route.path.startsWith('/sites'))
const isSpaceContext = computed(() => !!route.meta.isSpaceContext)

const isModule = (moduleName) => {
  if (moduleName === 'people') {
    return route.path.includes('/home/people') || route.path.includes('/home/profile')
  }
  return route.path.includes(`/home/${moduleName}`)
}

const searchQuery = ref('')
const searchResults = ref([])
const searching = ref(false)
const searchWrapperRef = ref(null)
let searchTimer = null
let searchAbortController = null
let searchRequestId = 0

const routeProjectId = computed(() => {
  if (isSpaceContext.value && route.params.id) return route.params.id
  return getScopedCurrentProjectId() || ''
})
const currentProjectId = computed(() => routeProjectId.value)
const activeProject = computed(() => projectStore.allProjects.find(project => project.id === currentProjectId.value) || projectStore.currentProject)
const workspaceName = computed(() => demoText(activeProject.value?.name) || 'SprintA')
const workspaceBadge = computed(() => activeProject.value?.icon || workspaceName.value.charAt(0).toUpperCase())
const showSearchDropdown = computed(() => searchQuery.value.trim().length > 0 && (searching.value || searchResults.value.length > 0))

const runSearch = async () => {
  const keyword = searchQuery.value.trim()
  if (!keyword) {
    searchAbortController?.abort()
    searchResults.value = []
    searching.value = false
    return
  }

  searchAbortController?.abort()
  const controller = new AbortController()
  searchAbortController = controller
  const requestId = searchRequestId + 1
  searchRequestId = requestId
  searching.value = true

  if (isHomeContext.value) {
    // Global Home Search API is not available yet, using empty fallback
    setTimeout(() => {
      if (requestId !== searchRequestId) return
      searchResults.value = []
      searching.value = false
      searchAbortController = null
    }, 200)
    return
  }

  // Real Search for Space Project Context
  try {
    const response = await axiosClient.get('/worktasks', {
      params: { search: keyword },
      signal: controller.signal
    })
    if (requestId !== searchRequestId) {
      return
    }
    searchResults.value = response.data?.data || []
  } catch (error) {
    if (error?.name === 'CanceledError' || error?.code === 'ERR_CANCELED') {
      return
    }
    searchResults.value = []
  } finally {
    if (requestId === searchRequestId) {
      searching.value = false
      searchAbortController = null
    }
  }
}

const handleSearchInput = () => {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(runSearch, 250)
}

const openSearchResult = (result) => {
  searchAbortController?.abort()
  searchResults.value = []
  searchQuery.value = ''

  if (isHomeContext.value) {
    // Navigate logic pending global search API implementation
    console.log('Global search item clicked:', result)
    return
  }

  router.push(`/space/${result.projectId}?task=${result.id}`)
}

const handleClickOutside = (e) => {
  if (searchWrapperRef.value && !searchWrapperRef.value.contains(e.target)) {
    searchResults.value = []
    searchQuery.value = ''
  }
}

const handleEscKey = (e) => {
  if (e.key === 'Escape') {
    searchResults.value = []
    searchQuery.value = ''
  }
}

const goToNotifications = () => {
  if (isHomeContext.value) {
    router.push('/home/notifications')
  }
}

watch(currentProjectId, (projectId) => {
  if (!projectId) {
    projectStore.clearProjectContext()
    return
  }

  if (isSpaceContext.value) {
    setScopedCurrentProjectId(projectId)
    projectStore.fetchProjectDetails(projectId, { force: true }).catch(() => {})
  }
}, { immediate: true })

onMounted(() => {
  if (isSpaceContext.value) {
    projectStore.fetchAllProjects(true).catch(() => {})
  }
  peopleStore.fetchPeople().catch(() => {})
  document.addEventListener('click', handleClickOutside)
  document.addEventListener('keydown', handleEscKey)
})

let unsubscribeAdminRealtime = null

onMounted(() => {
  unsubscribeAdminRealtime = subscribeAdminRealtime(async ({ type, payload }) => {
    const activeProjectId = routeProjectId.value || null

    if (payload?.projectId && activeProjectId && `${payload.projectId}` !== `${activeProjectId}`) {
      await projectStore.fetchAllProjects(true).catch(() => {})
      return
    }

    if (
      [
        'project-settings-updated',
        'project-settings-favorite-updated',
        'project-settings-integrations-updated',
        'project-administration-updated',
        'project-settings-deleted'
      ].includes(type)
    ) {
      await projectStore.fetchAllProjects(true).catch(() => {})
      if (activeProjectId && type !== 'project-settings-deleted') {
        await projectStore.fetchProjectDetails(activeProjectId, { force: true }).catch(() => {})
      }
    }
  })
})

onBeforeUnmount(() => {
  if (searchTimer) clearTimeout(searchTimer)
  searchAbortController?.abort()
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
  document.removeEventListener('keydown', handleEscKey)
  unsubscribeAdminRealtime?.()
})
</script>

<style scoped>
.app-topbar {
  height: var(--sa-topbar-height, 52px);
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--sa-topbar) 92%, #1d4ed8 8%), var(--sa-topbar)),
    var(--sa-topbar);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 12px;
  flex-shrink: 0;
  z-index: 1001;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  box-shadow: 0 8px 28px rgb(2 8 23 / 0.16);
}

.nav-left {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.app-launcher-icon {
  width: 30px;
  height: 30px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: rgba(226, 239, 255, 0.86);
  border-radius: var(--sa-radius-md);
  border: 1px solid transparent;
}

.app-launcher-icon:hover {
  background-color: rgba(255, 255, 255, 0.10);
  border-color: rgba(255, 255, 255, 0.12);
  color: #ffffff;
}

.grid-icon {
  font-size: 18px;
  line-height: 1;
  letter-spacing: -2px;
}

.sprinta-logo {
  display: flex;
  align-items: center;
  gap: 0;
  margin-right: 6px;
  cursor: pointer;
  min-height: 32px;
}

.sprinta-logo-img {
  height: 22px;
  width: auto;
  object-fit: contain;
  transform: scale(2.55);
  margin-right: 10px;
  margin-left: 6px;
  filter: drop-shadow(0 6px 14px rgba(14, 165, 233, 0.28));
}

.logo-text {
  font-size: 18px;
  font-weight: 800;
  color: #FFFFFF;
  letter-spacing: -0.03em;
}

/* Home Site Nav */
.topbar-nav {
  display: flex;
  align-items: center;
  gap: 4px;
}

.topbar-link {
  padding: 6px 10px;
  color: rgba(226, 239, 255, 0.82);
  text-decoration: none;
  font-weight: 600;
  font-size: 12.5px;
  border-radius: 9px;
  transition: background-color 0.2s, color 0.2s;
}

.topbar-link:hover {
  background-color: rgba(255, 255, 255, 0.2);
  color: #FFFFFF;
}

.topbar-link.active {
  color: #FFFFFF;
  background-color: rgba(255, 255, 255, 0.1);
}

.create-btn {
  background-color: var(--sa-primary);
  color: white;
  border: none;
  padding: 6px 10px;
  border-radius: 8px;
  font-weight: 700;
  font-size: 12.5px;
  margin-left: 8px;
  cursor: pointer;
  box-shadow: 0 10px 24px rgba(14, 165, 233, 0.24);
}

.create-btn:hover {
  background-color: var(--color-accent-hover);
}

/* Space Nav */
.space-nav {
  display: flex;
  align-items: center;
  gap: 8px;
}

.workspace-switcher {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 32px;
  padding: 4px 9px;
  border-radius: 9px;
  cursor: pointer;
  transition: background 0.2s;
  color: rgba(226, 239, 255, 0.90);
  border: 1px solid rgba(255, 255, 255, 0.08);
  background: rgba(255, 255, 255, 0.045);
}

.workspace-switcher:hover {
  background: rgba(255, 255, 255, 0.12);
  color: #FFFFFF;
}

.ws-icon {
  width: 22px;
  height: 22px;
  border-radius: 7px;
  background: linear-gradient(135deg, var(--sa-primary), #22d3ee);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 700;
}

.ws-name {
  color: inherit;
  font-size: 12.5px;
  font-weight: 700;
  letter-spacing: -0.02em;
  max-width: 180px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.menu-toggle {
  display: none;
  background: transparent;
  border: none;
  color: rgba(226, 239, 255, 0.88);
  cursor: pointer;
  padding: 7px;
  border-radius: 9px;
}

.nav-center {
  flex: 1;
  display: flex;
  justify-content: center;
  align-items: center;
  min-width: 260px;
}

.search-input-wrapper {
  position: relative;
  display: flex;
  align-items: center;
  background-color: rgba(255, 255, 255, 0.10);
  border: 1px solid rgba(203, 213, 225, 0.22);
  border-radius: 9px;
  padding: 0 11px;
  width: min(400px, 36vw);
  height: 34px;
  transition: all 0.2s ease;
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.06);
}

.search-input-wrapper:focus-within {
  border-color: rgba(125, 211, 252, 0.88);
  background-color: #ffffff;
  box-shadow: 0 0 0 3px rgba(56, 189, 248, 0.20), 0 12px 28px rgba(2, 8, 23, 0.18);
}

.search-input-wrapper:focus-within .search-icon {
  color: #6b778c;
}

.search-input-wrapper:focus-within input {
  color: #172b4d;
}

.search-icon {
  color: rgba(226, 239, 255, 0.78);
  font-size: 13px;
  margin-right: 8px;
}

.search-input-wrapper input {
  background: transparent !important;
  border: none !important;
  color: rgba(241, 245, 249, 0.94) !important;
  font-size: 12.5px;
  width: 100%;
  outline: none;
  height: auto !important;
  padding: 0 !important;
  box-shadow: none !important;
}

.search-input-wrapper input::placeholder {
  color: rgba(226, 239, 255, 0.56);
}

.search-input-wrapper:focus-within input::placeholder {
  color: #94a3b8;
}

.search-dropdown {
  position: absolute;
  top: calc(100% + 8px);
  left: 0;
  right: 0;
  background: var(--color-surface, #ffffff);
  border: 1px solid var(--color-border, #dfe1e6);
  border-radius: 12px;
  overflow: hidden;
  box-shadow: var(--sa-shadow-sm), 0 20px 60px rgba(15, 23, 42, 0.14);
  z-index: 20;
}

.search-result,
.search-state {
  width: 100%;
  display: grid;
  gap: 4px;
  text-align: left;
  padding: 12px 14px;
  color: var(--color-text-primary, #172b4d);
}

.search-result {
  border: none;
  background: transparent;
  cursor: pointer;
  border-bottom: 1px solid var(--color-border, #f4f5f7);
}

.search-result:last-child {
  border-bottom: none;
}

.search-result:hover {
  background: var(--color-surface-hover, #f4f5f7);
}

.search-result span {
  color: var(--color-text-secondary, #42526e);
  font-size: 13px;
}

.search-result small,
.search-state {
  color: var(--color-text-muted, #6b778c);
  font-size: 11px;
}

.nav-right {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 6px;
  min-width: 0;
}

.nav-right button,
.nav-right button i,
.nav-right button svg,
.nav-right .el-dropdown,
.nav-right .el-dropdown *,
.nav-right .nav-item,
.nav-right .nav-item * {
  color: rgba(226, 239, 255, 0.92);
}

.icon-btn {
  background: rgba(255, 255, 255, 0.055);
  border: 1px solid transparent;
  font-size: 13px;
  cursor: pointer;
  width: 30px;
  height: 30px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(226, 239, 255, 0.86);
}

.icon-btn i,
.app-launcher-icon,
.workspace-switcher i,
.menu-toggle i {
  color: currentColor;
}

.icon-btn:hover {
  background-color: rgba(255, 255, 255, 0.13);
  border-color: rgba(255, 255, 255, 0.12);
  color: #ffffff;
}

.icon-btn:hover i,
.icon-btn:hover svg,
.nav-right button:hover,
.nav-right button:hover i,
.nav-right button:hover svg {
  color: #ffffff;
}

.ai-topbar-btn {
  overflow: hidden;
  padding: 3px;
  border-color: rgba(226, 239, 255, 0.18);
}

.ai-topbar-btn img {
  width: 25px;
  height: 25px;
  object-fit: contain;
  display: block;
}

.ai-topbar-btn:hover {
  border-color: rgba(226, 239, 255, 0.34);
  background: rgba(255, 255, 255, 0.10);
}

.theme-toggle-btn.active {
  border-color: rgba(125, 211, 252, 0.42);
  background: rgba(56, 189, 248, 0.16);
  color: #f8fafc;
}

[data-theme='light'] .app-topbar,
[data-theme='dark'] .app-topbar {
  color: #f8fafc;
}

[data-theme='light'] .icon-btn,
[data-theme='dark'] .icon-btn,
[data-theme='light'] .app-launcher-icon,
[data-theme='dark'] .app-launcher-icon,
[data-theme='light'] .menu-toggle,
[data-theme='dark'] .menu-toggle {
  color: rgba(226, 239, 255, 0.92);
}

[data-theme='light'] .icon-btn:hover,
[data-theme='dark'] .icon-btn:hover,
[data-theme='light'] .app-launcher-icon:hover,
[data-theme='dark'] .app-launcher-icon:hover {
  color: #ffffff;
}

@media (max-width: 1024px) {
  .menu-toggle { display: block; }
  .nav-center { display: none; }
}

@media (max-width: 680px) {
  .app-topbar {
    height: 48px;
    padding: 0 8px;
  }

  .sprinta-logo-img {
    height: 18px;
    transform: scale(2.25);
    margin-left: 3px;
    margin-right: 6px;
  }

  .logo-text {
    font-size: 16px;
  }

  .workspace-switcher {
    max-width: 150px;
    min-height: 30px;
  }

  .ws-name {
    max-width: 92px;
  }

  .nav-right {
    gap: 4px;
  }

  .icon-btn,
  .app-launcher-icon {
    width: 28px;
    height: 28px;
  }

  .settings-btn,
  .bot-btn,
  .notification-btn,
  .user-name,
  .hidden-mobile {
    display: none !important;
  }
}

.lang-selector {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-right: 2px;
}

.flag-btn {
  background: rgba(255, 255, 255, 0.055);
  border: 1px solid transparent;
  padding: 2px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 8px;
  transition: all 0.2s ease;
  opacity: 0.6;
  color: rgba(226, 239, 255, 0.92);
}

.flag-btn:hover {
  opacity: 1;
  background-color: rgba(255, 255, 255, 0.1);
  border-color: rgba(255, 255, 255, 0.2);
}

.flag-btn.active {
  opacity: 1;
  border-color: rgba(56, 189, 248, 0.75);
  background-color: rgba(56, 189, 248, 0.14);
}

.flag-svg {
  width: 21px;
  height: 14px;
  border-radius: 2px;
  display: block;
  object-fit: cover;
  color: inherit;
}
</style>
