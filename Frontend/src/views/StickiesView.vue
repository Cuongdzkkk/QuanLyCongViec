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

    <div class="sprinta-layout-toolbar">
      <ProjectPageToolbar
        v-model:searchQuery="search"
        show-search
        search-placeholder="Tìm theo tiêu đề hoặc nội dung"
      >
        <template #filters>
          <div class="filter-dropdown-wrapper js-toolbar-popup-scope">
            <button
              class="timeline-filter-trigger icon-only-trigger"
              type="button"
              aria-label="Filters"
              title="Bộ lọc"
              @click="toggleFilterDropdown"
              :class="{ active: showFilterDropdown || activeFilters.length }"
            >
              <i class="fa-solid fa-filter"></i>
              <span v-if="activeFilters.length" class="filter-count">{{ activeFilters.length }}</span>
            </button>
            <div class="plane-dropdown-menu filter-dropdown-menu" v-show="showFilterDropdown" @click.stop>
              <FilterBar
                v-model:filters="activeFilters"
                :fields="stickyFilterFields"
                :operators="stickyOperators"
                :custom-value-meta="customStickyValueMeta"
                :active="showFilterDropdown"
              />
            </div>
          </div>
        </template>
  
        <template #actions>
          <span class="toolbar-count">{{ stickyStore.total }} ghi chú</span>
        </template>
  
        <template #sort>
          <div class="display-dropdown-wrapper js-toolbar-popup-scope" style="position: relative; display: inline-block;">
            <button
              class="timeline-filter-trigger icon-only-trigger"
              type="button"
              aria-label="Sort"
              title="Sắp xếp"
              @click.stop="toggleSortDropdown"
              :class="{ 'active': showSortDropdown }"
            >
              <i class="fa-solid fa-arrow-down-wide-short"></i>
            </button>
            <div class="plane-dropdown-menu" v-show="showSortDropdown" @click.stop style="width: 320px; left: 0; right: auto; display: flex; flex-direction: column; gap: 10px; padding: 8px; max-height: none; overflow: visible;">
              <!-- Sort Search Input -->
              <div class="filter-search-field">
                <i class="fa-solid fa-magnifying-glass filter-search-icon"></i>
                <input
                  v-model="sortSearchQuery"
                  type="text"
                  class="filter-search-input"
                  placeholder="Tìm kiếm trường sắp xếp..."
                  @click.stop
                />
              </div>
  
              <!-- Sort By Combobox -->
              <div class="filter-combobox" style="position: relative;">
                <span class="filter-label">Sắp xếp theo</span>
                <div class="filter-select-trigger sort-combobox-trigger">
                  <div style="display: flex; align-items: center; gap: 10px; flex: 1; cursor: pointer; min-width: 0;" @click="openSortSelect = (openSortSelect === 'sort' ? null : 'sort')">
                    <i :class="stickySortOptions.find(o => o.value === stickySortBy)?.icon || 'fa-solid fa-arrow-down-wide-short'" style="font-size: 13px; color: var(--color-text-secondary); width: 15px; text-align: center;"></i>
                    <span style="font-size: 13px; color: var(--color-text-primary); text-align: left; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">{{ stickySortOptions.find(o => o.value === stickySortBy)?.label }}</span>
                  </div>
                  <!-- Asc/Desc buttons inside the trigger -->
                  <div style="display: flex; align-items: center; gap: 4px; margin-right: 8px;">
                    <button
                      type="button"
                      class="dir-mini-btn"
                      :class="{ active: stickySortDirection === 'asc' }"
                      @click="stickySortDirection = 'asc'"
                      title="Tăng dần"
                    >
                      <i class="fa-solid fa-arrow-up-wide-short" style="font-size: 11px;"></i>
                    </button>
                    <button
                      type="button"
                      class="dir-mini-btn"
                      :class="{ active: stickySortDirection === 'desc' }"
                      @click="stickySortDirection = 'desc'"
                      title="Giảm dần"
                    >
                      <i class="fa-solid fa-arrow-down-short-wide" style="font-size: 11px;"></i>
                    </button>
                  </div>
                  <i class="fa-solid fa-chevron-down" style="font-size: 10px; transition: transform 0.2s; cursor: pointer;" :style="openSortSelect === 'sort' ? { transform: 'rotate(180deg)', color: 'var(--color-accent)' } : {}" @click="openSortSelect = (openSortSelect === 'sort' ? null : 'sort')"></i>
                </div>
                <div v-show="openSortSelect === 'sort'" class="filter-select-menu" style="position: absolute; top: calc(100% + 4px); left: 0; right: 0; max-height: 200px; z-index: 110;">
                  <button
                    v-for="opt in filteredStickySortOptions"
                    :key="opt.value"
                    class="filter-select-option"
                    :class="{ selected: stickySortBy === opt.value }"
                    type="button"
                    @click="stickySortBy = opt.value; openSortSelect = null"
                  >
                    <i :class="opt.icon"></i>
                    <span>{{ opt.label }}</span>
                  </button>
                </div>
              </div>
            </div>
          </div>
        </template>
      </ProjectPageToolbar>
    </div>

    <main class="page-content">
      <div v-if="stickyStore.loading" class="page-state"><i class="fa-solid fa-spinner fa-spin"></i> Đang tải ghi chú...</div>
      <div v-else-if="stickyStore.error" class="page-state error-state">
        <strong>Không thể tải ghi chú</strong>
        <span>{{ stickyStore.error }}</span>
        <button type="button" @click="loadNotes">Thử lại</button>
      </div>
      <div v-else-if="!displayNotes.length" class="empty-spaces-flat" style="padding: 80px 0;">
        <div class="empty-spaces-icon" aria-hidden="true">
          <i class="fa-regular fa-note-sticky"></i>
        </div>
        <div class="empty-spaces-copy">
          <h3>{{ activeFilters.length || search ? 'Không tìm thấy ghi chú phù hợp' : 'Chưa có ghi chú' }}</h3>
          <p v-if="!activeFilters.length && !search">Tạo ghi chú đầu tiên để lưu ý tưởng hoặc việc cần nhớ.</p>
          <button v-if="!activeFilters.length && !search" class="empty-spaces-btn mt-3" type="button" @click="addNote">
            Tạo ghi chú
          </button>
        </div>
      </div>
      <template v-else>
        <div class="notes-grid">
          <StickyNoteEditor
            v-for="note in displayNotes"
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
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import StickyNoteEditor from '@/components/stickies/StickyNoteEditor.vue'
import ProjectPageToolbar from '@/components/common/ProjectPageToolbar.vue'
import FilterBar from '@/components/FilterBar.vue'
import { useStickyStore } from '@/store/useStickyStore'
import { getRandomPaletteColor } from '@/utils/colors'

