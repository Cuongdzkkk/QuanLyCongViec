<template>
  <div class="teams-dashboard">
    <div class="dashboard-content">
      <section class="dashboard-section teams-content-panel">
        <div class="section-header">
          <h2>
            {{ t('homeSite.teams.yourTeams') }}
          </h2>
        </div>

        <ProjectPageToolbar
          v-model:searchQuery="teamSearch"
          show-search
          :search-placeholder="t('homeSite.teams.searchTeams')"
        >
          <template #filters>
            <ToolbarFilterMenu
              label="Filters"
              :clear-label="t('common.clear') || 'Clear'"
              :clear-all-label="t('common.clear') || 'Clear all'"
              empty-label="No filters applied"
              :count="activeFilterCount"
              :active-items="activeFilterItems"
              @clear="clearFilters"
              @remove="removeFilter"
            >
              <template #default="{ search }">
                <DropdownFilter v-if="matchesFilterSearch(t('homeSite.teams.teamType'), search)" :label="t('homeSite.teams.teamType')" :options="teamTypeOptions" v-model="filters.type" :searchable="false" />
                <DropdownFilter v-if="matchesFilterSearch(t('homeSite.teams.manager'), search)" :label="t('homeSite.teams.manager')" :options="managerOptions" v-model="filters.manager" :searchable="false" />
              </template>
            </ToolbarFilterMenu>
          </template>
          <template #sort>
            <ToolbarSortMenu v-model="teamSortBy" v-model:direction="teamSortDirection" :label="t('homeSite.teams.sortTeams') || 'Sắp xếp đội ngũ'" :options="teamSortOptions" />
          </template>
          <template #toggles>
            <div class="view-toggles">
              <button class="toggle-btn" :class="{ active: viewMode === 'grid' }" @click="viewMode = 'grid'" :title="t('homeSite.teams.gridView')">
                <i class="fa-solid fa-table-cells-large"></i>
              </button>
              <button class="toggle-btn" :class="{ active: viewMode === 'table' }" @click="viewMode = 'table'" :title="t('homeSite.teams.tableView')">
                <i class="fa-solid fa-list"></i>
              </button>
            </div>
          </template>
        </ProjectPageToolbar>

        <!-- Empty State (No teams at all) -->
        <div v-if="teams.length === 0" class="goals-empty-state">
          <div class="empty-spaces-icon" aria-hidden="true">
            <i class="fa-solid fa-users"></i>
          </div>
          <div class="empty-spaces-copy">
            <h3>{{ t('homeSite.teams.yourTeams') }}</h3>
            <p>{{ t('homeSite.teams.emptyDescription') }}</p>
            <div class="empty-banner-actions" style="margin-top: 16px; display: flex; gap: 8px; justify-content: center;">
              <button class="primary-btn" @click="openCreateTeamModal">
                {{ t('homeSite.teams.startTeam') }}
              </button>
              <router-link :to="`${teamsBasePath}/list`" class="secondary-btn">
                {{ t('homeSite.teams.browseTeams') }}
              </router-link>
            </div>
          </div>
        </div>

        <!-- Empty State (No search results) -->
        <div v-else-if="filteredTeams.length === 0" class="goals-empty-state">
          <div class="empty-spaces-icon" aria-hidden="true">
            <i class="fa-solid fa-users"></i>
          </div>
          <div class="empty-spaces-copy">
            <h3>{{ t('homeSite.teams.noTeamsFound') || 'Không tìm thấy đội ngũ nào' }}</h3>
            <p>Thử tìm kiếm với tên khác.</p>
          </div>
        </div>

        <!-- Grid View -->
        <div class="team-cards-grid" v-else-if="viewMode === 'grid'">
          <div class="team-card" v-for="team in filteredTeams" :key="team.id" @click="goToTeam(team.id)">
            <div class="team-card-cover"></div>
            <div class="team-card-content">
              <div class="team-avatar">{{ team.avatarText }}</div>
              <h3 class="team-name">{{ team.name }}</h3>
              <p class="team-meta">{{ t('homeSite.teams.membersCount', { count: team.memberCount }) }}</p>
            </div>
          </div>
        </div>

        <!-- Table View -->
        <WorkItemsListTable
          v-if="viewMode === 'table' && filteredTeams.length > 0"
          :columns="teamTableColumns"
          :rows="filteredTeams"
          min-width="1080"
          @row-click="team => goToTeam(team.id)"
        >
          <template #cell-team="{ row }">
            <div class="team-name-cell">
              <div class="team-avatar-small" :style="{ backgroundColor: '#0052cc' }">{{ row.avatarText }}</div>
              <span class="team-name-text">{{ row.name }}</span>
            </div>
          </template>
          <template #cell-type="{ row }"><span>{{ row.typeLabel }}</span></template>
          <template #cell-manager="{ row }">
            <div v-if="row.managerName !== noManagerLabel" class="manager-cell">
              <UserAvatar :user="{ fullName: row.managerName, email: row.managerEmail }" :size="24" :fontSize="10" />
              <span class="manager-name">{{ row.managerName }}</span>
            </div>
            <span v-else class="muted-text">{{ noManagerLabel }}</span>
          </template>
          <template #cell-members="{ row }"><span class="count-cell">{{ row.memberCount }}</span></template>
          <template #cell-parent="{ row }"><span class="count-cell">{{ row.parentCount }}</span></template>
          <template #cell-children="{ row }"><span class="count-cell">{{ row.childrenCount }}</span></template>
        </WorkItemsListTable>
        <div v-else-if="viewMode === 'table'" class="table-container work-items-table-shell">
        <table v-resizable class="jira-table work-items-style-table">
          <thead>
            <tr>
              <th style="width: 25%">{{ t('homeSite.teams.team') }}</th>
              <th style="width: 20%">{{ t('homeSite.teams.teamType') }}</th>
              <th style="width: 20%">{{ t('homeSite.teams.manager') }}</th>
              <th style="width: 10%">{{ t('homeSite.teams.members') }}</th>
              <th style="width: 10%">{{ t('homeSite.teams.parentTeam') }}</th>
              <th style="width: 15%">{{ t('homeSite.teams.childTeams') }} <i class="fa-solid fa-arrow-down" style="font-size: 10px; margin-left: 4px;"></i></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="team in filteredTeams" :key="team.id" @click="goToTeam(team.id)">
              <td>
                <div class="team-name-cell">
                  <div class="team-avatar-small" :style="{ backgroundColor: '#0052cc' }">{{ team.avatarText }}</div>
                  <span class="team-name-text">{{ team.name }}</span>
                </div>
              </td>
              <td style="white-space: nowrap;">{{ team.typeLabel }}</td>
              <td>
                <div v-if="team.managerName !== noManagerLabel" class="manager-cell" style="display: flex; align-items: center; gap: 8px;">
                  <UserAvatar :user="{ fullName: team.managerName, email: team.managerEmail }" :size="24" :fontSize="10" />
                  <span style="white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 120px;">{{ team.managerName }}</span>
                </div>
                <div v-else style="color: #5E6C84; display: flex; align-items: center; gap: 6px;">
                  <div style="width: 24px; height: 24px; border-radius: 50%; background: #DFE1E6; display: flex; align-items: center; justify-content: center; color: #172B4D; font-size: 10px; font-weight: bold;">?</div>
                  <span>{{ noManagerLabel }}</span>
                </div>
              </td>
              <td>{{ team.memberCount }}</td>
              <td>{{ team.parentCount }}</td>
              <td>{{ team.childrenCount }}</td>
            </tr>
          </tbody>
        </table>
        </div>
      </section>
    </div>

    <AppModal
      v-model="isCreateModalOpen"
      :title="t('homeSite.teams.title')"
      width="400px"
      :confirmText="isCreating ? t('common.creating') : t('common.create')"
      :cancelText="t('common.cancel')"
      :loading="isCreating"
      @confirm="submitCreateTeam"
      @cancel="isCreateModalOpen = false; isMemberDropdownOpen = false"
    >
      <template #header>
        <div style="display: flex; align-items: center; gap: 8px;">
          <i class="fa-solid fa-user-group" style="color: #6B778C;"></i> {{ t('homeSite.teams.title') }}
        </div>
      </template>

      <p class="required-subtitle" style="font-size: 11px; color: #6B778C; margin: 0 0 16px 0;">
        {{ t('homeSite.teams.requiredFields') }} <span class="required" style="color: #DE350B;">*</span>
      </p>

      <AppFormField :label="t('homeSite.teams.name')" required>
        <input type="text" v-model="newTeamData.name" style="width: 100%; padding: 8px 12px; border: 2px solid #DFE1E6; border-radius: 3px; font-size: 14px; box-sizing: border-box;" />
      </AppFormField>

      <AppFormField :label="t('homeSite.teams.addTeamMembers')" required>
        <div class="tags-input-container" @click="focusMemberInput" style="display: flex; flex-wrap: wrap; align-items: center; gap: 4px; padding: 4px; border: 2px solid #DFE1E6; border-radius: 3px; min-height: 40px; box-sizing: border-box; cursor: text; position: relative;">
          <div class="tag-chip" v-for="member in newTeamData.members" :key="member.id" style="display: flex; align-items: center; gap: 6px; background-color: #FFFFFF; border: 1px solid #DFE1E6; border-radius: 24px; padding: 2px 8px 2px 2px; font-size: 12px; color: #172B4D; font-weight: 500;">
            <UserAvatar :user="{ ...member, fullName: member.name, avatarColor: member.color }" :size="20" :fontSize="10" />
            {{ member.name }}
            <i class="fa-solid fa-xmark remove-tag" @click.stop="removeMember(member.id)" style="color: #5E6C84; cursor: pointer; font-size: 10px; margin-left: 4px;"></i>
          </div>
          <input type="text" ref="memberInputRef" :placeholder="t('homeSite.teams.enterName')" v-model="memberSearchQuery" @focus="isMemberDropdownOpen = true" style="border: none; outline: none; flex: 1; min-width: 80px; padding: 4px 8px; font-size: 14px;" />
        </div>
        <div class="member-dropdown" v-if="isMemberDropdownOpen" style="position: absolute; background: #FFFFFF; border-radius: 3px; box-shadow: 0 4px 8px -2px rgba(9, 30, 66, 0.25), 0 0 1px rgba(9, 30, 66, 0.31); margin-top: 4px; z-index: var(--sp-z-dropdown, 1000); max-height: 200px; overflow-y: auto; width: 100%;">
          <div class="member-dropdown-item" v-for="user in filteredUsers" :key="user.id" @click="addMember(user)" style="display: flex; align-items: center; gap: 12px; padding: 8px 12px; cursor: pointer;">
            <UserAvatar :user="{ ...user, fullName: user.name, avatarColor: user.color }" :size="20" :fontSize="10" />
            <span style="font-size: 14px; color: #172B4D;">{{ user.name }}</span>
          </div>
          <div class="member-dropdown-empty" v-if="filteredUsers.length === 0" style="padding: 12px; text-align: center; font-size: 13px; color: #5E6C84;">
            {{ t('homeSite.teams.noMembersFound') }}
          </div>
        </div>
      </AppFormField>

      <AppFormField :label="t('homeSite.teams.type')" required>
        <div class="select-wrapper" style="position: relative;">
          <select v-model="newTeamData.type" class="jira-select" style="width: 100%; padding: 8px 12px; border: 2px solid #DFE1E6; border-radius: 3px; font-size: 14px; color: #172B4D; appearance: none; outline: none; cursor: pointer; box-sizing: border-box;">
            <option :value="TEAM_TYPE_OFFICIAL">{{ t('homeSite.teams.officialTeam') }}</option>
            <option :value="TEAM_TYPE_GROUP">{{ t('homeSite.teams.group') }}</option>
          </select>
        </div>
      </AppFormField>

      <div class="recaptcha-text" style="font-size: 11px; color: #5E6C84; line-height: 1.5; margin-top: 8px;">
        {{ t('homeSite.teams.recaptchaPrefix') }}
        <a href="#" style="color: #0052CC; text-decoration: none;">{{ t('homeSite.teams.privacyPolicy') }} <i class="fa-solid fa-arrow-up-right-from-square" style="font-size: 10px;"></i></a>
        {{ t('homeSite.teams.and') }}
        <a href="#" style="color: #0052CC; text-decoration: none;">{{ t('homeSite.teams.termsOfService') }} <i class="fa-solid fa-arrow-up-right-from-square" style="font-size: 10px;"></i></a>
        {{ t('homeSite.teams.googleSuffix') }}
      </div>
    </AppModal>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, computed, reactive } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useTeamStore } from '@/store/useTeamStore'
