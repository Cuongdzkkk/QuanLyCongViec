<template>
  <AdminLayout>
    <div class="manage-spaces-page" style="padding: 0;">
      <!-- Header -->
      <header class="spaces-header" style="margin-bottom: 16px;">
        <div class="sh-left">
          <i class="fa-solid fa-trash-can"></i>
          <h1>Trash</h1>
        </div>
      </header>

      <!-- Jira-style Filter Bar -->
      <div class="jira-filter-bar" style="display: flex; gap: 12px; margin-bottom: 20px;">
        <div class="search-box" style="position: relative; display: flex; align-items: center;">
          <i class="fa-solid fa-magnifying-glass" style="position: absolute; left: 12px; color: var(--color-text-muted); font-size: 13px;"></i>
          <input
            type="text"
            placeholder="Search spaces"
            v-model="searchQuery"
            style="background: transparent; border: 1px solid var(--color-border); color: var(--color-text-primary); padding: 8px 12px 8px 32px; font-size: 13.5px; outline: none; width: 220px; border-radius: 4px;"
          />
        </div>
        <div class="filter-dropdown">
          <select style="background: var(--color-surface); border: 1px solid var(--color-border); color: var(--color-text-primary); padding: 8px 16px 8px 12px; font-size: 13.5px; outline: none; border-radius: 4px; cursor: pointer; min-width: 140px;">
            <option value="">Filter by app</option>
            <option value="SprintA">SprintA</option>
          </select>
        </div>
      </div>

      <section class="projects-scroll-panel">
        <div v-if="loading" class="loading-state" style="text-align: center; padding: 40px; color: var(--color-text-muted);">
           <i class="fa-solid fa-spinner fa-spin"></i> Loading trash...
        </div>
        <div v-else class="spaces-table-container">
          <table class="jira-table spaces-table" style="width: 100%; border-collapse: collapse; text-align: left;">
            <thead>
              <tr style="border-bottom: 2px solid var(--color-border); color: var(--color-text-muted); font-size: 12px; background: var(--color-bg);">
                <th style="padding: 12px 16px; font-weight: 600;">Name</th>
                <th style="padding: 12px 16px; font-weight: 600;">Key</th>
                <th style="padding: 12px 16px; font-weight: 600;">Type</th>
                <th style="padding: 12px 16px; font-weight: 600;">Lead</th>
                <th style="padding: 12px 16px; font-weight: 600;">Marked for deletion</th>
                <th style="padding: 12px 16px; font-weight: 600; width: 220px; text-align: right;">Deletion schedule</th>
              </tr>
            </thead>
            <tbody>
              <!-- Empty state row -->
              <tr v-if="filteredSpaces.length === 0">
                <td colspan="6" style="padding: 60px 20px; text-align: center;">
                  <div class="empty-icon-wrap" style="width: 80px; height: 80px; background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 16px; display: flex; align-items: center; justify-content: center; margin: 0 auto 24px; box-shadow: 0 10px 30px rgba(0,0,0,0.15);">
                    <i class="fa-solid fa-trash-can empty-icon" style="margin-bottom: 0; font-size: 36px; color: var(--color-text-muted);"></i>
                  </div>
                  <h3 class="empty-title" style="margin: 0 0 8px 0; font-size: 16px; font-weight: 600; color: var(--color-text-primary);">No projects in trash</h3>
                  <p style="margin: 0; font-size: 14px; color: var(--color-text-muted);">Deleted projects will appear here for temporary storage before permanent deletion.</p>
                </td>
              </tr>
              <!-- Data rows -->
              <tr v-else v-for="space in filteredSpaces" :key="space.id" style="border-bottom: 1px solid var(--color-border); transition: background 0.2s;" class="table-row-hover">
                <td style="padding: 12px 16px;">
                  <div style="display: flex; align-items: center; gap: 12px;">
                    <ProjectAvatar :icon="space.icon" :background="space.cover" size="xs" />
                    <span style="font-weight: 500; color: var(--color-text-primary);">{{ space.name }}</span>
                  </div>
                </td>
                <td style="padding: 12px 16px; font-size: 13px;">{{ space.key }}</td>
                <td style="padding: 12px 16px; font-size: 13px; color: var(--color-text-muted);">
                  {{ space.networkType === 'Private' ? 'Team-managed software (Private)' : 'Team-managed software' }}
                </td>
                <td style="padding: 12px 16px;">
                  <div style="display: flex; align-items: center; gap: 8px;">
                    <div style="width: 24px; height: 24px; border-radius: 50%; background: #ef4444; color: white; display: flex; align-items: center; justify-content: center; font-size: 11px; font-weight: 600;">
                      {{ space.leadName?.charAt(0).toUpperCase() || 'T' }}
                    </div>
                    <span style="font-size: 13px;">{{ space.leadName }}</span>
                  </div>
                </td>
                <td style="padding: 12px 16px; font-size: 13px; color: var(--color-text-muted);">
                  {{ new Date(space.originalRow?.updatedAt || Date.now()).toLocaleDateString() }}
                </td>
                <td style="padding: 12px 16px; text-align: right;">
                  <div style="display: flex; justify-content: flex-end; gap: 8px;">
                    <button class="plane-btn-secondary outline-btn action-btn-text" type="button" @click="restoreProject(space)">
                      <i class="fa-solid fa-rotate-left" style="margin-right: 4px;"></i> Restore
                    </button>
                    <button class="plane-btn-danger outline-btn action-btn-text" type="button" @click="confirmPermanentDelete(space)">
                      <i class="fa-solid fa-trash-can" style="margin-right: 4px;"></i> Permanent delete
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </div>
  </AdminLayout>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import AdminLayout from '@/components/layout/AdminLayout.vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useProjectStore } from '@/store/useProjectStore'
