<template>
  <div class="jd-content">
    <div class="jd-header">
      <h3>Starred</h3>
    </div>

    <div class="jd-body">
      <div v-if="starredStore.loading" class="jd-empty-starred">Loading starred items...</div>
      <div v-else-if="starredStore.error" class="jd-empty-starred">
        <p>{{ starredStore.error }}</p>
        <button type="button" @click="loadStarredItems">Retry</button>
      </div>
      <div v-else-if="starredProjects.length === 0 && starredTasks.length === 0" class="jd-empty-starred">
        <i class="fa-regular fa-star" style="font-size: 48px; color: var(--color-text-muted); margin-bottom: 16px;"></i>
        <h4>You haven't starred anything yet</h4>
        <p>Đánh dấu sao các mục quan trọng để truy cập nhanh tại đây.</p>
      </div>

      <div v-else class="jd-list">
        <!-- Section: Starred Spaces (Projects) -->
        <template v-if="starredProjects.length > 0">
          <div class="jd-section-label">Spaces</div>
          <div
            v-for="project in starredProjects"
            :key="`proj-${project.itemId}`"
            class="jd-item"
            @click="goToProject(project)"
          >
            <div class="jd-item-icon">
              <span
                class="proj-icon"
                :style="{ background: projectColor(project) }"
              >{{ project.icon || project.name?.charAt(0)?.toUpperCase() || 'P' }}</span>
            </div>
            <div class="jd-item-content">
              <div class="jd-item-title">{{ project.name || 'Space' }}</div>
              <div class="jd-item-subtitle">Space</div>
            </div>
            <button
              class="jd-item-action"
              type="button"
              :disabled="starredStore.isPending(project.itemType, project.itemId)"
              @click.stop="unstarItem(project)"
              title="Remove from starred"
            >
              <i class="fa-solid fa-star text-yellow-400"></i>
            </button>
          </div>
        </template>

        <!-- Section: Starred Tasks -->
        <template v-if="starredTasks.length > 0">
          <div class="jd-section-label">Work items</div>
          <div
            v-for="item in starredTasks"
            :key="`task-${item.itemId}`"
            class="jd-item"
            @click="goToTask(item)"
          >
            <div class="jd-item-icon">
              <i class="fa-solid fa-square-check text-blue-500"></i>
            </div>
            <div class="jd-item-content">
              <div class="jd-item-title">{{ item.title || 'Task' }}</div>
              <div class="jd-item-subtitle">Task • {{ item.sequenceId || item.id?.substring(0, 8).toUpperCase() }} • {{ item.projectName || 'Project' }}</div>
            </div>
            <button
              class="jd-item-action"
              type="button"
              :disabled="starredStore.isPending(item.itemType, item.itemId)"
              @click.stop="unstarItem(item)"
              title="Remove from starred"
            >
              <i class="fa-solid fa-star text-yellow-400"></i>
            </button>
          </div>
        </template>
      </div>
    </div>

    <div class="jd-footer" v-if="starredProjects.length > 0 || starredTasks.length > 0">
      <button @click="viewAllStarred">View all starred items</button>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useStarredStore } from '@/store/useStarredStore'
import { STARRED_ENTITY_TYPES } from '@/api/starredRecentApi'

const emit = defineEmits(['close'])
const router = useRouter()
const starredStore = useStarredStore()

const starredProjects = computed(() => starredStore.starredItems
  .filter(item => item.itemType === STARRED_ENTITY_TYPES.PROJECT)
  .map(item => ({ ...item, id: item.itemId, name: item.itemName || item.title })))
const starredTasks = computed(() => starredStore.starredItems
  .filter(item => item.itemType === STARRED_ENTITY_TYPES.WORK_TASK)
  .map(item => ({
    ...item,
    id: item.itemId,
    projectName: item.subtitle,
    sequenceId: item.itemId?.substring(0, 8).toUpperCase()
  })))

const loadStarredItems = () => starredStore.fetchStarredItems({ page: 1, pageSize: 20 }).catch(() => {})
defineExpose({ loadStarredItems })

