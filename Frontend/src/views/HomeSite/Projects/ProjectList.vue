<template>
  <div class="projects-wrapper">
    <header class="module-header">
      <div class="header-content">
        <div class="app-shell-title-wrap">
          <h1>{{ pageTitle }}</h1>
          <div class="app-shell-header-help">
            <span class="app-shell-header-help-btn" :aria-label="`About ${pageTitle}`">
              <i class="fa-solid fa-question"></i>
            </span>
            <div class="app-shell-header-help-popover" role="tooltip">
              <span>{{ pageTitle }}</span>
              <p>{{ labels.searchProjects }}</p>
            </div>
          </div>
        </div>
        <div class="header-actions">
          <button class="primary-btn" @click="openCreateModal">{{ labels.createProject }}</button>
        </div>
      </div>
      
      <div class="tabs-nav">
        <button class="tab-btn" :class="{ active: currentTab === 'all' }" @click="currentTab = 'all'">{{ labels.allProjects }}</button>
        <button class="tab-btn" :class="{ active: currentTab === 'following' }" @click="currentTab = 'following'">{{ labels.following }}</button>
        <button class="tab-btn" :class="{ active: currentTab === 'archived' }" @click="currentTab = 'archived'">{{ labels.archived }}</button>
      </div>
    </header>

    <div class="module-content">
      <ProjectPageToolbar
        v-model:searchQuery="searchQuery"
        show-search
        :search-placeholder="labels.searchProjects"
      >
        <template #filters>
          <div class="filter-dropdown-wrapper js-toolbar-popup-scope">
            <button
              class="timeline-filter-trigger icon-only-trigger"
              type="button"
              :aria-label="labels.filters"
              :title="labels.filters"
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
            <button class="icon-btn" :class="{ active: viewMode === 'table' }" :title="labels.listView" @click="viewMode = 'table'"><i class="fa-solid fa-list-ul"></i></button>
            <button class="icon-btn" :class="{ active: viewMode === 'cards' }" :title="labels.horizontalView" @click="viewMode = 'cards'"><i class="fa-solid fa-bars-staggered"></i></button>
          </div>
        </template>
        <template #sort>
          <ToolbarSortMenu v-model="projectSortBy" v-model:direction="projectSortDirection" :label="labels.sortByFollowing" :options="projectSortOptions" />
        </template>
      </ProjectPageToolbar>

      <div v-if="isLoading" class="loading-state">
        <div class="loader-spinner"></div>
      </div>
      <template v-else>
        <div class="table-container mt-16" v-if="filteredProjects.length > 0">
          <table class="jira-table" v-if="viewMode === 'table'">
            <thead>
              <tr>
                <th class="col-name">{{ labels.name }}</th>
                <th class="col-status">{{ labels.status }}</th>
                <th class="col-date">{{ labels.targetDate }}</th>
                <th class="col-owner">{{ labels.owner }}</th>
                <th class="col-following">{{ labels.following }}</th>
                  <th class="col-star" style="width: 100px;">{{ labels.starred }}</th>
                <th class="col-updated">{{ labels.lastUpdated }}</th>
                <th class="actions-col"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="proj in filteredProjects" :key="proj.id" @click="goToProject(proj.id)">
                <td>
                  <div class="project-title-cell">
                    <ProjectAvatar :icon="proj.icon" :background="proj.cover" size="sm" />
                    <span class="project-title">{{ proj.title }}</span>
                  </div>
                </td>
                <td>
                  <span class="status-badge" :class="getStatusClass(proj.status || labels.pending)">
                    {{ translateStatus(proj.status || labels.pending) }} <i class="fa-solid fa-chevron-down ms-1" v-if="!isCompletedStatus(proj.status)"></i>
                  </span>
                </td>
                <td>
                  <div class="target-date-badge" :class="{ 'overdue': false }">
                    <i class="fa-regular fa-calendar"></i> {{ formatDate(proj.startDate || proj.createdAt) }}
                  </div>
                </td>
                <td>
                  <UserAvatar :user="{ id: proj.ownerId, fullName: proj.owner || proj.ownerName || proj.creatorName, avatarUrl: proj.ownerAvatarUrl, avatarColor: proj.ownerColor }" :size="24" :fontSize="10" class="owner-avatar-micro" />
                </td>
                <td @click.stop="toggleFollow(proj.id)">
                  <span class="following-text" style="cursor: pointer;">{{ proj.isFollowing ? labels.following : labels.follow }}</span>
                </td>
                <td @click.stop>
                  <button
                    class="icon-btn"
                    type="button"
                    :disabled="starredStore.isPending('Project', proj.id)"
                    :aria-label="proj.isStarred ? labels.unstar : labels.starred"
                    @click="toggleStar(proj.id)"
                  >
                    <i :class="proj.isStarred ? 'fa-solid fa-star text-yellow-400' : 'fa-regular fa-star text-gray-400'"></i>
                  </button>
                </td>
                <td>
                  <span class="updated-text">{{ formatDate(proj.updatedAt || proj.createdAt) }}</span>
                </td>
                <td class="actions-col" @click.stop>
                  <button class="icon-btn"><i class="fa-solid fa-ellipsis"></i></button>
                </td>
              </tr>
            </tbody>
          </table>
          
          <div class="project-card-list" v-else>
            <article class="project-row-card" v-for="proj in filteredProjects" :key="proj.id" @click="goToProject(proj.id)">
              <div class="project-row-main">
                <ProjectAvatar :icon="proj.icon" :background="proj.cover" size="md" />
                <div class="project-row-text">
                  <h3>{{ proj.title }}</h3>
                  <p>{{ proj.owner || proj.ownerName || labels.noOwner }}</p>
                </div>
              </div>
              <div class="project-row-meta">
                <span class="status-badge" :class="getStatusClass(proj.status || labels.pending)">
                  {{ translateStatus(proj.status || labels.pending) }}
                </span>
                <span class="target-date-badge">
                  <i class="fa-regular fa-calendar"></i> {{ formatDate(proj.startDate || proj.createdAt) }}
                </span>
                <button class="row-action-btn" @click.stop="toggleFollow(proj.id)">
                  <i class="fa-regular fa-eye"></i>
                  {{ proj.isFollowing ? labels.following : labels.follow }}
                </button>
                <button
                  class="row-action-btn icon-only"
                  type="button"
                  :disabled="starredStore.isPending('Project', proj.id)"
                  :aria-label="proj.isStarred ? labels.unstar : labels.starred"
                  @click.stop="toggleStar(proj.id)"
                >
                  <i :class="proj.isStarred ? 'fa-solid fa-star text-yellow-400' : 'fa-regular fa-star text-gray-400'"></i>
                </button>
              </div>
            </article>
          </div>
        </div>

        <div class="empty-state-large mt-16" v-else>
          <div class="empty-spaces-icon" aria-hidden="true">
            <i class="fa-regular fa-folder-open"></i>
          </div>
          <div class="empty-spaces-copy">
            <h3>{{ labels.noProjects }}</h3>
            <p>{{ labels.tryFilters }} <a href="#" @click.prevent="clearFilters" style="color: var(--color-accent); text-decoration: underline;">{{ labels.clearAllFilters }}</a>.</p>
            <button class="empty-spaces-btn mt-3" type="button" @click="openCreateModal">
              {{ labels.createProject }}
            </button>
          </div>
        </div>
      </template>
    </div>

    <!-- Create Project Modal (Jira Style) -->
    <Teleport to="body">
    <div class="modal-overlay sa-data-modal-overlay sa-modal--lg" v-if="isCreateModalOpen" @click.self="isCreateModalOpen = false">
      <div class="jira-dialog">
        <div class="jira-dialog-header">
          <DataModalHeader icon="bi bi-rocket-takeoff" :title="labels.project" :description="labels.requiredNote" @close="isCreateModalOpen = false" />
        </div>
        
        <div class="jira-dialog-body">
          <DataModalSection icon="bi bi-card-text" :title="labels.name">
          <div class="form-group mt-16">
            <label>{{ labels.name }} <span class="required">*</span></label>
            <input type="text" v-model="newProject.title" class="jira-input" />
          </div>
          </DataModalSection>
          <DataModalSection icon="bi bi-palette2" :title="labels.chooseEmoji">
          <div class="form-group mt-16">
            <label>{{ labels.chooseEmoji }}</label>
            <div class="emoji-picker-control">
              <button class="emoji-btn">{{ newProject.icon }}</button>
              <button class="refresh-emoji-btn" @click="cycleEmoji"><i class="fa-solid fa-arrows-rotate"></i></button>
            </div>
          </div>
          </DataModalSection>
          <DataModalSection icon="bi bi-shield-lock" :title="labels.privacy">
          <div class="form-group mt-16">
            <label>{{ labels.linkScale }}</label>
            <input type="text" :placeholder="labels.searchScale" class="jira-input" />
          </div>
          </DataModalSection>
          
          <div class="form-group mt-16 privacy-group">
            <div class="privacy-info">
              <label>{{ labels.privacy }}</label>
              <p>{{ labels.privacyDesc }}</p>
            </div>
            <div class="toggle-switch" :class="{ 'active': newProject.isPrivate }" @click="newProject.isPrivate = !newProject.isPrivate">
              <div class="toggle-knob"></div>
            </div>
          </div>
        </div>
        
        <div class="jira-dialog-footer">
          <button class="cancel-btn" @click="isCreateModalOpen = false"><i class="bi bi-x-lg"></i>{{ labels.cancel }}</button>
          <button class="primary-btn" :disabled="!newProject.title" @click="submitCreateProject"><i class="fa-solid fa-plus"></i>{{ labels.create }}</button>
        </div>
      </div>
    </div>
    </Teleport>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useHomeProjectStore } from '@/store/useHomeProjectStore'
