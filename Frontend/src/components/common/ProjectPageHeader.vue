<template>
  <header class="project-page-header">
    <div class="pph-left">
      <div v-if="icon" class="pph-icon">
        <i :class="icon"></i>
      </div>
      <div class="pph-content">
        <h1 class="pph-title">{{ title }}</h1>
        <p v-if="description" class="pph-desc">{{ description }}</p>
      </div>
    </div>
    <div class="pph-right" v-if="$slots.actions">
      <slot name="actions"></slot>
    </div>
  </header>
</template>

<script setup>
defineProps({
  title: { type: String, required: true },
  description: { type: String, default: '' },
  icon: { type: String, default: '' }
})
</script>

<style scoped>
.project-page-header {
  display: flex;
  flex: 0 0 auto;
  justify-content: space-between;
  align-items: center;
  gap: 14px;
  min-height: 46px;
  padding: 0 0 8px;
  overflow: visible;
}
.pph-left {
  display: flex;
  flex: 1 1 auto;
  align-items: center;
  gap: 12px;
  min-width: 0;
  min-height: 46px;
  overflow: visible;
}
.pph-icon {
  width: 46px;
  height: 46px;
  border-radius: 12px;
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--color-accent) 18%, var(--color-surface)), var(--color-surface));
  border: 1px solid color-mix(in srgb, var(--color-accent) 24%, var(--color-border));
  color: var(--color-accent);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 17px;
  flex-shrink: 0;
  box-shadow: 0 12px 28px color-mix(in srgb, var(--color-accent) 10%, transparent);
}
.pph-content {
  display: flex;
  flex-direction: column;
  flex: 1 1 auto;
  gap: 2px;
  min-width: 0;
  padding-block: 2px;
  overflow: visible;
}
.pph-title {
  margin: 0;
  font-size: clamp(22px, 1.7vw, 28px);
  font-weight: 900;
  color: var(--color-text-primary, #172b4d);
  line-height: 1.2;
  overflow-wrap: anywhere;
}
.pph-desc {
  margin: 0;
  font-size: 13px;
  color: var(--color-text-muted, #6b778c);
  line-height: 1.35;
}
.pph-right {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-shrink: 0;
}

/* Ensure the action buttons passed into slot match the uniform design */
:deep(.pph-right button) {
  display: flex !important;
  align-items: center !important;
  justify-content: center !important;
  gap: 8px !important;
  height: 34px !important;
  padding: 0 14px !important;
  font-size: 13px !important;
  font-weight: 500 !important;
  border-radius: 6px !important;
}
:deep(.pph-right button i) {
  font-size: 14px !important;
}
</style>
