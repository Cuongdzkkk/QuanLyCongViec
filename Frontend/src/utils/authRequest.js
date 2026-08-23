const AUTH_PATHS = [
  '/auth/login',
  '/auth/register',
  '/auth/send-otp',
  '/auth/verify-otp',
  '/auth/reset-password',
  '/auth/refresh-token',
  '/auth/google-login',
  '/auth/github-login',
  '/auth/invite-info',
  '/auth/accept-invite-token'
]

export const getAuthHeader = token => token ? `Bearer ${token}` : ''

export const applyAuthHeader = (config, token) => {
  if (!config) return config
  config.headers = config.headers || {}
  const header = getAuthHeader(token)
  if (header) config.headers.Authorization = header
  else if (typeof config.headers.delete === 'function') config.headers.delete('Authorization')
  else delete config.headers.Authorization
  return config
}

export const isAuthRequest = url => {
  const requestUrl = String(url || '')
  return AUTH_PATHS.some(path => requestUrl.includes(path))
}

export const shouldRefreshUnauthorized = (error, request) => Boolean(
  error?.response?.status === 401 && request && !request._retry && !isAuthRequest(request.url)
)
