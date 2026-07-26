<template>
  <div class="starred-page">
    <header class="page-header">
      <h1>{{ labels.title }}</h1>
    </header>

    <div class="page-content">
      <div class="filter-controls">
        <div class="search-input-wrapper">
          <i class="fa-solid fa-magnifying-glass search-icon"></i>
          <input v-model="searchQuery" type="text" :placeholder="labels.search" class="search-input" />
        </div>
        <select v-model="typeFilter" class="filter-select">
          <option value="">{{ labels.allTypes }}</option>
          <option value="project">{{ labels.project }}</option>
          <option value="goal">{{ labels.goal }}</option>
          <option value="team">{{ labels.team }}</option>
          <option value="user">{{ labels.user }}</option>
        </select>
      </div>

      <div v-if="starredStore.loading" class="empty-state">{{ labels.loading }}</div>
      <div v-else-if="starredStore.error" class="empty-state">
        <p>{{ starredStore.error }}</p>
        <button class="retry-btn" type="button" @click="loadPage(currentPage)">{{ labels.retry }}</button>
      </div>

      <div class="starred-grid" v-else-if="filteredStarredItems.length > 0">
        <div class="starred-card" v-for="item in filteredStarredItems" :key="`${item.itemType}:${item.itemId}`" @click="openItem(item)">
          <div class="card-icon" :class="normalizeType(item)">
            <i class="fa-solid" :class="getIcon(item)"></i>
          </div>
          <div class="card-content">
            <h3>{{ item.itemName || item.name || item.title || labels.untitled }}</h3>
            <p>{{ typeLabel(item) }}</p>
          </div>
          <button
            class="unstar-btn"
            type="button"
            :disabled="starredStore.isPending(item.itemType, item.itemId)"
            :aria-label="labels.unstar"
            @click.stop="unstar(item)"
            :title="labels.unstar"
          >
            <i class="fa-solid fa-star"></i>
          </button>
        </div>
      </div>

      <div class="empty-state hero-empty" v-else>
        <div class="empty-illustration"><i class="fa-regular fa-star"></i></div>
        <h2>{{ labels.emptyTitle }}</h2>
        <p>{{ labels.emptyDesc }}</p>
      </div>
      <div v-if="starredStore.starredPagination.totalCount > pageSize" class="pagination">
        <button type="button" :disabled="currentPage <= 1 || starredStore.loading" @click="loadPage(currentPage - 1)">‹</button>
        <span>{{ currentPage }} / {{ totalPages }}</span>
        <button type="button" :disabled="currentPage >= totalPages || starredStore.loading" @click="loadPage(currentPage + 1)">›</button>
      </div>
    </div>
  </div>
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
      title: 'Gắn sao',
      search: 'Tìm kiếm theo tiêu đề',
      allTypes: 'Tất cả loại',
      loading: 'Đang tải dữ liệu...',
      retry: 'Thử lại',
      project: 'Dự án',
      goal: 'Mục tiêu',
      team: 'Nhóm',
      user: 'Người dùng',
      item: 'Mục',
      untitled: 'Chưa có tiêu đề',
      unstar: 'Bỏ gắn sao',
      emptyTitle: 'Bạn chưa đánh dấu sao mục nào.',
      emptyDesc: 'Những mục bạn gắn sao sẽ xuất hiện ở đây để truy cập nhanh.'
    }
  : {
      title: 'Starred',
      search: 'Search by title',
      allTypes: 'All types',
      loading: 'Loading data...',
      retry: 'Retry',
      project: 'Project',
      goal: 'Goal',
      team: 'Team',
      user: 'User',
      item: 'Item',
      untitled: 'Untitled',
      unstar: 'Unstar',
      emptyTitle: 'No starred items yet',
      emptyDesc: 'Items you star will appear here for quick access.'
    })

const loadPage = async (page) => {
  currentPage.value = page
  await starredStore.fetchStarredItems({ page, pageSize }).catch(() => {})
}

onMounted(() => loadPage(1))

const normalizeType = (item) => String(item?.itemType || item?.type || item?.entityType || 'item').toLowerCase()

const getTargetId = (item) => item?.itemId || item?.entityId || item?.targetId || item?.projectId || item?.goalId || item?.teamId || item?.userId

const filteredStarredItems = computed(() => {
  const q = searchQuery.value.trim().toLowerCase()
  return (starredStore.starredItems || []).filter((item) => {
    const type = normalizeType(item)
    const matchesType = !typeFilter.value || type === typeFilter.value
    const matchesSearch = !q || `${item.itemName || ''} ${item.name || ''} ${item.title || ''} ${type}`.toLowerCase().includes(q)
    return matchesType && matchesSearch
  })
})
const totalPages = computed(() => Math.max(1, Math.ceil(starredStore.starredPagination.totalCount / pageSize)))

const getIcon = (item) => {
  const type = normalizeType(item)
  if (type === 'project') return 'fa-rocket'
  if (type === 'goal') return 'fa-bullseye'
  if (type === 'team') return 'fa-users'
  if (type === 'user') return 'fa-user'
  return 'fa-file-lines'
}

