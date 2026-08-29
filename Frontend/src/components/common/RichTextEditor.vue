<template>
  <div class="rich-text-editor-container" v-if="editor">
    <div v-if="$slots.header" class="rte-header-slot">
      <slot name="header"></slot>
    </div>
    <div class="rte-toolbar">
      <div class="toolbar-group">
        <el-dropdown trigger="click" @command="handleHeadingCommand">
          <button class="toolbar-btn text-style-btn">
            {{ currentHeadingLabel }} <i class="fa-solid fa-chevron-down ms-1"></i>
          </button>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="0">Normal text</el-dropdown-item>
              <el-dropdown-item command="1"><h1>Heading 1</h1></el-dropdown-item>
              <el-dropdown-item command="2"><h2>Heading 2</h2></el-dropdown-item>
              <el-dropdown-item command="3"><h3>Heading 3</h3></el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </div>
      <div class="toolbar-divider"></div>
      <div class="toolbar-group">
        <button class="toolbar-btn" :class="{ 'is-active': editor.isActive('bold') }" @click="editor.chain().focus().toggleBold().run()">
          <i class="fa-solid fa-bold"></i>
        </button>
        <button class="toolbar-btn" :class="{ 'is-active': editor.isActive('italic') }" @click="editor.chain().focus().toggleItalic().run()">
          <i class="fa-solid fa-italic"></i>
        </button>
        <button class="toolbar-btn" :class="{ 'is-active': editor.isActive('underline') }" @click="editor.chain().focus().toggleUnderline().run()">
          <i class="fa-solid fa-underline"></i>
        </button>
      </div>
      <div class="toolbar-divider"></div>
      <div class="toolbar-group">
        <el-color-picker v-model="textColor" @change="setTextColor" size="small" :predefine="predefineColors" />
      </div>
      <div class="toolbar-divider"></div>
      <div class="toolbar-group">
        <button class="toolbar-btn" :class="{ 'is-active': editor.isActive('bulletList') }" @click="editor.chain().focus().toggleBulletList().run()">
          <i class="fa-solid fa-list-ul"></i>
        </button>
        <button class="toolbar-btn" :class="{ 'is-active': editor.isActive('orderedList') }" @click="editor.chain().focus().toggleOrderedList().run()">
          <i class="fa-solid fa-list-ol"></i>
        </button>
        <button class="toolbar-btn" :class="{ 'is-active': editor.isActive('taskList') }" @click="editor.chain().focus().toggleTaskList().run()">
          <i class="fa-regular fa-square-check"></i>
        </button>
      </div>
      <div class="toolbar-divider"></div>
      <div class="toolbar-group">
        <button class="toolbar-btn" @click="setLink" :class="{ 'is-active': editor.isActive('link') }">
          <i class="fa-solid fa-link"></i>
        </button>
        <button class="toolbar-btn" @click="triggerFileUpload">
          <i class="fa-regular fa-image"></i>
        </button>
        <button class="toolbar-btn" @click="insertMention">
          <i class="fa-solid fa-at"></i>
        </button>
        <button class="toolbar-btn" @click="insertTable">
          <i class="fa-solid fa-table"></i>
        </button>
      </div>
    </div>
    
    <input type="file" ref="fileInputRef" style="display: none" accept="image/*" @change="handleFileUpload" />

    <div class="rte-content-area">
      <editor-content :editor="editor" class="rte-tiptap" />
    </div>
    
    <div class="rte-footer">
      <button class="primary-btn" @click="handleSave">Save</button>
      <button class="cancel-btn" @click="handleCancel">Cancel</button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useEditor, EditorContent, VueRenderer } from '@tiptap/vue-3'
import StarterKit from '@tiptap/starter-kit'
import axiosClient from '@/api/axiosClient'
import Placeholder from '@tiptap/extension-placeholder'
import { Underline } from '@tiptap/extension-underline'
import { TextStyle } from '@tiptap/extension-text-style'
import { Color } from '@tiptap/extension-color'
import { TaskList } from '@tiptap/extension-task-list'
import { TaskItem } from '@tiptap/extension-task-item'
import { Table } from '@tiptap/extension-table'
import { TableRow } from '@tiptap/extension-table-row'
import { TableHeader } from '@tiptap/extension-table-header'
import { TableCell } from '@tiptap/extension-table-cell'
import { Image } from '@tiptap/extension-image'
import { Link } from '@tiptap/extension-link'
import { Mention } from '@tiptap/extension-mention'
import tippy from 'tippy.js'
import 'tippy.js/dist/tippy.css'
import MentionList from './MentionList.vue'
import { usePeopleStore } from '@/store/usePeopleStore'

