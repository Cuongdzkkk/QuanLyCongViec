import assert from 'node:assert/strict'
import test from 'node:test'
import {
  findPendingAiAction,
  getQuickAiCapabilities,
  isAiConfirmationMessage,
  normalizeAiCapability
} from '../src/utils/aiCapabilityRegistry.js'

test('canonical capability response becomes a real quick tool', () => {
  const capability = normalizeAiCapability({
    actionKey: 'task.create',
    legacyType: 'create_task',
    displayName: 'Tạo công việc',
    capabilityKind: 'Write',
    executor: 'AiController.ExecuteCreateTaskAsync',
    quickTool: true,
    quickPrompt: 'Tạo task mới trong project hiện tại.',
    icon: 'fa-solid fa-square-plus'
  })

  assert.equal(capability.type, 'create_task')
  assert.equal(capability.mode, 'write')
  assert.equal(getQuickAiCapabilities([capability]).length, 1)
})

test('short confirmation messages resume only a pending write action', () => {
  assert.equal(isAiConfirmationMessage('ok'), true)
  assert.equal(isAiConfirmationMessage('  LÀM ĐI! '), true)
  assert.equal(isAiConfirmationMessage('ok, tạo thêm'), false)
  const action = { type: 'create_task', uiStatus: 'pending', requiresConfirmation: true }
  assert.equal(findPendingAiAction([{ actions: [action] }]), action)
  assert.equal(findPendingAiAction([{ actions: [{ ...action, uiStatus: 'success' }] }]), null)
})
