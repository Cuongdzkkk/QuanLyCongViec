<template>
  <section class="checkin-page">
    <header class="page-header app-shell-page-header">
      <div class="app-shell-title-wrap">
        <span class="eyebrow">DAILY CHECK-IN</span>
        <h1>{{ t('checkin.title') }}</h1>
        <div class="app-shell-header-help">
          <span class="app-shell-header-help-btn" aria-label="About Daily Check-in">
            <i class="fa-solid fa-question"></i>
          </span>
          <div class="app-shell-header-help-popover" role="tooltip">
            <span>DAILY CHECK-IN</span>
            <p>{{ t('checkin.subtitle') }}</p>
          </div>
        </div>
      </div>

      <!-- Quick Action: Submit Checkin -->
      <div v-if="!userCheckedIn">
        <button class="primary-action" type="button" @click="openCheckinModal">
          <i class="fa-solid fa-plus"></i>{{ t('checkin.report') }}
        </button>
      </div>
      <div v-else>
        <el-tag type="success" size="large" class="flex items-center gap-2 font-semibold">
          <i class="fa-solid fa-circle-check" style="margin-right: 5px;"></i>ÄĂ£ Check-in hĂ´m nay
        </el-tag>
      </div>
    </header>

    <div class="page-content">
      <!-- Check-in filters -->
    <div class="project-selector-wrapper">
      <el-select
        ref="projectSelectRef"
        v-model="activeProjectId"
        placeholder="Filter dự án"
        @change="selectProject"
        class="custom-project-select"
        popper-class="custom-project-dropdown"
      >
        <template #prefix>
          <i class="fa-solid fa-filter"></i>
        </template>
        <el-option
          v-for="p in projectsList"
          :key="p.id"
          :label="`[${p.key}] ${p.name}`"
          :value="p.id"
        />
      </el-select>

      <label class="checkin-search-field">
        <i class="fa-solid fa-magnifying-glass"></i>
        <input v-model="checkinSearch" type="search" placeholder="Tìm thành viên, vai trò, nội dung..." />
      </label>

      <div class="checkin-filter-group" aria-label="Check-in filters">
        <button type="button" :class="{ active: checkinStatusFilter === 'all' }" @click="checkinStatusFilter = 'all'">All</button>
        <button type="button" :class="{ active: checkinStatusFilter === 'checked' }" @click="checkinStatusFilter = 'checked'">&#272;&#227; l&#224;m</button>
        <button type="button" :class="{ active: checkinStatusFilter === 'missing' }" @click="checkinStatusFilter = 'missing'">Ch&#432;a l&#224;m</button>
        <button type="button" :class="{ active: checkinStatusFilter === 'blocker' }" @click="checkinStatusFilter = 'blocker'">Blocker</button>
      </div>

      <span class="checkin-filter-count">{{ filteredTeamCheckins.length }}/{{ teamCheckins.length }}</span>
    </div>

    <!-- AI Meeting Summary Widget (TĂ³m táº¯t cuá»™c há»p AI) -->
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

            <el-tag size="small" type="success">{{ checkedInCount }}/{{ teamCheckins.length }} ThĂ nh viĂªn Ä‘Ă£ lĂ m</el-tag>
            <el-tag size="small" type="danger" v-if="blockerCount > 0">CĂ³ {{ blockerCount }} Blocker</el-tag>

          <el-tag size="small" type="success">{{ t('checkin.doneCount') }}</el-tag>
            <el-tag size="small" type="danger">{{ t('checkin.blockerCount') }}</el-tag>

          </div>
        </div>
      </div>
    </div>

    <!-- Checkin Cards List (Both checked-in and not checked-in members) -->
    <div class="team-checkins-grid">
      <div v-for="team in filteredTeamCheckins" :key="team.id" class="checkin-card card" :class="{ 'not-checked': !team.checkedIn }">
        <div class="card-header flex items-center justify-between">
          <div class="flex items-center" style="gap: 8px;">
            <el-avatar :size="32" :src="team.userAvatar" style="flex-shrink: 0;">{{ team.userName.charAt(0) }}</el-avatar>
            <div style="line-height: 1.3;">
              <span class="font-bold block text-sm">{{ team.userName }}</span>
              <span class="text-xxs text-muted">{{ team.role }}</span>
            </div>
          </div>
          <el-tag size="small" :type="team.checkedIn ? 'success' : 'info'">
            {{ team.checkedIn ? 'ÄĂ£ Check-in' : 'ChÆ°a Check-in' }}
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
              <span class="section-label">âœ… NgĂ y hĂ´m qua:</span>
              <p class="section-desc">{{ team.yesterday }}</p>
            </div>
            
            <!-- Focus today -->
            <div class="detail-section mt-3">
              <span class="section-label">đŸ“Œ Má»¥c tiĂªu hĂ´m nay:</span>
              <p class="section-desc">{{ team.today }}</p>
            </div>

            <!-- Blockers -->
            <div class="detail-section mt-3">
              <span class="section-label">â ï¸ KhĂ³ khÄƒn (Blocker):</span>
              <p class="section-desc" :class="{ 'has-blocker': team.blocker }">
                {{ team.blocker || 'KhĂ´ng cĂ³ khĂ³ khÄƒn gĂ¬' }}
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
          title="BĂ¡o cĂ¡o tiáº¿n Ä‘á»™ hĂ ng ngĂ y"
          description="Chia sáº» káº¿t quáº£ hĂ´m qua, má»¥c tiĂªu hĂ´m nay vĂ  khĂ³ khÄƒn Ä‘ang gáº·p"
          @close="checkinModalOpen = false"
        />
      </template>
      <div class="checkin-form-body flex flex-col gap-4">
        <!-- Project Select field -->
        <div>
          <span class="field-label mb-1 block">Dá»± Ă¡n bĂ¡o cĂ¡o *</span>
          <el-select 
            v-model="form.projectId" 
            placeholder="Chá»n dá»± Ă¡n liĂªn quan..."
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
          <span class="field-label mb-1 block">HĂ´m qua báº¡n Ä‘Ă£ lĂ m Ä‘Æ°á»£c gĂ¬? *</span>
          <textarea 
            v-model="form.yesterday" 
            placeholder="VĂ­ dá»¥: HoĂ n táº¥t cáº­p nháº­t connection string cho SQL Server, thiáº¿t káº¿ cĂ¡c layout..."
            class="w-full h-20 p-2"
          ></textarea>
        </div>

        <!-- Today input -->
        <div>
          <span class="field-label mb-1 block">Má»¥c tiĂªu chĂ­nh hĂ´m nay cá»§a báº¡n? *</span>
          <textarea 
            v-model="form.today" 
            placeholder="VĂ­ dá»¥: Thiáº¿t káº¿ giao diá»‡n Chat nhĂ³m vĂ  Daily Checkin..."
            class="w-full h-20 p-2"
          ></textarea>
        </div>

        <!-- Blocker input -->
        <div>
          <span class="field-label mb-1 block">KhĂ³ khÄƒn Ä‘ang gáº·p pháº£i (náº¿u cĂ³)?</span>
          <input 
            v-model="form.blocker" 
            type="text" 
            placeholder="Äá»ƒ trá»‘ng náº¿u khĂ´ng cĂ³ khĂ³ khÄƒn nĂ o"
            class="w-full"
          />
        </div>
      </div>
      <template #footer>
        <div class="flex justify-end gap-2">
          <el-button class="cancel-btn" @click="checkinModalOpen = false"><i class="bi bi-x-lg"></i> Há»§y</el-button>
          <el-button class="btn-primary" type="primary" @click="submitCheckin"><i class="fa-solid fa-paper-plane"></i> Gá»­i bĂ¡o cĂ¡o</el-button>
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
import { useProjectStore } from '@/store/useProjectStore'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'

