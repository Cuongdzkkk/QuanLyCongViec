<template>
  <main class="tool-page">
    <header class="page-header">
      <span class="eyebrow">{{ labels.eyebrow }}</span>
      <h1>{{ labels.title }}</h1>
      <p>{{ labels.description }}</p>

      <div class="tabs" role="tablist" :aria-label="labels.tabs">
        <button
          class="tab-button"
          type="button"
          role="tab"
          :aria-selected="activeTab === 'worked'"
          :class="{ active: activeTab === 'worked' }"
          @click="selectTab('worked')"
        >
          {{ labels.worked }}
        </button>
        <button
          class="tab-button"
          type="button"
          role="tab"
          :aria-selected="activeTab === 'viewed'"
          :class="{ active: activeTab === 'viewed' }"
          @click="selectTab('viewed')"
        >
          {{ labels.viewed }}
        </button>
      </div>
    </header>

    <section class="page-content">
      <div class="filter-search-field">
        <span class="sr-only">{{ labels.search }}</span>
        <i class="fa-solid fa-magnifying-glass filter-search-icon" aria-hidden="true"></i>
        <input v-model="searchQuery" type="text" :placeholder="labels.search" class="filter-search-input" />
      </div>

      <div v-if="isLoading" class="page-state" role="status" aria-live="polite">
        <span class="state-spinner" aria-hidden="true"></span>
        <h2>{{ labels.loading }}</h2>
        <p>{{ labels.loadingDesc }}</p>
      </div>

      <div v-else-if="activeError" class="page-state state-error" role="alert">
        <i class="fa-solid fa-triangle-exclamation state-icon" aria-hidden="true"></i>
        <h2>{{ labels.errorTitle }}</h2>
        <p>{{ activeError }}</p>
        <button class="retry-button" type="button" @click="retryActiveTab">{{ labels.retry }}</button>
      </div>

      <div v-else-if="groupedActivities.length > 0" class="activity-list">
        <section v-for="group in groupedActivities" :key="group.label" class="time-group">
          <h2>{{ group.label }}</h2>
          <div class="group-items">
            <button
              v-for="activity in group.items"
              :key="activity.id"
              class="activity-item"
              type="button"
              :disabled="!activity.url"
              @click="goToItem(activity)"
            >
              <span class="item-icon"><i :class="activity.icon" aria-hidden="true"></i></span>
              <span class="item-copy">
                <strong>{{ activity.bold || activity.text || labels.untitled }}</strong>
                <small>{{ activity.text }}</small>
              </span>
              <time class="item-time">{{ formatActivityTime(activity) }}</time>
            </button>
          </div>
        </section>
      </div>

      <div v-else class="page-state">
        <i class="fa-regular fa-clock state-icon" aria-hidden="true"></i>
        <h2>{{ searchQuery ? labels.noMatches : labels.emptyTitle }}</h2>
        <p>{{ searchQuery ? labels.noMatchesDesc : emptyDescription }}</p>
      </div>

      <nav
        v-if="activeTab === 'viewed' && !activeError && starredStore.recentPagination.totalCount > pageSize"
        class="pagination"
        :aria-label="labels.pagination"
      >
        <button type="button" :disabled="currentPage <= 1 || isLoading" :aria-label="labels.previous" @click="loadRecentPage(currentPage - 1)">
          <i class="fa-solid fa-chevron-left" aria-hidden="true"></i>
        </button>
        <span>{{ currentPage }} / {{ totalPages }}</span>
        <button type="button" :disabled="currentPage >= totalPages || isLoading" :aria-label="labels.next" @click="loadRecentPage(currentPage + 1)">
          <i class="fa-solid fa-chevron-right" aria-hidden="true"></i>
        </button>
      </nav>
    </section>
  </main>
</template>

<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { signalRService } from '@/api/signalrService'
import { useRouter } from 'vue-router'
import { useI18nStore } from '@/store/useI18nStore'
import { useActivityStore } from '@/store/useActivityStore'
import { useStarredStore } from '@/store/useStarredStore'

const i18nStore = useI18nStore()
const activityStore = useActivityStore()
const starredStore = useStarredStore()
const router = useRouter()
const activeTab = ref('worked')
const searchQuery = ref('')
const currentPage = ref(1)
const pageSize = 20

