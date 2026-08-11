<script setup>
import { computed, nextTick, ref } from 'vue'
import draggable from 'vuedraggable'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'
import { useI18n } from '@/composables/useI18n'
import { useStarredStore } from '@/store/useStarredStore'
import { STARRED_ENTITY_TYPES } from '@/api/starredRecentApi'

const { t } = useI18n()
const starredStore = useStarredStore()

const props = defineProps({
  tasks: { type: Array, default: () => [] },
  statuses: { type: Array, default: () => [] },
  projectId: { type: String, required: true },
  loading: { type: Boolean, default: false }
})

const emit = defineEmits(['task-click', 'status-changed', 'task-reordered', 'refresh'])

const dragEnabled = ref(true)
const createMode = ref(false)
const draftTitles = ref({})
const creatingByStatus = ref({})

function handleBoardWheel(event) {
  const board = event.currentTarget
  if (!board || board.scrollWidth <= board.clientWidth) return
  if (Math.abs(event.deltaY) <= Math.abs(event.deltaX)) return
  event.preventDefault()
  board.scrollLeft += event.deltaY
}

const statusColumns = computed(() => {
  let columns = []
  if (Array.isArray(props.statuses) && props.statuses.length > 0) {
    columns = props.statuses.map(s => ({
      key: s.name.toUpperCase().trim(),
      label: s.label || s.name,
      color: s.color || 'var(--color-text-muted)',
      icon: s.icon || 'fa-regular fa-circle'
    }))
  } else {
    columns = [
      { key: 'BACKLOG', label: t('workItems.statusLabels.backlog', 'Backlog'), color: '#94A3B8', icon: 'fa-regular fa-circle-dashed' },
      { key: 'TO DO', label: t('workItems.statusLabels.toDo', 'To Do'), color: 'var(--color-text-muted)', icon: 'fa-regular fa-circle' },
      { key: 'IN PROGRESS', label: t('workItems.statusLabels.inProgress', 'In Progress'), color: 'var(--color-accent)', icon: 'fa-solid fa-circle-half-stroke' },
      { key: 'IN REVIEW', label: t('workItems.statusLabels.inReview', 'In Review'), color: 'var(--color-warning)', icon: 'fa-solid fa-eye' },
      { key: 'DONE', label: t('workItems.statusLabels.done', 'Done'), color: 'var(--color-success)', icon: 'fa-solid fa-circle-check' },
      { key: 'CANCELLED', label: t('workItems.statusLabels.cancelled', 'Cancelled'), color: '#F43F5E', icon: 'fa-regular fa-circle-xmark' }
    ]
  }

  const mapped = columns.map(column => ({
    ...column,
    tasks: props.tasks
      .filter(task => {
        const taskStatus = (task.statusName || 'BACKLOG').toUpperCase().trim()
        return taskStatus === column.key
      })
      .sort((left, right) => (left.sortOrder || 0) - (right.sortOrder || 0))
  }))

  const definedKeys = columns.map(c => c.key)
  const fallbackTasks = props.tasks
    .filter(task => {
      const taskStatus = (task.statusName || 'BACKLOG').toUpperCase().trim()
      return !definedKeys.includes(taskStatus)
    })
    .sort((left, right) => (left.sortOrder || 0) - (right.sortOrder || 0))

  if (fallbackTasks.length > 0) {
    mapped.push({
      key: 'FALLBACK_UNCLASSIFIED',
      label: t('workItems.statusLabels.fallback', 'Khác / Chưa phân loại'),
      color: '#f43f5e',
      icon: 'fa-solid fa-triangle-exclamation',
      tasks: fallbackTasks,
      isFallback: true
    })
  }

  return mapped
})