import { useStarredStore } from '@/store/useStarredStore'
import { useFollowerStore } from '@/store/useFollowerStore'
import { useI18nStore } from '@/store/useI18nStore'
import { ElMessage } from 'element-plus'
import { useSiteStore } from '@/store/useSiteStore'
import { isValidEntityId } from '@/utils/contextIds'
import UserAvatar from '@/components/common/UserAvatar.vue'
import ProjectAvatar from '@/components/project/ProjectAvatar.vue'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import ToolbarSortMenu from '@/components/common/ToolbarSortMenu.vue'
import FilterBar from '@/components/FilterBar.vue'

const router = useRouter()
const projectStore = useHomeProjectStore()
const starredStore = useStarredStore()
const followerStore = useFollowerStore()
const i18nStore = useI18nStore()
const siteStore = useSiteStore()

const currentTab = ref('all')
const searchQuery = ref('')
const showProjectFilters = ref(false)
const viewMode = ref('table')
const isVi = computed(() => i18nStore.locale === 'vi')
const labels = computed(() => isVi.value
  ? {
      title: 'Dự án',
      createProject: 'Tạo dự án',
      allProjects: 'Tất cả dự án',
      following: 'Đang theo dõi',
      archived: 'Đã lưu trữ',
      searchProjects: 'Tìm kiếm dự án',
      status: 'Trạng thái',
      owner: 'Chủ sở hữu',
      follow: 'Theo dõi',
      favorite: 'Yêu thích',
      clearFilters: 'Xóa lọc',
      showing: 'Đang hiển thị',
      projectsLower: 'dự án',
      listView: 'Danh sách',
      horizontalView: 'Dạng ngang',
      sortByFollowing: 'Sắp xếp theo đang theo dõi',
      columns: 'Cột',
      name: 'Tên',
      targetDate: 'Ngày mục tiêu',
      starred: 'Có gắn sao',
      lastUpdated: 'Cập nhật lần cuối',
      pending: 'Đang chờ xử lý',
      completed: 'Đã hoàn tất',
      onTrack: 'Đúng tiến độ',
      atRisk: 'Có rủi ro',
      offTrack: 'Trễ tiến độ',
      noOwner: 'Chưa có chủ sở hữu',
      noProjects: 'Chúng tôi không tìm được dự án nào phù hợp với nội dung tìm kiếm của bạn.',
      tryFilters: 'Hãy thử thay đổi tiêu chí tìm kiếm hoặc',
      clearAllFilters: 'xóa tất cả bộ lọc'
      ,
      project: 'Dự án',
      requiredNote: 'Các trường bắt buộc được đánh dấu bằng dấu sao',
      chooseEmoji: 'Chọn một biểu tượng cảm xúc',
      linkScale: 'Liên kết tới quy mô lớn SprintA hiện có',
      searchScale: 'Tìm kiếm quy mô lớn',
      privacy: 'Kiểm soát quyền riêng tư',
      privacyDesc: 'Chỉ những người đóng góp hoặc những người bạn chia sẻ mới có thể xem dự án riêng tư.',
      cancel: 'Hủy',
      create: 'Tạo'
    }
  : {
      title: 'Projects',
      createProject: 'Create project',
      allProjects: 'All projects',
      following: 'Following',
      archived: 'Archived',
      searchProjects: 'Search projects',
      status: 'Status',
      owner: 'Owner',
      follow: 'Follow',
      favorite: 'Favorite',
      clearFilters: 'Clear filters',
      showing: 'Showing',
      projectsLower: 'projects',
      listView: 'List view',
      horizontalView: 'Horizontal cards',
      sortByFollowing: 'Sort by following',
      columns: 'Columns',
      name: 'Name',
      targetDate: 'Target date',
      starred: 'Starred',
      lastUpdated: 'Last updated',
      pending: 'Pending',
      completed: 'Completed',
      onTrack: 'On track',
      atRisk: 'At risk',
      offTrack: 'Off track',
      noOwner: 'No owner',
      noProjects: 'We could not find any projects matching your search.',
      tryFilters: 'Try changing the filters or',
      clearAllFilters: 'clear all filters'
      ,
      project: 'Project',
      requiredNote: 'Required fields are marked with an asterisk',
      chooseEmoji: 'Choose an emoji',
      linkScale: 'Link to an existing SprintA scale',
      searchScale: 'Search scale',
      privacy: 'Privacy control',
      privacyDesc: 'Only contributors or people you share with can view a private project.',
      cancel: 'Cancel',
      create: 'Create'
    })
