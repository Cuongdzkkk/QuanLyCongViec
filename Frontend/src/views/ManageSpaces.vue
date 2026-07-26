<template>
  <div class="manage-spaces-page sp-page-shell">
      <!-- Plane Style Header -->
          <header class="spaces-header">
            <div class="sh-left">
              <i class="fa-solid fa-briefcase"></i>
              <h1>{{ t('projects.title') }}</h1>
            </div>

        <div class="sh-right">
          <div class="search-box">
             <i class="fa-solid fa-magnifying-glass"></i>
             <input type="text" :placeholder="t('projects.searchPlaceholder')" v-model="searchQuery" />
          </div>
          <div style="display: flex; gap: 4px; border: 1px solid var(--color-border); padding: 4px; border-radius: 8px;">
            <button class="plane-btn-secondary outline-btn" style="border: none; margin: 0; padding: 6px 10px;" :class="{ active: viewMode === 'table' }" type="button" @click="setViewMode('table')" title="List view">
              <i class="fa-solid fa-list"></i>
            </button>
            <button class="plane-btn-secondary outline-btn" style="border: none; margin: 0; padding: 6px 10px;" :class="{ active: viewMode === 'grid' }" type="button" @click="setViewMode('grid')" title="Grid view">
              <i class="fa-solid fa-grip"></i>
            </button>
          </div>
          <button class="plane-btn-secondary outline-btn" type="button" @click="toggleSort">
             <i class="fa-solid fa-arrow-down-short-wide"></i> {{ t('projects.createdDate') }} {{ sortDirection === 'desc' ? '↓' : '↑' }}
          </button>
          <div class="project-filter-wrapper">
            <button class="plane-btn-secondary outline-btn" type="button" @click="showProjectFilters = !showProjectFilters" :class="{ active: showProjectFilters || visibilityFilter !== 'all' }">
               <i class="fa-solid fa-filter"></i> {{ filterLabel }}
            </button>
            <div class="project-filter-menu" v-if="showProjectFilters" @click.stop>
              <div class="filter-title">{{ t('projects.visibility') }}</div>
              <label class="filter-option"><input type="radio" value="all" v-model="visibilityFilter" /> {{ t('projects.allProjects') }}</label>
              <label class="filter-option"><input type="radio" value="Public" v-model="visibilityFilter" /> {{ t('projects.filterPublic') }}</label>
              <label class="filter-option"><input type="radio" value="Private" v-model="visibilityFilter" /> {{ t('projects.filterPrivate') }}</label>
              <label class="filter-option"><input type="radio" value="starred" v-model="visibilityFilter" /> {{ t('projects.filterStarred') }}</label>
              <button class="clear-filter-btn" type="button" @click="visibilityFilter = 'all'">{{ t('projects.clearFilters') }}</button>
            </div>
          </div>
          <button class="plane-btn-primary" @click="isCreateModalVisible = true">
            {{ t('projects.addProject') }}
          </button>
        </div>
      </header>

      <section class="projects-scroll-panel">
      <div v-if="loading" class="loading-state">
         <i class="fa-solid fa-spinner fa-spin"></i> {{ t('projects.loading') }}
      </div>
      <div v-else-if="filteredSpaces.length === 0" class="empty-state">
         <div class="empty-icon-wrap" style="width: 80px; height: 80px; background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 16px; display: flex; align-items: center; justify-content: center; margin: 0 auto 24px; box-shadow: 0 10px 30px rgba(0,0,0,0.5);">
           <i class="fa-solid fa-folder-open empty-icon" style="margin-bottom: 0;"></i>
         </div>
         <h3 class="empty-title" style="margin: 0 0 8px 0; font-size: 16px; font-weight: 600; color: var(--color-text-primary);">{{ t('projects.noProjectsFound') }}</h3>
         <p style="margin: 0 0 24px 0; font-size: 14px; color: var(--color-text-muted);">{{ t('projects.noProjectsDesc') }}</p>
         <button class="plane-btn-primary" @click="isCreateModalVisible = true">{{ t('projects.createFirst') }}</button>
      </div>
      <div v-else>
        <div v-if="viewMode === 'grid'" class="spaces-grid">
          <div class="project-card" v-for="(space, index) in filteredSpaces" :key="space.id" @click="goToSpace(space.id)">
            <div
              class="card-cover"
              :style="projectCoverStyle(space, index)"
              :role="space.cover ? 'img' : undefined"
              :aria-label="space.cover ? (space.coverAltText || `${space.name} cover`) : undefined"
            >
               <div class="card-actions-top" @click.stop>
                 <button class="card-icon-btn" type="button" @click="copySpaceLink(space)"><i class="fa-solid fa-link"></i></button>
                 <button class="card-icon-btn" type="button" :disabled="starredStore.isPending(STARRED_ENTITY_TYPES.PROJECT, space.id)" :class="{ 'starred': space.starred }" @click="toggleStar(space)"><i :class="space.starred ? 'fa-solid fa-star' : 'fa-regular fa-star'"></i></button>
               </div>
            </div>

            <div class="card-body">
              <!-- Floating Project Icon -->
              <div class="floating-icon">
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
                   {{ space.networkType === 'Private' ? t('projects.private') : t('projects.public') }}
                 </span>
                 <span style="font-size: 11px; color: var(--color-text-muted); margin-left: auto; margin-right: 8px;">
                   {{ t('projects.createdLabel') }} {{ new Date(space.originalRow?.createdAt || space.originalRow?.createdDate || Date.now()).toLocaleDateString() }}
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
                <th style="padding: 12px 16px;">{{ t('projects.colName') }}</th>
                <th style="padding: 12px 16px;">{{ t('projects.colKey') }}</th>
                <th style="padding: 12px 16px;">{{ t('projects.colType') }}</th>
                <th style="padding: 12px 16px;">{{ t('projects.colLead') }}</th>
                <th style="padding: 12px 16px;">{{ t('projects.colCreated') }}</th>
                <th style="padding: 12px 16px; width: 50px;"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(space, index) in filteredSpaces" :key="'table-' + space.id" @click="goToSpace(space.id)" style="border-bottom: 1px solid var(--color-border); cursor: pointer; transition: background 0.2s;" class="table-row-hover">
                <td style="padding: 12px 16px;" @click.stop>
                  <button class="card-icon-btn transparent-btn" type="button" :disabled="starredStore.isPending(STARRED_ENTITY_TYPES.PROJECT, space.id)" style="background: transparent; border: none; color: var(--color-text-muted);" :class="{ 'starred': space.starred }" @click="toggleStar(space)">
                    <i :class="space.starred ? 'fa-solid fa-star' : 'fa-regular fa-star'" :style="{ color: space.starred ? '#EAB308' : '' }"></i>
                  </button>
                </td>
                <td style="padding: 12px 16px;">
                  <div style="display: flex; align-items: center; gap: 12px;">
                    <div
                      style="width: 24px; height: 24px; border-radius: 4px; display: flex; align-items: center; justify-content: center; font-size: 12px; background-position: center; background-size: cover;"
                      :style="projectCoverStyle(space, index)"
                      :role="space.cover ? 'img' : undefined"
                      :aria-label="space.cover ? (space.coverAltText || `${space.name} cover`) : undefined"
                    >
                      {{ space.icon || emojiList[index % emojiList.length] || '📦' }}
                    </div>
                    <span style="font-weight: 500; color: #3b82f6;">{{ demoText(space.name) }}</span>
                  </div>
                </td>
                <td style="padding: 12px 16px; font-size: 13px;">{{ space.key }}</td>
                <td style="padding: 12px 16px; font-size: 13px; color: var(--color-text-muted);">
                  {{ space.networkType === 'Private' ? t('projects.teamManagedPrivate') : t('projects.teamManaged') }}
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
                  <el-dropdown trigger="click" v-if="showProjectSettingsButton(space)">
                    <button class="card-icon-btn transparent-btn" style="background: transparent; border: none; font-size: 16px; color: var(--color-text-muted);"><i class="fa-solid fa-ellipsis"></i></button>
                    <template #dropdown>
                      <el-dropdown-menu class="plane-dropdown">
                        <el-dropdown-item @click="goToAdmin(space)"><i class="fa-solid fa-gear" style="margin-right: 8px;"></i> {{ t('projects.settings') }}</el-dropdown-item>
                        <el-dropdown-item @click="archiveProject(space)"><i class="fa-solid fa-box-archive" style="margin-right: 8px;"></i> {{ t('projects.archiveProject') }}</el-dropdown-item>
                      </el-dropdown-menu>
                    </template>
                  </el-dropdown>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
      </section>

      <CreateSpaceModal v-model:visible="isCreateModalVisible" @created="fetchSpaces" />
    </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import CreateSpaceModal from '@/components/CreateSpaceModal.vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useProjectStore } from '@/store/useProjectStore'
