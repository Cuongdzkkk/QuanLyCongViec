<template>
  <div
    class="floating-stickies-layer"
    :class="{ 'accepting-drop': Boolean(stickyStore.draggingNoteId) }"
    aria-live="polite"
    @dragover="handleDragOver"
    @drop="handleDrop"
  >
    <div v-if="stickyStore.draggingNoteId" class="floating-drop-hint">
      <i class="fa-solid fa-note-sticky"></i>
      Thả vào vùng nội dung để dán ghi chú
    </div>

    <div v-if="stickyStore.floatingError" class="floating-layer-error" role="alert">
      <span>{{ stickyStore.floatingError }}</span>
      <button type="button" @click="loadFloatingNotes">Thử lại</button>
    </div>

    <article
      v-for="(note, index) in floatingNotes"
      :key="note.id"
      :data-floating-note-id="note.id"
      class="floating-sticky"
      :class="{ moving: movingNoteId === note.id }"
      :style="floatingStyle(note, index)"
    >
      <header class="floating-sticky-head">
        <button
          class="floating-move-handle"
          type="button"
          title="Kéo để di chuyển"
          aria-label="Kéo để di chuyển ghi chú"
          @pointerdown="beginMove(note, $event)"
        >
          <i class="fa-solid fa-grip-vertical"></i>
        </button>
        <input
          v-model="note.title"
          class="floating-sticky-title"
          type="text"
          maxlength="180"
          aria-label="Tiêu đề ghi chú"
          placeholder="Tiêu đề ghi chú"
          @input="scheduleNoteSave(note)"
        />
        <button
          class="floating-close"
          type="button"
          title="Gỡ khỏi màn hình"
          aria-label="Gỡ ghi chú khỏi màn hình"
          :disabled="stickyStore.isFloatingSaving(note.id)"
          @click="removeFromScreen(note)"
        >
          <i :class="stickyStore.isFloatingSaving(note.id) ? 'fa-solid fa-spinner fa-spin' : 'fa-solid fa-xmark'"></i>
        </button>
      </header>

      <textarea
        v-model="note.content"
        class="floating-sticky-content"
        maxlength="10000"
        aria-label="Nội dung ghi chú"
        placeholder="Nhập nội dung..."
        @input="scheduleNoteSave(note)"
      ></textarea>

      <footer class="floating-sticky-footer">
        <span>{{ formatUpdated(note.updatedAt) }}</span>
        <span v-if="stickyStore.isFloatingSaving(note.id)">Đang lưu vị trí...</span>
      </footer>
    </article>
  </div>
</template>

<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { MAX_FLOATING_STICKIES, useStickyStore } from '@/store/useStickyStore'
import { getContrastTextColor } from '@/utils/colors'

const emit = defineEmits(['floated'])
const stickyStore = useStickyStore()
const movingNoteId = ref('')
const floatingNotes = computed(() => Array.isArray(stickyStore.floatingNotes) ? stickyStore.floatingNotes : [])

const NOTE_WIDTH = 300
const NOTE_HEIGHT = 220
const GUTTER = 12
let moveState = null
let resizeTimer = null
const saveTimers = new Map()

const getContentBounds = () => {
  const content = document.querySelector('.content-area')
  const rect = content?.getBoundingClientRect()
  const left = Math.max(GUTTER, Math.round((rect?.left || 0) + GUTTER))
  const top = Math.max(GUTTER, Math.round((rect?.top || 0) + GUTTER))
  const rightEdge = Math.min(window.innerWidth - GUTTER, Math.round((rect?.right || window.innerWidth) - GUTTER))
  const bottomEdge = window.innerHeight - GUTTER

  return {
    minX: left,
    minY: top,
    maxX: Math.max(left, rightEdge - NOTE_WIDTH),
    maxY: Math.max(top, bottomEdge - NOTE_HEIGHT)
  }
}

const clampPosition = (x, y) => {
  const bounds = getContentBounds()
  return {
    x: Math.round(Math.min(Math.max(Number(x) || bounds.minX, bounds.minX), bounds.maxX)),
    y: Math.round(Math.min(Math.max(Number(y) || bounds.minY, bounds.minY), bounds.maxY))
  }
}

const floatingStyle = (note, index) => {
  const position = clampPosition(note.positionX, note.positionY)
  const color = note.color || '#EAB308'
  return {
    left: `${position.x}px`,
    top: `${position.y}px`,
    zIndex: 1 + index,
    '--floating-note-color': color,
    '--floating-note-text': getContrastTextColor(color)
  }
}

const floatingLimitMessage = () => {
  ElMessage.warning(`Bạn chỉ có thể dán tối đa ${MAX_FLOATING_STICKIES} ghi chú. Hãy gỡ một ghi chú khỏi màn hình trước.`)
}

