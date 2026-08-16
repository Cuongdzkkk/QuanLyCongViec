<script setup>
import { computed, nextTick, ref, watch } from 'vue'

const props = defineProps({
  filters: {
    type: Array,
    default: () => []
  },
  variant: {
    type: String,
    default: 'builder'
  },
  statusOptions: {
    type: Array,
    default: () => []
  },
  active: {
    type: Boolean,
    default: true
  }
})

const emit = defineEmits(['remove', 'clear', 'add', 'add-filter', 'apply', 'update:filters'])

const draft = ref({ field: 'status', operator: 'is', value: '' })
const fieldSearch = ref('')
const openSelect = ref(null)
const selectPlacement = ref({})
const selectRefs = ref({})

const filterFields = [
  { key: 'status', label: 'Status', icon: 'fa-regular fa-circle-dot', values: ['BACKLOG', 'TO DO', 'IN PROGRESS', 'IN REVIEW', 'DONE'] },
  { key: 'assignee', label: 'Assignee', icon: 'fa-regular fa-user', values: ['Unassigned'] },
  { key: 'creator', label: 'Creator', icon: 'fa-regular fa-user', values: ['Me'] },
  { key: 'priority', label: 'Priority', icon: 'fa-solid fa-signal', values: ['Urgent', 'High', 'Medium', 'Low', 'None'] },
  { key: 'label', label: 'Label', icon: 'fa-solid fa-tag', values: ['No label'] },
  { key: 'startDate', label: 'Start date', icon: 'fa-regular fa-calendar-plus', values: ['Today', 'This week', 'Empty'] },
  { key: 'dueDate', label: 'Due date', icon: 'fa-regular fa-calendar', values: ['Today', 'This week', 'Overdue', 'Empty'] },
  { key: 'cycle', label: 'Cycle', icon: 'fa-solid fa-arrows-spin', values: ['No cycle'] },
  { key: 'module', label: 'Module', icon: 'fa-solid fa-table-cells-large', values: ['No module'] },
  { key: 'createdAt', label: 'Created at', icon: 'fa-regular fa-calendar', values: ['Today', 'This week'] },
  { key: 'updatedAt', label: 'Updated at', icon: 'fa-regular fa-calendar', values: ['Today', 'This week'] }
]

const operatorsByField = {
  status: ['is', 'is not', 'in', 'not in'],
  assignee: ['is', 'is not', 'empty', 'not empty'],
  creator: ['is', 'is not'],
  priority: ['is', 'is not', 'in'],
  label: ['includes', 'not includes', 'empty'],
  startDate: ['before', 'after', 'between', 'empty'],
  dueDate: ['before', 'after', 'between', 'empty', 'overdue'],
  cycle: ['is', 'is not', 'empty'],
  module: ['is', 'is not', 'empty'],
  createdAt: ['before', 'after', 'between'],
  updatedAt: ['before', 'after', 'between']
}

const selectedField = computed(() => filterFields.find(field => field.key === draft.value.field) || filterFields[0])
const visibleFilterFields = computed(() => {
  const query = fieldSearch.value.trim().toLowerCase()
  if (!query) return filterFields
  return filterFields.filter(field =>
    field.label.toLowerCase().includes(query) ||
    field.key.toLowerCase().includes(query)
  )
})
const availableOperators = computed(() => operatorsByField[draft.value.field] || ['is'])
const valueRequired = computed(() => !['empty', 'not empty', 'overdue'].includes(draft.value.operator))
const isChipsOnly = computed(() => props.variant === 'chips')

const statusOptionMeta = {
  BACKLOG: { icon: 'fa-regular fa-circle-dashed', color: '#94A3B8' },
  'TO DO': { icon: 'fa-regular fa-circle', color: '#A78BFA' },
  'IN PROGRESS': { icon: 'fa-solid fa-circle-half-stroke', color: '#38BDF8' },
  'IN REVIEW': { icon: 'fa-solid fa-eye', color: '#F59E0B' },
  DONE: { icon: 'fa-solid fa-circle-check', color: '#22C55E' },
  CANCELLED: { icon: 'fa-regular fa-circle-xmark', color: '#F43F5E' }
}

const normalizeStatus = (value) => `${value || ''}`.toUpperCase().replace(/\s+/g, ' ').trim()

