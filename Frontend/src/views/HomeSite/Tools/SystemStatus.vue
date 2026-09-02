<template>
  <div class="status-page">
    <header class="page-header">
      <div class="header-main">
        <h1>{{ labels.title }}</h1>
        <select class="secondary-btn" v-model="rangeDays">
          <option :value="7">7 {{ labels.days }}</option>
          <option :value="14">14 {{ labels.days }}</option>
          <option :value="30">30 {{ labels.days }}</option>
          <option :value="90">90 {{ labels.days }}</option>
        </select>
      </div>
      <div class="tabs">
        <button class="tab-btn" :class="{ active: currentTab === 'projects' }" @click="currentTab = 'projects'">
          {{ labels.projects }}
        </button>
        <button class="tab-btn" :class="{ active: currentTab === 'goals' }" @click="currentTab = 'goals'">
          {{ labels.goals }}
        </button>
      </div>
    </header>

    <main class="page-content">
      <section class="main-column">
        <div class="timeline-header">
          <button class="icon-btn" @click="shiftRange(-1)" :aria-label="labels.previous">
            <i class="fa-solid fa-arrow-left"></i>
          </button>
          <h2 class="timeline-title">{{ rangeLabel }}</h2>
          <button class="icon-btn" @click="shiftRange(1)" :aria-label="labels.next">
            <i class="fa-solid fa-arrow-right"></i>
          </button>
        </div>

        <p class="stats-subtitle">
          {{ labels.followingPrefix }} {{ filteredItems.length }} {{ itemType }} {{ labels.followingSuffix }}
        </p>

        <div class="stats-grid">
          <div class="stat-card" v-for="stat in stats" :key="stat.key">
            <div class="stat-number" :class="stat.color">{{ stat.count }}</div>
            <div class="stat-info">
              <div class="stat-label" :class="stat.color">{{ stat.label }}</div>
              <div class="stat-desc">{{ stat.description }}</div>
            </div>
          </div>
        </div>

        <div class="tracked-items-list" v-if="!isLoading">
          <article class="tracked-item" v-for="item in filteredItems" :key="item.id">
            <div class="item-header">
              <span class="item-icon"><i :class="currentTab === 'projects' ? 'fa-solid fa-rocket' : 'fa-solid fa-bullseye'"></i></span>
              <div class="item-name-col">
                <span class="item-type-label">{{ itemTypeCapitalized }}</span>
                <span class="item-name">{{ item.name || item.title }}</span>
              </div>
            </div>
            <div class="item-card-inner">
              <div class="item-body">
                <div class="item-user">
                  <div class="item-avatar">{{ getInitials(item.ownerName || 'U') }}</div>
                  <div class="item-user-info">
                    <span class="item-user-name">{{ item.ownerName || labels.unassigned }}</span>
                    <span class="item-time">{{ formatDate(item.updatedAt || item.createdAt) }}</span>
                  </div>
                </div>
                <div class="item-status-badge" :class="statusColor(item.status)">{{ normalizeStatus(item.status) }}</div>
              </div>
              <div class="item-message">{{ labels.currentProgress }} {{ item.progress ?? (item.isArchived ? 100 : 0) }}%</div>
            </div>
          </article>

          <div v-if="filteredItems.length === 0" class="empty-state">{{ labels.noData }}</div>
        </div>
        <div v-else class="empty-state">{{ labels.loading }}</div>
      </section>

      <aside class="sidebar-column">
        <div class="sidebar-section">
          <h3>{{ itemTypeCapitalized }} {{ labels.newSuffix }}</h3>
          <div class="sidebar-item" v-for="item in newestItems" :key="item.id">
            <div class="sidebar-item-left">
              <span class="sidebar-item-icon"><i :class="currentTab === 'projects' ? 'fa-solid fa-rocket' : 'fa-solid fa-bullseye'"></i></span>
              <span class="sidebar-item-name">{{ item.name || item.title }}</span>
            </div>
          </div>
        </div>
      </aside>
    </main>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import axiosClient from '@/api/axiosClient'
