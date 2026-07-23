<template>
  <div>
    <div class="ai-page-flex-wrapper">
      <div class="ai-container">
        <div class="ai-page-header">
          <div class="header-left">
            <h2 class="page-title">Trá»£ lÃ½ AI</h2>
            <span class="header-pill">Chat, breakdown, repo analysis</span>
          </div>
        </div>

        <div class="repo-panel">
          <div class="repo-head">
            <div>
              <div class="panel-title">GitHub repo analysis</div>
              <div class="panel-copy">Chá»n repo, Ä‘á»c nhanh metadata vÃ  gá»­i má»™t prompt phÃ¢n tÃ­ch task vÃ o chat box.</div>
            </div>
            <button class="ghost-btn" type="button" :disabled="repoLoading" @click="analyzeRepository">Analyze repo</button>
          </div>
          <div class="repo-grid">
            <input v-model="repoForm.url" type="text" class="repo-input" placeholder="https://github.com/owner/repo" />
            <input v-model="repoForm.token" type="password" class="repo-input" placeholder="GitHub token (optional)" />
          </div>
          <div class="repo-actions">
            <button class="ghost-btn" type="button" @click="useQuickPrompt('Phan ra task sau thanh 3-5 subtask ro rang, co test va ban giao.')">
              Chen lenh breakdown
            </button>
            <button class="ghost-btn" type="button" :disabled="repoLoading" @click="prepareBreakdownPrompt">
              Mau prompt phan ra task
            </button>
          </div>
          <p v-if="repoStatus" class="repo-status">{{ repoStatus }}</p>
          <div v-if="repoAnalysis" class="repo-analysis-preview">
            <div class="analysis-title">{{ repoAnalysis.repository }}</div>
            <p class="analysis-summary">{{ repoAnalysis.summary }}</p>
            <div class="analysis-actions">
              <div class="analysis-project">
                <strong>Project tao task:</strong>
                <span>{{ activeProjectName }}</span>
              </div>
              <div class="analysis-action-buttons">
                <button class="ghost-btn" type="button" :disabled="createBacklogLoading || !canCreateIntoProject" @click="createBacklogItems('quick')">
                  {{ createBacklogLoading === 'quick' ? 'Creating...' : 'Create quick wins' }}
                </button>
                <button class="ghost-btn" type="button" :disabled="createBacklogLoading || !canCreateIntoProject" @click="createBacklogItems('medium')">
                  {{ createBacklogLoading === 'medium' ? 'Creating...' : 'Create medium tasks' }}
                </button>
                <button class="ghost-btn" type="button" :disabled="createBacklogLoading || !canCreateIntoProject" @click="createBacklogItems('risky')">
                  {{ createBacklogLoading === 'risky' ? 'Creating...' : 'Create risky tasks' }}
                </button>
                <button class="ghost-btn" type="button" :disabled="createBacklogLoading || !canCreateIntoProject" @click="createBacklogItems('all')">
                  {{ createBacklogLoading === 'all' ? 'Creating...' : 'Create all' }}
                </button>
              </div>
            </div>
            <div v-if="canManageProjectAi" class="operational-review-card">
              <div class="review-head">
                <div>
                  <div class="analysis-col-title">Operational review</div>
                  <p class="review-copy">PM/PO/SM/Admin co the chot backlog AI, xem tong estimate va dua task vao backlog hoac cycle dang chon.</p>
                </div>
                <div class="review-stats">
                  <div class="review-stat">
                    <span class="review-stat-label">Selected</span>
                    <strong>{{ selectedBacklogItems.length }}</strong>
                  </div>
                  <div class="review-stat">
                    <span class="review-stat-label">Estimate</span>
                    <strong>{{ selectedEstimateHours }}h</strong>
                  </div>
                  <div class="review-stat">
                    <span class="review-stat-label">Risky</span>
                    <strong>{{ selectedRiskCount }}</strong>
                  </div>
                </div>
              </div>

              <div class="review-controls">
                <label class="review-checkbox">
                  <input type="checkbox" :checked="allBacklogSelected" @change="toggleAllBacklogSelections($event.target.checked)" />
                  <span>Select all AI backlog items</span>
                </label>

                <div class="review-cycle-picker">
                  <span>Target cycle</span>
                  <select v-model="reviewTargetSprintId" class="repo-input">
                    <option value="">Backlog</option>
                    <option v-for="cycle in availablePlanningCycles" :key="cycle.id" :value="cycle.id">
                      {{ cycle.name }}
                    </option>
                  </select>
                </div>
              </div>

              <div class="analysis-columns review-columns">
                <div class="analysis-col">
                  <div class="analysis-col-title">Quick wins</div>
                  <label v-for="item in normalizedQuickWins" :key="item.selectionKey" class="review-item">
                    <input
                      type="checkbox"
                      :checked="isBacklogItemSelected(item.selectionKey)"
                      @change="toggleBacklogSelection(item.selectionKey, $event.target.checked)"
                    />
                    <span class="review-item-body">
                      <strong>{{ item.title }}</strong>
                      <small>{{ item.suggestedHours }}h Â· P{{ item.priority }}</small>
                    </span>
                  </label>
                </div>
                <div class="analysis-col">
                  <div class="analysis-col-title">Medium tasks</div>
                  <label v-for="item in normalizedMediumTasks" :key="item.selectionKey" class="review-item">
                    <input
                      type="checkbox"
                      :checked="isBacklogItemSelected(item.selectionKey)"
                      @change="toggleBacklogSelection(item.selectionKey, $event.target.checked)"
                    />
                    <span class="review-item-body">
                      <strong>{{ item.title }}</strong>
                      <small>{{ item.suggestedHours }}h Â· P{{ item.priority }}</small>
                    </span>
                  </label>
                </div>
                <div class="analysis-col">
                  <div class="analysis-col-title">Risky tasks</div>
                  <label v-for="item in normalizedRiskyTasks" :key="item.selectionKey" class="review-item">
                    <input
                      type="checkbox"
                      :checked="isBacklogItemSelected(item.selectionKey)"
                      @change="toggleBacklogSelection(item.selectionKey, $event.target.checked)"
                    />
                    <span class="review-item-body">
                      <strong>{{ item.title }}</strong>
                      <small>{{ item.suggestedHours }}h Â· P{{ item.priority }}</small>
                    </span>
                  </label>
                </div>
              </div>

              <div class="review-foot">
                <div class="review-test-plan">
                  <div class="analysis-col-title">Test plan</div>
                  <ul>
                    <li v-for="(step, index) in repoAnalysis.testPlan || []" :key="`test-plan-${index}`">{{ step }}</li>
                  </ul>
                </div>
                <div class="analysis-action-buttons">
                  <button
                    class="ghost-btn"
                    type="button"
                    :disabled="createBacklogLoading || !canCreateIntoProject || !selectedBacklogItems.length"
                    @click="createReviewedBacklogItems"
                  >
                    {{ createBacklogLoading === 'review' ? 'Creating...' : `Create selected to ${reviewTargetSprintLabel}` }}
                  </button>
                </div>
              </div>
            </div>
            <div class="analysis-columns">
              <div class="analysis-col">
                <div class="analysis-col-title">Quick wins</div>
                <ul>
                  <li v-for="item in repoAnalysis.quickWins" :key="`quick-${item.title}`">
                    {{ item.title }} Â· {{ item.suggestedHours }}h
                  </li>
                </ul>
              </div>
              <div class="analysis-col">
                <div class="analysis-col-title">Medium tasks</div>
                <ul>
                  <li v-for="item in repoAnalysis.mediumTasks" :key="`medium-${item.title}`">
                    {{ item.title }} Â· {{ item.suggestedHours }}h
                  </li>
                </ul>
              </div>
              <div class="analysis-col">
                <div class="analysis-col-title">Risky tasks</div>
                <ul>
                  <li v-for="item in repoAnalysis.riskyTasks" :key="`risk-${item.title}`">
                    {{ item.title }} Â· {{ item.suggestedHours }}h
                  </li>
                </ul>
              </div>
            </div>
          </div>
        </div>

        <div class="chat-history">
          <div v-for="(msg, idx) in chatHistory" :key="idx" class="chat-row" :class="msg.role">
            <div v-if="msg.role === 'bot'" class="bot-icon-circle"><i class="fa-solid fa-robot"></i></div>
            <div :class="['bubble', msg.role === 'user' ? 'primary' : '']">
              <div>{{ msg.content }}</div>
              <div v-if="msg.progressSteps?.length" class="thinking-steps">
                <div
                  v-for="(step, stepIdx) in msg.progressSteps"
                  :key="`${idx}-${stepIdx}`"
                  class="thinking-step"
                  :class="{ active: stepIdx <= (msg.progressIndex || 0) }"
                >
                  <i class="fa-solid fa-circle-notch" v-if="stepIdx === (msg.progressIndex || 0) && msg.isTyping"></i>
                  <i class="fa-solid fa-check" v-else-if="stepIdx < (msg.progressIndex || 0)"></i>
                  <i class="fa-regular fa-circle" v-else></i>
                  <span>{{ step }}</span>
                </div>
              </div>
              <i v-if="msg.isTyping" class="fa-solid fa-ellipsis fa-fade"></i>
            </div>
            <div v-if="msg.role === 'user'" class="user-avatar-circle">{{ userInitials }}</div>
          </div>
        </div>

        <div class="ai-chat-input-wrapper">
          <div class="input-box">
            <i class="fa-solid fa-paperclip attach-btn"></i>
            <input
              v-model="userMessage"
              type="text"
              placeholder="Hoi SprintA AI bat cu dieu gi..."
              :disabled="isLoading"
              @keyup.enter="sendMessage()"
            />
            <button class="send-btn" type="button" :disabled="isLoading || !userMessage.trim()" @click="sendMessage()">
              <span class="fa fa-paper-plane"></span>
            </button>
          </div>
          <div class="ai-disclaimer">SprintA AI co the mac sai sot. Hay kiem tra lai cac thong tin quan trong.</div>
        </div>
      </div>

      <aside class="ai-details-panel">
        <div class="panel-section">
          <div class="section-label">Tro ly AI</div>
          <div class="section-title">HANH DONG NHANH</div>
          <div class="quick-links">
            <button class="q-link" type="button" @click="useQuickPrompt('Tao lo trinh cho du an hien tai')">
              <i class="fa-solid fa-map-location-dot"></i> Tao lo trinh
            </button>
            <button class="q-link" type="button" @click="useQuickPrompt('Tom tat cac cong viec quan trong')">
              <i class="fa-regular fa-file-lines"></i> Tom tat cong viec
            </button>
            <button class="q-link" type="button" @click="useQuickPrompt('Soan ban cap nhat tien do ngan gon')">
              <i class="fa-solid fa-pen-nib"></i> Soan ban cap nhat
            </button>
            <button class="q-link" type="button" @click="prepareBreakdownPrompt()">
              <i class="fa-solid fa-list-check"></i> Breakdown task
            </button>
          </div>
        </div>

        <div class="panel-section mt-30">
          <div class="section-title">NHAC NHO</div>
          <p class="text-muted sidebar-copy">Retry Gemini da bat 3 lan o backend. Neu AI tre, bong loading se hien cac buoc dang xu ly.</p>
        </div>

        <div class="upgrade-card-wrapper">
          <div class="upgrade-card">
            <div class="plan-label">GOI PRO</div>
            <div class="plan-desc">Mo khoa cac truy van AI khong gioi han va quy trinh lam viec tuy chinh.</div>
            <button class="btn-upgrade" type="button" @click="router.push('/rewards')">Nang cap ngay</button>
          </div>
        </div>
      </aside>
    </div>

    <CustomizeSidebarModal :visible="showCustomizeModal" @update:visible="showCustomizeModal = $event" @saved="handleSidebarSaved" />
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import CustomizeSidebarModal from '../components/CustomizeSidebarModal.vue'

