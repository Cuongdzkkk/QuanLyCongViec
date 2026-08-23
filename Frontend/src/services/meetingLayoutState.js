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
  if (focusedParticipantId && visibleParticipantCount > 0) return 'CAMERA_FOCUS'
  return 'CAMERA_GRID'
}

export const getMeetingVisualRegions = mode => mode.startsWith('PRESENTATION')
  ? ['presentation-stage', 'participant-rail']
  : mode === 'CAMERA_FOCUS'
    ? ['camera-stage', 'participant-rail']
    : ['camera-stage']

export const getMeetingRenderCollections = ({
  mode,
  visibleParticipants = [],
  allParticipants = [],
  focusedParticipantId = ''
}) => {
  const uniqueVisibleParticipants = dedupeParticipantsByUser(visibleParticipants)
  const uniqueAllParticipants = dedupeParticipantsByUser(allParticipants)
  const focusedParticipant = uniqueVisibleParticipants.find(participant =>
    participant.connectionId === focusedParticipantId
  )

  if (mode === 'CAMERA_FOCUS' && focusedParticipant) {
    return {
      cameraStageParticipants: [focusedParticipant],
      cameraRailParticipants: uniqueVisibleParticipants.filter(participant =>
        participant.connectionId !== focusedParticipant.connectionId
      ),
      presentationRailParticipants: []
    }
  }

  if (mode.startsWith('PRESENTATION')) {
    return {
      cameraStageParticipants: [],
      cameraRailParticipants: [],
      presentationRailParticipants: uniqueAllParticipants
    }
  }

  return {
    cameraStageParticipants: uniqueVisibleParticipants,
    cameraRailParticipants: [],
    presentationRailParticipants: []
  }
}
