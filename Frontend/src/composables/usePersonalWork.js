import { ref } from 'vue'
import { personalWorkApi } from '@/api/personalWorkApi'

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

  const fetchPage = async ({ scope, page: nextPage = 1, pageSize: nextPageSize = 10 } = {}) => {
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
        signal: listController.signal
      })
      if (requestId !== listRequestId) return null

      items.value = result.items
      totalCount.value = result.totalCount
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

  const fetchSummary = async () => {
    summaryController?.abort()
    summaryController = new AbortController()
    const requestId = ++summaryRequestId

    summary.value = null
    summaryError.value = null
    summaryLoading.value = true

    try {
      const result = await personalWorkApi.getSummary({ signal: summaryController.signal })
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

    activities.value = []
    activityTotal.value = 0
    activityError.value = null
    activityLoading.value = true

    try {
      const result = await personalWorkApi.getActivity({
        ...options,
        signal: activityController.signal
      })
      if (requestId !== activityRequestId) return null
      activities.value = result.items
      activityTotal.value = result.total
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
