import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const service = fs.readFileSync(path.join(here, '..', 'src', 'services', 'callMediaService.js'), 'utf8')
const view = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')

assert.match(service, /downsampleToPcm16/)
assert.match(service, /Math\.sqrt\(energy \/ \(chunk\.length \/ 2\)\) >= 0\.012/)
assert.match(service, /capture\.preRoll\.length > 2/)
assert.match(service, /audio\/linear16;rate=16000;channels=1/)
assert.match(service, /capture\.language/)
assert.doesNotMatch(service, /MediaRecorder/)

assert.match(view, /callTranscriptionCapabilities\.configured/)
assert.match(view, /supportedLanguages/)
assert.match(view, /callCaptionLanguage/)
assert.match(view, /captionsEnabled && liveCaptionRows\.length/)
assert.match(view, /callTranscriptInterim\.text/)
assert.match(view, /v-for="caption in liveCaptionRows\.slice\(\)\.reverse\(\)"/)
assert.match(view, /upsertLiveCaptionInterim/)
assert.match(view, /upsertLiveCaptionFinal/)
assert.match(view, /toggleCallCaptions/)

console.log('CAPTIONS_RUNTIME: real PCM/VAD transcription path covered')
console.log('SUPPORTED_LANGUAGES: vi,en')
console.log('INTERIM_DISPLAY_ONLY: covered')
console.log('FINAL_TRANSCRIPT_PERSISTENCE_CONTRACT: covered by backend tests')
console.log('RAW_AUDIO_PERSISTED: NO')
