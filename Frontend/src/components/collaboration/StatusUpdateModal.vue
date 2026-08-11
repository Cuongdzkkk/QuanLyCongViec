<template>
  <el-dialog
    v-model="visible"
    title="Cập nhật trạng thái của bạn"
    width="500px"
    class="status-update-dialog sa-data-dialog sa-modal--form"
    :before-close="handleClose"
    :show-close="false"
    append-to-body
  >
    <template #header>
      <DataModalHeader
        icon="bi bi-person-badge"
        title="Cập nhật trạng thái"
        description="Hiển thị trạng thái của bạn cho đồng nghiệp"
        @close="handleClose"
      />
    </template>

    <div class="status-content">
      <!-- Current Status Preview -->
      <DataModalSection v-if="currentStatusText || currentStatusEmoji" icon="bi bi-person-check" title="Trạng thái hiện tại">
      <section class="current-status-box">
        <div class="current-status-preview">
          <span class="status-icon-shell" aria-hidden="true">
            <i class="bi" :class="getStatusIcon(currentStatusEmoji)"></i>
          </span>
          <span class="preview-copy">
            <span class="preview-text">{{ currentStatusText || 'Chưa cập nhật chi tiết' }}</span>
            <span class="active-status-badge">Đang hoạt động</span>
          </span>
          <button type="button" class="delete-btn" @click="clearStatus">
            <i class="bi bi-trash" aria-hidden="true"></i>
            <span>Xóa trạng thái</span>
          </button>
        </div>
      </section>
      </DataModalSection>

      <!-- Emoji Presets Grid -->
      <DataModalSection icon="bi bi-grid" title="Chọn trạng thái nhanh">
      <section class="emoji-presets">
        <div class="presets-grid">
          <button
            v-for="preset in presets"
            :key="preset.emoji"
            type="button"
            class="preset-btn"
            :class="{ active: selectedEmoji === preset.emoji }"
            @click="selectPreset(preset)"
          >
            <i class="bi preset-icon" :class="getStatusIcon(preset.emoji)" aria-hidden="true"></i>
            <span class="preset-text">{{ preset.text }}</span>
          </button>
        </div>
      </section>
      </DataModalSection>

      <!-- Custom Status Form -->
      <DataModalSection icon="bi bi-pencil-square" title="Trạng thái tùy chỉnh">
      <section class="custom-status-form">
        <div class="custom-input-wrapper">
          <button type="button" class="emoji-selector-trigger" aria-label="Đổi biểu tượng trạng thái" @click="toggleEmojiPicker">
            <i class="bi" :class="getStatusIcon(selectedEmoji)" aria-hidden="true"></i>
          </button>
          <input
            v-model="selectedText"
            type="text"
            placeholder="Nhập trạng thái bạn muốn chia sẻ..."
            maxlength="100"
          />
        </div>
      </section>
      </DataModalSection>

      <!-- Expiration Selector -->
      <DataModalSection icon="bi bi-clock" title="Thời hạn trạng thái">
      <section class="expiration-section">
        <DataModalField label="Xóa trạng thái sau">
        <el-select v-model="expiration" class="w-full custom-select">
          <el-option label="Không xóa" value="never" />
          <el-option label="30 phút" value="30m" />
          <el-option label="1 giờ" value="1h" />
          <el-option label="Hôm nay" value="today" />
          <el-option label="Tuần này" value="week" />
        </el-select>
        </DataModalField>
      </section>
      </DataModalSection>
    </div>

    <template #footer>
      <div class="dialog-footer">
        <el-button class="btn-cancel" @click="handleClose">
          <i class="bi bi-x-lg" aria-hidden="true"></i>
          Hủy
        </el-button>
        <button type="button" class="status-save-button" @click="saveStatus">
          <i class="bi bi-check2" aria-hidden="true"></i>
          Lưu trạng thái
        </button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup>
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import DataModalField from '@/components/common/Foundation/DataModalField.vue'

