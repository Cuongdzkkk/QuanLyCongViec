<template>
  <div class="project-layout-wrapper">
    <template v-if="accessState === 'checking'">
      <div class="project-access-state" role="status" aria-live="polite">
        <i class="fa-solid fa-spinner fa-spin" aria-hidden="true"></i>
        <span>{{ t('Checking project access...', 'Đang kiểm tra quyền truy cập dự án...') }}</span>
      </div>
    </template>

    <template v-else-if="accessState === 'denied'">
      <div class="project-access-state project-access-denied" role="alert">
        <div class="project-access-icon" aria-hidden="true">
          <i class="fa-solid fa-lock"></i>
        </div>
        <h1>{{ t('Project access denied', 'Không thể truy cập dự án') }}</h1>
        <p>{{ t('You are not a member of this project.', 'Bạn không có trong danh sách thành viên của dự án này.') }}</p>
        <button type="button" class="project-access-back" @click="router.push('/dashboard')">
          {{ t('Back to dashboard', 'Quay lại trang tổng quan') }}
        </button>
      </div>
    </template>

    <template v-else>
      <!-- Project Header -->
      <header class="project-global-header">
      <div class="pgh-content">
        <div class="pgh-left">
          <ProjectAvatar :icon="project?.icon" :background="project?.cover" size="md" />
          <div class="pgh-info">
            <h1 class="pgh-title">{{ demoText(project?.name) || 'Loading Project...' }}</h1>
            <p class="pgh-desc" v-if="project?.description">{{ demoText(project.description) }}</p>
          </div>
          <div class="pgh-status" v-if="project?.status">
            {{ project.status }}
          </div>
        </div>
        <div class="pgh-right">
        </div>
      </div>

      <!-- Horizontal Navigation -->
      <nav class="project-horizontal-nav" ref="navScrollRef">
        <div class="nav-links">
          <router-link 
            v-for="nav in projectNavLinks" 
            :key="nav.name"
            :to="buildSpacePath(project || projectId, nav.path)"
            class="nav-item"
            active-class="nav-active"
          >
            <i :class="nav.icon"></i>
            <span>{{ nav.label }}</span>
          </router-link>
        </div>
      </nav>
    </header>

    <!-- Project Content (Children Routes) -->
    <main class="project-main-content">
      <router-view v-slot="{ Component }">
        <Transition name="fade-fast" mode="out-in">
          <component :is="Component" />
        </Transition>
      </router-view>
      </main>
    </template>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'
import { useProjectStore } from '@/store/useProjectStore'
import { useAuthStore } from '@/store/useAuthStore'
import { useI18n } from '@/composables/useI18n'
import { translateDemoText } from '@/utils/demoContentLocale'
import ProjectAvatar from '@/components/project/ProjectAvatar.vue'
import { projectAccessRestrictionsEnabled } from '@/config/projectAccess'
import { buildSpacePath } from '@/utils/spaceRoute'

const route = useRoute()
const router = useRouter()
const projectStore = useProjectStore()
const authStore = useAuthStore()
const { t, language } = useI18n()
const demoText = (value) => translateDemoText(value, language.value)

const projectId = computed(() => route.params.id)
const project = computed(() => projectStore.currentProject)
const accessState = ref('checking')
const accessRequestId = ref(0)
const lastDeniedProjectId = ref(null)
const ACCESS_CHECK_TIMEOUT_MS = 5000

const showCreateTaskModal = ref(false)

const projectNavLinks = computed(() => [
  { name: 'WorkItems', path: 'work-items', label: t('Work items', 'Công việc'), icon: 'fa-solid fa-layer-group' },
  { name: 'Cycles', path: 'cycles', label: t('Cycles', 'Chu kỳ'), icon: 'fa-solid fa-rotate' },
  { name: 'Modules', path: 'modules', label: t('Modules', 'Phân hệ'), icon: 'fa-solid fa-cubes' },
  { name: 'Reports', path: 'reports', label: t('Reports', 'Báo cáo'), icon: 'fa-solid fa-chart-line' },
  { name: 'Intakes', path: 'intakes', label: t('Intakes', 'Yêu cầu (Intake)'), icon: 'fa-solid fa-inbox' },
  { name: 'Views', path: 'views', label: t('Views', 'Góc nhìn'), icon: 'fa-regular fa-eye' },
  { name: 'Pages', path: 'pages', label: t('Pages', 'Tài liệu'), icon: 'fa-regular fa-file-lines' },
  { name: 'Members', path: 'members', label: t('Members', 'Thành viên'), icon: 'fa-solid fa-users' }
])

const normalizeIdentity = value => String(value ?? '').trim().toLowerCase()

