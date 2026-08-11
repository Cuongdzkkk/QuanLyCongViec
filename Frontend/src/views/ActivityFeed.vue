<template>
  <div class="feed-container admin-page sp-page-shell">
    <div class="page-header flex justify-between items-center mb-6">
      <div>
        <h1 class="page-title flex items-center gap-2">
          <i class="fa-solid fa-bolt text-warning"></i>
          Bảng Tin Hoạt Động Nhóm
        </h1>
        <p class="page-subtitle">Theo dõi các cập nhật, chỉnh sửa công việc và tương tác theo thời gian thực từ các thành viên trong dự án.</p>
      </div>

      <!-- Action Button to Clear or Refresh -->
      <div>
        <el-button class="btn-refresh-accent" @click="refreshFeed">
          <i class="fa-solid fa-arrows-rotate mr-2"></i>Làm mới
        </el-button>
      </div>
    </div>

    <!-- Project Chat & Post Update Box -->
    <div class="project-post-box settings-card mb-6">
      <div class="flex flex-wrap items-center justify-between gap-4 mb-4 pb-3 border-b border-slate-700/40">
        <h3 class="text-sm font-semibold text-primary flex items-center" style="gap: 8px !important;">
          <i class="fa-solid fa-pen-fancy text-primary text-base" style="margin-right: 6px !important;"></i>
          <span>Thảo luận & Cập nhật hoạt động</span>
        </h3>
        <div class="flex items-center" style="gap: 20px !important;">
          <div class="flex items-center" style="gap: 8px !important;" @mouseenter="handleProjectMouseEnter">
            <span class="text-xs text-muted font-medium" style="margin-right: 4px !important;">Dự án:</span>
            <el-select 
              ref="projectSelectRef"
              v-model="selectedPostProject" 
              size="default" 
              class="custom-project-select" 
              style="width: 220px;"
              popper-class="custom-project-dropdown"
            >
              <!-- Dummy helper reference to trigger default project value assignment on render -->
              <span style="display:none;">{{ unwatchProjects }}</span>
              <el-option label="— Tất cả dự án —" value="all" />
              <el-option 
                v-for="proj in sidebarProjects" 
                :key="proj.id" 
                :label="proj.name" 
                :value="proj.name" 
              />
            </el-select>
          </div>
          <div class="flex items-center" style="gap: 8px !important;" @mouseenter="handleTypeMouseEnter">
            <span class="text-xs text-muted font-medium" style="margin-right: 4px !important; margin-left: 6px !important;">Loại:</span>
            <el-select 
              ref="typeSelectRef"
              v-model="selectedPostType" 
              size="default" 
              class="custom-project-select" 
              style="width: 120px;"
              popper-class="custom-project-dropdown"
            >
              <el-option label="Công việc" value="task" />
              <el-option label="Bình luận" value="comment" />
              <el-option label="Trạng thái" value="status" />
            </el-select>
          </div>
        </div>
      </div>
      
      <div class="post-input-wrapper flex flex-col" style="gap: 16px !important;">
        <input 
          v-model="newPostTarget" 
          type="text" 
          placeholder="Nhập tiêu đề công việc hoặc tiêu đề thảo luận..." 
          class="custom-post-field w-full"
        />
        <textarea 
          v-model="newPostDetail" 
          placeholder="Viết nội dung thảo luận chi tiết hoặc cập nhật công việc tại đây (không bắt buộc)..." 
          class="custom-post-textarea w-full"
          rows="4"
          style="min-height: 90px; max-height: 200px;"
        ></textarea>
        <div class="flex justify-end" style="margin-top: 6px !important;">
          <el-button class="btn-post-submit" @click="submitActivityPost">
            <i class="fa-solid fa-paper-plane mr-2"></i>Gửi tin cập nhật
          </el-button>
        </div>
      </div>
    </div>

    <!-- Filter Control Bar -->
    <div class="filter-bar settings-card flex items-center gap-4 mb-6">
      <span class="text-xs font-bold text-muted uppercase tracking-wider filter-label">Lọc theo:</span>
      <div class="flex flex-wrap gap-2.5">
        <el-button 
          v-for="filter in filterOptions" 
          :key="filter.value" 
          size="default"
          :class="activeFilter === filter.value ? 'filter-active-btn' : 'filter-inactive-btn'"
          @click="activeFilter = filter.value"
        >
          <i :class="filter.icon" style="margin-right: 8px !important;"></i> {{ filter.label }}
        </el-button>
      </div>
    </div>

    <!-- Feed Timeline -->
    <div class="feed-timeline">
      <div v-if="filteredActivities.length === 0" class="empty-state text-center py-12">
        <i class="fa-regular fa-folder-open text-4xl text-muted mb-4 block"></i>
        <p class="text-muted">Không tìm thấy hoạt động nào phù hợp.</p>
      </div>

      <div v-else class="timeline-wrapper">
        <div 
          v-for="act in filteredActivities" 
          :key="act.id" 
          class="timeline-item"
        >
          <!-- Left: User Avatar & Icon -->
          <div class="item-avatar-col">
            <el-avatar :size="42" :src="act.userAvatar">{{ act.userName.charAt(0) }}</el-avatar>
            <div class="activity-type-badge-elevated" :class="act.type">
              <i :class="getTypeIcon(act.type)"></i>
            </div>
          </div>

          <!-- Right: Details -->
          <div class="item-content-card card">
            <div class="flex flex-wrap justify-between items-start gap-2 mb-3">
              <div class="activity-desc flex flex-wrap items-center">
                <span class="font-semibold text-primary" style="margin-right: 6px;">{{ act.userName }}</span>
                <span class="text-secondary" style="margin-right: 6px;">{{ act.description }}</span>
                <span v-if="act.target" class="font-semibold text-accent" style="margin-left: 2px;">{{ act.target }}</span>
              </div>
              <span class="activity-time text-xs text-muted font-medium">{{ formatTimeAgo(act.timestamp) }}</span>
            </div>

            <!-- Details Block (If present) -->
            <div v-if="act.detail" class="activity-detail-block">
              <p class="text-sm text-secondary leading-relaxed">{{ act.detail }}</p>
            </div>

            <!-- Metadata tags -->
            <div class="activity-meta mt-4 flex items-center gap-3">
              <el-tag size="small" class="custom-project-tag">{{ act.project }}</el-tag>
              <span class="tag-separator"></span>
              <el-tag size="small" class="custom-type-tag" :class="act.type">{{ act.type.toUpperCase() }}</el-tag>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { useProjectStore } from '@/store/useProjectStore'
