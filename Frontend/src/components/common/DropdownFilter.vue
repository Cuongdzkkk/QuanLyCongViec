<template>
  <div class="dropdown-filter-wrapper" ref="wrapperRef">
    <button
      ref="buttonRef"
      class="filter-chip"
      :class="{ active: isOpen, 'has-value': modelValue !== '' }"
      type="button"
      @click="toggleOpen"
    >
      <slot name="icon"></slot>
      <span>{{ displayLabel }}</span>
      <i class="fa-solid fa-chevron-down"></i>
    </button>

    <Teleport to="body">
      <div v-if="isOpen" ref="popupRef" class="dropdown-popup" :style="popupStyle">
        <div class="search-box-wrapper" v-if="searchable">
          <i class="fa-solid fa-magnifying-glass search-icon"></i>
          <input
            v-model="searchQuery"
            type="text"
            :placeholder="searchPlaceholder"
            class="search-input"
            @click.stop
          />
        </div>

        <div class="options-list">
          <button class="option-item" :class="{ selected: modelValue === '' }" type="button" @click="selectOption('')">
            Tất cả
          </button>
          <button
            v-for="opt in filteredOptions"
            :key="String(opt.value)"
            class="option-item"
            :class="{ selected: modelValue === opt.value }"
            type="button"
            @click="selectOption(opt.value)"
          >
            {{ opt.label }}
          </button>
          <div v-if="filteredOptions.length === 0" class="no-results">
            Không tìm thấy kết quả
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'

const props = defineProps({
  label: { type: String, required: true },
  options: { type: Array, default: () => [] },
  modelValue: { type: [String, Number, Boolean], default: '' },
  searchable: { type: Boolean, default: true },
  searchPlaceholder: { type: String, default: 'Tìm kiếm...' }
})

const emit = defineEmits(['update:modelValue'])

const isOpen = ref(false)
const searchQuery = ref('')
const wrapperRef = ref(null)
const buttonRef = ref(null)
const popupRef = ref(null)
const popupStyle = ref({})

const normalizedOptions = computed(() => props.options.map((opt) => {
  if (typeof opt === 'object' && opt !== null) {
    const value = Object.prototype.hasOwnProperty.call(opt, 'value') ? opt.value : (opt.id ?? opt)
    return { label: String(opt.label || opt.name || opt.title || opt.fullName || value), value }
  }
  return { label: String(opt), value: opt }
}))

const filteredOptions = computed(() => {
  const q = searchQuery.value.trim().toLowerCase()
  if (!q) return normalizedOptions.value
  return normalizedOptions.value.filter((opt) => opt.label.toLowerCase().includes(q))
})

const displayLabel = computed(() => {
  if (props.modelValue === '') return props.label
  const selectedOpt = normalizedOptions.value.find((opt) => opt.value === props.modelValue)
  return selectedOpt ? `${props.label}: ${selectedOpt.label}` : props.label
})

const updatePopupPosition = () => {
  const button = buttonRef.value
  if (!button) return
  const rect = button.getBoundingClientRect()
  popupStyle.value = {
    top: `${rect.bottom + 6}px`,
    left: `${rect.left}px`,
    minWidth: `${Math.max(rect.width, 220)}px`
  }
}

const toggleOpen = async () => {
  isOpen.value = !isOpen.value
  if (!isOpen.value) return
  searchQuery.value = ''
  await nextTick()
  updatePopupPosition()
}

const selectOption = (value) => {
  emit('update:modelValue', value)
  isOpen.value = false
}

const handleClickOutside = (event) => {
  const insideButton = wrapperRef.value?.contains(event.target)
  const insidePopup = popupRef.value?.contains(event.target)
  if (!insideButton && !insidePopup) isOpen.value = false
}

onMounted(() => {
  document.addEventListener('click', handleClickOutside)
  window.addEventListener('resize', updatePopupPosition)
  window.addEventListener('scroll', updatePopupPosition, true)
})

onBeforeUnmount(() => {
  document.removeEventListener('click', handleClickOutside)
  window.removeEventListener('resize', updatePopupPosition)
  window.removeEventListener('scroll', updatePopupPosition, true)
})
</script>

<style scoped>
.dropdown-filter-wrapper {
  display: inline-block;
}

.filter-chip {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  min-height: 34px;
  padding: 7px 12px;
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 8px;
  background: var(--home-panel, #ffffff);
  color: var(--home-text, #172b4d);
  font-size: 13px;
  font-weight: 700;
  cursor: pointer;
  white-space: nowrap;
  transition: background 0.2s, border-color 0.2s, box-shadow 0.2s;
}

.filter-chip:hover,
.filter-chip.active {
  background: var(--home-panel-strong, #fafbfc);
  border-color: rgba(56, 189, 248, 0.78);
  box-shadow: 0 0 0 3px rgba(56, 189, 248, 0.12);
}

.filter-chip.has-value {
  background: rgba(56, 189, 248, 0.14);
  border-color: rgba(56, 189, 248, 0.78);
  color: var(--home-accent, #0052cc);
}

.filter-chip i {
  font-size: 11px;
}

.dropdown-popup {
  position: fixed;
  z-index: 5000;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 10px;
  background: var(--home-panel, #ffffff);
  box-shadow: 0 18px 44px rgba(2, 6, 23, 0.28);
}

.search-box-wrapper {
  position: relative;
  padding: 8px;
  border-bottom: 1px solid var(--home-border, #dfe1e6);
}

.search-icon {
  position: absolute;
  left: 17px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--home-muted, #5e6c84);
  font-size: 13px;
}

.search-input {
  width: 100%;
  box-sizing: border-box;
  padding: 8px 10px 8px 32px;
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 8px;
  outline: none;
  background: var(--home-panel-strong, #fafbfc);
  color: var(--home-text, #172b4d);
  font-size: 14px;
}

.search-input:focus {
  border-color: var(--home-accent, #4c9aff);
  background: var(--home-panel, #ffffff);
}

.options-list {
  max-height: 250px;
  overflow-y: auto;
  padding: 6px !important;
  display: flex;
  flex-direction: column;
  gap: 0 !important;
}

.option-item {
  width: 100%;
  display: flex !important;
  align-items: center !important;
  justify-content: flex-start !important;
  min-height: 32px !important;
  height: auto !important;
  gap: 8px !important;
  padding: 5px 9px !important;
  margin: 0 !important;
  border: 0 !important;
  border-left: 4px solid transparent !important;
  border-radius: 8px !important;
  background: transparent !important;
  color: var(--color-text-secondary, #475569) !important;
  font: inherit;
  font-size: 13px !important;
  text-align: left;
  cursor: pointer;
  transition: background-color 0.15s ease, border-color 0.15s ease, color 0.15s ease;
}

.option-item:hover {
  background: var(--color-surface-hover, #f1f5f9) !important;
  color: var(--color-text-primary, #0f172a) !important;
}

.option-item.selected {
  border-left-color: var(--color-accent, #0ea5e9) !important;
  border-radius: 8px !important;
  background: color-mix(in srgb, var(--color-accent, #0ea5e9) 12%, var(--color-surface, #ffffff)) !important;
  color: var(--color-accent, #0ea5e9) !important;
  font-weight: 650 !important;
}

.no-results {
  padding: 12px;
  color: var(--home-muted, #6b778c);
  text-align: center;
  font-size: 13px;
}
</style>
