<template>
  <AppPageLayout class="goals-dashboard-layout">
    <template #header>
      <AppPageHeader :title="labels.title" :subtitle="labels.search">
        <template #actions>
          <button class="primary-btn" @click="openCreateModal">{{ labels.createGoal }}</button>
        </template>
        <template #bottom>
          <div class="tabs-nav goals-tabs-nav">
            <button class="tab-btn" :class="{ active: currentTab === 'all' }" @click="currentTab = 'all'">{{ labels.goalDirectory }}</button>
            <button class="tab-btn" :class="{ active: currentTab === 'following' }" @click="currentTab = 'following'">{{ labels.following }}</button>
            <button class="tab-btn" :class="{ active: currentTab === 'archived' }" @click="currentTab = 'archived'">{{ labels.archived }}</button>
          </div>
        </template>
      </AppPageHeader>
    </template>

    <div class="module-content">
      <!-- Tab: Dành cho bạn -->
      <div v-if="currentTab === 'foryou'" class="tab-foryou">
        <div class="empty-state-banner">
          <div class="empty-banner-content">
            <div class="empty-banner-text">
              <h2>{{ labels.yourGoals }}</h2>
              <p>{{ labels.noAssignedGoals }}</p>
              <div class="empty-banner-actions">
                <button class="primary-btn" @click="openCreateModal">{{ labels.createGoal }}</button>
                <a href="#" class="secondary-btn">{{ labels.learnMore }}</a>
              </div>
            </div>
            <div class="empty-banner-illustration">
              <div class="empty-illustration">
                <i class="fa-solid fa-bullseye"></i>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Tab: Tất cả mục tiêu & Đã lưu trữ -->
      <div v-else class="tab-all-archived">
        <div class="section-header">
          <h2>{{ currentTabHeader }}</h2>
        </div>

        <div class="sprinta-layout-toolbar">
          <ProjectPageToolbar
            v-model:searchQuery="searchQuery"
            show-search
            :search-placeholder="labels.search"
          >
            <template #filters>
              <div class="filter-dropdown-wrapper js-toolbar-popup-scope">
                <button
                  class="timeline-filter-trigger icon-only-trigger"
                  type="button"
                  aria-label="Filters"
                  title="Bộ lọc"
                  @click="toggleFilterDropdown"
                  :class="{ active: showFilterDropdown || activeFilters.length }"
                >
                  <i class="fa-solid fa-filter"></i>
                  <span v-if="activeFilters.length" class="filter-count">{{ activeFilters.length }}</span>
                </button>
                <div class="plane-dropdown-menu filter-dropdown-menu" v-show="showFilterDropdown" @click.stop>
                  <FilterBar
                    v-model:filters="activeFilters"
                    :fields="goalFilterFields"
                    :operators="goalOperators"
                    :custom-value-meta="customGoalValueMeta"
                    :active="showFilterDropdown"
                  />
                </div>
              </div>
            </template>
            <template #sort>
              <ToolbarSortMenu v-model="goalSortMode" v-model:direction="goalSortDirection" label="Sắp xếp mục tiêu" :options="goalSortOptions" />
              <button v-if="false" class="timeline-filter-trigger icon-only-trigger" type="button" title="Sắp xếp mục tiêu" aria-label="Sắp xếp mục tiêu" @click="goalSortMode = goalSortMode === 'progress-desc' ? 'updated-desc' : 'progress-desc'">
                <i :class="goalSortMode === 'progress-desc' ? 'fa-solid fa-chart-line' : 'fa-regular fa-clock'"></i>
              </button>
            </template>
          </ProjectPageToolbar>
        </div>

        <div class="goals-list-container">
          <WorkItemsListTable
            v-if="!isLoading && filteredGoals.length > 0"
            :columns="goalTableColumns"
            :rows="filteredGoals"
            min-width="1260"
            @row-click="goal => goToGoal(goal.id)"
          >
            <template #cell-goal="{ row }">
              <div class="goal-title-cell">
                <span class="goal-icon"><i class="fa-solid fa-bullseye"></i></span>
                <span class="goal-title">{{ row.title }}</span>
              </div>
            </template>
            <template #cell-status="{ row }">
              <AppStatusBadge :status="translateStatus(row.status)" :statusText="translateStatus(row.status)" />
            </template>
            <template #cell-progress="{ row }">
              <div class="progress-cell">
                <div class="progress-bar-bg"><div class="progress-bar-fill" :style="{ width: (row.progress || 0) + '%' }"></div></div>
                <span class="progress-text">{{ row.progress || 0 }}%</span>
              </div>
            </template>
            <template #cell-createdAt="{ row }"><span class="muted-text">{{ row.createdAt ? new Date(row.createdAt).toLocaleDateString('vi-VN') : '-' }}</span></template>
            <template #cell-updatedAt="{ row }"><span class="muted-text">{{ row.updatedAt ? new Date(row.updatedAt).toLocaleDateString('vi-VN') : '-' }}</span></template>
            <template #cell-favorite="{ row }">
              <button class="goal-star-btn" type="button" :class="{ starred: isGoalStarred(row.id) }" :disabled="starredStore.isPending('Goal', row.id)" :aria-pressed="isGoalStarred(row.id)" :aria-label="isGoalStarred(row.id) ? 'Bỏ gắn sao mục tiêu' : 'Gắn sao mục tiêu'" @click.stop="toggleStar(row)">
                <i :class="isGoalStarred(row.id) ? 'fa-solid fa-star' : 'fa-regular fa-star'"></i>
              </button>
            </template>
            <template #cell-follow="{ row }">
              <span class="follow-cell" @click.stop="toggleWatch(row)">{{ row.isFollowing ? labels.following : labels.follow }}</span>
            </template>
            <template #cell-owner="{ row }">
              <div class="owner-cell">
                <AppAvatar :user="{ id: row.ownerId, fullName: row.ownerName, avatarColor: row.ownerColor, avatarUrl: row.ownerAvatarUrl }" :size="24" />
                <span class="owner-name">{{ row.owner || labels.unassigned }}</span>
              </div>
            </template>
          </WorkItemsListTable>

          <div v-else-if="false && !isLoading && filteredGoals.length > 0" class="table-container work-items-table-shell">
            <table v-resizable class="jira-table work-items-style-table goals-table">
              <thead>
                <tr>
                  <th class="col-title"><i class="fa-solid fa-bullseye"></i> {{ labels.goal }}</th>
                  <th class="col-status"><i class="fa-regular fa-circle-dot"></i> {{ labels.status }}</th>
                  <th class="col-progress"><i class="fa-solid fa-chart-line"></i> {{ labels.progress }}</th>
                  <th class="col-created"><i class="fa-regular fa-calendar"></i> {{ labels.createdDate }}</th>
                  <th class="col-updated"><i class="fa-regular fa-clock"></i> {{ labels.updatedDate }}</th>
                  <th class="col-star"><i class="fa-regular fa-star"></i> {{ labels.favorite }}</th>
                  <th class="col-watch"><i class="fa-regular fa-eye"></i> {{ labels.follow }}</th>
                  <th class="col-owner"><i class="fa-solid fa-user-tie"></i> {{ labels.owner }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="goal in filteredGoals" :key="goal.id" @click="goToGoal(goal.id)">
                  <td>
                    <div class="goal-title-cell">
                      <span class="goal-icon"><i class="fa-solid fa-bullseye"></i></span>
                      <span class="goal-title">{{ goal.title }}</span>
                    </div>
                  </td>
                  <td>
                    <AppStatusBadge :status="translateStatus(goal.status)" :statusText="translateStatus(goal.status)" />
                  </td>
                  <td>
                    <div class="progress-cell">
                      <div class="progress-bar-bg">
                        <div class="progress-bar-fill" :style="{ width: (goal.progress || 0) + '%' }"></div>
                      </div>
                      <span class="progress-text">{{ goal.progress || 0 }}%</span>
                    </div>
                  </td>
                  <td>{{ goal.createdAt ? new Date(goal.createdAt).toLocaleDateString('vi-VN') : '-' }}</td>
                  <td>{{ goal.updatedAt ? new Date(goal.updatedAt).toLocaleDateString('vi-VN') : '-' }}</td>
                  <td @click.stop>
                    <button
                      class="goal-star-btn"
                      type="button"
                      :class="{ starred: isGoalStarred(goal.id) }"
                      :disabled="starredStore.isPending('Goal', goal.id)"
                      :aria-pressed="isGoalStarred(goal.id)"
                      :aria-label="isGoalStarred(goal.id) ? 'Bỏ gắn sao mục tiêu' : 'Gắn sao mục tiêu'"
                      @click="toggleStar(goal)"
                    >
                      <i :class="isGoalStarred(goal.id) ? 'fa-solid fa-star' : 'fa-regular fa-star'"></i>
                    </button>
                  </td>
                  <td @click.stop="toggleWatch(goal)">
                    <span :class="goal.isFollowing ? 'text-blue-500' : 'text-gray-500'" style="cursor: pointer;">{{ goal.isFollowing ? labels.following : labels.follow }}</span>
                  </td>
                  <td>
                    <div class="owner-cell">
                      <AppAvatar :user="{ id: goal.ownerId, fullName: goal.ownerName, avatarColor: goal.ownerColor, avatarUrl: goal.ownerAvatarUrl }" :size="24" />
                      <span class="owner-name">{{ goal.owner || labels.unassigned }}</span>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div v-else-if="!isLoading" class="goals-empty-state">
            <div class="empty-spaces-icon" aria-hidden="true">
              <i class="fa-solid fa-bullseye"></i>
            </div>
            <div class="empty-spaces-copy">
              <h3>{{ labels.noGoals }}</h3>
              <p>{{ labels.noGoalsDesc }}</p>
              <button class="empty-spaces-btn mt-3" type="button" @click="openCreateModal">
                {{ labels.createGoal }}
              </button>
            </div>
          </div>
          
          <div class="loading-state" v-else>
            <div class="loader-spinner"></div>
          </div>
        </div>
      </div>
    </div>

    <!-- Create Goal Modal -->
    <AppModal
      v-model="isCreateModalOpen"
      :title="labels.createGoal"
      width="500px"
      :confirmText="labels.create"
      :cancelText="labels.cancel"
      @confirm="submitCreateGoal"
      @cancel="isCreateModalOpen = false"
    >
      <template #header>
        <div style="display: flex; align-items: center; gap: 8px;">
          <i class="fa-solid fa-bullseye" style="color: #6B778C;"></i> {{ labels.createGoal }}
        </div>
      </template>

      <p style="font-size: 11px; color: #6B778C; margin: 0 0 16px 0;">{{ labels.requiredNote }} <span class="required" style="color: #DE350B;">*</span></p>
      
      <AppFormField :label="labels.name" required :error="isTitleTouched && !newGoal.title ? labels.nameRequired : ''">
        <input type="text" v-model="newGoal.title" @blur="isTitleTouched = true" style="width: 100%; padding: 8px 12px; border: 2px solid #DFE1E6; border-radius: 3px; font-size: 14px; box-sizing: border-box;" :class="{'error-input': isTitleTouched && !newGoal.title}" />
      </AppFormField>
      
      <AppFormField :label="labels.type" required>
        <div style="position: relative; display: flex; align-items: center; gap: 8px; background: #FAFBFC; padding: 8px 12px; border: 1px solid #DFE1E6; border-radius: 3px; cursor: default;">
           <i class="fa-solid fa-bullseye" style="color: #6B778C; font-size: 14px;"></i>
           <span style="color: #172B4D; font-size: 14px;">Objective</span>
        </div>
      </AppFormField>
      
      <AppFormField :label="labels.targetDate">
        <el-date-picker
          v-model="newGoal.date"
          type="date"
          :placeholder="labels.chooseDate"
          format="MMM DD, YYYY"
          value-format="YYYY-MM-DD"
          style="width: 100%"
          class="jira-date-picker"
          :teleported="true"
        />
      </AppFormField>
      
      <AppFormField :label="labels.owner" required>
        <div class="owner-input-wrapper" @click="isOwnerDropdownOpen = !isOwnerDropdownOpen" style="position: relative; border: 2px solid #DFE1E6; border-radius: 3px; padding: 6px 12px; cursor: pointer; display: flex; align-items: center; gap: 8px; background: white;">
           <AppAvatar :user="{ avatarColor: newGoal.ownerAvatarColor, initials: newGoal.ownerAvatar, fullName: newGoal.ownerName, avatarUrl: newGoal.ownerAvatarUrl }" :size="24" />
           <span style="font-size: 14px; color: #172B4D;">{{ newGoal.ownerName }}</span>
        </div>
        
        <div v-if="isOwnerDropdownOpen" class="dropdown-menu" style="position: absolute; margin-top: 14px; background: white; border: 1px solid #DFE1E6; border-radius: 3px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); width: 100%; z-index: var(--sp-z-dropdown, 1000); max-height: 200px; overflow-y: auto;">
           <div v-for="user in siteUsers" :key="user.id" @click="selectOwner(user)" style="display: flex; align-items: center; gap: 8px; padding: 8px 12px; cursor: pointer; transition: background 0.1s;" onmouseover="this.style.background='#FAFBFC'" onmouseout="this.style.background='transparent'">
              <AppAvatar :user="user" :size="24" />
              <span style="font-size: 14px; color: #172B4D;">{{ user.name }}</span>
           </div>
        </div>
      </AppFormField>
      
      <div v-if="createError" style="color: #DE350B; font-size: 13px; margin-top: 12px;">{{ createError }}</div>
    </AppModal>
  </AppPageLayout>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useGoalStore } from '@/store/useGoalStore'
