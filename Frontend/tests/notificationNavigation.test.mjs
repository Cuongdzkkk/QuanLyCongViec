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

test('supported admin billing notification navigates once to its existing route', async () => {
  const pushed = []
  const router = {
    resolve: target => ({ matched: [{ path: target.split('?')[0] }] }),
    push: target => pushed.push(target)
  }

  const result = await navigateNotification(router, {
    notificationType: 'BILLING_PAYMENT_SUCCEEDED_ADMIN',
    linkUrl: '/admin/billing/payments?order=order-1'
  })

  assert.equal(result.reason, 'navigated')
  assert.deepEqual(pushed, ['/admin/billing/payments?order=order-1'])
})

test('project notification fetches the related ID before navigating', async () => {
  const fetched = []
  const pushed = []
  const router = {
    resolve: () => ({ matched: [{ path: '/space/alpha-project/project-1/work-items' }] }),
    push: target => pushed.push(target)
  }

  await navigateNotification(router, {
    notificationType: 'TASK_ASSIGNED',
    relatedProjectId: 'project-1',
    relatedTaskId: 'task-1'
  }, {
    fetchProject: async id => {
      fetched.push(id)
      return { id, name: 'Alpha Project' }
    }
  })

  assert.deepEqual(fetched, ['project-1'])
  assert.deepEqual(pushed, ['/space/alpha-project/project-1/work-items?task=task-1'])
})

test('missing application route stays in notification context without pushing', async () => {
  const pushed = []
  const invalid = []
  const router = {
    resolve: () => ({ matched: [] }),
    push: target => pushed.push(target)
  }

  const result = await navigateNotification(router, {
    notificationType: 'BILLING_PAYMENT_SUCCEEDED',
    linkUrl: '/billing?order=order-1'
  }, { onInvalid: () => invalid.push(true) })

  assert.equal(result.reason, 'invalid-target')
  assert.deepEqual(invalid, [true])
  assert.deepEqual(pushed, [])
})