import { useStarredStore } from '@/store/useStarredStore'
import { STARRED_ENTITY_TYPES } from '@/api/starredRecentApi'
import { canAccessProjectSettings, getProjectSettingsDeniedMessage, getStoredUser } from '@/utils/permissions'
import { subscribeAdminRealtime } from '@/utils/adminRealtime'
import { getProjectSettingsWindowName, openNamedAppWindow, PROJECT_ADMIN_WINDOW_NAME } from '@/utils/windowTabs'
import { useI18n } from '@/composables/useI18n'
import { translateDemoText } from '@/utils/demoContentLocale'

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
const viewMode = ref(localStorage.getItem('spaces_view_mode') || 'table')

const setViewMode = (mode) => {
  viewMode.value = mode
  localStorage.setItem('spaces_view_mode', mode)
}

const currentUser = computed(() => getStoredUser())
const demoText = (value) => translateDemoText(value, language.value)
const canManageSpace = (space) => canAccessProjectSettings(space, currentUser.value)
const showProjectSettingsButton = (space) => canManageSpace(space)

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
    await ElMessageBox.confirm(t('projects.archiveConfirm').replace('{name}', space.name), t('projects.archiveTitle'), { type: 'warning' })
    await axiosClient.put(`/projects/${space.id}/archive`)
    ElMessage.success(t('projects.archiveSuccess'))
    fetchSpaces()
  } catch (err) {
    if (err !== 'cancel') ElMessage.error(t('projects.archiveFailed'))
  }
}

