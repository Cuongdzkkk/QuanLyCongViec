<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import axiosClient from '@/api/axiosClient'
import UserAvatar from '@/components/common/UserAvatar.vue'

const props = defineProps({
  tasks: { type: Array, default: () => [] },
  projectId: { type: String, default: '' },
  projectMembers: { type: Array, default: () => [] },
  serverPagination: { type: Object, default: null },
  readonly: { type: Boolean, default: false }
})

const emit = defineEmits(['task-click', 'update-task', 'create-task', 'page-change', 'page-size-change'])

const modules = ref([])
const cycles = ref([])
const assigneeSearch = ref('')
const searchQuery = ref('')
const statusFilter = ref('all')
const page = ref(1)
const pageSize = ref(25)
const displayOptionsOpen = ref(false)
const showOnlyAssigned = ref(false)
const hideDone = ref(false)
const showOnlyScheduled = ref(false)
const usesServerPagination = computed(() => Boolean(props.serverPagination))

const fetchOptions = async () => {
  if (!props.projectId) return

  const [modulesRes, cyclesRes] = await Promise.allSettled([
    axiosClient.get(`/projects/${props.projectId}/modules`),
    axiosClient.get(`/projects/${props.projectId}/sprints`)
  ])

  if (modulesRes.status === 'fulfilled') modules.value = modulesRes.value.data?.data || []
  if (cyclesRes.status === 'fulfilled') cycles.value = cyclesRes.value.data?.data || []
}

const parseLocalDate = (value) => {
  if (!value) return null
  if (value instanceof Date) return new Date(value)
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)) {
    const [year, month, day] = value.split('-').map(Number)
    return new Date(year, month - 1, day)
  }
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(value)) {
    const [year, month, day] = value.slice(0, 10).split('-').map(Number)
    return new Date(year, month - 1, day)
  }
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? null : parsed
}

const formatDate = (dateString) => {
  if (!dateString) return '-'
  const date = parseLocalDate(dateString)
  return date ? date.toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : '-'
}

const toInputDate = (value) => {
  if (!value) return ''
  const date = parseLocalDate(value)
  if (!date) return ''
  if (Number.isNaN(date.getTime())) return ''
  const year = date.getFullYear()
  const month = `${date.getMonth() + 1}`.padStart(2, '0')
  const day = `${date.getDate()}`.padStart(2, '0')
  return `${year}-${month}-${day}`
}

const toApiDate = (value) => (value ? value : null)

const getPrioIcon = (priority) => {
  if (priority === 1) return { class: 'fa-solid fa-angles-up text-red', label: 'Urgent' }
  if (priority === 2) return { class: 'fa-solid fa-chevron-up text-orange', label: 'High' }
  if (priority === 3) return { class: 'fa-solid fa-minus text-blue', label: 'Normal' }
  if (priority === 4) return { class: 'fa-solid fa-chevron-down text-muted', label: 'Low' }
  return { class: 'fa-solid fa-ban text-muted', label: 'None' }
}

const getStatusDisplay = (statusName) => {
  const status = `${statusName || ''}`.toUpperCase()
  if (status === 'DONE') return { class: 'fa-solid fa-circle-check text-green', label: 'Done' }
  if (status === 'IN PROGRESS' || status === 'INPROGRESS') return { class: 'fa-solid fa-circle-half-stroke text-orange', label: 'In Progress' }
  if (status === 'IN REVIEW' || status === 'REVIEW') return { class: 'fa-solid fa-eye text-orange', label: 'In Review' }
  if (status === 'TO DO' || status === 'TODO') return { class: 'fa-regular fa-circle text-muted', label: 'Todo' }
  return { class: 'fa-regular fa-circle-dashed text-muted', label: 'Backlog' }
}

const getStatusTone = (statusName) => {
  const status = `${statusName || ''}`.toUpperCase()
  if (status.includes('DONE') || status.includes('COMPLETE')) return 'status-done'
  if (status.includes('PROGRESS')) return 'status-progress'
  if (status.includes('REVIEW')) return 'status-review'
  if (status.includes('TO DO') || status.includes('TODO')) return 'status-todo'
  return 'status-backlog'
}

