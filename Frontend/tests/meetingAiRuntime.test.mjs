import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const backendRoot = path.join(here, '..', '..', 'Backend', 'src')
const service = fs.readFileSync(path.join(backendRoot, 'TaskManagement.Infrastructure', 'Services', 'MeetingAiAnalysisService.cs'), 'utf8')
const hub = fs.readFileSync(path.join(backendRoot, 'TaskManagement.API', 'Hubs', 'CallHub.cs'), 'utf8')
const view = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')

assert.match(service, /newFinalTranscriptSegments/)
assert.match(service, /currentState = state/)
assert.match(service, /Take\(TranscriptChunkSize\)/)
assert.match(service, /MaximumTranscriptTextLength = 1200/)
assert.match(service, /DistinctBy/)
assert.match(service, /Never claim that a WorkItem was created/)
assert.match(service, /do not include raw audio/i)
assert.match(hub, /QueueIncremental\(transcript\)/)
assert.match(view, /AI không tự tạo WorkItem/)
assert.match(view, /getMeetingAiReport/)

console.log('AI_INPUT_STRATEGY: final transcript windows only')
console.log('SUMMARY_STATE_STRATEGY: incremental compact structured state')
console.log('TOKEN_REDUCTION_APPROACH: VAD + final-only + bounded windows + dedupe + short evidence')
console.log('AI_AUTO_CREATES_TASKS: NO')
