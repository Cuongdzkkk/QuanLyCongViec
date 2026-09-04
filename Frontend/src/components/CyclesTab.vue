<script setup>
import { computed, nextTick, onMounted, onUnmounted, provide, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useSprintStore } from '@/store/useSprintStore'
import { useProjectStore } from '@/store/useProjectStore'
import { hasProjectWritePermission, normalizeProjectRole, hasSystemAdminAccess } from '@/utils/permissions'
import { getStoredUserSession } from '@/utils/authSession'
import { getSprintErrorMessage, getSprintStateMeta, SPRINT_STATE } from '@/utils/sprintState'
import { useI18n } from '@/composables/useI18n'
import axiosClient from '@/api/axiosClient'
import { signalRService } from '@/api/signalrService'
import { subscribeAdminRealtime } from '@/utils/adminRealtime'
import { currentTheme } from '@/utils/theme'
import ProjectPageContainer from '@/components/common/ProjectPageContainer.vue'
import ProjectPageHeader from '@/components/common/ProjectPageHeader.vue'
import ProjectPageToolbar from '@/components/common/ProjectPageToolbar.vue'
import ToolbarSortMenu from '@/components/common/ToolbarSortMenu.vue'
import ToolbarValueFilter from '@/components/common/ToolbarValueFilter.vue'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import FilterBar from '@/components/FilterBar.vue'


import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart } from 'echarts/charts'
import { TitleComponent, TooltipComponent, LegendComponent, GridComponent } from 'echarts/components'
import VChart, { THEME_KEY } from 'vue-echarts'

use([CanvasRenderer, LineChart, TitleComponent, TooltipComponent, LegendComponent, GridComponent])

const props = defineProps({
  projectId: { type: String, required: true }
})

const router = useRouter()
const route = useRoute()
const sprintStore = useSprintStore()
const projectStore = useProjectStore()
const { t } = useI18n()

const chartTheme = computed(() => currentTheme.value === 'dark' ? 'dark' : 'light')
provide(THEME_KEY, chartTheme)

const showCreateModal = ref(false)
const closeCycleCandidate = ref(null)
const closeCycleConfirmButton = ref(null)
const burndownCharts = ref({})
const showCalendar = ref(false)
const currentMonth = ref(new Date().getMonth())
const currentYear = ref(new Date().getFullYear())
const dateSelectionStep = ref(0)
const tempStart = ref(null)
const tempEnd = ref(null)
const newCycle = ref({ name: '', description: '', startDate: null, endDate: null })
const showCycleSearch = ref(false)
const showCycleFilters = ref(false)
const cycleSearchQuery = ref('')
const activeFilters = ref([])

const cycleSortDirection = ref('desc')
const cycleSortBy = ref('startDate')
const cycleSortOptions = [
  { value: 'startDate', label: 'Start date', icon: 'fa-regular fa-calendar' },
  { value: 'endDate', label: 'End date', icon: 'fa-regular fa-calendar-check' },
  { value: 'name', label: 'Name', icon: 'fa-solid fa-font' },
  { value: 'progress', label: 'Progress', icon: 'fa-solid fa-chart-line' }
]

const cycleFilterFields = computed(() => [
  { key: 'progress', label: 'Progress state', icon: 'fa-solid fa-chart-line', values: ['Not started', 'In progress', 'Completed'] }
])

const cycleOperators = {
  progress: ['is', 'is not']
}

const customCycleValueMeta = (fieldKey, value) => {
  if (fieldKey === 'progress') {
    if (value === 'Not started') return { icon: 'fa-regular fa-circle', color: '#94a3b8' }
    if (value === 'In progress') return { icon: 'fa-solid fa-spinner', color: '#38bdf8' }
    if (value === 'Completed') return { icon: 'fa-solid fa-circle-check', color: '#22c55e' }
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

const expandedTabs = ref({
  active: true,
  upcoming: true,
  completed: true
})
const cyclePanelTabs = ref({})
const cycleWorkItems = ref({})
const cycleWorkItemsLoading = ref({})
const expandedCarryOverCycleId = ref(null)
const carryOverItems = ref({})
const carryOverLoading = ref({})
const carryOverFilters = ref({})
const carryOverSelection = ref({})
const carryOverTargetSprintId = ref({})
const carryOverMoving = ref({})

const monthNames = ['Thang 1', 'Thang 2', 'Thang 3', 'Thang 4', 'Thang 5', 'Thang 6', 'Thang 7', 'Thang 8', 'Thang 9', 'Thang 10', 'Thang 11', 'Thang 12']
const dayNames = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7']

const allSprints = computed(() => sprintStore.sprints || [])
const currentProject = computed(() => {
  if (`${projectStore.currentProject?.id || ''}` === `${props.projectId}`) {
    return projectStore.currentProject
  }
  return (projectStore.allProjects || []).find(project => `${project.id}` === `${props.projectId}`) || null
})
const canManageSprint = computed(() => {
  if (hasSystemAdminAccess(getStoredUserSession())) return true
  const role = normalizeProjectRole(
    currentProject.value?.myRole
    || currentProject.value?.MyRole
    || currentProject.value?.projectRole
    || currentProject.value?.ProjectRole
  )
  return hasProjectWritePermission(role)
})
const filteredSprints = computed(() => {
  const keyword = cycleSearchQuery.value.trim().toLowerCase()
  const filtered = allSprints.value.filter(cycle => {
    const matchesSearch = !keyword ||
      `${cycle.name || ''}`.toLowerCase().includes(keyword) ||
      `${cycle.description || ''}`.toLowerCase().includes(keyword)

    if (activeFilters.value.length > 0) {
      return activeFilters.value.every(f => {
        let val = ''
        const progress = cycle.progressPercent || 0
        if (progress === 0) val = 'Not started'
        else if (progress < 100) val = 'In progress'
        else val = 'Completed'

        const isMatch = val === f.value
        return f.operator === 'is' ? isMatch : !isMatch
      })
    }

    return matchesSearch
  })
  return [...filtered].sort((left, right) => {
    let l
    let r
    if (cycleSortBy.value === 'name') {
      l = `${left.name || ''}`.toLowerCase()
      r = `${right.name || ''}`.toLowerCase()
    } else if (cycleSortBy.value === 'progress') {
      l = Number(left.progressPercent || 0)
      r = Number(right.progressPercent || 0)
    } else {
      const field = cycleSortBy.value === 'endDate' ? 'endDate' : 'startDate'
      l = new Date(left[field] || left.createdAt || 0).getTime()
      r = new Date(right[field] || right.createdAt || 0).getTime()
    }
    const result = l < r ? -1 : (l > r ? 1 : 0)
    return cycleSortDirection.value === 'asc' ? result : -result
  })
})
const activeSprints = computed(() => filteredSprints.value.filter(s => s.state === SPRINT_STATE.ACTIVE))
const upcomingSprints = computed(() => filteredSprints.value.filter(s => s.state === SPRINT_STATE.UPCOMING))
const completedSprints = computed(() => filteredSprints.value.filter(s => s.state === SPRINT_STATE.COMPLETED))
const hasCycleFilters = computed(() => Boolean(cycleSearchQuery.value.trim()) || activeFilters.value.length > 0)

const toggleTab = (tab) => {
  expandedTabs.value[tab] = !expandedTabs.value[tab]
}

const clearCycleFilters = () => {
  cycleSearchQuery.value = ''
  activeFilters.value = []
}

const getCyclePanelTab = (cycleId) => cyclePanelTabs.value[cycleId] || 'state'

const setCyclePanelTab = async (cycle, tab) => {
  cyclePanelTabs.value = { ...cyclePanelTabs.value, [cycle.id]: tab }
  if (tab === 'items') {
    await fetchCycleWorkItems(cycle.id)
  }
}

const fetchCycleWorkItems = async (cycleId) => {
  if (cycleWorkItems.value[cycleId] || cycleWorkItemsLoading.value[cycleId]) return

  cycleWorkItemsLoading.value = { ...cycleWorkItemsLoading.value, [cycleId]: true }
  try {
    const res = await axiosClient.get(`/projects/${props.projectId}/WorkTasks`)
    const tasks = res.data?.data || []
    cycleWorkItems.value = {
      ...cycleWorkItems.value,
      [cycleId]: tasks.filter(task => task.sprintId === cycleId && !(task.parentTaskId || task.parentId))
    }
  } catch (error) {
    console.error('Failed to load cycle work items', error)
    cycleWorkItems.value = { ...cycleWorkItems.value, [cycleId]: [] }
  } finally {
    cycleWorkItemsLoading.value = { ...cycleWorkItemsLoading.value, [cycleId]: false }
  }
}

const cycleItemsFor = (cycleId) => cycleWorkItems.value[cycleId] || []
const getCarryOverFilter = (cycleId) => carryOverFilters.value[cycleId] || { search: '', scope: 'all' }
const getCarryOverSelection = (cycleId) => carryOverSelection.value[cycleId] || []
const getCarryOverTargetSprintId = (cycleId) => carryOverTargetSprintId.value[cycleId] || null
const carryOverTasksFor = (cycleId) => carryOverItems.value[cycleId] || []

const visibleCarryOverTasks = (cycleId) => {
  const filters = getCarryOverFilter(cycleId)
  const keyword = `${filters.search || ''}`.trim().toLowerCase()
  return carryOverTasksFor(cycleId).filter(task => {
    const matchesSearch = !keyword ||
      `${task.title || ''}`.toLowerCase().includes(keyword) ||
      `${task.sequenceId || ''}`.toLowerCase().includes(keyword)
    const matchesScope =
      filters.scope === 'all' ||
      (filters.scope === 'backlog' && task.currentLocation === 'Backlog') ||
      (filters.scope === 'cycle' && task.currentLocation === 'Cycle')
    return matchesSearch && matchesScope
  })
}

const availableTargetSprints = (cycleId) => allSprints.value.filter(sprint =>
  sprint.id !== cycleId && sprint.state === SPRINT_STATE.UPCOMING
)

const priorityLabel = (priority) => {
  if (priority === 1) return 'Urgent'
  if (priority === 2) return 'High'
  if (priority === 3) return 'Normal'
  if (priority === 4) return 'Low'
  return 'None'
}

const assigneeLabel = (task) => task.assignedUserName || 'Unassigned'

const formatDateCompact = (d) => {
  if (!d) return ''
  const date = toLocalDate(d)
  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  })
}

const toLocalDate = (value) => {
  if (value instanceof Date) {
    return new Date(value.getFullYear(), value.getMonth(), value.getDate())
  }

  if (typeof value === 'string') {
    const raw = value.split('T')[0]
    const [year, month, day] = raw.split('-').map(Number)
    if (year && month && day) return new Date(year, month - 1, day)
  }

  const date = new Date(value)
  return new Date(date.getFullYear(), date.getMonth(), date.getDate())
}

const toLocalIsoDate = (value) => {
  const date = toLocalDate(value)
  const year = date.getFullYear()
  const month = `${date.getMonth() + 1}`.padStart(2, '0')
  const day = `${date.getDate()}`.padStart(2, '0')
  return `${year}-${month}-${day}T00:00:00`
}

const percentLabel = (cycle) => `${cycle.progressPercent || 0}%`

const progressSegments = (cycle) => {
  const total = Math.max(cycle.taskCount || 0, 1)
  const completed = cycle.completedTaskCount || 0
  const started = cycle.inProgressTaskCount || 0
  const backlog = cycle.backlogTaskCount || 0
  const remaining = Math.max((cycle.taskCount || 0) - completed - started - backlog, 0)

  return [
    { label: t('cyclesTab.completed'), value: completed, width: `${(completed / total) * 100}%`, className: 'bg-green' },
    { label: t('cyclesTab.started'), value: started, width: `${(started / total) * 100}%`, className: 'bg-orange' },
    { label: t('cyclesTab.backlog'), value: backlog, width: `${(backlog / total) * 100}%`, className: 'bg-lightgray' },
    { label: t('cyclesTab.other'), value: remaining, width: `${(remaining / total) * 100}%`, className: 'bg-darkgray' }
  ]
}

