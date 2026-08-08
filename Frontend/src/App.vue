<template>
  <ErrorBoundary>
    <router-view v-slot="{ Component }">
      <Transition name="route-soft" mode="out-in">
        <component :is="Component" />
      </Transition>
    </router-view>
  </ErrorBoundary>
</template>

<script setup>
import { onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import ErrorBoundary from '@/components/ErrorBoundary.vue'
import { getStoredAccessToken } from '@/utils/authSession'
import { updateThemeAttributes } from '@/utils/theme'

const route = useRoute()

// Update theme attributes on route change to handle specialized pages (Login/Register)
watch(() => route.path, (newPath) => {
  updateThemeAttributes(newPath)
})

onMounted(async () => {
  // Initialize theme from localStorage first for immediate feedback
  const savedTheme = localStorage.getItem('theme') || 'light'
  document.documentElement.setAttribute('data-theme', savedTheme)

  // Sync with backend if logged in
  const token = getStoredAccessToken()
  if (token) {
    try {
      const res = await axiosClient.get('/settings/ThemeSettings')
      const data = res.data?.data
      if (data) {
        // Apply persisted theme tokens from backend
        // This ensures the user's specific color choices are respected globally
        const tokenMap = {
          'bgLayout': '--color-bg',
          'bgCard': '--color-surface',
          'textPrimary': '--color-text-primary',
          'borderColor': '--color-border',
          'accentColor': '--color-accent'
        }
        
        Object.entries(data).forEach(([key, value]) => {
          if (tokenMap[key] && value) {
            document.documentElement.style.setProperty(tokenMap[key], value)
          }
        })
      }
    } catch (e) {
      console.warn('Backend theme sync skipped or failed.')
    }
  }
})
</script>

<style>
/* Global Resets & Base Styles are in style.css */

.route-soft-enter-active,
.route-soft-leave-active {
  transition: opacity 180ms ease, transform 180ms cubic-bezier(0.2, 0.8, 0.2, 1), filter 180ms ease;
}

.route-soft-enter-from {
  opacity: 0;
  transform: translateY(8px);
  filter: blur(2px);
}

.route-soft-leave-to {
  opacity: 0;
  transform: translateY(-4px);
  filter: blur(1px);
}

@media (prefers-reduced-motion: reduce) {
  .route-soft-enter-active,
  .route-soft-leave-active {
    transition: none;
  }
}
</style>
