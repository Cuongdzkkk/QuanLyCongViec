<template>
  <el-dialog
    v-model="visibleComp"
    class="standard-dialog no-header-dialog sa-data-dialog"
    :show-close="false"
    :before-close="handleClose"
    append-to-body
    width="760px"
    top="6vh"
  >
    <template #header>
      <DataModalHeader icon="bi bi-folder-plus" title="Create project" description="Set project identity, visibility and ownership" close-label="Close" @close="handleClose" />
    </template>
    <div class="modal-content-inner">
      <div
        class="plane-cover-header"
        :style="coverPreviewStyle"
        :role="coverPreviewUrl ? 'img' : undefined"
        :aria-label="coverPreviewUrl ? (form.coverAltText || `Project cover for ${form.name || 'new project'}`) : undefined"
      >
        <button class="change-cover-btn" @click="showCoverPicker = !showCoverPicker">
          <i class="bi bi-palette2"></i>
          Choose cover
        </button>

        <div v-if="showCoverPicker" class="cover-popover">
          <div class="popover-heading">
            <div>
              <strong>Project background</strong>
              <span>Choose a lightweight CSS cover.</span>
            </div>
            <button type="button" @click="showCoverPicker = false"><i class="bi bi-x-lg"></i></button>
          </div>
          <ProjectBackgroundPicker v-model="form.cover" />
          <div class="custom-cover-block">
            <strong>Custom cover image</strong>
            <span>PNG, JPG, JPEG or WEBP, maximum 5 MB.</span>
            <input
              ref="coverInputRef"
              class="cover-file-input"
              type="file"
              accept=".png,.jpg,.jpeg,.webp,image/png,image/jpeg,image/webp"
              @change="handleCoverSelected"
            />
            <div class="custom-cover-actions">
              <button type="button" class="btn-secondary-sm" @click="openCustomCoverPicker">
                <i class="fa-regular fa-image"></i>
                {{ coverFile ? 'Replace image' : 'Upload image' }}
              </button>
              <button v-if="coverFile" type="button" class="btn-ghost-sm" @click="clearCustomCover">Remove image</button>
            </div>
            <input
              v-model="form.coverAltText"
              class="compact-input-field"
              type="text"
              maxlength="180"
              :disabled="!coverFile"
              :placeholder="coverFile ? 'Describe the cover image' : 'Upload a custom image first'"
            />
            <small v-if="coverFile" class="selected-cover-file">{{ coverFile.name }} · {{ formatFileSize(coverFile.size) }}</small>
            <small v-if="coverError" class="cover-error">{{ coverError }}</small>
          </div>
        </div>
      </div>

      <div class="plane-modal-body">
        <DataModalSection icon="bi bi-palette2" title="Project appearance">
        <button class="floating-emoji-selector" type="button" @click="showIconPicker = !showIconPicker">
          <ProjectAvatar :icon="form.icon" :background="form.cover" size="lg" />
        </button>

        <div v-if="showIconPicker" class="icon-popover">
          <div class="popover-heading">
            <div>
              <strong>Project avatar</strong>
              <span>Choose a Bootstrap icon.</span>
            </div>
            <button type="button" @click="showIconPicker = false"><i class="bi bi-x-lg"></i></button>
          </div>
          <ProjectAvatarPicker v-model="form.icon" />
        </div>
        </DataModalSection>

        <DataModalSection icon="bi bi-card-text" title="Project information">
        <div class="form-container">
          <div class="form-row">
            <div class="form-group flex-1">
              <label class="field-label">Project name</label>
              <input v-model="form.name" type="text" class="underlined-input" placeholder="Project Name" />
              <p v-if="submitted && !form.name" class="error-text">Name is required</p>
            </div>
            <div class="form-group w-140">
              <label class="field-label">Project ID</label>
              <input v-model="form.key" type="text" class="underlined-input" placeholder="ID" maxlength="8" />
            </div>
          </div>

          <div class="form-group">
            <label class="field-label">Description</label>
            <textarea
              v-model="form.description"
              class="compact-textarea-field"
              rows="3"
              placeholder="What is this project for?"
            ></textarea>
          </div>
        </div>
        </DataModalSection>

        <DataModalSection icon="bi bi-shield-check" title="Access and ownership">
        <div class="form-container">
          <div class="settings-grid">
            <div class="form-group">
              <label class="field-label">Visibility</label>
              <div class="segmented-control">
                <button
                  type="button"
                  :class="{ active: form.networkType === 'Public' }"
                  @click="form.networkType = 'Public'"
                >
                  <i class="fa-solid fa-globe"></i>
                  <span>Public</span>
                </button>
                <button
                  type="button"
                  :class="{ active: form.networkType === 'Private' }"
                  @click="form.networkType = 'Private'"
                >
                  <i class="fa-solid fa-lock"></i>
                  <span>Private</span>
                </button>
              </div>
            </div>

            <div class="form-group">
              <label class="field-label">Lead</label>
              <el-select v-model="form.leadUserId" class="full-width-select" placeholder="Select project lead">
                <el-option value="" label="No lead" />
                <el-option v-for="member in workspaceMembers" :key="member.userId" :label="member.fullName || member.email" :value="member.userId" />
              </el-select>
            </div>
          </div>
        </div>
        </DataModalSection>
      </div>

      <div class="dialog-footer-standard">
        <div class="footer-spacer"></div>
        <div class="footer-actions">
          <button class="btn-secondary-sm cancel-btn" @click="handleClose"><i class="bi bi-x-lg"></i>Cancel</button>
          <button class="btn-primary-sm" :disabled="loading" @click="handleSubmit">
            <i v-if="loading" class="fa-solid fa-spinner fa-spin"></i>
            <span>Create project</span>
          </button>
        </div>
      </div>
    </div>
  </el-dialog>
