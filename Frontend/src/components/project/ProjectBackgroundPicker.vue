<template>
  <div class="background-picker">
    <div class="picker-search">
      <i class="bi bi-search"></i>
      <input v-model="query" type="search" placeholder="Search backgrounds..." aria-label="Search project backgrounds" />
    </div>

    <div class="background-scroll-area">
      <div class="background-groups">
        <section v-for="group in filteredGroups" :key="group.id" class="background-group">
          <div class="group-heading">
            <span>{{ group.label }}</span>
            <span>{{ group.themes.length }}</span>
          </div>

          <div class="background-grid">
            <button
              v-for="background in group.themes"
              :key="background.id"
              type="button"
              class="background-option"
              :class="{ selected: modelValue === background.id }"
              :aria-label="background.label"
              :aria-pressed="modelValue === background.id"
              @click="$emit('update:modelValue', background.id)"
            >
              <span class="background-swatch" :style="{ background: background.value }">
                <span v-if="modelValue === background.id" class="selection-mark" :class="{ light: background.tone === 'light' }">
                  <i class="bi bi-check"></i>
                </span>
              </span>
              <span class="theme-name">{{ background.label }}</span>
            </button>
          </div>
        </section>
      </div>

      <p v-if="filteredGroups.length === 0" class="picker-empty">No matching backgrounds.</p>
    </div>
  </div>
</template>

<script setup>
import { computed, ref } from 'vue'
import { PROJECT_BACKGROUND_GROUPS } from '@/config/projectAppearance'

defineProps({ modelValue: { type: String, required: true } })
defineEmits(['update:modelValue'])

const query = ref('')
const filteredGroups = computed(() => {
  const keyword = query.value.trim().toLowerCase()
  if (!keyword) return PROJECT_BACKGROUND_GROUPS

  return PROJECT_BACKGROUND_GROUPS
    .map(group => ({
      ...group,
      themes: group.themes.filter(theme =>
        `${theme.label} ${theme.id} ${group.label}`.toLowerCase().includes(keyword)
      )
    }))
    .filter(group => group.themes.length)
})
</script>

<style scoped>
.background-picker { display: grid; grid-template-rows: auto minmax(0, 1fr); gap: 14px; min-height: 0; }
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
.background-scroll-area { min-height: 0; max-height: 320px; overflow: auto; padding-right: 4px; scrollbar-width: thin; }
.background-groups { display: flex; flex-direction: column; gap: 22px; }
.background-group { min-width: 0; }
.group-heading {
  display: flex; align-items: center; justify-content: space-between; margin-bottom: 9px;
  color: var(--color-text-muted); font-size: 10px; font-weight: 750; letter-spacing: 0; text-transform: uppercase;
}
.group-heading span:last-child { font-size: 10px; font-weight: 650; opacity: .72; }
.background-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 10px; }
.background-option {
  min-width: 0; padding: 4px 4px 7px; overflow: hidden; border: 1px solid transparent; border-radius: 9px;
  background: transparent; color: var(--color-text-secondary); cursor: pointer; text-align: left;
  transition: transform .18s ease, box-shadow .18s ease, border-color .18s ease, background-color .18s ease;
}
.background-option:hover { transform: translateY(-1px); background: var(--color-surface-hover); box-shadow: 0 7px 18px rgba(15, 23, 42, .07); }
.background-option.selected {
  border-color: color-mix(in srgb, var(--color-accent) 68%, var(--color-border));
  background: color-mix(in srgb, var(--color-accent) 5%, var(--color-surface));
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-accent) 7%, transparent);
}
.background-swatch {
  position: relative; display: block; width: 100%; height: 82px; overflow: hidden; border: 1px solid rgba(15, 23, 42, .1);
  border-radius: 7px; box-shadow: inset 0 1px 0 rgba(255, 255, 255, .12), inset 0 -1px 0 rgba(15, 23, 42, .06);
}
.selection-mark {
  position: absolute; top: 7px; right: 7px; display: grid; place-items: center; width: 18px; height: 18px;
  border: 1px solid rgba(255,255,255,.48); border-radius: 5px; background: rgba(15,23,42,.7); color: #fff;
  box-shadow: 0 1px 3px rgba(15,23,42,.15); font-size: 11px;
}
.selection-mark.light { border-color: rgba(15,23,42,.12); background: rgba(255,255,255,.82); color: #334155; }
.selection-mark i { line-height: 1; }
.theme-name {
  display: block; min-width: 0; margin: 7px 3px 0; overflow: hidden; color: var(--color-text-secondary);
  font-size: 10.5px; font-weight: 650; line-height: 1.2; text-overflow: ellipsis; white-space: nowrap;
}
.background-option.selected .theme-name { color: var(--color-text-primary); font-weight: 750; }
.picker-empty { margin: 40px 0; text-align: center; color: var(--color-text-muted); font-size: 13px; }
@media (max-width: 640px) {
  .background-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .background-swatch { height: 76px; }
}
</style>
