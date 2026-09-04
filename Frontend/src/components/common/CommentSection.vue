<template>
  <div class="comment-section">
    <div class="comment-section-header" @click="isExpanded = !isExpanded" style="cursor: pointer; display: flex; align-items: center; gap: 8px; margin-bottom: 16px;">
      <h3 style="margin: 0; font-size: 16px; color: #172B4D;">Bình luận</h3>
      <span class="badge-count">{{ comments.length }}</span>
      <i class="fa-solid" :class="isExpanded ? 'fa-chevron-up' : 'fa-chevron-down'" style="color: #6B778C; font-size: 12px;"></i>
    </div>
    
    <div v-show="isExpanded">
      <div class="comment-input-area">
        <UserAvatar :user="currentUser" :size="32" :fontSize="12" />
        <div class="input-wrapper">
          <textarea 
            v-model="newComment" 
            placeholder="Thêm bình luận..." 
            rows="2"
            :disabled="isSubmitting"
          ></textarea>
          <div class="comment-actions">
            <i class="fa-regular fa-face-smile emoji-icon-placeholder" title="Thêm biểu tượng cảm xúc"></i>
            <div class="action-buttons">
              <button class="cancel-btn" @click="newComment = ''" v-if="newComment.trim()">Hủy</button>
              <button class="primary-btn" @click="submitComment" :disabled="isSubmitting || !newComment.trim()">
                {{ isSubmitting ? 'Đang gửi...' : 'Gửi' }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <div class="comments-list">
        <div v-for="comment in sortedComments" :key="comment.id" class="comment-item">
          <UserAvatar :user="{ id: comment.userId || comment.user?.id, fullName: comment.fullName || comment.user?.name, email: comment.email || comment.user?.email, avatarColor: comment.avatarColor || comment.user?.avatarColor }" :size="32" :fontSize="12" />
          <div class="comment-body">
            <div class="comment-header">
              <span class="comment-author">{{ comment.fullName }}</span>
              <span class="comment-time">{{ formatDate(comment.createdAt) }}</span>
            </div>
            <div class="comment-content" v-html="sanitizeHtml(comment.content)"></div>
            <div class="comment-footer-actions">
              <span class="action-link">Thích</span>
              <span class="action-link">Phản hồi</span>
              <div class="reaction-buttons">
                <button class="reaction-btn">👍</button>
                <button class="reaction-btn">❤️</button>
                <button class="reaction-btn">🎉</button>
              </div>
            </div>
          </div>
        </div>
        
        <div v-if="comments.length === 0 && !isLoading" class="empty-comments">
          Chưa có bình luận nào.
        </div>
        <div v-if="isLoading" class="loading-state">
          <div class="loader-spinner"></div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import axiosClient from '@/api/axiosClient'
import { getStoredUser } from '@/utils/permissions'
import UserAvatar from '@/components/common/UserAvatar.vue'
import { getInitials } from '@/utils/avatarHelper'
import { usePeopleStore } from '@/store/usePeopleStore'
import DOMPurify from 'dompurify'

const peopleStore = usePeopleStore()

const props = defineProps({
  entityId: { type: String, required: true },
  entityType: { type: String, required: true }
})

const currentUser = computed(() => {
  const stored = getStoredUser() || {}
  const u = peopleStore.users.find(user => user.id === stored.id) || peopleStore.currentUser || stored
  return {
    id: u.id || stored.id,
    fullName: u.fullName || u.name || u.publicName,
    email: u.email || stored.email,
    initials: u.initials || getInitials(u.fullName || u.name, u.email),
    avatarColor: u.avatarColor || stored.avatarColor,
    avatarUrl: u.avatarUrl || stored.avatarUrl
  }
})

const isExpanded = ref(true)
const comments = ref([])
const newComment = ref('')
const isLoading = ref(false)
const isSubmitting = ref(false)

const sortedComments = computed(() => {
  return [...comments.value].sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt))
})

const sanitizeHtml = (value) => DOMPurify.sanitize(value || '')

const loadComments = async () => {
  isLoading.value = true
  try {
    const res = await axiosClient.get(`/comments/${props.entityType}/${props.entityId}`)
    comments.value = res.data?.data || []
  } catch (error) {
    console.error('Failed to load comments', error)
  } finally {
    isLoading.value = false
  }
}

