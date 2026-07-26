<script setup>
import { ref, onMounted, onBeforeUnmount, computed, watch } from 'vue'
import axiosClient from '@/api/axiosClient'
import { ElNotification } from 'element-plus'
import { useI18nStore } from '@/store/useI18nStore'
import { usePersonalWork } from '@/composables/usePersonalWork'
import { PERSONAL_WORK_SCOPES } from '@/api/personalWorkApi'
import { useAuthStore } from '@/store/useAuthStore'
import { useSiteStore } from '@/store/useSiteStore'
import UserAvatar from '@/components/common/UserAvatar.vue'

const i18nStore = useI18nStore()
const authStore = useAuthStore()
const siteStore = useSiteStore()
const t = (key) => i18nStore.t(key)
const activeTab = ref('Summary')
const tabs = ['Summary', 'Assigned', 'Created', 'Following', 'Worked', 'Activity']
const tabLabel = (tab) => t(`yourWork.tabs.${tab.toLowerCase()}`)

const selectedProjectId = ref(null)
const projectList = ref([])
const currentPage = ref(1)
const pageSize = ref(10)

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
  Assigned: PERSONAL_WORK_SCOPES.assigned,
  Created: PERSONAL_WORK_SCOPES.created,
  Following: PERSONAL_WORK_SCOPES.following,
  Worked: PERSONAL_WORK_SCOPES.worked
}
const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize.value)))
const contextKey = computed(() => [
  authStore.userId || '',
  authStore.token || '',
  siteStore.activeSite?.id || siteStore.activeSite?.Id || ''
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
  const scope = tabScopes[activeTab.value]
  if (!scope) return null
  return fetchPage({
    scope,
    page: currentPage.value,
    pageSize: pageSize.value
  }).catch(() => null)
}

onMounted(async () => {
  await fetchProjects()
  fetchProfile()
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
  return pageActivities.value.slice(0, 5).map(activity => ({
    id: activity.id,
    text: activity.text,
    time: activity.time
  }))
})

const listData = computed(() => {
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

const updateTaskProperty = async (task, field, value) => {
  try {
    const index = myTasks.value.findIndex(item => item.id === task.id)
    if (index !== -1) {
      myTasks.value[index][field] = value

      const updatePayload = { [field]: value }
      if (task.projectId) {
        await axiosClient.patch(`/projects/${task.projectId}/WorkTasks/${task.id}`, updatePayload)
      }

      await fetchActivity({ limit: 50 }).catch(() => null)
    }
  } catch (error) {
    console.error('Failed to update task:', error)
  }
}

const pageActivities = computed(() => activities.value.map(activity => ({
  id: activity.id,
  icon: 'fa-solid fa-clock-rotate-left',
  text: activity.summary || `${activity.action || ''} ${activity.resource || ''}`.trim(),
  bold: '',
  time: activity.timestamp ? new Date(activity.timestamp).toLocaleString() : ''
})))

const retryCurrentView = () => {
  if (activeTab.value === 'Summary') {
    fetchSummary().catch(() => null)
    fetchActivity({ limit: 50 }).catch(() => null)
    return
  }
  if (activeTab.value === 'Activity') {
    fetchActivity({ limit: 50 }).catch(() => null)
    return
  }
  fetchMyTasks()
}

watch(activeTab, () => {
  currentPage.value = 1
  myTasks.value = []
  totalCount.value = 0

  if (activeTab.value === 'Summary') {
    fetchSummary().catch(() => null)
    fetchActivity({ limit: 50 }).catch(() => null)
    return
  }
  if (activeTab.value === 'Activity') {
    fetchActivity({ limit: 50 }).catch(() => null)
    return
  }
  fetchMyTasks()
})

watch(currentPage, () => {
  if (tabScopes[activeTab.value]) fetchMyTasks()
})

watch(contextKey, () => {
  reset()
  currentPage.value = 1
  if (!authStore.token || !authStore.userId) return

  fetchSummary().catch(() => null)
  fetchActivity({ limit: 50 }).catch(() => null)
  if (tabScopes[activeTab.value]) fetchMyTasks()
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
  <div>
    <div class="yw-container">
      <div class="yw-main">
        <header class="yw-header flex-between">
          <span class="yw-title"><i class="fa-regular fa-user"></i> Your work</span>
        </header>


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

          <h3 class="section-title mt-4">{{ t('yourWork.workload') }}</h3>
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

          <h3 class="section-title mt-4">{{ t('yourWork.recentActivity') }}</h3>
          <div class="list-body">
            <div v-if="activityLoading" class="personal-state">
              <i class="fa-solid fa-spinner fa-spin"></i>
              <span>{{ t('yourWork.loadingActivity') }}</span>
            </div>
            <div v-else-if="activityError" class="personal-state personal-state-error">
              <span>{{ t('yourWork.activityFailed') }}</span>
              <button class="plane-primary-btn" @click="retryCurrentView">{{ t('yourWork.retry') }}</button>
            </div>
            <div v-else-if="recentActivity.length === 0" class="personal-state">
              {{ t('yourWork.noActivity') }}
            </div>
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

        <div class="yw-scrollable" v-else-if="['Assigned', 'Created', 'Following', 'Worked'].includes(activeTab)">
          <div class="list-header mt-4">
            <i class="fa-solid fa-circle-dashed f-icon"></i>
            <span class="lh-title">{{ t('yourWork.allWorkItems') }}</span>
            <span class="lh-count">{{ totalCount }}</span>
          </div>

          <div class="list-body mt-4">
            <div v-if="loading" class="personal-state">
              <i class="fa-solid fa-spinner fa-spin"></i>
              <span>{{ t('yourWork.loading') }}</span>
            </div>
            <div v-else-if="error" class="personal-state personal-state-error">
              <span>{{ t('yourWork.loadFailed') }}</span>
              <button class="plane-primary-btn" @click="retryCurrentView">{{ t('yourWork.retry') }}</button>
            </div>
            <div v-else-if="listData.length === 0" class="personal-state">
              {{ t(`yourWork.empty.${activeTab.toLowerCase()}`) }}
            </div>
            <div class="list-row" v-for="item in listData" :key="item.id">
              <div class="lr-left">
                <span class="lr-id">{{ item.id }}</span>
                <span class="lr-title">{{ item.title }}</span>
              </div>
              <div class="lr-right">
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
          <div class="personal-pagination" v-if="!loading && !error && totalPages > 1">
            <button class="jira-btn-subtle" :disabled="currentPage === 1" @click="currentPage -= 1">
              <i class="fa-solid fa-chevron-left"></i>
            </button>
            <span>{{ currentPage }} / {{ totalPages }}</span>
            <button class="jira-btn-subtle" :disabled="currentPage === totalPages" @click="currentPage += 1">
              <i class="fa-solid fa-chevron-right"></i>
            </button>
          </div>
        </div>

        <div class="yw-scrollable" v-else-if="activeTab === 'Activity'">
          <div class="activity-page-header mt-4 flex-between">
            <h3 class="section-title" style="margin: 0;">{{ t('yourWork.recentActivity') }}</h3>
            <button class="plane-primary-btn" @click="downloadWordActivity">{{ t('yourWork.downloadActivity') }}</button>
          </div>

          <div class="list-body mt-4">
            <div v-if="activityLoading" class="personal-state">
              <i class="fa-solid fa-spinner fa-spin"></i>
              <span>{{ t('yourWork.loadingActivity') }}</span>
            </div>
            <div v-else-if="activityError" class="personal-state personal-state-error">
              <span>{{ t('yourWork.activityFailed') }}</span>
              <button class="plane-primary-btn" @click="retryCurrentView">{{ t('yourWork.retry') }}</button>
            </div>
            <div v-else-if="pageActivities.length === 0" class="personal-state">
              {{ t('yourWork.noActivity') }}
            </div>
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
        </div>
      </div>

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
</style>
