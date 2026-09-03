<template>
  <div class="team-list-page">
    <section class="teams-content-panel">
      <div class="section-header">
        <h2>All teams</h2>
      </div>

    <ProjectPageToolbar
      v-model:searchQuery="searchQuery"
      show-search
      search-placeholder="Tìm kiếm các đội ngũ"
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
              :fields="teamFilterFields"
              :operators="teamOperators"
              :custom-value-meta="customTeamValueMeta"
              :active="showFilterDropdown"
            />
          </div>
        </div>
      </template>
      <template #toggles>
        <div class="view-toggles">
          <button class="toggle-btn" :class="{ active: viewMode === 'grid' }" @click="viewMode = 'grid'" title="Chế độ lưới">
            <i class="fa-solid fa-table-cells-large"></i>
          </button>
          <button class="toggle-btn" :class="{ active: viewMode === 'table' }" @click="viewMode = 'table'" title="Chế độ danh sách">
            <i class="fa-solid fa-list"></i>
          </button>
        </div>
      </template>
      <template #sort>
        <ToolbarSortMenu v-model="teamSortBy" v-model:direction="teamSortDirection" label="Sắp xếp đội ngũ" :options="teamSortOptions" />
      </template>
    </ProjectPageToolbar>

    <!-- Grid View -->
    <div v-if="viewMode === 'grid' && filteredTeams.length > 0" class="team-cards-grid">
      <div class="team-card" v-for="team in filteredTeams" :key="team.id" @click="goToTeam(team.id)">
        <div class="team-card-cover"></div>
        <div class="team-card-content">
          <div class="team-avatar">{{ team.avatarText }}</div>
          <h3 class="team-name-card">{{ team.name }}</h3>
          <p class="team-meta">{{ team.memberCount }} thành viên</p>
        </div>
      </div>
    </div>

    <WorkItemsListTable
      v-else-if="viewMode === 'table' && filteredTeams.length > 0"
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
      <template #cell-type="{ row }"><span>{{ row.type }}</span></template>
      <template #cell-manager="{ row }">
        <div v-if="row.managerName !== 'Chưa có'" class="manager-cell">
          <AppUserChip :name="row.managerName" :email="row.managerEmail" compact />
        </div>
        <span v-else class="muted-text">Chưa có</span>
      </template>
      <template #cell-members="{ row }"><span class="count-cell">{{ row.memberCount }}</span></template>
      <template #cell-parent="{ row }"><span class="count-cell">{{ row.parentCount }}</span></template>
      <template #cell-children="{ row }"><span class="count-cell">{{ row.childrenCount }}</span></template>
    </WorkItemsListTable>

    <!-- Empty State -->
    <div v-else-if="filteredTeams.length === 0" class="goals-empty-state">
      <div class="empty-spaces-icon" aria-hidden="true">
        <i class="fa-solid fa-users"></i>
      </div>
      <div class="empty-spaces-copy">
        <h3>Không tìm thấy đội ngũ nào</h3>
        <p>Thử tìm kiếm với tên khác.</p>
      </div>
    </div>
    </section>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useTeamStore } from '@/store/useTeamStore'
import { AppEmptyState, AppUserChip } from '@/components/common/Foundation'
import ProjectPageToolbar from '@/components/common/ProjectPageToolbar.vue'
import WorkItemsListTable from '@/components/common/WorkItemsListTable.vue'
import ToolbarSortMenu from '@/components/common/ToolbarSortMenu.vue'
import FilterBar from '@/components/FilterBar.vue'

const activeFilters = ref([])

const teamFilterFields = computed(() => [
  { key: 'type', label: 'Loại đội ngũ', icon: 'fa-solid fa-layer-group', values: teamTypeOptions.value },
  { key: 'manager', label: 'Người quản lý', icon: 'fa-regular fa-user', values: managerOptions.value }
])

const teamOperators = {
  type: ['is', 'is not'],
  manager: ['is', 'is not']
}

const customTeamValueMeta = (fieldKey, value) => {
  if (fieldKey === 'manager') return { icon: 'fa-regular fa-user', color: '#3b82f6' }
  if (fieldKey === 'type') return { icon: 'fa-solid fa-layer-group', color: '#10b981' }
  return null
}

const allMappedTeams = computed(() => {
  let list = teamStore.allTeams || []
  return list.map(t => ({
    ...t,
    managerName: t.manager?.fullName || t.manager?.name || 'Chưa có',
    type: t.type || 'Đội ngũ chính thức'
  }))
})

const teamTypeOptions = computed(() => Array.from(new Set(allMappedTeams.value.map(team => team.type).filter(Boolean))).sort())
const managerOptions = computed(() => Array.from(new Set(
  allMappedTeams.value
    .map(team => team.managerName)
    .filter(name => name && name !== 'Chưa có')
)).sort())

const router = useRouter()
const route = useRoute()
const teamsBasePath = computed(() => route.path.startsWith('/teams') ? '/teams' : '/home/teams')
const teamStore = useTeamStore()

const searchQuery = ref('')
const viewMode = ref('table')
const teamSortDirection = ref('asc')
const teamSortBy = ref('name')
const teamSortOptions = [
  { value: 'name', label: 'Tên đội ngũ', icon: 'fa-solid fa-font' },
  { value: 'members', label: 'Số thành viên', icon: 'fa-solid fa-users' },
  { value: 'children', label: 'Đội ngũ con', icon: 'fa-solid fa-sitemap' }
]

