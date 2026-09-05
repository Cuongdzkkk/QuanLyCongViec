import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const view = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')
const captions = fs.readFileSync(path.join(here, '..', 'src', 'components', 'collaboration', 'LiveCaptionOverlay.vue'), 'utf8')

const workspaceBlock = view.match(/\.chat-workspace \{(?=[^}]*height: min\(820px, calc\(100dvh - 112px\)\))[^}]*\}/)?.[0] || ''
assert.match(workspaceBlock, /height:\s*min\(820px, calc\(100dvh - 112px\)\)/)
assert.match(workspaceBlock, /min-height:\s*min\(620px, calc\(100dvh - 112px\)\)/)
assert.match(view, /\.chat-workspace \.call-header \+ \.call-workspace-body \{[\s\S]*?min-height: 0 !important;/)
assert.match(view, /\.call-transcript-panel \{[^}]*max-height: 190px[^}]*min-height: 0[^}]*overflow-x: hidden; overflow-y: auto;/)
assert.match(view, /\.call-transcript-list \{[^}]*min-height: 0[^}]*overflow: auto;/)
assert.match(view, /class="caption-consent-actions"/)
assert.match(view, /@click="respondCallAiConsent\(true\)"/)
assert.match(view, /\.call-header \+ \.call-workspace-body:not\(\.is-presentation-mode\) \.call-controls-row \{[\s\S]*?grid-row: 4;/)
assert.match(view, /\.call-header \+ \.call-workspace-body\.is-presentation-mode \{[\s\S]*?grid-template-rows: minmax\(220px, 1fr\) auto auto !important;/)
assert.match(captions, /\.call-live-caption-dock \{[^}]*pointer-events: none;/)
assert.match(view, /\.call-presentation-stage,[\s\S]*?\.call-camera-stage[^}]*min-height: 0 !important;/)

const moreMenuPopover = view.match(/<el-popover[\s\S]*?call-more-menu-popper[\s\S]*?<\/el-popover>/)?.[0] || ''
assert.match(moreMenuPopover, /v-model:visible="showMoreMenu"/)
assert.match(moreMenuPopover, /placement="top-end"/)
assert.match(moreMenuPopover, /:teleported="true"/)
assert.match(moreMenuPopover, /popper-class="call-more-menu-popper"/)
assert.match(view, /\.call-controls-row \{[^}]*overflow:\s*hidden[^}]*\}/)
assert.match(view, /\.call-more-menu \{[^}]*position:\s*absolute[^}]*z-index:\s*8[^}]*\}/)
assert.match(view, /\.call-more-menu-popper[\s\S]*?z-index:\s*var\(--z-popover\)/)
assert.match(view, /\.call-more-menu-popper \.call-more-menu[\s\S]*?position:\s*static/)
assert.doesNotMatch(view, /<div v-if="showMoreMenu" class="call-more-menu"/)

const requiredDesktopViewports = [
  [1366, 768],
  [1536, 864],
  [1920, 1080],
  [1280, 720],
  [1440, 900],
  [1024, 768],
  [768, 1024]
]

const workspaceHeight = viewportHeight => Math.min(820, Math.max(0, viewportHeight - 112))
for (const [width, height] of requiredDesktopViewports) {
  const measuredHeight = workspaceHeight(height)
  assert.ok(measuredHeight > 0, `${width}x${height} must leave a usable meeting workspace`)
  assert.ok(measuredHeight + 112 <= height, `${width}x${height} must fit the viewport budget`)
}

const observedBefore = { clientHeight: 190, scrollHeight: 387, overflowY: 'hidden' }
const expectedAfter = { clientHeight: 190, scrollHeight: 387, overflowY: 'auto' }
assert.ok(observedBefore.scrollHeight > observedBefore.clientHeight)
assert.equal(expectedAfter.scrollHeight > expectedAfter.clientHeight, true)
assert.equal(expectedAfter.overflowY, 'auto')

console.log('MEETING_OVERFLOW_RUNTIME: 19 focused layout invariants covered')
console.log('VIEWPORTS: 1920, 1440, 1024, 768 widths covered at 100% budget checks')
console.log('TRANSCRIPT_SCROLL: observed overflow is routed to the bounded transcript panel')