import { usePeopleStore } from '@/store/usePeopleStore'
import { getStoredUser } from '@/utils/permissions'
import { getAvatarColor, getInitials } from '@/utils/avatarHelper'
import UserAvatar from '@/components/common/UserAvatar.vue'
import { useI18nStore } from '@/store/useI18nStore'
import { AppModal, AppFormField } from '@/components/common/Foundation'
import ProjectPageToolbar from '@/components/common/ProjectPageToolbar.vue'
import WorkItemsListTable from '@/components/common/WorkItemsListTable.vue'
import ToolbarFilterMenu from '@/components/common/ToolbarFilterMenu.vue'
import DropdownFilter from '@/components/common/DropdownFilter.vue'
import ToolbarSortMenu from '@/components/common/ToolbarSortMenu.vue'

const TEAM_TYPE_OFFICIAL = 'Đội ngũ chính thức'
const TEAM_TYPE_GROUP = 'Nhóm'

const router = useRouter()
const route = useRoute()
const teamsBasePath = computed(() => route.path.startsWith('/teams') ? '/teams' : '/home/teams')
const teamStore = useTeamStore()
const peopleStore = usePeopleStore()
const i18nStore = useI18nStore()
const t = i18nStore.t

const isCreateModalOpen = ref(false)
const isCreating = ref(false)
const teamSearch = ref('')
const memberSearchQuery = ref('')
const isMemberDropdownOpen = ref(false)
const memberInputRef = ref(null)
const viewMode = ref('table')
const teamSortDirection = ref('asc')
const teamSortBy = ref('name')
const teamSortOptions = computed(() => {
  const isVi = i18nStore.locale === 'vi'
  return [
    { value: 'name', label: isVi ? 'Tên đội ngũ' : 'Team name', icon: 'fa-solid fa-font' },
    { value: 'members', label: isVi ? 'Số thành viên' : 'Members count', icon: 'fa-solid fa-users' },
    { value: 'children', label: isVi ? 'Đội ngũ con' : 'Child teams', icon: 'fa-solid fa-sitemap' }
  ]
})
const teamTableColumns = computed(() => [
  { key: 'team', label: t('homeSite.teams.team'), icon: 'fa-solid fa-people-group', width: '26%', minWidth: '280px', sticky: true },
  { key: 'type', label: t('homeSite.teams.teamType'), icon: 'fa-solid fa-shapes', width: '20%', minWidth: '190px' },
  { key: 'manager', label: t('homeSite.teams.manager'), icon: 'fa-solid fa-user-tie', width: '22%', minWidth: '220px' },
  { key: 'members', label: t('homeSite.teams.members'), icon: 'fa-solid fa-user-group', width: '12%', minWidth: '120px' },
  { key: 'parent', label: t('homeSite.teams.parentTeam'), icon: 'fa-solid fa-sitemap', width: '10%', minWidth: '120px' },
  { key: 'children', label: t('homeSite.teams.childTeams'), icon: 'fa-solid fa-network-wired', width: '10%', minWidth: '120px' }
])
const filters = ref({
  type: '',
  manager: ''
})

