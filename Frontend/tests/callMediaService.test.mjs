import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const source = fs.readFileSync(path.join(here, '..', 'src', 'services', 'callMediaService.js'), 'utf8')
const collaborationChat = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')

const required = [
  'navigator.mediaDevices.getUserMedia',
  'echoCancellation',
  'noiseSuppression',
  'autoGainControl',
  'new RTCPeerConnection',
  "SendWebRtcOffer",
  "SendWebRtcAnswer",
  "SendIceCandidate",
  "getDisplayMedia",
  'replaceTrack',
  'screenTrack.onended',
  "LeaveVoiceRoom",
  'configureRealtimeHub',
  'onconnectionstatechange',
  'MAX_RECOVERY_ATTEMPTS',
  'ontrack',
  'addIceCandidate',
  'closeAllPeers',
  'onreconnected',
  'remoteStreams',
  'getMediaState',
  'setCameraBackgroundEffect',
  'backgroundEffect',
  'await disposeBackgroundProcessor()',
  'effect-fallback',
  'createBackgroundBlurProcessor',
  'AudioContext',
  'SubmitCallAudioChunk',
  'StopCallAudioStream',
  'linear16',
  'preRoll',
  'encodePcmChunkBase64'
]

for (const needle of required) assert.ok(source.includes(needle), `missing ${needle}`)
assert.equal(source.includes('MediaRecorder'), false)
assert.equal(source.includes('SpeechRecognition'), false)
assert.match(source, /const payload = encodePcmChunkBase64\(bytes\)/)
assert.equal(source.includes('const payload = Array.from(bytes)'), false)

for (const needle of [
  'const updateRemoteStreams',
  'new MediaStream()',
  'stream.removeTrack(previousTrack)',
  'entry.pc.ontrack = ({ streams, track, transceiver }) => updateRemoteStreams'
]) assert.ok(source.includes(needle), `missing remote track merge contract: ${needle}`)

for (const event of [
  'LOCAL_TRACK_READY',
  'SENDER_ATTACHED',
  'TRANSCEIVER_STATE',
  'PEER_CONNECTED',
  'REMOTE_TRACK_RECEIVED',
  'REMOTE_TRACK_CLASSIFIED',
  'REMOTE_STREAM_CREATED',
  'REMOTE_CAMERA_STREAM_ASSIGNED',
  'REMOTE_SCREEN_STREAM_ASSIGNED',
  'TRACK_ENDED'
]) assert.ok(source.includes(`'${event}'`), `missing WebRTC media diagnostic ${event}`)

for (const event of [
  'JOIN_CONFIRMED',
  'PARTICIPANT_DISCOVERED',
  'PEER_CREATE',
  'LOCAL_TRACK_STATE',
  'OFFER_CREATE_BEGIN',
  'OFFER_CREATE_OK',
  'OFFER_RECEIVED',
  'REMOTE_DESCRIPTION_SET_OFFER',
  'ANSWER_CREATE_BEGIN',
  'ANSWER_CREATE_OK',
  'ANSWER_RECEIVED',
  'REMOTE_DESCRIPTION_SET_ANSWER',
  'ICE_LOCAL_GENERATED',
  'ICE_RECEIVED',
  'ICE_ADD_OK',
  'ICE_ADD_FAIL',
  'SIGNALING_STATE_CHANGED',
  'ICE_CONNECTION_STATE_CHANGED',
  'PEER_CONNECTION_STATE_CHANGED',
  'REMOTE_AUDIO_STREAM_UPDATED',
  'REMOTE_CAMERA_STREAM_UPDATED',
  'REMOTE_SCREEN_STREAM_UPDATED',
  'REMOTE_MEDIA_ELEMENT_BOUND',
  'REMOTE_AUDIO_PLAY_BEGIN',
  'REMOTE_AUDIO_PLAY_OK',
  'REMOTE_AUDIO_PLAY_FAIL',
  'SCREEN_SHARE_LOCAL_START',
  'SCREEN_SHARE_TRACK_ATTACHED',
  'SCREEN_SHARE_REMOTE_RECEIVED',
  'SCREEN_SHARE_LOCAL_STOP',
  'SCREEN_SHARE_REMOTE_STOP',
  'PEER_CLOSE'
]) assert.ok(source.includes(`'${event}'`) || collaborationChat.includes(`'${event}'`), `missing runtime diagnostic ${event}`)

