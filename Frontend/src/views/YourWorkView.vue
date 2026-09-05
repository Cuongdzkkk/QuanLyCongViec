<script setup>
import { ref, onMounted, onUnmounted, onBeforeUnmount, computed, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import { ElNotification, ElMessage } from 'element-plus'
import { useI18nStore } from '@/store/useI18nStore'
import { usePersonalWork } from '@/composables/usePersonalWork'
import { PERSONAL_WORK_SCOPES } from '@/api/personalWorkApi'
import { useAuthStore } from '@/store/useAuthStore'
import { useSiteStore } from '@/store/useSiteStore'
import { useProjectStore } from '@/store/useProjectStore'
import { useWorkTaskStore } from '@/store/useWorkTaskStore'
import { useStarredStore } from '@/store/useStarredStore'
import { useActivityStore } from '@/store/useActivityStore'
import { STARRED_ENTITY_TYPES } from '@/api/starredRecentApi'
import { translateDemoText } from '@/utils/demoContentLocale'
import UserAvatar from '@/components/common/UserAvatar.vue'
import ProjectAvatar from '@/components/project/ProjectAvatar.vue'
import TaskDetailModal from '@/components/TaskDetailModal.vue'
import { DEFAULT_PROJECT_BACKGROUND, DEFAULT_PROJECT_ICON } from '@/config/projectAppearance'
import ProjectEmptyState from '@/components/common/ProjectEmptyState.vue'
import { buildSpacePath } from '@/utils/spaceRoute'

const router = useRouter()
const route = useRoute()
const i18nStore = useI18nStore()
const authStore = useAuthStore()
const siteStore = useSiteStore()
const projectStore = useProjectStore()
const workTaskStore = useWorkTaskStore()
const starredStore = useStarredStore()
const activityStore = useActivityStore()
const t = (key) => i18nStore.t(key)
const demoText = (value) => translateDemoText(value, i18nStore.language || 'vi')

const activeTab = ref('Summary')
const tabs = ['Summary', 'Assigned', 'Worked', 'Starred', 'Created', 'Activity']

watch(
  () => route.query.tab,
  (newTab) => {
    if (newTab) {
      const matched = tabs.find(t => t.toLowerCase() === String(newTab).toLowerCase())
      if (matched) {
        activeTab.value = matched
      }
    }
  },
  { immediate: true }
)

watch(activeTab, (newTab) => {
  if (route.query.tab !== newTab) {
    router.replace({
      query: {
        ...route.query,
        tab: newTab
      }
    })
  }
})
const tabLabel = (tab) => {
  const map = {
    Summary: t('yourWork.tabs.summary') || 'Summary',
    Assigned: t('yourWork.tabs.assigned') || 'Assigned',
    Worked: t('yourWork.tabs.worked') || 'Worked on',
    Starred: t('forYou.starred') || 'Starred',
    Created: t('yourWork.tabs.created') || 'Created',
    Activity: t('yourWork.tabs.activity') || 'Activity'
  }
  return map[tab] || tab
}

const selectedProjectId = ref(null)
const projectList = ref([])
const currentPage = ref(1)
const pageSize = ref(10)

// Recommended Spaces
const loadingSpaces = ref(false)
const errorSpaces = ref(null)
const spaces = ref([])

const fetchSpaces = async () => {
  loadingSpaces.value = true
  errorSpaces.value = null
  try {
    const list = await projectStore.fetchAllProjects(true)
    spaces.value = list.map(p => ({
      id: p.id,
      name: p.name,
      key: p.key || p.name.substring(0, 4).toUpperCase(),
      description: p.description || t('forYou.softwareSpace'),
      cover: p.cover || DEFAULT_PROJECT_BACKGROUND,
      icon: p.icon || DEFAULT_PROJECT_ICON,
      taskCount: p.taskCount ?? p.TotalTasks ?? p.totalTasks ?? p.activeMemberCount ?? p.ActiveMemberCount ?? 0,
      activeMemberCount: p.activeMemberCount || p.ActiveMemberCount || 0,
      memberCount: p.activeMemberCount || p.ActiveMemberCount || p.memberCount || p.MemberCount || 0,
      networkType: p.networkType || p.NetworkType || p.visibility || p.Visibility || 'Public',
      createdAt: p.createdAt || null,
      leader: {
        id: p.leadUserId || p.creatorId || p.ownerId || p.originalRow?.leadUserId || p.originalRow?.creatorId || p.originalRow?.ownerId,
        fullName: p.leadName || p.creatorName || p.ownerName || p.owner || p.originalRow?.leadName || p.originalRow?.creatorName || p.originalRow?.ownerName || p.originalRow?.owner || 'Project',
        avatarUrl: p.leadAvatarUrl || p.creatorAvatarUrl || p.ownerAvatarUrl || p.originalRow?.leadAvatarUrl || p.originalRow?.creatorAvatarUrl || p.originalRow?.ownerAvatarUrl,
        avatarColor: p.leadColor || p.creatorColor || p.ownerColor || p.originalRow?.leadColor || p.originalRow?.creatorColor || p.originalRow?.ownerColor
      },
      originalRow: p
    }))
  } catch (error) {
    errorSpaces.value = t('forYou.loadProjectsFailed')
    console.error('Fetch spaces error:', error)
  } finally {
    loadingSpaces.value = false
  }
}

const toggleSpaceStar = async (space) => {
  try {
    const isCurrentlyFav = projectStore.favoriteProjects.some(p => p.id === space.id)
    await projectStore.updateFavorite(space.id, !isCurrentlyFav)
    ElMessage.success(isCurrentlyFav ? t('forYou.spaceUnstarred') : t('forYou.spaceStarred'))
  } catch {
    ElMessage.error(starredStore.error || t('forYou.updateSpaceStarFailed'))
  }
}

const toggleTaskStar = async (item) => {
  const targetId = item.rawId || item.id
  const wasStarred = starredStore.isStarred(STARRED_ENTITY_TYPES.WORK_TASK, targetId)
  try {
    await starredStore.setStarred(STARRED_ENTITY_TYPES.WORK_TASK, targetId, !wasStarred)
    ElMessage.success(wasStarred ? t('forYou.taskUnstarred') : t('forYou.taskStarred'))
  } catch {
    ElMessage.error(starredStore.error || t('forYou.updateTaskStarFailed'))
  }
}

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

const formatSpaceCreatedDate = (value) => {
  if (!value) return '--'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '--'
  return date.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

const getSpaceVisibilityLabel = (space) => {
  const value = `${space?.networkType || ''}`.trim().toLowerCase()
  return value === 'private' || value === 'privated' ? 'Private' : 'Public'
}

const getSpaceMemberCountLabel = (space) => {
  let count = Number(space?.activeMemberCount ?? space?.memberCount)
  if (!count) count = space?.originalRow?.members?.length || space?.originalRow?.projectMembers?.length || space?.originalRow?.users?.length || 0
  if (!Number.isFinite(count) || count === 0) count = 1
  return `${count} members`
}

const {
  items: myTasks,
  totalCount,
  loading,
  error,
  summary,
  summaryLoading,
  summaryError,
  activities,
  activityLoading,
  activityError,
  fetchPage,
  fetchSummary,
  fetchActivity,
  reset
} = usePersonalWork()

const tabScopes = {
  Recommended: PERSONAL_WORK_SCOPES.suggested,
  Assigned: PERSONAL_WORK_SCOPES.assigned,
  Created: PERSONAL_WORK_SCOPES.created,
  Following: PERSONAL_WORK_SCOPES.following,
  Worked: PERSONAL_WORK_SCOPES.worked
}
const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize.value)))
const activeWorkspaceId = computed(() => siteStore.activeSite?.id || siteStore.activeSite?.Id || '')
const activeWorkspaceProjectIds = computed(() => (
  projectStore.sidebarProjects || []
).map(project => project.id || project.Id).filter(Boolean))
const contextKey = computed(() => [
  authStore.userId || '',
  authStore.token || '',
  activeWorkspaceId.value
].join(':'))

const userProfile = ref({
  fullName: '—',
  email: '—',
  avatarUrl: null,
  initials: '—',
  joinedOn: '—',
  timezone: '—'
})

const fetchProfile = async () => {
  try {
    const res = await axiosClient.get('/users/me')
    const data = res.data?.data
    if (data) {
      userProfile.value = {
        fullName: data.fullName || null,
        displayName: data.displayName || null,
        email: data.email || null,
        avatarUrl: data.avatarUrl,
        avatarColor: data.avatarColor,
        initials: data.initials || (() => {
          const n = data.fullName || data.email;
          if (!n) return 'U';
          const parts = n.trim().split(/\s+/).filter(Boolean);
          if (parts.length >= 2) return (parts[0][0] + parts.at(-1)[0]).toUpperCase();
          return n[0]?.toUpperCase() || 'U';
        })(),
        joinedOn: data.createdAt ? new Date(data.createdAt).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : null,
        timezone: data.timezone || null,
        department: data.department || null,
        organization: data.organization || null,
        jobTitle: data.jobTitle || null,
        location: data.location || null,
        roles: data.roles || (data.roleName ? [data.roleName] : []),
        lastActive: data.lastActive || null,
        bio: data.bio || null,
        createdAtRaw: data.createdAt
      }
    }
  } catch (e) {
    console.error('Failed to fetch profile', e)
  }
}

const fetchProjects = async () => {
    try {
        const [discoveryRes, archivedRes] = await Promise.all([
            axiosClient.get('/projects/discovery'),
            axiosClient.get('/projects/archived')
        ])
        const activeProjects = (discoveryRes.data?.data || []).map(p => ({ ...p, isArchived: false }))
        const archivedProjects = (archivedRes.data?.data || []).map(p => ({ ...p, isArchived: true }))
        projectList.value = [...activeProjects, ...archivedProjects]
    } catch(e) {
        console.error('Error fetching projects', e)
    }
}

const getAvatarUrl = (path) => {
  if (!path) return ''
  const base = (axiosClient.defaults.baseURL || 'http://localhost:5136/api').replace(/\/api\/?$/, '')
  return `${base}${path}`
}