const { t } = useI18nStore()
const projectStore = useProjectStore()


const checkinModalOpen = ref(false)
const aiLoading = ref(false)
const aiSummaryText = ref('')
const projectsList = ref([])
const activeProjectId = ref('')
const teamCheckins = ref([])
const userCheckedIn = ref(false)
const projectSelectRef = ref(null)
const checkinSearch = ref('')
const checkinStatusFilter = ref('all')

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

const filteredTeamCheckins = computed(() => {
  const query = checkinSearch.value.trim().toLowerCase()

  return teamCheckins.value.filter(team => {
    if (checkinStatusFilter.value === 'checked' && !team.checkedIn) return false
    if (checkinStatusFilter.value === 'missing' && team.checkedIn) return false
    if (checkinStatusFilter.value === 'blocker' && !team.blocker) return false

    if (!query) return true

    return [
      team.userName,
      team.role,
      team.projectName,
      team.projectKey,
      team.yesterday,
      team.today,
      team.blocker
    ]
      .filter(Boolean)
      .some(value => `${value}`.toLowerCase().includes(query))
  })
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
    const res = await axiosClient.get('/checkins', {
      params: { projectId: activeProjectId.value }
    })
    const payload = res.data?.data || {}
    teamCheckins.value = Array.isArray(payload.members) ? payload.members : []

    const meCard = teamCheckins.value.find(t => t.isCurrentUser)
    userCheckedIn.value = meCard ? meCard.checkedIn : false
  } catch (error) {
    console.error('Cannot load project members/checkins:', error)
    ElMessage.error(error.response?.data?.message || 'Khong the tai danh sach check-in.')
    teamCheckins.value = []
    userCheckedIn.value = false
  }
}