const memberId = (member) => member.userId || member.id
const memberName = (member) => member.fullName || member.name || member.email || 'Unknown'

const filteredMembers = computed(() => {
  const keyword = assigneeSearch.value.trim().toLowerCase()
  let filtered = props.projectMembers
  if (keyword) {
    filtered = props.projectMembers.filter(member => memberName(member).toLowerCase().includes(keyword))
  }
  const totalTasks = props.tasks.length || 1;
  return filtered.map(member => {
    let count = 0;
    props.tasks.forEach(task => {
      const ids = getTaskAssigneeIds(task);
      if (ids.includes(memberId(member))) {
        count++;
      }
    });
    return {
      ...member,
      taskPercentage: Math.round((count / totalTasks) * 100)
    };
  }).sort((a, b) => a.taskPercentage - b.taskPercentage);
})

const getTaskAssigneeIds = (task) => {
  if (Array.isArray(task.assigneeIds) && task.assigneeIds.length) return task.assigneeIds
  if (Array.isArray(task.assignees) && task.assignees.length) return task.assignees.map(item => item.userId || item.id).filter(Boolean)
  if (task.assignedUserId) return [task.assignedUserId]
  return []
}

const getAssigneeUser = (task) => {
  const ids = getTaskAssigneeIds(task)
  if (!ids.length) return null
  return props.projectMembers.find(item => memberId(item) === ids[0]) || { fullName: task.assigneeName || 'Unknown' }
}

const assigneeLabel = (task) => {
  const ids = getTaskAssigneeIds(task)
  if (!ids.length) return 'Assignees'
  if (ids.length > 1) return `${ids.length} assignees`
  const member = props.projectMembers.find(item => memberId(item) === ids[0])
  return member ? memberName(member) : task.assigneeName || 'Assignee'
}

const createdByLabel = (task) => {
  const creatorId = task.createdById || task.createdBy || task.reporterId
  const member = props.projectMembers.find(item => memberId(item) === creatorId)
  return member ? memberName(member) : task.createdByName || task.reporterName || '-'
}

const moduleLabel = (task) => {
  const moduleId = task.moduleId || task.moduleIds?.[0] || task.modules?.[0]?.id || task.modules?.[0]?.moduleId
  return modules.value.find(item => item.id === moduleId)?.name || task.moduleName || 'Modules'
}

const cycleLabel = (task) => cycles.value.find(item => item.id === task.sprintId)?.name || task.sprintName || 'Cycle'
const parentLabel = (task) => task.parentSequenceId || task.parentTitle || task.parentId || 'Parent'

const labelsLabel = (task) => {
  const labels = task.labels || task.labelNames || []
  return Array.isArray(labels) && labels.length ? labels.map(item => item.name || item).join(', ') : 'Labels'
}

const updateTaskTitle = (task, event) => {
  const newTitle = event.target.innerText.trim()
  if (!newTitle) {
    event.target.innerText = task.title
    return
  }
  if (newTitle !== task.title) emit('update-task', task, 'title', newTitle)
}

const updateField = (task, field, value) => {
  emit('update-task', task, field, value, task[field])
}

const updateDateField = (task, field, event) => {
  updateField(task, field, toApiDate(event.target.value))
}

const toggleTaskAssignee = (task, id) => {
  const currentIds = getTaskAssigneeIds(task)
  const nextIds = currentIds.includes(id)
    ? currentIds.filter(item => item !== id)
    : Array.from(new Set([...currentIds, id]))
  emit('update-task', task, 'assigneeIds', nextIds, currentIds)
}

