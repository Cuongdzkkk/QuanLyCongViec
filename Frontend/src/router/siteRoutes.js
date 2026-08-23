export default [
  {
    path: '/sites',
    name: 'SitesForYou',
    component: () => import('../views/HomeSite/HomeSiteLayout.vue'),
    meta: { requiresAuth: true },
    children: [
      {
        path: '',
        name: 'SitesForYouContent',
        component: () => import('../views/SitesForYou.vue')
      }
    ]
  },
  {
    path: '/site-selection',
    name: 'SiteSelection',
    component: () => import('../views/SiteSelection.vue'),
    meta: { requiresAuth: true }
  },
  {
    path: '/home',
    name: 'HomeSite',
    component: () => import('../views/HomeSite/HomeSiteLayout.vue'),
    meta: { requiresAuth: true },
    children: [
      {
        path: '',
        name: 'HomeSiteIndex',
        redirect: '/home/for-you'
      },
      { path: 'for-you', name: 'HomeForYou', component: () => import('../views/SitesForYou.vue') },
      {
        path: 'teams',
        component: () => import('../views/HomeSite/Teams/TeamsWrapper.vue'),
        children: [
          {
            path: '',
            name: 'HomeTeamsDashboard',
            component: () => import('../views/HomeSite/Teams/TeamsDashboard.vue')
          },
          {
            path: 'list',
            name: 'HomeTeamList',
            component: () => import('../views/HomeSite/Teams/TeamList.vue')
          },
          {
            path: 'kudos',
            name: 'HomeTeamKudos',
            component: () => import('../views/HomeSite/Teams/TeamKudos.vue')
          },
          {
            path: 'people',
            name: 'HomeTeamPeople',
            component: () => import('../views/HomeSite/People/PeopleDirectory.vue')
          },
          {
            path: ':id',
            name: 'HomeTeamDetail',
            component: () => import('../views/HomeSite/Teams/TeamDetail.vue')
          }
        ]
      },
      { path: 'goals', name: 'HomeGoals', component: () => import('../views/HomeSite/Goals/GoalsDashboard.vue') },
      { path: 'goals/:id', name: 'HomeGoalDetail', component: () => import('../views/HomeSite/Goals/GoalDetail.vue') },
      { path: 'projects', name: 'HomeProjects', component: () => import('../views/HomeSite/Projects/ProjectList.vue') },
      { path: 'projects/:id', name: 'HomeProjectDetail', component: () => import('../views/HomeSite/Projects/ProjectDetail.vue') },
      { path: 'people', name: 'HomePeople', component: () => import('../views/HomeSite/People/PeopleDirectory.vue') },
      { path: 'profile/:id', name: 'HomeProfileDetail', component: () => import('../views/HomeSite/People/ProfileDetail.vue') },
      { path: 'recent', name: 'HomeRecentActivities', component: () => import('../views/HomeSite/Tools/RecentActivities.vue') },
      { path: 'audit-log', name: 'HomeAuditLog', component: () => import('../views/HomeSite/Tools/AuditLog.vue') },
      { path: 'starred', name: 'HomeStarred', component: () => import('../views/HomeSite/Tools/StarredList.vue') },
      { path: 'notifications', name: 'HomeNotifications', component: () => import('../views/HomeSite/Tools/NotificationsView.vue') },
      { path: 'status', name: 'HomeStatus', component: () => import('../views/HomeSite/Tools/SystemStatus.vue') }
    ]
  }
]
