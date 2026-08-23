<template>
  <div class="quick-panel">
    <header class="panel-header"><h3>Recent work</h3></header>

    <label class="panel-search">
      <span class="sr-only">Search recent items</span>
      <i class="fa-solid fa-magnifying-glass" aria-hidden="true"></i>
      <input v-model="searchQuery" type="search" placeholder="Search recent items" />
    </label>

    <div class="panel-body">
      <div v-if="starredStore.recentLoading" class="panel-state" role="status" aria-live="polite">
        <span class="state-spinner" aria-hidden="true"></span>
        <h4>Loading recent work</h4>
        <p>Finding the items you viewed most recently.</p>
      </div>

      <div v-else-if="starredStore.recentError" class="panel-state state-error" role="alert">
        <i class="fa-solid fa-triangle-exclamation state-icon" aria-hidden="true"></i>
        <h4>Recent work is unavailable</h4>
        <p>{{ starredStore.recentError }}</p>
        <button class="retry-button" type="button" @click="loadRecentItems">Try again</button>
      </div>

      <div v-else-if="filteredGroups.length === 0" class="panel-state">
        <i class="fa-regular fa-clock state-icon" aria-hidden="true"></i>
        <h4>{{ searchQuery ? 'No matching items' : 'No recent work yet' }}</h4>
        <p>{{ searchQuery ? 'Try a different title or project name.' : 'Items you open will appear here.' }}</p>
      </div>

      <div v-else class="panel-groups">
        <section v-for="group in filteredGroups" :key="group.label">
          <h4 class="group-label">{{ group.label }}</h4>
          <div
            v-for="item in group.items"
            :key="`${item.entityType}:${item.entityId}`"
            class="panel-item"
            :class="{ disabled: !item.url }"
            :role="item.url ? 'link' : undefined"
            :tabindex="item.url ? 0 : -1"
            @click="goToItem(item)"
            @keydown.enter.prevent="goToItem(item)"
            @keydown.space.prevent="goToItem(item)"
          >
            <span class="item-icon"><i :class="item.icon || 'fa-regular fa-eye'" aria-hidden="true"></i></span>
            <span class="item-copy">
              <strong>{{ item.title || 'Untitled item' }}</strong>
              <small>{{ item.subtitle || item.entityType }} · {{ timeAgo(item.viewedAt) }}</small>
            </span>
          </div>
        </section>
      </div>
    </div>

    <footer class="panel-footer">
      <button type="button" @click="viewAllRecent">View all recent activity</button>
    </footer>
  </div>
</template>

<script setup>
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useStarredStore } from '@/store/useStarredStore'

const emit = defineEmits(['close'])
const router = useRouter()
const starredStore = useStarredStore()
const searchQuery = ref('')

const loadRecentItems = () => starredStore.fetchRecentItems({ page: 1, pageSize: 20 }).catch(() => null)
defineExpose({ loadRecentItems })

const filteredGroups = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()
  const items = query
    ? starredStore.recentItems.filter(item => `${item.title || ''} ${item.subtitle || ''}`.toLowerCase().includes(query))
    : starredStore.recentItems
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const yesterday = new Date(today)
  yesterday.setDate(yesterday.getDate() - 1)
  const groups = { Today: [], Yesterday: [], Older: [] }

  items.forEach((item) => {
    const viewedAt = new Date(item.viewedAt)
    if (!Number.isNaN(viewedAt.getTime()) && viewedAt >= today) groups.Today.push(item)
    else if (!Number.isNaN(viewedAt.getTime()) && viewedAt >= yesterday) groups.Yesterday.push(item)
    else groups.Older.push(item)
  })

  return Object.entries(groups)
    .filter(([, groupItems]) => groupItems.length > 0)
    .map(([label, groupItems]) => ({ label, items: groupItems }))
})

const timeAgo = (value) => {
  const date = new Date(value)
  if (!value || Number.isNaN(date.getTime()) || date.getFullYear() <= 1970) return 'Just now'
  const seconds = Math.max(0, Math.floor((Date.now() - date.getTime()) / 1000))
  if (seconds < 60) return 'Just now'
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`
  if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`
  return `${Math.floor(seconds / 86400)}d ago`
}

const goToItem = (item) => {
  if (!item.url) return
  emit('close')
  router.push(item.url)
}

const viewAllRecent = () => {
  emit('close')
  router.push('/home/recent')
}
</script>

