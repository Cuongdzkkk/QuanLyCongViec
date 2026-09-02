<script setup>
import { ref, onMounted, onUnmounted, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import { ElNotification, ElMessageBox } from 'element-plus'
import ListView from '@/components/ListView.vue'
import { useI18nStore } from '@/store/useI18nStore'
import { signalRService } from '@/api/signalrService'

const i18nStore = useI18nStore()
const t = (key) => i18nStore.t(key)

import FilterBar from '@/components/FilterBar.vue'
import ProjectPageContainer from '@/components/common/ProjectPageContainer.vue'
import ProjectPageHeader from '@/components/common/ProjectPageHeader.vue'
import ProjectPageToolbar from '@/components/common/ProjectPageToolbar.vue'
import ToolbarSortMenu from '@/components/common/ToolbarSortMenu.vue'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import DataModalField from '@/components/common/Foundation/DataModalField.vue'

const route = useRoute()
const projectId = computed(() => route.params.id || localStorage.getItem('currentProjectId') || 'default')

const views = ref([])
const activeView = ref(null)
const tasks = ref([])
const originalTasks = ref([])
const projectMembers = ref([])
const loading = ref(false)
const showCreateModal = ref(false)
const modalTab = ref('list')
const viewType = ref('list') 
const viewListMode = ref('list')

// Selected Filters State
const activeFilters = ref([])
const showViewSearch = ref(false)
const showListFilters = ref(false)
const showActiveFilters = ref(false)
const listFavoritesOnly = ref(false)
const listGlobalOnly = ref(false)

const quickFilterPresets = {
    status: { field: 'status', label: 'Status', icon: 'fa-regular fa-circle-dot', operator: 'is', value: 'TO DO' },
    assignee: { field: 'assignee', label: 'Assignee', icon: 'fa-regular fa-user', operator: 'empty', value: '' },
    priority: { field: 'priority', label: 'Priority', icon: 'fa-solid fa-signal', operator: 'is', value: 'High' },
    label: { field: 'label', label: 'Label', icon: 'fa-solid fa-tag', operator: 'empty', value: '' },
    cycle: { field: 'cycle', label: 'Cycle', icon: 'fa-regular fa-circle-pause', operator: 'empty', value: '' },
    module: { field: 'module', label: 'Module', icon: 'fa-solid fa-table-cells-large', operator: 'empty', value: '' },
    startDate: { field: 'startDate', label: 'Start date', icon: 'fa-regular fa-calendar-plus', operator: 'empty', value: '' },
    dueDate: { field: 'dueDate', label: 'Target date', icon: 'fa-regular fa-calendar', operator: 'overdue', value: '' },
    createdAt: { field: 'createdAt', label: 'Created at', icon: 'fa-regular fa-calendar', operator: 'after', value: 'Today' },
    updatedAt: { field: 'updatedAt', label: 'Updated at', icon: 'fa-regular fa-calendar', operator: 'after', value: 'Today' }
}

const addFilterOption = (presetKey) => {
    const preset = quickFilterPresets[presetKey]
    if (!preset) return

    const filter = {
        id: `${preset.field}-${Date.now()}`,
        field: preset.field,
        operator: preset.operator,
        value: preset.value,
        label: preset.label,
        condition: preset.operator,
        displayValue: preset.value || preset.operator,
        icon: preset.icon
    }

    activeFilters.value.push(filter)
}

const handleRemoveFilter = (id) => {
    activeFilters.value = activeFilters.value.filter(f => f.id !== id)
}

const handleClearFilters = () => {
    activeFilters.value = []
}

const handleAddFilter = (filter) => {
    if (!filter?.id) return
    activeFilters.value.push(filter)
}

const parseViewMetadata = (rawMetadata) => {
    try {
        const parsed = typeof rawMetadata === 'string' ? JSON.parse(rawMetadata || '{}') : (rawMetadata || {})
        return {
            filters: Array.isArray(parsed.filters) ? parsed.filters.filter(item => item?.field) : [],
            displayProps: Array.isArray(parsed.displayProps) && parsed.displayProps.length ? parsed.displayProps : ['ID', 'Assignee', 'Start date', 'Due date', 'Labels', 'Priority', 'State'],
            groupBy: parsed.groupBy || 'States',
            orderBy: parsed.orderBy || 'Manual',
            showSubItems: Boolean(parsed.showSubItems),
            viewType: parsed.viewType || 'list'
        }
    } catch {
        return {
            filters: [],
            displayProps: ['ID', 'Assignee', 'Start date', 'Due date', 'Labels', 'Priority', 'State'],
            groupBy: 'States',
            orderBy: 'Manual',
            showSubItems: false,
            viewType: 'list'
        }
    }
}

const syncDesignerFromMetadata = (metadata) => {
    activeFilters.value = metadata.filters
    displayProps.value = metadata.displayProps
    groupBy.value = metadata.groupBy
    orderBy.value = metadata.orderBy
    showSubItems.value = metadata.showSubItems
    viewType.value = metadata.viewType
}

const buildQueryMetadata = () => JSON.stringify({
    filters: activeFilters.value,
    displayProps: displayProps.value,
    groupBy: groupBy.value,
    orderBy: orderBy.value,
    showSubItems: showSubItems.value,
    viewType: viewType.value
})

const getTaskFieldValue = (task, field) => {
    const fieldMap = {
        status: task.statusName || task.state || task.status,
        priority: task.priorityName || task.priority,
        assignee: task.assigneeName || task.assignedToName || task.assignees?.map(a => a.fullName || a.name).join(', '),
        creator: task.reporterName || task.createdByName || task.creatorName,
        label: task.labels?.map(l => l.name || l.labelName).join(', '),
        startDate: task.plannedStartDate || task.startDate,
        dueDate: task.dueDate || task.plannedEndDate,
        cycle: task.sprintName || task.cycleName,
        module: task.moduleName,
        createdAt: task.createdAt || task.createdDate,
        updatedAt: task.updatedAt || task.updatedDate
    }
    return fieldMap[field] ?? task[field]
}

const normalizeValue = (value) => `${value ?? ''}`.toLowerCase()
const normalizePriorityValue = (value) => {
    const map = {
        urgent: '1',
        high: '2',
        normal: '3',
        medium: '3',
        low: '4',
        none: ''
    }
    const text = normalizeValue(value).trim()
    return map[text] ?? text
}

const isSameDay = (left, right = new Date()) => {
    if (!(left instanceof Date) || Number.isNaN(left.getTime())) return false
    return left.getFullYear() === right.getFullYear()
        && left.getMonth() === right.getMonth()
        && left.getDate() === right.getDate()
}

const isThisWeek = (date) => {
    if (!(date instanceof Date) || Number.isNaN(date.getTime())) return false
    const now = new Date()
    const start = new Date(now)
    start.setDate(now.getDate() - now.getDay())
    start.setHours(0, 0, 0, 0)
    const end = new Date(start)
    end.setDate(start.getDate() + 7)
    return date >= start && date < end
}

const applyTaskFilters = (items, filters) => {
    if (!filters?.length) return items

    return items.filter(task => filters.every(filter => {
        const actual = getTaskFieldValue(task, filter.field)
        const expected = filter.value
        const operator = filter.operator || filter.condition || 'is'
        let actualText = normalizeValue(actual)
        const expectedText = normalizeValue(expected)
        const actualDate = actual ? new Date(actual).getTime() : null
        const expectedDate = expected ? new Date(expected).getTime() : null
        const actualDateValue = actual ? new Date(actual) : null

        if (filter.field === 'priority') {
            actualText = normalizePriorityValue(actual)
            const priorityValues = expectedText.split(',').map(v => normalizePriorityValue(v.trim()))
            if (operator === 'is not' || operator === 'is_not' || operator === 'not in' || operator === 'not_in') return !priorityValues.includes(actualText)
            return priorityValues.includes(actualText)
        }

        if (filter.field === 'assignee' && expectedText === 'unassigned') {
            const hasAssignee = Boolean(actualText)
            return operator === 'is not' ? hasAssignee : !hasAssignee
        }

        if (['startDate', 'dueDate', 'createdAt', 'updatedAt'].includes(filter.field)) {
            if (operator === 'empty') return !actual
            if (operator === 'overdue') return actualDate && actualDate < Date.now()
            if (expectedText === 'today') return isSameDay(actualDateValue)
            if (expectedText === 'this week') return isThisWeek(actualDateValue)
        }

        if (operator === 'empty') return !actual
        if (operator === 'not empty') return Boolean(actual)
        if (operator === 'overdue') return actualDate && actualDate < Date.now()
        if (operator === 'before') return actualDate && expectedDate && actualDate < expectedDate
        if (operator === 'after') return actualDate && expectedDate && actualDate > expectedDate
        if (operator === 'between') {
            const endDate = filter.valueTo ? new Date(filter.valueTo).getTime() : null
            return actualDate && expectedDate && endDate && actualDate >= expectedDate && actualDate <= endDate
        }
        if (operator === 'is not' || operator === 'is_not') return actualText !== expectedText
        if (operator === 'includes') return actualText.includes(expectedText)
        if (operator === 'not includes' || operator === 'not_includes') return !actualText.includes(expectedText)
        if (operator === 'in') return expectedText.split(',').map(v => v.trim()).includes(actualText)
        if (operator === 'not in' || operator === 'not_in') return !expectedText.split(',').map(v => v.trim()).includes(actualText)
        return actualText === expectedText || actualText.includes(expectedText)
    }))
}

// Sorting and Filtering
const sortBy = ref('Updated at')
const sortDir = ref('desc')
const viewSortOptions = [
  { value: 'Name', label: 'Name', icon: 'fa-solid fa-font' },
  { value: 'Created at', label: 'Created at', icon: 'fa-regular fa-calendar-plus' },
  { value: 'Updated at', label: 'Updated at', icon: 'fa-regular fa-clock' }
]
const filterSearch = ref('')

const activeListViewFilters = ref([])
const viewFilterFields = computed(() => [
  { key: 'favorites', label: 'Favorites', icon: 'fa-regular fa-star', values: ['Starred'] },
  { key: 'scope', label: 'Scope', icon: 'fa-solid fa-earth-americas', values: ['Global views'] }
])
const viewOperators = {
  favorites: ['is', 'is not'],
  scope: ['is', 'is not']
}
const customViewValueMeta = (fieldKey, value) => {
  if (fieldKey === 'favorites') return { icon: 'fa-solid fa-star', color: '#fbbf24' }
  if (fieldKey === 'scope') return { icon: 'fa-solid fa-earth-americas', color: '#3b82f6' }
  return null
}

const showViewsFilterDropdown = ref(false)
const showActiveViewFilterDropdown = ref(false)

const toggleViewsFilterDropdown = () => {
  showViewsFilterDropdown.value = !showViewsFilterDropdown.value
}
const toggleActiveViewFilterDropdown = () => {
  showActiveViewFilterDropdown.value = !showActiveViewFilterDropdown.value
}

const handleOutsideClick = (e) => {
  if (!e.target.closest('.js-toolbar-popup-scope-views')) {
    showViewsFilterDropdown.value = false
  }
  if (!e.target.closest('.js-toolbar-popup-scope-active-view')) {
    showActiveViewFilterDropdown.value = false
  }
}

const filterFields = computed(() => {
  const uniqStatuses = [...new Set(originalTasks.value.map(t => getTaskFieldValue(t, 'status')).filter(Boolean))]
  const uniqAssignees = [...new Set(originalTasks.value.map(t => getTaskFieldValue(t, 'assignee')).filter(Boolean))]
  const uniqPriorities = [...new Set(originalTasks.value.map(t => getTaskFieldValue(t, 'priority')).filter(Boolean))]
  const uniqLabels = [...new Set(originalTasks.value.flatMap(t => {
    const val = getTaskFieldValue(t, 'label')
    return val ? val.split(',').map(s => s.trim()) : []
  }).filter(Boolean))]
  const uniqCycles = [...new Set(originalTasks.value.map(t => getTaskFieldValue(t, 'cycle')).filter(Boolean))]
  const uniqModules = [...new Set(originalTasks.value.map(t => getTaskFieldValue(t, 'module')).filter(Boolean))]

  return [
    { key: 'status', label: 'State', icon: 'fa-regular fa-circle-dot', values: uniqStatuses },
    { key: 'assignee', label: 'Assignees', icon: 'fa-regular fa-user', values: uniqAssignees },
    { key: 'priority', label: 'Priority', icon: 'fa-solid fa-signal', values: uniqPriorities },
    { key: 'label', label: 'Label', icon: 'fa-solid fa-tag', values: uniqLabels },
    { key: 'cycle', label: 'Cycles', icon: 'fa-regular fa-circle-pause', values: uniqCycles },
    { key: 'module', label: 'Modules', icon: 'fa-solid fa-table-cells-large', values: uniqModules }
  ]
})

const filterOperators = {
  status: ['is', 'is not'],
  assignee: ['is', 'is not', 'empty', 'not empty'],
  priority: ['is', 'is not'],
  label: ['is', 'is not', 'empty', 'not empty'],
  cycle: ['is', 'is not', 'empty', 'not empty'],
  module: ['is', 'is not', 'empty', 'not empty']
}

const customValueMeta = (fieldKey, value) => {
  if (fieldKey === 'priority') {
    const p = String(value).toLowerCase()
    if (p === 'urgent') return { icon: 'fa-solid fa-circle-exclamation', color: '#ef4444' }
    if (p === 'high') return { icon: 'fa-solid fa-circle-chevron-up', color: '#f97316' }
    if (p === 'medium') return { icon: 'fa-solid fa-circle-dot', color: '#eab308' }
    if (p === 'low') return { icon: 'fa-solid fa-circle-chevron-down', color: '#3b82f6' }
  }
  return null
}

// Creation form
const newView = ref({
  name: '',
  description: '',
  queryMetadata: '{}'
})

// Display Properties State
const displayProps = ref(['ID', 'Assignee', 'Start date', 'Due date', 'Labels', 'Priority', 'State'])
const groupBy = ref('States')
const orderBy = ref('Manual')
const showSubItems = ref(false)

const filteredViews = computed(() => {
  const q = filterSearch.value.trim().toLowerCase()
  let nextViews = !q
    ? [...views.value]
    : views.value.filter(view =>
    `${view.name || ''} ${view.description || ''}`.toLowerCase().includes(q)
  )

  if (activeListViewFilters.value.length > 0) {
    nextViews = nextViews.filter(view => {
      return activeListViewFilters.value.every(f => {
        if (f.field === 'favorites') {
          const isMatch = Boolean(view.isFavorite)
          return f.operator === 'is' ? isMatch : !isMatch
        }
        if (f.field === 'scope') {
          const isMatch = Boolean(view.isGlobal)
          return f.operator === 'is' ? isMatch : !isMatch
        }
        return true
      })
    })
  }

  const sortValue = (view) => {
    if (sortBy.value === 'Name') return `${view.name || ''}`.toLowerCase()
    if (sortBy.value === 'Created at') return new Date(view.createdAt || 0).getTime()
    return new Date(view.updatedAt || view.createdAt || 0).getTime()
  }

  nextViews.sort((a, b) => {
    const left = sortValue(a)
    const right = sortValue(b)
    if (left === right) return 0
    const result = left > right ? 1 : -1
    return sortDir.value === 'desc' ? result * -1 : result
  })

  return nextViews
})

const fetchViews = async () => {
  try {
    const res = await axiosClient.get(`/projects/${projectId.value}/views`)
    views.value = res.data.data || []
  } catch (err) {
    console.error('Failed to fetch views', err)
  }
}

const fetchProjectMembers = async () => {
  try {
    const res = await axiosClient.get(`/projects/${projectId.value}/members`)
    projectMembers.value = res.data?.data || []
  } catch (err) {
    console.error('Failed to fetch project members', err)
    projectMembers.value = []
  }
}

const selectView = async (view) => {
  activeView.value = view
  const metadata = parseViewMetadata(view.queryMetadata)
  syncDesignerFromMetadata(metadata)
  await fetchViewTasks(view, metadata)
}

const fetchViewTasks = async (view, metadataInput = null) => {
  loading.value = true
  try {
    const metadata = metadataInput || parseViewMetadata(view.queryMetadata)
    const res = await axiosClient.get(`/tasks/search`, {
      params: { 
        projectId: projectId.value
      }
    })
    originalTasks.value = res.data.data || []
    tasks.value = applyTaskFilters(originalTasks.value, metadata.filters)
  } catch (err) {
    console.error('Failed to fetch tasks', err)
    originalTasks.value = []
    tasks.value = []
  } finally {
    loading.value = false
  }
}

const createView = async () => {
  if (!newView.value.name) return
  try {
    const payload = {
      ...newView.value,
      queryMetadata: buildQueryMetadata()
    }
    const res = await axiosClient.post(`/projects/${projectId.value}/views`, payload)
    views.value.unshift(res.data.data)
    ElNotification.success(t('Đã tạo góc nhìn'))
    showCreateModal.value = false
    resetForm()
  } catch (err) {
    ElNotification.error(t('Failed to create view'))
  }
}

const openCreateModal = () => {
  resetForm()
  showCreateModal.value = true
}

const refreshActiveViewTasks = async () => {
  if (!activeView.value) return
  await fetchViewTasks(activeView.value, {
    filters: activeFilters.value,
    displayProps: displayProps.value,
    groupBy: groupBy.value,
    orderBy: orderBy.value,
    showSubItems: showSubItems.value,
    viewType: viewType.value
  })
}

const handleTaskCreated = async (payload) => {
  try {
    await axiosClient.post(`/projects/${projectId.value}/WorkTasks`, {
      title: payload.title,
      description: payload.description || '',
      statusName: payload.statusName || 'BACKLOG',
      priority: payload.priority || 3
    })
    ElNotification.success(t('Work item created'))
    await refreshActiveViewTasks()
  } catch (err) {
    console.error('Failed to create work item', err)
    ElNotification.error(err.response?.data?.message || t('Failed to create work item'))
  }
}

const handleTaskUpdate = async (task, field, value, previousValue = task ? task[field] : undefined) => {
  if (!task?.id) return
  const payload = field && typeof field === 'object' && !Array.isArray(field) ? field : { [field]: value }
  const previousValues = Object.fromEntries(Object.keys(payload).map(key => [key, task[key]]))

  try {
    Object.entries(payload).forEach(([key, nextValue]) => {
      task[key] = nextValue
    })
    await axiosClient.patch(`/projects/${projectId.value}/WorkTasks/${task.id}`, payload)
    await refreshActiveViewTasks()
  } catch (err) {
    console.error('Failed to update work item', err)
    Object.entries(previousValues).forEach(([key, rollbackValue]) => {
      task[key] = rollbackValue
    })
    if (field && typeof field === 'string') task[field] = previousValue
    ElNotification.error(err.response?.data?.message || t('Failed to update work item'))
  }
}

const resetForm = () => {
    newView.value = { name: '', description: '', queryMetadata: '{}' }
    activeFilters.value = []
    displayProps.value = ['ID', 'Assignee', 'Start date', 'Due date', 'Labels', 'Priority', 'State']
    groupBy.value = 'States'
    orderBy.value = 'Manual'
    showSubItems.value = false
    viewType.value = 'list'
    modalTab.value = 'list'
}

const deleteView = async (id) => {
  try {
    await ElMessageBox.confirm(t('Are you sure you want to delete this view?'), t('Warning'), {
      type: 'warning',
      confirmButtonText: t('Delete'),
      confirmButtonClass: 'el-button--danger'
    })
    await axiosClient.delete(`/projects/${projectId.value}/views/${id}`)
    views.value = views.value.filter(v => v.id !== id)
    if (activeView.value?.id === id) activeView.value = null
    ElNotification.success(t('View deleted'))
  } catch (err) {
    if (err !== 'cancel') ElNotification.error(t('Failed to delete view'))
  }
}

const toggleFavorite = async (view) => {
  try {
    const res = await axiosClient.patch(`/projects/${projectId.value}/views/${view.id}/favorite`)
    view.isFavorite = res.data.data.isFavorite
  } catch (err) {
    ElNotification.error(t('Failed to toggle favorite'))
  }
}

const handleViewCommand = async (view, command) => {
    if (command === 'favorite') {
        await toggleFavorite(view)
        return
    }
    if (command === 'delete') {
        await deleteView(view.id)
    }
}

const resetModal = () => {
  showCreateModal.value = false
  resetForm()
}

const goBackToList = () => {
    activeView.value = null
    originalTasks.value = []
    tasks.value = []
    showActiveFilters.value = false
}

const toggleDisplayProp = (prop) => {
    const idx = displayProps.value.indexOf(prop)
    if (idx > -1) displayProps.value.splice(idx, 1)
    else displayProps.value.push(prop)
}

const viewTypeIcon = computed(() => {
    switch(viewType.value) {
        case 'list': return 'fa-solid fa-bars'
        case 'board': return 'fa-solid fa-columns'
        case 'calendar': return 'fa-regular fa-calendar'
        case 'table': return 'fa-solid fa-table'
        case 'timeline': return 'fa-solid fa-timeline'
        default: return 'fa-solid fa-bars'
    }
})

const activeFilterCount = computed(() => activeFilters.value.length)
const displayedColumnCount = computed(() => displayProps.value.length)
const activeListFilterCount = computed(() => Number(listFavoritesOnly.value) + Number(listGlobalOnly.value))
const activeViewSubtitle = computed(() => {
    if (activeView.value?.description) return activeView.value.description
    if (activeFilterCount.value) return t('{count} filters applied').replace('{count}', activeFilterCount.value)
    return t('Saved work item view')
})

const toTimestamp = (task, field) => {
    const value = getTaskFieldValue(task, field)
    const time = value ? new Date(value).getTime() : 0
    return Number.isNaN(time) ? 0 : time
}

const prioritySortWeight = (priority) => {
    const numeric = Number(priority)
    if (!Number.isNaN(numeric) && numeric > 0) return numeric
    return 99
}

const sortTasksByDisplayOrder = (items) => {
    const next = [...items]
    return next.sort((a, b) => {
        if (orderBy.value === 'Last created') return toTimestamp(b, 'createdAt') - toTimestamp(a, 'createdAt')
        if (orderBy.value === 'Last updated') return toTimestamp(b, 'updatedAt') - toTimestamp(a, 'updatedAt')
        if (orderBy.value === 'Start date') return toTimestamp(a, 'startDate') - toTimestamp(b, 'startDate')
        if (orderBy.value === 'Due date') return toTimestamp(a, 'dueDate') - toTimestamp(b, 'dueDate')
        if (orderBy.value === 'Priority') return prioritySortWeight(a.priority) - prioritySortWeight(b.priority)
        return (Number(a.sortOrder) || 0) - (Number(b.sortOrder) || 0)
    })
}

const visibleViewTasks = computed(() => {
    return sortTasksByDisplayOrder(applyTaskFilters(originalTasks.value, activeFilters.value))
})

const clearListFilters = () => {
    listFavoritesOnly.value = false
    listGlobalOnly.value = false
}

const handleViewRealtime = event => {
  if (`${event?.projectId}` !== `${projectId.value}` || event?.entityType !== 'project-view') return
  if (event.action === 'deleted') {
    views.value = views.value.filter(view => `${view.id}` !== `${event.entityId}`)
    if (`${activeView.value?.id}` === `${event.entityId}`) activeView.value = null
    return
  }
  if (!event.data?.id) return
  const index = views.value.findIndex(view => `${view.id}` === `${event.data.id}`)
  if (index >= 0) views.value[index] = { ...views.value[index], ...event.data }
  else views.value.unshift(event.data)
  if (`${activeView.value?.id}` === `${event.data.id}`) {
    activeView.value = { ...activeView.value, ...event.data }
  }
}

onMounted(async () => {
  signalRService.on('EntityChanged', handleViewRealtime)
  document.addEventListener('click', handleOutsideClick)
  await signalRService.startConnection(projectId.value)
  fetchViews()
  fetchProjectMembers()
})

onUnmounted(() => {
  signalRService.off('EntityChanged', handleViewRealtime)
  document.removeEventListener('click', handleOutsideClick)
})

watch(projectId, async () => {
    await signalRService.startConnection(projectId.value)
    activeView.value = null
    tasks.value = []
    originalTasks.value = []
    await Promise.all([fetchViews(), fetchProjectMembers()])
})

const getCreatorName = (view) => {
  if (view.creatorName) return view.creatorName
  if (view.createdByName) return view.createdByName
  if (view.createdBy) {
    const member = projectMembers.value.find(m => m.id === view.createdBy || m.userId === view.createdBy || m.userId === view.createdById)
    if (member) return member.fullName || member.name
  }
  return t('Owner')
}

const getAvatarBg = (name) => {
  if (!name || name === t('Owner')) return '#64748b'
  const colors = ['#3b82f6', '#10b981', '#fbbf24', '#ec4899', '#8b5cf6', '#06b6d4', '#f97316']
  let hash = 0
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash)
  }
  const index = Math.abs(hash) % colors.length
  return colors[index]
}

