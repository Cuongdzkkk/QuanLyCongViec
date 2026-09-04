<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from '@/composables/useI18n'
import { useRouter } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import { ElMessage, ElMessageBox } from 'element-plus'
import { subscribeAdminRealtime } from '@/utils/adminRealtime'
import ProjectPageContainer from '@/components/common/ProjectPageContainer.vue'
import ProjectPageHeader from '@/components/common/ProjectPageHeader.vue'
import ProjectPageToolbar from '@/components/common/ProjectPageToolbar.vue'
import ToolbarSortMenu from '@/components/common/ToolbarSortMenu.vue'
import ProjectEmptyState from '@/components/common/ProjectEmptyState.vue'
import { buildSpacePath } from '@/utils/spaceRoute'
import FilterBar from '@/components/FilterBar.vue'

const props = defineProps({
  projectId: { type: String, required: true }
})

const router = useRouter()
const { t } = useI18n()

const modules = ref([])
const projectMembers = ref([])
const projectTasks = ref([])
const loadingModules = ref(false)
const loadingTasks = ref(false)
const loadingMoreModules = ref(false)
const showCreateModal = ref(false)
const showRestoreModal = ref(false)
const isEditing = ref(false)
const editingModuleId = ref(null)
const rowCalendarModId = ref(null)
const moduleSearch = ref('')
const sortBy = ref('updatedAt')
const sortDirection = ref('desc')
const moduleSortOptions = [
  { value: 'updatedAt', label: 'Recently updated', icon: 'fa-regular fa-clock' },
  { value: 'name', label: 'Module name', icon: 'fa-solid fa-font' },
  { value: 'status', label: 'Status', icon: 'fa-solid fa-circle-dot' }
]
const activeFilters = ref([])

const moduleFilterFields = computed(() => [
  { key: 'status', label: 'Status', icon: 'fa-solid fa-circle-dot', values: ['Backlog', 'Planned', 'In Progress', 'Paused', 'Completed', 'Cancelled'] }
])

const moduleOperators = {
  status: ['is', 'is not']
}

const customModuleValueMeta = (fieldKey, value) => {
  if (fieldKey === 'status') {
    const opt = statusOptions.find(o => o.label.toLowerCase() === value.toLowerCase())
    if (opt) return { icon: opt.icon, color: opt.color }
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

const viewMode = ref('list')
const activeModule = ref(null)

const modulePagination = ref({
  page: 1,
  pageSize: 20,
  totalCount: 0,
  totalPages: 0,
  hasPreviousPage: false,
  hasNextPage: false
})

const statusOptions = [
  { key: 'backlog', label: 'Backlog', icon: 'fa-solid fa-expand', color: 'var(--color-text-muted)', bg: 'rgba(113,113,122,0.12)' },
  { key: 'planned', label: 'Planned', icon: 'fa-regular fa-circle', color: '#60A5FA', bg: 'rgba(96,165,250,0.12)' },
  { key: 'in progress', label: 'In Progress', icon: 'fa-solid fa-circle-notch', color: '#FBBF24', bg: 'rgba(251,191,36,0.12)' },
  { key: 'paused', label: 'Paused', icon: 'fa-solid fa-pause', color: 'var(--color-text-muted)', bg: 'rgba(161,161,170,0.12)' },
  { key: 'completed', label: 'Completed', icon: 'fa-regular fa-circle-check', color: '#4ADE80', bg: 'rgba(74,222,128,0.12)' },
  { key: 'cancelled', label: 'Cancelled', icon: 'fa-regular fa-circle-xmark', color: '#F87171', bg: 'rgba(248,113,113,0.12)' },
  { key: 'disabled', label: 'Disabled', icon: 'fa-solid fa-box-archive', color: '#94A3B8', bg: 'rgba(148,163,184,0.12)' }
]

const statusConfig = Object.fromEntries(statusOptions.map(option => [option.key, option]))

const getStatusLabel = (key) => {
  if (!key) return ''
  const camelKey = key.toLowerCase().replace(/\s+(.)/g, (_, char) => char.toUpperCase()).trim()
  return t(`modules.statuses.${camelKey}`, statusConfig[key]?.label || key)
}

const form = ref({
  name: '',
  description: '',
  status: 'Backlog',
  leadId: null,
  taskIds: [],
  dateRange: []
})

const getStatusKey = (raw) => {
  if (!raw) return 'backlog'
  const normalized = raw.toLowerCase().trim()
  if (normalized.includes('progress') || normalized === 'active') return 'in progress'
  if (normalized.includes('complete') || normalized === 'done') return 'completed'
  if (normalized.includes('cancel')) return 'cancelled'
  if (normalized.includes('plan')) return 'planned'
  if (normalized.includes('pause')) return 'paused'
  if (normalized.includes('disable') || normalized.includes('archive')) return 'disabled'
  return 'backlog'
}

const formatDate = (value) => {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  const day = `${date.getDate()}`.padStart(2, '0')
  const month = `${date.getMonth() + 1}`.padStart(2, '0')
  const year = date.getFullYear()
  return `${day}/${month}/${year}`
}

const formatDateRange = (startDate, targetDate) => {
  const start = formatDate(startDate)
  const end = formatDate(targetDate)
  if (start && end) return `${start} - ${end}`
  if (start) return `${start} - ...`
  if (end) return `... - ${end}`
  return 'Start date - End date'
}

const getLeadName = (leadId) => projectMembers.value.find(member => member.id === leadId)?.name || 'No lead'
const getLeadAvatar = (leadId) => projectMembers.value.find(member => member.id === leadId)?.avatar || null

const getAvatarBg = (name) => {
  if (!name || name === 'No lead' || name === 'Unassigned') return '#64748b'
  const colors = ['#3b82f6', '#10b981', '#fbbf24', '#ec4899', '#8b5cf6', '#06b6d4', '#f97316']
  let hash = 0
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash)
  }
  const index = Math.abs(hash) % colors.length
  return colors[index]
}

