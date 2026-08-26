<template>
  <div ref="root" class="toolbar-sort-menu">
    <button
      type="button"
      class="timeline-filter-trigger icon-only-trigger"
      :class="{ active: open }"
      :title="label"
      :aria-label="label"
      :aria-expanded="open"
      @click="toggleOpen"
    >
      <i :class="direction === 'asc' ? 'fa-solid fa-arrow-up-short-wide' : 'fa-solid fa-arrow-down-short-wide'"></i>
    </button>

    <div v-if="open" class="toolbar-sort-panel" @click.stop>
      <label class="sort-search">
        <i class="fa-solid fa-magnifying-glass"></i>
        <input v-model="query" type="search" placeholder="Search sort fields..." />
      </label>
      <div class="sort-section-label">SORT BY</div>
      <div class="sort-combobox">
        <div role="button" tabindex="0" class="sort-select-trigger" :class="{ active: fieldOpen }" @click="toggleField" @keydown.enter="toggleField">
          <i :class="selectedOption?.icon || 'fa-solid fa-arrow-down-wide-short'"></i>
          <span>{{ selectedOption?.label || label }}</span>
          <div class="sort-direction-inline">
            <button type="button" :class="{ selected: direction === 'asc' }" title="Ascending" @click.stop="setDirection('asc')"><i class="fa-solid fa-arrow-up-short-wide"></i></button>
            <button type="button" :class="{ selected: direction === 'desc' }" title="Descending" @click.stop="setDirection('desc')"><i class="fa-solid fa-arrow-down-short-wide"></i></button>
          </div>
          <i class="fa-solid fa-chevron-down chevron" :class="{ rotated: fieldOpen }"></i>
        </div>
        <div v-if="fieldOpen" class="sort-options-list">
          <button v-for="option in filteredOptions" :key="option.value" type="button" class="sort-option" :class="{ selected: modelValue === option.value }" @click="select(option.value)">
            <i :class="option.icon || 'fa-solid fa-arrow-down-wide-short'"></i>
            <span>{{ option.label }}</span>
            <i v-if="modelValue === option.value" class="fa-solid fa-check sort-check"></i>
          </button>
          <div v-if="filteredOptions.length === 0" class="sort-empty">No matching fields</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

const props = defineProps({
  label: { type: String, default: 'Sort' },
  options: { type: Array, default: () => [] },
  modelValue: { type: String, default: '' },
  direction: { type: String, default: 'desc' }
})

const emit = defineEmits(['update:modelValue', 'update:direction'])
const open = ref(false)
const fieldOpen = ref(false)
const query = ref('')
const root = ref(null)
const select = (value) => {
  emit('update:modelValue', value)
  fieldOpen.value = false
}
const setDirection = (value) => {
  emit('update:direction', value)
}
const toggleField = () => {
  fieldOpen.value = !fieldOpen.value
}
const toggleOpen = () => {
  open.value = !open.value
  if (open.value) {
    fieldOpen.value = false
    query.value = ''
    window.dispatchEvent(new CustomEvent('toolbar-popup-open', { detail: root.value }))
  }
}
const selectedOption = computed(() => props.options.find(option => option.value === props.modelValue))
const filteredOptions = computed(() => {
  const normalized = query.value.trim().toLowerCase()
  return normalized ? props.options.filter(option => `${option.label || ''}`.toLowerCase().includes(normalized)) : props.options
})
const closeOutside = (event) => {
  if (event.type === 'toolbar-popup-open') {
    if (event.detail !== root.value) open.value = false
    return
  }
  if (!root.value?.contains(event.target)) open.value = false
}
onMounted(() => document.addEventListener('click', closeOutside))
onMounted(() => window.addEventListener('toolbar-popup-open', closeOutside))
onBeforeUnmount(() => {
  document.removeEventListener('click', closeOutside)
  window.removeEventListener('toolbar-popup-open', closeOutside)
})
</script>