const fetchMyTasks = async () => {
  if (activeTab.value === 'Starred') {
    return starredStore.fetchStarredItems({ page: currentPage.value, pageSize: pageSize.value }).catch(() => null)
  }
  if (activeTab.value === 'Viewed') {
    return starredStore.fetchRecentItems({ page: currentPage.value, pageSize: pageSize.value }).catch(() => null)
  }
  const scope = tabScopes[activeTab.value]
  if (!scope) return null
  return fetchPage({
    scope,
    page: currentPage.value,
    pageSize: pageSize.value,
    workspaceId: activeWorkspaceId.value,
    projectIds: activeWorkspaceProjectIds.value
  }).catch(() => null)
}

const fetchScopedSummary = () => fetchSummary({
  workspaceId: activeWorkspaceId.value,
  projectIds: activeWorkspaceProjectIds.value
})

const fetchScopedActivity = (options = {}) => fetchActivity({
  ...options,
  workspaceId: activeWorkspaceId.value,
  projectIds: activeWorkspaceProjectIds.value
})

let timeInterval = null
onMounted(async () => {
  await fetchProjects()
  fetchProfile()
  fetchSpaces()
  starredStore.fetchStarredItems({ page: 1, pageSize: 100 }).catch(() => {})
  timeInterval = setInterval(() => {
    currentTime.value = new Date().toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: true })
  }, 1000)
  currentTime.value = new Date().toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: true })
})

onUnmounted(() => {
  if (timeInterval) clearInterval(timeInterval)
})

const currentTime = ref('')

const calculateMemberFor = (dateStr) => {
  if (!dateStr) return ''
  const joined = new Date(dateStr)
  if (isNaN(joined.getTime())) return ''
  const diffDays = Math.ceil(Math.abs(new Date() - joined) / (1000 * 60 * 60 * 24))
  if (diffDays < 30) return `${diffDays} days`
  const diffMonths = Math.floor(diffDays / 30)
  if (diffMonths < 12) return `${diffMonths} month${diffMonths > 1 ? 's' : ''}`
  const diffYears = Math.floor(diffMonths / 12)
  return `${diffYears} year${diffYears > 1 ? 's' : ''}`
}

const completionRate = computed(() => {
  const total = (workload.value.suggested || 0) + (workload.value.workedOn || 0) + (workload.value.overdue || 0) + (workload.value.completed || 0)
  if (total === 0) return 0
  return Math.round(((workload.value.completed || 0) / total) * 100)
})

const overview = computed(() => ({
  created: summary.value?.created ?? 0,
  assigned: summary.value?.assigned ?? 0,
  following: summary.value?.following ?? 0
}))

const workload = computed(() => {
  return {
    suggested: summary.value?.suggested ?? 0,
    workedOn: summary.value?.workedOn ?? 0,
    overdue: summary.value?.overdue ?? 0,
    completed: summary.value?.completed ?? 0
  }
})

const recentActivity = computed(() => {
  return pageActivities.value.slice(0, 10).map(activity => ({
    id: activity.id,
    text: activity.text,
    time: activity.time
  }))
})

const collectionTasks = computed(() => {
  const source = activeTab.value === 'Starred' ? starredStore.starredItems : starredStore.recentItems
  return source.map(item => ({
    id: item.sequenceId || (item.itemId || item.entityId)?.substring(0, 8).toUpperCase() || 'ITEM-1',
    rawId: item.itemId || item.entityId,
    title: item.itemName || item.title || t('forYou.untitled'),
    state: item.statusName || 'TO DO',
    priority: item.priority || 3,
    assigneeName: item.assigneeName || '',
    assigneeInitials: item.assigneeInitials || '',
    assigneeAvatarColor: item.assigneeAvatarColor || '',
    modules: '0 Modules',
    cycle: 'No Cycle',
    task: {
      id: item.itemId || item.entityId,
      title: item.itemName || item.title,
      projectId: item.projectId,
      projectName: item.subtitle,
      url: item.url,
      entityType: item.itemType || item.entityType,
      statusName: item.statusName || 'TO DO',
      priority: item.priority || 3
    }
  }))
})

const listData = computed(() => {
  if (activeTab.value === 'Starred' || activeTab.value === 'Viewed') {
    return collectionTasks.value
  }
  return myTasks.value.map(task => ({
    id: task.sequenceId || task.id.substring(0, 8).toUpperCase(),
    rawId: task.id,
    title: task.title,
    state: task.statusName || 'To Do',
    priority: task.priority || 3,
    assigneeName: task.assigneeName,
    assigneeInitials: task.assigneeInitials,
    assigneeAvatarColor: task.assigneeAvatarColor,
    modules: '0 Modules',
    cycle: 'No Cycle',
    task
  }))
})

const taskListLoading = computed(() => {
  if (activeTab.value === 'Starred') return starredStore.loading
  if (activeTab.value === 'Viewed') return starredStore.recentLoading
  return loading.value
})

const taskListError = computed(() => {
  if (activeTab.value === 'Starred') return starredStore.error
  if (activeTab.value === 'Viewed') return starredStore.recentError
  return error.value
})

const effectiveTotalCount = computed(() => {
  if (activeTab.value === 'Starred') return starredStore.starredPagination.totalCount || starredStore.starredItems.length
  if (activeTab.value === 'Viewed') return starredStore.recentPagination.totalCount || starredStore.recentItems.length
  return totalCount.value
})

const effectiveTotalPages = computed(() => Math.max(1, Math.ceil(effectiveTotalCount.value / pageSize.value)))

// Task Details Modal interactions
const selectedTask = ref(null)
const taskDetailHistory = ref([])
const projectMembers = ref([])
const currentProjectRole = ref('member')

const openTaskDetail = async (item) => {
  const task = item.task || item
  if (task.url && (task.entityType === STARRED_ENTITY_TYPES.PROJECT || !task.projectId)) {
    await router.push(task.url)
    return
  }
  if (!task.projectId) return

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

const updateTaskProperty = async (task, field, value) => {
  try {
    const index = myTasks.value.findIndex(item => item.id === task.id)
    if (index !== -1) {
      myTasks.value[index][field] = value

      const updatePayload = { [field]: value }
      if (task.projectId) {
        await axiosClient.patch(`/projects/${task.projectId}/WorkTasks/${task.id}`, updatePayload)
      }

      await fetchScopedActivity({ limit: 10 }).catch(() => null)
    }
  } catch (error) {
    console.error('Failed to update task:', error)
  }
}

const pageActivities = computed(() => activities.value.slice(0, 10).map(activity => activityStore.normalizeActivity(activity)))

const retryCurrentView = () => {
  if (activeTab.value === 'Summary') {
    fetchScopedSummary().catch(() => null)
    fetchScopedActivity({ limit: 10 }).catch(() => null)
    return
  }
  if (activeTab.value === 'Activity') {
    fetchScopedActivity({ limit: 10 }).catch(() => null)
    return
  }
  fetchMyTasks()
}

watch(activeTab, () => {
  currentPage.value = 1
  myTasks.value = []
  totalCount.value = 0

  if (activeTab.value === 'Summary') {
    fetchScopedSummary().catch(() => null)
    fetchScopedActivity({ limit: 10 }).catch(() => null)
    return
  }
  if (activeTab.value === 'Activity') {
    fetchScopedActivity({ limit: 10 }).catch(() => null)
    return
  }
  fetchMyTasks()
})

watch(currentPage, () => {
  if (activeTab.value === 'Summary' || activeTab.value === 'Activity') return
  fetchMyTasks()
})

watch(contextKey, () => {
  reset()
  currentPage.value = 1
  if (!authStore.token || !authStore.userId) return

  fetchScopedSummary().catch(() => null)
  fetchScopedActivity({ limit: 10 }).catch(() => null)
  fetchMyTasks()
}, { immediate: true })

onBeforeUnmount(() => {
  reset()
})

const downloadWordActivity = () => {
  let htmlContent = `
  <html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:w='urn:schemas-microsoft-com:office:word' xmlns='http://www.w3.org/TR/REC-html40'>
  <head><meta charset='utf-8'><title>Activity Log</title></head><body>
  <h2>Activity History</h2>
  <ul>
  `

  pageActivities.value.forEach(activity => {
    htmlContent += `<li><strong>${activity.time}</strong> - ${activity.text} <em>${activity.bold || ''}</em></li>`
  })

  htmlContent += '</ul></body></html>'

  const blob = new Blob(['\ufeff', htmlContent], { type: 'application/msword' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `Activity_Log_${new Date().toISOString().slice(0, 10)}.doc`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)

  ElNotification({ title: 'Success', message: 'Activity history exported.', type: 'success' })
}

const getAvatarBg = (name) => {
  if (!name || name === 'Unassigned') return '#64748b'
  const colors = ['#3b82f6', '#10b981', '#fbbf24', '#ec4899', '#8b5cf6', '#06b6d4', '#f97316']
  let hash = 0
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash)
  }
  const index = Math.abs(hash) % colors.length
  return colors[index]
}

const getInitials = (name) => {
  if (!name) return 'U'
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (parts.length >= 2) {
    return (parts[0][0] + parts.at(-1)[0]).toUpperCase()
  }
  return name[0]?.toUpperCase() || 'U'
}
</script>