const normalizeModule = (module) => ({
  id: module.id,
  name: module.name,
  description: module.description || '',
  statusKey: getStatusKey(module.status),
  statusRaw: module.status || 'Backlog',
  leadId: module.leadId || null,
  leadName: module.leadName || getLeadName(module.leadId),
  startDate: module.startDate || null,
  targetDate: module.targetDate || null,
  taskIds: Array.isArray(module.taskIds) ? module.taskIds : [],
  issueCount: module.issueCount ?? 0,
  doneIssueCount: module.doneIssueCount ?? 0,
  progress: module.progressPercent ?? 0,
  createdAt: module.createdAt,
  updatedAt: module.updatedAt,
  isFavorite: Boolean(module.isFavorite)
})

const activeModules = computed(() => modules.value.filter(module => module.statusKey !== 'disabled'))

const filteredModules = computed(() => {
  let list = activeModules.value
  if (activeFilters.value.length > 0) {
    list = list.filter(module => {
      return activeFilters.value.every(f => {
        const val = getStatusLabel(module.statusKey)
        const isMatch = val.toLowerCase() === f.value.toLowerCase()
        return f.operator === 'is' ? isMatch : !isMatch
      })
    })
  }
  return list
})

const disabledModules = computed(() => modules.value.filter(module => module.statusKey === 'disabled'))

const groupedModules = computed(() =>
  statusOptions
    .map(option => ({
      ...option,
      items: filteredModules.value.filter(module => module.statusKey === option.key)
    }))
    .filter(group => group.items.length > 0)
)

const canLoadMore = computed(() => modulePagination.value.hasNextPage)
const totalLoaded = computed(() => filteredModules.value.length)

const buildModuleParams = (page) => ({
  page,
  pageSize: modulePagination.value.pageSize,
  search: moduleSearch.value || undefined,
  sortBy: sortBy.value,
  sortDirection: sortDirection.value,
  includeDisabled: true
})

const fetchMembers = async () => {
  if (!props.projectId) return
  try {
    const res = await axiosClient.get(`/projects/${props.projectId}/members`)
    const raw = res.data?.data || res.data || []
    projectMembers.value = (Array.isArray(raw) ? raw : []).map(member => ({
      id: member.userId || member.id,
      name: member.fullName || member.name || member.userName || member.email || 'Unknown',
      avatar: (member.fullName || member.name || member.userName || member.email || 'U').substring(0, 1).toUpperCase()
    }))
  } catch (error) {
    console.error('[ModulesTab] Failed to fetch members', error)
  }
}

const fetchProjectTasks = async () => {
  if (!props.projectId) return
  loadingTasks.value = true
  try {
    const res = await axiosClient.get(`/projects/${props.projectId}/WorkTasks`)
    const raw = res.data?.data || []
    projectTasks.value = raw.map(task => ({
      id: task.id,
      title: task.title || 'Untitled',
      statusName: task.statusName || task.status || 'Backlog'
    }))
  } catch (error) {
    console.error('[ModulesTab] Failed to fetch project tasks', error)
  } finally {
    loadingTasks.value = false
  }
}

const fetchModules = async ({ page = 1, append = false } = {}) => {
  if (!props.projectId) return

  if (append) {
    loadingMoreModules.value = true
  } else {
    loadingModules.value = true
  }

  try {
    const res = await axiosClient.get(`/projects/${props.projectId}/modules`, {
      params: buildModuleParams(page)
    })

    const incomingModules = (res.data?.data || []).map(normalizeModule)
    modules.value = append ? [...modules.value, ...incomingModules] : incomingModules

    const pagination = res.data?.pagination || {}
    modulePagination.value = {
      page: pagination.page || page,
      pageSize: pagination.pageSize || modulePagination.value.pageSize,
      totalCount: pagination.totalCount || incomingModules.length,
      totalPages: pagination.totalPages || 0,
      hasPreviousPage: Boolean(pagination.hasPreviousPage),
      hasNextPage: Boolean(pagination.hasNextPage)
    }
  } catch (error) {
    console.error('[ModulesTab] Failed to fetch modules', error)
    if (!append) {
      modules.value = []
    }
  } finally {
    loadingModules.value = false
    loadingMoreModules.value = false
  }
}

const refreshModules = async () => {
  await fetchModules({ page: 1, append: false })
}

const loadMoreModules = async () => {
  if (!canLoadMore.value) return
  await fetchModules({ page: modulePagination.value.page + 1, append: true })
}

const openModuleTaskView = (module) => {
  router.push({
    path: buildSpacePath(props.projectId, 'work-items'),
    query: {
      tab: 'spreadsheet',
      moduleId: module.id,
      moduleName: module.name
    }
  })
}

const updateModuleStatus = async (module, newStatusKey) => {
  const config = statusConfig[newStatusKey]
  if (!config) return

  try {
    await axiosClient.put(`/projects/${props.projectId}/modules/${module.id}`, {
      name: module.name,
      description: module.description,
      status: config.label,
      leadId: module.leadId,
      taskIds: module.taskIds,
      startDate: module.startDate,
      targetDate: module.targetDate
    })

    module.statusKey = newStatusKey
    module.statusRaw = config.label
    ElMessage.success(t('modules.statusUpdated', { status: config.label }))
  } catch (error) {
    console.error('[ModulesTab] Failed to update status', error)
    ElMessage.error(t('modules.statusUpdateFailed', 'Failed to update status'))
  }
}

const updateModuleDateRange = async (module, value) => {
  const [startDate, targetDate] = Array.isArray(value) ? value : []

  try {
    await axiosClient.put(`/projects/${props.projectId}/modules/${module.id}`, {
      name: module.name,
      description: module.description,
      status: module.statusRaw,
      leadId: module.leadId,
      taskIds: module.taskIds,
      startDate: startDate || null,
      targetDate: targetDate || null
    })

    module.startDate = startDate || null
    module.targetDate = targetDate || null
    rowCalendarModId.value = null
    ElMessage.success(t('modules.dateRangeUpdated', 'Date range updated'))
  } catch (error) {
    console.error('[ModulesTab] Failed to update date range', error)
    ElMessage.error(t('modules.dateRangeUpdateFailed', 'Failed to update date range'))
  }
}

