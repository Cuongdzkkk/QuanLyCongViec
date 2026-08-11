<template>
  <div class="avatar-picker">
    <div class="picker-search">
      <i class="bi bi-search"></i>
      <input v-model="query" type="search" placeholder="Search icons..." aria-label="Search project icons" />
    </div>

    <div class="icon-scroll-area">
      <section v-for="group in filteredGroups" :key="group.id" class="icon-group">
        <h4>{{ group.label }}</h4>
        <div class="icon-grid">
          <button
            v-for="icon in group.icons"
            :key="`${group.id}-${icon}`"
            type="button"
            class="icon-option"
            :class="{ selected: modelValue === icon }"
            :title="icon"
            :aria-label="icon"
            :aria-pressed="modelValue === icon"
            @click="$emit('update:modelValue', icon)"
          >
            <i :class="`bi bi-${icon}`"></i>
            <i v-if="modelValue === icon" class="bi bi-check selection-check"></i>
          </button>
        </div>
      </section>

      <p v-if="filteredGroups.length === 0" class="picker-empty">No matching icons.</p>
    </div>
  </div>
</template>

<script setup>
import { computed, ref } from 'vue'
import { PROJECT_ICON_CATEGORIES } from '@/config/projectAppearance'

defineProps({ modelValue: { type: String, required: true } })
defineEmits(['update:modelValue'])

const query = ref('')
const filteredGroups = computed(() => {
  const keyword = query.value.trim().toLowerCase()
  if (!keyword) return PROJECT_ICON_CATEGORIES
  return PROJECT_ICON_CATEGORIES
    .map(group => ({ ...group, icons: group.icons.filter(icon => icon.includes(keyword)) }))
    .filter(group => group.icons.length)
})
</script>

<style scoped>
.avatar-picker { display: grid; grid-template-rows: auto minmax(0, 1fr); gap: 14px; min-height: 0; }
.picker-search { position: relative; display: flex; align-items: center; }
.picker-search > i {
  position: absolute; z-index: 1; left: 14px; top: 50%; color: var(--color-text-muted); font-size: 14px;
  line-height: 1; pointer-events: none; transform: translateY(-50%);
}
.picker-search input {
  width: 100%; height: 42px; padding: 0 14px 0 42px !important; border: 1px solid var(--color-border);
  border-radius: 10px; background: var(--color-surface); color: var(--color-text-primary); outline: none;
}
.picker-search input:focus { border-color: var(--color-accent); box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-accent) 12%, transparent); }
.icon-scroll-area { min-height: 0; max-height: 320px; overflow: auto; padding-right: 4px; scrollbar-width: thin; }
.icon-group + .icon-group { margin-top: 18px; }
.icon-group h4 { margin: 0 0 9px; color: var(--color-text-secondary); font-size: 11px; font-weight: 800; text-transform: uppercase; }
.icon-grid { display: grid; grid-template-columns: repeat(8, minmax(0, 1fr)); gap: 7px; }
.icon-option {
  position: relative; aspect-ratio: 1; min-width: 0; border: 1px solid var(--color-border); border-radius: 9px;
  background: var(--color-surface); color: var(--color-text-secondary); font-size: 18px; cursor: pointer;
  transition: transform .18s ease, border-color .18s ease, background .18s ease, color .18s ease;
}
.icon-option:hover { transform: scale(1.05); border-color: color-mix(in srgb, var(--color-accent) 54%, var(--color-border)); color: var(--color-accent); }
.icon-option.selected { border-color: var(--color-accent); background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)); color: var(--color-accent); box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-accent) 10%, transparent); }
.selection-check { position: absolute; right: 2px; bottom: 1px; font-size: 10px; font-weight: 900; }
.picker-empty { margin: 20px 0; text-align: center; color: var(--color-text-muted); font-size: 13px; }
@media (max-width: 640px) { .icon-grid { grid-template-columns: repeat(6, minmax(0, 1fr)); } }
</style>