<template>
  <section class="your-work-page">
    <header class="page-header app-shell-page-header your-work-page-header">
      <div class="your-work-title-wrap">
        <h1>Your work</h1>
        <div class="header-help">
          <span
            class="header-help-btn"
            aria-label="About Your work"
          >
            <i class="fa-solid fa-question"></i>
          </span>
          <div class="header-help-popover" role="tooltip">
            <span>PERSONAL PRODUCTIVITY</span>
            <p>Theo dõi công việc được giao, đã tạo, đánh dấu sao và hoạt động gần đây của bạn.</p>
          </div>
        </div>
        <p>Theo dõi công việc được giao, đã tạo, đánh dấu sao và hoạt động gần đây của bạn.</p>
      </div>
    </header>

    <div class="jira-dashboard">
      <div class="yw-grid">
      <div class="main-content-column">
        <!-- Recommended Spaces Section (Integrated from For You) -->
        <section class="recommended-spaces-section mb-6 sprinta-section-panel sprinta-section-panel-blue your-work-flat-section">
          <div class="section-header flex-between mb-4 items-center">
            <h2 class="section-title">
              {{ t('forYou.recommendedSpaces') }}
            </h2>
            <router-link to="/spaces" class="view-all-link">{{ t('forYou.viewAllSpaces') }}</router-link>
          </div>

          <div v-if="loadingSpaces" class="loading-state">
            <i class="fa-solid fa-spinner fa-spin"></i> {{ t('forYou.loadingSpaces') }}
          </div>
          
          <ProjectEmptyState
            v-else-if="sortedSpaces.length === 0"
            icon="fa-regular fa-folder-open"
            :title="t('forYou.noActiveSpaces')"
            :description="t('forYou.noActiveSpacesDesc')"
          />
          
          <div v-else class="spaces-row">
            <div 
              v-for="space in sortedSpaces.slice(0, 6)" 
              :key="space.id" 
              class="yw-project-card"
              @click="router.push(buildSpacePath(space, 'work-items'))"
            >
              <div class="yw-card-cover" :style="{ background: space.cover || '#1e293b' }">
                <div class="yw-card-actions">
                  <button 
                    class="yw-card-btn"
                    type="button"
                    :class="{ starred: projectStore.favoriteProjects.some(p => p.id === space.id) }"
                    :disabled="starredStore.isPending(STARRED_ENTITY_TYPES.PROJECT, space.id)"
                    :aria-pressed="projectStore.favoriteProjects.some(p => p.id === space.id)"
                    @click.stop="toggleSpaceStar(space)"
                    :title="projectStore.favoriteProjects.some(p => p.id === space.id) ? 'Bỏ đánh dấu' : 'Đánh dấu'"
                  >
                    <i :class="projectStore.favoriteProjects.some(p => p.id === space.id) ? 'fa-solid fa-star' : 'fa-regular fa-star'"></i>
                  </button>
                </div>
              </div>
              <div class="yw-card-body">
                <ProjectAvatar class="yw-floating-avatar" :icon="space.icon" :background="space.cover" size="sm" />
                <div class="yw-proj-header">
                  <h3 class="yw-proj-name" :title="demoText(space.name)">{{ demoText(space.name) }}</h3>
                  <span class="yw-proj-key" v-if="space.key">{{ space.key }}</span>
                </div>
                <p class="yw-proj-desc" :title="demoText(space.description)">{{ demoText(space.description) || '...' }}</p>
                <div class="yw-proj-meta">
                  <span class="yw-meta-item" :class="getSpaceVisibilityLabel(space).toLowerCase()">
                    <i :class="getSpaceVisibilityLabel(space) === 'Private' ? 'fa-solid fa-lock' : 'fa-solid fa-globe'"></i>
                    {{ getSpaceVisibilityLabel(space) }}
                  </span>
                  <span class="yw-meta-item"><i class="fa-regular fa-calendar"></i>{{ formatSpaceCreatedDate(space.createdAt) }}</span>
                  <span class="yw-meta-item"><i class="fa-solid fa-users"></i>{{ getSpaceMemberCountLabel(space) }}</span>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section class="yw-content-card sprinta-section-panel sprinta-section-panel-blue your-work-flat-section">
          <div class="yw-tabs">
            <button
              v-for="tab in tabs"
              :key="tab"
              class="tab-btn"
              :class="{ active: activeTab === tab }"
              @click="activeTab = tab"
            >
              {{ tabLabel(tab) }}
            </button>
          </div>

          <div class="yw-tabs-content-wrapper" style="min-height: 450px;">
              <div class="yw-scrollable" v-if="activeTab === 'Summary'">
          <div v-if="summaryLoading" class="personal-state mt-4">
            <i class="fa-solid fa-spinner fa-spin"></i>
            <span>{{ t('yourWork.loading') }}</span>
          </div>
          <div v-else-if="summaryError" class="personal-state personal-state-error mt-4">
            <span>{{ t('yourWork.loadFailed') }}</span>
            <button class="plane-primary-btn" @click="retryCurrentView">{{ t('yourWork.retry') }}</button>
          </div>
          <template v-else>
            <section class="yw-section">
              <h3 class="section-title mt-4">{{ t('yourWork.overview') }}</h3>
              <div class="yw-cards-row">
                <div class="yw-card">
                  <div class="card-icon"><i class="fa-solid fa-plus"></i></div>
                  <div class="card-info">
                    <div class="card-lbl">{{ t('yourWork.created') }}</div>
                    <div class="card-val">{{ overview.created }}</div>
                  </div>
                </div>
                <div class="yw-card">
                  <div class="card-icon"><i class="fa-regular fa-circle-user"></i></div>
                  <div class="card-info">
                    <div class="card-lbl">{{ t('yourWork.assigned') }}</div>
                    <div class="card-val">{{ overview.assigned }}</div>
                  </div>
                </div>
                <div class="yw-card">
                  <div class="card-icon"><i class="fa-solid fa-inbox"></i></div>
                  <div class="card-info">
                    <div class="card-lbl">{{ t('yourWork.following') }}</div>
                    <div class="card-val">{{ overview.following }}</div>
                  </div>
                </div>
              </div>
            </section>

          <section class="yw-section">
            <h3 class="yw-section-title">
              {{ t('yourWork.workload') }}
            </h3>
            <div class="yw-workload-row">
            <div class="wl-card">
              <div class="wl-lbl"><span class="dbox bg-blue"></span> {{ t('yourWork.suggested') }}</div>
              <div class="wl-val">{{ workload.suggested }}</div>
            </div>
            <div class="wl-card">
              <div class="wl-lbl"><span class="dbox bg-gray"></span> {{ t('yourWork.workedOn') }}</div>
              <div class="wl-val">{{ workload.workedOn }}</div>
            </div>
            <div class="wl-card">
              <div class="wl-lbl"><span class="dbox bg-orange"></span> {{ t('yourWork.overdue') }}</div>
              <div class="wl-val">{{ workload.overdue }}</div>
            </div>
            <div class="wl-card">
              <div class="wl-lbl"><span class="dbox bg-green"></span> {{ t('yourWork.completed') }}</div>
              <div class="wl-val">{{ workload.completed }}</div>
            </div>
          </div>
          </section>

          <h3 class="section-title mt-4 recent-section-title">{{ t('yourWork.recentActivity') }}</h3>
          <div class="list-body recent-list-body">
            <div v-if="activityLoading" class="personal-state">
              <i class="fa-solid fa-spinner fa-spin"></i>
              <span>{{ t('yourWork.loadingActivity') }}</span>
            </div>
            <div v-else-if="activityError" class="personal-state personal-state-error">
              <span>{{ t('yourWork.activityFailed') }}</span>
              <button class="plane-primary-btn" @click="retryCurrentView">{{ t('yourWork.retry') }}</button>
            </div>
            <ProjectEmptyState
              v-else-if="recentActivity.length === 0"
              icon="fa-solid fa-clock-rotate-left"
              :title="t('yourWork.noActivity')"
            />
            <div class="list-row" style="cursor: default;" v-for="activity in recentActivity" :key="activity.id">
              <div class="lr-left">
                <span class="lr-id" style="min-width: 30px;"><i class="fa-solid fa-clock-rotate-left" style="color: #A1A1AA"></i></span>
                <span class="lr-title">{{ activity.text }}</span>
              </div>
              <div class="lr-right">
                <div class="lr-badge cursor-not-allowed">
                  <i class="fa-regular fa-clock"></i> {{ activity.time }}
                </div>
              </div>
            </div>
          </div>
          </template>
        </div>

              <div class="yw-scrollable" v-else-if="['Assigned', 'Worked', 'Starred', 'Created'].includes(activeTab)">
          <div class="list-header mt-4 flex-between items-center">
            <div>
              <i class="fa-solid fa-circle-dashed f-icon"></i>
              <span class="lh-title">{{ t('yourWork.allWorkItems') }}</span>
              <span class="lh-count">{{ effectiveTotalCount }}</span>
            </div>
          </div>

          <div class="list-body mt-4" :class="{ 'recent-list-body': listData.length === 0 }">
            <Transition name="fade-fast" mode="out-in">
              <div v-if="taskListLoading" key="state-loading" class="personal-state">
                <i class="fa-solid fa-spinner fa-spin"></i>
                <span>{{ t('yourWork.loading') }}</span>
              </div>
              <div v-else-if="taskListError" key="state-error" class="personal-state personal-state-error">
                <span>{{ t('yourWork.loadFailed') }}</span>
                <button class="plane-primary-btn" @click="retryCurrentView">{{ t('yourWork.retry') }}</button>
              </div>
              <ProjectEmptyState
                v-else-if="listData.length === 0" key="state-empty"
                icon="fa-solid fa-layer-group"
                title="Không có công việc nào"
                :description="t(`yourWork.empty.${activeTab.toLowerCase()}`) || t('common.noData')"
              />
              <div v-else key="state-list" class="list-row-container">
                <div class="list-row cursor-pointer" v-for="item in listData" :key="item.id" @click="openTaskDetail(item)">
              <div class="lr-left">
                <button 
                  class="star-btn mr-2" 
                  type="button" 
                  :class="{ starred: starredStore.isStarred(STARRED_ENTITY_TYPES.WORK_TASK, item.rawId || item.id) }" 
                  @click.stop="toggleTaskStar(item)" 
                  :title="starredStore.isStarred(STARRED_ENTITY_TYPES.WORK_TASK, item.rawId || item.id) ? 'Bỏ đánh dấu' : 'Đánh dấu'"
                >
                  <i :class="starredStore.isStarred(STARRED_ENTITY_TYPES.WORK_TASK, item.rawId || item.id) ? 'fa-solid fa-star text-yellow-400' : 'fa-regular fa-star text-gray-400'"></i>
                </button>
                <span class="lr-id">{{ item.id }}</span>
                <span class="lr-title">{{ item.title }}</span>
              </div>
              <div class="lr-right" @click.stop>
                <el-dropdown trigger="click" @command="value => updateTaskProperty(item.task, 'statusName', value)">
                  <div class="lr-badge cursor-pointer hover:bg-[var(--color-bg-secondary)]">
                    <i class="fa-solid fa-circle-check" v-if="item.state.toUpperCase() === 'DONE'"></i>
                    <i class="fa-solid fa-circle-half-stroke" v-else-if="item.state.toUpperCase() === 'IN PROGRESS'"></i>
                    <i class="fa-regular fa-circle" v-else></i>
                    {{ item.state }}
                  </div>
                  <template #dropdown>
                    <el-dropdown-menu class="plane-dropdown">
                      <el-dropdown-item command="BACKLOG">Backlog</el-dropdown-item>
                      <el-dropdown-item command="TO DO">To Do</el-dropdown-item>
                      <el-dropdown-item command="IN PROGRESS">In Progress</el-dropdown-item>
                      <el-dropdown-item command="IN REVIEW">In Review</el-dropdown-item>
                      <el-dropdown-item command="DONE">Done</el-dropdown-item>
                    </el-dropdown-menu>
                  </template>
                </el-dropdown>

                <el-dropdown trigger="click" @command="value => updateTaskProperty(item.task, 'priority', value)">
                  <div class="lr-badge cursor-pointer hover:bg-[var(--color-bg-secondary)]">
                    <i class="fa-solid fa-angles-up text-red-500" v-if="Number(item.priority) === 1"></i>
                    <i class="fa-solid fa-chevron-up text-orange-500" v-else-if="Number(item.priority) === 2"></i>
                    <i class="fa-solid fa-minus text-blue-500" v-else-if="Number(item.priority) === 3"></i>
                    <i class="fa-solid fa-chevron-down text-gray-400" v-else-if="Number(item.priority) === 4"></i>
                    <i class="fa-solid fa-ban text-gray-500" v-else></i>
                  </div>
                  <template #dropdown>
                    <el-dropdown-menu class="plane-dropdown">
                      <el-dropdown-item :command="1"><i class="fa-solid fa-angles-up text-red-500"></i> Urgent</el-dropdown-item>
                      <el-dropdown-item :command="2"><i class="fa-solid fa-chevron-up text-orange-500"></i> High</el-dropdown-item>
                      <el-dropdown-item :command="3"><i class="fa-solid fa-minus text-blue-500"></i> Normal</el-dropdown-item>
                      <el-dropdown-item :command="4"><i class="fa-solid fa-chevron-down text-gray-400"></i> Low</el-dropdown-item>
                    </el-dropdown-menu>
                  </template>
                </el-dropdown>

                <UserAvatar v-if="item.assigneeName" :user="{ avatarColor: item.assigneeAvatarColor || getAvatarBg(item.assigneeName), initials: item.assigneeInitials || getInitials(item.assigneeName), fullName: item.assigneeName }" :size="24" :fontSize="10" />
                <div class="lr-badge cursor-not-allowed" v-else>
                  <i class="fa-regular fa-user"></i>
                </div>
                <div class="lr-badge cursor-not-allowed"><i class="fa-regular fa-calendar"></i></div>
                <div class="lr-badge cursor-not-allowed"><i class="fa-solid fa-table-cells-large"></i> {{ item.modules }}</div>
                <div class="lr-badge cursor-not-allowed"><i class="fa-solid fa-arrows-spin"></i> {{ item.cycle }}</div>
              </div>
            </div>
            </div>
            </Transition>
          </div>
          <div class="personal-pagination" v-if="!taskListLoading && !taskListError && effectiveTotalPages > 1">
            <button class="jira-btn-subtle" :disabled="currentPage === 1" @click="currentPage -= 1">
              <i class="fa-solid fa-chevron-left"></i>
            </button>
            <span>{{ currentPage }} / {{ effectiveTotalPages }}</span>
            <button class="jira-btn-subtle" :disabled="currentPage === effectiveTotalPages" @click="currentPage += 1">
              <i class="fa-solid fa-chevron-right"></i>
            </button>
          </div>
        </div>

              <div class="yw-scrollable" v-else-if="activeTab === 'Activity'">
          <div class="list-header mt-4 flex-between items-center" style="position: relative;">
            <div>
              <span class="lh-title">{{ t('yourWork.recentActivity') }}</span>
              <span class="lh-count">{{ activityTotal || pageActivities.length }}</span>
            </div>
            <button class="plane-primary-btn" @click="downloadWordActivity" style="position: absolute; right: 0; top: 50%; transform: translateY(-50%);">{{ t('yourWork.downloadActivity') }}</button>
          </div>

          <div class="list-body mt-4" :class="{ 'recent-list-body': pageActivities.length === 0 }">
            <Transition name="fade-fast" mode="out-in">
              <div v-if="activityLoading" key="state-loading" class="personal-state">
                <i class="fa-solid fa-spinner fa-spin"></i>
                <span>{{ t('yourWork.loadingActivity') }}</span>
              </div>
              <div v-else-if="activityError" key="state-error" class="personal-state personal-state-error">
                <span>{{ t('yourWork.activityFailed') }}</span>
                <button class="plane-primary-btn" @click="retryCurrentView">{{ t('yourWork.retry') }}</button>
              </div>
              <div v-else-if="pageActivities.length === 0" key="state-empty" class="empty-state-global">
                <div class="empty-spaces-icon" aria-hidden="true">
                  <i class="fa-solid fa-clock-rotate-left"></i>
                </div>
                <div class="empty-spaces-copy">
                  <h3>Không có hoạt động nào</h3>
                  <p>{{ t('yourWork.noActivity') }}</p>
                </div>
              </div>
              <div v-else key="state-list" class="list-row-container">
                <div class="list-row" style="cursor: default;" v-for="activity in pageActivities" :key="activity.id">
              <div class="lr-left">
                <span class="lr-id" style="min-width: 30px;"><i :class="activity.icon || 'fa-solid fa-bell'"></i></span>
                <span class="lr-title">{{ activity.text }} <span class="p-ac-bold text-white">{{ activity.bold }}</span></span>
              </div>
              <div class="lr-right">
                <div class="lr-badge cursor-not-allowed">
                  <i class="fa-regular fa-clock"></i> {{ activity.time }}
                </div>
              </div>
                </div>
              </div>
            </Transition>
          </div>
              </div>
          </div>
        </section>
      </div>

      <div class="yw-sidebar">
        <div class="profile-info-scroll">
          <div class="cover-image">
            <button class="edit-cover"><i class="fa-solid fa-pencil"></i></button>
          </div>
          <div class="profile-info">
            <UserAvatar :user="userProfile" :size="56" :fontSize="24" class="avatar-lg" style="position: absolute; top: -28px; left: 50%; transform: translateX(-50%);" />
            
            <!-- Section 1: Profile Header -->
            <div class="ps-header">
              <h2 class="user-name">{{ userProfile.fullName || t('Not specified') }}</h2>
              <p class="user-display-name" v-if="userProfile.displayName">{{ userProfile.displayName }}</p>
              <p class="user-handle">{{ userProfile.email || t('Not specified') }}</p>
              <div class="role-badges" v-if="userProfile.roles && userProfile.roles.length">
                <span class="role-badge" v-for="role in userProfile.roles" :key="role">{{ role }}</span>
              </div>
            </div>

            <div class="ps-divider"></div>

            <!-- Section 2: Work Information -->
            <div class="ps-section">
              <div class="info-row">
                <span class="info-lbl">{{ t('Department') }}</span>
                <span class="info-val">{{ userProfile.department || t('Not specified') }}</span>
              </div>
              <div class="info-row">
                <span class="info-lbl">{{ t('Organization') }}</span>
                <span class="info-val">{{ userProfile.organization || t('Not specified') }}</span>
              </div>
              <div class="info-row">
                <span class="info-lbl">{{ t('Job Title') }}</span>
                <span class="info-val">{{ userProfile.jobTitle || t('Not specified') }}</span>
              </div>
              <div class="info-row">
                <span class="info-lbl">{{ t('Location') }}</span>
                <span class="info-val">{{ userProfile.location || t('Not specified') }}</span>
              </div>
              <div class="info-row">
                <span class="info-lbl">{{ t('Timezone') }}</span>
                <span class="info-val text-right">
                  <template v-if="userProfile.timezone">
                    {{ userProfile.timezone }}<br><span class="time-now">{{ currentTime }}</span>
                  </template>
                  <template v-else>{{ t('Not specified') }}</template>
                </span>
              </div>
            </div>

            <div class="ps-divider"></div>

            <!-- Section 3: Member Information -->
            <div class="ps-section">
              <div class="info-row">
                <span class="info-lbl">{{ t('Joined') }}</span>
                <span class="info-val">{{ userProfile.joinedOn || t('Not specified') }}</span>
              </div>
              <div class="info-row" v-if="userProfile.createdAtRaw">
                <span class="info-lbl">{{ t('Member for') }}</span>
                <span class="info-val">{{ calculateMemberFor(userProfile.createdAtRaw) }}</span>
              </div>
              <div class="info-row" v-if="userProfile.lastActive">
                <span class="info-lbl">{{ t('Last Active') }}</span>
                <span class="info-val">{{ userProfile.lastActive }}</span>
              </div>
            </div>

            <div class="ps-divider"></div>

            <!-- Section 4: Performance -->
            <div class="ps-section">
              <div class="info-row">
                <span class="info-lbl">{{ t('Completion Rate') }}</span>
                <span class="info-val" style="color: var(--color-success); font-weight: 700;">{{ completionRate }}%</span>
              </div>
              <div class="progress-bar-container">
                <div class="progress-bar-fill" :style="{ width: completionRate + '%' }"></div>
              </div>
            </div>

            <div class="ps-divider"></div>

            <!-- Section 5: Workspace -->
            <div class="ps-section">
              <div class="ws-card">
                <div class="ws-card-icon">
                  <i class="fa-solid fa-briefcase"></i>
                </div>
                <div class="ws-card-info">
                  <div class="ws-name">{{ projectList[0]?.name || t('Not specified') }}</div>
                  <div class="ws-meta" v-if="projectList[0]">
                    <span v-if="projectList[0].memberCount !== undefined">{{ projectList[0].memberCount }} Members &bull; </span>
                    <span v-if="projectList.length">{{ projectList.length }} Projects</span>
                  </div>
                </div>
                <i class="fa-solid fa-chevron-right ws-chevron"></i>
              </div>
            </div>

            <div class="ps-divider"></div>

            <!-- Section 7: Quick Actions -->
            <div class="ps-section">
              <div class="quick-actions">
                <router-link to="/profile" class="qa-btn"><i class="fa-regular fa-user"></i> {{ t('Edit Profile') }}</router-link>
                <router-link to="/profile" class="qa-btn"><i class="fa-solid fa-gear"></i> {{ t('Account Settings') }}</router-link>
                <router-link to="/profile" class="qa-btn"><i class="fa-solid fa-shield-halved"></i> {{ t('Security') }}</router-link>
              </div>
            </div>

          </div>
        </div>
      </div>
      </div>
    </div>

    <!-- Task Detail Modal -->
    <TaskDetailModal
      v-if="selectedTask"
      :task="selectedTask"
      :projectId="selectedTask.projectId"
      :projectMembers="projectMembers"
      :currentRole="currentProjectRole"
      :canGoBack="taskDetailHistory.length > 0"
      @close="closeTaskDetail"
      @back="goBackTaskDetail"
      @open-task="openTaskDetailFromModal"
      @updateTask="updateTask"
      @refresh-tasks="fetchMyTasks"
    />
  </section>