const props = defineProps({
  modelValue: {
    type: Boolean,
    default: false
  },
  initialEmoji: {
    type: String,
    default: ''
  },
  initialText: {
    type: String,
    default: ''
  }
})

const emit = defineEmits(['update:modelValue', 'save', 'clear'])

const visible = ref(false)
const selectedEmoji = ref('💻')
const selectedText = ref('')
const expiration = ref('today')

const currentStatusEmoji = ref('')
const currentStatusText = ref('')

const presets = [
  { emoji: '💻', text: 'Đang làm việc' },
  { emoji: '☕', text: 'Nghỉ giải lao' },
  { emoji: '🤒', text: 'Nghỉ ốm' },
  { emoji: '🏠', text: 'WFH' },
  { emoji: '🚀', text: 'Đang triển khai' },
  { emoji: '💬', text: 'Đang họp' }
]

const statusIconMap = {
  '💻': 'bi-laptop',
  '☕': 'bi-cup-hot',
  '🤒': 'bi-heart-pulse',
  '🏠': 'bi-house-door',
  '🚀': 'bi-rocket-takeoff',
  '💬': 'bi-camera-video',
  '🎉': 'bi-stars',
  '✈️': 'bi-airplane',
  '🔍': 'bi-search',
  '💡': 'bi-lightbulb'
}

const getStatusIcon = (emoji) => statusIconMap[emoji] || 'bi-pencil-square'

watch(() => props.modelValue, (newVal) => {
  visible.value = newVal
  if (newVal) {
    selectedEmoji.value = props.initialEmoji || '💻'
    selectedText.value = props.initialText || ''
    currentStatusEmoji.value = props.initialEmoji
    currentStatusText.value = props.initialText
  }
})

watch(visible, (newVal) => {
  emit('update:modelValue', newVal)
})

const selectPreset = (preset) => {
  selectedEmoji.value = preset.emoji
  selectedText.value = preset.text
}

const toggleEmojiPicker = () => {
  // Simple rotation picker for demo
  const emojis = ['💻', '☕', '🤒', '🏠', '🚀', '💬', '🎉', '✈️', '🔍', '💡']
  const idx = emojis.indexOf(selectedEmoji.value)
  selectedEmoji.value = emojis[(idx + 1) % emojis.length]
}

const saveStatus = () => {
  currentStatusEmoji.value = selectedEmoji.value
  currentStatusText.value = selectedText.value
  emit('save', {
    emoji: selectedEmoji.value,
    text: selectedText.value,
    expiration: expiration.value
  })
  ElMessage.success('Cập nhật trạng thái thành công')
  visible.value = false
}

const clearStatus = () => {
  selectedEmoji.value = '💻'
  selectedText.value = ''
  currentStatusEmoji.value = ''
  currentStatusText.value = ''
  emit('clear')
  ElMessage.success('Đã xóa trạng thái làm việc')
  visible.value = false
}

const handleClose = () => {
  visible.value = false
}
</script>

<style scoped>
/* Dialog styling overrides */
:deep(.status-update-dialog) {
  background-color: var(--color-surface) !important;
  border: 1px solid var(--color-border);
  border-radius: var(--sa-radius-md, 8px) !important;
  box-shadow: var(--shadow-xl) !important;
  overflow: hidden;
}

:deep(.status-update-dialog .el-dialog__header) {
  margin-right: 0;
  padding: 18px 20px 14px;
  border-bottom: 1px solid var(--color-border);
}

:deep(.status-update-dialog .el-dialog__title) {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
}

:deep(.status-update-dialog .el-dialog__headerbtn) {
  top: 18px;
}

:deep(.status-update-dialog .el-dialog__body) {
  padding: 14px 20px;
  max-height: 70vh;
  overflow-y: auto;
}

:deep(.status-update-dialog .el-dialog__footer) {
  padding: 14px 20px;
  border-top: 1px solid var(--color-border);
  background-color: var(--color-surface-hover);
}

.field-label {
  display: block;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-muted);
}

