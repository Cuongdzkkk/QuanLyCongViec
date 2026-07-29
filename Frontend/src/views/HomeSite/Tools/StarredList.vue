<template>
  <main class="tool-page">
    <header class="page-header">
      <span class="eyebrow">{{ labels.eyebrow }}</span>
      <h1>{{ labels.title }}</h1>
      <p>{{ labels.description }}</p>
    </header>

    <section class="page-content" aria-labelledby="starred-results-title">
      <h2 id="starred-results-title" class="sr-only">{{ labels.results }}</h2>
      <div class="filter-controls">
        <label class="search-field">
          <span class="sr-only">{{ labels.search }}</span>
          <i class="fa-solid fa-magnifying-glass" aria-hidden="true"></i>
          <input v-model="searchQuery" type="search" :placeholder="labels.search" />
        </label>
        <label class="select-field">
          <span class="sr-only">{{ labels.allTypes }}</span>
          <select v-model="typeFilter">
            <option value="">{{ labels.allTypes }}</option>
            <option value="project">{{ labels.project }}</option>
            <option value="worktask">{{ labels.task }}</option>
            <option value="goal">{{ labels.goal }}</option>
            <option value="team">{{ labels.team }}</option>
            <option value="user">{{ labels.user }}</option>
          </select>
          <i class="fa-solid fa-chevron-down" aria-hidden="true"></i>
        </label>
      </div>

      <div v-if="starredStore.loading" class="page-state" role="status" aria-live="polite">
        <span class="state-spinner" aria-hidden="true"></span>
        <h2>{{ labels.loading }}</h2>
        <p>{{ labels.loadingDesc }}</p>
      </div>

      <div v-else-if="starredStore.error" class="page-state state-error" role="alert">
        <i class="fa-solid fa-triangle-exclamation state-icon" aria-hidden="true"></i>
        <h2>{{ labels.errorTitle }}</h2>
        <p>{{ starredStore.error }}</p>
        <button class="retry-button" type="button" @click="loadPage(currentPage)">{{ labels.retry }}</button>
      </div>

      <div v-else-if="filteredStarredItems.length > 0" class="starred-grid">
        <article
          v-for="item in filteredStarredItems"
          :key="`${item.itemType}:${item.itemId}`"
          class="starred-card"
          :class="{ clickable: canOpen(item) }"
        >
          <button
            class="card-main"
            type="button"
            :disabled="!canOpen(item)"
            @click="openItem(item)"
          >
            <span class="card-icon" :class="normalizeType(item)">
              <i class="fa-solid" :class="getIcon(item)" aria-hidden="true"></i>
            </span>
            <span class="card-copy">
              <strong>{{ item.itemName || item.name || item.title || labels.untitled }}</strong>
              <small>{{ typeLabel(item) }}</small>
            </span>
          </button>
          <button
            class="unstar-button"
            type="button"
            :disabled="starredStore.isPending(item.itemType, item.itemId)"
            :aria-busy="starredStore.isPending(item.itemType, item.itemId)"
            :aria-label="labels.unstar"
            :title="labels.unstar"
            @click="unstar(item)"
          >
            <i :class="starredStore.isPending(item.itemType, item.itemId) ? 'fa-solid fa-spinner fa-spin' : 'fa-solid fa-star'" aria-hidden="true"></i>
          </button>
        </article>
      </div>

      <div v-else class="page-state">
        <i class="fa-regular fa-star state-icon" aria-hidden="true"></i>
        <h2>{{ hasFilters ? labels.noMatches : labels.emptyTitle }}</h2>
        <p>{{ hasFilters ? labels.noMatchesDesc : labels.emptyDesc }}</p>
      </div>

      <nav v-if="!starredStore.error && starredStore.starredPagination.totalCount > pageSize" class="pagination" :aria-label="labels.pagination">
        <button type="button" :disabled="currentPage <= 1 || starredStore.loading" :aria-label="labels.previous" @click="loadPage(currentPage - 1)">
          <i class="fa-solid fa-chevron-left" aria-hidden="true"></i>
        </button>
        <span>{{ currentPage }} / {{ totalPages }}</span>
        <button type="button" :disabled="currentPage >= totalPages || starredStore.loading" :aria-label="labels.next" @click="loadPage(currentPage + 1)">
          <i class="fa-solid fa-chevron-right" aria-hidden="true"></i>
        </button>
      </nav>
    </section>
  </main>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useStarredStore } from '@/store/useStarredStore'
