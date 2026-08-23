<template>
  <div class="sprinta-page-header-block app-shell-page-header" style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px;">
    <div class="header-left">
      <div v-if="$slots.breadcrumb || back" class="header-breadcrumb" style="font-size: 14px; color: var(--sp-text-secondary); margin-bottom: 8px;">
        <slot name="breadcrumb">
          <span v-if="back" @click="$emit('back')" style="cursor: pointer;">
            <i class="fa-solid fa-arrow-left" style="margin-right: 4px;"></i> Quay lại
          </span>
        </slot>
      </div>
      <div class="app-shell-title-wrap">
        <h1>{{ title }}</h1>
        <div class="app-shell-header-help">
          <span class="app-shell-header-help-btn" :aria-label="`About ${title}`">
            <i class="fa-solid fa-question"></i>
          </span>
          <div class="app-shell-header-help-popover" role="tooltip">
            <span>{{ title }}</span>
            <p>
              <slot name="subtitle">{{ subtitle || title }}</slot>
            </p>
          </div>
        </div>
      </div>
      <p v-if="subtitle || $slots.subtitle" class="app-shell-header-subtitle">
        <slot name="subtitle">{{ subtitle }}</slot>
      </p>
    </div>
    <div v-if="$slots.actions" class="header-actions" style="display: flex; gap: 8px; align-items: center;">
      <slot name="actions" />
    </div>
    <div v-if="$slots.bottom" class="app-page-header-bottom">
      <slot name="bottom" />
    </div>
  </div>
</template>

<script setup>
defineProps({
  title: {
    type: String,
    required: true
  },
  subtitle: {
    type: String,
    default: ''
  },
  back: {
    type: Boolean,
    default: false
  }
})
defineEmits(['back'])
</script>