/* Current status box */
.current-status-box {
  background-color: var(--sa-primary-soft);
  border: 1px dashed var(--color-accent);
  padding: 10px 14px;
  border-radius: var(--radius-card);
  margin-bottom: 20px;
}

.current-status-preview {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: var(--color-text-primary);
}

.preview-emoji {
  font-size: 18px;
}

.delete-btn {
  color: var(--color-danger);
  font-weight: 500;
  font-size: 12px;
  cursor: pointer;
  transition: opacity 0.2s;
}

.delete-btn:hover {
  opacity: 0.8;
  text-decoration: underline;
}

/* Presets Grid */
.presets-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 8px;
}

.preset-btn {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-button);
  background-color: var(--color-surface);
  color: var(--color-text-primary);
  font-size: 13px;
  text-align: left;
  cursor: pointer;
  transition: all 0.2s;
  font-weight: 500;
}

.preset-btn:hover {
  background-color: var(--color-surface-hover);
  border-color: var(--color-border-hover);
}

.preset-btn.active {
  border-color: var(--color-accent);
  background-color: var(--sa-primary-soft);
  color: var(--color-accent);
  font-weight: 600;
  box-shadow: 0 0 0 1px var(--color-accent);
}

.preset-emoji {
  font-size: 16px;
}

/* Custom Input Wrapper */
.custom-input-wrapper {
  display: flex;
  align-items: center;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-input);
  background-color: var(--color-input-bg);
  overflow: hidden;
  height: 38px;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.custom-input-wrapper:focus-within {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 2px var(--sa-primary-soft);
}

.emoji-selector-trigger {
  padding: 0 12px;
  font-size: 18px;
  cursor: pointer;
  user-select: none;
  display: flex;
  align-items: center;
  justify-content: center;
  border-right: 1px solid var(--color-border);
  background-color: var(--color-surface-hover);
  height: 100%;
  transition: background-color 0.2s;
}

.emoji-selector-trigger:hover {
  background-color: var(--color-border);
}

.custom-input-wrapper input {
  flex: 1;
  border: none !important;
  outline: none !important;
  background: transparent !important;
  padding: 0 12px;
  color: var(--color-text-primary);
  font-size: 13px;
  height: 100% !important;
}

/* Expiration Selector */
.custom-select {
  margin-top: 6px !important;
}

:deep(.custom-select .el-select__wrapper) {
  background-color: rgba(255, 255, 255, 0.03) !important;
  border: 1px solid var(--color-border) !important;
  box-shadow: none !important;
  border-radius: 8px !important;
  padding: 4px 12px !important;
  transition: all 0.2s !important;
  height: 38px !important;
  min-height: 38px !important;
}

:deep(.custom-select .el-select__wrapper:hover) {
  border-color: rgba(56, 189, 248, 0.3) !important;
}

:deep(.custom-select .el-select__wrapper.is-focus) {
  border-color: rgba(56, 189, 248, 0.5) !important;
  box-shadow: 0 0 0 1px rgba(56, 189, 248, 0.5) !important;
}

:deep(.custom-select .el-select__selected-item) {
  color: var(--color-text-primary) !important;
  font-size: 13.5px !important;
  font-weight: 500 !important;
}

/* Footer Buttons */
:deep(.btn-cancel),
:deep(.btn-save) {
  height: 34px !important;
  padding: 0 18px !important;
  border-radius: 7px !important;
  font-size: 13px !important;
  font-weight: 600 !important;
  line-height: 34px !important;
  cursor: pointer;
  transition: all 0.2s ease !important;
  display: inline-flex !important;
  align-items: center !important;
}

:deep(.btn-cancel) {
  background: transparent !important;
  border: 1px solid rgba(148, 163, 184, 0.3) !important;
  color: var(--color-text-secondary) !important;
}

:deep(.btn-cancel:hover) {
  background-color: rgba(148, 163, 184, 0.08) !important;
  border-color: rgba(148, 163, 184, 0.5) !important;
  color: var(--color-text-primary) !important;
}

