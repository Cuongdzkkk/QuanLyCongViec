<template>
  <ProjectPageContainer>
    <div v-if="loading" class="loading-state">
      <i class="fa-solid fa-spinner fa-spin"></i>
      <p>Loading timeline...</p>
    </div>
    
    <template v-else>
      <ProjectPageHeader 
        icon="fa-solid fa-chart-gantt" 
        :title="t('Timeline')" 
        :description="t('Visualize project schedule and dependencies')"
      />

        <ProjectPageToolbar
          :showSearch="true"
          v-model:searchQuery="searchQuery"
          :searchPlaceholder="'Search tasks...'"
        >
          <template #filters>
            <div class="timeline-filter-wrapper">
              <button class="timeline-filter-trigger" type="button" :class="{ active: showFilters || activeFilterCount }" @click.stop="showFilters = !showFilters">
                <i class="fa-solid fa-filter"></i>
                <span>Filters</span>
                <span v-if="activeFilterCount" class="filter-count">{{ activeFilterCount }}</span>
              </button>
              <div v-if="showFilters" class="timeline-filter-popover" @click.stop>
                <div class="filter-popover-heading">
                  <span>Filter timeline</span>
                  <button type="button" aria-label="Close filters" @click="showFilters = false"><i class="fa-solid fa-xmark"></i></button>
                </div>
                <select v-model="filters.assignee" class="filter-select">
                  <option value="">All Assignees</option>
                  <option v-for="member in projectMembers" :key="member.id || member.userId" :value="member.id || member.userId">{{ member.name || member.fullName || member.email }}</option>
                </select>
                <select v-model="filters.status" class="filter-select">
                  <option value="">All Statuses</option>
                  <option v-for="status in taskStatusOptions" :key="status.name" :value="status.name">{{ status.label }}</option>
                </select>
                <select v-model="filters.priority" class="filter-select">
                  <option value="">All Priorities</option>
                  <option :value="1">Urgent</option><option :value="2">High</option><option :value="3">Normal</option><option :value="4">Low</option>
                </select>
                <select v-model="filters.sprint" class="filter-select">
                  <option value="">All Sprints</option>
                  <option v-for="sprint in sprints" :key="sprint.id" :value="sprint.id">{{ sprint.name }}</option>
                </select>
                <label class="filter-checkbox"><input type="checkbox" v-model="filters.myTasks" /> Only My Tasks</label>
                <button class="clear-timeline-filters" type="button" :disabled="!activeFilterCount" @click="clearFilters">Clear filters</button>
              </div>
            </div>
          </template>

          <template #actions>
            <div class="zoom-controls">
              <button class="zoom-btn" :class="{ active: zoomLevel === 'Day' }" @click="zoomLevel = 'Day'">Day</button>
              <button class="zoom-btn" :class="{ active: zoomLevel === 'Week' }" @click="zoomLevel = 'Week'">Week</button>
              <button class="zoom-btn" :class="{ active: zoomLevel === 'Month' }" @click="zoomLevel = 'Month'">Month</button>
            </div>
          </template>
        </ProjectPageToolbar>

      <div class="timeline-body">
        <div class="timeline-sidebar" ref="sidebarRef" @scroll="syncSidebarScroll">
          <div class="sidebar-header">
            <div class="col col-task">Task</div>
            <div class="col col-owner">Owner</div>
            <div class="col col-status">Status</div>
            <div class="col col-progress">Progress</div>
          </div>
          
          <div class="sidebar-rows" :style="{ minHeight: `${filteredTasks.length * rowHeight}px` }">
            <div v-for="(task, index) in filteredTasks" :key="task.id" class="sidebar-row" :style="{ top: `${index * rowHeight}px`, height: `${rowHeight}px`, position: 'absolute', width: '100%' }" @click="openTaskDetail(task)">
              <div class="col col-task task-title-cell">
                <span class="task-id">{{ task.sequenceId || task.id.substring(0, 8).toUpperCase() }}</span>
                <span class="task-name" :title="task.title">{{ task.title }}</span>
              </div>
              <div class="col col-owner">
                <div class="owner-cell" v-if="getTaskAssignee(task)">
                  <UserAvatar :user="getTaskAssignee(task)" :size="24" :fontSize="10" />
                  <span class="owner-name">{{ getTaskAssignee(task).fullName || getTaskAssignee(task).name || getTaskAssignee(task).email }}</span>
                </div>
                <div class="owner-cell empty" v-else>
                  <div class="empty-avatar"><i class="fa-solid fa-user"></i></div>
                  <span class="owner-name">Unassigned</span>
                </div>
              </div>
              <div class="col col-status">
                <span class="status-badge" :style="{ background: getStatusColor(task.statusName, 0.15), color: getStatusColor(task.statusName) }">
                  {{ normalizeStatusLabel(task.statusName) }}
                </span>
              </div>
              <div class="col col-progress">
                <div class="progress-cell">
                  <span class="progress-val">{{ task.progressPercent || 0 }}%</span>
                </div>
              </div>
            </div>
            <div v-if="filteredTasks.length === 0" style="padding: 24px 0;">
              <ProjectEmptyState
                icon="fa-solid fa-list-check"
                :title="t('common.noData')"
                description="No work items match your criteria."
              />
            </div>
          </div>
        </div>
        
        <div class="timeline-chart-wrapper" ref="chartWrapperRef" @scroll="syncChartScroll">
          <div class="timeline-chart" :style="{ width: `${totalCanvasWidth}px` }">
            
            <div class="chart-header">
              <div class="header-group-row">
                <div v-for="group in headerGroups" :key="group.label" class="header-group-cell" :style="{ width: `${group.span * cellWidth}px` }">
                  {{ group.label }}
                </div>
              </div>
              <div class="header-cell-row">
                <div v-for="bucket in timeBuckets" :key="bucket.key" class="header-cell" :class="{ 'is-today': isBucketToday(bucket), 'weekend': isWeekendBucket(bucket) }" :style="{ width: `${cellWidth}px` }">
                  <div class="hc-label">{{ bucket.label }}</div>
                  <div class="hc-sublabel">{{ bucket.subLabel }}</div>
                </div>
              </div>
            </div>
            
            <div class="chart-body" :style="{ height: `${filteredTasks.length * rowHeight}px` }">
              <div class="chart-grid">
                <div v-for="bucket in timeBuckets" :key="bucket.key" class="grid-line" :class="{ 'is-today': isBucketToday(bucket), 'weekend': isWeekendBucket(bucket) }" :style="{ width: `${cellWidth}px` }"></div>
              </div>
              
              <div class="today-marker" v-if="todayOffset >= 0" :style="{ left: `${todayOffset}px` }">
                <div class="today-line"></div>
                <div class="today-label">TODAY</div>
              </div>
              
              <div class="chart-rows">
                <div v-for="(task, index) in filteredTasks" :key="task.id" class="chart-row" :style="{ top: `${index * rowHeight}px`, height: `${rowHeight}px` }">
                  <div v-if="getTaskBar(task)" 
                       class="task-bar-wrapper"
                       :style="{ left: getTaskBar(task).left, width: getTaskBar(task).width }">
                       
                    <el-tooltip placement="top" popper-class="timeline-tooltip" :show-after="200" effect="light">
                      <template #content>
                        <div class="tooltip-content">
                          <div class="tt-title">{{ task.title }}</div>
                          <div class="tt-grid">
                            <span class="tt-lbl">Owner</span> <span class="tt-val">{{ getTaskAssignee(task)?.fullName || 'Unassigned' }}</span>
                            <span class="tt-lbl">Priority</span> <span class="tt-val">{{ getPriorityLabel(task.priority) }}</span>
                            <span class="tt-lbl">Status</span> <span class="tt-val">{{ normalizeStatusLabel(task.statusName) }}</span>
                            <span class="tt-lbl">Progress</span> <span class="tt-val">{{ task.progressPercent || 0 }}%</span>
                            <span class="tt-lbl">Start</span> <span class="tt-val">{{ formatDate(task.plannedStartDate) }}</span>
                            <span class="tt-lbl">Deadline</span> <span class="tt-val">{{ formatDate(task.dueDate) }}</span>
                          </div>
                        </div>
                      </template>
                      
                      <div class="task-bar" 
                           :class="{ 'is-overdue': isTaskOverdue(task) }"
                           :style="{ background: getTaskColor(task, 0.2), border: `1px solid ${getTaskColor(task)}` }"
                           @click="openTaskDetail(task)">
                        
                        <div class="bar-progress-fill" :style="{ width: `${task.progressPercent || 0}%`, background: getTaskColor(task) }"></div>
                        
                        <div class="bar-content-inner">
                          <div class="bar-avatar" v-if="getTaskAssignee(task)">
                            <UserAvatar :user="getTaskAssignee(task)" :size="24" :fontSize="10" />
                          </div>
                          
                          <div class="bar-title">{{ task.title }}</div>
                          
                          <div class="bar-progress-text">
                            <i class="fa-solid fa-check" v-if="task.progressPercent === 100"></i>
                            <span v-else>{{ task.progressPercent || 0 }}%</span>
                          </div>
                        </div>
                        
                      </div>
                    </el-tooltip>
                    
                  </div>
                  
                  <div v-else class="task-warning">
                    <i class="fa-solid fa-triangle-exclamation"></i>
                    {{ getTaskWarning(task) }}
                  </div>
                </div>
              </div>
            </div>
            
          </div>
        </div>
      </div>
        <TaskDetailModal 
          v-if="selectedTask"
          :selectedTask="selectedTask"
          :projectId="projectId"
          :projectMembers="projectMembers"
          :currentProjectRole="currentProjectRole"
          @close="selectedTask = null"
          @updateTask="updateTask"
          @refresh-tasks="fetchData"
        />
    </template>
  </ProjectPageContainer>
