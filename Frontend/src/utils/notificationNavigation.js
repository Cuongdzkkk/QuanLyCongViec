import { buildSpacePath } from './spaceRoute.js'

export const isPendingInvitation = (notification) =>
  notification?.notificationType?.toUpperCase() === 'PROJECT_INVITATION' &&
  notification?.actionState?.toLowerCase() === 'pending' &&
  Boolean(notification?.relatedInvitationId)

export const isResolvedInvitation = (notification) =>
  notification?.notificationType?.toUpperCase() === 'PROJECT_INVITATION' &&
  ['accepted', 'declined'].includes(notification?.actionState?.toLowerCase())

const isSafeInternalPath = (value) => {
  if (typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//')) return false
  return !value.startsWith('/space/')
}

export const normalizeNotification = (notification) => ({
  ...notification,
  linkUrl: isSafeInternalPath(notification?.linkUrl) ? notification.linkUrl : null
})

export const resolveNotificationRoute = (notification, project) => {
  if (isPendingInvitation(notification) || notification?.actionState?.toLowerCase() === 'declined') return null

  if (notification?.relatedProjectId) {
    if (!project?.id) return null
    const route = buildSpacePath({
      id: project.id,
      name: project.name || project.key || ''
    }, 'work-items')
    const taskId = notification.relatedTaskId
    return taskId ? `${route}?task=${encodeURIComponent(taskId)}` : route
  }

  return isSafeInternalPath(notification?.linkUrl) ? notification.linkUrl : null
}

export const navigateNotification = async (router, notification, {
  fetchProject,
  onDenied,
  onInvalid
} = {}) => {
  if (isPendingInvitation(notification)) return { handled: true, reason: 'pending-invitation' }
  if (notification?.actionState?.toLowerCase() === 'declined') return { handled: true, reason: 'declined' }

  let project = null
  if (notification?.relatedProjectId) {
    try {
      project = await fetchProject?.(notification.relatedProjectId)
    } catch (error) {
      const status = Number(error?.response?.status || 0)
      if (status === 401 || status === 403 || status === 404) {
        onDenied?.(error)
        return { handled: true, reason: 'inaccessible-project', status }
      }
      throw error
    }
  }

  const target = resolveNotificationRoute(notification, project)
  if (!target) {
    onInvalid?.()
    return { handled: true, reason: 'invalid-target' }
  }

  if (typeof router?.resolve === 'function' && !router.resolve(target)?.matched?.length) {
    onInvalid?.()
    return { handled: true, reason: 'invalid-target', target }
  }

  await router.push(target)
  return { handled: true, reason: 'navigated', target }
}