const resolveStatusIcon = (value) => {
  const status = normalizeStatus(value)
  if (status.includes('CANCEL')) return 'fa-regular fa-circle-xmark'
  if (status.includes('DONE') || status.includes('COMPLETE')) return 'fa-solid fa-circle-check'
  if (status.includes('PROGRESS') || status.includes('ACTIVE')) return 'fa-solid fa-circle-half-stroke'
  if (status.includes('REVIEW') || status.includes('TEST')) return 'fa-solid fa-eye'
  if (status.includes('TODO') || status.includes('TO DO')) return 'fa-regular fa-circle'
  return 'fa-regular fa-circle-dashed'
}

const projectStatusValues = computed(() => {
  const source = props.statusOptions?.length ? props.statusOptions : []
  const normalized = source
    .map(status => ({
      value: normalizeStatus(status.name || status.value || status.label),
      label: status.label || status.displayName || status.name || status.value,
      icon: status.icon || resolveStatusIcon(status.name || status.value || status.label),
      color: status.color || status.colorCode || statusOptionMeta[normalizeStatus(status.name || status.value || status.label)]?.color || 'var(--color-text-muted)'
    }))
    .filter(status => status.value)

  const seen = new Set()
  return normalized.filter(status => {
    if (seen.has(status.value)) return false
    seen.add(status.value)
    return true
  })
})

const draftOptions = computed(() => {
  if (draft.value.field === 'status' && projectStatusValues.value.length) {
    return projectStatusValues.value.map(status => status.value)
  }
  return selectedField.value.values || []
})

const priorityOptionMeta = {
  Urgent: { icon: 'fa-solid fa-angles-up', color: '#ef4444' },
  High: { icon: 'fa-solid fa-chevron-up', color: '#f97316' },
  Medium: { icon: 'fa-solid fa-minus', color: '#2563eb' },
  Normal: { icon: 'fa-solid fa-minus', color: '#2563eb' },
  Low: { icon: 'fa-solid fa-chevron-down', color: '#10b981' },
  None: { icon: 'fa-solid fa-ban', color: '#94a3b8' }
}

const getValueMeta = (fieldKey, value) => {
  if (fieldKey === 'status') {
    const projectStatus = projectStatusValues.value.find(status => status.value === normalizeStatus(value))
    return projectStatus || statusOptionMeta[normalizeStatus(value)] || { icon: 'fa-regular fa-circle-dot', color: 'var(--color-text-muted)' }
  }
  if (fieldKey === 'priority') return priorityOptionMeta[value] || { icon: 'fa-solid fa-signal', color: 'var(--color-text-muted)' }
  return null
}

const isSameFilter = (filter, candidate) =>
  filter.field === candidate.field &&
  filter.operator === candidate.operator &&
  `${filter.value || ''}` === `${candidate.value || ''}`

const removeFilter = (id) => {
  const next = props.filters.filter(filter => filter.id !== id)
  emit('update:filters', next)
  emit('remove', id)
  emit('apply', next)
}

const clearAll = () => {
  emit('update:filters', [])
  emit('clear')
  emit('apply', [])
}

const setSelectRef = (name, element) => {
  if (element) selectRefs.value[name] = element
}

const updateSelectPlacement = async (name) => {
  await nextTick()
  const element = selectRefs.value[name]
  if (!element) return

  const rect = element.getBoundingClientRect()
  const desiredMenuHeight = name === 'value' ? 188 : 272
  const roomBelow = window.innerHeight - rect.bottom
  const roomAbove = rect.top
  selectPlacement.value = {
    ...selectPlacement.value,
    [name]: roomBelow < desiredMenuHeight && roomAbove > roomBelow
  }
}

const toggleSelect = (name) => {
  const next = openSelect.value === name ? null : name
  openSelect.value = next
  if (next) updateSelectPlacement(next)
}

const closeSelect = () => {
  openSelect.value = null
}

watch(() => props.active, (isActive) => {
  if (!isActive) closeSelect()
})

const selectField = (fieldKey) => {
  draft.value.field = fieldKey
  draft.value.operator = (operatorsByField[fieldKey] || ['is'])[0]
  draft.value.value = ''
  closeSelect()
}

const selectOperator = (operator) => {
  draft.value.operator = operator
  draft.value.value = ''
  closeSelect()
}

const selectValue = (value) => {
  draft.value.value = value
  closeSelect()
}

