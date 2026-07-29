<template>
  <div class="quick-panel">
    <header class="panel-header">
      <h3>Starred</h3>
    </header>

    <div class="panel-body">
      <div v-if="starredStore.loading" class="panel-state" role="status" aria-live="polite">
        <span class="state-spinner" aria-hidden="true"></span>
        <h4>Loading starred items</h4>
        <p>Keeping your shortcuts up to date.</p>
      </div>

      <div v-else-if="starredStore.error" class="panel-state state-error" role="alert">
        <i class="fa-solid fa-triangle-exclamation state-icon" aria-hidden="true"></i>
        <h4>Starred items are unavailable</h4>
        <p>{{ starredStore.error }}</p>
        <button class="retry-button" type="button" @click="loadStarredItems">Try again</button>
      </div>

      <div v-else-if="starredProjects.length === 0 && starredTasks.length === 0" class="panel-state">
        <i class="fa-regular fa-star state-icon" aria-hidden="true"></i>
        <h4>You haven't starred anything yet</h4>
        <p>Star important items to keep them within easy reach.</p>
      </div>

      <div v-else class="panel-list">
        <section v-if="starredProjects.length > 0" aria-labelledby="starred-spaces-label">
          <h4 id="starred-spaces-label" class="section-label">Spaces</h4>
          <div
            v-for="project in starredProjects"
            :key="`project-${project.itemId}`"
            class="panel-item"
            role="link"
            tabindex="0"
            @click="goToProject(project)"
            @keydown.enter.prevent="goToProject(project)"
            @keydown.space.prevent="goToProject(project)"
          >
            <span class="project-icon" :style="{ background: projectColor(project) }">
              {{ project.icon || project.name?.charAt(0)?.toUpperCase() || 'P' }}
            </span>
            <span class="item-copy">
              <strong>{{ project.name || 'Space' }}</strong>
              <small>Space</small>
            </span>
            <button
              class="star-action"
              type="button"
              :disabled="starredStore.isPending(project.itemType, project.itemId)"
              :aria-busy="starredStore.isPending(project.itemType, project.itemId)"
              aria-label="Remove space from starred"
              title="Remove from starred"
              @click.stop="unstarItem(project)"
            >
              <i :class="pendingIcon(project)" aria-hidden="true"></i>
            </button>
          </div>
        </section>

        <section v-if="starredTasks.length > 0" aria-labelledby="starred-tasks-label">
          <h4 id="starred-tasks-label" class="section-label">Work items</h4>
          <div
            v-for="item in starredTasks"
            :key="`task-${item.itemId}`"
            class="panel-item"
            role="link"
            tabindex="0"
            @click="goToTask(item)"
            @keydown.enter.prevent="goToTask(item)"
            @keydown.space.prevent="goToTask(item)"
          >
            <span class="item-type-icon"><i class="fa-solid fa-square-check" aria-hidden="true"></i></span>
            <span class="item-copy">
              <strong>{{ item.title || 'Task' }}</strong>
              <small>Task · {{ item.sequenceId || '—' }} · {{ item.projectName || 'Project' }}</small>
            </span>
            <button
              class="star-action"
              type="button"
              :disabled="starredStore.isPending(item.itemType, item.itemId)"
              :aria-busy="starredStore.isPending(item.itemType, item.itemId)"
              aria-label="Remove work item from starred"
              title="Remove from starred"
              @click.stop="unstarItem(item)"
            >
              <i :class="pendingIcon(item)" aria-hidden="true"></i>
            </button>
          </div>
        </section>
      </div>
    </div>

    <footer v-if="hasItems" class="panel-footer">
      <button type="button" @click="viewAllStarred">View all starred items</button>
    </footer>
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
    sequenceId: item.sequenceId || item.itemId?.substring(0, 8).toUpperCase()
  })))

const hasItems = computed(() => starredProjects.value.length > 0 || starredTasks.value.length > 0)
const loadStarredItems = () => starredStore.fetchStarredItems({ page: 1, pageSize: 20 }).catch(() => null)
defineExpose({ loadStarredItems })

const unstarItem = async (item) => {
  try {
    await starredStore.setStarred(item.itemType, item.itemId, false)
  } catch {
    // The store exposes the API error in the shared error state.
  }
}

const pendingIcon = (item) => starredStore.isPending(item.itemType, item.itemId)
  ? 'fa-solid fa-spinner fa-spin'
  : 'fa-solid fa-star'

const goToProject = (project) => {
  emit('close')
  router.push(project.url || `/home/projects/${project.itemId}`)
}

const goToTask = (item) => {
  if (!item.url) return
  emit('close')
  router.push(item.url)
}

const viewAllStarred = () => {
  emit('close')
  router.push('/home/starred')
}

const projectColor = (project) => {
  if (project.cover?.startsWith('#')) return project.cover
  const colors = ['#0c66e4', '#7c3aed', '#0891b2', '#0f9d72', '#d97706']
  return colors[(project.name?.length || 0) % colors.length]
}
</script>

