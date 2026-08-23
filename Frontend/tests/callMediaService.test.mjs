import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const source = fs.readFileSync(path.join(here, '..', 'src', 'services', 'callMediaService.js'), 'utf8')

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
  'withAutomaticReconnect',
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
  'cameraSender',
  'screenSender',
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

console.log(`callMediaService.test.mjs: ${required.length + 12} media foundation checks passed`)