const getInitials = (name) => {
  if (!name) return 'O'
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (parts.length >= 2) {
    return (parts[0][0] + parts.at(-1)[0]).toUpperCase()
  }
  return name[0]?.toUpperCase() || 'O'
}
</script>

<template>
  <ProjectPageContainer>
    <ProjectPageHeader 
        icon="fa-regular fa-eye" 
        :title="activeView ? activeView.name : t('Views')" 
        :description="activeView ? activeViewSubtitle : t('Quản lý các chế độ xem tùy chỉnh cho công việc')"
      >
        <template #breadcrumbs v-if="activeView">
          <span class="cursor-pointer hover:underline text-blue-500" @click="goBackToList">{{ t('Views') }}</span>
          <i class="fa-solid fa-chevron-right text-xs mx-2 text-gray-500"></i>
        </template>
        <template #actions>
          <template v-if="!activeView">
            <button class="nexus-btn-primary" type="button" @click="openCreateModal">
              <i class="fa-solid fa-plus"></i> {{ t('Add view') }}
            </button>
          </template>
          <template v-else>
            <el-dropdown trigger="click" popper-class="display-popper-final" placement="bottom-end" :hide-on-click="false" :z-index="5000">
              <button class="nexus-btn-outlined" type="button">
                <i class="fa-solid fa-sliders"></i> {{ t('Display') }}
              </button>
              <template #dropdown>
                <div class="display-scroll-vfinal" @click.stop>
                  <div class="st-content">
                    <div class="st-sect">
                      <div class="st-sect-header"><span>{{ t('Display Properties') }}</span><i class="fa-solid fa-chevron-up"></i></div>
                      <div class="st-chips">
                        <span v-for="p in ['ID', 'Assignee', 'Start date', 'Due date', 'Labels', 'Priority', 'State', 'Sub-work item count', 'Attachment count', 'Link', 'Module', 'Cycle']"
                              :key="p" class="p-chip-st" :class="{ selected: displayProps.includes(p) }" @click.stop="toggleDisplayProp(p)">{{ t(p) }}</span>
                      </div>
                    </div>
                    <div class="st-sect">
                      <div class="st-sect-header"><span>{{ t('Group by') }}</span><i class="fa-solid fa-chevron-up"></i></div>
                      <div class="st-radios">
                        <label class="st-opt" v-for="g in ['States', 'Priority', 'None']" :key="g">
                          <input type="radio" name="active-groupby" :value="g" v-model="groupBy" />
                          <span class="st-dot"></span><span class="st-label">{{ t(g) }}</span>
                        </label>
                      </div>
                    </div>
                    <div class="st-sect">
                      <div class="st-sect-header"><span>{{ t('Order by') }}</span><i class="fa-solid fa-chevron-up"></i></div>
                      <div class="st-radios">
                        <label class="st-opt" v-for="o in ['Manual', 'Last created', 'Last updated', 'Start date', 'Due date', 'Priority']" :key="o">
                          <input type="radio" name="active-orderby" :value="o" v-model="orderBy" />
                          <span class="st-dot"></span><span class="st-label">{{ t(o) }}</span>
                        </label>
                      </div>
                    </div>
                    <div class="divider"></div>
                    <div class="st-foot">
                      <label class="st-check">
                        <input type="checkbox" v-model="showSubItems" />
                        <span class="checkmark"></span>
                        <span class="st-label">{{ t('Show sub-work items') }}</span>
                      </label>
                    </div>
                  </div>
                </div>
              </template>
            </el-dropdown>
          </template>
        </template>
      </ProjectPageHeader>

      <ProjectPageToolbar v-if="!activeView || activeView"
        :showSearch="!activeView"
        v-model:searchQuery="filterSearch"
        :searchPlaceholder="t('Search views...')"
      >
        <template #filters>
          <template v-if="!activeView">
            <div class="filter-dropdown-wrapper js-toolbar-popup-scope-views">
              <button
                class="timeline-filter-trigger icon-only-trigger"
                type="button"
                aria-label="Filters"
                title="Bộ lọc"
                @click="toggleViewsFilterDropdown"
                :class="{ active: showViewsFilterDropdown || activeListViewFilters.length }"
              >
                <i class="fa-solid fa-filter"></i>
                <span v-if="activeListViewFilters.length" class="filter-count">{{ activeListViewFilters.length }}</span>
              </button>
              <div class="plane-dropdown-menu filter-dropdown-menu" v-show="showViewsFilterDropdown" @click.stop>
                <FilterBar
                  v-model:filters="activeListViewFilters"
                  :fields="viewFilterFields"
                  :operators="viewOperators"
                  :custom-value-meta="customViewValueMeta"
                  :active="showViewsFilterDropdown"
                />
              </div>
            </div>
          </template>
          <template v-else>
            <div class="filter-dropdown-wrapper js-toolbar-popup-scope-active-view">
              <button
                class="timeline-filter-trigger icon-only-trigger"
                type="button"
                aria-label="Filters"
                title="Bộ lọc"
                @click="toggleActiveViewFilterDropdown"
                :class="{ active: showActiveViewFilterDropdown || activeFilters.length }"
              >
                <i class="fa-solid fa-filter"></i>
                <span v-if="activeFilters.length" class="filter-count">{{ activeFilters.length }}</span>
              </button>
              <div class="plane-dropdown-menu filter-dropdown-menu" v-show="showActiveViewFilterDropdown" @click.stop>
                <FilterBar
                  v-model:filters="activeFilters"
                  :fields="filterFields"
                  :operators="filterOperators"
                  :custom-value-meta="customValueMeta"
                  :active="showActiveViewFilterDropdown"
                />
              </div>
            </div>
          </template>
        </template>
        <template #toggles>
          <div v-if="!activeView" class="view-toggles">
            <button class="toggle-btn" :class="{ active: viewListMode === 'list' }" type="button" title="List view" @click="viewListMode = 'list'"><i class="fa-solid fa-bars"></i></button>
            <button class="toggle-btn" :class="{ active: viewListMode === 'grid' }" type="button" title="Grid view" @click="viewListMode = 'grid'"><i class="fa-solid fa-table-cells-large"></i></button>
          </div>
        </template>
        <template #sort>
          <ToolbarSortMenu v-if="!activeView" v-model="sortBy" v-model:direction="sortDir" label="Sort views" :options="viewSortOptions" />
        </template>
      </ProjectPageToolbar>

    <div v-if="!activeView && views.length === 0" class="empty-state-global">
      <div class="empty-spaces-icon"><i class="fa-solid fa-layer-group"></i></div>
      <div class="empty-spaces-copy">
        <h3>{{ t('No custom views here.') }}</h3>
        <p>{{ t('Create a filtered view to keep important work easy to find.') }}</p>
      </div>
      <button class="empty-state-action-btn" type="button" @click="openCreateModal">
        <i class="fa-solid fa-plus"></i> {{ t('Add view') }}
      </button>
    </div>

    <main v-else class="views-content">
      <div v-if="!activeView" class="views-list" :class="{ 'views-grid': viewListMode === 'grid' }">



        
        <div class="view-item-row" v-for="view in filteredViews" :key="view.id" @click="selectView(view)">
            <div class="vi-left">
                <div class="vi-icon-wrapper">
                    <i class="fa-solid fa-layer-group vi-icon"></i>
                </div>
                <div class="vi-meta">
                    <span class="vi-name">{{ view.name }}</span>
                    <span class="vi-desc" v-if="view.description">{{ view.description }}</span>
                </div>
            </div>
            <div class="vi-right">
                <i class="fa-solid fa-earth-americas vi-globe" v-if="view.isGlobal" :title="t('Global view')"></i>
                <div class="vi-avatar" :style="{ backgroundColor: getAvatarBg(getCreatorName(view)) }" :title="getCreatorName(view)">
                    {{ getInitials(getCreatorName(view)) }}
                </div>
                <button class="vi-star" type="button" :class="{ active: view.isFavorite }" @click.stop="toggleFavorite(view)">
                    <i class="fa-star" :class="view.isFavorite ? 'fa-solid' : 'fa-regular'"></i>
                </button>
                <el-dropdown trigger="click" @command="command => handleViewCommand(view, command)">
                    <button class="vi-more" type="button" @click.stop><i class="fa-solid fa-ellipsis"></i></button>
                    <template #dropdown>
                        <el-dropdown-menu class="dark-popover">
                            <el-dropdown-item command="favorite">{{ view.isFavorite ? t('Remove favorite') : t('Add to favorite') }}</el-dropdown-item>
                            <el-dropdown-item command="delete">{{ t('Delete view') }}</el-dropdown-item>
                        </el-dropdown-menu>
                    </template>
                </el-dropdown>
            </div>
        </div>
      </div>

      <div v-else class="detail-container">
        <section class="view-detail-summary">
          <div>
            <span class="section-kicker">{{ t('Active view') }}</span>
            <h2>{{ activeView.name }}</h2>
            <p>{{ activeViewSubtitle }}</p>
          </div>
          <div class="summary-metrics">
            <span><strong>{{ visibleViewTasks.length }}</strong> {{ t('items') }}</span>
            <span><strong>{{ activeFilterCount }}</strong> {{ t('filters') }}</span>
            <span><strong>{{ displayedColumnCount }}</strong> {{ t('columns') }}</span>
          </div>
        </section>

        <div v-if="showActiveFilters || activeFilterCount" class="active-filter-row">
          <FilterBar v-model:filters="activeFilters" />
        </div>

        <div v-if="loading" class="view-loading-shell">
          <div v-for="line in 6" :key="line" class="skeleton-row"></div>
        </div>

        <ListView
          v-else
          :tasks="visibleViewTasks"
          :project-members="projectMembers"
          :group-by="groupBy"
          :show-sub-items="showSubItems"
          @task-created="handleTaskCreated"
          @update-task="handleTaskUpdate"
        />
      </div>
    </main>

    <Teleport to="body">
    <div class="modal-overlay sa-data-modal-overlay sa-modal--form" v-if="showCreateModal" @click.self="resetModal">
      <div class="view-modal premium">
        <div class="modal-header">
          <DataModalHeader
            icon="bi bi-eye"
            :title="t('Create View')"
            :description="t('Save filters, grouping and display settings as a reusable view')"
            :close-label="t('Close')"
            @close="resetModal"
          />
        </div>
        <div class="modal-body">
          <DataModalSection icon="bi bi-card-text" :title="t('View information')">
            <DataModalField :label="t('Title')" required>
              <input type="text" v-model="newView.name" :placeholder="t('Title')" class="title-input" />
            </DataModalField>
            <DataModalField :label="t('Description')">
            <textarea v-model="newView.description" :placeholder="t('Description')" rows="4" class="desc-input"></textarea>
            </DataModalField>
          </DataModalSection>

          <DataModalSection icon="bi bi-funnel" :title="t('Filters')">
            <div class="m-filter-section">
                <FilterBar 
                    :filters="activeFilters" 
                    @remove="handleRemoveFilter" 
                    @clear="handleClearFilters"
                    @add="handleAddFilter"
                />
            </div>
          </DataModalSection>

          <DataModalSection icon="bi bi-layout-three-columns" :title="t('Display settings')">
            <div class="modal-controls-bar">
                <div class="toggle-group">
                    <button class="m-toggle" :class="{ active: modalTab === 'list' }" @click="modalTab = 'list'" type="button">
                        {{ t('List') }}
                    </button>
                    <el-dropdown trigger="click" popper-class="display-popper-final" placement="right-start" :hide-on-click="false" :z-index="5000">
                        <button class="m-toggle" :class="{ active: modalTab === 'display' }" @click="modalTab = 'display'" type="button">{{ t('Display') }}</button>
                        <template #dropdown>
                            <div class="display-scroll-vfinal">
                                <div class="st-content">
                                    <div class="st-sect">
                                        <div class="st-sect-header"><span>{{ t('Display Properties') }}</span><i class="fa-solid fa-chevron-up"></i></div>
                                        <div class="st-chips">
                                <span v-for="p in ['ID', 'Assignee', 'Start date', 'Due date', 'Labels', 'Priority', 'State', 'Sub-work item count', 'Attachment count', 'Link', 'Module', 'Cycle']"
                                                  :key="p" class="p-chip-st" :class="{ selected: displayProps.includes(p) }" @click.stop="toggleDisplayProp(p)">{{ t(p) }}</span>
                                        </div>
                                    </div>
                                    <div class="st-sect">
                                        <div class="st-sect-header"><span>{{ t('Group by') }}</span><i class="fa-solid fa-chevron-up"></i></div>
                                        <div class="st-radios">
                                            <label class="st-opt" v-for="g in ['States', 'Priority', 'Cycle', 'Module', 'Labels', 'Assignees', 'Created by', 'None']" :key="g">
                                                <input type="radio" name="pop-groupby" :value="g" v-model="groupBy" />
                                                <span class="st-dot"></span><span class="st-label">{{ t(g) }}</span>
                                            </label>
                                        </div>
                                    </div>
                                    <div class="st-sect">
                                        <div class="st-sect-header"><span>{{ t('Order by') }}</span><i class="fa-solid fa-chevron-up"></i></div>
                                        <div class="st-radios">
                                            <label class="st-opt" v-for="o in ['Manual', 'Last created', 'Last updated', 'Start date', 'Due date', 'Priority']" :key="o">
                                                <input type="radio" name="pop-orderby" :value="o" v-model="orderBy" />
                                                <span class="st-dot"></span><span class="st-label">{{ t(o) }}</span>
                                            </label>
                                        </div>
                                    </div>
                                    <div class="divider"></div>
                                    <div class="st-foot">
                                        <label class="st-check">
                                            <input type="checkbox" v-model="showSubItems" />
                                            <span class="checkmark"></span>
                                            <span class="st-label">{{ t('Show sub-work items') }}</span>
                                        </label>
                                    </div>
                                </div>
                            </div>
                        </template>
                    </el-dropdown>
                </div>
                <el-dropdown trigger="click" popper-class="filter-modal-popper" placement="bottom-start" :z-index="5000">
                    <button class="filter-btn" type="button">{{ t('Filters') }}</button>
                    <template #dropdown>
                        <div class="filter-modal-dropdown">
                            <div class="f-search">
                                <i class="fa-solid fa-magnifying-glass"></i>
                                <input type="text" :placeholder="t('Search')" />
                            </div>
                            <div class="f-options">
                                <div class="f-opt" @click="addFilterOption('status')"><i class="fa-regular fa-circle-dot"></i> {{ t('State') }}</div>
                                <div class="f-opt" @click="addFilterOption('assignee')"><i class="fa-regular fa-user"></i> {{ t('Assignees') }}</div>
                                <div class="f-opt" @click="addFilterOption('priority')"><i class="fa-solid fa-signal"></i> {{ t('Priority') }}</div>
                                <div class="f-opt" @click="addFilterOption('label')"><i class="fa-solid fa-tag"></i> {{ t('Label') }}</div>
                                <div class="f-opt" @click="addFilterOption('cycle')"><i class="fa-regular fa-circle-pause"></i> {{ t('Cycles') }}</div>
                                <div class="f-opt" @click="addFilterOption('module')"><i class="fa-solid fa-table-cells-large"></i> {{ t('Modules') }}</div>
                                <div class="f-opt" @click="addFilterOption('startDate')"><i class="fa-regular fa-calendar-plus"></i> {{ t('Start date') }}</div>
                                <div class="f-opt" @click="addFilterOption('dueDate')"><i class="fa-regular fa-calendar"></i> {{ t('Target date') }}</div>
                                <div class="f-opt" @click="addFilterOption('createdAt')"><i class="fa-regular fa-calendar"></i> {{ t('Created at') }}</div>
                                <div class="f-opt" @click="addFilterOption('updatedAt')"><i class="fa-regular fa-calendar"></i> {{ t('Updated at') }}</div>
                            </div>
                        </div>
                    </template>
                </el-dropdown>
            </div>
          </DataModalSection>
        </div>
        <div class="modal-footer">
            <button class="cancel-btn" type="button" @click="resetModal">{{ t('Cancel') }}</button>
            <button class="create-btn" type="button" @click="createView" :disabled="!newView.name">{{ t('Create View') }}</button>
        </div>
      </div>
    </div>
    </Teleport>
  </ProjectPageContainer>
