<template>
  <aside v-if="debugEnabled" class="webrtc-diagnostics-panel" aria-label="WebRTC diagnostics">
    <div class="webrtc-diagnostics-panel__header">
      <div>
        <strong>WebRTC diagnostics</strong>
        <span>debug-only</span>
      </div>
      <button type="button" class="webrtc-diagnostics-panel__close" aria-label="Hide WebRTC diagnostics" @click="hidePanel">×</button>
    </div>
    <div class="webrtc-diagnostics-panel__actions">
      <button type="button" class="webrtc-diagnostics-panel__copy" :disabled="busy" @click="copyDiagnostics">
        {{ busy ? 'Collecting…' : 'Copy WebRTC diagnostics' }}
      </button>
      <button type="button" class="webrtc-diagnostics-panel__refresh" :disabled="busy" @click="refreshDiagnostics">Refresh</button>
    </div>
    <p v-if="statusMessage" class="webrtc-diagnostics-panel__status" role="status">{{ statusMessage }}</p>
    <textarea
      v-if="showFallback"
      v-model="report"
      class="webrtc-diagnostics-panel__fallback"
      aria-label="WebRTC diagnostics fallback"
      readonly
      @focus="selectFallback"
    ></textarea>
    <pre v-else-if="report" class="webrtc-diagnostics-panel__report">{{ report }}</pre>
  </aside>
</template>

<script setup>
import { computed, ref } from 'vue'
import {
  copyTextToClipboard,
  createSanitizedWebRtcReport,
  getMediaElementDiagnostics,
  isWebRtcDebugEnabled
} from '@/utils/webrtcRuntimeDiagnostics'

const props = defineProps({
  callSession: { type: Object, default: null }
})

const panelHidden = ref(false)
const debugEnabled = computed(() => isWebRtcDebugEnabled() && !panelHidden.value)
const report = ref('')
const statusMessage = ref('')
const showFallback = ref(false)
const busy = ref(false)
const appBuild = import.meta.env.VITE_APP_COMMIT || import.meta.env.VITE_COMMIT_SHA || 'unknown'

const wait = milliseconds => new Promise(resolve => window.setTimeout(resolve, milliseconds))

const collectReport = async () => {
  const first = await props.callSession?.getWebRtcRuntimeDiagnostics?.() || {
    callSessionPresent: false,
    roomPresent: false,
    participantCount: 0,
    peerSnapshots: [],
    iceServer: undefined,
    events: []
  }
  if (first.peerSnapshots?.length) await wait(2000)
  const snapshot = first.peerSnapshots?.length
    ? await props.callSession?.getWebRtcRuntimeDiagnostics?.() || first
    : first
  return createSanitizedWebRtcReport({
    ...snapshot,
    appBuild,
    debugEnabled: debugEnabled.value,
    mediaElements: getMediaElementDiagnostics()
  })
}

const refreshDiagnostics = async () => {
  busy.value = true
  statusMessage.value = ''
  showFallback.value = false
  try {
    report.value = await collectReport()
    statusMessage.value = 'Diagnostics refreshed.'
  } catch (error) {
    statusMessage.value = `Diagnostics unavailable: ${error?.name || 'Unknown error'}`
  } finally {
    busy.value = false
  }
}

const copyDiagnostics = async () => {
  busy.value = true
  statusMessage.value = ''
  showFallback.value = false
  try {
    report.value = await collectReport()
    if (await copyTextToClipboard(report.value)) {
      statusMessage.value = 'Diagnostics copied.'
    } else {
      showFallback.value = true
      statusMessage.value = 'Clipboard unavailable. Select the text below and copy it manually.'
    }
  } catch (error) {
    statusMessage.value = `Diagnostics unavailable: ${error?.name || 'Unknown error'}`
  } finally {
    busy.value = false
  }
}

const selectFallback = event => event.target?.select?.()
const hidePanel = () => {
  panelHidden.value = true
}
</script>

<style scoped>
.webrtc-diagnostics-panel {
  position: fixed;
  z-index: 2100;
  right: 16px;
  bottom: 16px;
  width: min(440px, calc(100vw - 32px));
  max-height: min(70vh, 620px);
  padding: 12px;
  overflow: hidden;
  color: #e8eef8;
  background: #101722;
  border: 1px solid #3b82f6;
  border-radius: 10px;
  box-shadow: 0 14px 34px rgb(0 0 0 / 28%);
  font: 12px/1.45 ui-monospace, SFMono-Regular, Consolas, monospace;
}

.webrtc-diagnostics-panel__header,
.webrtc-diagnostics-panel__actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.webrtc-diagnostics-panel__header strong,
.webrtc-diagnostics-panel__header span {
  display: block;
}

.webrtc-diagnostics-panel__header span,
.webrtc-diagnostics-panel__status {
  color: #a9b7cb;
}

.webrtc-diagnostics-panel__close,
.webrtc-diagnostics-panel__refresh,
.webrtc-diagnostics-panel__copy {
  min-height: 30px;
  padding: 5px 9px;
  color: inherit;
  border: 1px solid #52647c;
  border-radius: 6px;
  background: #1b2636;
  cursor: pointer;
}

.webrtc-diagnostics-panel__copy {
  color: #fff;
  border-color: #2563eb;
  background: #2563eb;
}

.webrtc-diagnostics-panel__close {
  min-width: 30px;
  padding: 0;
  font-size: 18px;
}

.webrtc-diagnostics-panel__actions {
  justify-content: flex-start;
  margin-top: 10px;
}

button:disabled {
  cursor: wait;
  opacity: .6;
}

.webrtc-diagnostics-panel__status {
  margin: 8px 0;
}

.webrtc-diagnostics-panel__report,
.webrtc-diagnostics-panel__fallback {
  box-sizing: border-box;
  width: 100%;
  max-height: 48vh;
  margin: 0;
  padding: 8px;
  overflow: auto;
  color: #d9e4f5;
  background: #0a0f17;
  border: 1px solid #26364d;
  border-radius: 6px;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}

.webrtc-diagnostics-panel__fallback {
  min-height: 180px;
  resize: vertical;
}
</style>
