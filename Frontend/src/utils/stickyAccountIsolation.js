export const getStickyAccountId = user => `${user?.id || user?.Id || ''}`

export const hasStickyAccountChanged = (previousAccountId, nextAccountId) =>
  `${previousAccountId || ''}` !== `${nextAccountId || ''}`

export const createStickyRequestEpoch = () => {
  let epoch = 0

  return {
    current: () => epoch,
    invalidate: () => ++epoch,
    capture: () => epoch,
    isCurrent: requestEpoch => requestEpoch === epoch
  }
}
