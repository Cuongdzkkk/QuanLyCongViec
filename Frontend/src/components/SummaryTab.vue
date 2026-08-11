<template>
  <section class="summary-tab">
    <div class="summary-toolbar">
      <button
        class="filter-btn"
        type="button"
        disabled
        :title="t('workItems.summary.filterNeedsFlow')"
      >
        <i class="fa-solid fa-filter"></i>
        {{ t('workItems.summary.filter') }}
      </button>
    </div>

    <div class="metric-grid">
      <div v-for="item in metrics" :key="item.key" class="metric-card">
        <span class="metric-icon" :class="item.tone"><i :class="item.icon"></i></span>
        <div>
          <strong>{{ t('workItems.summary.workItemsCount', { count: item.value }) }}</strong>
          <span>{{ item.label }}</span>
        </div>
      </div>
    </div>

    <div class="summary-grid">
      <article class="summary-card">
        <div class="card-heading">
          <div>
            <h3>{{ t('workItems.summary.statusOverview') }}</h3>
            <p>
              {{ t('workItems.summary.statusOverviewHelp') }}
              <button class="text-link" type="button" @click="$emit('view-work-items')">
                {{ t('workItems.summary.viewAllWorkItems') }}
              </button>
            </p>
          </div>
        </div>

        <div v-if="totalTasks" class="donut-layout">
          <div class="donut-chart" :style="{ background: statusConicGradient }">
            <div class="donut-center">
              <strong>{{ totalTasks }}</strong>
              <span>{{ t('workItems.summary.totalWorkItemsShort') }}</span>
            </div>
          </div>
          <div class="legend-list">
            <div v-for="item in visibleStatusData" :key="item.key" class="legend-row">
              <span class="legend-dot" :style="{ background: item.color }"></span>
              <span>{{ item.label }}: {{ item.count }}</span>
            </div>
          </div>
        </div>

        <div v-else class="empty-block compact">
          <i class="fa-regular fa-circle-check"></i>
          <strong>{{ t('workItems.summary.noWorkItems') }}</strong>
        </div>
      </article>

      <article class="summary-card empty-activity">
        <div class="empty-illustration">
          <span></span>
          <span></span>
          <i class="fa-solid fa-check"></i>
        </div>
        <h3>{{ t('workItems.summary.noActivityYet') }}</h3>
        <p>{{ t('workItems.summary.noActivityHelp') }}</p>
      </article>

      <article class="summary-card">
        <h3>{{ t('workItems.summary.priorityBreakdown') }}</h3>
        <p>{{ t('workItems.summary.priorityBreakdownHelp') }}</p>
        <div class="bar-chart" :style="{ '--max-count': priorityMax }">
          <div v-for="item in priorityData" :key="item.key" class="priority-column">
            <div class="bar-track">
              <span
                class="vertical-bar"
                :class="{ empty: item.count === 0 }"
                :style="{ height: `${getPriorityHeight(item.count)}%` }"
              ></span>
            </div>
            <span class="priority-label">
              <i :class="item.icon" :style="{ color: item.color }"></i>
              {{ item.label }}
            </span>
          </div>
        </div>
      </article>

      <article class="summary-card">
        <h3>{{ t('workItems.summary.typesOfWork') }}</h3>
        <p>
          {{ t('workItems.summary.typesOfWorkHelp') }}
          <button class="text-link" type="button" @click="$emit('view-work-items')">
            {{ t('workItems.summary.viewAllItems') }}
          </button>
        </p>
        <div class="distribution-list">
          <div v-for="item in typeData" :key="item.key" class="distribution-row">
            <span class="type-label">
              <i :class="item.icon" :style="{ color: item.color }"></i>
              {{ item.label }}
            </span>
            <div class="distribution-track">
              <span :style="{ width: `${item.percent}%`, background: item.color }"></span>
              <strong v-if="item.count">{{ item.percent }}%</strong>
            </div>
          </div>
        </div>
      </article>

      <article class="summary-card">
        <h3>{{ t('workItems.summary.teamWorkload') }}</h3>
        <p>{{ t('workItems.summary.teamWorkloadHelp') }}</p>
        <div v-if="workloadData.length" class="workload-list">
          <div v-for="item in workloadData" :key="item.key" class="workload-row">
            <span class="avatar" :style="{ background: item.color }">{{ item.avatar }}</span>
            <span class="workload-name">{{ item.label }}</span>
            <div class="workload-track">
              <span :style="{ width: `${item.percent}%` }"></span>
              <strong>{{ item.percent }}%</strong>
            </div>
          </div>
        </div>
        <div v-else class="empty-block">
          <i class="fa-regular fa-user"></i>
          <strong>{{ t('workItems.summary.noWorkload') }}</strong>
        </div>
      </article>

      <article class="summary-card epic-card">
        <div v-if="epicData.length" class="epic-list">
          <h3>{{ t('workItems.summary.epicProgress') }}</h3>
          <div v-for="item in epicData" :key="item.key" class="epic-row">
            <span>{{ item.label }}</span>
            <div class="distribution-track">
              <span :style="{ width: `${item.percent}%` }"></span>
              <strong>{{ item.percent }}%</strong>
            </div>
          </div>
        </div>
        <div v-else class="empty-block">
          <div class="empty-illustration small">
            <span></span>
            <span></span>
            <i class="fa-solid fa-bolt"></i>
          </div>
          <h3>{{ t('workItems.summary.epicProgress') }}</h3>
          <p>{{ t('workItems.summary.epicProgressNeedsData') }}</p>
        </div>
      </article>
    </div>
  </section>
