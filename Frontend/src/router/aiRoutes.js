export default [
  {
    path: '/ai-assistant',
    alias: '/ai',
    name: 'AIPage',
    component: () => import('../views/AIPage.vue'),
    meta: {
      title: 'SprintA AI'
    }
  }
]
