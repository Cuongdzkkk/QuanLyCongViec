<template>
  <section class="checkin-page">
    <header class="page-header">
      <div>
        <span class="eyebrow">DAILY CHECK-IN</span>
        <h1>{{ t('checkin.title') }}</h1>
        <p>{{ t('checkin.subtitle') }}</p>
      </div>

      <!-- Quick Action: Submit Checkin -->
      <div v-if="!userCheckedIn">
        <button class="primary-action" type="button" @click="openCheckinModal">
          <i class="fa-solid fa-plus"></i>{{ t('checkin.report') }}
        </button>
      </div>
      <div v-else>
        <el-tag type="success" size="large" class="flex items-center gap-2 font-semibold">
          <i class="fa-solid fa-circle-check" style="margin-right: 5px;"></i>Đã Check-in hôm nay
        </el-tag>
      </div>
    </header>

    <div class="page-content">
      <!-- Project Selector ComboBox -->
    <div 
      class="project-selector-wrapper"
      @mouseenter="handleMouseEnter"
    >
      <span class="project-selector-label">
        <i class="fa-solid fa-folder-open"></i>
        <span>Dự án đang xem:</span>
      </span>
      <el-select
        ref="projectSelectRef"
        v-model="activeProjectId"
        placeholder="Chọn dự án..."
        @change="selectProject"
        class="custom-project-select"
        popper-class="custom-project-dropdown"
      >
        <el-option
          v-for="p in projectsList"
          :key="p.id"
          :label="`[${p.key}] ${p.name}`"
          :value="p.id"
        />
      </el-select>
    </div>

    <!-- AI Meeting Summary Widget (Tóm tắt cuộc họp AI) -->
    <div class="ai-summary-widget card mb-6 p-5">
      <div class="flex justify-between items-center mb-3 border-bottom pb-2">
        <div style="display:flex; align-items:center; gap: 10px;">
          <i class="fa-solid fa-brain text-accent" style="font-size: 20px; flex-shrink: 0;"></i>
          <h2 class="font-bold" style="font-size: 16px; margin: 0;">{{ t('checkin.aiTitle') }}</h2>
        </div>
        <el-button size="small" class="btn-secondary" :loading="aiLoading" @click="generateAiSummary">
          <i class="fa-solid fa-wand-magic-sparkles" style="margin-right: 6px;"></i>{{ t('checkin.aiAction') }}
        </el-button>
      </div>
      <div class="summary-body">
        <p v-if="!aiSummaryText" class="text-sm text-muted italic">{{ t('checkin.aiHint') }}</p>
        <div v-else class="ai-response-box">
          <div class="text-sm leading-relaxed text-secondary mb-2" v-html="renderMarkdown(aiSummaryText)"></div>
          <div class="flex gap-2 mt-3">

            <el-tag size="small" type="success">{{ checkedInCount }}/{{ teamCheckins.length }} Thành viên đã làm</el-tag>
            <el-tag size="small" type="danger" v-if="blockerCount > 0">Có {{ blockerCount }} Blocker</el-tag>

          <el-tag size="small" type="success">{{ t('checkin.doneCount') }}</el-tag>
            <el-tag size="small" type="danger">{{ t('checkin.blockerCount') }}</el-tag>

          </div>
        </div>
      </div>
    </div>

    <!-- Checkin Cards List (Both checked-in and not checked-in members) -->
    <div class="team-checkins-grid">
      <div v-for="team in teamCheckins" :key="team.id" class="checkin-card card" :class="{ 'not-checked': !team.checkedIn }">
        <div class="card-header flex items-center justify-between">
          <div class="flex items-center" style="gap: 8px;">
            <el-avatar :size="32" :src="team.userAvatar" style="flex-shrink: 0;">{{ team.userName.charAt(0) }}</el-avatar>
            <div style="line-height: 1.3;">
              <span class="font-bold block text-sm">{{ team.userName }}</span>
              <span class="text-xxs text-muted">{{ team.role }}</span>
            </div>
          </div>
          <el-tag size="small" :type="team.checkedIn ? 'success' : 'info'">
            {{ team.checkedIn ? 'Đã Check-in' : 'Chưa Check-in' }}
          </el-tag>
        </div>

        <div class="card-body">
          <div v-if="team.checkedIn" class="checkin-details">
            <!-- Project Badge -->
            <div v-if="team.projectName" class="mb-3">
              <el-tag size="small" type="warning" class="flex items-center gap-1 w-fit font-medium" style="background-color: rgba(230, 162, 60, 0.1); border-color: rgba(230, 162, 60, 0.2); color: #e6a23c;">
                <i class="fa-solid fa-folder-open" style="margin-right: 4px;"></i>
                <span>{{ team.projectKey ? `[${team.projectKey}] ` : '' }}{{ team.projectName }}</span>
              </el-tag>
            </div>

            <!-- Done yesterday -->
            <div class="detail-section">
              <span class="section-label">✅ Ngày hôm qua:</span>
              <p class="section-desc">{{ team.yesterday }}</p>
            </div>
            
            <!-- Focus today -->
            <div class="detail-section mt-3">
              <span class="section-label">📌 Mục tiêu hôm nay:</span>
              <p class="section-desc">{{ team.today }}</p>
            </div>

            <!-- Blockers -->
            <div class="detail-section mt-3">
              <span class="section-label">⚠️ Khó khăn (Blocker):</span>
              <p class="section-desc" :class="{ 'has-blocker': team.blocker }">
                {{ team.blocker || 'Không có khó khăn gì' }}
              </p>
            </div>
          </div>
          
          <div v-else class="empty-checkin flex flex-col items-center justify-center py-6 text-center">
            <i class="fa-regular fa-bell text-2xl text-muted mb-2"></i>
          </div>
        </div>
      </div>
    </div>
    </div>

    <!-- Virtual Check-in Modal Dialog -->
    <el-dialog
      v-model="checkinModalOpen"
      width="540px"
      append-to-body
      class="sa-data-dialog sa-modal--form"
      :show-close="false"
    >
      <template #header>
        <DataModalHeader
          icon="bi bi-calendar-check"
          title="Báo cáo tiến độ hàng ngày"
          description="Chia sẻ kết quả hôm qua, mục tiêu hôm nay và khó khăn đang gặp"
          @close="checkinModalOpen = false"
        />
      </template>
      <div class="checkin-form-body flex flex-col gap-4">
        <!-- Project Select field -->
        <div>
          <span class="field-label mb-1 block">Dự án báo cáo *</span>
          <el-select 
            v-model="form.projectId" 
            placeholder="Chọn dự án liên quan..."
            class="w-full"
            disabled
          >
            <el-option
              v-for="p in projectsList"
              :key="p.id"
              :label="`[${p.key}] ${p.name}`"
              :value="p.id"
            />
          </el-select>
        </div>

        <!-- Yesterday input -->
        <div>
          <span class="field-label mb-1 block">Hôm qua bạn đã làm được gì? *</span>
          <textarea 
            v-model="form.yesterday" 
            placeholder="Ví dụ: Hoàn tất cập nhật connection string cho SQL Server, thiết kế các layout..."
            class="w-full h-20 p-2"
          ></textarea>
        </div>

        <!-- Today input -->
        <div>
          <span class="field-label mb-1 block">Mục tiêu chính hôm nay của bạn? *</span>
          <textarea 
            v-model="form.today" 
            placeholder="Ví dụ: Thiết kế giao diện Chat nhóm và Daily Checkin..."
            class="w-full h-20 p-2"
          ></textarea>
        </div>

        <!-- Blocker input -->
        <div>
          <span class="field-label mb-1 block">Khó khăn đang gặp phải (nếu có)?</span>
          <input 
            v-model="form.blocker" 
            type="text" 
            placeholder="Để trống nếu không có khó khăn nào"
            class="w-full"
          />
        </div>
      </div>
      <template #footer>
        <div class="flex justify-end gap-2">
          <el-button class="cancel-btn" @click="checkinModalOpen = false"><i class="bi bi-x-lg"></i> Hủy</el-button>
          <el-button class="btn-primary" type="primary" @click="submitCheckin"><i class="fa-solid fa-paper-plane"></i> Gửi báo cáo</el-button>
        </div>
      </template>
    </el-dialog>
  </section>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import { ElMessage } from 'element-plus'

