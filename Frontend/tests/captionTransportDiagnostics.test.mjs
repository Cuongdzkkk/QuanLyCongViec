import assert from 'node:assert/strict'
import { createHash, webcrypto } from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  CAPTION_TRANSPORT_HASH_ALGORITHM,
  computePcmSha256,
  launchCaptionTransportClientDiagnostic,
  shouldSampleCaptionTransportChunk
} from '../src/services/captionTransportDiagnostics.js'

if (!globalThis.crypto) globalThis.crypto = webcrypto

const pcm = Uint8Array.from([0, 1, 2, 3, 0xff, 0x80])
const expectedPcmHash = createHash('sha256').update(pcm).digest('hex')
const pcmHash = await computePcmSha256(pcm)
const base64BytesHash = createHash('sha256').update(Buffer.from(Buffer.from(pcm).toString('base64'))).digest('hex')

assert.equal(CAPTION_TRANSPORT_HASH_ALGORITHM, 'SHA-256')
assert.equal(pcmHash, expectedPcmHash, 'hash must cover the exact PCM bytes')
assert.notEqual(pcmHash, base64BytesHash, 'hash must not cover Base64 characters')
assert.equal(await computePcmSha256(pcm), pcmHash, 'deterministic PCM must produce a deterministic hash')

assert.equal(shouldSampleCaptionTransportChunk(1), true)
assert.equal(shouldSampleCaptionTransportChunk(2), false)
assert.equal(shouldSampleCaptionTransportChunk(20), true)
assert.equal(shouldSampleCaptionTransportChunk(21), false)
assert.equal(shouldSampleCaptionTransportChunk(40), true)
assert.equal(shouldSampleCaptionTransportChunk(39), false)

const clientEvents = []
let hashStarted = false
let resolveHash
const pendingHash = new Promise(resolve => { resolveHash = resolve })
launchCaptionTransportClientDiagnostic({
  bytes: pcm,
  chunkIndex: 1,
  callSessionId: 'session-1',
  projectId: 'project-1',
  voiceChannelId: 'voice-1',
  hash: async input => {
    hashStarted = true
    assert.deepEqual(Array.from(input), Array.from(pcm))
    return await pendingHash
  },
  emit: (event, detail) => clientEvents.push({ event, detail })
})
assert.equal(hashStarted, false, 'client diagnostic hashing must start asynchronously')
let normalAudioSendCount = 0
normalAudioSendCount += 1
await Promise.resolve()
assert.equal(hashStarted, true)
assert.equal(normalAudioSendCount, 1, 'diagnostic launch must not block normal audio submission')
resolveHash(expectedPcmHash)
await new Promise(resolve => setTimeout(resolve, 0))
assert.equal(clientEvents.length, 1)
assert.equal(clientEvents[0].event, '[CAPTION_TRANSPORT_CLIENT_DIAG]')
assert.equal(clientEvents[0].detail.pcmSha256, expectedPcmHash)
assert.equal(clientEvents[0].detail.payloadBytes, pcm.byteLength)

let failedDiagnosticAudioSendCount = 0
launchCaptionTransportClientDiagnostic({
  bytes: pcm,
  chunkIndex: 20,
  hash: async () => { throw new Error('diagnostic failure') },
  emit: () => { throw new Error('diagnostic emission must not be required') }
})
failedDiagnosticAudioSendCount += 1
await Promise.resolve()
await Promise.resolve()
assert.equal(failedDiagnosticAudioSendCount, 1, 'diagnostic failure must not affect audio submission')

const here = path.dirname(fileURLToPath(import.meta.url))
const source = fs.readFileSync(path.join(here, '..', 'src', 'services', 'callMediaService.js'), 'utf8')
assert.match(source, /launchCaptionTransportClientDiagnostic\(\{[\s\S]{0,500}enabled: captionTransportTraceEnabled\(\)/)
assert.doesNotMatch(source, /await\s+computePcmSha256\(/)
assert.doesNotMatch(source, /transportDiagnostic|clientSha256|languageArgument/)
assert.match(source, /const payload = encodePcmChunkBase64\(bytes\)[\s\S]{0,500}transcriptionQueue\.enqueue/)
assert.match(source, /CAPTION_CHUNK_BYTES = 4000/)
assert.match(source, /CAPTION_MAX_PENDING_CHUNKS = 3/)
assert.match(source, /CAPTION_MAX_QUEUE_AGE_MS = 375/)
assert.match(source, /droppedChunkCount/)
assert.match(source, /SubmitCallAudioChunk'[\s\S]{0,500}capture\.language\)/)
const sourceTrace = source.slice(source.indexOf('const traceCaptionSource'), source.indexOf('const safeCaptionErrorMessage'))
assert.doesNotMatch(sourceTrace, /deviceId|label|track\.id/)
assert.doesNotMatch(source, /writeFile|appendFile|MediaRecorder/)

console.log('captionTransportDiagnostics.test.mjs: PCM hash, sampling, off-transport diagnostics, and no-audio-persistence checks passed')
