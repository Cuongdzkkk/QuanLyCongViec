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
const remoteCameraOff = participant('U2', 'C2', { cameraEnabled: false })
const threeUsers = [...twoUsers, participant('U3', 'C3')]

const oneDefaultMode = getMeetingLayoutMode({
  hasPresenter: false,
  presentationFocused: false,
  focusedParticipantId: '',
  participantCount: 1
})
assert.equal(oneDefaultMode, 'CAMERA_GRID')
const oneDefaultRender = getMeetingRenderCollections({
  mode: oneDefaultMode,
  participantsInCall: [oneUser]
})
assert.equal(oneDefaultRender.cameraStageParticipants.length, 1)
assert.equal(oneDefaultRender.cameraRailParticipants.length, 0)

const twoDefaultMode = getMeetingLayoutMode({
  hasPresenter: false,
  presentationFocused: false,
  focusedParticipantId: '',
  participantCount: 2
})
assert.equal(twoDefaultMode, 'CAMERA_GRID')
const twoDefaultRender = getMeetingRenderCollections({
  mode: twoDefaultMode,
  participantsInCall: twoUsers
})
assert.deepEqual(twoDefaultRender.cameraStageParticipants.map(item => item.userId), ['U1', 'U2'])
assert.equal(twoDefaultRender.cameraStageParticipants.length, 2)
assert.equal(twoDefaultRender.cameraRailParticipants.length, 0)

const oneFocusRender = getMeetingRenderCollections({
  mode: 'CAMERA_FOCUS',
  participantsInCall: [oneUser],
  focusedParticipantId: 'C1'
})
assert.deepEqual(oneFocusRender.cameraStageParticipants.map(item => item.userId), ['U1'])
assert.equal(oneFocusRender.cameraRailParticipants.length, 0)
assert.deepEqual(getMeetingVisualRegions('CAMERA_FOCUS'), ['camera-stage', 'participant-rail'])

const twoFocusRender = getMeetingRenderCollections({
  mode: 'CAMERA_FOCUS',
  participantsInCall: twoUsers,
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
  participantCount: 2
})
const presentationRender = getMeetingRenderCollections({
  mode: presentationMode,
  participantsInCall: twoUsers
})
assert.equal(presentationMode, 'PRESENTATION')
assert.equal(presentationRender.cameraStageParticipants.length, 0)
assert.deepEqual(presentationRender.presentationRailParticipants.map(item => item.userId), ['U1', 'U2'])
assert.deepEqual(getMeetingVisualRegions(presentationMode), ['presentation-stage', 'participant-rail'])

const stoppedPresentationRender = getMeetingRenderCollections({
  mode: 'CAMERA_GRID',
  participantsInCall: [oneUser, remoteCameraOff]
})
assert.equal(stoppedPresentationRender.cameraStageParticipants.length, 2)
assert.deepEqual(stoppedPresentationRender.cameraStageParticipants.map(item => item.userId), ['U1', 'U2'])
assert.equal(stoppedPresentationRender.presentationRailParticipants.length, 0)

const cameraOffRender = getMeetingRenderCollections({
  mode: 'CAMERA_GRID',
  participantsInCall: [oneUser, remoteCameraOff]
})
assert.equal(cameraOffRender.cameraStageParticipants.length, 2)
assert.equal(cameraOffRender.cameraStageParticipants.some(item => item.userId === 'U2'), true)

const threeParticipantRender = getMeetingRenderCollections({
  mode: 'CAMERA_GRID',
  participantsInCall: threeUsers
})
assert.deepEqual(threeParticipantRender.cameraStageParticipants.map(item => item.userId), ['U1', 'U2', 'U3'])
assert.equal(threeParticipantRender.cameraRailParticipants.length, 0)

const duplicateUser = dedupeParticipantsByUser([
  { ...oneUser, cameraStream: { id: 'STREAM1' }, videoTrack: { id: 'TRACK1' } },
  { ...oneUser, connectionId: 'reconnected-C1', cameraStream: { id: 'STREAM1' }, videoTrack: { id: 'TRACK1' } }
], 'C1')
assert.equal(duplicateUser.length, 1)

