<template>
  <aside class="plane-sidebar" :class="{ 'collapsed': !isVisible }">
    <div class="sidebar-scrollable">
      <div class="sidebar-top-action">
        <button class="new-work-btn" type="button" @click="triggerCreateTask">
          <i class="fa-solid fa-pen-to-square"></i>
          <span>{{ t('New work item') }}</span>
        </button>
      </div>

      <ul class="nav-menu">
        <li class="nav-item">
          <router-link to="/dashboard" class="nav-link" :class="{ active: $route.path === '/dashboard' && !$route.query.tab }" exact>
            <i class="fa-solid fa-house"></i>
            <span>{{ t('For you') }}</span>
          </router-link>
        </li>
        <li class="nav-item">
          <el-popover
            v-model:visible="recentVisible"
            placement="right-start"
            :width="320"
            trigger="click"
            popper-class="sidebar-quick-popover"
            popper-style="padding: 0;"
            :teleported="true"
            @show="onRecentShow"
          >
            <template #reference>
              <div class="nav-link" :class="{ active: $route.path === '/dashboard' && $route.query.tab === 'viewed' }" style="cursor: pointer;">
                <i class="fa-solid fa-clock-rotate-left"></i>
                <span>{{ t('Recent') }}</span>
                <i class="fa-solid fa-chevron-right" style="font-size:10px; margin-left:auto;"></i>
              </div>
            </template>
            <RecentDropdown ref="recentDropdownRef" @close="closeRecentPopover" />
          </el-popover>
        </li>
        <li class="nav-item">
          <el-popover
            v-model:visible="starredVisible"
            placement="right-start"
            :width="340"
            trigger="click"
            popper-class="sidebar-quick-popover"
            popper-style="padding: 0;"
            :teleported="true"
            @show="onStarredShow"
          >
            <template #reference>
              <div class="nav-link" :class="{ active: $route.path === '/dashboard' && $route.query.tab === 'starred' }" style="cursor: pointer;">
                <i class="fa-regular fa-star"></i>
                <span>{{ t('Starred') }}</span>
                <i class="fa-solid fa-chevron-right" style="font-size:10px; margin-left:auto;"></i>
              </div>
            </template>
            <StarredDropdown ref="starredDropdownRef" @close="closeStarredPopover" />
          </el-popover>
        </li>
        <li class="nav-item">
          <router-link to="/your-work" class="nav-link" :class="{ active: $route.path === '/your-work' }">
            <i class="fa-regular fa-user"></i>
            <span>{{ t('Your work') }}</span>
          </router-link>
        </li>
        <li class="nav-item">
          <router-link to="/priority" class="nav-link" :class="{ active: $route.path === '/priority' }">
            <i class="fa-solid fa-fire" style="color: #f97316;"></i>
            <span>{{ t('Daily Focus') }}</span>
          </router-link>
        </li>
        <li class="nav-item">
          <router-link to="/chat" class="nav-link" :class="{ active: $route.path === '/chat' }">
            <i class="fa-solid fa-comments" style="color: #3b82f6;"></i>
            <span>{{ t('Discussion Channel') }}</span>
          </router-link>
        </li>
        <li class="nav-item">
          <router-link to="/checkin" class="nav-link" :class="{ active: $route.path === '/checkin' }">
            <i class="fa-solid fa-calendar-check" style="color: #10b981;"></i>
            <span>{{ t('Daily Check-in') || 'Check-in ngày' }}</span>
          </router-link>
        </li>
        <li class="nav-item">
          <router-link to="/integrations" class="nav-link" :class="{ active: $route.path === '/integrations' }">
            <i class="fa-solid fa-plug-circle-bolt"></i>
            <span>{{ t('Integration Hub') }}</span>
          </router-link>
        </li>
        <li class="nav-item">
          <router-link to="/stickies" class="nav-link" :class="{ active: $route.path === '/stickies' }">
            <i class="fa-solid fa-note-sticky"></i>
            <span>{{ t('Stickies') }}</span>
          </router-link>
        </li>
        <li class="nav-item">
          <router-link to="/rewards" class="nav-link" :class="{ active: $route.path === '/rewards' }">
            <i class="fa-solid fa-trophy"></i>
            <span>{{ t('Rewards') }}</span>
          </router-link>
        </li>
      </ul>

      <!-- Workspace Division -->
      <div class="nav-section-title">{{ t('Workspace') }}</div>
      <ul class="nav-menu">
        <li class="nav-item">
          <div
            class="nav-link workspace-project-link"
            role="link"
            tabindex="0"
            @click="router.push('/spaces')"
            @keydown.enter="router.push('/spaces')"
            @keydown.space.prevent="router.push('/spaces')"
          >
            <i class="fa-solid fa-briefcase"></i>
            <span class="workspace-project-label">{{ t('Projects') }}</span>
            <button
              type="button"
              class="workspace-more-button"
              :class="{ 'dropdown-active': showMorePanel }"
              :aria-expanded="showMorePanel"
              :aria-label="t('More')"
              @click.stop="showMorePanel = !showMorePanel"
            >
              <i class="fa-solid fa-ellipsis"></i>
            </button>
          </div>
        </li>
      </ul>

      <!-- Secondary Panel for More -->
      <transition name="slide-left">
        <div class="more-panel" v-if="showMorePanel">
          <ul class="nav-menu">
            <li class="nav-item sub-item">
              <router-link to="/views" class="nav-link">
                <i class="fa-solid fa-layer-group"></i>
                <span>{{ t('Views') }}</span>
                <i class="fa-solid fa-thumbtack pin-icon"></i>
              </router-link>
            </li>
            <li class="nav-item sub-item">
              <router-link to="/analytics" class="nav-link">
                <i class="fa-solid fa-chart-simple"></i>
                <span>{{ t('Analytics') }}</span>
                <i class="fa-solid fa-thumbtack pin-icon"></i>
              </router-link>
            </li>
            <li class="nav-item sub-item">
              <router-link to="/archives" class="nav-link">
                <i class="fa-solid fa-box-archive"></i>
                <span>{{ t('Archives') }}</span>
                <i class="fa-solid fa-thumbtack pin-icon"></i>
              </router-link>
            </li>
          </ul>
        </div>
      </transition>

      <!-- Projects Division -->
      <button
        type="button"
        class="nav-section-title flex-between projects-section-toggle"
        :aria-expanded="showProjects"
        @click="showProjects = !showProjects"
      >
        <span class="projects-toggle-chevron" aria-hidden="true">
          <i class="fa-solid" :class="showProjects ? 'fa-chevron-down' : 'fa-chevron-right'"></i>
        </span>
        <span class="projects-count" :aria-label="`${projectTree.length} projects`">{{ projectTree.length }}</span>
      </button>
      <ul class="nav-menu" v-show="showProjects">
        <template v-for="project in projectTree" :key="project.id">
          <li class="nav-item">
            <button
              type="button"
              class="nav-link proj-folder"
              :class="{ active: isProjectContext && currentProjectId === project.id }"
              @click="openProject(project.id)"
            >
              <ProjectAvatar :icon="project.icon" :background="project.cover" size="xs" />
              <span class="truncate">{{ demoText(project.name) }}</span>
            </button>
          </li>
        </template>
      </ul>
    </div>

    <!-- Bottom Actions -->
    <div class="sidebar-bottom">
      <div
        class="user-status-card"
        role="button"
        tabindex="0"
        aria-label="Cập nhật trạng thái"
        @click="statusModalOpen = true"
        @keydown.enter="statusModalOpen = true"
        @keydown.space.prevent="statusModalOpen = true"
      >
        <span class="status-card-icon" aria-hidden="true">
          <i class="bi bi-laptop"></i>
        </span>
        <span class="status-card-copy">
          <span class="status-card-title">{{ userStatusText || 'Đang làm việc' }}</span>
          <span class="status-card-subtitle">Active now</span>
        </span>
        <span class="status-card-badge" aria-label="Đang hoạt động"></span>
      </div>
    </div>

    <!-- Status Modal Dialog -->
    <StatusUpdateModal 
      v-model="statusModalOpen"
      :initial-emoji="userEmoji"
      :initial-text="userStatusText"
      @save="onStatusSave"
      @clear="onStatusClear"
    />
  </aside>
