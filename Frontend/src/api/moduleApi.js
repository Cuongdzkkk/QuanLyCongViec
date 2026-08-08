import axiosClient from '@/api/axiosClient'

const toPositiveInteger = (value, fallback) => {
  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback
}

const normalizeTaskPage = (tasks, page, pageSize) => {
  const source = tasks && typeof tasks === 'object' ? tasks : {}
  const items = Array.isArray(source.items) ? source.items : []

  return {
    items,
    page: toPositiveInteger(source.page, page),
    pageSize: Math.min(100, toPositiveInteger(source.pageSize, pageSize)),
    totalCount: Math.max(0, Number(source.totalCount) || 0),
    totalPages: Math.max(0, Number(source.totalPages) || 0),
    hasPreviousPage: Boolean(source.hasPreviousPage),
    hasNextPage: Boolean(source.hasNextPage)
  }
}

export async function getModuleDetail(
  projectId,
  moduleId,
  { page = 1, pageSize = 20, signal } = {}
) {
  const normalizedPage = toPositiveInteger(page, 1)
  const normalizedPageSize = Math.min(100, toPositiveInteger(pageSize, 20))
  const response = await axiosClient.get(
    `/projects/${encodeURIComponent(projectId)}/modules/${encodeURIComponent(moduleId)}`,
    {
      params: {
        page: normalizedPage,
        pageSize: normalizedPageSize
      },
      signal
    }
  )
  const envelope = response?.data
  const detail = envelope?.data

  if (!detail || typeof detail !== 'object' || !detail.id) {
    const error = new Error('Invalid module detail response.')
    error.status = Number(envelope?.statusCode || response?.status || 500)
    throw error
  }

  return {
    ...detail,
    tasks: normalizeTaskPage(detail.tasks, normalizedPage, normalizedPageSize)
  }
}
