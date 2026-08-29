<template>
  <div ref="root" class="toolbar-value-filter">
    <button type="button" class="filter-trigger" :class="{ active: open || hasValue }" :title="label" :aria-label="label" :aria-expanded="open" @click.stop="toggleOpen">
      <i class="fa-solid fa-filter"></i>
      <span v-if="hasValue" class="filter-badge">1</span>
    </button>
    <div v-if="open" class="filter-panel" :class="{ 'wide-filter': fieldOptions.length }" @click.stop>
      <label class="filter-search">
        <i class="fa-solid fa-magnifying-glass"></i>
        <input v-model="query" type="search" :placeholder="searchPlaceholder" />
      </label>
      <div class="filter-section-label">FIELD</div>
      <div class="field-picker">
        <button type="button" class="field-trigger" :class="{ active: fieldOpen }" @click="toggleField">
          <i :class="selectedField?.icon || fieldIcon"></i><span>{{ selectedField?.label || label }}</span><i class="fa-solid fa-chevron-down chevron"></i>
        </button>
        <div v-if="fieldOpen" class="field-options">
          <button v-for="field in fieldOptions" :key="field.value" type="button" class="field-option" :class="{ selected: fieldValue === field.value }" @click="selectField(field.value)">
            <i :class="field.icon || fieldIcon"></i><span>{{ field.label }}</span><i v-if="fieldValue === field.value" class="fa-solid fa-check check"></i>
          </button>
        </div>
      </div>
      <div class="filter-section-label">VALUE</div>
      <div class="value-picker">
        <button type="button" class="value-trigger" :class="{ active: valueOpen }" @click="toggleValue">
          <i :class="selectedOption?.icon || 'fa-solid fa-circle-dot'"></i><span>{{ selectedOption?.label || 'Select value' }}</span><i class="fa-solid fa-chevron-down chevron" :class="{ rotated: valueOpen }"></i>
        </button>
        <div v-if="valueOpen" class="value-list">
          <button v-for="option in filteredOptions" :key="option.value" type="button" class="value-option" :class="{ selected: modelValue === option.value }" @click="select(option.value)">
            <i :class="option.icon || 'fa-solid fa-circle-dot'"></i><span>{{ option.label }}</span><i v-if="modelValue === option.value" class="fa-solid fa-check check"></i>
          </button>
          <div v-if="filteredOptions.length === 0" class="empty-value">No matching values</div>
        </div>
      </div>
      <div v-if="fieldOptions.length" class="active-filters-placeholder">
        <span v-if="hasValue"><i class="fa-solid fa-filter"></i> {{ selectedField?.label || label }}: {{ filteredOptions.find(option => option.value === modelValue)?.label || modelValue }}</span>
        <span v-else>No filters applied</span>
      </div>
      <button v-if="hasValue" type="button" class="clear-filter" @click="select(allValue)"><i class="fa-solid fa-rotate-left"></i> Clear filter</button>
    </div>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

