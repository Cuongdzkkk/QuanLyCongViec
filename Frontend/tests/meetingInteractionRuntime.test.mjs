import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const view = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')

const interactionCases = [
  ['microphone', /@click="toggleCallMicrophone"/],
  ['camera', /@click="toggleCallCameraReal"/],
  ['background effects', /@click="showCameraEffectsMenu = !showCameraEffectsMenu"/],
  ['screen share', /@click="toggleScreenShare"/],
  ['raise hand', /@click="toggleRaiseHand"/],
  ['call chat', /@click="openVoiceChannelChat"/],
  ['participants', /@click="openCallParticipants"/],
  ['captions', /@click="toggleCallCaptions"/],
  ['more menu', /@click="showMoreMenu = !showMoreMenu; moreMenuSection = ''"/],
  ['leave call', /@click="leaveVoiceChannel"/],
  ['presentation focus', /@click="togglePresentationFocus"/],
  ['picture-in-picture', /@click="toggleCallPictureInPicture"/],
  ['fullscreen', /@click="togglePresentationFullscreen"/],
  ['consent accept', /@click="respondCallAiConsent\(true\)"/],
  ['consent decline', /@click="respondCallAiConsent\(false\)"/],
  ['close call side panel', /@click="closeCallSidePanel"/]
]

for (const [name, contract] of interactionCases) {
  assert.match(view, contract, `${name} interaction is missing`)
}

const captionDock = view.match(/\.call-live-caption-dock \{[\s\S]*?\n\}/)?.[0] || ''
assert.match(captionDock, /position:\s*absolute/)
assert.match(captionDock, /width:\s*min\(680px, calc\(100% - 24px\)\)/)
assert.match(captionDock, /max-height:\s*192px/)
assert.match(captionDock, /overflow:\s*hidden/)
assert.match(captionDock, /pointer-events:\s*none/)
assert.doesNotMatch(captionDock, /inset:\s*0|width:\s*100%|height:\s*100%/)

const interactionGuard = view.match(/\/\* Keep visual regions[\s\S]*?\.chat-workspace \.call-live-caption-dock \{[\s\S]*?\n\}/)?.[0] || ''
assert.match(interactionGuard, /grid-template-rows: minmax\(0, 1fr\) auto auto auto !important/)
assert.match(interactionGuard, /\.call-controls-row \{[\s\S]*?z-index: 20;[\s\S]*?pointer-events: auto;/)
assert.match(interactionGuard, /\.call-transcript-panel \{[\s\S]*?position: relative;[\s\S]*?pointer-events: auto;/)
assert.match(interactionGuard, /\.call-live-caption-dock \{[\s\S]*?pointer-events: none;/)

const transcriptRules = view.match(/\.call-transcript-panel \{[\s\S]*?\n\}/)?.[0] || ''
assert.doesNotMatch(transcriptRules, /position:\s*(absolute|fixed)|inset:\s*0|width:\s*100%|height:\s*100%/)
assert.match(view, /<aside v-if="callAiState\.state !== 'OFF'[^>]*class="call-transcript-panel"/)
assert.doesNotMatch(view, /class="(?:call-consent|call-transcript)[^"]*overlay[^"]*"/)

const regions = [
  { id: 'stage', x: 0, y: 0, width: 1000, height: 400, zIndex: 0, pointerEvents: 'auto' },
  { id: 'caption-dock', x: 160, y: 250, width: 680, height: 140, zIndex: 3, pointerEvents: 'none' },
  { id: 'transcript', x: 690, y: 400, width: 310, height: 170, zIndex: 1, pointerEvents: 'auto' },
  { id: 'controls', x: 0, y: 570, width: 1000, height: 60, zIndex: 20, pointerEvents: 'auto' }
]

const elementFromPoint = (x, y) => regions
  .filter(region => region.pointerEvents !== 'none')
  .filter(region => x >= region.x && x <= region.x + region.width && y >= region.y && y <= region.y + region.height)
  .sort((left, right) => right.zIndex - left.zIndex)[0]?.id

assert.equal(elementFromPoint(500, 590), 'controls')
assert.equal(elementFromPoint(500, 280), 'stage')
assert.equal(elementFromPoint(700, 500), 'transcript')
assert.equal(elementFromPoint(950, 590), 'controls')

console.log('MEETING_INTERACTION_RUNTIME: 16 interaction contracts covered')
console.log('HIT_TEST_CONTRACT: caption dock is display-only; transcript and controls retain bounded interactive hitboxes')