function onDragEnd(event, targetStatus) {
  const movedTask = event.item?.__draggable_context?.element
  if (!movedTask) return

  const oldStatus = movedTask.statusName
  const newStatus = targetStatus

  if (oldStatus !== newStatus && newStatus !== 'FALLBACK_UNCLASSIFIED') {
    emit('status-changed', {
      issueId: movedTask.id,
      newStatusName: newStatus,
      taskStatusId: movedTask.taskStatusId,
      rowVersion: movedTask.rowVersion
    })
  }

  const column = statusColumns.value.find(item => item.key === newStatus)
  if (!column) return

  const newIndex = event.newIndex
  const tasks = column.tasks
  let sortOrder = 65536

  if (tasks.length > 1) {
    if (newIndex === 0) {
      sortOrder = (tasks[1]?.sortOrder || 65536) / 2
    } else if (newIndex >= tasks.length - 1) {
      sortOrder = (tasks[tasks.length - 2]?.sortOrder || 0) + 65536
    } else {
      const before = tasks[newIndex - 1]?.sortOrder || 0
      const after = tasks[newIndex + 1]?.sortOrder || before + 131072
      sortOrder = (before + after) / 2
    }
  }

  if (newStatus !== 'FALLBACK_UNCLASSIFIED') {
    emit('task-reordered', {
      issueId: movedTask.id,
      sortOrder,
      newStatusName: newStatus
    })
  }
}

function getPriorityInfo(priority) {
  const map = {
    0: {
      label: t('workItems.priority.none', 'None'),
      color: '#64748b',
      bgColor: 'rgba(100, 116, 139, 0.12)',
      icon: 'fa-solid fa-ban'
    },
    1: {
      label: t('workItems.priority.urgent', 'Urgent'),
      color: '#ef4444',
      bgColor: 'rgba(239, 68, 68, 0.12)',
      icon: 'fa-solid fa-angles-up'
    },
    2: {
      label: t('workItems.priority.high', 'High'),
      color: '#f97316',
      bgColor: 'rgba(249, 115, 22, 0.12)',
      icon: 'fa-solid fa-chevron-up'
    },
    3: {
      label: t('workItems.priority.normal', 'Medium'),
      color: '#3b82f6',
      bgColor: 'rgba(59, 130, 246, 0.12)',
      icon: 'fa-solid fa-minus'
    },
    4: {
      label: t('workItems.priority.low', 'Low'),
      color: '#10b981',
      bgColor: 'rgba(16, 185, 129, 0.12)',
      icon: 'fa-solid fa-chevron-down'
    }
  }
  return map[priority] || map[0]
}

function getColumnTheme(column) {
  return {
    '--column-color': column.color,
    '--column-tint': `color-mix(in srgb, ${column.color} 8%, white)`,
    '--column-soft': `color-mix(in srgb, ${column.color} 18%, white)`,
    '--column-border-soft': `color-mix(in srgb, ${column.color} 26%, white)`,
    '--column-strong': `color-mix(in srgb, ${column.color} 82%, black 18%)`
  }
}

function isTaskStarred(task) {
  return starredStore.isStarred(STARRED_ENTITY_TYPES.WORK_TASK, task?.id)
}

async function toggleTaskStar(task) {
  if (!task?.id || starredStore.isPending(STARRED_ENTITY_TYPES.WORK_TASK, task.id)) return
  await starredStore.toggleStar(STARRED_ENTITY_TYPES.WORK_TASK, task.id)
}

function scrollQuickCreateIntoView(columnKey) {
  nextTick(() => {
    const body = document.querySelector(`[data-column-body="${columnKey}"]`)
    if (body) {
      body.scrollTop = 0
    }
    const form = document.querySelector(`[data-quick-create="${columnKey}"]`)
    form?.scrollIntoView({ block: 'nearest', behavior: 'smooth' })
  })
}


function getInitials(name) {
  if (!name) return '?'
  return name.split(' ').map(word => word[0]).join('').substring(0, 2).toUpperCase()
}

function getAvatarColor(name) {
  if (!name) return '#6b7280'
  let hash = 0
  for (let index = 0; index < name.length; index += 1) {
    hash = name.charCodeAt(index) + ((hash << 5) - hash)
  }
  return `hsl(${Math.abs(hash % 360)}, 65%, 45%)`
}

function formatDate(value) {
  if (!value) return ''
  return new Date(value).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit' })
}

