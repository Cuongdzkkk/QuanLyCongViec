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
