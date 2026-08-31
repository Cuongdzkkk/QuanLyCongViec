import assert from 'node:assert/strict'
import { Buffer } from 'node:buffer'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { setImmediate } from 'node:timers'
import { fileURLToPath } from 'node:url'
import { createBoundedAsyncQueue } from '../src/services/captionTransportQueue.js'
import {
  createBoundedPeriodicSampler,
  summarizeRtpReport
} from '../src/services/webrtcRtpDiagnostics.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const callMediaSource = fs.readFileSync(path.join(here, '..', 'src', 'services', 'callMediaService.js'), 'utf8')

class MemoryStorage {
  constructor(values = {}) { this.values = new Map(Object.entries(values)) }
  getItem(key) { return this.values.get(key) ?? null }
  setItem(key, value) { this.values.set(key, `${value}`) }
  removeItem(key) { this.values.delete(key) }
}

class FakeTrack {
  constructor(kind) {
    this.kind = kind
    this.id = `${kind}-track`
    this.enabled = true
    this.muted = false
    this.readyState = 'live'
  }

  stop() { this.readyState = 'ended' }
}

class FakeMediaStream {
  constructor(tracks = []) {
    this.id = `stream-${FakeMediaStream.nextId++}`
    this.tracks = [...tracks]
  }

  getTracks() { return [...this.tracks] }
  getAudioTracks() { return this.tracks.filter(track => track.kind === 'audio') }
  getVideoTracks() { return this.tracks.filter(track => track.kind === 'video') }
  addTrack(track) { if (!this.tracks.includes(track)) this.tracks.push(track) }
  removeTrack(track) { this.tracks = this.tracks.filter(item => item !== track) }
}
FakeMediaStream.nextId = 1

const rtpReport = (direction, kind) => new Map([['rtp', {
  type: `${direction}-rtp`,
  kind,
  ...(direction === 'outbound'
    ? { packetsSent: 3, bytesSent: 300, ...(kind === 'video' ? { framesEncoded: 2 } : {}) }
    : {
        packetsReceived: 4,
        bytesReceived: 400,
        ...(kind === 'audio' ? { totalAudioEnergy: 0.5, audioLevel: 0.2 } : { framesReceived: 3, framesDecoded: 2 })
      })
}]])

class FakeRTCPeerConnection {
  static instances = []
  static initialConnectionState = 'new'

  constructor() {
    this.connectionState = FakeRTCPeerConnection.initialConnectionState
    this.iceConnectionState = 'new'
    this.signalingState = 'stable'
    this.localDescription = null
    this.remoteDescription = null
    this.transceivers = []
    this.statsCalls = 0
    FakeRTCPeerConnection.instances.push(this)
  }

  createTransceiver(kind, senderTrack = null) {
    const sender = {
      track: senderTrack,
      replaceTrack: async track => { sender.track = track },
      setStreams: () => {},
      getStats: async () => {
        this.statsCalls += 1
        return rtpReport('outbound', kind)
      }
    }
    const receiver = {
      track: new FakeTrack(kind),
      getStats: async () => {
        this.statsCalls += 1
        return rtpReport('inbound', kind)
      }
    }
    const transceiver = {
      sender,
      receiver,
      direction: senderTrack ? 'sendrecv' : 'recvonly',
      currentDirection: 'sendrecv',
      mid: `${this.transceivers.length}`
    }
    this.transceivers.push(transceiver)
    return transceiver
  }

  addTrack(track) { return this.createTransceiver(track.kind, track).sender }
  addTransceiver(kind) { return this.createTransceiver(kind) }
  getTransceivers() { return [...this.transceivers] }
  getSenders() { return this.transceivers.map(item => item.sender) }
  getReceivers() { return this.transceivers.map(item => item.receiver) }
  async getStats() { this.statsCalls += 1; return new Map() }
  async addIceCandidate() {}

  async setRemoteDescription(description) {
    this.remoteDescription = description
    this.signalingState = description?.type === 'offer' ? 'have-remote-offer' : 'stable'
    if (!this.transceivers.length) {
      this.createTransceiver('audio')
      this.createTransceiver('video')
      this.createTransceiver('video')
    }
  }

  async setLocalDescription(description) {
    if (description?.type === 'rollback') {
      this.localDescription = null
      this.signalingState = 'stable'
      return
    }
    const type = this.remoteDescription?.type === 'offer' ? 'answer' : 'offer'
    this.localDescription = description || { type, sdp: 'test-sdp' }
    this.signalingState = 'stable'
  }

  transitionConnection(state) {
    this.connectionState = state
    this.onconnectionstatechange?.()
  }

  transitionIce(state) {
    this.iceConnectionState = state
    this.oniceconnectionstatechange?.()
  }

  close() { this.connectionState = 'closed' }
}

