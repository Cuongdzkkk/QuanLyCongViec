import * as signalR from '@microsoft/signalr'
import axiosClient from '@/api/axiosClient'
import { getStoredAccessToken } from '@/utils/authSession'
import { createBackgroundBlurProcessor } from '@/services/cameraBackgroundEffect'
import { configureRealtimeHub } from '@/services/realtimeHubConfig'
import { launchCaptionTransportClientDiagnostic } from '@/services/captionTransportDiagnostics'
import { createBoundedAsyncQueue } from '@/services/captionTransportQueue'

const HUB_ROUTE = '/hubs/call'
const MAX_RECOVERY_ATTEMPTS = 2
let activeCallSession = null
let nextCallHubInstanceId = 0

const BASE64_CHUNK_SIZE = 0x8000
export const CAPTION_CHUNK_BYTES = 4000
export const CAPTION_CHUNK_DURATION_MS = CAPTION_CHUNK_BYTES / 2 / 16
export const CAPTION_MAX_PENDING_CHUNKS = 4

export const encodePcmChunkBase64 = bytes => {
  let binary = ''
  for (let offset = 0; offset < bytes.length; offset += BASE64_CHUNK_SIZE) {
    binary += String.fromCharCode(...bytes.subarray(offset, offset + BASE64_CHUNK_SIZE))
  }
  return btoa(binary)
}

const callHubTraceEnabled = () => {
  try {
    return Boolean(import.meta.env?.DEV || globalThis.localStorage?.getItem('debug_call_hub') === '1')
  } catch {
    return false
  }
}

const webRtcMediaTraceEnabled = () => {
  try {
    return Boolean(import.meta.env?.DEV || globalThis.localStorage?.getItem('debug_webrtc_media') === '1')
  } catch {
    return false
  }
}

const captionTransportTraceEnabled = () => {
  try {
    return globalThis.localStorage?.getItem('debug_caption_transport') === '1'
  } catch {
    return false
  }
}

const traceCaptionTransport = (event, detail = {}) => {
  if (!captionTransportTraceEnabled()) return
  console.info('[CAPTION_TRACE]', {
    timestamp: new Date().toISOString(),
    event,
    ...detail
  })
}

const traceCaptionSource = (event, detail = {}) => {
  if (!captionTransportTraceEnabled()) return
  console.info('[CAPTION_SOURCE_DIAG]', {
    timestamp: new Date().toISOString(),
    event,
    ...detail
  })
}

