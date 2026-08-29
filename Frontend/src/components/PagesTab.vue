<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import { ElMessageBox, ElNotification, ElMessage } from 'element-plus'
import { EditorContent, useEditor } from '@tiptap/vue-3'
import { useActivityStore } from '@/store/useActivityStore'
import { useI18nStore } from '@/store/useI18nStore'
import { signalRService } from '@/api/signalrService'

const actStore = useActivityStore()
const i18nStore = useI18nStore()
const t = (key) => i18nStore.t(key)
import StarterKit from '@tiptap/starter-kit'
import Image from '@tiptap/extension-image'
import Underline from '@tiptap/extension-underline'
import TextAlign from '@tiptap/extension-text-align'
import Placeholder from '@tiptap/extension-placeholder'
import { TextStyle } from '@tiptap/extension-text-style'
import { Color } from '@tiptap/extension-color'
import { TaskItem } from '@tiptap/extension-task-item'
import { TaskList } from '@tiptap/extension-task-list'
import { Table } from '@tiptap/extension-table'
import { TableRow } from '@tiptap/extension-table-row'
import { TableCell } from '@tiptap/extension-table-cell'
import { TableHeader } from '@tiptap/extension-table-header'
import ProjectPageContainer from '@/components/common/ProjectPageContainer.vue'
import ProjectPageHeader from '@/components/common/ProjectPageHeader.vue'
import ProjectPageToolbar from '@/components/common/ProjectPageToolbar.vue'
import ToolbarSortMenu from '@/components/common/ToolbarSortMenu.vue'
import ProjectEmptyState from '@/components/common/ProjectEmptyState.vue'
import { getStoredUserSession } from '@/utils/authSession'

const props = defineProps({
  projectId: { type: String, required: true }
})

const route = useRoute()
const router = useRouter()

const spaceName = 'CYBWF'
const pages = ref([])
const activePage = ref(null)
const loading = ref(false)
const saving = ref(false)
const activeTab = ref('Public')
const sortMode = ref('updated')
const textColor = ref('var(--color-text-muted)')
const activePreviewColor = ref('var(--color-text-muted)')
const pendingColor = ref('var(--color-text-muted)')
const colorPickerVisible = ref(false)
const tablePickerVisible = ref(false)
const hoverTableRows = ref(0)
const hoverTableCols = ref(0)
const imageFileInput = ref(null)
const stagedImages = ref([]) // { id, src, name }
const showImageStaging = ref(false)

function normalizePageContent(content) {
  if (!content) return ''
  if (typeof content === 'object') return content
  if (typeof content !== 'string') return ''
  const trimmed = content.trim()
  if (!trimmed.startsWith('{') && !trimmed.startsWith('[')) return content
  try {
    const parsed = JSON.parse(trimmed)
    return parsed && typeof parsed === 'object' ? parsed : content
  } catch {
    return content
  }
}

function searchablePageContent(content) {
  const normalized = normalizePageContent(content)
  if (typeof normalized === 'string') return normalized
  const text = []
  const visit = (node) => {
    if (!node || typeof node !== 'object') return
    if (typeof node.text === 'string') text.push(node.text)
    if (Array.isArray(node.content)) node.content.forEach(visit)
  }
  visit(normalized)
  return text.join(' ')
}

let saveTimeout = null

const sortBy = ref('date_modified')
const sortOrder = ref('desc')
const pageSortOptions = [
  { value: 'name', label: 'Name', icon: 'fa-solid fa-font' },
  { value: 'date_created', label: 'Date created', icon: 'fa-regular fa-calendar-plus' },
  { value: 'date_modified', label: 'Date modified', icon: 'fa-regular fa-clock' }
]
const filterSearch = ref('')
const pageViewMode = ref('list')
const filterFavorites = ref(false)
const createdDateWindow = ref('')
const createdByMe = ref(false)
const filterDateExpanded = ref(true)
const filterByExpanded = ref(true)

const filteredPages = computed(() => {
  let result = []
  if (activeTab.value === 'Archived') {
    result = pages.value.filter(p => p.isArchived)
  } else if (activeTab.value === 'Private') {
    result = pages.value.filter(p => p.isPrivate && !p.isArchived)
  } else {
    result = pages.value.filter(p => !p.isPrivate && !p.isArchived)
  }

  // Filter by search query
  if (filterSearch.value.trim()) {
    const q = filterSearch.value.toLowerCase()
    result = result.filter(p =>
      (p.title || '').toLowerCase().includes(q) ||
      searchablePageContent(p.content).toLowerCase().includes(q)
    )
  }

  if (filterFavorites.value) result = result.filter(page => Boolean(page.isFavorite || page.starred))
  if (createdDateWindow.value) {
    const cutoff = Date.now() - Number(createdDateWindow.value) * 24 * 60 * 60 * 1000
    result = result.filter(page => new Date(page.createdAt || 0).getTime() >= cutoff)
  }
  if (createdByMe.value) {
    const user = getStoredUserSession() || {}
    const userId = user.id || user.userId
    result = result.filter(page => `${page.createdById || page.createdByUserId || ''}` === `${userId || ''}`)
  }

  const sorted = [...result]
  sorted.sort((left, right) => {
    let comparison = 0
    if (sortBy.value === 'name') {
      comparison = (left.title || 'Untitled').localeCompare(right.title || 'Untitled')
    } else if (sortBy.value === 'date_created') {
      const leftTime = new Date(left.createdAt || 0).getTime()
      const rightTime = new Date(right.createdAt || 0).getTime()
      comparison = leftTime - rightTime
    } else {
      const leftTime = new Date(left.updatedAt || left.createdAt || 0).getTime()
      const rightTime = new Date(right.updatedAt || right.createdAt || 0).getTime()
      comparison = leftTime - rightTime
    }
    return sortOrder.value === 'desc' ? -comparison : comparison
  })
  return sorted
})

const editor = useEditor({
  content: '',
  extensions: [
    StarterKit,
    Image,
    Underline,
    TextStyle,
    Color,
    TaskList,
    TaskItem.configure({ nested: true }),
    Table.configure({ resizable: true }),
    TableRow,
    TableHeader,
    TableCell,
    TextAlign.configure({ types: ['heading', 'paragraph'] }),
    Placeholder.configure({
      placeholder: t("Press '/' for commands...")
    })
  ],
  editorProps: {
    attributes: {
      class: 'pages-editor prose prose-invert prose-sm max-w-none focus:outline-none'
    }
  },
  onUpdate: ({ editor: currentEditor }) => {
    if (!activePage.value || activePage.value.isLocked) return
    if (saveTimeout) clearTimeout(saveTimeout)
    saving.value = true
    saveTimeout = setTimeout(() => {
      activePage.value.content = JSON.stringify(currentEditor.getJSON())
      void savePage()
    }, 900)
  }
})