const applyFilter = () => {
  if (valueRequired.value && !draft.value.value) return

  const candidate = {
    field: draft.value.field,
    operator: draft.value.operator,
    value: valueRequired.value ? draft.value.value : ''
  }

  if (props.filters.some(filter => isSameFilter(filter, candidate))) {
    closeSelect()
    return
  }

  const valueMeta = valueRequired.value ? getValueMeta(draft.value.field, draft.value.value) : null

  const filter = {
    id: `${draft.value.field}-${Date.now()}`,
    ...candidate,
    label: selectedField.value.label,
    condition: draft.value.operator,
    displayValue: valueRequired.value ? draft.value.value : draft.value.operator,
    icon: selectedField.value.icon,
    valueIcon: valueMeta?.icon,
    valueColor: valueMeta?.color
  }

  const next = [...props.filters, filter]
  emit('update:filters', next)
  emit('add', filter)
  emit('add-filter', filter)
  emit('apply', next)
  draft.value = { field: 'status', operator: 'is', value: '' }
  fieldSearch.value = ''
  closeSelect()
}
</script>

<template>
  <div class="filter-bar-container">
    <label v-if="!isChipsOnly" class="filter-search-field">
      <i class="fa-solid fa-magnifying-glass filter-search-icon"></i>
      <input v-model="fieldSearch" class="filter-search-input" type="text" placeholder="Search filters..." />
    </label>

    <div class="filter-layout" :class="{ 'chips-only': isChipsOnly }">
      <div v-if="!isChipsOnly" class="filter-builder-panel">
        <div class="filter-builder" @click.stop>
          <div class="filter-combobox" :class="{ active: openSelect === 'field', 'drop-up': selectPlacement.field }" :ref="el => setSelectRef('field', el)">
            <span class="filter-label">FIELD</span>
            <button
              class="filter-select-trigger"
              type="button"
              :class="{ active: openSelect === 'field' }"
              @click="toggleSelect('field')"
            >
              <span>{{ selectedField.label }}</span>
              <i class="fa-solid fa-chevron-down"></i>
            </button>
            <div v-show="openSelect === 'field'" class="filter-select-menu">
              <button
                v-for="field in visibleFilterFields"
                :key="field.key"
                class="filter-select-option"
                :class="{ selected: draft.field === field.key }"
                type="button"
                @click="selectField(field.key)"
              >
                <i :class="field.icon"></i>
                <span>{{ field.label }}</span>
                <i v-if="draft.field === field.key" class="fa-solid fa-check selected-check"></i>
              </button>
            </div>
          </div>

          <div class="filter-combobox" :class="{ active: openSelect === 'operator', 'drop-up': selectPlacement.operator }" :ref="el => setSelectRef('operator', el)">
            <span class="filter-label">OPERATOR</span>
            <button
              class="filter-select-trigger"
              type="button"
              :class="{ active: openSelect === 'operator' }"
              @click="toggleSelect('operator')"
            >
              <span>{{ draft.operator }}</span>
              <i class="fa-solid fa-chevron-down"></i>
            </button>
            <div v-show="openSelect === 'operator'" class="filter-select-menu">
              <button
                v-for="operator in availableOperators"
                :key="operator"
                class="filter-select-option"
                :class="{ selected: draft.operator === operator }"
                type="button"
                @click="selectOperator(operator)"
              >
                <span>{{ operator }}</span>
                <i v-if="draft.operator === operator" class="fa-solid fa-check selected-check"></i>
              </button>
            </div>
          </div>

          <div v-if="valueRequired" class="filter-combobox" :class="{ active: openSelect === 'value', 'drop-up': selectPlacement.value }" :ref="el => setSelectRef('value', el)">
            <span class="filter-label">VALUE</span>
            <button
              class="filter-select-trigger"
              type="button"
              :class="{ active: openSelect === 'value', placeholder: !draft.value }"
              @click="toggleSelect('value')"
            >
              <span>{{ draft.value || 'Select value' }}</span>
              <i class="fa-solid fa-chevron-down"></i>
            </button>
            <div v-show="openSelect === 'value'" class="filter-select-menu value-select-menu">
              <button
                v-for="value in draftOptions"
                :key="value"
                class="filter-select-option"
                :class="{ selected: draft.value === value, 'has-value-color': getValueMeta(draft.field, value) }"
                :style="getValueMeta(draft.field, value) ? { '--option-color': getValueMeta(draft.field, value).color } : null"
                type="button"
                @click="selectValue(value)"
              >
                <i v-if="getValueMeta(draft.field, value)" :class="getValueMeta(draft.field, value).icon" class="value-option-icon"></i>
                <span>{{ value }}</span>
                <i v-if="draft.value === value" class="fa-solid fa-check selected-check"></i>
              </button>
            </div>
          </div>

          <button class="apply-filter-btn" type="button" :disabled="valueRequired && !draft.value" @click="applyFilter">
            Apply filter
          </button>
        </div>
      </div>

      <div class="active-filter-panel">
        <div class="filters-scroll-area" :class="{ empty: filters.length === 0 }">
          <span v-if="filters.length === 0" class="empty-filter-copy">No filters applied</span>
          <div v-else v-for="filter in filters" :key="filter.id" class="filter-chip">
            <div class="chip-segment label-sec">
              <i v-if="filter.icon" :class="filter.icon" class="mr-2"></i>
              <span>{{ filter.label }}</span>
            </div>
            <div class="chip-segment condition-sec">{{ filter.condition }}</div>
            <div class="chip-segment value-sec" :class="{ 'has-value-color': filter.valueColor }" :style="filter.valueColor ? { '--chip-value-color': filter.valueColor } : null">
              <i v-if="filter.valueIcon" :class="filter.valueIcon"></i>
              <span>{{ filter.displayValue || filter.value || '--' }}</span>
            </div>
            <button class="chip-segment remove-sec" type="button" @click="removeFilter(filter.id)" aria-label="Remove filter">
              <i class="fa-solid fa-xmark"></i>
            </button>
          </div>
          <button v-if="filters.length > 0 && !isChipsOnly" class="clear-all-inline" type="button" @click="clearAll">Clear all</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.filter-bar-container {
  display: flex;
  flex-direction: column;
  align-items: stretch;
  gap: 12px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 8px;
  width: 100%;
  min-height: auto;
  position: relative;
  z-index: 1000;
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
  border: 1px solid var(--color-border);
  border-radius: 9px;
  background: var(--color-surface);
  padding: 0 12px;
  color: var(--color-text-muted);
  transition: border-color 0.2s, box-shadow 0.2s;
}

