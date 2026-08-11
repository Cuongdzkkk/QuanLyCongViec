<template>
  <div>
    <div class="plane-dashboard">
      <header class="nexus-feature-header">
        <div class="header-info">
          <p class="eyebrow">{{ t('dashboard.workspace') }}</p>
          <h1><i class="fa-solid fa-house"></i> {{ t('dashboard.title') }}</h1>
          <p class="muted">{{ t('dashboard.subtitle') }}</p>
        </div>
        <div class="nexus-controls-row">
          <button class="nexus-btn nexus-btn-outlined" type="button" @click="router.push('/spaces')">{{ t('dashboard.browseProjects') }}</button>
          <button class="nexus-btn nexus-btn-primary" type="button" @click="openTaskModal"><i class="fa-solid fa-plus"></i> {{ t('dashboard.newWorkItem') }}</button>
        </div>
      </header>

      <section class="hero-panel">
        <div class="hero-copy">
          <h2>{{ t('dashboard.heroTitle', { name: currentUser?.fullName || t('dashboard.teammateFallback') }) }}</h2>
          <p>{{ t('dashboard.heroSubtitle') }}</p>
        </div>
        <div class="hero-stats">
          <div class="stat-box">
            <span class="stat-label">{{ t('common.projects') }}</span>
            <strong>{{ visibleProjects.length }}</strong>
          </div>
          <div class="stat-box">
            <span class="stat-label">{{ t('dashboard.favorites') }}</span>
            <strong>{{ favoriteProjects.length }}</strong>
          </div>
        </div>
      </section>

      <section class="dashboard-focus-widget">
        <DailyFocusWidget />
      </section>

      <section class="search-panel">
        <div class="search-shell">
          <i class="fa-solid fa-magnifying-glass"></i>
          <input v-model="projectSearch" type="text" :placeholder="t('dashboard.filterProjectsPlaceholder')" />
        </div>
      </section>

      <section class="projects-panel">
        <div class="section-head items-center">
          <h2>
            <div class="icon-glass">
              <i class="bi bi-briefcase"></i>
            </div>
            {{ t('common.projects') }}
          </h2>
          <span>{{ t('common.shown', { count: filteredProjects.length }) }}</span>
        </div>

        <div v-if="projectStore.loading" class="empty-state">
          <i class="fa-solid fa-spinner fa-spin"></i>
          <p>{{ t('dashboard.loadingProjects') }}</p>
        </div>

        <div v-else-if="filteredProjects.length === 0" class="empty-state">
          <i class="fa-regular fa-folder-open"></i>
          <p>{{ t('dashboard.noProjectsMatch') }}</p>
        </div>

        <div v-else class="project-grid">
          <article v-for="project in filteredProjects" :key="project.id" class="project-card">
            <button
              class="favorite-btn"
              type="button"
              :disabled="starredStore.isPending(STARRED_ENTITY_TYPES.PROJECT, project.id)"
              :aria-pressed="starredStore.isStarred(STARRED_ENTITY_TYPES.PROJECT, project.id)"
              :aria-label="starredStore.isStarred(STARRED_ENTITY_TYPES.PROJECT, project.id) ? 'Remove project from starred' : 'Add project to starred'"
              @click="toggleFavorite(project)"
            >
              <i :class="starredStore.isStarred(STARRED_ENTITY_TYPES.PROJECT, project.id) ? 'fa-solid fa-star' : 'fa-regular fa-star'"></i>
            </button>

            <div class="project-cover" :style="{ background: projectBackground(project) }">
              <ProjectAvatar :icon="project.icon" :background="project.cover" size="lg" />
            </div>

            <div class="project-body">
              <div class="project-heading">
                <h3>{{ demoText(project.name) }}</h3>
                <span>{{ project.key }}</span>
              </div>
              <p>{{ demoText(project.description) || t('dashboard.noDescription') }}</p>
              <div class="project-meta">
                <span>{{ project.networkType || 'Public' }}</span>
                <span>{{ project.leadName || t('dashboard.projectOwner') }}</span>
              </div>
            </div>

            <div class="project-actions">
              <button class="secondary-btn small" type="button" @click="openProject(project.id)">{{ t('common.open') }}</button>
              <button class="primary-btn small" type="button" @click="openTaskModal(project)">{{ t('dashboard.newWorkItem') }}</button>
            </div>
          </article>
        </div>
      </section>

      <section v-if="favoriteProjects.length" class="projects-panel">
        <div class="section-head items-center">
          <h2>
            <div class="icon-glass purple">
              <i class="bi bi-star-fill"></i>
            </div>
            {{ t('dashboard.favorites') }}
          </h2>
          <span>{{ t('dashboard.starredCount', { count: favoriteProjects.length }) }}</span>
        </div>

        <div class="project-grid">
          <article v-for="project in favoriteProjects" :key="`favorite-${project.id}`" class="project-card">
            <button
              class="favorite-btn"
              type="button"
              :disabled="starredStore.isPending(STARRED_ENTITY_TYPES.PROJECT, project.id)"
              :aria-pressed="starredStore.isStarred(STARRED_ENTITY_TYPES.PROJECT, project.id)"
              :aria-label="starredStore.isStarred(STARRED_ENTITY_TYPES.PROJECT, project.id) ? 'Remove project from starred' : 'Add project to starred'"
              @click="toggleFavorite(project)"
            >
              <i :class="starredStore.isStarred(STARRED_ENTITY_TYPES.PROJECT, project.id) ? 'fa-solid fa-star' : 'fa-regular fa-star'"></i>
            </button>

            <div class="project-cover" :style="{ background: projectBackground(project) }">
              <ProjectAvatar :icon="project.icon" :background="project.cover" size="lg" />
            </div>

            <div class="project-body">
              <div class="project-heading">
                <h3>{{ demoText(project.name) }}</h3>
                <span>{{ project.key }}</span>
              </div>
              <p>{{ demoText(project.description) || t('dashboard.noDescription') }}</p>
              <div class="project-meta">
                <span>{{ project.networkType || 'Public' }}</span>
                <span>{{ project.leadName || t('dashboard.projectOwner') }}</span>
              </div>
            </div>

            <div class="project-actions">
              <button class="secondary-btn small" type="button" @click="openProject(project.id)">{{ t('common.open') }}</button>
            </div>
          </article>
        </div>
      </section>

      <el-dialog v-model="taskModalVisible" width="560px" append-to-body class="sa-data-dialog sa-modal--form" :show-close="false">
        <template #header>
          <DataModalHeader
            icon="bi bi-list-check"
            :title="t('dashboard.dialogTitle')"
            :description="t('dashboard.descriptionPlaceholder')"
            :close-label="t('common.close')"
            @close="taskModalVisible = false"
          />
        </template>
        <div class="task-form">
          <label>
            {{ t('common.project') }}
            <select v-model="taskForm.projectId">
              <option value="" disabled>{{ t('dashboard.selectProject') }}</option>
              <option v-for="project in visibleProjects" :key="project.id" :value="project.id">{{ demoText(project.name) }}</option>
            </select>
          </label>

          <label>
            {{ t('common.title') }}
            <input v-model="taskForm.title" type="text" :placeholder="t('dashboard.titlePlaceholder')" />
          </label>

          <label>
            {{ t('common.status') }}
            <select v-model="taskForm.statusName">
              <option value="BACKLOG">BACKLOG</option>
              <option value="TO DO">TO DO</option>
              <option value="IN PROGRESS">IN PROGRESS</option>
              <option value="DONE">DONE</option>
            </select>
          </label>

          <label>
            {{ t('dashboard.description') }}
            <textarea v-model="taskForm.description" rows="5" :placeholder="t('dashboard.descriptionPlaceholder')"></textarea>
          </label>
        </div>

        <template #footer>
          <div class="dialog-actions">
            <button class="cancel-btn" type="button" @click="taskModalVisible = false"><i class="bi bi-x-lg"></i>{{ t('common.cancel') }}</button>
            <button class="primary-btn" type="button" :disabled="submittingTask" @click="submitTask">
              {{ submittingTask ? t('common.creating') : t('dashboard.newWorkItem') }}
            </button>
          </div>
        </template>
      </el-dialog>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'

