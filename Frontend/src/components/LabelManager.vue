<script setup>
import { onUnmounted, ref, watch } from 'vue'
import axiosClient from '@/api/axiosClient'
import { signalRService } from '@/api/signalrService'
import { ElMessage } from 'element-plus'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'

const props = defineProps({
  projectId: { type: String, required: true }
})

const labels = ref([])
const loading = ref(false)
const showCreate = ref(false)
const newLabel = ref({ name: '', colorCode: '#3b82f6', description: '' })

const upsertLabel = (label) => {
  if (!label?.id) return
  const normalized = { ...label, colorCode: label.colorCode || label.color || '#3b82f6' }
  const index = labels.value.findIndex(item => item.id === normalized.id)
  if (index >= 0) labels.value[index] = { ...labels.value[index], ...normalized }
  else labels.value.push(normalized)
}

const presetColors = [
  '#ef4444', '#f97316', '#f59e0b', '#eab308', '#84cc16', '#22c55e', '#14b8a6', '#06b6d4',
  '#3b82f6', '#6366f1', '#8b5cf6', '#a855f7', '#d946ef', '#ec4899', '#f43f5e', '#64748b'
]

const loadLabels = async () => {
  if (!props.projectId) {
    labels.value = []
    return
  }

  loading.value = true
  try {
    const res = await axiosClient.get(`/projects/${props.projectId}/labels`)
    const raw = res.data?.data || res.data || []
    labels.value = (Array.isArray(raw) ? raw : []).map(label => ({
      ...label,
      colorCode: label.colorCode || label.color || '#3b82f6'
    }))
  } catch (error) {
    console.error('Failed to load labels', error)
    labels.value = []
  } finally {
    loading.value = false
  }
}

const createLabel = async () => {
  if (!newLabel.value.name.trim()) return

  try {
    const tempId = `temp-${globalThis.crypto?.randomUUID?.() || Date.now()}`
    upsertLabel({ ...newLabel.value, id: tempId, issueCount: 0 })
    const response = await axiosClient.post(`/projects/${props.projectId}/labels`, {
      name: newLabel.value.name.trim(),
      colorCode: newLabel.value.colorCode,
      description: newLabel.value.description?.trim() || ''
    })
    labels.value = labels.value.filter(label => label.id !== tempId)
    upsertLabel(response.data?.data)
    newLabel.value = { name: '', colorCode: '#3b82f6', description: '' }
    showCreate.value = false
    ElMessage.success('Label created')
  } catch (error) {
    labels.value = labels.value.filter(label => !`${label.id}`.startsWith('temp-'))
    ElMessage.error(error.response?.data?.message || 'Failed to create label')
  }
}

const deleteLabel = async (labelId) => {
  if (!confirm('Delete this label?')) return

  let previous = null
  try {
    previous = labels.value
    labels.value = labels.value.filter(label => label.id !== labelId)
    await axiosClient.delete(`/projects/${props.projectId}/labels/${labelId}`)
    ElMessage.success('Label deleted')
  } catch (error) {
    if (previous) labels.value = previous
    ElMessage.error('Failed to delete label')
  }
}

watch(
  () => props.projectId,
  async () => {
    signalRService.startConnection(props.projectId)
    await loadLabels()
  },
  { immediate: true }
)

const labelRealtimeHandler = (event) => {
  if (event?.projectId && `${event.projectId}` !== `${props.projectId}`) return
  if (`${event?.entityType || ''}`.toLowerCase() !== 'label') return
  const action = `${event.action || ''}`.toLowerCase()
  if (action === 'deleted' || action === 'removed') {
    labels.value = labels.value.filter(label => label.id !== event.entityId)
  } else {
    upsertLabel(event.data)
  }
}
signalRService.on('EntityChanged', labelRealtimeHandler)

onUnmounted(() => signalRService.off('EntityChanged', labelRealtimeHandler))
</script>

<template>
  <div class="label-manager">
    <div class="label-header">
      <h3>Labels</h3>
      <el-button type="primary" size="small" @click="showCreate = true">Add label</el-button>
    </div>

    <div v-loading="loading" class="label-list">
      <div v-for="label in labels" :key="label.id" class="label-item">
        <div class="label-info">
          <span class="label-dot" :style="{ backgroundColor: label.colorCode }"></span>
          <span class="label-name">{{ label.name }}</span>
          <span class="label-count">{{ label.issueCount || 0 }}</span>
        </div>
        <div class="label-actions">
          <el-button size="small" text type="danger" @click="deleteLabel(label.id)">Delete</el-button>
        </div>
      </div>

      <div v-if="!loading && labels.length === 0" class="label-empty">
        <p>No labels available.</p>
      </div>
    </div>

    <el-dialog v-model="showCreate" width="400px" append-to-body class="sa-data-dialog sa-modal--form" :show-close="false">
      <template #header>
        <DataModalHeader
          icon="bi bi-tag"
          title="Create label"
          description="Create a reusable label for project work items"
          close-label="Close"
          @close="showCreate = false"
        />
      </template>
      <el-form label-position="top">
        <el-form-item label="Label name">
          <el-input v-model="newLabel.name" placeholder="Bug, Feature, Urgent..." />
        </el-form-item>
        <el-form-item label="Color">
          <div class="color-grid">
            <div
              v-for="color in presetColors"
              :key="color"
              class="color-swatch"
              :class="{ selected: newLabel.colorCode === color }"
              :style="{ backgroundColor: color }"
              @click="newLabel.colorCode = color"
            ></div>
          </div>
        </el-form-item>
        <el-form-item label="Description">
          <el-input v-model="newLabel.description" placeholder="Optional description" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button class="cancel-btn" @click="showCreate = false"><i class="bi bi-x-lg"></i> Cancel</el-button>
        <el-button type="primary" :disabled="!newLabel.name.trim()" @click="createLabel">Create</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.label-manager {
  padding: 4px 0;
}

.label-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.label-header h3 {
  font-size: 16px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.label-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.label-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 14px;
  border-radius: 8px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
}

.label-info {
  display: flex;
  align-items: center;
  gap: 10px;
}

.label-dot {
  width: 12px;
  height: 12px;
  border-radius: 3px;
  flex-shrink: 0;
}

.label-name {
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-primary);
}

.label-count {
  font-size: 11px;
  color: var(--color-text-muted);
  background: var(--color-surface-hover);
  padding: 1px 6px;
  border-radius: 8px;
}

.label-empty {
  text-align: center;
  padding: 30px;
  color: var(--color-text-muted);
  font-size: 13px;
}

.color-grid {
  display: grid;
  grid-template-columns: repeat(8, 1fr);
  gap: 6px;
}

.color-swatch {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  cursor: pointer;
  border: 2px solid transparent;
}

.color-swatch.selected {
  border-color: var(--color-text-primary);
}
</style>


