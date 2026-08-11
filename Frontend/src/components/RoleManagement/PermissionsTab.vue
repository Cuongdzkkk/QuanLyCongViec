<template>
  <div class="permissions-tab-root" style="position: relative; height: 100%; display: flex; flex-direction: column;">
    
    <!-- PERMISSION SUMMARY -->
    <div style="padding: 24px 24px 16px 24px; background: var(--el-fill-color-light); border-bottom: 1px solid var(--el-border-color-light);">
      <el-row :gutter="16">
        <el-col :span="6">
          <el-card shadow="never" class="summary-card">
            <div class="summary-title">Modules</div>
            <div class="summary-value">{{ availableModules.length }}</div>
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card shadow="never" class="summary-card">
            <div class="summary-title">Total Functions</div>
            <div class="summary-value">{{ allPermissions.length }}</div>
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card shadow="never" class="summary-card">
            <div class="summary-title">Enabled</div>
            <div class="summary-value" style="color: var(--el-color-success);">{{ currentSelectedIds.length }}</div>
          </el-card>
        </el-col>
        <el-col :span="6">
          <el-card shadow="never" class="summary-card">
            <div class="summary-title">High Risk Enabled</div>
            <div class="summary-value" :style="{ color: highRiskCount > 0 ? 'var(--el-color-danger)' : 'inherit' }">
              {{ highRiskCount }}
            </div>
          </el-card>
        </el-col>
      </el-row>
    </div>

    <div class="permissions-header-workspace">
      <div class="permissions-role-column">
        <slot name="role-header" />
      </div>

      <!-- FILTER TOOLBAR -->
      <div class="permissions-toolbar">
        <div class="permissions-toolbar-search-row">
          <el-input
            class="permission-search-input"
            v-model="filters.search"
            placeholder="Search permissions..."
            prefix-icon="Search"
            clearable
          />
          <el-popover
            placement="bottom-end"
            trigger="click"
            :width="360"
            popper-class="permission-filter-popover"
          >
            <template #reference>
              <el-button class="permission-filter-trigger" :type="activeFilterCount > 0 ? 'primary' : 'default'" plain>
                <el-icon><Filter /></el-icon>
                <span>Filter</span>
                <el-badge v-if="activeFilterCount > 0" :value="activeFilterCount" />
              </el-button>
            </template>

            <div class="permission-filter-panel">
              <div class="permission-filter-panel-header">
                <div>
                  <strong>Filter permissions</strong>
                  <span>Refine the functions shown below</span>
                </div>
                <el-button v-if="activeFilterCount > 0" link type="primary" @click="clearPermissionFilters">Clear</el-button>
              </div>

              <div class="permission-filter-grid">
                <label class="permission-filter-field">
                  <span>Status</span>
                  <el-select v-model="filters.status" placeholder="Status">
                    <el-option label="All Status" value="all" />
                    <el-option label="Enabled Only" value="enabled" />
                    <el-option label="Disabled Only" value="disabled" />
                  </el-select>
                </label>

                <label class="permission-filter-field permission-filter-field-wide">
                  <span>Modules</span>
                  <el-select v-model="filters.modules" placeholder="All modules" clearable multiple collapse-tags collapse-tags-tooltip>
                    <el-option v-for="m in availableModules" :key="m" :label="getPermissionModuleLabel(m)" :value="m" />
                  </el-select>
                </label>

                <label class="permission-filter-field">
                  <span>Risk level</span>
                  <el-select v-model="filters.risk" placeholder="All risks" clearable>
                    <el-option label="Critical Risk" :value="4" />
                    <el-option label="High Risk" :value="3" />
                    <el-option label="Medium Risk" :value="2" />
                    <el-option label="Low Risk" :value="1" />
                  </el-select>
                </label>

                <label class="permission-filter-field">
                  <span>Action type</span>
                  <el-select v-model="filters.action" placeholder="All actions" clearable>
                    <el-option v-for="a in allDistinctActions" :key="a" :label="getPermissionActionLabel(a)" :value="a" />
                  </el-select>
                </label>
              </div>
            </div>
          </el-popover>
        </div>

        <div class="permissions-toolbar-main-row">
          <div class="permissions-toolbar-controls">
            <div class="permissions-toolbar-actions">
          <el-button type="primary" @click="editorVisible = true">
            <el-icon><FullScreen /></el-icon> Open Permission Editor
          </el-button>

          <el-radio-group v-model="viewMode" size="small">
            <el-radio-button label="tree">
              <el-icon><DataBoard /></el-icon> Tree
            </el-radio-button>
            <el-radio-button label="matrix">
              <el-icon><Grid /></el-icon> Matrix
            </el-radio-button>
          </el-radio-group>
            </div>

            <div class="permissions-toolbar-bulk">
              <el-button size="small" @click="bulkEnableFiltered" type="primary" plain :disabled="role.isProtected || filteredTotalCount === 0">Enable Filtered</el-button>
              <el-button size="small" @click="bulkDisableFiltered" type="danger" plain :disabled="role.isProtected || filteredTotalCount === 0">Disable Filtered</el-button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- MAIN CONTENT AREA -->
    <el-scrollbar class="permissions-content-scroll" style="flex: 1;" ref="scrollbarRef">
      <div style="padding: 24px; max-width: 100%; padding-bottom: 100px;">
        <el-alert
          v-if="role.isProtected"
          title="System Role"
          type="warning"
          description="This role is managed by the system. Its permissions are locked and cannot be modified."
          show-icon
          :closable="false"
          style="margin-bottom: 24px;"
        />
        
        <template v-if="hasResults">
          <!-- TREE VIEW (Accordion) -->
          <div v-if="viewMode === 'tree'" class="permissions-tree-content" style="max-width: 900px;">
            <el-collapse v-model="activeModules" style="border-top: none; border-bottom: none;">
              <el-collapse-item 
                v-for="(perms, moduleName) in filteredGroupedPermissions" 
                :key="moduleName" 
                :name="moduleName"
                style="margin-bottom: 16px; border: 1px solid var(--el-border-color-light); border-radius: 8px; overflow: hidden;"
              >
                <template #title>
                  <div style="display: flex; align-items: center; justify-content: space-between; width: 100%;">
                    <div class="permissions-module-title" style="display: flex; align-items: center; gap: 12px;">
                      <el-checkbox 
                        v-if="perms.length > 0"
                        :model-value="getModuleCheckState(moduleName)"
                        :indeterminate="isModuleIndeterminate(moduleName)"
                        :disabled="role.isProtected"
                        @change="val => toggleModule(moduleName, val)"
                        @click.stop
                      />
                      <el-text style="font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; color: var(--el-text-color-primary);">
                        {{ getPermissionModuleLabel(moduleName) }}
                      </el-text>
                    </div>
                    <el-text v-if="perms.length > 0" type="info" size="small">{{ perms.length }} functions - {{ getSelectedCount(moduleName) }} selected</el-text>
                    <el-tag v-else type="warning" size="small" effect="plain">No permission definitions available yet</el-tag>
                  </div>
                </template>
                
                <div style="padding: 16px 24px 24px 48px;">
                  <el-row :gutter="24">
                    <el-col :span="8" :xs="24" :sm="12" :md="8" v-for="perm in perms" :key="perm.id" style="margin-bottom: 16px;">
                      <el-tooltip placement="top" effect="dark" :hide-after="50" :open-delay="400">
                        <template #content>
                          <div style="max-width: 260px; font-size: 13px; line-height: 1.5;">
                            <div style="font-weight: bold; margin-bottom: 4px; font-size: 14px;">{{ getPermissionActionLabel(perm) }}</div>
                            <div style="margin-bottom: 8px; color: #a3a6ad;">{{ perm.code }}</div>
                            <div style="margin-bottom: 8px;">{{ getPermissionDescription(perm) }}</div>
                            <div style="margin-bottom: 4px; display: flex; align-items: center; gap: 6px;">
                              <span style="font-weight: 600;">Risk:</span> 
                              <el-tag size="small" :type="getRiskTagType(perm.riskLevel)">{{ getRiskLevelStr(perm.riskLevel) }}</el-tag>
                            </div>
                            <div v-if="perm.dependencyJson" style="margin-top: 8px; border-top: 1px solid #4c4d4f; padding-top: 8px;">
                              <span style="font-weight: 600;">Requires:</span> 
                              <span style="color: #e6a23c;">{{ formatDependencies(perm.dependencyJson) }}</span>
                            </div>
                          </div>
                        </template>
                        <el-checkbox
                          :model-value="currentSelectedIds.includes(perm.id)"
                          :disabled="role.isProtected"
                          @change="val => togglePermission(perm.id, val)"
                          style="width: 100%; display: flex; align-items: flex-start; height: auto;"
                        >
                          <el-text style="white-space: normal; line-height: 1.4; display: inline-block;">
                            {{ getPermissionActionLabel(perm) }}
                            <div style="font-size: 11px; color: var(--el-text-color-secondary);">{{ getPermissionDescription(perm) }}</div>
                          </el-text>
                        </el-checkbox>
                      </el-tooltip>
                    </el-col>
                  </el-row>
                </div>
              </el-collapse-item>
            </el-collapse>
          </div>

          <!-- MATRIX VIEW (Data Table) -->
          <div v-else-if="viewMode === 'matrix'" class="matrix-container">
            <el-table 
              :data="matrixTableData" 
              :span-method="matrixSpanMethod"
              style="width: 100%; border: 1px solid var(--el-border-color-light); border-radius: 8px;"
              :header-cell-style="{ background: 'var(--el-fill-color-light)', color: 'var(--el-text-color-primary)', fontWeight: '600' }"
              border
            >
              <el-table-column label="Module" min-width="200" align="center">
                <template #default="{ row }">
                  <div class="matrix-module-cell">
                    <el-text style="font-weight: 600; color: var(--el-text-color-primary);">
                      {{ getPermissionModuleLabel(row.moduleName) }}
                    </el-text>
                    <el-checkbox
                      :model-value="getModuleCheckState(row.moduleName)"
                      :indeterminate="isModuleIndeterminate(row.moduleName)"
                      :disabled="role.isProtected"
                      @change="val => toggleModule(row.moduleName, val)"
                    />
                  </div>
                </template>
              </el-table-column>
              
              <el-table-column label="Function" min-width="250">
                <template #default="{ row }">
                  <div style="display: flex; align-items: center; justify-content: space-between; width: 100%;">
                    <el-tooltip placement="top" effect="dark" :hide-after="50" :open-delay="400">
                      <template #content>
                        <div style="max-width: 260px; font-size: 13px; line-height: 1.5;">
                          <div style="font-weight: bold; margin-bottom: 4px; font-size: 14px;">{{ getPermissionActionLabel(row.perm) }}</div>
                          <div style="margin-bottom: 8px; color: #a3a6ad;">{{ row.perm.code }}</div>
                          <div style="margin-bottom: 8px;">{{ getPermissionDescription(row.perm) }}</div>
                          <div style="margin-bottom: 4px; display: flex; align-items: center; gap: 6px;">
                            <span style="font-weight: 600;">Risk:</span> 
                            <el-tag size="small" :type="getRiskTagType(row.perm.riskLevel)">{{ getRiskLevelStr(row.perm.riskLevel) }}</el-tag>
                          </div>
                          <div v-if="row.perm.dependencyJson" style="margin-top: 8px; border-top: 1px solid #4c4d4f; padding-top: 8px;">
                            <span style="font-weight: 600;">Requires:</span> 
                            <span style="color: #e6a23c;">{{ formatDependencies(row.perm.dependencyJson) }}</span>
                          </div>
                        </div>
                      </template>
                      <el-text style="display: flex; flex-direction: column; align-items: flex-start; line-height: 1.4;">
                        <span>{{ getPermissionActionLabel(row.perm) }}</span>
                        <span style="font-size: 11px; color: var(--el-text-color-secondary);">{{ getPermissionDescription(row.perm) }}</span>
                      </el-text>
                    </el-tooltip>
                    <el-tag v-if="row.perm.riskLevel >= 3" size="small" :type="getRiskTagType(row.perm.riskLevel)" effect="plain">
                      {{ getRiskLevelStr(row.perm.riskLevel) }} Risk
                    </el-tag>
                  </div>
                </template>
              </el-table-column>

              <el-table-column label="Enabled" width="120" align="center">
                <template #default="{ row }">
                  <el-checkbox
                    :model-value="currentSelectedIds.includes(row.perm.id)"
                    :disabled="role.isProtected"
                    @change="val => togglePermission(row.perm.id, val)"
                  />
                </template>
              </el-table-column>
            </el-table>
          </div>
        </template>
        
        <el-empty v-else description="No permissions match your filters." />
      </div>
    </el-scrollbar>

    <!-- Sticky Footer -->
    <div 
      v-if="hasChanges && !role.isProtected" 
      style="position: absolute; bottom: 24px; left: 50%; transform: translateX(-50%); background: var(--el-bg-color-overlay); padding: 16px 32px; border-radius: 12px; box-shadow: var(--el-box-shadow-light); z-index: 10; display: flex; align-items: center; gap: 32px; border: 1px solid var(--el-border-color-light);"
    >
      <el-text style="font-weight: 500;">You have unsaved changes to permissions.</el-text>
      <el-space>
        <el-button @click="discardChanges" :disabled="saving">Discard</el-button>
        <el-button type="primary" @click="saveChanges" :loading="saving">Save Changes</el-button>
      </el-space>
    </div>

    <!-- FULL SCREEN EDITOR DIALOG -->
    <PermissionEditorDialog
      v-model:visible="editorVisible"
      :role="role"
      :allPermissions="allPermissions"
      :saving="saving"
      @save="onEditorSave"
    />
  </div>