import { useStarredStore } from '@/store/useStarredStore'
import { useFollowerStore } from '@/store/useFollowerStore'
import { usePeopleStore } from '@/store/usePeopleStore'
import { useI18nStore } from '@/store/useI18nStore'
import axiosClient from '@/api/axiosClient'
import { signalRService } from '@/api/signalrService'

import AppPageLayout from '@/components/common/Foundation/AppPageLayout.vue'
import AppPageHeader from '@/components/common/Foundation/AppPageHeader.vue'
import ProjectPageToolbar from '@/components/common/ProjectPageToolbar.vue'
import ToolbarSortMenu from '@/components/common/ToolbarSortMenu.vue'
import AppCard from '@/components/common/Foundation/AppCard.vue'
import AppEmptyState from '@/components/common/Foundation/AppEmptyState.vue'
import AppStatusBadge from '@/components/common/Foundation/AppStatusBadge.vue'
import AppAvatar from '@/components/common/Foundation/AppAvatar.vue'
import AppModal from '@/components/common/Foundation/AppModal.vue'
import AppFormField from '@/components/common/Foundation/AppFormField.vue'
import WorkItemsListTable from '@/components/common/WorkItemsListTable.vue'

import FilterBar from '@/components/FilterBar.vue'
import { getStoredUser } from '@/utils/permissions'
import { getInitials, getAvatarColor } from '@/utils/avatarHelper'

