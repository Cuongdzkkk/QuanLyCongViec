<template>
  <div>
    <div class="ai-page-flex-wrapper">
      <aside class="ai-page-history" :class="{ 'is-open': aiConversationStore.historyVisible }" aria-label="Lịch sử trò chuyện">
        <div class="ai-page-history-head">
          <div><span class="eyebrow">SPRINTA AI</span><strong>Lịch sử trò chuyện</strong></div>
          <button type="button" aria-label="Đóng lịch sử" title="Đóng lịch sử" @click="aiConversationStore.historyVisible = false">×</button>
        </div>
        <input v-model="conversationSearch" type="search" placeholder="Tìm cuộc trò chuyện" aria-label="Tìm cuộc trò chuyện" />
        <p v-if="conversationLoading" class="history-empty">Đang tải...</p>
        <p v-else-if="!conversations.length" class="history-empty">Chưa có cuộc trò chuyện đã lưu.</p>
        <button v-for="conversation in filteredConversations" :key="conversation.id" type="button" class="ai-page-history-item" :class="{ active: conversation.id === currentConversationId }" @click="openConversation(conversation.id)">
          <strong>{{ conversation.title }}</strong>
          <small>{{ formatConversationDate(conversation.updatedAt) }}</small>
        </button>
        <button v-if="conversationHasMore" class="history-more" type="button" @click="loadConversations(false)">Tải thêm</button>
      </aside>
      <div class="ai-container">
        <div class="ai-page-header">
          <div class="header-left">
            <h2 class="page-title">Trợ lý AI</h2>
            <span class="header-pill">Workspace assistant</span>
            <span v-if="aiUsage" class="credit-pill">
              {{ aiUsage.usedCredits }}/{{ aiUsage.includedCredits }} credits · còn {{ aiUsage.remainingCredits }}
            </span>
            <button v-if="aiUsage" class="credit-buy-inline" type="button" @click="openAiCreditPurchase">Mua thêm</button>
          </div>
          <div class="header-actions">
            <button class="header-icon-btn" type="button" title="Cuộc trò chuyện mới" aria-label="Cuộc trò chuyện mới" @click="startNewConversation"><i class="fa-solid fa-plus"></i></button>
            <button class="header-icon-btn mobile-history-toggle" type="button" title="Mở lịch sử" aria-label="Mở lịch sử" @click="toggleConversationHistory"><i class="fa-solid fa-clock-rotate-left"></i></button>
            <button class="return-floating-btn" type="button" @click="returnToFloating"><i class="fa-solid fa-arrow-left"></i> Về bảng AI</button>
          </div>
        </div>

        <details class="workspace-tools">
          <summary><span>Workspace tools</span><small>GitHub analysis và backlog review</small></summary>
        <div class="repo-panel">
          <div class="repo-head">
            <div>
              <div class="panel-title">GitHub repo analysis</div>
              <div class="panel-copy">Chọn repo, đọc nhanh metadata và gửi một prompt phân tích task vào chat box.</div>
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
                      <small>{{ item.suggestedHours }}h · P{{ item.priority }}</small>
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
                      <small>{{ item.suggestedHours }}h · P{{ item.priority }}</small>
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
                      <small>{{ item.suggestedHours }}h · P{{ item.priority }}</small>
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
                    {{ item.title }} · {{ item.suggestedHours }}h
                  </li>
                </ul>
              </div>
              <div class="analysis-col">
                <div class="analysis-col-title">Medium tasks</div>
                <ul>
                  <li v-for="item in repoAnalysis.mediumTasks" :key="`medium-${item.title}`">
                    {{ item.title }} · {{ item.suggestedHours }}h
                  </li>
                </ul>
              </div>
              <div class="analysis-col">
                <div class="analysis-col-title">Risky tasks</div>
                <ul>
                  <li v-for="item in repoAnalysis.riskyTasks" :key="`risk-${item.title}`">
                    {{ item.title }} · {{ item.suggestedHours }}h
                  </li>
                </ul>
              </div>
            </div>
          </div>
        </div>
        </details>

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
              <div v-if="msg.actions?.length" class="page-action-list" aria-label="AI action previews">
                <article v-for="(action, actionIndex) in writeActions(msg.actions)" :key="`${action.type}-${actionIndex}`" class="page-action-card">
                  <div class="page-action-head"><div><span>CẦN BẠN XÁC NHẬN</span><strong>{{ actionLabel(action.type) }}</strong></div><em>{{ actionStatusLabel(action) }}</em></div>
                  <p>{{ action.description || 'AI đề xuất một thay đổi dựa trên yêu cầu của bạn.' }}</p>
                  <dl><template v-for="detail in actionDetails(action)" :key="detail.label"><dt>{{ detail.label }}</dt><dd>{{ detail.value }}</dd></template></dl>
                  <p v-if="action.error" class="page-action-error" role="alert">{{ action.error }}</p>
                  <p v-if="action.result?.message" class="page-action-success" role="status">{{ action.result.message }}</p>
                  <div class="page-action-controls">
                    <button type="button" class="cancel-action" :disabled="action.loading || action.uiStatus === 'success'" @click="cancelPageAction(action)">Hủy</button>
                    <button type="button" class="confirm-action" :disabled="action.loading || action.uiStatus === 'success'" @click="confirmPageAction(action)">{{ action.loading ? 'Đang xử lý...' : action.uiStatus === 'success' ? 'Đã thực hiện' : 'Xác nhận' }}</button>
                  </div>
                </article>
                <small v-if="msg.actions.some(action => isReadOnlyAction(action.type, action.requiresConfirmation))" class="page-read-note">Đã đọc dữ liệu hiện tại để bổ sung kết quả.</small>
              </div>
            </div>
            <div v-if="msg.role === 'user'" class="user-avatar-circle">{{ userInitials }}</div>
          </div>
        </div>

        <div class="ai-chat-input-wrapper">
          <div class="input-box">
            <button class="full-composer-icon" type="button" title="Mở công cụ attachment" aria-label="Mở công cụ attachment" @click="returnToFloating"><i class="fa-solid fa-plus"></i></button>
            <textarea
              ref="fullComposerRef"
              v-model="userMessage"
              rows="1"
              placeholder="Hoi SprintA AI bat cu dieu gi..."
              :disabled="isLoading"
              @input="resizeFullComposer"
              @keydown="handleComposerKeydown"
            ></textarea>
            <button class="full-composer-icon" type="button" title="Mở nhập bằng giọng nói" aria-label="Mở nhập bằng giọng nói" @click="returnToFloating"><i class="fa-solid fa-microphone"></i></button>
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
          <p class="text-muted sidebar-copy">Credits và trạng thái xử lý được cập nhật theo tài khoản của bạn. Nếu AI chậm, tiến trình sẽ hiển thị ngay trong cuộc trò chuyện.</p>
        </div>

        <div class="upgrade-card-wrapper">
          <div class="upgrade-card">
            <div class="plan-label">AI CREDITS</div>
            <div class="plan-desc">Xem các gói và luồng mua credits thật của SprintA.</div>
            <button class="btn-upgrade" type="button" @click="openAiCreditPurchase">Mua thêm AI credits</button>
          </div>
        </div>
      </aside>
    </div>

    <CustomizeSidebarModal :visible="showCustomizeModal" @update:visible="showCustomizeModal = $event" @saved="handleSidebarSaved" />
  </div>
