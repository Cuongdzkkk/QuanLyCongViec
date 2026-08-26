import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const source = fs.readFileSync(path.join(here, '..', 'src', 'services', 'cameraBackgroundEffect.js'), 'utf8')

for (const needle of [
  "import('@mediapipe/tasks-vision')",
  'segmentForVideo',
  'captureStream',
  'PROCESS_WIDTH = 640',
  'PROCESS_FPS = 15',
  'PERSON_CATEGORY_VALUE = 0',
  'values[index] === PERSON_CATEGORY_VALUE ? 255 : 0',
  'dispose'
]) assert.ok(source.includes(needle), `missing ${needle}`)

assert.equal(source.includes('axios'), false)
assert.equal(source.includes('fetch('), false)

console.log('cameraBackgroundEffect.test.mjs: 8 privacy and pipeline checks passed')