const stickyStore = useStickyStore()
const search = ref(stickyStore.search)
const creating = ref(false)

// Dropdowns State
const showFilterDropdown = ref(false)
const showSortDropdown = ref(false)
const openSortSelect = ref(null)

const toggleFilterDropdown = () => {
  showFilterDropdown.value = !showFilterDropdown.value
  showSortDropdown.value = false
}

const toggleSortDropdown = () => {
  showSortDropdown.value = !showSortDropdown.value
  showFilterDropdown.value = false
  openSortSelect.value = null
}

const handleOutsideClick = (e) => {
  if (!e.target.closest('.js-toolbar-popup-scope')) {
    showFilterDropdown.value = false
    showSortDropdown.value = false
    openSortSelect.value = null
  }
}

onMounted(() => {
  document.addEventListener('click', handleOutsideClick)
})

onBeforeUnmount(() => {
  document.removeEventListener('click', handleOutsideClick)
})

// Filter Bar configuration
const activeFilters = ref([])

const noteCreator = note => note.createdByName || note.creatorName || note.createdBy?.fullName || note.createdBy?.name || note.createdById || ''

const stickyFilterFields = computed(() => {
  const creators = Array.from(new Set((stickyStore.notes || []).map(noteCreator).filter(Boolean))).sort()
  const colors = Array.from(new Set((stickyStore.notes || []).map(n => n.color).filter(Boolean))).sort()
  
  return [
    { key: 'status', label: 'Trạng thái', icon: 'fa-regular fa-note-sticky', values: ['Pinned', 'Unpinned'] },
    { key: 'creator', label: 'Người tạo', icon: 'fa-solid fa-user-pen', values: creators },
    { key: 'color', label: 'Màu sắc', icon: 'fa-solid fa-palette', values: colors }
  ]
})

