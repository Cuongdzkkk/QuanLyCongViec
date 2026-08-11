<script setup>

import ViewsTab from '@/components/ViewsTab.vue'
</script>

<template>
  <div class="views-route-shell">
    <div class="views-route-content">
      <ViewsTab />
    </div>
  </div>
</template>

<style scoped>
.views-route-shell,
.views-route-content {
  width: 100%;
  height: 100%;
  min-height: 0;
}

.views-route-content {
  overflow: hidden;
  background: var(--color-bg);
}
</style>


