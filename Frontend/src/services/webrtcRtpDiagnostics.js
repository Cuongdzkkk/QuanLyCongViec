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
  }

  if (kind === 'audio') {
    const totalAudioEnergy = optionalMetric(entries, 'totalAudioEnergy')
    const audioLevel = optionalMetric(entries, 'audioLevel')
    if (totalAudioEnergy !== undefined) summary.totalAudioEnergy = totalAudioEnergy
    if (audioLevel !== undefined) summary.audioLevel = audioLevel
  }

  return summary
}
