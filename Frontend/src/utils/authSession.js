import { clearLegacyGitHubCredentialStorage } from '@/utils/githubCredentials'

const ACCESS_TOKEN_KEY = 'accessToken'
const USER_KEY = 'user'

const safeJsonParse = (value) => {
  try {
    return JSON.parse(value || '{}')
  } catch {
    return {}
  }
}

export const getStoredAccessToken = () => {
  if (typeof window === 'undefined') return ''

  return (
    window.sessionStorage.getItem(ACCESS_TOKEN_KEY)
    || window.localStorage.getItem(ACCESS_TOKEN_KEY)
    || ''
  )
}

export const getStoredUserSession = () => {
  if (typeof window === 'undefined') return {}

  return safeJsonParse(
    window.sessionStorage.getItem(USER_KEY)
    || window.localStorage.getItem(USER_KEY)
  )
}

export const saveAuthSession = ({ accessToken, fullName, email, systemRoles, id, avatarColor, avatarUrl, username }) => {
  if (typeof window === 'undefined') return

  const userPayload = JSON.stringify({ id, fullName, email, systemRoles, avatarColor, avatarUrl, username })

  window.sessionStorage.setItem(ACCESS_TOKEN_KEY, accessToken || '')
  window.sessionStorage.setItem(USER_KEY, userPayload)

  // Clean legacy global storage to avoid cross-tab account collisions.
  window.localStorage.removeItem(ACCESS_TOKEN_KEY)
  window.localStorage.removeItem(USER_KEY)
}

export const clearAuthSession = () => {
  if (typeof window === 'undefined') return

  clearLegacyGitHubCredentialStorage()
  window.sessionStorage.removeItem(ACCESS_TOKEN_KEY)
  window.sessionStorage.removeItem(USER_KEY)
  window.localStorage.removeItem(ACCESS_TOKEN_KEY)
  window.localStorage.removeItem(USER_KEY)
}
