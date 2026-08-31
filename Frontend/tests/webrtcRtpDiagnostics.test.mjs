import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { summarizeRtpReport } from '../src/services/webrtcRtpDiagnostics.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const callMediaSource = fs.readFileSync(path.join(here, '..', 'src', 'services', 'callMediaService.js'), 'utf8')

const report = new Map([
  ['out-audio', {
    type: 'outbound-rtp',
    kind: 'audio',
    packetsSent: 12,
    bytesSent: 2400,
    totalAudioEnergy: 1.25,
    audioLevel: 0.4
  }],
  ['in-audio', {
    type: 'inbound-rtp',
    mediaType: 'audio',
    packetsReceived: 10,
    bytesReceived: 2000,
    packetsLost: 2,
    totalAudioEnergy: 0.75,
    audioLevel: 0.2
  }],
  ['out-video', {
    type: 'outbound-rtp',
    kind: 'video',
    packetsSent: 8,
    bytesSent: 8000,
    framesEncoded: 16
  }],
  ['in-video', {
    type: 'inbound-rtp',
    kind: 'video',
    packetsReceived: 7,
    bytesReceived: 7000,
    framesDecoded: 14
  }],
  ['codec', { type: 'codec', kind: 'audio' }]
])

assert.deepEqual(summarizeRtpReport(report, 'outbound', 'audio'), {
  outboundRtpFound: true,
  packetsSent: 12,
  bytesSent: 2400,
  totalAudioEnergy: 1.25,
  audioLevel: 0.4
})

assert.deepEqual(summarizeRtpReport(report, 'inbound', 'audio'), {
  inboundRtpFound: true,
  packetsReceived: 10,
  bytesReceived: 2000,
  packetsLost: 2,
  totalAudioEnergy: 0.75,
  audioLevel: 0.2
})

assert.deepEqual(summarizeRtpReport(report, 'outbound', 'video'), {
  outboundRtpFound: true,
  packetsSent: 8,
  bytesSent: 8000,
  framesEncoded: 16
})

assert.deepEqual(summarizeRtpReport(report, 'inbound', 'video'), {
  inboundRtpFound: true,
  packetsReceived: 7,
  bytesReceived: 7000,
  framesDecoded: 14
})

assert.deepEqual(summarizeRtpReport(new Map(), 'outbound', 'audio'), {
  outboundRtpFound: false,
  packetsSent: 0,
  bytesSent: 0
})

for (const event of [
  'RTP_OUTBOUND_AUDIO',
  'RTP_INBOUND_AUDIO',
  'RTP_OUTBOUND_VIDEO',
  'RTP_INBOUND_VIDEO',
  'CAMERA_ENABLE_BEGIN',
  'CAMERA_TRACK_ACQUIRED',
  'CAMERA_SENDER_FOUND',
  'CAMERA_REPLACE_TRACK_BEGIN',
  'CAMERA_REPLACE_TRACK_OK',
  'CAMERA_REPLACE_TRACK_FAIL',
  'CAMERA_SENDER_STATE_AFTER',
  'NEGOTIATION_NEEDED_AFTER_CAMERA',
  'CAMERA_DISABLE_BEGIN',
  'CAMERA_REPLACE_WITH_NULL_OR_DISABLE',
  'CAMERA_DISABLE_OK',
  'MIC_STATE_CHANGED',
  'MIC_SENDER_STATE'
]) assert.ok(callMediaSource.includes(event), `missing RTP diagnostic event: ${event}`)

assert.match(callMediaSource, /schedulePeerRtpStats\(entry, 'peer-connected-2s', 2000\)/)
assert.match(callMediaSource, /schedulePeerRtpStats\(entry, 'controlled-audio-window', 6500\)/)
assert.match(callMediaSource, /schedulePeerRtpStats\(entry, 'camera-enable-settled', 2000\)/)

const safeRtpDiagnostic = callMediaSource.slice(
  callMediaSource.indexOf("console.info('[WEBRTC_RTP_DIAG]'"),
  callMediaSource.indexOf('\n  })', callMediaSource.indexOf("console.info('[WEBRTC_RTP_DIAG]'")))
for (const forbidden of [
  ['track', 'Id'].join(''),
  ['device', 'Id'].join(''),
  ['device', 'Label'].join(''),
  ['access', 'Token'].join(''),
  ['Authorization'].join(''),
  ['candidate', 'String'].join(''),
  ['sdp'].join('')
]) assert.equal(safeRtpDiagnostic.includes(`${forbidden}:`), false, `unsafe RTP diagnostic field: ${forbidden}`)

assert.match(callMediaSource, /const endpointStats = async[\s\S]{0,180}getStats/)
assert.match(callMediaSource, /if \(!webRtcMediaTraceEnabled\(\)\) return[\s\S]{0,240}setTimeout/)

console.log('webrtcRtpDiagnostics.test.mjs: RTP aggregation checks passed')