const safeCaptionErrorMessage = error => {
  const message = `${error?.message || 'Unknown SignalR error'}`
    .replace(/access_token=[^&\s"']+/gi, 'access_token=[redacted]')
    .replace(/https?:\/\/[^\s"']+/gi, '[url-redacted]')
  return message.length <= 256 ? message : `${message.slice(0, 253)}...`
}

export const traceCallHubLifecycle = (event, detail = {}) => {
  if (!callHubTraceEnabled()) return
  console.info('[CALL_HUB]', {
    timestamp: new Date().toISOString(),
    event,
    ...detail
  })
}

export const traceWebRtcMedia = (event, detail = {}) => {
  if (!webRtcMediaTraceEnabled()) return
  console.info('[WEBRTC_MEDIA]', {
    timestamp: new Date().toISOString(),
    event,
    peerId: detail.peerId || '',
    connectionState: detail.connectionState || 'unknown',
    iceConnectionState: detail.iceConnectionState || 'unknown',
    signalingState: detail.signalingState || 'unknown',
    trackKind: detail.trackKind || '',
    trackId: detail.trackId || '',
    trackReadyState: detail.trackReadyState || '',
    mediaRole: detail.mediaRole || '',
    streamId: detail.streamId || ''
  })
}

const read = (value, camel, pascal) => value?.[camel] ?? value?.[pascal]

const normalizeParticipant = (value) => ({
  connectionId: read(value, 'connectionId', 'ConnectionId'),
  userId: read(value, 'userId', 'UserId'),
  displayName: read(value, 'displayName', 'DisplayName') || 'SprintA user',
  avatarUrl: read(value, 'avatarUrl', 'AvatarUrl') || null,
  microphoneEnabled: read(value, 'microphoneEnabled', 'MicrophoneEnabled') !== false,
  cameraEnabled: read(value, 'cameraEnabled', 'CameraEnabled') === true,
  screenSharing: read(value, 'screenSharing', 'ScreenSharing') === true
  ,handRaised: read(value, 'handRaised', 'HandRaised') === true
  ,isSpeaking: read(value, 'isSpeaking', 'IsSpeaking') === true
  ,role: read(value, 'role', 'Role') || 'participant'
})

const mediaError = (error, fallbackCode) => {
  const wrapped = new Error(error?.message || fallbackCode)
  wrapped.code = error?.name === 'NotAllowedError' ? 'PERMISSION_DENIED'
    : error?.name === 'NotFoundError' ? 'DEVICE_NOT_FOUND'
      : error?.name === 'NotReadableError' ? 'DEVICE_BUSY'
        : fallbackCode
  return wrapped
}

const normalizeBackgroundEffect = value => value === 'blur' ? 'blur' : 'none'

const describeTrack = track => track ? {
  id: track.id || '',
  kind: track.kind || '',
  enabled: track.enabled !== false,
  muted: track.muted === true,
  readyState: track.readyState || 'unknown'
} : null

export const inspectPeerConnection = (connectionId, pc) => ({
  connectionId,
  connectionState: pc?.connectionState || 'unknown',
  iceConnectionState: pc?.iceConnectionState || 'unknown',
  signalingState: pc?.signalingState || 'unknown',
  senders: (pc?.getSenders?.() || []).map(sender => describeTrack(sender.track)),
  receivers: (pc?.getReceivers?.() || []).map(receiver => describeTrack(receiver.track)),
  transceivers: (pc?.getTransceivers?.() || []).map(transceiver => ({
    mid: transceiver.mid ?? null,
    direction: transceiver.direction || '',
    currentDirection: transceiver.currentDirection || null,
    sender: describeTrack(transceiver.sender?.track),
    receiver: describeTrack(transceiver.receiver?.track)
  }))
})

export const classifyRemoteMediaRole = (entry, transceiver, track, streams = []) => {
  if (track?.kind === 'audio') return 'audio'
  if (transceiver === entry?.screenTransceiver) return 'screen'
  if (transceiver === entry?.cameraTransceiver) return 'camera'
  if (transceiver?.mid && transceiver.mid === entry?.screenTransceiver?.mid) return 'screen'
  if (transceiver?.mid && transceiver.mid === entry?.cameraTransceiver?.mid) return 'camera'
  const source = (transceiver?.mid && entry?.remoteMediaSourcesByMid?.get(transceiver.mid))
    || streams.map(stream => entry?.remoteMediaSources?.get(stream?.id)).find(Boolean)
    || entry?.remoteMediaSourcesByTrackId?.get(track?.id)
  return source === 'screen' ? 'screen' : 'camera'
}

const getIceServers = async () => {
  const response = await axiosClient.get('/webrtc/ice-servers')
  const payload = response?.data?.data ?? response?.data ?? {}
  return (payload.iceServers ?? payload.IceServers ?? []).map(server => ({
    urls: server.urls ?? server.Urls,
    ...(server.username || server.Username ? { username: server.username ?? server.Username } : {}),
    ...(server.credential || server.Credential ? { credential: server.credential ?? server.Credential } : {})
  }))
}

export const createCallMediaSession = ({ projectId, voiceChannelId, onState, onParticipants, onRemoteStreams, onAiState, onTranscriptChunk, onTranscriptInterim, onTranscriptionError, onTranscriptionCapabilities, onHandChanged, onReaction, onSpeakerChanged, onForceMute, onForceRemoved, onCallMessage, onCallHistory, initialMicrophoneEnabled = true, initialMicrophoneStream = null, initialCameraEnabled = false, initialCameraStream = null, initialMicrophoneDeviceId = '', initialCameraDeviceId = '' }) => {
  let connection = null
  let startPromise = null
  let leavePromise = null
  let roomId = null
  let localStream = null
  let localMicrophoneGeneration = 0
  let cameraStream = null
  let screenStream = null
  let rawCameraTrack = null
  let cameraTrack = null
  let screenTrack = null
  let backgroundProcessor = null
  let backgroundEffect = 'none'
  let microphoneEnabled = true
  let cameraEnabled = false
  let screenSharing = false
  let intentionalLeave = false
  let recoveryAttempts = 0
  let iceServers = []
  let joinedAck = false
  let callSessionId = null
  const pendingInboundSignals = []
  let participants = new Map()
  const peers = new Map()
  const remoteStreams = new Map()
  let transcriptionCapture = null
  const transcriptionQueue = createBoundedAsyncQueue({
    maxPending: CAPTION_MAX_PENDING_CHUNKS,
    onDrop: ({ pendingCount, maxPending }) => traceCaptionTransport('QUEUE_DROP', {
      pendingCount,
      maxPending,
      reason: 'stale-audio-backpressure'
    })
  })
  let transcriptionLanguage = 'vi'
  let selectedMicrophoneDeviceId = initialMicrophoneDeviceId || ''
  let selectedCameraDeviceId = initialCameraDeviceId || ''
  const instanceId = `call-hub-${++nextCallHubInstanceId}`
  const trace = (event, reason = '') => traceCallHubLifecycle(event, {
    instanceId,
    connectionId: connection?.connectionId || '',
    state: connection?.state || 'Disconnected',
    roomId: roomId || '',
    callSessionId: callSessionId || '',
    ...(reason ? { reason } : {})
  })
  trace('INSTANCE_CREATE', 'create-call-media-session')

  const emit = (state, detail = {}) => onState?.({ state, ...detail })
  const emitParticipants = () => onParticipants?.([...participants.values()])
  const emitRemoteStreams = () => onRemoteStreams?.(new Map(remoteStreams))
  const emitAiState = value => {
    const nested = read(value, 'state', 'State')
    onAiState?.(nested && typeof nested === 'object' ? nested : value)
  }

  const getCurrentMicrophoneTrack = () => localStream?.getAudioTracks?.()[0] || null

  const captionSourceMatchesCurrentMicrophone = capture => {
    if (!capture) return null
    return capture.localStream === localStream && capture.sourceTrack === getCurrentMicrophoneTrack()
  }

  const describeCaptionSource = capture => {
    const track = getCurrentMicrophoneTrack()
    return {
      audioContextSampleRate: capture?.context?.sampleRate ?? null,
      audioTrackReadyState: track?.readyState || 'none',
      audioTrackEnabled: track ? track.enabled !== false : null,
      audioTrackMuted: track?.muted === true,
      localStreamAudioTrackCount: localStream?.getAudioTracks?.().length || 0,
      sourceBelongsToCurrentLocalStream: captionSourceMatchesCurrentMicrophone(capture),
      sourceGeneration: capture?.sourceGeneration ?? localMicrophoneGeneration
    }
  }

  const setLocalMicrophoneStream = (stream, reason) => {
    const previous = localStream
    if (previous === stream) return previous
    const oldCaptionSourceGeneration = transcriptionCapture?.sourceGeneration ?? null
    localStream = stream
    localMicrophoneGeneration += 1
    traceCaptionSource('SOURCE_CHANGED', {
      reason,
      oldCaptionSourceGeneration,
      newLocalMicrophoneGeneration: localMicrophoneGeneration,
      activeCaptionAudioNodeRebuilt: false,
      captionCapturePointsToCurrentLocalMicrophoneSource: captionSourceMatchesCurrentMicrophone(transcriptionCapture),
      ...describeCaptionSource(transcriptionCapture)
    })
    return previous
  }

  const downsampleToPcm16 = (input, inputRate, outputRate = 16000) => {
    const ratio = inputRate / outputRate
    const outputLength = Math.max(1, Math.floor(input.length / ratio))
    const output = new Uint8Array(outputLength * 2)
    const view = new DataView(output.buffer)
    for (let index = 0; index < outputLength; index += 1) {
      const sourceIndex = Math.min(input.length - 1, Math.floor(index * ratio))
      const sample = Math.max(-1, Math.min(1, input[sourceIndex]))
      view.setInt16(index * 2, sample < 0 ? sample * 0x8000 : sample * 0x7fff, true)
    }
    return output
  }

  const sendTranscriptionChunk = (capture, bytes, startedAt, endedAt) => {
    if (!capture.active || !connection || connection.state !== signalR.HubConnectionState.Connected || !roomId) return
    const payload = encodePcmChunkBase64(bytes)
    const queuedAt = performance.now()
    void transcriptionQueue.enqueue(async () => {
        if (!capture.active || connection?.state !== signalR.HubConnectionState.Connected || capture !== transcriptionCapture) return
        const chunkIndex = ++capture.transportChunkIndex
        const started = performance.now()
        traceCaptionTransport('SEND_BEGIN', {
          connectionState: connection.state,
          callSessionIdPresent: capture.callSessionId ? 'YES' : 'NO',
          projectIdPresent: projectId ? 'YES' : 'NO',
          voiceChannelIdPresent: voiceChannelId ? 'YES' : 'NO',
          language: capture.language,
          sampleRate: 16000,
          channelCount: 1,
          chunkBytes: bytes.byteLength,
          chunkIndex,
          queueWaitMs: Math.max(0, Math.round(started - queuedAt))
        })
        try {
          launchCaptionTransportClientDiagnostic({
            bytes,
            chunkIndex,
            callSessionId: capture.callSessionId,
            projectId,
            voiceChannelId,
            enabled: captionTransportTraceEnabled()
          })
          await connection.invoke(
            'SubmitCallAudioChunk',
            roomId,
            capture.callSessionId,
            capture.consentGeneration,
            'audio/linear16;rate=16000;channels=1',
            payload,
            startedAt.toISOString(),
            endedAt.toISOString(),
            capture.language)
          traceCaptionTransport('SEND_OK', { elapsedMs: Math.round(performance.now() - started) })
        } catch (error) {
          traceCaptionTransport('SEND_FAIL', {
            errorName: error?.name || 'Error',
            errorMessage: safeCaptionErrorMessage(error),
            connectionState: connection?.state || 'unknown',
            elapsedMs: Math.round(performance.now() - started)
          })
          if (!['AI_TRANSCRIPTION_NOT_ACTIVE', 'CALL_TRANSCRIPTION_NOT_CONFIGURED'].includes(error?.message)) onTranscriptionError?.({ code: 'INGEST_ERROR', message: 'Không thể gửi âm thanh cho biên bản cuộc gọi.' })
        }
      }, { capture })
  }

  const stopTranscriptionCapture = async ({ notifyServer = true } = {}) => {
    const capture = transcriptionCapture
    transcriptionCapture = null
    if (!capture) return
    traceCaptionSource('CAPTURE_STOP', {
      ...describeCaptionSource(capture),
      sourceGeneration: capture.sourceGeneration,
      activeCaptionAudioNodeRebuilt: false,
      captionCapturePointsToCurrentLocalMicrophoneSource: captionSourceMatchesCurrentMicrophone(capture)
    })
    capture.active = false
    transcriptionQueue.clear(metadata => metadata.capture === capture)
    capture.processor.onaudioprocess = null
    capture.source.disconnect()
    capture.processor.disconnect()
    capture.sink.disconnect()
    await capture.context.close().catch(() => {})
    if (notifyServer && connection?.state === signalR.HubConnectionState.Connected && roomId) {
      await connection.invoke('StopCallAudioStream', roomId, capture.callSessionId, capture.consentGeneration).catch(() => {})
    }
  }

  const startTranscriptionCapture = async aiState => {
    const callSessionId = read(aiState, 'callSessionId', 'CallSessionId')
    const consentGeneration = read(aiState, 'consentGeneration', 'ConsentGeneration')
    if (!callSessionId || !consentGeneration || !localStream || !microphoneEnabled) return
    if (transcriptionCapture?.callSessionId === callSessionId && transcriptionCapture?.consentGeneration === consentGeneration) return
    await stopTranscriptionCapture()
    const AudioContextCtor = window.AudioContext || window.webkitAudioContext
    if (!AudioContextCtor) {
      onTranscriptionError?.({ code: 'UNSUPPORTED_AUDIO_CAPTURE', message: 'Trình duyệt không hỗ trợ thu âm cuộc gọi cho biên bản.' })
      return
    }
    const context = new AudioContextCtor()
    const source = context.createMediaStreamSource(localStream)
    const sourceTrack = getCurrentMicrophoneTrack()
    const processor = context.createScriptProcessor(4096, 1, 1)
    const sink = context.createGain()
    sink.gain.value = 0
    const capture = {
      active: true,
      callSessionId,
      consentGeneration,
      language: transcriptionLanguage,
      context,
      source,
      sourceTrack,
      localStream,
      sourceGeneration: localMicrophoneGeneration,
      processor,
      sink,
      pending: [],
      preRoll: [],
      speaking: false,
      silenceChunks: 0,
      transportChunkIndex: 0
    }
    transcriptionCapture = capture
    traceCaptionSource('CAPTURE_START', {
      ...describeCaptionSource(capture),
      sourceGeneration: capture.sourceGeneration,
      activeCaptionAudioNodeRebuilt: false,
      captionCapturePointsToCurrentLocalMicrophoneSource: captionSourceMatchesCurrentMicrophone(capture)
    })
    processor.onaudioprocess = event => {
      if (!capture.active || !microphoneEnabled) return
      const pcm = downsampleToPcm16(event.inputBuffer.getChannelData(0), context.sampleRate)
      capture.pending.push(pcm)
      let pendingLength = capture.pending.reduce((total, item) => total + item.length, 0)
      while (pendingLength >= CAPTION_CHUNK_BYTES && capture.active) {
        const chunk = new Uint8Array(CAPTION_CHUNK_BYTES)
        let offset = 0
        while (offset < chunk.length && capture.pending.length) {
          const first = capture.pending[0]
          const copyLength = Math.min(first.length, chunk.length - offset)
          chunk.set(first.subarray(0, copyLength), offset)
          offset += copyLength
          if (copyLength === first.length) capture.pending.shift()
          else capture.pending[0] = first.subarray(copyLength)
        }
        pendingLength -= CAPTION_CHUNK_BYTES
        let energy = 0
        const view = new DataView(chunk.buffer)
        for (let index = 0; index < chunk.length; index += 2) {
          const sample = view.getInt16(index, true) / 0x8000
          energy += sample * sample
        }
        const voiced = Math.sqrt(energy / (chunk.length / 2)) >= 0.012
        const endedAt = new Date()
        const startedAt = new Date(endedAt.getTime() - CAPTION_CHUNK_DURATION_MS)
        capture.preRoll.push({ chunk, startedAt, endedAt })
        if (capture.preRoll.length > 2) capture.preRoll.shift()
        if (voiced) {
          if (!capture.speaking) {
            capture.speaking = true
            for (const buffered of capture.preRoll) sendTranscriptionChunk(capture, buffered.chunk, buffered.startedAt, buffered.endedAt)
          } else sendTranscriptionChunk(capture, chunk, startedAt, endedAt)
          capture.silenceChunks = 0
        } else if (capture.speaking) {
          sendTranscriptionChunk(capture, chunk, startedAt, endedAt)
          capture.silenceChunks += 1
          if (capture.silenceChunks >= 2) {
            capture.speaking = false
            capture.preRoll = []
          }
        }
      }
    }
    source.connect(processor)
    processor.connect(sink)
    sink.connect(context.destination)
    await context.resume().catch(() => {})
  }

  const handleAiState = value => {
    const nested = read(value, 'state', 'State')
    const state = nested && typeof nested === 'object' ? nested : value
    emitAiState(state)
    const stateName = read(state, 'state', 'State')
    if (stateName === 'ACTIVE') void startTranscriptionCapture(state)
    else void stopTranscriptionCapture()
  }

  const localConnectionId = () => connection?.connectionId
  const getPeer = (connectionId) => peers.get(connectionId)
  const isPolite = (remoteConnectionId) => `${localConnectionId()}` > `${remoteConnectionId}`

  const sendMediaState = async () => {
    if (!connection || !roomId || connection.state !== signalR.HubConnectionState.Connected) return
    await connection.invoke('PublishParticipantMediaState', roomId, {
      microphoneEnabled,
      cameraEnabled,
      screenSharing
    })
  }

  const emptyRemoteMedia = () => ({
    audioStream: new MediaStream(),
    cameraStream: new MediaStream(),
    screenStream: new MediaStream()
  })

  const peerDiagnostic = (entry, detail = {}) => ({
    peerId: entry?.connectionId || detail.peerId || '',
    connectionState: entry?.pc?.connectionState || detail.connectionState || 'unknown',
    iceConnectionState: entry?.pc?.iceConnectionState || detail.iceConnectionState || 'unknown',
    signalingState: entry?.pc?.signalingState || detail.signalingState || 'unknown',
    ...detail
  })

  const removeRemoteTrack = (connectionId, source, track) => {
    const media = remoteStreams.get(connectionId)
    const stream = media?.[`${source}Stream`]
    if (!stream || !stream.getTracks().includes(track)) return
    stream.removeTrack(track)
    traceWebRtcMedia('TRACK_ENDED', {
      peerId: connectionId,
      trackKind: track.kind,
      trackId: track.id,
      trackReadyState: track.readyState,
      mediaRole: source,
      streamId: stream.id
    })
    emitRemoteStreams()
  }

  const addRemoteTrack = (connectionId, source, track, entry) => {
    if (!source || !track) return
    const isNewPeerMedia = !remoteStreams.has(connectionId)
    const media = remoteStreams.get(connectionId) || emptyRemoteMedia()
    const stream = media[`${source}Stream`]
    const previousTrack = stream.getTracks().find(item => item.kind === track.kind)
    if (previousTrack && previousTrack !== track) stream.removeTrack(previousTrack)
    if (!stream.getTracks().includes(track)) stream.addTrack(track)
    track.onended = () => removeRemoteTrack(connectionId, source, track)
    remoteStreams.set(connectionId, media)
    if (isNewPeerMedia) traceWebRtcMedia('REMOTE_STREAM_CREATED', peerDiagnostic(entry, { streamId: stream.id, mediaRole: source }))
    traceWebRtcMedia(source === 'screen' ? 'REMOTE_SCREEN_STREAM_ASSIGNED' : source === 'camera' ? 'REMOTE_CAMERA_STREAM_ASSIGNED' : 'REMOTE_STREAM_CREATED', peerDiagnostic(entry, {
      trackKind: track.kind,
      trackId: track.id,
      trackReadyState: track.readyState,
      mediaRole: source,
      streamId: stream.id
    }))
    emitRemoteStreams()
  }

  const updateRemoteStreams = (connectionId, entry, streams, track, transceiver) => {
    const incomingStreams = streams ?? []
    const source = classifyRemoteMediaRole(entry, transceiver, track, incomingStreams)
    traceWebRtcMedia('REMOTE_TRACK_RECEIVED', peerDiagnostic(entry, {
      trackKind: track?.kind,
      trackId: track?.id,
      trackReadyState: track?.readyState,
      mediaRole: source,
      streamId: incomingStreams[0]?.id || ''
    }))
    traceWebRtcMedia('REMOTE_TRACK_CLASSIFIED', peerDiagnostic(entry, {
      trackKind: track?.kind,
      trackId: track?.id,
      trackReadyState: track?.readyState,
      mediaRole: source,
      streamId: incomingStreams[0]?.id || ''
    }))
    if (track) addRemoteTrack(connectionId, source, track, entry)
  }

  const closePeer = (connectionId) => {
    const peer = peers.get(connectionId)
    if (!peer) return
    peer.pc.onicecandidate = null
    peer.pc.ontrack = null
    peer.pc.onconnectionstatechange = null
    peer.pc.onnegotiationneeded = null
    peer.pc.close()
    peers.delete(connectionId)
    remoteStreams.delete(connectionId)
    emitRemoteStreams()
  }

  const closeAllPeers = () => [...peers.keys()].forEach(closePeer)

  const sendSignal = async (method, targetConnectionId, payload) => {
    if (!joinedAck || !connection || connection.state !== signalR.HubConnectionState.Connected || !roomId) return
    try {
      await connection.invoke(method, roomId, targetConnectionId, payload)
    } catch (error) {
      if (!intentionalLeave && method !== 'SendIceCandidate') emit('error', { error, silent: true })
    }
  }

  const negotiate = async (entry) => {
    if (entry.makingOffer || entry.pc.signalingState !== 'stable') return
    try {
      entry.makingOffer = true
      await entry.pc.setLocalDescription()
      await sendSignal('SendWebRtcOffer', entry.connectionId, {
        description: entry.pc.localDescription,
        mediaSources: getLocalMediaSources(entry)
      })
    } finally {
      entry.makingOffer = false
    }
  }

  const getLocalMediaSources = entry => [
    cameraStream && { streamId: cameraStream.id, trackId: cameraTrack?.id, mid: entry?.cameraTransceiver?.mid || null, source: 'camera' },
    screenStream && { streamId: screenStream.id, trackId: screenTrack?.id, mid: entry?.screenTransceiver?.mid || null, source: 'screen' }
  ].filter(Boolean)

  const syncVideoSender = async (entry, senderKey, track, stream, mediaRole) => {
    const transceiver = entry[senderKey]
    if (!transceiver) return
    if (transceiver.sender.track !== track) await transceiver.sender.replaceTrack(track)
    if (typeof transceiver.sender.setStreams === 'function') transceiver.sender.setStreams(...(track && stream ? [stream] : []))
    transceiver.direction = track ? 'sendrecv' : 'recvonly'
    traceWebRtcMedia('SENDER_ATTACHED', peerDiagnostic(entry, {
      trackKind: track?.kind,
      trackId: track?.id,
      trackReadyState: track?.readyState,
      mediaRole,
      streamId: stream?.id || ''
    }))
    traceWebRtcMedia('TRANSCEIVER_STATE', peerDiagnostic(entry, {
      trackKind: track?.kind,
      trackId: track?.id,
      trackReadyState: track?.readyState,
      mediaRole,
      streamId: stream?.id || ''
    }))
  }

  const syncPeerMedia = async entry => {
    const audioTrack = localStream?.getAudioTracks?.()[0] || null
    if (entry.audioTransceiver?.sender.track !== audioTrack) await entry.audioTransceiver.sender.replaceTrack(audioTrack)
    if (typeof entry.audioTransceiver?.sender.setStreams === 'function') entry.audioTransceiver.sender.setStreams(...(audioTrack && localStream ? [localStream] : []))
    if (entry.audioTransceiver) entry.audioTransceiver.direction = audioTrack ? 'sendrecv' : 'recvonly'
    traceWebRtcMedia('SENDER_ATTACHED', peerDiagnostic(entry, { trackKind: audioTrack?.kind, trackId: audioTrack?.id, trackReadyState: audioTrack?.readyState, mediaRole: 'audio', streamId: localStream?.id || '' }))
    await syncVideoSender(entry, 'cameraTransceiver', cameraTrack, cameraStream, 'camera')
    await syncVideoSender(entry, 'screenTransceiver', screenTrack, screenStream, 'screen')
  }

  const recoverPeer = async (connectionId) => {
    if (intentionalLeave || !participants.has(connectionId) || recoveryAttempts >= MAX_RECOVERY_ATTEMPTS) return
    recoveryAttempts += 1
    closePeer(connectionId)
    await new Promise(resolve => setTimeout(resolve, 250 * recoveryAttempts))
    if (!intentionalLeave && participants.has(connectionId)) {
      await createPeer(connectionId, { initiate: `${localConnectionId()}` < `${connectionId}` })
    }
  }

  const createPeer = async (connectionId, { initiate = false } = {}) => {
    if (!connectionId || connectionId === localConnectionId()) return null
    if (!joinedAck) return null
    if (peers.has(connectionId)) return peers.get(connectionId)
    const entry = {
      connectionId,
      pc: new RTCPeerConnection({ iceServers }),
      makingOffer: false,
      ignoreOffer: false,
      isSettingRemoteAnswerPending: false,
      initialNegotiationComplete: false,
      initiateInitialOffer: initiate,
      polite: isPolite(connectionId),
      audioTransceiver: null,
      cameraTransceiver: null,
      screenTransceiver: null,
      pendingCandidates: [],
      remoteMediaSources: new Map(),
      remoteMediaSourcesByTrackId: new Map(),
      remoteMediaSourcesByMid: new Map()
    }
    peers.set(connectionId, entry)
    entry.audioTransceiver = entry.pc.addTransceiver('audio', { direction: 'recvonly' })
    entry.cameraTransceiver = entry.pc.addTransceiver('video', { direction: 'recvonly' })
    entry.screenTransceiver = entry.pc.addTransceiver('video', { direction: 'recvonly' })

    entry.pc.onicecandidate = ({ candidate }) => {
      if (candidate) void sendSignal('SendIceCandidate', connectionId, candidate)
    }
    entry.pc.ontrack = ({ streams, track, transceiver }) => updateRemoteStreams(connectionId, entry, streams, track, transceiver)
    entry.pc.onnegotiationneeded = () => {
      if (entry.initialNegotiationComplete || entry.initiateInitialOffer) void negotiate(entry)
    }
    entry.pc.onconnectionstatechange = () => {
      traceWebRtcMedia(entry.pc.connectionState === 'connected' ? 'PEER_CONNECTED' : 'TRANSCEIVER_STATE', peerDiagnostic(entry))
      if (['failed', 'disconnected'].includes(entry.pc.connectionState)) void recoverPeer(connectionId)
      if (entry.pc.connectionState === 'connected') recoveryAttempts = 0
    }
    entry.pc.oniceconnectionstatechange = () => traceWebRtcMedia('TRANSCEIVER_STATE', peerDiagnostic(entry))
    entry.pc.onsignalingstatechange = () => traceWebRtcMedia('TRANSCEIVER_STATE', peerDiagnostic(entry))
    await syncPeerMedia(entry)
    // The existing room member receives ParticipantJoined and creates the
    // pair's single initial offer. The joiner's snapshot-created peer waits
    // for it, avoiding the duplicate offer added after the known-good flow.
    if (entry.initiateInitialOffer) await negotiate(entry)
    return entry
  }

  const applyOffer = async message => {
    const connectionId = read(message, 'fromConnectionId', 'FromConnectionId')
    const signal = read(message, 'description', 'Description')
    const description = read(signal, 'description', 'Description') || signal
    const mediaSources = read(signal, 'mediaSources', 'MediaSources') ?? read(message, 'mediaSources', 'MediaSources') ?? []
    const entry = await createPeer(connectionId)
    if (!entry || !description) return
    entry.remoteMediaSources = new Map(mediaSources.map(item => [read(item, 'streamId', 'StreamId'), read(item, 'source', 'Source')]))
    entry.remoteMediaSourcesByTrackId = new Map(mediaSources.map(item => [read(item, 'trackId', 'TrackId'), read(item, 'source', 'Source')]))
    entry.remoteMediaSourcesByMid = new Map(mediaSources.map(item => [read(item, 'mid', 'Mid'), read(item, 'source', 'Source')]).filter(([mid]) => mid !== null && mid !== undefined && mid !== ''))
    const offerCollision = entry.makingOffer || entry.pc.signalingState !== 'stable'
    entry.ignoreOffer = !entry.polite && offerCollision
    if (entry.ignoreOffer) return
    entry.isSettingRemoteAnswerPending = entry.pc.signalingState === 'have-local-offer'
    if (offerCollision) await entry.pc.setLocalDescription({ type: 'rollback' })
    await entry.pc.setRemoteDescription(description)
    entry.isSettingRemoteAnswerPending = false
    for (const candidate of entry.pendingCandidates.splice(0)) await entry.pc.addIceCandidate(candidate)
    await entry.pc.setLocalDescription()
    await sendSignal('SendWebRtcAnswer', connectionId, {
      description: entry.pc.localDescription,
      mediaSources: getLocalMediaSources(entry)
    })
    entry.initialNegotiationComplete = true
  }

  const applyAnswer = async message => {
    const connectionId = read(message, 'fromConnectionId', 'FromConnectionId')
    const signal = read(message, 'description', 'Description')
    const description = read(signal, 'description', 'Description') || signal
    const entry = getPeer(connectionId)
    const mediaSources = read(signal, 'mediaSources', 'MediaSources') ?? read(message, 'mediaSources', 'MediaSources') ?? []
    if (entry) {
      entry.remoteMediaSources = new Map(mediaSources.map(item => [read(item, 'streamId', 'StreamId'), read(item, 'source', 'Source')]))
      entry.remoteMediaSourcesByTrackId = new Map(mediaSources.map(item => [read(item, 'trackId', 'TrackId'), read(item, 'source', 'Source')]))
      entry.remoteMediaSourcesByMid = new Map(mediaSources.map(item => [read(item, 'mid', 'Mid'), read(item, 'source', 'Source')]).filter(([mid]) => mid !== null && mid !== undefined && mid !== ''))
    }
    if (entry && description) {
      await entry.pc.setRemoteDescription(description)
      for (const candidate of entry.pendingCandidates.splice(0)) await entry.pc.addIceCandidate(candidate)
      entry.initialNegotiationComplete = true
    }
  }

  const applyCandidate = async message => {
    const connectionId = read(message, 'fromConnectionId', 'FromConnectionId')
    const candidate = read(message, 'candidate', 'Candidate')
    const entry = getPeer(connectionId)
    if (entry && candidate && !entry.ignoreOffer) {
      if (!entry.pc.remoteDescription) entry.pendingCandidates.push(candidate)
      else await entry.pc.addIceCandidate(candidate)
    }
  }

  const getCallChatHistory = async (limit = 100) => {
    if (!joinedAck || !connection || connection.state !== signalR.HubConnectionState.Connected || !roomId) return []
    return connection.invoke('GetCallChatHistory', roomId, limit)
  }

  const sendCallMessage = async (content, clientMessageId = null) => {
    if (!joinedAck || !connection || connection.state !== signalR.HubConnectionState.Connected || !roomId)
      throw new Error('CALL_NOT_CONNECTED')
    return connection.invoke('SendCallMessage', roomId, content, clientMessageId)
  }

  const refreshSnapshot = async snapshot => {
    joinedAck = true
    roomId = read(snapshot, 'roomId', 'RoomId')
    const aiState = read(snapshot, 'aiState', 'AiState')
    const transcription = read(snapshot, 'transcription', 'Transcription') || {}
    const supportedLanguages = read(transcription, 'supportedLanguages', 'SupportedLanguages') || []
    const defaultLanguage = read(transcription, 'defaultLanguage', 'DefaultLanguage') || 'vi'
    if (supportedLanguages.includes(defaultLanguage)) transcriptionLanguage = defaultLanguage
    onTranscriptionCapabilities?.({
      configured: read(transcription, 'configured', 'Configured') === true,
      provider: read(transcription, 'provider', 'Provider') || 'Unavailable',
      supportedLanguages,
      defaultLanguage,
      aiConfigured: read(transcription, 'aiConfigured', 'AiConfigured') === true,
      aiProvider: read(transcription, 'aiProvider', 'AiProvider') || 'Unavailable',
      aiTranscriptChunkSize: read(transcription, 'aiTranscriptChunkSize', 'AiTranscriptChunkSize') || 8
    })
    callSessionId = read(aiState, 'callSessionId', 'CallSessionId') || null
    trace('JOIN_ACK', 'join-voice-room-ack')
    participants = new Map((read(snapshot, 'participants', 'Participants') ?? []).map(item => {
      const participant = normalizeParticipant(item)
      return [participant.connectionId, participant]
    }))
    handleAiState(aiState)
    emitParticipants()
    for (const participant of participants.values()) await createPeer(participant.connectionId)
    const queuedSignals = pendingInboundSignals.splice(0)
    for (const signal of queuedSignals) {
      if (signal.type === 'offer') await applyOffer(signal.value)
      if (signal.type === 'answer') await applyAnswer(signal.value)
      if (signal.type === 'candidate') await applyCandidate(signal.value)
    }
    await onCallHistory?.(await getCallChatHistory())
  }

  const registerHandlers = () => {
    connection.on('ParticipantJoined', async event => {
      const participant = normalizeParticipant(read(event, 'participant', 'Participant'))
      participants.set(participant.connectionId, participant)
      emitParticipants()
      await createPeer(participant.connectionId, { initiate: true })
    })
    connection.on('ParticipantLeft', event => {
      const connectionId = read(event, 'connectionId', 'ConnectionId')
      participants.delete(connectionId)
      closePeer(connectionId)
      emitParticipants()
    })
    connection.on('ParticipantMediaStateChanged', event => {
      const connectionId = read(event, 'connectionId', 'ConnectionId')
      const state = read(event, 'state', 'State')
      const participant = participants.get(connectionId)
      if (!participant || !state) return
      participants.set(connectionId, {
        ...participant,
        microphoneEnabled: read(state, 'microphoneEnabled', 'MicrophoneEnabled') !== false,
        cameraEnabled: read(state, 'cameraEnabled', 'CameraEnabled') === true,
        screenSharing: read(state, 'screenSharing', 'ScreenSharing') === true
      })
      emitParticipants()
    })
    connection.on('ParticipantHandChanged', event => {
      const connectionId = read(event, 'connectionId', 'ConnectionId')
      const participant = participants.get(connectionId)
      const raised = read(event, 'handRaised', 'HandRaised') === true
      if (participant) { participants.set(connectionId, { ...participant, handRaised: raised }); emitParticipants() }
      onHandChanged?.({ connectionId, handRaised: raised })
    })
    connection.on('CallReactionAdded', event => onReaction?.({
      id: read(event, 'id', 'Id'), connectionId: read(event, 'connectionId', 'ConnectionId'),
      userId: read(event, 'userId', 'UserId'), displayName: read(event, 'displayName', 'DisplayName'), emoji: read(event, 'emoji', 'Emoji')
    }))
    connection.on('ParticipantSpeakerChanged', event => {
      const connectionId = read(event, 'connectionId', 'ConnectionId')
      const participant = participants.get(connectionId)
      const speaking = read(event, 'isSpeaking', 'IsSpeaking') === true
      if (participant) { participants.set(connectionId, { ...participant, isSpeaking: speaking }); emitParticipants() }
      onSpeakerChanged?.({ connectionId, isSpeaking: speaking })
    })
    connection.on('ForceMuteParticipant', () => onForceMute?.())
    connection.on('ForceRemovedFromCall', () => onForceRemoved?.())
    connection.on('CallMessageCreated', event => onCallMessage?.(event))
    connection.on('CallAiStateChanged', event => handleAiState(read(event, 'state', 'State')))
    connection.on('AiConsentRequested', event => handleAiState(read(event, 'state', 'State')))
    connection.on('AiParticipantAccepted', event => handleAiState(read(event, 'state', 'State')))
    connection.on('AiParticipantDeclined', event => handleAiState(read(event, 'state', 'State')))
    connection.on('AiTranscriptionStarted', event => handleAiState(read(event, 'state', 'State')))
    connection.on('AiTranscriptionPaused', event => handleAiState(read(event, 'state', 'State')))
    connection.on('AiTranscriptionStopped', event => handleAiState(read(event, 'state', 'State')))
    connection.on('AiTranscriptionUnavailable', event => {
      onTranscriptionError?.(event)
      void stopTranscriptionCapture({ notifyServer: false })
    })
    connection.on('AiTranscriptionError', event => {
      onTranscriptionError?.(event)
      void stopTranscriptionCapture({ notifyServer: false })
    })
    connection.on('CallTranscriptChunkAdded', event => onTranscriptChunk?.(event))
    connection.on('CallTranscriptInterim', event => onTranscriptInterim?.(event))
    connection.on('WebRtcOffer', event => {
      if (!joinedAck) pendingInboundSignals.push({ type: 'offer', value: event })
      else void applyOffer(event)
    })
    connection.on('WebRtcAnswer', event => {
      if (!joinedAck) pendingInboundSignals.push({ type: 'answer', value: event })
      else void applyAnswer(event)
    })
    connection.on('IceCandidate', event => {
      if (!joinedAck) pendingInboundSignals.push({ type: 'candidate', value: event })
      else void applyCandidate(event)
    })
    connection.onreconnecting(() => {
      trace('ON_RECONNECTING', 'signalr-automatic-reconnect')
      emit('reconnecting')
    })
    connection.onreconnected(async () => {
      if (intentionalLeave) return
      trace('ON_RECONNECTED', 'signalr-automatic-reconnect-complete')
      joinedAck = false
      roomId = null
      pendingInboundSignals.splice(0)
      closeAllPeers()
      emit('reconnecting')
      try {
        trace('JOIN_BEGIN', 'reconnect-rejoin')
        const snapshot = await connection.invoke('JoinVoiceRoom', projectId, voiceChannelId)
        await refreshSnapshot(snapshot)
        traceCaptionSource('RECONNECT_REJOIN', {
          oldCaptionSourceGeneration: transcriptionCapture?.sourceGeneration ?? null,
          newLocalMicrophoneGeneration: localMicrophoneGeneration,
          activeCaptionAudioNodeRebuilt: false,
          captionCapturePointsToCurrentLocalMicrophoneSource: captionSourceMatchesCurrentMicrophone(transcriptionCapture),
          ...describeCaptionSource(transcriptionCapture)
        })
        emit('connected')
      } catch (error) {
        emit('error', { error, silent: true })
      }
    })
    connection.onclose(error => {
      trace('ON_CLOSE', error?.message || 'signalr-closed')
      if (!intentionalLeave) emit('disconnected', { error })
    })
  }

  const acquireMicrophoneWithDevice = async (deviceId, enabled = microphoneEnabled, reason = 'microphone-acquisition') => {
    if (!navigator.mediaDevices?.getUserMedia) throw mediaError(null, 'UNSUPPORTED_BROWSER')
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          ...(deviceId ? { deviceId: { exact: deviceId } } : {}),
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true
        },
        video: false
      })
      setLocalMicrophoneStream(stream, reason)
      localStream.getAudioTracks().forEach(track => { track.enabled = Boolean(enabled) })
      for (const track of localStream.getAudioTracks()) traceWebRtcMedia('LOCAL_TRACK_READY', {
        trackKind: track.kind,
        trackId: track.id,
        trackReadyState: track.readyState,
        mediaRole: 'audio',
        streamId: localStream.id
      })
    } catch (error) {
      throw mediaError(error, 'MIC_UNAVAILABLE')
    }
  }

  const disposeBackgroundProcessor = async () => {
    await backgroundProcessor?.dispose?.()
    backgroundProcessor = null
  }

  const getCameraOutputTrack = async () => {
    if (backgroundEffect !== 'blur') {
      await disposeBackgroundProcessor()
      return rawCameraTrack
    }
    await disposeBackgroundProcessor()
    const processor = createBackgroundBlurProcessor()
    try {
      const processedTrack = await processor.start(rawCameraTrack)
      backgroundProcessor = processor
      return processedTrack
    } catch (error) {
      await processor.dispose()
      backgroundEffect = 'none'
      emit('effect-fallback', { error, effect: backgroundEffect })
      return rawCameraTrack
    }
  }

  const adoptCameraStream = async stream => {
    const nextTrack = stream?.getVideoTracks?.()[0]
    if (!nextTrack) throw mediaError(null, 'CAMERA_UNAVAILABLE')
    rawCameraTrack = nextTrack
    cameraTrack = await getCameraOutputTrack()
    cameraStream = new MediaStream([cameraTrack])
    cameraEnabled = true
    traceWebRtcMedia('LOCAL_TRACK_READY', {
      trackKind: cameraTrack.kind,
      trackId: cameraTrack.id,
      trackReadyState: cameraTrack.readyState,
      mediaRole: 'camera',
      streamId: cameraStream.id
    })
  }

  const setCameraEnabled = async enabled => {
    if (enabled === cameraEnabled) return
    if (!enabled) {
      await disposeBackgroundProcessor()
      cameraTrack?.stop()
      rawCameraTrack?.stop()
      cameraTrack = null
      rawCameraTrack = null
      cameraStream = null
      cameraEnabled = false
      for (const entry of peers.values()) await syncPeerMedia(entry)
    } else {
      try {
        const stream = await navigator.mediaDevices.getUserMedia({
          video: {
            ...(selectedCameraDeviceId ? { deviceId: { exact: selectedCameraDeviceId } } : {}),
            width: { ideal: 1280, max: 1280 },
            height: { ideal: 720, max: 720 },
            frameRate: { ideal: 30, max: 30 }
          },
          audio: false
        })
        await adoptCameraStream(stream)
        for (const entry of peers.values()) await syncPeerMedia(entry)
      } catch (error) {
        rawCameraTrack?.stop()
        rawCameraTrack = null
        throw mediaError(error, 'CAMERA_UNAVAILABLE')
      }
    }
    await sendMediaState()
    emit('media')
  }

  const setMicrophoneEnabled = async enabled => {
    const nextEnabled = Boolean(enabled)
    let audioTrack = localStream?.getAudioTracks?.()[0] || null
    let senderNeedsSync = false
    if (nextEnabled && (!audioTrack || audioTrack.readyState !== 'live')) {
      await acquireMicrophoneWithDevice(selectedMicrophoneDeviceId, true, 'track-recovery')
      audioTrack = localStream?.getAudioTracks?.()[0] || null
      senderNeedsSync = true
    }
    if (audioTrack) audioTrack.enabled = nextEnabled
    microphoneEnabled = nextEnabled
    if (senderNeedsSync) {
      for (const entry of peers.values()) await syncPeerMedia(entry)
    }
    await sendMediaState()
    emit('media')
  }

  const enumerateDevices = async () => (navigator.mediaDevices?.enumerateDevices ? navigator.mediaDevices.enumerateDevices() : [])

  const setMicrophoneDevice = async deviceId => {
    selectedMicrophoneDeviceId = deviceId || ''
    if (!localStream) return
    const next = await navigator.mediaDevices.getUserMedia({ audio: { deviceId: { exact: deviceId }, echoCancellation: true, noiseSuppression: true, autoGainControl: true }, video: false })
    next.getAudioTracks().forEach(track => { track.enabled = microphoneEnabled })
    const previous = setLocalMicrophoneStream(next, 'microphone-device-switch')
    for (const entry of peers.values()) await syncPeerMedia(entry)
    previous?.getTracks().forEach(track => track.stop())
    emit('media')
  }

  const setCameraDevice = async deviceId => {
    selectedCameraDeviceId = deviceId || ''
    if (!cameraEnabled) return
    const next = await navigator.mediaDevices.getUserMedia({ video: { deviceId: { exact: deviceId }, width: { ideal: 1280 }, height: { ideal: 720 } }, audio: false })
    const previousRaw = rawCameraTrack
    const previousOutput = cameraTrack
    await adoptCameraStream(next)
    previousOutput?.stop()
    previousRaw?.stop()
    for (const entry of peers.values()) await syncPeerMedia(entry)
    emit('media')
  }

  const setRaiseHand = async raised => connection?.invoke('SetRaiseHand', roomId, Boolean(raised))
  const sendReaction = async emoji => connection?.invoke('SendCallReaction', roomId, emoji)
  const publishSpeakerState = async speaking => connection?.invoke('PublishSpeakerState', roomId, Boolean(speaking))
  const muteParticipant = async id => connection?.invoke('MuteParticipant', roomId, id)
  const lowerParticipantHand = async id => connection?.invoke('LowerParticipantHand', roomId, id)
  const removeParticipant = async id => connection?.invoke('RemoveParticipant', roomId, id)

  const setCameraBackgroundEffect = async effect => {
    const nextEffect = normalizeBackgroundEffect(effect)
    if (nextEffect === backgroundEffect && (!nextEffect || backgroundProcessor?.isActive?.())) return
    backgroundEffect = nextEffect
    if (!cameraEnabled || !rawCameraTrack) {
      emit('media')
      return
    }
    const outputTrack = await getCameraOutputTrack()
    const previousTrack = cameraTrack
    cameraTrack = outputTrack
    if (cameraStream) {
      if (previousTrack) cameraStream.removeTrack(previousTrack)
      cameraStream.addTrack(cameraTrack)
    }
    for (const entry of peers.values()) await syncPeerMedia(entry)
    await sendMediaState()
    emit('media')
  }

  const stopScreenShare = async () => {
    if (screenTrack) screenTrack.onended = null
    screenTrack?.stop()
    screenTrack = null
    screenStream = null
    screenSharing = false
    for (const entry of peers.values()) await syncPeerMedia(entry)
    await sendMediaState()
    emit('media')
  }

  const toggleScreenShare = async () => {
    if (screenSharing) return stopScreenShare()
    if (!navigator.mediaDevices?.getDisplayMedia) throw mediaError(null, 'UNSUPPORTED_BROWSER')
    try {
      const stream = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: false })
      screenTrack = stream.getVideoTracks()[0]
      screenStream = stream
      screenSharing = true
      traceWebRtcMedia('LOCAL_TRACK_READY', {
        trackKind: screenTrack.kind,
        trackId: screenTrack.id,
        trackReadyState: screenTrack.readyState,
        mediaRole: 'screen',
        streamId: screenStream.id
      })
      for (const entry of peers.values()) await syncPeerMedia(entry)
      screenTrack.onended = () => { void stopScreenShare() }
      await sendMediaState()
      emit('media')
    } catch (error) {
      if (error?.name === 'AbortError') return
      throw mediaError(error, 'SCREEN_SHARE_UNAVAILABLE')
    }
  }

  const start = async () => {
    if (startPromise) return startPromise
    if ([signalR.HubConnectionState.Connected, signalR.HubConnectionState.Connecting, signalR.HubConnectionState.Reconnecting]
      .includes(connection?.state)) return
    if (activeCallSession && activeCallSession !== session) await activeCallSession.leave()
    activeCallSession = session
    trace('START_BEGIN', 'session-start')
    startPromise = (async () => {
      intentionalLeave = false
      joinedAck = false
      if (typeof RTCPeerConnection === 'undefined') throw mediaError(null, 'UNSUPPORTED_BROWSER')
      microphoneEnabled = Boolean(initialMicrophoneEnabled)
      if (initialMicrophoneStream) {
        setLocalMicrophoneStream(initialMicrophoneStream, 'initial-microphone-stream')
        localStream.getAudioTracks().forEach(track => { track.enabled = microphoneEnabled })
      } else {
        try {
          await acquireMicrophoneWithDevice(selectedMicrophoneDeviceId, microphoneEnabled, 'initial-microphone-acquisition')
        } catch (error) {
          if (microphoneEnabled) throw error
          setLocalMicrophoneStream(null, 'initial-microphone-unavailable')
        }
      }
      cameraEnabled = false
      if (initialCameraEnabled) {
        try {
          const stream = initialCameraStream || await navigator.mediaDevices.getUserMedia({
            video: {
              ...(selectedCameraDeviceId ? { deviceId: { exact: selectedCameraDeviceId } } : {}),
              width: { ideal: 1280, max: 1280 },
              height: { ideal: 720, max: 720 },
              frameRate: { ideal: 30, max: 30 }
            },
            audio: false
          })
          await adoptCameraStream(stream)
        } catch (error) {
          throw mediaError(error, 'CAMERA_UNAVAILABLE')
        }
      }
      const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5136/api'
      const hubBaseUrl = apiBaseUrl.replace(/\/api\/?$/, '')
      connection = configureRealtimeHub(new signalR.HubConnectionBuilder())
        .withUrl(`${hubBaseUrl}${HUB_ROUTE}`, { accessTokenFactory: () => getStoredAccessToken() || '' })
        .build()
      registerHandlers()
      emit('connecting')
      iceServers = await getIceServers().catch(() => [])
      await connection.start()
      trace('JOIN_BEGIN', 'initial-join')
      await refreshSnapshot(await connection.invoke('JoinVoiceRoom', projectId, voiceChannelId))
      trace('START_OK', 'session-started')
      emit('connected')
    })().finally(() => { startPromise = null })
    return startPromise
  }

  const leave = async () => {
    if (leavePromise) return leavePromise
    leavePromise = (async () => {
      intentionalLeave = true
      trace('LEAVE_BEGIN', 'session-leave')
      if (startPromise) {
        try { await startPromise } catch { /* cleanup below remains authoritative */ }
      }
      try {
        await stopTranscriptionCapture()
        if (connection?.state === signalR.HubConnectionState.Connected && roomId) {
          await connection.invoke('LeaveVoiceRoom', projectId, voiceChannelId)
        }
        if (connection) {
          trace('STOP_REQUEST', 'session-leave')
          await connection.stop()
          trace('STOP_DONE', 'session-leave')
        }
      } finally {
        joinedAck = false
        callSessionId = null
        pendingInboundSignals.splice(0)
        closeAllPeers()
        screenTrack?.stop()
        await disposeBackgroundProcessor()
        cameraTrack?.stop()
        rawCameraTrack?.stop()
        localStream?.getTracks().forEach(track => track.stop())
        setLocalMicrophoneStream(null, 'call-leave')
        cameraTrack = null
        rawCameraTrack = null
        cameraStream = null
        screenTrack = null
        screenStream = null
        participants.clear()
        remoteStreams.clear()
        connection = null
        roomId = null
        emitParticipants()
        emitRemoteStreams()
        emit('disconnected')
        trace('LEAVE_DONE', 'session-leave')
        if (activeCallSession === session) activeCallSession = null
      }
    })().finally(() => { leavePromise = null })
    return leavePromise
  }

  const session = {
    start,
    leave,
    setMicrophoneEnabled,
    enumerateDevices,
    setMicrophoneDevice,
    setCameraDevice,
    setRaiseHand,
    sendReaction,
    publishSpeakerState,
    muteParticipant,
    lowerParticipantHand,
    removeParticipant,
    setCameraEnabled,
    setCameraBackgroundEffect,
    toggleScreenShare,
    getCallChatHistory,
    sendCallMessage,
    requestAiTranscription: async () => connection?.invoke('RequestAiTranscription', roomId),
    respondToAiConsent: async (accepted, state) => connection?.invoke(
      'RespondToAiConsent', roomId, state?.callSessionId || state?.CallSessionId,
      state?.consentGeneration || state?.ConsentGeneration, accepted),
    stopAiTranscription: async () => connection?.invoke('StopAiTranscription', roomId),
    setTranscriptionLanguage: language => {
      if (!['vi', 'en'].includes(language)) throw new Error('UNSUPPORTED_TRANSCRIPTION_LANGUAGE')
      transcriptionLanguage = language
    },
    getLocalStream: () => cameraStream || localStream,
    getLocalCameraStream: () => cameraStream,
    getLocalScreenStream: () => screenStream,
    getRoomId: () => roomId,
    getCallSessionId: () => callSessionId,
    isJoined: () => joinedAck && Boolean(callSessionId),
    getConnectionId: localConnectionId,
    getPeerDiagnostics: () => [...peers.values()].map(entry => inspectPeerConnection(entry.connectionId, entry.pc)),
    getMediaState: () => ({ microphoneEnabled, cameraEnabled, screenSharing, backgroundEffect })
  }
  return session
}
