<template>
  <div class="jd-content">
    <div class="jd-header">
      <h3>Recent work</h3>
    </div>

    <div class="jd-search">
      <i class="fa-solid fa-magnifying-glass"></i>
      <input type="text" v-model="searchQuery" placeholder="Search recent items" />
    </div>

    <div class="jd-body">
      <div v-if="starredStore.recentLoading" class="jd-empty">Loading recent work...</div>
      <div v-else-if="starredStore.recentError" class="jd-empty">
        <p>{{ starredStore.recentError }}</p>
        <button type="button" @click="loadRecentItems">Retry</button>
      </div>
      <div v-else-if="filteredGroups.length === 0" class="jd-empty">
        <i class="fa-solid fa-box-open" style="font-size: 48px; color: var(--color-text-muted); margin-bottom: 16px;"></i>
        <p v-if="searchQuery">No matching items found</p>
        <p v-else>You haven't viewed any items recently</p>
      </div>

      <div v-else class="jd-groups">
        <div v-for="group in filteredGroups" :key="group.label" class="jd-group mb-4">
          <div class="jd-group-label">{{ group.label }}</div>
          
          <div 
            v-for="item in group.items" 
            :key="item.id" 
            class="jd-item"
            @click="goToItem(item)"
          >
            <div class="jd-item-icon">
              <i :class="item.icon || 'fa-regular fa-eye'"></i>
            </div>
            <div class="jd-item-content">
              <div class="jd-item-title">{{ item.title }}</div>
              <div class="jd-item-subtitle">{{ item.subtitle || item.entityType }} • {{ timeAgo(item.viewedAt) }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="jd-footer">
      <button @click="viewAllRecent">Xem tất cả hoạt động gần đây</button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useStarredStore } from '@/store/useStarredStore'

const emit = defineEmits(['close'])
const router = useRouter()
const starredStore = useStarredStore()
const searchQuery = ref('')

const recentItems = computed(() => starredStore.recentItems)
const loadRecentItems = () => starredStore.fetchRecentItems({ page: 1, pageSize: 20 }).catch(() => {})
defineExpose({ loadRecentItems })

const filteredGroups = computed(() => {
  let list = recentItems.value

  if (searchQuery.value.trim()) {
    const q = searchQuery.value.toLowerCase()
    list = list.filter(t => t.title?.toLowerCase().includes(q) || t.projectName?.toLowerCase().includes(q))
  }

  const groups = {
    'Today': [],
    'Yesterday': [],
    'Older': []
  }

  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const yesterday = new Date(today)
  yesterday.setDate(yesterday.getDate() - 1)

  list.forEach(item => {
    const d = new Date(item.viewedAt || Date.now())
    if (d >= today) groups['Today'].push(item)
    else if (d >= yesterday) groups['Yesterday'].push(item)
    else groups['Older'].push(item)
  })

  return Object.entries(groups).filter(([_, items]) => items.length > 0).map(([label, items]) => ({ label, items }))
})

const timeAgo = (dateStr) => {
  if (!dateStr || dateStr.startsWith('0001-01-01')) return 'Vừa xong'
  const date = new Date(dateStr)
  if (isNaN(date.getTime()) || date.getFullYear() <= 1970) return 'Vừa xong'
  const seconds = Math.floor((new Date() - date) / 1000)
  if (seconds < 0) return 'Vừa xong'
  let interval = seconds / 3600
  if (interval >= 1) return Math.floor(interval) + ' giờ trước'
  interval = seconds / 60
  if (interval >= 1) return Math.floor(interval) + ' phút trước'
  return 'Vừa xong'
}

const goToItem = (item) => {
  emit('close')
  if (item.url) router.push(item.url)
}

const viewAllRecent = () => {
  emit('close')
  router.push('/home/recent')
}
</script>

<style scoped>
.jd-content {
  display: flex;
  flex-direction: column;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 96%, var(--color-accent) 4%), var(--color-surface));
  color: var(--color-text-primary, #172b4d);
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
  border-radius: 16px;
  overflow: hidden;
  min-width: 320px;
}

.jd-header {
  padding: 16px 16px 8px;
}
.jd-header h3 {
  margin: 0;
  font-size: 12px;
  text-transform: uppercase;
  color: var(--color-text-muted, #6b778c);
  font-weight: 850;
  letter-spacing: 0.08em;
}

.jd-search {
  position: relative;
  padding: 8px 12px 12px;
  display: flex;
  align-items: center;
}
.jd-search i {
  position: absolute;
  left: 24px;
  top: 50%;
  transform: translateY(-50%);
  font-size: 12px;
  color: var(--color-text-muted, #6b778c);
  pointer-events: none;
  z-index: 1;
}
.jd-search input {
  width: 100% !important;
  box-sizing: border-box !important;
  border: 1px solid var(--color-border, #dfe1e6) !important;
  border-radius: 16px !important;
  padding: 7px 10px 7px 32px !important;
  font-size: 13px !important;
  height: 38px !important;
  outline: none !important;
  background: var(--color-surface-hover, #f4f5f7) !important;
  color: var(--color-text-primary, #172b4d) !important;
  box-shadow: none !important;
  transition: border-color 0.2s ease, background 0.2s ease;
}
.jd-search input:focus {
  border-color: var(--color-accent, #4c9aff) !important;
  background: var(--color-surface, #fff) !important;
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-accent, #0c66e4) 12%, transparent) !important;
}

.jd-body {
  flex: 1;
  overflow-y: auto;
  max-height: 320px;
  padding: 0 8px 8px;
}

.jd-empty {
  text-align: center;
  padding: 32px 16px;
  color: var(--color-text-muted, #6b778c);
  font-size: 13px;
}
.jd-empty img {
  width: 80px;
  margin: 0 auto 12px;
  opacity: 0.7;
}

.jd-group-label {
  font-size: 11px;
  font-weight: 700;
  color: var(--color-text-muted, #6b778c);
  text-transform: uppercase;
  padding: 8px 16px 4px;
}

.jd-item {
  display: flex;
  align-items: center;
  padding: 10px 10px;
  cursor: pointer;
  border-radius: 12px;
  transition: background 0.16s ease, transform 0.16s ease;
}
.jd-item:hover {
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface-hover));
  transform: translateX(2px);
}

.jd-item-icon {
  margin-right: 12px;
  font-size: 16px;
  display: flex;
  align-items: center;
}

.jd-item-content {
  flex: 1;
  min-width: 0;
}

.jd-item-title {
  font-size: 13px;
  font-weight: 800;
  color: var(--color-text-primary, #172b4d);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.jd-item-subtitle {
  font-size: 11px;
  color: var(--color-text-muted, #6b778c);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.jd-footer {
  padding: 10px 14px;
  border-top: 1px solid var(--color-border, #ebecf0);
}

.jd-footer button {
  width: 100%;
  text-align: left;
  background: transparent;
  border: none;
  color: var(--color-accent, #0c66e4);
  font-size: 13px;
  font-weight: 800;
  padding: 8px;
  border-radius: 10px;
  cursor: pointer;
}
.jd-footer button:hover {
  background: var(--color-surface-hover, #f4f5f7);
  text-decoration: none;
}

[data-theme='dark'] .jd-content {
  background:
    linear-gradient(180deg, rgba(30, 41, 59, 0.96), rgba(15, 23, 42, 0.98)),
    #0f172a;
}
</style>
