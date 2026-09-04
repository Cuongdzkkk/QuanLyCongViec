<template>
  <div>
      <header class="nexus-feature-header">
        <div class="header-info">
          <p class="eyebrow">Administration</p>
          <h1><i class="fa-solid fa-users-gear"></i> User & Team management</h1>
          <p class="muted">Manage platform access, organize teams, and configure system roles and permissions.</p>
        </div>
        <div class="nexus-controls-row">
          <button class="nexus-btn nexus-btn-primary" @click="showInviteModal = true">
            <i class="fa-solid fa-plus mr-1.5"></i> Invite member
          </button>
          <button class="nexus-btn nexus-btn-outlined" @click="showBulkInviteModal = true">
            <i class="fa-solid fa-file-import mr-1.5"></i> Import CSV
          </button>
        </div>
      </header>

          <el-tabs v-model="activeTab" class="user-tabs">
            <!-- TAB 1: USER LIST -->
            <el-tab-pane label="Người dùng" name="users">
              <!-- Filters Bar -->
              <div class="toolbar">
                <div class="search-box">
                  <el-input
                    v-model="searchQuery"
                    placeholder="Tìm theo tên, email..."
                    :prefix-icon="Search"
                    clearable
                  />
                </div>
                <div class="filter-group">
                  <el-select v-model="filterRole" placeholder="Vai trò" clearable style="width: 130px">
                    <el-option label="Admin" value="admin" />
                    <el-option label="Manager" value="manager" />
                    <el-option label="Member" value="member" />
                    <el-option label="Guest" value="guest" />
                  </el-select>
                  <el-select v-model="filterStatus" placeholder="Trạng thái" clearable style="width: 130px">
                    <el-option label="Active" value="active" />
                    <el-option label="Inactive" value="inactive" />
                    <el-option label="Pending" value="pending" />
                  </el-select>
                  <el-button plain @click="resetFilters">Làm mới</el-button>
                </div>
              </div>

              <!-- Bulk Actions Bar (Visible when selected) -->
              <div v-if="selectedUsers.length > 0" class="bulk-actions-bar">
                <span class="selected-count">{{ selectedUsers.length }} người dùng đã chọn</span>
                <el-divider direction="vertical" />
                <el-button size="small" plain @click="handleBulkRoleChange">Đổi Role</el-button>
                <el-button size="small" type="warning" plain @click="handleBulkDeactivate">Vô hiệu hóa</el-button>
                <el-button size="small" type="danger" plain @click="handleBulkDelete">Xóa</el-button>
              </div>

              <!-- User Table -->
              <div class="table-container jira-card">
                <el-table
                  :data="filteredUsers"
                  style="width: 100%"
                  @selection-change="handleSelectionChange"
                  v-loading="loading"
                >
                  <el-table-column type="selection" width="55" />
                  <el-table-column label="Thông tin cá nhân" min-width="250">
                    <template #default="scope">
                      <div class="user-cell">
                        <div class="user-avatar-circle" :style="{ background: scope.row.avatarColor }">
                          {{ scope.row.name.substring(0, 2).toUpperCase() }}
                        </div>
                        <div class="user-text">
                          <div class="user-name">{{ scope.row.name }}</div>
                          <div class="user-email text-muted">{{ scope.row.email }}</div>
                        </div>
                      </div>
                    </template>
                  </el-table-column>
                  <el-table-column prop="role" label="Vai trò" width="120">
                    <template #default="scope">
                      <el-tag :type="getRoleTagType(scope.row.role)" effect="light">
                        {{ scope.row.role.toUpperCase() }}
                      </el-tag>
                    </template>
                  </el-table-column>
                  <el-table-column prop="status" label="Trạng thái" width="120">
                    <template #default="scope">
                      <div class="status-cell">
                        <span class="dot" :class="scope.row.status"></span>
                        {{ scope.row.status.charAt(0).toUpperCase() + scope.row.status.slice(1) }}
                      </div>
                    </template>
                  </el-table-column>
                  <el-table-column prop="joinedDate" label="Ngày tham gia" width="150" />
                  <el-table-column prop="lastActive" label="Hoạt động cuối" width="180" />
                  <el-table-column label="Actions" width="120" fixed="right">
                    <template #default="scope">
                      <el-dropdown trigger="click">
                        <el-button link type="primary"><i class="fa-solid fa-ellipsis"></i></el-button>
                        <template #dropdown>
                          <el-dropdown-menu>
                            <el-dropdown-item @click="handleEditUser(scope.row)">Chỉnh sửa</el-dropdown-item>
                            <el-dropdown-item @click="handleResetPassword(scope.row)">Reset mật khẩu</el-dropdown-item>
                            <el-dropdown-item divided type="danger" @click="handleDeleteUser(scope.row)">Xóa tài khoản</el-dropdown-item>
                          </el-dropdown-menu>
                        </template>
                      </el-dropdown>
                    </template>
                  </el-table-column>
                </el-table>
                <div class="pagination-footer">
                  <el-pagination background layout="prev, pager, next" :total="filteredUsers.length" />
                </div>
              </div>
            </el-tab-pane>

            <!-- TAB 2: GROUPS / TEAMS -->
            <el-tab-pane label="Nhóm & Team" name="groups">
              <div class="groups-grid">
                <!-- Group Create Card -->
                <div class="group-card create-card" @click="showCreateGroupModal = true">
                  <div class="create-content">
                    <i class="fa-solid fa-plus-circle"></i>
                    <span>Tạo nhóm mới</span>
                  </div>
                </div>
                <!-- Mock Groups -->
                <div class="group-card" v-for="group in groups" :key="group.id">
                  <div class="group-header">
                    <h3 class="group-name">{{ group.name }}</h3>
                    <el-button link><i class="fa-solid fa-pen"></i></el-button>
                  </div>
                  <div class="group-meta">
                    <span class="member-count"><i class="fa-solid fa-users"></i> {{ group.members.length }} thành viên</span>
                    <span class="team-lead">Lead: {{ group.lead }}</span>
                  </div>
                  <div class="member-avatars">
                    <div class="avatar-stack">
                      <div v-for="m in group.members.slice(0, 4)" :key="m" class="small-avatar">{{ m.substring(0,1) }}</div>
                      <div v-if="group.members.length > 4" class="small-avatar more">+{{ group.members.length - 4 }}</div>
                    </div>
                  </div>
                  <div class="group-actions">
                    <el-button size="small" plain @click="handleManageGroup(group)">Quản lý thành viên</el-button>
                  </div>
                </div>
              </div>
            </el-tab-pane>

            <!-- TAB 3: ROLES & PERMISSIONS -->
            <el-tab-pane label="Vai trò & Quyền" name="roles">
              <div class="roles-container jira-card">
                <el-table :data="roleDefinitions" style="width: 100%">
                  <el-table-column prop="role" label="Vai trò" width="150" />
                  <el-table-column prop="description" label="Quyền tiêu biểu" />
                </el-table>
              </div>
            </el-tab-pane>

            <!-- TAB 4: SETTINGS -->
            <el-tab-pane label="Cài đặt bảo mật" name="settings">
              <div class="settings-grid">
                <div class="settings-card jira-card">
                  <h3>Chính sách & Xác thực</h3>
                  <div class="setting-row">
                    <div class="setting-info">
                      <div class="setting-label">Cho phép tự đăng ký</div>
                      <div class="setting-desc">Cho phép thành viên mới tự tạo tài khoản qua trang Register.</div>
                    </div>
                    <el-switch v-model="securitySettings.allowSelfRegister" />
                  </div>
                  <div class="setting-row">
                    <div class="setting-info">
                      <div class="setting-label">Yêu cầu xác nhận email</div>
                      <div class="setting-desc">Tất cả thành viên mới phải xác nhận email trước khi đăng nhập.</div>
                    </div>
                    <el-switch v-model="securitySettings.requireVerify" />
                  </div>
                  <el-divider />
                  <h3>Cấu hình SSO / OAuth</h3>
                  <div class="oauth-links">
                    <div class="oauth-item">
                      <i class="fa-brands fa-google"></i>
                      <span>Google Login (Đã bật)</span>
                      <el-button size="small" link type="primary">Cấu hình</el-button>
                    </div>
                    <div class="oauth-item">
                      <i class="fa-brands fa-microsoft"></i>
                      <span>Microsoft SSO</span>
                      <el-button size="small" type="primary" plain>Kết nối</el-button>
                    </div>
                  </div>
                </div>
              </div>
            </el-tab-pane>
          </el-tabs>    <!-- Edit User Drawer (Reuse detailed requirement 4) -->
    <el-drawer v-model="editDrawerVisible" title="Chi tiết người dùng" size="450px">
      <div v-if="editingUser" class="drawer-content">
        <div class="profile-header">
          <div class="large-avatar" :style="{ background: editingUser.avatarColor }">
            {{ editingUser.name.substring(0, 2).toUpperCase() }}
          </div>
          <h3>{{ editingUser.name }}</h3>
          <span class="text-muted">{{ editingUser.email }}</span>
        </div>
        
        <el-form label-position="top">
          <el-form-item label="Họ tên">
            <el-input v-model="editingUser.name" />
          </el-form-item>
          <el-form-item label="Vai trò">
            <el-select v-model="editingUser.role" style="width: 100%">
              <el-option label="Admin" value="admin" />
              <el-option label="Manager" value="manager" />
              <el-option label="Member" value="member" />
              <el-option label="Guest" value="guest" />
            </el-select>
          </el-form-item>
          <el-form-item label="Phòng ban / Team">
            <el-select v-model="editingUser.department" style="width: 100%">
              <el-option label="Engineering" value="eng" />
              <el-option label="Product" value="prod" />
              <el-option label="Design" value="design" />
            </el-select>
          </el-form-item>
          <el-divider />
          <div class="danger-zone">
            <h4>Khu vực nguy hiểm</h4>
            <el-button type="warning" plain @click="handleSendResetEmail">Gửi link đổi mật khẩu</el-button>
            <el-button type="danger" @click="handleDeactivate">Vô hiệu hóa tài khoản</el-button>
          </div>
        </el-form>
      </div>
    </el-drawer>

    <!-- Customize Sidebar Modal -->
    <CustomizeSidebarModal :visible="showCustomizeModal" @update:visible="showCustomizeModal = $event" @saved="handleSidebarSaved" />
  </div>