const memberIdentity = member => normalizeIdentity(
  member?.userId
  ?? member?.UserId
  ?? member?.id
  ?? member?.Id
  ?? member?.user?.id
  ?? member?.user?.userId
)

const memberEmail = member => normalizeIdentity(
  member?.email
  ?? member?.Email
  ?? member?.user?.email
  ?? member?.user?.Email
)

const currentUserIsMember = members => {
  const user = authStore.currentUser || {}
  const userId = normalizeIdentity(user.id ?? user.userId ?? user.Id ?? user.UserId)
  const email = normalizeIdentity(user.email ?? user.Email)

  if (!userId && !email) return false

  return (Array.isArray(members) ? members : []).some(member => {
    const matchesId = userId && memberIdentity(member) === userId
    const matchesEmail = email && memberEmail(member) === email
    return Boolean(matchesId || matchesEmail)
  })
}

const denyProjectAccess = projectKey => {
  accessState.value = 'denied'
  if (lastDeniedProjectId.value === projectKey) return

  lastDeniedProjectId.value = projectKey
  ElMessage.closeAll()
  ElMessage.error(t(
    'You cannot access this project because you are not a member.',
    'Bạn không thể truy cập dự án này vì bạn không có trong danh sách thành viên.'
  ))
}

const fetchMembersForAccess = async projectKey => {
  const response = await axiosClient.get(`/projects/${projectKey}/members`, {
    timeout: ACCESS_CHECK_TIMEOUT_MS
  })
  return response.data?.data || []
}

const fetchProjectWithTimeout = projectKey => {
  const request = projectStore.fetchProjectDetails(projectKey, { force: true })
  const timeout = new Promise((resolve) => {
    window.setTimeout(() => resolve(null), ACCESS_CHECK_TIMEOUT_MS)
  })
  return Promise.race([request, timeout])
}

const redirectWorkspaceRouteToDashboard = async targetId => {
  await projectStore.fetchAllProjects(true).catch(() => [])
  const matchingProject = projectStore.sidebarProjects.find(item => item.id === targetId)
  if (matchingProject) return false
  await router.replace('/dashboard')
  return true
}

const loadProject = async () => {
  const targetProjectId = String(projectId.value || '')
  if (!targetProjectId || !route.path.startsWith('/space/')) return

  const requestId = accessRequestId.value + 1
  accessRequestId.value = requestId
  accessState.value = 'checking'

  if (!projectAccessRestrictionsEnabled) {
    lastDeniedProjectId.value = null
    const loadedProject = await fetchProjectWithTimeout(targetProjectId)
    if (!loadedProject) {
      const redirected = await redirectWorkspaceRouteToDashboard(targetProjectId)
      if (redirected) return
    }
    accessState.value = 'allowed'
    return
  }

  let members
  try {
    members = await fetchMembersForAccess(targetProjectId)
  } catch {
    if (requestId === accessRequestId.value) denyProjectAccess(targetProjectId)
    return
  }

  if (requestId !== accessRequestId.value) return
  if (!currentUserIsMember(members)) {
    denyProjectAccess(targetProjectId)
    return
  }

  const loadedProject = await fetchProjectWithTimeout(targetProjectId)
  if (requestId !== accessRequestId.value) return

  if (!loadedProject || !currentUserIsMember(members)) {
    denyProjectAccess(targetProjectId)
    return
  }

  lastDeniedProjectId.value = null
  accessState.value = 'allowed'
}

onMounted(() => {
  loadProject()
})

watch(projectId, () => {
  loadProject()
})

const createTask = () => {
  window.dispatchEvent(new CustomEvent('open-create-task', { detail: { statusName: 'TO DO' } }))
  if (route.name !== 'SpaceSummary') {
    router.push(buildSpacePath(project.value || projectId.value, 'work-items'))
  }
}
</script>

<style scoped>
.project-layout-wrapper {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  flex: 1;
  overflow: hidden;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-bg) 76%, var(--color-surface)), var(--color-bg));
  color: var(--color-text-primary);
}

.project-access-state {
  min-height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 32px;
  color: var(--color-text-secondary);
  text-align: center;
}

.project-access-state > i {
  font-size: 24px;
  color: var(--color-accent);
}