.filter-search-icon {
  position: static;
  transform: none;
  width: 16px;
  height: 16px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: 0 0 16px;
  font-size: 14px;
  pointer-events: none;
}

.filter-search-input {
  width: 100% !important;
  height: 100% !important;
  box-sizing: border-box !important;
  min-width: 0 !important;
  border: 0 !important;
  border-radius: 0 !important;
  background: transparent !important;
  color: var(--color-text-primary) !important;
  padding: 0 !important;
  outline: none !important;
  font-size: 13.5px !important;
  line-height: 34px !important;
  text-indent: 0 !important;
  -webkit-appearance: none;
  appearance: none;
}

.filter-search-input::placeholder {
  color: var(--color-text-muted);
}

.filter-search-field:focus-within {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.14);
}

.filter-layout {
  display: grid;
  grid-template-columns: 260px minmax(300px, 1fr);
  gap: 12px;
  align-items: stretch;
}

.filter-layout.chips-only {
  display: block;
}

.filter-builder-panel,
.active-filter-panel {
  min-width: 0;
  padding: 0;
}

.filter-builder-panel {
  max-width: 260px;
}

.active-filter-panel {
  min-height: 184px;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 18px;
  background: color-mix(in srgb, var(--color-surface) 88%, transparent);
}

.clear-all-inline {
  height: 26px;
  padding: 0 9px;
  border: 1px solid var(--color-border);
  border-radius: 7px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font-size: 11px;
  font-weight: 650;
  cursor: pointer;
}

.clear-all-inline:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
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
  min-height: 154px;
  justify-content: flex-start;
  align-items: flex-start;
  align-content: flex-start;
  text-align: left;
  padding: 0;
}

.empty-filter-copy {
  color: var(--color-text-muted);
  font-size: 13px;
}

.filter-chip {
  display: flex;
  align-items: stretch;
  max-width: 100%;
  background: var(--color-surface-hover);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  overflow: hidden;
  height: 28px;
}

.chip-segment {
  display: flex;
  align-items: center;
  padding: 0 10px;
  font-size: 12px;
  border-right: 1px solid var(--color-border);
  white-space: nowrap;
}

.label-sec,
.value-sec {
  min-width: 0;
}

.label-sec span,
.value-sec {
  overflow: hidden;
  text-overflow: ellipsis;
}

.label-sec {
  color: var(--color-text-secondary);
}

.condition-sec {
  color: var(--color-text-muted);
  background: var(--color-surface-hover);
}

.value-sec {
  color: var(--color-text-primary);
  font-weight: 600;
  gap: 6px;
}

.value-sec.has-value-color {
  color: var(--chip-value-color);
}

.value-sec i {
  font-size: 12px;
  color: currentColor;
}

.remove-sec {
  border: 0;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
  padding: 0 8px;
}

.remove-sec:hover {
  background: rgba(255, 60, 60, 0.1);
  color: #ef4444;
}

.apply-filter-btn {
  border-radius: 8px;
}

