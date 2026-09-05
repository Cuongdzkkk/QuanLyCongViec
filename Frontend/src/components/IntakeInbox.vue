<script setup>
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import { ElMessage } from 'element-plus'
import ProjectPageHeader from '@/components/common/ProjectPageHeader.vue'
import ProjectPageToolbar from '@/components/common/ProjectPageToolbar.vue'
import ToolbarSortMenu from '@/components/common/ToolbarSortMenu.vue'
import FilterBar from '@/components/FilterBar.vue'
import { signalRService } from '@/api/signalrService'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import DataModalField from '@/components/common/Foundation/DataModalField.vue'
import WorkItemsListTable from '@/components/common/WorkItemsListTable.vue'
import ProjectEmptyState from '@/components/common/ProjectEmptyState.vue'
import { buildSpacePath } from '@/utils/spaceRoute'
import { useI18nStore } from '@/store/useI18nStore'

const i18nStore = useI18nStore()
const t = (key) => i18nStore.t(key)

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
const intakeSearch = ref('')
const activeFilters = ref([])

const intakeFilterFields = computed(() => [
  { key: 'status', label: 'Trạng thái', icon: 'fa-solid fa-circle-dot', values: ['Pending', 'Accepted', 'Declined'] }
])

const intakeOperators = {
  status: ['is', 'is not']
}

const customIntakeValueMeta = (fieldKey, value) => {
  if (fieldKey === 'status') {
    const info = getStatusInfo(value)
    if (info) return { icon: info.icon, color: info.color }
  }
  return null
}

const showFilterDropdown = ref(false)
const toggleFilterDropdown = () => {
  showFilterDropdown.value = !showFilterDropdown.value
}
const handleOutsideClick = (e) => {
  if (!e.target.closest('.js-toolbar-popup-scope')) {
    showFilterDropdown.value = false
  }
}

const intakeSortDirection = ref('desc')
const intakeSortBy = ref('createdAt')
const intakeSortOptions = [
  { value: 'createdAt', label: 'Ngày gửi', icon: 'fa-regular fa-calendar-plus' },
  { value: 'updatedAt', label: 'Cập nhật gần nhất', icon: 'fa-regular fa-clock' },
  { value: 'title', label: 'Tiêu đề', icon: 'fa-solid fa-font' },
  { value: 'status', label: 'Trạng thái', icon: 'fa-solid fa-circle-dot' }
]

const intakeTableColumns = [
  { key: 'title', label: 'Intake', icon: 'fa-solid fa-inbox', width: '28%', minWidth: '300px', sticky: true },
  { key: 'submittedBy', label: 'Người gửi', icon: 'fa-regular fa-user', width: '16%', minWidth: '170px' },
  { key: 'priority', label: 'Ưu tiên', icon: 'fa-solid fa-signal', width: '12%', minWidth: '130px' },
  { key: 'dueDate', label: 'Hạn mong muốn', icon: 'fa-regular fa-calendar', width: '14%', minWidth: '145px' },
  { key: 'status', label: 'Trạng thái', icon: 'fa-regular fa-circle-dot', width: '16%', minWidth: '170px' },
  { key: 'createdAt', label: 'Ngày tạo', icon: 'fa-regular fa-clock', width: '14%', minWidth: '150px' },
  { key: 'actions', label: 'Hành động', icon: 'fa-solid fa-bolt', width: '220px', minWidth: '220px' }
]
const filteredIntakes = computed(() => {
  const query = intakeSearch.value.trim().toLowerCase()
  const items = (intakes.value || []).filter(item => {
    const matchesQuery = !query || `${item.title || ''} ${item.submittedByName || ''} ${item.source || ''}`.toLowerCase().includes(query)
    
    if (activeFilters.value.length > 0) {
      return activeFilters.value.every(f => {
        const val = item.status || 'Pending'
        const isMatch = val === f.value
        return f.operator === 'is' ? isMatch : !isMatch
      })
    }
    
    return matchesQuery
  })
  return [...items].sort((left, right) => {
    let l
    let r
    if (intakeSortBy.value === 'title' || intakeSortBy.value === 'status') {
      l = `${left[intakeSortBy.value] || ''}`.toLowerCase()
      r = `${right[intakeSortBy.value] || ''}`.toLowerCase()
    } else {
      l = new Date(left[intakeSortBy.value] || 0).getTime()
      r = new Date(right[intakeSortBy.value] || 0).getTime()
    }
    const result = l < r ? -1 : (l > r ? 1 : 0)
    return intakeSortDirection.value === 'asc' ? result : -result
  })
})

