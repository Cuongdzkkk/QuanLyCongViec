import { defineStore } from 'pinia'
import { getStoredUserSession, getStoredAccessToken, restoreAuthSession, saveAuthSession, clearAuthSession, AUTH_SESSION_CHANGED, AUTH_STORAGE_EVENT_KEY } from '@/utils/authSession'
import { restoreAuthSessionFromCookie } from '@/api/authApi'
import { createAuthRestoreFlow } from '@/utils/authTransport'
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
    async initialize() {
      restoreAuthSession()
      const restoreFromCookie = createAuthRestoreFlow({
        getCurrentAccessToken: getStoredAccessToken,
        restoreSession: restoreAuthSessionFromCookie,
        saveAuthSession,
        clearAuthSession: () => clearAuthSession({ broadcast: false })
      })
      await restoreFromCookie()
      // Sync initial state
      this.user = getStoredUserSession() || {}
      this.token = getStoredAccessToken() || ''
      this.isAuthenticated = !!this.token

      // Listen for cross-tab login/logout/update
      window.addEventListener('storage', (event) => this.handleStorageEvent(event, restoreFromCookie))
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
    
    async handleStorageEvent(event, restoreFromCookie) {
      if (event.key !== AUTH_STORAGE_EVENT_KEY || !event.newValue) return

      let authEvent
      try {
        authEvent = JSON.parse(event.newValue)
      } catch {
        return
      }

      if (authEvent.type === 'logout') {
        useProjectStore().clearWorkspaceData()
        clearAuthSession({ broadcast: false })
        this.user = {}
        this.token = ''
        this.isAuthenticated = false
        return
      }

      if ((authEvent.type === 'login' || authEvent.type === 'token-updated') && !getStoredAccessToken()) {
        await restoreFromCookie()
        this.user = getStoredUserSession() || {}
        this.token = getStoredAccessToken() || ''
        this.isAuthenticated = !!this.token
      }
    }
  }
})
