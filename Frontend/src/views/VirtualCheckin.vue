<template>
  <section class="checkin-page">
    <header class="page-header app-shell-page-header">
      <div class="app-shell-title-wrap">
        <span class="eyebrow">DAILY CHECK-IN</span>
        <h1>{{ t('checkin.title') }}</h1>
        <div class="app-shell-header-help">
          <span class="app-shell-header-help-btn" aria-label="About Daily Check-in">
            <i class="fa-solid fa-question"></i>
          </span>
          <div class="app-shell-header-help-popover" role="tooltip">
            <span>DAILY CHECK-IN</span>
            <p>{{ t('checkin.subtitle') }}</p>
          </div>
        </div>
      </div>

      <!-- Quick Action: Submit Checkin -->
      <div v-if="!userCheckedIn">
        <button class="primary-action" type="button" @click="openCheckinModal">
          <i class="fa-solid fa-plus"></i>{{ t('checkin.report') }}
        </button>
      </div>
      <div v-else>
        <el-tag type="success" size="large" class="flex items-center gap-2 font-semibold">
          <i class="fa-solid fa-circle-check" style="margin-right: 5px;"></i>Đã Check-in hôm nay
        </el-tag>
      </div>
    </header>

    <!-- Standardized Toolbar using ProjectPageToolbar -->
    <div class="sprinta-layout-toolbar">
      <ProjectPageToolbar
        :showSearch="true"
        :searchQuery="checkinSearch"
        @update:searchQuery="checkinSearch = $event"
        searchPlaceholder="Tìm thành viên, vai trò, nội dung..."
      >
        <template #filters>
          <div class="filter-dropdown-wrapper js-toolbar-popup-scope">
            <button
              class="timeline-filter-trigger icon-only-trigger"
              type="button"
              aria-label="Filters"
              title="Bộ lọc"
              @click="toggleFilterDropdown"
              :class="{ active: showFilterDropdown || activeCheckinFilters.length }"
            >
              <i class="fa-solid fa-filter"></i>
              <span v-if="activeCheckinFilters.length" class="filter-count">{{ activeCheckinFilters.length }}</span>
            </button>
            <div class="plane-dropdown-menu filter-dropdown-menu" v-show="showFilterDropdown" @click.stop>
              <FilterBar
                v-model:filters="activeCheckinFilters"
                :fields="checkinFilterFields"
                :operators="checkinOperators"
                :custom-value-meta="customCheckinValueMeta"
                :active="showFilterDropdown"
              />
            </div>
          </div>
        </template>
  
        <template #left>
          <el-select
            ref="projectSelectRef"
            v-model="activeProjectId"
            placeholder="Filter dự án"
            @change="selectProject"
            class="custom-project-select"
            popper-class="custom-project-dropdown"
          >
            <template #prefix>
              <i class="fa-solid fa-folder-open"></i>
            </template>
            <el-option
              v-for="p in projectsList"
              :key="p.id"
              :label="`[${p.key}] ${p.name}`"
              :value="p.id"
            />
          </el-select>
  
          <span class="checkin-filter-count text-xs text-muted ml-2">
            {{ filteredTeamCheckins.length }}/{{ teamCheckins.length }}
          </span>
        </template>
  
        <template #toggles>
          <div class="view-toggles">
            <button 
              class="toggle-btn" 
              :class="{ active: currentView === 'list' }" 
              @click="currentView = 'list'" 
              title="List view"
            >
              <i class="fa-solid fa-bars"></i>
            </button>
            <button 
              class="toggle-btn" 
              :class="{ active: currentView === 'grid' }" 
              @click="currentView = 'grid'" 
              title="Card view"
            >
              <i class="fa-solid fa-table-columns"></i>
            </button>
          </div>
        </template>
        
        <template #sort>
          <div class="display-dropdown-wrapper js-toolbar-popup-scope" style="position: relative; display: inline-block;">
            <button
              class="timeline-filter-trigger icon-only-trigger"
              type="button"
              aria-label="Sort"
              title="Sắp xếp"
              @click.stop="toggleSortDropdown"
              :class="{ 'active': showSortDropdown }"
            >
              <i class="fa-solid fa-arrow-down-wide-short"></i>
            </button>
            <div class="plane-dropdown-menu" v-show="showSortDropdown" @click.stop style="width: 320px; left: 0; right: auto; display: flex; flex-direction: column; gap: 10px; padding: 8px; max-height: none; overflow: visible;">
              <!-- Sort Search Input -->
              <div class="filter-search-field">
                <i class="fa-solid fa-magnifying-glass filter-search-icon"></i>
                <input
                  v-model="checkinSortSearchQuery"
                  type="text"
                  class="filter-search-input"
                  placeholder="Tìm kiếm trường sắp xếp..."
                  @click.stop
                />
              </div>
  
              <!-- Sort By Combobox -->
              <div class="filter-combobox" style="position: relative;">
                <span class="filter-label">Sắp xếp theo</span>
                <div class="filter-select-trigger sort-combobox-trigger">
                  <div style="display: flex; align-items: center; gap: 10px; flex: 1; cursor: pointer; min-width: 0;" @click="openSortSelect = (openSortSelect === 'sort' ? null : 'sort')">
                    <i :class="checkinSortOptions.find(o => o.value === checkinSortMode)?.icon || 'fa-solid fa-arrow-down-wide-short'" style="font-size: 13px; color: var(--color-text-secondary); width: 15px; text-align: center;"></i>
                    <span style="font-size: 13px; color: var(--color-text-primary); text-align: left; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">{{ checkinSortOptions.find(o => o.value === checkinSortMode)?.label }}</span>
                  </div>
                  <!-- Asc/Desc buttons inside the trigger -->
                  <div style="display: flex; align-items: center; gap: 4px; margin-right: 8px;">
                    <button
                      type="button"
                      class="dir-mini-btn"
                      :class="{ active: checkinSortDirection === 'asc' }"
                      @click="checkinSortDirection = 'asc'"
                      title="Tăng dần"
                    >
                      <i class="fa-solid fa-arrow-up-wide-short" style="font-size: 11px;"></i>
                    </button>
                    <button
                      type="button"
                      class="dir-mini-btn"
                      :class="{ active: checkinSortDirection === 'desc' }"
                      @click="checkinSortDirection = 'desc'"
                      title="Giảm dần"
                    >
                      <i class="fa-solid fa-arrow-down-short-wide" style="font-size: 11px;"></i>
                    </button>
                  </div>
                  <i class="fa-solid fa-chevron-down" style="font-size: 10px; transition: transform 0.2s; cursor: pointer;" :style="openSortSelect === 'sort' ? { transform: 'rotate(180deg)', color: 'var(--color-accent)' } : {}" @click="openSortSelect = (openSortSelect === 'sort' ? null : 'sort')"></i>
                </div>
                <div v-show="openSortSelect === 'sort'" class="filter-select-menu" style="position: absolute; top: calc(100% + 4px); left: 0; right: 0; max-height: 200px; z-index: 110;">
                  <button
                    v-for="opt in filteredCheckinSortOptions"
                    :key="opt.value"
                    class="filter-select-option"
                    :class="{ selected: checkinSortMode === opt.value }"
                    type="button"
                    @click="checkinSortMode = opt.value; openSortSelect = null"
                  >
                    <i :class="opt.icon"></i>
                    <span>{{ opt.label }}</span>
                  </button>
                </div>
              </div>
            </div>
          </div>
        </template>
      </ProjectPageToolbar>
    </div>

    <div class="page-content">

      <!-- Checkin Cards List (Both checked-in and not checked-in members) -->
      <div v-if="filteredTeamCheckins.length === 0" class="empty-spaces-flat" style="padding: 80px 0;">
        <div class="empty-spaces-icon" aria-hidden="true">
          <i class="fa-regular fa-calendar-check"></i>
        </div>
        <div class="empty-spaces-copy">
          <h3>Chưa có báo cáo check-in</h3>
          <p>Danh sách check-in hàng ngày của các thành viên sẽ xuất hiện ở đây.</p>
        </div>
      </div>
      <template v-else>
        <div v-if="currentView === 'grid'" class="team-checkins-grid">
        <div v-for="team in filteredTeamCheckins" :key="team.id" class="checkin-card card" :class="{ 'not-checked': !team.checkedIn }">
          <div class="card-header flex items-center justify-between">
            <div class="flex items-center" style="gap: 8px;">
              <el-avatar :size="32" :src="team.userAvatar" style="flex-shrink: 0;">{{ team.userName.charAt(0) }}</el-avatar>
              <div style="line-height: 1.3;">
                <span class="font-bold block text-sm">{{ team.userName }}</span>
                <span class="text-xxs text-muted">{{ team.role }}</span>
              </div>
            </div>
            <el-tag size="small" :type="team.checkedIn ? 'success' : 'info'">
              {{ team.checkedIn ? 'Đã Check-in' : 'Chưa Check-in' }}
            </el-tag>
          </div>

          <div class="card-body">
            <div v-if="team.checkedIn" class="checkin-details">
              <!-- Project Badge -->
              <div v-if="team.projectName" class="mb-3">
                <el-tag size="small" type="warning" class="flex items-center gap-1 w-fit font-medium" style="background-color: rgba(230, 162, 60, 0.1); border-color: rgba(230, 162, 60, 0.2); color: #e6a23c;">
                  <i class="fa-solid fa-folder-open" style="margin-right: 4px;"></i>
                  <span>{{ team.projectKey ? `[${team.projectKey}] ` : '' }}{{ team.projectName }}</span>
                </el-tag>
              </div>

              <!-- Done yesterday -->
              <div class="detail-section">
                <span class="section-label">✅ Ngày hôm qua:</span>
                <p class="section-desc">{{ team.yesterday }}</p>
              </div>
              
              <!-- Focus today -->
              <div class="detail-section mt-3">
                <span class="section-label">📌 Mục tiêu hôm nay:</span>
                <p class="section-desc">{{ team.today }}</p>
              </div>

              <!-- Blockers -->
              <div class="detail-section mt-3">
                <span class="section-label">⚠️ Khó khăn (Blocker):</span>
                <p class="section-desc" :class="{ 'has-blocker': team.blocker }">
                  {{ team.blocker || 'Không có khó khăn gì' }}
                </p>
              </div>
            </div>
            
            <div v-else class="empty-checkin flex flex-col items-center justify-center py-6 text-center">
              <i class="fa-regular fa-bell text-2xl text-muted mb-2"></i>
            </div>
          </div>
        </div>
      </div>

      <!-- Checkin List View -->
      <div v-else class="team-checkins-list">
        <div v-for="team in filteredTeamCheckins" :key="team.id" class="checkin-list-row card" :class="{ 'not-checked': !team.checkedIn }">
          <div class="clr-left">
            <el-avatar :size="32" :src="team.userAvatar" style="flex-shrink: 0;">{{ team.userName.charAt(0) }}</el-avatar>
            <div class="user-meta" style="line-height: 1.3;">
              <span class="font-bold block text-sm">{{ team.userName }}</span>
              <span class="text-xxs text-muted">{{ team.role }}</span>
            </div>
          </div>
          
          <div class="clr-middle">
            <div v-if="team.checkedIn" class="clr-details">
              <div class="clr-detail-item">
                <span class="font-semibold text-xs text-muted mr-2">Hôm qua:</span>
                <span class="text-sm text-primary">{{ team.yesterday }}</span>
              </div>
              <div class="clr-detail-item mt-1">
                <span class="font-semibold text-xs text-muted mr-2">Hôm nay:</span>
                <span class="text-sm text-primary">{{ team.today }}</span>
              </div>
              <div class="clr-detail-item mt-1" v-if="team.blocker">
                <span class="font-semibold text-xs text-danger mr-2">Blocker:</span>
                <span class="text-sm text-danger font-medium">{{ team.blocker }}</span>
              </div>
            </div>
            <div v-else class="clr-empty-text text-sm italic text-muted">
              Chưa báo cáo check-in hôm nay
            </div>
          </div>

          <div class="clr-right">
            <div v-if="team.checkedIn && team.projectName" class="mr-3">
              <el-tag size="small" type="warning" class="flex items-center gap-1 font-medium" style="background-color: rgba(230, 162, 60, 0.1); border-color: rgba(230, 162, 60, 0.2); color: #e6a23c;">
                <i class="fa-solid fa-folder-open" style="margin-right: 4px;"></i>
                <span>{{ team.projectKey ? `[${team.projectKey}] ` : '' }}{{ team.projectName }}</span>
              </el-tag>
            </div>
            <el-tag size="small" :type="team.checkedIn ? 'success' : 'info'">
              {{ team.checkedIn ? 'Đã Check-in' : 'Chưa Check-in' }}
            </el-tag>
          </div>
        </div>
      </div>
      </template>
    </div>

    <!-- Virtual Check-in Modal Dialog -->
    <el-dialog
      v-model="checkinModalOpen"
      width="540px"
      append-to-body
      class="sa-data-dialog sa-modal--form"
      :show-close="false"
    >
      <template #header>
        <DataModalHeader
          icon="bi bi-calendar-check"
          title="Báo cáo tiến độ hàng ngày"
          description="Chia sẻ kết quả hôm qua, mục tiêu hôm nay và khó khăn đang gặp"
          @close="checkinModalOpen = false"
        />
      </template>
      <div class="checkin-form-body flex flex-col gap-4">
        <!-- Project Select field -->
        <div>
          <span class="field-label mb-1 block">Dự án báo cáo *</span>
          <el-select 
            v-model="form.projectId" 
            placeholder="Chọn dự án liên quan..."
            class="w-full"
            disabled
          >
            <el-option
              v-for="p in projectsList"
              :key="p.id"
              :label="`[${p.key}] ${p.name}`"
              :value="p.id"
            />
          </el-select>
        </div>

        <!-- Yesterday input -->
        <div>
          <span class="field-label mb-1 block">Hôm qua bạn đã làm được gì? *</span>
          <textarea 
            v-model="form.yesterday" 
            placeholder="Ví dụ: Hoàn tất cập nhật connection string cho SQL Server, thiết kế các layout..."
            class="w-full h-20 p-2"
          ></textarea>
        </div>

        <!-- Today input -->
        <div>
          <span class="field-label mb-1 block">Mục tiêu chính hôm nay của bạn? *</span>
          <textarea 
            v-model="form.today" 
            placeholder="Ví dụ: Thiết kế giao diện Chat nhóm và Daily Checkin..."
            class="w-full h-20 p-2"
          ></textarea>
        </div>

        <!-- Blocker input -->
        <div>
          <span class="field-label mb-1 block">Khó khăn đang gặp phải (nếu có)?</span>
          <input 
            v-model="form.blocker" 
            type="text" 
            placeholder="Để trống nếu không có khó khăn nào"
            class="w-full"
          />
        </div>
      </div>
      <template #footer>
        <div class="flex justify-end gap-2">
          <el-button class="checkin-modal-footer-btn cancel" @click="checkinModalOpen = false">Hủy</el-button>
          <el-button class="checkin-modal-footer-btn submit" @click="submitCheckin">Gửi báo cáo</el-button>
        </div>
      </template>
    </el-dialog>
  </section>
