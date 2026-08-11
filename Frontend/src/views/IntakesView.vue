<script setup>

import IntakeInbox from '@/components/IntakeInbox.vue'
import ProjectPageContainer from '@/components/common/ProjectPageContainer.vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const projectId = route.params.id || localStorage.getItem('lastProjectId')
</script>

<template>
  <ProjectPageContainer class="intakes-page">
    <IntakeInbox :projectId="projectId" />
  </ProjectPageContainer>
</template>

<style scoped>
.intakes-page {
  width: 100%;
  height: 100%;
  min-height: 0;
  padding: 0;
  overflow: hidden;
  background: var(--color-bg);
  box-sizing: border-box;
}

</style>


