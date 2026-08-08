<template>
  <div class="chat-container">
    <!-- Project scope sidebar for real collaboration channels -->
    <div class="server-bar" v-if="currentTab === 'channel'">
      <div 
        v-for="project in projectOptions"
        :key="project.id"
        class="server-icon-wrapper"
        :class="{ active: activeProjectId === project.id }"
        @click="selectProject(project.id)"
        :title="project.name"
      >
        <div class="server-icon">
          {{ project.name.charAt(0).toUpperCase() }}
        </div>
        <div class="active-indicator"></div>
      </div>
      
    </div>

    <!-- Chat Sidebar (Channels & Direct Messages) -->
    <div class="chat-sidebar">

      <div class="sidebar-header" style="display: flex; flex-direction: column; gap: 10px; padding-bottom: 12px; border-bottom: 1px solid var(--color-border); margin-bottom: 14px;">
        <div class="flex items-center justify-between" style="display: flex; align-items: center; justify-content: space-between; width: 100%;">
          <h3 class="font-bold truncate" style="display: flex; align-items: center; gap: 8px; flex: 1; min-width: 0; margin: 0;">
            <i class="fa-solid fa-comments text-primary text-lg" style="margin-right: 4px;"></i>
            <span>{{ t('Discussion Channel') }}</span>
          </h3>
        </div>
        
        <!-- Toggle Tabs -->
        <div class="tab-switcher">
          <button 
            @click="switchTab('channel')" 
            class="tab-btn" 
            :class="{ active: currentTab === 'channel' }"
          >
            <i class="fa-solid fa-server"></i>
            <span>{{ t('Group Chat') }}</span>
          </button>
          <button 
            @click="switchTab('dm')" 
            class="tab-btn" 
            :class="{ active: currentTab === 'dm' }"
          >
            <i class="fa-solid fa-message"></i>
            <span>{{ t('Direct Chat') }}</span>
          </button>
        </div>
      </div>

      <div class="sidebar-header">
        <h3 class="font-bold truncate" style="display: flex; align-items: center; gap: 8px; flex: 1; min-width: 0; margin: 0;">
          <i class="fa-solid fa-diagram-project text-primary text-base" v-if="currentTab === 'channel'"></i>
          <i class="fa-solid fa-comments text-primary text-lg" v-else style="margin-right: 8px;"></i>
          <span>{{ currentTab === 'channel' ? (activeProject?.name || 'Chọn Project') : 'Kênh Thảo Luận' }}</span>
        </h3>

      </div>
      <select
        v-if="currentTab === 'channel'"
        v-model="activeProjectId"
        class="project-scope-select"
        aria-label="Chọn Project cho Channel"
      >
        <option value="">Chọn Project</option>
        <option v-for="project in projectOptions" :key="project.id" :value="project.id">
          {{ project.name }}
        </option>
      </select>

      <!-- Sidebar lists wrap in scrollable container to pin voice panel at bottom -->
      <div class="sidebar-lists-scrollable">
        <!-- Channels List -->
        <div class="sidebar-section" v-if="currentTab === 'channel'">
          <!-- Server Name & Settings -->
          <div class="server-name-header" style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; padding-bottom: 8px; border-bottom: 1px dashed var(--color-border);">
            <span style="font-size: 13px; font-weight: 700; color: var(--color-text-primary);" class="truncate">{{ activeServer?.name }}</span>
            <i class="fa-solid fa-gear text-xs text-muted hover-settings-icon" style="cursor: pointer; transition: color 0.2s;" @click.stop="openServerSettingsModal" title="Cài đặt Server"></i>
          </div>

          <div class="flex items-center justify-between section-header" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
            <span class="section-title" style="margin-bottom: 0;">CHANNELS</span>
            <button
              class="add-btn-small"
              title="Tạo Channel"
              aria-label="Tạo Channel"
              :disabled="!activeProjectId || channelsLoading"
              @click="openCreateChannelModal"
            >
              <i class="fa-solid fa-plus text-xs"></i>
            </button>
          </div>
          <div class="section-list">
            <div v-if="projectsLoading || channelsLoading" class="channel-state" role="status">
              <i class="fa-solid fa-spinner fa-spin"></i>
              <span>Đang tải Channel...</span>
            </div>
            <div v-else-if="projectsError" class="channel-state channel-state-error" role="alert">
              <span>{{ projectsError }}</span>
              <button type="button" class="state-action" @click="retryProjects">Thử lại</button>
            </div>
            <div v-else-if="channelsError" class="channel-state channel-state-error" role="alert">
              <span>{{ channelsError }}</span>
              <button type="button" class="state-action" @click="retryChannels">Thử lại</button>
            </div>
            <div v-else-if="!activeProjectId" class="channel-state">
              Chọn Project để xem Channel.
            </div>
            <div v-else-if="channels.length === 0" class="channel-state">
              Project này chưa có Channel bạn có thể truy cập.
            </div>
            <button 
              v-for="ch in channels" 
              :key="ch.id" 
              class="list-item" 
              :class="{ active: activeChat?.id === ch.id && activeChat?.type === 'channel' }"
              @click="selectChat(ch, 'channel')"
            >
              <span class="item-icon">#</span>
              <span class="item-name truncate">{{ ch.name }}</span>
              <span
                v-if="ch.unreadCount > 0"
                class="collaboration-unread-badge"
                role="status"
                aria-live="polite"
                :aria-label="`${ch.unreadCount} tin nhắn chưa đọc trong Channel ${ch.name}`"
              >{{ formatUnreadCount(ch.unreadCount) }}</span>
            </button>
            <button
              v-if="channels.length < channelPagination.totalCount"
              type="button"
              class="state-action load-more-action"
              :disabled="channelsLoadingMore"
              @click="loadMoreChannels"
            >
              {{ channelsLoadingMore ? 'Đang tải...' : 'Tải thêm Channel' }}
            </button>
          </div>
        </div>

        <!-- Voice Channels List -->
        <div class="sidebar-section mt-4" v-if="currentTab === 'voice'">
          <div class="flex items-center justify-between section-header" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
            <span class="section-title" style="margin-bottom: 0;">KÊNH THOẠI (VOICE)</span>
            <button class="add-btn-small" title="Tạo kênh thoại mới" @click="openCreateVoiceModal">
              <i class="fa-solid fa-plus text-xs"></i>
            </button>
          </div>
          <div class="section-list">
            <div 
              v-for="vc in voiceChannels" 
              :key="vc.id" 
              class="voice-item-wrapper"
            >
              <button 
                class="list-item voice-item w-full text-left" 
                :class="{ active: activeVoiceChannel?.id === vc.id }"
                @click="joinVoiceChannel(vc)"
              >
                <span class="item-icon"><i class="fa-solid fa-volume-high"></i></span>
                <span class="item-name truncate">{{ vc.name }}</span>
              </button>
              <!-- Users in this voice channel -->
              <div class="voice-users-list ml-6 flex flex-col gap-1.5 mt-1" v-if="vc.users.length">
                <div 
                  v-for="user in vc.users" 
                  :key="user.id" 
                  class="voice-user flex items-center gap-2 py-0.5 text-xs text-secondary"
                  style="display: flex; align-items: center; gap: 6px; padding-left: 12px; margin-top: 2px;"
                >
                  <el-avatar :size="16" :src="user.avatar">{{ user.name.charAt(0) }}</el-avatar>
                  <span class="truncate text-xs" style="font-size: 11px; color: var(--color-text-secondary);">{{ user.name }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Direct Messages List -->
        <div class="sidebar-section mt-4" v-if="currentTab === 'dm'">
          <span class="section-title">TIN NHẮN TRỰC TIẾP</span>
          <select
            v-model="activeProjectId"
            class="project-scope-select"
            aria-label="Chọn Project để tìm người nhận"
          >
            <option value="">Chọn Project</option>
            <option v-for="project in projectOptions" :key="project.id" :value="project.id">
              {{ project.name }}
            </option>
          </select>
          <select
            class="project-scope-select"
            :value="selectedRecipientId"
            :disabled="membersLoading || findingConversation || !activeProjectId"
            aria-label="Chọn người nhận Direct Message"
            @change="selectDirectRecipient($event.target.value)"
          >
            <option value="">
              {{ membersLoading ? 'Đang tải thành viên...' : 'Chọn người nhận' }}
            </option>
            <option v-for="member in members" :key="member.id" :value="member.id">
              {{ member.name }}
            </option>
          </select>
          <div class="section-list">
            <div v-if="membersError" class="channel-state channel-state-error" role="alert">
              <span>{{ membersError }}</span>
              <button type="button" class="state-action" aria-label="Thử tải lại thành viên" @click="retryMembers">Thử lại</button>
            </div>
            <div v-if="conversationsLoading" class="channel-state" role="status">
              <i class="fa-solid fa-spinner fa-spin"></i>
              <span>Đang tải cuộc trò chuyện...</span>
            </div>
            <div v-else-if="conversationsError" class="channel-state channel-state-error" role="alert">
              <span>{{ conversationsError }}</span>
              <button type="button" class="state-action" aria-label="Thử tải lại cuộc trò chuyện" @click="retryConversations">Thử lại</button>
            </div>
            <div v-else-if="directConversations.length === 0" class="channel-state">
              Bạn chưa có cuộc trò chuyện nào.
            </div>
            <button 
              v-for="conversation in directConversations"
              :key="conversation.id"
              class="list-item" 
              :class="{ active: activeChat?.id === conversation.id && activeChat?.type === 'dm' }"
              :disabled="findingConversation"
              @click="selectChat(conversation, 'dm')"
            >
              <el-avatar :size="24" :src="conversation.avatar">{{ conversation.name.charAt(0) }}</el-avatar>
              <div class="flex flex-col text-left overflow-hidden ml-2">
                <span class="item-name truncate">{{ conversation.name }}</span>
                <span class="text-xs text-muted truncate">{{ conversation.lastMessagePreview || 'Chưa có tin nhắn' }}</span>
              </div>
              <span class="conversation-time">
                {{ formatTime(conversation.lastMessageAt || conversation.createdAt) }}
              </span>
              <span
                v-if="conversation.unreadCount > 0"
                class="collaboration-unread-badge"
                role="status"
                aria-live="polite"
                :aria-label="`${conversation.unreadCount} tin nhắn chưa đọc từ ${conversation.name}`"
              >{{ formatUnreadCount(conversation.unreadCount) }}</span>
            </button>
            <button
              v-if="directConversations.length < conversationPagination.totalCount"
              type="button"
              class="state-action load-more-action"
              :disabled="conversationsLoadingMore"
              @click="loadMoreConversations"
            >
              {{ conversationsLoadingMore ? 'Đang tải...' : 'Tải thêm cuộc trò chuyện' }}
            </button>
          </div>
        </div>
      </div>

      <!-- Connected Voice Control Panel (Discord style) -->
      <div v-if="currentTab === 'voice' && activeVoiceChannel" class="connected-voice-panel mt-auto">
        <div class="voice-status-info flex items-center justify-between" style="display: flex; justify-content: space-between; align-items: center;">
          <div class="flex items-center gap-2" style="display: flex; align-items: center; gap: 8px;">
            <span class="status-indicator-ping"><i class="fa-solid fa-signal text-success text-xs" style="color: var(--color-success);"></i></span>
            <div class="flex flex-col text-left" style="display: flex; flex-direction: column;">
              <span class="text-xs font-semibold text-success" style="font-size: 12px; color: var(--color-success);">Đã kết nối thoại</span>
              <span class="text-xxs text-muted truncate" style="font-size: 10px; color: var(--color-text-muted); max-width: 130px; display: inline-block;">{{ activeVoiceChannel.name }}</span>
            </div>
          </div>
          <button class="disconnect-btn-round" title="Ngắt kết nối" @click="leaveVoiceChannel">
            <i class="fa-solid fa-phone-slash text-xs"></i>
          </button>
        </div>
        <div class="voice-actions-row flex justify-around mt-2 pt-2 border-t border-slate-700/40" style="display: flex; justify-content: space-around; margin-top: 8px; padding-top: 8px; border-top: 1px solid rgba(255,255,255,0.08);">
          <button 
            class="voice-action-btn-small" 
            :class="{ active: isMuted }" 
            :title="isMuted ? 'Bật micro' : 'Tắt tiếng'"
            @click="isMuted = !isMuted"
          >
            <i :class="isMuted ? 'fa-solid fa-microphone-slash text-danger' : 'fa-solid fa-microphone'"></i>
          </button>
          <button 
            class="voice-action-btn-small" 
            :class="{ active: isCameraOn }" 
            :title="isCameraOn ? 'Tắt camera' : 'Bật camera'"
            @click="isCameraOn = !isCameraOn"
          >
            <i :class="isCameraOn ? 'fa-solid fa-video' : 'fa-solid fa-video-slash'"></i>
          </button>
        </div>
      </div>
    </div>

    <!-- Active Chat Area -->
    <div class="chat-main">
      <div class="chat-header">
        <div class="active-info">
          <span class="active-icon">{{ activeChat?.type === 'channel' ? '#' : '@' }}</span>
          <div>
            <h4 class="font-semibold text-primary leading-tight">{{ activeChat?.name || 'Chưa chọn Channel' }}</h4>
            <p class="text-xs text-muted leading-none">
              {{ activeChat?.type === 'channel' ? activeChat.desc : (activeChat ? 'Tin nhắn được lưu trên máy chủ' : 'Chọn một cuộc trò chuyện') }}
            </p>
          </div>
        </div>

        <div class="header-actions">
          <button class="action-btn" v-if="currentTab === 'dm'" title="Kết bạn & Mời thành viên" @click="openAddFriendModal">
            <i class="fa-solid fa-user-plus text-lg"></i>
          </button>
          <button class="action-btn" v-if="currentTab === 'dm'" title="Gọi thoại" @click="startVoiceCall">
            <i class="fa-solid fa-phone text-lg"></i>
          </button>
          <button class="action-btn" v-if="currentTab === 'dm'" title="Gọi video" @click="startVideoCall">
            <i class="fa-solid fa-video text-lg"></i>
          </button>
          <button class="action-btn" v-if="currentTab === 'dm'" title="Tìm kiếm tin nhắn">
            <i class="fa-solid fa-magnifying-glass text-lg"></i>
          </button>
          <button v-if="currentTab === 'dm'" class="action-btn" title="Tạo nhóm Server" @click="openCreateServerFromDmModal">
            <i class="fa-solid fa-users text-lg"></i>
          </button>
        </div>
      </div>
      <div
        v-if="connectionNotice"
        class="connection-notice"
        :class="`is-${connectionState}`"
        role="status"
        aria-live="polite"
        aria-atomic="true"
      >
        <i :class="connectionNoticeIcon" aria-hidden="true"></i>
        <span>{{ connectionNotice }}</span>
      </div>

      <!-- Main body layout with horizontal partition for Discord style members list -->
      <div style="display: flex; flex: 1; min-height: 0; width: 100%;">
        <!-- Chat Area (Messages + Input) -->
        <div style="display: flex; flex-direction: column; flex: 1; min-width: 0; height: 100%;">
          <!-- Messages View -->
          <div ref="messageThread" class="messages-thread">
            <div v-if="historyLoading" class="history-state" role="status">
              <i class="fa-solid fa-spinner fa-spin"></i>
              <span>Đang tải tin nhắn...</span>
            </div>
            <div v-else-if="historyError" class="history-state history-state-error" role="alert">
              <span>{{ historyError }}</span>
              <button type="button" class="state-action" @click="retryHistory">Thử lại</button>
            </div>
            <div v-else-if="currentTab === 'channel' && !activeChannel" class="history-state">
              Chọn một Channel để xem tin nhắn.
            </div>
            <div v-else-if="currentTab === 'channel' && activeMessages.length === 0" class="history-state">
              Chưa có tin nhắn trong kênh này.
            </div>
            <div v-else-if="currentTab === 'dm' && !activeChat" class="history-state">
              Chọn một cuộc trò chuyện hoặc người nhận để bắt đầu.
            </div>
            <div v-else-if="currentTab === 'dm' && activeMessages.length === 0" class="history-state">
              Chưa có tin nhắn trong cuộc trò chuyện này.
            </div>
            <button
              v-if="activeChat && activeMessages.length < messagePagination.totalCount"
              type="button"
              class="state-action load-older-action"
              :disabled="historyLoadingOlder"
              @click="loadOlderMessages"
            >
              {{ historyLoadingOlder ? 'Đang tải...' : 'Tải tin nhắn cũ hơn' }}
            </button>
            <div 
              v-for="msg in activeMessages"
              :key="messageKey(msg)"
              class="message-card"
              :class="{ 'mine': msg.senderId === currentUser.id, 'mention-target': route.query.messageId === msg.messageId }"
              :data-message-id="msg.messageId"
            >
              <el-avatar :size="32" :src="msg.senderAvatar" class="flex-shrink-0">
                {{ msg.senderName?.charAt(0) || '?' }}
              </el-avatar>
              <div class="message-body">
                <div class="message-meta">
                  <span class="sender-name">{{ msg.senderName }}</span>
                  <span class="send-time">{{ formatTime(msg.sentAt) }}</span>
                </div>
                <div class="message-content">
                  <p><template v-for="(segment, index) in msg.contentSegments" :key="`${msg.messageId}-${index}`"><span v-if="segment.isMention" class="message-mention">{{ segment.text }}</span><span v-else>{{ segment.text }}</span></template></p>
                  
                  <div v-if="msg.attachments.length" class="attachment-preview-container mt-2">
                    <div v-for="attachment in msg.attachments" :key="attachment.attachmentId" class="message-attachment">
                      <button
                        v-if="attachment.isImage && attachment.previewUrl"
                        type="button"
                        class="image-attachment"
                        :aria-label="`Tải ảnh ${attachment.originalFileName}`"
                        @click="downloadAttachment(attachment)"
                      >
                        <img :src="attachment.previewUrl" :alt="attachment.originalFileName" />
                      </button>
                      <div v-else class="attachment-preview flex items-center p-2 rounded">
                        <i :class="getFileIconClass(attachment.originalFileName)" class="text-2xl mr-2"></i>
                        <div class="flex flex-col overflow-hidden min-w-0">
                          <span class="text-xs font-semibold truncate text-primary">{{ attachment.originalFileName }}</span>
                          <span class="text-xxs text-muted">{{ formatFileSize(attachment.sizeBytes) }}</span>
                        </div>
                        <button
                          type="button"
                          class="attachment-download-btn"
                          :disabled="attachment.downloading"
                          :aria-label="`Tải ${attachment.originalFileName}`"
                          @click="downloadAttachment(attachment)"
                        ><i :class="attachment.downloading ? 'fa-solid fa-spinner fa-spin' : 'fa-solid fa-download'"></i> Tải xuống</button>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Input Bar -->
          <div class="chat-input-area">
            <!-- Hidden file input for attachment -->
            <input 
              type="file" 
              ref="fileInputRef" 
              style="display: none;" 
              multiple
              accept=".png,.jpg,.jpeg,.webp,.pdf,.txt,.docx,.xlsx"
              @change="handleFileChange" 
            />

            <!-- Attached File Preview Bar -->
            <div v-if="attachedFiles.length" class="attached-files-preview" aria-label="File đã chọn">
              <div v-for="file in attachedFiles" :key="file.id" class="attached-file-preview-bar">
                <img v-if="file.previewUrl" :src="file.previewUrl" alt="" class="selected-file-thumbnail" />
                <i v-else :class="getFileIconClass(file.name)" class="text-xl"></i>
                <span class="text-xs truncate font-semibold text-secondary">{{ file.name }}</span>
                <span class="text-xxs text-muted">({{ formatFileSize(file.sizeBytes) }})</span>
                <button type="button" class="remove-attachment-btn ml-auto" @click="removeAttachedFile(file.id)" :aria-label="`Gỡ ${file.name}`" title="Gỡ file đính kèm">
                  <i class="fa-solid fa-xmark"></i>
                </button>
              </div>
            </div>

            <div class="input-actions-bar">
              <el-button
                size="small"
                class="btn-secondary"
                title="Đính kèm file"
                :disabled="composerDisabled || attachedFiles.length >= 5"
                aria-label="Chọn file đính kèm"
                @click="triggerAttachment"
              >
                <i class="fa-solid fa-paperclip"></i>
              </el-button>
              
              <!-- Emoji Picker Popover -->
              <el-popover
                placement="top-start"
                :width="280"
                trigger="click"
                popper-class="emoji-popover-popper"
              >
                <template #reference>
                  <el-button size="small" class="btn-secondary" title="Emojis">
                    <i class="fa-regular fa-smile"></i>
                  </el-button>
                </template>
                <div class="emoji-picker-grid">
                  <span 
                    v-for="emoji in emojiList" 
                    :key="emoji" 
                    class="emoji-item"
                    @click="insertEmoji(emoji)"
                  >
                    {{ emoji }}
                  </span>
                </div>
              </el-popover>

            </div>
            <div class="input-form mention-composer">
              <textarea
                ref="composerInput"
                v-model="newMessage" 
                :placeholder="composerPlaceholder"
                class="chat-input w-full"
                rows="1"
                :maxlength="4000"
                :disabled="composerDisabled"
                @input="handleComposerInput"
                @keydown="handleComposerKeydown"
              ></textarea>
              <div
                v-if="mentionMenuOpen"
                class="mention-menu"
                role="listbox"
                aria-label="Channel members"
              >
                <div v-if="mentionLoading" class="mention-menu-state">Đang tìm thành viên...</div>
                <button
                  v-for="(member, index) in mentionSuggestions"
                  :key="member.userId"
                  type="button"
                  class="mention-option"
                  :class="{ active: index === mentionActiveIndex }"
                  role="option"
                  :aria-selected="index === mentionActiveIndex"
                  @mousedown.prevent="selectMention(member)"
                >
                  <el-avatar :size="26" :src="member.avatarUrl || ''">{{ member.displayName?.charAt(0) || '?' }}</el-avatar>
                  <span>{{ member.displayName }}</span>
                </button>
                <div v-if="!mentionLoading && mentionSuggestions.length === 0" class="mention-menu-state">Không có thành viên phù hợp.</div>
              </div>
              <button
                class="btn-send"
                :disabled="composerDisabled || (!newMessage.trim() && attachedFiles.length === 0)"
                :aria-label="sendingMessage ? 'Đang gửi tin nhắn' : 'Gửi tin nhắn'"
                :title="sendingMessage ? 'Đang gửi...' : 'Gửi tin nhắn'"
                @click="sendMessage"
              >
                <i :class="sendingMessage ? 'fa-solid fa-spinner fa-spin' : 'fa-solid fa-paper-plane'"></i>
              </button>
            </div>
            <div v-if="newMessage.length >= 3600" class="character-counter">
              {{ newMessage.length }}/4000
            </div>
          </div>
        </div>

        <!-- Right Server Members Sidebar -->
        <div v-if="currentTab === 'server' && showMembersSidebar" class="members-sidebar-right">
          <div class="flex items-center justify-between" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
            <span class="text-xs font-bold text-muted uppercase">Thành viên ({{ activeServerMembers.length }})</span>
          </div>
          <button class="invite-server-btn-sidebar mb-3" @click="openInviteServerModal">
            <i class="fa-solid fa-users"></i>
            <span>Mời bạn bè</span>
          </button>
          
          <div class="member-list-scrollable">
            <div v-for="user in activeServerMembers" :key="user.id" class="member-sidebar-card">
              <div class="avatar-status-wrapper">
                <el-avatar :size="24" :src="user.avatar">{{ user.name.charAt(0) }}</el-avatar>
                <span class="status-dot online"></span>
              </div>
              <span class="member-name truncate ml-2" style="font-size: 13px; color: var(--color-text-secondary);">{{ user.name }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Video Call Overlay (WebRTC / Jitsi Simulation) -->
    <el-dialog
      v-model="videoCallActive"
      width="800px"
      class="video-call-dialog"
      destroy-on-close
      append-to-body
    >
      <template #header>
        <div style="display: flex; align-items: center; gap: 8px;">
          <i :class="isCallCameraOn ? 'fa-solid fa-video text-primary' : 'fa-solid fa-phone text-success'"></i>
          <span style="font-size: 15px; font-weight: 600; color: #f8fafc;">
            {{ isCallCameraOn ? 'Cuộc gọi Video trực tiếp - ' : 'Cuộc gọi thoại trực tiếp - ' }}{{ activeChat.name }}
          </span>
        </div>
      </template>

      <div class="video-grid">
        <!-- Local User Feed -->
        <div class="video-feed local" :class="{ 'camera-active': (isCallCameraOn || isSharingScreen) }">
          <div v-show="isCallCameraOn || isSharingScreen" class="camera-stream-active" style="width: 100%; height: 100%;">
            <video 
              ref="localVideoRef" 
              autoplay 
              playsinline 
              muted 
              :style="{ 
                width: '100%', 
                height: '100%', 
                objectFit: 'cover', 
                transform: isSharingScreen ? 'none' : 'scaleX(-1)', 
                display: 'block' 
              }"
            ></video>
            <div class="feed-overlay">
              <span class="badge-live"><i class="fa-solid fa-circle text-danger animate-pulse"></i> LIVE</span>
              <span class="feed-name">Bạn (Quân)</span>
            </div>
          </div>
          <div v-show="!isCallCameraOn && !isSharingScreen" class="feed-placeholder">
            <el-avatar :size="80" :src="currentUser.avatar">{{ currentUser.name.charAt(0) }}</el-avatar>
            <span class="feed-name">Bạn (Quân) (Camera tắt)</span>
          </div>
        </div>

        <!-- Remote Partner Feed -->
        <div class="video-feed remote" :class="{ 'camera-active': isRemoteCameraOn }">
          <div v-if="isRemoteCameraOn" class="camera-stream-active">
            <div class="simulated-camera-bg remote-bg">
              <div class="camera-scanner"></div>
            </div>
            <div class="feed-overlay">
              <span class="badge-live"><i class="fa-solid fa-circle text-danger animate-pulse"></i> LIVE</span>
              <span class="feed-name">{{ activeChat.name }}</span>
            </div>
          </div>
          <div v-else class="feed-placeholder">
            <el-avatar :size="80" :src="activeChat.avatar">{{ activeChat.name.charAt(0) }}</el-avatar>
            <span class="feed-name">{{ activeChat.name }} (Camera tắt)</span>
          </div>
        </div>
      </div>

      <template #footer>
        <div class="call-controls-container">
          <!-- Mic Toggle -->
          <button 
            class="call-control-circle-btn" 
            :class="{ 'inactive': isCallMuted }" 
            @click="isCallMuted = !isCallMuted"
            :title="isCallMuted ? 'Bật Micro' : 'Tắt Micro'"
          >
            <i :class="isCallMuted ? 'fa-solid fa-microphone-slash' : 'fa-solid fa-microphone'"></i>
          </button>

          <!-- Camera Toggle -->
          <button 
            class="call-control-circle-btn" 
            :class="{ 'inactive': !isCallCameraOn }" 
            @click="toggleCallCamera"
            :title="isCallCameraOn ? 'Tắt Camera' : 'Bật Camera'"
          >
            <i :class="isCallCameraOn ? 'fa-solid fa-video' : 'fa-solid fa-video-slash'"></i>
          </button>

          <!-- Screen Share Toggle -->
          <button 
            class="call-control-circle-btn" 
            :class="{ 'active-share': isSharingScreen }" 
            @click="toggleScreenShare"
            :title="isSharingScreen ? 'Tắt chia sẻ' : 'Chia sẻ màn hình'"
            style="background-color: #4b5563; color: white;"
          >
            <i class="fa-solid fa-desktop" :style="{ color: isSharingScreen ? '#22c55e' : '#fff' }"></i>
          </button>

          <!-- Hang up -->
          <button 
            class="call-control-circle-btn hang-up" 
            @click="videoCallActive = false"
            title="Kết thúc cuộc gọi"
          >
            <i class="fa-solid fa-phone-slash"></i>
          </button>
        </div>
      </template>
    </el-dialog>

    <!-- Add Friend Dialog -->
    <el-dialog
      v-model="addFriendActive"
      width="480px"
      class="add-friend-dialog"
      append-to-body
    >
      <template #header>
        <div class="dialog-header flex items-center" style="display: flex; align-items: center; gap: 8px;">
          <i class="fa-solid fa-user-plus text-primary text-base" style="margin-right: 8px;"></i>
          <span class="text-sm font-semibold text-primary">Kết bạn & Mời thành viên</span>
        </div>
      </template>
      <div class="add-friend-content">
        <!-- My Invite Info -->
        <div class="my-invite-card mb-5">
          <h5 class="field-label mb-3">Tài khoản của bạn</h5>
          <div class="flex flex-col gap-3">
            <div class="info-row">
              <span class="info-label">Mã kết bạn:</span>
              <div class="info-value-wrapper">
                <code class="info-code">{{ myFriendCode }}</code>
                <button class="copy-btn-link" @click="copyToClipboard(myFriendCode)">
                  <i class="fa-regular fa-copy"></i> <span>Sao chép</span>
                </button>
              </div>
            </div>
            <div class="info-row">
              <span class="info-label">Link kết bạn:</span>
              <div class="info-value-wrapper">
                <span class="info-link truncate">{{ myInviteLink }}</span>
                <button class="copy-btn-link" @click="copyToClipboard(myInviteLink)">
                  <i class="fa-regular fa-copy"></i> <span>Sao chép</span>
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- Send Invite Form -->
        <div class="send-invite-section mb-6">
          <h5 class="field-label" style="margin-bottom: 8px !important; margin-top: 20px !important;">Gửi lời mời kết bạn</h5>
          <div style="display: flex; gap: 10px; align-items: center; width: 100%;">
            <input
              v-model="searchFriendQuery"
              placeholder="Nhập mã kết bạn, email hoặc tên..."
              class="custom-friend-input"
              style="flex: 1; height: 38px;"
              @keyup.enter="sendFriendRequest"
            />
            <button class="btn-save" style="height: 38px; padding: 0 16px;" @click="sendFriendRequest">Gửi yêu cầu</button>
          </div>
        </div>

        <!-- Friend Requests List -->
        <div class="friend-requests-section">
          <h5 class="field-label" style="margin-bottom: 10px !important; margin-top: 20px !important;">
            Lời mời kết bạn đang chờ ({{ friendRequests.length }})
          </h5>
          <div v-if="friendRequests.length === 0" class="text-center py-6 text-sm text-muted">
            Không có lời mời nào đang chờ
          </div>
          <div v-else class="requests-list">
            <div v-for="req in friendRequests" :key="req.id" class="request-item" style="display: flex; align-items: center; padding: 12px 16px; justify-content: space-between;">
              <div style="display: flex; align-items: center; flex: 1; min-width: 0;">
                <el-avatar :size="36" :src="req.avatar" style="flex-shrink: 0;">{{ req.name.charAt(0) }}</el-avatar>
                <div class="flex flex-col ml-3 overflow-hidden" style="margin-left: 12px;">
                  <span class="text-sm font-semibold truncate" style="color: var(--color-text-primary); display: block;">{{ req.name }}</span>
                  <span class="text-xs text-muted truncate" style="display: block; margin-top: 2px;">{{ req.email || 'Mã: ' + req.code }}</span>
                </div>
              </div>
              <div style="display: flex; gap: 10px; margin-left: 16px; flex-shrink: 0;">
                <button class="btn-action-accept" @click="acceptFriend(req)">Đồng ý</button>
                <button class="btn-action-decline" @click="declineFriend(req)">Từ chối</button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </el-dialog>


    <!-- Create Server Dialog -->
    <el-dialog
      v-model="createServerActive"
      title="Tạo Server mới"
      width="440px"
      append-to-body
    >
      <div style="display: flex; flex-direction: column; gap: 12px;">
        <label style="font-size: 13px; font-weight: 600; color: var(--color-text-secondary);">Tên Server</label>
        <input 
          v-model="newServerName" 
          placeholder="Nhập tên server mới..." 
          class="custom-friend-input"
          style="width: 100%; height: 38px;"
        />
      </div>
      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 10px;">
          <el-button @click="createServerActive = false">Hủy</el-button>
          <button class="btn-save" @click="createNewServer">Tạo Server</button>
        </div>
      </template>
    </el-dialog>

    <!-- Create Channel Dialog -->
    <el-dialog
      v-model="createChannelActive"
      title="Tạo Kênh chat mới (#)"
      width="440px"
      append-to-body
    >
      <div style="display: flex; flex-direction: column; gap: 16px;">
        <div style="display: flex; flex-direction: column; gap: 8px;">
          <label style="font-size: 13px; font-weight: 600; color: var(--color-text-secondary);">Tên Kênh</label>
          <input 
            v-model="newChannelName" 
            placeholder="Ví dụ: backend-dev" 
            class="custom-friend-input"
            style="width: 100%; height: 38px;"
            maxlength="100"
            :disabled="creatingChannel"
          />
        </div>
        <div style="display: flex; flex-direction: column; gap: 8px;">
          <label style="font-size: 13px; font-weight: 600; color: var(--color-text-secondary);">Mô tả kênh</label>
          <input 
            v-model="newChannelDesc" 
            placeholder="Mô tả mục đích của kênh này..." 
            class="custom-friend-input"
            style="width: 100%; height: 38px;"
            maxlength="500"
            :disabled="creatingChannel"
          />
        </div>
      </div>
      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 10px;">
          <el-button :disabled="creatingChannel" @click="closeCreateChannelModal">Hủy</el-button>
          <button
            class="btn-save"
            :disabled="creatingChannel || !newChannelName.trim()"
            @click="createNewChannel"
          >
            {{ creatingChannel ? 'Đang tạo...' : 'Tạo Kênh' }}
          </button>
        </div>
      </template>
    </el-dialog>

    <!-- Create Voice Channel Dialog -->
    <el-dialog
      v-model="createVoiceActive"
      title="Tạo Kênh thoại mới (Voice)"
      width="440px"
      append-to-body
    >
      <div style="display: flex; flex-direction: column; gap: 12px;">
        <label style="font-size: 13px; font-weight: 600; color: var(--color-text-secondary);">Tên Kênh thoại</label>
        <input 
          v-model="newVoiceName" 
          placeholder="Ví dụ: Họp kỹ thuật" 
          class="custom-friend-input"
          style="width: 100%; height: 38px;"
        />
      </div>
      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 10px;">
          <button class="btn-cancel-custom" @click="createVoiceActive = false">Hủy</button>
          <button class="btn-primary-custom" @click="createNewVoice">
            <i class="fa-solid fa-volume-high"></i> Tạo Kênh thoại
          </button>
        </div>
      </template>
    </el-dialog>

    <!-- Invite Friends to Server Dialog -->
    <el-dialog
      v-model="inviteServerActive"
      title="Mời bạn bè vào Server"
      width="460px"
      append-to-body
    >
      <div style="display: flex; flex-direction: column; gap: 12px; max-height: 300px; overflow-y: auto;">
        <span class="text-xs text-muted mb-2">Chọn bạn bè từ danh sách hệ thống để thêm vào Server này:</span>
        <div v-if="inviteableUsers.length === 0" class="text-center py-6 text-sm text-muted">
          Tất cả bạn bè đã ở trong Server này.
        </div>
        <div v-else style="display: flex; flex-direction: column; gap: 10px;">
          <div 
            v-for="u in inviteableUsers" 
            :key="u.id" 
            style="display: flex; align-items: center; justify-content: space-between; padding: 6px 12px; border-radius: 8px; background-color: rgba(255,255,255,0.02);"
          >
            <div style="display: flex; align-items: center; gap: 10px;">
              <el-avatar :size="28" :src="u.avatar">{{ u.name.charAt(0) }}</el-avatar>
              <div style="display: flex; flex-direction: column; text-align: left;">
                <span style="font-size: 13px; font-weight: 600; color: var(--color-text-primary);">{{ u.name }}</span>
                <span style="font-size: 11px; color: var(--color-text-muted);">{{ u.statusText || 'Thành viên' }}</span>
              </div>
            </div>
            <el-checkbox v-model="u.checked" size="large" />
          </div>
        </div>
      </div>
      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 10px;">
          <el-button @click="inviteServerActive = false">Hủy</el-button>
          <button class="btn-save" :disabled="inviteableUsers.filter(u => u.checked).length === 0" @click="confirmInviteToServer">Mời vào nhóm</button>
        </div>
      </template>
    </el-dialog>

    <!-- Server Settings Dialog -->
    <el-dialog
      v-model="serverSettingsActive"
      title="Cài đặt Server"
      width="480px"
      append-to-body
    >
      <div style="display: flex; flex-direction: column; gap: 16px;">
        <div style="display: flex; flex-direction: column; gap: 8px;">
          <label style="font-size: 13px; font-weight: 600; color: var(--color-text-secondary);">Tên Server</label>
          <input 
            v-model="editServerName" 
            placeholder="Nhập tên server..." 
            class="custom-friend-input"
            style="width: 100%; height: 38px;"
          />
        </div>
        <div style="display: flex; flex-direction: column; gap: 8px;">
          <label style="font-size: 13px; font-weight: 600; color: var(--color-text-secondary);">Màu chủ đạo</label>
          <div style="display: flex; gap: 8px;">
            <div 
              v-for="color in colors" 
              :key="color"
              :style="{ backgroundColor: color }"
              style="width: 32px; height: 32px; border-radius: 50%; cursor: pointer; display: flex; align-items: center; justify-content: center; border: 2px solid transparent;"
              :class="{ 'selected-color-swatch': editServerColor === color }"
              @click="editServerColor = color"
            >
              <i class="fa-solid fa-check text-white text-xs" v-if="editServerColor === color"></i>
            </div>
          </div>
        </div>

        <!-- Danger Zone -->
        <div style="margin-top: 12px; border-top: 1px solid rgba(255,255,255,0.08); padding-top: 16px;" v-if="activeServer.id !== 'srv-sprinta'">
          <h5 style="color: var(--color-danger); font-size: 14px; font-weight: 600; margin-bottom: 8px;">Vùng nguy hiểm</h5>
          <span style="font-size: 12px; color: var(--color-text-muted); display: block; margin-bottom: 12px;">Hành động này sẽ xóa hoàn toàn Server này cùng tất cả các kênh chat và lịch sử trò chuyện đi kèm.</span>
          <button class="btn-danger-custom" @click="deleteActiveServer">
            <i class="fa-solid fa-trash-can"></i> Xóa Server
          </button>
        </div>
      </div>
      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 10px;">
          <button class="btn-cancel-custom" @click="serverSettingsActive = false">Hủy</button>
          <button class="btn-primary-custom" @click="saveServerSettings">
            <i class="fa-solid fa-floppy-disk"></i> Lưu thay đổi
          </button>
        </div>
      </template>
    </el-dialog>

    <!-- Create Server From Dm Dialog -->
    <el-dialog
      v-model="createServerFromDmActive"
      title="Tạo nhóm Server từ cuộc trò chuyện"
      width="440px"
      append-to-body
    >
      <div style="display: flex; flex-direction: column; gap: 12px;">
        <label style="font-size: 13px; font-weight: 600; color: var(--color-text-secondary);">Tên nhóm Server mới</label>
        <input 
          v-model="dmServerName" 
          placeholder="Nhập tên nhóm..." 
          class="custom-friend-input"
          style="width: 100%; height: 38px;"
        />
        <span style="font-size: 11px; color: var(--color-text-muted);">
          Thành viên sẽ bao gồm bạn và đối tác chat hiện tại. Hệ thống sẽ tự động chuyển sang tab Chat nhóm sau khi tạo thành công.
        </span>
      </div>
      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 10px;">
          <button class="btn-cancel-custom" @click="createServerFromDmActive = false">Hủy</button>
          <button class="btn-primary-custom" @click="confirmCreateServerFromDm">
            <i class="fa-solid fa-users"></i> Tạo nhóm
          </button>
        </div>
      </template>
    </el-dialog>

    <!-- Outgoing Call (Ringing) Overlay -->
    <div v-if="outgoingCallActive" class="calling-overlay">
      <div class="calling-container">
        <div class="calling-avatar-pulse">
          <el-avatar :size="100" :src="callingPartnerAvatar">{{ callingPartnerName.charAt(0) }}</el-avatar>
          <div class="pulse-ring ring-1"></div>
          <div class="pulse-ring ring-2"></div>
        </div>
        <h3 class="calling-name">{{ callingPartnerName }}</h3>
        <p class="calling-status">Đang đổ chuông...</p>
        
        <!-- Hang up button -->
        <button class="call-decline-circle-btn" @click="cancelOutgoingCall" style="margin-bottom: 20px;">
          <i class="fa-solid fa-phone-slash text-xl"></i>
        </button>

        <!-- Simulated Partner Receiver Control Panel -->
        <div class="simulated-receiver-panel" style="margin-top: 15px; padding: 16px; border-radius: 12px; background: rgba(0,0,0,0.4); border: 1px dashed rgba(255,255,255,0.2); width: 100%; text-align: center;">
          <p style="font-size: 12px; color: var(--color-text-secondary); margin-bottom: 12px; font-weight: 500;">[ Giả lập phía người nhận cuộc gọi ]</p>
          <div style="display: flex; gap: 16px; justify-content: center; align-items: center;">
            <button class="call-accept-circle-btn small" @click="partnerAcceptCall" style="width: 44px; height: 44px; font-size: 14px;" title="Chấp nhận cuộc gọi">
              <i class="fa-solid fa-phone text-sm"></i>
            </button>
            <button class="call-decline-circle-btn small" @click="partnerDeclineCall" style="width: 44px; height: 44px; font-size: 14px;" title="Từ chối cuộc gọi">
              <i class="fa-solid fa-phone-slash text-sm"></i>
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Incoming Call Overlay -->
    <div v-if="incomingCallActive" class="calling-overlay">
      <div class="calling-container">
        <div class="calling-avatar-pulse animate-bounce">
          <el-avatar :size="100" :src="callingPartnerAvatar">{{ callingPartnerName.charAt(0) }}</el-avatar>
        </div>
        <h3 class="calling-name">{{ callingPartnerName }}</h3>
        <p class="calling-status">Cuộc gọi đến...</p>
        <div style="display: flex; gap: 24px; margin-top: 24px;">
          <button class="call-accept-circle-btn" @click="acceptIncomingCall">
            <i class="fa-solid fa-phone text-xl"></i>
          </button>
          <button class="call-decline-circle-btn" @click="declineIncomingCall">
            <i class="fa-solid fa-phone-slash text-xl"></i>
          </button>
        </div>
      </div>
    </div>

    <!-- Group Voice Channel Call Dialog -->
    <el-dialog
      v-model="voiceChannelCallActive"
      width="900px"
      class="video-call-dialog group-call-dialog"
      append-to-body
    >
      <template #header>
        <div style="display: flex; align-items: center; gap: 8px;">
          <i class="fa-solid fa-volume-high text-primary"></i>
          <span style="font-size: 15px; font-weight: 600; color: #f8fafc;">
            Kênh thoại: {{ activeVoiceChannel?.name }}
          </span>
        </div>
      </template>

      <div class="video-grid group-video-grid" style="display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; height: auto; min-height: 320px;">
        <!-- Render each user in the voice channel -->
        <div 
          v-for="user in activeVoiceChannel?.users" 
          :key="user.id" 
          class="video-feed"
          :class="{ 'local-user': user.id === currentUser.id, 'camera-active': (user.id === currentUser.id && (isCallCameraOn || isSharingScreen)) }"
          style="aspect-ratio: 4/3; height: auto;"
        >
          <!-- If local user and camera/screen share is on, render video element -->
          <div v-if="user.id === currentUser.id" style="width: 100%; height: 100%; position: relative; display: flex; align-items: center; justify-content: center;">
            <div v-show="isCallCameraOn || isSharingScreen" style="width: 100%; height: 100%;">
              <video 
                :ref="el => { if (el) groupLocalVideoRef = el }" 
                autoplay 
                playsinline 
                muted 
                :style="{ 
                  width: '100%', 
                  height: '100%', 
                  objectFit: 'cover', 
                  transform: isSharingScreen ? 'none' : 'scaleX(-1)', 
                  display: 'block' 
                }"
              ></video>
            </div>
            <div v-show="!isCallCameraOn && !isSharingScreen" class="feed-placeholder">
              <el-avatar :size="80" :src="currentUser.avatar">{{ currentUser.name.charAt(0) }}</el-avatar>
            </div>
          </div>
          
          <!-- Remote user placeholder -->
          <div v-else class="feed-placeholder">
            <el-avatar :size="80" :src="user.avatar">{{ user.name?.charAt(0) }}</el-avatar>
          </div>

          <div class="feed-overlay">
            <span class="badge-live" v-if="user.id === currentUser.id && (isCallCameraOn || isSharingScreen)">
              <i class="fa-solid fa-circle text-danger animate-pulse"></i> LIVE
            </span>
            <span class="feed-name">{{ user.name }} {{ user.id === currentUser.id ? '(Bạn)' : '' }}</span>
          </div>
        </div>
      </div>

      <template #footer>
        <div style="display: flex; justify-content: space-between; align-items: center; width: 100%;">
          <!-- Left: minimize button -->
          <el-button @click="voiceChannelCallActive = false" style="background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.1); color: #fff;">
            <i class="fa-solid fa-compress mr-1" style="margin-right: 6px;"></i> Thu nhỏ về nền
          </el-button>

          <!-- Center: Call controls -->
          <div class="call-controls-container" style="margin: 0; display: flex; gap: 12px; justify-content: center; align-items: center;">
            <!-- Mic Toggle -->
            <button 
              class="call-control-circle-btn" 
              :class="{ 'inactive': isMuted }" 
              @click="isMuted = !isMuted"
              :title="isMuted ? 'Bật Micro' : 'Tắt Micro'"
            >
              <i :class="isMuted ? 'fa-solid fa-microphone-slash' : 'fa-solid fa-microphone'"></i>
            </button>

            <!-- Camera Toggle -->
            <button 
              class="call-control-circle-btn" 
              :class="{ 'inactive': !isCallCameraOn }" 
              @click="isCallCameraOn = !isCallCameraOn"
              :title="isCallCameraOn ? 'Tắt Camera' : 'Bật Camera'"
            >
              <i :class="isCallCameraOn ? 'fa-solid fa-video' : 'fa-solid fa-video-slash'"></i>
            </button>

            <!-- Screen Share Toggle -->
            <button 
              class="call-control-circle-btn" 
              :class="{ 'active-share': isSharingScreen }" 
              @click="toggleScreenShare"
              :title="isSharingScreen ? 'Tắt chia sẻ' : 'Chia sẻ màn hình'"
              style="background-color: #4b5563; color: white;"
            >
              <i class="fa-solid fa-desktop" :style="{ color: isSharingScreen ? '#22c55e' : '#fff' }"></i>
            </button>

            <!-- Disconnect -->
            <button 
              class="call-control-circle-btn hang-up" 
              @click="leaveVoiceChannelAndClose"
              title="Rời kênh thoại"
            >
              <i class="fa-solid fa-phone-slash"></i>
            </button>
          </div>
          
          <!-- Right: spacer to balance layout -->
          <div style="width: 130px;"></div>
        </div>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted, onBeforeUnmount, nextTick, watch, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'