import axiosClient from '@/api/axiosClient'

import { useI18nStore } from '@/store/useI18nStore'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'

const { t } = useI18nStore()


const checkinModalOpen = ref(false)
const aiLoading = ref(false)
const aiSummaryText = ref('')
const projectsList = ref([])
const activeProjectId = ref('')
const teamCheckins = ref([])
const userCheckedIn = ref(false)
const projectSelectRef = ref(null)

const handleMouseEnter = () => {
  if (projectSelectRef.value) {
    projectSelectRef.value.focus()
    const isExpanded = projectSelectRef.value.expanded || 
                       (projectSelectRef.value.states && projectSelectRef.value.states.menuVisible)
    if (!isExpanded && typeof projectSelectRef.value.toggleMenu === 'function') {
      projectSelectRef.value.toggleMenu()
    }
  }
}

const currentUser = ref({
  id: '',
  fullName: 'Dev Admin',
  email: 'dev@sprinta.local',
  avatarUrl: ''
})

const form = ref({
  projectId: '',
  yesterday: '',
  today: '',
  blocker: ''
})

const checkedInCount = computed(() => {
  return teamCheckins.value.filter(t => t.checkedIn).length
})

const blockerCount = computed(() => {
  return teamCheckins.value.filter(t => t.checkedIn && t.blocker).length
})

