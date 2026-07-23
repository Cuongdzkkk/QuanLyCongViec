import assert from 'node:assert/strict'
import test from 'node:test'

import {
  clearLegacyGitHubCredentialStorage,
  runWithEphemeralGitHubToken
} from '../src/utils/githubCredentials.js'

const storage = () => {
  const values = new Map()
  return {
    getItem: key => values.get(key) ?? null,
    removeItem: key => values.delete(key),
    setItem: (key, value) => values.set(key, value)
  }
}

test('component initialization clears legacy GitHub credential storage', () => {
  const browserWindow = { localStorage: storage(), sessionStorage: storage() }
  browserWindow.localStorage.setItem('githubToken', 'legacy-local-test-value')
  browserWindow.sessionStorage.setItem('githubToken', 'legacy-session-test-value')

  clearLegacyGitHubCredentialStorage(browserWindow)

  assert.equal(browserWindow.localStorage.getItem('githubToken'), null)
  assert.equal(browserWindow.sessionStorage.getItem('githubToken'), null)
})

test('ephemeral GitHub credential is submitted once and cleared after success', async () => {
  const credentialState = { token: ' one-time-test-value ' }
  let calls = 0

  await runWithEphemeralGitHubToken(credentialState, async token => {
    calls += 1
    assert.equal(token, 'one-time-test-value')
  })

  assert.equal(calls, 1)
  assert.equal(credentialState.token, '')
})

test('ephemeral GitHub credential is cleared after an error', async () => {
  const credentialState = { token: 'one-time-error-test-value' }

  await assert.rejects(
    runWithEphemeralGitHubToken(credentialState, async () => {
      throw new Error('safe test failure')
    }),
    /safe test failure/
  )

  assert.equal(credentialState.token, '')
})