</template>

<script setup>
import { ref, computed, onMounted, watch, nextTick, onUnmounted } from 'vue'
import ProjectPageContainer from '@/components/common/ProjectPageContainer.vue'
import ProjectPageHeader from '@/components/common/ProjectPageHeader.vue'
import ProjectPageToolbar from '@/components/common/ProjectPageToolbar.vue'
import ProjectEmptyState from '@/components/common/ProjectEmptyState.vue'

import { useRoute } from 'vue-router'
import { useWorkTaskStore } from '@/store/useWorkTaskStore'
import { useProjectStore } from '@/store/useProjectStore'
import { useSprintStore } from '@/store/useSprintStore'
import { useI18n } from '@/composables/useI18n'
import { getStoredUserSession } from '@/utils/authSession'
import UserAvatar from '@/components/common/UserAvatar.vue'
import TaskDetailModal from '@/components/TaskDetailModal.vue'

const { t } = useI18n()

const route = useRoute()
const projectId = computed(() => route.params.id)
const taskStore = useWorkTaskStore()
const projectStore = useProjectStore()
const sprintStore = useSprintStore()

const loading = ref(true)
const selectedTask = ref(null)
const projectMembers = ref([])
const sprints = ref([])
const taskStatusOptions = ref([
  { name: 'BACKLOG', label: 'Backlog', color: '#94A3B8' },
  { name: 'TO DO', label: 'To Do', color: '#3b82f6' },
  { name: 'IN PROGRESS', label: 'In Progress', color: '#eab308' },
  { name: 'IN REVIEW', label: 'In Review', color: '#F59E0B' },
  { name: 'DONE', label: 'Done', color: '#10b981' }
])