const translateStatus = (status) => {
  if (!status) return labels.value.pending
  if (status === true || status === 'true') return labels.value.pending
  if (status === false || status === 'false') return labels.value.archived
  const map = {
    'on track': labels.value.onTrack,
    'đúng tiến độ': labels.value.onTrack,
    'dung tien do': labels.value.onTrack,
    'at risk': labels.value.atRisk,
    'có rủi ro': labels.value.atRisk,
    'co rui ro': labels.value.atRisk,
    'off track': labels.value.offTrack,
    'trễ tiến độ': labels.value.offTrack,
    'tre tien do': labels.value.offTrack,
    'pending': labels.value.pending,
    'đang chờ xử lý': labels.value.pending,
    'dang cho xu ly': labels.value.pending,
    'completed': labels.value.completed,
    'đã hoàn tất': labels.value.completed,
    'da hoan tat': labels.value.completed,
    'archived': labels.value.archived,
    'đã lưu trữ': labels.value.archived,
    'da luu tru': labels.value.archived
  }
  return map[status.toString().toLowerCase()] || status
}

const activeFilters = ref([])

const projectFilterFields = computed(() => [
  { key: 'status', label: labels.value.status, icon: 'fa-solid fa-circle-dot', values: statusOptions.value },
  { key: 'owner', label: labels.value.owner, icon: 'fa-regular fa-user', values: ownerOptions.value },
  { key: 'following', label: labels.value.follow, icon: 'fa-regular fa-eye', values: [isVi.value ? 'Có' : 'Yes', isVi.value ? 'Không' : 'No'] },
  { key: 'starred', label: labels.value.favorite, icon: 'fa-regular fa-star', values: [isVi.value ? 'Có' : 'Yes', isVi.value ? 'Không' : 'No'] }
])

const projectOperators = {
  status: ['is', 'is not'],
  owner: ['is', 'is not'],
  following: ['is', 'is not'],
  starred: ['is', 'is not']
}

const customProjectValueMeta = (fieldKey, value) => {
  if (fieldKey === 'status') {
    return { icon: 'fa-solid fa-circle-dot', color: '#10b981' }
  }
  if (fieldKey === 'owner') {
    return { icon: 'fa-regular fa-user', color: '#3b82f6' }
  }
  if (fieldKey === 'following') {
    return { icon: 'fa-regular fa-eye', color: '#8b5cf6' }
  }
  if (fieldKey === 'starred') {
    return { icon: 'fa-solid fa-star', color: '#eab308' }
  }
  return null
}

const showFilterDropdown = ref(false)
const toggleFilterDropdown = () => {
  showFilterDropdown.value = !showFilterDropdown.value
}
const handleOutsideClick = (e) => {
  if (!e.target.closest('.js-toolbar-popup-scope')) {
    showFilterDropdown.value = false
  }
}

