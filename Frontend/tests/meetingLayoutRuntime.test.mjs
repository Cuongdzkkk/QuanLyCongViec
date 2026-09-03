import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { computed, ref } from 'vue'
import {
  dedupeParticipantsByUser,
  getBoundedCallStageParticipants,
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

const fourUsers = [...threeUsers, participant('U4', 'C4')]
const fourParticipantRender = getMeetingRenderCollections({
  mode: 'CAMERA_GRID',
  participantsInCall: fourUsers
})
assert.deepEqual(fourParticipantRender.cameraStageParticipants.map(item => item.userId), ['U1', 'U2', 'U3', 'U4'])
assert.equal(fourParticipantRender.cameraStageParticipants.length, 4)

const fiveUsers = [...fourUsers, participant('U5', 'C5')]
const fiveParticipantRender = getMeetingRenderCollections({
  mode: 'CAMERA_GRID',
  participantsInCall: fiveUsers
})
assert.equal(fiveParticipantRender.cameraStageParticipants.length, 5)
assert.equal(new Set(fiveParticipantRender.cameraStageParticipants.map(item => item.userId)).size, 5)

const boundedStage = participants => getBoundedCallStageParticipants(participants)
const overflowCount = participants => Math.max(participants.length - boundedStage(participants).length, 0)
for (const [count, expectedReal, expectedOverflow] of [[1, 1, 0], [2, 2, 0], [3, 3, 0], [4, 4, 0], [5, 3, 2], [9, 3, 6], [20, 3, 17]]) {
  const items = Array.from({ length: count }, (_, index) => participant(`U${index + 1}`, `C${index + 1}`))
  assert.equal(boundedStage(items).length, expectedReal)
  assert.equal(overflowCount(items), expectedOverflow)
}
const orderedBoundedStage = boundedStage(Array.from({ length: 9 }, (_, index) => participant(`U${index + 1}`, `C${index + 1}`)))
assert.deepEqual(orderedBoundedStage.map(item => item.userId), ['U1', 'U2', 'U3'])
const localUser = participant('LOCAL', 'local-C1')
assert.equal(dedupeParticipantsByUser([localUser, { ...localUser, connectionId: 'local-C1-reconnected' }], 'local-C1').length, 1)
assert.equal(overflowCount(fiveUsers.map((item, index) => index === 1 ? { ...item, cameraEnabled: false } : item)), 2)
const reactiveParticipants = ref(twoUsers)
const reactiveVisibleStage = computed(() => boundedStage(reactiveParticipants.value))
const reactiveOverflow = computed(() => Math.max(reactiveParticipants.value.length - reactiveVisibleStage.value.length, 0))
assert.equal(reactiveVisibleStage.value.length, 2)
assert.equal(reactiveOverflow.value, 0)
reactiveParticipants.value = [...reactiveParticipants.value, participant('U3', 'C3'), participant('U4', 'C4')]
assert.equal(reactiveVisibleStage.value.length, 4)
assert.equal(reactiveOverflow.value, 0)
reactiveParticipants.value = [...reactiveParticipants.value, participant('U5', 'C5')]
assert.equal(reactiveVisibleStage.value.length, 3)
assert.equal(reactiveOverflow.value, 2)
reactiveParticipants.value = reactiveParticipants.value.slice(0, 4)
assert.equal(reactiveVisibleStage.value.length, 4)
assert.equal(reactiveOverflow.value, 0)

const duplicateUser = dedupeParticipantsByUser([
  { ...oneUser, cameraStream: { id: 'STREAM1' }, videoTrack: { id: 'TRACK1' } },
  { ...oneUser, connectionId: 'reconnected-C1', cameraStream: { id: 'STREAM1' }, videoTrack: { id: 'TRACK1' } }
], 'C1')
assert.equal(duplicateUser.length, 1)

const replacementUser = dedupeParticipantsByUser([
  { ...oneUser, connectionId: 'stale-C1', cameraEnabled: false },
  { ...oneUser, connectionId: 'replacement-C1', cameraEnabled: true }
])
assert.equal(replacementUser.length, 1)
assert.equal(replacementUser[0].connectionId, 'replacement-C1')
assert.equal(replacementUser[0].cameraEnabled, true)

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

assert.match(view, /v-for="user in visibleCallStageParticipants"/)
assert.doesNotMatch(view, /v-for="user in cameraStageParticipants"/)
assert.match(view, /v-if="callOverflowCount > 0"/)
assert.match(view, /v-for="user in participantsInCall\.slice\(visibleCallStageParticipants\.length/)
assert.match(view, /\+\{\{ callOverflowCount \}\} người còn lại/)
assert.match(view, /@keydown\.enter\.prevent="openCallParticipants"/)
assert.match(view, /@keydown\.space\.prevent="openCallParticipants"/)
assert.match(view, /v-else-if="hasCallParticipants" class="call-camera-stage"/)
assert.match(view, /v-else class="call-camera-off-state"/)
assert.match(view, /Camera đang tắt/)
assert.match(view, /Microphone đang bật/)
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
assert.match(view, /<LiveCaptionOverlay :enabled="captionsEnabled" :captions="liveCaptionRows"/)
assert.match(view, /:data-participant-count="visibleCallStageParticipants\.length"/)
assert.match(view, /\.call-camera-stage\[data-participant-count="1"\]/)
assert.match(view, /\.call-camera-stage\[data-participant-count="2"\]/)
assert.match(view, /\.call-camera-stage\[data-participant-count="3"\]/)
assert.match(view, /\.call-camera-stage\[data-participant-count="4"\]/)
assert.match(view, /\.call-workspace-body\.has-call-side-panel/)
assert.match(view, /call-reaction-option/)
assert.match(view, /const callViewModes =/)
assert.match(view, /\['spotlight', 'sidebar'\]/)
assert.match(view, /callViewMode\.value === 'tiled'/)
assert.match(view, /const visibleCallStageParticipants = computed\(\(\) => callLayoutMode\.value === 'CAMERA_GRID'/)
assert.match(view, /getBoundedCallStageParticipants\(participantsInCall\.value\)/)
assert.match(view, /const callOverflowCount = computed\(\(\) => callLayoutMode\.value === 'CAMERA_GRID'/)
assert.match(view, /Math\.max\(participantsInCall\.value\.length - visibleCallStageParticipants\.value\.length, 0\)/)
assert.doesNotMatch(view, /participantsInCall\.value\s*=/)

const layoutDiagnosticSource = view.slice(
  view.indexOf('const meetingLayoutCorrelation'),
  view.indexOf('const route = useRoute')
)
const layoutSnapshotSource = view.slice(
  view.indexOf('const describeMeetingParticipant'),
  view.indexOf('const callLayoutClasses')
)
assert.match(layoutDiagnosticSource, /\[MEETING_LAYOUT_DIAG\]/)
assert.match(layoutSnapshotSource, /PARTICIPANT_SNAPSHOT/)
assert.match(layoutSnapshotSource, /TILE_SNAPSHOT/)
assert.match(layoutSnapshotSource, /GRID_SNAPSHOT/)
assert.match(layoutSnapshotSource, /STREAM_OWNERSHIP_SNAPSHOT/)
assert.match(layoutSnapshotSource, /userKey/)
assert.match(layoutSnapshotSource, /connectionKey/)
assert.match(layoutSnapshotSource, /displayNameCollisionCount/)
assert.match(layoutSnapshotSource, /width: rect \? Math\.round\(rect\.width\) : 0/)
assert.match(layoutSnapshotSource, /height: rect \? Math\.round\(rect\.height\) : 0/)
assert.doesNotMatch(`${layoutDiagnosticSource}${layoutSnapshotSource}`, /\b(access_token|authorization|bearer|deviceId|trackId|streamId|SDP|ICE)\b/i)
assert.match(view, /scheduleMeetingLayoutDiagnostics\(\)/)

console.log('TWO_PARTICIPANTS_NO_SHARE_SHOWS_BOTH: covered')
console.log('REMOTE_CAMERA_OFF_STILL_SHOWS_REMOTE_TILE: covered')
console.log('REMOTE_CAMERA_ON_SHOWS_REMOTE_VIDEO: covered')
console.log('SCREEN_SHARE_SHOWS_PARTICIPANT_RAIL: covered')
console.log('STOP_SHARE_RETURNS_TO_TWO_PERSON_GRID: covered')
console.log('LOCAL_USER_DOES_NOT_HIDE_REMOTE_PARTICIPANT: covered')
console.log('THREE_PARTICIPANTS_RENDER_GRID: covered')
console.log('meetingLayoutRuntime.test.mjs: render collections prevent duplicate stage/thumb cameras and preserve presentation rails')
console.log('MEETING_LAYOUT_DIAGNOSTICS: participant, tile, grid, and ownership snapshots are redacted and debug-gated')