const noManagerLabel = computed(() => t('homeSite.teams.noManager'))

const newTeamData = reactive({
  name: '',
  description: '',
  type: TEAM_TYPE_OFFICIAL,
  members: []
})

const translateTeamType = (type) => {
  if (type === TEAM_TYPE_GROUP || String(type).toLowerCase() === 'group') return t('homeSite.teams.group')
  return t('homeSite.teams.officialTeam')
}

const filteredUsers = computed(() => {
  const allUsers = peopleStore.users.map(u => ({
    id: u.id,
    name: u.fullName || u.email,
    email: u.email,
    initials: getInitials(u.fullName, u.email),
    color: getAvatarColor(u.email || u.id),
    avatarUrl: u.avatarUrl
  }))
  if (!memberSearchQuery.value) return allUsers.filter(u => !newTeamData.members.find(m => m.id === u.id))
  const q = memberSearchQuery.value.toLowerCase()
  return allUsers.filter(u => (u.name || '').toLowerCase().includes(q) && !newTeamData.members.find(m => m.id === u.id))
})

const focusMemberInput = () => {
  memberInputRef.value?.focus()
  isMemberDropdownOpen.value = true
}

const addMember = (user) => {
  newTeamData.members.push(user)
  memberSearchQuery.value = ''
  isMemberDropdownOpen.value = false
}

