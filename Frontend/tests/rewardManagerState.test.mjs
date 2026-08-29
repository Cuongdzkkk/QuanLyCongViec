import test from 'node:test'
import assert from 'node:assert/strict'
import { validateRewardForm, validateRewardSeasonForm } from '../src/utils/rewardUi.js'

test('reward season form rejects missing fields and invalid range', async () => {
  assert.deepEqual(validateRewardSeasonForm({ type: 'Custom', name: '', startAt: '2026-09-30', endAt: '2026-09-01' }), [
    'Season name is required.',
    'Season end must be after start.'
  ])
  assert.deepEqual(validateRewardSeasonForm({ type: 'Month', name: 'September', startAt: '2026-09-01' }), [])
})

test('reward form validates condition-specific manager inputs', async () => {
  assert.ok(validateRewardForm({ seasonId: 's1', name: 'Top', rewardType: 'Gift', condition: 'TopN', threshold: 0, rankTo: 0 }).includes('Top N must be a positive whole number.'))
  assert.deepEqual(validateRewardForm({ seasonId: 's1', name: 'On time', rewardType: 'Voucher', condition: 'TeamOnTimeRate', threshold: 90 }), [])
})
