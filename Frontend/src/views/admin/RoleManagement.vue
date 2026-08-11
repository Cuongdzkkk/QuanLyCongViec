<template>
  <AdminLayout>
    <el-container style="height: calc(100vh - 80px);">
      <!-- Sidebar -->
      <el-aside width="320px" style="border-right: 1px solid var(--el-border-color-light); background: var(--el-bg-color);">
        <RoleSidebar 
          :roles="roles" 
          :selectedRoleId="selectedRoleId"
          @select-role="selectRole" 
          @create-role="openCreateRole"
          @duplicate-role="duplicateRole"
          @edit-role="editRole"
          @delete-role="deleteRole"
        />
      </el-aside>
      
      <!-- Main Content -->
      <el-main class="role-management-main" style="padding: 0; background: var(--el-bg-color-page); display: flex; flex-direction: column;">
        <template v-if="selectedRole">
          <div class="role-content-panel" style="flex: 1; padding: 0; overflow: hidden; display: flex; flex-direction: column;">
            <el-card shadow="never" style="flex: 1; display: flex; flex-direction: column; border: none; border-radius: 8px;" body-style="padding: 0; display: flex; flex-direction: column; height: 100%; overflow: hidden;">
              <el-tabs v-model="activeTab" style="height: 100%; display: flex; flex-direction: column;" class="content-tabs">
                <el-tab-pane label="Permissions" name="permissions">
                  <PermissionsTab 
                    :role="selectedRole"
                    :allPermissions="permissions"
                    :saving="saving"
                    @save="saveRolePermissions"
                  >
                    <template #role-header>
                      <div class="role-header-panel">
                        <RoleHeader
                          :role="selectedRole"
                          :userCount="roleUserCount"
                          @duplicate="duplicateRole(selectedRole)"
                        />
                      </div>
                    </template>
                  </PermissionsTab>
                </el-tab-pane>
                <el-tab-pane :label="`Members (${roleUserCount})`" name="members">
                  <MembersTab 
                    :role="selectedRole"
                    :allUsers="users"
                    :loading="loadingUsers"
                    @assign-members="assignMembers"
                    @remove-member="removeMember"
                  />
                </el-tab-pane>
                <el-tab-pane label="History" name="history">
                  <div style="padding: 40px;">
                    <el-empty description="History API is not supported yet." />
                  </div>
                </el-tab-pane>
              </el-tabs>
            </el-card>
          </div>
        </template>
        
        <div v-else style="height: 100%; display: flex; align-items: center; justify-content: center;">
          <el-empty description="Select a role from the sidebar to view details" />
        </div>
      </el-main>
    </el-container>

    <!-- Create/Edit Dialog -->
    <el-dialog
      v-model="roleDialogVisible"
      width="480px"
      destroy-on-close
      append-to-body
      class="sa-data-dialog sa-modal--form"
      :show-close="false"
    >
      <template #header>
        <DataModalHeader
          icon="bi bi-person-badge"
          :title="editingRole ? 'Edit Role' : 'Create Role'"
          description="Define the role name and its responsibility"
          close-label="Close"
          @close="roleDialogVisible = false"
        />
      </template>
      <el-form :model="roleForm" :rules="roleRules" ref="roleFormRef" label-position="top">
        <el-form-item label="Role Name" prop="name">
          <el-input v-model="roleForm.name" placeholder="Enter role name" size="large" />
        </el-form-item>
        <el-form-item label="Description" prop="description">
          <el-input 
            v-model="roleForm.description" 
            type="textarea" 
            :rows="3"
            placeholder="Brief description" 
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button class="cancel-btn" @click="roleDialogVisible = false"><i class="bi bi-x-lg"></i> Cancel</el-button>
        <el-button type="primary" @click="submitRoleForm" :loading="saving">
          {{ editingRole ? 'Save' : 'Create' }}
        </el-button>
      </template>
    </el-dialog>
  </AdminLayout>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import { ElMessage, ElMessageBox } from 'element-plus'
import AdminLayout from '@/components/layout/AdminLayout.vue'
import { useAdminUserStore } from '@/store/useAdminUserStore'
import RoleSidebar from '@/components/RoleManagement/RoleSidebar.vue'
import RoleHeader from '@/components/RoleManagement/RoleHeader.vue'
import PermissionsTab from '@/components/RoleManagement/PermissionsTab.vue'
import MembersTab from '@/components/RoleManagement/MembersTab.vue'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'

const adminUserStore = useAdminUserStore()
const { users, roles, permissions } = storeToRefs(adminUserStore)

const activeTab = ref('permissions')
const selectedRoleId = ref('')
const loadingUsers = ref(false)
const saving = ref(false)

const roleDialogVisible = ref(false)
const editingRole = ref(null)
const roleFormRef = ref(null)
const roleForm = ref({ name: '', description: '' })
const roleRules = {
  name: [
    { required: true, message: 'Required field', trigger: 'blur' },
    { max: 50, message: 'Max 50 characters', trigger: 'blur' }
  ]
}

const selectedRole = computed(() => roles.value.find(r => r.id === selectedRoleId.value) || null)
const roleUserCount = computed(() => {
  if (!selectedRole.value) return 0
  return users.value.filter(u => (u.roles || []).includes(selectedRole.value.name)).length
})

onMounted(async () => {
  await loadData()
})