const activeItemCount = (cycle) => Math.max((cycle.taskCount || 0) - (cycle.completedTaskCount || 0), 0)
const cycleStateLabel = (state) => getSprintStateMeta(state).label
const isTransitioning = (action, cycleId) =>
  sprintStore.isTransitioning(action, props.projectId, cycleId)

const openCycleBoard = (cycle) => {
  router.push({
    name: 'CycleDetailView',
    params: { id: props.projectId, cycleId: cycle.id },
    query: {
      tab: 'spreadsheet',
      sprintId: cycle.id,
      sprintName: cycle.name
    }
  })
}

const openCarryOverBoard = (cycle) => {
  router.push({
    name: 'CycleDetailView',
    params: { id: props.projectId, cycleId: cycle.id },
    query: {
      tab: 'spreadsheet',
      carryOverFromSprintId: cycle.id
    }
  })
}

const ensureCarryOverState = (cycleId) => {
  if (!carryOverFilters.value[cycleId]) {
    carryOverFilters.value = {
      ...carryOverFilters.value,
      [cycleId]: { search: '', scope: 'all' }
    }
  }
  if (!carryOverSelection.value[cycleId]) {
    carryOverSelection.value = { ...carryOverSelection.value, [cycleId]: [] }
  }
}

const fetchCarryOverTasks = async (cycleId, force = false) => {
  ensureCarryOverState(cycleId)
  if (!force && (carryOverItems.value[cycleId] || carryOverLoading.value[cycleId])) return

  carryOverLoading.value = { ...carryOverLoading.value, [cycleId]: true }
  try {
    const res = await axiosClient.get(`/projects/${props.projectId}/sprints/${cycleId}/carry-over-tasks`)
    carryOverItems.value = { ...carryOverItems.value, [cycleId]: res.data?.data || [] }
    carryOverSelection.value = { ...carryOverSelection.value, [cycleId]: [] }
  } catch (error) {
    console.error('Failed to load carry-over tasks', error)
    carryOverItems.value = { ...carryOverItems.value, [cycleId]: [] }
  } finally {
    carryOverLoading.value = { ...carryOverLoading.value, [cycleId]: false }
  }
}

const toggleCarryOverPlanner = async (cycle) => {
  expandedCarryOverCycleId.value = expandedCarryOverCycleId.value === cycle.id ? null : cycle.id
  if (expandedCarryOverCycleId.value === cycle.id) {
    await fetchCarryOverTasks(cycle.id)
  }
}

const updateCarryOverFilter = (cycleId, patch) => {
  ensureCarryOverState(cycleId)
  carryOverFilters.value = {
    ...carryOverFilters.value,
    [cycleId]: { ...carryOverFilters.value[cycleId], ...patch }
  }
}

const toggleCarryOverSelection = (cycleId, taskId) => {
  const next = new Set(getCarryOverSelection(cycleId))
  if (next.has(taskId)) next.delete(taskId)
  else next.add(taskId)
  carryOverSelection.value = { ...carryOverSelection.value, [cycleId]: Array.from(next) }
}

const toggleSelectAllCarryOver = (cycleId) => {
  const visibleIds = visibleCarryOverTasks(cycleId).map(task => task.id)
  const selected = getCarryOverSelection(cycleId)
  const shouldSelectAll = visibleIds.some(id => !selected.includes(id))
  carryOverSelection.value = {
    ...carryOverSelection.value,
    [cycleId]: shouldSelectAll ? visibleIds : []
  }
}

const carryOverSelectedCount = (cycleId) => getCarryOverSelection(cycleId).length

const moveCarryOverTasks = async (cycleId, targetSprintId = null) => {
  const taskIds = getCarryOverSelection(cycleId)
  if (!taskIds.length) return

  carryOverMoving.value = { ...carryOverMoving.value, [cycleId]: true }
  try {
    await axiosClient.post(`/projects/${props.projectId}/sprints/${cycleId}/carry-over-tasks/move`, {
      taskIds,
      targetSprintId
    })
    await Promise.all([
      loadCycles(true),
      fetchCarryOverTasks(cycleId, true)
    ])
  } catch (error) {
    console.error('Failed to move carry-over tasks', error)
    alert(error.response?.data?.message || 'Khong the cap nhat task ton dong')
  } finally {
    carryOverMoving.value = { ...carryOverMoving.value, [cycleId]: false }
  }
}

const getBurndownPalette = () => {
  const dark = currentTheme.value === 'dark'
  return {
    text: dark ? '#cbd5e1' : '#475569',
    grid: dark ? 'rgba(203, 213, 225, 0.10)' : 'rgba(71, 85, 105, 0.16)',
    current: dark ? '#60a5fa' : '#2563eb',
    currentArea: dark ? 'rgba(96, 165, 250, 0.18)' : 'rgba(37, 99, 235, 0.12)',
    ideal: dark ? '#f8fafc' : '#0f172a',
    tooltipBg: dark ? 'rgba(15, 23, 42, 0.96)' : 'rgba(255, 255, 255, 0.98)',
    tooltipBorder: dark ? 'rgba(148, 163, 184, 0.24)' : 'rgba(148, 163, 184, 0.36)'
  }
}

let burndownRequestSequence = 0
const fetchBurndowns = async () => {
  const requestId = ++burndownRequestSequence
  const projectId = props.projectId
  const chartEntries = {}
  const palette = getBurndownPalette()
  await Promise.all(activeSprints.value.map(async (sprint) => {
    try {
      const res = await axiosClient.get(`/projects/${projectId}/sprints/${sprint.id}/burndown`)
      const burndown = res.data?.data || []
      chartEntries[sprint.id] = {
        backgroundColor: 'transparent',
        tooltip: {
          trigger: 'axis',
          backgroundColor: palette.tooltipBg,
          borderColor: palette.tooltipBorder,
          textStyle: { color: palette.ideal }
        },
        legend: {
          data: [t('cyclesTab.currentWorkItems', 'Current work items'), t('cyclesTab.idealWorkItems', 'Ideal work items')],
          bottom: 0,
          textStyle: { color: palette.text, fontSize: 10, fontWeight: 700 }
        },
        grid: { top: 10, left: 30, right: 10, bottom: 40 },
        xAxis: {
          type: 'category',
          data: burndown.map(item => item.date),
          axisLine: { show: false },
          axisTick: { show: false },
          axisLabel: { color: palette.text, fontSize: 9, fontWeight: 700 }
        },
        yAxis: {
          type: 'value',
          splitLine: { lineStyle: { color: palette.grid } },
          axisLabel: { color: palette.text, fontSize: 10, fontWeight: 700 }
        },
        series: [
          {
            name: t('cyclesTab.currentWorkItems', 'Current work items'),
            type: 'line',
            data: burndown.map(item => item.actualRemaining ?? item.remainingPoints ?? 0),
            itemStyle: { color: palette.current },
            lineStyle: { width: 2 },
            areaStyle: { color: palette.currentArea },
            symbol: 'circle',
            symbolSize: 7,
            step: 'end',
            smooth: false
          },
          {
            name: t('cyclesTab.idealWorkItems', 'Ideal work items'),
            type: 'line',
            data: burndown.map(item => item.idealRemaining ?? item.idealPoints ?? 0),
            itemStyle: { color: palette.ideal },
            lineStyle: { type: 'dashed', width: 2, color: palette.ideal },
            symbol: 'circle',
            symbolSize: 7,
            smooth: false
          }
        ]
      }
    } catch (error) {
      console.error('Failed to load burndown', error)
    }
  }))
  if (requestId === burndownRequestSequence && props.projectId === projectId) {
    burndownCharts.value = chartEntries
  }
}

const loadCycles = async (force = false) => {
  await sprintStore.fetchSprints(props.projectId, { force })
  await fetchBurndowns()
}

const fixDateOffset = (dt) => {
  if (!dt) return null
  return toLocalIsoDate(dt)
}

const createNewCycle = async () => {
  if (!canManageSprint.value) return
  if (!newCycle.value.name?.trim() || !newCycle.value.startDate) return

  let finalEndDate = newCycle.value.endDate ? fixDateOffset(newCycle.value.endDate) : null
  if (!finalEndDate) {
    const fallback = new Date(newCycle.value.startDate)
    fallback.setDate(fallback.getDate() + 14)
    finalEndDate = fixDateOffset(fallback)
  }

  try {
    await sprintStore.createSprint(props.projectId, {
      name: newCycle.value.name.trim(),
      description: newCycle.value.description,
      startDate: fixDateOffset(newCycle.value.startDate),
      endDate: finalEndDate
    })

    showCreateModal.value = false
    showCalendar.value = false
    newCycle.value = { name: '', description: '', startDate: null, endDate: null }
    await fetchBurndowns()
  } catch (error) {
    alert(error.response?.data?.message || 'Không thể tạo cycle')
  }
}

const startCycle = async (cycle) => {
  if (
    !canManageSprint.value ||
    !getSprintStateMeta(cycle.state).canStart ||
    isTransitioning('start', cycle.id)
  ) return

  try {
    const result = await sprintStore.startSprint(props.projectId, cycle.id)
    if (result?.data) {
      ElMessage.success(`Đã bắt đầu Cycle "${cycle.name}".`)
      await fetchBurndowns()
    }
  } catch (error) {
    ElMessage.error(getSprintErrorMessage(error))
  }
}

const openCloseCycleModal = (cycle) => {
  if (!canManageSprint.value || !getSprintStateMeta(cycle.state).canClose) return
  closeCycleCandidate.value = cycle
}

const dismissCloseCycleModal = () => {
  if (
    closeCycleCandidate.value &&
    isTransitioning('close', closeCycleCandidate.value.id)
  ) return
  closeCycleCandidate.value = null
}

const closeCycleToBacklog = async () => {
  const cycle = closeCycleCandidate.value
  if (
    !cycle ||
    !canManageSprint.value ||
    !getSprintStateMeta(cycle.state).canClose ||
    isTransitioning('close', cycle.id)
  ) return

  try {
    const result = await sprintStore.closeSprint(props.projectId, cycle.id, null)
    if (result?.data) {
      closeCycleCandidate.value = null
      ElMessage.success(`Đã đóng Cycle "${cycle.name}".`)
      await fetchBurndowns()
    }
  } catch (error) {
    ElMessage.error(getSprintErrorMessage(error))
  }
}

const handleTransitionEscape = (event) => {
  if (event.key === 'Escape') dismissCloseCycleModal()
}

const toggleCalendar = () => {
  showCalendar.value = !showCalendar.value
  if (showCalendar.value) {
    tempStart.value = newCycle.value.startDate
    tempEnd.value = newCycle.value.endDate
    dateSelectionStep.value = 0
  }
}

const daysInMonth = computed(() => {
  const days = []
  const date = new Date(currentYear.value, currentMonth.value, 1)
  const firstDay = date.getDay()
  const lastDate = new Date(currentYear.value, currentMonth.value + 1, 0).getDate()
  const prevLastDate = new Date(currentYear.value, currentMonth.value, 0).getDate()

  for (let i = firstDay - 1; i >= 0; i -= 1) {
    days.push({ day: prevLastDate - i, isCurrent: false, date: null })
  }

  for (let i = 1; i <= lastDate; i += 1) {
    days.push({ day: i, isCurrent: true, date: new Date(currentYear.value, currentMonth.value, i) })
  }

  const remainder = days.length % 7
  if (remainder !== 0) {
    for (let i = 1; i <= 7 - remainder; i += 1) {
      days.push({ day: i, isCurrent: false, date: null })
    }
  }

  return days
})

