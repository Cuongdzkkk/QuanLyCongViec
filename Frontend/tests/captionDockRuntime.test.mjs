import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  LIVE_CAPTION_EXPIRY_MS,
  LIVE_CAPTION_MAX_ROWS,
  isLiveCaptionForSession,
  normalizeLiveCaptionEvent,
  normalizeTranscriptChunkEvent,
  removeTranscriptInterim,
  removeExpiredLiveCaptions,
  upsertLiveCaptionFinal,
  upsertLiveCaptionInterim,
  upsertTranscriptHistory,
  upsertTranscriptInterim
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
assert.equal(LIVE_CAPTION_MAX_ROWS, 3)
assert.equal(LIVE_CAPTION_EXPIRY_MS, 3500)
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
assert.deepEqual(rows.map(row => row.speakerUserId), ['U3', 'U4', 'U5'])
assert.equal(removeExpiredLiveCaptions(rows, 4899).length, LIVE_CAPTION_MAX_ROWS)
assert.equal(removeExpiredLiveCaptions(rows, 5000).length, 0)

let transcriptHistory = upsertTranscriptHistory([], event('U1', 'Một câu hoàn chỉnh', { id: 'chunk-1' }))
transcriptHistory = upsertTranscriptHistory(transcriptHistory, event('U1', 'Một câu hoàn chỉnh', { id: 'chunk-1' }))
assert.equal(transcriptHistory.length, 1)
assert.equal(normalizeTranscriptChunkEvent(transcriptHistory[0]).text, 'Một câu hoàn chỉnh')

let transcriptInterims = upsertTranscriptInterim([], event('U1', 'A đang nói'))
transcriptInterims = upsertTranscriptInterim(transcriptInterims, event('U2', 'B đang nói'))
transcriptInterims = upsertTranscriptInterim(transcriptInterims, event('U1', 'A tiếp tục nói'))
assert.deepEqual(transcriptInterims.map(item => item.speakerUserId), ['U2', 'U1'])
assert.equal(transcriptInterims.find(item => item.speakerUserId === 'U2').text, 'B đang nói')
transcriptInterims = removeTranscriptInterim(transcriptInterims, event('U2', 'B hoàn tất'))
assert.deepEqual(transcriptInterims.map(item => item.speakerUserId), ['U1'])

assert.match(view, /<LiveCaptionOverlay :enabled="captionsEnabled" :captions="liveCaptionRows"/)
assert.match(view, /ref="presentationStage" class="call-presentation-stage"/)
assert.match(service, /CallTranscriptInterim/)
assert.match(service, /CallTranscriptChunkAdded/)
assert.equal((service.match(/connection\.on\('CallTranscriptInterim'/g) || []).length, 1)
assert.equal((service.match(/connection\.on\('CallTranscriptChunkAdded'/g) || []).length, 1)
assert.equal((service.match(/registerHandlers\(\)/g) || []).length, 1)
assert.doesNotMatch(service, /connection\.off\('CallTranscript(?:Interim|ChunkAdded)'/)
assert.match(service, /await connection\.stop\(\)/)
assert.match(view, /const isCurrentCaptionEvent = value =>/)
assert.match(state, /eventSessionId.*currentSessionId/)
assert.match(view, /upsertLiveCaptionInterim/)
assert.match(view, /upsertLiveCaptionFinal/)
assert.match(view, /callTranscriptInterims/)
assert.match(view, /upsertTranscriptInterim/)
assert.doesNotMatch(view, /callTranscriptInterim\.value/)
assert.match(view, /callTranscriptChunks\.value = upsertTranscriptHistory/)
assert.match(view, /clearLiveCaptionRows\(\)/)
assert.match(view, /import LiveCaptionOverlay/)
assert.match(view, /upsertTranscriptHistory/)
assert.match(view, /aria-pressed="captionsEnabled"/)
assert.match(view, /Bật phụ đề trực tiếp\?/)
assert.match(view, /Giọng nói trong cuộc gọi sẽ được gửi để chuyển thành văn bản trực tiếp\./)
assert.match(view, /Cho phép &amp; bật phụ đề|Cho phép & bật phụ đề/)
assert.match(view, /const toggleTranscriptPanel =/)
assert.match(view, /v-if="showTranscriptPanel" class="call-transcript-panel"/)
assert.match(service, /CAPTION_CHUNK_BYTES = 4000/)
assert.match(service, /CAPTION_MAX_PENDING_CHUNKS = 3/)
assert.match(service, /CAPTION_MAX_QUEUE_AGE_MS = 375/)
assert.match(view, /CAPTION_RENDER_DIAG/)
assert.match(service, /transcriptionQueue\.clear/)
assert.doesNotMatch(view, /Array\.from\(new Uint8Array|MediaRecorder/)

console.log('CAPTION_DOCK_RUNTIME: 26 focused live-caption behaviors covered')
console.log('CAPTION_ROWS: per-speaker interim/final replacement, max rows, expiry, cleanup, stale-session guard')
console.log('NO_DUPLICATE_STT_OR_PERSISTENCE: covered by existing CallHub/caption contracts')
