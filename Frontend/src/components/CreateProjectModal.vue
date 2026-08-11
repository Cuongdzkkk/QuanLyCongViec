<template>
  <el-dialog
    v-model="visibleComp"
    width="940px"
    class="standard-dialog sa-data-dialog sa-modal--lg"
    :show-close="false"
    append-to-body
  >
    <template #header>
      <DataModalHeader
        icon="bi bi-briefcase"
        title="Tạo dự án"
        description="Thiết lập thông tin, phạm vi truy cập và diện mạo dự án"
        @close="handleClose"
      />
    </template>

    <div class="modal-layout">
      <div class="form-column">
        <DataModalSection icon="bi bi-card-text" title="Thông tin dự án">
          <DataModalField label="Tên dự án">
            <input v-model="form.name" type="text" placeholder="Kế hoạch Sprint A" class="compact-input-field" />
          </DataModalField>

          <DataModalField label="Mã dự án">
            <input v-model="form.key" type="text" maxlength="8" placeholder="SPR" class="compact-input-field" />
          </DataModalField>

          <DataModalField label="Mô tả">
            <textarea v-model="form.description" rows="3" placeholder="Dự án này dùng để quản lý việc gì?" class="compact-textarea-field"></textarea>
          </DataModalField>
        </DataModalSection>

        <DataModalSection icon="bi bi-sliders" title="Phạm vi và thời gian">
          <div class="split-grid">
          <DataModalField label="Ngày bắt đầu">
            <input v-model="form.startDate" type="date" class="compact-input-field" />
          </DataModalField>

          <DataModalField label="Quyền xem">
            <el-select v-model="form.networkType" class="full-width-select">
              <el-option label="Công khai" value="Public" />
              <el-option label="Riêng tư" value="Private" />
            </el-select>
          </DataModalField>
          </div>
        </DataModalSection>

        <DataModalSection icon="bi bi-palette2" title="Diện mạo">
          <DataModalField label="Project avatar">
            <ProjectAvatarPicker v-model="form.icon" />
          </DataModalField>
        </DataModalSection>
      </div>

      <div class="cover-column">
        <DataModalSection icon="bi bi-eye" title="Xem trước">
          <div class="cover-preview" :style="{ background: previewBackground }">
            <div class="cover-overlay">
              <ProjectAvatar :icon="form.icon" :background="form.cover" size="lg" />
              <strong class="preview-name">{{ form.name || 'Xem trước dự án' }}</strong>
            </div>
          </div>

          <ProjectBackgroundPicker v-model="form.cover" />

          <div class="gallery-header">
            <h4 class="section-title">Ảnh bìa dự án</h4>
            <p class="helper-text-muted">SprintA sẽ không tự dùng ảnh mẫu. Bạn có thể cập nhật ảnh bìa thật trong phần cài đặt dự án.</p>
          </div>
        </DataModalSection>
      </div>
    </div>

    <template #footer>
      <div class="dialog-footer-standard">
        <div class="footer-spacer"></div>
        <div class="footer-actions">
          <button class="cancel-btn" @click="handleClose"><i class="bi bi-x-lg"></i> Hủy</button>
          <button class="btn-primary-sm" :disabled="submitting" @click="handleSubmit">
            <i v-if="submitting" class="fa-solid fa-spinner fa-spin"></i>
            <i v-else class="fa-solid fa-plus"></i>
            {{ submitting ? 'Đang tạo...' : 'Tạo dự án' }}
          </button>
        </div>
      </div>
    </template>
  </el-dialog>
</template>

<script setup>
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'
import ProjectAvatar from '@/components/project/ProjectAvatar.vue'
import ProjectAvatarPicker from '@/components/project/ProjectAvatarPicker.vue'
import ProjectBackgroundPicker from '@/components/project/ProjectBackgroundPicker.vue'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import DataModalField from '@/components/common/Foundation/DataModalField.vue'
import {
  DEFAULT_PROJECT_BACKGROUND,
  DEFAULT_PROJECT_ICON,
  getProjectBackgroundStyle
} from '@/config/projectAppearance'

const props = defineProps({
  visible: Boolean
})

const emit = defineEmits(['update:visible', 'created'])

const visibleComp = computed({
  get: () => props.visible,
  set: (value) => emit('update:visible', value)
})

const submitting = ref(false)

const formatDateOnly = (value) => {
  const date = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  const year = date.getFullYear()
  const month = `${date.getMonth() + 1}`.padStart(2, '0')
  const day = `${date.getDate()}`.padStart(2, '0')
  return `${year}-${month}-${day}`
}

const createInitialForm = () => ({
  name: '',
  key: '',
  description: '',
  startDate: formatDateOnly(new Date()),
  networkType: 'Public',
  cover: DEFAULT_PROJECT_BACKGROUND,
  icon: DEFAULT_PROJECT_ICON
})

const form = ref(createInitialForm())
const previewBackground = computed(() => getProjectBackgroundStyle(form.value.cover))