import { useI18n } from '@/composables/useI18n'

const { t } = useI18n()

import { collaborationApi } from '@/api/collaborationApi'
import { useProjectStore } from '@/store/useProjectStore'
import { useAuthStore } from '@/store/useAuthStore'
import {
  collaborationRealtime,
  COLLABORATION_REALTIME_STATES,
  getCollaborationHubErrorCode
} from '@/services/collaborationRealtime'
import {
  clearScopedCurrentProjectId,
  getScopedCurrentProjectId,
  setScopedCurrentProjectId
} from '@/utils/projectContext'

const route = useRoute()
const router = useRouter()
const projectStore = useProjectStore()
const authStore = useAuthStore()
const currentTab = computed(() => route.query.tab === 'dm' ? 'dm' : 'channel')
const projectOptions = computed(() => projectStore.sidebarProjects)
const activeProjectId = ref('')
const activeProject = computed(() =>
  projectOptions.value.find(project => project.id === activeProjectId.value) || null
)
const projectsLoading = ref(false)
const projectsError = ref('')


const defaultServers = [
  { id: 'srv-sprinta', name: 'SprintA Workspace', color: '#6366f1', channels: [], voiceChannels: [
    { id: 'vc-sprint', name: 'Họp Kế Hoạch Sprint 🚀', users: [
      { id: 'user-kiet', name: 'Nguyễn Tuấn Kiệt', avatar: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&q=80&w=128' },
      { id: 'user-phat', name: 'Trần Gia Phát', avatar: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&q=80&w=128' }
    ] },
    { id: 'vc-tech', name: 'Trao Đổi Kỹ Thuật 💻', users: [] },
    { id: 'vc-lounge', name: 'Trà Chanh Chém Gió ☕', users: [] }
  ], members: [
    { id: 'user-kiet', name: 'Nguyễn Tuấn Kiệt', avatar: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&q=80&w=128' },
    { id: 'user-phat', name: 'Trần Gia Phát', avatar: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&q=80&w=128' }
  ] },
  { id: 'srv-gaming', name: 'Góc Giải Trí 🎮', color: '#10b981', channels: [], voiceChannels: [
    { id: 'vc-pubg', name: 'PUBG Team 🔫', users: [] },
    { id: 'vc-lol', name: 'Liên Minh Huyền Thoại ⚔️', users: [] }
  ] }
]

const loadServers = () => {
  const stored = localStorage.getItem('collaboration_servers')
  if (stored) {
    try {
      return JSON.parse(stored)
    } catch (e) {
      console.error(e)
    }
  }
  return defaultServers
}

const servers = ref(loadServers())
const saveServers = () => {
  localStorage.setItem('collaboration_servers', JSON.stringify(servers.value))
}

const activeServer = ref(servers.value[0])

const channels = ref([])
const channelsLoading = ref(false)
const channelsLoadingMore = ref(false)
const channelsError = ref('')
const channelPagination = ref({
  page: 1,
  pageSize: 50,
  totalCount: 0,
  ordering: ''
})
const channelAbortController = ref(null)
let channelRequestId = 0
const voiceChannels = computed(() => activeServer.value ? activeServer.value.voiceChannels : [])

const selectServer = (srv) => {
  activeServer.value = srv
  if (srv.channels.length > 0) {
    selectChat(srv.channels[0], 'channel')
  }
}

// Modal state refs
const createServerActive = ref(false)
const newServerName = ref('')
const createChannelActive = ref(false)
const newChannelName = ref('')
const newChannelDesc = ref('')
const creatingChannel = ref(false)
const createChannelIdempotencyKey = ref('')
const createChannelPayloadFingerprint = ref('')
const createChannelAbortController = ref(null)
const createVoiceActive = ref(false)
const newVoiceName = ref('')

// Server Settings States
const serverSettingsActive = ref(false)
const editServerName = ref('')
const editServerColor = ref('')

const openServerSettingsModal = () => {
  if (!activeServer.value) return
  editServerName.value = activeServer.value.name
  editServerColor.value = activeServer.value.color
  serverSettingsActive.value = true
}

const saveServerSettings = () => {
  if (!editServerName.value.trim()) {
    ElMessage.warning('Vui lòng nhập tên Server!')
    return
  }
  if (activeServer.value) {
    activeServer.value.name = editServerName.value.trim()
    activeServer.value.color = editServerColor.value
    saveServers()
    ElMessage.success('Cập nhật cài đặt Server thành công!')
  }
  serverSettingsActive.value = false
}

const deleteActiveServer = () => {
  if (activeServer.value.id === 'srv-sprinta') {
    ElMessage.error('Không thể xóa Server mặc định!')
    return
  }
  servers.value = servers.value.filter(s => s.id !== activeServer.value.id)
  saveServers()
  serverSettingsActive.value = false
  selectServer(servers.value[0])
  ElMessage.success('Đã xóa Server thành công!')
}

const openCreateServerModal = () => {
  newServerName.value = ''
  createServerActive.value = true
}
const openCreateChannelModal = () => {
  if (!activeProjectId.value) {
    ElMessage.warning('Chọn Project trước khi tạo Channel.')
    return
  }
  newChannelName.value = ''
  newChannelDesc.value = ''
  createChannelIdempotencyKey.value = makeChannelIdempotencyKey()
  createChannelPayloadFingerprint.value = ''
  createChannelActive.value = true
}

const closeCreateChannelModal = () => {
  if (creatingChannel.value) return
  createChannelActive.value = false
  createChannelIdempotencyKey.value = ''
  createChannelPayloadFingerprint.value = ''
}
const openCreateVoiceModal = () => {
  newVoiceName.value = ''
  createVoiceActive.value = true
}

const colors = ['#6366f1', '#10b981', '#f59e0b', '#ef4444', '#ec4899', '#8b5cf6', '#06b6d4']

const createNewServer = () => {
  if (!newServerName.value.trim()) {
    ElMessage.warning('Vui lòng nhập tên Server!')
    return
  }
  const color = colors[Math.floor(Math.random() * colors.length)]
  const newSrv = {
    id: `srv-${Date.now()}`,
    name: newServerName.value.trim(),
    color: color,
    channels: [],
    voiceChannels: [
      { id: `vc-gen-${Date.now()}`, name: 'Phòng thoại chung 🔊', users: [] }
    ],
    members: [
      {
        id: currentUser.value.id,
        name: currentUser.value.name,
        avatar: currentUser.value.avatar
      }
    ]
  }
  servers.value.push(newSrv)
  saveServers()
  createServerActive.value = false
  selectServer(newSrv)
  ElMessage.success(`Đã tạo Server mới: ${newSrv.name}`)
}

const createServerFromDmActive = ref(false)
const dmServerName = ref('')

const openCreateServerFromDmModal = () => {
  const myLastName = currentUser.value.name ? currentUser.value.name.split(' ').pop() : 'Quân'
  const partnerLastName = activeChat.value.name ? activeChat.value.name.split(' ').pop() : 'Bạn'
  dmServerName.value = `Nhóm ${myLastName} & ${partnerLastName}`
  createServerFromDmActive.value = true
}

const confirmCreateServerFromDm = () => {
  if (!dmServerName.value.trim()) {
    ElMessage.warning('Vui lòng nhập tên nhóm!')
    return
  }
  
  const color = colors[Math.floor(Math.random() * colors.length)]
  const partnerId = activeChat.value.id
  const partner = members.value.find(m => m.id === partnerId)
  
  const newSrv = {
    id: `srv-${Date.now()}`,
    name: dmServerName.value.trim(),
    color: color,
    channels: [],
    voiceChannels: [
      { id: `vc-gen-${Date.now()}`, name: 'Phòng thoại chung 🔊', users: [] }
    ],
    members: [
      {
        id: currentUser.value.id,
        name: currentUser.value.name,
        avatar: currentUser.value.avatar
      }
    ]
  }
  
  if (partner) {
    newSrv.members.push({
      id: partner.id,
      name: partner.name,
      avatar: partner.avatar
    })
  }
  
  servers.value.push(newSrv)
  saveServers()
  createServerFromDmActive.value = false
  
  // Switch to Team Chat
  router.push({ path: '/chat', query: { tab: 'channel' } })
  
  // Select the newly created server
  selectServer(newSrv)
  ElMessage.success(`Đã tạo nhóm server "${newSrv.name}" và chuyển sang chat nhóm!`)
}
const createNewChannel = async () => {
  if (creatingChannel.value || !activeProjectId.value) return
  const name = newChannelName.value.trim()
  const description = newChannelDesc.value.trim()
  if (!name) {
    ElMessage.warning('Vui lòng nhập tên Channel.')
    return
  }

  const payload = {
    name,
    description: description || null,
    visibility: 'Private'
  }
  const fingerprint = JSON.stringify(payload)
  if (
    !createChannelIdempotencyKey.value ||
    (createChannelPayloadFingerprint.value &&
      createChannelPayloadFingerprint.value !== fingerprint)
  ) {
    createChannelIdempotencyKey.value = makeChannelIdempotencyKey()
  }
  createChannelPayloadFingerprint.value = fingerprint
  const requestProjectId = activeProjectId.value
  const controller = new AbortController()
  createChannelAbortController.value = controller
  creatingChannel.value = true

  try {
    const result = await collaborationApi.createProjectChannel(
      requestProjectId,
      payload,
      {
        idempotencyKey: createChannelIdempotencyKey.value,
        signal: controller.signal
      }
    )
    if (activeProjectId.value !== requestProjectId) return
    const channel = mapChannel(result, requestProjectId)
    await loadChannels({ page: 1, selectFirst: false })
    channelsError.value = ''
    if (!channels.value.some(item => item.id === channel.id)) {
      channels.value = [...channels.value, channel]
      channelPagination.value.totalCount = Math.max(
        channelPagination.value.totalCount,
        channels.value.length
      )
    }
    createChannelActive.value = false
    createChannelIdempotencyKey.value = ''
    createChannelPayloadFingerprint.value = ''
    await selectChat(channel, 'channel')
    ElMessage.success(`Đã tạo Channel #${channel.name}`)
  } catch (error) {
    if (isCanceledRequest(error)) return
    const status = error?.response?.status
    if (status === 401) {
      clearCollaborationState()
    } else if (status === 403) {
      ElMessage.error('Bạn không có quyền tạo Channel trong Project này.')
    } else if (status === 404 || status === 409) {
      await loadChannels({ page: 1 })
      ElMessage.error(
        status === 409
          ? 'Yêu cầu tạo Channel bị xung đột. Danh sách đã được làm mới.'
          : 'Project không còn khả dụng. Danh sách Channel đã được làm mới.'
      )
    } else {
      ElMessage.error(apiErrorMessage(error, 'Không thể tạo Channel. Bạn có thể thử lại.'))
    }
  } finally {
    if (createChannelAbortController.value === controller) {
      createChannelAbortController.value = null
    }
    creatingChannel.value = false
  }
}

const createNewVoice = () => {
  if (!newVoiceName.value.trim()) {
    ElMessage.warning('Vui lòng nhập tên kênh thoại!')
    return
  }
  const newVc = {
    id: `vc-custom-${Date.now()}`,
    name: newVoiceName.value.trim(),
    users: []
  }
  activeServer.value.voiceChannels.push(newVc)
  saveServers()
  createVoiceActive.value = false
  ElMessage.success(`Đã tạo kênh thoại: ${newVc.name}`)
}

const members = ref([])
const membersLoading = ref(false)
const membersError = ref('')
const memberAbortController = ref(null)
let memberRequestId = 0
const selectedRecipientId = ref('')

const currentUser = ref({
  id: '',
  name: '',
  avatar: ''
})

const directConversations = ref([])
const conversationsLoading = ref(false)
const conversationsLoadingMore = ref(false)
const conversationsError = ref('')
const conversationPagination = ref({
  page: 1,
  pageSize: 50,
  totalCount: 0,
  ordering: ''
})
const conversationAbortController = ref(null)
let conversationRequestId = 0
const findingConversation = ref(false)
const findConversationAbortController = ref(null)

const activeChat = ref(null)

const activeMessages = ref([])
const activeChannel = computed(() =>
  activeChat.value?.type === 'channel' ? activeChat.value : null
)
const activeDirectConversation = computed(() =>
  activeChat.value?.type === 'dm' ? activeChat.value : null
)
const newMessage = ref('')
const composerInput = ref(null)
const selectedMentions = ref([])
const mentionSuggestions = ref([])
const mentionMenuOpen = ref(false)
const mentionLoading = ref(false)
const mentionActiveIndex = ref(0)
const mentionRange = ref(null)
const mentionAbortController = ref(null)
let mentionRequestId = 0
let mentionDebounceTimer = null
let previousComposerValue = ''
const messageThread = ref(null)
const historyLoading = ref(false)
const historyLoadingOlder = ref(false)
const historyError = ref('')
const sendingMessage = ref(false)
const sendMessageAbortController = ref(null)
const messagePagination = ref({
  page: 1,
  pageSize: 50,
  totalCount: 0,
  ordering: ''
})
const messageAbortController = ref(null)
let messageRequestId = 0
let chatSelectionId = 0
const connectionState = ref(COLLABORATION_REALTIME_STATES.DISCONNECTED)
const connectionNotice = ref('')
const connectionNoticeIcon = computed(() => {
  if (
    connectionState.value === COLLABORATION_REALTIME_STATES.CONNECTING ||
    connectionState.value === COLLABORATION_REALTIME_STATES.RECONNECTING
  ) {
    return 'fa-solid fa-arrows-rotate fa-spin'
  }
  if (connectionState.value === COLLABORATION_REALTIME_STATES.CONNECTED) {
    return 'fa-solid fa-circle-check'
  }
  return 'fa-solid fa-triangle-exclamation'
})
let connectionNoticeTimer = null
let markReadTimer = null
let pendingRead = null
let markReadVersion = 0
const realtimeUnsubscribers = []
const videoCallActive = ref(false)

const isCallMuted = ref(false)
const isCallCameraOn = ref(false)
const isRemoteCameraOn = ref(false)

const toggleCallCamera = () => {
  isCallCameraOn.value = !isCallCameraOn.value
  if (isCallCameraOn.value) {
    setTimeout(() => {
      isRemoteCameraOn.value = true
    }, 800)
  } else {
    isRemoteCameraOn.value = false
  }
}

// Voice Channels Discord style refs
const activeVoiceChannel = ref(null)
const isMuted = ref(false)
const isCameraOn = ref(false)

const localVideoRef = ref(null)
let localStream = null
const groupLocalVideoRef = ref(null)
const voiceChannelCallActive = ref(false)
const isSharingScreen = ref(false)
let screenStream = null

const startLocalCamera = async () => {
  try {
    if (localStream) {
      stopLocalCamera()
    }
    localStream = await navigator.mediaDevices.getUserMedia({
      video: { width: 640, height: 480 },
      audio: false
    })
    if (localVideoRef.value) {
      localVideoRef.value.srcObject = localStream
    }
  } catch (error) {
    console.error('Error accessing webcam:', error)
    ElMessage.error('Không thể truy cập camera của bạn!')
  }
}

const stopLocalCamera = () => {
  if (localStream) {
    localStream.getTracks().forEach(track => track.stop())
    localStream = null
  }
  if (localVideoRef.value) {
    localVideoRef.value.srcObject = null
  }
  const groupEl = getGroupVideoEl()
  if (groupEl) {
    groupEl.srcObject = null
  }
}

const getGroupVideoEl = () => {
  if (!groupLocalVideoRef.value) return null
  return Array.isArray(groupLocalVideoRef.value) 
    ? groupLocalVideoRef.value[0] 
    : groupLocalVideoRef.value
}

const startLocalCameraOrGroup = async () => {
  try {
    if (localStream) {
      stopLocalCamera()
    }
    localStream = await navigator.mediaDevices.getUserMedia({
      video: { width: 640, height: 480 },
      audio: false
    })
    
    await nextTick()
    const groupEl = getGroupVideoEl()
    if (voiceChannelCallActive.value && groupEl) {
      groupEl.srcObject = localStream
    } else if (videoCallActive.value && localVideoRef.value) {
      localVideoRef.value.srcObject = localStream
    }
  } catch (error) {
    console.error('Error accessing webcam:', error)
    ElMessage.error('Không thể truy cập camera của bạn!')
    isCallCameraOn.value = false
  }
}

const toggleScreenShare = async () => {
  if (isSharingScreen.value) {
    stopScreenShare()
    isSharingScreen.value = false
  } else {
    try {
      if (localStream) stopLocalCamera()
      
      screenStream = await navigator.mediaDevices.getDisplayMedia({
        video: true,
        audio: false
      })
      isSharingScreen.value = true
      isCallCameraOn.value = false
      
      await nextTick()
      const videoEl = getGroupVideoEl() || localVideoRef.value
      if (videoEl) {
        videoEl.srcObject = screenStream
      }
      
      screenStream.getVideoTracks()[0].onended = () => {
        stopScreenShare()
        isSharingScreen.value = false
      }
    } catch (error) {
      console.error('Error sharing screen:', error)
      ElMessage.error('Không thể chia sẻ màn hình!')
    }
  }
}

const stopScreenShare = () => {
  if (screenStream) {
    screenStream.getTracks().forEach(track => track.stop())
    screenStream = null
  }
  if (isCallCameraOn.value) {
    startLocalCameraOrGroup()
  } else {
    const videoEl = getGroupVideoEl() || localVideoRef.value
    if (videoEl) {
      videoEl.srcObject = null
    }
  }
}

const leaveVoiceChannelAndClose = () => {
  leaveVoiceChannel()
  voiceChannelCallActive.value = false
}

watch([videoCallActive, voiceChannelCallActive, isCallCameraOn], async ([activeDm, activeGroup, camOn]) => {
  if ((activeDm || activeGroup) && camOn) {
    isSharingScreen.value = false
    await nextTick()
    await startLocalCameraOrGroup()
  } else {
    if (!isSharingScreen.value) {
      stopLocalCamera()
    }
  }
})

const joinVoiceChannel = (vc) => {
  if (activeVoiceChannel.value?.id === vc.id) {
    voiceChannelCallActive.value = true
    return
  }
  
  if (activeVoiceChannel.value) {
    leaveVoiceChannel()
  }
  
  activeVoiceChannel.value = vc
  vc.users.push({
    id: currentUser.value.id,
    name: currentUser.value.name,
    avatar: currentUser.value.avatar
  })
  ElMessage.success(`Đã kết nối vào kênh thoại: ${vc.name}`)
  voiceChannelCallActive.value = true
}

const leaveVoiceChannel = () => {
  if (!activeVoiceChannel.value) return
  
  const vc = voiceChannels.value.find(v => v.id === activeVoiceChannel.value.id)
  if (vc) {
    vc.users = vc.users.filter(u => u.id !== currentUser.value.id)
  }
  ElMessage.info(`Đã ngắt kết nối khỏi kênh thoại: ${activeVoiceChannel.value.name}`)
  activeVoiceChannel.value = null
}

const isCanceledRequest = (error) =>
  error?.name === 'CanceledError' || error?.code === 'ERR_CANCELED'

const apiErrorMessage = (error, fallback) => {
  const message = error?.response?.data?.message
  return typeof message === 'string' && message.trim() ? message : fallback
}

const makeChannelIdempotencyKey = () => {
  const randomPart = typeof globalThis.crypto?.randomUUID === 'function'
    ? globalThis.crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(16).slice(2)}`
  return `channel:${randomPart}`
}

const mapChannel = (item, expectedProjectId) => {
  if (
    !item?.channelId ||
    !item?.workspaceId ||
    item?.projectId !== expectedProjectId
  ) {
    throw new Error('Invalid Channel response scope.')
  }

  return {
    id: item.channelId,
    channelId: item.channelId,
    name: item.name,
    desc: item.description || '',
    workspaceId: item.workspaceId,
    projectId: item.projectId,
    visibility: item.visibility,
    isMember: Boolean(item.isMember),
    canRead: Boolean(item.canRead),
    canSend: Boolean(item.canSend),
    canManage: Boolean(item.canManage),
    createdAt: item.createdAt,
    updatedAt: item.updatedAt,
    unreadCount: Math.max(0, Number(item.unreadCount || 0)),
    lastReadMessageId: item.lastReadMessageId || null
  }
}

const mapAttachment = (item) => {
  if (
    !item?.attachmentId ||
    typeof item?.originalFileName !== 'string' ||
    typeof item?.contentType !== 'string' ||
    !Number.isFinite(Number(item?.sizeBytes)) ||
    'storageKey' in item
  ) {
    throw new Error('Invalid collaboration attachment metadata.')
  }
  return {
    attachmentId: item.attachmentId,
    originalFileName: item.originalFileName,
    contentType: item.contentType,
    sizeBytes: Number(item.sizeBytes),
    isImage: item.contentType.startsWith('image/'),
    previewUrl: '',
    previewLoading: false,
    downloading: false
  }
}

const mapMentions = (items, content) => {
  if (!Array.isArray(items)) return []
  const seenUsers = new Set()
  return items
    .filter(item => {
      const start = Number(item?.startIndex)
      const length = Number(item?.length)
      const valid = item?.userId &&
        !seenUsers.has(item.userId) &&
        Number.isInteger(start) && start >= 0 &&
        Number.isInteger(length) && length >= 2 &&
        start + length <= content.length &&
        content.slice(start, start + length) === item.displayText &&
        item.displayText.startsWith('@')
      if (valid) seenUsers.add(item.userId)
      return valid
    })
    .map(item => ({
      userId: item.userId,
      displayText: item.displayText,
      startIndex: Number(item.startIndex),
      length: Number(item.length)
    }))
    .sort((left, right) => left.startIndex - right.startIndex)
}

const buildContentSegments = (content, mentions) => {
  const segments = []
  let cursor = 0
  mentions.forEach(mention => {
    if (mention.startIndex < cursor) return
    if (mention.startIndex > cursor) {
      segments.push({ text: content.slice(cursor, mention.startIndex), isMention: false })
    }
    segments.push({ text: mention.displayText, isMention: true })
    cursor = mention.startIndex + mention.length
  })
  if (cursor < content.length || segments.length === 0) {
    segments.push({ text: content.slice(cursor), isMention: false })
  }
  return segments
}

const mapChannelMessage = (item, expectedChannelId) => {
  if (
    !item?.messageId ||
    item?.channelId !== expectedChannelId ||
    !item?.sender?.userId ||
    !Number.isFinite(Date.parse(item?.createdAt)) ||
    typeof item?.content !== 'string'
  ) {
    throw new Error('Invalid Channel message response scope.')
  }

  const mentions = mapMentions(item.mentions, item.content)
  return {
    messageId: item.messageId,
    channelId: item.channelId,
    orderingId: item.orderingId,
    senderId: item.sender.userId,
    senderName: item.sender.displayName || 'Unknown user',
    senderAvatar: item.sender.avatarUrl || '',
    content: item.content,
    mentions,
    contentSegments: buildContentSegments(item.content, mentions),
    sentAt: item.createdAt,
    attachments: Array.isArray(item.attachments) ? item.attachments.map(mapAttachment) : []
  }
}

const mapDirectConversation = (item) => {
  if (!item?.conversationId || !item?.otherParticipant?.userId) {
    throw new Error('Invalid Direct Message conversation response.')
  }

  return {
    id: item.conversationId,
    conversationId: item.conversationId,
    participantUserId: item.otherParticipant.userId,
    name: item.otherParticipant.displayName || 'Unknown user',
    avatar: item.otherParticipant.avatarUrl || '',
    lastMessagePreview: item.lastMessagePreview || '',
    lastMessageAt: item.lastMessageAt || null,
    createdAt: item.createdAt,
    unreadCount: Math.max(0, Number(item.unreadCount || 0)),
    lastReadMessageId: item.lastReadMessageId || null,
    type: 'dm'
  }
}

const mapDirectMessage = (item, expectedConversationId) => {
  if (
    !item?.messageId ||
    item?.conversationId !== expectedConversationId ||
    !item?.sender?.userId ||
    !Number.isFinite(Date.parse(item?.createdAt)) ||
    typeof item?.content !== 'string'
  ) {
    throw new Error('Invalid Direct Message response scope.')
  }

  return {
    messageId: item.messageId,
    conversationId: item.conversationId,
    senderId: item.sender.userId,
    senderName: item.sender.displayName || 'Unknown user',
    senderAvatar: item.sender.avatarUrl || '',
    content: item.content,
    mentions: [],
    contentSegments: [{ text: item.content, isMention: false }],
    sentAt: item.createdAt,
    attachments: Array.isArray(item.attachments) ? item.attachments.map(mapAttachment) : []
  }
}

const messageKey = (message) => message.messageId

const compareMessages = (left, right) => {
  const timeDifference = Date.parse(left.sentAt) - Date.parse(right.sentAt)
  if (Number.isFinite(timeDifference) && timeDifference !== 0) return timeDifference
  return `${left.messageId}`.localeCompare(`${right.messageId}`)
}

const mergeMessages = (...collections) => {
  const unique = new Map()
  collections.flat().forEach((message) => {
    if (message?.messageId) unique.set(message.messageId, message)
  })
  return Array.from(unique.values()).sort(compareMessages)
}

const messageAttachmentObjectUrls = new Set()

const revokeMessageAttachmentUrls = () => {
  messageAttachmentObjectUrls.forEach(url => URL.revokeObjectURL(url))
  messageAttachmentObjectUrls.clear()
}

const hydrateImagePreviews = async (messages) => {
  const attachments = messages
    .flatMap(message => message.attachments || [])
    .filter(attachment => attachment.isImage && !attachment.previewUrl && !attachment.previewLoading)
  await Promise.all(attachments.map(async (attachment) => {
    attachment.previewLoading = true
    try {
      const blob = await collaborationApi.downloadAttachment(attachment.attachmentId)
      if (!blob?.type?.startsWith('image/')) return
      const url = URL.createObjectURL(blob)
      attachment.previewUrl = url
      messageAttachmentObjectUrls.add(url)
    } catch {
      // The file card remains usable; authorization is checked again on explicit download.
    } finally {
      attachment.previewLoading = false
    }
  }))
}

const downloadAttachment = async (attachment) => {
  if (!attachment?.attachmentId || attachment.downloading) return
  attachment.downloading = true
  try {
    const blob = await collaborationApi.downloadAttachment(attachment.attachmentId)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = attachment.originalFileName
    link.rel = 'noopener'
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
  } catch (error) {
    const status = error?.response?.status
    ElMessage.error(status === 404
      ? 'File không tồn tại hoặc bạn không còn quyền tải.'
      : 'Không thể tải file đính kèm.')
  } finally {
    attachment.downloading = false
  }
}

const formatFileSize = (bytes) => {
  if (!Number.isFinite(Number(bytes)) || Number(bytes) < 0) return '0 B'
  if (bytes >= 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`
  if (bytes >= 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${bytes} B`
}

const formatUnreadCount = (count) => count > 99 ? '99+' : `${count}`

const applyReadState = (state) => {
  if (!state?.resourceId || !['channel', 'dm'].includes(state.resourceType)) return
  const unreadCount = Math.max(0, Number(state.unreadCount || 0))
  const updateItem = item => item.id === state.resourceId
    ? {
        ...item,
        unreadCount,
        lastReadMessageId: state.lastReadMessageId || item.lastReadMessageId || null
      }
    : item
  if (state.resourceType === 'channel') {
    channels.value = channels.value.map(updateItem)
  } else {
    directConversations.value = directConversations.value.map(updateItem)
  }
  if (
    activeChat.value?.id === state.resourceId &&
    activeChat.value?.type === state.resourceType
  ) {
    activeChat.value = updateItem(activeChat.value)
  }
}

const cancelPendingMarkRead = () => {
  markReadVersion += 1
  pendingRead = null
  if (markReadTimer) {
    window.clearTimeout(markReadTimer)
    markReadTimer = null
  }
}

const flushMarkRead = async (request, version) => {
  if (
    version !== markReadVersion ||
    !request?.messageId ||
    activeChat.value?.id !== request.resourceId ||
    activeChat.value?.type !== request.resourceType
  ) {
    return
  }
  try {
    const state = request.resourceType === 'channel'
      ? await collaborationApi.markChannelRead(request.resourceId, request.messageId)
      : await collaborationApi.markDirectConversationRead(request.resourceId, request.messageId)
    if (
      version !== markReadVersion ||
      activeChat.value?.id !== request.resourceId ||
      activeChat.value?.type !== request.resourceType
    ) {
      return
    }
    applyReadState(state)
  } catch (error) {
    if (isCanceledRequest(error) || version !== markReadVersion) return
    if (error?.response?.status === 401) clearCollaborationState()
  } finally {
    if (version === markReadVersion) pendingRead = null
  }
}

const scheduleMarkRead = (resourceType, resourceId, messageId) => {
  if (
    !messageId ||
    activeChat.value?.id !== resourceId ||
    activeChat.value?.type !== resourceType
  ) {
    return
  }
  pendingRead = { resourceType, resourceId, messageId }
  const version = ++markReadVersion
  if (markReadTimer) window.clearTimeout(markReadTimer)
  markReadTimer = window.setTimeout(() => {
    markReadTimer = null
    const request = pendingRead
    void flushMarkRead(request, version)
  }, 180)
}

const markRenderedLatestMessageRead = (resourceType, resourceId) => {
  const latestMessage = activeMessages.value.at(-1)
  if (latestMessage?.messageId) {
    scheduleMarkRead(resourceType, resourceId, latestMessage.messageId)
  }
}

const appendRealtimeMessage = async (message) => {
  if (activeMessages.value.some(item => item.messageId === message.messageId)) return
  const shouldScroll = isNearMessageBottom()
  activeMessages.value = mergeMessages(activeMessages.value, [message])
  void hydrateImagePreviews([message])
  messagePagination.value.totalCount = Math.max(
    messagePagination.value.totalCount + 1,
    activeMessages.value.length
  )
  await nextTick()
  if (shouldScroll) scrollToBottom()
}

const clearMessageHistory = () => {
  cancelPendingMarkRead()
  messageAbortController.value?.abort()
  messageAbortController.value = null
  messageRequestId += 1
  chatSelectionId += 1
  revokeMessageAttachmentUrls()
  activeMessages.value = []
  historyLoading.value = false
  historyLoadingOlder.value = false
  historyError.value = ''
  sendingMessage.value = false
  messagePagination.value = {
    page: 1,
    pageSize: 50,
    totalCount: 0,
    ordering: ''
  }
}

const clearChannelSelection = () => {
  sendMessageAbortController.value?.abort()
  sendMessageAbortController.value = null
  clearMessageHistory()
  if (activeChat.value?.type === 'channel') {
    void collaborationRealtime.leaveChannel(activeChat.value.id)
    activeChat.value = null
  }
  newMessage.value = ''
  resetMentionComposer()
  removeAttachedFile()
}

const clearDirectSelection = ({ clearComposer = true } = {}) => {
  findConversationAbortController.value?.abort()
  findConversationAbortController.value = null
  findingConversation.value = false
  sendMessageAbortController.value?.abort()
  sendMessageAbortController.value = null
  clearMessageHistory()
  if (activeChat.value?.type === 'dm') {
    void collaborationRealtime.leaveDirectConversation(activeChat.value.id)
    activeChat.value = null
  }
  selectedRecipientId.value = ''
  if (clearComposer) newMessage.value = ''
  resetMentionComposer()
  removeAttachedFile()
}

const clearDirectContext = () => {
  memberAbortController.value?.abort()
  memberAbortController.value = null
  memberRequestId += 1
  conversationAbortController.value?.abort()
  conversationAbortController.value = null
  conversationRequestId += 1
  members.value = []
  membersLoading.value = false
  membersError.value = ''
  directConversations.value = []
  conversationsLoading.value = false
  conversationsLoadingMore.value = false
  conversationsError.value = ''
  conversationPagination.value = {
    page: 1,
    pageSize: 50,
    totalCount: 0,
    ordering: ''
  }
  clearDirectSelection()
}

const clearChannels = () => {
  createChannelAbortController.value?.abort()
  createChannelAbortController.value = null
  creatingChannel.value = false
  channelAbortController.value?.abort()
  channelAbortController.value = null
  channelRequestId += 1
  channels.value = []
  channelsLoading.value = false
  channelsLoadingMore.value = false
  channelsError.value = ''
  channelPagination.value = {
    page: 1,
    pageSize: 50,
    totalCount: 0,
    ordering: ''
  }
  clearChannelSelection()
}

const clearCollaborationState = () => {
  void collaborationRealtime.stop()
  clearChannels()
  clearDirectContext()
  activeProjectId.value = ''
  currentUser.value = { id: '', name: '', avatar: '' }
  clearScopedCurrentProjectId()
}

const selectProject = (projectId) => {
  if (!projectOptions.value.some(project => project.id === projectId)) return
  activeProjectId.value = projectId
}

const loadProjects = async ({ force = false } = {}) => {
  projectsLoading.value = true
  projectsError.value = ''
  try {
    const projects = await projectStore.fetchAllProjects(force)
    if (projectStore.error && projects.length === 0) {
      projectsError.value = 'Không thể tải danh sách Project.'
      return
    }
    const preferredProjectId = getScopedCurrentProjectId()
    if (
      !activeProjectId.value &&
      preferredProjectId &&
      projectOptions.value.some(project => project.id === preferredProjectId)
    ) {
      activeProjectId.value = preferredProjectId
    }
  } finally {
    projectsLoading.value = false
  }
}

const retryProjects = () => loadProjects({ force: true })

const loadChannels = async ({
  page = 1,
  append = false,
  selectFirst = true,
  preserveSelection = false
} = {}) => {
  const projectId = activeProjectId.value
  if (!projectId) {
    clearChannels()
    return
  }

  channelAbortController.value?.abort()
  const controller = new AbortController()
  channelAbortController.value = controller
  const requestId = channelRequestId + 1
  channelRequestId = requestId
  if (append) {
    channelsLoadingMore.value = true
  } else {
    if (!preserveSelection) clearChannelSelection()
    channels.value = []
    channelsError.value = ''
    channelsLoading.value = true
  }

  try {
    const result = await collaborationApi.getProjectChannels(projectId, {
      page,
      pageSize: channelPagination.value.pageSize,
      signal: controller.signal
    })
    if (
      requestId !== channelRequestId ||
      activeProjectId.value !== projectId
    ) {
      return
    }
    const items = Array.isArray(result?.items)
      ? result.items.map(item => mapChannel(item, projectId))
      : []
    const merged = append ? [...channels.value, ...items] : items
    const unique = new Map(merged.map(item => [item.id, item]))
    channels.value = Array.from(unique.values())
    channelPagination.value = {
      page: Number(result?.page || page),
      pageSize: Number(result?.pageSize || 50),
      totalCount: Number(result?.totalCount || 0),
      ordering: result?.ordering || ''
    }
    channelsError.value = ''

    if (
      !append &&
      selectFirst &&
      currentTab.value === 'channel' &&
      channels.value.length > 0
    ) {
      const linkedChannel = channels.value.find(item => item.id === route.query.channelId)
      await selectChat(linkedChannel || channels.value[0], 'channel')
    }
  } catch (error) {
    if (
      isCanceledRequest(error) ||
      requestId !== channelRequestId ||
      activeProjectId.value !== projectId
    ) {
      return
    }
    const status = error?.response?.status
    if (!append) {
      channels.value = []
      clearChannelSelection()
    }
    if (status === 401) {
      clearCollaborationState()
      channelsError.value = 'Phiên đăng nhập đã hết hạn.'
    } else if (status === 403) {
      channelsError.value = 'Bạn không có quyền xem Channel của Project này.'
    } else if (status === 404) {
      channelsError.value = 'Project không tồn tại hoặc bạn không còn quyền truy cập.'
      await projectStore.fetchAllProjects(true)
      if (!projectOptions.value.some(project => project.id === projectId)) {
        activeProjectId.value = ''
      }
    } else {
      channelsError.value = apiErrorMessage(error, 'Không thể tải danh sách Channel.')
    }
  } finally {
    if (requestId === channelRequestId) {
      channelsLoading.value = false
      channelsLoadingMore.value = false
      channelAbortController.value = null
    }
  }
}

const retryChannels = () => loadChannels({ page: 1 })

const loadMoreChannels = () => {
  if (
    channelsLoadingMore.value ||
    channels.value.length >= channelPagination.value.totalCount
  ) {
    return
  }
  return loadChannels({
    page: channelPagination.value.page + 1,
    append: true
  })
}

const loadDirectMessageUsers = async (projectId) => {
  if (!projectId) {
    members.value = []
    membersError.value = ''
    return
  }

  memberAbortController.value?.abort()
  const controller = new AbortController()
  memberAbortController.value = controller
  const requestId = memberRequestId + 1
  memberRequestId = requestId
  membersLoading.value = true
  membersError.value = ''

  try {
    const result = await collaborationApi.getDirectMessageUsers(projectId, {
      page: 1,
      pageSize: 100,
      signal: controller.signal
    })
    if (requestId !== memberRequestId || activeProjectId.value !== projectId) return
    members.value = (Array.isArray(result?.items) ? result.items : [])
      .filter(user => user?.id && user.id !== currentUser.value.id)
      .map(user => ({
        id: user.id,
        name: user.fullName || 'Unknown user',
        avatar: user.avatarUrl || '',
        statusText: user.jobTitle || 'Thành viên'
      }))
    membersError.value = ''
  } catch (error) {
    if (
      isCanceledRequest(error) ||
      requestId !== memberRequestId ||
      activeProjectId.value !== projectId
    ) {
      return
    }
    members.value = []
    const status = error?.response?.status
    if (status === 401) {
      clearCollaborationState()
      membersError.value = 'Phiên đăng nhập đã hết hạn.'
    } else if (status === 403 || status === 404) {
      membersError.value = 'Project không còn khả dụng hoặc bạn không có quyền xem thành viên.'
    } else {
      membersError.value = apiErrorMessage(error, 'Không thể tải danh sách thành viên.')
    }
  } finally {
    if (requestId === memberRequestId) {
      membersLoading.value = false
      memberAbortController.value = null
    }
  }
}

const retryMembers = () => loadDirectMessageUsers(activeProjectId.value)

const loadDirectConversations = async ({
  page = 1,
  append = false,
  selectFirst = true
} = {}) => {
  conversationAbortController.value?.abort()
  const controller = new AbortController()
  conversationAbortController.value = controller
  const requestId = conversationRequestId + 1
  conversationRequestId = requestId
  if (append) {
    conversationsLoadingMore.value = true
  } else {
    conversationsLoading.value = true
    conversationsError.value = ''
  }

  try {
    const result = await collaborationApi.getDirectConversations({
      page,
      pageSize: conversationPagination.value.pageSize,
      signal: controller.signal
    })
    if (requestId !== conversationRequestId || currentTab.value !== 'dm') return
    const items = Array.isArray(result?.items)
      ? result.items.map(mapDirectConversation)
      : []
    const merged = append ? [...directConversations.value, ...items] : items
    directConversations.value = Array.from(
      new Map(merged.map(item => [item.id, item])).values()
    )
    conversationPagination.value = {
      page: Number(result?.page || page),
      pageSize: Number(result?.pageSize || 50),
      totalCount: Number(result?.totalCount || 0),
      ordering: result?.ordering || ''
    }
    conversationsError.value = ''

    if (
      !append &&
      selectFirst &&
      !activeDirectConversation.value &&
      directConversations.value.length > 0
    ) {
      await selectChat(directConversations.value[0], 'dm')
    }
  } catch (error) {
    if (isCanceledRequest(error) || requestId !== conversationRequestId) return
    if (!append) {
      directConversations.value = []
      clearDirectSelection()
    }
    const status = error?.response?.status
    if (status === 401) {
      clearCollaborationState()
      conversationsError.value = 'Phiên đăng nhập đã hết hạn.'
    } else if (status === 403) {
      conversationsError.value = 'Bạn không có quyền tải Direct Message.'
    } else {
      conversationsError.value = apiErrorMessage(error, 'Không thể tải danh sách cuộc trò chuyện.')
    }
  } finally {
    if (requestId === conversationRequestId) {
      conversationsLoading.value = false
      conversationsLoadingMore.value = false
      conversationAbortController.value = null
    }
  }
}

const retryConversations = () => loadDirectConversations({ page: 1 })

const loadMoreConversations = () => {
  if (
    conversationsLoadingMore.value ||
    directConversations.value.length >= conversationPagination.value.totalCount
  ) {
    return
  }
  return loadDirectConversations({
    page: conversationPagination.value.page + 1,
    append: true,
    selectFirst: false
  })
}

const loadChannelHistory = async (channel, {
  page = 1,
  older = false,
  refresh = false
} = {}) => {
  if (!channel?.id || channel.projectId !== activeProjectId.value) return
  messageAbortController.value?.abort()
  const controller = new AbortController()
  messageAbortController.value = controller
  const requestId = messageRequestId + 1
  messageRequestId = requestId
  if (older) {
    historyLoadingOlder.value = true
  } else if (!refresh) {
    activeMessages.value = []
    historyError.value = ''
    historyLoading.value = true
  }
  const previousScrollHeight = messageThread.value?.scrollHeight || 0

  try {
    const result = await collaborationApi.getChannelMessages(channel.id, {
      page,
      pageSize: messagePagination.value.pageSize,
      signal: controller.signal
    })
    if (
      requestId !== messageRequestId ||
      activeChannel.value?.id !== channel.id ||
      activeProjectId.value !== channel.projectId
    ) {
      return
    }
    const newestFirst = Array.isArray(result?.items)
      ? result.items.map(item => mapChannelMessage(item, channel.id))
      : []
    const chronologicalPage = [...newestFirst].reverse()
    activeMessages.value = mergeMessages(chronologicalPage, activeMessages.value)
    void hydrateImagePreviews(activeMessages.value)
    messagePagination.value = {
      page: Number(result?.page || page),
      pageSize: Number(result?.pageSize || 50),
      totalCount: Number(result?.totalCount || 0),
      ordering: result?.ordering || ''
    }
    historyError.value = ''

    await nextTick()
    if (older && messageThread.value) {
      messageThread.value.scrollTop +=
        messageThread.value.scrollHeight - previousScrollHeight
    } else if (!refresh) {
      scrollToBottom()
    }
    if (page === 1 && !older) {
      markRenderedLatestMessageRead('channel', channel.id)
      const targetMessageId = `${route.query.messageId || ''}`
      if (targetMessageId && targetMessageId === activeMessages.value.find(item => item.messageId === targetMessageId)?.messageId) {
        messageThread.value?.querySelector(`[data-message-id="${targetMessageId}"]`)?.scrollIntoView({ block: 'center' })
      }
    }
  } catch (error) {
    if (
      isCanceledRequest(error) ||
      requestId !== messageRequestId ||
      activeChannel.value?.id !== channel.id
    ) {
      return
    }
    const status = error?.response?.status
    if (status === 401) {
      clearCollaborationState()
      historyError.value = 'Phiên đăng nhập đã hết hạn.'
    } else if (status === 403 || status === 404) {
      clearChannelSelection()
      channels.value = channels.value.filter(item => item.id !== channel.id)
      historyError.value = 'Channel không còn khả dụng hoặc bạn không còn quyền truy cập.'
      if (status === 404) await loadChannels({ page: 1 })
    } else {
      historyError.value = apiErrorMessage(error, 'Không thể tải lịch sử Channel.')
    }
  } finally {
    if (requestId === messageRequestId) {
      historyLoading.value = false
      historyLoadingOlder.value = false
      messageAbortController.value = null
    }
  }
}

const loadDirectHistory = async (conversation, {
  page = 1,
  older = false,
  refresh = false
} = {}) => {
  if (!conversation?.id) return
  messageAbortController.value?.abort()
  const controller = new AbortController()
  messageAbortController.value = controller
  const requestId = messageRequestId + 1
  messageRequestId = requestId
  if (older) {
    historyLoadingOlder.value = true
  } else if (!refresh) {
    activeMessages.value = []
    historyError.value = ''
    historyLoading.value = true
  }
  const previousScrollHeight = messageThread.value?.scrollHeight || 0

  try {
    const result = await collaborationApi.getDirectMessages(conversation.id, {
      page,
      pageSize: messagePagination.value.pageSize,
      signal: controller.signal
    })
    if (
      requestId !== messageRequestId ||
      activeDirectConversation.value?.id !== conversation.id
    ) {
      return
    }
    const newestFirst = Array.isArray(result?.items)
      ? result.items.map(item => mapDirectMessage(item, conversation.id))
      : []
    const chronologicalPage = [...newestFirst].reverse()
    activeMessages.value = mergeMessages(chronologicalPage, activeMessages.value)
    void hydrateImagePreviews(activeMessages.value)
    messagePagination.value = {
      page: Number(result?.page || page),
      pageSize: Number(result?.pageSize || 50),
      totalCount: Number(result?.totalCount || 0),
      ordering: result?.ordering || ''
    }
    historyError.value = ''

    await nextTick()
    if (older && messageThread.value) {
      messageThread.value.scrollTop +=
        messageThread.value.scrollHeight - previousScrollHeight
    } else if (!refresh) {
      scrollToBottom()
    }
    if (page === 1 && !older) {
      markRenderedLatestMessageRead('dm', conversation.id)
    }
  } catch (error) {
    if (
      isCanceledRequest(error) ||
      requestId !== messageRequestId ||
      activeDirectConversation.value?.id !== conversation.id
    ) {
      return
    }
    const status = error?.response?.status
    if (status === 401) {
      clearCollaborationState()
      historyError.value = 'Phiên đăng nhập đã hết hạn.'
    } else if (status === 403 || status === 404) {
      clearDirectSelection()
      directConversations.value = directConversations.value.filter(
        item => item.id !== conversation.id
      )
      historyError.value = 'Cuộc trò chuyện không còn khả dụng hoặc bạn không còn quyền truy cập.'
      await loadDirectConversations({ page: 1, selectFirst: false })
    } else {
      historyError.value = apiErrorMessage(error, 'Không thể tải lịch sử Direct Message.')
    }
  } finally {
    if (requestId === messageRequestId) {
      historyLoading.value = false
      historyLoadingOlder.value = false
      messageAbortController.value = null
    }
  }
}

const retryHistory = () => {
  if (activeChannel.value) {
    return loadChannelHistory(activeChannel.value, { page: 1 })
  }
  if (activeDirectConversation.value) {
    return loadDirectHistory(activeDirectConversation.value, { page: 1 })
  }
}

const loadOlderMessages = () => {
  if (
    !activeChat.value ||
    historyLoadingOlder.value ||
    activeMessages.value.length >= messagePagination.value.totalCount
  ) {
    return
  }
  const options = {
    page: messagePagination.value.page + 1,
    older: true
  }
  return activeChannel.value
    ? loadChannelHistory(activeChannel.value, options)
    : loadDirectHistory(activeDirectConversation.value, options)
}

const composerDisabled = computed(() => {
  if (currentTab.value === 'dm') return !activeChat.value || sendingMessage.value
  return (
    !activeChannel.value ||
    !activeChannel.value.canSend ||
    historyLoading.value ||
    sendingMessage.value
  )
})

const composerPlaceholder = computed(() => {
  if (currentTab.value === 'dm') return 'Gửi tin nhắn...'
  if (!activeChannel.value) return 'Chọn Channel để gửi tin nhắn'
  if (!activeChannel.value.canSend) return 'Bạn không có quyền gửi vào Channel này'
  return `Gửi tin nhắn tới #${activeChannel.value.name}`
})

const hubErrorMessage = (code) => ({
  AUTH_REQUIRED: 'Phiên đăng nhập đã hết hạn.',
  USER_INACTIVE: 'Tài khoản không còn hoạt động.',
  CHANNEL_NOT_FOUND_OR_FORBIDDEN: 'Channel không còn khả dụng hoặc bạn không còn quyền truy cập.',
  CONVERSATION_NOT_FOUND_OR_FORBIDDEN: 'Cuộc trò chuyện không còn khả dụng hoặc bạn không còn quyền truy cập.',
  INVALID_ID: 'Cuộc trò chuyện được chọn không hợp lệ.',
  JOIN_FAILED: 'Không thể kết nối realtime. Lịch sử REST vẫn khả dụng.'
}[code] || 'Không thể kết nối realtime. Lịch sử REST vẫn khả dụng.')

const setConnectionNotice = (message, { clearAfter = 0 } = {}) => {
  if (connectionNoticeTimer) {
    window.clearTimeout(connectionNoticeTimer)
    connectionNoticeTimer = null
  }
  connectionNotice.value = message
  if (clearAfter > 0) {
    connectionNoticeTimer = window.setTimeout(() => {
      connectionNotice.value = ''
      connectionNoticeTimer = null
    }, clearAfter)
  }
}

const handleRealtimeState = ({ state, code, reconnected = false }) => {
  connectionState.value = state
  if (state === COLLABORATION_REALTIME_STATES.CONNECTING) {
    setConnectionNotice('Đang kết nối realtime…')
  } else if (state === COLLABORATION_REALTIME_STATES.RECONNECTING) {
    setConnectionNotice('Đang kết nối lại…')
  } else if (state === COLLABORATION_REALTIME_STATES.CONNECTED && reconnected) {
    setConnectionNotice('Đã kết nối lại', { clearAfter: 2500 })
  } else if (state === COLLABORATION_REALTIME_STATES.CONNECTED) {
    setConnectionNotice('')
  } else if (state === COLLABORATION_REALTIME_STATES.ERROR) {
    setConnectionNotice(hubErrorMessage(code))
  } else if (
    state === COLLABORATION_REALTIME_STATES.DISCONNECTED &&
    currentUser.value.id
  ) {
    setConnectionNotice('Mất kết nối realtime. Tin nhắn vẫn được gửi và tải bằng REST.')
  }
}

const handleChannelRealtimeMessage = async (payload) => {
  const channel = activeChannel.value
  if (!channel?.id || payload?.channelId !== channel.id || !payload?.messageId) return
  try {
    await appendRealtimeMessage(mapChannelMessage(payload, channel.id))
    markRenderedLatestMessageRead('channel', channel.id)
  } catch {
    // Ignore payloads that do not match the documented Channel event contract.
  }
}

const handleDirectRealtimeMessage = async (payload) => {
  const conversation = activeDirectConversation.value
  if (
    !conversation?.id ||
    payload?.conversationId !== conversation.id ||
    !payload?.messageId
  ) {
    return
  }
  try {
    await appendRealtimeMessage(mapDirectMessage(payload, conversation.id))
    markRenderedLatestMessageRead('dm', conversation.id)
  } catch {
    // Ignore payloads that do not match the documented Direct event contract.
  }
}

const handleReadStateChanged = (payload) => {
  applyReadState(payload)
}

const receivedMentionNotificationIds = new Set()
const handleMentionCreated = (payload) => {
  if (!payload?.notificationId || receivedMentionNotificationIds.has(payload.notificationId)) return
  receivedMentionNotificationIds.add(payload.notificationId)
  window.dispatchEvent(new CustomEvent('collaboration-mention-created', { detail: payload }))
}

const leaveActiveRealtimeGroup = async (chat = activeChat.value) => {
  if (chat?.type === 'channel') {
    await collaborationRealtime.leaveChannel(chat.id)
  } else if (chat?.type === 'dm') {
    await collaborationRealtime.leaveDirectConversation(chat.id)
  }
}

const handleRealtimeGroupFailure = async ({ scope, id, code }) => {
  connectionState.value = COLLABORATION_REALTIME_STATES.ERROR
  setConnectionNotice(hubErrorMessage(code))
  const sensitiveFailure = [
    'AUTH_REQUIRED',
    'USER_INACTIVE',
    'CHANNEL_NOT_FOUND_OR_FORBIDDEN',
    'CONVERSATION_NOT_FOUND_OR_FORBIDDEN',
    'INVALID_ID'
  ].includes(code)
  if (!sensitiveFailure) return

  if (scope === 'channel' && activeChannel.value?.id === id) {
    clearChannelSelection()
    channels.value = channels.value.filter(item => item.id !== id)
    historyError.value = hubErrorMessage(code)
    await loadChannels({ page: 1, selectFirst: false })
  } else if (scope === 'dm' && activeDirectConversation.value?.id === id) {
    clearDirectSelection()
    directConversations.value = directConversations.value.filter(item => item.id !== id)
    historyError.value = hubErrorMessage(code)
    await loadDirectConversations({ page: 1, selectFirst: false })
  }
}

const joinRealtimeForChat = async (chat) => {
  if (
    !chat?.id ||
    activeChat.value?.id !== chat.id ||
    activeChat.value?.type !== chat.type
  ) {
    return false
  }
  try {
    if (chat.type === 'channel') {
      await collaborationRealtime.joinChannel(chat.id)
    } else {
      await collaborationRealtime.joinDirectConversation(chat.id)
    }
    return true
  } catch (error) {
    await handleRealtimeGroupFailure({
      scope: chat.type,
      id: chat.id,
      code: getCollaborationHubErrorCode(error)
    })
    return false
  }
}

const handleRealtimeReconnected = async ({ errors }) => {
  if (errors.length > 0) {
    await handleRealtimeGroupFailure(errors[0])
    return
  }
  window.dispatchEvent(new CustomEvent('collaboration-notifications-refresh'))
  const chat = activeChat.value
  if (!chat?.id) return
  if (chat.type === 'channel') {
    await Promise.all([
      loadChannelHistory(chat, { page: 1, refresh: true }),
      loadChannels({ page: 1, selectFirst: false, preserveSelection: true })
    ])
  } else {
    await Promise.all([
      loadDirectHistory(chat, { page: 1, refresh: true }),
      loadDirectConversations({ page: 1, selectFirst: false })
    ])
  }
}

const registerRealtimeHandlers = () => {
  realtimeUnsubscribers.push(
    collaborationRealtime.subscribeChannelMessage(handleChannelRealtimeMessage),
    collaborationRealtime.subscribeDirectMessage(handleDirectRealtimeMessage),
    collaborationRealtime.subscribeReadState(handleReadStateChanged),
    collaborationRealtime.subscribeMention(handleMentionCreated),
    collaborationRealtime.subscribeState(handleRealtimeState),
    collaborationRealtime.subscribeReconnected(handleRealtimeReconnected)
  )
}

let componentMounted = false
let collaborationContextVersion = 0

const initializeCollaborationContext = async ({ forceProjects = false } = {}) => {
  const version = ++collaborationContextVersion
  try {
    const meRes = await axiosClient.get('/users/me')
    if (!componentMounted || version !== collaborationContextVersion) return
    const me = meRes?.data?.data
    if (!me?.id) throw new Error('Current user response is invalid.')
    currentUser.value = {
      id: me.id,
      name: me.fullName || '',
      avatar: me.avatarUrl || ''
    }
  } catch (error) {
    const status = error?.response?.status
    membersError.value = status === 401
      ? 'Phiên đăng nhập đã hết hạn.'
      : 'Không thể xác định người dùng hiện tại.'
    conversationsError.value = membersError.value
    return
  }

  if (!componentMounted || version !== collaborationContextVersion) return
  try {
    await collaborationRealtime.start()
  } catch (error) {
    handleRealtimeState({
      state: COLLABORATION_REALTIME_STATES.ERROR,
      code: getCollaborationHubErrorCode(error)
    })
  }

  if (!componentMounted || version !== collaborationContextVersion) return
  await loadProjects({ force: forceProjects })
  if (!componentMounted || version !== collaborationContextVersion) return
  const linkedProjectId = `${route.query.projectId || ''}`
  if (linkedProjectId && projectOptions.value.some(project => project.id === linkedProjectId)) {
    activeProjectId.value = linkedProjectId
  } else if (!activeProjectId.value && projectOptions.value.length > 0) {
    activeProjectId.value = projectOptions.value[0].id
  } else if (currentTab.value === 'dm') {
    if (activeProjectId.value) {
      await loadDirectMessageUsers(activeProjectId.value)
    }
    await loadDirectConversations({ page: 1 })
  }
}

onMounted(() => {
  componentMounted = true
  registerRealtimeHandlers()
  void initializeCollaborationContext()
})

watch(() => route.query.tab, async (newTab) => {
  await leaveActiveRealtimeGroup()
  if (newTab === 'dm') {
    clearChannelSelection()
    clearDirectContext()
    if (activeProjectId.value) {
      await loadDirectMessageUsers(activeProjectId.value)
    }
    await loadDirectConversations({ page: 1 })
  } else {
    clearDirectContext()
    if (activeProjectId.value) {
      await loadChannels({ page: 1 })
    }
  }
})

watch(activeProjectId, async (projectId, previousProjectId) => {
  if (projectId === previousProjectId) return
  await leaveActiveRealtimeGroup()
  if (currentTab.value === 'dm') {
    clearDirectContext()
  } else {
    clearChannels()
  }
  if (!projectId) {
    clearScopedCurrentProjectId()
    if (currentTab.value === 'dm') {
      await loadDirectConversations({ page: 1 })
    }
    return
  }
  if (!projectOptions.value.some(project => project.id === projectId)) {
    activeProjectId.value = ''
    return
  }
  setScopedCurrentProjectId(projectId)
  if (currentTab.value === 'dm') {
    await Promise.all([
      loadDirectMessageUsers(projectId),
      loadDirectConversations({ page: 1 })
    ])
  } else {
    await loadChannels({ page: 1 })
  }
})

watch(() => authStore.token, async (token, previousToken) => {
  if (!componentMounted || token === previousToken) return
  collaborationContextVersion += 1
  await collaborationRealtime.stop()
  clearCollaborationState()
  receivedMentionNotificationIds.clear()
  window.dispatchEvent(new CustomEvent('collaboration-notifications-reset'))
  projectStore.allProjects = []
  setConnectionNotice('')
  if (token && componentMounted) {
    await initializeCollaborationContext({ forceProjects: true })
  }
})

watch(projectOptions, (projects) => {
  if (
    activeProjectId.value &&
    !projects.some(project => project.id === activeProjectId.value)
  ) {
    activeProjectId.value = ''
  }
})

onBeforeUnmount(() => {
  componentMounted = false
  collaborationContextVersion += 1
  realtimeUnsubscribers.splice(0).forEach(unsubscribe => unsubscribe())
  if (connectionNoticeTimer) {
    window.clearTimeout(connectionNoticeTimer)
    connectionNoticeTimer = null
  }
  cancelPendingMarkRead()
  removeAttachedFile()
  revokeMessageAttachmentUrls()
  void leaveActiveRealtimeGroup()
  createChannelAbortController.value?.abort()
  channelAbortController.value?.abort()
  memberAbortController.value?.abort()
  conversationAbortController.value?.abort()
  findConversationAbortController.value?.abort()
  messageAbortController.value?.abort()
  sendMessageAbortController.value?.abort()
  closeMentionMenu()
  channelRequestId += 1
  memberRequestId += 1
  conversationRequestId += 1
  messageRequestId += 1
  channels.value = []
  members.value = []
  directConversations.value = []
  activeMessages.value = []
  activeChat.value = null
  channelsError.value = ''
  historyError.value = ''
})
const addFriendActive = ref(false)
const searchFriendQuery = ref('')
const myFriendCode = ref('QUAN-9982')
const myInviteLink = computed(() => `http://localhost:5173/collaboration?invite=${myFriendCode.value}`)
const friendRequests = ref([])

const openAddFriendModal = () => {
  addFriendActive.value = true
}

const copyToClipboard = (text) => {
  navigator.clipboard.writeText(text).then(() => {
    ElMessage.success('Đã sao chép vào bộ nhớ tạm!')
  }).catch(() => {
    ElMessage.error('Không thể sao chép!')
  })
}

const formatTime = (timeStr) => {
  if (!timeStr) return ''
  try {
    const d = new Date(timeStr)
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  } catch (e) {
    return ''
  }
}

const showMembersSidebar = ref(true)
const toggleMembersSidebar = () => {
  showMembersSidebar.value = !showMembersSidebar.value
}
const activeServerMembers = computed(() => {
  if (!activeServer.value || !activeServer.value.members) return []
  return activeServer.value.members
})

const fileInputRef = ref(null)
const attachedFiles = ref([])
const allowedAttachmentExtensions = new Set(['png', 'jpg', 'jpeg', 'webp', 'pdf', 'txt', 'docx', 'xlsx'])
const maximumAttachmentBytes = 10 * 1024 * 1024

const triggerAttachment = () => {
  if (fileInputRef.value) {
    fileInputRef.value.click()
  }
}

const handleFileChange = (e) => {
  const candidates = Array.from(e.target.files || [])
  const remaining = 5 - attachedFiles.value.length
  if (candidates.length > remaining) {
    ElMessage.warning('Mỗi tin nhắn chỉ được đính kèm tối đa 5 file.')
  }
  candidates.slice(0, remaining).forEach((file) => {
    const extension = file.name.split('.').pop()?.toLowerCase() || ''
    if (!allowedAttachmentExtensions.has(extension)) {
      ElMessage.warning(`${file.name}: loại file không được hỗ trợ.`)
      return
    }
    if (file.size <= 0 || file.size > maximumAttachmentBytes) {
      ElMessage.warning(`${file.name}: file phải lớn hơn 0 B và không quá 10 MB.`)
      return
    }
    const previewUrl = isImageFile(file.name) ? URL.createObjectURL(file) : ''
    attachedFiles.value.push({
      id: typeof globalThis.crypto?.randomUUID === 'function' ? globalThis.crypto.randomUUID() : `${Date.now()}-${Math.random()}`,
      name: file.name,
      sizeBytes: file.size,
      previewUrl,
      rawFile: file
    })
  })
  e.target.value = ''
}

const removeAttachedFile = (fileId) => {
  const removed = fileId
    ? attachedFiles.value.filter(file => file.id === fileId)
    : attachedFiles.value
  removed.forEach((file) => {
    if (file.previewUrl) URL.revokeObjectURL(file.previewUrl)
  })
  if (fileId) {
    attachedFiles.value = attachedFiles.value.filter(file => file.id !== fileId)
  } else {
    attachedFiles.value = []
  }
}

const isImageFile = (fileName) => {
  if (!fileName) return false
  const ext = fileName.split('.').pop().toLowerCase()
  return ['jpg', 'jpeg', 'png', 'webp'].includes(ext)
}

const getFileIconClass = (fileName) => {
  if (!fileName) return 'fa-solid fa-file text-secondary'
  const ext = fileName.split('.').pop().toLowerCase()
  switch (ext) {
    case 'pdf': return 'fa-solid fa-file-pdf text-danger'
    case 'doc':
    case 'docx': return 'fa-solid fa-file-word text-primary'
    case 'xls':
    case 'xlsx': return 'fa-solid fa-file-excel text-success'
    case 'ppt':
    case 'pptx': return 'fa-solid fa-file-powerpoint text-warning'
    case 'zip':
    case 'rar':
    case '7z': return 'fa-solid fa-file-zipper text-warning'
    default: return 'fa-solid fa-file text-secondary'
  }
}

const emojiList = [
  '😀', '😃', '😄', '😁', '😆', '😅', '😂', '🤣', '😊', '😇',
  '🙂', '🙃', '😉', '😌', '😍', '🥰', '😘', '😗', '😙', '😚',
  '😋', '😛', '😝', '😜', '🤪', '🤨', '🧐', '🤓', '😎', '🥸',
  '🤩', '🥳', '😏', '😒', '😞', '😔', '😟', '😕', '🙁', '☹️',
  '😣', '😖', '😫', '😩', '🥺', '😢', '😭', '😤', '😠', '😡',
  '🤬', '🤯', '😳', '🥵', '🥶', '😱', '😨', '😰', '😥', '😓',
  '🤔', '💡', '🔥', '✨', '🎉', '🚀', '👀', '👍', '👎', '❤️'
]

const closeMentionMenu = () => {
  mentionAbortController.value?.abort()
  mentionAbortController.value = null
  mentionRequestId += 1
  if (mentionDebounceTimer) {
    window.clearTimeout(mentionDebounceTimer)
    mentionDebounceTimer = null
  }
  mentionMenuOpen.value = false
  mentionLoading.value = false
  mentionSuggestions.value = []
  mentionRange.value = null
  mentionActiveIndex.value = 0
}

const resetMentionComposer = () => {
  closeMentionMenu()
  selectedMentions.value = []
  previousComposerValue = newMessage.value
}

const reconcileMentionSpans = (oldValue, nextValue) => {
  let prefix = 0
  while (prefix < oldValue.length && prefix < nextValue.length && oldValue[prefix] === nextValue[prefix]) prefix += 1
  let suffix = 0
  while (
    suffix < oldValue.length - prefix &&
    suffix < nextValue.length - prefix &&
    oldValue[oldValue.length - 1 - suffix] === nextValue[nextValue.length - 1 - suffix]
  ) suffix += 1
  const oldEnd = oldValue.length - suffix
  const delta = nextValue.length - oldValue.length
  selectedMentions.value = selectedMentions.value.flatMap(mention => {
    const mentionEnd = mention.startIndex + mention.length
    if (mentionEnd <= prefix) return [mention]
    if (mention.startIndex >= oldEnd) {
      const shifted = { ...mention, startIndex: mention.startIndex + delta }
      return nextValue.slice(shifted.startIndex, shifted.startIndex + shifted.length) === shifted.displayText
        ? [shifted]
        : []
    }
    return []
  })
}

const loadMentionSuggestions = (query, range, channelId) => {
  if (mentionDebounceTimer) window.clearTimeout(mentionDebounceTimer)
  mentionMenuOpen.value = true
  mentionLoading.value = true
  mentionRange.value = range
  mentionDebounceTimer = window.setTimeout(async () => {
    mentionAbortController.value?.abort()
    const controller = new AbortController()
    mentionAbortController.value = controller
    const requestId = ++mentionRequestId
    try {
      const result = await collaborationApi.searchChannelMembers(channelId, query, {
        limit: 20,
        signal: controller.signal
      })
      if (
        requestId !== mentionRequestId ||
        activeChannel.value?.id !== channelId ||
        mentionRange.value?.start !== range.start
      ) return
      const selectedIds = new Set(selectedMentions.value.map(item => item.userId))
      mentionSuggestions.value = (Array.isArray(result) ? result : [])
        .filter(item => item?.userId && item?.displayName && !selectedIds.has(item.userId))
        .slice(0, 20)
      mentionActiveIndex.value = 0
    } catch (error) {
      if (!isCanceledRequest(error) && requestId === mentionRequestId) {
        mentionSuggestions.value = []
      }
    } finally {
      if (requestId === mentionRequestId) {
        mentionLoading.value = false
        mentionAbortController.value = null
      }
    }
  }, 180)
}

const handleComposerInput = (event) => {
  const nextValue = newMessage.value
  reconcileMentionSpans(previousComposerValue, nextValue)
  previousComposerValue = nextValue
  if (currentTab.value !== 'channel' || !activeChannel.value?.id) {
    closeMentionMenu()
    return
  }
  const caret = Number(event.target?.selectionStart ?? nextValue.length)
  const beforeCaret = nextValue.slice(0, caret)
  const match = beforeCaret.match(/(?:^|\s)@([^\s@]{0,100})$/u)
  if (!match || selectedMentions.value.length >= 20) {
    closeMentionMenu()
    return
  }
  const query = match[1]
  const start = caret - query.length - 1
  loadMentionSuggestions(query, { start, end: caret }, activeChannel.value.id)
}

const selectMention = async (member) => {
  const range = mentionRange.value
  if (!range || !member?.userId || selectedMentions.value.some(item => item.userId === member.userId)) return
  const token = `@${member.displayName}`
  const nextValue = `${newMessage.value.slice(0, range.start)}${token} ${newMessage.value.slice(range.end)}`
  const delta = nextValue.length - newMessage.value.length
  selectedMentions.value = selectedMentions.value.map(mention =>
    mention.startIndex >= range.end
      ? { ...mention, startIndex: mention.startIndex + delta }
      : mention
  )
  selectedMentions.value.push({
    userId: member.userId,
    displayText: token,
    startIndex: range.start,
    length: token.length
  })
  newMessage.value = nextValue
  previousComposerValue = nextValue
  closeMentionMenu()
  await nextTick()
  const caret = range.start + token.length + 1
  composerInput.value?.focus()
  composerInput.value?.setSelectionRange(caret, caret)
}

const handleComposerKeydown = (event) => {
  if (mentionMenuOpen.value) {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      const count = mentionSuggestions.value.length
      if (count) {
        const direction = event.key === 'ArrowDown' ? 1 : -1
        mentionActiveIndex.value = (mentionActiveIndex.value + direction + count) % count
      }
      return
    }
    if (event.key === 'Enter' || event.key === 'Tab') {
      event.preventDefault()
      const member = mentionSuggestions.value[mentionActiveIndex.value]
      if (member) void selectMention(member)
      return
    }
    if (event.key === 'Escape') {
      event.preventDefault()
      closeMentionMenu()
      return
    }
  }
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault()
    void sendMessage()
  }
}

const insertEmoji = (emoji) => {
  newMessage.value += emoji
  previousComposerValue = newMessage.value
}

const sendDirectMessage = async () => {
  if (sendingMessage.value || !activeDirectConversation.value) return
  const content = newMessage.value
    .replace(/\r\n/g, '\n')
    .replace(/\r/g, '\n')
    .trim()
  if (!content && attachedFiles.value.length === 0) return
  if (content.length > 4000) {
    ElMessage.warning('Tin nhắn không được vượt quá 4.000 ký tự.')
    return
  }

  const conversation = activeDirectConversation.value
  const shouldScroll = isNearMessageBottom()
  const controller = new AbortController()
  sendMessageAbortController.value = controller
  sendingMessage.value = true
  try {
    const result = await collaborationApi.sendDirectMessage(
      conversation.id,
      content,
      {
        signal: controller.signal,
        files: attachedFiles.value.map(file => file.rawFile)
      }
    )
    if (activeDirectConversation.value?.id !== conversation.id) return
    const message = mapDirectMessage(result, conversation.id)
    const messageAlreadyPresent = activeMessages.value.some(
      item => item.messageId === message.messageId
    )
    activeMessages.value = mergeMessages(activeMessages.value, [message])
    if (!messageAlreadyPresent) {
      messagePagination.value.totalCount += 1
    }
    newMessage.value = ''
    removeAttachedFile()
    await loadDirectConversations({ page: 1, selectFirst: false })
    await nextTick()
    if (shouldScroll) scrollToBottom()
  } catch (error) {
    if (isCanceledRequest(error)) return
    const status = error?.response?.status
    if (status === 401) {
      clearCollaborationState()
      ElMessage.error('Phiên đăng nhập đã hết hạn.')
    } else if (status === 403 || status === 404) {
      clearDirectSelection({ clearComposer: false })
      directConversations.value = directConversations.value.filter(
        item => item.id !== conversation.id
      )
      ElMessage.error('Cuộc trò chuyện không còn khả dụng hoặc bạn không còn quyền gửi.')
      await loadDirectConversations({ page: 1, selectFirst: false })
    } else if (status === 400) {
      ElMessage.error(apiErrorMessage(error, 'Nội dung tin nhắn không hợp lệ.'))
    } else {
      ElMessage.error(apiErrorMessage(error, 'Không thể gửi tin nhắn. Nội dung vẫn được giữ lại.'))
    }
  } finally {
    if (sendMessageAbortController.value === controller) {
      sendMessageAbortController.value = null
      sendingMessage.value = false
    }
  }
}

const sendChannelMessage = async () => {
  if (sendingMessage.value || !activeChannel.value?.canSend) return
  const normalizedInput = newMessage.value
    .replace(/\r\n/g, '\n')
    .replace(/\r/g, '\n')
  const leadingWhitespace = normalizedInput.length - normalizedInput.trimStart().length
  const content = normalizedInput.trim()
  if (!content && attachedFiles.value.length === 0) return
  if (content.length > 4000) {
    ElMessage.warning('Tin nhắn không được vượt quá 4.000 ký tự.')
    return
  }

  const channel = activeChannel.value
  const mentions = selectedMentions.value.flatMap(mention => {
    const adjusted = { ...mention, startIndex: mention.startIndex - leadingWhitespace }
    return adjusted.startIndex >= 0 &&
      content.slice(adjusted.startIndex, adjusted.startIndex + adjusted.length) === adjusted.displayText
      ? [{ userId: adjusted.userId, startIndex: adjusted.startIndex, length: adjusted.length }]
      : []
  })
  const controller = new AbortController()
  sendMessageAbortController.value = controller
  sendingMessage.value = true
  try {
    const result = await collaborationApi.sendChannelMessage(
      channel.id,
      {
        content,
        mentions,
        files: attachedFiles.value.map(file => file.rawFile)
      },
      { signal: controller.signal }
    )
    if (activeChannel.value?.id !== channel.id) return
    const message = mapChannelMessage(result, channel.id)
    const messageAlreadyPresent = activeMessages.value.some(
      item => item.messageId === message.messageId
    )
    activeMessages.value = mergeMessages(activeMessages.value, [message])
    if (!messageAlreadyPresent) {
      messagePagination.value.totalCount += 1
    }
    newMessage.value = ''
    resetMentionComposer()
    removeAttachedFile()
    await nextTick()
    scrollToBottom()
  } catch (error) {
    if (isCanceledRequest(error)) return
    const status = error?.response?.status
    if (status === 401) {
      clearCollaborationState()
    } else if (status === 403 || status === 404) {
      clearChannelSelection()
      channels.value = channels.value.filter(item => item.id !== channel.id)
      ElMessage.error('Channel không còn khả dụng hoặc bạn không còn quyền gửi.')
      if (status === 404) await loadChannels({ page: 1 })
    } else {
      ElMessage.error(apiErrorMessage(error, 'Không thể gửi tin nhắn. Nội dung vẫn được giữ lại.'))
    }
  } finally {
    if (sendMessageAbortController.value === controller) {
      sendMessageAbortController.value = null
    }
    sendingMessage.value = false
  }
}

const sendMessage = () => {
  if (composerDisabled.value) return
  return currentTab.value === 'channel'
    ? sendChannelMessage()
    : sendDirectMessage()
}

const selectDirectRecipient = async (participantUserId) => {
  if (
    findingConversation.value ||
    !participantUserId ||
    !members.value.some(member => member.id === participantUserId)
  ) {
    return
  }

  await leaveActiveRealtimeGroup()
  clearDirectSelection()
  selectedRecipientId.value = participantUserId
  const controller = new AbortController()
  findConversationAbortController.value = controller
  findingConversation.value = true
  try {
    const result = await collaborationApi.findOrCreateDirectConversation(
      participantUserId,
      { signal: controller.signal }
    )
    const conversation = mapDirectConversation(result)
    if (conversation.participantUserId !== participantUserId) {
      throw new Error('Direct Message participant response is out of scope.')
    }
    directConversations.value = [
      conversation,
      ...directConversations.value.filter(item => item.id !== conversation.id)
    ]
    conversationPagination.value.totalCount = Math.max(
      conversationPagination.value.totalCount,
      directConversations.value.length
    )
    await selectChat(conversation, 'dm')
    await loadDirectConversations({ page: 1, selectFirst: false })
  } catch (error) {
    if (isCanceledRequest(error)) return
    selectedRecipientId.value = ''
    const status = error?.response?.status
    if (status === 401) {
      clearCollaborationState()
      ElMessage.error('Phiên đăng nhập đã hết hạn.')
    } else if (status === 400) {
      ElMessage.error('Người nhận không hợp lệ.')
    } else if (status === 403 || status === 404) {
      ElMessage.error('Người dùng không tồn tại hoặc nằm ngoài phạm vi cộng tác.')
      await loadDirectMessageUsers(activeProjectId.value)
    } else if (status === 409) {
      await loadDirectConversations({ page: 1 })
      ElMessage.error('Cuộc trò chuyện vừa thay đổi. Danh sách đã được làm mới.')
    } else {
      ElMessage.error(apiErrorMessage(error, 'Không thể mở cuộc trò chuyện.'))
    }
  } finally {
    if (findConversationAbortController.value === controller) {
      findConversationAbortController.value = null
      findingConversation.value = false
    }
  }
}

const selectChat = async (item, type) => {
  if (type === 'channel') {
    if (
      !item?.id ||
      item.projectId !== activeProjectId.value ||
      !channels.value.some(channel => channel.id === item.id)
    ) {
      return
    }
  } else if (
    !item?.id ||
    !item?.participantUserId ||
    !directConversations.value.some(conversation => conversation.id === item.id)
  ) {
    return
  }

  const previousChat = activeChat.value
  await leaveActiveRealtimeGroup(previousChat)
  clearMessageHistory()
  removeAttachedFile()
  resetMentionComposer()
  const selectionId = chatSelectionId
  activeChat.value = {
    id: item.id,
    name: item.name,
    type: type,
    desc: item.desc || (type === 'dm' ? `Cuộc hội thoại trực tiếp với ${item.name}` : ''),
    avatar: item.avatar || '',
    participantUserId: item.participantUserId || null,
    projectId: item.projectId || null,
    workspaceId: item.workspaceId || null,
    canRead: type === 'channel' ? item.canRead : true,
    canSend: type === 'channel' ? item.canSend : true,
    canManage: type === 'channel' ? item.canManage : false,
    unreadCount: item.unreadCount || 0,
    lastReadMessageId: item.lastReadMessageId || null
  }

  await joinRealtimeForChat(activeChat.value)
  if (
    selectionId !== chatSelectionId ||
    activeChat.value?.id !== item.id ||
    activeChat.value?.type !== type
  ) {
    return
  }

  if (type === 'dm') {
    selectedRecipientId.value = item.participantUserId
    await loadDirectHistory(activeChat.value, { page: 1 })
  } else {
    await loadChannelHistory(activeChat.value, { page: 1 })
  }

}

const scrollToBottom = () => {
  if (messageThread.value) {
    messageThread.value.scrollTop = messageThread.value.scrollHeight
  }
}

const isNearMessageBottom = () => {
  if (!messageThread.value) return true
  const distance =
    messageThread.value.scrollHeight -
    messageThread.value.scrollTop -
    messageThread.value.clientHeight
  return distance <= 120
}

// Call simulation helpers
const outgoingCallActive = ref(false)
const incomingCallActive = ref(false)
const callingPartnerName = ref('')
const callingPartnerAvatar = ref('')

const startVoiceCall = () => {
  if (!activeChat.value || activeChat.value.type !== 'dm') return
  isCallCameraOn.value = false
  isRemoteCameraOn.value = false
  callingPartnerName.value = activeChat.value.name
  callingPartnerAvatar.value = activeChat.value.avatar || ''
  outgoingCallActive.value = true
}

const startVideoCall = () => {
  if (!activeChat.value || activeChat.value.type !== 'dm') return
  isCallCameraOn.value = true
  isRemoteCameraOn.value = true
  callingPartnerName.value = activeChat.value.name
  callingPartnerAvatar.value = activeChat.value.avatar || ''
  outgoingCallActive.value = true
}

const cancelOutgoingCall = () => {
  outgoingCallActive.value = false
  ElMessage.info('Đã hủy cuộc gọi.')
}

const partnerAcceptCall = () => {
  outgoingCallActive.value = false
  videoCallActive.value = true
  ElMessage.success('Cuộc gọi đã được kết nối!')
}

const partnerDeclineCall = () => {
  outgoingCallActive.value = false
  ElMessage.error(`${callingPartnerName.value} đã từ chối cuộc gọi.`)
}

const acceptIncomingCall = () => {
  incomingCallActive.value = false
  videoCallActive.value = true
  ElMessage.success('Đã chấp nhận cuộc gọi!')
}

const declineIncomingCall = () => {
  incomingCallActive.value = false
  ElMessage.info('Đã từ chối cuộc gọi.')
}

// Friend request actions
const sendFriendRequest = () => {
  if (!searchFriendQuery.value.trim()) {
    ElMessage.warning('Vui lòng nhập thông tin kết bạn!')
    return
  }
  ElMessage.success(`Đã gửi yêu cầu kết bạn tới "${searchFriendQuery.value.trim()}"!`)
  searchFriendQuery.value = ''
}

const acceptFriend = (req) => {
  friendRequests.value = friendRequests.value.filter(r => r.id !== req.id)
  ElMessage.success(`Đã đồng ý kết bạn với ${req.name}!`)
}

const declineFriend = (req) => {
  friendRequests.value = friendRequests.value.filter(r => r.id !== req.id)
  ElMessage.info(`Đã từ chối yêu cầu kết bạn của ${req.name}.`)
}

// Server invite helpers
const inviteServerActive = ref(false)
const inviteableUsers = ref([])

const openInviteServerModal = () => {
  if (!activeServer.value) return
  // Find friends who are not currently members of the server
  const currentMemberIds = (activeServer.value.members || []).map(m => m.id)
  inviteableUsers.value = members.value
    .filter(m => !currentMemberIds.includes(m.id))
    .map(m => ({ ...m, checked: false }))
  
  inviteServerActive.value = true
}

const confirmInviteToServer = () => {
  if (!activeServer.value) return
  const selected = inviteableUsers.value.filter(u => u.checked)
  if (selected.length === 0) return
  
  if (!activeServer.value.members) {
    activeServer.value.members = []
  }
  
  selected.forEach(u => {
    activeServer.value.members.push({
      id: u.id,
      name: u.name,
      avatar: u.avatar
    })
  })
  
  saveServers()
  inviteServerActive.value = false
  ElMessage.success(`Đã thêm ${selected.length} thành viên vào Server!`)
}


// Simulate receiving call after 15s if in DM
onMounted(() => {
  setTimeout(() => {
    if (activeChat.value && activeChat.value.type === 'dm' && !videoCallActive.value && !outgoingCallActive.value) {
      callingPartnerName.value = activeChat.value.name
      callingPartnerAvatar.value = activeChat.value.avatar || ''
      incomingCallActive.value = true
    }
  }, 15000)
})

import { onUnmounted } from 'vue'
onUnmounted(() => {
  stopLocalCamera()
  stopScreenShare()
})</script>


<style scoped>
.server-bar {
  width: 72px;
  background-color: #1e1f22;
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 12px 0;
  gap: 8px;
  border-right: 1px solid var(--color-border);
  flex-shrink: 0;
  overflow-y: auto;
}

.server-icon-wrapper {
  position: relative;
  width: 48px;
  height: 48px;
  margin: 2px 0;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.server-icon {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  color: #ffffff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  font-weight: 700;
  transition: all 0.2s cubic-bezier(0.175, 0.885, 0.32, 1.275);
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.12);
  user-select: none;
  background-color: var(--color-accent);
}

.project-scope-select {
  width: 100%;
  min-height: 36px;
  margin-bottom: 12px;
  padding: 0 10px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface);
  color: var(--color-text-primary);
}

