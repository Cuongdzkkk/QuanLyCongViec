<template>
  <div class="dashboard-layout">
    <AppTopBar
      :sidebarVisible="sidebarVisible"
      :showSidebar="!hideSidebar"
      @toggle-sidebar="toggleSidebar"
      @toggle-ai="toggleAI"
      @toggle-create="toggleCreate"
    />

    <div class="main-body">
      <div
        v-if="sidebarVisible && isMobile"
        class="sidebar-overlay"
        @click="sidebarVisible = false"
      ></div>

      <NexusSidebar v-if="!hideSidebar" :isVisible="sidebarVisible" @close-mobile="sidebarVisible = false" />

      <main class="content-area" :class="{ 'is-project-context': route.path.startsWith('/space/') }">
        <div class="content-wrapper">
          <slot></slot>
        </div>
      </main>
    </div>

    <button
      class="ai-floating-btn ai-pet"
      type="button"
      :title="aiCopy.floatingTitle"
      :aria-label="aiCopy.floatingTitle"
      aria-controls="ai-copilot-panel"
      :aria-expanded="aiVisible"
      :aria-pressed="petPinned"
      :class="{ 'is-dragging': petDragging }"
      :style="petStyle"
      @pointerdown="beginPetDrag"
      @click="openFromPet"
    >
      <img class="ai-pet-image" :src="petAsset" alt="" aria-hidden="true" draggable="false" />
    </button>

    <div
      ref="stickyLauncherRef"
      class="global-utility-rail"
      :class="{ 'is-dragging': stickyLauncherDragging }"
      :style="stickyLauncherStyle"
      aria-label="Công cụ nhanh"
    >
      <button
        class="sticky-launcher-handle"
        type="button"
        title="Kéo để di chuyển ghi chú"
        aria-label="Kéo để di chuyển launcher ghi chú theo chiều dọc"
        @pointerdown="beginStickyLauncherDrag"
      >
        <i class="fa-solid fa-grip-lines-vertical" aria-hidden="true"></i>
      </button>
      <button
        class="sticky-launcher-main"
        type="button"
        :class="{ active: notesVisible }"
        title="Mở ghi chú nhanh"
        aria-controls="global-stickies-drawer"
        :aria-expanded="notesVisible"
        @click="openNotesFromLauncher"
      >
        <i class="fa-solid fa-note-sticky" aria-hidden="true"></i>
        <span>Ghi chú</span>
      </button>
      <button
        class="sticky-launcher-add"
        type="button"
        title="Tạo ghi chú mới"
        aria-label="Tạo ghi chú mới"
        :disabled="stickyLauncherCreating"
        @click="quickCreateSticky"
      >
        <i :class="stickyLauncherCreating ? 'fa-solid fa-spinner fa-spin' : 'fa-solid fa-plus'" aria-hidden="true"></i>
      </button>
    </div>

    <div
      v-if="selectedText && selectionPopover.visible && !aiVisible"
      class="ai-selection-popover"
      :style="{ left: `${selectionPopover.left}px`, top: `${selectionPopover.top}px` }"
      role="toolbar"
      aria-label="Thao tác với đoạn văn bản đã chọn"
    >
      <button type="button" @click="askAboutSelection('Giải thích')">Giải thích</button>
      <button type="button" @click="askAboutSelection('Tóm tắt')">Tóm tắt</button>
      <button type="button" @click="askAboutSelection('Hỏi AI')">Hỏi AI</button>
      <button type="button" @click="askAboutSelection('Đề xuất công việc')">Đề xuất công việc</button>
    </div>

    <transition name="ai-backdrop-fade">
      <div v-if="aiVisible && isMobile" class="ai-mobile-backdrop" @click="toggleAI"></div>
    </transition>

    <transition name="slide-right">
      <aside
        v-if="aiVisible"
        id="ai-copilot-panel"
        class="ai-sidebar"
        :class="{ 'is-resizing': aiPanelResizing }"
        :style="{ '--ai-panel-width': `${aiPanelSize.width}px`, '--ai-panel-height': `${aiPanelSize.height}px` }"
        role="dialog"
        aria-modal="false"
        :aria-label="aiCopy.title"
      >
        <div class="ai-resize-handle" role="separator" aria-orientation="vertical" aria-label="Thay đổi chiều rộng bảng AI" @pointerdown="beginAiPanelResize"></div>
        <div class="ai-hero">
          <div class="ai-hero-top">
            <div class="ai-brand">
              <span class="ai-brand-icon"><img src="/ai-sprinta/idle.png" alt="" aria-hidden="true" /></span>
              <div>
                <p>{{ aiCopy.brand }}</p>
                <h4>{{ aiCopy.title }}</h4>
              </div>
            </div>
            <div class="ai-hero-actions">
            <button class="ai-open-full-chat" type="button" title="Đặt lại kích thước bảng AI" aria-label="Đặt lại kích thước bảng AI" @click="resetAiPanelSize">
              <i class="fa-solid fa-arrows-to-dot"></i>
            </button>
            <button class="ai-open-full-chat" type="button" title="Mở full chat" aria-label="Mở full chat" @click="openAiFullChat">
              <i class="fa-solid fa-up-right-and-down-left-from-center"></i>
            </button>
            <button class="close-ai" type="button" :title="aiCopy.closeTitle" :aria-label="aiCopy.closeTitle" @click="toggleAI">
              <i class="fa-solid fa-xmark"></i>
            </button>
            </div>
          </div>
          <p class="ai-hero-copy">{{ aiCopy.hero }}</p>

          <div
            v-if="aiUsage"
            class="ai-credit-card"
            :class="{ 'is-low': aiCreditsLow, 'is-empty': aiCreditsExhausted }"
          >
            <div class="ai-credit-head">
              <div>
                <span class="ai-credit-label">AI CREDITS</span>
                <strong>{{ aiPlanLabel }}</strong>
              </div>
              <strong>{{ aiRemainingCredits }} / {{ aiIncludedCredits }}</strong>
            </div>
            <div class="ai-credit-progress" aria-hidden="true">
              <span :style="{ width: `${aiCreditPercent}%` }"></span>
            </div>
            <p v-if="aiCreditsExhausted" class="ai-credit-message">Bạn đã sử dụng hết AI Credits trong tháng này.</p>
            <p v-else-if="aiCreditsLow" class="ai-credit-message">Bạn sắp hết AI Credits · còn {{ aiRemainingCredits }} credits.</p>
            <p v-else class="ai-credit-message">Còn {{ aiRemainingCredits }} AI Credits trong tháng này.</p>
            <button class="ai-credit-buy" type="button" @click="openAiCreditPurchase">Mua thêm</button>
          </div>
          <button class="ai-pin-toggle" type="button" @click="togglePetPinned">
            <i :class="petPinned ? 'fa-solid fa-thumbtack' : 'fa-solid fa-location-dot'"></i>
            {{ petPinned ? 'Đã ghim vị trí' : 'Thả cho pet di chuyển' }}
          </button>
          <div class="ai-conversation-toolbar">
            <button type="button" title="Cuộc trò chuyện mới" @click="startNewConversation"><i class="fa-solid fa-plus"></i></button>
            <button type="button" title="Lịch sử trò chuyện" @click="toggleConversationHistory"><i class="fa-solid fa-clock-rotate-left"></i></button>
            <span>{{ currentConversationTitle }}</span>
          </div>
        </div>

        <section v-if="conversationHistoryVisible" class="ai-history-panel" aria-label="Lịch sử trò chuyện">
          <div class="ai-history-head">
            <strong>Lịch sử trò chuyện</strong>
            <button type="button" title="Đóng lịch sử" @click="conversationHistoryVisible = false"><i class="fa-solid fa-xmark"></i></button>
          </div>
          <input v-model="conversationSearch" type="search" placeholder="Tìm cuộc trò chuyện" />
          <p v-if="conversationLoading">Đang tải...</p>
          <button v-for="conversation in filteredConversations" :key="conversation.id" type="button" class="ai-history-item" :class="{ active: conversation.id === currentConversationId }" @click="openConversation(conversation.id)">
            <span><strong>{{ conversation.title }}</strong><small>{{ formatConversationDate(conversation.updatedAt) }}</small></span>
            <i class="fa-solid fa-pen" title="Đổi tên" @click.stop="renameConversation(conversation)"></i>
            <i class="fa-solid fa-trash" title="Xóa" @click.stop="deleteConversation(conversation)"></i>
          </button>
          <button v-if="conversationHasMore" class="ai-history-more" type="button" @click="loadConversations(false)">Tải thêm</button>
        </section>

        <div ref="aiContentRef" class="ai-content">
          <div class="quick-actions">
            <button
              v-for="prompt in quickPrompts"
              :key="prompt.text"
              class="quick-action"
              type="button"
              @click="useQuickPrompt(prompt.text)"
            >
              <i :class="prompt.icon"></i>
              <span>{{ prompt.label }}</span>
            </button>
          </div>

          <div class="ai-context-card">
            <div>
              <strong>{{ aiCopy.contextTitle }}</strong>
              <span>{{ currentRouteLabel }}</span>
            </div>
            <button type="button" @click="useQuickPrompt(`${aiCopy.currentPagePrompt}: ${currentRouteLabel}`)">
              <i class="fa-solid fa-wand-magic-sparkles"></i>
            </button>
          </div>

          <div v-if="selectedText" class="ai-selected-text" role="status">
            <i class="fa-solid fa-quote-left"></i>
            <span>Đang dùng đoạn đã chọn</span>
            <button type="button" title="Xóa đoạn đã chọn" @click="clearSelectedText">
              <i class="fa-solid fa-xmark"></i>
            </button>
          </div>

          <div class="chat-thread">
            <AiMessage
              v-for="(sharedMessage, sharedIndex) in chatHistory"
              :key="`shared-${sharedMessage.role}-${sharedIndex}`"
              :message="sharedMessage"
              :profile-avatar="profileAvatar"
              :profile-name="profileName"
              :profile-initials="profileInitials"
              :can-update-task="canUpdateTaskInProject"
              :can-create-task="canCreateTaskInProject"
              @preview-attachment="openAttachmentPreview"
              @open-citation="openCitation"
              @copy="copyAiMessage"
              @continue="continueFromAiMessage"
              @execute-action="executeAiAction"
              @cancel-action="cancelAiAction"
              @retry-action="retryAiAction"
              @quick-prompt="useQuickPrompt"
              @confirm-suggested-action="confirmSuggestedAction"
              @create-suggested-task="createSuggestedTask"
              @create-all-suggested-tasks="createAllSuggestedTasks"
              @open-duplicate-task="openDuplicateTask"
              @confirm-duplicate-creation="confirmDuplicateCreation"
            />
          </div>
        </div>

        <AiComposer
          ref="aiComposerRef"
          :model-value="aiInput"
          :placeholder="aiCopy.placeholder"
          :enter-hint="aiCopy.enterHint"
          :reset-label="aiCopy.reset"
          :sending="aiSending"
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
          @update:model-value="aiInput = $event"
          @update:voice-language="voiceLanguage = $event"
          @update:voice-transcript="voiceTranscript = $event"
          @files="handleAttachmentInput"
          @preview-attachment="openAttachmentPreview"
          @remove-attachment="removePendingAttachment"
          @attachment-command="handleAttachmentCommand"
          @paste="handleComposerPaste"
          @keydown="handleAiComposerKeydown"
          @dragenter="composerDragActive = true"
          @dragleave="handleComposerDragLeave"
          @drop="handleComposerDrop"
          @start-voice="startVoiceRecording"
          @stop-voice="stopVoiceRecording"
          @cancel-voice="cancelVoiceInput"
          @record-again="recordVoiceAgain"
          @use-transcript="useVoiceTranscript"
          @send="sendAiMessage"
          @reset="startNewConversation"
        />
      </aside>
    </transition>

    <FloatingStickiesLayer @floated="closeNotes" />

    <GlobalStickiesDrawer
      :visible="notesVisible"
      :context="stickyContext"
      @close="closeNotes"
    />

    <CreateSpaceModal v-model:visible="createSpaceVisible" @created="handleSpaceCreated" />
    <CreateProjectModal v-model:visible="createVisible" @created="handleProjectCreated" />

    <transition name="fade">
      <div v-if="isOffline" class="offline-warning-banner" role="alert">
        <i class="fa-solid fa-cloud-slash mr-2"></i>
        <span>Bạn đang offline. Một số dữ liệu có thể không cập nhật.</span>
      </div>
    </transition>

    <!-- Persistent Voice Call Dock Overlay (Discord-style) -->
    <Transition name="route-soft">
      <div
        v-if="voiceCallStore.hasActiveCall && route.name !== 'CollaborationChat'"
        class="persistent-call-overlay"
        role="region"
        aria-label="Kênh thoại đang kết nối"
      >
        <div class="call-overlay-info" @click="goToChatCall">
          <span class="call-status-pulse"></span>
          <div>
            <strong>{{ voiceCallStore.activeVoiceChannel?.name || 'Kênh thoại' }}</strong>
            <small>{{ voiceCallStore.participantsCount || 1 }} người trong phòng</small>
          </div>
        </div>

        <div class="call-overlay-actions">
          <button
            type="button"
            class="call-action-pill"
            :class="{ muted: !voiceCallStore.isMicEnabled }"
            :title="voiceCallStore.isMicEnabled ? 'Tắt micro' : 'Bật micro'"
            @click="voiceCallStore.toggleMic()"
          >
            <i :class="voiceCallStore.isMicEnabled ? 'fa-solid fa-microphone' : 'fa-solid fa-microphone-slash'"></i>
          </button>
          
          <button
            type="button"
            class="call-action-pill"
            :class="{ active: voiceCallStore.isCameraEnabled }"
            :title="voiceCallStore.isCameraEnabled ? 'Tắt camera' : 'Bật camera'"
            @click="voiceCallStore.toggleCam()"
          >
            <i :class="voiceCallStore.isCameraEnabled ? 'fa-solid fa-video' : 'fa-solid fa-video-slash'"></i>
          </button>

          <button
            type="button"
            class="call-action-pill open-call"
            title="Mở màn hình cuộc gọi"
            @click="goToChatCall"
          >
            <i class="fa-solid fa-expand"></i>
            <span>Mở màn hình</span>
          </button>

          <button
            type="button"
            class="call-action-pill hang-up"
            title="Rời kênh thoại"
            @click="voiceCallStore.leaveCall()"
          >
            <i class="fa-solid fa-phone-slash"></i>
          </button>
        </div>
      </div>
    </Transition>
  </div>
</template>

<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useRoute, useRouter } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import CreateProjectModal from '../CreateProjectModal.vue'
import CreateSpaceModal from '../CreateSpaceModal.vue'
import AppTopBar from './AppTopBar.vue'
import NexusSidebar from './NexusSidebar.vue'
import GlobalStickiesDrawer from '@/components/stickies/GlobalStickiesDrawer.vue'
import FloatingStickiesLayer from '@/components/stickies/FloatingStickiesLayer.vue'
import AiComposer from '@/components/ai/AiComposer.vue'
import AiMessage from '@/components/ai/AiMessage.vue'
import { useI18nStore } from '@/store/useI18nStore'
import { useAiPetStore } from '@/store/useAiPetStore'
import { useAiConversationStore } from '@/store/useAiConversationStore'
import { useWorkTaskStore } from '@/store/useWorkTaskStore'
import { useProjectStore } from '@/store/useProjectStore'
import { useGoalStore } from '@/store/useGoalStore'
import { useSprintStore } from '@/store/useSprintStore'
import { useVoiceCallStore } from '@/store/useVoiceCallStore'
import { AUTH_SESSION_CHANGED, getStoredUserSession } from '@/utils/authSession'
import { getDefaultPermissionMatrix, hasPermission } from '@/utils/permissionGuard'
import { buildSpacePath } from '@/utils/spaceRoute'
import { MAX_FLOATING_STICKIES, useStickyStore } from '@/store/useStickyStore'
import { getRandomPaletteColor } from '@/utils/colors'
import { getStickyAccountId } from '@/utils/stickyAccountIsolation'
import {
  AI_PANEL_DEFAULT_WIDTH,
  clampAiPanelSize,
  isAiPanelResizable,
  isComposerSendKey,
  readAiPanelSize,
  writeAiPanelSize,
} from '@/utils/aiWorkspace'

const voiceCallStore = useVoiceCallStore()
const goToChatCall = () => {
  router.push('/chat')
}
import {
  STICKY_LAUNCHER_DRAG_THRESHOLD,
  clampStickyLauncherY,
  getStickyLauncherDragY,
  hasStickyLauncherDragged,
  readStickyLauncherY,
  writeStickyLauncherY
} from '@/utils/stickyLauncher'

defineProps({
  hideSidebar: {
    type: Boolean,
    default: false
  }
})

const route = useRoute()
const router = useRouter()
const i18nStore = useI18nStore()
const workTaskStore = useWorkTaskStore()
const projectStore = useProjectStore()
const goalStore = useGoalStore()
const sprintStore = useSprintStore()
const stickyStore = useStickyStore()
const sidebarVisible = ref(window.innerWidth > 1024)
const aiPetStore = useAiPetStore()
const aiConversationStore = useAiConversationStore()
const aiVisible = computed({ get: () => aiPetStore.isPanelOpen, set: value => aiPetStore.setPanelOpen(value) })
const notesVisible = ref(false)
const stickyLauncherRef = ref(null)
const stickyLauncherY = ref(null)
const stickyLauncherDragging = ref(false)
const stickyLauncherCreating = ref(false)
let stickyLauncherDragState = null
const createVisible = ref(false)
const createSpaceVisible = ref(false)
const isMobile = ref(window.innerWidth <= 1024)
const aiInput = ref('')
const aiSending = ref(false)
const aiUsage = ref(null)
const aiContentRef = ref(null)
const aiPanelSize = ref(readAiPanelSize(window.localStorage, {
  width: window.innerWidth,
  height: window.innerHeight,
  topInset: 68
}))
const aiPanelResizing = ref(false)
let aiPanelResizeState = null

const aiIncludedCredits = computed(() => Math.max(0, Number(aiUsage.value?.includedCredits || 0)))
const aiUsedCredits = computed(() => Math.max(0, Number(aiUsage.value?.usedCredits || 0)))
const aiRemainingCredits = computed(() => Math.max(0, Number(
  aiUsage.value?.remainingCredits
  ?? aiUsage.value?.remainingIncludedCredits
  ?? (aiIncludedCredits.value - aiUsedCredits.value)
)))
const aiCreditPercent = computed(() => aiIncludedCredits.value <= 0
  ? 0
  : Math.max(0, Math.min(100, Math.round((aiRemainingCredits.value / aiIncludedCredits.value) * 100))))
