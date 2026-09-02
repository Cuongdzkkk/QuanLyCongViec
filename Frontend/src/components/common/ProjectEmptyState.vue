<template>
  <div class="empty-state-global">
    <div class="empty-spaces-icon" aria-hidden="true">
      <i :class="icon"></i>
    </div>
    <div class="empty-spaces-copy">
      <h3>{{ title }}</h3>
      <p>{{ description }}</p>
    </div>
    <div v-if="$slots.action" class="empty-spaces-actions">
      <slot name="action"></slot>
    </div>
  </div>
</template>

<script setup>
defineProps({
  title: { type: String, required: true },
  description: { type: String, default: '' },
  icon: { type: String, default: 'fa-regular fa-folder-open' }
})
</script>

<style scoped>
/* Scoped styles removed in favor of global .empty-spaces-flat classes */
</style>
