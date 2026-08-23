import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  dedupeParticipantsByUser,
  getMeetingLayoutMode,
  getMeetingVisualRegions
} from '../src/services/meetingLayoutState.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const view = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')

const oneUserWithCameraAndScreen = dedupeParticipantsByUser([
  { userId: 'user-a', connectionId: 'connection-a', cameraEnabled: true, screenSharing: false },
  { userId: 'user-a', connectionId: 'connection-a', cameraEnabled: true, screenSharing: true }
])
assert.equal(oneUserWithCameraAndScreen.length, 1)
assert.equal(getMeetingLayoutMode({ hasPresenter: false, presentationFocused: false, focusedParticipantId: '', visibleParticipantCount: 1 }), 'CAMERA_FOCUS')
assert.deepEqual(getMeetingVisualRegions('CAMERA_FOCUS'), ['camera-stage'])

const twoUsers = dedupeParticipantsByUser([
  { userId: 'user-a', connectionId: 'connection-a' },
  { userId: 'user-b', connectionId: 'connection-b' }
])
assert.equal(twoUsers.length, 2)
assert.equal(getMeetingLayoutMode({ hasPresenter: false, presentationFocused: false, focusedParticipantId: '', visibleParticipantCount: 2 }), 'CAMERA_GRID')
assert.equal(getMeetingLayoutMode({ hasPresenter: false, presentationFocused: false, focusedParticipantId: 'connection-a', visibleParticipantCount: 2 }), 'CAMERA_FOCUS')
assert.deepEqual(getMeetingVisualRegions('CAMERA_GRID'), ['camera-stage'])

assert.equal(getMeetingLayoutMode({ hasPresenter: true, presentationFocused: false, focusedParticipantId: '', visibleParticipantCount: 2 }), 'PRESENTATION')
assert.equal(getMeetingLayoutMode({ hasPresenter: true, presentationFocused: true, focusedParticipantId: '', visibleParticipantCount: 2 }), 'PRESENTATION_FOCUS')
assert.deepEqual(getMeetingVisualRegions('PRESENTATION'), ['presentation-stage', 'participant-rail'])
assert.deepEqual(getMeetingVisualRegions('PRESENTATION_FOCUS'), ['presentation-stage', 'participant-rail'])

assert.match(view, /<section v-if="callLayoutMode\.startsWith\('PRESENTATION'\)" class="call-participant-rail"/)
assert.match(view, /ref="meetingShell" class="call-workspace-body"/)
assert.match(view, /document\.fullscreenElement === meetingShell\.value/)
assert.match(view, /requestFullscreen\(\)/)
assert.match(view, /callChatOpen = !callChatOpen/)
assert.match(view, /call-fullscreen-panel/)
assert.match(view, /callHandRaised \? 'Hạ tay' : 'Giơ tay'/)
assert.match(view, /Phụ đề <small>Chưa sẵn sàng<\/small>/)
assert.match(view, /call-reaction-option/)

console.log('meetingLayoutRuntime.test.mjs: exclusive modes, user dedupe, focus, fullscreen, controls, and truthful caption checks passed')