import { useProjectStore } from '@/store/useProjectStore'
import { useStarredStore } from '@/store/useStarredStore'
import { STARRED_ENTITY_TYPES } from '@/api/starredRecentApi'
import { useI18n } from '@/composables/useI18n'
import { translateDemoText } from '@/utils/demoContentLocale'
import DailyFocusWidget from '@/components/DailyFocusWidget.vue'
import ProjectAvatar from '@/components/project/ProjectAvatar.vue'
import { getProjectBackgroundStyle } from '@/config/projectAppearance'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'

const router = useRouter()
const projectStore = useProjectStore()
const starredStore = useStarredStore()
const { t, language } = useI18n()

const currentUser = ref(null)
const currentDateTime = ref('')
const projectSearch = ref('')
const taskModalVisible = ref(false)
const submittingTask = ref(false)
const taskForm = ref({
  projectId: '',
  title: '',
  description: '',
  statusName: 'TO DO'
})

const visibleProjects = computed(() => projectStore.sidebarProjects)
const favoriteProjects = computed(() => projectStore.favoriteProjects)
const demoText = (value) => translateDemoText(value, language.value)
const filteredProjects = computed(() => {
  const keyword = projectSearch.value.trim().toLowerCase()
  if (!keyword) return visibleProjects.value
  return visibleProjects.value.filter(project =>
    demoText(project.name)?.toLowerCase().includes(keyword) || project.key?.toLowerCase().includes(keyword)
  )
})

