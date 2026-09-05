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
              @click="runQuickPrompt(prompt.text)"
            >
              <i :class="prompt.icon"></i>
              <span>{{ prompt.label }}</span>
            </button>
          </div>

          <div class="ai-context-card">
            <div>
              <span class="ai-context-eyebrow">PHẠM VI AI</span>
              <strong>Workspace: {{ currentWorkspaceLabel }}</strong>
              <small>Project: {{ currentProjectLabel }}</small>
              <div class="ai-page-context">
                <span>TRANG HIỆN TẠI</span>
                <strong>{{ currentRouteLabel }}</strong>
              </div>
            </div>
            <button type="button" @click="runQuickPrompt(`${aiCopy.currentPagePrompt}: ${currentRouteLabel}`)">
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
              v-for="(message, index) in chatHistory"
              :key="`shared-${message.role}-${index}`"
              :message="message"
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
            <template v-if="false">
            <div
              v-for="(message, index) in chatHistory"
              :key="`${message.role}-${index}`"
              class="chat-message"
              :class="message.role"
            >
              <div class="message-avatar" :class="message.role === 'user' ? 'user-avatar' : 'ai-avatar'">
                <img v-if="message.role === 'bot'" src="/ai-sprinta/idle.png" alt="Mascot SprintA AI" />
                <img v-else-if="profileAvatar" :src="profileAvatar" :alt="`Ảnh đại diện của ${profileName}`" />
                <span v-else aria-hidden="true">{{ profileInitials }}</span>
              </div>
              <div class="message-stack">
                <span class="message-author">{{ message.role === 'bot' ? aiCopy.botName : aiCopy.you }}</span>
                <div class="message-bubble">
                  <i v-if="message.loading" class="fa-solid fa-spinner fa-spin mr-2"></i>
                  <div v-if="message.attachments?.length" class="message-attachments" role="list" aria-label="Attachment trong tin nhắn">
                    <article v-for="attachment in message.attachments" :key="attachment.id" class="message-attachment-card" role="listitem">
                      <button v-if="attachment.kind === 'image'" class="message-attachment-image" type="button" @click="openAttachmentPreview(attachment)">
                        <img v-if="attachment.previewUrl" :src="attachment.previewUrl" :alt="attachment.name" />
                        <i v-else class="fa-regular fa-image" aria-hidden="true"></i>
                      </button>
                      <div v-else class="ai-attachment-file-icon" aria-hidden="true"><i :class="attachment.icon"></i></div>
                      <div class="ai-attachment-meta">
                        <strong>{{ attachment.name }}</strong>
                        <span>{{ attachment.typeLabel }} · {{ formatAttachmentBytes(attachment.size) }}</span>
                        <small><i class="fa-solid fa-circle-check"></i> Đã xử lý</small>
                      </div>
                      <button class="message-attachment-open" type="button" :title="`Mở ${attachment.name}`" @click="openAttachmentPreview(attachment)">
                        <i class="fa-solid fa-up-right-from-square"></i>
                      </button>
                    </article>
                  </div>
                  <div class="markdown-body" v-html="renderMarkdown(message.content)"></div>
                  <div v-if="message.citations?.length" class="ai-citations" aria-label="Nguồn trích dẫn">
                    <strong>Nguồn</strong>
                    <button v-for="citation in message.citations" :key="`${citation.sourceId}-${citation.attachmentId}`" type="button" @click="openCitation(citation)">
                      <span>[{{ citation.sourceId }}] {{ citation.fileName }} · {{ citation.locator }}</span>
                      <small>{{ citation.excerpt }}</small>
                    </button>
                  </div>
                  <div v-if="message.role === 'bot' && !message.loading" class="message-tools" aria-label="Thao tác với câu trả lời">
                    <button type="button" title="Sao chép câu trả lời" @click="copyAiMessage(message.content)">
                      <i class="fa-regular fa-copy"></i>
                    </button>
                    <button type="button" title="Hỏi tiếp từ câu trả lời" @click="continueFromAiMessage(message.content)">
                      <i class="fa-solid fa-reply"></i>
                    </button>
                  </div>

                  <!-- Cảnh báo (warnings) -->
                  <div v-if="message.warnings && message.warnings.length" class="ai-warnings mt-3 bg-red-50 dark:bg-red-950/20 p-2.5 rounded border border-red-200 dark:border-red-900/50">
                    <div class="text-xs font-semibold text-red-600 dark:text-red-400 mb-1 flex items-center gap-1.5">
                      <i class="fa-solid fa-triangle-exclamation"></i> Cảnh báo rủi ro
                    </div>
                    <ul class="list-disc pl-4 text-xs text-red-700 dark:text-red-300 space-y-0.5">
                      <li v-for="(warn, wIdx) in message.warnings" :key="wIdx">{{ warn }}</li>
                    </ul>
                  </div>

                  <!-- Gợi ý hành động (suggestedActions) -->
                  <div v-if="message.actions && message.actions.length" class="ai-action-preview-list" aria-label="AI action previews">
                    <p v-if="hasReadOnlyActions(message.actions)" class="ai-activity-note" role="status">
                      <i class="fa-solid fa-circle-check"></i> Đã đọc dữ liệu hiện tại và bổ sung kết quả vào câu trả lời.
                    </p>
                    <article v-for="(action, aIdx) in writeActions(message.actions)" :key="`${action.type}-${aIdx}`" class="ai-action-preview-card" :class="{ 'is-pending': action.uiStatus === 'pending' }">
                      <div class="ai-action-preview-head">
                        <div>
                          <span class="ai-action-eyebrow">AI ACTION PREVIEW</span>
                          <strong>{{ actionLabel(action.type) }}</strong>
                        </div>
                        <span class="ai-action-status" :class="`is-${action.uiStatus || 'pending'}`">{{ actionStatusLabel(action) }}</span>
                      </div>
                      <p class="ai-action-description">{{ action.description || actionSummary(action) }}</p>
                      <dl class="ai-action-details">
                        <template v-for="detail in actionDetails(action)" :key="detail.label">
                          <dt>{{ detail.label }}</dt>
                          <dd>{{ detail.value }}</dd>
                        </template>
                      </dl>
                      <div v-if="action.duplicateCandidate" class="ai-duplicate-warning" role="alert">
                        <strong>Đã có công việc tương tự trong dự án</strong>
                        <p>#{{ action.duplicateCandidate.sequenceId || action.duplicateCandidate.id }} · {{ action.duplicateCandidate.title }} · {{ action.duplicateCandidate.statusName }}</p>
                        <div class="ai-duplicate-actions">
                          <button type="button" @click="openDuplicateTask(action, false)">Mở công việc hiện có</button>
                          <button type="button" @click="openDuplicateTask(action, true)">Cập nhật công việc hiện có</button>
                          <button type="button" class="is-danger" @click="confirmDuplicateCreation(action)">Vẫn tạo công việc mới</button>
                        </div>
                      </div>
                      <p v-if="action.error" class="ai-action-error" role="alert">{{ action.error }}</p>
                      <p v-if="action.result?.message" class="ai-action-result" role="status">{{ action.result.message }}</p>
                      <div v-if="!action.duplicateCandidate" class="ai-action-controls">
                        <button v-if="action.uiStatus === 'cancelled'" type="button" class="ai-action-confirm" @click="retryAiAction(action)">
                          <i class="fa-solid fa-rotate-right"></i>
                          Thực hiện lại
                        </button>
                        <button v-else-if="action.uiStatus === 'error'" type="button" class="ai-action-confirm" :disabled="action.loading" @click="executeAiAction(action)">
                          <i class="fa-solid fa-rotate-right"></i>
                          Thử lại
                        </button>
                        <template v-else>
                          <button v-if="!isReadOnlyAction(action.type, action.requiresConfirmation) && action.uiStatus !== 'success'" type="button" class="ai-action-cancel" :disabled="action.loading" @click="cancelAiAction(action)">Hủy</button>
                          <button type="button" class="ai-action-confirm" :disabled="action.loading || action.uiStatus === 'success'" @click="executeAiAction(action)">
                          <i v-if="action.loading" class="fa-solid fa-spinner fa-spin"></i>
                          <i v-else-if="action.uiStatus === 'success'" class="fa-solid fa-check"></i>
                          {{ action.uiStatus === 'success' ? 'Đã thực hiện' : (isReadOnlyAction(action.type, action.requiresConfirmation) ? 'Xem kết quả' : 'Xác nhận') }}
                          </button>
                        </template>
                      </div>
                    </article>
                  </div>

                  <div v-if="message.suggestedActions && message.suggestedActions.length" class="ai-actions mt-3 flex flex-col gap-2">
                    <div v-for="(action, aIdx) in message.suggestedActions" :key="aIdx" class="action-card bg-primary-light dark:bg-primary-dark/30 p-2.5 rounded border border-gray-200 dark:border-gray-800">
                      <p class="text-xs text-gray-700 dark:text-gray-300 font-medium">Chuyển công việc sang trạng thái mới:</p>
                      <div class="flex justify-between items-center mt-2 gap-2">
                        <span class="text-xs text-gray-500 font-semibold">{{ action.taskTitle }} &rarr; {{ action.statusName }}</span>
                        <el-button 
                          size="small" 
                          type="success"
                          :loading="action.loading"
                          :disabled="action.completed || !canUpdateTaskInProject"
                          @click="confirmSuggestedAction(action)"
                        >
                          {{ action.completed ? 'Đã thực hiện' : 'Xác nhận chuyển' }}
                        </el-button>
                      </div>
                    </div>
                  </div>

                  <!-- Đề xuất công việc (suggestedTasks) -->
                  <div v-if="message.suggestedTasks && message.suggestedTasks.length" class="ai-suggested-tasks mt-3 p-3 bg-gray-50 dark:bg-gray-900/30 rounded border border-gray-200 dark:border-gray-800">
                    <div class="flex justify-between items-center mb-2.5 pb-1.5 border-b border-gray-200 dark:border-gray-800">
                      <span class="text-xs font-semibold text-gray-700 dark:text-gray-300 flex items-center gap-1.5">
                        <i class="fa-solid fa-list-check text-blue-500"></i> AI đề xuất công việc
                      </span>
                      <el-button 
                        v-if="message.suggestedTasks.some(t => !t.created)"
                        size="small" 
                        type="primary" 
                        link
                        :disabled="!canCreateTaskInProject"
                        @click="createAllSuggestedTasks(message)"
                      >
                        Tạo tất cả
                      </el-button>
                    </div>
                    
                    <div class="space-y-2.5 max-h-[300px] overflow-y-auto">
                      <div v-for="(task, tIdx) in message.suggestedTasks" :key="tIdx" class="suggested-task-item p-2 bg-white dark:bg-gray-950 rounded border border-gray-100 dark:border-gray-900 text-xs">
                        <div class="font-medium text-gray-800 dark:text-gray-200 flex justify-between gap-2">
                          <span>{{ task.title }}</span>
                          <span v-if="task.priority" class="text-[10px] px-1.5 py-0.5 rounded" :class="[
                            task.priority === 1 ? 'bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300' :
                            task.priority === 2 ? 'bg-orange-100 text-orange-700 dark:bg-orange-950 dark:text-orange-300' :
                            task.priority === 4 ? 'bg-gray-100 text-gray-700 dark:bg-gray-900 dark:text-gray-300' :
                            'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300'
                          ]">
                            P{{ task.priority }}
                          </span>
                        </div>
                        <p class="text-gray-500 dark:text-gray-400 mt-1 text-[11px] leading-relaxed">{{ task.description }}</p>
                        
                        <div class="mt-2.5 flex justify-between items-center text-[10px] text-gray-400">
                          <span>Hạn: {{ task.dueDate || 'N/A' }}</span>
                          <span>{{ task.assigneeEmail || '' }}</span>
                        </div>

                        <div class="mt-2.5 pt-2 border-t border-gray-100 dark:border-gray-900 flex justify-end">
                          <span v-if="task.created" class="text-xs text-green-600 dark:text-green-400 font-semibold flex items-center gap-1">
                            <i class="fa-solid fa-circle-check"></i> Đã tạo
                          </span>
                          <el-button 
                            v-else
                            size="small" 
                            type="primary" 
                            plain
                            :loading="task.loading"
                            :disabled="!canCreateTaskInProject"
                            @click="createSuggestedTask(task, message)"
                          >
                            Tạo task này
                          </el-button>
                        </div>
                      </div>
                    </div>
                    
                    <div v-if="!canCreateTaskInProject" class="text-[10px] text-red-500 mt-2 text-center">
                      Bạn không có quyền tạo công việc trong dự án này.
                    </div>
                  </div>

                  <!-- Prompt gợi ý (suggestedPrompts) -->
                  <div v-if="message.suggestedPrompts && message.suggestedPrompts.length" class="ai-suggested-prompts mt-3 pt-2.5 border-t border-dashed border-gray-200 dark:border-gray-800 flex flex-wrap gap-1.5">
                    <button 
                      v-for="(p, pIdx) in message.suggestedPrompts" 
                      :key="pIdx"
                      class="px-2.5 py-1.5 rounded-full bg-gray-100 dark:bg-gray-900 hover:bg-blue-50 dark:hover:bg-blue-950 text-gray-700 dark:text-gray-300 hover:text-blue-600 dark:hover:text-blue-400 text-xs border border-gray-200 dark:border-gray-800 transition-colors text-left font-medium"
                      type="button"
                      @click="useQuickPrompt(p)"
                    >
                      <i class="fa-regular fa-lightbulb text-yellow-500 mr-1"></i>
                      <span>{{ p }}</span>
                    </button>
                  </div>
                </div>
              </div>
            </div>
            </template>
          </div>
        </div>

        <AiComposer
          ref="aiComposerRef"
          v-model="aiInput"
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
        <template v-if="false">
        <div
          class="ai-input-area"
          :class="{ 'is-dragging-files': composerDragActive }"
          @dragenter.prevent="composerDragActive = true"
          @dragover.prevent="composerDragActive = true"
          @dragleave.prevent="handleComposerDragLeave"
          @drop.prevent="handleComposerDrop"
        >
          <input
            ref="aiAttachmentInputRef"
            class="ai-attachment-input"
            type="file"
            multiple
            :accept="composerAttachmentAccept"
            @change="handleAttachmentInput"
          />

          <div v-if="pendingAttachments.length" class="ai-attachment-tray" role="list" aria-label="Tệp đang chờ tải lên">
            <article
              v-for="attachment in pendingAttachments"
              :key="attachment.id"
              class="ai-attachment-card"
              :class="`is-${attachment.kind}`"
              role="listitem"
            >
              <button
                v-if="attachment.kind === 'image'"
                class="ai-attachment-thumbnail"
                type="button"
                :title="`Mở ${attachment.name}`"
                @click="openAttachmentPreview(attachment)"
              >
                <img :src="attachment.previewUrl" :alt="attachment.name" />
              </button>
              <div v-else class="ai-attachment-file-icon" aria-hidden="true">
                <i :class="attachment.icon"></i>
              </div>

              <div class="ai-attachment-meta">
                <strong>{{ attachment.kind === 'image' ? attachment.displayName : attachment.name }}</strong>
                <span>
                  {{ attachment.typeLabel }} · {{ formatAttachmentBytes(attachment.size) }}
                  <template v-if="attachment.width && attachment.height"> · {{ attachment.width }}×{{ attachment.height }}</template>
                </span>
                <small :class="`is-${attachment.status || 'pending'}`"><i :class="attachmentStatusIcon(attachment.status)"></i> {{ attachmentStatusLabel(attachment.status) }}</small>
              </div>

              <div class="ai-attachment-actions">
                <button type="button" :title="`Mở ${attachment.name}`" @click="openAttachmentPreview(attachment)">
                  <i class="fa-solid fa-up-right-from-square"></i>
                </button>
                <button type="button" :title="`Gỡ ${attachment.name}`" @click="removePendingAttachment(attachment.id)">
                  <i class="fa-solid fa-xmark"></i>
                </button>
              </div>
            </article>
          </div>

          <section v-if="voiceState !== 'idle'" class="ai-voice-panel" aria-label="Nhập bằng giọng nói">
            <div class="ai-voice-head">
              <div>
                <strong>{{ voiceStatusTitle }}</strong>
                <span v-if="voiceState === 'recording'" class="ai-voice-timer">{{ voiceElapsedLabel }}</span>
              </div>
              <label class="ai-voice-language">
                <span>Ngôn ngữ giọng nói: {{ voiceLanguageLabel }}</span>
                <select v-model="voiceLanguage" :disabled="voiceState === 'transcribing'" aria-label="Ngôn ngữ giọng nói">
                  <option value="auto">Tự động (VI/EN)</option>
                  <option value="vi">Tiếng Việt</option>
                  <option value="en">English</option>
                </select>
              </label>
            </div>

            <p v-if="voiceState === 'requesting'" class="ai-voice-note" role="status">
              Trình duyệt đang yêu cầu quyền sử dụng microphone.
            </p>
            <p v-else-if="voiceState === 'recording'" class="ai-voice-note" role="status">
              Audio chỉ được giữ tạm để phiên âm và sẽ không được lưu vĩnh viễn.
            </p>
            <p v-else-if="voiceState === 'transcribing'" class="ai-voice-note" role="status">
              <i class="fa-solid fa-spinner fa-spin"></i> Đang chuyển giọng nói thành văn bản...
            </p>
            <p v-else-if="voiceState === 'error'" class="ai-voice-error" role="alert">{{ voiceError }}</p>

            <label v-if="voiceState === 'success'" class="ai-voice-transcript">
              <span>Transcript</span>
              <textarea v-model="voiceTranscript" rows="4" aria-label="Chỉnh sửa transcript"></textarea>
            </label>

            <div class="ai-voice-actions">
              <button type="button" class="ai-voice-secondary" @click="cancelVoiceInput">Hủy</button>
              <button v-if="voiceState === 'recording'" type="button" class="ai-voice-primary" @click="stopVoiceRecording">
                <i class="fa-solid fa-stop"></i> Dừng
              </button>
              <button v-if="voiceState === 'error'" type="button" class="ai-voice-primary" @click="startVoiceRecording">
                <i class="fa-solid fa-rotate-right"></i> Thử lại
              </button>
              <button v-if="voiceState === 'success'" type="button" class="ai-voice-secondary" @click="recordVoiceAgain">
                <i class="fa-solid fa-microphone-lines"></i> Thu lại
              </button>
              <button v-if="voiceState === 'success'" type="button" class="ai-voice-primary" :disabled="!voiceTranscript.trim()" @click="useVoiceTranscript">
                Dùng nội dung này
              </button>
            </div>
          </section>

          <div class="ai-input-wrapper">
            <el-dropdown trigger="click" placement="top-start" @command="handleAttachmentCommand">
              <button class="ai-composer-icon-btn" type="button" title="Thêm ảnh hoặc tài liệu" aria-label="Thêm ảnh hoặc tài liệu">
                <i class="fa-solid fa-plus"></i>
              </button>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item command="browse">
                    <i class="fa-regular fa-folder-open"></i> Chọn ảnh hoặc tài liệu
                  </el-dropdown-item>
                  <el-dropdown-item command="paste">
                    <i class="fa-regular fa-clipboard"></i> Dán ảnh từ clipboard
                  </el-dropdown-item>
                  <el-dropdown-item command="screenshot" :disabled="capturingScreenshot">
                    <i class="fa-solid fa-display"></i> Chụp màn hình
                  </el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
            <textarea
              ref="aiComposerRef"
              v-model="aiInput"
              rows="1"
              :aria-label="aiCopy.placeholder"
              :placeholder="aiCopy.placeholder"
              @paste="handleComposerPaste"
              @input="resizeAiComposer"
              @keydown="handleAiComposerKeydown"
            ></textarea>
            <button
              class="ai-composer-icon-btn"
              :class="{ active: voiceState !== 'idle' }"
              type="button"
              title="Nhập bằng giọng nói"
              aria-label="Nhập bằng giọng nói"
              :disabled="voiceState === 'requesting' || voiceState === 'recording' || voiceState === 'transcribing'"
              @click="startVoiceRecording"
            >
              <i class="fa-solid fa-microphone"></i>
            </button>
            <button class="send-btn" type="button" :disabled="aiSending || aiCreditsExhausted || (!aiInput.trim() && !pendingAttachments.length)" title="Gửi tin nhắn" aria-label="Gửi tin nhắn" @click="sendAiMessage">
              <i v-if="!aiSending" class="fa-solid fa-paper-plane"></i>
              <i v-else class="fa-solid fa-spinner fa-spin"></i>
            </button>
          </div>
          <div class="ai-input-foot">
            <span>{{ pendingAttachments.length ? 'Attachment sẽ được tải lên kho riêng tư khi gửi.' : aiCopy.enterHint }}</span>
            <button type="button" @click="startNewConversation">{{ aiCopy.reset }}</button>
          </div>
        </div>
        </template>
      </aside>
    </transition>

    <FloatingStickiesLayer @floated="closeNotes" />

    <GlobalStickiesDrawer
      :visible="notesVisible"
      :context="stickyContext"
      @close="closeNotes"
    />

    <CreateSpaceModal v-model:visible="createSpaceVisible" @created="handleSiteCreated" />
    <CreateProjectModal v-model:visible="createVisible" @created="handleProjectCreated" />
    <AiCreditsPurchaseModal v-model="aiCreditsModalVisible" :contact-context="aiContactContext" />

    <transition name="fade">
      <div v-if="isOffline" class="offline-warning-banner" role="alert">
        <i class="fa-solid fa-cloud-slash mr-2"></i>
        <span>Bạn đang offline. Một số dữ liệu có thể không cập nhật.</span>
      </div>
    </transition>

    <!-- Persistent Voice Call Dock Overlay (Google Meet / Discord Style Floating Dock) -->
    <Transition name="route-soft">
      <div
        v-if="voiceCallStore.hasActiveCall && route.name !== 'CollaborationChat'"
        class="persistent-call-overlay"
        :class="{ 'has-mini-video': (voiceCallStore.isCameraEnabled && voiceCallStore.hasLocalCameraTrack) || voiceCallStore.hasRemoteVideo }"
        role="region"
        aria-label="Kênh thoại đang kết nối"
      >
        <!-- Mini Floating Camera Tiles Grid (Google Meet style) -->
        <div v-if="(voiceCallStore.isCameraEnabled && voiceCallStore.hasLocalCameraTrack) || voiceCallStore.hasRemoteVideo" class="call-overlay-video-dock" @click="goToChatCall">
          <div v-if="voiceCallStore.isCameraEnabled && voiceCallStore.hasLocalCameraTrack" class="mini-video-tile self-tile">
            <video :ref="setMiniLocalVideoRef" autoplay playsinline muted class="mini-video-el"></video>
            <span class="mini-video-label">Bạn</span>
          </div>
          <template v-for="[connId, media] in voiceCallStore.remoteVideoStreams" :key="connId">
            <div v-if="media?.cameraStream && media.cameraStream.getVideoTracks().some(t => t.readyState === 'live')" class="mini-video-tile remote-tile">
              <video :ref="el => setMiniRemoteVideoRef(el, connId)" autoplay playsinline class="mini-video-el"></video>
              <span class="mini-video-label">{{ media.participantName || 'Đồng nghiệp' }}</span>
            </div>
          </template>
        </div>

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
import DOMPurify from 'dompurify'
import { useRoute, useRouter } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import CreateProjectModal from '../CreateProjectModal.vue'
import CreateSpaceModal from '../CreateSpaceModal.vue'
import AppTopBar from './AppTopBar.vue'
import NexusSidebar from './NexusSidebar.vue'
import AiComposer from '@/components/ai/AiComposer.vue'
import AiMessage from '@/components/ai/AiMessage.vue'
import AiCreditsPurchaseModal from '@/components/ai/AiCreditsPurchaseModal.vue'
import GlobalStickiesDrawer from '@/components/stickies/GlobalStickiesDrawer.vue'
import FloatingStickiesLayer from '@/components/stickies/FloatingStickiesLayer.vue'
import { useI18nStore } from '@/store/useI18nStore'
import { useAiPetStore } from '@/store/useAiPetStore'
import { useAiScopeStore } from '@/store/useAiScopeStore'
import { useAiConversationStore } from '@/store/useAiConversationStore'
import { useWorkTaskStore } from '@/store/useWorkTaskStore'
import { useProjectStore } from '@/store/useProjectStore'
import { useGoalStore } from '@/store/useGoalStore'
import { useSprintStore } from '@/store/useSprintStore'
import { useSiteStore } from '@/store/useSiteStore'
import { useVoiceCallStore } from '@/store/useVoiceCallStore'
import { AUTH_SESSION_CHANGED, getStoredUserSession } from '@/utils/authSession'
import { getDefaultPermissionMatrix, hasPermission } from '@/utils/permissionGuard'
import { buildSpacePath } from '@/utils/spaceRoute'
import { MAX_FLOATING_STICKIES, useStickyStore } from '@/store/useStickyStore'
import { getRandomPaletteColor } from '@/utils/colors'
import { getStickyAccountId } from '@/utils/stickyAccountIsolation'
import { AI_QUICK_ACTIONS, aiActionPayload, normalizeAiActionList } from '@/utils/aiActionUi'
import { decorateAiAction, findPendingAiAction, isAiConfirmationMessage, previewAndConfirmAiAction } from '@/utils/aiActionEngine'
import {
  AI_PANEL_DEFAULT_WIDTH,
  buildAiContextKey,
  clampAiPanelSize,
  isAiPanelResizable,
  isAiContextMatch,
  isComposerSendKey,
  readAiPanelSize,
  writeAiPanelSize,
  writeActionsOnly
} from '@/utils/aiWorkspace'

