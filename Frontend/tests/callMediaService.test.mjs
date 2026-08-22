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
  'createBackgroundBlurProcessor'
]

for (const needle of required) assert.ok(source.includes(needle), `missing ${needle}`)
assert.equal(source.includes('MediaRecorder'), false)
assert.equal(source.includes('transcri'), false)

console.log(`callMediaService.test.mjs: ${required.length} media foundation checks passed`)
