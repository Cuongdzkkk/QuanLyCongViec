import { defineStore } from 'pinia'

// Global container for persistent remote audio elements so audio plays continuously across router navigation
const globalAudioContainer = new Map()

const ensureGlobalAudioElement = (connectionId, stream) => {
  if (!stream) return
  let audioEl = globalAudioContainer.get(connectionId)
  if (!audioEl) {
    audioEl = document.createElement('audio')
    audioEl.autoplay = true
    audioEl.setAttribute('playsinline', 'true')
    audioEl.style.display = 'none'
    document.body.appendChild(audioEl)
    globalAudioContainer.set(connectionId, audioEl)
  }
  if (audioEl.srcObject !== stream) {
    audioEl.srcObject = stream
  }
  audioEl.play().catch(() => {})
}

const clearGlobalAudioElement = (connectionId) => {
  const audioEl = globalAudioContainer.get(connectionId)
  if (audioEl) {
    audioEl.pause()
    audioEl.srcObject = null
    audioEl.remove()
    globalAudioContainer.delete(connectionId)
  }
}

const clearAllGlobalAudioElements = () => {
  for (const [connectionId, audioEl] of globalAudioContainer.entries()) {
    audioEl.pause()
    audioEl.srcObject = null
    audioEl.remove()
  }
  globalAudioContainer.clear()
}

export const useVoiceCallStore = defineStore('voiceCall', {
  state: () => ({
    activeVoiceChannel: null,
    participantsCount: 0,
    isMicEnabled: true,
    isCameraEnabled: false,
    isScreenSharing: false,
    callSession: null,
    callParticipants: [],
    remoteStreams: new Map(),
    leaveCallHandler: null,
    toggleMicHandler: null,
    toggleCamHandler: null
  }),
  getters: {
    hasActiveCall: state => Boolean(state.activeVoiceChannel),
    remoteVideoStreams: state => {
      if (!state.remoteStreams) return new Map()
      const result = new Map()
      for (const [connId, media] of state.remoteStreams.entries()) {
        if (media?.cameraStream && media.cameraStream.getVideoTracks().some(t => t.readyState === 'live')) {
          const participant = (state.callParticipants || []).find(p => p.connectionId === connId)
          result.set(connId, {
            ...media,
            participantName: participant?.displayName || 'Đồng nghiệp'
          })
        }
      }
      return result
    },
    hasRemoteVideo() {
      return this.remoteVideoStreams.size > 0
    },
    hasLocalCameraTrack: state => {
      if (!state.isCameraEnabled || !state.callSession) return false
      const stream = state.callSession.getLocalCameraStream?.() || state.callSession.getLocalStream?.()
      return Boolean(stream?.getVideoTracks?.().some(t => t.readyState === 'live' && t.enabled !== false))
    }
  },
  actions: {
    setActiveCall({ channel, session = null, participantsCount = 1, isMicEnabled = true, isCameraEnabled = false, leaveHandler = null, toggleMicHandler = null, toggleCamHandler = null }) {
      this.activeVoiceChannel = channel
      if (session) this.callSession = session
      this.participantsCount = participantsCount
      this.isMicEnabled = isMicEnabled
      this.isCameraEnabled = isCameraEnabled
      this.leaveCallHandler = leaveHandler
      this.toggleMicHandler = toggleMicHandler
      this.toggleCamHandler = toggleCamHandler
    },
    syncRemoteAudioStreams(remoteStreamsMap) {
      this.remoteStreams = remoteStreamsMap || new Map()
      if (remoteStreamsMap) {
        for (const [connectionId, media] of remoteStreamsMap.entries()) {
          if (media?.audioStream && media.audioStream.getAudioTracks().length > 0) {
            ensureGlobalAudioElement(connectionId, media.audioStream)
          } else {
            clearGlobalAudioElement(connectionId)
          }
        }
      }
    },
    updateCallStatus({ participantsCount, isMicEnabled, isCameraEnabled, isScreenSharing }) {
      if (participantsCount !== undefined) this.participantsCount = participantsCount
      if (isMicEnabled !== undefined) this.isMicEnabled = isMicEnabled
      if (isCameraEnabled !== undefined) this.isCameraEnabled = isCameraEnabled
      if (isScreenSharing !== undefined) this.isScreenSharing = isScreenSharing
    },
    clearCall() {
      clearAllGlobalAudioElements()
      this.activeVoiceChannel = null
      this.callSession = null
      this.callParticipants = []
      this.remoteStreams = new Map()
      this.participantsCount = 0
      this.isMicEnabled = true
      this.isCameraEnabled = false
      this.isScreenSharing = false
      this.leaveCallHandler = null
      this.toggleMicHandler = null
      this.toggleCamHandler = null
    },
    leaveCall() {
      if (typeof this.leaveCallHandler === 'function') {
        this.leaveCallHandler()
      } else {
        if (this.callSession && typeof this.callSession.leave === 'function') {
          this.callSession.leave().catch(() => {})
        }
        this.clearCall()
      }
    },
    toggleMic() {
      if (typeof this.toggleMicHandler === 'function') {
        this.toggleMicHandler()
      } else {
        if (this.callSession && typeof this.callSession.setMicrophoneEnabled === 'function') {
          const nextState = !this.isMicEnabled
          this.callSession.setMicrophoneEnabled(nextState).catch(() => {})
          this.isMicEnabled = nextState
        } else {
          this.isMicEnabled = !this.isMicEnabled
        }
      }
    },
    toggleCam() {
      if (typeof this.toggleCamHandler === 'function') {
        this.toggleCamHandler()
      } else {
        if (this.callSession && typeof this.callSession.setCameraEnabled === 'function') {
          const nextState = !this.isCameraEnabled
          this.callSession.setCameraEnabled(nextState).catch(() => {})
          this.isCameraEnabled = nextState
        } else {
          this.isCameraEnabled = !this.isCameraEnabled
        }
      }
    }
  }
})

