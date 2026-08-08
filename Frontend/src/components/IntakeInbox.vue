<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import { ElMessage } from 'element-plus'
import ProjectPageHeader from '@/components/common/ProjectPageHeader.vue'

const props = defineProps({
  projectId: { type: String, required: true }
})

const emit = defineEmits(['task-created'])
const router = useRouter()

const intakes = ref([])
const loading = ref(false)
const showCreate = ref(false)
const showDetail = ref(false)
const selectedIntake = ref(null)
const loadError = ref('')
const intakePermissions = ref({
  canCreate: false,
  canReview: false
})

const newIntake = ref({
  title: '',
  description: '',
  priority: 3,
  dueDate: '',
  source: 'FORM'
})

onMounted(() => loadIntakes())

async function loadIntakes() {
  loading.value = true
  loadError.value = ''
  intakePermissions.value = { canCreate: false, canReview: false }
  try {
    const res = await axiosClient.get(`/projects/${props.projectId}/intakes`)
    intakes.value = res.data?.data || []
    intakePermissions.value = {
      canCreate: res.data?.permissions?.canCreate === true,
      canReview: res.data?.permissions?.canReview === true
    }
  } catch (e) {
    console.error('Failed to load intakes', e)
    intakes.value = []
    loadError.value = e.response?.status === 403
      ? 'Bạn không có quyền truy cập Intake của dự án này.'
      : (e.response?.data?.message || 'Không tải được danh sách Intake.')
  } finally {
    loading.value = false
  }
}

async function createIntake() {
  if (!intakePermissions.value.canCreate || !newIntake.value.title.trim()) return
  
  const payload = {
    title: newIntake.value.title,
    description: newIntake.value.description,
    priority: newIntake.value.priority,
    desiredDueDate: newIntake.value.dueDate || null,
    source: 'FORM'
  }

  try {
    await axiosClient.post(`/projects/${props.projectId}/intakes`, payload)
    newIntake.value = { title: '', description: '', priority: 3, dueDate: '', source: 'FORM' }
    showCreate.value = false
    ElMessage.success('Gửi yêu cầu thành công!')
    loadIntakes()
  } catch (e) {
    ElMessage.error(e.response?.data?.message || 'Không gửi được yêu cầu. Vui lòng thử lại.')
  }
}

async function updateStatus(id, status) {
  if (!intakePermissions.value.canReview) {
    ElMessage.warning('Bạn không có quyền duyệt Intake.')
    return
  }

  try {
    await axiosClient.put(`/projects/${props.projectId}/intakes/${id}/review`, { status })
    ElMessage.success(status === 'Accepted' ? 'Đã duyệt yêu cầu và tạo công việc.' : 'Đã từ chối yêu cầu.')
    loadIntakes()
    if (status === 'Accepted') {
      emit('task-created')
    }
  } catch (e) {
    ElMessage.error(e.response?.data?.message || 'Không thể cập nhật trạng thái yêu cầu.')
  }
}

function getPriorityInfo(priority) {
  const map = {
    1: { label: 'Khẩn cấp', color: '#ef4444', bg: 'rgba(239, 68, 68, 0.08)' },
    2: { label: 'Cao', color: '#f97316', bg: 'rgba(249, 115, 22, 0.08)' },
    3: { label: 'Trung bình', color: '#3b82f6', bg: 'rgba(59, 130, 246, 0.08)' },
    4: { label: 'Thấp', color: '#64748b', bg: 'rgba(100, 116, 139, 0.08)' }
  }
  return map[priority] || map[3]
}

function getStatusInfo(status) {
  const map = {
    'Pending': { color: '#f59e0b', label: 'Chờ duyệt', icon: 'fa-regular fa-clock', bg: 'rgba(245, 158, 11, 0.08)' },
    'Accepted': { color: '#10b981', label: 'Đã duyệt & Tạo việc', icon: 'fa-regular fa-circle-check', bg: 'rgba(16, 185, 129, 0.08)' },
    'Declined': { color: '#ef4444', label: 'Từ chối', icon: 'fa-regular fa-circle-xmark', bg: 'rgba(239, 68, 68, 0.08)' }
  }
  return map[status] || { color: '#64748b', label: status, icon: 'fa-regular fa-question', bg: '#f1f5f9' }
}

