export default [
  {
    path: '/',
    component: () => import('../components/layout/NexusLayoutWrapper.vue'),
    meta: { isSpaceContext: true },
    children: [
      {
        path: 'dashboard',
        name: 'Dashboard',
        component: () => import('../views/ForYou.vue')
      },
      {
        path: 'profile',
        name: 'Profile',
        component: () => import('../views/Profile.vue')
      },
      {
        path: 'your-work',
        name: 'YourWork',
        component: () => import('../views/YourWorkView.vue')
      },
      {
        path: 'stickies',
        name: 'Stickies',
        component: () => import('../views/StickiesView.vue')
      },
      {
        path: 'rewards',
        name: 'Rewards',
        component: () => import('../views/RewardsView.vue')
      },
      {
        path: 'drafts',
        name: 'Drafts',
        component: () => import('../views/DraftsView.vue')
      },
      {
        path: 'views',
        name: 'Views',
        component: () => import('../views/GlobalViewsView.vue')
      },
      {
        path: 'analytics',
        name: 'Analytics',
        component: () => import('../views/GlobalAnalyticsView.vue')
      },
      {
        path: 'archives',
        name: 'Archives',
        component: () => import('../views/GlobalArchivesView.vue')
      },
      {
        path: 'priority',
        name: 'DailyFocus',
        component: () => import('../views/DailyFocusView.vue')
      },
      {
        path: 'chat',
        name: 'CollaborationChat',
        component: () => import('../views/CollaborationChat.vue')
      },
      {
        path: 'feed',
        name: 'ActivityFeed',
        component: () => import('../views/ActivityFeed.vue')
      },
      {
        path: 'checkin',
        name: 'VirtualCheckin',
        component: () => import('../views/VirtualCheckin.vue')
      },
      {
        path: 'integrations',
        name: 'IntegrationHub',
        component: () => import('../views/IntegrationHubView.vue')
      }
    ]
  }
]
