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
  padding: 48px 24px;
}

.empty-icon {
  font-size: 48px;
  color: var(--sp-text-secondary, #A5ADBA);
  margin-bottom: 16px;
}

.empty-title {
  margin: 0 0 8px 0;
  font-size: 18px;
  font-weight: 500;
  color: var(--sp-text-primary, #172B4D);
}

.empty-description {
  margin: 0 0 24px 0;
  font-size: 14px;
  color: var(--sp-text-secondary, #5E6C84);
  max-width: 400px;
}

.empty-actions {
  display: flex;
  gap: 12px;
}
</style>
