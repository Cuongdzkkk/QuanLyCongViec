import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const frontendRoot = path.resolve(import.meta.dirname, '..')
const read = relativePath => fs.readFileSync(path.join(frontendRoot, relativePath), 'utf8')

const brand = read('src/components/branding/SprintaBrand.vue')
const siteSelection = read('src/views/SiteSelection.vue')
const notifications = read('src/views/HomeSite/Tools/NotificationsView.vue')
const selectorBlockUsesThemeToken = (source, selector) => {
  const escapedSelector = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  return new RegExp(`${escapedSelector}\\s*\\{[\\s\\S]*?var\\(--color-`)
}

test('site selection shell and wordmark use theme tokens', () => {
  assert.match(brand, /\.sprinta-brand--site-selection \.sprinta-brand__name \{[\s\S]*?color: var\(--color-text-primary\);/)

  for (const selector of ['.start-page-wrapper', '.start-header', '.state-box', '.jira-modal', '.jira-input-wrapper', '.site-list-item']) {
    assert.match(siteSelection, selectorBlockUsesThemeToken(siteSelection, selector))
  }

  for (const hardcodedColor of ['#f4f5f7', '#ffffff', '#dfe1e6', '#172b4d', '#5e6c84', '#0052cc', '#00875a']) {
    assert.doesNotMatch(siteSelection, new RegExp(hardcodedColor, 'i'))
  }
})

test('notification content and interaction states use theme tokens', () => {
  for (const selector of ['.notifications-page', '.notification-item', '.notif-avatar', '.notif-link', '.notif-time', '.notif-meta', '.notif-status.unread', '.empty-state']) {
    assert.match(notifications, selectorBlockUsesThemeToken(notifications, selector))
  }

  for (const hardcodedColor of ['#172B4D', '#5E6C84', '#DFE1E6', '#FAFBFC', '#0052CC', '#6B778C']) {
    assert.doesNotMatch(notifications, new RegExp(hardcodedColor, 'i'))
  }
})
