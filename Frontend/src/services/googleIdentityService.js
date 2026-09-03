const GOOGLE_IDENTITY_SCRIPT_URL = 'https://accounts.google.com/gsi/client'
const GOOGLE_IDENTITY_SCRIPT_SELECTOR = 'script[data-sprinta-google-identity]'
const GOOGLE_IDENTITY_STATE = Symbol.for('sprinta.googleIdentity')

const getState = () => {
  const root = globalThis
  if (!root[GOOGLE_IDENTITY_STATE]) {
    root[GOOGLE_IDENTITY_STATE] = {
      loadPromise: null,
      callback: null,
      callbackOwner: null,
      errorCallback: null
    }
  }
  return root[GOOGLE_IDENTITY_STATE]
}

const getGoogleAuthorizationCodeApi = () => globalThis.google?.accounts?.oauth2 || null

export const loadGoogleIdentityScript = () => {
  const readyApi = getGoogleAuthorizationCodeApi()
  if (readyApi) return Promise.resolve(readyApi)

  const state = getState()
  if (state.loadPromise) return state.loadPromise

  state.loadPromise = new Promise((resolve, reject) => {
    let script = document.querySelector(GOOGLE_IDENTITY_SCRIPT_SELECTOR)
      || document.querySelector(`script[src="${GOOGLE_IDENTITY_SCRIPT_URL}"]`)
    const createdHere = !script

    const finish = () => {
      const api = getGoogleAuthorizationCodeApi()
      if (api) {
        script?.setAttribute('data-sprinta-google-identity-ready', 'true')
        resolve(api)
        return
      }
      state.loadPromise = null
      reject(new Error('Google Identity Services is unavailable.'))
    }

    const fail = () => {
      state.loadPromise = null
      if (createdHere) script?.remove()
      reject(new Error('Google Identity Services could not be loaded.'))
    }

    if (!script) {
      script = document.createElement('script')
      script.src = GOOGLE_IDENTITY_SCRIPT_URL
      script.async = true
      script.defer = true
      script.setAttribute('data-sprinta-google-identity', 'true')
    }

    if (script.getAttribute('data-sprinta-google-identity-ready') === 'true') {
      finish()
      return
    }

    script.addEventListener('load', finish, { once: true })
    script.addEventListener('error', fail, { once: true })
    if (createdHere) document.head.appendChild(script)
  })

  return state.loadPromise
}

export const registerGoogleAuthorizationCodeClient = async ({
  clientId,
  state: oauthState,
  callback,
  errorCallback
}) => {
  if (!clientId || !oauthState || typeof callback !== 'function') {
    throw new Error('Google authorization configuration is incomplete.')
  }

  const api = await loadGoogleIdentityScript()
  if (typeof api.initCodeClient !== 'function') {
    throw new Error('Google authorization code flow is unavailable.')
  }

  const sharedState = getState()
  const owner = Symbol('google-authorization-code-callback-owner')
  sharedState.callbackOwner = owner
  sharedState.callback = callback
  sharedState.errorCallback = errorCallback

  const client = api.initCodeClient({
    client_id: clientId,
    scope: 'openid email profile',
    include_granted_scopes: false,
    ux_mode: 'popup',
    state: oauthState,
    callback: response => sharedState.callback?.(response),
    error_callback: response => sharedState.errorCallback?.(response)
  })

  if (!client || typeof client.requestCode !== 'function') {
    sharedState.callback = null
    sharedState.errorCallback = null
    sharedState.callbackOwner = null
    throw new Error('Google authorization code client is unavailable.')
  }

  return {
    requestCode: () => client.requestCode(),
    release: () => {
      if (sharedState.callbackOwner !== owner) return
      sharedState.callback = null
      sharedState.errorCallback = null
      sharedState.callbackOwner = null
    }
  }
}
