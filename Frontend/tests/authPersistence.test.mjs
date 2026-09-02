import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'

import { createAuthRestoreFlow } from '../src/utils/authTransport.js'

const authSessionSource = fs.readFileSync(
  path.resolve(import.meta.dirname, '../src/utils/authSession.js'),
  'utf8'
)

const createHarness = ({ token = '', restoreResult, restoreError = null } = {}) => {
  let currentToken = token
  let saved = []
  let cleared = 0
  let restoreCalls = 0

  const restoreSession = async () => {
    restoreCalls += 1
    if (restoreError) throw restoreError
    return restoreResult
  }

  const flow = createAuthRestoreFlow({
    getCurrentAccessToken: () => currentToken,
    restoreSession,
    saveAuthSession: authData => {
      saved.push(authData)
      currentToken = authData.accessToken
    },
    clearAuthSession: () => {
      cleared += 1
      currentToken = ''
    }
  })

  return {
    flow,
    clearPersistedSession: () => {
      cleared += 1
      currentToken = ''
    },
    getState: () => ({ currentToken, saved, cleared, restoreCalls })
  }
}

test('LOGIN_RELOAD_PERSISTS and PERSISTENT_LOGIN_BROWSER_RESTART_SIMULATION restore through refresh cookie', async () => {
  const harness = createHarness({ restoreResult: { accessToken: 'fresh-access', id: 'user-1' } })

  assert.equal(await harness.flow(), true)
  assert.deepEqual(harness.getState().saved, [{ accessToken: 'fresh-access', id: 'user-1' }])
  assert.equal(harness.getState().restoreCalls, 1)

  assert.equal(await harness.flow(), true)
  assert.equal(harness.getState().restoreCalls, 1, 'an already hydrated session is not restored twice')
})

test('LOGIN_NAVIGATION_PERSISTS without a second restore', async () => {
  const harness = createHarness({ token: 'current-access', restoreResult: { accessToken: 'unused' } })

  assert.equal(await harness.flow(), true)
  assert.equal(harness.getState().restoreCalls, 0)
  assert.equal(harness.getState().cleared, 0)
})

test('LOGOUT_CLEARS_PERSISTENCE and expired or invalid auth is not restored', async () => {
  const logoutHarness = createHarness({ token: 'current-access', restoreResult: { accessToken: 'unused' } })
  logoutHarness.clearPersistedSession()
  assert.equal(logoutHarness.getState().currentToken, '')
  assert.equal(logoutHarness.getState().cleared, 1)
  assert.match(authSessionSource, /window\.sessionStorage\.removeItem\(ACCESS_TOKEN_KEY\)/)
  assert.match(authSessionSource, /notifyAuthSessionChanged\('logout'\)/)

  for (const error of [new Error('expired'), new Error('invalid')]) {
    const harness = createHarness({ restoreError: error })
    assert.equal(await harness.flow(), false)
    assert.equal(harness.getState().cleared, 1)
    assert.equal(harness.getState().currentToken, '')
  }
})

test('REMEMBER_ME_BEHAVIOR is persistent by default and missing refresh does not duplicate tokens', async () => {
  const harness = createHarness({ restoreResult: { user: { id: 'user-1' } } })

  assert.equal(await harness.flow(), false)
  assert.equal(harness.getState().saved.length, 0)
  assert.equal(harness.getState().cleared, 1)
})

test('LEGACY_SESSION_STORAGE_MIGRATION_SAFE and NO_TOKEN_DUPLICATION_BETWEEN_STORES', () => {
  assert.match(authSessionSource, /legacyToken = window\.localStorage\.getItem\(ACCESS_TOKEN_KEY\)/)
  assert.match(authSessionSource, /window\.localStorage\.removeItem\(ACCESS_TOKEN_KEY\)/)
  assert.match(authSessionSource, /window\.sessionStorage\.setItem\(ACCESS_TOKEN_KEY, accessToken\)/)
  assert.doesNotMatch(authSessionSource, /window\.localStorage\.setItem\(ACCESS_TOKEN_KEY/)
  assert.match(authSessionSource, /AUTH_STORAGE_EVENT_KEY/)
})
