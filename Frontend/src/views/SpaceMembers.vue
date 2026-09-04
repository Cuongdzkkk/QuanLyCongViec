<template>
  <ProjectPageContainer class="space-members-view">
    <ProjectPageHeader
      icon="fa-solid fa-users"
      title="Quản lý thành viên & Đội ngũ"
      description="Quản lý thành viên, team và quyền truy cập trong Space"
    >
      <template #actions>
        <button v-if="activeTab === 'members'" class="nexus-btn-primary" @click="showAddMemberModal = true">
          <i class="fa-solid fa-plus"></i> Thêm thành viên
        </button>
        <button v-if="activeTab === 'teams'" class="nexus-btn-primary" @click="openLinkTeamModal">
          <i class="fa-solid fa-link"></i> Liên kết Team
        </button>
      </template>
    </ProjectPageHeader>

    <el-tabs v-model="activeTab" class="nexus-tabs">
      <!-- TAB 1: MEMBERS -->
      <el-tab-pane label="Danh sách thành viên" name="members">
        <ProjectPageToolbar
          :showSearch="true"
          v-model:searchQuery="searchQuery"
          searchPlaceholder="Tìm kiếm thành viên theo tên/email..."
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
                  :fields="memberFilterFields"
                  :operators="memberOperators"
                  :custom-value-meta="customMemberValueMeta"
                  :active="showFilterDropdown"
                />
              </div>
            </div>
          </template>
          <template #sort>
            <ToolbarSortMenu v-model="memberSortBy" v-model:direction="memberSortDirection" label="Sort members" :options="memberSortOptions" />
          </template>
        </ProjectPageToolbar>

        <div v-if="loadingMembers" class="loading-state">
          <el-icon class="is-loading"><Loading /></el-icon> Đang tải dữ liệu...
        </div>
        <div v-else-if="filteredMembers.length === 0" class="empty-state">
          <i class="fa-solid fa-users-slash empty-icon"></i>
          <p>Không tìm thấy thành viên nào phù hợp.</p>
        </div>
        <div v-else class="table-container">
        <el-table border v-resizable :data="filteredMembers" style="width: 100%" class="nexus-table">
          <el-table-column min-width="200">
            <template #header>
              <i class="fa-solid fa-user-group"></i> Thành viên
            </template>
            <template #default="{ row }">
              <div class="member-info cursor-pointer flex items-center gap-3" @click="goToMemberProfile(row.userId)">
                <UserAvatar :user="row" :size="28" :fontSize="12" :clickable="false" />
                <div class="member-details">
                  <span class="member-name hover:text-blue-600 hover:underline" style="font-weight: 700; font-size: 13px; color: var(--color-text-primary);">{{ row.fullName || row.email }}</span>
                  <span class="member-email" style="font-size: 12px; color: #5E6C84;">{{ row.email }}</span>
                </div>
              </div>
            </template>
          </el-table-column>

          <el-table-column min-width="200">
            <template #header>
              <i class="fa-solid fa-people-group"></i> Team hiện tại
            </template>
            <template #default="{ row }">
              <div v-if="row.teams && row.teams.length > 0" class="flex flex-wrap gap-1">
                <el-tag v-for="team in row.teams" :key="team.id" size="small" type="info" class="mb-1">
                  {{ team.name }}
                </el-tag>
              </div>
              <span v-else class="text-sm text-gray-400 italic">Chưa có team</span>
            </template>
          </el-table-column>

          <el-table-column width="180">
            <template #header>
              <i class="fa-solid fa-user-lock"></i> Vai trò
            </template>
            <template #default="{ row }">
              <el-select
                v-model="row.projectRole"
                size="small"
                popper-class="members-role-popper"
                @change="(newRole) => updateMemberRole(row.userId, newRole)"
                :disabled="isCurrentUser(row.userId)"
              >
                <el-option
                  v-for="role in roleOptions"
                  :key="role.value"
                  :label="role.label"
                  :value="role.value"
                />
              </el-select>
            </template>
          </el-table-column>

          <el-table-column width="150">
            <template #header>
              <i class="fa-regular fa-calendar"></i> Ngày tham gia
            </template>
            <template #default="{ row }">
              <span class="text-sm text-gray-600">{{ formatDate(row.joinedAt) }}</span>
            </template>
          </el-table-column>

          <el-table-column width="80" align="right">
            <template #default="{ row }">
              <el-dropdown trigger="click" placement="bottom-end">
                <el-button text size="small" aria-label="Member actions" title="Member actions">
                  <i class="fa-solid fa-ellipsis"></i>
                </el-button>
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item @click="removeMember(row.userId)" class="text-red-500">
                      <i class="fa-solid fa-user-xmark mr-2"></i> Xóa khỏi dự án
                    </el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
            </template>
          </el-table-column>
        </el-table>
        </div>
      </el-tab-pane>

      <!-- TAB 2: TEAMS -->
      <el-tab-pane label="Danh sách team" name="teams">
        <ProjectPageToolbar
          :showSearch="true"
          v-model:searchQuery="teamSearchQuery"
          searchPlaceholder="Tìm kiếm team..."
        >
          <template #sort>
            <ToolbarSortMenu v-model="teamSortBy" v-model:direction="teamSortDirection" label="Sort teams" :options="teamSortOptions" />
          </template>
        </ProjectPageToolbar>
        <div v-if="loadingTeams" class="loading-state">
          <el-icon class="is-loading"><Loading /></el-icon> Đang phân tích dữ liệu phòng ban...
        </div>
        <div v-else-if="linkedTeams.length === 0" class="empty-state">
          <i class="fa-solid fa-users-rectangle empty-icon"></i>
          <p>Chưa có team nào được liên kết với dự án này.</p>
          <el-button type="primary" plain class="mt-4" @click="openLinkTeamModal">Liên kết Team ngay</el-button>
        </div>
        <div v-else class="table-container">
        <el-table :data="linkedTeams" style="width: 100%" class="nexus-table">
          <el-table-column min-width="220">
            <template #header>
              <i class="fa-solid fa-people-group"></i> Tên Đội ngũ / Team
            </template>
            <template #default="{ row }">
              <div class="flex items-center">
                <el-avatar :size="28" shape="square" :src="row.coverImage" class="bg-blue-100 text-blue-600 font-bold">
                  {{ row.name ? row.name.substring(0,2).toUpperCase() : 'T' }}
                </el-avatar>
                <div class="flex flex-col ml-5">
                  <span class="font-bold text-gray-900" style="font-size: 13px; color: var(--color-text-primary);">{{ row.name }}</span>
                  <span class="text-xs text-gray-500">{{ row.description || 'Không có mô tả' }}</span>
                </div>
              </div>
            </template>
          </el-table-column>

          <el-table-column width="160">
            <template #header>
              <i class="fa-solid fa-user-lock"></i> Vai trò / Quyền
            </template>
            <template #default="{ row }">
              <el-tag size="small" :type="row.isDirectlyLinked ? 'primary' : 'info'" effect="plain">
                {{ row.linkedRole || (row.isDirectlyLinked ? 'Team' : 'Thành viên độc lập') }}
              </el-tag>
            </template>
          </el-table-column>

          <el-table-column width="150">
            <template #header>
              <i class="fa-solid fa-user-group"></i> Thành viên
            </template>
            <template #default="{ row }">
              <div class="flex items-center gap-2">
                <el-tag size="small" type="info"><i class="fa-solid fa-user mr-1"></i> {{ row.projectMemberCount }}/{{ row.totalMemberCount }}</el-tag>
                <div class="flex -space-x-2 overflow-hidden ml-1" v-if="row.projectMembers && row.projectMembers.length > 0">
                  <UserAvatar v-for="user in row.projectMembers.slice(0, 3)" :key="user.id" :user="user" :size="24" :fontSize="10" class="border border-white" />
                  <div v-if="row.projectMembers.length > 3" class="z-10 flex items-center justify-center w-6 h-6 rounded-full bg-gray-100 border border-white text-[10px] font-medium text-gray-500">
                    +{{ row.projectMembers.length - 3 }}
                  </div>
                </div>
              </div>
            </template>
          </el-table-column>

          <el-table-column width="200">
            <template #header>
              <i class="fa-solid fa-user-tie"></i> Quản lý
            </template>
            <template #default="{ row }">
              <div class="flex items-center gap-2 cursor-pointer" v-if="row.manager" @click="goToMemberProfile(row.manager.id || row.manager.userId)">
                <UserAvatar :user="row.manager" :size="24" :fontSize="10" :clickable="false" />
                <span class="text-sm text-gray-700 hover:text-blue-600 hover:underline">{{ row.manager.name }}</span>
              </div>
              <span v-else class="text-sm text-gray-400 italic">Chưa có</span>
            </template>
          </el-table-column>

          <el-table-column width="80" align="right">
            <template #default="{ row }">
              <el-dropdown trigger="click" placement="bottom-end">
                <el-button text size="small">
                  <i class="fa-solid fa-ellipsis"></i>
                </el-button>
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item v-if="row.isDirectlyLinked" @click="unlinkTeam(row.id)" class="text-red-500">
                      <i class="fa-solid fa-link-slash mr-2"></i> Hủy liên kết
                    </el-dropdown-item>
                    <el-dropdown-item v-else disabled>
                      <i class="fa-solid fa-info-circle mr-2"></i> Team hiển thị do có thành viên
                    </el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
            </template>
          </el-table-column>
        </el-table>
        </div>
      </el-tab-pane>
    </el-tabs>

    <!-- Modal Mời Thành Viên -->
    <el-dialog v-model="showAddMemberModal" width="560px" destroy-on-close append-to-body class="sa-data-dialog sa-modal--form" :show-close="false">
      <template #header>
        <DataModalHeader
          icon="bi bi-person-plus"
          title="Mời thành viên vào dự án"
          description="Chọn thành viên, phương thức mời và vai trò trong dự án"
          @close="showAddMemberModal = false"
        />
      </template>
      <DataModalSection icon="bi bi-person-lines-fill" title="Thông tin thành viên">
      <el-tabs v-model="inviteTab" class="nexus-tabs-small mb-4">
        <el-tab-pane label="Thêm thành viên Workspace" name="system">
          <div class="mt-2">
            <label class="block text-sm font-medium mb-1">Thành viên</label>
            <el-select
              v-model="inviteForm.systemUserId"
              filterable
              remote
              reserve-keyword
              placeholder="Tìm người trong Workspace..."
              :remote-method="searchSystemUsers"
              :loading="isSearchingUsers"
              class="w-full"
            >
                <el-option
                v-for="user in systemUsers"
                :key="user.userId"
                :label="user.fullName || user.email"
                :value="user.userId"
                style="height: auto; padding: 4px 8px;"
              >
                <div class="flex items-center">
                  <UserAvatar :user="user" :size="26" :fontSize="10" :clickable="false" />
                  <div class="flex flex-col leading-none ml-4">
                    <span class="text-[13px] text-gray-800 font-medium">{{ user.fullName || user.email }}</span>
                    <span class="text-[11px] text-gray-500 mt-1" v-if="user.fullName">{{ user.email }}</span>
                  </div>
                </div>
              </el-option>
            </el-select>
          </div>
        </el-tab-pane>
        <el-tab-pane label="Mời qua Email" name="email">
          <div class="mt-2">
            <label class="block text-sm font-medium mb-1">Email thành viên ngoài</label>
            <el-input v-model="inviteForm.email" placeholder="Nhập email..." />
          </div>
        </el-tab-pane>
      </el-tabs>
      </DataModalSection>

      <DataModalSection icon="bi bi-person-badge" title="Vai trò và quyền">
      <DataModalField label="Vai trò" helper="Vai trò quyết định quyền thao tác của thành viên trong dự án">
        <el-select v-model="inviteForm.role" class="w-full">
                <el-option
            v-for="role in roleOptions"
            :key="role.value"
            :label="role.label"
            :value="role.value"
          />
        </el-select>
      </DataModalField>
      </DataModalSection>
      <template #footer>
        <span class="dialog-footer">
          <el-button class="cancel-btn" @click="showAddMemberModal = false"><i class="bi bi-x-lg"></i> Hủy</el-button>
          <el-button type="primary" @click="inviteMember" :loading="isInviting"><i class="fa-solid fa-plus"></i>{{ inviteTab === 'system' ? ' Thêm thành viên' : ' Gửi lời mời' }}</el-button>
        </span>
      </template>
    </el-dialog>

    <!-- Modal Liên kết Team -->
    <el-dialog v-model="showAddTeamModal" width="560px" destroy-on-close append-to-body class="sa-data-dialog sa-modal--form" :show-close="false">
      <template #header>
        <DataModalHeader
          icon="bi bi-people"
          title="Liên kết Team phụ trách"
          description="Chọn team sẽ chịu trách nhiệm chính cho dự án"
          @close="showAddTeamModal = false"
        />
      </template>
      <DataModalSection icon="bi bi-search" title="Tìm và chọn Team">
      <DataModalField label="Tìm kiếm Team">
        <el-input v-model="linkTeamQuery" clearable placeholder="Tìm theo tên hoặc mô tả Team...">
          <template #prefix><i class="bi bi-search"></i></template>
        </el-input>
      </DataModalField>
      </DataModalSection>
      <DataModalSection icon="bi bi-people" title="Danh sách Team" description="Chọn Team chịu trách nhiệm chính cho dự án">
      <div v-if="allTeams.length === 0" class="text-center py-4 text-gray-500">
        <el-icon class="is-loading mr-2" v-if="loadingAllTeams"><Loading /></el-icon>
        <span v-if="loadingAllTeams">Đang tải danh sách team...</span>
        <span v-else>Không có team nào trong hệ thống.</span>
      </div>
      <div v-else class="team-selection-list">
        <div
          v-for="team in availableTeamsToLink"
          :key="team.id"
          class="team-option"
          :class="{ 'is-selected': selectedTeamToLink === team.id }"
          @click="selectedTeamToLink = team.id"
        >
          <el-avatar :size="36" shape="square" class="bg-blue-100 text-blue-600 mr-3">
            {{ team.name ? team.name.substring(0,2).toUpperCase() : 'T' }}
          </el-avatar>
          <div class="flex-1">
            <div class="font-medium text-gray-900">{{ team.name }}</div>
            <div class="text-xs text-gray-500">{{ team.memberCount }} thành viên</div>
          </div>
          <i class="fa-solid fa-circle-check text-blue-600 text-lg" v-if="selectedTeamToLink === team.id"></i>
        </div>
        <div v-if="availableTeamsToLink.length === 0" class="text-center py-4 text-gray-500">
          Tất cả các team đã được liên kết với dự án này.
        </div>
      </div>
      </DataModalSection>
      <template #footer>
        <span class="dialog-footer">
          <el-button class="cancel-btn" @click="showAddTeamModal = false"><i class="bi bi-x-lg"></i> Hủy</el-button>
          <el-button type="primary" @click="linkSelectedTeam" :loading="isLinking" :disabled="!selectedTeamToLink"><i class="fa-solid fa-link"></i> Liên kết Team</el-button>
        </span>
      </template>
    </el-dialog>
  </ProjectPageContainer>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import UserAvatar from '@/components/common/UserAvatar.vue'