const aiCreditsExhausted = computed(() => Boolean(
  aiUsage.value && aiIncludedCredits.value > 0 && aiRemainingCredits.value <= 0
))
const aiCreditsLow = computed(() => Boolean(
  aiUsage.value && !aiCreditsExhausted.value && aiIncludedCredits.value > 0 && aiCreditPercent.value <= 20
))
const aiPlanLabel = computed(() => {
  const plan = String(aiUsage.value?.planCode || 'free').trim()
  return plan ? plan.charAt(0).toUpperCase() + plan.slice(1) : 'Free'
})
const selectedText = ref('')
const selectionPopover = ref({ visible: false, left: 0, top: 0 })
const petPinned = computed({ get: () => aiPetStore.isPinned, set: value => aiPetStore.setPinned(value) })
const petPosition = computed({ get: () => aiPetStore.position, set: value => aiPetStore.setPosition(value) })
const stickyLauncherStyle = computed(() => ({ top: `${stickyLauncherY.value ?? Math.round(window.innerHeight * 0.5)}px` }))
const stickyLauncherAccountId = () => getStickyAccountId(getStoredUserSession())
const getStickyLauncherBounds = () => {
  const launcherHeight = stickyLauncherRef.value?.offsetHeight || 42
  const topInset = Math.max(12, Number.parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--sa-topbar-height')) || 52) + 12
  return { launcherHeight, topInset }
}
const clampStickyLauncherPosition = y => {
  const { launcherHeight, topInset } = getStickyLauncherBounds()
  return clampStickyLauncherY(y, window.innerHeight, launcherHeight, topInset)
}
const restoreStickyLauncherPosition = () => {
  const { launcherHeight, topInset } = getStickyLauncherBounds()
  const accountId = stickyLauncherAccountId()
  const stored = readStickyLauncherY(window.localStorage, accountId, window.innerHeight, launcherHeight, topInset)
  stickyLauncherY.value = stored ?? clampStickyLauncherY((window.innerHeight - launcherHeight) / 2, window.innerHeight, launcherHeight, topInset)
}
const persistStickyLauncherPosition = () => {
  stickyLauncherY.value = clampStickyLauncherPosition(stickyLauncherY.value)
  writeStickyLauncherY(window.localStorage, stickyLauncherAccountId(), stickyLauncherY.value)
}
const beginStickyLauncherDrag = event => {
  if (event.button !== undefined && event.button !== 0) return
  event.preventDefault()
  event.stopPropagation()
  stickyLauncherDragState = {
    pointerId: event.pointerId,
    startX: event.clientX,
    startY: event.clientY,
    originY: stickyLauncherY.value ?? clampStickyLauncherPosition(window.innerHeight / 2),
    moved: false
  }
  event.currentTarget?.setPointerCapture?.(event.pointerId)
  window.addEventListener('pointermove', moveStickyLauncher)
  window.addEventListener('pointerup', endStickyLauncherDrag)
  window.addEventListener('pointercancel', cancelStickyLauncherDrag)
}
const moveStickyLauncher = event => {
  const state = stickyLauncherDragState
  if (!state || event.pointerId !== state.pointerId) return
  if (!state.moved && !hasStickyLauncherDragged(state.startX, state.startY, event.clientX, event.clientY, STICKY_LAUNCHER_DRAG_THRESHOLD)) return
  state.moved = true
  stickyLauncherDragging.value = true
  const { launcherHeight, topInset } = getStickyLauncherBounds()
  stickyLauncherY.value = getStickyLauncherDragY(state.originY, event.clientY - state.startY, window.innerHeight, launcherHeight, topInset)
}
const clearStickyLauncherDrag = () => {
  window.removeEventListener('pointermove', moveStickyLauncher)
  window.removeEventListener('pointerup', endStickyLauncherDrag)
  window.removeEventListener('pointercancel', cancelStickyLauncherDrag)
  stickyLauncherDragging.value = false
  stickyLauncherDragState = null
}
const endStickyLauncherDrag = event => {
  const state = stickyLauncherDragState
  if (!state || event.pointerId !== state.pointerId) return
  if (state.moved) persistStickyLauncherPosition()
  clearStickyLauncherDrag()
}
const cancelStickyLauncherDrag = event => {
  const state = stickyLauncherDragState
  if (!state || event.pointerId !== state.pointerId) return
  stickyLauncherY.value = state.originY
  clearStickyLauncherDrag()
}
const focusCreatedSticky = async note => {
  await nextTick()
  const floatingTitle = document.querySelector(`[data-floating-note-id="${note.id}"] input[aria-label="Tiêu đề ghi chú"]`)
  const drawerTitle = document.querySelector('#global-stickies-drawer input[aria-label="Tiêu đề ghi chú"]')
  ;(floatingTitle || drawerTitle)?.focus()
  ;(floatingTitle || drawerTitle)?.select?.()
}
const quickCreateSticky = async () => {
  if (stickyLauncherCreating.value) return
  if (!stickyStore.canAddFloating) {
    ElMessage.warning(`Bạn chỉ có thể dán tối đa ${MAX_FLOATING_STICKIES} ghi chú. Hãy gỡ một ghi chú khỏi màn hình trước.`)
    openNotesFromLauncher()
    return
  }
  stickyLauncherCreating.value = true
  try {
    const created = await stickyStore.createNote({
      ...stickyContext.value,
      title: 'Ghi chú mới',
      content: '',
      color: getRandomPaletteColor(stickyStore.notes[0]?.color),
      isPinned: false
    })
    const launcherX = Math.max(12, window.innerWidth - 324)
    const launcherY = clampStickyLauncherPosition(stickyLauncherY.value - 92)
    await stickyStore.setFloatingState(created, { isFloating: true, positionX: launcherX, positionY: launcherY })
    closeNotes()
    await focusCreatedSticky(created)
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể tạo ghi chú.')
  } finally {
    stickyLauncherCreating.value = false
  }
}
const petDragging = ref(false)
const petMoved = ref(false)
const petDragOffset = ref({ x: 0, y: 0 })
const aiComposerRef = ref(null)
const pendingAttachments = ref([])
const composerDragActive = ref(false)
const capturingScreenshot = ref(false)
const voiceState = ref('idle')
const voiceLanguage = ref('auto')
const voiceTranscript = ref('')
const voiceError = ref('')
const voiceElapsedSeconds = ref(0)
let wanderTimer = null
let voiceMediaRecorder = null
let voiceMediaStream = null
let voiceChunks = []
let voiceTimer = null
let voiceStartedAt = 0
let voiceRequestId = 0
let voiceDiscardRecording = false
let voiceAbortController = null

const MAX_COMPOSER_ATTACHMENTS = 6
const IMAGE_MAX_BYTES = 5 * 1024 * 1024
const DOCUMENT_MAX_BYTES = 10 * 1024 * 1024
const VOICE_MAX_SECONDS = 60
const VOICE_MAX_BYTES = 3 * 1024 * 1024
const composerAttachmentAccept = [
  '.png', '.jpg', '.jpeg', '.webp', '.pdf', '.docx', '.txt', '.md', '.csv',
  '.xlsx', '.pptx', '.json', '.js', '.ts', '.vue', '.html', '.css', '.scss',
  '.cs', '.java', '.py', '.go', '.sql', '.xml', '.yaml', '.yml', '.sh', '.ps1'
].join(',')

const imageAttachmentRules = {
  '.png': { label: 'PNG', mimeTypes: ['image/png'] },
  '.jpg': { label: 'JPG', mimeTypes: ['image/jpeg'] },
  '.jpeg': { label: 'JPEG', mimeTypes: ['image/jpeg'] },
  '.webp': { label: 'WEBP', mimeTypes: ['image/webp'] }
}

const documentAttachmentRules = {
  '.pdf': { label: 'PDF', mimeTypes: ['application/pdf'] },
  '.docx': { label: 'DOCX', mimeTypes: ['application/vnd.openxmlformats-officedocument.wordprocessingml.document'] },
  '.txt': { label: 'TXT', mimeTypes: ['text/plain'] },
  '.md': { label: 'Markdown', mimeTypes: ['text/markdown', 'text/plain'] },
  '.csv': { label: 'CSV', mimeTypes: ['text/csv', 'application/csv', 'application/vnd.ms-excel'] },
  '.xlsx': { label: 'XLSX', mimeTypes: ['application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'] },
  '.pptx': { label: 'PPTX', mimeTypes: ['application/vnd.openxmlformats-officedocument.presentationml.presentation'] },
  '.json': { label: 'JSON', mimeTypes: ['application/json', 'text/json', 'text/plain'] }
}

const sourceCodeExtensions = new Set([
  '.js', '.ts', '.vue', '.html', '.css', '.scss', '.cs', '.java', '.py', '.go',
  '.sql', '.xml', '.yaml', '.yml', '.sh', '.ps1'
])

const attachmentExtension = (name = '') => {
  const dotIndex = name.lastIndexOf('.')
  return dotIndex >= 0 ? name.slice(dotIndex).toLowerCase() : ''
}

const isSourceCodeMime = (mimeType = '') =>
  !mimeType || mimeType.startsWith('text/') || [
    'application/javascript', 'application/json', 'application/xml', 'application/x-sh'
  ].includes(mimeType)

const attachmentRule = (file) => {
  const extension = attachmentExtension(file.name)
  if (imageAttachmentRules[extension]) return { ...imageAttachmentRules[extension], extension, kind: 'image', maxBytes: IMAGE_MAX_BYTES }
  if (documentAttachmentRules[extension]) return { ...documentAttachmentRules[extension], extension, kind: 'document', maxBytes: DOCUMENT_MAX_BYTES }
  if (sourceCodeExtensions.has(extension)) {
    return { extension, kind: 'document', label: `Source ${extension.slice(1).toUpperCase()}`, maxBytes: DOCUMENT_MAX_BYTES, sourceCode: true }
  }
  return null
}

const attachmentMimeMatches = (file, rule) => {
  const mimeType = (file.type || '').toLowerCase()
  if (!mimeType) return true
  if (rule.sourceCode) return isSourceCodeMime(mimeType)
  return rule.mimeTypes.some(allowed => allowed.toLowerCase() === mimeType)
}

const formatAttachmentBytes = (bytes) => {
  if (!Number.isFinite(bytes) || bytes <= 0) return '0 B'
  const units = ['B', 'KB', 'MB']
  const unitIndex = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1)
  const value = bytes / (1024 ** unitIndex)
  return `${value >= 10 || unitIndex === 0 ? value.toFixed(0) : value.toFixed(1)} ${units[unitIndex]}`
}

const imageDimensions = (objectUrl) => new Promise((resolve, reject) => {
  const image = new Image()
  image.onload = () => resolve({ width: image.naturalWidth, height: image.naturalHeight })
  image.onerror = () => reject(new Error('Không thể đọc nội dung ảnh.'))
  image.src = objectUrl
})

const attachmentIcon = (extension) => {
  if (extension === '.pdf') return 'fa-regular fa-file-pdf'
  if (extension === '.docx') return 'fa-regular fa-file-word'
  if (extension === '.xlsx' || extension === '.csv') return 'fa-regular fa-file-excel'
  if (extension === '.pptx') return 'fa-regular fa-file-powerpoint'
  if (extension === '.json' || sourceCodeExtensions.has(extension)) return 'fa-regular fa-file-code'
  return 'fa-regular fa-file-lines'
}

const addPendingFiles = async (files, source = 'picker') => {
  for (const file of Array.from(files || [])) {
    if (pendingAttachments.value.length >= MAX_COMPOSER_ATTACHMENTS) {
      ElMessage.error(`Chỉ được chọn tối đa ${MAX_COMPOSER_ATTACHMENTS} tệp trong một lượt.`)
      break
    }

    const rule = attachmentRule(file)
    if (!rule) {
      ElMessage.error(`Không hỗ trợ định dạng của tệp “${file.name || 'không tên'}”.`)
      continue
    }
    if (!file.size) {
      ElMessage.error(`Tệp “${file.name}” không có dữ liệu.`)
      continue
    }
    if (file.size > rule.maxBytes) {
      ElMessage.error(`${rule.kind === 'image' ? 'Ảnh' : 'Tài liệu'} “${file.name}” vượt quá giới hạn ${formatAttachmentBytes(rule.maxBytes)}.`)
      continue
    }
    if (!attachmentMimeMatches(file, rule)) {
      ElMessage.error(`Loại nội dung “${file.type || 'không xác định'}” không khớp với ${rule.extension}.`)
      continue
    }

    const duplicate = pendingAttachments.value.some(item =>
      item.name === file.name && item.size === file.size && item.file.lastModified === file.lastModified
    )
    if (duplicate) {
      ElMessage.info(`Tệp “${file.name}” đã có trong danh sách.`)
      continue
    }

    const previewUrl = URL.createObjectURL(file)
    let dimensions = {}
    if (rule.kind === 'image') {
      try {
        dimensions = await imageDimensions(previewUrl)
      } catch (error) {
        URL.revokeObjectURL(previewUrl)
        ElMessage.error(error.message)
        continue
      }
    }

    pendingAttachments.value.push({
      id: crypto.randomUUID(),
      file,
      name: file.name,
      displayName: source === 'paste' ? 'Ảnh đã dán' : source === 'screenshot' ? 'Ảnh chụp màn hình' : file.name,
      size: file.size,
      kind: rule.kind,
      typeLabel: rule.label,
      icon: attachmentIcon(rule.extension),
      previewUrl,
      status: 'pending',
      ...dimensions
    })
  }
}

const removePendingAttachment = (id) => {
  const attachment = pendingAttachments.value.find(item => item.id === id)
  if (attachment?.previewUrl) URL.revokeObjectURL(attachment.previewUrl)
  pendingAttachments.value = pendingAttachments.value.filter(item => item.id !== id)
}

const clearPendingAttachments = () => {
  pendingAttachments.value.forEach(item => item.previewUrl && URL.revokeObjectURL(item.previewUrl))
  pendingAttachments.value = []
}

const openAttachmentPreview = async (attachment) => {
  if (!attachment) return
  try {
    if (!attachment.previewUrl && attachment.contentUrl) {
      const response = await axiosClient.get(attachment.contentUrl, { responseType: 'blob' })
      attachment.previewUrl = URL.createObjectURL(response.data)
    }
    if (!attachment.previewUrl) return
    const link = document.createElement('a')
    link.href = attachment.previewUrl
    link.target = '_blank'
    link.rel = 'noopener noreferrer'
    link.click()
  } catch {
    ElMessage.error(`Không thể mở “${attachment.name}”.`)
  }
}

const handleAttachmentInput = async (event) => {
  await addPendingFiles(event.target.files, 'picker')
  event.target.value = ''
}

const handleComposerPaste = async (event) => {
  const imageFiles = Array.from(event.clipboardData?.files || []).filter(file => file.type.startsWith('image/'))
  if (!imageFiles.length) return
  event.preventDefault()
  await addPendingFiles(imageFiles, 'paste')
}

const readClipboardImage = async () => {
  if (!navigator.clipboard?.read) {
    ElMessage.info('Trình duyệt này chưa hỗ trợ đọc ảnh clipboard. Hãy dùng Ctrl+V trong ô nhập.')
    return
  }
  try {
    const clipboardItems = await navigator.clipboard.read()
    for (const item of clipboardItems) {
      const imageType = item.types.find(type => type.startsWith('image/'))
      if (!imageType) continue
      const blob = await item.getType(imageType)
      const extension = imageType === 'image/jpeg' ? 'jpg' : imageType.split('/')[1]
      const file = new File([blob], `anh-da-dan-${Date.now()}.${extension}`, { type: imageType, lastModified: Date.now() })
      await addPendingFiles([file], 'paste')
      return
    }
    ElMessage.info('Clipboard không có ảnh được hỗ trợ.')
  } catch (error) {
    if (error?.name !== 'NotAllowedError') ElMessage.error('Không thể đọc ảnh từ clipboard.')
  }
}

const captureScreenAttachment = async () => {
  if (!navigator.mediaDevices?.getDisplayMedia || capturingScreenshot.value) {
    ElMessage.info('Trình duyệt này chưa hỗ trợ chụp màn hình.')
    return
  }

  capturingScreenshot.value = true
  let stream
  try {
    stream = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: false })
    const video = document.createElement('video')
    video.srcObject = stream
    video.muted = true
    await new Promise((resolve, reject) => {
      video.onloadedmetadata = resolve
      video.onerror = reject
      video.play().catch(reject)
    })
    const canvas = document.createElement('canvas')
    canvas.width = video.videoWidth
    canvas.height = video.videoHeight
    canvas.getContext('2d')?.drawImage(video, 0, 0)
    const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'))
    if (!blob) throw new Error('Không thể tạo ảnh chụp màn hình.')
    const file = new File([blob], `anh-chup-man-hinh-${Date.now()}.png`, { type: 'image/png', lastModified: Date.now() })
    await addPendingFiles([file], 'screenshot')
  } catch (error) {
    if (error?.name !== 'NotAllowedError') ElMessage.error(error.message || 'Không thể chụp màn hình.')
  } finally {
    stream?.getTracks().forEach(track => track.stop())
    capturingScreenshot.value = false
  }
}

const handleAttachmentCommand = (command) => {
  if (command === 'browse') aiComposerRef.value?.openFilePicker?.()
  if (command === 'paste') readClipboardImage()
  if (command === 'screenshot') captureScreenAttachment()
}

const handleComposerDrop = async (event) => {
  composerDragActive.value = false
  await addPendingFiles(event.dataTransfer?.files, 'drop')
}

const handleComposerDragLeave = (event) => {
  if (!event.currentTarget.contains(event.relatedTarget)) composerDragActive.value = false
}

const voiceLanguageLabel = computed(() => ({
  auto: 'Tự động (VI/EN)',
  vi: 'Tiếng Việt',
  en: 'English'
}[voiceLanguage.value] || 'Tự động (VI/EN)'))

const voiceStatusTitle = computed(() => ({
  requesting: 'Đang xin quyền microphone',
  recording: 'Đang ghi âm',
  transcribing: 'Đang nhận dạng giọng nói',
  success: 'Đã nhận transcript',
  error: 'Không thể nhận dạng giọng nói'
}[voiceState.value] || 'Nhập bằng giọng nói'))

const voiceElapsedLabel = computed(() => {
  const seconds = Math.min(VOICE_MAX_SECONDS, voiceElapsedSeconds.value)
  return `${String(Math.floor(seconds / 60)).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`
})

const stopVoiceTracks = () => {
  voiceMediaStream?.getTracks().forEach(track => track.stop())
  voiceMediaStream = null
}

const clearVoiceTimer = () => {
  if (voiceTimer) window.clearInterval(voiceTimer)
  voiceTimer = null
}

const releaseVoiceAudio = () => {
  voiceChunks = []
  voiceStartedAt = 0
  voiceElapsedSeconds.value = 0
}

const cancelVoiceInput = () => {
  voiceRequestId += 1
  voiceDiscardRecording = true
  voiceAbortController?.abort()
  voiceAbortController = null
  clearVoiceTimer()
  stopVoiceTracks()
  if (voiceMediaRecorder?.state && voiceMediaRecorder.state !== 'inactive') voiceMediaRecorder.stop()
  voiceMediaRecorder = null
  releaseVoiceAudio()
  voiceTranscript.value = ''
  voiceError.value = ''
  voiceState.value = 'idle'
}

const writeWaveString = (view, offset, value) => {
  for (let index = 0; index < value.length; index += 1) view.setUint8(offset + index, value.charCodeAt(index))
}