</template>

<style scoped>
.your-work-page {
  --sa-page-x: 18px;
  min-height: 100%;
  background: var(--color-background);
  color: var(--color-text-primary);
}

.page-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 20px;
  padding: 26px var(--sa-page-x, 24px) 22px;
  background: var(--color-surface);
  border-bottom: 1px solid var(--color-border);
  overflow: visible;
}

.your-work-page-header {
  padding-bottom: 0 !important;
}

.your-work-title-wrap {
  display: inline-flex;
  align-items: flex-start;
  gap: 6px;
  min-width: 0;
}

.your-work-title-wrap > .eyebrow,
.your-work-title-wrap > p {
  display: none;
}

.eyebrow {
  color: var(--color-accent);
  font-size: 10px;
  font-weight: 800;
}

.page-header h1 {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 26px;
  font-weight: 900;
  line-height: 1.15;
  letter-spacing: 0;
}

.header-help {
  position: relative;
  display: inline-flex;
  align-items: center;
  margin-top: -1px;
}

.header-help-btn {
  width: 16px;
  height: 16px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid color-mix(in srgb, var(--color-border) 80%, var(--color-accent) 20%);
  border-radius: 50%;
  background: color-mix(in srgb, var(--color-surface) 92%, var(--color-accent) 8%);
  color: var(--color-text-muted);
  font-size: 8px;
  cursor: help;
  transition: all 0.18s ease;
}

