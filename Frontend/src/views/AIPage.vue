<template>
  <div>
    <div class="ai-page-flex-wrapper">
      <aside class="ai-page-history" :class="{ 'is-open': aiConversationStore.historyVisible }" aria-label="Lịch sử trò chuyện">
        <div class="ai-page-history-head">
          <div>
            <span class="eyebrow">SPRINTA AI</span>
            <strong>Lịch sử trò chuyện</strong>
            <small>Các cuộc trò chuyện của bạn</small>
          </div>
          <button type="button" aria-label="Đóng lịch sử" title="Đóng lịch sử" @click="aiConversationStore.historyVisible = false">×</button>
        </div>
        <button class="history-new-button" type="button" @click="startNewConversation">
          <i class="fa-solid fa-plus" aria-hidden="true"></i>
          <span>Cuộc trò chuyện mới</span>
        </button>
        <label class="history-search">
          <i class="fa-solid fa-magnifying-glass" aria-hidden="true"></i>
          <input v-model="conversationSearch" type="search" placeholder="Tìm cuộc trò chuyện" aria-label="Tìm cuộc trò chuyện" />
        </label>
        <p v-if="conversationLoading" class="history-empty">Đang tải...</p>
        <p v-else-if="!conversations.length" class="history-empty">Chưa có cuộc trò chuyện đã lưu.</p>
        <p v-else-if="!filteredConversations.length" class="history-empty">Không tìm thấy cuộc trò chuyện phù hợp.</p>
        <div v-else class="history-groups">
          <section v-for="group in groupedConversations" :key="group.key" class="history-group">
            <h3>{{ group.label }}</h3>
            <button v-for="conversation in group.items" :key="conversation.id" type="button" class="ai-page-history-item" :class="{ active: conversation.id === currentConversationId }" @click="openConversation(conversation.id)">
              <span class="history-item-icon"><i class="fa-regular fa-message" aria-hidden="true"></i></span>
              <span class="history-item-content">
                <strong>{{ conversation.title }}</strong>
                <small>{{ formatConversationDate(conversation.updatedAt) }}</small>
              </span>
              <i class="fa-solid fa-chevron-right history-item-arrow" aria-hidden="true"></i>
            </button>
          </section>
        </div>
        <button v-if="conversationHasMore" class="history-more" type="button" @click="loadConversations(false)">Tải thêm</button>
      </aside>
      <div class="ai-container">
        <div class="ai-page-header">
          <div class="ai-page-header-top">
            <div class="header-left">
              <div class="page-title-line">
                <h2 class="page-title">Trợ lý AI</h2>
                <span class="header-pill">Trợ lý công việc</span>
              </div>
              <span class="page-subtitle">Làm việc với dữ liệu thật trong đúng phạm vi bạn đang chọn.</span>
            </div>
            <div class="header-actions">
              <button class="header-icon-btn" type="button" title="Cuộc trò chuyện mới" aria-label="Cuộc trò chuyện mới" @click="startNewConversation"><i class="fa-solid fa-plus"></i></button>
              <button class="header-icon-btn mobile-history-toggle" type="button" title="Mở lịch sử" aria-label="Mở lịch sử" @click="toggleConversationHistory"><i class="fa-solid fa-clock-rotate-left"></i></button>
              <button class="return-floating-btn" type="button" @click="returnToFloating"><i class="fa-solid fa-arrow-left"></i> Về bảng AI</button>
            </div>
          </div>
          <div class="ai-context-bar">
            <div class="context-summary">
              <span class="context-kicker"><i class="fa-solid fa-crosshairs" aria-hidden="true"></i> PHẠM VI AI</span>
              <strong>{{ activeWorkspaceName }} <span aria-hidden="true">·</span> {{ activeProjectName }}</strong>
              <small>Workspace/project được chọn cho các thao tác AI.</small>
            </div>
            <div class="context-controls">
              <span class="workspace-context-pill" :title="currentWorkspaceId ? `Workspace ${currentWorkspaceId}` : 'Chưa chọn workspace'">
                <i class="fa-solid fa-layer-group" aria-hidden="true"></i>
                <span>Phạm vi hiện tại</span>
              </span>
              <label v-if="workspaceOptions.length" class="workspace-selector">
                <i class="fa-solid fa-building" aria-hidden="true"></i>
                <span class="sr-only">Workspace</span>
                <select v-model="selectedWorkspaceId" aria-label="Chọn workspace" @change="handleWorkspaceChange">
                  <option v-for="workspace in workspaceOptions" :key="workspace.id || workspace.Id" :value="workspace.id || workspace.Id">
                    {{ workspace.name || workspace.Name }}
                  </option>
                </select>
              </label>
              <label class="workspace-selector project-selector">
                <i class="fa-solid fa-folder-tree" aria-hidden="true"></i>
                <span class="sr-only">Project</span>
                <select v-model="selectedProjectId" aria-label="Chọn project" @change="handleProjectChange">
                  <option value="">Chọn project</option>
                  <option v-for="project in availableProjects" :key="project.id" :value="project.id">
                    {{ project.name }}
                  </option>
                </select>
              </label>
              <span v-if="aiUsage" class="credit-pill">
                {{ aiRemainingCredits }} credits còn lại
              </span>
              <button v-if="aiUsage" class="credit-buy-inline" type="button" @click="openAiCreditPurchase">Mua thêm</button>
            </div>
            <div class="current-page-summary">
              <span>TRANG HIỆN TẠI</span>
              <strong>{{ currentPageLabel }}</strong>
            </div>
          </div>
        </div>

        <details v-if="hasConnectedGithubIntegration" class="workspace-tools">
          <summary><span>Công cụ workspace</span><small>Phân tích GitHub và review backlog</small></summary>
        <div class="repo-panel">
          <div class="repo-head">
            <div>
              <div class="panel-title">Phân tích repo GitHub</div>
              <div class="panel-copy">Chọn repo, đọc nhanh metadata và gửi prompt phân tích task vào khung chat.</div>
            </div>
            <button class="ghost-btn" type="button" :disabled="repoLoading" @click="analyzeRepository">Phân tích repo</button>
          </div>
          <div class="repo-grid">
            <input v-model="repoForm.url" type="text" class="repo-input" placeholder="https://github.com/owner/repo" />
            <input v-model="repoForm.token" type="password" class="repo-input" placeholder="GitHub token (optional)" />
          </div>
          <div class="repo-actions">
            <button class="ghost-btn" type="button" @click="useQuickPrompt('Phân rã task sau thành 3-5 subtask rõ ràng, có test và bàn giao.')">
              Chèn lệnh breakdown
            </button>
            <button class="ghost-btn" type="button" :disabled="repoLoading" @click="prepareBreakdownPrompt">
              Mẫu prompt phân rã task
            </button>
          </div>
          <p v-if="repoStatus" class="repo-status">{{ repoStatus }}</p>
          <div v-if="repoAnalysis" class="repo-analysis-preview">
            <div class="analysis-title">{{ repoAnalysis.repository }}</div>
            <p class="analysis-summary">{{ repoAnalysis.summary }}</p>
            <div class="analysis-actions">
              <div class="analysis-project">
                <strong>Project tạo task:</strong>
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
          <AiMessage
            v-for="(msg, idx) in chatHistory"
            :key="`${msg.role}-${idx}`"
            :message="msg"
            :profile-initials="userInitials"
            @preview-attachment="openAttachmentPreview"
            @open-citation="openCitation"
            @copy="copyAiMessage"
            @continue="continueFromAiMessage"
            @execute-action="confirmPageAction"
            @cancel-action="cancelPageAction"
            @retry-action="retryPageAction"
            @quick-prompt="useQuickPrompt"
          />
        </div>

        <div class="ai-chat-input-wrapper">
          <AiComposer
            ref="aiComposerRef"
            v-model="userMessage"
            :placeholder="'Hỏi SprintA AI bất cứ điều gì...'"
            :enter-hint="'Enter để gửi · Shift + Enter để xuống dòng'"
            reset-label="Cuộc trò chuyện mới"
            :sending="isLoading"
            :credits-exhausted="aiCreditsExhausted"
            :pending-attachments="pendingAttachments"
            :composer-drag-active="composerDragActive"
            :capturing-screenshot="capturingScreenshot"
            :voice-state="voiceState"
            :voice-language="voiceLanguage"
            :voice-language-label="voiceLanguageLabel"
            :voice-status-title="voiceStatusTitle"
            :voice-elapsed-label="voiceElapsedLabel"
            :voice-transcript="voiceTranscript"
            :voice-error="voiceError"
            :accept="composerAttachmentAccept"
            @files="handleAttachmentInput"
            @preview-attachment="openAttachmentPreview"
            @remove-attachment="removePendingAttachment"
            @attachment-command="handleAttachmentCommand"
            @paste="handleComposerPaste"
            @keydown="handleComposerKeydown"
            @dragenter="composerDragActive = true"
            @dragleave="handleComposerDragLeave"
            @drop="handleComposerDrop"
            @start-voice="startVoiceRecording"
            @stop-voice="stopVoiceRecording"
            @cancel-voice="cancelVoiceInput"
            @record-again="recordVoiceAgain"
            @use-transcript="applyVoiceTranscript"
            @send="sendMessage"
            @reset="startNewConversation"
          />
        </div>
      </div>

      <aside class="ai-details-panel">
        <div class="panel-section">
          <div class="section-label">SPRINTA AI</div>
          <div class="section-title">CÔNG CỤ NHANH</div>
          <div class="quick-links">
            <button v-for="action in quickActions.slice(0, 4)" :key="action.type" class="q-link" type="button" @click="runQuickPrompt(action.prompt)">
              <i :class="action.icon" aria-hidden="true"></i> {{ action.label }}
            </button>
          </div>
          <button v-if="quickActions.slice(4).length" class="quick-more-toggle" type="button" :aria-expanded="quickToolsExpanded" @click="quickToolsExpanded = !quickToolsExpanded">
            <span><i class="fa-solid fa-sliders" aria-hidden="true"></i> Xem thêm công cụ</span>
            <i class="fa-solid fa-chevron-down" aria-hidden="true"></i>
          </button>
          <div v-if="quickToolsExpanded" class="quick-links quick-links-more" aria-label="Công cụ bổ sung">
              <button v-for="action in quickActions.slice(4)" :key="`more-${action.type}`" class="q-link" type="button" @click="runQuickPrompt(action.prompt)">
                <i :class="action.icon" aria-hidden="true"></i> {{ action.label }}
              </button>
          </div>
        </div>

        <div class="panel-section mt-30">
          <div class="section-title">NGỮ CẢNH HIỆN TẠI</div>
          <p class="text-muted sidebar-copy">{{ activeProjectName }} · {{ activeWorkspaceName }}. Credits và tiến trình được cập nhật theo tài khoản của bạn.</p>
        </div>

        <div class="upgrade-card-wrapper">
          <div class="upgrade-card" aria-label="Tình trạng AI Credits">
            <div class="plan-label">AI CREDITS · {{ aiPlanLabel }}</div>
            <strong class="credit-balance">{{ aiRemainingCredits }} còn lại</strong>
            <div class="credit-meter" role="progressbar" :aria-valuenow="aiCreditPercent" aria-valuemin="0" aria-valuemax="100" aria-label="Tỷ lệ AI credits còn lại">
              <span :style="{ width: `${aiCreditPercent}%` }"></span>
            </div>
            <p class="plan-desc">{{ aiUsage ? `${aiUsage.usedCredits || 0} đã dùng · ${aiUsage.includedCredits || 0} được cấp trong kỳ này.` : 'Đang tải trạng thái credits...' }}</p>
            <button class="btn-upgrade" type="button" @click="openAiCreditPurchase">Quản lý gói</button>
          </div>
        </div>
      </aside>
    </div>

    <CustomizeSidebarModal :visible="showCustomizeModal" @update:visible="showCustomizeModal = $event" @saved="handleSidebarSaved" />
    <AiCreditsPurchaseModal v-model="aiCreditsModalVisible" />
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import CustomizeSidebarModal from '../components/CustomizeSidebarModal.vue'
import AiComposer from '@/components/ai/AiComposer.vue'
import AiMessage from '@/components/ai/AiMessage.vue'
import AiCreditsPurchaseModal from '@/components/ai/AiCreditsPurchaseModal.vue'

