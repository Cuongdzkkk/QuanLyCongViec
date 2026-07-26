<template>
  <transition name="slide-right">
    <div v-if="isVisible" class="recent-popup-overlay" @click.self="closePopup">
      <div class="recent-sheet">
        <div class="sheet-header">
          <h3><i class="fa-solid fa-clock-rotate-left"></i> Recent Activity</h3>
          <button class="close-btn" @click="closePopup">
            <i class="fa-solid fa-xmark"></i>
          </button>
        </div>

        <div class="sheet-search">
          <i class="fa-solid fa-magnifying-glass search-icon"></i>
          <input type="text" v-model="searchQuery" placeholder="Search recent work items..." />
        </div>

        <div class="sheet-body">
          <div v-if="filteredRecentTasks.length === 0" class="empty-recent">
            <i class="fa-solid fa-folder-open empty-icon"></i>
            <p v-if="searchQuery">No recent items match your search.</p>
            <p v-else>No recent activity. View a work item to see it here.</p>
          </div>
          
          <ul v-else class="recent-list">
            <li 
              v-for="task in filteredRecentTasks" 
              :key="task.id" 
              class="recent-item"
              @click="goToTask(task)"
            >
              <div class="ri-left">
                <i :class="getStatusIcon(task.statusName)" class="status-icon"></i>
              </div>
              <div class="ri-center">
                <div class="ri-title">{{ task.title }}</div>
                <div class="ri-meta">
                  <span class="ri-key font-mono">{{ task.sequenceId || task.id.substring(0, 8).toUpperCase() }}</span>
                  <span class="ri-project">
                    <i class="fa-solid fa-briefcase"></i> {{ task.projectName }}
                  </span>
                </div>
              </div>
              <div class="ri-right">
                <span class="time-ago">{{ timeAgo(task.updatedAt) }}</span>
              </div>
            </li>
          </ul>
        </div>
        
        <div class="sheet-footer">
          <router-link :to="viewAllLink" class="view-all-link" @click="closePopup">
            Xem tất cả hoạt động gần đây <i class="fa-solid fa-arrow-right"></i>
          </router-link>
        </div>
      </div>
    </div>
  </transition>
</template>

<script setup>
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useStarredStore } from '@/store/useStarredStore'

const props = defineProps({
  isVisible: {
    type: Boolean,
    default: false
  }
})

const emit = defineEmits(['close'])

const router = useRouter()
const route = useRoute()
const starredStore = useStarredStore()
const searchQuery = ref('')
const recentTasks = computed(() => starredStore.recentItems.map(item => ({
  ...item,
  id: item.entityId,
  projectName: item.subtitle,
  updatedAt: item.viewedAt
})))

const loadRecentTasks = () => {
  starredStore.fetchRecentItems({ page: 1, pageSize: 20 }).catch(() => {})
}

watch(() => props.isVisible, (newVal) => {
  if (newVal) {
    searchQuery.value = ''
    loadRecentTasks()
    document.body.style.overflow = 'hidden'
  } else {
    document.body.style.overflow = ''
  }
})

onUnmounted(() => {
  document.body.style.overflow = ''
})

const filteredRecentTasks = computed(() => {
  if (!searchQuery.value.trim()) return recentTasks.value
  const q = searchQuery.value.toLowerCase().trim()
  return recentTasks.value.filter(t => 
    t.title?.toLowerCase().includes(q) || 
    t.sequenceId?.toLowerCase().includes(q) ||
    t.projectName?.toLowerCase().includes(q)
  )
})

const currentProjectId = computed(() => route.params.id || 'default')

const viewAllLink = computed(() => {
  // Go to For You page with tab=viewed
  return `/space/${currentProjectId.value}?tab=viewed`
})

const closePopup = () => {
  emit('close')
}

const goToTask = (task) => {
  // We navigate to the project's work-items page
  // Since we don't have a direct route to open a task by ID in the URL for now,
  // we just take the user to the project's board where they can see it.
  closePopup()
  if (task.url) router.push(task.url)
}

const getStatusIcon = (statusName) => {
  const s = `${statusName || 'BACKLOG'}`.toUpperCase().trim()
  if (s === 'DONE') return 'fa-solid fa-circle-check text-green-500'
  if (s === 'IN PROGRESS') return 'fa-solid fa-circle-half-stroke text-blue-500'
  if (s === 'IN REVIEW') return 'fa-solid fa-eye text-orange-500'
  if (s === 'TO DO' || s === 'TODO') return 'fa-regular fa-circle text-gray-400'
  return 'fa-regular fa-circle-dashed text-gray-400'
}