import { useI18nStore } from '@/store/useI18nStore'

const router = useRouter()
const starredStore = useStarredStore()
const i18nStore = useI18nStore()
const searchQuery = ref('')
const typeFilter = ref('')
const currentPage = ref(1)
const pageSize = 24

const labels = computed(() => i18nStore.locale === 'vi'
  ? {
      eyebrow: 'Lối tắt cá nhân', title: 'Đã gắn sao', description: 'Tập hợp dự án, mục tiêu và công việc quan trọng để quay lại nhanh.',
      results: 'Danh sách đã gắn sao', search: 'Tìm theo tiêu đề', allTypes: 'Tất cả loại', loading: 'Đang tải mục đã gắn sao',
      loadingDesc: 'Danh sách của bạn sẽ xuất hiện trong giây lát.', retry: 'Thử lại', errorTitle: 'Không thể tải mục đã gắn sao',
      project: 'Dự án', task: 'Công việc', goal: 'Mục tiêu', team: 'Nhóm', user: 'Người dùng', item: 'Mục',
      untitled: 'Chưa có tiêu đề', unstar: 'Bỏ gắn sao', emptyTitle: 'Chưa có mục nào được gắn sao',
      emptyDesc: 'Các mục bạn gắn sao sẽ xuất hiện ở đây để truy cập nhanh.', noMatches: 'Không có kết quả phù hợp',
      noMatchesDesc: 'Thử thay đổi từ khóa hoặc loại mục.', pagination: 'Phân trang mục đã gắn sao', previous: 'Trang trước', next: 'Trang sau'
    }
  : {
      eyebrow: 'Personal shortcuts', title: 'Starred', description: 'Keep important projects, goals, and work items close at hand.',
      results: 'Starred items', search: 'Search by title', allTypes: 'All types', loading: 'Loading starred items',
      loadingDesc: 'Your shortcuts will be ready in a moment.', retry: 'Try again', errorTitle: 'Starred items are unavailable',
      project: 'Project', task: 'Work item', goal: 'Goal', team: 'Team', user: 'User', item: 'Item',
      untitled: 'Untitled', unstar: 'Remove from starred', emptyTitle: 'No starred items yet',
      emptyDesc: 'Items you star will appear here for quick access.', noMatches: 'No matching items',
      noMatchesDesc: 'Try changing the search or item type.', pagination: 'Starred pagination', previous: 'Previous page', next: 'Next page'
    })

const normalizeType = (item) => String(item?.itemType || item?.type || item?.entityType || 'item').toLowerCase()
const getTargetId = (item) => item?.itemId || item?.entityId || item?.targetId || item?.projectId || item?.goalId || item?.teamId || item?.userId
const hasFilters = computed(() => Boolean(searchQuery.value.trim() || typeFilter.value))

const filteredStarredItems = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()
  return starredStore.starredItems.filter((item) => {
    const type = normalizeType(item)
    return (!typeFilter.value || type === typeFilter.value) &&
      (!query || `${item.itemName || ''} ${item.name || ''} ${item.title || ''} ${type}`.toLowerCase().includes(query))
  })
})

const totalPages = computed(() => Math.max(1, Math.ceil(starredStore.starredPagination.totalCount / pageSize)))
const loadPage = async (page) => {
  try {
    await starredStore.fetchStarredItems({ page, pageSize })
    currentPage.value = page
  } catch {
    // Keep the current page number and expose the API error state.
  }
}
onMounted(() => loadPage(1))

const getIcon = (item) => {
  const type = normalizeType(item)
  if (type === 'project') return 'fa-rocket'
  if (type === 'worktask') return 'fa-square-check'
  if (type === 'goal') return 'fa-bullseye'
  if (type === 'team') return 'fa-users'
  if (type === 'user') return 'fa-user'
  return 'fa-file-lines'
}

const typeLabel = (item) => {
  const type = normalizeType(item)
  if (type === 'project') return labels.value.project
  if (type === 'worktask') return labels.value.task
  if (type === 'goal') return labels.value.goal
  if (type === 'team') return labels.value.team
  if (type === 'user') return labels.value.user
  return labels.value.item
}