const searchQuery = ref('')
const showFilters = ref(false)
const filters = ref({
  assignee: '',
  status: '',
  priority: '',
  sprint: '',
  myTasks: false
})
const activeFilterCount = computed(() => [filters.value.assignee, filters.value.status, filters.value.priority, filters.value.sprint, filters.value.myTasks].filter(Boolean).length)
const clearFilters = () => {
  filters.value = { assignee: '', status: '', priority: '', sprint: '', myTasks: false }
}
const zoomLevel = ref('Week')
const rowHeight = 36

const sidebarRef = ref(null)
const chartWrapperRef = ref(null)
const anchorDate = ref(new Date())

const syncSidebarScroll = (e) => {
  if (chartWrapperRef.value) {
    chartWrapperRef.value.scrollTop = e.target.scrollTop
  }
}

const syncChartScroll = (e) => {
  if (sidebarRef.value) {
    sidebarRef.value.scrollTop = e.target.scrollTop
  }
}

const fetchData = async () => {
  loading.value = true
  if (projectId.value) {
    await taskStore.fetchTasks(projectId.value)
    const projectBundle = await projectStore.prefetchProjectBundle(projectId.value)
    if (projectBundle) {
      projectMembers.value = projectBundle.members || []
      if (projectBundle.taskStatuses?.length) {
        taskStatusOptions.value = projectBundle.taskStatuses.map(s => ({
          name: (s.name || '').toUpperCase().trim(),
          label: s.displayName || s.name,
          color: s.colorCode
        }))
      }
    }
    await sprintStore.fetchSprints(projectId.value)
    sprints.value = sprintStore.sprints || []
  }
  loading.value = false
  scrollToToday()
}

const handleGlobalCreateTask = (e) => {
  selectedTask.value = {
    isNew: true,
    title: '',
    description: '',
    statusName: e.detail?.statusName || 'BACKLOG',
    priority: 3,
    sprintId: filters.value.sprint || null,
    plannedStartDate: null,
    dueDate: null
  }
}

onMounted(() => {
  fetchData()
  window.addEventListener('open-create-task', handleGlobalCreateTask)
})

onUnmounted(() => {
  window.removeEventListener('open-create-task', handleGlobalCreateTask)
})