const normalizedTasks = computed(() => {
  return props.tasks.filter(task => {
    const status = `${task.statusName || ''}`.toUpperCase()
    if (hideDone.value && status.includes('DONE')) return false
    if (showOnlyAssigned.value && !getTaskAssigneeIds(task).length) return false
    if (showOnlyScheduled.value && !(task.plannedStartDate || task.dueDate || task.plannedEndDate)) return false
    if (statusFilter.value !== 'all' && status !== statusFilter.value) return false

    const keyword = searchQuery.value.trim().toLowerCase()
    if (!keyword) return true

    return [
      task.title,
      task.sequenceId,
      task.description,
      assigneeLabel(task),
      moduleLabel(task),
      cycleLabel(task)
    ].some(value => `${value || ''}`.toLowerCase().includes(keyword))
  })
})

const totalPages = computed(() => usesServerPagination.value
  ? Math.max(1, Number(props.serverPagination?.totalPages) || 1)
  : Math.max(1, Math.ceil(normalizedTasks.value.length / pageSize.value)))
const pagedTasks = computed(() => {
  if (usesServerPagination.value) return normalizedTasks.value
  const start = (page.value - 1) * pageSize.value
  return normalizedTasks.value.slice(start, start + pageSize.value)
})

watch([searchQuery, statusFilter, showOnlyAssigned, hideDone, showOnlyScheduled, pageSize], () => {
  page.value = 1
})

watch(totalPages, (next) => {
  if (page.value > next) page.value = next
})

watch(
  () => [props.serverPagination?.page, props.serverPagination?.pageSize],
  ([nextPage, nextPageSize]) => {
    if (!usesServerPagination.value) return
    page.value = Number(nextPage) || 1
    pageSize.value = Number(nextPageSize) || 20
  },
  { immediate: true }
)

const changePage = (nextPage) => {
  if (nextPage < 1 || nextPage > totalPages.value || nextPage === page.value) return
  if (usesServerPagination.value) {
    emit('page-change', nextPage)
    return
  }
  page.value = nextPage
}

const changePageSize = (event) => {
  const nextPageSize = Number(event.target.value) || 20
  pageSize.value = nextPageSize
  page.value = 1
  if (usesServerPagination.value) emit('page-size-change', nextPageSize)
}

onMounted(fetchOptions)
watch(() => props.projectId, fetchOptions)
</script>