.filter-builder {
  background: transparent;
  border: none;
  border-radius: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
  box-shadow: none !important;
}

.filter-label {
  display: flex;
  color: var(--color-text-secondary);
  font-size: 11px;
  font-weight: 750;
  letter-spacing: 0.02em;
}

.filter-combobox {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 5px;
  z-index: 1;
}

.filter-combobox.active {
  z-index: calc(var(--z-popover) + 10);
}

.filter-select-trigger {
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 10px;
  width: 100%;
  height: 34px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 9px;
  color: var(--color-text-primary);
  padding: 0 12px;
  outline: none;
  font-size: 13.5px;
  line-height: 1;
  cursor: pointer;
  transition: border-color 0.18s ease, background-color 0.18s ease, box-shadow 0.18s ease;
}

.filter-select-trigger span {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  text-align: left;
}

.filter-select-trigger i {
  flex: 0 0 auto;
  color: var(--color-text-muted);
  font-size: 11px;
  transition: transform 0.18s ease, color 0.18s ease;
}

.filter-select-trigger:hover,
.filter-select-trigger.active {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border));
  background: color-mix(in srgb, var(--color-accent) 6%, var(--color-surface));
}

.filter-select-trigger.active {
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.12);
}

.filter-select-trigger.active i {
  color: var(--color-accent);
  transform: rotate(180deg);
}

.filter-select-trigger.placeholder {
  color: var(--color-text-muted);
}

.filter-select-menu {
  position: absolute;
  left: 0;
  right: 0;
  top: calc(100% + 5px);
  z-index: calc(var(--z-popover) + 20);
  max-height: min(260px, calc(100vh - 260px));
  overflow-y: auto;
  overscroll-behavior: contain;
  padding: 8px;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-surface-elevated);
  box-shadow: var(--shadow-popover);
}

.filter-combobox.drop-up .filter-select-menu {
  top: auto;
  bottom: calc(100% + 5px);
}

.value-select-menu {
  max-height: 176px !important;
  overflow-y: scroll !important;
  scrollbar-gutter: stable;
}

.value-select-menu::-webkit-scrollbar {
  width: 8px;
}

.value-select-menu::-webkit-scrollbar-thumb {
  border: 2px solid var(--color-surface-elevated);
  border-radius: 999px;
  background: color-mix(in srgb, var(--color-text-muted) 36%, transparent);
}

.value-select-menu::-webkit-scrollbar-track {
  background: transparent;
}

.filter-select-option {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 8px;
  width: 100%;
  min-height: 34px;
  padding: 7px 9px;
  border: 0;
  border-left: 4px solid transparent;
  border-radius: 8px;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 13px;
  font-weight: 500;
  text-align: left;
  cursor: pointer;
  transition: background-color 0.15s ease, border-color 0.15s ease, color 0.15s ease;
}

.filter-select-option:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.filter-select-option.selected {
  background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface));
  border-left-color: var(--color-accent);
  border-radius: 2px;
  color: var(--color-accent);
  font-weight: 650;
}

.filter-select-option.selected:hover {
  background: color-mix(in srgb, var(--color-accent) 18%, var(--color-surface));
  color: var(--color-accent);
}

.filter-select-option > span {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  text-align: left;
}

.filter-select-option > i:first-child {
  width: 15px;
  color: currentColor;
  font-size: 12px;
  text-align: center;
}

.filter-select-option.has-value-color {
  color: var(--option-color);
}

.filter-select-option.has-value-color:hover {
  color: var(--option-color);
  background: color-mix(in srgb, var(--option-color) 10%, var(--color-surface));
}

.filter-select-option.has-value-color.selected {
  color: var(--option-color);
  background: color-mix(in srgb, var(--option-color) 14%, var(--color-surface));
  border-left-color: var(--option-color);
}

.filter-select-option.has-value-color.selected:hover {
  background: color-mix(in srgb, var(--option-color) 20%, var(--color-surface));
}

.value-option-icon {
  color: currentColor;
}

.selected-check {
  margin-left: auto;
  font-size: 11px;
  color: var(--color-accent);
}

.apply-filter-btn {
  background: var(--color-accent);
  border: none;
  color: #ffffff;
  font-size: 13px;
  font-weight: 600;
  height: 32px;
  padding: 0 10px;
  cursor: pointer;
}

.apply-filter-btn:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

@media (max-width: 720px) {
  .filter-layout {
    grid-template-columns: 1fr;
  }

  .filters-scroll-area,
  .filters-scroll-area.empty {
    min-height: 64px;
    max-height: 160px;
  }
}
</style>