const submitComment = async () => {
  if (!newComment.value.trim()) return
  
  isSubmitting.value = true
  try {
    const formData = new FormData()
    formData.append('content', newComment.value)
    
    const res = await axiosClient.post(`/comments/${props.entityType}/${props.entityId}`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    if (res.data?.data) {
      const newCmt = res.data.data;
      if (!newCmt.userId && !newCmt.email && !newCmt.avatarColor) {
        newCmt.userId = currentUser.value.id;
        newCmt.email = currentUser.value.email;
        newCmt.avatarColor = currentUser.value.avatarColor;
        newCmt.fullName = newCmt.fullName || currentUser.value.fullName;
      }
      comments.value.unshift(newCmt) // Thêm bình luận mới vào danh sách
      newComment.value = ''
    }
  } catch (error) {
    console.error('Failed to submit comment', error)
  } finally {
    isSubmitting.value = false
  }
}

const formatDate = (dateString) => {
  if (!dateString) return ''
  const date = new Date(dateString)
  return new Intl.DateTimeFormat('vi-VN', {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit', second: '2-digit'
  }).format(date)
}

onMounted(() => {
  loadComments()
})
</script>

<style scoped>
.comment-section {
  margin-top: 24px;
}

.badge-count {
  background: #EBECF0;
  color: #172B4D;
  font-size: 12px;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 12px;
}

.comment-input-area {
  display: flex;
  gap: 12px;
  margin-bottom: 24px;
}

.input-wrapper {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

textarea {
  width: 100%;
  padding: 12px;
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  resize: vertical;
  font-family: inherit;
  font-size: 14px;
  transition: border-color 0.2s, box-shadow 0.2s;
}

textarea:focus {
  outline: none;
  border-color: #4C9AFF;
  box-shadow: 0 0 0 1px #4C9AFF;
}

.comment-actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 4px;
}

.emoji-icon-placeholder {
  color: #6B778C;
  font-size: 16px;
  cursor: pointer;
  padding: 4px;
}

.emoji-icon-placeholder:hover {
  color: #172B4D;
}

.action-buttons {
  display: flex;
  gap: 8px;
}

.cancel-btn {
  background: transparent;
  color: #5E6C84;
  border: none;
  padding: 6px 12px;
  border-radius: 3px;
  font-weight: 500;
  cursor: pointer;
}

.cancel-btn:hover {
  background: rgba(9, 30, 66, 0.08);
}

.primary-btn {
  background-color: #0052CC;
  color: white;
  border: none;
  padding: 6px 16px;
  border-radius: 3px;
  font-weight: 500;
  cursor: pointer;
}

.primary-btn:hover:not(:disabled) {
  background-color: #0047B3;
}

.primary-btn:disabled {
  background-color: #091E420A;
  color: #A5ADBA;
  cursor: not-allowed;
}

.comments-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.comment-item {
  display: flex;
  gap: 12px;
}

.comment-avatar {
  width: 32px;
  height: 32px;
  color: white;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  font-weight: 600;
  flex-shrink: 0;
}

.comment-body {
  flex: 1;
}

.comment-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}

.comment-author {
  font-weight: 600;
  color: #172B4D;
  font-size: 14px;
}

.comment-time {
  color: #5E6C84;
  font-size: 12px;
}

.comment-content {
  color: #172B4D;
  font-size: 14px;
  line-height: 1.5;
  background: #FFFFFF;
  padding: 12px;
  border-radius: 3px;
  border: 1px solid #DFE1E6;
  margin-bottom: 8px;
}

.comment-footer-actions {
  display: flex;
  align-items: center;
  gap: 16px;
  font-size: 12px;
}

.action-link {
  color: #5E6C84;
  font-weight: 600;
  cursor: pointer;
}

.action-link:hover {
  color: #172B4D;
  text-decoration: underline;
}

.reaction-buttons {
  display: flex;
  gap: 4px;
}

.reaction-btn {
  background: transparent;
  border: 1px solid transparent;
  padding: 2px 6px;
  border-radius: 12px;
  cursor: pointer;
  font-size: 12px;
  transition: background 0.1s;
}

.reaction-btn:hover {
  background: #EBECF0;
}

.empty-comments {
  color: #5E6C84;
  font-size: 14px;
}

.loading-state {
  display: flex;
  justify-content: center;
  padding: 16px;
}

.loader-spinner {
  width: 24px;
  height: 24px;
  border: 2px solid #DFE1E6;
  border-top-color: #0052CC;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>
