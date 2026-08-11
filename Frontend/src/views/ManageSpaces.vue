<template>
  <section class="manage-spaces-page">
    <header class="page-header">
      <div>
        <span class="eyebrow">WORKSPACE</span>
        <h1>{{ t('Projects') }}</h1>
        <p>{{ t('Quản lý và theo dõi danh sách tất cả các dự án trong workspace của bạn.', 'Manage and track all projects in your workspace.') }}</p>
      </div>

      <button class="primary-action" type="button" @click="isCreateModalVisible = true">
        <i class="fa-solid fa-plus"></i>
        {{ t('Add Project') }}
      </button>
    </header>

    <div class="toolbar">
      <label class="search-field">
        <i class="fa-solid fa-magnifying-glass"></i>
        <input type="search" :placeholder="t('Search spaces...')" v-model="searchQuery" />
      </label>

      <div class="view-toggles">
        <button type="button" :class="{ active: viewMode === 'table' }" @click="setViewMode('table')" title="List view">
          <i class="fa-solid fa-list"></i>
        </button>
        <button type="button" :class="{ active: viewMode === 'grid' }" @click="setViewMode('grid')" title="Grid view">
          <i class="fa-solid fa-grip"></i>
        </button>
      </div>

      <button type="button" @click="toggleSort">
        <i class="fa-solid fa-arrow-down-short-wide"></i> Created date {{ sortDirection === 'desc' ? '↓' : '↑' }}
      </button>

      <div class="project-filter-wrapper">
        <button type="button" @click="showProjectFilters = !showProjectFilters" :class="{ active: showProjectFilters || visibilityFilter !== 'all' }">
          <i class="fa-solid fa-filter"></i> {{ filterLabel }}
        </button>
        <div class="project-filter-menu" v-if="showProjectFilters" @click.stop>
          <div class="filter-title">Visibility</div>
          <label class="filter-option"><input type="radio" value="all" v-model="visibilityFilter" /> All projects</label>
          <label class="filter-option"><input type="radio" value="Public" v-model="visibilityFilter" /> Public</label>
          <label class="filter-option"><input type="radio" value="Private" v-model="visibilityFilter" /> Private</label>
          <label class="filter-option"><input type="radio" value="starred" v-model="visibilityFilter" /> Starred</label>
          <button class="clear-filter-btn" type="button" @click="visibilityFilter = 'all'">Clear filters</button>
        </div>
      </div>

      <span>{{ filteredSpaces.length }} {{ t('dự án', 'projects') }}</span>
    </div>

    <main class="projects-scroll-panel page-content">
      <div v-if="loading" class="loading-state">
         <i class="fa-solid fa-spinner fa-spin"></i> {{ t('Loading projects...') }}
      </div>
      <div v-else-if="filteredSpaces.length === 0" class="empty-state">
         <div class="empty-icon-wrap" style="width: 80px; height: 80px; background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 16px; display: flex; align-items: center; justify-content: center; margin: 0 auto 24px; box-shadow: 0 10px 30px rgba(0,0,0,0.5);">
           <i class="fa-solid fa-folder-open empty-icon" style="margin-bottom: 0;"></i>
         </div>
         <h3 class="empty-title" style="margin: 0 0 8px 0; font-size: 16px; font-weight: 600; color: var(--color-text-primary);">{{ t('No projects found') }}</h3>
         <p style="margin: 0 0 24px 0; font-size: 14px; color: var(--color-text-muted);">It looks like there are no projects here. Let's create your first one!</p>
         <button class="plane-btn-primary" @click="isCreateModalVisible = true">{{ t('Create your first project') }}</button>
      </div>
      <div v-else>
        <div v-if="viewMode === 'grid'" class="spaces-grid">
          <div class="project-card" v-for="(space, index) in filteredSpaces" :key="space.id" @click="goToSpace(space.id)">
            <!-- Cover Image Mock -->
            <div class="card-cover" :style="{ background: projectCover(space) }">
               <div class="card-actions-top" @click.stop>
                 <button
                   v-if="showProjectSettingsButton(space)"
                   class="card-icon-btn appearance-edit-btn"
                   type="button"
                   title="Customize project"
                   aria-label="Customize project"
                   @click="openAppearanceEditor(space)"
                 ><i class="bi bi-pencil"></i></button>
                 <button class="card-icon-btn" type="button" @click="copySpaceLink(space)"><i class="fa-solid fa-link"></i></button>
                 <button
                   class="card-icon-btn"
                   type="button"
                   :disabled="starredStore.isPending(STARRED_ENTITY_TYPES.PROJECT, space.id)"
                   :class="{ starred: space.starred }"
                   :aria-pressed="space.starred"
                   :aria-label="space.starred ? 'Bỏ gắn sao không gian' : 'Gắn sao không gian'"
                   @click="toggleStar(space)"
                 >
                   <i :class="space.starred ? 'fa-solid fa-star' : 'fa-regular fa-star'" aria-hidden="true"></i>
                 </button>
               </div>
            </div>

            <div class="card-body">
              <!-- Floating Project Icon -->
              <ProjectAvatar class="floating-project-avatar" :icon="space.icon" :background="space.cover" size="card" />
              <div class="floating-icon legacy-project-icon" aria-hidden="true">
                <span class="emoji">{{ space.icon || emojiList[index % emojiList.length] || '👇' }}</span>
              </div>

              <div class="proj-title-row">
                 <h3>{{ demoText(space.name) }}</h3>
                 <span class="proj-key">{{ space.key }}</span>
              </div>

              <p class="proj-desc">
                {{ demoText(space.originalRow?.description) || 'Welcome to this project. Explore curated work items, team progress, and reports from one workspace.' }}
              </p>

              <div class="card-footer" @click.stop>
                 <span class="visibility-pill" :class="space.networkType?.toLowerCase()">
                   <i :class="space.networkType === 'Private' ? 'fa-solid fa-lock' : 'fa-solid fa-globe'"></i>
                   {{ space.networkType || 'Public' }}
                 </span>
                 <span style="font-size: 11px; color: var(--color-text-muted); margin-left: auto; margin-right: 8px;">
                   Created: {{ new Date(space.originalRow?.createdAt || space.originalRow?.createdDate || Date.now()).toLocaleDateString() }}
                 </span>
                 <el-dropdown trigger="click" v-if="showProjectSettingsButton(space)" @click.stop>
                   <button class="card-icon-btn" type="button"><i class="fa-solid fa-ellipsis"></i></button>
                   <template #dropdown>
                     <el-dropdown-menu class="plane-dropdown">
                       <el-dropdown-item @click="goToAdmin(space)"><i class="fa-solid fa-gear" style="margin-right: 8px;"></i> Settings</el-dropdown-item>
                       <el-dropdown-item @click="archiveProject(space)"><i class="fa-solid fa-box-archive" style="margin-right: 8px;"></i> Archive project</el-dropdown-item>
                     </el-dropdown-menu>
                   </template>
                 </el-dropdown>
              </div>
            </div>
          </div>
        </div>

        <div v-else class="spaces-table-container">
          <table class="jira-table spaces-table" style="width: 100%; border-collapse: collapse; text-align: left;">
            <thead>
              <tr style="border-bottom: 2px solid var(--color-border); color: var(--color-text-muted); font-size: 12px;">
                <th style="padding: 12px 16px; width: 40px;"></th>
                <th style="padding: 12px 16px;">Name</th>
                <th style="padding: 12px 16px;">Key</th>
                <th style="padding: 12px 16px;">Type</th>
                <th style="padding: 12px 16px;">Lead</th>
                <th style="padding: 12px 16px;">Created</th>
                <th style="padding: 12px 16px; width: 50px;"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(space, index) in filteredSpaces" :key="'table-' + space.id" @click="goToSpace(space.id)" style="border-bottom: 1px solid var(--color-border); cursor: pointer; transition: background 0.2s;" class="table-row-hover">
                <td style="padding: 12px 16px;" @click.stop>
                  <button class="card-icon-btn transparent-btn" type="button" :disabled="starredStore.isPending(STARRED_ENTITY_TYPES.PROJECT, space.id)" :class="{ starred: space.starred }" :aria-pressed="space.starred" :aria-label="space.starred ? 'Bỏ gắn sao không gian' : 'Gắn sao không gian'" @click="toggleStar(space)">
                    <i :class="space.starred ? 'fa-solid fa-star' : 'fa-regular fa-star'" :style="{ color: space.starred ? '#EAB308' : '' }"></i>
                  </button>
                </td>
                <td style="padding: 12px 16px;">
                  <div style="display: flex; align-items: center; gap: 12px;">
                    <ProjectAvatar :icon="space.icon" :background="space.cover" size="xs" />
                    <div class="legacy-project-icon" style="width: 24px; height: 24px; border-radius: 4px; display: flex; align-items: center; justify-content: center; font-size: 12px;" :style="{ background: space.cover || coverGradients[index % coverGradients.length] }">
                      {{ space.icon || emojiList[index % emojiList.length] || '📦' }}
                    </div>
                    <span style="font-weight: 500; color: #3b82f6;">{{ demoText(space.name) }}</span>
                  </div>
                </td>
                <td style="padding: 12px 16px; font-size: 13px;">{{ space.key }}</td>
                <td style="padding: 12px 16px; font-size: 13px; color: var(--color-text-muted);">
                  {{ space.networkType === 'Private' ? 'Team-managed software (Private)' : 'Team-managed software' }}
                </td>
                <td style="padding: 12px 16px;">
                  <div style="display: flex; align-items: center; gap: 8px;">
                    <div style="width: 24px; height: 24px; border-radius: 50%; background: #10B981; color: white; display: flex; align-items: center; justify-content: center; font-size: 11px; font-weight: 600;">
                      {{ space.leadName?.charAt(0).toUpperCase() || 'T' }}
                    </div>
                    <span style="font-size: 13px;">{{ space.leadName }}</span>
                  </div>
                </td>
                <td style="padding: 12px 16px; font-size: 13px; color: var(--color-text-muted);">
                  {{ new Date(space.originalRow?.createdAt || space.originalRow?.createdDate || Date.now()).toLocaleDateString() }}
                </td>
                <td style="padding: 12px 16px;" @click.stop>
                  <button
                    v-if="showProjectSettingsButton(space)"
                    class="card-icon-btn transparent-btn table-appearance-btn"
                    type="button"
                    title="Customize project"
                    aria-label="Customize project"
                    @click="openAppearanceEditor(space)"
                  ><i class="bi bi-pencil"></i></button>
                  <el-dropdown trigger="click" v-if="showProjectSettingsButton(space)">
                    <button class="card-icon-btn transparent-btn" style="background: transparent; border: none; font-size: 16px; color: var(--color-text-muted);"><i class="fa-solid fa-ellipsis"></i></button>
                    <template #dropdown>
                      <el-dropdown-menu class="plane-dropdown">
                        <el-dropdown-item @click="goToAdmin(space)"><i class="fa-solid fa-gear" style="margin-right: 8px;"></i> Settings</el-dropdown-item>
                        <el-dropdown-item @click="archiveProject(space)"><i class="fa-solid fa-box-archive" style="margin-right: 8px;"></i> Archive project</el-dropdown-item>
                      </el-dropdown-menu>
                    </template>
                  </el-dropdown>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </main>

    <CreateSpaceModal v-model:visible="isCreateModalVisible" @created="fetchSpaces" />
    <ProjectAppearanceDialog
      v-model:visible="isAppearanceModalVisible"
      :project="selectedAppearanceProject"
      @saved="handleAppearanceSaved"
    />
  </section>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import CreateSpaceModal from '@/components/CreateSpaceModal.vue'
