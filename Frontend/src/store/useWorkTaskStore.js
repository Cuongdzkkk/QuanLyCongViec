import { defineStore } from 'pinia'
import axiosClient from '@/api/axiosClient'
import { useSiteStore } from './useSiteStore'
import { useProjectStore } from './useProjectStore'
import { useStarredStore } from './useStarredStore'
import { ensureWorkspaceIdFromState, isValidEntityId, resolveWorkspaceIdFromState } from '@/utils/contextIds'
import { getProjectBackgroundStyle } from '@/config/projectAppearance'
import { STARRED_ENTITY_TYPES } from '@/api/starredRecentApi'

const normalizeDateOnly = (value) => {
  if (!value) return null
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)) return value
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(value)) return value.slice(0, 10)
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  const year = date.getFullYear()
  const month = `${date.getMonth() + 1}`.padStart(2, '0')
  const day = `${date.getDate()}`.padStart(2, '0')
  return `${year}-${month}-${day}`
}

import { normalizeProjectRole } from '@/utils/permissions'

const normalizeTaskRecord = (task = {}, fallbackProjectId = null) => {
  const parentId = task.parentTaskId || task.parentId || task.ParentTaskId || task.ParentId || null
  const id = task.id || task.Id || null
  const projectId = task.projectId || task.ProjectId || fallbackProjectId || null
  const assignees = Array.isArray(task.assignees)
    ? task.assignees
    : Array.isArray(task.Assignees)
      ? task.Assignees
      : []
  const assigneeIds = Array.from(new Set([
    ...(Array.isArray(task.assigneeIds) ? task.assigneeIds : []),
    ...(Array.isArray(task.AssigneeIds) ? task.AssigneeIds : []),
    ...assignees.map(item => item?.userId || item?.id).filter(Boolean),
    ...(task.assignedUserId || task.AssignedUserId ? [task.assignedUserId || task.AssignedUserId] : [])
  ]))

  return {
    ...task,
    id,
    projectId,
    parentId,
    parentTaskId: parentId,
    assignedUserId: task.assignedUserId || task.AssignedUserId || null,
    assigneeIds,
    assignees: assignees
      .map(item => ({
        ...item,
        userId: item?.userId || item?.UserId || item?.id,
        fullName: item?.fullName || item?.FullName || item?.name,
        email: item?.email || item?.Email,
        progressPercent: item?.progressPercent ?? item?.ProgressPercent ?? 0,
        contributionWeight: item?.contributionWeight ?? item?.ContributionWeight ?? 1,
        estimatedHours: item?.estimatedHours ?? item?.EstimatedHours ?? 0,
        totalActualHours: item?.totalActualHours ?? item?.TotalActualHours ?? 0
      }))
      .filter(item => item.userId)
      .filter((item, index, list) => list.findIndex(candidate => candidate.userId === item.userId) === index),
    statusName: task.statusName || task.StatusName || '',
    taskStatusId: task.taskStatusId || task.TaskStatusId || null,
    taskTypeId: task.taskTypeId || task.TaskTypeId || null,
    rowVersion: task.rowVersion || task.RowVersion || null,
    sequenceId: task.sequenceId || task.SequenceId || null,
    sortOrder: task.sortOrder ?? task.SortOrder ?? 0,
    sprintId: task.sprintId || task.SprintId || null,
    moduleId: task.moduleId || task.ModuleId || null,
    totalEstimatedHours: task.totalEstimatedHours ?? task.TotalEstimatedHours ?? 0,
    totalActualHours: task.totalActualHours ?? task.TotalActualHours ?? 0,
      visibilityMode: `${task.visibilityMode || task.VisibilityMode || 'project'}`
        .trim()
        .toLowerCase()
        .replace(/\s+/g, '_'),
      visibleToRoles: (Array.isArray(task.visibleToRoles)
        ? task.visibleToRoles
        : Array.isArray(task.VisibleToRoles)
          ? task.VisibleToRoles
          : [])
        .map(role => normalizeProjectRole(role))
        .filter(Boolean),
    storyPoints: task.storyPoints ?? task.StoryPoints ?? 0,
    plannedStartDate: normalizeDateOnly(task.plannedStartDate || task.PlannedStartDate || null),
    plannedEndDate: normalizeDateOnly(task.plannedEndDate || task.PlannedEndDate || null),
    dueDate: normalizeDateOnly(task.dueDate || task.DueDate || null),
    createdAt: task.createdAt || task.CreatedAt || null,
    updatedAt: task.updatedAt || task.UpdatedAt || null
  }
}