const filteredTasks = computed(() => {
  let tasks = taskStore.tasks || []
  
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    tasks = tasks.filter(t => t.title?.toLowerCase().includes(q) || t.sequenceId?.toLowerCase().includes(q))
  }
  
  if (filters.value.assignee) {
    tasks = tasks.filter(t => getTaskAssigneeIds(t).includes(filters.value.assignee))
  }
  
  if (filters.value.status) {
    tasks = tasks.filter(t => normalizeStatus(t.statusName) === filters.value.status)
  }
  
  if (filters.value.priority) {
    tasks = tasks.filter(t => t.priority === filters.value.priority)
  }
  
  if (filters.value.sprint) {
    tasks = tasks.filter(t => t.sprintId === filters.value.sprint)
  }
  
  if (filters.value.myTasks) {
    const user = getStoredUserSession()
    const myId = user?.id || user?.userId
    if (myId) {
      tasks = tasks.filter(t => getTaskAssigneeIds(t).includes(myId))
    }
  }
  
  return tasks.sort((a, b) => {
    const aStart = parseDate(a.plannedStartDate) || new Date('2099-01-01')
    const bStart = parseDate(b.plannedStartDate) || new Date('2099-01-01')
    return aStart - bStart
  })
})

const getTaskAssigneeIds = (task) => {
  return Array.from(new Set([
    ...(Array.isArray(task.assigneeIds) ? task.assigneeIds : []),
    ...(Array.isArray(task.assignees) ? task.assignees.map(item => item.userId || item.id).filter(Boolean) : []),
    ...(task.assignedUserId ? [task.assignedUserId] : [])
  ]))
}

const getTaskAssignee = (task) => {
  const ids = getTaskAssigneeIds(task)
  if (!ids.length) return null
  return projectMembers.value.find(m => (m.userId || m.id) === ids[0]) || { name: 'Assignee', initials: 'A', id: ids[0] }
}

const normalizeStatus = (status) => `${status || 'BACKLOG'}`.toUpperCase().trim()

const normalizeStatusLabel = (status) => {
  const norm = normalizeStatus(status)
  const option = taskStatusOptions.value.find(o => o.name === norm)
  return option ? option.label : status
}

const getStatusColor = (statusName, opacity = 1) => {
  const norm = normalizeStatus(statusName)
  let hex = '#64748b' // default
  if (norm.includes('DONE') || norm.includes('COMPLETE')) hex = '#10b981'
  else if (norm.includes('PROGRESS') || norm.includes('DOING')) hex = '#eab308'
  else if (norm.includes('TODO') || norm.includes('TO DO')) hex = '#3b82f6'
  else if (norm.includes('BLOCKED')) hex = '#ef4444'
  
  if (opacity < 1) {
    return `color-mix(in srgb, ${hex} ${opacity * 100}%, transparent)`
  }
  return hex
}

const getTaskColor = (task, opacity = 1) => {
  if (isTaskOverdue(task) && !normalizeStatus(task.statusName).includes('DONE')) {
    const hex = '#991b1b'
    if (opacity < 1) return `color-mix(in srgb, ${hex} ${opacity * 100}%, transparent)`
    return hex
  }
  return getStatusColor(task.statusName, opacity)
}

const getPriorityLabel = (priority) => {
  switch (priority) {
    case 1: return 'Urgent'
    case 2: return 'High'
    case 3: return 'Normal'
    case 4: return 'Low'
    default: return 'None'
  }
}

const parseDate = (val) => {
  if (!val) return null
  const parsed = new Date(val)
  return Number.isNaN(parsed.getTime()) ? null : parsed
}

const formatDate = (val) => {
  const d = parseDate(val)
  if (!d) return '-'
  return `${d.getDate().toString().padStart(2, '0')}/${(d.getMonth() + 1).toString().padStart(2, '0')}/${d.getFullYear()}`
}

const startOfDay = (d) => {
  const date = new Date(d)
  date.setHours(0, 0, 0, 0)
  return date
}

const endOfDay = (d) => {
  const date = new Date(d)
  date.setHours(23, 59, 59, 999)
  return date
}

const isTaskOverdue = (task) => {
  const end = parseDate(task.dueDate)
  if (!end) return false
  const normStatus = normalizeStatus(task.statusName)
  if (normStatus.includes('DONE')) return false
  return endOfDay(end) < startOfDay(new Date())
}

const getTaskWarning = (task) => {
  const start = parseDate(task.plannedStartDate)
  const end = parseDate(task.dueDate)
  
  if (!start) return 'Task chưa có ngày bắt đầu'
  if (start && end && start > end) return 'Ngày kết thúc không hợp lệ'
  
  return ''
}

// Timeline Logic
const viewConfig = computed(() => {
  if (zoomLevel.value === 'Day') return { unit: 'day', cellWidth: 50, daysPerBucket: 1 }
  if (zoomLevel.value === 'Week') return { unit: 'week', cellWidth: 100, daysPerBucket: 7 }
  return { unit: 'month', cellWidth: 140, daysPerBucket: 30 }
})

const cellWidth = computed(() => viewConfig.value.cellWidth)