<template>
  <div class="spreadsheet-container">
    <div class="table-toolbar">
      <div class="toolbar-left">
        <input v-model="searchQuery" type="text" class="toolbar-search" placeholder="Search work items" />
        <select v-model="statusFilter" class="toolbar-select">
          <option value="all">All states</option>
          <option value="BACKLOG">Backlog</option>
          <option value="TO DO">Todo</option>
          <option value="IN PROGRESS">In Progress</option>
          <option value="IN REVIEW">In Review</option>
          <option value="DONE">Done</option>
        </select>
      </div>

      <div class="toolbar-right">
        <div class="display-options">
          <button class="toolbar-btn" type="button" @click="displayOptionsOpen = !displayOptionsOpen">Display options</button>
          <div v-if="displayOptionsOpen" class="display-options-menu">
            <label class="display-option"><input v-model="showOnlyAssigned" type="checkbox" /> Only assigned</label>
            <label class="display-option"><input v-model="hideDone" type="checkbox" /> Hide done</label>
            <label class="display-option"><input v-model="showOnlyScheduled" type="checkbox" /> Only dated</label>
          </div>
        </div>

        <select :value="pageSize" class="toolbar-select small" @change="changePageSize">
          <option :value="20">20 rows</option>
          <option :value="25">25 rows</option>
          <option :value="50">50 rows</option>
          <option :value="100">100 rows</option>
        </select>
      </div>
    </div>

    <table class="plane-table">
      <thead>
        <tr>
          <th class="sticky-work-item">Work items</th>
          <th><i class="fa-regular fa-circle-dot"></i> State</th>
          <th><i class="fa-solid fa-signal"></i> Priority</th>
          <th><i class="fa-solid fa-user-group"></i> Assignees</th>
          <th><i class="fa-regular fa-user"></i> Created by</th>
          <th><i class="fa-regular fa-calendar"></i> Start date</th>
          <th><i class="fa-solid fa-calendar-day"></i> Due date</th>
          <th><i class="fa-solid fa-table-cells-large"></i> Modules</th>
          <th><i class="fa-solid fa-arrows-spin"></i> Cycle</th>
          <th><i class="fa-solid fa-diagram-project"></i> Parent</th>
          <th><i class="fa-solid fa-tag"></i> Labels</th>
          <th>Created on</th>
          <th>Updated on</th>
        </tr>
      </thead>

      <tbody>
        <tr v-for="task in pagedTasks" :key="task.id" :class="getStatusTone(task.statusName)">
          <td class="sticky-work-item">
            <div
              class="wi-cell"
              role="button"
              tabindex="0"
              :aria-label="`Open work item ${task.sequenceId || task.id}: ${task.title}`"
              :title="task.title"
              @click="emit('task-click', task)"
              @keydown.enter.self="emit('task-click', task)"
            >
              <span class="wi-id">{{ task.sequenceId || String(task.id).slice(0, 8).toUpperCase() }}</span>
              <span
                class="wi-title"
                :contenteditable="!readonly"
                @click.stop
                @blur="updateTaskTitle(task, $event)"
                @keydown.enter.prevent="$event.target.blur()"
              >{{ task.title }}</span>
            </div>
          </td>

          <td>
            <el-dropdown trigger="click" :disabled="readonly" @command="value => updateField(task, 'statusName', value)">
              <button class="cell-btn state-badge" :class="getStatusTone(task.statusName)">
                <i :class="getStatusDisplay(task.statusName).class"></i>
                <span>{{ getStatusDisplay(task.statusName).label }}</span>
              </button>
              <template #dropdown>
                <el-dropdown-menu class="plane-dropdown">
                  <el-dropdown-item command="BACKLOG">Backlog</el-dropdown-item>
                  <el-dropdown-item command="TO DO">Todo</el-dropdown-item>
                  <el-dropdown-item command="IN PROGRESS">In Progress</el-dropdown-item>
                  <el-dropdown-item command="IN REVIEW">In Review</el-dropdown-item>
                  <el-dropdown-item command="DONE">Done</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </td>

          <td>
            <el-dropdown trigger="click" :disabled="readonly" @command="value => updateField(task, 'priority', value)">
              <button class="cell-btn priority-badge" :class="`priority-${task.priority || 0}`">
                <i :class="getPrioIcon(task.priority).class"></i>
                <span>{{ getPrioIcon(task.priority).label }}</span>
              </button>
              <template #dropdown>
                <el-dropdown-menu class="plane-dropdown">
                  <el-dropdown-item :command="1">Urgent</el-dropdown-item>
                  <el-dropdown-item :command="2">High</el-dropdown-item>
                  <el-dropdown-item :command="3">Normal</el-dropdown-item>
                  <el-dropdown-item :command="4">Low</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </td>

          <td>
            <el-popover placement="bottom" trigger="click" width="260" popper-class="plane-popover" :disabled="readonly">
              <template #reference>
                <button class="cell-btn" style="gap: 6px;">
                  <div v-if="!getTaskAssigneeIds(task).length" style="width: 18px; height: 18px; border-radius: 50%; background: #e2e8f0; color: #64748b; display: flex; align-items: center; justify-content: center; border: 1px dashed #cbd5e1; flex-shrink: 0;">
                    <i class="fa-solid fa-question" style="font-size: 9px;"></i>
                  </div>
                  <UserAvatar v-else-if="getTaskAssigneeIds(task).length === 1" :user="getAssigneeUser(task)" :size="18" :fontSize="9" />
                  <div v-else style="width: 18px; height: 18px; border-radius: 50%; background: #0c66e4; color: white; display: flex; align-items: center; justify-content: center; font-size: 9px; font-weight: bold; flex-shrink: 0;">
                    +{{ getTaskAssigneeIds(task).length }}
                  </div>
                  <span>{{ assigneeLabel(task) }}</span>
                </button>
              </template>
              <div class="popover-content" style="padding-top: 8px;">
                <input v-model="assigneeSearch" type="text" class="popover-search mb-2" placeholder="Search members" />
                <div class="popover-list">
                  <div
                    v-for="member in filteredMembers"
                    :key="memberId(member)"
                    class="popover-item flex items-center justify-between transition-colors cursor-pointer"
                    @click.stop="toggleTaskAssignee(task, memberId(member))"
                    :class="getTaskAssigneeIds(task).includes(memberId(member)) ? 'bg-green-100 hover:bg-green-200 text-green-900 border-l-4 border-green-500 rounded-sm' : 'hover:bg-gray-100'"
                  >
                    <div class="flex items-center truncate max-w-[75%] pl-2">
                      <UserAvatar :user="member" :size="22" :fontSize="10" class="mr-2" />
                      <span class="truncate" :class="getTaskAssigneeIds(task).includes(memberId(member)) ? 'font-semibold' : ''">{{ memberName(member) }}</span>
                    </div>
                    <div class="flex items-center flex-shrink-0 pr-2">
                      <span v-if="member.taskPercentage !== undefined" class="text-[11px] px-1.5 py-0.5 rounded text-gray-500">{{ member.taskPercentage }}%</span>
                    </div>
                  </div>
                </div>
              </div>
            </el-popover>
          </td>

          <td><span class="muted-text">{{ createdByLabel(task) }}</span></td>
          <td><input class="date-input" type="date" :disabled="readonly" :value="toInputDate(task.plannedStartDate || task.startDate)" @change="updateDateField(task, 'plannedStartDate', $event)" /></td>
          <td><input class="date-input" type="date" :disabled="readonly" :value="toInputDate(task.dueDate)" @change="updateDateField(task, 'dueDate', $event)" /></td>

          <td>
            <el-dropdown trigger="click" :disabled="readonly" @command="value => updateField(task, 'moduleId', value)">
              <button class="cell-btn">{{ moduleLabel(task) }}</button>
              <template #dropdown>
                <el-dropdown-menu class="plane-dropdown">
                  <el-dropdown-item :command="null">No module</el-dropdown-item>
                  <el-dropdown-item v-for="module in modules" :key="module.id" :command="module.id">{{ module.name }}</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </td>

          <td>
            <el-dropdown trigger="click" :disabled="readonly" @command="value => updateField(task, 'sprintId', value)">
              <button class="cell-btn">{{ cycleLabel(task) }}</button>
              <template #dropdown>
                <el-dropdown-menu class="plane-dropdown">
                  <el-dropdown-item :command="null">No cycle</el-dropdown-item>
                  <el-dropdown-item v-for="cycle in cycles" :key="cycle.id" :command="cycle.id">{{ cycle.name }}</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </td>

          <td><span class="muted-text">{{ parentLabel(task) }}</span></td>
          <td><span class="muted-text">{{ labelsLabel(task) }}</span></td>
          <td><span class="date-text">{{ formatDate(task.createdDate || task.createdAt) }}</span></td>
          <td><span class="date-text">{{ formatDate(task.updatedDate || task.updatedAt) }}</span></td>
        </tr>

        <tr v-if="pagedTasks.length === 0">
          <td colspan="13" class="empty-cell">
            <div class="empty-state">
              <div class="empty-icon"><i class="fa-regular fa-folder-open"></i></div>
              <strong>No work items found</strong>
              <span>Try changing search, state, or display options.</span>
            </div>
          </td>
        </tr>
      </tbody>
    </table>

    <div class="table-footer">
      <button v-if="!readonly" class="add-btn" type="button" @click="emit('create-task', { statusName: 'TO DO' })">
        <i class="fa-solid fa-plus"></i> Add work item
      </button>

      <div class="pagination">
        <button class="page-btn" type="button" aria-label="Previous page" :disabled="page <= 1" @click="changePage(page - 1)">Prev</button>
        <span>{{ page }} / {{ totalPages }}</span>
        <button class="page-btn" type="button" aria-label="Next page" :disabled="page >= totalPages" @click="changePage(page + 1)">Next</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.spreadsheet-container {
  flex: 1;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--sa-bg, var(--color-bg)) 84%, var(--color-surface) 16%), var(--sa-bg, var(--color-bg)));
  color: var(--color-text-primary);
  overflow: auto;
  border-top: 1px solid var(--color-border);
  scrollbar-color: color-mix(in srgb, var(--sa-primary, var(--color-accent)) 32%, var(--color-border)) transparent;
}