import axiosClient from '@/api/axiosClient'
import { useProjectStore } from '@/store/useProjectStore'
import { useWorkTaskStore } from '@/store/useWorkTaskStore'
import { useSiteStore } from '@/store/useSiteStore'
import { useSprintStore } from '@/store/useSprintStore'
import { useI18nStore } from '@/store/useI18nStore'
import { broadcastAdminRealtime } from '@/utils/adminRealtime'
import { signalRService } from '@/api/signalrService'
import { hasProjectWritePermission, normalizeProjectRole } from '@/utils/permissions'
import { AUTH_SESSION_CHANGED, getStoredUserSession } from '@/utils/authSession'
import { clearScopedCurrentProjectId, getScopedCurrentProjectId, setScopedCurrentProjectId } from '@/utils/projectContext'
import { clearLegacyGitHubCredentialStorage, runWithEphemeralGitHubToken } from '@/utils/githubCredentials'
import { useAiConversationStore } from '@/store/useAiConversationStore'
import { useAiPetStore } from '@/store/useAiPetStore'
import { useAiScopeStore } from '@/store/useAiScopeStore'
import { buildAiContextKey, isAiContextMatch, isComposerSendKey } from '@/utils/aiWorkspace'
import { useAiComposer } from '@/composables/useAiComposer'
import { AI_QUICK_ACTIONS, aiActionPayload, normalizeAiActionList } from '@/utils/aiActionUi'
import { decorateAiAction, findPendingAiAction, isAiConfirmationMessage, previewAndConfirmAiAction } from '@/utils/aiActionEngine'

const router = useRouter()
const route = useRoute()
const aiComposerRef = ref(null)
const aiConversationStore = useAiConversationStore()
const aiPetStore = useAiPetStore()
const aiScopeStore = useAiScopeStore()
const projectStore = useProjectStore()
const workTaskStore = useWorkTaskStore()
const siteStore = useSiteStore()
const sprintStore = useSprintStore()
const i18nStore = useI18nStore()
const currentUser = ref(getStoredUserSession())
const selectedWorkspaceId = computed({
  get: () => aiScopeStore.workspaceId,
  set: value => aiScopeStore.setWorkspace(value)
})
const selectedProjectId = computed({
  get: () => aiScopeStore.projectId || getScopedCurrentProjectId(),
  set: value => aiScopeStore.setProject(value)
})
const showCustomizeModal = ref(false)
const sidebarPreferences = ref({ audit: true, users: true })