</template>

<style scoped>
.views-page {
  --views-bg: var(--color-bg);
  --views-panel: var(--color-surface);
  --views-panel-strong: var(--color-surface);
  --views-line: var(--color-border);
  --views-muted-line: color-mix(in srgb, var(--color-border) 84%, transparent);
  --views-accent-soft: color-mix(in srgb, var(--color-accent) 14%, transparent);
  display: flex;
  flex-direction: column;
  min-height: 100%;
  background: var(--views-bg);
  color: var(--color-text-primary);
  font-family: 'Inter', system-ui, sans-serif;
}

.nexus-project-header {
  position: sticky;
  top: 0;
  z-index: 5;
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 56px;
  padding: 0 24px;
  border-bottom: 1px solid var(--views-muted-line);
  background: color-mix(in srgb, var(--views-bg) 88%, transparent);
  backdrop-filter: blur(14px);
}

.nexus-breadcrumb,
.nexus-controls-row,
.vi-left,
.vi-right {
  display: flex;
  align-items: center;
}

.nexus-breadcrumb {
  gap: 10px;
  min-width: 0;
  font-size: 13px;
  font-weight: 650;
}

.project-icon {
  width: 18px;
  height: 18px;
  display: grid;
  place-items: center;
  border-radius: 5px;
  background: color-mix(in srgb, var(--color-accent) 90%, #ffffff);
  color: #020617;
  box-shadow: 0 0 0 1px color-mix(in srgb, var(--color-accent) 45%, transparent);
}

.view-name {
  max-width: 360px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--color-text-primary);
}