import { Loading } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getStoredUser } from '@/utils/permissions'
import { useProjectStore } from '@/store/useProjectStore'
import ProjectPageContainer from '@/components/common/ProjectPageContainer.vue'
import ProjectPageHeader from '@/components/common/ProjectPageHeader.vue'
import ProjectPageToolbar from '@/components/common/ProjectPageToolbar.vue'
import ToolbarValueFilter from '@/components/common/ToolbarValueFilter.vue'
import ToolbarSortMenu from '@/components/common/ToolbarSortMenu.vue'
import { useI18n } from '@/composables/useI18n'
import { signalRService } from '@/api/signalrService'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import DataModalField from '@/components/common/Foundation/DataModalField.vue'

const route = useRoute()
const router = useRouter()
const projectStore = useProjectStore()
const projectId = computed(() => route.params.id)

const goToMemberProfile = (userId) => {
  if (!userId) return
  router.push(`/space/${route.params.spaceSlug}/${projectId.value}/profile/${userId}`)
}

const currentUser = getStoredUser()
const { isVietnamese } = useI18n()

const activeTab = ref('members')
const members = ref([])
const loadingMembers = ref(false)
const searchQuery = ref('')

const activeFilters = ref([])

const memberFilterFields = computed(() => [
  { key: 'team', label: 'Team membership', icon: 'fa-solid fa-users', values: ['All members', 'Has team', 'No team'] },
  { key: 'role', label: 'Role', icon: 'fa-solid fa-user-tag', values: roleOptions.value.map(role => role.label) }
])