const timelineRange = computed(() => {
  const center = anchorDate.value
  const start = new Date(center)
  const end = new Date(center)
  
  if (zoomLevel.value === 'Day') {
    start.setDate(start.getDate() - 30)
    end.setDate(end.getDate() + 90)
  } else if (zoomLevel.value === 'Week') {
    start.setDate(start.getDate() - 90)
    end.setDate(end.getDate() + 270)
  } else {
    start.setMonth(start.getMonth() - 12)
    end.setMonth(end.getMonth() + 24)
  }
  
  return { start: startOfDay(start), end: endOfDay(end) }
})

const timeBuckets = computed(() => {
  const buckets = []
  const { start, end } = timelineRange.value
  let cursor = new Date(start)
  
  if (zoomLevel.value === 'Week') {
    const day = (cursor.getDay() + 6) % 7
    cursor.setDate(cursor.getDate() - day)
  } else if (zoomLevel.value === 'Month') {
    cursor.setDate(1)
  }
  
  while (cursor <= end) {
    const bucketStart = new Date(cursor)
    let bucketEnd, label, subLabel, groupLabel
    
    if (zoomLevel.value === 'Day') {
      bucketEnd = new Date(cursor)
      label = cursor.getDate().toString()
      const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
      subLabel = days[cursor.getDay()]
      groupLabel = cursor.toLocaleString('en-US', { month: 'short', year: 'numeric' })
      cursor.setDate(cursor.getDate() + 1)
    } else if (zoomLevel.value === 'Week') {
      bucketEnd = new Date(cursor)
      bucketEnd.setDate(bucketEnd.getDate() + 6)
      const weekNum = getWeekNumber(cursor)
      label = `W${weekNum}`
      subLabel = `${cursor.getDate()}/${cursor.getMonth() + 1}`
      groupLabel = cursor.toLocaleString('en-US', { month: 'short', year: 'numeric' })
      cursor.setDate(cursor.getDate() + 7)
    } else {
      bucketEnd = new Date(cursor.getFullYear(), cursor.getMonth() + 1, 0)
      label = cursor.toLocaleString('en-US', { month: 'short' })
      subLabel = cursor.getFullYear().toString()
      groupLabel = cursor.getFullYear().toString()
      cursor.setMonth(cursor.getMonth() + 1)
    }
    
    buckets.push({
      key: bucketStart.getTime(),
      start: bucketStart,
      end: endOfDay(bucketEnd),
      label,
      subLabel,
      groupLabel
    })
  }
  
  return buckets
})

const headerGroups = computed(() => {
  const groups = []
  let current = null
  
  timeBuckets.value.forEach((bucket, index) => {
    if (!current || current.label !== bucket.groupLabel) {
      current = { label: bucket.groupLabel, span: 1, startIndex: index }
      groups.push(current)
    } else {
      current.span += 1
    }
  })
  
  return groups
})

const totalCanvasWidth = computed(() => timeBuckets.value.length * cellWidth.value)

const getTaskBar = (task) => {
  const start = parseDate(task.plannedStartDate)
  const end = parseDate(task.dueDate)
  
  if (!start) return null
  if (start && end && start > end) return null // Invalid dates
  
  const actualEnd = end || start // if no due date, it's a milestone/1-day task
  
  // Find bucket indices
  const startTime = startOfDay(start).getTime()
  const endTime = endOfDay(actualEnd).getTime()
  
  const tlStart = timeBuckets.value[0].start.getTime()
  const tlEnd = timeBuckets.value[timeBuckets.value.length - 1].end.getTime()
  
  if (endTime < tlStart || startTime > tlEnd) return null // Out of bounds
  
  const totalDurationMs = tlEnd - tlStart
  const startOffsetMs = Math.max(0, startTime - tlStart)
  const taskDurationMs = Math.min(endTime, tlEnd) - Math.max(startTime, tlStart)
  
  const leftPx = (startOffsetMs / totalDurationMs) * totalCanvasWidth.value
  const widthPx = Math.max(cellWidth.value * 0.5, (taskDurationMs / totalDurationMs) * totalCanvasWidth.value)
  
  return {
    left: `${leftPx}px`,
    width: `${widthPx}px`
  }
}

const todayOffset = computed(() => {
  const now = new Date().getTime()
  const tlStart = timeBuckets.value[0]?.start.getTime()
  const tlEnd = timeBuckets.value[timeBuckets.value.length - 1]?.end.getTime()
  
  if (!tlStart || now < tlStart || now > tlEnd) return -1
  
  return ((now - tlStart) / (tlEnd - tlStart)) * totalCanvasWidth.value
})

const isBucketToday = (bucket) => {
  const now = new Date().getTime()
  return now >= bucket.start.getTime() && now <= bucket.end.getTime()
}

const isWeekendBucket = (bucket) => {
  if (zoomLevel.value !== 'Day') return false
  const day = bucket.start.getDay()
  return day === 0 || day === 6
}