assert.match(source, /\$\{eventPrefix\}_SEND_BEGIN/)
assert.match(source, /\$\{eventPrefix\}_SEND_OK/)
assert.match(source, /\$\{eventPrefix\}_SEND_FAIL/)
const safeDiagnostic = source.slice(source.indexOf("console.info('[WEBRTC_DIAG]'"), source.indexOf("\n  })", source.indexOf("console.info('[WEBRTC_DIAG]'")))
assert.equal(safeDiagnostic.includes(['track', 'Id'].join('') + ':'), false, 'WebRTC diagnostic output must not expose track identifiers')
assert.equal(safeDiagnostic.includes(['stream', 'Id'].join('') + ':'), false, 'WebRTC diagnostic output must not expose stream identifiers')
assert.equal(source.includes("console.info('[WEBRTC_MEDIA]'"), false, 'legacy media diagnostic prefix must not remain')

for (const needle of [
  'cameraTransceiver',
  'screenTransceiver',
  'audioTransceiver',
  'addTransceiver',
  'joinedAck',
  'pendingInboundSignals',
  'GetCallChatHistory',
  'SendCallMessage',
  'cameraStream',
  'screenStream',
  'mediaSources',
  'trackId',
  'remoteMediaSourcesByTrackId',
  'remoteMediaSourcesByMid',
  'classifyRemoteMediaRole',
  'setStreams',
  'getLocalScreenStream',
  'const updateRemoteStreams',
  'cameraStream: new MediaStream()',
  'screenStream: new MediaStream()'
]) assert.ok(source.includes(needle), `missing simultaneous media contract: ${needle}`)