const deleteModule = async (module) => {
  try {
    await ElMessageBox.confirm(
      `Disable "${module.name}" and move it to the restore list?`,
      'Disable Module',
      { confirmButtonText: 'Disable', cancelButtonText: 'Cancel', type: 'warning' }
    )
    await axiosClient.delete(`/projects/${props.projectId}/modules/${module.id}`)
    await refreshModules()
    ElMessage.success(t('modules.disabledSuccess', 'Module disabled'))
  } catch (error) {
    if (error !== 'cancel') {
      console.error('[ModulesTab] Failed to delete module', error)
      ElMessage.error(t('modules.disabledFailed', 'Failed to disable module'))
    }
  }
}

const restoreModule = async (module) => {
  try {
    await axiosClient.put(`/projects/${props.projectId}/modules/${module.id}`, {
      name: module.name,
      description: module.description,
      status: 'Backlog',
      leadId: module.leadId,
      taskIds: module.taskIds,
      startDate: module.startDate,
      targetDate: module.targetDate
    })
    await refreshModules()
    ElMessage.success(t('modules.restoredSuccess', 'Module restored'))
  } catch (error) {
    console.error('[ModulesTab] Failed to restore module', error)
    ElMessage.error(t('modules.restoredFailed', 'Failed to restore module'))
  }
}

const toggleFavorite = (module) => {
  module.isFavorite = !module.isFavorite
}

const openCreateModal = () => {
  isEditing.value = false
  editingModuleId.value = null
  form.value = {
    name: '',
    description: '',
    status: 'Backlog',
    leadId: null,
    taskIds: [],
    dateRange: []
  }
  showCreateModal.value = true
}

const editModule = (module) => {
  isEditing.value = true
  editingModuleId.value = module.id
  form.value = {
    name: module.name,
    description: module.description,
    status: module.statusRaw,
    leadId: module.leadId,
    taskIds: [...module.taskIds],
    dateRange: module.startDate || module.targetDate ? [module.startDate, module.targetDate] : []
  }
  showCreateModal.value = true
}

const submitModule = async () => {
  if (!form.value.name.trim()) {
    ElMessage.warning(t('modules.nameRequired', 'Module name is required'))
    return
  }

  const [startDate, targetDate] = form.value.dateRange || []
  const payload = {
    name: form.value.name.trim(),
    description: form.value.description?.trim() || '',
    status: form.value.status,
    leadId: form.value.leadId,
    taskIds: form.value.taskIds,
    startDate: startDate || null,
    targetDate: targetDate || null
  }

  try {
    if (isEditing.value && editingModuleId.value) {
      await axiosClient.put(`/projects/${props.projectId}/modules/${editingModuleId.value}`, payload)
      ElMessage.success(t('modules.updatedSuccess', 'Module updated'))
    } else {
      await axiosClient.post(`/projects/${props.projectId}/modules`, payload)
      ElMessage.success(t('modules.createdSuccess', 'Module created'))
    }

    showCreateModal.value = false
    await refreshModules()
  } catch (error) {
    console.error('[ModulesTab] Failed to save module', error)
    ElMessage.error(t('modules.saveFailed', 'Failed to save module'))
  }
}

watch(
  () => props.projectId,
  async () => {
    modules.value = []
    await Promise.all([fetchMembers(), fetchProjectTasks(), refreshModules()])
  },
  { immediate: true }
)

watch([moduleSearch, sortBy, sortDirection], async () => {
  await refreshModules()
})

const unsubscribeAdminRealtime = subscribeAdminRealtime(async ({ type, payload }) => {
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
    await Promise.all([fetchMembers(), fetchProjectTasks(), refreshModules()])
  }
})

onMounted(() => {
  document.addEventListener('click', handleOutsideClick)
})

onUnmounted(() => {
  unsubscribeAdminRealtime?.()
  document.removeEventListener('click', handleOutsideClick)
})

</script>