const fetchCurrentUser = async () => {
  try {
    const res = await axiosClient.get('/users/me')
    if (res.data && res.data.data) {
      currentUser.value = res.data.data
    }
  } catch (error) {
    console.error('Cannot load current user profile:', error)
  }
}

const fetchProjectMembersAndCheckins = async () => {
  if (!activeProjectId.value) return
  
  try {
    const membersRes = await axiosClient.get(`/projects/${activeProjectId.value}/members`)
    let membersData = []
    if (membersRes.data && membersRes.data.data) {
      membersData = membersRes.data.data.filter(m => m.status)
    }
    
    if (membersData.length === 0) {
      teamCheckins.value = []
      userCheckedIn.value = false
      return
    }
    
    const storedCheckins = localStorage.getItem(`project_checkins_${activeProjectId.value}`)
    let checkinMap = {}
    if (storedCheckins) {
      try {
        checkinMap = JSON.parse(storedCheckins)
      } catch (e) {
        console.error(e)
      }
    } else {
      if (activeProjectId.value === 'C0000001-0001-0001-0001-000000000001') {
        checkinMap = {
          'D0000001-0001-0001-0001-000000000009': {
            checkedIn: true,
            yesterday: 'Viết test case và thực hiện automation testing cho Kanban board.',
            today: 'Viết tài liệu báo cáo kiểm thử và test hiệu năng tải trang.',
            blocker: ''
          },
          'D0000001-0001-0001-0001-000000000006': {
            checkedIn: true,
            yesterday: 'Thiết kế giao diện chat nhóm và sửa lỗi đồng bộ tin nhắn.',
            today: 'Tối ưu hóa tốc độ render các component nặng của chat board.',
            blocker: ''
          },
          'D0000001-0001-0001-0001-000000000010': {
            checkedIn: true,
            yesterday: 'Thiết kế wireframe cho Dashboard và trang cài đặt dự án.',
            today: 'Hoàn thiện bản mockup hi-fi và bàn giao file Figma cho team Frontend.',
            blocker: ''
          }
        }
      } else if (activeProjectId.value === 'C0000001-0001-0001-0001-000000000002') {
        checkinMap = {
          'D0000001-0001-0001-0001-000000000009': {
            checkedIn: true,
            yesterday: 'Tích hợp OAuth với GitHub và Slack.',
            today: 'Cố gắng đồng bộ email Gmail thành task tự động.',
            blocker: 'Đang bị nghẽn do API rate limit của Google OAuth cần cấu hình lại khóa app.'
          }
        }
      }
    }
    
    teamCheckins.value = membersData.map(m => {
      const c = checkinMap[m.userId] || { checkedIn: false, yesterday: '', today: '', blocker: '' }
      const isMe = m.userId === currentUser.value.id || m.email === currentUser.value.email
      
      return {
        id: m.userId,
        userName: m.fullName || m.email || 'Thành viên',
        userAvatar: m.avatarUrl || '',
        role: m.roleName || 'Member',
        checkedIn: c.checkedIn,
        yesterday: c.yesterday,
        today: c.today,
        blocker: c.blocker,
        isCurrentUser: isMe
      }
    })
    
    const meCard = teamCheckins.value.find(t => t.isCurrentUser)
    userCheckedIn.value = meCard ? meCard.checkedIn : false
    
  } catch (error) {
    console.error('Cannot load project members/checkins:', error)
  }
}