const getWeekNumber = (d) => {
  const date = new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()))
  const dayNum = date.getUTCDay() || 7
  date.setUTCDate(date.getUTCDate() + 4 - dayNum)
  const yearStart = new Date(Date.UTC(date.getUTCFullYear(), 0, 1))
  return Math.ceil((((date - yearStart) / 86400000) + 1) / 7)
}

const scrollToToday = async () => {
  await nextTick()
  if (chartWrapperRef.value && todayOffset.value >= 0) {
    const center = chartWrapperRef.value.clientWidth / 2
    chartWrapperRef.value.scrollLeft = Math.max(0, todayOffset.value - center)
  }
}

watch(zoomLevel, () => {
  scrollToToday()
})

const openTaskDetail = (task) => {
  selectedTask.value = task
}

</script>

<style scoped>
.space-timeline-view {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  background: var(--sa-bg, #ffffff);
  color: var(--sa-text, #1e293b);
  overflow: hidden;
}

.timeline-container {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.timeline-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 24px;
  background: var(--sa-surface, #ffffff);
  border-bottom: 1px solid var(--sa-border, #e2e8f0);
}

.th-left {
  display: flex;
  align-items: baseline;
  gap: 12px;
}

.th-left h2 {
  margin: 0;
  font-size: 20px;
  font-weight: 700;
}

.task-count {
  font-size: 13px;
  color: var(--sa-text-muted, #64748b);
  font-weight: 500;
}

.th-right {
  display: flex;
  align-items: center;
  gap: 20px;
}

.filters-row {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: nowrap;
  width: 100%;
}

.timeline-filter-wrapper {
  position: relative;
}

.timeline-filter-trigger {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  height: 34px;
  padding: 0 12px;
  border: 1px solid var(--sa-border, var(--color-border));
  border-radius: 9px;
  background: var(--sa-surface, var(--color-surface));
  color: var(--sa-text-secondary, var(--color-text-secondary));
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
}

.timeline-filter-trigger:hover,
.timeline-filter-trigger.active {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border));
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface));
  color: var(--color-accent);
}

.filter-count {
  display: grid;
  min-width: 18px;
  height: 18px;
  padding: 0 5px;
  place-items: center;
  border-radius: 999px;
  background: var(--color-accent);
  color: var(--color-text-inverse, #fff);
  font-size: 10px;
}

.timeline-filter-popover {
  position: absolute;
  top: calc(100% + 8px);
  right: 0;
  z-index: 100;
  display: grid;
  width: min(300px, calc(100vw - 32px));
  gap: 10px;
  padding: 14px;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface);
  box-shadow: var(--shadow-xl, 0 20px 48px rgba(15, 23, 42, 0.18));
}

.timeline-filter-popover .filter-select {
  width: 100%;
}

.filter-popover-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  color: var(--color-text-primary);
  font-size: 13px;
  font-weight: 700;
}

.filter-popover-heading button {
  width: 28px;
  height: 28px;
  border: 0;
  border-radius: 7px;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
}

.clear-timeline-filters {
  height: 34px;
  border: 1px solid var(--color-border);
  border-radius: 9px;
  background: var(--color-surface-hover);
  color: var(--color-text-secondary);
  font-weight: 600;
  cursor: pointer;
}

.clear-timeline-filters:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.filter-item {
  position: relative;
  display: flex;
  align-items: center;
}

.filter-item i {
  position: absolute;
  left: 10px;
  color: #94a3b8;
  font-size: 12px;
}

