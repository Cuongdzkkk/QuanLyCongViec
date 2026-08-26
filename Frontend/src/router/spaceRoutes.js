import axiosClient from '@/api/axiosClient'
import { buildSpacePath } from '@/utils/spaceRoute'

const ensureProjectRoute = async (to) => {
  const projectId = String(to.params.id || '').trim()
  if (!projectId) {
    return { path: '/your-work' }
  }

  try {
    await axiosClient.get(`/projects/${projectId}`)
    return true
  } catch (error) {
    const status = Number(error?.response?.status || 0)

    if (status === 404) {
      return {
        path: '/your-work',
        query: { invalidProject: projectId }
      }
    }

    return true
  }
}

const spaceChildren = [
  {
    path: '',
    redirect: to => buildSpacePath({ id: to.params.id, name: to.params.spaceSlug }, 'work-items')
  },
  {
    path: 'profile/:profileId',
    name: 'SpaceProfileDetail',
    component: () => import('../views/HomeSite/People/ProfileDetail.vue')
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
    redirect: to => buildSpacePath({ id: to.params.id, name: to.params.spaceSlug }, 'work-items')
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

const legacySpaceRedirect = to => {
  const childPath = `${to.params.legacyPath || 'work-items'}`
  return {
    path: buildSpacePath(to.params.id, childPath === 'dashboard' ? 'work-items' : childPath),
    query: to.query
  }
}

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
        path: 'space/:spaceSlug/:id([0-9a-fA-F-]{32,36})',
        component: () => import('../components/layout/ProjectLayoutWrapper.vue'),
        meta: { isSpaceContext: true },
        beforeEnter: ensureProjectRoute,
        children: spaceChildren
      },
      {
        path: 'space/:id([0-9a-fA-F-]{32,36})/:legacyPath?',
        redirect: legacySpaceRedirect
      }
    ]
  }
]
