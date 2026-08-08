<template>
  <el-dropdown trigger="click" popper-class="notifications-dropdown-popper" @visible-change="handleDropdownOpen">
    <div class="nav-icon notification-trigger">
      <el-badge :value="unreadCount" class="notification-badge" :hidden="unreadCount === 0">
        <i class="fa-solid fa-bell"></i>
      </el-badge>
    </div>

    <template #dropdown>
      <div class="jira-notifications-menu">
        <div class="notif-header">
          <h2 class="notif-title">Thông báo</h2>
          <div class="header-actions">
            <span class="unread-toggle-label">Chỉ hiện chưa đọc</span>
            <el-switch v-model="onlyUnread" size="small" />
            <el-button v-if="unreadCount > 0" type="primary" link size="small" @click="markAllAsRead">Đánh dấu đã đọc</el-button>
          </div>
        </div>

        <div class="notif-scroll-area">
          <div v-if="loading" class="notif-empty-state">
            <div class="empty-icon"><i class="fa-solid fa-spinner fa-spin"></i></div>
            <p>Loading notifications...</p>
          </div>

          <div v-else-if="filteredNotifications.length === 0" class="notif-empty-state">
            <div class="empty-icon"><i class="fa-solid fa-flag"></i></div>
            <p>Không có thông báo phù hợp trong 30 ngày gần đây.</p>
          </div>

          <div v-else class="notif-section">
            <div
              v-for="notification in filteredNotifications"
              :key="notification.id"
              class="notif-item-wrapper"
            >
              <button
                type="button"
                class="notif-item"
                :class="{ unread: !notification.isRead }"
                @click="openNotification(notification)"
              >
                <div class="notif-type-icon" :class="getTypeClass(notification.notificationType)">
                  <i :class="getTypeIcon(notification.notificationType)"></i>
                </div>
                <div class="notif-content">
                  <div class="notif-text">
                    <span class="user-name">{{ notification.triggeredByName || 'Hệ thống' }}</span>
                    <span>{{ notification.content }}</span>
                  </div>
                  <div class="notif-context">
                    <span class="notif-title-badge">{{ notification.title }}</span>
                    <span class="time-ago">{{ formatTimeAgo(notification.createdAt) }}</span>
                  </div>
                </div>
                <div v-if="!notification.isRead" class="unread-dot-box" @click.stop="markAsRead(notification)">
                  <div class="unread-dot"></div>
                  <div class="mark-read-hint">Đánh dấu đã đọc</div>
                </div>
              </button>
            </div>
          </div>
        </div>

        <div class="notif-footer">
          <el-button type="primary" link size="small" @click="router.push('/notifications')">Xem tất cả thông báo</el-button>
        </div>
      </div>
    </template>
  </el-dropdown>
</template>

<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'
import * as signalR from '@microsoft/signalr'
import { isExpectedNetworkError } from '@/utils/errorTelemetry'
import { getStoredAccessToken } from '@/utils/authSession'
import { useAuthStore } from '@/store/useAuthStore'
import { collaborationRealtime } from '@/services/collaborationRealtime'

const router = useRouter()
const authStore = useAuthStore()
const notifications = ref([])
const onlyUnread = ref(false)
const loading = ref(false)
const connection = ref(null)
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5136/api'
const collaborationMentionIds = new Set()
let unsubscribeCollaborationMention = null
let unsubscribeCollaborationReconnect = null
let notificationRequestId = 0
let notificationAbortController = null

const unreadCount = computed(() => notifications.value.filter(item => !item.isRead).length)
const filteredNotifications = computed(() => {
  if (onlyUnread.value) return notifications.value.filter(item => !item.isRead)
  return notifications.value
})

const getInitials = (name) => {
  if (!name) return '?'
  return name.split(' ').map(part => part[0]).join('').slice(0, 2).toUpperCase()
}

const formatTimeAgo = (dateStr) => {
  if (!dateStr || dateStr.startsWith('0001-01-01')) return 'Vừa xong'
  const date = new Date(dateStr)
  if (isNaN(date.getTime()) || date.getFullYear() <= 1970) return 'Vừa xong'
  const diffMs = new Date() - date
  if (diffMs < 60000) return 'Vừa xong'
  const diffMins = Math.floor(diffMs / 60000)
  if (diffMins < 60) return `${diffMins} phút trước`
  const diffHours = Math.floor(diffMins / 60)
  if (diffHours < 24) return `${diffHours} giờ trước`
  return `${Math.floor(diffHours / 24)} ngày trước`
}

const getTypeIcon = (type) => {
  switch (type?.toUpperCase()) {
    case 'TASK_ASSIGNED': return 'fa-solid fa-user-plus'
    case 'TASK_STATUS_CHANGED': return 'fa-solid fa-rotate'
    case 'COMMENT_ADDED': return 'fa-solid fa-comment'
    case 'TASK_DUE_SOON': return 'fa-solid fa-clock'
    case 'POINT_AWARDED': return 'fa-solid fa-trophy'
    default: return 'fa-solid fa-bell'
  }
}