const handlePageRealtime = event => {
  if (`${event?.projectId}` !== `${props.projectId}` || event?.entityType !== 'page') return
  if (event.action === 'deleted') {
    pages.value = pages.value.filter(page => `${page.id}` !== `${event.entityId}`)
    if (`${activePage.value?.id}` === `${event.entityId}`) activePage.value = null
    return
  }
  if (!event.data?.id) return
  const index = pages.value.findIndex(page => `${page.id}` === `${event.data.id}`)
  if (index >= 0) pages.value[index] = { ...pages.value[index], ...event.data }
  else pages.value.unshift(event.data)
  if (`${activePage.value?.id}` === `${event.data.id}`) {
    activePage.value = { ...activePage.value, ...event.data }
    if (!saving.value && editor.value) {
      editor.value.commands.setContent(normalizePageContent(activePage.value.content), false)
    }
  }
}

onMounted(async () => {
  signalRService.on('EntityChanged', handlePageRealtime)
  await signalRService.startConnection(props.projectId)
  void fetchPages()
})

onBeforeUnmount(() => {
  if (saveTimeout) clearTimeout(saveTimeout)
  signalRService.off('EntityChanged', handlePageRealtime)
  if (editor.value) editor.value.destroy()
})

watch(
  () => route.query.pageId,
  async (pageId) => {
    if (!pageId) {
      activePage.value = null
      return
    }
    if (String(activePage.value?.id || '') === String(pageId)) return
    await openPage(pageId, false)
  }
)

async function fetchPages() {
  loading.value = true
  try {
    const res = await axiosClient.get(`/projects/${props.projectId}/pages`)
    // Note: Backend might need update to return archived pages for the Archived tab
    pages.value = res.data?.data || []
    const qPageId = route.query.pageId
    if (qPageId) {
       const exists = pages.value.some(p => String(p.id) === String(qPageId))
       if (exists) await openPage(qPageId, false)
    }
  } catch (error) {
    console.error(error)
    ElNotification({ title: 'Lỗi', message: 'Không thể tải danh sách trang', type: 'error' })
  } finally {
    loading.value = false
  }
}

async function createPage() {
  try {
    const res = await axiosClient.post(`/projects/${props.projectId}/pages`, {
      title: 'Untitled',
      content: ''
    })
    await fetchPages()
    const createdPageId = res.data?.data?.id
    if (createdPageId) await openPage(createdPageId)
    actStore.logActivity(`Created page "${res.data?.data?.title || 'Untitled'}"`, 'in Pages', 'fa-regular fa-file-lines')
    ElNotification({ title: 'Thành công', message: 'Tạo trang mới thành công', type: 'success' })
  } catch (error) {
    console.error(error)
    ElNotification({ title: 'Lỗi', message: 'Không thể tạo trang', type: 'error' })
  }
}

async function openPage(pageId, syncRoute = true) {
  loading.value = true
  try {
    const res = await axiosClient.get(`/projects/${props.projectId}/pages/${pageId}`)
    activePage.value = res.data?.data || null
    if (editor.value && activePage.value) {
      editor.value.commands.setContent(normalizePageContent(activePage.value.content), false)
      editor.value.setEditable(!activePage.value.isLocked)
    }
    if (syncRoute) {
      await router.replace({ query: { ...route.query, pageId: pageId } })
    }
  } catch (error) {
    console.error(error)
    ElNotification({ title: 'Lỗi', message: 'Không thể mở trang', type: 'error' })
  } finally {
    loading.value = false
  }
}

async function savePage() {
  if (!activePage.value) {
    saving.value = false
    return
  }
  const content = editor.value
    ? JSON.stringify(editor.value.getJSON())
    : JSON.stringify(normalizePageContent(activePage.value.content) || { type: 'doc', content: [] })
  activePage.value.content = content
  try {
    await axiosClient.put(`/projects/${props.projectId}/pages/${activePage.value.id}`, {
      title: activePage.value.title,
      content
    })
    patchPageInList(activePage.value.id, {
      title: activePage.value.title,
      content,
      updatedAt: new Date().toISOString()
    })
  } catch (error) {
    console.error(error)
    ElNotification({ title: 'Lỗi', message: 'Lỗi khi lưu trang', type: 'error' })
  } finally {
    saving.value = false
  }
}

function handleContentInput() {
  if (!activePage.value || activePage.value.isLocked) return
  if (saveTimeout) clearTimeout(saveTimeout)
  saving.value = true
  saveTimeout = setTimeout(() => {
    void savePage()
  }, 900)
}

async function updatePageState(page, payload, successMessage) {
  try {
    await axiosClient.put(`/projects/${props.projectId}/pages/${page.id}`, payload)
    patchPageInList(page.id, payload)
    if (activePage.value?.id === page.id) {
      activePage.value = { ...activePage.value, ...payload }
      if (Object.prototype.hasOwnProperty.call(payload, 'isLocked')) {
        editor.value?.setEditable(!payload.isLocked)
      }
    }
    if (successMessage) ElNotification({ title: 'Thành công', message: successMessage, type: 'success' })
  } catch (error) {
    console.error(error)
    ElNotification({ title: 'Lỗi', message: 'Không thể cập nhật trạng thái', type: 'error' })
  }
}

async function toggleLock(page) {
  const nextValue = !page.isLocked
  await updatePageState(page, { isLocked: nextValue }, nextValue ? 'Đã khóa trang' : 'Đã mở khóa trang')
}

async function togglePrivacy(page) {
  const nextValue = !page.isPrivate
  await updatePageState(page, { isPrivate: nextValue }, nextValue ? 'Đã chuyển sang Private' : 'Đã chuyển sang Public')
}

async function toggleStar(page) {
  const nextValue = !page.isStarred
  await updatePageState(page, { isStarred: nextValue }, nextValue ? 'Đã gắn sao' : 'Đã bỏ sao')
}

