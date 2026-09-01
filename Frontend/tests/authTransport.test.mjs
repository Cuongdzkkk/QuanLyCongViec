import assert from 'node:assert/strict'
import test from 'node:test'

import {
  attachCurrentAccessToken,
  createAuthReadiness,
  createCurrentAccessTokenFactory,
  createRefreshCoordinator,
  createTokenAwareReconnectPolicy
} from '../src/utils/authTransport.js'

test('authenticated REST requests wait for hydration and read the latest token', async () => {
  const readiness = createAuthReadiness()
  let token = 'before-login'
  const config = { headers: {} }
  let settled = false

  const pending = attachCurrentAccessToken(config, {
    waitForAuthReady: readiness.waitForReady,
    getCurrentAccessToken: () => token,
    applyAuthHeader: (request, value) => { request.headers.Authorization = `Bearer ${value}` }
  }).then(() => { settled = true })

  await Promise.resolve()
  assert.equal(settled, false, 'protected requests must wait for auth hydration')
  token = 'after-hydration'
  readiness.markReady()
  await pending
  assert.equal(config.headers.Authorization, 'Bearer after-hydration')

  token = 'after-refresh'
  await attachCurrentAccessToken(config, {
    waitForAuthReady: readiness.waitForReady,
    getCurrentAccessToken: () => token,
    applyAuthHeader: (request, value) => { request.headers.Authorization = `Bearer ${value}` }
  })
  assert.equal(config.headers.Authorization, 'Bearer after-refresh')
})

test('all SignalR token factories obtain the current token on reconnect', () => {
  let token = 'initial'
  const factories = ['CallHub', 'ChatHub', 'NotificationHub', 'KanbanHub']
    .map(() => createCurrentAccessTokenFactory(() => token))

  assert.deepEqual(factories.map(factory => factory()), Array(4).fill('initial'))
  token = 'refreshed'
  assert.deepEqual(factories.map(factory => factory()), Array(4).fill('refreshed'))
})

test('parallel 401 retries share one refresh and retry only with its token', async () => {
  let refreshCalls = 0
  const retriedWith = []
  const coordinator = createRefreshCoordinator({
    refreshAccessToken: async () => {
      refreshCalls += 1
      await Promise.resolve()
      return 'refreshed-token'
    },
    updateAccessToken: () => {},
    handleRefreshFailure: () => assert.fail('refresh should succeed')
  })

  const responses = await Promise.all(Array.from({ length: 4 }, (_, index) =>
    coordinator.retryAfterRefresh(token => {
      retriedWith.push(token)
      return `retry-${index}`
    })
  ))

  assert.equal(refreshCalls, 1)
  assert.deepEqual(retriedWith, Array(4).fill('refreshed-token'))
  assert.deepEqual(responses, ['retry-0', 'retry-1', 'retry-2', 'retry-3'])
})

test('a failed refresh clears authentication once for every queued retry', async () => {
  let clearCalls = 0
  const coordinator = createRefreshCoordinator({
    refreshAccessToken: async () => { throw new Error('refresh denied') },
    updateAccessToken: () => assert.fail('token must not be updated after refresh failure'),
    handleRefreshFailure: () => { clearCalls += 1 }
  })

  const outcomes = await Promise.allSettled([
    coordinator.retryAfterRefresh(() => assert.fail('retry must not run')),
    coordinator.retryAfterRefresh(() => assert.fail('retry must not run')),
    coordinator.retryAfterRefresh(() => assert.fail('retry must not run'))
  ])

  assert.equal(clearCalls, 1)
  assert.deepEqual(outcomes.map(outcome => outcome.status), ['rejected', 'rejected', 'rejected'])
})

test('logout prevents automatic SignalR reconnect attempts', () => {
  let token = 'active-token'
  const policy = createTokenAwareReconnectPolicy(() => token, [0, 2000, 10000])

  assert.equal(policy.nextRetryDelayInMilliseconds({ previousRetryCount: 0 }), 0)
  assert.equal(policy.nextRetryDelayInMilliseconds({ previousRetryCount: 1 }), 2000)
  token = ''
  assert.equal(policy.nextRetryDelayInMilliseconds({ previousRetryCount: 2 }), null)
})