.channel-state,
.history-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 18px 10px;
  color: var(--color-text-muted);
  font-size: 12px;
  line-height: 1.45;
  text-align: center;
}

.history-state {
  min-height: 120px;
  margin: auto;
}

.channel-state-error,
.history-state-error {
  color: var(--color-danger);
}

.state-action {
  min-height: 30px;
  padding: 5px 10px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
  cursor: pointer;
}

.load-more-action,
.load-older-action {
  width: 100%;
}

.load-older-action {
  align-self: center;
  width: auto;
}

.state-action:disabled,
.add-btn-small:disabled,
.btn-send:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

.server-icon-wrapper:hover .server-icon {
  border-radius: 16px;
  transform: scale(1.04);
  filter: brightness(1.1);
}

.server-icon-wrapper.active .server-icon {
  border-radius: 16px;
  transform: scale(1.02);
  box-shadow: 0 0 12px var(--color-primary);
}

.active-indicator {
  position: absolute;
  left: 0;
  width: 4px;
  height: 20px;
  background-color: #ffffff;
  border-radius: 0 4px 4px 0;
  transform: scaleX(0);
  transition: all 0.2s ease;
  transform-origin: left center;
}

.server-icon-wrapper:hover .active-indicator {
  transform: scaleX(1) scaleY(0.7);
}

