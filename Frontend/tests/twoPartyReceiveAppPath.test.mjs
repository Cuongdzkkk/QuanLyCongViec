import assert from 'node:assert/strict'
import { Buffer } from 'node:buffer'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { setImmediate } from 'node:timers'
import { fileURLToPath } from 'node:url'
import { createBoundedAsyncQueue } from '../src/services/captionTransportQueue.js'
import { dedupeParticipantsByUser } from '../src/services/meetingLayoutState.js'
import {
  createBoundedPeriodicSampler,
  summarizeRtpReport
} from '../src/services/webrtcRtpDiagnostics.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const serviceSource = fs.readFileSync(path.join(here, '..', 'src', 'services', 'callMediaService.js'), 'utf8')
const viewSource = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')

class FakeTrack {
  constructor(kind, id) {
    this.kind = kind
    this.id = id
    this.enabled = true
    this.muted = false
    this.readyState = 'live'
  }

  stop() { this.readyState = 'ended' }
}

class FakeMediaStream {
  constructor(tracks = [], id = `stream-${FakeMediaStream.nextId++}`) {
    this.id = id
    this.tracks = [...tracks]
  }

  getTracks() { return [...this.tracks] }
  getAudioTracks() { return this.tracks.filter(track => track.kind === 'audio') }
  getVideoTracks() { return this.tracks.filter(track => track.kind === 'video') }
  addTrack(track) { if (!this.tracks.includes(track)) this.tracks.push(track) }
  removeTrack(track) { this.tracks = this.tracks.filter(item => item !== track) }
}
FakeMediaStream.nextId = 1

class FakeMediaElement {
  constructor() {
    this.autoplay = false
    this.muted = true
    this.playsInline = false
    this.srcObject = null
    this.volume = 1
  }

  play() { return Promise.resolve() }
}

class FakeRTCPeerConnection {
  static instances = []

  constructor() {
    this.connectionState = 'new'
    this.iceConnectionState = 'new'
    this.signalingState = 'stable'
    this.localDescription = null
    this.remoteDescription = null
    this.transceivers = []
    FakeRTCPeerConnection.instances.push(this)
  }

