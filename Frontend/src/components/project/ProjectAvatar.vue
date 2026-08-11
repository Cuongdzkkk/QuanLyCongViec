<template>
  <span
    class="project-avatar"
    :class="[`project-avatar--${size}`]"
    :style="avatarStyle"
    aria-hidden="true"
  >
    <i :class="`bi bi-${resolvedIcon}`"></i>
  </span>
</template>

<script setup>
import { computed } from 'vue'
import { getProjectBackgroundStyle, getProjectIconColor, normalizeProjectIcon } from '@/config/projectAppearance'

const props = defineProps({
  icon: { type: String, default: '' },
  background: { type: String, default: '' },
  size: { type: String, default: 'md' },
  showBackground: { type: Boolean, default: true }
})

const resolvedIcon = computed(() => normalizeProjectIcon(props.icon))
const avatarStyle = computed(() => ({
  color: getProjectIconColor(props.background),
  background: props.showBackground ? getProjectBackgroundStyle(props.background) : 'transparent'
}))
</script>

<style scoped>
.project-avatar {
  display: inline-flex;
  flex: 0 0 auto;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  box-sizing: border-box;
  padding: 6px;
  border: 1px solid rgba(255, 255, 255, 0.22);
  border-radius: 10px;
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.24), 0 5px 14px rgba(15, 23, 42, 0.12);
}

.project-avatar > i {
  width: auto !important;
  margin: 0 !important;
  color: inherit !important;
  font-size: inherit !important;
  line-height: 1 !important;
}

.project-avatar--xs { width: 28px; height: 28px; padding: 5px; border-radius: 8px; font-size: 14px; }
.project-avatar--sm { width: 32px; height: 32px; padding: 6px; border-radius: 9px; font-size: 16px; }
.project-avatar--md { width: 40px; height: 40px; padding: 7px; font-size: 20px; }
.project-avatar--lg { width: 58px; height: 58px; padding: 8px; border-radius: 14px; font-size: 27px; }
.project-avatar--card {
  width: 52px;
  min-width: 52px;
  max-width: 52px;
  height: 52px;
  min-height: 52px;
  max-height: 52px;
  flex-basis: 52px;
  padding: 12px;
  border-radius: 15px;
  font-size: 24px;
}
.project-avatar--xl { width: 68px; height: 68px; padding: 10px; border-radius: 16px; font-size: 32px; }
</style>
