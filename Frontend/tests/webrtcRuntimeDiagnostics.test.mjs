import assert from 'node:assert/strict'
import test from 'node:test'
import {
  collectPeerRuntimeStats,
  copyTextToClipboard,
  createSanitizedWebRtcReport,
  describeMediaElement,
  getRecentWebRtcDiagnosticEvents,
  isWebRtcDebugEnabled,
  recordMediaElementDiagnostic,
  recordWebRtcDiagnosticEvent,
  resetWebRtcRuntimeDiagnostics,
  sanitizeIceServerConfig
} from '../src/utils/webrtcRuntimeDiagnostics.js'

const statsReport = ({ bytes = 100, packets = 10, frames = 4 } = {}) => new Map([
  ['pair', {
    type: 'candidate-pair',
    state: 'succeeded',
    nominated: true,
    localCandidateId: 'local-secret-address',
    remoteCandidateId: 'remote-secret-address',
    protocol: 'udp'
  }],
  ['local-secret-address', {
    id: 'local-secret-address',
    type: 'local-candidate',
    candidateType: 'relay',
    relayProtocol: 'udp',
    address: '192.0.2.10',
    port: 5349,
    candidate: 'candidate:1 1 UDP 123 192.0.2.10 5349 typ relay'
  }],
  ['remote-secret-address', {
    id: 'remote-secret-address',
    type: 'remote-candidate',
    candidateType: 'srflx',
    address: '198.51.100.7',
    port: 42111,
    candidate: 'candidate:2 1 UDP 456 198.51.100.7 42111 typ srflx'
  }],
  ['audio-in', {
    type: 'inbound-rtp',
    kind: 'audio',
    packetsReceived: packets,
    bytesReceived: bytes,
    totalAudioEnergy: 0.2,
    audioLevel: 0.3
  }],
  ['video-in', {
    type: 'inbound-rtp',
    kind: 'video',
    packetsReceived: packets,
    bytesReceived: bytes,
    framesReceived: frames,
    framesDecoded: frames - 1
  }],
  ['audio-out', {
    type: 'outbound-rtp',
    kind: 'audio',
    packetsSent: packets,
    bytesSent: bytes
  }],
  ['video-out', {
    type: 'outbound-rtp',
    kind: 'video',
    packetsSent: packets,
    bytesSent: bytes,
    framesEncoded: frames
  }]
])

const fakePc = report => ({
  connectionState: 'connected',
  iceConnectionState: 'connected',
  signalingState: 'stable',
  getStats: async () => report,
  getSenders: () => [{ track: { kind: 'audio', enabled: true, muted: false, readyState: 'live' } }],
  getReceivers: () => [{ track: { kind: 'video', enabled: true, muted: false, readyState: 'live' } }]
})

test('debug flag supports query and localStorage while remaining disabled by default', () => {
  assert.equal(isWebRtcDebugEnabled({ location: { search: '' }, localStorage: { getItem: () => null } }), false)
  assert.equal(isWebRtcDebugEnabled({ location: { search: '?webrtcDebug=1' }, localStorage: { getItem: () => null } }), true)
  assert.equal(isWebRtcDebugEnabled({ location: { search: '' }, localStorage: { getItem: key => key === 'debug_webrtc_media' ? '1' : null } }), true)
})

test('debug flag does not activate for other query or storage values', () => {
  assert.equal(isWebRtcDebugEnabled({ location: { search: '?webrtcDebug=0' }, localStorage: { getItem: () => '0' } }), false)
  assert.equal(isWebRtcDebugEnabled({ location: { search: '?other=1' }, localStorage: { getItem: () => null } }), false)
})

test('ICE and RTP diagnostics are sanitized and deltas are computed', async () => {
  const first = await collectPeerRuntimeStats({ pc: fakePc(statsReport()), previous: null })
  const second = await collectPeerRuntimeStats({ pc: fakePc(statsReport({ bytes: 240, packets: 24, frames: 9 })), previous: first })
  assert.deepEqual(first.selectedCandidatePair, {
    state: 'succeeded',
    localCandidateType: 'relay',
    remoteCandidateType: 'srflx',
    protocol: 'udp',
    relayProtocol: 'udp'
  })
  assert.equal(second.rtp.audio.inbound.packetsIncreasing, true)
  assert.equal(second.rtp.video.inbound.framesIncreasing, true)
  assert.equal(second.rtp.audio.outbound.bytesIncreasing, true)
  assert.equal(second.tracks.receivers[0].kind, 'video')

  const report = createSanitizedWebRtcReport({
    appBuild: 'test-build',
    debugEnabled: true,
    callSessionPresent: true,
    roomPresent: true,
    participantCount: 2,
    peerSnapshots: [{ ...second, target: 'connection-id-secret' }],
    iceServer: sanitizeIceServerConfig([
      { urls: 'turn:203.0.113.20:5349', username: 'secret-user', credential: 'secret-token' },
      { urls: ['stun:stun.example.test', 'turns:turn.example.test'] }
    ]),
    events: [{ event: 'REMOTE_TRACK_RECEIVED', trackKind: 'audio' }],
    mediaElements: []
  })
  assert.match(report, /CANDIDATE_TYPE=relay/)
  assert.match(report, /RTP_AUDIO_INBOUND_PACKETS_INCREASING=YES/)
  assert.doesNotMatch(report, /192\.0\.2\.10|198\.51\.100\.7|5349|secret-user|secret-token|candidate:1|connection-id-secret/)
  assert.doesNotMatch(report, /v=0|a=ice-ufrag|a=ice-pwd|sdpMLineIndex/)
})