const memberOperators = {
  team: ['is', 'is not'],
  role: ['is', 'is not']
}

const customMemberValueMeta = (fieldKey, value) => {
  if (fieldKey === 'team') {
    return { icon: 'fa-solid fa-users', color: '#3b82f6' }
  }
  if (fieldKey === 'role') {
    return { icon: 'fa-solid fa-user-tag', color: '#10b981' }
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

const teamSearchQuery = ref('')
const memberSortBy = ref('name')
const teamSortBy = ref('name')
const memberSortDirection = ref('asc')
const teamSortDirection = ref('asc')
const memberSortOptions = [
  { value: 'name', label: 'Member name', icon: 'fa-solid fa-user' },
  { value: 'role', label: 'Role', icon: 'fa-solid fa-user-tag' },
  { value: 'teamCount', label: 'Team count', icon: 'fa-solid fa-users' },
  { value: 'joinedAt', label: 'Joined date', icon: 'fa-solid fa-calendar-days' }
]
const teamSortOptions = [
  { value: 'name', label: 'Team name', icon: 'fa-solid fa-users' },
  { value: 'projectMemberCount', label: 'Project members', icon: 'fa-solid fa-user-group' },
  { value: 'totalMemberCount', label: 'Total members', icon: 'fa-solid fa-people-group' },
  { value: 'linkedRole', label: 'Project role', icon: 'fa-solid fa-user-tag' }
]

const allTeamsFull = ref([]) // Stores detailed info of all teams
const linkedTeams = computed(() => {
  return allTeamsFull.value.filter(team => {
    const isDirectlyLinked = team.projects && team.projects.some(p => p.id === projectId.value);
    const membersInProject = team.members.filter(m => members.value.some(pm => pm.userId === m.id));
    return isDirectlyLinked || membersInProject.length > 0;
  }).map(team => {
    const isDirectlyLinked = team.projects && team.projects.some(p => p.id === projectId.value);
    const linkedRole = isDirectlyLinked ? team.projects.find(p => p.id === projectId.value).roleName : null;

    // Nếu mời nguyên team (isDirectlyLinked), thì tất cả thành viên trong team coi như đều có mặt 100% (ví dụ: 7/7)
    // Nếu không (chỉ mời lẻ tẻ), thì đếm những người có mặt thực tế trong danh sách project members.
    const displayMembers = isDirectlyLinked
      ? team.members
      : team.members.filter(m => members.value.some(pm => pm.userId === m.id));

    return {
      ...team,
      isDirectlyLinked,
      linkedRole,
      projectMemberCount: displayMembers.length,
      totalMemberCount: team.members.length,
      projectMembers: displayMembers
    };
  }).filter(team => {
    if (teamSearchQuery.value && !team.name.toLowerCase().includes(teamSearchQuery.value.toLowerCase())) {
        return false;
    }
    return true;
  }).sort((left, right) => {
    let result
    if (teamSortBy.value === 'projectMemberCount' || teamSortBy.value === 'totalMemberCount') {
      result = (Number(left[teamSortBy.value]) || 0) - (Number(right[teamSortBy.value]) || 0)
    } else {
      result = `${left[teamSortBy.value] || ''}`.localeCompare(`${right[teamSortBy.value] || ''}`)
    }
    return teamSortDirection.value === 'asc' ? result : -result
  });
})
const loadingTeams = ref(false)

const allTeams = ref([])
const loadingAllTeams = ref(false)
const selectedTeamToLink = ref(null)
const linkTeamQuery = ref('')

const showAddMemberModal = ref(false)
const showAddTeamModal = ref(false)
const isInviting = ref(false)
const isLinking = ref(false)

const inviteForm = ref({
  email: '',
  systemUserId: '',
  role: 'Developer',
  message: ''
})

const inviteTab = ref('system')
const isSearchingUsers = ref(false)
const systemUsers = ref([])

const fetchDefaultUsers = async () => {
  isSearchingUsers.value = true
  try {
    const res = await axiosClient.get(`/projects/${projectId.value}/members/member-candidates`, { params: { page: 1, pageSize: 50 } })
    const allUsers = res.data?.data || []
    systemUsers.value = allUsers
  } catch (error) {
    console.error('Lỗi khi fetch users:', error)
  } finally {
    isSearchingUsers.value = false
  }
}

const searchSystemUsers = async (query) => {
  if (query !== '') {
    isSearchingUsers.value = true
    try {
      const res = await axiosClient.get(`/projects/${projectId.value}/members/member-candidates`, { params: { search: query, page: 1, pageSize: 50 } })
      const allUsers = res.data?.data || []
      systemUsers.value = allUsers
    } catch (error) {
      console.error(error)
    } finally {
      isSearchingUsers.value = false
    }
  } else {
    fetchDefaultUsers()
  }
}

watch(showAddMemberModal, (val) => {
  if (val && inviteTab.value === 'system') {
    fetchDefaultUsers()
  }
})

const roleOptions = computed(() => isVietnamese.value ? [
  { label: 'Quản lý dự án (PM)', value: 'PM' },
  { label: 'Chủ sản phẩm (PO)', value: 'PO' },
  { label: 'Trưởng dự án', value: 'Project Lead' },
  { label: 'Lập trình viên', value: 'Developer' },
  { label: 'Kiểm thử (QA)', value: 'QA' },
  { label: 'Thành viên', value: 'Member' }
] : [
  { label: 'Project Manager (PM)', value: 'PM' },
  { label: 'Product Owner (PO)', value: 'PO' },
  { label: 'Project Lead', value: 'Project Lead' },
  { label: 'Developer', value: 'Developer' },
  { label: 'QA', value: 'QA' },
  { label: 'Member', value: 'Member' }
])

const normalizeMemberRole = (value) => {
  const role = String(value || '').toUpperCase().replace(/[-\s]+/g, '_')
  if (role === 'PROJECT_MANAGER' || role === 'PM') return 'PM'
  if (role === 'PRODUCT_OWNER' || role === 'PO') return 'PO'
  if (role === 'PROJECT_LEAD') return 'Project Lead'
  if (role === 'DEVELOPER' || role === 'DEV') return 'Developer'
  if (role === 'QA') return 'QA'
  return 'Member'
}

const fetchMembers = async () => {
  loadingMembers.value = true
  try {
    const res = await axiosClient.get(`/projects/${projectId.value}/members`)
    members.value = (res.data?.data || []).map(member => ({
      ...member,
      projectRole: normalizeMemberRole(member.projectRole || member.ProjectRole || member.role)
    }))
  } catch (error) {
    ElMessage.error('Không thể tải danh sách thành viên.')
  } finally {
    loadingMembers.value = false
  }
}

// Lấy danh sách các team đã liên kết bằng cách lấy full thông tin team
const fetchLinkedTeams = async () => {
  loadingTeams.value = true
  try {
    const res = await axiosClient.get('/departments')
    const deps = res.data?.data || []
    allTeams.value = deps // For linking modal

    const fullTeams = []
    await Promise.all(deps.map(async (dep) => {
      try {
        const detailRes = await axiosClient.get(`/departments/${dep.id}/full`)
        if (detailRes.data?.data) {
          fullTeams.push(detailRes.data.data)
        }
      } catch (err) {
        // Bỏ qua lỗi 404 hoặc phân quyền
      }
    }))

    allTeamsFull.value = fullTeams
  } catch (error) {
    console.error('Lỗi khi fetch teams:', error)
  } finally {
    loadingTeams.value = false
  }
}

const openLinkTeamModal = async () => {
  selectedTeamToLink.value = null
  showAddTeamModal.value = true
  loadingAllTeams.value = true
  try {
    const res = await axiosClient.get('/departments')
    allTeams.value = res.data?.data || []
  } catch (error) {
    ElMessage.error('Không thể tải danh sách team từ hệ thống.')
  } finally {
    loadingAllTeams.value = false
  }
}

const availableTeamsToLink = computed(() => {
  // Lọc ra các team chưa được liên kết trực tiếp
  const linkedIds = linkedTeams.value.filter(t => t.isDirectlyLinked).map(t => t.id)
  const query = linkTeamQuery.value.trim().toLowerCase()
  return allTeams.value.filter(t =>
    !linkedIds.includes(t.id) &&
    (!query || `${t.name || ''} ${t.description || ''}`.toLowerCase().includes(query))
  )
})

const linkSelectedTeam = async () => {
  if (!selectedTeamToLink.value) return
  isLinking.value = true
  try {
    await axiosClient.post(`/departments/${selectedTeamToLink.value}/projects/${projectId.value}`)
    ElMessage.success('Đã liên kết team thành công.')
    showAddTeamModal.value = false
    await fetchLinkedTeams()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể liên kết team.')
  } finally {
    isLinking.value = false
  }
}

const unlinkTeam = async (teamId) => {
  ElMessageBox.confirm(
    'Bạn có chắc chắn muốn hủy liên kết team này khỏi dự án?',
    'Xác nhận',
    {
      confirmButtonText: 'Hủy liên kết',
      cancelButtonText: 'Đóng',
      type: 'warning'
    }
  ).then(async () => {
    try {
      await axiosClient.delete(`/departments/${teamId}/projects/${projectId.value}`)
      ElMessage.success('Đã hủy liên kết team.')
      await fetchLinkedTeams()
    } catch (error) {
      ElMessage.error(error.response?.data?.message || 'Không thể hủy liên kết.')
    }
  }).catch(() => {})
}

const filteredMembers = computed(() => {
  return members.value.map(member => {
    // Tìm team của member từ allTeamsFull
    const userTeams = allTeamsFull.value.filter(t => t.members && t.members.some(m => m.id === member.userId));
    return {
      ...member,
      teams: userTeams
    };
  }).filter(m => {
    const matchSearch = (m.fullName || '').toLowerCase().includes(searchQuery.value.toLowerCase()) ||
                        (m.email || '').toLowerCase().includes(searchQuery.value.toLowerCase())
    if (!matchSearch) return false

    if (activeFilters.value.length > 0) {
      return activeFilters.value.every(f => {
        let isMatch = false
        if (f.field === 'team') {
          const hasTeam = m.teams && m.teams.length > 0
          if (f.value === 'All members') isMatch = true
          else if (f.value === 'Has team') isMatch = hasTeam
          else if (f.value === 'No team') isMatch = !hasTeam
        } else if (f.field === 'role') {
          const option = roleOptions.value.find(ro => ro.label === f.value)
          const roleValue = option ? option.value : f.value
          isMatch = m.projectRole === roleValue
        }
        return f.operator === 'is' ? isMatch : !isMatch
      })
    }

    return true
  }).sort((left, right) => {
    let result
    if (memberSortBy.value === 'teamCount') {
      result = (left.teams?.length || 0) - (right.teams?.length || 0)
    } else if (memberSortBy.value === 'joinedAt') {
      result = new Date(left.joinedAt || 0).getTime() - new Date(right.joinedAt || 0).getTime()
    } else {
      const leftValue = memberSortBy.value === 'role' ? left.projectRole : (left.fullName || left.email)
      const rightValue = memberSortBy.value === 'role' ? right.projectRole : (right.fullName || right.email)
      result = `${leftValue || ''}`.localeCompare(`${rightValue || ''}`)
    }
    return memberSortDirection.value === 'asc' ? result : -result
  })
})

const isCurrentUser = (userId) => {
  return currentUser && currentUser.id === userId
}

const formatDate = (dateString) => {
  if (!dateString) return '-'
  return new Date(dateString).toLocaleDateString('vi-VN')
}

const inviteMember = async () => {
  if (isInviting.value) return

  if (inviteTab.value === 'system') {
    if (!inviteForm.value.systemUserId) {
      ElMessage.warning('Vui lòng chọn thành viên trong Workspace.')
      return
    }
  }
  if (inviteTab.value === 'email' && !inviteForm.value.email) {
    ElMessage.warning('Vui lòng nhập email.')
    return
  }

  isInviting.value = true
  try {
    if (inviteTab.value === 'system') {
      await axiosClient.post(`/projects/${projectId.value}/members/add-existing`, {
        userId: inviteForm.value.systemUserId,
        role: inviteForm.value.role
      })
      ElMessage.success('Đã thêm thành viên vào dự án.')
    } else {
      await axiosClient.post(`/projects/${projectId.value}/members`, {
        email: inviteForm.value.email,
        role: inviteForm.value.role,
        inviteMessage: inviteForm.value.message
      })
      ElMessage.success('Đã gửi lời mời qua email. Thành viên cần chấp nhận lời mời để tham gia dự án.')
    }
    showAddMemberModal.value = false
    inviteForm.value = { email: '', systemUserId: '', role: 'Developer', message: '' }
    await fetchMembers()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Có lỗi xảy ra khi mời thành viên.')
  } finally {
    isInviting.value = false
  }
}

const updateMemberRole = async (userId, newRole) => {
  try {
    await axiosClient.put(`/projects/${projectId.value}/members/${userId}/role`, {
      role: newRole
    })
    ElMessage.success('Đã cập nhật vai trò thành công.')
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể cập nhật vai trò.')
    await fetchMembers()
  }
}

const removeMember = async (userId) => {
  const removingCurrentUser = isCurrentUser(userId)

  ElMessageBox.confirm(
    'Bạn có chắc chắn muốn xóa thành viên này khỏi dự án? Các công việc của họ sẽ bị bỏ trống.',
    'Xác nhận xóa',
    {
      confirmButtonText: 'Xóa',
      cancelButtonText: 'Hủy',
      type: 'warning'
    }
  ).then(async () => {
    try {
      await axiosClient.delete(`/projects/${projectId.value}/members/${userId}`)
      ElMessage.success('Đã xóa thành viên khỏi dự án.')
      if (removingCurrentUser) {
        projectStore.fetchAllProjects(true).catch(() => {})
        await router.push('/dashboard')
        return
      }
      await fetchMembers()
    } catch (error) {
      ElMessage.error(error.response?.data?.message || 'Không thể xóa thành viên.')
    }
  }).catch(() => {})
}

const handleMemberRealtime = event => {
  if (`${event?.projectId}` !== `${projectId.value}` || event?.entityType !== 'project-member') return
  const userId = event?.data?.userId || event?.entityId
  if (event.action === 'deleted') {
    members.value = members.value.filter(member => `${member.userId || member.id}` !== `${userId}`)
  } else if (event.action === 'role-updated') {
    const member = members.value.find(item => `${item.userId || item.id}` === `${userId}`)
    if (member) member.projectRole = normalizeMemberRole(event.data?.role)
  } else {
    fetchMembers()
  }
}

onMounted(async () => {
  if (projectId.value) {
    signalRService.on('EntityChanged', handleMemberRealtime)
    await signalRService.startConnection(projectId.value)
    fetchMembers()
    fetchLinkedTeams()
  }
  document.addEventListener('click', handleOutsideClick)
})

onUnmounted(() => {
  signalRService.off('EntityChanged', handleMemberRealtime)
  document.removeEventListener('click', handleOutsideClick)
})
</script>

<style scoped>
.space-members-view {
  width: 100%;
  min-width: 0;
  color: var(--color-text-primary);
  font-family: var(--sp-font-ui);
}

.filter-select {
  width: 168px;
}

.member-info {
  display: flex;
  align-items: center;
  gap: 12px;
}

.member-details {
  display: flex;
  flex-direction: column;
}

.member-name {
  font-weight: 750;
  color: var(--color-text-primary, #172b4d);
  font-size: 13px;
  line-height: 1.35;
}

.member-email {
  font-size: 11.5px;
  line-height: 1.4;
  color: var(--color-text-muted, #6b778c);
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 0;
  color: var(--color-text-muted, #6b778c);
}

.empty-icon {
  font-size: 48px;
  margin-bottom: 16px;
  color: var(--color-border, #dfe1e6);
}

.loading-state {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 40px;
  color: var(--color-text-muted, #6b778c);
}

.team-selection-list {
  max-height: 350px;
  overflow-y: auto;
  border: 1px solid var(--color-border, #dfe1e6);
  border-radius: 6px;
  padding: 8px;
}

.team-option {
  display: flex;
  align-items: center;
  padding: 12px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  border: 1px solid transparent;
}

.team-option:hover {
  background-color: var(--color-background-hover, #f4f5f7);
}

.team-option.is-selected {
  background-color: color-mix(in srgb, var(--sp-blue-700) 10%, var(--color-surface));
  border-color: var(--sp-blue-700);
}

/* Tweak Element Plus tabs to match SprintA design */
:deep(.el-tabs__nav-wrap::after) {
  height: 1px;
  background-color: var(--color-border, #dfe1e6);
}
:deep(.el-tabs__item) {
  font-size: 13px;
  font-weight: 750;
  color: var(--color-text-secondary, #42526e);
}
:deep(.el-tabs__item.is-active) {
  color: var(--color-accent, #0c66e4);
}
:deep(.el-tabs__active-bar) {
  background-color: var(--color-accent, #0c66e4);
}
/* Table Container - matches IntakeInbox */
.table-container {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.04);
  margin-top: 14px;
}


:deep(.table-container .nexus-table) {
  border-radius: 12px !important;
}

:deep(.nexus-table .el-table__inner-wrapper) {
  border-radius: 12px !important;
  overflow: hidden !important;
}

:deep(.nexus-table::before),
:deep(.nexus-table .el-table__border-left-patch),
:deep(.nexus-table .el-table__border-bottom-patch) {
  display: none !important;
}

:deep(.nexus-table .el-table__header-wrapper) {
  border-radius: 12px 12px 0 0 !important;
  overflow: hidden !important;
}

:deep(.nexus-table) {
  width: 100% !important;
  border-collapse: collapse !important;
}

:deep(.nexus-table .el-table__inner-wrapper::before) {
  display: none !important;
}

:deep(.nexus-table th.el-table__cell) {
  background: var(--color-surface) !important;
  border-bottom: 2px solid var(--color-border) !important;
  padding: 12px 16px !important;
  font-size: 11px !important;
  letter-spacing: 0.05em !important;
  font-weight: 700 !important;
  text-transform: uppercase !important;
  white-space: nowrap !important;
  color: var(--color-text-secondary) !important;
  font-family: var(--sp-font-ui) !important;
}

:deep(.nexus-table th.el-table__cell i) {
  color: inherit !important;
  margin-right: 6px !important;
  opacity: 0.88 !important;
}

:deep(.nexus-table td.el-table__cell) {
  height: 50px !important;
  max-height: 50px !important;
  padding: 4px 14px !important;
  box-sizing: border-box !important;
  font-size: 13px !important;
  color: var(--color-text-primary) !important;
  border-bottom: 1px solid var(--color-border) !important;
}

:deep(.nexus-table .el-table__body tr) {
  box-shadow: inset 3px 0 0 transparent !important;
  transition: all 0.2s ease !important;
}

:deep(.nexus-table .el-table__body tr:hover > td.el-table__cell) {
  background: color-mix(in srgb, var(--sa-primary, var(--color-accent)) 8%, var(--color-surface)) !important;
}

:deep(.nexus-table .el-table__body tr:hover > td.el-table__cell:first-child) {
  box-shadow: inset 3px 0 0 var(--sa-primary, var(--color-accent)) !important;
}

:deep(.nexus-table .el-select__wrapper) {
  min-height: 30px !important;
  height: 30px !important;
  border-radius: 6px !important;
  background: var(--color-input-bg) !important;
  box-shadow: 0 0 0 1px var(--color-input-border) inset !important;
}

:deep(.nexus-table .el-select__wrapper:hover) {
  box-shadow: 0 0 0 1px var(--color-accent) inset !important;
}
:deep(.el-avatar) { font-family: var(--sp-font-ui); font-weight: 800; }
:deep(.el-tag) { border-radius: 999px; font-family: var(--sp-font-ui); }
.member-email { color: var(--color-text-muted) !important; }
.team-option.is-selected { background: var(--sa-primary-soft); border-color: var(--color-accent); }
:global(.members-role-popper) { background: var(--color-surface) !important; border: 1px solid var(--color-border) !important; box-shadow: var(--sp-shadow-sm) !important; }
:global(.members-role-popper .el-select-dropdown__item) { color: var(--color-text-primary) !important; }
:global(.members-role-popper .el-select-dropdown__item.is-hovering),
:global(.members-role-popper .el-select-dropdown__item:hover) { background: var(--color-surface-hover) !important; color: var(--color-text-primary) !important; }

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
</style>