import { useI18nStore } from '@/store/useI18nStore'

const i18nStore = useI18nStore()
const currentTab = ref('projects')
const rangeDays = ref(7)
const rangeOffset = ref(0)
const dashboardItems = ref([])
const dashboardStats = ref(null)
const isLoading = ref(false)

const labels = computed(() => i18nStore.locale === 'vi'
  ? {
      title: 'Cập nhật trạng thái',
      days: 'ngày',
      projects: 'Dự án',
      goals: 'Mục tiêu',
      previous: 'Khoảng trước',
      next: 'Khoảng sau',
      projectType: 'dự án',
      goalType: 'mục tiêu',
      followingPrefix: 'Bạn đang theo dõi',
      followingSuffix: 'trong khoảng thời gian này.',
      newest: 'Mới tạo',
      updated: 'Mới cập nhật',
      risk: 'Có rủi ro',
      pending: 'Đang chờ xử lý',
      done: 'Đã hoàn tất',
      lastDays: 'Trong 7 ngày gần đây',
      changed: 'Có thay đổi gần đây',
      needsAttention: 'Cần chú ý',
      noClearUpdate: 'Chưa có cập nhật rõ',
      finished: 'Đã kết thúc',
      unassigned: 'Chưa gắn',
      currentProgress: 'Tiến độ hiện tại:',
      noData: 'Không có dữ liệu trạng thái trong khoảng thời gian này.',
      loading: 'Đang tải dữ liệu...',
      newSuffix: 'mới',
      active: 'Đang thực hiện',
      archived: 'Đã lưu trữ'
    }
  : {
      title: 'Status updates',
      days: 'days',
      projects: 'Projects',
      goals: 'Goals',
      previous: 'Previous range',
      next: 'Next range',
      projectType: 'projects',
      goalType: 'goals',
      followingPrefix: 'You are tracking',
      followingSuffix: 'in this time range.',
      newest: 'New',
      updated: 'Updated',
      risk: 'At risk',
      pending: 'Pending',
      done: 'Completed',
      lastDays: 'In the last 7 days',
      changed: 'Changed recently',
      needsAttention: 'Needs attention',
      noClearUpdate: 'No clear update',
      finished: 'Finished',
      unassigned: 'Unassigned',
      currentProgress: 'Current progress:',
      noData: 'No status data in this time range.',
      loading: 'Loading data...',
      newSuffix: 'new',
      active: 'Active',
      archived: 'Archived'
    })

const itemType = computed(() => currentTab.value === 'projects' ? labels.value.projectType : labels.value.goalType)
const itemTypeCapitalized = computed(() => currentTab.value === 'projects' ? labels.value.projects : labels.value.goals)

const rangeStart = computed(() => {
  const date = new Date()
  date.setDate(date.getDate() - rangeDays.value * (rangeOffset.value + 1))
  return date
})

const rangeEnd = computed(() => {
  const date = new Date()
  date.setDate(date.getDate() - rangeDays.value * rangeOffset.value)
  return date
})

const rangeLabel = computed(() => `${rangeStart.value.toLocaleDateString('vi-VN')} - ${rangeEnd.value.toLocaleDateString('vi-VN')}`)
const filteredItems = computed(() => dashboardItems.value || [])

const stats = computed(() => {
  const serverStats = dashboardStats.value || {}
  return [
    { key: 'newest', label: labels.value.newest, color: 'green', count: serverStats.newest || 0, description: labels.value.lastDays },
    { key: 'updated', label: labels.value.updated, color: 'blue', count: serverStats.updated || 0, description: labels.value.changed },
    { key: 'risk', label: labels.value.risk, color: 'orange', count: serverStats.risk || 0, description: labels.value.needsAttention },
    { key: 'pending', label: labels.value.pending, color: 'gray', count: serverStats.pending || 0, description: labels.value.noClearUpdate },
    { key: 'done', label: labels.value.done, color: 'blue', count: serverStats.done || 0, description: labels.value.finished }
  ]
})