</template>

<script setup>
import { ref, computed, onBeforeUnmount, watch } from 'vue'
import axiosClient from '@/api/axiosClient'
import { ElMessage } from 'element-plus'
import { useSiteStore } from '@/store/useSiteStore'
import { ensureWorkspaceIdFromState } from '@/utils/contextIds'
import ProjectAvatar from '@/components/project/ProjectAvatar.vue'
import ProjectAvatarPicker from '@/components/project/ProjectAvatarPicker.vue'
import ProjectBackgroundPicker from '@/components/project/ProjectBackgroundPicker.vue'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import { useProjectStore } from '@/store/useProjectStore'
import {
  DEFAULT_PROJECT_BACKGROUND,
  DEFAULT_PROJECT_ICON,
  getProjectBackground
} from '@/config/projectAppearance'

const props = defineProps({
  visible: Boolean
})

const emit = defineEmits(['update:visible', 'created'])
const siteStore = useSiteStore()
const projectStore = useProjectStore()
const allowedCoverTypes = new Set(['image/png', 'image/jpeg', 'image/webp'])
const allowedCoverExtensions = new Set(['png', 'jpg', 'jpeg', 'webp'])
const maxCoverSize = 5 * 1024 * 1024

const coverOptions = [
  { name: 'Coral', value: 'linear-gradient(135deg, #ffb199 0%, #ff5f6d 100%)' },
  { name: 'Ocean', value: 'linear-gradient(135deg, #2563eb 0%, #06b6d4 100%)' },
  { name: 'Forest', value: 'linear-gradient(135deg, #047857 0%, #84cc16 100%)' },
  { name: 'Sunset', value: 'linear-gradient(135deg, #f97316 0%, #db2777 100%)' },
  { name: 'Graphite', value: 'linear-gradient(135deg, #3f3f46 0%, #18181b 100%)' },
  { name: 'Violet', value: 'linear-gradient(135deg, #7c3aed 0%, #ec4899 100%)' }
]

const iconOptions = ['😀', '🚀', '⚡', '💡', '🔥', '🎯', '📦', '🧩', '🛠️', '📌', '🌱', '🏁']

const defaultForm = () => ({
  name: '',
  key: '',
  description: '',
  startDate: new Date(),
  cover: DEFAULT_PROJECT_BACKGROUND,
  coverAltText: '',
  icon: DEFAULT_PROJECT_ICON,
  networkType: 'Public',
  leadUserId: ''
})

const visibleComp = computed({
  get: () => props.visible,
  set: (val) => emit('update:visible', val)
})

const formatLocalIsoDate = (value) => {
  const date = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(date.getTime())) return null
  const year = date.getFullYear()
  const month = `${date.getMonth() + 1}`.padStart(2, '0')
  const day = `${date.getDate()}`.padStart(2, '0')
  return `${year}-${month}-${day}T00:00:00`
}

const form = ref(defaultForm())
const submitted = ref(false)
const loading = ref(false)
const workspaceMembers = ref([])
const showCoverPicker = ref(false)
const showIconPicker = ref(false)
const coverInputRef = ref(null)
const coverFile = ref(null)
const coverPreviewUrl = ref('')
const coverError = ref('')

const selectedCover = computed(() => {
  return getProjectBackground(form.value.cover)
})