const updateTime = () => {
  const now = new Date()
  currentDateTime.value = now.toLocaleString(language.value === 'vi' ? 'vi-VN' : 'en-GB', {
    weekday: 'long',
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const fallbackCover = (project) => {
  const gradients = [
    'linear-gradient(135deg, var(--color-bg) 0%, var(--color-accent) 100%)',
    'linear-gradient(135deg, var(--bg-secondary) 0%, var(--color-success) 100%)',
    'linear-gradient(135deg, var(--bg-tertiary) 0%, var(--color-warning) 100%)',
    'linear-gradient(135deg, var(--color-surface) 0%, var(--color-danger) 100%)'
  ]
  const name = project?.name || ''
  const index = name.length % gradients.length
  return gradients[index]
}

const projectBackground = (project) => getProjectBackgroundStyle(project?.cover)

const openProject = (projectId) => {
  router.push(`/space/${projectId}`)
}

const openTaskModal = async (project = null) => {
  const availableProjects = visibleProjects.value.length
    ? visibleProjects.value
    : await projectStore.fetchAllProjects(true)

  if (!availableProjects.length) {
    ElMessage.warning(t('dashboard.createProjectFirst'))
    await router.push('/spaces')
    return
  }

  taskForm.value = {
    projectId: project?.id || availableProjects[0]?.id || '',
    title: '',
    description: '',
    statusName: 'TO DO'
  }
  taskModalVisible.value = true
}

const toggleFavorite = async (project) => {
  const nextValue = !starredStore.isStarred(STARRED_ENTITY_TYPES.PROJECT, project.id)
  try {
    await projectStore.updateFavorite(project.id, nextValue)
    ElMessage.success(nextValue ? t('dashboard.projectStarred') : t('dashboard.projectUnstarred'))
  } catch (error) {
    ElMessage.error(starredStore.error || t('dashboard.favoriteUpdateFailed'))
  }
}

const submitTask = async () => {
  if (!taskForm.value.projectId || !taskForm.value.title.trim()) {
    ElMessage.warning(t('dashboard.chooseProjectAndTitle'))
    return
  }

  submittingTask.value = true
  try {
    await axiosClient.post(`/projects/${taskForm.value.projectId}/WorkTasks`, {
      title: taskForm.value.title.trim(),
      description: taskForm.value.description,
      statusName: taskForm.value.statusName,
      priority: 3
    })
    ElMessage.success(t('dashboard.workItemCreated'))
    taskModalVisible.value = false
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('dashboard.createWorkItemFailed'))
  } finally {
    submittingTask.value = false
  }
}

onMounted(async () => {
  const userStr = localStorage.getItem('user')
  if (userStr) currentUser.value = JSON.parse(userStr)
  updateTime()
  setInterval(updateTime, 60000)
  await Promise.all([
    projectStore.fetchAllProjects(true),
    starredStore.fetchStarredItems({ page: 1, pageSize: 100 }).catch(() => [])
  ])
})

watch(language, updateTime)
</script>

<style scoped>
.plane-dashboard {
  min-height: 100vh;
  background: var(--color-bg);
  color: var(--color-text-primary);
  padding: 32px;
}

.dashboard-header,
.hero-panel,
.search-panel,
.projects-panel {
  max-width: 1180px;
  margin: 0 auto 24px;
}

.dashboard-header,
.section-head,
.hero-panel,
.project-actions,
.dialog-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.eyebrow {
  margin: 0 0 8px;
  color: var(--color-accent);
  font-size: 12px;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-weight: 700;
}

.nexus-feature-header h1 {
  font-size: 28px;
  font-weight: 600;
  margin: 0;
  letter-spacing: -0.02em;
}

.hero-copy h2 {
  font-size: 28px;
  font-weight: 600;
  margin: 0 0 8px;
  letter-spacing: -0.02em;
}

.section-head h2 {
  font-size: 24px;
  font-weight: 600;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 12px;
  letter-spacing: -0.01em;
}

.icon-glass {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border-radius: 12px;
  font-size: 20px;
  background: linear-gradient(135deg, rgba(12, 102, 228, 0.12), rgba(12, 102, 228, 0.04));
  color: var(--color-accent, #0c66e4);
  border: 1px solid rgba(12, 102, 228, 0.15);
  box-shadow: 0 4px 12px rgba(12, 102, 228, 0.08);
  transition: all 0.3s ease;
}
.icon-glass:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(12, 102, 228, 0.12);
}

.icon-glass.purple {
  background: linear-gradient(135deg, rgba(143, 73, 222, 0.12), rgba(143, 73, 222, 0.04));
  color: #8f49de;
  border-color: rgba(143, 73, 222, 0.15);
  box-shadow: 0 4px 12px rgba(143, 73, 222, 0.08);
}

.dashboard-subtitle,
.hero-copy p,
.section-head span,
.project-body p,
.project-meta {
  color: var(--color-text-secondary);
}

.header-actions,
.hero-stats {
  display: flex;
  gap: 12px;
}

.primary-btn,
.secondary-btn {
  border-radius: 2px;
  padding: 10px 14px;
  border: 1px solid transparent;
  cursor: pointer;
  font-weight: 600;
  transition: all 0.2s;
}

.primary-btn {
  background: var(--color-accent);
  color: #ffffff;
}

.secondary-btn {
  background: transparent;
  border-color: var(--color-border);
  color: var(--text-secondary);
}

.small {
  padding: 8px 12px;
  font-size: 13px;
}

.hero-panel {
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: 2px;
  padding: 24px;
  box-shadow: var(--shadow-sm);
}

.hero-copy {
  max-width: 680px;
}

.hero-stats {
  flex-wrap: wrap;
}

.stat-box {
  min-width: 120px;
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 2px;
  padding: 14px;
}

.stat-label {
  display: block;
  color: var(--color-text-muted);
  font-size: 12px;
  margin-bottom: 6px;
}

.search-shell {
  display: flex;
  align-items: center;
  gap: 10px;
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: 2px;
  padding: 0 14px;
}

.search-shell input {
  width: 100%;
  background: transparent;
  border: none;
  color: var(--color-text-primary);
  padding: 14px 0;
  outline: none;
}

.project-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 18px;
}

.project-card {
  position: relative;
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: 2px;
  overflow: hidden;
  transition: transform 0.2s, box-shadow 0.2s;
}
.project-card:hover {
  transform: translateY(-4px);
  box-shadow: var(--shadow-md);
}

.favorite-btn {
  appearance: none;
  -webkit-appearance: none;
  position: absolute;
  top: 12px;
  right: 12px;
  z-index: 1;
  width: 40px;
  height: 40px;
  padding: 0;
  border: 0;
  border-radius: 9px;
  background: rgba(0, 0, 0, 0.45);
  color: #facc15;
  font: inherit;
  cursor: pointer;
  touch-action: manipulation;
  display: grid;
  place-items: center;
}
.favorite-btn:focus-visible {
  outline: 3px solid color-mix(in srgb, var(--color-accent, #0c66e4) 52%, #fff);
  outline-offset: 2px;
}
.favorite-btn:disabled { cursor: wait; }
.favorite-btn i { width: 1em; line-height: 1; text-align: center; }

.project-cover {
  height: 110px;
  display: flex;
  align-items: flex-end;
  padding: 16px;
}

.project-icon {
  width: 40px;
  height: 40px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 2px;
  background: rgba(255, 255, 255, 0.25);
  color: #ffffff;
  font-weight: 700;
  backdrop-filter: blur(4px);
}

.project-body {
  padding: 16px;
}

.project-heading {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 10px;
}

.project-heading h3 {
  font-size: 16px;
  font-weight: 600;
  margin: 0;
}

.project-heading span {
  color: var(--color-text-muted);
  font-size: 12px;
}

.project-body p {
  font-size: 13px;
  font-weight: 400;
  opacity: 0.85;
}

.project-meta {
  display: flex;
  justify-content: space-between;
  font-size: 12px;
}

.project-actions {
  padding: 0 16px 16px;
}

.empty-state {
  min-height: 180px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  background: var(--bg-secondary);
  border: 1px dashed var(--border-color);
  border-radius: 2px;
}

.task-form {
  display: grid;
  gap: 14px;
}

.task-form label {
  display: grid;
  gap: 8px;
  color: var(--color-text-secondary);
  font-size: 13px;
  font-weight: 600;
}

.task-form input,
.task-form select,
.task-form textarea {
  width: 100%;
  border: 1px solid var(--border-color);
  border-radius: 4px;
  padding: 10px 12px;
  background: var(--bg-tertiary);
  color: var(--text-primary);
  font: inherit;
  outline: none;
  transition: all 0.2s;
}

.task-form input:focus,
.task-form select:focus,
.task-form textarea:focus {
  border-color: var(--accent-color);
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.1);
}

@media (max-width: 768px) {
  .plane-dashboard {
    padding: 20px;
  }

  .dashboard-header,
  .hero-panel {
    flex-direction: column;
    align-items: flex-start;
  }
}

/* SprintA dashboard polish */
@keyframes dashboard-rise-in {
  from {
    opacity: 0;
    transform: translateY(16px) scale(0.99);
  }
  to {
    opacity: 1;
    transform: translateY(0) scale(1);
  }
}



.plane-dashboard {
  background: var(--color-bg) !important;
}

.nexus-feature-header,
.hero-panel,
.search-panel,
.projects-panel {
  animation: dashboard-rise-in 520ms cubic-bezier(0.2, 0.8, 0.2, 1) both;
}

.hero-panel {
  position: relative;
  overflow: hidden;
  border-radius: 10px !important;
  border-color: color-mix(in srgb, var(--color-accent) 22%, var(--color-border)) !important;
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--color-surface) 92%, transparent), color-mix(in srgb, var(--color-surface-hover) 72%, transparent)),
    var(--color-surface) !important;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05) !important;
}



.hero-panel > * {
  position: relative;
  z-index: 1;
}

.stat-box,
.project-card,
.search-shell,
.empty-state {
  border-radius: 8px !important;
  border-color: color-mix(in srgb, var(--color-border) 82%, transparent) !important;
  background: color-mix(in srgb, var(--color-surface) 88%, transparent) !important;
}

.project-card {
  box-shadow: 0 12px 30px rgba(15, 23, 42, 0.07);
}

.project-card:hover {
  border-color: color-mix(in srgb, var(--color-accent) 35%, var(--color-border)) !important;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1) !important;
}

[data-theme='light'] .hero-copy h2,
[data-theme='light'] .section-head h2,
[data-theme='light'] .project-heading h3,
[data-theme='light'] .stat-box strong {
  color: #0f172a !important;
}

[data-theme='dark'] .hero-copy h2,
[data-theme='dark'] .section-head h2,
[data-theme='dark'] .project-heading h3,
[data-theme='dark'] .stat-box strong {
  color: #f8fafc !important;
}

@media (prefers-reduced-motion: reduce) {
  .nexus-feature-header,
  .hero-panel,
  .search-panel,
  .projects-panel {
    animation: none !important;
  }
}
</style>
