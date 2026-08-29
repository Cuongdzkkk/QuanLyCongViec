<template>
  <section class="manage-spaces-page">
    <header class="page-header app-shell-page-header">
      <div class="app-shell-title-wrap">
        <span class="eyebrow">WORKSPACE</span>
        <h1>{{ t('Projects') }}</h1>
        <div class="app-shell-header-help">
          <span class="app-shell-header-help-btn" aria-label="About Projects">
            <i class="fa-solid fa-question"></i>
          </span>
          <div class="app-shell-header-help-popover" role="tooltip">
            <span>WORKSPACE</span>
            <p>{{ t('Quản lý và theo dõi danh sách tất cả các dự án trong workspace của bạn.', 'Manage and track all projects in your workspace.') }}</p>
          </div>
        </div>
      </div>

      <button class="primary-action" type="button" @click="isCreateModalVisible = true">
        <i class="fa-solid fa-plus"></i>
        {{ t('Add Project') }}
      </button>
    </header>

    <div class="sprinta-layout-toolbar">
      <ProjectPageToolbar
        v-model:searchQuery="searchQuery"
        show-search
        :search-placeholder="t('Search spaces...')"
      >
        <template #filters>
          <div class="filter-dropdown-wrapper js-toolbar-popup-scope" style="position: relative;">
            <button
              class="timeline-filter-trigger icon-only-trigger"
              type="button"
              :aria-label="t('Filters')"
              :title="t('Filters')"
              @click="toggleFilterDropdown"
              :class="{ active: showFilterDropdown || activeFilters.length }"
            >
              <i class="fa-solid fa-filter"></i>
              <span v-if="activeFilters.length" class="filter-count">{{ activeFilters.length }}</span>
            </button>
            <div class="plane-dropdown-menu filter-dropdown-menu" v-show="showFilterDropdown" @click.stop>
              <FilterBar
                v-model:filters="activeFilters"
                :fields="projectFilterFields"
                :operators="projectOperators"
                :custom-value-meta="customProjectValueMeta"
                :active="showFilterDropdown"
              />
            </div>
          </div>
        </template>

        <template #toggles>
          <div class="view-toggles">
            <button type="button" :class="{ active: viewMode === 'table' }" @click="setViewMode('table')" :title="t('Danh sách', 'List view')">
              <i class="fa-solid fa-list"></i>
            </button>
            <button type="button" :class="{ active: viewMode === 'grid' }" @click="setViewMode('grid')" :title="t('Lưới', 'Grid view')">
              <i class="fa-solid fa-grip"></i>
            </button>
          </div>
        </template>

        <template #sort>
          <ToolbarSortMenu
            v-model="projectSortBy"
            v-model:direction="projectSortDirection"
            :label="t('Sắp xếp theo', 'Sort by')"
            :options="projectSortOptions"
          />
        </template>

        <template #right>
          <span style="color: var(--color-text-muted); font-size: 11px; margin-left: 8px;">
            {{ filteredSpaces.length }} {{ t('dự án', 'projects') }}
          </span>
        </template>
      </ProjectPageToolbar>
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
          <div class="project-card" v-for="(space, index) in filteredSpaces" :key="space.id" @click="goToSpace(space)">
            <!-- Cover Image Mock -->
            <div class="card-cover" :style="{ background: projectCover(space) }">
               <div class="card-actions-top" @click.stop>
                 
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
                 <div class="project-meta-row">
                   <span class="project-meta-item visibility-pill" :class="getSpaceVisibilityLabel(space).toLowerCase()">
                     <i :class="getSpaceVisibilityLabel(space) === 'Private' ? 'fa-solid fa-lock' : 'fa-solid fa-globe'"></i>
                     {{ getSpaceVisibilityLabel(space) }}
                   </span>
                   <span class="project-meta-item date-meta">
                     <i class="fa-regular fa-calendar"></i>
                     {{ formatSpaceCreatedDate(space) }}
                   </span>
                   <span class="project-meta-item member-meta">
                     <i class="fa-solid fa-users"></i>
                     {{ getSpaceMemberCountLabel(space) }}
                   </span>
                 </div>
                 <el-dropdown trigger="click" v-if="showProjectSettingsButton(space)" @click.stop>
                   <button class="card-icon-btn" type="button"><i class="fa-solid fa-ellipsis"></i></button>
                   <template #dropdown>
                     <el-dropdown-menu class="plane-dropdown">
                       <el-dropdown-item @click="openAppearanceEditor(space)"><i class="bi bi-pencil" style="margin-right: 8px;"></i> Cập nhật giao diện</el-dropdown-item>
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
          <table v-resizable class="jira-table spaces-table">
            <thead>
              <tr>
                <th style="width: 40px;"></th>
                <th><i class="fa-solid fa-folder"></i> Name</th>
                <th><i class="fa-solid fa-key"></i> Key</th>
                <th><i class="fa-solid fa-shapes"></i> Type</th>
                <th><i class="fa-solid fa-user-tie"></i> Lead</th>
                <th><i class="fa-regular fa-calendar"></i> Created</th>
                <th style="width: 50px;"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(space, index) in filteredSpaces" :key="'table-' + space.id" @click="goToSpace(space)">
                <td @click.stop>
                  <button class="card-icon-btn transparent-btn" type="button" :disabled="starredStore.isPending(STARRED_ENTITY_TYPES.PROJECT, space.id)" :class="{ starred: space.starred }" :aria-pressed="space.starred" :aria-label="space.starred ? 'Bỏ gắn sao không gian' : 'Gắn sao không gian'" @click="toggleStar(space)">
                    <i :class="space.starred ? 'fa-solid fa-star' : 'fa-regular fa-star'" :style="{ color: space.starred ? '#EAB308' : '' }"></i>
                  </button>
                </td>
                <td>
                  <div style="display: flex; align-items: center; gap: 12px;">
                    <ProjectAvatar :icon="space.icon" :background="space.cover" size="xs" />
                    <div class="legacy-project-icon" style="width: 24px; height: 24px; border-radius: 4px; display: flex; align-items: center; justify-content: center; font-size: 12px;" :style="{ background: space.cover || coverGradients[index % coverGradients.length] }">
                      {{ space.icon || emojiList[index % emojiList.length] || '📦' }}
                    </div>
                    <span class="project-name-title">{{ demoText(space.name) }}</span>
                  </div>
                </td>
                <td style="font-size: 13px;">{{ space.key }}</td>
                <td style="font-size: 13px; color: var(--color-text-muted);">
                  {{ space.networkType === 'Private' ? 'Team-managed software (Private)' : 'Team-managed software' }}
                </td>
                <td>
                  <div style="display: flex; align-items: center; gap: 8px;">
                    <div style="width: 24px; height: 24px; border-radius: 50%; background: #10B981; color: white; display: flex; align-items: center; justify-content: center; font-size: 11px; font-weight: 600;">
                      {{ space.leadName?.charAt(0).toUpperCase() || 'T' }}
                    </div>
                    <span style="font-size: 13px;">{{ space.leadName }}</span>
                  </div>
                </td>
                <td style="font-size: 13px; color: var(--color-text-muted);">
                  {{ new Date(space.originalRow?.createdAt || space.originalRow?.createdDate || Date.now()).toLocaleDateString() }}
                </td>
                <td @click.stop>

                  <el-dropdown trigger="click" v-if="showProjectSettingsButton(space)">
                    <button class="card-icon-btn transparent-btn" style="background: transparent; border: none; font-size: 16px; color: var(--color-text-muted);"><i class="fa-solid fa-ellipsis"></i></button>
                    <template #dropdown>
                      <el-dropdown-menu class="plane-dropdown">
                        <el-dropdown-item @click="openAppearanceEditor(space)"><i class="bi bi-pencil" style="margin-right: 8px;"></i> Cập nhật giao diện</el-dropdown-item>
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

    <CreateSpaceModal v-model:visible="isCreateModalVisible" @created="handleProjectCreated" />
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
import { buildSpacePath } from '@/utils/spaceRoute'
import { getProjectBackgroundStyle } from '@/config/projectAppearance'
import ProjectPageToolbar from '@/components/common/ProjectPageToolbar.vue'
import ToolbarSortMenu from '@/components/common/ToolbarSortMenu.vue'
import FilterBar from '@/components/FilterBar.vue'

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
const activeFilters = ref([])
const projectSortBy = ref('createdAt')
const projectSortDirection = ref('desc')
const showFilterDropdown = ref(false)