const scheduleNoteSave = note => {
  stickyStore.replaceNote(note)
  const previousTimer = saveTimers.get(note.id)
  if (previousTimer) clearTimeout(previousTimer)
  saveTimers.set(note.id, window.setTimeout(() => {
    saveTimers.delete(note.id)
    stickyStore.updateNote(note).catch(error => {
      ElMessage.error(error.response?.data?.message || 'Không thể lưu ghi chú.')
    })
  }, 850))
}

const flushNoteSaves = () => {
  for (const [noteId, timer] of saveTimers) {
    clearTimeout(timer)
    const note = floatingNotes.value.find(item => item.id === noteId)
    if (note) stickyStore.updateNote(note)
  }
  saveTimers.clear()
}

const loadFloatingNotes = async () => {
  try {
    await stickyStore.fetchFloatingNotes()
    await nextTick()
    await normalizeFloatingPositions()
  } catch {
    // The layer exposes the API error and retry action.
  }
}

const handleDragOver = event => {
  if (!stickyStore.draggingNoteId || window.innerWidth <= 760) return
  event.preventDefault()
  event.dataTransfer.dropEffect = 'move'
}

const handleDrop = async event => {
  if (window.innerWidth <= 760) return
  event.preventDefault()

  const noteId = event.dataTransfer.getData('application/x-sprinta-sticky') || stickyStore.draggingNoteId
  const note = stickyStore.findNote(noteId)
  stickyStore.endDrawerDrag()
  if (!note || note.isFloating) return
  if (!stickyStore.canAddFloating) {
    floatingLimitMessage()
    return
  }

  const position = clampPosition(event.clientX - 24, event.clientY - 24)
  try {
    const updated = await stickyStore.setFloatingState(note, {
      isFloating: true,
      positionX: position.x,
      positionY: position.y
    })
    emit('floated', updated)
  } catch (error) {
    if (error.code === 'FLOATING_LIMIT' || error.response?.status === 409) floatingLimitMessage()
    else ElMessage.error(error.response?.data?.message || 'Không thể dán ghi chú lên màn hình.')
  }
}

const beginMove = (note, event) => {
  if (event.button !== 0 || window.innerWidth <= 760) return
  event.preventDefault()
  event.stopPropagation()

  const current = clampPosition(note.positionX, note.positionY)
  moveState = {
    note,
    pointerId: event.pointerId,
    originX: current.x,
    originY: current.y,
    pointerX: event.clientX,
    pointerY: event.clientY,
    changed: false
  }
  movingNoteId.value = note.id
  event.currentTarget.setPointerCapture?.(event.pointerId)
  window.addEventListener('pointermove', moveFloatingNote)
  window.addEventListener('pointerup', finishMove)
  window.addEventListener('pointercancel', cancelMove)
}

const moveFloatingNote = event => {
  if (!moveState || event.pointerId !== moveState.pointerId) return
  const position = clampPosition(
    moveState.originX + event.clientX - moveState.pointerX,
    moveState.originY + event.clientY - moveState.pointerY
  )
  moveState.note.positionX = position.x
  moveState.note.positionY = position.y
  moveState.changed = position.x !== moveState.originX || position.y !== moveState.originY
}

const clearMoveListeners = () => {
  window.removeEventListener('pointermove', moveFloatingNote)
  window.removeEventListener('pointerup', finishMove)
  window.removeEventListener('pointercancel', cancelMove)
  movingNoteId.value = ''
}

const finishMove = async event => {
  if (!moveState || event.pointerId !== moveState.pointerId) return
  const completed = moveState
  moveState = null
  clearMoveListeners()
  if (!completed.changed) return

  try {
    await stickyStore.setFloatingState(completed.note, {
      isFloating: true,
      positionX: completed.note.positionX,
      positionY: completed.note.positionY
    })
  } catch (error) {
    const current = floatingNotes.value.find(note => note.id === completed.note.id)
    if (current) {
      current.positionX = completed.originX
      current.positionY = completed.originY
    }
    ElMessage.error(error.response?.data?.message || 'Không thể lưu vị trí ghi chú.')
  }
}

const cancelMove = event => {
  if (!moveState || event.pointerId !== moveState.pointerId) return
  moveState.note.positionX = moveState.originX
  moveState.note.positionY = moveState.originY
  moveState = null
  clearMoveListeners()
}

const removeFromScreen = async note => {
  try {
    await stickyStore.setFloatingState(note, { isFloating: false })
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể gỡ ghi chú khỏi màn hình.')
  }
}