import ProjectAvatar from '@/components/project/ProjectAvatar.vue'
import ProjectAppearanceDialog from '@/components/project/ProjectAppearanceDialog.vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useProjectStore } from '@/store/useProjectStore'
import { useStarredStore } from '@/store/useStarredStore'
import { STARRED_ENTITY_TYPES } from '@/api/starredRecentApi'
import { canAccessProjectSettings, getProjectSettingsDeniedMessage, getStoredUser } from '@/utils/permissions'
import { subscribeAdminRealtime } from '@/utils/adminRealtime'
import { getProjectSettingsWindowName, openNamedAppWindow, PROJECT_ADMIN_WINDOW_NAME } from '@/utils/windowTabs'
import { useI18n } from '@/composables/useI18n'
import { translateDemoText } from '@/utils/demoContentLocale'
import { getProjectBackgroundStyle } from '@/config/projectAppearance'

const router = useRouter()
const handleSwitchSettings = (path) => {
  router.push(path)
}

const projectStore = useProjectStore()
const starredStore = useStarredStore()
const { language, t } = useI18n()
const loading = ref(false)
const spaces = ref([])
const searchQuery = ref('')
const sortDirection = ref('desc')
const showProjectFilters = ref(false)
const visibilityFilter = ref('all')
const isCreateModalVisible = ref(false)
const isAppearanceModalVisible = ref(false)
const selectedAppearanceProject = ref(null)
const viewMode = ref(localStorage.getItem('spaces_view_mode') || 'table')