const coverPreviewStyle = computed(() => coverPreviewUrl.value
  ? {
      backgroundImage: `linear-gradient(rgba(15, 23, 42, 0.08), rgba(15, 23, 42, 0.32)), url("${coverPreviewUrl.value}")`,
      backgroundPosition: 'center',
      backgroundSize: 'cover'
    }
  : { background: selectedCover.value.value })

watch(() => form.value.name, (newVal) => {
  if (!form.value.key && newVal) {
    form.value.key = newVal.substring(0, 4).toUpperCase().replace(/[^A-Z0-9]/g, '')
  }
})

watch(() => props.visible, (isVisible) => {
  if (isVisible) fetchWorkspaceMembers()
})

const selectCover = (cover) => {
  form.value.cover = cover
  showCoverPicker.value = false
}

const revokeCoverPreview = () => {
  if (coverPreviewUrl.value) URL.revokeObjectURL(coverPreviewUrl.value)
  coverPreviewUrl.value = ''
}

const clearCustomCover = () => {
  revokeCoverPreview()
  coverFile.value = null
  coverError.value = ''
  form.value.coverAltText = ''
  if (coverInputRef.value) coverInputRef.value.value = ''
}

const openCustomCoverPicker = () => coverInputRef.value?.click()
const formatFileSize = (size) => `${(size / 1024 / 1024).toFixed(2)} MB`

const handleCoverSelected = (event) => {
  const file = event.target.files?.[0]
  coverError.value = ''
  if (!file) return

  const extension = file.name.split('.').pop()?.toLowerCase() || ''
  if (!allowedCoverTypes.has(file.type.toLowerCase()) || !allowedCoverExtensions.has(extension)) {
    coverError.value = 'Project cover must be PNG, JPG, JPEG, or WEBP.'
    event.target.value = ''
    return
  }
  if (file.size > maxCoverSize) {
    coverError.value = 'Project cover must be 5 MB or smaller.'
    event.target.value = ''
    return
  }

  revokeCoverPreview()
  coverFile.value = file
  coverPreviewUrl.value = URL.createObjectURL(file)
  if (!form.value.coverAltText.trim()) {
    form.value.coverAltText = form.value.name.trim()
      ? `Project cover for ${form.value.name.trim()}`
      : 'Project cover'
  }
}

const selectIcon = (icon) => {
  form.value.icon = icon
  showIconPicker.value = false
}

const fetchWorkspaceMembers = async () => {
  workspaceMembers.value = []
  try {
    const workspaceId = await ensureWorkspaceIdFromState({ siteStore })
    if (!workspaceId) return

    const membersRes = await axiosClient.get(`/workspaces/${workspaceId}/members`)
    const seen = new Set()
    workspaceMembers.value = (membersRes.data?.data || [])
      .map((member) => ({
        userId: member.userId || member.UserId || member.id || member.Id,
        fullName: member.fullName || member.FullName || member.name || member.Name,
        email: member.email || member.Email
      }))
      .filter((member) => {
        if (!member.userId || seen.has(member.userId)) return false
        seen.add(member.userId)
        return true
      })
  } catch (error) {
    console.error('Fetch members error:', error)
  }
}

const handleClose = () => {
  visibleComp.value = false
  submitted.value = false
  clearCustomCover()
  form.value = defaultForm()
  showCoverPicker.value = false
  showIconPicker.value = false
}

const handleSubmit = async () => {
  submitted.value = true
  if (!form.value.name) return
  loading.value = true
  try {
    const workspaceId = await ensureWorkspaceIdFromState({ siteStore })
    if (!workspaceId) {
      ElMessage.error('Please select a workspace before creating a project')
      return
    }
    const payload = {
      name: form.value.name,
      key: form.value.key,
      description: form.value.description,
      startDate: formatLocalIsoDate(form.value.startDate),
      networkType: form.value.networkType,
      cover: form.value.cover,
      icon: form.value.icon,
      workspaceId,
      leadUserId: form.value.leadUserId || null
    }
    const response = await axiosClient.post('/projects', payload)
    const createdProject = response.data?.data || response.data
    const projectId = createdProject?.id || createdProject?.Id

    if (coverFile.value && projectId) {
      const coverPayload = new FormData()
      coverPayload.append('file', coverFile.value)
      coverPayload.append('coverAltText', form.value.coverAltText.trim() || `Project cover for ${form.value.name.trim()}`)
      if (form.value.icon) coverPayload.append('icon', form.value.icon)

      try {
        await axiosClient.post(`/projects/${projectId}/cover`, coverPayload)
      } catch (uploadError) {
        await projectStore.fetchAllProjects(true).catch(() => {})
        emit('created', createdProject)
        const message = uploadError.response?.data?.message || 'The cover upload failed.'
        ElMessage.warning(`Project "${form.value.name}" was created, but its cover was not saved. ${message} Do not create the project again; add the cover from Project settings.`)
        handleClose()
        return
      }
    } else if (coverFile.value) {
      await projectStore.fetchAllProjects(true).catch(() => {})
      emit('created', createdProject)
      ElMessage.warning(`Project "${form.value.name}" was created, but the response did not include an ID for cover upload. Do not create the project again.`)
      handleClose()
      return
    }

    await projectStore.fetchAllProjects(true)
    const refreshedProject = projectStore.allProjects?.find((project) => (project.id || project.Id) === projectId) || createdProject
    ElMessage.success(`Created project "${form.value.name}"`)
    emit('created', refreshedProject)
    handleClose()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not create project')
  } finally {
    loading.value = false
  }
}

