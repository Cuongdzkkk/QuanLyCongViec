<template>
  <section class="stickies-page">
    <header class="page-header app-shell-page-header">
      <div class="app-shell-title-wrap">
        <span class="eyebrow">PERSONAL PRODUCTIVITY</span>
        <h1>Ghi chú nhanh</h1>
        <div class="app-shell-header-help">
          <span class="app-shell-header-help-btn" aria-label="About Stickies">
            <i class="fa-solid fa-question"></i>
          </span>
          <div class="app-shell-header-help-popover" role="tooltip">
            <span>PERSONAL PRODUCTIVITY</span>
            <p>Ghi chú cá nhân được đồng bộ với tài khoản của bạn.</p>
          </div>
        </div>
      </div>
      <button class="primary-action" type="button" :disabled="creating" @click="addNote">
        <i :class="creating ? 'fa-solid fa-spinner fa-spin' : 'fa-solid fa-plus'"></i>
        Ghi chú mới
      </button>
    </header>

    <div class="toolbar">
      <label class="search-field">
        <i class="fa-solid fa-magnifying-glass"></i>
        <input v-model="search" type="search" placeholder="Tìm theo tiêu đề hoặc nội dung" />
      </label>
      <button type="button" :class="{ active: stickyStore.pinnedOnly }" @click="togglePinned">
        <i class="fa-solid fa-thumbtack"></i>
        Đã ghim
      </button>
      <span>{{ stickyStore.total }} ghi chú</span>
    </div>

    <main class="page-content">
      <div v-if="stickyStore.loading" class="page-state"><i class="fa-solid fa-spinner fa-spin"></i> Đang tải ghi chú...</div>
      <div v-else-if="stickyStore.error" class="page-state error-state">
        <strong>Không thể tải ghi chú</strong>
        <span>{{ stickyStore.error }}</span>
        <button type="button" @click="loadNotes">Thử lại</button>
      </div>
      <div v-else-if="!stickyStore.notes.length" class="page-state empty-state">
        <i class="fa-regular fa-note-sticky"></i>
        <strong>{{ stickyStore.pinnedOnly || search ? 'Không tìm thấy ghi chú phù hợp' : 'Chưa có ghi chú' }}</strong>
        <span v-if="!stickyStore.pinnedOnly && !search">Tạo ghi chú đầu tiên để lưu ý tưởng hoặc việc cần nhớ.</span>
        <button v-if="!stickyStore.pinnedOnly && !search" type="button" @click="addNote">Tạo ghi chú</button>
      </div>
      <template v-else>
        <div class="notes-grid">
          <StickyNoteEditor
            v-for="note in stickyStore.notes"
            :key="note.id"
            :note="note"
            :saving="stickyStore.isSaving(note.id)"
            @save="saveNote"
            @pin="pinNote"
            @delete="confirmDelete"
          />
        </div>
        <button v-if="stickyStore.hasMore" class="load-more" type="button" :disabled="stickyStore.loadingMore" @click="stickyStore.fetchNotes({ reset: false })">
          {{ stickyStore.loadingMore ? 'Đang tải...' : 'Tải thêm ghi chú' }}
        </button>
      </template>
    </main>
  </section>
</template>

<script setup>
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import StickyNoteEditor from '@/components/stickies/StickyNoteEditor.vue'
import { useStickyStore } from '@/store/useStickyStore'
import { getRandomPaletteColor } from '@/utils/colors'

const stickyStore = useStickyStore()
const search = ref(stickyStore.search)
const creating = ref(false)
let searchTimer = null

const loadNotes = async () => {
  try {
    await stickyStore.fetchNotes()
  } catch {
    // The page renders the error stored by Pinia.
  }
}

watch(search, value => {
  stickyStore.search = value
  clearTimeout(searchTimer)
  searchTimer = setTimeout(loadNotes, 350)
})

const togglePinned = () => {
  stickyStore.pinnedOnly = !stickyStore.pinnedOnly
  loadNotes()
}