const normalizeFloatingPositions = async () => {
  if (window.innerWidth <= 760) return
  const updates = []
  for (const note of floatingNotes.value) {
    const position = clampPosition(note.positionX, note.positionY)
    if (position.x === note.positionX && position.y === note.positionY) continue
    note.positionX = position.x
    note.positionY = position.y
    updates.push(stickyStore.setFloatingState(note, {
      isFloating: true,
      positionX: position.x,
      positionY: position.y
    }))
  }
  if (updates.length) await Promise.allSettled(updates)
}

const handleResize = () => {
  clearTimeout(resizeTimer)
  resizeTimer = window.setTimeout(normalizeFloatingPositions, 180)
}

const formatUpdated = value => {
  if (!value) return 'Chưa lưu'
  return new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

onMounted(() => {
  loadFloatingNotes()
  window.addEventListener('resize', handleResize)
})

onBeforeUnmount(() => {
  flushNoteSaves()
  clearTimeout(resizeTimer)
  clearMoveListeners()
  stickyStore.endDrawerDrag()
  window.removeEventListener('resize', handleResize)
})
</script>

<style scoped>
.floating-stickies-layer {
  position: fixed;
  z-index: 1350;
  inset: 0;
  pointer-events: none;
}

.floating-stickies-layer.accepting-drop {
  pointer-events: auto;
}

.floating-drop-hint,
.floating-layer-error {
  position: fixed;
  z-index: 10;
  left: 50%;
  display: flex;
  align-items: center;
  gap: 8px;
  transform: translateX(-50%);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface);
  color: var(--color-text-primary);
  box-shadow: var(--shadow-popover);
  font-size: 12px;
  font-weight: 700;
}

.floating-drop-hint {
  top: calc(var(--sa-topbar-height, 52px) + 14px);
  padding: 9px 12px;
}

.floating-layer-error {
  bottom: 18px;
  padding: 8px;
  pointer-events: auto;
}

.floating-layer-error button {
  border: 0;
  background: transparent;
  color: var(--color-accent);
  cursor: pointer;
  font-weight: 800;
}

.floating-sticky {
  position: fixed;
  width: 300px;
  height: 220px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  pointer-events: auto;
  border: 1px solid color-mix(in srgb, var(--floating-note-color) 72%, var(--color-border));
  border-radius: 8px;
  background: color-mix(in srgb, var(--floating-note-color) 19%, var(--color-surface));
  color: var(--floating-note-text);
  box-shadow: 0 16px 38px rgba(2, 6, 23, .24);
}

.floating-sticky.moving {
  box-shadow: 0 22px 48px rgba(2, 6, 23, .32);
}

.floating-sticky-head {
  min-height: 42px;
  display: grid;
  grid-template-columns: 34px minmax(0, 1fr) 34px;
  align-items: center;
  gap: 6px;
  padding: 4px 6px;
  border-bottom: 1px solid color-mix(in srgb, var(--floating-note-color) 50%, var(--color-border));
}

.floating-sticky-title {
  overflow: hidden;
  min-width: 0;
  width: 100%;
  border: 0;
  border-bottom: 1px solid transparent;
  outline: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  font-size: 13px;
  font-weight: 700;
}

.floating-sticky-title:focus { border-bottom-color: currentColor; }

.floating-move-handle,
.floating-close {
  width: 34px;
  height: 34px;
  display: grid;
  place-items: center;
  padding: 0;
  border: 0;
  border-radius: 6px;
  background: transparent;
  color: inherit;
}

.floating-move-handle {
  cursor: grab;
  touch-action: none;
}

.floating-move-handle:active {
  cursor: grabbing;
}

.floating-close {
  cursor: pointer;
}

.floating-move-handle:hover,
.floating-move-handle:focus-visible,
.floating-close:hover,
.floating-close:focus-visible {
  background: color-mix(in srgb, var(--floating-note-color) 26%, transparent);
  outline: none;
}

.floating-sticky-content {
  min-height: 0;
  flex: 1;
  width: 100%;
  margin: 0;
  padding: 12px;
  overflow-y: auto;
  resize: none;
  border: 0;
  outline: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  font-size: 13px;
  line-height: 1.5;
  white-space: pre-wrap;
}

.floating-sticky-content::placeholder,
.floating-sticky-title::placeholder { color: currentColor; opacity: .62; }

.floating-sticky-footer {
  min-height: 32px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 6px 10px;
  border-top: 1px solid color-mix(in srgb, var(--floating-note-color) 42%, var(--color-border));
  font-size: 10px;
  opacity: .78;
}

@media (max-width: 760px) {
  .floating-stickies-layer {
    display: none;
  }
}

@media (prefers-reduced-motion: reduce) {
  .floating-sticky {
    scroll-behavior: auto;
  }
}
</style>
