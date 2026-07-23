const LEGACY_GITHUB_TOKEN_KEY = 'githubToken'

export const clearLegacyGitHubCredentialStorage = (browserWindow = globalThis.window) => {
  if (!browserWindow) return

  browserWindow.localStorage?.removeItem(LEGACY_GITHUB_TOKEN_KEY)
  browserWindow.sessionStorage?.removeItem(LEGACY_GITHUB_TOKEN_KEY)
}

export const runWithEphemeralGitHubToken = async (credentialState, submit) => {
  const token = credentialState.token?.trim() || null

  try {
    return await submit(token)
  } finally {
    credentialState.token = ''
    clearLegacyGitHubCredentialStorage()
  }
}