const router = useRouter()
const route = useRoute()
const goalsBasePath = computed(() => route.path.startsWith('/goals') ? '/goals' : '/home/goals')
const goalStore = useGoalStore()
const starredStore = useStarredStore()
const followerStore = useFollowerStore()
const peopleStore = usePeopleStore()
const i18nStore = useI18nStore()

const currentTab = ref('all')
const searchQuery = ref('')
const currentTabHeader = computed(() => {
  if (currentTab.value === 'following') return labels.value.following
  if (currentTab.value === 'archived') return labels.value.archived
  return labels.value.goalDirectory
})
const isVi = computed(() => i18nStore.locale === 'vi')
const labels = computed(() => isVi.value
  ? {
      title: 'Mục tiêu',
      createGoal: 'Tạo mục tiêu',
      goalDirectory: 'Thư mục mục tiêu',
      following: 'Đang theo dõi',
      archived: 'Đã lưu trữ',
      yourGoals: 'Mục tiêu của bạn',
      noAssignedGoals: 'Bạn chưa được gán hoặc cộng tác trên bất kỳ mục tiêu nào.',
      learnMore: 'Tìm hiểu thêm',
      search: 'Tìm kiếm',
      status: 'Trạng thái',
      owner: 'Chủ sở hữu',
      progress: 'Tiến độ',
      favorite: 'Yêu thích',
      follow: 'Theo dõi',
      clearFilters: 'Xóa lọc',
      goal: 'Mục tiêu',
      createdDate: 'Ngày tạo',
      updatedDate: 'Ngày cập nhật',
      unassigned: 'Chưa gán',
      noGoals: 'Không tìm thấy mục tiêu nào',
      noGoalsDesc: 'Không có mục tiêu nào khớp với bộ lọc của bạn.',
      notStarted: 'Chưa bắt đầu (0%)',
      inProgress: 'Đang tiến hành (>0%)',
      completeProgress: 'Hoàn thành (100%)',
      pending: 'Đang chờ cập nhật',
      onTrack: 'Đúng tiến độ',
      atRisk: 'Có rủi ro',
      offTrack: 'Trễ tiến độ',
      completed: 'Đã hoàn tất',
      requiredNote: 'Các trường bắt buộc được đánh dấu sao',
      name: 'Tên',
      nameRequired: 'Bạn phải đặt tên mục tiêu',
      type: 'Loại',
      targetDate: 'Ngày mục tiêu',
      chooseDate: 'Chọn ngày',
      cancel: 'Hủy',
      create: 'Tạo'
    }
  : {
      title: 'Goals',
      createGoal: 'Create goal',
      goalDirectory: 'Goal directory',
      following: 'Following',
      archived: 'Archived',
      yourGoals: 'Your goals',
      noAssignedGoals: 'You are not assigned or collaborating on any goals yet.',
      learnMore: 'Learn more',
      search: 'Search',
      status: 'Status',
      owner: 'Owner',
      progress: 'Progress',
      favorite: 'Favorite',
      follow: 'Follow',
      clearFilters: 'Clear filters',
      goal: 'Goal',
      createdDate: 'Created date',
      updatedDate: 'Updated date',
      unassigned: 'Unassigned',
      noGoals: 'No goals found',
      noGoalsDesc: 'No goals match your filters.',
      notStarted: 'Not started (0%)',
      inProgress: 'In progress (>0%)',
      completeProgress: 'Completed (100%)',
      pending: 'Pending update',
      onTrack: 'On track',
      atRisk: 'At risk',
      offTrack: 'Off track',
      completed: 'Completed',
      requiredNote: 'Required fields are marked with an asterisk',
      name: 'Name',
      nameRequired: 'You must name this goal',
      type: 'Type',
      targetDate: 'Target date',
      chooseDate: 'Choose date',
      cancel: 'Cancel',
      create: 'Create'
    })

