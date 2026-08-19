<template>
  <div class="df-page">

      <!-- ══ HEADER ══ -->
      <header class="page-header">
        <div>
          <span class="eyebrow">{{ t('dailyFocus.eyebrow') }}</span>
          <h1><i class="fa-solid fa-fire df-fire-icon"></i> {{ t('dailyFocus.title') }}</h1>
          <p>{{ todayLabel }} · {{ t('dailyFocus.subtitle') }}</p>
        </div>
        <div class="df-header-right">
          <!-- Smart Filter -->
          <div class="df-filter-group">
            <label class="df-filter-label"><i class="fa-solid fa-filter"></i> {{ t('dailyFocus.smartFilterLabel') }}</label>
            <select id="df-smart-filter" v-model="filterLevel" class="df-select">
              <option value="all">{{ t('dailyFocus.filterAll') }}</option>
              <option value="high">{{ t('dailyFocus.filterHigh') }}</option>
              <option value="medium">{{ t('dailyFocus.filterMedium') }}</option>
              <option value="low">{{ t('dailyFocus.filterLow') }}</option>
            </select>
          </div>
          <!-- Auto-Sort toggle -->
          <button
            id="df-autosort-btn"
            class="df-toggle-btn"
            :class="{ active: autoSort }"
            type="button"
            @click="autoSort = !autoSort"
          >
            <i class="fa-solid fa-arrow-up-wide-short"></i>
            {{ t('dailyFocus.autoSort') }}
          </button>
          <!-- Refresh -->
          <button id="df-refresh-btn" class="df-icon-btn" type="button" :disabled="loading" @click="loadTasks" title="Reload">
            <i class="fa-solid fa-rotate-right" :class="{ 'fa-spin': loading }"></i>
          </button>
        </div>
      </header>

      <!-- ══ ERROR STATE ══ -->
      <div v-if="error && !loading" class="df-state-box df-error-box">
        <i class="fa-solid fa-triangle-exclamation"></i>
        <p>{{ t('dailyFocus.loadError') }}</p>
        <button class="df-retry-btn" type="button" @click="loadTasks">
          <i class="fa-solid fa-rotate-right"></i> {{ t('dailyFocus.retryBtn') }}
        </button>
      </div>

      <!-- ══ LOADING STATE ══ -->
      <div v-else-if="loading" class="df-state-box">
        <div class="df-spinner"></div>
        <p>{{ t('dailyFocus.loading') }}</p>
      </div>

      <!-- ══ MAIN CONTENT ══ -->
      <template v-else>

        <!-- TODAY's FOCUS – HIGH PRIORITY -->
        <section class="df-section">
          <div class="df-section-head">
            <span class="df-section-badge badge-high">{{ t('dailyFocus.sectionHigh') }}</span>
            <span class="df-section-count">{{ highTasks.length }} {{ t('dailyFocus.countWork') }}</span>
          </div>

          <!-- Empty (tích cực) khi không còn việc cao nào -->
          <div v-if="highTasks.length === 0 && filterLevel === 'all'" class="df-empty-celebrate">
            <span class="df-celebrate-icon">🎉</span>
            <p class="df-celebrate-title">{{ t('dailyFocus.emptyAll') }}</p>
            <p class="df-celebrate-sub">{{ t('dailyFocus.emptyAllSub') }}</p>
          </div>
          <div v-else-if="highTasks.length === 0 && filterLevel === 'high'" class="df-empty-celebrate">
            <span class="df-celebrate-icon">🎉</span>
            <p class="df-celebrate-title">{{ t('dailyFocus.emptyHigh') }}</p>
          </div>

          <div v-else class="df-task-list">
            <PriorityAIPanel
              v-for="item in highTasks"
              :key="item.task.id"
              :task="item.task"
              :priority-level="item.level"
              :days-left="item.daysLeft"
              :score="item.score"
              :is-postponed="isPostponed(item.task.id)"
              @start="handleStart(item.task)"
              @postpone="handlePostpone(item.task)"
              @ai-hint="handleAiHint(item)"
            />
          </div>
        </section>

        <!-- MEDIUM PRIORITY -->
        <section v-if="filterLevel === 'all' || filterLevel === 'medium'" class="df-section">
          <div class="df-section-head">
            <span class="df-section-badge badge-medium">{{ t('dailyFocus.sectionMedium') }}</span>
            <span class="df-section-count">{{ mediumTasks.length }} {{ t('dailyFocus.countWork') }}</span>
          </div>

          <div v-if="mediumTasks.length === 0" class="df-section-empty">
            <i class="fa-regular fa-circle-check"></i> {{ t('dailyFocus.emptyMedium') }}
          </div>
          <div v-else class="df-risk-list">
            <div
              v-for="item in mediumTasks"
              :key="item.task.id"
              class="df-risk-row"
              :class="{ 'postponed': isPostponed(item.task.id) }"
            >
              <span class="df-risk-icon">•</span>
              <span class="df-risk-title" @click="handleStart(item.task)">{{ item.task.title }}</span>
              <span v-if="item.daysLeft !== null" class="df-risk-deadline">
                {{ item.daysLeft === 0 ? t('dailyFocus.deadlineToday') : item.daysLeft === 1 ? t('dailyFocus.deadlineTomorrow') : t('dailyFocus.deadlineDays', { days: item.daysLeft }) }}
              </span>
              <span v-else class="df-risk-deadline df-no-deadline">{{ t('dailyFocus.noDeadline') }}</span>
              <button class="df-mini-btn" type="button" @click="handlePostpone(item.task)" v-if="!isPostponed(item.task.id)">{{ t('dailyFocus.postponeBtn') }}</button>
              <span v-else class="df-postponed-badge">{{ t('dailyFocus.postponedBadge') }}</span>
            </div>
          </div>
        </section>

        <!-- LOW PRIORITY -->
        <section v-if="filterLevel === 'all' || filterLevel === 'low'" class="df-section">
          <div class="df-section-head">
            <span class="df-section-badge badge-low">{{ t('dailyFocus.sectionLow') }}</span>
            <span class="df-section-count">{{ lowTasks.length }} {{ t('dailyFocus.countWork') }}</span>
          </div>
          <div v-if="lowTasks.length === 0" class="df-section-empty">
            <i class="fa-regular fa-circle-check"></i> {{ t('dailyFocus.emptyLow') }}
          </div>
          <div v-else class="df-risk-list">
            <div
              v-for="item in lowTasks"
              :key="item.task.id"
              class="df-risk-row df-risk-row--low"
            >
              <span class="df-risk-icon">•</span>
              <span class="df-risk-title" @click="handleStart(item.task)">{{ item.task.title }}</span>
              <span v-if="item.daysLeft !== null" class="df-risk-deadline">{{ t('dailyFocus.deadlineDaysLeft', { days: item.daysLeft }) }}</span>
              <span v-else class="df-risk-deadline df-no-deadline">{{ t('dailyFocus.noDeadline') }}</span>
            </div>
          </div>
        </section>

        <!-- STATS FOOTER -->
        <div class="df-stats-row">
          <div class="df-stat-card">
            <span class="df-stat-num">{{ scoredTasks.length }}</span>
            <span class="df-stat-lbl">{{ t('dailyFocus.statTotal') }}</span>
          </div>
          <div class="df-stat-card">
            <span class="df-stat-num df-stat-high">{{ highTasks.length }}</span>
            <span class="df-stat-lbl">{{ t('dailyFocus.statHigh') }}</span>
          </div>
          <div class="df-stat-card">
            <span class="df-stat-num df-stat-medium">{{ mediumTasks.length }}</span>
            <span class="df-stat-lbl">{{ t('dailyFocus.statMedium') }}</span>
          </div>
          <div class="df-stat-card">
            <span class="df-stat-num">{{ postponedToday.length }}</span>
            <span class="df-stat-lbl">{{ t('dailyFocus.statPostponed') }}</span>
          </div>
        </div>
      </template>

      <!-- ══ AI HINT POPUP ══ -->
      <transition name="df-popup-fade">
        <div v-if="aiHintItem" class="df-ai-popup-overlay" @click.self="aiHintItem = null">
          <div class="df-ai-popup" role="dialog" aria-modal="true" :aria-label="t('dailyFocus.aiHintTitle')">
            <div class="df-ai-popup-head">
              <span class="df-ai-popup-icon">🤖</span>
              <strong>{{ t('dailyFocus.aiHintTitle') }}</strong>
              <button class="df-ai-close-btn" type="button" @click="aiHintItem = null">
                <i class="fa-solid fa-xmark"></i>
              </button>
            </div>
            <div class="df-ai-popup-body">
              <div class="df-ai-task-name">{{ aiHintItem.task.title }}</div>
              <ul class="df-ai-reasons">
                <li v-for="reason in aiHintItem.reasons" :key="reason">
                  <i class="fa-solid fa-circle-arrow-right"></i> {{ reason }}
                </li>
              </ul>
              <div class="df-ai-score-bar">
                <span class="df-ai-score-label">{{ t('dailyFocus.aiHintScore') }}</span>
                <div class="df-ai-bar-track">
                  <div
                    class="df-ai-bar-fill"
                    :style="{ width: Math.min(aiHintItem.score / 140 * 100, 100) + '%' }"
                    :class="aiHintItem.level"
                  ></div>
                </div>
                <span class="df-ai-score-val">{{ aiHintItem.score }}/140</span>
              </div>
              <p class="df-ai-suggestion">
                {{ aiHintItem.suggestion }}
              </p>
            </div>
          </div>
        </div>
      </transition>

      <!-- ══ POSTPONE CONFIRM TOAST ══ -->
      <transition name="df-toast">
        <div v-if="postponeToast" class="df-toast">
          <i class="fa-solid fa-clock"></i> {{ t('dailyFocus.postponeToast', { title: postponeToast }) }}
        </div>
      </transition>

  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import PriorityAIPanel from '@/components/PriorityAIPanel.vue'
