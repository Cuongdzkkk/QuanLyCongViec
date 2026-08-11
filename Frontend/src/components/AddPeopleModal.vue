<template>
  <el-dialog
    v-model="dialogVisible"
    width="480px"
    class="standard-dialog sa-data-dialog"
    :show-close="false"
    append-to-body>
    
    <template #header>
      <DataModalHeader
        icon="bi bi-person-plus"
        title="Add people"
        description="Invite people and choose their role in this workspace"
        close-label="Close"
        @close="closeDialog"
      />
    </template>

    <div class="dialog-body">
      <DataModalSection icon="bi bi-person-lines-fill" title="People">
        <DataModalField label="Names or emails" required>
          <el-input 
            v-model="emailInput" 
            placeholder="e.g., Maria, maria@company.com" 
            class="compact-input" />
        </DataModalField>
      </DataModalSection>

      <DataModalSection icon="bi bi-cloud-plus" title="Import from">
        <div class="integration-grid">
          <button class="integration-card">
            <i class="fa-brands fa-google text-google"></i>
            <span>Google</span>
          </button>
          <button class="integration-card">
            <i class="fa-brands fa-slack text-slack"></i>
            <span>Slack</span>
          </button>
          <button class="integration-card">
            <i class="fa-brands fa-microsoft text-microsoft"></i>
            <span>Microsoft</span>
          </button>
        </div>
      </DataModalSection>

      <DataModalSection icon="bi bi-person-badge" title="Access">
        <DataModalField label="Role" required>
          <el-select v-model="selectedRole" class="full-width-select">
            <el-option label="Member" value="Member" />
            <el-option label="Admin" value="Admin" />
            <el-option label="Guest" value="Guest" />
          </el-select>
        </DataModalField>
      </DataModalSection>

      <p class="helper-text-muted">
        This site is protected by reCAPTCHA and the Google 
        <a href="#" target="_blank">Privacy Policy</a> and 
        <a href="#" target="_blank">Terms of Service</a> apply.
      </p>
    </div>

    <template #footer>
      <div class="dialog-footer-standard">
        <button class="btn-ghost-sm" @click="copyLink">
          <i class="fa-solid fa-link"></i> Copy link
        </button>
        <div class="footer-actions">
          <button class="cancel-btn" @click="closeDialog"><i class="bi bi-x-lg"></i> Cancel</button>
          <button class="btn-primary-sm" @click="submitAdd" :disabled="!emailInput.trim()">Add</button>
        </div>
      </div>
    </template>
  </el-dialog>
</template>

<script setup>
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import DataModalField from '@/components/common/Foundation/DataModalField.vue'

const props = defineProps({
  visible: { type: Boolean, default: false }
})

const emit = defineEmits(['update:visible', 'added'])

const dialogVisible = ref(props.visible)
const emailInput = ref('')
const selectedRole = ref('Member')

watch(() => props.visible, (newVal) => {
  dialogVisible.value = newVal
  if (newVal) {
    emailInput.value = ''
    selectedRole.value = 'Member'
  }
})

watch(dialogVisible, (newVal) => {
  emit('update:visible', newVal)
})

const closeDialog = () => {
  dialogVisible.value = false
}

const submitAdd = () => {
  if (!emailInput.value.trim()) return
  emit('added', { emails: emailInput.value, role: selectedRole.value })
  ElMessage.success('People added successfully')
  closeDialog()
}

const copyLink = () => {
  navigator.clipboard.writeText(window.location.href)
  ElMessage.success('Link copied to clipboard')
}
</script>

<style scoped>
.dialog-header-standard {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 24px 24px 16px;
}

.dialog-title {
  margin: 0;
  font-size: 20px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.header-actions { display: flex; gap: 8px; }

.icon-btn-ghost {
  width: 32px; height: 32px;
  display: flex; align-items: center; justify-content: center;
  border-radius: 6px; border: none; background: transparent;
  color: var(--color-text-muted); cursor: pointer; transition: all 0.2s;
}
.icon-btn-ghost:hover { background: var(--color-surface-hover); color: var(--color-text-primary); }

.dialog-body { padding: 0 24px 24px; }

.form-group { margin-bottom: 20px; }

.field-label {
  display: block; font-size: 13px; font-weight: 700;
  color: var(--color-text-secondary); margin-bottom: 6px;
}

.required { color: #ef4444; }

.integration-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; }

.integration-card {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  gap: 8px; padding: 12px;
  background: var(--color-bg);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  cursor: pointer; transition: all 0.2s;
  color: var(--color-text-primary); font-size: 13px; font-weight: 600;
}
.integration-card:hover { border-color: var(--color-accent); background: var(--color-surface-hover); }
.integration-card i { font-size: 18px; }

.text-google { color: #ea4335; }
.text-slack { color: #4a154b; }
.text-microsoft { color: #00a4ef; }

.full-width-select { width: 100%; }

.helper-text-muted {
  font-size: 12px; color: var(--color-text-muted); line-height: 1.6;
}
.helper-text-muted a { color: var(--color-accent); text-decoration: none; font-weight: 600; }

.dialog-footer-standard {
  display: flex; justify-content: space-between; align-items: center;
  padding: 16px 24px 24px; border-top: 1px solid var(--color-border);
}

.footer-actions { display: flex; gap: 12px; }

.btn-primary-sm {
  background: var(--color-accent); color: #fff;
  border: none; border-radius: 6px; padding: 8px 16px;
  font-weight: 600; font-size: 13px; cursor: pointer; transition: all 0.2s;
}
.btn-primary-sm:hover { background: var(--color-accent-hover); }
.btn-primary-sm:disabled { opacity: 0.5; cursor: not-allowed; }

.btn-secondary-sm {
  background: var(--color-surface); color: var(--color-text-primary);
  border: 1px solid var(--color-border); border-radius: 6px; padding: 8px 16px;
  font-weight: 600; font-size: 13px; cursor: pointer; transition: all 0.2s;
}
.btn-secondary-sm:hover { background: var(--color-surface-hover); border-color: var(--color-border-hover); }

.btn-ghost-sm {
  background: transparent; color: var(--color-text-secondary);
  border: none; border-radius: 6px; padding: 8px 12px;
  font-weight: 600; font-size: 13px; cursor: pointer; transition: all 0.2s;
  display: flex; align-items: center; gap: 6px;
}
.btn-ghost-sm:hover { background: var(--color-surface-hover); color: var(--color-text-primary); }
</style>

<style>
.standard-dialog.el-dialog {
  background: var(--color-surface) !important;
  border-radius: 12px !important;
  box-shadow: var(--shadow-xl) !important;
  border: 1px solid var(--color-border) !important;
  overflow: hidden;
}
.standard-dialog .el-dialog__header { padding: 0; margin: 0; }
.standard-dialog .el-dialog__body { padding: 0; }
.standard-dialog .el-dialog__footer { padding: 0; }
</style>