import axiosClient from '@/api/axiosClient'
import { useProjectStore } from '@/store/useProjectStore'
import { useWorkTaskStore } from '@/store/useWorkTaskStore'
import { useSprintStore } from '@/store/useSprintStore'
import { useI18nStore } from '@/store/useI18nStore'
import { broadcastAdminRealtime } from '@/utils/adminRealtime'
import { signalRService } from '@/api/signalrService'
import { hasProjectWritePermission, normalizeProjectRole } from '@/utils/permissions'
import { getScopedCurrentProjectId } from '@/utils/projectContext'
import { clearLegacyGitHubCredentialStorage, runWithEphemeralGitHubToken } from '@/utils/githubCredentials'

const router = useRouter()
const projectStore = useProjectStore()
const workTaskStore = useWorkTaskStore()
const sprintStore = useSprintStore()
const i18nStore = useI18nStore()
const currentUser = JSON.parse(localStorage.getItem('user') || '{}')
const showCustomizeModal = ref(false)
const sidebarPreferences = ref({ audit: true, users: true })

const userMessage = ref('')
const isLoading = ref(false)
const repoLoading = ref(false)
const repoStatus = ref('')
const repoAnalysis = ref(null)
const createBacklogLoading = ref('')
const selectedBacklogKeys = ref([])
const reviewTargetSprintId = ref('')
const repoForm = ref({
  url: '',
  token: ''
})