function formatDate(d) {
  if (!d) return '—'
  return new Date(d).toLocaleString('vi-VN', {
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit'
  })
}

function formatDateOnly(d) {
  if (!d) return 'Không có'
  return new Date(d).toLocaleDateString('vi-VN', {
    day: '2-digit', month: '2-digit', year: 'numeric'
  })
}

function viewDetail(item) {
  selectedIntake.value = item
  showDetail.value = true
}

function navigateToTask(taskId) {
  if (!taskId) return
  router.push({
    name: 'SpaceSummary',
    params: { id: props.projectId },
    query: { task: taskId }
  })
}
</script>

<template>
  <div class="intake-portal">
    <!-- Header -->
    <ProjectPageHeader
      icon="fa-solid fa-inbox"
      title="Hộp thư yêu cầu"
      description="Duyệt các yêu cầu công việc được gửi từ nhân viên và chuyển thành công việc chính thức"
    >
      <template #actions>
        <button v-if="intakePermissions.canCreate" class="nexus-btn-primary" @click="showCreate = true">
          <i class="fa-solid fa-plus mr-1"></i> Gửi yêu cầu mới
        </button>
      </template>
    </ProjectPageHeader>

    <!-- Inbox List -->
    <div v-loading="loading" class="intake-content-area">
      <div v-if="!loading && loadError" class="intake-empty-state" role="alert">
        <div class="empty-icon-wrapper">
          <i class="fa-solid fa-shield-halved text-4xl text-[var(--color-text-muted)]"></i>
        </div>
        <h4 class="font-bold text-sm text-[var(--color-text-primary)] mt-4">Không thể mở Intake</h4>
        <p class="text-xs text-[var(--color-text-muted)] mt-1 max-w-sm">{{ loadError }}</p>
      </div>
      
      <!-- Empty State -->
      <div v-else-if="!loading && intakes.length === 0" class="intake-empty-state">
        <div class="empty-icon-wrapper">
          <i class="fa-regular fa-envelope-open text-4xl text-[var(--color-text-muted)]"></i>
        </div>
        <h4 class="font-bold text-sm text-[var(--color-text-primary)] mt-4">Chưa có yêu cầu nào</h4>
        <p class="text-xs text-[var(--color-text-muted)] mt-1 mb-4 max-w-sm">Hãy tạo form yêu cầu để nhân viên gửi công việc vào SprintA.</p>
        <button v-if="intakePermissions.canCreate" class="nexus-btn-primary" @click="showCreate = true">
          <i class="fa-solid fa-paper-plane mr-1"></i> Gửi yêu cầu mới
        </button>
      </div>

      <!-- Table Listing -->
      <div v-else class="table-container">
        <table class="intake-table">
          <thead>
            <tr>
              <th>Tiêu đề yêu cầu</th>
              <th>Người gửi</th>
              <th>Mức độ ưu tiên</th>
              <th>Hạn mong muốn</th>
              <th>Trạng thái</th>
              <th>Ngày tạo</th>
              <th class="actions-header">Hành động</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in intakes" :key="item.id" class="table-row">
              <td class="title-cell font-bold" @click="viewDetail(item)">
                {{ item.title }}
                <span class="source-tag text-[10px] ml-2">{{ item.source }}</span>
              </td>
              <td>{{ item.submittedByName || 'Khách vãng lai' }}</td>
              <td>
                <span 
                  class="priority-badge"
                  :style="{ 
                    color: getPriorityInfo(item.priority).color, 
                    backgroundColor: getPriorityInfo(item.priority).bg 
                  }"
                >
                  {{ getPriorityInfo(item.priority).label }}
                </span>
              </td>
              <td>{{ formatDateOnly(item.desiredDueDate) }}</td>
              <td>
                <span class="status-badge" :style="{ color: getStatusInfo(item.status).color, backgroundColor: getStatusInfo(item.status).bg }">
                  <i :class="getStatusInfo(item.status).icon" class="mr-1"></i>
                  {{ getStatusInfo(item.status).label }}
                </span>
              </td>
              <td class="text-xs text-[var(--color-text-muted)]">{{ formatDate(item.createdAt) }}</td>
              <td class="actions-cell">
                <el-button size="small" link type="primary" @click="viewDetail(item)">Chi tiết</el-button>
                
                <template v-if="item.status === 'Pending' && intakePermissions.canReview">
                  <el-button size="small" type="success" plain @click="updateStatus(item.id, 'Accepted')">Duyệt</el-button>
                  <el-button size="small" type="danger" plain @click="updateStatus(item.id, 'Declined')">Từ chối</el-button>
                </template>

                <template v-if="item.status === 'Accepted' && item.createdIssueId">
                  <el-button size="small" type="primary" plain @click="navigateToTask(item.createdIssueId)">
                    <i class="fa-solid fa-arrow-up-right-from-square mr-1"></i> Xem việc
                  </el-button>
                </template>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

    </div>

    <!-- Dialog: Gửi yêu cầu mới -->
    <el-dialog v-model="showCreate" title="Gửi yêu cầu công việc mới" width="500px" destroy-on-close>
      <el-form label-position="top">
        <el-form-item label="Tiêu đề yêu cầu" required>
          <el-input v-model="newIntake.title" placeholder="Nhập tiêu đề ngắn gọn..." />
        </el-form-item>
        
        <el-form-item label="Mô tả chi tiết">
          <el-input v-model="newIntake.description" type="textarea" :rows="4" placeholder="Nhập chi tiết yêu cầu, lỗi gặp phải hoặc mục tiêu công việc..." />
        </el-form-item>

        <div class="form-grid">
          <el-form-item label="Mức độ ưu tiên">
            <el-select v-model="newIntake.priority" class="w-full" style="width: 100%;">
              <el-option :value="1" label="🚨 Khẩn cấp" />
              <el-option :value="2" label="🟠 Cao" />
              <el-option :value="3" label="🔵 Trung bình" />
              <el-option :value="4" label="⚪ Thấp" />
            </el-select>
          </el-form-item>

          <el-form-item label="Hạn mong muốn">
            <el-date-picker 
              v-model="newIntake.dueDate" 
              type="date" 
              placeholder="Chọn ngày hoàn thành mong muốn" 
              format="YYYY-MM-DD"
              value-format="YYYY-MM-DD"
              class="w-full" 
              style="width: 100%;"
            />
          </el-form-item>
        </div>
      </el-form>
      <template #footer>
        <div class="dialog-footer">
          <el-button @click="showCreate = false">Hủy bỏ</el-button>
          <el-button type="primary" @click="createIntake" :disabled="!newIntake.title.trim()">Gửi yêu cầu</el-button>
        </div>
      </template>
    </el-dialog>

    <!-- Dialog: Chi tiết yêu cầu -->
    <el-dialog v-model="showDetail" title="Chi tiết yêu cầu công việc" width="540px">
      <div v-if="selectedIntake" class="intake-detail-modal">
        <div class="detail-row">
          <span class="detail-label">Tiêu đề:</span>
          <span class="detail-val font-bold">{{ selectedIntake.title }}</span>
        </div>
        <div class="detail-row">
          <span class="detail-label">Trạng thái:</span>
          <span class="status-badge" :style="{ color: getStatusInfo(selectedIntake.status).color, backgroundColor: getStatusInfo(selectedIntake.status).bg }">
            <i :class="getStatusInfo(selectedIntake.status).icon" class="mr-1"></i>
            {{ getStatusInfo(selectedIntake.status).label }}
          </span>
        </div>
        <div class="detail-row">
          <span class="detail-label">Mức độ ưu tiên:</span>
          <span 
            class="priority-badge" 
            :style="{ 
              color: getPriorityInfo(selectedIntake.priority).color, 
              backgroundColor: getPriorityInfo(selectedIntake.priority).bg 
            }"
          >
            {{ getPriorityInfo(selectedIntake.priority).label }}
          </span>
        </div>
        <div class="detail-row">
          <span class="detail-label">Hạn mong muốn:</span>
          <span class="detail-val">{{ formatDateOnly(selectedIntake.desiredDueDate) }}</span>
        </div>
        <div class="detail-row">
          <span class="detail-label">Người gửi:</span>
          <span class="detail-val">{{ selectedIntake.submittedByName || 'Khách vãng lai' }}</span>
        </div>
        <div class="detail-row">
          <span class="detail-label">Nguồn:</span>
          <span class="detail-val">{{ selectedIntake.source }}</span>
        </div>
        <div class="detail-row">
          <span class="detail-label">Ngày gửi:</span>
          <span class="detail-val">{{ formatDate(selectedIntake.createdAt) }}</span>
        </div>
        
        <div class="detail-row block-row">
          <span class="detail-label">Mô tả chi tiết:</span>
          <div class="detail-desc-box">
            {{ selectedIntake.description || 'Không có mô tả chi tiết.' }}
          </div>
        </div>
        
        <div v-if="selectedIntake.createdIssueId" class="detail-row link-row">
          <span class="detail-label">Công việc đã tạo:</span>
          <span class="task-link" @click="navigateToTask(selectedIntake.createdIssueId)">
            <i class="fa-solid fa-arrow-up-right-from-square mr-1"></i> Mở công việc trên Space Board
          </span>
        </div>
      </div>
      <template #footer>
        <div class="dialog-footer">
          <el-button @click="showDetail = false">Đóng lại</el-button>
          <template v-if="selectedIntake && selectedIntake.status === 'Pending' && intakePermissions.canReview">
            <el-button type="danger" plain @click="updateStatus(selectedIntake.id, 'Declined'); showDetail = false">Từ chối</el-button>
            <el-button type="success" @click="updateStatus(selectedIntake.id, 'Accepted'); showDetail = false">Duyệt & Tạo việc</el-button>
          </template>
        </div>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.intake-portal {
  width: 100%;
  min-height: 100%;
  display: flex;
  flex-direction: column;
  gap: 14px;
  font-family: 'Inter', system-ui, sans-serif;
  color: var(--color-text-primary);
}

