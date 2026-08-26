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

app.directive('resizable', {
  mounted(el) {
    const table = el.tagName === 'TABLE' ? el : el.querySelector('table');
    if (!table) return;

    // Element plus already has resize if border is true. 
    // We just hide the vertical borders via CSS for el-table.
    if (el.classList.contains('el-table')) {
       const style = document.createElement('style');
       style.innerHTML = `
         .el-table--border .el-table__inner-wrapper::after,
         .el-table--border::after, .el-table--border::before { display: none !important; }
         .el-table--border th.el-table__cell, .el-table--border td.el-table__cell { border-right: none !important; }
       `;
       document.head.appendChild(style);
       return;
    }

    // Native table resizing
    const headers = table.querySelectorAll('th');
    headers.forEach(th => {
      const resizer = document.createElement('div');
      resizer.style.position = 'absolute';
      resizer.style.right = '0';
      resizer.style.top = '0';
      resizer.style.bottom = '0';
      resizer.style.width = '8px';
      resizer.style.cursor = 'col-resize';
      resizer.style.zIndex = '10';
      
      th.style.position = 'relative';
      th.appendChild(resizer);

      let startX, startWidth;
      const onMouseDown = (e) => {
        startX = e.pageX;
        startWidth = th.offsetWidth;
        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup', onMouseUp);
        e.preventDefault();
        e.stopPropagation();
      };

      const onMouseMove = (e) => {
        const newWidth = startWidth + (e.pageX - startX);
        th.style.width = `${newWidth}px`;
        th.style.minWidth = `${newWidth}px`;
      };

      const onMouseUp = () => {
        document.removeEventListener('mousemove', onMouseMove);
        document.removeEventListener('mouseup', onMouseUp);
      };

      resizer.addEventListener('mousedown', onMouseDown);
    });
  }
});

app.directive('draggable-y', {
  mounted(el) {
    el.style.cursor = 'grab';
    
    let isDragging = false;
    let hasMoved = false;
    let startY = 0;
    let initialTop = 0;

    const onPointerDown = (e) => {
      if (e.button !== 0) return; // Only left click
      isDragging = true;
      hasMoved = false;
      el.style.cursor = 'grabbing';
      startY = e.clientY;
      initialTop = el.getBoundingClientRect().top;
      
      document.addEventListener('pointermove', onPointerMove);
      document.addEventListener('pointerup', onPointerUp);
    };

    const onPointerMove = (e) => {
      if (!isDragging) return;
      const dy = e.clientY - startY;
      if (Math.abs(dy) > 3) hasMoved = true;
      
      let newTop = initialTop + dy;
      newTop = Math.max(0, Math.min(window.innerHeight - el.offsetHeight, newTop));
      
      el.style.top = `${newTop}px`;
      el.style.transform = 'none';
      el.style.bottom = 'auto'; // ensure bottom doesn't conflict
    };

    const onPointerUp = (e) => {
      isDragging = false;
      el.style.cursor = 'grab';
      document.removeEventListener('pointermove', onPointerMove);
      document.removeEventListener('pointerup', onPointerUp);
      
      if (hasMoved) {
        const preventClick = (clickEvent) => {
          clickEvent.stopPropagation();
          clickEvent.preventDefault();
          window.removeEventListener('click', preventClick, true);
        };
        window.addEventListener('click', preventClick, true);
        setTimeout(() => window.removeEventListener('click', preventClick, true), 50);
      }
    };

    el.addEventListener('pointerdown', onPointerDown);
  }
});

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