</template>

<script setup>
import { ref, computed, watch, reactive } from 'vue'
import { Search, Grid, DataBoard, FullScreen, Filter } from '@element-plus/icons-vue'
import PermissionEditorDialog from './PermissionEditorDialog.vue'
import {
  getPermissionActionLabel,
  getPermissionDescription,
  getPermissionModuleLabel
} from '@/utils/permissionPresentation'

const props = defineProps({
  role: { type: Object, required: true },
  allPermissions: { type: Array, required: true },
  saving: { type: Boolean, default: false }
})

const emit = defineEmits(['save'])

const viewMode = ref('tree') // 'tree' or 'matrix'
const currentSelectedIds = ref([])
const activeModules = ref([])
const editorVisible = ref(false)

const filters = reactive({
  search: '',
  status: 'all',
  modules: [],
  risk: '',
  action: ''
})

watch(() => props.role, (newRole) => {
  if (newRole) {
    currentSelectedIds.value = [...(newRole.permissionIds || [])]
  }
}, { immediate: true })

watch(() => props.saving, (newVal, oldVal) => {
  if (oldVal === true && newVal === false && editorVisible.value) {
    editorVisible.value = false
  }
})

const groupedPermissions = computed(() => {
  const groups = {}
  
  props.allPermissions.forEach(p => {
    const permission = p.riskLevel === undefined
      ? { ...p, riskLevel: inferRiskLevel(p.code) }
      : p
    const mod = permission.module || 'general'
    if (!groups[mod]) groups[mod] = []
    groups[mod].push(permission)
  })
  return groups
})