import axiosClient from '@/api/axiosClient'
import { ElMessage } from 'element-plus'

import { useI18n } from '@/composables/useI18n'
import { buildSpacePath } from '@/utils/spaceRoute'

const router = useRouter()
const { t } = useI18n()

// ── State ──────────────────────────────────────────────────────────────────
const tasks = ref([])
const loading = ref(false)
const error = ref(false)
const filterLevel = ref('all')
const autoSort = ref(true)
const aiHintItem = ref(null)
const postponeToast = ref('')

// ── Helpers ────────────────────────────────────────────────────────────────

/** Tính số ngày còn đến deadline (dương = còn, 0 = hôm nay, âm = quá hạn) */
const calcDaysLeft = (task) => {
  const dateStr = task.dueDate || task.plannedEndDate
  if (!dateStr) return null
  const due = new Date(dateStr)
  due.setHours(23, 59, 59, 999)
  const now = new Date()
  return Math.ceil((due - now) / (1000 * 60 * 60 * 24))
}

/** Tính urgency score từ số ngày còn lại */
const urgencyScore = (daysLeft) => {
  if (daysLeft === null) return 10
  if (daysLeft <= 0) return 100
  if (daysLeft <= 1) return 80
  if (daysLeft <= 3) return 60
  if (daysLeft <= 7) return 40
  return 20
}