const moveMonth = (direction) => {
  currentMonth.value += direction
  if (currentMonth.value > 11) {
    currentMonth.value = 0
    currentYear.value += 1
  }
  if (currentMonth.value < 0) {
    currentMonth.value = 11
    currentYear.value -= 1
  }
}

const isSameDate = (left, right) => left && right && left.getFullYear() === right.getFullYear() && left.getMonth() === right.getMonth() && left.getDate() === right.getDate()

const todayStart = () => {
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  return today
}

const isPastDate = (date) => {
  if (!date) return false
  return toLocalDate(date).getTime() < todayStart().getTime()
}

const selectDate = (day) => {
  if (!day.isCurrent || isPastDate(day.date)) return
  const picked = day.date
  if (dateSelectionStep.value === 0) {
    tempStart.value = picked
    tempEnd.value = null
    newCycle.value.startDate = picked
    newCycle.value.endDate = null
    dateSelectionStep.value = 1
    return
  }

  if (picked < tempStart.value) {
    tempStart.value = picked
    tempEnd.value = null
    newCycle.value.startDate = picked
    newCycle.value.endDate = null
    return
  }

  tempEnd.value = picked
  newCycle.value.endDate = picked
  dateSelectionStep.value = 0
  showCalendar.value = false
}

const isSelectedStart = (date) => isSameDate(date, tempStart.value)
const isSelectedEnd = (date) => isSameDate(date, tempEnd.value)
const isInRange = (day) => {
  if (!day.date || !tempStart.value || !tempEnd.value || !day.isCurrent) return false
  const time = day.date.getTime()
  return time > tempStart.value.getTime() && time < tempEnd.value.getTime()
}

const btnDateText = computed(() => {
  if (!newCycle.value.startDate) return 'Chon khoang thoi gian'
  const start = formatDateCompact(newCycle.value.startDate)
  const end = newCycle.value.endDate ? formatDateCompact(newCycle.value.endDate) : '...'
  return `${start} -> ${end}`
})

watch(
  () => props.projectId,
  () => {
    burndownRequestSequence += 1
    burndownCharts.value = {}
    cycleWorkItems.value = {}
    carryOverItems.value = {}
    expandedCarryOverCycleId.value = null
    closeCycleCandidate.value = null
    loadCycles(true)
  },
  { immediate: true }
)
watch(closeCycleCandidate, async (cycle) => {
  if (!cycle) return
  await nextTick()
  closeCycleConfirmButton.value?.focus()
})
watch(
  () => route.query.carryOverFromSprintId,
  async (cycleId) => {
    if (!cycleId || typeof cycleId !== 'string') return
    expandedTabs.value.completed = true
    expandedCarryOverCycleId.value = cycleId
    await fetchCarryOverTasks(cycleId, true)
  },
  { immediate: true }
)

watch(currentTheme, () => {
  fetchBurndowns()
})

let cycleRefreshTimer = null
let cycleTaskRefreshTimer = null
let unsubscribeAdminRealtime = null
let sprintRealtimeHandler = null
let taskRealtimeHandler = null
onMounted(() => {
  window.addEventListener('keydown', handleTransitionEscape)
  document.addEventListener('click', handleOutsideClick)
  signalRService.startConnection(props.projectId)
  sprintRealtimeHandler = (event) => {
    if (event?.projectId && `${event.projectId}` !== `${props.projectId}`) return
    if (`${event?.entityType || ''}`.toLowerCase() !== 'sprint') return
    sprintStore.applyRealtimeEntityEvent(event)
    fetchBurndowns()
  }
  signalRService.on('EntityChanged', sprintRealtimeHandler)
  taskRealtimeHandler = () => {
    clearTimeout(cycleTaskRefreshTimer)
    cycleTaskRefreshTimer = window.setTimeout(() => {
      cycleWorkItems.value = {}
      loadCycles(true)
    }, 150)
  }
  signalRService.on('TaskUpdated', taskRealtimeHandler)
  signalRService.on('WorkTaskUpdated', taskRealtimeHandler)
  signalRService.on('TasksUpdated', taskRealtimeHandler)
  cycleRefreshTimer = window.setInterval(() => {
    if (props.projectId) {
      loadCycles()
    }
  }, 60000)

  unsubscribeAdminRealtime = subscribeAdminRealtime(async ({ type, payload }) => {
    if (!props.projectId) return
    if (payload?.projectId && `${payload.projectId}` !== `${props.projectId}`) return

    if (
      [
        'project-settings-updated',
        'project-settings-favorite-updated',
        'project-settings-integrations-updated',
        'project-administration-updated'
      ].includes(type)
    ) {
      await loadCycles(true)
    }
  })
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleTransitionEscape)
  document.removeEventListener('click', handleOutsideClick)
  burndownRequestSequence += 1
  if (cycleRefreshTimer) {
    window.clearInterval(cycleRefreshTimer)
  }
  clearTimeout(cycleTaskRefreshTimer)
  unsubscribeAdminRealtime?.()
  if (sprintRealtimeHandler) signalRService.off('EntityChanged', sprintRealtimeHandler)
  if (taskRealtimeHandler) {
    signalRService.off('TaskUpdated', taskRealtimeHandler)
    signalRService.off('WorkTaskUpdated', taskRealtimeHandler)
    signalRService.off('TasksUpdated', taskRealtimeHandler)
  }
})
</script>