const encodeWave = (audioBuffer, targetSampleRate = 16000) => {
  const outputLength = Math.max(1, Math.round(audioBuffer.duration * targetSampleRate))
  const samples = new Float32Array(outputLength)
  const channels = Array.from({ length: audioBuffer.numberOfChannels }, (_, index) => audioBuffer.getChannelData(index))
  const sourceStep = audioBuffer.sampleRate / targetSampleRate

  for (let outputIndex = 0; outputIndex < outputLength; outputIndex += 1) {
    const sourcePosition = outputIndex * sourceStep
    const sourceIndex = Math.floor(sourcePosition)
    const nextIndex = Math.min(sourceIndex + 1, audioBuffer.length - 1)
    const fraction = sourcePosition - sourceIndex
    let mixed = 0
    channels.forEach(channel => {
      mixed += channel[sourceIndex] + (channel[nextIndex] - channel[sourceIndex]) * fraction
    })
    samples[outputIndex] = mixed / channels.length
  }

  const waveBuffer = new ArrayBuffer(44 + samples.length * 2)
  const view = new DataView(waveBuffer)
  writeWaveString(view, 0, 'RIFF')
  view.setUint32(4, 36 + samples.length * 2, true)
  writeWaveString(view, 8, 'WAVE')
  writeWaveString(view, 12, 'fmt ')
  view.setUint32(16, 16, true)
  view.setUint16(20, 1, true)
  view.setUint16(22, 1, true)
  view.setUint32(24, targetSampleRate, true)
  view.setUint32(28, targetSampleRate * 2, true)
  view.setUint16(32, 2, true)
  view.setUint16(34, 16, true)
  writeWaveString(view, 36, 'data')
  view.setUint32(40, samples.length * 2, true)
  samples.forEach((sample, index) => {
    const normalized = Math.max(-1, Math.min(1, sample))
    view.setInt16(44 + index * 2, normalized < 0 ? normalized * 0x8000 : normalized * 0x7fff, true)
  })
  return new Blob([waveBuffer], { type: 'audio/wav' })
}

const convertRecordingToWave = async (recording) => {
  const AudioContextClass = window.AudioContext || window.webkitAudioContext
  if (!AudioContextClass) throw new Error('Trình duyệt không hỗ trợ xử lý audio để phiên âm.')
  const audioContext = new AudioContextClass()
  try {
    const source = await recording.arrayBuffer()
    const decoded = await audioContext.decodeAudioData(source.slice(0))
    return encodeWave(decoded)
  } finally {
    await audioContext.close()
  }
}

const transcribeVoiceRecording = async (recording) => {
  try {
    const wave = await convertRecordingToWave(recording)
    if (wave.size > VOICE_MAX_BYTES) throw new Error('Bản ghi âm vượt quá giới hạn 60 giây.')
    if (voiceState.value !== 'transcribing') return

    const form = new FormData()
    form.append('audio', wave, 'voice-recording.wav')
    form.append('languageMode', voiceLanguage.value)
    voiceAbortController = new AbortController()
    const response = await axiosClient.post('/ai/transcribe-audio', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      signal: voiceAbortController.signal
    })
    if (voiceState.value !== 'transcribing') return
    const transcript = String(apiPayload(response)?.transcript || '').trim()
    if (!transcript) throw new Error('Không nhận diện được giọng nói Việt hoặc Anh. Hãy thu lại.')
    voiceTranscript.value = transcript
    voiceState.value = 'success'
  } catch (error) {
    if (error?.code === 'ERR_CANCELED' || voiceState.value === 'idle') return
    voiceError.value = error.response?.data?.message || error.message || 'Không thể nhận dạng giọng nói. Hãy thử lại.'
    voiceState.value = 'error'
  } finally {
    voiceAbortController = null
    releaseVoiceAudio()
  }
}

const stopVoiceRecording = () => {
  if (voiceState.value !== 'recording' || !voiceMediaRecorder || voiceMediaRecorder.state === 'inactive') return
  voiceState.value = 'transcribing'
  clearVoiceTimer()
  stopVoiceTracks()
  voiceMediaRecorder.stop()
}

const startVoiceRecording = async () => {
  if (['requesting', 'recording', 'transcribing'].includes(voiceState.value)) return
  if (!navigator.mediaDevices?.getUserMedia || !window.MediaRecorder) {
    voiceError.value = 'Trình duyệt này không hỗ trợ ghi âm microphone.'
    voiceState.value = 'error'
    return
  }

  voiceRequestId += 1
  const requestId = voiceRequestId
  voiceTranscript.value = ''
  voiceError.value = ''
  voiceDiscardRecording = false
  voiceState.value = 'requesting'
  try {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
    if (requestId !== voiceRequestId || voiceState.value !== 'requesting') {
      stream.getTracks().forEach(track => track.stop())
      return
    }

    const mimeType = ['audio/webm;codecs=opus', 'audio/webm', 'audio/ogg;codecs=opus']
      .find(type => MediaRecorder.isTypeSupported(type))
    voiceMediaStream = stream
    voiceMediaRecorder = mimeType ? new MediaRecorder(stream, { mimeType }) : new MediaRecorder(stream)
    voiceChunks = []
    voiceMediaRecorder.addEventListener('dataavailable', event => {
      if (!voiceDiscardRecording && event.data.size > 0) voiceChunks.push(event.data)
    })
    voiceMediaRecorder.addEventListener('stop', () => {
      const recorderMimeType = voiceMediaRecorder?.mimeType || mimeType || 'audio/webm'
      const recording = new Blob(voiceChunks, { type: recorderMimeType })
      voiceMediaRecorder = null
      if (voiceDiscardRecording || voiceState.value !== 'transcribing') {
        releaseVoiceAudio()
        return
      }
      void transcribeVoiceRecording(recording)
    }, { once: true })
    voiceMediaRecorder.start(250)
    voiceStartedAt = Date.now()
    voiceElapsedSeconds.value = 0
    voiceState.value = 'recording'
    voiceTimer = window.setInterval(() => {
      voiceElapsedSeconds.value = Math.floor((Date.now() - voiceStartedAt) / 1000)
      if (voiceElapsedSeconds.value >= VOICE_MAX_SECONDS) stopVoiceRecording()
    }, 250)
  } catch (error) {
    stopVoiceTracks()
    voiceError.value = error?.name === 'NotAllowedError'
      ? 'Quyền microphone đã bị từ chối. Hãy cho phép quyền trong trình duyệt rồi bấm Thử lại.'
      : error?.name === 'NotFoundError'
        ? 'Không tìm thấy microphone khả dụng trên thiết bị.'
        : 'Không thể mở microphone. Hãy kiểm tra thiết bị và thử lại.'
    voiceState.value = 'error'
  }
}

const recordVoiceAgain = async () => {
  cancelVoiceInput()
  await nextTick()
  await startVoiceRecording()
}

const useVoiceTranscript = async () => {
  const transcript = voiceTranscript.value.trim()
  if (!transcript) return
  cancelVoiceInput()
  aiInput.value = transcript
  await nextTick()
  document.querySelector('.ai-input-wrapper textarea')?.focus()
}

function loadPetPosition() {
  try {
    const stored = JSON.parse(localStorage.getItem('sprinta-ai-pet-position') || 'null')
    if (stored && Number.isFinite(stored.x) && Number.isFinite(stored.y)) return stored
  } catch {}
  return { x: Math.max(12, window.innerWidth - 88), y: Math.max(64, window.innerHeight - 116) }
}

const clampPetPosition = (position = petPosition.value) => ({
  x: Math.min(Math.max(8, position.x), Math.max(8, window.innerWidth - 76)),
  y: Math.min(Math.max(56, position.y), Math.max(56, window.innerHeight - 76))
})

const savePetPosition = () => {
  petPosition.value = clampPetPosition()
  localStorage.setItem('sprinta-ai-pet-position', JSON.stringify(petPosition.value))
}

const petAsset = computed(() => {
  if (petDragging.value || (!petPinned.value && !aiVisible.value && !isMobile.value)) return '/ai-sprinta/walk.png'
  if (selectedText.value && selectionPopover.value.visible) return '/ai-sprinta/guide.png'
  return '/ai-sprinta/idle.png'
})

const petStyle = computed(() => ({
  transform: `translate3d(${petPosition.value.x}px, ${petPosition.value.y}px, 0)`
}))
const storedProfile = computed(() => getStoredUserSession() || {})
const profileAvatar = computed(() => storedProfile.value.avatarUrl || storedProfile.value.AvatarUrl || '')
const profileName = computed(() => storedProfile.value.fullName || storedProfile.value.FullName || storedProfile.value.username || storedProfile.value.email || 'Bạn')
const profileInitials = computed(() => profileName.value.split(/\s+/).filter(Boolean).slice(-2).map(part => part[0]).join('').toUpperCase() || 'B')
const aiCopyMap = {
  vi: {
    floatingTitle: 'Mở AI Assistant',
    closeTitle: 'Đóng AI',
    brand: 'SPRINTA AI',
    title: 'Trợ lý công việc',
    hero: 'Hỏi nhanh, tóm tắt tiến độ, tạo checklist hoặc xin gợi ý ưu tiên ở bất kỳ trang nào.',
    contextTitle: 'Ngữ cảnh hiện tại',
    currentPagePrompt: 'Tóm tắt trang hiện tại',
    botName: 'SprintA AI',
    you: 'Bạn',
    placeholder: 'Hỏi AI về task, dashboard, deadline...',
    enterHint: 'Enter để gửi',
    reset: 'Làm mới',
    thinking: 'Đang đọc ngữ cảnh và suy nghĩ...',
    emptyResponse: 'AI không trả về nội dung.',
    sendFailed: 'Không gửi được tin nhắn tới AI.',
    welcome: 'Xin chào Khôi. Mình sẵn sàng tóm tắt, gợi ý ưu tiên, tạo checklist hoặc phân tích nội dung trên trang hiện tại.',
    prompts: [
      { label: 'Tóm tắt trang', icon: 'fa-regular fa-file-lines', text: 'Tóm tắt trang hiện tại và nêu 3 điểm cần chú ý.' },
      { label: 'Gợi ý ưu tiên', icon: 'fa-solid fa-arrow-up-wide-short', text: 'Gợi ý việc nên làm tiếp theo dựa trên ngữ cảnh hiện tại.' },
      { label: 'Tạo checklist', icon: 'fa-solid fa-list-check', text: 'Tạo checklist ngắn gọn để hoàn thành công việc này.' },
      { label: 'Viết cập nhật', icon: 'fa-solid fa-pen-nib', text: 'Soạn bản cập nhật tiến độ ngắn gọn cho team.' }
    ]
  },
  en: {
    floatingTitle: 'Open AI Assistant',
    closeTitle: 'Close AI',
    brand: 'SPRINTA AI',
    title: 'Work assistant',
    hero: 'Ask quickly, summarize progress, create checklists, or get priority suggestions from any page.',
    contextTitle: 'Current context',
    currentPagePrompt: 'Summarize the current page',
    botName: 'SprintA AI',
    you: 'You',
    placeholder: 'Ask AI about tasks, dashboards, deadlines...',
    enterHint: 'Enter to send',
    reset: 'Reset',
    thinking: 'Reading context and thinking...',
    emptyResponse: 'AI did not return any content.',
    sendFailed: 'Could not send the message to AI.',
    welcome: 'Hi Khoi. I can summarize, suggest priorities, create checklists, or analyze the current page.',
    prompts: [
      { label: 'Summarize page', icon: 'fa-regular fa-file-lines', text: 'Summarize the current page and list 3 key points.' },
      { label: 'Suggest priority', icon: 'fa-solid fa-arrow-up-wide-short', text: 'Suggest what I should do next based on the current context.' },
      { label: 'Create checklist', icon: 'fa-solid fa-list-check', text: 'Create a short checklist to finish this work.' },
      { label: 'Write update', icon: 'fa-solid fa-pen-nib', text: 'Draft a concise progress update for the team.' }
    ]
  }
}

const aiCopyOverrideMap = {
  vi: {
    floatingTitle: 'Mở AI Assistant',
    closeTitle: 'Đóng AI',
    brand: 'SPRINTA AI',
    title: 'Trợ lý công việc',
    hero: 'Hỏi nhanh, tạo task thật, chuyển trạng thái, tóm tắt tiến độ hoặc xem thống kê ở bất kỳ trang nào.',
    contextTitle: 'Ngữ cảnh hiện tại',
    currentPagePrompt: 'Tóm tắt trang hiện tại',
    botName: 'SprintA AI',
    you: 'Bạn',
    placeholder: 'Ví dụ: tạo task sửa UI deadline mai, thống kê project, tóm tắt trang...',
    enterHint: 'Enter để gửi',
    reset: 'Làm mới',
    thinking: 'Đang đọc dữ liệu thật và xử lý...',
    emptyResponse: 'AI không trả về nội dung.',
    sendFailed: 'Không gửi được tin nhắn tới AI.',
    needProject: 'Bạn cần mở một project trước khi yêu cầu AI tạo hoặc cập nhật task.',
    welcome: 'Xin chào Khôi. Mình có thể tạo task thật, chuyển trạng thái task, thống kê project, tóm tắt trang và gợi ý ưu tiên từ dữ liệu hiện tại.',
    prompts: [
      { label: 'Tạo task', icon: 'fa-solid fa-square-plus', text: 'Tạo task mới: Hoàn thiện phần demo hôm nay, deadline ngày mai, ưu tiên cao.' },
      { label: 'Thống kê project', icon: 'fa-solid fa-chart-simple', text: 'Thống kê project hiện tại.' },
      { label: 'Tóm tắt trang', icon: 'fa-regular fa-file-lines', text: 'Tóm tắt trang hiện tại và nêu 3 điểm cần chú ý.' },
      { label: 'Gợi ý ưu tiên', icon: 'fa-solid fa-arrow-up-wide-short', text: 'Gợi ý 5 việc nên làm tiếp theo dựa trên task hiện tại.' }
    ]
  },
  en: {
    floatingTitle: 'Open AI Assistant',
    closeTitle: 'Close AI',
    brand: 'SPRINTA AI',
    title: 'Work assistant',
    hero: 'Ask quickly, create real tasks, move status, summarize progress, or get project statistics from any page.',
    contextTitle: 'Current context',
    currentPagePrompt: 'Summarize the current page',
    botName: 'SprintA AI',
    you: 'You',
    placeholder: 'Try: create task fix UI due tomorrow, project stats, summarize page...',
    enterHint: 'Enter to send',
    reset: 'Reset',
    thinking: 'Reading real data and processing...',
    emptyResponse: 'AI did not return any content.',
    sendFailed: 'Could not send the message to AI.',
    needProject: 'Open a project before asking AI to create or update tasks.',
    welcome: 'Hi Khoi. I can create real tasks, move task status, summarize the page, report project stats, and suggest priorities from the current data.',
    prompts: [
      { label: 'Create task', icon: 'fa-solid fa-square-plus', text: 'Create a new task: Finish today demo, due tomorrow, high priority.' },
      { label: 'Project stats', icon: 'fa-solid fa-chart-simple', text: 'Show stats for the current project.' },
      { label: 'Summarize page', icon: 'fa-regular fa-file-lines', text: 'Summarize the current page and list 3 key points.' },
      { label: 'Suggest priority', icon: 'fa-solid fa-arrow-up-wide-short', text: 'Suggest 5 next actions based on current tasks.' }
    ]
  }
}

const viAiCopy = {
  floatingTitle: 'Mở trợ lý AI', closeTitle: 'Đóng trợ lý AI', brand: 'SPRINTA AI',
  title: 'Trợ lý công việc',
  hero: 'Hỏi nhanh, tóm tắt tiến độ, tạo checklist hoặc xin gợi ý ưu tiên từ trang hiện tại.',
  contextTitle: 'Ngữ cảnh hiện tại', currentPagePrompt: 'Tóm tắt trang hiện tại', botName: 'SprintA AI', you: 'Bạn',
  placeholder: 'Ví dụ: tạo task sửa UI deadline mai, thống kê dự án, tóm tắt trang…', enterHint: 'Enter để gửi', reset: 'Làm mới',
  thinking: 'Đang đọc dữ liệu thật và xử lý…', emptyResponse: 'AI chưa trả về nội dung.', sendFailed: 'Không gửi được tin nhắn tới AI.',
  needProject: 'Bạn cần mở một dự án trước khi yêu cầu AI tạo hoặc cập nhật task.',
  welcome: 'Xin chào Khôi. Mình có thể tạo task thật, cập nhật trạng thái, tóm tắt trang và gợi ý ưu tiên từ dữ liệu hiện tại.',
  prompts: [
    { label: 'Tạo task', icon: 'fa-solid fa-square-plus', text: 'Tạo task mới: Hoàn thiện phần demo hôm nay, deadline ngày mai, ưu tiên cao.' },
    { label: 'Thống kê dự án', icon: 'fa-solid fa-chart-simple', text: 'Thống kê dự án hiện tại.' },
    { label: 'Tóm tắt trang', icon: 'fa-regular fa-file-lines', text: 'Tóm tắt trang hiện tại và nêu 3 điểm cần chú ý.' },
    { label: 'Gợi ý ưu tiên', icon: 'fa-solid fa-arrow-up-wide-short', text: 'Gợi ý 5 việc nên làm tiếp theo dựa trên task hiện tại.' }
  ]
}
const aiCopy = computed(() => i18nStore.locale === 'en' ? aiCopyOverrideMap.en : viAiCopy)

const pageSuggestions = {
  'work-items': ['Tóm tắt tình hình dự án này', 'Công việc nào đang trễ hạn?', 'Ai đang bị quá tải?', 'Gợi ý ưu tiên hôm nay', 'Giải thích các cột Kanban hiện tại'],
  reports: ['Báo cáo này đang nói điều gì?', 'Rủi ro lớn nhất của dự án là gì?', 'Nên xử lý vấn đề nào trước?'],
  settings: ['Giải thích quyền của tôi trong dự án này', 'Workflow hiện tại có hợp lý không?', 'Custom Fields này dùng để làm gì?'],
  goals: ['Tóm tắt tiến độ mục tiêu', 'Mục tiêu nào đang có nguy cơ?', 'Đề xuất việc cần làm để tăng tiến độ'],
  integration: ['Tóm tắt các item mới', 'Item nào nên chuyển thành công việc?', 'Có nội dung nào cần xử lý gấp?'],
  inbox: ['Tóm tắt các item mới', 'Item nào nên chuyển thành công việc?', 'Có nội dung nào cần xử lý gấp?'],
  dashboard: ['Tóm tắt dashboard hiện tại', 'Rủi ro nào cần xử lý trước?', 'Gợi ý ưu tiên hôm nay'],
  unknown: ['Tôi có thể giúp gì cho bạn trong SprintA?', 'Tóm tắt trang hiện tại', 'Giải thích đoạn đã chọn']
}

const inferPageType = (path = '') => {
  const value = path.toLowerCase()
  if (value.includes('work-items') || value.includes('kanban')) return 'work-items'
  if (value.includes('report')) return 'reports'
  if (value.includes('setting')) return 'settings'
  if (value.includes('goal')) return 'goals'
  if (value.includes('integration')) return 'integration'
  if (value.includes('inbox')) return 'inbox'
  if (value.includes('dashboard')) return 'dashboard'
  return 'unknown'
}