const props = defineProps({
  modelValue: {
    type: String,
    default: ''
  },
  placeholder: {
    type: String,
    default: 'Thêm mô tả...'
  }
})

const emit = defineEmits(['update:modelValue', 'save', 'cancel'])

const fileInputRef = ref(null)
const textColor = ref('#172B4D')
const predefineColors = ref([
  '#ff4500', '#ff8c00', '#ffd700', '#90ee90', '#00ced1', '#1e90ff', '#c71585', '#172B4D', '#5E6C84', '#0052CC', '#36B37E', '#FF5630', '#FFAB00'
])

const peopleStore = usePeopleStore()

const getMentionSuggestions = {
  items: ({ query }) => {
    return peopleStore.users.filter(item => {
      const name = item.fullName || item.name || item.email || ''
      return name.toLowerCase().includes(query.toLowerCase())
    }).slice(0, 5)
  },
  render: () => {
    let component
    let popup

    return {
      onStart: props => {
        component = new VueRenderer(MentionList, {
          props,
          editor: props.editor,
        })

        if (!props.clientRect) return

        popup = tippy('body', {
          getReferenceClientRect: props.clientRect,
          appendTo: () => document.body,
          content: component.element,
          showOnCreate: true,
          interactive: true,
          trigger: 'manual',
          placement: 'bottom-start',
        })
      },
      onUpdate(props) {
        component.updateProps(props)
        if (!props.clientRect) return
        popup[0].setProps({
          getReferenceClientRect: props.clientRect,
        })
      },
      onKeyDown(props) {
        if (props.event.key === 'Escape') {
          popup[0].hide()
          return true
        }
        return component.ref?.onKeyDown(props)
      },
      onExit() {
        if (popup && popup[0]) {
          popup[0].destroy()
        }
        if (component) {
          component.destroy()
        }
      },
    }
  },
}

const editor = useEditor({
  content: props.modelValue,
  extensions: [
    StarterKit,
    Placeholder.configure({ placeholder: props.placeholder }),
    Underline,
    TextStyle,
    Color,
    TaskList,
    TaskItem.configure({ nested: true }),
    Table.configure({ resizable: true }),
    TableRow,
    TableHeader,
    TableCell,
    Image,
    Link.configure({ openOnClick: false }),
    Mention.configure({
      HTMLAttributes: {
        class: 'mention-tag',
      },
      suggestion: getMentionSuggestions,
    }),
  ],
  onUpdate: ({ editor }) => {
    // We emit only when user presses save, or emit update:modelValue for v-model sync
    emit('update:modelValue', editor.getHTML())
  },
})

watch(() => props.modelValue, (newVal) => {
  // Prevent circular updates if editor is already at newVal
  const isSame = editor.value && editor.value.getHTML() === newVal
  if (!isSame && editor.value) {
    editor.value.commands.setContent(newVal, false)
  }
})

onBeforeUnmount(() => {
  if (editor.value) {
    editor.value.destroy()
  }
})

const currentHeadingLabel = computed(() => {
  if (!editor.value) return 'Normal text'
  if (editor.value.isActive('heading', { level: 1 })) return 'Heading 1'
  if (editor.value.isActive('heading', { level: 2 })) return 'Heading 2'
  if (editor.value.isActive('heading', { level: 3 })) return 'Heading 3'
  return 'Normal text'
})

const handleHeadingCommand = (command) => {
  const level = parseInt(command)
  if (level === 0) {
    editor.value.chain().focus().setParagraph().run()
  } else {
    editor.value.chain().focus().toggleHeading({ level }).run()
  }
}

const setTextColor = (color) => {
  if (!color) {
    editor.value.chain().focus().unsetColor().run()
  } else {
    editor.value.chain().focus().setColor(color).run()
  }
}

const setLink = () => {
  const previousUrl = editor.value.getAttributes('link').href
  const url = window.prompt('URL', previousUrl)
  if (url === null) return // cancelled
  if (url === '') {
    editor.value.chain().focus().extendMarkRange('link').unsetLink().run()
    return
  }
  editor.value.chain().focus().extendMarkRange('link').setLink({ href: url }).run()
}

const triggerFileUpload = () => {
  if (fileInputRef.value) {
    fileInputRef.value.click()
  }
}