.header-help:hover .header-help-btn {
  border-color: var(--color-accent);
  background: color-mix(in srgb, var(--color-accent) 14%, var(--color-surface));
  color: var(--color-accent);
  box-shadow: 0 6px 12px rgba(14, 165, 233, 0.14);
}

.header-help-popover {
  position: absolute;
  left: -8px;
  top: calc(100% + 9px);
  z-index: 30;
  width: min(360px, calc(100vw - 56px));
  padding: 12px 14px;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface);
  box-shadow: 0 18px 42px rgba(15, 23, 42, 0.16);
  opacity: 0;
  visibility: hidden;
  transform: translate(0, -4px);
  pointer-events: none;
  transition: opacity 0.16s ease, transform 0.16s ease, visibility 0.16s ease;
}

.header-help-popover::before {
  content: '';
  position: absolute;
  top: -6px;
  left: 13px;
  width: 10px;
  height: 10px;
  border-left: 1px solid var(--color-border);
  border-top: 1px solid var(--color-border);
  background: var(--color-surface);
  transform: rotate(45deg);
}

.header-help:hover .header-help-popover {
  opacity: 1;
  visibility: visible;
  transform: translate(0, 0);
  pointer-events: auto;
}

.header-help-popover span {
  display: block;
  color: var(--color-accent);
  font-size: 11px;
  font-weight: 900;
  letter-spacing: 0;
  text-transform: uppercase;
}

.header-help-popover p {
  margin: 0;
  margin-top: 5px;
  color: var(--color-text-muted);
  font-size: 13px;
  line-height: 1.45;
}

/* Spaces Row */
.spaces-row {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
  min-height: 250px;
  align-content: start;
}

.yw-project-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  cursor: pointer;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
  transition: all 0.2s ease;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.yw-project-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.08);
  border-color: var(--color-accent);
}

.yw-card-cover {
  height: 38px;
  background-color: #1e293b;
  position: relative;
}

.yw-card-actions {
  position: absolute;
  top: 6px;
  right: 6px;
  display: flex;
  gap: 4px;
}

.yw-card-btn {
  width: 24px;
  height: 24px;
  border-radius: 6px;
  background: rgba(255, 255, 255, 0.9);
  border: none;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--color-text-primary);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s ease;
  opacity: 0;
}

.yw-project-card:hover .yw-card-btn {
  opacity: 1;
}

.yw-card-btn:hover { background: #ffffff; color: var(--color-accent); }
.yw-card-btn.starred { opacity: 1; color: #facc15; }

.yw-card-body {
  padding: 0 12px 12px;
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.yw-floating-avatar {
  margin-top: -14px;
  margin-bottom: 4px;
  border: 2px solid var(--color-surface);
  border-radius: 8px;
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.1);
  background: var(--color-surface);
}

.yw-proj-header {
  display: flex;
  align-items: center;
  gap: 6px;
}

.yw-proj-name {
  font-size: 13px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.yw-proj-key {
  font-size: 10px;
  color: var(--color-text-muted);
  font-weight: 600;
}

.yw-proj-desc {
  font-size: 11px;
  color: var(--color-text-muted);
  margin: 0;
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 1;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.yw-proj-meta {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 4px;
}

.yw-meta-item {
  font-size: 9.5px;
  font-weight: 600;
  display: inline-flex;
  align-items: center;
  gap: 3px;
  color: var(--color-text-muted);
  background: color-mix(in srgb, var(--color-surface-hover) 70%, transparent);
  padding: 2px 6px;
  border-radius: 4px;
  border: 1px solid rgba(0,0,0,0.04);
}

.yw-meta-item i {
  font-size: 9px;
  color: var(--color-accent);
}

.yw-meta-item.public { color: #0f766e; background: rgba(20, 184, 166, 0.1); border-color: rgba(20, 184, 166, 0.2); }
.yw-meta-item.private { color: #7c3aed; background: rgba(124, 58, 237, 0.1); border-color: rgba(124, 58, 237, 0.2); }
.yw-meta-item.public i, .yw-meta-item.private i { color: currentColor; }

.view-all-link {
  font-size: 13px;
  font-weight: 500;
  color: var(--color-accent);
  text-decoration: none;
}
.view-all-link:hover {
  text-decoration: underline;
}

.star-btn {
  background: transparent;
  border: none;
  cursor: pointer;
  font-size: 14px;
  padding: 2px 4px;
}

.yw-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.yw-title {
  font-size: 16px;
  font-weight: 500;
  display: flex;
  align-items: center;
  gap: 8px;
}

.yw-tabs {
  display: flex;
  gap: 24px;
  border-bottom: 1px solid var(--color-border);
}

.tab-btn {
  background: transparent;
  border: none;
  color: var(--color-text-muted);
  font-size: 13px;
  font-weight: 500;
  padding: 8px 0;
  cursor: pointer;
  border-bottom: 2px solid transparent;
  margin-bottom: -1px;
}
.tab-btn:hover { color: var(--color-text-primary); }
.tab-btn.active { color: var(--color-accent); border-bottom: 2px solid var(--color-accent); }

.yw-scrollable {
  padding-bottom: 40px;
}

.mt-4 { margin-top: 24px; }
.section-title { font-size: 14px; font-weight: 600; margin-bottom: 16px; color: var(--color-text-primary); }

.yw-cards-row {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
}

.yw-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 16px;
  padding: 20px 24px;
  display: flex;
  align-items: center;
  gap: 20px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.02);
  transition: all 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
}
.yw-card:hover {
  transform: translateY(-4px);
  border-color: var(--color-accent);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.08);
}
.card-icon {
  font-size: 20px;
  color: var(--color-accent);
  width: 40px;
  height: 40px;
  border-radius: 10px;
  background: color-mix(in srgb, var(--color-accent) 10%, transparent);
  display: flex;
  align-items: center;
  justify-content: center;
}
.card-lbl { font-size: 12px; font-weight: 500; color: var(--color-text-muted); margin-bottom: 4px; }
.card-val { font-size: 24px; font-weight: 700; color: var(--color-text-primary); }

.yw-workload-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

.wl-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 14px 18px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  height: 70px;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.01);
  transition: all 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
}
.wl-card:hover {
  transform: translateY(-2px);
  border-color: var(--color-accent);
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.05);
}
.wl-lbl { font-size: 12px; font-weight: 500; color: var(--color-text-muted); display: flex; align-items: center; gap: 6px; }
.dbox { width: 8px; height: 8px; border-radius: 2px; }
.bg-gray { background: var(--color-text-muted); }
.bg-blue { background: var(--color-accent); }
.bg-orange { background: var(--color-warning); }
.bg-green { background: var(--color-success); }
.bg-red { background: var(--color-danger); }
.wl-val { font-size: 22px; font-weight: 700; color: var(--color-text-primary); margin-top: auto;}

.yw-two-cols {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 24px;
}

.chart-col {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 16px;
  padding: 24px;
  transition: all 0.3s ease;
}

.empty-chart {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  height: 150px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  color: var(--color-text-muted);
  font-size: 12px;
}

.chart-icon { font-size: 32px; opacity: 0.3; }

.list-header { display: flex; align-items: center; gap: 8px; font-size: 14px; font-weight: 600; color: var(--color-text-primary); }
.f-icon { color: var(--color-text-muted); font-size: 12px; }
.lh-count { font-size: 12px; font-weight: 400; color: var(--color-text-muted); }