const typeLabel = (item) => {
  const type = normalizeType(item)
  if (type === 'project') return labels.value.project
  if (type === 'goal') return labels.value.goal
  if (type === 'team') return labels.value.team
  if (type === 'user') return labels.value.user
  return labels.value.item
}

const openItem = (item) => {
  if (item.url) return router.push(item.url)
  const id = getTargetId(item)
  const type = normalizeType(item)
  if (type === 'project') return router.push(id ? `/home/projects/${id}` : '/home/projects')
  if (type === 'goal') return router.push(id ? `/home/goals/${id}` : '/home/goals')
  if (type === 'team') return router.push(id ? `/home/teams/${id}` : '/home/teams')
  if (type === 'user') return router.push(id ? `/home/people/${id}` : '/home/teams')
}

const unstar = async (item) => {
  const id = getTargetId(item)
  if (!id) return
  await starredStore.setStarred(item.itemType || item.type || item.entityType || 'Project', id, false)
  if (filteredStarredItems.value.length === 0 && currentPage.value > 1) {
    await loadPage(currentPage.value - 1)
  }
}
</script>

<style scoped>
.starred-page {
  min-height: 100vh;
  background: var(--home-bg, #ffffff);
  color: var(--home-text, #172b4d);
}

.page-header {
  padding: 32px 40px 0;
}

.page-header h1 {
  margin: 0 0 24px;
  color: var(--home-text, #172b4d);
  font-size: 28px;
  font-weight: 800;
}

.page-content {
  padding: 20px 40px 40px;
  border-top: 1px solid var(--home-border, #dfe1e6);
}

.filter-controls {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  width: min(100%, 1040px);
  margin-bottom: 18px;
  padding: 12px;
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 12px;
  background: var(--home-panel, #ffffff);
}

.search-input-wrapper {
  position: relative;
  width: min(100%, 300px);
}

.search-icon {
  position: absolute;
  left: 12px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--home-muted, #5e6c84);
  font-size: 14px;
}

.search-input,
.filter-select {
  height: 38px;
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 8px;
  background: var(--home-panel-strong, #ffffff);
  color: var(--home-text, #091e42);
  font-size: 14px;
  outline: none;
}

.search-input {
  width: 100%;
  box-sizing: border-box;
  padding: 8px 12px 8px 38px;
}

.filter-select {
  padding: 0 12px;
}

.search-input:focus,
.filter-select:focus {
  border-color: var(--home-accent, #4c9aff);
  box-shadow: 0 0 0 3px rgba(56, 189, 248, 0.14);
}

.starred-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
  width: min(100%, 1040px);
}

.starred-card {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 14px;
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 12px;
  background: var(--home-panel, #ffffff);
  cursor: pointer;
  transition: border-color 0.2s, transform 0.2s, box-shadow 0.2s;
}

.starred-card:hover {
  transform: translateY(-1px);
  border-color: rgba(56, 189, 248, 0.55);
  box-shadow: 0 16px 38px rgba(2, 6, 23, 0.12);
}

.card-icon {
  width: 42px;
  height: 42px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  flex: 0 0 auto;
}

.card-icon.project { background: linear-gradient(135deg, #0ea5e9, #2563eb); }
.card-icon.goal { background: linear-gradient(135deg, #10b981, #0f766e); }
.card-icon.team { background: linear-gradient(135deg, #8b5cf6, #4f46e5); }
.card-icon.user { background: linear-gradient(135deg, #06b6d4, #0284c7); }
.card-icon.item { background: linear-gradient(135deg, #f59e0b, #ef4444); }

.card-content {
  flex: 1;
  min-width: 0;
}

.card-content h3 {
  margin: 0 0 4px;
  overflow: hidden;
  color: var(--home-text, #172b4d);
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 14px;
  font-weight: 800;
}

.card-content p {
  margin: 0;
  color: var(--home-muted, #5e6c84);
  font-size: 12px;
}

.unstar-btn {
  border: 0;
  border-radius: 9px;
  background: rgba(245, 158, 11, 0.14);
  color: #fbbf24;
  cursor: pointer;
  padding: 9px;
  font-size: 16px;
}

.unstar-btn:disabled {
  cursor: wait;
  opacity: 0.55;
}

.retry-btn,
.pagination button {
  border: 1px solid var(--home-border, #dfe1e6);
  background: var(--home-panel, #ffffff);
  color: var(--home-text, #172b4d);
  cursor: pointer;
  padding: 8px 12px;
}

.pagination {
  display: flex;
  align-items: center;
  gap: 12px;
  width: min(100%, 1040px);
  margin-top: 20px;
}

.pagination button:disabled {
  cursor: not-allowed;
  opacity: 0.5;
}

.empty-state {
  width: min(100%, 1040px);
  padding: 48px 20px;
  border: 1px dashed var(--home-border, #dfe1e6);
  border-radius: 16px;
  color: var(--home-muted, #5e6c84);
  text-align: center;
}

.hero-empty {
  margin-top: 8px;
}

.empty-illustration {
  margin-bottom: 16px;
  color: #fbbf24;
  font-size: 52px;
}

.empty-state h2 {
  margin: 0 0 8px;
  color: var(--home-text, #172b4d);
  font-size: 20px;
}

.empty-state p {
  margin: 0;
}
</style>
