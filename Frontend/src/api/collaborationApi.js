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
  }
}
