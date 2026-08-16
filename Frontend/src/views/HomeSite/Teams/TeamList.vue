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
      <template #search>
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
    </ProjectPageToolbar>

    <!-- Grid View -->
    <div v-if="viewMode === 'grid'" class="team-cards-grid">
      <div class="team-card" v-for="team in filteredTeams" :key="team.id" @click="goToTeam(team.id)">
        <div class="team-card-cover"></div>
        <div class="team-card-content">
          <div class="team-avatar">{{ team.avatarText }}</div>
          <h3 class="team-name-card">{{ team.name }}</h3>
          <p class="team-meta">{{ team.memberCount }} thành viên</p>
        </div>
      </div>
      <AppEmptyState 
        v-if="filteredTeams.length === 0" 
        icon="fa-solid fa-users"
        title="Không tìm thấy đội ngũ nào" 
        description="Thử tìm kiếm với tên khác."
      />
    </div>

    <!-- Table View -->
    <table v-if="viewMode === 'table'" class="jira-table">
                  <thead>
              <tr>
                <th style="width: 25%">Đội ngũ</th>
                <th style="width: 20%">Loại đội ngũ</th>
                <th style="width: 20%">Người quản lý</th>
                <th style="width: 10%">Thành viên</th>
                <th style="width: 10%">Đội ngũ gốc</th>
                <th style="width: 15%">Đội ngũ con <i class="fa-solid fa-arrow-down" style="font-size: 10px; margin-left: 4px;"></i></th>
              </tr>
            </thead>
                  <tbody>
              <tr v-for="team in (viewMode === 'table' && filteredTeams ? filteredTeams : teams)" :key="team.id" @click="goToTeam(team.id)">
                <td>
                  <div class="team-name-cell">
                    <div class="team-avatar-small" :style="{ backgroundColor: '#0052cc' }">{{ team.avatarText }}</div>
                    <span class="team-name-text">{{ team.name }}</span>
                  </div>
                </td>
                <td style="white-space: nowrap;">{{ team.type }}</td>
                <td>
                  <div v-if="team.managerName !== 'Chưa có'" class="manager-cell" style="display: flex; align-items: center; gap: 8px;">
                    <AppUserChip :name="team.managerName" :email="team.managerEmail" compact />
                  </div>
                  <div v-else style="color: #5E6C84; display: flex; align-items: center; gap: 6px;">
                    <div style="width: 24px; height: 24px; border-radius: 50%; background: #DFE1E6; display: flex; align-items: center; justify-content: center; color: #172B4D; font-size: 10px; font-weight: bold;">?</div>
                    <span>Chưa có</span>
                  </div>
                </td>
                <td>{{ team.memberCount }}</td>
                <td>{{ team.parentCount }}</td>
                <td>{{ team.childrenCount }}</td>
              </tr>
            </tbody>
      <tbody v-if="filteredTeams.length === 0">
        <tr>
          <td colspan="6">
            <AppEmptyState 
              icon="fa-solid fa-users"
              title="Không tìm thấy đội ngũ nào" 
              description="Thử tìm kiếm với tên khác."
            />
          </td>
        </tr>
      </tbody>
    </table>
    </section>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useTeamStore } from '@/store/useTeamStore'
import { AppEmptyState, AppUserChip } from '@/components/common/Foundation'
import ProjectPageToolbar from '@/components/common/ProjectPageToolbar.vue'

const router = useRouter()
const route = useRoute()
const teamsBasePath = computed(() => route.path.startsWith('/teams') ? '/teams' : '/home/teams')
const teamStore = useTeamStore()

const searchQuery = ref('')
const viewMode = ref('grid')

onMounted(() => {
  teamStore.initializeRealtime()
  teamStore.fetchAllTeams()
})

const filteredTeams = computed(() => {
  let list = teamStore.allTeams || []

  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    list = list.filter(t => t.name.toLowerCase().includes(q))
  }

  return list.map(t => ({
    ...t,
    avatarText: t.name ? t.name.substring(0, 2).toUpperCase() : 'T',
    memberCount: t.memberCount ?? t.members?.length ?? t.users?.length ?? 0,
    childrenCount: t.children?.length || t.subDepartments?.length || 0,
    manager: t.manager || t.managerId,
    managerName: t.manager?.fullName || t.manager?.name || 'Chưa có',
    managerEmail: t.manager?.email || '',
    parentTeamName: t.parentDepartment?.name || t.parent?.name || 'Không có đội ngũ gốc',
    parentCount: (t.parentDepartment || t.parent || t.parentId) ? 1 : 0,
    type: t.type || 'Đội ngũ chính thức'
  }))
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

/* Jira Table Styles */
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

.sort-icon {
  margin-left: 4px;
  font-size: 12px;
  color: #5E6C84;
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
  padding: 12px;
  font-size: 14px;
  color: #172B4D;
  border-bottom: 1px solid #DFE1E6;
  cursor: pointer;
}

.jira-table tbody tr:hover td {
  background-color: #FAFBFC;
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
  font-weight: 500;
  color: #0052CC;
}

.team-name:hover {
  text-decoration: underline;
}

.empty-table-state {
  text-align: center;
  padding: 40px !important;
  color: #5E6C84 !important;
}
</style>