import axiosClient from '@/api/axiosClient'

const projectStore = useProjectStore()
const loading = ref(false)

// Load projects dynamically on mount
onMounted(() => {
  projectStore.fetchAllProjects()
  fetchActivities()
})

const sidebarProjects = computed(() => projectStore.sidebarProjects || [])

const activeFilter = ref('all')
const selectedPostProject = ref('all')
const selectedPostType = ref('comment')
const newPostTarget = ref('')
const newPostDetail = ref('')

const projectSelectRef = ref(null)
const typeSelectRef = ref(null)

const handleProjectMouseEnter = () => {
  if (projectSelectRef.value) {
    projectSelectRef.value.focus()
    const isExpanded = projectSelectRef.value.expanded || 
                       (projectSelectRef.value.states && projectSelectRef.value.states.menuVisible)
    if (!isExpanded && typeof projectSelectRef.value.toggleMenu === 'function') {
      projectSelectRef.value.toggleMenu()
    }
  }
}

const handleTypeMouseEnter = () => {
  if (typeSelectRef.value) {
    typeSelectRef.value.focus()
    const isExpanded = typeSelectRef.value.expanded || 
                       (typeSelectRef.value.states && typeSelectRef.value.states.menuVisible)
    if (!isExpanded && typeof typeSelectRef.value.toggleMenu === 'function') {
      typeSelectRef.value.toggleMenu()
    }
  }
}

// Watch projects list to set default project selection
const unwatchProjects = computed(() => {
  if (sidebarProjects.value.length > 0 && !selectedPostProject.value) {
    selectedPostProject.value = 'all'
  }
  return sidebarProjects.value
})


