const GOOGLE_IDENTITY_SCRIPT_URL = 'https://accounts.google.com/gsi/client'
const GOOGLE_IDENTITY_SCRIPT_SELECTOR = 'script[data-sprinta-google-identity]'
const GOOGLE_IDENTITY_STATE = Symbol.for('sprinta.googleIdentity')

const getState = () => {
  const root = globalThis
  if (!root[GOOGLE_IDENTITY_STATE]) {
    root[GOOGLE_IDENTITY_STATE] = {
      loadPromise: null,
      initializedClientId: '',
      callback: null,
      callbackOwner: null
    }
  }
  return root[GOOGLE_IDENTITY_STATE]
}

const getGoogleIdentityApi = () => globalThis.google?.accounts?.id || null

export const loadGoogleIdentityScript = () => {
  const readyApi = getGoogleIdentityApi()
  if (readyApi) return Promise.resolve(readyApi)

  const state = getState()
  if (state.loadPromise) return state.loadPromise

  state.loadPromise = new Promise((resolve, reject) => {
    let script = document.querySelector(GOOGLE_IDENTITY_SCRIPT_SELECTOR)
      || document.querySelector(`script[src="${GOOGLE_IDENTITY_SCRIPT_URL}"]`)
    const createdHere = !script

    const finish = () => {
      const api = getGoogleIdentityApi()
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

export const registerGoogleIdentity = async ({ clientId, callback }) => {
  if (!clientId || typeof callback !== 'function') {
    throw new Error('Google Identity Services configuration is incomplete.')
  }

  const api = await loadGoogleIdentityScript()
  const state = getState()

  if (state.initializedClientId && state.initializedClientId !== clientId) {
    throw new Error('Google Identity Services was initialized with a different client.')
  }

  if (!state.initializedClientId) {
    api.initialize({
      client_id: clientId,
      callback: response => state.callback?.(response),
      auto_select: false,
      cancel_on_tap_outside: true
    })
    state.initializedClientId = clientId
  }

  const owner = Symbol('google-identity-callback-owner')
  state.callbackOwner = owner
  state.callback = callback

  return () => {
    if (state.callbackOwner !== owner) return
    state.callback = null
    state.callbackOwner = null
  }
}

export const promptGoogleIdentity = () => {
  const api = getGoogleIdentityApi()
  if (!api) {
    throw new Error('Google Identity Services is not ready.')
  }

  api.prompt()
}
