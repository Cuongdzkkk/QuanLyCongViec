import assert from 'node:assert/strict'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { createSSRApp, h } from 'vue'
import { renderToString } from '@vue/server-renderer'
import { createServer } from 'vite'
import {
  normalizeTranscriptChunkEvent,
  upsertLiveCaptionFinal,
  upsertLiveCaptionInterim,
  upsertTranscriptHistory
} from '../src/services/liveCaptionState.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const frontendRoot = path.resolve(here, '..')
const vite = await createServer({
  root: frontendRoot,
  appType: 'custom',
  logLevel: 'silent',
  server: { middlewareMode: true }
})

try {
  const { default: LiveCaptionOverlay } = await vite.ssrLoadModule('/src/components/collaboration/LiveCaptionOverlay.vue')
  const renderOverlay = captions => renderToString(createSSRApp({
    render: () => h(LiveCaptionOverlay, { enabled: true, captions })
  }))
  const event = (text, overrides = {}) => ({
    callSessionId: '11111111-1111-1111-1111-111111111111',
    speakerUserId: '22222222-2222-2222-2222-222222222222',
    speakerDisplayName: 'An Nguyen',
    startedAt: '2026-08-30T00:00:00.000Z',
    endedAt: '2026-08-30T00:00:00.250Z',
    text,
    confidence: 0.95,
    ...overrides
  })

  assert.doesNotMatch(await renderOverlay([]), /call-live-caption-dock/)

  let liveRows = upsertLiveCaptionInterim([], event('interim fixture'), 1000)
  let html = await renderOverlay(liveRows)
  assert.match(html, /call-live-caption-dock/)
  assert.match(html, /is-interim/)
  assert.match(html, /An Nguyen/)
  assert.match(html, /interim fixture/)

  liveRows = upsertLiveCaptionInterim(liveRows, event('updated interim fixture'), 1100)
  html = await renderOverlay(liveRows)
  assert.doesNotMatch(html, />interim fixture</)
  assert.match(html, /updated interim fixture/)

  const finalEvent = event('final fixture', { id: '33333333-3333-3333-3333-333333333333' })
  liveRows = upsertLiveCaptionFinal(liveRows, finalEvent, 1200)
  html = await renderOverlay(liveRows)
  assert.match(html, /final fixture/)
  assert.doesNotMatch(html, /is-interim/)

  let history = upsertTranscriptHistory([], finalEvent)
  history = upsertTranscriptHistory(history, finalEvent)
  assert.equal(history.length, 1)
  assert.equal(normalizeTranscriptChunkEvent(history[0]).text, 'final fixture')

  for (const index of [2, 3, 4]) {
    liveRows = upsertLiveCaptionFinal(liveRows, event(`final ${index}`, {
      id: `33333333-3333-3333-3333-33333333333${index}`,
      speakerUserId: `speaker-${index}`,
      speakerDisplayName: `Speaker ${index}`
    }), 1200 + index)
  }
  html = await renderOverlay(liveRows)
  assert.equal((html.match(/call-live-caption-row/g) || []).length, 3)

  console.log('LIVE_CAPTION_SSR: exact interim/final payload updates Vue-rendered overlay and final history without duplicates')
} finally {
  await vite.close()
}
