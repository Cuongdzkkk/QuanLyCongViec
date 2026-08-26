import { defineStore } from 'pinia'
import { getStoredUserSession, getStoredAccessToken, saveAuthSession, clearAuthSession, AUTH_SESSION_CHANGED } from '@/utils/authSession'
import { useProjectStore } from '@/store/useProjectStore'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    user: getStoredUserSession() || {},
    token: getStoredAccessToken() || '',
    isAuthenticated: !!getStoredAccessToken()
  }),
  
  getters: {
    currentUser: (state) => state.user,
    userId: (state) => state.user?.id,
    userAvatar: (state) => state.user?.avatarUrl || state.user?.AvatarUrl || '',
    userName: (state) => state.user?.fullName || state.user?.username || '',
    userColor: (state) => state.user?.avatarColor || 'var(--color-primary)',
    roles: (state) => state.user?.systemRoles || []
  },
  
  actions: {
    initialize() {
      // Sync initial state
      this.user = getStoredUserSession() || {}
      this.token = getStoredAccessToken() || ''
      this.isAuthenticated = !!this.token

      // Listen for cross-tab login/logout/update
      window.addEventListener('storage', (event) => this.handleStorageEvent(event))
      window.addEventListener(AUTH_SESSION_CHANGED, () => this.handleAuthSessionChanged())
    },
    
    login(authData) {
      saveAuthSession(authData)
      this.user = getStoredUserSession()
      this.token = authData.accessToken
      this.isAuthenticated = true
    },
    
    logout() {
      useProjectStore().clearWorkspaceData()
      clearAuthSession()
      this.user = {}
      this.token = ''
      this.isAuthenticated = false
    },
    
    updateUser(userData) {
      this.user = { ...this.user, ...userData }
      saveAuthSession({ 
        accessToken: this.token, 
        ...this.user 
      })
    },

    updateAvatar(avatarUrl) {
      this.user.avatarUrl = avatarUrl
      saveAuthSession({
        accessToken: this.token,
        ...this.user
      })
    },

    handleAuthSessionChanged() {
      const previousUserId = this.user?.id || this.user?.Id || ''
      const storedUser = getStoredUserSession()
      const storedToken = getStoredAccessToken()
      const nextUserId = storedUser?.id || storedUser?.Id || ''
      if (`${previousUserId}` !== `${nextUserId}` || (!!this.token !== !!storedToken)) {
        useProjectStore().clearWorkspaceData()
      }
      this.user = storedUser
      this.token = storedToken
      this.isAuthenticated = !!storedToken
    },
    
    handleStorageEvent(event) {
      if (event.key === 'user' || event.key === 'accessToken') {
        const storedUser = getStoredUserSession()
        const storedToken = getStoredAccessToken()
        
        if (!storedToken && this.isAuthenticated) {
          // Cross-tab logout detected
          this.user = {}
          this.token = ''
          this.isAuthenticated = false
          // Có thể kích hoạt reload hoặc redirect tới login ở router thay vì reload cả app
        } else if (storedUser) {
          // Cross-tab update or login
          this.user = storedUser
          this.token = storedToken
          this.isAuthenticated = !!storedToken
        }
      }
    }
  }
})