.list-body { border-top: none; }
.recent-list-body {
  border: 0 !important;
  border-radius: 0 !important;
  background: transparent !important;
  padding: 18px !important;
}
.recent-section-title {
  margin-bottom: 16px !important;
}
.personal-state {
  min-height: 96px;
  padding: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  color: var(--color-text-muted);
  text-align: center;
}
.personal-state-error {
  flex-direction: column;
  color: var(--color-danger);
}
.recent-empty-state {
  min-height: 204px;
  flex-direction: column;
  gap: 12px;
  padding: 24px 26px;
  background: transparent;
  border: 0;
  box-shadow: none;
  color: var(--color-text-muted);
}
.recent-empty-icon {
  width: 54px;
  height: 54px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid color-mix(in srgb, var(--color-accent) 18%, transparent);
  border-radius: 14px;
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface));
  color: var(--color-accent);
  font-size: 23px;
  box-shadow: 0 14px 30px rgba(14, 165, 233, 0.12);
}
.recent-empty-state span {
  max-width: 380px;
  font-size: 13px;
  line-height: 1.4;
}
.personal-pagination {
  min-height: 44px;
  margin-top: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 16px;
  color: var(--color-text-muted);
  font-size: 13px;
}
.list-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 16px;
  margin-bottom: 8px;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface);
  transition: all 0.2s cubic-bezier(0.25, 0.8, 0.25, 1);
  cursor: pointer;
}
.list-row:hover {
  background: var(--color-surface-hover);
  border-color: var(--color-accent);
  transform: translateX(4px);
}
.lr-left { display: flex; align-items: center; gap: 16px; }
.lr-id { font-size: 12px; color: var(--color-text-muted); min-width: 45px; font-weight: 600; }
.lr-title { font-size: 13px; font-weight: 500; color: var(--color-text-primary); }
.lr-right { display: flex; align-items: center; gap: 8px; }

.lr-badge { border: 1px solid var(--color-border); border-radius: 8px; padding: 6px 12px; font-size: 12px; color: var(--color-text-muted); display: flex; align-items: center; gap: 6px; transition: all 0.2s; background: var(--color-bg); }
.lr-badge.green { border-color: #064E3B; background: rgba(16, 185, 129, 0.1); color: #10B981; }
.lr-badge i { font-size: 11px; }
.text-orange { color: #F59E0B; }
.avatar-badge { width: 24px; height: 24px; border-radius: 50%; color: #ffffff; display: flex; align-items: center; justify-content: center; font-size: 10px; font-weight: 700; padding: 0; border: 1.5px solid var(--color-surface); box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1); }

.flex-between { display: flex; justify-content: space-between; align-items: center; }
.plane-primary-btn { background: var(--color-accent); color: var(--color-text-inverse); border: none; border-radius: 8px; padding: 8px 16px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.2s; }
.plane-primary-btn:hover { filter: brightness(1.1); }

.p-act-row { display: flex; align-items: flex-start; gap: 16px; padding: 16px 0; border-bottom: 1px solid var(--color-border); }
.p-act-icon { width: 20px; font-size: 12px; color: var(--color-text-muted); text-align: center; margin-top: 2px; }
.p-act-content { display: flex; align-items: center; flex-wrap: wrap; gap: 6px; font-size: 13px; }
.p-ac-text { color: var(--color-text-muted); }
.p-ac-bold { color: var(--color-text-primary); font-weight: 500; }
.p-ac-time { color: var(--color-text-muted); font-size: 11px; }

.yw-sidebar {
  width: 320px;
  background: var(--color-surface);
  border-left: 1px solid var(--color-border);
  display: flex;
  flex-direction: column;
}

.cover-image {
  height: 120px;
  background: var(--color-border);
  background-image: linear-gradient(135deg, var(--color-surface), var(--color-border));
  position: relative;
}

.edit-cover {
  position: absolute;
  top: 16px;
  right: 16px;
  background: rgba(0, 0, 0, 0.5);
  border: none;
  color: var(--color-text-primary);
  border-radius: 6px;
  width: 24px;
  height: 24px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background 0.2s;
}
.edit-cover:hover { background: rgba(0, 0, 0, 0.7); }

.profile-info-scroll {
  flex: 1;
  overflow-y: auto;
  overflow-x: hidden;
}
.profile-info-scroll::-webkit-scrollbar {
  width: 6px;
}
.profile-info-scroll::-webkit-scrollbar-thumb {
  background: var(--color-border);
  border-radius: 4px;
}

.profile-info {
  padding: 0 24px 24px;
  position: relative;
  display: flex;
  flex-direction: column;
}

.avatar-lg {
  position: absolute;
  top: -28px;
  left: 50%;
  transform: translateX(-50%);
  width: 56px;
  height: 56px;
  background: var(--color-accent);
  color: #ffffff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 24px;
  font-weight: 700;
  border-radius: 50%;
  border: 4px solid var(--color-surface);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}

.ps-header {
  margin-top: 40px;
  text-align: center;
}
.user-name { font-size: 16px; font-weight: 700; margin: 0; color: var(--color-text-primary); }
.user-display-name { font-size: 13px; color: var(--color-text-primary); margin: 4px 0 0 0; font-weight: 500; }
.user-handle { font-size: 13px; color: var(--color-text-muted); margin: 2px 0 0 0; }
.role-badges { display: flex; flex-wrap: wrap; justify-content: center; gap: 6px; margin-top: 12px; }
.role-badge { background: color-mix(in srgb, var(--color-accent) 12%, transparent); color: var(--color-accent); font-size: 11px; font-weight: 700; padding: 4px 10px; border-radius: 12px; }

.ps-divider {
  height: 1px;
  background: var(--color-border);
  opacity: 0.6;
  margin: 20px 0;
}

.ps-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.info-row { display: flex; justify-content: space-between; font-size: 13px; margin-bottom: 2px; }
.info-lbl { color: var(--color-text-muted); }
.info-val { color: var(--color-text-primary); font-weight: 500; }
.text-right { text-align: right; }
.time-now { font-size: 11px; color: var(--color-accent); font-weight: 600; }

.stats-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}
.stat-box {
  background: var(--color-surface-hover);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 12px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 4px;
}
.stat-lbl { font-size: 12px; color: var(--color-text-muted); font-weight: 500; }
.stat-val { font-size: 18px; color: var(--color-text-primary); font-weight: 700; }

.progress-bar-container {
  height: 6px;
  background: var(--color-surface-hover);
  border-radius: 4px;
  overflow: hidden;
  margin-top: 4px;
}
.progress-bar-fill {
  height: 100%;
  background: var(--color-success);
  border-radius: 4px;
  transition: width 0.4s ease;
}

.ws-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface-hover);
  cursor: pointer;
  transition: all 0.2s;
}
.ws-card:hover { border-color: var(--color-accent); background: color-mix(in srgb, var(--color-accent) 4%, transparent); }
.ws-card-icon {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  background: color-mix(in srgb, var(--color-warning) 15%, transparent);
  color: var(--color-warning);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
}
.ws-card-info { flex: 1; overflow: hidden; }
.ws-name { font-size: 13px; font-weight: 600; color: var(--color-text-primary); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.ws-meta { font-size: 11px; color: var(--color-text-muted); margin-top: 2px; }
.ws-chevron { font-size: 10px; color: var(--color-text-muted); }

.quick-actions { display: flex; flex-direction: column; gap: 8px; }
.qa-btn {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  border-radius: 10px;
  color: var(--color-text-primary);
  font-size: 13px;
  font-weight: 500;
  text-decoration: none;
  background: var(--color-surface-hover);
  transition: all 0.2s;
}
.qa-btn:hover { background: color-mix(in srgb, var(--color-accent) 10%, transparent); color: var(--color-accent); }
.qa-btn i { font-size: 14px; width: 16px; text-align: center; color: var(--color-text-muted); }
.qa-btn:hover i { color: var(--color-accent); }

.bio-section { gap: 8px; }
.bio-title { font-size: 12px; font-weight: 600; color: var(--color-text-muted); margin: 0; text-transform: uppercase; letter-spacing: 0.5px; }
.bio-text { font-size: 13px; color: var(--color-text-primary); font-style: italic; line-height: 1.5; margin: 0; }





.yw-tabs {
  gap: 8px;
  width: fit-content;
  padding: 6px;
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 14px;
  background: rgba(255, 255, 255, 0.72);
}

.tab-btn {
  margin: 0;
  border: 0;
  border-radius: 10px;
  padding: 9px 13px;
  color: #64748b;
  font-weight: 800;
}

.tab-btn.active {
  border: 0;
  color: #0369a1;
  background: linear-gradient(135deg, rgba(56, 189, 248, 0.16), rgba(14, 165, 233, 0.08));
}

/* Removed local section-title override */

.yw-card,
.wl-card,
.chart-col,
.list-body,
.yw-sidebar {
  border-color: rgba(148, 163, 184, 0.24);
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.94), rgba(255, 255, 255, 0.78)),
    #ffffff;
  box-shadow:
    0 18px 42px rgba(15, 23, 42, 0.07),
    inset 0 1px 0 rgba(255, 255, 255, 0.9);
}

.yw-card {
  position: relative;
  overflow: hidden;
  border-radius: 18px;
}