const chatHistory = ref([
  {
    role: 'bot',
    content: 'Xin chÃ o! TÃ´i lÃ  trá»£ lÃ½ AI cá»§a SprintA. TÃ´i cÃ³ thá»ƒ táº¡o task tháº­t, thá»‘ng kÃª dá»± Ã¡n, tÃ³m táº¯t cÃ´ng viá»‡c, breakdown task vÃ  phÃ¢n tÃ­ch repo GitHub Ä‘á»ƒ gá»£i Ã½ backlog.'
  }
])

const breakdownProgressSteps = [
  'Dang phan tich task',
  'Dang goi Gemini',
  'Dang thu lai neu can',
  'Dang tong hop ket qua'
]

const defaultProgressSteps = [
  'Dang doc yeu cau',
  'Dang truy van AI',
  'Dang tong hop phan hoi'
]

let progressTimer = null

const userInitials = computed(() => {
  const name = currentUser?.name || currentUser?.fullName || 'ME'
  return name.substring(0, 2).toUpperCase()
})

const currentProjectId = computed(() => getScopedCurrentProjectId())
const activeProjectName = computed(() => {
  const projectId = currentProjectId.value
  if (!projectId) return 'Chua chon project'
  const project = projectStore.allProjects.find(item => item.id === projectId) || projectStore.currentProject
  return project?.name || `Project ${projectId}`
})
const currentProjectRecord = computed(() => {
  const projectId = `${currentProjectId.value || ''}`
  if (!projectId) {
    return null
  }

  if (`${projectStore.currentProject?.id || ''}` === projectId) {
    return projectStore.currentProject
  }

  return projectStore.allProjects.find(item => `${item.id || ''}` === projectId) || null
})