const removeMember = (id) => {
  newTeamData.members = newTeamData.members.filter(m => m.id !== id)
}

const teams = computed(() => teamStore.allTeams.map(team => {
  const type = team.type || TEAM_TYPE_OFFICIAL
  return {
    id: team.id,
    name: team.name,
    avatarText: team.name ? team.name.substring(0, 2).toUpperCase() : 'T',
    memberCount: team.memberCount ?? team.members?.length ?? team.users?.length ?? 0,
    childrenCount: team.children?.length || team.subDepartments?.length || 0,
    manager: team.manager || team.managerId,
    managerName: team.manager?.fullName || team.manager?.name || noManagerLabel.value,
    managerEmail: team.manager?.email || '',
    parentTeamName: team.parentDepartment?.name || team.parent?.name || t('homeSite.teams.noParentTeam'),
    parentCount: (team.parentDepartment || team.parent || team.parentId) ? 1 : 0,
    type,
    typeLabel: translateTeamType(type),
    coverImage: team.coverImage || ''
  }
}))

const teamTypeOptions = computed(() => Array.from(new Set(teams.value.map(team => team.typeLabel).filter(Boolean))).sort())
const managerOptions = computed(() => Array.from(new Set(
  teams.value
    .map(team => team.managerName)
    .filter(name => name && name !== noManagerLabel.value)
)).sort())
const activeFilterCount = computed(() => Object.values(filters.value).filter(Boolean).length)
const activeFilterItems = computed(() => [
  filters.value.type ? { key: 'type', label: t('homeSite.teams.teamType'), icon: 'fa-solid fa-layer-group', value: filters.value.type } : null,
  filters.value.manager ? { key: 'manager', label: t('homeSite.teams.manager'), icon: 'fa-regular fa-user', value: filters.value.manager } : null
].filter(Boolean))
const matchesFilterSearch = (label, search) => !search || String(label || '').toLowerCase().includes(search)