</template>

<script setup>
import { computed } from 'vue'
import { useI18n } from '@/composables/useI18n'

defineEmits(['view-work-items'])

const props = defineProps({
  tasks: { type: Array, default: () => [] },
  projectMembers: { type: Array, default: () => [] },
  statusOptions: { type: Array, default: () => [] }
})

const { t } = useI18n()
const totalTasks = computed(() => props.tasks.length)

const STATUS_COLORS = {
  BACKLOG: '#8993a4',
  TODO: '#8993a4',
  'TO DO': '#8993a4',
  'IN PROGRESS': '#0c66e4',
  'IN REVIEW': '#e56910',
  DONE: '#22a06b',
  CANCELLED: '#e2483d'
}

const priorityMeta = computed(() => [
  { key: 'highest', values: [1], label: t('workItems.summary.priorityHighest'), icon: 'fa-solid fa-angles-up', color: '#e2483d' },
  { key: 'high', values: [2], label: t('workItems.summary.priorityHigh'), icon: 'fa-solid fa-chevron-up', color: '#e56910' },
  { key: 'medium', values: [3], label: t('workItems.summary.priorityMedium'), icon: 'fa-solid fa-equals', color: '#f59e0b' },
  { key: 'low', values: [4], label: t('workItems.summary.priorityLow'), icon: 'fa-solid fa-chevron-down', color: '#0c66e4' },
  { key: 'lowest', values: [0, null, undefined, ''], label: t('workItems.summary.priorityLowest'), icon: 'fa-solid fa-angles-down', color: '#6b778c' }
])

const typeMeta = computed(() => [
  { key: 'Subtask', label: t('workItems.summary.typeSubtask'), icon: 'fa-solid fa-diagram-project', color: '#6e5dc6' },
  { key: 'Task', label: t('workItems.summary.typeTask'), icon: 'fa-solid fa-square-check', color: '#0c66e4' },
  { key: 'Bug', label: t('workItems.summary.typeBug'), icon: 'fa-solid fa-bug', color: '#e2483d' },
  { key: 'Epic', label: t('workItems.summary.typeEpic'), icon: 'fa-solid fa-bolt', color: '#ae2e24' },
  { key: 'Story', label: t('workItems.summary.typeStory'), icon: 'fa-regular fa-bookmark', color: '#22a06b' }
])

const avatarColors = ['#6554c0', '#0c66e4', '#ff991f', '#00875a', '#00a3bf', '#de350b']

