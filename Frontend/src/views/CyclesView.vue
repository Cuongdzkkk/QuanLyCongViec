<script setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import CyclesTab from '@/components/CyclesTab.vue'

const route = useRoute()
const projectId = computed(() => route.params.id || null)
</script>

<template>
  <div class="cycles-route-shell">
    <CyclesTab v-if="projectId && projectId !== 'default'" :key="projectId" :projectId="projectId" />
    <div v-else class="text-muted text-center pt-10">Không có dự án để tải Cycle.</div>
  </div>
</template>

<style scoped>
.cycles-route-shell {
  min-height: 100%;
  background: var(--color-bg);
  color: var(--color-text-primary);
  font-family: var(--sp-font-ui);
}
</style>
