export const createAuthReadiness = () => {
  let ready = false
  let resolveReady
  const readyPromise = new Promise(resolve => { resolveReady = resolve })

  return {
    isReady: () => ready,
    markReady: () => {
      if (ready) return
      ready = true
      resolveReady()
    },
    waitForReady: () => ready ? Promise.resolve() : readyPromise
  }
}

export const attachCurrentAccessToken = async (config, {
  waitForAuthReady,
  getCurrentAccessToken,
  applyAuthHeader
}) => {
  await waitForAuthReady()
  applyAuthHeader(config, getCurrentAccessToken())
  return config
}

export const createCurrentAccessTokenFactory = getCurrentAccessToken => () => getCurrentAccessToken() || ''

export const createTokenAwareReconnectPolicy = (getCurrentAccessToken, delays) => ({
  nextRetryDelayInMilliseconds: ({ previousRetryCount }) => {
    if (!getCurrentAccessToken()) return null
    return delays[previousRetryCount] ?? null
  }
})

export const createRefreshCoordinator = ({
  refreshAccessToken,
  updateAccessToken,
  handleRefreshFailure
}) => {
  let refreshPromise = null

  const refresh = () => {
    if (refreshPromise) return refreshPromise

    refreshPromise = Promise.resolve()
      .then(refreshAccessToken)
      .then((token) => {
        if (!token) throw new Error('Refresh token response did not include an access token.')
        updateAccessToken(token)
        return token
      })
      .catch((error) => {
        handleRefreshFailure(error)
        throw error
      })
      .finally(() => { refreshPromise = null })

    return refreshPromise
  }

  return {
    refresh,
    retryAfterRefresh: async retry => retry(await refresh())
  }
}
