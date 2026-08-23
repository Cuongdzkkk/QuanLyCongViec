import assert from 'node:assert/strict'
import test from 'node:test'

import {
  applyAuthHeader,
  getAuthHeader,
  isAuthRequest,
  shouldRefreshUnauthorized
} from '../src/utils/authRequest.js'

test('billing requests receive the bearer header without changing AI operation IDs', () => {
  const config = { url: '/billing/me', headers: { 'X-AI-Operation-Id': 'operation-1' } }
  applyAuthHeader(config, 'test-access-token')
  assert.equal(getAuthHeader('test-access-token'), 'Bearer test-access-token')
  assert.equal(config.headers.Authorization, 'Bearer test-access-token')
  assert.equal(config.headers['X-AI-Operation-Id'], 'operation-1')
})

test('billing history is refreshable once while auth endpoints are excluded', () => {
  const error = { response: { status: 401 } }
  assert.equal(shouldRefreshUnauthorized(error, { url: '/billing/orders/history' }), true)
  assert.equal(shouldRefreshUnauthorized(error, { url: '/billing/orders/history', _retry: true }), false)
  assert.equal(isAuthRequest('/auth/refresh-token'), true)
  assert.equal(shouldRefreshUnauthorized(error, { url: '/auth/refresh-token' }), false)
})

test('refresh failure does not create a retry loop', () => {
  const error = { response: { status: 401 } }
  const request = { url: '/billing/me', _retry: true }
  assert.equal(shouldRefreshUnauthorized(error, request), false)
})
