export const participantIdentityKey = participant =>
  `${participant?.userId || participant?.connectionId || ''}`.trim().toLowerCase()

export const dedupeParticipantsByUser = (participants = [], preferredConnectionId = '') => {
  const unique = new Map()
  for (const participant of participants) {
    const key = participantIdentityKey(participant)
    if (!key) continue
    const previous = unique.get(key)
    if (!previous || participant.connectionId === preferredConnectionId || previous.connectionId !== preferredConnectionId) {
      unique.set(key, participant)
    }
  }
  return [...unique.values()]
}

export const getMeetingLayoutMode = ({
  hasPresenter,
  presentationFocused,
  focusedParticipantId,
  participantCount
}) => {
  if (hasPresenter) return presentationFocused ? 'PRESENTATION_FOCUS' : 'PRESENTATION'
  if (focusedParticipantId && participantCount > 0) return 'CAMERA_FOCUS'
  return 'CAMERA_GRID'
}

export const getMeetingVisualRegions = mode => mode.startsWith('PRESENTATION')
  ? ['presentation-stage', 'participant-rail']
  : mode === 'CAMERA_FOCUS'
    ? ['camera-stage', 'participant-rail']
    : ['camera-stage']

export const getMeetingRenderCollections = ({
  mode,
  participantsInCall = [],
  focusedParticipantId = ''
}) => {
  const uniqueParticipantsInCall = dedupeParticipantsByUser(participantsInCall)
  const focusedParticipant = uniqueParticipantsInCall.find(participant =>
    participant.connectionId === focusedParticipantId
  )

  if (mode === 'CAMERA_FOCUS' && focusedParticipant) {
    return {
      cameraStageParticipants: [focusedParticipant],
      cameraRailParticipants: uniqueParticipantsInCall.filter(participant =>
        participant.connectionId !== focusedParticipant.connectionId
      ),
      presentationRailParticipants: []
    }
  }

  if (mode.startsWith('PRESENTATION')) {
    return {
      cameraStageParticipants: [],
      cameraRailParticipants: [],
      presentationRailParticipants: uniqueParticipantsInCall
    }
  }

  return {
    cameraStageParticipants: uniqueParticipantsInCall,
    cameraRailParticipants: [],
    presentationRailParticipants: []
  }
}