const goalTableColumns = computed(() => [
  { key: 'goal', label: labels.value.goal, icon: 'fa-solid fa-bullseye', width: '30%', minWidth: '300px', sticky: true },
  { key: 'status', label: labels.value.status, icon: 'fa-regular fa-circle-dot', width: '16%', minWidth: '170px' },
  { key: 'progress', label: labels.value.progress, icon: 'fa-solid fa-chart-line', width: '16%', minWidth: '170px' },
  { key: 'createdAt', label: labels.value.createdDate, icon: 'fa-regular fa-calendar', width: '12%', minWidth: '140px' },
  { key: 'updatedAt', label: labels.value.updatedDate, icon: 'fa-regular fa-clock', width: '12%', minWidth: '140px' },
  { key: 'favorite', label: labels.value.favorite, icon: 'fa-regular fa-star', width: '100px', minWidth: '100px' },
  { key: 'follow', label: labels.value.follow, icon: 'fa-regular fa-eye', width: '120px', minWidth: '120px' },
  { key: 'owner', label: labels.value.owner, icon: 'fa-solid fa-user-tie', width: '18%', minWidth: '190px' }
])
const activeFilters = ref([])

const goalFilterFields = computed(() => [
  { key: 'status', label: labels.value.status, icon: 'fa-regular fa-circle-dot', values: statusOptions.value },
  { key: 'owner', label: labels.value.owner, icon: 'fa-regular fa-user', values: ownerOptions.value },
  { key: 'progress', label: labels.value.progress, icon: 'fa-solid fa-chart-line', values: [labels.value.notStarted, labels.value.inProgress, labels.value.completeProgress] },
  { key: 'favorite', label: labels.value.favorite, icon: 'fa-regular fa-star', values: [isVi.value ? 'Có' : 'Yes', isVi.value ? 'Không' : 'No'] },
  { key: 'following', label: labels.value.follow, icon: 'fa-regular fa-eye', values: [isVi.value ? 'Có' : 'Yes', isVi.value ? 'Không' : 'No'] }
])