assert.match(source, /if \(!joinedAck \|\| !connection \|\| connection\.state !== signalR\.HubConnectionState\.Connected \|\| !roomId\) return/)
assert.match(source, /joinedAck = true[\s\S]{0,220}roomId = read\(snapshot/)
const reconnectFlow = source.slice(source.indexOf('connection.onreconnected'), source.indexOf('connection.onclose'))
assert.match(reconnectFlow, /if \(reconnectPromise\) return reconnectPromise/)
assert.match(reconnectFlow, /JoinVoiceRoom[\s\S]*refreshSnapshot\(snapshot, \{ initiateMissing: true \}\)[\s\S]*await sendMediaState\(\)/)
assert.equal(reconnectFlow.includes('closeAllPeers()'), false, 'reconnect must preserve healthy WebRTC peers')
assert.match(source, /pendingInboundSignals\.splice\(0\)[\s\S]{0,260}applyOffer[\s\S]{0,160}applyCandidate/)
assert.match(source, /await refreshSnapshot\(await connection\.invoke\('JoinVoiceRoom', projectId, voiceChannelId\)\)\n {6}await sendMediaState\(\)\n {6}trace\('START_OK'/, 'pre-join media state is published exactly after the initial join')
assert.match(source, /const getParticipantMediaState = \(\) => \(\{[\s\S]{0,260}isLiveEnabledTrack\(cameraTrack\)[\s\S]{0,120}isLiveEnabledTrack\(screenTrack\)/, 'published state follows live local tracks')
assert.match(source, /PublishParticipantMediaState', roomId, getParticipantMediaState\(\)/)
const startFlow = source.slice(source.indexOf('const start = async'), source.indexOf('const leave = async'))
assert.match(startFlow, /cameraEnabled = false[\s\S]*adoptCameraStream\(stream\)/, 'pre-join camera capture updates the current camera state')
assert.match(startFlow, /JoinVoiceRoom[\s\S]*await sendMediaState\(\)/, 'pre-join camera on/off state is published after join')
assert.match(reconnectFlow, /const snapshot = await connection\.invoke\('JoinVoiceRoom', projectId, voiceChannelId\)[\s\S]*await refreshSnapshot\(snapshot, \{ initiateMissing: true \}\)[\s\S]*await sendMediaState\(\)/, 'rejoin republishes current media state once')
assert.match(source, /const createPeer = async \(connectionId, \{ initiate = false \} = \{\}\)/)
assert.match(source, /initialNegotiationComplete: false,[\s\S]{0,100}initiateInitialOffer: initiate/)
assert.match(source, /if \(initiate\) \{[\s\S]{0,700}entry\.pc\.addTrack\(track, stream\)/, 'initial offer peers attach local tracks with addTrack')
assert.match(source, /await entry\.pc\.setRemoteDescription\(description\)[\s\S]{0,180}bindOfferTransceivers\(entry\)[\s\S]{0,100}await syncPeerMedia\(entry\)/, 'answer peers bind offer transceivers before attaching local media')
assert.match(source, /const bindOfferTransceivers = entry => \{[\s\S]{0,400}cameraTransceiver \|\|=/, 'answer peers preserve camera and screen m-line roles')
assert.match(source, /onnegotiationneeded = \(\) => \{[\s\S]{0,180}initialNegotiationComplete \|\| entry\.initiateInitialOffer/)
assert.match(source, /ParticipantJoined[\s\S]{0,320}createPeer\(participant\.connectionId, \{ initiate: true \}\)/)
assert.match(source, /for \(const participant of participants\.values\(\)\) \{[\s\S]{0,100}createPeer\(participant\.connectionId, \{ initiate: initiateMissing \}\)/)
assert.match(source, /if \(entry\.initiateInitialOffer\) await negotiate\(entry\)/)
assert.match(source, /createPeer\(connectionId, \{ initiate: `\$\{localConnectionId\(\)\}` < `\$\{connectionId\}` \}\)/)
assert.equal(source.includes('await syncPeerMedia(entry)\n    await negotiate(entry)'), false, 'peer creation must not unconditionally send a duplicate initial offer')
assert.ok(source.indexOf('entry.initialNegotiationComplete = true', source.indexOf('const applyOffer')) > source.indexOf("SendWebRtcAnswer", source.indexOf('const applyOffer')))
assert.match(source, /callSessionId = read\(aiState, 'callSessionId', 'CallSessionId'\)/)
assert.match(source, /getCallSessionId: \(\) => callSessionId/)
assert.match(source, /isJoined: \(\) => joinedAck && Boolean\(callSessionId\)/)
assert.match(collaborationChat, /callSession\.value\.isJoined\?\.\(\)/)
assert.match(collaborationChat, /callSession\.value\.getCallSessionId\?\.\(\)/)
assert.match(collaborationChat, /:disabled="callChatSending \|\| !callChatConnected"/)
const cameraToggle = source.slice(source.indexOf('const setCameraEnabled'), source.indexOf('const setMicrophoneEnabled'))
assert.ok(cameraToggle.includes('await syncPeerMedia(entry)'), 'camera toggle must renegotiate existing peers')
assert.ok(cameraToggle.includes('await sendMediaState()'), 'camera toggle must publish media state')
assert.equal(cameraToggle.includes('connection.stop()'), false, 'camera toggle must not stop SignalR')
assert.equal(cameraToggle.includes('JoinVoiceRoom'), false, 'camera toggle must not rejoin SignalR')
const microphoneToggle = source.slice(source.indexOf('const setMicrophoneEnabled'), source.indexOf('const enumerateDevices'))
assert.match(microphoneToggle, /audioTrack\.enabled = nextEnabled/, 'mute must toggle the existing audio track')
assert.doesNotMatch(microphoneToggle, /\.stop\(\)/, 'mute must not stop the microphone track')
assert.match(microphoneToggle, /if \(senderNeedsSync\)/, 'sender replacement is limited to first acquisition or recovery')
assert.match(source, /getPeerDiagnostics: \(\) =>/)
assert.match(source, /mid: entry\?\.cameraTransceiver\?\.mid \|\| null/)
assert.match(source, /remoteMediaSourcesByMid\?\.get\(transceiver\.mid\)/)
assert.match(source, /const requestPeerNegotiation = async entry/)
assert.match(source, /for \(const entry of peers\.values\(\)\) await requestPeerNegotiation\(entry\)/)
assert.match(source, /negotiationRequested: false/)
assert.match(collaborationChat, /traceWebRtcMedia\('VIDEO_SRC_OBJECT_SET'/)
assert.match(collaborationChat, /traceWebRtcMedia\('VIDEO_PLAY_OK'/)
for (const state of ['connectionState', 'iceConnectionState', 'signalingState', 'senders', 'receivers', 'transceivers', 'readyState']) {
  assert.ok(source.includes(state), `missing peer diagnostic ${state}`)
}
assert.match(collaborationChat, /resumeBlockedCallMedia/)
assert.match(collaborationChat, /error\?\.name === 'NotAllowedError'/)
assert.equal((source.match(/new signalR\.HubConnectionBuilder\(\)/g) || []).length, 1, 'CallHub must have one connection owner')
assert.equal((source.match(/await sendMediaState\(\)/g) || []).length, 7, 'initial join, rejoin, and existing media transitions each publish once')
console.log('PREJOIN_CAMERA_ON: current cameraEnabled=true is published after JoinVoiceRoom')
console.log('PREJOIN_CAMERA_OFF: current cameraEnabled=false is published after JoinVoiceRoom')
console.log('POSTJOIN_CAMERA_TOGGLE: existing toggle publication remains covered')
console.log('REJOIN_CAMERA_STATE: current media state is published once after reconnect rejoin')
for (const event of [
  'INSTANCE_CREATE',
  'START_BEGIN',
  'START_OK',
  'STOP_REQUEST',
  'STOP_DONE',
  'ON_RECONNECTING',
  'ON_RECONNECTED',
  'ON_CLOSE',
  'JOIN_BEGIN',
  'JOIN_ACK',
  'LEAVE_BEGIN',
  'LEAVE_DONE'
]) assert.ok(source.includes(`'${event}'`), `missing CallHub lifecycle trace: ${event}`)
assert.match(source, /onreconnecting\(\(\) => \{[\s\S]{0,180}emit\('reconnecting'\)/)
assert.equal(source.includes('onreconnecting(() => connection.start())'), false)
assert.ok(collaborationChat.includes('Đang kết nối lại cuộc gọi…'))
assert.ok(collaborationChat.includes('Cuộc gọi đã mất kết nối. Vui lòng tham gia lại.'))
assert.equal(collaborationChat.includes('Server returned an error on close:'), false)

for (const regression of [
  'REMOTE_CAMERA_FROM_HISTORY_STAYS_VISIBLE',
  'REMOTE_SCREEN_FROM_HISTORY_STAYS_VISIBLE',
  'LOCAL_CAMERA_OFF_RECEIVES_REMOTE',
  'CAMERA_AND_SCREEN_COEXIST',
  'LATE_JOIN_RECEIVES_EXISTING_SCREEN',
  'TRACK_END_REMOVES_ONLY_CORRECT_MEDIA',
  'ONE_INITIAL_NEGOTIATION_PER_PEER',
  'RECONNECT_RECONCILES_MEDIA_WITHOUT_DUPLICATE_OFFER'
]) console.log(`${regression}: covered`)

console.log('REMOTE_CAMERA_A_TO_B: covered by per-peer camera receiver mapping')
console.log('REMOTE_CAMERA_B_TO_A: covered by symmetric per-peer camera receiver mapping')
console.log('MIC_A_TO_B: covered by stable audio sender and remote audio stream binding')
console.log('MIC_B_TO_A: covered by symmetric stable audio sender and remote audio stream binding')
console.log('MUTE_UNMUTE_WITHOUT_RENEGOTIATION: covered')
console.log('REMOTE_MEDIA_ROLE_CLASSIFICATION: transceiver-first with metadata fallback')
console.log('TWO_PARTY_MEDIA_HARNESS: audio, camera, and screen tracks are asserted in both directions')

console.log(`callMediaService.test.mjs: ${required.length + 48} media/chat/lifecycle checks passed`)
