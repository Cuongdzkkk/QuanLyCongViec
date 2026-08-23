import assert from 'node:assert/strict'
import test from 'node:test'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const config = fs.readFileSync(path.join(here, '..', 'vite.config.js'), 'utf8')

test('checkout navigation bypasses a stale PWA app-shell entry', () => {
  assert.match(config, /registerType: 'autoUpdate'/)
  assert.match(config, /cleanupOutdatedCaches: true/)
  assert.match(config, /clientsClaim: true/)
  assert.match(config, /skipWaiting: true/)
  assert.ok(config.includes("/^\\/billing\\/checkout/"))
})
