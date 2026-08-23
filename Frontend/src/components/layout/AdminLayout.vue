<template>
  <div class="admin-layout">
    <AdminSidebar />
    <main class="admin-content">
      <slot></slot>
    </main>
  </div>
</template>

<script setup>
import AdminSidebar from './AdminSidebar.vue'
</script>

<style scoped>
.admin-layout {
  display: flex;
  min-height: 100vh;
  background-color: var(--color-bg);
  color: var(--color-text-primary);
  font-family: 'Inter', system-ui, sans-serif;
}

.admin-content {
  flex: 1;
  background-color: var(--color-bg);
  padding: 32px;
  overflow-y: auto;
  height: 100vh;
}
</style>




