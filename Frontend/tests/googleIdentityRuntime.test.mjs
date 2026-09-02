import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'

const GOOGLE_STATE = Symbol.for('sprinta.googleIdentity')
const frontendRoot = path.resolve(import.meta.dirname, '..')
const read = relativePath => fs.readFileSync(path.join(frontendRoot, relativePath), 'utf8')
const loginSource = read('src/views/Login.vue')
const serviceSource = read('src/services/googleIdentityService.js')
const authApiSource = read('src/api/authApi.js')

const resetGoogleState = () => {
  delete globalThis[GOOGLE_STATE]
  delete globalThis.google
}

test('custom button uses GIS authorization-code request with exact Google label', async () => {
  resetGoogleState()
  let initializedOptions
  let requestCount = 0
  globalThis.google = {
    accounts: {
      oauth2: {
        initCodeClient: options => {
          initializedOptions = options
          return { requestCode: () => { requestCount += 1 } }
        }
      }
    }
  }

  const { registerGoogleAuthorizationCodeClient } = await import('../src/services/googleIdentityService.js')
  const callbacks = []
  const client = await registerGoogleAuthorizationCodeClient({
    clientId: 'test-client-id',
    state: 'server-issued-state',
    callback: response => callbacks.push(response)
  })

  client.requestCode()
  initializedOptions.callback({ code: 'authorization-code', state: 'server-issued-state' })

  assert.equal(requestCount, 1)
  assert.equal(initializedOptions.client_id, 'test-client-id')
  assert.equal(initializedOptions.scope, 'openid email profile')
  assert.equal(initializedOptions.include_granted_scopes, false)
  assert.equal(initializedOptions.ux_mode, 'popup')
  assert.equal(initializedOptions.state, 'server-issued-state')
  assert.deepEqual(callbacks, [{ code: 'authorization-code', state: 'server-issued-state' }])
  assert.match(loginSource, /<span>Google<\/span>/)
  assert.match(loginSource, /@click="startGoogleAuthorization"/)
  assert.doesNotMatch(loginSource, /renderGoogleIdentityButton|renderButton/)
  assert.doesNotMatch(serviceSource, /accounts\.id|renderButton|\.prompt\(/)
  client.release()
  resetGoogleState()
})

test('Google auth API posts code and state with the protected request header', () => {
  assert.match(authApiSource, /['"]\/auth\/google-code\/start['"]/)
  assert.match(authApiSource, /['"]\/auth\/google-code\/login['"]/)
  assert.match(authApiSource, /X-Requested-With.*XmlHttpRequest/)
  assert.doesNotMatch(loginSource, /google\.accounts\.id\.prompt|accounts\.id\.initialize|accounts\.id\.renderButton/)
  assert.doesNotMatch(serviceSource, /auto_select|fedcm|prompt\(/i)
})