const stickyOperators = {
  status: ['is', 'is not'],
  creator: ['is', 'is not'],
  color: ['is', 'is not']
}

const customStickyValueMeta = (fieldKey, value) => {
  if (fieldKey === 'status') {
    if (value === 'Pinned') return { icon: 'fa-solid fa-thumbtack', color: '#f59e0b' }
    return { icon: 'fa-regular fa-note-sticky', color: '#94a3b8' }
  }
  if (fieldKey === 'creator') {
    return { icon: 'fa-solid fa-user', color: 'var(--color-text-secondary)' }
  }
  if (fieldKey === 'color') {
    return { icon: 'fa-solid fa-square', color: value }
  }
  return null
}

// Sorting config
const stickySortDirection = ref('desc')
const stickySortBy = ref('updatedAt')
const sortSearchQuery = ref('')

const stickySortOptions = [
  { value: 'updatedAt', label: 'Cập nhật gần nhất', icon: 'fa-regular fa-clock' },
  { value: 'createdAt', label: 'Mới tạo gần nhất', icon: 'fa-regular fa-calendar-plus' },
  { value: 'title', label: 'Tiêu đề', icon: 'fa-solid fa-font' },
  { value: 'creator', label: 'Người tạo', icon: 'fa-solid fa-user-pen' },
  { value: 'pinned', label: 'Trạng thái ghim', icon: 'fa-solid fa-thumbtack' },
  { value: 'color', label: 'Màu sắc', icon: 'fa-solid fa-palette' }
]

const filteredStickySortOptions = computed(() => {
  const q = sortSearchQuery.value.trim().toLowerCase()
  if (!q) return stickySortOptions
  return stickySortOptions.filter(o => o.label.toLowerCase().includes(q))
})

// Combined Display List
const displayNotes = computed(() => {
  let list = [...(stickyStore.notes || [])]
  
  if (activeFilters.value.length > 0) {
    list = list.filter(note => {
      return activeFilters.value.every(f => {
        let val = ''
        if (f.field === 'status') {
          val = note.isPinned ? 'Pinned' : 'Unpinned'
        } else if (f.field === 'creator') {
          val = noteCreator(note)
        } else if (f.field === 'color') {
          val = note.color
        }
        const isMatch = `${val || ''}`.toLowerCase() === `${f.value || ''}`.toLowerCase()
        return f.operator === 'is' ? isMatch : !isMatch
      })
    })
  }

  list.sort((left, right) => {
    const leftSortValue = stickySortBy.value === 'creator' ? noteCreator(left) : (stickySortBy.value === 'pinned' ? left.isPinned : left[stickySortBy.value])
    const rightSortValue = stickySortBy.value === 'creator' ? noteCreator(right) : (stickySortBy.value === 'pinned' ? right.isPinned : right[stickySortBy.value])
    
    const l = ['title', 'creator', 'color'].includes(stickySortBy.value)
      ? `${leftSortValue || ''}`.toLowerCase()
      : (stickySortBy.value === 'pinned' ? Number(leftSortValue) : new Date(leftSortValue || 0).getTime())
    const r = ['title', 'creator', 'color'].includes(stickySortBy.value)
      ? `${rightSortValue || ''}`.toLowerCase()
      : (stickySortBy.value === 'pinned' ? Number(rightSortValue) : new Date(rightSortValue || 0).getTime())
    
    const result = l < r ? -1 : (l > r ? 1 : 0)
    return stickySortDirection.value === 'desc' ? -result : result
  })
  
  return list
})

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

