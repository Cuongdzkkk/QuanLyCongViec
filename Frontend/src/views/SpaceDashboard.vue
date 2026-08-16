<template>
  <ProjectPageContainer scrollable>
      <ProjectPageHeader
        :title="t('Dashboard')"
        :description="t('Project overview and quick insights')"
        icon="fa-solid fa-chart-pie"
      >
        <template #actions>
          <!-- Active Cycle Card -->
          <router-link :to="{ name: 'CyclesView', params: { id: projectId } }" style="text-decoration: none;">
            <div v-if="activeSprint" class="current-cycle-card">
              <div class="cycle-icon-wrapper active">
                <i class="fa-solid fa-arrows-spin fa-spin-pulse"></i>
              </div>
              <div class="cycle-info">
                <h4>{{ activeSprint.name }} <span class="active-badge">ACTIVE</span></h4>
                <p>{{ t('Current running cycle') }}</p>
              </div>
            </div>
            <div v-else class="current-cycle-card empty">
              <div class="cycle-icon-wrapper">
                <i class="fa-solid fa-rotate"></i>
              </div>
              <div class="cycle-info">
                <h4 class="text-gray-600">{{ t('No active cycle') }}</h4>
                <p>{{ t('Click to plan sprints') }}</p>
              </div>
            </div>
          </router-link>

          <!-- Project Info -->
          <div class="header-project-badge">
            <ProjectAvatar :icon="project?.icon" :background="project?.cover" size="md" />
            <div class="hpb-details">
              <span class="hpb-key">{{ project?.key }}</span>
              <span class="hpb-name">{{ project?.name }}</span>
            </div>
          </div>
        </template>
      </ProjectPageHeader>

      <div v-if="loading" class="dashboard-loading-container">
        <i class="fa-solid fa-spinner fa-spin text-3xl mb-3 text-[var(--color-accent)]"></i>
        <p class="text-sm font-medium">{{ t('Fetching dashboard insights...') }}</p>
      </div>
      <div v-else class="dashboard-content">
        <!-- Stats Cards Grid -->
        <div class="stats-grid">
          <div class="stat-card open-tasks-card">
            <div class="stat-icon">
              <i class="fa-solid fa-list-check"></i>
            </div>
            <div class="stat-info">
              <span class="stat-value">{{ openTasks.length }}</span>
              <span class="stat-label">{{ t('Open Tasks') }}</span>
            </div>
          </div>

          <div class="stat-card completed-tasks-card">
            <div class="stat-icon">
              <i class="fa-solid fa-check-double"></i>
            </div>
            <div class="stat-info">
              <span class="stat-value">{{ completedTasks.length }}</span>
              <span class="stat-label">{{ t('Completed') }}</span>
            </div>
          </div>

          <div class="stat-card inprogress-tasks-card">
            <div class="stat-icon">
              <i class="fa-solid fa-clock-rotate-left"></i>
            </div>
            <div class="stat-info">
              <span class="stat-value">{{ inProgressTasks.length }}</span>
              <span class="stat-label">{{ t('In Progress') }}</span>
            </div>
          </div>

          <div class="stat-card blocked-tasks-card">
            <div class="stat-icon">
              <i class="fa-solid fa-triangle-exclamation"></i>
            </div>
            <div class="stat-info">
              <span class="stat-value">{{ blockedTasks.length }}</span>
              <span class="stat-label">{{ t('Blocked') }}</span>
            </div>
          </div>
        </div>

        <!-- Compact Daily Focus integration; the full page remains at /priority. -->
        <div style="margin-top: 24px; padding-top: 24px; border-top: 1px dashed rgba(148, 163, 184, 0.4);">
          <DailyFocusWidget :tasks="allTasks" :project-id="projectId" />
        </div>

      </div>
  </ProjectPageContainer>
</template>

