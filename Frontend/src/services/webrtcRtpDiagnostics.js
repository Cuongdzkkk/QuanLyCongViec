const numeric = value => Number.isFinite(Number(value)) ? Number(value) : null

const reportEntries = report => {
  const entries = []
  report?.forEach?.(entry => entries.push(entry))
  return entries
}

const sumMetric = (entries, field) => entries.reduce((total, entry) => {
  const value = numeric(entry?.[field])
  return total + (value ?? 0)
}, 0)

const optionalMetric = (entries, field) => {
  const available = entries.filter(entry => numeric(entry?.[field]) !== null)
  return available.length ? sumMetric(available, field) : undefined
}

export const summarizeRtpReport = (report, direction, kind) => {
  const type = direction === 'outbound' ? 'outbound-rtp' : 'inbound-rtp'
  const entries = reportEntries(report).filter(entry =>
    entry?.type === type && (entry?.kind || entry?.mediaType) === kind)
  const foundKey = direction === 'outbound' ? 'outboundRtpFound' : 'inboundRtpFound'
  const summary = {
    [foundKey]: entries.length > 0,
    ...(direction === 'outbound'
      ? { packetsSent: sumMetric(entries, 'packetsSent'), bytesSent: sumMetric(entries, 'bytesSent') }
      : {
          packetsReceived: sumMetric(entries, 'packetsReceived'),
          bytesReceived: sumMetric(entries, 'bytesReceived'),
          ...(kind === 'audio' ? { packetsLost: sumMetric(entries, 'packetsLost') } : {})
        })
  }

  if (kind === 'video') {
    summary[direction === 'outbound' ? 'framesEncoded' : 'framesDecoded'] = sumMetric(
      entries,
      direction === 'outbound' ? 'framesEncoded' : 'framesDecoded')
    if (direction === 'inbound') {
      const framesReceived = optionalMetric(entries, 'framesReceived')
      if (framesReceived !== undefined) summary.framesReceived = framesReceived
    }
  }

  if (kind === 'audio') {
    const totalAudioEnergy = optionalMetric(entries, 'totalAudioEnergy')
    const audioLevel = optionalMetric(entries, 'audioLevel')
    if (totalAudioEnergy !== undefined) summary.totalAudioEnergy = totalAudioEnergy
    if (audioLevel !== undefined) summary.audioLevel = audioLevel
  }

  return summary
}

export const createBoundedPeriodicSampler = ({
  isActive,
  sample,
  intervalMs = 2000,
  maxDurationMs = 20000,
  setIntervalFn = globalThis.setInterval,
  clearIntervalFn = globalThis.clearInterval,
  setTimeoutFn = globalThis.setTimeout,
  clearTimeoutFn = globalThis.clearTimeout,
  nowFn = Date.now
}) => {
  let intervalId = null
  let deadlineId = null
  let deadlineAt = null
  let running = false

  const stop = () => {
    running = false
    if (intervalId !== null) clearIntervalFn(intervalId)
    if (deadlineId !== null) clearTimeoutFn(deadlineId)
    intervalId = null
    deadlineId = null
    deadlineAt = null
  }

  const tick = () => {
    if (!running || !isActive() || nowFn() > deadlineAt) {
      stop()
      return
    }
    void sample()
  }

  const start = () => {
    if (running || !isActive()) return false
    running = true
    deadlineAt = nowFn() + maxDurationMs
    tick()
    if (running) {
      intervalId = setIntervalFn(tick, intervalMs)
      deadlineId = setTimeoutFn(stop, maxDurationMs)
    }
    return true
  }

  return { start, stop }
}
