<script setup>
import { ref, onMounted, computed, watch } from 'vue'
import axiosClient from '@/api/axiosClient'

import { useActivityStore } from '@/store/useActivityStore'
import apexchart from 'vue3-apexcharts'
import { ElNotification, ElMessage } from 'element-plus'
import { useI18nStore } from '@/store/useI18nStore'
import { useProjectStore } from '@/store/useProjectStore'
import { useWorkTaskStore } from '@/store/useWorkTaskStore'
import TaskDetailModal from '@/components/TaskDetailModal.vue'
import { translateDemoText } from '@/utils/demoContentLocale'
import { useRouter } from 'vue-router'

const router = useRouter()
const i18nStore = useI18nStore()
const t = (key) => i18nStore.t(key)
const projectStore = useProjectStore()
const workTaskStore = useWorkTaskStore()
const activeTab = ref('Summary')
const tabs = ['Summary', 'Assigned', 'Created', 'Subscribed', 'Starred', 'Activity']

// ── Chức năng từ For You ──────────────────────────────────────
// Recommended Spaces
const spaces = ref([])
const loadingSpaces = ref(false)
const language = computed(() => i18nStore.language || 'vi')
const demoText = (value) => translateDemoText(value, language.value)

const sortedSpaces = computed(() => {
  return spaces.value.map(space => {
    const spaceTasks = myTasks.value.filter(t => t.projectId === space.id || (t.projectName && t.projectName === space.name)).length
    return { ...space, displayTaskCount: spaceTasks > 0 ? spaceTasks : (space.taskCount || 0) }
  }).sort((a, b) => {
    const aStarred = projectStore.favoriteProjects.some(p => p.id === a.id)
    const bStarred = projectStore.favoriteProjects.some(p => p.id === b.id)
    if (aStarred !== bStarred) return aStarred ? -1 : 1
    return new Date(b.createdAt || 0) - new Date(a.createdAt || 0)
  })
})

const fetchSpaces = async () => {
  loadingSpaces.value = true
  try {
    const list = await projectStore.fetchAllProjects(true)
    spaces.value = list.map((p, index) => ({
      id: p.id,
      name: p.name,
      key: p.key || p.name.substring(0, 4).toUpperCase(),
      description: p.description || 'Project space',
      cover: p.cover || '#3b82f6',
      icon: p.icon || '📦',
      taskCount: p.taskCount ?? p.TotalTasks ?? p.totalTasks ?? 0,
      createdAt: p.createdAt || null,
    }))
  } catch (e) {
    console.error('fetchSpaces error:', e)
  } finally {
    loadingSpaces.value = false
  }
}

const toggleSpaceStar = async (space) => {
  try {
    const isCurrentlyFav = projectStore.favoriteProjects.some(p => p.id === space.id)
    await projectStore.updateFavorite(space.id, !isCurrentlyFav)
    ElMessage.success(isCurrentlyFav ? 'Đã bỏ đánh dấu Space' : 'Đã đánh dấu Space')
  } catch {
    ElMessage.error('Không thể cập nhật')
  }
}

// Task starring
const toggleTaskStar = (task) => {
  workTaskStore.toggleTaskStar(task)
  ElMessage.success(workTaskStore.isTaskStarred(task.id) ? 'Đã đánh dấu task' : 'Đã bỏ đánh dấu task')
}
const isTaskStarred = (taskId) => workTaskStore.isTaskStarred(taskId)

// Task Detail Modal
const selectedTask = ref(null)
const taskDetailHistory = ref([])
const projectMembers = ref([])
const currentProjectRole = ref('member')

const timeAgo = (dateStr) => {
  if (!dateStr || dateStr.startsWith('0001-01-01')) return 'vừa xong'
  const date = new Date(dateStr)
  if (isNaN(date.getTime()) || date.getFullYear() <= 1970) return 'vừa xong'
  const seconds = Math.floor((new Date() - date) / 1000)
  if (seconds < 0) return 'vừa xong'
  let interval = seconds / 31536000
  if (interval >= 1) return `${Math.floor(interval)} năm trước`
  interval = seconds / 2592000
  if (interval >= 1) return `${Math.floor(interval)} tháng trước`
  interval = seconds / 86400
  if (interval >= 1) return `${Math.floor(interval)} ngày trước`
  interval = seconds / 3600
  if (interval >= 1) return `${Math.floor(interval)} giờ trước`
  interval = seconds / 60
  if (interval >= 1) return `${Math.floor(interval)} phút trước`
  return 'vừa xong'
}

