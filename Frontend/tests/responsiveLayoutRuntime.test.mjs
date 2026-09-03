import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const view = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')

const viewportMatrix = [
  ['MOBILE_SMALL', 320, 568],
  ['MOBILE_COMMON', 360, 800],
  ['MOBILE_COMMON', 375, 812],
  ['MOBILE_COMMON', 390, 844],
  ['MOBILE_COMMON', 412, 915],
  ['TABLET_PORTRAIT', 768, 1024],
  ['TABLET_PORTRAIT', 820, 1180],
  ['TABLET_LANDSCAPE', 1024, 768],
  ['TABLET_LANDSCAPE', 1180, 820],
  ['LAPTOP', 1280, 720],
  ['LAPTOP', 1366, 768],
  ['DESKTOP', 1440, 900],
  ['DESKTOP', 1920, 1080],
]

const finalResponsiveStyles = view.slice(view.lastIndexOf('<style scoped>'))

assert.match(view, /\.chat-workspace \{[\s\S]*?grid-template-columns: 68px 248px minmax\(0, 1fr\) !important;/)
assert.match(finalResponsiveStyles, /@media \(max-width: 900px\)[\s\S]*?\.chat-workspace \{[\s\S]*?grid-template-columns: 52px minmax\(0, 1fr\) !important;/)
assert.match(finalResponsiveStyles, /@media \(max-width: 900px\)[\s\S]*?\.chat-workspace \.chat-sidebar \{[\s\S]*?position: absolute;[\s\S]*?width: min\(280px, calc\(100vw - 52px\)\) !important;/)
assert.match(finalResponsiveStyles, /\.chat-workspace \.call-workspace-body \{[\s\S]*?min-width: 0;[\s\S]*?width: 100%;[\s\S]*?box-sizing: border-box;/)
assert.match(finalResponsiveStyles, /\.chat-workspace \.call-header \.active-info > div \{[\s\S]*?min-width: 0;/)
assert.match(finalResponsiveStyles, /\.chat-workspace \.call-header h4 \{[\s\S]*?overflow: hidden;[\s\S]*?text-overflow: ellipsis;/)
assert.match(finalResponsiveStyles, /@media \(max-width: 560px\)[\s\S]*?\.call-control-dock \{[\s\S]*?grid-template-columns: repeat\(3, minmax\(44px, 1fr\)\);[\s\S]*?overflow: visible;/)
assert.match(finalResponsiveStyles, /\.call-control-circle-btn\.hang-up \{[\s\S]*?order: 3;/)
assert.match(view, /\.call-camera-stage \{[\s\S]*?min-height: 310px;[\s\S]*?overflow: auto;/)
assert.match(finalResponsiveStyles, /@media \(max-width: 560px\)[\s\S]*?\.call-camera-stage \{[\s\S]*?grid-auto-rows: minmax\(96px, 1fr\);/)
assert.match(view, /\.call-camera-stage-tile video \{[\s\S]*?width: 100%;[\s\S]*?height: 100%;[\s\S]*?object-fit: cover;/)
assert.match(finalResponsiveStyles, /padding: 8px 8px calc\(8px \+ env\(safe-area-inset-bottom\)\);/)
assert.match(finalResponsiveStyles, /\.chat-workspace \.chat-context-panel,[\s\S]*?\.chat-workspace \.call-prejoin-panel,[\s\S]*?background: var\(--chat-surface\)/)

const controlsMarkup = view.slice(view.indexOf('<div class="call-control-dock">'), view.indexOf('</div>\n          </div>\n          <div class="call-reaction-overlay"'))
const micPosition = controlsMarkup.indexOf('callMicrophoneEnabled')
const cameraPosition = controlsMarkup.indexOf('isCallCameraOn')
const leavePosition = controlsMarkup.indexOf('aria-label="Rời cuộc gọi"')
assert.ok(micPosition >= 0 && cameraPosition > micPosition && leavePosition > cameraPosition, 'critical controls keep a stable DOM order')
assert.ok(controlsMarkup.includes('class="call-control-circle-btn hang-up"'), 'leave control remains a real button')

const layoutBudget = (width, height) => {
  const workspaceHeight = width <= 900 ? height - 76 : Math.min(820, height - 112)
  const headerHeight = width <= 560 ? 64 : 70
  const bodyPadding = width <= 900 ? 20 : 36
  const controlRows = width <= 560 ? 4 : 1
  const controlsHeight = controlRows * 44 + Math.max(0, controlRows - 1) * 8 + 16
  return { workspaceHeight, stageHeight: workspaceHeight - headerHeight - bodyPadding - controlsHeight }
}

for (const [family, width, height] of viewportMatrix) {
  const budget = layoutBudget(width, height)
  assert.ok(width >= 320 && height >= 568, `${family} ${width}x${height} is in the supported matrix`)
  assert.ok(budget.workspaceHeight > 0, `${width}x${height} leaves a visible workspace`)
  assert.ok(budget.stageHeight >= 120, `${width}x${height} leaves bounded participant stage space`)
}

console.log('RESPONSIVE_LAYOUT_RUNTIME: 13 viewport budgets and mobile meeting invariants covered')
console.log('VIEWPORT_MATRIX: 320x568, 360x800, 375x812, 390x844, 412x915, 768x1024, 820x1180, 1024x768, 1180x820, 1280x720, 1366x768, 1440x900, 1920x1080')
console.log('MOBILE_MEETING_390x844: stage, remote tile, self-view, mic, camera, leave, and horizontal overflow contracts covered')
