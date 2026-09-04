import { defineStore } from 'pinia'
import axiosClient from '@/api/axiosClient'
import { isValidEntityId } from '@/utils/contextIds'
import { signalRService } from '@/api/signalrService'

let siteRealtimeRegistered = false
let activeSiteStore = null

const handleSiteRealtime = (event) => {
  const entityType = `${event?.entityType || ''}`.toLowerCase()
  if (entityType !== 'workspace' || !event?.entityId) return
  const id = `${event.entityId}`
  const index = activeSiteStore?.sites.findIndex(site => `${site.id || site.Id}` === id) ?? -1
  if (`${event.action}`.toLowerCase() === 'deleted') {
    if (index >= 0) activeSiteStore.sites.splice(index, 1)
    if (`${activeSiteStore?.recentSite?.id || activeSiteStore?.recentSite?.Id || ''}` === id) {
      activeSiteStore.recentSite = activeSiteStore.sites[0] || null
      if (activeSiteStore.recentSite) {
        localStorage.setItem('recent_site_id', activeSiteStore.recentSite.id || activeSiteStore.recentSite.Id)
      } else {
        localStorage.removeItem('recent_site_id')
      }
    }
    return
  }
  if (index >= 0 && event.data) {
    activeSiteStore.sites[index] = { ...activeSiteStore.sites[index], ...event.data }
    if (`${activeSiteStore?.recentSite?.id || activeSiteStore?.recentSite?.Id || ''}` === id) {
      activeSiteStore.recentSite = activeSiteStore.sites[index]
    }
  }
}

export const useSiteStore = defineStore('site', {
  state: () => ({
    recentSite: null,
    sites: [],
    loading: false,
    error: null
  }),
  getters: {
    activeSite: (state) => state.recentSite
  },
  actions: {
    registerRealtime() {
      activeSiteStore = this
      if (siteRealtimeRegistered) return
      signalRService.on('EntityChanged', handleSiteRealtime)
      siteRealtimeRegistered = true
    },
    async fetchSites() {
      this.registerRealtime()
      this.loading = true
      this.error = null
      try {
        const response = await axiosClient.get('/workspaces')
        this.sites = (response.data?.data || []).map(site => ({
          ...site,
          id: site.id || site.Id,
          name: site.name || site.Name,
          logo: site.logo || site.Logo || null,
          slug: site.slug || site.Slug || '',
          ownerId: site.ownerId || site.OwnerId || null,
          ownerName: site.ownerName || site.OwnerName || '',
          ownerEmail: site.ownerEmail || site.OwnerEmail || '',
          ownerAvatarUrl: site.ownerAvatarUrl || site.OwnerAvatarUrl || null,
          workspaceRole: site.workspaceRole || site.WorkspaceRole || '',
          accessSource: site.accessSource || site.AccessSource || 'DIRECT',
          projectCount: site.projectCount ?? site.ProjectCount ?? 0,
          memberCount: site.memberCount ?? site.MemberCount ?? 0,
          createdAt: site.createdAt || site.CreatedAt || null,
          updatedAt: site.updatedAt || site.UpdatedAt || null
        }))
        
        // Find recent site based on most recently created or some local storage logic
        if (this.sites.length > 0) {
          const recentId = localStorage.getItem('recent_site_id')
          this.recentSite = isValidEntityId(recentId)
            ? (this.sites.find(s => s.id === recentId || s.Id === recentId) || this.sites[0])
            : this.sites[0]
        } else {
          this.recentSite = null
          localStorage.removeItem('recent_site_id')
        }
      } catch (err) {
        this.error = err.message || 'Failed to fetch sites'
      } finally {
        this.loading = false
      }
    },
    async createSite(siteData) {
      this.loading = true
      try {
        const payload = {
          name: siteData.name,
          slug: siteData.slug || siteData.name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, ''),
          timezone: 'Asia/Ho_Chi_Minh'
        }
        const response = await axiosClient.post('/workspaces', payload)
        const newSite = response.data?.data
        if (newSite) {
          this.sites.push(newSite)
          this.setRecentSite(newSite)
        }
        return newSite
      } finally {
        this.loading = false
      }
    },
    setRecentSite(site) {
      const siteId = site?.id || site?.Id
      if (!site || !isValidEntityId(siteId)) return
      const previousSiteId = this.recentSite?.id || this.recentSite?.Id || null
      this.recentSite = site
      localStorage.setItem('recent_site_id', siteId)
      if (`${previousSiteId || ''}` !== `${siteId}`) {
        window.dispatchEvent(new CustomEvent('sprinta-workspace-changed', {
          detail: { workspaceId: siteId, previousWorkspaceId: previousSiteId }
        }))
      }
    }
  }
})