const openTaskDetail = async (task) => {
  try {
    const mRes = await axiosClient.get(`/projects/${task.projectId}/members`)
    projectMembers.value = (mRes.data?.data || []).map(member => ({
      ...member,
      userId: member.userId || member.id,
      fullName: member.fullName || member.name || member.email,
      projectRole: member.projectRole || member.ProjectRole || member.myRole || ''
    }))
  } catch {
    projectMembers.value = []
  }
  taskDetailHistory.value = []
  selectedTask.value = workTaskStore.normalizeTaskRecord(task, task.projectId)
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

const updateTaskFromModal = async (task, field, value) => {
  if (!task?.id) return
  try {
    const usesPut = ['title', 'description', 'priority', 'assignedUserId'].includes(field)
    let payload = { [field]: value }
    if (usesPut) {
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
        ...{ [field]: value }
      }
    }
    if (usesPut) {
      await axiosClient.put(`/projects/${task.projectId}/WorkTasks/${task.id}`, payload)
    } else {
      await axiosClient.patch(`/projects/${task.projectId}/WorkTasks/${task.id}`, payload)
    }
    const idx = myTasks.value.findIndex(item => item.id === task.id)
    if (idx !== -1) myTasks.value[idx][field] = value
    if (selectedTask.value?.id === task.id) selectedTask.value[field] = value
  } catch {
    ElMessage.error('Không thể cập nhật công việc')
  }
}

// Search / Filter / Sort for task tabs
const taskSearch = ref('')
const statusFilter = ref('all')
const sortOption = ref('updated')

watch(activeTab, () => {
  taskSearch.value = ''
  statusFilter.value = 'all'
  sortOption.value = 'updated'
})
// ── Kết thúc chức năng từ For You ────────────────────────────
const tabLabel = (tab) => t(`yourWork.tabs.${tab.toLowerCase()}`)

const myTasks = ref([])
const loading = ref(false)
const actStore = useActivityStore()
const selectedProjectId = ref(null)
const projectList = ref([])

const currentUserId = computed(() => {
  const userStr = localStorage.getItem('user')
  return userStr ? JSON.parse(userStr).id : null
})

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
        fullName: data.fullName || '—',
        email: data.email || '—',
        avatarUrl: data.avatarUrl,
        avatarColor: data.avatarColor,
        initials: data.initials || (data.fullName ? data.fullName.substring(0, 1).toUpperCase() : (data.email ? data.email.substring(0, 1).toUpperCase() : '—')),
        joinedOn: '—',
        timezone: '—'
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
  try {
    loading.value = true
    const res = await axiosClient.get('/tasks/search')
    myTasks.value = res.data?.data || []
    actStore.fetchRecentActivities()

    const existingIds = new Set(actStore.activities.map(activity => activity.id))
    let added = false
    myTasks.value.forEach(task => {
      const id = `db-${task.id}`
      if (!existingIds.has(id)) {
        const action = task.reporterId === currentUserId.value ? 'Created' : 'Assigned to'
        actStore.activities.push({
          id,
          icon: 'fa-solid fa-list-check',
          text: `${action} work item`,
          bold: `"${task.title}"`,
          time: new Date(task.createdAt).toLocaleString(),
          _ts: new Date(task.createdAt).getTime()
        })
        added = true
      }
    })

    if (added) {
      actStore.activities.forEach(activity => {
        if (!activity._ts) {
          const ts = Date.parse(activity.time)
          activity._ts = Number.isNaN(ts) ? Date.now() : ts
        }
      })
      actStore.activities.sort((left, right) => right._ts - left._ts)
      actStore.activities = actStore.activities.slice(0, 50)
      localStorage.setItem('nexus_activities', JSON.stringify(actStore.activities))
    }
  } catch (error) {
    console.error('Failed to load tasks:', error)
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await fetchProjects()
  fetchProfile()
  fetchMyTasks()
  fetchSpaces()
  workTaskStore.fetchStarredTasks().catch(() => {})
})

const overview = computed(() => ({
  created: myTasks.value.filter(task => task.reporterId === currentUserId.value).length,
  assigned: myTasks.value.filter(task => task.assignedUserId === currentUserId.value).length,
  subscribed: myTasks.value.filter(task => task.isSubscribed).length
}))

