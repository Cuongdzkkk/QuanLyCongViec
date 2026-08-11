<template>
  <div class="sa-modal-field" :class="{ 'is-required': required }">
    <label v-if="label" :for="forId">
      {{ label }}<span v-if="required" class="sa-modal-field__required" aria-hidden="true">*</span>
    </label>
    <slot />
    <p v-if="helper && !error" class="sa-modal-field__helper">{{ helper }}</p>
    <p v-if="error" class="sa-modal-field__error" role="alert">{{ error }}</p>
  </div>
</template>

<script setup>
defineProps({
  label: { type: String, default: '' },
  forId: { type: String, default: undefined },
  helper: { type: String, default: '' },
  error: { type: String, default: '' },
  required: { type: Boolean, default: false }
})
</script>