<style scoped>
.quick-panel {
  width: min(340px, calc(100vw - 24px));
  min-width: 0;
  min-height: min(520px, calc(100dvh - 32px));
  overflow: hidden;
  display: flex;
  flex-direction: column;
  box-sizing: border-box;
  border-radius: 14px;
  background: linear-gradient(180deg, color-mix(in srgb, var(--color-surface, #fff) 96%, var(--color-accent, #0c66e4) 4%), var(--color-surface, #fff));
  color: var(--color-text-primary, #172b4d);
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
}
.panel-header { padding: 16px 16px 8px; }
.panel-header h3,
.group-label { margin: 0; color: var(--color-text-muted, #6b778c); font-size: 11px; font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; }
.group-label { padding: 9px 12px 5px; font-size: 10px; }

.panel-search { position: relative; padding: 6px 12px 12px; display: block; }
.panel-search i { position: absolute; left: 25px; top: 25px; transform: translateY(-50%); color: var(--color-text-muted, #6b778c); font-size: 12px; pointer-events: none; }
.panel-search input {
  appearance: none;
  -webkit-appearance: none;
  width: 100%;
  height: 38px;
  box-sizing: border-box;
  padding: 7px 10px 7px 34px;
  border: 1px solid var(--color-border, #dfe1e6);
  border-radius: 11px;
  outline: 0;
  background: var(--color-surface-hover, #f4f5f7);
  color: var(--color-text-primary, #172b4d);
  font: inherit;
  font-size: 13px;
}
.panel-search input:focus-visible { border-color: var(--color-accent, #0c66e4); box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-accent, #0c66e4) 18%, transparent); }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }

.panel-body { min-height: 260px; max-height: min(560px, calc(100dvh - 150px)); flex: 1; overflow-y: auto; padding: 0 8px 8px; }
.panel-state { min-height: 230px; padding: 24px 16px; display: flex; flex-direction: column; align-items: center; justify-content: center; box-sizing: border-box; text-align: center; }
.panel-state h4 { margin: 0 0 6px; font-size: 14px; font-weight: 800; }
.panel-state p { max-width: 250px; margin: 0; color: var(--color-text-muted, #6b778c); font-size: 12px; line-height: 1.5; }
.state-icon { width: 40px; height: 40px; margin-bottom: 14px; display: grid; place-items: center; color: var(--color-text-muted, #6b778c); font-size: 26px; }
.state-error .state-icon { color: #e34935; }
.state-spinner { width: 24px; height: 24px; margin-bottom: 16px; border: 3px solid color-mix(in srgb, var(--color-accent, #0c66e4) 18%, transparent); border-top-color: var(--color-accent, #0c66e4); border-radius: 50%; animation: panel-spin 0.8s linear infinite; }
@keyframes panel-spin { to { transform: rotate(360deg); } }

.panel-item { min-height: 52px; padding: 7px 10px; display: flex; align-items: center; box-sizing: border-box; border-radius: 11px; cursor: pointer; transition: background 0.16s ease, transform 0.16s ease; }
.panel-item:hover { background: color-mix(in srgb, var(--color-accent, #0c66e4) 9%, var(--color-surface-hover, #f4f5f7)); transform: translateX(2px); }
.panel-item:focus-visible { outline: 3px solid color-mix(in srgb, var(--color-accent, #0c66e4) 38%, transparent); outline-offset: -2px; }
.panel-item.disabled { cursor: default; }
.item-icon { width: 28px; height: 28px; margin-right: 10px; flex: 0 0 auto; display: grid; place-items: center; border-radius: 8px; background: color-mix(in srgb, var(--color-accent, #0c66e4) 14%, transparent); color: var(--color-accent, #0c66e4); font-size: 13px; }
.item-copy { min-width: 0; flex: 1; display: flex; flex-direction: column; }
.item-copy strong,
.item-copy small { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.item-copy strong { font-size: 13px; font-weight: 800; }
.item-copy small { color: var(--color-text-muted, #6b778c); font-size: 11px; }

.panel-footer { padding: 9px 12px; border-top: 1px solid var(--color-border, #ebecf0); }
.panel-footer button,
.retry-button {
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
.panel-footer button { width: 100%; padding: 9px 10px; border-radius: 9px; color: var(--color-accent, #0c66e4); text-align: left; font-size: 13px; font-weight: 800; }
.panel-footer button:hover { background: var(--color-surface-hover, #f4f5f7); }
.retry-button { margin-top: 14px; padding: 8px 12px; border: 1px solid var(--color-border, #dfe1e6); border-radius: 9px; background: var(--color-surface, #fff); font-size: 12px; font-weight: 800; }
.retry-button:hover { background: var(--color-surface-hover, #f4f5f7); }
.panel-footer button:focus-visible,
.retry-button:focus-visible { outline: 3px solid color-mix(in srgb, var(--color-accent, #0c66e4) 42%, transparent); outline-offset: 2px; }

[data-theme='dark'] .quick-panel { background: linear-gradient(180deg, color-mix(in srgb, var(--color-surface, #162033) 94%, #2563eb 6%), var(--color-surface, #162033)); }
@media (hover: none) { .panel-item:hover { transform: none; } }
@media (prefers-reduced-motion: reduce) { .panel-item { transition: none; } }
</style>