const projectSortDirection = ref('desc')
const projectSortBy = ref('updatedAt')
const projectSortOptions = [
  { value: 'updatedAt', label: 'Cập nhật gần nhất', icon: 'fa-regular fa-clock' },
  { value: 'createdAt', label: 'Mới tạo gần nhất', icon: 'fa-regular fa-calendar-plus' },
  { value: 'name', label: 'Tên dự án', icon: 'fa-solid fa-font' },
  { value: 'status', label: 'Trạng thái', icon: 'fa-solid fa-circle-dot' }
]
const toggleProjectSort = () => {
  projectSortDirection.value = projectSortDirection.value === 'desc' ? 'asc' : 'desc'
}

const uniqueValues = (selector) => Array.from(new Set(
  (projectStore.projects || [])
    .map(selector)
    .filter(value => value && value !== 'N/A')
)).sort()

const statusOptions = computed(() => {
  const statuses = uniqueValues(p => p.status);
  return Array.from(new Set(statuses.map(translateStatus).filter(s => s && s.toString().trim() !== '')));
});
const ownerOptions = computed(() => uniqueValues(p => p.owner))

const booleanOptions = computed(() => [
  { label: isVi.value ? 'Có' : 'Yes', value: 'true' },
  { label: isVi.value ? 'Không' : 'No', value: 'false' }
])

const clearFilters = () => {
  activeFilters.value = []
}
const hasActiveFilters = computed(() => activeFilters.value.length > 0)

const isCreateModalOpen = ref(false)

const newProject = ref({
  title: '',
  icon: '😎',
  isPrivate: false
})

const projectEmojis = ['😎', '🚀', '🎯', '💡', '🔥', '🌟', '💻', '📈', '✨', '🌈']

const cycleEmoji = () => {
  const currentIndex = projectEmojis.indexOf(newProject.value.icon)
  const nextIndex = (currentIndex + 1) % projectEmojis.length
  newProject.value.icon = projectEmojis[nextIndex]
}

const openCreateModal = () => {
  newProject.value = { title: '', icon: '😎', isPrivate: false }
  isCreateModalOpen.value = true
}

const submitCreateProject = async () => {
  if (!newProject.value.title) return
  
  try {
    const payload = {
      name: newProject.value.title, // Backend uses Name
      icon: newProject.value.icon, // Backend uses Icon
      startDate: new Date().toISOString(), // Required by CreateProjectDto
      networkType: newProject.value.isPrivate ? 'Private' : 'Public',
      workspaceId: projectStore.getWorkspaceId()
    }
    
    await projectStore.createProject(payload)
    isCreateModalOpen.value = false
  } catch (err) {
    console.error('Lỗi khi tạo dự án:', err)
  }
}

const route = useRoute()

const isDirectory = computed(() => currentTab.value === 'all')
const isFollowing = computed(() => currentTab.value === 'following')
const isArchived = computed(() => currentTab.value === 'archived')

const pageTitle = computed(() => {
  if (isFollowing.value) return labels.value.following
  if (isArchived.value) return labels.value.archived
  return labels.value.title
})

onMounted(async () => {
  await siteStore.fetchSites()
  await projectStore.initializeRealtime()
  await projectStore.fetchProjects()
  await starredStore.fetchStarredItems({ page: 1, pageSize: 100 })
  await followerStore.fetchFollowedItems()
  window.addEventListener('global-create-click', openCreateModal)
  document.addEventListener('click', handleOutsideClick)
})

watch(
  () => siteStore.activeSite?.id || siteStore.activeSite?.Id || null,
  async (workspaceId, previousWorkspaceId) => {
    if (!workspaceId || !previousWorkspaceId || `${workspaceId}` === `${previousWorkspaceId}`) return
    currentTab.value = 'all'
    searchQuery.value = ''
    clearFilters()
    projectStore.clearWorkspaceData(workspaceId)
    await projectStore.initializeRealtime()
    await projectStore.fetchProjects()
    await starredStore.fetchStarredItems({ page: 1, pageSize: 100 })
    await followerStore.fetchFollowedItems()
  }
)

onUnmounted(() => {
  window.removeEventListener('global-create-click', openCreateModal)
  document.removeEventListener('click', handleOutsideClick)
})

const isLoading = computed(() => projectStore.isLoading)

const filteredProjects = computed(() => {
  let list = projectStore.projects || []
  
  const currentWorkspaceId = projectStore.getWorkspaceId()
  if (isValidEntityId(currentWorkspaceId)) {
    list = list.filter(p => p.workspaceId === currentWorkspaceId)
  }

  // Filter by tab
  if (isArchived.value) {
    list = list.filter(p => p.isArchived)
  } else if (isFollowing.value) {
    list = list.filter(p => p.isFollowing)
  } else {
    list = list.filter(p => !p.isArchived)
  }

  // Filter by search
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    list = list.filter(p => 
      p.title.toLowerCase().includes(q) || 
      (p.owner && p.owner.toLowerCase().includes(q)) ||
      (p.key && p.key.toLowerCase().includes(q))
    )
  }

  
  if (activeFilters.value.length > 0) {
    list = list.filter(p => {
      return activeFilters.value.every(f => {
        let val = ''
        let isMatch = false
        if (f.field === 'status') {
          val = translateStatus(p.status)
          isMatch = val === f.value
        } else if (f.field === 'owner') {
          val = p.owner
          isMatch = val === f.value
        } else if (f.field === 'following') {
          const isFol = f.value === (isVi.value ? 'Có' : 'Yes')
          isMatch = (followerStore.followedItems?.some(i => i.entityId === p.id) || false) === isFol
        } else if (f.field === 'starred') {
          const isStar = f.value === (isVi.value ? 'Có' : 'Yes')
          isMatch = starredStore.isStarred('Project', p.id) === isStar
        }
        return f.operator === 'is' ? isMatch : !isMatch
      })
    })
  }

    const normalized = list.map(p => ({
    ...p,
    key: p.key || (p.title ? p.title.substring(0, 3).toUpperCase() : 'PRJ'),
    status: p.status === true ? labels.value.pending : (p.status === false ? labels.value.archived : (p.status || labels.value.pending)),
    isStarred: starredStore.isStarred('Project', p.id),
    isFollowing: followerStore.followedItems?.some(i => i.entityId === p.id) || false
  }))
  return normalized.sort((a, b) => {
    let left
    let right
    if (projectSortBy.value === 'name' || projectSortBy.value === 'status') {
      left = `${a[projectSortBy.value] || ''}`.toLowerCase()
      right = `${b[projectSortBy.value] || ''}`.toLowerCase()
    } else {
      left = new Date(a[projectSortBy.value] || 0).getTime()
      right = new Date(b[projectSortBy.value] || 0).getTime()
    }
    const result = left < right ? -1 : (left > right ? 1 : 0)
    return projectSortDirection.value === 'asc' ? result : -result
  })
})

