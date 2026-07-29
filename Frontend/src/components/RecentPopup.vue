<template>
  <transition name="slide-right">
    <div v-if="isVisible" class="recent-popup-overlay" @click.self="closePopup">
      <aside class="recent-sheet" aria-labelledby="recent-sheet-title">
        <header class="sheet-header">
          <h3 id="recent-sheet-title"><i class="fa-solid fa-clock-rotate-left" aria-hidden="true"></i> Recent activity</h3>
          <button class="icon-button" type="button" aria-label="Close recent activity" @click="closePopup">
            <i class="fa-solid fa-xmark" aria-hidden="true"></i>
          </button>
        </header>

        <label class="sheet-search">
          <span class="sr-only">Search recent work items</span>
          <i class="fa-solid fa-magnifying-glass search-icon" aria-hidden="true"></i>
          <input v-model="searchQuery" type="search" placeholder="Search recent work items" />
        </label>

        <div class="sheet-body">
          <div v-if="starredStore.recentLoading" class="sheet-state" role="status" aria-live="polite">
            <span class="state-spinner" aria-hidden="true"></span>
            <h4>Loading recent activity</h4>
            <p>Finding the items you viewed most recently.</p>
          </div>

          <div v-else-if="starredStore.recentError" class="sheet-state state-error" role="alert">
            <i class="fa-solid fa-triangle-exclamation state-icon" aria-hidden="true"></i>
            <h4>Recent activity is unavailable</h4>
            <p>{{ starredStore.recentError }}</p>
            <button class="retry-button" type="button" @click="loadRecentTasks">Try again</button>
          </div>

          <div v-else-if="filteredRecentTasks.length === 0" class="sheet-state">
            <i class="fa-regular fa-clock state-icon" aria-hidden="true"></i>
            <h4>{{ searchQuery ? 'No matching items' : 'No recent activity yet' }}</h4>
            <p>{{ searchQuery ? 'Try a different title, key, or project.' : 'View a work item to see it here.' }}</p>
          </div>

          <ul v-else class="recent-list">
            <li v-for="task in filteredRecentTasks" :key="`${task.entityType}:${task.id}`">
              <button
                class="recent-item"
                type="button"
                :disabled="!task.url"
                @click="goToTask(task)"
              >
                <span class="status-box"><i :class="getStatusIcon(task.statusName)" aria-hidden="true"></i></span>
                <span class="item-center">
                  <strong>{{ task.title || 'Untitled work item' }}</strong>
                  <span class="item-meta">
                    <span class="item-key">{{ task.sequenceId || shortId(task.id) }}</span>
                    <span class="item-project"><i class="fa-solid fa-briefcase" aria-hidden="true"></i> {{ task.projectName || 'Project' }}</span>
                  </span>
                </span>
                <span class="time-ago">{{ timeAgo(task.updatedAt) }}</span>
              </button>
            </li>
          </ul>
        </div>

        <footer class="sheet-footer">
          <router-link to="/home/recent" class="view-all-link" @click="closePopup">
            View all recent activity <i class="fa-solid fa-arrow-right" aria-hidden="true"></i>
          </router-link>
        </footer>
      </aside>
    </div>
  </transition>
</template>

<script setup>
import { computed, onUnmounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useStarredStore } from '@/store/useStarredStore'

const props = defineProps({
  isVisible: { type: Boolean, default: false }
})
const emit = defineEmits(['close'])
const router = useRouter()
const starredStore = useStarredStore()
const searchQuery = ref('')

const recentTasks = computed(() => starredStore.recentItems.map(item => ({
  ...item,
  id: item.entityId,
  projectName: item.subtitle,
  updatedAt: item.viewedAt
})))

const loadRecentTasks = () => starredStore.fetchRecentItems({ page: 1, pageSize: 20 }).catch(() => null)

watch(() => props.isVisible, (visible) => {
  if (visible) {
    searchQuery.value = ''
    loadRecentTasks()
    document.body.style.overflow = 'hidden'
  } else {
    document.body.style.overflow = ''
  }
})
onUnmounted(() => { document.body.style.overflow = '' })