const getTypeClass = (type) => {
  switch (type?.toUpperCase()) {
    case 'TASK_ASSIGNED': return 'type-assign'
    case 'TASK_STATUS_CHANGED': return 'type-status'
    case 'COMMENT_ADDED': return 'type-comment'
    case 'TASK_DUE_SOON': return 'type-due'
    case 'POINT_AWARDED': return 'type-reward'
    default: return 'type-general'
  }
}

const normalizeLink = (notification) => {
  if (notification.linkUrl?.startsWith('/chat')) return notification.linkUrl
  if (notification.linkUrl?.startsWith('/space/')) return notification.linkUrl
  if (notification.relatedProjectId && notification.relatedTaskId) return `/space/${notification.relatedProjectId}?task=${notification.relatedTaskId}`
  if (notification.relatedProjectId) return `/space/${notification.relatedProjectId}`
  if (notification.linkUrl?.startsWith('/projects/')) {
    const parts = notification.linkUrl.split('/').filter(Boolean)
    if (parts[1]) return `/space/${parts[1]}`
  }
  return notification.linkUrl || null
}

const fetchNotifications = async () => {
  notificationAbortController?.abort()
  const controller = new AbortController()
  notificationAbortController = controller
  const requestId = ++notificationRequestId
  const requestToken = authStore.token
  loading.value = true
  try {
    const response = await axiosClient.get('/notifications', {
      params: onlyUnread.value ? { unreadOnly: true } : {},
      signal: controller.signal
    })
    if (requestId !== notificationRequestId || authStore.token !== requestToken) return
    notifications.value = (response.data?.data || []).map(item => ({
      ...item,
      linkUrl: normalizeLink(item)
    }))
  } catch (error) {
    if (error?.code !== 'ERR_CANCELED') ElMessage.error('Could not load notifications')
  } finally {
    if (requestId === notificationRequestId) {
      loading.value = false
      notificationAbortController = null
    }
  }
}

const markAsRead = async (notification) => {
  if (notification.isRead) return
  try {
    notification.isRead = true
    await axiosClient.put(`/notifications/${notification.id}/read`)
  } catch (error) {
    notification.isRead = false
    ElMessage.error('Could not update notification')
  }
}

const openNotification = async (notification) => {
  try {
    if (!notification.isRead) {
      await markAsRead(notification)
    }
    if (notification.linkUrl) router.push(notification.linkUrl)
  } catch (error) {
    // Error handled in markAsRead
  }
}

const markAllAsRead = async () => {
  try {
    await axiosClient.put('/notifications/read-all')
    notifications.value = notifications.value.map(item => ({ ...item, isRead: true }))
  } catch (error) {
    ElMessage.error('Could not mark all notifications as read')
  }
}

const handleDropdownOpen = (visible) => {
  if (visible) fetchNotifications()
}

const initSignalR = () => {
    const token = getStoredAccessToken() || localStorage.getItem('token')
  if (!token) return

  const hubUrl = new URL(apiBaseUrl, window.location.origin)
  hubUrl.pathname = '/notification-hub'
  hubUrl.search = ''
  hubUrl.hash = ''

  connection.value = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl.toString(), {
        accessTokenFactory: () => getStoredAccessToken() || token
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.None)
    .build()

  connection.value.on('ReceiveNotification', (notification) => {
    if (notifications.value.some(item => item.id === notification.id)) return
    notifications.value.unshift({
      ...notification,
      isRead: false,
      linkUrl: normalizeLink(notification)
    })
  })

  connection.value.start().catch((error) => {
    if (!isExpectedNetworkError(error)) {
      console.error('Notification hub connection failed:', error)
    }
  })
}

watch(onlyUnread, () => {
  fetchNotifications()
})

watch(() => authStore.token, (token, previousToken) => {
  if (token === previousToken) return
  notificationAbortController?.abort()
  notificationRequestId += 1
  notifications.value = []
  if (connection.value) {
    void connection.value.stop()
    connection.value = null
  }
  if (token) {
    void fetchNotifications()
    initSignalR()
  }
})

const handleMentionRefresh = () => { void fetchNotifications() }
const handleNotificationReset = () => {
  notificationAbortController?.abort()
  notificationRequestId += 1
  notifications.value = []
}
const handlePrivateMention = (notification) => {
  if (!notification?.notificationId || collaborationMentionIds.has(notification.notificationId)) return
  collaborationMentionIds.add(notification.notificationId)
  void fetchNotifications()
}

onMounted(() => {
  window.addEventListener('collaboration-mention-created', handleMentionRefresh)
  window.addEventListener('collaboration-notifications-refresh', handleMentionRefresh)
  window.addEventListener('collaboration-notifications-reset', handleNotificationReset)
  fetchNotifications()
  initSignalR()
  unsubscribeCollaborationMention = collaborationRealtime.subscribeMention(handlePrivateMention)
  unsubscribeCollaborationReconnect = collaborationRealtime.subscribeReconnected(() => {
    void fetchNotifications()
  })
  void collaborationRealtime.start().catch(() => {
    // REST remains authoritative and refreshes whenever the dropdown opens.
  })
})