.table-toolbar,
.toolbar-left,
.toolbar-right,
.table-footer,
.pagination {
  display: flex;
  align-items: center;
}

.table-toolbar,
.table-footer {
  justify-content: space-between;
  gap: 12px;
  padding: 14px 20px;
  border-bottom: 1px solid var(--color-border);
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 94%, var(--sa-bg, var(--color-bg)) 6%), var(--color-surface));
  position: relative;
  z-index: 40;
  box-shadow: 0 1px 0 rgba(255, 255, 255, 0.55);
}

.table-footer {
  border-top: 1px solid var(--color-border);
  border-bottom: 0;
  background: color-mix(in srgb, var(--color-surface) 92%, var(--sa-bg, var(--color-bg)));
}

.toolbar-left,
.toolbar-right,
.pagination {
  gap: 10px;
}

.toolbar-search,
.toolbar-select,
.date-input,
.plane-search-input {
  border: 1px solid var(--color-border) !important;
  border-radius: 10px !important;
  background: var(--color-input-bg) !important;
  color: var(--color-text-primary) !important;
  transition: border-color 0.18s ease, box-shadow 0.18s ease, background 0.18s ease;
}

.toolbar-search:focus,
.toolbar-select:focus,
.date-input:focus,
.plane-search-input:focus {
  border-color: color-mix(in srgb, var(--sa-primary, var(--color-accent)) 72%, var(--color-border));
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--sa-primary, var(--color-accent)) 16%, transparent);
  outline: none;
}