const workload = computed(() => {
  let backlog = 0
  let notStarted = 0
  let workingOn = 0
  let completed = 0
  let canceled = 0

  myTasks.value.forEach(task => {
    const status = (task.statusName || 'BACKLOG').toUpperCase().trim()
    if (status === 'BACKLOG') backlog += 1
    else if (status === 'TODO' || status === 'TO DO') notStarted += 1
    else if (status === 'IN PROGRESS' || status === 'INPROGRESS') workingOn += 1
    else if (status === 'DONE') completed += 1
    else if (status === 'CANCELLED' || status === 'CANCELED') canceled += 1
    else backlog += 1
  })

  return { backlog, notStarted, workingOn, completed, canceled }
})

const prioritySeries = computed(() => [
  myTasks.value.filter(task => task.priority === 1).length,
  myTasks.value.filter(task => task.priority === 2).length,
  myTasks.value.filter(task => task.priority === 3).length,
  myTasks.value.filter(task => task.priority === 4).length
])

const priorityChartOptions = computed(() => ({
  chart: { type: 'pie', toolbar: { show: false }, background: 'transparent' },
  theme: { mode: 'light' },
  labels: ['Urgent', 'High', 'Medium', 'Low'],
  dataLabels: {
    enabled: true,
    style: { fontSize: '11px', fontWeight: 800, colors: ['#ffffff'] },
    dropShadow: { enabled: false }
  },
  legend: {
    position: 'bottom',
    labels: { colors: '#64748b' }
  },
  colors: ['#f43f5e', '#f97316', '#38bdf8', '#94a3b8'],
  stroke: { colors: ['#ffffff'], width: 3 },
  tooltip: { theme: 'light' }
}))

const stateSeries = computed(() => [{
  name: 'Work items',
  data: [
    workload.value.backlog,
    workload.value.notStarted,
    workload.value.workingOn,
    workload.value.completed
  ]
}])

const stateChartOptions = computed(() => ({
  chart: { type: 'bar', toolbar: { show: false }, background: 'transparent' },
  theme: { mode: 'light' },
  plotOptions: { bar: { horizontal: true, borderRadius: 4, barHeight: '48%', distributed: true } },
  dataLabels: { enabled: false },
  legend: { show: false },
  colors: ['#94a3b8', '#a78bfa', '#38bdf8', '#22c55e'],
  grid: { borderColor: '#e2e8f0', strokeDashArray: 4 },
  xaxis: {
    categories: ['Backlog', 'Not Started', 'In Progress', 'Completed'],
    labels: { style: { colors: '#64748b' } },
    axisBorder: { show: false },
    axisTicks: { show: false }
  },
  yaxis: {
    labels: { style: { colors: '#94a3b8' } }
  },
  tooltip: { theme: 'light' }
}))

const recentActivity = computed(() => {
  return actStore.activities.map(activity => ({
    id: activity.id,
    text: `${activity.text} ${activity.bold || ''}`.trim(),
    time: activity.time
  }))
})

