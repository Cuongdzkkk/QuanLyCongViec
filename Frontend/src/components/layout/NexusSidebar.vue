<template>
  <aside class="plane-sidebar" :class="{ 'collapsed': !isVisible }">
    <div class="sidebar-scrollable">
      <div class="sidebar-top-action">
        <button class="new-work-btn" @click="triggerCreateTask">
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
          <router-link to="/your-work" class="nav-link">
            <i class="fa-regular fa-user"></i>
            <span>{{ t('Your work') }}</span>
          </router-link>
        </li>
        <li class="nav-item">
          <router-link to="/priority" class="nav-link">
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
          <router-link to="/checkin" class="nav-link">
            <i class="fa-solid fa-calendar-check" style="color: #10b981;"></i>
            <span>{{ t('Daily Check-in') || 'Check-in ngày' }}</span>
          </router-link>
        </li>
        <li class="nav-item">
          <router-link to="/integrations" class="nav-link">
            <i class="fa-solid fa-plug-circle-bolt"></i>
            <span>{{ t('Integration Hub') }}</span>
          </router-link>
        </li>
        <li class="nav-item">
          <router-link to="/stickies" class="nav-link">
            <i class="fa-solid fa-note-sticky"></i>
            <span>{{ t('Stickies') }}</span>
          </router-link>
        </li>
        <li class="nav-item">
          <router-link to="/rewards" class="nav-link">
            <i class="fa-solid fa-trophy"></i>
            <span>{{ t('Rewards') }}</span>
          </router-link>
        </li>
      </ul>

      <!-- Workspace Division -->
      <div class="nav-section-title">{{ t('Workspace') }}</div>
      <ul class="nav-menu">
        <li class="nav-item">
          <router-link to="/spaces" class="nav-link">
            <i class="fa-solid fa-briefcase"></i>
            <span>{{ t('Projects') }}</span>
          </router-link>
        </li>
        <li class="nav-item">
          <div class="nav-link" :class="{ 'dropdown-active': showMorePanel }" @click="showMorePanel = !showMorePanel">
            <i class="fa-solid fa-ellipsis"></i>
            <span>{{ showMorePanel ? t('Hide') : t('More') }}</span>
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
      <div class="nav-section-title flex-between">
        {{ t('Projects') }}
        <i class="fa-solid fa-chevron-down" style="font-size: 10px;"></i>
      </div>
      <ul class="nav-menu">
        <template v-for="project in projectTree" :key="project.id">
          <li class="nav-item">
            <router-link
              :to="`/space/${project.id}/dashboard`"
              class="nav-link proj-folder"
              :class="{ active: currentProjectId === project.id }"
            >
              <ProjectAvatar :icon="project.icon" :background="project.cover" size="xs" />
              <span class="truncate">{{ demoText(project.name) }}</span>
            </router-link>
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
import { useSprintStore } from '@/store/useSprintStore'
import { useProjectStore } from '@/store/useProjectStore'
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
const projectStore = useProjectStore()

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
const currentProjectId = computed(() => {
  if (route.path.startsWith('/space') && route.params.id) return route.params.id
  return getScopedCurrentProjectId() || 'default'
})


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

const toggleProject = (projectId) => {
  if (currentProjectId.value !== projectId) {
    router.push(`/space/${projectId}`)
  }
  projectStore.toggleProject(projectId)
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
  position: absolute;
  top: 0;
  left: 250px;
  width: 250px;
  height: 100vh;
  background-color: var(--sa-sidebar);
  border-right: 1px solid var(--color-border);
  padding: 16px 12px;
  z-index: 998;
  box-shadow: var(--shadow-xl);
}

.pin-icon { margin-left: auto; font-size: 11px; color: var(--color-text-muted); opacity: 0; }
.nav-link:hover .pin-icon { opacity: 1; }

.proj-folder {
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
}

.proj-icon {
  width: 20px; height: 20px; border-radius: 6px;
  display: flex; align-items: center; justify-content: center;
  font-size: 10px; font-weight: 800; color: #fff; margin-right: 8px;
  box-shadow: 0 6px 14px rgb(15 23 42 / 0.12);
}

.sub-item .nav-link {
  padding-left: 28px;
  min-height: 30px;
  font-size: 12px;
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

.sidebar-scrollable::-webkit-scrollbar { width: 4px; }
.sidebar-scrollable::-webkit-scrollbar-thumb { background: transparent; border-radius: 10px; }
.sidebar-scrollable:hover::-webkit-scrollbar-thumb { background: var(--color-border); }

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