onMounted(async () => {
  await fetchCurrentUser()
  try {
    const projects = await projectStore.fetchAllProjects(true)
    projectsList.value = projects
      .filter(project => project.isMember !== false)
      .map(project => ({
        id: project.id,
        key: project.key,
        name: project.name,
        isMember: project.isMember,
        originalRow: project.originalRow
      }))

    if (projectsList.value.length > 0) {
      const savedProjId = localStorage.getItem('active_checkin_project_id')
      if (savedProjId && projectsList.value.some(p => p.id === savedProjId)) {
        activeProjectId.value = savedProjId
      } else {
        activeProjectId.value = projectsList.value[0].id
      }
      await fetchProjectMembersAndCheckins()
    }
  } catch (error) {
    console.error('Cannot load projects:', error)
    ElMessage.error(error.response?.data?.message || 'Khong the tai danh sach du an.')
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
    ElMessage.warning('Vui long chon du an bao cao!')
    return
  }
  if (!form.value.yesterday.trim() || !form.value.today.trim()) {
    ElMessage.warning('Vui long dien day du thong tin ngay hom qua va hom nay!')
    return
  }

  try {
    await axiosClient.post('/checkins', {
      yesterday: form.value.yesterday,
      today: form.value.today,
      blocker: form.value.blocker,
      projectId: form.value.projectId
    })

    activeProjectId.value = form.value.projectId
    localStorage.setItem('active_checkin_project_id', form.value.projectId)
    await fetchProjectMembersAndCheckins()
    checkinModalOpen.value = false
    ElMessage.success('Gui bao cao Check-in ngay thanh cong!')
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Khong the gui bao cao check-in.')
  }
}

const generateAiSummary = async () => {
  if (checkedInCount.value === 0) {
    ElMessage.warning('Khong co bao cao check-in hom nay de tom tat!')
    aiSummaryText.value = 'Khong co bao cao check-in nao duoc nop hom nay.'
    return
  }
  aiLoading.value = true
  try {
    const res = await axiosClient.post('/checkins/ai-summary', {
      projectId: activeProjectId.value
    })
    if (res.data?.data?.summaryText) {
      aiSummaryText.value = res.data.data.summaryText
      ElMessage.success('Da tao tom tat check-in thanh cong!')
    }
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Khong the tao tom tat check-in.')
  } finally {
    aiLoading.value = false
  }
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
  max-width: none;
  margin: 0;
}

/* Project Selector wrapper */
.project-selector-wrapper {
  position: relative;
  z-index: 5;
  width: 100%;
  min-height: 42px;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px !important;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 86%, transparent), color-mix(in srgb, var(--color-surface-hover) 46%, transparent));
  border: 1px solid color-mix(in srgb, var(--color-border) 72%, transparent);
  border-radius: 12px !important;
  margin-bottom: 18px;
  box-shadow: 0 10px 24px color-mix(in srgb, #020617 6%, transparent);
  box-sizing: border-box;
  overflow: hidden;
  transition: all 0.25s ease;
}