</template>
<script setup>
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import logoImg from '../assets/logo_QLCV.png'
import UserDropdown from '../components/UserDropdown.vue'
import NotificationsDropdown from '../components/NotificationsDropdown.vue'
import CustomizeSidebarModal from '../components/CustomizeSidebarModal.vue'
import { Plus, Search, Edit, Delete, Key, More, ArrowDown, User, Monitor, Folder } from '@element-plus/icons-vue'


import { onMounted } from 'vue'

const router = useRouter()
const currentUser = JSON.parse(localStorage.getItem('user') || '{}')
const isAdmin = computed(() => {
  const roles = currentUser.systemRoles || []
  return roles.includes('Admin') || roles.includes('admin')
})

const isPM = computed(() => {
  const roles = currentUser.systemRoles || []
  return roles.includes('Manager') || roles.includes('manager') || roles.includes('PM')
})

const isAuthorized = computed(() => isAdmin.value || isPM.value)
const activeTab = ref('users')
const loading = ref(false)
const searchQuery = ref('')
const filterRole = ref('')
const filterStatus = ref('')
const selectedUsers = ref([])

const editDrawerVisible = ref(false)
const editingUser = ref(null)

const showInviteModal = ref(false)
const showBulkInviteModal = ref(false)
const showCreateGroupModal = ref(false)

