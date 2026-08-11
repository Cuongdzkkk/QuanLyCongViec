<template>
  <el-dialog
    :model-value="visible"
    @update:model-value="$emit('update:visible', $event)"
    title="Permission Editor Workspace"
    width="95vw"
    top="4vh"
    :close-on-click-modal="false"
    :close-on-press-escape="false"
    :show-close="false"
    class="permission-editor-dialog sa-data-dialog sa-modal--workspace"
    destroy-on-close
  >
    <template #header>
      <DataModalHeader icon="bi bi-shield-lock" title="Permission editor" :description="`Configure permissions for ${role?.name || 'this role'}`" close-label="Close" @close="onCancel" />
    </template>

    <div class="permission-editor-body" style="display: flex; flex-direction: column; height: 75vh; overflow: hidden; background: var(--el-bg-color);">
      
      <!-- TOOLBAR -->
      <DataModalSection icon="bi bi-funnel" title="Filter permissions">
      <div style="padding: 16px; border-bottom: 1px solid var(--el-border-color-light); display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 16px; background: var(--el-fill-color-light);">
        <el-space :size="16" wrap>
          <el-input
            v-model="filters.search"
            placeholder="Search by function or module..."
            prefix-icon="Search"
            clearable
            style="width: 260px;"
          />
          <el-select v-model="filters.status" placeholder="Status" style="width: 140px;">
            <el-option label="All Status" value="all" />
            <el-option label="Enabled Only" value="enabled" />
            <el-option label="Disabled Only" value="disabled" />
          </el-select>
          <el-select v-model="filters.modules" placeholder="Modules" clearable multiple collapse-tags collapse-tags-tooltip style="width: 200px;">
            <el-option v-for="m in availableModules" :key="m" :label="getPermissionModuleLabel(m)" :value="m" />
          </el-select>
          <el-select v-model="filters.risk" placeholder="Risk Level" clearable style="width: 140px;">
            <el-option label="Critical Risk" :value="4" />
            <el-option label="High Risk" :value="3" />
            <el-option label="Medium Risk" :value="2" />
            <el-option label="Low Risk" :value="1" />
          </el-select>
        </el-space>

        <el-space :size="16">
          <el-radio-group v-model="viewMode">
            <el-radio-button label="tree">
              <el-icon style="vertical-align: middle; margin-right: 4px;"><DataBoard /></el-icon> Tree View
            </el-radio-button>
            <el-radio-button label="matrix">
              <el-icon style="vertical-align: middle; margin-right: 4px;"><Grid /></el-icon> Matrix DataGrid
            </el-radio-button>
          </el-radio-group>
          <el-tag type="info" effect="plain">{{ availableModules.length }} modules - {{ allPermissions.length }} functions</el-tag>
          <el-button size="small" @click="bulkEnableFiltered" type="primary" plain :disabled="role?.isProtected">Enable matching</el-button>
          <el-button size="small" @click="bulkDisableFiltered" type="danger" plain :disabled="role?.isProtected">Disable matching</el-button>
        </el-space>
      </div>
      </DataModalSection>

      <!-- MAIN CONTENT -->
      <DataModalSection icon="bi bi-grid-3x3-gap" title="Available functions">
      <el-scrollbar style="flex: 1;" class="workspace-scrollbar permission-editor-scroll">
        <div style="padding: 24px;">
          
          <template v-if="hasResults">
            <!-- TREE VIEW -->
            <div v-if="viewMode === 'tree'" style="max-width: 1200px; margin: 0 auto;">
              <el-collapse v-model="activeModules">
                <el-collapse-item 
                  v-for="(perms, moduleName) in filteredGroupedPermissions" 
                  :key="moduleName" 
                  :name="moduleName"
                  class="workspace-module-panel"
                >
                  <template #title>
                    <div style="display: flex; align-items: center; justify-content: space-between; width: 100%; padding-right: 16px;">
                      <div style="display: flex; align-items: center; gap: 12px;">
                        <el-checkbox 
                          v-if="perms.length > 0"
                          :model-value="getModuleCheckState(moduleName)"
                          :indeterminate="isModuleIndeterminate(moduleName)"
                          :disabled="role?.isProtected"
                          @change="val => toggleModule(moduleName, val)"
                          @click.stop
                        />
                        <span style="font-weight: 600; font-size: 16px;">{{ getPermissionModuleLabel(moduleName) }}</span>
                      </div>
                      <el-text type="info">{{ perms.length }} functions - {{ getSelectedCount(moduleName) }} selected</el-text>
                    </div>
                  </template>
                  
                  <div style="padding: 16px 24px;">
                    <div class="permission-grid">
                      <div v-for="perm in perms" :key="perm.id" class="permission-item">
                        <el-tooltip placement="top" effect="dark">
                          <template #content>
                            <div style="max-width: 250px;">
                              <strong>{{ getPermissionActionLabel(perm) }}</strong><br>
                              <span style="color:#aaa; font-size:12px;">{{ perm.code }}</span><br>
                              <div style="margin-top:4px;">{{ getPermissionDescription(perm) }}</div>
                            </div>
                          </template>
                          <el-checkbox
                            :model-value="currentSelectedIds.includes(perm.id)"
                            :disabled="role?.isProtected"
                            @change="val => togglePermission(perm.id, val)"
                          >
                            <div style="display: flex; flex-direction: column;">
                              <span>{{ getPermissionActionLabel(perm) }}</span>
                              <span style="font-size: 11px; color: var(--el-text-color-secondary);">{{ getPermissionDescription(perm) }}</span>
                            </div>
                          </el-checkbox>
                        </el-tooltip>
                        <el-tag v-if="perm.riskLevel >= 3" size="small" type="danger" style="margin-left: 8px;">High Risk</el-tag>
                      </div>
                    </div>
                  </div>
                </el-collapse-item>
              </el-collapse>
            </div>

            <!-- MATRIX VIEW -->
            <div v-else-if="viewMode === 'matrix'">
              <el-table 
                :data="matrixTableData" 
                :span-method="matrixSpanMethod"
                style="width: 100%;"
                border
                height="calc(75vh - 120px)"
              >
                <el-table-column label="Module" width="220" fixed align="center">
                  <template #default="{ row }">
                    <div class="matrix-module-cell">
                      <strong>{{ getPermissionModuleLabel(row.moduleName) }}</strong>
                      <el-checkbox
                        :model-value="getModuleCheckState(row.moduleName)"
                        :indeterminate="isModuleIndeterminate(row.moduleName)"
                        :disabled="role?.isProtected"
                        @change="val => toggleModule(row.moduleName, val)"
                      />
                    </div>
                  </template>
                </el-table-column>
                
                <el-table-column label="Function" min-width="250">
                  <template #default="{ row }">
                    <div style="display: flex; justify-content: space-between;">
                      <el-text>
                        <strong>{{ getPermissionActionLabel(row.perm) }}</strong>
                        <br>
                        <span style="font-size: 11px; color: #888;">{{ getPermissionDescription(row.perm) }}</span>
                      </el-text>
                      <el-tag v-if="row.perm.riskLevel >= 3" type="danger" size="small">Risk</el-tag>
                    </div>
                  </template>
                </el-table-column>
                
                <el-table-column label="Status" width="100" align="center">
                  <template #default="{ row }">
                    <el-checkbox
                      :model-value="currentSelectedIds.includes(row.perm.id)"
                      :disabled="role?.isProtected"
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
      </DataModalSection>
    </div>
    <template #footer>
      <div class="dialog-footer">
        <el-button class="cancel-btn" @click="onCancel" :disabled="saving"><i class="bi bi-x-lg"></i>Cancel</el-button>
        <el-button type="primary" @click="onSave" :loading="saving" :disabled="role?.isProtected"><i class="bi bi-check-lg"></i>Save changes</el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup>