/** Tính importance score từ priority field (1=Urgent…4=Low) */
const importanceScore = (priority) => {
  if (priority === 1) return 40
  if (priority === 2) return 30
  if (priority === 3) return 20
  return 10
}

/** Map score → level */
const levelFromScore = (score) => {
  if (score >= 80) return 'high'
  if (score >= 50) return 'medium'
  return 'low'
}

/** Lý do AI gợi ý */
const buildReasons = (task, daysLeft, score) => {
  const reasons = []
  if (daysLeft !== null && daysLeft <= 0) reasons.push(t('dailyFocus.aiReasonOverdue'))
  else if (daysLeft !== null && daysLeft <= 1) reasons.push(t('dailyFocus.aiReasonUrgent', { days: daysLeft === 0 ? t('dailyFocus.deadlineToday') : t('dailyFocus.deadlineTomorrow') }))
  else if (daysLeft !== null && daysLeft <= 3) reasons.push(t('dailyFocus.aiReasonWarning', { days: daysLeft }))
  else if (daysLeft !== null) reasons.push(t('dailyFocus.aiReasonNormal', { days: daysLeft }))
  else reasons.push(t('dailyFocus.aiReasonNoDeadline'))

  const p = task.priority || 3
  const importanceText = t(`dailyFocus.aiImportance${p}`)
  reasons.push(t('dailyFocus.aiReasonImportance', { p: importanceText }))
  if (task.statusName === 'IN PROGRESS' || task.statusName === 'INPROGRESS') {
    reasons.push(t('dailyFocus.aiReasonProgress'))
  }
  return reasons
}

