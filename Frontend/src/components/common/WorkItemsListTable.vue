<script setup>
const props = defineProps({
  columns: { type: Array, default: () => [] },
  rows: { type: Array, default: () => [] },
  rowKey: { type: [String, Function], default: 'id' },
  minWidth: { type: [String, Number], default: 1100 },
  rowClass: { type: [String, Function], default: '' }
})

const emit = defineEmits(['row-click'])

const getRowKey = (row, index) => {
  if (typeof props.rowKey === 'function') return props.rowKey(row, index)
  return row?.[props.rowKey] ?? index
}

const getRowClass = (row, index) => {
  if (typeof props.rowClass === 'function') return props.rowClass(row, index)
  return props.rowClass
}
</script>

<template>
  <div class="work-items-table-shell">
    <table class="plane-table work-items-style-table work-items-list-table" :style="{ minWidth: `${minWidth}px` }">
      <thead>
        <tr>
          <th
            v-for="(column, index) in columns"
            :key="column.key"
            :class="{ 'sticky-work-item': column.sticky || index === 0 }"
            :style="{ width: column.width, minWidth: column.minWidth || column.width }"
          >
            <i v-if="column.icon" :class="column.icon" aria-hidden="true"></i>
            {{ column.label }}
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="(row, index) in rows"
          :key="getRowKey(row, index)"
          :class="getRowClass(row, index)"
          @click="emit('row-click', row, index)"
        >
          <td
            v-for="(column, columnIndex) in columns"
            :key="column.key"
            :class="{ 'sticky-work-item': column.sticky || columnIndex === 0 }"
          >
            <slot :name="`cell-${column.key}`" :row="row" :index="index" :column="column">
              {{ row?.[column.key] ?? '-' }}
            </slot>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.work-items-list-table {
  min-width: 1100px;
  border-top: 1px solid var(--color-border);
  border-collapse: separate;
  border-spacing: 0;
  font-size: 13.5px;
}

.work-items-list-table .sticky-work-item {
  position: static;
  left: auto;
  z-index: auto;
  background: var(--color-surface);
  box-shadow: none;
}

.work-items-list-table th.sticky-work-item {
  z-index: auto;
}

.work-items-list-table th,
.work-items-list-table td {
  border-bottom: 1px solid var(--color-border);
  border-right: 1px solid var(--color-border);
  background: var(--color-surface);
}

.work-items-list-table th {
  padding: 14px 16px !important;
  color: color-mix(in srgb, var(--color-text-primary) 78%, var(--color-text-muted));
  font-size: 12px !important;
  font-weight: 850 !important;
  letter-spacing: .015em;
  white-space: nowrap;
  background: linear-gradient(180deg, color-mix(in srgb, var(--color-table-header) 84%, var(--color-surface) 16%), var(--color-table-header));
}

.work-items-list-table th i {
  color: var(--sa-primary, var(--color-accent));
  margin-right: 6px;
  opacity: .88;
}

.work-items-list-table td {
  height: 50px;
  padding: 10px 14px !important;
  white-space: nowrap;
  color: var(--color-text-primary);
}

.work-items-list-table th:first-child,
.work-items-list-table td:first-child {
  border-left: 1px solid var(--color-border);
}

.work-items-list-table tbody tr:hover td {
  background: color-mix(in srgb, var(--color-table-row-hover) 82%, var(--sa-primary, var(--color-accent)) 6%) !important;
}
</style>