.toolbar-search {
  width: 280px;
  min-height: 38px !important;
  height: 38px !important;
  padding: 8px 13px !important;
}

.toolbar-select {
  min-width: 124px;
  min-height: 38px !important;
  height: 38px !important;
  padding: 8px 12px !important;
}

.toolbar-select.small {
  min-width: 90px;
}

.toolbar-btn,
.page-btn {
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-surface);
  color: var(--text-secondary);
  min-height: 38px;
  padding: 8px 13px;
  cursor: pointer;
  font-weight: 700;
  transition: all 0.18s ease;
}

.toolbar-btn:hover,
.page-btn:hover:not(:disabled) {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
  border-color: var(--color-border-hover);
}

.page-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.pagination span {
  min-width: 48px;
  text-align: center;
  font-size: 13px;
  font-weight: 800;
  color: var(--color-text-primary);
}

.display-options {
  position: relative;
}

.display-options-menu {
  position: absolute;
  top: calc(100% + 8px);
  right: 0;
  z-index: var(--z-popover);
  min-width: 230px;
  padding: 10px;
  border-radius: 12px;
  border: 1px solid var(--color-border);
  background: var(--color-surface-elevated);
  box-shadow: var(--sa-shadow-sm, var(--shadow-popover)), 0 18px 50px rgb(15 23 42 / 0.12);
}

.display-option {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 10px;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 700;
  color: var(--color-text-secondary);
  cursor: pointer;
}

.display-option:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.display-option input {
  width: 15px !important;
  height: 15px !important;
  min-height: 15px !important;
  padding: 0 !important;
  border-radius: 4px !important;
  accent-color: var(--color-accent);
}

.plane-table {
  min-width: 1900px;
  border-collapse: separate;
  border-spacing: 0;
  text-align: left;
  font-size: 13.5px;
  background: var(--color-surface);
  border-top: 1px solid var(--color-border);
}