const clearFilters = () => {
  filters.value = {
    type: '',
    manager: ''
  }
}

const removeFilter = (key) => {
  if (Object.prototype.hasOwnProperty.call(filters.value, key)) {
    filters.value[key] = ''
  }
}

const filteredTeams = computed(() => {
  const query = teamSearch.value.trim().toLowerCase()
  let list = teams.value
  if (query) {
    list = list.filter(team => `${team.name} ${team.typeLabel} ${team.managerName}`.toLowerCase().includes(query))
  }
  if (filters.value.type) {
    list = list.filter(team => team.typeLabel === filters.value.type)
  }
  if (filters.value.manager) {
    list = list.filter(team => team.managerName === filters.value.manager)
  }
  return list.sort((left, right) => {
    const result = teamSortBy.value === 'name'
      ? `${left.name || ''}`.localeCompare(`${right.name || ''}`)
      : (Number(left[teamSortBy.value === 'members' ? 'memberCount' : 'childrenCount']) - Number(right[teamSortBy.value === 'members' ? 'memberCount' : 'childrenCount']))
    return teamSortDirection.value === 'asc' ? result : -result
  })
})

onMounted(() => {
  teamStore.initializeRealtime()
  teamStore.fetchAllTeams()
  peopleStore.fetchPeople()
  window.addEventListener('global-create-click', openCreateTeamModal)
})

onUnmounted(() => {
  window.removeEventListener('global-create-click', openCreateTeamModal)
})

const goToTeam = (id) => {
  router.push(`${teamsBasePath.value}/${id}`)
}

const openCreateTeamModal = () => {
  const sessionUser = getStoredUser()
  const storeUser = sessionUser ? (peopleStore.users.find(u => u.id === sessionUser.id) || sessionUser) : null
  const currentMemberObj = storeUser ? {
    id: storeUser.id,
    name: storeUser.fullName || storeUser.name || sessionUser.fullName || sessionUser.email,
    email: storeUser.email || sessionUser.email,
    initials: getInitials(storeUser.fullName || sessionUser.fullName, storeUser.email || sessionUser.email),
    color: getAvatarColor(storeUser.email || sessionUser.email || storeUser.id || sessionUser.id),
    avatarUrl: storeUser.avatarUrl || sessionUser.avatarUrl
  } : null

  newTeamData.name = ''
  newTeamData.description = ''
  newTeamData.type = TEAM_TYPE_OFFICIAL
  newTeamData.members = currentMemberObj ? [currentMemberObj] : []
  isCreateModalOpen.value = true
  isMemberDropdownOpen.value = false
  memberSearchQuery.value = ''
}

