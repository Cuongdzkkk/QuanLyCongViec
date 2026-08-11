<template>
  <transition name="notes-fade">
    <div v-if="visible" class="notes-backdrop" @click="$emit('close')"></div>
  </transition>

  <transition name="notes-slide">
    <aside v-if="visible" id="global-stickies-drawer" class="notes-drawer" role="dialog" aria-modal="false" aria-label="Ghi chú nhanh">
      <header class="drawer-head">
        <div>
          <span class="eyebrow">QUICK NOTES</span>
          <h2>Ghi chú nhanh</h2>
        </div>
        <button type="button" title="Đóng ghi chú" @click="$emit('close')"><i class="fa-solid fa-xmark"></i></button>
      </header>

      <div class="drawer-tools">
        <button class="add-note" type="button" :disabled="creating" @click="createNote">
          <i :class="creating ? 'fa-solid fa-spinner fa-spin' : 'fa-solid fa-plus'"></i>
          Ghi chú
        </button>
        <label class="search-box">
          <i class="fa-solid fa-magnifying-glass"></i>
          <input v-model="search" type="search" placeholder="Tìm ghi chú" />
        </label>
        <button class="pin-filter" type="button" :class="{ active: stickyStore.pinnedOnly }" title="Chỉ hiện ghi chú đã ghim" @click="togglePinnedFilter">
          <i class="fa-solid fa-thumbtack"></i>
        </button>
      </div>

      <div class="drawer-content">
        <p v-if="stickyStore.loading" class="drawer-state"><i class="fa-solid fa-spinner fa-spin"></i> Đang tải ghi chú...</p>
        <div v-else-if="stickyStore.error" class="drawer-state is-error">
          <span>{{ stickyStore.error }}</span>
          <button type="button" @click="loadNotes">Thử lại</button>
        </div>
        <div v-else-if="!stickyStore.notes.length" class="drawer-state empty">
          <i class="fa-regular fa-note-sticky"></i>
          <strong>Chưa có ghi chú</strong>
          <span>Tạo ghi chú cho trang bạn đang xem.</span>
        </div>
        <div v-else class="note-list">
          <StickyNoteEditor
            v-for="note in stickyStore.notes"
            :key="note.id"
            :note="note"
            :saving="stickyStore.isSaving(note.id)"
            compact
            allow-float
            @save="saveNote"
            @pin="pinNote"
            @delete="confirmDelete"
            @float-drag-start="startFloatingDrag"
            @float-drag-end="stickyStore.endDrawerDrag"
          />
          <button v-if="stickyStore.hasMore" class="load-more" type="button" :disabled="stickyStore.loadingMore" @click="stickyStore.fetchNotes({ reset: false })">
            {{ stickyStore.loadingMore ? 'Đang tải...' : 'Tải thêm' }}
          </button>
        </div>
      </div>

      <footer class="drawer-footer">
        <button type="button" @click="openManager">
          <i class="fa-solid fa-arrow-up-right-from-square"></i>
          Mở trang quản lý đầy đủ
        </button>
      </footer>
    </aside>
  </transition>
</template>

<script setup>
import { ref, watch, onBeforeUnmount } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import StickyNoteEditor from './StickyNoteEditor.vue'
import { MAX_FLOATING_STICKIES, useStickyStore } from '@/store/useStickyStore'
import { getRandomPaletteColor } from '@/utils/colors'

const props = defineProps({
  visible: { type: Boolean, default: false },
  context: { type: Object, default: () => ({}) }
})
const emit = defineEmits(['close'])
const router = useRouter()
const stickyStore = useStickyStore()
const search = ref('')
const creating = ref(false)
let searchTimer = null

const loadNotes = async () => {
  try {
    await stickyStore.fetchNotes()
  } catch {
    // The store exposes the API error in the drawer state.
  }
}

watch(() => props.visible, value => {
  if (value) loadNotes()
})

watch(search, value => {
  stickyStore.search = value
  clearTimeout(searchTimer)
  searchTimer = setTimeout(loadNotes, 350)
})

const togglePinnedFilter = () => {
  stickyStore.pinnedOnly = !stickyStore.pinnedOnly
  loadNotes()
}

const createNote = async () => {
  if (creating.value) return
  creating.value = true
  try {
    await stickyStore.createNote({
      ...props.context,
      title: 'Ghi chú mới',
      content: '',
      color: getRandomPaletteColor(stickyStore.notes[0]?.color),
      isPinned: false
    })
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể tạo ghi chú.')
  } finally {
    creating.value = false
  }
}

const saveNote = async note => {
  try {
    await stickyStore.updateNote(note)
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể lưu ghi chú.')
  }
}

const pinNote = async (note, value) => {
  try {
    await stickyStore.setPinned(note, value)
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể cập nhật ghim.')
  }
}