const goToProject = (id) => {
  router.push(`/home/projects/${id}`)
}

const toggleStar = async (id) => {
  try {
    await starredStore.setStarred('Project', id, !starredStore.isStarred('Project', id))
  } catch {
    ElMessage.error(starredStore.error || labels.value.loadFailed)
  }
}

const toggleFollow = async (id) => {
  await followerStore.toggleFollow('Project', id)
}

const getInitials = (value) => {
  const text = String(value || '').trim()
  if (!text) return '?'
  return text
    .split(/\s+/)
    .slice(0, 2)
    .map(part => part[0]?.toUpperCase())
    .join('')
}

const formatDate = (value) => {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '-'
  return date.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

const getStatusClass = (status) => {
  const value = String(status || '').toLowerCase()
  if (value.includes('risk') || value.includes('rui ro') || value.includes('rủi ro')) return 'status-default'
  if (value.includes('done') || value.includes('complete') || value.includes('hoan') || value.includes('hoàn')) return 'status-done'
  if (value.includes('on track') || value.includes('active') || value.includes('dung') || value.includes('đúng')) return 'status-on-track'
  return 'status-default'
}

const isCompletedStatus = (status) => {
  const value = String(status || '').toLowerCase()
  return value.includes('complete') || value.includes('done') || value.includes('hoàn') || value.includes('hoan')
}

</script>

<style scoped>
.projects-wrapper {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
  background-color: #FFFFFF;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
}

.module-header {
  padding: var(--app-shell-header-top, 18px) var(--app-shell-page-x, 18px) 0;
  background-color: #FFFFFF;
}

.header-content {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 24px;
}

.header-content .app-shell-title-wrap h1 {
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif !important;
  font-size: 26px !important;
  font-weight: 900 !important;
  line-height: 1.15 !important;
  letter-spacing: 0 !important;
  color: var(--color-text-primary, #172B4D) !important;
  margin: 0 !important;
}

.primary-btn {
  background-color: #0052CC;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 3px;
  font-weight: 500;
  font-size: 14px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.primary-btn:hover:not(:disabled) {
  background-color: #0047B3;
}

.primary-btn:disabled {
  background-color: #EBECF0;
  color: #A5ADBA;
  cursor: not-allowed;
}

.secondary-btn {
  background-color: rgba(9, 30, 66, 0.04);
  color: #42526E;
  border: none;
  padding: 8px 16px;
  border-radius: 3px;
  font-weight: 500;
  font-size: 14px;
  cursor: pointer;
  text-decoration: none;
  transition: background-color 0.2s;
}

.secondary-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.tabs-nav {
  display: flex;
  border-bottom: 2px solid #DFE1E6;
  gap: 24px;
}

.tab-btn {
  background: none;
  border: none;
  padding: 8px 0 12px;
  font-size: 14px;
  font-weight: 500;
  color: #5E6C84;
  cursor: pointer;
  position: relative;
  margin-bottom: -2px;
  border-bottom: 2px solid transparent;
  transition: color 0.2s;
}

.tab-btn:hover {
  color: #172B4D;
}

.tab-btn.active {
  color: #0052CC;
  border-bottom-color: #0052CC;
}

.module-content {
  padding: 18px var(--app-shell-page-x, 18px) 28px;
  flex: 1;
}

/* Empty State Dành cho bạn */
.empty-state-banner {
  background-color: #FAFBFC;
  border-radius: 8px;
  overflow: hidden;
}

.empty-banner-content {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 40px 64px;
  max-width: 1000px;
  margin: 0 auto;
}

.empty-banner-text {
  flex: 1;
  max-width: 400px;
}

.empty-banner-text h2 {
  font-size: 24px;
  font-weight: 500;
  color: #172B4D;
  margin: 0 0 16px 0;
}

.empty-banner-text p {
  font-size: 16px;
  color: #42526E;
  margin: 0 0 32px 0;
  line-height: 1.5;
}

.empty-banner-actions {
  display: flex;
  gap: 16px;
  align-items: center;
}

.empty-banner-illustration {
  flex: 1;
  display: flex;
  justify-content: flex-end;
}

.empty-illustration {
  width: 280px;
  height: 200px;
  background-color: #E6FCFF;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.empty-illustration i {
  font-size: 64px;
  color: #0052CC;
}

/* List Controls */
.list-controls {
  margin-bottom: 24px;
}

.search-box-wrapper {
  position: relative;
  width: 250px;
}

.search-icon {
  position: absolute;
  left: 10px;
  top: 50%;
  transform: translateY(-50%);
  color: #5E6C84;
  font-size: 14px;
}

.search-input {
  width: 100%;
  padding: 8px 12px 8px 44px;
  border: 2px solid #DFE1E6;
  border-radius: 3px;
  font-size: 14px;
  color: #172B4D;
  outline: none;
  transition: border-color 0.2s, background-color 0.2s;
  box-sizing: border-box;
}

.search-input:hover {
  background-color: #FAFBFC;
}

.search-input:focus {
  background-color: #FFFFFF;
  border-color: #4C9AFF;
}

/* Table */
.jira-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
}

.jira-table th {
  padding: 8px 12px;
  font-size: 12px;
  font-weight: 600;
  color: #5E6C84;
  border-bottom: 2px solid #DFE1E6;
}

.sort-icon {
  margin-left: 4px;
  font-size: 12px;
  color: #5E6C84;
}
.project-toolbar-icon {
  width: 42px !important;
  min-width: 42px !important;
  height: 34px;
  padding: 0 !important;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
}
.project-toolbar-icon:hover {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  background: color-mix(in srgb, var(--color-accent) 6%, var(--color-surface)) !important;
  color: var(--color-accent) !important;
}
.project-toolbar-icon:hover i { color: var(--color-accent) !important; }
.project-toolbar-icon.active {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important;
  color: var(--color-accent) !important;
}

.col-name { width: 40%; }
.col-key { width: 15%; }
.col-type { width: 25%; }
.col-lead { width: 15%; }
.actions-col { width: 5%; text-align: right; }

.jira-table td {
  padding: 12px;
  font-size: 14px;
  color: #172B4D;
  border-bottom: 1px solid #DFE1E6;
  cursor: pointer;
  vertical-align: middle;
}

.jira-table tbody tr:hover td {
  background-color: #FAFBFC;
}

.project-title-cell {
  display: flex;
  align-items: center;
  gap: 12px;
}

.project-avatar-small {
  width: 24px;
  height: 24px;
  background-color: #0052CC;
  color: white;
  border-radius: 3px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  font-weight: bold;
}

.project-title {
  font-weight: 500;
  color: #0052CC;
}

.project-title:hover {
  text-decoration: underline;
}

.owner-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.owner-avatar {
  width: 24px;
  height: 24px;
  background-color: #172B4D;
  color: white;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  font-weight: bold;
}

.icon-btn {
  appearance: none;
  -webkit-appearance: none;
  background: transparent;
  border: 0;
  font-size: 14px;
  font-family: inherit;
  color: #6B778C;
  cursor: pointer;
  padding: 6px;
  border-radius: 3px;
  opacity: 1;
  transition: opacity 0.2s, background-color 0.2s;
  touch-action: manipulation;
}

.icon-btn.starred {
  color: #FFAB00;
  opacity: 1;
}

.jira-table tbody tr:hover .icon-btn {
  opacity: 1;
}

.icon-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.icon-btn:focus-visible,
.row-action-btn:focus-visible {
  outline: 3px solid color-mix(in srgb, var(--home-accent, #0c66e4) 42%, transparent);
  outline-offset: 2px;
}

.icon-btn:disabled,
.row-action-btn:disabled {
  cursor: wait;
}

.icon-btn i {
  display: block;
  width: 1em;
  line-height: 1;
  text-align: center;
}

/* Empty State Table */
.empty-state {
  text-align: center;
  padding: 64px 20px;
}

.empty-icon-wrapper {
  width: 80px;
  height: 80px;
  background-color: #E6FCFF;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0 auto 16px;
  color: #0052CC;
  font-size: 32px;
}

.empty-state h3 {
  margin: 0 0 8px 0;
  color: #172B4D;
  font-size: 20px;
}

.empty-state p {
  margin: 0;
  color: #5E6C84;
}

.loading-state {
  display: flex;
  justify-content: center;
  padding: 64px;
}

.loader-spinner {
  width: 32px;
  height: 32px;
  border: 3px solid #DFE1E6;
  border-top-color: #0052CC;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* Modal */
.modal-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background-color: rgba(9, 30, 66, 0.54);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background-color: #FFFFFF;
  border-radius: 3px;
  width: 500px;
  box-shadow: 0 8px 16px -4px rgba(9, 30, 66, 0.25);
}

.modal-header {
  padding: 20px 24px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid #DFE1E6;
}

.modal-header h2 {
  margin: 0;
  font-size: 20px;
  font-weight: 500;
  color: #172B4D;
}

.close-btn {
  background: none;
  border: none;
  font-size: 24px;
  color: #5E6C84;
  cursor: pointer;
}

.modal-body {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.form-group label {
  display: block;
  font-size: 12px;
  font-weight: 600;
  color: #5E6C84;
  margin-bottom: 8px;
}

.required {
  color: #DE350B;
}

.form-group input {
  width: 100%;
  padding: 8px 12px;
  border: 2px solid #DFE1E6;
  border-radius: 3px;
  font-size: 14px;
  box-sizing: border-box;
  outline: none;
}

.form-group input:focus {
  border-color: #4C9AFF;
}

.modal-footer {
  padding: 16px 24px;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  border-top: 1px solid #DFE1E6;
}

.cancel-btn {
  background: transparent;
  color: #5E6C84;
  border: none;
  padding: 8px 12px;
  border-radius: 3px;
  font-weight: 500;
  cursor: pointer;
}

.cancel-btn:hover {
  background: rgba(9, 30, 66, 0.08);
}

/* --- Jira Dialog Styles --- */
.jira-dialog {
  background: #FFFFFF;
  border-radius: 3px;
  width: 400px;
  max-width: 90vw;
  box-shadow: 0 8px 16px -4px rgba(9,30,66,0.25), 0 0 1px rgba(9,30,66,0.31);
  display: flex;
  flex-direction: column;
}

.jira-dialog-header {
  display: flex;
  align-items: center;
  padding: 16px;
  gap: 12px;
}

.icon-btn-header {
  background: transparent;
  border: none;
  color: #42526E;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 3px;
  font-size: 16px;
}

.icon-btn-header:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.dialog-title {
  display: flex;
  align-items: center;
  gap: 8px;
}

.dialog-icon {
  color: #42526E;
  font-size: 16px;
}

.dialog-title h2 {
  margin: 0;
  font-size: 16px;
  color: #172B4D;
  font-weight: 600;
}

.jira-dialog-body {
  padding: 0 24px 16px;
}

.required-note {
  font-size: 12px;
  color: #5E6C84;
  margin: 0;
}

.required {
  color: #DE350B;
}

.jira-input {
  width: 100%;
  padding: 8px 10px;
  border: 2px solid #DFE1E6;
  border-radius: 3px;
  font-size: 14px;
  color: #172B4D;
  transition: border-color 0.2s, background-color 0.2s;
  box-sizing: border-box;
}

.jira-input:hover {
  background-color: #FAFBFC;
}

.jira-input:focus {
  border-color: #4C9AFF;
  background-color: #FFFFFF;
  outline: none;
}

.emoji-picker-control {
  display: flex;
  align-items: center;
  gap: 8px;
}

.emoji-btn {
  background-color: #FAFBFC;
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  font-size: 20px;
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.emoji-btn:hover {
  background-color: #EBECF0;
}

.refresh-emoji-btn {
  background: transparent;
  border: 1px solid transparent;
  color: #5E6C84;
  width: 32px;
  height: 32px;
  border-radius: 3px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.refresh-emoji-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.privacy-group {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
}

.privacy-info p {
  margin: 4px 0 0;
  font-size: 12px;
  color: #5E6C84;
  line-height: 1.4;
}

.toggle-switch {
  width: 32px;
  height: 16px;
  background-color: #DFE1E6;
  border-radius: 8px;
  position: relative;
  cursor: pointer;
  transition: background-color 0.2s;
  flex-shrink: 0;
}

.toggle-switch.active {
  background-color: #36B37E;
}

.toggle-knob {
  width: 12px;
  height: 12px;
  background-color: #FFFFFF;
  border-radius: 50%;
  position: absolute;
  top: 2px;
  left: 2px;
  transition: transform 0.2s;
  box-shadow: 0 1px 2px rgba(0,0,0,0.2);
}

.toggle-switch.active .toggle-knob {
  transform: translateX(16px);
}

.jira-dialog-footer {
  padding: 16px 24px;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  border-top: 1px solid #DFE1E6;
}

/* --- New Styles for Project List Layout --- */
.search-box-full {
  position: relative;
  width: 100%;
}

.search-box-full .search-icon {
  position: absolute;
  left: 12px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--color-text-muted, #5E6C84);
  font-size: 14px;
}

.search-box-full .search-input {
  width: 100%;
  height: 34px !important;
  padding-left: 36px !important;
  padding-right: 12px !important;
  border-radius: 9px !important;
  border: 1px solid var(--color-border, #DFE1E6) !important;
  background-color: var(--color-surface, #FFFFFF) !important;
  color: var(--color-text-primary, #172B4D) !important;
  font-size: 13.5px !important;
  box-sizing: border-box;
  transition: border-color 0.2s, box-shadow 0.2s, background-color 0.2s;
}

.search-box-full .search-input:hover {
  background-color: var(--color-surface-hover, #FAFBFC) !important;
}

.search-box-full .search-input:focus {
  border-color: var(--color-accent, #4C9AFF) !important;
  background-color: var(--color-surface, #FFFFFF) !important;
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15) !important;
  outline: none;
}

.mt-16 { margin-top: 16px; }
.mt-24 { margin-top: 24px; }
.ms-1 { margin-left: 4px; }

.filters-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
}

.filter-btn {
  background: transparent;
  border: none;
  color: #42526E;
  font-size: 14px;
  font-weight: 500;
  padding: 6px 12px;
  border-radius: 3px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
  transition: background-color 0.2s;
}

.filter-btn:hover {
  background: rgba(9, 30, 66, 0.08);
}

.active-filter-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background-color: rgba(9, 30, 66, 0.08);
  padding: 6px 12px;
  border-radius: 16px;
  font-size: 14px;
  color: #172B4D;
}

.chip-close {
  cursor: pointer;
  color: #5E6C84;
  margin-left: 4px;
}

.chip-close:hover {
  color: #172B4D;
}

.table-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  min-height: 42px;
  padding: 8px 10px;
  border: 1px solid color-mix(in srgb, var(--color-border) 72%, transparent);
  border-radius: 12px;
  background: linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 86%, transparent), color-mix(in srgb, var(--color-surface-hover) 46%, transparent));
  box-shadow: 0 10px 24px color-mix(in srgb, #020617 6%, transparent);
}

.results-count {
  font-size: 14px;
  color: #5E6C84;
  font-weight: 500;
}

.toolbar-actions {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
  flex: 1 1 auto;
}

.toolbar-actions .search-box-full {
  width: min(325px, 34vw);
  flex: 0 1 325px;
}

.view-toggles {
  display: flex;
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  overflow: hidden;
}

.view-toggles .icon-btn {
  border-radius: 0;
  padding: 6px 10px;
  color: #5E6C84;
  opacity: 1;
}

.view-toggles .icon-btn.active {
  background-color: rgba(9, 30, 66, 0.08);
  color: #172B4D;
}

.small-btn {
  padding: 6px 12px;
  background: transparent;
  color: #42526E;
}

.small-btn:hover {
  background: rgba(9, 30, 66, 0.08);
}

.col-status { width: 15%; }
.col-date { width: 15%; }
.col-owner { width: 10%; }
.col-following { width: 10%; }
.col-updated { width: 15%; }

.project-emoji {
  font-size: 16px;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
}

.status-on-track {
  background-color: #E3FCEF;
  color: #006644;
}

.status-done {
  background-color: #EAE6FF;
  color: #403294;
}

.status-default {
  background-color: #DFE1E6;
  color: #42526E;
}

.target-date-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 12px;
  color: #42526E;
  background-color: transparent;
}

.target-date-badge.overdue {
  color: #DE350B;
  background-color: #FFEBE6;
}

.owner-avatar-micro {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background-color: #0052CC;
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: bold;
}

.following-text, .updated-text {
  color: #5E6C84;
  font-size: 14px;
}

.empty-state-large {
  min-height: 204px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 24px 26px;
  background: transparent;
  border: 0;
  box-shadow: none;
  text-align: center;
}

.empty-spaces-icon {
  width: 54px;
  height: 54px;
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid color-mix(in srgb, var(--color-accent, #0ea5e9) 18%, transparent);
  border-radius: 14px;
  background: color-mix(in srgb, var(--color-accent, #0ea5e9) 10%, var(--color-surface, #ffffff));
  color: var(--color-accent, #0ea5e9);
  font-size: 23px;
  box-shadow: 0 14px 30px rgba(14, 165, 233, 0.12);
}

.empty-spaces-copy {
  max-width: 380px;
}

.empty-spaces-copy h3 {
  margin: 0;
  color: var(--color-text-primary, #172B4D);
  font-size: 15px;
  font-weight: 800;
  line-height: 1.35;
}

.empty-spaces-copy p {
  margin: 3px 0 0;
  color: var(--color-text-muted, #5E6C84);
  font-size: 13px;
  line-height: 1.4;
}

.empty-spaces-copy p a {
  color: var(--color-accent, #0052CC);
  text-decoration: none;
}

.empty-spaces-copy p a:hover {
  text-decoration: underline;
}

.clear-filters-btn {
  background: white;
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  padding: 6px 12px;
  font-size: 13px;
  font-weight: 500;
  color: #0052CC;
  cursor: pointer;
  transition: background 0.2s;
}
.clear-filters-btn:hover {
  background: #E6FCFF;
}

.project-card-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(420px, 1fr));
  gap: 14px;
}

.project-row-card {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 18px;
  padding: 16px;
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 14px;
  background: var(--home-panel, #ffffff);
  color: var(--home-text, #172b4d);
  cursor: pointer;
  transition: border-color 0.18s ease, transform 0.18s ease, box-shadow 0.18s ease;
}

.project-row-card:hover {
  border-color: color-mix(in srgb, var(--home-accent, #0052cc) 58%, var(--home-border, #dfe1e6));
  box-shadow: var(--home-shadow, 0 16px 40px rgba(15, 23, 42, 0.10));
  transform: translateY(-1px);
}

.project-row-main,
.project-row-meta {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.project-row-main {
  flex: 1 1 auto;
}

.project-row-meta {
  flex: 0 0 auto;
  flex-wrap: wrap;
  justify-content: flex-end;
}

.project-row-icon {
  width: 38px;
  height: 38px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 10px;
  background: color-mix(in srgb, var(--home-accent, #0052cc) 16%, var(--home-panel-strong, #f8fafc));
  font-size: 18px;
  flex: 0 0 auto;
}

.project-row-text {
  min-width: 0;
}

.project-row-text h3 {
  margin: 0 0 4px;
  color: var(--home-text, #172b4d);
  font-size: 15px;
  font-weight: 800;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.project-row-text p {
  margin: 0;
  color: var(--home-muted, #5e6c84);
  font-size: 12px;
}

.row-action-btn {
  appearance: none;
  -webkit-appearance: none;
  display: inline-flex;
  align-items: center;
  gap: 7px;
  height: 32px;
  padding: 0 10px;
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 999px;
  background: var(--home-panel-strong, #f8fafc);
  color: var(--home-text, #172b4d);
  font-size: 12px;
  font-weight: 800;
  font-family: inherit;
  cursor: pointer;
  touch-action: manipulation;
}

.row-action-btn:hover {
  border-color: color-mix(in srgb, var(--home-accent, #0052cc) 56%, var(--home-border, #dfe1e6));
  color: var(--home-accent, #0052cc);
}

.row-action-btn.icon-only {
  width: 32px;
  justify-content: center;
  padding: 0;
}

@media (max-width: 900px) {
  .project-card-list {
    grid-template-columns: 1fr;
  }

  .project-row-card {
    align-items: flex-start;
    flex-direction: column;
  }

  .project-row-meta {
    justify-content: flex-start;
  }
}

.filter-dropdown-wrapper {
  position: relative;
  display: inline-block;
}
.plane-dropdown-menu {
  position: absolute;
  top: calc(100% + 6px);
  right: 0;
  z-index: 1050;
  width: 290px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 9px;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.08);
  padding: 12px;
}
.filter-dropdown-menu {
  width: 640px;
  max-width: calc(100vw - 32px);
  max-height: none;
  padding: 8px !important;
  left: 0;
  right: auto;
  overflow: visible;
}
.filter-dropdown-menu :deep(.filter-bar-container) {
  min-height: auto;
  box-shadow: none;
  background: transparent;
  border: none;
  padding: 0 !important;
  overflow: visible;
}

.empty-spaces-btn {
  height: 36px;
  padding: 0 16px;
  border-radius: 9px;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-primary);
  font-size: 13.5px;
  font-weight: 600;
  cursor: pointer;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
  transition: all 0.2s ease;
  margin-top: 12px;
}

.empty-spaces-btn:hover {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border));
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface));
  color: var(--color-accent);
}
</style>