const securitySettings = ref({
  allowSelfRegister: true,
  requireVerify: true
})

const showCustomizeModal = ref(false)
const sidebarPreferences = ref({
  audit: true,
  users: true
})

// Mock Data
const users = ref([
    { id: 1, name: 'Cường Nguyễn', email: 'cuong@sprinta.io', role: 'admin', status: 'active', joinedDate: '2026-03-01', lastActive: 'Vừa xong', avatarColor: '#3b82f6' },
    { id: 2, name: 'Linh Trần', email: 'linh.tran@sprinta.io', role: 'manager', status: 'active', joinedDate: '2026-03-05', lastActive: '2 giờ trước', avatarColor: '#a855f7' },
    { id: 3, name: 'Minh Hoàng', email: 'minh.h@gmail.com', role: 'member', status: 'pending', joinedDate: '2026-03-29', lastActive: 'Chưa đăng nhập', avatarColor: '#10b981' },
    { id: 4, name: 'Hương Lê', email: 'huong.le@design.com', role: 'member', status: 'active', joinedDate: '2026-03-10', lastActive: 'Sáng nay', avatarColor: '#f97316' },
    { id: 5, name: 'Khách hàng A', email: 'guest@vips.com', role: 'guest', status: 'inactive', joinedDate: '2026-02-15', lastActive: '1 tháng trước', avatarColor: '#64748b' },
])

