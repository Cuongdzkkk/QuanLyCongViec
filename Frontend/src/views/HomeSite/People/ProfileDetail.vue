<template>
  <template v-if="user">
    <DetailLayout>
    <template #hero>
      <DetailHero
        cover-color="#091E42"
        cover-pattern="dynamic"
        back-text="Quay lại"
        :title="user.fullName"
        avatar-type="circle"
        @back="handleBack"
      >
        <template #cover-actions>
          <input type="file" ref="imageUploader" style="display: none" accept="image/*" @change="onImageSelected" />
          <button class="sprinta-btn sprinta-btn-secondary" style="background: rgba(0,0,0,0.4); color: white; border: 1px solid rgba(255,255,255,0.2); backdrop-filter: blur(4px);" v-if="!isInactive" @click="triggerImageUpload">
            <i class="fa-solid fa-camera"></i> Đổi ảnh bìa

          </button>
        </template>
        <template #avatar>
          <div class="editable-avatar-wrapper" style="position: relative; width: 100%; height: 100%; cursor: pointer; border-radius: 50%; overflow: hidden;" @click="triggerImageUpload">
            <UserAvatar :user="{ id: user.id, avatarUrl: user.avatarUrl, avatarColor: user.avatarColor, initials: user.initials, fullName: user.fullName, email: user.email }" :size="96" :fontSize="36" :class="{ inactive: isInactive }" />
            <div class="avatar-edit-overlay" v-if="!isInactive" style="position: absolute; bottom: 0; left: 0; width: 100%; height: 32px; background: rgba(0,0,0,0.5); display: flex; align-items: center; justify-content: center; color: white; opacity: 0; transition: opacity 0.2s;">
              <i class="fa-solid fa-camera" style="font-size: 14px;"></i>
            </div>
          </div>
        </template>
        <template #badges>
          <span v-if="isInactive" class="badge inactive">Inactive Account</span>
        </template>
        <template #actions>
          <button class="sprinta-btn sprinta-btn-primary" @click="handleFriendRequest" :disabled="isInactive">
            <i class="fa-solid fa-user-plus" style="margin-right:6px"></i> Gửi lời mời kết bạn
          </button>
          <button class="sprinta-icon-btn" title="Nhắn tin" @click="openChat" :disabled="isInactive">
            <i class="fa-regular fa-message"></i>
          </button>
        </template>
      </DetailHero>
    </template>

    <template #tabs>
      <button class="sprinta-tab-btn" :class="{ active: currentTab === 'overview' }" @click="currentTab = 'overview'">Tổng quan</button>
      <button class="sprinta-tab-btn" :class="{ active: currentTab === 'tasks' }" @click="currentTab = 'tasks'">SprintA</button>
      <button class="sprinta-tab-btn" :class="{ active: currentTab === 'goals' }" @click="currentTab = 'goals'">Mục tiêu</button>
      <button class="sprinta-tab-btn" :class="{ active: currentTab === 'projects' }" @click="currentTab = 'projects'">Dự án</button>
      <button class="sprinta-tab-btn" :class="{ active: currentTab === 'kudos' }" @click="currentTab = 'kudos'">Khen ngợi</button>
      <button class="sprinta-tab-btn" :class="{ active: currentTab === 'history' }" @click="currentTab = 'history'">Lịch sử</button>
    </template>

    <template #main>
      <div :class="{ 'read-only-state': isInactive }">
        <div v-if="isInactive" class="inactive-banner" style="margin-bottom: 24px;">
          <i class="fa-solid fa-circle-info" style="margin-right:8px; color: #0052cc"></i>
          This account is inactive. Some information might be hidden and actions are disabled.
        </div>

        <!-- Overview -->
        <div v-if="currentTab === 'overview'" class="tab-pane" style="display: flex; flex-direction: column; gap: 18px;">
          <!-- Bio -->
          <section class="info-section" style="display: flex; flex-direction: column; gap: 8px;">
            <div class="section-header" style="display: flex; justify-content: space-between; align-items: center;">
              <h3 style="margin: 0; font-size: 16px; font-weight: 600; color: #172B4D;">Bio</h3>
            </div>
            <div class="section-body">
              <RichTextEditor v-if="editingBio && !isInactive" v-model="tempBio" @save="saveBio" @cancel="editingBio = false" placeholder="Thêm giới thiệu về bạn..." />
              <div v-else @click="!isInactive && startEditingBio()" :style="{ cursor: !isInactive ? 'pointer' : 'default', color: '#5E6C84', fontSize: '14px', padding: '8px', borderRadius: '3px', minHeight: '40px' }" :onmouseover="!isInactive ? 'this.style.backgroundColor=\'#FAFBFC\'' : ''" :onmouseout="!isInactive ? 'this.style.backgroundColor=\'transparent\'' : ''">
                <div v-if="user.bio && user.bio !== '<p></p>' && user.bio !== '<p class=\'empty-state-micro\'>Thêm giới thiệu về bạn...</p>'" v-html="user.bio" class="tiptap-content" style="color: #172B4D;"></div>
                <div v-else>Thêm giới thiệu về bạn...</div>
              </div>
            </div>
          </section>

          <!-- Hobbies -->
          <section class="info-section" style="display: flex; flex-direction: column; gap: 8px;">
            <div class="section-header" style="display: flex; justify-content: space-between; align-items: center;">
              <h3 style="margin: 0; font-size: 16px; font-weight: 600; color: #172B4D;">Hobbies & Interests</h3>
            </div>
            <div class="section-body">
              <RichTextEditor v-if="editingHobbies && !isInactive" v-model="tempHobbies" @save="saveHobbies" @cancel="editingHobbies = false" placeholder="Sở thích của bạn là gì?" />
              <div v-else @click="!isInactive && startEditingHobbies()" :style="{ cursor: !isInactive ? 'pointer' : 'default', color: '#5E6C84', fontSize: '14px', padding: '8px', borderRadius: '3px', minHeight: '40px' }" :onmouseover="!isInactive ? 'this.style.backgroundColor=\'#FAFBFC\'' : ''" :onmouseout="!isInactive ? 'this.style.backgroundColor=\'transparent\'' : ''">
                <div v-if="user.hobbies && user.hobbies !== '<p></p>' && user.hobbies !== '<p class=\'empty-state-micro\'>Has not shared any hobbies yet.</p>'" v-html="user.hobbies" class="tiptap-content" style="color: #172B4D;"></div>
                <div v-else>Has not shared any hobbies yet.</div>
              </div>
            </div>
          </section>

          <!-- Teams & Departments -->
          <section class="info-section" style="display: flex; flex-direction: column; gap: 8px;">
            <div class="section-header" style="display: flex; justify-content: space-between; align-items: center;">
              <h3 style="margin: 0; font-size: 16px; font-weight: 600; color: #172B4D;">Teams & Departments</h3>
            </div>
            <div class="section-body" style="padding: 8px;">
              <div class="teams-list" v-if="user.teamsList && user.teamsList.length">
                <div class="team-chip" v-for="team in user.teamsList" :key="team.id">
                  <i class="fa-solid fa-users team-icon"></i>
                  <span>{{ team.name }}</span>
                </div>
              </div>
              <div v-else style="color: #5E6C84; font-size: 14px;">Not a member of any teams.</div>
            </div>
          </section>
        </div>

        <!-- Tasks -->
        <div v-if="currentTab === 'tasks'" class="tab-pane">
          <div class="section-header-row">
            <h3>Công việc SprintA</h3>
          </div>
          <table class="jira-table mt-16" v-if="assignedTasks && assignedTasks.length">
            <thead>
              <tr><th>Mã CV</th><th>Tiêu đề</th><th>Trạng thái</th></tr>
            </thead>
            <tbody>
              <tr v-for="task in assignedTasks" :key="task.id" @click="goToTask(task)">
                <td class="key-col">{{ task.key }}</td>
                <td class="link-text">{{ task.title }}</td>
                <td><AppStatusBadge :status="task.status" :statusText="task.status" /></td>
              </tr>
            </tbody>
          </table>
          <div v-else style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 24px; display: flex; align-items: flex-start; gap: 24px; background: white;">
             <div style="position: relative;">
                <div style="width: 80px; height: 80px; background-color: #EBECF0; border-radius: 8px; display: flex; align-items: center; justify-content: center; transform: rotate(-5deg);">
                   <i class="fa-brands fa-jira" style="font-size: 32px; color: #0052CC;"></i>
                </div>
                <div v-if="!isInactive" style="position: absolute; bottom: -8px; right: -8px; width: 32px; height: 32px; background-color: #0052CC; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; cursor: pointer; box-shadow: 0 2px 4px rgba(0,0,0,0.1);">
                   <i class="fa-solid fa-plus" style="font-size: 16px;"></i>
                </div>
             </div>
             <div style="flex: 1; position: relative;">
                <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Công việc SprintA</h4>
                <p style="font-size: 13px; color: #6B778C; margin-bottom: 16px; line-height: 1.5;">Người dùng này hiện không có công việc nào đang được giao.</p>
                <div style="position: relative; display: inline-block;">
                  <button v-if="!isInactive" class="secondary-btn">Thêm hạng mục công việc SprintA</button>
                </div>
             </div>
          </div>
        </div>

        <!-- Goals -->
        <div v-if="currentTab === 'goals'" class="tab-pane">
          <div class="section-header-row">
            <h3>Mục tiêu sở hữu</h3>
          </div>
          <table class="jira-table mt-16" v-if="linkedGoals && linkedGoals.length">
            <thead>
              <tr><th>Tiêu đề mục tiêu</th><th>Trạng thái</th></tr>
            </thead>
            <tbody>
              <tr v-for="goal in linkedGoals" :key="goal.id" @click="goToGoal(goal.id)">
                <td class="link-text"><i class="fa-solid fa-bullseye"></i> {{ goal.title }}</td>
                <td><AppStatusBadge :status="goal.status" :statusText="goal.status" /></td>
              </tr>
            </tbody>
          </table>
          <div v-else class="jira-empty-box" style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 24px; display: flex; align-items: flex-start; gap: 24px;">
             <div style="position: relative;">
                <div style="width: 80px; height: 80px; background-color: #EBECF0; border-radius: 8px; display: flex; align-items: center; justify-content: center; transform: rotate(-5deg);">
                   <i class="fa-solid fa-bullseye" style="font-size: 32px; color: #172B4D;"></i>
                </div>
                <div style="position: absolute; bottom: -8px; right: -8px; width: 32px; height: 32px; background-color: #0052CC; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; cursor: pointer; box-shadow: 0 2px 4px rgba(0,0,0,0.1);">
                   <i class="fa-solid fa-plus" style="font-size: 16px;"></i>
                </div>
             </div>
             <div style="flex: 1; position: relative;">
                <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Không có mục tiêu nào</h4>
                <p style="font-size: 13px; color: #6B778C; margin-bottom: 16px; line-height: 1.5;">Chưa có mục tiêu nào được liên kết với người dùng này.</p>
                <div style="position: relative; display: inline-block;">
                  <button class="secondary-btn">Thêm mục tiêu</button>
                </div>
             </div>
          </div>
        </div>

        <!-- Projects -->
        <div v-if="currentTab === 'projects'" class="tab-pane">
          <div class="section-header-row">
            <h3>Dự án tham gia</h3>
          </div>
          <table class="jira-table mt-16" v-if="linkedProjects && linkedProjects.length">
            <thead>
              <tr><th>Tên dự án</th><th>Trạng thái</th></tr>
            </thead>
            <tbody>
              <tr v-for="proj in linkedProjects" :key="proj.id" @click="goToProject(proj.id)">
                <td class="link-text"><i class="fa-solid fa-chart-simple"></i> {{ proj.title }}</td>
                <td><span class="badge status-light">{{ proj.status }}</span></td>
              </tr>
            </tbody>
          </table>
          <div v-else class="jira-empty-box" style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 24px; display: flex; align-items: flex-start; gap: 24px;">
             <div style="position: relative;">
                <div style="width: 80px; height: 80px; background-color: #EBECF0; border-radius: 8px; display: flex; align-items: center; justify-content: center; transform: rotate(-5deg);">
                   <i class="fa-solid fa-folder" style="font-size: 32px; color: #172B4D;"></i>
                </div>
                <div style="position: absolute; bottom: -8px; right: -8px; width: 32px; height: 32px; background-color: #0052CC; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; cursor: pointer; box-shadow: 0 2px 4px rgba(0,0,0,0.1);">
                   <i class="fa-solid fa-plus" style="font-size: 16px;"></i>
                </div>
             </div>
             <div style="flex: 1; position: relative;">
                <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Không có dự án nào</h4>
                <p style="font-size: 13px; color: #6B778C; margin-bottom: 16px; line-height: 1.5;">Chưa có dự án nào được liên kết với người dùng này.</p>
                <div style="position: relative; display: inline-block;">
                  <button class="secondary-btn">Thêm dự án</button>
                </div>
             </div>
          </div>
        </div>

        <!-- Kudos -->
        <div v-if="currentTab === 'kudos'" class="tab-pane">
          <div class="section-header-row" style="position: relative;">
            <h3>Lời khen đã nhận</h3>
            <button class="secondary-btn" :disabled="isInactive" @click="handleGiveKudos" style="position: absolute; right: 0; top: -6px;">Tặng lời khen</button>
          </div>
          <div class="kudos-grid mt-16" v-if="kudos && kudos.length">
            <div class="kudos-card" v-for="k in kudos" :key="k.id">
              <div class="kudos-icon">{{ k.icon || 'Star' }}</div>
              <div class="kudos-content">
                <p class="kudos-msg">"{{ k.message }}"</p>
                <div class="kudos-meta">
                  <span class="kudos-sender">Từ {{ k.sender }}</span> &bull; <span class="kudos-date">{{ k.date }}</span>
                </div>
              </div>
            </div>
          </div>
          <div v-else class="jira-empty-box" style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 24px; display: flex; align-items: flex-start; gap: 24px;">
             <div style="position: relative;">
                <div style="width: 80px; height: 80px; background-color: #FFFAE6; border-radius: 8px; display: flex; align-items: center; justify-content: center; transform: rotate(-5deg);">
                   <i class="fa-solid fa-medal" style="font-size: 32px; color: #FFAB00;"></i>
                </div>
                <div style="position: absolute; bottom: -8px; right: -8px; width: 32px; height: 32px; background-color: #FFAB00; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; cursor: pointer; box-shadow: 0 2px 4px rgba(0,0,0,0.1);" @click="handleGiveKudos">
                   <i class="fa-solid fa-plus" style="font-size: 16px;"></i>
                </div>
             </div>
             <div style="flex: 1; position: relative;">
                <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Chưa có lời khen nào</h4>
                <p style="font-size: 13px; color: #6B778C; margin-bottom: 16px; line-height: 1.5;">Người dùng này chưa nhận được lời khen ngợi nào.</p>
                <div style="position: relative; display: inline-block;">
                  <button class="secondary-btn" @click="handleGiveKudos">Thêm khen ngợi</button>
                </div>
             </div>
          </div>
        </div>

        <!-- History -->
        <div v-if="currentTab === 'history'" class="tab-pane">
          <div class="section-header-row">
            <h3>Lịch sử hoạt động</h3>
          </div>
          <table class="jira-table mt-16" v-if="history && history.length">
            <thead>
              <tr><th>Thời gian</th><th>Hành động</th></tr>
            </thead>
            <tbody>
              <tr v-for="log in history" :key="log.id">
                <td class="time-col">{{ log.time }}</td>
                <td>{{ log.action }}</td>
              </tr>
            </tbody>
          </table>
          <div v-else class="jira-empty-box" style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 24px; display: flex; align-items: flex-start; gap: 24px;">
             <div style="position: relative;">
                <div style="width: 80px; height: 80px; background-color: #EBECF0; border-radius: 8px; display: flex; align-items: center; justify-content: center; transform: rotate(-5deg);">
                   <i class="fa-solid fa-clock-rotate-left" style="font-size: 32px; color: #172B4D;"></i>
                </div>
                <div style="position: absolute; bottom: -8px; right: -8px; width: 32px; height: 32px; background-color: #0052CC; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; cursor: pointer; box-shadow: 0 2px 4px rgba(0,0,0,0.1);">
                   <i class="fa-solid fa-plus" style="font-size: 16px;"></i>
                </div>
             </div>
             <div style="flex: 1; position: relative;">
                <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Chưa có lịch sử hoạt động</h4>
                <p style="font-size: 13px; color: #6B778C; margin-bottom: 16px; line-height: 1.5;">Hoạt động của người dùng sẽ được ghi nhận và hiển thị tại đây.</p>
                <div style="position: relative; display: inline-block;">
                  <button class="secondary-btn">Thêm hoạt động</button>
                </div>
             </div>
          </div>
        </div>
      </div>
    </template>

    <template #sidebar>
      <div class="side-column">
        <div class="sidebar-card">
          <div class="sidebar-card-header">
            <h3>Chi tiết</h3>
          </div>
          <div class="details-body">
            <div class="detail-row">
              <div class="detail-label">Họ và tên</div>
              <div class="detail-value">
                <span>{{ user.fullName }}</span>
              </div>
            </div>
            <div class="detail-row">
              <div class="detail-label">Email</div>
              <div class="detail-value">
                <span>{{ user.email }}</span>
              </div>
            </div>
            <div class="detail-row">
              <div class="detail-label">Phòng ban</div>
              <div class="detail-value">
                <span>{{ user.department || 'N/A' }}</span>
              </div>
            </div>
            <div class="detail-row">
              <div class="detail-label">Chức vụ</div>
              <div class="detail-value">
                <span>{{ user.position || 'N/A' }}</span>
              </div>
            </div>
            <div class="detail-row">
              <div class="detail-label">Đội ngũ</div>
              <div class="detail-value">
                <span>{{ user.team || 'N/A' }}</span>
              </div>
            </div>
            <div class="detail-row">
              <div class="detail-label">Vị trí làm việc</div>
              <div class="detail-value">
                <span>{{ user.location || 'N/A' }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>
  </DetailLayout>

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

  <div v-else class="loading-state">
    <div class="loader-spinner"></div>
    <p>Loading profile...</p>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { getStoredUser } from '@/utils/permissions'
import { useRoute, useRouter } from 'vue-router'
import { usePeopleStore } from '@/store/usePeopleStore'
import UserAvatar from '@/components/common/UserAvatar.vue'
import RichTextEditor from '@/components/common/RichTextEditor.vue'
import AppEmptyState from '@/components/common/Foundation/AppEmptyState.vue'
import AppStatusBadge from '@/components/common/Foundation/AppStatusBadge.vue'
import { useGoalStore } from '@/store/useGoalStore'
import { useHomeProjectStore as useProjectStore } from '@/store/useHomeProjectStore'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import DataModalField from '@/components/common/Foundation/DataModalField.vue'
import DetailLayout from '@/components/common/Detail/DetailLayout.vue'
import DetailHero from '@/components/common/Detail/DetailHero.vue'

const imageUploader = ref(null)

const triggerImageUpload = () => {
  if (imageUploader.value) {
    imageUploader.value.click()
  }
}

const onImageSelected = (event) => {
  const file = event.target.files[0]
  if (file) {
    console.log('User selected file for upload:', file.name)
    // Handle image upload logic here when backend is ready
  }
}

const route = useRoute()
const router = useRouter()
const peopleStore = usePeopleStore()
const goalStore = useGoalStore()
const projectStore = useProjectStore()
const profileUserId = computed(() => String(route.params.profileId || route.params.id || '').trim())


const editingBio = ref(false)
const tempBio = ref('')
const startEditingBio = () => {
  let b = user.value.bio || ''
  if (b.includes('Thêm giới thiệu về bạn...')) b = ''
  tempBio.value = b
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
  let h = user.value.hobbies || ''
  if (h.includes('Has not shared any hobbies yet')) h = ''
  tempHobbies.value = h
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
    if (window.history.length > 1) {
      router.back()
    } else {
      router.push('/home/people')
    }
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
.editable-avatar-wrapper:hover .avatar-edit-overlay {
  opacity: 1 !important;
}
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

/* Sidebar Card styling */
.sidebar-card {
  background: #ffffff;
  border: 1px solid rgba(148, 163, 184, 0.15);
  border-radius: 12px;
  padding: 20px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.sidebar-card h3 {
  font-size: 13px;
  font-weight: 700;
  color: #475569;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.sidebar-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.details-body {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.detail-row {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.detail-label {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 12px;
  font-weight: 700;
  color: #64748b;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.detail-value {
  font-size: 13.5px;
  color: #334155;
}

.empty-value {
  color: #94a3b8;
  font-style: italic;
  font-size: 13px;
}

/* Section headers */
.section-header-row {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 16px;
}

.section-header-row h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #172B4D;
}

/* Tables */
.jira-table {
  width: 100%;
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