.filter-input {
  padding: 6px 10px 6px 30px;
  border: 1px solid var(--sa-border, #e2e8f0);
  border-radius: 6px;
  font-size: 13px;
  outline: none;
  background: var(--sa-bg);
  color: var(--sa-text);
  width: 170px;
}

.filter-select {
  padding: 6px 10px;
  border: 1px solid var(--sa-border, #e2e8f0);
  border-radius: 6px;
  font-size: 13px;
  outline: none;
  background: var(--sa-bg);
  color: var(--sa-text);
  cursor: pointer;
  width: 150px;
  min-width: 0;
}

.filter-select:last-of-type {
  width: 210px;
}

.filter-checkbox {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  cursor: pointer;
  color: var(--sa-text-secondary);
  white-space: nowrap;
  flex: 0 0 auto;
}

.zoom-controls {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  height: 34px;
  padding: 2px;
  border: 1px solid color-mix(in srgb, var(--color-border) 86%, transparent);
  border-radius: 9px;
  background: var(--color-surface);
  box-sizing: border-box;
}
.zoom-btn-old {
  display: flex;
  background: color-mix(in srgb, var(--sa-border, #e2e8f0) 50%, transparent);
  border-radius: 6px;
  padding: 2px;
}

.zoom-btn {
  position: relative;
  height: 28px;
  padding: 0 12px;
  font-size: 13px;
  font-weight: 600;
  border: none;
  background: transparent;
  color: var(--color-text-muted);
  border-radius: 7px;
  cursor: pointer;
  overflow: hidden;
  transition: all 0.2s ease;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.zoom-btn:hover {
  color: var(--color-text-primary);
  background: color-mix(in srgb, var(--color-accent) 8%, var(--color-surface-hover));
}

.zoom-btn.active {
  background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface));
  color: var(--color-accent);
  font-weight: 700;
}

.zoom-btn.active::after {
  content: '';
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 3.5px;
  background: linear-gradient(90deg, var(--color-accent, #0c66e4), color-mix(in srgb, var(--color-accent, #0c66e4) 80%, #38bdf8));
  border-radius: 4px 4px 0 0;
  box-shadow: 0 -1px 6px color-mix(in srgb, var(--color-accent, #0c66e4) 50%, transparent);
}
.zoom-btn-disabled {
  padding: 4px 12px;
  font-size: 13px;
  font-weight: 600;
  border: none;
  background: transparent;
  color: var(--sa-text-muted);
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
}

.zoom-btn.active {
  background: var(--sa-surface, #ffffff);
  color: var(--sa-primary, #3b82f6);
  box-shadow: 0 1px 3px rgba(0,0,0,0.1);
}

.timeline-body {
  display: flex;
  flex: 1;
  min-height: 0;
  overflow: hidden;
  position: relative;
}

/* Left Sidebar */
.timeline-sidebar {
  width: 420px;
  min-width: 420px;
  border-right: 1px solid var(--sa-border);
  display: flex;
  flex-direction: column;
  background: var(--sa-surface);
  z-index: 10;
  overflow-y: auto;
  overflow-x: hidden;
}

.sidebar-header {
  display: flex;
  align-items: center;
  height: 40px;
  border-bottom: 1px solid var(--sa-border);
  background: var(--sa-surface);
  position: sticky;
  top: 0;
  z-index: 11;
  font-size: 11px;
  font-weight: 700;
  color: var(--sa-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.col {
  padding: 0 8px;
  display: flex;
  align-items: center;
  overflow: hidden;
}

.col-task { flex: 1; min-width: 0; }
.col-owner { width: 120px; flex-shrink: 0; }
.col-status { width: 88px; flex-shrink: 0; }
.col-progress { width: 64px; flex-shrink: 0; justify-content: flex-end; }

.sidebar-rows {
  position: relative;
}

.sidebar-row {
  display: flex;
  align-items: center;
  border-bottom: 1px solid color-mix(in srgb, var(--sa-border) 40%, transparent);
  cursor: pointer;
  background: var(--sa-surface);
  transition: background 0.1s;
}

.sidebar-row:hover {
  background: var(--sa-surface-hover, #f8fafc);
}

.task-title-cell {
  gap: 6px;
}

.task-id {
  font-size: 11px;
  color: var(--sa-text-muted);
  font-family: monospace;
}

.task-name {
  font-size: 12px;
  font-weight: 500;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.owner-cell {
  display: flex;
  align-items: center;
  gap: 5px;
  font-size: 11px;
}

.empty-avatar {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  border: 1px dashed var(--sa-border);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--sa-text-muted);
}

.owner-name {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 70px;
}

.status-badge {
  font-size: 10px;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 12px;
  white-space: nowrap;
}

.progress-cell {
  font-size: 11px;
  font-weight: 600;
}

/* Chart Area */
.timeline-chart-wrapper {
  flex: 1;
  overflow: auto;
  position: relative;
  background: var(--sa-bg);
}

.timeline-chart {
  position: relative;
}

.chart-header {
  position: sticky;
  top: 0;
  z-index: 5;
  background: var(--sa-surface);
  border-bottom: 1px solid var(--sa-border);
}

.header-group-row {
  display: flex;
  height: 24px;
  border-bottom: 1px solid color-mix(in srgb, var(--sa-border) 50%, transparent);
}

.header-group-cell {
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 700;
  color: var(--sa-text-secondary);
  border-right: 1px solid color-mix(in srgb, var(--sa-border) 50%, transparent);
}

.header-cell-row {
  display: flex;
  height: 26px;
}

.header-cell {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  border-right: 1px solid var(--sa-border);
  font-size: 11px;
}

.hc-label { font-weight: 600; }
.hc-sublabel { font-size: 9px; color: var(--sa-text-muted); }

.header-cell.weekend { background: color-mix(in srgb, var(--sa-border) 20%, transparent); }
.header-cell.is-today { background: color-mix(in srgb, #ef4444 10%, transparent); color: #ef4444; }

.chart-body {
  position: relative;
}

.chart-grid {
  display: flex;
  position: absolute;
  top: 0; left: 0; right: 0; bottom: 0;
}

.grid-line {
  border-right: 1px solid color-mix(in srgb, var(--sa-border) 50%, transparent);
  height: 100%;
}
.grid-line.weekend { background: color-mix(in srgb, var(--sa-border) 10%, transparent); }

.today-marker {
  position: absolute;
  top: 0; bottom: 0;
  width: 0;
  z-index: 2;
}

.today-line {
  position: absolute;
  left: 0; top: 0; bottom: 0;
  width: 2px;
  background: #ef4444;
}

.today-label {
  position: absolute;
  left: 4px; top: 4px;
  font-size: 9px;
  font-weight: 800;
  color: #ef4444;
  background: var(--sa-surface);
  padding: 1px 4px;
  border-radius: 4px;
}

.chart-rows {
  position: absolute;
  top: 0; left: 0; right: 0; bottom: 0;
}

.chart-row {
  position: absolute;
  width: 100%;
  border-bottom: 1px solid color-mix(in srgb, var(--sa-border) 30%, transparent);
}

.task-bar-wrapper {
  position: absolute;
  top: 5px;
  height: 26px;
  z-index: 3;
}

.task-bar {
  position: relative;
  width: 100%;
  height: 100%;
  border-radius: 6px;
  cursor: pointer;
  overflow: hidden;
  display: flex;
  align-items: center;
  transition: transform 0.1s, box-shadow 0.1s;
  box-shadow: 0 2px 5px rgba(0,0,0,0.05);
}

.task-bar:hover {
  transform: translateY(-1px);
  box-shadow: 0 4px 8px rgba(0,0,0,0.1);
}

.task-bar.is-overdue {
  border: 1px solid #991b1b !important;
}

.bar-progress-fill {
  position: absolute;
  left: 0; top: 0; bottom: 0;
  z-index: 1;
  opacity: 0.9;
}

.bar-content-inner {
  position: relative;
  z-index: 2;
  display: flex;
  align-items: center;
  width: 100%;
  height: 100%;
  padding: 0 7px;
  gap: 6px;
}

.bar-avatar {
  flex-shrink: 0;
}

.bar-title {
  font-size: 11px;
  font-weight: 600;
  color: #fff;
  text-shadow: 0 1px 2px rgba(0,0,0,0.3);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  flex: 1;
}

.bar-progress-text {
  font-size: 11px;
  font-weight: 700;
  color: #fff;
  text-shadow: 0 1px 2px rgba(0,0,0,0.3);
}

.task-warning {
  position: absolute;
  left: 12px;
  top: 50%;
  transform: translateY(-50%);
  font-size: 12px;
  color: #f59e0b;
  display: flex;
  align-items: center;
  gap: 6px;
  font-style: italic;
}

/* Tooltip Styles */
.tooltip-content {
  padding: 4px;
}
.tt-title {
  font-weight: 700;
  font-size: 14px;
  margin-bottom: 8px;
  border-bottom: 1px solid #e2e8f0;
  padding-bottom: 6px;
}
.tt-grid {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 6px 12px;
  font-size: 12px;
}
.tt-lbl {
  color: #64748b;
  font-weight: 500;
}
.tt-val {
  font-weight: 600;
  color: #0f172a;
}

/* SprintA premium timeline pass */
.timeline-body {
  border: 1px solid color-mix(in srgb, var(--color-border) 82%, transparent);
  border-radius: 14px;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 94%, transparent), color-mix(in srgb, var(--color-surface-hover) 44%, var(--color-surface)));
  box-shadow:
    0 12px 34px color-mix(in srgb, #020617 8%, transparent),
    inset 0 1px 0 rgba(255,255,255,0.10);
}

.filters-row {
  flex-wrap: nowrap;
}

.filter-input,
.filter-select,
.zoom-controls-override {
  min-height: 34px;
  border-radius: 9px !important;
  border-color: color-mix(in srgb, var(--color-border) 86%, transparent) !important;
  background: color-mix(in srgb, var(--color-surface) 88%, transparent) !important;
}

.timeline-sidebar,
.sidebar-header,
.sidebar-row,
.chart-header {
  background: color-mix(in srgb, var(--color-surface) 88%, transparent) !important;
}

.timeline-chart-wrapper {
  background:
    color-mix(in srgb, var(--color-bg) 74%, #020617) !important;
}

.sidebar-row:hover {
  background: color-mix(in srgb, var(--color-accent) 9%, var(--color-surface-hover)) !important;
}

.task-name,
.owner-name {
  min-width: 0;
}

.status-badge {
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
}

.zoom-btn.active {
  background: color-mix(in srgb, var(--color-accent) 16%, var(--color-surface)) !important;
  color: var(--color-accent) !important;
}
</style>
