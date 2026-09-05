import { aiActionPayload, aiActionTitle, isExecutableAiAction, normalizeAiAction } from './aiActionUi.js'

const CONFIRMATION_PHRASES = [
  'ok',
  'ok hãy làm đi',
  'ok lam di',
  'đồng ý',
  'lam di',
  'làm đi',
  'xác nhận',
  'xac nhan'
]

const normalizeText = value => `${value || ''}`
  .trim()
  .toLocaleLowerCase('vi-VN')
  .replace(/[.!?,;:]+$/g, '')

export const isAiConfirmationMessage = message => {
  const normalized = normalizeText(message)
  return CONFIRMATION_PHRASES.includes(normalized)
}

export const decorateAiAction = (action, { contextKey, conversationId, workspaceId, projectId } = {}) => {
  const normalized = normalizeAiAction(action)
  const actionWorkspaceId = normalized.workspaceId ?? normalized.payload?.workspaceId ?? normalized.payload?.workspace_id
  const actionProjectId = normalized.projectId ?? normalized.payload?.projectId ?? normalized.payload?.project_id
  const now = new Date().toISOString()
  return {
    ...normalized,
    actionKey: normalized.type,
    contextKey: contextKey ?? normalized.contextKey,
    conversationId: conversationId ?? normalized.conversationId,
    workspaceId: actionWorkspaceId ?? workspaceId,
    projectId: actionProjectId ?? projectId,
    status: normalized.status || 'AWAITING_CONFIRMATION',
    createdAt: normalized.createdAt || now,
    expiresAt: normalized.expiresAt || new Date(Date.parse(now) + 15 * 60 * 1000).toISOString(),
    uiStatus: normalized.uiStatus || 'pending',
    loading: false,
    error: normalized.error || '',
    result: normalized.result || null
  }
}

export const findPendingAiAction = (messages, context = {}) => {
  for (const message of [...(messages || [])].reverse()) {
    for (const candidate of [...(message?.actions || [])].reverse()) {
      const action = decorateAiAction(candidate)
      const isTerminalStatus = [
        'success',
        'cancelled',
        'executed',
        'failed'
      ].includes(`${action.status || action.uiStatus || ''}`.toLowerCase())
      const isTerminalUiStatus = ['success', 'cancelled', 'error'].includes(`${action.uiStatus || ''}`.toLowerCase())
      const isTerminal = isTerminalStatus || isTerminalUiStatus
      const isPending = !isTerminal && (action.status === 'AWAITING_CONFIRMATION' || action.uiStatus === 'pending')
      const isUnexpired = !action.expiresAt || Date.parse(action.expiresAt) > Date.now()
      const sameWorkspace = !action.workspaceId || !context.workspaceId || `${action.workspaceId}` === `${context.workspaceId}`
      const sameProject = !action.projectId || !context.projectId || `${action.projectId}` === `${context.projectId}`
      const sameContext = !action.contextKey || !context.contextKey || action.contextKey === context.contextKey
      if (isPending && isUnexpired && sameWorkspace && sameProject && sameContext && isExecutableAiAction(action)) {
        Object.assign(candidate, action)
        return candidate
      }
    }
  }
  return null
}

export const previewAndConfirmAiAction = async (action, { workspaceId, projectId, conversationId } = {}) => {
  const { default: axiosClient } = await import('../api/axiosClient.js')
  const normalized = normalizeAiAction(action)
  const payload = { ...aiActionPayload(normalized), ...(conversationId ? { conversationId } : {}) }
  action.payload = payload
  action.title = aiActionTitle(normalized)
  action.idempotencyKey ||= `${normalized.type}-${crypto.randomUUID()}`

  if (!action.serverActionId) {
    const preview = await axiosClient.post('/ai/actions/preview', {
      type: normalized.type,
      idempotencyKey: action.idempotencyKey,
      workspaceId: workspaceId || null,
      projectId: projectId || payload.projectId || null,
      payload
    })
    action.serverActionId = preview.data?.data?.actionId
  }
  if (!action.serverActionId) throw new Error('Không thể tạo action preview.')
  return axiosClient.post(`/ai/actions/${action.serverActionId}/confirm`)
}