const handleFileUpload = async (event) => {
  const file = event.target.files[0]
  if (!file) return

  try {
    const formData = new FormData()
    formData.append('file', file)
    
    const response = await axiosClient.post('/uploads/image', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
    
    const imageUrl = response.data.url
    
    editor.value.chain().focus().setImage({ src: imageUrl }).run()
    event.target.value = ''
  } catch (error) {
    console.error('Failed to upload image:', error)
    alert('Không thể tải ảnh lên. Vui lòng thử lại.')
  }
}

const insertMention = () => {
  editor.value.chain().focus().insertContent('@').run()
}

const insertTable = () => {
  editor.value.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run()
}

const handleSave = () => {
  if (editor.value) {
    emit('save', editor.value.getHTML())
  }
}

const handleCancel = () => {
  emit('cancel')
}
</script>

<style>
.rich-text-editor-container {
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  background-color: #FFFFFF;
  display: flex;
  flex-direction: column;
  box-shadow: 0 1px 1px rgba(9, 30, 66, 0.08);
  margin-bottom: 16px;
}

.rte-toolbar {
  display: flex;
  align-items: center;
  padding: 4px 8px;
  border-bottom: 1px solid #DFE1E6;
  background-color: #FAFBFC;
  border-radius: 3px 3px 0 0;
  flex-wrap: wrap;
  gap: 4px;
}

.toolbar-group {
  display: flex;
  align-items: center;
}

.toolbar-divider {
  width: 1px;
  height: 20px;
  background-color: #DFE1E6;
  margin: 0 4px;
}

.toolbar-btn {
  background: transparent;
  border: none;
  color: #42526E;
  width: 32px;
  height: 32px;
  border-radius: 3px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: background-color 0.2s;
  font-size: 14px;
}

.toolbar-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
  color: #172B4D;
}

.toolbar-btn.is-active {
  background-color: rgba(9, 30, 66, 0.08);
  color: #0052CC;
}

.text-style-btn {
  width: auto;
  padding: 0 8px;
  font-size: 14px;
  font-weight: 500;
}

.ms-1 {
  margin-left: 4px;
}

.rte-content-area {
  padding: 12px 16px;
  min-height: 120px;
  cursor: text;
}

.rte-tiptap .ProseMirror {
  min-height: 100px;
  outline: none;
  font-size: 14px;
  color: #172B4D;
  line-height: 1.5;
}

.rte-tiptap .ProseMirror p.is-editor-empty:first-child::before {
  content: attr(data-placeholder);
  float: left;
  color: #8993A4;
  pointer-events: none;
  height: 0;
}

.rte-tiptap .ProseMirror.is-focused p.is-editor-empty:first-child::before,
.rte-tiptap .ProseMirror-focused p.is-editor-empty:first-child::before {
  display: none;
}

.rte-tiptap .ProseMirror img {
  max-width: 100%;
  height: auto;
  border-radius: 3px;
}

.rte-tiptap .ProseMirror table {
  border-collapse: collapse;
  table-layout: fixed;
  width: 100%;
  margin: 0;
  overflow: hidden;
}

.rte-tiptap .ProseMirror table td,
.rte-tiptap .ProseMirror table th {
  min-width: 1em;
  border: 1px solid #DFE1E6;
  padding: 8px;
  vertical-align: top;
  box-sizing: border-box;
  position: relative;
}

.rte-tiptap .ProseMirror table th {
  font-weight: 600;
  text-align: left;
  background-color: #FAFBFC;
}

.mention-tag {
  color: #0052CC;
  background-color: #E6FCFF;
  border-radius: 3px;
  padding: 0 4px;
  font-weight: 500;
}

.rte-footer {
  padding: 12px 16px;
  display: flex;
  gap: 8px;
  align-items: center;
  border-top: 1px solid #DFE1E6;
  background-color: #FAFBFC;
  border-radius: 0 0 3px 3px;
}

.primary-btn {
  background-color: #0052CC;
  color: white;
  border: none;
  padding: 6px 12px;
  border-radius: 3px;
  font-weight: 500;
  font-size: 14px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.primary-btn:hover {
  background-color: #0047B3;
}

.cancel-btn {
  background: transparent;
  color: #42526E;
  border: none;
  padding: 6px 12px;
  border-radius: 3px;
  font-weight: 500;
  font-size: 14px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.cancel-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
  color: #172B4D;
}
</style>
