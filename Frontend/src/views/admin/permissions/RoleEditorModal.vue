<template>
  <el-dialog
    :model-value="visible"
    width="500px"
    @update:model-value="$emit('update:visible', $event)"
    :before-close="handleClose"
    append-to-body
    class="sa-data-dialog sa-modal--form"
    :show-close="false"
  >
    <template #header>
      <DataModalHeader
        icon="bi bi-person-gear"
        :title="editingRole ? 'Edit role' : 'Create role'"
        description="Configure the role identity before assigning permissions"
        close-label="Close"
        @close="handleClose"
      />
    </template>
    <div class="role-form">
      <div class="form-group">
        <label>Role name <span class="required">*</span></label>
        <el-input v-model="form.name" placeholder="E.g. Release manager" :disabled="editingRole?.isProtected" />
      </div>
      <div class="form-group">
        <label>Description</label>
        <el-input v-model="form.description" type="textarea" :rows="3" placeholder="What this role is responsible for" />
      </div>
      <div v-if="editingRole?.isProtected" class="protected-notice">
        <i class="fa-solid fa-lock"></i>
        <span>This is a protected role. Some fields cannot be modified.</span>
      </div>
    </div>
    <template #footer>
      <div class="dialog-footer">
        <el-button class="cancel-btn" @click="handleClose"><i class="bi bi-x-lg"></i> Cancel</el-button>
        <el-button type="primary" @click="handleSave" :loading="saving">
          {{ editingRole ? 'Save changes' : 'Create role' }}
        </el-button>
      </div>
    </template>
  </el-dialog>
</template>

<script setup>
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'

const props = defineProps({
  visible: Boolean,
  editingRole: Object
})

const emit = defineEmits(['update:visible', 'save'])

const form = ref({
  name: '',
  description: ''
})

const saving = ref(false)

watch(() => props.visible, (val) => {
  if (val) {
    if (props.editingRole) {
      form.value.name = props.editingRole.name || ''
      form.value.description = props.editingRole.description || ''
    } else {
      form.value.name = ''
      form.value.description = ''
    }
  }
})

const handleClose = () => {
  emit('update:visible', false)
}

const handleSave = async () => {
  if (!form.value.name.trim()) {
    ElMessage.warning('Role name is required')
    return
  }
  
  saving.value = true
  try {
    await emit('save', {
      ...form.value
    })
    handleClose()
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.role-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.form-group label {
  font-weight: 500;
  font-size: 13px;
  color: var(--color-text-primary, #e4e4e7);
}
.required {
  color: var(--color-danger, #ef4444);
}
.protected-notice {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px;
  background-color: var(--color-surface-hover, #27272a);
  border-radius: 6px;
  color: var(--color-warning, #eab308);
  font-size: 13px;
}
</style>
