<script setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import CyclesTab from '@/components/CyclesTab.vue'
import AppEmptyState from '@/components/common/Foundation/AppEmptyState.vue'

const route = useRoute()
const projectId = computed(() => route.params.id || null)
</script>

<template>
  <div class="cycles-route-shell">
    <CyclesTab v-if="projectId && projectId !== 'default'" :key="projectId" :projectId="projectId" />
    <AppEmptyState v-else icon="fa-solid fa-arrows-rotate" title="Chưa có dự án" description="Hãy chọn một dự án để xem các chu kỳ công việc." />
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