</template>

<script setup>
import { ref, onMounted, onBeforeUnmount, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'

import axiosClient from '@/api/axiosClient'

import { useI18nStore } from '@/store/useI18nStore'
import { useProjectStore } from '@/store/useProjectStore'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import ProjectPageToolbar from '@/components/common/ProjectPageToolbar.vue'
import FilterBar from '@/components/FilterBar.vue'

const { t } = useI18nStore()
const projectStore = useProjectStore()

const checkinModalOpen = ref(false)
const aiLoading = ref(false)
const aiSummaryText = ref('')
const projectsList = ref([])
const activeProjectId = ref('')
const teamCheckins = ref([])
const userCheckedIn = ref(false)
const projectSelectRef = ref(null)
const currentView = ref(localStorage.getItem('checkin_view_mode') || 'grid')
watch(currentView, (val) => {
  localStorage.setItem('checkin_view_mode', val)
})

const checkinSearch = ref('')
const tr = (key, fallback) => {
  const translated = t(key)
  return translated === key ? fallback : translated
}

const currentUser = ref({
  id: '',
  fullName: 'Dev Admin',
  email: 'dev@sprinta.local',
  avatarUrl: ''
})

const form = ref({
  projectId: '',
  yesterday: '',
  today: '',
  blocker: ''
})

const checkedInCount = computed(() => {
  return teamCheckins.value.filter(t => t.checkedIn).length
})

const blockerCount = computed(() => {
  return teamCheckins.value.filter(t => t.checkedIn && t.blocker).length
})

// Dropdowns State
const showFilterDropdown = ref(false)
const showSortDropdown = ref(false)
const openSortSelect = ref(null)

const toggleFilterDropdown = () => {
  showFilterDropdown.value = !showFilterDropdown.value
  showSortDropdown.value = false
}

const toggleSortDropdown = () => {
  showSortDropdown.value = !showSortDropdown.value
  showFilterDropdown.value = false
  openSortSelect.value = null
}

const handleOutsideClick = (e) => {
  if (!e.target.closest('.js-toolbar-popup-scope')) {
    showFilterDropdown.value = false
    showSortDropdown.value = false
    openSortSelect.value = null
  }
}

// Filter Configuration
const activeCheckinFilters = ref([])

const checkinFilterFields = computed(() => {
  const roles = Array.from(new Set(teamCheckins.value.map(item => item.role).filter(Boolean))).sort()
  const accounts = Array.from(new Map(teamCheckins.value.filter(item => item.userId || item.userName).map(item => [item.userId || item.userName, item])).values())
    .sort((a, b) => `${a.userName || ''}`.localeCompare(`${b.userName || ''}`))
    .map(item => item.userName || item.email || 'Unknown account')
    
  return [
    { key: 'status', label: 'Trạng thái', icon: 'fa-solid fa-calendar-check', values: ['Checked', 'Missing', 'Blocker'] },
    { key: 'role', label: 'Chức vụ / Role', icon: 'fa-solid fa-user-tag', values: roles },
    { key: 'account', label: 'Thành viên', icon: 'fa-solid fa-user', values: accounts }
  ]
})

const checkinOperators = {
  status: ['is', 'is not'],
  role: ['is', 'is not'],
  account: ['is', 'is not']
}

const customCheckinValueMeta = (fieldKey, value) => {
  if (fieldKey === 'status') {
    if (value === 'Checked') return { icon: 'fa-solid fa-circle-check', color: '#22c55e' }
    if (value === 'Missing') return { icon: 'fa-regular fa-circle', color: '#94a3b8' }
    if (value === 'Blocker') return { icon: 'fa-solid fa-triangle-exclamation', color: '#ef4444' }
  }
  if (fieldKey === 'role') {
    return { icon: 'fa-solid fa-user-tag', color: 'var(--color-text-secondary)' }
  }
  if (fieldKey === 'account') {
    return { icon: 'fa-solid fa-user', color: 'var(--color-text-secondary)' }
  }
  return null
}

// Sorting config
const checkinSortMode = ref('status')
const checkinSortDirection = ref('desc')
const checkinSortSearchQuery = ref('')

const checkinSortOptions = [
  { value: 'status', label: 'Trạng thái check-in', icon: 'fa-solid fa-circle-check' },
  { value: 'name', label: 'Tên thành viên', icon: 'fa-regular fa-user' },
  { value: 'role', label: 'Chức vụ / Role', icon: 'fa-solid fa-user-tag' },
  { value: 'account', label: 'Tài khoản', icon: 'fa-solid fa-at' },
  { value: 'project', label: 'Dự án', icon: 'fa-solid fa-folder' },
  { value: 'blocker', label: 'Khó khăn (Blocker)', icon: 'fa-solid fa-triangle-exclamation' },
  { value: 'checkinDate', label: 'Ngày check-in', icon: 'fa-regular fa-calendar' }
]

const filteredCheckinSortOptions = computed(() => {
  const q = checkinSortSearchQuery.value.trim().toLowerCase()
  if (!q) return checkinSortOptions
  return checkinSortOptions.filter(o => o.label.toLowerCase().includes(q))
})

const filteredTeamCheckins = computed(() => {
  const query = checkinSearch.value.trim().toLowerCase()

  let list = teamCheckins.value.filter(team => {
    // 1. Text Search Filter:
    if (query) {
      const matchText = [
        team.userName,
        team.role,
        team.projectName,
        team.projectKey,
        team.yesterday,
        team.today,
        team.blocker
      ]
        .filter(Boolean)
        .some(value => `${value}`.toLowerCase().includes(query))
      if (!matchText) return false
    }

    // 2. FilterBar active filters (AND match):
    if (activeCheckinFilters.value.length > 0) {
      return activeCheckinFilters.value.every(f => {
        let val = ''
        if (f.field === 'status') {
          if (f.value === 'Checked') val = team.checkedIn ? 'Checked' : ''
          else if (f.value === 'Missing') val = !team.checkedIn ? 'Missing' : ''
          else if (f.value === 'Blocker') val = team.blocker ? 'Blocker' : ''
        } else if (f.field === 'role') {
          val = team.role
        } else if (f.field === 'account') {
          val = team.userName || team.email || 'Unknown account'
        }
        
        const isMatch = `${val || ''}`.toLowerCase() === `${f.value || ''}`.toLowerCase()
        return f.operator === 'is' ? isMatch : !isMatch
      })
    }
    
    return true
  })

  // 3. Sorting
  return [...list].sort((left, right) => {
    let result
    if (checkinSortMode.value === 'name') result = `${left.userName || ''}`.localeCompare(`${right.userName || ''}`)
    else if (checkinSortMode.value === 'role') result = `${left.role || ''}`.localeCompare(`${right.role || ''}`)
    else if (checkinSortMode.value === 'account') result = `${left.email || left.userName || ''}`.localeCompare(`${right.email || right.userName || ''}`)
    else if (checkinSortMode.value === 'project') result = `${left.projectName || ''}`.localeCompare(`${right.projectName || ''}`)
    else if (checkinSortMode.value === 'blocker') result = Number(!!right.blocker) - Number(!!left.blocker)
    else if (checkinSortMode.value === 'checkinDate') result = new Date(left.checkinDate || left.checkedInAt || left.createdAt || 0).getTime() - new Date(right.checkinDate || right.checkedInAt || right.createdAt || 0).getTime()
    else result = Number(left.checkedIn) - Number(right.checkedIn) // Sort status: checked-in members first
    
    const statusSort = ['status', 'blocker'].includes(checkinSortMode.value)
    return statusSort
      ? (checkinSortDirection.value === 'asc' ? result * -1 : result)
      : (checkinSortDirection.value === 'asc' ? result : -result)
  })
})

const fetchCurrentUser = async () => {
  try {
    const res = await axiosClient.get('/users/me')
    if (res.data && res.data.data) {
      currentUser.value = res.data.data
    }
  } catch (error) {
    console.error('Cannot load current user profile:', error)
  }
}

const fetchProjectMembersAndCheckins = async () => {
  if (!activeProjectId.value) return

  try {
    const res = await axiosClient.get('/checkins', {
      params: { projectId: activeProjectId.value }
    })
    const payload = res.data?.data || {}
    teamCheckins.value = Array.isArray(payload.members) ? payload.members : []

    const meCard = teamCheckins.value.find(t => t.isCurrentUser)
    userCheckedIn.value = meCard ? meCard.checkedIn : false
  } catch (error) {
    console.error('Cannot load project members/checkins:', error)
    ElMessage.error(error.response?.data?.message || 'Không thể tải danh sách check-in.')
    teamCheckins.value = []
    userCheckedIn.value = false
  }
}

onMounted(async () => {
  document.addEventListener('click', handleOutsideClick)
  await fetchCurrentUser()
  try {
    const projects = await projectStore.fetchAllProjects(true)
    projectsList.value = projects
      .filter(project => project.isMember !== false)
      .map(project => ({
        id: project.id,
        key: project.key,
        name: project.name,
        isMember: project.isMember,
        originalRow: project.originalRow
      }))

    if (projectsList.value.length > 0) {
      const savedProjId = localStorage.getItem('active_checkin_project_id')
      if (savedProjId && projectsList.value.some(p => p.id === savedProjId)) {
        activeProjectId.value = savedProjId
      } else {
        activeProjectId.value = projectsList.value[0].id
      }
      await fetchProjectMembersAndCheckins()
    }
  } catch (error) {
    console.error('Cannot load projects:', error)
    ElMessage.error(error.response?.data?.message || 'Không thể tải danh sách dự án.')
  }
})

onBeforeUnmount(() => {
  document.removeEventListener('click', handleOutsideClick)
})

const selectProject = async (id) => {
  activeProjectId.value = id
  localStorage.setItem('active_checkin_project_id', id)
  aiSummaryText.value = ''
  await fetchProjectMembersAndCheckins()
}

const openCheckinModal = () => {
  form.value = {
    projectId: activeProjectId.value,
    yesterday: '',
    today: '',
    blocker: ''
  }
  checkinModalOpen.value = true
}

const submitCheckin = async () => {
  if (!form.value.projectId) {
    ElMessage.warning('Vui lòng chọn dự án báo cáo!')
    return
  }
  if (!form.value.yesterday.trim() || !form.value.today.trim()) {
    ElMessage.warning('Vui lòng điền đầy đủ thông tin ngày hôm qua và hôm nay!')
    return
  }

  try {
    await axiosClient.post('/checkins', {
      yesterday: form.value.yesterday,
      today: form.value.today,
      blocker: form.value.blocker,
      projectId: form.value.projectId
    })

    activeProjectId.value = form.value.projectId
    localStorage.setItem('active_checkin_project_id', form.value.projectId)
    await fetchProjectMembersAndCheckins()
    checkinModalOpen.value = false
    ElMessage.success('Gửi báo cáo Check-in ngày thành công!')
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể gửi báo cáo check-in.')
  }
}

const generateAiSummary = async () => {
  if (checkedInCount.value === 0) {
    ElMessage.warning('Không có báo cáo check-in hôm nay để tóm tắt!')
    aiSummaryText.value = 'Không có báo cáo check-in nào được nộp hôm nay.'
    return
  }
  aiLoading.value = true
  try {
    const res = await axiosClient.post('/checkins/ai-summary', {
      projectId: activeProjectId.value
    })
    if (res.data?.data?.summaryText) {
      aiSummaryText.value = res.data.data.summaryText
      ElMessage.success('Đã tạo tóm tắt check-in thành công!')
    }
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể tạo tóm tắt check-in.')
  } finally {
    aiLoading.value = false
  }
}

const renderMarkdown = (text) => {
  if (!text) return ''
  let html = text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/^### (.*$)/gim, '<h3 class="text-sm font-bold text-primary mt-1 mb-3 pb-2" style="border-bottom: 1px solid rgba(255,255,255,0.06);">$1</h3>')
    .replace(/\*\*(.*?)\*\*/g, '<strong style="color: var(--color-primary); font-weight: 600;">$1</strong>')
    .replace(/^(\d+)\.\s(.*$)/gim, '<div style="display: flex; align-items: flex-start; gap: 8px; margin-top: 10px; line-height: 1.6;"><span style="color: var(--color-accent); font-weight: 700;">$1.</span><span style="color: var(--color-text-secondary);">$2</span></div>')
    .replace(/\n/g, '<br>')
  return html
}
</script>

<style scoped>
.checkin-page {
  --sa-page-x: 18px;
  min-height: 100%;
  width: 100%;
  background: var(--color-bg);
  color: var(--color-text-primary);
  padding: 0 !important;
  margin: 0 !important;
}

.page-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 20px;
  padding: 22px var(--sa-page-x, 24px) 18px;
  background: var(--color-surface);
  border-bottom: none !important;
  margin-bottom: 0 !important;
}