</template>

<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
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
import { useAiConversationStore } from '@/store/useAiConversationStore'
import { useAiPetStore } from '@/store/useAiPetStore'
import { isComposerSendKey, writeActionsOnly } from '@/utils/aiWorkspace'

const router = useRouter()
const aiConversationStore = useAiConversationStore()
const aiPetStore = useAiPetStore()
const projectStore = useProjectStore()
const workTaskStore = useWorkTaskStore()
const sprintStore = useSprintStore()
const i18nStore = useI18nStore()
const currentUser = JSON.parse(localStorage.getItem('user') || '{}')
const showCustomizeModal = ref(false)
const sidebarPreferences = ref({ audit: true, users: true })

const userMessage = ref('')
const fullComposerRef = ref(null)
const isLoading = ref(false)
const repoLoading = ref(false)
const repoStatus = ref('')
const repoAnalysis = ref(null)
const createBacklogLoading = ref('')
const aiUsage = ref(null)
const selectedBacklogKeys = ref([])
const reviewTargetSprintId = ref('')
const repoForm = ref({
  url: '',
  token: ''
})

const chatHistory = computed({
  get: () => aiConversationStore.messages,
  set: value => { aiConversationStore.messages = value }
})
const conversations = computed(() => aiConversationStore.conversations)
const filteredConversations = computed(() => aiConversationStore.filteredConversations)
const currentConversationId = computed(() => aiConversationStore.currentConversationId)
const conversationLoading = computed(() => aiConversationStore.loading)
const conversationHasMore = computed(() => aiConversationStore.hasMore)
const conversationSearch = computed({
  get: () => aiConversationStore.search,
  set: value => { aiConversationStore.search = value }
})
const currentWorkspaceId = computed(() => currentProjectRecord.value?.workspaceId || currentProjectRecord.value?.WorkspaceId || workTaskStore.resolveWorkspaceId(currentProjectId.value) || null)
const formatConversationDate = value => value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)) : ''

