import { defineStore } from 'pinia'
import axiosClient from '@/api/axiosClient'
import { reportExpectedError } from '@/utils/errorTelemetry'
import { clearScopedCurrentProjectId } from '@/utils/projectContext'
import { useStarredStore } from '@/store/useStarredStore'
import { STARRED_ENTITY_TYPES } from '@/api/starredRecentApi'

const PROJECT_BUNDLE_CACHE_TTL_MS = 30000
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5136/api'
const apiOrigin = new URL(apiBaseUrl, window.location.origin).origin
const resolveProjectId = (project) => project?.id || project?.Id || project?.projectId || project?.ProjectId || null
const projectIdentityKey = (project) => resolveProjectId(project) || `${project?.key || project?.Key || ''}:${project?.name || project?.Name || ''}`

const dedupeProjects = (projects = []) => {
  const seen = new Map()
  for (const project of projects) {
    const key = projectIdentityKey(project)
    if (!key || seen.has(key)) continue
    seen.set(key, project)
  }
  return Array.from(seen.values())
}

const readProjectUiConfig = (project) => {
  const rawConfig = project?.navigationConfig || project?.NavigationConfig || project?.cover || project?.Cover
  if (!rawConfig || typeof rawConfig !== 'string' || !rawConfig.trim().startsWith('{')) {
    return {}
  }

  try {
    return JSON.parse(rawConfig)
  } catch {
    return {}
  }
}

const readProjectCover = (project, config) => {
  const directCover = project.cover || project.Cover || null
  let candidate
  if (typeof directCover === 'string' && directCover.trim().startsWith('{')) {
    candidate = config.cover || config.coverUrl || null
  } else {
    candidate = project.coverUrl || project.CoverUrl || directCover || config.cover || config.coverUrl || null
  }

  if (typeof candidate !== 'string') return null
  if (candidate.startsWith('/uploads/')) return `${apiOrigin}${candidate}`
  return /^(https?:|data:image|blob:)/i.test(candidate) ? candidate : null
}

const upsertProject = (projects = [], rawProject) => {
  const mappedProject = mapProjectRow(rawProject)
  if (!mappedProject?.id) {
    return dedupeProjects(projects)
  }

  const nextProjects = [...dedupeProjects(projects)]
  const existingIndex = nextProjects.findIndex(project => project.id === mappedProject.id)
  if (existingIndex >= 0) {
    nextProjects.splice(existingIndex, 1, {
      ...nextProjects[existingIndex],
      ...mappedProject
    })
    return nextProjects
  }

  nextProjects.push(mappedProject)
  return dedupeProjects(nextProjects)
}

const defaultProjectNodes = (projectId) => ([
  {
    id: `${projectId}-dashboard`,
    key: 'dashboard',
    label: 'Dashboard',
    route: `/space/${projectId}/dashboard`
  },
  {
    id: `${projectId}-work-items`,
    key: 'work-items',
    label: 'Work items',
    route: `/space/${projectId}/work-items`
  },
  {
    id: `${projectId}-timeline`,
    key: 'timeline',
    label: 'Timeline',
    route: `/space/${projectId}/timeline`
  },
  {
    id: `${projectId}-cycles`,
    key: 'cycles',
    label: 'Cycles',
    route: `/space/${projectId}/cycles`
  },
  {
    id: `${projectId}-modules`,
    key: 'modules',
    label: 'Modules',
    route: `/space/${projectId}/modules`
  },
  {
    id: `${projectId}-reports`,
    key: 'reports',
    label: 'Reports',
    route: `/space/${projectId}/reports`
  },
  {
    id: `${projectId}-views`,
    key: 'views',
    label: 'Views',
    route: `/space/${projectId}/views`
  },
  {
    id: `${projectId}-pages`,
    key: 'pages',
    label: 'Pages',
    route: `/space/${projectId}/pages`
  }
])

const mapProjectRow = (project) => {
  const config = readProjectUiConfig(project)
  const projectId = resolveProjectId(project)
  const workspaceId = project.workspaceId || project.WorkspaceId || null

  return {
    id: projectId,
    workspaceId,
    name: project.name || project.Name || '',
    key: project.key || project.Key || project.identifier || project.Identifier || project.name?.substring(0, 4)?.toUpperCase() || project.Name?.substring(0, 4)?.toUpperCase() || 'PRJ',
    description: project.description || project.Description || '',
    icon: project.icon || project.Icon || config.icon || null,
    cover: readProjectCover(project, config),
    coverAltText: project.coverAltText || project.CoverAltText || config.coverAltText || null,
    networkType: project.networkType || project.NetworkType || 'Public',
    leadName: project.leadName || project.LeadName || project.creatorName || project.CreatorName || 'Project',
    isMember: typeof project.isMember === 'boolean' ? project.isMember : true,
    isFavorite: Boolean(project.isFavorite ?? project.IsFavorite),
    myRole: project.myRole || project.MyRole || null,
    createdAt: project.createdAt || project.CreatedAt || null,
    updatedAt: project.updatedAt || project.UpdatedAt || null,
    WorkspaceId: workspaceId,
    children: defaultProjectNodes(projectId),
    originalRow: project
  }
}

