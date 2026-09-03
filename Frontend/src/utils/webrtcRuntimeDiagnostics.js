const MAX_EVENTS = 120

let recentEvents = []
let iceServerDiagnostics = {
  httpStatus: null,
  iceServerCount: 0,
  stunPresent: false,
  turnPresent: false,
  turnServerCount: 0
}
const mediaElementDiagnostics = new Map()

const asFiniteNumber = value => typeof value === 'number' && Number.isFinite(value) ? value : null
const asBooleanOrNull = value => typeof value === 'boolean' ? value : null
const normalizeKind = value => value === 'audio' || value === 'video' ? value : ''
const yesNo = value => value === true ? 'YES' : value === false ? 'NO' : 'INCONCLUSIVE'
const valueOrDash = value => value === null || value === undefined || value === '' ? '-' : `${value}`

export const isWebRtcDebugEnabled = (windowLike = globalThis.window) => {
  try {
    const query = new URLSearchParams(windowLike?.location?.search || '')
    return query.get('webrtcDebug') === '1' || windowLike?.localStorage?.getItem('debug_webrtc_media') === '1'
  } catch {
    return false
  }
}

const sanitizeEvent = (event, detail = {}) => ({
  event: `${event || 'UNKNOWN'}`.slice(0, 80),
  ...(normalizeKind(detail.trackKind || detail.kind) ? { trackKind: normalizeKind(detail.trackKind || detail.kind) } : {}),
  ...(detail.mediaRole === 'audio' || detail.mediaRole === 'camera' || detail.mediaRole === 'screen' ? { mediaRole: detail.mediaRole } : {}),
  ...(typeof detail.result === 'string' && detail.result ? { result: detail.result.slice(0, 80) } : {}),
  ...(typeof detail.errorName === 'string' && detail.errorName ? { errorName: detail.errorName.slice(0, 80) } : {}),
  ...(typeof detail.connectionState === 'string' && detail.connectionState ? { connectionState: detail.connectionState.slice(0, 40) } : {}),
  ...(typeof detail.iceConnectionState === 'string' && detail.iceConnectionState ? { iceConnectionState: detail.iceConnectionState.slice(0, 40) } : {})
})

export const recordWebRtcDiagnosticEvent = (event, detail = {}) => {
  recentEvents = [...recentEvents, sanitizeEvent(event, detail)].slice(-MAX_EVENTS)
}

export const getRecentWebRtcDiagnosticEvents = () => recentEvents.map(event => ({ ...event }))

const sanitizeIceUrlShape = value => {
  const urls = Array.isArray(value) ? value : [value]
  return urls.filter(url => typeof url === 'string').map(url => url.trim().toLowerCase()).filter(Boolean)
}

export const sanitizeIceServerConfig = (servers = [], { httpStatus = null } = {}) => {
  const list = Array.isArray(servers) ? servers : []
  const urls = list.flatMap(server => sanitizeIceUrlShape(server?.urls ?? server?.Urls))
  const stunUrls = urls.filter(url => url.startsWith('stun:') || url.startsWith('stuns:'))
  const turnUrls = urls.filter(url => url.startsWith('turn:') || url.startsWith('turns:'))
  return {
    httpStatus: asFiniteNumber(httpStatus),
    iceServerCount: list.length,
    stunPresent: stunUrls.length > 0,
    turnPresent: turnUrls.length > 0,
    turnServerCount: turnUrls.length
  }
}

export const recordIceServerDiagnostics = (servers = [], options = {}) => {
  iceServerDiagnostics = sanitizeIceServerConfig(servers, options)
  return { ...iceServerDiagnostics }
}

export const getIceServerDiagnostics = () => ({ ...iceServerDiagnostics })

const statsEntries = report => {
  const entries = []
  if (report?.forEach) report.forEach(value => entries.push(value))
  else if (report?.[Symbol.iterator]) {
    for (const item of report) entries.push(Array.isArray(item) ? item[1] : item)
  }
  return entries
}