const pageType = computed(() => inferPageType(route.path))
const localizedPageSuggestions = {
  'work-items': ['Tóm tắt tình hình dự án này', 'Công việc nào đang trễ hạn?', 'Ai đang bị quá tải?', 'Gợi ý ưu tiên hôm nay', 'Giải thích các cột Kanban hiện tại'],
  reports: ['Báo cáo này đang nói điều gì?', 'Rủi ro lớn nhất của dự án là gì?', 'Nên xử lý vấn đề nào trước?'],
  settings: ['Giải thích quyền của tôi trong dự án này', 'Quy trình hiện tại có hợp lý không?', 'Trường tùy chỉnh này dùng để làm gì?'],
  goals: ['Tóm tắt tiến độ mục tiêu', 'Mục tiêu nào đang có nguy cơ?', 'Đề xuất việc cần làm để tăng tiến độ'],
  dashboard: ['Tóm tắt dashboard hiện tại', 'Rủi ro nào cần xử lý trước?', 'Gợi ý ưu tiên hôm nay'],
  unknown: ['Tôi có thể giúp gì cho bạn trong SprintA?', 'Tóm tắt trang hiện tại', 'Giải thích đoạn đã chọn']
}
const quickPrompts = computed(() => (localizedPageSuggestions[pageType.value] || localizedPageSuggestions.unknown)
  .map((text, index) => ({
    label: text,
    text,
    icon: ['fa-regular fa-file-lines', 'fa-solid fa-arrow-up-wide-short', 'fa-solid fa-lightbulb'][index % 3]
  })))

const chatHistory = computed({
  get: () => aiConversationStore.messages,
  set: value => { aiConversationStore.messages = value }
})
const conversations = computed({
  get: () => aiConversationStore.conversations,
  set: value => { aiConversationStore.conversations = value }
})
const currentConversationId = computed({
  get: () => aiConversationStore.currentConversationId,
  set: value => { aiConversationStore.currentConversationId = value }
})
const currentConversationWorkspaceId = computed({
  get: () => aiConversationStore.currentConversationWorkspaceId,
  set: value => { aiConversationStore.currentConversationWorkspaceId = value }
})
const currentConversationTitle = computed({
  get: () => aiConversationStore.currentConversationTitle,
  set: value => { aiConversationStore.currentConversationTitle = value }
})
const conversationHistoryVisible = computed({
  get: () => aiConversationStore.historyVisible,
  set: value => { aiConversationStore.historyVisible = value }
})
const conversationSearch = computed({
  get: () => aiConversationStore.search,
  set: value => { aiConversationStore.search = value }
})
const conversationLoading = computed({
  get: () => aiConversationStore.loading,
  set: value => { aiConversationStore.loading = value }
})
const conversationHasMore = computed({
  get: () => aiConversationStore.hasMore,
  set: value => { aiConversationStore.hasMore = value }
})
const filteredConversations = computed(() => aiConversationStore.filteredConversations)

const apiPayload = (response) => response?.data?.data ?? response?.data ?? response
const formatConversationDate = (value) => value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)) : ''

const loadConversations = async (reset = true) => {
  if (conversationLoading.value) return
  try {
    await aiConversationStore.loadConversations({ workspaceId: currentWorkspaceId.value, reset })
  } catch (error) {
    const status = error?.response?.status
    const message = status === 429
      ? 'Lịch sử trò chuyện đang bị giới hạn tạm thời. Hãy thử lại sau vài giây.'
      : status === 403
        ? 'Bạn không có quyền xem lịch sử trò chuyện trong workspace này.'
        : 'Không thể tải lịch sử trò chuyện.'
    ElMessage.warning(message)
    conversationHasMore.value = false
  }
}

const toggleConversationHistory = async () => {
  conversationHistoryVisible.value = !conversationHistoryVisible.value
  if (conversationHistoryVisible.value) await loadConversations(true)
}

const startNewConversation = () => {
  releaseMessageAttachmentUrls()
  aiConversationStore.startNewConversation()
  clearPendingAttachments()
}

const ensureConversation = async (firstMessage) => {
  return aiConversationStore.ensureConversation({ workspaceId: currentWorkspaceId.value, firstMessage })
}

const releaseMessageAttachmentUrls = () => {
  chatHistory.value.forEach(message => message.attachments?.forEach((attachment) => {
    if (attachment.previewUrl?.startsWith('blob:')) URL.revokeObjectURL(attachment.previewUrl)
  }))
}

const persistConversation = async () => {
  if (!currentConversationId.value) return
  try {
    await aiConversationStore.persistConversation()
  } catch {
    ElMessage.warning('Chưa thể lưu lịch sử trò chuyện. Hãy kiểm tra kết nối.')
  }
}

const openConversation = async (id) => {
  releaseMessageAttachmentUrls()
  await aiConversationStore.openConversation(id)
  await hydrateConversationImages()
  await scrollAiToBottom()
}

const hydrateConversationImages = async () => {
  const images = chatHistory.value.flatMap(message => message.attachments || []).filter(attachment => attachment.kind === 'image' && attachment.contentUrl)
  await Promise.all(images.map(async (attachment) => {
    try {
      const response = await axiosClient.get(attachment.contentUrl, { responseType: 'blob' })
      attachment.previewUrl = URL.createObjectURL(response.data)
    } catch {
      attachment.previewUrl = ''
    }
  }))
}

const openCitation = (citation) => {
  const attachment = chatHistory.value
    .flatMap(message => message.attachments || [])
    .find(item => item.id === citation.attachmentId)
  if (attachment) openAttachmentPreview(attachment)
}

const renameConversation = async (conversation) => {
  try {
    const result = await ElMessageBox.prompt('Nhập tên cuộc trò chuyện', 'Đổi tên', { inputValue: conversation.title, inputPattern: /\S+/, inputErrorMessage: 'Tên không được để trống' })
    const response = await axiosClient.patch(`/ai/conversations/${conversation.id}/title`, { title: result.value })
    const updated = apiPayload(response)
    conversation.title = updated.title
    if (currentConversationId.value === conversation.id) currentConversationTitle.value = updated.title
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') ElMessage.error('Không thể đổi tên cuộc trò chuyện.')
  }
}

const deleteConversation = async (conversation) => {
  try {
    await ElMessageBox.confirm(`Xóa "${conversation.title}"?`, 'Xóa cuộc trò chuyện', { type: 'warning' })
    await axiosClient.delete(`/ai/conversations/${conversation.id}`)
    conversations.value = conversations.value.filter(item => item.id !== conversation.id)
    if (currentConversationId.value === conversation.id) startNewConversation()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') ElMessage.error('Không thể xóa cuộc trò chuyện.')
  }
}

const currentRouteLabel = computed(() => {
  const name = route.meta?.title || route.name || route.path
  return typeof name === 'string' ? name : route.path
})

const resetAiPanelSize = () => {
  aiPanelSize.value = clampAiPanelSize({ width: AI_PANEL_DEFAULT_WIDTH, height: window.innerHeight }, {
    width: window.innerWidth,
    height: window.innerHeight,
    topInset: 68
  })
  writeAiPanelSize(window.localStorage, aiPanelSize.value)
}

const beginAiPanelResize = (event) => {
  if (!isAiPanelResizable(window.innerWidth) || (event.button !== undefined && event.button !== 0)) return
  event.preventDefault()
  aiPanelResizeState = { pointerId: event.pointerId, startX: event.clientX, startWidth: aiPanelSize.value.width }
  aiPanelResizing.value = true
  event.currentTarget?.setPointerCapture?.(event.pointerId)
  window.addEventListener('pointermove', moveAiPanelResize)
  window.addEventListener('pointerup', endAiPanelResize)
  window.addEventListener('pointercancel', endAiPanelResize)
}

const moveAiPanelResize = (event) => {
  const state = aiPanelResizeState
  if (!state || event.pointerId !== state.pointerId) return
  aiPanelSize.value = clampAiPanelSize({
    ...aiPanelSize.value,
    width: state.startWidth + state.startX - event.clientX
  }, { width: window.innerWidth, height: window.innerHeight, topInset: 68 })
}

const endAiPanelResize = (event) => {
  if (!aiPanelResizeState || event.pointerId !== aiPanelResizeState.pointerId) return
  writeAiPanelSize(window.localStorage, aiPanelSize.value)
  aiPanelResizeState = null
  aiPanelResizing.value = false
  window.removeEventListener('pointermove', moveAiPanelResize)
  window.removeEventListener('pointerup', endAiPanelResize)
  window.removeEventListener('pointercancel', endAiPanelResize)
}

const openAiFullChat = async () => {
  aiVisible.value = false
  await router.push({ name: 'AIPage' })
}

const openAiCreditPurchase = () => router.push('/#pricing')

const handleAiComposerKeydown = (event) => {
  if (!isComposerSendKey(event)) return
  event.preventDefault()
  sendAiMessage()
}

const updateSize = () => {
  isMobile.value = window.innerWidth <= 1024
  if (!isMobile.value) {
    sidebarVisible.value = true
  }
  aiPanelSize.value = clampAiPanelSize(aiPanelSize.value, {
    width: window.innerWidth,
    height: window.innerHeight,
    topInset: 68
  })
  petPosition.value = clampPetPosition()
  nextTick(() => window.setTimeout(normalizePetPosition, 80))
  if (isMobile.value || aiVisible.value) stopPetWandering()
  else startPetWandering()
}

const isOffline = ref(!navigator.onLine)
const updateOnlineStatus = () => {
  isOffline.value = !navigator.onLine
}

const persistPetPinned = () => localStorage.setItem('sprinta-ai-pet-pinned', String(petPinned.value))

const togglePetPinned = () => {
  petPinned.value = !petPinned.value
  persistPetPinned()
  if (!petPinned.value && !isMobile.value && !aiVisible.value) startPetWandering()
  else stopPetWandering()
}

const stopPetWandering = () => {
  if (wanderTimer) window.clearInterval(wanderTimer)
  wanderTimer = null
}

const petOverlapsUnsafeZone = (position) => {
  const petRect = { left: position.x, top: position.y, right: position.x + 68, bottom: position.y + 68 }
  const selectors = [
    { selector: '.app-topbar', minOverlap: 1 },
    { selector: '.plane-sidebar', minOverlap: 1 },
    { selector: '.ai-sidebar', minOverlap: 1 },
    { selector: '.el-overlay', minOverlap: 1 },
    { selector: '.el-dialog', minOverlap: 1 },
    { selector: '.modal-content', minOverlap: 1 },
    { selector: '[role="dialog"]', minOverlap: 1 },
    { selector: '.report-card', minOverlap: 1200 },
    { selector: '.health-alert-card', minOverlap: 1200 },
    { selector: '.reports-stats-grid', minOverlap: 1200 },
    { selector: '.page-editor', minOverlap: 1200 },
    { selector: '.editor-content', minOverlap: 1200 },
    { selector: '.nexus-btn-primary', minOverlap: 800 },
    { selector: '.project-tabs', minOverlap: 1 },
    { selector: '.project-tab', minOverlap: 1 },
    { selector: '.project-nav', minOverlap: 1 },
    { selector: '.space-tabs', minOverlap: 1 },
    { selector: '.workspace-nav', minOverlap: 1 },
    { selector: '.nav-tabs', minOverlap: 1 },
    { selector: '.project-page-header', minOverlap: 1 },
    { selector: '.project-global-header', minOverlap: 1 },
    { selector: '.project-horizontal-nav', minOverlap: 1 },
    { selector: '.nav-item', minOverlap: 1 }
  ]
  return selectors.some(({ selector, minOverlap }) => [...document.querySelectorAll(selector)].some(element => {
    const rect = element.getBoundingClientRect()
    if (rect.width <= 0 || rect.height <= 0) return false
    const overlapX = Math.max(0, Math.min(petRect.right, rect.right) - Math.max(petRect.left, rect.left))
    const overlapY = Math.max(0, Math.min(petRect.bottom, rect.bottom) - Math.max(petRect.top, rect.top))
    return overlapX * overlapY >= minOverlap
  }))
}

const edgePetPosition = () => clampPetPosition({
  x: window.innerWidth - 76,
  y: Math.max(96, Math.min(window.innerHeight - 96, Math.round(window.innerHeight * 0.82)))
})

const chooseSafePetPosition = () => {
  const current = clampPetPosition()
  const edge = edgePetPosition()
  if (!petOverlapsUnsafeZone(edge)) return edge
  for (let attempt = 0; attempt < 12; attempt += 1) {
    const candidate = clampPetPosition({
      x: 24 + Math.random() * Math.max(24, window.innerWidth - 116),
      y: Math.max(220, 160 + Math.random() * Math.max(30, window.innerHeight - 246))
    })
    if (!petOverlapsUnsafeZone(candidate)) return candidate
  }
  return petOverlapsUnsafeZone(current) ? edge : current
}

const normalizePetPosition = () => {
  if (petDragging.value || isMobile.value) return
  const current = clampPetPosition()
  if (petOverlapsUnsafeZone(current)) {
    petPosition.value = chooseSafePetPosition()
  } else {
    petPosition.value = current
  }
  savePetPosition()
}

