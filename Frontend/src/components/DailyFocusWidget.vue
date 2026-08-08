<template>
  <section class="dfw-card" aria-labelledby="dfw-title">
    <!-- Header -->
    <div class="dfw-header">
      <div class="dfw-header-left">
        <i class="fa-solid fa-fire dfw-fire"></i>
        <div>
          <h2 id="dfw-title" class="dfw-title">{{ t('dailyFocusWidget.title') }}</h2>
          <p class="dfw-subtitle">{{ t('dailyFocusWidget.subtitle') }}</p>
        </div>
      </div>
      <div class="dfw-header-right">
        <!-- Filter project -->
        <select
          v-if="projectOptions.length > 1"
          v-model="selectedProjectId"
          class="dfw-project-select"
          :aria-label="t('dailyFocusWidget.filterAll')"
        >
          <option value="">{{ t('dailyFocusWidget.filterAll') }}</option>
          <option v-for="p in projectOptions" :key="p.id" :value="p.id">{{ p.name }}</option>
        </select>
        <!-- View all -->
        <router-link to="/priority" class="dfw-viewall-btn">
          {{ t('dailyFocusWidget.viewAll') }}
          <i class="fa-solid fa-arrow-right"></i>
        </router-link>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="dfw-state">
      <div class="dfw-spinner"></div>
      <span>{{ t('dailyFocusWidget.loading') }}</span>
    </div>

    <!-- Error -->
    <div v-else-if="error" class="dfw-state dfw-state--error">
      <i class="fa-solid fa-triangle-exclamation"></i>
      <span>{{ t('dailyFocusWidget.error') }}</span>
      <button class="dfw-retry-btn" type="button" @click="load">
        <i class="fa-solid fa-rotate-right"></i> {{ t('dailyFocusWidget.retry') }}
      </button>
    </div>

    <!-- Empty -->
    <div v-else-if="topTasks.length === 0" class="dfw-state dfw-state--empty">
      <i class="fa-regular fa-circle-check dfw-empty-icon"></i>
      <p class="dfw-empty-title">{{ t('dailyFocusWidget.empty') }}</p>
      <p class="dfw-empty-desc">{{ t('dailyFocusWidget.emptyDesc') }}</p>
    </div>

    <!-- Task list -->
    <ul v-else class="dfw-list" role="list">
      <li
        v-for="item in topTasks"
        :key="item.task.id"
        class="dfw-item"
        :class="[`dfw-item--${item.level}`, { 'dfw-item--postponed': isPostponed(item.task.id) }]"
        role="listitem"
      >
        <!-- Priority badge -->
        <span class="dfw-level-dot" :class="`dfw-dot--${item.level}`" :aria-label="item.level"></span>

        <!-- Task info -->
        <div class="dfw-item-body" @click="openTask(item.task)" role="button" tabindex="0" @keydown.enter="openTask(item.task)">
          <p class="dfw-item-title">{{ item.task.title }}</p>
          <div class="dfw-item-meta">
            <span class="dfw-project-badge" v-if="item.task.projectName">{{ item.task.projectName }}</span>
            <span class="dfw-deadline" :class="deadlineClass(item)">
              <i class="fa-regular fa-clock"></i>
              {{ deadlineLabel(item) }}
            </span>
            <span v-if="isPostponed(item.task.id)" class="dfw-postponed-badge">
              <i class="fa-regular fa-pause-circle"></i> {{ t('dailyFocusWidget.postponed') }} · {{ t('dailyFocusWidget.postponeLocal') }}
            </span>
          </div>
        </div>

        <!-- Actions -->
        <div class="dfw-item-actions">
          <button
            v-if="!isPostponed(item.task.id)"
            class="dfw-action-btn dfw-action-btn--start"
            type="button"
            @click.stop="openTask(item.task)"
            :title="t('dailyFocusWidget.start')"
          >
            <i class="fa-solid fa-arrow-up-right-from-square"></i>
          </button>
          <button
            v-if="!isPostponed(item.task.id)"
            class="dfw-action-btn dfw-action-btn--postpone"
            type="button"
            @click.stop="postpone(item.task)"
            :title="t('dailyFocusWidget.postponeLocal')"
          >
            <i class="fa-regular fa-clock"></i>
          </button>
        </div>
      </li>
    </ul>
  </section>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import { useI18n } from '@/composables/useI18n'