<script setup>
import { ref, onMounted, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import UserAvatar from '@/components/common/UserAvatar.vue'
import ProjectPageHeader from '@/components/common/ProjectPageHeader.vue'
import ProjectAvatar from '@/components/project/ProjectAvatar.vue'
import DailyFocusWidget from '@/components/DailyFocusWidget.vue'

import { useI18nStore } from '@/store/useI18nStore'

const i18nStore = useI18nStore()
const t = (key) => i18nStore.t(key)

import { useWorkTaskStore } from '@/store/useWorkTaskStore'
import { useProjectStore } from '@/store/useProjectStore'
import { useSprintStore } from '@/store/useSprintStore'
import { buildSpacePath } from '@/utils/spaceRoute'

const route = useRoute()
const router = useRouter()
const projectId = computed(() => route.params.id)
const workTaskStore = useWorkTaskStore()
const projectStore = useProjectStore()
const sprintStore = useSprintStore()

const loading = ref(true)
let loadRequestId = 0

const project = computed(() => projectStore.currentProject)
const allTasks = computed(() => workTaskStore.tasks || [])
const activeSprint = computed(() => sprintStore.activeSprint)

const doneStatuses = ['done', 'completed', 'finished', 'hoàn thành', 'success', 'hoàn tất']
const cancelStatuses = ['cancel', 'cancelled', 'hủy', 'hủy bỏ']

const openTasks = computed(() => {
  return allTasks.value.filter(task => {
    const status = (task.statusName || '').toLowerCase().trim()
    return !doneStatuses.includes(status) && !cancelStatuses.includes(status)
  })
})

const completedTasks = computed(() => {
  return allTasks.value.filter(task => {
    const status = (task.statusName || '').toLowerCase().trim()
    return doneStatuses.includes(status)
  })
})

const inProgressTasks = computed(() => {
  return allTasks.value.filter(task => {
    const status = (task.statusName || '').toLowerCase().trim()
    return status === 'in progress' || status === 'inprogress'
  })
})

const blockedTasks = computed(() => {
  return allTasks.value.filter(task => {
    const status = (task.statusName || '').toLowerCase().trim()
    return status.includes('block')
  })
})

const scoredTasks = computed(() => {
  const incompleteTasks = allTasks.value.filter(t => {
    const status = (t.statusName || '').toLowerCase().trim()
    return !doneStatuses.includes(status) && !cancelStatuses.includes(status)
  })

  return incompleteTasks.map(task => {
    let score = 0;
    
    // 1. Deadline (max 35)
    const dueDateStr = task.dueDate || task.deadline || task.endDate || task.DueDate;
    if (dueDateStr) {
      const due = new Date(dueDateStr);
      due.setHours(0,0,0,0);
      const today = new Date();
      today.setHours(0,0,0,0);
      const diffTime = due - today;
      const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
      
      if (diffDays < 0) score += 35; 
      else if (diffDays === 0) score += 32;
      else if (diffDays === 1) score += 28;
      else if (diffDays <= 3) score += 20;
      else if (diffDays <= 7) score += 10;
    }
    
    // 2. Priority (max 25)
    const prio = Number(task.priority || task.Priority);
    if (prio === 1) score += 25; // Critical/Urgent
    else if (prio === 2) score += 20; // High
    else if (prio === 3) score += 12; // Medium
    else if (prio === 4) score += 5; // Low
    
    // 3. Status (max 10)
    const status = (task.statusName || '').toLowerCase().trim();
    if (status.includes('todo') || status.includes('to do') || status.includes('backlog') || status === 'new') score += 10;
    else if (status.includes('progress') || status.includes('doing')) score += 8;
    else if (status.includes('review') || status.includes('test')) score += 5;
    
    // 4. Progress (max 15)
    const progress = Number(task.progress || task.Progress || 0);
    if (progress >= 90) score += 15;
    else if (progress >= 80) score += 12;
    else if (progress >= 70) score += 10;
    else if (progress >= 50) score += 8;
    else if (progress === 0) score += 3;
    
    // 5. Dependency (max 10)
    if (task.linkedTasks && task.linkedTasks.length > 0) {
      const hasBlocked = task.linkedTasks.some(l => l.linkType === 'blocks' || l.linkType === 'blocking');
      if (hasBlocked) score += 10;
    }
    
    return { ...task, score };
  }).sort((a, b) => b.score - a.score);
})

const suggestedTasks = computed(() => scoredTasks.value.slice(0, 4))
const continueTasks = computed(() => scoredTasks.value.slice(4, 12))

const teamWorkload = computed(() => {
  const membersMap = {}
  allTasks.value.forEach(task => {
    const taskAssignees = task.assignees || []
    if (taskAssignees.length === 0) {
      if (!membersMap['unassigned']) {
        membersMap['unassigned'] = {
          userId: 'unassigned',
          fullName: t('Unassigned'),
          avatar: null,
          count: 0
        }
      }
      membersMap['unassigned'].count++
    } else {
      taskAssignees.forEach(assignee => {
        const uid = assignee.userId || assignee.id
        if (!uid) return
        if (!membersMap[uid]) {
          const name = assignee.fullName || assignee.name || t('Unknown')
          membersMap[uid] = {
            userId: uid,
            fullName: name,
            avatarColor: assignee.avatarColor || assignee.AvatarColor,
            avatarUrl: assignee.avatarUrl || assignee.AvatarUrl,
            count: 0
          }
        }
        membersMap[uid].count++
      })
    }
  })

  const list = Object.values(membersMap)
  if (list.length === 0) return []

  const totalTasks = allTasks.value.length || 1
  return list
    .map(item => ({
      ...item,
      percentage: Math.round((item.count / totalTasks) * 100)
    }))
    .sort((a, b) => b.count - a.count)
})

const normalizePriorityValue = (p) => {
  if (p === null || p === undefined) return 0
  if (typeof p === 'number') return p
  const str = String(p).toLowerCase().trim()
  if (str === '1' || str === 'urgent' || str === 'critical') return 1
  if (str === '2' || str === 'high') return 2
  if (str === '3' || str === 'medium' || str === 'normal') return 3
  if (str === '4' || str === 'low') return 4
  return 0
}

const getPriorityIcon = (priority) => {
  const p = normalizePriorityValue(priority)
  if (p === 1) return 'fa-solid fa-angles-up'
  if (p === 2) return 'fa-solid fa-chevron-up'
  if (p === 3) return 'fa-solid fa-minus'
  if (p === 4) return 'fa-solid fa-chevron-down'
  return 'fa-solid fa-ban'
}

const getPriorityLabel = (priority) => {
  const p = normalizePriorityValue(priority)
  switch (p) {
    case 1: return t('Urgent')
    case 2: return t('High')
    case 3: return t('Medium')
    case 4: return t('Low')
    default: return t('None')
  }
}

const getPriorityClass = (priority) => {
  const p = normalizePriorityValue(priority)
  switch (p) {
    case 1: return 'priority-urgent'
    case 2: return 'priority-high'
    case 3: return 'priority-medium'
    case 4: return 'priority-low'
    default: return 'priority-none'
  }
}

const getStatusIcon = (statusName = '') => {
  const status = `${statusName}`.toLowerCase().trim()
  if (status.includes('cancel')) return 'fa-regular fa-circle-xmark'
  if (status.includes('done') || status.includes('complete')) return 'fa-solid fa-circle-check'
  if (status.includes('progress') || status.includes('doing')) return 'fa-solid fa-circle-half-stroke'
  if (status.includes('review') || status.includes('test')) return 'fa-solid fa-clock'
  if (status.includes('todo') || status.includes('to do')) return 'fa-regular fa-circle'
  return 'fa-regular fa-circle-dashed'
}

const normalizeStatusLabel = (statusName = '') => {
  const normalized = `${statusName || ''}`.trim().toUpperCase()
  if (!normalized) return t('No status')
  return t(normalized)
}

const getStatusClass = (statusName = '') => {
  const status = `${statusName}`.toLowerCase().trim()
  if (status.includes('done') || status.includes('complete')) return 'status-done'
  if (status.includes('progress')) return 'status-progress'
  if (status.includes('review')) return 'status-review'
  if (status.includes('block')) return 'status-blocked'
  if (status.includes('backlog')) return 'status-backlog'
  if (status.includes('todo') || status.includes('to do')) return 'status-todo'
  return 'status-default'
}

const calcDaysLeft = (task) => {
  const dateStr = task?.dueDate || task?.deadline || task?.plannedEndDate || task?.endDate || task?.DueDate || task?.Deadline
  if (!dateStr) return null
  const due = new Date(dateStr)
  if (isNaN(due.getTime())) return null
  due.setHours(23, 59, 59, 999)
  const today = new Date()
  return Math.ceil((due - today) / (1000 * 60 * 60 * 24))
}

const getDeadlineText = (task) => {
  const d = calcDaysLeft(task)
  if (d === null) return t('No deadline')
  if (d < 0) return t('Overdue {days}d', { days: Math.abs(d) })
  if (d === 0) return t('Due today')
  if (d === 1) return t('Due tomorrow')
  return t('{days} days left', { days: d })
}

const getDeadlineClass = (task) => {
  const d = calcDaysLeft(task)
  if (d === null) return 'deadline-none'
  if (d < 0) return 'deadline-overdue'
  if (d <= 1) return 'deadline-urgent'
  if (d <= 3) return 'deadline-warning'
  return 'deadline-ok'
}

const navigateToTask = (taskId) => {
  const project = projectStore.currentProject || projectStore.allProjects.find(item => `${item.id}` === `${projectId.value}`) || projectId.value
  router.push({
    path: buildSpacePath(project, 'work-items'),
    query: { task: taskId }
  })
}

const loadDashboard = async () => {
  const id = projectId.value
  const requestId = ++loadRequestId
  loading.value = true

  projectStore.clearProjectContext(id)
  projectStore.fetchProjectDetails(id, { background: true, reset: false }).catch(error => {
    console.error('Failed to load project summary', error)
  })

  try {
    await Promise.all([
      workTaskStore.fetchTasks(id),
      sprintStore.fetchSprints(id)
    ])
  } catch (error) {
    console.error('Failed to load dashboard data', error)
  } finally {
    if (requestId === loadRequestId) {
      loading.value = false
    }
  }
}

onMounted(loadDashboard)

watch(projectId, () => {
  loadDashboard()
})
</script>

<style scoped>
.current-cycle-card {
  display: flex;
  align-items: center;
  gap: 12px;
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)), color-mix(in srgb, var(--color-surface) 88%, transparent));
  border: 1px solid color-mix(in srgb, var(--color-accent) 28%, var(--color-border));
  padding: 7px 12px;
  border-radius: 12px;
  box-shadow: 0 10px 24px color-mix(in srgb, #020617 8%, transparent);
  transition: all 0.2s;
}