const aggregateRtp = (entries, direction, kind) => {
  const selected = entries.filter(item => item?.type === `${direction}-rtp` && normalizeKind(item.kind || item.mediaType) === kind)
  if (!selected.length) return null
  const sum = keys => keys.reduce((total, key) => total + selected.reduce((subtotal, item) => subtotal + (asFiniteNumber(item?.[key]) || 0), 0), 0)
  const packets = direction === 'inbound' ? sum(['packetsReceived']) : sum(['packetsSent'])
  const bytes = direction === 'inbound' ? sum(['bytesReceived']) : sum(['bytesSent'])
  const frames = direction === 'inbound' ? sum(['framesReceived']) : sum(['framesEncoded'])
  const decoded = sum(['framesDecoded'])
  const encoded = sum(['framesEncoded'])
  const totalAudioEnergy = sum(['totalAudioEnergy'])
  const audioLevels = selected.map(item => asFiniteNumber(item?.audioLevel)).filter(value => value !== null)
  return {
    packets,
    bytes,
    ...(kind === 'video' || frames > 0 ? { frames } : {}),
    ...(decoded > 0 ? { framesDecoded: decoded } : {}),
    ...(encoded > 0 ? { framesEncoded: encoded } : {}),
    ...(kind === 'audio' && totalAudioEnergy > 0 ? { totalAudioEnergy } : {}),
    ...(audioLevels.length ? { audioLevel: Math.max(...audioLevels) } : {}),
    count: selected.length
  }
}

const withDeltas = (current, previous) => {
  if (!current) return null
  const prior = previous || {}
  const increasing = key => asFiniteNumber(current[key]) !== null && asFiniteNumber(prior[key]) !== null
    ? current[key] > prior[key]
    : null
  return {
    ...current,
    packetsIncreasing: increasing('packets'),
    bytesIncreasing: increasing('bytes'),
    ...(Object.prototype.hasOwnProperty.call(current, 'frames') ? { framesIncreasing: increasing('frames') } : {})
  }
}

const describeTracks = tracks => (tracks || []).map(item => {
  const track = item?.track
  return {
    kind: normalizeKind(track?.kind) || 'unknown',
    present: Boolean(track),
    enabled: asBooleanOrNull(track?.enabled),
    muted: asBooleanOrNull(track?.muted),
    readyState: typeof track?.readyState === 'string' ? track.readyState : 'unknown'
  }
})

export const collectPeerRuntimeStats = async ({ pc, previous = null } = {}) => {
  const entries = statsEntries(await pc?.getStats?.())
  const pairs = entries.filter(item => item?.type === 'candidate-pair')
  const selectedPair = pairs
    .filter(item => item?.selected === true || item?.nominated === true || item?.state === 'succeeded')
    .sort((a, b) => Number(b?.selected === true) - Number(a?.selected === true) || Number(b?.nominated === true) - Number(a?.nominated === true))[0]
  const byId = new Map(entries.map(item => [item?.id, item]))
  const local = selectedPair ? byId.get(selectedPair.localCandidateId) : null
  const remote = selectedPair ? byId.get(selectedPair.remoteCandidateId) : null
  const rtp = { audio: {}, video: {} }
  for (const kind of ['audio', 'video']) {
    for (const direction of ['inbound', 'outbound']) {
      const current = aggregateRtp(entries, direction, kind)
      if (current) rtp[kind][direction] = withDeltas(current, previous?.rtp?.[kind]?.[direction])
    }
  }
  return {
    sampledAt: new Date().toISOString(),
    connectionState: typeof pc?.connectionState === 'string' ? pc.connectionState : 'unknown',
    iceConnectionState: typeof pc?.iceConnectionState === 'string' ? pc.iceConnectionState : 'unknown',
    signalingState: typeof pc?.signalingState === 'string' ? pc.signalingState : 'unknown',
    selectedCandidatePair: selectedPair ? {
      state: typeof selectedPair.state === 'string' ? selectedPair.state : 'unknown',
      localCandidateType: typeof local?.candidateType === 'string' ? local.candidateType : 'unknown',
      remoteCandidateType: typeof remote?.candidateType === 'string' ? remote.candidateType : 'unknown',
      protocol: typeof selectedPair.protocol === 'string' ? selectedPair.protocol : 'unknown',
      relayProtocol: typeof local?.relayProtocol === 'string' ? local.relayProtocol : ''
    } : null,
    rtp,
    tracks: {
      senders: describeTracks(pc?.getSenders?.()),
      receivers: describeTracks(pc?.getReceivers?.())
    }
  }
}