<template>
  <ProjectPageContainer>
    <ProjectPageHeader 
        icon="fa-solid fa-cubes" 
        :title="t('shell.modules', 'Modules')" 
        :description="t('modules.description', 'Organize related work items into large initiatives')"
      >
        <template #actions>
          <button class="nexus-btn-outlined" type="button" @click="showRestoreModal = true">{{ t('modules.restore', 'Restore') }}</button>
          <button class="nexus-btn-primary" @click="openCreateModal"><i class="fa-solid fa-plus"></i> {{ t('modules.addModule', 'Add Module') }}</button>
        </template>
      </ProjectPageHeader>

      <ProjectPageToolbar
        :showSearch="true"
        v-model:searchQuery="moduleSearch"
        :searchPlaceholder="t('modules.searchPlaceholder', 'Search modules...')"
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
                :fields="moduleFilterFields"
                :operators="moduleOperators"
                :custom-value-meta="customModuleValueMeta"
                :active="showFilterDropdown"
              />
            </div>
          </div>
        </template>
        <template #sort>
          <ToolbarSortMenu v-model="sortBy" v-model:direction="sortDirection" label="Sort modules" :options="moduleSortOptions" />
        </template>

        <template #left>
          <div class="view-toggles">
            <button class="toggle-btn" :class="{ active: viewMode === 'list' }" @click="viewMode = 'list'" :title="t('modules.listView', 'List view')"><i class="fa-solid fa-bars"></i></button>
            <button class="toggle-btn" :class="{ active: viewMode === 'grid' }" @click="viewMode = 'grid'" :title="t('modules.gridView', 'Grid view')"><i class="fa-solid fa-border-all"></i></button>
            <button class="toggle-btn" :class="{ active: viewMode === 'status' }" @click="viewMode = 'status'" :title="t('modules.statusView', 'Status view')"><i class="fa-solid fa-table-list"></i></button>
          </div>
        </template>
      </ProjectPageToolbar>

    <ProjectEmptyState 
      v-if="!loadingModules && filteredModules.length === 0"
      icon="fa-solid fa-cubes"
      :title="t('modules.noModulesFound', 'No modules found')"
      :description="t('modules.noModulesFoundDesc', 'Create a module, adjust the status, then assign work items into it.')"
    >
      <template #action>
        <button class="empty-spaces-btn" type="button" @click="openCreateModal">
          <i class="fa-solid fa-plus"></i> {{ t('modules.addModule', 'Add Module') }}
        </button>
      </template>
    </ProjectEmptyState>

    <div v-show="loadingModules || filteredModules.length > 0" class="modules-body" v-loading="loadingModules">

      <!-- List View Mode -->
      <div v-if="viewMode === 'list'" class="modules-list">
        <div class="module-row" v-for="module in filteredModules" :key="module.id" @click="openModuleTaskView(module)">
          <div class="mr-left">
            <div class="m-progress-ring">{{ Math.round(module.progress) }}%</div>
            <div class="module-copy">
              <div class="m-title">{{ module.name }}</div>
              <div class="m-subtitle">
                {{ t('modules.tasksCompletedCount', { done: module.doneIssueCount, total: module.issueCount }) }}
              </div>
            </div>
          </div>

          <div class="mr-right" @click.stop>
            <el-popover placement="bottom-end" :width="280" trigger="click" @show="rowCalendarModId = module.id">
              <template #reference>
                <button class="m-date" type="button">
                  <i class="fa-regular fa-calendar"></i>
                  {{ formatDateRange(module.startDate, module.targetDate) === 'Start date - End date' ? t('modules.dateRangePlaceholder', 'Start date - End date') : formatDateRange(module.startDate, module.targetDate) }}
                </button>
              </template>
              <div class="date-editor">
                <el-date-picker
                  :model-value="[module.startDate, module.targetDate]"
                  type="daterange"
                  :start-placeholder="t('modules.startDate', 'Start date')"
                  :end-placeholder="t('modules.targetDate', 'Target date')"
                  value-format="YYYY-MM-DDTHH:mm:ss.SSS[Z]"
                  @change="updateModuleDateRange(module, $event)"
                />
              </div>
            </el-popover>

            <el-dropdown trigger="click" @command="(key) => updateModuleStatus(module, key)">
              <button
                class="m-status-chip"
                type="button"
                :style="{ background: statusConfig[module.statusKey]?.bg, color: statusConfig[module.statusKey]?.color }"
              >
                <span class="status-dot" :style="{ backgroundColor: statusConfig[module.statusKey]?.color }"></span>
                {{ getStatusLabel(module.statusKey) }}
              </button>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item v-for="status in statusOptions" :key="status.key" :command="status.key">
                    {{ status.label }}
                  </el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>

            <div 
              class="m-avatar" 
              :class="{ 'has-lead': getLeadAvatar(module.leadId) }" 
              :title="getLeadName(module.leadId)"
              :style="{ backgroundColor: getLeadAvatar(module.leadId) ? getAvatarBg(getLeadName(module.leadId)) : 'transparent' }"
            >
              <span v-if="getLeadAvatar(module.leadId)">{{ getLeadAvatar(module.leadId) }}</span>
              <i v-else class="fa-solid fa-user"></i>
            </div>

            <el-dropdown trigger="click">
              <button class="icon-action m-icon"><i class="fa-solid fa-ellipsis"></i></button>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item @click="editModule(module)">{{ t('modules.edit', 'Edit') }}</el-dropdown-item>
                  <el-dropdown-item @click="openModuleTaskView(module)">{{ t('common.open', 'Open') }}</el-dropdown-item>
                  <el-dropdown-item @click="deleteModule(module)">{{ t('modules.delete', 'Delete') }}</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </div>
        </div>
      </div>

      <!-- Grid View Mode -->
      <div v-else-if="viewMode === 'grid'" class="module-grid">
        <article class="grid-card" v-for="module in filteredModules" :key="module.id" @click="openModuleTaskView(module)">

          
          <div class="grid-card-header">
            <div class="grid-card-title-wrap">
              <h3 class="grid-title" :title="module.name">{{ module.name }}</h3>
              <p class="grid-desc">{{ module.description || t('dashboard.noDescription') }}</p>
            </div>
            <button class="card-edit-btn" @click.stop="editModule(module)">
              <i class="fa-solid fa-pen-to-square"></i>
            </button>
          </div>

          <div class="grid-progress-section">
            <div class="progress-bar-wrapper">
              <div class="grid-progress-bar">
                <span class="progress-fill" :style="{ width: `${Math.min(Math.max(module.progress, 0), 100)}%` }"></span>
              </div>
              <span class="progress-percentage">{{ Math.round(module.progress) }}%</span>
            </div>
          </div>

          <div class="grid-meta-info">
            <div class="meta-row">
              <span class="meta-pill text-xs">
                <i class="fa-regular fa-square-check text-green-400 mr-1.5"></i>
                <strong>{{ module.doneIssueCount }}</strong>/{{ module.issueCount }} {{ t('modules.tasksDone', 'tasks done') }}
              </span>
              <span class="meta-pill text-xs">
                <i class="fa-regular fa-calendar text-sky-400 mr-1.5"></i>
                {{ formatDateRange(module.startDate, module.targetDate) }}
              </span>
            </div>
          </div>

          <div class="grid-card-footer">
            <span class="status-badge" :style="{ color: statusConfig[module.statusKey]?.color, backgroundColor: statusConfig[module.statusKey]?.bg }">
              <span class="status-dot" :style="{ backgroundColor: statusConfig[module.statusKey]?.color }"></span>
              {{ statusConfig[module.statusKey]?.label }}
            </span>
            <button class="btn-open-module" @click.stop="openModuleTaskView(module)">
              Open <i class="fa-solid fa-arrow-right-long text-xs ml-1"></i>
            </button>
          </div>
        </article>
      </div>

      <!-- Status Grouped View Mode -->
      <div v-else class="status-groups">
        <section class="status-group" v-for="group in groupedModules" :key="group.key">
          <header class="status-group-header">
            <div class="status-group-title">
              <i :class="group.icon" :style="{ color: group.color }"></i>
              <span>{{ getStatusLabel(group.key) }}</span>
            </div>
            <span class="status-group-count">{{ group.items.length }}</span>
          </header>

          <div class="status-group-list">
            <div class="status-row" v-for="module in group.items" :key="module.id" @click="openModuleTaskView(module)">
              <div class="status-row-info">
                <div class="m-title">{{ module.name }}</div>
                <div class="m-subtitle">{{ module.doneIssueCount }} / {{ module.issueCount }} {{ t('modules.tasksDone', 'tasks done') }}</div>
              </div>
              <div class="status-row-actions" @click.stop>
                <button class="card-edit-btn" @click="editModule(module)">
                  <i class="fa-solid fa-pen-to-square"></i>
                </button>
              </div>
            </div>
          </div>
        </section>
      </div>

      <div v-if="canLoadMore" class="load-more-wrap">
        <button class="load-more-btn" :disabled="loadingMoreModules" @click="loadMoreModules">
          {{ loadingMoreModules ? t('common.loading') : t('modules.loadMore', 'Load more modules') }}
        </button>
      </div>
    </div>

    <!-- Create/Edit Modal -->
    <div class="modal-overlay" v-if="showCreateModal" @click.self="showCreateModal = false">
      <div class="create-module-modal">
        <div class="cm-header">
          <h2 class="cm-title">{{ isEditing ? t('modules.editModule') : t('modules.createModule') }}</h2>
        </div>

        <div class="cm-body">
          <input v-model="form.name" class="cm-input" type="text" :placeholder="t('modules.moduleNamePlaceholder', 'Module name')" />
          <textarea v-model="form.description" class="cm-textarea" rows="4" :placeholder="t('dashboard.description')"></textarea>

          <div class="cm-grid">
            <label class="field-block">
              <span>{{ t('common.status') }}</span>
              <select v-model="form.status" class="field-select">
                <option v-for="status in statusOptions" :key="status.key" :value="status.label">{{ getStatusLabel(status.key) }}</option>
              </select>
            </label>

            <label class="field-block">
              <span>{{ t('modules.lead', 'Lead') }}</span>
              <select v-model="form.leadId" class="field-select">
                <option :value="null">{{ t('modules.noLead', 'No lead') }}</option>
                <option v-for="member in projectMembers" :key="member.id" :value="member.id">{{ member.name }}</option>
              </select>
            </label>
          </div>

          <label class="field-block">
            <span>{{ t('modules.dateRange', 'Date range') }}</span>
            <el-date-picker
              v-model="form.dateRange"
              type="daterange"
              start-placeholder="Start date"
              end-placeholder="Target date"
              value-format="YYYY-MM-DDTHH:mm:ss.SSS[Z]"
            />
          </label>

          <label class="field-block">
            <span>{{ t('modules.workItemsInModule', 'Work items in module') }}</span>
            <div class="task-picker" v-loading="loadingTasks">
              <label v-for="task in projectTasks" :key="task.id" class="task-option">
                <input v-model="form.taskIds" type="checkbox" :value="task.id" />
                <span class="task-title-text">{{ task.title }}</span>
                <small class="task-status-text">{{ task.statusName }}</small>
              </label>
            </div>
          </label>
        </div>

        <div class="cm-footer">
          <button class="cm-btn-cancel" @click="showCreateModal = false">{{ t('common.cancel') }}</button>
          <button class="cm-btn-create" @click="submitModule">{{ isEditing ? t('modules.updateModule', 'Update Module') : t('modules.createModuleBtn', 'Create Module') }}</button>
        </div>
      </div>
    </div>

    <!-- Restore Modal -->
    <div class="modal-overlay" v-if="showRestoreModal" @click.self="showRestoreModal = false">
      <div class="create-module-modal restore-modal">
        <div class="cm-header">
          <h2 class="cm-title">{{ t('modules.restoreModules', 'Restore modules') }}</h2>
        </div>

        <div class="cm-body">
          <div v-if="!disabledModules.length" class="empty-state-wrapper compact-empty-state">
            <div class="es-icon"><i class="fa-solid fa-box-archive"></i></div>
            <h3 class="es-title">{{ t('modules.noDisabledModules', 'No disabled modules') }}</h3>
            <p class="es-desc">{{ t('modules.noDisabledModulesDesc', 'Disabled modules will appear here and can only be restored from this screen.') }}</p>
          </div>

          <div v-else class="restore-modal-list">
            <div class="restore-modal-row" v-for="module in disabledModules" :key="`restore-modal-${module.id}`">
              <div class="restore-modal-copy">
                <strong>{{ module.name }}</strong>
                <span>{{ module.description || t('modules.disabledReadyToRestore', 'Disabled module ready to restore.') }}</span>
              </div>
              <button class="cm-btn-create" type="button" @click="restoreModule(module)">{{ t('modules.restore', 'Restore') }}</button>
            </div>
          </div>
        </div>

        <div class="cm-footer">
          <button class="cm-btn-cancel" @click="showRestoreModal = false">{{ t('common.cancel', 'Close') }}</button>
        </div>
      </div>
    </div>
  </ProjectPageContainer>