.search-field { position: relative; width: min(260px, 30vw); min-height: 34px; display: flex; align-items: center; gap: 0; padding: 0; border: 0; border-radius: 9px; color: var(--color-text-muted); background: transparent; box-shadow: none; }
.search-field > i { position: absolute; left: 12px; z-index: 1; color: var(--color-text-muted); font-size: 14px; }
.search-field input { box-sizing: border-box !important; width: 100%; height: 34px !important; min-height: 34px !important; border: 1px solid var(--color-border) !important; border-radius: 9px !important; outline: 0; padding-left: 36px !important; padding-right: 12px !important; background: var(--color-surface) !important; color: var(--color-text-primary) !important; font-size: 13.5px !important; transition: border-color 0.2s, box-shadow 0.2s; }
.search-field input:focus { border-color: var(--color-accent) !important; box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15) !important; }
.page-state button, .load-more {
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
.toolbar-count { color: var(--color-text-muted); font-size: 11px; white-space: nowrap; }

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

/* Popover/dropdown menu custom styling */
.filter-dropdown-wrapper,
.display-dropdown-wrapper {
  position: relative;
  display: inline-block;
}

.plane-dropdown-menu {
  position: absolute;
  top: 100%;
  right: 0;
  margin-top: 8px;
  background: var(--color-surface-elevated);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  width: 260px;
  max-height: min(450px, calc(100vh - 180px));
  overflow-y: auto;
  box-shadow: var(--shadow-popover);
  z-index: 120;
  color: var(--color-text-primary);
  font-size: 13px;
  padding: 8px;
}

.filter-dropdown-menu {
  left: 0;
  right: auto;
  width: 640px;
  max-width: calc(100vw - 32px);
  max-height: none;
  overflow: visible;
  padding: 8px !important;
}

:deep(.filter-dropdown-menu .filter-bar-container) {
  border: none;
  background: transparent;
  padding: 0 !important;
  min-height: auto;
  box-shadow: none;
  overflow: visible;
}

/* Sort / Combobox inside popup */
.filter-combobox {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 5px;
  width: 100%;
}
.filter-label {
  display: flex;
  color: var(--color-text-secondary);
  font-size: 11px;
  font-weight: 750;
  letter-spacing: 0.02em;
  text-transform: uppercase;
}
.filter-select-trigger {
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 10px;
  width: 100%;
  height: 36px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  color: var(--color-text-primary);
  padding: 0 12px;
  outline: none;
  font-size: 13px;
  cursor: pointer;
  transition: border-color 0.15s ease, background-color 0.15s ease;
}
.filter-select-trigger:hover,
.filter-select-trigger.active {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  background: color-mix(in srgb, var(--color-accent) 6%, var(--color-surface)) !important;
  color: var(--color-accent) !important;
  box-shadow: none !important;
}
.sort-combobox-trigger:hover,
.sort-combobox-trigger.active {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  background: color-mix(in srgb, var(--color-accent) 6%, var(--color-surface)) !important;
  color: var(--color-accent) !important;
  box-shadow: none !important;
}
.filter-select-trigger:hover > i,
.filter-select-trigger.active > i,
.sort-combobox-trigger:hover i,
.sort-combobox-trigger.active i {
  color: var(--color-accent) !important;
}
.filter-select-trigger:hover > span,
.filter-select-trigger.active > span,
.sort-combobox-trigger:hover span,
.sort-combobox-trigger.active span {
  color: var(--color-accent) !important;
}
.filter-select-trigger span {
  flex: 1;
  text-align: left;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.filter-select-trigger i {
  color: var(--color-text-secondary);
}
.filter-select-menu {
  position: absolute;
  left: 0;
  right: 0;
  top: calc(100% + 4px);
  z-index: 120;
  max-height: 220px;
  overflow-y: auto;
  padding: 6px !important;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface-elevated);
  box-shadow: var(--shadow-popover);
  display: flex;
  flex-direction: column;
  gap: 0 !important;
}
.filter-select-option {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 8px;
  width: 100%;
  min-height: 32px !important;
  padding: 5px 9px !important;
  margin: 0 !important;
  border: 0;
  border-left: 4px solid transparent !important;
  border-radius: 8px !important;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 13px;
  font-weight: 500;
  text-align: left;
  cursor: pointer;
  transition: background-color 0.15s ease, border-color 0.15s ease, color 0.15s ease;
}
.filter-select-option:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}
.filter-select-option.selected {
  background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface)) !important;
  border-left-color: var(--color-accent) !important;
  border-radius: 8px !important;
  color: var(--color-accent);
  font-weight: 650;
}
.filter-select-option.selected:hover {
  background: color-mix(in srgb, var(--color-accent) 18%, var(--color-surface)) !important;
  color: var(--color-accent);
}
.filter-select-option > span {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  text-align: left;
}
.filter-select-option > i:first-child {
  width: 15px;
  color: currentColor;
  font-size: 12px;
  text-align: center;
}

