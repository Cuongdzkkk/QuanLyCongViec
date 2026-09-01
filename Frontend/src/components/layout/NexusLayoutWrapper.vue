<template>
  <NexusLayout>
    <router-view v-slot="{ Component, route }">
      <Transition name="route-soft" mode="out-in">
        <KeepAlive include="CollaborationChat">
          <component :is="Component" :key="route.name === 'CollaborationChat' ? 'CollaborationChat' : route.path" />
        </KeepAlive>
      </Transition>
    </router-view>
  </NexusLayout>
</template>

<script setup>
import NexusLayout from '@/components/layout/NexusLayout.vue'
</script>