const resetForm = () => {
  form.value = createInitialForm()
}

const handleClose = () => {
  visibleComp.value = false
  resetForm()
}

const handleSubmit = async () => {
  if (!form.value.name.trim()) {
    ElMessage.warning('Vui lòng nhập tên dự án')
    return
  }

  submitting.value = true
  try {
    const response = await axiosClient.post('/projects', {
      name: form.value.name.trim(),
      key: form.value.key.trim() || null,
      description: form.value.description.trim() || null,
      startDate: form.value.startDate,
      networkType: form.value.networkType,
      cover: form.value.cover || null,
      icon: form.value.icon || null
    })

    emit('created', response.data?.data || response.data)
    ElMessage.success('Đã tạo dự án')
    handleClose()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể tạo dự án')
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped>
.modal-layout {
  display: grid;
  grid-template-columns: 1.1fr 0.9fr;
  gap: 32px;
  padding: 0 24px 24px;
  max-height: calc(100vh - 220px);
  overflow-y: auto;
}

.form-column { display: flex; flex-direction: column; gap: 20px; }
.form-group { display: flex; flex-direction: column; gap: 6px; }

.field-label {
  font-size: 13px; font-weight: 700;
  color: var(--color-text-secondary);
}

.compact-input-field, .compact-textarea-field {
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 6px;
  padding: 8px 12px;
  color: var(--color-text-primary);
  font-size: 14px;
  outline: none;
  transition: all 0.2s;
}
.compact-input-field:focus, .compact-textarea-field:focus {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-accent) 20%, transparent);
}

.split-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }

.cover-column { display: flex; flex-direction: column; gap: 16px; }

.cover-preview {
  height: 160px; border-radius: 10px; overflow: hidden;
  background-size: cover; background-position: center;
  border: 1px solid var(--color-border);
}

.cover-overlay {
  height: 100%; display: flex; flex-direction: column; justify-content: flex-end;
  gap: 10px; padding: 16px;
  background: linear-gradient(180deg, transparent 0%, rgba(0,0,0,0.7) 100%);
}

.preview-badge {
  width: 36px; height: 36px; display: flex; align-items: center; justify-content: center;
  background: rgba(255,255,255,0.2); backdrop-filter: blur(4px);
  border-radius: 8px; font-size: 18px;
}

.preview-name { color: #fff; font-size: 16px; font-weight: 700; }

.gallery-header { margin-top: 4px; }
.section-title { font-size: 14px; font-weight: 700; color: var(--color-text-primary); margin-bottom: 4px; }
.helper-text-muted { font-size: 12px; color: var(--color-text-muted); }

.cover-grid {
  display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px;
}

.cover-option {
  height: 60px; border-radius: 6px; border: 2px solid transparent;
  background-size: cover; background-position: center;
  cursor: pointer; transition: all 0.2s;
}
.cover-option.active { border-color: var(--color-accent); transform: scale(1.02); }

.dialog-header-standard {
  display: flex; align-items: center; justify-content: space-between;
  padding: 24px 24px 16px;
}
.dialog-title { font-size: 20px; font-weight: 700; color: var(--color-text-primary); margin: 0; }
.icon-btn-ghost {
  width: 32px; height: 32px; display: flex; align-items: center; justify-content: center;
  border: none; background: transparent; color: var(--color-text-muted); cursor: pointer; border-radius: 6px;
}
.icon-btn-ghost:hover { background: var(--color-surface-hover); color: var(--color-text-primary); }

.dialog-footer-standard {
  display: flex; justify-content: space-between; align-items: center;
  padding: 16px 24px 24px; border-top: 1px solid var(--color-border);
}
.footer-actions { display: flex; gap: 12px; }

.btn-primary-sm {
  background: var(--color-accent); color: #fff;
  border: none; border-radius: 6px; padding: 8px 16px;
  font-weight: 600; font-size: 13px; cursor: pointer; transition: all 0.2s;
  display: flex; align-items: center; gap: 8px;
}
.btn-primary-sm:hover { background: var(--color-accent-hover); }

.btn-secondary-sm {
  background: var(--color-surface); color: var(--color-text-primary);
  border: 1px solid var(--color-border); border-radius: 6px; padding: 8px 16px;
  font-weight: 600; font-size: 13px; cursor: pointer; transition: all 0.2s;
}
.btn-secondary-sm:hover { background: var(--color-surface-hover); border-color: var(--color-border-hover); }

.full-width-select { width: 100%; }

@media (max-width: 768px) {
  .modal-layout { grid-template-columns: 1fr; }
}
</style>

<style>
.standard-dialog.el-dialog {
  max-width: 92vw;
  margin-top: 48px;
  background: var(--color-surface) !important;
  border-radius: 12px !important;
  box-shadow: var(--shadow-xl) !important;
  border: 1px solid var(--color-border) !important;
}
.standard-dialog .el-dialog__header, .standard-dialog .el-dialog__body, .standard-dialog .el-dialog__footer { padding: 0 !important; }
</style>