const voiceCallStore = useVoiceCallStore()
const miniLocalVideoRef = ref(null)
const miniRemoteVideoRefs = new Map()

const bindMiniLocalVideo = (el) => {
  if (!el) return
  if (voiceCallStore.isCameraEnabled && voiceCallStore.callSession) {
    const stream = voiceCallStore.callSession.getLocalCameraStream?.() || voiceCallStore.callSession.getLocalStream?.()
    if (stream && stream.getVideoTracks?.().some(t => t.readyState === 'live' && t.enabled !== false)) {
      if (el.srcObject !== stream) {
        el.srcObject = stream
        el.play().catch(() => {})
      }
      return
    }
  }
  el.srcObject = null
}

const setMiniLocalVideoRef = (el) => {
  miniLocalVideoRef.value = el
  if (el) {
    bindMiniLocalVideo(el)
  }
}

const bindMiniRemoteVideo = (el, connId) => {
  if (!el) return
  const media = voiceCallStore.remoteVideoStreams.get(connId)
  if (media?.cameraStream && media.cameraStream.getVideoTracks().some(t => t.readyState === 'live')) {
    if (el.srcObject !== media.cameraStream) {
      el.srcObject = media.cameraStream
      el.play().catch(() => {})
    }
  } else {
    el.srcObject = null
  }
}