.current-cycle-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(14, 165, 233, 0.15);
}

.current-cycle-card.empty {
  border: 1px dashed color-mix(in srgb, var(--color-border) 78%, transparent);
  box-shadow: none;
  background: color-mix(in srgb, var(--color-surface) 78%, transparent);
}

.cycle-icon-wrapper {
  width: 30px;
  height: 30px;
  border-radius: 50%;
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface-hover));
  display: flex;
  align-items: center;
  justify-content: center;
  color: #64748b;
}

.cycle-icon-wrapper.active {
  background: rgba(14, 165, 233, 0.1);
  color: #0ea5e9;
}

.cycle-info h4 {
  font-size: 13px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0;
  display: flex;
  align-items: center;
  gap: 6px;
}

.cycle-info p {
  font-size: 11px;
  color: var(--color-text-muted);
  margin: 2px 0 0 0;
}

.active-badge {
  font-size: 9px;
  background: #10b981;
  color: white;
  padding: 2px 6px;
  border-radius: 10px;
  font-weight: 800;
}

.header-project-badge {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 6px 14px 6px 6px;
  background: color-mix(in srgb, var(--color-surface) 82%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-border) 72%, transparent);
  border-radius: 99px;
}

.hpb-avatar {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  font-size: 12px;
  font-weight: 700;
}