</template>

<style scoped>
.plane-modules-wrapper {
  display: flex;
  flex-direction: column;
  min-height: calc(100vh - 120px);
  background: var(--color-bg);
  color: var(--color-text-primary);
  font-family: 'Inter', system-ui, sans-serif;
}

.modules-toolbar-meta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 24px;
  color: var(--color-text-secondary);
  font-size: 12px;
  border-bottom: 1px solid var(--color-border);
}

.mr-right {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-shrink: 0;
}

.filter-action,
.primary-action,
.view-btn,
.icon-action,
.load-more-btn,
.cm-btn-cancel,
.cm-btn-create,
.mini-link {
  cursor: pointer;
}

.filter-action,
.primary-action,
.load-more-btn,
.cm-btn-cancel,
.cm-btn-create {
  padding: 8px 12px;
  font-size: 13px;
}

.view-toggles {
  padding: 2px;
  border-radius: 6px;
  background: var(--color-surface);
}

.view-btn {
  width: 34px;
  height: 30px;
}

.view-btn.active {
  background: var(--color-border);
  color: var(--color-text-primary);
}

.primary-action {
  background: #0ea5e9;
  border: none;
  color: #ffffff;
}

.modules-body {
  padding: 24px;
  flex: 1;
  overflow-y: auto;
}