const buildSuggestion = (item) => {
  if (item.daysLeft !== null && item.daysLeft <= 0) return t('dailyFocus.aiSuggestionOverdue')
  if (item.level === 'high') return t('dailyFocus.aiSuggestionHigh')
  if (item.level === 'medium') return t('dailyFocus.aiSuggestionMedium')
  return t('dailyFocus.aiSuggestionLow')
}

// ── Postpone persistence ───────────────────────────────────────────────────
const todayKey = () => {
  const d = new Date()
  return `postponed_${d.getFullYear()}_${d.getMonth() + 1}_${d.getDate()}`
}

const postponedToday = ref((() => {
  try { return JSON.parse(localStorage.getItem(todayKey()) || '[]') }
  catch { return [] }
})())

const isPostponed = (taskId) => postponedToday.value.includes(taskId)

const persistPostponed = () => {
  localStorage.setItem(todayKey(), JSON.stringify(postponedToday.value))
}

// ── Computed ───────────────────────────────────────────────────────────────
const scoredTasks = computed(() => {
  const EXCLUDED = ['DONE', 'CANCELLED', 'CANCELED', 'CLOSED']
  const active = tasks.value.filter(t => {
    const s = (t.statusName || '').toUpperCase().trim()
    return !EXCLUDED.includes(s) && !isPostponed(t.id)
  })

  const scored = active.map(task => {
    const daysLeft = calcDaysLeft(task)
    const score = urgencyScore(daysLeft) + importanceScore(task.priority || 3)
    const level = levelFromScore(score)
    const reasons = buildReasons(task, daysLeft, score)
    return { task, daysLeft, score, level, reasons, suggestion: '' }
  }).map(item => ({ ...item, suggestion: buildSuggestion(item) }))

  return autoSort.value
    ? scored.sort((a, b) => b.score - a.score)
    : scored
})

const highTasks = computed(() =>
  scoredTasks.value.filter(i => i.level === 'high')
    .filter(i => filterLevel.value === 'all' || filterLevel.value === 'high')
)

const mediumTasks = computed(() =>
  scoredTasks.value.filter(i => i.level === 'medium')
)

const lowTasks = computed(() =>
  scoredTasks.value.filter(i => i.level === 'low')
)

const todayLabel = computed(() => {
  return new Date().toLocaleDateString('vi-VN', {
    weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric'
  })
})

// ── Actions ────────────────────────────────────────────────────────────────
const loadTasks = async () => {
  loading.value = true
  error.value = false
  try {
    const res = await axiosClient.get('/tasks/search')
    tasks.value = res.data?.data || []
  } catch (e) {
    console.error('DailyFocus: failed to load tasks', e)
    error.value = true
  } finally {
    loading.value = false
  }
}

const handleStart = (task) => {
  if (!task.projectId) {
    ElMessage.warning('Công việc này chưa thuộc project nào.')
    return
  }
  router.push(buildSpacePath(task.projectId, 'work-items'))
}

const handlePostpone = (task) => {
  if (isPostponed(task.id)) return
  postponedToday.value.push(task.id)
  persistPostponed()
  postponeToast.value = task.title
  setTimeout(() => { postponeToast.value = '' }, 3000)
}

