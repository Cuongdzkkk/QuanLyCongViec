import { defineStore } from 'pinia'
import axiosClient from '@/api/axiosClient'
import { language } from '@/i18n'

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

      const viToViMap = {
        'Da tao': 'Đã tạo',
        'Da them thanh vien vao': 'Đã thêm thành viên vào',
        'Da xoa thanh vien khoi': 'Đã xóa thành viên khỏi',
        'Da doi quan ly': 'Đã đổi quản lý cho',
        'Da cap nhat tien do': 'Đã cập nhật tiến độ của',
        'Da cap nhat': 'Đã cập nhật',
        'Da luu tru': 'Đã lưu trữ',
        'Da khoi phuc': 'Đã khôi phục',
        'Da xoa': 'Đã xóa',
        'Da lien ket muc tieu voi team': 'Đã liên kết mục tiêu với team',
        'Da bo lien ket muc tieu khoi team': 'Đã bỏ liên kết mục tiêu khỏi team',
        'Da lien ket du an voi team': 'Đã liên kết dự án với team',
        'Da bo lien ket du an khoi team': 'Đã bỏ liên kết dự án khỏi team',
        'Da binh luan tren': 'Đã bình luận trên',
        'Da them ban cap nhat cho': 'Đã thêm bản cập nhật cho',
        'Da them bai hoc cho': 'Đã thêm bài học cho',
        'Da them rui ro cho': 'Đã thêm rủi ro cho',
        'Da them quyet dinh cho': 'Đã thêm quyết định cho',
        'Da gui loi khen:': 'Đã gửi lời khen:',
        'Da doi trang thai': 'Đã đổi trạng thái',
        'Da gan sao': 'Đã gắn sao',
        'Da bo gan sao': 'Đã bỏ gắn sao',
        'Da theo doi': 'Đã theo dõi',
        'Da bo theo doi': 'Đã bỏ theo dõi'
      };

      const currentMap = language.value === 'en' ? viToEnMap : viToViMap;

      for (const [vi, mappedStr] of Object.entries(currentMap)) {
        if (summary.startsWith(vi)) {
          summary = summary.replace(vi, mappedStr);
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

