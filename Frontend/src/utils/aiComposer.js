export const AI_COMPOSER_MIN_HEIGHT = 52
export const AI_COMPOSER_MAX_HEIGHT = 184

export const measureAiComposerHeight = (scrollHeight, maxHeight = AI_COMPOSER_MAX_HEIGHT) => {
  const safeScrollHeight = Number.isFinite(Number(scrollHeight)) ? Number(scrollHeight) : AI_COMPOSER_MIN_HEIGHT
  const safeMaxHeight = Math.max(AI_COMPOSER_MIN_HEIGHT, Number(maxHeight) || AI_COMPOSER_MAX_HEIGHT)
  const height = Math.max(AI_COMPOSER_MIN_HEIGHT, Math.min(safeScrollHeight, safeMaxHeight))
  return { height, overflowY: safeScrollHeight > safeMaxHeight ? 'auto' : 'hidden' }
}