const filterOptions = [
  { label: 'Tất cả', value: 'all', icon: 'fa-solid fa-list' },
  { label: 'Công việc', value: 'task', icon: 'fa-solid fa-square-check' },
  { label: 'Bình luận', value: 'comment', icon: 'fa-solid fa-comment' },
  { label: 'Lịch trình', value: 'sprint', icon: 'fa-solid fa-calendar-days' },
  { label: 'Trạng thái', value: 'status', icon: 'fa-solid fa-face-smile' }
]

const activities = ref([])

const fetchActivities = async () => {
  loading.value = true
  try {
    const res = await axiosClient.get('/auditlogs', {
      params: {
        limit: 50
      }
    })
    if (res.data && res.data.data && res.data.data.items) {
      activities.value = res.data.data.items.map(item => ({
        id: item.id,
        userName: item.user,
        userAvatar: '', 
        type: item.action === 'create' ? 'task' : (item.action === 'comment' ? 'comment' : (item.action === 'system' ? 'sprint' : 'status')),
        description: item.summary,
        target: item.resource,
        detail: '', 
        timestamp: item.timestamp,
        project: item.projectName || 'TaskManagement'
      }))
    }
  } catch (error) {
    console.error('Cannot load audit logs:', error)
    // Fallback to mock data if API fails
    activities.value = [
      {
        id: 1,
        userName: 'Nguyễn Tuấn Kiệt',
        userAvatar: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&q=80&w=128',
        type: 'task',
        description: 'vừa hoàn thành công việc',
        target: 'Tích hợp mô hình AI Priority',
        detail: 'Đã hoàn tất tính toán điểm ưu tiên dựa trên trọng số deadline và mức độ quan trọng.',
        timestamp: new Date(Date.now() - 1000 * 60 * 12).toISOString(),
        project: 'Dự án Alpha'
      },
      {
        id: 2,
        userName: 'Phạm Minh Tú',
        userAvatar: 'https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?auto=format&fit=crop&q=80&w=128',
        type: 'comment',
        description: 'đã bình luận trong task',
        target: 'Thiết kế cơ sở dữ liệu phân công',
        detail: '“Cần bổ sung bảng lịch sử phân công (AssignmentHistory) để tracking chặt chẽ hơn.”',
        timestamp: new Date(Date.now() - 1000 * 60 * 45).toISOString(),
        project: 'Dự án Beta'
      },
      {
        id: 3,
        userName: 'Đoàn Minh Quân',
        userAvatar: '',
        type: 'status',
        description: 'đã cập nhật trạng thái làm việc mới',
        target: '💻 Đang code giao diện Team Collaboration',
        detail: null,
        timestamp: new Date(Date.now() - 1000 * 3600 * 1.5).toISOString(),
        project: 'Workspace Chung'
      },
      {
        id: 4,
        userName: 'Lê Tiến Đạt',
        userAvatar: 'https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?auto=format&fit=crop&q=80&w=128',
        type: 'sprint',
        description: 'vừa bắt đầu Sprint mới',
        target: 'Sprint 5 - Hoàn thiện các tính năng cốt lõi',
        detail: 'Sprint kéo dài từ 01/07/2026 đến 15/07/2026.',
        timestamp: new Date(Date.now() - 1000 * 3600 * 3).toISOString(),
        project: 'Dự án Alpha'
      },
      {
        id: 5,
        userName: 'Trần Gia Phát',
        userAvatar: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&q=80&w=128',
        type: 'task',
        description: 'đã chuyển trạng thái công việc sang In Progress',
        target: 'Kéo thả các thẻ Kanban',
        detail: 'Đang triển khai thư viện vuedraggable.',
        timestamp: new Date(Date.now() - 1000 * 3600 * 5).toISOString(),
        project: 'Dự án Alpha'
      }
    ]
  } finally {
    loading.value = false
  }
}

const filteredActivities = computed(() => {
  let list = activities.value
  if (selectedPostProject.value && selectedPostProject.value !== 'all') {
    list = list.filter(a => a.project === selectedPostProject.value)
  }
  if (activeFilter.value !== 'all') {
    list = list.filter(a => a.type === activeFilter.value)
  }
  return list
})