const listData = computed(() => {
  let list = myTasks.value
  if (activeTab.value === 'Assigned') {
    list = myTasks.value.filter(task => task.assignedUserId === currentUserId.value)
  } else if (activeTab.value === 'Created') {
    list = myTasks.value.filter(task => task.reporterId === currentUserId.value)
  } else if (activeTab.value === 'Subscribed') {
    list = myTasks.value.filter(task => task.isSubscribed)
  } else if (activeTab.value === 'Starred') {
    const starred = workTaskStore.starredTasks.map(v => myTasks.value.find(t => t.id === (v.itemId || v.id))).filter(Boolean)
    list = starred
  }

  // Search filter
  if (taskSearch.value.trim()) {
    const q = taskSearch.value.toLowerCase().trim()
    list = list.filter(task =>
      (task.title || '').toLowerCase().includes(q) ||
      (task.sequenceId || '').toLowerCase().includes(q) ||
      (task.projectName || '').toLowerCase().includes(q)
    )
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

  // Sort
  list = [...list].sort((a, b) => {
    if (sortOption.value === 'created') return new Date(b.createdAt || 0) - new Date(a.createdAt || 0)
    if (sortOption.value === 'priority') return (a.priority || 3) - (b.priority || 3)
    return new Date(b.updatedAt || b.createdAt || 0) - new Date(a.updatedAt || a.createdAt || 0)
  })

  return list.map(task => ({
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

const updateTaskProperty = async (task, field, value) => {
  try {
    const index = myTasks.value.findIndex(item => item.id === task.id)
    if (index !== -1) {
      myTasks.value[index][field] = value

      const updatePayload = { [field]: value }
      if (task.projectId) {
        await axiosClient.patch(`/projects/${task.projectId}/WorkTasks/${task.id}`, updatePayload)
      }

      let activityText = `Updated ${field} to ${value}`
      if (field === 'statusName') activityText = `Changed status to ${value}`
      if (field === 'priority') {
        activityText = `Changed priority to ${value === 1 ? 'Urgent' : value === 2 ? 'High' : value === 3 ? 'Normal' : 'Low'}`
      }
      actStore.logActivity(activityText, `on ${task.sequenceId || task.id}`, 'fa-solid fa-pen-to-square')
    }
  } catch (error) {
    console.error('Failed to update task:', error)
  }
}

const pageActivities = computed(() => actStore.activities)

const downloadWordActivity = () => {
  let htmlContent = `
  <html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:w='urn:schemas-microsoft-com:office:word' xmlns='http://www.w3.org/TR/REC-html40'>
  <head><meta charset='utf-8'><title>Activity Log</title></head><body>
  <h2>Activity History</h2>
  <ul>
  `

  actStore.activities.forEach(activity => {
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
import UserAvatar from '@/components/common/UserAvatar.vue'
</script>

<template>
  <div>
    <div class="yw-container">
      <div class="yw-main">
        <header class="yw-header flex-between">
          <span class="yw-title"><i class="fa-regular fa-user"></i> Your work</span>
        </header>

        <!-- ── Recommended Spaces (từ For You) ── -->
        <section class="yw-spaces-section">
          <div class="yw-spaces-header">
            <h2 class="yw-spaces-title"><i class="fa-solid fa-layer-group"></i> Không gian làm việc</h2>
            <a class="yw-spaces-viewall" @click.prevent="router.push('/spaces')" href="#">Xem tất cả</a>
          </div>
          <div v-if="loadingSpaces" class="yw-spaces-loading">
            <i class="fa-solid fa-spinner fa-spin"></i> Đang tải...
          </div>
          <div v-else-if="sortedSpaces.length === 0" class="yw-spaces-empty">
            <i class="fa-solid fa-folder-open"></i>
            <span>Chưa có project nào</span>
          </div>
          <div v-else class="yw-spaces-row">
            <div
              v-for="space in sortedSpaces.slice(0, 4)"
              :key="space.id"
              class="yw-space-card"
              @click="router.push(`/space/${space.id}`)"
            >
              <div class="yw-sc-icon" :style="{ backgroundColor: space.cover }">
                <span>{{ space.icon || '📦' }}</span>
              </div>
              <div class="yw-sc-info">
                <h3 class="yw-sc-name" :title="demoText(space.name)">{{ demoText(space.name) }}</h3>
                <p class="yw-sc-desc">{{ demoText(space.description) }} · {{ space.displayTaskCount }} tasks</p>
              </div>
              <button
                class="yw-sc-star"
                :class="{ starred: projectStore.favoriteProjects.some(p => p.id === space.id) }"
                @click.stop="toggleSpaceStar(space)"
                :title="projectStore.favoriteProjects.some(p => p.id === space.id) ? 'Bỏ đánh dấu' : 'Đánh dấu'"
              >
                <i :class="projectStore.favoriteProjects.some(p => p.id === space.id) ? 'fa-solid fa-star' : 'fa-regular fa-star'"></i>
              </button>
            </div>
          </div>
        </section>

        <div class="yw-tabs">
          <button
            v-for="tab in tabs"
            :key="tab"
            class="tab-btn"
            :class="{ active: activeTab === tab }"
            @click="activeTab = tab"
          >
            <span>{{ tabLabel(tab) }}</span>
            <span
              v-if="tab === 'Starred' && workTaskStore.starredTasks.length > 0"
              class="yw-tab-badge"
            >{{ workTaskStore.starredTasks.length }}</span>
            <span
              v-if="tab === 'Assigned' && myTasks.filter(t => t.assignedUserId === currentUserId).length > 0"
              class="yw-tab-badge"
            >{{ myTasks.filter(t => t.assignedUserId === currentUserId).length }}</span>
          </button>
        </div>

        <div class="yw-scrollable" v-if="activeTab === 'Summary'">
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
                <div class="card-lbl">{{ t('yourWork.subscribed') }}</div>
                <div class="card-val">{{ overview.subscribed }}</div>
              </div>
            </div>
          </div>

          <h3 class="section-title mt-4">{{ t('yourWork.workload') }}</h3>
          <div class="yw-workload-row">
            <div class="wl-card">
              <div class="wl-lbl"><span class="dbox bg-gray"></span> {{ t('yourWork.backlog') }}</div>
              <div class="wl-val">{{ workload.backlog }}</div>
            </div>
            <div class="wl-card">
              <div class="wl-lbl"><span class="dbox bg-blue"></span> {{ t('yourWork.notStarted') }}</div>
              <div class="wl-val">{{ workload.notStarted }}</div>
            </div>
            <div class="wl-card">
              <div class="wl-lbl"><span class="dbox bg-orange"></span> {{ t('yourWork.inProgress') }}</div>
              <div class="wl-val">{{ workload.workingOn }}</div>
            </div>
            <div class="wl-card">
              <div class="wl-lbl"><span class="dbox bg-green"></span> {{ t('yourWork.completed') }}</div>
              <div class="wl-val">{{ workload.completed }}</div>
            </div>
            <div class="wl-card">
              <div class="wl-lbl"><span class="dbox bg-red"></span> {{ t('yourWork.canceled') }}</div>
              <div class="wl-val">{{ workload.canceled }}</div>
            </div>
          </div>

          <div class="yw-two-cols mt-4">
            <div class="chart-col">
              <h3 class="section-title">{{ t('yourWork.byPriority') }}</h3>
              <div class="empty-chart" v-if="myTasks.length === 0">
                <i class="fa-solid fa-chart-simple chart-icon"></i>
                <span>{{ t('yourWork.empty') }}</span>
              </div>
              <apexchart
                v-else
                type="pie"
                height="220"
                :options="priorityChartOptions"
                :series="prioritySeries"
              />
            </div>
            <div class="chart-col">
              <h3 class="section-title">{{ t('yourWork.byState') }}</h3>
              <div class="empty-chart" v-if="myTasks.length === 0">
                <i class="fa-solid fa-chart-column chart-icon"></i>
                <span>{{ t('yourWork.empty') }}</span>
              </div>
              <apexchart
                v-else
                type="bar"
                height="170"
                :options="stateChartOptions"
                :series="stateSeries"
              />
            </div>
          </div>

          <h3 class="section-title mt-4">{{ t('yourWork.recentActivity') }}</h3>
          <div class="list-body">
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
        </div>

        <div class="yw-scrollable" v-else-if="['Assigned', 'Created', 'Subscribed', 'Starred'].includes(activeTab)">
          <!-- ── Search / Filter / Sort Toolbar (từ For You) ── -->
          <div class="yw-task-toolbar">
            <div class="yw-search-input">
              <i class="fa-solid fa-magnifying-glass"></i>
              <input type="text" v-model="taskSearch" placeholder="Tìm kiếm công việc..." />
            </div>
            <select v-model="statusFilter" class="yw-jira-select">
              <option value="all">Tất cả trạng thái</option>
              <option value="todo">To Do / Backlog</option>
              <option value="inprogress">In Progress</option>
              <option value="done">Done</option>
            </select>
            <select v-model="sortOption" class="yw-jira-select">
              <option value="updated">Mới cập nhật</option>
              <option value="created">Mới tạo</option>
              <option value="priority">Độ ưu tiên</option>
            </select>
          </div>

          <div class="list-header mt-4">
            <i class="fa-solid fa-circle-dashed f-icon"></i>
            <span class="lh-title">{{ t('yourWork.allWorkItems') }}</span>
            <span class="lh-count">{{ listData.length }}</span>
          </div>

          <div class="list-body mt-4">
            <div v-if="listData.length === 0" class="yw-empty-state">
              <i class="fa-solid fa-inbox"></i>
              <p>Không có công việc nào</p>
            </div>
            <div class="list-row" v-for="item in listData" :key="item.id" @click="openTaskDetail(item.task)">
              <div class="lr-left">
                <span class="lr-id">{{ item.id }}</span>
                <span class="lr-title">{{ demoText(item.title) }}</span>
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
                    <i class="fa-solid fa-angles-up text-red-500" v-if="item.priority === 1"></i>
                    <i class="fa-solid fa-chevron-up text-orange-500" v-else-if="item.priority === 2"></i>
                    <i class="fa-solid fa-minus text-blue-500" v-else-if="item.priority === 3"></i>
                    <i class="fa-solid fa-chevron-down text-gray-400" v-else></i>
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

                <!-- Nút ⭐ Star (từ For You) -->
                <button
                  class="yw-star-btn"
                  :class="{ starred: isTaskStarred(item.task.id) }"
                  @click.stop="toggleTaskStar(item.task)"
                  :title="isTaskStarred(item.task.id) ? 'Bỏ đánh dấu' : 'Đánh dấu'"
                >
                  <i :class="isTaskStarred(item.task.id) ? 'fa-solid fa-star text-yellow-400' : 'fa-regular fa-star'"></i>
                </button>

                <UserAvatar v-if="item.assigneeName" :user="{ avatarColor: item.assigneeAvatarColor || getAvatarBg(item.assigneeName), initials: item.assigneeInitials || getInitials(item.assigneeName), fullName: item.assigneeName }" :size="24" :fontSize="10" />
                <div class="lr-badge cursor-not-allowed" v-else>
                  <i class="fa-regular fa-user"></i>
                </div>
                <span class="lr-time">{{ timeAgo(item.task.updatedAt || item.task.createdAt) }}</span>
              </div>
            </div>
          </div>
        </div>

        <div class="yw-scrollable" v-else-if="activeTab === 'Activity'">
          <div class="activity-page-header mt-4 flex-between">
            <h3 class="section-title" style="margin: 0;">{{ t('yourWork.recentActivity') }}</h3>
            <button class="plane-primary-btn" @click="downloadWordActivity">{{ t('yourWork.downloadActivity') }}</button>
          </div>

          <div class="list-body mt-4">
            <div class="list-row" style="cursor: default;" v-for="(activity, index) in pageActivities" :key="index">
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
        </div>
      </div>

      <!-- Task Detail Modal (từ For You) -->
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
        @updateTask="updateTaskFromModal"
        @refresh-tasks="fetchMyTasks"
      />

      <div class="yw-sidebar">
        <div class="cover-image">
          <button class="edit-cover"><i class="fa-solid fa-pencil"></i></button>
        </div>
        <div class="profile-info">
          <UserAvatar :user="userProfile" :size="56" :fontSize="24" class="avatar-lg" style="position: absolute; top: -28px;" />
          <div class="user-details">
            <h2 class="user-name">{{ userProfile.fullName }}</h2>
            <p class="user-handle">({{ userProfile.email }})</p>
          </div>

          <div class="info-row mt-4">
            <span class="info-lbl">Joined on</span>
            <span class="info-val">{{ userProfile.joinedOn }}</span>
          </div>
          <div class="info-row">
            <span class="info-lbl">Timezone</span>
            <span class="info-val">{{ userProfile.timezone }}</span>
          </div>

          <div class="workspace-row mt-4">
            <i class="fa-solid fa-briefcase ws-icon"></i>
            <span style="white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 200px;">{{ projectList[0]?.name || '—' }}</span>
            <i class="fa-solid fa-chevron-down ms-auto" style="font-size: 10px; color: #71717A;"></i>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.yw-container {
  display: flex;
  height: 100vh;
  background: var(--color-bg);
  color: var(--color-text-primary);
  font-family: 'Inter', sans-serif;
  overflow: hidden;
}

.yw-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  padding: 0 32px;
  overflow-y: auto;
}

.yw-header {
  padding: 24px 0 16px;
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
  grid-template-columns: repeat(5, 1fr);
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

.profile-info {
  padding: 0 24px 24px;
  position: relative;
}

.avatar-lg {
  position: absolute;
  top: -28px;
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

.user-details { margin-top: 40px; }
.user-name { font-size: 16px; font-weight: 600; margin: 0; color: var(--color-text-primary); }
.user-handle { font-size: 12px; color: var(--color-text-muted); margin: 4px 0 0 0; }

.info-row { display: flex; justify-content: space-between; font-size: 12px; margin-bottom: 8px; }
.info-lbl { color: var(--color-text-muted); }
.info-val { color: var(--color-text-primary); font-weight: 500; }

.workspace-row { display: flex; align-items: center; gap: 8px; font-size: 13px; font-weight: 500; padding-top: 16px; border-top: 1px solid var(--color-border); cursor: pointer; }
.ws-icon { color: #F59E0B; }
.ms-auto { margin-left: auto; }

/* SprintA your-work refresh */
.yw-container {
  height: calc(100vh - 56px);
  background:
    radial-gradient(circle at 12% 0%, rgba(56, 189, 248, 0.16), transparent 34%),
    radial-gradient(circle at 86% 4%, rgba(34, 197, 94, 0.1), transparent 30%),
    linear-gradient(180deg, #f8fbff, #eef5fb 52%, #f8fafc);
  font-family: inherit;
}

.yw-main {
  padding: 0 clamp(22px, 3vw, 44px);
}

.yw-header {
  padding-top: 30px;
}

.yw-title {
  font-size: clamp(24px, 2vw, 34px);
  font-weight: 900;
  color: #0f172a;
}

.yw-title i {
  color: #0284c7;
}

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

.section-title {
  color: #0f172a;
  font-size: 17px;
  font-weight: 900;
}

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
}

.cover-image {
  height: 132px;
  background:
    radial-gradient(circle at 25% 16%, rgba(250, 204, 21, 0.45), transparent 18%),
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
  .yw-container {
    height: auto;
    flex-direction: column;
    overflow: visible;
  }

  .yw-sidebar {
    width: auto;
    margin: 0 22px 22px;
    border: 1px solid rgba(148, 163, 184, 0.24);
    border-radius: 18px;
    overflow: hidden;
  }
}

[data-theme='dark'] .yw-container {
  background:
    radial-gradient(circle at 12% 0%, rgba(56, 189, 248, 0.16), transparent 34%),
    radial-gradient(circle at 86% 4%, rgba(34, 197, 94, 0.1), transparent 30%),
    linear-gradient(180deg, #07111f, #0f172a 52%, #101827);
  color: #e2e8f0;
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
    radial-gradient(circle at 25% 16%, rgba(250, 204, 21, 0.28), transparent 18%),
    linear-gradient(135deg, #0f172a, #164e63 42%, #064e3b);
}

/* Compact density */
.yw-container {
  min-height: calc(100vh - var(--sa-topbar-height, 52px)) !important;
}

.yw-main {
  padding: 0 var(--sa-page-x, 24px) 24px !important;
}

.yw-header {
  padding-top: 18px !important;
}

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

.section-title {
  font-size: 15px !important;
  margin-bottom: 12px !important;
}

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
  .yw-main {
    padding: 0 14px 18px !important;
  }

  .yw-sidebar {
    width: 100% !important;
  }
}

@media (max-width: 720px) {
  .yw-container {
    display: block !important;
  }

  .yw-main {
    padding: 0 12px 16px !important;
  }

  .overview-grid,
  .workload-grid,
  .charts-grid {
    grid-template-columns: 1fr !important;
  }

  .yw-tabs {
    overflow-x: auto !important;
  }
}

/* ── Recommended Spaces Section (từ For You) ─────────────── */
.yw-spaces-section {
  margin: 18px 0 0;
  padding-bottom: 6px;
}

.yw-spaces-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 14px;
}

.yw-spaces-title {
  font-size: 15px;
  font-weight: 900;
  color: #0f172a;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.yw-spaces-title i { color: #0284c7; }

.yw-spaces-viewall {
  font-size: 13px;
  font-weight: 600;
  color: #0284c7;
  text-decoration: none;
  cursor: pointer;
  transition: color 0.15s;
}
.yw-spaces-viewall:hover { color: #0052cc; text-decoration: underline; }

.yw-spaces-loading,
.yw-spaces-empty {
  display: flex;
  align-items: center;
  gap: 10px;
  color: #64748b;
  font-size: 13px;
  padding: 16px;
  border: 1px dashed rgba(148, 163, 184, 0.4);
  border-radius: 12px;
  background: rgba(248, 250, 252, 0.6);
}

.yw-spaces-row {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 12px;
}

.yw-space-card {
  display: flex;
  align-items: center;
  gap: 12px;
  background: linear-gradient(180deg, rgba(255,255,255,0.94), rgba(255,255,255,0.78));
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 14px;
  padding: 14px 16px;
  cursor: pointer;
  box-shadow: 0 4px 14px rgba(15, 23, 42, 0.05);
  transition: all 0.25s cubic-bezier(0.25, 0.8, 0.25, 1);
  position: relative;
  overflow: hidden;
}
.yw-space-card:hover {
  transform: translateY(-2px);
  border-color: rgba(14, 165, 233, 0.4);
  box-shadow: 0 10px 28px rgba(15, 23, 42, 0.1);
}

.yw-sc-icon {
  width: 36px;
  height: 36px;
  border-radius: 9px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 17px;
  flex-shrink: 0;
}

.yw-sc-info {
  flex: 1;
  min-width: 0;
  padding-right: 24px;
}

.yw-sc-name {
  font-size: 13px;
  font-weight: 800;
  margin: 0 0 2px 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  color: #0f172a;
}

.yw-sc-desc {
  font-size: 11px;
  color: #64748b;
  margin: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.yw-sc-star {
  background: transparent;
  border: none;
  cursor: pointer;
  color: #94a3b8;
  font-size: 13px;
  padding: 5px;
  border-radius: 6px;
  transition: all 0.2s ease;
  flex-shrink: 0;
  position: absolute;
  right: 10px;
  top: 50%;
  transform: translateY(-50%);
}
.yw-sc-star.starred { color: #f5cd47; }
.yw-sc-star:hover { color: #0f172a; background: rgba(9, 30, 66, 0.06); }

[data-theme='dark'] .yw-space-card {
  background: linear-gradient(180deg, rgba(30,41,59,0.92), rgba(15,23,42,0.86));
  border-color: rgba(148, 163, 184, 0.2);
}
[data-theme='dark'] .yw-sc-name { color: #f8fafc; }
[data-theme='dark'] .yw-spaces-title { color: #f8fafc; }

/* ── Task Toolbar (Search / Filter / Sort) ────────────────── */
.yw-task-toolbar {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
  align-items: center;
  margin-top: 18px;
  margin-bottom: 6px;
  padding: 12px 16px;
  background: rgba(248, 250, 252, 0.8);
  border: 1px solid rgba(148, 163, 184, 0.2);
  border-radius: 12px;
}

.yw-search-input {
  display: flex;
  align-items: center;
  gap: 8px;
  background: rgba(255, 255, 255, 0.9);
  border: 1px solid rgba(148, 163, 184, 0.3);
  border-radius: 9px;
  padding: 7px 12px;
  flex: 1;
  min-width: 180px;
  transition: border-color 0.2s;
}
.yw-search-input:focus-within { border-color: #0284c7; }
.yw-search-input i { color: #94a3b8; font-size: 12px; }
.yw-search-input input {
  border: none;
  outline: none;
  background: transparent;
  font-size: 13px;
  color: #0f172a;
  width: 100%;
}
.yw-search-input input::placeholder { color: #94a3b8; }

.yw-jira-select {
  background: rgba(255, 255, 255, 0.9);
  border: 1px solid rgba(148, 163, 184, 0.3);
  border-radius: 9px;
  padding: 7px 10px;
  font-size: 12.5px;
  font-weight: 700;
  color: #475569;
  cursor: pointer;
  transition: border-color 0.2s;
  outline: none;
}
.yw-jira-select:focus { border-color: #0284c7; }

[data-theme='dark'] .yw-task-toolbar { background: rgba(15,23,42,0.7); border-color: rgba(148,163,184,0.2); }
[data-theme='dark'] .yw-search-input, [data-theme='dark'] .yw-jira-select { background: rgba(30,41,59,0.9); border-color: rgba(148,163,184,0.2); color: #e2e8f0; }
[data-theme='dark'] .yw-search-input input { color: #e2e8f0; }

/* ── Star Button in Task Row ──────────────────────────────── */
.yw-star-btn {
  background: transparent;
  border: none;
  cursor: pointer;
  color: #94a3b8;
  font-size: 13px;
  padding: 5px 7px;
  border-radius: 7px;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
}
.yw-star-btn.starred { color: #f5cd47; }
.yw-star-btn:hover { background: rgba(9, 30, 66, 0.06); color: #475569; }

/* ── Tab Badge ────────────────────────────────────────────── */
.yw-tab-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 16px;
  height: 16px;
  padding: 0 4px;
  font-size: 10px;
  font-weight: 800;
  background: #0284c7;
  color: white;
  border-radius: 8px;
  margin-left: 4px;
}

/* ── Empty state & Time ────────────────────────────────────── */
.yw-empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 48px 24px;
  gap: 12px;
  color: #94a3b8;
  font-size: 13px;
}
.yw-empty-state i { font-size: 32px; opacity: 0.4; }

.lr-time {
  font-size: 11px;
  color: #94a3b8;
  white-space: nowrap;
}
</style>




