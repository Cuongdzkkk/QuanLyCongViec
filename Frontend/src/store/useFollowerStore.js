import { defineStore } from 'pinia'
import axiosClient from '@/api/axiosClient'
import { useSiteStore } from '@/store/useSiteStore'
import { ensureWorkspaceIdFromState, resolveWorkspaceIdFromState } from '@/utils/contextIds'
import { signalRService } from '@/api/signalrService'

let followerRealtimeRegistered = false
let activeFollowerStore = null
const handleFollowerRealtime = event => {
  if (`${event?.entityType || ''}`.toLowerCase() === 'follower') activeFollowerStore?.fetchFollowedItems()
}

export const useFollowerStore = defineStore('followerStore', {
  state: () => ({
    followedItems: [],
    loading: false,
    error: null
  }),
  actions: {
    registerRealtime() {
      activeFollowerStore = this
      if (followerRealtimeRegistered) return
      signalRService.on('EntityChanged', handleFollowerRealtime)
      followerRealtimeRegistered = true
    },
    getWorkspaceId() {
      const siteStore = useSiteStore()
      return resolveWorkspaceIdFromState({ siteStore })
    },
    async ensureWorkspaceId() {
      const siteStore = useSiteStore()
      return ensureWorkspaceIdFromState({ siteStore })
    },
    async fetchFollowedItems() {
      this.registerRealtime()
      this.loading = true
      this.error = null
      try {
        const workspaceId = await this.ensureWorkspaceId()
        if (!workspaceId) throw new Error('No workspace selected')
        
        const response = await axiosClient.get(`/workspaces/${workspaceId}/followers`)
        this.followedItems = response.data?.data || response.data || []
      } catch (err) {
        this.error = err.message || 'Failed to fetch followed items'
        console.error(this.error)
      } finally {
        this.loading = false
      }
    },
    async toggleFollow(entityType, entityId) {
      try {
        const workspaceId = await this.ensureWorkspaceId()
        if (!workspaceId) throw new Error('No workspace selected')
        await axiosClient.post(`/workspaces/${workspaceId}/followers/toggle`, null, {
          params: { entityType, entityId }
        })
        await this.fetchFollowedItems()
      } catch (err) {
        console.error('Failed to toggle follow', err)
      }
    }
  }
})