onMounted(async () => {
  await fetchCurrentUser()
  try {
    const res = await axiosClient.get('/projects')
    if (res.data && res.data.data) {
      projectsList.value = res.data.data
      if (projectsList.value.length > 0) {
        const savedProjId = localStorage.getItem('active_checkin_project_id')
        if (savedProjId && projectsList.value.some(p => p.id === savedProjId)) {
          activeProjectId.value = savedProjId
        } else {
          activeProjectId.value = projectsList.value[0].id
        }
        await fetchProjectMembersAndCheckins()
      }
    }
  } catch (error) {
    console.error('Cannot load projects:', error)
  }
})

const selectProject = async (id) => {
  activeProjectId.value = id
  localStorage.setItem('active_checkin_project_id', id)
  aiSummaryText.value = ''
  await fetchProjectMembersAndCheckins()
}

const openCheckinModal = () => {
  form.value = {
    projectId: activeProjectId.value,
    yesterday: '',
    today: '',
    blocker: ''
  }
  checkinModalOpen.value = true
}

const submitCheckin = async () => {
  if (!form.value.projectId) {
    ElMessage.warning('Vui lòng chọn dự án báo cáo!')
    return
  }
  if (!form.value.yesterday.trim() || !form.value.today.trim()) {
    ElMessage.warning('Vui lòng điền đầy đủ thông tin ngày hôm qua và hôm nay!')
    return
  }

  try {
    await axiosClient.post('/checkins', {
      yesterday: form.value.yesterday,
      today: form.value.today,
      blocker: form.value.blocker,
      projectId: form.value.projectId
    })
  } catch (error) {
    console.log('API submission not available yet, saving locally:', error)
  }

  const stored = localStorage.getItem(`project_checkins_${form.value.projectId}`)
  let checkinMap = {}
  if (stored) {
    try {
      checkinMap = JSON.parse(stored)
    } catch (e) {
      console.error(e)
    }
  }
  
  checkinMap[currentUser.value.id || 'user-quan'] = {
    checkedIn: true,
    yesterday: form.value.yesterday,
    today: form.value.today,
    blocker: form.value.blocker
  }
  
  localStorage.setItem(`project_checkins_${form.value.projectId}`, JSON.stringify(checkinMap))

  if (form.value.projectId === activeProjectId.value) {
    userCheckedIn.value = true
    await fetchProjectMembersAndCheckins()
  } else {
    activeProjectId.value = form.value.projectId
    localStorage.setItem('active_checkin_project_id', form.value.projectId)
    userCheckedIn.value = true
    await fetchProjectMembersAndCheckins()
  }
  
  checkinModalOpen.value = false
  ElMessage.success('Gửi báo cáo Check-in ngày thành công!')
}

const generateAiSummary = async () => {
  if (checkedInCount.value === 0) {
    ElMessage.warning('Không có thành viên nào nộp báo cáo check-in hôm nay để tóm tắt!')
    aiSummaryText.value = 'Không có báo cáo check-in nào được nộp hôm nay.'
    return
  }
  aiLoading.value = true
  try {
    const res = await axiosClient.post('/checkins/ai-summary', {
      projectId: activeProjectId.value
    })
    if (res.data && res.data.data && res.data.data.summaryText) {
      aiSummaryText.value = res.data.data.summaryText
      ElMessage.success('Đã tạo tóm tắt họp AI thành công!')
      aiLoading.value = false
      return
    }
  } catch (error) {
    console.log('AI API summary not available yet, using simulation summary:', error)
  }

  setTimeout(() => {
    aiLoading.value = false
    const proj = projectsList.value.find(p => p.id === activeProjectId.value)
    const pName = proj ? proj.name : 'Dự án'
    
    if (activeProjectId.value === 'C0000001-0001-0001-0001-000000000002') {
      aiSummaryText.value = `### Tóm tắt Daily Scrum của cả đội - ${pName} (Hôm nay):
      
1. **Tiến độ tốt**: Bùi Minh Anh đã tích hợp thành công OAuth cho GitHub và Slack.
2. **Điểm cần lưu ý**: Minh Anh đang tìm cách đồng bộ email Gmail thành task tự động.
3. **Đề xuất**: Cần giải quyết sự cố giới hạn request của Google API.`
    } else {
      aiSummaryText.value = `### Tóm tắt Daily Scrum của cả đội - ${pName} (Hôm nay):
      
1. **Tiến độ tốt**: Bùi Minh Anh và Đặng Bảo Ngọc đã viết xong test cases và thiết kế giao diện chat.
2. **Điểm cần lưu ý**: Đỗ Quang Huy đã bàn giao mockup hi-fi cho team Frontend.
3. **Đề xuất**: Tiếp tục tối ưu hóa hiệu năng render giao diện chat và tiến hành chạy thử nghiệm.`
    }
    
    ElMessage.success('Đã tạo tóm tắt họp AI thành công!')
  }, 1200)
}