.plane-table th,
.plane-table td {
  border-bottom: 1px solid var(--color-border);
  border-right: 1px solid var(--color-border);
  background: var(--color-surface);
}

.plane-table th {
  position: static;
  top: auto;
  z-index: 15;
  padding: 14px 16px;
  color: color-mix(in srgb, var(--color-text-primary) 78%, var(--color-text-muted));
  font-size: 12px;
  font-weight: 850;
  letter-spacing: 0.015em;
  white-space: nowrap;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-table-header) 84%, var(--color-surface) 16%), var(--color-table-header));
}

.plane-table th i {
  color: var(--sa-primary, var(--color-accent));
  margin-right: 6px;
  opacity: 0.88;
}

.plane-table td {
  height: 50px;
  padding: 10px 14px;
  white-space: nowrap;
  color: var(--color-text-primary);
}

.plane-table tr:hover td {
  background: color-mix(in srgb, var(--color-table-row-hover) 82%, var(--sa-primary, var(--color-accent)) 6%);
}

.sticky-work-item {
  position: static;
  left: auto;
  z-index: 20;
  min-width: 420px;
  max-width: 420px;
  box-shadow: none;
}

th.sticky-work-item {
  z-index: 25;
}

.spreadsheet-container {
  scrollbar-gutter: stable;
}

.plane-table th:first-child,
.plane-table td:first-child {
  border-left: 1px solid var(--color-border);
}

.wi-cell {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
  cursor: pointer;
}

.wi-cell:focus-visible {
  border-radius: 6px;
  outline: 3px solid color-mix(in srgb, var(--color-accent) 30%, transparent);
  outline-offset: 2px;
}

.wi-id {
  color: color-mix(in srgb, var(--sa-primary, var(--color-accent)) 74%, var(--color-text-primary));
  font-size: 12px;
  font-weight: 850;
  min-width: 70px;
  flex-shrink: 0;
}

.wi-title {
  color: var(--color-text-primary);
  font-weight: 700;
  overflow: hidden;
  text-overflow: ellipsis;
  outline: none;
}

.wi-title:focus {
  color: var(--color-accent);
}

.cell-btn {
  display: flex;
  align-items: center;
  gap: 8px;
  width: fit-content;
  max-width: 100%;
  min-height: 30px;
  padding: 6px 9px;
  border: 1px solid transparent;
  border-radius: 999px;
  background: transparent;
  color: var(--color-text-primary);
  cursor: pointer;
  text-align: left;
  font-size: 13px;
  font-weight: 700;
}

.cell-btn:hover {
  background: var(--color-surface-hover);
  border-color: var(--color-border);
}

.state-badge {
  background: color-mix(in srgb, var(--sa-primary-soft, rgba(14, 165, 233, 0.12)) 62%, var(--color-surface));
  border-color: color-mix(in srgb, var(--sa-primary, var(--color-accent)) 18%, var(--color-border));
}

.plane-table tbody tr.status-backlog {
  --row-status: #64748b;
}

.plane-table tbody tr.status-todo {
  --row-status: #8b5cf6;
}

.plane-table tbody tr.status-progress {
  --row-status: #0ea5e9;
}

.plane-table tbody tr.status-review {
  --row-status: #f59e0b;
}

.plane-table tbody tr.status-done {
  --row-status: #22c55e;
}

.plane-table tbody tr {
  box-shadow: inset 3px 0 0 color-mix(in srgb, var(--row-status, transparent) 0%, transparent);
}

.plane-table tbody tr:hover,
.plane-table tbody tr.status-progress:hover,
.plane-table tbody tr.status-review:hover,
.plane-table tbody tr.status-todo:hover,
.plane-table tbody tr.status-done:hover,
.plane-table tbody tr.status-backlog:hover {
  background: color-mix(in srgb, var(--row-status, var(--color-accent)) 8%, var(--color-surface)) !important;
  box-shadow: inset 3px 0 0 var(--row-status, var(--color-accent));
}

.state-badge.status-backlog {
  --state-color: #64748b;
}