<style scoped>
.toolbar-sort-menu { position: relative; display: inline-flex; }
.toolbar-sort-menu > button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 42px;
  min-width: 42px;
  height: 34px;
  padding: 0;
  border: 1px solid var(--color-border);
  border-radius: 9px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: border-color .2s ease, background .2s ease, color .2s ease;
}
.toolbar-sort-menu > button:hover, .toolbar-sort-menu > button.active {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border));
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface));
  color: var(--color-accent);
}
.toolbar-sort-panel { position: absolute; top: calc(100% + 8px); left: 0; z-index: 1200; width: 420px; padding: 10px; border: 1px solid var(--color-border); border-radius: 12px; background: var(--color-surface); box-shadow: 0 18px 42px rgba(15, 23, 42, .18); }
.sort-search { display: flex; align-items: center; gap: 9px; height: 34px; padding: 0 10px; border: 1px solid var(--color-border); border-radius: 9px; color: var(--color-text-muted); }
.sort-search:focus-within { border-color: var(--color-accent); box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-accent) 16%, transparent); }
.sort-search input { display: block !important; min-width: 0 !important; width: 100% !important; height: 100% !important; padding: 0 !important; border: 0 !important; border-radius: 0 !important; outline: 0 !important; box-shadow: none !important; background: transparent !important; color: var(--color-text-primary) !important; font: inherit; font-size: 13px !important; line-height: 34px !important; -webkit-appearance: none; appearance: none; }
.sort-section-label { padding: 12px 3px 7px; color: var(--color-text-muted); font-size: 11px; font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }
.sort-combobox { position: relative; }
.sort-select-trigger { display: flex !important; align-items: center !important; justify-content: flex-start !important; width: 100%; height: 38px !important; min-height: 38px !important; gap: 8px !important; padding: 0 9px !important; border: 1px solid var(--color-border) !important; border-radius: 9px; background: var(--color-surface) !important; color: var(--color-text-primary) !important; font: inherit; font-size: 13px !important; text-align: left; cursor: pointer; }
.sort-select-trigger:hover, .sort-select-trigger.active { border-color: var(--color-accent) !important; background: color-mix(in srgb, var(--color-accent) 7%, var(--color-surface)) !important; color: var(--color-accent) !important; }
.sort-select-trigger > i:first-child { width: 15px; color: var(--color-text-secondary) !important; text-align: center; }
.sort-select-trigger > span { flex: 1 1 auto; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; text-align: left; }
.sort-select-trigger:hover > i:first-child,
.sort-select-trigger.active > i:first-child,
.sort-select-trigger:hover > span,
.sort-select-trigger.active > span,
.sort-select-trigger:hover .chevron,
.sort-select-trigger.active .chevron { color: var(--color-accent) !important; }
.sort-select-trigger .chevron { margin-left: auto; color: var(--color-text-muted) !important; transition: transform .18s ease, color .18s ease; }
.sort-select-trigger .chevron.rotated { transform: rotate(180deg); }
.sort-direction-inline { display: inline-flex; align-items: center; gap: 4px; margin-left: auto; }
.sort-direction-inline button { display: inline-flex !important; align-items: center !important; justify-content: center !important; width: 32px !important; height: 30px !important; min-height: 30px !important; padding: 0 !important; border: 1px solid var(--color-border) !important; border-radius: 8px; background: var(--color-surface) !important; color: var(--color-text-secondary) !important; cursor: pointer; }
.sort-direction-inline button:hover, .sort-direction-inline button.selected { border-color: var(--color-accent) !important; background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important; color: var(--color-accent) !important; }
.sort-options-list { position: absolute; top: calc(100% + 5px); left: 0; right: 0; z-index: 2; max-height: 210px; overflow-y: auto; padding: 6px !important; display: flex; flex-direction: column; gap: 0 !important; border: 1px solid var(--color-border); border-radius: 10px; background: var(--color-surface); box-shadow: 0 14px 32px rgba(15, 23, 42, .16); }
.sort-option { display: flex !important; align-items: center !important; justify-content: flex-start !important; width: 100%; min-height: 32px !important; height: auto !important; gap: 8px !important; padding: 5px 9px !important; margin: 0 !important; border: 0 !important; border-left: 4px solid transparent !important; border-radius: 8px !important; background: transparent !important; color: var(--color-text-secondary) !important; font: inherit; font-size: 13px !important; text-align: left; cursor: pointer; }
.sort-option > i:first-child { width: 15px; color: currentColor !important; text-align: center; }
.sort-option > span { flex: 1 1 auto; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; text-align: left; }
.sort-option:hover { background: var(--color-surface-hover) !important; color: var(--color-accent) !important; }
.sort-option:hover > i:first-child { color: var(--color-accent) !important; }
.sort-option.selected { border-left-color: var(--color-accent) !important; border-radius: 8px !important; background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface)) !important; color: var(--color-accent) !important; font-weight: 650; }
.sort-option.selected > i:first-child { color: var(--color-accent) !important; }
.sort-option.selected:hover { background: color-mix(in srgb, var(--color-accent) 18%, var(--color-surface)) !important; color: var(--color-accent) !important; }
.sort-check { margin-left: auto; color: var(--color-accent) !important; }
.sort-empty { padding: 10px; color: var(--color-text-muted); font-size: 12px; }
.direction-toggle { display: flex; gap: 7px; }
.direction-toggle button { display: inline-flex; align-items: center; justify-content: center; flex: 1; height: 32px; gap: 7px; border: 1px solid var(--color-border); border-radius: 8px; background: var(--color-surface); color: var(--color-text-secondary); font: inherit; font-size: 11px; font-weight: 800; cursor: pointer; }
.direction-toggle button:hover, .direction-toggle button.selected { border-color: var(--color-accent); background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)); color: var(--color-accent); }
</style>