import { ref, computed, watch, reactive } from 'vue'
import { Search, Grid, DataBoard, Monitor } from '@element-plus/icons-vue'
import { ElMessageBox } from 'element-plus'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import {
  getPermissionActionLabel,
  getPermissionDescription,
  getPermissionModuleLabel
} from '@/utils/permissionPresentation'

const props = defineProps({
  visible: { type: Boolean, default: false },
  role: { type: Object, default: () => ({}) },
  allPermissions: { type: Array, default: () => [] },
  saving: { type: Boolean, default: false }
})

const emit = defineEmits(['update:visible', 'save'])

const viewMode = ref('tree')
const activeModules = ref([])
const currentSelectedIds = ref([])

const filters = reactive({
  search: '',
  status: 'all',
  modules: [],
  risk: '',
  action: ''
})

watch(() => props.visible, (newVal) => {
  if (newVal && props.role) {
    currentSelectedIds.value = [...(props.role.permissionIds || [])]
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

const filteredGroupedPermissions = computed(() => {
  const q = filters.search.toLowerCase()
  const filtered = {}
  
  for (const [mod, perms] of Object.entries(groupedPermissions.value)) {
    if (filters.modules.length > 0 && !filters.modules.includes(mod)) continue

    let matched = perms
    if (filters.status === 'enabled') matched = matched.filter(p => currentSelectedIds.value.includes(p.id))
    else if (filters.status === 'disabled') matched = matched.filter(p => !currentSelectedIds.value.includes(p.id))
    if (filters.risk) matched = matched.filter(p => p.riskLevel === filters.risk)
    if (q) matched = matched.filter(p =>
      p.code.toLowerCase().includes(q) ||
      getPermissionModuleLabel(mod).toLowerCase().includes(q) ||
      getPermissionActionLabel(p).toLowerCase().includes(q) ||
      getPermissionDescription(p).toLowerCase().includes(q)
    )

    if (matched.length > 0) filtered[mod] = matched
  }
  return filtered
})

const hasResults = computed(() => Object.keys(filteredGroupedPermissions.value).length > 0)

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

function onCancel() {
  if (hasChanges.value) {
    ElMessageBox.confirm('You have unsaved changes. Discard?', 'Warning', { type: 'warning' })
      .then(() => emit('update:visible', false))
      .catch(() => {})
  } else {
    emit('update:visible', false)
  }
}

function onSave() {
  emit('save', currentSelectedIds.value)
}

function togglePermission(id, val) {
  if (val) {
    if (!currentSelectedIds.value.includes(id)) currentSelectedIds.value.push(id)
  } else {
    currentSelectedIds.value = currentSelectedIds.value.filter(x => x !== id)
  }
}

function getModuleCheckState(moduleName) {
  const perms = groupedPermissions.value[moduleName] || []
  if (perms.length === 0) return false
  return perms.every(p => currentSelectedIds.value.includes(p.id))
}

function isModuleIndeterminate(moduleName) {
  const perms = groupedPermissions.value[moduleName] || []
  if (perms.length === 0) return false
  const count = getSelectedCount(moduleName)
  return count > 0 && count < perms.length
}

function getSelectedCount(moduleName) {
  const perms = groupedPermissions.value[moduleName] || []
  return perms.filter(p => currentSelectedIds.value.includes(p.id)).length
}

function toggleModule(moduleName, val) {
  const perms = groupedPermissions.value[moduleName] || []
  if (val) {
    perms.forEach(p => { if (!currentSelectedIds.value.includes(p.id)) currentSelectedIds.value.push(p.id) })
  } else {
    currentSelectedIds.value = currentSelectedIds.value.filter(id => !perms.find(p => p.id === id))
  }
}

function bulkEnableFiltered() {
  Object.values(filteredGroupedPermissions.value).forEach(perms => {
    perms.forEach(p => { if (!currentSelectedIds.value.includes(p.id)) currentSelectedIds.value.push(p.id) })
  })
}

function bulkDisableFiltered() {
  const ids = new Set()
  Object.values(filteredGroupedPermissions.value).forEach(perms => perms.forEach(p => ids.add(p.id)))
  currentSelectedIds.value = currentSelectedIds.value.filter(id => !ids.has(id))
}

function inferRiskLevel(code) {
  const lower = code.toLowerCase()
  if (lower.includes('delete') || lower.includes('admin')) return 3
  if (lower.includes('create') || lower.includes('update')) return 2
  return 1
}

// MATRIX
const matrixTableData = computed(() => {
  const data = []
  Object.keys(filteredGroupedPermissions.value).sort().forEach(moduleName => {
    const perms = filteredGroupedPermissions.value[moduleName] || []
    if (perms.length === 0) return
    const sorted = [...perms].sort((a, b) => a.code.localeCompare(b.code))
    sorted.forEach((p, index) => {
      data.push({
        moduleName,
        perm: p,
        rowspan: index === 0 ? sorted.length : 0,
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
::v-deep(.permission-editor-dialog .el-dialog__header) {
  margin: 0;
  padding: 20px 24px;
  border-bottom: 1px solid var(--el-border-color-light);
  background: var(--el-bg-color);
}
::v-deep(.permission-editor-dialog .el-dialog__body) {
  padding: 0;
}
.permission-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
}
.permission-item {
  display: flex;
  align-items: flex-start;
}
.permission-editor-body {
  min-width: 0;
  min-height: 0;
}
.permission-editor-body :deep(.sa-modal-section) {
  min-width: 0;
  min-height: 0;
  display: flex;
  flex-direction: column;
}
.permission-editor-body :deep(.sa-modal-section:first-child) {
  flex: 0 0 auto;
}
.permission-editor-body :deep(.sa-modal-section:last-child) {
  flex: 1 1 auto;
  margin-top: 0 !important;
}
.permission-editor-body :deep(.sa-modal-section:last-child .sa-modal-section__content) {
  flex: 1 1 auto;
  min-height: 0;
}
.permission-editor-scroll {
  min-height: 0;
}
.matrix-module-cell {
  min-height: 56px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}
.workspace-module-panel {
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  margin-bottom: 16px;
  overflow: hidden;
}
::v-deep(.workspace-module-panel .el-collapse-item__header) {
  background: var(--el-fill-color-blank);
  height: 56px;
  padding-left: 16px;
}
</style>