const currentProjectRole = computed(() => normalizeProjectRole(
  currentProjectRecord.value?.myRole
  || currentProjectRecord.value?.MyRole
  || currentProjectRecord.value?.projectRole
  || currentProjectRecord.value?.ProjectRole
))

const canManageProjectAi = computed(() => {
  return hasProjectWritePermission(currentProjectRole.value)
})

const canCreateIntoProject = computed(() => Boolean(currentProjectId.value && repoAnalysis.value && canManageProjectAi.value))
const availablePlanningCycles = computed(() => (sprintStore.sprints || []).filter(item => `${item.state || ''}`.toLowerCase() !== 'completed'))

const buildSelectionKey = (category, item) => `${category}::${item.title}::${item.priority}::${item.suggestedHours}`
const normalizeReviewItems = (items, category) => (items || []).map(item => ({
  ...item,
  category,
  selectionKey: buildSelectionKey(category, item)
}))

const normalizedQuickWins = computed(() => normalizeReviewItems(repoAnalysis.value?.quickWins, 'quick-win'))
const normalizedMediumTasks = computed(() => normalizeReviewItems(repoAnalysis.value?.mediumTasks, 'medium'))
const normalizedRiskyTasks = computed(() => normalizeReviewItems(repoAnalysis.value?.riskyTasks, 'risky'))
const allBacklogItems = computed(() => [
  ...normalizedQuickWins.value,
  ...normalizedMediumTasks.value,
  ...normalizedRiskyTasks.value
])
const selectedBacklogItems = computed(() => allBacklogItems.value.filter(item => selectedBacklogKeys.value.includes(item.selectionKey)))
const allBacklogSelected = computed(() => allBacklogItems.value.length > 0 && selectedBacklogItems.value.length === allBacklogItems.value.length)
const selectedEstimateHours = computed(() => Math.round(selectedBacklogItems.value.reduce((sum, item) => sum + Number(item.suggestedHours || 0), 0) * 10) / 10)
const selectedRiskCount = computed(() => selectedBacklogItems.value.filter(item => `${item.category}` === 'risky').length)
const reviewTargetSprintLabel = computed(() => {
  if (!reviewTargetSprintId.value) {
    return 'backlog'
  }
  return availablePlanningCycles.value.find(item => item.id === reviewTargetSprintId.value)?.name || 'selected cycle'
})

const syncReviewSelectionFromAnalysis = () => {
  selectedBacklogKeys.value = allBacklogItems.value.map(item => item.selectionKey)
}

const isBacklogItemSelected = (selectionKey) => selectedBacklogKeys.value.includes(selectionKey)

const toggleBacklogSelection = (selectionKey, checked) => {
  if (checked) {
    selectedBacklogKeys.value = Array.from(new Set([...selectedBacklogKeys.value, selectionKey]))
    return
  }

  selectedBacklogKeys.value = selectedBacklogKeys.value.filter(key => key !== selectionKey)
}

const toggleAllBacklogSelections = (checked) => {
  selectedBacklogKeys.value = checked ? allBacklogItems.value.map(item => item.selectionKey) : []
}

const clearProgressTimer = () => {
  if (progressTimer) {
    window.clearInterval(progressTimer)
    progressTimer = null
  }
}

const isBreakdownPrompt = (message) => {
  const text = `${message || ''}`.toLowerCase()
  return text.includes('phan ra') || text.includes('breakdown') || text.includes('subtask') || text.includes('sub-work item')
}

const startThinkingMessage = (message) => {
  const progressSteps = isBreakdownPrompt(message) ? breakdownProgressSteps : defaultProgressSteps
  const thinkingMessage = {
    role: 'bot',
    content: progressSteps[0],
    isTyping: true,
    progressSteps,
    progressIndex: 0
  }

  chatHistory.value.push(thinkingMessage)
  clearProgressTimer()
  progressTimer = window.setInterval(() => {
    const activeMessage = chatHistory.value[chatHistory.value.length - 1]
    if (!activeMessage?.isTyping || !activeMessage.progressSteps?.length) {
      clearProgressTimer()
      return
    }

    const nextIndex = Math.min((activeMessage.progressIndex || 0) + 1, activeMessage.progressSteps.length - 1)
    activeMessage.progressIndex = nextIndex
    activeMessage.content = activeMessage.progressSteps[nextIndex]
  }, 900)
}