async function archivePage(page) {
  try {
    await ElMessageBox.confirm(
      page.isArchived ? 'Bạn có chắc muốn khôi phục trang này?' : 'Bạn có chắc muốn lưu trữ trang này?',
      'Xác nhận',
      { type: 'warning' }
    )
    await axiosClient.put(`/projects/${props.projectId}/pages/${page.id}/archive`)
    actStore.logActivity(page.isArchived ? 'Restored page' : 'Archived page', `Page: ${page.title}`, 'fa-regular fa-box-archive')
    if (activePage.value?.id === page.id && !page.isArchived) activePage.value = null
    await fetchPages()
    ElNotification({
      title: 'Thành công',
      message: page.isArchived ? 'Đã khôi phục trang' : 'Đã lưu trữ trang',
      type: 'success'
    })
  } catch (error) { if (error !== 'cancel') console.error(error) }
}

async function deletePage(page) {
  try {
    await ElMessageBox.confirm('Bạn có chắc muốn xóa vĩnh viễn trang này?', 'Xác nhận xóa', { type: 'error' })
    await axiosClient.delete(`/projects/${props.projectId}/pages/${page.id}`)
    if (activePage.value?.id === page.id) activePage.value = null
    await fetchPages()
    ElNotification({ title: 'Thành công', message: 'Đã xóa trang', type: 'success' })
  } catch (error) { if (error !== 'cancel') console.error(error) }
}

async function copyPage(page) {
  try {
    const detailRes = await axiosClient.get(`/projects/${props.projectId}/pages/${page.id}`)
    const fullPage = detailRes.data?.data || {}
    await axiosClient.post(`/projects/${props.projectId}/pages`, {
      title: `${fullPage.title || 'Untitled'} copied`,
      content: fullPage.content || ''
    })
    await fetchPages()
    ElNotification({ title: 'Thành công', message: 'Đã tạo bản sao', type: 'success' })
  } catch (error) {
    console.error(error)
    ElNotification({ title: 'Lỗi', message: 'Không thể tạo bản sao', type: 'error' })
  }
}

function patchPageInList(pageId, patch) {
  const index = pages.value.findIndex(p => String(p.id) === String(pageId))
  if (index !== -1) {
    pages.value[index] = { ...pages.value[index], ...patch, updatedAt: patch.updatedAt || new Date().toISOString() }
  }
}

function setHeading(level) {
  if (!editor.value) return
  if (level === 'p') {
    editor.value.chain().focus().setParagraph().run()
  } else {
    editor.value.chain().focus().toggleHeading({ level: Number(level) }).run()
  }
}

function applyColor() {
  if (!editor.value) return
  const color = pendingColor.value
  activePreviewColor.value = color
  editor.value.chain().focus().setColor(color).run()
  colorPickerVisible.value = false
}

function selectPendingColor(c) {
  pendingColor.value = c
}

function insertTable(rows, cols) {
  if (!editor.value) return
  editor.value.chain().focus().insertTable({ rows, cols, withHeaderRow: true }).run()
  tablePickerVisible.value = false
  ElMessage.success(`Inserted ${rows}x${cols} table`)
}

async function handleImageUpload(e) {
  const files = e.target.files
  if (!files || !files.length) return

  for (const file of Array.from(files)) {
    const reader = new FileReader()
    reader.onload = (event) => {
      stagedImages.value.push({
        id: Date.now() + Math.random(),
        src: event.target.result,
        name: file.name
      })
    }
    reader.readAsDataURL(file)
  }
  showImageStaging.value = true
  if (imageFileInput.value) imageFileInput.value.value = ''
}

function removeStagedImage(id) {
  stagedImages.value = stagedImages.value.filter(img => img.id !== id)
  if (stagedImages.value.length === 0) showImageStaging.value = false
}

function insertStagedImages() {
  if (!editor.value) return
  stagedImages.value.forEach(img => {
    editor.value.chain().focus().setImage({ src: img.src }).run()
  })
  actStore.logActivity(`Inserted ${stagedImages.value.length} images`, `Page: ${activePage.value.title}`, 'fa-regular fa-image')
  stagedImages.value = []
  showImageStaging.value = false
}

async function handlePageCommand(command) {
  const { action, page } = command
  if (action === 'open') {
    const target = router.resolve({ query: { pageId: page.id } })
    window.open(target.href, '_blank')
  } else if (action === 'copy-link') {
    const url = new URL(window.location.href)
    url.searchParams.set('pageId', page.id)
    await navigator.clipboard.writeText(url.toString())
    ElNotification({ title: 'Thành công', message: 'Đã copy link', type: 'success' })
  } else if (action === 'copy') await copyPage(page)
  else if (action === 'lock') await toggleLock(page)
  else if (action === 'privacy') await togglePrivacy(page)
  else if (action === 'archive') await archivePage(page)
  else if (action === 'delete') await deletePage(page)
}

function getPageAvatar(page) {
  const name = page.createdByName || page.title || 'P'
  return name.charAt(0).toUpperCase()
}

function formatTimestamp(value) {
  if (!value) return 'No date'
  return new Intl.DateTimeFormat('vi-VN', { month: 'short', day: 'numeric', year: 'numeric' }).format(new Date(value))
}

