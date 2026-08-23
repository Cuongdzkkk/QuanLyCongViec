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
  'preRoll'
]

for (const needle of required) assert.ok(source.includes(needle), `missing ${needle}`)
assert.equal(source.includes('MediaRecorder'), false)
assert.equal(source.includes('SpeechRecognition'), false)

for (const needle of [
  'const updateRemoteStreams',
  'new MediaStream()',
  'stream.removeTrack(previousTrack)',
  'entry.pc.ontrack = ({ streams, track }) => updateRemoteStreams'
]) assert.ok(source.includes(needle), `missing remote track merge contract: ${needle}`)

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
  'getLocalScreenStream',
  'const updateRemoteStreams',
  'cameraStream: new MediaStream()',
  'screenStream: new MediaStream()'
]) assert.ok(source.includes(needle), `missing simultaneous media contract: ${needle}`)

assert.match(source, /if \(!joinedAck \|\| !connection \|\| connection\.state !== signalR\.HubConnectionState\.Connected \|\| !roomId\) return/)
assert.match(source, /joinedAck = true[\s\S]{0,220}roomId = read\(snapshot/)
assert.match(source, /onreconnected\(async \(\) => \{[\s\S]{0,500}JoinVoiceRoom[\s\S]{0,220}refreshSnapshot/)
assert.match(source, /pendingInboundSignals\.splice\(0\)[\s\S]{0,260}applyOffer[\s\S]{0,160}applyCandidate/)
assert.match(source, /await syncPeerMedia\(entry\)[\s\S]{0,320}await negotiate\(entry\)/)
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

console.log(`callMediaService.test.mjs: ${required.length + 22} media/chat hotfix checks passed`)
