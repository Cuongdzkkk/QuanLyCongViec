<template>
  <div class="toolbar-filter-menu" ref="menuRef">
    <button
      type="button"
      class="timeline-filter-trigger icon-only-trigger"
      :title="label"
      :aria-label="label"
      :aria-expanded="isOpen"
      :class="{ active: isOpen || active }"
      @click="toggleOpen"
    >
      <i class="fa-solid fa-filter"></i>
      <span v-if="count > 0" class="filter-count">{{ count }}</span>
    </button>

    <Teleport to="body">
      <div v-if="isOpen" ref="panelRef" class="toolbar-filter-panel" :style="panelStyle">
        <div class="filter-panel-header">
          <strong>{{ label }}</strong>
          <button v-if="count > 0" type="button" @click="$emit('clear')">{{ clearLabel }}</button>
        </div>
        <label class="filter-search-field">
          <i class="fa-solid fa-magnifying-glass filter-search-icon"></i>
          <input v-model="filterSearch" class="filter-search-input" type="text" :placeholder="searchPlaceholder" />
        </label>
        <div class="filter-panel-layout">
          <div class="filter-builder-panel">
            <slot :search="normalizedSearch"></slot>
          </div>
          <div class="active-filter-panel">
            <div class="filters-scroll-area" :class="{ empty: activeItems.length === 0 }">
              <span v-if="activeItems.length === 0" class="empty-filter-copy">{{ emptyLabel }}</span>
              <div v-else v-for="item in activeItems" :key="item.key" class="active-filter-chip">
                <div class="chip-segment label-sec">
                  <i v-if="item.icon" :class="item.icon"></i>
                  <span>{{ item.label }}</span>
                </div>
                <div class="chip-segment condition-sec">{{ item.condition || 'is' }}</div>
                <div class="chip-segment value-sec">
                  <span>{{ item.value }}</span>
                </div>
                <button class="chip-segment remove-sec" type="button" @click="$emit('remove', item.key)" :aria-label="removeLabel">
                  <i class="fa-solid fa-xmark"></i>
                </button>
              </div>
              <button v-if="activeItems.length > 0" class="clear-all-inline" type="button" @click="$emit('clear')">{{ clearAllLabel }}</button>
            </div>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'

const props = defineProps({
  label: { type: String, default: 'Filters' },
  clearLabel: { type: String, default: 'Clear' },
  clearAllLabel: { type: String, default: 'Clear all' },
  emptyLabel: { type: String, default: 'No filters applied' },
  removeLabel: { type: String, default: 'Remove filter' },
  searchPlaceholder: { type: String, default: 'Search filters...' },
  count: { type: Number, default: 0 },
  activeItems: { type: Array, default: () => [] }
})

defineEmits(['clear', 'remove'])

const isOpen = ref(false)
const filterSearch = ref('')
const menuRef = ref(null)
const panelRef = ref(null)
const panelStyle = ref({})
const active = computed(() => props.count > 0)
const normalizedSearch = computed(() => filterSearch.value.trim().toLowerCase())
const toggleOpen = () => {
  isOpen.value = !isOpen.value
  if (isOpen.value) {
    filterSearch.value = ''
    window.dispatchEvent(new CustomEvent('toolbar-popup-open', { detail: menuRef.value }))
  }
}

const updatePanelPosition = () => {
  const button = menuRef.value?.querySelector('button')
  if (!button) return
  const rect = button.getBoundingClientRect()
  panelStyle.value = {
    top: `${rect.bottom + 8}px`,
    left: `${Math.max(12, Math.min(rect.left, window.innerWidth - 732))}px`
  }
}

const handleClickOutside = (event) => {
  if (event.type === 'toolbar-popup-open') {
    if (event.detail !== menuRef.value) isOpen.value = false
    return
  }
  if (menuRef.value?.contains(event.target) || panelRef.value?.contains(event.target)) return
  isOpen.value = false
}

watch(isOpen, async (open) => {
  if (!open) return
  await nextTick()
  updatePanelPosition()
})

onMounted(() => {
  document.addEventListener('click', handleClickOutside)
  window.addEventListener('toolbar-popup-open', handleClickOutside)
  window.addEventListener('resize', updatePanelPosition)
  window.addEventListener('scroll', updatePanelPosition, true)
})

onBeforeUnmount(() => {
  document.removeEventListener('click', handleClickOutside)
  window.removeEventListener('toolbar-popup-open', handleClickOutside)
  window.removeEventListener('resize', updatePanelPosition)
  window.removeEventListener('scroll', updatePanelPosition, true)
})
</script>

<style scoped>
.toolbar-filter-menu {
  display: inline-flex;
}

