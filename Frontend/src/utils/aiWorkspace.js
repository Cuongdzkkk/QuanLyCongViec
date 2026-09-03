export const AI_PANEL_SIZE_KEY = 'sprinta-ai-panel-size'
export const AI_PANEL_MIN_WIDTH = 360
export const AI_PANEL_DEFAULT_WIDTH = 456
export const AI_PANEL_MAX_WIDTH = 720
export const AI_PANEL_MIN_HEIGHT = 500

export const getAiPanelMaxWidth = (viewportWidth) => Math.max(
  AI_PANEL_MIN_WIDTH,
  Math.min(AI_PANEL_MAX_WIDTH, Math.floor(Number(viewportWidth || 0) * 0.7), Number(viewportWidth || 0) - 32)
)

export const getAiPanelMaxHeight = (viewportHeight, topInset = 68) => Math.max(
  AI_PANEL_MIN_HEIGHT,
  Number(viewportHeight || 0) - Number(topInset || 0) - 32
)

export const clampAiPanelSize = (size = {}, viewport = {}) => {
  const maxWidth = getAiPanelMaxWidth(viewport.width)
  const maxHeight = getAiPanelMaxHeight(viewport.height, viewport.topInset)
  return {
    width: Math.round(Math.min(Math.max(Number(size.width) || AI_PANEL_DEFAULT_WIDTH, AI_PANEL_MIN_WIDTH), maxWidth)),
    height: Math.round(Math.min(Math.max(Number(size.height) || maxHeight, AI_PANEL_MIN_HEIGHT), maxHeight))
  }
}

export const readAiPanelSize = (storage, viewport) => {
  try {
    const saved = JSON.parse(storage?.getItem(AI_PANEL_SIZE_KEY) || 'null')
    return clampAiPanelSize(saved || {}, viewport)
  } catch {
    return clampAiPanelSize({}, viewport)
  }
}

export const writeAiPanelSize = (storage, size) => {
  storage?.setItem(AI_PANEL_SIZE_KEY, JSON.stringify(size))
}

export const isAiPanelResizable = (viewportWidth) => Number(viewportWidth || 0) > 1024

export const isComposerSendKey = (event) => Boolean(
  event?.key === 'Enter' && !event.shiftKey && !event.isComposing
)

export const writeActionsOnly = (actions = [], isReadOnlyAction = () => false) =>
  actions.filter(action => !isReadOnlyAction(action?.type, action?.requiresConfirmation))