const startFloatingDrag = (note, event) => {
  if (window.innerWidth <= 760) {
    event.preventDefault()
    ElMessage.info('Kéo ghi chú nổi hiện chỉ hỗ trợ trên desktop.')
    return
  }
  if (note.isFloating) {
    event.preventDefault()
    return
  }
  if (!stickyStore.canAddFloating) {
    event.preventDefault()
    ElMessage.warning(`Bạn chỉ có thể dán tối đa ${MAX_FLOATING_STICKIES} ghi chú. Hãy gỡ một ghi chú khỏi màn hình trước.`)
    return
  }

  stickyStore.beginDrawerDrag(note.id)
  event.dataTransfer.effectAllowed = 'move'
  event.dataTransfer.setData('application/x-sprinta-sticky', note.id)
  event.dataTransfer.setData('text/plain', note.id)
}

const confirmDelete = async note => {
  try {
    await ElMessageBox.confirm(`Xóa ghi chú “${note.title}”?`, 'Xác nhận xóa', {
      confirmButtonText: 'Xóa',
      cancelButtonText: 'Hủy',
      type: 'warning'
    })
    await stickyStore.deleteNote(note.id)
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') ElMessage.error(error.response?.data?.message || 'Không thể xóa ghi chú.')
  }
}

const openManager = () => {
  emit('close')
  router.push('/stickies')
}

onBeforeUnmount(() => clearTimeout(searchTimer))
</script>

<style scoped>
.notes-backdrop {
  display: none;
  position: fixed;
  inset: var(--sa-topbar-height, 52px) 0 0;
  z-index: 1490;
  background: rgba(2, 6, 23, 0.48);
  backdrop-filter: none;
  -webkit-backdrop-filter: none;
}
.notes-drawer {
  position: fixed;
  z-index: 1500;
  top: calc(var(--sa-topbar-height, 52px) + 16px);
  right: 16px;
  bottom: 16px;
  width: min(400px, calc(100vw - 32px));
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface);
  box-shadow: var(--shadow-popover);
}
.drawer-head, .drawer-tools, .drawer-footer { flex: 0 0 auto; }
.drawer-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px;
  border-bottom: 1px solid var(--color-border);
}
.eyebrow { display: block; color: var(--color-accent); font-size: 10px; font-weight: 800; }
h2 { margin: 2px 0 0; color: var(--color-text-primary); font-size: 17px; letter-spacing: 0; }
.drawer-head button, .pin-filter {
  width: 32px;
  height: 32px;
  border: 0;
  border-radius: 7px;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
}
.drawer-head button:hover, .pin-filter:hover, .pin-filter.active { background: var(--color-surface-hover); color: var(--color-text-primary); }
.drawer-tools { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; gap: 8px; padding: 12px; border-bottom: 1px solid var(--color-border); }
.add-note {
  min-height: 34px;
  border: 0;
  border-radius: 7px;
  padding: 0 10px;
  background: var(--color-accent);
  color: #fff;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
}
.search-box { min-width: 0; display: flex; align-items: center; gap: 7px; padding: 0 10px; border: 1px solid var(--color-border); border-radius: 7px; color: var(--color-text-muted); }
.search-box input { min-width: 0; width: 100%; border: 0; outline: 0; background: transparent; color: var(--color-text-primary); font-size: 12px; }
.drawer-content { min-height: 0; flex: 1; overflow-y: auto; padding: 12px; }
.note-list { display: flex; flex-direction: column; gap: 10px; }
.drawer-state { min-height: 240px; display: flex; align-items: center; justify-content: center; gap: 8px; color: var(--color-text-muted); font-size: 13px; text-align: center; }
.drawer-state.empty { flex-direction: column; }
.drawer-state.empty > i { font-size: 28px; }
.drawer-state.empty strong { color: var(--color-text-primary); }
.drawer-state.is-error { flex-direction: column; color: var(--color-danger); }
.drawer-state button, .load-more { border: 1px solid var(--color-border); border-radius: 7px; padding: 7px 10px; background: var(--color-surface); color: var(--color-text-primary); cursor: pointer; }
.load-more { width: 100%; }
.drawer-footer { padding: 10px 12px; border-top: 1px solid var(--color-border); }
.drawer-footer button { width: 100%; border: 0; background: transparent; color: var(--color-accent); font-size: 12px; font-weight: 700; cursor: pointer; }
.notes-slide-enter-active, .notes-slide-leave-active { transition: transform 220ms ease, opacity 220ms ease; }
.notes-slide-enter-from, .notes-slide-leave-to { transform: translateX(24px); opacity: 0; }
.notes-fade-enter-active, .notes-fade-leave-active { transition: opacity 180ms ease; }
.notes-fade-enter-from, .notes-fade-leave-to { opacity: 0; }
@media (max-width: 760px) {
  .notes-backdrop { display: block; }
  .notes-drawer { top: 56px; right: 8px; left: 8px; bottom: 8px; width: auto; border-radius: 10px; }
  .drawer-tools { grid-template-columns: auto minmax(0, 1fr) auto; }
}
@media (prefers-reduced-motion: reduce) {
  .notes-slide-enter-active, .notes-slide-leave-active, .notes-fade-enter-active, .notes-fade-leave-active { transition: none; }
}
</style>