function pageMenuItems(page) {
  if (page.isArchived) {
    return [
      { label: 'Open in new tab', action: 'open', icon: 'fa-solid fa-up-right-from-square' },
      { label: 'Copy link', action: 'copy-link', icon: 'fa-solid fa-link' },
      { label: 'Make a copy', action: 'copy', icon: 'fa-regular fa-copy' },
      { label: 'Restore', action: 'archive', icon: 'fa-solid fa-rotate-left' },
      { label: 'Delete', action: 'delete', icon: 'fa-regular fa-trash-can' }
    ]
  }
  return [
    { label: 'Open in new tab', action: 'open', icon: 'fa-solid fa-up-right-from-square' },
    { label: 'Copy link', action: 'copy-link', icon: 'fa-solid fa-link' },
    { label: 'Make a copy', action: 'copy', icon: 'fa-regular fa-copy' },
    { label: page.isLocked ? 'Unlock' : 'Lock', action: 'lock', icon: page.isLocked ? 'fa-solid fa-unlock' : 'fa-solid fa-lock' },
    { label: page.isPrivate ? 'Make public' : 'Make private', action: 'privacy', icon: page.isPrivate ? 'fa-solid fa-globe' : 'fa-solid fa-eye-slash' },
    { label: 'Archive', action: 'archive', icon: 'fa-solid fa-box-archive' }
  ]
}
</script>
<template>
  <ProjectPageContainer>
    <!-- LIST VIEW -->
      <template v-if="!activePage">
        <ProjectPageHeader
          icon="fa-regular fa-file-lines"
          :title="t('Pages')"
          :description="t('Tạo và quản lý tài liệu dự án')"
        >
          <template #actions>
            <button class="nexus-btn-primary" @click="createPage"><i class="fa-solid fa-plus"></i> {{ t('Add page') }}</button>
          </template>
        </ProjectPageHeader>

        <ProjectPageToolbar
          :showSearch="true"
          v-model:searchQuery="filterSearch"
          :searchPlaceholder="t('Search pages...')"
        >
          <template #filters>
            <el-popover v-if="false" placement="bottom-end" trigger="click" :width="220" popper-class="custom-dark-popover sort-popover" :offset="8" :show-arrow="false" class="toolbar-sort-control">
              <template #reference>
                <button class="nexus-btn-outlined icon-only-trigger" :title="t('Sort')" :aria-label="t('Sort')">
                  <i class="fa-solid fa-arrow-down-short-wide"></i>
                </button>
              </template>
              <div class="popover-menu-list">
                <div class="pm-item" @click="sortBy = 'name'">
                  <span class="pm-label">{{ t('Name') }}</span>
                  <i v-if="sortBy === 'name'" class="fa-solid fa-check pm-check"></i>
                </div>
                <div class="pm-item" @click="sortBy = 'date_created'">
                  <span class="pm-label">{{ t('Date created') }}</span>
                  <i v-if="sortBy === 'date_created'" class="fa-solid fa-check pm-check"></i>
                </div>
                <div class="pm-item" @click="sortBy = 'date_modified'">
                  <span class="pm-label">{{ t('Date modified') }}</span>
                  <i v-if="sortBy === 'date_modified'" class="fa-solid fa-check pm-check"></i>
                </div>
                <div class="pm-divider"></div>
                <div class="pm-item" @click="sortOrder = 'asc'">
                  <span class="pm-label">{{ t('Ascending') }}</span>
                  <i v-if="sortOrder === 'asc'" class="fa-solid fa-check pm-check"></i>
                </div>
                <div class="pm-item" @click="sortOrder = 'desc'">
                  <span class="pm-label">{{ t('Descending') }}</span>
                  <i v-if="sortOrder === 'desc'" class="fa-solid fa-check pm-check"></i>
                </div>
              </div>
            </el-popover>

            <el-popover placement="bottom-end" trigger="click" :width="280" popper-class="custom-dark-popover filter-popover" :offset="8" :show-arrow="false" class="toolbar-filter-control">
              <template #reference>
                <button class="nexus-btn-outlined icon-only-trigger" :title="t('Filters')" :aria-label="t('Filters')"><i class="fa-solid fa-bars-staggered"></i></button>
              </template>
              <div class="filter-menu-container">
                <div class="fm-search">
                  <i class="fa-solid fa-magnifying-glass fms-icon"></i>
                  <input v-model="filterSearch" type="text" :placeholder="t('Search')" class="fms-input" />
                </div>

                <div class="fm-section" style="margin-top: 12px">
                  <label class="fm-checkbox-row">
                    <input type="checkbox" v-model="filterFavorites" class="fm-checkbox" />
                    <span class="fm-checkbox-label">{{ t('Favorites') }}</span>
                  </label>
                </div>

                <div class="pm-divider"></div>

                <div class="fm-collapsible">
                  <div class="fm-col-header" @click="filterDateExpanded = !filterDateExpanded">
                    <span class="fm-col-title">{{ t('Created date') }}</span>
                    <i class="fa-solid fa-chevron-up fm-col-icon" :class="{'rotate-180': !filterDateExpanded}"></i>
                  </div>
                  <div class="fm-col-content" v-show="filterDateExpanded">
                    <label class="fm-checkbox-row">
                      <input type="radio" name="page-created-date" value="7" v-model="createdDateWindow" class="fm-checkbox" />
                      <span class="fm-checkbox-label">{{ t('1 week ago') }}</span>
                    </label>
                    <label class="fm-checkbox-row">
                      <input type="radio" name="page-created-date" value="14" v-model="createdDateWindow" class="fm-checkbox" />
                      <span class="fm-checkbox-label">{{ t('2 weeks ago') }}</span>
                    </label>
                    <label class="fm-checkbox-row">
                      <input type="radio" name="page-created-date" value="30" v-model="createdDateWindow" class="fm-checkbox" />
                      <span class="fm-checkbox-label">{{ t('1 month ago') }}</span>
                    </label>
                    <label class="fm-checkbox-row">
                      <input type="radio" name="page-created-date" value="" v-model="createdDateWindow" class="fm-checkbox" />
                      <span class="fm-checkbox-label">{{ t('Custom') }}</span>
                    </label>
                  </div>
                </div>

                <div class="pm-divider"></div>

                <div class="fm-collapsible">
                  <div class="fm-col-header" @click="filterByExpanded = !filterByExpanded">
                    <span class="fm-col-title">{{ t('Created by') }}</span>
                    <i class="fa-solid fa-chevron-up fm-col-icon" :class="{'rotate-180': !filterByExpanded}"></i>
                  </div>
                  <div class="fm-col-content" v-show="filterByExpanded">
                    <label class="fm-checkbox-row">
                      <input type="checkbox" v-model="createdByMe" class="fm-checkbox" />
                      <div class="fm-user">
                        <div class="fm-avatar">D</div>
                        <span>{{ t('You') }}</span>
                      </div>
                    </label>
                  </div>
                </div>
              </div>
            </el-popover>
          </template>
          <template #sort>
            <ToolbarSortMenu v-model="sortBy" v-model:direction="sortOrder" label="Sort pages" :options="pageSortOptions" />
          </template>
          <template #toggles>
            <div class="view-toggles">
              <button class="toggle-btn" :class="{ active: pageViewMode === 'list' }" type="button" title="List view" @click="pageViewMode = 'list'"><i class="fa-solid fa-bars"></i></button>
              <button class="toggle-btn" :class="{ active: pageViewMode === 'grid' }" type="button" title="Grid view" @click="pageViewMode = 'grid'"><i class="fa-solid fa-table-cells-large"></i></button>
            </div>
          </template>
        </ProjectPageToolbar>

      <div class="pages-nav">
        <div class="nav-tab" :class="{ 'active': activeTab === 'Public' }" @click="activeTab = 'Public'">{{ t('Public') }}</div>
        <div class="nav-tab" :class="{ 'active': activeTab === 'Private' }" @click="activeTab = 'Private'">{{ t('Private') }}</div>
        <div class="nav-tab" :class="{ 'active': activeTab === 'Archived' }" @click="activeTab = 'Archived'">{{ t('Archived') }}</div>
      </div>

      <div class="pages-list" :class="{ 'pages-grid': pageViewMode === 'grid' }" v-loading="loading">
        <ProjectEmptyState
           v-if="filteredPages.length === 0"
           icon="fa-regular fa-file-lines"
           :title="activeTab === 'Archived' ? t('No archived pages yet') : t('No pages yet')"
           :description="activeTab === 'Archived' ? t('Archive pages not on your radar. Access them here when needed.') : t('Create your first page to get started and keep your work organized.')"
        />

        <div v-for="page in filteredPages" :key="page.id" class="page-row" @click="openPage(page.id)">
           <div class="pr-left">
              <i :class="page.icon || 'fa-regular fa-file-lines'" class="doc-icon" aria-hidden="true"></i>
              <span class="page-title">{{ page.title || t('Untitled') }}</span>
           </div>
            <div class="pr-right hover-actions-container" @click.stop>
              <div class="avatar-xxs">{{ getPageAvatar(page) }}</div>
              <el-tooltip placement="top" :content="page.isPrivate ? t('Private Page') : t('Public Page')">
                <button class="icon-btn action-hide" @click="togglePrivacy(page)">
                  <i v-if="page.isPrivate" class="fa-solid fa-user-secret text-amber-500"></i>
                  <i v-else class="fa-solid fa-globe text-blue-400"></i>
                </button>
              </el-tooltip>
              <el-tooltip placement="top" :content="t('Created on') + ' ' + formatTimestamp(page.createdAt)">
                <button class="icon-btn action-hide"><i class="fa-solid fa-circle-info"></i></button>
              </el-tooltip>
              <el-tooltip placement="top" :content="t('Star Page')">
                <button class="icon-btn action-hide" @click="toggleStar(page)">
                  <i :class="page.isStarred ? 'fa-solid fa-star text-yellow-500' : 'fa-regular fa-star'"></i>
                </button>
              </el-tooltip>
              <el-dropdown trigger="click" @command="handlePageCommand">
                <button class="icon-btn btn-box"><i class="fa-solid fa-ellipsis"></i></button>
                <template #dropdown>
                  <el-dropdown-menu class="pages-menu">
                    <el-dropdown-item v-for="item in pageMenuItems(page)" :key="item.action" :command="{ action: item.action, page }">
                      <i :class="item.icon"></i> <span>{{ t(item.label) }}</span>
                    </el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
           </div>
        </div>
      </div>
    </template>

    <!-- EDITOR VIEW -->
    <div v-else class="page-editor-view">
      <div class="editor-header">
        <div class="eh-left">
          <button class="back-btn" @click="activePage = null"><i class="fa-solid fa-chevron-left"></i></button>
          <div class="nexus-breadcrumb">
             <span class="text-muted">{{ spaceName }}</span>
             <i class="fa-solid fa-chevron-right text-[10px] text-muted mx-1"></i>
             <span class="text-muted">{{ t('Pages') }}</span>
             <i class="fa-solid fa-chevron-right text-[10px] text-muted mx-1"></i>
             <span class="font-semibold text-primary">{{ activePage.title || t('Untitled') }}</span>
          </div>
          <div class="flex items-center ml-4 px-2 py-0.5 rounded-full bg-surface-hover">
            <span v-if="saving" class="status-saving text-[11px]">{{ t('Saving...') }}</span>
            <span v-else class="status-saved text-[11px]"><i class="fa-solid fa-check text-green-500 mr-1"></i> {{ t('Saved') }}</span>
          </div>
        </div>
        <div class="eh-right">
           <button class="nexus-btn-outlined !h-8 !px-3" @click="toggleLock(activePage)">
             <i :class="activePage.isLocked ? 'fa-solid fa-lock text-amber-500' : 'fa-solid fa-unlock-keyhole'"></i>
             <span class="ml-2">{{ activePage.isLocked ? t('Locked') : t('Lock') }}</span>
           </button>
           <button class="nexus-btn-outlined !h-8 !px-3" @click="handlePageCommand({ action: 'copy-link', page: activePage })">
             <i class="fa-solid fa-link"></i>
             <span class="ml-2">{{ t('Share') }}</span>
           </button>
           <el-dropdown trigger="click">
             <button class="nexus-btn-icon !h-8 !w-8"><i class="fa-solid fa-ellipsis"></i></button>
             <template #dropdown>
               <el-dropdown-menu class="pages-menu">
                 <el-dropdown-item @click="toggleStar(activePage)">
                   <i :class="activePage.isStarred ? 'fa-solid fa-star text-yellow-500' : 'fa-regular fa-star'"></i>
                   <span>{{ activePage.isStarred ? t('Unstar') : t('Star') }}</span>
                 </el-dropdown-item>
                 <el-dropdown-item @click="handlePageCommand({ action: 'delete', page: activePage })">
                   <i class="fa-solid fa-trash text-red-500"></i>
                   <span class="text-red-500">{{ t('Delete') }}</span>
                 </el-dropdown-item>
               </el-dropdown-menu>
             </template>
           </el-dropdown>
        </div>
      </div>

      <div class="wysiwyg-toolbar-wrapper">
        <div class="wysiwyg-toolbar-container">
          <div class="wysiwyg-toolbar" v-if="editor">
            <el-dropdown trigger="click" @command="setHeading">
               <div class="tb-group hover-bg">{{ t('Text') }} <i class="fa-solid fa-chevron-down" style="font-size: 10px"></i></div>
               <template #dropdown>
                 <el-dropdown-menu class="plane-dropdown">
                    <el-dropdown-item command="p">{{ t('Paragraph') }}</el-dropdown-item>
                    <el-dropdown-item command="1">{{ t('Heading 1') }}</el-dropdown-item>
                    <el-dropdown-item command="2">{{ t('Heading 2') }}</el-dropdown-item>
                    <el-dropdown-item command="3">{{ t('Heading 3') }}</el-dropdown-item>
                 </el-dropdown-menu>
               </template>
            </el-dropdown>
            <div class="v-divider-tb"></div>

            <el-color-picker
              v-model="pendingColor"
              show-alpha
              :predefine="['#F44336','#E91E63','#9C27B0','#673AB7','#3F51B5','#2196F3','#03A9F4','#00BCD4','#009688','#4CAF50','#8BC34A','#CDDC39','#FFEB3B','#FFC107','#FF9800','#FF5722','#795548','#9E9E9E','#607D8B']"
              size="small"
              @change="applyColor"
              class="plane-color-picker"
            />
            <div class="v-divider-tb"></div>

            <div class="tb-group">
              <button class="tb-btn" :class="{ 'active': editor.isActive('bold') }" @click="editor.chain().focus().toggleBold().run()"><i class="fa-solid fa-bold"></i></button>
              <button class="tb-btn" :class="{ 'active': editor.isActive('italic') }" @click="editor.chain().focus().toggleItalic().run()"><i class="fa-solid fa-italic"></i></button>
              <button class="tb-btn" :class="{ 'active': editor.isActive('underline') }" @click="editor.chain().focus().toggleUnderline().run()"><i class="fa-solid fa-underline"></i></button>
            </div>
            <div class="v-divider-tb"></div>

            <div class="tb-group">
              <button class="tb-btn" @click="editor.chain().focus().setTextAlign('left').run()"><i class="fa-solid fa-align-left"></i></button>
              <button class="tb-btn" @click="editor.chain().focus().setTextAlign('center').run()"><i class="fa-solid fa-align-center"></i></button>
              <button class="tb-btn" @click="editor.chain().focus().setTextAlign('right').run()"><i class="fa-solid fa-align-right"></i></button>
            </div>
            <div class="v-divider-tb"></div>

            <div class="tb-group">
              <button class="tb-btn" @click="editor.chain().focus().toggleBulletList().run()"><i class="fa-solid fa-list-ul"></i></button>
              <button class="tb-btn" @click="editor.chain().focus().toggleOrderedList().run()"><i class="fa-solid fa-list-ol"></i></button>
              <button class="tb-btn" @click="editor.chain().focus().toggleTaskList().run()"><i class="fa-solid fa-list-check"></i></button>
            </div>
            <div class="v-divider-tb"></div>

            <div class="tb-group">
              <button class="tb-btn" @click="editor.chain().focus().toggleCodeBlock().run()"><i class="fa-solid fa-code"></i></button>
              <button class="tb-btn" @click="() => imageFileInput?.click()"><i class="fa-solid fa-image"></i></button>
              <input type="file" ref="imageFileInput" style="display: none" accept="image/*" multiple @change="handleImageUpload" />
              <el-popover v-model:visible="tablePickerVisible" trigger="click" placement="bottom" :width="200" popper-class="plane-popover dark">
                <template #reference>
                  <button class="tb-btn"><i class="fa-solid fa-table"></i></button>
                </template>
                <div class="p-2 bg-[#1B1C20] border border-[#27272A] rounded-lg">
                  <div class="text-[11px] text-gray-400 mb-2 text-center font-medium">{{ hoverTableRows || 0 }} x {{ hoverTableCols || 0 }} {{ t('Table') }}</div>
                  <div class="grid-picker" @mouseleave="hoverTableRows=0; hoverTableCols=0">
                    <div v-for="r in 10" :key="'r'+r" class="grid-row">
                      <div v-for="c in 10" :key="'r'+r+'c'+c"
                           class="grid-cell"
                           :class="{ 'active': r <= hoverTableRows && c <= hoverTableCols }"
                           @mouseover="hoverTableRows=r; hoverTableCols=c"
                           @click="insertTable(r, c)"></div>
                    </div>
                  </div>
                </div>
              </el-popover>
            </div>
          </div>
        </div>
      </div>

      <div class="editor-scroll-area">
        <div class="editor-content-container">
          <div class="canvas-inner">
            <div class="page-icon-fallback" :title="activePage.icon ? 'Biểu tượng trang' : 'Biểu tượng tài liệu mặc định'">
              <i :class="activePage.icon || 'fa-regular fa-file-lines'" aria-hidden="true"></i>
              <span>{{ activePage.icon ? 'Biểu tượng trang' : 'Tài liệu' }}</span>
            </div>
            <input class="editor-title-input" v-model="activePage.title" @input="handleContentInput" :placeholder="t('Untitled')" />
            <editor-content :editor="editor" class="editor-tiptap-content" />
          </div>
        </div>
      </div>

      <transition name="fade">
        <div v-if="showImageStaging" class="image-staging-overlay" @click.self="showImageStaging = false">
          <div class="image-staging-card">
            <div class="is-header">
              <div class="flex items-center gap-3">
                <i class="fa-regular fa-images text-blue-400"></i>
                <h3 class="text-white">{{ t('Selected Images') }} ({{ stagedImages.length }})</h3>
              </div>
              <div class="flex items-center gap-2">
                <button class="add-more-staging-btn" @click="() => imageFileInput?.click()">
                  <i class="fa-solid fa-plus"></i> {{ t('Add more') }}
                </button>
                <button @click="showImageStaging = false; stagedImages = []" class="close-staging"><i class="fa-solid fa-xmark"></i></button>
              </div>
            </div>
            <div class="is-body">
              <div v-for="img in stagedImages" :key="img.id" class="staged-item">
                <img :src="img.src" class="staged-img" />
                <button class="remove-staged-btn-fixed" @click="removeStagedImage(img.id)">
                  <i class="fa-solid fa-x"></i>
                </button>
                <div class="staged-name-bar">{{ img.name }}</div>
              </div>
            </div>
            <div class="is-footer">
              <button class="btn-cancel-staging" @click="showImageStaging = false; stagedImages = []">{{ t('Clear all') }}</button>
              <button class="btn-confirm-staging" @click="insertStagedImages">{{ t('Insert into page') }}</button>
            </div>
          </div>
        </div>
      </transition>
    </div>
  </ProjectPageContainer>