class FakeHubConnection {
  constructor(snapshot) {
    this.snapshot = snapshot
    this.connectionId = 'local-peer'
    this.state = 'Disconnected'
    this.handlers = new Map()
  }

  on(name, handler) { this.handlers.set(name, handler) }
  onreconnecting(handler) { this.reconnectingHandler = handler }
  onreconnected(handler) { this.reconnectedHandler = handler }
  onclose(handler) { this.closeHandler = handler }
  async start() { this.state = 'Connected' }
  async stop() { this.state = 'Disconnected' }

  async invoke(method) {
    if (method === 'JoinVoiceRoom') return this.snapshot
    if (method === 'GetCallChatHistory') return []
    return null
  }

  emit(name, value) { this.handlers.get(name)?.(value) }
}

let currentHub = null
class FakeHubConnectionBuilder {
  withUrl() { return this }
  withAutomaticReconnect() { return this }
  configureLogging() { return this }
  build() { return currentHub }
}

globalThis.__callMediaRuntime = {
  signalR: {
    HubConnectionBuilder: FakeHubConnectionBuilder,
    HubConnectionState: {
      Connected: 'Connected',
      Connecting: 'Connecting',
      Reconnecting: 'Reconnecting',
      Disconnected: 'Disconnected'
    },
    LogLevel: { Warning: 'Warning' }
  },
  axiosClient: { get: async () => ({ data: { iceServers: [] } }) },
  getStoredAccessToken: () => '',
  createBackgroundBlurProcessor: () => null,
  configureRealtimeHub: builder => builder,
  launchCaptionTransportClientDiagnostic: () => {},
  createBoundedAsyncQueue,
  createBoundedPeriodicSampler,
  summarizeRtpReport
}

const runtimePrelude = `
const {
  signalR,
  axiosClient,
  getStoredAccessToken,
  createBackgroundBlurProcessor,
  configureRealtimeHub,
  launchCaptionTransportClientDiagnostic,
  createBoundedAsyncQueue,
  createBoundedPeriodicSampler,
  summarizeRtpReport
} = globalThis.__callMediaRuntime
`
const runtimeSource = `${runtimePrelude}\n${callMediaSource
  .split(/\r?\n/)
  .filter(line => !line.startsWith('import '))
  .join('\n')
  .replaceAll('import.meta.env', 'globalThis.__callMediaEnv')}`

globalThis.__callMediaEnv = { DEV: false, VITE_API_BASE_URL: 'https://api.test/api' }
globalThis.MediaStream = FakeMediaStream
globalThis.RTCPeerConnection = FakeRTCPeerConnection
const { createCallMediaSession } = await import(`data:text/javascript;base64,${Buffer.from(runtimeSource).toString('base64')}`)

const flushAsync = async () => {
  await new Promise(resolve => setImmediate(resolve))
  await new Promise(resolve => setImmediate(resolve))
}

const periodicSnapshots = logs => logs.filter(args => {
  if (args[0] !== '[WEBRTC_RTP_DIAG]' || typeof args[1] !== 'string') return false
  return JSON.parse(args[1]).event === 'RTP_PERIODIC_SNAPSHOT'
})

const createRuntime = async ({ debugEnabled = false, initialConnectionState = 'new' } = {}) => {
  FakeRTCPeerConnection.instances = []
  FakeRTCPeerConnection.initialConnectionState = initialConnectionState
  globalThis.localStorage = new MemoryStorage(debugEnabled ? { debug_webrtc_media: '1' } : {})
  currentHub = new FakeHubConnection({
    roomId: 'room-1',
    aiState: { callSessionId: 'call-1' },
    participants: [{ connectionId: 'remote-peer', displayName: 'Remote' }],
    transcription: {}
  })
  const session = createCallMediaSession({
    projectId: 'project-1',
    voiceChannelId: 'voice-1',
    initialMicrophoneStream: new FakeMediaStream([new FakeTrack('audio')])
  })
  await session.start()
  return { session, pc: FakeRTCPeerConnection.instances[0], hub: currentHub }
}

const establishRemoteOffer = async runtime => {
  runtime.hub.emit('WebRtcOffer', {
    fromConnectionId: 'remote-peer',
    description: { type: 'offer', sdp: 'test-sdp' },
    mediaSources: []
  })
  await flushAsync()
}

const periodicPayloads = logs => periodicSnapshots(logs).map(args => JSON.parse(args[1]))
const webRtcEvents = (logs, event) => logs
  .filter(args => args[0] === '[WEBRTC_DIAG]' && args[1]?.event === event)
  .map(args => args[1])

const originalConsoleInfo = console.info
const withRuntime = async (options, scenario) => {
  const logs = []
  console.info = (...args) => logs.push(args)
  let runtime = null
  try {
    runtime = await createRuntime(options)
    return await scenario(runtime, logs)
  } finally {
    await runtime?.session.leave()
    console.info = originalConsoleInfo
  }
}

