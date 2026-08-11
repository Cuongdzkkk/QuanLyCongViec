<template>
  <div>
    <div class="settings-wrapper" v-loading="loading">
      <header class="settings-header">
        <div class="sh-left">
           <span class="breadcrumb">
             <i class="fa-solid fa-briefcase"></i> 
             {{ project?.name || 'Project' }} 
             <i class="fa-solid fa-chevron-right separator"></i> 
             Settings
           </span>
        </div>
        <div class="sh-right">
           <el-button type="primary" @click="handleSave" :loading="saving">Save changes</el-button>
        </div>
      </header>

      <div class="settings-content">
        <div class="settings-sidebar">
           <ul class="settings-nav">
             <li :class="{ active: activeSection === 'general' }" @click="activeSection = 'general'">
               <i class="fa-solid fa-gear"></i> General
             </li>
             <li :class="{ active: activeSection === 'members' }" @click="activeSection = 'members'">
               <i class="fa-solid fa-users"></i> Members
             </li>
             <li :class="{ active: activeSection === 'danger' }" @click="activeSection = 'danger'" class="danger-nav">
               <i class="fa-solid fa-triangle-exclamation"></i> Danger Zone
             </li>
           </ul>
        </div>

        <div class="settings-main">
          <!-- General Section -->
          <section v-if="activeSection === 'general'" class="settings-section">
            <h2 class="section-title">General Settings</h2>
            
            <div class="form-group">
              <label>Project Name</label>
              <input type="text" v-model="form.name" class="glass-input" />
            </div>

            <div class="form-group">
              <label>Project Identifier (Key)</label>
              <input type="text" v-model="form.identifier" class="glass-input" disabled title="Identifier cannot be changed yet" />
              <small class="form-hint">Unique shorthand for this project.</small>
            </div>

            <div class="form-group">
              <label>Description</label>
              <textarea v-model="form.description" class="glass-input" rows="4"></textarea>
            </div>

            <div class="form-grid">
               <div class="form-group">
                 <label>Network Type</label>
                 <el-select v-model="form.networkType" class="glass-select w-full">
                   <el-option label="Public" value="Public" />
                   <el-option label="Private" value="Private" />
                   <el-option label="Internal" value="Internal" />
                 </el-select>
               </div>
               <div class="form-group">
                 <label>Project Icon</label>
                 <div class="icon-selector">
                   <span class="current-icon">{{ form.icon || '📁' }}</span>
                   <button @click="showIconPicker = true" class="neutral-btn">Change</button>
                 </div>
               </div>
            </div>
          </section>

          <!-- Members Section -->
          <section v-if="activeSection === 'members'" class="settings-section">
            <h2 class="section-title">Project Members</h2>
            <div class="members-list">
              <div v-for="m in members" :key="m.id" class="member-item">
                <div class="member-avatar">{{ m.fullName?.charAt(0) || 'U' }}</div>
                <div class="member-info">
                  <div class="member-name">{{ m.fullName }}</div>
                  <div class="member-role">{{ m.projectRole }}</div>
                </div>
                <div class="member-actions">
                  <el-button link type="danger" @click="removeMember(m)">Remove</el-button>
                </div>
              </div>
            </div>
            <el-button class="mt-4" @click="showAddMember = true"><i class="fa-solid fa-plus mr-2"></i> Add member</el-button>
          </section>

          <!-- Danger Zone -->
          <section v-if="activeSection === 'danger'" class="settings-section danger-zone">
            <h2 class="section-title text-red-500">Danger Zone</h2>
            
            <div class="danger-card">
              <div class="dc-info">
                <h4>Archive this project</h4>
                <p>Archiving the project will make it read-only and move it to the archives.</p>
              </div>
              <button class="danger-btn-outline" @click="handleArchive" v-if="!project?.isArchived">Archive</button>
              <button class="neutral-btn" @click="handleRestore" v-else>Restore Project</button>
            </div>

            <div class="danger-card mt-4">
              <div class="dc-info">
                <h4>Delete this project</h4>
                <p>Once you delete a project, there is no going back. Please be certain.</p>
              </div>
              <button class="danger-btn" @click="handleDelete">Delete Project</button>
            </div>
          </section>
        </div>
      </div>
    </div>

    <!-- Icon Picker Simple -->
    <el-dialog
      v-model="showIconPicker"
      width="400px"
      custom-class="glass-dialog"
      class="sa-data-dialog sa-modal--sm"
      :show-close="false"
    >
      <template #header>
        <DataModalHeader
          icon="bi bi-emoji-smile"
          title="Pick an Icon"
          description="Choose a project icon for easier scanning"
          @close="showIconPicker = false"
        />
      </template>
      <DataModalSection icon="bi bi-grid-3x3-gap" title="Icon options">
        <div class="emoji-grid">
          <span v-for="e in emojiList" :key="e" @click="form.icon = e; showIconPicker = false" class="emoji-opt">{{ e }}</span>
        </div>
      </DataModalSection>
    </el-dialog>

  </div>
