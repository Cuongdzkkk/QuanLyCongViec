<script setup>
import { ref, onMounted, onBeforeUnmount, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'

import TaskDetailModal from '@/components/TaskDetailModal.vue'
import { useProjectStore } from '@/store/useProjectStore'
import { useWorkTaskStore } from '@/store/useWorkTaskStore'
import { useI18n } from '@/composables/useI18n'
import { usePersonalWork } from '@/composables/usePersonalWork'
import { PERSONAL_WORK_SCOPES } from '@/api/personalWorkApi'
import { useAuthStore } from '@/store/useAuthStore'
import { useSiteStore } from '@/store/useSiteStore'
import { useStarredStore } from '@/store/useStarredStore'
import { STARRED_ENTITY_TYPES } from '@/api/starredRecentApi'
import { translateDemoText } from '@/utils/demoContentLocale'

const route = useRoute()
const router = useRouter()
const projectStore = useProjectStore()
const workTaskStore = useWorkTaskStore()
const authStore = useAuthStore()
const siteStore = useSiteStore()
const starredStore = useStarredStore()
const { t, language } = useI18n()
const demoText = (value) => translateDemoText(value, language.value)

const currentProjectId = computed(() => route.params.id || null)

// Loading, empty & error states
const loadingSpaces = ref(false)
const errorSpaces = ref(null)

// Data refs
const spaces = ref([])
const projectMembers = ref([])
const currentProjectRole = ref('member')
const activeTab = ref('assigned') // recommended, assigned, starred, worked, viewed
const taskSearch = ref('')
const statusFilter = ref('all') // all, todo, inprogress, done
const sortOption = ref('updated') // updated, created, priority
const currentPage = ref(1)
const itemsPerPage = ref(10)

const {
  items: personalTasks,
  totalCount,
  loading: personalLoading,
  error: personalError,
  summary,
  fetchPage,
  fetchSummary,
  reset
} = usePersonalWork()

const personalTabScopes = {
  recommended: PERSONAL_WORK_SCOPES.suggested,
  assigned: PERSONAL_WORK_SCOPES.assigned,
  worked: PERSONAL_WORK_SCOPES.worked
}
const isPersonalApiTab = computed(() => Boolean(personalTabScopes[activeTab.value]))
const collectionTasks = computed(() => {
  const source = activeTab.value === 'starred' ? starredStore.starredItems : starredStore.recentItems
  return source.map(item => ({
    id: item.itemId || item.entityId,
    entityType: item.itemType || item.entityType,
    title: item.itemName || item.title || t('forYou.untitled'),
    projectId: item.projectId,
    projectName: item.subtitle,
    url: item.url,
    createdAt: item.createdAt || item.viewedAt,
    updatedAt: item.updatedAt || item.viewedAt,
    statusName: item.statusName || ''
  }))
})
const myTasks = computed(() => isPersonalApiTab.value ? personalTasks.value : collectionTasks.value)
const loadingTasks = computed(() => isPersonalApiTab.value ? personalLoading.value : (activeTab.value === 'starred' ? starredStore.loading : starredStore.recentLoading))
const errorTasks = computed(() => isPersonalApiTab.value ? personalError.value : (activeTab.value === 'starred' ? starredStore.error : starredStore.recentError))
const contextKey = computed(() => [
  authStore.userId || '',
  authStore.token || '',
  siteStore.activeSite?.id || siteStore.activeSite?.Id || ''
].join(':'))

// Task details modal interaction
const selectedTask = ref(null)
const taskDetailHistory = ref([])

const emojiList = ['📦', '🚀', '⚡', '💡', '🔥', '🎯']

// 1. Fetch Spaces/Projects
const fetchSpaces = async () => {
  loadingSpaces.value = true
  errorSpaces.value = null
  try {
    const list = await projectStore.fetchAllProjects(true)
    spaces.value = list.map((p, index) => ({
      id: p.id,
      name: p.name,
      key: p.key || p.name.substring(0, 4).toUpperCase(),
      description: p.description || t('forYou.softwareSpace'),
      cover: p.cover || '#3b82f6',
      icon: p.icon || '📦',
      taskCount: p.taskCount ?? p.TotalTasks ?? p.totalTasks ?? p.activeMemberCount ?? p.ActiveMemberCount ?? 0,
      networkType: p.networkType || 'Public',
      createdAt: p.createdAt || null,
      originalRow: p
    }))
  } catch (error) {
    errorSpaces.value = t('forYou.loadProjectsFailed')
    console.error('Fetch spaces error:', error)
  } finally {
    loadingSpaces.value = false
  }
}

// 2. Fetch User Personal Tasks
const fetchMyTasks = async () => {
  const scope = personalTabScopes[activeTab.value]
  if (activeTab.value === 'starred') {
    return starredStore.fetchStarredItems({ page: currentPage.value, pageSize: itemsPerPage.value }).catch(() => null)
  }
  if (activeTab.value === 'viewed') {
    return starredStore.fetchRecentItems({ page: currentPage.value, pageSize: itemsPerPage.value }).catch(() => null)
  }
  return fetchPage({
    scope,
    page: currentPage.value,
    pageSize: itemsPerPage.value
  }).catch(() => null)
}

// 3. Task Starring (Client-side localized)
const toggleTaskStar = async (task) => {
  const wasStarred = starredStore.isStarred(STARRED_ENTITY_TYPES.WORK_TASK, task.id)
  try {
    await starredStore.setStarred(STARRED_ENTITY_TYPES.WORK_TASK, task.id, !wasStarred)
    ElMessage.success(wasStarred ? t('forYou.taskUnstarred') : t('forYou.taskStarred'))
  } catch {
    ElMessage.error(starredStore.error || t('forYou.updateTaskStarFailed'))
  }
}

// 4. Space Starring
const toggleSpaceStar = async (space) => {
  try {
    const isCurrentlyFav = projectStore.favoriteProjects.some(p => p.id === space.id)
    await projectStore.updateFavorite(space.id, !isCurrentlyFav)
    ElMessage.success(isCurrentlyFav ? t('forYou.spaceUnstarred') : t('forYou.spaceStarred'))
  } catch {
    ElMessage.error(starredStore.error || t('forYou.updateSpaceStarFailed'))
  }
}

const forYouTabs = computed(() => [
  { id: 'recommended', label: t('forYou.recommended') },
  { id: 'assigned', label: t('forYou.assignedToMe') },
  { id: 'starred', label: t('forYou.starred') },
  { id: 'worked', label: t('forYou.workedOn') },
  { id: 'viewed', label: t('forYou.viewed') }
])

// Sorted Spaces
const sortedSpaces = computed(() => {
  return spaces.value.map(space => {
    const spaceTasks = myTasks.value.filter(t => t.projectId === space.id || (t.projectName && t.projectName === space.name)).length;
    return {
      ...space,
      displayTaskCount: spaceTasks > 0 ? spaceTasks : (space.taskCount || 0)
    }
  }).sort((a, b) => {
    const aStarred = projectStore.favoriteProjects.some(p => p.id === a.id)
    const bStarred = projectStore.favoriteProjects.some(p => p.id === b.id)
    if (aStarred !== bStarred) return aStarred ? -1 : 1
    return new Date(b.createdAt || 0) - new Date(a.createdAt || 0)
  })
})

// Filtered and Sorted Tasks
const filteredTasksList = computed(() => {
  let list = []
  
  if (isPersonalApiTab.value) {
    list = myTasks.value
  } else {
    list = myTasks.value
  }

  // Status filter
  if (statusFilter.value !== 'all') {
    const sf = statusFilter.value.toUpperCase().replace(/\s+/g, '')
    list = list.filter(task => {
      const ts = (task.statusName || 'BACKLOG').toUpperCase().replace(/\s+/g, '')
      if (sf === 'TODO') return ts.includes('TODO') || ts.includes('BACKLOG')
      if (sf === 'INPROGRESS') return ts.includes('PROGRESS') || ts.includes('REVIEW')
      if (sf === 'DONE') return ts.includes('DONE') || ts.includes('COMPLETE')
      return true
    })
  }

  // Search filter
  if (taskSearch.value.trim()) {
    const q = taskSearch.value.toLowerCase().trim()
    list = list.filter(task => 
      demoText(task.title)?.toLowerCase().includes(q) || 
      task.sequenceId?.toLowerCase().includes(q) ||
      demoText(task.projectName)?.toLowerCase().includes(q)
    )
  }

  // Sorting
  if (activeTab.value !== 'viewed') {
    list.sort((a, b) => {
      if (sortOption.value === 'created') {
        return new Date(b.createdAt || 0) - new Date(a.createdAt || 0)
      } else if (sortOption.value === 'priority') {
        return (a.priority || 3) - (b.priority || 3)
      }
      // Default: updated
      return new Date(b.updatedAt || b.createdAt || 0) - new Date(a.updatedAt || a.createdAt || 0)
    })
  }

  return list
})

// Pagination
const totalPages = computed(() => Math.max(
  1,
  Math.ceil(
    (isPersonalApiTab.value
      ? totalCount.value
      : activeTab.value === 'starred'
        ? starredStore.starredPagination.totalCount
        : starredStore.recentPagination.totalCount) /
    itemsPerPage.value
  )
))
const paginatedTasks = computed(() => {
  return filteredTasksList.value
})

// Group Tasks
const groupedTasks = computed(() => {
  // Group by Project for Assigned and Starred
  if (activeTab.value === 'assigned' || activeTab.value === 'starred') {
    const projectGroups = {}
    paginatedTasks.value.forEach(task => {
      const pName = demoText(task.projectName) || t('forYou.otherProjects')
      if (!projectGroups[pName]) projectGroups[pName] = []
      projectGroups[pName].push(task)
    })
    return Object.entries(projectGroups).map(([label, items]) => ({ label, items }))
  }

  // Group by Date for Worked, Viewed, Recommended
  const groups = {
    [t('forYou.today')]: [],
    [t('forYou.yesterday')]: [],
    [t('forYou.thisWeek')]: [],
    [t('forYou.lastMonth')]: [],
    [t('forYou.older')]: []
  }

  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const yesterday = new Date(today)
  yesterday.setDate(yesterday.getDate() - 1)
  const lastWeek = new Date(today)
  lastWeek.setDate(lastWeek.getDate() - 7)
  const lastMonth = new Date(today)
  lastMonth.setMonth(lastMonth.getMonth() - 1)

  paginatedTasks.value.forEach(task => {
    const d = new Date(task.updatedAt || task.createdAt || Date.now())
    if (d >= today) groups[t('forYou.today')].push(task)
    else if (d >= yesterday) groups[t('forYou.yesterday')].push(task)
    else if (d >= lastWeek) groups[t('forYou.thisWeek')].push(task)
    else if (d >= lastMonth) groups[t('forYou.lastMonth')].push(task)
    else groups[t('forYou.older')].push(task)
  })

  // Remove empty groups
  return Object.entries(groups).filter(([_, items]) => items.length > 0).map(([label, items]) => ({ label, items }))
})

// Task Details Dialog
const openTaskDetail = async (task) => {
  if (task.url && (task.entityType === STARRED_ENTITY_TYPES.PROJECT || !task.projectId)) {
    await router.push(task.url)
    return
  }
  
  try {
    const mRes = await axiosClient.get(`/projects/${task.projectId}/members`)
    projectMembers.value = (mRes.data?.data || []).map(member => ({
      ...member,
      userId: member.userId || member.id,
      fullName: member.fullName || member.name || member.email,
      projectRole: member.projectRole || member.ProjectRole || member.myRole || member.MyRole || ''
    }))
  } catch (err) {
    projectMembers.value = []
  }

  taskDetailHistory.value = []
  selectedTask.value = workTaskStore.normalizeTaskRecord(task, task.projectId)
  starredStore.recordViewed(STARRED_ENTITY_TYPES.WORK_TASK, task.id).catch(() => {})
}

const openTaskDetailFromModal = (task, options = {}) => {
  const previousTask = options?.fromTask || selectedTask.value
  if (previousTask?.id && previousTask.id !== task?.id) {
    const cachedPrevious = myTasks.value.find(item => item.id === previousTask.id) || previousTask
    taskDetailHistory.value = [...taskDetailHistory.value, cachedPrevious]
  }
  selectedTask.value = workTaskStore.normalizeTaskRecord(task, task.projectId)
}

const goBackTaskDetail = () => {
  const history = [...taskDetailHistory.value]
  const previousTask = history.pop()
  if (!previousTask) return
  taskDetailHistory.value = history
  selectedTask.value = myTasks.value.find(item => item.id === previousTask.id) || previousTask
}

const closeTaskDetail = () => {
  taskDetailHistory.value = []
  selectedTask.value = null
}

const updateTask = async (task, field, value) => {
  if (!task?.id) return
  try {
    const updatePayload = { [field]: value }
    const usesPutUpdate = ['title', 'description', 'priority', 'assignedUserId'].includes(field)
    
    let payload = updatePayload
    if (usesPutUpdate) {
      payload = {
        title: task.title || '',
        description: task.description || '',
        priority: task.priority || 3,
        assignedUserId: task.assignedUserId || null,
        statusName: task.statusName || 'TO DO',
        sprintId: task.sprintId || null,
        plannedStartDate: task.plannedStartDate || null,
        plannedEndDate: task.plannedEndDate || null,
        dueDate: task.dueDate || null,
        ...updatePayload
      }
    }

    if (usesPutUpdate) {
      await axiosClient.put(`/projects/${task.projectId}/WorkTasks/${task.id}`, payload)
    } else {
      await axiosClient.patch(`/projects/${task.projectId}/WorkTasks/${task.id}`, payload)
    }

    const idx = myTasks.value.findIndex(item => item.id === task.id)
    if (idx !== -1) myTasks.value[idx][field] = value
    if (selectedTask.value?.id === task.id) selectedTask.value[field] = value
  } catch (error) {
    ElMessage.error('Could not update work item')
  }
}

const getTaskIcon = (task) => {
  const ts = (task.statusName || '').toUpperCase()
  if (ts.includes('DONE')) return 'fa-solid fa-square-check text-green-500'
  return 'fa-solid fa-square-check text-blue-500'
}

const isTaskStarred = (taskId) => workTaskStore.isTaskStarred(taskId)

const timeAgo = (dateStr) => {
  if (!dateStr || dateStr.startsWith('0001-01-01')) return t('forYou.justNow')
  const date = new Date(dateStr)
  if (isNaN(date.getTime()) || date.getFullYear() <= 1970) return t('forYou.justNow')
  const seconds = Math.floor((new Date() - date) / 1000)
  if (seconds < 0) return t('forYou.justNow')

  let interval = seconds / 31536000
  if (interval >= 1) return t('forYou.yearsAgo', { count: Math.floor(interval) })
  interval = seconds / 2592000
  if (interval >= 1) return t('forYou.monthsAgo', { count: Math.floor(interval) })
  interval = seconds / 86400
  if (interval >= 1) return t('forYou.daysAgo', { count: Math.floor(interval) })
  interval = seconds / 3600
  if (interval >= 1) return t('forYou.hoursAgo', { count: Math.floor(interval) })
  interval = seconds / 60
  if (interval >= 1) return t('forYou.minutesAgo', { count: Math.floor(interval) })
  return t('forYou.justNow')
}

onMounted(() => {
  fetchSpaces()
  starredStore.fetchStarredItems().catch(() => {})
})

watch(() => route.query.tab, (tab) => {
  activeTab.value = tab || 'worked'
}, { immediate: true })

watch(activeTab, () => {
  if (currentPage.value !== 1) {
    currentPage.value = 1
    return
  }
  fetchMyTasks()
})

watch(currentPage, () => {
  fetchMyTasks()
})

watch(contextKey, () => {
  reset()
  currentPage.value = 1
  if (!authStore.token || !authStore.userId) return
  fetchSummary().catch(() => null)
  fetchMyTasks()
}, { immediate: true })

onBeforeUnmount(() => {
  reset()
})

</script>

<template>
  <div>
    <div class="jira-dashboard">
      
      <!-- Main Content Area (Left Column) -->
      <div class="main-content-column">
        
        <!-- Recommended Spaces Section -->
        <section class="mb-10 mt-2">
          <div class="section-header flex-between mb-4">
            <h2 class="section-title">{{ t('forYou.recommendedSpaces') }}</h2>
            <a href="/spaces" class="view-all-link">{{ t('forYou.viewAllSpaces') }}</a>
          </div>

          <div v-if="loadingSpaces" class="loading-state">
            <i class="fa-solid fa-spinner fa-spin"></i> {{ t('forYou.loadingSpaces') }}
          </div>
          
          <!-- Premium Empty State for Spaces -->
          <div v-else-if="sortedSpaces.length === 0" class="empty-spaces-card">
            <div class="esc-icon">
              <svg class="h-10 w-10 text-blue-500/80" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 12.75V12A2.25 2.25 0 014.5 9.75h15A2.25 2.25 0 0121.75 12v.75m-8.69-6.44l-2.12-2.12a1.5 1.5 0 00-1.061-.44H4.5A2.25 2.25 0 002.25 6v12a2.25 2.25 0 002.25 2.25h15A2.25 2.25 0 0021.75 18V9a2.25 2.25 0 00-2.25-2.25h-5.379a1.5 1.5 0 01-1.06-.44z" />
              </svg>
            </div>
            <div class="esc-text">
              <h3 class="text-sm font-semibold text-gray-700 dark:text-neutral-300">{{ t('forYou.noActiveSpaces') }}</h3>
              <p class="text-xs text-gray-500 dark:text-neutral-500 mt-0.5">{{ t('forYou.noActiveSpacesDesc') }}</p>
            </div>
          </div>
          
          <div v-else class="spaces-row">
            <div 
              v-for="space in sortedSpaces.slice(0, 4)" 
              :key="space.id" 
              class="space-card"
              @click="router.push(`/space/${space.id}`)"
            >
              <div class="sc-icon-wrapper" :style="{ backgroundColor: space.cover }">
                <span class="sc-emoji">{{ space.icon || '📦' }}</span>
              </div>
              <div class="sc-info">
                <h3 class="sc-name" :title="demoText(space.name)">{{ demoText(space.name) }}</h3>
                <p class="sc-desc">{{ demoText(space.description) }} - {{ space.displayTaskCount }} {{ t('common.tasks') }}</p>
              </div>
              <!-- Star button for Space -->
              <button 
                class="sc-star-btn"
                :class="{ starred: projectStore.favoriteProjects.some(p => p.id === space.id) }"
                @click.stop="toggleSpaceStar(space)"
                :title="projectStore.favoriteProjects.some(p => p.id === space.id) ? 'Bỏ đánh dấu' : 'Đánh dấu'"
              >
                <i :class="projectStore.favoriteProjects.some(p => p.id === space.id) ? 'fa-solid fa-star' : 'fa-regular fa-star'"></i>
              </button>
            </div>
          </div>
        </section>

        <!-- For You Section -->
        <section>
          <div class="foryou-header-row mb-6">
            <h2 class="section-title text-2xl font-bold">{{ t('forYou.forYou') }}</h2>
            
            <div class="jira-tabs">
              <button 
                v-for="tab in forYouTabs"
                :key="tab.id"
                class="j-tab"
                :class="{ 'active': activeTab === tab.id }"
                @click="activeTab = tab.id"
              >
                <span>{{ tab.label }}</span>
                <span 
                  v-if="tab.id === 'assigned' && summary?.assigned > 0"
                  class="tab-badge"
                >
                  {{ summary.assigned }}
                </span>
              </button>
            </div>
          </div>

          <!-- Toolbar: Search, Filter, Sort -->
          <div class="task-toolbar mb-6">
            <div class="search-input">
              <i class="fa-solid fa-magnifying-glass"></i>
              <input type="text" v-model="taskSearch" :placeholder="t('forYou.searchTasksPlaceholder')" />
            </div>
            <select v-model="statusFilter" class="jira-select">
              <option value="all">{{ t('forYou.allStatuses') }}</option>
              <option value="todo">{{ t('forYou.toDo') }}</option>
              <option value="inprogress">{{ t('forYou.inProgress') }}</option>
              <option value="done">{{ t('forYou.done') }}</option>
            </select>
            <select v-model="sortOption" class="jira-select">
              <option value="updated">{{ t('forYou.recentlyUpdated') }}</option>
              <option value="created">{{ t('forYou.recentlyCreated') }}</option>
              <option value="priority">{{ t('forYou.priority') }}</option>
            </select>
          </div>

          <div v-if="loadingTasks" class="loading-state">
            <i class="fa-solid fa-spinner fa-spin"></i> {{ t('forYou.loadingYourWork') }}
          </div>

          <div v-else-if="errorTasks" class="empty-state">
            <h3 class="text-sm font-semibold text-gray-700 dark:text-neutral-300">{{ t('forYou.loadTasksFailed') }}</h3>
            <button class="jira-btn-subtle mt-4" @click="fetchMyTasks">{{ t('common.retry') }}</button>
          </div>
          
          <!-- Premium Empty State for Tasks -->
          <div v-else-if="filteredTasksList.length === 0" class="empty-state">
            <div class="empty-state-illustration">
              <svg class="mx-auto h-16 w-16 text-blue-500/20 dark:text-blue-400/10 mb-4 animate-pulse" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4" />
              </svg>
            </div>
            <h3 class="text-sm font-semibold text-gray-700 dark:text-neutral-300">{{ t('forYou.allCaughtUp') }}</h3>
            <p class="text-xs text-gray-500 dark:text-neutral-500 mt-1 max-w-xs mx-auto">
              {{ t('forYou.noMatchingWorkItems') }}
            </p>
          </div>

          <!-- Task Groups -->
          <div v-else class="task-groups-container">
            <div v-for="group in groupedTasks" :key="group.label" class="task-group mb-6">
              <div class="group-header mb-3">
                <h4 class="group-label">{{ group.label }}</h4>
              </div>

              <div class="task-list">
                <div 
                  v-for="task in group.items" 
                  :key="task.id" 
                  class="jira-task-row"
                  @click="openTaskDetail(task)"
                >
                  <div class="jtr-left">
                    <i :class="getTaskIcon(task)" class="task-type-icon"></i>
                  </div>
                  <div class="jtr-center">
                    <div class="jtr-title" :class="{ 'line-through text-gray-400 dark:text-neutral-500': task.statusName === 'DONE' }">
                      {{ demoText(task.title) }}
                    </div>
                    <div class="jtr-subtitle">
                      {{ t('common.task') }} - {{ task.sequenceId || 'DTN-5' }} - {{ demoText(task.projectName) || t('common.project') }}
                    </div>
                  </div>
                  <div class="jtr-actions" @click.stop>
                    <button
                      class="star-btn"
                      type="button"
                      :class="{ starred: isTaskStarred(task.id) }"
                      :disabled="starredStore.isPending(STARRED_ENTITY_TYPES.WORK_TASK, task.id)"
                      :aria-label="isTaskStarred(task.id) ? t('forYou.taskUnstarred') : t('forYou.taskStarred')"
                      @click.stop="toggleTaskStar(task)"
                    >
                      <i :class="isTaskStarred(task.id) ? 'fa-solid fa-star text-yellow-400' : 'fa-regular fa-star'"></i>
                    </button>
                  </div>
                  <div class="jtr-right">
                    <span class="time-text">{{ timeAgo(task.createdAt) }}</span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Pagination -->
            <div class="pagination flex justify-center items-center gap-4 mt-6" v-if="totalPages > 1">
              <button class="jira-btn-subtle" :disabled="currentPage === 1" @click="currentPage--"><i class="fa-solid fa-chevron-left"></i></button>
              <span class="text-sm text-gray-500">{{ t('common.pageOf', { current: currentPage, total: totalPages }) }}</span>
              <button class="jira-btn-subtle" :disabled="currentPage === totalPages" @click="currentPage++"><i class="fa-solid fa-chevron-right"></i></button>
            </div>
          </div>

        </section>
      </div>



      <!-- Task Detail Modal integration -->
      <TaskDetailModal 
        v-if="selectedTask"
        :selectedTask="selectedTask"
        :projectId="selectedTask.projectId"
        :projectMembers="projectMembers"
        :currentProjectRole="currentProjectRole"
        :canGoBack="taskDetailHistory.length > 0"
        @close="closeTaskDetail"
        @back="goBackTaskDetail"
        @open-task="openTaskDetailFromModal"
        @updateTask="updateTask"
        @refresh-tasks="fetchMyTasks"
      />

    </div>
  </div>
</template>

<style scoped>
.jira-dashboard {
  max-width: 1400px;
  margin: 0 auto;
  padding: 24px 32px;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Noto Sans", Ubuntu, "Droid Sans", "Helvetica Neue", sans-serif;
  color: var(--color-text-primary, #172b4d);
  display: flex;
  flex-direction: column;
  gap: 24px;
}

@media (min-width: 1200px) {
  .jira-dashboard {
    flex-direction: row;
    align-items: flex-start;
  }
}

.main-content-column {
  flex: 1;
  min-width: 0;
}

.section-title {
  font-size: 20px;
  font-weight: 600;
  margin: 0;
  color: var(--color-text-primary, #172b4d);
}

.view-all-link {
  font-size: 14px;
  color: var(--color-accent, #0c66e4);
  text-decoration: none;
  font-weight: 500;
  transition: color 0.15s ease;
}
.view-all-link:hover {
  color: var(--color-accent-hover, #0052cc);
  text-decoration: underline;
}

/* Spaces Row */
.spaces-row {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 16px;
}

.space-card {
  display: flex;
  align-items: center;
  background: var(--color-surface, #ffffff);
  border: 1px solid rgba(9, 30, 66, 0.06);
  border-radius: 16px;
  padding: 16px 20px;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.03);
  transition: all 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
  position: relative;
  overflow: hidden;
}

.space-card:hover {
  background-color: var(--color-surface-hover, #f4f5f7);
  border-color: rgba(9, 30, 66, 0.12);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.06);
  transform: translateY(-2px);
}

.sc-icon-wrapper {
  width: 38px;
  height: 38px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-right: 12px;
  flex-shrink: 0;
}

.sc-emoji {
  font-size: 18px;
}

.sc-info {
  flex: 1;
  min-width: 0;
  padding-right: 24px;
}

.sc-name {
  font-size: 14px;
  font-weight: 600;
  margin: 0 0 2px 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  color: var(--color-text-primary, #172b4d);
}

.sc-desc {
  font-size: 12px;
  color: var(--color-text-muted, #6b778c);
  margin: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.sc-star-btn {
  background: transparent;
  border: none;
  cursor: pointer;
  color: var(--color-text-muted, #6b778c);
  font-size: 14px;
  padding: 6px;
  border-radius: 6px;
  opacity: 1;
  transition: all 0.2s ease;
  flex-shrink: 0;
  position: absolute;
  right: 10px;
  top: 50%;
  transform: translateY(-50%);
}
.space-card:hover .sc-star-btn {
  opacity: 1;
}
.sc-star-btn.starred {
  opacity: 1;
  color: #f5cd47;
}
.sc-star-btn:hover {
  color: var(--color-text-primary, #172b4d);
  background: rgba(9, 30, 66, 0.08);
}

/* Premium Space Empty Card */
.empty-spaces-card {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 20px;
  background: var(--color-surface);
  border: 1px dashed var(--color-border);
  border-radius: 12px;
}
.esc-icon {
  background: var(--color-surface-hover);
  padding: 10px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
}
.esc-text h3 {
  margin: 0;
  font-size: 14px;
}
.esc-text p {
  margin: 0;
}

/* For You Section Header */
.foryou-header-row {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  flex-wrap: wrap;
  gap: 16px;
  border-bottom: 2px solid var(--color-border, #dfe1e6);
  padding-bottom: 0px;
  margin-top: 16px;
}

.jira-tabs {
  display: flex;
  gap: 4px;
}

.j-tab {
  background: transparent;
  border: none;
  padding: 10px 16px;
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text-secondary, #42526e);
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 6px;
  position: relative;
  outline: none;
}

.j-tab::after {
  content: '';
  position: absolute;
  bottom: -2px;
  left: 0;
  right: 0;
  height: 2px;
  background: transparent;
  transition: background-color 0.2s ease;
}

.j-tab:hover {
  color: var(--color-text-primary, #172b4d);
}

.j-tab.active {
  color: var(--color-accent, #0c66e4);
  font-weight: 600;
}

.j-tab.active::after {
  background-color: var(--color-accent, #0c66e4);
}

.tab-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 18px;
  height: 18px;
  padding: 0 5px;
  font-size: 11px;
  font-weight: 600;
  background-color: var(--color-accent, #0c66e4);
  color: white;
  border-radius: 9px;
  line-height: 1;
}

/* Toolbar */
.task-toolbar {
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
  align-items: center;
}

.task-toolbar .jira-select:first-of-type {
  margin-left: auto; /* Pushes filters to the right */
}

.search-input {
  position: relative;
  display: flex;
  align-items: center;
  flex: 1;
  min-width: 260px;
  max-width: 320px;
}
.search-input i {
  position: absolute;
  left: 12px;
  color: var(--color-text-muted, #6b778c);
  font-size: 13px;
  pointer-events: none;
}
.search-input input {
  width: 100%;
  box-sizing: border-box !important;
  border: 1px solid rgba(9, 30, 66, 0.08) !important;
  border-radius: 20px !important;
  padding: 8px 16px 8px 36px !important;
  font-size: 14px !important;
  height: 40px !important;
  background-color: var(--color-surface, #ffffff) !important;
  color: var(--color-text-primary, #172b4d) !important;
  transition: all 0.3s ease;
  outline: none;
}
.search-input input:focus {
  border-color: var(--color-accent, #4c9aff) !important;
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-accent, #0c66e4) 15%, transparent) !important;
  background-color: var(--color-surface, #ffffff) !important;
}

.jira-select {
  box-sizing: border-box !important;
  border: 1px solid rgba(9, 30, 66, 0.08) !important;
  border-radius: 20px !important;
  padding: 0 16px !important;
  font-size: 14px !important;
  height: 40px !important;
  background-color: var(--color-surface, #ffffff) !important;
  color: var(--color-text-primary, #172b4d) !important;
  outline: none;
  cursor: pointer;
  transition: all 0.3s ease;
  min-width: 140px;
  appearance: none;
  background-image:
    linear-gradient(45deg, transparent 50%, var(--color-text-secondary, #64748b) 50%),
    linear-gradient(135deg, var(--color-text-secondary, #64748b) 50%, transparent 50%);
  background-position:
    calc(100% - 18px) 17px,
    calc(100% - 12px) 17px;
  background-repeat: no-repeat;
  background-size: 6px 6px, 6px 6px;
  padding-right: 38px !important;
}

.jira-select option {
  background: var(--color-surface, #ffffff);
  color: var(--color-text-primary, #172b4d);
  font-weight: 700;
}
.jira-select:focus {
  border-color: var(--color-accent, #4c9aff) !important;
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-accent, #0c66e4) 15%, transparent) !important;
}

/* Loading & Empty States */
.loading-state {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 40px 0;
  font-size: 14px;
  color: var(--color-text-muted, #6b778c);
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 48px 16px;
  text-align: center;
  border: 1px dashed var(--color-border, #dfe1e6);
  border-radius: 16px;
  background: rgba(9, 30, 66, 0.02);
}

.empty-state-illustration svg {
  animation: float 4s ease-in-out infinite;
}

/* Task Groups */
.group-label {
  font-size: 11px;
  text-transform: uppercase;
  color: var(--color-text-muted, #6b778c);
  font-weight: 600;
  margin: 0;
  letter-spacing: 0.5px;
}

/* Task List */
.task-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.jira-task-row {
  display: flex;
  align-items: center;
  padding: 16px 20px;
  background: var(--color-surface, #ffffff);
  border: 1px solid rgba(9, 30, 66, 0.06);
  border-radius: 12px;
  cursor: pointer;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.02);
  transition: all 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
  overflow: hidden;
}

.jira-task-row:hover {
  background-color: var(--color-surface-hover, #f4f5f7);
  border-color: rgba(9, 30, 66, 0.12);
  transform: translateX(4px);
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.05);
}

.jtr-left {
  margin-right: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.task-type-icon {
  font-size: 16px;
  border-radius: 3px;
}

.jtr-center {
  flex: 1;
  min-width: 0;
}

.jtr-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text-primary, #172b4d);
  margin-bottom: 2px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.jtr-subtitle {
  font-size: 12px;
  color: var(--color-text-muted, #6b778c);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.jtr-actions {
  opacity: 0;
  transition: opacity 0.2s ease;
  margin-right: 16px;
}
.jira-task-row:hover .jtr-actions {
  opacity: 1;
}

.star-btn {
  background: transparent;
  border: none;
  cursor: pointer;
  color: var(--color-text-muted, #6b778c);
  font-size: 14px;
  padding: 4px;
  border-radius: 4px;
  transition: color 0.15s ease;
}
.star-btn:hover {
  color: var(--color-text-primary, #172b4d);
  background: rgba(9, 30, 66, 0.04);
}
.star-btn.starred {
  opacity: 1 !important;
  color: #f5cd47;
}

.jtr-right {
  min-width: 100px;
  text-align: right;
  flex-shrink: 0;
}

.time-text {
  font-size: 12px;
  color: var(--color-text-muted, #6b778c);
}

.jira-btn-subtle {
  background: transparent;
  border: none;
  cursor: pointer;
  padding: 8px 16px;
  border-radius: 8px;
  color: var(--color-text-secondary, #42526e);
  transition: all 0.2s ease;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
.jira-btn-subtle:hover:not(:disabled) {
  background: rgba(9, 30, 66, 0.08);
  color: var(--color-text-primary);
}
.jira-btn-subtle:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.flex-between { display: flex; justify-content: space-between; align-items: center; }
.flex-center { display: flex; align-items: center; }

/* Right Sidebar styling */
.jira-right-sidebar {
  width: 320px;
  flex-shrink: 0;
  display: none;
}

@media (min-width: 1200px) {
  .jira-right-sidebar {
    display: block;
    padding-left: 24px;
    border-left: 1px solid var(--color-border, #dfe1e6);
  }
}

.sidebar-widget {
  background: var(--color-surface, #ffffff);
  border: 1px solid var(--color-border, #dfe1e6);
  border-radius: 16px;
  overflow: hidden;
  box-shadow: var(--shadow-sm);
  display: flex;
  flex-direction: column;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.sidebar-widget:hover {
  transform: translateY(-4px);
  box-shadow: var(--shadow-md);
}

.widget-banner {
  background: linear-gradient(135deg, #0747a6 0%, #0052cc 100%);
  padding: 24px 20px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.widget-body {
  padding: 20px;
}

.widget-title {
  font-size: 16px;
  font-weight: 700;
  margin: 0 0 2px 0;
  color: var(--color-text-primary, #172b4d);
}

.widget-subtitle {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-accent, #0c66e4);
  margin: 0 0 12px 0;
}

.widget-desc {
  font-size: 12.5px;
  line-height: 1.6;
  color: var(--color-text-secondary, #42526e);
  margin: 0 0 20px 0;
}

.widget-actions {
  display: flex;
  gap: 16px;
  align-items: center;
}

.btn-primary-sm {
  background: var(--color-accent, #0c66e4);
  color: white;
  border: none;
  padding: 8px 16px;
  font-size: 12.5px;
  font-weight: 600;
  border-radius: 8px;
  cursor: pointer;
  transition: background 0.15s;
}
.btn-primary-sm:hover {
  background: var(--color-accent-hover, #0052cc);
}

.btn-link-sm {
  background: transparent;
  border: none;
  color: var(--color-accent, #0c66e4);
  font-size: 12.5px;
  font-weight: 600;
  cursor: pointer;
  transition: color 0.15s ease;
}
.btn-link-sm:hover {
  color: var(--color-accent-hover, #0052cc);
  text-decoration: underline;
}

.widget-illustration {
  background: var(--color-surface-hover, #f4f5f7);
  padding: 24px 16px;
  display: flex;
  justify-content: center;
  border-top: 1px solid var(--color-border, #dfe1e6);
}

.illustration-box {
  position: relative;
  width: 180px;
  height: 90px;
}

.ill-circle {
  position: absolute;
  border-radius: 50%;
  opacity: 0.12;
}

.ill-c1 {
  width: 60px;
  height: 60px;
  background: var(--color-accent, #0c66e4);
  left: 20px;
  top: 10px;
}

.ill-c2 {
  width: 40px;
  height: 40px;
  background: #00b8d9;
  right: 30px;
  bottom: 10px;
}

.ill-card {
  position: absolute;
  background: var(--color-surface, #ffffff);
  border: 1px solid var(--color-border, #dfe1e6);
  border-radius: 8px;
  padding: 8px 10px;
  box-shadow: 0 4px 10px rgba(9, 30, 66, 0.08);
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.ill-card1 {
  width: 90px;
  left: 10px;
  top: 25px;
  transform: rotate(-4deg);
  animation: float 4s ease-in-out infinite;
}

.ill-card2 {
  width: 80px;
  right: 15px;
  top: 15px;
  transform: rotate(6deg);
  animation: float-reverse 4s ease-in-out infinite;
}

.ill-line {
  height: 4px;
  background: var(--color-border, #dfe1e6);
  border-radius: 2px;
}

.ill-line.w-16 { width: 100%; }
.ill-line.w-8 { width: 50%; background: var(--color-accent, #0c66e4); }
.ill-line.w-12 { width: 75%; }
.ill-line.w-10 { width: 60%; background: #00b8d9; }

@keyframes float {
  0% { transform: translateY(0px) rotate(-4deg); }
  50% { transform: translateY(-6px) rotate(-3deg); }
  100% { transform: translateY(0px) rotate(-4deg); }
}

@keyframes float-reverse {
  0% { transform: translateY(0px) rotate(6deg); }
  50% { transform: translateY(6px) rotate(5deg); }
  100% { transform: translateY(0px) rotate(6deg); }
}

/* SprintA dashboard refresh */
.jira-dashboard {
  position: relative;
  width: 100%;
  max-width: 1120px;
  min-height: calc(100vh - 60px);
  margin: 0 auto;
  padding: 32px clamp(22px, 3vw, 44px) 56px;
  display: flex;
  flex-direction: column !important;
  background:
    radial-gradient(circle at 18% 0%, rgba(14, 165, 233, 0.12), transparent 30%),
    radial-gradient(circle at 86% 6%, rgba(34, 197, 94, 0.08), transparent 26%),
    linear-gradient(180deg, #f8fbff 0%, #eef5fb 54%, #f8fafc 100%);
}

.main-content-column {
  display: grid;
  gap: 28px;
  width: 100%;
  max-width: 100%;
}

.main-content-column > section {
  margin: 0 !important;
}

.main-content-column > section:first-child {
  position: relative;
  overflow: hidden;
  padding: 26px;
  border: 1px solid rgba(125, 211, 252, 0.34);
  border-radius: 22px;
  background:
    linear-gradient(135deg, rgba(240, 249, 255, 0.94), rgba(255, 255, 255, 0.88)),
    #ffffff;
  box-shadow: 0 18px 46px rgba(15, 23, 42, 0.07);
}

.main-content-column > section:first-child::after {
  content: "";
  position: absolute;
  right: 24px;
  top: 20px;
  width: 86px;
  height: 86px;
  border-radius: 24px;
  background: linear-gradient(135deg, #bae6fd, #dcfce7);
  opacity: 0.62;
  transform: rotate(10deg);
  z-index: 0;
  pointer-events: none;
}

.main-content-column > section:first-child > * {
  position: relative;
  z-index: 1;
}

.section-title {
  color: #0f172a;
  font-size: clamp(22px, 1.7vw, 30px);
  font-weight: 900;
  letter-spacing: 0;
  line-height: 1.12;
  text-wrap: balance;
}

.view-all-link {
  color: #0284c7;
  font-weight: 800;
}

.view-all-link:hover {
  color: #0369a1;
  text-decoration: none;
}

.spaces-row {
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
}

.spaces-row .space-card:nth-child(4) {
  grid-column: span 1;
}

.space-card {
  min-height: 74px;
  border-color: rgba(148, 163, 184, 0.22);
  border-radius: 16px;
  padding: 14px 16px;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.94)),
    #ffffff;
  box-shadow: 0 12px 26px rgba(15, 23, 42, 0.055);
}

.space-card::before {
  content: "";
  position: absolute;
  inset: 0 auto 0 0;
  width: 3px;
  background: linear-gradient(180deg, #22d3ee, #34d399);
}

.space-card:hover {
  background: #ffffff;
  border-color: rgba(14, 165, 233, 0.36);
  box-shadow: 0 16px 34px rgba(15, 23, 42, 0.09);
  transform: translateY(-1px);
}

.sc-icon-wrapper {
  width: 40px;
  height: 40px;
  border-radius: 12px;
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.46);
}

.sc-name {
  font-size: 15px;
  font-weight: 900;
  line-height: 1.15;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sc-desc {
  color: #64748b;
  font-weight: 600;
  font-size: 12px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sc-info {
  min-width: 0;
  overflow: hidden;
}

.sc-star-btn {
  flex: 0 0 auto;
}

.foryou-header-row {
  display: grid;
  grid-template-columns: minmax(190px, auto) minmax(0, 1fr);
  align-items: center;
  gap: 16px;
  border-bottom: 0;
  padding: 0;
}

.jira-tabs {
  justify-self: end;
  gap: 6px;
  padding: 6px;
  border: 1px solid rgba(148, 163, 184, 0.2);
  border-radius: 16px;
  background: rgba(255, 255, 255, 0.72);
  min-height: 50px;
  align-items: center;
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.72);
  width: max-content;
  max-width: 100%;
  overflow-x: auto;
  scrollbar-width: none;
}

.jira-tabs::-webkit-scrollbar {
  display: none;
}

.j-tab {
  box-sizing: border-box;
  justify-content: center;
  flex: 0 0 auto;
  width: auto;
  min-width: max-content;
  height: 38px;
  min-height: 38px;
  border-radius: 11px;
  padding: 0 18px;
  color: #475569;
  font-weight: 800;
  transform: none !important;
  line-height: 1;
  white-space: nowrap;
  box-shadow: none !important;
  overflow: visible;
}

.j-tab span:first-child {
  min-width: max-content;
  overflow: visible;
  text-overflow: clip;
}

.j-tab::after {
  display: none;
}

.j-tab.active {
  color: #0369a1;
  background: linear-gradient(135deg, rgba(14, 165, 233, 0.14), rgba(34, 197, 94, 0.08));
  font-weight: 800;
}

.tab-badge {
  background: linear-gradient(135deg, #38bdf8, #2563eb);
  font-weight: 900;
}

.task-toolbar {
  display: grid;
  grid-template-columns: minmax(280px, 1fr) 170px 180px;
  gap: 12px;
  padding: 12px;
  border: 1px solid rgba(148, 163, 184, 0.18);
  border-radius: 18px;
  background: rgba(255, 255, 255, 0.72);
  box-shadow: 0 14px 36px rgba(15, 23, 42, 0.05);
  overflow: hidden;
}

.task-toolbar .jira-select:first-of-type {
  margin-left: 0;
}

.search-input {
  width: 100%;
  max-width: none;
  min-width: 0;
  position: relative;
  z-index: 0;
}

.search-input input,
.jira-select {
  border-color: rgba(148, 163, 184, 0.24) !important;
  border-radius: 13px !important;
  background-color: rgba(255, 255, 255, 0.94) !important;
  font-weight: 700;
  color-scheme: light;
}

.search-input input {
  height: 42px !important;
  min-width: 0;
  padding-left: 38px !important;
}

.jira-select {
  height: 42px !important;
}

.group-label {
  color: #0284c7;
  font-weight: 900;
  letter-spacing: 0.08em;
}

.task-list {
  gap: 10px;
}

.jira-task-row {
  border-color: rgba(148, 163, 184, 0.2);
  border-radius: 18px;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.92)),
    #ffffff;
  box-shadow: 0 10px 26px rgba(15, 23, 42, 0.055);
  min-height: 70px;
  transform: none !important;
}

.jira-task-row:hover {
  border-color: rgba(14, 165, 233, 0.3);
  box-shadow: 0 20px 44px rgba(15, 23, 42, 0.1);
  transform: none;
}

.j-tab:hover,
.j-tab:active,
.j-tab:focus,
.j-tab.active {
  transform: none !important;
  height: 38px;
  min-height: 38px;
  flex-basis: 118px;
}

.jira-task-row:active,
.space-card:active,
.task-toolbar button:active,
.task-toolbar select:active {
  transform: none !important;
}

.task-type-icon {
  color: #38bdf8;
}

.jtr-title {
  color: #0f172a;
  font-weight: 900;
  font-size: 14px;
  line-height: 1.25;
}

.jtr-subtitle,
.time-text {
  color: #64748b;
  font-weight: 600;
  font-size: 12px;
}

.star-btn {
  border-radius: 10px;
}

[data-theme='dark'] .jira-dashboard {
  background:
    radial-gradient(circle at 14% 0%, rgba(14, 165, 233, 0.13), transparent 32%),
    radial-gradient(circle at 88% 4%, rgba(34, 197, 94, 0.07), transparent 26%),
    linear-gradient(180deg, #08111f, #0f172a 52%, #111827);
}

[data-theme='dark'] .main-content-column > section:first-child,
[data-theme='dark'] .task-toolbar,
[data-theme='dark'] .jira-tabs {
  border-color: rgba(148, 163, 184, 0.2);
  background:
    linear-gradient(135deg, rgba(14, 165, 233, 0.10), transparent 48%),
    rgba(15, 23, 42, 0.84);
  box-shadow: 0 18px 46px rgba(0, 0, 0, 0.22);
}

[data-theme='dark'] .section-title,
[data-theme='dark'] .sc-name,
[data-theme='dark'] .jtr-title {
  color: #f8fafc;
}

[data-theme='dark'] .view-all-link,
[data-theme='dark'] .group-label {
  color: #38bdf8;
}

[data-theme='dark'] .space-card,
[data-theme='dark'] .jira-task-row {
  border-color: rgba(148, 163, 184, 0.18);
  background:
    linear-gradient(180deg, rgba(30, 41, 59, 0.88), rgba(15, 23, 42, 0.94)),
    #0f172a;
  box-shadow:
    0 12px 32px rgba(0, 0, 0, 0.22),
    inset 0 1px 0 rgba(255, 255, 255, 0.05);
}

[data-theme='dark'] .space-card:hover,
[data-theme='dark'] .jira-task-row:hover {
  background:
    linear-gradient(180deg, rgba(30, 41, 59, 0.98), rgba(15, 23, 42, 0.94)),
    #111827;
  border-color: rgba(56, 189, 248, 0.3);
}

[data-theme='dark'] .sc-desc,
[data-theme='dark'] .jtr-subtitle,
[data-theme='dark'] .time-text {
  color: #94a3b8;
}

[data-theme='dark'] .search-input input,
[data-theme='dark'] .jira-select {
  border-color: rgba(148, 163, 184, 0.22) !important;
  background-color: rgba(15, 23, 42, 0.82) !important;
  color: #e2e8f0 !important;
  background-image:
    linear-gradient(45deg, transparent 50%, #94a3b8 50%),
    linear-gradient(135deg, #94a3b8 50%, transparent 50%);
  color-scheme: dark;
}

[data-theme='dark'] .search-input input {
  background-image: none !important;
}

[data-theme='dark'] .jira-select option {
  background: #0f172a;
  color: #e2e8f0;
}

[data-theme='dark'] .j-tab {
  color: #94a3b8;
}

[data-theme='dark'] .j-tab.active {
  color: #7dd3fc;
  background: linear-gradient(135deg, rgba(56, 189, 248, 0.18), rgba(14, 165, 233, 0.08));
}

@media (max-width: 1180px) {
  .spaces-row {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .foryou-header-row {
    grid-template-columns: 1fr;
  }

  .jira-tabs {
    justify-self: stretch;
    overflow-x: auto;
  }
}

@media (max-width: 760px) {
  .jira-dashboard {
    padding: 24px 16px 44px;
  }

  .spaces-row,
  .task-toolbar {
    grid-template-columns: 1fr;
  }

  .j-tab {
    flex-basis: 116px;
  }
}

/* Compact density */
.jira-dashboard {
  padding: 18px var(--sa-page-x, 24px) 30px !important;
  gap: 22px !important;
  min-height: calc(100vh - var(--sa-topbar-height, 52px)) !important;
}

.recommended-section,
.for-you-section,
.jira-section {
  max-width: 1180px !important;
}

.recommended-section:first-child,
.spaces-panel {
  padding: 18px !important;
  border-radius: 10px !important;
}

.section-title,
.jira-section-title {
  font-size: clamp(21px, 1.65vw, 28px) !important;
  line-height: 1.12 !important;
}

.spaces-row,
.recommendations-grid {
  gap: 12px !important;
}

.space-card {
  min-height: 58px !important;
  padding: 10px 12px !important;
  border-radius: 8px !important;
}

.space-avatar,
.space-icon,
.space-emoji {
  width: 30px !important;
  height: 30px !important;
  border-radius: 8px !important;
}

.space-title,
.sc-title {
  font-size: 13px !important;
}

.space-meta,
.sc-desc,
.task-meta,
.jtr-subtitle {
  font-size: 11.5px !important;
}

.jira-tabs {
  min-height: 42px !important;
  padding: 4px !important;
  border-radius: 9px !important;
  width: max-content !important;
  max-width: 100% !important;
  overflow-x: auto !important;
}

.tab-button,
.j-tab {
  height: 34px !important;
  min-height: 34px !important;
  width: auto !important;
  min-width: max-content !important;
  flex: 0 0 auto !important;
  padding: 0 16px !important;
  border-radius: 7px !important;
  font-size: 12.5px !important;
  overflow: visible !important;
}

.j-tab span:first-child {
  min-width: max-content !important;
  overflow: visible !important;
  text-overflow: clip !important;
  white-space: nowrap !important;
}

.task-toolbar {
  padding: 8px !important;
  border-radius: 10px !important;
  gap: 8px !important;
}

.search-input-wrapper,
.search-input input,
.toolbar-select,
.jira-select {
  height: 34px !important;
  border-radius: 8px !important;
  font-size: 12.5px !important;
}

.jira-task-row {
  min-height: 56px !important;
  padding: 10px 14px !important;
  border-radius: 8px !important;
}

.task-title,
.jtr-title {
  font-size: 13px !important;
  line-height: 1.25 !important;
  overflow-wrap: anywhere !important;
}

@media (max-width: 768px) {
  .jira-dashboard {
    padding: 12px !important;
    gap: 16px !important;
  }

  .recommended-section:first-child,
  .spaces-panel {
    padding: 12px !important;
  }

  .spaces-row,
  .recommendations-grid {
    grid-template-columns: 1fr !important;
  }

  .foryou-header-row,
  .for-you-header {
    align-items: flex-start !important;
    grid-template-columns: 1fr !important;
    flex-direction: column !important;
    gap: 8px !important;
  }

  .jira-tabs {
    width: 100% !important;
    overflow-x: auto !important;
  }

  .j-tab {
    padding: 0 14px !important;
  }

  .task-toolbar {
    grid-template-columns: 1fr !important;
  }
}

/* Premium experience layer */
@keyframes sprinta-rise-in {
  from {
    opacity: 0;
    transform: translateY(16px) scale(0.985);
  }
  to {
    opacity: 1;
    transform: translateY(0) scale(1);
  }
}

@keyframes sprinta-soft-sheen {
  from { transform: translateX(-130%) rotate(12deg); }
  to { transform: translateX(130%) rotate(12deg); }
}

@keyframes sprinta-ambient-drift {
  0% { transform: translate3d(-1.5%, -1%, 0) scale(1); opacity: 0.70; }
  50% { transform: translate3d(1.5%, 1%, 0) scale(1.015); opacity: 0.95; }
  100% { transform: translate3d(0.5%, -0.5%, 0) scale(1.01); opacity: 0.78; }
}

.jira-dashboard {
  background:
    radial-gradient(circle at 22% 4%, rgba(56, 189, 248, 0.18), transparent 28%),
    radial-gradient(circle at 78% 10%, rgba(34, 197, 94, 0.12), transparent 26%),
    linear-gradient(180deg, #f8fcff 0%, #eef6fb 48%, #f8fafc 100%) !important;
}

.main-content-column > section {
  animation: sprinta-rise-in 520ms cubic-bezier(0.2, 0.8, 0.2, 1) both;
}

.main-content-column > section:nth-child(2) {
  animation-delay: 90ms;
}

.main-content-column > section:first-child {
  position: relative;
  overflow: hidden;
  border: 1px solid rgba(56, 189, 248, 0.22) !important;
  background:
    linear-gradient(135deg, rgba(255, 255, 255, 0.96), rgba(240, 249, 255, 0.82)),
    var(--color-surface) !important;
  box-shadow:
    0 28px 80px rgba(14, 165, 233, 0.12),
    0 1px 0 rgba(255, 255, 255, 0.92) inset !important;
}

.main-content-column > section:first-child::before {
  content: "";
  position: absolute;
  inset: 0;
  background:
    radial-gradient(circle at 15% 20%, rgba(56, 189, 248, 0.22), transparent 24%),
    radial-gradient(circle at 74% 8%, rgba(45, 212, 191, 0.16), transparent 22%),
    linear-gradient(120deg, rgba(255,255,255,0.20), transparent 38%, rgba(14,165,233,0.08));
  opacity: 0.84;
  pointer-events: none;
  animation: sprinta-ambient-drift 8s ease-in-out infinite alternate;
}

.main-content-column > section:first-child:hover::before {
  opacity: 0.96;
}

.main-content-column > section:first-child > * {
  position: relative;
  z-index: 1;
}

.section-title {
  letter-spacing: -0.015em !important;
  text-shadow: 0 1px 0 rgba(255, 255, 255, 0.74);
}

.view-all-link {
  font-weight: 900 !important;
  transition: color 180ms ease, transform 180ms ease;
}

.view-all-link:hover {
  transform: translateX(3px);
}

.space-card,
.jira-task-row,
.task-toolbar,
.jira-tabs {
  transition:
    transform 220ms cubic-bezier(0.2, 0.8, 0.2, 1),
    box-shadow 220ms ease,
    border-color 220ms ease,
    background 220ms ease !important;
}

.space-card {
  position: relative;
  overflow: hidden;
  border: 1px solid rgba(148, 163, 184, 0.20) !important;
  background:
    linear-gradient(135deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.88)),
    var(--color-surface) !important;
  box-shadow: 0 14px 34px rgba(15, 23, 42, 0.075) !important;
}

.space-card::before {
  content: "";
  position: absolute;
  inset: 0 auto 0 0;
  width: 3px;
  background: linear-gradient(180deg, #22d3ee, #2dd4bf, #facc15);
}

.space-card:hover {
  transform: translateY(-3px) scale(1.01);
  border-color: rgba(14, 165, 233, 0.36) !important;
  box-shadow: 0 24px 58px rgba(14, 165, 233, 0.16) !important;
}

.sc-icon-wrapper {
  box-shadow:
    0 10px 24px rgba(14, 165, 233, 0.14),
    inset 0 1px 0 rgba(255, 255, 255, 0.74) !important;
}

.jira-tabs {
  background: rgba(255, 255, 255, 0.82) !important;
  box-shadow: 0 14px 34px rgba(15, 23, 42, 0.08) !important;
  backdrop-filter: blur(14px);
}

[data-theme='light'] .main-content-column > section:first-child {
  color: #0f172a !important;
  background:
    linear-gradient(135deg, rgba(255, 255, 255, 0.94), rgba(239, 248, 255, 0.80)),
    #ffffff !important;
}

[data-theme='light'] .section-title,
[data-theme='light'] .task-title,
[data-theme='light'] .sc-name,
[data-theme='light'] .j-tab,
[data-theme='light'] .filter-pill,
[data-theme='light'] .sort-pill {
  color: #0f172a !important;
}

[data-theme='light'] .task-meta,
[data-theme='light'] .sc-meta,
[data-theme='light'] .empty-text {
  color: #475569 !important;
}

[data-theme='light'] .jira-task-row,
[data-theme='light'] .task-toolbar,
[data-theme='light'] .jira-tabs,
[data-theme='light'] .space-card {
  background: rgba(255,255,255,0.88) !important;
  border-color: rgba(148, 163, 184, 0.20) !important;
}

.j-tab.active {
  background:
    linear-gradient(135deg, rgba(34, 211, 238, 0.20), rgba(45, 212, 191, 0.14)) !important;
  box-shadow: inset 0 0 0 1px rgba(14, 165, 233, 0.12);
}

.task-toolbar {
  background: rgba(255, 255, 255, 0.78) !important;
  box-shadow: 0 16px 38px rgba(15, 23, 42, 0.08) !important;
  backdrop-filter: blur(14px);
}

.jira-task-row {
  position: relative;
  overflow: hidden;
  border: 1px solid rgba(148, 163, 184, 0.18) !important;
  background: rgba(255, 255, 255, 0.90) !important;
}

.jira-task-row::before {
  content: "";
  position: absolute;
  inset: 0 auto 0 0;
  width: 3px;
  background: linear-gradient(180deg, #38bdf8, #8b5cf6);
  opacity: 0;
  transition: opacity 180ms ease;
}

.jira-task-row:hover {
  transform: translateX(4px);
  background:
    linear-gradient(90deg, rgba(56, 189, 248, 0.09), rgba(255, 255, 255, 0.94) 38%),
    #ffffff !important;
  border-color: rgba(56, 189, 248, 0.28) !important;
  box-shadow: 0 18px 42px rgba(15, 23, 42, 0.10) !important;
}

.jira-task-row:hover::before {
  opacity: 1;
}

[data-theme='dark'] .jira-dashboard {
  background:
    radial-gradient(circle at 18% 0%, rgba(56, 189, 248, 0.18), transparent 30%),
    radial-gradient(circle at 84% 8%, rgba(34, 197, 94, 0.10), transparent 28%),
    linear-gradient(180deg, #06111f, #0f172a 52%, #101827) !important;
}

[data-theme='dark'] .main-content-column > section:first-child,
[data-theme='dark'] .task-toolbar,
[data-theme='dark'] .jira-tabs {
  background:
    linear-gradient(135deg, rgba(30, 41, 59, 0.92), rgba(15, 23, 42, 0.82)),
    #0f172a !important;
}

[data-theme='dark'] .main-content-column > section:first-child::before {
  background:
    radial-gradient(circle at 16% 18%, rgba(56, 189, 248, 0.22), transparent 26%),
    radial-gradient(circle at 76% 10%, rgba(34, 197, 94, 0.13), transparent 24%),
    linear-gradient(120deg, rgba(148,163,184,0.08), transparent 44%, rgba(14,165,233,0.10));
}

[data-theme='dark'] .section-title {
  color: #f8fafc !important;
  text-shadow: none !important;
}

[data-theme='dark'] .task-meta,
[data-theme='dark'] .sc-meta,
[data-theme='dark'] .empty-text {
  color: #cbd5e1 !important;
}

[data-theme='dark'] .space-card,
[data-theme='dark'] .jira-task-row {
  background:
    linear-gradient(135deg, rgba(30, 41, 59, 0.88), rgba(15, 23, 42, 0.92)),
    #0f172a !important;
}

@media (prefers-reduced-motion: reduce) {
  .main-content-column > section,
  .main-content-column > section:first-child::before {
    animation: none !important;
  }

  .space-card,
  .jira-task-row,
  .view-all-link {
    transition: none !important;
  }
}
</style>