const getTypeIcon = (type) => {
  switch (type) {
    case 'task': return 'fa-solid fa-square-check'
    case 'comment': return 'fa-solid fa-comment'
    case 'status': return 'fa-solid fa-face-smile'
    case 'sprint': return 'fa-solid fa-calendar-days'
    default: return 'fa-solid fa-circle-info'
  }
}

const getTagType = (type) => {
  switch (type) {
    case 'task': return 'success'
    case 'comment': return 'warning'
    case 'status': return 'danger'
    case 'sprint': return ''
    default: return 'info'
  }
}

const formatTimeAgo = (dateObj) => {
  const diffMs = Date.now() - new Date(dateObj).getTime()
  const diffMins = Math.floor(diffMs / 60000)
  if (diffMins < 60) return `${diffMins} phút trước`
  const diffHours = Math.floor(diffMins / 60)
  if (diffHours < 24) return `${diffHours} giờ trước`
  return new Date(dateObj).toLocaleDateString()
}

const refreshFeed = async () => {
  await fetchActivities()
  ElMessage.success('Đã cập nhật bảng tin hoạt động mới nhất')
}

const submitActivityPost = () => {
  if (!newPostTarget.value.trim()) {
    ElMessage.warning('Vui lòng nhập tiêu đề cập nhật!')
    return
  }

  const newActivity = {
    id: Date.now(),
    userName: 'Bạn (dev)',
    userAvatar: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&q=80&w=128',
    type: selectedPostType.value,
    description: selectedPostType.value === 'comment' ? 'đã thảo luận trong' : (selectedPostType.value === 'task' ? 'vừa cập nhật công việc' : 'đã cập nhật trạng thái mới'),
    target: newPostTarget.value,
    detail: newPostDetail.value.trim() || null,
    timestamp: new Date(),
    project: selectedPostProject.value === 'all' ? (sidebarProjects.value.length > 0 ? sidebarProjects.value[0].name : 'Workspace Chung') : selectedPostProject.value
  }

  // Prepend new activity
  activities.value.unshift(newActivity)
  
  // Clear fields
  newPostTarget.value = ''
  newPostDetail.value = ''
  
  ElMessage.success('Đã đăng bài thảo luận thành công lên Bảng Tin!')
}
</script>

<style scoped>
.feed-container {
  max-width: 900px !important;
}

.feed-timeline {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.timeline-wrapper {
  position: relative;
  padding-left: 28px;
  border-left: 2px solid rgba(56, 189, 248, 0.15);
  display: flex;
  flex-direction: column;
  gap: 28px;
}

.timeline-item {
  position: relative;
  display: flex;
  gap: 20px;
}

.item-avatar-col {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
}



.activity-type-badge-elevated {
  position: absolute;
  bottom: -4px;
  right: -6px;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  color: white;
  border: 2px solid var(--color-surface);
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.3);
  z-index: 2;
}