import { openNamedAppWindow, PROJECT_ADMIN_WINDOW_NAME } from '@/utils/windowTabs'
import ProjectAvatar from '@/components/project/ProjectAvatar.vue'

const router = useRouter()
const handleSwitchSettings = (path) => {
  router.push(path)
}

const projectStore = useProjectStore()
const loading = ref(false)
const spaces = ref([])
const searchQuery = ref('')

const fetchTrashSpaces = async () => {
  loading.value = true
  try {
    const response = await axiosClient.get('/projects/deleted')
    const data = response.data.data || response.data || []

    spaces.value = data.map(p => ({
      id: p.id,
      name: p.name,
      key: p.key || p.identifier,
      leadName: p.leadName || 'Admin',
      icon: p.icon || p.Icon,
      cover: p.cover || p.Cover,
      networkType: p.networkType || 'Public',
      originalRow: p
    }))
  } catch (error) {
    console.error('Fetch deleted spaces error:', error)
    ElMessage.error('Failed to load trash list')
  } finally {
    loading.value = false
  }
}

const restoreProject = async (space) => {
  try {
    await ElMessageBox.confirm(`Are you sure you want to restore project "${space.name}"?`, 'Restore Project', { type: 'info' })
    await axiosClient.put(`/projects/${space.id}/restore-deleted`)
    ElMessage.success('Project restored successfully')
    fetchTrashSpaces()
    projectStore.fetchAllProjects(true).catch(() => {})
  } catch (err) {
    if (err !== 'cancel') ElMessage.error('Failed to restore project')
  }
}

const confirmPermanentDelete = async (space) => {
  try {
    await ElMessageBox.prompt(
      `Warning: Permanent deletion is irreversible! All tasks, members, and data associated with "${space.name}" will be deleted. \n\nPlease type the project name "${space.name}" to confirm:`,
      'Permanent Delete Project',
      {
        confirmButtonText: 'Delete Permanently',
        confirmButtonClass: 'el-button--danger',
        cancelButtonText: 'Cancel',
        inputValidator: (val) => {
          if (val !== space.name) {
            return 'Project name does not match!'
          }
          return true
        }
      }
    )

    await axiosClient.delete(`/projects/${space.id}/permanent`)
    ElMessage.success('Project permanently deleted')
    fetchTrashSpaces()
  } catch (err) {
    if (err !== 'cancel') {
      console.error(err)
      ElMessage.error('Failed to permanently delete project')
    }
  }
}

const filteredSpaces = computed(() => {
  return spaces.value.filter(s => {
    return !searchQuery.value || s.name.toLowerCase().includes(searchQuery.value.toLowerCase()) || s.key.toLowerCase().includes(searchQuery.value.toLowerCase())
  })
})

onMounted(() => {
  fetchTrashSpaces()
})
</script>

<style scoped>
.manage-spaces-layout {
  display: flex;
  height: 100vh;
  width: 100%;
  background: var(--color-bg);
  overflow: hidden;
}

/* Jira Settings Sidebar */
.jira-admin-sidebar {
  width: 240px;
  border-right: 1px solid var(--color-border);
  background: var(--color-bg);
  padding: 24px 16px;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  font-family: 'Inter', sans-serif;
}

.sidebar-header {
  margin-bottom: 24px;
}

.back-link {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--color-text-muted);
  text-decoration: none;
  font-size: 13px;
  font-weight: 500;
  transition: color 0.2s;
}
.back-link:hover {
  color: var(--color-text-primary);
}

