<template>
  <div class="project-page-toolbar">
    <div class="ppt-left">
      <!-- Standardized Search -->
      <div v-if="showSearch" class="ppt-search">
        <i class="fa-solid fa-magnifying-glass search-icon"></i>
        <input 
          :value="searchQuery" 
          @input="$emit('update:searchQuery', $event.target.value)" 
          type="text" 
          class="nexus-search-input" 
          :placeholder="searchPlaceholder" 
        />
      </div>
      <slot name="search"></slot>
      <div class="ppt-group ppt-filters" v-if="$slots.filters"><slot name="filters"></slot></div>
      <div class="ppt-group ppt-sort" v-if="$slots.sort">
        <slot name="sort"></slot>
      </div>
      <div class="ppt-group ppt-toggles" v-if="$slots.toggles">
        <slot name="toggles"></slot>
      </div>
      <slot name="left"></slot>
    </div>
    <div class="ppt-right">
      <!-- Actions stay on the far right; search/filter/sort/view stay together. -->

      <div class="ppt-group ppt-actions" v-if="$slots.actions">
        <slot name="actions"></slot>
      </div>
      
      <slot name="right"></slot>
    </div>
  </div>
</template>

<script setup>
defineProps({
  showSearch: { type: Boolean, default: false },
  searchQuery: { type: String, default: '' },
  searchPlaceholder: { type: String, default: 'Search...' }
})
defineEmits(['update:searchQuery'])
</script>

<style scoped>
.project-page-toolbar {
  position: relative;
  z-index: 5;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
  min-height: 42px;
  width: 100%;
  padding: 8px !important;
  border: 1px solid color-mix(in srgb, var(--color-border) 72%, transparent);
  border-radius: 12px;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 86%, transparent), color-mix(in srgb, var(--color-surface-hover) 46%, transparent));
  box-shadow: 0 10px 24px color-mix(in srgb, #020617 6%, transparent);
  flex: 0 0 auto;
  margin-bottom: 20px;
}