const availableModules = computed(() => Object.keys(groupedPermissions.value).sort())

const allDistinctActions = computed(() => {
  const actions = new Set()
  props.allPermissions.forEach(p => actions.add(getRawActionStr(p.code)))
  return Array.from(actions).sort()
})

const filteredGroupedPermissions = computed(() => {
  const q = filters.search.toLowerCase()
  const filtered = {}
  
  for (const [mod, perms] of Object.entries(groupedPermissions.value)) {
    if (filters.modules.length > 0 && !filters.modules.includes(mod)) continue

    let matched = perms

    if (filters.status === 'enabled') {
      matched = matched.filter(p => currentSelectedIds.value.includes(p.id))
    } else if (filters.status === 'disabled') {
      matched = matched.filter(p => !currentSelectedIds.value.includes(p.id))
    }
    
    if (filters.risk) {
      matched = matched.filter(p => p.riskLevel === filters.risk)
    }

    if (filters.action) {
      matched = matched.filter(p => getRawActionStr(p.code) === filters.action)
    }
    
    if (q) {
      matched = matched.filter(p => 
      p.code.toLowerCase().includes(q) ||
      getPermissionModuleLabel(mod).toLowerCase().includes(q) ||
      getPermissionActionLabel(p).toLowerCase().includes(q) ||
      getPermissionDescription(p).toLowerCase().includes(q)
      )
    }

    if (matched.length > 0) {
      filtered[mod] = matched
    }
  }
  return filtered
})

