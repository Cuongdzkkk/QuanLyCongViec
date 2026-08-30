export const createBoundedAsyncQueue = ({ maxPending = 4, maxPendingAgeMs = Number.POSITIVE_INFINITY, onDrop = () => {}, now = () => performance.now() } = {}) => {
  const pending = []
  let running = false
  let droppedChunkCount = 0

  const diagnostics = () => ({
    queueDepth: pending.length,
    oldestQueuedAgeMs: pending.length ? Math.max(0, Math.round(now() - pending[0].queuedAt)) : 0,
    droppedChunkCount
  })

  const dropOldest = reason => {
    const dropped = pending.shift()
    if (!dropped) return false
    droppedChunkCount += 1
    dropped.resolve({ dropped: true, reason })
    onDrop({ ...diagnostics(), maxPending, maxPendingAgeMs, reason, droppedMetadata: dropped.metadata })
    return true
  }

  const drain = async () => {
    if (running) return
    running = true
    try {
      while (pending.length) {
        const entry = pending.shift()
        try {
          await entry.task()
          entry.resolve()
        } catch (error) {
          entry.reject(error)
        }
      }
    } finally {
      running = false
      if (pending.length) void drain()
    }
  }

  const enqueue = (task, metadata = {}) => new Promise((resolve, reject) => {
    while (pending.length && diagnostics().oldestQueuedAgeMs > maxPendingAgeMs) dropOldest('stale-audio-backpressure')
    if (pending.length >= maxPending) dropOldest('bounded-audio-backpressure')
    pending.push({ task, resolve, reject, metadata, queuedAt: now() })
    void drain()
  })

  const clear = predicate => {
    for (let index = pending.length - 1; index >= 0; index -= 1) {
      if (!predicate || predicate(pending[index].metadata)) {
        pending[index].resolve({ dropped: true })
        pending.splice(index, 1)
      }
    }
  }

  return {
    enqueue,
    clear,
    getDiagnostics: diagnostics,
    get pendingCount() { return pending.length },
    get isRunning() { return running }
  }
}
