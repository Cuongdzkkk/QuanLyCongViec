<template>
  <div style="height: 100%; display: flex; flex-direction: column;">
    <div style="padding: 16px 24px; border-bottom: 1px solid var(--el-border-color-light); display: flex; justify-content: space-between; align-items: center;">
      <el-space :size="16">
        <el-input
          v-model="searchQuery"
          placeholder="Search members..."
          prefix-icon="Search"
          clearable
          style="width: 260px;"
        />
        <el-select v-model="filterDept" placeholder="All Departments" clearable style="width: 180px;">
          <el-option v-for="dept in uniqueDepartments" :key="dept" :label="dept" :value="dept" />
        </el-select>
        <el-select v-model="filterStatus" placeholder="Any Status" clearable style="width: 140px;">
          <el-option label="Active" value="Active" />
          <el-option label="Invited" value="Invited" />
          <el-option label="Suspended" value="Suspended" />
        </el-select>
      </el-space>
      
      <el-space :size="12">
        <el-button 
          v-if="selectedRows.length > 0" 
          type="danger" 
          plain
          @click="handleBulkRemove"
        >
          Remove Selected ({{ selectedRows.length }})
        </el-button>
        <el-button type="primary" @click="dialogVisible = true">
          <el-icon style="margin-right: 6px;"><Plus /></el-icon> Add Members
        </el-button>
      </el-space>
    </div>

    <div style="flex: 1; overflow: hidden; padding: 0;" v-loading="loading">
      <el-table 
        :data="filteredMembers" 
        style="width: 100%; height: 100%;" 
        @selection-change="handleSelectionChange"
        :header-cell-style="{ background: 'var(--el-fill-color-light)', color: 'var(--el-text-color-secondary)', fontWeight: '600' }"
      >
        <template #empty>
          <div style="padding: 60px 0;">
            <el-empty description="No members found." />
          </div>
        </template>
        
        <el-table-column type="selection" width="55" align="center" />
        <el-table-column label="User Details" min-width="280">
          <template #default="{ row }">
            <div style="display: flex; align-items: center; gap: 16px; padding: 4px 0;">
              <el-avatar :size="36" :src="row.avatar" style="background: var(--el-color-primary-light-8); color: var(--el-color-primary); font-weight: 600;">
                {{ row.name?.charAt(0) || 'U' }}
              </el-avatar>
              <div style="display: flex; flex-direction: column; gap: 2px;">
                <el-text style="font-weight: 500; color: var(--el-text-color-primary);">{{ row.name }}</el-text>
                <el-text type="info" size="small">{{ row.email }}</el-text>
              </div>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="Department" min-width="180">
          <template #default="{ row }">
            <el-text v-if="row.departments?.length > 0">{{ row.departments.join(', ') }}</el-text>
            <el-text v-else type="info">-</el-text>
          </template>
        </el-table-column>
        <el-table-column prop="status" label="Status" width="140">
          <template #default="{ row }">
            <el-tag :type="getStatusType(row.status)" effect="plain" style="border-radius: 12px; padding: 0 12px;">{{ row.status }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="Actions" width="120" align="right">
          <template #default="{ row }">
            <el-button link type="danger" @click="$emit('remove-member', row)">
              Remove
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- Add Members Full Screen Dialog -->
    <el-dialog
      v-model="dialogVisible"
      :title="`Assign Users to ${role.name}`"
      fullscreen
      destroy-on-close
      class="sa-data-dialog"
      :show-close="false"
    >
      <template #header>
        <DataModalHeader icon="bi bi-person-plus" :title="`Assign users to ${role.name}`" description="Search and select users for this role" close-label="Close" @close="dialogVisible = false" />
      </template>
      <div style="display: flex; flex-direction: column; height: calc(100vh - 140px); max-width: 1200px; margin: 0 auto; width: 100%;">
        <DataModalSection icon="bi bi-search" title="Find users">
        <div style="margin-bottom: 24px;">
          <el-text type="info" style="margin-bottom: 16px; display: block;">Select users to assign them the {{ role.name }} role.</el-text>
          <el-space :size="16">
            <el-input v-model="addSearch" placeholder="Search users by name or email..." prefix-icon="Search" style="width: 300px;" size="large" />
            <el-select v-model="addFilterDept" placeholder="Filter by Department" clearable size="large" style="width: 220px;">
              <el-option v-for="dept in uniqueDepartments" :key="dept" :label="dept" :value="dept" />
            </el-select>
          </el-space>
        </div>
        </DataModalSection>
        
        <DataModalSection icon="bi bi-people" title="Available users">
        <div style="flex: 1; border: 1px solid var(--el-border-color-light); border-radius: 8px; overflow: hidden; box-shadow: var(--el-box-shadow-light);">
          <el-table 
            :data="nonMembers" 
            style="width: 100%; height: 100%;" 
            @selection-change="handleAddSelection"
            :header-cell-style="{ background: 'var(--el-fill-color-light)', color: 'var(--el-text-color-secondary)' }"
          >
            <el-table-column type="selection" width="55" align="center" />
            <el-table-column label="User" min-width="280">
              <template #default="{ row }">
                <div style="display: flex; align-items: center; gap: 16px; padding: 4px 0;">
                  <el-avatar :size="36" :src="row.avatar" style="background: var(--el-color-primary-light-8); color: var(--el-color-primary); font-weight: 600;">
                    {{ row.name?.charAt(0) || 'U' }}
                  </el-avatar>
                  <div style="display: flex; flex-direction: column; gap: 2px;">
                    <el-text style="font-weight: 500; color: var(--el-text-color-primary);">{{ row.name }}</el-text>
                    <el-text type="info" size="small">{{ row.email }}</el-text>
                  </div>
                </div>
              </template>
            </el-table-column>
            <el-table-column label="Department" min-width="180">
              <template #default="{ row }">
                <el-text v-if="row.departments?.length > 0">{{ row.departments.join(', ') }}</el-text>
                <el-text v-else type="info">-</el-text>
              </template>
            </el-table-column>
            <el-table-column prop="status" label="Status" width="140">
              <template #default="{ row }">
                <el-tag :type="getStatusType(row.status)" effect="plain" style="border-radius: 12px; padding: 0 12px;">{{ row.status }}</el-tag>
              </template>
            </el-table-column>
          </el-table>
        </div>
        </DataModalSection>
      </div>
      
      <template #footer>
        <div style="display: flex; justify-content: space-between; align-items: center; max-width: 1200px; margin: 0 auto;">
          <el-text type="info">{{ addSelectedRows.length }} users selected</el-text>
          <el-space :size="16">
            <el-button class="cancel-btn" @click="dialogVisible = false" size="large"><i class="bi bi-x-lg"></i>Cancel</el-button>
            <el-button type="primary" :disabled="addSelectedRows.length === 0" @click="submitAddMembers" size="large">
              Assign Role
            </el-button>
          </el-space>
        </div>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { Search, Plus } from '@element-plus/icons-vue'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'

const props = defineProps({
  role: { type: Object, required: true },
  allUsers: { type: Array, required: true },
  loading: { type: Boolean, default: false }
})

const emit = defineEmits(['assign-members', 'remove-member'])

const searchQuery = ref('')
const filterDept = ref('')
const filterStatus = ref('')
const selectedRows = ref([])

const dialogVisible = ref(false)
const addSearch = ref('')
const addFilterDept = ref('')
const addSelectedRows = ref([])

const uniqueDepartments = computed(() => {
  const depts = new Set()
  props.allUsers.forEach(u => {
    if (u.departments) u.departments.forEach(d => depts.add(d))
  })
  return Array.from(depts).sort()
})

const members = computed(() => {
  return props.allUsers.filter(u => (u.roles || []).includes(props.role.name))
})

const filteredMembers = computed(() => {
  return members.value.filter(u => {
    const matchSearch = !searchQuery.value || 
      u.name.toLowerCase().includes(searchQuery.value.toLowerCase()) || 
      u.email.toLowerCase().includes(searchQuery.value.toLowerCase())
    const matchDept = !filterDept.value || (u.departments || []).includes(filterDept.value)
    const matchStatus = !filterStatus.value || u.status === filterStatus.value
    return matchSearch && matchDept && matchStatus
  })
})

const nonMembers = computed(() => {
  let list = props.allUsers.filter(u => !(u.roles || []).includes(props.role.name) && !u.isDeleted)
  
  if (addSearch.value) {
    const q = addSearch.value.toLowerCase()
    list = list.filter(u => u.name.toLowerCase().includes(q) || u.email.toLowerCase().includes(q))
  }
  if (addFilterDept.value) {
    list = list.filter(u => (u.departments || []).includes(addFilterDept.value))
  }
  return list
})

function handleSelectionChange(val) {
  selectedRows.value = val
}

function handleAddSelection(val) {
  addSelectedRows.value = val
}

function handleBulkRemove() {
  selectedRows.value.forEach(row => {
    emit('remove-member', row)
  })
}

function submitAddMembers() {
  const userIds = addSelectedRows.value.map(r => r.id)
  emit('assign-members', userIds)
  dialogVisible.value = false
}

function getStatusType(status) {
  if (status === 'Active') return 'success'
  if (status === 'Invited') return 'warning'
  return 'info'
}
</script>
