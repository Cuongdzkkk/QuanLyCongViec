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
          <div
            class="notification-item"
            role="button"
            tabindex="0"
            v-for="notification in visibleNotifications"
            :key="notification.id"
            @click="openNotification(notification)"
            @keydown.enter="openNotification(notification)"
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
              <div v-if="isPendingInvitation(notification)" class="invitation-actions" @click.stop>
                <button type="button" @click="acceptInvitation(notification)">Chấp nhận</button>
                <button type="button" @click="declineInvitation(notification)">Từ chối</button>
              </div>
              <div v-else-if="isResolvedInvitation(notification)" class="invitation-resolved">
                {{ notification.actionState === 'Accepted' ? 'Bạn đã tham gia dự án này.' : 'Bạn đã từ chối lời mời tham gia dự án này.' }}
              </div>
            </div>
            <div v-if="!notification.isRead" class="notif-status unread"></div>
          </div>
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
import { ElMessage } from 'element-plus'
import {
  isPendingInvitation,
  isResolvedInvitation,
  navigateNotification,
  normalizeNotification
} from '@/utils/notificationNavigation'

const router = useRouter()
const i18nStore = useI18nStore()
const t = i18nStore.t
const notifications = ref([])
const loading = ref(false)
const filter = ref('all')
const showUnreadOnly = ref(false)
const invitationActionId = ref(null)
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
    const rows = response.data?.data || response.data || []
    notifications.value = (Array.isArray(rows) ? rows : []).map(normalizeNotification)
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
  try {
    if (!notification.isRead) {
      await axiosClient.put(`/notifications/${notification.id}/read`)
      notification.isRead = true
    }
    await navigateNotification(router, notification, {
      fetchProject: async projectId => {
        const response = await axiosClient.get(`/projects/${projectId}`)
        return response.data?.data || response.data
      },
      onDenied: () => ElMessage.warning('Bạn không còn quyền truy cập dự án này.'),
      onInvalid: () => ElMessage.info('Thông báo này không còn liên kết hợp lệ.')
    })
  } catch (error) {
    if (error?.response?.status === 403) {
      ElMessage.warning('Bạn không còn quyền truy cập dự án này.')
      return
    }
    ElMessage.error('Không thể mở thông báo này.')
  }
}

const acceptInvitation = async (notification) => {
  if (!isPendingInvitation(notification) || invitationActionId.value) return
  invitationActionId.value = notification.id
  try {
    await axiosClient.post(`/project-invitations/${notification.relatedInvitationId}/accept`)
    notification.actionState = 'Accepted'
    notification.isRead = true
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể chấp nhận lời mời.')
  } finally {
    invitationActionId.value = null
  }
}

const declineInvitation = async (notification) => {
  if (!isPendingInvitation(notification) || invitationActionId.value) return
  invitationActionId.value = notification.id
  try {
    await axiosClient.post(`/project-invitations/${notification.relatedInvitationId}/decline`)
    notification.actionState = 'Declined'
    notification.isRead = true
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể từ chối lời mời.')
  } finally {
    invitationActionId.value = null
  }
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
.notifications-main { flex: 1; min-width: 0; max-width: 900px; }
.main-header { display: flex; justify-content: space-between; align-items: center; padding-bottom: 16px; border-bottom: 1px solid #DFE1E6; margin-bottom: 16px; }
.main-header h2 { font-size: 16px; margin: 0; }
.header-actions { display: flex; gap: 24px; align-items: center; }
.text-action-btn { background: transparent; border: 0; color: #0052CC; cursor: pointer; }
.toggle-wrapper { display: flex; gap: 8px; align-items: center; font-size: 13px; color: #5E6C84; }
.time-group-title { font-size: 12px; color: #5E6C84; margin-bottom: 8px; }
.notification-item { width: 100%; min-width: 0; box-sizing: border-box; display: flex; gap: 16px; padding: 16px 0; border: 0; border-bottom: 1px solid #DFE1E6; background: transparent; text-align: left; cursor: pointer; }
.notification-item:hover { background: #FAFBFC; }
.notification-item:focus-visible { outline: 2px solid var(--home-accent, #0052CC); outline-offset: 2px; }
.notif-avatar { width: 32px; height: 32px; border-radius: 50%; background: #172B4D; color: white; display: flex; align-items: center; justify-content: center; font-size: 12px; font-weight: 700; flex-shrink: 0; }
.notif-avatar.read { background: #6B778C; }
.notif-content { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 4px; }
.notif-header { display: flex; flex-wrap: wrap; gap: 4px 8px; font-size: 14px; }
.notif-time { color: #5E6C84; margin-left: 8px; }
.notif-link { color: #172B4D; font-size: 14px; }
.notif-meta { color: #5E6C84; font-size: 12px; }
.notif-status { width: 8px; height: 8px; flex: 0 0 auto; border-radius: 50%; margin-top: 6px; }
.notif-status.unread { background: #0052CC; }
.empty-state { padding: 48px 0; color: #5E6C84; text-align: center; }

.notif-header strong,
.notif-link,
.notif-meta,
.invitation-resolved {
  overflow-wrap: anywhere;
}

.invitation-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

@media (max-width: 640px) {
  .page-header {
    padding: 20px 14px 12px;
  }

  .notifications-layout {
    flex-direction: column;
    gap: 16px;
    padding: 0 14px 28px;
  }

  .filters-sidebar {
    width: 100%;
    flex-direction: row;
    gap: 8px;
  }

  .filter-btn {
    flex: 1 1 0;
    justify-content: center;
    min-width: 0;
    min-height: 44px;
  }

  .notifications-main {
    width: 100%;
    max-width: none;
  }

  .main-header {
    flex-direction: column;
    align-items: stretch;
    gap: 12px;
  }

  .header-actions {
    width: 100%;
    flex-wrap: wrap;
    justify-content: space-between;
    gap: 8px 12px;
  }

  .text-action-btn,
  .toggle-wrapper,
  .invitation-actions button {
    min-height: 44px;
  }

  .toggle-wrapper input {
    width: 20px;
    height: 20px;
  }

  .notification-item {
    gap: 10px;
    padding: 14px 0;
  }

  .notif-time {
    margin-left: 0;
  }
}
</style>
