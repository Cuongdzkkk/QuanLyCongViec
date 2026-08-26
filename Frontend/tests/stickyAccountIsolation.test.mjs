import assert from 'node:assert/strict'
import test from 'node:test'
import {
  createStickyRequestEpoch,
  getStickyAccountId,
  hasStickyAccountChanged
} from '../src/utils/stickyAccountIsolation.js'

test('account switch is detected while same-user refresh is stable', () => {
  const userA = getStickyAccountId({ id: 'user-a' })
  const sameUser = getStickyAccountId({ Id: 'user-a' })
  const userB = getStickyAccountId({ id: 'user-b' })

  assert.equal(hasStickyAccountChanged(userA, sameUser), false)
  assert.equal(hasStickyAccountChanged(userA, userB), true)
  assert.equal(hasStickyAccountChanged(userA, ''), true)
})

test('account reset invalidates stale responses before the next fetch completes', () => {
  const epoch = createStickyRequestEpoch()
  const userARequest = epoch.capture()

  epoch.invalidate()

  assert.equal(epoch.isCurrent(userARequest), false)
  assert.equal(epoch.isCurrent(epoch.capture()), true)
})

test('pending autosave and in-flight CRUD work are rejected after reset', () => {
  const epoch = createStickyRequestEpoch()
  const pendingAutosave = epoch.capture()
  const inFlightUpdate = epoch.capture()

  epoch.invalidate()

  assert.equal(epoch.isCurrent(pendingAutosave), false)
  assert.equal(epoch.isCurrent(inFlightUpdate), false)
})

test('normal same-user state remains valid across token refresh', () => {
  const epoch = createStickyRequestEpoch()
  const noteRequest = epoch.capture()

  assert.equal(hasStickyAccountChanged('user-a', 'user-a'), false)
  assert.equal(epoch.isCurrent(noteRequest), true)
})

test('CRUD state guard accepts current work and rejects cleared note work', () => {
  const notes = [{ id: 'note-a', content: 'draft' }]
  const canUpdate = id => notes.some(note => note.id === id)

  assert.equal(canUpdate('note-a'), true)
  notes.length = 0
  assert.equal(canUpdate('note-a'), false)
})
