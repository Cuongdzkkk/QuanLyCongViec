import * as signalR from '@microsoft/signalr'
import axiosClient from '@/api/axiosClient'
import { getStoredAccessToken } from '@/utils/authSession'
import { createBackgroundBlurProcessor } from '@/services/cameraBackgroundEffect'

const HUB_ROUTE = '/hubs/call'
const MAX_RECOVERY_ATTEMPTS = 2

const read = (value, camel, pascal) => value?.[camel] ?? value?.[pascal]

const normalizeParticipant = (value) => ({
  connectionId: read(value, 'connectionId', 'ConnectionId'),
  userId: read(value, 'userId', 'UserId'),
  displayName: read(value, 'displayName', 'DisplayName') || 'SprintA user',
  avatarUrl: read(value, 'avatarUrl', 'AvatarUrl') || null,
  microphoneEnabled: read(value, 'microphoneEnabled', 'MicrophoneEnabled') !== false,
  cameraEnabled: read(value, 'cameraEnabled', 'CameraEnabled') === true,
  screenSharing: read(value, 'screenSharing', 'ScreenSharing') === true
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

const getIceServers = async () => {
  const response = await axiosClient.get('/webrtc/ice-servers')
  const payload = response?.data?.data ?? response?.data ?? {}
  return (payload.iceServers ?? payload.IceServers ?? []).map(server => ({
    urls: server.urls ?? server.Urls,
    ...(server.username || server.Username ? { username: server.username ?? server.Username } : {}),
    ...(server.credential || server.Credential ? { credential: server.credential ?? server.Credential } : {})
  }))
}

export const createCallMediaSession = ({ projectId, voiceChannelId, onState, onParticipants, onRemoteStreams, onAiState, onTranscriptChunk, onTranscriptInterim, onTranscriptionError }) => {
  let connection = null
  let roomId = null
  let localStream = null
  let rawCameraTrack = null
  let cameraTrack = null
  let screenTrack = null
  let backgroundProcessor = null
  let backgroundEffect = 'none'
  let preShareCameraEnabled = false
  let microphoneEnabled = true
  let cameraEnabled = false
  let screenSharing = false
  let intentionalLeave = false
  let recoveryAttempts = 0
  let iceServers = []
  let participants = new Map()
  const peers = new Map()
  const remoteStreams = new Map()
  let transcriptionCapture = null
  let transcriptionQueue = Promise.resolve()

  const emit = (state, detail = {}) => onState?.({ state, ...detail })
  const emitParticipants = () => onParticipants?.([...participants.values()])
  const emitRemoteStreams = () => onRemoteStreams?.(new Map(remoteStreams))
  const emitAiState = value => {
    const nested = read(value, 'state', 'State')
    onAiState?.(nested && typeof nested === 'object' ? nested : value)
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
    const payload = Array.from(bytes)
    transcriptionQueue = transcriptionQueue
      .catch(() => {})
      .then(async () => {
        if (!capture.active || connection?.state !== signalR.HubConnectionState.Connected || capture !== transcriptionCapture) return
        try {
          await connection.invoke(
            'SubmitCallAudioChunk',
            roomId,
            capture.callSessionId,
            capture.consentGeneration,
            'audio/linear16;rate=16000;channels=1',
            payload,
            startedAt.toISOString(),
            endedAt.toISOString())
        } catch (error) {
          if (!['AI_TRANSCRIPTION_NOT_ACTIVE', 'CALL_TRANSCRIPTION_NOT_CONFIGURED'].includes(error?.message)) onTranscriptionError?.({ code: 'INGEST_ERROR', message: 'Không thể gửi âm thanh cho biên bản cuộc gọi.' })
        }
      })
  }

  const stopTranscriptionCapture = async ({ notifyServer = true } = {}) => {
    const capture = transcriptionCapture
    transcriptionCapture = null
    if (!capture) return
    capture.active = false
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
    const processor = context.createScriptProcessor(4096, 1, 1)
    const sink = context.createGain()
    sink.gain.value = 0
    const capture = {
      active: true,
      callSessionId,
      consentGeneration,
      context,
      source,
      processor,
      sink,
      pending: [],
      preRoll: [],
      speaking: false,
      silenceChunks: 0
    }
    transcriptionCapture = capture
    processor.onaudioprocess = event => {
      if (!capture.active || !microphoneEnabled) return
      const pcm = downsampleToPcm16(event.inputBuffer.getChannelData(0), context.sampleRate)
      capture.pending.push(pcm)
      let pendingLength = capture.pending.reduce((total, item) => total + item.length, 0)
      while (pendingLength >= 8000 && capture.active) {
        const chunk = new Uint8Array(8000)
        let offset = 0
        while (offset < chunk.length && capture.pending.length) {
          const first = capture.pending[0]
          const copyLength = Math.min(first.length, chunk.length - offset)
          chunk.set(first.subarray(0, copyLength), offset)
          offset += copyLength
          if (copyLength === first.length) capture.pending.shift()
          else capture.pending[0] = first.subarray(copyLength)
        }
        pendingLength -= chunk.length
        let energy = 0
        const view = new DataView(chunk.buffer)
        for (let index = 0; index < chunk.length; index += 2) {
          const sample = view.getInt16(index, true) / 0x8000
          energy += sample * sample
        }
        const voiced = Math.sqrt(energy / (chunk.length / 2)) >= 0.012
        const endedAt = new Date()
        const startedAt = new Date(endedAt.getTime() - 250)
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
    if (!connection || connection.state !== signalR.HubConnectionState.Connected || !roomId) return
    try {
      await connection.invoke(method, roomId, targetConnectionId, payload)
    } catch (error) {
      if (!intentionalLeave) emit('error', { error })
    }
  }

  const negotiate = async (entry) => {
    if (entry.makingOffer || entry.pc.signalingState !== 'stable') return
    try {
      entry.makingOffer = true
      await entry.pc.setLocalDescription()
      await sendSignal('SendWebRtcOffer', entry.connectionId, entry.pc.localDescription)
    } finally {
      entry.makingOffer = false
    }
  }

  const recoverPeer = async (connectionId) => {
    if (intentionalLeave || !participants.has(connectionId) || recoveryAttempts >= MAX_RECOVERY_ATTEMPTS) return
    recoveryAttempts += 1
    closePeer(connectionId)
    await new Promise(resolve => setTimeout(resolve, 250 * recoveryAttempts))
    if (!intentionalLeave && participants.has(connectionId)) await createPeer(connectionId)
  }

  const createPeer = async (connectionId) => {
    if (!connectionId || connectionId === localConnectionId() || peers.has(connectionId)) return peers.get(connectionId)
    const entry = {
      connectionId,
      pc: new RTCPeerConnection({ iceServers }),
      makingOffer: false,
      ignoreOffer: false,
      isSettingRemoteAnswerPending: false,
      polite: isPolite(connectionId)
    }
    peers.set(connectionId, entry)
    for (const track of localStream?.getTracks() ?? []) entry.pc.addTrack(track, localStream)

    entry.pc.onicecandidate = ({ candidate }) => {
      if (candidate) void sendSignal('SendIceCandidate', connectionId, candidate)
    }
    entry.pc.ontrack = ({ streams, track }) => {
      const stream = streams[0] || new MediaStream([track])
      remoteStreams.set(connectionId, stream)
      emitRemoteStreams()
    }
    entry.pc.onnegotiationneeded = () => negotiate(entry)
    entry.pc.onconnectionstatechange = () => {
      if (['failed', 'disconnected'].includes(entry.pc.connectionState)) void recoverPeer(connectionId)
      if (entry.pc.connectionState === 'connected') recoveryAttempts = 0
    }
    return entry
  }

  const applyOffer = async message => {
    const connectionId = read(message, 'fromConnectionId', 'FromConnectionId')
    const description = read(message, 'description', 'Description')
    const entry = await createPeer(connectionId)
    if (!entry || !description) return
    const offerCollision = entry.makingOffer || entry.pc.signalingState !== 'stable'
    entry.ignoreOffer = !entry.polite && offerCollision
    if (entry.ignoreOffer) return
    entry.isSettingRemoteAnswerPending = entry.pc.signalingState === 'have-local-offer'
    if (offerCollision) await entry.pc.setLocalDescription({ type: 'rollback' })
    await entry.pc.setRemoteDescription(description)
    entry.isSettingRemoteAnswerPending = false
    await entry.pc.setLocalDescription()
    await sendSignal('SendWebRtcAnswer', connectionId, entry.pc.localDescription)
  }

  const applyAnswer = async message => {
    const connectionId = read(message, 'fromConnectionId', 'FromConnectionId')
    const description = read(message, 'description', 'Description')
    const entry = getPeer(connectionId)
    if (entry && description) await entry.pc.setRemoteDescription(description)
  }

  const applyCandidate = async message => {
    const connectionId = read(message, 'fromConnectionId', 'FromConnectionId')
    const candidate = read(message, 'candidate', 'Candidate')
    const entry = getPeer(connectionId)
    if (entry && candidate && !entry.ignoreOffer) await entry.pc.addIceCandidate(candidate)
  }

  const refreshSnapshot = async snapshot => {
    roomId = read(snapshot, 'roomId', 'RoomId')
    participants = new Map((read(snapshot, 'participants', 'Participants') ?? []).map(item => {
      const participant = normalizeParticipant(item)
      return [participant.connectionId, participant]
    }))
    handleAiState(read(snapshot, 'aiState', 'AiState'))
    emitParticipants()
    for (const participant of participants.values()) await createPeer(participant.connectionId)
  }

  const registerHandlers = () => {
    connection.on('ParticipantJoined', async event => {
      const participant = normalizeParticipant(read(event, 'participant', 'Participant'))
      participants.set(participant.connectionId, participant)
      emitParticipants()
      await createPeer(participant.connectionId)
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
    connection.on('WebRtcOffer', applyOffer)
    connection.on('WebRtcAnswer', applyAnswer)
    connection.on('IceCandidate', applyCandidate)
    connection.onreconnecting(() => emit('reconnecting'))
    connection.onreconnected(async () => {
      if (intentionalLeave) return
      closeAllPeers()
      emit('reconnecting')
      try {
        const snapshot = await connection.invoke('JoinVoiceRoom', projectId, voiceChannelId)
        await refreshSnapshot(snapshot)
        emit('connected')
      } catch (error) {
        emit('error', { error })
      }
    })
    connection.onclose(error => {
      if (!intentionalLeave) emit('disconnected', { error })
    })
  }

  const acquireMicrophone = async () => {
    if (!navigator.mediaDevices?.getUserMedia) throw mediaError(null, 'UNSUPPORTED_BROWSER')
    try {
      localStream = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
        video: false
      })
      microphoneEnabled = true
    } catch (error) {
      throw mediaError(error, 'MIC_UNAVAILABLE')
    }
  }

  const replaceVideoTrack = async track => {
    for (const entry of peers.values()) {
      const sender = entry.pc.getSenders().find(item => item.track?.kind === 'video')
      if (sender) await sender.replaceTrack(track)
      else if (track && localStream) entry.pc.addTrack(track, localStream)
    }
    for (const trackInStream of localStream?.getVideoTracks() ?? []) localStream.removeTrack(trackInStream)
    if (track && localStream) localStream.addTrack(track)
  }

  const disposeBackgroundProcessor = async () => {
    await backgroundProcessor?.dispose?.()
    backgroundProcessor = null
  }

  const getCameraOutputTrack = async () => {
    if (backgroundEffect !== 'blur') return rawCameraTrack
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

  const setCameraEnabled = async enabled => {
    if (enabled === cameraEnabled) return
    if (!enabled) {
      await disposeBackgroundProcessor()
      cameraTrack?.stop()
      rawCameraTrack?.stop()
      cameraTrack = null
      rawCameraTrack = null
      cameraEnabled = false
      await replaceVideoTrack(null)
    } else {
      try {
        const stream = await navigator.mediaDevices.getUserMedia({
          video: { width: { ideal: 1280, max: 1280 }, height: { ideal: 720, max: 720 }, frameRate: { ideal: 30, max: 30 } },
          audio: false
        })
        rawCameraTrack = stream.getVideoTracks()[0]
        cameraTrack = await getCameraOutputTrack()
        cameraEnabled = true
        await replaceVideoTrack(cameraTrack)
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
    microphoneEnabled = Boolean(enabled)
    for (const track of localStream?.getAudioTracks() ?? []) track.enabled = microphoneEnabled
    await sendMediaState()
    emit('media')
  }

  const setCameraBackgroundEffect = async effect => {
    const nextEffect = normalizeBackgroundEffect(effect)
    if (nextEffect === backgroundEffect && (!nextEffect || backgroundProcessor?.isActive?.())) return
    backgroundEffect = nextEffect
    if (!cameraEnabled || !rawCameraTrack) {
      emit('media')
      return
    }
    const outputTrack = await getCameraOutputTrack()
    cameraTrack = outputTrack
    await replaceVideoTrack(cameraTrack)
    await sendMediaState()
    emit('media')
  }

  const stopScreenShare = async () => {
    screenTrack?.stop()
    screenTrack = null
    screenSharing = false
    if (preShareCameraEnabled) await setCameraEnabled(true)
    else await replaceVideoTrack(null)
    await sendMediaState()
    emit('media')
  }

  const toggleScreenShare = async () => {
    if (screenSharing) return stopScreenShare()
    if (!navigator.mediaDevices?.getDisplayMedia) throw mediaError(null, 'UNSUPPORTED_BROWSER')
    try {
      const stream = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: false })
      screenTrack = stream.getVideoTracks()[0]
      preShareCameraEnabled = cameraEnabled
      if (cameraEnabled) await setCameraEnabled(false)
      cameraEnabled = false
      screenSharing = true
      await replaceVideoTrack(screenTrack)
      screenTrack.onended = () => { void stopScreenShare() }
      await sendMediaState()
      emit('media')
    } catch (error) {
      if (error?.name === 'AbortError') return
      throw mediaError(error, 'SCREEN_SHARE_UNAVAILABLE')
    }
  }

  const start = async () => {
    if (connection) return
    intentionalLeave = false
    if (typeof RTCPeerConnection === 'undefined') throw mediaError(null, 'UNSUPPORTED_BROWSER')
    await acquireMicrophone()
    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5136/api'
    const hubBaseUrl = apiBaseUrl.replace(/\/api\/?$/, '')
    connection = new signalR.HubConnectionBuilder()
      .withUrl(`${hubBaseUrl}${HUB_ROUTE}`, { accessTokenFactory: () => getStoredAccessToken() || '' })
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .build()
    registerHandlers()
    emit('connecting')
    iceServers = await getIceServers().catch(() => [])
    await connection.start()
    await refreshSnapshot(await connection.invoke('JoinVoiceRoom', projectId, voiceChannelId))
    emit('connected')
  }

  const leave = async () => {
    intentionalLeave = true
    try {
      await stopTranscriptionCapture()
      if (connection?.state === signalR.HubConnectionState.Connected && roomId) {
        await connection.invoke('LeaveVoiceRoom', projectId, voiceChannelId)
      }
      await connection?.stop()
    } finally {
      closeAllPeers()
      screenTrack?.stop()
      await disposeBackgroundProcessor()
      cameraTrack?.stop()
      rawCameraTrack?.stop()
      localStream?.getTracks().forEach(track => track.stop())
      localStream = null
      cameraTrack = null
      rawCameraTrack = null
      screenTrack = null
      participants.clear()
      remoteStreams.clear()
      connection = null
      roomId = null
      emitParticipants()
      emitRemoteStreams()
      emit('disconnected')
    }
  }

  return {
    start,
    leave,
    setMicrophoneEnabled,
    setCameraEnabled,
    setCameraBackgroundEffect,
    toggleScreenShare,
    requestAiTranscription: async () => connection?.invoke('RequestAiTranscription', roomId),
    respondToAiConsent: async (accepted, state) => connection?.invoke(
      'RespondToAiConsent', roomId, state?.callSessionId || state?.CallSessionId,
      state?.consentGeneration || state?.ConsentGeneration, accepted),
    stopAiTranscription: async () => connection?.invoke('StopAiTranscription', roomId),
    getLocalStream: () => localStream,
    getRoomId: () => roomId,
    getConnectionId: localConnectionId,
    getMediaState: () => ({ microphoneEnabled, cameraEnabled, screenSharing, backgroundEffect })
  }
}