:deep(.btn-save) {
  background: linear-gradient(135deg, #0ea5e9, #0284c7) !important;
  border: none !important;
  color: #ffffff !important;
  box-shadow: 0 2px 8px rgba(14, 165, 233, 0.3) !important;
}

:deep(.btn-save:hover) {
  background: linear-gradient(135deg, #38bdf8, #0ea5e9) !important;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(14, 165, 233, 0.4) !important;
}

/* Enterprise status dialog */
:deep(.status-update-dialog) {
  --status-primary: var(--color-accent, #0ea5e9);
  width: min(500px, calc(100vw - 24px)) !important;
  border: 1px solid color-mix(in srgb, var(--color-border) 88%, transparent) !important;
  border-radius: 14px !important;
  background: var(--color-surface) !important;
  box-shadow: 0 24px 64px rgba(15, 23, 42, 0.16) !important;
}

:deep(.status-update-dialog .el-dialog__header) {
  padding: 20px 22px 18px !important;
  border-bottom: 1px solid var(--color-border);
  margin: 0 !important;
}

.status-dialog-heading {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.heading-icon,
.status-icon-shell {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: 0 0 auto;
  width: 40px;
  height: 40px;
  border: 1px solid color-mix(in srgb, var(--status-primary) 22%, var(--color-border));
  border-radius: 11px;
  color: var(--status-primary);
  background: color-mix(in srgb, var(--status-primary) 8%, var(--color-surface));
}

.heading-copy {
  min-width: 0;
}

.heading-copy h2 {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 22px;
  font-weight: 600;
  line-height: 1.25;
}

.heading-copy p {
  margin: 3px 0 0;
  color: var(--color-text-muted);
  font-size: 12.5px;
  line-height: 1.4;
}

.dialog-close-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  margin-left: auto;
  padding: 0;
  border: 1px solid transparent;
  border-radius: 9px;
  color: #64748b;
  background: transparent;
  cursor: pointer;
  transition: color 150ms ease, border-color 150ms ease, background-color 150ms ease;
}

.dialog-close-btn:hover {
  color: var(--status-primary);
  border-color: var(--color-border);
  background: var(--color-surface-hover);
}

:deep(.status-update-dialog .el-dialog__body) {
  max-height: min(68vh, 650px);
  padding: 22px !important;
}

.status-content {
  display: flex;
  flex-direction: column;
  gap: 22px;
}

.status-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin: 0 !important;
}

.divided-section {
  padding-top: 20px !important;
  border-top: 1px solid var(--color-border) !important;
}

.field-label {
  display: flex;
  align-items: center;
  gap: 7px;
  margin: 0 !important;
  color: #64748b;
  font-size: 12px;
  font-weight: 600;
  line-height: 1.3;
  letter-spacing: 0.5px;
  text-transform: uppercase;
}

.field-label .bi,
.preset-icon,
.emoji-selector-trigger .bi,
.dialog-close-btn .bi,
.dialog-footer .bi,
.heading-icon .bi,
.status-icon-shell .bi,
.delete-btn .bi {
  font-size: 18px;
  line-height: 1;
}

.current-status-box {
  margin: 0 !important;
  padding: 16px !important;
  border: 1px solid #e2e8f0 !important;
  border-radius: 12px !important;
  background: #f8fafc !important;
  box-shadow: none;
  transition: border-color 180ms ease, box-shadow 180ms ease;
}

.current-status-box:hover {
  border-color: color-mix(in srgb, var(--status-primary) 30%, #e2e8f0) !important;
  box-shadow: 0 8px 22px rgba(15, 23, 42, 0.06);
}

.current-status-box .field-label {
  margin-bottom: 12px !important;
}

.current-status-preview {
  gap: 12px;
}

.status-icon-shell {
  width: 38px;
  height: 38px;
  color: #64748b;
  border-color: #e2e8f0;
  background: #ffffff;
}

.preview-copy {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  min-width: 0;
}

.preview-text {
  color: #0f172a;
  font-size: 15px;
  font-weight: 500;
  line-height: 1.4;
}

.active-status-badge {
  display: inline-flex;
  align-items: center;
  min-height: 22px;
  padding: 0 8px;
  border-radius: 999px;
  color: #047857;
  background: #ecfdf5;
  font-size: 10px;
  font-weight: 700;
  white-space: nowrap;
}

.delete-btn {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  flex: 0 0 auto;
  margin-left: auto !important;
  padding: 7px 9px;
  border: 0;
  border-radius: 8px;
  color: #64748b;
  background: transparent;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: color 150ms ease, background-color 150ms ease;
}

.delete-btn:hover {
  color: #dc2626;
  background: #fef2f2;
  text-decoration: none;
  opacity: 1;
}

.presets-grid {
  gap: 12px;
}

.preset-btn {
  height: 44px;
  padding: 0 16px;
  gap: 10px;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  color: #334155;
  background: var(--color-surface);
  font-size: 14px;
  font-weight: 500;
  transition: transform 150ms ease, border-color 150ms ease, background-color 150ms ease, box-shadow 150ms ease;
}

.preset-icon {
  color: #64748b;
  transition: color 150ms ease;
}

.preset-btn:hover {
  transform: translateY(-1px);
  border-color: var(--status-primary);
  color: #0f172a;
  background: #f8fafc;
  box-shadow: 0 5px 14px rgba(15, 23, 42, 0.05);
}

.preset-btn:hover .preset-icon,
.preset-btn.active .preset-icon {
  color: var(--status-primary);
}

.preset-btn.active {
  transform: scale(1.01);
  border: 1px solid var(--status-primary);
  color: #0f172a;
  background: #eff6ff;
  font-weight: 600;
  box-shadow: 0 0 0 1px color-mix(in srgb, var(--status-primary) 16%, transparent);
}

.custom-input-wrapper {
  height: 44px;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  background: var(--color-input-bg, var(--color-surface));
}

.custom-input-wrapper:focus-within {
  border-color: var(--status-primary);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--status-primary) 13%, transparent);
}

.emoji-selector-trigger {
  width: 44px;
  padding: 0;
  border: 0;
  border-right: 1px solid #e2e8f0;
  color: #64748b;
  background: #f8fafc;
}

.emoji-selector-trigger:hover {
  color: var(--status-primary);
  background: #f1f5f9;
}

.custom-input-wrapper input {
  padding: 0 14px;
  color: var(--color-text-primary);
  font-size: 14px;
}

.custom-input-wrapper input::placeholder {
  color: #94a3b8;
}

.custom-select {
  margin-top: 0 !important;
}

:deep(.custom-select .el-select__wrapper) {
  height: 44px !important;
  min-height: 44px !important;
  padding: 0 14px !important;
  border: 1px solid #e2e8f0 !important;
  border-radius: 12px !important;
  background: var(--color-input-bg, var(--color-surface)) !important;
}

:deep(.custom-select .el-select__wrapper:hover),
:deep(.custom-select .el-select__wrapper.is-focus) {
  border-color: var(--status-primary) !important;
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--status-primary) 13%, transparent) !important;
}

