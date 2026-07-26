import axiosClient from '@/api/axiosClient'

export const STARRED_ENTITY_TYPES = Object.freeze({
  PROJECT: 'Project',
  WORK_TASK: 'WorkTask'
})

const normalizeCollection = (response, page, pageSize) => {
  const payload = response?.data
  const items = Array.isArray(payload?.data)
    ? payload.data
    : Array.isArray(payload)
      ? payload
      : []
  const pagination = payload?.pagination || {}

  return {
    items,
    pagination: {
      totalCount: Number(pagination.totalCount ?? items.length),
      page: Number(pagination.page ?? page),
      pageSize: Number(pagination.pageSize ?? pageSize)
    }
  }
}

export async function getStarredItems({ workspaceId, page = 1, pageSize = 50, signal }) {
  const response = await axiosClient.get(`/workspaces/${workspaceId}/starreditems`, {
    params: { page, pageSize },
    signal
  })
  return normalizeCollection(response, page, pageSize)
}

export async function starItem({ workspaceId, itemType, itemId }) {
  const response = await axiosClient.post(`/workspaces/${workspaceId}/starreditems`, {
    itemType,
    itemId
  })
  return response.data?.data || response.data || null
}

export async function unstarItem({ workspaceId, itemType, itemId }) {
  const response = await axiosClient.delete(
    `/workspaces/${workspaceId}/starreditems/${encodeURIComponent(itemType)}/${encodeURIComponent(itemId)}`
  )
  return response.data?.data || response.data || null
}

export async function getRecentViews({ page = 1, pageSize = 50, signal }) {
  const response = await axiosClient.get('/recentviews', {
    params: { page, pageSize },
    signal
  })
  return normalizeCollection(response, page, pageSize)
}

export async function recordRecentView({ entityType, entityId }) {
  const response = await axiosClient.post('/recentviews', {
    entityType,
    entityId
  })
  return response.data?.data || response.data || null
}
