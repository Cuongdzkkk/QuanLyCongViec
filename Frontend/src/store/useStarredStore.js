import { computed, ref, watch } from 'vue'
import { defineStore } from 'pinia'
import { useAuthStore } from '@/store/useAuthStore'
import { useSiteStore } from '@/store/useSiteStore'
import { ensureWorkspaceIdFromState, resolveWorkspaceIdFromState } from '@/utils/contextIds'
import {
  getRecentViews,
  getStarredItems,
  recordRecentView,
  starItem,
  unstarItem
} from '@/api/starredRecentApi'

const emptyPagination = (pageSize = 50) => ({ totalCount: 0, page: 1, pageSize })
const itemKey = (itemType, itemId) => `${itemType}:${itemId}`
const isCanceled = (error) => error?.name === 'CanceledError' || error?.code === 'ERR_CANCELED'

export const useStarredStore = defineStore('starredStore', () => {
  const authStore = useAuthStore()
  const siteStore = useSiteStore()
  const starredItems = ref([])
  const recentItems = ref([])
  const starredPagination = ref(emptyPagination())
  const recentPagination = ref(emptyPagination())
  const loading = ref(false)
  const recentLoading = ref(false)
  const error = ref(null)
  const recentError = ref(null)
  const pendingItems = ref({})
  const pendingRecentItems = ref({})
  const knownStarredItems = ref({})
  let starredController = null
  let recentController = null
  let starredRequestId = 0
  let recentRequestId = 0

  const workspaceId = computed(() => resolveWorkspaceIdFromState({ siteStore }))
  const contextKey = computed(() => [
    authStore.isAuthenticated ? authStore.userId || 'authenticated' : 'anonymous',
    workspaceId.value || 'no-workspace'
  ].join(':'))

  function resetPersonalState() {
    starredController?.abort()
    recentController?.abort()
    starredRequestId += 1
    recentRequestId += 1
    starredItems.value = []
    recentItems.value = []
    starredPagination.value = emptyPagination(starredPagination.value.pageSize)
    recentPagination.value = emptyPagination(recentPagination.value.pageSize)
    loading.value = false
    recentLoading.value = false
    error.value = null
    recentError.value = null
    pendingItems.value = {}
    pendingRecentItems.value = {}
    knownStarredItems.value = {}
  }

  watch(contextKey, resetPersonalState, { flush: 'sync' })
  localStorage.removeItem('recently_viewed_tasks')

  const ensureWorkspaceId = () => ensureWorkspaceIdFromState({ siteStore })
  const isStarred = (itemType, id) => Boolean(knownStarredItems.value[itemKey(itemType, id)])
  const isPending = (itemType, id) => Boolean(pendingItems.value[itemKey(itemType, id)])

  async function fetchStarredItems(options = {}) {
    const page = Math.max(1, Number(options.page || 1))
    const pageSize = Math.min(100, Math.max(1, Number(options.pageSize || starredPagination.value.pageSize || 50)))
    const resolvedWorkspaceId = await ensureWorkspaceId()
    if (!resolvedWorkspaceId) {
      error.value = 'No workspace selected'
      throw new Error(error.value)
    }

    starredController?.abort()
    const requestContext = contextKey.value
    starredController = new AbortController()
    const requestId = ++starredRequestId
    loading.value = true
    error.value = null
    try {
      const result = await getStarredItems({
        workspaceId: resolvedWorkspaceId,
        page,
        pageSize,
        signal: starredController.signal
      })
      if (requestId !== starredRequestId || requestContext !== contextKey.value) return starredItems.value
      starredItems.value = result.items
      starredPagination.value = result.pagination
      const fetchedStatus = Object.fromEntries(
        result.items.map(item => [itemKey(item.itemType, item.itemId), true])
      )
      knownStarredItems.value = result.pagination.totalCount <= pageSize && page === 1
        ? fetchedStatus
        : { ...knownStarredItems.value, ...fetchedStatus }
      return starredItems.value
    } catch (requestError) {
      if (isCanceled(requestError)) return starredItems.value
      if (requestId === starredRequestId) {
        error.value = requestError.response?.data?.message || requestError.message || 'Failed to fetch starred items'
      }
      throw requestError
    } finally {
      if (requestId === starredRequestId) {
        loading.value = false
        starredController = null
      }
    }
  }

  async function setStarred(itemType, id, shouldStar) {
    if (!itemType || !id || isPending(itemType, id)) return null
    const resolvedWorkspaceId = await ensureWorkspaceId()
    if (!resolvedWorkspaceId) throw new Error('No workspace selected')

    const key = itemKey(itemType, id)
    const requestContext = contextKey.value
    const pendingToken = `${Date.now()}:${Math.random()}`
    pendingItems.value = { ...pendingItems.value, [key]: pendingToken }
    error.value = null
    try {
      const result = shouldStar
        ? await starItem({ workspaceId: resolvedWorkspaceId, itemType, itemId: id })
        : await unstarItem({ workspaceId: resolvedWorkspaceId, itemType, itemId: id })

      if (requestContext !== contextKey.value) return result
      if (shouldStar) {
        const mutationItem = result?.item || result?.Item
        const item = mutationItem?.itemId ? mutationItem : { itemType, itemId: id }
        starredItems.value = [
          item,
          ...starredItems.value.filter(current => itemKey(current.itemType, current.itemId) !== key)
        ]
        starredPagination.value = {
          ...starredPagination.value,
          totalCount: Math.max(starredPagination.value.totalCount, starredItems.value.length)
        }
      } else {
        const previousLength = starredItems.value.length
        starredItems.value = starredItems.value.filter(current => itemKey(current.itemType, current.itemId) !== key)
        if (starredItems.value.length !== previousLength) {
          starredPagination.value = {
            ...starredPagination.value,
            totalCount: Math.max(0, starredPagination.value.totalCount - 1)
          }
        }
      }
      knownStarredItems.value = { ...knownStarredItems.value, [key]: shouldStar }
      return result
    } catch (mutationError) {
      error.value = mutationError.response?.data?.message || mutationError.message || 'Failed to update starred item'
      throw mutationError
    } finally {
      if (pendingItems.value[key] === pendingToken) {
        const nextPending = { ...pendingItems.value }
        delete nextPending[key]
        pendingItems.value = nextPending
      }
    }
  }

  const toggleStar = (itemType, id) => setStarred(itemType, id, !isStarred(itemType, id))

  async function fetchRecentItems(options = {}) {
    const page = Math.max(1, Number(options.page || 1))
    const pageSize = Math.min(100, Math.max(1, Number(options.pageSize || recentPagination.value.pageSize || 50)))
    recentController?.abort()
    const requestContext = contextKey.value
    recentController = new AbortController()
    const requestId = ++recentRequestId
    recentLoading.value = true
    recentError.value = null
    try {
      const result = await getRecentViews({ page, pageSize, signal: recentController.signal })
      if (requestId !== recentRequestId || requestContext !== contextKey.value) return recentItems.value
      recentItems.value = result.items
      recentPagination.value = result.pagination
      return recentItems.value
    } catch (requestError) {
      if (isCanceled(requestError)) return recentItems.value
      if (requestId === recentRequestId) {
        recentError.value = requestError.response?.data?.message || requestError.message || 'Failed to fetch recent views'
      }
      throw requestError
    } finally {
      if (requestId === recentRequestId) {
        recentLoading.value = false
        recentController = null
      }
    }
  }

  async function recordViewed(entityType, entityId) {
    if (!entityType || !entityId) return null
    const key = itemKey(entityType, entityId)
    if (pendingRecentItems.value[key]) return null
    const requestContext = contextKey.value
    const pendingToken = `${Date.now()}:${Math.random()}`
    pendingRecentItems.value = { ...pendingRecentItems.value, [key]: pendingToken }
    recentError.value = null
    try {
      const result = await recordRecentView({ entityType, entityId })
      if (requestContext !== contextKey.value) return result
      const item = result?.entityId ? result : { entityType, entityId, viewedAt: new Date().toISOString() }
      recentItems.value = [
        item,
        ...recentItems.value.filter(current => itemKey(current.entityType, current.entityId) !== key)
      ]
      recentPagination.value = {
        ...recentPagination.value,
        totalCount: Math.max(recentPagination.value.totalCount, recentItems.value.length)
      }
      return result
    } catch (mutationError) {
      recentError.value = mutationError.response?.data?.message || mutationError.message || 'Failed to record recent view'
      throw mutationError
    } finally {
      if (pendingRecentItems.value[key] === pendingToken) {
        const nextPending = { ...pendingRecentItems.value }
        delete nextPending[key]
        pendingRecentItems.value = nextPending
      }
    }
  }

  return {
    starredItems,
    recentItems,
    starredPagination,
    recentPagination,
    loading,
    recentLoading,
    error,
    recentError,
    workspaceId,
    ensureWorkspaceId,
    isStarred,
    isPending,
    fetchStarredItems,
    fetchRecentItems,
    setStarred,
    toggleStar,
    recordViewed,
    resetPersonalState
  }
})
