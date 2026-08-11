<script setup>
import { ref, onMounted } from 'vue'
import axiosClient from '@/api/axiosClient'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'

const props = defineProps({
  projectId: { type: String, required: true }
})

const modules = ref([])
const loading = ref(false)
const showCreateModal = ref(false)
const newModule = ref({ name: '', description: '', startDate: null, targetDate: null })

onMounted(() => loadModules())

async function loadModules() {
  loading.value = true
  try {
    const res = await axiosClient.get(`/projects/${props.projectId}/modules`)
    modules.value = res.data?.data || []
  } catch (e) {
    console.error('Failed to load modules', e)
    modules.value = []
  } finally {
    loading.value = false
  }
}

async function createModule() {
  try {
    await axiosClient.post(`/projects/${props.projectId}/modules`, newModule.value)
    newModule.value = { name: '', description: '', startDate: null, targetDate: null }
    showCreateModal.value = false
    loadModules()
  } catch (e) {
    alert(e.response?.data?.message || 'Lỗi khi tạo module')
  }
}

function getStatusColor(status) {
  const map = {
    'Backlog': '#94a3b8',
    'Planned': '#3b82f6',
    'InProgress': '#f59e0b',
    'Paused': '#8b5cf6',
    'Completed': '#22c55e',
    'Cancelled': '#ef4444'
  }
  return map[status] || '#94a3b8'
}

function formatDate(d) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}
</script>

<template>
  <div class="module-list">
    <div class="module-header">
      <h3>📦 Modules</h3>
      <el-button type="primary" size="small" @click="showCreateModal = true">
        + Thêm Module
      </el-button>
    </div>

    <div v-loading="loading" class="module-grid">
      <div v-for="mod in modules" :key="mod.id" class="module-card">
        <div class="module-card-header">
          <span class="module-status-dot" :style="{ backgroundColor: getStatusColor(mod.status) }"></span>
          <h4 class="module-name">{{ mod.name }}</h4>
        </div>
        <p class="module-desc">{{ mod.description || 'Không có mô tả' }}</p>
        <div class="module-dates">
          <span>📅 {{ formatDate(mod.startDate) }} → {{ formatDate(mod.targetDate) }}</span>
        </div>
        <div class="module-stats">
          <span class="module-stat">{{ mod.issueCount || 0 }} issues</span>
          <span class="module-stat-status">{{ mod.status }}</span>
        </div>
      </div>

      <div v-if="!loading && modules.length === 0" class="module-empty">
        <p>Chưa có module nào.</p>
        <p class="module-empty-hint">Module giúp nhóm các công việc theo tính năng lớn (Epic).</p>
      </div>
    </div>

    <!-- Create Module Modal -->
    <el-dialog v-model="showCreateModal" width="480px" append-to-body class="sa-data-dialog sa-modal--form" :show-close="false">
      <template #header>
        <DataModalHeader
          icon="bi bi-box-seam"
          title="Tạo Module mới"
          description="Nhóm các công việc liên quan theo một tính năng lớn"
          @close="showCreateModal = false"
        />
      </template>
      <el-form label-position="top">
        <el-form-item label="Tên Module">
          <el-input v-model="newModule.name" placeholder="VD: Authentication System" />
        </el-form-item>
        <el-form-item label="Mô tả">
          <el-input v-model="newModule.description" type="textarea" :rows="3" placeholder="Mô tả chi tiết..." />
        </el-form-item>
        <el-form-item label="Thời gian">
          <div style="display: flex; gap: 12px; width: 100%">
            <el-date-picker v-model="newModule.startDate" type="date" placeholder="Ngày bắt đầu" style="flex: 1" />
            <el-date-picker v-model="newModule.targetDate" type="date" placeholder="Ngày mục tiêu" style="flex: 1" />
          </div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button class="cancel-btn" @click="showCreateModal = false"><i class="bi bi-x-lg"></i> Hủy</el-button>
        <el-button type="primary" @click="createModule" :disabled="!newModule.name.trim()"><i class="bi bi-plus-lg"></i> Tạo Module</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.module-list {
  padding: 4px 0;
}

.module-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.module-header h3 {
  font-size: 16px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.module-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
}

.module-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 16px;
  transition: all 0.2s;
  cursor: pointer;
}

.module-card:hover {
  border-color: var(--el-color-primary);
  box-shadow: 0 4px 16px rgba(59, 130, 246, 0.1);
  transform: translateY(-2px);
}

.module-card-header {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 8px;
}

.module-status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.module-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.module-desc {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin-bottom: 10px;
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.module-dates {
  font-size: 11px;
  color: var(--color-text-muted);
  margin-bottom: 10px;
}

.module-stats {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.module-stat {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.module-stat-status {
  font-size: 11px;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 10px;
  background: var(--color-surface-hover);
  color: var(--color-text-secondary);
}

.module-empty {
  grid-column: 1 / -1;
  text-align: center;
  padding: 40px;
  color: var(--color-text-muted);
}

.module-empty-hint {
  font-size: 12px;
  margin-top: 4px;
  color: var(--color-text-muted);
  opacity: 0.7;
}
</style>