const unstarItem = (item) => starredStore.setStarred(item.itemType, item.itemId, false).catch(() => {})

const goToProject = (project) => {
  emit('close')
  router.push(project.url || `/home/projects/${project.itemId}`)
}

const goToTask = (item) => {
  emit('close')
  if (item.url) router.push(item.url)
}

const viewAllStarred = () => {
  emit('close')
  router.push('/home/starred')
}

const projectColor = (project) => {
  if (project.cover && project.cover.startsWith('#')) return project.cover
  const colors = ['#579dff', '#c97cf4', '#00b8d9', '#22a06b', '#f5cd47']
  return colors[(project.name?.length || 0) % colors.length]
}
</script>

<style scoped>
.jd-content {
  display: flex;
  flex-direction: column;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 96%, var(--color-accent) 4%), var(--color-surface));
  color: var(--color-text-primary, #172b4d);
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
  border-radius: 16px;
  overflow: hidden;
  min-width: 340px;
}

.jd-header {
  padding: 16px 16px 8px;
}
.jd-header h3 {
  margin: 0;
  font-size: 12px;
  text-transform: uppercase;
  color: var(--color-text-muted, #6b778c);
  font-weight: 850;
  letter-spacing: 0.08em;
}

.jd-body {
  flex: 1;
  overflow-y: auto;
  max-height: 360px;
  padding: 0 8px 8px;
}

.jd-empty-starred {
  text-align: center;
  padding: 24px 16px;
}
.jd-empty-starred img {
  width: 100px;
  margin: 0 auto 16px;
  opacity: 0.7;
}
.jd-empty-starred h4 {
  font-size: 14px;
  font-weight: 600;
  margin: 0 0 6px 0;
  color: var(--color-text-primary, #172b4d);
}
.jd-empty-starred p {
  font-size: 12px;
  color: var(--color-text-muted, #6b778c);
  margin: 0;
  line-height: 1.4;
}

.jd-section-label {
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--color-text-muted, #6b778c);
  letter-spacing: 0.6px;
  padding: 10px 16px 4px;
}

.jd-list {
  padding-bottom: 8px;
}

.jd-item {
  display: flex;
  align-items: center;
  padding: 10px 10px;
  cursor: pointer;
  border-radius: 12px;
  transition: background 0.16s ease, transform 0.16s ease;
}
.jd-item:hover {
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface-hover));
  transform: translateX(2px);
}

.jd-item-icon {
  margin-right: 12px;
  font-size: 16px;
  display: flex;
  align-items: center;
  flex-shrink: 0;
}

.proj-icon {
  width: 24px;
  height: 24px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 700;
  color: #fff;
}

.jd-item-content {
  flex: 1;
  min-width: 0;
}

.jd-item-title {
  font-size: 13px;
  font-weight: 800;
  color: var(--color-text-primary, #172b4d);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.jd-item-subtitle {
  font-size: 11px;
  color: var(--color-text-muted, #6b778c);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.jd-item-action {
  margin-left: 12px;
  font-size: 14px;
  padding: 4px;
  border-radius: 3px;
  display: flex;
  align-items: center;
  flex-shrink: 0;
  opacity: 0;
  transition: opacity 0.15s;
}
.jd-item:hover .jd-item-action {
  opacity: 1;
}
.jd-item-action:hover {
  background: rgba(9, 30, 66, 0.08);
}

.jd-footer {
  padding: 10px 14px;
  border-top: 1px solid var(--color-border, #ebecf0);
}

.jd-footer button {
  width: 100%;
  text-align: left;
  background: transparent;
  border: none;
  color: var(--color-accent, #0c66e4);
  font-size: 13px;
  font-weight: 800;
  padding: 8px;
  border-radius: 10px;
  cursor: pointer;
}
.jd-footer button:hover {
  background: var(--color-surface-hover, #f4f5f7);
  text-decoration: none;
}

[data-theme='dark'] .jd-content {
  background:
    linear-gradient(180deg, rgba(30, 41, 59, 0.96), rgba(15, 23, 42, 0.98)),
    #0f172a;
}
</style>
