import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  dedupeParticipantsByUser,
  getMeetingLayoutMode,
  getMeetingRenderCollections,
  getMeetingVisualRegions
} from '../src/services/meetingLayoutState.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const view = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')

const participant = (userId, connectionId, overrides = {}) => ({
  userId,
  connectionId,
  displayName: userId,
  cameraEnabled: true,
  ...overrides
})

const oneUser = participant('U1', 'C1')
const twoUsers = [oneUser, participant('U2', 'C2')]

const oneDefaultMode = getMeetingLayoutMode({
  hasPresenter: false,
  presentationFocused: false,
  focusedParticipantId: '',
  visibleParticipantCount: 1
})
assert.equal(oneDefaultMode, 'CAMERA_GRID')
const oneDefaultRender = getMeetingRenderCollections({
  mode: oneDefaultMode,
  visibleParticipants: [oneUser],
  allParticipants: [oneUser]
})
assert.equal(oneDefaultRender.cameraStageParticipants.length, 1)
assert.equal(oneDefaultRender.cameraRailParticipants.length, 0)

const twoDefaultMode = getMeetingLayoutMode({
  hasPresenter: false,
  presentationFocused: false,
  focusedParticipantId: '',
  visibleParticipantCount: 2
})
assert.equal(twoDefaultMode, 'CAMERA_GRID')
const twoDefaultRender = getMeetingRenderCollections({
  mode: twoDefaultMode,
  visibleParticipants: twoUsers,
  allParticipants: twoUsers
})
assert.deepEqual(twoDefaultRender.cameraStageParticipants.map(item => item.userId), ['U1', 'U2'])
assert.equal(twoDefaultRender.cameraStageParticipants.length, 2)
assert.equal(twoDefaultRender.cameraRailParticipants.length, 0)

const oneFocusRender = getMeetingRenderCollections({
  mode: 'CAMERA_FOCUS',
  visibleParticipants: [oneUser],
  allParticipants: [oneUser],
  focusedParticipantId: 'C1'
})
assert.deepEqual(oneFocusRender.cameraStageParticipants.map(item => item.userId), ['U1'])
assert.equal(oneFocusRender.cameraRailParticipants.length, 0)
assert.deepEqual(getMeetingVisualRegions('CAMERA_FOCUS'), ['camera-stage', 'participant-rail'])

const twoFocusRender = getMeetingRenderCollections({
  mode: 'CAMERA_FOCUS',
  visibleParticipants: twoUsers,
  allParticipants: twoUsers,
  focusedParticipantId: 'C1'
})
assert.deepEqual(twoFocusRender.cameraStageParticipants.map(item => item.userId), ['U1'])
assert.deepEqual(twoFocusRender.cameraRailParticipants.map(item => item.userId), ['U2'])
assert.equal(twoFocusRender.cameraStageParticipants.length + twoFocusRender.cameraRailParticipants.length, 2)
assert.equal(twoFocusRender.cameraRailParticipants.some(item => item.userId === 'U1'), false)

const presentationMode = getMeetingLayoutMode({
  hasPresenter: true,
  presentationFocused: false,
  focusedParticipantId: '',
  visibleParticipantCount: 2
})
const presentationRender = getMeetingRenderCollections({
  mode: presentationMode,
  visibleParticipants: twoUsers,
  allParticipants: twoUsers
})
assert.equal(presentationMode, 'PRESENTATION')
assert.equal(presentationRender.cameraStageParticipants.length, 0)
assert.deepEqual(presentationRender.presentationRailParticipants.map(item => item.userId), ['U1', 'U2'])
assert.deepEqual(getMeetingVisualRegions(presentationMode), ['presentation-stage', 'participant-rail'])

const stoppedPresentationRender = getMeetingRenderCollections({
  mode: 'CAMERA_GRID',
  visibleParticipants: twoUsers,
  allParticipants: twoUsers
})
assert.equal(stoppedPresentationRender.cameraStageParticipants.length, 2)
assert.equal(stoppedPresentationRender.presentationRailParticipants.length, 0)

const duplicateUser = dedupeParticipantsByUser([
  { ...oneUser, cameraStream: { id: 'STREAM1' }, videoTrack: { id: 'TRACK1' } },
  { ...oneUser, connectionId: 'reconnected-C1', cameraStream: { id: 'STREAM1' }, videoTrack: { id: 'TRACK1' } }
], 'C1')
assert.equal(duplicateUser.length, 1)

const productionEvidenceRender = getMeetingRenderCollections({
  mode: 'CAMERA_FOCUS',
  visibleParticipants: duplicateUser,
  allParticipants: duplicateUser,
  focusedParticipantId: 'C1'
})
const renderedCameraSources = [
  ...productionEvidenceRender.cameraStageParticipants,
  ...productionEvidenceRender.cameraRailParticipants
].map(item => `${item.cameraStream?.id || ''}/${item.videoTrack?.id || ''}`)
assert.deepEqual(renderedCameraSources, ['STREAM1/TRACK1'])

assert.match(view, /v-for="user in cameraStageParticipants"/)
assert.match(view, /v-if="callRailParticipants\.length" class="call-participant-rail"/)
assert.match(view, /v-for="user in callRailParticipants"/)
assert.doesNotMatch(view, /v-show="isParticipantStageVisible\(user\)"/)
assert.match(view, /const cameraStageParticipants = computed\(\(\) => meetingRenderCollections\.value\.cameraStageParticipants\)/)
assert.match(view, /const callRailParticipants = computed\(\(\) => \[/)
assert.match(view, /getMeetingRenderCollections/)
assert.match(view, /focusedParticipantConnectionId\.value = ''/)
assert.match(view, /ref="meetingShell" class="call-workspace-body"/)
assert.match(view, /document\.fullscreenElement === meetingShell\.value/)
assert.match(view, /requestFullscreen\(\)/)
assert.match(view, /@click="openVoiceChannelChat"/)
assert.match(view, /call-fullscreen-panel/)
assert.match(view, /callHandRaised \? 'Hạ tay' : 'Giơ tay'/)
assert.match(view, /Phụ đề <small>Chưa sẵn sàng<\/small>/)
assert.match(view, /call-reaction-option/)

console.log('meetingLayoutRuntime.test.mjs: render collections prevent duplicate stage/thumb cameras and preserve presentation rails')