:deep(.status-update-dialog .el-dialog__footer) {
  padding: 16px 22px !important;
  border-top: 1px solid var(--color-border);
  background: color-mix(in srgb, var(--color-surface-hover) 48%, var(--color-surface));
}

.dialog-footer {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 12px;
}

.status-save-button {
  min-width: 148px;
  height: 40px;
  padding: 0 18px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border: 1px solid #0284c7;
  border-radius: 10px;
  outline: none;
  background: #0ea5e9;
  color: #ffffff;
  font: inherit;
  font-size: 14px;
  font-weight: 700;
  line-height: 1;
  white-space: nowrap;
  cursor: pointer;
  box-shadow: 0 5px 14px rgb(14 165 233 / 0.25);
  transition: transform 150ms ease, box-shadow 150ms ease, background-color 150ms ease;
}

.status-save-button:hover {
  transform: translateY(-1px);
  background: #0284c7;
  box-shadow: 0 8px 18px rgb(14 165 233 / 0.32);
}

.status-save-button:focus-visible {
  outline: none;
  box-shadow: 0 0 0 3px rgb(14 165 233 / 0.22), 0 5px 14px rgb(14 165 233 / 0.25);
}

.status-save-button .bi {
  font-size: 18px;
  line-height: 1;
}

:deep(.btn-cancel),
:deep(.btn-save) {
  justify-content: center;
  gap: 8px;
  height: 40px !important;
  min-width: 104px;
  padding: 0 16px !important;
  border-radius: 10px !important;
  font-size: 14px !important;
  font-weight: 600 !important;
  line-height: 1 !important;
  transition: transform 150ms ease, box-shadow 150ms ease, background-color 150ms ease, border-color 150ms ease !important;
}

