<template>
  <Teleport to="body">
  <div class="modal-overlay sa-data-modal-overlay" v-if="isOpen" @click.self="closeModal">
    <div class="share-dialog">
      <div class="share-header">
        <DataModalHeader icon="bi bi-share" title="Share" description="Invite people and manage access" close-label="Close" @close="closeModal" />
      </div>
      
      <div class="share-body">
        <DataModalSection icon="bi bi-person-plus" title="People and access">
        <!-- Search Input & Selected Chips -->
        <div class="search-container" :class="{ 'has-selection': selectedUsers.length > 0 }">
          <div class="selected-chips" v-if="selectedUsers.length > 0">
            <div class="chip" v-for="(user, index) in selectedUsers" :key="user.id">
              <div class="chip-avatar">{{ getInitials(user.name) }}</div>
              <span class="chip-name">{{ user.name }}</span>
              <button class="chip-remove" @click="removeUser(index)"><i class="fa-solid fa-xmark"></i></button>
            </div>
          </div>
          <input 
            type="text" 
            class="search-input" 
            :placeholder="selectedUsers.length > 0 ? 'Add more people' : 'Search and invite people'" 
            v-model="searchQuery"
          />
          <div class="role-dropdown-container" v-if="selectedUsers.length > 0">
            <select class="role-select" v-model="inviteRole">
              <option value="Can edit">Can edit</option>
              <option value="Can view">Can view</option>
            </select>
          </div>
        </div>
        </DataModalSection>

        <!-- Suggested Users (when NO user is selected) -->
        <DataModalSection icon="bi bi-people" title="Suggested people">
        <div class="suggested-users" v-if="selectedUsers.length === 0">
          <button 
            class="suggestion-btn" 
            v-for="user in filteredSuggestions" 
            :key="user.id"
            @click="selectUser(user)"
          >
            <i class="fa-solid fa-plus"></i> {{ user.name }}
          </button>
        </div>
        </DataModalSection>

        <!-- SHARE MODE (when a user IS selected) -->
        <div class="share-actions-mode" v-if="selectedUsers.length > 0">
          <div class="share-options">
            <label class="checkbox-label">
              <input type="checkbox" v-model="addAsFollower" />
              <span>Thêm vào danh sách người theo dõi</span>
            </label>
            <button class="primary-btn" @click="handleShare" :disabled="isSubmitting">
              {{ isSubmitting ? 'Đang gửi...' : 'Share' }}
            </button>
          </div>
        </div>

        <!-- TABS MODE (when NO user is selected) -->
        <template v-else>
          <div class="share-tabs">
            <button class="tab-btn" :class="{ active: activeTab === 'access' }" @click="activeTab = 'access'">Access <span class="badge">1</span></button>
            <button class="tab-btn" :class="{ active: activeTab === 'followers' }" @click="activeTab = 'followers'">Followers <span class="badge">{{ followers.length }}</span></button>
            
          </div>

          <!-- Tab Content: Access -->
          <div class="tab-content" v-if="activeTab === 'access'">
            <div class="access-section">
              <h4 class="section-label">General access</h4>
              <div class="access-row">
                <div class="access-icon open"><i class="fa-solid fa-lock-open"></i></div>
                <div class="access-info">
                  <div class="access-title">Open</div>
                  <div class="access-desc">Anyone can view, only specific people can edit</div>
                </div>
                <div class="access-role">
                  <select class="role-select-simple">
                    <option>Can view</option>
                    <option>Can edit</option>
                  </select>
                </div>
              </div>
            </div>

            <div class="access-section mt-16">
              <h4 class="section-label">Specific access</h4>
              <div class="access-list">
                <div class="access-row">
                  <UserAvatar :user="owner || currentUser" :size="32" :fontSize="14" />
                  <div class="access-info">
                    <div class="access-title">{{ owner?.fullName || currentUser.fullName }} (Owner)</div>
                    <div class="access-desc">{{ owner?.email || currentUser.email }}</div>
                  </div>
                  <div class="access-role text-muted">
                    Owner
                  </div>
                </div>
                <!-- Other members would go here -->
              </div>
            </div>
          </div>

          <!-- Tab Content: Followers -->
          <div class="tab-content" v-if="activeTab === 'followers'">
            <div v-if="followers.length === 0" class="empty-state">
              <div class="empty-illustration">
                <div class="avatar-circle top-left"><i class="fa-solid fa-user"></i></div>
                <div class="avatar-circle top-right"><i class="fa-solid fa-user"></i></div>
                <div class="avatar-circle bottom-left"><i class="fa-solid fa-user"></i></div>
                <div class="avatar-circle bottom-right primary"><i class="fa-solid fa-plus"></i></div>
              </div>
              <h3>Add followers to share your updates</h3>
              <p>Followers get a monthly update digest for any work they follow</p>
              <button class="primary-btn mt-16" @click="focusSearch">Add followers</button>
            </div>
            
            <div v-else class="access-list">
              <div class="access-row" v-for="follower in followers" :key="follower.userId || follower.id">
                <UserAvatar :user="{ fullName: follower.name, avatarColor: follower.avatarColor, avatarUrl: follower.avatarUrl }" :size="32" :fontSize="14" />
                <div class="access-info">
                  <div class="access-title">{{ follower.name }}</div>
                  <div class="access-desc">{{ follower.email || 'Thành viên' }}</div>
                </div>
                <div class="access-actions">
                  <button class="icon-btn"><i class="fa-solid fa-ellipsis"></i></button>
                </div>
              </div>
            </div>
          </div>

          
        </template>
      </div>
    </div>
  </div>
  </Teleport>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import axiosClient from '@/api/axiosClient'