const userMessage = ref('')
const isLoading = ref(false)
const repoLoading = ref(false)
const repoStatus = ref('')
const repoAnalysis = ref(null)
const createBacklogLoading = ref('')
const aiUsage = ref(null)
const aiCreditsModalVisible = ref(false)
const aiCreditsExhausted = computed(() => Boolean(
  aiUsage.value
  && Number(aiUsage.value.includedCredits || 0) > 0
  && Number(aiUsage.value.remainingCredits ?? aiUsage.value.remainingIncludedCredits ?? (aiUsage.value.includedCredits - Number(aiUsage.value.usedCredits || 0))) <= 0
))
const aiRemainingCredits = computed(() => Math.max(0, Number(
  aiUsage.value?.remainingCredits
  ?? aiUsage.value?.remainingIncludedCredits
  ?? (Number(aiUsage.value?.includedCredits || 0) - Number(aiUsage.value?.usedCredits || 0))
)))
const aiCreditPercent = computed(() => {
  const included = Number(aiUsage.value?.includedCredits || 0)
  return included > 0 ? Math.max(0, Math.min(100, Math.round((aiRemainingCredits.value / included) * 100))) : 0
})
const aiPlanLabel = computed(() => {
  const plan = String(aiUsage.value?.planCode || 'free').trim()
  return plan ? plan.charAt(0).toUpperCase() + plan.slice(1) : 'Free'
})
const quickActions = computed(() => AI_QUICK_ACTIONS)
const quickToolsExpanded = ref(false)
const connectedIntegrations = ref([])
const hasConnectedGithubIntegration = computed(() => connectedIntegrations.value.some(item => item?.provider === 'github' && item?.status === 'connected'))
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
const workspaceOptions = computed(() => siteStore.sites || [])
const projectWorkspaceIdOf = project => project?.workspaceId || project?.WorkspaceId || project?.originalRow?.workspaceId || project?.originalRow?.WorkspaceId || ''
const availableProjects = computed(() => {
  const workspaceId = `${currentWorkspaceId.value || ''}`
  if (!workspaceId) return []
  return (projectStore.allProjects || []).filter(project => `${projectWorkspaceIdOf(project)}` === workspaceId)
})
const currentWorkspaceId = computed(() => selectedWorkspaceId.value || currentProjectRecord.value?.workspaceId || currentProjectRecord.value?.WorkspaceId || workTaskStore.resolveWorkspaceId(currentProjectId.value) || null)
const formatConversationDate = value => {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  const now = new Date()
  const dateKey = item => `${item.getFullYear()}-${item.getMonth()}-${item.getDate()}`
  const time = new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit' }).format(date)
  if (dateKey(date) === dateKey(now)) return `Hôm nay · ${time}`
  const yesterday = new Date(now)
  yesterday.setDate(now.getDate() - 1)
  if (dateKey(date) === dateKey(yesterday)) return `Hôm qua · ${time}`
  return new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(date)
}
const groupedConversations = computed(() => {
  const now = new Date()
  const dateKey = item => `${item.getFullYear()}-${item.getMonth()}-${item.getDate()}`
  const todayKey = dateKey(now)
  const yesterday = new Date(now)
  yesterday.setDate(now.getDate() - 1)
  const yesterdayKey = dateKey(yesterday)
  const groups = [
    { key: 'today', label: 'Hôm nay', items: [] },
    { key: 'recent', label: 'Gần đây', items: [] },
    { key: 'earlier', label: 'Trước đó', items: [] }
  ]
  filteredConversations.value.forEach(conversation => {
    const date = new Date(conversation.updatedAt)
    const key = Number.isNaN(date.getTime())
      ? 'earlier'
      : dateKey(date) === todayKey
        ? 'today'
        : dateKey(date) === yesterdayKey
          ? 'recent'
          : 'earlier'
    groups.find(group => group.key === key).items.push(conversation)
  })
  return groups.filter(group => group.items.length)
})
const {
  pendingAttachments,
  composerDragActive,
  capturingScreenshot,
  voiceState,
  voiceLanguage,
  voiceTranscript,
  voiceError,
  voiceElapsedLabel,
  voiceLanguageLabel,
  voiceStatusTitle,
  accept: composerAttachmentAccept,
  openAttachmentPreview,
  removePendingAttachment,
  handleAttachmentInput,
  handleComposerPaste,
  handleAttachmentCommand,
  handleComposerDrop,
  handleComposerDragLeave,
  startVoiceRecording,
  stopVoiceRecording,
  cancelVoiceInput,
  recordVoiceAgain,
  useVoiceTranscript
} = useAiComposer({ workspaceId: currentWorkspaceId })

const breakdownProgressSteps = [
  'Đang phân tích task',
  'Đang truy vấn dịch vụ AI',
  'Đang thử lại nếu cần',
  'Đang tổng hợp kết quả'
]

const defaultProgressSteps = [
  'Đang đọc yêu cầu',
  'Đang truy vấn AI',
  'Đang tổng hợp phản hồi'
]

let progressTimer = null

const userInitials = computed(() => {
  const name = currentUser.value?.fullName || currentUser.value?.username || currentUser.value?.email || 'ME'
  return name.substring(0, 2).toUpperCase()
})

const currentProjectId = computed(() => selectedProjectId.value || null)
const aiContextKey = computed(() => buildAiContextKey(currentWorkspaceId.value, currentProjectId.value))
const aiContextRevision = ref(0)
const activeProjectName = computed(() => {
  const projectId = currentProjectId.value
  if (!projectId) return 'Chua chon project'
  const project = projectStore.allProjects.find(item => item.id === projectId) || projectStore.currentProject
  return project?.name || `Project ${projectId}`
})
const activeWorkspaceName = computed(() => {
  const workspace = workspaceOptions.value.find(item => `${item.id || item.Id}` === `${currentWorkspaceId.value || ''}`)
  return workspace?.name || workspace?.Name || (currentWorkspaceId.value ? 'Workspace hiện tại' : 'Chưa chọn workspace')
})
const currentPageLabel = computed(() => {
  const name = route.meta?.title || route.name || route.path
  return typeof name === 'string' ? name : route.path
})
const workspaceIdOf = workspace => workspace?.id || workspace?.Id || ''
const syncSelectedWorkspace = () => {
  const preferredId = currentProjectRecord.value?.workspaceId
    || currentProjectRecord.value?.WorkspaceId
    || siteStore.activeSite?.id
    || siteStore.activeSite?.Id
    || ''
  const preferredWorkspace = workspaceOptions.value.find(item => `${workspaceIdOf(item)}` === `${preferredId}`)
  aiScopeStore.hydrate({ workspaceId: workspaceIdOf(preferredWorkspace) || workspaceIdOf(workspaceOptions.value[0]) })
}
const syncSelectedProject = () => {
  const candidate = selectedProjectId.value || getScopedCurrentProjectId()
  const selected = availableProjects.value.find(project => `${project.id}` === `${candidate}`)
  if (selected) {
    aiScopeStore.hydrate({ projectId: selected.id })
    setScopedCurrentProjectId(selected.id)
    projectStore.clearProjectContext(selected.id)
    return selected.id
  }

  aiScopeStore.clearProject()
  clearScopedCurrentProjectId()
  projectStore.clearProjectContext()
  return ''
}
const resetAiConversation = () => {
  releaseMessageAttachmentUrls()
  aiConversationStore.startNewConversation()
}
const handleWorkspaceChanged = async event => {
  const workspaceId = event?.detail?.workspaceId
  if (!workspaceId || `${workspaceId}` === `${selectedWorkspaceId.value}`) return
  aiScopeStore.setWorkspace(workspaceId)
  aiContextRevision.value += 1
  clearScopedCurrentProjectId()
  projectStore.clearWorkspaceData()
  resetAiConversation()
  await projectStore.fetchAllProjects(true).catch(() => [])
  syncSelectedProject()
  await loadConversations(true)
}
const handleWorkspaceChange = async () => {
  const workspace = workspaceOptions.value.find(item => `${workspaceIdOf(item)}` === `${selectedWorkspaceId.value}`)
  if (!workspace) return

  const previousWorkspaceId = siteStore.activeSite?.id || siteStore.activeSite?.Id || null
  siteStore.setRecentSite(workspace)
  if (`${previousWorkspaceId || ''}` === `${selectedWorkspaceId.value}`) return

  aiScopeStore.setWorkspace(selectedWorkspaceId.value)
  aiContextRevision.value += 1
  clearScopedCurrentProjectId()
  projectStore.clearWorkspaceData()
  resetAiConversation()
  await projectStore.fetchAllProjects(true).catch(() => [])
  syncSelectedProject()
  await loadConversations(true)
}
const handleProjectChange = async () => {
  const selected = availableProjects.value.find(project => `${project.id}` === `${selectedProjectId.value}`)
  if (!selected) {
    syncSelectedProject()
    return
  }

  aiScopeStore.setProject(selected.id)
  setScopedCurrentProjectId(selected.id)
  projectStore.clearProjectContext(selected.id)
  aiContextRevision.value += 1
  resetAiConversation()
  await loadConversations(true)
}
const refreshCurrentUser = () => {
  currentUser.value = getStoredUserSession()
}
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