.sidebar-section {
  margin-bottom: 20px;
}

.section-title {
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--color-text-muted);
  margin-bottom: 8px;
  letter-spacing: 0.5px;
}

.section-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border-radius: 6px;
  font-size: 13px;
  color: var(--color-text-primary);
  font-weight: 600;
}
.section-item.active {
  background: #18181b;
  border: 1px solid var(--color-border);
}

.sidebar-menu {
  list-style: none;
  padding: 0 0 0 12px;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
  border-left: 1px solid var(--color-border);
}

.menu-item {
  display: block;
  padding: 6px 12px;
  border-radius: 4px;
  font-size: 13px;
  color: var(--color-text-secondary);
  text-decoration: none;
  transition: all 0.2s;
}
.menu-item:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}
.menu-item.active {
  background: color-mix(in srgb, var(--color-accent) 10%, transparent);
  color: var(--color-accent);
  font-weight: 600;
}

/* Right Content */
.spaces-main-content {
  flex: 1;
  overflow-y: auto;
  min-width: 0;
}

.manage-spaces-page {
  padding: 40px;
  width: 100%;
  max-width: 1200px;
  margin: 0 auto;
  color: var(--color-text-primary);
  font-family: 'Inter', -apple-system, sans-serif;
  min-height: 0;
}

/* Header */
.spaces-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 24px;
  margin-bottom: 24px;
}

.sh-left {
  display: flex;
  align-items: center;
  gap: 12px;
}
.sh-left i {
  color: var(--color-text-muted);
  font-size: 18px;
}
.sh-left h1 {
  font-size: 16px;
  font-weight: 500;
  margin: 0;
  color: var(--color-text-primary);
}

.sh-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.search-box {
  position: relative;
  display: flex;
  align-items: center;
}
.search-box i {
  position: absolute;
  left: 12px;
  color: var(--color-text-muted);
  font-size: 13px;
}
.search-box input {
  background: transparent;
  border: 1px solid var(--color-border);
  color: var(--color-text-primary);
  padding: 6px 12px 6px 32px;
  font-size: 13px;
  outline: none;
  width: 200px;
  border-radius: 6px;
  transition: width 0.2s;
}
.search-box input:focus { width: 280px; border-color: var(--color-accent); }
.search-box input::placeholder { color: #52525B; }

.projects-scroll-panel {
  min-height: 0;
  padding-bottom: 24px;
}

.spaces-table-container {
  border-radius: 8px;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  overflow: hidden;
}

.table-row-hover:hover {
  background: #18181b;
}

.plane-btn-secondary {
  background: transparent;
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  padding: 6px 12px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 500;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  transition: all 0.2s;
}
.plane-btn-secondary:hover {
  background: var(--color-border);
  color: var(--color-text-primary);
}

.plane-btn-danger {
  background: transparent;
  border: 1px solid #ef4444;
  color: #ef4444;
  padding: 6px 12px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 500;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  transition: all 0.2s;
}
.plane-btn-danger:hover {
  background: #ef4444;
  color: white;
}

.loading-state, .empty-state { text-align: center; margin-top: 60px; color: var(--color-text-muted); }
.empty-icon { font-size: 48px; color: #3F3F46; margin-bottom: 16px; }
.empty-state p { margin-bottom: 24px; }

.switch-trigger-btn {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  cursor: pointer;
  background: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
  color: var(--color-text-primary) !important;
  padding: 8px 12px;
  border-radius: 4px;
  font-size: 13px;
  font-weight: 600;
  transition: all 0.2s;
}

.switch-trigger-btn:hover {
  background: var(--color-surface-hover) !important;
}

.switch-trigger-btn i {
  color: var(--color-text-secondary) !important;
}

.switch-trigger-btn span {
  color: var(--color-text-primary) !important;
}
</style>

<style>
.el-popper.jira-switch-dropdown-popper {
  background: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
  padding: 4px 0 !important;
  z-index: 100002 !important;
  box-shadow: 0 10px 30px rgba(0,0,0,0.2) !important;
}

.jira-switch-dropdown-menu .el-dropdown-menu__item {
  color: var(--color-text-primary) !important;
  display: flex !important;
  align-items: center !important;
  gap: 8px !important;
  font-size: 13px !important;
  padding: 8px 16px !important;
}

.jira-switch-dropdown-menu .el-dropdown-menu__item:hover {
  background-color: var(--color-surface-hover) !important;
  color: var(--color-text-primary) !important;
}

.jira-switch-dropdown-menu .el-dropdown-menu__item.is-disabled {
  color: var(--color-accent) !important;
  font-weight: 600 !important;
  background: transparent !important;
  cursor: default !important;
}
</style>