import { useSiteStore } from '@/store/useSiteStore'
import { usePeopleStore } from '@/store/usePeopleStore'
import { ensureWorkspaceIdFromState, resolveWorkspaceIdFromState } from '@/utils/contextIds'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'

import UserAvatar from '@/components/common/UserAvatar.vue'

const props = defineProps({
  isOpen: Boolean,
  entityId: String,
  entityType: { type: String, default: 'Project' },
  entityName: String,
  workspaceId: String,
  owner: Object // { fullName, email, avatarUrl, avatarColor }
})

const emit = defineEmits(['close', 'shared'])

const activeTab = ref('access')
const searchQuery = ref('')
const selectedUsers = ref([])
const inviteRole = ref('Can view')
const addAsFollower = ref(true)
const isSubmitting = ref(false)

const siteStore = useSiteStore()
const peopleStore = usePeopleStore()
const followers = ref([])

const currentUser = computed(() => {
  try {
    return JSON.parse(localStorage.getItem('user') || localStorage.getItem('currentUser') || '{}')
  } catch {
    return {}
  }
})

onMounted(async () => {
  if (peopleStore.users.length === 0) {
    await peopleStore.fetchPeople()
  }
  if (props.isOpen && props.entityId) {
    fetchFollowers()
  }
})

watch(() => props.isOpen, (newVal) => {
  if (newVal && props.entityId) fetchFollowers()
})

const fetchFollowers = async () => {
  const workspaceId = await ensureWorkspaceId()
  if (!workspaceId) {
    followers.value = []
    return
  }
  try {
    const res = await axiosClient.get(`/workspaces/${workspaceId}/followers/entity`, {
      params: {
        entityType: props.entityType,
        entityId: props.entityId
      }
    })
    followers.value = normalizeFollowers(res.data?.data || res.data || [])
  } catch (e) {
    console.error('Failed to fetch followers', e)
  }
}

const getWorkspaceId = () => {
  return resolveWorkspaceIdFromState({
    explicitId: props.workspaceId,
    siteStore
  })
}

const ensureWorkspaceId = () => {
  return ensureWorkspaceIdFromState({
    explicitId: props.workspaceId,
    siteStore
  })
}

const normalizeFollowers = (items) => {
  return (items || []).map(item => ({
    id: item.id,
    userId: item.userId,
    name: item.name || item.fullName || item.FullName,
    fullName: item.fullName || item.FullName || item.name,
    email: item.email || item.Email,
    avatarUrl: item.avatarUrl || item.AvatarUrl,
    avatarColor: item.avatarColor
  }))
}