const setViewMode = (mode) => {
  viewMode.value = mode
  localStorage.setItem('spaces_view_mode', mode)
}

const currentUser = computed(() => getStoredUser())
const demoText = (value) => translateDemoText(value, language.value)
const canManageSpace = (space) => canAccessProjectSettings(space, currentUser.value)
const showProjectSettingsButton = (space) => canManageSpace(space)

const projectCover = (space) => getProjectBackgroundStyle(space?.cover)

const openAppearanceEditor = (space) => {
  selectedAppearanceProject.value = space
  isAppearanceModalVisible.value = true
}

const handleAppearanceSaved = async (updatedProject) => {
  const index = spaces.value.findIndex(space => space.id === updatedProject.id)
  if (index >= 0) {
    spaces.value.splice(index, 1, { ...spaces.value[index], icon: updatedProject.icon, cover: updatedProject.cover })
  }
  await projectStore.fetchAllProjects(true).catch(() => {})
}

const goToAdmin = (space) => {
  if (!canManageSpace(space)) {
    ElMessage.warning(getProjectSettingsDeniedMessage())
    return
  }
  const routeData = router.resolve(`/space/${space.id}/settings`)
  openNamedAppWindow(routeData.href, getProjectSettingsWindowName(space.id))
}

const archiveProject = async (space) => {
  try {
    await ElMessageBox.confirm(`Are you sure you want to archive project "${space.name}"?`, 'Archive Project', { type: 'warning' })
    await axiosClient.put(`/projects/${space.id}/archive`)
    ElMessage.success('Project archived')
    fetchSpaces()
  } catch (err) {
    if (err !== 'cancel') ElMessage.error('Failed to archive project')
  }
}

