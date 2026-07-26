<template>
  <div class="recent-page">
    <header class="page-header">
      <h1>{{ t('Recent') }}</h1>

      <div class="tabs">
        <button class="tab-btn" :class="{ active: activeTab === 'worked' }" @click="activeTab = 'worked'">
          {{ t('Worked on') }}
        </button>
        <button class="tab-btn" :class="{ active: activeTab === 'viewed' }" @click="activeTab = 'viewed'">
          {{ t('Viewed') }}
        </button>
      </div>
    </header>

    <div class="page-content">
      <div class="filter-container">
        <div class="search-input-wrapper">
          <i class="fa-solid fa-magnifying-glass search-icon"></i>
          <input v-model="searchQuery" type="text" :placeholder="t('Filter by title')" class="search-input" />
        </div>
      </div>

      <div class="audit-list" v-if="!isLoading">
        <div class="time-group" v-for="group in groupedActivities" :key="group.label">
          <h3 class="time-label">{{ t(group.label) }}</h3>
          <div class="audit-item" v-for="act in group.items" :key="act.id" :class="{ clickable: act.url }" @click="goToItem(act)">
            <div class="item-icon light-blue square"><i :class="act.icon"></i></div>
            <div class="item-details">
              <div class="item-title">{{ act.bold || act.text }}</div>
              <div class="item-path">{{ act.text }}</div>
            </div>
            <div class="item-meta">
              <span class="status-badge done">DONE</span>
              <span class="time-ago">{{ formatActivityTime(act) }}</span>
            </div>
          </div>
        </div>

        <div v-if="filteredItems.length === 0" class="empty-state">
          {{ emptyLabel }}
        </div>
      </div>
      <div v-else class="empty-state">
        {{ labels.loading }}
      </div>
      <div v-if="activeTab === 'viewed' && starredStore.recentError" class="empty-state">
        <p>{{ starredStore.recentError }}</p>
        <button type="button" @click="loadRecentPage(currentPage)">{{ labels.retry }}</button>
      </div>
      <div v-if="activeTab === 'viewed' && starredStore.recentPagination.totalCount > pageSize" class="pagination">
        <button type="button" :disabled="currentPage <= 1 || isLoading" @click="loadRecentPage(currentPage - 1)">‹</button>
        <span>{{ currentPage }} / {{ totalPages }}</span>
        <button type="button" :disabled="currentPage >= totalPages || isLoading" @click="loadRecentPage(currentPage + 1)">›</button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18nStore } from '@/store/useI18nStore'
import { useActivityStore } from '@/store/useActivityStore'
import { useStarredStore } from '@/store/useStarredStore'

const i18nStore = useI18nStore()
const t = (key) => i18nStore.t(key)

const activityStore = useActivityStore()
const starredStore = useStarredStore()
const router = useRouter()
const activeTab = ref('worked')
const searchQuery = ref('')
const currentPage = ref(1)
const pageSize = 20

const labels = computed(() => i18nStore.locale === 'vi'
  ? { empty: 'Không có hoạt động nào gần đây.', loading: 'Đang tải dữ liệu...', retry: 'Thử lại' }
  : { empty: 'No recent activity.', loading: 'Loading data...', retry: 'Retry' })
const emptyLabel = computed(() => {
  if (activeTab.value !== 'viewed') return labels.value.empty
  return i18nStore.locale === 'vi'
    ? 'Bạn chưa xem dự án hoặc công việc nào gần đây.'
    : 'You have not viewed any projects or work items recently.'
})