function getTaskProgress(task) {
  if (Array.isArray(task.assignees) && task.assignees.length) {
    const total = task.assignees.reduce((sum, item) => sum + (Number(item.contributionWeight) || 1), 0)
    const weighted = task.assignees.reduce((sum, item) => {
      const weight = Number(item.contributionWeight) || 1
      return sum + ((Number(item.progressPercent) || 0) * weight)
    }, 0)
    return Math.round(weighted / Math.max(total, 1))
  }
  if (`${task.statusName || ''}`.toUpperCase().includes('DONE')) return 100
  return 0
}

function progressStyle(task) {
  const percent = getTaskProgress(task)
  return {
    background: `conic-gradient(var(--color-success) ${percent}%, var(--border-color) ${percent}% 100%)`
  }
}

async function createTask(statusName) {
  const title = `${draftTitles.value[statusName] || ''}`.trim()
  if (!title || creatingByStatus.value[statusName]) return
  creatingByStatus.value = { ...creatingByStatus.value, [statusName]: true }
  try {
    await axiosClient.post(`/projects/${props.projectId}/WorkTasks`, {
      title,
      description: '',
      statusName,
      priority: 3
    })
    draftTitles.value = { ...draftTitles.value, [statusName]: '' }
    emit('refresh')
    ElMessage.success(t('workItems.created', 'Đã tạo công việc thành công.'))
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('workItems.createFailed', 'Không thể tạo công việc. Vui lòng thử lại.'))
  } finally {
    creatingByStatus.value = { ...creatingByStatus.value, [statusName]: false }
  }
}

function handleColumnClick(column) {
  if (column.key === 'FALLBACK_UNCLASSIFIED') return
  if (!createMode.value) return
  if (!draftTitles.value[column.key]) {
    draftTitles.value = { ...draftTitles.value, [column.key]: '' }
  }
  scrollQuickCreateIntoView(column.key)
}
</script>

