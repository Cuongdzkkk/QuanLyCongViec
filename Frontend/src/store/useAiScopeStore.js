import { defineStore } from 'pinia'
import {
  clearScopedCurrentProjectId,
  setScopedCurrentProjectId
} from '@/utils/projectContext'

export const useAiScopeStore = defineStore('aiScope', {
  state: () => ({
    workspaceId: '',
    projectId: ''
  }),

  actions: {
    hydrate({ workspaceId, projectId } = {}) {
      if (workspaceId !== undefined && workspaceId !== null) this.workspaceId = `${workspaceId}`
      if (projectId !== undefined && projectId !== null) this.projectId = `${projectId}`
    },

    setWorkspace(workspaceId) {
      this.workspaceId = workspaceId ? `${workspaceId}` : ''
      this.projectId = ''
      clearScopedCurrentProjectId()
    },

    setProject(projectId) {
      this.projectId = projectId ? `${projectId}` : ''
      if (this.projectId) setScopedCurrentProjectId(this.projectId)
      else clearScopedCurrentProjectId()
    },

    clearProject() {
      this.projectId = ''
      clearScopedCurrentProjectId()
    }
  }
})
