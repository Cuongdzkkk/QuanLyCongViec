<template>
  <div class="sprinta-empty-state">
    <div class="empty-icon" v-if="icon || $slots.icon">
      <slot name="icon">
        <i :class="icon"></i>
      </slot>
    </div>
    <h3 class="empty-title">{{ title }}</h3>
    <p class="empty-description" v-if="description">{{ description }}</p>
    <div class="empty-actions" v-if="$slots.action">
      <slot name="action" />
    </div>
  </div>
</template>

<script setup>
defineProps({
  icon: {
    type: String,
    default: 'fa-solid fa-inbox'
  },
  title: {
    type: String,
    default: 'Không có dữ liệu'
  },
  description: {
    type: String,
    default: ''
  }
})
</script>

<style scoped>
.sprinta-empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 56px 24px;
  min-height: 260px;
}

.empty-icon {
  display: grid;
  place-items: center;
  width: 54px;
  height: 54px;
  border: 1px solid #bde7fb;
  border-radius: 14px;
  background: #eaf8ff;
  color: #0ea5e9;
  font-size: 25px;
  margin-bottom: 16px;
  box-shadow: 0 4px 12px rgba(14, 165, 233, .08);
}

.empty-title {
  margin: 0 0 8px 0;
  font-size: 16px;
  font-weight: 700;
  color: var(--sp-text-primary, #172B4D);
}

.empty-description {
  margin: 0 0 24px 0;
  font-size: 13px;
  color: var(--sp-text-secondary, #5E6C84);
  max-width: 400px;
}

.empty-actions {
  display: flex;
  gap: 12px;
}

.empty-actions :deep(button) {
  min-height: 36px;
  padding: 7px 16px;
  border: 1px solid #8bd5f7;
  border-radius: 8px;
  background: #fff;
  color: #0795d3;
  font: inherit;
  font-size: 13px;
  font-weight: 700;
  cursor: pointer;
  transition: .15s ease;
}

.empty-actions :deep(button:hover) {
  background: #0ea5e9;
  border-color: #0ea5e9;
  color: #fff;
}

.empty-actions :deep(button:active) {
  background: #0284c7;
  border-color: #0284c7;
  color: #fff;
}
</style>