const toggleFilterDropdown = () => {
  showFilterDropdown.value = !showFilterDropdown.value
}

const projectSortOptions = computed(() => [
  { value: 'createdAt', label: t('Ngày tạo', 'Created date') },
  { value: 'name', label: t('Tên dự án', 'Project name') },
  { value: 'key', label: t('Mã dự án', 'Project key') },
  { value: 'memberCount', label: t('Số thành viên', 'Members count') }
])

const leadOptions = computed(() => {
  const leads = new Set()
  spaces.value.forEach(s => {
    if (s.leadName) leads.add(s.leadName)
  })
  return Array.from(leads)
})

const projectFilterFields = computed(() => [
  { key: 'networkType', label: t('Quyền riêng tư', 'Visibility'), icon: 'fa-solid fa-lock', values: ['Public', 'Private'] },
  { key: 'starred', label: t('Gắn sao', 'Starred'), icon: 'fa-regular fa-star', values: [t('Có', 'Yes'), t('Không', 'No')] },
  { key: 'leadName', label: t('Người dẫn dắt', 'Lead'), icon: 'fa-regular fa-user', values: leadOptions.value }
])

const projectOperators = {
  networkType: ['is', 'is not'],
  starred: ['is', 'is not'],
  leadName: ['is', 'is not']
}

