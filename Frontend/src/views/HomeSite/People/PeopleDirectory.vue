<template>
  <AppPageLayout fluid>
    <template #header>
      <AppPageHeader title="Mọi người">
        <template #actions>
          <button class="primary-btn" @click="openInviteModal">Mời mọi người</button>
        </template>
      </AppPageHeader>
      
      <div class="tabs-nav">
        <router-link to="/home/teams" class="tab-link" exact-active-class="active">Dành cho bạn</router-link>
        <router-link to="/home/teams/list" class="tab-link" active-class="active">Tất cả các đội ngũ</router-link>
        <router-link to="/home/teams/kudos" class="tab-link" active-class="active">Khen ngợi</router-link>
        <router-link to="/home/people" class="tab-link" active-class="active">Mọi người</router-link>
      </div>
    </template>

    <div class="module-content">
      <AppToolbar>
        <template #search>
          <AppSearchInput v-model="searchQuery" placeholder="Tìm kiếm người" />
        </template>
        <template #filters>
          <DropdownFilter label="Dự án" :options="projectOptions" v-model="filters.projectId" />
          <DropdownFilter label="Mục tiêu" :options="goalOptions" v-model="filters.goalId" />
          <DropdownFilter label="Nhóm" :options="teamOptions" v-model="filters.teamId" />
          <DropdownFilter label="Người quản lý" :options="managerOptions" v-model="filters.managerId" />
          <DropdownFilter label="Chức danh" :options="jobTitleOptions" v-model="filters.jobTitle" />
          <DropdownFilter label="Phòng ban" :options="departmentOptions" v-model="filters.department" />
          <DropdownFilter label="Vị trí" :options="locationOptions" v-model="filters.location" />
          <button v-if="hasActiveFilters" class="filter-chip" @click="clearFilters">Xóa lọc</button>
        </template>
      </AppToolbar>

      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; margin-top: 24px;">
        <div style="font-size: 14px; font-weight: 600; color: #172B4D;">{{ filteredUsers.length }} người</div>
        
        <div class="view-toggle" style="display: flex; border: 1px solid #DFE1E6; border-radius: 3px; overflow: hidden; height: 32px;">
          <button class="icon-btn" :style="{ background: viewMode === 'grid' ? '#DEEBFF' : 'white', color: viewMode === 'grid' ? '#0052CC' : '#6B778C' }" @click="viewMode = 'grid'" style="border: none; border-radius: 0; padding: 0 12px; height: 100%; display: flex; align-items: center; justify-content: center;"><i class="fa-solid fa-table-cells-large"></i></button>
          <div style="width: 1px; background-color: #DFE1E6; height: 100%;"></div>
          <button class="icon-btn" :style="{ background: viewMode === 'table' ? '#DEEBFF' : 'white', color: viewMode === 'table' ? '#0052CC' : '#6B778C' }" @click="viewMode = 'table'" style="border: none; border-radius: 0; padding: 0 12px; height: 100%; display: flex; align-items: center; justify-content: center;"><i class="fa-solid fa-list"></i></button>
          <div style="width: 1px; background-color: #DFE1E6; height: 100%;"></div>
          <button class="icon-btn" style="border: none; border-radius: 0; background: white; color: #6B778C; padding: 0 12px; height: 100%; display: flex; align-items: center; justify-content: center;"><i class="fa-solid fa-ellipsis"></i></button>
        </div>
      </div>

      <div class="table-container" v-if="!isLoading">
        <!-- Grid View -->
        <div v-if="viewMode === 'grid' && filteredUsers.length > 0" style="display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 16px;">
          <div class="people-card" v-for="user in filteredUsers" :key="user.id" @click="goToProfile(user.id)" style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 16px; text-align: center; cursor: pointer; transition: background 0.2s, box-shadow 0.2s; display: flex; flex-direction: column; align-items: center;" onmouseover="this.style.boxShadow='0 4px 8px rgba(9, 30, 66, 0.15)'" onmouseout="this.style.boxShadow='none'">
            <AppAvatar :src="user.avatarUrl" :name="user.fullName" :email="user.email" size="lg" style="margin-bottom: 12px;" />
            <div style="font-size: 14px; font-weight: 500; color: #172B4D; margin-bottom: 4px;">{{ user.fullName }}</div>
            <div v-if="user.email && user.email.includes('@')" style="font-size: 12px; color: #6B778C;">{{ user.email }}</div>
            <div v-if="!user.email || !user.email.includes('@')" style="font-size: 12px; color: #6B778C;">
               <div v-if="user.email">{{ user.email }}</div>
               <div v-if="user.location">{{ user.location }}</div>
            </div>
          </div>
        </div>

        <!-- Table View -->
        <table class="jira-table" v-if="viewMode === 'table' && filteredUsers.length > 0">
          <thead>
            <tr>
              <th class="col-name">Tên</th>
              <th>Chức danh nghề nghiệp</th>
              <th>Phòng ban</th>
              <th>Vị trí</th>
              <th>Thông tin liên hệ</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="user in filteredUsers" :key="user.id" @click="goToProfile(user.id)">
              <td>
                <div class="user-cell">
                  <AppUserChip :src="user.avatarUrl" :name="user.fullName" :email="user.email" />
                </div>
              </td>
              <td>{{ user.position || '-' }}</td>
              <td>{{ user.department || '-' }}</td>
              <td>{{ user.location || '-' }}</td>
              <td>{{ user.email || '-' }}</td>
            </tr>
          </tbody>
        </table>
        
        
        <AppEmptyState 
          v-if="filteredUsers.length === 0"
          icon="fa-solid fa-user-group"
          title="Không tìm thấy ai"
          description="Chúng tôi không tìm thấy ai khớp với tiêu chí tìm kiếm của bạn."
        />
      </div>
      
      <div class="loading-state" v-else>
        <div class="loader-spinner"></div>
      </div>
      
      <div class="error-state" v-if="error">
        <p style="color: #DE350B; text-align: center; margin-top: 24px;">Đã xảy ra lỗi khi tải danh sách người dùng. Vui lòng thử lại.</p>
      </div>
    </div>

    <!-- Invite Modal -->
    <AppModal
      v-model="isInviteModalOpen"
      title="Mời mọi người tham gia SprintA"
      width="500px"
      :confirmText="isSending ? 'Đang gửi...' : 'Gửi lời mời'"
      cancelText="Hủy"
      :loading="isSending"
      @confirm="sendInvites"
      @cancel="closeInviteModal"
    >
      <p class="invite-description" style="margin: 0 0 16px 0; font-size: 14px; color: #42526E; line-height: 1.5;">
        Mời thành viên mới vào Không gian làm việc của bạn qua email. Họ sẽ nhận được một email chứa liên kết để tham gia.
      </p>
      
      <AppFormField label="Địa chỉ email" helpText="Nhập email và nhấn Enter hoặc Dấu phẩy để thêm nhiều người">
        <div class="email-input-container" @click="focusEmailInput">
          <div class="email-chip" v-for="(email, index) in inviteEmails" :key="index">
            {{ email }}
            <i class="fa-solid fa-xmark remove-chip" @click.stop="removeEmail(index)"></i>
          </div>
          <input 
            type="text" 
            v-model="emailInput" 
            placeholder="Ví dụ: name@example.com" 
            @keydown.enter.prevent="addEmail"
            @keydown.space.prevent="addEmail"
            @keydown="handleEmailKeydown"
            @blur="addEmail"
            ref="emailInputRef"
          />
        </div>
      </AppFormField>
      
      <div class="success-message" v-if="inviteSuccess" style="margin-top: 16px; padding: 12px 16px; background-color: #E3FCEF; color: #006644; border-radius: 3px; display: flex; align-items: center; gap: 8px; font-size: 14px; font-weight: 500;">
        <i class="fa-solid fa-circle-check"></i> Đã gửi lời mời thành công!
      </div>
    </AppModal>
  </AppPageLayout>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { usePeopleStore } from '@/store/usePeopleStore'