const newestItems = computed(() => [...(dashboardItems.value || [])]
  .sort((a, b) => new Date(b.createdAt || 0) - new Date(a.createdAt || 0))
  .slice(0, 5))

const normalizeStatus = (status) => {
  if (status === true) return labels.value.active
  if (status === false) return labels.value.archived
  return status || labels.value.pending
}

const statusColor = (status) => {
  const normalized = normalizeStatus(status).toLowerCase()
  if (normalized.includes('risk') || normalized.includes('rủi ro')) return 'orange-badge'
  if (normalized.includes('off') || normalized.includes('không')) return 'red-badge'
  if (normalized.includes('complete') || normalized.includes('hoàn') || normalized.includes('done')) return 'blue-badge'
  return 'green-badge'
}

const getInitials = (value = '') => value.trim().split(/\s+/).slice(0, 2).map((part) => part[0]).join('').toUpperCase() || 'U'
const formatDate = (value) => value ? new Date(value).toLocaleString('vi-VN') : ''
const shiftRange = (direction) => { rangeOffset.value = Math.max(0, rangeOffset.value + direction) }

const fetchStatusDashboard = async () => {
  isLoading.value = true
  try {
    const res = await axiosClient.get('/status-dashboard', {
      params: { type: currentTab.value, days: rangeDays.value }
    })
    dashboardItems.value = res.data?.data?.items || []
    dashboardStats.value = res.data?.data?.stats || null
  } catch (err) {
    console.error('Failed to load status dashboard', err)
    dashboardItems.value = []
    dashboardStats.value = null
  } finally {
    isLoading.value = false
  }
}

watch([rangeDays, currentTab], () => {
  rangeOffset.value = 0
  fetchStatusDashboard()
})

onMounted(fetchStatusDashboard)
</script>

