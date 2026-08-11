export default [
  {
    path: '/',
    name: 'Home',
    component: () => import('../views/Home.vue')
  },
  {
    path: '/billing/checkout/:planCode',
    name: 'BillingCheckout',
    component: () => import('../views/BillingCheckout.vue')
  }
]