const teamTableColumns = [
  { key: 'team', label: 'Đội ngũ', icon: 'fa-solid fa-people-group', width: '26%', minWidth: '280px', sticky: true },
  { key: 'type', label: 'Loại đội ngũ', icon: 'fa-solid fa-shapes', width: '20%', minWidth: '190px' },
  { key: 'manager', label: 'Người quản lý', icon: 'fa-solid fa-user-tie', width: '22%', minWidth: '220px' },
  { key: 'members', label: 'Thành viên', icon: 'fa-solid fa-user-group', width: '12%', minWidth: '120px' },
  { key: 'parent', label: 'Đội ngũ gốc', icon: 'fa-solid fa-sitemap', width: '10%', minWidth: '120px' },
  { key: 'children', label: 'Đội ngũ con', icon: 'fa-solid fa-network-wired', width: '10%', minWidth: '120px' }
]

const showFilterDropdown = ref(false)

const toggleFilterDropdown = (e) => {
  e.stopPropagation()
  showFilterDropdown.value = !showFilterDropdown.value
}

const handleOutsideClick = (e) => {
  if (showFilterDropdown.value && !e.target.closest('.js-toolbar-popup-scope')) {
    showFilterDropdown.value = false
  }
}

onMounted(() => {
  teamStore.initializeRealtime()
  teamStore.fetchAllTeams()
  document.addEventListener('click', handleOutsideClick)
})

onUnmounted(() => {
  document.removeEventListener('click', handleOutsideClick)
})

const filteredTeams = computed(() => {
  let list = teamStore.allTeams || []

  let mapped = list.map(t => ({
    ...t,
    avatarText: t.name ? t.name.substring(0, 2).toUpperCase() : "T",
    memberCount: t.memberCount ?? t.members?.length ?? t.users?.length ?? 0,
    childrenCount: t.children?.length || t.subDepartments?.length || 0,
    manager: t.manager || t.managerId,
    managerName: t.manager?.fullName || t.manager?.name || "Chưa có",
    managerEmail: t.manager?.email || "",
    parentTeamName: t.parentDepartment?.name || t.parent?.name || "Không có đội ngũ gốc",
    parentCount: (t.parentDepartment || t.parent || t.parentId) ? 1 : 0,
    type: t.type || "Đội ngũ chính thức"
  }))

  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    mapped = mapped.filter(t => t.name.toLowerCase().includes(q))
  }

  if (activeFilters.value.length > 0) {
    mapped = mapped.filter(t => {
      return activeFilters.value.every(f => {
        let val = ''
        if (f.field === 'type') val = t.type
        else if (f.field === 'manager') val = t.managerName

        const isMatch = val === f.value
        return f.operator === 'is' ? isMatch : !isMatch
      })
    })
  }

  return mapped.sort((left, right) => {
    const result = teamSortBy.value === 'name'
      ? `${left.name || ''}`.localeCompare(`${right.name || ''}`)
      : (Number(left[teamSortBy.value === 'members' ? 'memberCount' : 'childrenCount']) - Number(right[teamSortBy.value === 'members' ? 'memberCount' : 'childrenCount']))
    return teamSortDirection.value === 'asc' ? result : -result
  })
})

const goToTeam = (id) => {
  router.push(`${teamsBasePath.value}/${id}`)
}
</script>

<style scoped>
.team-list-container {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
}

.team-list-page {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
}

.teams-content-panel {
  background: transparent;
  border: 0;
  border-radius: 0;
  padding: 0;
  box-shadow: none;
}

.section-header h2 {
  color: #172B4D;
  font-size: 18px;
  font-weight: 750;
  line-height: 1.25;
  margin: 0;
}

.section-header {
  margin: 0 0 16px;
}

.list-controls {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
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

/* View Toggle */
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
  background-color: #EBECF0;
}

.toggle-btn.active {
  background-color: #DEEBFF;
  color: #0052CC;
}

/* Grid View Styles */
.team-cards-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 24px;
  margin-top: 14px;
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

.team-name-card {
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

.empty-state-grid {
  grid-column: 1 / -1;
  text-align: center;
  padding: 40px;
  color: #5E6C84;
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

/* Jira Table Styles */
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
  color: var(--color-text-secondary);
}

.jira-table th i {
  color: inherit;
  margin-right: 6px;
  opacity: 0.88;
}

.jira-table th {
  cursor: pointer;
}

.sort-icon {
  margin-left: 4px;
  font-size: 12px;
  color: var(--color-text-muted);
}

.col-team {
  width: 50%;
}

.col-members {
  width: 25%;
}

.col-children {
  width: 25%;
}

.jira-table td {
  height: 50px;
  padding: 10px 14px !important;
  font-size: 13px;
  color: var(--color-text-primary);
  border-bottom: 1px solid var(--color-border) !important;
  cursor: pointer;
  white-space: nowrap;
  vertical-align: middle;
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

.team-cell {
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

.team-name {
  font-weight: 700;
  color: var(--color-text-primary);
  font-size: 13px;
}

.team-name-text {
  font-weight: 700;
  color: var(--color-text-primary);
  font-size: 13px;
}

.team-name:hover,
.team-name-text:hover {
  color: var(--color-accent);
}

.empty-table-state {
  text-align: center;
  padding: 40px !important;
  color: #5E6C84 !important;
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
</style>