const suggestedPeople = computed(() => {
  return (peopleStore.users || []).map(u => ({ id: u.id, name: u.name || u.fullName, email: u.email, avatarColor: u.avatarColor, avatarUrl: u.avatarUrl }))
})

const filteredSuggestions = computed(() => {
  if (!searchQuery.value) return suggestedPeople.value.filter(u => !selectedUsers.value.some(s => s.id === u.id))
  const lowerQ = searchQuery.value.toLowerCase()
  return suggestedPeople.value.filter(u => 
    !selectedUsers.value.some(s => s.id === u.id) &&
    ((u.name && u.name.toLowerCase().includes(lowerQ)) || (u.email && u.email.toLowerCase().includes(lowerQ)))
  )
})

const getInitials = (name) => {
  if (!name) return '?'
  return name.charAt(0).toUpperCase()
}

const selectUser = (user) => {
  selectedUsers.value.push(user)
  searchQuery.value = ''
}

const removeUser = (index) => {
  selectedUsers.value.splice(index, 1)
}

const closeModal = () => {
  selectedUsers.value = []
  searchQuery.value = ''
  emit('close')
}

const focusSearch = () => {
  const input = document.querySelector('.search-input')
  if (input) input.focus()
}

const handleShare = async () => {
  if (selectedUsers.value.length === 0 || !props.entityId) return
  
  isSubmitting.value = true
  try {
    const workspaceId = await ensureWorkspaceId()
    if (!workspaceId) throw new Error('No workspace selected')

    if (addAsFollower.value) {
      const res = await axiosClient.post(`/workspaces/${workspaceId}/followers/entity`, {
        userIds: selectedUsers.value.map(user => user.id)
      }, {
        params: {
          entityType: props.entityType,
          entityId: props.entityId
        }
      })
      followers.value = normalizeFollowers(res.data?.data || res.data || [])
    }
    
    emit('shared', selectedUsers.value)
    selectedUsers.value = []
    activeTab.value = 'followers'
    
  } catch (err) {
    console.error('Lỗi khi chia sẻ:', err)
  } finally {
    isSubmitting.value = false
  }
}
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background-color: rgba(9, 30, 66, 0.54);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.share-dialog {
  background: #FFFFFF;
  border-radius: 3px;
  width: 520px;
  max-width: 90vw;
  box-shadow: 0 8px 16px -4px rgba(9,30,66,0.25), 0 0 1px rgba(9,30,66,0.31);
  display: flex;
  flex-direction: column;
}

.share-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
}

.share-header h2 {
  margin: 0;
  font-size: 20px;
  color: #172B4D;
  font-weight: 500;
}

.header-right-actions {
  display: flex;
  gap: 4px;
}

.icon-btn {
  background: transparent;
  border: none;
  color: #42526E;
  width: 32px;
  height: 32px;
  border-radius: 3px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: background-color 0.2s;
}

.icon-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.share-body {
  padding: 0 20px 20px;
}

.search-container {
  display: flex;
  align-items: center;
  border: 2px solid #DFE1E6;
  border-radius: 3px;
  padding: 4px 8px;
  transition: border-color 0.2s;
  flex-wrap: wrap;
  gap: 4px;
}

.search-container.has-selection {
  border-color: #4C9AFF;
}

.search-container:focus-within {
  border-color: #4C9AFF;
}

.selected-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.chip {
  display: flex;
  align-items: center;
  background-color: #DEEBFF;
  border: 1px solid #B3D4FF;
  border-radius: 16px;
  padding: 2px 4px 2px 2px;
  gap: 6px;
}

.chip-avatar {
  width: 20px;
  height: 20px;
  background-color: #0052CC;
  color: white;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  font-weight: bold;
}

.chip-name {
  font-size: 12px;
  color: #0052CC;
  font-weight: 500;
}

.chip-remove {
  background: transparent;
  border: none;
  color: #0052CC;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  border-radius: 50%;
}

.chip-remove:hover {
  background-color: rgba(0, 82, 204, 0.1);
}

