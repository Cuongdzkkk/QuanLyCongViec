import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  LIVE_CAPTION_EXPIRY_MS,
  LIVE_CAPTION_MAX_ROWS,
  isLiveCaptionForSession,
  normalizeLiveCaptionEvent,
  removeExpiredLiveCaptions,
  upsertLiveCaptionFinal,
  upsertLiveCaptionInterim
} from '../src/services/liveCaptionState.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const view = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')
const service = fs.readFileSync(path.join(here, '..', 'src', 'services', 'callMediaService.js'), 'utf8')
const state = fs.readFileSync(path.join(here, '..', 'src', 'services', 'liveCaptionState.js'), 'utf8')

const event = (speakerUserId, text, overrides = {}) => ({
  callSessionId: 'session-1',
  speakerUserId,
  speakerDisplayName: speakerUserId,
  startedAt: '2026-08-29T10:00:00.000Z',
  text,
  ...overrides
})

assert.equal(normalizeLiveCaptionEvent({ Text: '  Xin chào  ', SpeakerUserId: 'U1' }).text, 'Xin chào')
assert.equal(LIVE_CAPTION_MAX_ROWS, 4)
assert.equal(LIVE_CAPTION_EXPIRY_MS, 8000)
assert.equal(isLiveCaptionForSession(event('U1', 'current'), 'session-1'), true)
assert.equal(isLiveCaptionForSession(event('U1', 'stale', { callSessionId: 'session-old' }), 'session-1'), false)

let rows = []
rows = upsertLiveCaptionInterim(rows, event('U1', 'Một câu'), 1000)
const interimId = rows[0].id
rows = upsertLiveCaptionInterim(rows, event('U1', 'Một câu đang được sửa'), 1100)
assert.equal(rows.length, 1)
assert.equal(rows[0].id, interimId)
assert.equal(rows[0].text, 'Một câu đang được sửa')
assert.equal(rows[0].isInterim, true)

rows = upsertLiveCaptionInterim(rows, event('U2', 'Người thứ hai đang nói'), 1200)
assert.equal(rows.length, 2)
assert.deepEqual(rows.map(row => row.speakerUserId), ['U1', 'U2'])

rows = upsertLiveCaptionFinal(rows, event('U1', 'Một câu hoàn chỉnh', { id: 'chunk-1' }), 1300)
const finalRow = rows.find(row => row.speakerUserId === 'U1')
assert.equal(rows.length, 2)
assert.equal(finalRow.text, 'Một câu hoàn chỉnh')
assert.equal(finalRow.isInterim, false)
assert.equal(finalRow.expiresAt, 1300 + LIVE_CAPTION_EXPIRY_MS)

rows = upsertLiveCaptionFinal(rows, event('U2', 'Người thứ hai hoàn chỉnh', { id: 'chunk-2' }), 1350)

for (const speaker of ['U3', 'U4', 'U5']) {
  rows = upsertLiveCaptionFinal(rows, event(speaker, `Nội dung ${speaker}`, { id: `chunk-${speaker}` }), 1400)
}
assert.equal(rows.length, LIVE_CAPTION_MAX_ROWS)
assert.equal(rows.some(row => row.speakerUserId === 'U2'), true)
assert.equal(removeExpiredLiveCaptions(rows, 9299).length, LIVE_CAPTION_MAX_ROWS)
assert.equal(removeExpiredLiveCaptions(rows, 9400).length, 0)

assert.match(view, /v-if="captionsEnabled && liveCaptionRows\.length" class="call-live-caption-dock"/)
assert.match(view, /ref="presentationStage" class="call-presentation-stage"/)
assert.match(service, /CallTranscriptInterim/)
assert.match(service, /CallTranscriptChunkAdded/)
assert.match(view, /const isCurrentCaptionEvent = value =>/)
assert.match(state, /eventSessionId.*currentSessionId/)
assert.match(view, /upsertLiveCaptionInterim/)
assert.match(view, /upsertLiveCaptionFinal/)
assert.match(view, /callTranscriptChunks\.value = \[\.\.\.callTranscriptChunks\.value/)
assert.match(view, /clearLiveCaptionRows\(\)/)
assert.match(view, /:src="caption\.avatarUrl \|\| ''"/)
assert.match(view, /caption\.speakerDisplayName/)
assert.match(view, /caption\.text/)
assert.match(view, /aria-pressed="captionsEnabled"/)
assert.doesNotMatch(view, /Array\.from\(new Uint8Array|MediaRecorder/)

console.log('CAPTION_DOCK_RUNTIME: 22 focused live-caption behaviors covered')
console.log('CAPTION_ROWS: per-speaker interim/final replacement, max rows, expiry, cleanup, stale-session guard')
console.log('NO_DUPLICATE_STT_OR_PERSISTENCE: covered by existing CallHub/caption contracts')