const startPetWandering = () => {
  stopPetWandering()
  if (petPinned.value || isMobile.value || aiVisible.value || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return
  wanderTimer = window.setInterval(() => {
    if (petPinned.value || isMobile.value || aiVisible.value || petDragging.value || document.querySelector('.el-overlay')) return
    petPosition.value = chooseSafePetPosition()
    savePetPosition()
  }, 20000)
}

const beginPetDrag = (event) => {
  if (event.button !== undefined && event.button !== 0) return
  petDragging.value = true
  petMoved.value = false
  petDragOffset.value = { x: event.clientX - petPosition.value.x, y: event.clientY - petPosition.value.y }
  event.currentTarget?.setPointerCapture?.(event.pointerId)
}

const movePet = (event) => {
  if (!petDragging.value) return
  petMoved.value = true
  petPosition.value = clampPetPosition({
    x: event.clientX - petDragOffset.value.x,
    y: event.clientY - petDragOffset.value.y
  })
}

const endPetDrag = () => {
  if (!petDragging.value) return
  petDragging.value = false
  savePetPosition()
  window.setTimeout(() => { petMoved.value = false }, 0)
  startPetWandering()
}

const openFromPet = (event) => {
  if (petMoved.value) {
    event.preventDefault()
    return
  }
  toggleAI()
}

const handleGlobalKeydown = (event) => {
  const isEscape = event.key === 'Escape' || event.key === 'Esc' || event.code === 'Escape' || event.keyCode === 27
  if (!isEscape) return
  // Element Plus owns Escape while a real modal overlay is open. The AI panel
  // is not an overlay, so only close it when no modal is currently active.
  const hasActiveElementPlusOverlay = [...document.querySelectorAll('.el-overlay')].some((overlay) => {
    const style = window.getComputedStyle(overlay)
    return style.display !== 'none' && style.visibility !== 'hidden' && style.opacity !== '0'
  })
  if (hasActiveElementPlusOverlay) return

  if (isMobile.value && sidebarVisible.value) {
    event.preventDefault()
    event.stopPropagation()
    sidebarVisible.value = false
    return
  }

  if (!aiPetStore.isPanelOpen && !notesVisible.value) return
  event.preventDefault()
  event.stopPropagation()
  aiPetStore.setPanelOpen(false)
  notesVisible.value = false
  stopPetWandering()
}

const closeUtilitiesForIntegrationDetail = () => {
  aiPetStore.setPanelOpen(false)
  notesVisible.value = false
  stopPetWandering()
}

onMounted(() => {
  window.addEventListener('resize', updateSize)
  window.addEventListener('resize', restoreStickyLauncherPosition)
  window.addEventListener(AUTH_SESSION_CHANGED, restoreStickyLauncherPosition)
  window.addEventListener('online', updateOnlineStatus)
  window.addEventListener('offline', updateOnlineStatus)
  document.addEventListener('mouseup', captureSelectedText)
  document.addEventListener('keyup', captureSelectedText)
  window.addEventListener('keydown', handleGlobalKeydown)
  window.addEventListener('integration-detail-opened', closeUtilitiesForIntegrationDetail)
  window.addEventListener('pointermove', movePet)
  window.addEventListener('pointerup', endPetDrag)
  nextTick(() => {
    restoreStickyLauncherPosition()
    window.setTimeout(normalizePetPosition, 120)
  })
  startPetWandering()
})

onUnmounted(() => {
  window.removeEventListener('resize', updateSize)
  window.removeEventListener('resize', restoreStickyLauncherPosition)
  window.removeEventListener(AUTH_SESSION_CHANGED, restoreStickyLauncherPosition)
  window.removeEventListener('online', updateOnlineStatus)
  window.removeEventListener('offline', updateOnlineStatus)
  document.removeEventListener('mouseup', captureSelectedText)
  document.removeEventListener('keyup', captureSelectedText)
  window.removeEventListener('keydown', handleGlobalKeydown)
  window.removeEventListener('integration-detail-opened', closeUtilitiesForIntegrationDetail)
  window.removeEventListener('pointermove', movePet)
  window.removeEventListener('pointerup', endPetDrag)
  window.removeEventListener('pointermove', moveAiPanelResize)
  window.removeEventListener('pointerup', endAiPanelResize)
  window.removeEventListener('pointercancel', endAiPanelResize)
  aiPanelResizeState = null
  aiPanelResizing.value = false
  clearStickyLauncherDrag()
  stopPetWandering()
  cancelVoiceInput()
  clearPendingAttachments()
  releaseMessageAttachmentUrls()
})

watch(() => route.fullPath, () => {
  if (isMobile.value) sidebarVisible.value = false
  nextTick(() => window.setTimeout(normalizePetPosition, 160))
})

const toggleSidebar = () => {
  sidebarVisible.value = !sidebarVisible.value
}

const scrollAiToBottom = async () => {
  await nextTick()
  if (aiContentRef.value) {
    aiContentRef.value.scrollTop = aiContentRef.value.scrollHeight
  }
}

const toggleAI = async () => {
  const willOpen = !aiVisible.value
  notesVisible.value = false
  aiVisible.value = willOpen
  if (willOpen) window.dispatchEvent(new CustomEvent('global-utility-drawer-opened'))
  if (aiVisible.value) stopPetWandering()
  else startPetWandering()
  if (aiVisible.value) {
    await scrollAiToBottom()
  }
}

const toggleCreate = () => {
  createVisible.value = !createVisible.value
}

const useQuickPrompt = (prompt) => {
  aiInput.value = prompt
}

const actionPayload = (action) => action?.payload || {}
const payloadValue = (action, ...keys) => {
  const payload = actionPayload(action)
  const key = keys.find(item => payload[item] !== undefined && payload[item] !== null && `${payload[item]}`.trim() !== '')
  return key ? payload[key] : ''
}

const cancelAiAction = async (action) => {
  if (action.loading || action.uiStatus === 'success') return
  if (action.serverActionId) {
    await axiosClient.post(`/ai/actions/${action.serverActionId}/cancel`)
  }
  action.uiStatus = 'cancelled'
  action.error = ''
  await persistConversation()
}

const retryAiAction = async (action) => {
  if (!action || action.loading || action.uiStatus !== 'cancelled') return
  action.uiStatus = 'pending'
  action.error = ''
  action.result = null
  await persistConversation()
}

const refreshAfterAiAction = async (action, result) => {
  const entityId = result?.entityId || result?.EntityId
  const entityType = String(result?.entityType || result?.EntityType || '').toLowerCase()
  const projectId = currentProjectId.value || payloadValue(action, 'projectId')
  await Promise.all([
    projectStore.fetchAllProjects(true).catch(() => []),
    projectId ? workTaskStore.fetchTasks(projectId, { reset: false }).catch(() => []) : Promise.resolve(),
    entityType === 'goal' ? goalStore.fetchGoals().catch(() => {}) : Promise.resolve(),
    ['cycle', 'sprint'].includes(entityType) && projectId ? sprintStore.fetchSprints(projectId, { force: true }).catch(() => {}) : Promise.resolve()
  ])
  return { entityId, entityType, projectId }
}

const navigateToAiEntity = async ({ entityId, entityType, projectId }) => {
  if (!entityId) return
  const project = projectStore.allProjects.find(item => `${item.id}` === `${projectId || entityId || currentProjectId.value}`)
  const projectTarget = project || projectId || entityId || currentProjectId.value
  if (entityType === 'project') return router.push(buildSpacePath(projectTarget, 'work-items'))
  if (entityType === 'worktask' || entityType === 'task') {
    return router.push({ path: buildSpacePath(projectTarget, 'work-items'), query: { task: entityId } })
  }
  if (entityType === 'goal') return router.push(`/home/goals/${entityId}`)
  if (['cycle', 'sprint'].includes(entityType)) return router.push(buildSpacePath(projectTarget, 'cycles'))
  if (entityType === 'module') return router.push(buildSpacePath(projectTarget, 'modules'))
  if (entityType === 'page') return router.push(buildSpacePath(projectTarget, 'pages'))
  if (entityType === 'view') return router.push(buildSpacePath(projectTarget, 'views'))
  if (entityType === 'intake' || entityType === 'intake_request') return router.push(buildSpacePath(projectTarget, 'intakes'))
  if (entityType === 'report') return router.push(buildSpacePath(projectTarget, 'reports'))
}

const normalizeTaskTitle = (title = '') => `${title}`.trim().replace(/\s+/g, ' ').toLocaleUpperCase('vi-VN')

const taskTitlesAreSimilar = (existingTitle, requestedTitle) => {
  const existingTokens = normalizeTaskTitle(existingTitle).split(' ').filter(Boolean)
  const requestedTokens = normalizeTaskTitle(requestedTitle).split(' ').filter(Boolean)
  if (existingTokens.join(' ') === requestedTokens.join(' ')) return true
  if (existingTokens.length < 3 || requestedTokens.length < 3) return false
  const existingSet = new Set(existingTokens)
  const requestedSet = new Set(requestedTokens)
  const intersection = [...existingSet].filter(token => requestedSet.has(token)).length
  const union = new Set([...existingSet, ...requestedSet]).size
  return union > 0 && intersection / union >= 0.8
}

const findDuplicateTask = async (action) => {
  if (action.type !== 'create_task' || actionPayload(action).allowDuplicate) return null
  const title = actionPayload(action).title || actionPayload(action).name
  if (!title || !currentProjectId.value) return null
  const tasks = await ensureProjectTasks()
  const match = tasks.find(task => taskTitlesAreSimilar(task.title || task.Title, title))
  if (!match) return null
  return {
    id: match.id || match.Id,
    sequenceId: match.sequenceId || match.SequenceId,
    title: match.title || match.Title,
    statusName: match.statusName || match.StatusName || match.taskStatus?.name || match.TaskStatus?.Name || 'Không rõ trạng thái'
  }
}

const toggleNotes = () => {
  const willOpen = !notesVisible.value
  notesVisible.value = willOpen
  if (willOpen) {
    aiVisible.value = false
    window.dispatchEvent(new CustomEvent('global-utility-drawer-opened'))
    stopPetWandering()
  } else if (!aiVisible.value) {
    startPetWandering()
  }
}

const openNotesFromLauncher = event => {
  if (stickyLauncherDragState?.moved) {
    event?.preventDefault?.()
    return
  }
  toggleNotes()
}

const closeNotes = () => {
  notesVisible.value = false
  if (!aiVisible.value) startPetWandering()
}

const openDuplicateTask = (action, edit) => {
  const task = action.duplicateCandidate
  if (!task?.id) return
  const project = projectStore.allProjects.find(item => `${item.id}` === `${currentProjectId.value}`) || currentProjectId.value
  return router.push({
    path: buildSpacePath(project, 'work-items'),
    query: { task: task.id, ...(edit ? { edit: '1' } : {}) }
  })
}

const confirmDuplicateCreation = async (action) => {
  try {
    await ElMessageBox.confirm(
      'Công việc mới sẽ được tạo dù có tiêu đề trùng hoặc rất gần với công việc hiện có.',
      'Xác nhận tạo trùng',
      { confirmButtonText: 'Vẫn tạo', cancelButtonText: 'Quay lại', type: 'warning' }
    )
    action.payload = { ...actionPayload(action), allowDuplicate: true }
    action.duplicateCandidate = null
    action.uiStatus = 'pending'
    await executeAiAction(action)
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') ElMessage.error('Không thể xác nhận thao tác.')
  }
}
const executeAiAction = async (action) => {
  if (!action || action.loading || action.uiStatus === 'success' || action.uiStatus === 'cancelled') return
  const duplicate = await findDuplicateTask(action)
  if (duplicate) {
    action.duplicateCandidate = duplicate
    action.uiStatus = 'pending'
    await persistConversation()
    return
  }
  action.loading = true
  action.uiStatus = 'loading'
  action.error = ''
  try {
    if (action.directExecution === true) {
      const response = await axiosClient.post('/ai/actions/execute', {
        type: action.type,
        workspaceId: currentWorkspaceId.value || null,
        projectId: currentProjectId.value || actionPayload(action).projectId || null,
        payload: actionPayload(action)
      })
      const root = response.data || {}
      const result = root?.data ?? root
      if (root?.success === false || !result || typeof result !== 'object') throw new Error('Backend không trả về kết quả đọc dữ liệu.')
      action.result = result
      action.uiStatus = 'success'
      ElMessage.success(result?.message || 'Đã tải dữ liệu thành công.')
      return
    }
    action.idempotencyKey ||= `${action.type}-${crypto.randomUUID()}`
    if (!action.serverActionId) {
      const previewResponse = await axiosClient.post('/ai/actions/preview', {
        type: action.type,
        idempotencyKey: action.idempotencyKey,
        workspaceId: currentWorkspaceId.value || null,
        projectId: currentProjectId.value || actionPayload(action).projectId || null,
        payload: actionPayload(action)
      })
      action.serverActionId = previewResponse.data?.data?.actionId
      if (!action.serverActionId) throw new Error('Backend khÃ´ng táº¡o Ä‘Æ°á»£c action preview.')
      await persistConversation()
    }
    const response = await axiosClient.post(`/ai/actions/${action.serverActionId}/confirm`)
    const root = response.data || {}
    const payload = root?.data ?? root
    const actionResult = payload?.result ?? payload
    const result = actionResult?.data ?? actionResult?.result ?? actionResult
    const failed = root?.success === false || root?.succeeded === false || payload?.success === false || payload?.succeeded === false || Boolean(root?.error || payload?.error)
    const hasResult = result && typeof result === 'object' && Object.keys(result).length > 0 && !result.error
    const confirmed = root?.success === true || root?.succeeded === true || payload?.success === true || payload?.succeeded === true
    if (failed || !hasResult || (!confirmed && !result?.entityId && !result?.id && !result?.taskId && !result?.message)) throw new Error('Backend không xác nhận action thành công.')
    action.result = result
    action.uiStatus = 'success'
    const navigation = await refreshAfterAiAction(action, result)
    ElMessage.success(result?.message || 'AI đã thực hiện thay đổi thành công.')
    await navigateToAiEntity(navigation)
  } catch (error) {
    const duplicateCandidate = error.response?.data?.data?.existingTask
    if (error.response?.status === 409 && duplicateCandidate) {
      action.duplicateCandidate = duplicateCandidate
      action.uiStatus = 'pending'
      await persistConversation()
      return
    }
    action.uiStatus = 'error'
        const status = error.response?.status
    const mapped = { 400: 'Dữ liệu action không hợp lệ.', 401: 'Phiên đăng nhập đã hết hạn.', 403: 'Bạn không có quyền thực hiện action này.', 404: 'Không tìm thấy entity cần thao tác.', 409: 'Action bị trùng hoặc xung đột dữ liệu.', 422: 'Dữ liệu không vượt qua kiểm tra nghiệp vụ.', 429: 'AI đang quá tải. Hãy thử lại sau.', 503: 'Dịch vụ AI tạm thời không khả dụng.' }
    action.error = mapped[status] || error.response?.data?.message || error.message || 'Không thể thực hiện action.'
    ElMessage.error(action.error)
  } finally {
    action.loading = false
    await persistConversation()
  }
}

const normalizeAiText = (value = '') =>
  `${value}`
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()

const currentProjectId = computed(() => {
  const routeId = route.params?.id
  if (typeof routeId === 'string' && routeId.length >= 30) return routeId
  return projectStore.currentProject?.id || projectStore.currentProject?.Id || workTaskStore.currentProjectId || null
})

const currentWorkspaceId = computed(() => {
  const routeWorkspaceId = route.params?.workspaceId || route.params?.spaceId
  if (typeof routeWorkspaceId === 'string' && routeWorkspaceId.length >= 30) return routeWorkspaceId
  const project = projectStore.currentProject
  return project?.workspaceId || project?.WorkspaceId || workTaskStore.resolveWorkspaceId(currentProjectId.value) || null
})

const stickyContext = computed(() => ({
  workspaceId: currentWorkspaceId.value || null,
  projectId: route.path.startsWith('/space/') ? currentProjectId.value : null,
  workTaskId: route.params?.taskId || route.query?.taskId || null,
  goalId: route.path.startsWith('/goals/') ? route.params?.id || null : route.params?.goalId || null,
  sourceRoute: route.fullPath.slice(0, 500)
}))

const clearSelectedText = () => {
  selectedText.value = ''
  selectionPopover.value.visible = false
}

const copyAiMessage = async (content) => {
  if (!content) return
  try {
    await navigator.clipboard.writeText(content)
    ElMessage.success('Đã sao chép câu trả lời.')
  } catch {
    ElMessage.info('Không thể sao chép tự động trên trình duyệt này.')
  }
}

const continueFromAiMessage = (content) => {
  aiInput.value = `Hãy giải thích thêm và đưa ra bước tiếp theo từ câu trả lời này:\n${content.slice(0, 600)}`
  nextTick(() => document.querySelector('.ai-input-wrapper textarea')?.focus())
}

const captureSelectedText = () => {
  const selection = window.getSelection?.()
  if (!selection || selection.isCollapsed) {
    selectionPopover.value.visible = false
    return
  }
  const anchor = selection.anchorNode?.parentElement
  if (anchor?.closest('input, textarea, select, [contenteditable="true"]')) {
    selectionPopover.value.visible = false
    return
  }
  const text = selection.toString().trim()
  if (text) {
    const rect = selection.getRangeAt(0).getBoundingClientRect()
    selectedText.value = text.slice(0, 4000)
    selectionPopover.value = {
      visible: true,
      left: Math.min(Math.max(12, rect.left), window.innerWidth - 300),
      top: Math.min(rect.bottom + 8, window.innerHeight - 54)
    }
  }
}

const askAboutSelection = (action) => {
  aiInput.value = `${action} đoạn văn bản sau:\n\n${selectedText.value}`
  selectionPopover.value.visible = false
  aiVisible.value = true
  notesVisible.value = false
  window.dispatchEvent(new CustomEvent('global-utility-drawer-opened'))
}

// ────────────────────────────────────────────
// SME Permission Matrix for AI Sidebar
// ────────────────────────────────────────────
const permissionMatrix = ref(getDefaultPermissionMatrix())

const loadPermissionMatrix = async () => {
  const pId = currentProjectId.value
  if (!pId) return
  try {
    const res = await axiosClient.get(`/settings/ProjectPermissions:${pId}`)
    if (res.data?.data?.rolePermissions) {
      permissionMatrix.value = JSON.parse(res.data.data.rolePermissions)
    } else {
      permissionMatrix.value = getDefaultPermissionMatrix()
    }
  } catch {
    permissionMatrix.value = getDefaultPermissionMatrix()
  }
}

const canCreateTaskInProject = computed(() => {
  const user = getStoredUserSession()
  if (!user) return false
  
  const wsRole = user.workspaceRole?.toUpperCase()
  if (wsRole === 'OWNER' || wsRole === 'ADMIN') return true

  const me = projectStore.currentProject?.myRole || projectStore.currentProject?.MyRole || 'Member'
  return hasPermission(permissionMatrix.value, me, 'task.create')
})

const canUpdateTaskInProject = computed(() => {
  const user = getStoredUserSession()
  if (!user) return false
  
  const wsRole = user.workspaceRole?.toUpperCase()
  if (wsRole === 'OWNER' || wsRole === 'ADMIN') return true

  const me = projectStore.currentProject?.myRole || projectStore.currentProject?.MyRole || 'Member'
  return hasPermission(permissionMatrix.value, me, 'task.update')
})

watch(currentProjectId, async (newVal) => {
  if (newVal) {
    await loadPermissionMatrix()
  }
}, { immediate: true })

const currentTasks = computed(() => Array.isArray(workTaskStore.tasks) ? workTaskStore.tasks : [])

const ensureProjectTasks = async () => {
  const projectId = currentProjectId.value
  if (!projectId) return []
  if (workTaskStore.currentProjectId !== projectId || !currentTasks.value.length) {
    await workTaskStore.fetchTasks(projectId)
  }
  return currentTasks.value
}

const todayDateOnly = () => new Date().toISOString().slice(0, 10)

const offsetDateOnly = (days) => {
  const date = new Date()
  date.setDate(date.getDate() + days)
  return date.toISOString().slice(0, 10)
}

const inferDueDate = (normalized) => {
  if (normalized.includes('hom nay') || normalized.includes('today')) return todayDateOnly()
  if (normalized.includes('ngay mai') || normalized.includes('tomorrow')) return offsetDateOnly(1)
  if (normalized.includes('tuan sau') || normalized.includes('next week')) return offsetDateOnly(7)
  const match = normalized.match(/(\d{1,2})[/-](\d{1,2})(?:[/-](\d{2,4}))?/)
  if (!match) return null
  const currentYear = new Date().getFullYear()
  const day = Number(match[1])
  const month = Number(match[2])
  const year = match[3] ? Number(match[3].length === 2 ? `20${match[3]}` : match[3]) : currentYear
  if (!day || !month) return null
  return `${year}-${`${month}`.padStart(2, '0')}-${`${day}`.padStart(2, '0')}`
}

const inferPriority = (normalized) => {
  if (/(khan|urgent|rat cao|critical|nghiem trong|blocker)/.test(normalized)) return 1
  if (/(cao|high|important)/.test(normalized)) return 2
  if (/(thap|low)/.test(normalized)) return 4
  return 3
}

const inferStatusName = (normalized) => {
  if (/(done|hoan thanh|da xong|xong)/.test(normalized)) return 'DONE'
  if (/(review|kiem tra|danh gia)/.test(normalized)) return 'IN REVIEW'
  if (/(progress|dang lam|dang thuc hien|in progress)/.test(normalized)) return 'IN PROGRESS'
  if (/(todo|to do|can lam)/.test(normalized)) return 'TO DO'
  if (/(backlog|cho xu ly)/.test(normalized)) return 'BACKLOG'
  return 'TO DO'
}

const cleanTaskTitle = (message, normalized) => {
  const raw = `${message}`.trim()
  const quoted = raw.match(/["“”']([^"“”']{2,})["“”']/)
  if (quoted?.[1]) return quoted[1].trim()

  const lastBot = [...chatHistory.value].reverse().find(item => item.role === 'bot' && !item.loading)
  const suggested = lastBot?.content?.match(/(?:Tên Task|Task|title)[:：\s*"']+([^*\n"]{2,80})/i)
  if (/(ok tao|tao di|create it|add it|lam di)/.test(normalized) && suggested?.[1]) {
    return suggested[1].replace(/\*\*/g, '').trim()
  }

  const markers = ['tạo task', 'tao task', 'tạo công việc', 'tao cong viec', 'create task', 'add task', 'task mới', 'task moi']
  const lower = raw.toLowerCase()
  let title = raw
  for (const marker of markers) {
    const index = lower.indexOf(marker)
    if (index >= 0) {
      title = raw.slice(index + marker.length)
      break
    }
  }

  title = title
    .replace(/^\s*[:\-–]\s*/, '')
    .replace(/^(mới|moi|new)\s*[:\-–]\s*/i, '')
    .replace(/\b(deadline|due|hạn|han|ưu tiên|uu tien|priority)\b.*$/i, '')
    .replace(/\s+/g, ' ')
    .trim()

  if (!title || /^(moi|mới|new)$/i.test(title)) {
    if (suggested?.[1]) return suggested[1].replace(/\*\*/g, '').trim()
  }

  if (!title && normalized.includes('ok tao')) return 'Task mới từ SprintA AI'
  return title || 'Task mới từ SprintA AI'
}

const splitTaskTitles = (message) => {
  const lines = `${message}`
    .split(/\n|;|\d+\.\s+/)
    .map(item => item.replace(/^[-*]\s*/, '').trim())
    .filter(Boolean)
  const taskLines = lines.filter(item => /^(tao|tạo|create|add|task)/i.test(item) || lines.length > 1)
  return taskLines.length > 1 ? taskLines.map(item => cleanTaskTitle(item, normalizeAiText(item))).filter(Boolean) : []
}

const formatTaskLine = (task) => {
  const status = task.statusName || 'BACKLOG'
  const due = task.dueDate || task.plannedEndDate
  return `- ${task.sequenceId || task.id?.slice?.(0, 8) || 'Task'}: ${task.title} (${status}${due ? `, hạn ${due}` : ''})`
}

const buildProjectStats = async () => {
  const tasks = await ensureProjectTasks()
  const isDone = (task) => normalizeAiText(task.statusName).includes('done') || normalizeAiText(task.statusName).includes('hoan thanh')
  const isProgress = (task) => normalizeAiText(task.statusName).includes('progress') || normalizeAiText(task.statusName).includes('dang')
  const isTodo = (task) => normalizeAiText(task.statusName).includes('todo') || normalizeAiText(task.statusName).includes('to do') || normalizeAiText(task.statusName).includes('can lam')
  const today = todayDateOnly()
  const overdue = tasks.filter(task => !isDone(task) && (task.dueDate || task.plannedEndDate) && (task.dueDate || task.plannedEndDate) < today)
  return {
    total: tasks.length,
    done: tasks.filter(isDone).length,
    inProgress: tasks.filter(isProgress).length,
    todo: tasks.filter(isTodo).length,
    backlog: tasks.filter(task => normalizeAiText(task.statusName).includes('backlog') || !task.statusName).length,
    overdue: overdue.length,
    highPriority: tasks.filter(task => Number(task.priority) > 0 && Number(task.priority) <= 2).length
  }
}