const toggleSort = () => {
  sortDirection.value = sortDirection.value === 'desc' ? 'asc' : 'desc'
}

const copySpaceLink = async (space) => {
  const url = `${window.location.origin}/space/${space.id}`
  try {
    await navigator.clipboard.writeText(url)
    ElMessage.success('Project link copied')
  } catch (error) {
    ElMessage.info(url)
  }
}

const toggleStar = async (space) => {
  const nextFavorite = !starredStore.isStarred(STARRED_ENTITY_TYPES.PROJECT, space.id)
  space.starred = nextFavorite
  try {
    await projectStore.updateFavorite(space.id, nextFavorite)
    ElMessage.success(nextFavorite ? 'Project starred' : 'Project unstarred')
  } catch (error) {
    space.starred = !nextFavorite
    ElMessage.error(starredStore.error || t('projects.favoriteFailed'))
  }
}

const coverGradients = [
  'linear-gradient(135deg, #1f0b0f 0%, #761d28 40%, #1e1215 100%)',
  'linear-gradient(135deg, #0f172a 0%, #1e40af 50%, #172554 100%)',
  'linear-gradient(135deg, #064e3b 0%, #059669 40%, #022c22 100%)',
  'linear-gradient(135deg, #4c1d95 0%, #7c3aed 50%, #2e1065 100%)'
]

const emojiList = ['👇', '🚀', '⚡', '💡', '🔥', '🎯']