.ppt-left, .ppt-right {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.ppt-left { flex: 1 1 auto; }
.ppt-right { flex: 0 1 auto; margin-left: auto; }

.ppt-group {
  display: flex;
  align-items: center;
  gap: 8px;
}

:deep(.ppt-filters .toolbar-filter-control) { order: 1; }
:deep(.ppt-filters .toolbar-sort-control) { order: 2; }

/* Standardized Search Input */
.ppt-search {
  position: relative;
  display: flex;
  align-items: center;
  width: min(325px, 32vw);
}

.ppt-search .search-icon {
  position: absolute;
  left: 12px;
  color: var(--color-text-muted);
  font-size: 14px;
}

.ppt-search .nexus-search-input {
  width: 100%;
  height: 34px !important;
  padding-left: 36px !important;
  padding-right: 12px !important;
  border-radius: 9px !important;
  border: 1px solid var(--color-border) !important;
  background-color: var(--color-surface) !important;
  color: var(--color-text-primary) !important;
  font-size: 13.5px !important;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.ppt-search .nexus-search-input:focus {
  border-color: var(--color-accent) !important;
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15) !important;
  outline: none;
}

/* Standardized Filter Trigger Button across all tabs */
:deep(.timeline-filter-trigger) {
  display: inline-flex !important;
  align-items: center !important;
  gap: 7px !important;
  height: 34px !important;
  padding: 0 12px !important;
  border: 1px solid var(--color-border) !important;
  border-radius: 9px !important;
  background: var(--color-surface) !important;
  color: var(--color-text-secondary) !important;
  font-size: 13px !important;
  font-weight: 600 !important;
  cursor: pointer !important;
  transition: all 0.2s ease !important;
}

:deep(.icon-only-trigger) {
  width: 42px !important;
  min-width: 42px !important;
  justify-content: center !important;
  padding-left: 0 !important;
  padding-right: 0 !important;
  position: relative;
}

:deep(.ppt-group button.icon-only-trigger) {
  width: 42px !important;
  min-width: 42px !important;
  justify-content: center !important;
  padding: 0 !important;
}

:deep(.icon-only-trigger .filter-count) {
  position: absolute;
  top: -6px;
  right: -6px;
}

:deep(.timeline-filter-trigger:hover),
:deep(.timeline-filter-trigger.active) {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important;
  color: var(--color-accent) !important;
}

/* Work Items toolbar contract: compact icon controls with the same hover. */
:deep(.ppt-group .icon-only-trigger) {
  width: 42px !important;
  min-width: 42px !important;
  padding: 0 !important;
  gap: 0 !important;
}

:deep(.ppt-group .icon-only-trigger i) {
  color: currentColor !important;
  transition: color 0.2s ease;
}

:deep(.ppt-group .icon-only-trigger:hover i),
:deep(.ppt-group .icon-only-trigger.active i) {
  color: var(--color-accent) !important;
}

/* Force standard height and typography for all toolbar elements */
:deep(.ppt-group button),
:deep(.ppt-group select),
:deep(.ppt-group input),
:deep(.ppt-group .el-dropdown) {
  height: 34px !important;
  min-height: 34px !important;
  box-sizing: border-box !important;
  display: inline-flex !important;
  align-items: center !important;
  justify-content: center !important;
  font-size: 13px !important;
  border-radius: 9px !important;
}

/* Standardize outlined buttons in toolbar */
:deep(.ppt-group .nexus-btn-outlined),
:deep(.ppt-group .btn-secondary) {
  background-color: transparent !important;
  border: 1px solid var(--color-border) !important;
  color: var(--color-text-secondary) !important;
  padding: 0 12px !important;
  gap: 6px !important;
  font-weight: 500 !important;
}

:deep(.ppt-group .nexus-btn-outlined:hover),
:deep(.ppt-group .btn-secondary:hover) {
  background-color: var(--color-surface-hover) !important;
  color: var(--color-text-primary) !important;
  border-color: var(--color-border-hover) !important;
}

/* Standardize icon-only buttons */
:deep(.ppt-group .nexus-btn-icon) {
  width: 36px !important;
  padding: 0 !important;
  border: 1px solid var(--color-border) !important;
  background-color: transparent !important;
  color: var(--color-text-secondary) !important;
}

:deep(.ppt-group .nexus-btn-icon:hover) {
  background-color: var(--color-surface-hover) !important;
  color: var(--color-text-primary) !important;
}

/* Specific standard for view toggles */
:deep(.view-toggles) {
  display: flex !important;
  align-items: center !important;
  gap: 2px !important;
  background-color: var(--color-surface-hover) !important;
  padding: 2px !important;
  border-radius: 8px !important;
  height: 32px !important;
  border: 1px solid var(--color-border) !important;
}

:deep(.view-toggles button),
:deep(.view-toggles .toggle-btn) {
  height: 28px !important;
  min-height: 28px !important;
  width: 28px !important;
  border: 1px solid transparent !important;
  background: transparent !important;
  color: var(--color-text-muted) !important;
  border-radius: 6px !important;
  transition: all 0.2s ease !important;
  cursor: pointer !important;
}

:deep(.view-toggles button:hover),
:deep(.view-toggles .toggle-btn:hover) {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important;
  color: var(--color-accent) !important;
}

:deep(.view-toggles button.active),
:deep(.view-toggles .toggle-btn.active) {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  background: color-mix(in srgb, var(--color-accent) 14%, var(--color-surface)) !important;
  color: var(--color-accent) !important;
  box-shadow: 0 1px 3px rgba(0,0,0,0.1) !important;
}

@media (max-width: 900px) {
  .project-page-toolbar { align-items: stretch; flex-wrap: wrap; }
  .ppt-left, .ppt-right { width: 100%; flex-wrap: wrap; }
  .ppt-right { margin-left: 0; justify-content: flex-end; }
  .ppt-search { width: min(100%, 320px); }
}

/* Dropdown styling overrides for high-density components inside the toolbar */
:deep(.filter-select-option),
:deep(.sort-option) {
  height: auto !important;
  min-height: 32px !important;
  display: inline-flex !important;
  align-items: center !important;
  justify-content: flex-start !important;
  font-size: 13px !important;
  border-radius: 8px !important;
  margin: 0 !important;
  padding: 5px 9px !important;
  border: 0 !important;
  border-left: 4px solid transparent !important;
  white-space: nowrap !important;
  width: 100% !important;
}

:deep(.filter-select-option.selected),
:deep(.sort-option.selected) {
  background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface)) !important;
  border-left-color: var(--color-accent) !important;
  border-radius: 8px !important;
  color: var(--color-accent) !important;
  font-weight: 650 !important;
}

:deep(.filter-select-option:hover),
:deep(.sort-option:hover) {
  background: var(--color-surface-hover) !important;
  color: var(--color-text-primary) !important;
}

:deep(.sort-direction-inline button) {
  height: 30px !important;
  min-height: 30px !important;
  width: 32px !important;
  justify-content: center !important;
  border-radius: 8px !important;
}
</style>
