import assert from 'node:assert/strict'
import fs from 'node:fs'
import test from 'node:test'
import {
  STICKY_LAUNCHER_DRAG_THRESHOLD,
  clampStickyLauncherY,
  getStickyLauncherDragY,
  getStickyLauncherStorageKey,
  hasStickyLauncherDragged,
  readStickyLauncherY,
  writeStickyLauncherY
} from '../src/utils/stickyLauncher.js'

const nexusLayout = fs.readFileSync(new URL('../src/components/layout/NexusLayout.vue', import.meta.url), 'utf8')
const floatingLayer = fs.readFileSync(new URL('../src/components/stickies/FloatingStickiesLayer.vue', import.meta.url), 'utf8')
const stickyStore = fs.readFileSync(new URL('../src/store/useStickyStore.js', import.meta.url), 'utf8')

test('launcher remains available without notes and exposes the real quick-create action', () => {
  assert.match(nexusLayout, /class="global-utility-rail"/)
  assert.match(nexusLayout, /stickyStore\.createNote\(/)
  assert.match(nexusLayout, /stickyStore\.setFloatingState\(/)
})

test('launcher drag changes Y while ignoring horizontal movement', () => {
  assert.equal(getStickyLauncherDragY(240, 80, 900, 42, 64), 320)
  assert.equal(getStickyLauncherDragY(240, 0, 900, 42, 64), 240)
})

test('launcher movement clamps to the viewport', () => {
  assert.equal(clampStickyLauncherY(-20, 900, 42, 64, 12), 64)
  assert.equal(clampStickyLauncherY(9999, 900, 42, 64, 12), 846)
})

test('clicks below the drag threshold remain clicks', () => {
  assert.equal(hasStickyLauncherDragged(10, 10, 10 + STICKY_LAUNCHER_DRAG_THRESHOLD - 1, 10), false)
  assert.equal(hasStickyLauncherDragged(10, 10, 10, 10 + STICKY_LAUNCHER_DRAG_THRESHOLD), true)
})

test('launcher position restores per account without storing note content', () => {
  const values = new Map()
  const storage = {
    getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value)
  }
  writeStickyLauncherY(storage, 'account-a', 530)
  assert.equal(readStickyLauncherY(storage, 'account-a', 900, 42, 64), 530)
  assert.equal(readStickyLauncherY(storage, 'account-b', 900, 42, 64), null)
  assert.notEqual(getStickyLauncherStorageKey('account-a'), getStickyLauncherStorageKey('account-b'))
  assert.equal([...values.values()].some(value => `${value}`.includes('Ghi chú')), false)
})

test('floating title and content are directly editable with the existing debounce', () => {
  assert.match(floatingLayer, /v-model="note\.title"/)
  assert.match(floatingLayer, /v-model="note\.content"/)
  assert.match(floatingLayer, /setTimeout\(\(\) => \{/)
  assert.match(floatingLayer, /}, 850\)/)
})

test('floating note header is the drag surface and editable body is not draggable', () => {
  assert.match(floatingLayer, /class="floating-move-handle"[\s\S]*@pointerdown="beginMove\(note, \$event\)"/)
  assert.doesNotMatch(floatingLayer, /floating-sticky-content[\s\S]*@pointerdown/)
})

test('floating edit reuses Pinia state so drawer and page receive the draft', () => {
  assert.match(floatingLayer, /stickyStore\.replaceNote\(note\)/)
  assert.match(floatingLayer, /stickyStore\.updateNote\(note\)/)
  assert.match(stickyStore, /replaceNote\(updated\)/)
})

test('X action removes floating state without deleting the note', () => {
  assert.match(floatingLayer, /@click="removeFromScreen\(note\)"/)
  assert.match(floatingLayer, /setFloatingState\(note, \{ isFloating: false \}\)/)
  assert.doesNotMatch(floatingLayer, /removeFromScreen[\s\S]*deleteNote/)
})

test('launcher has accessible main, plus, and drag-handle controls', () => {
  assert.match(nexusLayout, /aria-label="Kéo để di chuyển launcher ghi chú theo chiều dọc"/)
  assert.match(nexusLayout, /aria-label="Tạo ghi chú mới"/)
  assert.match(nexusLayout, /aria-controls="global-stickies-drawer"/)
})

test('account switch reset remains owned by the sticky store', () => {
  assert.match(stickyStore, /AUTH_SESSION_CHANGED/)
  assert.match(stickyStore, /resetForAccountChange\(nextAccountId\)/)
  assert.match(stickyStore, /store\.floatingNotes = \[\]/)
})

test('five-note floating limit remains explicit', () => {
  assert.match(nexusLayout, /MAX_FLOATING_STICKIES/)
  assert.match(nexusLayout, /chỉ có thể dán tối đa/)
})
