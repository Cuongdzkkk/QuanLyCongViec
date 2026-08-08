import { defineStore } from 'pinia'
import axiosClient from '@/api/axiosClient'
import { useSiteStore } from '@/store/useSiteStore'
import { ElMessage } from 'element-plus'
import { ensureWorkspaceIdFromState, resolveWorkspaceIdFromState } from '@/utils/contextIds'
import { useStarredStore } from '@/store/useStarredStore'
import { STARRED_ENTITY_TYPES } from '@/api/starredRecentApi'

export const useHomeProjectStore = defineStore('homeProject', {
  state: () => ({
    lessons: [],
    risks: [],
    decisions: [],
    projects: [],
    currentProject: null,
    linkedGoals: [],
    linkedTasks: [],
    relatedProjects: [],
    history: [],
    updates: [],
    isLoading: false,
    error: null,
    isEmpty: false,
    isSuccess: false
  }),
  actions: {
    getWorkspaceId() {
      const siteStore = useSiteStore()
      return resolveWorkspaceIdFromState({ siteStore, project: this.currentProject })
    },
    async ensureWorkspaceId() {
      const siteStore = useSiteStore()
      return ensureWorkspaceIdFromState({ siteStore, project: this.currentProject })
    },
    async fetchProjectTabs(projectId) {
      try {
        const [lessonsRes, risksRes, decisionsRes] = await Promise.all([
          axiosClient.get(`/projects/${projectId}/lessons`),
          axiosClient.get(`/projects/${projectId}/risks`),
          axiosClient.get(`/projects/${projectId}/decisions`)
        ])
        this.lessons = (lessonsRes.data.data || lessonsRes.data)
        this.risks = (risksRes.data.data || risksRes.data)
        this.decisions = (decisionsRes.data.data || decisionsRes.data)
      } catch (err) {
        console.error(err)
      }
    },
    async addProjectLesson(projectId, payload) {
      try {
        const res = await axiosClient.post(`/projects/${projectId}/lessons`, payload)
        this.lessons.unshift(res.data.data || res.data)
        return res.data
      } catch (err) { throw err }
    },
    async addProjectRisk(projectId, payload) {
      try {
        const res = await axiosClient.post(`/projects/${projectId}/risks`, payload)
        this.risks.unshift(res.data.data || res.data)
        return res.data
      } catch (err) { throw err }
    },
    async addProjectDecision(projectId, payload) {
      try {
        const res = await axiosClient.post(`/projects/${projectId}/decisions`, payload)
        this.decisions.unshift(res.data.data || res.data)
        return res.data
      } catch (err) { throw err }
    },
    async fetchProjects() {
      this.isLoading = true
      this.error = null
      this.isEmpty = false
      this.isSuccess = false
      try {
        const response = await axiosClient.get('/projects')
        this.projects = (response.data?.data || response.data || []).map(this.mapProjectFromApi)
        this.isEmpty = this.projects.length === 0
        this.isSuccess = true
      } catch (err) {
        this.error = err.message || 'Failed to fetch projects'
        this.projects = []
      } finally {
        this.isLoading = false
      }
    },
    mapProjectFromApi(data) {
      const ownerName = data.leadName || data.creatorName || data.owner || data.ownerName || ''
      return {
        ...data,
        id: data.id || data._id,
        workspaceId: data.workspaceId || data.WorkspaceId || null,
        title: data.title || data.name || data.Name || '',
        name: data.name || data.title || data.Name || '',
        owner: ownerName,
        ownerName,
        ownerId: data.leadUserId || data.creatorId || data.ownerId,
        ownerAvatarUrl: data.leadAvatarUrl || data.creatorAvatarUrl || data.ownerAvatarUrl,
        status: data.latestUpdateStatus || data.updateStatus || data.status,
        leadAvatarUrl: data.leadAvatarUrl,
        creatorAvatarUrl: data.creatorAvatarUrl,
        reason: data.why || data.Why || data.reason || '',
        success: data.successCriteria || data.SuccessCriteria || data.success || '',
        trackedLinkUrl: data.trackedLinkUrl || data.TrackedLinkUrl || '',
        startDate: data.startDate,
        createdAt: data.createdAt,
        updatedAt: data.updatedAt,
        isStarred: data.isStarred ?? data.isFavorite ?? false,
        isFollowing: data.isFollowing ?? false,
        isArchived: data.isArchived ?? data.status === false
      }
    },
    async fetchProjectDetail(projectId) {
      this.isLoading = true
      this.error = null
      try {
        const response = await axiosClient.get(`/projects/${projectId}`)
        if (response.data && (response.data.statusCode === 200 || !response.data.statusCode)) {
          const rawData = response.data?.data || response.data
          const mappedProject = this.mapProjectFromApi(rawData)
          this.project = mappedProject
          this.currentProject = mappedProject
          this.updates = rawData.updates || []
          
          try {
            const [risksRes, decisionsRes, lessonsRes, updatesRes] = await Promise.all([
              axiosClient.get(`/projects/${projectId}/risks`),
              axiosClient.get(`/projects/${projectId}/decisions`),
              axiosClient.get(`/projects/${projectId}/lessons`),
              axiosClient.get(`/projects/${projectId}/updates`)
            ]);
            
          this.project.risks = risksRes.data?.data || [];
          this.project.decisions = decisionsRes.data?.data || [];
          this.project.lessons = lessonsRes.data?.data || [];
          this.currentProject.risks = this.project.risks;
          this.currentProject.decisions = this.project.decisions;
          this.currentProject.lessons = this.project.lessons;
          this.updates = updatesRes.data?.data || [];
          } catch (subErr) {
            console.error('Error fetching sub-resources:', subErr);
          this.project.risks = [];
          this.project.decisions = [];
          this.project.lessons = [];
          this.currentProject.risks = [];
          this.currentProject.decisions = [];
          this.currentProject.lessons = [];
          this.updates = [];
          }
          this.isSuccess = true
        } else {
          this.error = response.data?.message || 'Failed to fetch project detail'
        }
      } catch (err) {
        this.error = err.message || 'Failed to fetch project detail'
      } finally {
        this.isLoading = false
      }
    },
    async toggleArchive() {
      if (!this.currentProject) return
      try {
        await axiosClient.put(`/projects/${this.currentProject.id}/archive`)
        this.currentProject.isArchived = true
      } catch (err) {
        console.error('Failed to archive project', err)
      }
    },
    async updateProjectOverview(projectId, fields) {
      if (!this.currentProject) return null
      const payload = {
        name: this.currentProject.name || this.currentProject.title,
        description: fields.description ?? this.currentProject.description ?? '',
        why: fields.reason ?? this.currentProject.reason ?? '',
        successCriteria: fields.success ?? this.currentProject.success ?? '',
        trackedLinkUrl: fields.trackedLinkUrl ?? this.currentProject.trackedLinkUrl ?? null,
        startDate: this.currentProject.startDate || new Date().toISOString(),
        endDate: this.currentProject.endDate || null,
        departmentId: this.currentProject.departmentId || null
      }
      const response = await axiosClient.put(`/projects/${projectId}`, payload)
      const updated = this.mapProjectFromApi(response.data?.data || response.data)
      this.currentProject = {
        ...this.currentProject,
        ...updated,
        lessons: this.currentProject.lessons || [],
        risks: this.currentProject.risks || [],
        decisions: this.currentProject.decisions || []
      }
      const index = this.projects.findIndex(project => project.id === projectId)
      if (index >= 0) this.projects[index] = { ...this.projects[index], ...updated }
      return this.currentProject
    },
    async toggleStar() {
      if (!this.currentProject) return
      try {
        const starredStore = useStarredStore()
        const nextValue = !starredStore.isStarred(STARRED_ENTITY_TYPES.PROJECT, this.currentProject.id)
        await starredStore.setStarred(STARRED_ENTITY_TYPES.PROJECT, this.currentProject.id, nextValue)
        this.currentProject.isStarred = nextValue
        const target = this.projects.find(p => p.id === this.currentProject.id)
        if (target) target.isStarred = this.currentProject.isStarred
      } catch (err) {
        console.error('Failed to toggle star', err)
        throw err
      }
    },
    async toggleFollow(projectId) {
      const current = this.currentProject?.id === projectId
        ? this.currentProject
        : this.projects.find(project => project.id === projectId)
      const previousValue = Boolean(current?.isFollowing)
      const nextValue = !previousValue
      if (this.currentProject?.id === projectId) this.currentProject.isFollowing = nextValue
      if (this.project?.id === projectId) this.project.isFollowing = nextValue
      const optimisticTarget = this.projects.find(p => p.id === projectId)
      if (optimisticTarget) optimisticTarget.isFollowing = nextValue
      try {
        const workspaceId = await this.ensureWorkspaceId()
        if (!workspaceId) throw new Error('No workspace selected')
        const res = await axiosClient.post(`/workspaces/${workspaceId}/followers/toggle`, null, {
          params: { entityType: 'Project', entityId: projectId }
        })
        const responseData = res.data?.data ?? res.data ?? {}
        const isFollowing = responseData.isFollowing ?? responseData.IsFollowing ?? nextValue
        if (this.currentProject?.id === projectId) this.currentProject.isFollowing = isFollowing
        if (this.project?.id === projectId) this.project.isFollowing = isFollowing
        const target = this.projects.find(p => p.id === projectId)
        if (target) target.isFollowing = isFollowing
      } catch (error) {
        if (this.currentProject?.id === projectId) this.currentProject.isFollowing = previousValue
        if (this.project?.id === projectId) this.project.isFollowing = previousValue
        const target = this.projects.find(p => p.id === projectId)
        if (target) target.isFollowing = previousValue
        console.error('Lỗi khi toggle follow:', error)
      }
    },
    async addRisk(projectId, payload) {
      try {
        const res = await axiosClient.post(`/projects/${projectId}/risks`, payload)
        if (res.data?.statusCode === 201) {
          if (!this.project.risks) this.project.risks = [];
          this.project.risks.unshift(res.data.data);
          ElMessage.success("Đã thêm rủi ro");
        }
      } catch(e) {
        console.error("Error adding risk", e);
        ElMessage.error("Không thể thêm rủi ro");
      }
    },
    async addDecision(projectId, payload) {
      try {
        const res = await axiosClient.post(`/projects/${projectId}/decisions`, payload)
        if (res.data?.statusCode === 201) {
          if (!this.project.decisions) this.project.decisions = [];
          this.project.decisions.unshift(res.data.data);
          ElMessage.success("Đã thêm quyết định");
        }
      } catch(e) {
        console.error("Error adding decision", e);
        ElMessage.error("Không thể thêm quyết định");
      }
    },
    async addLesson(projectId, payload) {
      try {
        const res = await axiosClient.post(`/projects/${projectId}/lessons`, payload)
        if (res.data?.statusCode === 201) {
          if (!this.project.lessons) this.project.lessons = [];
          this.project.lessons.unshift(res.data.data);
          ElMessage.success("Đã thêm bài học");
        }
      } catch(e) {
        console.error("Error adding lesson", e);
        ElMessage.error("Không thể thêm bài học");
      }
    },
    async addUpdate(projectId, payload) {
      try {
        const res = await axiosClient.post(`/projects/${projectId}/updates`, payload)
        if (res.data?.statusCode === 201) {
          if (!this.updates) this.updates = [];
          const update = res.data.data;
          this.updates.unshift(update);
          const nextStatus = update?.newStatus || update?.title || update?.status || payload?.title || payload?.status;
          if (nextStatus) {
            if (this.currentProject?.id === projectId) this.currentProject.status = nextStatus;
            if (this.project?.id === projectId) this.project.status = nextStatus;
            const target = this.projects.find(project => project.id === projectId);
            if (target) target.status = nextStatus;
          }
          ElMessage.success("Đã đăng bản cập nhật");
          return true;
        }
      } catch(e) {
        console.error("Error adding update", e);
        ElMessage.error("Không thể đăng bản cập nhật");
        return false;
      }
    },
    async addProjectUpdate(projectId, payload) {
      return this.addUpdate(projectId, payload)
    },
    async createProject(projectData) {
      this.isLoading = true
      try {
        const response = await axiosClient.post('/projects', projectData)
        const newProject = this.mapProjectFromApi(response.data?.data || response.data)
        if (this.projects) {
          this.projects.unshift(newProject)
        }
        return newProject
      } catch (err) {
        console.error('Failed to create project', err)
        throw err
      } finally {
        this.isLoading = false
      }
    }
  }
})