const metrics = computed(() => {
  const last7 = startOfDay(daysFromNow(-7))
  const next7 = endOfDay(daysFromNow(7))
  const now = new Date()

  let completed = 0
  let updated = 0
  let created = 0
  let dueSoon = 0

  props.tasks.forEach((task) => {
    const updatedAt = toDate(task.updatedAt || task.completedAt || task.resolvedAt)
    const createdAt = toDate(task.createdAt)
    const dueDate = toDate(task.dueDate)

    if (isDone(task) && updatedAt && updatedAt >= last7) completed += 1
    if (updatedAt && updatedAt >= last7) updated += 1
    if (createdAt && createdAt >= last7) created += 1
    if (!isDone(task) && dueDate && dueDate >= now && dueDate <= next7) dueSoon += 1
  })

  return [
    { key: 'completed', value: completed, label: t('workItems.summary.completedLast7Days'), icon: 'fa-regular fa-circle-check', tone: 'green' },
    { key: 'updated', value: updated, label: t('workItems.summary.updatedLast7Days'), icon: 'fa-solid fa-pen', tone: 'blue' },
    { key: 'created', value: created, label: t('workItems.summary.createdLast7Days'), icon: 'fa-regular fa-square-plus', tone: 'gray' },
    { key: 'dueSoon', value: dueSoon, label: t('workItems.summary.dueNext7Days'), icon: 'fa-regular fa-calendar', tone: 'red' }
  ]
})

const statusData = computed(() => {
  const counts = new Map()
  props.tasks.forEach((task) => {
    const status = normalizeStatus(task.statusName)
    counts.set(status, (counts.get(status) || 0) + 1)
  })

  const configured = props.statusOptions.length
    ? props.statusOptions.map((status) => normalizeStatus(status.name || status.label))
    : ['TO DO', 'IN PROGRESS', 'DONE']
  const keys = Array.from(new Set([...configured, ...counts.keys()]))

  return keys.map((key) => ({
    key,
    label: getStatusLabel(key),
    count: counts.get(key) || 0,
    color: STATUS_COLORS[key] || '#8993a4'
  }))
})

const visibleStatusData = computed(() => statusData.value.filter((item) => item.count > 0))

const statusConicGradient = computed(() => {
  if (!totalTasks.value) return '#dfe1e6'
  let cursor = 0
  const segments = visibleStatusData.value.map((item) => {
    const start = cursor
    const size = (item.count / totalTasks.value) * 100
    cursor += size
    return `${item.color} ${start}% ${cursor}%`
  })
  return `conic-gradient(${segments.join(', ')})`
})

const priorityData = computed(() => priorityMeta.value.map((meta) => ({
  ...meta,
  count: props.tasks.filter((task) => meta.values.includes(task.priority ?? null)).length
})))

const priorityMax = computed(() => Math.max(1, ...priorityData.value.map((item) => item.count)))

const typeData = computed(() => {
  const counts = new Map(typeMeta.value.map((item) => [item.key, 0]))
  props.tasks.forEach((task) => {
    const type = getTaskType(task)
    counts.set(type, (counts.get(type) || 0) + 1)
  })

  return typeMeta.value.map((meta) => {
    const count = counts.get(meta.key) || 0
    return {
      ...meta,
      count,
      percent: totalTasks.value ? Math.round((count / totalTasks.value) * 100) : 0
    }
  })
})

const workloadData = computed(() => {
  const counts = new Map()
  props.tasks.forEach((task) => {
    const ids = getAssigneeIds(task)
    if (!ids.length) {
      counts.set('__unassigned', (counts.get('__unassigned') || 0) + 1)
      return
    }
    ids.forEach((id) => counts.set(id, (counts.get(id) || 0) + 1))
  })

  const totalAssignments = Array.from(counts.values()).reduce((sum, count) => sum + count, 0)
  if (!totalAssignments) return []

  return Array.from(counts.entries())
    .map(([id, count], index) => {
      const member = props.projectMembers.find((item) => `${item.userId || item.id}` === `${id}`)
      const label = id === '__unassigned'
        ? t('workItems.unassigned')
        : member?.fullName || member?.name || member?.email || t('workItems.assignee')
      return {
        key: id,
        label,
        count,
        percent: Math.round((count / totalAssignments) * 100),
        avatar: label.trim().slice(0, 1).toUpperCase() || '?',
        color: avatarColors[index % avatarColors.length]
      }
    })
    .sort((left, right) => right.count - left.count)
    .slice(0, 6)
})