const canOpen = (item) => Boolean(item.url || getTargetId(item))
const openItem = (item) => {
  if (item.url) return router.push(item.url)
  const id = getTargetId(item)
  const type = normalizeType(item)
  if (type === 'project') return router.push(`/home/projects/${id}`)
  if (type === 'goal') return router.push(`/home/goals/${id}`)
  if (type === 'team') return router.push(`/home/teams/${id}`)
  if (type === 'user') return router.push(`/home/people/${id}`)
}

const unstar = async (item) => {
  const id = getTargetId(item)
  if (!id) return
  try {
    await starredStore.setStarred(item.itemType || item.type || item.entityType, id, false)
    if (starredStore.starredItems.length === 0 && currentPage.value > 1) await loadPage(currentPage.value - 1)
  } catch {
    // The failed item stays visible; the page renders the API error with retry.
  }
}
</script>

<style scoped>
.tool-page { min-height: 100vh; box-sizing: border-box; background: var(--home-bg, #f7f8fa); color: var(--home-text, #172b4d); }
.page-header { padding: 34px 40px 25px; border-bottom: 1px solid var(--home-border, #dfe1e6); background: linear-gradient(135deg, color-mix(in srgb, var(--home-panel, #fff) 92%, var(--home-accent, #0c66e4) 8%), var(--home-panel, #fff)); }
.eyebrow { color: var(--home-accent, #0c66e4); font-size: 11px; font-weight: 900; letter-spacing: 0.11em; text-transform: uppercase; }
.page-header h1 { margin: 6px 0 7px; font-size: clamp(26px, 4vw, 34px); font-weight: 850; letter-spacing: -0.025em; }
.page-header p { max-width: 620px; margin: 0; color: var(--home-muted, #5e6c84); font-size: 14px; line-height: 1.55; }
.page-content { max-width: 1120px; padding: 24px 40px 44px; }

.filter-controls { width: min(100%, 1040px); margin-bottom: 20px; padding: 10px; display: flex; gap: 10px; box-sizing: border-box; border: 1px solid var(--home-border, #dfe1e6); border-radius: 14px; background: var(--home-panel, #fff); box-shadow: 0 6px 20px rgba(15, 23, 42, 0.04); }
.search-field { position: relative; min-width: 0; flex: 1; }
.search-field i { position: absolute; left: 13px; top: 50%; transform: translateY(-50%); color: var(--home-muted, #5e6c84); font-size: 13px; pointer-events: none; }
.search-field input,
.select-field select { appearance: none; -webkit-appearance: none; width: 100%; height: 40px; box-sizing: border-box; border: 1px solid var(--home-border, #dfe1e6); border-radius: 9px; outline: 0; background: var(--home-panel-strong, #fff); color: var(--home-text, #172b4d); font: inherit; font-size: 14px; }
.search-field input { padding: 9px 12px 9px 38px; }
.select-field { position: relative; width: 180px; flex: 0 0 auto; }
.select-field select { padding: 0 36px 0 12px; cursor: pointer; }
.select-field i { position: absolute; right: 13px; top: 50%; transform: translateY(-50%); color: var(--home-muted, #5e6c84); font-size: 11px; pointer-events: none; }
.search-field input:focus-visible,
.select-field select:focus-visible { border-color: var(--home-accent, #0c66e4); box-shadow: 0 0 0 3px color-mix(in srgb, var(--home-accent, #0c66e4) 18%, transparent); }

.starred-grid { width: min(100%, 1040px); display: grid; grid-template-columns: repeat(auto-fill, minmax(min(100%, 280px), 1fr)); gap: 14px; }
.starred-card { min-width: 0; min-height: 76px; padding: 7px; display: flex; align-items: center; box-sizing: border-box; border: 1px solid var(--home-border, #dfe1e6); border-radius: 14px; background: var(--home-panel, #fff); transition: border-color 0.18s ease, transform 0.18s ease, box-shadow 0.18s ease; }
.starred-card:hover { transform: translateY(-1px); border-color: color-mix(in srgb, var(--home-accent, #0c66e4) 45%, var(--home-border, #dfe1e6)); box-shadow: 0 14px 30px rgba(15, 23, 42, 0.09); }

.card-main,
.unstar-button,
.retry-button,
.pagination button { appearance: none; -webkit-appearance: none; border: 0; background: transparent; color: inherit; font: inherit; padding: 0; cursor: pointer; touch-action: manipulation; }
.card-main { min-width: 0; min-height: 58px; flex: 1; display: flex; align-items: center; gap: 12px; border-radius: 10px; text-align: left; }
.card-main:disabled { cursor: default; }
.card-icon { width: 42px; height: 42px; margin-left: 4px; flex: 0 0 auto; display: grid; place-items: center; border-radius: 11px; color: #fff; }
.card-icon.project { background: linear-gradient(135deg, #0ea5e9, #2563eb); }
.card-icon.worktask { background: linear-gradient(135deg, #2563eb, #4f46e5); }
.card-icon.goal { background: linear-gradient(135deg, #10b981, #0f766e); }
.card-icon.team { background: linear-gradient(135deg, #8b5cf6, #4f46e5); }
.card-icon.user { background: linear-gradient(135deg, #06b6d4, #0284c7); }
.card-icon.item { background: linear-gradient(135deg, #f59e0b, #e34935); }
.card-copy { min-width: 0; flex: 1; display: flex; flex-direction: column; }
.card-copy strong,
.card-copy small { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.card-copy strong { font-size: 14px; font-weight: 850; }
.card-copy small { margin-top: 3px; color: var(--home-muted, #5e6c84); font-size: 12px; }
.unstar-button { width: 42px; height: 42px; flex: 0 0 auto; display: grid; place-items: center; border-radius: 10px; color: #e3a008; font-size: 15px; }
.unstar-button:hover { background: color-mix(in srgb, #e3a008 14%, transparent); }
.unstar-button:disabled { cursor: wait; }
.unstar-button i { width: 1em; line-height: 1; text-align: center; }

.page-state { width: min(100%, 1040px); min-height: 300px; padding: 42px 22px; display: flex; flex-direction: column; align-items: center; justify-content: center; box-sizing: border-box; border: 1px dashed var(--home-border, #dfe1e6); border-radius: 16px; background: var(--home-panel, #fff); text-align: center; }
.page-state h2 { margin: 0 0 7px; font-size: 17px; font-weight: 850; }
.page-state p { max-width: 460px; margin: 0; color: var(--home-muted, #5e6c84); font-size: 13px; line-height: 1.55; overflow-wrap: anywhere; }
.state-icon { width: 54px; height: 54px; margin-bottom: 17px; display: grid; place-items: center; border-radius: 15px; background: color-mix(in srgb, var(--home-accent, #0c66e4) 10%, transparent); color: var(--home-accent, #0c66e4); font-size: 26px; }
.state-error .state-icon { background: color-mix(in srgb, #e34935 10%, transparent); color: #e34935; }
.state-spinner { width: 30px; height: 30px; margin-bottom: 20px; border: 3px solid color-mix(in srgb, var(--home-accent, #0c66e4) 18%, transparent); border-top-color: var(--home-accent, #0c66e4); border-radius: 50%; animation: page-spin 0.8s linear infinite; }
@keyframes page-spin { to { transform: rotate(360deg); } }
.retry-button { margin-top: 18px; padding: 9px 14px; border: 1px solid var(--home-border, #dfe1e6); border-radius: 9px; background: var(--home-panel-strong, #fff); font-size: 13px; font-weight: 800; }
.retry-button:hover { background: var(--home-panel-hover, #f4f5f7); }

.pagination { width: min(100%, 1040px); margin-top: 20px; display: flex; align-items: center; justify-content: center; gap: 12px; color: var(--home-muted, #5e6c84); font-size: 13px; font-weight: 800; }
.pagination button { width: 40px; height: 40px; display: grid; place-items: center; border: 1px solid var(--home-border, #dfe1e6); border-radius: 10px; background: var(--home-panel, #fff); }
.pagination button:hover:not(:disabled) { background: var(--home-panel-hover, #f4f5f7); color: var(--home-accent, #0c66e4); }
.pagination button:disabled { cursor: not-allowed; opacity: 0.48; }

.card-main:focus-visible,
.unstar-button:focus-visible,
.retry-button:focus-visible,
.pagination button:focus-visible { outline: 3px solid color-mix(in srgb, var(--home-accent, #0c66e4) 42%, transparent); outline-offset: 2px; }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }

@media (max-width: 640px) {
  .page-header { padding: 26px 18px 21px; }
  .page-content { padding: 18px 14px 34px; }
  .filter-controls { flex-direction: column; }
  .select-field { width: 100%; }
  .starred-grid { grid-template-columns: minmax(0, 1fr); }
}
@media (hover: none) { .starred-card:hover { transform: none; } }
@media (prefers-reduced-motion: reduce) { .starred-card { transition: none; } }
</style>
