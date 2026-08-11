import { createApp } from 'vue'
import { createPinia } from 'pinia'
import './style.css'
import './assets/styles/sprinta-theme.css'
import './assets/styles/sprinta-foundation.css'
import './utils/theme.js'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
import 'element-plus/theme-chalk/dark/css-vars.css'
import * as ElementPlusIconsVue from '@element-plus/icons-vue'
import App from './App.vue'
import router from './router'
import VueApexCharts from 'vue3-apexcharts'

const app = createApp(App)

for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component)
}

app.use(createPinia())
app.use(ElementPlus)
app.use(router)
app.use(VueApexCharts)
app.mount('#app')

// Register PWA Service Worker
import { registerSW } from 'virtual:pwa-register'
const registerPwa = import.meta.env.PROD
  ? registerSW
  : () => {
      if ('serviceWorker' in navigator) {
        navigator.serviceWorker.getRegistrations()
          .then((registrations) => Promise.all(registrations.map((registration) => registration.unregister())))
          .catch(() => {})
      }
    }
registerPwa({
  immediate: true,
  onNeedRefresh() {
    console.info('SprintA có phiên bản mới, vui lòng tải lại trang.')
  },
  onOfflineReady() {
    console.info('SprintA app shell đã sẵn sàng dùng khi offline.')
  }
})
