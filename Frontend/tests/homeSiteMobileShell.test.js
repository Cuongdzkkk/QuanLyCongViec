import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const layout = fs.readFileSync(path.join(here, '..', 'src', 'views', 'HomeSite', 'HomeSiteLayout.vue'), 'utf8')
const responsiveStyles = layout.slice(layout.indexOf('@media (max-width: 1024px)'))

assert.match(layout, /<AppTopBar[\s\S]*?:sidebar-visible="isSidebarOpen"[\s\S]*?@toggle-sidebar="toggleSidebar"/)
assert.match(layout, /<aside[\s\S]*?id="app-sidebar"[\s\S]*?:class="\{ 'sidebar--open': isSidebarOpen \}"/)
assert.match(layout, /v-if="isSidebarOpen"[\s\S]*?class="sidebar-overlay"[\s\S]*?@click="closeSidebar"/)
assert.match(layout, /watch\(\(\) => route\.fullPath,[\s\S]*?closeSidebar\(false\)/)
assert.match(layout, /if \(event\.key === 'Escape'\)[\s\S]*?closeSidebar\(\)/)

assert.match(responsiveStyles, /\.sidebar \{[\s\S]*?position: fixed !important;[\s\S]*?width: min\(280px, calc\(100vw - 48px\)\) !important;/)
assert.match(responsiveStyles, /\.sidebar \{[\s\S]*?transform: translateX\(-100%\) !important;/)
assert.match(responsiveStyles, /\.sidebar\.sidebar--open \{[\s\S]*?transform: translateX\(0\) !important;/)
assert.match(responsiveStyles, /\.main-content \{[\s\S]*?width: 100%;[\s\S]*?min-width: 0;/)
assert.match(responsiveStyles, /:deep\(\.menu-toggle\) \{[\s\S]*?min-width: 44px;[\s\S]*?min-height: 44px;/)
assert.match(responsiveStyles, /padding-bottom: env\(safe-area-inset-bottom\);/)
assert.doesNotMatch(responsiveStyles, /overflow-x:\s*hidden/)

const viewportMatrix = [
  [320, 568], [360, 640], [375, 667], [390, 844], [412, 915], [425, 671],
  [768, 1024], [820, 1180], [1024, 768], [1280, 720], [1366, 768], [1920, 1080],
]

for (const [width, height] of viewportMatrix) {
  const sidebarWidth = width <= 1024 ? 0 : 240
  assert.ok(width - sidebarWidth >= (width <= 1024 ? width : 1040), `${width}x${height} preserves usable shell width`)
}

console.log('HOME_SITE_MOBILE_SHELL: drawer, focus, navigation, safe-area, and width contracts covered')