const goalOperators = {
  status: ['is', 'is not'],
  owner: ['is', 'is not'],
  progress: ['is', 'is not'],
  favorite: ['is', 'is not'],
  following: ['is', 'is not']
}

const customGoalValueMeta = (fieldKey, value) => {
  if (fieldKey === 'status') {
    return { icon: 'fa-regular fa-circle-dot', color: '#10b981' }
  }
  if (fieldKey === 'owner') {
    return { icon: 'fa-regular fa-user', color: '#3b82f6' }
  }
  if (fieldKey === 'progress') {
    return { icon: 'fa-solid fa-chart-line', color: '#8b5cf6' }
  }
  if (fieldKey === 'favorite') {
    return { icon: 'fa-solid fa-star', color: '#eab308' }
  }
  if (fieldKey === 'following') {
    return { icon: 'fa-regular fa-eye', color: '#6366f1' }
  }
  return null
}

const showFilterDropdown = ref(false)
const toggleFilterDropdown = () => {
  showFilterDropdown.value = !showFilterDropdown.value
}
const handleOutsideClick = (e) => {
  if (!e.target.closest('.js-toolbar-popup-scope')) {
    showFilterDropdown.value = false
  }
}

const uniqueValues = (selector) => Array.from(new Set(
  (goalStore.goals || [])
    .map(selector)
    .filter(value => value && value !== 'N/A')
)).sort()

const statusOptions = computed(() => {
  const statuses = uniqueValues(g => g.status);
  // Map raw statuses to translated statuses and remove duplicates again
  return Array.from(new Set(statuses.map(translateStatus)));
})
const ownerOptions = computed(() => uniqueValues(g => g.owner))
const progressOptions = computed(() => [
  { label: labels.value.notStarted, value: '0' },
  { label: labels.value.inProgress, value: 'in_progress' },
  { label: labels.value.completeProgress, value: '100' }
])
const booleanOptions = computed(() => [
  { label: isVi.value ? 'Có' : 'Yes', value: 'true' },
  { label: isVi.value ? 'Không' : 'No', value: 'false' }
])

const clearFilters = () => {
  activeFilters.value = []
}
const hasActiveFilters = computed(() => activeFilters.value.length > 0)
const activeFilterCount = computed(() => activeFilters.value.length)

const isCreateModalOpen = ref(false)
const isTitleTouched = ref(false)
const isOwnerDropdownOpen = ref(false)
const createError = ref('')

const siteUsers = computed(() => {
  return peopleStore.users.map(u => ({
    id: u.id,
    name: u.fullName || u.email,
    initials: u.initials || getInitials(u.fullName || u.email),
    avatarColor: u.avatarColor,
    avatarUrl: u.avatarUrl
  }))
})

const newGoal = ref({
  title: '',
  type: 'Objective',
  date: '',
  ownerId: '',
  ownerName: '',
  ownerAvatar: '',
  ownerAvatarColor: '',
  ownerAvatarUrl: '',
  status: 'Đang chờ cập nhật'
})

const handleRealtimeGoalChange = event => goalStore.applyRealtimeEntityEvent(event)

onMounted(async () => {
  await goalStore.fetchGoals()
  const workspaceId = await goalStore.ensureWorkspaceId()
  signalRService.on('EntityChanged', handleRealtimeGoalChange)
  await signalRService.startWorkspaceConnection(workspaceId)
  await starredStore.fetchStarredItems({ page: 1, pageSize: 100 })
  await followerStore.fetchFollowedItems()
  await peopleStore.fetchPeople()
  window.addEventListener('global-create-click', openCreateModal)
  document.addEventListener('click', handleOutsideClick)
})

onUnmounted(() => {
  signalRService.off('EntityChanged', handleRealtimeGoalChange)
  window.removeEventListener('global-create-click', openCreateModal)
  document.removeEventListener('click', handleOutsideClick)
})

const isLoading = computed(() => goalStore.isLoading)
const goalSortMode = ref('progress-desc')
const goalSortDirection = ref('desc')
const goalSortOptions = [
  { value: 'progress-desc', label: 'Tiến độ', icon: 'fa-solid fa-chart-line' },
  { value: 'updated-desc', label: 'Cập nhật gần nhất', icon: 'fa-regular fa-clock' },
  { value: 'name', label: 'Tên mục tiêu', icon: 'fa-solid fa-font' }
]

