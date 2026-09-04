<template>
  <div class="project-empty-state">
    <div class="empty-icon-wrapper">
      <i :class="icon"></i>
    </div>
    <h3 class="empty-title">{{ title }}</h3>
    <p class="empty-desc">{{ description }}</p>
    <div v-if="$slots.action" class="empty-action">
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
.project-empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 64px 24px;
  background: var(--color-surface, #ffffff);
  border: 1px dashed var(--color-border, #dfe1e6);
  border-radius: 12px;
  margin: 0 auto;
  width: 100%;
  max-width: 600px;
}
.empty-icon-wrapper {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  background: rgba(9, 30, 66, 0.04);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 28px;
  color: var(--color-text-muted, #6b778c);
  margin-bottom: 24px;
}
.empty-title {
  margin: 0 0 8px 0;
  font-size: 18px;
  font-weight: 600;
  color: var(--color-text-primary, #172b4d);
}
.empty-desc {
  margin: 0 0 24px 0;
  font-size: 14px;
  color: var(--color-text-secondary, #42526e);
  max-width: 400px;
}
.empty-action {
  display: flex;
  justify-content: center;
}

/* Ensure button inside action slot is standardized */
:deep(.empty-action button) {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  height: 36px;
  padding: 0 16px;
  font-size: 14px;
  font-weight: 500;
  border-radius: 6px;
}
</style>