const hasResults = computed(() => Object.keys(filteredGroupedPermissions.value).length > 0)

const filteredTotalCount = computed(() => {
  let count = 0
  Object.values(filteredGroupedPermissions.value).forEach(arr => count += arr.length)
  return count
})

const activeFilterCount = computed(() => {
  return [
    filters.status !== 'all',
    filters.modules.length > 0,
    filters.risk !== '',
    filters.action !== ''
  ].filter(Boolean).length
})

const highRiskCount = computed(() => {
  return props.allPermissions.filter(p => p.riskLevel >= 3 && currentSelectedIds.value.includes(p.id)).length
})

watch(filteredGroupedPermissions, (newVal) => {
  activeModules.value = Object.keys(newVal)
}, { immediate: true })

const hasChanges = computed(() => {
  if (!props.role) return false
  const original = [...(props.role.permissionIds || [])].sort()
  const current = [...currentSelectedIds.value].sort()
  if (original.length !== current.length) return true
  return !original.every((val, idx) => val === current[idx])
})

function discardChanges() {
  currentSelectedIds.value = [...(props.role.permissionIds || [])]
}

function saveChanges() {
  emit('save', currentSelectedIds.value)
}

function onEditorSave(ids) {
  emit('save', ids)
}

function getSelectedCount(moduleName) {
  const perms = groupedPermissions.value[moduleName] || []
  return perms.filter(p => currentSelectedIds.value.includes(p.id)).length
}