const submitCreateTeam = async () => {
  if (!newTeamData.name) return
  isCreating.value = true
  try {
    const createdTeam = await teamStore.createTeam({
      name: newTeamData.name,
      description: newTeamData.description,
      type: newTeamData.type,
      members: newTeamData.members
    })
    isCreateModalOpen.value = false
    router.push(`${teamsBasePath.value}/${createdTeam.id}`)
  } catch (err) {
    console.error(err)
  } finally {
    isCreating.value = false
  }
}
</script>

<style scoped>
.teams-dashboard {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
}

.dashboard-content {
  display: flex;
  flex-direction: column;
  background: transparent !important;
}

.teams-sections,
.dashboard-section,
.teams-content-panel {
  background: transparent !important;
  border: 0 !important;
  box-shadow: none !important;
  border-radius: 0 !important;
  padding: 0 !important;
}

.empty-state-banner {
  background-color: #FAFBFC;
  border-radius: 8px;
  overflow: hidden;
  margin-top: 24px;
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

.primary-btn {
  background-color: #0052CC;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 3px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background-color 0.2s;
}

.primary-btn:hover:not(:disabled) {
  background-color: #0047B3;
}

.primary-btn:disabled {
  cursor: not-allowed;
  opacity: 0.65;
}

.cancel-btn,
.secondary-btn {
  background-color: rgba(9, 30, 66, 0.04);
  color: #42526E;
  border: none;
  padding: 8px 16px;
  border-radius: 3px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  text-decoration: none;
  transition: background-color 0.2s;
}

.cancel-btn:hover,
.secondary-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.empty-banner-illustration {
  flex: 1;
  display: flex;
  justify-content: flex-end;
}

.mock-illustration {
  width: 280px;
  height: 200px;
  background-color: #E6FCFF;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.mock-illustration i {
  font-size: 64px;
  color: #0052CC;
}

.dashboard-section {
  display: flex;
  flex-direction: column;
}

.section-header {
  margin: 0 0 16px;
}

.section-header h2 {
  font-size: 18px;
  font-weight: 750;
  line-height: 1.25;
  color: #172B4D;
  margin: 0;
}

.team-cards-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 24px;
  margin-top: 30px;
}

.team-card {
  background-color: #FFFFFF;
  border: 1px solid #DFE1E6;
  border-radius: 8px;
  overflow: hidden;
  cursor: pointer;
  transition: box-shadow 0.2s, transform 0.2s;
  display: flex;
  flex-direction: column;
}

.team-card:hover {
  box-shadow: 0 4px 8px -2px rgba(9, 30, 66, 0.25), 0 0 1px rgba(9, 30, 66, 0.31);
  transform: translateY(-2px);
}

.team-card-cover {
  height: 64px;
  background: #ffffff !important;
  border-bottom: 1px solid #EEF2F6;
}