export const useWorkTaskStore = defineStore('workTask', {
  state: () => ({
    tasks: [],
    loading: false,
    error: null,
    errorStatus: null,
    currentProjectId: null,
    fetchAbortController: null,
    fetchRequestId: 0,
    taskVersions: {}
  }),
  getters: {
    backlogTasks: (state) => (state.tasks || []).filter(t => {
       const s = (t.statusName || '').toUpperCase().trim();
       return s === 'BACKLOG' || s === '';
    }).sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0)),
    
    todoTasks: (state) => (state.tasks || []).filter(t => {
       const s = (t.statusName || '').toUpperCase().trim();
       return s === 'TODO' || s === 'TO DO';
    }).sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0)),
    
    inProgressTasks: (state) => (state.tasks || []).filter(t => {
       const s = (t.statusName || '').toUpperCase().trim();
       return s === 'IN PROGRESS' || s === 'INPROGRESS';
    }).sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0)),
    
    reviewTasks: (state) => (state.tasks || []).filter(t => {
       const s = (t.statusName || '').toUpperCase().trim();
       return s === 'IN REVIEW' || s === 'REVIEW';
    }).sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0)),

    doneTasks: (state) => (state.tasks || []).filter(t => {
       const s = (t.statusName || '').toUpperCase().trim();
       return s === 'DONE';
    }).sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0)),
  },
  actions: {
    normalizeTaskRecord(task = {}, fallbackProjectId = null) {
      return normalizeTaskRecord(task, fallbackProjectId)
    },
    upsertTask(task, fallbackProjectId = null) {
      const normalized = normalizeTaskRecord(task, fallbackProjectId || this.currentProjectId)
      if (!normalized.id) return null
      if (this.currentProjectId && normalized.projectId && `${normalized.projectId}` !== `${this.currentProjectId}`) {
        return null
      }

      const version = normalized.rowVersion
      const previousVersion = this.taskVersions[normalized.id]
      if (version && previousVersion === version) {
        return this.tasks.find(item => item.id === normalized.id) || normalized
      }

      const index = this.tasks.findIndex(item => item.id === normalized.id)
      if (index >= 0) {
        this.tasks[index] = { ...this.tasks[index], ...normalized }
      } else {
        this.tasks.push(normalized)
      }
      if (version) {
        this.taskVersions = { ...this.taskVersions, [normalized.id]: version }
      }
      return this.tasks.find(item => item.id === normalized.id) || normalized
    },
    removeTask(taskId) {
      if (!taskId) return
      this.tasks = this.tasks.filter(task => task.id !== taskId)
      if (this.taskVersions[taskId]) {
        const nextVersions = { ...this.taskVersions }
        delete nextVersions[taskId]
        this.taskVersions = nextVersions
      }
    },
    applyRealtimeEntityEvent(event) {
      if (!event) return null
      const entityType = `${event.entityType || ''}`.toLowerCase()
      if (event.projectId && this.currentProjectId && `${event.projectId}` !== `${this.currentProjectId}`) return null
      const action = `${event.action || ''}`.toLowerCase()
      if (entityType === 'task-label') {
        const task = this.tasks.find(item => `${item.id}` === `${event.entityId}`)
        const labelId = event.data?.labelId
        if (!task || !labelId) return null
        const currentLabelIds = Array.isArray(task.labelIds) ? task.labelIds : []
        task.labelIds = action === 'deleted' || action === 'removed'
          ? currentLabelIds.filter(id => `${id}` !== `${labelId}`)
          : Array.from(new Set([...currentLabelIds, labelId]))
        return task
      }
      if (entityType !== 'task') return null
      if (action === 'deleted' || action === 'removed') {
        this.removeTask(event.entityId)
        return null
      }
      return this.upsertTask(event.data, event.projectId)
    },
    clearTasks(projectId = null) {
      this.tasks = []
      this.taskVersions = {}
      this.error = null
      this.errorStatus = null
      this.currentProjectId = projectId
    },
    async fetchTasks(projectId, options = {}) {
      if (!projectId) return;

      const { reset = true } = options
      const previousProjectId = this.currentProjectId
      const requestId = this.fetchRequestId + 1
      this.fetchRequestId = requestId
      this.fetchAbortController?.abort()
      const controller = new AbortController()
      this.fetchAbortController = controller

      this.loading = true;
      this.error = null;
      this.errorStatus = null
      this.currentProjectId = projectId

      const shouldReset = reset && (previousProjectId !== projectId || !this.tasks.length)
      if (shouldReset) {
        this.tasks = []
      }

      try {
        const res = await axiosClient.get(`/projects/${projectId}/WorkTasks`, {
          signal: controller.signal
        });

        if (requestId !== this.fetchRequestId || this.currentProjectId !== projectId) {
          return this.currentProjectId === projectId ? this.tasks : []
        }

        this.tasks = (res.data?.data || []).map(task => normalizeTaskRecord(task, projectId))
        this.taskVersions = Object.fromEntries(
          this.tasks.filter(task => task.id && task.rowVersion).map(task => [task.id, task.rowVersion])
        )
        this.fetchStarredTasks().catch(() => {})
        return this.tasks
      } catch (err) {
        if (err?.name === 'CanceledError' || err?.code === 'ERR_CANCELED') {
          return this.currentProjectId === projectId ? this.tasks : []
        }

        this.error = err.message;
        this.errorStatus = Number(err?.response?.status || 0) || null
        console.error('Failed to fetch tasks:', err);
        return this.currentProjectId === projectId ? this.tasks : []
      } finally {
        if (requestId === this.fetchRequestId) {
          this.loading = false;
          this.fetchAbortController = null
        }
      }
    },
    async createTask(projectId, payload) {
      const tempId = `temp-${globalThis.crypto?.randomUUID?.() || `${Date.now()}-${Math.random().toString(36).slice(2)}`}`
      const optimisticTask = this.upsertTask({
        ...payload,
        id: tempId,
        projectId,
        statusName: payload.statusName || 'BACKLOG',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        isOptimistic: true
      }, projectId)
      try {
        const res = await axiosClient.post(`/projects/${projectId}/WorkTasks`, payload);
        this.removeTask(tempId)
        return this.upsertTask(res.data?.data, projectId);
      } catch (err) {
        if (optimisticTask) this.removeTask(tempId)
        console.error('Error creating task:', err);
        throw err;
      }
    },
    async updateTask(projectId, taskId, payload, options = {}) {
      const index = this.tasks.findIndex(t => t.id === taskId);
      const previousTask = index >= 0 ? { ...this.tasks[index] } : null;
      const method = options.method === 'put' ? 'put' : 'patch';

      if (index >= 0) {
        this.tasks[index] = { ...this.tasks[index], ...payload };
      }

      try {
        const res = method === 'put'
          ? await axiosClient.put(`/projects/${projectId}/WorkTasks/${taskId}`, payload)
          : await axiosClient.patch(`/projects/${projectId}/WorkTasks/${taskId}`, payload);
        const updatedTask = normalizeTaskRecord(res.data?.data || {}, projectId);
        return this.upsertTask(updatedTask, projectId);
      } catch (err) {
        if (index >= 0 && previousTask) {
          this.tasks[index] = previousTask;
        }
        this.error = err.response?.data?.message || err.message;
        throw err;
      }
    },
    async updateTaskStatus(projectId, taskId, statusName, options = {}) {
      const task = this.tasks.find(t => t.id === taskId);
      const previousTask = task ? { ...task } : null;
      const rowVersion = options.rowVersion || task?.rowVersion || task?.RowVersion || null;
      if (!rowVersion) {
        throw new Error('Task is missing rowVersion. Please reload the task before updating status.')
      }
      if (task) task.statusName = statusName;
      try {
        const res = await axiosClient.put(`/projects/${projectId}/WorkTasks/${taskId}/status`, {
          statusName,
          rowVersion
        });
        const updatedTask = normalizeTaskRecord(res.data?.data || {}, projectId);
        if (task && updatedTask?.id) {
          Object.assign(task, updatedTask)
        }
        this.upsertTask(updatedTask, projectId)
        return updatedTask
      } catch (err) {
        if (task && previousTask) Object.assign(task, previousTask);
        this.error = err.response?.data?.message || err.message;
        throw err;
      }
    },
    async reorderTask(projectId, taskId, sortOrder, newStatusName) {
      const task = this.tasks.find(t => t.id === taskId);
      const previousTask = task ? { ...task } : null;
      if (task) {
        task.sortOrder = sortOrder;
        if (newStatusName) task.statusName = newStatusName;
      }

      try {
        await axiosClient.put(`/projects/${projectId}/WorkTasks/${taskId}/reorder`, { sortOrder, newStatusName });
        return task
      } catch (err) {
        if (task && previousTask) Object.assign(task, previousTask);
        this.error = err.response?.data?.message || err.message;
        throw err;
      }
    },
    resolveWorkspaceId(projectId = null, taskProjectId = null) {
      const siteStore = useSiteStore()
      const projectStore = useProjectStore()
      const searchId = projectId || this.currentProjectId

      let project = projectStore.currentProject
      if (project && project.id === searchId) {
        const workspaceId = project.workspaceId || project.WorkspaceId
        if (isValidEntityId(workspaceId)) return workspaceId
      }

      if (searchId) {
        project = projectStore.projectDetailsById[searchId]
        const workspaceId = project?.workspaceId || project?.WorkspaceId
        if (isValidEntityId(workspaceId)) return workspaceId
      }

      if (searchId) {
        project = projectStore.allProjects.find(item => item.id === searchId)
        const workspaceId = project?.workspaceId || project?.WorkspaceId || project?.originalRow?.workspaceId || project?.originalRow?.WorkspaceId
        if (isValidEntityId(workspaceId)) return workspaceId
      }

      const taskProject = taskProjectId || projectId
      if (taskProject) {
        project = projectStore.allProjects.find(item => item.id === taskProject)
        const workspaceId = project?.workspaceId || project?.WorkspaceId || project?.originalRow?.workspaceId || project?.originalRow?.WorkspaceId
        if (isValidEntityId(workspaceId)) return workspaceId
      }

      return resolveWorkspaceIdFromState({ siteStore })
    },
    async fetchStarredTasks() {
      const starredStore = useStarredStore()
      await starredStore.fetchStarredItems({ page: 1, pageSize: 100 })
      return starredStore.starredItems.filter(item => item.itemType === STARRED_ENTITY_TYPES.WORK_TASK)
    },
    async toggleTaskStar(taskOrId) {
      if (!taskOrId) return
      const taskId = typeof taskOrId === 'object' ? taskOrId.id : taskOrId
      return useStarredStore().toggleStar(STARRED_ENTITY_TYPES.WORK_TASK, taskId)
    },
    isTaskStarred(taskId) {
      if (!taskId) return false
      const id = typeof taskId === 'object' ? taskId.id : taskId
      return useStarredStore().isStarred(STARRED_ENTITY_TYPES.WORK_TASK, id)
    },
    logViewedTask(task) {
      if (!task?.id) return Promise.resolve(null)
      return useStarredStore().recordViewed(STARRED_ENTITY_TYPES.WORK_TASK, task.id)
    }
  }
})