.eyebrow {
  color: var(--color-accent);
  font-size: 10px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  display: block;
}

.page-header h1 {
  margin: 3px 0 4px;
  font-size: 22px;
  font-weight: 700;
  color: var(--color-text-primary);
  letter-spacing: 0;
  line-height: 1.2;
}

.page-header p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 12px;
}

.primary-action {
  min-height: 36px;
  border: 0;
  border-radius: 7px;
  padding: 0 14px;
  background: var(--color-accent);
  color: #fff;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  transition: opacity 0.15s ease;
}
.primary-action:hover {
  opacity: 0.9;
}

.page-content {
  padding: 0 var(--sa-page-x, 24px) 32px !important;
  max-width: none;
  margin: 0;
}

.checkin-page :deep(.project-page-toolbar) {
  margin: 0 0 18px !important;
  width: auto !important;
}

.ai-extract-header-btn {
  margin-left: 12px !important;
  height: 28px !important;
  border-radius: 6px !important;
  font-size: 12px !important;
  padding: 0 10px !important;
  display: inline-flex !important;
  align-items: center !important;
  gap: 5px !important;
  background-color: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
  color: var(--color-text-secondary) !important;
  font-weight: 600 !important;
  cursor: pointer !important;
  transition: all 0.15s ease !important;
}