const sendMessage = async (overrideMessage = null) => {
  const outgoing = `${overrideMessage ?? userMessage.value}`.trim()
  if (!outgoing || isLoading.value) return

  if (!overrideMessage) {
    userMessage.value = ''
  }

  chatHistory.value.push({ role: 'user', content: outgoing })
  isLoading.value = true
  startThinkingMessage(outgoing)

  try {
    const history = chatHistory.value
      .filter(item => !item.isTyping)
      .slice(-10)
      .map(item => ({ role: item.role === 'bot' ? 'assistant' : 'user', content: item.content }))

    const response = await axiosClient.post('/ai/command', {
      prompt: outgoing,
      projectId: currentProjectId.value || null,
      locale: i18nStore.locale,
      history
    })

    clearProgressTimer()
    chatHistory.value.pop()
    const payload = response.data?.data ?? response.data
    const message = payload?.message || response.data?.message || (i18nStore.locale === 'en'
      ? 'Sorry, AI did not return content. Please try another request.'
      : 'R\u1ea5t ti\u1ebfc, AI kh\u00f4ng ph\u1ea3n h\u1ed3i n\u1ed9i dung. B\u1ea1n c\u00f3 th\u1ec3 th\u1eed l\u1ea1i v\u1edbi c\u00e2u h\u1ecfi kh\u00e1c.')
    chatHistory.value.push({ role: 'bot', content: message })

    if (payload?.action === 'create-task' && currentProjectId.value) {
      await Promise.all([
        workTaskStore.fetchTasks(currentProjectId.value, { reset: false }).catch(() => []),
        projectStore.fetchProjectDetails(currentProjectId.value, { force: true }).catch(() => null)
      ])
      notifyProjectRealtime('work-item-created', { taskId: payload.createdTask?.id, source: 'ai-command' })
      ElMessage.success(i18nStore.locale === 'en' ? 'AI created a real task.' : 'AI \u0111\u00e3 t\u1ea1o task th\u1eadt.')
    }
  } catch (error) {
    clearProgressTimer()
    chatHistory.value.pop()
    const message = error.response?.data?.message || error.response?.data?.error || error.message || 'Lá»—i káº¿t ná»‘i'
    let friendlyMessage = `AI khÃ´ng thá»ƒ xá»­ lÃ½ yÃªu cáº§u lÃºc nÃ y: ${message}`
    
    if (message.toLowerCase().includes('quota') || message.toLowerCase().includes('limit') || error.response?.status === 429) {
      friendlyMessage = 'Báº¡n Ä‘Ã£ Ä‘áº¡t giá»›i háº¡n sá»­ dá»¥ng AI (Quota). Vui lÃ²ng thá»­ láº¡i sau hoáº·c nÃ¢ng cáº¥p gÃ³i thÃ nh viÃªn.'
    } else if (message.toLowerCase().includes('key') || message.toLowerCase().includes('auth')) {
      friendlyMessage = 'Lá»—i cáº¥u hÃ¬nh API key. Vui lÃ²ng liÃªn há»‡ quáº£n trá»‹ viÃªn Ä‘á»ƒ kiá»ƒm tra láº¡i há»‡ thá»‘ng.'
    }
    
    chatHistory.value.push({ role: 'bot', content: friendlyMessage })
  } finally {
    isLoading.value = false
  }
}

const useQuickPrompt = (prompt) => {
  userMessage.value = prompt
}

const prepareBreakdownPrompt = () => {
  userMessage.value = 'Phan ra task sau thanh 3-5 subtask ro rang. Moi subtask can co muc tieu, owner de xuat, test/checklist va ban giao.'
}

const analyzeRepository = async () => {
  const repoUrl = repoForm.value.url.trim()
  if (!repoUrl) {
    ElMessage.warning('Hay nhap repo GitHub truoc.')
    return
  }

  const parsed = parseRepo(repoUrl)
  if (!parsed) {
    ElMessage.error('Repo URL khong dung dinh dang GitHub.')
    return
  }

  repoLoading.value = true
  repoStatus.value = 'Dang phan tich repo qua backend AI...'

  try {
    const response = await runWithEphemeralGitHubToken(repoForm.value, gitHubToken =>
      axiosClient.post('/ai/repo-analysis', {
        repoUrl,
        gitHubToken,
        focus: 'Repository planning, backlog, risks, and test strategy'
      }))

    const analysis = response.data?.data
    if (!analysis) {
      throw new Error('AI khong tra ve repo analysis hop le.')
    }

    repoAnalysis.value = analysis
    syncReviewSelectionFromAnalysis()
    userMessage.value = analysis.suggestedPrompt || `Phan tich repo ${parsed.owner}/${parsed.repo} va de xuat backlog tiep theo.`
    repoStatus.value = `Da phan tich repo ${analysis.repository}. Prompt da san sang trong chat box.`
    chatHistory.value.push({
      role: 'bot',
      content: [
        `Repo ${analysis.repository}: ${analysis.summary}`,
        '',
        `Quick wins: ${(analysis.quickWins || []).map(item => item.title).join(' | ') || 'Khong co'}`,
        `Medium tasks: ${(analysis.mediumTasks || []).map(item => item.title).join(' | ') || 'Khong co'}`,
        `Risky tasks: ${(analysis.riskyTasks || []).map(item => item.title).join(' | ') || 'Khong co'}`
      ].join('\n')
    })
  } catch (error) {
    repoStatus.value = error.response?.data?.message || error.message || 'Khong phan tich duoc repo.'
    ElMessage.error(repoStatus.value)
  } finally {
    repoLoading.value = false
  }
}