export const describeMediaElement = (element, { mediaRole = '' } = {}) => {
  const tracks = element?.srcObject?.getTracks?.() || []
  return {
    mediaRole: mediaRole || (element?.tagName === 'AUDIO' ? 'audio' : 'video'),
    elementKind: element?.tagName === 'AUDIO' ? 'audio' : 'video',
    hasSrcObject: Boolean(element?.srcObject),
    streamActive: element?.srcObject?.active === true,
    audioTrackCount: tracks.filter(track => track?.kind === 'audio').length,
    videoTrackCount: tracks.filter(track => track?.kind === 'video').length,
    trackStates: tracks.map(track => ({
      kind: normalizeKind(track?.kind) || 'unknown',
      enabled: asBooleanOrNull(track?.enabled),
      muted: asBooleanOrNull(track?.muted),
      readyState: typeof track?.readyState === 'string' ? track.readyState : 'unknown'
    })),
    autoplay: element?.autoplay === true,
    muted: element?.muted === true,
    paused: element?.paused === true,
    readyState: asFiniteNumber(element?.readyState),
    volume: asFiniteNumber(element?.volume),
    playResult: 'not-recorded',
    playErrorName: ''
  }
}

export const recordMediaElementDiagnostic = (element, { mediaRole = '', playResult = 'not-recorded', errorName = '' } = {}) => {
  if (!element) return
  mediaElementDiagnostics.set(element, {
    ...describeMediaElement(element, { mediaRole }),
    playResult: playResult === 'ok' || playResult === 'error' ? playResult : 'not-recorded',
    playErrorName: typeof errorName === 'string' ? errorName.slice(0, 80) : ''
  })
}

export const getMediaElementDiagnostics = (elements = []) => {
  const current = [...mediaElementDiagnostics.entries()]
    .filter(([element]) => !elements.length || elements.includes(element))
    .map(([, state]) => ({ ...state, trackStates: state.trackStates.map(track => ({ ...track })) }))
  return current
}

const anyIncreasing = (peerSnapshots, direction, kind, field) => peerSnapshots.some(peer => peer?.rtp?.[kind]?.[direction]?.[field] === true)
const hasEvent = (events, event, kind = '') => events.some(item => item?.event === event && (!kind || item?.trackKind === kind))

const classifyReport = ({ peerSnapshots, events, mediaElements }) => {
  if (!peerSnapshots.length) return 'INCONCLUSIVE'
  const inboundIncreasing = anyIncreasing(peerSnapshots, 'inbound', 'audio', 'packetsIncreasing') || anyIncreasing(peerSnapshots, 'inbound', 'video', 'packetsIncreasing')
  const outboundIncreasing = anyIncreasing(peerSnapshots, 'outbound', 'audio', 'packetsIncreasing') || anyIncreasing(peerSnapshots, 'outbound', 'video', 'packetsIncreasing')
  const badIce = peerSnapshots.some(peer => ['failed', 'disconnected', 'closed'].includes(peer?.iceConnectionState))
  const bound = mediaElements.some(element => element?.hasSrcObject)
  const playbackFailed = mediaElements.some(element => element?.playResult === 'error')
  if (badIce && !inboundIncreasing) return 'ICE_OR_NETWORK_FAILURE'
  if (inboundIncreasing && !bound && (hasEvent(events, 'REMOTE_TRACK_RECEIVED', 'audio') || hasEvent(events, 'REMOTE_TRACK_RECEIVED', 'video'))) return 'FRONTEND_BINDING_FAILURE'
  if (inboundIncreasing && bound && playbackFailed) return 'MEDIA_PLAYBACK_FAILURE'
  if (outboundIncreasing && !inboundIncreasing) return 'REMOTE_MEDIA_PATH_FAILURE'
  if (inboundIncreasing && bound && !playbackFailed) return 'MEDIA_PATH_WORKING'
  return 'INCONCLUSIVE'
}

