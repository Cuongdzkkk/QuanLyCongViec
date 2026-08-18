import axiosClient from '@/api/axiosClient'

const unwrapData = (response) => response?.data?.data ?? response?.data

export const sprintApi = {
  async getSprints(projectId, options = {}) {
    const response = await axiosClient.get(`/projects/${projectId}/sprints`, {
      signal: options.signal
    })
    const data = unwrapData(response)
    return Array.isArray(data) ? data : []
  },

  async getCurrentSprint(projectId, options = {}) {
    const sprints = await this.getSprints(projectId, options)
    return sprints.find(sprint => sprint.state === 'Active') || null
  },

  async getSprint(projectId, sprintId, options = {}) {
    const response = await axiosClient.get(`/projects/${projectId}/sprints/${sprintId}`, {
      signal: options.signal
    })
    return unwrapData(response)
  },

  async createSprint(projectId, payload, options = {}) {
    const response = await axiosClient.post(
      `/projects/${projectId}/sprints`,
      payload,
      { signal: options.signal }
    )
    return unwrapData(response)
  },

  async toggleFavorite(projectId, sprintId, options = {}) {
    const response = await axiosClient.patch(
      `/projects/${projectId}/sprints/${sprintId}/favorite`,
      undefined,
      { signal: options.signal }
    )
    return unwrapData(response)
  },

  async startSprint(projectId, sprintId, options = {}) {
    const response = await axiosClient.post(
      `/projects/${projectId}/sprints/${sprintId}/start`,
      undefined,
      { signal: options.signal }
    )
    return unwrapData(response)
  },

  async closeSprint(projectId, sprintId, targetSprintId = null, options = {}) {
    const response = await axiosClient.post(
      `/projects/${projectId}/sprints/${sprintId}/close`,
      { targetSprintId },
      { signal: options.signal }
    )
    return unwrapData(response)
  }
}