const customProjectValueMeta = (fieldKey, value) => {
  if (fieldKey === 'networkType') {
    return { icon: value === 'Private' ? 'fa-solid fa-lock' : 'fa-solid fa-globe', color: '#3b82f6' }
  }
  if (fieldKey === 'starred') {
    return { icon: 'fa-solid fa-star', color: '#eab308' }
  }
  if (fieldKey === 'leadName') {
    return { icon: 'fa-regular fa-user', color: '#10b981' }
  }
  return null
}

const handleDocumentClick = (e) => {
  const container = document.querySelector('.js-toolbar-popup-scope')
  if (container && !container.contains(e.target)) {
    showFilterDropdown.value = false
  }
}
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
  const routeData = router.resolve(buildSpacePath(space, 'settings'))
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



const copySpaceLink = async (space) => {
  const url = `${window.location.origin}${buildSpacePath(space, 'work-items')}`
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

const getSpaceCreatedValue = (space) =>
  space?.originalRow?.createdAt ||
  space?.originalRow?.CreatedAt ||
  space?.originalRow?.createdDate ||
  space?.originalRow?.CreatedDate ||
  space?.createdAt ||
  space?.CreatedAt ||
  null

const formatSpaceCreatedDate = (space) => {
  const value = getSpaceCreatedValue(space)
  if (!value) return '--'

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '--'

  return date.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

const getSpaceVisibilityLabel = (space) => {
  const value = `${space?.networkType || ''}`.trim().toLowerCase()
  return value === 'private' || value === 'privated' ? 'Private' : 'Public'
}

const getSpaceMemberCount = (space) => {
  const row = space?.originalRow || {}
  const directValue =
    space?.memberCount ??
    space?.MemberCount ??
    space?.activeMemberCount ??
    space?.ActiveMemberCount ??
    row.memberCount ??
    row.MemberCount ??
    row.activeMemberCount ??
    row.ActiveMemberCount ??
    row.totalMembers ??
    row.TotalMembers

  if (Number.isFinite(Number(directValue))) {
    return Number(directValue)
  }

  const members = space?.members || space?.Members || row.members || row.Members
  return Array.isArray(members) ? members.length : 0
}

const getSpaceMemberCountLabel = (space) => {
  let count = Number(getSpaceMemberCount(space))
  if (!Number.isFinite(count) || count === 0) count = 1
  return `${count} members`
}

const mapProjectToSpace = (p) => {
  const id = p.id || p.Id || p.projectId || p.ProjectId
  const name = p.name || p.Name || ''

  return {
    id,
    starred: starredStore.isStarred(STARRED_ENTITY_TYPES.PROJECT, id),
    name,
    key: p.key || p.Key || p.identifier || p.Identifier || name.substring(0, 4).toUpperCase() || 'PRJ',
    myRole: p.myRole || p.MyRole || null,
    projectRole: p.projectRole || p.ProjectRole || null,
    leadName: p.leadName || p.LeadName || p.reporterName || p.ReporterName || p.creatorName || p.CreatorName || 'Admin',
    cover: p.cover || p.Cover,
    icon: p.icon || p.Icon,
    networkType: p.networkType || p.NetworkType || 'Public',
    activeMemberCount: p.activeMemberCount || p.ActiveMemberCount || 0,
    memberCount: p.activeMemberCount || p.ActiveMemberCount || p.memberCount || p.MemberCount || p.totalMembers || p.TotalMembers || (Array.isArray(p.members || p.Members) ? (p.members || p.Members).length : 0),
    createdAt: p.createdAt || p.CreatedAt || p.createdDate || p.CreatedDate || null,
    originalRow: p
  }
}

const upsertSpace = (project) => {
  const mapped = mapProjectToSpace(project)
  if (!mapped.id) return

  const existingIndex = spaces.value.findIndex(space => `${space.id}` === `${mapped.id}`)
  if (existingIndex >= 0) {
    spaces.value.splice(existingIndex, 1, { ...spaces.value[existingIndex], ...mapped })
  } else {
    spaces.value.unshift(mapped)
  }
}

const handleProjectCreated = async (createdProject) => {
  if (createdProject) {
    const mappedProject = projectStore.applyProjectUpdate?.(createdProject) || createdProject
    upsertSpace(mappedProject)
    await router.push(buildSpacePath(mappedProject, 'work-items'))
    return
  }

  await fetchSpaces({ preserveExisting: true })
}

const fetchSpaces = async (options = {}) => {
  const previousSpaces = options.preserveExisting ? [...spaces.value] : []
  loading.value = true
  try {
    const [, data] = await Promise.all([
      starredStore.fetchStarredItems({ page: 1, pageSize: 100 }).catch(() => []),
      projectStore.fetchAllProjects(true)
    ])

    const nextSpaces = data.map(mapProjectToSpace).filter(space => space.id)
    spaces.value = options.preserveExisting
      ? [...previousSpaces, ...nextSpaces].reduce((items, space) => {
          if (!space.id || items.some(item => `${item.id}` === `${space.id}`)) return items
          items.push(space)
          return items
        }, [])
      : nextSpaces
  } catch (error) {
    console.error('Fetch spaces error:', error)
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchSpaces()
  document.addEventListener('click', handleDocumentClick)
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
  document.removeEventListener('click', handleDocumentClick)
})

const filteredSpaces = computed(() => {
  let result = spaces.value.filter(s => {
    // 1. Search Query
    const query = searchQuery.value.toLowerCase()
    if (query && !demoText(s.name).toLowerCase().includes(query) && !s.key.toLowerCase().includes(query)) {
      return false
    }

    // 2. Active Filters
    for (const filter of activeFilters.value) {
      const field = filter.field
      const op = filter.operator
      const val = filter.value

      let itemValue = s[field]
      if (field === 'starred') {
        const isStarred = starredStore.isStarred(STARRED_ENTITY_TYPES.PROJECT, s.id)
        itemValue = isStarred ? t('Có', 'Yes') : t('Không', 'No')
      }

      const isMatch = String(itemValue || '').toLowerCase() === String(val || '').toLowerCase()

      if (op === 'is' && !isMatch) return false
      if (op === 'is not' && isMatch) return false
    }

    return true
  })

  // 3. Sorting
  result.sort((a, b) => {
    let left, right
    if (projectSortBy.value === 'createdAt') {
      left = new Date(getSpaceCreatedValue(a) || 0).getTime()
      right = new Date(getSpaceCreatedValue(b) || 0).getTime()
    } else if (projectSortBy.value === 'memberCount') {
      left = getSpaceMemberCount(a)
      right = getSpaceMemberCount(b)
    } else {
      left = String(a[projectSortBy.value] || '').toLowerCase()
      right = String(b[projectSortBy.value] || '').toLowerCase()
    }

    if (left < right) return projectSortDirection.value === 'asc' ? -1 : 1
    if (left > right) return projectSortDirection.value === 'asc' ? 1 : -1
    return 0
  })

  return result
})

const goToSpace = (space) => {
  router.push(buildSpacePath(space, 'work-items'))
}
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
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
  padding: var(--app-shell-header-top, 18px) var(--app-shell-page-x, 18px) var(--app-shell-header-bottom, 18px);
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
  margin: 0 !important;
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif !important;
  font-size: 26px !important;
  font-weight: 900 !important;
  line-height: 1.15 !important;
  letter-spacing: 0 !important;
  color: var(--color-text-primary) !important;
}

.page-header .app-shell-title-wrap > .eyebrow,
.page-header .app-shell-title-wrap > p {
  display: none !important;
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
  position: relative;
  z-index: 5;
  min-height: 42px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 8px !important;
  margin: 0 var(--sa-page-x, 24px) 18px;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 86%, transparent), color-mix(in srgb, var(--color-surface-hover) 46%, transparent));
  border: 1px solid color-mix(in srgb, var(--color-border) 72%, transparent);
  border-radius: 12px;
  box-shadow: 0 10px 24px color-mix(in srgb, #020617 6%, transparent);
  flex-wrap: wrap;
}