import { useGoalStore } from '@/store/useGoalStore'
import { useHomeProjectStore } from '@/store/useHomeProjectStore'
import { useTeamStore } from '@/store/useTeamStore'
import debounce from 'lodash/debounce'
import DropdownFilter from '@/components/common/DropdownFilter.vue'
import axiosClient from '@/api/axiosClient'
import { AppPageLayout, AppPageHeader, AppToolbar, AppSearchInput, AppEmptyState, AppAvatar, AppUserChip, AppModal, AppFormField } from '@/components/common/Foundation'

const router = useRouter()
const peopleStore = usePeopleStore()
const goalStore = useGoalStore()
const projectStore = useHomeProjectStore()
const teamStore = useTeamStore()

const searchQuery = ref('')
const viewMode = ref('grid')
const filters = ref({
  projectId: '',
  goalId: '',
  teamId: '',
  managerId: '',
  jobTitle: '',
  department: '',
  location: ''
})

// Invite Modal State
const isInviteModalOpen = ref(false)
const emailInput = ref('')
const inviteEmails = ref([])
const emailInputRef = ref(null)
const isSending = ref(false)
const inviteSuccess = ref(false)

const openInviteModal = () => {
  isInviteModalOpen.value = true
  inviteEmails.value = []
  emailInput.value = ''
  inviteSuccess.value = false
}