onBeforeUnmount(revokeCoverPreview)
</script>

<style scoped>
.modal-content-inner {
  position: relative;
  background: var(--color-surface);
  border-radius: 12px;
  max-height: calc(100vh - 96px);
  overflow: auto;
}

.floating-close-btn {
  position: absolute; top: 16px; right: 16px;
  width: 32px; height: 32px; border-radius: 6px;
  background: rgba(0,0,0,0.3); backdrop-filter: blur(4px);
  border: none; color: #fff; cursor: pointer; z-index: 10;
  display: flex; align-items: center; justify-content: center;
  transition: all 0.2s;
}
.floating-close-btn:hover { background: rgba(0,0,0,0.5); transform: scale(1.05); }

.plane-cover-header {
  height: 180px; width: 100%; position: relative;
}

.change-cover-btn {
  position: absolute; bottom: 16px; right: 24px;
  background: rgba(0,0,0,0.4); color: #fff;
  border: 1px solid rgba(255,255,255,0.2); border-radius: 6px;
  padding: 6px 12px; font-size: 12px; font-weight: 600; cursor: pointer;
}

.cover-popover {
  position: absolute; top: 56px; right: 24px;
  width: min(620px, calc(100vw - 80px)); padding: 16px; background: var(--color-surface);
  border: 1px solid var(--color-border); border-radius: 10px;
  box-shadow: var(--shadow-xl); display: block; z-index: 20;
  max-height: min(520px, calc(100vh - 180px)); overflow-y: auto;
}

