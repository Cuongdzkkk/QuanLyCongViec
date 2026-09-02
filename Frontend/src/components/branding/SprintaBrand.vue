<script setup>
import logoUrl from '@/assets/logo_QLCV.png'

defineProps({
  size: {
    type: String,
    default: 'compact',
    validator: value => ['compact', 'inline', 'dialog', 'site-selection'].includes(value)
  },
  showName: {
    type: Boolean,
    default: true
  }
})
</script>

<template>
  <span class="sprinta-brand" :class="`sprinta-brand--${size}`">
    <img class="sprinta-brand__image" :src="logoUrl" alt="SprintA logo" />
    <span v-if="showName" class="sprinta-brand__name">SprintA</span>
  </span>
</template>

<style scoped>
.sprinta-brand {
  display: inline-flex;
  align-items: center;
  min-width: max-content;
  line-height: 1;
}

.sprinta-brand__image {
  display: block;
  width: auto;
  object-fit: contain;
  flex: 0 0 auto;
}

.sprinta-brand__name {
  white-space: nowrap;
  opacity: 1;
  filter: none;
  -webkit-font-smoothing: antialiased;
  text-rendering: geometricPrecision;
}

/* These dimensions preserve the exact source treatment used by /site-selection. */
.sprinta-brand--site-selection {
  gap: 0;
  margin-left: 24px;
}

.sprinta-brand--site-selection .sprinta-brand__image {
  height: 40px;
  transform: scale(4);
  margin-right: 8px;
}

.sprinta-brand--site-selection .sprinta-brand__name {
  color: #f7fbff;
  font-size: 24px;
  font-weight: bold;
  letter-spacing: -0.5px;
}

.sprinta-brand--inline {
  gap: 0;
}

.sprinta-brand--inline .sprinta-brand__image {
  height: 16px;
  transform: scale(4);
  margin: 0 16px;
}

.sprinta-brand--compact {
  gap: 8px;
}

.sprinta-brand--compact .sprinta-brand__image {
  height: 24px;
  transform: scale(4);
}

.sprinta-brand--compact .sprinta-brand__name {
  color: inherit;
  font-size: 20px;
  font-weight: 700;
  letter-spacing: -0.5px;
}

.sprinta-brand--dialog {
  gap: 12px;
}

.sprinta-brand--dialog .sprinta-brand__image {
  height: 40px;
  transform: scale(4);
}

.sprinta-brand--dialog .sprinta-brand__name {
  color: inherit;
  font-size: 26px;
  font-weight: 800;
}

@media (max-width: 680px) {
  .sprinta-brand--compact {
    gap: 6px;
  }

  .sprinta-brand--compact .sprinta-brand__image {
    height: 20px;
  }

  .sprinta-brand--compact .sprinta-brand__name {
    font-size: 16px;
  }
}
</style>