.ai-extract-header-btn:hover {
  background-color: var(--color-surface-hover) !important;
  color: var(--color-text-primary) !important;
  border-color: var(--color-border-hover) !important;
}

/* Modal form input, textarea, label and modern footer button styles */
.sa-modal--form .field-label {
  font-size: 12px !important;
  font-weight: 650 !important;
  color: var(--color-text-secondary) !important;
  margin-bottom: 6px !important;
}

.sa-modal--form input,
.sa-modal--form textarea {
  box-sizing: border-box !important;
  width: 100% !important;
  border: 1px solid var(--color-border) !important;
  border-radius: 8px !important;
  background-color: var(--color-surface) !important;
  color: var(--color-text-primary) !important;
  font-size: 13.5px !important;
  font-family: inherit !important;
  padding: 10px 12px !important;
  outline: none !important;
  transition: border-color 0.2s, box-shadow 0.2s !important;
}

.sa-modal--form input:focus,
.sa-modal--form textarea:focus {
  border-color: var(--color-accent) !important;
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15) !important;
}

.checkin-modal-footer-btn {
  height: 36px !important;
  border-radius: 8px !important;
  font-size: 13.5px !important;
  font-weight: 600 !important;
  padding: 0 16px !important;
  display: inline-flex !important;
  align-items: center !important;
  gap: 6px !important;
  cursor: pointer !important;
  transition: all 0.15s ease !important;
}

