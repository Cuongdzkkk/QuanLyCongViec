<script setup>

import ModulesTab from '@/components/ModulesTab.vue'
import { useRoute } from 'vue-router'
import { ref, onMounted } from 'vue'
import axiosClient from '@/api/axiosClient'

const route = useRoute()
const projectId = ref(route.params.id)
const isReady = ref(false)

onMounted(async () => {
   let pId = projectId.value || localStorage.getItem('lastProjectId')
   if (!pId || pId === 'default') {
      try {
         const res = await axiosClient.get('/projects')
         if (res.data?.data?.length > 0) {
            pId = res.data.data[0].id
            localStorage.setItem('lastProjectId', pId)
         }
      } catch (e) {
         console.error('Failed to get default project')
      }
   }
   projectId.value = pId
   isReady.value = true
})
</script>

<template>
  <div class="modules-route-shell">
    <div class="modules-route-content">
      <ModulesTab v-if="isReady && projectId && projectId !== 'default'" :projectId="projectId" />
      <div v-else-if="isReady" class="text-muted text-center pt-10">No project available to load Modules.</div>
    </div>
  </div>
</template>

<style scoped>
.modules-route-shell,
.modules-route-content {
  width: 100%;
  height: 100%;
  min-height: 0;
}

.modules-route-content {
  overflow: hidden;
  background: var(--color-bg);
}
</style>