const createBacklogItems = async (mode) => {
  if (!repoAnalysis.value) {
    ElMessage.warning('Hay phan tich repo truoc')
    return
  }

  if (!currentProjectId.value) {
    ElMessage.warning('Hay chon project tren sidebar truoc')
    return
  }

  if (!canManageProjectAi.value) {
    ElMessage.error('You do not have permission to create AI backlog items for this project.')
    return
  }

  createBacklogLoading.value = mode
  try {
    const response = await axiosClient.post('/ai/repo-analysis/create-work-items', {
      projectId: currentProjectId.value,
      repository: repoAnalysis.value.repository,
      includeQuickWins: mode === 'quick' || mode === 'all',
      includeMediumTasks: mode === 'medium' || mode === 'all',
      includeRiskyTasks: mode === 'risky' || mode === 'all',
      quickWins: repoAnalysis.value.quickWins || [],
      mediumTasks: repoAnalysis.value.mediumTasks || [],
      riskyTasks: repoAnalysis.value.riskyTasks || []
    })

    const created = response.data?.data || []
    if (created.length > 0) {
      await Promise.all([
        workTaskStore.fetchTasks(currentProjectId.value, { reset: false }).catch(() => []),
        projectStore.fetchProjectDetails(currentProjectId.value, { force: true }).catch(() => null),
        projectStore.fetchAllProjects(true).catch(() => [])
      ])
      notifyProjectRealtime('project-settings-updated', { source: 'ai-repo-create' })
    }

    ElMessage.success(response.data?.message || `Da tao ${created.length} work items`)
    repoStatus.value = `Da tao ${created.length} work items vao ${activeProjectName.value}.`
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Khong tao duoc AI backlog items')
  } finally {
    createBacklogLoading.value = ''
  }
}

const createReviewedBacklogItems = async () => {
  if (!repoAnalysis.value) {
    ElMessage.warning('Hay phan tich repo truoc')
    return
  }

  if (!currentProjectId.value) {
    ElMessage.warning('Hay chon project tren sidebar truoc')
    return
  }

  if (!canManageProjectAi.value) {
    ElMessage.error('You do not have permission to create AI backlog items for this project.')
    return
  }

  if (!selectedBacklogItems.value.length) {
    ElMessage.warning('Hay chon it nhat mot AI backlog item')
    return
  }

  createBacklogLoading.value = 'review'
  try {
    const response = await axiosClient.post('/ai/repo-analysis/create-work-items', {
      projectId: currentProjectId.value,
      targetSprintId: reviewTargetSprintId.value || null,
      repository: repoAnalysis.value.repository,
      includeQuickWins: false,
      includeMediumTasks: false,
      includeRiskyTasks: false,
      selectedItems: selectedBacklogItems.value.map(({ title, category, suggestedHours, priority, reasoning }) => ({
        title,
        category,
        suggestedHours,
        priority,
        reasoning
      })),
      quickWins: [],
      mediumTasks: [],
      riskyTasks: []
    })

    const created = response.data?.data || []
    if (created.length > 0) {
      await Promise.all([
        workTaskStore.fetchTasks(currentProjectId.value, { reset: false }).catch(() => []),
        projectStore.fetchProjectDetails(currentProjectId.value, { force: true }).catch(() => null),
        projectStore.fetchAllProjects(true).catch(() => []),
        sprintStore.fetchSprints(currentProjectId.value, { force: true }).catch(() => [])
      ])
      notifyProjectRealtime('project-settings-updated', { source: 'ai-operational-review' })
    }

    ElMessage.success(response.data?.message || `Da tao ${created.length} work items`)
    repoStatus.value = `Da tao ${created.length} work items vao ${reviewTargetSprintLabel.value}.`
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Khong tao duoc reviewed AI backlog items')
  } finally {
    createBacklogLoading.value = ''
  }
}