<style scoped>
.status-page {
  min-height: 100vh;
  background: var(--home-bg, #ffffff);
  color: var(--home-text, #172b4d);
}

.page-header {
  padding: 32px 40px 0;
  border-bottom: 1px solid var(--home-border, #dfe1e6);
}

.header-main {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 24px;
  gap: 16px;
}

.header-main h1 {
  margin: 0;
  color: var(--home-text, #172b4d);
  font-size: 28px;
  font-weight: 800;
}

.secondary-btn,
.icon-btn {
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 8px;
  background: var(--home-panel, #ffffff);
  color: var(--home-text, #42526e);
  cursor: pointer;
}

.secondary-btn {
  padding: 8px 12px;
  font-weight: 700;
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
  font-weight: 700;
  cursor: pointer;
}

.tab-btn.active,
.tab-btn:hover {
  background: var(--home-panel, #ffffff);
  border-color: var(--home-border, #dfe1e6);
  color: var(--home-accent, #0052cc);
}

.page-content {
  display: grid;
  grid-template-columns: minmax(0, 850px) minmax(240px, 320px);
  gap: 32px;
  padding: 32px 40px;
  min-width: 0;
  box-sizing: border-box;
}

.main-column {
  min-width: 0;
}

.timeline-header {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 20px;
  margin-bottom: 24px;
}

.timeline-title {
  min-width: 0;
  margin: 0;
  color: var(--home-muted, #5e6c84);
  font-size: 18px;
  font-weight: 800;
}

.icon-btn {
  width: 34px;
  height: 34px;
}

.icon-btn:hover,
.secondary-btn:hover {
  border-color: rgba(56, 189, 248, 0.6);
  background: var(--home-panel-strong, #fafbfc);
}

.stats-subtitle {
  margin: 0 0 16px;
  color: var(--home-text, #172b4d);
  font-size: 14px;
  font-weight: 700;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 14px;
  margin-bottom: 34px;
}

.stat-card,
.item-card-inner,
.sidebar-section {
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 12px;
  background: var(--home-panel, #ffffff);
}

.stat-card {
  display: flex;
  gap: 12px;
  padding: 14px;
}

.stat-number {
  font-size: 24px;
  font-weight: 800;
}

.green { color: #10b981; }
.orange { color: #f59e0b; }
.red { color: #ef4444; }
.gray { color: var(--home-muted, #5e6c84); }
.blue { color: var(--home-accent, #0052cc); }

.stat-label {
  font-size: 12px;
  font-weight: 800;
}

.stat-desc,
.item-type-label,
.item-time {
  color: var(--home-muted, #5e6c84);
  font-size: 12px;
}

.tracked-items-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.tracked-item {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.item-header,
.item-user,
.sidebar-item-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.item-icon,
.sidebar-item-icon {
  color: var(--home-accent, #0052cc);
}

.item-name-col,
.item-user-info {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.item-name,
.item-user-name,
.sidebar-item-name {
  color: var(--home-text, #172b4d);
  font-weight: 800;
  overflow-wrap: anywhere;
}

.item-card-inner {
  margin-left: 24px;
  padding: 16px;
}

.item-body {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
}

.item-avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #00875a;
  color: white;
  font-size: 12px;
  font-weight: 800;
}

.item-status-badge {
  padding: 4px 8px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 800;
}

.green-badge { background: rgba(34, 197, 94, 0.14); color: #10b981; }
.orange-badge { background: rgba(245, 158, 11, 0.16); color: #f59e0b; }
.red-badge { background: rgba(239, 68, 68, 0.16); color: #ef4444; }
.blue-badge { background: rgba(59, 130, 246, 0.16); color: #3b82f6; }

.item-message {
  margin-top: 12px;
  padding: 12px;
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 10px;
  background: var(--home-panel-strong, #fafbfc);
  color: var(--home-text, #172b4d);
}

.sidebar-column {
  border-left: 1px solid var(--home-border, #dfe1e6);
  padding-left: 28px;
}

.sidebar-section {
  padding: 16px;
}

.sidebar-section h3 {
  margin: 0 0 12px;
  color: var(--home-text, #172b4d);
  font-size: 14px;
}

.sidebar-item {
  padding: 8px 0;
}

.empty-state {
  padding: 32px;
  border: 1px dashed var(--home-border, #dfe1e6);
  border-radius: 12px;
  color: var(--home-muted, #5e6c84);
  text-align: center;
}

@media (max-width: 980px) {
  .page-content {
    grid-template-columns: 1fr;
  }

  .sidebar-column {
    border-left: 0;
    padding-left: 0;
  }
}

@media (max-width: 640px) {
  .page-header {
    padding: 20px 14px 0;
  }

  .header-main {
    flex-direction: column;
    align-items: stretch;
    margin-bottom: 16px;
  }

  .header-main h1 {
    font-size: 24px;
    line-height: 1.2;
    word-break: normal;
  }

  .secondary-btn {
    width: 100%;
    min-height: 44px;
  }

  .tabs {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .tab-btn {
    min-height: 44px;
  }

  .page-content {
    gap: 20px;
    padding: 24px 14px;
  }

  .timeline-header {
    display: grid;
    grid-template-columns: 44px minmax(0, 1fr) 44px;
    gap: 8px;
    margin-bottom: 20px;
  }

  .timeline-title {
    text-align: center;
    font-size: 15px;
    line-height: 1.35;
  }

  .icon-btn {
    width: 44px;
    height: 44px;
  }

  .stats-grid {
    grid-template-columns: 1fr;
    gap: 10px;
    margin-bottom: 24px;
  }

  .item-card-inner {
    margin-left: 0;
    padding: 14px;
  }

  .item-body {
    align-items: flex-start;
    flex-wrap: wrap;
  }

  .item-message {
    overflow-wrap: anywhere;
  }
}
</style>