const handleAiHint = (item) => {
  aiHintItem.value = item
}

// ── Mount ──────────────────────────────────────────────────────────────────
onMounted(loadTasks)
</script>

<style scoped>
/* ════════════════════════════════════════════════
   BASE LAYOUT
════════════════════════════════════════════════ */
.df-page {
  min-height: 100%;
  width: 100%;
  padding: 0 !important;
  margin: 0 !important;
  background: var(--color-bg);
  color: var(--color-text-primary);
  animation: df-rise 480ms cubic-bezier(0.2, 0.8, 0.2, 1) both;
}

@keyframes df-rise {
  from { opacity: 0; transform: translateY(14px); }
  to   { opacity: 1; transform: translateY(0); }
}

/* ════════════════════════════════════════════════
   HEADER
════════════════════════════════════════════════ */
.page-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 20px;
  padding: 22px var(--sa-page-x, 24px) 18px;
  background: var(--color-surface);
  border-bottom: none !important;
  margin-bottom: 0 !important;
}

.eyebrow {
  color: var(--color-accent);
  font-size: 10px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  display: block;
}

.page-header h1 {
  margin: 3px 0 4px;
  font-size: 22px;
  font-weight: 700;
  color: var(--color-text-primary);
  letter-spacing: 0;
  line-height: 1.2;
  display: flex;
  align-items: center;
  gap: 8px;
}

.df-fire-icon {
  color: #f97316;
  filter: drop-shadow(0 0 6px rgba(249,115,22,.45));
}

.page-header p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 12px;
}

.df-header-right {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-shrink: 0;
}

.df-filter-group {
  display: flex;
  align-items: center;
  gap: 6px;
}

.df-filter-label {
  font-size: 12px;
  color: var(--color-text-muted);
}

.df-select {
  padding: 6px 10px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface);
  color: var(--color-text-primary);
  font-size: 13px;
  cursor: pointer;
  outline: none;
  transition: border-color .2s;
}
.df-select:focus { border-color: var(--color-accent); }

.df-toggle-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: all .2s;
}
.df-toggle-btn.active {
  border-color: var(--color-accent);
  color: var(--color-accent);
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface));
}

.df-icon-btn {
  width: 34px;
  height: 34px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all .2s;
}
.df-icon-btn:hover { border-color: var(--color-accent); color: var(--color-accent); }
.df-icon-btn:disabled { opacity: .5; cursor: not-allowed; }

/* ════════════════════════════════════════════════
   SECTIONS
════════════════════════════════════════════════ */
.df-section {
  max-width: 1080px;
  margin: 0 auto 16px;
}

.df-section-head {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 10px;
}

.df-section-badge {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 4px 12px;
  border-radius: 20px;
  font-size: 12px;
  font-weight: 700;
  letter-spacing: .02em;
}
.badge-high {
  background: color-mix(in srgb, var(--color-danger) 15%, transparent);
  color: var(--color-danger);
  border: 1px solid color-mix(in srgb, var(--color-danger) 30%, transparent);
}
.badge-medium {
  background: color-mix(in srgb, var(--color-warning) 15%, transparent);
  color: var(--color-warning);
  border: 1px solid color-mix(in srgb, var(--color-warning) 30%, transparent);
}
.badge-low {
  background: color-mix(in srgb, var(--color-border) 40%, transparent);
  color: var(--color-text-muted);
  border: 1px solid var(--color-border);
}

.df-section-count {
  font-size: 12px;
  color: var(--color-text-muted);
}

.df-section-empty {
  padding: 12px 16px;
  color: var(--color-text-muted);
  font-size: 13px;
  display: flex;
  align-items: center;
  gap: 8px;
  background: var(--color-surface);
  border: 1px dashed var(--color-border);
  border-radius: 8px;
}

/* ════════════════════════════════════════════════
   TASK LIST
════════════════════════════════════════════════ */
.df-task-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

/* ════════════════════════════════════════════════
   RISK LIST (medium & low)
════════════════════════════════════════════════ */
.df-risk-list {
  display: flex;
  flex-direction: column;
  gap: 2px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  overflow: hidden;
}