async function loadData() {
  loadingUsers.value = true
  try {
    await Promise.all([
      adminUserStore.fetchRoles(),
      adminUserStore.fetchUsers()
    ])
    if (roles.value.length > 0 && !selectedRoleId.value) {
      selectedRoleId.value = roles.value[0].id
    }
  } catch (err) {
    ElMessage.error('Failed to load data')
  } finally {
    loadingUsers.value = false
  }
}

function selectRole(role) {
  selectedRoleId.value = role.id
  activeTab.value = 'permissions'
}

function openCreateRole() {
  editingRole.value = null
  roleForm.value = { name: '', description: '' }
  roleDialogVisible.value = true
}

function editRole(role) {
  editingRole.value = role
  roleForm.value = { name: role.name, description: role.description }
  roleDialogVisible.value = true
}

function duplicateRole(role) {
  editingRole.value = null
  roleForm.value = { name: role.name + ' (Copy)', description: role.description }
  roleDialogVisible.value = true
  roleForm.value._permissionIds = role.permissionIds || []
}

async function submitRoleForm() {
  if (!roleFormRef.value) return
  await roleFormRef.value.validate(async (valid) => {
    if (!valid) return
    saving.value = true
    try {
      if (editingRole.value) {
        await adminUserStore.updateRole(editingRole.value.id, {
          name: roleForm.value.name,
          description: roleForm.value.description,
          permissionIds: editingRole.value.permissionIds
        })
        ElMessage.success('Role updated')
      } else {
        await adminUserStore.createRole({
          name: roleForm.value.name,
          description: roleForm.value.description,
          permissionIds: roleForm.value._permissionIds || []
        })
        ElMessage.success('Role created')
      }
      roleDialogVisible.value = false
    } catch (err) {
      ElMessage.error(err.response?.data?.message || 'Error saving role')
    } finally {
      saving.value = false
    }
  })
}

async function deleteRole(role) {
  try {
    await ElMessageBox.confirm(`Are you sure you want to delete "${role.name}"?`, 'Confirm', {
      type: 'warning',
      confirmButtonText: 'Delete'
    })
    saving.value = true
    await adminUserStore.deleteRole(role.id)
    if (selectedRoleId.value === role.id) selectedRoleId.value = ''
    ElMessage.success('Role deleted')
  } catch (e) {
    if (e !== 'cancel') ElMessage.error(e.response?.data?.message || 'Error deleting role')
  } finally {
    saving.value = false
  }
}

async function saveRolePermissions(newPermissionIds) {
  if (!selectedRole.value) return
  saving.value = true
  try {
    await adminUserStore.updateRole(selectedRole.value.id, {
      name: selectedRole.value.name,
      description: selectedRole.value.description,
      permissionIds: newPermissionIds
    })
    ElMessage.success('Permissions saved')
  } catch (err) {
    ElMessage.error(err.response?.data?.message || 'Error saving permissions')
  } finally {
    saving.value = false
  }
}

async function assignMembers(userIds) {
  if (!selectedRole.value) return
  saving.value = true
  try {
    const roleToAdd = selectedRole.value
    for (const uId of userIds) {
      const user = users.value.find(u => u.id === uId)
      if (user) {
        const existingRoleIds = roles.value
          .filter(r => (user.roles || []).includes(r.name))
          .map(r => r.id)
        
        if (!existingRoleIds.includes(roleToAdd.id)) {
          await adminUserStore.assignUserRoles(uId, [...existingRoleIds, roleToAdd.id])
        }
      }
    }
    ElMessage.success('Members assigned')
    await adminUserStore.fetchUsers()
  } catch (err) {
    ElMessage.error('Error assigning members')
  } finally {
    saving.value = false
  }
}

async function removeMember(user) {
  if (!selectedRole.value) return
  try {
    await ElMessageBox.confirm(`Remove this member from ${selectedRole.value.name}?`, 'Confirm')
    saving.value = true
    const existingRoleIds = roles.value
      .filter(r => (user.roles || []).includes(r.name) && r.id !== selectedRole.value.id)
      .map(r => r.id)
      
    await adminUserStore.assignUserRoles(user.id, existingRoleIds)
    ElMessage.success('Member removed')
    await adminUserStore.fetchUsers()
  } catch (e) {
    if (e !== 'cancel') ElMessage.error('Error removing member')
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.role-management-main {
  width: 100%;
  flex: 1 1 auto;
  min-height: 0;
  min-width: 0;
  box-sizing: border-box;
}

.role-content-panel {
  order: 1;
  width: 100%;
  min-height: 0;
  min-width: 0;
  box-sizing: border-box;
  align-self: stretch;
}

.role-header-panel {
  padding: 16px 24px;
  background: var(--el-bg-color);
  border-top: 1px solid var(--el-border-color-light);
  border-bottom: 1px solid var(--el-border-color-light);
  flex: 0 0 auto;
}

.role-content-panel :deep(.el-card),
.role-content-panel :deep(.el-tabs),
.role-content-panel :deep(.el-tab-pane) {
  width: 100%;
  min-width: 0;
  box-sizing: border-box;
}

::v-deep(.content-tabs > .el-tabs__header) {
  margin: 0;
  padding: 0 24px;
  background: var(--el-bg-color);
  border-bottom: 1px solid var(--el-border-color-light);
}
::v-deep(.content-tabs > .el-tabs__content) {
  flex: 1;
  overflow: hidden;
  display: flex;
}
::v-deep(.content-tabs .el-tab-pane) {
  flex: 1;
  display: flex;
  flex-direction: column;
  height: 100%;
}
</style>
