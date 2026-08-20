export const STICKY_LAUNCHER_STORAGE_PREFIX = 'sprinta:stickies-launcher-position:'
export const STICKY_LAUNCHER_DRAG_THRESHOLD = 6
export const STICKY_LAUNCHER_MARGIN = 12

export const getStickyLauncherStorageKey = (accountId = '') =>
  `${STICKY_LAUNCHER_STORAGE_PREFIX}${accountId || 'anonymous'}`

export const clampStickyLauncherY = (
  y,
  viewportHeight,
  launcherHeight,
  topInset = 12,
  bottomMargin = STICKY_LAUNCHER_MARGIN
) => {
  const minY = Math.max(0, Number(topInset) || 0)
  const maxY = Math.max(minY, (Number(viewportHeight) || 0) - (Number(launcherHeight) || 0) - bottomMargin)
  return Math.round(Math.min(Math.max(Number(y) || 0, minY), maxY))
}

export const hasStickyLauncherDragged = (startX, startY, currentX, currentY, threshold = STICKY_LAUNCHER_DRAG_THRESHOLD) =>
  Math.hypot((Number(currentX) || 0) - (Number(startX) || 0), (Number(currentY) || 0) - (Number(startY) || 0)) >= threshold

export const getStickyLauncherDragY = (originY, deltaY, viewportHeight, launcherHeight, topInset = 12) =>
  clampStickyLauncherY((Number(originY) || 0) + (Number(deltaY) || 0), viewportHeight, launcherHeight, topInset)

export const readStickyLauncherY = (storage, accountId, viewportHeight, launcherHeight, topInset = 12) => {
  try {
    const raw = storage?.getItem(getStickyLauncherStorageKey(accountId))
    return raw == null ? null : clampStickyLauncherY(raw, viewportHeight, launcherHeight, topInset)
  } catch {
    return null
  }
}

export const writeStickyLauncherY = (storage, accountId, y) => {
  try {
    storage?.setItem(getStickyLauncherStorageKey(accountId), `${Math.round(Number(y) || 0)}`)
  } catch {
    // Preferences are optional; launcher behavior must continue if storage is unavailable.
  }
}
