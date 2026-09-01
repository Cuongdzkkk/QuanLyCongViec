import { clearLegacyGitHubCredentialStorage } from '@/utils/githubCredentials'
import { createAuthReadiness } from '@/utils/authTransport'

const ACCESS_TOKEN_KEY = 'accessToken'
const USER_KEY = 'user'
export const AUTH_SESSION_CHANGED = 'sprinta:auth-session-changed'
const ACCOUNT_CONTEXT_KEYS = [
  'recent_site_id',
  'currentProjectId',
  'lastProjectId',
  'active_checkin_project_id'
]
const authReadiness = createAuthReadiness()

const safeJsonParse = (value) => {
  try {
    return JSON.parse(value || '{}')
  } catch {
    return {}
  }
}

const notifyAuthSessionChanged = () => {
  if (typeof window !== 'undefined') {
    window.dispatchEvent(new Event(AUTH_SESSION_CHANGED))
  }
}

export const getCurrentAccessToken = () => {
  if (typeof window === 'undefined') return ''

  return window.sessionStorage.getItem(ACCESS_TOKEN_KEY) || ''
}

export const getStoredAccessToken = getCurrentAccessToken

export const getStoredUserSession = () => {
  if (typeof window === 'undefined') return {}

  return safeJsonParse(window.sessionStorage.getItem(USER_KEY))
}

export const restoreAuthSession = () => {
  if (typeof window !== 'undefined') {
    const legacyToken = window.localStorage.getItem(ACCESS_TOKEN_KEY)
    const legacyUser = window.localStorage.getItem(USER_KEY)

    if (!getCurrentAccessToken() && legacyToken) {
      window.sessionStorage.setItem(ACCESS_TOKEN_KEY, legacyToken)
    }
    if (!window.sessionStorage.getItem(USER_KEY) && legacyUser) {
      window.sessionStorage.setItem(USER_KEY, legacyUser)
    }
    window.localStorage.removeItem(ACCESS_TOKEN_KEY)
    window.localStorage.removeItem(USER_KEY)
  }
  authReadiness.markReady()
}

export const waitForAuthReady = () => authReadiness.waitForReady()
export const isAuthReady = () => authReadiness.isReady()

export const updateCurrentAccessToken = (accessToken) => {
  if (typeof window === 'undefined') return

  if (accessToken) window.sessionStorage.setItem(ACCESS_TOKEN_KEY, accessToken)
  else window.sessionStorage.removeItem(ACCESS_TOKEN_KEY)
  window.localStorage.removeItem(ACCESS_TOKEN_KEY)
  notifyAuthSessionChanged()
}

export const saveAuthSession = ({ accessToken, fullName, email, systemRoles, id, avatarColor, avatarUrl, username }) => {
  if (typeof window === 'undefined') return

  const previousUser = getStoredUserSession()
  const previousUserId = previousUser?.id || previousUser?.Id || ''
  if (previousUserId && id && `${previousUserId}` !== `${id}`) {
    clearAccountContext()
  }

  const userPayload = JSON.stringify({ id, fullName, email, systemRoles, avatarColor, avatarUrl, username })

  if (accessToken) window.sessionStorage.setItem(ACCESS_TOKEN_KEY, accessToken)
  else window.sessionStorage.removeItem(ACCESS_TOKEN_KEY)
  window.sessionStorage.setItem(USER_KEY, userPayload)

  // Clean legacy global storage to avoid cross-tab account collisions.
  window.localStorage.removeItem(ACCESS_TOKEN_KEY)
  window.localStorage.removeItem(USER_KEY)
  notifyAuthSessionChanged()
}

export const clearAuthSession = () => {
  if (typeof window === 'undefined') return

  clearLegacyGitHubCredentialStorage()
  clearAccountContext()
  window.sessionStorage.removeItem(ACCESS_TOKEN_KEY)
  window.sessionStorage.removeItem(USER_KEY)
  window.localStorage.removeItem(ACCESS_TOKEN_KEY)
  window.localStorage.removeItem(USER_KEY)
  authReadiness.markReady()
  notifyAuthSessionChanged()
}

export const clearAccountContext = () => {
  if (typeof window === 'undefined') return

  ACCOUNT_CONTEXT_KEYS.forEach((key) => {
    window.sessionStorage.removeItem(key)
    window.localStorage.removeItem(key)
  })
}