const parseRepo = (url) => {
  const match = url.match(/github\.com\/([^/]+)\/([^/#?]+)/i)
  if (!match) return null
  return {
    owner: match[1],
    repo: match[2].replace(/\.git$/i, '')
  }
}

const notifyProjectRealtime = (type, payload = {}) => {
  const projectId = currentProjectId.value
  if (!projectId || !type) return

  const message = { projectId, source: 'ai-page', ...payload }
  broadcastAdminRealtime(type, message)
  signalRService.sendProjectEvent(`${projectId}`, type, message)
}

onMounted(() => {
  clearLegacyGitHubCredentialStorage()
  projectStore.fetchAllProjects().catch(() => [])
  if (currentProjectId.value) {
    sprintStore.fetchSprints(currentProjectId.value).catch(() => [])
    signalRService.startConnection(`${currentProjectId.value}`)
  }
  const saved = localStorage.getItem('sidebarPreferences')
  if (saved) {
    try {
      Object.assign(sidebarPreferences.value, JSON.parse(saved))
    } catch {
      // ignore malformed preferences
    }
  }

  const stashedRepoUrl = sessionStorage.getItem('sprinta-ai-repo-url')
  const stashedPrompt = sessionStorage.getItem('sprinta-ai-prefill-message')
  const stashedAnalysis = sessionStorage.getItem('sprinta-ai-repo-analysis')

  if (stashedRepoUrl) {
    repoForm.value.url = stashedRepoUrl
  }

  if (stashedPrompt) {
    userMessage.value = stashedPrompt
  }

  if (stashedAnalysis) {
    try {
      repoAnalysis.value = JSON.parse(stashedAnalysis)
      syncReviewSelectionFromAnalysis()
    } catch {
      repoAnalysis.value = null
    }
  }
})

watch(currentProjectId, (projectId) => {
  if (!projectId) {
    reviewTargetSprintId.value = ''
    return
  }

  sprintStore.fetchSprints(projectId, { force: true }).catch(() => [])
  signalRService.startConnection(`${projectId}`)
})

watch(repoAnalysis, () => {
  reviewTargetSprintId.value = ''
  syncReviewSelectionFromAnalysis()
})

onBeforeUnmount(() => {
  clearProgressTimer()
})

const handleSidebarSaved = (prefs) => {
  const next = { ...sidebarPreferences.value }
  if (prefs?.navItems) {
    prefs.navItems.forEach(item => {
      if (['recent', 'spaces', 'ai', 'audit', 'users'].includes(item.id)) {
        next[item.id] = item.checked
      }
    })
  }
  sidebarPreferences.value = next
  localStorage.setItem('sidebarPreferences', JSON.stringify(next))
}
</script>

<style scoped>
.ai-page-flex-wrapper {
  display: flex;
  height: calc(100vh - 64px);
  width: 100%;
  max-width: 1440px;
  margin: 0 auto;
  background: var(--bg-primary);
}

.ai-container {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
}

.ai-page-header {
  padding: 20px 32px 12px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.page-title {
  margin: 0;
  font-size: 24px;
  font-weight: 700;
}

.header-pill {
  padding: 4px 10px;
  border-radius: 999px;
  background: var(--bg-tertiary);
  color: var(--accent-color);
  font-size: 12px;
  font-weight: 600;
  border: 1px solid var(--border-color);
}

.repo-panel {
  margin: 0 32px 12px;
  padding: 16px;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  background: var(--bg-secondary);
}

.repo-analysis-preview {
  margin-top: 14px;
  padding-top: 14px;
  border-top: 1px solid var(--color-border);
}

.analysis-title {
  font-size: 14px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.analysis-summary {
  margin: 8px 0 12px;
  color: var(--color-text-secondary);
  line-height: 1.5;
}

.analysis-actions {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 14px;
  padding: 12px;
  border-radius: 8px;
  background: var(--bg-tertiary);
  border: 1px solid var(--border-color);
}

.analysis-project {
  display: grid;
  gap: 4px;
  color: var(--color-text-secondary);
  font-size: 13px;
}

.analysis-project strong {
  color: var(--color-text-primary);
}

.analysis-action-buttons {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 8px;
}

.analysis-columns {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
}

.operational-review-card {
  margin: 16px 0;
  padding: 16px;
  border: 1px solid var(--color-border);
  border-radius: 16px;
  background: color-mix(in srgb, var(--color-surface-elevated, #141722) 92%, #0ea5e9 8%);
}

.review-head {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  align-items: flex-start;
  margin-bottom: 12px;
}

.review-copy {
  margin: 6px 0 0;
  color: var(--color-text-secondary);
  font-size: 13px;
}

.review-stats {
  display: flex;
  gap: 12px;
}

.review-stat {
  min-width: 84px;
  padding: 10px 12px;
  border-radius: 12px;
  background: rgba(15, 23, 42, 0.45);
  border: 1px solid var(--color-border);
}

.review-stat-label {
  display: block;
  margin-bottom: 4px;
  color: var(--color-text-secondary);
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.review-controls {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  align-items: center;
  margin-bottom: 14px;
  flex-wrap: wrap;
}

.review-checkbox {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}

.review-cycle-picker {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 280px;
}

.review-cycle-picker span {
  color: var(--color-text-secondary);
  font-size: 13px;
}

.review-columns .analysis-col {
  min-height: 180px;
}

.review-item {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 8px 0;
  cursor: pointer;
}

.review-item-body {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.review-item-body small {
  color: var(--color-text-secondary);
}

.review-foot {
  display: flex;
  justify-content: space-between;
  gap: 18px;
  align-items: flex-start;
  margin-top: 14px;
  flex-wrap: wrap;
}

.review-test-plan {
  flex: 1;
  min-width: 260px;
}

.review-test-plan ul {
  margin: 8px 0 0;
  padding-left: 18px;
}

.analysis-col {
  padding: 12px;
  border-radius: 8px;
  background: var(--bg-tertiary);
}

.analysis-col-title {
  margin-bottom: 8px;
  font-size: 12px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}

.analysis-col ul {
  margin: 0;
  padding-left: 18px;
}

.analysis-col li {
  margin-bottom: 6px;
  color: var(--color-text-primary);
  line-height: 1.4;
}

.repo-head,
.repo-grid,
.repo-actions,
.quick-links,
.chat-row,
.input-box,
.thinking-step {
  display: flex;
}

.repo-head,
.thinking-step {
  align-items: center;
}

.repo-head {
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 12px;
}

.panel-title {
  font-weight: 700;
  color: var(--color-text-primary);
}

.panel-copy,
.repo-status {
  font-size: 13px;
  color: var(--color-text-secondary);
}

.repo-grid {
  gap: 10px;
  margin-bottom: 10px;
}

.repo-input {
  flex: 1;
  min-width: 0;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  background: var(--bg-tertiary);
  color: var(--text-primary);
  padding: 10px 12px;
  outline: none;
}
.repo-input:focus {
  border-color: var(--accent-color);
}

.repo-actions {
  gap: 10px;
}

.ghost-btn {
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: transparent;
  color: var(--color-text-primary);
  padding: 8px 12px;
  cursor: pointer;
}

.chat-history {
  flex: 1;
  padding: 20px 32px;
  display: flex;
  flex-direction: column;
  gap: 28px;
  overflow-y: auto;
}

.chat-row {
  gap: 12px;
  max-width: 70%;
  align-items: flex-start;
}

.chat-row.user {
  align-self: flex-end;
  flex-direction: row-reverse;
}

.bot-icon-circle,
.user-avatar-circle {
  width: 32px;
  height: 32px;
  border-radius: 6px;
  display: grid;
  place-items: center;
}

.bot-icon-circle {
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  color: var(--accent-color);
}

.user-avatar-circle {
  background: var(--accent-color);
  color: #ffffff;
  font-weight: 700;
}

.bubble {
  min-width: 120px;
  max-width: 100%;
  padding: 10px 14px;
  border-radius: 12px;
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  color: var(--text-primary);
  line-height: 1.5;
  font-size: 14px;
}

.bubble.primary {
  background: var(--accent-color);
  color: #ffffff;
  border-color: var(--accent-color);
}

.thinking-steps {
  margin-top: 12px;
  display: grid;
  gap: 6px;
}

.thinking-step {
  gap: 8px;
  color: var(--color-text-secondary);
  font-size: 12px;
}

.thinking-step.active {
  color: var(--color-text-primary);
}

.ai-chat-input-wrapper {
  padding: 0 32px 24px;
}

.input-box {
  align-items: center;
  gap: 10px;
  border: 1px solid var(--border-color);
  border-radius: 10px;
  background: var(--bg-tertiary);
  padding: 0 14px;
  transition: all 0.2s;
}
.input-box:focus-within {
  border-color: var(--accent-color);
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.2);
}

.input-box input {
  flex: 1;
  border: 0;
  background: transparent;
  color: var(--text-primary);
  padding: 12px 0;
  outline: none;
  font-size: 14px;
}

.attach-btn {
  color: var(--color-text-secondary);
}

.send-btn {
  border: 0;
  background: transparent;
  color: #0ea5e9;
  cursor: pointer;
}

.send-btn:disabled,
.ghost-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.ai-disclaimer {
  margin-top: 10px;
  color: var(--color-text-secondary);
  font-size: 12px;
}

.ai-details-panel {
  width: 300px;
  padding: 24px 20px;
  border-left: 1px solid var(--border-color);
  background: var(--bg-secondary);
}

.section-label {
  color: var(--color-text-secondary);
  font-size: 12px;
  margin-bottom: 10px;
}

.section-title {
  font-size: 12px;
  font-weight: 700;
  color: var(--color-text-secondary);
  margin-bottom: 12px;
}

.quick-links {
  flex-direction: column;
  gap: 10px;
}

.q-link {
  width: 100%;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  background: var(--bg-tertiary);
  color: var(--text-primary);
  padding: 10px 12px;
  text-align: left;
  cursor: pointer;
  transition: all 0.2s;
}
.q-link:hover {
  background: var(--hover-bg);
  border-color: var(--accent-color);
}

.mt-30 {
  margin-top: 30px;
}

.sidebar-copy {
  line-height: 1.6;
}

.upgrade-card-wrapper {
  margin-top: 30px;
}

.upgrade-card {
  border: 1px solid var(--border-color);
  border-radius: 8px;
  background: var(--bg-tertiary);
  padding: 18px;
}

.plan-label {
  color: #0ea5e9;
  font-size: 12px;
  font-weight: 700;
  margin-bottom: 8px;
}

.plan-desc {
  color: var(--color-text-secondary);
  font-size: 13px;
  line-height: 1.6;
  margin-bottom: 12px;
}

.btn-upgrade {
  border: 0;
  border-radius: 6px;
  background: #0ea5e9;
  color: #ffffff;
  padding: 10px 14px;
  cursor: pointer;
}

@media (max-width: 1100px) {
  .ai-page-flex-wrapper {
    width: 100%;
    margin: 0;
    height: auto;
    flex-direction: column;
  }

  .ai-details-panel {
    width: 100%;
    border-left: 0;
    border-top: 1px solid var(--color-border);
  }

  .repo-grid {
    flex-direction: column;
  }

  .analysis-actions {
    flex-direction: column;
  }

  .analysis-action-buttons {
    justify-content: flex-start;
  }
}
</style>