.checkin-modal-footer-btn.cancel {
  background-color: transparent !important;
  border: 1px solid var(--color-border) !important;
  color: var(--color-text-secondary) !important;
}

.checkin-modal-footer-btn.cancel:hover {
  background-color: var(--color-surface-hover) !important;
  color: var(--color-text-primary) !important;
  border-color: var(--color-border-hover) !important;
}

.checkin-modal-footer-btn.submit {
  background-color: var(--color-accent) !important;
  border: 1px solid var(--color-accent) !important;
  color: #ffffff !important;
}

.checkin-modal-footer-btn.submit:hover {
  opacity: 0.9 !important;
}

/* Custom project select container */
.custom-project-select {
  width: min(220px, 24vw);
  flex: 0 0 min(220px, 24vw);
}

.custom-project-select :deep(.el-select__wrapper) {
  min-height: 34px;
  height: 34px;
  border-radius: 9px !important;
  border: 1px solid var(--color-border);
  box-shadow: none !important;
  background: var(--color-surface) !important;
  overflow: hidden;
  transition: border-color 0.2s, box-shadow 0.2s, background 0.2s;
}

.custom-project-select :deep(.el-select__wrapper:hover),
.custom-project-select :deep(.el-select__wrapper.is-focused) {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border));
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important;
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15) !important;
}

