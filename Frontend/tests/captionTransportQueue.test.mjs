import assert from 'node:assert/strict'
import { createBoundedAsyncQueue } from '../src/services/captionTransportQueue.js'

const waitFor = async predicate => {
  for (let attempt = 0; attempt < 20; attempt += 1) {
    if (predicate()) return
    await new Promise(resolve => setTimeout(resolve, 0))
  }
  assert.fail('queue did not drain')
}

let releaseFirst
const first = new Promise(resolve => { releaseFirst = resolve })
const ran = []
const drops = []
const queue = createBoundedAsyncQueue({ maxPending: 2, onDrop: detail => drops.push(detail) })

void queue.enqueue(async () => { ran.push('first'); await first })
void queue.enqueue(async () => { ran.push('stale-1') })
void queue.enqueue(async () => { ran.push('stale-2') })
void queue.enqueue(async () => { ran.push('latest') })

assert.equal(queue.pendingCount, 2)
assert.equal(drops.length, 1)
releaseFirst()
await waitFor(() => !queue.isRunning && queue.pendingCount === 0)
assert.deepEqual(ran, ['first', 'stale-2', 'latest'])
assert.equal(drops[0].reason, 'bounded-audio-backpressure')
assert.equal(drops[0].droppedChunkCount, 1)

let now = 0
const staleDrops = []
const staleQueue = createBoundedAsyncQueue({
  maxPending: 3,
  maxPendingAgeMs: 375,
  now: () => now,
  onDrop: detail => staleDrops.push(detail)
})
let releaseStaleFirst
const staleFirst = new Promise(resolve => { releaseStaleFirst = resolve })
void staleQueue.enqueue(async () => { await staleFirst })
void staleQueue.enqueue(async () => {})
now = 400
void staleQueue.enqueue(async () => {})
assert.equal(staleDrops[0].reason, 'stale-audio-backpressure')
assert.equal(staleDrops[0].droppedChunkCount, 1)
releaseStaleFirst()
await waitFor(() => !staleQueue.isRunning && staleQueue.pendingCount === 0)

let released = false
const clearQueue = createBoundedAsyncQueue({ maxPending: 3 })
void clearQueue.enqueue(async () => { released = true })
clearQueue.clear()
await waitFor(() => !clearQueue.isRunning && clearQueue.pendingCount === 0)
assert.equal(released, true)

console.log('CAPTION_TRANSPORT_QUEUE: bounded pending audio, stale queued chunk drop, and capture cleanup covered')