const fetchSpaces = async () => {
  loading.value = true
  try {
    const [, data] = await Promise.all([
      starredStore.fetchStarredItems({ page: 1, pageSize: 100 }).catch(() => []),
      projectStore.fetchAllProjects(true)
    ])

    // Transform data
    spaces.value = data.map(p => ({
      id: p.id,
      starred: starredStore.isStarred(STARRED_ENTITY_TYPES.PROJECT, p.id),
      name: p.name,
      key: p.key || p.identifier || p.name.substring(0, 4).toUpperCase(),
      myRole: p.myRole || p.MyRole || null,
      projectRole: p.projectRole || p.ProjectRole || null,
      leadName: p.leadName || p.reporterName || 'Admin',
      cover: p.cover || p.Cover,
      icon: p.icon || p.Icon,
      networkType: p.networkType || 'Public',
      originalRow: p
    }))
  } catch (error) {
    console.error('Fetch spaces error:', error)
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchSpaces()
})

let unsubscribeAdminRealtime = null

onMounted(() => {
  unsubscribeAdminRealtime = subscribeAdminRealtime(async ({ type }) => {
    if (
      [
        'project-settings-updated',
        'project-settings-favorite-updated',
        'project-settings-integrations-updated',
        'project-administration-updated',
        'project-settings-deleted'
      ].includes(type)
    ) {
      await fetchSpaces()
      await projectStore.fetchAllProjects(true).catch(() => {})
    }
  })
})

onUnmounted(() => {
  unsubscribeAdminRealtime?.()
})

const filteredSpaces = computed(() => {
  return spaces.value
    .filter(s => {
      const query = searchQuery.value.toLowerCase()
      const matchesSearch = !query || demoText(s.name).toLowerCase().includes(query) || s.key.toLowerCase().includes(query)
      const matchesVisibility =
        visibilityFilter.value === 'all' ||
        (visibilityFilter.value === 'starred' && s.starred) ||
        s.networkType === visibilityFilter.value
      return matchesSearch && matchesVisibility
    })
    .sort((a, b) => {
      const left = new Date(a.originalRow?.createdAt || a.originalRow?.createdDate || 0).getTime()
      const right = new Date(b.originalRow?.createdAt || b.originalRow?.createdDate || 0).getTime()
      return sortDirection.value === 'desc' ? right - left : left - right
    })
})

const goToSpace = (id) => {
  router.push(`/space/${id}/dashboard`)
}

const filterLabel = computed(() => ({
  all: 'Filters',
  Public: 'Public',
  Private: 'Private',
  starred: 'Starred'
}[visibilityFilter.value] || 'Filters'))
</script>

<style scoped>
.manage-spaces-layout {
  display: flex;
  height: 100vh;
  width: 100%;
  background: var(--color-bg);
  overflow: hidden;
}

/* Jira Settings Sidebar */
.jira-admin-sidebar {
  width: 240px;
  border-right: 1px solid var(--color-border);
  background: var(--color-bg);
  padding: 24px 16px;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  font-family: 'Inter', sans-serif;
}

.sidebar-header {
  margin-bottom: 24px;
}

.back-link {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--color-text-muted);
  text-decoration: none;
  font-size: 13px;
  font-weight: 500;
  transition: color 0.2s;
}
.back-link:hover {
  color: var(--color-text-primary);
}

.sidebar-section {
  margin-bottom: 20px;
}

.section-title {
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--color-text-muted);
  margin-bottom: 8px;
  letter-spacing: 0.5px;
}

.section-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border-radius: 6px;
  font-size: 13px;
  color: var(--color-text-primary);
  font-weight: 600;
}
.section-item.active {
  background: #18181b;
  border: 1px solid var(--color-border);
}

.sidebar-menu {
  list-style: none;
  padding: 0 0 0 12px;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
  border-left: 1px solid var(--color-border);
}

.menu-item {
  display: block;
  padding: 6px 12px;
  border-radius: 4px;
  font-size: 13px;
  color: var(--color-text-secondary);
  text-decoration: none;
  transition: all 0.2s;
}
.menu-item:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}
.menu-item.active {
  background: color-mix(in srgb, var(--color-accent) 10%, transparent);
  color: var(--color-accent);
  font-weight: 600;
}

/* Right Content */
.spaces-main-content {
  flex: 1;
  overflow-y: auto;
  min-width: 0;
}

.manage-spaces-page {
  --sa-page-x: 18px;
  min-height: 100%;
  width: 100%;
  background: var(--color-bg);
  color: var(--color-text-primary);
  padding: 0 !important;
  max-width: none !important;
  margin: 0 !important;
  font-family: 'Inter', -apple-system, sans-serif;
}