const labels = computed(() => i18nStore.locale === 'vi'
  ? {
      eyebrow: 'Lịch sử cá nhân', title: 'Gần đây', description: 'Tiếp tục từ nơi bạn dừng lại hoặc xem lại các thay đổi gần đây.',
      tabs: 'Loại hoạt động', worked: 'Đã làm việc', viewed: 'Đã xem', search: 'Lọc theo tiêu đề',
      loading: 'Đang tải hoạt động', loadingDesc: 'Dòng thời gian sẽ xuất hiện trong giây lát.', retry: 'Thử lại',
      errorTitle: 'Không thể tải hoạt động gần đây', untitled: 'Hoạt động không có tiêu đề',
      emptyTitle: 'Chưa có hoạt động gần đây', workedEmpty: 'Các thay đổi bạn thực hiện sẽ xuất hiện ở đây.',
      viewedEmpty: 'Các dự án và công việc bạn mở sẽ xuất hiện ở đây.', noMatches: 'Không có kết quả phù hợp',
      noMatchesDesc: 'Thử một từ khóa khác.', today: 'Hôm nay', thisWeek: 'Tuần này', older: 'Trước đó',
      pagination: 'Phân trang hoạt động gần đây', previous: 'Trang trước', next: 'Trang sau'
    }
  : {
      eyebrow: 'Personal history', title: 'Recent', description: 'Pick up where you left off or review your latest changes.',
      tabs: 'Activity type', worked: 'Worked on', viewed: 'Viewed', search: 'Filter by title',
      loading: 'Loading recent activity', loadingDesc: 'Your timeline will be ready in a moment.', retry: 'Try again',
      errorTitle: 'Recent activity is unavailable', untitled: 'Untitled activity',
      emptyTitle: 'No recent activity yet', workedEmpty: 'Changes you make will appear here.',
      viewedEmpty: 'Projects and work items you open will appear here.', noMatches: 'No matching activity',
      noMatchesDesc: 'Try a different search term.', today: 'Today', thisWeek: 'This week', older: 'Older',
      pagination: 'Recent activity pagination', previous: 'Previous page', next: 'Next page'
    })

const recentViews = computed(() => starredStore.recentItems.map(item => ({
  id: item.id || `${item.entityType}:${item.entityId}`,
  icon: item.icon || 'fa-regular fa-eye',
  text: item.subtitle || item.entityType || '',
  bold: item.title,
  time: item.viewedAt,
  _ts: Date.parse(item.viewedAt) || Date.now(),
  url: item.url
})))

const isLoading = computed(() => activeTab.value === 'worked' ? activityStore.loading : starredStore.recentLoading)
const activeError = computed(() => activeTab.value === 'viewed' ? starredStore.recentError : null)
const emptyDescription = computed(() => activeTab.value === 'viewed' ? labels.value.viewedEmpty : labels.value.workedEmpty)
const totalPages = computed(() => Math.max(1, Math.ceil(starredStore.recentPagination.totalCount / pageSize)))

const filteredItems = computed(() => {
  const source = activeTab.value === 'worked' ? activityStore.activities : recentViews.value
  const query = searchQuery.value.trim().toLowerCase()
  if (!query) return source
  return source.filter(item => `${item.bold || ''} ${item.text || ''}`.toLowerCase().includes(query))
})

const groupedActivities = computed(() => {
  const groups = { today: [], thisWeek: [], older: [] }
  const now = Date.now()
  filteredItems.value.forEach((activity) => {
    const age = now - (activity._ts || Date.parse(activity.time) || now)
    if (age < 86400000) groups.today.push(activity)
    else if (age < 604800000) groups.thisWeek.push(activity)
    else groups.older.push(activity)
  })
  return [
    { label: labels.value.today, items: groups.today },
    { label: labels.value.thisWeek, items: groups.thisWeek },
    { label: labels.value.older, items: groups.older }
  ].filter(group => group.items.length > 0)
})

const formatActivityTime = (activity) => {
  const date = new Date(activity?._ts || activity?.time)
  if (Number.isNaN(date.getTime())) return activity?.time || ''
  return new Intl.DateTimeFormat(i18nStore.locale === 'vi' ? 'vi-VN' : 'en', {
    day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit'
  }).format(date)
}