:deep(.btn-cancel) {
  border-color: #cbd5e1 !important;
  color: #475569 !important;
  background: var(--color-surface) !important;
}

:deep(.btn-cancel:hover) {
  transform: translateY(-1px);
  border-color: var(--status-primary) !important;
  color: var(--status-primary) !important;
  background: #f8fafc !important;
}

:deep(.btn-save) {
  border: 1px solid color-mix(in srgb, var(--status-primary) 82%, #0369a1) !important;
  background: linear-gradient(135deg, var(--status-primary), color-mix(in srgb, var(--status-primary) 78%, #0369a1)) !important;
  box-shadow: 0 5px 14px color-mix(in srgb, var(--status-primary) 24%, transparent) !important;
}

:deep(.btn-save:hover) {
  transform: translateY(-1px);
  background: linear-gradient(135deg, color-mix(in srgb, var(--status-primary) 88%, #ffffff), var(--status-primary)) !important;
  box-shadow: 0 8px 18px color-mix(in srgb, var(--status-primary) 30%, transparent) !important;
}

[data-theme='dark'] .current-status-box {
  border-color: var(--color-border) !important;
  background: color-mix(in srgb, var(--color-surface-hover) 54%, var(--color-surface)) !important;
}

[data-theme='dark'] .status-icon-shell,
[data-theme='dark'] .preset-btn,
[data-theme='dark'] .emoji-selector-trigger {
  border-color: var(--color-border);
  color: var(--color-text-secondary);
  background: var(--color-surface-hover);
}

[data-theme='dark'] .preview-text,
[data-theme='dark'] .preset-btn,
[data-theme='dark'] .preset-btn:hover,
[data-theme='dark'] .preset-btn.active {
  color: var(--color-text-primary);
}

[data-theme='dark'] .preset-btn:hover,
[data-theme='dark'] .preset-btn.active {
  background: color-mix(in srgb, var(--status-primary) 11%, var(--color-surface));
}

/* The shared popup contract is the source of truth. These final selectors
 * neutralize legacy scoped declarations kept above for the status content. */
:deep(.status-update-dialog .el-dialog__header) {
  min-height: 92px !important;
  padding: 16px 24px !important;
}

:deep(.status-update-dialog .el-dialog__body) {
  padding: 0 24px 24px !important;
}

.status-content {
  gap: 0 !important;
}

@media (max-width: 540px) {
  :deep(.status-update-dialog .el-dialog__header),
  :deep(.status-update-dialog .el-dialog__body),
  :deep(.status-update-dialog .el-dialog__footer) {
    padding-left: 16px !important;
    padding-right: 16px !important;
  }

  .heading-copy h2 {
    font-size: 19px;
  }

  .presets-grid {
    grid-template-columns: 1fr;
  }

  .current-status-preview {
    align-items: flex-start;
    flex-wrap: wrap;
  }

  .delete-btn {
    margin-left: 50px !important;
  }

  .dialog-footer {
    width: 100%;
  }

  :deep(.btn-cancel),
  :deep(.btn-save),
  .status-save-button {
    flex: 1;
    min-width: 0;
  }
}
</style>