.page-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 20px;
  padding: 22px var(--sa-page-x, 24px) 18px;
  background: var(--color-surface);
  border-bottom: none !important;
  margin-bottom: 0 !important;
}

.eyebrow {
  color: var(--color-accent);
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.page-header h1 {
  margin: 3px 0 4px;
  font-size: 22px;
  font-weight: 700;
  line-height: 1.2;
  letter-spacing: 0;
  color: var(--color-text-primary);
}

.page-header p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 12px;
}

.primary-action {
  min-height: 36px;
  border: 0;
  border-radius: 7px;
  padding: 0 14px;
  background: var(--color-accent);
  color: #fff;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  transition: opacity 0.15s ease;
}
.primary-action:hover {
  opacity: 0.9;
}

.toolbar {
  min-height: 54px;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px var(--sa-page-x, 24px);
  background: var(--color-surface);
  border-bottom: none !important;
  margin-bottom: 0 !important;
  flex-wrap: wrap;
}

.search-field {
  width: min(320px, 100%);
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 11px;
  border: 1px solid var(--color-border);
  border-radius: 7px;
  color: var(--color-text-muted);
  background: var(--color-bg);
}

.search-field input {
  width: 100%;
  height: 34px;
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--color-text-primary);
  font-size: 12px;
}

.view-toggles {
  display: flex;
  gap: 4px;
}

.toolbar button {
  min-height: 34px;
  border: 1px solid var(--color-border);
  border-radius: 7px;
  padding: 0 11px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 500;
  transition: all 0.15s ease;
}

.toolbar button:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.toolbar button.active {
  border-color: var(--color-accent);
  color: var(--color-accent);
  background: color-mix(in srgb, var(--color-accent) 10%, transparent);
}

.toolbar > span {
  margin-left: auto;
  color: var(--color-text-muted);
  font-size: 11px;
}

.project-filter-wrapper { position: relative; }
.project-filter-menu {
  position: absolute;
  top: calc(100% + 8px);
  right: 0;
  z-index: 20;
  width: 220px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 12px;
  box-shadow: var(--shadow-lg);
}
.filter-title { color: var(--color-text-muted); font-size: 12px; font-weight: 600; margin-bottom: 8px; }
.filter-option {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--color-text-secondary);
  font-size: 13px;
  padding: 6px 0;
  cursor: pointer;
}
.filter-option:hover {
  color: var(--color-text-primary);
}
.clear-filter-btn {
  width: 100%;
  margin-top: 8px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
  padding: 7px;
  cursor: pointer;
  font-size: 12px;
  font-weight: 500;
}
.clear-filter-btn:hover { background: var(--color-border); }

.page-content {
  padding: 18px var(--sa-page-x, 24px) 28px;
}

.projects-scroll-panel {
  min-height: 0;
  overflow-y: auto;
  padding: 2px 8px 24px 2px;
  scrollbar-width: thin;
  scrollbar-color: #3f3f46 transparent;
}

.projects-scroll-panel::-webkit-scrollbar {
  width: 8px;
}

.projects-scroll-panel::-webkit-scrollbar-thumb {
  background: #3f3f46;
  border-radius: 999px;
}

.spaces-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 20px;
  align-items: stretch;
}

/* Card */
.project-card {
  background: rgba(255, 255, 255, 0.88);
  border: 1px solid rgba(148, 163, 184, 0.24);
  border-radius: 18px;
  overflow: hidden;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  flex-direction: column;
  min-height: 300px;
  box-shadow: 0 18px 44px rgba(15, 23, 42, 0.08);
}
.project-card:hover {
  border-color: rgba(14, 165, 233, 0.42);
  transform: translateY(-2px);
  box-shadow: 0 26px 62px rgba(15, 23, 42, 0.12);
}

.card-cover {
  height: 128px;
  position: relative;
  display: flex;
  justify-content: flex-end;
  padding: 12px;
}

.card-actions-top {
  display: flex;
  gap: 7px;
}

.appearance-edit-btn {
  color: #0f172a;
}

.table-appearance-btn {
  display: inline-flex;
  margin-right: 4px;
  color: var(--color-text-muted) !important;
}