export const useProjectStore = defineStore('project', {
  state: () => ({ lessons: [], risks: [], decisions: [],
    currentProject: null,
    allProjects: [],
    expandedProjectIds: [],
    loading: false,
    error: null,
    members: [],
    tags: [],
    taskStatusesByProjectId: {},
    previewTasksByProjectId: {},
    projectDetailsById: {},
    membersByProjectId: {},
    labelsByProjectId: {},
    bundleFetchedAtByProject: {},
    detailsAbortController: null,
    detailsRequestId: 0
  }),
  getters: {
    sidebarProjects: (state) => dedupeProjects(state.allProjects).filter(project => project.isMember !== false),
    favoriteProjects: (state) => {
      const starredStore = useStarredStore()
      return dedupeProjects(state.allProjects).filter(project =>
        starredStore.isStarred(STARRED_ENTITY_TYPES.PROJECT, project.id)
      )
    },
    projectTree: (state) => dedupeProjects(state.allProjects).filter(project => project.isMember !== false).map(project => ({
      ...project,
      expanded: state.expandedProjectIds.includes(project.id)
    }))
  },
  actions: {
    applyProjectUpdate(rawProject) {
      const mappedProject = mapProjectRow(rawProject)
      if (!mappedProject?.id) return null

      this.allProjects = upsertProject(this.allProjects, mappedProject)
      this.projectDetailsById = { ...this.projectDetailsById, [mappedProject.id]: mappedProject }
      this.bundleFetchedAtByProject = { ...this.bundleFetchedAtByProject, [mappedProject.id]: Date.now() }
      if (this.currentProject?.id === mappedProject.id || this.currentProject?.Id === mappedProject.id) {
        this.currentProject = { ...this.currentProject, ...mappedProject }
      }
      return mappedProject
    },
    
    
    

    async fetchAllProjects(force = false) {
      if (!force && this.allProjects.length > 0) return this.allProjects;

      this.loading = true;
      this.error = null;
      try {
        const response = await axiosClient.get('/projects/discovery');
        const rows = response.data?.data || response.data || [];
        this.allProjects = dedupeProjects(rows.map(mapProjectRow));
        return this.allProjects;
      } catch (err) {
        this.error = err.message || 'Failed to fetch projects';
        reportExpectedError('Failed to fetch all projects', err);
        return [];
      } finally {
        this.loading = false;
      }
    },
    toggleProject(projectId) {
      if (!projectId) return;
      this.expandedProjectIds = Array.from(new Set(this.expandedProjectIds));
      if (this.expandedProjectIds.includes(projectId)) {
        this.expandedProjectIds = this.expandedProjectIds.filter(id => id !== projectId);
        return;
      }
      this.expandedProjectIds = [projectId];
    },
    expandProject(projectId) {
      if (!projectId || this.expandedProjectIds.includes(projectId)) return;
      this.expandedProjectIds = [projectId];
    },
    collapseProject(projectId) {
      this.expandedProjectIds = this.expandedProjectIds.filter(id => id !== projectId);
    },
    async updateFavorite(projectId, favorite) {
      await useStarredStore().setStarred(STARRED_ENTITY_TYPES.PROJECT, projectId, favorite)
      const index = this.allProjects.findIndex(project => project.id === projectId);
      if (index >= 0) {
        const current = this.allProjects[index];
        this.allProjects.splice(index, 1, { ...current, isFavorite: favorite });
      }
      if (this.currentProject?.id === projectId) {
        this.currentProject = { ...this.currentProject, isFavorite: favorite };
      }
    },
    clearProjectContext(projectId = null) {
      this.currentProject = projectId
        ? this.projectDetailsById[projectId] || this.allProjects.find(project => project.id === projectId) || null
        : null
      this.members = projectId ? (this.membersByProjectId[projectId] || []) : []
      this.tags = projectId ? (this.labelsByProjectId[projectId] || []) : []
    },
    hydrateProjectBundle(projectId) {
      if (!projectId || !this.projectDetailsById[projectId]) return null

      this.currentProject = this.projectDetailsById[projectId]
      this.members = this.membersByProjectId[projectId] || []
      this.tags = this.labelsByProjectId[projectId] || []

      const mappedProject = mapProjectRow(this.currentProject)
      if (mappedProject?.id) {
        this.allProjects = upsertProject(this.allProjects, mappedProject)
      }

      return this.currentProject
    },
    async prefetchProjectBundle(projectId, options = {}) {
      if (!projectId) return null

      const { force = false } = options
      const fetchedAt = this.bundleFetchedAtByProject[projectId]
      const isWarm = fetchedAt && (Date.now() - fetchedAt) < PROJECT_BUNDLE_CACHE_TTL_MS
      if (!force && isWarm && this.projectDetailsById[projectId]) {
        return {
          project: this.projectDetailsById[projectId],
          members: this.membersByProjectId[projectId] || [],
          labels: this.labelsByProjectId[projectId] || [],
          taskStatuses: this.taskStatusesByProjectId[projectId] || [],
          tasks: this.previewTasksByProjectId[projectId] || []
        }
      }

      try {
        const [projRes, membersRes, labelsRes, statusesRes, tasksRes] = await Promise.all([
          axiosClient.get(`/projects/${projectId}`),
          axiosClient.get(`/projects/${projectId}/members`),
          axiosClient.get(`/projects/${projectId}/labels`),
          axiosClient.get(`/projects/${projectId}/task-statuses`).catch(() => ({ data: { data: [] } })),
          axiosClient.get(`/projects/${projectId}/WorkTasks`).catch(() => ({ data: { data: [] } }))
        ])

        const project = projRes.data?.data ? mapProjectRow(projRes.data.data) : null
        const wId = project?.workspaceId || project?.WorkspaceId
        if (wId) {
          localStorage.setItem('recent_site_id', wId)
        }
        const members = membersRes.data?.data || []
        const labels = labelsRes.data?.data || []
        const taskStatuses = statusesRes.data?.data || []
        const tasks = (tasksRes.data?.data || []).filter(task => task?.projectId === projectId)

        this.projectDetailsById = { ...this.projectDetailsById, [projectId]: project }
        this.membersByProjectId = { ...this.membersByProjectId, [projectId]: members }
        this.labelsByProjectId = { ...this.labelsByProjectId, [projectId]: labels }
        this.taskStatusesByProjectId = { ...this.taskStatusesByProjectId, [projectId]: taskStatuses }
        this.previewTasksByProjectId = { ...this.previewTasksByProjectId, [projectId]: tasks }
        this.bundleFetchedAtByProject = { ...this.bundleFetchedAtByProject, [projectId]: Date.now() }

        if (project) {
          this.allProjects = upsertProject(this.allProjects, project)
        }

        return { project, members, labels, taskStatuses, tasks }
      } catch (err) {
        reportExpectedError('Failed to prefetch project bundle', err)
        return null
      }
    },
    async fetchProjectDetails(projectId, options = {}) {
      if (!projectId) return null;

      const {
        reset = true,
        background = false,
        force = false
      } = options;

      const fetchedAt = this.bundleFetchedAtByProject[projectId]
      const isWarm = fetchedAt && (Date.now() - fetchedAt) < PROJECT_BUNDLE_CACHE_TTL_MS
      if (!force && isWarm && this.projectDetailsById[projectId]) {
        if (reset) {
          this.clearProjectContext(projectId)
        }
        return this.hydrateProjectBundle(projectId)
      }

      if (!projectId || projectId === 'default' || projectId.length < 30) {
        this.clearProjectContext(projectId);
        return null;
      }

      this.detailsAbortController?.abort();
      const controller = new AbortController();
      const requestId = this.detailsRequestId + 1;
      this.detailsAbortController = controller;
      this.detailsRequestId = requestId;
      this.loading = !background;
      this.error = null;

      if (reset) {
        this.clearProjectContext(projectId);
      }

      try {
        const [projRes, membersRes, tagsRes] = await Promise.all([
          axiosClient.get(`/projects/${projectId}`, { signal: controller.signal }),
          axiosClient.get(`/projects/${projectId}/members`, { signal: controller.signal }),
          axiosClient.get(`/projects/${projectId}/labels`, { signal: controller.signal })
        ]);

        if (requestId !== this.detailsRequestId) {
          return null;
        }

        this.currentProject = projRes.data?.data ? mapProjectRow(projRes.data.data) : null;
        const wId = this.currentProject?.workspaceId || this.currentProject?.WorkspaceId
        if (wId) {
          localStorage.setItem('recent_site_id', wId)
        }
        this.members = membersRes.data?.data || [];
        this.tags = tagsRes.data?.data || [];
        this.projectDetailsById = { ...this.projectDetailsById, [projectId]: this.currentProject }
        this.membersByProjectId = { ...this.membersByProjectId, [projectId]: this.members }
        this.labelsByProjectId = { ...this.labelsByProjectId, [projectId]: this.tags }
        this.bundleFetchedAtByProject = { ...this.bundleFetchedAtByProject, [projectId]: Date.now() }

        if (this.currentProject) {
          this.allProjects = upsertProject(this.allProjects, this.currentProject);
        }

        return this.currentProject;
      } catch (err) {
        if (err?.name === 'CanceledError' || err?.code === 'ERR_CANCELED') {
          return null;
        }

        if ([403, 404].includes(err?.response?.status)) {
          clearScopedCurrentProjectId()
          this.clearProjectContext(projectId)
          return null
        }

        this.error = err.message || 'Failed to fetch project details';
        reportExpectedError('Failed to fetch project details', err);
        return null;
      } finally {
        if (requestId === this.detailsRequestId) {
          this.loading = false;
          this.detailsAbortController = null;
        }
      }
    }
  }
})
