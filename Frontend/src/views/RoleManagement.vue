<template>
  <div class="role-management-wrapper">
    <div class="role-management-layout dark-theme">
      <!-- Sidebar Column (25%) -->
      <RoleSidebar 
        :roles="allRoles" 
        :selectedRoleId="selectedRole?.id"
        @select-role="selectRole" 
        @create-role="openCreateRole"
      />
      
      <!-- Main Content Column (75%) -->
      <div class="main-panel">
        <RoleHeader :role="selectedRole" v-if="selectedRole" />
        
        <div class="main-content-area" v-if="selectedRole">
          <div class="tabs-container">
            <button 
              class="tab-btn" 
              :class="{ active: activeTab === 'permissions' }" 
              @click="activeTab = 'permissions'"
            >
              Permissions
            </button>
            <button 
              class="tab-btn" 
              :class="{ active: activeTab === 'members' }" 
              @click="activeTab = 'members'"
            >
              Members
            </button>
            <button 
              class="tab-btn" 
              :class="{ active: activeTab === 'history' }" 
              @click="activeTab = 'history'"
            >
              History
            </button>
          </div>
          
          <div class="tab-content-area">
            <PermissionsTab v-if="activeTab === 'permissions'" :role="selectedRole" />
            <MembersTab v-else-if="activeTab === 'members'" :role="selectedRole" />
            <HistoryTab v-else-if="activeTab === 'history'" :role="selectedRole" />
          </div>
        </div>
        
        <!-- Empty State when no role is selected -->
        <!-- Empty State when no role is selected -->
        <ProjectEmptyState
          v-else
          icon="fa-solid fa-shield-halved"
          title="Select a Role"
          description="Choose a role from the sidebar to view its permissions and members."
        />
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import RoleSidebar from '@/components/RoleManagement/RoleSidebar.vue';
import RoleHeader from '@/components/RoleManagement/RoleHeader.vue';
import PermissionsTab from '@/components/RoleManagement/PermissionsTab.vue';
import MembersTab from '@/components/RoleManagement/MembersTab.vue';
import HistoryTab from '@/components/RoleManagement/HistoryTab.vue';
import ProjectEmptyState from '@/components/common/ProjectEmptyState.vue';

const activeTab = ref('permissions');

const allRoles = ref([
  {
    id: 'r1',
    name: 'Administrator',
    description: 'System administrator with full access to all settings and data.',
    type: 'System',
    createdAt: 'Jan 23, 2023',
    creator: { name: 'System' },
    userCount: 2,
    permissionCount: 45,
    riskLevel: 'High',
    isDefault: false
  },
  {
    id: 'r2',
    name: 'Project Manager',
    description: 'Can create and manage projects, assign tasks, and view reports.',
    type: 'System',
    createdAt: 'Jan 23, 2023',
    creator: { name: 'System' },
    userCount: 5,
    permissionCount: 30,
    riskLevel: 'Medium',
    isDefault: false
  },
  {
    id: 'r3',
    name: 'Developer',
    description: 'Standard access for developers to work on assigned tasks.',
    type: 'System',
    createdAt: 'Feb 10, 2023',
    creator: { name: 'System' },
    userCount: 15,
    permissionCount: 20,
    riskLevel: 'Low',
    isDefault: true
  },
  {
    id: 'r4',
    name: 'QA Lead',
    description: 'Custom role for QA team leads to manage testing processes.',
    type: 'Custom',
    createdAt: 'Mar 05, 2024',
    creator: { name: 'John Kenwin' },
    userCount: 3,
    permissionCount: 25,
    riskLevel: 'Medium',
    isDefault: false
  }
]);

const selectedRole = ref(allRoles.value[0]);

function selectRole(role) {
  selectedRole.value = role;
  // Reset tab to permissions when switching roles
  activeTab.value = 'permissions';
}

function openCreateRole() {
  console.log('Open create role modal');
}
</script>

<style>
/* Global resets for the dark theme inside this view */
.dark-theme {
  --bg-darker: #0B0D14;
  --bg-dark: #11131a;
  --bg-panel: #161922;
  --border-color: #222631;
  --text-primary: #f8fafc;
  --text-secondary: #94a3b8;
  --text-muted: #64748b;
  --accent-blue: #3b82f6;
  --accent-green: #10b981;
  --accent-red: #ef4444;
  --accent-orange: #f59e0b;
}

.role-management-wrapper {
  margin: 0;
  padding: 0;
  font-family: 'Inter', -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
  box-sizing: border-box;
  background-color: var(--bg-darker);
  height: 100vh;
}

.role-management-wrapper * {
  box-sizing: border-box;
}
</style>

<style scoped>
.role-management-layout {
  display: flex;
  height: 100vh;
  width: 100vw;
  overflow: hidden;
  color: var(--text-primary);
}

.main-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: var(--bg-darker);
  position: relative;
  overflow: hidden;
}

.main-content-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.tabs-container {
  display: flex;
  padding: 0 32px;
  background: var(--bg-dark);
  border-bottom: 1px solid var(--border-color);
}

.tab-btn {
  background: transparent;
  border: none;
  color: var(--text-muted);
  font-size: 14px;
  font-weight: 500;
  padding: 16px 24px;
  cursor: pointer;
  position: relative;
  transition: color 0.2s;
}

.tab-btn:hover {
  color: var(--text-secondary);
}

.tab-btn.active {
  color: var(--accent-blue);
}

.tab-btn.active::after {
  content: '';
  position: absolute;
  bottom: -1px;
  left: 0;
  right: 0;
  height: 2px;
  background-color: var(--accent-blue);
  border-radius: 2px 2px 0 0;
}

.tab-content-area {
  flex: 1;
  overflow-y: auto;
  padding: 32px;
}

/* Custom Scrollbar for tab content */
.tab-content-area::-webkit-scrollbar {
  width: 8px;
}
.tab-content-area::-webkit-scrollbar-track {
  background: transparent;
}
.tab-content-area::-webkit-scrollbar-thumb {
  background: #2d313f;
  border-radius: 4px;
}
.tab-content-area::-webkit-scrollbar-thumb:hover {
  background: #475569;
}


</style>