onUnmounted(() => {
  window.removeEventListener('collaboration-mention-created', handleMentionRefresh)
  window.removeEventListener('collaboration-notifications-refresh', handleMentionRefresh)
  window.removeEventListener('collaboration-notifications-reset', handleNotificationReset)
  unsubscribeCollaborationMention?.()
  unsubscribeCollaborationReconnect?.()
  collaborationMentionIds.clear()
  notificationAbortController?.abort()
  notificationRequestId += 1
  if (connection.value) connection.value.stop()
  void collaborationRealtime.stop()
})
</script>

<style scoped>
.jira-notifications-menu {
  width: 480px;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  background: var(--color-surface);
  color: var(--color-text-primary);
}

.notif-header,
.header-actions,
.notif-context {
  display: flex;
  align-items: center;
}

.notif-header {
  justify-content: space-between;
  gap: 12px;
  padding: 16px 20px 12px;
}

.header-actions {
  gap: 12px;
}

.notif-title {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
}

.unread-toggle-label {
  color: var(--color-text-secondary);
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
}

.notif-scroll-area {
  min-height: 200px;
  max-height: 480px;
  overflow-y: auto;
}

.notif-section {
  padding: 0;
}

.notif-item-wrapper {
  border-bottom: 1px solid var(--color-border);
}

.notif-item-wrapper:last-child {
  border-bottom: none;
}

.notif-item {
  width: 100%;
  display: flex;
  gap: 14px;
  padding: 14px 20px;
  border: none;
  background: transparent;
  color: inherit;
  text-align: left;
  cursor: pointer;
  transition: all 0.2s ease;
  position: relative;
}

.notif-item:hover {
  background: var(--color-surface-hover);
}

.notif-item.unread {
  background: rgba(var(--color-primary-rgb, 37, 99, 235), 0.04);
}

.notif-item.unread:hover {
  background: rgba(var(--color-primary-rgb, 37, 99, 235), 0.08);
}

.notif-type-icon {
  width: 36px;
  height: 36px;
  min-width: 36px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 8px;
  font-size: 16px;
}

.type-assign { background: rgba(37, 99, 235, 0.1); color: #2563eb; }
.type-status { background: rgba(147, 51, 234, 0.1); color: #9333ea; }
.type-comment { background: rgba(16, 185, 129, 0.1); color: #10b981; }
.type-due { background: rgba(245, 158, 11, 0.1); color: #f59e0b; }
.type-reward { background: rgba(236, 72, 153, 0.1); color: #ec4899; }
.type-general { background: var(--bg-tertiary); color: var(--color-text-secondary); }

.notif-content {
  flex: 1;
  min-width: 0;
}

.notif-text {
  font-size: 13.5px;
  line-height: 1.4;
  color: var(--color-text-primary);
  margin-bottom: 4px;
  display: block;
}

.user-name {
  font-weight: 700;
  margin-right: 4px;
}

.notif-context {
  display: flex;
  align-items: center;
  gap: 10px;
}

.notif-title-badge {
  font-size: 11px;
  font-weight: 600;
  color: var(--color-text-muted);
  background: var(--bg-tertiary);
  padding: 1px 6px;
  border-radius: 4px;
}

.time-ago {
  color: var(--color-text-muted);
  font-size: 11px;
}

.unread-dot-box {
  width: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
}

.unread-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #3b82f6;
  transition: transform 0.2s;
}

.unread-dot-box:hover .unread-dot {
  transform: scale(1.4);
}

.mark-read-hint {
  position: absolute;
  right: 30px;
  background: #1e293b;
  color: white;
  font-size: 10px;
  padding: 4px 8px;
  border-radius: 4px;
  white-space: nowrap;
  opacity: 0;
  pointer-events: none;
  transition: opacity 0.2s;
}

.unread-dot-box:hover .mark-read-hint {
  opacity: 1;
}

.notif-footer {
  padding: 12px;
  text-align: center;
  border-top: 1px solid var(--color-border);
}

.notif-empty-state {
  min-height: 280px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  text-align: center;
  color: var(--color-text-secondary);
}

.empty-icon {
  font-size: 30px;
  color: var(--color-text-muted);
}

.nav-icon {
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  color: var(--color-text-secondary);
  cursor: pointer;
}

.nav-icon:hover {
  background: var(--color-surface-hover);
}

.notification-badge :deep(.el-badge__content) {
  background: #f87171;
  border: none;
  font-size: 9px;
  height: 14px;
  line-height: 14px;
}
</style>

<style>
.el-popper.notifications-dropdown-popper {
  padding: 0 !important;
  border: 1px solid var(--color-border) !important;
  border-radius: 8px !important;
  background: var(--color-surface) !important;
  box-shadow: 0 12px 32px rgba(0, 0, 0, 0.22) !important;
}
</style>