.search-field {
  position: relative;
  width: min(260px, 30vw);
  min-height: 34px;
  display: flex;
  align-items: center;
  gap: 0;
  padding: 0;
  border: 0;
  border-radius: 9px;
  color: var(--color-text-muted);
  background: transparent;
  box-shadow: none;
}

.search-field > i {
  position: absolute;
  left: 12px;
  z-index: 1;
  color: var(--color-text-muted);
  font-size: 14px;
}

.search-field input {
  box-sizing: border-box !important;
  width: 100%;
  height: 34px !important;
  min-height: 34px !important;
  border: 1px solid var(--color-border) !important;
  border-radius: 9px !important;
  background: var(--color-surface) !important;
  color: var(--color-text-primary) !important;
  font-size: 13.5px !important;
  outline: none !important;
  padding-left: 36px !important;
  padding-right: 12px !important;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.search-field input:focus {
  border-color: var(--color-accent) !important;
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15) !important;
}

.view-toggles {
  display: flex;
  align-items: center;
  gap: 2px;
  height: 32px;
  padding: 2px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface-hover);
}

.toolbar button {
  height: 34px;
  min-height: 34px;
  border: 1px solid var(--color-border);
  border-radius: 9px;
  padding: 0 12px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  font-weight: 600;
  transition: all 0.15s ease;
}