const loadRecentPage = async (page) => {
  try {
    await starredStore.fetchRecentItems({ page, pageSize })
    currentPage.value = page
  } catch {
    // Keep the current page and expose the API error state.
  }
}

const selectTab = (tab) => {
  activeTab.value = tab
  searchQuery.value = ''
}
const retryActiveTab = () => activeTab.value === 'viewed'
  ? loadRecentPage(currentPage.value)
  : activityStore.fetchRecentActivities({ limit: 50 })
const goToItem = (item) => {
  if (item.url) router.push(item.url)
}

onMounted(() => {
  activityStore.fetchRecentActivities({ limit: 50 })
  loadRecentPage(1)
})
</script>

<style scoped>
.tool-page { min-height: 100vh; box-sizing: border-box; background: var(--home-bg, #f7f8fa); color: var(--home-text, #172b4d); }
.page-header { padding: 34px 40px 0; border-bottom: 1px solid var(--home-border, #dfe1e6); background: linear-gradient(135deg, color-mix(in srgb, var(--home-panel, #fff) 92%, var(--home-accent, #0c66e4) 8%), var(--home-panel, #fff)); }
.eyebrow { color: var(--home-accent, #0c66e4); font-size: 11px; font-weight: 900; letter-spacing: 0.11em; text-transform: uppercase; }
.page-header h1 { margin: 6px 0 7px; font-size: clamp(26px, 4vw, 34px); font-weight: 850; letter-spacing: -0.025em; }
.page-header > p { max-width: 620px; margin: 0; color: var(--home-muted, #5e6c84); font-size: 14px; line-height: 1.55; }
.tabs { margin-top: 24px; display: flex; gap: 4px; }
.tab-button,
.activity-item,
.retry-button,
.pagination button { appearance: none; -webkit-appearance: none; border: 0; background: transparent; color: inherit; font: inherit; padding: 0; cursor: pointer; touch-action: manipulation; }
.tab-button { min-height: 42px; padding: 0 14px; border-bottom: 3px solid transparent; color: var(--home-muted, #5e6c84); font-size: 13px; font-weight: 800; }
.tab-button:hover { color: var(--home-text, #172b4d); }
.tab-button.active { border-bottom-color: var(--home-accent, #0c66e4); color: var(--home-accent, #0c66e4); }

.page-content { max-width: 1040px; padding: 24px 40px 44px; }
.filter-search-field {
  position: relative;
  display: flex;
  align-items: center;
  width: min(100%, 360px);
  height: 40px;
  box-sizing: border-box;
  border: 1px solid var(--color-border, #dfe1e6);
  border-radius: 6px;
  background-color: var(--color-surface, #fff);
  margin-bottom: 20px;
  transition: all 0.2s ease;
}

.filter-search-icon {
  position: absolute;
  left: 12px;
  color: var(--color-text-muted, #6b778c);
  font-size: 14px;
  pointer-events: none;
}

.filter-search-input {
  width: 100% !important;
  height: 100% !important;
  box-sizing: border-box !important;
  min-width: 0 !important;
  border: 0 !important;
  border-radius: 0 !important;
  background: transparent !important;
  padding: 0 12px 0 32px !important;
  font-size: 14px !important;
  color: var(--color-text-primary, #172b4d) !important;
  outline: none !important;
  box-shadow: none !important;
  line-height: normal !important;
}

.filter-search-input::placeholder {
  color: var(--color-text-muted, #6b778c);
}

.filter-search-field:focus-within {
  border-color: var(--color-primary, #0c66e4);
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-primary, #0c66e4) 20%, transparent);
}

.activity-list { width: min(100%, 960px); display: flex; flex-direction: column; gap: 25px; }
.time-group h2 { margin: 0 0 9px; color: var(--home-muted, #5e6c84); font-size: 11px; font-weight: 900; letter-spacing: 0.08em; text-transform: uppercase; }
.group-items { padding-left: 15px; border-left: 2px solid var(--home-border, #dfe1e6); display: flex; flex-direction: column; gap: 9px; }
.activity-item { width: 100%; min-width: 0; min-height: 66px; padding: 10px 12px; display: flex; align-items: center; gap: 12px; box-sizing: border-box; border: 1px solid var(--home-border, #dfe1e6); border-radius: 12px; background: var(--home-panel, #fff); text-align: left; transition: border-color 0.16s ease, background 0.16s ease, transform 0.16s ease; }
.activity-item:hover:not(:disabled) { transform: translateX(2px); border-color: color-mix(in srgb, var(--home-accent, #0c66e4) 42%, var(--home-border, #dfe1e6)); background: var(--home-panel-strong, #fff); }
.activity-item:disabled { cursor: default; }
.item-icon { width: 34px; height: 34px; flex: 0 0 auto; display: grid; place-items: center; border-radius: 10px; background: color-mix(in srgb, var(--home-accent, #0c66e4) 11%, transparent); color: var(--home-accent, #0c66e4); font-size: 13px; }
.item-copy { min-width: 0; flex: 1; display: flex; flex-direction: column; }
.item-copy strong,
.item-copy small { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.item-copy strong { font-size: 14px; font-weight: 850; }
.item-copy small { margin-top: 3px; color: var(--home-muted, #5e6c84); font-size: 12px; }
.item-time { flex: 0 0 auto; color: var(--home-muted, #5e6c84); font-size: 11px; white-space: nowrap; }

.page-state { width: min(100%, 960px); min-height: 300px; padding: 42px 22px; display: flex; flex-direction: column; align-items: center; justify-content: center; box-sizing: border-box; border: 1px dashed var(--home-border, #dfe1e6); border-radius: 16px; background: var(--home-panel, #fff); text-align: center; }
.page-state h2 { margin: 0 0 7px; font-size: 17px; font-weight: 850; }
.page-state p { max-width: 460px; margin: 0; color: var(--home-muted, #5e6c84); font-size: 13px; line-height: 1.55; overflow-wrap: anywhere; }
.state-icon { width: 54px; height: 54px; margin-bottom: 17px; display: grid; place-items: center; border-radius: 15px; background: color-mix(in srgb, var(--home-accent, #0c66e4) 10%, transparent); color: var(--home-accent, #0c66e4); font-size: 26px; }
.state-error .state-icon { background: color-mix(in srgb, #e34935 10%, transparent); color: #e34935; }
.state-spinner { width: 30px; height: 30px; margin-bottom: 20px; border: 3px solid color-mix(in srgb, var(--home-accent, #0c66e4) 18%, transparent); border-top-color: var(--home-accent, #0c66e4); border-radius: 50%; animation: page-spin 0.8s linear infinite; }
@keyframes page-spin { to { transform: rotate(360deg); } }
.retry-button { margin-top: 18px; padding: 9px 14px; border: 1px solid var(--home-border, #dfe1e6); border-radius: 9px; background: var(--home-panel-strong, #fff); font-size: 13px; font-weight: 800; }
.retry-button:hover { background: var(--home-panel-hover, #f4f5f7); }

.pagination { width: min(100%, 960px); margin-top: 20px; display: flex; align-items: center; justify-content: center; gap: 12px; color: var(--home-muted, #5e6c84); font-size: 13px; font-weight: 800; }
.pagination button { width: 40px; height: 40px; display: grid; place-items: center; border: 1px solid var(--home-border, #dfe1e6); border-radius: 10px; background: var(--home-panel, #fff); }
.pagination button:hover:not(:disabled) { background: var(--home-panel-hover, #f4f5f7); color: var(--home-accent, #0c66e4); }
.pagination button:disabled { cursor: not-allowed; opacity: 0.48; }
.tab-button:focus-visible,
.activity-item:focus-visible,
.retry-button:focus-visible,
.pagination button:focus-visible { outline: 3px solid color-mix(in srgb, var(--home-accent, #0c66e4) 42%, transparent); outline-offset: 2px; }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }

@media (max-width: 640px) {
  .page-header { padding: 26px 18px 0; }
  .page-content { padding: 18px 14px 34px; }
  .group-items { padding-left: 9px; }
  .activity-item { align-items: flex-start; }
  .item-time { max-width: 84px; overflow: hidden; text-overflow: ellipsis; }
}
@media (hover: none) { .activity-item:hover:not(:disabled) { transform: none; } }
@media (prefers-reduced-motion: reduce) { .activity-item { transition: none; } }
</style>
