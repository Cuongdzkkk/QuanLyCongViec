import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const view = fs.readFileSync(path.join(import.meta.dirname, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')

test('collaboration navigator uses real project and channel state', () => {
  assert.match(view, /projectStore\.sidebarProjects/)
  assert.match(view, /collaborationApi\.getProjectChannels/)
  assert.match(view, /v-for="project in filteredProjects"/)
  assert.doesNotMatch(view, /<nav class="server-bar"/)
  assert.match(view, /class="navigator-search"/)
  assert.match(view, /placeholder="Tìm dự án hoặc kênh"/)
})

test('accordion state is single-project and active project follows navigation', () => {
  assert.match(view, /const expandedProjectId = ref\(''\)/)
  assert.match(view, /const toggleProject = \(projectId\) =>/)
  assert.match(view, /expandedProjectId\.value === projectId/)
  assert.match(view, /:aria-expanded="expandedProjectId === project.id"/)
  assert.match(view, /expandedProjectId\.value = activeProjectId\.value/)
  assert.match(view, /watch\(\s*\[activeProjectId,\s*\(\) => activeChat\.value\?\.id/)
  assert.match(view, /watch\(navigatorQuery, \(query, previousQuery\) =>/)
  assert.match(view, /expandedProjectBeforeSearch\.value = expandedProjectId\.value/)
})

test('text, voice, direct-message, unread, and create actions stay wired', () => {
  for (const needle of [
    "@click=\"selectChat(ch, 'channel')\"",
    '@click="openPreJoinVoiceChannel(vc)"',
    "@click=\"selectChat(conversation, 'dm')\"",
    'collaboration-unread-badge',
    '@click="openCreateChannelModal"',
    '@click="openCreateVoiceModal"',
    'directConversations.length'
  ]) assert.ok(view.includes(needle), `missing ${needle}`)
})

test('search filters projects and reveals matching channels without a new API', () => {
  assert.match(view, /const filteredProjects = computed\(\(\) => projectOptions\.value\.filter\(projectHasNavigatorMatch\)\)/)
  assert.match(view, /normalizeNavigatorText\(channel\.name\)/)
  assert.match(view, /const channelsForNavigator = \(project\) =>/)
  assert.match(view, /Không tìm thấy dự án hoặc kênh phù hợp\./)
  assert.doesNotMatch(view, /getProjectChannels\([^)]*navigatorQuery/)
})

test('navigator is a single-column responsive surface with theme tokens', () => {
  assert.match(view, /grid-template-columns: minmax\(248px, 276px\) minmax\(0, 1fr\)/)
  assert.match(view, /@media \(max-width: 760px\) \{[\s\S]*?grid-template-columns: minmax\(0, 1fr\)/)
  assert.match(view, /\.navigator-search[\s\S]*?var\(--chat-surface-2\)/)
  assert.match(view, /\.project-row[\s\S]*?var\(--chat-muted\)/)
  assert.match(view, /\.project-accordion\.is-expanded[\s\S]*?var\(--chat-accent-soft\)/)
})

console.log('chatProjectAccordionNavigation.test.js: focused navigation checks passed')