</template>

<script setup>
import { ref, onMounted, reactive } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import axiosClient from '@/api/axiosClient'
import { ElMessage, ElMessageBox } from 'element-plus'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'

const route = useRoute()
const router = useRouter()
const projectId = route.params.id

const project = ref(null)
const members = ref([])
const loading = ref(true)
const saving = ref(false)
const activeSection = ref('general')
const showIconPicker = ref(false)
const showAddMember = ref(false)

const form = reactive({
  name: '',
  identifier: '',
  description: '',
  networkType: 'Public',
  icon: '📁'
})

const emojiList = ['📁', '🚀', '🛠️', '🎨', '📊', '🌐', '🔐', '📅', '💡', '🔥', '✨', '📦', '🏗️', '🧪', '📱']

const fetchData = async () => {
  loading.value = true
  try {
    const [projRes, membersRes] = await Promise.all([
      axiosClient.get(`/projects/${projectId}`),
      axiosClient.get(`/projects/${projectId}/members`)
    ])
    
    project.value = projRes.data?.data
    members.value = membersRes.data?.data || []
    
    if (project.value) {
      form.name = project.value.name
      form.identifier = project.value.identifier
      form.description = project.value.description
      form.networkType = project.value.networkType || 'Public'
      form.icon = project.value.icon || '📁'
    }
  } catch (err) {
    ElMessage.error('Failed to load project settings')
    console.error(err)
  } finally {
    loading.value = false
  }
}

const handleSave = async () => {
  saving.value = true
  try {
    await axiosClient.put(`/projects/${projectId}`, {
      ...form,
      id: projectId
    })
    ElMessage.success('Project settings updated')
  } catch (err) {
    ElMessage.error('Failed to save changes')
  } finally {
    saving.value = false
  }
}

const handleArchive = async () => {
  try {
    await ElMessageBox.confirm('Are you sure you want to archive this project?', 'Archive Project', { type: 'warning' })
    await axiosClient.put(`/projects/${projectId}/archive`)
    ElMessage.success('Project archived')
    fetchData()
  } catch (e) {}
}

const handleRestore = async () => {
  try {
    await axiosClient.put(`/projects/${projectId}/restore`)
    ElMessage.success('Project restored')
    fetchData()
  } catch (e) {}
}

const handleDelete = async () => {
  try {
    await ElMessageBox.confirm('CRITICAL: This will permanently delete the project and all its tasks. Continue?', 'Delete Project', { 
      type: 'error',
      confirmButtonText: 'Delete Forever',
      confirmButtonClass: 'el-button--danger'
    })
    await axiosClient.delete(`/projects/${projectId}`)
    ElMessage.success('Project deleted')
    router.push('/spaces')
  } catch (e) {}
}

const removeMember = async (member) => {
  try {
    await ElMessageBox.confirm(`Remove ${member.fullName} from project?`, 'Remove Member')
    // API call for remove member if exists
    ElMessage.info('Member removal logic needed')
  } catch (e) {}
}

onMounted(fetchData)
</script>

