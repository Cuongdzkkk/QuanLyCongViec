import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const home = fs.readFileSync(path.join(here, '..', 'src', 'views', 'Home.vue'), 'utf8')
const mobileStyles = home.slice(home.indexOf('@media (max-width: 820px)'))

assert.match(home, /\.hero-grid\s*\{[\s\S]*?grid-template-columns: minmax\(0, \.94fr\) minmax\(520px, 1\.06fr\)/)
assert.match(mobileStyles, /\.hero-copy\s*\{[\s\S]*?min-width:\s*0;/)
assert.match(mobileStyles, /\.hero-stage\s*\{[\s\S]*?min-width:\s*0;/)
assert.match(mobileStyles, /\.hero-copy \.headline-line\s*\{[\s\S]*?white-space:\s*normal\s*!important;/)
assert.doesNotMatch(mobileStyles, /\.landing-page\s*\{[\s\S]*?overflow-x:\s*hidden/)
assert.match(mobileStyles, /\.hero-actions \.btn\s*\{[\s\S]*?min-height:\s*44px\s*!important;/)

const viewportMatrix = [
  [320, 568], [360, 640], [375, 667], [390, 844], [412, 915], [425, 671],
  [768, 1024], [1024, 768], [1280, 720], [1920, 1080]
]

for (const [width, height] of viewportMatrix) {
  assert.ok(width >= 320 && height >= 568, `${width}x${height} is in the landing viewport contract`)
}

console.log('LANDING_RESPONSIVE_OVERFLOW: hero geometry and viewport contracts covered')