.card-icon-btn {
  appearance: none;
  -webkit-appearance: none;
  width: 32px;
  height: 32px;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.9);
  border: 1px solid rgba(255, 255, 255, 0.72);
  color: #172b4d;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  font-size: 14px;
  transition: transform 0.18s ease, background 0.18s ease, box-shadow 0.18s ease, color 0.18s ease;
  backdrop-filter: blur(8px);
  box-shadow: 0 5px 14px rgba(15, 23, 42, 0.14);
  touch-action: manipulation;
}
.card-icon-btn:hover {
  background: #ffffff;
  color: #0c66e4;
  transform: translateY(-1px);
  box-shadow: 0 8px 18px rgba(15, 23, 42, 0.2);
}
.card-icon-btn.starred { color: #EAB308; }
.card-icon-btn:disabled { opacity: 0.45; cursor: not-allowed; }
.card-icon-btn:focus-visible {
  outline: 3px solid color-mix(in srgb, var(--color-accent, #0c66e4) 48%, transparent);
  outline-offset: 2px;
}
.card-icon-btn i { display: block; width: 1em; line-height: 1; text-align: center; }
.card-icon-btn.transparent-btn { background: transparent; border-color: transparent; color: var(--color-text-muted); }

.card-body {
  padding: 0 20px 20px 20px;
  position: relative;
  flex: 1;
  display: flex;
  flex-direction: column;
}

.floating-icon {
  width: 42px;
  height: 42px;
  border-radius: 14px;
  background: var(--color-border);
  border: 4px solid var(--color-surface);
  display: flex;
  align-items: center;
  justify-content: center;
  margin-top: -18px;
  margin-bottom: 12px;
  font-size: 18px;
}

.legacy-project-icon {
  display: none !important;
}

.floating-project-avatar {
  width: 52px !important;
  min-width: 52px !important;
  max-width: 52px !important;
  height: 52px !important;
  min-height: 52px !important;
  max-height: 52px !important;
  flex-basis: 52px !important;
  padding: 12px !important;
  font-size: 24px !important;
  margin-top: -22px;
  margin-bottom: 12px;
  border: 2px solid #ffffff;
  color: #ffffff !important;
  box-shadow: 0 9px 22px rgba(15, 23, 42, 0.2);
}

.floating-project-avatar :deep(i) {
  color: #ffffff !important;
}

.proj-title-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}
.proj-title-row h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 900;
  color: #0f172a;
}
.proj-key {
  font-size: 11px;
  color: var(--color-text-muted);
  font-weight: 600;
  margin-top: 2px;
}

.proj-desc {
  font-size: 13px;
  color: #64748b;
  line-height: 1.5;
  margin: 0 0 20px 0;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  text-overflow: ellipsis;
  flex: 1;
}

.card-footer {
  display: flex;
  justify-content: flex-start;
  align-items: center;
  gap: 10px;
  margin-top: auto;
}

.visibility-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 24px;
  padding: 0 8px;
  border-radius: 6px;
  background: var(--color-surface-hover);
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  font-size: 11px;
  font-weight: 600;
}

.visibility-pill.private {
  color: var(--color-danger);
  background: var(--color-danger-bg);
  border-color: rgba(239, 68, 68, 0.2);
}

.avatar-group {
  display: flex;
}
.avatar {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: #10B981;
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 600;
  border: 2px solid var(--color-surface);
}

.card-footer .card-icon-btn {
  background: transparent;
  border: none;
  font-size: 14px;
  margin-left: auto;
}
.card-footer .card-icon-btn:hover { background: var(--color-border); }