const router = useRouter()
const { t } = useI18n()
const props = defineProps({
  tasks: { type: Array, default: null },
  projectId: { type: String, default: '' }
})

// ── State ─────────────────────────────────────────────────────────────
const fetchedTasks = ref([])
const tasks = computed(() => props.tasks ?? fetchedTasks.value)
const loading = ref(false)
const error = ref(false)
const selectedProjectId = ref(props.projectId)

// ── Postpone - lưu localStorage với ghi chú rõ là "hoãn trên thiết bị hôm nay" ──
const todayKey = () => {
  const d = new Date()
  return `dfw_postponed_${d.getFullYear()}_${d.getMonth() + 1}_${d.getDate()}`
}

const postponedToday = ref((() => {
  try { return JSON.parse(localStorage.getItem(todayKey()) || '[]') }
  catch { return [] }
})())

const isPostponed = (taskId) => postponedToday.value.includes(taskId)

const postpone = (task) => {
  if (isPostponed(task.id)) return
  postponedToday.value = [...postponedToday.value, task.id]
  localStorage.setItem(todayKey(), JSON.stringify(postponedToday.value))
}

// ── Scoring helpers (mirror của DailyFocusView - không sửa logic) ────
const calcDaysLeft = (task) => {
  const dateStr = task.dueDate || task.plannedEndDate
  if (!dateStr) return null
  const due = new Date(dateStr)
  due.setHours(23, 59, 59, 999)
  return Math.ceil((due - new Date()) / (1000 * 60 * 60 * 24))
}

const urgencyScore = (daysLeft) => {
  if (daysLeft === null) return 10
  if (daysLeft <= 0) return 100
  if (daysLeft <= 1) return 80
  if (daysLeft <= 3) return 60
  if (daysLeft <= 7) return 40
  return 20
}

const importanceScore = (priority) => {
  if (priority === 1) return 40
  if (priority === 2) return 30
  if (priority === 3) return 20
  return 10
}

const levelFromScore = (score) => {
  if (score >= 80) return 'high'
  if (score >= 50) return 'medium'
  return 'low'
}

// ── Project options ───────────────────────────────────────────────────
const projectOptions = computed(() => {
  const map = new Map()
  tasks.value.forEach(t => {
    if (t.projectId && t.projectName) map.set(t.projectId, { id: t.projectId, name: t.projectName })
  })
  return [...map.values()]
})

// ── Scored & filtered ─────────────────────────────────────────────────
const EXCLUDED_STATUSES = ['DONE', 'CANCELLED', 'CANCELED', 'CLOSED']

const scoredTasks = computed(() => {
  const filtered = tasks.value.filter(task => {
    const s = (task.statusName || '').toUpperCase().trim()
    if (EXCLUDED_STATUSES.includes(s)) return false
    if (selectedProjectId.value && task.projectId !== selectedProjectId.value) return false
    return true
  })

  return filtered
    .map(task => {
      const daysLeft = calcDaysLeft(task)
      const score = urgencyScore(daysLeft) + importanceScore(task.priority || 3)
      const level = levelFromScore(score)
      return { task, daysLeft, score, level }
    })
    .sort((a, b) => b.score - a.score)
})

// Top 5: ưu tiên không postponed trước, kể cả postponed nếu không đủ
const topTasks = computed(() => {
  const active = scoredTasks.value.filter(i => !isPostponed(i.task.id))
  const postponed = scoredTasks.value.filter(i => isPostponed(i.task.id))
  return [...active, ...postponed].slice(0, 5)
})

// ── Labels ────────────────────────────────────────────────────────────
const deadlineLabel = (item) => {
  const d = item.daysLeft
  if (d === null) return t('dailyFocusWidget.noDeadline')
  if (d < 0) return `${t('dailyFocusWidget.overdue')} ${Math.abs(d)}d`
  if (d === 0) return t('dailyFocusWidget.today')
  if (d === 1) return t('dailyFocusWidget.tomorrow')
  return t('dailyFocusWidget.daysLeft', { days: d })
}