.server-icon-wrapper.active .active-indicator {
  transform: scaleX(1) scaleY(1.4);
  background-color: var(--color-primary);
  height: 20px;
}

.add-server-circle-btn {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  border: 1px dashed var(--color-border);
  background: transparent;
  color: var(--color-text-secondary);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  cursor: pointer;
  transition: all 0.2s;
  margin-top: 4px;
}

.add-server-circle-btn:hover {
  border-radius: 16px;
  background-color: var(--color-success);
  border-color: transparent;
  color: #ffffff;
  transform: translateY(-2px);
}

.add-btn-small {
  background: transparent;
  border: none;
  color: var(--color-text-muted);
  cursor: pointer;
  width: 20px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s;
}

.add-btn-small:hover {
  background-color: rgba(255, 255, 255, 0.08);
  color: var(--color-text-primary);
}

.btn-danger-custom {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  height: 34px;
  padding: 0 16px;
  border: 1.5px solid rgba(239, 68, 68, 0.6);
  border-radius: 7px;
  background: rgba(239, 68, 68, 0.08);
  color: #f87171;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
  white-space: nowrap;
}

.btn-danger-custom:hover {
  background: rgba(239, 68, 68, 0.18);
  border-color: #ef4444;
  color: #fca5a5;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(239, 68, 68, 0.2);
}

