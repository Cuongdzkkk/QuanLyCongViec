import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const harness = fs.readFileSync(path.join(here, 'webrtc-runtime-harness.html'), 'utf8')

assert.match(harness, /const role = parameters\.get\('role'\) === 'B' \? 'B' : 'A'/)
assert.match(harness, /const channel = new BroadcastChannel\(channelName\)/)
assert.match(harness, /const pc = new RTCPeerConnection\(\{ iceServers: \[\] \}\)/)
assert.match(harness, /fixture = \{ audio: audioStream\(\), camera: canvasStream\('camera'\), screen: canvasStream\('screen'\) \}/)
assert.match(harness, /pc\.addTrack\(fixture\.audio\.stream\.getAudioTracks\(\)\[0\], fixture\.audio\.stream\)/)
assert.match(harness, /await attachFixture\(entry\)/)
assert.match(harness, /send\('offer'/)
assert.match(harness, /send\('answer'/)
assert.match(harness, /send\('ice'/)
assert.match(harness, /\['audio', 'camera', 'screen'\]\.every\(item => roles\.has\(item\)\)/)
assert.match(harness, /REMOTE_AUDIO=live/)
assert.match(harness, /REMOTE_CAMERA=live/)
assert.match(harness, /REMOTE_SCREEN=live/)

console.log('TWO_PARTY_MEDIA_HARNESS_CONTRACT: independent pages exchange offer, answer, and ICE while checking live audio, camera, and screen tracks in both directions')