.intake-content-area {
  width: 100%;
}

/* Empty State */
.intake-empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 80px 24px;
  background: var(--color-surface);
  border: 1px dashed var(--color-border);
  border-radius: 16px;
  box-shadow: 0 10px 30px rgba(15, 23, 42, 0.04);
}

.empty-icon-wrapper {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  background: rgba(148, 163, 184, 0.08);
  display: flex;
  align-items: center;
  justify-content: center;
}

/* Table Listing */
.table-container {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  overflow-x: auto;
  overflow-y: hidden;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.04);
}

.intake-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
}

.intake-table th,
.intake-table td {
  padding: 14px 18px;
  font-size: 13px;
  border-bottom: 1px solid var(--color-border);
}

.intake-table th {
  background: rgba(148, 163, 184, 0.04);
  font-weight: 700;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  font-size: 11px;
  letter-spacing: 0.05em;
}

.table-row {
  transition: background-color 0.2s;
}

.table-row:hover {
  background-color: var(--color-surface-hover);
}

.title-cell {
  cursor: pointer;
  max-width: 280px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.title-cell:hover {
  color: var(--color-accent);
}

.source-tag {
  background: rgba(14, 165, 233, 0.08);
  color: var(--color-accent);
  padding: 2px 6px;
  border-radius: 4px;
  font-weight: 700;
}

.priority-badge,
.status-badge {
  display: inline-flex;
  align-items: center;
  padding: 4px 8px;
  border-radius: 6px;
  font-size: 11px;
  font-weight: 700;
}

.actions-header {
  text-align: right;
}

.actions-cell {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  align-items: center;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
}

/* Detail Modal */
.intake-detail-modal {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.detail-row {
  display: flex;
  align-items: center;
  padding: 6px 0;
  border-bottom: 1px solid rgba(148, 163, 184, 0.08);
}

.block-row {
  flex-direction: column;
  align-items: flex-start;
  gap: 8px;
  border-bottom: none;
}

.detail-label {
  width: 140px;
  font-size: 12.5px;
  font-weight: 700;
  color: var(--color-text-secondary);
  flex-shrink: 0;
}

.detail-val {
  font-size: 13px;
  color: var(--color-text-primary);
}

.detail-desc-box {
  width: 100%;
  padding: 12px;
  background: rgba(0, 0, 0, 0.015);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  font-size: 13px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-word;
}

.link-row {
  background: rgba(16, 185, 129, 0.04);
  border: 1px solid rgba(16, 185, 129, 0.12);
  border-radius: 8px;
  padding: 10px 14px;
  margin-top: 8px;
}

.task-link {
  font-size: 13px;
  font-weight: 700;
  color: #10b981;
  cursor: pointer;
}

.task-link:hover {
  text-decoration: underline;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

@media (max-width: 768px) {
  .intake-portal {
    gap: 12px;
  }
}
</style>