const filteredRecentTasks = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()
  if (!query) return recentTasks.value
  return recentTasks.value.filter(task =>
    `${task.title || ''} ${task.sequenceId || ''} ${task.projectName || ''}`.toLowerCase().includes(query)
  )
})

const closePopup = () => emit('close')
const goToTask = (task) => {
  if (!task.url) return
  closePopup()
  router.push(task.url)
}
const shortId = (id) => id ? String(id).substring(0, 8).toUpperCase() : '—'

const getStatusIcon = (statusName) => {
  const status = `${statusName || 'BACKLOG'}`.toUpperCase().trim()
  if (status === 'DONE') return 'fa-solid fa-circle-check status-done'
  if (status === 'IN PROGRESS') return 'fa-solid fa-circle-half-stroke status-progress'
  if (status === 'IN REVIEW') return 'fa-solid fa-eye status-review'
  return 'fa-regular fa-circle status-default'
}

const timeAgo = (value) => {
  const date = new Date(value)
  if (!value || Number.isNaN(date.getTime()) || date.getFullYear() <= 1970) return 'Just now'
  const seconds = Math.max(0, Math.floor((Date.now() - date.getTime()) / 1000))
  if (seconds < 60) return 'Just now'
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`
  if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`
  if (seconds < 2592000) return `${Math.floor(seconds / 86400)}d ago`
  return `${Math.floor(seconds / 2592000)}mo ago`
}
</script>

