import axiosClient from '@/api/axiosClient'

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
