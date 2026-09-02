import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const readView = (...parts) => fs.readFileSync(path.join(here, '..', 'src', 'views', 'HomeSite', ...parts), 'utf8')

const projects = readView('Projects', 'ProjectList.vue')
const notifications = readView('Tools', 'NotificationsView.vue')
const status = readView('Tools', 'SystemStatus.vue')

assert.match(projects, /const effectiveViewMode = computed\(\(\) => isMobileViewport\.value \? 'cards' : viewMode\.value\)/)
assert.match(projects, /window\.matchMedia\('\(max-width: 640px\)'\)/)
assert.match(projects, /<table class="jira-table" v-if="effectiveViewMode === 'table'">/)
assert.match(projects, /<button class="project-row-main project-row-open" type="button" @click="goToProject\(proj\.id\)">/)
assert.match(projects, /project-card-list[\s\S]*?updatedAt \|\| proj\.createdAt/)
assert.match(projects, /@media \(max-width: 640px\)[\s\S]*?\.row-action-btn[\s\S]*?min-height: 44px;/)

assert.match(notifications, /\.notifications-main \{[\s\S]*?min-width: 0;/)
assert.match(notifications, /\.notif-content \{[\s\S]*?min-width: 0;/)
assert.match(notifications, /@media \(max-width: 640px\)[\s\S]*?\.notifications-layout \{[\s\S]*?flex-direction: column;[\s\S]*?padding: 0 14px 28px;/)
assert.match(notifications, /@media \(max-width: 640px\)[\s\S]*?\.filters-sidebar \{[\s\S]*?width: 100%;[\s\S]*?flex-direction: row;/)
assert.match(notifications, /@media \(max-width: 640px\)[\s\S]*?\.filter-btn[\s\S]*?min-height: 44px;/)
assert.match(notifications, /overflow-wrap: anywhere;/)

assert.match(status, /@media \(max-width: 640px\)[\s\S]*?\.header-main \{[\s\S]*?flex-direction: column;/)
assert.match(status, /@media \(max-width: 640px\)[\s\S]*?\.secondary-btn \{[\s\S]*?width: 100%;/)
assert.match(status, /@media \(max-width: 640px\)[\s\S]*?\.timeline-header \{[\s\S]*?grid-template-columns: 44px minmax\(0, 1fr\) 44px;/)
assert.match(status, /@media \(max-width: 640px\)[\s\S]*?\.item-card-inner \{[\s\S]*?margin-left: 0;/)
assert.match(status, /@media \(max-width: 640px\)[\s\S]*?\.page-content \{[\s\S]*?padding: 24px 14px;/)

for (const [name, source] of Object.entries({ projects, notifications, status })) {
  const responsiveStyles = source.slice(source.indexOf('@media'))
  assert.doesNotMatch(responsiveStyles, /overflow-x:\s*hidden/, `${name} must fix the width cause instead of masking it`)
}

const viewportMatrix = [
  [320, 568], [360, 640], [375, 667], [390, 844], [412, 915], [425, 671],
  [768, 1024], [820, 1180], [1024, 768], [1280, 720], [1366, 768], [1920, 1080],
]

for (const [width, height] of viewportMatrix) {
  assert.ok(width >= 320 && height >= 568, `${width}x${height} belongs to the supported viewport contract`)
}

console.log('HOME_SITE_RESPONSIVE_CONTENT: projects, notifications, and status contracts covered')