.project-selector-wrapper:hover {
  border-color: color-mix(in srgb, var(--color-border) 72%, transparent);
  box-shadow: 0 10px 24px color-mix(in srgb, #020617 6%, transparent);
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
  width: min(220px, 24vw);
  flex: 0 0 min(220px, 24vw);
}

.custom-project-select :deep(.el-select__wrapper) {
  min-height: 34px;
  height: 34px;
  border-radius: 9px !important;
  border: 1px solid var(--color-border);
  box-shadow: none !important;
  background: var(--color-surface) !important;
  overflow: hidden;
  transition: border-color 0.2s, box-shadow 0.2s, background 0.2s;
}

.custom-project-select :deep(.el-select__wrapper:hover),
.custom-project-select :deep(.el-select__wrapper.is-focused) {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border));
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important;
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15) !important;
}

.custom-project-select :deep(.el-select__prefix) {
  color: var(--color-text-secondary);
  font-size: 13px;
}

.custom-project-select :deep(.el-select__placeholder),
.custom-project-select :deep(.el-select__selected-item) {
  font-size: 13px;
  font-weight: 600;
}

.checkin-search-field {
  position: relative;
  flex: 0 0 min(326px, 34vw);
  width: min(326px, 34vw);
  min-height: 34px;
  display: flex;
  align-items: center;
  color: var(--color-text-muted);
}

.checkin-search-field > i {
  position: absolute;
  left: 12px;
  top: 50%;
  z-index: 1;
  transform: translateY(-50%);
  color: var(--color-text-muted);
  font-size: 14px;
  pointer-events: none;
}

.checkin-search-field input {
  width: 100%;
  height: 34px !important;
  box-sizing: border-box !important;
  padding: 0 12px 0 36px !important;
  border: 1px solid var(--color-border) !important;
  border-radius: 9px !important;
  outline: 0;
  background: var(--color-surface) !important;
  color: var(--color-text-primary) !important;
  font-size: 13.5px !important;
  overflow: hidden;
  -webkit-appearance: none;
  appearance: none;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.checkin-search-field input:focus {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15);
}

.checkin-filter-group {
  display: flex;
  align-items: center;
  gap: 2px;
  height: 32px;
  padding: 2px;
  border: 1px solid var(--color-border);
  border-radius: 8px !important;
  background: var(--color-surface-hover);
  overflow: hidden;
}

.checkin-filter-group button {
  height: 28px;
  min-height: 28px;
  padding: 0 10px;
  border: 1px solid transparent;
  border-radius: 6px !important;
  background: transparent;
  color: var(--color-text-muted);
  font-size: 12.5px;
  font-weight: 650;
  cursor: pointer;
  transition: all 0.2s ease;
}

.checkin-filter-group button:hover,
.checkin-filter-group button.active {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border));
  background: color-mix(in srgb, var(--color-accent) 14%, var(--color-surface));
  color: var(--color-accent);
  box-shadow: 0 1px 3px rgba(0,0,0,0.1);
}

.checkin-filter-count {
  margin-left: auto;
  color: var(--color-text-muted);
  font-size: 11px;
  white-space: nowrap;
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
  background-color: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
  box-shadow: none !important;
  border-radius: 9px !important;
  padding: 0 12px !important;
  height: 34px !important;
  min-height: 34px !important;
  transition: border-color 0.2s, box-shadow 0.2s, background 0.2s !important;
}

body .custom-project-select .el-input__wrapper:hover {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  background-color: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important;
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15) !important;
}

body .custom-project-select .el-input__wrapper.is-focus {
  border-color: var(--color-accent) !important;
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15) !important;
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