<style scoped>
.recent-popup-overlay {
  position: fixed;
  inset: 0;
  z-index: 9999;
  display: flex;
  justify-content: flex-end;
  background: rgba(5, 12, 24, 0.46);
  backdrop-filter: blur(3px);
}
.recent-sheet {
  width: min(400px, 100vw);
  height: 100vh;
  height: 100dvh;
  min-width: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  box-sizing: border-box;
  border-left: 1px solid var(--color-border, #dfe1e6);
  background: var(--color-bg, #f7f8fa);
  color: var(--color-text-primary, #172b4d);
  box-shadow: -12px 0 42px rgba(2, 6, 23, 0.2);
}
.sheet-header { min-height: 68px; padding: 14px 20px; display: flex; align-items: center; justify-content: space-between; box-sizing: border-box; border-bottom: 1px solid var(--color-border, #dfe1e6); }
.sheet-header h3 { min-width: 0; margin: 0; display: flex; align-items: center; gap: 10px; font-size: 16px; font-weight: 800; }

.icon-button,
.retry-button,
.recent-item {
  appearance: none;
  -webkit-appearance: none;
  border: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  padding: 0;
  cursor: pointer;
  touch-action: manipulation;
}
.icon-button { width: 40px; height: 40px; flex: 0 0 auto; display: grid; place-items: center; border-radius: 10px; color: var(--color-text-muted, #6b778c); }
.icon-button:hover { background: var(--color-surface-hover, #eef1f5); color: var(--color-text-primary, #172b4d); }

.sheet-search { position: relative; padding: 14px 20px; display: block; box-sizing: border-box; border-bottom: 1px solid var(--color-border, #dfe1e6); }
.search-icon { position: absolute; left: 33px; top: 50%; transform: translateY(-50%); color: var(--color-text-muted, #6b778c); font-size: 13px; pointer-events: none; }
.sheet-search input {
  appearance: none;
  -webkit-appearance: none;
  width: 100%;
  height: 40px;
  box-sizing: border-box;
  padding: 9px 12px 9px 36px;
  border: 1px solid var(--color-border, #dfe1e6);
  border-radius: 10px;
  outline: 0;
  background: var(--color-surface, #fff);
  color: var(--color-text-primary, #172b4d);
  font: inherit;
  font-size: 14px;
}
.sheet-search input:focus-visible { border-color: var(--color-accent, #0c66e4); box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-accent, #0c66e4) 18%, transparent); }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }

.sheet-body { min-height: 0; flex: 1; overflow-y: auto; }
.sheet-state { min-height: 280px; height: 100%; padding: 40px 24px; display: flex; flex-direction: column; align-items: center; justify-content: center; box-sizing: border-box; text-align: center; }
.sheet-state h4 { margin: 0 0 7px; font-size: 15px; font-weight: 800; }
.sheet-state p { max-width: 290px; margin: 0; color: var(--color-text-muted, #6b778c); font-size: 13px; line-height: 1.5; overflow-wrap: anywhere; }
.state-icon { width: 44px; height: 44px; margin-bottom: 16px; display: grid; place-items: center; color: var(--color-text-muted, #6b778c); font-size: 28px; }
.state-error .state-icon { color: #e34935; }
.state-spinner { width: 28px; height: 28px; margin-bottom: 18px; border: 3px solid color-mix(in srgb, var(--color-accent, #0c66e4) 18%, transparent); border-top-color: var(--color-accent, #0c66e4); border-radius: 50%; animation: popup-spin 0.8s linear infinite; }
@keyframes popup-spin { to { transform: rotate(360deg); } }
.retry-button { margin-top: 16px; padding: 9px 13px; border: 1px solid var(--color-border, #dfe1e6); border-radius: 9px; background: var(--color-surface, #fff); font-size: 13px; font-weight: 800; }
.retry-button:hover { background: var(--color-surface-hover, #eef1f5); }

.recent-list { margin: 0; padding: 8px; list-style: none; }
.recent-list li { margin: 0; padding: 0; }
.recent-item { width: 100%; min-height: 70px; padding: 11px 10px; display: flex; align-items: center; gap: 11px; box-sizing: border-box; border-radius: 11px; text-align: left; }
.recent-item:hover { background: var(--color-surface-hover, #eef1f5); }
.recent-item:disabled { cursor: default; }
.status-box { width: 30px; height: 30px; flex: 0 0 auto; display: grid; place-items: center; border-radius: 9px; background: var(--color-surface, #fff); }
.status-done { color: #0f9d72; }
.status-progress { color: #0c66e4; }
.status-review { color: #d97706; }
.status-default { color: var(--color-text-muted, #6b778c); }
.item-center { min-width: 0; flex: 1; display: flex; flex-direction: column; }
.item-center strong { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 14px; font-weight: 800; }
.item-meta { min-width: 0; margin-top: 4px; display: flex; align-items: center; gap: 9px; color: var(--color-text-muted, #6b778c); font-size: 11px; }
.item-key { flex: 0 0 auto; font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
.item-project { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.time-ago { flex: 0 0 auto; color: var(--color-text-muted, #6b778c); font-size: 11px; white-space: nowrap; }

.sheet-footer { padding: 13px 20px; border-top: 1px solid var(--color-border, #dfe1e6); text-align: center; }
.view-all-link { min-height: 36px; display: inline-flex; align-items: center; gap: 7px; color: var(--color-accent, #0c66e4); font-size: 13px; font-weight: 800; text-decoration: none; }
.view-all-link:hover { text-decoration: underline; }
.icon-button:focus-visible,
.retry-button:focus-visible,
.recent-item:focus-visible,
.view-all-link:focus-visible { outline: 3px solid color-mix(in srgb, var(--color-accent, #0c66e4) 42%, transparent); outline-offset: 2px; }

.slide-right-enter-active,
.slide-right-leave-active { transition: opacity 0.24s ease; }
.slide-right-enter-active .recent-sheet,
.slide-right-leave-active .recent-sheet { transition: transform 0.24s cubic-bezier(0.2, 0.7, 0.2, 1); }
.slide-right-enter-from,
.slide-right-leave-to { opacity: 0; }
.slide-right-enter-from .recent-sheet,
.slide-right-leave-to .recent-sheet { transform: translateX(100%); }

@media (max-width: 390px) {
  .sheet-header,
  .sheet-search,
  .sheet-footer { padding-left: 14px; padding-right: 14px; }
  .search-icon { left: 27px; }
  .item-meta { gap: 6px; }
  .time-ago { max-width: 58px; overflow: hidden; text-overflow: ellipsis; }
}
@media (prefers-reduced-motion: reduce) {
  .slide-right-enter-active,
  .slide-right-leave-active,
  .slide-right-enter-active .recent-sheet,
  .slide-right-leave-active .recent-sheet { transition: none; }
}
</style>
