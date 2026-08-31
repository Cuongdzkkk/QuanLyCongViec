import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  createBoundedPeriodicSampler,
  summarizeRtpReport
} from '../src/services/webrtcRtpDiagnostics.js'

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
    framesReceived: 15,
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
  framesReceived: 15,
  framesDecoded: 14
})

const createFakeIntervalClock = () => {
  let callback = null
  let intervalMs = null
  let deadlineCallback = null
  let deadlineMs = null
  let clearCount = 0
  let nowMs = 0
  return {
    setIntervalFn: (next, delay) => {
      callback = next
      intervalMs = delay
      return 1
    },
    clearIntervalFn: () => {
      callback = null
      clearCount += 1
    },
    setTimeoutFn: (next, delay) => {
      deadlineCallback = next
      deadlineMs = delay
      return 2
    },
    clearTimeoutFn: () => {
      deadlineCallback = null
    },
    tick: () => {
      nowMs += intervalMs
      callback?.()
    },
    expire: () => {
      nowMs = deadlineMs
      deadlineCallback?.()
    },
    advance: delay => { nowMs += delay },
    now: () => nowMs,
    interval: () => intervalMs,
    deadline: () => deadlineMs,
    clearCount: () => clearCount,
    active: () => Boolean(callback),
    deadlineActive: () => Boolean(deadlineCallback)
  }
}

{
  const clock = createFakeIntervalClock()
  let active = false
  let sampleCount = 0
  const sampler = createBoundedPeriodicSampler({
    isActive: () => active,
    sample: () => { sampleCount += 1 },
    setIntervalFn: clock.setIntervalFn,
    clearIntervalFn: clock.clearIntervalFn,
    setTimeoutFn: clock.setTimeoutFn,
    clearTimeoutFn: clock.clearTimeoutFn,
    nowFn: clock.now
  })

  assert.equal(sampler.start(), false, 'inactive peers must not start RTP sampling')
  assert.equal(sampleCount, 0)
  assert.equal(clock.active(), false)

  active = true
  assert.equal(sampler.start(), true)
  assert.equal(sampleCount, 1, 'active peers receive an immediate snapshot')
  assert.equal(clock.interval(), 2000)
  assert.equal(clock.deadline(), 20000)
  assert.equal(sampler.start(), false, 'a peer may only have one periodic sampler')

  for (let elapsed = 2000; elapsed <= 20000; elapsed += 2000) clock.tick()
  assert.equal(sampleCount, 11, 'sampling includes t=0 through t=20s')
  assert.equal(clock.active(), true, 'the interval remains active until the wall-clock deadline')
  clock.expire()
  assert.equal(clock.active(), false, 'sampler stops after the bounded duration')
  assert.equal(clock.deadlineActive(), false)
  assert.equal(clock.clearCount(), 1)
}

{
  const clock = createFakeIntervalClock()
  let active = true
  let sampleCount = 0
  const sampler = createBoundedPeriodicSampler({
    isActive: () => active,
    sample: () => { sampleCount += 1 },
    setIntervalFn: clock.setIntervalFn,
    clearIntervalFn: clock.clearIntervalFn,
    setTimeoutFn: clock.setTimeoutFn,
    clearTimeoutFn: clock.clearTimeoutFn,
    nowFn: clock.now
  })

  assert.equal(sampler.start(), true)
  active = false
  clock.tick()
  assert.equal(sampleCount, 1, 'an inactive/closed peer must not be sampled again')
  assert.equal(clock.active(), false)

  active = true
  assert.equal(sampler.start(), true)
  sampler.stop()
  assert.equal(clock.active(), false, 'explicit peer close clears the sampler')
  assert.equal(clock.deadlineActive(), false, 'explicit peer close clears the max-duration timer')
}

{
  const clock = createFakeIntervalClock()
  let sampleCount = 0
  const sampler = createBoundedPeriodicSampler({
    isActive: () => true,
    sample: () => { sampleCount += 1 },
    setIntervalFn: clock.setIntervalFn,
    clearIntervalFn: clock.clearIntervalFn,
    setTimeoutFn: clock.setTimeoutFn,
    clearTimeoutFn: clock.clearTimeoutFn,
    nowFn: clock.now
  })

  assert.equal(sampler.start(), true)
  clock.advance(20001)
  clock.tick()
  assert.equal(sampleCount, 1, 'a throttled interval must not sample after the 20s deadline')
  assert.equal(clock.active(), false)
  assert.equal(clock.deadlineActive(), false)
}

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
  'RTP_PERIODIC_SNAPSHOT',
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

assert.match(callMediaSource, /entry\.pc\.connectionState === 'connected'[\s\S]{0,180}startPeriodicRtpSampling\(entry\)/)
assert.match(callMediaSource, /schedulePeerRtpStats\(entry, 'camera-enable-settled', 2000\)/)
assert.match(callMediaSource, /const startPeriodicRtpSampling = entry => \{\s+if \(!webRtcMediaTraceEnabled\(\)\) return/)
assert.match(callMediaSource, /framesDecoded: detail\.framesDecoded \?\? null,\s+framesReceived: detail\.framesReceived \?\? null/)

const periodicRtpDiagnostic = callMediaSource.slice(
  callMediaSource.indexOf('const tracePeriodicRtpSnapshot'),
  callMediaSource.indexOf('\nconst read', callMediaSource.indexOf('const tracePeriodicRtpSnapshot')))
assert.match(periodicRtpDiagnostic, /console\.info\('\[WEBRTC_RTP_DIAG\]', JSON\.stringify\(\{/)
assert.deepEqual(
  [...periodicRtpDiagnostic.matchAll(/^\s{4}(\w+):/gm)].map(match => match[1]),
  ['event', 'timestamp', 'peerState', 'iceState', 'audioInbound', 'audioOutbound', 'videoInbound', 'videoOutbound'])
for (const forbidden of [
  'ip',
  'candidate',
  'sdp',
  'turnCredential',
  'accessToken',
  'jwt',
  'cookie',
  'authorization'
]) assert.equal(periodicRtpDiagnostic.toLowerCase().includes(`${forbidden.toLowerCase()}:`), false, `unsafe RTP diagnostic field: ${forbidden}`)

assert.match(callMediaSource, /const endpointStats = async[\s\S]{0,180}getStats/)
assert.match(callMediaSource, /if \(!webRtcMediaTraceEnabled\(\)\) return[\s\S]{0,240}setTimeout/)
assert.match(callMediaSource, /rtpPeriodicSampler\?\.stop\(\)/)
assert.match(callMediaSource, /entry\.rtpPeriodicSampler \|\|= createBoundedPeriodicSampler/)

console.log('webrtcRtpDiagnostics.test.mjs: RTP aggregation checks passed')
