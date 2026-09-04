import axiosClient from '@/api/axiosClient'

const unwrapData = (response) => response?.data?.data ?? response?.data

const messageForm = (content, files, mentions = [], replyToMessageId = null) => {
  const form = new FormData()
  if (content) form.append('content', content)
  if (replyToMessageId) form.append('replyToMessageId', replyToMessageId)
  files.forEach(file => form.append('files', file))
  mentions.forEach((mention, index) => {
    form.append(`mentions[${index}].userId`, mention.userId)
    form.append(`mentions[${index}].startIndex`, `${mention.startIndex}`)
    form.append(`mentions[${index}].length`, `${mention.length}`)
  })
  return form
}

export const collaborationApi = {
  async getProjectChannels(projectId, options = {}) {
    const response = await axiosClient.get(`/projects/${projectId}/channels`, {
      params: {
        page: options.page ?? 1,
        pageSize: options.pageSize ?? 50
      },
      signal: options.signal
    })
    return unwrapData(response)
  },

  async createProjectChannel(projectId, payload, options = {}) {
    const response = await axiosClient.post(
      `/projects/${projectId}/channels`,
      payload,
      {
        headers: {
          'Idempotency-Key': options.idempotencyKey
        },
        signal: options.signal
      }
    )
    return unwrapData(response)
  },

  async getChannelMessages(channelId, options = {}) {
    const response = await axiosClient.get(`/channels/${channelId}/messages`, {
      params: {
        page: options.page ?? 1,
        pageSize: options.pageSize ?? 50
      },
      signal: options.signal
    })
    return unwrapData(response)
  },

  async sendChannelMessage(channelId, payload, options = {}) {
    const files = Array.isArray(payload.files) ? payload.files : []
    const mentions = Array.isArray(payload.mentions) ? payload.mentions : []
    const response = await axiosClient.post(
      `/channels/${channelId}/messages`,
      files.length
        ? messageForm(payload.content, files, mentions, payload.replyToMessageId)
        : { content: payload.content, mentions, replyToMessageId: payload.replyToMessageId || null },
      {
        signal: options.signal,
        timeout: 60000,
        headers: files.length ? { 'Content-Type': 'multipart/form-data' } : undefined,
        onUploadProgress: options.onUploadProgress
      }
    )
    return unwrapData(response)
  },

  async searchChannelMessages(channelId, query, options = {}) {
    const response = await axiosClient.get(`/channels/${channelId}/messages/search`, {
      params: {
        query,
        page: options.page ?? 1,
        pageSize: options.pageSize ?? 50
      },
      signal: options.signal
    })
    return unwrapData(response)
  },

  async analyzeChannelWithAi(channelId, payload, options = {}) {
    const response = await axiosClient.post(
      `/channels/${channelId}/ai/analysis`,
      payload,
      { signal: options.signal }
    )
    return unwrapData(response)
  },

  async getCallTranscript(projectId, voiceChannelId, callSessionId, options = {}) {
    const response = await axiosClient.get(
      `/projects/${projectId}/voice-channels/${encodeURIComponent(voiceChannelId)}/calls/${callSessionId}/transcript`,
      { signal: options.signal }
    )
    return unwrapData(response)
  },

  async getMeetingAiReport(projectId, voiceChannelId, callSessionId, options = {}) {
    const response = await axiosClient.get(
      `/projects/${projectId}/voice-channels/${encodeURIComponent(voiceChannelId)}/calls/${callSessionId}/transcript/ai-report`,
      { signal: options.signal }
    )
    return unwrapData(response)
  },

  async getMeetingCapabilities(projectId, voiceChannelId, options = {}) {
    const response = await axiosClient.get(
      `/projects/${projectId}/voice-channels/${encodeURIComponent(voiceChannelId)}/calls/capabilities`,
      { signal: options.signal }
    )
    return unwrapData(response)
  },

  async addChannelReaction(channelId, messageId, emoji, options = {}) {
    const response = await axiosClient.post(
      `/channels/${channelId}/messages/${messageId}/reactions`,
      { emoji },
      { signal: options.signal }
    )
    return unwrapData(response)
  },

  async removeChannelReaction(channelId, messageId, emoji, options = {}) {
    const response = await axiosClient.delete(
      `/channels/${channelId}/messages/${messageId}/reactions`,
      { params: { emoji }, signal: options.signal }
    )
    return unwrapData(response)
  },

  async getChannelPins(channelId, options = {}) {
    const response = await axiosClient.get(`/channels/${channelId}/pins`, {
      signal: options.signal
    })
    return unwrapData(response)
  },

  async pinChannelMessage(channelId, messageId, options = {}) {
    const response = await axiosClient.post(
      `/channels/${channelId}/messages/${messageId}/pin`,
      null,
      { signal: options.signal }
    )
    return unwrapData(response)
  },

  async unpinChannelMessage(channelId, messageId, options = {}) {
    const response = await axiosClient.delete(
      `/channels/${channelId}/messages/${messageId}/pin`,
      { signal: options.signal }
    )
    return unwrapData(response)
  },

  async searchChannelMembers(channelId, query, options = {}) {
    const response = await axiosClient.get(`/channels/${channelId}/members`, {
      params: { query, limit: options.limit ?? 10 },
      signal: options.signal
    })
    return unwrapData(response)
  },

  async markChannelRead(channelId, messageId, options = {}) {
    const response = await axiosClient.post(
      `/channels/${channelId}/read`,
      { messageId },
      { signal: options.signal }
    )
    return unwrapData(response)
  },

  async getDirectMessageUsers(projectId, options = {}) {
    const response = await axiosClient.get('/users', {
      params: {
        projectId,
        page: options.page ?? 1,
        pageSize: options.pageSize ?? 100
      },
      signal: options.signal
    })
    return {
      items: response?.data?.data ?? [],
      page: response?.data?.page ?? options.page ?? 1,
      pageSize: response?.data?.pageSize ?? options.pageSize ?? 100,
      totalCount: response?.data?.total ?? 0
    }
  },

  async findOrCreateDirectConversation(participantUserId, options = {}) {
    const response = await axiosClient.post(
      '/direct-conversations',
      { participantUserId },
      { signal: options.signal }
    )
    return unwrapData(response)
  },

  async getDirectConversations(options = {}) {
    const response = await axiosClient.get('/direct-conversations', {
      params: {
        page: options.page ?? 1,
        pageSize: options.pageSize ?? 50
      },
      signal: options.signal
    })
    return unwrapData(response)
  },

  async getDirectMessages(conversationId, options = {}) {
    const response = await axiosClient.get(
      `/direct-conversations/${conversationId}/messages`,
      {
        params: {
          page: options.page ?? 1,
          pageSize: options.pageSize ?? 50
        },
        signal: options.signal
      }
    )
    return unwrapData(response)
  },

  async sendDirectMessage(conversationId, content, options = {}) {
    const files = Array.isArray(options.files) ? options.files : []
    const response = await axiosClient.post(
      `/direct-conversations/${conversationId}/messages`,
      files.length ? messageForm(content, files) : { content },
      {
        signal: options.signal,
        timeout: 60000,
        headers: files.length ? { 'Content-Type': 'multipart/form-data' } : undefined,
        onUploadProgress: options.onUploadProgress
      }
    )
    return unwrapData(response)
  },

  async downloadAttachment(attachmentId, options = {}) {
    const response = await axiosClient.get(
      `/collaboration-attachments/${attachmentId}/content`,
      { responseType: 'blob', signal: options.signal }
    )
    return response.data
  },

  async editChannelMessage(channelId, messageId, content, options = {}) {
    try {
      const response = await axiosClient.put(`/channels/${channelId}/messages/${messageId}`, { content }, { signal: options.signal })
      return unwrapData(response)
    } catch {
      const response = await axiosClient.patch(`/channels/${channelId}/messages/${messageId}`, { content }, { signal: options.signal })
      return unwrapData(response)
    }
  },

  async deleteChannelMessage(channelId, messageId, options = {}) {
    const response = await axiosClient.delete(`/channels/${channelId}/messages/${messageId}`, { signal: options.signal })
    return unwrapData(response)
  },

  async editDirectMessage(conversationId, messageId, content, options = {}) {
    try {
      const response = await axiosClient.put(`/direct-conversations/${conversationId}/messages/${messageId}`, { content }, { signal: options.signal })
      return unwrapData(response)
    } catch {
      const response = await axiosClient.patch(`/direct-conversations/${conversationId}/messages/${messageId}`, { content }, { signal: options.signal })
      return unwrapData(response)
    }
  },

  async deleteDirectMessage(conversationId, messageId, options = {}) {
    const response = await axiosClient.delete(`/direct-conversations/${conversationId}/messages/${messageId}`, { signal: options.signal })
    return unwrapData(response)
  },

  async markDirectConversationRead(conversationId, messageId, options = {}) {
    const response = await axiosClient.post(
      `/direct-conversations/${conversationId}/read`,
      { messageId },
      { signal: options.signal }
    )
    return unwrapData(response)
  }
}