const productionEvidenceRender = getMeetingRenderCollections({
  mode: 'CAMERA_FOCUS',
  participantsInCall: duplicateUser,
  focusedParticipantId: 'C1'
})
const renderedCameraSources = [
  ...productionEvidenceRender.cameraStageParticipants,
  ...productionEvidenceRender.cameraRailParticipants
].map(item => `${item.cameraStream?.id || ''}/${item.videoTrack?.id || ''}`)
assert.deepEqual(renderedCameraSources, ['STREAM1/TRACK1'])

assert.match(view, /v-for="user in cameraStageParticipants"/)
assert.match(view, /v-else-if="hasCallParticipants" class="call-camera-stage"/)
assert.match(view, /v-else class="call-camera-stage-avatar"/)
assert.match(view, /user\.connectionId !== callConnectionId && isParticipantVideoVisible\(user\)/)
assert.match(view, /v-if="callRailParticipants\.length" class="call-participant-rail"/)
assert.match(view, /v-for="user in callRailParticipants"/)
assert.doesNotMatch(view, /v-show="isParticipantStageVisible\(user\)"/)
assert.match(view, /const cameraStageParticipants = computed\(\(\) => meetingRenderCollections\.value\.cameraStageParticipants\)/)
assert.match(view, /const callRailParticipants = computed\(\(\) => \[/)
assert.match(view, /getMeetingRenderCollections/)
assert.match(view, /const participantsInCall = computed\(\(\) => dedupeParticipantsByUser/)
assert.doesNotMatch(view, /participantsInCall\.value\.filter\(isParticipantVideoVisible\)/)
assert.match(view, /focusedParticipantConnectionId\.value = ''/)
assert.match(view, /ref="meetingShell" class="call-workspace-body"/)
assert.match(view, /document\.fullscreenElement === meetingShell\.value/)
assert.match(view, /requestFullscreen\(\)/)
assert.match(view, /@click="openVoiceChannelChat"/)
assert.match(view, /call-fullscreen-panel/)
assert.match(view, /callHandRaised \? 'Hạ tay' : 'Giơ tay'/)
assert.match(view, /callTranscriptionCapabilities\.configured/)
assert.match(view, /@click="toggleCallCaptions"/)
assert.match(view, /class="call-live-caption"/)
assert.match(view, /:data-participant-count="cameraStageParticipants\.length"/)
assert.match(view, /\.call-camera-stage\[data-participant-count="1"\]/)
assert.match(view, /\.call-camera-stage\[data-participant-count="2"\]/)
assert.match(view, /\.call-camera-stage\[data-participant-count="3"\]/)
assert.match(view, /\.call-camera-stage\[data-participant-count="4"\]/)
assert.match(view, /\.call-workspace-body\.has-call-side-panel/)
assert.match(view, /call-reaction-option/)
assert.match(view, /const callViewModes =/)
assert.match(view, /\['spotlight', 'sidebar'\]/)
assert.match(view, /callViewMode\.value === 'tiled'/)

console.log('TWO_PARTICIPANTS_NO_SHARE_SHOWS_BOTH: covered')
console.log('REMOTE_CAMERA_OFF_STILL_SHOWS_REMOTE_TILE: covered')
console.log('REMOTE_CAMERA_ON_SHOWS_REMOTE_VIDEO: covered')
console.log('SCREEN_SHARE_SHOWS_PARTICIPANT_RAIL: covered')
console.log('STOP_SHARE_RETURNS_TO_TWO_PERSON_GRID: covered')
console.log('LOCAL_USER_DOES_NOT_HIDE_REMOTE_PARTICIPANT: covered')
console.log('THREE_PARTICIPANTS_RENDER_GRID: covered')
console.log('meetingLayoutRuntime.test.mjs: render collections prevent duplicate stage/thumb cameras and preserve presentation rails')