.loading-state, .empty-state { text-align: center; margin-top: 60px; color: var(--color-text-muted); }
.empty-icon { font-size: 48px; color: #3F3F46; margin-bottom: 16px; }
.empty-state p { margin-bottom: 24px; }

@media (max-width: 900px) {
  .manage-spaces-page {
    padding: 0 !important;
  }

  .spaces-header {
    align-items: flex-start;
    flex-direction: column;
  }

  .sh-right {
    width: 100%;
    justify-content: flex-start;
  }

  .search-box input,
  .search-box input:focus {
    width: 180px;
  }
}

.table-row-hover:hover {
  background: var(--color-surface-hover) !important;
}

.spaces-table-container {
  overflow-x: auto;
  border-radius: 18px;
  border: 1px solid rgba(148, 163, 184, 0.22);
  background: rgba(255, 255, 255, 0.86);
  box-shadow: 0 18px 44px rgba(15, 23, 42, 0.08);
}

[data-theme='dark'] .manage-spaces-page {
  background:
    linear-gradient(180deg, #07111f, #0f172a 54%, #101827);
}

[data-theme='dark'] .spaces-header,
[data-theme='dark'] .project-card,
[data-theme='dark'] .spaces-table-container {
  border-color: rgba(148, 163, 184, 0.18);
  background: rgba(15, 23, 42, 0.78);
  box-shadow: 0 18px 44px rgba(0, 0, 0, 0.24);
}

[data-theme='dark'] .sh-left h1,
[data-theme='dark'] .proj-title-row h3 {
  color: #f8fafc;
}

[data-theme='dark'] .search-box input,
[data-theme='dark'] .plane-btn-secondary.outline-btn {
  border-color: rgba(148, 163, 184, 0.22);
  background: rgba(15, 23, 42, 0.82);
  color: #e2e8f0;
}

[data-theme='dark'] .proj-desc {
  color: #94a3b8;
}

.switch-trigger-btn {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  cursor: pointer;
  background: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
  color: var(--color-text-primary) !important;
  padding: 8px 12px;
  border-radius: 4px;
  font-size: 13px;
  font-weight: 600;
  transition: all 0.2s;
}

.switch-trigger-btn:hover {
  background: var(--color-surface-hover) !important;
}

.switch-trigger-btn i {
  color: var(--color-text-secondary) !important;
}

.switch-trigger-btn span {
  color: var(--color-text-primary) !important;
}

/* Compact density */
.manage-spaces-page {
  width: 100% !important;
  min-height: 100% !important;
  padding: 0 !important;
  margin: 0 !important;
}

.spaces-toolbar {
  border-radius: 10px !important;
  padding: 14px 16px !important;
  gap: 12px !important;
}

.sh-left h1 {
  font-size: clamp(22px, 2vw, 30px) !important;
  line-height: 1.12 !important;
}

.search-box input {
  height: 32px !important;
  border-radius: 8px !important;
  padding: 7px 10px 7px 32px !important;
  font-size: 12.5px !important;
}

.plane-btn-secondary,
.plane-btn-primary,
.view-toggle button {
  min-height: 32px !important;
  border-radius: 8px !important;
  padding: 6px 10px !important;
  font-size: 12.5px !important;
}

.projects-grid {
  grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)) !important;
  gap: 14px !important;
}

.project-card {
  min-height: 250px !important;
  border-radius: 10px !important;
}

.project-cover {
  height: 112px !important;
}

.project-avatar {
  width: 36px !important;
  height: 36px !important;
  border-radius: 10px !important;
}

.project-card-body {
  padding: 0 16px 16px !important;
}

.proj-title-row h3 {
  font-size: 15px !important;
  line-height: 1.25 !important;
}

.proj-desc {
  font-size: 12.5px !important;
  line-height: 1.45 !important;
}

@media (max-width: 760px) {
  .manage-spaces-page {
    padding: 12px !important;
  }

  .spaces-toolbar {
    align-items: stretch !important;
    flex-direction: column !important;
    padding: 12px !important;
  }

  .projects-grid {
    grid-template-columns: 1fr !important;
  }
}
</style>

<style>
.el-popper.jira-switch-dropdown-popper {
  background: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
  padding: 4px 0 !important;
  z-index: 100002 !important;
  box-shadow: 0 10px 30px rgba(0,0,0,0.2) !important;
}

.jira-switch-dropdown-menu .el-dropdown-menu__item {
  color: var(--color-text-primary) !important;
  display: flex !important;
  align-items: center !important;
  gap: 8px !important;
  font-size: 13px !important;
  padding: 8px 16px !important;
}

.jira-switch-dropdown-menu .el-dropdown-menu__item:hover {
  background-color: var(--color-surface-hover) !important;
  color: var(--color-text-primary) !important;
}

.jira-switch-dropdown-menu .el-dropdown-menu__item.is-disabled {
  color: var(--color-accent) !important;
  font-weight: 600 !important;
  background: transparent !important;
  cursor: default !important;
}
</style>
