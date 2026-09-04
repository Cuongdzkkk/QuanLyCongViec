import assert from 'node:assert/strict'
import test from 'node:test'
import {
  isPendingInvitation,
  navigateNotification,
  normalizeNotification,
  resolveNotificationRoute
} from '../src/utils/notificationNavigation.js'

test('pending invitation stays in notification context', () => {
  const notification = {
    notificationType: 'PROJECT_INVITATION',
    actionState: 'Pending',
    relatedInvitationId: 'invite-1',
    relatedProjectId: 'project-1',
    linkUrl: '/space/project-1'
  }

  assert.equal(isPendingInvitation(notification), true)
  assert.equal(resolveNotificationRoute(notification, { id: 'project-1', name: 'Alpha' }), null)
})

test('accessible project notification uses a canonical single-space route', () => {
  const route = resolveNotificationRoute(
    { notificationType: 'TASK_ASSIGNED', relatedProjectId: 'project-1', relatedTaskId: 'task-1' },
    { id: 'project-1', name: 'Alpha Project' }
  )

  assert.equal(route, '/space/alpha-project/project-1/work-items?task=task-1')
  assert.equal(route.includes('/space/space/'), false)
})

test('legacy project links are not trusted blindly', () => {
  assert.equal(normalizeNotification({ linkUrl: '/space/project-1' }).linkUrl, null)
  assert.equal(normalizeNotification({ linkUrl: '/home/notifications' }).linkUrl, '/home/notifications')
})

test('inaccessible project notifications stay in context without pushing a route', async () => {
  const pushed = []
  const denied = []

  const result = await navigateNotification(
    { push: route => pushed.push(route) },
    { notificationType: 'TASK_ASSIGNED', relatedProjectId: 'project-1' },
    {
      fetchProject: async () => { throw { response: { status: 403 } } },
      onDenied: error => denied.push(error.response.status)
    }
  )

  assert.equal(result.handled, true)
  assert.deepEqual(denied, [403])
  assert.deepEqual(pushed, [])
})
