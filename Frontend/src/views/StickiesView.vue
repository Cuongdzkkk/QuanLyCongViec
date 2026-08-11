<template>
  <section class="stickies-page">
    <header class="page-header">
      <div>
        <span class="eyebrow">PERSONAL PRODUCTIVITY</span>
        <h1>Ghi chú nhanh</h1>
        <p>Ghi chú cá nhân được đồng bộ với tài khoản của bạn.</p>
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
.stickies-page { --sa-page-x: 18px; min-height: 100%; background: var(--color-background); color: var(--color-text-primary); }
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
  min-height: 54px;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px var(--sa-page-x, 24px);
  background: var(--color-surface);
}
.search-field { width: min(360px, 100%); display: flex; align-items: center; gap: 8px; padding: 0 11px; border: 1px solid var(--color-border); border-radius: 7px; color: var(--color-text-muted); }
.search-field input { width: 100%; height: 34px; border: 0; outline: 0; background: transparent; color: var(--color-text-primary); font-size: 12px; }
.toolbar > button, .page-state button, .load-more {
  min-height: 34px;
  border: 1px solid var(--color-border);
  border-radius: 7px;
  padding: 0 11px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  cursor: pointer;
}
.toolbar > button.active { border-color: var(--color-accent); color: var(--color-accent); }
.toolbar > span { margin-left: auto; color: var(--color-text-muted); font-size: 11px; }
.page-content { padding: 18px var(--sa-page-x, 24px) 28px; }
.notes-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)); gap: 14px; align-items: start; }
.page-state { min-height: 360px; display: flex; align-items: center; justify-content: center; gap: 8px; color: var(--color-text-muted); text-align: center; }
.empty-state, .error-state { flex-direction: column; }
.empty-state > i { font-size: 34px; }
.empty-state strong, .error-state strong { color: var(--color-text-primary); }
.load-more { display: block; margin: 18px auto 0; }
@media (max-width: 700px) {
  .page-header { align-items: stretch; flex-direction: column; padding: 16px; }
  .primary-action { width: 100%; }
  .toolbar { flex-wrap: wrap; padding: 10px 12px; }
  .search-field { width: 100%; }
  .toolbar > span { margin-left: 0; }
  .page-content { padding: 12px; }
  .notes-grid { grid-template-columns: 1fr; }
}
</style>