/* Sort Search field styling matching FilterBar */
.filter-search-field {
  position: relative;
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  min-height: 34px;
  height: 34px;
  box-sizing: border-box;
  border: 1px solid var(--color-border);
  border-radius: 9px;
  background: var(--color-surface);
  padding: 0 12px;
  color: var(--color-text-muted);
  transition: border-color 0.2s, box-shadow 0.2s;
}
.filter-search-icon {
  position: static;
  transform: none;
  width: 16px;
  height: 16px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: 0 0 16px;
  font-size: 14px;
  pointer-events: none;
  color: var(--color-text-muted);
}
.filter-search-input {
  width: 100% !important;
  height: 100% !important;
  box-sizing: border-box !important;
  min-width: 0 !important;
  border: 0 !important;
  border-radius: 0 !important;
  background: transparent !important;
  color: var(--color-text-primary) !important;
  padding: 0 !important;
  outline: none !important;
  font-size: 13.5px !important;
  line-height: 34px !important;
  text-indent: 0 !important;
  appearance: none;
}
.filter-search-input::placeholder {
  color: var(--color-text-muted);
}
.filter-search-field:focus-within {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.14);
}

/* Mini direction buttons next to selected sort item */
.dir-mini-btn {
  width: 30px;
  min-width: 30px;
  height: 30px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: all 0.15s ease;
}
.dir-mini-btn:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}
.dir-mini-btn.active {
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important;
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  color: var(--color-accent) !important;
  font-weight: 600 !important;
}

@media (max-width: 700px) {
  .page-header { align-items: stretch; flex-direction: column; padding: 16px; }
  .primary-action { width: 100%; }
  .stickies-page .page-content { padding: 12px !important; }
  .notes-grid { grid-template-columns: 1fr; }
}

/* Empty State Styles */
.empty-spaces-flat {
  min-height: 204px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 24px 26px;
  background: transparent;
  border: 0;
  box-shadow: none;
  text-align: center;
}

.empty-spaces-icon {
  width: 54px;
  height: 54px;
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid color-mix(in srgb, var(--color-accent) 18%, transparent);
  border-radius: 14px;
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface));
  color: var(--color-accent);
  font-size: 23px;
  box-shadow: 0 14px 30px rgba(14, 165, 233, 0.12);
}

.empty-spaces-copy {
  max-width: 380px;
}

.empty-spaces-copy h3 {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 15px;
  font-weight: 800;
  line-height: 1.35;
}

.empty-spaces-copy p {
  margin: 3px 0 0;
  color: var(--color-text-muted);
  font-size: 13px;
  line-height: 1.4;
}

.empty-spaces-btn {
  height: 36px;
  padding: 0 16px;
  border-radius: 9px;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-primary);
  font-size: 13.5px;
  font-weight: 600;
  cursor: pointer;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
  transition: all 0.2s ease;
  margin-top: 12px;
}

.empty-spaces-btn:hover {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border));
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface));
  color: var(--color-accent);
}
</style>