.activity-type-badge-elevated.task { background-color: #10b981; }
.activity-type-badge-elevated.comment { background-color: #f59e0b; }
.activity-type-badge-elevated.status { background-color: #ef4444; }
.activity-type-badge-elevated.sprint { background-color: #3b82f6; }

.item-content-card {
  flex: 1;
  padding: 18px;
  border-radius: 12px;
  background-color: var(--color-surface);
  border: 1px solid var(--color-border);
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
  transition: all 0.2s;
}

.item-content-card:hover {
  border-color: rgba(56, 189, 248, 0.3);
  box-shadow: 0 6px 12px -2px rgba(0, 0, 0, 0.15);
  transform: translateY(-1px);
}

.activity-desc span {
  font-size: 14.5px;
}

.activity-detail-block {
  background-color: rgba(255, 255, 255, 0.02);
  padding: 12px 16px;
  border-radius: 8px;
  margin-top: 10px;
  border: 1px solid var(--color-border);
  border-left: 3px solid var(--color-primary);
}

/* Custom design for filter buttons */
.filter-bar {
  padding: 12px 20px;
  border-radius: 12px;
  background-color: var(--color-surface);
  border: 1px solid var(--color-border);
}

.filter-label {
  margin-right: 12px !important;
}

.filter-active-btn {
  background: linear-gradient(135deg, #0ea5e9, #0284c7) !important;
  border: none !important;
  color: white !important;
  font-weight: 600 !important;
  border-radius: 6px !important;
  box-shadow: 0 4px 10px rgba(14, 165, 233, 0.2) !important;
  transition: all 0.25s !important;
}

.filter-active-btn:hover {
  transform: translateY(-1px);
  box-shadow: 0 6px 14px rgba(14, 165, 233, 0.3) !important;
}

.filter-inactive-btn {
  background: rgba(255, 255, 255, 0.03) !important;
  border: 1px solid var(--color-border) !important;
  color: var(--color-text-secondary) !important;
  border-radius: 6px !important;
  transition: all 0.2s !important;
}

.filter-inactive-btn:hover {
  background: rgba(255, 255, 255, 0.08) !important;
  color: var(--color-text-primary) !important;
  border-color: rgba(255, 255, 255, 0.15) !important;
}

/* Refresh Button Accent styling */
.btn-refresh-accent {
  background: linear-gradient(135deg, rgba(56, 189, 248, 0.12), rgba(56, 189, 248, 0.04)) !important;
  border: 1px solid rgba(56, 189, 248, 0.3) !important;
  color: #38bdf8 !important;
  font-weight: 600 !important;
  border-radius: 8px !important;
  padding: 8px 16px !important;
  height: 38px !important;
  transition: all 0.25s !important;
}

.btn-refresh-accent:hover {
  background: linear-gradient(135deg, rgba(56, 189, 248, 0.2), rgba(56, 189, 248, 0.08)) !important;
  border-color: #38bdf8 !important;
  color: #ffffff !important;
  box-shadow: 0 0 12px rgba(56, 189, 248, 0.25) !important;
  transform: translateY(-1px);
}

/* Custom Tags style & Spacing */
.custom-project-tag {
  background-color: rgba(255, 255, 255, 0.04) !important;
  border-color: var(--color-border) !important;
  color: var(--color-text-secondary) !important;
  border-radius: 6px !important;
  padding: 4px 10px !important;
  height: auto !important;
}

.tag-separator {
  width: 4px;
  height: 4px;
  border-radius: 50%;
  background-color: rgba(255, 255, 255, 0.2);
}

.custom-type-tag {
  font-weight: 600 !important;
  border-radius: 6px !important;
  padding: 4px 10px !important;
  height: auto !important;
}
.custom-type-tag.task {
  background-color: rgba(16, 185, 129, 0.1) !important;
  border-color: rgba(16, 185, 129, 0.2) !important;
  color: #10b981 !important;
}
.custom-type-tag.comment {
  background-color: rgba(245, 158, 11, 0.1) !important;
  border-color: rgba(245, 158, 11, 0.2) !important;
  color: #f59e0b !important;
}
.custom-type-tag.status {
  background-color: rgba(239, 68, 68, 0.1) !important;
  border-color: rgba(239, 68, 68, 0.2) !important;
  color: #ef4444 !important;
}
.custom-type-tag.sprint {
  background-color: rgba(59, 130, 246, 0.1) !important;
  border-color: rgba(59, 130, 246, 0.2) !important;
  color: #3b82f6 !important;
}

/* Post box styles */
.project-post-box {
  padding: 20px 24px;
  border-radius: 14px;
  background-color: var(--color-surface);
  border: 1px solid var(--color-border);
  box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.2), 0 4px 6px -2px rgba(0, 0, 0, 0.1);
  background-image: linear-gradient(135deg, rgba(56, 189, 248, 0.03), rgba(56, 189, 248, 0.01));
}

.custom-post-field {
  background: rgba(255, 255, 255, 0.02);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 10px 16px;
  color: var(--color-text-primary);
  font-size: 14px;
  outline: none;
  transition: all 0.25s;
}

.custom-post-field:focus {
  border-color: rgba(56, 189, 248, 0.5);
  background: rgba(255, 255, 255, 0.04);
  box-shadow: 0 0 0 3px rgba(56, 189, 248, 0.15);
}

.custom-post-textarea {
  background: rgba(255, 255, 255, 0.02);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 12px 16px;
  color: var(--color-text-primary);
  font-size: 13.5px;
  outline: none;
  resize: vertical;
  transition: all 0.25s;
}

.custom-post-textarea:focus {
  border-color: rgba(56, 189, 248, 0.5);
  background: rgba(255, 255, 255, 0.04);
  box-shadow: 0 0 0 3px rgba(56, 189, 248, 0.15);
}

.btn-post-submit {
  background: linear-gradient(135deg, #0ea5e9, #0284c7) !important;
  border: none !important;
  color: white !important;
  font-weight: 600 !important;
  border-radius: 8px !important;
  padding: 0 22px !important;
  height: 42px !important;
  transition: all 0.25s !important;
  box-shadow: 0 4px 12px rgba(14, 165, 233, 0.25) !important;
}

.btn-post-submit:hover {
  transform: translateY(-1px);
  box-shadow: 0 6px 16px rgba(14, 165, 233, 0.35) !important;
  filter: brightness(1.05);
}

:deep(.custom-project-select .el-input__wrapper) {
  background-color: rgba(255, 255, 255, 0.03) !important;
  border: 1px solid var(--color-border) !important;
  box-shadow: none !important;
  border-radius: 8px !important;
  padding: 4px 12px !important;
  transition: all 0.2s !important;
}

:deep(.custom-project-select .el-input__wrapper:hover) {
  border-color: rgba(56, 189, 248, 0.3) !important;
}

:deep(.custom-project-select .el-input__wrapper.is-focus) {
  border-color: rgba(56, 189, 248, 0.5) !important;
  box-shadow: 0 0 0 1px rgba(56, 189, 248, 0.5) !important;
}

:deep(.custom-project-select .el-input__inner) {
  color: var(--color-text-primary) !important;
  font-size: 13px !important;
  font-weight: 500 !important;
}

:deep(.custom-project-select .el-select__wrapper) {
  background-color: rgba(255, 255, 255, 0.03) !important;
  border: 1px solid var(--color-border) !important;
  box-shadow: none !important;
  border-radius: 8px !important;
  padding: 4px 12px !important;
  transition: all 0.2s !important;
  height: 36px !important;
  min-height: 36px !important;
}

:deep(.custom-project-select .el-select__wrapper:hover) {
  border-color: rgba(56, 189, 248, 0.3) !important;
}

:deep(.custom-project-select .el-select__wrapper.is-focus) {
  border-color: rgba(56, 189, 248, 0.5) !important;
  box-shadow: 0 0 0 1px rgba(56, 189, 248, 0.5) !important;
}

:deep(.custom-project-select .el-select__selected-item) {
  color: var(--color-text-primary) !important;
  font-size: 13px !important;
  font-weight: 500 !important;
}

.feed-container.admin-page {
  width: min(100%, 1080px) !important;
  max-width: 1080px !important;
  margin: 0 auto !important;
  padding: 22px 24px 32px !important;
}

.feed-container .page-header {
  margin-bottom: 16px !important;
  padding: 16px 18px !important;
  border: 1px solid color-mix(in srgb, var(--color-border) 88%, transparent);
  border-radius: 14px;
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--color-accent) 7%, var(--color-surface)), var(--color-surface));
  box-shadow: 0 16px 42px color-mix(in srgb, #020617 10%, transparent);
}

.feed-container .page-title {
  margin-bottom: 4px !important;
  font-size: clamp(24px, 2.2vw, 34px) !important;
  line-height: 1.1 !important;
}

.feed-container .page-subtitle {
  max-width: 720px;
  margin: 0 !important;
  font-size: 14px !important;
  line-height: 1.5 !important;
}

.project-post-box {
  margin-bottom: 14px !important;
  padding: 16px 18px;
}

.filter-bar {
  margin-bottom: 14px !important;
  padding: 10px 14px;
}

.timeline-wrapper {
  gap: 16px;
}

.item-content-card {
  padding: 14px 16px;
  border-radius: 10px;
}

@media (max-width: 860px) {
  .feed-container.admin-page {
    padding: 16px !important;
  }

  .feed-container .page-header,
  .project-post-box .flex {
    align-items: flex-start !important;
    flex-direction: column !important;
  }
}
</style>

<style>
/* Global style overrides for Element Plus Select Dropdowns to match Dark theme */
.el-select__popper.el-popper,
.el-popper.is-light,
.el-popper.is-pure {
  background: #111c2d !important;
  border: 1px solid rgba(56, 189, 248, 0.2) !important;
  box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.5) !important;
  border-radius: 8px !important;
}

/* Remove default light-theme borders */
.el-popper.is-light[data-popper-placement^=bottom] .el-popper__arrow::before,
.el-popper.is-light[data-popper-placement^=top] .el-popper__arrow::before {
  border-color: rgba(56, 189, 248, 0.2) !important;
  background: #111c2d !important;
}

.el-select-dropdown__list {
  padding: 6px 0 !important;
  background: #111c2d !important;
}

.el-select-dropdown__item {
  color: #94a3b8 !important;
  font-size: 13.5px !important;
  padding: 8px 16px !important;
  height: 38px !important;
  line-height: 22px !important;
  background: transparent !important;
  transition: all 0.15s !important;
}

/* Hover state styling */
.el-select-dropdown__item.hover,
.el-select-dropdown__item.is-hovering,
.el-select-dropdown__item:hover,
.el-select-dropdown__item.el-select-dropdown__item.hover {
  background-color: rgba(56, 189, 248, 0.12) !important;
  color: #f8fafc !important;
}

/* Selected state styling */
.el-select-dropdown__item.selected,
.el-select-dropdown__item.is-selected {
  color: #38bdf8 !important;
  background-color: rgba(56, 189, 248, 0.18) !important;
  font-weight: 600 !important;
}

.el-popper__arrow::before {
  background: #111c2d !important;
  border-color: rgba(56, 189, 248, 0.2) !important;
}

/* High specificity overrides for custom dropdown select views */
body .custom-project-select .el-input__wrapper {
  background-color: var(--color-surface-hover) !important;
  border: 1px solid var(--color-border) !important;
  box-shadow: none !important;
  border-radius: 8px !important;
  padding: 6px 12px !important;
  height: 38px !important;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1) !important;
}

body .custom-project-select .el-input__wrapper:hover {
  border-color: var(--color-primary) !important;
  background-color: color-mix(in srgb, var(--color-primary) 5%, var(--color-surface-hover)) !important;
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-primary) 15%, transparent) !important;
}