function getModuleCheckState(moduleName) {
  const perms = groupedPermissions.value[moduleName] || []
  if (perms.length === 0) return false
  return perms.every(p => currentSelectedIds.value.includes(p.id))
}

function isModuleIndeterminate(moduleName) {
  const perms = groupedPermissions.value[moduleName] || []
  if (perms.length === 0) return false
  const selectedCount = getSelectedCount(moduleName)
  return selectedCount > 0 && selectedCount < perms.length
}

function toggleModule(moduleName, val) {
  const perms = groupedPermissions.value[moduleName] || []
  if (val) {
    perms.forEach(p => {
      if (!currentSelectedIds.value.includes(p.id)) currentSelectedIds.value.push(p.id)
    })
  } else {
    currentSelectedIds.value = currentSelectedIds.value.filter(
      id => !perms.find(p => p.id === id)
    )
  }
}

function togglePermission(permId, val) {
  if (val) {
    if (!currentSelectedIds.value.includes(permId)) currentSelectedIds.value.push(permId)
  } else {
    currentSelectedIds.value = currentSelectedIds.value.filter(id => id !== permId)
  }
}

// Bulk Actions
function clearPermissionFilters() {
  filters.status = 'all'
  filters.modules = []
  filters.risk = ''
  filters.action = ''
}