.filter-count {
  display: inline-grid;
  min-width: 18px;
  height: 18px;
  padding: 0 5px;
  place-items: center;
  border-radius: 999px;
  background: color-mix(in srgb, var(--color-accent, #0ea5e9) 16%, #ffffff);
  color: var(--color-accent, #0284c7);
  font-size: 11px;
  font-weight: 800;
}

.toolbar-filter-panel {
  position: fixed;
  z-index: 5000;
  width: min(720px, calc(100vw - 24px));
  padding: 10px;
  border: 1px solid var(--color-border, #dfe1e6);
  border-radius: 12px;
  background: var(--color-surface, #ffffff);
  box-shadow: 0 18px 44px rgba(2, 6, 23, 0.20);
}

.filter-panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 4px 4px 10px;
  color: var(--color-text-primary, #172b4d);
  font-size: 13px;
}

.filter-panel-header button {
  border: 0;
  background: transparent;
  color: var(--color-accent, #0ea5e9);
  font-size: 12px;
  font-weight: 800;
  cursor: pointer;
}

.filter-search-field {
  position: relative;
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  min-height: 34px;
  height: 34px;
  box-sizing: border-box;
  border: 1px solid var(--color-border, #dfe1e6);
  border-radius: 9px;
  background: var(--color-surface, #ffffff);
  padding: 0 12px;
  color: var(--color-text-muted, #64748b);
  transition: border-color 0.2s, box-shadow 0.2s;
}

.filter-search-icon {
  width: 16px;
  flex: 0 0 16px;
  font-size: 14px;
}

.filter-search-input {
  width: 100%;
  height: 100%;
  min-width: 0;
  border: 0;
  background: transparent;
  color: var(--color-text-primary, #172b4d);
  padding: 0;
  outline: none;
  font-size: 13.5px;
}

.filter-search-field:focus-within {
  border-color: var(--color-accent, #0ea5e9);
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.14);
}

.filter-panel-layout {
  display: grid;
  grid-template-columns: 260px minmax(300px, 1fr);
  gap: 12px;
  align-items: stretch;
  margin-top: 12px;
}

.filter-builder-panel {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 0;
  max-width: 260px;
}

.active-filter-panel {
  min-height: 184px;
  min-width: 0;
  border: 1px solid var(--color-border, #dfe1e6);
  border-radius: 10px;
  padding: 18px;
  background: color-mix(in srgb, var(--color-surface, #ffffff) 88%, transparent);
}

.filters-scroll-area {
  display: flex;
  flex-wrap: wrap;
  align-content: flex-start;
  align-items: flex-start;
  gap: 8px;
  width: 100%;
  min-height: 154px;
  max-height: 186px;
  overflow-y: auto;
}

.filters-scroll-area.empty {
  justify-content: flex-start;
  text-align: left;
}

.empty-filter-copy {
  color: var(--color-text-muted, #64748b);
  font-size: 13px;
}

.active-filter-chip {
  display: flex;
  align-items: stretch;
  max-width: 100%;
  height: 28px;
  overflow: hidden;
  border: 1px solid var(--color-border, #dfe1e6);
  border-radius: 8px;
  background: var(--color-surface-hover, #f8fafc);
}

.chip-segment {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 0 10px;
  border-right: 1px solid var(--color-border, #dfe1e6);
  font-size: 12px;
  white-space: nowrap;
}

.label-sec {
  color: var(--color-text-secondary, #475569);
}

.condition-sec {
  color: var(--color-text-muted, #64748b);
  background: var(--color-surface-hover, #f8fafc);
}

.value-sec {
  min-width: 0;
  max-width: 170px;
  color: var(--color-text-primary, #172b4d);
  font-weight: 700;
}

.value-sec span {
  overflow: hidden;
  text-overflow: ellipsis;
}

.remove-sec {
  border: 0;
  background: transparent;
  color: var(--color-text-muted, #64748b);
  cursor: pointer;
}

.remove-sec:hover {
  background: rgba(255, 60, 60, 0.1);
  color: #ef4444;
}

.clear-all-inline {
  height: 26px;
  padding: 0 9px;
  border: 1px solid var(--color-border, #dfe1e6);
  border-radius: 7px;
  background: var(--color-surface, #ffffff);
  color: var(--color-text-secondary, #475569);
  font-size: 11px;
  font-weight: 700;
  cursor: pointer;
}

.clear-all-inline:hover {
  background: var(--color-surface-hover, #f8fafc);
  color: var(--color-text-primary, #172b4d);
}

:deep(.dropdown-filter-wrapper),
:deep(.filter-chip) {
  width: 100%;
}

:deep(.filter-chip) {
  justify-content: space-between;
}

@media (max-width: 640px) {
  .filter-panel-layout {
    grid-template-columns: 1fr;
  }

  .filter-builder-panel {
    max-width: none;
  }
}
</style>