const startNewConversation = () => resetAiConversation()
const openConversation = async id => {
  try {
    releaseMessageAttachmentUrls()
    await aiConversationStore.openConversation(id)
    await hydrateConversationImages()
  } catch {
    ElMessage.error('Không thể mở cuộc trò chuyện.')
  }
}
const releaseMessageAttachmentUrls = () => {
  chatHistory.value.flatMap(message => message.attachments || []).forEach(attachment => {
    if (attachment.previewUrl?.startsWith('blob:')) URL.revokeObjectURL(attachment.previewUrl)
    attachment.previewUrl = ''
  })
}
const hydrateConversationImages = async () => {
  const images = chatHistory.value.flatMap(message => message.attachments || [])
    .filter(attachment => attachment.kind === 'image' && attachment.contentUrl)
  await Promise.all(images.map(async attachment => {
    try {
      const response = await axiosClient.get(attachment.contentUrl, { responseType: 'blob' })
      attachment.previewUrl = URL.createObjectURL(response.data)
    } catch {
      attachment.previewUrl = ''
    }
  }))
}
const returnToFloating = async () => {
  aiPetStore.setPanelOpen(true)
  await router.back()
}
const openAiCreditPurchase = () => {
  aiCreditsModalVisible.value = true
}
const actionPayload = aiActionPayload

