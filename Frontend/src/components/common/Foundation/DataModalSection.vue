<template>
  <section class="sa-modal-section" :aria-label="title">
    <p v-if="description" class="sa-modal-section__description">{{ description }}</p>
    <div class="sa-modal-section__content">
      <slot />
    </div>
  </section>
</template>

<script setup>
defineProps({
  title: { type: String, required: true },
  description: { type: String, default: '' },
  // Kept for compatibility while section decoration is intentionally hidden.
  icon: { type: String, default: '' }
})
</script>
