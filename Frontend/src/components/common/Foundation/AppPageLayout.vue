<template>
  <div class="sprinta-layout-container">
    <div :class="fluid ? 'sprinta-layout-fluid' : 'sprinta-layout-boxed'" class="sprinta-layout-surface">
      <div v-if="$slots.header" class="sprinta-layout-header">
        <slot name="header" />
      </div>
      <div v-if="$slots.toolbar" class="sprinta-layout-toolbar" style="margin-top: 16px;">
        <slot name="toolbar" />
      </div>
      <div class="sprinta-layout-content" style="margin-top: 24px;">
        <div style="display: flex; gap: 32px;">
          <div style="flex: 1; min-width: 0;">
            <slot />
          </div>
          <div v-if="withSidebar" style="width: 300px; flex-shrink: 0;">
            <slot name="sidebar" />
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
defineProps({
  fluid: {
    type: Boolean,
    default: false
  },
  withSidebar: {
    type: Boolean,
    default: false
  }
})
</script>
