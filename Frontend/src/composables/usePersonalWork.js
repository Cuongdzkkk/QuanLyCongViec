import { ref } from 'vue'
import { filterActivitiesByProjectIds, filterItemsByProjectIds, personalWorkApi, PERSONAL_WORK_SCOPES } from '@/api/personalWorkApi'

const isCanceled = (error) => error?.name === 'CanceledError' || error?.code === 'ERR_CANCELED'

export const usePersonalWork = () => {
  const items = ref([])
  const totalCount = ref(0)
  const page = ref(1)
  const pageSize = ref(10)
  const loading = ref(false)
  const error = ref(null)

  const summary = ref(null)
  const summaryLoading = ref(false)
  const summaryError = ref(null)

  const activities = ref([])
  const activityTotal = ref(0)
  const activityLoading = ref(false)
  const activityError = ref(null)

  let listRequestId = 0
  let summaryRequestId = 0
  let activityRequestId = 0
  let listController
  let summaryController
  let activityController

  const fetchPage = async ({ scope, page: nextPage = 1, pageSize: nextPageSize = 10, workspaceId, projectIds = [] } = {}) => {
    listController?.abort()
    listController = new AbortController()
    const requestId = ++listRequestId

    items.value = []
    totalCount.value = 0
    error.value = null
    loading.value = true

    try {
      const result = await personalWorkApi.getPage({
        scope,
        page: nextPage,
        pageSize: nextPageSize,
        workspaceId,
        signal: listController.signal
      })
      if (requestId !== listRequestId) return null

      const scopedItems = filterItemsByProjectIds(result.items, projectIds)
      items.value = scopedItems
      totalCount.value = projectIds.length ? scopedItems.length : result.totalCount
      page.value = result.page
      pageSize.value = result.pageSize
      return result
    } catch (requestError) {
      if (requestId !== listRequestId || isCanceled(requestError)) return null
      error.value = requestError
      throw requestError
    } finally {
      if (requestId === listRequestId) loading.value = false
    }
  }

  const fetchSummary = async ({ workspaceId, projectIds = [] } = {}) => {
    summaryController?.abort()
    summaryController = new AbortController()
    const requestId = ++summaryRequestId

    summary.value = null
    summaryError.value = null
    summaryLoading.value = true

    try {
      let result
      if (projectIds.length) {
        const [assigned, created, following, workedOn, suggested] = await Promise.all([
          personalWorkApi.getPage({ scope: PERSONAL_WORK_SCOPES.assigned, page: 1, pageSize: 100, workspaceId, signal: summaryController.signal }),
          personalWorkApi.getPage({ scope: PERSONAL_WORK_SCOPES.created, page: 1, pageSize: 100, workspaceId, signal: summaryController.signal }),
          personalWorkApi.getPage({ scope: PERSONAL_WORK_SCOPES.following, page: 1, pageSize: 100, workspaceId, signal: summaryController.signal }),
          personalWorkApi.getPage({ scope: PERSONAL_WORK_SCOPES.worked, page: 1, pageSize: 100, workspaceId, signal: summaryController.signal }),
          personalWorkApi.getPage({ scope: PERSONAL_WORK_SCOPES.suggested, page: 1, pageSize: 100, workspaceId, signal: summaryController.signal })
        ])
        const assignedItems = filterItemsByProjectIds(assigned.items, projectIds)
        const createdItems = filterItemsByProjectIds(created.items, projectIds)
        const followingItems = filterItemsByProjectIds(following.items, projectIds)
        const workedItems = filterItemsByProjectIds(workedOn.items, projectIds)
        const suggestedItems = filterItemsByProjectIds(suggested.items, projectIds)
        const now = Date.now()
        const isDone = (status) => ['DONE', 'COMPLETED', 'FINISHED'].includes(String(status || '').trim().toUpperCase())
        result = {
          assigned: assignedItems.length,
          created: createdItems.length,
          following: followingItems.length,
          workedOn: workedItems.length,
          suggested: suggestedItems.length,
          overdue: assignedItems.filter(task => task.dueDate && new Date(task.dueDate).getTime() < now && !isDone(task.statusName)).length,
          completed: assignedItems.filter(task => isDone(task.statusName)).length
        }
      } else if (workspaceId) {
        result = {
          assigned: 0,
          created: 0,
          following: 0,
          workedOn: 0,
          suggested: 0,
          overdue: 0,
          completed: 0
        }
      } else {
        result = await personalWorkApi.getSummary({ workspaceId, signal: summaryController.signal })
      }
      if (requestId !== summaryRequestId) return null
      summary.value = result
      return result
    } catch (requestError) {
      if (requestId !== summaryRequestId || isCanceled(requestError)) return null
      summaryError.value = requestError
      throw requestError
    } finally {
      if (requestId === summaryRequestId) summaryLoading.value = false
    }
  }

  const fetchActivity = async (options = {}) => {
    activityController?.abort()
    activityController = new AbortController()
    const requestId = ++activityRequestId
    const projectIds = Array.isArray(options.projectIds) ? options.projectIds : []

    activities.value = []
    activityTotal.value = 0
    activityError.value = null
    activityLoading.value = true

    try {
      if (options.workspaceId && projectIds.length === 0) {
        if (requestId !== activityRequestId) return null
        return { items: [], total: 0 }
      }

      const result = await personalWorkApi.getActivity({
        ...options,
        signal: activityController.signal
      })
      if (requestId !== activityRequestId) return null
      const scopedItems = filterActivitiesByProjectIds(result.items, projectIds)
      activities.value = scopedItems
      activityTotal.value = projectIds.length ? scopedItems.length : result.total
      return result
    } catch (requestError) {
      if (requestId !== activityRequestId || isCanceled(requestError)) return null
      activityError.value = requestError
      throw requestError
    } finally {
      if (requestId === activityRequestId) activityLoading.value = false
    }
  }

  const reset = () => {
    listController?.abort()
    summaryController?.abort()
    activityController?.abort()
    listRequestId += 1
    summaryRequestId += 1
    activityRequestId += 1
    items.value = []
    totalCount.value = 0
    page.value = 1
    loading.value = false
    error.value = null
    summary.value = null
    summaryLoading.value = false
    summaryError.value = null
    activities.value = []
    activityTotal.value = 0
    activityLoading.value = false
    activityError.value = null
  }

  return {
    items,
    totalCount,
    page,
    pageSize,
    loading,
    error,
    summary,
    summaryLoading,
    summaryError,
    activities,
    activityTotal,
    activityLoading,
    activityError,
    fetchPage,
    fetchSummary,
    fetchActivity,
    reset
  }
}
