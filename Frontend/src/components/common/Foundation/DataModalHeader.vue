<template>
  <div class="sa-modal-heading">
    <span v-if="icon" class="sa-modal-heading__icon" aria-hidden="true">
      <i :class="icon"></i>
    </span>
    <div class="sa-modal-heading__copy">
      <h2 :id="titleId" class="sa-modal-heading__title">{{ title }}</h2>
      <p v-if="description" class="sa-modal-heading__description">{{ description }}</p>
    </div>
    <button
      v-if="closable"
      type="button"
      class="sa-modal-heading__close"
      :aria-label="closeLabel"
      :title="closeLabel"
      @click="$emit('close')"
    >
      <i class="bi bi-x-lg" aria-hidden="true"></i>
    </button>
  </div>
</template>

<script setup>
defineProps({
  title: { type: String, required: true },
  description: { type: String, default: '' },
  icon: { type: String, default: '' },
  titleId: { type: String, default: undefined },
  closable: { type: Boolean, default: true },
  closeLabel: { type: String, default: 'Đóng' }
})

defineEmits(['close'])
</script>

<style scoped>
.sa-modal-heading {
  width: 100%;
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 14px;
}

.sa-modal-heading__icon {
  width: 40px;
  height: 40px;
  flex: 0 0 40px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 11px;
  color: var(--color-accent);
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface));
}

.sa-modal-heading__icon i {
  font-size: 20px;
  line-height: 1;
}

.sa-modal-heading__copy {
  min-width: 0;
  flex: 1;
}

.sa-modal-heading__title {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 24px;
  font-weight: 600;
  line-height: 1.35;
}

.sa-modal-heading__description {
  margin: 3px 0 0;
  color: var(--color-text-muted);
  font-size: 13px;
  line-height: 1.4;
}

.sa-modal-heading__close {
  width: 36px;
  height: 36px;
  flex: 0 0 36px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  border: 1px solid color-mix(in srgb, var(--color-danger) 86%, #b91c1c);
  border-radius: 10px;
  color: #fff;
  background: var(--color-danger);
  box-shadow: 0 5px 14px color-mix(in srgb, var(--color-danger) 24%, transparent);
  cursor: pointer;
  transition: transform 180ms ease, background-color 180ms ease, box-shadow 180ms ease;
}

.sa-modal-heading__close:hover {
  transform: translateY(-1px);
  background: #dc2626;
  box-shadow: 0 7px 18px color-mix(in srgb, var(--color-danger) 32%, transparent);
}

.sa-modal-heading__close:focus-visible {
  outline: none;
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-danger) 24%, transparent);
}
</style>