const timeAgo = (dateStr) => {
  if (!dateStr || dateStr.startsWith('0001-01-01')) return 'Vừa xong'
  const date = new Date(dateStr)
  if (isNaN(date.getTime()) || date.getFullYear() <= 1970) return 'Vừa xong'
  const seconds = Math.floor((new Date() - date) / 1000)
  if (seconds < 0) return 'Vừa xong'
  
  let interval = seconds / 31536000
  if (interval >= 1) return Math.floor(interval) + ' năm trước'
  interval = seconds / 2592000
  if (interval >= 1) return Math.floor(interval) + ' tháng trước'
  interval = seconds / 86400
  if (interval >= 1) return Math.floor(interval) + ' ngày trước'
  interval = seconds / 3600
  if (interval >= 1) return Math.floor(interval) + ' giờ trước'
  interval = seconds / 60
  if (interval >= 1) return Math.floor(interval) + ' phút trước'
  return 'Vừa xong'
}
</script>

<style scoped>
.recent-popup-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background: rgba(0, 0, 0, 0.4);
  backdrop-filter: blur(2px);
  z-index: 9999;
  display: flex;
  justify-content: flex-end;
}

.recent-sheet {
  width: 400px;
  max-width: 100%;
  height: 100vh;
  background: var(--color-bg);
  border-left: 1px solid var(--color-border);
  box-shadow: -4px 0 24px rgba(0, 0, 0, 0.15);
  display: flex;
  flex-direction: column;
}

.sheet-header {
  padding: 20px 24px;
  border-bottom: 1px solid var(--color-border);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.sheet-header h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 10px;
  color: var(--color-text-primary);
}

.close-btn {
  background: transparent;
  border: none;
  color: var(--color-text-muted);
  font-size: 16px;
  cursor: pointer;
  padding: 4px;
  border-radius: 4px;
  transition: all 0.2s;
}

.close-btn:hover {
  background: var(--color-border);
  color: var(--color-text-primary);
}

.sheet-search {
  padding: 16px 24px;
  border-bottom: 1px solid var(--color-border);
  position: relative;
}

.search-icon {
  position: absolute;
  left: 36px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--color-text-muted);
  font-size: 14px;
}

.sheet-search input {
  width: 100%;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  color: var(--color-text-primary);
  border-radius: 6px;
  padding: 10px 12px 10px 36px;
  font-size: 14px;
  outline: none;
  transition: border-color 0.2s;
}

.sheet-search input:focus {
  border-color: var(--color-accent);
}

.sheet-body {
  flex: 1;
  overflow-y: auto;
  padding: 0;
}

.empty-recent {
  padding: 60px 24px;
  text-align: center;
  color: var(--color-text-muted);
}

.empty-icon {
  font-size: 32px;
  margin-bottom: 16px;
  opacity: 0.5;
}

.recent-list {
  list-style: none;
  margin: 0;
  padding: 0;
}

.recent-item {
  display: flex;
  padding: 16px 24px;
  border-bottom: 1px solid var(--color-border);
  cursor: pointer;
  transition: background 0.2s;
  gap: 12px;
}

.recent-item:hover {
  background: var(--color-surface-hover);
}

.ri-left {
  padding-top: 2px;
}

.ri-center {
  flex: 1;
  min-width: 0; /* for truncation */
}

.ri-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text-primary);
  margin-bottom: 4px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.ri-meta {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 12px;
  color: var(--color-text-muted);
}

.ri-project {
  display: flex;
  align-items: center;
  gap: 4px;
}

.ri-right {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  justify-content: flex-start;
}

.time-ago {
  font-size: 11px;
  color: var(--color-text-muted);
}

.sheet-footer {
  padding: 16px 24px;
  border-top: 1px solid var(--color-border);
  text-align: center;
}

.view-all-link {
  color: var(--color-accent);
  font-size: 13px;
  font-weight: 500;
  text-decoration: none;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  transition: opacity 0.2s;
}

.view-all-link:hover {
  opacity: 0.8;
  text-decoration: underline;
}

/* Transitions */
.slide-right-enter-active,
.slide-right-leave-active {
  transition: opacity 0.3s ease;
}

.slide-right-enter-active .recent-sheet,
.slide-right-leave-active .recent-sheet {
  transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.slide-right-enter-from,
.slide-right-leave-to {
  opacity: 0;
}

.slide-right-enter-from .recent-sheet,
.slide-right-leave-to .recent-sheet {
  transform: translateX(100%);
}
</style>
