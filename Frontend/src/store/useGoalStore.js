import { defineStore } from 'pinia'
import axiosClient from '@/api/axiosClient'
import { useSiteStore } from '@/store/useSiteStore'
import { ensureWorkspaceIdFromState, resolveWorkspaceIdFromState } from '@/utils/contextIds'
import { useStarredStore } from '@/store/useStarredStore'

export const useGoalStore = defineStore('goal', {
  state: () => ({ lessons: [], risks: [], decisions: [],
    goals: [],
    currentGoal: null,
    updates: [],
    linkedProjects: [],
    lessons: [],
    risks: [],
    decisions: [],
    history: [],
    isLoading: false,
    error: null,
    isEmpty: false,
    isSuccess: false
  }),
  actions: {
    requireWorkspaceId() {
      const workspaceId = this.getWorkspaceId()
      if (!workspaceId) {
        throw new Error('No workspace selected')
      }
      return workspaceId
    },
    async ensureWorkspaceId() {
      const siteStore = useSiteStore()
      const workspaceId = await ensureWorkspaceIdFromState({ siteStore })
      if (!workspaceId) {
        throw new Error('No workspace selected')
      }
      return workspaceId
    },
    async fetchGoalTabs(goalId) {
      try {
        const workspaceId = await this.ensureWorkspaceId()
        const [lessonsRes, risksRes, decisionsRes] = await Promise.all([
          axiosClient.get(`/workspaces/${workspaceId}/goals/${goalId}/lessons`),
          axiosClient.get(`/workspaces/${workspaceId}/goals/${goalId}/risks`),
          axiosClient.get(`/workspaces/${workspaceId}/goals/${goalId}/decisions`)
        ])
        this.lessons = (lessonsRes.data.data || lessonsRes.data)
        this.risks = (risksRes.data.data || risksRes.data)
        this.decisions = (decisionsRes.data.data || decisionsRes.data)
      } catch (err) {
        console.error(err)
      }
    },
    async addGoalLesson(goalId, payload) {
      try {
        const workspaceId = await this.ensureWorkspaceId()
        const res = await axiosClient.post(`/workspaces/${workspaceId}/goals/${goalId}/lessons`, payload)
        this.lessons.unshift(res.data.data || res.data)
        return res.data
      } catch (err) { throw err }
    },
    async addGoalRisk(goalId, payload) {
      try {
        const workspaceId = await this.ensureWorkspaceId()
        const res = await axiosClient.post(`/workspaces/${workspaceId}/goals/${goalId}/risks`, payload)
        this.risks.unshift(res.data.data || res.data)
        return res.data
      } catch (err) { throw err }
    },
    async addGoalDecision(goalId, payload) {
      try {
        const workspaceId = await this.ensureWorkspaceId()
        const res = await axiosClient.post(`/workspaces/${workspaceId}/goals/${goalId}/decisions`, payload)
        this.decisions.unshift(res.data.data || res.data)
        return res.data
      } catch (err) { throw err }
    },

    getWorkspaceId() {
      const siteStore = useSiteStore()
      return resolveWorkspaceIdFromState({ siteStore })
    },
    async fetchGoals() {
      this.isLoading = true
      this.error = null
      this.isEmpty = false
      this.isSuccess = false
      try {
        const workspaceId = await this.ensureWorkspaceId()
        
        const response = await axiosClient.get(`/workspaces/${workspaceId}/goals`)
        this.goals = response.data?.data || response.data || []
        this.isEmpty = this.goals.length === 0
        this.isSuccess = true
      } catch (err) {
        this.error = err.message || 'Failed to fetch goals'
        this.goals = []
      } finally {
        this.isLoading = false
      }
    },
    async createGoal(goalData) {
      this.isLoading = true
      try {
        const workspaceId = await this.ensureWorkspaceId()
        
        const response = await axiosClient.post(`/workspaces/${workspaceId}/goals`, goalData)
        const newGoal = response.data?.data || response.data
        
        // Map UI properties for immediate display
        newGoal.owner = newGoal.owner || goalData.owner || newGoal.ownerName || 'Chưa gán'
        newGoal.ownerColor = newGoal.ownerColor || goalData.ownerColor || null
        
        this.goals.push(newGoal)
        return newGoal
      } catch (err) {
        this.error = err.message || 'Failed to create goal'
        throw err
      } finally {
        this.isLoading = false
      }
    },
    async fetchGoalDetail(id) {
      this.isLoading = true
      this.error = null
      try {
        const workspaceId = await this.ensureWorkspaceId()
        
        const response = await axiosClient.get(`/workspaces/${workspaceId}/goals/${id}`)
        const goal = response.data?.data || response.data
        this.currentGoal = goal
        
        // Map sub-entities from goal object (assuming EF Core Include)
        this.updates = goal.updates || []
        this.lessons = goal.lessons || []
        this.risks = goal.risks || []
        this.decisions = goal.decisions || []
        this.linkedProjects = goal.linkedProjects || []
        
        this.isSuccess = true
      } catch (err) {
        this.error = err.message || 'Failed to fetch goal detail'
      } finally {
        this.isLoading = false
      }
    },
    async addUpdate(goalId, data) {
      const workspaceId = await this.ensureWorkspaceId()
      const response = await axiosClient.post(`/workspaces/${workspaceId}/goals/${goalId}/updates`, data)
      const update = response.data?.data || response.data
      this.updates.unshift(update)
      const nextStatus = update?.newStatus || update?.status || data?.status
      const nextProgress = update?.newProgress ?? update?.progress ?? data?.progress
      if (this.currentGoal?.id === goalId) {
        if (nextStatus) this.currentGoal.status = nextStatus
        if (nextProgress !== undefined && nextProgress !== null) this.currentGoal.progress = nextProgress
      }
      const target = this.goals.find(goal => goal.id === goalId)
      if (target) {
        if (nextStatus) target.status = nextStatus
        if (nextProgress !== undefined && nextProgress !== null) target.progress = nextProgress
      }
    },
    async addLesson(goalId, data) {
      const workspaceId = await this.ensureWorkspaceId()
      const response = await axiosClient.post(`/workspaces/${workspaceId}/goals/${goalId}/lessons`, data)
      this.lessons.push(response.data?.data || response.data)
    },
    async addRisk(goalId, data) {
      const workspaceId = await this.ensureWorkspaceId()
      const response = await axiosClient.post(`/workspaces/${workspaceId}/goals/${goalId}/risks`, data)
      this.risks.push(response.data?.data || response.data)
    },
    async addDecision(goalId, data) {
      const workspaceId = await this.ensureWorkspaceId()
      const response = await axiosClient.post(`/workspaces/${workspaceId}/goals/${goalId}/decisions`, data)
      this.decisions.push(response.data?.data || response.data)
    },
    async toggleArchive() {
      if (!this.currentGoal) return
      try {
        const workspaceId = await this.ensureWorkspaceId()
        await axiosClient.post(`/workspaces/${workspaceId}/goals/${this.currentGoal.id}/archive`)
        this.currentGoal.isArchived = !this.currentGoal.isArchived
      } catch (err) {
        console.error('Failed to archive goal', err)
      }
    },
    async toggleFollow(goalId) {
      const workspaceId = await this.ensureWorkspaceId()
      const response = await axiosClient.post(`/workspaces/${workspaceId}/followers/toggle`, null, {
        params: { entityType: 'Goal', entityId: goalId }
      })
      const isFollowing = response.data?.data?.isFollowing ?? response.data?.isFollowing
      const target = this.goals.find(g => g.id === goalId)
      if (target) target.isFollowing = isFollowing
      if (this.currentGoal && this.currentGoal.id === goalId) {
        this.currentGoal.isFollowing = isFollowing
      }
    },
    async toggleStar() {
      if (!this.currentGoal) return
      try {
        const starredStore = useStarredStore()
        const nextValue = !starredStore.isStarred('Goal', this.currentGoal.id)
        await starredStore.setStarred('Goal', this.currentGoal.id, nextValue)
        this.currentGoal.isStarred = nextValue
        const target = this.goals.find(g => g.id === this.currentGoal.id)
        if (target) target.isStarred = this.currentGoal.isStarred
      } catch (err) {
        console.error('Failed to toggle star', err)
      }
    }
  }
})