const appendRtpLines = (lines, peerIndex, peer) => {
  for (const kind of ['audio', 'video']) {
    for (const direction of ['inbound', 'outbound']) {
      const stats = peer?.rtp?.[kind]?.[direction]
      const prefix = `PEER_${peerIndex}_RTP_${kind.toUpperCase()}_${direction.toUpperCase()}`
      lines.push(`${prefix}_PACKETS=${valueOrDash(stats?.packets)}`)
      lines.push(`${prefix}_BYTES=${valueOrDash(stats?.bytes)}`)
      if (Object.prototype.hasOwnProperty.call(stats || {}, 'frames')) lines.push(`${prefix}_FRAMES=${valueOrDash(stats.frames)}`)
      lines.push(`${prefix}_PACKETS_INCREASING=${yesNo(stats?.packetsIncreasing)}`)
      lines.push(`${prefix}_BYTES_INCREASING=${yesNo(stats?.bytesIncreasing)}`)
      if (Object.prototype.hasOwnProperty.call(stats || {}, 'frames')) lines.push(`${prefix}_FRAMES_INCREASING=${yesNo(stats?.framesIncreasing)}`)
    }
  }
}

const appendTrackLines = (lines, prefix, tracks = []) => {
  lines.push(`${prefix}_COUNT=${tracks.length}`)
  tracks.forEach((track, index) => {
    lines.push(`${prefix}_${index + 1}=kind:${valueOrDash(track?.kind)},present:${yesNo(track?.present)},enabled:${yesNo(track?.enabled)},muted:${yesNo(track?.muted)},readyState:${valueOrDash(track?.readyState)}`)
  })
}

