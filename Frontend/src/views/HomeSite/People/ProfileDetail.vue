<template>
  <div class="profile-detail-container" v-if="user">
    <!-- Cover Image -->
    <div class="profile-cover">
      <!-- Floating Back Button -->
      <button class="back-floating-btn" @click="handleBack">
        <i class="fa-solid fa-arrow-left"></i>
        <span>Quay lại</span>
      </button>
    </div>

    <!-- Header -->
    <div class="profile-header-wrapper">
      <div class="profile-identity">
        <UserAvatar :user="{ id: user.id, avatarUrl: user.avatarUrl, avatarColor: user.avatarColor, initials: user.initials, fullName: user.fullName, email: user.email }" :size="96" :fontSize="36" class="profile-avatar" :class="{ inactive: isInactive }" />
        <div class="profile-title-block">
          <div class="title-row">
            <h1 style="margin: 0; font-size: 28px;">{{ user.fullName }}</h1>
            <span v-if="isInactive" class="badge inactive">Inactive Account</span>
          </div>
          <div class="profile-status-row" style="margin-top: 4px; font-size: 13px; color: #42526E; display: flex; align-items: center; gap: 8px; flex-wrap: wrap;">
            <span><span style="color: #5e6c84;">Chức vụ:</span> <strong>{{ user.position || 'N/A' }}</strong></span>
            <span style="color: #dfe1e6;">|</span>
            <span><span style="color: #5e6c84;">Email:</span> <strong>{{ user.email }}</strong></span>
          </div>
        </div>
      </div>
      <div class="header-actions">
        <button class="secondary-btn" :disabled="isInactive">Message</button>
        <div class="dropdown-container">
          <button class="icon-btn menu-btn" @click.stop="isMenuOpen = !isMenuOpen" title="More actions">
            <i class="fa-solid fa-ellipsis-vertical"></i>
          </button>
          <div class="dropdown-menu" v-if="isMenuOpen">
            <button class="menu-item" :disabled="isInactive" @click="openEditProfile"><i class="fa-solid fa-pen"></i> Edit Profile</button>
            <button class="menu-item" :disabled="isInactive"><i class="fa-solid fa-gear"></i> Admin Settings</button>
          </div>
        </div>
      </div>
    </div>

    <!-- Tabs Nav -->
    <div class="tabs-nav">
      <button class="tab-btn" :class="{ active: currentTab === 'overview' }" @click="currentTab = 'overview'">Overview</button>
      <button class="tab-btn" :class="{ active: currentTab === 'tasks' }" @click="currentTab = 'tasks'">Tasks</button>
      <button class="tab-btn" :class="{ active: currentTab === 'goals' }" @click="currentTab = 'goals'">Goals</button>
      <button class="tab-btn" :class="{ active: currentTab === 'projects' }" @click="currentTab = 'projects'">Projects</button>
      <button class="tab-btn" :class="{ active: currentTab === 'kudos' }" @click="currentTab = 'kudos'">Kudos</button>
      <button class="tab-btn" :class="{ active: currentTab === 'history' }" @click="currentTab = 'history'">History</button>
    </div>

    <!-- Tab Content -->
    <div class="tab-content" :class="{ 'read-only-state': isInactive }">
      <div v-if="isInactive" class="inactive-banner">
        This user account is inactive. Profile information is read-only.
      </div>

      <!-- Overview -->
      <div v-if="currentTab === 'overview'" class="tab-pane layout-grid">
        <div class="main-column">
          <section class="info-section">
            <h3>Bio</h3>
            <div class="bio-content-wrapper" :class="{ 'is-editing': editingBio }">
              <RichTextEditor 
                v-if="editingBio"
                v-model="tempBio"
                @save="saveBio"
                @cancel="editingBio = false"
                placeholder="Thêm giới thiệu về bạn..."
              />
              <div 
                v-else 
                class="bio-display tiptap-content" 
                @click="startEditingBio"
                style="min-height: 40px; padding: 8px; border: 1px solid transparent; border-radius: 4px; cursor: pointer;"
                onmouseover="this.style.backgroundColor='#FAFBFC'"
                onmouseout="this.style.backgroundColor='transparent'"
              >
                <div v-if="user.bio && user.bio !== '<p></p>'" v-html="user.bio"></div>
                <div v-else style="color: #5E6C84;">Thêm giới thiệu về bạn...</div>
              </div>
            </div>
          </section>
          <section class="info-section">
            <h3>Hobbies & Interests</h3>
            <div class="bio-content-wrapper" :class="{ 'is-editing': editingHobbies }">
              <RichTextEditor 
                v-if="editingHobbies"
                v-model="tempHobbies"
                @save="saveHobbies"
                @cancel="editingHobbies = false"
                placeholder="Chia sẻ sở thích của bạn..."
              />
              <div 
                v-else 
                class="bio-display tiptap-content" 
                @click="startEditingHobbies"
                style="min-height: 40px; padding: 8px; border: 1px solid transparent; border-radius: 4px; cursor: pointer;"
                onmouseover="this.style.backgroundColor='#FAFBFC'"
                onmouseout="this.style.backgroundColor='transparent'"
              >
                <div v-if="user.hobbies && user.hobbies !== '<p></p>'" v-html="user.hobbies"></div>
                <div v-else style="color: #5E6C84;">Has not shared any hobbies yet.</div>
              </div>
            </div>
          </section>
          <section class="info-section">
            <h3>Teams & Departments</h3>
            <div class="teams-list">
              <div class="team-chip" v-for="t in user.teamsList" :key="t.id">
                <i class="fa-solid fa-users team-icon"></i>
                {{ t.name }}
              </div>
              <div class="empty-state-micro" v-if="!user.teamsList || user.teamsList.length === 0">
                Not a member of any teams.
              </div>
            </div>
          </section>
        </div>
        <div class="side-column">
          <div class="side-card">
            <h3>About</h3>
            <div class="detail-row">
              <span class="label">Full Name</span>
              <span class="value">{{ user.fullName }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Email</span>
              <span class="value">{{ user.email }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Department</span>
              <span class="value">{{ user.department }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Position</span>
              <span class="value">{{ user.position }}</span>
            </div>
            <div class="detail-row">
              <span class="label">Team</span>
              <span class="value">{{ user.team }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Tasks -->
      <div v-if="currentTab === 'tasks'" class="tab-pane">
        <div class="section-header-row">
          <h3>Assigned Tasks</h3>
        </div>
        <p class="helper-text">Tasks assigned across all Space Projects.</p>
        <table class="jira-table mt-16" v-if="assignedTasks?.length">
          <thead>
            <tr><th>Key</th><th>Summary</th><th>Project</th><th>Status</th></tr>
          </thead>
          <tbody>
            <tr v-for="task in assignedTasks" :key="task.id" @click="goToTask(task)">
              <td class="key-col">{{ task.key }}</td>
              <td class="link-text">{{ task.summary }}</td>
              <td>{{ task.projectName }}</td>
              <td><span class="badge status-light">{{ task.status }}</span></td>
            </tr>
          </tbody>
        </table>
        <div class="empty-state-card" v-else>
          <i class="fa-solid fa-file-signature"></i>
          <span>No tasks assigned.</span>
        </div>
      </div>

      <!-- Goals -->
      <div v-if="currentTab === 'goals'" class="tab-pane">
        <div class="section-header-row">
          <h3>Linked Goals</h3>
        </div>
        <table class="jira-table mt-16" v-if="linkedGoals && linkedGoals.length">
          <thead>
            <tr><th>Goal Title</th><th>Status</th></tr>
          </thead>
          <tbody>
            <tr v-for="goal in linkedGoals" :key="goal.id" @click="goToGoal(goal.id)">
              <td class="link-text"><i class="fa-solid fa-bullseye"></i> {{ goal.title }}</td>
              <td><span class="status-badge" :class="statusClass(goal.status)">{{ goal.status }}</span></td>
            </tr>
          </tbody>
        </table>
        <div class="empty-state-card" v-else>
          <i class="fa-solid fa-bullseye"></i>
          <span>No goals linked.</span>
        </div>
      </div>

      <!-- Projects -->
      <div v-if="currentTab === 'projects'" class="tab-pane">
        <div class="section-header-row">
          <h3>Linked Projects</h3>
        </div>
        <table class="jira-table mt-16" v-if="linkedProjects && linkedProjects.length">
          <thead>
            <tr><th>Project Name</th><th>Status</th></tr>
          </thead>
          <tbody>
            <tr v-for="proj in linkedProjects" :key="proj.id" @click="goToProject(proj.id)">
              <td class="link-text"><i class="fa-solid fa-chart-simple"></i> {{ proj.title }}</td>
              <td><span class="badge status-light">{{ proj.status }}</span></td>
            </tr>
          </tbody>
        </table>
        <div class="empty-state-card" v-else>
          <i class="fa-solid fa-chart-simple"></i>
          <span>No projects linked.</span>
        </div>
      </div>

      <!-- Kudos -->
      <div v-if="currentTab === 'kudos'" class="tab-pane">
        <div class="section-header-row">
          <h3>Kudos Received</h3>
          <button class="secondary-btn" :disabled="isInactive" @click="handleGiveKudos">Give Kudos</button>
        </div>
        <div class="kudos-grid mt-16" v-if="kudos && kudos.length">
          <div class="kudos-card" v-for="k in kudos" :key="k.id">
            <div class="kudos-icon">{{ k.icon || 'Star' }}</div>
            <div class="kudos-content">
              <p class="kudos-msg">"{{ k.message }}"</p>
              <div class="kudos-meta">
                <span class="kudos-sender">From {{ k.sender }}</span> &bull; <span class="kudos-date">{{ k.date }}</span>
              </div>
            </div>
          </div>
        </div>
        <div class="empty-state-card" v-else>
          <i class="fa-solid fa-star"></i>
          <span>No kudos received yet.</span>
        </div>
      </div>

      <!-- History -->
      <div v-if="currentTab === 'history'" class="tab-pane">
        <h3>Activity Timeline</h3>
        <table class="jira-table mt-16" v-if="history && history.length">
          <thead>
            <tr><th>Time</th><th>Action</th></tr>
          </thead>
          <tbody>
            <tr v-for="log in history" :key="log.id">
              <td class="time-col">{{ log.time }}</td>
              <td>{{ log.action }}</td>
            </tr>
          </tbody>
        </table>
        <div class="empty-state-card" v-else>
          <i class="fa-solid fa-clock-rotate-left"></i>
          <span>No activity history yet.</span>
        </div>
      </div>
    </div>
  </div>
  <div v-else class="loading-state">
    <div class="loader-spinner"></div>
    <p>Loading profile...</p>
  </div>

  <!-- Edit Profile Modal -->
  <Teleport to="body">
  <div class="modal-overlay sa-data-modal-overlay" v-if="isEditModalOpen" @click.self="closeEditProfile">
    <div class="modal-content">
      <DataModalHeader
        icon="bi bi-person-gear"
        title="Edit Profile"
        description="Update public profile information shown across the workspace"
        @close="closeEditProfile"
      />
      <div class="modal-body">
        <DataModalSection
          icon="bi bi-person-lines-fill"
          title="Basic information"
          description="Keep name, role, and location consistent for teammates"
        >
          <div class="sa-modal-form-grid">
            <DataModalField label="Full Name">
              <input type="text" v-model="editForm.fullName" class="form-input" />
            </DataModalField>
            <DataModalField label="Job Title">
              <input type="text" v-model="editForm.jobTitle" class="form-input" />
            </DataModalField>
          </div>
          <DataModalField label="Location">
            <input type="text" v-model="editForm.location" class="form-input" />
          </DataModalField>
        </DataModalSection>
        <DataModalSection
          icon="bi bi-card-text"
          title="Profile bio"
          description="Describe responsibilities, focus, or useful context"
        >
          <DataModalField label="Bio">
            <textarea v-model="editForm.bio" class="form-input" rows="4"></textarea>
          </DataModalField>
        </DataModalSection>
        <div class="error-message" v-if="editError">{{ editError }}</div>
      </div>
      <div class="modal-footer">
        <button class="cancel-btn" @click="closeEditProfile" :disabled="isSaving">
          <i class="bi bi-x-lg"></i>
          Cancel
        </button>
        <button class="primary-btn" @click="saveProfile" :disabled="isSaving">
          <i class="bi bi-check-lg"></i>
          {{ isSaving ? 'Saving...' : 'Save Changes' }}
        </button>
      </div>
    </div>
  </div>
  </Teleport>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { getStoredUser } from '@/utils/permissions'
import { useRoute, useRouter } from 'vue-router'
import { usePeopleStore } from '@/store/usePeopleStore'
import UserAvatar from '@/components/common/UserAvatar.vue'
import RichTextEditor from '@/components/common/RichTextEditor.vue'
import { useGoalStore } from '@/store/useGoalStore'
import { useHomeProjectStore as useProjectStore } from '@/store/useHomeProjectStore'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import DataModalField from '@/components/common/Foundation/DataModalField.vue'

const route = useRoute()
const router = useRouter()
const peopleStore = usePeopleStore()
const goalStore = useGoalStore()
const projectStore = useProjectStore()
const profileUserId = computed(() => String(route.params.profileId || route.params.id || '').trim())


const editingBio = ref(false)
const tempBio = ref('')
const startEditingBio = () => {
  tempBio.value = user.value.bio || ''
  editingBio.value = true
}
const saveBio = async () => {
  try {
    await peopleStore.updateProfile({ bio: tempBio.value })
    editingBio.value = false
    await peopleStore.fetchProfileDetail(profileUserId.value)
  } catch(e) { console.error('Save bio failed', e) }
}

const editingHobbies = ref(false)
const tempHobbies = ref('')
const startEditingHobbies = () => {
  tempHobbies.value = user.value.hobbies || ''
  editingHobbies.value = true
}
const saveHobbies = async () => {
  try {
    await peopleStore.updateProfile({ hobbies: tempHobbies.value })
    editingHobbies.value = false
    await peopleStore.fetchProfileDetail(profileUserId.value)
  } catch(e) { console.error('Save hobbies failed', e) }
}

const currentTab = ref('overview')
const isMenuOpen = ref(false)
const isEditModalOpen = ref(false)
const isSaving = ref(false)
const editError = ref('')
const editForm = ref({
  fullName: '',
  jobTitle: '',
  location: '',
  bio: ''
})

const user = computed(() => {
  const u = peopleStore.currentUser || getStoredUser() || {}
  return {
    ...u,
    teamsList: u.departments || [],
    hobbies: u.hobbies || '',
    avatarColor: u.avatarColor
  }
})

const assignedTasks = ref([])
const linkedGoals = computed(() => peopleStore.linkedGoals)
const linkedProjects = computed(() => peopleStore.linkedProjects)
const kudos = computed(() => peopleStore.kudos)
const history = computed(() => peopleStore.history)

const isInactive = computed(() => user.value?.status === 'Inactive')

onMounted(async () => {
  goalStore.fetchGoals();
  projectStore.fetchProjects();
  if (profileUserId.value) {
    await peopleStore.fetchProfileDetail(profileUserId.value)
  }
  document.addEventListener('click', closeMenuOnOutsideClick)
})

onUnmounted(() => {
  document.removeEventListener('click', closeMenuOnOutsideClick)
})

const closeMenuOnOutsideClick = (e) => {
  if (isMenuOpen.value && !e.target.closest('.dropdown-container')) {
    isMenuOpen.value = false
  }
}

const handleBack = () => {
  if (route.path.startsWith('/space/')) {
    const spaceSlug = route.params.spaceSlug || 'project'
    const projectId = route.params.id || route.params.projectId
    router.push(`/space/${spaceSlug}/${projectId}/members`)
  } else if (route.path.startsWith('/home/')) {
    router.push('/home/people')
  } else {
    router.push('/teams/people')
  }
}

const goToGoal = (id) => {
  if (route.path.startsWith('/home/')) {
    router.push(`/home/goals/${id}`)
  } else {
    router.push(`/goals/${id}`)
  }
}

const goToProject = (id) => {
  if (route.path.startsWith('/home/')) {
    router.push(`/home/projects/${id}`)
  } else {
    router.push(`/space/project/${id}`)
  }
}

const handleGiveKudos = () => {
  if (route.path.startsWith('/home/')) {
    router.push('/home/teams/kudos')
  } else {
    router.push('/teams/kudos')
  }
}

const statusClass = (status) => `${status || ''}`.toLowerCase().replace(/\s+/g, '-')

const openEditProfile = () => {
  isMenuOpen.value = false
  editForm.value = {
    fullName: user.value?.fullName || '',
    jobTitle: user.value?.position || '',
    location: user.value?.location || '',
    bio: user.value?.bio || ''
  }
  editError.value = ''
  isEditModalOpen.value = true
}

const closeEditProfile = () => {
  if (!isSaving.value) {
    isEditModalOpen.value = false
  }
}

const saveProfile = async () => {
  isSaving.value = true
  editError.value = ''
  try {
    const payload = {
      fullName: editForm.value.fullName,
      jobTitle: editForm.value.jobTitle,
      location: editForm.value.location,
      bio: editForm.value.bio
    }
    await peopleStore.updateProfile(payload)
    isEditModalOpen.value = false
    await peopleStore.fetchProfileDetail(profileUserId.value) // reload
  } catch (err) {
    editError.value = err.response?.data?.message || err.message || 'Failed to update profile'
  } finally {
    isSaving.value = false
  }
}

const goToTask = (task) => {
  // Navigate to space project
  console.log('Navigate to space task', task.id)
}
</script>

<style scoped>
.profile-detail-container {
  display: flex;
  flex-direction: column;
  position: relative;
  margin: -8px 0 0;
  background-color: #ffffff;
  min-height: 100vh;
  width: 100% !important;
  max-width: none !important;
}

.profile-cover {
  height: 200px;
  background: linear-gradient(135deg, #0747a6 0%, #0052cc 50%, #2684ff 100%);
  position: relative;
  flex-shrink: 0;
}

.back-floating-btn {
  position: absolute;
  top: 16px;
  left: 18px;
  z-index: 100;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: rgba(255, 255, 255, 0.85);
  backdrop-filter: blur(8px);
  border: 1px solid rgba(148, 163, 184, 0.2);
  border-radius: 20px;
  color: #172b4d;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
  transition: all 0.2s ease;
}

.back-floating-btn:hover {
  background: #ffffff;
  transform: translateY(-1px);
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.12);
  color: #0052cc;
}

.profile-header-wrapper {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  padding: 0 18px;
  margin-top: -16px; /* Lowered from -32px to give more space from cover banner */
  margin-bottom: 24px;
}

.profile-identity {
  display: flex;
  align-items: flex-end;
  gap: 20px;
}

.profile-avatar {
  border: 4px solid #ffffff;
  z-index: 2;
}

.profile-title-block {
  padding-bottom: 4px;
}

.badge {
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
}

.badge.inactive {
  background-color: #dfe1e6;
  color: #42526e;
}

.badge.status-light {
  background-color: #ebecf0;
  color: #172b4d;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  padding-bottom: 8px;
}

.secondary-btn {
  background-color: rgba(9, 30, 66, 0.04);
  color: #42526e;
  border: none;
  padding: 6px 12px;
  border-radius: 3px;
  font-weight: 500;
  font-size: 14px;
  cursor: pointer;
}

.secondary-btn:hover:not(:disabled) {
  background-color: rgba(9, 30, 66, 0.08);
}

.icon-btn {
  background: rgba(9, 30, 66, 0.04);
  border: none;
  cursor: pointer;
  width: 32px;
  height: 32px;
  color: #42526e;
  border-radius: 3px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.icon-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.dropdown-container {
  position: relative;
}

.dropdown-menu {
  position: absolute;
  top: 100%;
  right: 0;
  margin-top: 4px;
  width: 200px;
  background: white;
  border-radius: 3px;
  box-shadow: 0 4px 8px -2px rgba(9, 30, 66, 0.25), 0 0 1px rgba(9, 30, 66, 0.31);
  padding: 8px 0;
  z-index: 10;
}

.menu-item {
  width: 100%;
  text-align: left;
  background: none;
  border: none;
  padding: 8px 16px;
  font-size: 14px;
  color: #172b4d;
  cursor: pointer;
}

.menu-item:hover:not(:disabled) {
  background-color: #f4f5f7;
}

.menu-item:disabled {
  color: #a5adba;
  cursor: not-allowed;
}

/* Tabs Nav */
.tabs-nav {
  display: flex;
  align-items: center;
  gap: 6px !important;
  width: max-content !important;
  max-width: calc(100% - 36px);
  min-height: 42px;
  margin: 0 18px 12px !important;
  padding: 4px !important;
  border: 1px solid rgba(148, 163, 184, 0.2) !important;
  border-radius: 9px !important;
  background: transparent !important;
  box-shadow: none !important;
  overflow-x: auto;
  flex-shrink: 0;
}

.tab-btn {
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 34px !important;
  min-width: max-content;
  padding: 0 16px !important;
  border: 0 !important;
  border-radius: 7px !important;
  background: transparent !important;
  color: #475569 !important;
  font-size: 12.5px !important;
  font-weight: 800 !important;
  line-height: 1;
  text-decoration: none;
  white-space: nowrap;
  transition: background 0.18s ease, color 0.18s ease;
  cursor: pointer;
}

.tab-btn:hover {
  color: #0f172a !important;
  background: rgba(14, 165, 233, 0.06) !important;
}

.tab-btn.active {
  color: #0369a1 !important;
  background: linear-gradient(135deg, rgba(34, 211, 238, 0.20), rgba(45, 212, 191, 0.14)) !important;
  box-shadow: none !important;
}

/* Tab Content */
.tab-content {
  padding: 8px 18px 32px;
  flex: 1;
}

.inactive-banner {
  background-color: #fafbfc;
  border: 1px solid #dfe1e6;
  border-left: 4px solid #6b778c;
  padding: 12px 16px;
  border-radius: 3px;
  color: #172b4d;
  margin-bottom: 24px;
  font-size: 14px;
  font-weight: 500;
}

.read-only-state .info-section,
.read-only-state .jira-table,
.read-only-state .side-card {
  opacity: 0.9;
}

.layout-grid {
  display: flex;
  gap: 32px;
}

.main-column {
  flex: 1;
  min-width: 0;
}

.side-column {
  width: 320px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.side-card {
  border: 1px solid #dfe1e6;
  border-radius: 12px;
  padding: 20px;
  background-color: #ffffff;
  box-shadow: var(--sp-shadow-xs);
}

.side-card h3 {
  margin: 0 0 16px 0;
  font-size: 14px;
  color: #5e6c84;
  text-transform: uppercase;
}

.detail-row {
  display: flex;
  flex-direction: column;
  gap: 4px;
  margin-bottom: 16px;
}

.detail-row:last-child {
  margin-bottom: 0;
}

.detail-row .label {
  font-size: 12px;
  color: #5e6c84;
}

.detail-row .value {
  font-size: 14px;
  color: #172b4d;
  font-weight: 500;
}

/* Section headers */
.section-header-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  max-width: 800px;
}

.section-header-row h3 {
  margin: 0;
  font-size: 18px;
  color: #172b4d;
}

/* Tables */
.jira-table {
  width: 100%;
  max-width: 800px;
  border-collapse: collapse;
  text-align: left;
}

.jira-table th {
  padding: 8px 12px;
  font-size: 12px;
  font-weight: 600;
  color: #5e6c84;
  border-bottom: 2px solid #dfe1e6;
}

.jira-table td {
  padding: 12px;
  font-size: 14px;
  color: #172b4d;
  border-bottom: 1px solid #dfe1e6;
  cursor: pointer;
}

.jira-table tbody tr:hover td {
  background-color: #fafbfc;
}

.link-text {
  color: #0052cc;
  cursor: pointer;
}

.link-text:hover {
  text-decoration: underline;
}

.time-col {
  color: #5e6c84;
  font-size: 12px;
}

.status-badge {
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  background-color: #dfe1e6;
  color: #42526e;
}

.status-badge.on-track { background-color: #e3fcef; color: #006644; }

/* Kudos Grid */
.kudos-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
  max-width: 800px;
}

.kudos-card {
  border: 1px solid #dfe1e6;
  border-radius: 3px;
  padding: 16px;
  display: flex;
  gap: 12px;
  background-color: #ffffff;
}

.kudos-icon {
  font-size: 24px;
}

.kudos-content {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.kudos-msg {
  margin: 0;
  font-size: 14px;
  color: #172b4d;
  font-style: italic;
}

.kudos-meta {
  font-size: 12px;
  color: #5e6c84;
}

.kudos-sender {
  font-weight: 500;
  color: #172b4d;
}

.mt-16 { margin-top: 16px; }

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #5e6c84;
  gap: 16px;
  padding: 60px;
}

.loader-spinner {
  width: 32px;
  height: 32px;
  border: 3px solid #dfe1e6;
  border-top-color: #0052cc;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.teams-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 8px;
}

.team-chip {
  display: flex;
  align-items: center;
  gap: 6px;
  background-color: #fafbfc;
  border: 1px solid #dfe1e6;
  border-radius: 16px;
  padding: 4px 12px;
  font-size: 13px;
  color: #172b4d;
}

.team-icon {
  font-size: 14px;
}

.empty-state-micro {
  color: #5e6c84;
  font-style: italic;
  font-size: 13px;
}

.key-col {
  color: #5e6c84;
  font-size: 12px;
  font-family: monospace;
}

.helper-text {
  color: #5e6c84;
  font-size: 13px;
  margin-bottom: 16px;
}

.empty-state-card {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 14px 16px;
  border-radius: 12px;
  background: var(--color-surface);
  border: 1px solid var(--color-border) !important;
  color: var(--color-text-secondary) !important;
  font-weight: 600;
  box-shadow: var(--sp-shadow-xs);
  box-sizing: border-box;
  margin-top: 16px;
}

.empty-state-card i {
  font-size: 16px;
  color: var(--color-text-secondary);
}

/* Light / Dark Mode Overrides */
[data-theme='dark'] .profile-detail-container {
  background-color: #0b0f19 !important;
}
[data-theme='dark'] .profile-header-wrapper h1,
[data-theme='dark'] .side-card h3,
[data-theme='dark'] .detail-row .value {
  color: #f1f5f9 !important;
}
[data-theme='dark'] .tab-btn {
  color: #94a3b8 !important;
}
[data-theme='dark'] .tab-btn:hover {
  color: #f1f5f9 !important;
  background: rgba(14, 165, 233, 0.1) !important;
}
[data-theme='dark'] .tab-btn.active {
  color: #38bdf8 !important;
  background: linear-gradient(135deg, rgba(56, 189, 248, 0.2), rgba(45, 212, 191, 0.14)) !important;
}
[data-theme='dark'] .side-card {
  background-color: #1e293b !important;
  border-color: rgba(148, 163, 184, 0.18) !important;
}
[data-theme='dark'] .empty-state-card {
  background: rgba(30, 41, 59, 0.72) !important;
  border-color: rgba(148, 163, 184, 0.18) !important;
}
[data-theme='light'] .empty-state-card {
  background:
    linear-gradient(135deg, rgba(255, 255, 255, 0.97), rgba(248, 250, 252, 0.88)),
    #ffffff !important;
  border-color: rgba(148, 163, 184, 0.24) !important;
}

/* Modal Styles */
.modal-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background-color: rgba(9, 30, 66, 0.54);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background-color: #FFFFFF;
  border-radius: 3px;
  width: 500px;
  box-shadow: 0 8px 16px -4px rgba(9, 30, 66, 0.25);
  display: flex;
  flex-direction: column;
}

.modal-header {
  padding: 20px 24px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid #DFE1E6;
}

.modal-header h2 {
  margin: 0;
  font-size: 20px;
  font-weight: 500;
  color: #172B4D;
}

.close-btn {
  background: none;
  border: none;
  font-size: 24px;
  color: #5E6C84;
  cursor: pointer;
}

.modal-body {
  padding: 24px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 16px;
}

.form-group label {
  font-size: 12px;
  font-weight: 600;
  color: #5E6C84;
}

.form-input {
  width: 100%;
  padding: 8px 12px;
  border: 2px solid #DFE1E6;
  border-radius: 3px;
  font-size: 14px;
  color: #172B4D;
  outline: none;
  transition: border-color 0.2s, background-color 0.2s;
  box-sizing: border-box;
  font-family: inherit;
}

.form-input:focus {
  border-color: #4C9AFF;
  background-color: #FFFFFF;
}

.error-message {
  color: #DE350B;
  font-size: 13px;
  margin-top: 8px;
}

.modal-footer {
  padding: 16px 24px;
  border-top: 1px solid #DFE1E6;
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.cancel-btn {
  background: transparent;
  border: none;
  color: #42526E;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  padding: 8px 12px;
  border-radius: 3px;
}

.cancel-btn:hover:not(:disabled) {
  background-color: rgba(9, 30, 66, 0.08);
}

.primary-btn {
  background-color: #0052CC;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 3px;
  font-weight: 500;
  font-size: 14px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.primary-btn:hover:not(:disabled) {
  background-color: #0047B3;
}

.primary-btn:disabled {
  background-color: rgba(9, 30, 66, 0.04);
  color: #A5ADBA;
  cursor: not-allowed;
}
</style>