.view-name.cursor-pointer {
  color: var(--color-text-secondary);
}

.view-name.cursor-pointer:hover {
  color: var(--color-text-primary);
}

.sep {
  font-size: 9px;
  color: var(--color-text-muted);
}

.nexus-controls-row {
  gap: 8px;
}

.nexus-search-input,
.title-input,
.desc-input,
.plane-search-input {
  outline: none;
}

.nexus-search-input {
  height: 32px;
  border: 1px solid var(--views-muted-line);
  border-radius: 7px;
  background: color-mix(in srgb, var(--views-panel) 78%, #020617);
  color: var(--color-text-primary);
  padding: 0 11px;
  font-size: 13px;
}

.nexus-search-input:focus,
.title-input:focus,
.desc-input:focus {
  border-color: color-mix(in srgb, var(--color-accent) 68%, var(--color-border));
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-accent) 14%, transparent);
}

.nexus-btn-icon,
.nexus-btn-outlined,
.nexus-btn-primary,
.filter-btn,
.m-toggle,
.cancel-btn,
.create-btn {
  min-height: 32px;
  border-radius: 7px;
  transition: background 160ms ease, border-color 160ms ease, color 160ms ease, transform 120ms ease;
}

.nexus-btn-icon:active,
.nexus-btn-outlined:active,
.nexus-btn-primary:active,
.filter-btn:active,
.m-toggle:active,
.cancel-btn:active,
.create-btn:active {
  transform: translateY(1px);
}

