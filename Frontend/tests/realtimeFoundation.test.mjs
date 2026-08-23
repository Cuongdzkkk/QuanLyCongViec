import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const read = relative => fs.readFileSync(path.join(here, '..', relative), 'utf8')
const config = read('src/services/realtimeHubConfig.js')
const call = read('src/services/callMediaService.js')
const chat = read('src/services/collaborationRealtime.js')
const notifications = read('src/components/NotificationsDropdown.vue')
const compose = fs.readFileSync(path.join(here, '..', '..', 'docker-compose.yml'), 'utf8')
const vercel = read('vercel.json')

assert.match(config, /SIGNALR_KEEP_ALIVE_MS = 15000/)
assert.match(config, /SIGNALR_SERVER_TIMEOUT_MS = 60000/)
assert.match(config, /withAutomaticReconnect\(SIGNALR_RECONNECT_DELAYS_MS\)/)
assert.match(call, /let activeCallSession = null/)
assert.match(call, /let startPromise = null/)
assert.match(call, /if \(startPromise\) return startPromise/)
assert.match(call, /if \(activeCallSession && activeCallSession !== session\) await activeCallSession\.leave\(\)/)
assert.match(chat, /HubConnectionState\.Connecting/)
assert.match(chat, /this\.connection = null/)
assert.match(notifications, /notificationStartPromise/)
assert.match(notifications, /notificationLifecycle/)
assert.match(compose, /Cors__AllowedOrigins__0: "https:\/\/sprinta\.id\.vn"/)
assert.match(compose, /Cors__AllowedOrigins__1: "https:\/\/www\.sprinta\.id\.vn"/)
assert.match(vercel, /"value": "www\.sprinta\.id\.vn"/)
assert.match(vercel, /https:\/\/sprinta\.id\.vn\/\$1/)

console.log('realtimeFoundation.test.mjs: hub lifecycle and origin allowlist checks passed')