const filteredGoals = computed(() => {
  let list = goalStore.goals || []

  // Lọc theo tab
  if (currentTab.value === 'archived') {
    list = list.filter(g => g.isArchived)
  } else if (currentTab.value === 'following') {
    list = list.filter(g => !g.isArchived && g.isFollowing)
  } else {
    // Tất cả mục tiêu (all) thì chỉ hiện những cái chưa archived
    list = list.filter(g => !g.isArchived)
  }

  // Tìm kiếm
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    list = list.filter(g => 
      g.title.toLowerCase().includes(q) || 
      (g.status && g.status.toLowerCase().includes(q)) ||
      (g.owner && g.owner.toLowerCase().includes(q))
    )
  }

  if (activeFilters.value.length > 0) {
    list = list.filter(g => {
      return activeFilters.value.every(f => {
        let val = ''
        let isMatch = false
        if (f.field === 'status') {
          val = translateStatus(g.status)
          isMatch = val === f.value
        } else if (f.field === 'owner') {
          val = g.owner
          isMatch = val === f.value
        } else if (f.field === 'progress') {
          if (f.value === labels.value.notStarted) isMatch = g.progress === 0
          else if (f.value === labels.value.completeProgress) isMatch = g.progress === 100
          else if (f.value === labels.value.inProgress) isMatch = g.progress > 0 && g.progress < 100
        } else if (f.field === 'favorite') {
          const isFav = f.value === (isVi.value ? 'Có' : 'Yes')
          isMatch = starredStore.isStarred('Goal', g.id) === isFav
        } else if (f.field === 'following') {
          const isFol = f.value === (isVi.value ? 'Có' : 'Yes')
          isMatch = !!g.isFollowing === isFol
        }
        return f.operator === 'is' ? isMatch : !isMatch
      })
    })
  }

  return [...list].sort((left, right) => {
    let result
    if (goalSortMode.value === 'updated-desc') {
      result = new Date(right.updatedAt || 0).getTime() - new Date(left.updatedAt || 0).getTime()
    } else if (goalSortMode.value === 'name') {
      result = `${left.title || ''}`.localeCompare(`${right.title || ''}`)
    } else {
      result = (Number(right.progress) || 0) - (Number(left.progress) || 0)
    }
    return goalSortDirection.value === 'asc' ? result * -1 : result
  })
})

const getStatusClass = (status) => {
  if (!status) return 'status-pending'
  const map = {
    'đúng tiến độ': 'status-on-track',
    'on track': 'status-on-track',
    'có rủi ro': 'status-at-risk',
    'at risk': 'status-at-risk',
    'trễ tiến độ': 'status-off-track',
    'off track': 'status-off-track',
    'không đúng tiến độ': 'status-off-track',
    'đang chờ cập nhật': 'status-pending',
    'pending': 'status-pending',
    'đã hoàn tất': 'status-done',
    'completed': 'status-done',
    'đã lưu trữ': 'status-archived',
    'archived': 'status-archived'
  }
  return map[status.toLowerCase()] || 'status-pending'
}

const translateStatus = (status) => {
  if (!status) return labels.value.pending
  const map = {
    'on track': labels.value.onTrack,
    'đúng tiến độ': labels.value.onTrack,
    'dung tien do': labels.value.onTrack,
    'at risk': labels.value.atRisk,
    'có rủi ro': labels.value.atRisk,
    'co rui ro': labels.value.atRisk,
    'off track': labels.value.offTrack,
    'trễ tiến độ': labels.value.offTrack,
    'tre tien do': labels.value.offTrack,
    'pending': labels.value.pending,
    'đang chờ cập nhật': labels.value.pending,
    'dang cho cap nhat': labels.value.pending,
    'completed': labels.value.completed,
    'đã hoàn tất': labels.value.completed,
    'da hoan tat': labels.value.completed,
    'archived': labels.value.archived,
    'đã lưu trữ': labels.value.archived,
    'da luu tru': labels.value.archived
  }
  return map[status.toLowerCase()] || status
}

const openCreateModal = () => {
  const stored = getStoredUser() || {}
  const currentUser = peopleStore.users.find(u => u.id === stored.id) || peopleStore.currentUser || stored
  const name = currentUser.fullName || currentUser.name || currentUser.publicName || currentUser.email || 'User'
  const email = currentUser.email || ''

  newGoal.value = { 
    title: '', 
    type: 'Objective', 
    date: '', 
    ownerId: currentUser.id,
    ownerName: name, 
    ownerAvatar: currentUser.initials || getInitials(name, email),
    ownerAvatarColor: currentUser.avatarColor,
    ownerAvatarUrl: currentUser.avatarUrl,
    status: 'Đang chờ cập nhật' 
  }
  isTitleTouched.value = false
  isOwnerDropdownOpen.value = false
  isCreateModalOpen.value = true
}

const selectOwner = (user) => {
  newGoal.value.ownerId = user.id
  newGoal.value.ownerName = user.name
  newGoal.value.ownerAvatar = user.initials
  newGoal.value.ownerAvatarColor = user.avatarColor
  newGoal.value.ownerAvatarUrl = user.avatarUrl
  isOwnerDropdownOpen.value = false
}

const submitCreateGoal = async () => {
  isTitleTouched.value = true
  if (!newGoal.value.title) return
  
  createError.value = ''
  try {
    await goalStore.createGoal({ 
      title: newGoal.value.title, 
      status: newGoal.value.status,
      ownerId: newGoal.value.ownerId,
      owner: newGoal.value.ownerName,
      ownerColor: newGoal.value.ownerAvatarColor,
      type: newGoal.value.type,
      endDate: newGoal.value.date
    })
    isCreateModalOpen.value = false
    ElMessage.success(isVi.value ? 'Tạo mục tiêu thành công!' : 'Goal created successfully!')
    await goalStore.fetchGoals()
  } catch (error) {
    console.error('Lỗi khi tạo mục tiêu:', error)
    createError.value = error.response?.data?.message || (isVi.value ? 'Lỗi khi tạo mục tiêu' : 'Failed to create goal')
  }
}

const goToGoal = (id) => {
  router.push(`${goalsBasePath.value}/${id}`)
}

const isGoalStarred = (id) => starredStore.isStarred('Goal', id)