.nexus-btn-icon,
.nexus-btn-outlined {
  border: 1px solid var(--views-muted-line);
  background: color-mix(in srgb, var(--views-panel) 82%, transparent);
  color: var(--color-text-secondary);
}

.nexus-btn-icon {
  width: 32px;
  display: grid;
  place-items: center;
}

.nexus-btn-outlined,
.nexus-btn-primary {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 0 12px;
  font-size: 12px;
  font-weight: 650;
}

.nexus-btn-icon:hover,
.nexus-btn-outlined:hover {
  border-color: color-mix(in srgb, var(--color-accent) 46%, var(--color-border));
  background: color-mix(in srgb, var(--views-panel-strong) 92%, var(--color-accent));
  color: var(--color-text-primary);
}

.nexus-btn-outlined.active {
  border-color: color-mix(in srgb, var(--color-accent) 56%, var(--color-border));
  background: var(--views-accent-soft);
  color: var(--color-accent);
}

.toolbar-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 17px;
  height: 17px;
  padding: 0 5px;
  border-radius: 999px;
  background: var(--color-accent);
  color: #020617;
  font-size: 10px;
  font-weight: 800;
  font-variant-numeric: tabular-nums;
}

.nexus-btn-primary {
  border: 1px solid color-mix(in srgb, var(--color-accent) 76%, #ffffff);
  background: color-mix(in srgb, var(--color-accent) 88%, #0f172a);
  color: #020617;
  box-shadow: 0 8px 24px color-mix(in srgb, var(--color-accent) 18%, transparent);
}

.nexus-btn-primary:hover {
  background: var(--color-accent-hover);
}

.views-filter-menu {
  --views-menu-panel: color-mix(in srgb, var(--color-surface) 92%, #020617);
  --views-menu-line: color-mix(in srgb, var(--color-border) 82%, transparent);
  width: 220px;
  padding: 8px;
  border: 1px solid var(--views-menu-line);
  border-radius: 8px;
  background: var(--views-menu-panel);
  box-shadow: 0 18px 48px color-mix(in srgb, #020617 44%, transparent);
}

.menu-check {
  display: flex;
  align-items: center;
  gap: 9px;
  min-height: 34px;
  padding: 0 8px;
  border-radius: 7px;
  color: var(--color-text-secondary);
  cursor: pointer;
  font-size: 13px;
}

.menu-check:hover {
  background: color-mix(in srgb, var(--color-surface-hover) 72%, transparent);
  color: var(--color-text-primary);
}

.menu-check input {
  accent-color: var(--color-accent);
}

.menu-clear {
  width: 100%;
  margin-top: 6px;
  min-height: 32px;
  border: 1px solid var(--views-menu-line);
  border-radius: 7px;
  background: transparent;
  color: var(--color-text-secondary);
  cursor: pointer;
  font-size: 12px;
  font-weight: 650;
}

.menu-clear:hover:not(:disabled) {
  color: var(--color-text-primary);
  background: color-mix(in srgb, var(--color-surface-hover) 72%, transparent);
}

.menu-clear:disabled {
  cursor: not-allowed;
  opacity: 0.45;
}

.views-content {
  flex: 1;
  padding: 22px 28px 32px;
  overflow: visible;
}

.views-list,
.detail-container {
  width: min(100%, 1280px);
  margin: 0 auto;
}

.views-list.views-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 12px; }
.views-list.views-grid .view-item-row { min-height: 140px; margin-bottom: 0; align-items: flex-start; flex-direction: column; }
.views-list.views-grid .vi-right { width: 100%; justify-content: flex-end; }

.views-list-head,
.view-detail-summary {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 20px;
  margin-bottom: 16px;
}

.section-kicker {
  display: block;
  margin-bottom: 5px;
  color: var(--color-accent);
  font-size: 11px;
  font-weight: 750;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.views-list-head h2,
.view-detail-summary h2 {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 20px;
  font-weight: 760;
  letter-spacing: 0;
  line-height: 1.15;
  text-wrap: balance;
}

.view-detail-summary p {
  margin: 6px 0 0;
  max-width: 58ch;
  color: var(--color-text-secondary);
  font-size: 13px;
  line-height: 1.45;
}

.views-count,
.summary-metrics {
  color: var(--color-text-muted);
  font-size: 12px;
  font-variant-numeric: tabular-nums;
}

.summary-metrics {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  justify-content: flex-end;
}

.summary-metrics span {
  display: inline-flex;
  align-items: baseline;
  gap: 5px;
  padding: 6px 9px;
  border: 1px solid var(--views-muted-line);
  border-radius: 7px;
  background: color-mix(in srgb, var(--views-panel) 76%, transparent);
}

.summary-metrics strong {
  color: var(--color-text-primary);
}

.view-item-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 18px;
  min-height: 62px;
  padding: 12px 14px;
  margin-bottom: 8px;
  border: 1px solid var(--views-muted-line);
  border-radius: 8px;
  background: color-mix(in srgb, var(--views-panel) 88%, transparent);
  cursor: pointer;
  box-shadow: 0 14px 32px color-mix(in srgb, #020617 18%, transparent);
  transition: transform 160ms ease, background 160ms ease, border-color 160ms ease;
}

.view-item-row:hover {
  border-color: color-mix(in srgb, var(--color-accent) 42%, var(--color-border));
  background: color-mix(in srgb, var(--views-panel-strong) 88%, var(--color-accent));
  transform: translateY(-1px);
}

.view-item-row:active {
  transform: translateY(0);
}

.vi-left {
  gap: 12px;
  min-width: 0;
}

.vi-icon-wrapper {
  width: 34px;
  height: 34px;
  border-radius: 8px;
  display: grid;
  place-items: center;
  flex: 0 0 auto;
  background: var(--views-accent-soft);
  color: var(--color-accent);
  font-size: 14px;
}

.vi-meta {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 3px;
}

.vi-name {
  color: var(--color-text-primary);
  font-size: 14px;
  font-weight: 650;
}

.vi-desc {
  color: var(--color-text-muted);
  font-size: 12px;
  line-height: 1.35;
}

.vi-right {
  gap: 8px;
  flex: 0 0 auto;
}

.vi-avatar {
  width: 26px;
  height: 26px;
  display: grid;
  place-items: center;
  border: 1px solid color-mix(in srgb, #ffffff 26%, transparent);
  border-radius: 7px;
  color: #ffffff;
  font-size: 10px;
  font-weight: 750;
  box-shadow: 0 1px 0 color-mix(in srgb, #ffffff 18%, transparent) inset;
}

.vi-star,
.vi-more {
  width: 28px;
  height: 28px;
  display: grid;
  place-items: center;
  border: 1px solid transparent;
  border-radius: 7px;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
  transition: color 160ms ease, background 160ms ease, border-color 160ms ease;
}

.vi-star:hover,
.vi-more:hover {
  border-color: var(--views-muted-line);
  background: color-mix(in srgb, var(--views-panel-strong) 86%, #ffffff);
  color: var(--color-text-primary);
}

.vi-star.active {
  color: #fbbf24;
}

.vi-globe {
  color: var(--color-accent);
  font-size: 13px;
}

.empty-placeholder,
.view-loading-shell {
  border: 1px solid var(--views-muted-line);
  border-radius: 8px;
  background: color-mix(in srgb, var(--views-panel) 86%, transparent);
}

.empty-placeholder {
  display: grid;
  place-items: center;
  min-height: 260px;
  padding: 32px;
  text-align: center;
}

.empty-mark {
  width: 44px;
  height: 44px;
  display: grid;
  place-items: center;
  margin-bottom: 14px;
  border-radius: 8px;
  background: var(--views-accent-soft);
  color: var(--color-accent);
}

.empty-placeholder h3 {
  margin: 0;
  font-size: 15px;
  font-weight: 700;
}

.empty-placeholder p {
  margin: 8px 0 0;
  max-width: 36ch;
  color: var(--color-text-muted);
  font-size: 13px;
  line-height: 1.45;
}

.detail-container {
  padding: 18px;
  border: 1px solid var(--views-muted-line);
  border-radius: 8px;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--views-panel-strong) 86%, transparent), color-mix(in srgb, var(--views-panel) 92%, transparent)),
    var(--views-panel);
  box-shadow: 0 18px 48px color-mix(in srgb, #020617 24%, transparent);
}

.active-filter-row {
  margin-bottom: 14px;
}

.view-loading-shell {
  display: grid;
  gap: 8px;
  padding: 12px;
}

.skeleton-row {
  height: 42px;
  border-radius: 7px;
  background:
    linear-gradient(90deg, transparent, color-mix(in srgb, #ffffff 7%, transparent), transparent),
    color-mix(in srgb, var(--color-surface-hover) 52%, transparent);
  background-size: 240px 100%, 100% 100%;
  animation: skeleton-sweep 1.2s ease-in-out infinite;
}

@keyframes skeleton-sweep {
  from { background-position: -240px 0, 0 0; }
  to { background-position: calc(100% + 240px) 0, 0 0; }
}

.modal-overlay {
  position: fixed;
  inset: 0;
  z-index: 2000;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
  background: color-mix(in srgb, #020617 74%, transparent);
  backdrop-filter: none;
  -webkit-backdrop-filter: none;
}

.view-modal.premium {
  width: min(720px, 100%);
  max-height: min(820px, calc(100dvh - 48px));
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid color-mix(in srgb, var(--color-accent) 22%, var(--color-border));
  border-radius: 8px;
  background: var(--color-surface);
  box-shadow: var(--shadow-xl);
}

.modal-header,
.modal-footer {
  flex: 0 0 auto;
  border-color: var(--views-muted-line);
}

.modal-header {
  padding: 18px 22px;
  border-bottom: 1px solid var(--views-muted-line);
}

.modal-header h3 {
  margin: 0;
  font-size: 15px;
  font-weight: 750;
}

.modal-body {
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding: 20px 22px;
  overflow: auto;
}

.title-input,
.desc-input {
  width: 100%;
  border: 1px solid transparent;
  background: transparent;
  color: var(--color-text-primary);
  font-size: 13px;
}

.title-input {
  padding: 0 12px;
}

.desc-input {
  min-height: 96px;
  resize: vertical;
  padding: 13px 14px;
  border-color: var(--views-muted-line);
  border-radius: 8px;
  background: var(--color-input-bg);
  color: var(--color-text-secondary);
}

.m-filter-section {
  margin: 2px 0;
}

.modal-controls-bar,
.toggle-group,
.st-opt,
.st-check {
  display: flex;
  align-items: center;
}

.modal-controls-bar {
  justify-content: space-between;
  gap: 12px;
}

.toggle-group {
  gap: 3px;
  padding: 3px;
  border: 1px solid var(--views-muted-line);
  border-radius: 8px;
  background: var(--color-input-bg);
}

.m-toggle,
.filter-btn,
.cancel-btn {
  border: 1px solid transparent;
  background: transparent;
  color: var(--color-text-secondary);
  cursor: pointer;
  font-size: 13px;
  font-weight: 650;
}

.m-toggle {
  padding: 6px 12px;
}

.m-toggle.active,
.m-toggle:hover,
.filter-btn:hover,
.cancel-btn:hover {
  border-color: var(--views-muted-line);
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.filter-btn {
  display: inline-flex;
  align-items: center;
  padding: 7px 12px;
  border-color: var(--views-muted-line);
  background: var(--color-input-bg);
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  padding: 14px 22px;
  border-top: 1px solid var(--views-muted-line);
  background: var(--color-surface);
}

.cancel-btn {
  padding: 8px 13px;
}

.create-btn {
  border: 1px solid var(--color-accent);
  background: var(--color-accent);
  color: #fff;
  font-size: 13px;
  font-weight: 760;
  padding: 8px 16px;
  cursor: pointer;
}

.create-btn:disabled {
  cursor: not-allowed;
  opacity: 0.46;
}

.display-scroll-vfinal {
  --views-panel-strong: var(--color-surface);
  --views-muted-line: color-mix(in srgb, var(--color-border) 84%, transparent);
  --views-accent-soft: color-mix(in srgb, var(--color-accent) 14%, transparent);
  width: 330px;
  max-height: 520px;
  overflow: auto;
  border: 1px solid var(--views-muted-line);
  border-radius: 8px;
  background: var(--views-panel-strong);
  box-shadow: var(--shadow-xl);
}

.display-scroll-vfinal::-webkit-scrollbar {
  width: 5px;
}

.display-scroll-vfinal::-webkit-scrollbar-thumb {
  border-radius: 10px;
  background: var(--color-border);
}

.st-content {
  padding: 18px;
}

.st-sect {
  margin-bottom: 22px;
}

.st-sect-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 11px;
  color: var(--color-text-muted);
  font-size: 11px;
  font-weight: 760;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.st-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 7px;
}

.p-chip-st {
  padding: 6px 9px;
  border: 1px solid var(--views-muted-line);
  border-radius: 7px;
  background: var(--color-input-bg);
  color: var(--color-text-secondary);
  cursor: pointer;
  font-size: 12px;
  transition: background 150ms ease, color 150ms ease, border-color 150ms ease;
}

.p-chip-st:hover {
  color: var(--color-text-primary);
  border-color: color-mix(in srgb, var(--color-accent) 42%, var(--color-border));
}

.p-chip-st.selected {
  border-color: var(--color-accent);
  background: var(--views-accent-soft);
  color: var(--color-accent);
}

.st-radios {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.st-opt {
  gap: 10px;
  cursor: pointer;
}

.st-opt input,
.st-check input {
  display: none;
}

.st-dot {
  position: relative;
  width: 14px;
  height: 14px;
  border: 1.5px solid var(--color-border);
  border-radius: 50%;
}

.st-opt input:checked + .st-dot {
  border-color: var(--color-accent);
}

.st-opt input:checked + .st-dot::after {
  content: "";
  position: absolute;
  inset: 3px;
  border-radius: 50%;
  background: var(--color-accent);
}

.st-label {
  color: var(--color-text-secondary);
  font-size: 13px;
}

.divider {
  height: 1px;
  margin: 16px 0;
  background: var(--views-muted-line);
}

.st-check {
  gap: 10px;
  cursor: pointer;
}

.checkmark {
  position: relative;
  width: 15px;
  height: 15px;
  border: 1.5px solid var(--color-border);
  border-radius: 4px;
}

.st-check input:checked + .checkmark {
  border-color: var(--color-accent);
  background: var(--color-accent);
}

.st-check input:checked + .checkmark::after {
  content: "";
  position: absolute;
  left: 4px;
  top: 1px;
  width: 4px;
  height: 8px;
  border: solid var(--color-text-inverse);
  border-width: 0 1.5px 1.5px 0;
  transform: rotate(45deg);
}

:deep(.display-popper-final) {
  z-index: 10000 !important;
  border: none !important;
  background: transparent !important;
  box-shadow: none !important;
}

:deep(.views-popper-clean) {
  z-index: 10000 !important;
  border: none !important;
  background: transparent !important;
  box-shadow: none !important;
}

@media (max-width: 760px) {
  .nexus-project-header,
  .views-list-head,
  .view-detail-summary,
  .modal-controls-bar {
    align-items: stretch;
    flex-direction: column;
  }

  .nexus-project-header {
    position: static;
    padding: 12px 16px;
  }

  .nexus-controls-row,
  .summary-metrics {
    justify-content: flex-start;
  }

  .views-content {
    padding: 16px;
  }

  .detail-container {
    padding: 12px;
  }
}

/* MODAL FILTER DROPDOWN REFINEMENT */
.filter-modal-dropdown {
    width: 240px; background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 8px; overflow: hidden;
}
.f-search {
    display: flex; align-items: center; gap: 10px; padding: 10px 14px; border-bottom: 1px solid var(--color-border);
}
.f-search i { font-size: 11px; color: var(--color-text-muted); }
.f-search input {
    background: transparent; border: none; color: var(--color-text-primary); font-size: 13px; outline: none; width: 100%;
}
.f-options { padding: 4px; max-height: 400px; overflow-y: auto; }
.f-opt {
    display: flex; align-items: center; gap: 12px; padding: 8px 12px; border-radius: 6px; cursor: pointer;
    font-size: 13px; color: var(--color-text-secondary); transition: background 0.2s;
}
.f-opt:hover { background: var(--color-surface-hover); color: var(--color-text-primary); }
.f-opt i { font-size: 12px; width: 16px; text-align: center; color: var(--color-text-muted); }

:deep(.filter-modal-popper) { background: transparent !important; border: none !important; box-shadow: none !important; z-index: 10000 !important; }

/* Compact density */
.views-page {
  min-height: calc(100vh - var(--sa-topbar-height, 52px)) !important;
}

.nexus-project-header {
  min-height: 52px !important;
  padding: 10px 16px !important;
}

.nexus-controls-row {
  gap: 8px !important;
}

.nexus-search-input,
.nexus-btn-primary,
.nexus-btn-secondary,
.view-btn,
.views-filter-btn {
  min-height: 32px !important;
  border-radius: 8px !important;
  padding: 6px 10px !important;
  font-size: 12.5px !important;
}

.views-content {
  padding: 16px var(--sa-page-x, 24px) 26px !important;
}

.views-list-head h2 {
  font-size: 17px !important;
}

.views-list-row,
.view-row {
  min-height: 54px !important;
  padding: 10px 12px !important;
  border-radius: 8px !important;
}

.view-title {
  font-size: 13.5px !important;
}

.empty-state {
  min-height: 180px !important;
  padding: 22px !important;
}

@media (max-width: 760px) {
  .nexus-project-header {
    padding: 10px 12px !important;
  }

  .views-content {
    padding: 12px !important;
  }
}
.views-page {
  min-height: 0 !important;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-bg) 88%, var(--color-surface)) 0%, var(--color-bg) 220px),
    var(--color-bg) !important;
}

:deep(.project-page-toolbar) {
  width: 100%;
  margin-top: 0;
}

:deep(.project-page-toolbar .ppt-left) {
  flex: 1;
}

:deep(.project-page-toolbar .ppt-search) {
  width: min(260px, 100%);
}

:deep(.project-page-toolbar .ppt-right) {
  margin-left: auto;
}

.views-content {
  padding: 16px 0 18px !important;
}

.views-list,
.detail-container {
  width: 100% !important;
}

.views-list-head,
.view-detail-summary {
  margin-bottom: 12px !important;
}

.views-list-head h2,
.view-detail-summary h2 {
  font-size: 18px !important;
}

.view-item-row,
.views-list-row,
.view-row {
  min-height: 50px !important;
  padding: 9px 12px !important;
  border-radius: 10px !important;
  box-shadow: 0 8px 20px color-mix(in srgb, #020617 10%, transparent) !important;
}

.empty-state,
.empty-placeholder {
  min-height: 150px !important;
  padding: 18px !important;
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
</style>