<template>
  <ProjectPageContainer scrollable>
    <ProjectPageHeader 
        icon="fa-solid fa-rotate" 
        :title="t('cyclesTab.cycles', 'Cycles')" 
        :description="t('cyclesTab.cyclesDesc', 'Manage project sprints and iterations')"
      >
        <template #actions>
          <button class="nexus-btn-primary cycles-add-button" type="button" @click="showCreateModal = true" :disabled="!canManageSprint" :title="!canManageSprint ? t('cyclesTab.noPermissionToAddCycle', 'You do not have permission to add a cycle') : ''">
            <i class="fa-solid fa-plus"></i><span class="cycle-button-label">{{ t('cyclesTab.addCycle', 'Add cycle') }}</span>
          </button>
        </template>
      </ProjectPageHeader>

      <ProjectPageToolbar
        :showSearch="true"
        v-model:searchQuery="cycleSearchQuery"
        :searchPlaceholder="t('cyclesTab.searchCycles', 'Search cycles...')"
      >
        
        <template #filters>
          <div class="filter-dropdown-wrapper js-toolbar-popup-scope">
            <button
              class="timeline-filter-trigger icon-only-trigger"
              type="button"
              aria-label="Filters"
              title="Bộ lọc"
              @click="toggleFilterDropdown"
              :class="{ active: showFilterDropdown || activeFilters.length }"
            >
              <i class="fa-solid fa-filter"></i>
              <span v-if="activeFilters.length" class="filter-count">{{ activeFilters.length }}</span>
            </button>
            <div class="plane-dropdown-menu filter-dropdown-menu" v-show="showFilterDropdown" @click.stop>
              <FilterBar
                v-model:filters="activeFilters"
                :fields="cycleFilterFields"
                :operators="cycleOperators"
                :custom-value-meta="customCycleValueMeta"
                :active="showFilterDropdown"
              />
            </div>
          </div>
        </template>
        <template #sort>
          <ToolbarSortMenu v-model="cycleSortBy" v-model:direction="cycleSortDirection" label="Sort cycles" :options="cycleSortOptions" />
        </template>
      </ProjectPageToolbar>

    <div v-if="sprintStore.loading && allSprints.length === 0" class="cycle-load-state" role="status">
      <i class="fa-solid fa-circle-notch fa-spin" aria-hidden="true"></i>
      Đang tải Cycle...
    </div>
    <div v-else-if="sprintStore.error && allSprints.length === 0" class="cycle-load-state cycle-load-error" role="alert">
      <i class="fa-solid fa-triangle-exclamation" aria-hidden="true"></i>
      {{ sprintStore.error.message || 'Không thể tải danh sách Cycle.' }}
      <button type="button" class="nexus-btn-outlined" @click="loadCycles(true)">Thử lại</button>
    </div>

    <div
      v-if="allSprints.length > 0 || (!sprintStore.loading && !sprintStore.error)"
      class="cycles-body"
    >
      <div class="cycle-section">
        <div class="cs-header" @click="toggleTab('active')">
          <i class="chevron fa-solid" :class="expandedTabs.active ? 'fa-chevron-down' : 'fa-chevron-right'"></i>
          <i class="fa-solid fa-circle-half-stroke text-orange"></i>
          <span class="cs-title">{{ t('cyclesTab.activeCycle', 'Active cycle') }}</span>
        </div>

        <div class="cs-content" v-show="expandedTabs.active">
          <div class="empty-state text-muted" v-if="activeSprints.length === 0">{{ t('cyclesTab.noActiveCycles', 'No active cycles.') }}</div>
          <div class="cycle-card expanded" v-for="cycle in activeSprints" :key="cycle.id">
            <div class="cc-top">
              <div class="cct-left">
                <div class="progress-ring text-orange">{{ percentLabel(cycle) }}</div>
                <span class="cycle-name">{{ cycle.name }}</span>
              </div>
              <div class="cct-right">
                <button
                  v-if="canManageSprint"
                  class="plane-danger-btn"
                  type="button"
                  :disabled="isTransitioning('close', cycle.id)"
                  :aria-label="`Đóng Cycle ${cycle.name}`"
                  title="Đóng Cycle"
                  @click.stop="openCloseCycleModal(cycle)"
                >
                  <i
                    class="fa-solid"
                    :class="isTransitioning('close', cycle.id) ? 'fa-circle-notch fa-spin' : 'fa-circle-check'"
                    aria-hidden="true"
                  ></i>
                  {{ isTransitioning('close', cycle.id) ? 'Đang đóng...' : 'Đóng Cycle' }}
                </button>
                <span class="detail-link cursor-pointer hover:text-white" @click.stop="openCycleBoard(cycle)">
                  <i class="fa-solid fa-info-circle"></i> {{ t('cyclesTab.openBoard', 'Open board') }}
                </span>
                <span class="date-range">
                  <i class="fa-regular fa-calendar"></i>
                  {{ formatDateCompact(cycle.startDate) }} - {{ formatDateCompact(cycle.endDate) }}
                </span>
                <button class="icon-btn" @click.stop="sprintStore.toggleFavorite(props.projectId, cycle.id)">
                  <i class="fa-solid fa-star text-orange-400" v-if="cycle.isFavorite"></i>
                  <i class="fa-regular fa-star" v-else></i>
                </button>
              </div>
            </div>

            <div class="cc-grid">
              <div class="grid-panel panel-progress">
                <div class="gp-header">
                  <span>{{ t('cyclesTab.progress', 'Progress') }}</span>
                  <span class="sub">{{ t('cyclesTab.workItems', 'Work items') }}</span>
                </div>
                <div class="progress-bar-container">
                  <div
                    v-for="segment in progressSegments(cycle)"
                    :key="segment.label"
                    class="pb-segment"
                    :class="segment.className"
                    :style="{ width: segment.width }"
                  ></div>
                </div>
                <div class="legend-list">
                  <div v-for="segment in progressSegments(cycle)" :key="segment.label" class="legend-item">
                    <span class="dot" :class="segment.className"></span>
                    {{ segment.label }}
                    <span class="val">{{ segment.value }}</span>
                  </div>
                </div>
              </div>

              <div class="grid-panel panel-chart">
                <div class="gp-header">
                  <span>{{ t('cyclesTab.workItemBurndown', 'Work item burndown') }}</span>
                  <span class="sub text-right">{{ percentLabel(cycle) }}</span>
                </div>
                <div class="burndown-chart-panel" style="height: 140px;">
                  <v-chart v-if="burndownCharts[cycle.id]" :option="burndownCharts[cycle.id]" :theme="chartTheme" autoresize />
                  <div v-else class="text-muted text-xs text-center pt-8">{{ t('cyclesTab.noBurndownData', 'No burndown data yet.') }}</div>
                </div>
              </div>

              <div class="grid-panel panel-tabs">
                <div class="tabs-header">
                  <button class="tab-h" :class="{ active: getCyclePanelTab(cycle.id) === 'state' }" @click="setCyclePanelTab(cycle, 'state')">{{ t('cyclesTab.cycleState', 'Cycle state') }}</button>
                  <button class="tab-h" :class="{ active: getCyclePanelTab(cycle.id) === 'items' }" @click="setCyclePanelTab(cycle, 'items')">{{ t('cyclesTab.workItems', 'Work items') }}</button>
                </div>
                <div class="tabs-body" v-if="getCyclePanelTab(cycle.id) === 'state'">
                  <div class="tab-row">
                    <div class="tr-user">
                      <i class="fa-solid fa-arrows-spin avatar-icon"></i> {{ cycleStateLabel(cycle.state) }}
                    </div>
                    <div class="tr-stat text-muted">{{ activeItemCount(cycle) }} {{ t('cyclesTab.items') }}</div>
                  </div>
                  <div class="tab-row">
                    <div class="tr-user">
                      <i class="fa-solid fa-circle-check avatar-icon"></i> {{ t('cyclesTab.completed', 'Completed') }}
                    </div>
                    <div class="tr-stat text-muted">{{ cycle.completedTaskCount || 0 }}</div>
                  </div>
                </div>
                <div class="tabs-body work-items-body" v-else>
                  <div v-if="cycleWorkItemsLoading[cycle.id]" class="tab-empty text-muted">{{ t('cyclesTab.loadingWorkItems', 'Loading work items...') }}</div>
                  <div v-else-if="cycleItemsFor(cycle.id).length === 0" class="tab-empty text-muted">{{ t('cyclesTab.noWorkItemsInCycle', 'No work items in this cycle.') }}</div>
                  <div v-else class="cycle-work-item" v-for="item in cycleItemsFor(cycle.id)" :key="item.id">
                    <div class="work-item-main">
                      <span class="work-item-id">{{ item.sequenceId || item.id?.substring(0, 8).toUpperCase() }}</span>
                      <span class="work-item-title">{{ item.title }}</span>
                    </div>
                    <div class="work-item-meta">
                      <span>{{ item.statusName || 'Backlog' }}</span>
                      <span>{{ priorityLabel(item.priority) }}</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="cycle-section">
        <div class="cs-header" @click="toggleTab('upcoming')">
          <i class="chevron fa-solid" :class="expandedTabs.upcoming ? 'fa-chevron-down' : 'fa-chevron-right'"></i>
          <i class="fa-regular fa-circle-dashed text-blue"></i>
          <span class="cs-title">{{ t('cyclesTab.upcomingCycle', 'Upcoming cycle') }}</span>
          <span class="cs-count">{{ upcomingSprints.length }}</span>
        </div>

        <div class="cs-content" v-show="expandedTabs.upcoming">
          <div class="empty-state text-muted" v-if="upcomingSprints.length === 0">{{ t('cyclesTab.noUpcomingCycles', 'No upcoming cycles.') }}</div>
          <div class="cycle-card collapsed hover-card" v-for="cycle in upcomingSprints" :key="cycle.id">
            <div class="cct-left">
              <div class="progress-ring text-muted">{{ percentLabel(cycle) }}</div>
              <span class="cycle-name">{{ cycle.name }}</span>
            </div>
            <div class="cct-right">
              <span class="date-range mr-4">
                <i class="fa-regular fa-calendar"></i>
                {{ formatDateCompact(cycle.startDate) }} - {{ formatDateCompact(cycle.endDate) }}
              </span>
              <button
                v-if="canManageSprint && getSprintStateMeta(cycle.state).canStart"
                class="plane-primary-btn"
                style="margin-right: 8px;"
                type="button"
                :disabled="isTransitioning('start', cycle.id)"
                :aria-label="`Bắt đầu Cycle ${cycle.name}`"
                title="Bắt đầu Cycle"
                @click.stop="startCycle(cycle)"
              >
                <i
                  class="fa-solid"
                  :class="isTransitioning('start', cycle.id) ? 'fa-circle-notch fa-spin' : 'fa-play'"
                  aria-hidden="true"
                ></i>
                {{ isTransitioning('start', cycle.id) ? 'Đang bắt đầu...' : t('cyclesTab.startCycle', 'Start cycle') }}
              </button>
              <span class="detail-link cursor-pointer hover:text-white" @click.stop="openCycleBoard(cycle)">
                <i class="fa-solid fa-info-circle"></i> {{ t('cyclesTab.openBoard', 'Open board') }}
              </span>
              <button class="icon-btn" @click.stop="sprintStore.toggleFavorite(props.projectId, cycle.id)">
                <i class="fa-solid fa-star text-orange-400" v-if="cycle.isFavorite"></i>
                <i class="fa-regular fa-star" v-else></i>
              </button>
            </div>
          </div>
        </div>
      </div>

      <div class="cycle-section">
        <div class="cs-header" @click="toggleTab('completed')">
          <i class="chevron fa-solid" :class="expandedTabs.completed ? 'fa-chevron-down' : 'fa-chevron-right'"></i>
          <i class="fa-solid fa-circle-check text-green"></i>
          <span class="cs-title">{{ t('cyclesTab.completedCycle', 'Completed cycle') }}</span>
          <span class="cs-count">{{ completedSprints.length }}</span>
        </div>

        <div class="cs-content" v-show="expandedTabs.completed">
          <div class="empty-state text-muted" v-if="completedSprints.length === 0">{{ t('cyclesTab.noCompletedCyclesYet', 'No completed cycles yet.') }}</div>
          <div v-for="cycle in completedSprints" :key="cycle.id" class="completed-cycle-wrapper">
            <div class="cycle-card collapsed hover-card">
              <div class="cct-left">
                <div class="progress-ring text-green">{{ percentLabel(cycle) }}</div>
                <span class="cycle-name">{{ cycle.name }}</span>
              </div>
              <div class="cct-right">
                <span class="completed-badge">{{ t('cyclesTab.completed', 'Completed') }}</span>
                <span class="detail-link cursor-pointer hover:text-white" @click.stop="openCycleBoard(cycle)">
                  <i class="fa-solid fa-info-circle"></i> {{ t('cyclesTab.openBoard', 'Open board') }}
                </span>
                <span class="date-range">
                  <i class="fa-regular fa-calendar"></i>
                  {{ formatDateCompact(cycle.startDate) }} - {{ formatDateCompact(cycle.endDate) }}
                </span>
                <span class="task-count-badge" v-if="cycle.taskCount">
                  <i class="fa-solid fa-layer-group"></i> {{ cycle.taskCount }}
                </span>
                <button class="icon-btn" @click.stop="sprintStore.toggleFavorite(props.projectId, cycle.id)">
                  <i class="fa-solid fa-star text-orange-400" v-if="cycle.isFavorite"></i>
                  <i class="fa-regular fa-star" v-else></i>
                </button>
                <button v-if="canManageSprint" class="filter-action" type="button" @click.stop="toggleCarryOverPlanner(cycle)" :class="{ active: expandedCarryOverCycleId === cycle.id }">
                  <i class="fa-solid fa-list-check"></i> {{ t('cyclesTab.carryOver', 'Carry-over') }}
                </button>
              </div>
            </div>

            <div v-if="expandedCarryOverCycleId === cycle.id" class="carry-over-panel">
              <div class="carry-over-toolbar">
                <div class="carry-over-filters">
                  <input
                    class="carry-over-search"
                    :value="getCarryOverFilter(cycle.id).search"
                    type="text"
                    placeholder="Search carry-over tasks"
                    @input="updateCarryOverFilter(cycle.id, { search: $event.target.value })"
                  />
                  <select
                    class="carry-over-select"
                    :value="getCarryOverFilter(cycle.id).scope"
                    @change="updateCarryOverFilter(cycle.id, { scope: $event.target.value })"
                  >
                    <option value="all">All locations</option>
                    <option value="backlog">Backlog only</option>
                    <option value="cycle">Cycle only</option>
                  </select>
                  <button class="filter-action" type="button" @click="fetchCarryOverTasks(cycle.id, true)">Refresh</button>
                  <button class="filter-action" type="button" @click="openCarryOverBoard(cycle)">Open board</button>
                </div>
                <div class="carry-over-actions">
                  <span class="text-muted">{{ carryOverSelectedCount(cycle.id) }} selected</span>
                  <select
                    class="carry-over-select"
                    :value="getCarryOverTargetSprintId(cycle.id)"
                    @change="carryOverTargetSprintId = { ...carryOverTargetSprintId, [cycle.id]: $event.target.value || null }"
                  >
                    <option :value="null">Move to cycle...</option>
                    <option v-for="target in availableTargetSprints(cycle.id)" :key="target.id" :value="target.id">
                      {{ target.name }}
                    </option>
                  </select>
                  <button class="filter-action" type="button" :disabled="carryOverMoving[cycle.id]" @click="moveCarryOverTasks(cycle.id, null)">Move to backlog</button>
                  <button
                    class="primary-action"
                    type="button"
                    :disabled="carryOverMoving[cycle.id] || !getCarryOverTargetSprintId(cycle.id)"
                    @click="moveCarryOverTasks(cycle.id, getCarryOverTargetSprintId(cycle.id))"
                  >
                    Move selected
                  </button>
                </div>
              </div>

              <div v-if="carryOverLoading[cycle.id]" class="tab-empty text-muted">Loading carry-over tasks...</div>
              <div v-else-if="visibleCarryOverTasks(cycle.id).length === 0" class="tab-empty text-muted">No carry-over tasks from this completed cycle.</div>
              <div v-else class="carry-over-table">
                <div class="carry-over-head">
                  <label class="carry-over-check">
                    <input type="checkbox" :checked="visibleCarryOverTasks(cycle.id).length > 0 && carryOverSelectedCount(cycle.id) === visibleCarryOverTasks(cycle.id).length" @change="toggleSelectAllCarryOver(cycle.id)" />
                    <span>Select all visible</span>
                  </label>
                  <span class="text-muted">{{ visibleCarryOverTasks(cycle.id).length }} tasks</span>
                </div>

                <div v-for="task in visibleCarryOverTasks(cycle.id)" :key="task.id" class="carry-over-row">
                  <label class="carry-over-check">
                    <input type="checkbox" :checked="getCarryOverSelection(cycle.id).includes(task.id)" @change="toggleCarryOverSelection(cycle.id, task.id)" />
                  </label>
                  <div class="carry-over-main">
                    <span class="work-item-id">{{ task.sequenceId || task.id?.substring(0, 8).toUpperCase() }}</span>
                    <span class="work-item-title">{{ task.title }}</span>
                  </div>
                  <div class="carry-over-meta">
                    <span>{{ task.statusName }}</span>
                    <span>{{ priorityLabel(task.priority) }}</span>
                    <span>{{ assigneeLabel(task) }}</span>
                    <span>{{ task.currentSprintName || 'Backlog' }}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div
      v-if="closeCycleCandidate"
      class="modal-overlay transition-modal-overlay"
      role="presentation"
      @click.self="dismissCloseCycleModal"
    >
      <section
        class="transition-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby="close-cycle-title"
        aria-describedby="close-cycle-description"
      >
        <header class="transition-modal-header">
          <div class="transition-modal-icon" aria-hidden="true">
            <i class="fa-solid fa-flag-checkered"></i>
          </div>
          <div>
            <h2 id="close-cycle-title">Đóng Cycle</h2>
            <p>{{ closeCycleCandidate.name }}</p>
          </div>
        </header>
        <div id="close-cycle-description" class="transition-modal-body">
          <p>Cycle sẽ được đánh dấu hoàn tất theo trạng thái mới nhất từ máy chủ.</p>
          <div class="transition-warning">
            <i class="fa-solid fa-circle-info" aria-hidden="true"></i>
            <span>Task chưa hoàn tất sẽ được chuyển về Backlog. Task, trạng thái và người được giao sẽ không bị xóa hoặc tự động hoàn tất.</span>
          </div>
        </div>
        <footer class="transition-modal-actions">
          <button
            class="cm-btn-cancel"
            type="button"
            :disabled="isTransitioning('close', closeCycleCandidate.id)"
            @click="dismissCloseCycleModal"
          >
            Hủy
          </button>
          <button
            ref="closeCycleConfirmButton"
            class="plane-danger-btn"
            type="button"
            :disabled="isTransitioning('close', closeCycleCandidate.id)"
            aria-label="Xác nhận đóng Cycle"
            @click="closeCycleToBacklog"
          >
            <i
              class="fa-solid"
              :class="isTransitioning('close', closeCycleCandidate.id) ? 'fa-circle-notch fa-spin' : 'fa-circle-check'"
              aria-hidden="true"
            ></i>
            {{ isTransitioning('close', closeCycleCandidate.id) ? 'Đang đóng...' : 'Đóng Cycle' }}
          </button>
        </footer>
      </section>
    </div>

    <Teleport to="body">
    <div class="modal-overlay sa-data-modal-overlay" v-if="showCreateModal && canManageSprint" @click.self="showCreateModal = false; showCalendar = false">
      <div class="create-cycle-modal sa-modal--form" :class="{ 'with-horizontal-calendar': showCalendar }">
        <div class="cm-header">
          <DataModalHeader
            icon="bi bi-arrow-repeat"
            title="Create cycle"
            description="Plan a focused timebox for the selected project"
            close-label="Close"
            @close="showCreateModal = false; showCalendar = false"
          />
        </div>

        <div class="cm-body">
          <div class="cm-layout-grid" :class="{ 'has-side-calendar': showCalendar }">
            <div class="cm-form-col">
              <div class="cm-field">
                <label for="new-cycle-title">Title</label>
                <input id="new-cycle-title" v-model="newCycle.name" type="text" class="cm-input" placeholder="Enter a cycle title" autofocus />
              </div>
              <div class="cm-field">
                <label for="new-cycle-description">Description</label>
                <textarea id="new-cycle-description" v-model="newCycle.description" class="cm-textarea" placeholder="Add a description" rows="4"></textarea>
              </div>

              <div class="dp-wrapper mt-4">
                <button class="dp-btn" @click="toggleCalendar" type="button">
                  <i class="fa-regular fa-calendar"></i> {{ btnDateText }}
                  <i class="fa-solid fa-chevron-right ml-auto text-xs toggle-icon" :class="{ 'rotate-90': showCalendar }"></i>
                </button>
              </div>
            </div>

            <div class="cm-calendar-col" v-if="showCalendar">
              <div class="dp-popover-inline" @click.stop>
                <div class="dp-header">
                  <div class="dp-month-year">
                    <span>{{ monthNames[currentMonth] }}</span>
                    <span>{{ currentYear }}</span>
                  </div>
                  <div class="dp-nav">
                    <button type="button" @click="moveMonth(-1)"><i class="fa-solid fa-chevron-left"></i></button>
                    <button type="button" @click="moveMonth(1)"><i class="fa-solid fa-chevron-right"></i></button>
                  </div>
                </div>

                <div class="dp-grid">
                  <div class="dp-day-num headday" v-for="dayName in dayNames" :key="dayName">{{ dayName }}</div>

                  <div class="dp-day-wrapper" v-for="(day, index) in daysInMonth" :key="index">
                    <div
                      class="dp-bg-range"
                      v-if="isInRange(day) || (isSelectedStart(day.date) && tempEnd) || isSelectedEnd(day.date)"
                      :class="{ 'range-start': isSelectedStart(day.date) && tempEnd, 'range-end': isSelectedEnd(day.date), 'range-mid': isInRange(day) }"
                    ></div>
                    <div class="dp-day-num" :class="{ 'current-month': day.isCurrent, selected: isSelectedStart(day.date) || isSelectedEnd(day.date), disabled: !day.isCurrent || isPastDate(day.date) }" @click="selectDate(day)">
                      {{ day.day }}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="cm-footer">
          <button class="cm-btn-cancel sa-btn-cancel" @click="showCreateModal = false"><i class="bi bi-x-lg"></i> Cancel</button>
          <button class="cm-btn-create" @click="createNewCycle"><i class="bi bi-plus-lg"></i> Create cycle</button>
        </div>
      </div>
    </div>
    </Teleport>
  </ProjectPageContainer>