test('PR #211 sampler runtime scenarios', async t => {
  await t.test('new -> connecting -> connected emits automatically in production', async () => {
    const count = await withRuntime({ debugEnabled: false }, async (runtime, logs) => {
      runtime.pc.transitionConnection('connecting')
      runtime.pc.transitionConnection('connected')
      await flushAsync()
      return periodicPayloads(logs).length
    })
    assert.equal(count, 1)
  })

  await t.test('existing debug opt-in path records exact peer and ICE states', async () => {
    await withRuntime({ debugEnabled: true }, async (runtime, logs) => {
      runtime.pc.transitionConnection('connecting')
      runtime.pc.transitionIce('checking')
      runtime.pc.transitionIce('connected')
      runtime.pc.transitionIce('completed')
      assert.equal(periodicPayloads(logs).length, 0, 'ICE state alone must not hide a non-connected peer')
      runtime.pc.transitionConnection('connected')
      await flushAsync()
      assert.deepEqual(
        webRtcEvents(logs, 'PEER_CONNECTION_STATE_CHANGED').map(event => event.connectionState),
        ['connecting', 'connected'])
      assert.deepEqual(
        webRtcEvents(logs, 'ICE_CONNECTION_STATE_CHANGED').map(event => event.iceConnectionState),
        ['checking', 'connected', 'completed'])
      assert.equal(periodicPayloads(logs).length, 1)
    })
  })

  await t.test('peer already connected before hook registration emits immediately', async () => {
    await withRuntime({ debugEnabled: true, initialConnectionState: 'connected' }, async (runtime, logs) => {
      await flushAsync()
      assert.equal(runtime.pc.connectionState, 'connected')
      assert.equal(periodicPayloads(logs).length, 1)
    })
  })

  await t.test('replacement peer starts a fresh sampler after recovery', async () => {
    await withRuntime({ debugEnabled: true }, async (runtime, logs) => {
      runtime.pc.transitionConnection('connected')
      await flushAsync()
      assert.equal(periodicPayloads(logs).length, 1)
      runtime.pc.transitionConnection('failed')
      await new Promise(resolve => setTimeout(resolve, 350))
      const replacement = FakeRTCPeerConnection.instances[1]
      assert.ok(replacement)
      replacement.transitionConnection('connected')
      await flushAsync()
      assert.equal(periodicPayloads(logs).length, 2)
      assert.equal(runtime.pc.connectionState, 'closed')
    })
  })

  await t.test('renegotiation while connected does not stop periodic sampling', async () => {
    await withRuntime({ debugEnabled: true }, async (runtime, logs) => {
      await establishRemoteOffer(runtime)
      runtime.pc.transitionConnection('connected')
      await flushAsync()
      runtime.pc.onnegotiationneeded?.()
      await new Promise(resolve => setTimeout(resolve, 2100))
      assert.ok(periodicPayloads(logs).length >= 2)
    })
  })

  await t.test('connected peer accepts completed ICE state', async () => {
    await withRuntime({ debugEnabled: true }, async (runtime, logs) => {
      runtime.pc.transitionIce('completed')
      runtime.pc.transitionConnection('connected')
      await flushAsync()
      assert.equal(periodicPayloads(logs).length, 1)
      assert.equal(periodicPayloads(logs)[0].iceState, 'completed')
    })
  })

  await t.test('successful getStats repeats at the two-second interval', async () => {
    await withRuntime({ debugEnabled: true }, async (runtime, logs) => {
      await establishRemoteOffer(runtime)
      runtime.pc.transitionConnection('connected')
      await new Promise(resolve => setTimeout(resolve, 2100))
      assert.ok(runtime.pc.statsCalls >= 12, `expected at least 12 endpoint stats calls, received ${runtime.pc.statsCalls}`)
      assert.ok(periodicPayloads(logs).length >= 2)
    })
  })

  await t.test('sampler timer survives long enough to emit a later snapshot', async () => {
    await withRuntime({ debugEnabled: false }, async (runtime, logs) => {
      runtime.pc.transitionConnection('connected')
      await new Promise(resolve => setTimeout(resolve, 2100))
      assert.ok(periodicPayloads(logs).length >= 2)
    })
  })

  await t.test('peer close stops future snapshots', async () => {
    const logs = []
    console.info = (...args) => logs.push(args)
    const runtime = await createRuntime({ debugEnabled: true })
    try {
      runtime.pc.transitionConnection('connected')
      await flushAsync()
      const countBeforeClose = periodicPayloads(logs).length
      await runtime.session.leave()
      await new Promise(resolve => setTimeout(resolve, 2100))
      assert.equal(periodicPayloads(logs).length, countBeforeClose)
      assert.equal(runtime.pc.connectionState, 'closed')
    } finally {
      await runtime.session.leave()
      console.info = originalConsoleInfo
    }
  })
})
