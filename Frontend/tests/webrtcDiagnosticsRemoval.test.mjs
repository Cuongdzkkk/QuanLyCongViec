import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const frontendRoot = path.join(here, '..')
const view = fs.readFileSync(path.join(frontendRoot, 'src', 'views', 'CollaborationChat.vue'), 'utf8')
const callMediaService = fs.readFileSync(path.join(frontendRoot, 'src', 'services', 'callMediaService.js'), 'utf8')
const panelPath = path.join(frontendRoot, 'src', 'components', 'WebRtcDiagnosticsPanel.vue')
const collectorPath = path.join(frontendRoot, 'src', 'utils', 'webrtcRuntimeDiagnostics.js')

assert.equal(fs.existsSync(panelPath), false, 'the obsolete diagnostics panel must be removed')
assert.equal(fs.existsSync(collectorPath), false, 'the UI-only runtime collector must be removed')
assert.doesNotMatch(view, /WebRtcDiagnosticsPanel|WebRTC diagnostics|Copy WebRTC diagnostics/)
assert.doesNotMatch(view, /webrtcDebug|debug_webrtc_media/)
assert.doesNotMatch(callMediaService, /webrtcDebug|debug_webrtc_media|getWebRtcRuntimeDiagnostics|webrtcRuntimeDiagnostics/)
assert.match(view, /@click="toggleCallMicrophone"/)
assert.match(view, /@click="toggleCallCameraReal"/)
assert.match(view, /@click="leaveVoiceChannel"/)

console.log('WEBRTC_DIAGNOSTICS_REMOVAL: panel and public activation paths absent; call controls remain')
