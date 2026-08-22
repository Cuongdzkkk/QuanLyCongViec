<template>
  <main class="chat-container chat-workspace" :class="{ 'has-context-panel': showMembersSidebar }" aria-label="Không gian cộng tác SprintA">
    <!-- Project scope sidebar for real collaboration channels -->
    <nav class="server-bar" aria-label="Project spaces">
      <div class="rail-caption">PROJECTS</div>
      <div 
        v-for="project in projectOptions"
        :key="project.id"
        class="server-icon-wrapper"
        :class="{ active: activeProjectId === project.id }"
        @click="selectProject(project.id)"
        :aria-label="`Mở project ${project.name}`"
        :aria-current="activeProjectId === project.id ? 'page' : undefined"
        :title="project.name"
      >
        <div class="server-icon">
          {{ project.name.charAt(0).toUpperCase() }}
        </div>
        <div class="active-indicator"></div>
       </div>
    </nav>

    <!-- Chat Sidebar (Channels & Direct Messages) -->
    <div class="chat-sidebar">

      <div class="sidebar-header" style="display: flex; flex-direction: column; gap: 6px; padding-bottom: 12px; border-bottom: 1px solid var(--color-border); margin-bottom: 14px;">
          <span class="eyebrow">SPRINTA / COLLABORATION</span>
        <div class="flex items-center justify-between" style="display: flex; align-items: center; justify-content: space-between; width: 100%;">
          <h3 class="font-bold" style="display: flex; align-items: center; gap: 8px; flex: 1; min-width: 0; margin: 0; white-space: normal; line-height: 1.3;">
            <span class="workspace-mark" aria-hidden="true">S</span>
            <span>{{ activeProject?.name || 'Chọn project' }}</span>
          </h3>
          <button v-if="currentTab === 'dm'" type="button" class="workspace-back-button" @click="switchTab('channel')">
            <i class="fa-solid fa-arrow-left" aria-hidden="true"></i><span>Channels</span>
          </button>
      </div>
      </div>

      <!-- Sidebar lists wrap in scrollable container to pin voice panel at bottom -->
      <div class="sidebar-lists-scrollable">
        <!-- Channels List -->
        <div class="sidebar-section" v-if="currentTab === 'channel'">
          <div class="flex items-center justify-between section-header" style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
            <span class="section-title" style="margin-bottom: 0;">TEXT CHANNELS</span>
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
              Chưa có channel trong project này.
            </div>
            <button 
              v-for="ch in visibleChannels" 
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
        <div class="sidebar-section mt-4" v-if="currentTab === 'channel' && activeProjectId">
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
                style="display: flex; align-items: center;"
              >
                <span class="item-icon"><i class="fa-solid fa-volume-high"></i></span>
                <span class="item-name" style="white-space: normal; word-break: break-word; line-height: 1.3;">{{ vc.name }}</span>
              </button>
              <!-- Users in this voice channel -->
              <div class="voice-users-list ml-6 flex flex-col gap-1.5 mt-1" v-if="vc.id === activeVoiceChannel?.id && callParticipants.length">
                <div 
                  v-for="user in callParticipants"
                  :key="user.connectionId"
                  class="voice-user flex items-center gap-2 py-0.5 text-xs text-secondary"
                  style="display: flex; align-items: center; gap: 6px; padding-left: 12px; margin-top: 2px;"
                >
                  <el-avatar :size="16" :src="user.avatarUrl">{{ user.displayName.charAt(0) }}</el-avatar>
                  <span class="truncate text-xs" style="font-size: 11px; color: var(--color-text-secondary);">{{ user.displayName }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="sidebar-section direct-section" v-if="activeProjectId">
          <div class="section-header">
            <span class="section-title">DIRECT MESSAGES</span>
            <button class="add-btn-small" type="button" title="Tìm thành viên" aria-label="Tìm thành viên" @click="toggleContextPanel">
              <i class="fa-solid fa-user-plus" aria-hidden="true"></i>
            </button>
          </div>
          <div class="section-list">
            <button
              v-for="conversation in directConversations"
              :key="conversation.id"
              type="button"
              class="list-item direct-item"
              :class="{ active: activeChat?.id === conversation.id && activeChat?.type === 'dm' }"
              @click="selectChat(conversation, 'dm')"
            >
              <span class="presence-dot" aria-hidden="true"></span>
              <span class="item-name truncate">{{ conversation.name }}</span>
            </button>
            <button
              v-for="member in members.slice(0, 5)"
              :key="`member-${member.id}`"
              type="button"
              class="list-item direct-item direct-member-item"
              @click="selectDirectRecipient(member.id)"
            >
              <span class="presence-dot is-idle" aria-hidden="true"></span>
              <span class="item-name truncate">{{ member.name || member.fullName || member.email }}</span>
            </button>
            <div v-if="!directConversations.length && !members.length" class="channel-state">Chưa có cuộc trò chuyện riêng.</div>
          </div>
        </div>
      </div>

      <!-- Connected Voice Control Panel (Discord style) -->
      <div v-if="activeVoiceChannel" class="connected-voice-panel mt-auto" @click="showVoiceCallMain = true" style="cursor: pointer;">
        <div class="voice-status-info flex items-center justify-between" style="display: flex; justify-content: space-between; align-items: center;">
          <div class="flex items-center gap-2" style="display: flex; align-items: center; gap: 8px;">
            <span class="status-indicator-ping"><i class="fa-solid fa-signal text-success text-xs" style="color: var(--color-success);"></i></span>
            <div class="flex flex-col text-left" style="display: flex; flex-direction: column;">
               <span class="text-xs font-semibold text-success" style="font-size: 12px; color: var(--color-success);">Voice connected</span>
              <span class="text-xxs text-muted truncate" style="font-size: 10px; color: var(--color-text-muted); max-width: 130px; display: inline-block;">{{ activeVoiceChannel.name }}</span>
            </div>
          </div>
          <button class="disconnect-btn-round" title="Ngắt kết nối" @click.stop="leaveVoiceChannel">
            <i class="fa-solid fa-phone-slash text-xs"></i>
          </button>
        </div>
        <div class="voice-actions-row flex justify-around mt-2 pt-2 border-t border-slate-700/40" style="display: flex; justify-content: space-around; margin-top: 8px; padding-top: 8px; border-top: 1px solid var(--color-border);">
          <button 
            class="voice-action-btn-small" 
            :class="{ active: !callMicrophoneEnabled }"
            :title="callMicrophoneEnabled ? 'Tắt micro' : 'Bật micro'"
            @click.stop="toggleCallMicrophone"
          >
            <i :class="callMicrophoneEnabled ? 'fa-solid fa-microphone' : 'fa-solid fa-microphone-slash text-danger'"></i>
          </button>
          <button 
            class="voice-action-btn-small" 
            :class="{ active: isCallCameraOn }"
            :title="isCallCameraOn ? 'Tắt camera' : 'Bật camera'"
            @click.stop="toggleCallCameraReal"
          >
            <i :class="isCallCameraOn ? 'fa-solid fa-video' : 'fa-solid fa-video-slash'"></i>
          </button>
        </div>
      </div>
    </div>

    <!-- Active Chat Area -->
    <div class="chat-main">
      
      <!-- Embedded Voice Call View (Discord Style) -->
      <template v-if="showVoiceCallMain && activeVoiceChannel">
        <header class="chat-header call-header">
          <div class="active-info">
            <span class="active-icon"><i class="fa-solid fa-volume-high"></i></span>
            <div>
              <h4 class="font-semibold text-primary leading-tight">Kênh thoại: {{ activeVoiceChannel.name }}</h4>
              <p class="text-xs text-muted leading-none">
                Đã kết nối · {{ callParticipants.length }} người tham gia · Chất lượng tốt
              </p>
              <p v-if="callError" class="call-error" role="alert">{{ callError }}</p>
            </div>
          </div>
          <div class="header-actions">
            <button type="button" class="ai-entry-button" :class="{ 'is-open': aiAnalysisOpen }" aria-label="AI đang OFF — tính năng sắp ra mắt" title="AI đang OFF — tính năng sắp ra mắt" @click="openAiAnalysis('call')">
              <i class="fa-solid fa-wand-magic-sparkles" aria-hidden="true"></i>
              <span>AI đang OFF</span>
              <span class="ai-off-state">Sắp ra mắt</span>
            </button>
            <button type="button" class="action-btn" aria-label="Mở panel cuộc gọi" title="Mở panel cuộc gọi" @click="toggleContextPanel">
              <i class="fa-solid fa-layout-sidebar" aria-hidden="true"></i>
            </button>
            <button 
              class="action-btn" 
              title="Mở kênh chat"
              @click="openVoiceChannelChat"
              style="display: flex; align-items: center; justify-content: center;"
            >
              <i class="fa-solid fa-message text-lg"></i>
            </button>
          </div>
        </header>

        <div class="call-workspace-body">
          <section ref="presentationStage" class="call-presentation-stage" :class="{ 'is-focused': presentationFocused, 'is-fullscreen': presentationIsFullscreen }" aria-label="Presentation stage">
            <template v-if="activePresenter">
              <div class="presentation-heading">
                <span class="presentation-live-dot" aria-hidden="true"></span>
                <strong>{{ activePresenter.displayName }} đang chia sẻ màn hình</strong>
                <span class="presentation-hint">Nội dung được giữ nguyên tỷ lệ</span>
              </div>
              <button type="button" class="presentation-screen" :aria-label="presentationFocused ? 'Thu nhỏ màn hình chia sẻ' : 'Phóng to màn hình chia sẻ'" @click="togglePresentationFocus">
                <video :ref="setPresentationVideoElement" autoplay playsinline muted></video>
              </button>
              <div class="presentation-toolbar" role="toolbar" aria-label="Presentation controls">
                <button type="button" class="presentation-control" :title="presentationFocused ? 'Thu nhỏ' : 'Phóng to'" @click="togglePresentationFocus">
                  <i :class="presentationFocused ? 'fa-solid fa-compress' : 'fa-solid fa-expand'" aria-hidden="true"></i>
                  <span>{{ presentationFocused ? 'Thu nhỏ' : 'Phóng to' }}</span>
                </button>
                <button type="button" class="presentation-control" :title="presentationIsFullscreen ? 'Thoát toàn màn hình' : 'Toàn màn hình'" @click="togglePresentationFullscreen">
                  <i :class="presentationIsFullscreen ? 'fa-solid fa-compress-arrows-alt' : 'fa-solid fa-expand-arrows-alt'" aria-hidden="true"></i>
                  <span>{{ presentationIsFullscreen ? 'Thoát toàn màn hình' : 'Toàn màn hình' }}</span>
                </button>
                <button v-if="presentationFocused" type="button" class="presentation-control" title="Về lưới người tham gia" @click="returnToParticipantGrid">
                  <i class="fa-solid fa-table-cells" aria-hidden="true"></i>
                  <span>Về lưới</span>
                </button>
              </div>
            </template>
            <div v-else class="call-grid-empty" aria-live="polite">
              <i class="fa-solid fa-users-viewfinder" aria-hidden="true"></i>
              <span>Camera của người tham gia sẽ xuất hiện ở đây</span>
            </div>
          </section>

          <section class="call-participant-rail" aria-label="Call participants">
            <article
              v-for="user in callParticipants"
              :key="user.connectionId"
              class="call-participant-thumb"
              :class="{ 'is-presenter': activePresenter?.connectionId === user.connectionId }"
            >
              <div class="call-thumb-media">
                <video
                  v-if="user.connectionId === callConnectionId && (isCallCameraOn || isSharingScreen)"
                  :ref="setLocalVideoElement"
                  autoplay
                  playsinline
                  muted
                  :style="{ transform: isSharingScreen ? 'none' : 'scaleX(-1)' }"
                ></video>
                <video
                  v-else-if="user.connectionId !== callConnectionId && remoteStreams.has(user.connectionId) && (user.cameraEnabled || user.screenSharing)"
                  :ref="el => setRemoteVideoElement(el, user.connectionId)"
                  autoplay
                  playsinline
                  muted
                ></video>
                <el-avatar v-else :size="44" :src="user.connectionId === callConnectionId ? currentUser.avatar : user.avatarUrl">
                  {{ (user.connectionId === callConnectionId ? currentUser.name : user.displayName)?.charAt(0) }}
                </el-avatar>
                <audio
                  v-if="user.connectionId !== callConnectionId && remoteStreams.has(user.connectionId)"
                  :ref="el => setRemoteAudioElement(el, user.connectionId)"
                  autoplay
                ></audio>
              </div>
              <div class="call-thumb-caption">
                <span class="truncate">{{ user.displayName }}{{ user.connectionId === callConnectionId ? ' (Bạn)' : '' }}</span>
                <i v-if="!user.microphoneEnabled" class="fa-solid fa-microphone-slash" aria-label="Đang tắt micro"></i>
                <span v-if="activePresenter?.connectionId === user.connectionId" class="presenter-tag">Đang trình bày</span>
              </div>
            </article>
          </section>

          <div class="call-controls-row">
            <div class="call-control-dock">
              <button 
                class="call-control-circle-btn" 
                :class="{ 'inactive': !callMicrophoneEnabled }"
                @click="toggleCallMicrophone"
                :title="callMicrophoneEnabled ? 'Tắt Micro' : 'Bật Micro'"
              >
                <i :class="callMicrophoneEnabled ? 'fa-solid fa-microphone' : 'fa-solid fa-microphone-slash'"></i>
              </button>

              <button 
                class="call-control-circle-btn" 
                :class="{ 'inactive': !isCallCameraOn }" 
                @click="toggleCallCameraReal"
                :title="isCallCameraOn ? 'Tắt Camera' : 'Bật Camera'"
              >
                <i :class="isCallCameraOn ? 'fa-solid fa-video' : 'fa-solid fa-video-slash'"></i>
              </button>

              <div class="camera-effects-control">
                <button type="button" class="call-control-label-btn" :class="{ active: cameraBackgroundEffect === 'blur' }" title="Hiệu ứng camera" aria-haspopup="menu" :aria-expanded="showCameraEffectsMenu" @click="showCameraEffectsMenu = !showCameraEffectsMenu">
                  <i class="fa-solid fa-wand-magic-sparkles" aria-hidden="true"></i>
                  <span>Nền</span>
                </button>
                <div v-if="showCameraEffectsMenu" class="camera-effects-menu" role="menu" aria-label="Hiệu ứng nền camera">
                  <div class="camera-effects-title">Background</div>
                  <button type="button" role="menuitemradio" :aria-checked="cameraBackgroundEffect === 'none'" :class="{ selected: cameraBackgroundEffect === 'none' }" @click="setCallBackgroundEffect('none')">
                    <span class="effect-radio"></span><span>Không làm mờ</span>
                  </button>
                  <button type="button" role="menuitemradio" :aria-checked="cameraBackgroundEffect === 'blur'" :class="{ selected: cameraBackgroundEffect === 'blur' }" :disabled="cameraEffectPending" @click="setCallBackgroundEffect('blur')">
                    <span class="effect-radio"></span><span>Làm mờ nền</span>
                    <i v-if="cameraEffectPending" class="fa-solid fa-spinner fa-spin" aria-hidden="true"></i>
                  </button>
                  <p v-if="cameraEffectNotice" class="camera-effects-notice" role="status">{{ cameraEffectNotice }}</p>
                </div>
              </div>

              <button 
                class="call-control-circle-btn" 
                :class="{ 'active-share': isSharingScreen }" 
                @click="toggleScreenShare"
                :title="isSharingScreen ? 'Tắt chia sẻ' : 'Chia sẻ màn hình'"
                style="background-color: #2b2d31; color: white;"
              >
                <i class="fa-solid fa-desktop" :style="{ color: isSharingScreen ? '#22c55e' : '#dbdee1' }"></i>
              </button>

              <button 
                class="call-control-circle-btn hang-up" 
                @click="leaveVoiceChannel"
                title="Rời kênh thoại"
              >
                <i class="fa-solid fa-phone-slash"></i>
              </button>
            </div>
          </div>
        </div>
      </template>

      <!-- Standard Text Chat View -->
      <template v-else>
        <header class="chat-header text-chat-header">
          <div class="active-info">
            <span class="active-icon">{{ activeChat?.type === 'channel' ? '#' : '@' }}</span>
            <div>
              <div style="display: flex; align-items: center; gap: 6px;">
                <h4 class="font-semibold text-primary leading-tight">{{ activeChat?.name || 'Chưa chọn Channel' }}</h4>
                <span 
                  v-if="activeChannel?.desc?.startsWith('__voice_chat_channel__')"
                  style="font-size: 10px; background-color: rgba(34, 197, 94, 0.1); color: #22c55e; padding: 2px 6px; border-radius: 4px; font-weight: 600; line-height: 1;"
                >
                  Voice Chat
                </span>
              </div>
              <p class="text-xs text-muted leading-none" style="margin-top: 2px;">
                {{ activeChat?.type === 'channel' ? (activeChat.desc.startsWith('__voice_chat_channel__') ? 'Kênh chat riêng dành cho phòng thoại' : activeChat.desc) : (activeChat ? 'Tin nhắn được lưu trên máy chủ' : 'Chọn một cuộc trò chuyện') }}
              </p>
            </div>
          </div>

          <div class="header-actions" v-if="activeProjectId">
            <button type="button" class="ai-entry-button" :class="{ 'is-open': aiAnalysisOpen }" aria-label="AI đang OFF — tính năng sắp ra mắt" title="AI đang OFF — tính năng sắp ra mắt" @click="openAiAnalysis('text')">
              <i class="fa-solid fa-wand-magic-sparkles" aria-hidden="true"></i>
              <span>AI đang OFF</span>
              <span class="ai-off-state">Sắp ra mắt</span>
            </button>
            <button type="button" class="action-btn" aria-label="Mở panel channel" title="Mở panel channel" @click="toggleContextPanel">
              <i class="fa-solid fa-layout-sidebar" aria-hidden="true"></i>
            </button>
            <button 
              v-if="activeChannel?.desc?.startsWith('__voice_chat_channel__') && activeVoiceChannel?.id === activeChannel.desc.split(':')[1]"
              class="action-btn"
              title="Quay lại phòng thoại"
              @click="showVoiceCallMain = true"
              style="display: flex; align-items: center; justify-content: center; background-color: var(--sa-primary-soft, rgba(99,102,241,0.08)); color: var(--color-accent, #6366f1); margin-right: 8px; border-radius: 6px; padding: 4px 10px; font-size: 12px; gap: 6px; width: auto; height: 32px;"
            >
              <i class="fa-solid fa-volume-high"></i>
              <span class="font-semibold">Vào phòng thoại</span>
            </button>

            <el-popover
              placement="bottom-end"
              :width="300"
              trigger="click"
              popper-class="project-members-popover"
              @before-enter="fetchProjectMembers"
            >
              <template #reference>
                <button class="action-btn" title="Thành viên dự án">
                  <i class="fa-solid fa-users text-lg"></i>
                </button>
              </template>
              
              <div class="popover-members-content" style="padding: 4px;">
                <h5 style="margin: 0 0 12px 0; font-size: 13px; font-weight: 700; color: #f8fafc; border-bottom: 1px solid var(--color-border); padding-bottom: 8px; display: flex; justify-content: space-between; align-items: center;">
                  <span>Thành viên Project ({{ projectMembers.length }})</span>
                  <i v-if="loadingMembers" class="fa-solid fa-spinner fa-spin text-xs"></i>
                </h5>
                
                <div style="max-height: 240px; overflow-y: auto; display: flex; flex-direction: column; gap: 8px;">
                  <div v-if="projectMembers.length === 0 && !loadingMembers" style="text-align: center; color: var(--color-text-muted); font-size: 12px; padding: 12px 0;">
                    Không có thành viên nào.
                  </div>
                  
                  <div 
                    v-for="member in projectMembers" 
                    :key="member.userId || member.id"
                    style="display: flex; align-items: center; gap: 10px; padding: 6px 8px; border-radius: 6px;"
                  >
                    <el-avatar :size="28" :src="member.avatarUrl || member.avatar">{{ (member.fullName || member.name || '?').charAt(0) }}</el-avatar>
                    <div style="display: flex; flex-direction: column; min-width: 0; flex: 1;">
                      <span style="font-size: 13px; font-weight: 600; color: #f8fafc;" class="truncate">{{ member.fullName || member.name }}</span>
                      <span style="font-size: 11px; color: var(--color-text-muted);" class="truncate">{{ member.email || member.jobTitle || 'Thành viên' }}</span>
                    </div>
                  </div>
                </div>
              </div>
            </el-popover>
          </div>
        </header>

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
                <div v-else-if="currentTab === 'channel' && activeMessages.length === 0" class="history-state empty-chat-state">
                 <span class="empty-state-icon" aria-hidden="true">#</span>
                 <strong>Channel này đã sẵn sàng</strong>
                 <span>Bắt đầu bằng một câu hỏi, cập nhật hoặc file liên quan đến project.</span>
              </div>
              <div v-else-if="currentTab === 'dm' && !activeChat" class="history-state">
                Chọn người dùng để bắt đầu trò chuyện.
              </div>
              <div v-else-if="currentTab === 'dm' && activeMessages.length === 0" class="history-state">
                Chưa có tin nhắn nào ở đây. Gửi tin nhắn đầu tiên để bắt đầu cuộc trò chuyện.
              </div>
              <template v-else>
                <div v-if="historyLoadingOlder" class="history-state" role="status">
                  <i class="fa-solid fa-spinner fa-spin"></i>
                  <span>Đang tải tin nhắn cũ hơn...</span>
                </div>
                <button
                  v-else-if="messagePagination.page < Math.ceil(messagePagination.totalCount / messagePagination.pageSize)"
                  type="button"
                  class="state-action load-older-action"
                  @click="loadOlderMessages"
                >
                  Tải tin nhắn cũ hơn
                </button>
                <div
                  v-for="msg in activeMessages"
                  :key="msg.messageId"
                  class="message-card"
                  :class="{ 'mention-target': msg.isMentioned }"
                >
                  <el-avatar :size="36" :src="msg.senderAvatar || ''" class="sender-avatar">{{ msg.senderName?.charAt(0) || '?' }}</el-avatar>
                  <div class="message-content-wrapper">
                    <div class="message-header-line">
                      <span class="sender-name">{{ msg.senderName }}</span>
                      <span class="message-time">{{ formatTime(msg.sentAt) }}</span>
                    </div>
                    <div class="message-body">
                      <span
                        v-for="(segment, segmentIndex) in msg.contentSegments"
                        :key="`${msg.messageId}-${segmentIndex}`"
                        :class="{ 'message-mention': segment.isMention }"
                      >{{ segment.text }}</span>
                    </div>
                    <div v-if="msg.files && msg.files.length > 0" class="attachment-preview-container">
                      <div
                        v-for="file in msg.files"
                        :key="file.id"
                        class="attachment-preview"
                        style="display: flex; align-items: center; padding: 8px; border-radius: 8px; margin-top: 4px;"
                      >
                        <template v-if="isImageFile(file.fileName)">
                          <button
                            type="button"
                            class="image-attachment"
                            @click="previewImage(file.url)"
                            title="Nhấp để xem ảnh lớn"
                          >
                            <img :src="file.url" :alt="file.fileName" />
                          </button>
        </template>
                        <template v-else>
                          <div class="message-attachment" style="display: flex; align-items: center; gap: 8px; flex: 1;">
                            <i class="fa-solid fa-file-lines text-muted text-lg"></i>
                            <div style="display: flex; flex-direction: column; min-width: 0; text-align: left;">
                              <span style="font-size: 13px; font-weight: 600; color: #fff;" class="truncate">{{ file.fileName }}</span>
                              <span style="font-size: 11px; color: var(--color-text-muted);">{{ formatFileSize(file.fileSize) }}</span>
                            </div>
                            <a
                              :href="file.url"
                              download
                              class="attachment-download-btn"
                              style="background: rgba(255,255,255,0.06); border: 1px solid rgba(255,255,255,0.1); border-radius: 4px; padding: 4px 10px; color: #fff; font-size: 12px; text-decoration: none;"
                            >
                              <i class="fa-solid fa-download mr-1"></i> Tải xuống
                            </a>
                          </div>
       </template>

                    </div>
                  </div>
                </div>
                </div>
              </template>
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
                    <el-avatar :size="26" :src="member.avatarUrl || ''">
                      <i v-if="member.userId === 'all'" class="fa-solid fa-bullhorn text-xs"></i>
                      <template v-else>{{ member.displayName?.charAt(0) || '?' }}</template>
                    </el-avatar>
                    <span>{{ member.userId === 'all' ? member.fullName : member.displayName }}</span>
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
        </div>
      </template>

      <aside v-if="showMembersSidebar" class="chat-context-panel" aria-label="Context panel">
        <div class="context-panel-header">
          <div><span class="context-kicker">CONTEXT</span><h3>{{ showVoiceCallMain && activeVoiceChannel ? 'Cuộc gọi' : 'Channel details' }}</h3></div>
          <button type="button" class="context-close" aria-label="Đóng panel context" title="Đóng panel" @click="toggleContextPanel"><i class="fa-solid fa-xmark" aria-hidden="true"></i></button>
        </div>
        <div class="context-tabs" role="tablist" aria-label="Context tabs">
          <button type="button" class="context-tab is-active" role="tab" aria-selected="true">{{ showVoiceCallMain && activeVoiceChannel ? 'Participants' : 'Members' }}</button>
          <button type="button" class="context-tab" role="tab" @click="openAiAnalysis(showVoiceCallMain ? 'call' : 'text')">AI</button>
        </div>
        <div v-if="showVoiceCallMain && activeVoiceChannel" class="context-call-summary">
          <span class="context-status-dot"></span><div><strong>{{ activeVoiceChannel.name }}</strong><span>{{ callParticipants.length }} người trong phòng</span></div>
        </div>
        <div v-if="showVoiceCallMain && activeVoiceChannel" class="context-member-list">
          <div v-for="user in callParticipants" :key="`context-${user.connectionId}`" class="context-member-row">
            <el-avatar :size="30" :src="user.avatarUrl">{{ user.displayName?.charAt(0) }}</el-avatar><span>{{ user.displayName }}{{ user.connectionId === callConnectionId ? ' (Bạn)' : '' }}</span>
            <i v-if="user.userId === currentUser.id && !user.microphoneEnabled" class="fa-solid fa-microphone-slash" aria-label="Đang tắt micro"></i>
          </div>
        </div>
        <div v-else class="context-member-list">
          <div v-if="loadingMembers" class="context-empty">Đang tải thành viên...</div>
          <div v-else-if="!projectMembers.length" class="context-empty">Chưa có thành viên để hiển thị.</div>
          <div v-for="member in projectMembers" :key="`project-member-${member.userId || member.id}`" class="context-member-row">
            <el-avatar :size="30" :src="member.avatarUrl || member.avatar">{{ (member.fullName || member.name || '?').charAt(0) }}</el-avatar>
            <div class="context-member-copy"><strong>{{ member.fullName || member.name }}</strong><span>{{ member.jobTitle || 'Thành viên project' }}</span></div><span class="presence-dot is-idle" aria-label="Đang offline"></span>
          </div>
        </div>
      </aside>

      <aside v-if="aiAnalysisOpen" class="ai-analysis-surface" aria-live="polite" aria-label="AI analysis">
        <div class="ai-surface-header"><div><span class="context-kicker">SPRINTA AI</span><h3>{{ aiAnalysisScope === 'call' ? 'Phân tích cuộc gọi' : 'Phân tích channel' }}</h3></div><button type="button" class="context-close" aria-label="Đóng AI analysis" title="Đóng" @click="aiAnalysisOpen = false"><i class="fa-solid fa-xmark" aria-hidden="true"></i></button></div>
        <div class="ai-off-banner"><span class="ai-state-indicator"></span><strong>AI đang OFF</strong></div>
        <p>Đây là điểm vào đã chuẩn bị cho phase AI tiếp theo. Chưa có phân tích, ghi âm hay lắng nghe nào được khởi động.</p>
        <div class="ai-capability-list"><span>Tóm tắt</span><span>Quyết định</span><span>Việc cần làm</span><span>Rủi ro / câu hỏi</span></div>
        <button type="button" class="ai-disabled-action" disabled>Coming in a later phase</button>
      </aside>
    </div>



    <!-- Create Channel Dialog -->
    <el-dialog
      v-model="createChannelActive"
      width="440px"
      append-to-body
      class="sa-data-dialog sa-modal--sm"
      :show-close="false"
    >
      <template #header>
        <DataModalHeader
          icon="bi bi-hash"
          title="Tạo Kênh chat mới"
          description="Tạo kênh thảo luận trong server hiện tại"
          @close="createChannelActive = false"
        />
      </template>
      <DataModalSection icon="bi bi-card-text" title="Thông tin kênh">
        <DataModalField label="Tên Kênh">
          <input 
            v-model="newChannelName" 
            placeholder="đặt tên cho channel" 
            class="custom-friend-input"
            style="width: 100%; height: 38px;"
            maxlength="100"
            :disabled="creatingChannel"
          />
        </DataModalField>
        <DataModalField label="Mô tả kênh">
          <input 
            v-model="newChannelDesc" 
            placeholder="Mô tả mục đích của kênh này..." 
            class="custom-friend-input"
            style="width: 100%; height: 38px;"
            maxlength="500"
            :disabled="creatingChannel"
          />
        </DataModalField>
      </DataModalSection>
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
      width="440px"
      append-to-body
      class="sa-data-dialog sa-modal--sm"
      :show-close="false"
    >
      <template #header>
        <DataModalHeader
          icon="bi bi-mic"
          title="Tạo Kênh thoại mới"
          description="Tạo phòng thoại cho cuộc họp hoặc trao đổi nhanh"
          @close="createVoiceActive = false"
        />
      </template>
      <DataModalSection icon="bi bi-card-text" title="Thông tin kênh thoại">
        <DataModalField label="Tên Kênh thoại">
          <input
            v-model="newVoiceName"
            placeholder="Ví dụ: Họp kỹ thuật"
            class="custom-friend-input"
          />
        </DataModalField>
      </DataModalSection>
      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 10px;">
          <el-button @click="createVoiceActive = false">Hủy</el-button>
          <button class="btn-primary-custom" @click="createNewVoice" style="background-color: var(--color-primary); color: white; border: none; border-radius: var(--radius-button); padding: 8px 16px; font-weight: 600; cursor: pointer;">
            Tạo Kênh thoại
          </button>
        </div>
      </template>
    </el-dialog>
  </main>
</template>

<script setup>
import { ref, onMounted, onBeforeUnmount, nextTick, watch, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import DataModalField from '@/components/common/Foundation/DataModalField.vue'

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
import { createCallMediaSession } from '@/services/callMediaService'
import {
  clearScopedCurrentProjectId,
  getScopedCurrentProjectId,
  setScopedCurrentProjectId
} from '@/utils/projectContext'

const route = useRoute()
const router = useRouter()
const projectStore = useProjectStore()
const authStore = useAuthStore()
const currentTab = ref('channel')
const switchTab = (tab) => {
  if (tab !== 'channel' && tab !== 'dm') return
  currentTab.value = tab
}

const projectOptions = computed(() => projectStore.sidebarProjects)
const activeProjectId = ref('')
const activeProject = computed(() =>
  projectOptions.value.find(project => project.id === activeProjectId.value) || null
)
const projectsLoading = ref(false)
const projectsError = ref('')

const channels = ref([])
const visibleChannels = computed(() => {
  return channels.value.filter(ch => !ch.desc?.startsWith('__voice_chat_channel__'))
})
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

const voiceChannels = ref([])
const loadVoiceChannels = (projectId) => {
  if (!projectId) {
    voiceChannels.value = []
    return
  }
  const key = `voice_channels_${projectId}`
  const stored = localStorage.getItem(key)
  if (stored) {
    try {
      voiceChannels.value = JSON.parse(stored)
      return
    } catch (e) {
      console.error(e)
    }
  }
  const defaultVcs = [
    { id: `vc-gen-${projectId}`, name: 'Phòng thoại chung 🔊', users: [] },
    { id: `vc-tech-${projectId}`, name: 'Trao Đổi Kỹ Thuật 💻', users: [] }
  ]
  voiceChannels.value = defaultVcs
  localStorage.setItem(key, JSON.stringify(defaultVcs))
}

const saveVoiceChannels = () => {
  if (!activeProjectId.value) return
  const key = `voice_channels_${activeProjectId.value}`
  localStorage.setItem(key, JSON.stringify(voiceChannels.value))
}

// Modal state refs
const createChannelActive = ref(false)
const newChannelName = ref('')
const newChannelDesc = ref('')
const creatingChannel = ref(false)
const createChannelIdempotencyKey = ref('')
const createChannelPayloadFingerprint = ref('')
const createChannelAbortController = ref(null)
const createVoiceActive = ref(false)
const newVoiceName = ref('')
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
  voiceChannels.value.push(newVc)
  saveVoiceChannels()
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
const aiAnalysisOpen = ref(false)
const aiAnalysisScope = ref('text')
const openAiAnalysis = (scope = 'text') => {
  aiAnalysisScope.value = scope
  aiAnalysisOpen.value = true
}
const isCallCameraOn = ref(false)
const activeVoiceChannel = ref(null)
const showVoiceCallMain = ref(false)
const isSharingScreen = ref(false)
const cameraBackgroundEffect = ref('none')
const cameraEffectPending = ref(false)
const cameraEffectNotice = ref('')
const showCameraEffectsMenu = ref(false)
const presentationStage = ref(null)
const presentationFocused = ref(false)
const presentationIsFullscreen = ref(false)
const callMicrophoneEnabled = ref(true)
const callParticipants = ref([])
const remoteStreams = ref(new Map())
const localCallStream = ref(null)
const callConnectionId = ref('')
const callState = ref('disconnected')
const callError = ref('')
const callSession = ref(null)

const activePresenter = computed(() => {
  if (isSharingScreen.value) {
    return callParticipants.value.find(user => user.connectionId === callConnectionId.value) || {
      connectionId: callConnectionId.value,
      displayName: currentUser.value.name || 'Bạn',
      userId: currentUser.value.id,
      screenSharing: true
    }
  }
  return callParticipants.value.find(user => user.screenSharing) || null
})

const activePresenterStream = () => {
  const presenter = activePresenter.value
  if (!presenter) return null
  return presenter.connectionId === callConnectionId.value
    ? localCallStream.value
    : remoteStreams.value.get(presenter.connectionId) || null
}

const setLocalVideoElement = (element) => {
  if (element) element.srcObject = localCallStream.value
}

const setRemoteVideoElement = (element, connectionId) => {
  if (element) element.srcObject = remoteStreams.value.get(connectionId) || null
}

const setRemoteAudioElement = (element, connectionId) => {
  if (element) element.srcObject = remoteStreams.value.get(connectionId) || null
}

const setPresentationVideoElement = (element) => {
  if (element) element.srcObject = activePresenterStream()
}

const describeCallError = (error) => ({
  CALL_ROOM_FULL: 'Phòng thoại đã đủ 6 người.',
  PERMISSION_DENIED: 'Bạn đã từ chối quyền truy cập microphone hoặc camera.',
  DEVICE_NOT_FOUND: 'Không tìm thấy thiết bị microphone hoặc camera.',
  DEVICE_BUSY: 'Microphone hoặc camera đang được ứng dụng khác sử dụng.',
  UNSUPPORTED_BROWSER: 'Trình duyệt này không hỗ trợ cuộc gọi media an toàn.',
  MIC_UNAVAILABLE: 'Không thể khởi động microphone cho cuộc gọi.',
  CAMERA_UNAVAILABLE: 'Không thể bật camera cho cuộc gọi.',
  SCREEN_SHARE_UNAVAILABLE: 'Không thể chia sẻ màn hình.',
  SCREEN_SHARE_BUSY: 'Một người khác đang chia sẻ màn hình. Hãy đợi họ dừng chia sẻ.'
}[error?.code] || (error?.message?.includes('CALL_ROOM_FULL') ? 'Phòng thoại đã đủ 6 người.' : null) || error?.message || 'Không thể kết nối cuộc gọi.')

const handleCallError = (error, showMessage = true) => {
  callError.value = describeCallError(error)
  if (showMessage) ElMessage.error(callError.value)
}

const syncLocalCallPreview = async () => {
  localCallStream.value = callSession.value?.getLocalStream?.() || null
  callConnectionId.value = callSession.value?.getConnectionId?.() || ''
  await nextTick()
  const localVideo = document.querySelector('.group-video-grid .local-user video')
  if (localVideo) localVideo.srcObject = localCallStream.value
}

const createCallSessionForVoiceChannel = (voiceChannel) => createCallMediaSession({
  projectId: activeProjectId.value,
  voiceChannelId: `${voiceChannel.name}`.trim().toLocaleLowerCase(),
  onState: async ({ state, error }) => {
    callState.value = state
    if (error) handleCallError(error, state === 'error')
    if (state === 'connected') await syncLocalCallPreview()
    if (state === 'media' && callSession.value) {
      const mediaState = callSession.value.getMediaState()
      callMicrophoneEnabled.value = mediaState.microphoneEnabled
      isCallCameraOn.value = mediaState.cameraEnabled
      isSharingScreen.value = mediaState.screenSharing
      cameraBackgroundEffect.value = mediaState.backgroundEffect || 'none'
      await syncLocalCallPreview()
    }
    if (state === 'effect-fallback') {
      cameraBackgroundEffect.value = 'none'
      cameraEffectNotice.value = 'Không thể làm mờ nền trên thiết bị này. Camera thường vẫn đang được dùng.'
      ElMessage.warning(cameraEffectNotice.value)
    }
  },
  onParticipants: (items) => {
    callParticipants.value = items
  },
  onRemoteStreams: (items) => {
    remoteStreams.value = items
  }
})

const toggleCallMicrophone = async () => {
  if (!callSession.value) return
  const nextValue = !callMicrophoneEnabled.value
  try {
    await callSession.value.setMicrophoneEnabled(nextValue)
    callMicrophoneEnabled.value = nextValue
  } catch (error) {
    handleCallError(error)
  }
}

const toggleCallCameraReal = async () => {
  if (!callSession.value) return
  const nextValue = !isCallCameraOn.value
  try {
    await callSession.value.setCameraEnabled(nextValue)
    isCallCameraOn.value = nextValue
    await syncLocalCallPreview()
  } catch (error) {
    handleCallError(error)
  }
}

const setCallBackgroundEffect = async effect => {
  if (!callSession.value) return
  cameraEffectPending.value = true
  cameraEffectNotice.value = ''
  try {
    await callSession.value.setCameraBackgroundEffect(effect)
    cameraBackgroundEffect.value = callSession.value.getMediaState().backgroundEffect || 'none'
    await syncLocalCallPreview()
    showCameraEffectsMenu.value = false
  } catch (error) {
    handleCallError(error)
  } finally {
    cameraEffectPending.value = false
  }
}

const toggleScreenShare = async () => {
  if (!callSession.value) return
  if (!isSharingScreen.value && activePresenter.value && activePresenter.value.connectionId !== callConnectionId.value) {
    const error = { code: 'SCREEN_SHARE_BUSY' }
    handleCallError(error)
    return
  }
  try {
    await callSession.value.toggleScreenShare()
    const mediaState = callSession.value.getMediaState()
    isSharingScreen.value = mediaState.screenSharing
    isCallCameraOn.value = mediaState.cameraEnabled
    cameraBackgroundEffect.value = mediaState.backgroundEffect || cameraBackgroundEffect.value
    await syncLocalCallPreview()
  } catch (error) {
    handleCallError(error, error?.code !== 'PERMISSION_DENIED')
  }
}

const joinVoiceChannel = async (vc) => {
  if (activeVoiceChannel.value?.id === vc.id) {
    showVoiceCallMain.value = true
    return
  }
  if (callSession.value) await leaveVoiceChannel(false)

  const session = createCallSessionForVoiceChannel(vc)
  callSession.value = session
  callError.value = ''
  try {
    await session.start()
    activeVoiceChannel.value = vc
    showVoiceCallMain.value = true
    await syncLocalCallPreview()
    ElMessage.success(`Đã kết nối vào kênh thoại: ${vc.name}`)
  } catch (error) {
    handleCallError(error)
    await session.leave().catch(() => {})
    callSession.value = null
    callParticipants.value = []
    remoteStreams.value = new Map()
    callConnectionId.value = ''
  }
}

const leaveVoiceChannel = async (showMessage = true) => {
  const current = activeVoiceChannel.value
  if (callSession.value) await callSession.value.leave()
  callSession.value = null
  callParticipants.value = []
  remoteStreams.value = new Map()
  localCallStream.value = null
  callConnectionId.value = ''
  callMicrophoneEnabled.value = true
  isCallCameraOn.value = false
  isSharingScreen.value = false
  cameraBackgroundEffect.value = 'none'
  cameraEffectNotice.value = ''
  showCameraEffectsMenu.value = false
  presentationFocused.value = false
  activeVoiceChannel.value = null
  showVoiceCallMain.value = false
  if (showMessage && current) ElMessage.info(`Đã ngắt kết nối khỏi kênh thoại: ${current.name}`)
}

const togglePresentationFocus = () => {
  presentationFocused.value = !presentationFocused.value
}

const returnToParticipantGrid = () => {
  presentationFocused.value = false
}

const syncPresentationFullscreen = () => {
  presentationIsFullscreen.value = document.fullscreenElement === presentationStage.value
}

const togglePresentationFullscreen = async () => {
  if (!presentationStage.value) return
  try {
    if (document.fullscreenElement === presentationStage.value) await document.exitFullscreen()
    else if (presentationStage.value.requestFullscreen) await presentationStage.value.requestFullscreen()
  } catch (error) {
    handleCallError(error)
  }
}

watch(activePresenter, async presenter => {
  if (presenter) return
  presentationFocused.value = false
  if (document.fullscreenElement === presentationStage.value) await document.exitFullscreen().catch(() => {})
})

const openVoiceChannelChat = async () => {
  if (!activeVoiceChannel.value || !activeProjectId.value) return
  const voiceVc = activeVoiceChannel.value
  const targetDesc = `__voice_chat_channel__:${voiceVc.id}`
  
  let targetChannel = channels.value.find(ch => ch.desc === targetDesc)
  
  if (!targetChannel) {
    try {
      ElMessage.info('Đang mở kết nối kênh chat cho phòng thoại...')
      const cleanName = voiceVc.name
        .replace(/[\uE000-\uF8FF]|\uD83C[\uDC00-\uDFFF]|\uD83D[\uDC00-\uDFFF]|[\u2011-\u26FF]|\uD83E[\uDD10-\uDDFF]/g, '')
        .replace(/[^a-zA-Z0-9\sÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚĂĐĨŨƠàáâãèéêìíòóôõùúăđĩũơƯĂÂĐÊÔƠưăâđêôơ]/g, '')
        .trim() || 'Kênh thoại'
      
      const result = await collaborationApi.createProjectChannel(
        activeProjectId.value,
        {
          name: cleanName,
          description: targetDesc,
          visibility: 'Private'
        },
        {
          idempotencyKey: makeChannelIdempotencyKey()
        }
      )
      targetChannel = mapChannel(result, activeProjectId.value)
      channels.value.push(targetChannel)
      channelPagination.value.totalCount += 1
    } catch (error) {
      console.error('Failed to create voice channel chat:', error)
      const details = error?.response?.data?.message || error?.message || 'Lỗi không xác định'
      ElMessage.error(`Không thể kết nối kênh chat cho phòng thoại này: ${details}`)
      return
    }
  }
  
  if (targetChannel) {
    showVoiceCallMain.value = false
    await selectChat(targetChannel, 'channel')
  }
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
      const lastChannelId = localStorage.getItem(`last_channel_id_${projectId}`)
      const savedChannel = lastChannelId ? channels.value.find(item => item.id === lastChannelId) : null
      const firstVisibleChannel = channels.value.find(ch => !ch.desc?.startsWith('__voice_chat_channel__'))
      await selectChat(linkedChannel || savedChannel || firstVisibleChannel || channels.value[0], 'channel')
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
  const savedProjectId = localStorage.getItem('last_active_project_id')
  if (linkedProjectId && projectOptions.value.some(project => project.id === linkedProjectId)) {
    activeProjectId.value = linkedProjectId
  } else if (savedProjectId && projectOptions.value.some(project => project.id === savedProjectId)) {
    activeProjectId.value = savedProjectId
  } else if (!activeProjectId.value && projectOptions.value.length > 0) {
    activeProjectId.value = projectOptions.value[0].id
  }
  if (activeProjectId.value) {
    loadVoiceChannels(activeProjectId.value)
    fetchProjectMembers()
    await loadChannels({ page: 1 })
  }
}

onMounted(() => {
  componentMounted = true
  document.addEventListener('fullscreenchange', syncPresentationFullscreen)
  registerRealtimeHandlers()
  void initializeCollaborationContext()
})

watch(activeProjectId, async (projectId, previousProjectId) => {
  if (projectId === previousProjectId) return
  await leaveVoiceChannel(false)
  await leaveActiveRealtimeGroup()
  clearChannels()
  loadVoiceChannels(projectId)
  if (!projectId) {
    clearScopedCurrentProjectId()
    return
  }
  fetchProjectMembers()
  if (!projectOptions.value.some(project => project.id === projectId)) {
    activeProjectId.value = ''
    return
  }
  setScopedCurrentProjectId(projectId)
  localStorage.setItem('last_active_project_id', projectId)
  await loadChannels({ page: 1 })
})

watch(() => authStore.token, async (token, previousToken) => {
  if (!componentMounted || token === previousToken) return
  collaborationContextVersion += 1
  await leaveVoiceChannel(false)
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
  document.removeEventListener('fullscreenchange', syncPresentationFullscreen)
  collaborationContextVersion += 1
  realtimeUnsubscribers.splice(0).forEach(unsubscribe => unsubscribe())
  if (connectionNoticeTimer) {
    window.clearTimeout(connectionNoticeTimer)
    connectionNoticeTimer = null
  }
  cancelPendingMarkRead()
  removeAttachedFile()
  revokeMessageAttachmentUrls()
  void leaveVoiceChannel(false)
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
const toggleContextPanel = toggleMembersSidebar
const activeServerMembers = computed(() => {
  return projectMembers.value
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
  
  mentionDebounceTimer = window.setTimeout(() => {
    mentionLoading.value = false
    const selectedIds = new Set(selectedMentions.value.map(item => item.userId))
    const candidates = []
    
    // Local filter on project members
    const queryLower = query ? query.toLowerCase() : ''
    projectMembers.value.forEach(member => {
      const uId = member.userId || member.id
      if (selectedIds.has(uId)) return
      
      const fName = member.fullName || member.name || ''
      const email = member.email || ''
      
      if (!query || fName.toLowerCase().includes(queryLower) || email.toLowerCase().includes(queryLower)) {
        candidates.push({
          userId: uId,
          displayName: fName,
          fullName: fName,
          avatarUrl: member.avatarUrl || member.avatar || ''
        })
      }
    })
    
    mentionSuggestions.value = candidates.slice(0, 20)
    mentionActiveIndex.value = 0
  }, 100)
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
  switchTab(type === 'dm' ? 'dm' : 'channel')
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
  showVoiceCallMain.value = false
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

  if (type === 'channel' && activeProjectId.value) {
    localStorage.setItem(`last_channel_id_${activeProjectId.value}`, item.id)
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

const projectMembers = ref([])
const loadingMembers = ref(false)
const fetchProjectMembers = async () => {
  if (!activeProjectId.value) {
    projectMembers.value = []
    return
  }
  loadingMembers.value = true
  try {
    const res = await axiosClient.get(`/projects/${activeProjectId.value}/members`)
    projectMembers.value = res.data?.data || []
  } catch (error) {
    console.error('Cannot load project members:', error)
    projectMembers.value = []
  } finally {
    loadingMembers.value = false
  }
}

</script>


<style scoped>
.action-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: transparent;
  border: none;
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: all 0.2s;
}

.action-btn:hover {
  background-color: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.server-bar {
  width: 72px;
  background-color: var(--color-surface-hover, #f1f5f9);
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
  background-color: var(--color-primary, #6366f1);
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
  width: 100%;
  height: calc(100vh - 64px);
  margin: 0;
  background-color: var(--color-surface);
  border: none;
  border-radius: 0;
  overflow: hidden;
  box-shadow: none;
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
  background-color: var(--color-surface-hover, #f8fafc);
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
  display: block;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
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

.message-header-line {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin-bottom: 4px;
}

.message-time {
  font-size: 11px;
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

.call-workspace-body {
  flex: 1;
  min-height: 0;
  overflow: auto;
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding: 18px;
  background: #0f172a;
}

.call-presentation-stage {
  position: relative;
  min-height: 310px;
  flex: 1 1 auto;
  display: flex;
  flex-direction: column;
  gap: 10px;
  overflow: hidden;
  border: 1px solid rgba(148, 163, 184, 0.18);
  border-radius: 16px;
  background: #080d16;
  box-shadow: 0 16px 36px rgba(2, 6, 23, 0.26);
}

.call-presentation-stage.is-focused {
  min-height: min(68vh, 720px);
}

.call-presentation-stage:fullscreen {
  padding: 24px;
  border: 0;
  border-radius: 0;
  background: #05070b;
}

.presentation-heading {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 42px;
  padding: 0 14px;
  color: #e2e8f0;
  font-size: 13px;
}

.presentation-live-dot {
  width: 7px;
  height: 7px;
  flex: 0 0 auto;
  border-radius: 50%;
  background: #4ade80;
  box-shadow: 0 0 0 4px rgba(74, 222, 128, 0.12);
}

.presentation-hint {
  margin-left: auto;
  color: #64748b;
  font-size: 11px;
}

.presentation-screen {
  min-height: 250px;
  flex: 1;
  width: 100%;
  padding: 0;
  border: 0;
  background: #02040a;
  cursor: zoom-in;
  transition: background-color 160ms ease-out, transform 160ms ease-out;
}

.presentation-screen:active {
  transform: scale(0.995);
}

.presentation-screen video {
  display: block;
  width: 100%;
  height: 100%;
  min-height: 250px;
  object-fit: contain;
}

.presentation-toolbar {
  display: flex;
  justify-content: center;
  gap: 8px;
  padding: 0 14px 14px;
}

.presentation-control,
.call-control-label-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 7px;
  min-height: 34px;
  padding: 0 11px;
  border: 1px solid rgba(148, 163, 184, 0.2);
  border-radius: 9px;
  background: #1e293b;
  color: #cbd5e1;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: background-color 160ms ease-out, border-color 160ms ease-out, color 160ms ease-out, transform 160ms ease-out;
}

.presentation-control:hover,
.presentation-control:focus-visible,
.call-control-label-btn:hover,
.call-control-label-btn:focus-visible {
  border-color: rgba(96, 165, 250, 0.55);
  background: #273449;
  color: #f8fafc;
  outline: none;
}

.presentation-control:active,
.call-control-label-btn:active {
  transform: scale(0.97);
}

.call-grid-empty {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  min-height: 260px;
  color: #64748b;
  font-size: 13px;
}

.call-grid-empty i {
  color: #475569;
  font-size: 18px;
}

.call-participant-rail {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 10px;
  max-height: 142px;
  overflow: auto;
}

.call-participant-thumb {
  min-width: 0;
  overflow: hidden;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 11px;
  background: #111827;
  transition: border-color 160ms ease-out, transform 160ms ease-out;
}

.call-participant-thumb.is-presenter {
  border-color: rgba(74, 222, 128, 0.65);
}

.call-participant-thumb:hover {
  transform: translateY(-1px);
  border-color: rgba(148, 163, 184, 0.35);
}

.call-thumb-media {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  aspect-ratio: 16 / 9;
  overflow: hidden;
  background: #0b1220;
}

.call-thumb-media video {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.call-thumb-media audio {
  display: none;
}

.call-thumb-caption {
  display: flex;
  align-items: center;
  gap: 6px;
  min-height: 30px;
  padding: 0 8px;
  color: #cbd5e1;
  font-size: 11px;
}

.call-thumb-caption > .truncate {
  min-width: 0;
  flex: 1;
}

.call-thumb-caption > i {
  color: #f87171;
  font-size: 10px;
}

.presenter-tag {
  flex: 0 0 auto;
  color: #86efac;
  font-size: 10px;
  white-space: nowrap;
}

.call-controls-row {
  display: flex;
  justify-content: center;
  flex: 0 0 auto;
}

.call-control-dock {
  position: relative;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 12px;
  border: 1px solid rgba(148, 163, 184, 0.14);
  border-radius: 15px;
  background: #111827;
  box-shadow: 0 12px 24px rgba(2, 6, 23, 0.28);
}

.camera-effects-control {
  position: relative;
}

.call-control-label-btn {
  min-width: 72px;
  height: 40px;
  border-color: transparent;
  border-radius: 20px;
}

.call-control-label-btn.active {
  border-color: rgba(96, 165, 250, 0.55);
  color: #bfdbfe;
}

.camera-effects-menu {
  position: absolute;
  z-index: 6;
  bottom: calc(100% + 10px);
  left: 50%;
  width: 210px;
  padding: 8px;
  transform: translateX(-50%);
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 12px;
  background: #111827;
  box-shadow: 0 16px 32px rgba(2, 6, 23, 0.35);
}

.camera-effects-title {
  padding: 5px 8px 7px;
  color: #94a3b8;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.camera-effects-menu > button {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  min-height: 34px;
  padding: 0 8px;
  border: 0;
  border-radius: 7px;
  background: transparent;
  color: #cbd5e1;
  text-align: left;
  cursor: pointer;
}

.camera-effects-menu > button:hover,
.camera-effects-menu > button.selected {
  background: #1e293b;
  color: #f8fafc;
}

.camera-effects-menu > button:disabled {
  cursor: wait;
  opacity: 0.65;
}

.effect-radio {
  width: 10px;
  height: 10px;
  border: 1px solid #64748b;
  border-radius: 50%;
}

.selected .effect-radio {
  border: 3px solid #60a5fa;
}

.camera-effects-notice {
  margin: 7px 8px 3px;
  color: #fbbf24;
  font-size: 11px;
  line-height: 1.35;
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
  width: 44px;
  height: 44px;
  border-radius: 50%;
  background-color: #2b2d31;
  border: none;
  color: #dbdee1;
  font-size: 16px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color 0.15s, color 0.15s;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.25);
}

.call-control-circle-btn:hover {
  background-color: #35373c;
  color: #ffffff;
}

.call-control-circle-btn.inactive {
  background-color: #f23f43 !important;
  color: #ffffff !important;
}

.call-control-circle-btn.inactive:hover {
  background-color: #db3737 !important;
}

.call-control-circle-btn.hang-up {
  background-color: #f23f43 !important;
  color: #ffffff !important;
}

.call-control-circle-btn.hang-up:hover {
  background-color: #db3737 !important;
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

  .call-workspace-body {
    padding: 10px;
  }

  .call-presentation-stage {
    min-height: 240px;
  }

  .presentation-hint {
    display: none;
  }

  .presentation-toolbar {
    flex-wrap: wrap;
  }

  .call-participant-rail {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    max-height: 176px;
  }

  .call-control-dock {
    gap: 6px;
    padding-inline: 8px;
  }

  .call-control-label-btn {
    min-width: 42px;
    padding-inline: 8px;
  }

  .call-control-label-btn span {
    display: none;
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

.connected-voice-panel {
  background-color: var(--color-surface-hover, #f1f5f9);
  border: 1px solid var(--color-border, #e2e8f0);
  border-radius: 8px;
  padding: 12px;
  margin-top: auto;
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05);
}
.disconnect-btn-round {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background-color: #ef4444;
  color: #ffffff;
  border: none;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
}
.disconnect-btn-round:hover {
  background-color: #db3737;
  color: #ffffff;
}
.voice-action-btn-small {
  width: 32px;
  height: 32px;
  border-radius: 6px;
  background-color: transparent;
  color: var(--color-text-secondary);
  border: none;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
}
.voice-action-btn-small:hover {
  background-color: var(--color-surface-hover);
  color: var(--color-text-primary);
}
.voice-action-btn-small.active {
  background-color: rgba(99, 102, 241, 0.1);
  color: var(--color-primary);
}
</style>

<style scoped>
.chat-workspace {
  --chat-ink: #e8eef7;
  --chat-muted: #91a2b8;
  --chat-line: rgba(148, 163, 184, 0.14);
  --chat-surface: #0b1422;
  --chat-surface-2: #101d2e;
  --chat-accent: #58b7e8;
  --chat-context-width: 264px;
  position: relative;
  display: grid !important;
  grid-template-columns: 68px 248px minmax(0, 1fr) !important;
  width: min(1440px, calc(100% - 32px)) !important;
  height: min(820px, calc(100dvh - 112px));
  min-height: 620px;
  margin: 20px auto 28px !important;
  overflow: hidden;
  border: 1px solid var(--chat-line);
  border-radius: 18px;
  background: #08111e;
  color: var(--chat-ink);
  box-shadow: 0 24px 70px rgba(2, 8, 23, 0.34);
}

.chat-workspace.has-context-panel .chat-main { padding-right: var(--chat-context-width); }
.chat-workspace .server-bar,
.chat-workspace .chat-sidebar,
.chat-workspace .chat-main { min-width: 0; min-height: 0; }
.chat-workspace .server-bar { width: auto !important; padding: 16px 10px !important; border-right: 1px solid var(--chat-line); background: #07101c; }
.rail-caption { margin: 0 0 14px; color: #60748d; font-size: 9px; font-weight: 800; letter-spacing: .13em; text-align: center; }
.chat-workspace .server-icon-wrapper { width: 46px; height: 46px; margin: 0 auto 10px; }
.chat-workspace .server-icon { border: 1px solid rgba(88, 183, 232, .2); border-radius: 13px; background: #12253a; color: #bde9ff; font-weight: 700; transition: transform 180ms ease-out, background-color 180ms ease-out; }
.chat-workspace .server-icon-wrapper:hover .server-icon,
.chat-workspace .server-icon-wrapper.active .server-icon { background: #185174; color: #fff; transform: translateY(-1px); }
.chat-workspace .chat-sidebar { display: flex; flex-direction: column; width: auto !important; padding: 18px 14px 12px !important; border-right: 1px solid var(--chat-line); background: var(--chat-surface); }
.chat-workspace .sidebar-header { border-bottom-color: var(--chat-line) !important; }
.chat-workspace .eyebrow, .context-kicker { color: #6e91ac !important; font-size: 9px !important; font-weight: 800; letter-spacing: .13em; }
.workspace-mark { display: inline-grid; width: 24px; height: 24px; place-items: center; border-radius: 7px; background: #1c6d95; color: #e8f7ff; font-size: 12px; font-weight: 800; }
.workspace-back-button { display: inline-flex; gap: 5px; align-items: center; border: 0; background: transparent; color: var(--chat-muted); font-size: 11px; cursor: pointer; }
.workspace-back-button:hover { color: var(--chat-ink); }
.chat-workspace .section-title { color: #7389a0; font-size: 10px; letter-spacing: .1em; }
.chat-workspace .list-item { min-height: 34px; border-radius: 7px; color: #aebdd0; transition: background-color 160ms ease-out, color 160ms ease-out, transform 160ms ease-out; }
.chat-workspace .list-item:hover { background: rgba(88, 183, 232, .08); color: #eef8ff; transform: translateX(1px); }
.chat-workspace .list-item.active { background: rgba(88, 183, 232, .16); color: #f4fbff; box-shadow: inset 2px 0 #58b7e8; }
.chat-workspace .voice-item .item-icon { color: #6bc5a5 !important; }
.chat-workspace .voice-users-list { opacity: .9; }
.chat-workspace .direct-section { margin-top: 18px; }
.chat-workspace .direct-item { gap: 9px; }
.chat-workspace .presence-dot, .context-status-dot, .ai-state-indicator { width: 8px; height: 8px; flex: 0 0 auto; border-radius: 50%; background: #63d29f; box-shadow: 0 0 0 3px rgba(99, 210, 159, .12); }
.chat-workspace .presence-dot.is-idle { background: #63758c; box-shadow: none; }
.chat-workspace .connected-voice-panel { margin: 12px 0 0 !important; border: 1px solid rgba(99, 210, 159, .22); border-radius: 10px; background: rgba(25, 75, 66, .22); box-shadow: none; }
.chat-workspace .chat-main { position: relative; display: flex; flex-direction: column; width: auto !important; background: #0a1422; }
.chat-workspace .chat-header { min-height: 70px; padding: 14px 20px !important; border-bottom: 1px solid var(--chat-line); background: #0c1828; }
.chat-workspace .active-info { min-width: 0; }
.chat-workspace .active-icon { display: inline-grid; width: 30px; height: 30px; place-items: center; border-radius: 8px; background: rgba(88, 183, 232, .12); color: var(--chat-accent); font-size: 17px; }
.chat-workspace .chat-header h4 { margin: 0; color: #edf7ff; font-size: 15px; }
.chat-workspace .chat-header p { color: var(--chat-muted); font-size: 11px; }
.chat-workspace .header-actions { gap: 7px; }
.chat-workspace .action-btn { border: 1px solid transparent; border-radius: 7px; color: #91a8bd; transition: background-color 160ms ease-out, color 160ms ease-out, transform 120ms ease-out; }
.chat-workspace .action-btn:hover { background: rgba(88, 183, 232, .1); color: #ecf9ff; }
.chat-workspace .action-btn:active, .chat-workspace .ai-entry-button:active, .chat-workspace button:active { transform: scale(.97); }
.chat-workspace .action-btn:focus-visible, .chat-workspace button:focus-visible, .chat-workspace textarea:focus-visible { outline: 2px solid var(--chat-accent); outline-offset: 2px; }
.ai-entry-button { display: inline-flex; align-items: center; gap: 7px; min-height: 32px; padding: 0 10px; border: 1px solid rgba(88, 183, 232, .25); border-radius: 7px; background: rgba(88, 183, 232, .08); color: #bfeaff; font-size: 11px; font-weight: 700; cursor: pointer; transition: background-color 160ms ease-out, border-color 160ms ease-out, transform 120ms ease-out; }
.ai-entry-button:hover, .ai-entry-button.is-open { border-color: rgba(88, 183, 232, .58); background: rgba(88, 183, 232, .16); }
.ai-off-state { color: #86a0b2; font-size: 9px; letter-spacing: .08em; }
.chat-workspace .messages-thread { background: #0a1422; }
.chat-workspace .message-card { border-bottom: 1px solid rgba(148, 163, 184, .08); padding: 13px 22px; }
.chat-workspace .message-card:hover { background: rgba(255, 255, 255, .018); }
.chat-workspace .message-body { color: #d7e2ee; line-height: 1.55; }
.empty-chat-state { display: flex; flex-direction: column; align-items: center; gap: 8px; max-width: 360px; margin: auto; color: #7f94aa; text-align: center; }
.empty-chat-state strong { color: #dbeaf5; font-size: 15px; }
.empty-state-icon { display: grid; width: 42px; height: 42px; place-items: center; border: 1px solid rgba(88, 183, 232, .3); border-radius: 12px; color: var(--chat-accent); font-size: 23px; }
.chat-workspace .chat-input-area { margin: 12px 18px 16px; border: 1px solid var(--chat-line); border-radius: 11px; background: #101d2e; box-shadow: 0 10px 30px rgba(2, 8, 23, .18); }
.chat-workspace .chat-input { color: #e7f2fb; }
.chat-workspace .chat-input::placeholder { color: #72869b; }
.chat-workspace .btn-send { background: #1c6d95; color: #f1fbff; transition: background-color 160ms ease-out, transform 120ms ease-out; }
.chat-workspace .btn-send:hover:not(:disabled) { background: #2687b4; }
.chat-workspace .call-header + div { min-height: 0 !important; background: #07111e !important; }
.chat-workspace .call-error { margin-top: 5px; color: #ff9aa7; font-size: 11px; line-height: 1.3; }
.chat-workspace .group-video-grid { display: grid !important; grid-auto-flow: dense; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); align-content: start !important; align-items: stretch !important; justify-content: stretch !important; gap: 12px !important; padding: 8px; }
.chat-workspace .group-video-grid .video-feed { width: 100% !important; max-width: none !important; min-height: 150px; border: 1px solid rgba(148, 163, 184, .16) !important; border-radius: 12px !important; }
.chat-workspace .call-control-circle-btn { transition: background-color 160ms ease-out, color 160ms ease-out, transform 120ms ease-out; }
.chat-workspace .call-control-circle-btn.hang-up { background: #bd4d5c; }
.chat-workspace .call-control-circle-btn.hang-up:hover { background: #d35d6c; }
.chat-context-panel { position: absolute; z-index: 4; top: 0; right: 0; bottom: 0; display: flex; width: var(--chat-context-width); flex-direction: column; border-left: 1px solid var(--chat-line); background: #0c1828; }
.context-panel-header, .ai-surface-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 10px; padding: 18px 16px 12px; border-bottom: 1px solid var(--chat-line); }
.context-panel-header h3, .ai-surface-header h3 { margin: 4px 0 0; color: #e8f4fc; font-size: 14px; }
.context-close { display: grid; width: 28px; height: 28px; place-items: center; border: 0; border-radius: 6px; background: transparent; color: #8297aa; cursor: pointer; transition: background-color 160ms ease-out, color 160ms ease-out, transform 120ms ease-out; }
.context-close:hover { background: rgba(255,255,255,.07); color: #fff; }
.context-tabs { display: flex; gap: 4px; padding: 10px 12px; border-bottom: 1px solid var(--chat-line); }
.context-tab { flex: 1; padding: 7px 5px; border: 0; border-radius: 6px; background: transparent; color: #8297aa; font-size: 11px; cursor: pointer; }
.context-tab.is-active, .context-tab:hover { background: rgba(88, 183, 232, .1); color: #dff5ff; }
.context-call-summary { display: flex; align-items: center; gap: 10px; margin: 14px 12px 8px; padding: 10px; border: 1px solid rgba(99, 210, 159, .2); border-radius: 8px; background: rgba(99, 210, 159, .06); }
.context-call-summary div, .context-member-copy { display: flex; min-width: 0; flex: 1; flex-direction: column; gap: 2px; }
.context-call-summary strong, .context-member-copy strong { overflow: hidden; color: #e7f2fb; font-size: 11px; text-overflow: ellipsis; white-space: nowrap; }
.context-call-summary span:last-child, .context-member-copy span { color: #8196aa; font-size: 10px; }
.context-member-list { display: flex; min-height: 0; flex-direction: column; gap: 3px; overflow-y: auto; padding: 8px 10px; }
.context-member-row { display: flex; align-items: center; gap: 9px; min-height: 42px; padding: 5px 6px; border-radius: 7px; color: #c4d3df; font-size: 11px; }
.context-member-row:hover { background: rgba(255,255,255,.04); }
.context-member-row > i { color: #d35d6c; font-size: 10px; }
.context-empty { padding: 22px 10px; color: #8095a8; font-size: 11px; line-height: 1.5; text-align: center; }
.ai-analysis-surface { position: absolute; z-index: 8; top: 76px; right: calc(var(--chat-context-width) + 12px); width: min(330px, calc(100% - 32px)); padding-bottom: 16px; border: 1px solid rgba(88, 183, 232, .28); border-radius: 12px; background: #112338; box-shadow: 0 20px 50px rgba(2, 8, 23, .45); }
.ai-analysis-surface p { padding: 0 16px; color: #a9bdce; font-size: 12px; line-height: 1.55; }
.ai-off-banner { display: flex; align-items: center; gap: 8px; margin: 14px 16px 0; color: #dcebf3; font-size: 12px; }
.ai-state-indicator { background: #657a8b; box-shadow: none; }
.ai-capability-list { display: grid; grid-template-columns: repeat(2, 1fr); gap: 6px; padding: 0 16px; }
.ai-capability-list span { padding: 7px 8px; border: 1px solid rgba(148, 163, 184, .12); border-radius: 6px; color: #99afc1; font-size: 10px; }
.ai-disabled-action { display: block; width: calc(100% - 32px); margin: 14px 16px 0; padding: 9px; border: 1px solid rgba(148, 163, 184, .16); border-radius: 7px; background: rgba(255,255,255,.04); color: #7890a4; font-size: 11px; }

@media (max-width: 1120px) {
  .chat-workspace { grid-template-columns: 60px 220px minmax(0, 1fr) !important; }
  .chat-workspace.has-context-panel .chat-main { padding-right: 0; }
  .chat-context-panel { width: min(264px, 78%); box-shadow: -16px 0 34px rgba(2, 8, 23, .3); }
  .ai-analysis-surface { right: 12px; }
}
@media (max-width: 760px) {
  .chat-workspace { width: 100% !important; height: calc(100dvh - 76px); min-height: 520px; margin: 0 !important; border-radius: 0; border-inline: 0; grid-template-columns: 54px minmax(0, 1fr) !important; }
  .chat-workspace .chat-sidebar { position: absolute; z-index: 6; top: 0; bottom: 0; left: 54px; width: 248px !important; transform: translateX(-100%); transition: transform 200ms cubic-bezier(.32,.72,0,1); }
  .chat-workspace:focus-within .chat-sidebar, .chat-workspace .chat-sidebar:hover { transform: translateX(0); }
  .chat-workspace .chat-main { grid-column: 2; padding-right: 0 !important; }
  .chat-workspace .server-bar { grid-column: 1; }
  .chat-workspace .message-card { padding-inline: 14px; }
  .chat-workspace .ai-entry-button span:not(.ai-off-state) { display: none; }
  .chat-workspace .ai-entry-button { width: 32px; padding: 0; justify-content: center; }
  .chat-context-panel { width: min(280px, 88vw); }
  .chat-workspace .group-video-grid { grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); }
}
@media (prefers-reduced-motion: reduce) {
  .chat-workspace *, .chat-workspace *::before, .chat-workspace *::after { transition-duration: .01ms !important; animation-duration: .01ms !important; }
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

/* Custom dialog style for group call to match Discord */
.group-call-dialog.video-call-dialog {
  background-color: #111214 !important;
  border-radius: 12px !important;
  border: 1px solid rgba(255, 255, 255, 0.05) !important;
  box-shadow: 0 24px 50px rgba(0, 0, 0, 0.6) !important;
  overflow: hidden;
}
.group-call-dialog.video-call-dialog .el-dialog__header {
  background-color: #111214 !important;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05) !important;
  padding: 14px 20px !important;
  margin-right: 0 !important;
}
.group-call-dialog.video-call-dialog .el-dialog__headerbtn .el-dialog__close {
  color: #94a3b8 !important;
}
.group-call-dialog.video-call-dialog .el-dialog__headerbtn:hover .el-dialog__close {
  color: #ffffff !important;
}
.group-call-dialog.video-call-dialog .el-dialog__body {
  background-color: #111214 !important;
  padding: 16px !important;
}
.group-call-dialog.video-call-dialog .el-dialog__footer {
  background-color: #0b0c0e !important;
  border-top: 1px solid rgba(255, 255, 255, 0.05) !important;
  padding: 12px 20px !important;
}
</style>