const summarizeCurrentProject = async () => {
  const tasks = await ensureProjectTasks()
  const stats = await buildProjectStats()
  const topTasks = tasks
    .filter(task => !/(done|hoan thanh)/.test(normalizeAiText(task.statusName)))
    .sort((a, b) => Number(a.priority || 9) - Number(b.priority || 9))
    .slice(0, 5)

  return [
    `Tóm tắt project hiện tại: có ${stats.total} task, ${stats.done} đã xong, ${stats.inProgress} đang làm, ${stats.todo} cần làm, ${stats.overdue} quá hạn.`,
    stats.highPriority ? `Có ${stats.highPriority} task ưu tiên cao cần theo dõi.` : 'Hiện chưa có task ưu tiên cao.',
    topTasks.length ? `Việc nên chú ý:\n${topTasks.map(formatTaskLine).join('\n')}` : 'Chưa có task mở nào cần xử lý.'
  ].join('\n\n')
}

const suggestNextActions = async () => {
  const tasks = await ensureProjectTasks()
  const openTasks = tasks
    .filter(task => !/(done|hoan thanh)/.test(normalizeAiText(task.statusName)))
    .sort((a, b) => {
      const priorityDiff = Number(a.priority || 9) - Number(b.priority || 9)
      if (priorityDiff !== 0) return priorityDiff
      return `${a.dueDate || a.plannedEndDate || '9999-12-31'}`.localeCompare(`${b.dueDate || b.plannedEndDate || '9999-12-31'}`)
    })
    .slice(0, 5)

  if (!openTasks.length) return 'Project hiện tại chưa có task mở. Bạn có thể yêu cầu: "tạo task chuẩn bị demo ngày mai".'
  return `Gợi ý ưu tiên tiếp theo:\n${openTasks.map((task, index) => `${index + 1}. ${formatTaskLine(task).slice(2)}`).join('\n')}`
}

const createRealTasks = async (message) => {
  const projectId = currentProjectId.value
  if (!projectId) throw new Error(aiCopy.value.needProject)
  const normalized = normalizeAiText(message)
  const titles = splitTaskTitles(message)
  const finalTitles = titles.length ? titles : [cleanTaskTitle(message, normalized)]
  const dueDate = inferDueDate(normalized)
  const statusName = inferStatusName(normalized)
  const priority = inferPriority(normalized)
  const created = []

  for (const title of finalTitles.slice(0, 8)) {
    const payload = {
      title,
      description: `Được tạo bởi SprintA AI từ yêu cầu:\n${message}`,
      statusName,
      typeName: 'Task',
      priority,
      storyPoints: 0
    }
    if (dueDate) payload.dueDate = dueDate
    created.push(await workTaskStore.createTask(projectId, payload))
  }

  window.dispatchEvent(new CustomEvent('sprinta-ai-task-created', { detail: { projectId, tasks: created } }))
  return created.length === 1
    ? `Đã tạo task thật: "${created[0]?.title || finalTitles[0]}" (${statusName}${dueDate ? `, hạn ${dueDate}` : ''}).`
    : `Đã tạo ${created.length} task thật:\n${created.map(task => `- ${task?.title}`).join('\n')}`
}

const moveTaskByPrompt = async (message) => {
  const projectId = currentProjectId.value
  if (!projectId) throw new Error(aiCopy.value.needProject)
  const tasks = await ensureProjectTasks()
  const normalized = normalizeAiText(message)
  const statusName = inferStatusName(normalized)
  const sequenceMatch = message.match(/\b[A-Z0-9]+-\d+\b/i)
  const quoted = message.match(/["“”']([^"“”']{2,})["“”']/)
  const keyword = normalizeAiText(quoted?.[1] || sequenceMatch?.[0] || message.replace(/(chuyen|chuyển|move|dua|đưa|sang|vao|vào|to do|todo|done|in progress|dang lam|hoan thanh|xong)/gi, ''))
  const task = tasks.find(item =>
    (sequenceMatch && normalizeAiText(item.sequenceId) === normalizeAiText(sequenceMatch[0])) ||
    (keyword && normalizeAiText(item.title).includes(keyword.trim()))
  )

  if (!task) {
    return 'Mình chưa tìm thấy task cần chuyển. Hãy ghi rõ mã task hoặc đặt tên task trong dấu ngoặc kép, ví dụ: chuyển "Bug Bash" sang Done.'
  }

  await workTaskStore.updateTaskStatus(projectId, task.id, statusName)
  return `Đã chuyển task "${task.title}" sang trạng thái ${statusName}.`
}

const tryHandleLocalAiCommand = async (message) => {
  const normalized = normalizeAiText(message)
  const wantsCreate = /(tao|create|add).*(task|cong viec)|ok tao|tao di|create it/.test(normalized)
  const wantsMove = /(chuyen|move|dua).*(task|cong viec|sang|vao|done|todo|progress|review)|sang (to do|todo|done|in progress)/.test(normalized)
  const wantsStats = /(thong ke|bao cao|report|stats|dashboard|tong quan)/.test(normalized)
  const wantsSummary = /(tom tat|summary|summarize|tong ket)/.test(normalized)
  const wantsPriority = /(uu tien|priority|nen lam|next action|goi y)/.test(normalized)
  const wantsChecklist = /(checklist|danh sach viec|cac buoc)/.test(normalized)

  if (wantsCreate) {
    const finalTitles = splitTaskTitles(message).length ? splitTaskTitles(message) : [cleanTaskTitle(message, normalized)]
    const dueDate = inferDueDate(normalized)
    const priority = inferPriority(normalized)
    const suggested = finalTitles.map(t => ({
      title: t,
      description: `Đề xuất tạo từ yêu cầu: "${message}"`,
      priority,
      dueDate
    }))
    return {
      answer: "SprintA AI đã đề xuất tạo các công việc sau đây. Vui lòng kiểm tra và xác nhận:",
      suggestedTasks: suggested
    }
  }

  if (wantsMove) {
    const tasks = await ensureProjectTasks()
    const statusName = inferStatusName(normalized)
    const sequenceMatch = message.match(/\b[A-Z0-9]+-\d+\b/i)
    const quoted = message.match(/["“”']([^"“”']{2,})["“”']/)
    const keyword = normalizeAiText(quoted?.[1] || sequenceMatch?.[0] || message.replace(/(chuyen|chuyển|move|dua|đưa|sang|vao|vào|to do|todo|done|in progress|dang lam|hoan thanh|xong)/gi, ''))
    const task = tasks.find(item =>
      (sequenceMatch && normalizeAiText(item.sequenceId) === normalizeAiText(sequenceMatch[0])) ||
      (keyword && normalizeAiText(item.title).includes(keyword.trim()))
    )

    if (!task) {
      return {
        answer: "Mình chưa tìm thấy công việc cần chuyển. Hãy ghi rõ mã task hoặc đặt tên task trong dấu ngoặc kép."
      }
    }

    return {
      answer: `Bạn có muốn chuyển trạng thái công việc **${task.title}** sang **${statusName}** không?`,
      suggestedActions: [
        {
          type: 'move-task',
          taskId: task.id,
          taskTitle: task.title,
          statusName: statusName
        }
      ]
    }
  }

  if (wantsStats) {
    const stats = await buildProjectStats()
    return `Thống kê project:\n- Tổng task: ${stats.total}\n- Đã xong: ${stats.done}\n- Đang làm: ${stats.inProgress}\n- Cần làm: ${stats.todo}\n- Backlog: ${stats.backlog}\n- Quá hạn: ${stats.overdue}\n- Ưu tiên cao: ${stats.highPriority}`
  }
  if (wantsSummary) return await summarizeCurrentProject()
  if (wantsPriority) return await suggestNextActions()
  if (wantsChecklist) {
    const suggestion = await suggestNextActions()
    return `Checklist đề xuất:\n1. Kiểm tra các task đang quá hạn hoặc ưu tiên cao.\n2. Chốt task cần làm tiếp theo trong cột To Do.\n3. Chuyển task đang xử lý sang In Progress.\n4. Cập nhật deadline/mô tả nếu còn thiếu.\n5. Báo cáo tiến độ ngắn cho team.\n\n${suggestion}`
  }

  return null
}

const createSuggestedTask = async (task, messageItem) => {
  if (!canCreateTaskInProject.value) {
    ElMessage.error("Bạn không có quyền tạo công việc trong dự án này.")
    return
  }

  task.loading = true
  try {
    const created = await workTaskStore.createTask(currentProjectId.value, {
      title: task.title,
      description: task.description || "Được tạo từ gợi ý của SprintA AI",
      priority: task.priority || 3,
      dueDate: task.dueDate || null,
      typeName: "Task",
      storyPoints: 0
    })
    task.created = true
    task.createdTask = created
    ElMessage.success(`Đã tạo thành công task: "${created.title || created.Title}"`)
    // Refresh lists
    window.dispatchEvent(new CustomEvent('sprinta-ai-task-created', {
      detail: { projectId: currentProjectId.value, task: created }
    }))
  } catch (e) {
    ElMessage.error(e.response?.data?.message || "Không thể tạo task gợi ý.")
  } finally {
    task.loading = false
  }
}

const createAllSuggestedTasks = async (messageItem) => {
  if (!canCreateTaskInProject.value) {
    ElMessage.error("Bạn không có quyền tạo công việc trong dự án này.")
    return
  }

  const uncreated = messageItem.suggestedTasks.filter(t => !t.created)
  if (!uncreated.length) return

  ElMessage.info(`Đang tạo ${uncreated.length} task gợi ý...`)
  for (const task of uncreated) {
    await createSuggestedTask(task, messageItem)
  }
}

const confirmSuggestedAction = async (action) => {
  if (action.type === 'move-task') {
    if (!canUpdateTaskInProject.value) {
      ElMessage.error("Bạn không có quyền cập nhật công việc trong dự án này.")
      return
    }

    action.loading = true
    try {
      await workTaskStore.updateTaskStatus(currentProjectId.value, action.taskId, action.statusName)
      action.completed = true
      ElMessage.success(`Đã chuyển task "${action.taskTitle}" sang trạng thái ${action.statusName}.`)
      // Refresh list
      await workTaskStore.fetchTasks(currentProjectId.value)
    } catch (e) {
      ElMessage.error(e.response?.data?.message || "Không thể chuyển trạng thái task.")
    } finally {
      action.loading = false
    }
  }
}

const normalizeUploadedAttachment = (payload, localAttachment) => ({
  id: payload.id,
  name: payload.fileName,
  displayName: localAttachment.displayName || payload.fileName,
  size: payload.fileSize,
  kind: payload.kind,
  typeLabel: localAttachment.typeLabel || attachmentExtension(payload.fileName).slice(1).toUpperCase(),
  icon: localAttachment.icon || attachmentIcon(attachmentExtension(payload.fileName)),
  previewUrl: localAttachment.previewUrl || '',
  contentUrl: payload.contentUrl,
  mimeType: payload.mimeType,
  status: String(payload.status || 'ready').toLowerCase(),
  width: payload.width,
  height: payload.height,
  chunkCount: payload.chunkCount
})

const loadAiUsage = async () => {
  try {
    const response = await axiosClient.get('/ai/usage-summary')
    aiUsage.value = apiPayload(response) || null
  } catch {
    // Lỗi badge Credits không được làm hỏng AI Drawer.
    aiUsage.value = null
  }
}

onMounted(() => {
  loadAiUsage()
})

watch(aiVisible, (visible) => {
  if (visible) loadAiUsage()
})

const uploadPendingAttachments = async (conversationId) => {
  const uploaded = []
  for (const attachment of pendingAttachments.value) {
    if (attachment.status === 'ready' && attachment.id && attachment.contentUrl) {
      uploaded.push(attachment)
      continue
    }

    attachment.status = 'uploading'
    const form = new FormData()
    form.append('file', attachment.file, attachment.name)
    form.append('conversationId', conversationId)
    const workspaceId = currentConversationWorkspaceId.value || currentWorkspaceId.value
    if (workspaceId) form.append('workspaceId', workspaceId)

    try {
      const response = await axiosClient.post('/ai/attachments', form, {
        headers: { 'Content-Type': 'multipart/form-data' }
      })
      attachment.status = 'processing'
      Object.assign(attachment, normalizeUploadedAttachment(apiPayload(response), attachment))
      uploaded.push(attachment)
    } catch (error) {
      attachment.status = 'error'
      throw error
    }
  }
  return uploaded
}

const sendAiMessage = async () => {
  const outgoing = aiInput.value.trim()
  const hasAttachments = pendingAttachments.value.length > 0
  if (aiCreditsExhausted.value) {
    ElMessage.warning('Bạn đã sử dụng hết AI Credits trong tháng này.')
    return
  }
  if ((!outgoing && !hasAttachments) || aiSending.value) return

  aiSending.value = true
  let loadingAdded = false
  let userMessageAdded = false

  try {
    const titleSeed = outgoing || pendingAttachments.value.map(item => item.name).join(', ')
    const conversationId = await ensureConversation(titleSeed)
    const uploadedAttachments = hasAttachments ? await uploadPendingAttachments(conversationId) : []

    if (uploadedAttachments.length) pendingAttachments.value = []
    aiInput.value = ''
    chatHistory.value.push({
      role: 'user',
      content: outgoing || 'Hãy phân tích các attachment đã đính kèm.',
      attachments: uploadedAttachments
    })
    userMessageAdded = true
    chatHistory.value.push({ role: 'bot', content: aiCopy.value.thinking, loading: true })
    loadingAdded = true
    await scrollAiToBottom()

    if (uploadedAttachments.length) {
      const response = await axiosClient.post('/ai/attachment-chat', {
        workspaceId: currentConversationWorkspaceId.value || currentWorkspaceId.value || null,
        conversationId,
        attachmentIds: uploadedAttachments.map(item => item.id),
        message: outgoing
      })
      const responseData = apiPayload(response)
      chatHistory.value.pop()
      loadingAdded = false
      chatHistory.value.push({
        role: 'bot',
        content: responseData?.answer || aiCopy.value.emptyResponse,
        citations: responseData?.citations || []
      })
      await loadAiUsage()
      return
    }

    const visibleTasks = currentTasks.value.slice(0, 100)
    const response = await axiosClient.post('/ai/context-chat', {
      route: route.fullPath,
      projectId: currentProjectId.value || null,
      workspaceId: currentWorkspaceId.value || null,
      message: outgoing,
      selectedText: selectedText.value || null,
      pageContext: {
        pageType: pageType.value,
        currentView: route.query?.view || route.name || '',
        visibleTaskIds: visibleTasks.map(task => task.id || task.Id).filter(Boolean),
        visibleStatuses: [...new Set(visibleTasks.map(task => task.statusName || task.StatusName || task.status?.name || task.Status?.Name).filter(Boolean))],
        filters: {},
        extra: {}
      }
    })
    const responseData = apiPayload(response)

    chatHistory.value.pop()
    loadingAdded = false
    
    chatHistory.value.push({
      role: 'bot',
      content: responseData?.answer || aiCopy.value.emptyResponse,
      suggestedPrompts: responseData?.suggestions || [],
      warnings: responseData?.warnings || [],
      actions: (responseData?.actions || []).map(action => ({
        ...action,
        type: String(action.type || '').toLowerCase(),
        payload: action.payload || {},
        duplicateCandidate: null,
        uiStatus: 'pending',
        loading: false,
        error: '',
        result: null
      })),
      suggestedActions: responseData?.suggestedActions || []
    })
    await loadAiUsage()
  } catch (error) {
    if (loadingAdded && chatHistory.value.at(-1)?.loading) chatHistory.value.pop()
    const status = error.response?.status
    const errorData = error.response?.data?.data || {}
    const errorCode = errorData?.code
    const retryAfterSeconds = Number(errorData?.retryAfterSeconds || 0)

    let message

    if (errorCode === 'AI_CREDITS_EXHAUSTED') {
      message = 'Bạn đã sử dụng hết AI Credits trong tháng này.'
      await loadAiUsage()
    } else if (errorCode === 'AI_RATE_LIMITED') {
      message = retryAfterSeconds > 0
        ? `Bạn thao tác AI quá nhanh. Vui lòng thử lại sau ${retryAfterSeconds} giây.`
        : 'Bạn thao tác AI quá nhanh. Vui lòng thử lại sau.'
    } else if (errorCode === 'AI_PROVIDER_RATE_LIMITED') {
      message = 'Dịch vụ AI đang bận. Vui lòng thử lại sau.'
    } else if (errorCode === 'AI_PROVIDER_UNAVAILABLE') {
      message = 'Dịch vụ AI tạm thời không khả dụng. Vui lòng thử lại sau.'
    } else {
      const messages = {
        400: error.response?.data?.message || 'Attachment không hợp lệ hoặc không thể xử lý.',
        401: 'Vui lòng đăng nhập lại để sử dụng SprintA AI.',
        402: 'Bạn đã sử dụng hết AI Credits trong tháng này.',
        403: 'Bạn không có quyền truy cập attachment trong workspace này.',
        413: 'Attachment vượt quá giới hạn dung lượng.',
        429: 'Dịch vụ AI đang bận. Vui lòng thử lại sau.',
        503: 'SprintA AI chưa sẵn sàng. Vui lòng thử lại sau.'
      }

      message =
        messages[status]
        || error.response?.data?.message
        || 'Không thể kết nối SprintA AI. Vui lòng thử lại.'
    }
    if (userMessageAdded) chatHistory.value.push({ role: 'bot', content: message })
    ElMessage.error(message)
  } finally {
    aiSending.value = false
    await persistConversation()
    await scrollAiToBottom()
  }
}

const handleSpaceCreated = (newSpace) => {
  if (newSpace && newSpace.id) {
    window.location.href = buildSpacePath(newSpace, 'work-items')
  } else {
    window.location.reload()
  }
}

const handleProjectCreated = (newProject) => {
  console.log('Task created:', newProject)
}
</script>

<style scoped>
.dashboard-layout {
  height: 100dvh;
  min-height: 0;
  display: flex;
  flex-direction: column;
  background:
    radial-gradient(circle at top left, color-mix(in srgb, var(--sa-primary) 8%, transparent), transparent 34%),
    var(--sa-bg);
  color: var(--color-text-primary);
  overflow: hidden;
  font-family: 'Be Vietnam Pro', 'Inter', system-ui, sans-serif;
}

.main-body {
  display: flex;
  flex: 1;
  overflow: hidden;
  position: relative;
  min-height: 0;
  background: var(--sa-bg);
}

.sidebar-overlay {
  position: fixed;
  top: var(--sa-topbar-height, 52px);
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  z-index: 998;
  backdrop-filter: blur(2px);
}

.content-area {
  flex: 1;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--sa-bg) 82%, var(--sa-surface) 18%), var(--sa-bg));
  padding: 0;
  overflow-y: auto;
  transition: all 0.3s;
  display: flex;
  flex-direction: column;
  min-height: 0;
  border-left: 1px solid color-mix(in srgb, var(--sa-border) 62%, transparent);
}