const epicData = computed(() => {
  const epics = props.tasks.filter((task) => getTaskType(task) === 'Epic')
  return epics.map((task) => ({
    key: task.id,
    label: task.sequenceId || task.title || t('workItems.summary.typeEpic'),
    percent: isDone(task) ? 100 : 0
  }))
})

function getPriorityHeight(count) {
  if (!count) return 0
  return Math.max(12, Math.round((count / priorityMax.value) * 100))
}

function getStatusLabel(status) {
  const found = props.statusOptions.find((option) => normalizeStatus(option.name || option.label) === status)
  if (found?.label) return found.label
  if (status === 'TO DO' || status === 'TODO') return t('workItems.statusLabels.toDo')
  if (status === 'IN PROGRESS') return t('workItems.statusLabels.inProgress')
  if (status === 'IN REVIEW') return t('workItems.statusLabels.inReview')
  if (status === 'DONE') return t('workItems.statusLabels.done')
  if (status === 'CANCELLED') return t('workItems.statusLabels.cancelled')
  if (status === 'BACKLOG') return t('workItems.statusLabels.backlog')
  return status || t('workItems.statusLabels.toDo')
}

function getTaskType(task) {
  const raw = `${task.issueType || task.workItemType || task.taskType || task.type || ''}`.trim().toLowerCase()
  if (raw.includes('bug')) return 'Bug'
  if (raw.includes('epic')) return 'Epic'
  if (raw.includes('story')) return 'Story'
  if (isSubtask(task) || raw.includes('subtask') || raw.includes('sub-task')) return 'Subtask'
  return 'Task'
}

function isSubtask(task) {
  return Boolean(task.parentTaskId || task.parentId || task.parentTaskTitle || task.parentTitle)
}

function getAssigneeIds(task) {
  return Array.from(new Set([
    ...(Array.isArray(task.assigneeIds) ? task.assigneeIds : []),
    ...(Array.isArray(task.assignees) ? task.assignees.map((assignee) => assignee.userId || assignee.id).filter(Boolean) : []),
    ...(task.assignedUserId ? [task.assignedUserId] : [])
  ]))
}

function normalizeStatus(value) {
  const normalized = `${value || 'TO DO'}`.toUpperCase().replace(/\s+/g, ' ').trim()
  if (normalized === 'TODO') return 'TO DO'
  return normalized
}

function isDone(task) {
  const status = normalizeStatus(task.statusName)
  return status === 'DONE' || status.includes('COMPLETE')
}

function toDate(value) {
  if (!value) return null
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date
}

function daysFromNow(amount) {
  const date = new Date()
  date.setDate(date.getDate() + amount)
  return date
}

function startOfDay(value) {
  const date = new Date(value)
  date.setHours(0, 0, 0, 0)
  return date
}

function endOfDay(value) {
  const date = new Date(value)
  date.setHours(23, 59, 59, 999)
  return date
}
</script>

<style scoped>
.summary-tab {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 18px 24px 42px;
  background: var(--color-bg);
  color: var(--color-text-primary);
}

.summary-toolbar {
  display: flex;
  justify-content: flex-start;
  margin-bottom: 12px;
}

.filter-btn {
  height: 28px;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-secondary);
  border-radius: 4px;
  padding: 0 10px;
  font-size: 12px;
}

.filter-btn:disabled {
  cursor: not-allowed;
  opacity: 0.65;
}

.metric-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(160px, 1fr));
  gap: 12px;
  margin-bottom: 14px;
}