.df-risk-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  border-bottom: 1px solid color-mix(in srgb, var(--color-border) 50%, transparent);
  transition: background .15s;
}
.df-risk-row:last-child { border-bottom: none; }
.df-risk-row:hover { background: var(--color-surface-hover); }
.df-risk-row.postponed { opacity: .5; }
.df-risk-row--low { opacity: .8; }

.df-risk-icon { color: var(--color-warning); font-size: 14px; flex-shrink: 0; }
.df-risk-row--low .df-risk-icon { color: var(--color-text-muted); }

.df-risk-title {
  flex: 1;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: color .15s;
}
.df-risk-title:hover { color: var(--color-accent); }

.df-risk-deadline {
  font-size: 11px;
  padding: 3px 8px;
  border-radius: 10px;
  background: color-mix(in srgb, var(--color-warning) 12%, transparent);
  color: var(--color-warning);
  font-weight: 600;
  white-space: nowrap;
}
.df-no-deadline {
  background: color-mix(in srgb, var(--color-border) 40%, transparent);
  color: var(--color-text-muted);
}

.df-mini-btn {
  padding: 3px 10px;
  border: 1px solid var(--color-border);
  border-radius: 5px;
  background: transparent;
  color: var(--color-text-muted);
  font-size: 11px;
  cursor: pointer;
  transition: all .2s;
  white-space: nowrap;
}
.df-mini-btn:hover { border-color: var(--color-accent); color: var(--color-accent); }

.df-postponed-badge {
  font-size: 11px;
  color: var(--color-text-muted);
  font-style: italic;
}

/* ════════════════════════════════════════════════
   STATS FOOTER
════════════════════════════════════════════════ */
.df-stats-row {
  max-width: 1080px;
  margin: 8px auto 0;
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.df-stat-card {
  flex: 1;
  min-width: 120px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 12px 14px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.df-stat-num {
  font-size: 26px;
  font-weight: 700;
  line-height: 1;
}
.df-stat-high { color: var(--color-danger); }
.df-stat-medium { color: var(--color-warning); }

.df-stat-lbl {
  font-size: 11px;
  color: var(--color-text-muted);
}

/* ════════════════════════════════════════════════
   STATE BOXES (loading / error)
════════════════════════════════════════════════ */
.df-state-box {
  max-width: 1080px;
  margin: 48px auto;
  min-height: 200px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 14px;
  background: var(--color-surface);
  border: 1px dashed var(--color-border);
  border-radius: 12px;
  color: var(--color-text-secondary);
  font-size: 14px;
}

.df-error-box { border-color: color-mix(in srgb, var(--color-danger) 40%, transparent); }
.df-error-box i { font-size: 28px; color: var(--color-danger); }

.df-retry-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 18px;
  border: 1px solid var(--color-danger);
  border-radius: 7px;
  background: color-mix(in srgb, var(--color-danger) 8%, transparent);
  color: var(--color-danger);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all .2s;
}
.df-retry-btn:hover { background: color-mix(in srgb, var(--color-danger) 18%, transparent); }

.df-spinner {
  width: 36px;
  height: 36px;
  border: 3px solid var(--color-border);
  border-top-color: var(--color-accent);
  border-radius: 50%;
  animation: df-spin 0.8s linear infinite;
}
@keyframes df-spin { to { transform: rotate(360deg); } }

/* ════════════════════════════════════════════════
   EMPTY CELEBRATE
════════════════════════════════════════════════ */
.df-empty-celebrate {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  padding: 32px;
  background: color-mix(in srgb, var(--color-success) 6%, var(--color-surface));
  border: 1px dashed color-mix(in srgb, var(--color-success) 40%, transparent);
  border-radius: 12px;
  text-align: center;
}
.df-celebrate-icon { font-size: 32px; }
.df-celebrate-title { font-size: 15px; font-weight: 600; color: var(--color-success); margin: 0; }
.df-celebrate-sub { font-size: 12px; color: var(--color-text-muted); margin: 0; }

/* ════════════════════════════════════════════════
   AI HINT POPUP
════════════════════════════════════════════════ */
.df-ai-popup-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,.45);
  backdrop-filter: none;
  -webkit-backdrop-filter: none;
  z-index: 1000;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
}