const renderMarkdown = (text) => {
  if (!text) return ''
  let html = text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/^### (.*$)/gim, '<h3 class="text-sm font-bold text-primary mt-1 mb-3 pb-2" style="border-bottom: 1px solid rgba(255,255,255,0.06);">$1</h3>')
    .replace(/\*\*(.*?)\*\*/g, '<strong style="color: var(--color-primary); font-weight: 600;">$1</strong>')
    .replace(/^(\d+)\.\s(.*$)/gim, '<div style="display: flex; align-items: flex-start; gap: 8px; margin-top: 10px; line-height: 1.6;"><span style="color: var(--color-accent); font-weight: 700;">$1.</span><span style="color: var(--color-text-secondary);">$2</span></div>')
    .replace(/\n/g, '<br>')
  return html
}
</script>

<style scoped>
.checkin-page {
  --sa-page-x: 18px;
  min-height: 100%;
  width: 100%;
  background: var(--color-bg);
  color: var(--color-text-primary);
  padding: 0 !important;
  margin: 0 !important;
}

.page-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 20px;
  padding: 22px var(--sa-page-x, 24px) 18px;
  background: var(--color-surface);
  border-bottom: none !important;
  margin-bottom: 0 !important;
}

.eyebrow {
  color: var(--color-accent);
  font-size: 10px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  display: block;
}

.page-header h1 {
  margin: 3px 0 4px;
  font-size: 22px;
  font-weight: 700;
  color: var(--color-text-primary);
  letter-spacing: 0;
  line-height: 1.2;
}

.page-header p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 12px;
}

.primary-action {
  min-height: 36px;
  border: 0;
  border-radius: 7px;
  padding: 0 14px;
  background: var(--color-accent);
  color: #fff;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  transition: opacity 0.15s ease;
}
.primary-action:hover {
  opacity: 0.9;
}

.page-content {
  padding: 18px var(--sa-page-x, 24px) 32px;
  max-width: 1080px;
  margin: 0 auto;
}

/* Project Selector wrapper */
.project-selector-wrapper {
  display: inline-flex;
  align-items: center;
  gap: 12px;
  padding: 10px 16px;
  background-color: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  margin-bottom: 20px;
  transition: all 0.25s ease;
}

.project-selector-wrapper:hover {
  border-color: var(--color-primary);
  box-shadow: 0 4px 16px color-mix(in srgb, var(--color-primary) 8%, transparent);
}

.project-selector-label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-secondary);
  white-space: nowrap;
}

.project-selector-label i {
  font-size: 15px;
  color: var(--color-primary);
}

/* Custom project select container */
.custom-project-select {
  width: 280px;
}

.ai-summary-widget {
  margin-bottom: 14px !important;
  padding: 16px 18px !important;
  border-left: 4px solid var(--color-accent);
  border-radius: 12px;
}

.ai-response-box {
  background-color: var(--color-surface-hover);
  border: 1px solid var(--color-border);
  padding: 14px;
  border-radius: var(--radius-card);
}

.team-checkins-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 14px;
}

.checkin-card {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.checkin-card.not-checked {
  opacity: 0.7;
  border-style: dashed;
}

.card-header {
  border-bottom: 1px solid var(--color-border);
  padding: 11px 14px;
}

.card-body {
  padding: 14px;
  flex: 1;
}

.section-label {
  font-size: 11px;
  font-weight: 700;
  color: var(--color-text-muted);
  text-transform: uppercase;
  display: block;
  margin-bottom: 2px;
}

.section-desc {
  font-size: 13px;
  color: var(--color-text-primary);
  line-height: 1.5;
}

.has-blocker {
  color: var(--color-danger);
  font-weight: 500;
}

:deep(.card-header .el-avatar) {
  margin: 0 !important;
  flex-shrink: 0;
}

@media (max-width: 860px) {
  .checkin-container {
    padding: 16px !important;
  }

  .checkin-container .page-header,
  .ai-summary-widget > .flex {
    align-items: flex-start !important;
    flex-direction: column !important;
  }
}
</style>

<style>
/* Non-scoped styles for custom project dropdown options and select input overrides */
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

.custom-project-dropdown .el-popper__arrow::before {
  background-color: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
}
</style>