.btn-danger-custom:active {
  transform: translateY(0);
  background: rgba(239, 68, 68, 0.25);
}

.btn-cancel-custom {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 34px;
  padding: 0 16px;
  border: 1.5px solid var(--color-border);
  border-radius: 7px;
  background: rgba(255, 255, 255, 0.04);
  color: var(--color-text-secondary);
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  white-space: nowrap;
}

.btn-cancel-custom:hover {
  background: rgba(255, 255, 255, 0.08);
  border-color: rgba(255, 255, 255, 0.2);
  color: var(--color-text-primary);
}

.btn-primary-custom {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  height: 34px;
  padding: 0 18px;
  border: none;
  border-radius: 7px;
  background: linear-gradient(135deg, var(--color-primary, #6366f1), #4f46e5);
  color: #ffffff;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
  white-space: nowrap;
  box-shadow: 0 4px 12px rgba(99, 102, 241, 0.25);
}

.btn-primary-custom:hover {
  background: linear-gradient(135deg, #818cf8, #6366f1);
  transform: translateY(-1px);
  box-shadow: 0 6px 16px rgba(99, 102, 241, 0.35);
}

.btn-primary-custom:active {
  transform: translateY(0);
  box-shadow: 0 2px 6px rgba(99, 102, 241, 0.2);
}

.clickable-header {
  cursor: pointer;
  padding: 8px 12px;
  border-radius: 8px;
  transition: all 0.2s;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.clickable-header:hover {
  background-color: rgba(255, 255, 255, 0.06);
}

.hover-settings-icon:hover {
  color: var(--color-primary) !important;
}

.selected-color-swatch {
  border-color: #ffffff !important;
  transform: scale(1.1);
  box-shadow: 0 0 8px rgba(255, 255, 255, 0.5);
}

.members-sidebar-right {
  width: 220px;
  border-left: 1px solid var(--color-border);
  background-color: color-mix(in srgb, var(--sa-sidebar) 70%, transparent);
  display: flex;
  flex-direction: column;
  padding: 14px;
  flex-shrink: 0;
}
.invite-server-btn-sidebar {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  width: 100%;
  padding: 8px;
  background-color: var(--color-primary);
  color: white;
  border: none;
  border-radius: var(--radius-button);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
  margin-bottom: 14px;
}
.invite-server-btn-sidebar:hover {
  background-color: var(--color-primary-hover);
  transform: translateY(-1px);
}
.invite-icon-btn {
  background: transparent;
  border: none;
  color: var(--color-primary);
  cursor: pointer;
  transition: color 0.2s;
}
.invite-icon-btn:hover {
  color: var(--color-primary-hover);
}
.member-list-scrollable {
  display: flex;
  flex-direction: column;
  gap: 8px;
  overflow-y: auto;
  flex: 1;
}
.member-sidebar-card {
  display: flex;
  align-items: center;
  padding: 6px 8px;
  border-radius: 6px;
  cursor: pointer;
  transition: background 0.2s;
}
.member-sidebar-card:hover {
  background-color: rgba(255,255,255,0.04);
}

.chat-container {
  display: flex;
  width: min(100% - 44px, 1280px);
  height: calc(100vh - 132px);
  margin: 22px auto;
  background-color: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 14px;
  overflow: hidden;
  box-shadow: 0 18px 46px color-mix(in srgb, #020617 12%, transparent);
}

.chat-sidebar {
  width: 248px;
  border-right: 1px solid var(--color-border);
  background-color: var(--sa-sidebar);
  display: flex;
  flex-direction: column;
  padding: 14px;
}
.sidebar-header {
  margin-bottom: 14px;
}
.sidebar-section {
  display: flex;
  flex-direction: column;
  margin-bottom: 14px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-card);
  padding: 12px;
  background-color: rgba(255, 255, 255, 0.01);
}

.section-title {
  font-size: 11px;
  font-weight: 700;
  color: var(--color-text-muted);
  letter-spacing: 0.08em;
  margin-bottom: 12px;
  text-transform: uppercase;
}

.conversation-time {
  flex: 0 0 auto;
  margin-left: auto;
  padding-left: 6px;
  color: var(--color-text-muted);
  font-size: 10px;
}

.collaboration-unread-badge {
  flex: 0 0 auto;
  min-width: 20px;
  height: 20px;
  padding: 0 6px;
  border-radius: 999px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: var(--color-accent);
  color: #ffffff;
  font-size: 10px;
  font-weight: 700;
  line-height: 1;
  box-shadow: 0 0 0 2px var(--sa-sidebar);
}

.list-item.active .collaboration-unread-badge {
  background: var(--color-text-primary);
  color: var(--color-surface);
}

.section-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
  border: none !important;
  padding: 0 !important;
  background-color: transparent !important;
}

.list-item {
  display: flex;
  align-items: center;
  padding: 7px 10px;
  border: none;
  border-radius: var(--radius-button);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s;
  gap: 10px;
}

.list-item:hover, .list-item.active {
  background-color: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.list-item.active {
  font-weight: 600;
  background-color: var(--sa-primary-soft);
  color: var(--color-accent);
}
.item-icon {
  margin-right: 8px;
  font-weight: bold;
}
.avatar-status-wrapper {
  position: relative;
  display: inline-block;
}

.status-dot {
  position: absolute;
  bottom: 0;
  right: 0;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  border: 1.5px solid var(--color-surface);
}

.status-dot.online { background-color: var(--color-success); }
.status-dot.away { background-color: var(--color-warning); }
.status-dot.offline { background-color: var(--color-text-muted); }

.chat-main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  background-color: var(--color-surface);
}

.chat-header {
  min-height: 58px;
  border-bottom: 1px solid var(--color-border);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 16px;
}

.connection-notice {
  flex: 0 0 auto;
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 7px 16px;
  border-bottom: 1px solid var(--color-border);
  background: color-mix(in srgb, var(--color-warning) 10%, var(--color-surface));
  color: var(--color-text-secondary);
  font-size: 12px;
  line-height: 1.35;
  overflow-wrap: anywhere;
}

.connection-notice i {
  flex: 0 0 14px;
  width: 14px;
  text-align: center;
}

.connection-notice.is-connected {
  background: color-mix(in srgb, var(--color-success) 10%, var(--color-surface));
}

.connection-notice.is-error {
  color: var(--color-danger);
  background: color-mix(in srgb, var(--color-danger) 8%, var(--color-surface));
}

.active-info {
  display: flex;
  min-width: 0;
  align-items: center;
  gap: 8px;
}

.active-icon {
  font-size: 20px;
  font-weight: bold;
  color: var(--color-text-muted);
}

.header-actions {
  display: flex;
  gap: 8px;
}

.action-btn {
  background: transparent;
  border: none;
  color: var(--color-text-secondary);
  cursor: pointer;
  padding: 6px;
  border-radius: var(--radius-button);
  transition: all 0.2s;
}

.action-btn:hover {
  background-color: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.messages-thread {
  flex: 1;
  padding: 16px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.message-card {
  display: flex;
  gap: 12px;
  max-width: 80%;
  align-self: flex-start;
}

.message-card.mine {
  align-self: flex-end;
  flex-direction: row-reverse;
}

.message-body {
  display: flex;
  flex-direction: column;
}

.message-card.mine .message-body {
  align-items: flex-end;
}

.message-meta {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin-bottom: 4px;
}

.sender-name {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.send-time {
  font-size: 10px;
  color: var(--color-text-muted);
}

.message-content {
  background-color: var(--color-surface-hover);
  padding: 9px 12px;
  border-radius: 12px;
  border-top-left-radius: 0;
  color: var(--color-text-primary);
  font-size: 14px;
  border: 1px solid var(--color-border);
}

.message-content p {
  margin: 0;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}

.message-card.mine .message-content {
  background-color: var(--color-accent);
  color: white;
  border-top-left-radius: 12px;
  border-top-right-radius: 0;
  border: none;
}

.attachment-preview {
  display: flex;
  align-items: center;
  background-color: var(--color-surface);
  border: 1px solid var(--color-border);
  padding: 8px 12px;
  border-radius: 8px;
  margin-top: 8px;
  min-width: 200px;
}

.message-card.mine .attachment-preview {
  background-color: rgba(255, 255, 255, 0.15);
  border-color: transparent;
}

.message-card.mine .attachment-preview .text-primary {
  color: #fff;
}

.chat-input-area {
  padding: 12px 16px;
  border-top: 1px solid var(--color-border);
  background-color: var(--color-surface);
}

.input-actions-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.input-form {
  display: flex;
  gap: 8px;
}

.chat-input {
  border: 2px solid var(--color-border) !important;
  border-radius: 8px !important;
  min-height: 40px !important;
  max-height: 112px;
  padding: 9px 12px !important;
  line-height: 20px;
  resize: vertical;
  overflow-y: auto;
  font: inherit;
}

.character-counter {
  margin-top: 4px;
  color: var(--color-text-muted);
  font-size: 11px;
  text-align: right;
}

.btn-send {
  width: 40px;
  height: 40px;
  border: none;
  background-color: var(--color-accent);
  color: white;
  border-radius: 8px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color 0.2s;
}

.btn-send:hover {
  background-color: var(--color-accent-hover);
}

.btn-send:disabled:hover {
  background-color: var(--color-accent);
}

.video-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 20px;
  height: 420px;
  margin-bottom: 10px;
}

.video-feed {
  background-color: #0c111d;
  border-radius: 12px;
  overflow: hidden;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 2px solid #273549;
  position: relative;
  transition: all 0.3s ease;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.3);
}

.video-feed.camera-active {
  border-color: #38bdf8;
  box-shadow: 0 0 20px rgba(56, 189, 248, 0.25);
}

.feed-placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
  z-index: 10;
}

.camera-stream-active {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.simulated-camera-bg {
  width: 100%;
  height: 100%;
  background: linear-gradient(135deg, #1e1b4b, #312e81, #1e1b4b);
  background-size: 400% 400%;
  animation: cameraStreamGradient 8s ease infinite;
  position: relative;
  overflow: hidden;
}

.simulated-camera-bg.remote-bg {
  background: linear-gradient(135deg, #064e3b, #065f46, #064e3b);
  background-size: 400% 400%;
  animation: cameraStreamGradient 8s ease infinite;
}

@keyframes cameraStreamGradient {
  0% { background-position: 0% 50%; }
  50% { background-position: 100% 50%; }
  100% { background-position: 0% 50%; }
}

.camera-scanner {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 4px;
  background: linear-gradient(to bottom, rgba(56, 189, 248, 0.4), rgba(56, 189, 248, 0));
  animation: scanlines 4s linear infinite;
  pointer-events: none;
}

@keyframes scanlines {
  0% { transform: translateY(0); }
  100% { transform: translateY(420px); }
}

.feed-overlay {
  position: absolute;
  bottom: 12px;
  left: 12px;
  right: 12px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  z-index: 20;
}

.feed-name {
  color: #f8fafc;
  font-size: 13px;
  font-weight: 600;
  background-color: rgba(15, 23, 42, 0.75);
  padding: 4px 10px;
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.08);
  backdrop-filter: blur(4px);
}

.badge-live {
  background-color: rgba(239, 68, 68, 0.85);
  color: #ffffff;
  font-size: 11px;
  font-weight: 700;
  padding: 4px 8px;
  border-radius: 4px;
  letter-spacing: 0.05em;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border: 1px solid rgba(239, 68, 68, 0.4);
  backdrop-filter: blur(4px);
}

/* Call Control Buttons */
.call-controls-container {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 16px;
  padding: 10px 0;
}

.call-control-circle-btn {
  width: 50px;
  height: 50px;
  border-radius: 50%;
  background-color: #273549;
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: #e2e8f0;
  font-size: 18px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.25);
}

.call-control-circle-btn:hover {
  background-color: #384a62;
  transform: translateY(-2px);
  box-shadow: 0 6px 14px rgba(56, 189, 248, 0.15);
}

.call-control-circle-btn.inactive {
  background-color: #ea580c !important;
  color: #ffffff !important;
  border-color: rgba(234, 88, 12, 0.3) !important;
}

.call-control-circle-btn.inactive:hover {
  background-color: #d97706 !important;
}

.call-control-circle-btn.hang-up {
  background-color: #dc2626 !important;
  color: #ffffff !important;
  border-color: rgba(220, 38, 38, 0.3) !important;
}

.call-control-circle-btn.hang-up:hover {
  background-color: #b91c1c !important;
  box-shadow: 0 6px 16px rgba(220, 38, 38, 0.4);
}

@media (max-width: 900px) {
  .chat-container {
    width: calc(100% - 24px);
    height: calc(100vh - 112px);
    margin: 12px auto;
  }

  .chat-sidebar {
    width: 210px;
  }
}

@media (max-width: 720px) {
  .chat-container {
    flex-direction: column;
    height: auto;
    min-height: calc(100vh - 112px);
  }

  .server-bar {
    width: 100%;
    height: 64px;
    flex-direction: row;
    justify-content: flex-start;
    padding: 6px 10px;
    overflow-x: auto;
    overflow-y: hidden;
    border-right: 0;
    border-bottom: 1px solid var(--color-border);
  }

  .server-icon-wrapper,
  .server-icon {
    width: 42px;
    height: 42px;
    flex: 0 0 42px;
  }

  .chat-sidebar {
    width: 100%;
    max-height: 240px;
    border-right: 0;
    border-bottom: 1px solid var(--color-border);
  }

  .messages-thread {
    min-height: 420px;
  }

  .chat-header,
  .connection-notice {
    padding-left: 12px;
    padding-right: 12px;
  }

  .message-card {
    max-width: 92%;
  }

  .chat-input-area {
    position: sticky;
    bottom: 0;
    z-index: 2;
  }
}
</style>

<style>
.add-friend-dialog {
  background-color: #111c2d !important;
  border: 1px solid rgba(56, 189, 248, 0.15) !important;
  border-radius: 12px !important;
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.5), 0 10px 10px -5px rgba(0, 0, 0, 0.4) !important;
  overflow: hidden !important;
}

.add-friend-dialog .el-dialog__header {
  margin-right: 0 !important;
  padding: 20px 24px 16px !important;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06) !important;
  background-color: #0b1523 !important;
}

.add-friend-dialog .el-dialog__headerbtn {
  top: 20px !important;
  right: 20px !important;
  margin-top: 0 !important;
}

.add-friend-dialog .el-dialog__title {
  display: flex !important;
  align-items: center !important;
  gap: 10px !important;
}

.add-friend-dialog .dialog-header span {
  font-size: 15px !important;
  font-weight: 600 !important;
  color: #e2e8f0 !important;
}

.add-friend-dialog .dialog-header i {
  color: #38bdf8 !important;
  text-shadow: 0 0 10px rgba(56, 189, 248, 0.5) !important;
}

.add-friend-dialog .el-dialog__body {
  padding: 24px !important;
  background-color: #111c2d !important;
}

.add-friend-dialog .my-invite-card {
  background: linear-gradient(135deg, rgba(56, 189, 248, 0.06), rgba(56, 189, 248, 0.01)) !important;
  border: 1px solid rgba(56, 189, 248, 0.15) !important;
  border-radius: 8px !important;
  padding: 16px !important;
}

.add-friend-dialog .info-row {
  display: flex !important;
  align-items: center !important;
  justify-content: space-between !important;
  font-size: 13px !important;
  gap: 12px !important;
}

.add-friend-dialog .info-label {
  color: #9fb0c8 !important;
  font-weight: 500 !important;
  width: 90px !important;
  flex-shrink: 0 !important;
}

.add-friend-dialog .info-value-wrapper {
  display: flex !important;
  align-items: center !important;
  gap: 10px !important;
  flex: 1 !important;
  min-width: 0 !important;
  justify-content: flex-end !important;
}

.add-friend-dialog .info-code {
  background-color: rgba(56, 189, 248, 0.12) !important;
  color: #38bdf8 !important;
  padding: 3px 10px !important;
  border-radius: 4px !important;
  font-family: monospace !important;
  font-weight: 600 !important;
  border: 1px solid rgba(56, 189, 248, 0.2) !important;
  font-size: 13px !important;
}

.add-friend-dialog .info-link {
  color: #e2e8f0 !important;
  font-size: 12px !important;
  text-decoration: none !important;
  border-bottom: 1px dashed rgba(255, 255, 255, 0.2) !important;
  flex: 1 !important;
  min-width: 0 !important;
  white-space: nowrap !important;
  overflow: hidden !important;
  text-overflow: ellipsis !important;
  text-align: right !important;
}

.add-friend-dialog .copy-btn-link {
  background: transparent !important;
  border: none !important;
  color: #38bdf8 !important;
  cursor: pointer !important;
  font-size: 12px !important;
  font-weight: 600 !important;
  padding: 4px 8px !important;
  border-radius: 4px !important;
  display: inline-flex !important;
  align-items: center !important;
  gap: 6px !important;
  white-space: nowrap !important;
  flex-shrink: 0 !important;
  transition: all 0.2s ease !important;
  box-shadow: none !important;
}

.add-friend-dialog .copy-btn-link:hover {
  background-color: rgba(56, 189, 248, 0.12) !important;
  color: #7dd3fc !important;
}

.add-friend-dialog .custom-friend-input {
  flex: 1 !important;
  border: 1px solid rgba(255, 255, 255, 0.1) !important;
  border-radius: 6px !important;
  background-color: #0b131f !important;
  padding: 0 14px !important;
  color: #f8fafc !important;
  font-size: 13px !important;
  height: 38px !important;
  outline: none !important;
  transition: all 0.2s ease !important;
}

.add-friend-dialog .custom-friend-input:focus {
  border-color: #38bdf8 !important;
  box-shadow: 0 0 0 3px rgba(56, 189, 248, 0.15) !important;
  background-color: #070d16 !important;
}

.add-friend-dialog .field-label {
  display: block !important;
  font-size: 10px !important;
  font-weight: 700 !important;
  text-transform: uppercase !important;
  letter-spacing: 0.08em !important;
  color: #64748b !important;
  margin-bottom: 10px !important;
}

.add-friend-dialog .requests-list {
  display: flex !important;
  flex-direction: column !important;
  gap: 10px !important;
}

.add-friend-dialog .request-item {
  display: flex !important;
  align-items: center !important;
  padding: 12px 16px !important;
  border: 1px solid rgba(255, 255, 255, 0.05) !important;
  border-radius: 8px !important;
  background-color: rgba(255, 255, 255, 0.02) !important;
  gap: 12px !important;
  transition: all 0.2s ease !important;
}

.add-friend-dialog .request-item:hover {
  background-color: rgba(255, 255, 255, 0.04) !important;
  border-color: rgba(255, 255, 255, 0.08) !important;
}

.add-friend-dialog .btn-save {
  display: inline-flex !important;
  align-items: center !important;
  justify-content: center !important;
  line-height: 1 !important;
  background: linear-gradient(135deg, #0ea5e9, #0284c7) !important;
  border: none !important;
  color: #ffffff !important;
  font-weight: 600 !important;
  font-size: 13px !important;
  height: 38px !important;
  padding: 0 20px !important;
  border-radius: 6px !important;
  cursor: pointer !important;
  transition: all 0.2s ease !important;
  white-space: nowrap !important;
  box-shadow: 0 4px 12px rgba(14, 165, 233, 0.2) !important;
}

.add-friend-dialog .btn-save:hover {
  background: linear-gradient(135deg, #38bdf8, #0ea5e9) !important;
  transform: translateY(-1px) !important;
  box-shadow: 0 6px 16px rgba(56, 189, 248, 0.3) !important;
}

.add-friend-dialog .btn-action-accept {
  display: inline-flex !important;
  align-items: center !important;
  justify-content: center !important;
  line-height: 1 !important;
  background: linear-gradient(135deg, #0ea5e9, #0284c7) !important;
  border: none !important;
  color: #fff !important;
  font-weight: 600 !important;
  font-size: 12px !important;
  height: 30px !important;
  padding: 0 14px !important;
  border-radius: 6px !important;
  cursor: pointer !important;
  white-space: nowrap !important;
  box-shadow: 0 3px 8px rgba(14, 165, 233, 0.15) !important;
  transition: all 0.2s ease !important;
}

.add-friend-dialog .btn-action-accept:hover {
  background: linear-gradient(135deg, #38bdf8, #0ea5e9) !important;
  transform: translateY(-1px) !important;
  box-shadow: 0 4px 12px rgba(56, 189, 248, 0.25) !important;
}

.add-friend-dialog .btn-action-decline {
  display: inline-flex !important;
  align-items: center !important;
  justify-content: center !important;
  line-height: 1 !important;
  background: rgba(255, 255, 255, 0.05) !important;
  border: 1px solid rgba(255, 255, 255, 0.1) !important;
  color: #9fb0c8 !important;
  font-weight: 500 !important;
  font-size: 12px !important;
  height: 30px !important;
  padding: 0 14px !important;
  border-radius: 6px !important;
background-color: #111c2d !important;
}

.add-friend-dialog .my-invite-card {
  background: linear-gradient(135deg, rgba(56, 189, 248, 0.06), rgba(56, 189, 248, 0.01)) !important;
  border: 1px solid rgba(56, 189, 248, 0.15) !important;
  border-radius: 8px !important;
  padding: 16px !important;
}

.add-friend-dialog .info-row {
  display: flex !important;
  align-items: center !important;
  justify-content: space-between !important;
  font-size: 13px !important;
  gap: 12px !important;
}

.add-friend-dialog .info-label {
  color: #9fb0c8 !important;
  font-weight: 500 !important;
  width: 90px !important;
  flex-shrink: 0 !important;
}

.add-friend-dialog .info-value-wrapper {
  display: flex !important;
  align-items: center !important;
  gap: 10px !important;
  flex: 1 !important;
  min-width: 0 !important;
  justify-content: flex-end !important;
}

.add-friend-dialog .info-code {
  background-color: rgba(56, 189, 248, 0.12) !important;
  color: #38bdf8 !important;
  padding: 3px 10px !important;
  border-radius: 4px !important;
  font-family: monospace !important;
  font-weight: 600 !important;
  border: 1px solid rgba(56, 189, 248, 0.2) !important;
  font-size: 13px !important;
}

.add-friend-dialog .info-link {
  color: #e2e8f0 !important;
  font-size: 12px !important;
  text-decoration: none !important;
  border-bottom: 1px dashed rgba(255, 255, 255, 0.2) !important;
  flex: 1 !important;
  min-width: 0 !important;
  white-space: nowrap !important;
  overflow: hidden !important;
  text-overflow: ellipsis !important;
  text-align: right !important;
}

.add-friend-dialog .copy-btn-link {
  background: transparent !important;
  border: none !important;
  color: #38bdf8 !important;
  cursor: pointer !important;
  font-size: 12px !important;
  font-weight: 600 !important;
  padding: 4px 8px !important;
  border-radius: 4px !important;
  display: inline-flex !important;
  align-items: center !important;
  gap: 6px !important;
  white-space: nowrap !important;
  flex-shrink: 0 !important;
  transition: all 0.2s ease !important;
  box-shadow: none !important;
}

.add-friend-dialog .copy-btn-link:hover {
  background-color: rgba(56, 189, 248, 0.12) !important;
  color: #7dd3fc !important;
}

.add-friend-dialog .custom-friend-input {
  flex: 1 !important;
  border: 1px solid rgba(255, 255, 255, 0.1) !important;
  border-radius: 6px !important;
  background-color: #0b131f !important;
  padding: 0 14px !important;
  color: #f8fafc !important;
  font-size: 13px !important;
  height: 38px !important;
  outline: none !important;
  transition: all 0.2s ease !important;
}

.add-friend-dialog .custom-friend-input:focus {
  border-color: #38bdf8 !important;
  box-shadow: 0 0 0 3px rgba(56, 189, 248, 0.15) !important;
  background-color: #070d16 !important;
}

.add-friend-dialog .field-label {
  display: block !important;
  font-size: 10px !important;
  font-weight: 700 !important;
  text-transform: uppercase !important;
  letter-spacing: 0.08em !important;
  color: #64748b !important;
  margin-bottom: 10px !important;
}

.add-friend-dialog .requests-list {
  display: flex !important;
  flex-direction: column !important;
  gap: 10px !important;
}

.add-friend-dialog .request-item {
  display: flex !important;
  align-items: center !important;
  padding: 12px 16px !important;
  border: 1px solid rgba(255, 255, 255, 0.05) !important;
  border-radius: 8px !important;
  background-color: rgba(255, 255, 255, 0.02) !important;
  gap: 12px !important;
  transition: all 0.2s ease !important;
}

.add-friend-dialog .request-item:hover {
  background-color: rgba(255, 255, 255, 0.04) !important;
  border-color: rgba(255, 255, 255, 0.08) !important;
}

.add-friend-dialog .btn-save {
  display: inline-flex !important;
  align-items: center !important;
  justify-content: center !important;
  line-height: 1 !important;
  background: linear-gradient(135deg, #0ea5e9, #0284c7) !important;
  border: none !important;
  color: #ffffff !important;
  font-weight: 600 !important;
  font-size: 13px !important;
  height: 38px !important;
  padding: 0 20px !important;
  border-radius: 6px !important;
  cursor: pointer !important;
  white-space: nowrap !important;
  transition: all 0.2s ease !important;
}

.add-friend-dialog .btn-save:hover {
  background: linear-gradient(135deg, #38bdf8, #0ea5e9) !important;
  transform: translateY(-1px) !important;
  box-shadow: 0 6px 16px rgba(56, 189, 248, 0.3) !important;
}

.add-friend-dialog .btn-action-accept {
  display: inline-flex !important;
  align-items: center !important;
  justify-content: center !important;
  line-height: 1 !important;
  background: linear-gradient(135deg, #0ea5e9, #0284c7) !important;
  border: none !important;
  color: #fff !important;
  font-weight: 600 !important;
  font-size: 12px !important;
  height: 30px !important;
  padding: 0 14px !important;
  border-radius: 6px !important;
  cursor: pointer !important;
  white-space: nowrap !important;
  box-shadow: 0 3px 8px rgba(14, 165, 233, 0.15) !important;
  transition: all 0.2s ease !important;
}

.add-friend-dialog .btn-action-decline:hover {
  background: rgba(255, 255, 255, 0.08) !important;
  color: #fff !important;
}

.sidebar-lists-scrollable {
  flex: 1;
  overflow-y: auto;
  margin-bottom: 12px;
  display: flex;
  flex-direction: column;
}
.sidebar-lists-scrollable::-webkit-scrollbar {
  width: 4px;
}
.sidebar-lists-scrollable::-webkit-scrollbar-thumb {
  background: transparent;
  border-radius: 4px;
}
.sidebar-lists-scrollable:hover::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.1);
}

.voice-item-wrapper {
  display: flex;
  flex-direction: column;
}
.voice-item .item-icon {
  color: #10b981 !important;
}
.voice-user {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 8px;
  border-radius: 4px;
  background-color: rgba(255, 255, 255, 0.02);
}

.connected-voice-panel {
  background-color: rgba(15, 23, 42, 0.3);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-card, 8px);
  padding: 10px 12px;
  margin-top: auto;
  box-shadow: 0 -4px 12px rgba(0, 0, 0, 0.15);
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.disconnect-btn-round {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  background-color: #ef4444 !important;
  color: white;
  border: none;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}
.disconnect-btn-round:hover {
  background-color: #dc2626 !important;
  transform: scale(1.05);
}
.voice-action-btn-small {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.08);
  color: var(--color-text-secondary);
  width: 32px;
  height: 32px;
  border-radius: 6px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}
.voice-action-btn-small:hover {
  background-color: rgba(255, 255, 255, 0.1);
  color: var(--color-text-primary);
}
.voice-action-btn-small.active {
  background-color: rgba(239, 68, 68, 0.15);
  color: #ef4444;
  border-color: rgba(239, 68, 68, 0.3);
}

/* Calling Simulation Screens Styling */
.calling-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background-color: rgba(15, 23, 42, 0.95);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 9999;
}

.calling-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
}

.calling-avatar-pulse {
  position: relative;
  margin-bottom: 24px;
}

.pulse-ring {
  position: absolute;
  top: -10px;
  left: -10px;
  right: -10px;
  bottom: -10px;
  border: 2px solid var(--color-primary);
  border-radius: 50%;
  animation: callingPulse 2s infinite ease-out;
  opacity: 0;
}

.ring-2 {
  animation-delay: 1s;
}

@keyframes callingPulse {
  0% {
    transform: scale(0.95);
    opacity: 0.5;
  }
  100% {
    transform: scale(1.6);
    opacity: 0;
  }
}

.calling-name {
  font-size: 24px;
  font-weight: 700;
  color: #ffffff;
  margin-bottom: 8px;
}

.calling-status {
  font-size: 15px;
  color: var(--color-text-muted);
  margin-bottom: 36px;
  animation: flash 1.5s infinite;
}

@keyframes flash {
  0%, 100% { opacity: 0.6; }
  50% { opacity: 1; }
}

.call-accept-circle-btn {
  width: 60px;
  height: 60px;
  border-radius: 50%;
  background-color: #22c55e;
  border: none;
  color: white;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 4px 14px rgba(34, 197, 94, 0.4);
  transition: all 0.2s;
}

.call-accept-circle-btn:hover {
  background-color: #16a34a;
  transform: scale(1.08);
}

.call-decline-circle-btn {
  width: 60px;
  height: 60px;
  border-radius: 50%;
  background-color: #ef4444;
  border: none;
  color: white;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 4px 14px rgba(239, 68, 68, 0.4);
  transition: all 0.2s;
}

.call-decline-circle-btn:hover {
  background-color: #dc2626;
  transform: scale(1.08);
}

/* Emoji Picker styling */
.emoji-picker-grid {
  display: grid;
  grid-template-columns: repeat(8, 1fr);
  gap: 6px;
  max-height: 200px;
  overflow-y: auto;
  padding: 6px;
}

.emoji-item {
  font-size: 20px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.15s ease;
  user-select: none;
}

.emoji-item:hover {
  background-color: var(--color-surface-hover);
  transform: scale(1.15);
}

/* Custom styles for attachment cards in messages */
.attachment-preview-container {
  margin-top: 8px;
  display: grid;
  gap: 8px;
  max-width: min(420px, 100%);
}

.message-attachment {
  min-width: 0;
}

.message-mention {
  display: inline;
  color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 14%, transparent);
  border-radius: 4px;
  padding: 1px 3px;
  font-weight: 650;
  overflow-wrap: anywhere;
}

.message-card.mention-target {
  outline: 2px solid color-mix(in srgb, var(--color-primary) 55%, transparent);
  outline-offset: 2px;
}

.mention-composer {
  position: relative;
}

.mention-menu {
  position: absolute;
  left: 0;
  bottom: calc(100% + 6px);
  z-index: 20;
  width: min(340px, calc(100vw - 32px));
  max-height: 240px;
  overflow-y: auto;
  padding: 6px;
  border: 1px solid var(--color-border);
  border-radius: 9px;
  background: var(--color-surface);
  box-shadow: var(--shadow-lg);
}

.mention-option {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 9px;
  padding: 7px 8px;
  border: 0;
  border-radius: 6px;
  background: transparent;
  color: var(--color-text-primary);
  text-align: left;
  cursor: pointer;
  overflow-wrap: anywhere;
}

.mention-option:hover,
.mention-option.active,
.mention-option:focus-visible {
  background: color-mix(in srgb, var(--color-primary) 13%, var(--color-surface));
  outline: none;
}

.mention-menu-state {
  padding: 9px;
  color: var(--color-text-muted);
  font-size: 12px;
}

.attachment-preview {
  gap: 8px;
  background: color-mix(in srgb, var(--color-surface) 88%, var(--color-primary) 12%);
  border: 1px solid var(--color-border);
}

.image-attachment {
  display: block;
  padding: 0;
  border: 0;
  border-radius: 8px;
  background: transparent;
  max-width: 100%;
}

.image-attachment img {
  display: block;
  max-width: min(320px, 100%);
  max-height: 240px;
  object-fit: contain;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
  transition: transform 0.2s ease;
  cursor: pointer;
}

.attachment-download-btn {
  margin-left: auto;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  border: 0;
  background: transparent;
  color: var(--color-primary);
  font-size: 12px;
  cursor: pointer;
  white-space: nowrap;
}

.attached-files-preview {
  display: grid;
  gap: 4px;
  max-height: 190px;
  overflow-y: auto;
  padding: 6px;
  border-bottom: 1px solid var(--color-border);
}

.image-attachment img:hover {
  transform: scale(1.02);
}

.attached-file-preview-bar {
  display: flex;
  align-items: center;
  background-color: color-mix(in srgb, var(--color-primary) 5%, var(--color-surface));
  border-radius: 8px;
  padding: 8px 12px;
  border: 1px solid var(--color-border);
  gap: 7px;
  min-width: 0;
}

.attached-file-preview-bar .truncate {
  max-width: min(260px, 45vw);
}

.selected-file-thumbnail {
  width: 30px;
  height: 30px;
  flex: 0 0 auto;
  border-radius: 6px;
  object-fit: cover;
}

.remove-attachment-btn {
  background: transparent;
  border: none;
  cursor: pointer;
  color: var(--color-text-muted);
  transition: color 0.15s;
}

.remove-attachment-btn:hover {
  color: var(--color-danger);
}

@media (max-width: 390px) {
  .attachment-preview {
    align-items: flex-start;
    flex-wrap: wrap;
  }

  .attachment-download-btn {
    width: 100%;
    margin-left: 32px;
  }

  .attached-file-preview-bar .truncate {
    max-width: 130px;
  }
}
</style>

<style>
/* Non-scoped style for emoji popover */
.emoji-popover-popper {
  background-color: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
  box-shadow: var(--shadow-lg) !important;
  border-radius: 10px !important;
  padding: 6px !important;
}
.emoji-popover-popper .el-popper__arrow::before {
  background-color: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
}
</style>