const toggleSort = () => {
  sortDirection.value = sortDirection.value === 'desc' ? 'asc' : 'desc'
}

const copySpaceLink = async (space) => {
  const url = `${window.location.origin}/space/${space.id}`
  try {
    await navigator.clipboard.writeText(url)
    ElMessage.success(t('projects.linkCopied'))
  } catch (error) {
    ElMessage.info(url)
  }
}

const toggleStar = async (space) => {
  const nextFavorite = !starredStore.isStarred(STARRED_ENTITY_TYPES.PROJECT, space.id)
  space.starred = nextFavorite
  try {
    await projectStore.updateFavorite(space.id, nextFavorite)
    ElMessage.success(nextFavorite ? t('projects.projectStarred') : t('projects.projectUnstarred'))
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

const projectCoverStyle = (space, index) => space.cover
  ? {
      backgroundImage: `url("${space.cover}")`,
      backgroundPosition: 'center',
      backgroundSize: 'cover'
    }
  : { background: coverGradients[index % coverGradients.length] }

const fetchSpaces = async () => {
  loading.value = true
  try {
    const [, data] = await Promise.all([
      starredStore.fetchStarredItems({ page: 1, pageSize: 100 }).catch(() => []),
      projectStore.fetchAllProjects(true)
    ])

    spaces.value = data.map(p => ({
      id: p.id,
      starred: starredStore.isStarred(STARRED_ENTITY_TYPES.PROJECT, p.id),
      name: p.name,
      key: p.key || p.name.substring(0, 4).toUpperCase(),
      myRole: p.myRole || null,
      projectRole: p.originalRow?.projectRole || p.originalRow?.ProjectRole || null,
      leadName: p.leadName || 'Admin',
      cover: p.cover,
      coverAltText: p.coverAltText,
      icon: p.icon,
      networkType: p.networkType || 'Public',
      originalRow: p.originalRow || p
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
  all: t('projects.filters'),
  Public: t('projects.filterPublic'),
  Private: t('projects.filterPrivate'),
  starred: t('projects.filterStarred')
}[visibilityFilter.value] || t('projects.filters')))
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
  padding: 34px clamp(20px, 4vw, 48px) 48px !important;
  width: 100%;
  max-width: 1320px;
  margin: 0 auto;
  color: var(--color-text-primary);
  font-family: 'Inter', -apple-system, sans-serif;
  min-height: 100vh;
  background:
    radial-gradient(circle at 14% 0%, rgba(56, 189, 248, 0.12), transparent 32%),
    radial-gradient(circle at 88% 0%, rgba(34, 197, 94, 0.10), transparent 28%),
    linear-gradient(180deg, #f8fbff, #eef5fb 54%, #f8fafc);
}

/* Header */
.spaces-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 24px;
  margin-bottom: 26px;
  flex-shrink: 0;
  padding: 18px 20px;
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 18px;
  background: rgba(255, 255, 255, 0.78);
  box-shadow: 0 18px 46px rgba(15, 23, 42, 0.07);
  backdrop-filter: blur(14px);
}

.sh-left {
  display: flex;
  align-items: center;
  gap: 12px;
}
.sh-left i {
  color: var(--color-text-muted);
  font-size: 18px;
}
.sh-left h1 {
  font-size: 24px;
  font-weight: 900;
  margin: 0;
  color: #0f172a;
  letter-spacing: 0;
}

.sh-right {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
  justify-content: flex-end;
}

.search-box {
  position: relative;
  display: flex;
  align-items: center;
}
.search-box i {
  position: absolute;
  left: 12px;
  color: var(--color-text-muted);
  font-size: 13px;
}
.search-box input {
  background: #ffffff;
  border: 1px solid rgba(148, 163, 184, 0.24);
  border-radius: 12px;
  color: #0f172a;
  padding: 9px 12px 9px 34px;
  font-size: 13px;
  font-weight: 700;
  outline: none;
  width: 180px;
  transition: width 0.2s;
}
.search-box input:focus { width: 240px; }
.search-box input::placeholder { color: var(--color-text-disabled); }

.plane-btn-secondary.outline-btn {
  background: rgba(255, 255, 255, 0.86);
  border: 1px solid rgba(148, 163, 184, 0.24);
  color: #334155;
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  border-radius: 12px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}
.plane-btn-secondary.outline-btn:hover { background: var(--color-surface-hover); color: var(--color-text-primary); }
.plane-btn-secondary.outline-btn.active { background: rgba(56, 189, 248, 0.14); color: #0284c7; border-color: rgba(14, 165, 233, 0.34); }

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

.plane-btn-primary {
  background: linear-gradient(135deg, #38bdf8, #2563eb);
  color: white;
  border: none;
  border-radius: 12px;
  padding: 9px 16px;
  font-size: 13px;
  font-weight: 900;
  cursor: pointer;
  transition: background 0.2s;
  box-shadow: 0 14px 30px rgba(37, 99, 235, 0.22);
}
.plane-btn-primary:hover { background: #0284C7; }

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
  gap: 8px;
}

.card-icon-btn {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  background: rgba(0,0,0,0.3);
  border: 1px solid rgba(255,255,255,0.1);
  color: var(--color-text-muted);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  font-size: 12px;
  transition: all 0.2s;
  backdrop-filter: blur(4px);
}
.card-icon-btn:hover { background: rgba(0,0,0,0.5); color: var(--color-text-primary); }
.card-icon-btn.starred { color: #EAB308; }
.card-icon-btn:disabled { opacity: 0.45; cursor: not-allowed; }

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
    padding: 24px;
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
    radial-gradient(circle at 14% 0%, rgba(56, 189, 248, 0.13), transparent 32%),
    radial-gradient(circle at 88% 0%, rgba(34, 197, 94, 0.08), transparent 28%),
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
  padding: 18px var(--sa-page-x, 24px) 30px !important;
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