const closeInviteModal = () => {
  isInviteModalOpen.value = false
}

const focusEmailInput = () => {
  emailInputRef.value?.focus()
}

const addEmail = () => {
  const emails = emailInput.value.split(/[\s,]+/).filter(e => e.trim() !== '')
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
  
  emails.forEach(email => {
    if (emailRegex.test(email) && !inviteEmails.value.includes(email)) {
      inviteEmails.value.push(email)
    }
  })
  emailInput.value = ''
}

const handleEmailKeydown = (event) => {
  if (event.key !== ',') return
  event.preventDefault()
  addEmail()
}

const removeEmail = (index) => {
  inviteEmails.value.splice(index, 1)
}

const sendInvites = async () => {
  addEmail() // add whatever is currently in the input
  if (inviteEmails.value.length === 0) return
  
  isSending.value = true
  inviteSuccess.value = false
  
  try {
    await Promise.all(inviteEmails.value.map(email => axiosClient.post('/admin/users', {
      email,
      role: 'Developer',
      projectRole: 'DEV',
      inviteMessage: ''
    })))
    inviteSuccess.value = true
    
    // Auto close after 2 seconds
    setTimeout(() => {
      if (inviteSuccess.value) {
        closeInviteModal()
      }
    }, 2000)
  } catch (error) {
    console.error('Failed to send invites:', error)
  } finally {
    isSending.value = false
  }
}

const cleanFilters = () => Object.fromEntries(
  Object.entries(filters.value).filter(([, value]) => value)
)

const hasActiveFilters = computed(() => Object.values(filters.value).some(Boolean))

const fetchPeople = async () => {
  await peopleStore.fetchPeople(searchQuery.value, 1, 20, cleanFilters())
}

onMounted(async () => {
  await Promise.all([
    fetchPeople(),
    goalStore.fetchGoals(),
    projectStore.fetchProjects(),
    teamStore.fetchAllTeams()
  ])
})

const isLoading = computed(() => peopleStore.isLoading)
const error = computed(() => peopleStore.error)
const goalOptions = computed(() => goalStore.goals || [])
const projectOptions = computed(() => projectStore.projects || [])
const teamOptions = computed(() => teamStore.allTeams || [])
const managerOptions = computed(() => (peopleStore.users || []).filter(user =>
  (peopleStore.users || []).some(member =>
    (member.departments || []).some(department => department.managerId === user.id)
  )
))

const uniqueValues = (selector) => Array.from(new Set(
  (peopleStore.users || [])
    .map(selector)
    .filter(value => value && value !== 'N/A')
)).sort()

const jobTitleOptions = computed(() => uniqueValues(user => user.position))
const departmentOptions = computed(() => uniqueValues(user => user.department))
const locationOptions = computed(() => uniqueValues(user => user.location))

const filteredUsers = computed(() => {
  return (peopleStore.users || []).map(u => {
    let initial = 'U';
    if (u.email) {
      initial = u.email.charAt(0).toUpperCase();
    } else if (u.fullName) {
      initial = u.fullName.charAt(0).toUpperCase();
    }
    return {
      ...u,
      avatar: u.avatar || initial
    }
  })
})

const handleSearch = debounce(async () => {
  await fetchPeople()
}, 500)

watch(searchQuery, () => {
  handleSearch()
})

watch(filters, () => {
  fetchPeople()
}, { deep: true })

const clearFilters = () => {
  filters.value = {
    projectId: '',
    goalId: '',
    teamId: '',
    managerId: '',
    jobTitle: '',
    department: '',
    location: ''
  }
}

const goToProfile = (id) => {
  router.push(`/home/profile/${id}`)
}
</script>

<style scoped>
.people-wrapper {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
  background-color: #FFFFFF;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
}

.module-header {
  padding: 32px 40px 0;
  background-color: #FFFFFF;
}

