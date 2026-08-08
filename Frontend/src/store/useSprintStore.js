import { defineStore } from 'pinia'
import { sprintApi } from '@/api/sprintApi'
import { reportExpectedError } from '@/utils/errorTelemetry'
import { getStoredAccessToken, getStoredUserSession } from '@/utils/authSession'
import { getSprintApiError, normalizeSprintState, SPRINT_STATE } from '@/utils/sprintState'

const SPRINT_CACHE_TTL_MS = 30000
let listAbortController = null
let listRequestSequence = 0
const transitionControllers = new Map()

const sessionKey = () => {
  if (!getStoredAccessToken()) return 'anonymous'
  const user = getStoredUserSession() || {}
  return user.id || user.userId || user.Id || user.email || 'authenticated'
}

const transitionKey = (action, projectId, sprintId) =>
  `${action}:${projectId}:${sprintId}`

export const useSprintStore = defineStore('sprint', {
  state: () => ({
    sprints: [],
    activeSprint: null,
    sprintDetails: {},
    loading: false,
    error: null,
    loadedProjectId: null,
    loadedSessionKey: null,
    lastFetchedByProject: {},
    transitioning: {}
  }),
  getters: {
    isTransitioning: state => (action, projectId, sprintId) =>
      Boolean(state.transitioning[transitionKey(action, projectId, sprintId)])
  },
  actions: {
    resetScope() {
      listRequestSequence += 1
      listAbortController?.abort()
      listAbortController = null
      transitionControllers.forEach(controller => controller.abort())
      transitionControllers.clear()
      this.sprints = []
      this.activeSprint = null
      this.sprintDetails = {}
      this.loading = false
      this.error = null
      this.loadedProjectId = null
      this.loadedSessionKey = null
      this.lastFetchedByProject = {}
      this.transitioning = {}
    },

    ensureScope(projectId) {
      const currentSessionKey = sessionKey()
      if (
        this.loadedProjectId !== projectId ||
        this.loadedSessionKey !== currentSessionKey
      ) {
        this.resetScope()
        this.loadedProjectId = projectId
        this.loadedSessionKey = currentSessionKey
      }
      return currentSessionKey
    },

    applySprint(sprint) {
      if (!sprint?.id) return
      const normalized = { ...sprint, state: normalizeSprintState(sprint.state) }
      const index = this.sprints.findIndex(item => item.id === normalized.id)
      if (index === -1) this.sprints = [...this.sprints, normalized]
      else this.sprints = this.sprints.map(item => item.id === normalized.id ? normalized : item)
      this.sprintDetails = { ...this.sprintDetails, [normalized.id]: normalized }
      this.activeSprint = this.sprints.find(item => item.state === SPRINT_STATE.ACTIVE) || null
    },

    async fetchSprints(projectId, options = {}) {
      if (!projectId || projectId === 'default' || projectId.length < 30) {
        this.resetScope()
        return []
      }

      const { force = false } = options
      const currentSessionKey = this.ensureScope(projectId)
      const lastFetched = this.lastFetchedByProject[projectId]
      const isWarm = lastFetched && (Date.now() - lastFetched) < SPRINT_CACHE_TTL_MS
      if (!force && isWarm) {
        return this.sprints
      }

      listAbortController?.abort()
      const controller = new AbortController()
      listAbortController = controller
      const requestId = ++listRequestSequence
      this.loading = true
      this.error = null

      try {
        const result = await sprintApi.getSprints(projectId, { signal: controller.signal })
        if (
          requestId !== listRequestSequence ||
          this.loadedProjectId !== projectId ||
          this.loadedSessionKey !== currentSessionKey ||
          sessionKey() !== currentSessionKey
        ) {
          return this.sprints
        }

        this.sprints = result.map(sprint => ({
          ...sprint,
          state: normalizeSprintState(sprint.state)
        }))
        this.activeSprint = this.sprints.find(item => item.state === SPRINT_STATE.ACTIVE) || null
        this.sprintDetails = Object.fromEntries(this.sprints.map(sprint => [sprint.id, sprint]))
        this.lastFetchedByProject = {
          ...this.lastFetchedByProject,
          [projectId]: Date.now()
        }
        return this.sprints
      } catch (error) {
        if (error?.code === 'ERR_CANCELED') return this.sprints
        if (requestId === listRequestSequence) {
          this.sprints = []
          this.activeSprint = null
          this.sprintDetails = {}
          this.error = getSprintApiError(error)
        }
        reportExpectedError('Failed to fetch sprints', error)
        return []
      } finally {
        if (requestId === listRequestSequence) {
          this.loading = false
          if (listAbortController === controller) listAbortController = null
        }
      }
    },

    async fetchCurrentSprint(projectId, options = {}) {
      await this.fetchSprints(projectId, options)
      return this.loadedProjectId === projectId ? this.activeSprint : null
    },

    async fetchSprintDetail(projectId, sprintId) {
      const currentSessionKey = this.ensureScope(projectId)
      const sprint = await sprintApi.getSprint(projectId, sprintId)
      if (
        this.loadedProjectId === projectId &&
        this.loadedSessionKey === currentSessionKey &&
        sessionKey() === currentSessionKey
      ) {
        this.applySprint(sprint)
        return sprint
      }
      return null
    },

    async toggleFavorite(projectId, sprintId) {
      if (!projectId || !sprintId) return
      this.ensureScope(projectId)

      const index = this.sprints.findIndex(item => item.id === sprintId)
      const previous = index !== -1 ? this.sprints[index].isFavorite : null

      if (index !== -1) {
        this.sprints[index].isFavorite = !this.sprints[index].isFavorite
      }

      try {
        const result = await sprintApi.toggleFavorite(projectId, sprintId)
        if (index !== -1) {
          this.sprints[index].isFavorite = Boolean(result?.isFavorite)
        }
      } catch (error) {
        if (index !== -1) {
          this.sprints[index].isFavorite = previous
        }
        reportExpectedError('Failed to toggle sprint favorite', error)
      }
    },

    async runTransition(action, projectId, sprintId, request) {
      if (!projectId || !sprintId) return { ignored: true }
      const key = transitionKey(action, projectId, sprintId)
      if (this.transitioning[key]) return { deduplicated: true }

      const currentSessionKey = this.ensureScope(projectId)
      const controller = new AbortController()
      transitionControllers.set(key, controller)
      this.transitioning = { ...this.transitioning, [key]: true }
      try {
        const sprint = await request(controller.signal)
        const stale =
          this.loadedProjectId !== projectId ||
          this.loadedSessionKey !== currentSessionKey ||
          sessionKey() !== currentSessionKey
        if (stale) return { stale: true }

        this.applySprint(sprint)
        await this.fetchSprints(projectId, { force: true })
        if (
          this.loadedProjectId !== projectId ||
          this.loadedSessionKey !== currentSessionKey ||
          sessionKey() !== currentSessionKey
        ) {
          return { stale: true }
        }
        return { data: sprint }
      } catch (error) {
        if (error?.code === 'ERR_CANCELED') return { stale: true }
        const status = error?.response?.status
        if (
          [404, 409].includes(status) &&
          this.loadedProjectId === projectId &&
          this.loadedSessionKey === currentSessionKey
        ) {
          await this.fetchSprints(projectId, { force: true })
        }
        reportExpectedError(`Failed to ${action} sprint`, error)
        throw error
      } finally {
        if (transitionControllers.get(key) === controller) {
          transitionControllers.delete(key)
          const next = { ...this.transitioning }
          delete next[key]
          this.transitioning = next
        }
      }
    },

    async startSprint(projectId, sprintId) {
      return this.runTransition(
        'start',
        projectId,
        sprintId,
        signal => sprintApi.startSprint(projectId, sprintId, { signal })
      )
    },

    async closeSprint(projectId, sprintId, targetSprintId = null) {
      return this.runTransition(
        'close',
        projectId,
        sprintId,
        signal => sprintApi.closeSprint(projectId, sprintId, targetSprintId, { signal })
      )
    }
  }
})