const addNote = async () => {
  if (creating.value) return
  creating.value = true
  try {
    await stickyStore.createNote({
      title: 'Ghi chú mới',
      content: '',
      color: getRandomPaletteColor(stickyStore.notes[0]?.color),
      isPinned: false,
      sourceRoute: '/stickies'
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

onMounted(loadNotes)
onBeforeUnmount(() => clearTimeout(searchTimer))
</script>

<style scoped>
.stickies-page { --sa-page-x: 18px; width: 100%; min-width: 0; min-height: 100%; background: var(--color-background); color: var(--color-text-primary); }
.page-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 20px;
  padding: 22px var(--sa-page-x, 24px) 18px;
  background: var(--color-surface);
}
.eyebrow { color: var(--color-accent); font-size: 10px; font-weight: 800; }
h1 { margin: 3px 0 4px; font-size: 22px; letter-spacing: 0; }
.page-header p { margin: 0; color: var(--color-text-muted); font-size: 12px; }
.primary-action {
  min-height: 36px;
  border: 0;
  border-radius: 7px;
  padding: 0 14px;
  background: var(--color-accent);
  color: #fff;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
}
.toolbar {
  position: relative;
  z-index: 5;
  min-height: 42px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 8px !important;
  margin: 0 var(--sa-page-x, 24px) 18px;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 86%, transparent), color-mix(in srgb, var(--color-surface-hover) 46%, transparent));
  border: 1px solid color-mix(in srgb, var(--color-border) 72%, transparent);
  border-radius: 12px;
  box-shadow: 0 10px 24px color-mix(in srgb, #020617 6%, transparent);
  width: auto;
  box-sizing: border-box;
}
.search-field { position: relative; width: min(260px, 30vw); min-height: 34px; display: flex; align-items: center; gap: 0; padding: 0; border: 0; border-radius: 9px; color: var(--color-text-muted); background: transparent; box-shadow: none; }
.search-field > i { position: absolute; left: 12px; z-index: 1; color: var(--color-text-muted); font-size: 14px; }
.search-field input { width: 100%; height: 34px; border: 1px solid var(--color-border); border-radius: 9px; outline: 0; padding-left: 36px; padding-right: 12px; background: var(--color-surface); color: var(--color-text-primary); font-size: 13.5px; transition: border-color 0.2s, box-shadow 0.2s; }
.search-field input:focus { border-color: var(--color-accent); box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15); }
.toolbar > button, .page-state button, .load-more {
  height: 34px;
  min-height: 34px;
  border: 1px solid var(--color-border);
  border-radius: 9px;
  padding: 0 12px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  cursor: pointer;
  font-size: 13px;
  font-weight: 600;
}
.toolbar > button.active { border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)); color: var(--color-accent); background: color-mix(in srgb, var(--color-accent) 14%, var(--color-surface)); box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
.toolbar > span { margin-left: auto; color: var(--color-text-muted); font-size: 11px; }
.toolbar,
.toolbar .search-field input,
.toolbar > button {
  overflow: hidden;
}
.toolbar { border-radius: 12px !important; }
.toolbar .search-field input,
.toolbar > button { border-radius: 9px !important; }
.toolbar .search-field {
  flex: 0 0 min(326px, 34vw);
  width: min(326px, 34vw) !important;
}
.search-field > i {
  top: 50%;
  transform: translateY(-50%);
  pointer-events: none;
}
.search-field input[type="search"] {
  box-sizing: border-box !important;
  padding: 0 12px 0 36px !important;
  -webkit-appearance: none;
  appearance: none;
}
.stickies-page .page-content {
  width: 100% !important;
  max-width: none !important;
  margin: 0 !important;
  padding: 18px !important;
  box-sizing: border-box !important;
}
.notes-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)); gap: 14px; align-items: start; }
.page-state {
  width: 100% !important;
  min-width: 100% !important;
  min-height: 360px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  color: var(--color-text-muted);
  text-align: center;
  box-sizing: border-box !important;
}
.page-state.empty-state,
.page-state.error-state {
  border: 1px dashed var(--color-border) !important;
  border-radius: 12px !important;
  background: var(--color-surface) !important;
  padding: 54px 24px !important;
}
.empty-state, .error-state { flex-direction: column; }
.empty-state > i { font-size: 34px; }
.empty-state strong, .error-state strong { color: var(--color-text-primary); }
.load-more { display: block; margin: 18px auto 0; }
@media (max-width: 700px) {
  .page-header { align-items: stretch; flex-direction: column; padding: 16px; }
  .primary-action { width: 100%; }
  .toolbar { flex-wrap: wrap; margin: 0 12px 12px; padding: 10px 12px; }
  .search-field { width: 100%; }
  .toolbar > span { margin-left: 0; }
  .stickies-page .page-content { padding: 12px !important; }
  .notes-grid { grid-template-columns: 1fr; }
}
</style>
