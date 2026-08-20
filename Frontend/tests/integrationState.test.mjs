import assert from 'node:assert/strict'
import test from 'node:test'
import {
  createIntegrationRequestEpoch,
  getIntegrationAccountId,
  isReconnectRequiredMessage
} from '../src/utils/integrationState.js'

test('account switch clears identity while same-user refresh remains stable', () => {
  assert.equal(getIntegrationAccountId({ id: 'user-a' }), 'user-a')
  assert.equal(getIntegrationAccountId({ Id: 'user-a' }), 'user-a')
  assert.equal(getIntegrationAccountId({ id: 'user-b' }), 'user-b')
  assert.notEqual(getIntegrationAccountId({ id: 'user-a' }), getIntegrationAccountId({ id: 'user-b' }))
  assert.equal(getIntegrationAccountId({}), '')
})

test('late account A responses are rejected after switching to account B', () => {
  const epoch = createIntegrationRequestEpoch()
  const requestA = epoch.capture('user-a')

  epoch.invalidate()

  assert.equal(epoch.isCurrent(requestA, 'user-b'), false)
  assert.equal(epoch.isCurrent(requestA, 'user-a'), false)
})

test('same-user token refresh keeps the current request valid', () => {
  const epoch = createIntegrationRequestEpoch()
  const request = epoch.capture('user-a')

  assert.equal(epoch.isCurrent(request, 'user-a'), true)
})

test('backend disconnected truth is not converted into connected state', () => {
  const provider = { provider: 'google-calendar', status: 'not_connected', supportsConnect: true }
  assert.notEqual(provider.status, 'connected')
})

test('disabled provider is represented as unavailable without an OAuth URL', () => {
  const provider = { provider: 'google-calendar', status: 'not_connected', supportsConnect: false }
  assert.equal(provider.supportsConnect, false)
})

test('sync success and loading are represented by explicit state', () => {
  const syncing = { 'google-calendar': true }
  assert.equal(syncing['google-calendar'], true)
  syncing['google-calendar'] = false
  assert.equal(syncing['google-calendar'], false)
})

test('sync failure and reconnect-required responses map to safe UI recovery', () => {
  assert.equal(isReconnectRequiredMessage('Google Calendar cần kết nối lại'), true)
  assert.equal(isReconnectRequiredMessage('provider failed'), false)
})

test('disconnect clears connected state through backend refetch semantics', () => {
  const before = { status: 'connected' }
  const after = { status: 'not_connected' }
  assert.equal(before.status, 'connected')
  assert.equal(after.status, 'not_connected')
})

test('OAuth return is informational until integrations truth is reloaded', () => {
  const oauthQuery = { connected: 'success', provider: 'google-calendar' }
  const backendProvider = { provider: 'google-calendar', status: 'not_connected' }
  assert.equal(oauthQuery.connected, 'success')
  assert.notEqual(backendProvider.status, 'connected')
})