.header-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.header-content h1 {
  font-size: 24px;
  font-weight: 500;
  color: #172B4D;
  margin: 0;
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

.tabs-nav {
  display: flex;
  border-bottom: 2px solid #dfe1e6;
  gap: 24px;
}

.tab-link {
  padding: 8px 0 12px;
  font-size: 14px;
  font-weight: 500;
  color: #5e6c84;
  text-decoration: none;
  position: relative;
  margin-bottom: -2px;
  border-bottom: 2px solid transparent;
  transition: color 0.2s;
}

.tab-link:hover {
  color: #172b4d;
}

.tab-link.active {
  color: #0052cc;
  border-bottom-color: #0052cc;
}

.module-content {
  padding: 32px 40px 40px;
  flex: 1;
}

.list-controls {
  margin-bottom: 0px;
}

.filter-chip {
  background: white;
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  padding: 6px 12px;
  font-size: 13px;
  font-weight: 500;
  color: #42526E;
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  transition: background 0.2s;
}

.filter-chip:hover {
  background: #FAFBFC;
}

.search-box-wrapper {
  position: relative;
  width: 250px;
}

.search-icon {
  position: absolute;
  left: 10px;
  top: 50%;
  transform: translateY(-50%);
  color: #5E6C84;
  font-size: 14px;
}

.search-input {
  width: 100%;
  padding: 8px 12px 8px 44px;
  border: 2px solid #DFE1E6;
  border-radius: 3px;
  font-size: 14px;
  color: #172B4D;
  outline: none;
  transition: border-color 0.2s, background-color 0.2s;
  box-sizing: border-box;
}

.search-input:hover {
  background-color: #FAFBFC;
}

.search-input:focus {
  background-color: #FFFFFF;
  border-color: #4C9AFF;
}

.jira-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
}

.jira-table th {
  padding: 8px 12px;
  font-size: 12px;
  font-weight: 600;
  color: #5E6C84;
  border-bottom: 2px solid #DFE1E6;
}

.jira-table th:hover {
  background-color: #FAFBFC;
  cursor: pointer;
}

.col-name {
  width: 25%;
}

.jira-table td {
  padding: 12px;
  font-size: 14px;
  color: #172B4D;
  border-bottom: 1px solid #DFE1E6;
  cursor: pointer;
}

.jira-table tbody tr:hover td {
  background-color: #FAFBFC;
}

.user-cell {
  display: flex;
  align-items: center;
  gap: 12px;
}

.user-avatar {
  width: 24px;
  height: 24px;
  background-color: #0052CC;
  color: white;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: bold;
}

.user-info-stack {
  display: flex;
  flex-direction: column;
}

.user-name {
  font-weight: 500;
  color: #0052CC;
}

.jira-table tbody tr:hover .user-name {
  text-decoration: underline;
}

.empty-state {
  text-align: center;
  padding: 64px 20px;
}

.empty-icon-wrapper {
  width: 80px;
  height: 80px;
  background-color: #E6FCFF;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0 auto 16px;
  color: #0052CC;
  font-size: 32px;
}

.empty-state h3 {
  margin: 0 0 8px 0;
  color: #172B4D;
  font-size: 20px;
}

.empty-state p {
  margin: 0;
  color: #5E6C84;
}

.loading-state {
  display: flex;
  justify-content: center;
  padding: 64px;
}

.loader-spinner {
  width: 32px;
  height: 32px;
  border: 3px solid #DFE1E6;
  border-top-color: #0052CC;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
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

.invite-description {
  margin: 0 0 16px 0;
  font-size: 14px;
  color: #42526E;
  line-height: 1.5;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form-group label {
  font-size: 12px;
  font-weight: 600;
  color: #5E6C84;
}

.email-input-container {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px;
  padding: 4px;
  border: 2px solid #DFE1E6;
  border-radius: 3px;
  min-height: 40px;
  box-sizing: border-box;
  cursor: text;
  transition: border-color 0.2s;
}

.email-input-container:focus-within {
  border-color: #4C9AFF;
}

.email-input-container input {
  border: none !important;
  outline: none !important;
  flex: 1;
  min-width: 150px;
  padding: 4px 8px !important;
  font-size: 14px;
  color: #172B4D;
}

.email-chip {
  display: flex;
  align-items: center;
  gap: 6px;
  background-color: #FAFBFC;
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  padding: 4px 8px;
  font-size: 12px;
  color: #172B4D;
}

.remove-chip {
  color: #5E6C84;
  cursor: pointer;
  font-size: 10px;
}

.remove-chip:hover {
  color: #DE350B;
}

.helper-text {
  font-size: 11px;
  color: #5E6C84;
}

.success-message {
  margin-top: 16px;
  padding: 12px 16px;
  background-color: #E3FCEF;
  color: #006644;
  border-radius: 3px;
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 500;
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

.cancel-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
}
</style>