const confirmPageAction = async action => {
  if (!action || action.loading || action.uiStatus === 'success' || action.uiStatus === 'cancelled') return
  if (!isAiContextMatch(action.contextKey, currentWorkspaceId.value, currentProjectId.value)) {
    action.uiStatus = 'error'
    action.error = 'Ngữ cảnh AI đã thay đổi. Hãy gửi lại yêu cầu trong project hiện tại.'
    await aiConversationStore.persistConversation()
    return
  }
  action.loading = true
  action.uiStatus = 'loading'
  try {
    const response = await previewAndConfirmAiAction(action, {
      workspaceId: currentWorkspaceId.value,
      projectId: currentProjectId.value || actionPayload(action).projectId,
      conversationId: currentConversationId.value
    })
    const payload = response.data?.data ?? response.data
    action.result = payload?.result ?? payload
    action.uiStatus = 'success'
    action.status = 'EXECUTED'
    ElMessage.success('AI đã thực hiện thay đổi thành công.')
    await aiConversationStore.persistConversation()
  } catch (error) {
    action.uiStatus = 'error'
    action.status = 'FAILED'
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
  action.status = 'CANCELLED'
  await aiConversationStore.persistConversation()
}

const retryPageAction = action => {
  if (!action || action.loading) return
  action.uiStatus = 'pending'
  action.error = ''
  return confirmPageAction(action)
}

const copyAiMessage = async content => {
  if (!content) return
  try { await navigator.clipboard.writeText(content); ElMessage.success('Đã sao chép câu trả lời.') } catch { ElMessage.info('Không thể sao chép tự động trên trình duyệt này.') }
}

const continueFromAiMessage = content => {
  userMessage.value = `Hãy giải thích thêm và đưa ra bước tiếp theo từ câu trả lời này:\n${`${content || ''}`.slice(0, 600)}`
  window.setTimeout(() => aiComposerRef.value?.focusInput?.(), 0)
}

const openCitation = citation => {
  const attachment = chatHistory.value.flatMap(message => message.attachments || []).find(item => item.id === citation?.attachmentId)
  if (attachment) openAttachmentPreview(attachment)
}

const applyVoiceTranscript = () => {
  const transcript = useVoiceTranscript()
  if (!transcript) return
  userMessage.value = transcript
  cancelVoiceInput()
}

const handleComposerKeydown = event => {
  if (!isComposerSendKey(event)) return
  event.preventDefault()
  sendMessage()
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

const uploadFullAttachments = async conversationId => {
  const uploaded = []
  for (const attachment of pendingAttachments.value) {
    attachment.status = 'uploading'
    const form = new FormData()
    form.append('file', attachment.file, attachment.name)
    form.append('conversationId', conversationId)
    if (currentWorkspaceId.value) form.append('workspaceId', currentWorkspaceId.value)
    try {
      const response = await axiosClient.post('/ai/attachments', form, { headers: { 'Content-Type': 'multipart/form-data' } })
      const payload = response.data?.data ?? response.data
      Object.assign(attachment, { id: payload.id, name: payload.fileName || attachment.name, size: payload.fileSize || attachment.size, contentUrl: payload.contentUrl, mimeType: payload.mimeType, status: String(payload.status || 'ready').toLowerCase() })
      uploaded.push(attachment)
    } catch (error) {
      attachment.status = 'error'
      throw error
    }
  }
  return uploaded
}

const sendMessage = async (overrideMessage = null) => {
  const outgoing = `${overrideMessage ?? userMessage.value}`.trim()
  const hasAttachments = pendingAttachments.value.length > 0
  if (aiCreditsExhausted.value || (!outgoing && !hasAttachments) || isLoading.value) return

  if (!hasAttachments && isAiConfirmationMessage(outgoing)) {
    const pendingAction = findPendingAiAction(chatHistory.value, {
      contextKey: aiContextKey.value,
      conversationId: currentConversationId.value,
      workspaceId: currentWorkspaceId.value,
      projectId: currentProjectId.value
    })
    if (pendingAction) {
      userMessage.value = ''
      await confirmPageAction(pendingAction)
      return
    }
  }

  if (!overrideMessage) {
    userMessage.value = ''
  }

  const sentAttachments = pendingAttachments.value
  isLoading.value = true
  const requestRevision = aiContextRevision.value
  startThinkingMessage(outgoing)

  try {
    const conversationId = await aiConversationStore.ensureConversation({
      workspaceId: currentWorkspaceId.value,
      firstMessage: outgoing || sentAttachments.map(item => item.name).join(', ')
    })
    const uploadedAttachments = hasAttachments ? await uploadFullAttachments(conversationId) : []
    if (requestRevision !== aiContextRevision.value) return
    pendingAttachments.value = []
    chatHistory.value.splice(chatHistory.value.length - 1, 0, { role: 'user', content: outgoing || 'Hãy phân tích các attachment đã đính kèm.', attachments: uploadedAttachments })
    const history = chatHistory.value
      .filter(item => !item.isTyping)
      .slice(-10)
      .map(item => ({ role: item.role === 'bot' ? 'assistant' : 'user', content: item.content }))

    const response = uploadedAttachments.length
      ? await axiosClient.post('/ai/attachment-chat', { conversationId, workspaceId: currentWorkspaceId.value || null, attachmentIds: uploadedAttachments.map(item => item.id), message: outgoing })
      : await axiosClient.post('/ai/context-chat', {
        conversationId,
        route: '/ai-assistant',
        projectId: currentProjectId.value || null,
        workspaceId: currentWorkspaceId.value || null,
        message: outgoing,
        pageContext: { pageType: 'ai-assistant', currentView: 'conversation', visibleTaskIds: [], visibleStatuses: [], filters: {}, extra: { history: JSON.stringify(history) } }
      })
    if (requestRevision !== aiContextRevision.value) return

    clearProgressTimer()
    chatHistory.value.pop()
    const payload = response.data?.data ?? response.data
    const message = payload?.answer || payload?.message || response.data?.message || (i18nStore.locale === 'en'
      ? 'Sorry, AI did not return content. Please try another request.'
      : 'R\u1ea5t ti\u1ebfc, AI kh\u00f4ng ph\u1ea3n h\u1ed3i n\u1ed9i dung. B\u1ea1n c\u00f3 th\u1ec3 th\u1eed l\u1ea1i v\u1edbi c\u00e2u h\u1ecfi kh\u00e1c.')
    const normalizedActions = normalizeAiActionList(payload?.actions || [])
    chatHistory.value.push({
      role: 'bot',
      content: [message, normalizedActions.hasMissingTaskTitle ? 'Bạn muốn đặt tên công việc là gì?' : ''].filter(Boolean).join('\n\n'),
      warnings: payload?.warnings || [],
      citations: payload?.citations || [],
      actions: normalizedActions.actions.map(action => decorateAiAction(action, {
        contextKey: aiContextKey.value,
        conversationId,
        workspaceId: currentWorkspaceId.value,
        projectId: currentProjectId.value || actionPayload(action).projectId
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
    await loadAiUsage()
    isLoading.value = false
  }
}

const useQuickPrompt = (prompt) => {
  userMessage.value = prompt
}
const runQuickPrompt = (prompt) => {
  userMessage.value = prompt
  void sendMessage()
}

const prepareBreakdownPrompt = () => {
  userMessage.value = 'Phân rã task sau thành 3-5 subtask rõ ràng. Mỗi subtask cần có mục tiêu, owner đề xuất, test/checklist và bàn giao.'
}

const analyzeRepository = async () => {
  const repoUrl = repoForm.value.url.trim()
  if (!repoUrl) {
    ElMessage.warning('Hãy nhập repo GitHub trước.')
    return
  }

  const parsed = parseRepo(repoUrl)
  if (!parsed) {
    ElMessage.error('Repo URL không đúng định dạng GitHub.')
    return
  }

  repoLoading.value = true
  repoStatus.value = 'Đang phân tích repo qua backend AI...'

  try {
    const response = await runWithEphemeralGitHubToken(repoForm.value, gitHubToken =>
      axiosClient.post('/ai/repo-analysis', {
        repoUrl,
        gitHubToken,
        focus: 'Repository planning, backlog, risks, and test strategy'
      }))

    const analysis = response.data?.data
    if (!analysis) {
      throw new Error('AI không trả về repo analysis hợp lệ.')
    }

    repoAnalysis.value = analysis
    syncReviewSelectionFromAnalysis()
    userMessage.value = analysis.suggestedPrompt || `Phân tích repo ${parsed.owner}/${parsed.repo} và đề xuất backlog tiếp theo.`
    repoStatus.value = `Da phan tich repo ${analysis.repository}. Prompt da san sang trong chat box.`
    chatHistory.value.push({
      role: 'bot',
      content: [
        `Repo ${analysis.repository}: ${analysis.summary}`,
        '',
        `Quick wins: ${(analysis.quickWins || []).map(item => item.title).join(' | ') || 'Không có'}`,
        `Medium tasks: ${(analysis.mediumTasks || []).map(item => item.title).join(' | ') || 'Không có'}`,
        `Risky tasks: ${(analysis.riskyTasks || []).map(item => item.title).join(' | ') || 'Không có'}`
      ].join('\n')
    })
  } catch (error) {
    repoStatus.value = error.response?.data?.message || error.message || 'Không phân tích được repo.'
    ElMessage.error(repoStatus.value)
  } finally {
    repoLoading.value = false
  }
}

const createBacklogItems = async (mode) => {
  if (!repoAnalysis.value) {
    ElMessage.warning('Hãy phân tích repo trước.')
    return
  }

  if (!currentProjectId.value) {
    ElMessage.warning('Hãy chọn project trên sidebar trước.')
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
    ElMessage.error(error.response?.data?.message || 'Không tạo được AI backlog items.')
  } finally {
    createBacklogLoading.value = ''
  }
}

const createReviewedBacklogItems = async () => {
  if (!repoAnalysis.value) {
    ElMessage.warning('Hãy phân tích repo trước.')
    return
  }

  if (!currentProjectId.value) {
    ElMessage.warning('Hãy chọn project trên sidebar trước.')
    return
  }

  if (!canManageProjectAi.value) {
    ElMessage.error('You do not have permission to create AI backlog items for this project.')
    return
  }

  if (!selectedBacklogItems.value.length) {
    ElMessage.warning('Hãy chọn ít nhất một AI backlog item.')
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
    ElMessage.error(error.response?.data?.message || 'Không tạo được reviewed AI backlog items.')
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

const loadConnectedIntegrations = async () => {
  try {
    const response = await axiosClient.get('/integrations')
    const payload = response.data?.data ?? response.data
    const rawProviders = payload?.providers ?? []
    const providers = Array.isArray(rawProviders) ? rawProviders : (Array.isArray(rawProviders?.$values) ? rawProviders.$values : [])
    connectedIntegrations.value = Array.isArray(providers)
      ? providers.filter(provider => provider?.status === 'connected')
      : []
  } catch {
    connectedIntegrations.value = []
  }
}

onMounted(() => {
  siteStore.fetchSites()
    .then(async () => {
      syncSelectedWorkspace()
      await projectStore.fetchAllProjects(true).catch(() => [])
      syncSelectedProject()
      await loadConversations(true)
    })
    .catch(async () => {
      await projectStore.fetchAllProjects(true).catch(() => [])
      syncSelectedProject()
      await loadConversations(true)
    })
  window.addEventListener('sprinta-workspace-changed', handleWorkspaceChanged)
  window.addEventListener(AUTH_SESSION_CHANGED, refreshCurrentUser)
  clearLegacyGitHubCredentialStorage()
  loadAiUsage()
  loadConnectedIntegrations()
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
  releaseMessageAttachmentUrls()
  window.removeEventListener('sprinta-workspace-changed', handleWorkspaceChanged)
  window.removeEventListener(AUTH_SESSION_CHANGED, refreshCurrentUser)
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
  background: var(--color-bg);
}

/* Final page pass: distinct navy-tinted layers, more breathing room, and a
   deliberate right rail instead of a wall of identical pale controls. */
.ai-page-flex-wrapper {
  --ai-page-ink: #0a2340;
  --ai-page-accent: #0b8fd3;
  min-height: 0;
  background: var(--color-bg);
}

.ai-page-history {
  flex-basis: 264px;
  padding: 26px 16px;
  background: linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 86%, var(--ai-page-accent)), var(--color-surface));
}

.ai-container {
  padding: 26px clamp(20px, 4vw, 62px) 0;
  background:
    radial-gradient(circle at 58% -10%, color-mix(in srgb, var(--ai-page-accent) 11%, transparent), transparent 34%),
    var(--color-bg);
}

.ai-page-header { margin-bottom: 20px; padding: 0; }
.page-title { font-size: clamp(27px, 2.7vw, 36px); font-weight: 800; letter-spacing: -.045em; }
.header-left { gap: 9px; }
.header-pill,
.workspace-context-pill,
.credit-pill,
.workspace-selector,
.header-icon-btn,
.return-floating-btn,
.credit-buy-inline {
  border-color: color-mix(in srgb, var(--ai-page-accent) 24%, var(--color-border));
  background: color-mix(in srgb, var(--color-surface) 88%, var(--ai-page-accent));
}
.header-pill { padding: 6px 10px; font-weight: 750; }
.credit-pill { margin-left: 0; font-variant-numeric: tabular-nums; }

.workspace-tools { margin-bottom: 18px; }
.workspace-tools > summary {
  min-height: 46px;
  padding: 10px 14px;
  border-color: color-mix(in srgb, var(--ai-page-accent) 25%, var(--color-border));
  border-radius: 13px;
  background: color-mix(in srgb, var(--color-surface) 86%, var(--ai-page-accent));
}
.repo-panel {
  margin: 0 0 16px;
  padding: 18px;
  border-color: color-mix(in srgb, var(--ai-page-accent) 20%, var(--color-border));
  border-radius: 0 0 15px 15px;
  background: color-mix(in srgb, var(--color-surface) 93%, var(--ai-page-accent));
  box-shadow: 0 12px 28px color-mix(in srgb, var(--color-text-primary) 7%, transparent);
}

.chat-history {
  max-width: 940px;
  gap: 18px;
  padding-bottom: 24px;
}
.chat-history :deep(.ai-message-bubble) { line-height: 1.68; }
.ai-chat-input-wrapper {
  width: min(940px, 100%);
  padding: 14px 0 22px;
  background: linear-gradient(var(--color-bg), color-mix(in srgb, var(--color-bg) 92%, transparent) 82%, transparent);
}

.ai-details-panel {
  width: 302px;
  padding: 30px 21px 24px;
  border-left-color: color-mix(in srgb, var(--ai-page-accent) 20%, var(--color-border));
  background: linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 94%, var(--ai-page-accent)), var(--color-surface));
}
.section-label { font-size: 10px; letter-spacing: .16em; }
.section-title { margin-bottom: 0; font-size: 16px; font-weight: 800; letter-spacing: -.025em; text-transform: none; }
.quick-links { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 9px; margin-top: 14px; }
.q-link {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 58px;
  padding: 10px;
  border-color: color-mix(in srgb, var(--ai-page-accent) 19%, var(--color-border));
  border-radius: 13px;
  background: color-mix(in srgb, var(--color-surface-hover) 65%, var(--color-surface));
  font-size: 11px;
  font-weight: 750;
  line-height: 1.3;
}
.q-link i { width: 25px; flex: 0 0 25px; color: var(--ai-page-accent); text-align: center; font-size: 15px; }
.q-link:hover { transform: translateY(-2px); box-shadow: 0 8px 18px color-mix(in srgb, var(--ai-page-accent) 12%, transparent); }
.quick-more-toggle {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  min-height: 42px;
  margin-top: 12px;
  padding: 0 11px;
  border: 1px solid color-mix(in srgb, var(--ai-page-accent) 22%, var(--color-border));
  border-radius: 11px;
  background: color-mix(in srgb, var(--color-surface) 82%, var(--ai-page-accent));
  color: var(--color-text-secondary);
  cursor: pointer;
  font: inherit;
  font-size: 11px;
  font-weight: 750;
}
.quick-more-toggle span { display: inline-flex; align-items: center; gap: 8px; }
.quick-more-toggle span i { color: var(--ai-page-accent); }
.quick-more-toggle > i { transition: transform 180ms ease; }
.quick-more-toggle[aria-expanded='true'] > i { transform: rotate(180deg); }
.quick-links-more { margin-top: 9px; padding-top: 9px; border-top: 1px solid color-mix(in srgb, var(--ai-page-accent) 15%, var(--color-border)); }
.sidebar-copy { margin: 10px 0 0; font-size: 12px; }
.upgrade-card-wrapper { margin-top: 34px; }
.upgrade-card {
  padding: 20px;
  border-color: color-mix(in srgb, var(--ai-page-accent) 38%, var(--color-border));
  border-radius: 18px;
  background: linear-gradient(145deg, color-mix(in srgb, var(--ai-page-accent) 18%, var(--color-surface)), var(--color-surface) 72%);
  box-shadow: 0 16px 32px color-mix(in srgb, var(--ai-page-accent) 12%, transparent);
}
.credit-balance { font-size: 29px; }
.btn-upgrade { width: 100%; min-height: 42px; border-radius: 11px; }

@media (max-width: 1100px) {
  .ai-container { padding: 20px 18px 0; }
  .ai-details-panel { padding: 22px 18px; }
}

@media (max-width: 620px) {
  .ai-container { padding: 16px 14px 0; }
  .header-left { align-items: flex-start; }
  .workspace-context-pill { max-width: min(100%, 300px); }
  .chat-history { padding-bottom: 12px; }
  .ai-chat-input-wrapper { padding: 10px 0 calc(12px + env(safe-area-inset-bottom)); }
  .quick-links { grid-template-columns: 1fr 1fr; }
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
  background: var(--color-surface-hover);
  color: var(--color-accent);
  font-size: 12px;
  font-weight: 600;
  border: 1px solid var(--color-border);
}

.credit-pill {
  margin-left: auto;
  padding: 5px 10px;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font-size: 12px;
  font-weight: 600;
}

.repo-panel {
  margin: 0 32px 12px;
  padding: 16px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface);
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
  background: var(--color-surface-hover);
  border: 1px solid var(--color-border);
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
  background: color-mix(in srgb, var(--color-surface-elevated) 92%, var(--color-accent) 8%);
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
  background: color-mix(in srgb, var(--color-surface-hover) 72%, transparent);
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
  background: var(--color-surface-hover);
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
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
  padding: 10px 12px;
  outline: none;
}
.repo-input:focus {
  border-color: var(--color-accent);
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
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  color: var(--color-accent);
}

.user-avatar-circle {
  background: var(--color-accent);
  color: var(--color-text-inverse);
  font-weight: 700;
}

.bubble {
  min-width: 120px;
  max-width: 100%;
  padding: 10px 14px;
  border-radius: 12px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  color: var(--color-text-primary);
  line-height: 1.5;
  font-size: 14px;
}

.bubble.primary {
  background: var(--color-accent);
  color: var(--color-text-inverse);
  border-color: var(--color-accent);
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
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-surface-hover);
  padding: 0 14px;
  transition: all 0.2s;
}
.input-box:focus-within {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-accent) 20%, transparent);
}

.input-box input {
  flex: 1;
  border: 0;
  background: transparent;
  color: var(--color-text-primary);
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
  color: var(--color-accent);
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
  border-left: 1px solid var(--color-border);
  background: var(--color-surface);
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

.quick-more { margin-top: 10px; border-top: 1px solid var(--color-border); padding-top: 10px; }
.quick-more summary { color: var(--color-accent); font-size: 12px; font-weight: 750; cursor: pointer; }
.quick-links-more { margin-top: 10px; }

.q-link {
  width: 100%;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
  padding: 10px 12px;
  text-align: left;
  cursor: pointer;
  transition: all 0.2s;
}
.q-link:hover {
  background: var(--color-surface-hover);
  border-color: var(--color-accent);
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
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface-hover);
  padding: 18px;
}

.credit-balance { display: block; margin-bottom: 10px; color: var(--color-text-primary); font-size: 22px; }
.credit-meter { height: 7px; margin-bottom: 10px; overflow: hidden; border-radius: 999px; background: var(--color-border); }
.credit-meter span { display: block; height: 100%; border-radius: inherit; background: var(--color-accent); }

.plan-label {
  color: var(--color-accent);
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
  background: var(--color-accent);
  color: var(--color-text-inverse);
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
.workspace-context-pill { display: inline-flex; align-items: center; gap: 6px; max-width: 280px; padding: 6px 9px; border: 1px solid var(--color-border); border-radius: 999px; background: var(--color-surface); color: var(--color-text-secondary); font-size: 11px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.workspace-context-pill i { color: var(--color-accent); }
.workspace-selector { display: inline-flex; align-items: center; gap: 7px; min-height: 30px; max-width: 220px; padding: 0 9px; border: 1px solid var(--color-border); border-radius: 8px; background: var(--color-surface); color: var(--color-text-secondary); }
.workspace-selector:focus-within { border-color: var(--color-accent); box-shadow: 0 0 0 2px var(--sa-primary-soft); }
.workspace-selector i { color: var(--color-accent); font-size: 11px; }
.workspace-selector select { min-width: 0; max-width: 180px; border: 0; outline: 0; background: transparent; color: var(--color-text-primary); font: inherit; font-size: 11px; font-weight: 700; cursor: pointer; }
.workspace-selector option { background: var(--color-surface); color: var(--color-text-primary); }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
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
.confirm-action { border: 1px solid var(--color-accent); background: var(--color-accent); color: var(--color-text-inverse); }
.cancel-action:disabled, .confirm-action:disabled { cursor: not-allowed; opacity: .55; }
.page-action-error { color: var(--color-danger) !important; }
.page-action-success, .page-read-note { color: var(--color-success) !important; }

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

/* Product polish: keep the full chat's shell and utility rail visually distinct
   from the shared message/composer components without changing their contracts. */
.ai-page-flex-wrapper {
  background: var(--color-bg);
}

.ai-page-history {
  background: linear-gradient(180deg, var(--color-surface), color-mix(in srgb, var(--color-surface) 88%, var(--color-accent)));
  box-shadow: inset -1px 0 0 color-mix(in srgb, var(--color-accent) 8%, transparent);
}

.ai-page-history-head strong,
.page-title,
.section-title,
.credit-balance {
  color: var(--color-text-primary);
}

.ai-page-history-head strong { font-size: 14px; letter-spacing: -.01em; }
.ai-page-history > input,
.workspace-selector,
.workspace-context-pill,
.return-floating-btn,
.header-icon-btn {
  box-shadow: var(--shadow-xs, 0 1px 2px color-mix(in srgb, var(--color-text-primary) 8%, transparent));
}

.ai-page-history > input:focus,
.workspace-tools > summary:focus-visible,
.ai-page-history-item:focus-visible,
.history-more:focus-visible,
.workspace-selector:focus-within,
.header-icon-btn:focus-visible,
.return-floating-btn:focus-visible,
.credit-buy-inline:focus-visible,
.q-link:focus-visible,
.quick-more > summary:focus-visible,
.btn-upgrade:focus-visible {
  outline: 2px solid var(--color-accent);
  outline-offset: 2px;
}

.ai-page-history-item {
  transition: border-color 160ms ease, background 160ms ease, transform 160ms ease;
}

.ai-page-history-item:hover { transform: translateX(2px); }

.ai-container {
  background:
    radial-gradient(circle at 50% 0, color-mix(in srgb, var(--color-accent) 7%, transparent), transparent 34%),
    var(--color-bg);
}

.page-title { font-size: clamp(24px, 2.1vw, 31px); letter-spacing: -.035em; }
.header-pill { background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)); color: var(--color-accent); }
.workspace-context-pill { background: color-mix(in srgb, var(--color-surface) 86%, var(--color-accent)); }
.credit-buy-inline { background: color-mix(in srgb, var(--color-accent) 7%, var(--color-surface)); }

.workspace-tools > summary {
  background: color-mix(in srgb, var(--color-surface) 76%, var(--color-accent));
  transition: border-color 160ms ease, background 160ms ease;
}

.workspace-tools > summary:hover { border-color: color-mix(in srgb, var(--color-accent) 40%, var(--color-border)); }
.chat-history { scrollbar-color: var(--color-border) transparent; }
.ai-chat-input-wrapper { background: linear-gradient(var(--color-bg), color-mix(in srgb, var(--color-bg) 90%, transparent) 78%, transparent); }

.ai-details-panel {
  width: 272px;
  padding: 28px 20px 22px;
  background: linear-gradient(180deg, var(--color-surface), color-mix(in srgb, var(--color-surface) 90%, var(--color-accent)));
  box-shadow: inset 1px 0 0 color-mix(in srgb, var(--color-accent) 8%, transparent);
}

.panel-section + .panel-section { margin-top: 26px; }
.section-label { color: var(--color-accent); font-size: 10px; font-weight: 900; letter-spacing: .13em; }
.section-title { margin-top: 7px; font-size: 11px; letter-spacing: .1em; }
.quick-links { gap: 7px; margin-top: 13px; }
.q-link {
  min-height: 40px;
  padding: 8px 10px;
  border: 1px solid color-mix(in srgb, var(--color-border) 92%, var(--color-accent));
  border-radius: 10px;
  background: color-mix(in srgb, var(--color-surface-hover) 78%, var(--color-surface));
  color: var(--color-text-secondary);
  text-align: left;
  transition: border-color 160ms ease, background 160ms ease, color 160ms ease, transform 160ms ease;
}
.q-link:hover { transform: translateX(2px); }
.quick-more > summary { color: var(--color-text-muted); }
.sidebar-copy { max-width: 30ch; line-height: 1.65; }

.upgrade-card {
  padding: 17px;
  border: 1px solid color-mix(in srgb, var(--color-accent) 30%, var(--color-border));
  border-radius: 16px;
  background:
    linear-gradient(145deg, color-mix(in srgb, var(--color-accent) 15%, var(--color-surface)), var(--color-surface)),
    var(--color-surface);
  box-shadow: 0 14px 32px color-mix(in srgb, var(--color-accent) 12%, transparent);
}
.plan-label { color: var(--color-accent); font-size: 10px; font-weight: 900; letter-spacing: .1em; }
.credit-balance { display: block; margin-top: 10px; font-size: 27px; letter-spacing: -.05em; }
.credit-meter { background: color-mix(in srgb, var(--color-border) 78%, var(--color-bg)); }
.credit-meter span { background: linear-gradient(90deg, var(--color-accent), var(--sa-primary)); }
.plan-desc { margin-top: 10px; line-height: 1.55; }
.btn-upgrade { min-height: 38px; border-radius: 10px; background: var(--color-accent); color: var(--color-on-accent, #fff); font-weight: 850; transition: filter 160ms ease, transform 160ms ease; }
.btn-upgrade:hover { filter: brightness(1.06); transform: translateY(-1px); }

@media (max-width: 1100px) {
  .ai-details-panel { width: 100%; padding: 22px 18px; box-shadow: none; }
}

/* Cascade guard: this block intentionally lives last so the final page
   direction wins over the legacy compatibility rules above. */
.ai-page-flex-wrapper { min-height: 0; background: var(--color-bg); }
.ai-page-history { flex-basis: 264px; padding: 26px 16px; background: linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 86%, #0b8fd3), var(--color-surface)); }
.ai-container { padding: 26px clamp(20px, 4vw, 62px) 0; background: radial-gradient(circle at 58% -10%, color-mix(in srgb, #0b8fd3 11%, transparent), transparent 34%), var(--color-bg); }
.ai-page-header { margin-bottom: 20px; padding: 0; }
.page-title { font-size: clamp(27px, 2.7vw, 36px); font-weight: 800; letter-spacing: -.045em; }
.header-left { gap: 9px; }
.header-pill, .workspace-context-pill, .credit-pill, .workspace-selector, .header-icon-btn, .return-floating-btn, .credit-buy-inline { border-color: color-mix(in srgb, #0b8fd3 24%, var(--color-border)); background: color-mix(in srgb, var(--color-surface) 88%, #0b8fd3); }
.credit-pill { margin-left: 0; font-variant-numeric: tabular-nums; }
.workspace-tools { margin-bottom: 18px; }
.workspace-tools > summary { min-height: 46px; padding: 10px 14px; border-color: color-mix(in srgb, #0b8fd3 25%, var(--color-border)); border-radius: 13px; background: color-mix(in srgb, var(--color-surface) 86%, #0b8fd3); }
.repo-panel { margin: 0 0 16px; padding: 18px; border-color: color-mix(in srgb, #0b8fd3 20%, var(--color-border)); border-radius: 0 0 15px 15px; background: color-mix(in srgb, var(--color-surface) 93%, #0b8fd3); box-shadow: 0 12px 28px color-mix(in srgb, var(--color-text-primary) 7%, transparent); }
.chat-history { max-width: 940px; gap: 18px; padding-bottom: 24px; }
.chat-history :deep(.ai-message-bubble) { line-height: 1.68; }
.ai-chat-input-wrapper { width: min(940px, 100%); padding: 14px 0 22px; background: linear-gradient(var(--color-bg), color-mix(in srgb, var(--color-bg) 92%, transparent) 82%, transparent); }
.ai-details-panel { width: 302px; padding: 30px 21px 24px; border-left-color: color-mix(in srgb, #0b8fd3 20%, var(--color-border)); background: linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 94%, #0b8fd3), var(--color-surface)); }
.section-label { font-size: 10px; letter-spacing: .16em; }
.section-title { margin-bottom: 0; font-size: 16px; font-weight: 800; letter-spacing: -.025em; text-transform: none; }
.quick-links { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 9px; margin-top: 14px; }
.q-link { display: flex; align-items: center; gap: 8px; min-height: 58px; padding: 10px; border-color: color-mix(in srgb, #0b8fd3 19%, var(--color-border)); border-radius: 13px; background: color-mix(in srgb, var(--color-surface-hover) 65%, var(--color-surface)); font-size: 11px; font-weight: 750; line-height: 1.3; }
.q-link i { width: 25px; flex: 0 0 25px; color: #0b8fd3; text-align: center; font-size: 15px; }
.q-link:hover { transform: translateY(-2px); box-shadow: 0 8px 18px color-mix(in srgb, #0b8fd3 12%, transparent); }
.quick-more-toggle { display: flex; align-items: center; justify-content: space-between; width: 100%; min-height: 42px; margin-top: 12px; padding: 0 11px; border: 1px solid color-mix(in srgb, #0b8fd3 22%, var(--color-border)); border-radius: 11px; background: color-mix(in srgb, var(--color-surface) 82%, #0b8fd3); color: var(--color-text-secondary); cursor: pointer; font: inherit; font-size: 11px; font-weight: 750; }
.quick-more-toggle span { display: inline-flex; align-items: center; gap: 8px; }
.quick-more-toggle span i { color: #0b8fd3; }
.quick-more-toggle > i { transition: transform 180ms ease; }
.quick-more-toggle[aria-expanded='true'] > i { transform: rotate(180deg); }
.quick-links-more { margin-top: 9px; padding-top: 9px; border-top: 1px solid color-mix(in srgb, #0b8fd3 15%, var(--color-border)); }
.sidebar-copy { margin: 10px 0 0; font-size: 12px; }
.upgrade-card-wrapper { margin-top: 34px; }
.upgrade-card { padding: 20px; border-color: color-mix(in srgb, #0b8fd3 38%, var(--color-border)); border-radius: 18px; background: linear-gradient(145deg, color-mix(in srgb, #0b8fd3 18%, var(--color-surface)), var(--color-surface) 72%); box-shadow: 0 16px 32px color-mix(in srgb, #0b8fd3 12%, transparent); }
.credit-balance { font-size: 29px; }
.btn-upgrade { width: 100%; min-height: 42px; border-radius: 11px; }

@media (max-width: 1100px) { .ai-container { padding: 20px 18px 0; } .ai-details-panel { padding: 22px 18px; } }
@media (max-width: 620px) { .ai-container { padding: 16px 14px 0; } .header-left { align-items: flex-start; } .workspace-context-pill { max-width: min(100%, 300px); } .chat-history { padding-bottom: 12px; } .ai-chat-input-wrapper { padding: 10px 0 calc(12px + env(safe-area-inset-bottom)); } .quick-links { grid-template-columns: 1fr 1fr; } }

/* Focused layout pass: keep the utility rail self-contained while giving the
   conversation and current scope the strongest hierarchy. */
.ai-page-flex-wrapper {
  overflow: hidden;
}

.ai-page-history {
  box-sizing: border-box;
  min-height: 0;
  overflow-y: auto;
  scrollbar-color: var(--color-border) transparent;
}

.ai-page-history-head > div {
  gap: 4px;
}

.ai-page-history-head small {
  color: var(--color-text-muted);
  font-size: 11px;
}

.history-new-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  width: 100%;
  min-height: 40px;
  border: 1px solid var(--color-accent);
  border-radius: 10px;
  background: var(--color-accent);
  color: var(--color-on-accent, #fff);
  font-size: 12px;
  font-weight: 850;
  cursor: pointer;
  box-shadow: 0 8px 18px color-mix(in srgb, var(--color-accent) 16%, transparent);
}

.history-new-button:hover,
.history-new-button:focus-visible {
  filter: brightness(1.05);
  outline: 2px solid var(--color-accent);
  outline-offset: 2px;
}

.history-search {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 38px;
  padding: 0 10px;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-bg);
  color: var(--color-text-muted);
}

.history-search:focus-within {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 2px var(--sa-primary-soft);
}

.history-search input {
  width: 100%;
  min-width: 0;
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--color-text-primary);
  font: inherit;
  font-size: 12px;
}

.history-groups {
  display: grid;
  gap: 18px;
}

.history-group {
  display: grid;
  gap: 6px;
}

.history-group h3 {
  margin: 0 4px;
  color: var(--color-text-muted);
  font-size: 10px;
  font-weight: 850;
  letter-spacing: .1em;
  text-transform: uppercase;
}

.ai-page-history-item {
  grid-template-columns: 28px minmax(0, 1fr) 12px;
  align-items: center;
  min-height: 58px;
  gap: 8px;
  padding: 10px 9px;
}

.history-item-icon {
  display: grid;
  place-items: center;
  width: 28px;
  height: 28px;
  border-radius: 8px;
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface));
  color: var(--color-accent);
}

.history-item-content {
  display: grid;
  min-width: 0;
  gap: 4px;
}

.ai-page-history-item strong {
  font-size: 12px;
  line-height: 1.35;
}

.ai-page-history-item small {
  font-size: 10px;
}

.history-item-arrow {
  color: var(--color-text-muted);
  font-size: 9px;
  opacity: .7;
}

.ai-page-header {
  display: grid;
  gap: 16px;
  flex: 0 0 auto;
  min-height: 0;
  margin-bottom: 18px;
}

.ai-page-header-top {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}

.page-title-line {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 10px;
}

.page-subtitle {
  display: block;
  margin-top: 5px;
  color: var(--color-text-muted);
  font-size: 12px;
}

.ai-context-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 18px;
  padding: 14px 16px;
  border: 1px solid color-mix(in srgb, var(--color-accent) 24%, var(--color-border));
  border-radius: 14px;
  background: linear-gradient(110deg, color-mix(in srgb, var(--color-accent) 9%, var(--color-surface)), var(--color-surface) 72%);
  box-shadow: 0 8px 22px color-mix(in srgb, var(--color-text-primary) 5%, transparent);
}

.context-summary {
  display: grid;
  min-width: 0;
  gap: 4px;
}

.context-kicker {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--color-accent);
  font-size: 10px;
  font-weight: 900;
  letter-spacing: .1em;
}

.context-summary strong {
  overflow: hidden;
  color: var(--color-text-primary);
  font-size: 15px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.context-summary small {
  color: var(--color-text-secondary);
  font-size: 11px;
}

.context-controls {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  flex-wrap: wrap;
  gap: 7px;
}

.context-controls .workspace-context-pill {
  flex: 0 0 auto;
  max-width: none;
  background: color-mix(in srgb, var(--color-accent) 8%, var(--color-surface));
  font-weight: 800;
}

.context-controls .credit-pill {
  font-size: 11px;
}

.ai-details-panel {
  box-sizing: border-box;
  flex: 0 0 302px;
  min-height: 0;
  max-height: 100%;
  overflow-y: auto;
  overscroll-behavior: contain;
  scrollbar-color: var(--color-border) transparent;
}

.ai-details-panel > * {
  flex: 0 0 auto;
}

.upgrade-card-wrapper {
  margin-top: 24px;
  padding-bottom: 2px;
}

.upgrade-card {
  padding: 16px;
}

.credit-balance {
  font-size: 24px;
}

@media (max-width: 1100px) {
  .ai-page-header-top,
  .ai-context-bar {
    align-items: flex-start;
    flex-direction: column;
  }

  .context-controls {
    justify-content: flex-start;
    width: 100%;
  }
}

@media (max-width: 620px) {
  .ai-page-header-top {
    gap: 12px;
  }

  .header-actions {
    width: 100%;
  }

  .context-summary strong {
    white-space: normal;
  }

  .context-controls {
    align-items: stretch;
  }

  .context-controls .workspace-selector {
    flex: 1 1 170px;
  }
}

/* Responsive contract: the desktop shell keeps three bounded columns; below
   tablet width, history becomes a drawer and the utility rail is removed so
   the conversation remains the only expanding surface. */
.ai-page-flex-wrapper,
.ai-container,
.ai-page-header,
.ai-context-bar,
.context-summary,
.context-controls,
.chat-history,
.ai-details-panel {
  min-width: 0;
}

.ai-page-flex-wrapper { width: 100%; max-width: none; overflow: hidden; }
.ai-container { min-height: 0; overflow-x: hidden; }
.ai-context-bar {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto minmax(140px, .55fr);
  align-items: center;
}
.current-page-summary {
  display: grid;
  min-width: 0;
  gap: 4px;
  justify-items: end;
}
.current-page-summary span {
  color: var(--color-text-muted);
  font-size: 9px;
  font-weight: 850;
  letter-spacing: .1em;
}
.current-page-summary strong {
  max-width: 100%;
  overflow: hidden;
  color: var(--color-text-secondary);
  font-size: 11px;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ai-details-panel { overflow-x: hidden; }

@media (max-width: 1100px) {
  .ai-context-bar { display: flex; align-items: flex-start; }
  .current-page-summary { width: 100%; justify-items: start; }
  .ai-details-panel { display: none; }
}

@media (max-width: 620px) {
  .ai-context-bar { gap: 12px; }
  .context-summary strong { white-space: normal; overflow-wrap: anywhere; }
  .current-page-summary strong { white-space: normal; overflow-wrap: anywhere; }
}
</style>