const newIntake = ref({
  title: '',
  description: '',
  priority: 3,
  dueDate: '',
  source: 'FORM'
})

const handleIntakeRealtime = event => {
  if (`${event?.entityType || ''}`.toLowerCase() !== 'intake') return
  if (event.projectId && `${event.projectId}` !== `${props.projectId}`) return
  loadIntakes()
}

onMounted(() => {
  signalRService.on('EntityChanged', handleIntakeRealtime)
  document.addEventListener('click', handleOutsideClick)
  loadIntakes()
})

onUnmounted(() => {
  signalRService.off('EntityChanged', handleIntakeRealtime)
  document.removeEventListener('click', handleOutsideClick)
})

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
    path: buildSpacePath(props.projectId, 'work-items'),
    query: { task: taskId }
  })
}
</script>

<template>
  <div class="intake-portal">
    <!-- Header -->
    <ProjectPageHeader
      icon="fa-solid fa-inbox"
      :title="t('intakes.title')"
      :description="t('intakes.description')"
    >
      <template #actions>
        <button v-if="intakePermissions.canCreate" class="nexus-btn-primary" @click="showCreate = true">
          <i class="fa-solid fa-plus mr-1"></i> {{ t('intakes.submitNew') }}
        </button>
      </template>
    </ProjectPageHeader>

    <ProjectPageToolbar
      v-model:searchQuery="intakeSearch"
      show-search
      :search-placeholder="t('intakes.searchPlaceholder')"
    >
      <template #filters>
        <div class="filter-dropdown-wrapper js-toolbar-popup-scope">
          <button
            class="timeline-filter-trigger icon-only-trigger"
            type="button"
            aria-label="Filters"
            title="Bộ lọc"
            @click="toggleFilterDropdown"
            :class="{ active: showFilterDropdown || activeFilters.length }"
          >
            <i class="fa-solid fa-filter"></i>
            <span v-if="activeFilters.length" class="filter-count">{{ activeFilters.length }}</span>
          </button>
          <div class="plane-dropdown-menu filter-dropdown-menu" v-show="showFilterDropdown" @click.stop>
            <FilterBar
              v-model:filters="activeFilters"
              :fields="intakeFilterFields"
              :operators="intakeOperators"
              :custom-value-meta="customIntakeValueMeta"
              :active="showFilterDropdown"
            />
          </div>
        </div>
      </template>
      <template #sort>
        <ToolbarSortMenu v-model="intakeSortBy" v-model:direction="intakeSortDirection" label="Sắp xếp intake" :options="intakeSortOptions" />
      </template>
    </ProjectPageToolbar>

    <!-- Error State -->
    <ProjectEmptyState
      v-if="!loading && loadError"
      icon="fa-solid fa-shield-halved"
      title="Không thể mở Intake"
      :description="loadError"
    />
    
    <!-- Empty State -->
    <ProjectEmptyState
      v-else-if="!loading && intakes.length === 0"
      icon="fa-regular fa-envelope-open"
      :title="t('intakes.noRequestsTitle')"
      :description="t('intakes.noRequestsDesc')"
    >
      <template #action>
        <button v-if="intakePermissions.canCreate" class="empty-spaces-btn" @click="showCreate = true">
          <i class="fa-solid fa-paper-plane mr-1"></i> {{ t('intakes.submitNew') }}
        </button>
      </template>
    </ProjectEmptyState>

    <!-- Inbox List -->
    <div v-else v-loading="loading" class="intake-content-area">
      <WorkItemsListTable :columns="intakeTableColumns" :rows="filteredIntakes" min-width="1280" @row-click="viewDetail">
        <template #cell-title="{ row }">
          <div class="wi-cell">
            <span class="wi-id">{{ row.id?.slice(0, 8).toUpperCase() }}</span>
            <span class="wi-title" :title="row.title">{{ row.title }}</span>
            <span class="source-tag text-[10px]">{{ row.source }}</span>
          </div>
        </template>
        <template #cell-submittedBy="{ row }">
          <span>{{ row.submittedByName || 'Khách vãng lai' }}</span>
        </template>
        <template #cell-priority="{ row }">
          <span class="priority-badge" :style="{ color: getPriorityInfo(row.priority).color, backgroundColor: getPriorityInfo(row.priority).bg }">
            <i :class="getPriorityInfo(row.priority).icon"></i>
            {{ getPriorityInfo(row.priority).label }}
          </span>
        </template>
        <template #cell-dueDate="{ row }">
          <span class="muted-text">{{ formatDateOnly(row.desiredDueDate) }}</span>
        </template>
        <template #cell-status="{ row }">
          <span class="status-badge" :style="{ color: getStatusInfo(row.status).color, backgroundColor: getStatusInfo(row.status).bg }">
            <i :class="getStatusInfo(row.status).icon"></i>
            {{ getStatusInfo(row.status).label }}
          </span>
        </template>
        <template #cell-createdAt="{ row }">
          <span class="muted-text">{{ formatDate(row.createdAt) }}</span>
        </template>
        <template #cell-actions="{ row }">
          <div class="actions-cell" @click.stop>
            <el-button size="small" link type="primary" @click="viewDetail(row)">Chi tiết</el-button>
            <template v-if="row.status === 'Pending' && intakePermissions.canReview">
              <el-button size="small" type="success" plain @click="updateStatus(row.id, 'Accepted')">Duyệt</el-button>
              <el-button size="small" type="danger" plain @click="updateStatus(row.id, 'Declined')">Từ chối</el-button>
            </template>
            <el-button v-if="row.status === 'Accepted' && row.createdIssueId" size="small" type="primary" plain @click="navigateToTask(row.createdIssueId)">
              <i class="fa-solid fa-arrow-up-right-from-square mr-1"></i> Xem việc
            </el-button>
          </div>
        </template>
      </WorkItemsListTable>

    </div>

    <!-- Dialog: Gửi yêu cầu mới -->
    <el-dialog v-model="showCreate" width="560px" destroy-on-close append-to-body class="sa-data-dialog sa-modal--form" :show-close="false">
      <template #header>
        <DataModalHeader
          icon="bi bi-inbox"
          title="Gửi yêu cầu công việc mới"
          description="Mô tả nhu cầu để người phụ trách xem xét và tạo công việc"
          @close="showCreate = false"
        />
      </template>
      <el-form label-position="top">
        <DataModalSection icon="bi bi-card-text" title="Thông tin yêu cầu">
        <DataModalField label="Tiêu đề yêu cầu" required>
          <el-input v-model="newIntake.title" placeholder="Nhập tiêu đề ngắn gọn..." />
        </DataModalField>
        
        <DataModalField label="Mô tả chi tiết">
          <el-input v-model="newIntake.description" type="textarea" :rows="4" placeholder="Nhập chi tiết yêu cầu, lỗi gặp phải hoặc mục tiêu công việc..." />
        </DataModalField>
        </DataModalSection>

        <DataModalSection icon="bi bi-calendar2-check" title="Ưu tiên và thời hạn">
        <div class="form-grid sa-modal-form-grid">
          <DataModalField label="Mức độ ưu tiên">
            <el-select v-model="newIntake.priority" class="w-full" style="width: 100%;">
              <el-option :value="1" label="🚨 Khẩn cấp" />
              <el-option :value="2" label="🟠 Cao" />
              <el-option :value="3" label="🔵 Trung bình" />
              <el-option :value="4" label="⚪ Thấp" />
            </el-select>
          </DataModalField>

          <DataModalField label="Hạn mong muốn">
            <el-date-picker 
              v-model="newIntake.dueDate" 
              type="date" 
              placeholder="Chọn ngày hoàn thành mong muốn" 
              format="YYYY-MM-DD"
              value-format="YYYY-MM-DD"
              class="w-full" 
              style="width: 100%;"
            />
          </DataModalField>
        </div>
        </DataModalSection>
      </el-form>
      <template #footer>
        <div class="dialog-footer">
          <el-button class="cancel-btn" @click="showCreate = false"><i class="bi bi-x-lg"></i> Hủy</el-button>
          <el-button type="primary" @click="createIntake" :disabled="!newIntake.title.trim()"><i class="fa-solid fa-paper-plane"></i> Gửi yêu cầu</el-button>
        </div>
      </template>
    </el-dialog>

    <!-- Dialog: Chi tiết yêu cầu -->
    <el-dialog v-model="showDetail" width="540px" append-to-body class="sa-data-dialog sa-modal--form" :show-close="false">
      <template #header>
        <DataModalHeader
          icon="bi bi-card-checklist"
          title="Chi tiết yêu cầu công việc"
          description="Kiểm tra nội dung trước khi duyệt, từ chối hoặc tạo công việc"
          @close="showDetail = false"
        />
      </template>
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
          <el-button class="cancel-btn" @click="showDetail = false"><i class="bi bi-x-lg"></i> Đóng</el-button>
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
  gap: 0;
  font-family: 'Inter', system-ui, sans-serif;
  color: var(--color-text-primary);
}