.custom-project-select :deep(.el-select__prefix) {
  color: var(--color-text-secondary);
  font-size: 13px;
}

.custom-project-select :deep(.el-select__placeholder),
.custom-project-select :deep(.el-select__selected-item) {
  font-size: 13px;
  font-weight: 600;
}

.checkin-search-field {
  position: relative;
  flex: 0 0 min(326px, 34vw);
  width: min(326px, 34vw);
  min-height: 34px;
  display: flex;
  align-items: center;
  color: var(--color-text-muted);
}

.checkin-search-field > i {
  position: absolute;
  left: 12px;
  top: 50%;
  z-index: 1;
  transform: translateY(-50%);
  color: var(--color-text-muted);
  font-size: 14px;
  pointer-events: none;
}

.checkin-search-field input {
  width: 100%;
  height: 34px !important;
  box-sizing: border-box !important;
  padding: 0 12px 0 36px !important;
  border: 1px solid var(--color-border) !important;
  border-radius: 9px !important;
  outline: 0;
  background: var(--color-surface) !important;
  color: var(--color-text-primary) !important;
  font-size: 13.5px !important;
  overflow: hidden;
  -webkit-appearance: none;
  appearance: none;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.checkin-search-field input:focus {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15);
}

/* Timeline Filter Trigger to match Work Items Filter Button */
:deep(.timeline-filter-trigger) {
  display: inline-flex !important;
  align-items: center !important;
  gap: 7px !important;
  height: 34px !important;
  padding: 0 14px !important;
  border: 1px solid var(--color-border) !important;
  border-radius: 9px !important;
  background: var(--color-surface) !important;
  color: var(--color-text-secondary) !important;
  font-size: 13px !important;
  font-weight: 600 !important;
  cursor: pointer !important;
  transition: all 0.2s ease !important;
}

:deep(.timeline-filter-trigger:hover) {
  background-color: var(--color-surface-hover) !important;
  border-color: var(--color-border-hover) !important;
  color: var(--color-text-primary) !important;
}

:deep(.timeline-filter-trigger.active) {
  background-color: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important;
  border-color: var(--color-accent) !important;
  color: var(--color-accent) !important;
}

.checkin-filter-count {
  color: var(--color-text-muted);
  font-size: 11px;
  white-space: nowrap;
}

.ai-summary-widget {
  margin-bottom: 20px !important;
  padding: 18px 22px !important;
  border-radius: 12px;
  border: 1px solid color-mix(in srgb, var(--color-accent) 22%, var(--color-border)) !important;
  background: linear-gradient(135deg, color-mix(in srgb, var(--color-surface) 96%, var(--color-accent)), var(--color-surface)) !important;
  box-shadow: 0 4px 20px color-mix(in srgb, var(--color-accent) 5%, transparent) !important;
}