const groups = ref([
  { id: 1, name: 'Frontend Team', members: ['Cường', 'Linh', 'Minh', 'Hương', 'An', 'Bình'], lead: 'Cường Nguyễn' },
  { id: 2, name: 'Designers', members: ['Hương', 'Thảo', 'Duy'], lead: 'Hương Lê' },
  { id: 3, name: 'Quản trị viên', members: ['Cường'], lead: 'Cường Nguyễn' },
])

const roleDefinitions = [
  { role: 'Admin', description: 'Toàn quyền hệ thống, quản lý bảo mật, billing và phân quyền người dùng.' },
  { role: 'Manager', description: 'Quản lý dự án, phê duyệt công việc, phân công task và xem báo cáo.' },
  { role: 'Member', description: 'Thực hiện task được giao, tạo task mới trong không gian được phép.' },
  { role: 'Guest', description: 'Chỉ xem nội dung, không có quyền chỉnh sửa hoặc tạo mới.' }
]

const filteredUsers = computed(() => {
  // Logic "Chỉ khi add ai vào rồi thì mới xuất hiện" (Only when they are added to the platform)
  // We simulate this by checking status and a dummy isAdded flag if needed, 
  // or assuming members fetching results in "added" users.
  return users.value.filter(u => {
    // Basic filter: Search & Role & Status
    const matchSearch = u.name.toLowerCase().includes(searchQuery.value.toLowerCase()) || 
                      u.email.toLowerCase().includes(searchQuery.value.toLowerCase())
    const matchRole = !filterRole.value || u.role === filterRole.value
    const matchStatus = !filterStatus.value || u.status === filterStatus.value
    
    // "Only Added" logic: e.g., Filter out users without status 'active' or 'pending' 
    // or simulate that those not 'added' are simply not in this list.
    return matchSearch && matchRole && matchStatus
  })
})

const getRoleTagType = (role) => {
  switch(role) {
    case 'admin': return 'danger'
    case 'manager': return 'primary'
    case 'member': return 'success'
    case 'guest': return 'info'
    default: return ''
  }
}

const handleSelectionChange = (selection) => {
  selectedUsers.value = selection
}

const handleEditUser = (user) => {
  editingUser.value = { ...user }
  editDrawerVisible.value = true
}

const handleResetPassword = (user) => {
  ElMessage.success(`Đã gửi yêu cầu reset mật khẩu đến ${user.email}`)
}

const handleDeleteUser = (user) => {
  ElMessage.warning(`Bạn đang cố gắng xóa người dùng ${user.name}`)
}

const resetFilters = () => {
    searchQuery.value = ''
    filterRole.value = ''
    filterStatus.value = ''
}

// Bulk handles
const handleBulkRoleChange = () => ElMessage.info('Thay đổi role hàng loạt')
const handleBulkDeactivate = () => ElMessage.warning('Vô hiệu hóa hàng loạt')
const handleBulkDelete = () => ElMessage.error('Xóa hàng loạt')

const handleManageGroup = (group) => ElMessage.info(`Quản lý thành viên nhóm ${group.name}`)

onMounted(() => {
  // Second protection: Redirect if not PM or Admin
  if (!isAuthorized.value) {
    ElMessage.error('Bạn không có quyền truy cập trang Quản lý người dùng.')
    router.push('/dashboard')
    return
  }

  const saved = localStorage.getItem('sidebarPreferences')
  if (saved) {
    try {
      const prefs = JSON.parse(saved)
      const newPrefs = { ...sidebarPreferences.value }
      if (prefs && prefs.navItems) {
        prefs.navItems.forEach(item => {
          if (['recent', 'spaces', 'ai', 'audit', 'users'].includes(item.id)) {
            newPrefs[item.id] = item.checked
          }
        })
      } else {
        Object.assign(newPrefs, prefs)
      }
      sidebarPreferences.value = newPrefs
    } catch (e) {
      console.error('Error parsing sidebar preferences:', e)
    }
  }
})

const handleSidebarSaved = (prefs) => {
  const newPrefs = { ...sidebarPreferences.value }
  if (prefs && prefs.navItems) {
    prefs.navItems.forEach(item => {
      if (['recent', 'spaces', 'ai', 'audit', 'users'].includes(item.id)) {
        newPrefs[item.id] = item.checked
      }
    })
  }
  sidebarPreferences.value = newPrefs
  localStorage.setItem('sidebarPreferences', JSON.stringify(newPrefs))
}

</script>