const formatDateTime = (value) => {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleString('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const formatActivityTime = (activity) => {
  if (activity?._ts) return formatDateTime(activity._ts)
  const parsed = Date.parse(activity?.time)
  if (!Number.isNaN(parsed)) return formatDateTime(parsed)
  return activity?.time || ''
}

const recentViews = computed(() => starredStore.recentItems.map((item) => ({
      id: item.id || `${item.entityType}:${item.entityId}`,
      icon: item.icon || 'fa-regular fa-eye',
      text: item.subtitle || item.entityType || '',
      bold: item.title,
      time: formatDateTime(item.viewedAt),
      _ts: Date.parse(item.viewedAt) || Date.now(),
      url: item.url
    })))
const loadRecentPage = async (page) => {
  currentPage.value = page
  await starredStore.fetchRecentItems({ page, pageSize }).catch(() => {})
}
const totalPages = computed(() => Math.max(1, Math.ceil(starredStore.recentPagination.totalCount / pageSize)))

onMounted(async () => {
  await Promise.all([
    activityStore.fetchRecentActivities({ limit: 50 }),
    loadRecentPage(1)
  ])
})

const isLoading = computed(() => activeTab.value === 'worked' ? activityStore.loading : starredStore.recentLoading)

const filteredItems = computed(() => {
  const source = activeTab.value === 'worked' ? activityStore.activities : recentViews.value
  const q = searchQuery.value.trim().toLowerCase()
  if (!q) return source
  return source.filter((item) => `${item.bold || ''} ${item.text || ''}`.toLowerCase().includes(q))
})

const groupedActivities = computed(() => {
  const groups = { today: [], thisWeek: [], older: [] }
  const now = Date.now()
  const oneDay = 24 * 60 * 60 * 1000
  const oneWeek = 7 * oneDay

  filteredItems.value.forEach((act) => {
    const age = now - (act._ts || Date.parse(act.time) || now)
    if (age < oneDay) groups.today.push(act)
    else if (age < oneWeek) groups.thisWeek.push(act)
    else groups.older.push(act)
  })

  const result = []
  if (groups.today.length) result.push({ label: 'Today', items: groups.today })
  if (groups.thisWeek.length) result.push({ label: 'This week', items: groups.thisWeek })
  if (groups.older.length) result.push({ label: 'Older', items: groups.older })
  return result
})

const goToItem = (item) => {
  if (item.url) router.push(item.url)
}
</script>

<style scoped>
.recent-page {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  background: var(--home-bg, #ffffff);
  color: var(--home-text, #172b4d);
}

.page-header {
  padding: 32px 40px 0;
  border-bottom: 1px solid var(--home-border, #dfe1e6);
}

.page-header h1 {
  margin: 0 0 24px;
  color: var(--home-text, #172b4d);
  font-size: 28px;
  font-weight: 800;
}

.tabs {
  display: flex;
  gap: 8px;
}

.tab-btn {
  border: 1px solid transparent;
  border-bottom: 0;
  border-radius: 8px 8px 0 0;
  padding: 10px 14px;
  background: transparent;
  color: var(--home-muted, #5e6c84);
  cursor: pointer;
  font-size: 14px;
  font-weight: 700;
}

.tab-btn:hover,
.tab-btn.active {
  background: var(--home-panel, #ffffff);
  border-color: var(--home-border, #dfe1e6);
  color: var(--home-accent, #0052cc);
}

.tab-btn.active {
  box-shadow: inset 0 -2px 0 var(--home-accent, #0052cc);
}

.page-content {
  max-width: 1040px;
  padding: 32px 40px;
}

.filter-container {
  margin-bottom: 24px;
}

.search-input-wrapper {
  position: relative;
  width: min(100%, 320px);
}

.search-icon {
  position: absolute;
  left: 12px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--home-muted, #5e6c84);
  font-size: 14px;
}

.search-input {
  width: 100%;
  box-sizing: border-box;
  padding: 10px 12px 10px 40px;
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 8px;
  outline: none;
  background: var(--home-panel, #ffffff);
  color: var(--home-text, #091e42);
  font-size: 14px;
}

.search-input:focus {
  border-color: var(--home-accent, #4c9aff);
  box-shadow: 0 0 0 3px rgba(56, 189, 248, 0.14);
}

.audit-list {
  display: flex;
  flex-direction: column;
  gap: 32px;
}

.time-label {
  margin: 0 0 12px;
  color: var(--home-muted, #5e6c84);
  font-size: 12px;
  font-weight: 800;
}

.audit-item {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 14px 16px;
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 10px;
  background: var(--home-panel, #ffffff);
  margin-bottom: 10px;
}

.audit-item.clickable {
  cursor: pointer;
}

.audit-item.clickable:hover {
  background: var(--home-panel-strong, #fafbfc);
  border-color: rgba(56, 189, 248, 0.55);
}

.item-icon {
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 8px;
  font-size: 12px;
}

.light-blue {
  background: rgba(56, 189, 248, 0.18);
  color: var(--home-accent, #00b8d9);
}

.item-details {
  flex: 1;
  min-width: 0;
}

.item-title {
  margin-bottom: 2px;
  color: var(--home-text, #172b4d);
  font-size: 14px;
  font-weight: 800;
}

.item-path {
  color: var(--home-muted, #5e6c84);
  font-size: 12px;
}

.item-meta {
  display: flex;
  align-items: center;
  gap: 16px;
}

.status-badge {
  padding: 3px 8px;
  border-radius: 999px;
  background: rgba(34, 197, 94, 0.16);
  color: #00875a;
  font-size: 11px;
  font-weight: 800;
}

.time-ago {
  min-width: 128px;
  color: var(--home-muted, #5e6c84);
  text-align: right;
  font-size: 12px;
}

.empty-state {
  padding: 40px 16px;
  border: 1px dashed var(--home-border, #dfe1e6);
  border-radius: 12px;
  color: var(--home-muted, #5e6c84);
  text-align: center;
}
</style>
