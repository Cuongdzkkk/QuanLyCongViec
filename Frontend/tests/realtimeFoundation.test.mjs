import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import * as signalR from '@microsoft/signalr'
import {
  configureRealtimeHub,
  SIGNALR_KEEP_ALIVE_MS,
  SIGNALR_SERVER_TIMEOUT_MS
} from '../src/services/realtimeHubConfig.js'

const here = path.dirname(fileURLToPath(import.meta.url))
const read = relative => fs.readFileSync(path.join(here, '..', relative), 'utf8')
const config = read('src/services/realtimeHubConfig.js')
const call = read('src/services/callMediaService.js')
const chat = read('src/services/collaborationRealtime.js')
const notificationHub = read('src/components/NotificationsDropdown.vue')
const kanbanHub = read('src/api/signalrService.js')
const notifications = read('src/components/NotificationsDropdown.vue')
const compose = fs.readFileSync(path.join(here, '..', '..', 'docker-compose.yml'), 'utf8')
const vercel = read('vercel.json')

const configuredBuilder = configureRealtimeHub(new signalR.HubConnectionBuilder())
assert.equal(typeof configuredBuilder.withAutomaticReconnect, 'function')
assert.equal(typeof configuredBuilder.withServerTimeout, 'function')
assert.equal(typeof configuredBuilder.withKeepAliveInterval, 'function')
assert.equal(typeof configuredBuilder.withServerTimeoutInMilliseconds, 'undefined')
assert.equal(typeof configuredBuilder.withKeepAliveIntervalInMilliseconds, 'undefined')
const connection = configuredBuilder.withUrl('http://localhost:5136/hubs/test').build()
assert.equal(connection.serverTimeoutInMilliseconds, SIGNALR_SERVER_TIMEOUT_MS)
assert.equal(connection.keepAliveIntervalInMilliseconds, SIGNALR_KEEP_ALIVE_MS)

for (const [name, source] of Object.entries({ notificationHub, chat, kanbanHub, call })) {
  assert.match(source, /configureRealtimeHub/)
  assert.doesNotMatch(source, /withKeepAliveIntervalInMilliseconds|withServerTimeoutInMilliseconds/, `${name} uses an invalid SignalR builder API`)
}

assert.match(config, /SIGNALR_KEEP_ALIVE_MS = 15000/)
assert.match(config, /SIGNALR_SERVER_TIMEOUT_MS = 60000/)
assert.match(config, /createTokenAwareReconnectPolicy\(getCurrentAccessToken, SIGNALR_RECONNECT_DELAYS_MS\)/)
assert.match(config, /withKeepAliveInterval\(SIGNALR_KEEP_ALIVE_MS\)/)
assert.match(config, /withServerTimeout\(SIGNALR_SERVER_TIMEOUT_MS\)/)
assert.doesNotMatch(config, /withKeepAliveIntervalInMilliseconds|withServerTimeoutInMilliseconds/)
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
assert.doesNotMatch(vercel, /"redirects"/)
assert.match(vercel, /"source": "\/\(\(\?!videos\/\)\.\*\)"/)

console.log('realtimeFoundation.test.mjs: hub lifecycle and origin allowlist checks passed')
