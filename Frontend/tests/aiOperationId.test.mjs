import assert from 'node:assert/strict'
import test from 'node:test'

import { ensureAiOperationId, isBillableAiRequest } from '../src/utils/aiOperationId.js'

test('billable AI request receives one client operation ID and retry preserves it', () => {
  const request = { url: '/ai/generate-description', headers: {} }
  ensureAiOperationId(request)
  const first = request.headers['X-AI-Operation-Id']
  ensureAiOperationId(request)
  assert.match(first, /^[0-9a-f-]{36}$/i)
  assert.equal(request.headers['X-AI-Operation-Id'], first)
})

test('identical prompts are independent when callers provide different operation IDs', () => {
  const first = { url: '/ai/chat', headers: { 'X-AI-Operation-Id': 'operation-a' } }
  const second = { url: '/ai/chat', headers: { 'X-AI-Operation-Id': 'operation-b' } }
  ensureAiOperationId(first)
  ensureAiOperationId(second)
  assert.notEqual(first.headers['X-AI-Operation-Id'], second.headers['X-AI-Operation-Id'])
})

test('non-billable AI reads do not create a charge identity', () => {
  const request = { url: '/ai/usage-summary', headers: {} }
  ensureAiOperationId(request)
  assert.equal(isBillableAiRequest(request.url), false)
  assert.deepEqual(request.headers, {})
})