const deadlineClass = (item) => {
  const d = item.daysLeft
  if (d === null) return 'dfw-deadline--none'
  if (d <= 0) return 'dfw-deadline--overdue'
  if (d <= 1) return 'dfw-deadline--urgent'
  if (d <= 3) return 'dfw-deadline--warning'
  return 'dfw-deadline--ok'
}

// ── Actions ───────────────────────────────────────────────────────────
const openTask = (task) => {
  if (task.projectId) {
    router.push(`/space/${task.projectId}`)
  }
}

// ── Load ──────────────────────────────────────────────────────────────
const load = async () => {
  if (loading.value) return
  loading.value = true
  error.value = false
  try {
    const res = await axiosClient.get('/tasks/search')
    fetchedTasks.value = res.data?.data || []
  } catch (e) {
    error.value = true
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  if (props.tasks === null) load()
})
</script>

<style scoped>
/* ── Card container ── */
.dfw-card {
  background: var(--color-surface);
  border: 1px solid color-mix(in srgb, var(--color-border) 85%, transparent);
  border-radius: 12px;
  padding: 20px;
  box-shadow: 0 8px 28px color-mix(in srgb, #020617 6%, transparent);
  transition: box-shadow 0.2s;
}
.dfw-card:hover {
  box-shadow: 0 12px 40px color-mix(in srgb, #020617 10%, transparent);
}

/* ── Header ── */
.dfw-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
}
.dfw-header-left {
  display: flex;
  align-items: center;
  gap: 10px;
}
.dfw-fire {
  font-size: 22px;
  color: #f97316;
  filter: drop-shadow(0 0 5px rgba(249,115,22,0.4));
  flex-shrink: 0;
}
.dfw-title {
  margin: 0;
  font-size: 15px;
  font-weight: 700;
  color: var(--color-text-primary);
  line-height: 1.2;
}
.dfw-subtitle {
  margin: 2px 0 0;
  font-size: 11px;
  color: var(--color-text-muted);
}

.dfw-header-right {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.dfw-project-select {
  font-size: 12px;
  padding: 4px 8px;
  border-radius: 6px;
  border: 1px solid var(--color-border);
  background: var(--color-bg);
  color: var(--color-text-secondary);
  cursor: pointer;
  outline: none;
}
.dfw-project-select:focus {
  border-color: var(--color-accent);
}

.dfw-viewall-btn {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  font-size: 12px;
  font-weight: 600;
  color: var(--color-accent);
  text-decoration: none;
  padding: 5px 10px;
  border-radius: 6px;
  border: 1px solid color-mix(in srgb, var(--color-accent) 30%, transparent);
  background: color-mix(in srgb, var(--color-accent) 6%, transparent);
  transition: all 0.15s;
  white-space: nowrap;
}
.dfw-viewall-btn:hover {
  background: color-mix(in srgb, var(--color-accent) 14%, transparent);
  border-color: color-mix(in srgb, var(--color-accent) 50%, transparent);
}

/* ── State boxes ── */
.dfw-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 120px;
  padding: 20px;
  text-align: center;
  font-size: 13px;
  color: var(--color-text-muted);
}
.dfw-state--error { color: var(--color-danger, #ef4444); }
.dfw-state--empty { }

.dfw-spinner {
  width: 24px;
  height: 24px;
  border: 2px solid color-mix(in srgb, var(--color-accent) 25%, transparent);
  border-top-color: var(--color-accent);
  border-radius: 50%;
  animation: dfw-spin 0.7s linear infinite;
}
@keyframes dfw-spin {
  to { transform: rotate(360deg); }
}

.dfw-retry-btn {
  margin-top: 4px;
  padding: 5px 12px;
  font-size: 12px;
  font-weight: 600;
  border-radius: 6px;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: all 0.15s;
}
.dfw-retry-btn:hover { background: var(--color-surface-hover); }

.dfw-empty-icon {
  font-size: 28px;
  color: var(--color-success, #22c55e);
  opacity: 0.7;
}
.dfw-empty-title { margin: 0; font-size: 13px; font-weight: 600; color: var(--color-text-primary); }
.dfw-empty-desc { margin: 0; font-size: 11px; color: var(--color-text-muted); }

/* ── Task list ── */
.dfw-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.dfw-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border-radius: 8px;
  border: 1px solid color-mix(in srgb, var(--color-border) 70%, transparent);
  background: color-mix(in srgb, var(--color-surface) 60%, transparent);
  transition: background 0.15s, border-color 0.15s, transform 0.15s;
  cursor: pointer;
}
.dfw-item:hover {
  background: var(--color-surface-hover);
  border-color: color-mix(in srgb, var(--color-accent) 30%, var(--color-border));
  transform: translateX(2px);
}
.dfw-item--postponed {
  opacity: 0.55;
  filter: grayscale(0.3);
}

/* level color accent */
.dfw-item--high { border-left: 3px solid var(--color-danger, #ef4444); }
.dfw-item--medium { border-left: 3px solid var(--color-warning, #f59e0b); }
.dfw-item--low { border-left: 3px solid var(--color-border); }

/* ── Level dot ── */
.dfw-level-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}
.dfw-dot--high { background: var(--color-danger, #ef4444); box-shadow: 0 0 4px rgba(239,68,68,0.5); }
.dfw-dot--medium { background: var(--color-warning, #f59e0b); }
.dfw-dot--low { background: var(--color-text-muted); }

/* ── Item body ── */
.dfw-item-body {
  flex: 1;
  min-width: 0;
  cursor: pointer;
  outline: none;
}
.dfw-item-body:focus-visible {
  outline: 2px solid var(--color-accent);
  border-radius: 4px;
}
.dfw-item-title {
  margin: 0 0 4px;
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.dfw-item-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.dfw-project-badge {
  font-size: 10px;
  font-weight: 600;
  padding: 1px 6px;
  border-radius: 4px;
  background: color-mix(in srgb, var(--color-accent) 12%, transparent);
  color: var(--color-accent);
  white-space: nowrap;
  max-width: 120px;
  overflow: hidden;
  text-overflow: ellipsis;
}
.dfw-deadline {
  font-size: 11px;
  display: flex;
  align-items: center;
  gap: 3px;
  white-space: nowrap;
}
.dfw-deadline--overdue { color: var(--color-danger, #ef4444); font-weight: 700; }
.dfw-deadline--urgent  { color: var(--color-warning, #f59e0b); font-weight: 600; }
.dfw-deadline--warning { color: var(--color-warning, #f59e0b); }
.dfw-deadline--ok      { color: var(--color-text-muted); }
.dfw-deadline--none    { color: var(--color-text-muted); font-style: italic; }

.dfw-postponed-badge {
  font-size: 10px;
  color: var(--color-text-muted);
  font-style: italic;
}

/* ── Actions ── */
.dfw-item-actions {
  display: flex;
  gap: 4px;
  flex-shrink: 0;
  opacity: 0;
  transition: opacity 0.15s;
}
.dfw-item:hover .dfw-item-actions { opacity: 1; }

.dfw-action-btn {
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-bg);
  color: var(--color-text-secondary);
  cursor: pointer;
  font-size: 12px;
  transition: all 0.15s;
}
.dfw-action-btn--start:hover {
  background: color-mix(in srgb, var(--color-accent) 12%, transparent);
  border-color: var(--color-accent);
  color: var(--color-accent);
}
.dfw-action-btn--postpone:hover {
  background: color-mix(in srgb, var(--color-warning) 12%, transparent);
  border-color: var(--color-warning, #f59e0b);
  color: var(--color-warning, #f59e0b);
}

/* ── Responsive ── */
@media (max-width: 480px) {
  .dfw-header { flex-direction: column; }
  .dfw-header-right { width: 100%; justify-content: flex-end; }
  .dfw-item-title { font-size: 12px; }
}

@media (prefers-reduced-motion: reduce) {
  .dfw-spinner { animation: none; }
  .dfw-item { transition: none; }
  .dfw-card { transition: none; }
}
</style>
