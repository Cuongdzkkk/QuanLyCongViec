import { defineStore } from 'pinia'
import axiosClient from '@/api/axiosClient'
import { useStarredStore } from '@/store/useStarredStore'
import { signalRService } from '@/api/signalrService'

let teamRealtimeHandler = null

export const useTeamStore = defineStore('team', {
  state: () => ({
    currentTeam: null,
    members: [],
    hierarchy: { parent: null, children: [] },
    goals: [],
    projects: [],
    activityTasks: [],
    kudos: [],
    isLoading: false,
    error: null,
    isEmpty: false,
    isSuccess: false,
    allTeams: []
  }),
  actions: {
    async initializeRealtime() {
      if (!teamRealtimeHandler) {
        teamRealtimeHandler = event => this.applyRealtimeEntityEvent(event)
        signalRService.on('EntityChanged', teamRealtimeHandler)
      }
      await signalRService.startAuthenticatedConnection()
    },
    upsertTeam(team) {
      if (!team?.id) return
      const index = this.allTeams.findIndex(item => `${item.id}` === `${team.id}`)
      if (index >= 0) this.allTeams[index] = { ...this.allTeams[index], ...team }
      else this.allTeams.unshift(team)
      if (`${this.currentTeam?.id}` === `${team.id}`) {
        this.currentTeam = {
          ...this.currentTeam,
          ...team,
          status: team.isActive === false || team.isArchived ? 'Archived' : 'Active'
        }
      }
      this.isEmpty = this.allTeams.length === 0
    },
    async applyRealtimeEntityEvent(event) {
      if (!event) return
      if (event.entityType === 'Kudo') {
        const departmentId = event.data?.departmentId
        if ((!departmentId || `${this.currentTeam?.id}` === `${departmentId}`) && this.currentTeam?.id && !this.isLoading) {
          await this.fetchTeamDetail(this.currentTeam.id)
        }
        return
      }
      if (event.entityType === 'department') {
        if (event.action === 'deleted') {
          this.allTeams = this.allTeams.filter(team => `${team.id}` !== `${event.entityId}`)
          if (`${this.currentTeam?.id}` === `${event.entityId}`) this.currentTeam = null
        } else if (event.data) {
          this.upsertTeam(event.data)
        }
        return
      }
      if (
        event.entityType === 'department-detail' &&
        `${this.currentTeam?.id}` === `${event.entityId}` &&
        !this.isLoading
      ) {
        await this.fetchTeamDetail(event.entityId)
      }
    },
    async fetchAllTeams() {
      this.isLoading = true
      try {
        const response = await axiosClient.get('/departments')
        this.allTeams = response.data?.data || response.data || []
      } catch (err) {
        this.error = err.message || 'Failed to fetch teams'
      } finally {
        this.isLoading = false
      }
    },
    async fetchWorkspaceTeams(workspaceId) {
      this.isLoading = true
      this.error = null
      try {
        if (!workspaceId) {
          this.allTeams = []
          return
        }
        const response = await axiosClient.get(`/workspaces/${workspaceId}/teams`)
        const teams = response.data?.data || response.data || []
        this.allTeams = teams.map(team => ({
          ...team,
          id: team.departmentId || team.DepartmentId || team.id,
          name: team.name || team.Name,
          description: team.description || team.Description,
          memberCount: team.memberCount ?? team.MemberCount ?? 0,
          workspaceAccess: true
        }))
      } catch (err) {
        this.error = err.message || 'Failed to fetch teams for this site'
        this.allTeams = []
      } finally {
        this.isLoading = false
      }
    },
    async fetchTeamDetail(id) {
      this.isLoading = true
      this.error = null
      try {
        const response = await axiosClient.get(`/departments/${id}/full`)
        const team = response.data?.data || response.data
        const starredStore = useStarredStore()
        this.currentTeam = {
          id: team.id,
          name: team.name,
          avatarText: team.name ? team.name.substring(0, 2).toUpperCase() : 'T',
          coverImage: team.coverImage || 'https://images.unsplash.com/photo-1550751827-4bd374c3f58b?w=1200&q=80',
          status: team.isArchived ? 'Archived' : 'Active',
          isStarred: starredStore.isStarred('Team', team.id),
          description: team.description || 'Department details.',
          manager: team.manager || null
        }
        
        this.members = team.members || []
        this.hierarchy = team.hierarchy || { parent: null, children: [] }
        this.goals = team.goals || []
        this.projects = team.projects || []
        this.activityTasks = team.activityTasks || []
        this.kudos = team.kudos || []
        
        this.isSuccess = true
      } catch (err) {
        this.error = err.message || 'Failed to fetch team detail'
      } finally {
        this.isLoading = false
      }
    },
    async toggleArchive() {
      if (!this.currentTeam) return
      try {
        if (this.currentTeam.status === 'Archived') {
          await axiosClient.put(`/departments/${this.currentTeam.id}/restore`)
        } else {
          await axiosClient.put(`/departments/${this.currentTeam.id}/archive`)
        }
        
        const newStatus = this.currentTeam.status === 'Archived' ? 'Active' : 'Archived'
        this.currentTeam.status = newStatus
        
        // Cập nhật lại trong allTeams
        const index = this.allTeams.findIndex(t => t.id === this.currentTeam.id)
        if (index !== -1) {
          this.allTeams[index].isArchived = newStatus === 'Archived'
          this.allTeams[index].isActive = newStatus === 'Active'
        }
      } catch (err) {
        console.error('Failed to toggle archive team', err)
      }
    },
    async toggleStar() {
      if (!this.currentTeam) return
      const starredStore = useStarredStore()
      await starredStore.toggleStar('Team', this.currentTeam.id)
      this.currentTeam.isStarred = starredStore.isStarred('Team', this.currentTeam.id)
    },
    async addMembers(userIds) {
      if (!this.currentTeam) return
      try {
        await axiosClient.post(`/departments/${this.currentTeam.id}/members`, userIds)
        await this.fetchTeamDetail(this.currentTeam.id) // Reload members
      } catch (err) {
        console.error('Failed to add members', err)
        throw err
      }
    },
    async removeMember(userId) {
      if (!this.currentTeam) return
      try {
        await axiosClient.delete(`/departments/${this.currentTeam.id}/members/${userId}`)
        await this.fetchTeamDetail(this.currentTeam.id) // Reload members
      } catch (err) {
        console.error('Failed to remove member', err)
        throw err
      }
    },
    async updateHierarchy(parentId) {
      if (!this.currentTeam) return
      try {
        await axiosClient.put(`/departments/${this.currentTeam.id}/hierarchy`, parentId)
        await this.fetchTeamDetail(this.currentTeam.id) // Reload hierarchy
      } catch (err) {
        console.error('Failed to update hierarchy', err)
        throw err
      }
    },
    async updateTeamParent(teamId, parentId) {
      try {
        await axiosClient.put(`/departments/${teamId}/hierarchy`, parentId)
        if (this.currentTeam) {
          await this.fetchTeamDetail(this.currentTeam.id)
        }
      } catch (err) {
        console.error('Failed to update team parent', err)
        throw err
      }
    },
    async updateManager(userId) {
      if (!this.currentTeam) return
      try {
        await axiosClient.put(`/departments/${this.currentTeam.id}/manager/${userId}`)
        await this.fetchTeamDetail(this.currentTeam.id)
      } catch (err) {
        console.error('Failed to update manager', err)
        throw err
      }
    },
    async updateTeam(data) {
      if (!this.currentTeam) return
      try {
        await axiosClient.put(`/departments/${this.currentTeam.id}`, data)
        await this.fetchTeamDetail(this.currentTeam.id) // Reload data
      } catch (err) {
        console.error('Failed to update team', err)
        throw err
      }
    },
    async deleteTeam() {
      if (!this.currentTeam) return
      try {
        const teamId = this.currentTeam.id
        await axiosClient.delete(`/departments/${teamId}`)
        this.allTeams = this.allTeams.filter(team => `${team.id}` !== `${teamId}`)
        this.currentTeam = null
      } catch (err) {
        console.error('Failed to delete team', err)
        throw err
      }
    },
    async createTeam(data) {
      try {
        const response = await axiosClient.post('/departments', data)
        const team = response.data?.data || response.data
        this.upsertTeam(team)
        return team
      } catch (err) {
        console.error('Failed to create team', err)
        throw err
      }
    },
    async linkGoal(goalId) {
      if (!this.currentTeam) return
      try {
        await axiosClient.post(`/departments/${this.currentTeam.id}/goals/${goalId}`)
        await this.fetchTeamDetail(this.currentTeam.id)
      } catch (err) {
        console.error('Failed to link goal', err)
        throw err
      }
    },
    async unlinkGoal(goalId) {
      if (!this.currentTeam) return
      try {
        await axiosClient.delete(`/departments/${this.currentTeam.id}/goals/${goalId}`)
        await this.fetchTeamDetail(this.currentTeam.id)
      } catch (err) {
        console.error('Failed to unlink goal', err)
        throw err
      }
    },
    async linkProject(projectId) {
      if (!this.currentTeam) return
      try {
        await axiosClient.post(`/departments/${this.currentTeam.id}/projects/${projectId}`)
        await this.fetchTeamDetail(this.currentTeam.id)
      } catch (err) {
        console.error('Failed to link project', err)
        throw err
      }
    },
    async unlinkProject(projectId) {
      if (!this.currentTeam) return
      try {
        await axiosClient.delete(`/departments/${this.currentTeam.id}/projects/${projectId}`)
        await this.fetchTeamDetail(this.currentTeam.id)
      } catch (err) {
        console.error('Failed to unlink project', err)
        throw err
      }
    },
    async sendKudos(data) {
      try {
        await axiosClient.post('/kudos', data)
        if (this.currentTeam && this.currentTeam.id === data.departmentId) {
          await this.fetchTeamDetail(this.currentTeam.id)
        } else {
          await this.fetchRecentKudos()
        }
      } catch (err) {
        console.error('Failed to send kudos', err)
        throw err
      }
    },
    async fetchRecentKudos() {
      try {
        const response = await axiosClient.get('/kudos')
        this.kudos = response.data?.data || response.data || []
      } catch (err) {
        console.error('Failed to fetch kudos', err)
        throw err
      }
    }
  }
})
