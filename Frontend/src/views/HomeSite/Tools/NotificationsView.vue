<template>
  <div class="notifications-page">
    <div class="page-header">
      <h1>{{ t('homeSite.notifications.title') }}</h1>
    </div>

    <div class="notifications-layout">
      <aside class="filters-sidebar">
        <button class="filter-btn" :class="{ active: filter === 'all' }" @click="filter = 'all'">
          <i class="fa-regular fa-square-check"></i>
          {{ t('homeSite.notifications.all') }}
        </button>
        <button class="filter-btn" :class="{ active: filter === 'unread' }" @click="filter = 'unread'">
          <i class="fa-regular fa-bell"></i>
          {{ t('homeSite.notifications.unread') }}
        </button>
      </aside>

      <main class="notifications-main">
        <div class="main-header">
          <h2>{{ t('homeSite.notifications.latest') }}</h2>
          <div class="header-actions">
            <button class="text-action-btn" @click="markAllAsRead">
              {{ t('homeSite.notifications.markAllRead') }}
            </button>
            <label class="toggle-wrapper">
              <span class="toggle-label">{{ t('homeSite.notifications.unreadOnly') }}</span>
              <input type="checkbox" v-model="showUnreadOnly">
            </label>
          </div>
        </div>

        <div v-if="loading" class="empty-state">{{ t('homeSite.notifications.loading') }}</div>
        <div v-else-if="visibleNotifications.length === 0" class="empty-state">
          {{ t('homeSite.notifications.empty') }}
        </div>

        <div v-else class="notifications-list">
          <div class="time-group-title">{{ t('homeSite.notifications.latest') }}</div>
          <button
            class="notification-item"
            v-for="notification in visibleNotifications"
            :key="notification.id"
            @click="openNotification(notification)"
          >
            <UserAvatar
              :user="{ fullName: notification.triggeredByName || notification.title, avatarUrl: notification.triggeredByAvatar }"
              :size="32"
              :fontSize="12"
            />
            <div class="notif-content">
              <div class="notif-header">
                <strong>{{ notification.title || notification.notificationType }}</strong>
                <span class="notif-time">{{ formatTime(notification.createdAt) }}</span>
              </div>
              <div class="notif-link">{{ notification.content }}</div>
              <div class="notif-meta">{{ notification.notificationType || t('homeSite.notifications.notification') }}</div>
            </div>
            <div v-if="!notification.isRead" class="notif-status unread"></div>
          </button>
        </div>
      </main>
    </div>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import UserAvatar from '@/components/common/UserAvatar.vue'
import { useI18nStore } from '@/store/useI18nStore'

const router = useRouter()
const i18nStore = useI18nStore()
const t = i18nStore.t
const notifications = ref([])
const loading = ref(false)
const filter = ref('all')
const showUnreadOnly = ref(false)
let notificationRequestId = 0
let notificationAbortController = null

const visibleNotifications = computed(() => {
  return notifications.value.filter(item => filter.value !== 'unread' || !item.isRead)
})

const fetchNotifications = async () => {
  notificationAbortController?.abort()
  const controller = new AbortController()
  notificationAbortController = controller
  const requestId = ++notificationRequestId
  loading.value = true
  try {
    const response = await axiosClient.get('/notifications', {
      params: { unreadOnly: showUnreadOnly.value || filter.value === 'unread' },
      signal: controller.signal
    })
    if (requestId !== notificationRequestId) return
    notifications.value = response.data?.data || response.data || []
  } catch (error) {
    if (error?.code !== 'ERR_CANCELED') throw error
  } finally {
    if (requestId === notificationRequestId) {
      loading.value = false
      notificationAbortController = null
    }
  }
}

const markAllAsRead = async () => {
  await axiosClient.put('/notifications/read-all')
  await fetchNotifications()
}

const openNotification = async (notification) => {
  if (!notification.isRead) {
    await axiosClient.put(`/notifications/${notification.id}/read`)
  }
  if (notification.linkUrl) {
    router.push(notification.linkUrl)
    return
  }
  await fetchNotifications()
}

const formatTime = (value) => {
  if (!value) return ''
  return new Date(value).toLocaleString(i18nStore.locale === 'vi' ? 'vi-VN' : 'en-US')
}

const handleMentionRefresh = () => { void fetchNotifications() }
const handleNotificationReset = () => {
  notificationAbortController?.abort()
  notificationRequestId += 1
  notifications.value = []
}

watch([filter, showUnreadOnly], fetchNotifications)
onMounted(() => {
  window.addEventListener('collaboration-mention-created', handleMentionRefresh)
  window.addEventListener('collaboration-notifications-refresh', handleMentionRefresh)
  window.addEventListener('collaboration-notifications-reset', handleNotificationReset)
  void fetchNotifications()
})
onBeforeUnmount(() => {
  notificationAbortController?.abort()
  notificationRequestId += 1
  window.removeEventListener('collaboration-mention-created', handleMentionRefresh)
  window.removeEventListener('collaboration-notifications-refresh', handleMentionRefresh)
  window.removeEventListener('collaboration-notifications-reset', handleNotificationReset)
})
</script>

<style scoped>
.notifications-page { color: #172B4D; background: #fff; min-height: 100vh; }
.page-header { padding: 32px 40px 16px; }
.page-header h1 { font-size: 24px; font-weight: 500; margin: 0; }
.notifications-layout { display: flex; gap: 32px; padding: 0 40px 40px; }
.filters-sidebar { width: 220px; display: flex; flex-direction: column; gap: 4px; }
.filter-btn { display: flex; align-items: center; gap: 12px; border: 0; background: transparent; padding: 8px 12px; border-radius: 3px; color: #42526E; cursor: pointer; text-align: left; }
.filter-btn.active { background: #E6FCFF; color: #0052CC; font-weight: 600; }
.notifications-main { flex: 1; max-width: 900px; }
.main-header { display: flex; justify-content: space-between; align-items: center; padding-bottom: 16px; border-bottom: 1px solid #DFE1E6; margin-bottom: 16px; }
.main-header h2 { font-size: 16px; margin: 0; }
.header-actions { display: flex; gap: 24px; align-items: center; }
.text-action-btn { background: transparent; border: 0; color: #0052CC; cursor: pointer; }
.toggle-wrapper { display: flex; gap: 8px; align-items: center; font-size: 13px; color: #5E6C84; }
.time-group-title { font-size: 12px; color: #5E6C84; margin-bottom: 8px; }
.notification-item { width: 100%; display: flex; gap: 16px; padding: 16px 0; border: 0; border-bottom: 1px solid #DFE1E6; background: transparent; text-align: left; cursor: pointer; }
.notification-item:hover { background: #FAFBFC; }
.notif-avatar { width: 32px; height: 32px; border-radius: 50%; background: #172B4D; color: white; display: flex; align-items: center; justify-content: center; font-size: 12px; font-weight: 700; flex-shrink: 0; }
.notif-avatar.read { background: #6B778C; }
.notif-content { flex: 1; display: flex; flex-direction: column; gap: 4px; }
.notif-header { font-size: 14px; }
.notif-time { color: #5E6C84; margin-left: 8px; }
.notif-link { color: #172B4D; font-size: 14px; }
.notif-meta { color: #5E6C84; font-size: 12px; }
.notif-status { width: 8px; height: 8px; border-radius: 50%; margin-top: 6px; }
.notif-status.unread { background: #0052CC; }
.empty-state { padding: 48px 0; color: #5E6C84; text-align: center; }
</style>