.modules-list,
.status-groups {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

/* List Row Redesign */
.module-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  padding: 16px 20px;
  border: 1px solid var(--color-border);
  border-radius: 12px !important;
  background: var(--color-surface);
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  cursor: pointer;
}

.module-row:hover {
  border-color: var(--color-accent);
  background: var(--color-surface-hover);
  transform: translateX(4px);
}

.mr-left {
  display: flex;
  align-items: center;
  gap: 16px;
  min-width: 0;
}

.module-copy {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 2px;
}

.m-progress-ring {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  border: 2.5px solid var(--color-border);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 750;
  color: var(--color-text-primary);
  background: var(--color-bg);
  flex-shrink: 0;
}

.m-title {
  font-size: 14.5px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.m-subtitle {
  font-size: 12px;
  color: var(--color-text-muted);
}

.m-date {
  background: rgba(0, 0, 0, 0.01);
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  padding: 6px 12px;
  border-radius: 8px !important;
  font-size: 12px;
  font-weight: 550;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
  transition: all 0.2s;
}

[data-theme='dark'] .m-date {
  background: rgba(255, 255, 255, 0.01);
}

.m-date:hover {
  background: var(--color-surface-hover);
  border-color: var(--color-border-hover);
  color: var(--color-text-primary);
}

.m-status-chip {
  padding: 5px 12px;
  border-radius: 999px !important;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.02em;
  cursor: pointer;
  transition: all 0.2s;
  border: none;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.m-status-chip:hover {
  filter: brightness(1.1);
}

.m-avatar {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 11px;
  font-weight: 700;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  border: 1.5px solid var(--color-surface);
  cursor: pointer;
}

.m-avatar:not(.has-lead) {
  border: 1.5px dashed var(--color-text-disabled);
  background: transparent;
  color: var(--color-text-muted);
}

.icon-action {
  background: transparent;
  border: none;
  color: var(--color-text-muted);
  cursor: pointer;
  font-size: 14px;
  border-radius: 8px !important;
  padding: 6px 8px;
}

.icon-action:hover {
  color: var(--color-text-primary);
  background: var(--color-surface-hover);
}

/* Grid Card View */
.module-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
  gap: 24px;
  width: 100%;
}

.grid-card {
  border: 1px solid var(--color-border);
  border-radius: 16px !important;
  background: var(--color-surface);
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 20px;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  box-shadow: var(--shadow-sm);
  position: relative;
  overflow: hidden;
  cursor: pointer;
}

.grid-card:hover {
  transform: translateY(-4px);
  border-color: var(--color-accent);
  box-shadow: var(--shadow-lg);
}



.grid-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
  position: relative;
  z-index: 1;
}

.grid-card-title-wrap {
  flex-grow: 1;
  min-width: 0;
}

.grid-title {
  font-size: 16px;
  font-weight: 800;
  color: var(--color-text-primary);
  margin: 0 0 6px 0;
  line-height: 1.3;
}

.grid-desc {
  font-size: 13px;
  color: var(--color-text-muted);
  line-height: 1.5;
  margin: 0;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  text-overflow: ellipsis;
  min-height: 38px;
}

.card-edit-btn {
  background: transparent;
  border: 1px solid var(--color-border);
  color: var(--color-text-muted);
  width: 32px;
  height: 32px;
  border-radius: 8px !important;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
  flex-shrink: 0;
}

.card-edit-btn:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
  border-color: var(--color-border-hover);
}

.grid-progress-section {
  position: relative;
  z-index: 1;
}

.progress-bar-wrapper {
  display: flex;
  align-items: center;
  gap: 12px;
}

.grid-progress-bar {
  flex-grow: 1;
  height: 8px;
  background: var(--color-border);
  border-radius: 999px;
  overflow: hidden;
}

.progress-fill {
  display: block;
  height: 100%;
  background: linear-gradient(90deg, var(--color-accent) 0%, var(--color-accent-hover) 100%);
  border-radius: 999px;
  transition: width 0.6s cubic-bezier(0.4, 0, 0.2, 1);
}

.progress-percentage {
  font-size: 12px;
  font-weight: 750;
  color: var(--color-text-primary);
  min-width: 32px;
  text-align: right;
}

.grid-meta-info {
  position: relative;
  z-index: 1;
}

.meta-row {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.meta-pill {
  display: inline-flex;
  align-items: center;
  color: var(--color-text-secondary);
  font-size: 12px;
}

.grid-card-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  position: relative;
  z-index: 1;
  margin-top: 4px;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 12px;
  border-radius: 999px !important;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.02em;
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
}

.btn-open-module {
  background: transparent;
  border: none;
  color: var(--color-accent);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  transition: color 0.2s, transform 0.2s;
  padding: 0;
}

.btn-open-module:hover {
  color: var(--color-accent-hover);
  transform: translateX(2px);
}

/* Status Group View */
.status-group {
  border: 1px solid var(--color-border);
  border-radius: 14px !important;
  background: var(--color-surface);
  padding: 20px;
  box-shadow: var(--shadow-sm);
  margin-bottom: 20px;
}

.status-group-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--color-border);
  margin-bottom: 14px;
}

.status-group-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  font-weight: 750;
  color: var(--color-text-primary);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.status-group-count {
  font-size: 12px;
  font-weight: 700;
  color: var(--color-text-muted);
  background: var(--color-border);
  padding: 2px 8px;
  border-radius: 10px;
}

