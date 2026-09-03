import { defineStore } from 'pinia'

export const useVoiceCallStore = defineStore('voiceCall', {
  state: () => ({
    activeVoiceChannel: null,
    participantsCount: 0,
    isMicEnabled: true,
    isCameraEnabled: false,
    isScreenSharing: false,
    leaveCallHandler: null,
    toggleMicHandler: null,
    toggleCamHandler: null
  }),
  getters: {
    hasActiveCall: state => Boolean(state.activeVoiceChannel)
  },
  actions: {
    setActiveCall({ channel, participantsCount = 1, isMicEnabled = true, isCameraEnabled = false, leaveHandler = null, toggleMicHandler = null, toggleCamHandler = null }) {
      this.activeVoiceChannel = channel
      this.participantsCount = participantsCount
      this.isMicEnabled = isMicEnabled
      this.isCameraEnabled = isCameraEnabled
      this.leaveCallHandler = leaveHandler
      this.toggleMicHandler = toggleMicHandler
      this.toggleCamHandler = toggleCamHandler
    },
    updateCallStatus({ participantsCount, isMicEnabled, isCameraEnabled, isScreenSharing }) {
      if (participantsCount !== undefined) this.participantsCount = participantsCount
      if (isMicEnabled !== undefined) this.isMicEnabled = isMicEnabled
      if (isCameraEnabled !== undefined) this.isCameraEnabled = isCameraEnabled
      if (isScreenSharing !== undefined) this.isScreenSharing = isScreenSharing
    },
    clearCall() {
      this.activeVoiceChannel = null
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
        this.clearCall()
      }
    },
    toggleMic() {
      if (typeof this.toggleMicHandler === 'function') {
        this.toggleMicHandler()
      } else {
        this.isMicEnabled = !this.isMicEnabled
      }
    },
    toggleCam() {
      if (typeof this.toggleCamHandler === 'function') {
        this.toggleCamHandler()
      } else {
        this.isCameraEnabled = !this.isCameraEnabled
      }
    }
  }
})
