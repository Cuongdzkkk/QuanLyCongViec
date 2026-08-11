<template>
  <div class="role-header-layout">
    <div class="role-header-main">
      <div class="role-header-title-row">
        <el-text tag="h1" style="font-size: 24px; font-weight: 600; color: var(--el-text-color-primary); margin: 0; line-height: 1.2;">
          {{ role.name }}
        </el-text>
        <el-tag v-if="role.isProtected" type="warning" effect="dark" size="small" style="font-weight: 600;">System Protected</el-tag>
        <el-tag v-else type="info" effect="plain" size="small" style="font-weight: 600;">Custom Role</el-tag>
      </div>

      <div class="role-header-description-row">
        <el-text class="role-header-description" type="info">
          {{ role.description || 'No description provided.' }}
        </el-text>

        <div class="role-header-actions">
          <el-button @click="$emit('duplicate')">
            <el-icon><CopyDocument /></el-icon> Duplicate
          </el-button>
          <el-button disabled title="Preview not supported by current API">Preview</el-button>
        </div>
      </div>

      <div class="role-header-stats">
        <el-text type="info" size="small">
          <el-icon style="vertical-align: middle; margin-right: 4px;"><User /></el-icon> 
          <span style="vertical-align: middle;">{{ userCount }} Assigned Users</span>
        </el-text>
        <el-text type="info" size="small">
          <el-icon style="vertical-align: middle; margin-right: 4px;"><Key /></el-icon> 
          <span style="vertical-align: middle;">{{ role.permissionIds?.length || 0 }} Active Permissions</span>
        </el-text>
      </div>
    </div>
  </div>
</template>

<script setup>
import { User, Key, CopyDocument } from '@element-plus/icons-vue'

defineProps({
  role: { type: Object, required: true },
  userCount: { type: Number, default: 0 }
})
defineEmits(['duplicate'])
</script>

<style scoped>
.role-header-layout {
  width: 100%;
}

.role-header-main {
  min-width: 0;
}

.role-header-title-row {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
}

.role-header-description-row {
  display: flex;
  align-items: center;
  gap: 16px;
  min-width: 0;
}

.role-header-description {
  min-width: 0;
  flex: 0 1 auto;
  font-size: 14px;
  white-space: nowrap;
}

.role-header-actions {
  display: flex;
  flex-direction: row;
  align-items: center;
  gap: 8px;
  flex: 0 0 auto;
}

.role-header-stats {
  display: flex;
  align-items: center;
  flex-wrap: nowrap;
  gap: 24px;
  margin-top: 12px;
}

.role-header-stats .el-text {
  white-space: nowrap;
}

@media (max-width: 760px) {
  .role-header-description-row {
    align-items: flex-start;
    flex-direction: column;
    gap: 10px;
  }

  .role-header-actions {
    width: 100%;
  }

  .role-header-stats {
    gap: 12px;
  }
}
</style>