const props = defineProps({
  label: { type: String, default: 'Filter' },
  fieldIcon: { type: String, default: 'fa-solid fa-filter' },
  fieldOptions: { type: Array, default: () => [] },
  fieldValue: { type: String, default: '' },
  options: { type: Array, default: () => [] },
  modelValue: { type: String, default: 'all' },
  allValue: { type: String, default: 'all' },
  searchPlaceholder: { type: String, default: 'Search filter values...' }
})
const emit = defineEmits(['update:modelValue', 'update:fieldValue'])
const open = ref(false)
const fieldOpen = ref(false)
const valueOpen = ref(false)
const query = ref('')
const root = ref(null)
const hasValue = computed(() => props.modelValue !== props.allValue)
const selectedField = computed(() => props.fieldOptions.find(field => field.value === props.fieldValue))
const selectedOption = computed(() => activeOptions.value.find(option => option.value === props.modelValue))
const activeOptions = computed(() => selectedField.value?.options || props.options)
const filteredOptions = computed(() => {
  const q = query.value.trim().toLowerCase()
  return q ? activeOptions.value.filter(option => `${option.label || ''}`.toLowerCase().includes(q)) : activeOptions.value
})
const select = value => {
  emit('update:modelValue', value)
  valueOpen.value = false
}
const selectField = value => {
  emit('update:fieldValue', value)
  fieldOpen.value = false
  valueOpen.value = false
  query.value = ''
}
const toggleField = () => {
  if (!props.fieldOptions.length) return
  fieldOpen.value = !fieldOpen.value
  if (fieldOpen.value) valueOpen.value = false
}
const toggleValue = () => {
  valueOpen.value = !valueOpen.value
  if (valueOpen.value) fieldOpen.value = false
}
const toggleOpen = () => {
  open.value = !open.value
  if (open.value) {
    fieldOpen.value = false
    valueOpen.value = false
    query.value = ''
    window.dispatchEvent(new CustomEvent('toolbar-popup-open', { detail: root.value }))
  }
}
const closeOutside = event => {
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
.toolbar-value-filter { position: relative; display: inline-flex; }
.filter-trigger { position: relative; display: inline-flex; align-items: center; justify-content: center; width: 42px; min-width: 42px; height: 34px; padding: 0; border: 1px solid var(--color-border); border-radius: 9px; background: var(--color-surface); color: var(--color-text-secondary); cursor: pointer; transition: .2s ease; }
.filter-trigger:hover, .filter-trigger.active { border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)); background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)); color: var(--color-accent); }
.filter-badge { position: absolute; top: -6px; right: -6px; display: grid; place-items: center; width: 17px; height: 17px; border-radius: 50%; background: var(--color-accent); color: #fff; font-size: 10px; font-weight: 800; }
.filter-panel { position: absolute; top: calc(100% + 8px); left: 0; z-index: 1200; width: 300px; padding: 10px; border: 1px solid var(--color-border); border-radius: 12px; background: var(--color-surface); box-shadow: 0 18px 42px rgba(15, 23, 42, .18); }
.filter-panel.wide-filter { display: grid; grid-template-columns: 325px minmax(300px, 1fr); column-gap: 18px; align-items: start; width: 800px; min-height: 350px; }
.wide-filter .filter-search { grid-column: 1 / -1; width: 100%; margin-bottom: 12px; }
.wide-filter > .filter-section-label,
.wide-filter > .field-picker,
.wide-filter > .value-picker,
.wide-filter > .clear-filter { grid-column: 1; width: 325px; }
.active-filters-placeholder { grid-column: 2; grid-row: 2 / span 4; width: auto; min-height: 282px; display: flex; align-items: flex-start; padding: 18px 22px; border: 1px solid var(--color-border); border-radius: 10px; color: var(--color-text-muted); font-size: 13px; }
.active-filters-placeholder i { margin-right: 8px; color: var(--color-accent); }
.filter-search { display: flex; align-items: center; gap: 9px; height: 34px; padding: 0 10px; border: 1px solid var(--color-border); border-radius: 9px; color: var(--color-text-muted); }
.filter-search:focus-within { border-color: var(--color-accent); box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-accent) 16%, transparent); }
.filter-search input { display: block !important; width: 100% !important; min-width: 0 !important; height: 100% !important; padding: 0 !important; border: 0 !important; border-radius: 0 !important; outline: 0 !important; box-shadow: none !important; background: transparent !important; color: var(--color-text-primary) !important; font: inherit; font-size: 13px !important; line-height: 34px !important; -webkit-appearance: none; appearance: none; }
.filter-section-label { padding: 12px 3px 7px; color: var(--color-text-muted); font-size: 11px; font-weight: 800; letter-spacing: .04em; }
.field-picker { position: relative; }
.field-trigger { display: flex !important; align-items: center !important; justify-content: flex-start !important; width: 100%; height: 38px !important; min-height: 38px !important; gap: 8px !important; padding: 0 11px !important; border: 1px solid var(--color-border) !important; border-radius: 9px; background: var(--color-surface) !important; color: var(--color-text-primary) !important; font: inherit; font-size: 13px !important; text-align: left; cursor: pointer; }
.field-trigger:hover, .field-trigger.active { border-color: var(--color-accent) !important; background: color-mix(in srgb, var(--color-accent) 7%, var(--color-surface)) !important; color: var(--color-accent) !important; }
.field-trigger > span, .value-trigger > span { flex: 1 1 auto; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; text-align: left; }
.field-trigger > i:first-child { width: 15px; color: var(--color-text-secondary) !important; text-align: center; }.field-trigger:hover > i:first-child, .field-trigger.active > i:first-child, .field-trigger:hover > span, .field-trigger.active > span, .field-trigger:hover .chevron, .field-trigger.active .chevron { color: var(--color-accent) !important; }.field-trigger .chevron { margin-left: auto; color: var(--color-text-muted) !important; font-size: 11px; transition: transform .18s ease, color .18s ease; }.field-trigger.active .chevron { transform: rotate(180deg); }
.field-options { position: absolute; top: calc(100% + 5px); left: 0; right: 0; z-index: 3; display: grid; gap: 0; max-height: 180px; overflow-y: auto; padding: 8px; border: 1px solid var(--color-border); border-radius: 10px; background: var(--color-surface); box-shadow: 0 14px 32px rgba(15, 23, 42, .16); }
.field-option { display: flex !important; align-items: center !important; justify-content: flex-start !important; width: 100%; min-height: 34px !important; height: auto !important; gap: 8px !important; padding: 7px 9px !important; border: 0 !important; border-left: 4px solid transparent !important; border-radius: 8px !important; background: transparent !important; color: var(--color-text-secondary) !important; font: inherit; font-size: 13px !important; text-align: left; cursor: pointer; }.field-option > i:first-child { width: 15px; color: currentColor !important; text-align: center; }.field-option > span { flex: 1 1 auto; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; text-align: left; }.field-option:hover { background: var(--color-surface-hover) !important; color: var(--color-accent) !important; }.field-option.selected { border-left-color: var(--color-accent) !important; border-radius: 2px !important; background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface)) !important; color: var(--color-accent) !important; font-weight: 650; }.field-option.selected:hover { background: color-mix(in srgb, var(--color-accent) 18%, var(--color-surface)) !important; color: var(--color-accent) !important; }
.value-picker { position: relative; }
.value-trigger { display: flex !important; align-items: center !important; justify-content: flex-start !important; width: 100%; height: 38px !important; min-height: 38px !important; gap: 8px !important; padding: 0 11px !important; border: 1px solid var(--color-border) !important; border-radius: 9px; background: var(--color-surface) !important; color: var(--color-text-primary) !important; font: inherit; font-size: 13px !important; text-align: left; cursor: pointer; }.value-trigger > i:first-child { width: 15px; color: var(--color-text-secondary) !important; text-align: center; }.value-trigger .chevron { margin-left: auto; color: var(--color-text-muted) !important; font-size: 11px; transition: transform .18s ease, color .18s ease; }.value-trigger .chevron.rotated { transform: rotate(180deg); }.value-trigger:hover, .value-trigger.active { border-color: var(--color-accent) !important; background: color-mix(in srgb, var(--color-accent) 7%, var(--color-surface)) !important; color: var(--color-accent) !important; }.value-trigger:hover > i:first-child, .value-trigger.active > i:first-child, .value-trigger:hover > span, .value-trigger.active > span, .value-trigger:hover .chevron, .value-trigger.active .chevron { color: var(--color-accent) !important; }
.value-list { position: absolute; left: 0; right: 0; top: calc(100% + 5px); z-index: 4; display: grid; gap: 0; max-height: 210px; overflow-y: auto; padding: 8px; border: 1px solid var(--color-border); border-radius: 10px; background: var(--color-surface); box-shadow: 0 14px 32px rgba(15, 23, 42, .16); }
.value-option { display: flex !important; align-items: center !important; justify-content: flex-start !important; width: 100%; min-height: 34px !important; height: auto !important; gap: 8px !important; padding: 7px 9px !important; border: 0 !important; border-left: 4px solid transparent !important; border-radius: 8px !important; background: transparent !important; color: var(--color-text-secondary) !important; font: inherit; font-size: 13px !important; text-align: left; cursor: pointer; }.value-option > i:first-child { width: 15px; color: currentColor !important; text-align: center; }.value-option > span { flex: 1 1 auto; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; text-align: left; }.value-option:hover { background: var(--color-surface-hover) !important; color: var(--color-accent) !important; }.value-option.selected { border-left-color: var(--color-accent) !important; border-radius: 2px !important; background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface)) !important; color: var(--color-accent) !important; font-weight: 650; }.value-option.selected:hover { background: color-mix(in srgb, var(--color-accent) 18%, var(--color-surface)) !important; color: var(--color-accent) !important; }.check { margin-left: auto; color: var(--color-accent) !important; }.empty-value { padding: 10px; color: var(--color-text-muted); font-size: 12px; }.clear-filter { display: inline-flex !important; align-items: center !important; justify-content: flex-start !important; width: auto !important; height: auto !important; min-height: 0 !important; gap: 7px !important; margin-top: 9px; padding: 0 !important; border: 0 !important; background: transparent !important; color: var(--color-accent) !important; font: inherit; font-size: 12px !important; font-weight: 700; cursor: pointer; }
</style>
