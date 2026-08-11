<template>
  <el-dialog
    :model-value="modelValue"
    @update:model-value="$emit('update:modelValue', $event)"
    :width="resolvedWidth"
    :destroy-on-close="destroyOnClose"
    :close-on-click-modal="closeOnOverlayClick"
    :lock-scroll="preventBackgroundScroll"
    :modal-class="overlayClass"
    :style="modalStyle"
    :class="['sprinta-app-modal', 'sa-data-dialog', sizeClass]"
    :show-close="false"
    @close="$emit('close')"
    @closed="$emit('closed')"
    append-to-body
  >
    <template #header>
      <DataModalHeader
        :icon="icon"
        :title="title"
        :description="subtitle"
        :close-label="closeLabel"
        @close="handleCancel"
      />
    </template>
    
    <div :class="['sprinta-app-modal-body', bodyClassName]">
      <slot></slot>
    </div>

    <template #footer v-if="showFooter || $slots.footer">
      <span class="dialog-footer sprint-app-modal-footer">
        <slot name="footer">
          <el-button @click="handleCancel" class="cancel-btn">{{ cancelText }}</el-button>
          <el-button type="primary" @click="handleConfirm" :loading="loading" class="primary-btn">
            {{ confirmText }}
          </el-button>
        </slot>
      </span>
    </template>
  </el-dialog>
</template>

<script setup>
import { computed } from 'vue'
import DataModalHeader from './DataModalHeader.vue'

const props = defineProps({
  modelValue: {
    type: Boolean,
    required: true
  },
  title: {
    type: String,
    default: ''
  },
  subtitle: {
    type: String,
    default: ''
  },
  icon: {
    type: String,
    default: 'bi bi-pencil-square'
  },
  closeLabel: {
    type: String,
    default: 'Đóng'
  },
  size: {
    type: String,
    default: 'medium',
    validator: value => ['small', 'medium', 'form', 'large'].includes(value)
  },
  width: {
    type: String,
    default: ''
  },
  topOffset: {
    type: String,
    default: ''
  },
  overlayVariant: {
    type: String,
    default: 'subtle',
    validator: value => ['none', 'subtle', 'blur'].includes(value)
  },
  closeOnOverlayClick: {
    type: Boolean,
    default: false
  },
  preventBackgroundScroll: {
    type: Boolean,
    default: true
  },
  bodyClassName: {
    type: String,
    default: ''
  },
  loading: {
    type: Boolean,
    default: false
  },
  confirmText: {
    type: String,
    default: 'Xác nhận'
  },
  cancelText: {
    type: String,
    default: 'Hủy'
  },
  showFooter: {
    type: Boolean,
    default: true
  },
  destroyOnClose: {
    type: Boolean,
    default: true
  }
})

const emit = defineEmits(['update:modelValue', 'confirm', 'cancel', 'close', 'closed'])

const widthBySize = {
  small: '440px',
  medium: '560px',
  form: '680px',
  large: '760px'
}

const resolvedWidth = computed(() => props.width || widthBySize[props.size])
const modalStyle = computed(() => ({
  '--sa-modal-instance-width': resolvedWidth.value,
  ...(props.topOffset ? { '--sa-modal-top-offset': props.topOffset } : {})
}))
const sizeClass = computed(() => `sa-modal--${props.size === 'medium' ? 'md' : props.size}`)
const overlayClass = computed(() => [
  'sa-data-modal-overlay',
  `sa-modal--${props.size === 'medium' ? 'md' : props.size}`,
  props.overlayVariant === 'blur' ? 'sa-modal--blur' : '',
  props.overlayVariant === 'none' ? 'sa-modal--clear' : ''
].filter(Boolean).join(' '))

const handleConfirm = () => {
  emit('confirm')
}

const handleCancel = () => {
  emit('cancel')
  emit('update:modelValue', false)
}
</script>

<style>
/* Global overrides for this specific modal instance */
.sprinta-app-modal {
  border-radius: var(--sp-radius-lg, 8px) !important;
  box-shadow: 0 8px 16px -4px rgba(9, 30, 66, 0.25) !important;
  padding: 0 !important;
}

.sprinta-app-modal .el-dialog__header {
  padding: 20px 24px;
  margin-right: 0;
  border-bottom: 1px solid var(--sp-border-color, #DFE1E6);
  font-size: 20px;
  font-weight: 500;
  color: var(--sp-text-primary, #172B4D);
}

.sprinta-app-modal .el-dialog__body {
  padding: 24px;
  color: var(--sp-text-primary, #172B4D);
}

.sprinta-app-modal .el-dialog__footer {
  padding: 16px 24px;
  border-top: 1px solid var(--sp-border-color, #DFE1E6);
  margin-top: 0;
}

.sprinta-app-modal-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.sprinta-app-modal-subtitle {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 13px;
  line-height: 20px;
}

.sprint-app-modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.sprint-app-modal-footer .cancel-btn {
  background: var(--color-danger);
  color: var(--color-text-inverse);
  border: 1px solid var(--color-danger);
  font-weight: 500;
}

.sprint-app-modal-footer .cancel-btn:hover {
  background: color-mix(in srgb, var(--color-danger) 88%, var(--color-text-primary));
}

.sprint-app-modal-footer .primary-btn {
  background-color: var(--color-accent);
  color: var(--color-text-inverse);
  border: none;
  font-weight: 500;
}

.sprint-app-modal-footer .primary-btn:hover {
  background-color: var(--color-accent-hover);
}
</style>