.state-badge.status-todo {
  --state-color: #8b5cf6;
}

.state-badge.status-progress {
  --state-color: #0ea5e9;
}

.state-badge.status-review {
  --state-color: #f59e0b;
}

.state-badge.status-done {
  --state-color: #22c55e;
}

.state-badge {
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--state-color, var(--color-accent)) 16%, transparent), transparent 70%),
    color-mix(in srgb, var(--state-color, var(--color-accent)) 9%, var(--color-surface)) !important;
  border-color: color-mix(in srgb, var(--state-color, var(--color-accent)) 36%, var(--color-border)) !important;
  color: var(--color-text-primary) !important;
}

.state-badge i {
  color: var(--state-color, var(--color-accent)) !important;
}

.priority-badge {
  background: color-mix(in srgb, var(--color-surface-hover) 78%, var(--color-surface));
}

.priority-1 {
  background: color-mix(in srgb, var(--color-danger) 12%, var(--color-surface));
  border-color: color-mix(in srgb, var(--color-danger) 28%, var(--color-border));
}

.priority-2 {
  background: color-mix(in srgb, var(--color-warning) 12%, var(--color-surface));
  border-color: color-mix(in srgb, var(--color-warning) 30%, var(--color-border));
}

.priority-3 {
  background: color-mix(in srgb, var(--sa-primary, var(--color-accent)) 11%, var(--color-surface));
  border-color: color-mix(in srgb, var(--sa-primary, var(--color-accent)) 24%, var(--color-border));
}

.priority-4 {
  background: color-mix(in srgb, #64748b 12%, var(--color-surface));
  border-color: color-mix(in srgb, #64748b 28%, var(--color-border));
}

.priority-0 {
  background: color-mix(in srgb, var(--color-text-muted) 8%, var(--color-surface));
  border-color: color-mix(in srgb, var(--color-text-muted) 20%, var(--color-border));
}

.date-input {
  width: 135px;
  min-height: 34px !important;
  height: 34px !important;
  padding: 6px 9px !important;
}

.plane-search-input {
  width: 100%;
  min-height: 36px !important;
  height: 36px !important;
  padding: 7px 10px !important;
  margin-bottom: 8px;
}

.member-option {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 9px 10px;
  border-radius: 8px;
  cursor: pointer;
  color: var(--color-text-secondary);
}

.member-option:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.text-green { color: #10b981; }
.text-orange { color: #f59e0b; }
.text-red { color: #ef4444; }
.text-blue { color: #3b82f6; }
.text-muted,
.muted-text,
.date-text { color: var(--color-text-secondary); }

.empty-cell {
  padding: 56px 24px;
  text-align: center;
  color: var(--color-text-muted);
  background: color-mix(in srgb, var(--color-surface) 92%, var(--color-bg)) !important;
}

.empty-state {
  display: inline-flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  min-width: 320px;
  padding: 26px 28px;
  border: 1px solid var(--color-border);
  border-radius: 14px;
  background: var(--color-surface);
  box-shadow: var(--sa-shadow-sm, var(--shadow-sm));
}

.empty-icon {
  width: 42px;
  height: 42px;
  border-radius: 14px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: var(--sa-primary-soft, color-mix(in srgb, var(--color-accent) 12%, transparent));
  color: var(--sa-primary, var(--color-accent));
  font-size: 18px;
}

.empty-state strong {
  color: var(--color-text-primary);
  font-size: 15px;
}

.empty-state span {
  color: var(--color-text-secondary);
  font-size: 13px;
}

.add-btn {
  border: 1px solid transparent;
  background: var(--color-surface);
  color: var(--color-text-primary);
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 38px;
  padding: 8px 12px;
  border-radius: 10px;
  font-weight: 800;
}

.add-btn:hover {
  background: var(--sa-primary-soft, var(--color-surface-hover));
  color: var(--sa-primary, var(--color-accent));
  border-color: color-mix(in srgb, var(--sa-primary, var(--color-accent)) 24%, var(--color-border));
}
</style>
