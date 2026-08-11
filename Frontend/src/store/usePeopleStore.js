import { defineStore } from 'pinia'
import axiosClient from '@/api/axiosClient'
import { signalRService } from '@/api/signalrService'

let peopleRealtimeRegistered = false
let activePeopleStore = null

const handlePeopleRealtime = (event) => {
  if (`${event?.entityType || ''}`.toLowerCase() !== 'user' || !event?.data?.id) return
  const data = event.data
  const index = activePeopleStore?.users.findIndex(user => `${user.id}` === `${data.id}`) ?? -1
  if (index >= 0) activePeopleStore.users[index] = { ...activePeopleStore.users[index], ...data }
  if (`${activePeopleStore?.currentUser?.id || ''}` === `${data.id}`) {
    activePeopleStore.currentUser = { ...activePeopleStore.currentUser, ...data }
  }
}

export const usePeopleStore = defineStore('people', {
  state: () => ({
    users: [],
    currentUser: null,
    linkedGoals: [],
    linkedProjects: [],
    kudos: [],
    history: [],
    isLoading: false,
    error: null,
    isEmpty: false,
    isSuccess: false,
    totalCount: 0,
    page: 1,
    pageSize: 20
  }),
  actions: {
    registerRealtime() {
      activePeopleStore = this
      if (peopleRealtimeRegistered) return
      signalRService.on('EntityChanged', handlePeopleRealtime)
      peopleRealtimeRegistered = true
    },
    async fetchPeople(search = '', page = 1, pageSize = 20, filters = {}) {
      this.registerRealtime()
      this.isLoading = true
      this.error = null
      this.isEmpty = false
      this.isSuccess = false
      try {
        const response = await axiosClient.get('/users', {
          params: { search, page, pageSize, ...filters }
        })
        const data = response.data?.data || response.data || []
        
        this.users = data.map(u => ({
          id: u.id,
          fullName: u.fullName || u.email,
          email: u.email,
          location: u.location || '',
          avatarUrl: u.avatarUrl,
          avatarColor: u.avatarColor,
          initials: u.initials,
          department: u.departmentName || 'N/A',
          departments: u.departments || [],
          position: u.jobTitle || 'Member',
          projects: u.projects || [],
          ownedProjectIds: u.ownedProjectIds || [],
          ownedGoalIds: u.ownedGoalIds || [],
          team: u.projects && u.projects.length > 0 ? u.projects[0] : 'N/A',
          timezone: u.timezone || '',
          status: u.status,
          bio: u.bio || ''
        }))
        this.totalCount = response.data?.total || 0
        this.page = response.data?.page || 1
        this.pageSize = response.data?.pageSize || 20
        this.isEmpty = this.users.length === 0
        this.isSuccess = true
      } catch (err) {
        this.error = err.message || 'Failed to fetch people'
        this.users = []
      } finally {
        this.isLoading = false
      }
    },
    async fetchProfileDetail(id) {
      this.registerRealtime()
      this.isLoading = true
      this.error = null
      try {
        const response = await axiosClient.get(`/users/${id}`)
        const data = response.data?.data || response.data
        
        if (!data) {
          throw new Error('User not found')
        }

        this.currentUser = {
          id: data.id,
          fullName: data.fullName,
          email: data.email,
          avatarUrl: data.avatarUrl,
          avatarColor: data.avatarColor,
          initials: data.initials,
          coverUrl: data.coverUrl,
          departments: data.departments || [],
          department: data.departmentName || 'N/A',
          position: data.jobTitle,
          team: data.team,
          status: data.status,
          bio: data.bio,
          location: data.location,
          timezone: data.timezone
        }
        
        this.linkedGoals = data.linkedGoals || []
        this.linkedProjects = data.linkedProjects || []
        this.kudos = (data.kudos || []).map(k => ({
          ...k,
          sender: k.senderName || k.sender,
          date: new Date(k.createdAt).toLocaleDateString('vi-VN')
        }))
        this.history = (data.history || []).map(log => ({
          ...log,
          time: new Date(log.time || log.createdAt).toLocaleString('vi-VN'),
          action: [log.actorName, log.action, log.entityType].filter(Boolean).join(' - ')
        }))
        
        this.isSuccess = true
      } catch (err) {
        this.error = err.message || 'Failed to fetch profile detail'
      } finally {
        this.isLoading = false
      }
    },
    async updateProfile(payload) {
      try {
        const response = await axiosClient.put('/users/profile', payload)
        return response.data
      } catch (err) {
        throw err
      }
    }
  }
})