<template>
  <div class="kanban-shell">
    <div class="board-toolbar">
      <button class="toolbar-btn" type="button" :class="{ active: createMode }" @click="createMode = !createMode">
        <i class="fa-solid fa-plus-circle"></i> {{ t('workItems.kanban.createMode', 'Create mode') }}
      </button>
      <span class="toolbar-copy">{{ t('workItems.kanban.createModeHint', 'Click column header to quickly add work items.') }}</span>
    </div>

    <div class="kanban-board" @wheel="handleBoardWheel">
      <div
        v-for="column in statusColumns"
        :key="column.key"
        class="kanban-column"
        :style="getColumnTheme(column)"
        @click="handleColumnClick(column)"
      >
        <div class="column-header">
          <div class="column-header-left">
            <span class="column-status-dot" :style="{ background: column.color }"></span>
            <span class="column-title">{{ column.label }}</span>
            <span class="column-count">{{ column.tasks.length }}</span>
          </div>
          <button
            v-if="column.key !== 'FALLBACK_UNCLASSIFIED'"
            class="column-add-btn"
            @click.stop="createMode = true; handleColumnClick(column)"
          >
            <i class="fa-solid fa-plus"></i>
          </button>
        </div>

        <div
          v-if="createMode && column.key !== 'FALLBACK_UNCLASSIFIED'"
          :data-quick-create="column.key"
          class="quick-create"
          @click.stop
        >
          <input
            v-model="draftTitles[column.key]"
            type="text"
            class="quick-create-input"
            :placeholder="t('workItems.kanban.newItemInColumn', { column: column.label })"
            @keyup.enter="createTask(column.key)"
          />
          <button class="quick-create-btn" type="button" :disabled="creatingByStatus[column.key]" @click="createTask(column.key)">
            <i v-if="creatingByStatus[column.key]" class="fa-solid fa-spinner fa-spin"></i>
            <span v-else>{{ t('workItems.kanban.add', 'Add') }}</span>
          </button>
        </div>

        <div v-if="column.isFallback" class="fallback-desc-container" style="padding: 6px 12px; background: rgba(244, 63, 94, 0.05); border-bottom: 1px solid rgba(244, 63, 94, 0.1);">
          <small style="color: #f43f5e; font-size: 11px; font-style: italic;">
            {{ t('workItems.kanban.fallbackDesc', 'Các công việc có trạng thái không còn tồn tại trong workflow hiện tại.') }}
          </small>
        </div>

        <draggable
          :list="column.tasks"
          :group="{ name: 'kanban', put: column.key !== 'FALLBACK_UNCLASSIFIED' }"
          item-key="id"
          ghost-class="task-ghost"
          drag-class="task-drag"
          :animation="250"
          :disabled="!dragEnabled || column.key === 'FALLBACK_UNCLASSIFIED'"
          class="column-body"
          :data-column-body="column.key"
          @end="event => onDragEnd(event, column.key)"
        >
          <template #item="{ element }">
            <div class="kanban-card" @click="emit('task-click', element)">
              <div class="card-top">
                <div class="card-top-left">
                  <span v-if="element.dueDate || element.plannedEndDate" class="card-due" :class="{ overdue: new Date(element.dueDate || element.plannedEndDate) < new Date() }">
                    <i class="fa-regular fa-calendar"></i> {{ formatDate(element.dueDate || element.plannedEndDate) }}
                  </span>
                  <span class="card-seq">#{{ element.sequenceId || element.id?.substring(0, 4) }}</span>
                </div>
                <button
                  class="card-star-btn"
                  type="button"
                  :disabled="starredStore.isPending(STARRED_ENTITY_TYPES.WORK_TASK, element.id)"
                  :title="isTaskStarred(element) ? 'Bỏ đánh dấu' : 'Đánh dấu'"
                  @click.stop="toggleTaskStar(element)"
                >
                  <i :class="isTaskStarred(element) ? 'fa-solid fa-star' : 'fa-regular fa-star'"></i>
                </button>
              </div>

              <div class="card-title">{{ element.title }}</div>

              <div class="card-meta">
                <div class="card-meta-left">
                  <span v-if="element.storyPoints" class="card-sp"><i class="fa-solid fa-bolt"></i> {{ element.storyPoints }}</span>
                  <span class="card-priority" :style="{ color: getPriorityInfo(element.priority).color, backgroundColor: getPriorityInfo(element.priority).bgColor }">
                    <i :class="getPriorityInfo(element.priority).icon"></i>
                    {{ getPriorityInfo(element.priority).label }}
                  </span>
                </div>

                <div class="card-meta-right">
                  <div class="task-progress-ring" :style="progressStyle(element)" :title="`${getTaskProgress(element)}% ${t('workItems.progress', 'progress')}`">
                    <span class="ring-value">{{ getTaskProgress(element) }}%</span>
                  </div>

                  <div
                    v-if="element.assigneeName"
                    class="card-avatar"
                    :style="{ backgroundColor: getAvatarColor(element.assigneeName) }"
                    :title="element.assigneeName"
                  >
                    {{ getInitials(element.assigneeName) }}
                  </div>
                </div>
              </div>
            </div>
          </template>

          <template #footer>
            <div v-if="column.tasks.length === 0" class="column-empty">
              <i class="fa-regular fa-square-check"></i>
              <span>{{ t('workItems.kanban.noTasksHere', 'No tasks here') }}</span>
            </div>
          </template>
        </draggable>
      </div>
    </div>
  </div>
</template>

<style scoped>
.kanban-shell {
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: 100%;
}

.board-toolbar {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 0 4px;
}

