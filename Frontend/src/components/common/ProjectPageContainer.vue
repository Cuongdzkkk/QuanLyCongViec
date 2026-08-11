<template>
  <div class="project-page-container" :class="{ 'scrollable': scrollable }">
    <div class="project-page-inner">
      <slot></slot>
    </div>
  </div>
</template>

<script setup>
defineProps({
  scrollable: {
    type: Boolean,
    default: false
  }
})
</script>

<style scoped>
.project-page-container {
  width: 100%;
  height: 100%;
  flex: 1;
  display: flex;
  flex-direction: column;
  background: transparent;
  min-height: 0;
  overflow: hidden;
}
.project-page-container.scrollable {
  overflow-y: auto;
  overflow-x: hidden;
}
.project-page-inner {
  width: 100%;
  height: 100%;
  padding: 0 18px;
  display: flex;
  flex-direction: column;
  box-sizing: border-box;
  min-height: 0;
  flex: 1;
  overflow: hidden;
  overflow-anchor: auto;
  overscroll-behavior: auto;
}
.project-page-container.scrollable .project-page-inner {
  height: auto;
  min-height: 100%;
  overflow: visible;
  padding: 0 18px 24px 18px;
}

:deep(.project-page-inner > * + *) {
  margin-top: 14px;
}
:deep(.project-page-inner > .project-page-header + .project-page-toolbar) {
  margin-top: 0;
}

@media (max-width: 768px) {
  :deep(.project-page-inner > * + *) {
    margin-top: 12px;
  }
}
</style>