const toggleStar = async (goal) => {
  if (!goal?.id) return
  goalStore.currentGoal = goal
  try {
    await goalStore.toggleStar()
  } catch {
    ElMessage.error(starredStore.error || 'Could not update starred item.')
  }
}

const toggleWatch = async (goal) => {
  if (!goal?.id) return
  await goalStore.toggleFollow(goal.id)
}
</script>

<style scoped>
.section-header {
  margin: -10px 0 16px;
}

.section-header h2 {
  color: #172B4D;
  font-size: 18px;
  font-weight: 750;
  line-height: 1.25;
  margin: 0;
}

.goals-list-container {
  margin-top: 0;
}


.goals-wrapper {
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
  background-color: #EBECF0;
  color: #A5ADBA;
  cursor: not-allowed;
}

.secondary-btn {
  background-color: transparent;
  color: #0052CC;
  border: none;
  padding: 8px 16px;
  border-radius: 3px;
  font-weight: 500;
  font-size: 14px;
  cursor: pointer;
  text-decoration: none;
  transition: background-color 0.2s;
}

.secondary-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
  text-decoration: underline;
}

.tabs-nav {
  display: flex;
  align-items: center;
  gap: 6px !important;
  width: max-content !important;
  max-width: 100%;
  min-height: 42px;
  margin: 0 !important;
  padding: 4px !important;
  border: 1px solid rgba(148, 163, 184, 0.2) !important;
  border-radius: 9px !important;
  background: transparent !important;
  box-shadow: none !important;
  overflow-x: auto;
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
  cursor: pointer;
  white-space: nowrap;
  transition: background 0.18s ease, color 0.18s ease;
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

.module-content {
  padding: 0;
  flex: 1;
}

.goals-tabs-nav {
  margin-top: 18px;
}

.goals-toolbar-filters {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
}

/* Empty State Dành cho bạn */
.empty-state-banner {
  background-color: #FAFBFC;
  border-radius: 8px;
  overflow: hidden;
}

.empty-banner-content {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 40px 64px;
  max-width: 1000px;
  margin: 0 auto;
}

.empty-banner-text {
  flex: 1;
  max-width: 400px;
}

.empty-banner-text h2 {
  font-size: 24px;
  font-weight: 500;
  color: #172B4D;
  margin: 0 0 16px 0;
}

.empty-banner-text p {
  font-size: 16px;
  color: #42526E;
  margin: 0 0 32px 0;
  line-height: 1.5;
}

.empty-banner-actions {
  display: flex;
  gap: 16px;
  align-items: center;
}

.empty-banner-illustration {
  flex: 1;
  display: flex;
  justify-content: flex-end;
}

.empty-illustration {
  width: 280px;
  height: 200px;
  background-color: #E6FCFF;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.empty-illustration i {
  font-size: 64px;
  color: #0052CC;
}

/* List Controls */
.list-controls {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 24px;
  flex-wrap: wrap;
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

.filter-actions {
  display: flex;
  gap: 8px;
  flex: 0 0 auto;
  min-width: max-content;
}

.active-filter-pill {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-width: max-content;
  height: 38px;
  padding: 0 14px;
  border: 1px solid color-mix(in srgb, var(--home-accent, #0052cc) 62%, var(--home-border, #dfe1e6));
  border-radius: 10px;
  background: color-mix(in srgb, var(--home-accent, #0052cc) 12%, var(--home-panel, #ffffff));
  color: var(--home-accent, #0052cc);
  font-size: 13px;
  font-weight: 800;
  line-height: 1;
  white-space: nowrap;
  cursor: default;
}

.active-filter-pill i {
  font-size: 11px;
}

.filter-btn {
  background-color: rgba(9, 30, 66, 0.04);
  border: none;
  border-radius: 3px;
  padding: 8px 12px;
  font-size: 14px;
  font-weight: 500;
  color: #42526E;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
  transition: background-color 0.2s;
}

.filter-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.filter-btn i {
  font-size: 10px;
}

/* Table Container - matches IntakeInbox */
.table-container {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.04);
  margin-top: 12px;
}

/* Table */
.jira-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
}

.jira-table th {
  background: var(--color-surface);
  border-bottom: 2px solid var(--color-border) !important;
  padding: 12px 16px !important;
  font-size: 11px;
  letter-spacing: 0.05em;
  font-weight: 700;
  text-transform: uppercase;
  white-space: nowrap;
  color: var(--color-text-secondary);
}

.jira-table th i {
  color: inherit;
  margin-right: 6px;
  opacity: 0.88;
}

.col-title { width: 30%; }
.col-status { width: 15%; }
.col-progress { width: 15%; }
.col-report { width: 15%; }
.col-labels { width: 15%; }
.col-owner { width: 10%; }

.jira-table td {
  height: 50px;
  padding: 10px 14px !important;
  font-size: 13px;
  color: var(--color-text-primary);
  border-bottom: 1px solid var(--color-border) !important;
  cursor: pointer;
  vertical-align: middle;
  white-space: nowrap;
}

.jira-table tbody tr {
  box-shadow: inset 3px 0 0 transparent;
  transition: all 0.2s ease;
}

.jira-table tbody tr:hover {
  box-shadow: inset 3px 0 0 var(--sa-primary, var(--color-accent)) !important;
}

.jira-table tbody tr:hover td {
  background: color-mix(in srgb, var(--sa-primary, var(--color-accent)) 8%, var(--color-surface)) !important;
}

.goal-title-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.goal-icon {
  color: var(--sa-primary, var(--color-accent));
  font-size: 16px;
}

.goal-title {
  font-weight: 700;
  color: var(--color-text-primary);
}

.goal-title:hover {
  color: var(--color-accent);
}

/* Status Badge matching Jira exactly */
.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

/* Colors for Statuses */
.status-on-track { background-color: #E3FCEF; color: #006644; }
.status-on-track .status-dot { background-color: #36B37E; }

.status-at-risk { background-color: #FFF0B3; color: #FF8B00; }
.status-at-risk .status-dot { background-color: #FFAB00; }

.status-off-track { background-color: #FFEBE6; color: #BF2600; }
.status-off-track .status-dot { background-color: #FF5630; }

.status-done { background-color: #EAE6FF; color: #403294; }
.status-done .status-dot { background-color: #6554C0; }

.status-pending, .status-archived { background-color: #DFE1E6; color: #42526E; }
.status-pending .status-dot, .status-archived .status-dot { background-color: #7A869A; }

/* Progress Bar */
.progress-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.progress-bar-bg {
  flex: 1;
  height: 6px;
  background-color: #DFE1E6;
  border-radius: 3px;
  overflow: hidden;
}

.progress-bar-fill {
  height: 100%;
  background-color: #0052CC;
  border-radius: 3px;
}

.progress-text {
  font-size: 12px;
  color: #5E6C84;
  min-width: 28px;
}

/* Labels */
.labels-container {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.label-badge {
  background-color: #DFE1E6;
  color: #42526E;
  font-size: 12px;
  padding: 2px 6px;
  border-radius: 3px;
  white-space: nowrap;
}

/* Owner */
.owner-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.owner-avatar {
  width: 24px;
  height: 24px;
  background-color: #0052CC;
  color: white;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  font-weight: bold;
}

.owner-name {
  color: #172B4D;
}

/* Empty State Table */
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
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.form-group label {
  display: block;
  font-size: 12px;
  font-weight: 600;
  color: #5E6C84;
  margin-bottom: 8px;
}

.required {
  color: #DE350B;
}

.form-group input, .jira-select {
  width: 100%;
  padding: 8px 12px;
  border: 2px solid #DFE1E6;
  border-radius: 3px;
  font-size: 14px;
  box-sizing: border-box;
  outline: none;
}

.form-group input:focus, .jira-select:focus {
  border-color: #4C9AFF;
}

.error-input {
  border-color: #DE350B !important;
}

.error-input:focus {
  box-shadow: 0 0 0 2px rgba(222, 53, 11, 0.2);
}

.modal-footer {
  padding: 16px 24px;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  border-top: 1px solid #DFE1E6;
}

.cancel-btn {
  background: transparent;
  color: #5E6C84;
  border: none;
  padding: 8px 12px;
  border-radius: 3px;
  font-weight: 500;
  cursor: pointer;
}

.cancel-btn:hover {
  background: rgba(9, 30, 66, 0.08);
}

.goal-star-btn {
  appearance: none;
  -webkit-appearance: none;
  min-width: 40px;
  min-height: 40px;
  padding: 0;
  border: 0;
  border-radius: 9px;
  background: transparent;
  color: var(--home-muted, #6b778c);
  font: inherit;
  cursor: pointer;
  touch-action: manipulation;
  display: inline-grid;
  place-items: center;
}

.goal-star-btn:hover {
  background: color-mix(in srgb, var(--home-accent, #0c66e4) 9%, transparent);
}

.goal-star-btn.starred {
  color: #e3a008;
}

.goal-star-btn:focus-visible {
  outline: 3px solid color-mix(in srgb, var(--home-accent, #0c66e4) 42%, transparent);
  outline-offset: 2px;
}

.goal-star-btn:disabled {
  cursor: wait;
}

.goal-star-btn i {
  width: 1em;
  line-height: 1;
  text-align: center;
}

:deep(.jira-date-picker .el-input__inner) {
  border: none !important;
  padding: 0 !important;
  height: auto !important;
}

.filter-dropdown-wrapper {
  position: relative;
  display: inline-block;
}
.plane-dropdown-menu {
  position: absolute;
  top: calc(100% + 6px);
  right: 0;
  z-index: 1050;
  width: 290px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 9px;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.08);
  padding: 12px;
}
.filter-dropdown-menu {
  width: 640px;
  max-width: calc(100vw - 32px);
  max-height: none;
  padding: 8px !important;
  left: 0;
  right: auto;
  overflow: visible;
}
.filter-dropdown-menu :deep(.filter-bar-container) {
  min-height: auto;
  box-shadow: none;
  background: transparent;
  border: none;
  padding: 0 !important;
  overflow: visible;
}

.empty-spaces-btn {
  height: 36px;
  padding: 0 16px;
  border-radius: 9px;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-primary);
  font-size: 13.5px;
  font-weight: 600;
  cursor: pointer;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
  transition: all 0.2s ease;
  margin-top: 12px;
}

.empty-spaces-btn:hover {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border));
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface));
  color: var(--color-accent);
}
</style>