.status-group-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.status-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border: 1px solid var(--color-border);
  border-radius: 10px !important;
  background: rgba(0, 0, 0, 0.01);
  transition: all 0.2s;
  cursor: pointer;
}

[data-theme='dark'] .status-row {
  background: rgba(255, 255, 255, 0.01);
}

.status-row:hover {
  border-color: var(--color-accent);
  background: var(--color-surface-hover);
  transform: translateX(4px);
}

.status-row-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.status-row-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.load-more-wrap {
  display: flex;
  justify-content: center;
  margin-top: 20px;
}

.empty-state-wrapper {
  min-height: 50vh;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
}

.es-icon {
  font-size: 40px;
  color: var(--color-text-muted);
  margin-bottom: 16px;
}

.es-title {
  font-size: 18px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin-bottom: 6px;
}

.es-desc {
  font-size: 13px;
  color: var(--color-text-secondary);
  max-width: 320px;
  line-height: 1.5;
}

.empty-spaces-btn {
}

/* Modal Redesigns */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  backdrop-filter: blur(4px);
}

.create-module-modal {
  width: min(680px, calc(100vw - 32px));
  max-height: calc(100vh - 48px);
  overflow: auto;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 16px !important;
  box-shadow: var(--shadow-xl);
  display: flex;
  flex-direction: column;
}

.cm-header {
  padding: 24px 24px 16px;
  border-bottom: 1px solid var(--color-border);
}

.cm-title {
  font-size: 18px;
  font-weight: 750;
  color: var(--color-text-primary);
  margin: 0;
}

.cm-body {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 20px;
  overflow-y: auto;
}

.cm-input,
.cm-textarea,
.field-select {
  width: 100%;
  background-color: var(--input-bg) !important;
  color: var(--text-primary) !important;
  border: 2px solid var(--border-color) !important;
  border-radius: 8px !important;
  padding: 10px 12px !important;
  font-size: 14px !important;
  outline: none;
  transition: border-color 0.2s;
  box-shadow: none !important;
}

.cm-input:focus,
.cm-textarea:focus,
.field-select:focus {
  border-color: var(--color-accent) !important;
}

.cm-textarea {
  resize: none;
}

.cm-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

@media (max-width: 576px) {
  .cm-grid {
    grid-template-columns: 1fr;
  }
}