<style scoped>
.page-header-flex { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
.page-title { font-size: 26px; font-weight: 700; color: var(--color-text-primary); margin: 0; }

.toolbar { display: flex; gap: 16px; margin-bottom: 20px; flex-wrap: wrap; }
.search-box { width: 300px; }
.filter-group { display: flex; gap: 12px; }

.bulk-actions-bar {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  padding: 8px 16px;
  border-radius: 6px;
  margin-bottom: 12px;
  display: flex;
  align-items: center;
  gap: 12px;
}
.selected-count { font-size: 13px; font-weight: 600; color: #3b82f6; }

.table-container { background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 8px; overflow: hidden; }

.user-cell { display: flex; align-items: center; gap: 12px; }
.user-avatar-circle { width: 36px; height: 36px; border-radius: 50%; display: flex; align-items: center; justify-content: center; color: white; font-weight: 700; font-size: 13px; }
.user-name { font-weight: 600; font-size: 14px; color: var(--color-text-primary); }
.user-email { font-size: 12px; }

.status-cell { display: flex; align-items: center; gap: 8px; font-size: 13px; }
.dot { width: 8px; height: 8px; border-radius: 50%; }
.dot.active { background: #10b981; }
.dot.pending { background: #f59e0b; }
.dot.inactive { background: #64748b; }

.pagination-footer { padding: 16px; display: flex; justify-content: flex-end; }

/* Groups Tab Styling */
.groups-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 20px; }
.group-card { 
  background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 8px; padding: 20px; cursor: pointer; transition: transform 0.2s;
}
.group-card:hover { border-color: #3b82f6; transform: translateY(-2px); }
.group-card.create-card { 
  border: 2px dashed var(--color-border); background: transparent; display: flex; align-items: center; justify-content: center; min-height: 180px; color: var(--color-text-secondary);
}
.create-content { display: flex; flex-direction: column; align-items: center; gap: 12px; }
.group-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
.group-name { font-size: 16px; font-weight: 700; margin: 0; color: var(--color-text-primary); }
.group-meta { display: flex; flex-direction: column; gap: 4px; font-size: 13px; color: var(--color-text-muted); margin-bottom: 20px; }
.avatar-stack { display: flex; margin-left: 8px; }
.small-avatar { 
    width: 24px; height: 24px; background: var(--color-surface-hover); border: 2px solid var(--color-surface); border-radius: 50%; margin-left: -8px; font-size: 10px; display: flex; align-items: center; justify-content: center; font-weight: 700;
}
.small-avatar.more { background: var(--color-bg); }

/* Settings Styling */
.settings-grid { max-width: 800px; }
.setting-row { display: flex; justify-content: space-between; align-items: center; padding: 16px 0; border-bottom: 1px solid var(--color-border); }
.setting-label { font-weight: 600; font-size: 14px; margin-bottom: 4px; }
.setting-desc { font-size: 12px; color: var(--color-text-muted); }
.oauth-item { display: flex; align-items: center; gap: 12px; padding: 12px; border-radius: 8px; background: var(--color-surface); margin-top: 12px; }
.oauth-item i { font-size: 20px; }

/* Drawer */
.profile-header { text-align: center; margin-bottom: 32px; }
.large-avatar { width: 80px; height: 80px; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 28px; font-weight: 800; color: white; margin: 0 auto 16px; }
.drawer-content { padding: 0 24px; }
.danger-zone { margin-top: 32px; padding: 16px; border: 1px solid #ef444433; border-radius: 8px; display: flex; flex-direction: column; gap: 12px; }
.danger-zone h4 { color: #ef4444; margin: 0 0 8px 0; font-size: 14px; }

/* Custom Element Plus theme styles local to this component */
:deep(.el-table) {
  --el-table-bg-color: var(--color-surface);
  --el-table-tr-bg-color: var(--color-surface);
  --el-table-header-bg-color: var(--color-bg);
  --el-table-header-text-color: var(--color-text-secondary);
  --el-table-text-color: var(--color-text-primary);
  --el-table-border-color: var(--color-border);
  --el-table-row-hover-bg-color: var(--color-surface-hover);
}

:deep(.el-input__wrapper), :deep(.el-select__wrapper) {
  background-color: var(--color-surface) !important;
  box-shadow: 0 0 0 1px var(--color-border) inset !important;
}

:deep(.el-input__inner) {
  color: var(--color-text-primary) !important;
}
</style>