</template>

<script setup>
import { computed, ref, defineProps, defineEmits, watch, onMounted, onUnmounted, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'
import { useSprintStore } from '@/store/useSprintStore'
import { useProjectStore } from '@/store/useProjectStore'
import { useAuthStore } from '@/store/useAuthStore'
import { subscribeAdminRealtime } from '@/utils/adminRealtime'
import { getScopedCurrentProjectId, setScopedCurrentProjectId } from '@/utils/projectContext'
import { useI18n } from '@/composables/useI18n'
import { translateDemoText } from '@/utils/demoContentLocale'
import RecentDropdown from '@/components/RecentDropdown.vue'
import StarredDropdown from '@/components/StarredDropdown.vue'
import StatusUpdateModal from '@/components/collaboration/StatusUpdateModal.vue'
import ProjectAvatar from '@/components/project/ProjectAvatar.vue'

const route = useRoute()
const router = useRouter()
const { t, language } = useI18n()
const demoText = (value) => translateDemoText(value, language.value)
const showMorePanel = ref(false)
const showProjects = ref(true)
const projectStore = useProjectStore()
const authStore = useAuthStore()
const pendingProjectId = ref(null)

// User status state
const statusModalOpen = ref(false)
const userEmoji = ref('💻')
const userStatusText = ref('Đang làm việc')

const onStatusSave = (status) => {
  userEmoji.value = status.emoji
  userStatusText.value = status.text
}

const onStatusClear = () => {
  userEmoji.value = ''
  userStatusText.value = ''
}

// Popover control variables
const recentVisible = ref(false)
const starredVisible = ref(false)
const recentDropdownRef = ref(null)
const starredDropdownRef = ref(null)

const onRecentShow = () => {
  recentDropdownRef.value?.loadRecentItems()
}
const onStarredShow = () => {
  starredDropdownRef.value?.loadStarredItems()
}
const closeRecentPopover = () => {
  recentVisible.value = false
}
const closeStarredPopover = () => {
  starredVisible.value = false
}
const isProjectContext = computed(() => Boolean(
  route.path.startsWith('/space/') && route.params.id
))

const currentProjectId = computed(() => (
  isProjectContext.value ? String(route.params.id) : null
))


const props = defineProps({
  isVisible: { type: Boolean, default: true }
})
const emit = defineEmits(['close-mobile'])

const sprintStore = useSprintStore()
const projectTree = computed(() => projectStore.projectTree)
const favoriteProjects = computed(() => projectStore.favoriteProjects)
const favoriteSprints = computed(() => {
   if (!sprintStore.sprints) return [];
   return sprintStore.sprints.filter(s => s.isFavorite);
})

watch(() => [route.path, route.params.id], async ([path, newVal], previous = []) => {
   const oldVal = previous[1]
   const isProjectRoute = path.startsWith('/space') && newVal
   if (!isProjectRoute) {
      sprintStore.resetScope()
      return
   }

   if (newVal && newVal !== 'default') {
      if (newVal !== oldVal) {
        projectStore.expandProject(newVal)
      }
      setScopedCurrentProjectId(newVal)
      sprintStore.fetchSprints(newVal)
      await projectStore.fetchProjectDetails(newVal)
   }
}, { immediate: true })

onMounted(() => {
  projectStore.fetchAllProjects(true).catch(() => {})
})

let unsubscribeAdminRealtime = null

onMounted(() => {
  unsubscribeAdminRealtime = subscribeAdminRealtime(async ({ type, payload }) => {
    const activeProjectId = route.path.startsWith('/space') && route.params.id
      ? route.params.id
      : getScopedCurrentProjectId() || null
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

onUnmounted(() => {
  unsubscribeAdminRealtime?.()
})

const normalizeIdentity = value => String(value ?? '').trim().toLowerCase()

const currentUserIsProjectMember = members => {
  const user = authStore.currentUser || {}
  const userId = normalizeIdentity(user.id ?? user.userId ?? user.Id ?? user.UserId)
  const email = normalizeIdentity(user.email ?? user.Email)
  if (!userId && !email) return false

  return (Array.isArray(members) ? members : []).some(member => {
    const memberId = normalizeIdentity(
      member?.userId
      ?? member?.UserId
      ?? member?.id
      ?? member?.Id
      ?? member?.user?.id
      ?? member?.user?.userId
    )
    const memberEmail = normalizeIdentity(
      member?.email
      ?? member?.Email
      ?? member?.user?.email
      ?? member?.user?.Email
    )
    return Boolean(
      (userId && memberId === userId)
      || (email && memberEmail === email)
    )
  })
}

const openProject = async projectId => {
  if (pendingProjectId.value) return

  if (isProjectContext.value && `${currentProjectId.value}` === `${projectId}`) {
    if (route.path !== `/space/${projectId}/work-items`) {
      await router.push(`/space/${projectId}/work-items`)
      return
    }
    projectStore.toggleProject(projectId)
    return
  }

  pendingProjectId.value = projectId
  try {
    const response = await axiosClient.get(`/projects/${projectId}/members`, { timeout: 5000 })
    const members = response.data?.data || []
    if (!currentUserIsProjectMember(members)) {
      ElMessage.closeAll()
      ElMessage.error(t(
        'You cannot access this project because you are not a member.',
        'Bạn không thể truy cập dự án này vì bạn không có trong danh sách thành viên.'
      ))
      return
    }

    projectStore.toggleProject(projectId)
    await router.push(`/space/${projectId}`)
  } catch (error) {
    ElMessage.closeAll()
    ElMessage.error(error.response?.status === 403
      ? t('You cannot access this project.', 'Bạn không có quyền truy cập dự án này.')
      : t('Unable to check project access.', 'Không thể kiểm tra quyền truy cập dự án.'))
  } finally {
    pendingProjectId.value = null
  }
}

const projectIcon = (project) => project.icon || project.name?.charAt(0)?.toUpperCase() || 'P'
const projectColor = (project) => {
  const colors = ['#579dff', '#c97cf4', '#00b8d9', '#22a06b', '#f5cd47']
  return colors[project.name?.length % colors.length] || '#579dff'
}

const triggerCreateTask = async () => {
  const projects = projectStore.allProjects.length
    ? projectStore.allProjects
    : await projectStore.fetchAllProjects()

  if (!projects.length) {
    ElMessage.warning('Create a project before creating a work item.')
    await router.push('/spaces')
    return
  }

  const preferredProjectId = projects.some(p => p.id === currentProjectId.value)
    ? currentProjectId.value
    : projects[0].id

  if (route.path !== `/space/${preferredProjectId}`) {
    await router.push(`/space/${preferredProjectId}`)
    await nextTick()
    window.setTimeout(() => {
      window.dispatchEvent(new CustomEvent('global-create-task'))
    }, 120)
    return
  }
  window.dispatchEvent(new CustomEvent('global-create-task'))
}
</script>

<style scoped>
.plane-sidebar {
  width: var(--sa-sidebar-width, 224px);
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--sa-sidebar) 88%, #ffffff 12%), var(--sa-sidebar)),
    var(--sa-sidebar);
  border-right: 1px solid var(--color-border);
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  z-index: 999;
  height: 100%;
  position: relative;
  box-shadow: inset -1px 0 0 rgba(255, 255, 255, 0.55);
}

.plane-sidebar.collapsed { width: 0; border-right: none; overflow: hidden; }

.sidebar-scrollable { flex: 1; overflow-y: auto; padding: 12px 10px; }

.sidebar-top-action { margin-bottom: 12px; }

.new-work-btn {
  width: 100%;
  background: var(--sa-surface);
  color: var(--color-text-primary);
  border: 1px solid var(--color-border);
  border-radius: var(--sa-radius-md);
  min-height: 36px;
  padding: 8px 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  box-shadow: var(--sa-shadow-sm);
}

.new-work-btn:hover {
  background: color-mix(in srgb, var(--sa-primary-soft) 44%, var(--sa-surface));
  border-color: color-mix(in srgb, var(--sa-primary) 38%, var(--sa-border));
  color: var(--sa-text);
}

.nav-section-title {
  font-size: 11px;
  color: color-mix(in srgb, var(--sa-text-muted) 82%, var(--sa-text));
  text-transform: uppercase;
  font-weight: 800;
  letter-spacing: 0.075em;
  margin: 16px 8px 7px;
}

.flex-between { display: flex; justify-content: space-between; align-items: center; padding-right: 4px; }

.nav-menu { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 2px; }

.nav-link {
  display: flex;
  align-items: center;
  min-height: 36px;
  padding: 8px 10px;
  color: var(--color-text-secondary);
  font-size: 14px;
  font-weight: 500;
  border-radius: 8px;
  text-decoration: none;
  cursor: pointer;
  transition: all 0.2s;
  border: 1px solid transparent;
}

.nav-link i:first-child {
  width: 16px;
  font-size: 13px;
  margin-right: 12px;
  text-align: center;
  color: color-mix(in srgb, var(--sa-primary) 42%, var(--color-text-secondary));
}

.nav-link:hover {
  background-color: color-mix(in srgb, var(--sa-surface-soft) 80%, var(--sa-surface));
  color: var(--color-text-primary);
  border-color: color-mix(in srgb, var(--sa-border) 70%, transparent);
}

.nav-link.active {
  background:
    linear-gradient(90deg, color-mix(in srgb, var(--sa-primary-soft) 82%, var(--sa-surface)), color-mix(in srgb, var(--sa-primary-soft) 42%, var(--sa-surface)));
  color: color-mix(in srgb, var(--sa-primary) 78%, #0f172a);
  border-color: color-mix(in srgb, var(--sa-primary) 24%, var(--sa-border));
  font-weight: 800;
  box-shadow: inset 3px 0 0 var(--sa-primary);
}

.nav-link.active i:first-child,
.nav-link:hover i:first-child {
  color: var(--sa-primary);
}

.fav-icon { color: #f59e0b; }

.more-panel {
  position: relative;
  width: 100%;
  height: auto;
  margin-top: 2px;
  padding: 4px 0 8px;
  background: transparent;
  border-right: none;
  z-index: 1;
  box-shadow: none;
}

.pin-icon { margin-left: auto; font-size: 11px; color: var(--color-text-muted); opacity: 0; }
.nav-link:hover .pin-icon { opacity: 1; }

.proj-folder {
  width: 100%;
  border: 1px solid transparent;
  background: transparent;
  font: inherit;
  text-align: left;
  color: var(--color-text-primary);
  margin-bottom: 2px;
  gap: 10px;
  padding-left: 12px;
}

.proj-folder :deep(.project-avatar) {
  flex: 0 0 28px;
  color: #ffffff !important;
}

.proj-folder :deep(.project-avatar > i) {
  color: #ffffff !important;
}

.proj-folder.active {
  background:
    linear-gradient(90deg, color-mix(in srgb, var(--sa-primary-soft) 86%, var(--sa-surface)), color-mix(in srgb, var(--sa-primary-soft) 48%, var(--sa-surface)));
  border-color: color-mix(in srgb, var(--sa-primary) 28%, var(--sa-border));
  color: color-mix(in srgb, var(--sa-primary) 78%, #0f172a);
  font-weight: 800;
  box-shadow: inset 3px 0 0 var(--sa-primary);
}

.proj-icon {
  width: 20px; height: 20px; border-radius: 6px;
  display: flex; align-items: center; justify-content: center;
  font-size: 10px; font-weight: 800; color: #fff; margin-right: 8px;
  box-shadow: 0 6px 14px rgb(15 23 42 / 0.12);
}

.sub-item .nav-link {
  padding-left: 32px;
  min-height: 30px;
  font-size: 12px;
}

.workspace-project-link {
  gap: 0;
}

.workspace-project-label {
  flex: 1;
  color: inherit;
  text-decoration: none;
}

.workspace-more-button {
  width: 28px;
  height: 28px;
  margin-left: auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 0;
  border-radius: 6px;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
}

.workspace-more-button:hover,
.workspace-more-button.dropdown-active {
  background: color-mix(in srgb, var(--sa-primary) 10%, transparent);
  color: var(--sa-primary);
}

.workspace-more-button i {
  margin: 0 !important;
  font-size: 12px !important;
}

.projects-section-toggle {
  width: fit-content;
  min-width: 44px;
  min-height: 22px;
  margin: 0 8px 4px;
  padding: 2px 8px;
  gap: 4px;
  border: 0;
  background: transparent;
  color: inherit;
  cursor: pointer;
  text-align: left;
}

.projects-toggle-chevron {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 14px;
  color: var(--color-text-muted);
}

.projects-toggle-chevron i {
  font-size: 10px;
}

.projects-count {
  margin-left: 0;
  min-width: 20px;
  padding: 2px 6px;
  border-radius: 999px;
  background: var(--color-surface-hover);
  color: var(--color-text-muted);
  font-size: 11px;
  font-weight: 700;
  line-height: 1.2;
  text-align: center;
}

.workspace-more-icon {
  margin-left: auto;
  margin-right: 0;
  font-size: 12px !important;
  color: var(--color-text-muted) !important;
}

.workspace-more-link {
  min-height: 32px;
  color: var(--color-text-muted);
}

.workspace-more-link i:first-child {
  color: var(--color-text-muted);
}

.sidebar-bottom {
  flex-shrink: 0;
  padding: 12px 16px 16px;
  border-top: 1px solid var(--color-border);
  background: color-mix(in srgb, var(--sa-sidebar) 84%, var(--sa-surface));
}

.user-status-card {
  width: 100%;
  min-height: 58px;
  padding: 12px;
  display: flex;
  align-items: center;
  gap: 10px;
  box-sizing: border-box;
  border: 0;
  border-radius: 12px;
  outline: none;
  background: color-mix(in srgb, var(--sa-primary) 7%, var(--sa-surface));
  cursor: pointer;
  transition: background-color 220ms ease, box-shadow 220ms ease;
}

.user-status-card:hover {
  background: color-mix(in srgb, var(--sa-primary) 10%, var(--sa-surface));
  box-shadow: 0 6px 16px rgb(15 23 42 / 0.06);
}

.user-status-card:focus-visible {
  outline: none;
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--sa-primary) 22%, transparent);
}

.status-card-icon {
  width: 32px;
  height: 32px;
  flex: 0 0 32px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: color-mix(in srgb, var(--sa-primary) 14%, var(--sa-surface));
  color: var(--sa-primary);
}

.status-card-icon .bi {
  font-size: 19px;
  line-height: 1;
}

.status-card-copy {
  min-width: 0;
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.status-card-title,
.status-card-subtitle {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.status-card-title {
  color: var(--color-text-primary);
  font-size: 13px;
  font-weight: 600;
  line-height: 18px;
}

.status-card-subtitle {
  color: var(--color-text-muted);
  font-size: 12px;
  line-height: 16px;
}

.status-card-badge {
  width: 8px;
  height: 8px;
  flex: 0 0 8px;
  border-radius: 50%;
  background: #22c55e;
  box-shadow: 0 0 0 3px rgb(34 197 94 / 0.14);
}

.community-link {
  display: flex; align-items: center; gap: 8px;
  color: var(--color-text-secondary); font-size: 12.5px; text-decoration: none;
  padding: 6px 8px; border-radius: 8px; transition: all 0.2s;
}

.community-link:hover { background: var(--color-surface-hover); color: var(--color-text-primary); }

[data-theme='dark'] .plane-sidebar {
  box-shadow: inset -1px 0 0 rgba(255, 255, 255, 0.06);
}

[data-theme='dark'] .nav-link {
  color: #b8c7db;
}

[data-theme='dark'] .nav-link i:first-child {
  color: #8fc8f5;
}

[data-theme='dark'] .nav-link.active,
[data-theme='dark'] .proj-folder.active {
  color: #7dd3fc;
  background:
    linear-gradient(90deg, rgba(56, 189, 248, 0.18), rgba(56, 189, 248, 0.08));
  border-color: rgba(56, 189, 248, 0.32);
}

[data-theme='light'] .nav-link {
  color: #334155;
}

[data-theme='light'] .nav-link i:first-child {
  color: #3b7196;
}

.ms-auto { margin-left: auto; }

.slide-left-enter-active, .slide-left-leave-active { transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1); }
.slide-left-enter-from, .slide-left-leave-to { transform: translateX(-100%); opacity: 0; }

.sidebar-scrollable {
  scrollbar-width: none;
  -ms-overflow-style: none;
}

.sidebar-scrollable::-webkit-scrollbar {
  width: 0;
  height: 0;
  display: none;
}

@media (max-width: 768px) {
  .plane-sidebar {
    width: min(82vw, 250px);
  }

  .sidebar-scrollable {
    padding: 10px 8px;
  }

  .nav-link {
    min-height: 30px;
    font-size: 12px;
  }
}
</style>
