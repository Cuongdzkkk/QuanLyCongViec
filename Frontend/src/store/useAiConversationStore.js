import { defineStore } from 'pinia'
import axiosClient from '@/api/axiosClient'
import { normalizeAiActionList } from '@/utils/aiActionUi'
import { decorateAiAction } from '@/utils/aiActionEngine'

const defaultMessages = () => [{
  role: 'bot',
  content: 'Xin chào. Tôi sẵn sàng tóm tắt tiến độ, gợi ý ưu tiên, tạo checklist hoặc phân tích nội dung trên trang hiện tại.'
}]

const apiPayload = response => response?.data?.data ?? response?.data ?? response

const canonicalizeAiMessage = message => {
  if (!Array.isArray(message?.actions)) return message
  const normalized = normalizeAiActionList(message.actions)
  const clarification = 'Bạn muốn đặt tên công việc là gì?'
  return {
    ...message,
    content: normalized.hasMissingTaskTitle && !`${message.content || ''}`.includes(clarification)
      ? [message.content || '', clarification].filter(Boolean).join('\n\n')
      : message.content,
    actions: normalized.actions.map(action => decorateAiAction(action))
  }
}

export const useAiConversationStore = defineStore('aiConversation', {
  state: () => ({
    messages: defaultMessages(),
    conversations: [],
    currentConversationId: null,
    currentConversationWorkspaceId: null,
    currentConversationTitle: 'Cuộc trò chuyện mới',
    historyVisible: false,
    search: '',
    loading: false,
    page: 1,
    hasMore: false
  }),
  getters: {
    filteredConversations: state => {
      const query = state.search.trim().toLocaleLowerCase('vi-VN')
      return query
        ? state.conversations.filter(item => item.title.toLocaleLowerCase('vi-VN').includes(query))
        : state.conversations
    }
  },
  actions: {
    defaultMessages,
    async loadConversations({ workspaceId, reset = true } = {}) {
      if (this.loading) return
      this.loading = true
      if (reset) {
        this.page = 1
        this.conversations = []
      }
      try {
        const response = await axiosClient.get('/ai/conversations', {
          params: { workspaceId, page: this.page, pageSize: 20 }
        })
        const payload = apiPayload(response)
        const items = payload?.items || []
        this.conversations = reset ? items : [...this.conversations, ...items]
        this.hasMore = this.conversations.length < (payload?.total || 0)
        if (this.hasMore) this.page += 1
      } finally {
        this.loading = false
      }
    },
    startNewConversation() {
      this.currentConversationId = null
      this.currentConversationWorkspaceId = null
      this.currentConversationTitle = 'Cuộc trò chuyện mới'
      this.messages = defaultMessages()
      this.historyVisible = false
    },
    async ensureConversation({ workspaceId, firstMessage = '' } = {}) {
      if (this.currentConversationId) return this.currentConversationId
      const title = firstMessage.trim().replace(/\s+/g, ' ').slice(0, 80) || 'Cuộc trò chuyện mới'
      const response = await axiosClient.post('/ai/conversations', { workspaceId, title })
      const conversation = apiPayload(response)
      this.currentConversationId = conversation.id
      this.currentConversationWorkspaceId = conversation.workspaceId
      this.currentConversationTitle = conversation.title
      return conversation.id
    },
    async persistConversation() {
      if (!this.currentConversationId) return
      const messages = JSON.parse(JSON.stringify(this.messages.filter(message => !message.loading).map(message => ({
        ...canonicalizeAiMessage(message),
        attachments: message.attachments?.map(attachment => Object.fromEntries(
          Object.entries(attachment).filter(([key]) => !['file', 'previewUrl'].includes(key))
        ))
      }))))
      await axiosClient.put(`/ai/conversations/${this.currentConversationId}`, {
        title: this.currentConversationTitle,
        messages
      })
    },
    async openConversation(id) {
      const response = await axiosClient.get(`/ai/conversations/${id}`)
      const conversation = apiPayload(response)
      this.currentConversationId = conversation.id
      this.currentConversationWorkspaceId = conversation.workspaceId
      this.currentConversationTitle = conversation.title
      this.messages = Array.isArray(conversation.messages) && conversation.messages.length
        ? conversation.messages.map(canonicalizeAiMessage)
        : defaultMessages()
      this.historyVisible = false
    }
  }
})