.yw-card::before {
  content: "";
  position: absolute;
  inset: 0 auto 0 0;
  width: 4px;
  background: linear-gradient(180deg, #38bdf8, #22c55e);
}

.yw-card:hover,
.wl-card:hover,
.chart-col:hover {
  transform: translateY(-3px);
  border-color: rgba(14, 165, 233, 0.34);
  box-shadow: 0 24px 58px rgba(15, 23, 42, 0.1);
}

.card-icon {
  border-radius: 14px;
  background: linear-gradient(135deg, rgba(56, 189, 248, 0.18), rgba(14, 165, 233, 0.08));
  color: #0284c7;
}

.card-lbl,
.wl-lbl {
  color: #64748b;
  font-weight: 800;
}

.card-val,
.wl-val {
  color: #0f172a;
  font-weight: 900;
}

.wl-card {
  border-radius: 16px;
  height: auto;
  min-height: 78px;
}

.dbox {
  width: 9px;
  height: 9px;
  border-radius: 999px;
}

.chart-col {
  border-radius: 20px;
  padding: 26px;
}

.list-body {
  overflow: hidden;
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 18px;
}

.list-row {
  border-color: rgba(148, 163, 184, 0.16);
  transition: background 160ms ease, transform 160ms ease;
}

.list-row:hover {
  background: rgba(14, 165, 233, 0.06);
  transform: translateX(2px);
}

.lr-id {
  color: #0284c7;
  font-weight: 900;
}

.lr-title {
  color: #0f172a;
  font-weight: 800;
}

.lr-badge {
  border-color: rgba(148, 163, 184, 0.24);
  border-radius: 10px;
  background: rgba(241, 245, 249, 0.82);
  color: #475569;
  font-weight: 800;
}

.yw-sidebar {
  border-left: 1px solid rgba(148, 163, 184, 0.24);
  padding-top: 32px;
}

.cover-image {
  height: 132px;
  background:
    linear-gradient(135deg, #dbeafe, #e0f2fe 42%, #dcfce7);
}

.edit-cover {
  width: 30px;
  height: 30px;
  border-radius: 10px;
  background: rgba(15, 23, 42, 0.64);
}

.avatar-lg {
  border-color: #ffffff;
  box-shadow: 0 12px 32px rgba(15, 23, 42, 0.18);
}

.user-name {
  color: #0f172a;
  font-size: 18px;
  font-weight: 900;
}

.user-handle,
.info-lbl {
  color: #64748b;
}

.info-val {
  color: #0f172a;
  font-weight: 800;
}

.workspace-row {
  border-top-color: rgba(148, 163, 184, 0.2);
  border-radius: 12px;
  padding: 14px 10px;
  background: rgba(248, 250, 252, 0.78);
}

@media (max-width: 1100px) {

  .yw-sidebar {
    width: auto;
    margin: 0 22px 22px;
    border: 1px solid rgba(148, 163, 184, 0.24);
    border-radius: 18px;
    overflow: hidden;
  }
}



[data-theme='dark'] .yw-title,
[data-theme='dark'] .section-title,
[data-theme='dark'] .card-val,
[data-theme='dark'] .wl-val,
[data-theme='dark'] .lr-title,
[data-theme='dark'] .user-name,
[data-theme='dark'] .info-val {
  color: #f8fafc;
}

[data-theme='dark'] .yw-tabs,
[data-theme='dark'] .yw-card,
[data-theme='dark'] .wl-card,
[data-theme='dark'] .chart-col,
[data-theme='dark'] .list-body,
[data-theme='dark'] .yw-sidebar {
  border-color: rgba(148, 163, 184, 0.2);
  background:
    linear-gradient(180deg, rgba(30, 41, 59, 0.92), rgba(15, 23, 42, 0.86)),
    #0f172a;
  box-shadow:
    0 18px 42px rgba(0, 0, 0, 0.28),
    inset 0 1px 0 rgba(255, 255, 255, 0.05);
}

[data-theme='dark'] .tab-btn {
  color: #94a3b8;
}

[data-theme='dark'] .tab-btn.active {
  color: #7dd3fc;
  background: linear-gradient(135deg, rgba(56, 189, 248, 0.18), rgba(14, 165, 233, 0.08));
}

[data-theme='dark'] .card-lbl,
[data-theme='dark'] .wl-lbl,
[data-theme='dark'] .user-handle,
[data-theme='dark'] .info-lbl {
  color: #94a3b8;
}

[data-theme='dark'] .list-row {
  border-color: rgba(148, 163, 184, 0.16);
}

[data-theme='dark'] .list-row:hover {
  background: rgba(56, 189, 248, 0.08);
}

[data-theme='dark'] .lr-badge,
[data-theme='dark'] .workspace-row {
  border-color: rgba(148, 163, 184, 0.2);
  background: rgba(15, 23, 42, 0.7);
  color: #cbd5e1;
}

[data-theme='dark'] .cover-image {
  background:
    linear-gradient(135deg, #0f172a, #164e63 42%, #064e3b);
}



/* Removed yw-main override */

.yw-title {
  font-size: clamp(22px, 2vw, 30px) !important;
  line-height: 1.12 !important;
}

.yw-tabs {
  border-radius: 10px !important;
  padding: 4px !important;
  margin: 18px 0 24px !important;
}

.tab-btn {
  min-height: 32px !important;
  padding: 6px 10px !important;
  border-radius: 8px !important;
  font-size: 12.5px !important;
}

/* Removed 15px section-title override */

.overview-grid,
.workload-grid,
.charts-grid {
  gap: 14px !important;
  margin-bottom: 24px !important;
}

.yw-card,
.wl-card,
.chart-col,
.list-body,
.yw-sidebar {
  border-radius: 10px !important;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.06) !important;
}

.yw-card {
  min-height: 92px !important;
  padding: 16px !important;
  gap: 14px !important;
}

.card-icon {
  width: 38px !important;
  height: 38px !important;
  border-radius: 9px !important;
  font-size: 18px !important;
}

.card-val,
.wl-val {
  font-size: 24px !important;
}

.wl-card {
  min-height: 66px !important;
  padding: 14px 16px !important;
}

.chart-col {
  padding: 18px !important;
}

.list-row {
  padding: 10px 12px !important;
  min-height: 52px !important;
}

.yw-sidebar {
  width: 280px !important;
}

.cover-image {
  height: 98px !important;
}

.profile-block,
.user-meta,
.workspace-row {
  padding-left: 16px !important;
  padding-right: 16px !important;
}

@media (max-width: 1100px) {
  

  .yw-sidebar {
    width: 100% !important;
  }
}

@media (max-width: 720px) {

  

  .overview-grid,
  .workload-grid,
  .charts-grid {
    grid-template-columns: 1fr !important;
  }

  .yw-tabs {
    overflow-x: auto !important;
  }

  .yw-cards-row,
  .yw-workload-row {
    grid-template-columns: 1fr !important;
  }

  .list-row,
  .lr-left,
  .lr-right {
    min-width: 0;
  }

  .list-row {
    align-items: flex-start;
    gap: 10px;
  }

  .lr-left {
    flex: 1;
  }

  .lr-title {
    overflow-wrap: anywhere;
  }

  .lr-right {
    flex-wrap: wrap;
    justify-content: flex-end;
  }
}

/* Match For You page wrapper spacing exactly; Your Work only adds a two-column grid inside it. */
.jira-dashboard {
  --sa-page-x: 18px;
  position: relative;
  width: 100%;
  max-width: 1120px;
  min-height: calc(100vh - 60px);
  margin: 0 auto;
  padding: 18px var(--sa-page-x, 24px) 30px !important;
  display: flex;
  flex-direction: column !important;
  gap: 22px !important;
  background:
    linear-gradient(180deg, #f8fcff 0%, #eef6fb 48%, #f8fafc 100%) !important;
}

.yw-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 280px;
  gap: 24px;
  align-items: start;
  width: 100%;
  position: relative;
}

.main-content-column {
  display: grid;
  gap: 0;
  width: 100%;
  max-width: 100%;
  min-width: 0;
  padding-bottom: 18px;
}

.main-content-column > section {
  margin: 0 !important;
}

/* Keep the inner cards visible while removing the large outer panels. */
.your-work-flat-section {
  background: transparent !important;
  border: 0 !important;
  box-shadow: none !important;
}

.yw-content-card {
  display: flex;
  flex-direction: column;
  gap: 32px;
  min-width: 0;
  margin: 0 !important;
  padding: 0 !important;
  border: 0 !important;
  border-radius: 0 !important;
  background: transparent !important;
  box-shadow: none !important;
}

.yw-header {
  margin-bottom: 0 !important;
}

.yw-scrollable {
  display: flex;
  flex-direction: column;
  gap: 32px;
  padding-bottom: 0;
}

.yw-section,
.chart-col {
  padding: 0 !important;
  margin: 0 !important;
  border: 0 !important;
  border-radius: 0 !important;
  background: transparent !important;
  box-shadow: none !important;
}

.yw-section .section-title,
.chart-col .section-title {
  font-size: 15px;
  line-height: 1.2;
  text-shadow: none;
}

.yw-card,
.wl-card,
.empty-chart,
.list-body,
.list-row {
  border-color: rgba(148, 163, 184, 0.18) !important;
  background: transparent !important;
  box-shadow: none !important;
}

.yw-card,
.wl-card {
  border-radius: 10px !important;
  transform: none !important;
}

.yw-card::before {
  display: none !important;
}

.yw-card:hover,
.wl-card:hover,
.chart-col:hover {
  transform: none !important;
  box-shadow: none !important;
}

.list-body {
  overflow: visible;
  border: 0 !important;
  border-radius: 0 !important;
}

.list-row {
  border-width: 1px 0 0 0 !important;
  border-radius: 0 !important;
  margin-bottom: 0 !important;
}

.list-row:hover {
  background: rgba(14, 165, 233, 0.04) !important;
  transform: none !important;
}

.yw-sidebar {
  position: sticky;
  top: 18px;
  width: 280px !important;
  max-height: none;
  align-self: start;
  overflow: hidden;
  border: 1px solid rgba(148, 163, 184, 0.24) !important;
  border-radius: 10px !important;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.94), rgba(255, 255, 255, 0.82)),
    #ffffff !important;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.06) !important;
  padding-top: 0;
}

.profile-info-scroll {
  overflow: visible;
}

.ps-divider {
  margin: 18px 0;
}

.stat-box,
.ws-card,
.qa-btn,
.progress-bar-container {
  background: transparent;
}

