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
    this.paused = true
    this.playsInline = false
    this.srcObject = null
    this.volume = 1
  }

  play() {
    this.paused = false
    return Promise.resolve()
  }
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
    this.invocations = []
  }

  on(name, handler) { this.handlers.set(name, handler) }
  onreconnecting(handler) { this.reconnectingHandler = handler }
  onreconnected(handler) { this.reconnectedHandler = handler }
  onclose(handler) { this.closeHandler = handler }
  async start() { this.state = 'Connected' }
  async stop() { this.state = 'Disconnected' }

  async invoke(method, ...args) {
    this.invocations.push({ method, args })
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
const visibilityStart = viewSource.indexOf('const hasLiveVideoTrack =')
const visibilityEnd = viewSource.indexOf('const pictureInPictureUnsupportedMessage', visibilityStart)
const binderStart = viewSource.indexOf('const bindMediaElement =')
const binderEnd = viewSource.indexOf('const setPresentationVideoElement =', binderStart)
assert.ok(visibilityStart >= 0 && visibilityEnd > visibilityStart, 'real CollaborationChat remote camera visibility seam must remain available')
assert.ok(binderStart >= 0 && binderEnd > binderStart, 'real CollaborationChat media binding seam must remain available')
const binderPrelude = `
const traceWebRtcMedia = () => {}
const blockedMediaElements = new Set()
const localVideoElements = new Map()
const remoteVideoElements = new Map()
const remoteAudioElements = new Map()
const localCallStream = { value: null }
const callConnectionId = { value: 'B-connection' }
const isCallCameraOn = { value: false }
const remoteStreams = globalThis.__receivePathView.remoteStreamsRef
const presentationVideoElement = { value: null }
const activePresenterStream = () => null
const activePresenter = { value: null }
`
const transformedBinder = `${binderPrelude}\n${viewSource.slice(visibilityStart, visibilityEnd)}\n${viewSource.slice(binderStart, binderEnd)}\nexport { isParticipantVideoVisible, setRemoteVideoElement, setRemoteAudioElement, syncCallVideoElements }`
const {
  isParticipantVideoVisible,
  setRemoteVideoElement,
  setRemoteAudioElement,
  syncCallVideoElements
} = await import(`data:text/javascript;base64,${Buffer.from(transformedBinder).toString('base64')}`)

const flushAsync = async () => {
  await new Promise(resolve => setImmediate(resolve))
  await new Promise(resolve => setImmediate(resolve))
}

const deferred = () => {
  let resolve
  const promise = new Promise(complete => { resolve = complete })
  return { promise, resolve }
}

const participant = (connectionId, cameraEnabled = true) => ({
  connectionId,
  userId: 'A-user',
  displayName: 'User A',
  microphoneEnabled: true,
  cameraEnabled,
  screenSharing: false
})

const snapshot = participants => ({
  roomId: 'room-1',
  aiState: { callSessionId: 'call-1' },
  participants,
  transcription: {}
})

const publishedMediaStates = hub => hub.invocations
  .filter(invocation => invocation.method === 'PublishParticipantMediaState')
  .map(invocation => invocation.args[1])

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
    const cameraVisible = isParticipantVideoVisible(renderedParticipant)
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

test('User B renders live User A audio and camera despite stale signaling state and through reconnect', async () => {
  FakeRTCPeerConnection.instances = []
  remoteStreamsRef.value = new Map()
  const app = createAppPath()
  currentHub = new FakeHubConnection([snapshot([participant('A-old', false)])])
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
    currentHub.emit('ParticipantJoined', { participant: participant('A-new', false) })
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

test('prejoin microphone and camera tracks are published after joining', async () => {
  FakeRTCPeerConnection.instances = []
  const microphoneTrack = new FakeTrack('audio', 'B-prejoin-mic')
  const cameraTrack = new FakeTrack('video', 'B-prejoin-camera')
  currentHub = new FakeHubConnection([snapshot([])])
  const session = createCallMediaSession({
    projectId: 'project-1',
    voiceChannelId: 'voice-1',
    initialMicrophoneEnabled: true,
    initialMicrophoneStream: new FakeMediaStream([microphoneTrack]),
    initialCameraEnabled: true,
    initialCameraStream: new FakeMediaStream([cameraTrack])
  })

  try {
    await session.start()
    assert.deepEqual(publishedMediaStates(currentHub), [{
      microphoneEnabled: true,
      cameraEnabled: true,
      screenSharing: false
    }])
  } finally {
    await session.leave()
  }
})

test('reconnect rejoins idempotently without replacing a healthy peer or local tracks', async () => {
  FakeRTCPeerConnection.instances = []
  const microphoneTrack = new FakeTrack('audio', 'B-reconnect-mic')
  const cameraTrack = new FakeTrack('video', 'B-reconnect-camera')
  const remoteParticipant = participant('A-connection')
  currentHub = new FakeHubConnection([
    snapshot([remoteParticipant]),
    snapshot([remoteParticipant])
  ])
  const session = createCallMediaSession({
    projectId: 'project-1',
    voiceChannelId: 'voice-1',
    initialMicrophoneEnabled: true,
    initialMicrophoneStream: new FakeMediaStream([microphoneTrack]),
    initialCameraEnabled: true,
    initialCameraStream: new FakeMediaStream([cameraTrack])
  })

  try {
    await session.start()
    const healthyPeer = FakeRTCPeerConnection.instances[0]
    healthyPeer.transitionConnection('connected')

    currentHub.state = 'Reconnecting'
    currentHub.reconnectingHandler?.()
    currentHub.state = 'Connected'
    await Promise.all([
      currentHub.reconnectedHandler?.(),
      currentHub.reconnectedHandler?.()
    ])

    assert.equal(FakeRTCPeerConnection.instances.length, 1)
    assert.equal(currentHub.invocations.filter(invocation => invocation.method === 'JoinVoiceRoom').length, 2)
    assert.equal(healthyPeer.connectionState, 'connected')
    assert.equal(microphoneTrack.readyState, 'live')
    assert.equal(cameraTrack.readyState, 'live')
    assert.deepEqual(publishedMediaStates(currentHub), [
      { microphoneEnabled: true, cameraEnabled: true, screenSharing: false },
      { microphoneEnabled: true, cameraEnabled: true, screenSharing: false }
    ])
  } finally {
    await session.leave()
  }
})

test('reconnect publishes ended local tracks as disabled', async () => {
  FakeRTCPeerConnection.instances = []
  const microphoneTrack = new FakeTrack('audio', 'B-ended-mic')
  const cameraTrack = new FakeTrack('video', 'B-ended-camera')
  currentHub = new FakeHubConnection([snapshot([]), snapshot([])])
  const session = createCallMediaSession({
    projectId: 'project-1',
    voiceChannelId: 'voice-1',
    initialMicrophoneStream: new FakeMediaStream([microphoneTrack]),
    initialCameraEnabled: true,
    initialCameraStream: new FakeMediaStream([cameraTrack])
  })

  try {
    await session.start()
    microphoneTrack.readyState = 'ended'
    cameraTrack.readyState = 'ended'

    await currentHub.reconnect()

    assert.deepEqual(publishedMediaStates(currentHub).at(-1), {
      microphoneEnabled: false,
      cameraEnabled: false,
      screenSharing: false
    })
  } finally {
    await session.leave()
  }
})

test('leaving and entering a second call does not reuse peer or local track state', async () => {
  FakeRTCPeerConnection.instances = []
  const firstMicrophoneTrack = new FakeTrack('audio', 'first-call-mic')
  currentHub = new FakeHubConnection([snapshot([participant('first-peer')])])
  const firstSession = createCallMediaSession({
    projectId: 'project-1',
    voiceChannelId: 'voice-1',
    initialMicrophoneStream: new FakeMediaStream([firstMicrophoneTrack])
  })
  await firstSession.start()
  const firstPeer = FakeRTCPeerConnection.instances[0]

  const secondMicrophoneTrack = new FakeTrack('audio', 'second-call-mic')
  currentHub = new FakeHubConnection([snapshot([participant('second-peer')])])
  const secondSession = createCallMediaSession({
    projectId: 'project-2',
    voiceChannelId: 'voice-2',
    initialMicrophoneStream: new FakeMediaStream([secondMicrophoneTrack])
  })

  try {
    await secondSession.start()
    assert.equal(firstPeer.connectionState, 'closed')
    assert.equal(firstMicrophoneTrack.readyState, 'ended')
    assert.equal(secondMicrophoneTrack.readyState, 'live')
    assert.deepEqual(secondSession.getPeerDiagnostics().map(peer => peer.connectionId), ['second-peer'])
  } finally {
    await secondSession.leave()
  }

  assert.equal(secondMicrophoneTrack.readyState, 'ended')
  assert.deepEqual(secondSession.getPeerDiagnostics(), [])
})

test('a delayed reconnect cannot repopulate a call after a second session starts', async () => {
  FakeRTCPeerConnection.instances = []
  const delayedSnapshot = deferred()
  const firstMicrophoneTrack = new FakeTrack('audio', 'delayed-first-call-mic')
  const firstHub = new FakeHubConnection([snapshot([]), delayedSnapshot.promise])
  currentHub = firstHub
  const firstSession = createCallMediaSession({
    projectId: 'project-1',
    voiceChannelId: 'voice-1',
    initialMicrophoneStream: new FakeMediaStream([firstMicrophoneTrack])
  })
  await firstSession.start()
  firstHub.state = 'Connected'
  const reconnectRun = firstHub.reconnectedHandler?.()

  const secondMicrophoneTrack = new FakeTrack('audio', 'delayed-second-call-mic')
  currentHub = new FakeHubConnection([snapshot([])])
  const secondSession = createCallMediaSession({
    projectId: 'project-2',
    voiceChannelId: 'voice-2',
    initialMicrophoneStream: new FakeMediaStream([secondMicrophoneTrack])
  })

  try {
    await secondSession.start()
    delayedSnapshot.resolve(snapshot([participant('stale-first-peer')]))
    await reconnectRun

    assert.equal(firstMicrophoneTrack.readyState, 'ended')
    assert.deepEqual(firstSession.getPeerDiagnostics(), [])
    assert.equal(secondMicrophoneTrack.readyState, 'live')
  } finally {
    delayedSnapshot.resolve(snapshot([]))
    await secondSession.leave()
  }
})
