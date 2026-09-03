<template>
  <div class="work-items-table-shell">
    <table class="work-items-style-table" :style="{ minWidth: `${minWidth}px` }">
      <thead>
        <tr>
          <th
            v-for="column in columns"
            :key="column.key"
            :style="columnStyle(column)"
          >
            <i v-if="column.icon" :class="column.icon" aria-hidden="true"></i>
            {{ column.label }}
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="(row, index) in rows"
          :key="rowKey(row, index)"
          tabindex="0"
          @click="emit('row-click', row)"
          @keydown.enter="emit('row-click', row)"
          @keydown.space.prevent="emit('row-click', row)"
        >
          <td
            v-for="column in columns"
            :key="column.key"
            :style="columnStyle(column)"
          >
            <slot
              :name="`cell-${column.key}`"
              :row="row"
              :value="row[column.key]"
              :column="column"
            >
              {{ row[column.key] }}
            </slot>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup>
defineProps({
  columns: { type: Array, required: true },
  rows: { type: Array, required: true },
  minWidth: { type: [String, Number], default: 900 }
})

const emit = defineEmits(['row-click'])

const rowKey = (row, index) => row?.id || row?.userId || index

const columnStyle = (column) => ({
  width: column.width,
  minWidth: column.minWidth,
  position: column.sticky ? 'sticky' : undefined,
  left: column.sticky ? '0' : undefined,
  zIndex: column.sticky ? '1' : undefined
})
</script>