.search-input {
  flex: 1;
  min-width: 150px;
  border: none;
  outline: none;
  padding: 8px 4px;
  font-size: 14px;
  color: #172B4D;
}

.role-dropdown-container {
  margin-left: auto;
  padding-left: 8px;
  border-left: 1px solid #DFE1E6;
}

.role-select {
  border: none;
  outline: none;
  background: transparent;
  color: #42526E;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
}

.suggested-users {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 12px;
}

.suggestion-btn {
  background: transparent;
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  padding: 6px 12px;
  font-size: 12px;
  color: #42526E;
  font-weight: 500;
  cursor: pointer;
  transition: background-color 0.2s;
  display: flex;
  align-items: center;
  gap: 6px;
}

.suggestion-btn:hover {
  background-color: rgba(9, 30, 66, 0.04);
}

.share-actions-mode {
  margin-top: 20px;
  display: flex;
  justify-content: flex-end;
}

.share-options {
  display: flex;
  align-items: center;
  gap: 16px;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: #172B4D;
  cursor: pointer;
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

.primary-btn:hover {
  background-color: #0047B3;
}

.primary-btn:disabled {
  background-color: #B3D4FF;
  cursor: not-allowed;
}

/* Tabs */
.share-tabs {
  display: flex;
  gap: 20px;
  border-bottom: 1px solid #DFE1E6;
  margin-top: 16px;
}

.tab-btn {
  background: transparent;
  border: none;
  color: #5E6C84;
  font-size: 14px;
  font-weight: 500;
  padding: 8px 0 12px;
  cursor: pointer;
  position: relative;
  margin-bottom: -1px;
  border-bottom: 2px solid transparent;
}

.tab-btn:hover {
  color: #172B4D;
}

.tab-btn.active {
  color: #0052CC;
  border-bottom-color: #0052CC;
}

.badge {
  background-color: #DFE1E6;
  color: #172B4D;
  padding: 2px 6px;
  border-radius: 12px;
  font-size: 11px;
  margin-left: 4px;
}

.tab-btn.active .badge {
  background-color: #DEEBFF;
  color: #0052CC;
}

/* Tab Content */
.tab-content {
  padding-top: 16px;
  min-height: 150px;
}

.section-label {
  font-size: 11px;
  color: #6B778C;
  text-transform: uppercase;
  margin: 0 0 8px 0;
  font-weight: 700;
}

.access-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 0;
}

.access-icon {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
}

.access-icon.open {
  background-color: #FAFBFC;
  border: 1px solid #DFE1E6;
  color: #42526E;
}

.user-avatar-current {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background-color: #0052CC;
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  font-weight: 500;
}

.access-info {
  flex: 1;
}

.access-title {
  font-size: 14px;
  font-weight: 500;
  color: #172B4D;
}

.access-desc {
  font-size: 12px;
  color: #5E6C84;
  margin-top: 2px;
}

.role-select-simple {
  border: none;
  background: transparent;
  color: #42526E;
  font-size: 14px;
  cursor: pointer;
  outline: none;
}

.text-muted {
  color: #5E6C84;
  font-size: 14px;
}

.mt-16 { margin-top: 16px; }

/* Empty State */
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 24px 0;
  text-align: center;
}

.empty-illustration {
  position: relative;
  width: 100px;
  height: 100px;
  margin-bottom: 16px;
}

.avatar-circle {
  position: absolute;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background-color: #EBECF0;
  color: #A5ADBA;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  border: 2px solid #FFFFFF;
}

.avatar-circle.top-left { top: 0; left: 10px; }
.avatar-circle.top-right { top: 10px; right: 0; }
.avatar-circle.bottom-left { bottom: 10px; left: 0; }
.avatar-circle.bottom-right { 
  bottom: 0; right: 10px; 
  background-color: #0052CC;
  color: white;
}

.empty-state h3 {
  margin: 0 0 8px 0;
  font-size: 16px;
  color: #172B4D;
  font-weight: 600;
}

.empty-state p {
  margin: 0;
  font-size: 14px;
  color: #5E6C84;
  max-width: 250px;
}
</style>