.toolbar-btn {
  display: flex;
  align-items: center;
  gap: 8px;
  border: 1px solid var(--border-color);
  border-radius: 2px;
  background: var(--bg-secondary);
  color: var(--color-text-secondary);
  padding: 8px 14px;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.toolbar-btn:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.toolbar-btn.active {
  background: var(--color-accent);
  color: #fff;
  border-color: var(--color-accent);
}

.toolbar-copy {
  color: var(--color-text-muted);
  font-size: 13px;
}

.kanban-board {
  display: flex;
  gap: 20px;
  overflow-x: auto;
  overscroll-behavior-x: contain;
  scrollbar-gutter: stable;
  scroll-behavior: smooth;
  padding-bottom: 12px;
  height: calc(100vh - 200px);
}

.kanban-board::-webkit-scrollbar { height: 12px; }
.kanban-board::-webkit-scrollbar-track {
  border-radius: 999px;
  background: color-mix(in srgb, var(--color-border) 48%, transparent);
}
.kanban-board::-webkit-scrollbar-thumb {
  min-width: 72px;
  border: 3px solid transparent;
  border-radius: 999px;
  background: color-mix(in srgb, var(--sp-blue-600) 66%, var(--sp-slate-500)) padding-box;
}
.kanban-board::-webkit-scrollbar-thumb:hover {
  background: var(--sp-blue-600) padding-box;
}

.kanban-column {
  flex: 0 0 320px;
  min-width: 320px;
  display: flex;
  flex-direction: column;
  min-height: 0;
  background: var(--column-tint, var(--color-bg));
  border: 1px solid var(--column-border-soft, var(--color-border));
  border-radius: 2px;
  overflow: hidden;
}

.column-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px;
  background: color-mix(in srgb, var(--column-color, var(--color-accent)) 10%, white);
  border-bottom: 1px solid var(--column-border-soft, var(--color-border));
}

.column-header-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.column-status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.column-title {
  font-size: 14px;
  font-weight: 700;
  color: var(--color-text-primary);
  text-transform: uppercase;
  letter-spacing: 0.02em;
}

.column-count {
  padding: 2px 8px;
  border-radius: 2px;
  background: color-mix(in srgb, var(--column-color, var(--color-accent)) 16%, white);
  border: 1px solid var(--column-border-soft, var(--border-color));
  color: var(--column-strong, var(--color-text-secondary));
  font-size: 11px;
  font-weight: 700;
}

.column-add-btn {
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 2px;
  border: none;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
  transition: all 0.2s;
}

.column-add-btn:hover {
  background: color-mix(in srgb, var(--column-color, var(--color-accent)) 12%, white);
  color: var(--column-strong, var(--color-text-primary));
}

.quick-create {
  display: flex;
  gap: 8px;
  padding: 12px;
  background: color-mix(in srgb, var(--column-color, var(--color-accent)) 6%, white);
  border-bottom: 1px solid var(--column-border-soft, var(--color-border));
  flex-wrap: wrap;
}

.quick-create-input {
  flex: 1 1 180px;
  background: rgba(255, 255, 255, 0.92);
  color: var(--text-primary);
  border: 1px solid var(--column-border-soft, var(--border-color));
  border-radius: 2px;
  padding: 8px 12px;
  font-size: 13px;
  outline: none;
}

.quick-create-input:focus {
  border-color: var(--column-color, var(--color-accent));
}

.quick-create-btn {
  min-height: 36px;
  background: var(--column-color, var(--color-accent));
  color: #fff;
  border: none;
  border-radius: 2px;
  padding: 0 12px;
  font-weight: 600;
  font-size: 13px;
  cursor: pointer;
}

.column-body {
  flex: 1;
  min-height: 0;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  overflow-y: auto;
}

.kanban-card {
  border: 1px solid var(--column-border-soft, var(--color-border));
  border-left: 4px solid var(--column-color, var(--color-accent));
  border-radius: 2px;
  background: rgba(255, 255, 255, 0.96);
  padding: 16px;
  cursor: pointer;
  box-shadow: none !important;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
}

.kanban-card:hover {
  border-color: var(--column-color, var(--color-accent));
  transform: translateY(-2px);
  box-shadow: none !important;
}

.card-top {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 12px;
}

.card-top-left {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  min-width: 0;
}

.card-seq {
  font-size: 12px;
  font-weight: 700;
  color: var(--color-text-muted);
}

.card-priority {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 24px;
  padding: 0 9px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 700;
  line-height: 1;
  white-space: nowrap;
}

.card-star-btn {
  width: 28px;
  height: 28px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--column-border-soft, var(--color-border));
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.92);
  color: #94a3b8;
  cursor: pointer;
  flex-shrink: 0;
}

.card-star-btn:hover {
  color: #f59e0b;
  border-color: #fcd34d;
}

.card-star-btn:disabled {
  cursor: wait;
  opacity: 0.7;
}

.card-title {
  margin-bottom: 16px;
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-primary);
  line-height: 1.5;
}

.card-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.card-meta-left {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
  font-size: 12px;
  color: var(--color-text-muted);
}

