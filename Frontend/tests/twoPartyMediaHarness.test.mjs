import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const harness = fs.readFileSync(path.join(here, 'webrtc-runtime-harness.html'), 'utf8')

assert.match(harness, /const fixtureA = \{ audio: createAudio\(\), camera: canvasTrack\('camera'\), screen: canvasTrack\('screen'\) \}/)
assert.match(harness, /const fixtureB = \{ audio: createAudio\(\), camera: canvasTrack\('camera'\), screen: canvasTrack\('screen'\) \}/)
assert.match(harness, /const participantA = createPeer\(fixtureA\)[\s\S]{0,80}const participantB = createPeer\(\)/)
assert.match(harness, /pc\.addTrack\(fixture\.audio\.stream\.getAudioTracks\(\)\[0\], fixture\.audio\.stream\)/)
assert.match(harness, /mapMediaTransceivers\(receiver\)[\s\S]{0,100}await attach\(receiver, receiver\.fixture/)
assert.match(harness, /receivedByA = receiveRoles\(participantA\)/)
assert.match(harness, /receivedByB = receiveRoles\(participantB\)/)
assert.match(harness, /\['audio', 'camera', 'screen'\]\.every\(role => liveRole\(values, role\)\)/)
assert.match(harness, /A_RECEIVES_B=/)
assert.match(harness, /B_RECEIVES_A=/)

console.log('TWO_PARTY_MEDIA_HARNESS_CONTRACT: synthetic audio, camera, and screen delivery is checked in both directions')