.intake-content-area {
  width: 100%;
  padding: 0;
  box-sizing: border-box;
}



/* Table Listing */
.table-container {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.04);
  margin-top: 14px;
}

.intake-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
}

.intake-table th {
  background: var(--color-surface);
  border-bottom: 2px solid var(--color-border) !important;
  padding: 12px 16px !important;
  font-size: 11px;
  letter-spacing: 0.05em;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--color-text-secondary);
}

.intake-table th i {
  color: inherit;
  margin-right: 6px;
  opacity: 0.88;
}

.intake-table td {
  height: 50px;
  padding: 10px 14px !important;
  font-size: 13px;
  color: var(--color-text-primary);
  border-bottom: 1px solid var(--color-border) !important;
  vertical-align: middle;
  white-space: nowrap;
}

.table-row {
  box-shadow: inset 3px 0 0 transparent;
  transition: all 0.2s ease;
}

.table-row:hover {
  box-shadow: inset 3px 0 0 var(--sa-primary, var(--color-accent)) !important;
}

.table-row:hover td {
  background: color-mix(in srgb, var(--sa-primary, var(--color-accent)) 8%, var(--color-surface)) !important;
}

.title-cell {
  cursor: pointer;
  max-width: 280px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  font-weight: 700;
  color: var(--color-text-primary);
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

.filter-dropdown-wrapper {
  position: relative;
  display: inline-block;
}
.plane-dropdown-menu {
  position: absolute;
  top: calc(100% + 6px);
  right: 0;
  z-index: 1050;
  width: 290px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 9px;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.08);
  padding: 12px;
}
.filter-dropdown-menu {
  width: 640px;
  max-width: calc(100vw - 32px);
  max-height: none;
  padding: 8px !important;
  left: 0;
  right: auto;
  overflow: visible;
}
.filter-dropdown-menu :deep(.filter-bar-container) {
  min-height: auto;
  box-shadow: none;
  background: transparent;
  border: none;
  padding: 0 !important;
  overflow: visible;
}
</style>
