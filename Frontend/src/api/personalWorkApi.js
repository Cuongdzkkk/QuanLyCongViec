import axiosClient from '@/api/axiosClient'

export const PERSONAL_WORK_SCOPES = Object.freeze({
  suggested: 'suggested',
  assigned: 'assigned',
  created: 'created',
  following: 'following',
  worked: 'worked'
})

const normalizePage = (response) => {
  const data = response?.data?.data || {}
  return {
    totalCount: Number(data.totalCount) || 0,
    page: Math.max(1, Number(data.page) || 1),
    pageSize: Math.max(1, Number(data.pageSize) || 1),
    items: Array.isArray(data.items) ? data.items : []
  }
}

const normalizeSummary = (response) => {
  const data = response?.data?.data || {}
  return {
    assigned: Number(data.assigned) || 0,
    created: Number(data.created) || 0,
    following: Number(data.following) || 0,
    workedOn: Number(data.workedOn) || 0,
    suggested: Number(data.suggested) || 0,
    overdue: Number(data.overdue) || 0,
    completed: Number(data.completed) || 0
  }
}

const normalizeActivity = (response) => {
  const data = response?.data?.data || {}
  return {
    total: Number(data.total) || 0,
    items: Array.isArray(data.items) ? data.items : []
  }
}

const sameId = (left, right) => `${left || ''}` === `${right || ''}`

export const filterItemsByProjectIds = (items = [], projectIds = []) => {
  if (!Array.isArray(projectIds) || projectIds.length === 0) return items
  return (items || []).filter(item => projectIds.some(projectId => sameId(item.projectId || item.ProjectId, projectId)))
}

export const filterActivitiesByProjectIds = (items = [], projectIds = []) => {
  if (!Array.isArray(projectIds) || projectIds.length === 0) return items
  return (items || []).filter(item => {
    const entityType = `${item.entityType || item.EntityType || item.resource || ''}`.trim().toLowerCase()
    const entityId = item.entityId || item.EntityId || item.targetId || item.TargetId
    const projectId = item.projectId || item.ProjectId
    return projectIds.some(id => sameId(projectId, id) || (entityType === 'project' && sameId(entityId, id)))
  })
}

export const personalWorkApi = {
  async getPage({ scope, page = 1, pageSize = 10, workspaceId, signal } = {}) {
    if (!Object.values(PERSONAL_WORK_SCOPES).includes(scope)) {
      throw new TypeError(`Unsupported personal work scope: ${scope}`)
    }

    const response = await axiosClient.get('/tasks/personal-work', {
      params: { scope, page, pageSize, workspaceId },
      signal
    })
    return normalizePage(response)
  },

  async getSummary({ workspaceId, signal } = {}) {
    const response = await axiosClient.get('/tasks/personal-summary', {
      params: { workspaceId },
      signal
    })
    return normalizeSummary(response)
  },

  async getActivity({ timeFilter = '30d', search, limit = 50, workspaceId, signal } = {}) {
    const response = await axiosClient.get('/site-auditlogs', {
      params: { timeFilter, search, limit, workspaceId },
      signal
    })
    return normalizeActivity(response)
  }
}