function bulkEnableFiltered() {
  Object.values(filteredGroupedPermissions.value).forEach(perms => {
    perms.forEach(p => {
      if (!currentSelectedIds.value.includes(p.id)) {
        currentSelectedIds.value.push(p.id)
      }
    })
  })
}

function bulkDisableFiltered() {
  const filteredIds = new Set()
  Object.values(filteredGroupedPermissions.value).forEach(perms => {
    perms.forEach(p => filteredIds.add(p.id))
  })
  
  currentSelectedIds.value = currentSelectedIds.value.filter(id => !filteredIds.has(id))
}

// Formatting
function getRawActionStr(code) {
  const parts = code.split('.')
  return parts[parts.length - 1]
}

function formatDependencies(depJson) {
  if (!depJson) return 'None'
  try {
    const parsed = JSON.parse(depJson)
    if (Array.isArray(parsed) && parsed.length > 0) return parsed.join(', ')
    return depJson
  } catch(e) {
    return depJson
  }
}

function inferRiskLevel(code) {
  const lower = code.toLowerCase()
  if (lower.includes('delete') || lower.includes('destroy') || lower.includes('admin') || lower.includes('manage')) return 3
  if (lower.includes('create') || lower.includes('update') || lower.includes('edit') || lower.includes('import')) return 2
  return 1
}

function getRiskLevelStr(level) {
  if (level >= 4) return 'Critical'
  if (level === 3) return 'High'
  if (level === 2) return 'Medium'
  return 'Low'
}

function getRiskTagType(level) {
  if (level >= 4) return 'danger'
  if (level === 3) return 'danger'
  if (level === 2) return 'warning'
  return 'info'
}

// MATRIX VIEW LOGIC
const matrixTableData = computed(() => {
  const data = []
  const sortedModules = Object.keys(filteredGroupedPermissions.value).sort()
  
  sortedModules.forEach(moduleName => {
    const perms = filteredGroupedPermissions.value[moduleName] || []
    if (perms.length === 0) return
    
    const sortedPerms = [...perms].sort((a, b) => {
      return getRawActionStr(a.code).localeCompare(getRawActionStr(b.code))
    })
    
    sortedPerms.forEach((p, index) => {
      data.push({
        moduleName,
        perm: p,
        rowspan: index === 0 ? sortedPerms.length : 0,
        isFirst: index === 0
      })
    })
  })
  return data
})

const matrixSpanMethod = ({ row, columnIndex }) => {
  if (columnIndex === 0) {
    return row.isFirst
      ? { rowspan: row.rowspan, colspan: 1 }
      : { rowspan: 0, colspan: 0 }
  }
}

</script>

<style scoped>
.summary-card {
  border-radius: 8px;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-light);
}
.permissions-tab-root,
.permissions-content-scroll {
  min-width: 0;
  min-height: 0;
}

.permissions-tab-root {
  flex: 1 1 auto;
}

.permissions-header-workspace {
  display: grid;
  grid-template-columns: minmax(360px, 42%) minmax(0, 1fr);
  align-items: stretch;
  min-width: 0;
  border-bottom: 1px solid var(--el-border-color-light);
}