const setMiniRemoteVideoRef = (el, connId) => {
  if (el) {
    miniRemoteVideoRefs.set(connId, el)
    bindMiniRemoteVideo(el, connId)
  } else {
    const prevEl = miniRemoteVideoRefs.get(connId)
    if (prevEl) prevEl.srcObject = null
    miniRemoteVideoRefs.delete(connId)
  }
}

watch([() => voiceCallStore.isCameraEnabled, () => voiceCallStore.callSession, () => voiceCallStore.hasLocalCameraTrack], async () => {
  await nextTick()
  if (miniLocalVideoRef.value) {
    bindMiniLocalVideo(miniLocalVideoRef.value)
  }
}, { immediate: true })

watch(() => voiceCallStore.remoteVideoStreams, async (remoteMap) => {
  await nextTick()
  if (!remoteMap) return
  for (const [connId, media] of remoteMap.entries()) {
    const videoEl = miniRemoteVideoRefs.get(connId)
    if (videoEl) {
      bindMiniRemoteVideo(videoEl, connId)
    }
  }
}, { deep: true, immediate: true })

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

const props = defineProps({
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
const siteStore = useSiteStore()
const goalStore = useGoalStore()
const sprintStore = useSprintStore()
const stickyStore = useStickyStore()
const sidebarVisible = ref(window.innerWidth > 1024)
const aiPetStore = useAiPetStore()
const aiScopeStore = useAiScopeStore()
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
const aiCreditsModalVisible = ref(false)
const isMobile = ref(window.innerWidth <= 1024)
const aiInput = ref('')
const aiSending = ref(false)
const aiUsage = ref(null)
const aiContentRef = ref(null)
const aiComposerRef = ref(null)
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
const aiAttachmentInputRef = ref(null)
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

const attachmentStatusLabel = (status) => ({
  uploading: 'Đang tải lên',
  processing: 'Đang xử lý',
  error: 'Tải lên thất bại',
  ready: 'Đã xử lý'
}[String(status || 'pending').toLowerCase()] || 'Chờ tải lên')

const attachmentStatusIcon = (status) => ({
  uploading: 'fa-solid fa-arrow-up-from-bracket fa-bounce',
  processing: 'fa-solid fa-spinner fa-spin',
  error: 'fa-solid fa-circle-exclamation',
  ready: 'fa-solid fa-circle-check'
}[String(status || 'pending').toLowerCase()] || 'fa-regular fa-clock')

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
  if (command === 'browse') aiAttachmentInputRef.value?.click()
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
  aiComposerRef.value?.focusInput?.()
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
    currentPagePrompt: 'Tom tat trang hien tai',
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
      { label: 'Tóm tắt trang', icon: 'fa-regular fa-file-lines', text: 'Tom tat trang hien tai va neu 3 diem can chu y.' },
      { label: 'Gợi ý ưu tiên', icon: 'fa-solid fa-arrow-up-wide-short', text: 'Goi y viec nen lam tiep theo dua tren ngu canh hien tai.' },
      { label: 'Tạo checklist', icon: 'fa-solid fa-list-check', text: 'Tao checklist ngan gon de hoan thanh cong viec nay.' },
      { label: 'Viết cập nhật', icon: 'fa-solid fa-pen-nib', text: 'Soan ban cap nhat tien do ngan gon cho team.' }
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
  'work-items': ['Tom tat tinh hinh du an nay', 'Cong viec nao dang tre han?', 'Ai dang bi qua tai?', 'Goi y uu tien hom nay', 'Giai thich cac cot Kanban hien tai'],
  reports: ['Bao cao nay dang noi dieu gi?', 'Rui ro lon nhat cua du an la gi?', 'Nen xu ly van de nao truoc?'],
  settings: ['Giai thich quyen cua toi trong du an nay', 'Workflow hien tai co hop ly khong?', 'Custom Fields nay dung de lam gi?'],
  goals: ['Tom tat tien do muc tieu', 'Muc tieu nao dang co nguy co?', 'De xuat viec can lam de tang tien do'],
  integration: ['Tom tat cac item moi', 'Item nao nen chuyen thanh cong viec?', 'Co noi dung nao can xu ly gap?'],
  inbox: ['Tom tat cac item moi', 'Item nao nen chuyen thanh cong viec?', 'Co noi dung nao can xu ly gap?'],
  dashboard: ['Tom tat dashboard hien tai', 'Rui ro nao can xu ly truoc?', 'Goi y uu tien hom nay'],
  unknown: ['Toi co the giup gi cho ban trong SprintA?', 'Tom tat trang hien tai', 'Giai thich doan da chon']
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
const quickPrompts = computed(() => AI_QUICK_ACTIONS.slice(0, 4).map(action => ({
  label: action.label,
  text: action.prompt,
  icon: action.icon
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

const handleAiWorkspaceChanged = event => {
  const workspaceId = event?.detail?.workspaceId
  if (workspaceId) aiScopeStore.setWorkspace(workspaceId)
  aiContextRevision.value += 1
  aiInput.value = ''
  if (currentConversationId.value) startNewConversation()
  if (aiVisible.value) void loadConversations(true)
}

const ensureConversation = async (firstMessage) => {
  return aiConversationStore.ensureConversation({ workspaceId: currentWorkspaceId.value, firstMessage })
}

const releaseMessageAttachmentUrls = () => {
  chatHistory.value.forEach(message => message.attachments?.forEach((attachment) => {
    if (attachment.previewUrl?.startsWith('blob:')) URL.revokeObjectURL(attachment.previewUrl)
    attachment.previewUrl = ''
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

const openAiCreditPurchase = () => {
  aiVisible.value = false
  aiCreditsModalVisible.value = true
}

const resizeAiComposer = () => {
  aiComposerRef.value?.resetTextarea?.()
}

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
  window.addEventListener('sprinta-workspace-changed', handleAiWorkspaceChanged)
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
  window.removeEventListener('sprinta-workspace-changed', handleAiWorkspaceChanged)
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
const runQuickPrompt = (prompt) => {
  aiInput.value = prompt
  void sendAiMessage()
}

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
const hasReadOnlyActions = actions => (actions || []).some(action => isReadOnlyAction(action?.type, action?.requiresConfirmation))

const escapeHtml = (value = '') => `${value}`
  .replace(/&/g, '&amp;')
  .replace(/</g, '&lt;')
  .replace(/>/g, '&gt;')
  .replace(/"/g, '&quot;')
  .replace(/'/g, '&#039;')

const renderMarkdown = (value = '') => {
  const source = `${value || ''}`.replace(/\r\n/g, '\n').trim()
  if (!source) return ''
  const codeBlocks = []
  let safe = escapeHtml(source).replace(/```([\w-]*)\n?([\s\S]*?)```/g, (_, language, code) => {
    const index = codeBlocks.push(`<pre><code class="language-${language || 'text'}">${code.trim()}</code></pre>`) - 1
    return `@@CODE_BLOCK_${index}@@`
  })
  safe = safe
    .replace(/^### (.+)$/gm, '<h4>$1</h4>')
    .replace(/^## (.+)$/gm, '<h3>$1</h3>')
    .replace(/^# (.+)$/gm, '<h2>$1</h2>')
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/__(.+?)__/g, '<strong>$1</strong>')
    .replace(/\*([^*\n]+)\*/g, '<em>$1</em>')
    .replace(/`([^`\n]+)`/g, '<code>$1</code>')
    .replace(/^\s*[-*] (.+)$/gm, '<li>$1</li>')
    .replace(/(<li>.*<\/li>\n?)+/g, '<ul>$&</ul>')
    .replace(/^(\d+)\. (.+)$/gm, '<li><span class="md-list-index">$1.</span> $2</li>')
    .replace(/\n{2,}/g, '</p><p>')
    .replace(/\n/g, '<br>')
    .replace(/@@CODE_BLOCK_(\d+)@@/g, (_, index) => codeBlocks[Number(index)])
  return DOMPurify.sanitize(`<p>${safe}</p>`, { USE_PROFILES: { html: true } })
}

const actionLabel = (type = '') => ({
  create_project: 'Tạo project mới',
  create_task: 'Tạo task mới',
  create_cycle: 'Tạo chu kỳ mới',
  create_module: 'Tạo mô-đun mới',
  create_page: 'Tạo tài liệu mới',
  create_view: 'Tạo bộ lọc đã lưu',
  create_intake_request: 'Tạo yêu cầu mới',
  update_task_status: 'Cập nhật trạng thái task',
  update_task_priority: 'Cập nhật độ ưu tiên',
  update_task_due_date: 'Cập nhật hạn task',
  assign_task: 'Giao task cho thành viên',
  add_comment: 'Thêm bình luận',
  create_goal: 'Tạo mục tiêu mới',
  summarize_dashboard: 'Tóm tắt dashboard',
  summarize_project: 'Tóm tắt dự án',
  list_overdue_tasks: 'Liệt kê task quá hạn',
  get_workload: 'Xem tải công việc',
  explain_report: 'Giải thích báo cáo',
  summarize_page: 'Tóm tắt tài liệu',
  summarize_intakes: 'Tóm tắt hàng chờ yêu cầu',
  suggest_view_filter: 'Gợi ý bộ lọc'
}[String(type).toLowerCase()] || 'Thực hiện thay đổi')

const actionStatusLabel = (action) => ({
  pending: 'Chờ xác nhận',
  loading: 'Đang xử lý',
  success: 'Thành công',
  cancelled: 'Đã hủy',
  error: 'Thất bại'
}[action.uiStatus || 'pending'] || 'Chờ xác nhận')

const actionPayload = aiActionPayload
const payloadValue = (action, ...keys) => {
  const payload = actionPayload(action)
  const key = keys.find(item => payload[item] !== undefined && payload[item] !== null && `${payload[item]}`.trim() !== '')
  return key ? payload[key] : ''
}

const resolveProjectLabel = (action) => {
  const projectId = payloadValue(action, 'projectId')
  const current = projectStore.currentProject
  if (current && (!projectId || current.id === projectId || current.Id === projectId)) {
    return current.name || current.Name || 'Dự án hiện tại'
  }
  const projects = projectStore.projects || projectStore.allProjects || []
  const project = projects.find(item => item?.id === projectId || item?.Id === projectId)
  return project?.name || project?.Name || 'Dự án hiện tại'
}

const actionSummary = (action) => {
  const type = String(action?.type || '').toLowerCase()
  if (type === 'create_project') return `Tạo project “${payloadValue(action, 'name', 'projectName') || 'Chưa đặt tên'}”.`
  if (type === 'create_task') return `Tạo task “${payloadValue(action, 'title', 'taskTitle') || 'Chưa đặt tên'}”.`
  if (type === 'create_goal') return `Tạo mục tiêu “${payloadValue(action, 'title', 'name') || 'Chưa đặt tên'}”.`
  if (type === 'update_task_status') return `Chuyển task sang “${payloadValue(action, 'statusName', 'status') || 'trạng thái mới'}”.`
  if (type === 'assign_task') return 'Giao task cho thành viên được chỉ định.'
  if (isReadOnlyAction(type)) return 'Đọc dữ liệu hiện tại để trả về một tóm tắt có căn cứ.'
  return 'AI đề xuất một thay đổi cần bạn xác nhận.'
}

const actionDetails = (action) => {
  const type = String(action?.type || '').toLowerCase()
  const details = []
  const add = (label, value) => { if (value !== '' && value !== null && value !== undefined) details.push({ label, value: `${value}` }) }
  if (type === 'create_project') {
    add('Tên project', payloadValue(action, 'name', 'projectName'))
    add('Mô tả', payloadValue(action, 'description'))
  } else if (type === 'create_task') {
    add('Tiêu đề', payloadValue(action, 'title', 'taskTitle'))
    add('Hạn', payloadValue(action, 'dueDate', 'plannedEndDate'))
    add('Ưu tiên', payloadValue(action, 'priority'))
  } else if (type === 'create_goal') {
    add('Tên mục tiêu', payloadValue(action, 'title', 'name'))
    add('Mô tả', payloadValue(action, 'description'))
  } else if (type === 'update_task_status') {
    add('Task', payloadValue(action, 'taskTitle', 'title'))
    add('Trạng thái mới', payloadValue(action, 'statusName', 'status'))
  } else if (type === 'assign_task') {
    add('Task', payloadValue(action, 'taskTitle', 'title'))
    add('Người nhận', payloadValue(action, 'assigneeName', 'assigneeEmail', 'assignee'))
  } else if (['create_cycle', 'create_module', 'create_page', 'create_view', 'create_intake_request'].includes(type)) {
    add('Tên', payloadValue(action, 'name', 'title'))
    add('Dự án', payloadValue(action, 'projectName') || resolveProjectLabel(action))
    add('Bắt đầu', payloadValue(action, 'startDate'))
    add('Kết thúc', payloadValue(action, 'endDate'))
  } else if (['update_task_priority', 'update_task_due_date'].includes(type)) {
    add('Task', payloadValue(action, 'taskTitle', 'title'))
    add(type === 'update_task_priority' ? 'Độ ưu tiên mới' : 'Hạn mới', payloadValue(action, type === 'update_task_priority' ? 'priority' : 'dueDate'))
  } else if (type === 'add_comment') {
    add('Đối tượng', payloadValue(action, 'entityType'))
    add('Nội dung', payloadValue(action, 'content'))
  }
  return details
}

const cancelAiAction = async (action) => {
  if (action.loading || action.uiStatus === 'success') return
  if (action.serverActionId) {
    await axiosClient.post(`/ai/actions/${action.serverActionId}/cancel`)
  }
  action.uiStatus = 'cancelled'
  action.status = 'CANCELLED'
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
const executeAiAction = async (action, { navigate = true } = {}) => {
  if (!action || action.loading || action.uiStatus === 'success' || action.uiStatus === 'cancelled') return
  if (!isAiContextMatch(action.contextKey, currentWorkspaceId.value, currentProjectId.value)) {
    action.uiStatus = 'error'
    action.error = 'Ngữ cảnh AI đã thay đổi. Hãy gửi lại yêu cầu trong project hiện tại.'
    await persistConversation()
    return
  }
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
      action.status = 'EXECUTED'
      ElMessage.success(result?.message || 'Đã tải dữ liệu thành công.')
      return
    }
    const response = await previewAndConfirmAiAction(action, {
      workspaceId: currentWorkspaceId.value,
      projectId: currentProjectId.value || actionPayload(action).projectId,
      conversationId: currentConversationId.value
    })
    await persistConversation()
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
    action.status = 'EXECUTED'
    const navigation = await refreshAfterAiAction(action, result)
    ElMessage.success(result?.message || 'AI đã thực hiện thay đổi thành công.')
    if (navigate) await navigateToAiEntity(navigation)
  } catch (error) {
    const duplicateCandidate = error.response?.data?.data?.existingTask
    if (error.response?.status === 409 && duplicateCandidate) {
      action.duplicateCandidate = duplicateCandidate
      action.uiStatus = 'pending'
      await persistConversation()
      return
    }
    action.uiStatus = 'error'
    action.status = 'FAILED'
        const status = error.response?.status
    const mapped = { 400: 'Dữ liệu action không hợp lệ.', 401: 'Phiên đăng nhập đã hết hạn.', 403: 'Bạn không có quyền thực hiện action này.', 404: 'Không tìm thấy entity cần thao tác.', 409: 'Action bị trùng hoặc xung đột dữ liệu.', 422: 'Dữ liệu không vượt qua kiểm tra nghiệp vụ.', 429: 'AI đang quá tải. Hãy thử lại sau.', 503: 'Dịch vụ AI tạm thời không khả dụng.' }
    action.error = mapped[status] || error.response?.data?.message || error.message || 'Không thể thực hiện action.'
    ElMessage.error(action.error)
  } finally {
    action.loading = false
    await persistConversation()
  }
}

const currentProjectId = computed(() => {
  if (aiScopeStore.projectId) {
    const scopedProject = projectStore.allProjects.find(item => `${item.id || item.Id}` === `${aiScopeStore.projectId}`)
    const scopedWorkspaceId = scopedProject?.workspaceId || scopedProject?.WorkspaceId
    if (!scopedWorkspaceId || !aiScopeStore.workspaceId || `${scopedWorkspaceId}` === `${aiScopeStore.workspaceId}`) return aiScopeStore.projectId
    return null
  }
  const routeId = route.params?.id
  if (typeof routeId === 'string' && routeId.length >= 30) return routeId
  return projectStore.currentProject?.id || projectStore.currentProject?.Id || workTaskStore.currentProjectId || null
})

const currentWorkspaceId = computed(() => {
  if (aiScopeStore.workspaceId) return aiScopeStore.workspaceId
  const routeWorkspaceId = route.params?.workspaceId || route.params?.spaceId
  if (typeof routeWorkspaceId === 'string' && routeWorkspaceId.length >= 30) return routeWorkspaceId
  const project = projectStore.allProjects.find(item => `${item.id || item.Id}` === `${currentProjectId.value || ''}`)
    || projectStore.currentProject
  return project?.workspaceId || project?.WorkspaceId || workTaskStore.resolveWorkspaceId(currentProjectId.value) || null
})

const aiContextKey = computed(() => buildAiContextKey(currentWorkspaceId.value, currentProjectId.value))
const aiContextRevision = ref(0)

const currentProjectLabel = computed(() => {
  const project = projectStore.allProjects.find(item => `${item.id || item.Id}` === `${currentProjectId.value || ''}`)
    || projectStore.currentProject
  return project?.name || project?.Name || (currentProjectId.value ? 'Project hiện tại' : 'Chưa chọn project')
})

const currentWorkspaceLabel = computed(() => {
  const workspaceId = currentWorkspaceId.value
  const workspace = siteStore.sites.find(item => `${item.id || item.Id}` === `${workspaceId || ''}`)
  return workspace?.name || workspace?.Name || (workspaceId ? 'Workspace hiện tại' : 'Chưa chọn workspace')
})

const aiContactContext = computed(() => ({
  contactName: storedProfile.value.fullName || storedProfile.value.FullName || storedProfile.value.username || storedProfile.value.email || '',
  workEmail: storedProfile.value.email || '',
  workspaceId: currentWorkspaceId.value || '',
  workspaceName: currentWorkspaceLabel.value,
  projectName: currentProjectLabel.value
}))

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
  nextTick(() => aiComposerRef.value?.focusInput?.())
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

watch([currentWorkspaceId, currentProjectId], async ([workspaceId, projectId], previous = []) => {
  const [previousWorkspaceId, previousProjectId] = previous
  if (workspaceId === previousWorkspaceId && projectId === previousProjectId) return

  aiContextRevision.value += 1
  if (currentConversationId.value) {
    startNewConversation()
  }
  if (aiVisible.value) {
    await loadConversations(true)
  }
}, { flush: 'post' })

const currentTasks = computed(() => Array.isArray(workTaskStore.tasks) ? workTaskStore.tasks : [])

const ensureProjectTasks = async () => {
  const projectId = currentProjectId.value
  if (!projectId) return []
  if (workTaskStore.currentProjectId !== projectId || !currentTasks.value.length) {
    await workTaskStore.fetchTasks(projectId)
  }
  return currentTasks.value
}

const createSuggestedTask = async (task) => {
  if (!canCreateTaskInProject.value) {
    ElMessage.error("Bạn không có quyền tạo công việc trong dự án này.")
    return
  }

  task.loading = true
  try {
    const action = {
      type: 'create_task',
      contextKey: aiContextKey.value,
      payload: {
        projectId: currentProjectId.value,
        title: task.title,
        description: task.description || 'Được tạo từ gợi ý của SprintA AI',
        priority: task.priority || 3,
        dueDate: task.dueDate || null,
        typeName: 'Task',
        storyPoints: 0
      },
      uiStatus: 'pending',
      loading: false,
      error: '',
      result: null,
      duplicateCandidate: null
    }
    await executeAiAction(action, { navigate: false })
    if (action.uiStatus === 'success') {
      task.created = true
      task.createdTask = action.result?.task || action.result
      ElMessage.success(`Đã tạo thành công task: "${task.createdTask?.title || task.createdTask?.Title || task.title}"`)
    } else if (action.duplicateCandidate) {
      task.error = 'Đã tìm thấy task tương tự. Không tạo task mới.'
    }
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

    try {
      const guardedAction = {
        type: 'update_task_status',
        contextKey: aiContextKey.value,
        payload: {
          projectId: currentProjectId.value,
          taskId: action.taskId,
          statusName: action.statusName
        },
        uiStatus: 'pending',
        loading: false,
        error: '',
        result: null,
        duplicateCandidate: null
      }
      await executeAiAction(guardedAction, { navigate: false })
      if (guardedAction.uiStatus !== 'success') return
      action.completed = true
      ElMessage.success(`Đã chuyển task "${action.taskTitle}" sang trạng thái ${action.statusName}.`)
    } catch (e) {
      ElMessage.error(e.response?.data?.message || "Không thể chuyển trạng thái task.")
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
      attachment.errorMessage = error.response?.data?.message || error.response?.data?.error || error.message || 'Không thể xử lý attachment.'
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

  if (!hasAttachments && isAiConfirmationMessage(outgoing)) {
    const pendingAction = findPendingAiAction(chatHistory.value, {
      contextKey: aiContextKey.value,
      conversationId: currentConversationId.value,
      workspaceId: currentWorkspaceId.value,
      projectId: currentProjectId.value
    })
    if (pendingAction) {
      aiInput.value = ''
      await executeAiAction(pendingAction)
      return
    }
  }

  aiSending.value = true
  const requestRevision = aiContextRevision.value
  let loadingAdded = false
  let userMessageAdded = false

  try {
    const titleSeed = outgoing || pendingAttachments.value.map(item => item.name).join(', ')
    const conversationId = await ensureConversation(titleSeed)
    const uploadedAttachments = hasAttachments ? await uploadPendingAttachments(conversationId) : []
    if (requestRevision !== aiContextRevision.value) return

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
      if (requestRevision !== aiContextRevision.value) return
      const responseData = apiPayload(response)
      chatHistory.value.pop()
      loadingAdded = false
      const normalizedActions = normalizeAiActionList(responseData?.actions || [])
      chatHistory.value.push({
        role: 'bot',
        content: responseData?.answer || aiCopy.value.emptyResponse,
        citations: responseData?.citations || [],
        actions: normalizedActions.actions.map(action => decorateAiAction(action, {
          contextKey: aiContextKey.value,
          conversationId,
          workspaceId: currentConversationWorkspaceId.value || currentWorkspaceId.value,
          projectId: currentProjectId.value || actionPayload(action).projectId
        }))
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
    if (requestRevision !== aiContextRevision.value) return
    const responseData = apiPayload(response)

    chatHistory.value.pop()
    loadingAdded = false
    
    const normalizedActions = normalizeAiActionList(responseData?.actions || [])
    chatHistory.value.push({
      role: 'bot',
      content: [responseData?.answer || aiCopy.value.emptyResponse, normalizedActions.hasMissingTaskTitle ? 'Bạn muốn đặt tên công việc là gì?' : ''].filter(Boolean).join('\n\n'),
      suggestedPrompts: responseData?.suggestions || [],
      warnings: responseData?.warnings || [],
      actions: normalizedActions.actions.map(action => ({
        ...decorateAiAction(action, {
          contextKey: aiContextKey.value,
          conversationId,
          workspaceId: currentWorkspaceId.value,
          projectId: currentProjectId.value || actionPayload(action).projectId
        }),
        duplicateCandidate: null
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

const handleSiteCreated = (newSite) => {
  if (newSite && newSite.id) {
    window.location.href = buildSpacePath(newSite, 'work-items')
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
  outline: 3px solid color-mix(in srgb, var(--sa-primary) 55%, #ffffff);
  outline-offset: 3px;
}

.ai-mobile-backdrop {
  position: fixed;
  inset: var(--sa-topbar-height, 52px) 0 0;
  z-index: 1490;
  background: rgba(2, 6, 23, 0.48);
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

.ai-activity-note i { color: var(--color-success, #16803c); }

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

.ai-action-status.is-success { color: #16803c; }
.ai-action-status.is-error { color: #c2410c; }
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
.ai-action-error { color: #dc2626; }
.ai-action-result { color: #16803c; }

.ai-duplicate-warning {
  padding: 10px;
  border: 1px solid #d97706;
  border-radius: 8px;
  background: var(--color-warning-bg);
  color: #7c2d12;
}
.ai-duplicate-warning p { margin: 4px 0 8px; overflow-wrap: anywhere; }
.ai-duplicate-actions { display: flex; flex-wrap: wrap; gap: 6px; }
.ai-duplicate-actions button {
  min-height: 30px;
  padding: 6px 9px;
  border: 1px solid #d97706;
  border-radius: 6px;
  background: var(--color-surface);
  color: #7c2d12;
  cursor: pointer;
}
.ai-duplicate-actions .is-danger { background: #9a3412; color: #fff; }

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
.ai-action-confirm { border: 1px solid var(--sa-primary); background: var(--sa-primary); color: #fff; }

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

.ai-attachment-meta small.is-ready { color: #16803c; }
.ai-attachment-meta small.is-error { color: #dc2626; }
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
  color: var(--color-danger, #dc2626);
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

.ai-voice-error { color: var(--color-danger, #dc2626); }

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
.ai-voice-primary { border-color: var(--color-accent) !important; background: var(--color-accent); color: #ffffff; }
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
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.04);
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
  color: #ffffff;
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

.ai-credit-card.is-low { border-color: #d9a441; }
.ai-credit-card.is-low .ai-credit-progress > span { background: #d9a441; }
.ai-credit-card.is-empty { border-color: #d25b5b; }
.ai-credit-card.is-empty .ai-credit-progress > span { width: 0 !important; background: #d25b5b; }

/* Shared floating panel polish. Keep it visually related to the full AI page
   while preserving the existing conversation, action and upload contracts. */
.ai-sidebar {
  border-radius: 22px;
  border-color: color-mix(in srgb, #0b8fd3 28%, var(--color-border));
  background: radial-gradient(circle at 100% 0, color-mix(in srgb, #0b8fd3 13%, transparent), transparent 32%), var(--color-surface);
  box-shadow: 0 28px 80px color-mix(in srgb, var(--color-text-primary) 30%, transparent), 0 0 0 1px color-mix(in srgb, var(--color-text-inverse) 8%, transparent) inset;
}
.ai-hero {
  padding: 22px 22px 18px;
  border-bottom-color: color-mix(in srgb, #0b8fd3 20%, var(--color-border));
  background: linear-gradient(145deg, color-mix(in srgb, #0b8fd3 14%, var(--color-surface)), var(--color-surface) 72%);
}
.ai-brand-icon { width: 44px; height: 44px; border-radius: 14px; background: color-mix(in srgb, #0b8fd3 15%, var(--color-surface)); border-color: color-mix(in srgb, #0b8fd3 32%, var(--color-border)); }
.ai-brand h4 { font-size: 18px; letter-spacing: -.03em; }
.ai-hero-copy { margin-top: 14px; line-height: 1.6; }
.ai-credit-card { margin-top: 16px; padding: 15px; border-radius: 16px; border-color: color-mix(in srgb, #0b8fd3 38%, var(--color-border)); background: linear-gradient(145deg, color-mix(in srgb, #0b8fd3 18%, var(--color-surface-hover)), var(--color-surface-hover) 76%); box-shadow: 0 12px 26px color-mix(in srgb, #0b8fd3 13%, transparent); }
.ai-credit-label { color: #0b8fd3; font-size: 10px; letter-spacing: .1em; }
.ai-credit-progress { height: 7px; margin-top: 12px; }
.ai-credit-progress > span { background: linear-gradient(90deg, #0b8fd3, #41c0f2); }
.ai-credit-buy { min-height: 34px; border-radius: 10px; }
.ai-conversation-toolbar { margin-top: 14px; grid-template-columns: 36px 36px minmax(0, 1fr); gap: 7px; }
.ai-conversation-toolbar button { width: 36px; height: 36px; border-radius: 10px; }
.ai-conversation-toolbar span { padding-inline: 4px; font-size: 11px; }
.ai-content { padding: 18px 20px 22px; background: color-mix(in srgb, var(--color-bg) 74%, var(--color-surface)); }
.quick-actions { gap: 9px; margin-top: 0; margin-bottom: 15px; }
.quick-action { flex: 1 1 calc(50% - 5px); justify-content: flex-start; min-height: 42px; padding: 8px 10px; border-radius: 12px; border-color: color-mix(in srgb, #0b8fd3 21%, var(--color-border)); background: color-mix(in srgb, var(--color-surface) 84%, #0b8fd3); font-size: 11px; line-height: 1.3; }
.quick-action i { width: 17px; color: #0b8fd3; text-align: center; }
.quick-action:hover { transform: translateY(-1px); box-shadow: 0 8px 18px color-mix(in srgb, #0b8fd3 13%, transparent); }
.ai-context-card { margin-bottom: 18px; padding: 14px; border-radius: 14px; border-color: color-mix(in srgb, #0b8fd3 24%, var(--color-border)); background: color-mix(in srgb, #0b8fd3 8%, var(--color-surface)); }
.ai-context-card button { width: 36px; height: 36px; border-radius: 10px; }
.ai-content :deep(.ai-composer) { margin-top: 0; }
.ai-sidebar > :deep(.ai-composer) { margin: 0 16px 16px; width: auto; flex: 0 0 auto; }
.ai-sidebar > :deep(.ai-composer) .ai-input-foot { padding-inline: 3px; }
.global-utility-rail.is-ai-open { right: calc(16px + min(var(--ai-sidebar-width, 456px), 70vw) + 18px); border-color: color-mix(in srgb, #0b8fd3 30%, var(--color-border)); box-shadow: 0 14px 30px color-mix(in srgb, var(--color-text-primary) 18%, transparent); }

@media (max-width: 760px) {
  .ai-sidebar { border-radius: 20px 20px 0 0; }
  .ai-hero { padding: 18px 16px 15px; }
  .ai-content { padding: 15px 14px 18px; }
  .ai-sidebar > :deep(.ai-composer) { margin: 0 12px calc(12px + env(safe-area-inset-bottom)); }
}

/* Focused floating-panel pass: reserve the panel's viewport for interaction;
   passive metadata stays compact and the content owns the scroll. */
.ai-sidebar {
  min-height: 0;
}

.ai-hero {
  flex: 0 0 auto;
  padding: 14px 16px 12px;
}

.ai-brand {
  gap: 9px;
}

.ai-brand-icon {
  width: 36px;
  height: 36px;
  border-radius: 11px;
}

.ai-brand-icon img {
  width: 29px;
  height: 29px;
}

.ai-brand p {
  font-size: 10px;
}

.ai-brand h4 {
  font-size: 16px;
}

.ai-hero-copy {
  max-width: 54ch;
  margin-top: 8px;
  font-size: 12px;
  line-height: 1.45;
}

.ai-open-full-chat,
.close-ai {
  width: 30px;
  height: 30px;
  border-radius: 8px;
}

.ai-credit-card {
  margin-top: 9px;
  padding: 9px 10px;
  border-radius: 11px;
  box-shadow: none;
}

.ai-credit-head {
  gap: 8px;
}

.ai-credit-head > div {
  gap: 6px;
}

.ai-credit-label,
.ai-credit-head strong,
.ai-credit-message {
  font-size: 10px;
}

.ai-credit-progress {
  height: 5px;
  margin-top: 7px;
}

.ai-credit-message {
  margin-top: 6px;
  line-height: 1.3;
}

.ai-credit-buy {
  min-height: 28px;
  margin-top: 6px;
  padding: 0 9px;
  border-radius: 8px;
  font-size: 10px;
}

.ai-pin-toggle {
  min-height: 26px;
  margin-top: 7px;
  padding: 4px 8px;
  font-size: 10px;
}

.ai-conversation-toolbar {
  grid-template-columns: 30px 30px minmax(0, 1fr);
  gap: 6px;
  margin-top: 7px;
}

.ai-conversation-toolbar button {
  width: 30px;
  height: 30px;
  border-radius: 8px;
}

.ai-conversation-toolbar span {
  font-size: 10px;
}

.ai-content {
  min-height: 0;
  overflow-y: auto;
}

.ai-context-card small {
  display: block;
  margin-top: 4px;
  overflow: hidden;
  color: var(--color-text-muted);
  font-size: 10px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ai-context-card > div {
  min-width: 0;
}
.ai-context-eyebrow,
.ai-page-context > span {
  margin-top: 0 !important;
  color: var(--color-accent) !important;
  font-size: 9px !important;
  font-weight: 850;
  letter-spacing: .1em;
  text-transform: uppercase;
}
.ai-page-context {
  display: grid;
  gap: 3px;
  min-width: 0;
  margin-top: 9px;
  padding-top: 8px;
  border-top: 1px solid color-mix(in srgb, var(--color-border) 80%, transparent);
}
.ai-page-context strong {
  overflow: hidden;
  color: var(--color-text-secondary);
  font-size: 11px;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ai-sidebar,
.ai-hero,
.ai-content,
.ai-history-panel,
.ai-content :deep(.ai-composer) {
  min-width: 0;
}
.ai-content {
  overflow-x: hidden;
}

@media (max-width: 760px) {
  .ai-hero {
    padding: 13px 14px 11px;
  }
}

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

/* Floating Mini Video Grid Overlay (Google Meet Style) */
.persistent-call-overlay.has-mini-video {
  flex-direction: column;
  align-items: stretch;
  padding: 12px;
  max-width: 320px;
}
.call-overlay-video-dock {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 8px;
  max-height: 140px;
  overflow: hidden;
  border-radius: 10px;
  cursor: pointer;
}
.mini-video-tile {
  position: relative;
  flex: 1 1 120px;
  height: 90px;
  min-width: 100px;
  border-radius: 8px;
  overflow: hidden;
  background: #000000;
  border: 1px solid rgba(255, 255, 255, 0.12);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
}
.mini-video-el {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}
.mini-video-tile.self-tile .mini-video-el {
  transform: scaleX(-1);
}
.mini-video-label {
  position: absolute;
  bottom: 4px;
  left: 6px;
  background: rgba(15, 23, 42, 0.75);
  backdrop-filter: blur(4px);
  color: #ffffff;
  font-size: 10px;
  font-weight: 600;
  padding: 2px 6px;
  border-radius: 4px;
  max-width: calc(100% - 12px);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
