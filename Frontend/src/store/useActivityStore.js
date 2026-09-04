import { defineStore } from 'pinia'
import axiosClient from '@/api/axiosClient'

export const useActivityStore = defineStore('activityStore', {
  state: () => ({
    activities: [],
    loading: false,
    total: 0
  }),
  actions: {
    normalizeActivity(item) {
      const timestamp = item.timestamp || item.createdAt || item.time || new Date().toISOString()
      const user = item.user || item.userName || item.actorName || item.email || 'System'
      const action = item.action || item.eventType || 'updated'
      const resource = item.resource || item.entityName || item.targetType || ''
      let summary = item.summary || item.description || item.message || `${user} ${action} ${resource}`.trim()

      // Clean up GUIDs from the text for better readability
      const guidRegex = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;
      summary = summary.replace(guidRegex, '').replace(/\s+/g, ' ').trim();
      summary = summary.replace(/:\s*$/, '').replace(/->\s*$/, '').trim();

      // Translate Vietnamese backend hardcoded strings to English
      const viToEnMap = {
        'Da tao': 'Created',
        'Da them thanh vien vao': 'Added member to',
        'Da xoa thanh vien khoi': 'Removed member from',
        'Da doi quan ly': 'Changed manager for',
        'Da cap nhat tien do': 'Updated progress of',
        'Da cap nhat': 'Updated',
        'Da luu tru': 'Archived',
        'Da khoi phuc': 'Restored',
        'Da xoa': 'Deleted',
        'Da lien ket muc tieu voi team': 'Linked goal to team',
        'Da bo lien ket muc tieu khoi team': 'Unlinked goal from team',
        'Da lien ket du an voi team': 'Linked project to team',
        'Da bo lien ket du an khoi team': 'Unlinked project from team',
        'Da binh luan tren': 'Commented on',
        'Da them ban cap nhat cho': 'Added an update to',
        'Da them bai hoc cho': 'Added a lesson to',
        'Da them rui ro cho': 'Added a risk to',
        'Da them quyet dinh cho': 'Added a decision to',
        'Da gui loi khen:': 'Sent a praise:',
        'Da doi trang thai': 'Changed status of',
        'Da gan sao': 'Starred',
        'Da bo gan sao': 'Unstarred',
        'Da theo doi': 'Followed',
        'Da bo theo doi': 'Unfollowed'
      };

      for (const [vi, en] of Object.entries(viToEnMap)) {
        if (summary.startsWith(vi)) {
          summary = summary.replace(vi, en);
          break;
        }
      }

      return {
        id: item.id || `${action}-${resource}-${timestamp}`,
        icon: item.icon || 'fa-solid fa-clock-rotate-left',
        text: summary,
        bold: item.bold || '',
        time: new Date(timestamp).toLocaleString(),
        _ts: Date.parse(timestamp) || Date.now(),
        raw: item
      }
    },

    async fetchRecentActivities(params = {}) {
      this.loading = true
      try {
        // Default to last 30 days if no timeFilter provided
        if (!params.timeFilter) params.timeFilter = '30d'
        
        const res = await axiosClient.get('/site-auditlogs', { params })
        if (res.data && res.data.data) {
          this.activities = (res.data.data.items || []).map(item => this.normalizeActivity(item))
          this.total = res.data.data.total || 0
        }
      } catch (err) {
        console.error('Failed to load activities', err)
      } finally {
        this.loading = false
      }
    },
    
    async logActivity(text, bold, icon = 'fa-regular fa-bell') {
      // In a real app, this might be handled by the backend automatically on actions.
      // But we can keep a local-only log or just refresh from server.
      await this.fetchRecentActivities()
    }
  }
})