.metric-card,
.summary-card {
  border: 1px solid color-mix(in srgb, var(--color-border) 82%, transparent);
  background: color-mix(in srgb, var(--color-surface) 94%, transparent);
  border-radius: 14px;
  box-shadow: 0 14px 34px rgba(15, 23, 42, 0.06);
}

.metric-card {
  min-height: 66px;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 14px;
  min-width: 0;
}

.metric-icon {
  width: 34px;
  height: 34px;
  border-radius: 10px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  background: var(--color-bg);
  color: var(--color-text-secondary);
}

.metric-icon.green { color: #22a06b; }
.metric-icon.blue { color: #0c66e4; }
.metric-icon.gray { color: #626f86; }
.metric-icon.red { color: #e2483d; }

.metric-card strong {
  display: block;
  font-size: 14px;
  color: var(--color-text-primary);
  line-height: 1.2;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.metric-card span:last-child {
  display: block;
  margin-top: 2px;
  font-size: 11px;
  color: var(--color-text-muted);
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(280px, 1fr));
  gap: 14px;
  max-width: 1100px;
  margin: 0 auto;
}

.summary-card {
  min-height: 204px;
  padding: 16px 18px;
  overflow: hidden;
}

.summary-card h3 {
  margin: 0;
  font-size: 14px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.summary-card p {
  margin: 3px 0 0;
  font-size: 12px;
  line-height: 1.45;
  color: var(--color-text-secondary);
}

.text-link {
  border: 0;
  background: transparent;
  color: #0c66e4;
  padding: 0;
  font-size: inherit;
  cursor: pointer;
}

.text-link:hover {
  text-decoration: underline;
}

.donut-layout {
  min-height: 145px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 34px;
  padding-top: 14px;
}

.donut-chart {
  width: 132px;
  height: 132px;
  border-radius: 50%;
  position: relative;
  flex-shrink: 0;
}

.donut-chart::after {
  content: "";
  position: absolute;
  inset: 22px;
  border-radius: 50%;
  background: var(--color-surface);
}

.donut-center {
  position: absolute;
  inset: 0;
  z-index: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
}

.donut-center strong {
  font-size: 22px;
  line-height: 1;
  color: var(--color-text-primary);
}

.donut-center span {
  max-width: 72px;
  margin-top: 6px;
  font-size: 11px;
  color: var(--color-text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.legend-list,
.distribution-list,
.workload-list,
.epic-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.legend-row {
  display: flex;
  align-items: center;
  gap: 7px;
  font-size: 12px;
  color: var(--color-text-secondary);
}

.legend-dot {
  width: 9px;
  height: 9px;
  border-radius: 2px;
}

.empty-activity,
.empty-block {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
}

.empty-activity {
  min-height: 204px;
}

.empty-block {
  min-height: 148px;
  color: var(--color-text-secondary);
}

.empty-block.compact {
  min-height: 138px;
}

.empty-block i {
  margin-bottom: 8px;
  color: var(--color-text-muted);
}

.empty-block strong {
  color: var(--color-text-primary);
  font-size: 13px;
}

.empty-illustration {
  width: 62px;
  height: 42px;
  position: relative;
  margin-bottom: 12px;
}

.empty-illustration span {
  position: absolute;
  width: 38px;
  height: 18px;
  background: var(--color-border);
}

.empty-illustration span:first-child {
  left: 6px;
  top: 8px;
}

.empty-illustration span:nth-child(2) {
  right: 5px;
  top: 14px;
  background: #c1c7d0;
}

.empty-illustration i {
  position: absolute;
  right: 3px;
  bottom: 5px;
  width: 22px;
  height: 22px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: #0c66e4;
  color: #ffffff;
  border-radius: 2px;
  font-size: 12px;
}

.empty-illustration.small {
  margin: 0 auto 12px;
}

.bar-chart {
  height: 130px;
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  align-items: end;
  gap: 12px;
  margin-top: 20px;
  border-left: 1px solid color-mix(in srgb, var(--color-border) 84%, transparent);
  border-bottom: 1px solid color-mix(in srgb, var(--color-border) 84%, transparent);
  padding: 0 8px 0 12px;
}

.priority-column {
  min-width: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 7px;
}

.bar-track {
  height: 92px;
  width: 100%;
  display: flex;
  align-items: end;
  justify-content: center;
}

.vertical-bar {
  width: min(42px, 75%);
  min-height: 8px;
  display: block;
  background: linear-gradient(180deg, var(--color-accent), color-mix(in srgb, var(--color-accent) 70%, #22c55e));
  border-radius: 999px 999px 4px 4px;
}

.vertical-bar.empty {
  min-height: 0;
}

.priority-label {
  width: 100%;
  display: flex;
  justify-content: center;
  gap: 4px;
  color: var(--color-text-secondary);
  font-size: 11px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.distribution-row,
.workload-row,
.epic-row {
  display: grid;
  grid-template-columns: 122px 1fr;
  align-items: center;
  gap: 10px;
  font-size: 12px;
}

.distribution-list {
  margin-top: 18px;
}

.type-label {
  display: flex;
  align-items: center;
  gap: 7px;
  color: var(--color-text-primary);
}

.distribution-track,
.workload-track {
  height: 18px;
  position: relative;
  overflow: hidden;
  background: color-mix(in srgb, var(--color-border) 70%, transparent);
  border-radius: 999px;
}

.distribution-track span,
.workload-track span {
  display: block;
  height: 100%;
  border-radius: inherit;
}

.distribution-track strong,
.workload-track strong {
  position: absolute;
  left: 8px;
  top: 50%;
  transform: translateY(-50%);
  color: #ffffff;
  font-size: 11px;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.24);
}

.workload-list {
  margin-top: 16px;
  max-height: 156px;
  overflow: auto;
  padding-right: 4px;
}

.workload-row {
  grid-template-columns: 22px minmax(78px, 120px) 1fr;
}

.avatar {
  width: 22px;
  height: 22px;
  border-radius: 50%;
  color: #ffffff;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 700;
}

.workload-name {
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
  color: var(--color-text-primary);
}

.epic-card {
  display: flex;
}

.epic-card > * {
  width: 100%;
}

.epic-list h3 {
  margin-bottom: 12px;
}

[data-theme='dark'] .metric-card,
[data-theme='dark'] .summary-card {
  background: rgba(15, 23, 42, 0.78);
  border-color: rgba(148, 163, 184, 0.18);
  box-shadow: 0 18px 44px rgba(0, 0, 0, 0.24);
}

[data-theme='dark'] .metric-icon {
  background: rgba(255, 255, 255, 0.04);
}

@media (max-width: 900px) {
  .metric-grid,
  .summary-grid {
    grid-template-columns: 1fr;
  }

  .summary-grid {
    max-width: none;
  }
}

/* Compact density */
.summary-dashboard {
  padding: 16px var(--sa-page-x, 24px) 28px !important;
}

.metric-grid,
.summary-grid {
  gap: 14px !important;
}

.metric-card,
.summary-card {
  border-radius: 10px !important;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.06) !important;
}

.metric-card {
  min-height: 78px !important;
  padding: 10px 12px !important;
}

.metric-icon {
  width: 34px !important;
  height: 34px !important;
  border-radius: 8px !important;
}

.metric-card strong {
  font-size: 22px !important;
}

.summary-card {
  padding: 14px 16px !important;
}

.summary-card h3 {
  font-size: 13.5px !important;
}

.summary-card p {
  font-size: 12px !important;
}

.recent-task-row {
  min-height: 42px !important;
}

@media (max-width: 760px) {
  .summary-dashboard {
    padding: 12px !important;
  }
}

/* Premium overview polish */
@keyframes summary-float-in {
  from {
    opacity: 0;
    transform: translateY(14px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.summary-tab {
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-bg) 84%, #f8fafc), var(--color-bg)) !important;
}

.metric-card,
.summary-card {
  position: relative;
  overflow: hidden;
  animation: summary-float-in 480ms cubic-bezier(0.2, 0.8, 0.2, 1) both;
  transition:
    transform 220ms cubic-bezier(0.2, 0.8, 0.2, 1),
    box-shadow 220ms ease,
    border-color 220ms ease !important;
}

.metric-card:nth-child(2) { animation-delay: 55ms; }
.metric-card:nth-child(3) { animation-delay: 110ms; }
.metric-card:nth-child(4) { animation-delay: 165ms; }
.summary-card:nth-child(1) { animation-delay: 120ms; }
.summary-card:nth-child(2) { animation-delay: 170ms; }
.summary-card:nth-child(3) { animation-delay: 220ms; }
.summary-card:nth-child(4) { animation-delay: 270ms; }
.summary-card:nth-child(5) { animation-delay: 320ms; }
.summary-card:nth-child(6) { animation-delay: 370ms; }

.metric-card::before,
.summary-card::before {
  content: "";
  position: absolute;
  inset: 0 0 auto 0;
  height: 3px;
  background: linear-gradient(90deg, #38bdf8, #2dd4bf, #facc15);
  opacity: 0.88;
}

.metric-card::after,
.summary-card::after {
  content: "";
  position: absolute;
  inset: -40% auto auto -24%;
  width: 46%;
  height: 180%;
  background: linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.36), transparent);
  transform: rotate(14deg);
  opacity: 0;
  pointer-events: none;
  transition: opacity 220ms ease, transform 680ms ease;
}

.metric-card:hover,
.summary-card:hover {
  transform: translateY(-3px);
  border-color: color-mix(in srgb, var(--color-accent) 28%, var(--color-border)) !important;
  box-shadow: 0 26px 70px rgba(15, 23, 42, 0.12) !important;
}

.metric-card:hover::after,
.summary-card:hover::after {
  opacity: 1;
  transform: translateX(220%) rotate(14deg);
}

.metric-card:nth-child(1) { --metric-tone: #38bdf8; }
.metric-card:nth-child(2) { --metric-tone: #22c55e; }
.metric-card:nth-child(3) { --metric-tone: #8b5cf6; }
.metric-card:nth-child(4) { --metric-tone: #f43f5e; }

.metric-card {
  border-left: 3px solid var(--metric-tone, var(--color-accent)) !important;
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--metric-tone, var(--color-accent)) 10%, transparent), transparent 68%),
    color-mix(in srgb, var(--color-surface) 94%, transparent) !important;
}

.metric-icon {
  background:
    color-mix(in srgb, var(--metric-tone, var(--color-accent)) 14%, var(--color-surface-hover)) !important;
  color: var(--metric-tone, var(--color-accent)) !important;
  box-shadow: 0 10px 24px color-mix(in srgb, var(--metric-tone, var(--color-accent)) 18%, transparent);
}

.summary-card h3 {
  letter-spacing: -0.005em;
}

.donut-chart {
  box-shadow:
    0 18px 42px rgba(15, 23, 42, 0.10),
    inset 0 0 0 1px rgba(255,255,255,0.42);
}

.vertical-bar,
.distribution-track span,
.workload-track span {
  box-shadow: 0 8px 18px color-mix(in srgb, var(--color-accent) 18%, transparent);
}

.distribution-row,
.workload-row,
.epic-row,
.legend-row {
  border-radius: 9px;
  padding: 6px 8px;
  transition: background 180ms ease, transform 180ms ease;
}

.distribution-row:hover,
.workload-row:hover,
.epic-row:hover,
.legend-row:hover {
  background: color-mix(in srgb, var(--color-accent) 7%, var(--color-surface));
  transform: translateX(3px);
}

[data-theme='dark'] .metric-card,
[data-theme='dark'] .summary-card {
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--metric-tone, #38bdf8) 10%, transparent), transparent 68%),
    rgba(15, 23, 42, 0.82) !important;
}

@media (prefers-reduced-motion: reduce) {
  .metric-card,
  .summary-card {
    animation: none !important;
    transition: none !important;
  }
}
</style>