.dark .content-area {
  box-shadow: -4px 0 24px rgba(0, 0, 0, 0.2);
}

.content-area.is-project-context {
  overflow: hidden;
}

.content-wrapper {
  --app-shell-page-x: 18px;
  --app-shell-header-top: 18px;
  --app-shell-header-bottom: 18px;
  width: 100%;
  height: 100%;
  min-height: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  background: transparent;
}

.content-wrapper :deep(.app-shell-page-header) {
  display: flex !important;
  align-items: flex-start !important;
  justify-content: space-between !important;
  gap: 20px !important;
  width: 100% !important;
  margin: 0 !important;
  padding: var(--app-shell-header-top) var(--app-shell-page-x) var(--app-shell-header-bottom) !important;
  background: transparent !important;
  border-top: 0 !important;
  border-right: 0 !important;
  border-bottom: 0 !important;
  border-left: 0 !important;
  box-sizing: border-box !important;
}

.content-wrapper :deep(.app-shell-page-header > div:first-child) {
  min-width: 0;
}

.content-wrapper :deep(.app-shell-page-header .eyebrow) {
  display: block;
}

.content-wrapper :deep(.app-shell-page-header h1) {
  margin: 0 !important;
  font-size: 26px !important;
  line-height: 1.15 !important;
  font-weight: 900 !important;
  letter-spacing: 0 !important;
}

.content-wrapper :deep(.app-shell-page-header p) {
  margin: 0 !important;
  font-size: 12px !important;
}

.content-wrapper :deep(.app-shell-page-header + .page-content) {
  width: 100% !important;
  max-width: none !important;
  margin: 0 !important;
  padding: 18px !important;
  box-sizing: border-box !important;
}

@media (max-width: 1024px) {
  .content-area {
    padding: 0;
    width: 100% !important;
    min-width: 0 !important;
    overflow-x: clip !important;
  }

  .sidebar-overlay {
    z-index: 1000 !important;
  }

  :deep(.plane-sidebar) {
    position: fixed !important;
    left: 0 !important;
    top: var(--sa-topbar-height, 52px) !important;
    bottom: 0 !important;
    height: calc(100vh - var(--sa-topbar-height, 52px)) !important;
    height: calc(100dvh - var(--sa-topbar-height, 52px)) !important;
    z-index: 1001 !important;
    transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1), width 0.3s ease !important;
    transform: translateX(0);
    width: 250px !important;
  }

  :deep(.plane-sidebar.collapsed) {
    transform: translateX(-100%) !important;
    width: 250px !important;
    border-right: none !important;
  }
}

.ai-floating-btn {
  position: fixed;
  top: 0;
  left: 0;
  z-index: 1400;
  width: 68px;
  height: 68px;
  display: grid;
  align-items: center;
  justify-content: center;
  padding: 0;
  border: 0;
  border-radius: 50%;
  background: transparent;
  box-shadow: none;
  cursor: pointer;
  touch-action: none;
  user-select: none;
  will-change: transform;
  transition: filter 220ms ease;
}

.ai-floating-btn:hover {
  filter: brightness(1.04);
}

.global-utility-rail {
  position: fixed;
  z-index: 1510;
  right: 10px;
  display: flex;
  align-items: stretch;
  min-height: 42px;
  border: 1px solid var(--color-border);
  border-radius: 9px;
  background: var(--color-surface);
  box-shadow: var(--shadow-md);
  overflow: hidden;
  user-select: none;
  transition: border-color 160ms ease, box-shadow 160ms ease;
}

.global-utility-rail.is-dragging {
  border-color: var(--color-accent);
  box-shadow: var(--shadow-lg, var(--shadow-md));
}

.global-utility-rail button {
  min-height: 40px;
  border: 0;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
}

.sticky-launcher-handle {
  width: 24px;
  display: grid;
  place-items: center;
  border-right: 1px solid var(--color-border) !important;
  cursor: ns-resize !important;
  touch-action: none;
}

.sticky-launcher-handle i { font-size: 12px; }
.sticky-launcher-main {
  min-width: 78px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 7px;
  padding: 0 9px;
  font-size: 12px;
  font-weight: 700;
}

.sticky-launcher-add {
  width: 30px;
  display: grid;
  place-items: center;
  border-left: 1px solid var(--color-border) !important;
  font-size: 15px;
  font-weight: 700;
}

.sticky-launcher-main:hover,
.sticky-launcher-main.active,
.sticky-launcher-add:hover:not(:disabled),
.sticky-launcher-handle:hover,
.sticky-launcher-handle:focus-visible,
.sticky-launcher-add:focus-visible,
.sticky-launcher-main:focus-visible {
  background: var(--color-surface-hover);
  color: var(--color-accent);
}

.global-utility-rail button:active:not(:disabled) { transform: scale(.97); }
.global-utility-rail button:focus-visible { outline: 2px solid var(--color-accent); outline-offset: -2px; }
.global-utility-rail button:disabled { cursor: wait; opacity: .7; }

.ai-floating-btn.is-dragging { cursor: grabbing; filter: brightness(1.08); }
.ai-floating-btn.is-dragging .ai-pet-image { animation: none; }

.ai-pet-image {
  display: block;
  width: 68px;
  height: 68px;
  object-fit: contain;
  pointer-events: none;
  animation: sprinta-pet-idle 3.2s ease-in-out infinite;
}

.ai-selection-popover {
  position: fixed;
  z-index: 1450;
  display: flex;
  gap: 4px;
  padding: 5px;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-surface-elevated);
  box-shadow: var(--shadow-popover);
}

.ai-selection-popover button {
  border: 0;
  border-radius: 6px;
  padding: 6px 8px;
  background: transparent;
  color: var(--color-text-primary);
  font-size: 11px;
  cursor: pointer;
}

.ai-selection-popover button:hover,
.ai-selection-popover button:focus-visible {
  background: var(--sa-primary-soft);
  color: var(--color-accent);
  outline: none;
}

.ai-floating-btn:focus-visible,
.close-ai:focus-visible,
.ai-open-full-chat:focus-visible,
.quick-action:focus-visible,
.send-btn:focus-visible,
.ai-composer-icon-btn:focus-visible,
.ai-attachment-actions button:focus-visible,
.ai-attachment-thumbnail:focus-visible,
.ai-context-card button:focus-visible,
.ai-selected-text button:focus-visible,
.ai-input-foot button:focus-visible {
  outline: 3px solid color-mix(in srgb, var(--sa-primary) 55%, var(--color-text-inverse));
  outline-offset: 3px;
}

.ai-mobile-backdrop {
  position: fixed;
  inset: var(--sa-topbar-height, 52px) 0 0;
  z-index: 1490;
  background: color-mix(in srgb, var(--color-bg) 48%, transparent);
  backdrop-filter: blur(3px);
}

.ai-sidebar {
  position: fixed;
  right: 16px;
  top: calc(var(--sa-topbar-height, 52px) + 16px);
  width: clamp(360px, var(--ai-panel-width, 456px), min(720px, 70vw));
  height: clamp(500px, var(--ai-panel-height, 680px), calc(100dvh - var(--sa-topbar-height, 52px) - 32px));
  max-height: calc(100dvh - var(--sa-topbar-height, 52px) - 32px);
  box-sizing: border-box;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 18px;
  box-shadow: 0 24px 70px rgb(15 35 60 / 0.22), 0 1px 0 rgb(255 255 255 / 0.18) inset;
  z-index: 1500;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.ai-sidebar.is-resizing,
.ai-sidebar.is-resizing * { user-select: none; }

.ai-resize-handle {
  position: absolute;
  z-index: 3;
  top: 18px;
  bottom: 18px;
  left: -5px;
  width: 10px;
  cursor: ew-resize;
  touch-action: none;
}

.ai-resize-handle::after {
  content: '';
  position: absolute;
  top: 50%;
  left: 4px;
  width: 2px;
  height: 48px;
  border-radius: 999px;
  background: color-mix(in srgb, var(--color-accent) 60%, transparent);
  opacity: 0;
  transform: translateY(-50%);
  transition: opacity .16s ease;
}

.ai-sidebar:hover .ai-resize-handle::after,
.ai-sidebar.is-resizing .ai-resize-handle::after { opacity: 1; }

.ai-hero {
  padding: 20px 20px 17px;
  border-bottom: 1px solid var(--color-border);
  background: var(--color-surface);
}

.quick-actions,
.ai-action-preview-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-top: 12px;
}

.ai-hero-top,
.ai-brand,
.ai-context-card {
  display: flex;
  flex-direction: row;
}

.ai-action-preview-card {
  padding: 14px;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface);
  box-shadow: var(--shadow-sm);
  width: 100%;
  min-width: 0;
  box-sizing: border-box;
  flex: 0 0 auto;
}

.ai-activity-note {
  display: flex;
  align-items: center;
  gap: 7px;
  margin: 0;
  color: var(--color-text-muted);
  font-size: 11px;
}

.ai-activity-note i { color: var(--color-success); }

.ai-action-preview-card.is-pending {
  border-color: color-mix(in srgb, var(--sa-primary) 42%, var(--color-border));
  box-shadow: 0 0 0 1px color-mix(in srgb, var(--sa-primary) 12%, transparent), var(--shadow-sm);
}

.ai-action-preview-card.is-pending .ai-action-status {
  animation: ai-status-breathe 1.8s ease-in-out infinite;
}

.ai-action-preview-head,
.ai-action-controls {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.ai-action-preview-head strong,
.ai-action-eyebrow {
  display: block;
}

.ai-action-eyebrow {
  margin-bottom: 3px;
  color: var(--color-accent);
  font-size: 9px;
  font-weight: 800;
  letter-spacing: .08em;
}

.ai-action-preview-head strong {
  color: var(--color-text-primary);
  font-size: 13px;
}

.ai-action-status {
  flex: 0 0 auto;
  padding: 4px 7px;
  border-radius: 999px;
  color: var(--color-text-secondary);
  background: var(--color-surface);
  font-size: 10px;
  font-weight: 800;
}

.ai-action-status.is-success { color: var(--color-success); }
.ai-action-status.is-error { color: var(--color-danger); }
.ai-action-description,
.ai-action-result,
.ai-action-error {
  margin: 9px 0;
  color: var(--color-text-secondary);
  font-size: 12px;
  line-height: 1.5;
}

.ai-action-details {
  display: grid;
  grid-template-columns: minmax(84px, auto) minmax(0, 1fr);
  gap: 4px 8px;
  margin: 0 0 11px;
  font-size: 11px;
}

.ai-action-details dt { color: var(--color-text-muted); }
.ai-action-details dd { margin: 0; color: var(--color-text-primary); overflow-wrap: anywhere; }
.ai-action-error { color: var(--color-danger); }
.ai-action-result { color: var(--color-success); }

.ai-duplicate-warning {
  padding: 10px;
  border: 1px solid var(--color-warning);
  border-radius: 8px;
  background: var(--color-warning-bg);
  color: var(--color-text-primary);
}
.ai-duplicate-warning p { margin: 4px 0 8px; overflow-wrap: anywhere; }
.ai-duplicate-actions { display: flex; flex-wrap: wrap; gap: 6px; }
.ai-duplicate-actions button {
  min-height: 30px;
  padding: 6px 9px;
  border: 1px solid var(--color-warning);
  border-radius: 6px;
  background: var(--color-surface);
  color: var(--color-text-primary);
  cursor: pointer;
}
.ai-duplicate-actions .is-danger { background: var(--color-danger); color: var(--color-text-inverse); }

.ai-action-controls { justify-content: flex-end; }
.ai-action-controls button {
  min-width: 72px;
  min-height: 30px;
  padding: 6px 11px;
  border-radius: 8px;
  font-size: 11px;
  font-weight: 800;
  cursor: pointer;
}
.ai-action-controls button:disabled { cursor: not-allowed; opacity: .55; }
.ai-action-cancel { border: 1px solid var(--color-border); background: transparent; color: var(--color-text-secondary); }
.ai-action-confirm { border: 1px solid var(--sa-primary); background: var(--sa-primary); color: var(--color-text-inverse); }

.chat-message,
.message-bubble,
.ai-input-wrapper,
.ai-input-foot {
  display: flex;
}

.message-stack,
.message-bubble,
.ai-action-preview-list { min-width: 0; width: 100%; }
.message-stack { display: flex; flex-direction: column; align-items: stretch; }
.message-bubble { flex-direction: column; align-items: stretch; }
.ai-action-preview-list { flex: 0 0 auto; }
.ai-action-preview-list { align-items: stretch; }
.ai-action-description, .ai-action-result, .ai-action-error { overflow-wrap: anywhere; }

.ai-hero-top {
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.ai-hero-actions {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 0 0 auto;
}

.ai-brand {
  align-items: center;
  gap: 11px;
  min-width: 0;
}

.ai-brand-icon {
  width: 40px;
  height: 40px;
  display: grid;
  place-items: center;
  border-radius: 12px;
  background: var(--color-surface-hover);
  border: 1px solid var(--color-border);
  overflow: hidden;
}

.ai-brand-icon img {
  width: 34px;
  height: 34px;
  object-fit: contain;
}

.ai-brand p,
.ai-brand h4,
.ai-hero-copy {
  margin: 0;
}

.ai-brand p {
  color: var(--color-accent);
  font-size: 11px;
  font-weight: 900;
  letter-spacing: 0.08em;
}

.ai-brand h4 {
  font-size: 17px;
  line-height: 1.25;
}

.ai-hero-copy {
  margin-top: 12px;
  color: var(--color-text-secondary);
  font-size: 13px;
  line-height: 1.55;
}

.close-ai {
  width: 34px;
  height: 34px;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-surface);
  color: var(--color-text-muted);
  cursor: pointer;
}

.close-ai:hover {
  color: var(--color-text-primary);
  border-color: color-mix(in srgb, var(--sa-primary) 36%, var(--color-border));
}

.ai-open-full-chat {
  width: 34px;
  height: 34px;
  display: grid;
  place-items: center;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-surface);
  color: var(--color-text-muted);
  cursor: pointer;
}

.ai-open-full-chat:hover,
.ai-open-full-chat:focus-visible {
  color: var(--color-accent);
  border-color: color-mix(in srgb, var(--sa-primary) 36%, var(--color-border));
  outline: none;
}

.ai-content {
  flex: 1;
  padding: 16px 18px 20px;
  overflow-y: auto;
  scrollbar-color: var(--color-border) transparent;
}

.quick-actions {
  gap: 8px;
  margin-bottom: 12px;
  flex-direction: row;
  flex-wrap: wrap;
}

.quick-action {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  min-height: 32px;
  padding: 6px 10px;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: var(--color-surface);
  color: var(--color-text-primary);
  font-size: 12px;
  font-weight: 800;
  cursor: pointer;
}

.quick-action:hover {
  border-color: color-mix(in srgb, var(--sa-primary) 36%, var(--color-border));
  background: var(--sa-primary-soft);
  color: var(--color-accent);
}

.ai-context-card {
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
  padding: 12px;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface-hover);
}

.ai-context-card strong,
.ai-context-card span {
  display: block;
}

.ai-context-card strong {
  font-size: 12px;
}

.ai-context-card span {
  margin-top: 2px;
  color: var(--color-text-secondary);
  font-size: 12px;
}

.ai-context-card button {
  width: 32px;
  height: 32px;
  border: 1px solid var(--color-border);
  border-radius: 9px;
  background: var(--color-surface);
  color: var(--color-accent);
  cursor: pointer;
}

.ai-pin-toggle {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  margin-top: 10px;
  padding: 6px 9px;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font-size: 11px;
  font-weight: 800;
  cursor: pointer;
}

.ai-pin-toggle:hover,
.ai-pin-toggle:focus-visible {
  border-color: var(--sa-primary);
  color: var(--color-accent);
}

.ai-conversation-toolbar {
  display: grid;
  grid-template-columns: 32px 32px minmax(0, 1fr);
  align-items: center;
  gap: 6px;
  margin-top: 10px;
}
.ai-conversation-toolbar button,
.ai-history-head button {
  width: 32px;
  height: 32px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  cursor: pointer;
}
.ai-conversation-toolbar span { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 12px; font-weight: 700; }
.ai-history-panel {
  position: absolute;
  inset: 0;
  z-index: 5;
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 16px;
  overflow-y: auto;
  background: var(--color-surface);
}
.ai-history-head { display: flex; align-items: center; justify-content: space-between; }
.ai-history-panel > input { min-height: 38px; padding: 8px 10px; border: 1px solid var(--color-border); border-radius: 6px; background: var(--color-bg); color: var(--color-text-primary); }
.ai-history-item { display: grid; grid-template-columns: minmax(0, 1fr) 24px 24px; align-items: center; gap: 6px; width: 100%; padding: 9px; border: 1px solid var(--color-border); border-radius: 6px; background: transparent; color: var(--color-text-primary); text-align: left; cursor: pointer; }
.ai-history-item.active { border-color: var(--sa-primary); background: color-mix(in srgb, var(--sa-primary) 8%, transparent); }
.ai-history-item span { min-width: 0; }
.ai-history-item strong, .ai-history-item small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.ai-history-item small { margin-top: 3px; color: var(--color-text-muted); }
.ai-history-more { min-height: 36px; border: 1px solid var(--color-border); border-radius: 6px; background: var(--color-surface-hover); color: var(--color-text-primary); cursor: pointer; }

.ai-selected-text {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: -4px 0 14px;
  padding: 8px 10px;
  border: 1px solid color-mix(in srgb, var(--sa-primary) 30%, var(--color-border));
  border-radius: 8px;
  background: var(--sa-primary-soft);
  color: var(--color-text-secondary);
  font-size: 12px;
}

.ai-selected-text > i {
  color: var(--color-accent);
}

.ai-selected-text span {
  flex: 1;
}

.ai-selected-text button {
  width: 24px;
  height: 24px;
  border: 0;
  border-radius: 6px;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
}

.ai-selected-text button:hover {
  background: var(--color-surface);
  color: var(--color-text-primary);
}

.chat-thread {
  display: grid;
  gap: 14px;
}

.chat-message {
  align-items: flex-start;
  gap: 10px;
}

.chat-message.user {
  flex-direction: row-reverse;
}

.message-avatar {
  width: 30px;
  height: 30px;
  flex: 0 0 30px;
  display: grid;
  place-items: center;
  border-radius: 9px;
  background: var(--color-surface-hover);
  color: var(--color-text-secondary);
}

.message-avatar img {
  width: 26px;
  height: 26px;
  object-fit: contain;
}

.chat-message.bot .message-avatar {
  background: var(--sa-primary-soft);
  color: var(--color-accent);
}

.chat-message.user .message-avatar {
  background: color-mix(in srgb, var(--color-success) 14%, var(--color-surface));
  color: var(--color-success);
}

.message-stack {
  max-width: calc(100% - 42px);
}

.chat-message.user .message-stack {
  display: grid;
  justify-items: end;
}

.message-author {
  display: block;
  margin-bottom: 4px;
  color: var(--color-text-muted);
  font-size: 11px;
  font-weight: 800;
}