.permissions-role-column {
  min-width: 0;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.permissions-role-column :deep(.role-header-panel) {
  border-bottom: none;
}

.permissions-toolbar {
  flex: 0 0 auto;
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-width: 0;
  padding: 12px 24px 16px !important;
}

.permissions-toolbar-search-row,
.permissions-toolbar-main-row,
.permissions-toolbar-controls,
.permissions-toolbar-actions {
  display: flex;
  align-items: center;
}

.permissions-toolbar-search-row {
  justify-content: flex-end;
  gap: 8px;
  min-width: 0;
}

.permissions-toolbar-main-row {
  justify-content: space-between;
  gap: 16px;
  min-width: 0;
  align-items: flex-start;
}

.permission-search-input {
  width: min(260px, calc(100% - 82px));
  flex: 0 1 260px;
  min-width: 0;
}

.permission-search-input :deep(.el-input__wrapper) {
  height: 36px;
  padding: 0 12px;
  border-radius: 9px;
  background: var(--el-bg-color);
  box-shadow: 0 0 0 1px var(--el-border-color) inset;
  transition: box-shadow 0.2s ease, background-color 0.2s ease;
}

.permission-search-input :deep(.el-input__wrapper:hover),
.permission-search-input :deep(.el-input__wrapper.is-focus) {
  background: var(--el-bg-color-overlay);
  box-shadow: 0 0 0 1px var(--el-color-primary) inset, 0 0 0 3px color-mix(in srgb, var(--el-color-primary) 12%, transparent);
}

.permission-search-input :deep(.el-input__inner) {
  font-size: 13.5px;
}

.permission-search-input :deep(.el-input__prefix) {
  color: var(--el-text-color-secondary);
}

.permissions-toolbar-controls {
  flex-direction: column;
  align-items: flex-end;
  gap: 8px;
  width: 100%;
  min-width: 0;
}

.permissions-toolbar-actions {
  justify-content: flex-end;
  flex-wrap: wrap;
  gap: 8px;
  min-width: 0;
}

.permissions-toolbar-actions :deep(.el-button),
.permission-filter-trigger {
  min-height: 36px;
  border-radius: 9px;
}

.permission-filter-trigger :deep(.el-badge) {
  margin-left: 2px;
}

.permissions-toolbar-actions :deep(.el-radio-group) {
  height: 36px;
}

.permissions-toolbar-actions :deep(.el-radio-button__inner) {
  height: 36px;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.permissions-toolbar-bulk {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  flex-wrap: wrap;
  gap: 8px;
}

.permissions-filter-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.permission-filter-panel-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--el-border-color-light);
}

.permission-filter-panel-header strong,
.permission-filter-panel-header span {
  display: block;
}

.permission-filter-panel-header strong {
  color: var(--el-text-color-primary);
  font-size: 14px;
}

.permission-filter-panel-header span {
  margin-top: 4px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.permission-filter-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.permission-filter-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}

.permission-filter-field-wide {
  grid-column: 1 / -1;
}

.permission-filter-field > span {
  color: var(--el-text-color-secondary);
  font-size: 12px;
  font-weight: 600;
}

.permission-filter-field :deep(.el-select) {
  width: 100%;
}

.permissions-content-scroll {
  flex: 1 1 auto !important;
  overflow: hidden;
}

.permissions-content-scroll :deep(.el-scrollbar__wrap) {
  overflow-x: auto;
}

.permissions-tree-content {
  width: 100%;
  margin: 0;
}

.permissions-module-title {
  margin-left: 24px;
}

:global(.permission-filter-popover) {
  padding: 16px !important;
  border-radius: 10px !important;
}

@media (max-width: 980px) {
  .permissions-header-workspace {
    grid-template-columns: 1fr;
  }

  .permissions-toolbar-search-row {
    justify-content: flex-start;
  }

  .permission-search-input {
    width: min(260px, calc(100% - 82px));
  }

  .permissions-toolbar-main-row {
    flex-direction: column;
    align-items: stretch;
  }

  .permissions-toolbar-controls {
    align-items: flex-start;
  }

  .permissions-toolbar-actions {
    justify-content: flex-start;
  }
}

.matrix-module-cell {
  min-height: 56px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}
.summary-title {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  margin-bottom: 8px;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}
.summary-value {
  font-size: 24px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

::v-deep(.el-collapse-item__header) {
  background: var(--el-fill-color-blank);
  border-bottom: 1px solid var(--el-border-color-light);
  height: 56px;
}
::v-deep(.el-collapse-item__wrap) {
  border-bottom: none;
}
::v-deep(.el-collapse-item__content) {
  padding-bottom: 0;
}
::v-deep(.el-checkbox__label) {
  color: var(--el-text-color-primary);
}
.matrix-container {
  overflow-x: auto;
}
</style>