  createTransceiver(kind, senderTrack = null) {
    const sender = {
      track: senderTrack,
      replaceTrack: async track => { sender.track = track },
      setStreams: () => {},
      getStats: async () => new Map()
    }
    const receiver = {
      track: new FakeTrack(kind, `receiver-${kind}-${this.transceivers.length}`),
      getStats: async () => new Map()
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
  async getStats() { return new Map() }
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

  close() { this.connectionState = 'closed' }
}

class FakeHubConnection {
  constructor(snapshots) {
    this.snapshots = [...snapshots]
    this.connectionId = 'B-connection'
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
    if (method === 'JoinVoiceRoom') return this.snapshots.shift()
    if (method === 'GetCallChatHistory') return []
    return null
  }

  emit(name, value) { this.handlers.get(name)?.(value) }

  async reconnect() {
    this.state = 'Reconnecting'
    this.reconnectingHandler?.()
    this.state = 'Connected'
    await this.reconnectedHandler?.()
  }
}

let currentHub = null
class FakeHubConnectionBuilder {
  withUrl() { return this }
  withAutomaticReconnect() { return this }
  configureLogging() { return this }
  build() { return currentHub }
}

globalThis.__receivePathRuntime = {
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

const servicePrelude = `
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
} = globalThis.__receivePathRuntime
`
const transformedService = `${servicePrelude}\n${serviceSource
  .split(/\r?\n/)
  .filter(line => !line.startsWith('import '))
  .join('\n')
  .replaceAll('import.meta.env', 'globalThis.__receivePathEnv')}`

globalThis.__receivePathEnv = { DEV: false, VITE_API_BASE_URL: 'https://api.test/api' }
globalThis.localStorage = { getItem: () => null }
globalThis.MediaStream = FakeMediaStream
globalThis.RTCPeerConnection = FakeRTCPeerConnection
const { createCallMediaSession } = await import(`data:text/javascript;base64,${Buffer.from(transformedService).toString('base64')}`)

const remoteStreamsRef = { value: new Map() }
globalThis.__receivePathView = { remoteStreamsRef }
const binderStart = viewSource.indexOf('const bindMediaElement =')
const binderEnd = viewSource.indexOf('const setPresentationVideoElement =', binderStart)
assert.ok(binderStart >= 0 && binderEnd > binderStart, 'real CollaborationChat media binding seam must remain available')
const binderPrelude = `
const traceWebRtcMedia = () => {}
const blockedMediaElements = new Set()
const localVideoElements = new Map()
const remoteVideoElements = new Map()
const remoteAudioElements = new Map()
const localCallStream = { value: null }
const callConnectionId = { value: 'B-connection' }
const remoteStreams = globalThis.__receivePathView.remoteStreamsRef
const presentationVideoElement = { value: null }
const activePresenterStream = () => null
const activePresenter = { value: null }
`
const transformedBinder = `${binderPrelude}\n${viewSource.slice(binderStart, binderEnd)}\nexport { setRemoteVideoElement, setRemoteAudioElement, syncCallVideoElements }`
const {
  setRemoteVideoElement,
  setRemoteAudioElement,
  syncCallVideoElements
} = await import(`data:text/javascript;base64,${Buffer.from(transformedBinder).toString('base64')}`)

const flushAsync = async () => {
  await new Promise(resolve => setImmediate(resolve))
  await new Promise(resolve => setImmediate(resolve))
}

const participant = connectionId => ({
  connectionId,
  userId: 'A-user',
  displayName: 'User A',
  microphoneEnabled: true,
  cameraEnabled: true,
  screenSharing: false
})

const snapshot = participants => ({
  roomId: 'room-1',
  aiState: { callSessionId: 'call-1' },
  participants,
  transcription: {}
})

const offer = connectionId => ({
  fromConnectionId: connectionId,
  description: { type: 'offer', sdp: 'test-sdp' },
  mediaSources: []
})

const deliverRemoteTrack = (pc, kind, id, transceiverIndex) => {
  const track = new FakeTrack(kind, id)
  pc.ontrack?.({
    streams: [new FakeMediaStream([track], `${id}-source-stream`)],
    track,
    transceiver: pc.transceivers[transceiverIndex]
  })
  return track
}

const createAppPath = () => {
  let callParticipants = []
  let renderedParticipant = null
  let audioElement = null
  let videoElement = null

  const unmount = () => {
    if (!renderedParticipant) return
    setRemoteAudioElement(null, renderedParticipant.connectionId, 'stage')
    setRemoteVideoElement(null, renderedParticipant.connectionId, 'stage')
    audioElement = null
    videoElement = null
  }

  const render = () => {
    const participantInCall = dedupeParticipantsByUser(callParticipants, 'B-connection')[0] || null
    const connectionChanged = participantInCall?.connectionId !== renderedParticipant?.connectionId
    if (connectionChanged) unmount()
    renderedParticipant = participantInCall
    if (!renderedParticipant) return
    const media = remoteStreamsRef.value.get(renderedParticipant.connectionId)
    const cameraVisible = renderedParticipant.cameraEnabled &&
      media?.cameraStream?.getVideoTracks?.().some(track => track.readyState === 'live') === true
    if (cameraVisible && !videoElement) {
      videoElement = new FakeMediaElement()
      setRemoteVideoElement(videoElement, renderedParticipant.connectionId, 'stage')
    }
    if (!cameraVisible && videoElement) {
      setRemoteVideoElement(null, renderedParticipant.connectionId, 'stage')
      videoElement = null
    }
    if (media && !audioElement) {
      audioElement = new FakeMediaElement()
      setRemoteAudioElement(audioElement, renderedParticipant.connectionId, 'stage')
    }
    if (!media && audioElement) {
      setRemoteAudioElement(null, renderedParticipant.connectionId, 'stage')
      audioElement = null
    }
  }

  return {
    onParticipants: async items => {
      callParticipants = dedupeParticipantsByUser(items, 'B-connection')
      render()
      syncCallVideoElements()
    },
    onRemoteStreams: async items => {
      remoteStreamsRef.value = items
      render()
      syncCallVideoElements()
    },
    state: () => ({ renderedParticipant, audioElement, videoElement })
  }
}

test('User B keeps User A audio and camera attached through replacement and a transient disconnect', async () => {
  FakeRTCPeerConnection.instances = []
  remoteStreamsRef.value = new Map()
  const app = createAppPath()
  currentHub = new FakeHubConnection([snapshot([participant('A-old')])])
  const session = createCallMediaSession({
    projectId: 'project-1',
    voiceChannelId: 'voice-1',
    initialMicrophoneStream: new FakeMediaStream([new FakeTrack('audio', 'B-mic')]),
    onParticipants: app.onParticipants,
    onRemoteStreams: app.onRemoteStreams
  })

  try {
    await session.start()
    currentHub.emit('WebRtcOffer', offer('A-old'))
    await flushAsync()
    const oldPeer = FakeRTCPeerConnection.instances[0]
    const oldAudio = deliverRemoteTrack(oldPeer, 'audio', 'A-old-audio', 0)
    const oldCamera = deliverRemoteTrack(oldPeer, 'video', 'A-old-camera', 1)
    await flushAsync()

    assert.equal(app.state().audioElement?.muted, false)
    assert.ok(app.state().audioElement?.volume > 0)
    assert.ok(app.state().audioElement?.srcObject?.getTracks().includes(oldAudio))
    assert.ok(app.state().videoElement?.srcObject?.getTracks().includes(oldCamera))

    currentHub.emit('ParticipantLeft', { connectionId: 'A-old' })
    currentHub.emit('ParticipantJoined', { participant: participant('A-new') })
    await flushAsync()
    currentHub.emit('WebRtcOffer', offer('A-new'))
    await flushAsync()
    const replacementPeer = FakeRTCPeerConnection.instances[1]
    const newAudio = deliverRemoteTrack(replacementPeer, 'audio', 'A-new-audio', 0)
    const newCamera = deliverRemoteTrack(replacementPeer, 'video', 'A-new-camera', 1)
    await flushAsync()

    let state = app.state()
    assert.equal(state.renderedParticipant?.connectionId, 'A-new')
    assert.equal(state.audioElement?.muted, false)
    assert.ok(state.audioElement?.volume > 0)
    assert.ok(state.audioElement?.srcObject?.getTracks().includes(newAudio))
    assert.ok(state.videoElement?.srcObject?.getTracks().includes(newCamera))
    assert.notEqual(state.audioElement, state.videoElement)
    assert.equal(oldPeer.connectionState, 'closed')

    replacementPeer.transitionConnection('disconnected')
    await new Promise(resolve => setTimeout(resolve, 350))
    state = app.state()
    assert.deepEqual({
      renderedConnectionId: state.renderedParticipant?.connectionId || '',
      activeStreamConnectionIds: [...remoteStreamsRef.value.keys()],
      audioAttached: Boolean(state.audioElement?.srcObject?.getTracks().includes(newAudio)),
      cameraAttached: Boolean(state.videoElement?.srcObject?.getTracks().includes(newCamera)),
      peerConnectionCount: FakeRTCPeerConnection.instances.length,
      activePeerState: replacementPeer.connectionState
    }, {
      renderedConnectionId: 'A-new',
      activeStreamConnectionIds: ['A-new'],
      audioAttached: true,
      cameraAttached: true,
      peerConnectionCount: 2,
      activePeerState: 'disconnected'
    })
    assert.equal(state.audioElement.muted, false)
    assert.ok(state.audioElement.volume > 0)

    replacementPeer.transitionConnection('connected')
    await flushAsync()
    state = app.state()
    assert.ok(state.audioElement.srcObject.getTracks().includes(newAudio))
    assert.ok(state.videoElement.srcObject.getTracks().includes(newCamera))
  } finally {
    await session.leave()
  }
})