test('media element diagnostics expose binding, playback, and track state only', () => {
  const track = { kind: 'audio', enabled: true, muted: false, readyState: 'live' }
  const element = {
    tagName: 'AUDIO',
    srcObject: { active: true, getTracks: () => [track] },
    autoplay: true,
    muted: false,
    paused: false,
    readyState: 4,
    volume: 1
  }
  const state = describeMediaElement(element, { mediaRole: 'audio' })
  assert.deepEqual(state, {
    mediaRole: 'audio',
    elementKind: 'audio',
    hasSrcObject: true,
    streamActive: true,
    audioTrackCount: 1,
    videoTrackCount: 0,
    trackStates: [{ kind: 'audio', enabled: true, muted: false, readyState: 'live' }],
    autoplay: true,
    muted: false,
    paused: false,
    readyState: 4,
    volume: 1,
    playResult: 'not-recorded',
    playErrorName: ''
  })
  resetWebRtcRuntimeDiagnostics()
  recordMediaElementDiagnostic(element, { mediaRole: 'audio', playResult: 'error', errorName: 'NotAllowedError' })
  assert.equal(getRecentWebRtcDiagnosticEvents().length, 0)
})

test('first RTP sample stays inconclusive until a second sample exists', async () => {
  const first = await collectPeerRuntimeStats({ pc: fakePc(statsReport()), previous: null })
  assert.equal(first.rtp.audio.inbound.packetsIncreasing, null)
  assert.equal(first.rtp.video.inbound.framesIncreasing, null)
})

test('recent events are opt-in, sanitized, and classify a working path', () => {
  resetWebRtcRuntimeDiagnostics()
  recordWebRtcDiagnosticEvent('REMOTE_TRACK_RECEIVED', { trackKind: 'video', peerId: 'secret-peer', roomId: 'secret-room' })
  assert.deepEqual(getRecentWebRtcDiagnosticEvents(), [{ event: 'REMOTE_TRACK_RECEIVED', trackKind: 'video' }])
  const report = createSanitizedWebRtcReport({
    appBuild: 'test',
    debugEnabled: true,
    callSessionPresent: true,
    roomPresent: true,
    participantCount: 2,
    peerSnapshots: [{
      connectionState: 'connected',
      iceConnectionState: 'connected',
      signalingState: 'stable',
      selectedCandidatePair: { state: 'succeeded', localCandidateType: 'host', remoteCandidateType: 'host', protocol: 'udp', relayProtocol: '' },
      rtp: { audio: { inbound: { packets: 10, bytes: 100, packetsIncreasing: true, bytesIncreasing: true }, outbound: { packets: 10, bytes: 100, packetsIncreasing: true, bytesIncreasing: true } }, video: { inbound: { packets: 10, bytes: 100, frames: 4, framesIncreasing: true }, outbound: { packets: 10, bytes: 100, frames: 4, packetsIncreasing: true, bytesIncreasing: true } } },
      tracks: { senders: [], receivers: [] }
    }],
    iceServer: { httpStatus: 200, iceServerCount: 1, stunPresent: true, turnPresent: false, turnServerCount: 0 },
    events: getRecentWebRtcDiagnosticEvents(),
    mediaElements: [{ hasSrcObject: true, streamActive: true, playResult: 'ok', elementKind: 'video' }]
  })
  assert.match(report, /CLASSIFICATION=MEDIA_PATH_WORKING/)
  assert.doesNotMatch(report, /secret-peer|secret-room/)
})

test('clipboard helper reports API failure so the panel can show its selectable fallback', async () => {
  let copied = ''
  assert.equal(await copyTextToClipboard('diagnostics', { clipboard: { writeText: async value => { copied = value } } }), true)
  assert.equal(copied, 'diagnostics')
  assert.equal(await copyTextToClipboard('diagnostics', { clipboard: { writeText: async () => { throw new Error('blocked') } } }), false)
  assert.equal(await copyTextToClipboard('diagnostics', {}), false)
})