.hpb-details {
  display: flex;
  flex-direction: column;
  justify-content: center;
}

.hpb-key {
  font-size: 10px;
  font-weight: 800;
  color: var(--color-text-muted);
  line-height: 1;
  margin-bottom: 2px;
}

.hpb-name {
  font-size: 12px;
  font-weight: 700;
  color: var(--color-text-primary);
  line-height: 1;
  max-width: 100px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.dashboard-loading-container {
  min-height: 420px;
  display: grid;
  place-items: center;
  border: 1px solid color-mix(in srgb, var(--color-border) 80%, transparent);
  border-radius: 8px;
  background: color-mix(in srgb, var(--color-surface) 82%, #020617);
  color: var(--color-text-muted);
}

.dashboard-content {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

/* Stats Cards Grid */
.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
  width: 100%;
}

@media (max-width: 1024px) {
  .stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 640px) {
  .stats-grid {
    grid-template-columns: 1fr;
  }
}

.stat-card {
  min-height: 64px;
  background: rgba(255, 255, 255, 0.86);
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 12px;
  padding: 12px 14px;
  display: flex;
  align-items: center;
  gap: 12px;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
  position: relative;
  overflow: hidden;
}

.stat-card::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  width: 4px;
  height: 100%;
  background: transparent;
  transition: background-color 0.2s;
}

.stat-card:hover {
  transform: none;
  border-color: rgba(14, 165, 233, 0.32);
  box-shadow: 0 12px 28px rgba(15, 23, 42, 0.08);
}

.stat-card.open-tasks-card::before { background: #3b82f6; }
.stat-card.completed-tasks-card::before { background: #10b981; }
.stat-card.inprogress-tasks-card::before { background: #f59e0b; }
.stat-card.blocked-tasks-card::before { background: #ef4444; }

.stat-icon {
  width: 34px;
  height: 34px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  flex-shrink: 0;
}

.open-tasks-card .stat-icon { background: rgba(59, 130, 246, 0.1); color: #3b82f6; }
.completed-tasks-card .stat-icon { background: rgba(16, 185, 129, 0.1); color: #10b981; }
.inprogress-tasks-card .stat-icon { background: rgba(245, 158, 11, 0.1); color: #f59e0b; }
.blocked-tasks-card .stat-icon { background: rgba(239, 68, 68, 0.1); color: #ef4444; }

.stat-info {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.stat-value {
  font-size: 22px;
  font-weight: 800;
  line-height: 1;
  color: var(--color-text-primary);
  letter-spacing: -0.01em;
}

.stat-label {
  font-size: 11px;
  color: var(--color-text-secondary);
  margin-top: 4px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.02em;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* Panels Grid */
.panels-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 14px;
  align-items: stretch;
}

@media (max-width: 1024px) {
  .panels-grid {
    grid-template-columns: 1fr;
  }
}

.dashboard-panel {
  background: rgba(255, 255, 255, 0.86);
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 12px;
  padding: 14px;
  min-height: 260px;
  display: flex;
  flex-direction: column;
  transition: box-shadow 0.25s ease;
  position: relative;
  overflow: hidden;
}

.dashboard-panel:hover {
  box-shadow: 0 14px 34px rgba(15, 23, 42, 0.08);
}

[data-theme='dark'] .stat-card,
[data-theme='dark'] .dashboard-panel {
  border-color: rgba(148, 163, 184, 0.18);
  background: rgba(15, 23, 42, 0.78);
  box-shadow: 0 18px 44px rgba(0, 0, 0, 0.24);
}

[data-theme='dark'] .current-cycle-card,
[data-theme='dark'] .header-project-badge {
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--color-accent) 9%, #17233a), color-mix(in srgb, var(--color-surface) 84%, #020617));
  border-color: color-mix(in srgb, var(--color-accent) 24%, var(--color-border));
  box-shadow: 0 12px 30px rgba(0, 0, 0, 0.22);
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  padding-bottom: 10px;
  border-bottom: 1px solid color-mix(in srgb, var(--color-border) 80%, transparent);
}

.panel-title {
  font-size: 15px;
  font-weight: 750;
  color: var(--color-text-primary);
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.panel-title span,
.panel-title {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.panel-link {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-accent);
  text-decoration: none;
  transition: color 0.15s;
}

.panel-link:hover {
  color: var(--color-accent-hover);
  text-decoration: underline;
}

/* Empty state */
.empty-state {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  background: rgba(0, 0, 0, 0.05);
  border: 1px dashed var(--color-border);
  border-radius: 8px;
  padding: 32px 16px;
}

[data-theme='dark'] .empty-state {
  background: rgba(255, 255, 255, 0.02);
}

.empty-state i {
  font-size: 36px;
  color: var(--color-text-muted);
  margin-bottom: 12px;
}

.empty-state h4 {
  color: var(--color-text-primary);
  font-size: 14px;
  font-weight: 600;
  margin: 0;
}

.empty-state p {
  color: var(--color-text-secondary);
  font-size: 12px;
  margin-top: 6px;
  max-width: 250px;
  line-height: 1.4;
}

/* Task list styles */
.task-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  flex: 1;
}

.task-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  min-height: 44px;
  padding: 0 12px;
  border: 1px solid color-mix(in srgb, var(--color-border) 70%, transparent);
  background: color-mix(in srgb, var(--color-bg) 42%, transparent);
  border-radius: 7px;
  transition: all 0.2s;
}

[data-theme='dark'] .task-row {
  background: rgba(255, 255, 255, 0.02);
}

.task-row:hover {
  border-color: var(--color-border-hover);
  background: var(--color-surface-hover);
  transform: none;
}

.task-info-left {
  display: flex;
  align-items: center;
  gap: 10px;
  overflow: hidden;
  min-width: 0;
  flex: 1;
}

.task-seq-id {
  font-size: 11px;
  font-family: monospace;
  font-weight: 700;
  background: color-mix(in srgb, var(--color-surface) 88%, #020617);
  color: var(--color-text-muted);
  min-width: 62px;
  text-align: center;
  padding: 4px 6px;
  border-radius: 4px;
  border: 1px solid var(--color-border);
  flex-shrink: 0;
}

.task-title-btn {
  background: none;
  border: none;
  text-align: left;
  min-width: 0;
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
  cursor: pointer;
  padding: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  transition: color 0.15s;
}

.task-title-btn:hover {
  color: var(--color-accent);
  text-decoration: underline;
}

.task-meta-right {
  display: grid;
  grid-template-columns: 110px 85px 90px;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.priority-badge,
.task-status-tag {
  justify-self: start;
  width: fit-content;
  max-width: 100%;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 5px;
  min-height: 24px;
  height: 24px;
  border: 1px solid transparent;
  border-radius: 999px;
  padding: 0 8px;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.02em;
  line-height: 1;
  text-transform: uppercase;
  white-space: nowrap;
  box-sizing: border-box;
}

.priority-badge i,
.task-status-tag i,
.task-deadline-tag i {
  font-size: 11px;
}

.priority-urgent { background: rgba(239, 68, 68, 0.12); border-color: rgba(239, 68, 68, 0.35); color: #dc2626; }
.priority-high { background: rgba(249, 115, 22, 0.12); border-color: rgba(249, 115, 22, 0.35); color: #ea580c; }
.priority-medium { background: rgba(14, 165, 233, 0.12); border-color: rgba(14, 165, 233, 0.35); color: #0284c7; }
.priority-low { background: rgba(100, 116, 139, 0.12); border-color: rgba(100, 116, 139, 0.30); color: #475569; }
.priority-none { background: rgba(100, 116, 139, 0.08); border-color: rgba(100, 116, 139, 0.20); color: #64748b; }

[data-theme='dark'] .priority-urgent { background: rgba(239, 68, 68, 0.2); border-color: rgba(239, 68, 68, 0.45); color: #f87171; }
[data-theme='dark'] .priority-high { background: rgba(249, 115, 22, 0.2); border-color: rgba(249, 115, 22, 0.45); color: #fb923c; }
[data-theme='dark'] .priority-medium { background: rgba(56, 189, 248, 0.2); border-color: rgba(56, 189, 248, 0.45); color: #38bdf8; }
[data-theme='dark'] .priority-low { background: rgba(148, 163, 184, 0.2); border-color: rgba(148, 163, 184, 0.35); color: #cbd5e1; }
[data-theme='dark'] .priority-none { background: rgba(148, 163, 184, 0.12); border-color: rgba(148, 163, 184, 0.25); color: #94a3b8; }

.status-blocked { background: rgba(239, 68, 68, 0.12); border-color: rgba(239, 68, 68, 0.35); color: #dc2626; }
.status-progress { background: rgba(14, 165, 233, 0.12); border-color: rgba(14, 165, 233, 0.35); color: #0284c7; }
.status-review { background: rgba(245, 158, 11, 0.12); border-color: rgba(245, 158, 11, 0.35); color: #d97706; }
.status-done { background: rgba(22, 163, 74, 0.12); border-color: rgba(22, 163, 74, 0.35); color: #16a34a; }
.status-backlog { background: rgba(100, 116, 139, 0.12); border-color: rgba(100, 116, 139, 0.30); color: #475569; }
.status-todo { background: rgba(124, 58, 237, 0.12); border-color: rgba(124, 58, 237, 0.35); color: #7c3aed; }
.status-default { background: rgba(100, 116, 139, 0.10); border-color: rgba(100, 116, 139, 0.22); color: #64748b; }

[data-theme='dark'] .status-blocked { background: rgba(239, 68, 68, 0.2); border-color: rgba(239, 68, 68, 0.45); color: #f87171; }
[data-theme='dark'] .status-progress { background: rgba(56, 189, 248, 0.2); border-color: rgba(56, 189, 248, 0.45); color: #38bdf8; }
[data-theme='dark'] .status-review { background: rgba(245, 158, 11, 0.2); border-color: rgba(245, 158, 11, 0.45); color: #fbbf24; }
[data-theme='dark'] .status-done { background: rgba(34, 197, 94, 0.2); border-color: rgba(34, 197, 94, 0.45); color: #34d399; }
[data-theme='dark'] .status-backlog { background: rgba(148, 163, 184, 0.2); border-color: rgba(148, 163, 184, 0.35); color: #cbd5e1; }
[data-theme='dark'] .status-todo { background: rgba(167, 139, 250, 0.2); border-color: rgba(167, 139, 250, 0.45); color: #c4b5fd; }
[data-theme='dark'] .status-default { background: rgba(148, 163, 184, 0.12); border-color: rgba(148, 163, 184, 0.25); color: #94a3b8; }

.task-deadline-tag {
  justify-self: start;
  width: fit-content;
  min-width: 86px;
  max-width: 100%;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  min-height: 24px;
  height: 24px;
  border: 1px solid transparent;
  border-radius: 6px;
  padding: 0 8px;
  font-size: 10.5px;
  font-weight: 600;
  white-space: nowrap;
  box-sizing: border-box;
  text-transform: none;
}

.deadline-overdue { color: #dc2626; background: rgba(239, 68, 68, 0.1); border: 1px solid rgba(239, 68, 68, 0.3); }
.deadline-urgent { color: #d97706; background: rgba(245, 158, 11, 0.1); border: 1px solid rgba(245, 158, 11, 0.3); }
.deadline-warning { color: #b45309; background: rgba(245, 158, 11, 0.08); border: 1px solid rgba(245, 158, 11, 0.25); }
.deadline-ok { color: #64748b; background: rgba(100, 116, 139, 0.08); border: 1px solid rgba(100, 116, 139, 0.2); }
.deadline-none { color: #94a3b8; background: rgba(148, 163, 184, 0.08); border: 1px dashed rgba(148, 163, 184, 0.3); }

[data-theme='dark'] .deadline-overdue { color: #f87171; background: rgba(239, 68, 68, 0.18); border-color: rgba(239, 68, 68, 0.4); }
[data-theme='dark'] .deadline-urgent { color: #fbbf24; background: rgba(245, 158, 11, 0.18); border-color: rgba(245, 158, 11, 0.4); }
[data-theme='dark'] .deadline-warning { color: #fcd34d; background: rgba(245, 158, 11, 0.12); border-color: rgba(245, 158, 11, 0.3); }
[data-theme='dark'] .deadline-ok { color: #94a3b8; background: rgba(148, 163, 184, 0.12); border-color: rgba(148, 163, 184, 0.25); }
[data-theme='dark'] .deadline-none { color: #64748b; background: rgba(148, 163, 184, 0.08); border-color: rgba(148, 163, 184, 0.2); }

/* Team Workload list styles */
.workload-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.workload-item {
  display: flex;
  flex-direction: column;
  gap: 9px;
  padding: 10px 0;
}

.workload-item-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.workload-user {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.user-avatar {
  width: 26px;
  height: 26px;
  border-radius: 7px;
  background: var(--color-accent);
  color: #ffffff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: 700;
  border: 1.5px solid var(--color-border);
}

.user-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-primary);
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}

.workload-count {
  font-size: 12px;
  font-weight: 700;
  color: var(--color-text-secondary);
  flex: 0 0 auto;
}

.workload-progress-track {
  width: 100%;
  background: var(--color-border);
  height: 7px;
  border-radius: 999px;
  overflow: hidden;
}

.workload-progress-bar {
  height: 100%;
  border-radius: 999px;
  background: linear-gradient(90deg, #10b981 0%, #059669 100%);
  transition: width 0.6s cubic-bezier(0.4, 0, 0.2, 1);
}

.workload-progress-bar.is-unassigned {
  background: linear-gradient(90deg, #94a3b8 0%, #64748b 100%);
}

.dashboard-content {
  gap: 16px !important;
}

.stats-grid,
.panels-grid {
  gap: 14px !important;
}

.stat-card,
.dashboard-panel {
  border-radius: 10px !important;
  padding: 12px !important;
  box-shadow: 0 8px 20px rgba(15, 23, 42, 0.05) !important;
}

.stat-card {
  min-height: 64px !important;
}

.stat-icon {
  width: 36px !important;
  height: 36px !important;
  border-radius: 8px !important;
  font-size: 15px !important;
}

.stat-value {
  font-size: 24px !important;
}

.panel-title {
  font-size: 14px !important;
  padding-bottom: 10px !important;
  margin-bottom: 12px !important;
}

.recent-task-row,
.workload-item {
  padding: 8px 0 !important;
}

@keyframes overview-panel-in {
  from {
    opacity: 0;
    transform: translateY(14px) scale(0.99);
  }
  to {
    opacity: 1;
    transform: translateY(0) scale(1);
  }
}

@keyframes overview-border-flow {
  from { background-position: 0% 50%; }
  to { background-position: 200% 50%; }
}

@keyframes overview-progress-grow {
  from { transform: scaleX(0); }
  to { transform: scaleX(1); }
}

.dashboard-panel {
  animation: overview-panel-in 520ms cubic-bezier(0.2, 0.8, 0.2, 1) both;
  transition:
    transform 220ms cubic-bezier(0.2, 0.8, 0.2, 1),
    background 220ms ease,
    border-color 220ms ease,
    box-shadow 220ms ease,
    color 220ms ease !important;
  background:
    linear-gradient(135deg, rgba(255,255,255,0.96), rgba(248,250,252,0.84)),
    var(--color-surface) !important;
  border-color: rgba(148, 163, 184, 0.20) !important;
}

.stat-card::after,
.dashboard-panel::after {
  content: "";
  position: absolute;
  inset: 0;
  border-radius: inherit;
  padding: 1px;
  background: linear-gradient(90deg, rgba(56, 189, 248, 0.70), rgba(45, 212, 191, 0.45), rgba(250, 204, 21, 0.52), rgba(56, 189, 248, 0.70));
  background-size: 200% 100%;
  -webkit-mask: linear-gradient(#000 0 0) content-box, linear-gradient(#000 0 0);
  -webkit-mask-composite: xor;
  mask-composite: exclude;
  opacity: 0;
  pointer-events: none;
  animation: overview-border-flow 4.6s linear infinite;
  transition: opacity 180ms ease;
}

.stat-card:hover,
.dashboard-panel:hover {
  transform: translateY(-1px);
  border-color: rgba(56, 189, 248, 0.30) !important;
  box-shadow: 0 16px 40px rgba(15, 23, 42, 0.10) !important;
}

.stat-card:hover::after,
.dashboard-panel:hover::after {
  opacity: 0.95;
}

.stats-grid .stat-card:nth-child(1) { animation-delay: 60ms; }
.stats-grid .stat-card:nth-child(2) { animation-delay: 110ms; }
.stats-grid .stat-card:nth-child(3) { animation-delay: 160ms; }
.stats-grid .stat-card:nth-child(4) { animation-delay: 210ms; }
.panels-grid .dashboard-panel:nth-child(1) { animation-delay: 250ms; }
.panels-grid .dashboard-panel:nth-child(2) { animation-delay: 300ms; }

.workload-progress-bar {
  background: linear-gradient(90deg, #10b981 0%, #059669 100%) !important;
  transform-origin: left center;
  animation: overview-progress-grow 700ms 320ms cubic-bezier(0.2, 0.8, 0.2, 1) both;
}

.workload-progress-bar.is-unassigned {
  background: linear-gradient(90deg, #ef4444 0%, #dc2626 100%) !important;
}

[data-theme='dark'] .stat-card,
[data-theme='dark'] .dashboard-panel {
  background: var(--bg-secondary) !important;
  border-color: rgba(148, 163, 184, 0.18) !important;
}

[data-theme='dark'] .project-title,
[data-theme='dark'] .stat-value,
[data-theme='dark'] .panel-title,
[data-theme='dark'] .task-title-btn,
[data-theme='dark'] .user-name {
  color: #f8fafc !important;
}

[data-theme='dark'] .project-subtitle,
[data-theme='dark'] .stat-label,
[data-theme='dark'] .workload-count {
  color: #cbd5e1 !important;
}

[data-theme='light'] .project-title,
[data-theme='light'] .stat-value,
[data-theme='light'] .panel-title,
[data-theme='light'] .task-title-btn,
[data-theme='light'] .user-name {
  color: #0f172a !important;
}

[data-theme='light'] .project-subtitle,
[data-theme='light'] .stat-label,
[data-theme='light'] .workload-count {
  color: #475569 !important;
}

@media (prefers-reduced-motion: reduce) {
  .stat-card,
  .dashboard-panel {
    animation: none !important;
    transition: none !important;
  }
}

@media (max-width: 760px) {
  .stats-grid,
  .panels-grid {
    grid-template-columns: 1fr !important;
  }
}
</style>