.custom-cover-block { display: flex; flex-direction: column; gap: 8px; margin-top: 16px; padding-top: 14px; border-top: 1px solid var(--color-border); }
.custom-cover-block > strong { color: var(--color-text-primary); font-size: 13px; }
.custom-cover-block > span { color: var(--color-text-muted); font-size: 11px; }
.custom-cover-block .cover-file-input { position: absolute; width: 1px; height: 1px; opacity: 0; pointer-events: none; }
.custom-cover-actions { display: flex; align-items: center; flex-wrap: wrap; gap: 8px; }
.btn-ghost-sm { border: 0; background: transparent; color: var(--color-text-secondary); padding: 8px 10px; cursor: pointer; }
.compact-input-field { width: 100%; border: 1px solid var(--color-border); border-radius: 7px; background: var(--color-bg); color: var(--color-text-primary); padding: 9px 10px; }
.selected-cover-file { color: var(--color-text-secondary); overflow-wrap: anywhere; }
.cover-error { color: var(--color-danger, #dc2626); }

.cover-swatch {
  height: 50px; border-radius: 6px; border: 2px solid transparent;
  cursor: pointer; display: flex; align-items: center; justify-content: center;
  font-size: 11px; font-weight: 700; color: #fff; text-shadow: 0 1px 2px rgba(0,0,0,0.3);
}
.cover-swatch.active { border-color: var(--color-accent); }

.plane-modal-body { padding: 0 32px 32px; position: relative; }

.floating-emoji-selector {
  width: 64px; height: 64px; border-radius: 12px;
  background: var(--color-surface); border: 4px solid var(--color-surface);
  box-shadow: var(--shadow-md); display: flex; align-items: center; justify-content: center;
  padding: 0; margin-top: -32px; margin-bottom: 24px; cursor: pointer; transition: all 0.2s;
}
.floating-emoji-selector:hover { transform: translateY(-2px); box-shadow: var(--shadow-lg); }

.icon-popover {
  position: absolute; top: 40px; left: 32px;
  width: min(560px, calc(100vw - 80px)); padding: 16px; background: var(--color-surface);
  border: 1px solid var(--color-border); border-radius: 10px;
  box-shadow: var(--shadow-xl); display: block; z-index: 20;
  max-height: min(520px, calc(100vh - 180px)); overflow-y: auto;
}

.popover-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 14px;
}

.popover-heading > div {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.popover-heading strong { color: var(--color-text-primary); font-size: 14px; }
.popover-heading span { color: var(--color-text-muted); font-size: 11px; }
.popover-heading button {
  display: grid; place-items: center; width: 30px; height: 30px; border: 0; border-radius: 8px;
  background: transparent; color: var(--color-text-muted); cursor: pointer;
}
.popover-heading button:hover { background: var(--color-surface-hover); color: var(--color-text-primary); }

.icon-choice {
  width: 36px; height: 36px; border-radius: 6px; border: 1px solid var(--color-border);
  background: var(--color-bg); cursor: pointer; font-size: 18px; display: flex; align-items: center; justify-content: center;
}
.icon-choice:hover { background: var(--color-surface-hover); }
.icon-choice.active { border-color: var(--color-accent); background: color-mix(in srgb, var(--color-accent) 10%, transparent); }

.form-container { display: flex; flex-direction: column; gap: 24px; }
.form-row { display: flex; gap: 24px; align-items: flex-end; }
.form-group { display: flex; flex-direction: column; gap: 8px; }

.field-label { font-size: 12px; font-weight: 700; color: var(--color-text-muted); text-transform: uppercase; letter-spacing: 0.05em; }

.underlined-input {
  background: transparent; border: none; border-bottom: 2px solid var(--color-border);
  padding: 8px 0; font-size: 18px; font-weight: 600; color: var(--color-text-primary);
  width: 100%; outline: none; transition: all 0.2s;
}
.underlined-input:focus { border-color: var(--color-accent); }

.compact-textarea-field {
  background: var(--color-bg); border: 1px solid var(--color-border);
  border-radius: 8px; padding: 12px; font-size: 14px; color: var(--color-text-primary);
  outline: none; transition: all 0.2s; resize: none;
}
.compact-textarea-field:focus { border-color: var(--color-accent); box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-accent) 15%, transparent); }

.settings-grid { display: grid; grid-template-columns: 1fr 1.2fr; gap: 24px; }

.segmented-control { display: flex; gap: 8px; background: var(--color-bg); padding: 4px; border-radius: 8px; border: 1px solid var(--color-border); }
.segmented-control button {
  flex: 1; height: 36px; border: none; background: transparent;
  border-radius: 6px; display: flex; align-items: center; justify-content: center;
  gap: 8px; cursor: pointer; color: var(--color-text-secondary); font-size: 13px; font-weight: 600; transition: all 0.2s;
}
.segmented-control button.active { background: var(--color-surface); color: var(--color-accent); box-shadow: var(--shadow-sm); }

.full-width-select { width: 100%; }

.error-text { font-size: 11px; color: #ef4444; margin-top: 4px; }

.dialog-footer-standard {
  display: flex; justify-content: space-between; align-items: center;
  padding: 16px 32px 32px; border-top: 1px solid var(--color-border);
}

.footer-actions { display: flex; gap: 12px; }

.btn-primary-sm {
  background: var(--color-accent); color: #fff;
  border: none; border-radius: 6px; padding: 10px 20px;
  font-weight: 700; font-size: 14px; cursor: pointer; transition: all 0.2s;
  display: flex; align-items: center; gap: 8px;
}
.btn-primary-sm:hover:not(:disabled) { background: var(--color-accent-hover); transform: translateY(-1px); }

.btn-secondary-sm {
  background: transparent; color: var(--color-text-secondary);
  border: 1px solid var(--color-border); border-radius: 6px; padding: 10px 20px;
  font-weight: 600; font-size: 14px; cursor: pointer; transition: all 0.2s;
}
.btn-secondary-sm:hover { background: var(--color-surface-hover); color: var(--color-text-primary); }

.w-140 { width: 140px; }
.flex-1 { flex: 1; }
</style>

<style>
.no-header-dialog.el-dialog {
  max-width: 92vw;
  margin-top: 48px;
  background: transparent !important;
  box-shadow: none !important;
  border: none !important;
}
.no-header-dialog .el-dialog__header { display: none !important; }
.no-header-dialog .el-dialog__body { padding: 0 !important; }
</style>