const breakdownProgressSteps = [
  'Dang phan tich task',
  'Dang truy van dich vu AI',
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

const loadConversations = async (reset = true) => {
  try {
    await aiConversationStore.loadConversations({ workspaceId: currentWorkspaceId.value, reset })
  } catch {
    ElMessage.warning('Không thể tải lịch sử trò chuyện.')
  }
}

const toggleConversationHistory = () => {
  aiConversationStore.historyVisible = !aiConversationStore.historyVisible
  if (aiConversationStore.historyVisible) loadConversations(true)
}

const startNewConversation = () => aiConversationStore.startNewConversation()
const openConversation = async id => {
  try {
    await aiConversationStore.openConversation(id)
  } catch {
    ElMessage.error('Không thể mở cuộc trò chuyện.')
  }
}
const returnToFloating = async () => {
  aiPetStore.setPanelOpen(true)
  await router.back()
}
const openAiCreditPurchase = () => router.push('/#pricing')

const readOnlyActionTypes = new Set([
  'summarize_dashboard', 'summarize_project', 'list_overdue_tasks', 'get_workload',
  'explain_report', 'summarize_page', 'summarize_intakes', 'suggest_view_filter',
  'list_work_items', 'list_cycles', 'list_modules', 'list_pages', 'list_views',
  'list_intakes', 'list_pending_intakes', 'analyze_priority_distribution',
  'analyze_status_distribution', 'analyze_workload', 'identify_project_risks',
  'refresh_report', 'export_report_csv', 'summarize_report'
])
const isReadOnlyAction = (type, requiresConfirmation) => requiresConfirmation === false || readOnlyActionTypes.has(String(type || '').toLowerCase())
const writeActions = actions => writeActionsOnly(actions, isReadOnlyAction)
const actionPayload = action => action?.payload || {}
const payloadValue = (action, ...keys) => {
  const payload = actionPayload(action)
  const key = keys.find(item => payload[item] !== undefined && payload[item] !== null && `${payload[item]}`.trim() !== '')
  return key ? payload[key] : ''
}
const actionLabel = type => ({
  create_task: 'Tạo task mới', create_project: 'Tạo project mới', create_goal: 'Tạo mục tiêu mới',
  update_task_status: 'Cập nhật trạng thái task', update_task_priority: 'Cập nhật độ ưu tiên',
  update_task_due_date: 'Cập nhật hạn task', assign_task: 'Giao task cho thành viên',
  add_comment: 'Thêm bình luận', create_cycle: 'Tạo chu kỳ mới', create_module: 'Tạo mô-đun mới',
  create_page: 'Tạo tài liệu mới', create_view: 'Tạo bộ lọc đã lưu', create_intake_request: 'Tạo yêu cầu mới'
}[String(type || '').toLowerCase()] || 'Thực hiện thay đổi')
const actionDetails = action => {
  const type = String(action?.type || '').toLowerCase()
  const details = []
  const add = (label, value) => { if (value !== '' && value !== null && value !== undefined) details.push({ label, value: `${value}` }) }
  if (type === 'create_task') {
    add('Tiêu đề', payloadValue(action, 'title', 'taskTitle'))
    add('Hạn', payloadValue(action, 'dueDate', 'plannedEndDate'))
    add('Ưu tiên', payloadValue(action, 'priority'))
  } else if (type === 'create_project' || type === 'create_goal') {
    add('Tên', payloadValue(action, 'name', 'projectName', 'title'))
    add('Mô tả', payloadValue(action, 'description'))
  } else if (type === 'update_task_status') {
    add('Task', payloadValue(action, 'taskTitle', 'title'))
    add('Trạng thái mới', payloadValue(action, 'statusName', 'status'))
  } else if (type === 'assign_task') {
    add('Task', payloadValue(action, 'taskTitle', 'title'))
    add('Người nhận', payloadValue(action, 'assigneeName', 'assigneeEmail', 'assignee'))
  } else if (type === 'update_task_priority' || type === 'update_task_due_date') {
    add('Task', payloadValue(action, 'taskTitle', 'title'))
    add(type === 'update_task_priority' ? 'Độ ưu tiên mới' : 'Hạn mới', payloadValue(action, type === 'update_task_priority' ? 'priority' : 'dueDate'))
  } else if (type === 'add_comment') {
    add('Đối tượng', payloadValue(action, 'entityType'))
    add('Nội dung', payloadValue(action, 'content'))
  } else {
    add('Tên', payloadValue(action, 'name', 'title'))
    add('Dự án', payloadValue(action, 'projectName'))
  }
  return details
}
const actionStatusLabel = action => ({ pending: 'Chờ xác nhận', loading: 'Đang xử lý', success: 'Thành công', cancelled: 'Đã hủy', error: 'Thất bại' }[action.uiStatus || 'pending'] || 'Chờ xác nhận')

const confirmPageAction = async action => {
  if (!action || action.loading || action.uiStatus === 'success' || action.uiStatus === 'cancelled') return
  action.loading = true
  action.uiStatus = 'loading'
  try {
    action.idempotencyKey ||= `${action.type}-${crypto.randomUUID()}`
    if (!action.serverActionId) {
      const preview = await axiosClient.post('/ai/actions/preview', {
        type: action.type, idempotencyKey: action.idempotencyKey,
        workspaceId: currentWorkspaceId.value || null, projectId: currentProjectId.value || actionPayload(action).projectId || null,
        payload: actionPayload(action)
      })
      action.serverActionId = preview.data?.data?.actionId
    }
    if (!action.serverActionId) throw new Error('Không thể tạo action preview.')
    const response = await axiosClient.post(`/ai/actions/${action.serverActionId}/confirm`)
    const payload = response.data?.data ?? response.data
    action.result = payload?.result ?? payload
    action.uiStatus = 'success'
    ElMessage.success('AI đã thực hiện thay đổi thành công.')
    await aiConversationStore.persistConversation()
  } catch (error) {
    action.uiStatus = 'error'
    action.error = error.response?.data?.message || error.message || 'Không thể thực hiện action.'
    ElMessage.error(action.error)
  } finally {
    action.loading = false
  }
}

const cancelPageAction = async action => {
  if (!action || action.loading || action.uiStatus === 'success') return
  if (action.serverActionId) await axiosClient.post(`/ai/actions/${action.serverActionId}/cancel`).catch(() => {})
  action.uiStatus = 'cancelled'
  await aiConversationStore.persistConversation()
}

const handleComposerKeydown = event => {
  if (!isComposerSendKey(event)) return
  event.preventDefault()
  sendMessage()
}

const resizeFullComposer = () => {
  const textarea = fullComposerRef.value
  if (!textarea) return
  textarea.style.height = 'auto'
  textarea.style.height = `${Math.min(textarea.scrollHeight, 150)}px`
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
    const conversationId = await aiConversationStore.ensureConversation({
      workspaceId: currentWorkspaceId.value,
      firstMessage: outgoing
    })
    const history = chatHistory.value
      .filter(item => !item.isTyping)
      .slice(-10)
      .map(item => ({ role: item.role === 'bot' ? 'assistant' : 'user', content: item.content }))

    const response = await axiosClient.post('/ai/context-chat', {
      conversationId,
      route: '/ai-assistant',
      projectId: currentProjectId.value || null,
      workspaceId: currentWorkspaceId.value || null,
      message: outgoing,
      pageContext: { pageType: 'ai-assistant', currentView: 'conversation', visibleTaskIds: [], visibleStatuses: [], filters: {}, extra: { history } }
    })

    clearProgressTimer()
    chatHistory.value.pop()
    const payload = response.data?.data ?? response.data
    const message = payload?.answer || payload?.message || response.data?.message || (i18nStore.locale === 'en'
      ? 'Sorry, AI did not return content. Please try another request.'
      : 'R\u1ea5t ti\u1ebfc, AI kh\u00f4ng ph\u1ea3n h\u1ed3i n\u1ed9i dung. B\u1ea1n c\u00f3 th\u1ec3 th\u1eed l\u1ea1i v\u1edbi c\u00e2u h\u1ecfi kh\u00e1c.')
    chatHistory.value.push({
      role: 'bot',
      content: message,
      warnings: payload?.warnings || [],
      actions: (payload?.actions || []).map(action => ({
        ...action,
        type: String(action.type || '').toLowerCase(),
        payload: action.payload || {},
        uiStatus: 'pending',
        loading: false,
        error: '',
        result: null
      }))
    })
    await aiConversationStore.persistConversation()

  } catch (error) {
    clearProgressTimer()
    chatHistory.value.pop()
    const message = error.response?.data?.message || error.response?.data?.error || error.message || 'Lỗi kết nối'
    let friendlyMessage = `AI không thể xử lý yêu cầu lúc này: ${message}`
    
    if (message.toLowerCase().includes('quota') || message.toLowerCase().includes('limit') || error.response?.status === 429) {
      friendlyMessage = 'Bạn đã đạt giới hạn sử dụng AI trong tháng này. Vui lòng thử lại khi hạn mức được làm mới.'
    } else if (message.toLowerCase().includes('key') || message.toLowerCase().includes('auth')) {
      friendlyMessage = 'Lỗi cấu hình dịch vụ AI. Vui lòng liên hệ quản trị viên để kiểm tra lại hệ thống.'
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

    ElMessage.success(response.data?.message || `Đã tạo ${created.length} công việc`)
    repoStatus.value = `Đã tạo ${created.length} công việc vào dự án ${activeProjectName.value}.`
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

    ElMessage.success(response.data?.message || `Đã tạo ${created.length} công việc`)
    repoStatus.value = `Đã tạo ${created.length} công việc vào ${reviewTargetSprintLabel.value}.`
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

const loadAiUsage = async () => {
  try {
    aiUsage.value = (await axiosClient.get('/ai/usage-summary')).data?.data || null
  } catch {
    aiUsage.value = null
  }
}

onMounted(() => {
  clearLegacyGitHubCredentialStorage()
  projectStore.fetchAllProjects().catch(() => [])
  loadAiUsage()
  loadConversations(true)
  nextTick(resizeFullComposer)
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
  flex-wrap: wrap;
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

.credit-pill {
  margin-left: auto;
  padding: 5px 10px;
  border: 1px solid var(--border-color);
  border-radius: 999px;
  background: var(--bg-secondary);
  color: var(--text-secondary);
  font-size: 12px;
  font-weight: 600;
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

.ai-page-flex-wrapper {
  max-width: none;
  height: calc(100dvh - 64px);
  gap: 0;
  background: var(--color-bg);
}

.ai-page-history {
  display: flex;
  flex: 0 0 250px;
  flex-direction: column;
  gap: 10px;
  min-width: 0;
  padding: 22px 14px;
  border-right: 1px solid var(--color-border);
  background: var(--color-surface);
}

.ai-page-history-head,
.header-actions,
.ai-page-history-head > div { display: flex; align-items: center; }
.ai-page-history-head { justify-content: space-between; gap: 8px; }
.ai-page-history-head > div { align-items: flex-start; flex-direction: column; gap: 3px; min-width: 0; }
.ai-page-history-head strong { font-size: 13px; }
.ai-page-history-head button { width: 32px; height: 32px; display: none; border: 1px solid var(--color-border); border-radius: 8px; background: transparent; color: var(--color-text-muted); font-size: 20px; cursor: pointer; }
.eyebrow { color: var(--color-accent); font-size: 10px; font-weight: 850; letter-spacing: .08em; }
.ai-page-history > input { min-height: 38px; padding: 8px 10px; border: 1px solid var(--color-border); border-radius: 8px; background: var(--color-bg); color: var(--color-text-primary); }
.history-empty { margin: 12px 4px; color: var(--color-text-muted); font-size: 12px; line-height: 1.5; }
.ai-page-history-item { display: grid; gap: 4px; width: 100%; min-height: 50px; padding: 9px 10px; border: 1px solid transparent; border-radius: 8px; background: transparent; color: var(--color-text-primary); text-align: left; cursor: pointer; }
.ai-page-history-item:hover, .ai-page-history-item.active { border-color: color-mix(in srgb, var(--color-accent) 35%, var(--color-border)); background: var(--sa-primary-soft); }
.ai-page-history-item strong, .ai-page-history-item small { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.ai-page-history-item strong { font-size: 12px; }
.ai-page-history-item small { color: var(--color-text-muted); font-size: 10px; }
.history-more { min-height: 34px; border: 1px solid var(--color-border); border-radius: 8px; background: transparent; color: var(--color-text-secondary); cursor: pointer; }

.ai-container { display: flex; flex: 1 1 auto; flex-direction: column; min-width: 0; max-width: none; padding: 24px clamp(18px, 4vw, 58px) 0; background: var(--color-bg); }
.ai-page-header { flex: 0 0 auto; align-items: center; min-height: 52px; margin-bottom: 16px; }
.header-left { min-width: 0; }
.credit-buy-inline { min-height: 30px; padding: 0 9px; border: 1px solid var(--color-border); border-radius: 8px; background: transparent; color: var(--color-accent); font-size: 11px; font-weight: 800; cursor: pointer; }
.credit-buy-inline:hover, .credit-buy-inline:focus-visible { border-color: var(--color-accent); background: var(--sa-primary-soft); outline: none; }
.header-actions { gap: 7px; }
.header-icon-btn, .full-composer-icon { width: 40px; height: 40px; display: grid; place-items: center; border: 1px solid var(--color-border); border-radius: 10px; background: var(--color-surface); color: var(--color-text-secondary); cursor: pointer; }
.header-icon-btn:hover, .header-icon-btn:focus-visible, .full-composer-icon:hover, .full-composer-icon:focus-visible { border-color: var(--color-accent); color: var(--color-accent); outline: none; }
.return-floating-btn { min-height: 40px; padding: 0 12px; border: 1px solid var(--color-border); border-radius: 10px; background: var(--color-surface); color: var(--color-text-primary); font-weight: 750; cursor: pointer; }
.return-floating-btn:hover, .return-floating-btn:focus-visible { border-color: var(--color-accent); color: var(--color-accent); outline: none; }
.workspace-tools { flex: 0 0 auto; margin-bottom: 14px; }
.workspace-tools > summary { display: flex; align-items: baseline; justify-content: space-between; gap: 12px; padding: 10px 12px; border: 1px solid var(--color-border); border-radius: 10px; color: var(--color-text-primary); cursor: pointer; }
.workspace-tools > summary small { color: var(--color-text-muted); }
.workspace-tools[open] > summary { border-radius: 10px 10px 0 0; }
.workspace-tools .repo-panel { border-top: 0; border-radius: 0 0 10px 10px; }
.chat-history { flex: 1 1 auto; min-height: 0; max-width: 900px; width: 100%; margin: 0 auto; padding: 4px 0 18px; overflow-y: auto; }
.chat-row { align-items: flex-start; gap: 10px; margin: 0 0 14px; }
.chat-row.user { justify-content: flex-end; }
.chat-row .bubble { max-width: min(760px, 88%); padding: 12px 14px; border: 1px solid var(--color-border); border-radius: 14px; background: var(--color-surface); color: var(--color-text-primary); line-height: 1.6; overflow-wrap: anywhere; }
.chat-row.user .bubble { border-color: color-mix(in srgb, var(--color-accent) 35%, var(--color-border)); background: var(--sa-primary-soft); }
.ai-chat-input-wrapper { position: sticky; bottom: 0; width: min(900px, 100%); margin: 0 auto; padding: 12px 0 18px; background: linear-gradient(var(--color-bg), var(--color-bg) 40%); }
.input-box { display: flex; align-items: center; gap: 7px; min-height: 58px; padding: 6px 7px 6px 10px; border: 1px solid var(--color-border); border-radius: 15px; background: var(--color-surface); box-shadow: 0 10px 28px rgb(15 35 60 / 0.08); }
.input-box textarea { flex: 1; min-width: 0; max-height: 150px; min-height: 44px; padding: 10px 6px; resize: none; border: 0; outline: none; background: transparent; color: var(--color-text-primary); font: inherit; line-height: 1.45; }
.input-box:focus-within { border-color: var(--color-accent); }
.input-box .send-btn { width: 44px; height: 44px; flex: 0 0 44px; border-radius: 12px; }
.ai-disclaimer { margin: 7px 4px 0; color: var(--color-text-muted); font-size: 11px; }
.page-action-list { display: grid; gap: 10px; margin-top: 12px; }
.page-action-card { padding: 12px; border: 1px solid color-mix(in srgb, var(--color-accent) 38%, var(--color-border)); border-radius: 12px; background: color-mix(in srgb, var(--color-surface-hover) 55%, var(--color-surface)); }
.page-action-head, .page-action-controls { display: flex; align-items: center; justify-content: space-between; gap: 10px; }
.page-action-head span { display: block; margin-bottom: 3px; color: var(--color-accent); font-size: 9px; font-weight: 850; letter-spacing: .08em; }
.page-action-head strong { font-size: 13px; }
.page-action-head em { color: var(--color-text-muted); font-size: 10px; font-style: normal; font-weight: 750; }
.page-action-card > p { margin: 9px 0; color: var(--color-text-secondary); font-size: 12px; }
.page-action-card dl { display: grid; grid-template-columns: max-content minmax(0, 1fr); gap: 4px 10px; margin: 0 0 10px; font-size: 11px; }
.page-action-card dt { color: var(--color-text-muted); }
.page-action-card dd { margin: 0; overflow-wrap: anywhere; }
.cancel-action, .confirm-action { min-height: 36px; padding: 0 12px; border-radius: 8px; font-size: 11px; font-weight: 800; cursor: pointer; }
.cancel-action { border: 1px solid var(--color-border); background: transparent; color: var(--color-text-secondary); }
.confirm-action { border: 1px solid var(--color-accent); background: var(--color-accent); color: #fff; }
.cancel-action:disabled, .confirm-action:disabled { cursor: not-allowed; opacity: .55; }
.page-action-error { color: var(--color-danger, #dc2626) !important; }
.page-action-success, .page-read-note { color: var(--color-success, #16803c) !important; }

@media (max-width: 1100px) {
  .ai-page-flex-wrapper { height: calc(100dvh - 56px); }
  .ai-page-history { display: none; position: fixed; inset: 0 auto 0 0; z-index: 20; width: min(300px, 86vw); box-shadow: 16px 0 40px rgb(15 35 60 / 0.2); }
  .ai-page-history.is-open { display: flex; }
  .ai-page-history-head button { display: grid; place-items: center; }
  .ai-container { padding: 16px 16px 0; }
  .ai-details-panel { display: none; }
}

@media (max-width: 520px) {
  .ai-page-header { align-items: flex-start; flex-direction: column; gap: 12px; }
  .header-actions { width: 100%; }
  .return-floating-btn { margin-left: auto; }
  .chat-row .bubble { max-width: 92%; }
  .ai-chat-input-wrapper { padding-bottom: calc(12px + env(safe-area-inset-bottom)); }
  .input-box { gap: 4px; }
  .full-composer-icon, .input-box .send-btn { width: 40px; height: 40px; flex-basis: 40px; }
}
</style>
