import axiosClient from '@/api/axiosClient'

const unwrapData = (response) => response?.data?.data ?? response?.data

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
    const response = await axiosClient.post(
      `/channels/${channelId}/messages`,
      { content: payload.content },
      { signal: options.signal }
    )
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
    const response = await axiosClient.post(
      `/direct-conversations/${conversationId}/messages`,
      { content },
      { signal: options.signal }
    )
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
