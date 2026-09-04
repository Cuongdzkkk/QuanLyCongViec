import assert from 'node:assert/strict'
import fs from 'node:fs'
import test from 'node:test'
import { AI_QUICK_ACTIONS } from '../src/utils/aiActionUi.js'
import { buildAiContextKey, isAiContextMatch } from '../src/utils/aiWorkspace.js'
import { buildBillingCheckoutLocation, resolveBillingPlanFlow } from '../src/utils/billingPlanFlow.js'
import { AI_COMPOSER_MAX_HEIGHT, AI_COMPOSER_MIN_HEIGHT, measureAiComposerHeight } from '../src/utils/aiComposer.js'

const sourceRoot = new URL('../src/', import.meta.url)
const aiPage = fs.readFileSync(new URL('views/AIPage.vue', sourceRoot), 'utf8')
const nexusLayout = fs.readFileSync(new URL('components/layout/NexusLayout.vue', sourceRoot), 'utf8')
const message = fs.readFileSync(new URL('components/ai/AiMessage.vue', sourceRoot), 'utf8')
const composer = fs.readFileSync(new URL('components/ai/AiComposer.vue', sourceRoot), 'utf8')
const billingModal = fs.readFileSync(new URL('components/ai/AiCreditsPurchaseModal.vue', sourceRoot), 'utf8')

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

test('billing plan CTAs use the exact existing checkout or activation flow', () => {
  const plans = [
    { code: 'free', monthlyPriceVnd: 0 },
    { code: 'starter', monthlyPriceVnd: 99000 },
    { code: 'plus', monthlyPriceVnd: 199000 },
    { code: 'pro', monthlyPriceVnd: 399000 },
    { code: 'team', monthlyPriceVnd: 799000 },
    { code: 'enterprise', monthlyPriceVnd: null }
  ]
  assert.equal(resolveBillingPlanFlow(plans[0], 'pro'), 'free')
  assert.equal(resolveBillingPlanFlow(plans[1], 'pro'), 'paid')
  assert.equal(resolveBillingPlanFlow(plans[2], 'pro'), 'paid')
  assert.equal(resolveBillingPlanFlow(plans[3], 'pro'), 'current')
  assert.equal(resolveBillingPlanFlow(plans[4], 'pro'), 'paid')
  assert.equal(resolveBillingPlanFlow(plans[5], 'pro'), 'enterprise')
  assert.deepEqual(buildBillingCheckoutLocation('Plus'), {
    name: 'BillingCheckout',
    params: { planCode: 'plus' }
  })
  assert.deepEqual(buildBillingCheckoutLocation('Plus', '', '/ai-assistant?conversation=42'), {
    name: 'BillingCheckout',
    params: { planCode: 'plus' },
    query: { returnTo: '/ai-assistant?conversation=42' }
  })
  assert.match(billingModal, /@click\.stop="selectPlan\(plan\)"/)
  assert.match(billingModal, /billingApi\.activateFree\(\)/)
  assert.equal(resolveBillingPlanFlow({ id: 'pro', name: 'Pro', monthlyPriceVnd: 399000 }, 'plus'), 'paid')
  assert.match(billingModal, /const getPlanCode = plan => String\(plan\?\.code \|\| plan\?\.id/)
  assert.match(billingModal, /router\.push\(buildBillingCheckoutLocation\(getPlanCode\(plan\), '', route\.fullPath\)\)/)
  assert.match(billingModal, /router\.push\(buildBillingCheckoutLocation\([\s\S]*route\.fullPath[\s\S]*\)\)/)
  assert.doesNotMatch(billingModal, /billingApi\.createOrder\(plan\.code\)/)
})

test('shared composer grows from a stable minimum and scrolls after its maximum', () => {
  assert.equal(AI_COMPOSER_MIN_HEIGHT, 52)
  assert.equal(AI_COMPOSER_MAX_HEIGHT, 184)
  assert.deepEqual(measureAiComposerHeight(24), { height: 52, overflowY: 'hidden' })
  assert.deepEqual(measureAiComposerHeight(120), { height: 120, overflowY: 'hidden' })
  assert.deepEqual(measureAiComposerHeight(240), { height: 184, overflowY: 'auto' })
  assert.match(composer, /watch\(\(\) => props\.modelValue,/)
  assert.match(composer, /max-height: 184px;/)
  assert.match(composer, /max-height: 152px;/)
  assert.match(composer, /ai-attachment-tray/)
})

test('AI scope is shared while current page remains a separate display value', () => {
  assert.match(aiPage, /useAiScopeStore\(\)/)
  assert.match(nexusLayout, /useAiScopeStore\(\)/)
  assert.match(aiPage, /PHẠM VI AI/)
  assert.match(aiPage, /TRANG HIỆN TẠI/)
  assert.match(nexusLayout, /ai-context-eyebrow/)
  assert.match(nexusLayout, /ai-page-context/)
  assert.match(nexusLayout, /currentWorkspaceLabel/)
  assert.match(nexusLayout, /if \(aiScopeStore\.workspaceId\) return aiScopeStore\.workspaceId/)
  assert.match(nexusLayout, /if \(aiScopeStore\.projectId\)/)
  assert.match(aiPage, /projectId: currentProjectId\.value \|\| null/)
})

test('responsive AI surfaces contain their expanding columns and panel scroll regions', () => {
  assert.match(aiPage, /grid-template-columns: minmax\(0, 1fr\) auto minmax\(140px, \.55fr\)/)
  assert.match(aiPage, /\.ai-details-panel \{ overflow-x: hidden; \}/)
  assert.match(aiPage, /@media \(max-width: 1100px\)[\s\S]*?\.ai-details-panel \{ display: none; \}/)
  assert.match(nexusLayout, /\.ai-content \{[\s\S]*?overflow-x: hidden;/)
  assert.match(nexusLayout, /@media \(min-width: 761px\) and \(max-width: 1024px\)/)
  assert.match(nexusLayout, /@media \(max-width: 760px\)[\s\S]*?\.ai-sidebar \{[\s\S]*?left: 0;/)
})