</template>

<style scoped>
.plane-cycles-wrapper {
  display: flex;
  flex-direction: column;
  height: 100%;
  color: var(--color-text-primary);
  font-family: inherit;
  background: var(--color-bg);
  min-height: calc(100vh - 100px);
}

.nexus-project-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  max-width: 1600px;
  width: calc(100% - 48px);
  margin: 20px auto 0;
  padding: 16px 18px;
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 16px;
  background: var(--color-surface);
  box-shadow: var(--sp-shadow-xs);
}

.view-name {
  font-weight: 900;
  font-size: 15px;
  color: var(--color-text-primary);
}

.text-muted { color: var(--color-text-muted); }
.text-orange { color: #F59E0B; }
.text-green { color: #10B981; }
.text-blue { color: #3B82F6; }
.bg-green { background-color: #10B981; }
.bg-orange { background-color: #F59E0B; }
.bg-darkgray { background-color: #3F3F46; }
.bg-lightgray { background-color: var(--color-text-muted); }

.cycles-view-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  max-width: 1600px;
  width: calc(100% - 48px);
  margin: 24px auto 0;
  padding: 18px 20px;
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 16px;
  background: var(--color-surface);
  box-shadow: var(--sp-shadow-xs);
}

.vh-right { display: flex; align-items: center; gap: 12px; }
.icon-action { background: transparent; border: none; color: var(--color-text-muted); cursor: pointer; font-size: 14px; border-radius: 6px; padding: 6px 8px; }
.icon-action:hover { color: var(--color-text-primary); }
.icon-action.active { color: var(--color-text-primary); background: var(--color-border); }
.filter-action { background: var(--color-surface); border: 1px solid var(--color-border); color: var(--color-text-primary); padding: 8px 12px; border-radius: 10px; font-size: 13px; cursor: pointer; display: flex; align-items: center; gap: 6px; font-weight: 800; transition: border-color .18s ease, background .18s ease, color .18s ease, transform .18s ease; }
.filter-action:hover { background: var(--color-surface-hover); border-color: color-mix(in srgb, var(--sp-sky-400) 55%, var(--color-border)); color: var(--color-accent); transform: translateY(-1px); }
.filter-action.active { background: color-mix(in srgb, var(--sp-blue-700) 12%, var(--color-surface)); border-color: var(--sp-blue-700); color: var(--color-accent); }
.cycle-search-wrapper {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 260px;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 7px 12px;
  background: var(--color-surface);
}
.cycle-search-wrapper i { color: var(--color-text-muted); font-size: 12px; }
.cycle-search-wrapper input {
  flex: 1;
  min-width: 0;
  background: transparent;
  border: 0;
  color: var(--color-text-primary);
  outline: none;
  font-size: 13px;
}
.cycle-filter-wrapper { position: relative; }
.cycle-filter-menu {
  position: absolute;
  top: calc(100% + 8px);
  right: 0;
  width: 220px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 14px;
  padding: 12px;
  z-index: 20;
  box-shadow: var(--sp-shadow-sm);
}
.filter-title { color: var(--color-text-muted); font-size: 12px; font-weight: 600; margin-bottom: 8px; }
.filter-option {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--color-text-primary);
  font-size: 13px;
  padding: 6px 0;
  cursor: pointer;
}
.clear-filter-btn {
  width: 100%;
  margin-top: 8px;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-surface);
  color: var(--color-text-primary);
  padding: 7px;
  cursor: pointer;
}
.clear-filter-btn:hover { background: var(--color-surface-hover); border-color: var(--sp-sky-400); color: var(--color-accent); }
.primary-action { background: var(--sp-blue-700); color: white; border: 1px solid var(--sp-blue-700); border-radius: 10px; padding: 8px 16px; font-size: 13px; cursor: pointer; font-weight: 800; box-shadow: 0 8px 18px color-mix(in srgb, var(--sp-blue-700) 20%, transparent); }
.primary-action:hover { background: var(--sp-blue-600); border-color: var(--sp-sky-400); }

.cycles-body { width: 100%; max-width: 1600px; margin: 0 auto; padding: 16px 24px 28px; flex: 1; overflow: visible; }
.cycle-section { margin-bottom: 24px; }
.cs-header { display: flex; align-items: center; gap: 12px; padding: 10px 0; cursor: pointer; user-select: none; }
.chevron { font-size: 12px; color: var(--color-text-muted); width: 16px; text-align: center; }
.cs-title { font-size: 14px; font-weight: 900; color: var(--color-text-primary); }
.cs-count { font-size: 12px; color: var(--color-text-primary); background: color-mix(in srgb, var(--color-accent) 14%, var(--color-surface-hover)); padding: 2px 8px; border-radius: 999px; font-weight: 900; }
.cs-content { padding-left: 28px; margin-top: 10px; display: flex; flex-direction: column; gap: 12px; }

.cycle-card {
  background: var(--color-surface);
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 14px;
  overflow: hidden;
  box-shadow: var(--sp-shadow-xs);
  transition: border-color 0.2s, background 0.2s, box-shadow 0.2s;
}

.cycle-card.hover-card:hover { border-color: color-mix(in srgb, var(--sp-sky-400) 44%, var(--color-border)); background: var(--color-surface-hover); box-shadow: var(--sp-shadow-sm); }
.cycle-card.collapsed { display: flex; justify-content: space-between; align-items: center; gap: 14px; padding: 14px 16px; }
.cc-top { display: flex; justify-content: space-between; align-items: center; gap: 16px; padding: 16px 18px; border-bottom: 1px solid var(--color-border); }
.cct-left, .cct-right { display: flex; align-items: center; gap: 12px; min-width: 0; }
.cct-right { flex-wrap: wrap; justify-content: flex-end; }
.progress-ring { width: 38px; height: 38px; border-radius: 50%; border: 3px solid currentColor; display: flex; align-items: center; justify-content: center; font-size: 10px; font-weight: 900; flex: 0 0 auto; background: color-mix(in srgb, currentColor 10%, transparent); }
.cycle-name { font-size: 15px; font-weight: 900; color: var(--color-text-primary); min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.detail-link { font-size: 12px; color: var(--color-accent); display: flex; align-items: center; gap: 6px; font-weight: 900; white-space: nowrap; }
.date-range { font-size: 12px; color: var(--color-text-secondary); display: flex; align-items: center; gap: 6px; background: var(--color-surface-hover); padding: 5px 10px; border-radius: 999px; border: 1px solid var(--color-border); font-weight: 800; white-space: nowrap; }
.icon-btn { background: transparent; border: none; color: var(--color-text-muted); cursor: pointer; font-size: 14px; transition: color 0.2s; }
.icon-btn:hover { color: var(--color-text-primary); }
.completed-badge { font-size: 12px; color: var(--color-success, #10B981); font-weight: 800; }
.task-count-badge { font-size: 12px; color: var(--color-text-muted); display: flex; align-items: center; gap: 6px; }
.carry-over-panel {
  margin-top: 12px;
  border: 1px solid var(--border-color);
  border-radius: 10px;
  background: var(--bg-tertiary);
  padding: 16px;
}
.carry-over-toolbar {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
  margin-bottom: 12px;
}
.carry-over-filters,
.carry-over-actions {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.carry-over-search,
.carry-over-select {
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  color: var(--text-primary);
  border-radius: 8px;
  padding: 8px 10px;
  font-size: 13px;
}
.carry-over-search {
  min-width: 220px;
}
.carry-over-table {
  display: grid;
  gap: 8px;
}
.carry-over-head,
.carry-over-row {
  display: grid;
  grid-template-columns: 34px minmax(0, 1fr) minmax(280px, 420px);
  gap: 12px;
  align-items: center;
}
.carry-over-head {
  color: #A1A1AA;
  font-size: 12px;
  padding: 4px 0;
}
.carry-over-row {
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 10px 12px;
  background: var(--color-surface);
}
.carry-over-check {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
}
.carry-over-main {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 10px;
}
.carry-over-meta {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  flex-wrap: wrap;
  color: var(--color-text-muted);
  font-size: 11px;
}
.carry-over-meta span {
  background: var(--color-surface-hover);
  border-radius: 999px;
  padding: 4px 8px;
}

.cc-grid { display: grid; grid-template-columns: minmax(220px, 0.9fr) minmax(320px, 1.65fr) minmax(280px, 1.1fr); }
.grid-panel { padding: 18px; border-right: 1px solid var(--color-border); min-width: 0; }
.grid-panel:last-child { border-right: none; }
.gp-header { display: flex; justify-content: space-between; gap: 10px; font-size: 13px; font-weight: 900; margin-bottom: 18px; color: var(--color-text-primary); }
.gp-header .sub { color: var(--color-text-muted); font-weight: 800; }

.progress-bar-container { display: flex; height: 10px; border-radius: 999px; overflow: hidden; background: var(--color-border); margin-bottom: 18px; }
.pb-segment { height: 100%; }
.legend-list { display: flex; flex-direction: column; gap: 12px; }
.legend-item { display: flex; align-items: center; font-size: 12px; color: var(--color-text-secondary); font-weight: 800; }
.legend-item .dot { width: 8px; height: 8px; border-radius: 50%; margin-right: 10px; }
.legend-item .val { margin-left: auto; color: var(--color-text-primary); }

.tabs-header { display: flex; border-bottom: 1px solid var(--color-border); margin-bottom: 14px; gap: 8px; overflow-x: auto; }
.tab-h { padding: 0 8px 8px; font-size: 12px; color: var(--color-text-muted); border: none; border-bottom: 2px solid transparent; background: transparent; cursor: pointer; font-weight: 800; white-space: nowrap; }
.tab-h.active { color: var(--color-accent); border-bottom-color: var(--sp-sky-400); font-weight: 900; }
.tab-row { display: flex; justify-content: space-between; gap: 10px; font-size: 12px; padding: 9px 10px; border-radius: 10px; background: color-mix(in srgb, var(--color-surface-hover) 62%, transparent); }
.tr-user { display: flex; align-items: center; gap: 8px; color: var(--color-text-primary); }
.avatar-icon { background: color-mix(in srgb, var(--color-accent) 12%, var(--color-border)); border-radius: 50%; width: 24px; height: 24px; display: flex; align-items: center; justify-content: center; color: var(--color-accent); }
.work-items-body { display: flex; flex-direction: column; gap: 8px; max-height: 150px; overflow-y: auto; padding-right: 4px; }
.tab-empty { font-size: 12px; padding: 8px 12px; }
.cycle-work-item { display: flex; justify-content: space-between; gap: 12px; padding: 9px 10px; border: 1px solid var(--color-border); border-radius: 10px; background: var(--color-surface); }
.work-item-main { min-width: 0; display: flex; align-items: center; gap: 8px; }
.work-item-id { color: var(--color-text-muted); font-size: 11px; flex-shrink: 0; }
.work-item-title { color: var(--color-text-primary); font-size: 12px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.work-item-meta { display: flex; align-items: center; gap: 6px; flex-shrink: 0; color: var(--color-text-muted); font-size: 11px; }
.work-item-meta span { background: var(--color-surface-hover); border-radius: 999px; padding: 3px 7px; max-width: 96px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.create-cycle-modal {
  width: 580px;
  max-width: calc(100vw - 32px);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 16px;
  box-shadow: var(--shadow-xl);
  transition: width 0.24s ease;
}

.create-cycle-modal.with-horizontal-calendar {
  width: 880px;
}

[data-theme='dark'] .plane-cycles-wrapper {
  background:
    linear-gradient(180deg, #07111f, #0f172a 52%, #101827);
}

[data-theme='dark'] .cycles-view-header,
[data-theme='dark'] .nexus-project-header,
[data-theme='dark'] .cycle-card {
  border-color: rgba(148, 163, 184, 0.18);
  background: rgba(15, 23, 42, 0.78);
  box-shadow: 0 18px 44px rgba(0, 0, 0, 0.24);
}

@media (max-width: 1180px) {
  .cc-grid {
    grid-template-columns: 1fr;
  }

  .grid-panel {
    border-right: none;
    border-bottom: 1px solid var(--color-border);
  }

  .grid-panel:last-child {
    border-bottom: none;
  }
}

@media (max-width: 780px) {
  .nexus-project-header,
  .cycle-card.collapsed,
  .cc-top {
    align-items: stretch;
    flex-direction: column;
  }

  .nexus-controls-row,
  .cct-right {
    justify-content: flex-start !important;
  }

  .cycles-body {
    padding: 18px 16px 30px;
  }

  .cs-content {
    padding-left: 0;
  }
}

[data-theme='dark'] .filter-action {
  background: rgba(15, 23, 42, 0.82);
  border-color: rgba(148, 163, 184, 0.22);
  color: #e2e8f0;
}

[data-theme='dark'] .cycle-card.hover-card:hover {
  background: rgba(30, 41, 59, 0.90);
}

.cm-header { padding: 24px 24px 16px; }
.cm-badge { display: inline-flex; align-items: center; gap: 8px; border: 1px solid var(--color-border); background: var(--color-bg-secondary); padding: 4px 10px; border-radius: 6px; font-size: 12px; color: var(--color-text-primary); font-weight: 500; margin-bottom: 16px; }
.cm-title { font-size: 20px; font-weight: 600; color: var(--color-text-primary); margin: 0; }
.cm-body { padding: 0 24px 24px; display: flex; flex-direction: column; }
.cm-layout-grid { display: grid; grid-template-columns: 1fr; gap: 20px; }
.cm-layout-grid.has-side-calendar { grid-template-columns: minmax(0, 1fr) minmax(290px, 310px); align-items: start; }
.cm-form-col { display: flex; flex-direction: column; }
.cm-calendar-col { height: 100%; }
.cm-input, .cm-textarea { width: 100%; background: var(--color-bg-secondary); border: 1px solid var(--color-border); border-radius: 8px; padding: 12px 16px; color: var(--color-text-primary); outline: none; transition: border-color 0.2s; }
.cm-input:focus, .cm-textarea:focus { border-color: var(--color-accent); }
.cm-input { font-size: 15px; }
.cm-textarea { font-size: 14px; resize: none; }
.cm-field { display: flex; flex-direction: column; gap: 7px; margin-bottom: 16px; }
.cm-field > label { color: var(--color-text-secondary); font-size: 13px; font-weight: 650; }
.cm-footer { padding: 16px 24px; border-top: 1px solid var(--color-border); display: flex; justify-content: flex-end; gap: 12px; background: var(--color-surface-hover); border-bottom-left-radius: 12px; border-bottom-right-radius: 12px; }
.cm-btn-cancel { background: transparent; border: 1px solid var(--color-border); border-radius: 8px; padding: 8px 16px; color: var(--color-text-primary); font-size: 13px; font-weight: 500; cursor: pointer; transition: background 0.2s; }
.cm-btn-cancel:hover { background: var(--color-surface-hover); }
.cm-btn-create { background: var(--color-accent); border: none; border-radius: 8px; padding: 8px 16px; color: white; font-size: 13px; font-weight: 500; cursor: pointer; transition: opacity 0.2s; }
.cm-btn-create:hover { opacity: 0.9; }

.dp-wrapper { position: relative; }
.dp-btn { width: 100%; background: var(--color-bg-secondary); border: 1px solid var(--color-border); color: var(--color-text-primary); padding: 10px 14px; border-radius: 10px; font-size: 13px; cursor: pointer; display: flex; align-items: center; gap: 8px; transition: border-color 0.2s; font-weight: 600; }
.dp-btn:hover { border-color: var(--color-accent); }
.toggle-icon { transition: transform 0.2s ease; color: var(--color-text-muted); }
.rotate-90 { transform: rotate(90deg); }
.dp-popover, .dp-popover-inline { background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 14px; width: 100%; padding: 16px; box-shadow: var(--shadow-sm); }
.dp-popover { position: absolute; top: 100%; left: 0; margin-top: 8px; z-index: 1001; }
.dp-popover-inline { position: relative; }
.dp-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
.dp-month-year { display: flex; gap: 12px; font-size: 14px; font-weight: 600; color: var(--color-text-primary); }
.dp-nav { display: flex; gap: 8px; }
.dp-nav button { background: transparent; border: none; color: var(--color-text-muted); cursor: pointer; transition: color 0.2s; }
.dp-nav button:hover { color: var(--color-text-primary); }
.dp-grid { display: grid; grid-template-columns: repeat(7, 1fr); row-gap: 6px; }
.headday { font-size: 10px; font-weight: 600; color: var(--color-text-muted); text-align: center; }
.dp-day-wrapper { position: relative; display: flex; align-items: center; justify-content: center; height: 32px; }
.dp-bg-range { position: absolute; inset: 0; background: color-mix(in srgb, var(--color-accent) 20%, transparent); z-index: 1; }
.range-start { border-top-left-radius: 16px; border-bottom-left-radius: 16px; }
.range-end { border-top-right-radius: 16px; border-bottom-right-radius: 16px; }
.dp-day-num { position: relative; z-index: 2; width: 32px; height: 32px; display: flex; align-items: center; justify-content: center; border-radius: 50%; font-size: 12px; color: var(--color-text-muted); cursor: pointer; transition: all 0.2s; }
.dp-day-num.current-month { color: var(--color-text-primary); }
.dp-day-num:hover:not(.headday):not(.disabled) { background: var(--color-surface-hover); color: var(--color-text-primary); }
.dp-day-num.selected { background: var(--color-accent) !important; color: white !important; }
.dp-day-num.disabled { color: var(--color-text-disabled); cursor: not-allowed; opacity: 0.5; }

/* Compact density */
.cycles-tab {
  min-height: calc(100vh - var(--sa-topbar-height, 52px)) !important;
}

.nexus-project-header {
  padding: 12px 16px !important;
  border-radius: 10px !important;
}

.cycle-page-title {
  font-size: 16px !important;
}

.icon-action,
.filter-action,
.primary-action,
.cycle-search-wrapper,
.cycle-filter-chip {
  min-height: 32px !important;
  border-radius: 8px !important;
  padding: 6px 10px !important;
  font-size: 12.5px !important;
}

.cycles-body {
  max-width: 1160px !important;
  padding: 16px var(--sa-page-x, 24px) 26px !important;
}

.cs-header {
  padding: 8px 0 !important;
}

.cs-content {
  gap: 10px !important;
  padding-left: 22px !important;
}

.cycle-card {
  border-radius: 10px !important;
}

.cycle-card.collapsed {
  padding: 12px 14px !important;
}

.cc-top {
  padding: 12px 14px !important;
  gap: 12px !important;
}

.progress-ring {
  width: 34px !important;
  height: 34px !important;
}

.cycle-name {
  font-size: 14px !important;
}

.date-range {
  padding: 4px 8px !important;
  font-size: 11.5px !important;
}

.cc-grid {
  grid-template-columns: 0.8fr 1.55fr 1fr !important;
}

.grid-panel {
  padding: 14px !important;
}

.gp-header {
  margin-bottom: 12px !important;
}

.progress-bar-container {
  height: 8px !important;
  margin-bottom: 12px !important;
}

.cm-header {
  padding: 16px 18px 12px !important;
}

.cm-title {
  font-size: 17px !important;
}

.cm-body {
  padding: 0 18px 18px !important;
}

/* Create-cycle dialog: focused SaaS form treatment */
.sa-data-modal-overlay {
  padding: 24px;
  background: rgba(15, 23, 42, .48);
  backdrop-filter: blur(8px);
}

.create-cycle-modal {
  overflow: hidden;
  border: 1px solid color-mix(in srgb, var(--color-accent) 22%, var(--color-border));
  border-radius: 22px;
  box-shadow: 0 28px 80px rgba(15, 23, 42, .28), 0 0 0 1px rgba(255,255,255,.22) inset;
  animation: create-cycle-dialog-in .22s ease-out both;
}

.create-cycle-modal.with-horizontal-calendar { width: min(900px, 100%); }

.create-cycle-modal .cm-header {
  padding: 22px 24px 18px !important;
  background: linear-gradient(135deg, color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)), var(--color-surface));
  border-bottom: 1px solid color-mix(in srgb, var(--color-border) 75%, transparent);
}

.create-cycle-modal .cm-body { padding: 22px 24px 24px !important; }
.create-cycle-modal .cm-layout-grid { gap: 24px; }
.create-cycle-modal .cm-field { gap: 8px; margin-bottom: 18px; }
.create-cycle-modal .cm-field > label { font-size: 12px; letter-spacing: .02em; text-transform: uppercase; color: var(--color-text-muted); }
.create-cycle-modal .cm-input,
.create-cycle-modal .cm-textarea {
  border-radius: 12px;
  border-color: color-mix(in srgb, var(--color-border) 82%, var(--color-accent));
  background: color-mix(in srgb, var(--color-bg-secondary) 72%, var(--color-surface));
  box-shadow: 0 1px 2px rgba(15, 23, 42, .04) inset;
}
.create-cycle-modal .cm-input { min-height: 46px; }
.create-cycle-modal .cm-textarea { min-height: 112px; line-height: 1.5; }
.create-cycle-modal .cm-input:focus,
.create-cycle-modal .cm-textarea:focus,
.create-cycle-modal .dp-btn:focus-visible { border-color: var(--color-accent); box-shadow: 0 0 0 4px color-mix(in srgb, var(--color-accent) 16%, transparent); }
.create-cycle-modal .dp-btn { min-height: 48px; border-radius: 12px; background: color-mix(in srgb, var(--color-accent) 6%, var(--color-surface)); }
.create-cycle-modal .cm-footer { padding: 16px 24px 20px; background: color-mix(in srgb, var(--color-bg-secondary) 62%, var(--color-surface)); }
.create-cycle-modal .cm-btn-cancel,
.create-cycle-modal .cm-btn-create { min-height: 42px; border-radius: 11px; padding: 10px 18px; font-size: 13px; font-weight: 700; }
.create-cycle-modal .cm-btn-create { box-shadow: 0 8px 18px color-mix(in srgb, var(--color-accent) 28%, transparent); }

@keyframes create-cycle-dialog-in {
  from { opacity: 0; transform: translateY(8px) scale(.985); }
  to { opacity: 1; transform: translateY(0) scale(1); }
}

@media (max-width: 780px) {
  .cycles-body {
    padding: 12px !important;
  }

  .cs-content {
    padding-left: 0 !important;
  }

  .cc-grid {
    grid-template-columns: 1fr !important;
  }

  .sa-data-modal-overlay { padding: 12px; align-items: flex-end; }
  .create-cycle-modal,
  .create-cycle-modal.with-horizontal-calendar { width: 100%; max-width: none; border-radius: 20px; }
  .create-cycle-modal .cm-header { padding: 18px 18px 14px !important; }
  .create-cycle-modal .cm-body { padding: 18px !important; max-height: 72vh; overflow-y: auto; }
  .create-cycle-modal .cm-footer { padding: 14px 18px 18px; }
  .create-cycle-modal .cm-btn-cancel,
  .create-cycle-modal .cm-btn-create { flex: 1; }
}

/* Polished cycles experience */
.plane-cycles-wrapper {
  background: var(--color-bg) !important;
}

.cycles-view-header,
.nexus-project-header {
  border-radius: 10px !important;
  background: color-mix(in srgb, var(--color-surface) 90%, transparent) !important;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.06) !important;
}

.cycle-card {
  border-radius: 10px !important;
  background: color-mix(in srgb, var(--color-surface) 90%, transparent) !important;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.06) !important;
}

.cycle-card.expanded {
  border-left: 3px solid #f59e0b;
}

.cycle-card.collapsed {
  border-left: 3px solid color-mix(in srgb, var(--color-accent) 72%, #f59e0b);
}

.cycle-card.expanded {
  position: relative;
}

.cycle-card.expanded::before {
  content: "";
  position: absolute;
  inset: 0 0 auto 0;
  height: 3px;
  background: linear-gradient(90deg, #f59e0b, #38bdf8, #22c55e);
}

.cc-top {
  background:
    linear-gradient(90deg, color-mix(in srgb, #f59e0b 12%, transparent), transparent 64%),
    color-mix(in srgb, var(--color-surface-hover) 48%, transparent);
}

.grid-panel {
  background: color-mix(in srgb, var(--color-surface) 74%, transparent);
}

.grid-panel + .grid-panel {
  border-left-color: color-mix(in srgb, var(--color-border) 80%, transparent) !important;
}

.gp-header,
.cycle-name,
.tab-row,
.cycle-work-item {
  color: var(--color-text-primary) !important;
}

.legend-item,
.detail-link,
.date-range,
.work-item-id,
.work-item-meta {
  color: var(--color-text-secondary) !important;
}

.date-range,
.tab-row,
.cycle-work-item,
.carry-over-row {
  border-radius: 8px !important;
  background: color-mix(in srgb, var(--color-surface-hover) 58%, transparent) !important;
}

.burndown-chart-panel {
  min-height: 150px;
  border-radius: 8px;
  border: 1px solid color-mix(in srgb, var(--color-border) 72%, transparent);
  background: color-mix(in srgb, var(--color-bg) 38%, transparent);
}

.cs-title {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.cs-count {
  min-width: 22px;
  text-align: center;
}

.empty-state.text-muted {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 54px !important;
  height: 54px !important;
  background: transparent !important;
  border: none !important;
  color: var(--color-text-secondary) !important;
  font-weight: 550;
  box-shadow: none !important;
  box-sizing: border-box;
  margin: 0 !important;
  padding: 0 !important;
}

.progress-ring {
  box-shadow:
    inset 0 0 0 4px color-mix(in srgb, currentColor 12%, transparent),
    0 8px 18px color-mix(in srgb, currentColor 18%, transparent);
}

.legend-item .dot {
  box-shadow: 0 0 0 4px color-mix(in srgb, currentColor 12%, transparent);
}

.tab-row,
.cycle-work-item {
  border: 1px solid color-mix(in srgb, var(--color-border) 78%, transparent) !important;
}

.tab-row:hover,
.cycle-work-item:hover {
  border-color: color-mix(in srgb, var(--color-accent) 32%, var(--color-border)) !important;
  background: color-mix(in srgb, var(--color-accent) 8%, var(--color-surface)) !important;
}

.cycle-card,
.cycles-view-header,
.nexus-project-header,
.tab-row,
.cycle-work-item,
.date-range,
.empty-state.text-muted {
  transition:
    background 220ms ease,
    color 220ms ease,
    border-color 220ms ease,
    box-shadow 220ms ease,
    transform 220ms cubic-bezier(0.2, 0.8, 0.2, 1) !important;
}

[data-theme='dark'] .plane-cycles-wrapper,
[data-theme='dark'] .cycles-tab {
  color: #e5edf7 !important;
}

[data-theme='dark'] .cycles-view-header,
[data-theme='dark'] .nexus-project-header,
[data-theme='dark'] .cycle-card {
  background:
    linear-gradient(135deg, rgba(30, 41, 59, 0.90), rgba(15, 23, 42, 0.86)),
    #111827 !important;
  border-color: rgba(125, 211, 252, 0.20) !important;
}

[data-theme='dark'] .cc-top {
  background:
    linear-gradient(90deg, rgba(245, 158, 11, 0.18), rgba(56, 189, 248, 0.06) 56%, transparent),
    rgba(30, 41, 59, 0.62) !important;
}

[data-theme='dark'] .cycle-page-title,
[data-theme='dark'] .cs-title,
[data-theme='dark'] .cycle-name,
[data-theme='dark'] .gp-header,
[data-theme='dark'] .gp-header .title,
[data-theme='dark'] .legend-item .val,
[data-theme='dark'] .tab-row,
[data-theme='dark'] .cycle-work-item,
[data-theme='dark'] .cycle-work-item .work-item-title,
[data-theme='dark'] .cycle-status-row,
[data-theme='dark'] .cycle-status-row strong,
[data-theme='dark'] .progress-ring {
  color: #f8fafc !important;
}

[data-theme='dark'] .gp-header .sub,
[data-theme='dark'] .legend-item,
[data-theme='dark'] .detail-link,
[data-theme='dark'] .date-range,
[data-theme='dark'] .work-item-id,
[data-theme='dark'] .work-item-meta,
[data-theme='dark'] .empty-state.text-muted,
[data-theme='dark'] .tab-label {
  color: #cbd5e1 !important;
}

[data-theme='dark'] .date-range,
[data-theme='dark'] .tab-row,
[data-theme='dark'] .cycle-work-item {
  background: rgba(30, 41, 59, 0.72) !important;
  border-color: rgba(148, 163, 184, 0.18) !important;
}

[data-theme='dark'] .burndown-chart-panel {
  background:
    linear-gradient(rgba(203, 213, 225, 0.08) 1px, transparent 1px),
    linear-gradient(90deg, rgba(203, 213, 225, 0.08) 1px, transparent 1px),
    rgba(15, 23, 42, 0.64) !important;
  border-color: rgba(148, 163, 184, 0.18) !important;
}

[data-theme='light'] .plane-cycles-wrapper,
[data-theme='light'] .cycles-tab {
  color: #0f172a !important;
}

[data-theme='light'] .cycle-page-title,
[data-theme='light'] .cs-title,
[data-theme='light'] .cycle-name,
[data-theme='light'] .gp-header,
[data-theme='light'] .legend-item .val,
[data-theme='light'] .tab-row,
[data-theme='light'] .cycle-work-item,
[data-theme='light'] .cycle-work-item .work-item-title {
  color: #0f172a !important;
}

[data-theme='light'] .legend-item,
[data-theme='light'] .detail-link,
[data-theme='light'] .date-range,
[data-theme='light'] .work-item-id,
[data-theme='light'] .work-item-meta,
[data-theme='light'] .empty-state.text-muted {
  color: #475569 !important;
}

[data-theme='light'] .cycle-card,
[data-theme='light'] .nexus-project-header,
[data-theme='light'] .grid-panel,
[data-theme='light'] .create-cycle-modal,
[data-theme='light'] .cycle-filter-menu {
  background:
    linear-gradient(135deg, rgba(255, 255, 255, 0.97), rgba(248, 250, 252, 0.88)),
    #ffffff !important;
  border-color: rgba(148, 163, 184, 0.24) !important;
}

[data-theme='light'] .cc-top {
  background:
    linear-gradient(90deg, rgba(245, 158, 11, 0.13), rgba(56, 189, 248, 0.07) 58%, transparent),
    #f8fafc !important;
}

[data-theme='light'] .progress-ring {
  color: #b45309 !important;
  background: rgba(245, 158, 11, 0.10) !important;
  text-shadow: none !important;
}

[data-theme='light'] .panel-progress .legend-item,
[data-theme='light'] .gp-header .sub,
[data-theme='light'] .tr-stat,
[data-theme='light'] .tab-empty,
[data-theme='light'] .filter-title,
[data-theme='light'] .filter-option,
[data-theme='light'] .clear-filter-btn {
  color: #334155 !important;
}

[data-theme='light'] .legend-item .val,
[data-theme='light'] .tab-h.active,
[data-theme='light'] .tr-user,
[data-theme='light'] .work-item-title,
[data-theme='light'] .carry-over-main,
[data-theme='light'] .carry-over-check {
  color: #0f172a !important;
}

[data-theme='light'] .burndown-chart-panel {
  background:
    linear-gradient(rgba(71, 85, 105, 0.10) 1px, transparent 1px),
    linear-gradient(90deg, rgba(71, 85, 105, 0.09) 1px, transparent 1px),
    #f8fafc !important;
  border-color: rgba(148, 163, 184, 0.28) !important;
}

[data-theme='dark'] .progress-ring {
  text-shadow: 0 1px 10px rgba(0, 0, 0, 0.24);
}

.cycles-body {
  min-height: 0 !important;
  overflow: visible !important;
  max-width: 100% !important;
  padding: 8px 4px 18px !important;
}

.cycle-section {
  margin-bottom: 10px !important;
}

.cycle-card {
  border-radius: 12px !important;
  box-shadow: 0 8px 20px rgba(15, 23, 42, 0.06) !important;
}

.cycle-card.expanded {
  max-height: none !important;
}

.cc-grid {
  grid-template-columns: minmax(180px, 0.75fr) minmax(300px, 1.5fr) minmax(240px, 0.95fr) !important;
}

.grid-panel {
  padding: 10px 12px !important;
}

.gp-header {
  margin-bottom: 10px !important;
}

.burndown-chart-panel {
  min-height: 112px !important;
  height: 112px !important;
}

.legend-list {
  gap: 8px !important;
}

.progress-bar-container {
  height: 8px !important;
  margin-bottom: 10px !important;
}

.tabs-header {
  margin-bottom: 8px !important;
}

.cycle-card.collapsed {
  min-height: 54px !important;
  padding: 10px 12px !important;
}

.cc-top {
  padding: 12px 14px !important;
  gap: 10px !important;
}

.progress-ring {
  width: 34px !important;
  height: 34px !important;
  font-size: 11px !important;
}

.cycle-name {
  font-size: 14px !important;
  line-height: 1.25 !important;
}

.tabs-body {
  padding: 12px 14px !important;
}

.work-items-body {
  max-height: 210px !important;
}

.cycle-work-item {
  padding: 7px 9px !important;
  border-radius: 8px !important;
}

[data-theme='dark'] .cycle-card {
  box-shadow: 0 12px 30px rgba(0, 0, 0, 0.22) !important;
}

.cycle-load-state {
  min-height: 120px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  color: var(--color-text-secondary);
}

.cycle-load-error {
  color: var(--color-danger, #dc2626);
  flex-wrap: wrap;
}

.plane-danger-btn {
  min-height: 36px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 8px 12px;
  border: 1px solid color-mix(in srgb, #dc2626 55%, transparent);
  border-radius: 6px;
  background: color-mix(in srgb, #dc2626 10%, var(--color-surface));
  color: #dc2626;
  font-weight: 700;
}

.plane-danger-btn:disabled,
.plane-primary-btn:disabled,
.cm-btn-cancel:disabled {
  cursor: not-allowed;
  opacity: 0.62;
}

.transition-modal-overlay {
  padding: 16px;
  z-index: 1100;
}

.transition-modal {
  width: min(100%, 480px);
  max-height: calc(100vh - 32px);
  overflow-y: auto;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface);
  color: var(--color-text-primary);
  box-shadow: 0 24px 60px rgba(15, 23, 42, 0.24);
}

.transition-modal-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 20px 20px 14px;
  border-bottom: 1px solid var(--color-border);
}

.transition-modal-header h2 {
  margin: 0;
  font-size: 18px;
  line-height: 1.35;
}

.transition-modal-header p {
  margin: 3px 0 0;
  color: var(--color-text-secondary);
  overflow-wrap: anywhere;
}

.transition-modal-icon {
  width: 38px;
  height: 38px;
  flex: 0 0 38px;
  display: grid;
  place-items: center;
  border-radius: 50%;
  background: color-mix(in srgb, #dc2626 12%, transparent);
  color: #dc2626;
}

.transition-modal-body {
  padding: 18px 20px;
}

.transition-modal-body > p {
  margin: 0 0 14px;
}

.transition-warning {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 12px;
  border-left: 3px solid #d97706;
  border-radius: 4px;
  background: color-mix(in srgb, #d97706 10%, var(--color-surface));
  color: var(--color-text-secondary);
  line-height: 1.5;
}

.transition-modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  padding: 14px 20px 20px;
}

@media (max-width: 390px) {
  .transition-modal-overlay {
    align-items: flex-end;
    padding: 8px;
  }

  .transition-modal {
    max-height: calc(100vh - 16px);
  }

  .transition-modal-header,
  .transition-modal-body,
  .transition-modal-actions {
    padding-left: 14px;
    padding-right: 14px;
  }

  .transition-modal-actions {
    display: grid;
    grid-template-columns: 1fr 1fr;
  }

  .transition-modal-actions > button {
    width: 100%;
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

/* Premium primary action for the cycle page. */
.cycles-add-button {
  position: relative;
  isolation: isolate;
  overflow: visible;
  min-width: 150px;
  height: 44px !important;
  padding: 0 20px !important;
  border: 1px solid rgba(56, 189, 248, .58) !important;
  border-radius: 11px !important;
  background:
    linear-gradient(135deg, rgba(255, 255, 255, .12), transparent 32%),
    linear-gradient(115deg, #061638 0%, #0a2f78 34%, #075bd1 65%, #08a9e5 100%) !important;
  background-size: 100% 100%, 220% 220%;
  color: #fff !important;
  gap: 8px;
  font-size: 13px !important;
  font-weight: 850 !important;
  letter-spacing: .025em;
  text-shadow: 0 1px 8px rgba(2, 16, 48, .7);
  box-shadow:
    0 8px 18px rgba(2, 26, 82, .28),
    0 0 0 1px rgba(8, 169, 229, .12),
    inset 0 1px 0 rgba(255, 255, 255, .24),
    inset 0 -2px 0 rgba(1, 19, 59, .32) !important;
  transition: transform .24s cubic-bezier(.22, 1, .36, 1), box-shadow .24s ease, filter .24s ease;
}

.cycles-add-button::before {
  content: '';
  position: absolute;
  inset: -3px;
  width: auto;
  height: auto;
  border-radius: 13px;
  transform: none;
  padding: 1px;
  background: conic-gradient(from 0deg, transparent 0 58%, rgba(34, 211, 238, .35) 66%, #d7fbff 73%, #0ea5e9 80%, transparent 89% 100%);
  opacity: .72;
  animation: none;
  -webkit-mask: linear-gradient(#000 0 0) content-box, linear-gradient(#000 0 0);
  -webkit-mask-composite: xor;
  mask: linear-gradient(#000 0 0) content-box, linear-gradient(#000 0 0);
  mask-composite: exclude;
  transition: opacity .25s ease, filter .25s ease;
  pointer-events: none;
  z-index: 2;
}

.cycles-add-button::after {
  content: '';
  position: absolute;
  inset: 2px;
  border-radius: 9px;
  background: transparent;
  display: none;
  pointer-events: none;
  transition: opacity .25s ease, transform .25s ease;
  z-index: -1;
}

@keyframes cycle-border-orbit {
  to { transform: rotate(360deg); }
}

@keyframes cycle-button-energy {
  0%, 100% {
    background-position: 0 0, 0% 50%;
    box-shadow: 0 8px 18px rgba(2, 26, 82, .28), 0 0 0 1px rgba(8, 169, 229, .12), inset 0 1px 0 rgba(255, 255, 255, .24), inset 0 -2px 0 rgba(1, 19, 59, .32);
  }
  50% {
    background-position: 0 0, 100% 50%;
    box-shadow: 0 11px 24px rgba(2, 50, 142, .34), 0 0 0 3px rgba(34, 211, 238, .055), 0 0 18px rgba(14, 165, 233, .14), inset 0 1px 0 rgba(255, 255, 255, .28), inset 0 -2px 0 rgba(1, 19, 59, .32);
  }
}

.cycles-add-button:hover:not(:disabled) {
  transform: translateY(-2px);
  filter: brightness(1.04);
  box-shadow: 0 11px 22px rgba(2, 50, 142, .3), inset 0 1px 0 rgba(255, 255, 255, .28), inset 0 -2px 0 rgba(1, 19, 59, .28) !important;
}

.cycles-add-button:hover:not(:disabled)::after {
  opacity: 1;
  transform: scale(1.04);
}

.cycles-add-button:hover:not(:disabled)::before {
  opacity: .95;
  filter: drop-shadow(0 0 3px rgba(125, 231, 255, .62));
}

.cycles-add-button:hover:not(:disabled) i {
  transform: translateX(-2px) rotate(-12deg) scale(1.16);
  filter: drop-shadow(0 0 3px rgba(165, 243, 252, .45));
}

.cycles-add-button i {
  position: relative;
  z-index: 1;
  transition: transform .24s cubic-bezier(.22, 1, .36, 1);
}

/* Twin light beams sweep into the label, matching the Work items action. */
.cycle-button-label {
  position: relative;
  z-index: 1;
  display: inline-block;
  overflow: visible;
  padding: 2px 1px;
  text-shadow: 0 1px 3px rgba(2, 16, 48, .58);
}
.cycle-button-label::before,
.cycle-button-label::after {
  content: '';
  position: absolute;
  top: 50%;
  width: 46px;
  height: 22px;
  opacity: 0;
  pointer-events: none;
  filter: blur(3px);
  transition: opacity .2s ease, transform .65s cubic-bezier(.2,.8,.2,1);
}
.cycle-button-label::before,
.cycle-button-label::after { display: none !important; }
.cycle-button-label::before {
  left: -42px;
  transform: translateY(-50%) translateX(0);
  background: linear-gradient(90deg, transparent, rgba(190, 247, 255, .95));
}
.cycle-button-label::after {
  right: -42px;
  transform: translateY(-50%) translateX(0);
  background: linear-gradient(270deg, transparent, rgba(190, 247, 255, .95));
}
.cycles-add-button:hover:not(:disabled) .cycle-button-label {
  color: #ffffff;
  text-shadow: 0 1px 3px rgba(2, 16, 48, .62);
}
.cycles-add-button:hover:not(:disabled) .cycle-button-label::before {
  opacity: 0;
}
.cycles-add-button:hover:not(:disabled) .cycle-button-label::after {
  opacity: 0;
}
.cycles-add-button:active:not(:disabled) .cycle-button-label {
  text-shadow: 0 1px 3px rgba(2, 16, 48, .62);
}

.cycles-add-button:focus-visible {
  outline: 3px solid rgba(56, 189, 248, .35);
  outline-offset: 3px;
}

.cycles-add-button:active:not(:disabled) {
  transform: translateY(-1px) scale(.965);
  filter: brightness(.94) saturate(1.08);
  box-shadow: 0 7px 14px rgba(2, 30, 78, .3), 0 0 0 3px rgba(34, 211, 238, .17), inset 0 3px 8px rgba(1, 19, 59, .35) !important;
}

.cycles-add-button:disabled {
  opacity: .55;
  filter: grayscale(.2);
  box-shadow: none !important;
}

@media (prefers-reduced-motion: reduce) {
  .cycles-add-button,
  .cycles-add-button::before,
  .cycles-add-button::after,
  .cycles-add-button i {
    animation: none !important;
    transition: none !important;
  }
  .cycle-button-label::before,
  .cycle-button-label::after {
    transition: none !important;
  }
}
</style>