.project-access-icon {
  width: 56px;
  height: 56px;
  display: grid;
  place-items: center;
  border-radius: 16px;
  background: color-mix(in srgb, var(--color-danger, #de350b) 12%, var(--color-surface));
  color: var(--color-danger, #de350b);
  font-size: 22px;
}

.project-access-state h1 {
  margin: 4px 0 0;
  color: var(--color-text-primary);
  font-size: 20px;
}

.project-access-state p {
  margin: 0;
  color: var(--color-text-secondary);
}

.project-access-back {
  margin-top: 8px;
  border: 1px solid color-mix(in srgb, var(--color-accent) 28%, var(--color-border));
  border-radius: 8px;
  padding: 9px 14px;
  background: var(--color-accent);
  color: #fff;
  font: inherit;
  font-weight: 700;
  cursor: pointer;
}

.project-global-header {
  border-bottom: 1px solid color-mix(in srgb, var(--color-border) 82%, transparent);
  background: var(--color-surface);
  box-shadow: 0 3px 10px color-mix(in srgb, #020617 7%, transparent);
  backdrop-filter: none;
  -webkit-backdrop-filter: none;
  flex-shrink: 0;
  position: sticky;
  top: 0;
  z-index: 40;
  isolation: isolate;
}

.pgh-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 24px 8px;
}

.pgh-left {
  display: flex;
  align-items: center;
  gap: 12px;
  overflow: hidden;
}

.pgh-icon {
  width: 34px;
  height: 34px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 17px;
  flex-shrink: 0;
  box-shadow:
    0 12px 28px color-mix(in srgb, var(--color-accent) 18%, transparent),
    inset 0 1px 0 rgba(255,255,255,0.22);
}

.pgh-info {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.pgh-title {
  margin: 0;
  font-size: 19px;
  font-weight: 850;
  color: var(--color-text-primary, #172b4d);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.pgh-desc {
  margin: 2px 0 0;
  font-size: 12px;
  color: var(--color-text-muted, #6b778c);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.pgh-status {
  padding: 4px 9px;
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface));
  border: 1px solid color-mix(in srgb, var(--color-accent) 18%, var(--color-border));
  border-radius: 8px;
  font-size: 12px;
  font-weight: 800;
  color: var(--color-text-secondary, #42526e);
}

.pgh-right {
  display: flex;
  align-items: center;
}

.create-btn {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 7px 14px;
  font-size: 13px;
  font-weight: 500;
}

/* Horizontal Navigation */
.project-horizontal-nav {
  padding: 0 24px;
  overflow-x: auto;
  white-space: nowrap;
  scrollbar-width: none; /* Firefox */
  -ms-overflow-style: none;  /* IE and Edge */
}
.project-horizontal-nav::-webkit-scrollbar {
  display: none;
}

.nav-links {
  display: flex;
  gap: 4px;
}

.nav-item {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  min-height: 36px;
  padding: 0 12px;
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-secondary, #42526e);
  text-decoration: none;
  position: relative;
  border: 1px solid transparent;
  border-radius: 8px;
  overflow: hidden;
  transition: color 0.15s ease, background 0.15s ease, border-color 0.15s ease;
  cursor: pointer;
}

.nav-item:hover {
  color: var(--color-text-primary, #172b4d);
  background: color-mix(in srgb, var(--color-surface-hover) 80%, transparent);
}

.nav-active,
.nav-active:hover {
  color: var(--color-accent, #0c66e4);
  font-weight: 700;
  background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface));
  border-color: color-mix(in srgb, var(--color-accent) 30%, transparent);
  box-shadow: 0 2px 8px color-mix(in srgb, var(--color-accent) 10%, transparent);
}

.nav-item::after {
  content: '';
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 3.5px;
  background-color: transparent;
  transition: background 0.2s ease, transform 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  transform: scaleX(0);
  transform-origin: center;
}

.nav-active::after,
.nav-active:hover::after {
  background: linear-gradient(90deg, var(--color-accent, #0c66e4), color-mix(in srgb, var(--color-accent, #0c66e4) 80%, #38bdf8));
  transform: scaleX(1);
}

.project-main-content {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  padding: 18px 0 0 0;
  overflow: hidden;
  background: transparent;
  box-sizing: border-box;
  position: static;
}

:deep(.project-page-container) {
  height: 100%;
  min-height: 0;
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

:deep(.project-page-inner) {
  height: 100%;
  min-height: 0;
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  overscroll-behavior: auto;
}

@media (max-width: 768px) {
  .project-main-content {
    padding: 12px;
  }
}

[data-theme='dark'] .project-global-header {
  background: var(--color-surface);
  box-shadow: 0 3px 10px rgba(0, 0, 0, 0.18);
}

[data-theme='dark'] .nav-active {
  color: #e0f2fe;
}

/* Transition */
.fade-fast-enter-active,
.fade-fast-leave-active {
  transition: opacity 0.15s ease;
}
.fade-fast-enter-from,
.fade-fast-leave-to {
  opacity: 0;
}
</style>