</template>

<style scoped>
.plane-pages-wrapper { min-height: 100vh; background: var(--color-bg); color: var(--color-text-primary); font-family: 'Inter', sans-serif; }

/* Header & Nav */
.pages-header { display: flex; justify-content: space-between; align-items: center; padding: 16px 24px; border-bottom: 1px solid var(--color-border); }
.ph-left { display: flex; align-items: center; font-size: 14px; font-weight: 500; }
.primary-action { background: #0EA5E9; color: white; border: none; padding: 6px 14px; border-radius: 6px; font-size: 13px; font-weight: 500; cursor: pointer; }
.primary-action:hover { background: #0284C7; }

.pages-nav { display: flex; gap: 24px; padding: 0 24px; border-bottom: 1px solid var(--color-border); }
.nav-tab { padding: 14px 0; font-size: 13px; font-weight: 500; color: var(--color-text-muted); cursor: pointer; border-bottom: 2px solid transparent; margin-bottom: -1px; }
.nav-tab.active { color: #38BDF8; border-bottom: 2px solid #38BDF8; }


/* List */
.pages-list { padding: 0 24px 24px; }
.pages-list.pages-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 12px; }
.pages-list.pages-grid .page-row { min-height: 132px; flex-direction: column; align-items: flex-start; justify-content: space-between; border: 1px solid var(--color-border); background: var(--color-surface); }
.pages-list.pages-grid .pr-left { width: 100%; align-items: flex-start; }
.pages-list.pages-grid .pr-right { width: 100%; justify-content: flex-end; }
.page-row { display: flex; justify-content: space-between; align-items: center; padding: 12px 16px; border-radius: 8px; cursor: pointer; transition: background 0.2s; border-bottom: 1px solid var(--color-surface); }
.page-row:hover { background: var(--color-surface); }
.pr-left { display: flex; align-items: center; gap: 12px; }
.doc-icon { color: var(--color-text-muted); font-size: 18px; }
.page-title { font-size: 14px; font-weight: 500; color: var(--color-text-primary); }
.pr-right { display: flex; align-items: center; gap: 14px; }

.avatar-xxs { width: 22px; height: 22px; border-radius: 50%; background: #0F766E; display: flex; align-items: center; justify-content: center; font-size: 10px; font-weight: 600; color: white; }
.icon-btn { background: transparent; border: none; color: var(--color-text-muted); font-size: 14px; cursor: pointer; display: flex; align-items: center; justify-content: center; }
.icon-btn:hover { color: var(--color-text-primary); }
.action-hide { opacity: 1; transition: opacity 0.2s; }
.btn-box { width: 28px; height: 28px; }

/* Editor View Refactoring */
.page-editor-view { display: flex; flex-direction: column; height: 100%; min-height: 0; background: var(--color-bg); overflow: hidden; }
.editor-header { display: flex; justify-content: space-between; align-items: center; padding: 12px 24px; border-bottom: 1px solid var(--color-border); background: var(--color-bg); z-index: 20; }
.eh-left { display: flex; align-items: center; gap: 12px; }
.eh-right { display: flex; align-items: center; gap: 8px; }
.back-btn { width: 32px; height: 32px; display: flex; align-items: center; justify-content: center; border-radius: 6px; border: 1px solid var(--color-border); background: var(--color-surface); color: var(--color-text-secondary); cursor: pointer; transition: all 0.2s; }
.back-btn:hover { background: var(--color-surface-hover); color: var(--color-text-primary); }
.status-saving { margin-left:16px; color:#F59E0B; font-size:12px; }
.status-saved { margin-left:16px; color:#10B981; font-size:12px; opacity:0.7; }

.wysiwyg-toolbar-wrapper { background: var(--color-bg); position: sticky; top: 0; z-index: 10; padding: 12px 0; border-bottom: 1px solid var(--color-border); }
.wysiwyg-toolbar-container { max-width: 720px; margin: 0 auto; width: 100%; padding: 0 16px; display: flex; justify-content: center; }
.wysiwyg-toolbar { display: flex; align-items: center; gap: 6px; background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 10px; padding: 6px 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.1); }
.tb-group { display: flex; align-items: center; gap: 2px; padding: 0 4px; }
.tb-btn { background: transparent; border: none; width: 28px; height: 28px; border-radius: 4px; color: var(--color-text-muted); display: flex; align-items: center; justify-content: center; cursor: pointer; transition: all 0.2s; }
.tb-btn:hover, .tb-btn.active { background: var(--color-surface-hover); color: var(--color-text-primary); }
.v-divider-tb { width:1px; height:20px; background:var(--color-border); margin:0 6px; }
.hover-bg:hover { background: var(--color-border); border-radius: 4px; }

.editor-scroll-area { flex: 1; overflow-y: auto; padding: 0; background: var(--color-bg); }
.editor-content-container { width: 100%; min-height: 100%; display: flex; justify-content: center; padding-top: 40px; padding-bottom: 80px; }
.canvas-inner { width: 100%; max-width: 720px; padding: 0 16px; }
.editor-title-input { background: transparent; border: none !important; font-size: 36px; font-weight: 700; color: var(--color-text-primary); outline: none !important; margin-bottom: 20px; width: 100%; letter-spacing: -0.03em; padding: 0 !important; height: auto !important; box-shadow: none !important; }
.editor-title-input::placeholder { color: var(--color-text-disabled); opacity: 0.3; }
.page-icon-fallback { color: var(--color-text-muted); font-size: 13px; display: flex; align-items: center; gap: 8px; margin-bottom: 12px; padding: 4px 0; }

.editor-tiptap-content :deep(.ProseMirror) { min-height: 400px; outline: none; font-size: 16px; line-height: 1.7; color: var(--color-text-primary); }
.editor-tiptap-content :deep(.ProseMirror p.is-editor-empty:first-child::before) { content: "Nhấn '/' để hiển thị lệnh..."; float: left; color: var(--color-text-disabled); pointer-events: none; height: 0; font-style: italic; opacity: 0.5; }
.editor-tiptap-content :deep(p) { margin-bottom: 1.2em; }
.editor-tiptap-content :deep(h1) { font-size: 2.2em; font-weight: 800; margin: 1.5em 0 0.8em; }
.editor-tiptap-content :deep(h2) { font-size: 1.8em; font-weight: 700; margin: 1.2em 0 0.6em; }
.editor-tiptap-content :deep(h3) { font-size: 1.4em; font-weight: 600; margin: 1em 0 0.5em; }
.editor-tiptap-content :deep(ul) { list-style-type: disc; margin-left: 1.5em; margin-bottom: 1em; }
.editor-tiptap-content :deep(ol) { list-style-type: decimal; margin-left: 1.5em; margin-bottom: 1em; }
.editor-tiptap-content :deep(table) { border-collapse: collapse; table-layout: fixed; width: 100%; margin: 1em 0; overflow: hidden; border: 1px solid var(--color-border); }
.editor-tiptap-content :deep(table td), .editor-tiptap-content :deep(table th) { min-width: 1em; border: 1px solid var(--color-border); padding: 6px 8px; vertical-align: top; box-sizing: border-box; }
.editor-tiptap-content :deep(table th) { font-weight: bold; text-align: left; background: var(--color-surface); }
.editor-tiptap-content :deep(ul[data-type="taskList"]) { list-style: none; padding: 0; }
.editor-tiptap-content :deep(ul[data-type="taskList"] li) { display: flex; gap: 8px; margin-bottom: 0.5em; }
.editor-tiptap-content :deep(img) { border-radius: 8px; max-width: 100%; border: 1px solid var(--color-border); margin: 1.5em 0; }

:deep(.pages-menu) { background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 10px; padding: 6px; box-shadow: var(--shadow-md); }
:deep(.el-dropdown-menu__item) { color: var(--color-text-primary); font-size: 13px; display: flex; align-items: center; gap: 10px; padding: 8px 12px; border-radius: 6px; }
:deep(.el-dropdown-menu__item:hover) { background: var(--color-surface-hover) !important; color: var(--color-accent) !important; }

/* Image Staging Styles */
.image-staging-overlay {
  position: fixed; top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(0,0,0,0.85); backdrop-filter: none; -webkit-backdrop-filter: none;
  z-index: 2000; display: flex; align-items: center; justify-content: center;
}
.image-staging-card {
  width: 500px; max-width: 90vw; background: #1B1C20; border: 1px solid var(--color-border);
  border-radius: 12px; display: flex; flex-direction: column; overflow: hidden;
  box-shadow: 0 25px 60px rgba(0,0,0,0.8);
}
.is-header { display: flex; justify-content: space-between; align-items: center; padding: 16px 20px; border-bottom: 1px solid var(--color-border); }
.is-header h3 { font-size: 16px; font-weight: 600; }
.close-staging { background: transparent; border: none; color: var(--color-text-muted); cursor: pointer; font-size: 18px; }
.is-body { padding: 20px; display: grid; grid-template-cols: repeat(auto-fill, minmax(100px, 1fr)); gap: 16px; max-height: 350px; overflow-y: auto; }
.staged-item { position: relative; border-radius: 6px; overflow: hidden; background: var(--color-bg); border: 1px solid var(--color-border); aspect-ratio: 1; display: flex; flex-direction: column; }
.staged-item img { width: 100%; height: 85px; object-fit: cover; }
.remove-staged { position: absolute; top: 2px; right: 2px; background: transparent; border: none; color: #F87171; cursor: pointer; font-size: 18px; text-shadow: 0 0 4px rgba(0,0,0,0.8); }
.is-footer { display: flex; justify-content: flex-end; gap: 12px; padding: 16px 20px; border-top: 1px solid var(--color-border); background: var(--color-surface); }
.btn-cancel-staging { background: transparent; border: 1px solid var(--color-border); color: var(--color-text-primary); padding: 8px 16px; border-radius: 6px; cursor: pointer; }
.btn-confirm-staging { background: #0EA5E9; border: none; color: white; padding: 8px 20px; border-radius: 6px; font-weight: 500; cursor: pointer; }
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

:deep(.plane-color-picker .el-color-picker__trigger) { border: 1px solid var(--color-border); background: transparent; border-radius: 4px; padding: 4px 8px; height: 32px; width: 44px; }
:deep(.plane-color-picker .el-color-picker__color) { border: none; }

/* Table Picker Styles */
.grid-picker { display: flex; flex-direction: column; gap: 3px; }
.grid-row { display: flex; gap: 3px; }
.grid-cell { width: 14px; height: 14px; border: 1px solid #3F3F46; border-radius: 2px; cursor: pointer; transition: all 0.1s; }
.grid-cell.active { background: #0EA5E9 !important; border-color: #38BDF8 !important; }

/* SprintA premium pages pass */
.pages-list-view {
  min-height: calc(100vh - 230px);
}

.pages-nav {
  gap: 8px;
  padding: 6px;
  border: 1px solid color-mix(in srgb, var(--color-border) 82%, transparent);
  border-radius: 14px;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 88%, transparent), color-mix(in srgb, var(--color-surface-hover) 46%, transparent));
  width: max-content;
  max-width: 100%;
}

.nav-tab {
  min-height: 38px;
  display: inline-flex;
  align-items: center;
  padding: 0 14px;
  border-radius: 10px;
  border-bottom: 0 !important;
  margin-bottom: 0;
  font-weight: 800;
}

.nav-tab.active {
  background: color-mix(in srgb, var(--color-accent) 15%, var(--color-surface));
  color: var(--color-accent);
  box-shadow: 0 10px 26px color-mix(in srgb, var(--color-accent) 12%, transparent);
}

.pages-list {
  margin-top: 12px;
  padding: 8px;
  border: 1px solid color-mix(in srgb, var(--color-border) 82%, transparent);
  border-radius: 18px;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 92%, transparent), color-mix(in srgb, var(--color-surface-hover) 40%, transparent));
  box-shadow: 0 18px 48px color-mix(in srgb, #020617 9%, transparent);
}

.page-row {
  border-bottom-color: color-mix(in srgb, var(--color-border) 65%, transparent);
}

.page-row:hover {
  background: color-mix(in srgb, var(--color-accent) 9%, var(--color-surface-hover));
}

.page-editor-view,
.editor-header {
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-bg) 78%, var(--color-surface)), var(--color-bg));
}
.pages-list-view,
.page-editor-view {
  min-height: 0 !important;
}

.pages-nav {
  gap: 6px !important;
  padding: 0 !important;
  border-bottom: none !important;
}

.nav-tab {
  padding: 9px 14px !important;
  border: 1px solid var(--color-border) !important;
  border-radius: 10px !important;
  background: color-mix(in srgb, var(--color-surface) 80%, transparent) !important;
  margin-bottom: 0 !important;
}

.nav-tab.active {
  background: color-mix(in srgb, var(--color-accent) 16%, var(--color-surface)) !important;
  border-color: color-mix(in srgb, var(--color-accent) 42%, var(--color-border)) !important;
}

.pages-list {
  padding: 12px 0 18px !important;
  overflow: visible !important;
}

.page-row {
  min-height: 52px !important;
  padding: 10px 14px !important;
  border: 1px solid color-mix(in srgb, var(--color-border) 76%, transparent) !important;
  border-radius: 12px !important;
  background: color-mix(in srgb, var(--color-surface) 82%, transparent) !important;
  margin-bottom: 8px !important;
}

.page-title {
  font-size: 13.5px !important;
}

.pr-right {
  gap: 8px !important;
}

.doc-icon,
.pr-right .icon-btn {
  width: 30px !important;
  height: 30px !important;
  border-radius: 9px !important;
  display: inline-flex !important;
  align-items: center !important;
  justify-content: center !important;
  border: 1px solid color-mix(in srgb, var(--color-border) 70%, transparent) !important;
  background: color-mix(in srgb, var(--color-surface-hover) 60%, transparent) !important;
  color: color-mix(in srgb, var(--color-accent) 72%, var(--color-text-secondary)) !important;
  font-size: 13px !important;
}

.avatar-xxs {
  width: 30px !important;
  height: 30px !important;
  border-radius: 9px !important;
}

.btn-box {
  background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface)) !important;
}

.editor-header,
.wysiwyg-toolbar-wrapper {
  padding-top: 8px !important;
  padding-bottom: 8px !important;
}
</style>
