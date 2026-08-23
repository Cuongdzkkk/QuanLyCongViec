export const participantIdentityKey = participant =>
  `${participant?.userId || participant?.connectionId || ''}`.trim().toLowerCase()

export const dedupeParticipantsByUser = (participants = [], preferredConnectionId = '') => {
  const unique = new Map()
  for (const participant of participants) {
    const key = participantIdentityKey(participant)
    if (!key) continue
    const previous = unique.get(key)
    if (!previous || participant.connectionId === preferredConnectionId) unique.set(key, participant)
  }
  return [...unique.values()]
}

export const getMeetingLayoutMode = ({
  hasPresenter,
  presentationFocused,
  focusedParticipantId,
  visibleParticipantCount
}) => {
  if (hasPresenter) return presentationFocused ? 'PRESENTATION_FOCUS' : 'PRESENTATION'
  if (focusedParticipantId && visibleParticipantCount > 1) return 'CAMERA_FOCUS'
  return visibleParticipantCount === 1 ? 'CAMERA_FOCUS' : 'CAMERA_GRID'
}

export const getMeetingVisualRegions = mode => mode.startsWith('PRESENTATION')
  ? ['presentation-stage', 'participant-rail']
  : ['camera-stage']