.toolbar button:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.toolbar button.active {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border));
  color: var(--color-accent);
  background: color-mix(in srgb, var(--color-accent) 14%, var(--color-surface));
  box-shadow: 0 1px 3px rgba(0,0,0,0.1);
}

.toolbar,
.toolbar .search-field input,
.toolbar button,
.toolbar .view-toggles {
  overflow: hidden;
}

.toolbar {
  border-radius: 12px !important;
}

.toolbar .search-field input,
.toolbar button {
  border-radius: 9px !important;
}

.toolbar .view-toggles {
  border-radius: 8px !important;
}

.toolbar > span {
  margin-left: auto;
  color: var(--color-text-muted);
  font-size: 11px;
}

.toolbar .search-field {
  order: 1;
  flex: 0 0 min(326px, 34vw);
  width: min(326px, 34vw) !important;
}

.toolbar .project-filter-wrapper {
  order: 2;
}

.toolbar > button {
  order: 3;
}

.toolbar .view-toggles {
  order: 4;
}

.toolbar > span {
  order: 5;
}

.search-field > i {
  top: 50%;
  transform: translateY(-50%);
  pointer-events: none;
}

.search-field input[type="search"] {
  box-sizing: border-box !important;
  padding: 0 12px 0 36px !important;
  -webkit-appearance: none;
  appearance: none;
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

.manage-spaces-page .page-content {
  width: 100% !important;
  max-width: none !important;
  margin: 0 !important;
  padding: 0 18px !important;
  box-sizing: border-box !important;
}

.projects-scroll-panel {
  min-height: 0;
  overflow-y: auto;
  padding: 0 18px 18px 18px !important;
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
  width: 100%;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
  align-items: stretch;
  justify-items: stretch;
}

@media (max-width: 1280px) {
  .spaces-grid {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}

/* Card */
.project-card {
  width: 100%;
  max-width: none;
  background: rgba(255, 255, 255, 0.88);
  border: 1px solid rgba(148, 163, 184, 0.24);
  border-radius: 8px;
  overflow: hidden;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  flex-direction: column;
  min-height: 228px;
  box-shadow: 0 10px 26px rgba(15, 23, 42, 0.07);
}
.project-card:hover {
  border-color: rgba(14, 165, 233, 0.42);
  transform: translateY(-2px);
  box-shadow: 0 26px 62px rgba(15, 23, 42, 0.12);
}

.card-cover {
  height: 76px;
  position: relative;
  display: flex;
  justify-content: flex-end;
  padding: 10px;
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
  padding: 0 14px 14px;
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
  width: 42px !important;
  min-width: 42px !important;
  max-width: 42px !important;
  height: 42px !important;
  min-height: 42px !important;
  max-height: 42px !important;
  flex-basis: 42px !important;
  padding: 9px !important;
  font-size: 18px !important;
  margin-top: -18px;
  margin-bottom: 9px;
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
  margin-bottom: 6px;
}
.proj-title-row h3 {
  margin: 0;
  font-size: 14px;
  font-weight: 800;
  color: #0f172a;
}
.proj-key {
  font-size: 11px;
  color: var(--color-text-muted);
  font-weight: 600;
  margin-top: 2px;
}

.proj-desc {
  font-size: 12px;
  color: #64748b;
  line-height: 1.5;
  margin: 0 0 14px;
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
  gap: 8px;
  margin-top: auto;
  min-width: 0;
}

.project-meta-row {
  display: flex;
  flex-wrap: nowrap;
  gap: 5px;
  align-items: center;
  flex: 1;
  min-width: 0;
  overflow: hidden;
}

.project-meta-item {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 3px;
  min-width: 0;
  min-height: 20px;
  padding: 4px 5px;
  border-radius: 7px;
  border: 1px solid rgba(148, 163, 184, 0.24);
  background: color-mix(in srgb, var(--color-surface-hover, #f4f5f7) 72%, transparent);
  color: var(--color-text-muted, #6b778c);
  font-size: 10px;
  font-weight: 700;
  line-height: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  flex: 0 1 auto;
}

.project-meta-item i {
  flex: 0 0 auto;
  color: var(--color-accent);
  font-size: 10px;
}

.member-meta {
  color: #2563eb;
  border-color: rgba(37, 99, 235, 0.18);
  background: rgba(37, 99, 235, 0.08);
}

.date-meta {
  color: #64748b;
  border-color: rgba(14, 165, 233, 0.18);
  background: rgba(14, 165, 233, 0.08);
}

.visibility-pill {
  color: #0f766e;
  background: rgba(20, 184, 166, 0.1);
  border-color: rgba(20, 184, 166, 0.22);
}

.visibility-pill.private {
  color: #7c3aed;
  background: rgba(124, 58, 237, 0.1);
  border-color: rgba(124, 58, 237, 0.22);
}

.member-meta i,
.date-meta i,
.visibility-pill i {
  color: currentColor;
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
  border: 0 !important;
  font-size: 14px;
  flex: 0 0 30px;
  width: 30px;
  height: 30px;
  margin-left: auto;
  box-shadow: none;
  backdrop-filter: none;
  color: var(--color-text-muted);
}
.card-footer .card-icon-btn:hover {
  background: transparent;
  color: var(--color-text-primary);
  box-shadow: none;
}

.loading-state, .empty-state { text-align: center; margin-top: 60px; color: var(--color-text-muted); }
.empty-icon { font-size: 48px; color: #3F3F46; margin-bottom: 16px; }
.empty-state p { margin-bottom: 24px; }

@media (max-width: 900px) {
  .spaces-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

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

@media (max-width: 640px) {
  .spaces-grid {
    grid-template-columns: 1fr;
  }
}

.spaces-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
}

.spaces-table th {
  background: var(--color-surface);
  border-bottom: 2px solid var(--color-border) !important;
  padding: 12px 16px !important;
  font-size: 11px;
  letter-spacing: 0.05em;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--color-text-secondary);
}

.spaces-table th i {
  color: inherit;
  margin-right: 6px;
  opacity: 0.88;
}

.spaces-table td {
  height: 50px;
  padding: 10px 14px !important;
  font-size: 13px;
  color: var(--color-text-primary);
  border-bottom: 1px solid var(--color-border) !important;
  vertical-align: middle;
  white-space: nowrap;
}

.spaces-table tbody tr {
  box-shadow: inset 3px 0 0 transparent;
  transition: all 0.2s ease;
  cursor: pointer;
}

.spaces-table tbody tr:hover {
  box-shadow: inset 3px 0 0 var(--sa-primary, var(--color-accent)) !important;
}

.spaces-table tbody tr:hover td {
  background: color-mix(in srgb, var(--sa-primary, var(--color-accent)) 8%, var(--color-surface)) !important;
}

.project-name-title {
  font-weight: 700;
  color: var(--color-text-primary);
  font-size: 13px;
}

.project-name-title:hover {
  color: var(--color-accent);
}

.spaces-table-container {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.04);
  margin-top: 14px;
}

[data-theme='dark'] .manage-spaces-page {
  background:
    linear-gradient(180deg, #07111f, #0f172a 54%, #101827);
}

[data-theme='dark'] .spaces-header,
[data-theme='dark'] .project-card,
[data-theme='dark'] .spaces-table-container {
  border-color: var(--color-border);
  background: var(--color-surface);
  box-shadow: 0 10px 24px rgba(0, 0, 0, 0.16);
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

[data-theme='dark'] .project-meta-item {
  border-color: rgba(148, 163, 184, 0.18);
  background: rgba(30, 41, 59, 0.72);
  color: #cbd5e1;
}

[data-theme='dark'] .member-meta {
  color: #5eead4;
  border-color: rgba(45, 212, 191, 0.24);
  background: rgba(20, 83, 75, 0.42);
}

[data-theme='dark'] .date-meta {
  color: #c4b5fd;
  border-color: rgba(167, 139, 250, 0.22);
  background: rgba(67, 56, 202, 0.24);
}

[data-theme='dark'] .visibility-pill {
  color: #93c5fd;
  border-color: rgba(96, 165, 250, 0.24);
  background: rgba(30, 64, 175, 0.28);
}

[data-theme='dark'] .visibility-pill.private {
  color: #fcd34d;
  border-color: rgba(251, 191, 36, 0.22);
  background: rgba(146, 64, 14, 0.28);
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
  width: 100% !important;
  grid-template-columns: repeat(auto-fit, minmax(min(250px, 100%), 1fr)) !important;
  gap: 14px !important;
  justify-items: stretch !important;
}

.project-card {
  width: 100% !important;
  max-width: none !important;
  min-height: 228px !important;
  border-radius: 8px !important;
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
  font-size: 14px !important;
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

.filter-dropdown-wrapper {
  position: relative !important;
  display: inline-block !important;
}

.filter-dropdown-menu {
  position: absolute !important;
  top: calc(100% + 8px) !important;
  left: 0 !important;
  right: auto !important;
  z-index: 1050 !important;
  width: 640px;
  max-width: calc(100vw - 32px);
  padding: 8px !important;
  border: 1px solid var(--color-border) !important;
  border-radius: 12px !important;
  background: var(--color-surface-elevated) !important;
  box-shadow: var(--shadow-popover) !important;
  overflow: hidden;
}
:deep(.filter-bar-container) {
  border: none !important;
  background: transparent !important;
  box-shadow: none !important;
  padding: 0 !important;
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
