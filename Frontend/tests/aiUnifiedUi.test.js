import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'

const root = new URL('../src/', import.meta.url)
const aiPage = fs.readFileSync(new URL('views/AIPage.vue', root), 'utf8')
const nexusLayout = fs.readFileSync(new URL('components/layout/NexusLayout.vue', root), 'utf8')
const composer = fs.readFileSync(new URL('components/ai/AiComposer.vue', root), 'utf8')
const message = fs.readFileSync(new URL('components/ai/AiMessage.vue', root), 'utf8')
const creditsModal = fs.readFileSync(new URL('components/ai/AiCreditsPurchaseModal.vue', root), 'utf8')
const actionUi = fs.readFileSync(new URL('utils/aiActionUi.js', root), 'utf8')

test('panel and full chat use the same composer and message components', () => {
  assert.match(aiPage, /import AiComposer from ['"]@\/components\/ai\/AiComposer\.vue['"]/)
  assert.match(aiPage, /import AiMessage from ['"]@\/components\/ai\/AiMessage\.vue['"]/)
  assert.match(nexusLayout, /import AiComposer from ['"]@\/components\/ai\/AiComposer\.vue['"]/)
  assert.match(nexusLayout, /import AiMessage from ['"]@\/components\/ai\/AiMessage\.vue['"]/)
})

test('AI composer focus uses the exposed component ref contract', () => {
  assert.match(composer, /ref="textareaInput"/)
  assert.match(composer, /focusInput: \(\) => textareaInput\.value\?\.focus\(\)/)
  assert.match(nexusLayout, /ref="aiComposerRef"/)
  assert.match(nexusLayout, /aiComposerRef\.value\?\.focusInput\?\.\(\)/)
  assert.match(aiPage, /ref="aiComposerRef"/)
  assert.match(aiPage, /aiComposerRef\.value\?\.focusInput\?\.\(\)/)
  assert.doesNotMatch(nexusLayout, /querySelector\([^\n]*textarea/)
  assert.doesNotMatch(aiPage, /querySelector\([^\n]*textarea/)
})

test('full composer exposes the same attachment, screenshot, voice, and send controls', () => {
  for (const contract of ['attachment-command', 'screenshot', 'start-voice', 'use-transcript', 'drop', 'send']) {
    assert.match(composer, new RegExp(contract))
  }
  assert.match(aiPage, /@attachment-command="handleAttachmentCommand"/)
  assert.match(aiPage, /@start-voice="startVoiceRecording"/)
  assert.match(aiPage, /@send="sendMessage"/)
  assert.match(aiPage, /\/ai\/attachment-chat/)
})

test('both surfaces use the sanitized markdown renderer and explicit action events', () => {
  assert.match(message, /DOMPurify\.sanitize/)
  assert.match(message, /execute-action/)
  assert.match(message, /cancel-action/)
  assert.match(message, /retry-action/)
  assert.match(aiPage, /@execute-action="confirmPageAction"/)
  assert.match(nexusLayout, /@execute-action="executeAiAction"/)
})

test('floating AI has no local fake task-creation path', () => {
  assert.doesNotMatch(nexusLayout, /createRealTasks|tryHandleLocalAiCommand|sprinta-ai-task-created|Đã tạo task thật/)
})

test('shared AI UI styles use semantic theme tokens for state colors', () => {
  assert.match(composer, /var\(--color-on-accent/)
  assert.match(message, /var\(--color-on-accent/)
  assert.match(composer, /var\(--color-on-accent/)
  assert.match(message, /var\(--color-text-primary\)/)
})

test('credit management uses authenticated billing data and the existing checkout flow', () => {
  assert.match(nexusLayout, /AiCreditsPurchaseModal/)
  assert.match(aiPage, /AiCreditsPurchaseModal/)
  assert.match(nexusLayout, /aiCreditsModalVisible\.value = true/)
  assert.match(aiPage, /aiCreditsModalVisible\.value = true/)
  assert.doesNotMatch(nexusLayout, /router\.push\(['"]\/#pricing['"]\)/)
  assert.doesNotMatch(aiPage, /router\.push\(['"]\/#pricing['"]\)/)
  assert.match(creditsModal, /<AppModal/)
  assert.match(creditsModal, /billingApi\.getMe\(\)/)
  assert.match(creditsModal, /billingApi\.getMyHistory\(/)
  assert.match(creditsModal, /axiosClient\.get\(['"]\/public\/pricing['"]\)/)
  assert.doesNotMatch(creditsModal, /billingApi\.createOrder\(plan\.code\)/)
  assert.match(creditsModal, /router\.push\(buildBillingCheckoutLocation\(getPlanCode\(plan\), '', route\.fullPath\)\)/)
  assert.match(creditsModal, /Mở trang gói &(?:amp;|&) thanh toán/)
  assert.match(creditsModal, /Mở trang thanh toán &(?:amp;|&) lịch sử/)
  assert.doesNotMatch(creditsModal, /Mở billing/)
  assert.doesNotMatch(creditsModal, /Xem đầy đủ trong billing/)
  assert.match(creditsModal, /extra credit/i)
})

test('quick tools are shared from one catalog with safe read and write intents', () => {
  assert.match(actionUi, /export const AI_QUICK_ACTIONS = \[/)
  assert.match(actionUi, /summarize_project/)
  assert.match(actionUi, /get_workload/)
  assert.match(actionUi, /list_overdue_tasks/)
  for (const type of ['create_task', 'create_cycle', 'update_task_status', 'assign_task', 'add_comment']) {
    assert.match(actionUi, new RegExp(`type: ['"]${type}['"]`))
    assert.match(actionUi, new RegExp(`type: ['"]${type}['"][^\\n]*mode: ['"]write['"]`))
  }
  assert.match(nexusLayout, /AI_QUICK_ACTIONS/)
  assert.match(aiPage, /AI_QUICK_ACTIONS/)
  assert.match(aiPage, /quickActions\.slice\(0, 4\)/)
  assert.match(aiPage, /quickActions\.slice\(4\)/)
})

test('AI identity uses the canonical auth session source', () => {
  assert.match(aiPage, /getStoredUserSession/)
  assert.doesNotMatch(aiPage, /JSON\.parse\(localStorage\.getItem\(['"]user['"]\)/)
  assert.match(aiPage, /currentUser\.value/)
})

test('AI page exposes the existing workspace switch contract', () => {
  assert.match(aiPage, /useSiteStore/)
  assert.match(aiPage, /siteStore\.fetchSites\(\)/)
  assert.match(aiPage, /siteStore\.setRecentSite\(/)
  assert.match(aiPage, /sprinta-workspace-changed/)
  assert.match(aiPage, /aria-label="Chọn workspace"/)
})

test('AI product surfaces keep the visual hierarchy and theme contract', () => {
  assert.match(aiPage, /CÔNG CỤ NHANH/)
  assert.match(aiPage, /NGỮ CẢNH HIỆN TẠI/)
  assert.match(aiPage, /Quản lý gói/)
  assert.match(aiPage, /\.ai-details-panel[\s\S]*var\(--color-surface\)/)
  assert.match(nexusLayout, /is-ai-open/)
  assert.match(nexusLayout, /--ai-sidebar-width/)
  assert.match(nexusLayout, /global-utility-rail\.is-ai-open/)
  assert.match(composer, /\.ai-composer:focus-within/)
  assert.match(message, /\.ai-message-bubble[\s\S]*color-mix/)
  assert.match(creditsModal, /is-recommended/)
  assert.match(creditsModal, /dark-first product surface/)
  for (const source of [aiPage, nexusLayout, composer, message, creditsModal]) {
    assert.doesNotMatch(source, /background:\s*#fff/i)
    assert.doesNotMatch(source, /color:\s*#000/i)
  }
})