export const createSanitizedWebRtcReport = ({ appBuild = 'unknown', debugEnabled = false, callSessionPresent = false, roomPresent = false, participantCount = 0, peerSnapshots = [], iceServer = getIceServerDiagnostics(), events = getRecentWebRtcDiagnosticEvents(), mediaElements = getMediaElementDiagnostics() } = {}) => {
  const lines = [
    'WEBRTC_RUNTIME_DIAGNOSTICS',
    `APP_BUILD_OR_COMMIT=${valueOrDash(appBuild)}`,
    `DEBUG_ENABLED=${debugEnabled ? 'YES' : 'NO'}`,
    `CALL_SESSION_PRESENT=${callSessionPresent ? 'YES' : 'NO'}`,
    `ROOM_PRESENT=${roomPresent ? 'YES' : 'NO'}`,
    `PARTICIPANT_COUNT=${valueOrDash(participantCount)}`,
    `PEER_COUNT=${peerSnapshots.length}`,
    `ICE_HTTP_STATUS=${valueOrDash(iceServer?.httpStatus)}`,
    `ICE_SERVER_COUNT=${valueOrDash(iceServer?.iceServerCount)}`,
    `STUN_PRESENT=${yesNo(iceServer?.stunPresent)}`,
    `TURN_PRESENT=${yesNo(iceServer?.turnPresent)}`,
    `TURN_SERVER_COUNT=${valueOrDash(iceServer?.turnServerCount)}`
  ]
  peerSnapshots.forEach((peer, index) => {
    const peerIndex = index + 1
    const pair = peer?.selectedCandidatePair
    lines.push(`PEER_TARGET=peer-${peerIndex}`)
    lines.push(`PEER_${peerIndex}_CONNECTION_STATE=${valueOrDash(peer?.connectionState)}`)
    lines.push(`PEER_${peerIndex}_ICE_STATE=${valueOrDash(peer?.iceConnectionState)}`)
    lines.push(`PEER_${peerIndex}_SIGNALING_STATE=${valueOrDash(peer?.signalingState)}`)
    lines.push(`PEER_${peerIndex}_PAIR_STATE=${valueOrDash(pair?.state)}`)
    lines.push(`PEER_${peerIndex}_LOCAL_CANDIDATE_TYPE=${valueOrDash(pair?.localCandidateType)}`)
    lines.push(`PEER_${peerIndex}_REMOTE_CANDIDATE_TYPE=${valueOrDash(pair?.remoteCandidateType)}`)
    lines.push(`PEER_${peerIndex}_CANDIDATE_TYPE=${valueOrDash(pair?.localCandidateType)}`)
    lines.push(`PEER_${peerIndex}_CANDIDATE_PROTOCOL=${valueOrDash(pair?.protocol)}`)
    lines.push(`PEER_${peerIndex}_RELAY_PROTOCOL=${valueOrDash(pair?.relayProtocol)}`)
    lines.push(`PEER_${peerIndex}_SENDERS=${peer?.tracks?.senders?.length || 0}`)
    lines.push(`PEER_${peerIndex}_RECEIVERS=${peer?.tracks?.receivers?.length || 0}`)
    appendTrackLines(lines, `PEER_${peerIndex}_SENDER`, peer?.tracks?.senders)
    appendTrackLines(lines, `PEER_${peerIndex}_RECEIVER`, peer?.tracks?.receivers)
    appendRtpLines(lines, peerIndex, peer)
  })
  lines.push(`MEDIA_ELEMENT_COUNT=${mediaElements.length}`)
  mediaElements.forEach((element, index) => {
    const prefix = `MEDIA_ELEMENT_${index + 1}`
    lines.push(`${prefix}_KIND=${valueOrDash(element?.elementKind)}`)
    lines.push(`${prefix}_ROLE=${valueOrDash(element?.mediaRole)}`)
    lines.push(`${prefix}_BOUND=${yesNo(element?.hasSrcObject)}`)
    lines.push(`${prefix}_STREAM_ACTIVE=${yesNo(element?.streamActive)}`)
    lines.push(`${prefix}_AUDIO_TRACKS=${valueOrDash(element?.audioTrackCount)}`)
    lines.push(`${prefix}_VIDEO_TRACKS=${valueOrDash(element?.videoTrackCount)}`)
    lines.push(`${prefix}_AUTOPLAY=${yesNo(element?.autoplay)}`)
    lines.push(`${prefix}_MUTED=${yesNo(element?.muted)}`)
    lines.push(`${prefix}_PAUSED=${yesNo(element?.paused)}`)
    lines.push(`${prefix}_READY_STATE=${valueOrDash(element?.readyState)}`)
    lines.push(`${prefix}_VOLUME=${valueOrDash(element?.volume)}`)
    lines.push(`${prefix}_PLAY_RESULT=${valueOrDash(element?.playResult)}`)
    lines.push(`${prefix}_PLAY_ERROR=${valueOrDash(element?.playErrorName)}`)
    element?.trackStates?.forEach((track, trackIndex) => {
      lines.push(`${prefix}_TRACK_${trackIndex + 1}=kind:${valueOrDash(track?.kind)},enabled:${yesNo(track?.enabled)},muted:${yesNo(track?.muted)},readyState:${valueOrDash(track?.readyState)}`)
    })
  })
  lines.push(`RECENT_EVENT_COUNT=${events.length}`)
  events.slice(-20).forEach((event, index) => lines.push(`RECENT_EVENT_${index + 1}=${valueOrDash(event?.event)}`))
  lines.push(`CLASSIFICATION=${classifyReport({ peerSnapshots, events, mediaElements })}`)
  return lines.join('\n')
}

export const copyTextToClipboard = async (value, navigatorLike = globalThis.navigator) => {
  try {
    if (!navigatorLike?.clipboard?.writeText) return false
    await navigatorLike.clipboard.writeText(value)
    return true
  } catch {
    return false
  }
}

export const resetWebRtcRuntimeDiagnostics = () => {
  recentEvents = []
  mediaElementDiagnostics.clear()
  iceServerDiagnostics = {
    httpStatus: null,
    iceServerCount: 0,
    stunPresent: false,
    turnPresent: false,
    turnServerCount: 0
  }
}