.field-block {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 11px;
  font-weight: 700;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.field-block span {
  margin-bottom: 2px;
}

.task-picker {
  max-height: 200px;
  overflow-y: auto;
  padding: 8px;
  border-radius: 10px;
  border: 2px solid var(--border-color);
  background: var(--color-bg);
}

.task-option {
  display: grid;
  grid-template-columns: auto 1fr auto;
  gap: 12px;
  align-items: center;
  padding: 10px 12px;
  border-radius: 8px;
  transition: background 0.15s;
  cursor: pointer;
  margin-bottom: 4px;
}

.task-option:hover {
  background: var(--color-surface-hover);
}

.task-option input[type="checkbox"] {
  accent-color: var(--color-accent);
  width: 16px;
  height: 16px;
  cursor: pointer;
}

.task-title-text {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.task-status-text {
  font-size: 11px;
  color: var(--color-text-secondary);
  background: var(--color-border);
  padding: 2px 8px;
  border-radius: 4px;
  font-weight: 600;
  text-transform: capitalize;
}

.cm-footer {
  padding: 16px 24px;
  border-top: 1px solid var(--color-border);
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  background: var(--color-surface-hover);
  border-bottom-left-radius: 16px;
  border-bottom-right-radius: 16px;
}

.cm-btn-cancel {
  background: transparent;
  border: 1px solid var(--color-border);
  border-radius: 8px !important;
  padding: 8px 16px;
  color: var(--color-text-secondary);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.cm-btn-cancel:hover {
  background: var(--color-border);
  color: var(--color-text-primary);
}

.cm-btn-create {
  background: var(--color-accent);
  border: none;
  border-radius: 8px !important;
  padding: 8px 16px;
  color: #ffffff;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: opacity 0.2s;
}

.cm-btn-create:hover {
  opacity: 0.9;
}

/* Restore Modal styles */
.restore-modal {
  width: min(640px, calc(100vw - 32px));
}

.restore-modal-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.restore-modal-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 14px 16px;
  border: 1px solid var(--color-border);
  border-radius: 12px !important;
  background: rgba(0, 0, 0, 0.01);
  transition: all 0.2s;
}

[data-theme='dark'] .restore-modal-row {
  background: rgba(255, 255, 255, 0.01);
}

.restore-modal-row:hover {
  background: var(--color-surface-hover);
}

.restore-modal-copy {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.restore-modal-copy strong {
  font-size: 14px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.restore-modal-copy span {
  color: var(--color-text-muted);
  font-size: 12px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.compact-empty-state {
  margin: 0;
  padding: 32px 0;
}

@media (max-width: 900px) {
  .modules-view-header,
  .modules-toolbar-meta {
    flex-direction: column;
    align-items: stretch;
  }

  .mr-right,
  .module-row {
    flex-wrap: wrap;
  }
}

/* Compact density */
.modules-page {
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
.view-btn {
  min-height: 32px !important;
  border-radius: 8px !important;
  padding: 6px 10px !important;
  font-size: 12.5px !important;
}

.modules-content {
  padding: 16px var(--sa-page-x, 24px) 26px !important;
}

.module-row {
  padding: 12px 14px !important;
  border-radius: 8px !important;
  min-height: 68px !important;
}

.module-title {
  font-size: 13.5px !important;
}

.module-meta,
.module-subtitle {
  font-size: 11.5px !important;
}

.module-progress-ring {
  width: 38px !important;
  height: 38px !important;
}

/* Polished module modal */
.modal-overlay {
  padding: 20px !important;
  background: rgba(2, 6, 23, 0.68) !important;
  backdrop-filter: blur(10px) saturate(120%) !important;
  z-index: 2000 !important;
}

.create-module-modal {
  width: min(760px, calc(100vw - 28px)) !important;
  max-height: min(820px, calc(100vh - 28px)) !important;
  overflow: hidden !important;
  border-radius: 12px !important;
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--color-surface) 94%, var(--color-accent) 6%), var(--color-surface)) !important;
  border: 1px solid color-mix(in srgb, var(--color-border) 84%, var(--color-accent) 16%) !important;
  box-shadow: 0 24px 70px rgba(2, 8, 23, 0.38) !important;
}

.cm-header {
  position: relative;
  padding: 16px 22px 14px !important;
  border-bottom: 1px solid var(--color-border) !important;
  background: color-mix(in srgb, var(--color-surface-hover) 72%, transparent) !important;
}

.cm-header::before {
  content: "";
  position: absolute;
  left: 22px;
  right: 22px;
  top: 0;
  height: 3px;
  border-radius: 999px;
  background: linear-gradient(90deg, var(--color-accent), #22c55e, #facc15);
}

.cm-title {
  font-size: 17px !important;
  line-height: 1.25 !important;
  color: var(--color-text-primary) !important;
}

.cm-body {
  padding: 18px 22px !important;
  gap: 14px !important;
  overflow: auto !important;
}

.cm-input,
.cm-textarea,
.field-select {
  min-height: 38px !important;
  border: 1px solid var(--color-border) !important;
  border-radius: 8px !important;
  background: var(--color-input-bg) !important;
  color: var(--color-text-primary) !important;
  font-size: 13px !important;
}

.cm-input::placeholder,
.cm-textarea::placeholder {
  color: var(--color-text-muted) !important;
}

.cm-input:focus,
.cm-textarea:focus,
.field-select:focus {
  border-color: var(--color-accent) !important;
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-accent) 18%, transparent) !important;
}

.cm-textarea {
  min-height: 82px !important;
}

.field-block {
  gap: 7px !important;
  color: var(--color-text-secondary) !important;
  letter-spacing: 0.045em !important;
}

.field-block :deep(.el-date-editor) {
  width: 100% !important;
  height: 40px !important;
  border: 1px solid var(--color-border) !important;
  border-radius: 8px !important;
  background: var(--color-input-bg) !important;
  box-shadow: none !important;
}

.field-block :deep(.el-date-editor:hover),
.field-block :deep(.el-date-editor.is-active) {
  border-color: var(--color-accent) !important;
}

.field-block :deep(.el-range-input) {
  background: transparent !important;
  color: var(--color-text-primary) !important;
  font-size: 13px !important;
}

.field-block :deep(.el-range-input::placeholder),
.field-block :deep(.el-range-separator),
.field-block :deep(.el-range__icon) {
  color: var(--color-text-muted) !important;
}

.task-picker {
  max-height: 224px !important;
  border: 1px solid var(--color-border) !important;
  border-radius: 10px !important;
  background: color-mix(in srgb, var(--color-bg) 88%, var(--color-surface) 12%) !important;
  padding: 8px !important;
}

.task-option {
  min-height: 44px !important;
  padding: 8px 10px !important;
  margin-bottom: 6px !important;
  border: 1px solid transparent !important;
  border-radius: 8px !important;
  background: transparent !important;
}

.task-option:hover {
  background: var(--color-surface-hover) !important;
  border-color: var(--color-border) !important;
}

.task-title-text {
  min-width: 0 !important;
  font-size: 13px !important;
  color: var(--color-text-primary) !important;
}

.task-status-text {
  border-radius: 999px !important;
  background: color-mix(in srgb, var(--color-accent) 14%, var(--color-surface-hover) 86%) !important;
  color: var(--color-text-secondary) !important;
  padding: 3px 9px !important;
  white-space: nowrap !important;
}

.cm-footer {
  padding: 14px 22px !important;
  border-top: 1px solid var(--color-border) !important;
  background: color-mix(in srgb, var(--color-surface-hover) 76%, var(--color-surface) 24%) !important;
  border-bottom-left-radius: 12px !important;
  border-bottom-right-radius: 12px !important;
}

.cm-btn-cancel,
.cm-btn-create {
  min-height: 38px !important;
  border-radius: 8px !important;
  padding: 8px 16px !important;
  font-size: 13px !important;
}

@media (max-width: 760px) {
  .nexus-project-header {
    align-items: stretch !important;
    flex-direction: column !important;
    padding: 10px 12px !important;
  }

  .modules-content {
    padding: 12px !important;
  }

  .modal-overlay {
    align-items: flex-end !important;
    padding: 10px !important;
  }

  .create-module-modal {
    width: 100% !important;
    max-height: calc(100vh - 20px) !important;
  }

  .cm-body {
    padding: 14px !important;
  }

  .cm-header,
  .cm-footer {
    padding-left: 14px !important;
    padding-right: 14px !important;
  }

  .cm-grid {
    grid-template-columns: 1fr !important;
  }

  .task-option {
    grid-template-columns: auto 1fr !important;
  }

  .task-status-text {
    grid-column: 2;
    justify-self: flex-start;
  }
}
.modules-toolbar-meta {
  padding: 0 4px !important;
  margin: -2px 0 6px !important;
  font-size: 12px !important;
}

.modules-list,
.modules-grid {
  min-height: 0 !important;
  overflow: auto !important;
}

.module-row {
  min-height: 56px !important;
  padding: 10px 12px !important;
  border-radius: 10px !important;
  box-shadow: 0 8px 20px rgba(15, 23, 42, 0.05) !important;
}

.module-row:hover {
  transform: translateX(2px) !important;
}

.m-progress-ring {
  width: 38px !important;
  height: 38px !important;
  font-size: 12px !important;
}

.module-copy h3,
.module-title {
  font-size: 14px !important;
  line-height: 1.25 !important;
}

.modules-copy p,
.module-meta {
  font-size: 12px !important;
  line-height: 1.35 !important;
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