@media (max-width: 1280px) {
  .team-cards-grid {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}

@media (max-width: 900px) {
  .team-cards-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 560px) {
  .team-cards-grid {
    grid-template-columns: 1fr;
  }
}

.team-card-content {
  padding: 0 16px 16px;
  display: flex;
  flex-direction: column;
  align-items: center;
  margin-top: -24px;
}

.team-avatar {
  width: 48px;
  height: 48px;
  background-color: #00875A;
  color: white;
  border-radius: 3px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  font-weight: 600;
  border: 2px solid #FFFFFF;
  margin-bottom: 12px;
}

.team-name {
  margin: 0 0 4px 0;
  font-size: 14px;
  font-weight: 600;
  color: #172B4D;
  text-align: center;
}

.team-meta {
  margin: 0;
  font-size: 12px;
  color: #5E6C84;
}

.modal-overlay.invisible-backdrop {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background-color: transparent;
  z-index: 1000;
}

.side-popover {
  position: absolute;
  top: 60px;
  right: 16px;
  width: 380px;
  background-color: #FFFFFF;
  border-radius: 3px;
  box-shadow: 0 8px 16px -4px rgba(9, 30, 66, 0.25), 0 0 1px rgba(9, 30, 66, 0.31);
  display: flex;
  flex-direction: column;
}

.popover-header {
  padding: 16px 20px 12px;
  display: flex;
  align-items: center;
  gap: 12px;
}

.icon-btn-small {
  background: none;
  border: none;
  color: #42526E;
  cursor: pointer;
  padding: 4px 6px;
  border-radius: 3px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.icon-btn-small:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.popover-title {
  font-size: 16px;
  font-weight: 600;
  color: #172B4D;
  display: flex;
  align-items: center;
  gap: 8px;
}

.popover-body {
  padding: 0 20px 20px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.required-subtitle {
  margin: 0;
  font-size: 12px;
  color: #5E6C84;
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

.required {
  color: #DE350B;
}

.form-group input {
  padding: 8px 12px;
  border: 2px solid #DFE1E6;
  border-radius: 3px;
  font-size: 14px;
  color: #172B4D;
  outline: none;
  transition: border-color 0.2s;
  width: 100%;
  box-sizing: border-box;
}

.form-group input:focus {
  border-color: #4C9AFF;
}

.tags-input-container {
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
  position: relative;
  transition: border-color 0.2s;
}

.tags-input-container:focus-within {
  border-color: #4C9AFF;
}

.tags-input-container input {
  border: none !important;
  outline: none !important;
  flex: 1;
  min-width: 80px;
  padding: 4px 8px !important;
}

.tag-chip {
  display: flex;
  align-items: center;
  gap: 6px;
  background-color: #FFFFFF;
  border: 1px solid #DFE1E6;
  border-radius: 24px;
  padding: 2px 8px 2px 2px;
  font-size: 12px;
  color: #172B4D;
  font-weight: 500;
}

.remove-tag {
  color: #5E6C84;
  cursor: pointer;
  font-size: 10px;
  margin-left: 4px;
}

.remove-tag:hover {
  color: #DE350B;
}

.member-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  background: #FFFFFF;
  border-radius: 3px;
  box-shadow: 0 4px 8px -2px rgba(9, 30, 66, 0.25), 0 0 1px rgba(9, 30, 66, 0.31);
  margin-top: 4px;
  z-index: 10;
  max-height: 200px;
  overflow-y: auto;
}

.member-dropdown-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 12px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.member-dropdown-item:hover {
  background-color: #FAFBFC;
}

.member-dropdown-item span {
  font-size: 14px;
  color: #172B4D;
}

.member-dropdown-empty {
  padding: 12px;
  text-align: center;
  font-size: 13px;
  color: #5E6C84;
}

.select-wrapper {
  position: relative;
}

.jira-select {
  width: 100%;
  padding: 8px 12px;
  border: 2px solid #DFE1E6;
  border-radius: 3px;
  font-size: 14px;
  color: #172B4D;
  appearance: none;
  outline: none;
  cursor: pointer;
  transition: border-color 0.2s;
}

.jira-select:focus {
  border-color: #4C9AFF;
}

.recaptcha-text {
  font-size: 11px;
  color: #5E6C84;
  line-height: 1.5;
  margin-top: 8px;
}

.recaptcha-text a {
  color: #0052CC;
  text-decoration: none;
}

.recaptcha-text a:hover {
  text-decoration: underline;
}

.popover-footer {
  padding: 16px 20px;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.view-toggle {
  display: flex;
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  overflow: hidden;
}

.toggle-btn {
  background: #FAFBFC;
  border: none;
  padding: 8px 12px;
  color: #5E6C84;
  cursor: pointer;
  font-size: 14px;
  transition: background-color 0.2s, color 0.2s;
}

.toggle-btn:hover {
  background: #EBECF0;
}

.toggle-btn.active {
  background-color: #DEEBFF;
  color: #0052CC;
}

.jira-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  margin-top: 30px;
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

.team-name-cell {
  display: flex;
  align-items: center;
  gap: 12px;
}

.team-avatar-small {
  width: 24px;
  height: 24px;
  background-color: #00875A;
  color: white;
  border-radius: 3px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: bold;
}

.team-name-text {
  font-weight: 500;
  color: #0052CC;
}

.team-name-text:hover {
  text-decoration: underline;
}

.empty-table-state {
  text-align: center;
  padding: 40px !important;
  color: #5E6C84 !important;
}
</style>