<style scoped>
.quick-panel {
  width: min(340px, calc(100vw - 24px));
  min-width: 0;
  min-height: 286px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  box-sizing: border-box;
  border-radius: 14px;
  background: linear-gradient(180deg, color-mix(in srgb, var(--color-surface, #fff) 96%, var(--color-accent, #0c66e4) 4%), var(--color-surface, #fff));
  color: var(--color-text-primary, #172b4d);
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
}

.panel-header { padding: 16px 16px 8px; }
.panel-header h3,
.section-label {
  margin: 0;
  color: var(--color-text-muted, #6b778c);
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}
.section-label { padding: 10px 12px 5px; font-size: 10px; }

.panel-body {
  min-height: 216px;
  max-height: 360px;
  flex: 1;
  overflow-y: auto;
  padding: 0 8px 8px;
}

.panel-state {
  min-height: 216px;
  padding: 24px 16px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  box-sizing: border-box;
  text-align: center;
}
.panel-state h4 { margin: 0 0 6px; font-size: 14px; font-weight: 800; }
.panel-state p { max-width: 250px; margin: 0; color: var(--color-text-muted, #6b778c); font-size: 12px; line-height: 1.5; }
.state-icon { width: 40px; height: 40px; margin-bottom: 14px; display: grid; place-items: center; color: var(--color-text-muted, #6b778c); font-size: 26px; }
.state-error .state-icon { color: #e34935; }
.state-spinner {
  width: 24px;
  height: 24px;
  margin-bottom: 16px;
  border: 3px solid color-mix(in srgb, var(--color-accent, #0c66e4) 18%, transparent);
  border-top-color: var(--color-accent, #0c66e4);
  border-radius: 50%;
  animation: panel-spin 0.8s linear infinite;
}
@keyframes panel-spin { to { transform: rotate(360deg); } }

.panel-list { padding-bottom: 4px; }
.panel-item {
  min-height: 54px;
  padding: 7px 6px 7px 10px;
  display: flex;
  align-items: center;
  box-sizing: border-box;
  border-radius: 11px;
  cursor: pointer;
  transition: background 0.16s ease, transform 0.16s ease;
}
.panel-item:hover { background: color-mix(in srgb, var(--color-accent, #0c66e4) 9%, var(--color-surface-hover, #f4f5f7)); transform: translateX(2px); }
.panel-item:focus-visible { outline: 3px solid color-mix(in srgb, var(--color-accent, #0c66e4) 38%, transparent); outline-offset: -2px; }
.project-icon,
.item-type-icon {
  width: 28px;
  height: 28px;
  margin-right: 10px;
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  border-radius: 8px;
  color: #fff;
  font-size: 11px;
  font-weight: 800;
}
.item-type-icon { background: color-mix(in srgb, var(--color-accent, #0c66e4) 14%, transparent); color: var(--color-accent, #0c66e4); font-size: 14px; }
.item-copy { min-width: 0; flex: 1; display: flex; flex-direction: column; }
.item-copy strong,
.item-copy small { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.item-copy strong { font-size: 13px; font-weight: 800; }
.item-copy small { color: var(--color-text-muted, #6b778c); font-size: 11px; }

.star-action,
.panel-footer button,
.retry-button {
  appearance: none;
  -webkit-appearance: none;
  border: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  padding: 0;
  cursor: pointer;
  touch-action: manipulation;
}
.star-action {
  width: 40px;
  height: 40px;
  margin-left: 6px;
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  border-radius: 10px;
  color: #e3a008;
}
.star-action:hover { background: color-mix(in srgb, #e3a008 14%, transparent); }
.star-action:disabled { cursor: wait; }
.star-action i { width: 1em; line-height: 1; text-align: center; }

.panel-footer { padding: 9px 12px; border-top: 1px solid var(--color-border, #ebecf0); }
.panel-footer button { width: 100%; padding: 9px 10px; border-radius: 9px; color: var(--color-accent, #0c66e4); text-align: left; font-size: 13px; font-weight: 800; }
.panel-footer button:hover { background: var(--color-surface-hover, #f4f5f7); }
.retry-button { margin-top: 14px; padding: 8px 12px; border: 1px solid var(--color-border, #dfe1e6); border-radius: 9px; background: var(--color-surface, #fff); font-size: 12px; font-weight: 800; }
.retry-button:hover { background: var(--color-surface-hover, #f4f5f7); }

.star-action:focus-visible,
.panel-footer button:focus-visible,
.retry-button:focus-visible {
  outline: 3px solid color-mix(in srgb, var(--color-accent, #0c66e4) 42%, transparent);
  outline-offset: 2px;
}

[data-theme='dark'] .quick-panel {
  background: linear-gradient(180deg, color-mix(in srgb, var(--color-surface, #162033) 94%, #2563eb 6%), var(--color-surface, #162033));
}

@media (hover: none) {
  .panel-item:hover { transform: none; }
}

@media (prefers-reduced-motion: reduce) {
  .panel-item { transition: none; }
}
</style>
