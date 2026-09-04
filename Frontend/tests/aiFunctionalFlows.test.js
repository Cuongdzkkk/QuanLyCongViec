import assert from 'node:assert/strict'
import fs from 'node:fs'
import test from 'node:test'
import { AI_QUICK_ACTIONS } from '../src/utils/aiActionUi.js'
import { buildAiContextKey, isAiContextMatch } from '../src/utils/aiWorkspace.js'

const sourceRoot = new URL('../src/', import.meta.url)
const aiPage = fs.readFileSync(new URL('views/AIPage.vue', sourceRoot), 'utf8')
const nexusLayout = fs.readFileSync(new URL('components/layout/NexusLayout.vue', sourceRoot), 'utf8')
const message = fs.readFileSync(new URL('components/ai/AiMessage.vue', sourceRoot), 'utf8')

test('quick tools retain the complete read/write catalog and run through the submit handlers', () => {
  assert.equal(AI_QUICK_ACTIONS.length, 11)
  assert.equal(AI_QUICK_ACTIONS.filter(action => action.mode === 'read').length, 6)
  assert.equal(AI_QUICK_ACTIONS.filter(action => action.mode === 'write').length, 5)
  assert.match(aiPage, /@click="runQuickPrompt\(action\.prompt\)"/)
  assert.match(nexusLayout, /@click="runQuickPrompt\(prompt\.text\)"/)
  assert.match(aiPage, /const runQuickPrompt = \(prompt\) => \{[\s\S]*void sendMessage\(\)/)
  assert.match(nexusLayout, /const runQuickPrompt = \(prompt\) => \{[\s\S]*void sendAiMessage\(\)/)
})

test('write actions are confirmation-gated and bound to the active context', () => {
  assert.equal(isAiContextMatch(buildAiContextKey('workspace-a', 'project-a'), 'workspace-a', 'project-a'), true)
  assert.equal(isAiContextMatch(buildAiContextKey('workspace-a', 'project-a'), 'workspace-b', 'project-a'), false)
  assert.match(aiPage, /isAiContextMatch\(action\.contextKey, currentWorkspaceId\.value, currentProjectId\.value\)/)
  assert.match(nexusLayout, /isAiContextMatch\(action\.contextKey, currentWorkspaceId\.value, currentProjectId\.value\)/)
  assert.match(nexusLayout, /axiosClient\.post\('\/ai\/actions\/preview'/)
  assert.match(nexusLayout, /axiosClient\.post\(`\/ai\/actions\/\$\{action\.serverActionId\}\/confirm`\)/)
  assert.doesNotMatch(nexusLayout, /workTaskStore\.(createTask|updateTaskStatus)\(/)
})

test('full AI exposes a real project selector and refreshes scoped projects', () => {
  assert.match(aiPage, /aria-label="Chọn project"/)
  assert.match(aiPage, /availableProjects/)
  assert.match(aiPage, /projectStore\.fetchAllProjects\(true\)/)
  assert.match(aiPage, /setScopedCurrentProjectId\(selected\.id\)/)
  assert.match(aiPage, /projectId: currentProjectId\.value \|\| null/)
})

test('context changes invalidate in-flight floating requests and reset the scoped conversation', () => {
  assert.match(nexusLayout, /window\.addEventListener\('sprinta-workspace-changed', handleAiWorkspaceChanged\)/)
  assert.match(nexusLayout, /watch\(\[currentWorkspaceId, currentProjectId\]/)
  assert.match(nexusLayout, /aiContextRevision\.value \+= 1/)
  assert.match(nexusLayout, /requestRevision !== aiContextRevision\.value/)
  assert.match(nexusLayout, /if \(currentConversationId\.value\) startNewConversation\(\)/)
})

test('panel and full page restore image attachment previews without re-uploading', () => {
  assert.match(aiPage, /await hydrateConversationImages\(\)/)
  assert.match(nexusLayout, /await hydrateConversationImages\(\)/)
  assert.match(aiPage, /axiosClient\.get\(attachment\.contentUrl, \{ responseType: 'blob' \}\)/)
  assert.match(nexusLayout, /axiosClient\.get\(attachment\.contentUrl, \{ responseType: 'blob' \}\)/)
  assert.doesNotMatch(aiPage, /axiosClient\.post\('\/ai\/attachments'.*openConversation/)
})

