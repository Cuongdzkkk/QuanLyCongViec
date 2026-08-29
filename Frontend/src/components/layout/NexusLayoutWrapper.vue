<template>
  <NexusLayout>
    <router-view v-slot="{ Component, route }">
      <Transition name="route-soft" mode="out-in">
        <component :is="Component" :key="route.path" />
      </Transition>
    </router-view>
  </NexusLayout>
</template>

<script setup>
import NexusLayout from '@/components/layout/NexusLayout.vue'
</script>
