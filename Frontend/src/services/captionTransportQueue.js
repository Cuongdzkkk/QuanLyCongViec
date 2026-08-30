export const createBoundedAsyncQueue = ({ maxPending = 4, onDrop = () => {} } = {}) => {
  const pending = []
  let running = false

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
    if (pending.length >= maxPending) {
      const dropped = pending.shift()
      dropped?.resolve({ dropped: true })
      onDrop({ pendingCount: pending.length + 1, maxPending, metadata })
    }
    pending.push({ task, resolve, reject, metadata })
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
    get pendingCount() { return pending.length },
    get isRunning() { return running }
  }
}