.ai-response-box {
  background-color: var(--color-surface-hover);
  border: 1px solid var(--color-border);
  padding: 14px;
  border-radius: var(--radius-card);
}

.team-checkins-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 14px;
}

.checkin-card {
  display: flex;
  flex-direction: column;
  height: 100%;
  border-radius: 10px !important;
  border: 1px solid var(--color-border) !important;
  background-color: var(--color-surface) !important;
  transition: border-color 0.2s, box-shadow 0.2s, transform 0.2s !important;
}

.checkin-card:hover {
  border-color: var(--color-border-hover) !important;
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.04) !important;
  transform: translateY(-2px);
}

.checkin-card.not-checked {
  background-color: color-mix(in srgb, var(--color-surface) 95%, transparent) !important;
  border-style: dashed !important;
  opacity: 0.75;
}

.card-header {
  border-bottom: 1px solid var(--color-border);
  padding: 11px 14px;
}

.card-body {
  padding: 14px;
  flex: 1;
}

.section-label {
  font-size: 11px;
  font-weight: 700;
  color: var(--color-text-muted);
  text-transform: uppercase;
  display: block;
  margin-bottom: 2px;
}

.section-desc {
  font-size: 13px;
  color: var(--color-text-primary);
  line-height: 1.5;
}

.has-blocker {
  color: var(--color-danger);
  font-weight: 500;
}

:deep(.card-header .el-avatar) {
  margin: 0 !important;
  flex-shrink: 0;
}

@media (max-width: 860px) {
  .checkin-container {
    padding: 16px !important;
  }

  .checkin-container .page-header,
  .ai-summary-widget > .flex {
    align-items: flex-start !important;
    flex-direction: column !important;
  }
}

.team-checkins-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
  width: 100%;
}

.checkin-list-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  padding: 12px 18px !important;
  border-radius: var(--radius-card, 10px);
  background: var(--color-surface, #fff);
  border: 1px solid var(--color-border);
  transition: box-shadow 0.15s ease, border-color 0.15s ease;
}

.checkin-list-row:hover {
  border-color: var(--color-border-hover);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.03);
}

.checkin-list-row.not-checked {
  opacity: 0.65;
  border-style: dashed;
}

.clr-left {
  display: flex;
  align-items: center;
  gap: 10px;
  flex: 0 0 180px;
  min-width: 180px;
}

.clr-middle {
  flex: 1;
  min-width: 0;
}

.clr-right {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: 0 0 auto;
}

.clr-detail-item {
  display: flex;
  align-items: baseline;
  line-height: 1.4;
}

.clr-detail-item span {
  word-break: break-word;
}

/* View toggles */
.view-toggles {
  display: flex;
  align-items: center;
  gap: 4px;
}
.toggle-btn {
  width: 34px;
  height: 34px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 8px;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: all 0.15s ease;
}
.toggle-btn:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}
.toggle-btn.active {
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important;
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  color: var(--color-accent) !important;
}

/* Popover/dropdown menu custom styling */
.filter-dropdown-wrapper,
.display-dropdown-wrapper {
  position: relative;
  display: inline-block;
}

.plane-dropdown-menu {
  position: absolute;
  top: 100%;
  right: 0;
  margin-top: 8px;
  background: var(--color-surface-elevated);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  width: 260px;
  max-height: min(450px, calc(100vh - 180px));
  overflow-y: auto;
  box-shadow: var(--shadow-popover);
  z-index: 120;
  color: var(--color-text-primary);
  font-size: 13px;
  padding: 8px;
}

.filter-dropdown-menu {
  left: 0;
  right: auto;
  width: 640px;
  max-width: calc(100vw - 32px);
  max-height: none;
  overflow: visible;
  padding: 8px !important;
}

:deep(.filter-dropdown-menu .filter-bar-container) {
  border: none;
  background: transparent;
  padding: 0 !important;
  min-height: auto;
  box-shadow: none;
  overflow: visible;
}

/* Sort / Combobox inside popup */
.filter-combobox {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 5px;
  width: 100%;
}
.filter-label {
  display: flex;
  color: var(--color-text-secondary);
  font-size: 11px;
  font-weight: 750;
  letter-spacing: 0.02em;
  text-transform: uppercase;
}
.filter-select-trigger {
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 10px;
  width: 100%;
  height: 36px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  color: var(--color-text-primary);
  padding: 0 12px;
  outline: none;
  font-size: 13px;
  cursor: pointer;
  transition: border-color 0.15s ease, background-color 0.15s ease;
}
.filter-select-trigger:hover,
.filter-select-trigger.active {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  background: color-mix(in srgb, var(--color-accent) 6%, var(--color-surface)) !important;
  color: var(--color-accent) !important;
  box-shadow: none !important;
}
.sort-combobox-trigger:hover,
.sort-combobox-trigger.active {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  background: color-mix(in srgb, var(--color-accent) 6%, var(--color-surface)) !important;
  color: var(--color-accent) !important;
  box-shadow: none !important;
}
.filter-select-trigger:hover > i,
.filter-select-trigger.active > i,
.sort-combobox-trigger:hover i,
.sort-combobox-trigger.active i {
  color: var(--color-accent) !important;
}
.filter-select-trigger:hover > span,
.filter-select-trigger.active > span,
.sort-combobox-trigger:hover span,
.sort-combobox-trigger.active span {
  color: var(--color-accent) !important;
}
.filter-select-trigger span {
  flex: 1;
  text-align: left;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.filter-select-trigger i {
  color: var(--color-text-secondary);
}
.filter-select-menu {
  position: absolute;
  left: 0;
  right: 0;
  top: calc(100% + 4px);
  z-index: 120;
  max-height: 220px;
  overflow-y: auto;
  padding: 6px !important;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface-elevated);
  box-shadow: var(--shadow-popover);
  display: flex;
  flex-direction: column;
  gap: 0 !important;
}
.filter-select-option {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 8px;
  width: 100%;
  min-height: 32px !important;
  padding: 5px 9px !important;
  margin: 0 !important;
  border: 0;
  border-left: 4px solid transparent !important;
  border-radius: 8px !important;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 13px;
  font-weight: 500;
  text-align: left;
  cursor: pointer;
  transition: background-color 0.15s ease, border-color 0.15s ease, color 0.15s ease;
}
.filter-select-option:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}
.filter-select-option.selected {
  background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface)) !important;
  border-left-color: var(--color-accent) !important;
  border-radius: 8px !important;
  color: var(--color-accent);
  font-weight: 650;
}
.filter-select-option.selected:hover {
  background: color-mix(in srgb, var(--color-accent) 18%, var(--color-surface)) !important;
  color: var(--color-accent);
}
.filter-select-option > span {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  text-align: left;
}
.filter-select-option > i:first-child {
  width: 15px;
  color: currentColor;
  font-size: 12px;
  text-align: center;
}

