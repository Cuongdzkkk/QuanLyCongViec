import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'

const GOOGLE_STATE = Symbol.for('sprinta.googleIdentity')
const loginSource = fs.readFileSync(
  path.resolve(import.meta.dirname, '../src/views/Login.vue'),
  'utf8'
)

const resetGoogleState = () => {
  delete globalThis[GOOGLE_STATE]
  delete globalThis.google
}

test('Google button click uses the supported rendered-button path, not One Tap prompt', async () => {
  resetGoogleState()
  const renderCalls = []
  let promptCalls = 0
  globalThis.google = {
    accounts: {
      id: {
        renderButton: (container, options) => renderCalls.push({ container, options }),
        prompt: () => { promptCalls += 1 },
        initialize: () => {}
      }
    }
  }

  const { renderGoogleIdentityButton } = await import('../src/services/googleIdentityService.js')
  const container = {
    clientWidth: 320,
    replaceChildren: () => {}
  }

  renderGoogleIdentityButton(container)
  assert.equal(renderCalls.length, 1)
  assert.equal(renderCalls[0].options.text, 'continue_with')
  assert.equal(promptCalls, 0)
  assert.doesNotMatch(loginSource, /promptGoogleIdentity|google\.accounts\.id\.prompt/)
  assert.doesNotMatch(loginSource, /google-identity-button\.is-hidden/)
  resetGoogleState()
})

test('Google Identity registration forwards the credential callback', async () => {
  resetGoogleState()
  let registeredCallback
  globalThis.google = {
    accounts: {
      id: {
        initialize: options => { registeredCallback = options.callback },
        renderButton: () => {},
        prompt: () => {}
      }
    }
  }

  const { registerGoogleIdentity } = await import('../src/services/googleIdentityService.js?callback')
  const received = []
  const release = await registerGoogleIdentity({
    clientId: 'test-client-id',
    callback: response => received.push(response)
  })

  registeredCallback({ credential: 'test-credential' })
  assert.deepEqual(received, [{ credential: 'test-credential' }])
  release()
  resetGoogleState()
})
