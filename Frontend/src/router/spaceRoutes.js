export default [
  {
    path: '/',
    component: () => import('../components/layout/NexusLayoutWrapper.vue'),
    children: [
      {
        path: 'spaces',
        name: 'ManageSpaces',
        component: () => import('../views/ManageSpaces.vue')
      },
      {
        path: 'spaces/trash',
        name: 'GlobalTrashView',
        component: () => import('../views/GlobalTrashView.vue')
      },
      {
        path: 'spaces/categories',
        name: 'SpaceCategories',
        component: () => import('../views/SpaceCategories.vue')
      },
      {
        path: 'spaces/archive',
        name: 'GlobalArchivesViewSpace',
        component: () => import('../views/GlobalArchivesView.vue')
      },
      {
        path: 'space/:id',
        component: () => import('../components/layout/ProjectLayoutWrapper.vue'),
        meta: { isSpaceContext: true },
        children: [
          {
            path: '',
            redirect: to => `/space/${to.params.id}/dashboard`
          },
          {
            path: 'work-items',
            name: 'SpaceSummary',
            component: () => import('../views/SpaceSummary.vue')
          },
          {
            path: 'cycles',
            name: 'CyclesView',
            component: () => import('../views/CyclesView.vue')
          },
          {
            path: 'cycles/:cycleId',
            name: 'CycleDetailView',
            component: () => import('../views/SpaceSummary.vue')
          },
          {
            path: 'intakes',
            name: 'IntakesView',
            component: () => import('../views/IntakesView.vue')
          },
          {
            path: 'modules',
            name: 'ModulesView',
            component: () => import('../views/ModulesView.vue')
          },
          {
            path: 'views',
            name: 'ViewsViewSpace',
            component: () => import('../views/ViewsView.vue')
          },
          {
            path: 'pages',
            name: 'PagesView',
            component: () => import('../views/PagesView.vue')
          },
          {
            path: 'reports',
            name: 'ReportsView',
            component: () => import('../views/ReportsView.vue')
          },
          {
            path: 'dashboard',
            name: 'SpaceDashboard',
            component: () => import('../views/SpaceDashboard.vue')
          },
          {
            path: 'timeline',
            name: 'SpaceTimeline',
            component: () => import('../views/SpaceTimeline.vue')
          },
          {
            path: 'members',
            name: 'SpaceMembers',
            component: () => import('../views/SpaceMembers.vue')
          },
          {
            path: 'settings',
            name: 'ProjectSettings',
            component: () => import('../views/ProjectSettings.vue'),
            meta: { requiresProjectSettingsAccess: true }
          },
          {
            path: 'ai-intake',
            name: 'AiFileIntake',
            component: () => import('../views/AiFileIntake.vue')
          }
        ]
      }
    ]
  }
]
