export const LIVE_CAPTION_MAX_ROWS = 3
export const LIVE_CAPTION_EXPIRY_MS = 3500

const read = (value, camel, pascal) => value?.[camel] ?? value?.[pascal]

export const normalizeLiveCaptionEvent = value => ({
  callSessionId: read(value, 'callSessionId', 'CallSessionId') || '',
  speakerUserId: read(value, 'speakerUserId', 'SpeakerUserId') || '',
  speakerDisplayName: read(value, 'speakerDisplayName', 'SpeakerDisplayName') || 'Unknown user',
  startedAt: read(value, 'startedAt', 'StartedAt') || '',
  endedAt: read(value, 'endedAt', 'EndedAt') || '',
  text: `${read(value, 'text', 'Text') || ''}`.trim(),
  confidence: read(value, 'confidence', 'Confidence') ?? null,
  id: read(value, 'id', 'Id') || ''
})

export const isLiveCaptionForSession = (value, currentSessionId) => {
  const eventSessionId = normalizeLiveCaptionEvent(value).callSessionId
  return Boolean(currentSessionId) && (!eventSessionId || `${eventSessionId}`.toLowerCase() === `${currentSessionId}`.toLowerCase())
}

export const getCaptionSpeakerKey = caption =>
  `${caption?.speakerUserId || caption?.speakerDisplayName || 'unknown'}`.trim().toLowerCase()

const limitRows = rows => rows.slice(-LIVE_CAPTION_MAX_ROWS)

const createRow = (caption, { isInterim, now }) => ({
  ...caption,
  id: caption.id || `${isInterim ? 'interim' : 'final'}:${getCaptionSpeakerKey(caption)}:${caption.startedAt || now}`,
  speakerKey: getCaptionSpeakerKey(caption),
  isInterim,
  expiresAt: isInterim ? null : now + LIVE_CAPTION_EXPIRY_MS,
  updatedAt: now
})

export const upsertLiveCaptionInterim = (rows, value, now = Date.now()) => {
  const caption = normalizeLiveCaptionEvent(value)
  const speakerKey = getCaptionSpeakerKey(caption)
  const existingIndex = rows.findIndex(row => row.isInterim && row.speakerKey === speakerKey)
  if (!caption.text) return existingIndex < 0 ? rows : rows.filter((_, index) => index !== existingIndex)

  const nextRow = createRow(caption, { isInterim: true, now })
  if (existingIndex < 0) return limitRows([...rows, nextRow])

  const next = [...rows]
  next[existingIndex] = { ...next[existingIndex], ...nextRow, id: next[existingIndex].id }
  return next
}

export const upsertLiveCaptionFinal = (rows, value, now = Date.now()) => {
  const caption = normalizeLiveCaptionEvent(value)
  if (!caption.text) return rows

  const speakerKey = getCaptionSpeakerKey(caption)
  const interimIndex = rows.findIndex(row => row.isInterim && row.speakerKey === speakerKey)
  const duplicateIndex = caption.id ? rows.findIndex(row => row.id === caption.id) : -1
  const nextRow = createRow(caption, { isInterim: false, now })
  const next = rows.filter((_, index) => index !== duplicateIndex && index !== interimIndex)

  if (interimIndex >= 0) {
    const insertAt = Math.min(interimIndex, next.length)
    next.splice(insertAt, 0, { ...nextRow, id: caption.id || rows[interimIndex].id })
  } else {
    next.push(nextRow)
  }
  return limitRows(next)
}

export const removeExpiredLiveCaptions = (rows, now = Date.now()) =>
  rows.filter(row => row.isInterim || !row.expiresAt || row.expiresAt > now)

export const clearLiveCaptions = () => []

export const normalizeTranscriptChunkEvent = value => ({
  id: read(value, 'id', 'Id') || '',
  callSessionId: read(value, 'callSessionId', 'CallSessionId') || '',
  speakerUserId: read(value, 'speakerUserId', 'SpeakerUserId') || '',
  startedAt: read(value, 'startedAt', 'StartedAt') || '',
  speakerDisplayName: read(value, 'speakerDisplayName', 'SpeakerDisplayName') || 'Unknown user',
  text: `${read(value, 'text', 'Text') || ''}`.trim()
})

export const upsertTranscriptHistory = (rows, value) => {
  const chunk = normalizeTranscriptChunkEvent(value)
  if (!chunk.id || !chunk.text) return rows
  return [...rows.filter(item => item.id !== chunk.id), chunk]
    .sort((left, right) => Date.parse(left.startedAt) - Date.parse(right.startedAt))
}

export const upsertTranscriptInterim = (rows, value) => {
  const chunk = normalizeTranscriptChunkEvent(value)
  const speakerKey = getCaptionSpeakerKey(chunk)
  const next = rows.filter(item => !(item.isInterim && item.speakerKey === speakerKey))
  if (!chunk.text) return next
  return [...next, { ...chunk, id: `interim:${speakerKey}`, speakerKey, isInterim: true }]
}

export const removeTranscriptInterim = (rows, value) => {
  const speakerKey = getCaptionSpeakerKey(normalizeTranscriptChunkEvent(value))
  return rows.filter(item => !(item.isInterim && item.speakerKey === speakerKey))
}
