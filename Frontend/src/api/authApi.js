import axiosClient from '@/api/axiosClient'
import { updateCurrentAccessToken } from '@/utils/authSession'

export async function loginWithGoogleCredential(credential, { signal } = {}) {
  if (typeof credential !== 'string' || !credential.trim()) {
    const error = new Error('Google credential is required.')
    error.status = 400
    throw error
  }

  const response = await axiosClient.post(
    '/auth/google-login',
    { credential: credential.trim() },
    { signal }
  )
  const authData = response?.data?.data

  if (!authData?.accessToken || !authData?.id || !authData?.email) {
    const error = new Error('Invalid authentication response.')
    error.status = 502
    throw error
  }

  return authData
}

export async function startGoogleAuthorizationCodeLogin() {
  const response = await axiosClient.post(
    '/auth/google-code/start',
    {},
    { headers: { 'X-Requested-With': 'XmlHttpRequest' } }
  )
  const state = response?.data?.data?.state
  if (typeof state !== 'string' || !state.trim()) {
    const error = new Error('Google authorization state was not issued.')
    error.status = 502
    throw error
  }
  return state
}

export async function loginWithGoogleAuthorizationCode(code, state, { signal } = {}) {
  if (typeof code !== 'string' || !code.trim() || typeof state !== 'string' || !state.trim()) {
    const error = new Error('Google authorization response is incomplete.')
    error.status = 400
    throw error
  }

  const response = await axiosClient.post(
    '/auth/google-code/login',
    { code: code.trim(), state: state.trim() },
    {
      signal,
      headers: { 'X-Requested-With': 'XmlHttpRequest' }
    }
  )
  const authData = response?.data?.data

  if (!authData?.accessToken || !authData?.id || !authData?.email) {
    const error = new Error('Invalid authentication response.')
    error.status = 502
    throw error
  }

  return authData
}

export async function startGoogleAccountLink() {
  const response = await axiosClient.post(
    '/auth/google-code/link/start',
    {},
    { headers: { 'X-Requested-With': 'XmlHttpRequest' } }
  )
  const state = response?.data?.data?.state
  if (typeof state !== 'string' || !state.trim()) {
    throw new Error('Google account link state was not issued.')
  }
  return state
}

export async function linkGoogleAccountWithAuthorizationCode(code, state, { signal } = {}) {
  if (typeof code !== 'string' || !code.trim() || typeof state !== 'string' || !state.trim()) {
    const error = new Error('Google account link response is incomplete.')
    error.status = 400
    throw error
  }
  return axiosClient.post(
    '/auth/google-code/link',
    { code: code.trim(), state: state.trim() },
    { signal, headers: { 'X-Requested-With': 'XmlHttpRequest' } }
  )
}

export async function startGitHubAccountLink() {
  const response = await axiosClient.get('/auth/github-link/start')
  const url = response?.data?.data?.url
  if (typeof url !== 'string' || !url.trim()) {
    throw new Error('GitHub account link URL was not issued.')
  }
  return url
}

export async function linkGitHubAccount(code, state) {
  if (typeof code !== 'string' || !code.trim() || typeof state !== 'string' || !state.trim()) {
    const error = new Error('GitHub account link response is incomplete.')
    error.status = 400
    throw error
  }
  return axiosClient.post('/auth/github-link', { code: code.trim(), state: state.trim() })
}

export async function getExternalLoginStatus() {
  const response = await axiosClient.get('/auth/external-logins')
  return response?.data?.data || []
}

export async function unlinkExternalLogin(provider) {
  return axiosClient.delete(`/auth/external-logins/${encodeURIComponent(provider)}`)
}

export async function restoreAuthSessionFromCookie() {
  const refreshResponse = await axiosClient.post('/auth/refresh-token')
  const accessToken = refreshResponse?.data?.data?.accessToken ?? refreshResponse?.data?.accessToken
  if (!accessToken) {
    throw new Error('Auth restore response did not include an access token.')
  }

  updateCurrentAccessToken(accessToken)
  const profileResponse = await axiosClient.get('/users/me')
  const profile = profileResponse?.data?.data
  if (!profile?.id) {
    throw new Error('Auth restore response did not include a user profile.')
  }

  return {
    accessToken,
    id: profile.id,
    email: profile.email,
    fullName: profile.fullName,
    avatarUrl: profile.avatarUrl,
    systemRoles: profile.systemRoles || [],
    username: profile.username
  }
}