.message-bubble {
  align-items: flex-start;
  gap: 8px;
  max-width: 100%;
  padding: 10px 12px;
  border: 1px solid var(--color-border);
  border-radius: 14px;
  border-top-left-radius: 5px;
  background: var(--color-surface);
  color: var(--color-text-primary);
  font-size: 13px;
  line-height: 1.55;
  white-space: pre-wrap;
  box-shadow: 0 6px 18px rgb(15 35 60 / 0.06);
  position: relative;
}

.message-tools {
  display: flex;
  gap: 4px;
  margin-top: 8px;
  padding-top: 7px;
  border-top: 1px solid var(--color-border);
}

.message-tools button {
  width: 26px;
  height: 26px;
  border: 0;
  border-radius: 7px;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
}

.message-tools button:hover,
.message-tools button:focus-visible {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
  outline: none;
}

.chat-message.user .message-bubble {
  border-top-left-radius: 14px;
  border-top-right-radius: 5px;
  border-color: color-mix(in srgb, var(--sa-primary) 30%, var(--color-border));
  background: color-mix(in srgb, var(--sa-primary-soft) 68%, var(--color-surface));
}

.ai-input-area {
  position: relative;
  padding: 14px 18px 16px;
  border-top: 1px solid var(--color-border);
  background: color-mix(in srgb, var(--color-surface) 92%, var(--color-surface-hover));
}

.ai-input-area.is-dragging-files {
  outline: 2px solid var(--color-accent);
  outline-offset: -4px;
  background: color-mix(in srgb, var(--sa-primary-soft) 52%, var(--color-surface));
}

.ai-attachment-input {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
  clip-path: inset(50%);
  white-space: nowrap;
}

.ai-attachment-tray {
  display: grid;
  gap: 8px;
  max-height: 220px;
  margin-bottom: 10px;
  overflow-y: auto;
  scrollbar-color: var(--color-border) transparent;
}

.ai-attachment-card {
  display: grid;
  grid-template-columns: 72px minmax(0, 1fr) auto;
  align-items: center;
  gap: 10px;
  min-width: 0;
  padding: 8px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface);
}

.ai-attachment-card.is-document {
  grid-template-columns: 48px minmax(0, 1fr) auto;
}

.ai-attachment-thumbnail {
  width: 72px;
  height: 54px;
  padding: 0;
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface-hover);
  cursor: pointer;
}

.ai-attachment-thumbnail img {
  width: 100%;
  height: 100%;
  display: block;
  object-fit: cover;
}

.ai-attachment-file-icon {
  width: 48px;
  height: 48px;
  display: grid;
  place-items: center;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface-hover);
  color: var(--color-accent);
  font-size: 20px;
}

.ai-attachment-meta {
  min-width: 0;
}

.ai-attachment-meta strong,
.ai-attachment-meta span,
.ai-attachment-meta small {
  display: block;
}

.ai-attachment-meta strong {
  overflow: hidden;
  color: var(--color-text-primary);
  font-size: 12px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ai-attachment-meta span {
  margin-top: 3px;
  color: var(--color-text-muted);
  font-size: 10px;
  overflow-wrap: anywhere;
}

.ai-attachment-meta small {
  margin-top: 5px;
  color: #b45309;
  font-size: 10px;
  font-weight: 700;
}

.ai-attachment-meta small i {
  margin-right: 4px;
}

.ai-attachment-meta small.is-ready { color: var(--color-success); }
.ai-attachment-meta small.is-error { color: var(--color-danger); }
.ai-attachment-meta small.is-uploading,
.ai-attachment-meta small.is-processing { color: var(--color-accent); }

.message-attachments {
  display: grid;
  width: min(100%, 390px);
  gap: 8px;
}

.message-attachment-card {
  display: grid;
  grid-template-columns: 48px minmax(0, 1fr) 32px;
  align-items: center;
  gap: 9px;
  min-width: 0;
  padding: 7px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: color-mix(in srgb, var(--color-surface-hover) 62%, transparent);
}

.message-attachment-image {
  width: 72px;
  height: 54px;
  display: grid;
  place-items: center;
  padding: 0;
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface-hover);
  color: var(--color-accent);
  cursor: pointer;
}

.message-attachment-card:has(.message-attachment-image) {
  grid-template-columns: 72px minmax(0, 1fr) 32px;
}

.message-attachment-image img {
  width: 100%;
  height: 100%;
  display: block;
  object-fit: cover;
}

.message-attachment-open {
  width: 32px;
  height: 32px;
  display: grid;
  place-items: center;
  padding: 0;
  border: 0;
  border-radius: 6px;
  background: transparent;
  color: var(--color-text-secondary);
  cursor: pointer;
}

.message-attachment-open:hover { background: var(--color-surface); color: var(--color-accent); }

.ai-citations {
  display: grid;
  width: 100%;
  gap: 6px;
  margin-top: 4px;
  padding-top: 9px;
  border-top: 1px solid var(--color-border);
}

.ai-citations > strong {
  color: var(--color-text-muted);
  font-size: 10px;
  text-transform: uppercase;
}

.ai-citations button {
  display: grid;
  gap: 2px;
  min-width: 0;
  padding: 7px 8px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: transparent;
  color: var(--color-text-primary);
  text-align: left;
  cursor: pointer;
}

.ai-citations button:hover { border-color: var(--color-accent); }
.ai-citations span { font-size: 11px; font-weight: 800; overflow-wrap: anywhere; }
.ai-citations small { color: var(--color-text-muted); font-size: 10px; line-height: 1.4; overflow-wrap: anywhere; }

.ai-attachment-actions {
  display: flex;
  align-items: center;
  gap: 4px;
}

.ai-attachment-actions button,
.ai-composer-icon-btn {
  width: 34px;
  height: 34px;
  display: grid;
  place-items: center;
  flex: 0 0 34px;
  padding: 0;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: transparent;
  color: var(--color-text-secondary);
  cursor: pointer;
}

.ai-attachment-actions button:hover,
.ai-composer-icon-btn:hover {
  border-color: color-mix(in srgb, var(--sa-primary) 42%, var(--color-border));
  background: var(--sa-primary-soft);
  color: var(--color-accent);
}

.ai-composer-icon-btn.active {
  border-color: var(--color-accent);
  background: var(--sa-primary-soft);
  color: var(--color-accent);
}

.ai-composer-icon-btn:disabled {
  cursor: not-allowed;
  opacity: 0.65;
}

.ai-voice-panel {
  display: grid;
  gap: 10px;
  margin-bottom: 10px;
  padding: 12px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: color-mix(in srgb, var(--color-surface) 92%, var(--sa-primary-soft));
}

.ai-voice-head,
.ai-voice-head > div,
.ai-voice-actions {
  display: flex;
  align-items: center;
}

.ai-voice-head {
  justify-content: space-between;
  gap: 12px;
}

.ai-voice-head > div { gap: 8px; }
.ai-voice-head strong { color: var(--color-text-primary); font-size: 13px; }

.ai-voice-timer {
  color: var(--color-danger);
  font: 700 12px/1 ui-monospace, SFMono-Regular, Consolas, monospace;
}

.ai-voice-language {
  display: grid;
  gap: 4px;
  min-width: 0;
  color: var(--color-text-muted);
  font-size: 10px;
}

.ai-voice-language select {
  max-width: 180px;
  height: 30px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface);
  color: var(--color-text-primary);
  padding: 0 8px;
  font-size: 11px;
}

.ai-voice-note,
.ai-voice-error {
  margin: 0;
  color: var(--color-text-secondary);
  font-size: 11px;
  line-height: 1.5;
}

.ai-voice-error { color: var(--color-danger); }

.ai-voice-transcript {
  display: grid;
  gap: 6px;
  color: var(--color-text-muted);
  font-size: 11px;
  font-weight: 700;
}

.ai-voice-transcript textarea {
  width: 100%;
  min-height: 92px;
  resize: vertical;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface);
  color: var(--color-text-primary);
  padding: 9px 10px;
  font: inherit;
  font-weight: 500;
  line-height: 1.5;
}

.ai-voice-actions {
  justify-content: flex-end;
  flex-wrap: wrap;
  gap: 6px;
}

.ai-voice-actions button {
  min-height: 32px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  padding: 0 10px;
  cursor: pointer;
  font-size: 11px;
  font-weight: 700;
}

.ai-voice-secondary { background: transparent; color: var(--color-text-secondary); }
.ai-voice-primary { border-color: var(--color-accent) !important; background: var(--color-accent); color: var(--color-text-inverse); }
.ai-voice-actions button:disabled { cursor: not-allowed; opacity: 0.55; }

@media (max-width: 560px) {
  .ai-voice-head { align-items: stretch; flex-direction: column; }
  .ai-voice-language select { width: 100%; max-width: none; }
}

.ai-input-wrapper {
  align-items: center;
  gap: 8px;
  border: 1px solid color-mix(in srgb, var(--color-border) 84%, var(--sa-primary));
  border-radius: 16px;
  background: var(--color-surface);
  padding: 8px 9px 8px 12px;
  box-shadow: inset 0 1px 0 color-mix(in srgb, var(--color-text-inverse) 4%, transparent);
}

.ai-input-wrapper :deep(.el-dropdown) {
  flex: 0 0 44px;
}

.ai-input-wrapper .ai-composer-icon-btn,
.ai-input-wrapper .send-btn {
  width: 44px;
  height: 44px;
  flex-basis: 44px;
  border-radius: 12px;
}

.ai-input-wrapper:focus-within {
  border-color: var(--color-accent);
  box-shadow: none;
}

.markdown-body { min-width: 0; overflow-wrap: anywhere; }
.markdown-body p { margin: 0 0 8px; }
.markdown-body p:last-child { margin-bottom: 0; }
.markdown-body h2,
.markdown-body h3,
.markdown-body h4 { margin: 0 0 8px; color: var(--color-text-primary); line-height: 1.3; }
.markdown-body h2 { font-size: 15px; }
.markdown-body h3 { font-size: 14px; }
.markdown-body h4 { font-size: 13px; }
.markdown-body ul { margin: 6px 0 10px; padding-left: 18px; }
.markdown-body li { margin: 4px 0; }
.markdown-body code { padding: 2px 5px; border-radius: 5px; background: var(--color-surface-hover); font: 600 11px/1.4 ui-monospace, SFMono-Regular, Consolas, monospace; }
.markdown-body pre { margin: 9px 0; padding: 10px 12px; overflow-x: auto; border: 1px solid var(--color-border); border-radius: 9px; background: color-mix(in srgb, var(--color-bg) 72%, var(--color-surface)); }
.markdown-body pre code { padding: 0; background: transparent; font-weight: 500; white-space: pre; }
.markdown-body .md-list-index { color: var(--color-accent); font-weight: 800; }

.ai-input-wrapper textarea {
  flex: 1;
  min-height: 44px !important;
  max-height: 170px;
  resize: none;
  background: transparent !important;
  border: 0 !important;
  color: var(--color-text-primary) !important;
  padding: 8px 10px !important;
  line-height: 1.5;
  outline: none;
  box-shadow: none !important;
}

.send-btn {
  width: 40px;
  height: 40px;
  flex: 0 0 40px;
  border: 0;
  border-radius: 12px;
  background: var(--color-accent);
  color: var(--color-text-inverse);
  cursor: pointer;
  display: grid;
  place-items: center;
}

.send-btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.ai-input-foot {
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  margin-top: 8px;
  color: var(--color-text-muted);
  font-size: 11px;
}

.ai-input-foot button {
  border: 0;
  background: transparent;
  color: var(--color-accent);
  font-weight: 800;
  cursor: pointer;
}

.ai-backdrop-fade-enter-active,
.ai-backdrop-fade-leave-active {
  transition: opacity 0.2s ease;
}

.ai-backdrop-fade-enter-from,
.ai-backdrop-fade-leave-to {
  opacity: 0;
}

.slide-right-enter-active,
.slide-right-leave-active {
  transition: transform 0.3s ease;
}

.slide-right-enter-from,
.slide-right-leave-to {
  transform: translateX(100%);
}

@media (max-width: 760px) {
  .content-area {
    border-left: 0;
  }

  .main-body {
    min-width: 0;
  }

  .ai-sidebar {
    top: calc(var(--sa-topbar-height, 52px) + env(safe-area-inset-top));
    right: 0;
    left: 0;
    bottom: env(safe-area-inset-bottom);
    width: auto;
    height: auto;
    max-height: none;
    border-radius: 14px 14px 0 0;
  }

  .ai-resize-handle { display: none; }
  .ai-input-area { padding-bottom: calc(12px + env(safe-area-inset-bottom)); }

  .ai-floating-btn {
    width: 58px;
    height: 58px;
  }

  .global-utility-rail {
    right: 8px;
  }

  .sticky-launcher-main { min-width: 72px; padding: 0 8px; }

  .ai-pet-image {
    width: 58px;
    height: 58px;
  }

  .quick-action {
    flex: 1 1 calc(50% - 6px);
    justify-content: center;
  }

  .ai-action-preview-head { align-items: flex-start; flex-direction: column; }
  .ai-action-controls { justify-content: stretch; flex-direction: column-reverse; }
  .ai-action-controls button { width: 100%; min-height: 38px; }
  .ai-action-details { grid-template-columns: 1fr; gap: 2px; }
  .ai-action-details dd { margin-bottom: 6px; }
}

@media (min-width: 761px) and (max-width: 1024px) {
  .ai-sidebar {
    top: calc(var(--sa-topbar-height, 52px) + 12px);
    right: 12px;
    bottom: 12px;
    left: auto;
    width: min(560px, calc(100vw - 24px));
    height: auto;
    max-height: none;
    border-radius: 16px;
  }

  .ai-resize-handle { display: none; }
}

.offline-warning-banner {
  position: fixed;
  bottom: 24px;
  left: 50%;
  transform: translateX(-50%);
  background: rgba(220, 38, 38, 0.88);
  backdrop-filter: blur(10px);
  -webkit-backdrop-filter: blur(10px);
  color: #ffffff;
  padding: 8px 18px;
  border-radius: 9999px;
  box-shadow: 0 4px 16px rgba(220, 38, 38, 0.25), 0 2px 4px rgba(0, 0, 0, 0.1);
  z-index: 9999;
  display: flex;
  align-items: center;
  font-size: 13px;
  font-weight: 500;
  border: 1px solid rgba(255, 255, 255, 0.15);
  pointer-events: none;
  transition: opacity 0.3s ease;
}

@keyframes sprinta-pet-idle {
  0%, 100% { transform: translateY(0) rotate(0deg); }
  50% { transform: translateY(-3px) rotate(-1deg); }
}

@keyframes ai-status-breathe {
  0%, 100% { opacity: 0.72; }
  50% { opacity: 1; }
}

@media (prefers-reduced-motion: reduce) {
  .ai-pet-image,
  .ai-action-preview-card.is-pending .ai-action-status {
    animation: none;
  }
}

.ai-credit-card {
  margin-top: 12px;
  padding: 12px;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface-hover);
}

.ai-credit-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.ai-credit-head > div {
  display: flex;
  align-items: center;
  gap: 8px;
}

.ai-credit-label {
  color: var(--color-text-muted);
  font-size: 10px;
  font-weight: 800;
  letter-spacing: .08em;
}

.ai-credit-head strong { font-size: 12px; }

.ai-credit-progress {
  height: 6px;
  margin-top: 10px;
  overflow: hidden;
  border-radius: 999px;
  background: color-mix(in srgb, var(--color-border) 78%, transparent);
}

.ai-credit-progress > span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: var(--color-accent);
  transition: width .25s ease;
}

.ai-credit-message {
  margin: 8px 0 0;
  color: var(--color-text-secondary);
  font-size: 11px;
  line-height: 1.4;
}

.ai-credit-buy {
  min-height: 32px;
  margin-top: 9px;
  padding: 0 10px;
  border: 1px solid color-mix(in srgb, var(--color-accent) 45%, var(--color-border));
  border-radius: 8px;
  background: transparent;
  color: var(--color-accent);
  font-size: 11px;
  font-weight: 800;
  cursor: pointer;
}

.ai-credit-buy:hover,
.ai-credit-buy:focus-visible {
  background: var(--sa-primary-soft);
  outline: none;
}

.ai-credit-card.is-low { border-color: var(--color-warning); }
.ai-credit-card.is-low .ai-credit-progress > span { background: var(--color-warning); }
.ai-credit-card.is-empty { border-color: var(--color-danger); }
.ai-credit-card.is-empty .ai-credit-progress > span { width: 0 !important; background: var(--color-danger); }

.persistent-call-overlay {
  position: fixed;
  z-index: 2000;
  bottom: 20px;
  left: 20px;
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 10px 16px;
  border-radius: 14px;
  border: 1px solid color-mix(in srgb, var(--color-success, #10b981) 40%, var(--color-border));
  background: var(--color-surface, #0f172a);
  color: var(--color-text-primary, #ffffff);
  box-shadow: 0 12px 32px rgba(0, 0, 0, 0.35);
  backdrop-filter: blur(12px);
  transition: all 0.2s ease;
}
.persistent-call-overlay .call-overlay-info {
  display: flex;
  align-items: center;
  gap: 10px;
  cursor: pointer;
}
.persistent-call-overlay .call-status-pulse {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: #10b981;
  box-shadow: 0 0 0 4px rgba(16, 185, 129, 0.25);
  animation: call-pulse-ping 2s infinite ease-in-out;
}
@keyframes call-pulse-ping {
  0%, 100% { transform: scale(1); opacity: 1; }
  50% { transform: scale(1.2); opacity: 0.7; }
}
.persistent-call-overlay .call-overlay-info strong {
  display: block;
  font-size: 13px;
  font-weight: 700;
  line-height: 1.2;
}
.persistent-call-overlay .call-overlay-info small {
  display: block;
  font-size: 11px;
  color: var(--color-text-muted, #94a3b8);
}
.persistent-call-overlay .call-overlay-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}
.persistent-call-overlay .call-action-pill {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  min-width: 36px;
  height: 36px;
  padding: 0 10px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.15s ease;
}
.persistent-call-overlay .call-action-pill:hover {
  border-color: var(--color-accent);
  background: color-mix(in srgb, var(--color-accent) 15%, var(--color-surface));
  color: var(--color-accent);
}
.persistent-call-overlay .call-action-pill.muted {
  background: rgba(239, 68, 68, 0.15);
  border-color: rgba(239, 68, 68, 0.4);
  color: #ef4444;
}
.persistent-call-overlay .call-action-pill.active {
  background: color-mix(in srgb, var(--color-accent) 20%, var(--color-surface));
  border-color: var(--color-accent);
  color: var(--color-accent);
}
.persistent-call-overlay .call-action-pill.open-call {
  background: var(--color-accent);
  border-color: var(--color-accent);
  color: #ffffff;
}
.persistent-call-overlay .call-action-pill.open-call:hover {
  opacity: 0.9;
}
.persistent-call-overlay .call-action-pill.hang-up {
  background: #ef4444;
  border-color: #ef4444;
  color: #ffffff;
}
.persistent-call-overlay .call-action-pill.hang-up:hover {
  background: #dc2626;
}
</style>