.card-due {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 24px;
  padding: 0 9px;
  border-radius: 999px;
  background: rgba(148, 163, 184, 0.12);
  color: #475569;
  font-size: 11px;
  font-weight: 700;
  white-space: nowrap;
}

.card-due.overdue {
  background: rgba(239, 68, 68, 0.12);
  color: #ef4444;
}

.card-sp {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 24px;
  padding: 0 9px;
  border-radius: 999px;
  background: rgba(14, 165, 233, 0.1);
  color: #0369a1;
  font-weight: 700;
}

.card-meta-right {
  display: flex;
  align-items: center;
  gap: 10px;
}

.task-progress-ring {
  position: relative;
  width: 28px;
  height: 28px;
  border-radius: 50%;
  display: grid;
  place-items: center;
}

.task-progress-ring::after {
  content: '';
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.96);
}

.ring-value {
  position: absolute;
  font-size: 8px;
  font-weight: 800;
  color: var(--color-text-primary);
  z-index: 1;
}

.card-avatar {
  width: 26px;
  height: 26px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #ffffff;
  font-size: 11px;
  font-weight: 700;
  border: 2px solid var(--color-surface);
}

.column-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  color: var(--color-text-muted);
  padding: 40px 20px;
  font-size: 14px;
  border: 2px dashed var(--column-border-soft, var(--border-color));
  border-radius: 2px;
  background: rgba(255, 255, 255, 0.58);
}

.column-empty i {
  font-size: 24px;
}

.task-ghost {
  opacity: 0;
}

.task-drag {
  opacity: 0.9;
  transform: scale(1.05) rotate(2deg);
}

/* Compact density */
.kanban-shell {
  gap: 10px !important;
}

.board-toolbar {
  gap: 8px !important;
}

.toolbar-btn {
  min-height: 30px !important;
  padding: 6px 10px !important;
  border-radius: 8px !important;
  font-size: 12.5px !important;
}

.kanban-board {
  gap: 14px !important;
  height: calc(100vh - 178px) !important;
  padding: 0 0 10px !important;
}

.kanban-column {
  flex-basis: 284px !important;
  min-width: 284px !important;
  border-radius: 10px !important;
}

.column-header {
  min-height: 48px !important;
  padding: 10px 12px !important;
}

.column-header-left {
  gap: 8px !important;
}

.column-title {
  font-size: 12.5px !important;
}

.column-count {
  padding: 1px 7px !important;
  border-radius: 999px !important;
}

.column-add-btn {
  width: 26px !important;
  height: 26px !important;
  border-radius: 7px !important;
}

.quick-create {
  padding: 10px !important;
}

.quick-create-input {
  min-height: 30px !important;
  padding: 6px 8px !important;
  border-radius: 7px !important;
  font-size: 12px !important;
}

.quick-create-btn {
  border-radius: 7px !important;
  font-size: 12px !important;
}

.column-body {
  gap: 10px !important;
  padding: 10px !important;
}

.kanban-card {
  border-radius: 8px !important;
  padding: 12px !important;
}

.card-top {
  margin-bottom: 8px !important;
}

.card-top-left {
  gap: 6px !important;
}

.card-title {
  margin-bottom: 10px !important;
  font-size: 13px !important;
  line-height: 1.3 !important;
  overflow-wrap: anywhere !important;
}

.card-meta-left {
  gap: 8px !important;
  font-size: 11px !important;
}

.task-progress-ring,
.card-avatar {
  width: 24px !important;
  height: 24px !important;
}

.column-empty {
  padding: 22px 12px !important;
  border-radius: 8px !important;
  font-size: 12px !important;
}

@media (max-width: 720px) {
  .kanban-board {
    height: calc(100vh - 210px) !important;
    height: calc(100dvh - 210px) !important;
    min-height: 420px !important;
    max-height: calc(100dvh - 160px) !important;
    overflow-x: auto !important;
    overflow-y: hidden !important;
    max-width: 100% !important;
  }

  .kanban-column {
    flex-basis: 284px !important;
    min-width: 284px !important;
    height: 100% !important;
  }
}
</style>