/* Sort Search field styling matching FilterBar */
.filter-search-field {
  position: relative;
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  min-height: 34px;
  height: 34px;
  box-sizing: border-box;
  border: 1px solid var(--color-border);
  border-radius: 9px;
  background: var(--color-surface);
  padding: 0 12px;
  color: var(--color-text-muted);
  transition: border-color 0.2s, box-shadow 0.2s;
}
.filter-search-icon {
  position: static;
  transform: none;
  width: 16px;
  height: 16px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: 0 0 16px;
  font-size: 14px;
  pointer-events: none;
  color: var(--color-text-muted);
}
.filter-search-input {
  width: 100% !important;
  height: 100% !important;
  box-sizing: border-box !important;
  min-width: 0 !important;
  border: 0 !important;
  border-radius: 0 !important;
  background: transparent !important;
  color: var(--color-text-primary) !important;
  padding: 0 !important;
  outline: none !important;
  font-size: 13.5px !important;
  line-height: 34px !important;
  text-indent: 0 !important;
  appearance: none;
}
.filter-search-input::placeholder {
  color: var(--color-text-muted);
}
.filter-search-field:focus-within {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.14);
}

/* Mini direction buttons next to selected sort item */
.dir-mini-btn {
  width: 30px;
  min-width: 30px;
  height: 30px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: all 0.15s ease;
}
.dir-mini-btn:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}
.dir-mini-btn.active {
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important;
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  color: var(--color-accent) !important;
  font-weight: 600 !important;
}
</style>

<style>
/* Non-scoped styles for custom project dropdown options and select input overrides */
body .custom-project-select .el-input__wrapper {
  background-color: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
  box-shadow: none !important;
  border-radius: 9px !important;
  padding: 0 12px !important;
  height: 34px !important;
  min-height: 34px !important;
  transition: border-color 0.2s, box-shadow 0.2s, background 0.2s !important;
}

body .custom-project-select .el-input__wrapper:hover {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  background-color: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important;
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15) !important;
}

body .custom-project-select .el-input__wrapper.is-focus {
  border-color: var(--color-accent) !important;
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15) !important;
}

body .custom-project-select .el-input__inner {
  color: var(--color-text-primary) !important;
  font-weight: 550 !important;
  font-size: 13px !important;
}

.custom-project-dropdown {
  background-color: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.45) !important;
  border-radius: 10px !important;
  padding: 4px 0 !important;
}

.custom-project-dropdown .el-select-dropdown__item {
  border-radius: 6px !important;
  margin: 3px 6px !important;
  padding: 8px 12px !important;
  height: auto !important;
  line-height: 1.4 !important;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1) !important;
  color: var(--color-text-secondary) !important;
  font-weight: 500 !important;
  font-size: 13px !important;
}

body .custom-project-dropdown .el-select-dropdown__item.hover,
body .custom-project-dropdown .el-select-dropdown__item:hover,
body .custom-project-dropdown .el-select-dropdown__item.is-hovering {
  background-color: color-mix(in srgb, var(--color-primary) 15%, transparent) !important;
  background: color-mix(in srgb, var(--color-primary) 15%, transparent) !important;
  color: #ffffff !important;
  transform: translateX(4px) !important;
}

body .custom-project-dropdown .el-select-dropdown__item.selected {
  background-color: var(--color-primary) !important;
  background: var(--color-primary) !important;
  color: #ffffff !important;
  font-weight: 600 !important;
}

body .custom-project-dropdown .el-select-dropdown__item.selected.hover,
body .custom-project-dropdown .el-select-dropdown__item.selected:hover,
body .custom-project-dropdown .el-select-dropdown__item.selected.is-hovering {
  background-color: color-mix(in srgb, var(--color-primary) 85%, #000000) !important;
  background: color-mix(in srgb, var(--color-primary) 85%, #000000) !important;
  color: #ffffff !important;
  transform: translateX(4px) !important;
}

.custom-project-dropdown .el-popper__arrow::before {
  background-color: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
}

/* Empty State Styles */
.empty-spaces-flat {
  min-height: 204px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 24px 26px;
  background: transparent;
  border: 0;
  box-shadow: none;
  text-align: center;
}

.empty-spaces-icon {
  width: 54px;
  height: 54px;
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid color-mix(in srgb, var(--color-accent) 18%, transparent);
  border-radius: 14px;
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface));
  color: var(--color-accent);
  font-size: 23px;
  box-shadow: 0 14px 30px rgba(14, 165, 233, 0.12);
}

.empty-spaces-copy {
  max-width: 380px;
}

.empty-spaces-copy h3 {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 15px;
  font-weight: 800;
  line-height: 1.35;
}

.empty-spaces-copy p {
  margin: 3px 0 0;
  color: var(--color-text-muted);
  font-size: 13px;
  line-height: 1.4;
}
</style>