<style scoped>
.settings-wrapper {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background: var(--bg-primary);
}

.settings-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 24px;
  border-bottom: 1px solid var(--border-color);
}

.breadcrumb {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 500;
  color: var(--text-secondary);
}
.separator { font-size: 10px; color: #71717A; }

.settings-content {
  display: flex;
  flex: 1;
  overflow: hidden;
}

.settings-sidebar {
  width: 240px;
  border-right: 1px solid var(--border-color);
  padding: 20px 12px;
}

.settings-nav {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.settings-nav li {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  color: #A1A1AA;
  font-size: 14px;
  font-weight: 500;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.settings-nav li:hover {
  background: var(--hover-bg);
  color: #E4E4E7;
}

.settings-nav li.active {
  background: var(--hover-bg);
  color: #0EA5E9;
}

.settings-nav li.danger-nav:hover {
  color: #EF4444;
}

.settings-main {
  flex: 1;
  padding: 40px;
  overflow-y: auto;
}

.settings-section {
  max-width: 640px;
}

.section-title {
  font-size: 20px;
  font-weight: 600;
  margin-bottom: 24px;
}

.form-group {
  margin-bottom: 20px;
}

.form-group label {
  display: block;
  font-size: 13px;
  font-weight: 500;
  color: #71717A;
  margin-bottom: 8px;
}

.glass-input {
  width: 100%;
  background: var(--bg-tertiary);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  color: #E4E4E7;
  padding: 10px 14px;
  font-size: 14px;
  outline: none;
  transition: border-color 0.2s;
}

.glass-input:focus {
  border-color: #3B82F6;
}

.glass-input:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.form-hint {
  display: block;
  font-size: 12px;
  color: #52525B;
  margin-top: 4px;
}

.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
}

.icon-selector {
  display: flex;
  align-items: center;
  gap: 16px;
}

.current-icon {
  width: 44px; height: 44px;
  background: var(--bg-secondary);
  border-radius: 8px;
  display: flex; align-items: center; justify-content: center;
  font-size: 24px;
}

.neutral-btn {
  background: var(--bg-secondary);
  border: 1px solid #27272A;
  color: #E4E4E7;
  padding: 8px 16px;
  border-radius: 6px;
  font-size: 13px;
  cursor: pointer;
}

.member-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  background: var(--bg-tertiary);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  margin-bottom: 8px;
}

.member-avatar {
  width: 32px; height: 32px;
  background: #0EA5E9;
  border-radius: 50%;
  display: flex; align-items: center; justify-content: center;
  font-size: 14px; font-weight: 600;
}

.member-info { flex: 1; }
.member-name { font-size: 14px; font-weight: 500; }
.member-role { font-size: 12px; color: #71717A; }

.danger-card {
  padding: 20px;
  border: 1px solid rgba(239, 68, 68, 0.2);
  background: rgba(239, 68, 68, 0.05);
  border-radius: 12px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.dc-info h4 { margin: 0 0 4px 0; font-size: 15px; font-weight: 600; }
.dc-info p { margin: 0; font-size: 13px; color: #A1A1AA; }

.danger-btn {
  background: #EF4444;
  color: white;
  border: none;
  padding: 10px 20px;
  border-radius: 8px;
  font-weight: 600;
  cursor: pointer;
}

.danger-btn-outline {
  background: transparent;
  border: 1px solid #EF4444;
  color: #EF4444;
  padding: 8px 16px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
}
.danger-btn-outline:hover { background: #EF4444; color: white; }

.emoji-grid {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 12px;
  padding: 10px;
}
.emoji-opt {
  font-size: 24px;
  padding: 10px;
  cursor: pointer;
  border-radius: 8px;
  text-align: center;
  transition: background 0.2s;
}
.emoji-opt:hover { background: var(--hover-bg); }

:deep(.glass-select .el-input__wrapper) {
  background: var(--bg-tertiary) !important;
  border: 1px solid var(--border-color) !important;
  box-shadow: none !important;
}
:deep(.glass-select .el-input__inner) {
  color: #E4E4E7 !important;
}
</style>
