import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    AutoImport({
      resolvers: [ElementPlusResolver()],
    }),
    Components({
      resolvers: [ElementPlusResolver()],
    }),
    VitePWA({
      registerType: 'autoUpdate',
      devOptions: {
        enabled: false
      },
      manifestFilename: 'manifest.webmanifest',
      includeAssets: ['favicon.svg', 'pwa-192x192.png', 'pwa-512x512.png'],
      manifest: {
        name: 'SprintA',
        short_name: 'SprintA',
        description: 'Nền tảng quản lý công việc cho đội nhóm Việt Nam',
        theme_color: '#2563EB',
        background_color: '#ffffff',
        display: 'standalone',
        start_url: '/',
        scope: '/',
        icons: [
          {
            src: '/pwa-192x192.png',
            sizes: '192x192',
            type: 'image/png'
          },
          {
            src: '/pwa-512x512.png',
            sizes: '512x512',
            type: 'image/png'
          }
        ]
      },
      workbox: {
        globPatterns: ['**/*.{js,css,html,ico,png,svg}'],
        navigateFallbackDenylist: [/^\/api/, /^\/kanban-hub/, /^\/auth/, /^\/hubs/, /^\/signalr/]
      }
    })
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },
  build: {
    chunkSizeWarningLimit: 1600,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes('node_modules')) return

          if (id.includes('/xlsx/')) return 'xlsx'
          if (id.includes('/echarts/') || id.includes('/vue-echarts/') || id.includes('/apexcharts/') || id.includes('/vue3-apexcharts/') || id.includes('/chart.js/') || id.includes('/vue-chartjs/')) return 'charts'
          if (id.includes('/@tiptap/') || id.includes('/prosemirror-')) return 'editor'
          if (id.includes('/element-plus/') || id.includes('/@element-plus/')) return 'ui'
          if (id.includes('/vue/') || id.includes('/vue-router/') || id.includes('/pinia/') || id.includes('/@vue/')) return 'framework'

          return 'vendor'
        }
      }
    }
  },
  server: {
    host: '0.0.0.0',
    port: 5173,
    strictPort: false,
    headers: {
      'Cross-Origin-Opener-Policy': 'same-origin-allow-popups'
    },
    allowedHosts: ['sprinta.id.vn'],
    proxy: {
      '/api': {
        target: 'http://localhost:5136',
        changeOrigin: true
      },
      '/kanban-hub': {
        target: 'http://localhost:5136',
        ws: true,
        changeOrigin: true
      }
    }
  }
})