body .custom-project-select .el-input__wrapper.is-focus {
  border-color: var(--color-primary) !important;
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-primary) 20%, transparent) !important;
}

body .custom-project-select .el-input__inner {
  color: var(--color-text-primary) !important;
  font-weight: 550 !important;
  font-size: 13px !important;
}

.custom-project-dropdown {
  background-color: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.45) !important;
  border-radius: 10px !important;
  padding: 4px 0 !important;
}

.custom-project-dropdown .el-select-dropdown__item {
  border-radius: 6px !important;
  margin: 3px 6px !important;
  padding: 8px 12px !important;
  height: auto !important;
  line-height: 1.4 !important;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1) !important;
  color: var(--color-text-secondary) !important;
  font-weight: 500 !important;
  font-size: 13px !important;
}

body .custom-project-dropdown .el-select-dropdown__item.hover,
body .custom-project-dropdown .el-select-dropdown__item:hover,
body .custom-project-dropdown .el-select-dropdown__item.is-hovering {
  background-color: color-mix(in srgb, var(--color-primary) 15%, transparent) !important;
  background: color-mix(in srgb, var(--color-primary) 15%, transparent) !important;
  color: #ffffff !important;
  transform: translateX(4px) !important;
}

body .custom-project-dropdown .el-select-dropdown__item.selected {
  background-color: var(--color-primary) !important;
  background: var(--color-primary) !important;
  color: #ffffff !important;
  font-weight: 600 !important;
}

body .custom-project-dropdown .el-select-dropdown__item.selected.hover,
body .custom-project-dropdown .el-select-dropdown__item.selected:hover,
body .custom-project-dropdown .el-select-dropdown__item.selected.is-hovering {
  background-color: color-mix(in srgb, var(--color-primary) 85%, #000000) !important;
  background: color-mix(in srgb, var(--color-primary) 85%, #000000) !important;
  color: #ffffff !important;
  transform: translateX(4px) !important;
}
</style>