.df-ai-popup {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 14px;
  max-width: 440px;
  width: 100%;
  box-shadow: 0 24px 60px rgba(0,0,0,.25);
  overflow: hidden;
}

.df-ai-popup-head {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 16px 20px;
  background: color-mix(in srgb, var(--color-accent) 8%, var(--color-surface));
  border-bottom: 1px solid var(--color-border);
}
.df-ai-popup-icon { font-size: 20px; }
.df-ai-popup-head strong { flex: 1; font-size: 14px; }

.df-ai-close-btn {
  width: 28px;
  height: 28px;
  border: none;
  border-radius: 6px;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all .15s;
}
.df-ai-close-btn:hover { background: var(--color-surface-hover); color: var(--color-text-primary); }

.df-ai-popup-body {
  padding: 20px;
}

.df-ai-task-name {
  font-size: 15px;
  font-weight: 700;
  margin-bottom: 14px;
  color: var(--color-text-primary);
}

.df-ai-reasons {
  list-style: none;
  padding: 0;
  margin: 0 0 18px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.df-ai-reasons li {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  font-size: 13px;
  color: var(--color-text-secondary);
  line-height: 1.5;
}
.df-ai-reasons li i { color: var(--color-accent); flex-shrink: 0; margin-top: 2px; }

.df-ai-score-bar {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 14px;
}
.df-ai-score-label { font-size: 11px; color: var(--color-text-muted); white-space: nowrap; }
.df-ai-bar-track {
  flex: 1;
  height: 8px;
  background: var(--color-border);
  border-radius: 4px;
  overflow: hidden;
}
.df-ai-bar-fill {
  height: 100%;
  border-radius: 4px;
  transition: width .6s ease;
}
.df-ai-bar-fill.high { background: var(--color-danger); }
.df-ai-bar-fill.medium { background: var(--color-warning); }
.df-ai-bar-fill.low { background: var(--color-accent); }
.df-ai-score-val { font-size: 12px; font-weight: 700; white-space: nowrap; }

.df-ai-suggestion {
  margin: 0;
  padding: 12px;
  background: color-mix(in srgb, var(--color-accent) 8%, var(--color-surface));
  border-left: 3px solid var(--color-accent);
  border-radius: 0 6px 6px 0;
  font-size: 13px;
  color: var(--color-text-secondary);
  line-height: 1.6;
}

/* ════════════════════════════════════════════════
   TOAST
════════════════════════════════════════════════ */
.df-toast {
  position: fixed;
  bottom: 24px;
  left: 50%;
  transform: translateX(-50%);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 12px 20px;
  font-size: 13px;
  color: var(--color-text-primary);
  box-shadow: 0 8px 28px rgba(0,0,0,.18);
  display: flex;
  align-items: center;
  gap: 8px;
  z-index: 2000;
  white-space: nowrap;
}
.df-toast i { color: var(--color-warning); }

/* ════════════════════════════════════════════════
   TRANSITIONS
════════════════════════════════════════════════ */
.df-popup-fade-enter-active,
.df-popup-fade-leave-active { transition: opacity .2s, transform .2s; }
.df-popup-fade-enter-from,
.df-popup-fade-leave-to { opacity: 0; transform: scale(.96); }

.df-toast-enter-active,
.df-toast-leave-active { transition: all .3s; }
.df-toast-enter-from,
.df-toast-leave-to { opacity: 0; transform: translateX(-50%) translateY(12px); }

/* ════════════════════════════════════════════════
   RESPONSIVE
════════════════════════════════════════════════ */
@media (max-width: 768px) {
  .df-page { padding: 16px; }
  .df-header { flex-direction: column; }
  .df-header-right { flex-wrap: wrap; }
  .df-stats-row { gap: 8px; }
}
</style>
