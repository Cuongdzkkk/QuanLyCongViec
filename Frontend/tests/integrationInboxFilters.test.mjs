import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const integrationHub = await readFile(new URL('../src/views/IntegrationHubView.vue', import.meta.url), 'utf8')

test('Unified Inbox hides only unadvertised GitHub and Zalo filters', () => {
  const tabsBlock = integrationHub.match(/const tabs = computed\(\(\) => \[(?<tabs>[\s\S]*?)\]\)/)?.groups?.tabs || ''
  assert.ok(tabsBlock, 'tabs configuration should remain local to Integration Hub')
  for (const id of ['all', 'calendar', 'email', 'slack', 'system']) assert.match(tabsBlock, new RegExp(`id: '${id}'`))
  assert.doesNotMatch(tabsBlock, /id: 'github'/)
  assert.doesNotMatch(tabsBlock, /id: 'zalo'/)
})

test('hidden provider state normalizes to All without changing inbox data or counts', () => {
  assert.match(integrationHub, /HIDDEN_INBOX_FILTER_IDS/)
  assert.match(integrationHub, /activeTab\.value = 'all'/)
  assert.match(integrationHub, /query: \{ \.\.\.route\.query, provider: 'all' \}/)
  assert.match(integrationHub, /let list = inboxItems\.value/)
  assert.match(integrationHub, /const connectedCount = computed\(\(\) => providers\.value\.filter/)
  assert.match(integrationHub, /const renderedProviders = computed\(\(\) => \{/)
  assert.match(integrationHub, /provider: 'github'/)
  assert.match(integrationHub, /provider: 'zalo'/)
  assert.match(integrationHub, /\.tabs \{[\s\S]*flex-wrap: wrap;/)
})