[data-theme='dark'] .jira-dashboard {
  background:
    linear-gradient(180deg, #0f172a 0%, #111827 52%, #0b1120 100%) !important;
}

[data-theme='dark'] .yw-content-card,
[data-theme='dark'] .yw-sidebar {
  border-color: rgba(148, 163, 184, 0.2) !important;
  background:
    linear-gradient(180deg, rgba(30, 41, 59, 0.92), rgba(15, 23, 42, 0.86)),
    #0f172a !important;
  box-shadow: 0 18px 42px rgba(0, 0, 0, 0.28) !important;
}

@media (max-width: 1100px) {
  .yw-grid {
    grid-template-columns: 1fr;
  }

  .yw-sidebar {
    position: static;
    width: 100% !important;
    margin: 0 !important;
  }
}

@media (max-width: 720px) {
  .jira-dashboard {
    padding-inline: var(--sa-page-x, 16px) !important;
  }

  .yw-content-card {
    padding: 0 !important;
  }

  .yw-cards-row,
  .yw-workload-row,
  .yw-two-cols {
    grid-template-columns: 1fr !important;
  }
}

/* Reference alignment: Your Work uses the For You shell, card, header, and section scale. */
.jira-dashboard {
  background:
    linear-gradient(180deg, #f8fcff 0%, #eef6fb 48%, #f8fafc 100%) !important;
}

.yw-grid {
  gap: 24px;
}

.yw-content-card {
  gap: 24px !important;
  padding: 0 !important;
  border: 0 !important;
  border-radius: 0 !important;
  background: transparent !important;
  box-shadow: none !important;
}

.yw-header {
  min-height: 34px;
  margin: 0 !important;
}

.yw-header .section-title {
  display: flex;
  align-items: center;
  gap: 12px;
  margin: 0;
  color: #0f172a;
  font-size: clamp(21px, 1.65vw, 28px) !important;
  font-weight: 900;
  line-height: 1.12 !important;
  letter-spacing: -0.015em;
  text-shadow: 0 1px 0 rgba(255, 255, 255, 0.74);
}

.yw-tabs {
  display: flex;
  gap: 6px !important;
  width: max-content !important;
  max-width: 100%;
  min-height: 42px;
  margin: 0 !important;
  padding: 4px !important;
  overflow-x: auto;
  border: 1px solid rgba(148, 163, 184, 0.2) !important;
  border-radius: 9px !important;
  background: transparent !important;
  box-shadow: none !important;
}

.tab-btn {
  flex: 0 0 auto;
  min-height: 34px !important;
  padding: 0 16px !important;
  border-radius: 7px !important;
  color: #475569 !important;
  font-size: 12.5px !important;
  font-weight: 800 !important;
  line-height: 1;
  white-space: nowrap;
}

.tab-btn.active {
  color: #0369a1 !important;
  background:
    linear-gradient(135deg, rgba(34, 211, 238, 0.20), rgba(45, 212, 191, 0.14)) !important;
}

.yw-scrollable {
  gap: 28px !important;
}

.yw-section,
.chart-col {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.yw-section-title,
.list-header {
  display: flex;
  align-items: center;
  gap: 10px;
  margin: 0 0 2px !important;
  color: #0f172a;
  font-size: 15px;
  font-weight: 900;
  line-height: 1.2;
  letter-spacing: 0;
}

.yw-section-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: 0 0 auto;
  width: 30px;
  height: 30px;
  border-radius: 8px;
  background: linear-gradient(135deg, #0ea5e9, #38bdf8);
  color: #ffffff;
  font-size: 13px;
  box-shadow: 0 2px 8px rgba(14, 165, 233, 0.25);
}

.lh-title {
  color: #0f172a;
  font-size: 15px;
  font-weight: 900;
}

.lh-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 24px;
  height: 22px;
  padding: 0 8px;
  border-radius: 999px;
  background: #f1f5f9;
  border: 1px solid #e2e8f0;
  color: #64748b;
  font-size: 12px;
  font-weight: 800;
}

.yw-cards-row,
.yw-workload-row,
.yw-two-cols {
  gap: 14px !important;
}

.yw-card,
.wl-card {
  border: 1px solid rgba(148, 163, 184, 0.18) !important;
  border-radius: 10px !important;
  background: rgba(255, 255, 255, 0.78) !important;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.06) !important;
}

.yw-card {
  min-height: 92px !important;
  padding: 16px !important;
}

.wl-card {
  min-height: 66px !important;
  padding: 14px 16px !important;
}

.card-icon {
  width: 38px !important;
  height: 38px !important;
  border-radius: 9px !important;
  background: linear-gradient(135deg, rgba(56, 189, 248, 0.18), rgba(14, 165, 233, 0.08)) !important;
  color: #0284c7 !important;
  font-size: 18px !important;
}

.card-lbl,
.wl-lbl {
  color: #64748b !important;
  font-size: 12px !important;
  font-weight: 800 !important;
}

.card-val,
.wl-val {
  color: #0f172a !important;
  font-size: 24px !important;
  font-weight: 900 !important;
  line-height: 1.1;
}

.chart-col {
  padding: 18px !important;
  border: 1px solid rgba(148, 163, 184, 0.18) !important;
  border-radius: 10px !important;
  background: rgba(255, 255, 255, 0.78) !important;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.06) !important;
}

.empty-chart {
  border-color: rgba(148, 163, 184, 0.18) !important;
  border-radius: 10px !important;
  background: rgba(248, 250, 252, 0.78) !important;
}

.list-body {
  overflow: hidden !important;
  border: 1px solid rgba(148, 163, 184, 0.18) !important;
  border-radius: 10px !important;
  background: rgba(255, 255, 255, 0.78) !important;
}

.recent-list-body {
  border: 0 !important;
  border-radius: 0 !important;
  background: transparent !important;
  padding: 18px !important;
}

.list-row {
  min-height: 52px !important;
  margin: 0 !important;
  padding: 10px 12px !important;
  border-width: 0 0 1px 0 !important;
  border-color: rgba(148, 163, 184, 0.16) !important;
  border-radius: 0 !important;
  background: transparent !important;
}

.list-row:last-child {
  border-bottom: 0 !important;
}

.list-row:hover {
  background: rgba(14, 165, 233, 0.04) !important;
  transform: none !important;
}

.lr-title {
  color: #0f172a !important;
  font-size: 13px !important;
  font-weight: 800 !important;
}

.lr-id,
.lr-badge {
  font-size: 12px !important;
  font-weight: 800 !important;
}

.yw-sidebar {
  border-radius: 10px !important;
  border: 1px solid rgba(148, 163, 184, 0.24) !important;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.94), rgba(255, 255, 255, 0.82)),
    #ffffff !important;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.06) !important;
}

[data-theme='dark'] .jira-dashboard {
  background:
    linear-gradient(180deg, #06111f, #0f172a 52%, #101827) !important;
}

[data-theme='dark'] .yw-content-card,
[data-theme='dark'] .yw-sidebar,
[data-theme='dark'] .yw-tabs,
[data-theme='dark'] .yw-card,
[data-theme='dark'] .wl-card,
[data-theme='dark'] .chart-col,
[data-theme='dark'] .list-body {
  border-color: rgba(148, 163, 184, 0.2) !important;
  background:
    linear-gradient(180deg, rgba(30, 41, 59, 0.92), rgba(15, 23, 42, 0.86)),
    #0f172a !important;
  box-shadow: 0 18px 42px rgba(0, 0, 0, 0.28) !important;
}

[data-theme='dark'] .yw-header .section-title,
[data-theme='dark'] .yw-section-title,
[data-theme='dark'] .lh-title,
[data-theme='dark'] .card-val,
[data-theme='dark'] .wl-val,
[data-theme='dark'] .lr-title {
  color: #f8fafc !important;
  text-shadow: none !important;
}

[data-theme='dark'] .tab-btn,
[data-theme='dark'] .card-lbl,
[data-theme='dark'] .wl-lbl,
[data-theme='dark'] .lh-count {
  color: #94a3b8 !important;
}

[data-theme='dark'] .tab-btn.active {
  color: #7dd3fc !important;
  background: linear-gradient(135deg, rgba(56, 189, 248, 0.18), rgba(14, 165, 233, 0.08)) !important;
}

[data-theme='dark'] .list-row {
  border-color: rgba(148, 163, 184, 0.16) !important;
}

.yw-content-card,
[data-theme='dark'] .yw-content-card {
  padding: 0 !important;
  border: 0 !important;
  border-radius: 0 !important;
  background: transparent !important;
  box-shadow: none !important;
}

.recommended-spaces-section,
[data-theme='dark'] .recommended-spaces-section {
  padding: 0 !important;
}

.yw-tabs,
[data-theme='dark'] .yw-tabs {
  background: transparent !important;
  box-shadow: none !important;
}

@media (max-width: 720px) {
  .yw-content-card {
    padding: 0 !important;
  }

  .yw-tabs {
    width: 100% !important;
  }
}

/* Fill the workspace width while keeping the profile column fixed and nearby. */
.jira-dashboard {
  max-width: none !important;
  margin: 0 !important;
  padding: 0 18px 18px !important;
  background: transparent !important;
}

.yw-grid {
  grid-template-columns: minmax(0, 1fr) 280px !important;
  gap: 16px !important;
}

.yw-sidebar {
  width: 280px !important;
  min-width: 280px;
  max-width: 280px;
}

@media (min-width: 861px) {
  .yw-grid {
    display: block !important;
    padding-right: 296px;
  }

  .yw-sidebar {
    position: absolute !important;
    top: 0;
    right: 0;
  }
}

.yw-section-title,
.list-header {
  gap: 7px;
}

.yw-section-icon {
  width: 22px;
  height: 22px;
  border-radius: 6px;
  font-size: 10px;
  box-shadow: none;
}

.card-icon {
  width: 30px !important;
  height: 30px !important;
  border-radius: 7px !important;
  font-size: 13px !important;
}

.main-content-column {
  gap: 18px !important;
}

.yw-content-card {
  gap: 18px !important;
}

.yw-tabs {
  margin-top: 18px !important;
  display: flex !important;
  flex-wrap: wrap !important;
  align-content: flex-start;
  width: 100% !important;
  max-width: 100% !important;
  max-height: 82px;
  overflow: hidden !important;
  overflow-x: hidden !important;
  overflow-y: hidden !important;
}

.yw-tabs .tab-btn {
  flex: 0 1 auto !important;
}

.yw-scrollable {
  gap: 18px !important;
}

.yw-scrollable .yw-section {
  gap: 18px !important;
}

.yw-scrollable .section-title,
.yw-scrollable .yw-section-title {
  margin: 0 !important;
}

.yw-scrollable .mt-4 {
  margin-top: 0 !important;
}

@media (max-width: 860px) {
  .jira-dashboard {
    padding: 12px !important;
  }

  .yw-grid {
    grid-template-columns: minmax(0, 1fr) !important;
  }

  .yw-sidebar {
    position: static;
    justify-self: start;
    width: 280px !important;
    min-width: 280px;
    max-width: 280px;
    margin: 0 !important;
  }
}
</style>
