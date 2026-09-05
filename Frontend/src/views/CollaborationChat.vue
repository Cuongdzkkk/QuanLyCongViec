<template>
  <main
    class="chat-container chat-workspace relative"
    :class="{ 'has-context-panel': showMembersSidebar && !showVoiceCallMain, 'is-sidebar-open': sidebarOpen }"
    aria-label="Không gian cộng tác SprintA"
    @dragover="handleDragOver"
    @dragleave="handleDragLeave"
    @drop="handleDropFile"
  >
    <button
      v-if="sidebarOpen"
      type="button"
      class="chat-sidebar-backdrop"
      aria-label="Đóng danh sách kênh"
      @click="sidebarOpen = false"
    ></button>

    <!-- Unified project and channel navigator -->
    <aside class="chat-sidebar" aria-label="Project and channel navigation">
      <div class="navigator-header">
        <div class="navigator-heading">
          <span class="eyebrow">SPRINTA / COLLABORATION</span>
          <span class="navigator-count">{{ projectOptions.length }} projects</span>
        </div>
        <label class="navigator-search">
          <i class="fa-solid fa-magnifying-glass" aria-hidden="true"></i>
          <input v-model="navigatorQuery" type="search" placeholder="Tìm dự án hoặc kênh" aria-label="Tìm dự án hoặc kênh" />
          <button v-if="navigatorQuery" type="button" class="navigator-search-clear" aria-label="Xóa tìm kiếm" @click="navigatorQuery = ''"><i class="fa-solid fa-xmark" aria-hidden="true"></i></button>
        </label>
      </div>

      <div class="sidebar-lists-scrollable navigator-list">
        <div v-if="projectsLoading" class="channel-state" role="status"><i class="fa-solid fa-spinner fa-spin"></i><span>Đang tải Project...</span></div>
        <div v-else-if="projectsError" class="channel-state channel-state-error" role="alert"><span>{{ projectsError }}</span><button type="button" class="state-action" @click="retryProjects">Thử lại</button></div>
        <div v-else-if="!filteredProjects.length" class="channel-state navigator-empty">Không tìm thấy dự án hoặc kênh phù hợp.</div>

        <section v-for="project in filteredProjects" :key="project.id" class="project-accordion" :class="{ 'is-expanded': expandedProjectId === project.id, 'is-active': activeProjectId === project.id }">
          <button type="button" class="project-row" :aria-expanded="expandedProjectId === project.id" :aria-current="activeProjectId === project.id ? 'page' : undefined" @click="toggleProject(project.id)">
            <span class="project-chevron" aria-hidden="true"><i class="fa-solid fa-chevron-right"></i></span>
            <span class="project-avatar" aria-hidden="true">{{ project.name.charAt(0).toUpperCase() }}</span>
            <span class="project-name">{{ project.name || 'Project' }}</span>
            <span v-if="projectUnreadCount(project) > 0" class="collaboration-unread-badge" role="status" :aria-label="`${projectUnreadCount(project)} tin nhắn chưa đọc trong ${project.name}`">{{ formatUnreadCount(projectUnreadCount(project)) }}</span>
          </button>

          <div v-if="expandedProjectId === project.id" class="project-channel-tree">
            <div v-if="project.id === activeProjectId && channelsLoading && !projectChannels(project.id).length" class="channel-state" role="status"><i class="fa-solid fa-spinner fa-spin"></i><span>Đang tải Channel...</span></div>
            <div v-else-if="project.id === activeProjectId && channelsError" class="channel-state channel-state-error" role="alert"><span>{{ channelsError }}</span><button type="button" class="state-action" @click="retryChannels">Thử lại</button></div>

            <div class="navigator-section-head">
              <span class="section-title">TEXT CHANNELS</span>
              <button v-if="canManageChannels" type="button" class="add-btn-small" title="Tạo Channel" aria-label="Tạo Channel" :disabled="project.id !== activeProjectId || channelsLoading" @click="openCreateChannelModal">
                <i class="fa-solid fa-plus text-xs"></i>
              </button>
            </div>
            <div class="section-list">
              <div v-if="!projectChannels(project.id).length && project.id === activeProjectId && !channelsLoading && !channelsError" class="channel-state">Chưa có channel trong project này.</div>
              <button v-for="ch in channelsForNavigator(project)" :key="ch.id" type="button" class="list-item navigator-channel" :class="{ active: activeChat?.id === ch.id && activeChat?.type === 'channel' }" @click="selectChat(ch, 'channel')">
                <span class="item-icon">#</span><span class="item-name truncate">{{ ch.name }}</span>
                <span v-if="ch.unreadCount > 0" class="collaboration-unread-badge" role="status" aria-live="polite" :aria-label="`${ch.unreadCount} tin nhắn chưa đọc trong Channel ${ch.name}`">{{ formatUnreadCount(ch.unreadCount) }}</span>
              </button>
              <button v-if="project.id === activeProjectId && channels.length < channelPagination.totalCount && !navigatorQuery" type="button" class="state-action load-more-action" :disabled="channelsLoadingMore" @click="loadMoreChannels">{{ channelsLoadingMore ? 'Đang tải...' : 'Tải thêm Channel' }}</button>
            </div>

            <div class="navigator-section-head voice-section-head">
              <span class="section-title">KÊNH THOẠI (VOICE)</span>
              <button v-if="canManageChannels" type="button" class="add-btn-small" title="Tạo kênh thoại mới" aria-label="Tạo kênh thoại mới" :disabled="project.id !== activeProjectId" @click="openCreateVoiceModal">
                <i class="fa-solid fa-plus text-xs"></i>
              </button>
            </div>
            <div class="section-list">
              <div v-for="vc in voiceChannelsForProject(project)" :key="vc.id" class="voice-item-wrapper">
                <button type="button" class="list-item voice-item navigator-channel" :class="{ active: activeVoiceChannel?.id === vc.id }" @click="openPreJoinVoiceChannel(vc)">
                  <span class="item-icon"><i class="fa-solid fa-volume-high"></i></span><span class="item-name">{{ vc.name }}</span>
                </button>
                <div v-if="vc.id === activeVoiceChannel?.id && participantsInCall.length" class="voice-users-list">
                  <div v-for="user in participantsInCall" :key="user.connectionId" class="voice-user">
                    <el-avatar :size="16" :src="user.avatarUrl">{{ user.displayName.charAt(0) }}</el-avatar>
                    <span class="truncate">{{ user.displayName }}</span>
                  </div>
                </div>
              </div>
              <div v-if="!voiceChannelsForProject(project).length" class="channel-state">Chưa có kênh thoại trong project này.</div>
            </div>
          </div>
        </section>

        <section v-if="directConversations.length" class="direct-section navigator-direct-section">
          <div class="navigator-section-head"><span class="section-title">TIN NHẮN TRỰC TIẾP</span></div>
          <div class="section-list"><button v-for="conversation in directConversations" :key="conversation.id" type="button" class="list-item direct-item" :class="{ active: activeChat?.id === conversation.id && activeChat?.type === 'dm' }" @click="selectChat(conversation, 'dm')"><el-avatar :size="24" :src="conversation.avatar">{{ conversation.name?.charAt(0) }}</el-avatar><span class="item-name truncate">{{ conversation.name }}</span><span v-if="conversation.unreadCount > 0" class="collaboration-unread-badge">{{ formatUnreadCount(conversation.unreadCount) }}</span></button></div>
        </section>
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
          <button type="button" class="disconnect-btn-round" title="Ngắt kết nối" aria-label="Ngắt kết nối khỏi kênh thoại" @click.stop="leaveVoiceChannel">
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
    </aside>

    <!-- Active Chat Area -->
    <div class="chat-main">
      <section v-if="workspaceState === 'VOICE_PRE_JOIN'" class="call-prejoin-panel" :class="{ 'is-camera-on': preJoinCameraEnabled, 'is-camera-off': !preJoinCameraEnabled }" aria-labelledby="call-prejoin-title">
        <div class="call-prejoin-copy">
          <span class="context-kicker">PRE-JOIN</span>
          <h2 id="call-prejoin-title">Tham gia {{ preJoinVoiceChannel.name }}</h2>
          <p>Kiểm tra thiết bị trước khi vào phòng. Bạn có thể tham gia khi tắt microphone và camera.</p>
        </div>
        <div class="call-prejoin-layout">
          <div class="call-prejoin-preview" aria-live="polite">
            <video v-if="preJoinCameraEnabled" ref="preJoinVideo" class="call-prejoin-video" autoplay muted playsinline aria-label="Xem trước camera"></video>
            <div v-else class="call-prejoin-camera-off" aria-label="Camera đang tắt">
              <el-avatar :size="80" :src="currentUser.avatar">{{ (currentUser.name || 'U').charAt(0).toUpperCase() }}</el-avatar>
              <strong>Camera đang tắt</strong>
              <span>Bạn có thể bật camera trước khi tham gia.</span>
            </div>
          </div>
          <div class="call-prejoin-settings">
            <div class="call-prejoin-group">
              <span class="call-prejoin-group-title">Thiết bị trong cuộc gọi</span>
              <div class="call-prejoin-controls">
                <button type="button" class="call-prejoin-toggle" :class="{ active: preJoinMicEnabled }" @click="preJoinMicEnabled = !preJoinMicEnabled"><i :class="preJoinMicEnabled ? 'fa-solid fa-microphone' : 'fa-solid fa-microphone-slash'" aria-hidden="true"></i>{{ preJoinMicEnabled ? 'Microphone bật' : 'Microphone tắt' }}</button>
                <button type="button" class="call-prejoin-toggle" :class="{ active: preJoinCameraEnabled }" @click="togglePreJoinCamera"><i :class="preJoinCameraEnabled ? 'fa-solid fa-video' : 'fa-solid fa-video-slash'" aria-hidden="true"></i>{{ preJoinCameraEnabled ? 'Camera bật' : 'Camera tắt' }}</button>
              </div>
            </div>
            <div class="call-prejoin-group">
              <span class="call-prejoin-group-title">Chọn thiết bị</span>
              <div class="call-prejoin-device-grid">
                <label for="prejoin-microphone">Microphone
                  <div class="call-prejoin-select-wrapper">
                    <i class="fa-solid fa-microphone call-prejoin-select-icon" aria-hidden="true"></i>
                    <select id="prejoin-microphone" v-model="preJoinMicrophoneId">
                      <option value="">Thiết bị mặc định</option>
                      <option v-for="device in audioInputDevices" :key="device.deviceId" :value="device.deviceId">{{ device.label || 'Microphone' }}</option>
                    </select>
                    <i class="fa-solid fa-chevron-down call-prejoin-select-chevron" aria-hidden="true"></i>
                  </div>
                </label>
                <label for="prejoin-camera">Camera
                  <div class="call-prejoin-select-wrapper">
                    <i class="fa-solid fa-video call-prejoin-select-icon" aria-hidden="true"></i>
                    <select id="prejoin-camera" v-model="preJoinCameraId" @change="switchPreJoinCamera">
                      <option value="">Thiết bị mặc định</option>
                      <option v-for="device in videoInputDevices" :key="device.deviceId" :value="device.deviceId">{{ device.label || 'Camera' }}</option>
                    </select>
                    <i class="fa-solid fa-chevron-down call-prejoin-select-chevron" aria-hidden="true"></i>
                  </div>
                </label>
              </div>
            </div>
            <div class="call-prejoin-actions"><button type="button" class="secondary-button" @click="cancelPreJoin">Hủy</button><button type="button" class="primary-button" :disabled="voiceJoinPending" @click="confirmJoinVoiceChannel">Tham gia</button></div>
          </div>
        </div>
      </section>

      <section v-else-if="workspaceState === 'VOICE_JOINING'" class="call-prejoin-panel call-prejoin-joining" aria-live="polite" aria-labelledby="call-joining-title">
        <div class="call-prejoin-copy">
          <span class="context-kicker">VOICE CHANNEL</span>
          <h2 id="call-joining-title">Đang vào phòng thoại</h2>
        <p>{{ voiceJoiningChannelName || activeVoiceChannel?.name || 'Kênh thoại' }} đang kết nối. Vui lòng đợi xác nhận tham gia.</p>
        </div>
        <div class="call-prejoin-joining-status"><i class="fa-solid fa-circle-notch fa-spin" aria-hidden="true"></i><span>Đang kết nối…</span></div>
        <div class="call-prejoin-actions"><button type="button" class="secondary-button" @click="cancelVoiceJoining">Hủy</button></div>
      </section>
      
      <!-- Embedded Voice Call View (Discord Style) -->
      <template v-else-if="workspaceState === 'VOICE_IN_CALL'">
        <header class="chat-header call-header">
          <div class="active-info">
            <button type="button" class="mobile-sidebar-trigger" aria-label="Mở danh sách kênh" title="Mở danh sách kênh" :aria-expanded="sidebarOpen" @click="sidebarOpen = !sidebarOpen">
              <i class="fa-solid fa-bars" aria-hidden="true"></i>
            </button>
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
            <button type="button" class="ai-entry-button transcript-entry-button" :class="{ 'is-open': showTranscriptPanel }" :disabled="!callTranscriptionCapabilities.configured" :aria-expanded="showTranscriptPanel" :aria-label="callAiButtonLabel" :title="callAiButtonLabel" @click="toggleTranscriptPanel">
              <i class="fa-solid fa-closed-captioning" aria-hidden="true"></i>
              <span>Biên bản</span>
              <span v-if="callAiState.state === 'ACTIVE'" class="call-header-status-dot" aria-label="Đang ghi"></span>
            </button>
            <button type="button" class="ai-entry-button meeting-ai-entry-button" :class="{ 'is-open': showTranscriptPanel && callTranscriptionCapabilities.aiConfigured }" :disabled="!callTranscriptionCapabilities.configured" :aria-label="`AI cuộc họp: ${callAiStateLabel}`" :title="`AI cuộc họp: ${callAiStateLabel}`" @click="openMeetingAi">
              <i class="fa-solid fa-wand-magic-sparkles" aria-hidden="true"></i>
              <span>AI cuộc họp</span>
              <span class="ai-off-state">{{ callAiStateLabel }}</span>
            </button>
            <button type="button" class="action-btn" aria-label="Mở danh sách người tham gia" title="Người tham gia" @click="openCallParticipants">
              <i class="fa-solid fa-circle-info" aria-hidden="true"></i>
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

        <div ref="meetingShell" class="call-workspace-body" :class="callLayoutClasses" @pointerdown.capture="resumeBlockedCallMedia">
          <section ref="presentationStage" class="call-presentation-stage" :class="{ 'is-focused': presentationFocused, 'is-fullscreen': presentationIsFullscreen }" :data-layout-mode="callLayoutMode" aria-label="Presentation stage">
            <template v-if="activePresenter && callViewMode !== 'tiled'">
              <div class="presentation-heading">
                <span class="presentation-live-dot" aria-hidden="true"></span>
                <strong>{{ activePresenter.displayName }} đang chia sẻ màn hình</strong>
                <span class="presentation-hint">Nội dung được giữ nguyên tỷ lệ</span>
              </div>
              <button type="button" class="presentation-screen" :aria-label="presentationFocused ? 'Thu nhỏ màn hình chia sẻ' : 'Phóng to màn hình chia sẻ'" @click="togglePresentationFocus">
                <video :ref="el => setPresentationVideoElement(el, activePresenter?.connectionId || '')" autoplay playsinline muted></video>
              </button>
              <div class="presentation-toolbar" role="toolbar" aria-label="Presentation controls">
                <button type="button" class="presentation-control" :title="presentationFocused ? 'Thu nhỏ' : 'Phóng to'" @click="togglePresentationFocus">
                  <i :class="presentationFocused ? 'fa-solid fa-compress' : 'fa-solid fa-expand'" aria-hidden="true"></i>
                  <span>{{ presentationFocused ? 'Quay lại bố cục' : 'Phóng to' }}</span>
                </button>
                <button type="button" class="presentation-control" :title="presentationIsFullscreen ? 'Thoát toàn màn hình' : 'Toàn màn hình'" @click="togglePresentationFullscreen">
                  <i :class="presentationIsFullscreen ? 'fa-solid fa-compress-arrows-alt' : 'fa-solid fa-expand-arrows-alt'" aria-hidden="true"></i>
                  <span>{{ presentationIsFullscreen ? 'Thoát toàn màn hình' : 'Toàn màn hình' }}</span>
                </button>
                <button v-if="presentationFocused" type="button" class="presentation-control" title="Về lưới người tham gia" @click="returnToParticipantGrid">
                  <i class="fa-solid fa-table-cells" aria-hidden="true"></i>
                  <span>Về lưới</span>
                </button>
                <button v-if="focusedParticipantConnectionId" type="button" class="presentation-control" title="Quay lại màn hình chia sẻ" @click="returnToPresentation">
                  <i class="fa-solid fa-display" aria-hidden="true"></i>
                  <span>Quay lại màn hình chia sẻ</span>
                </button>
              </div>
            </template>
            <div v-else-if="hasCallParticipants" class="call-camera-stage" :class="`layout-${callLayoutMode.toLowerCase()}`" :data-participant-count="visibleCallStageParticipants.length" aria-label="Call participants">
              <article
                v-for="user in visibleCallStageParticipants"
                :key="`stage-${user.connectionId}`"
                class="call-camera-stage-tile"
                :class="{ 'is-focused-participant': focusedParticipantConnectionId === user.connectionId, 'is-speaking': isParticipantSpeaking(user) }"
                tabindex="0"
                role="button"
                :aria-label="`Tập trung vào ${user.displayName}`"
                @click="focusParticipant(user.connectionId)"
                @keydown.enter.prevent="focusParticipant(user.connectionId)"
                @keydown.space.prevent="focusParticipant(user.connectionId)"
              >
                <video
                  v-if="user.connectionId === callConnectionId && isParticipantVideoVisible(user)"
                  :ref="el => setLocalVideoElement(el, 'stage')"
                  autoplay
                  playsinline
                  muted
                  :style="{ transform: isSharingScreen ? 'none' : 'scaleX(-1)' }"
                ></video>
                <video
                  v-else-if="user.connectionId !== callConnectionId && isParticipantVideoVisible(user)"
                  :ref="el => setRemoteVideoElement(el, user.connectionId, 'stage')"
                  autoplay
                  playsinline
                ></video>
                <div v-else class="call-camera-off-state" role="status" :aria-label="`${user.displayName}: camera đang tắt`">
                  <span class="call-camera-off-glow" aria-hidden="true"></span>
                  <el-avatar :size="96" :src="user.connectionId === callConnectionId ? (currentUser.avatar || user.avatarUrl) : user.avatarUrl">
                    {{ ((user.connectionId === callConnectionId ? (currentUser.name || user.displayName) : user.displayName) || 'U').charAt(0).toUpperCase() }}
                  </el-avatar>
                  <strong>{{ user.displayName }}{{ user.connectionId === callConnectionId ? ' (Bạn)' : '' }}</strong>
                  <span class="call-camera-off-label"><i class="fa-solid fa-video-slash" aria-hidden="true"></i> Camera đang tắt</span>
                  <small :class="{ 'is-muted': !user.microphoneEnabled }">
                    <i :class="user.microphoneEnabled ? 'fa-solid fa-microphone' : 'fa-solid fa-microphone-slash'" aria-hidden="true"></i>
                    {{ user.microphoneEnabled ? 'Microphone đang bật' : 'Microphone đang tắt' }}
                  </small>
                </div>
                <span v-if="isParticipantVideoVisible(user)" class="call-camera-stage-label">
                  {{ user.displayName }}{{ user.connectionId === callConnectionId ? ' (Bạn)' : '' }}
                  <span v-if="user.handRaised" class="call-hand-indicator" title="Đang giơ tay"><i class="fa-solid fa-hand" aria-hidden="true"></i><span>Đang giơ tay</span></span>
                </span>
                <span v-if="isParticipantVideoVisible(user) && !user.microphoneEnabled" class="call-camera-stage-muted" title="Đang tắt micro" aria-label="Đang tắt micro"><i class="fa-solid fa-microphone-slash" aria-hidden="true"></i></span>
              </article>
              <article
                v-if="callOverflowCount > 0"
                class="call-camera-stage-tile call-overflow-tile"
                role="button"
                tabindex="0"
                aria-label="Xem thêm người tham gia"
                @click="openCallParticipants"
                @keydown.enter.prevent="openCallParticipants"
                @keydown.space.prevent="openCallParticipants"
              >
                <div class="call-overflow-content">
                  <div class="call-overflow-avatars">
                    <el-avatar
                      v-for="user in participantsInCall.slice(visibleCallStageParticipants.length, visibleCallStageParticipants.length + 3)"
                      :key="`overflow-${user.connectionId}`"
                      :size="36"
                      :src="user.avatarUrl"
                    >
                      {{ (user.displayName || 'U').charAt(0).toUpperCase() }}
                    </el-avatar>
                  </div>
                  <strong>+{{ callOverflowCount }} người còn lại</strong>
                  <span class="call-overflow-hint">Nhấp để xem danh sách</span>
                </div>
              </article>
            </div>
            <div v-else class="call-grid-empty" aria-live="polite">
              <span class="call-grid-empty-icon"><i class="fa-solid fa-users-viewfinder" aria-hidden="true"></i></span>
              <strong>Phòng họp đã sẵn sàng</strong>
              <span>Người tham gia và nội dung chia sẻ sẽ xuất hiện tại đây.</span>
            </div>
            <LiveCaptionOverlay :enabled="captionsEnabled" :captions="liveCaptionRows" />
          </section>

          <section v-if="callRailParticipants.length" class="call-participant-rail" aria-label="Call participants">
            <article
              v-for="user in callRailParticipants"
              :key="user.connectionId"
              class="call-participant-thumb"
              :class="{ 'is-presenter': activePresenter?.connectionId === user.connectionId, 'is-speaking': isParticipantSpeaking(user), 'is-focused-participant': focusedParticipantConnectionId === user.connectionId }"
              tabindex="0"
              role="button"
              :aria-label="`Tập trung vào ${user.displayName}`"
              @click="focusParticipant(user.connectionId)"
              @keydown.enter.prevent="focusParticipant(user.connectionId)"
              @keydown.space.prevent="focusParticipant(user.connectionId)"
            >
              <div class="call-thumb-media">
                <video
                  v-if="user.connectionId === callConnectionId && isParticipantVideoVisible(user)"
                  :ref="el => setLocalVideoElement(el, 'rail')"
                  autoplay
                  playsinline
                  muted
                  :style="{ transform: isSharingScreen ? 'none' : 'scaleX(-1)' }"
                ></video>
                <video
                  v-else-if="user.connectionId !== callConnectionId && isParticipantVideoVisible(user)"
                  :ref="el => setRemoteVideoElement(el, user.connectionId, 'rail')"
                  autoplay
                  playsinline
                ></video>
                <el-avatar v-else :size="44" :src="user.connectionId === callConnectionId ? (currentUser.avatar || user.avatarUrl) : user.avatarUrl">
                  {{ ((user.connectionId === callConnectionId ? (currentUser.name || user.displayName) : user.displayName) || 'U').charAt(0).toUpperCase() }}
                </el-avatar>
              </div>
              <div class="call-thumb-caption">
                <span class="truncate">{{ user.displayName }}{{ user.connectionId === callConnectionId ? ' (Bạn)' : '' }}</span>
                <i v-if="!user.microphoneEnabled" class="fa-solid fa-microphone-slash" aria-label="Đang tắt micro"></i>
                <span v-if="activePresenter?.connectionId === user.connectionId" class="presenter-tag">Đang trình bày</span>
                <span v-if="user.handRaised" class="call-hand-indicator" title="Đang giơ tay"><i class="fa-solid fa-hand" aria-hidden="true"></i><span>Đang giơ tay</span></span>
              </div>
            </article>
          </section>

          <audio
            v-for="user in remoteAudioParticipants"
            :key="`remote-audio-${user.connectionId}`"
            :ref="el => setRemoteAudioElement(el, user.connectionId, 'output')"
            class="call-remote-audio-output"
            autoplay
            aria-hidden="true"
          ></audio>

          <aside v-if="showTranscriptPanel" class="call-transcript-panel" aria-label="Biên bản cuộc gọi">
            <div class="call-transcript-header">
              <div class="call-transcript-title"><span class="context-kicker">BIÊN BẢN</span><strong>Biên bản cuộc gọi</strong><small>{{ callTranscriptionCapabilities.provider }} · {{ callCaptionLanguageLabel }}</small></div>
              <span class="call-ai-state-pill" :class="`is-${callAiState.state.toLowerCase()}`">{{ callAiStateLabel }}</span>
            </div>
            <div v-if="callAiState.state === 'OFF'" class="call-transcript-off">
              <strong>{{ callTranscriptionCapabilities.configured ? 'Phụ đề đang tắt' : 'Phụ đề chưa được cấu hình' }}</strong>
              <p>{{ callTranscriptionCapabilities.configured ? 'Bật Phụ đề ở thanh điều khiển để xem lời nói trực tiếp trong cuộc gọi.' : 'Quản trị viên chưa cấu hình phiên âm cuộc họp. Bạn vẫn có thể tiếp tục cuộc gọi.' }}</p>
            </div>
            <div v-else-if="callAiState.state === 'WAITING_FOR_CONSENT' || callAiState.state === 'PAUSED_CONSENT'" class="call-transcript-consent">
              <strong>{{ callAiState.state === 'PAUSED_CONSENT' ? 'Đã tạm dừng' : 'Đang chờ quyền bật phụ đề' }}</strong>
              <p>Quyền bật phụ đề được xử lý trong hộp thoại xác nhận.</p>
            </div>
            <div v-else-if="callAiState.state === 'ACTIVE'" class="call-transcript-active">
              <div class="call-transcript-indicator"><span></span> Đang ghi</div>
              <button type="button" class="ai-secondary-action" @click="toggleCallCaptions">Tắt phụ đề</button>
            </div>
            <div v-else class="call-transcript-paused">
              <strong>{{ callAiState.state === 'ERROR' ? 'Không thể khởi động phiên âm' : 'AI đang tắt' }}</strong>
            </div>
            <section v-if="callTranscriptionCapabilities.aiConfigured" class="meeting-ai-report" aria-label="AI meeting report">
              <div class="meeting-ai-report-heading">
                <strong>Phân tích AI</strong>
                <span>{{ callMeetingAiReport?.status || 'Đang chờ đủ nội dung' }}</span>
              </div>
              <template v-if="callMeetingAiReport?.state">
                <p v-if="callMeetingAiReport.state.meetingSummaryDraft" class="meeting-ai-summary">{{ callMeetingAiReport.state.meetingSummaryDraft }}</p>
                <div v-if="callMeetingAiReport.state.decisions?.length" class="meeting-ai-group"><strong>Quyết định</strong><ul><li v-for="item in callMeetingAiReport.state.decisions" :key="item.text"><span>{{ item.text }}</span><small v-if="formatAiEvidence(item.evidenceChunkIds)">{{ formatAiEvidence(item.evidenceChunkIds) }}</small></li></ul></div>
                <div v-if="callMeetingAiReport.state.actionItems?.length" class="meeting-ai-group"><strong>Việc đề xuất — cần duyệt</strong><ul><li v-for="item in callMeetingAiReport.state.actionItems" :key="item.task"><span>{{ item.task }}<small v-if="item.proposedOwner"> · {{ item.proposedOwner }}</small></span><small v-if="formatAiEvidence(item.evidenceChunkIds)">{{ formatAiEvidence(item.evidenceChunkIds) }}</small></li></ul></div>
                <div v-if="callMeetingAiReport.state.blockers?.length" class="meeting-ai-group"><strong>Trở ngại</strong><ul><li v-for="item in callMeetingAiReport.state.blockers" :key="item.text"><span>{{ item.text }}</span><small v-if="formatAiEvidence(item.evidenceChunkIds)">{{ formatAiEvidence(item.evidenceChunkIds) }}</small></li></ul></div>
                <div v-if="callMeetingAiReport.state.risks?.length" class="meeting-ai-group"><strong>Rủi ro</strong><ul><li v-for="item in callMeetingAiReport.state.risks" :key="item.text"><span>{{ item.text }}</span><small v-if="formatAiEvidence(item.evidenceChunkIds)">{{ formatAiEvidence(item.evidenceChunkIds) }}</small></li></ul></div>
                <div v-if="callMeetingAiReport.state.openQuestions?.length" class="meeting-ai-group"><strong>Câu hỏi mở</strong><ul><li v-for="item in callMeetingAiReport.state.openQuestions" :key="item.text"><span>{{ item.text }}</span><small v-if="formatAiEvidence(item.evidenceChunkIds)">{{ formatAiEvidence(item.evidenceChunkIds) }}</small></li></ul></div>
              </template>
              <small class="meeting-ai-review-note">AI không tự tạo WorkItem. Mọi đề xuất cần được bạn xem lại.</small>
            </section>
            <div v-else-if="callTranscriptionCapabilities.configured" class="meeting-ai-unavailable">Trợ lý cuộc họp chưa sẵn sàng. Quản trị viên chưa cấu hình phiên âm cuộc họp; phiên âm vẫn hoạt động độc lập khi được bật.</div>
            <div class="call-transcript-list" aria-live="polite">
              <div v-for="chunk in callTranscriptChunks" :key="chunk.id" class="call-transcript-chunk">
                <div><time>{{ formatTime(chunk.startedAt) }}</time><strong>{{ chunk.speakerDisplayName }}</strong></div>
                <p>“{{ chunk.text }}”</p>
              </div>
              <div v-for="interim in callTranscriptInterims" :key="interim.id" class="call-transcript-chunk is-interim">
                <div><time>{{ formatTime(interim.startedAt) }}</time><strong>{{ interim.speakerDisplayName }}</strong></div>
                <p>“{{ interim.text }}”</p>
              </div>
              <span v-if="!callTranscriptChunks.length" class="channel-utility-empty">Chưa có nội dung phiên âm.</span>
            </div>
          </aside>

          <div class="call-controls-row">
            <div class="call-control-dock">
              <button 
                class="call-control-circle-btn" 
                :class="{ 'inactive': !callMicrophoneEnabled }"
                @click="toggleCallMicrophone"
                :aria-label="callMicrophoneEnabled ? 'Mic đang bật — tắt mic' : 'Mic đã tắt — bật mic'"
                :title="callMicrophoneEnabled ? 'Tắt Micro' : 'Bật Micro'"
              >
                <i :class="callMicrophoneEnabled ? 'fa-solid fa-microphone' : 'fa-solid fa-microphone-slash'"></i>
              </button>

              <button 
                class="call-control-circle-btn" 
                :class="{ 'inactive': !isCallCameraOn }" 
                @click="toggleCallCameraReal"
                :aria-label="isCallCameraOn ? 'Camera đang bật — tắt camera' : 'Camera đang tắt — bật camera'"
                :title="isCallCameraOn ? 'Tắt Camera' : 'Bật Camera'"
              >
                <i :class="isCallCameraOn ? 'fa-solid fa-video' : 'fa-solid fa-video-slash'"></i>
              </button>

              <div class="camera-effects-control">
                <button type="button" class="call-control-label-btn" :class="{ active: cameraBackgroundEffect === 'blur' }" title="Hiệu ứng camera" aria-label="Mở hiệu ứng camera" aria-haspopup="menu" :aria-expanded="showCameraEffectsMenu" @click="showCameraEffectsMenu = !showCameraEffectsMenu">
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
                class="call-control-circle-btn share-control"
                :class="{ 'active-share': isSharingScreen }" 
                @click="toggleScreenShare"
                :aria-label="isSharingScreen ? 'Đang chia sẻ — dừng chia sẻ' : 'Chia sẻ màn hình'"
                :title="isSharingScreen ? 'Tắt chia sẻ' : 'Chia sẻ màn hình'"
              >
                <i class="fa-solid fa-desktop" aria-hidden="true"></i>
              </button>

              <button type="button" class="call-control-label-btn hand-control" :class="{ active: callHandRaised }" :aria-pressed="callHandRaised" :aria-label="callHandRaised ? 'Hạ tay' : 'Giơ tay'" :title="callHandRaised ? 'Hạ tay' : 'Giơ tay'" @click="toggleRaiseHand">
                <i class="fa-solid fa-hand" aria-hidden="true"></i><span>{{ callHandRaised ? 'Hạ tay' : 'Giơ tay' }}</span>
              </button>

              <button v-if="callLayoutMode === 'CAMERA_FOCUS'" type="button" class="call-control-label-btn" aria-label="Thu về lưới" title="Thu về lưới" @click="returnToParticipantGrid">
                <i class="fa-solid fa-table-cells" aria-hidden="true"></i><span>Thu về lưới</span>
              </button>



              <button type="button" class="call-control-label-btn" :class="{ active: showMembersSidebar }" aria-label="Mở danh sách người tham gia" title="Người tham gia" :aria-pressed="showMembersSidebar" @click="openCallParticipants">
                <i class="fa-solid fa-users" aria-hidden="true"></i><span>Người tham gia</span>
              </button>

              <button type="button" class="call-control-label-btn" :class="{ active: captionsEnabled }" :disabled="!callTranscriptionCapabilities.configured" :aria-pressed="captionsEnabled" :aria-label="captionsEnabled ? 'Tắt phụ đề' : 'Bật phụ đề'" :title="callTranscriptionCapabilities.configured ? (captionsEnabled ? 'Tắt phụ đề' : 'Bật phụ đề') : 'Phụ đề chưa được cấu hình'" @click="toggleCallCaptions">
                <i class="fa-solid fa-closed-captioning" aria-hidden="true"></i><span>Phụ đề</span>
              </button>

              <el-popover
                v-model:visible="showMoreMenu"
                placement="top-end"
                :width="270"
                :offset="10"
                trigger="click"
                :teleported="true"
                :show-arrow="false"
                popper-class="call-more-menu-popper"
                @show="moreMenuSection = ''"
              >
                <template #reference>
                  <button type="button" class="call-control-label-btn" aria-haspopup="menu" :aria-expanded="showMoreMenu" aria-label="Mở thêm tùy chọn">
                    <i class="fa-solid fa-ellipsis" aria-hidden="true"></i><span>Thêm</span>
                  </button>
                </template>
                <div class="call-more-menu" role="menu" aria-label="Tùy chọn cuộc gọi">
                  <template v-if="!moreMenuSection">
                    <button type="button" class="call-more-menu-item" role="menuitem" @click="moreMenuSection = 'view-mode'"><span>Chế độ xem</span><small>{{ callViewModeLabel }}</small></button>
                    <button type="button" class="call-more-menu-item" role="menuitem" @click="moreMenuSection = 'reactions'"><span>Phản ứng</span><i class="fa-solid fa-chevron-right" aria-hidden="true"></i></button>
                    <button type="button" class="call-more-menu-item" role="menuitem" @click="openCallDevicesMenu"><span>Thiết bị</span><i class="fa-solid fa-chevron-right" aria-hidden="true"></i></button>
                    <button type="button" class="call-more-menu-item" role="menuitem" @click="moreMenuSection = 'effects'"><span>Hiệu ứng hình ảnh</span><i class="fa-solid fa-chevron-right" aria-hidden="true"></i></button>
                    <button
                      type="button"
                      class="call-more-menu-item"
                      :class="{ 'is-unavailable': !hasEligiblePictureInPictureVideo }"
                      role="menuitem"
                      :aria-label="pictureInPictureActionLabel"
                      :title="pictureInPictureActionLabel"
                      @click="toggleCallPictureInPicture"
                    >Picture-in-picture</button>
                    <button type="button" class="call-more-menu-item" role="menuitem" @click="togglePresentationFullscreen">{{ presentationIsFullscreen ? 'Thoát toàn màn hình' : 'Toàn màn hình' }}</button>
                    <button type="button" class="call-more-menu-item" :class="{ 'is-unavailable': !callTranscriptionCapabilities.configured }" role="menuitem" :disabled="!callTranscriptionCapabilities.configured" @click="moreMenuSection = 'captions'"><span>Ngôn ngữ phụ đề</span><small>{{ callTranscriptionCapabilities.configured ? callCaptionLanguageLabel : 'Chưa cấu hình' }}</small></button>
                    <button type="button" class="call-more-menu-item" role="menuitem" @click="moreMenuSection = 'shortcuts'"><span>Phím tắt</span><small>Ctrl/Cmd+D · E</small></button>
                  </template>
                  <template v-else>
                    <button type="button" class="call-more-menu-back" @click="moreMenuSection = ''"><i class="fa-solid fa-arrow-left" aria-hidden="true"></i><span>Thêm</span></button>
                    <div v-if="moreMenuSection === 'view-mode'" class="call-device-panel" role="radiogroup" aria-label="Chế độ xem">
                      <span class="call-more-section-label">Bố cục cuộc họp</span>
                      <button v-for="mode in callViewModes" :key="mode.value" type="button" class="call-device-option" role="radio" :aria-checked="callViewMode === mode.value" :class="{ selected: callViewMode === mode.value }" @click="setCallViewMode(mode.value)">{{ mode.label }}</button>
                    </div>
                    <div v-else-if="moreMenuSection === 'shortcuts'" class="call-device-panel">
                      <span class="call-more-section-label">Phím tắt cuộc gọi</span>
                      <span class="call-more-empty">Ctrl/Cmd + D — bật hoặc tắt microphone</span>
                      <span class="call-more-empty">Ctrl/Cmd + E — bật hoặc tắt camera</span>
                    </div>
                    <div v-else-if="moreMenuSection === 'reactions'" class="call-reaction-picker" role="group" aria-label="Phản ứng">
                      <span class="call-more-section-label">Chọn một phản ứng</span>
                      <div class="call-reaction-options">
                        <button v-for="emoji in ['👍', '👏', '❤️', '😂', '🎉']" :key="emoji" type="button" class="call-reaction-option" :aria-label="`Gửi phản ứng ${emoji}`" @click="sendCallReaction(emoji)">{{ emoji }}</button>
                      </div>
                    </div>
                    <div v-else-if="moreMenuSection === 'devices'" class="call-device-panel">
                      <span class="call-more-section-label">Thiết bị cuộc gọi</span>
                      <label class="call-device-select">Microphone<select v-model="selectedCallMicrophoneId" @change="switchCallDevice('audioinput', selectedCallMicrophoneId)"><option value="">Thiết bị mặc định</option><option v-for="device in audioInputDevices" :key="device.deviceId" :value="device.deviceId">{{ device.label || 'Microphone' }}</option></select></label>
                      <label class="call-device-select">Camera<select v-model="selectedCallCameraId" @change="switchCallDevice('videoinput', selectedCallCameraId)"><option value="">Thiết bị mặc định</option><option v-for="device in videoInputDevices" :key="device.deviceId" :value="device.deviceId">{{ device.label || 'Camera' }}</option></select></label>
                      <label v-if="speakerSelectionSupported" class="call-device-select">Loa<select v-model="selectedCallSpeakerId" @change="switchCallSpeaker"><option value="">Loa mặc định</option><option v-for="device in audioOutputDevices" :key="device.deviceId" :value="device.deviceId">{{ device.label || 'Loa' }}</option></select></label>
                      <span v-else class="call-more-empty">Trình duyệt chưa cho phép chọn loa.</span>
                      <span v-if="!callDevices.length" class="call-more-empty">Chưa tìm thấy thiết bị.</span>
                    </div>
                    <div v-else-if="moreMenuSection === 'captions'" class="call-device-panel">
                      <span class="call-more-section-label">Ngôn ngữ phụ đề</span>
                      <label class="call-device-select">Ngôn ngữ<select v-model="callCaptionLanguage" :disabled="callAiState.state === 'ACTIVE'" @change="setCallCaptionLanguage"><option v-for="language in callTranscriptionCapabilities.supportedLanguages" :key="language" :value="language">{{ language === 'vi' ? 'Tiếng Việt' : 'English' }}</option></select></label>
                      <span v-if="callAiState.state === 'ACTIVE'" class="call-more-empty">Dừng biên bản trước khi đổi ngôn ngữ.</span>
                    </div>
                    <div v-else class="call-effects-panel">
                      <span class="call-more-section-label">Hiệu ứng hình ảnh</span>
                      <button type="button" class="call-device-option" :class="{ selected: cameraBackgroundEffect === 'none' }" @click="setCallBackgroundEffect('none')">Không làm mờ</button>
                      <button type="button" class="call-device-option" :class="{ selected: cameraBackgroundEffect === 'blur' }" :disabled="cameraEffectPending" @click="setCallBackgroundEffect('blur')">Làm mờ nền</button>
                    </div>
                  </template>
                </div>
              </el-popover>

              <button 
                class="call-control-circle-btn hang-up" 
                @click="leaveVoiceChannel"
                aria-label="Rời cuộc gọi"
                title="Rời kênh thoại"
              >
                <i class="fa-solid fa-phone-slash"></i>
              </button>
            </div>
          </div>
          <div class="call-reaction-overlay" aria-live="polite">
            <span v-for="reaction in callReactions" :key="reaction.id" class="call-reaction-bubble">{{ reaction.emoji }} <small>{{ reaction.displayName }}</small></span>
          </div>
          <div class="sr-only" aria-live="polite">{{ callLiveNotice }}</div>
          <aside
            v-if="callChatOpen || showMembersSidebar"
            class="call-chat-panel relative"
            :aria-label="callChatOpen ? 'Call chat' : 'Call participants'"
            @dragover="handleDragOver"
            @dragleave="handleDragLeave"
            @drop="handleDropFile"
          >
            <div class="call-chat-panel-header">
              <div class="call-chat-panel-title">
                <span class="context-kicker">MEETING</span>
                <strong class="call-chat-channel-name" :title="activeVoiceChannel?.name || activeChannel?.name || 'Tin nhắn cuộc gọi'">{{ activeVoiceChannel?.name || activeChannel?.name || 'Tin nhắn cuộc gọi' }}</strong>
              </div>
              <button type="button" class="context-close" aria-label="Đóng panel cuộc gọi" title="Đóng panel" @click="closeCallSidePanel"><i class="fa-solid fa-xmark" aria-hidden="true"></i></button>
            </div>
            <div class="context-tabs call-panel-tabs" role="tablist" aria-label="Nội dung cuộc gọi">
              <button type="button" class="context-tab" :class="{ 'is-active': showMembersSidebar }" role="tab" :aria-selected="showMembersSidebar" @click="openCallParticipants">Người tham gia</button>
              <button type="button" class="context-tab" :class="{ 'is-active': callChatOpen }" role="tab" :aria-selected="callChatOpen" @click="openVoiceChannelChat">Chat</button>
            </div>
            <div v-if="showMembersSidebar" class="context-member-list call-panel-participants">
              <div class="context-call-summary">
                <span class="context-status-dot"></span><div><strong>{{ activeVoiceChannel?.name }}</strong><span>{{ participantsInCall.length }} người trong phòng</span></div>
              </div>
              <div v-for="user in participantsInCall" :key="`call-panel-${user.connectionId}`" class="context-member-row">
                <el-avatar :size="32" :src="user.connectionId === callConnectionId ? currentUser.avatar : user.avatarUrl">{{ (user.connectionId === callConnectionId ? currentUser.name : user.displayName)?.charAt(0) }}</el-avatar>
                <span>{{ user.displayName }}{{ user.connectionId === callConnectionId ? ' (Bạn)' : '' }}</span>
                <i v-if="!user.microphoneEnabled" class="fa-solid fa-microphone-slash" aria-label="Đang tắt micro"></i>
                <i v-if="user.handRaised" class="fa-solid fa-hand call-hand-indicator" aria-label="Đang giơ tay" title="Đang giơ tay"></i>
              </div>
            </div>
            <div v-if="callChatOpen" ref="callChatThread" class="call-chat-thread">
              <template v-if="callChatDisplayMessages.length">
                <div
                  v-for="msg in callChatDisplayMessages"
                  :key="`call-${msg.messageId || msg.clientMessageId}`"
                  class="call-chat-message"
                  :class="{
                    'is-own': isOwnCallMessage(msg),
                    'mine': isOwnCallMessage(msg),
                    'is-media-only': !msg.content && msg.attachments?.length && msg.attachments.every(a => a.isImage || a.previewUrl || isImageFile(a.originalFileName || a.fileName)),
                    'is-audio-only': (!msg.content || !msg.content.trim()) && msg.attachments?.length && msg.attachments.some(a => isAudioMedia(a))
                  }"
                >
                  <div class="call-chat-message-body">
                    <!-- Reply Quote Box -->
                    <button v-if="msg.replyTo" type="button" class="message-reply-quote mb-1" @click="focusMessage(msg.replyTo.messageId)">
                      <span class="reply-quote-label">Trả lời {{ msg.replyTo.senderName }}</span>
                      <span class="reply-quote-content">{{ msg.replyTo.content || 'Tin nhắn không còn khả dụng' }}</span>
                    </button>

                    <div class="message-bubble-wrapper">
                      <div
                        class="call-msg-bubble"
                        :class="{
                          'is-revoked': msg.isRevoked,
                          'is-media-only': !msg.content && msg.attachments?.length && msg.attachments.every(a => a.isImage || a.previewUrl || isImageFile(a.originalFileName || a.fileName)),
                          'is-audio-only': (!msg.content || !msg.content.trim()) && msg.attachments?.length && msg.attachments.some(a => isAudioMedia(a))
                        }"
                        @click="onMessageBodyClick($event, msg)"
                      >
                        <div class="call-msg-inline-header">
                          <el-avatar :size="20" :src="msg.senderAvatar || ''" class="call-msg-avatar">
                            {{ (msg.senderName || '?').charAt(0).toUpperCase() }}
                          </el-avatar>
                          <strong class="call-msg-author">{{ msg.senderName }}</strong>
                          <span v-if="msg.isEdited" class="edited-badge">(đã sửa)</span>
                          <span class="call-msg-time">{{ formatTime(msg.sentAt) }}</span>
                        </div>
                        <span v-if="msg.isRevoked" class="revoked-text" style="font-style: italic; opacity: 0.65;">
                          <i class="fa-solid fa-ban mr-1"></i> Tin nhắn đã được thu hồi
                        </span>
                        <div v-if="msg.attachments?.length" class="call-msg-attachments mt-1.5 flex flex-col gap-1.5">
                          <div v-for="att in msg.attachments" :key="att.attachmentId" class="call-msg-att-item relative">
                            <!-- Uploading overlay when message is sending -->
                            <div v-if="(msg.status === 'sending' || msg.uploadProgress !== undefined) && (msg.uploadProgress || 0) < 100" class="mb-1 rounded-xl p-2 bg-black/40 backdrop-blur-sm border border-sky-400/30 flex flex-col gap-1.5">
                              <div class="flex items-center justify-between text-xs text-sky-200 font-semibold px-1">
                                <span class="flex items-center gap-1.5">
                                  <i class="fa-solid fa-spinner animate-spin text-sky-400"></i>
                                  Đang tải lên...
                                </span>
                                <span class="text-sky-300 font-bold">{{ msg.uploadProgress || 0 }}%</span>
                              </div>
                              <div class="w-full h-1.5 bg-black/40 rounded-full overflow-hidden">
                                <div class="h-full bg-gradient-to-r from-sky-400 to-blue-500 rounded-full transition-all duration-150" :style="{ width: `${msg.uploadProgress || 0}%` }"></div>
                              </div>
                            </div>

                            <template v-if="isAudioMedia(att)">
                              <div class="msg-audio-player-card my-1.5 p-2.5 rounded-2xl bg-slate-800 text-white flex flex-col gap-1.5 min-w-[240px] max-w-[280px]">
                                <div class="flex items-center justify-between gap-2 px-1">
                                  <div class="flex items-center gap-1.5 text-sky-300 font-bold text-[11px]">
                                    <i class="fa-solid fa-microphone text-xs animate-pulse text-sky-400"></i>
                                    <span>Tin nhắn thoại</span>
                                  </div>
                                  <span v-if="att.durationSeconds || att.duration" class="text-[10px] font-mono font-bold text-sky-200 bg-sky-900/60 px-2 py-0.5 rounded-md">
                                    {{ formatRecordingTimer(att.durationSeconds || att.duration) }}
                                  </span>
                                </div>
                                <audio controls controlsList="nodownload noplaybackrate" class="w-full h-8 rounded-lg" :src="getAttachmentDisplayUrl(att) || att.previewUrl || att.fileUrl"></audio>
                              </div>
                            </template>
                            <template v-else-if="att.isImage || isImageFile(att.originalFileName || att.fileName || att.name || '')">
                              <div class="msg-inline-image-wrapper my-1 max-w-[180px] max-h-[130px] rounded-xl overflow-hidden shadow-md cursor-pointer hover:opacity-95 transition-all bg-transparent relative block" @click.stop="openImageLightbox(getAttachmentDisplayUrl(att) || att.previewUrl || att.fileUrl, att)" title="Nhấn để phóng to hình ảnh">
                                <div v-if="att.previewLoading || (!att.previewUrl && !att.fileUrl && !att.url)" class="absolute inset-0 flex items-center justify-center bg-black/40 backdrop-blur-sm gap-2 text-sky-300 text-xs font-semibold p-2">
                                  <i class="fa-solid fa-circle-notch animate-spin text-xs text-sky-400"></i>
                                  <span>Đang tải...</span>
                                </div>
                                <img :src="getAttachmentDisplayUrl(att) || att.previewUrl || att.fileUrl || att.url" :alt="att.originalFileName || att.fileName || att.name" class="w-full h-full object-cover block rounded-xl" />
                              </div>
                            </template>
                            <template v-else>
                              <div class="msg-file-card min-w-[240px] max-w-[300px] flex items-center justify-between gap-3.5 px-3.5 py-2.5 my-1.5 rounded-2xl bg-black/25 dark:bg-black/40 hover:bg-black/35 dark:hover:bg-black/55 cursor-pointer transition-all shadow-md" @click="downloadAttachment(att)" title="Tải xuống tệp">
                                <div class="w-10 h-10 rounded-xl bg-white/20 dark:bg-sky-500/30 flex items-center justify-center flex-shrink-0 shadow-inner">
                                  <i :class="getFileIconClass(att.originalFileName)" class="text-base text-white dark:text-sky-400"></i>
                                </div>
                                <div class="flex flex-col flex-1 justify-center min-w-0 px-1">
                                  <span class="truncate text-xs font-bold text-white tracking-wide leading-snug">{{ att.originalFileName }}</span>
                                  <span v-if="att.sizeBytes" class="text-[11px] text-white/80 font-normal leading-tight mt-1">{{ formatFileSize(att.sizeBytes) }}</span>
                                </div>
                                <div class="w-8 h-8 rounded-full bg-white/20 hover:bg-white/35 flex items-center justify-center transition-colors flex-shrink-0 shadow-sm ml-1">
                                  <i class="fa-solid fa-download text-xs text-white"></i>
                                </div>
                              </div>
                            </template>
                          </div>
                        </div>
                        <p v-if="!msg.isRevoked && msg.content" class="call-msg-content mt-1">{{ msg.content }}</p>
                      </div>

                      <!-- Zalo Hover Action Toolbar -->
                      <div class="zalo-hover-toolbar" aria-label="Thao tác tin nhắn">
                        <div class="zalo-action-buttons">
                          <button v-if="!msg.isRevoked" type="button" class="zalo-circle-btn" title="Trích dẫn / Trả lời" @click="startReply(msg)">
                            <i class="fa-solid fa-quote-right"></i>
                          </button>
                          <div v-if="!msg.isRevoked" class="zalo-reaction-trigger-wrapper">
                            <button type="button" class="zalo-circle-btn reaction-trigger-btn" title="Thả cảm xúc" @click="toggleReaction(msg, '👍')">
                              <i class="fa-regular fa-thumbs-up"></i>
                            </button>
                            <div class="zalo-emoji-bar">
                              <button v-for="emoji in ['👍', '❤️', '😂', '😮', '😭', '😡']" :key="emoji" type="button" class="zalo-emoji-btn" :title="`Thêm ${emoji}`" @click="toggleReaction(msg, emoji)">
                                {{ emoji }}
                              </button>
                            </div>
                          </div>
                          <el-dropdown trigger="click" placement="bottom-end">
                            <button type="button" class="zalo-circle-btn" title="Tùy chọn khác">
                              <i class="fa-solid fa-ellipsis"></i>
                            </button>
                            <template #dropdown>
                              <el-dropdown-menu class="zalo-menu-dropdown">
                                <el-dropdown-item v-if="!msg.isRevoked" @click="copyMessageText(msg)">
                                  <i class="fa-regular fa-copy mr-2"></i> Copy tin nhắn
                                </el-dropdown-item>
                                <el-dropdown-item v-if="isOwnCallMessage(msg) && canEditOrRevoke(msg)" @click="startEditMessage(msg)">
                                  <i class="fa-regular fa-pen-to-square mr-2"></i> Chỉnh sửa
                                </el-dropdown-item>
                                <el-dropdown-item v-if="isOwnCallMessage(msg) && canEditOrRevoke(msg)" @click="revokeMessage(msg)">
                                  <span class="text-rose-500 flex items-center gap-1"><i class="fa-solid fa-rotate-left mr-1"></i> Thu hồi</span>
                                </el-dropdown-item>
                                <el-dropdown-item :divided="!msg.isRevoked" @click="deleteMessageForMe(msg)">
                                  <span class="text-rose-500 flex items-center gap-1"><i class="fa-regular fa-trash-can mr-1"></i> Xóa ở phía tôi</span>
                                </el-dropdown-item>
                              </el-dropdown-menu>
                            </template>
                          </el-dropdown>
                        </div>
                      </div>
                    </div>

                    <!-- Reactions List -->
                    <div v-if="msg.reactions?.length" class="message-reactions mt-1">
                      <span v-for="r in msg.reactions" :key="r.emoji" class="reaction-pill" @click="toggleReaction(msg, r.emoji)">
                        {{ r.emoji }} {{ r.count }}
                      </span>
                    </div>
                  </div>
                </div>
              </template>
              <div v-else class="call-chat-empty-state">
                <i class="fa-regular fa-comments text-2xl text-accent mb-1"></i>
                <strong>Chưa có tin nhắn trong phòng này.</strong>
                <span>Bắt đầu cuộc trò chuyện để mọi người cùng thảo luận.</span>
              </div>
            </div>

            <!-- Enhanced Composer Input with Attachments, Emojis, Reply & Editing target -->
            <form v-if="callChatOpen" class="call-chat-composer" @submit.prevent="sendCallChatMessage">
              <!-- Editing Target Preview Bar -->
              <div v-if="editingTarget" class="reply-composer-strip editing-composer-strip mb-1">
                <div class="reply-content-box">
                  <div class="reply-header-row">
                    <span class="reply-header-text" style="color: #3b82f6;">
                      <i class="fa-regular fa-pen-to-square reply-quote-icon"></i> Đang chỉnh sửa tin nhắn
                    </span>
                  </div>
                  <div class="reply-snippet-text truncate">{{ editingTarget.content }}</div>
                </div>
                <button type="button" class="reply-close-btn" title="Hủy chỉnh sửa" @click="cancelEditMessage">
                  <i class="fa-solid fa-xmark"></i>
                </button>
              </div>

              <!-- Reply Target Preview Bar -->
              <div v-if="replyTarget" class="reply-composer-strip mb-1">
                <div class="reply-content-box">
                  <div class="reply-header-row">
                    <span class="reply-header-text">
                      <i class="fa-solid fa-quote-left reply-quote-icon"></i> Trả lời <strong class="reply-user-name">{{ replyTarget.senderName }}</strong>
                    </span>
                  </div>
                  <div class="reply-snippet-text truncate">{{ replyTarget.content || 'Tin nhắn không còn khả dụng' }}</div>
                </div>
                <button type="button" class="reply-close-btn" title="Hủy trả lời" @click="cancelReply">
                  <i class="fa-solid fa-xmark"></i>
                </button>
              </div>

              <!-- Hidden file input for call chat attachment -->
              <input 
                type="file" 
                ref="fileInputRef" 
                style="display: none;" 
                multiple
                accept=".png,.jpg,.jpeg,.webp,.gif,.svg,.pdf,.txt,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.csv,.zip,.rar,.7z,.tar,.gz,.bz2,.xz,.iso,.tgz,.mp3,.wav,.ogg,.m4a,.mp4,.webm,.mkv,.avi,.json,.xml,.sql,.js,.ts,.html,.css,.py,.cs,.java"
                @change="handleFileChange" 
              />

              <!-- Live Voice Recording Banner (Animated Waveform & Realtime Timer) -->
              <div v-if="isRecordingAudio" class="recording-live-banner px-3 py-2 border-b border-rose-500/30 bg-rose-950/40 rounded-t-xl flex items-center justify-between gap-3 animate-fade-in">
                <div class="flex items-center gap-2">
                  <span class="w-2.5 h-2.5 rounded-full bg-rose-500 animate-ping"></span>
                  <span class="text-xs font-bold text-rose-300 tracking-wide">Đang ghi âm...</span>
                  <span class="text-xs font-mono font-semibold text-rose-200 bg-rose-900/60 px-2 py-0.5 rounded-md border border-rose-500/30">{{ formatRecordingTimer(recordingDurationSeconds) }}</span>
                </div>
                <!-- Live Animated Audio Equalizer Waveform Bars -->
                <div class="flex items-center gap-1 h-5 px-2">
                  <span v-for="(height, i) in recordingWaveBars" :key="i" class="w-1 bg-gradient-to-t from-rose-500 to-rose-300 rounded-full transition-all duration-100 shadow-sm" :style="{ height: `${height}%` }"></span>
                </div>
                <button type="button" class="text-xs font-semibold px-2.5 py-1 rounded-lg bg-rose-600 hover:bg-rose-500 text-white transition-all shadow-md flex items-center gap-1.5" @click="toggleVoiceRecording">
                  <i class="fa-solid fa-square text-[10px]"></i>
                  <span>Dừng & Nhập</span>
                </button>
              </div>

              <!-- Attached File Preview Bar (Ultra Compact - Media Thumbnail Only) -->
              <div v-if="attachedFiles.length" class="call-attached-preview px-2 py-1.5 border-b border-white/10 bg-black/40 rounded-t-lg">
                <div class="flex flex-wrap gap-1.5 max-h-14 overflow-y-auto">
                  <div v-for="file in attachedFiles" :key="file.id" class="relative group flex items-center gap-1.5 p-1 rounded-md bg-slate-800/90 border border-white/10">
                    <template v-if="file.isAudio">
                      <div class="w-7 h-7 rounded bg-rose-500/20 flex items-center justify-center text-rose-400">
                        <i class="fa-solid fa-microphone text-xs animate-pulse"></i>
                      </div>
                      <div class="flex flex-col">
                        <span class="truncate text-[10px] font-bold text-rose-300 max-w-[70px]">Thoại</span>
                        <span v-if="file.durationSeconds" class="text-[9px] text-slate-300 font-mono">{{ formatRecordingTimer(file.durationSeconds) }}</span>
                      </div>
                    </template>
                    <img v-else-if="file.previewUrl && (file.type?.startsWith('image/') || isImageFile(file.name))" :src="file.previewUrl" alt="" class="w-7 h-7 object-cover rounded shadow-sm" />
                    <template v-else>
                      <i :class="getFileIconClass(file.name)" class="text-xs text-sky-400 px-0.5"></i>
                      <div class="flex flex-col max-w-[80px]">
                        <span class="truncate text-[10px] font-medium text-slate-100 leading-tight">{{ file.name }}</span>
                      </div>
                    </template>
                    <button type="button" class="ml-0.5 text-slate-400 hover:text-rose-400 transition-colors p-0.5" @click="removeAttachedFile(file.id)" title="Bỏ chọn tệp">
                      <i class="fa-solid fa-xmark text-[9px]"></i>
                    </button>
                  </div>
                  <button type="button" class="flex flex-col items-center justify-center w-7 h-7 rounded border border-dashed border-white/20 text-slate-400 hover:text-sky-400 hover:border-sky-400/50 transition-colors" title="Thêm tệp khác" @click="triggerAttachment">
                    <i class="fa-solid fa-plus text-[9px]"></i>
                  </button>
                </div>
              </div>

              <div class="call-composer-controls-row">
                <button type="button" class="call-composer-icon-btn" title="Đính kèm file" @click="triggerAttachment">
                  <i class="fa-solid fa-paperclip"></i>
                </button>
                
                <button type="button" class="call-composer-icon-btn" :class="{ 'recording-active-btn': isRecordingAudio }" :title="isRecordingAudio ? 'Đang ghi âm... Nhấn để dừng & gửi' : 'Ghi âm tin nhắn thoại'" @click="toggleVoiceRecording">
                  <i :class="isRecordingAudio ? 'fa-solid fa-square' : 'fa-solid fa-microphone'"></i>
                </button>

                <el-popover placement="top-start" :width="260" trigger="click" popper-class="emoji-popover-popper">
                  <template #reference>
                    <button type="button" class="call-composer-icon-btn" title="Biểu tượng cảm xúc">
                      <i class="fa-regular fa-smile"></i>
                    </button>
                  </template>
                  <div class="emoji-picker-grid">
                    <span v-for="emoji in emojiList" :key="emoji" class="emoji-item" @click="insertCallEmoji(emoji)">{{ emoji }}</span>
                  </div>
                </el-popover>

                <textarea
                  ref="callChatComposer"
                  v-model="callChatDraft"
                  :disabled="callChatSending"
                  maxlength="4000"
                  rows="1"
                  aria-label="Nội dung chat cuộc gọi"
                  placeholder="Gửi tin nhắn..."
                  @keydown.enter.exact.prevent="sendCallChatMessage"
                  @paste="handleCallChatPaste"
                ></textarea>

                <button
                  type="submit"
                  class="call-composer-send-btn"
                  :disabled="callChatSending || (!callChatDraft.trim() && !attachedFiles.length && !editingTarget)"
                  :aria-label="editingTarget ? 'Lưu thay đổi' : 'Gửi tin nhắn'"
                  :title="editingTarget ? 'Lưu thay đổi' : 'Gửi tin nhắn'"
                >
                  <i :class="editingTarget ? 'fa-solid fa-check' : (callChatSending ? 'fa-solid fa-spinner fa-spin' : 'fa-solid fa-paper-plane')"></i>
                </button>
              </div>
            </form>
          </aside>
        </div>
      </template>

      <!-- Standard Text Chat View -->
      <template v-else-if="workspaceState === 'TEXT_CHANNEL'">
        <header class="chat-header text-chat-header">
          <div class="active-info">
            <button type="button" class="mobile-sidebar-trigger" aria-label="Mở danh sách kênh" title="Mở danh sách kênh" :aria-expanded="sidebarOpen" @click="sidebarOpen = !sidebarOpen">
              <i class="fa-solid fa-bars" aria-hidden="true"></i>
            </button>
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
            <button type="button" class="action-btn ai-action-btn" :class="{ 'is-open': aiAnalysisOpen }" aria-label="SprintA AI Assistant" title="SprintA AI Assistant" @click="openAiAnalysis('text')">
              <i class="fa-solid fa-wand-magic-sparkles" aria-hidden="true"></i>
            </button>
            <button type="button" class="action-btn" aria-label="Mở panel channel" title="Mở panel channel" @click="toggleContextPanel">
              <i class="fa-solid fa-circle-info" aria-hidden="true"></i>
            </button>
            <button v-if="activeChannel" type="button" class="action-btn" aria-label="Tìm trong Channel" title="Tìm trong Channel" @click="openChannelUtility('search')">
              <i class="fa-solid fa-magnifying-glass" aria-hidden="true"></i>
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
          <span class="flex-1">{{ connectionNotice }}</span>
          <button type="button" class="connection-notice-close-btn" title="Đóng thông báo" @click="connectionNotice = ''">
            <i class="fa-solid fa-xmark"></i>
          </button>
        </div>

        <!-- Main body layout with horizontal partition for Discord style members list -->
        <div class="chat-content-split relative">
          <!-- Chat Area (Messages + Input) -->
          <div class="chat-thread-column">
            <!-- Pinned Messages Banner (Image 1 & Image 2) -->
            <div v-if="pinnedList.length > 0" class="chat-pinned-banner" :class="{ 'is-expanded': isPinnedListExpanded }">
              <!-- Collapsed Single Pin Summary Bar (Image 1) -->
              <div v-if="!isPinnedListExpanded" class="pinned-summary-bar">
                <div class="pinned-summary-left" @click="focusMessage(pinnedList[0].messageId)">
                  <div class="pinned-icon-circle">
                    <i class="fa-regular fa-comment-dots"></i>
                  </div>
                  <div class="pinned-summary-text">
                    <strong class="pinned-type-label">Tin nhắn</strong>
                    <div class="pinned-summary-snippet truncate">
                      <span>{{ pinnedList[0].senderName }}: </span>
                      <span class="pinned-content-preview">{{ pinnedList[0].content }}</span>
                    </div>
                  </div>
                </div>
                <div class="pinned-summary-right">
                  <button
                    v-if="pinnedList.length > 1"
                    type="button"
                    class="pinned-count-toggle-btn"
                    @click="isPinnedListExpanded = true"
                  >
                    +{{ pinnedList.length - 1 }} ghim <i class="fa-solid fa-chevron-down ml-1"></i>
                  </button>
                  <el-dropdown trigger="click" placement="bottom-end">
                    <button type="button" class="pinned-action-more-btn" title="Tùy chọn ghim">
                      <i class="fa-solid fa-ellipsis"></i>
                    </button>
                    <template #dropdown>
                      <el-dropdown-menu>
                        <el-dropdown-item @click="focusMessage(pinnedList[0].messageId)">
                          <i class="fa-solid fa-arrow-turn-down mr-2"></i> Đi đến tin nhắn
                        </el-dropdown-item>
                        <el-dropdown-item v-if="activeChannel?.canManage" @click="togglePin(pinnedList[0])">
                          <i class="fa-solid fa-thumbtack-slash mr-2 text-danger"></i> <span class="text-danger">Bỏ ghim</span>
                        </el-dropdown-item>
                      </el-dropdown-menu>
                    </template>
                  </el-dropdown>
                </div>
              </div>

              <!-- Expanded Full Pinned List (Image 2) -->
              <div v-else class="pinned-expanded-box">
                <div class="pinned-expanded-header">
                  <span class="pinned-expanded-title">Danh sách ghim ({{ pinnedList.length }})</span>
                  <button type="button" class="pinned-collapse-btn" @click="isPinnedListExpanded = false">
                    Thu gọn <i class="fa-solid fa-chevron-up ml-1"></i>
                  </button>
                </div>
                <div class="pinned-expanded-list">
                  <div
                    v-for="item in pinnedList"
                    :key="item.messageId"
                    class="pinned-expanded-item"
                  >
                    <div class="pinned-item-left" @click="focusMessage(item.messageId)">
                      <div class="pinned-icon-circle">
                        <i class="fa-regular fa-comment-dots"></i>
                      </div>
                      <div class="pinned-item-text">
                        <strong class="pinned-type-label">Tin nhắn</strong>
                        <div class="pinned-item-snippet truncate">
                          <span>{{ item.senderName }}: </span>
                          <span class="pinned-content-preview">{{ item.content }}</span>
                        </div>
                      </div>
                    </div>
                    <el-dropdown trigger="click" placement="bottom-end">
                      <button type="button" class="pinned-action-more-btn" title="Tùy chọn ghim">
                        <i class="fa-solid fa-ellipsis"></i>
                      </button>
                      <template #dropdown>
                        <el-dropdown-menu>
                          <el-dropdown-item @click="focusMessage(item.messageId)">
                            <i class="fa-solid fa-arrow-turn-down mr-2"></i> Đi đến tin nhắn
                          </el-dropdown-item>
                          <el-dropdown-item v-if="activeChannel?.canManage" @click="togglePin(item)">
                            <i class="fa-solid fa-thumbtack-slash mr-2 text-danger"></i> <span class="text-danger">Bỏ ghim</span>
                          </el-dropdown-item>
                        </el-dropdown-menu>
                      </template>
                    </el-dropdown>
                  </div>
                </div>
              </div>
            </div>

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
                  :data-message-id="msg.messageId"
                  :class="{
                    'is-own': isOwnMessage(msg),
                    'mine': isOwnMessage(msg),
                    'mention-target': msg.isMentioned,
                    'message-focus-target': highlightedMessageId === msg.messageId,
                    'is-audio-only': (!msg.content || !msg.content.trim()) && msg.attachments?.length && msg.attachments.some(a => isAudioMedia(a))
                  }"
                >
                  <el-avatar :size="36" :src="msg.senderAvatar || ''" class="sender-avatar">{{ msg.senderName?.charAt(0) || '?' }}</el-avatar>
                  <div class="message-content-wrapper">
                    <div class="message-header-line">
                      <span class="sender-name">{{ msg.senderName }}</span>
                      <span class="message-time">{{ formatTime(msg.sentAt) }}<span v-if="msg.isEdited" class="edited-badge" style="font-size: 11px; opacity: 0.75; margin-left: 6px; font-weight: normal;">(Đã chỉnh sửa)</span></span>
                    </div>
                    <button
                      v-if="msg.replyTo"
                      type="button"
                      class="message-reply-quote"
                      @click="focusMessage(msg.replyTo.messageId)"
                    >
                      <span class="reply-quote-label">Trả lời {{ msg.replyTo.senderName }}</span>
                      <span class="reply-quote-content">{{ msg.replyTo.content || 'Tin nhắn không còn khả dụng' }}</span>
                    </button>
                    <div class="message-bubble-wrapper">
                      <div
                        class="message-body"
                        :class="{
                          'is-revoked': msg.isRevoked,
                          'is-media-only': !msg.content && msg.attachments?.length && msg.attachments.every(a => a.isImage || a.previewUrl || isImageFile(a.originalFileName || a.fileName)),
                          'is-audio-only': (!msg.content || !msg.content.trim()) && msg.attachments?.length && msg.attachments.some(a => isAudioMedia(a))
                        }"
                        @click="onMessageBodyClick($event, msg)"
                      >
                        <span v-if="msg.isRevoked" class="revoked-text" style="font-style: italic; opacity: 0.65;">
                          <i class="fa-solid fa-ban mr-1"></i> Tin nhắn đã được thu hồi
                        </span>
                        <template v-else>
                          <!-- Image/File Attachments inside message body -->
                          <div v-if="msg.attachments?.length" class="main-msg-attachments mb-1.5 flex flex-col gap-1.5">
                            <div v-for="att in msg.attachments" :key="att.attachmentId || att.id" class="main-msg-att-item relative">
                              <!-- Uploading overlay when message is sending -->
                              <div v-if="(msg.status === 'sending' || msg.uploadProgress !== undefined) && (msg.uploadProgress || 0) < 100" class="mb-1 rounded-xl p-2 bg-black/40 backdrop-blur-sm flex flex-col gap-1.5">
                                <div class="flex items-center justify-between text-xs text-sky-200 font-semibold px-1">
                                  <span class="flex items-center gap-1.5">
                                    <i class="fa-solid fa-spinner animate-spin text-sky-400"></i>
                                    Đang tải lên...
                                  </span>
                                  <span class="text-sky-300 font-bold">{{ msg.uploadProgress || 0 }}%</span>
                                </div>
                                <div class="w-full h-1.5 bg-black/40 rounded-full overflow-hidden">
                                  <div class="h-full bg-gradient-to-r from-sky-400 to-blue-500 rounded-full transition-all duration-150" :style="{ width: `${msg.uploadProgress || 0}%` }"></div>
                                </div>
                              </div>

                              <template v-if="isAudioMedia(att)">
                                <div class="msg-audio-player-card my-1.5 p-3 rounded-2xl bg-slate-800 text-white flex flex-col gap-2 min-w-[260px] max-w-[300px]">
                                  <div class="flex items-center justify-between gap-2 px-1">
                                    <div class="flex items-center gap-2 text-sky-300 font-bold text-xs">
                                      <i class="fa-solid fa-microphone text-xs animate-pulse text-sky-400"></i>
                                      <span>Tin nhắn thoại</span>
                                    </div>
                                    <span v-if="att.durationSeconds || att.duration" class="text-xs font-mono font-bold text-sky-200 bg-sky-900/60 px-2 py-0.5 rounded-md">
                                      {{ formatRecordingTimer(att.durationSeconds || att.duration) }}
                                    </span>
                                  </div>
                                  <audio controls controlsList="nodownload noplaybackrate" class="w-full h-9 rounded-lg" :src="getAttachmentDisplayUrl(att) || att.previewUrl || att.fileUrl"></audio>
                                </div>
                              </template>
                              <template v-else-if="att.isImage || isImageFile(att.originalFileName || att.fileName || att.name || '')">
                                <div class="msg-inline-image-wrapper my-1 max-w-[420px] rounded-2xl overflow-hidden shadow-md cursor-pointer hover:opacity-95 transition-all bg-transparent relative block" @click.stop="openImageLightbox(getAttachmentDisplayUrl(att) || att.previewUrl || att.fileUrl, att)" title="Nhấn để phóng to hình ảnh">
                                  <div v-if="att.previewLoading || (!att.previewUrl && !att.fileUrl && !att.url)" class="absolute inset-0 flex items-center justify-center bg-black/40 backdrop-blur-sm gap-2 text-sky-300 text-xs font-semibold p-4">
                                    <i class="fa-solid fa-circle-notch animate-spin text-base text-sky-400"></i>
                                    <span>Đang tải...</span>
                                  </div>
                                  <img :src="getAttachmentDisplayUrl(att) || att.previewUrl || att.fileUrl || att.url" :alt="att.originalFileName || att.fileName || att.name" class="w-auto h-auto max-w-full max-h-[380px] object-contain block rounded-2xl" />
                                </div>
                              </template>
                              <template v-else>
                                <div class="msg-file-card min-w-[260px] max-w-[340px] flex items-center justify-between gap-3.5 px-3.5 py-2.5 my-1.5 rounded-2xl bg-black/25 dark:bg-black/40 hover:bg-black/35 dark:hover:bg-black/55 cursor-pointer transition-all shadow-md" @click="downloadAttachment(att)" title="Tải xuống tệp">
                                  <div class="w-10 h-10 rounded-xl bg-white/20 dark:bg-sky-500/30 flex items-center justify-center flex-shrink-0 shadow-inner">
                                    <i :class="getFileIconClass(att.originalFileName || att.fileName)" class="text-base text-white"></i>
                                  </div>
                                  <div class="flex flex-col flex-1 justify-center min-w-0 px-1">
                                    <span class="truncate text-xs font-bold text-white tracking-wide leading-snug">{{ att.originalFileName || att.fileName }}</span>
                                    <span v-if="att.sizeBytes" class="text-[11px] text-white/80 font-medium leading-tight mt-1">{{ formatFileSize(att.sizeBytes) }}</span>
                                  </div>
                                  <div class="w-8 h-8 rounded-full bg-white/20 hover:bg-white/35 flex items-center justify-center transition-colors flex-shrink-0 shadow-sm ml-1">
                                    <i class="fa-solid fa-download text-xs text-white"></i>
                                  </div>
                                </div>
                              </template>
                            </div>
                          </div>
                          <template v-if="msg.content">
                            <template v-for="(segment, segmentIndex) in (msg.contentSegments || [{ text: msg.content, isMention: false }])" :key="`${msg.messageId}-${segmentIndex}`">
                              <a
                                v-if="segment.isUrl"
                                :href="segment.url"
                                target="_blank"
                                rel="noopener noreferrer"
                                class="message-url-link underline font-medium text-sky-200 hover:text-white hover:underline transition-colors cursor-pointer"
                                @click.stop
                              >{{ segment.text }}</a>
                              <span
                                v-else
                                class="message-text-segment"
                                :class="{ 'message-mention': segment.isMention }"
                              >{{ segment.text }}</span>
                            </template>
                          </template>
                        </template>
                      </div>

                      <!-- Zalo style message action toolbar on hover -->
                      <div class="zalo-hover-toolbar" aria-label="Thao tác tin nhắn">
                        <div class="zalo-action-buttons">
                          <!-- 1. Quote / Reply button ("") -->
                          <button
                            v-if="!msg.isRevoked"
                            type="button"
                            class="zalo-circle-btn"
                            title="Trích dẫn / Trả lời"
                            @click="startReply(msg)"
                          >
                            <i class="fa-solid fa-quote-right"></i>
                          </button>

                          <!-- 2. Thumbs-up / Reaction trigger button (Image 3) -->
                          <div v-if="!msg.isRevoked" class="zalo-reaction-trigger-wrapper">
                            <button
                              type="button"
                              class="zalo-circle-btn reaction-trigger-btn"
                              title="Thả cảm xúc"
                              @click="toggleReaction(msg, '👍')"
                            >
                              <i class="fa-regular fa-thumbs-up"></i>
                            </button>

                            <!-- Emoji reaction bar (Image 2 - appears when hovering over like button) -->
                            <div class="zalo-emoji-bar">
                              <button
                                v-for="emoji in ['👍', '❤️', '😂', '😮', '😭', '😡']"
                                :key="emoji"
                                type="button"
                                class="zalo-emoji-btn"
                                :title="`Thêm ${emoji}`"
                                @click="toggleReaction(msg, emoji)"
                              >
                                {{ emoji }}
                              </button>
                            </div>
                          </div>

                          <!-- 3. More options dropdown menu (...) -->
                          <el-dropdown trigger="click" placement="bottom-end">
                            <button type="button" class="zalo-circle-btn" title="Tùy chọn khác">
                              <i class="fa-solid fa-ellipsis"></i>
                            </button>
                            <template #dropdown>
                              <el-dropdown-menu class="zalo-menu-dropdown">
                                <el-dropdown-item v-if="!msg.isRevoked" @click="copyMessageText(msg)">
                                  <i class="fa-regular fa-copy mr-2"></i> Copy tin nhắn
                                </el-dropdown-item>
                                <el-dropdown-item v-if="activeChannel?.canManage && !msg.isRevoked" @click="togglePin(msg)">
                                  <i :class="msg.isPinned ? 'fa-solid fa-thumbtack-slash' : 'fa-solid fa-thumbtack'" class="mr-2"></i> {{ msg.isPinned ? 'Bỏ ghim tin nhắn' : 'Ghim tin nhắn' }}
                                </el-dropdown-item>
                                <el-dropdown-item v-if="!msg.isRevoked" @click="starMessage(msg)">
                                  <i :class="msg.isStarred ? 'fa-solid fa-star text-amber-400' : 'fa-regular fa-star'" class="mr-2"></i> {{ msg.isStarred ? 'Bỏ đánh dấu' : 'Đánh dấu tin nhắn' }}
                                </el-dropdown-item>
                                <el-dropdown-item divided v-if="isOwnMessage(msg) && canEditOrRevoke(msg)" @click="startEditMessage(msg)">
                                  <i class="fa-regular fa-pen-to-square mr-2"></i> Chỉnh sửa
                                </el-dropdown-item>
                                 <el-dropdown-item v-if="isOwnMessage(msg) && canEditOrRevoke(msg)" @click="revokeMessage(msg)">
                                  <span class="text-rose-500 flex items-center gap-1"><i class="fa-solid fa-rotate-left mr-1"></i> Thu hồi</span>
                                </el-dropdown-item>
                                <el-dropdown-item :divided="!msg.isRevoked" @click="deleteMessageForMe(msg)">
                                  <span class="text-rose-500 flex items-center gap-1"><i class="fa-regular fa-trash-can mr-1"></i> Xóa ở phía tôi</span>
                                </el-dropdown-item>
                              </el-dropdown-menu>
                            </template>
                          </el-dropdown>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </template>
            </div>

            <!-- Input Bar -->
            <div class="chat-input-area relative">
              <!-- Dropzone: Xem trước khi gửi (Fits exact composer bar width and height) -->
              <div
                v-if="isDraggingFile"
                class="absolute inset-0 z-[99999] flex flex-col items-center justify-center border-2 border-dashed border-sky-400 bg-sky-950/90 dark:bg-slate-900/95 backdrop-blur-md rounded-xl text-white p-3 shadow-2xl transition-all duration-200 pointer-events-none"
              >
                <div class="flex items-center gap-2 text-sky-400">
                  <i class="fa-solid fa-cloud-arrow-up text-lg"></i>
                  <strong class="text-base font-bold text-sky-300">Xem trước khi gửi</strong>
                </div>
                <span class="text-xs text-slate-300 mt-0.5">Thả File hoặc Ảnh vào đây để xem lại trước khi gửi</span>
              </div>
              <!-- Editing Target Preview Bar (Above Input) -->
              <div v-if="editingTarget" class="reply-composer-strip editing-composer-strip">
                <div class="reply-content-box">
                  <div class="reply-header-row">
                    <span class="reply-header-text" style="color: #3b82f6;">
                      <i class="fa-regular fa-pen-to-square reply-quote-icon"></i>
                      Đang chỉnh sửa tin nhắn
                    </span>
                  </div>
                  <div class="reply-snippet-text">
                    {{ editingTarget.content }}
                  </div>
                </div>
                <button type="button" class="reply-close-btn" title="Hủy chỉnh sửa" @click="cancelEditMessage">
                  <i class="fa-solid fa-xmark"></i>
                </button>
              </div>

              <!-- Reply Target Preview Bar (Above Input) -->
              <div v-if="replyTarget" class="reply-composer-strip">
                <div class="reply-content-box">
                  <div class="reply-header-row">
                    <span class="reply-header-text">
                      <i class="fa-solid fa-quote-left reply-quote-icon"></i>
                      Trả lời <strong class="reply-user-name">{{ replyTarget.senderName }}</strong>
                    </span>
                  </div>
                  <div class="reply-snippet-text">
                    {{ replyTarget.content || 'Tin nhắn không còn khả dụng' }}
                  </div>
                </div>
                <button type="button" class="reply-close-btn" title="Hủy trả lời" @click="cancelReply">
                  <i class="fa-solid fa-xmark"></i>
                </button>
              </div>

              <!-- Hidden file input for attachment -->
              <input 
                type="file" 
                ref="fileInputRef" 
                style="display: none;" 
                multiple
                accept=".png,.jpg,.jpeg,.webp,.pdf,.txt,.docx,.xlsx,.zip,.rar,.7z"
                @change="handleFileChange" 
              />

              <!-- Attached File Preview Bar -->
              <div v-if="attachedFiles.length" class="attached-files-preview" aria-label="File đã chọn">
                <div v-for="file in attachedFiles" :key="file.id" class="attached-file-preview-bar">
                  <template v-if="file.isAudio">
                    <div class="w-12 h-12 rounded-lg bg-rose-500/20 flex items-center justify-center text-rose-400 flex-shrink-0">
                      <i class="fa-solid fa-microphone text-base animate-pulse"></i>
                    </div>
                    <span class="text-xs truncate font-semibold text-secondary">Tin nhắn thoại (Ghi âm)</span>
                  </template>
                  <img v-else-if="file.previewUrl && (file.type?.startsWith('image/') || isImageFile(file.name))" :src="file.previewUrl" alt="" class="selected-file-thumbnail" />
                  <template v-else>
                    <i :class="getFileIconClass(file.name)" class="text-xl"></i>
                    <span class="text-xs truncate font-semibold text-secondary">{{ file.name }}</span>
                    <span class="text-xxs text-muted">({{ formatFileSize(file.sizeBytes) }})</span>
                  </template>
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

                <el-button
                  size="small"
                  class="btn-secondary"
                  :class="{ 'recording-active-btn': isRecordingAudio }"
                  :title="isRecordingAudio ? 'Đang ghi âm... Nhấn để dừng & gửi' : 'Ghi âm tin nhắn thoại'"
                  :disabled="composerDisabled"
                  @click="toggleVoiceRecording"
                >
                  <i :class="isRecordingAudio ? 'fa-solid fa-square' : 'fa-solid fa-microphone'"></i>
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
                  @paste="handleCallChatPaste"
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
                  :disabled="(composerDisabled && !editingTarget) || (editingTarget && !newMessage.trim())"
                  :aria-label="editingTarget ? 'Lưu thay đổi' : (sendingMessage ? 'Đang gửi tin nhắn' : 'Gửi tin nhắn')"
                  :title="editingTarget ? 'Lưu thay đổi' : (sendingMessage ? 'Đang gửi...' : 'Gửi tin nhắn')"
                  @click="sendMessage"
                >
                  <i :class="editingTarget ? 'fa-solid fa-check text-base' : (sendingMessage ? 'fa-solid fa-spinner fa-spin' : 'fa-solid fa-paper-plane')"></i>
                </button>
              </div>
              <div v-if="newMessage.length >= 3600" class="character-counter">
                {{ newMessage.length }}/4000
              </div>
            </div>
          </div>
        </div>
      </template>

      <aside v-if="channelUtilityOpen && channelUtilityMode === 'search'" class="channel-utility-drawer" aria-label="Channel tools">
        <div class="channel-utility-header">
          <div><span class="context-kicker">CHANNEL TOOLS</span><h3>Tìm tin nhắn</h3></div>
          <button type="button" class="context-close" title="Đóng" @click="channelUtilityOpen = false"><i class="fa-solid fa-xmark"></i></button>
        </div>
        <div class="channel-search-box">
          <input v-model="channelSearchQuery" type="search" placeholder="Tìm trong Channel..." @keydown.enter="searchChannelMessages" />
          <button type="button" class="btn-send" :disabled="channelSearchLoading" @click="searchChannelMessages"><i :class="channelSearchLoading ? 'fa-solid fa-spinner fa-spin' : 'fa-solid fa-arrow-right'"></i></button>
        </div>
        <div class="channel-utility-list">
          <button v-for="result in channelSearchResults" :key="result.messageId" type="button" class="channel-utility-item" @click="focusMessage(result.messageId, result)">
            <strong>{{ result.senderName }}</strong><span>{{ result.content }}</span><small>{{ formatTime(result.sentAt) }}</small>
          </button>
          <span v-if="!channelSearchLoading && channelSearchQuery && !channelSearchResults.length" class="channel-utility-empty">Không tìm thấy tin nhắn phù hợp.</span>
        </div>
      </aside>

      <aside v-if="showMembersSidebar && !showVoiceCallMain" class="chat-context-panel" aria-label="Context panel">
        <div class="context-panel-header">
          <div><span class="context-kicker">CONTEXT</span><h3>{{ (preJoinVoiceChannel || activeVoiceChannel) ? 'Chi tiết kênh thoại' : (activeChat?.type === 'dm' ? 'Chi tiết hội thoại' : 'Chi tiết kênh') }}</h3></div>
          <button type="button" class="context-close" aria-label="Đóng panel context" title="Đóng panel" @click="toggleContextPanel"><i class="fa-solid fa-xmark" aria-hidden="true"></i></button>
        </div>
        <div class="context-tabs" role="tablist" aria-label="Context tabs">
          <button type="button" class="context-tab" :class="{ 'is-active': contextActiveTab === 'details' }" role="tab" :aria-selected="contextActiveTab === 'details'" @click="contextActiveTab = 'details'">Chi tiết</button>
          <button type="button" class="context-tab" :class="{ 'is-active': contextActiveTab === 'files' }" role="tab" :aria-selected="contextActiveTab === 'files'" @click="contextActiveTab = 'files'">Tệp tin</button>
        </div>
        
        <!-- Tab Chi tiết -->
        <div v-if="contextActiveTab === 'details'" class="context-details-content">
          <div v-if="preJoinVoiceChannel || (showVoiceCallMain && activeVoiceChannel)" class="context-call-summary">
            <span class="context-status-dot"></span><div><strong>{{ (preJoinVoiceChannel || activeVoiceChannel)?.name }}</strong><span>{{ showVoiceCallMain && activeVoiceChannel ? participantsInCall.length + ' người trong phòng' : 'Đang chuẩn bị tham gia phòng' }}</span></div>
          </div>
          
          <!-- Thành viên -->
          <section class="context-section">
            <div class="context-section-header">
              <h4>Thành viên ({{ projectMembers.length }})</h4>
              <button v-if="projectMembers.length > 5" type="button" class="context-link-btn" @click="toggleShowAllContextMembers">
                {{ showAllContextMembers ? 'Thu gọn' : 'Xem tất cả' }}
              </button>
            </div>
            <div v-if="loadingMembers" class="context-empty">Đang tải thành viên...</div>
            <div v-else-if="!projectMembers.length" class="context-empty">Chưa có thành viên nào.</div>
            <div v-else class="context-member-list">
              <div v-for="member in (showAllContextMembers ? projectMembers : projectMembers.slice(0, 5))" :key="`context-m-${member.userId || member.id}`" class="context-member-row">
                <div class="context-avatar-wrapper">
                  <el-avatar :size="32" :src="member.avatarUrl || member.avatar">{{ (member.fullName || member.name || '?').charAt(0) }}</el-avatar>
                  <span class="presence-dot is-online" aria-label="Đang hoạt động"></span>
                </div>
                <div class="context-member-copy">
                  <strong>{{ member.fullName || member.name }} {{ member.userId === currentUser.id ? '(bạn)' : '' }}</strong>
                  <span>{{ member.jobTitle || 'Thành viên' }}</span>
                </div>
              </div>
              <div v-if="!showAllContextMembers && projectMembers.length > 5" class="context-members-avatar-row">
                <div v-for="member in projectMembers.slice(5, 7)" :key="`avatar-pill-${member.userId || member.id}`" class="member-avatar-pill">
                  {{ (member.fullName || member.name || '?').charAt(0).toUpperCase() }}
                </div>
                <div v-if="projectMembers.length > 7" class="member-avatar-pill avatar-more-pill" @click="toggleShowAllContextMembers" style="cursor: pointer;">
                  +{{ projectMembers.length - 7 }}
                </div>
              </div>
            </div>
          </section>

          <!-- Mô tả kênh -->
          <section class="context-section">
            <div class="context-section-header">
              <h4>Mô tả {{ (preJoinVoiceChannel || activeVoiceChannel) ? 'kênh thoại' : (activeChat?.type === 'dm' ? 'hội thoại' : 'kênh') }}</h4>
            </div>
            <p class="context-desc-text">
              <template v-if="preJoinVoiceChannel || activeVoiceChannel">
                {{ (preJoinVoiceChannel || activeVoiceChannel)?.desc && !(preJoinVoiceChannel || activeVoiceChannel).desc.startsWith('__voice_chat_channel__') ? (preJoinVoiceChannel || activeVoiceChannel).desc : 'Kênh thoại dùng để trao đổi trực tiếp qua âm thanh và hình ảnh trong dự án.' }}
              </template>
              <template v-else-if="activeChat?.type === 'dm'">
                {{ activeChat?.desc || `Cuộc hội thoại trực tiếp với ${activeChat?.name || 'thành viên'}.` }}
              </template>
              <template v-else>
                {{ activeChannel?.desc && !activeChannel.desc.startsWith('__voice_chat_channel__') ? activeChannel.desc : 'Kênh dùng để thảo luận các vấn đề liên quan đến dự án, cập nhật tiến độ và chia sẻ tài liệu.' }}
              </template>
            </p>
            <button v-if="!preJoinVoiceChannel && !activeVoiceChannel && activeChannel?.canManage" type="button" class="context-edit-desc-btn" @click="openEditDescModal">
              <i class="fa-solid fa-pen"></i> Chỉnh sửa mô tả
            </button>
          </section>

          <!-- Quản lý Kênh / Xóa Kênh (Dành cho Chủ/Quản lý dự án) -->
          <section v-if="canManageChannels && ((!preJoinVoiceChannel && !activeVoiceChannel && activeChat?.type === 'channel' && activeChannel && activeChannel.name.toLowerCase() !== 'general') || ((preJoinVoiceChannel || activeVoiceChannel) && (preJoinVoiceChannel || activeVoiceChannel).id !== 'general-voice'))" class="context-section context-danger-section">
            <div class="context-section-header">
              <h4 style="color: #ef4444; font-weight: 700;">Quản lý Kênh</h4>
            </div>
            <button
              v-if="!preJoinVoiceChannel && !activeVoiceChannel && activeChat?.type === 'channel' && activeChannel && activeChannel.name.toLowerCase() !== 'general'"
              type="button"
              class="context-delete-channel-btn"
              @click="deleteTextChannel(activeChannel, activeProject)"
            >
              <i class="fa-solid fa-trash-can mr-2"></i> Xóa Kênh "#{{ activeChannel.name }}"
            </button>
            <button
              v-else-if="(preJoinVoiceChannel || activeVoiceChannel) && (preJoinVoiceChannel || activeVoiceChannel).id !== 'general-voice'"
              type="button"
              class="context-delete-channel-btn"
              @click="deleteVoiceChannel(preJoinVoiceChannel || activeVoiceChannel, activeProject)"
            >
              <i class="fa-solid fa-trash-can mr-2"></i> Xóa Kênh thoại "{{ (preJoinVoiceChannel || activeVoiceChannel).name }}"
            </button>
          </section>
        </div>

        <!-- Tab Tệp tin -->
        <div v-else-if="contextActiveTab === 'files'" class="context-files-content">
          <!-- Ảnh/Video -->
          <details open class="context-collapsible-group">
            <summary class="context-group-summary">
              <span>Ảnh/Video ({{ channelMediaFiles.length }})</span>
              <i class="fa-solid fa-chevron-down summary-chevron"></i>
            </summary>
            <div class="context-group-body">
              <div v-if="!channelMediaFiles.length" class="context-empty-small">Chưa có ảnh/video</div>
              <div v-else>
                <div class="media-thumbnails-grid">
                  <div v-for="item in (showMoreMedia ? channelMediaFiles : channelMediaFiles.slice(0, 8))" :key="`media-${item.attachmentId}`" class="media-thumbnail-item" @click="openImageLightbox(item.previewUrl || item.fileUrl, item.originalFileName)" title="Xem phóng to">
                    <img v-if="item.previewUrl || item.fileUrl" :src="item.previewUrl || item.fileUrl" :alt="item.originalFileName" />
                    <div v-else class="media-thumb-placeholder"><i class="fa-solid fa-image"></i></div>
                  </div>
                </div>
                <button
                  v-if="channelMediaFiles.length > 8"
                  type="button"
                  class="sidebar-see-more-btn w-full text-center text-xs font-semibold py-2 mt-2.5 rounded-xl border border-sky-500/30 text-sky-400 bg-sky-500/10 hover:bg-sky-500/20 active:scale-[0.98] transition-all shadow-sm flex items-center justify-center gap-1.5"
                  @click="showMoreMedia = !showMoreMedia"
                >
                  <span>{{ showMoreMedia ? 'Thu gọn' : `Xem thêm (${channelMediaFiles.length - 8} ảnh)` }}</span>
                  <i :class="showMoreMedia ? 'fa-solid fa-chevron-up text-[10px]' : 'fa-solid fa-chevron-down text-[10px]'"></i>
                </button>
              </div>
            </div>
          </details>

          <!-- File -->
          <details open class="context-collapsible-group">
            <summary class="context-group-summary">
              <span>File ({{ channelDocumentFiles.length }})</span>
              <i class="fa-solid fa-chevron-down summary-chevron"></i>
            </summary>
            <div class="context-group-body">
              <div v-if="!channelDocumentFiles.length" class="context-empty-small">Chưa có tệp tin</div>
              <div v-else>
                <div class="context-files-list">
                  <div
                    v-for="file in (showMoreDocs ? channelDocumentFiles : channelDocumentFiles.slice(0, 4))"
                    :key="`file-${file.attachmentId}`"
                    class="context-file-item cursor-pointer hover:opacity-90"
                    @click="file.messageId && focusMessage(file.messageId)"
                    title="Đi đến tin nhắn chứa tệp này"
                  >
                    <i :class="getFileIconClass(file.originalFileName)" class="text-lg"></i>
                    <div class="file-item-info">
                      <span class="file-name truncate">{{ file.originalFileName }}</span>
                      <span class="file-size">{{ formatFileSize(file.sizeBytes) }}</span>
                    </div>
                    <button type="button" class="file-download-action" @click.stop="downloadAttachment(file)" title="Tải xuống">
                      <i class="fa-solid fa-download"></i>
                    </button>
                  </div>
                </div>
                <button
                  v-if="channelDocumentFiles.length > 4"
                  type="button"
                  class="sidebar-see-more-btn w-full text-center text-xs font-semibold py-2 mt-2.5 rounded-xl border border-sky-500/30 text-sky-400 bg-sky-500/10 hover:bg-sky-500/20 active:scale-[0.98] transition-all shadow-sm flex items-center justify-center gap-1.5"
                  @click="showMoreDocs = !showMoreDocs"
                >
                  <span>{{ showMoreDocs ? 'Thu gọn' : `Xem thêm (${channelDocumentFiles.length - 4} tệp)` }}</span>
                  <i :class="showMoreDocs ? 'fa-solid fa-chevron-up text-[10px]' : 'fa-solid fa-chevron-down text-[10px]'"></i>
                </button>
              </div>
            </div>
          </details>

          <!-- Link -->
          <details open class="context-collapsible-group">
            <summary class="context-group-summary">
              <span>Link ({{ channelExtractedLinks.length }})</span>
              <i class="fa-solid fa-chevron-down summary-chevron"></i>
            </summary>
            <div class="context-group-body">
              <div v-if="!channelExtractedLinks.length" class="context-empty-small">Chưa có liên kết</div>
              <div v-else>
                <div class="context-links-list">
                  <div
                    v-for="(link, idx) in (showMoreLinks ? channelExtractedLinks : channelExtractedLinks.slice(0, 4))"
                    :key="`link-${idx}`"
                    class="context-link-item cursor-pointer hover:opacity-90"
                    @click="link.messageId && focusMessage(link.messageId)"
                    title="Đi đến tin nhắn chứa liên kết này"
                  >
                    <i class="fa-solid fa-link link-item-icon"></i>
                    <div class="link-item-info">
                      <span class="link-url truncate text-sky-400 underline">{{ link.url }}</span>
                      <span class="link-meta">{{ link.senderName }} • {{ formatTime(link.sentAt) }}</span>
                    </div>
                    <a :href="link.url" target="_blank" rel="noopener noreferrer" class="link-ext-icon p-1 hover:text-sky-400" @click.stop title="Mở liên kết trong tab mới">
                      <i class="fa-solid fa-arrow-up-right-from-square"></i>
                    </a>
                  </div>
                </div>
                <button
                  v-if="channelExtractedLinks.length > 4"
                  type="button"
                  class="sidebar-see-more-btn w-full text-center text-xs font-semibold py-2 mt-2.5 rounded-xl border border-sky-500/30 text-sky-400 bg-sky-500/10 hover:bg-sky-500/20 active:scale-[0.98] transition-all shadow-sm flex items-center justify-center gap-1.5"
                  @click="showMoreLinks = !showMoreLinks"
                >
                  <span>{{ showMoreLinks ? 'Thu gọn' : `Xem thêm (${channelExtractedLinks.length - 4} liên kết)` }}</span>
                  <i :class="showMoreLinks ? 'fa-solid fa-chevron-up text-[10px]' : 'fa-solid fa-chevron-down text-[10px]'"></i>
                </button>
              </div>
            </div>
          </details>
        </div>
      </aside>

      <aside v-if="aiAnalysisOpen" class="ai-analysis-surface" aria-live="polite" aria-label="AI analysis">
        <div class="ai-surface-header"><div><span class="context-kicker">SPRINTA AI</span><h3>{{ aiAnalysisScope === 'call' ? 'AI chỉ dùng cho văn bản' : 'Phân tích cuộc trò chuyện' }}</h3></div><button type="button" class="context-close" aria-label="Đóng AI analysis" title="Đóng" @click="aiAnalysisOpen = false"><i class="fa-solid fa-xmark" aria-hidden="true"></i></button></div>
        <div v-if="aiAnalysisScope === 'call'" class="ai-text-only-notice">
          <strong>Không phân tích cuộc gọi</strong>
          <p>AI không ghi âm, phiên âm, lắng nghe microphone hoặc đọc camera, screen-share.</p>
        </div>
        <template v-else>
          <div v-if="!aiAnalysisResult && !aiAnalysisLoading && !aiAnalysisError" class="ai-off-state-panel">
            <div class="ai-off-banner"><span class="ai-state-indicator"></span><strong>AI đang OFF</strong></div>
            <p>Chỉ phân tích các tin nhắn văn bản trong channel hiện tại khi bạn chủ động yêu cầu.</p>
            <button type="button" class="ai-primary-action" @click="runAiAnalysis()">Phân tích bằng AI</button>
          </div>
          <div v-else-if="aiAnalysisLoading" class="ai-loading-state" role="status">
            <span class="ai-loading-line"></span><strong>Đang phân tích tin nhắn văn bản...</strong><small>Chưa có dữ liệu nào được gửi từ cuộc gọi hoặc camera.</small>
          </div>
          <div v-else-if="aiAnalysisError" class="ai-error-state" role="alert">
            <strong>{{ aiAnalysisError }}</strong><p>Không có kết quả thay thế được tạo.</p><button type="button" class="ai-secondary-action" @click="runAiAnalysis({ retry: true })">Thử lại</button>
          </div>
          <div v-else class="ai-result-content">
            <div class="ai-result-meta"><span>{{ aiAnalysisResult.sourceMessageCount }} tin nhắn văn bản</span><button type="button" class="ai-inline-action" @click="runAiAnalysis({ retry: true })">Phân tích lại</button></div>
            <section class="ai-result-section"><h4>Tóm tắt cuộc trò chuyện</h4><p>{{ aiAnalysisResult.summary || 'Chưa có tóm tắt đáng tin cậy.' }}</p></section>
            <section class="ai-result-section"><h4>Quyết định đã thống nhất</h4><p v-if="!aiAnalysisResult.decisions?.length" class="ai-muted-copy">Không phát hiện quyết định đã được xác nhận.</p><div v-for="(decision, index) in aiAnalysisResult.decisions" :key="`ai-decision-${index}`" class="ai-result-item"><p>{{ decision.text }}</p><div class="ai-evidence-row"><button v-for="messageId in decision.evidenceMessageIds" :key="messageId" type="button" class="ai-evidence-link" @click="focusMessage(messageId)">Tin nhắn dẫn chứng</button></div></div></section>
            <section class="ai-result-section"><h4>Việc cần làm</h4><p v-if="!aiAnalysisResult.actionItems?.length" class="ai-muted-copy">Không phát hiện việc cần làm rõ ràng.</p><div v-for="(item, index) in aiAnalysisResult.actionItems" :key="`ai-action-${index}`" class="ai-result-item"><p>{{ item.text }}</p><small>{{ item.assigneeCandidate ? `Người phụ trách: ${item.assigneeCandidate}` : 'Chưa xác định người phụ trách' }} · {{ item.deadlineCandidate ? `Hạn: ${item.deadlineCandidate}` : 'Chưa xác định hạn' }}</small><div class="ai-evidence-row"><button v-for="messageId in item.evidenceMessageIds" :key="messageId" type="button" class="ai-evidence-link" @click="focusMessage(messageId)">Tin nhắn dẫn chứng</button></div></div></section>
            <section class="ai-result-section"><h4>Điểm chưa rõ / cần xác nhận</h4><p v-if="!aiAnalysisResult.openQuestions?.length" class="ai-muted-copy">Không phát hiện câu hỏi còn mở.</p><div v-for="(item, index) in aiAnalysisResult.openQuestions" :key="`ai-question-${index}`" class="ai-result-item"><p>{{ item.text }}</p><div class="ai-evidence-row"><button v-for="messageId in item.evidenceMessageIds" :key="messageId" type="button" class="ai-evidence-link" @click="focusMessage(messageId)">Tin nhắn dẫn chứng</button></div></div></section>
            <section class="ai-result-section ai-question-section"><h4>Hỏi AI về cuộc trò chuyện</h4><form @submit.prevent="askAiQuestion"><input v-model="aiQuestion" type="text" maxlength="500" placeholder="Ví dụ: Ai đã nhận việc này?" :disabled="aiAnalysisLoading" /><button type="submit" class="ai-primary-action" :disabled="aiAnalysisLoading || !aiQuestion.trim()">Hỏi AI</button></form><div v-if="aiAnalysisResult.questionAnswer" class="ai-answer"><strong>{{ aiAnalysisResult.questionAnswer.unsupported ? 'Chưa đủ thông tin' : 'Trả lời' }}</strong><p>{{ aiAnalysisResult.questionAnswer.answer }}</p><div class="ai-evidence-row"><button v-for="messageId in aiAnalysisResult.questionAnswer.evidenceMessageIds" :key="messageId" type="button" class="ai-evidence-link" @click="focusMessage(messageId)">Tin nhắn dẫn chứng</button></div></div></section>
          </div>
        </template>
      </aside>
    </div>



    <!-- Fullscreen Professional Media Viewer Overlay (Sample 2 Pixel Perfect) -->
    <Teleport to="body">
      <!-- Image Lightbox Fullscreen Viewer (Dark Overlay Backdrop) -->
      <Transition name="el-fade-in-linear">
        <div v-if="showImageLightbox" class="fixed inset-0 z-[99999] flex flex-col bg-black/90 text-slate-100 overflow-hidden select-none font-sans" @click.self="showImageLightbox = false">
          <!-- Top Bar Header (Centered title) -->
          <div class="h-10 px-4 flex items-center justify-between bg-black/40 backdrop-blur-md border-b border-white/10 flex-shrink-0 relative z-20">
            <div class="w-20"></div>
            <div class="flex-1 text-center font-medium text-xs text-slate-200 truncate tracking-wide px-4">
              {{ activeProject?.name || activeChat?.name || 'HÌNH ẢNH CHI TIẾT' }}
            </div>
            <div class="w-20 flex justify-end">
              <button type="button" class="w-7 h-7 flex items-center justify-center text-slate-300 hover:text-white hover:bg-white/10 rounded transition-colors" @click="showImageLightbox = false" title="Đóng (Esc)">
                <i class="fa-solid fa-xmark text-base"></i>
              </button>
            </div>
          </div>

          <!-- Main Workspace Row -->
          <div class="flex-1 flex min-h-0 relative bg-black/80" @click.self="showImageLightbox = false">
            <!-- Center Viewer Area (Click dark area to close) -->
            <div class="flex-1 flex items-center justify-center p-4 relative bg-transparent overflow-hidden" @click.self="showImageLightbox = false">
              <img
                v-if="currentLightboxItem"
                :src="currentLightboxItem.previewUrl || currentLightboxItem.fileUrl || currentLightboxItem.url"
                :alt="currentLightboxItem.originalFileName"
                class="max-w-[92vw] max-h-[88vh] object-contain shadow-2xl transition-all duration-200 cursor-default rounded"
                @click.stop
              />
            </div>

            <!-- Right Sidebar Media History Strip (Sample 2 vertical thumbnails + up/down arrows) -->
            <div class="w-48 bg-[#252525] border-l border-[#353535] flex flex-col p-3 flex-shrink-0 relative">
              <div class="text-[11px] font-medium text-slate-300 mb-2 px-1">Hôm nay</div>
              
              <div class="flex-1 overflow-y-auto pr-0 flex flex-col gap-3 scrollbar-none">
                <div
                  v-for="(item, idx) in lightboxMediaList"
                  :key="`thumb-${item.attachmentId || idx}`"
                  class="relative rounded overflow-hidden cursor-pointer transition-all aspect-[4/3] bg-[#1a1a1a] flex items-center justify-center"
                  :class="idx === lightboxActiveIndex ? 'border-2 border-white shadow-md' : 'border border-transparent opacity-50 hover:opacity-100'"
                  @click="selectLightboxIndex(idx)"
                >
                  <img :src="item.previewUrl || item.fileUrl" :alt="item.originalFileName" class="w-full h-full object-cover" />
                </div>
              </div>

              <!-- Up & Down Nav Buttons on Right Sidebar (Sample 2 style) -->
              <div v-if="lightboxMediaList.length > 1" class="absolute right-3 top-1/2 -translate-y-1/2 flex flex-col gap-2 z-10">
                <button
                  type="button"
                  class="w-9 h-9 rounded-full bg-black/75 hover:bg-black text-white flex items-center justify-center border border-white/20 shadow-lg disabled:opacity-30 disabled:pointer-events-none transition-colors"
                  :disabled="lightboxActiveIndex <= 0"
                  @click="selectLightboxIndex(lightboxActiveIndex - 1)"
                  title="Ảnh trên"
                >
                  <i class="fa-solid fa-chevron-up text-sm"></i>
                </button>
                <button
                  type="button"
                  class="w-9 h-9 rounded-full bg-black/75 hover:bg-black text-white flex items-center justify-center border border-white/20 shadow-lg disabled:opacity-30 disabled:pointer-events-none transition-colors"
                  :disabled="lightboxActiveIndex >= lightboxMediaList.length - 1"
                  @click="selectLightboxIndex(lightboxActiveIndex + 1)"
                  title="Ảnh dưới"
                >
                  <i class="fa-solid fa-chevron-down text-sm"></i>
                </button>
              </div>
            </div>
          </div>

          <!-- Bottom Toolbar (Sample 2 toolbar icons & user info) -->
          <div class="h-12 px-5 flex items-center justify-between bg-[#2d2d2d] border-t border-[#3d3d3d] flex-shrink-0 text-slate-300">
            <div class="flex items-center gap-2.5">
              <el-avatar :size="28" :src="currentLightboxItem?.senderAvatar" class="border border-white/20">{{ (currentLightboxItem?.senderName || '?').charAt(0) }}</el-avatar>
              <div class="flex flex-col">
                <span class="text-xs font-medium text-slate-200 leading-tight">{{ currentLightboxItem?.senderName || 'Thành viên' }}</span>
                <span class="text-[10px] text-slate-400 leading-tight">{{ formatTime(currentLightboxItem?.sentAt) }} Hôm nay</span>
              </div>
            </div>

            <!-- Toolbar Action Icons (Sample 2 center tools) -->
            <div class="flex items-center gap-5 text-sm">
              <button type="button" class="hover:text-white transition-colors" title="Chia sẻ"><i class="fa-solid fa-share-nodes"></i></button>
              <button type="button" class="hover:text-white transition-colors" title="Tải xuống" @click="downloadCurrentLightboxImage"><i class="fa-solid fa-download"></i></button>
              <span class="w-[1px] h-3.5 bg-white/20"></span>
              <button type="button" class="hover:text-white transition-colors" title="Xoay ảnh"><i class="fa-solid fa-rotate-right"></i></button>
              <button type="button" class="hover:text-white transition-colors" title="Phóng to"><i class="fa-solid fa-magnifying-glass-plus"></i></button>
              <button type="button" class="hover:text-white transition-colors" title="Thu nhỏ"><i class="fa-solid fa-magnifying-glass-minus"></i></button>
            </div>

            <!-- Right Toolbar Icons -->
            <div class="flex items-center gap-3 text-sm">
              <button type="button" class="hover:text-white transition-colors" title="Thích"><i class="fa-regular fa-thumbs-up"></i></button>
              <button type="button" class="hover:text-white transition-colors" title="Chế độ chia ô"><i class="fa-regular fa-rectangle-list"></i></button>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>

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

    <el-dialog
      v-model="showCaptionConsentModal"
      width="min(420px, calc(100vw - 32px))"
      append-to-body
      class="caption-consent-dialog"
      :show-close="true"
      :close-on-click-modal="false"
      :close-on-press-escape="!captionConsentSubmitting"
      @close="cancelCaptionConsent"
    >
      <template #header>
        <div class="caption-consent-heading">
          <span class="caption-consent-icon" aria-hidden="true"><i class="fa-solid fa-closed-captioning"></i></span>
          <div><span class="context-kicker">PHỤ ĐỀ TRỰC TIẾP</span><h3>Bật phụ đề trực tiếp?</h3></div>
        </div>
      </template>
      <div class="caption-consent-copy">
        <p>Giọng nói trong cuộc gọi sẽ được gửi để chuyển thành văn bản trực tiếp.</p>
        <small>Không lưu âm thanh gốc.</small>
      </div>
      <template #footer>
        <div class="caption-consent-actions">
          <button type="button" class="ai-secondary-action" :disabled="captionConsentSubmitting" @click="cancelCaptionConsent">Hủy</button>
          <button type="button" class="ai-primary-action" :disabled="captionConsentSubmitting" @click="respondCallAiConsent(true)">
            {{ captionConsentSubmitting ? 'Đang bật…' : 'Cho phép & bật phụ đề' }}
          </button>
        </div>
      </template>
    </el-dialog>

  </main>
</template>

<script setup>
defineOptions({
  name: 'CollaborationChat'
})

import { ref, onMounted, onBeforeUnmount, nextTick, watch, computed } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import axiosClient from '@/api/axiosClient'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import DataModalField from '@/components/common/Foundation/DataModalField.vue'
import LiveCaptionOverlay from '@/components/collaboration/LiveCaptionOverlay.vue'

import { collaborationApi } from '@/api/collaborationApi'
import { useProjectStore } from '@/store/useProjectStore'
import { useAuthStore } from '@/store/useAuthStore'
import { useVoiceCallStore } from '@/store/useVoiceCallStore'

import {
  collaborationRealtime,
  COLLABORATION_REALTIME_STATES,
  getCollaborationHubErrorCode
} from '@/services/collaborationRealtime'
import { createCallMediaSession, traceCallHubLifecycle, traceWebRtcMedia } from '@/services/callMediaService'
import {
  dedupeParticipantsByUser,
  getBoundedCallStageParticipants,
  getMeetingLayoutMode,
  getMeetingRenderCollections
} from '@/services/meetingLayoutState'
import {
  createMeetingPictureInPictureController,
  isDocumentPictureInPictureSupported
} from '@/services/meetingPictureInPicture'
import {
  clearLiveCaptions,
  isLiveCaptionForSession,
  normalizeLiveCaptionEvent,
  normalizeTranscriptChunkEvent,
  removeTranscriptInterim,
  removeExpiredLiveCaptions,
  upsertLiveCaptionFinal,
  upsertLiveCaptionInterim,
  upsertTranscriptHistory,
  upsertTranscriptInterim
} from '@/services/liveCaptionState'
import {
  clearScopedCurrentProjectId,
  getScopedCurrentProjectId,
  setScopedCurrentProjectId
} from '@/utils/projectContext'

const voiceCallStore = useVoiceCallStore()

let captionRenderDiagnosticCount = 0
const traceCaptionRender = (resultType, receivedAt) => {
  try {
    if (globalThis.localStorage?.getItem('debug_caption_transport') !== '1') return
    captionRenderDiagnosticCount += 1
    if (captionRenderDiagnosticCount !== 1 && captionRenderDiagnosticCount % 20 !== 0) return
    console.info('[CAPTION_RENDER_DIAG]', {
      timestamp: new Date().toISOString(),
      resultType,
      eventToDomMs: Math.max(0, Math.round(performance.now() - receivedAt))
    })
  } catch {
    // Diagnostics remain optional when browser storage is unavailable.
  }
}

const meetingLayoutCorrelation = (prefix, value, map) => {
  const rawValue = `${value || ''}`
  if (!rawValue) return ''
  if (!map.has(rawValue)) map.set(rawValue, `${prefix}-${map.size + 1}`)
  return map.get(rawValue)
}

const meetingLayoutUserKeys = new Map()
const meetingLayoutConnectionKeys = new Map()
let meetingLayoutDiagnosticSignature = ''
let meetingLayoutDiagnosticQueued = false
const meetingLayoutTraceEnabled = () => {
  return Boolean(import.meta.env?.DEV)
}
const traceMeetingLayout = (event, detail = {}) => {
  if (!meetingLayoutTraceEnabled()) return
  console.info('[MEETING_LAYOUT_DIAG]', {
    timestamp: new Date().toISOString(),
    event,
    ...detail
  })
}

const route = useRoute()
const projectStore = useProjectStore()
const authStore = useAuthStore()
const currentTab = ref('channel')
const switchTab = (tab) => {
  if (tab !== 'channel' && tab !== 'dm') return
  currentTab.value = tab
}

const projectOptions = computed(() => projectStore.sidebarProjects)
const activeProjectId = ref('')
const navigatorQuery = ref('')
const expandedProjectId = ref('')
const expandedProjectBeforeSearch = ref('')
const channelsByProjectId = ref({})
const voiceChannelsByProjectId = ref({})
const normalizeNavigatorText = (value) => `${value || ''}`.trim().toLocaleLowerCase()
const projectChannels = (projectId) => projectId === activeProjectId.value
  ? visibleChannels.value
  : (channelsByProjectId.value[projectId] || []).filter(channel => !channel.desc?.startsWith('__voice_chat_channel__'))
const projectVoiceChannels = (projectId) => projectId === activeProjectId.value
  ? voiceChannels.value
  : (voiceChannelsByProjectId.value[projectId] || [])
const projectHasNavigatorMatch = (project) => {
  const query = normalizeNavigatorText(navigatorQuery.value)
  if (!query) return true
  return normalizeNavigatorText(project.name).includes(query)
    || projectChannels(project.id).some(channel => normalizeNavigatorText(channel.name).includes(query))
    || projectVoiceChannels(project.id).some(channel => normalizeNavigatorText(channel.name).includes(query))
}
const filteredProjects = computed(() => projectOptions.value.filter(projectHasNavigatorMatch))
const channelsForNavigator = (project) => {
  const items = projectChannels(project.id)
  const query = normalizeNavigatorText(navigatorQuery.value)
  if (!query || normalizeNavigatorText(project.name).includes(query)) return items
  return items.filter(channel => normalizeNavigatorText(channel.name).includes(query))
}
const voiceChannelsForProject = (project) => {
  const items = projectVoiceChannels(project.id)
  const query = normalizeNavigatorText(navigatorQuery.value)
  if (!query || normalizeNavigatorText(project.name).includes(query)) return items
  return items.filter(channel => normalizeNavigatorText(channel.name).includes(query))
}
const projectUnreadCount = (project) => [...projectChannels(project.id), ...projectVoiceChannels(project.id)]
  .reduce((total, channel) => total + Math.max(0, Number(channel.unreadCount || 0)), 0)
const toggleProject = (projectId) => {
  if (!projectOptions.value.some(project => project.id === projectId)) return
  if (expandedProjectId.value === projectId) {
    expandedProjectId.value = ''
    return
  }
  expandedProjectId.value = projectId
  if (activeProjectId.value !== projectId) selectProject(projectId)
}
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
      voiceChannelsByProjectId.value = { ...voiceChannelsByProjectId.value, [projectId]: voiceChannels.value }
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
  voiceChannelsByProjectId.value = { ...voiceChannelsByProjectId.value, [projectId]: defaultVcs }
  localStorage.setItem(key, JSON.stringify(defaultVcs))
}

const saveVoiceChannels = () => {
  if (!activeProjectId.value) return
  const key = `voice_channels_${activeProjectId.value}`
  voiceChannelsByProjectId.value = { ...voiceChannelsByProjectId.value, [activeProjectId.value]: voiceChannels.value }
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
const highlightedMessageId = ref('')
let highlightTimer = null
const activeChannel = computed(() =>
  activeChat.value?.type === 'channel' ? activeChat.value : null
)
const activeDirectConversation = computed(() =>
  activeChat.value?.type === 'dm' ? activeChat.value : null
)
const newMessage = ref('')
const replyTarget = ref(null)
const editingTarget = ref(null)

const cancelEditMessage = () => {
  editingTarget.value = null
  newMessage.value = ''
}
const quickReactionList = ['👍', '❤️', '😂', '🎉', '👀']
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
const aiAnalysisResult = ref(null)
const aiAnalysisLoading = ref(false)
const aiAnalysisError = ref('')
const aiQuestion = ref('')
const aiRequestId = ref('')
const openAiAnalysis = (scope = 'text') => {
  aiAnalysisScope.value = scope
  if (scope === 'text') {
    aiAnalysisResult.value = null
    aiAnalysisError.value = ''
    aiQuestion.value = ''
    aiRequestId.value = ''
  }
  aiAnalysisOpen.value = true
}
const aiErrorMessage = (error) => {
  const status = error?.response?.status
  if (status === 402) return 'Không đủ credit AI để phân tích channel.'
  if (status === 503) return 'Dịch vụ AI tạm thời không khả dụng. Hãy thử lại sau.'
  return error?.response?.data?.message || error?.message || 'Không thể tạo phân tích AI.'
}
const runAiAnalysis = async ({ retry = false } = {}) => {
  if (aiAnalysisLoading.value || !activeChannel.value?.id) return
  if (!retry || !aiRequestId.value) aiRequestId.value = crypto.randomUUID()
  aiAnalysisLoading.value = true
  aiAnalysisError.value = ''
  try {
    aiAnalysisResult.value = await collaborationApi.analyzeChannelWithAi(activeChannel.value.id, {
      requestId: aiRequestId.value,
      messageIds: [],
      question: null
    })
  } catch (error) {
    aiAnalysisError.value = aiErrorMessage(error)
  } finally {
    aiAnalysisLoading.value = false
  }
}
const askAiQuestion = async () => {
  const question = aiQuestion.value.trim()
  if (!question || aiAnalysisLoading.value || !activeChannel.value?.id) return
  aiRequestId.value = crypto.randomUUID()
  aiAnalysisLoading.value = true
  aiAnalysisError.value = ''
  try {
    aiAnalysisResult.value = await collaborationApi.analyzeChannelWithAi(activeChannel.value.id, {
      requestId: aiRequestId.value,
      messageIds: [],
      question
    })
  } catch (error) {
    aiAnalysisError.value = aiErrorMessage(error)
  } finally {
    aiAnalysisLoading.value = false
  }
}
const isCallCameraOn = ref(false)
const activeVoiceChannel = ref(null)
const showVoiceCallMain = ref(false)
const isSharingScreen = ref(false)
const cameraBackgroundEffect = ref('none')
const cameraEffectPending = ref(false)
const cameraEffectNotice = ref('')
const showCameraEffectsMenu = ref(false)
const showMoreMenu = ref(false)
const moreMenuSection = ref('')
const showTranscriptPanel = ref(false)
const showCaptionConsentModal = ref(false)
const captionConsentSubmitting = ref(false)
const callHandRaised = ref(false)
const callReactions = ref([])
const callLiveNotice = ref('')
const callDevices = ref([])
const presentationStage = ref(null)
const meetingShell = ref(null)
const presentationFocused = ref(false)
const presentationIsFullscreen = ref(false)
const callViewMode = ref('auto')
const callMicrophoneEnabled = ref(true)
const callParticipants = ref([])
const remoteStreams = ref(new Map())
const localCallStream = ref(null)
const localScreenStream = ref(null)
const callConnectionId = ref('')
const callState = ref('disconnected')
const callError = ref('')
const callSession = ref(null)
let callJoinPromise = null
const callAiState = ref({ state: 'OFF', callSessionId: '', consentGeneration: 0, participants: [] })
const callTranscriptChunks = ref([])
const callTranscriptInterims = ref([])
const liveCaptionRows = ref([])
let liveCaptionExpirySweep = null
const callTranscriptionCapabilities = ref({ configured: false, provider: 'Unavailable', supportedLanguages: [], defaultLanguage: 'vi', aiConfigured: false, aiProvider: 'Unavailable', aiTranscriptChunkSize: 8 })
const callMeetingAiReport = ref(null)
let callMeetingAiRefreshTimer = null
const callCaptionLanguage = ref('vi')
const captionsEnabled = ref(false)
const localVideoElements = new Map()
const presentationVideoElement = ref(null)
const remoteVideoElements = new Map()
const remoteAudioElements = new Map()
const blockedMediaElements = new Set()
const focusedParticipantConnectionId = ref('')
const preJoinVoiceChannel = ref(null)
const voiceJoinPending = ref(false)
const voiceJoiningChannelName = ref('')
const preJoinMicEnabled = ref(true)
const preJoinCameraEnabled = ref(false)
const preJoinVideo = ref(null)
const preJoinPreviewStream = ref(null)
const preJoinMicrophoneId = ref('')
const preJoinCameraId = ref('')
const callChatOpen = ref(false)
const callChatDraft = ref('')
const callChatSending = ref(false)
const callChatThread = ref(null)
const callChatComposer = ref(null)
const callChatMessages = ref([])
const selectedCallMicrophoneId = ref('')
const selectedCallCameraId = ref('')
const selectedCallSpeakerId = ref('')
const speakerSelectionSupported = ref(false)
const audioInputDevices = computed(() => callDevices.value.filter(device => device.kind === 'audioinput'))
const videoInputDevices = computed(() => callDevices.value.filter(device => device.kind === 'videoinput'))
const audioOutputDevices = computed(() => callDevices.value.filter(device => device.kind === 'audiooutput'))
const workspaceState = computed(() => {
  if (preJoinVoiceChannel.value) return 'VOICE_PRE_JOIN'
  if (voiceJoinPending.value) return 'VOICE_JOINING'
  if (showVoiceCallMain.value && activeVoiceChannel.value) return 'VOICE_IN_CALL'
  return 'TEXT_CHANNEL'
})

// Restore existing callSession and state from global voiceCallStore if returning to CollaborationChat while call is active
if (voiceCallStore.hasActiveCall && voiceCallStore.callSession) {
  callSession.value = voiceCallStore.callSession
  activeVoiceChannel.value = voiceCallStore.activeVoiceChannel
  showVoiceCallMain.value = true
  callChatOpen.value = true
  callMicrophoneEnabled.value = voiceCallStore.isMicEnabled
  isCallCameraOn.value = voiceCallStore.isCameraEnabled
  isSharingScreen.value = voiceCallStore.isScreenSharing
}
const callChatConnected = computed(() => Boolean(
  callSession.value &&
  callState.value === 'connected' &&
  callConnectionId.value &&
  callSession.value.isJoined?.() &&
  callSession.value.getCallSessionId?.()))
const channelUtilityOpen = ref(false)
const channelUtilityMode = ref('search')
const channelSearchQuery = ref('')
const channelSearchResults = ref([])
const channelSearchLoading = ref(false)
const pinnedMessages = ref([])
const pinsLoading = ref(false)

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
    ? localScreenStream.value
    : remoteStreams.value.get(presenter.connectionId)?.screenStream || null
}

const hasLiveVideoTrack = stream => stream?.getVideoTracks?.().some(track => track.readyState === 'live' && track.enabled !== false) === true
const hasLiveAudioTrack = stream => stream?.getAudioTracks?.().some(track => track.readyState === 'live' && track.enabled !== false) === true
const isParticipantVideoVisible = user => user.connectionId === callConnectionId.value
  ? isCallCameraOn.value && hasLiveVideoTrack(localCallStream.value)
  : hasLiveVideoTrack(remoteStreams.value.get(user.connectionId)?.cameraStream)
const pictureInPictureUnsupportedMessage = 'Trình duyệt của bạn không hỗ trợ Picture-in-Picture.'
const pictureInPictureNoVideoMessage = 'Hãy bật camera hoặc chia sẻ màn hình để sử dụng Picture-in-Picture.'
const documentPictureInPictureSupported = () => typeof window !== 'undefined' &&
  isDocumentPictureInPictureSupported(window)
const standardPictureInPictureSupported = () => typeof document !== 'undefined' &&
  document.pictureInPictureEnabled === true &&
  typeof HTMLVideoElement !== 'undefined' &&
  typeof HTMLVideoElement.prototype.requestPictureInPicture === 'function'
const hasEligiblePictureInPictureVideo = computed(() => {
  if (documentPictureInPictureSupported()) return callParticipants.value.length > 0
  const hasRenderedPresentation = activePresenter.value && callViewMode.value !== 'tiled' && hasLiveVideoTrack(activePresenterStream())
  if (hasRenderedPresentation) return true
  return callParticipants.value.some(isParticipantVideoVisible)
})
const pictureInPictureActionLabel = computed(() => {
  if (!documentPictureInPictureSupported() && !standardPictureInPictureSupported()) return pictureInPictureUnsupportedMessage
  return hasEligiblePictureInPictureVideo.value ? 'Picture-in-picture' : pictureInPictureNoVideoMessage
})
const isParticipantSpeaking = user => user.isSpeaking === true || user.speaking === true || user.activeSpeaker === true
const participantsInCall = computed(() => dedupeParticipantsByUser(callParticipants.value, callConnectionId.value))
const remoteAudioParticipants = computed(() => participantsInCall.value.filter(user =>
  user.connectionId !== callConnectionId.value &&
  hasLiveAudioTrack(remoteStreams.value.get(user.connectionId)?.audioStream)
))
const meetingPictureInPicture = createMeetingPictureInPictureController()
const getMeetingPictureInPictureSnapshot = () => ({
  meetingName: activeVoiceChannel.value?.name || 'Cuộc họp đang diễn ra',
  participants: participantsInCall.value.map(user => ({
    connectionId: user.connectionId,
    displayName: user.connectionId === callConnectionId.value ? (currentUser.value.name || 'Bạn') : user.displayName,
    avatarUrl: user.connectionId === callConnectionId.value ? currentUser.value.avatar : user.avatarUrl,
    isLocal: user.connectionId === callConnectionId.value,
    isSpeaking: isParticipantSpeaking(user),
    cameraEnabled: user.connectionId === callConnectionId.value ? isCallCameraOn.value : user.cameraEnabled,
    cameraStream: user.connectionId === callConnectionId.value
      ? localCallStream.value
      : remoteStreams.value.get(user.connectionId)?.cameraStream || null
  })),
  presentation: activePresenter.value && hasLiveVideoTrack(activePresenterStream())
    ? { displayName: activePresenter.value.displayName, stream: activePresenterStream() }
    : null
})
const syncMeetingPictureInPicture = () => {
  if (!meetingPictureInPicture.isOpen()) return
  if (!participantsInCall.value.length) {
    meetingPictureInPicture.close()
    return
  }
  meetingPictureInPicture.update(getMeetingPictureInPictureSnapshot())
}
const focusedCallParticipant = computed(() => participantsInCall.value.find(user =>
  user.connectionId === focusedParticipantConnectionId.value
))
const hasCallParticipants = computed(() => participantsInCall.value.length > 0)
const effectiveFocusedParticipantId = computed(() => focusedCallParticipant.value?.connectionId || (
  ['spotlight', 'sidebar'].includes(callViewMode.value) ? participantsInCall.value[0]?.connectionId || '' : ''
))
const callLayoutMode = computed(() => {
  if (callViewMode.value === 'tiled') return 'CAMERA_GRID'
  if (callViewMode.value === 'spotlight' && activePresenter.value) return 'PRESENTATION_FOCUS'
  if (callViewMode.value === 'sidebar' && activePresenter.value) return 'PRESENTATION'
  if (!activePresenter.value && !focusedParticipantConnectionId.value) return 'CAMERA_GRID'
  return getMeetingLayoutMode({
    hasPresenter: Boolean(activePresenter.value),
    presentationFocused: presentationFocused.value,
    focusedParticipantId: effectiveFocusedParticipantId.value,
    participantCount: participantsInCall.value.length
  })
})
const meetingRenderCollections = computed(() => getMeetingRenderCollections({
  mode: callLayoutMode.value,
  participantsInCall: participantsInCall.value,
  focusedParticipantId: effectiveFocusedParticipantId.value
}))
const cameraStageParticipants = computed(() => meetingRenderCollections.value.cameraStageParticipants)
const callRailParticipants = computed(() => [
  ...meetingRenderCollections.value.cameraRailParticipants,
  ...meetingRenderCollections.value.presentationRailParticipants
])
const visibleCallStageParticipants = computed(() => callLayoutMode.value === 'CAMERA_GRID'
  ? getBoundedCallStageParticipants(participantsInCall.value)
  : cameraStageParticipants.value)
const callOverflowCount = computed(() => callLayoutMode.value === 'CAMERA_GRID'
  ? Math.max(participantsInCall.value.length - visibleCallStageParticipants.value.length, 0)
  : 0)
const describeMeetingParticipant = participant => {
  const isSelf = participant.connectionId === callConnectionId.value
  const remoteEntry = isSelf ? null : remoteStreams.value.get(participant.connectionId)
  const cameraStream = isSelf ? localCallStream.value : remoteEntry?.cameraStream
  const audioStream = isSelf ? localCallStream.value : remoteEntry?.audioStream
  const screenStream = isSelf ? localScreenStream.value : remoteEntry?.screenStream
  const displayName = `${participant.displayName || ''}`.trim().toLocaleLowerCase()
  const displayNameCollisionCount = participantsInCall.value.filter(item =>
    `${item.displayName || ''}`.trim().toLocaleLowerCase() === displayName
  ).length
  return {
    userKey: meetingLayoutCorrelation('user', participant.userId, meetingLayoutUserKeys),
    connectionKey: meetingLayoutCorrelation('connection', participant.connectionId, meetingLayoutConnectionKeys),
    isSelf,
    displayName: participant.displayName || '',
    displayNameCollisionCount,
    cameraEnabled: isSelf ? isCallCameraOn.value : participant.cameraEnabled === true,
    micEnabled: isSelf ? callMicrophoneEnabled.value : participant.microphoneEnabled !== false,
    remoteEntryExists: Boolean(remoteEntry),
    selectedRemoteConnectionKey: isSelf ? '' : meetingLayoutCorrelation('connection', participant.connectionId, meetingLayoutConnectionKeys),
    cameraStreamPresent: Boolean(cameraStream),
    cameraLiveTrackPresent: hasLiveVideoTrack(cameraStream),
    audioStreamPresent: Boolean(audioStream),
    audioLiveTrackPresent: audioStream?.getAudioTracks?.().some(track => track.readyState === 'live') === true,
    screenStreamPresent: Boolean(screenStream)
  }
}
const traceMeetingLayoutSnapshot = () => {
  if (!meetingLayoutTraceEnabled() || !meetingShell.value) return
  const participants = participantsInCall.value.map(describeMeetingParticipant)
  const renderedParticipants = [
    ...cameraStageParticipants.value,
    ...callRailParticipants.value
  ]
  const renderedElements = [...meetingShell.value.querySelectorAll('.call-camera-stage-tile, .call-participant-thumb')]
  const tiles = renderedParticipants.map((participant, index) => {
    const element = renderedElements[index]
    const rect = element?.getBoundingClientRect?.()
    const showingVideo = isParticipantVideoVisible(participant)
    return {
      userKey: meetingLayoutCorrelation('user', participant.userId, meetingLayoutUserKeys),
      connectionKey: meetingLayoutCorrelation('connection', participant.connectionId, meetingLayoutConnectionKeys),
      tileRole: participant.connectionId === callConnectionId.value ? 'SELF' : 'PARTICIPANT',
      displayName: participant.displayName || '',
      cameraEnabled: participant.connectionId === callConnectionId.value ? isCallCameraOn.value : participant.cameraEnabled === true,
      cameraLiveTrackPresent: participant.connectionId === callConnectionId.value
        ? hasLiveVideoTrack(localCallStream.value)
        : hasLiveVideoTrack(remoteStreams.value.get(participant.connectionId)?.cameraStream),
      showingVideo,
      showingAvatarFallback: !showingVideo,
      width: rect ? Math.round(rect.width) : 0,
      height: rect ? Math.round(rect.height) : 0
    }
  })
  const stage = meetingShell.value.querySelector('.call-camera-stage')
  const stageStyle = stage ? getComputedStyle(stage) : null
  const gridColumns = stageStyle?.gridTemplateColumns ? stageStyle.gridTemplateColumns.trim().split(/\s+/).filter(Boolean).length : 0
  const gridRows = stageStyle?.gridTemplateRows ? stageStyle.gridTemplateRows.trim().split(/\s+/).filter(Boolean).length : 0
  const uniqueRenderedUserCount = new Set(tiles.map(tile => tile.userKey).filter(Boolean)).size
  const signature = JSON.stringify({
    participants,
    tiles,
    logicalParticipantCount: participants.length,
    renderedTileCount: tiles.length,
    uniqueRenderedUserCount,
    gridColumns,
    gridRows,
    presentationActive: Boolean(activePresenter.value)
  })
  if (signature === meetingLayoutDiagnosticSignature) return
  meetingLayoutDiagnosticSignature = signature
  traceMeetingLayout('PARTICIPANT_SNAPSHOT', { participants })
  traceMeetingLayout('TILE_SNAPSHOT', { tiles })
  traceMeetingLayout('GRID_SNAPSHOT', {
    logicalParticipantCount: participants.length,
    renderedTileCount: tiles.length,
    uniqueRenderedUserCount,
    viewportWidth: Math.round(globalThis.innerWidth || 0),
    viewportHeight: Math.round(globalThis.innerHeight || 0),
    gridColumns,
    gridRows,
    presentationActive: Boolean(activePresenter.value),
    duplicateLogicalUserDetected: uniqueRenderedUserCount !== tiles.length
  })
  const ownership = [...remoteStreams.value.entries()]
    .filter(([, media]) => Boolean(media?.cameraStream))
    .map(([connectionId, media]) => {
      const selectedParticipant = participantsInCall.value.find(item => item.connectionId === connectionId)
      const ownerKey = selectedParticipant
        ? meetingLayoutCorrelation('user', selectedParticipant.userId, meetingLayoutUserKeys)
        : ''
      return {
        streamOwnerUserKey: ownerKey,
        streamConnectionKey: meetingLayoutCorrelation('connection', connectionId, meetingLayoutConnectionKeys),
        selectedParticipantUserKey: ownerKey,
        selectedParticipantConnectionKey: selectedParticipant
          ? meetingLayoutCorrelation('connection', selectedParticipant.connectionId, meetingLayoutConnectionKeys)
          : '',
        ownershipMatches: Boolean(selectedParticipant && selectedParticipant.userId),
        cameraLiveTrackPresent: hasLiveVideoTrack(media.cameraStream)
      }
    })
  traceMeetingLayout('STREAM_OWNERSHIP_SNAPSHOT', { ownership })
}
const scheduleMeetingLayoutDiagnostics = () => {
  if (!meetingLayoutTraceEnabled() || meetingLayoutDiagnosticQueued) return
  meetingLayoutDiagnosticQueued = true
  void nextTick().then(() => {
    meetingLayoutDiagnosticQueued = false
    traceMeetingLayoutSnapshot()
  })
}
const callLayoutClasses = computed(() => ({
  'is-presentation-mode': callLayoutMode.value.startsWith('PRESENTATION'),
  'is-camera-mode': callLayoutMode.value.startsWith('CAMERA'),
  'is-focus-mode': callLayoutMode.value.endsWith('FOCUS'),
  'has-call-side-panel': callChatOpen.value || showMembersSidebar.value
}))
const focusParticipant = connectionId => {
  const participant = participantsInCall.value.find(user => user.connectionId === connectionId)
  if (!participant) return
  focusedParticipantConnectionId.value = focusedParticipantConnectionId.value === connectionId ? '' : connectionId
}

const bindMediaElement = (element, stream, muted = false, { peerId = '', mediaRole = '' } = {}) => {
  if (!element) return
  const track = stream?.getVideoTracks?.()[0] || stream?.getAudioTracks?.()[0] || null
  traceWebRtcMedia('VIDEO_ELEMENT_FOUND', {
    peerId,
    trackKind: track?.kind,
    trackId: track?.id,
    trackReadyState: track?.readyState,
    mediaRole,
    streamId: stream?.id || ''
  })
  element.muted = muted
  if (mediaRole === 'audio') element.volume = 1
  element.autoplay = true
  element.playsInline = true
  if (element.srcObject !== stream) {
    element.srcObject = stream || null
    traceWebRtcMedia('REMOTE_MEDIA_ELEMENT_BOUND', {
      peerId,
      trackKind: track?.kind,
      trackReadyState: track?.readyState,
      trackEnabled: track ? track.enabled !== false : null,
      mediaRole,
      streamTrackCount: stream?.getTracks?.().length || 0,
      result: stream ? 'srcObject-set' : 'srcObject-cleared'
    })
    traceWebRtcMedia('VIDEO_SRC_OBJECT_SET', {
      peerId,
      trackKind: track?.kind,
      trackId: track?.id,
      trackReadyState: track?.readyState,
      mediaRole,
      streamId: stream?.id || ''
    })
  }
  if (stream) {
    if (mediaRole === 'audio') traceWebRtcMedia('REMOTE_AUDIO_PLAY_BEGIN', {
      peerId,
      trackKind: track?.kind,
      trackReadyState: track?.readyState,
      mediaRole,
      streamTrackCount: stream.getTracks?.().length || 0
    })
    const playback = element.play?.()
    if (playback?.then) {
      void playback.then(() => {
        blockedMediaElements.delete(element)
        if (mediaRole === 'audio') traceWebRtcMedia('REMOTE_AUDIO_PLAY_OK', { peerId, mediaRole, result: 'play-resolved' })
        traceWebRtcMedia('VIDEO_PLAY_OK', { peerId, trackKind: track?.kind, trackId: track?.id, trackReadyState: track?.readyState, mediaRole, streamId: stream.id })
      }).catch(error => {
        if (error?.name === 'NotAllowedError') blockedMediaElements.add(element)
        if (mediaRole === 'audio') traceWebRtcMedia('REMOTE_AUDIO_PLAY_FAIL', { peerId, mediaRole, errorName: error?.name || 'Error' })
        traceWebRtcMedia('VIDEO_PLAY_FAILED', { peerId, trackKind: track?.kind, trackId: track?.id, trackReadyState: track?.readyState, mediaRole, streamId: stream.id })
      })
    }
  }
}

const resumeBlockedCallMedia = () => {
  for (const element of [...blockedMediaElements]) {
    const playback = element?.play?.()
    if (!playback?.then) {
      blockedMediaElements.delete(element)
      continue
    }
    void playback.then(() => blockedMediaElements.delete(element)).catch(error => {
      if (error?.name !== 'NotAllowedError') blockedMediaElements.delete(element)
    })
  }
}

const syncCallVideoElements = () => {
  for (const element of localVideoElements.values()) bindMediaElement(element, localCallStream.value, true, { peerId: callConnectionId.value, mediaRole: 'camera' })
  for (const { connectionId, element } of remoteVideoElements.values()) {
    bindMediaElement(element, remoteStreams.value.get(connectionId)?.cameraStream, false, { peerId: connectionId, mediaRole: 'camera' })
  }
  for (const { connectionId, element } of remoteAudioElements.values()) {
    bindMediaElement(element, remoteStreams.value.get(connectionId)?.audioStream, false, { peerId: connectionId, mediaRole: 'audio' })
  }
  bindMediaElement(presentationVideoElement.value, activePresenterStream(), true, { peerId: activePresenter.value?.connectionId || '', mediaRole: 'screen' })
}

const setLocalVideoElement = (element, slot = 'rail') => {
  if (element) localVideoElements.set(slot, element)
  else localVideoElements.delete(slot)
  bindMediaElement(element, localCallStream.value, true, { peerId: callConnectionId.value, mediaRole: 'camera' })
}

const setRemoteVideoElement = (element, connectionId, slot = 'rail') => {
  const key = `${slot}:${connectionId}`
  if (element) remoteVideoElements.set(key, { connectionId, element })
  else remoteVideoElements.delete(key)
  bindMediaElement(element, remoteStreams.value.get(connectionId)?.cameraStream, false, { peerId: connectionId, mediaRole: 'camera' })
}

const setRemoteAudioElement = (element, connectionId, slot = 'rail') => {
  const key = `${slot}:${connectionId}`
  if (element) remoteAudioElements.set(key, { connectionId, element })
  else remoteAudioElements.delete(key)
  bindMediaElement(element, remoteStreams.value.get(connectionId)?.audioStream, false, { peerId: connectionId, mediaRole: 'audio' })
}

const setPresentationVideoElement = (element, connectionId = '') => {
  presentationVideoElement.value = element
  bindMediaElement(element, activePresenterStream(), true, { peerId: connectionId, mediaRole: 'screen' })
}

const callAiStateLabel = computed(() => ({
  OFF: 'Đang tắt',
  WAITING_FOR_CONSENT: 'Chờ quyền',
  ACTIVE: 'Đang ghi',
  PAUSED_CONSENT: 'Đã tạm dừng',
  STOPPING: 'Đang dừng',
  ERROR: 'Có lỗi'
}[callAiState.value.state] || 'Đang tắt'))
const callAiButtonLabel = computed(() => callTranscriptionCapabilities.value.configured
  ? `${showTranscriptPanel.value ? 'Đóng' : 'Mở'} biên bản cuộc gọi`
  : 'Biên bản và AI chưa sẵn sàng vì phiên âm chưa được cấu hình')
const callCaptionLanguageLabel = computed(() => callCaptionLanguage.value === 'en' ? 'English' : 'Tiếng Việt')
const callViewModes = [
  { value: 'auto', label: 'Tự động' },
  { value: 'tiled', label: 'Dạng lưới' },
  { value: 'spotlight', label: 'Tiêu điểm' },
  { value: 'sidebar', label: 'Thanh bên' }
]
const callViewModeLabel = computed(() => callViewModes.find(mode => mode.value === callViewMode.value)?.label || 'Tự động')
const setCallViewMode = mode => {
  if (!callViewModes.some(item => item.value === mode)) return
  callViewMode.value = mode
  presentationFocused.value = mode === 'spotlight' && Boolean(activePresenter.value)
  if (mode === 'auto' || mode === 'tiled') focusedParticipantConnectionId.value = ''
  showMoreMenu.value = false
  moreMenuSection.value = ''
}
const normalizeCallAiState = value => {
  const state = value || {}
  return {
    state: state.state ?? state.State ?? 'OFF',
    callSessionId: state.callSessionId ?? state.CallSessionId ?? '',
    consentGeneration: state.consentGeneration ?? state.ConsentGeneration ?? 0,
    participants: (state.participants ?? state.Participants ?? []).map(item => ({
      userId: item.userId ?? item.UserId,
      displayName: item.displayName ?? item.DisplayName ?? 'SprintA user',
      consentStatus: item.consentStatus ?? item.ConsentStatus ?? 'PENDING',
      respondedAt: item.respondedAt ?? item.RespondedAt ?? null
    }))
  }
}

const handleCallAiState = value => {
  const nextState = normalizeCallAiState(value)
  if (callAiState.value.callSessionId && callAiState.value.callSessionId !== nextState.callSessionId) {
    clearLiveCaptionRows()
  }
  callAiState.value = nextState
  if (nextState.state === 'ACTIVE') {
    captionsEnabled.value = true
    showCaptionConsentModal.value = false
  } else if (nextState.state === 'OFF') {
    captionsEnabled.value = false
    showCaptionConsentModal.value = false
    clearLiveCaptionRows()
  } else if (nextState.state === 'WAITING_FOR_CONSENT' || nextState.state === 'PAUSED_CONSENT') {
    if (captionsEnabled.value) showCaptionConsentModal.value = true
  }
}

const normalizeMeetingAiReport = value => {
  if (!value) return null
  return {
    status: value.status ?? value.Status ?? 'PROCESSING',
    processedTranscriptChunkCount: value.processedTranscriptChunkCount ?? value.ProcessedTranscriptChunkCount ?? 0,
    state: value.state ?? value.State ?? null,
    evidence: value.evidence ?? value.Evidence ?? [],
    autoCreatesTasks: value.autoCreatesTasks ?? value.AutoCreatesTasks ?? false
  }
}

const formatAiEvidence = evidenceChunkIds => {
  const requestedIds = new Set((evidenceChunkIds || []).map(value => `${value}`.toLowerCase()))
  const evidence = (callMeetingAiReport.value?.evidence || []).find(item => requestedIds.has(`${item.transcriptChunkId ?? item.TranscriptChunkId}`.toLowerCase()))
  if (!evidence) return ''
  const speaker = evidence.speakerDisplayName ?? evidence.SpeakerDisplayName ?? 'Người tham gia'
  const timestamp = evidence.timestamp ?? evidence.Timestamp
  const excerpt = evidence.excerpt ?? evidence.Excerpt ?? ''
  return `${speaker} · ${formatTime(timestamp)} · “${excerpt}”`
}

const loadMeetingAiReport = async voiceChannel => {
  const sessionId = callAiState.value.callSessionId
  if (!activeProjectId.value || !sessionId || !voiceChannel || !callTranscriptionCapabilities.value.aiConfigured) return
  try {
    const report = await collaborationApi.getMeetingAiReport(
      activeProjectId.value,
      `${voiceChannel.name}`.trim().toLocaleLowerCase(),
      sessionId)
    callMeetingAiReport.value = normalizeMeetingAiReport(report)
  } catch (error) {
    if (error?.response?.status !== 404 && error?.response?.status !== 403) console.warn('Unable to load meeting AI report', error)
  }
}

const scheduleMeetingAiReportRefresh = () => {
  if (!callTranscriptionCapabilities.value.aiConfigured || !activeVoiceChannel.value) return
  const chunkSize = Math.max(1, Number(callTranscriptionCapabilities.value.aiTranscriptChunkSize) || 8)
  if (callTranscriptChunks.value.length % chunkSize !== 0) return
  window.clearTimeout(callMeetingAiRefreshTimer)
  callMeetingAiRefreshTimer = window.setTimeout(() => void loadMeetingAiReport(activeVoiceChannel.value), 1200)
}

const currentCallSessionId = () => callSession.value?.getCallSessionId?.() || callAiState.value.callSessionId || ''

const clearLiveCaptionRows = () => {
  liveCaptionRows.value = clearLiveCaptions()
  if (liveCaptionExpirySweep) {
    window.clearTimeout(liveCaptionExpirySweep)
    liveCaptionExpirySweep = null
  }
}

const scheduleLiveCaptionExpiry = () => {
  if (liveCaptionExpirySweep) window.clearTimeout(liveCaptionExpirySweep)
  const nextExpiry = liveCaptionRows.value
    .filter(row => !row.isInterim && row.expiresAt)
    .reduce((soonest, row) => Math.min(soonest, row.expiresAt), Number.POSITIVE_INFINITY)
  if (!Number.isFinite(nextExpiry)) {
    liveCaptionExpirySweep = null
    return
  }
  liveCaptionExpirySweep = window.setTimeout(() => {
    liveCaptionRows.value = removeExpiredLiveCaptions(liveCaptionRows.value)
    scheduleLiveCaptionExpiry()
  }, Math.max(0, nextExpiry - Date.now()))
}

const normalizeCaptionForDisplay = value => {
  const caption = normalizeLiveCaptionEvent(value)
  const speaker = participantsInCall.value.find(participant =>
    `${participant.userId}` === `${caption.speakerUserId}`)
  const isCurrentUser = `${currentUser.value?.id || ''}` === `${caption.speakerUserId}`
  return {
    ...caption,
    speakerDisplayName: speaker?.displayName || (isCurrentUser ? currentUser.value?.name : '') || caption.speakerDisplayName,
    avatarUrl: speaker?.avatarUrl || (isCurrentUser ? currentUser.value?.avatar : '') || ''
  }
}

const isCurrentCaptionEvent = value => {
  return captionsEnabled.value && isCaptionSessionCurrent(value)
}

const isCaptionSessionCurrent = value => {
  return isLiveCaptionForSession(value, currentCallSessionId())
}

const updateLiveCaptionRows = (value, update) => {
  if (!isCurrentCaptionEvent(value)) return
  const caption = normalizeCaptionForDisplay(value)
  liveCaptionRows.value = update(liveCaptionRows.value, caption)
  scheduleLiveCaptionExpiry()
}

const handleTranscriptChunk = (value, { showLive = true } = {}) => {
  const receivedAt = performance.now()
  const chunk = normalizeTranscriptChunkEvent(value)
  if (!chunk.id || !chunk.text) return
  if (showLive && !isCaptionSessionCurrent(chunk)) return
  if (showLive) updateLiveCaptionRows(chunk, upsertLiveCaptionFinal)
  callTranscriptInterims.value = removeTranscriptInterim(callTranscriptInterims.value, chunk)
  callTranscriptChunks.value = upsertTranscriptHistory(callTranscriptChunks.value, chunk)
  scheduleMeetingAiReportRefresh()
  void nextTick().then(() => traceCaptionRender('final', receivedAt))
}

const handleTranscriptInterim = value => {
  const receivedAt = performance.now()
  if (!isCurrentCaptionEvent(value)) return
  callTranscriptInterims.value = upsertTranscriptInterim(
    callTranscriptInterims.value,
    normalizeCaptionForDisplay(value))
  updateLiveCaptionRows(value, upsertLiveCaptionInterim)
  void nextTick().then(() => traceCaptionRender('interim', receivedAt))
}

const handleTranscriptionError = value => {
  const message = value?.message ?? value?.Message ?? 'Không thể ghi biên bản cuộc gọi lúc này.'
  callError.value = message
  ElMessage.warning(message)
}

const loadCallTranscript = async voiceChannel => {
  const sessionId = callAiState.value.callSessionId
  if (!activeProjectId.value || !sessionId) return
  try {
    const items = await collaborationApi.getCallTranscript(
      activeProjectId.value,
      `${voiceChannel.name}`.trim().toLocaleLowerCase(),
      sessionId)
    callTranscriptChunks.value = []
    const chunks = Array.isArray(items) ? items : []
    chunks.forEach(chunk => handleTranscriptChunk(chunk, { showLive: false }))
    await loadMeetingAiReport(voiceChannel)
  } catch (error) {
    if (error?.response?.status !== 404 && error?.response?.status !== 403) console.warn('Unable to load call transcript', error)
  }
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
  SCREEN_SHARE_BUSY: 'Một người khác đang chia sẻ màn hình. Hãy đợi họ dừng chia sẻ.',
  CALL_NOT_CONNECTED: 'Cuộc gọi đang kết nối lại. Vui lòng thử lại sau giây lát.',
  NOT_IN_CALL_ROOM: 'Bạn không còn ở trong phòng thoại. Đang kết nối lại…',
  INVALID_CALL_MESSAGE: 'Tin nhắn cuộc gọi không hợp lệ.'
}[error?.code] || (error?.message?.includes('CALL_ROOM_FULL') ? 'Phòng thoại đã đủ 6 người.' : null) || error?.message || 'Không thể kết nối cuộc gọi.')

const handleCallError = (error, showMessage = true) => {
  callError.value = describeCallError(error)
  if (showMessage && !error?.silent) ElMessage.error(callError.value)
}

const normalizeCallChatMessage = value => ({
  messageId: value?.messageId ?? value?.MessageId ?? null,
  callSessionId: value?.callSessionId ?? value?.CallSessionId ?? '',
  roomId: value?.roomId ?? value?.RoomId ?? '',
  senderId: value?.senderUserId ?? value?.SenderUserId ?? value?.senderId ?? value?.SenderId ?? '',
  senderName: value?.senderName ?? value?.SenderName ?? 'SprintA user',
  senderAvatar: value?.senderAvatar ?? value?.SenderAvatar ?? null,
  content: value?.content ?? value?.Content ?? '',
  attachments: value?.attachments ?? value?.Attachments ?? [],
  replyTo: value?.replyTo ?? value?.ReplyTo ?? null,
  isRevoked: value?.isRevoked ?? value?.IsRevoked ?? false,
  isEdited: value?.isEdited ?? value?.IsEdited ?? false,
  reactions: value?.reactions ?? value?.Reactions ?? [],
  sentAt: value?.createdAt ?? value?.CreatedAt ?? value?.sentAt ?? value?.SentAt ?? new Date().toISOString(),
  clientMessageId: value?.clientMessageId ?? value?.ClientMessageId ?? null,
  status: value?.status ?? 'sent'
})

const getVoiceChatStorageKey = () => {
  const channelId = activeVoiceChannel.value?.id || activeVoiceChannel.value?.name || 'common'
  return `sprinta_voice_chat_${channelId}`
}

const saveCallChatToStorage = () => {
  try {
    const key = getVoiceChatStorageKey()
    const data = (callChatMessages.value || []).slice(-100)
    localStorage.setItem(key, JSON.stringify(data))
  } catch (e) {
    console.warn('Could not save voice chat to localStorage:', e)
  }
}

const loadCallChatFromStorage = () => {
  try {
    const key = getVoiceChatStorageKey()
    const raw = localStorage.getItem(key)
    if (raw) {
      const parsed = JSON.parse(raw)
      if (Array.isArray(parsed)) {
        const deletedForMeSet = getDeletedForMeStore()
        callChatMessages.value = parsed
          .map(normalizeCallChatMessage)
          .filter(msg => msg?.messageId && !deletedForMeSet.has(msg.messageId))
      }
    }
  } catch (e) {
    console.warn('Could not load voice chat from localStorage:', e)
  }
}

const handleCallChatHistory = items => {
  const deletedForMeSet = getDeletedForMeStore()
  const incoming = (Array.isArray(items) ? items : [])
    .map(normalizeCallChatMessage)
    .filter(msg => msg?.messageId && !deletedForMeSet.has(msg.messageId))
  if (incoming.length > 0) {
    callChatMessages.value = incoming
  } else if (!callChatMessages.value.length) {
    loadCallChatFromStorage()
  }
  saveCallChatToStorage()
  void nextTick().then(() => {
    if (callChatThread.value) callChatThread.value.scrollTop = callChatThread.value.scrollHeight
  })
}

const handleCallChatMessage = value => {
  const message = normalizeCallChatMessage(value)
  const deletedForMeSet = getDeletedForMeStore()
  if (message.messageId && deletedForMeSet.has(message.messageId)) return

  const clientMessageId = message.clientMessageId
  const existingIndex = callChatMessages.value.findIndex(item =>
    (message.messageId && item.messageId === message.messageId) ||
    (clientMessageId && item.clientMessageId === clientMessageId))
  if (existingIndex >= 0) {
    const next = [...callChatMessages.value]
    next[existingIndex] = { ...next[existingIndex], ...message, status: 'sent' }
    callChatMessages.value = next
  } else {
    callChatMessages.value = [...callChatMessages.value, message]
  }
  saveCallChatToStorage()
  void nextTick().then(() => {
    if (callChatThread.value) callChatThread.value.scrollTop = callChatThread.value.scrollHeight
  })
}

const markMessageAsDeletedForMeLocal = (messageId) => {
  if (!messageId) return
  const deletedSet = getDeletedForMeStore()
  deletedSet.add(messageId)
  localStorage.setItem('chat_deleted_for_me_message_ids', JSON.stringify(Array.from(deletedSet)))
}



const callChatDisplayMessages = computed(() => {
  const deletedForMeSet = getDeletedForMeStore()
  return (callChatMessages.value || [])
    .filter(msg => msg?.messageId && !deletedForMeSet.has(msg.messageId))
    .slice(-60)
})

const isOwnCallMessage = msg => {
  if (!msg) return false
  const senderId = `${msg.senderId || msg.senderUserId || ''}`
  return senderId === `${currentUser.value.id}` || isOwnMessage(msg)
}

const insertCallEmoji = emoji => {
  callChatDraft.value += emoji
}



const syncLocalCallPreview = async () => {
  localCallStream.value = callSession.value?.getLocalStream?.() || null
  localScreenStream.value = callSession.value?.getLocalScreenStream?.() || null
  callConnectionId.value = callSession.value?.getConnectionId?.() || ''
  await nextTick()
  syncCallVideoElements()
}

const createCallSessionForVoiceChannel = (voiceChannel, options = {}) => createCallMediaSession({
  projectId: activeProjectId.value,
  voiceChannelId: `${voiceChannel.name}`.trim().toLocaleLowerCase(),
  initialMicrophoneEnabled: options.initialMicrophoneEnabled ?? true,
  initialMicrophoneStream: options.initialMicrophoneStream || null,
  initialCameraEnabled: options.initialCameraEnabled === true,
  initialCameraStream: options.initialCameraStream || null,
  onState: async ({ state, error }) => {
    callState.value = state
    if (state === 'reconnecting') {
      callError.value = 'Đang kết nối lại cuộc gọi…'
    } else if (state === 'connected') {
      callError.value = ''
    } else if (state === 'disconnected' && error) {
      callError.value = 'Cuộc gọi đã mất kết nối. Vui lòng tham gia lại.'
    } else if (error) {
      handleCallError(error, state === 'error')
    }
    if (state === 'disconnected') clearLiveCaptionRows()
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
  onParticipants: async (items) => {
    callParticipants.value = dedupeParticipantsByUser(items, callConnectionId.value)
    await nextTick()
    syncCallVideoElements()
  },
  onRemoteStreams: async (items) => {
    remoteStreams.value = items
    voiceCallStore.syncRemoteAudioStreams(items)
    await nextTick()
    syncCallVideoElements()
  },
  onHandChanged: ({ connectionId, handRaised }) => {
    if (connectionId === callConnectionId.value) callHandRaised.value = handRaised
  },
  onReaction: reaction => {
    callReactions.value = [...callReactions.value.filter(item => item.id !== reaction.id), { ...reaction, expiresAt: Date.now() + 4000 }]
    window.setTimeout(() => { callReactions.value = callReactions.value.filter(item => item.id !== reaction.id) }, 4100)
  },
  onForceMute: async () => {
    if (callSession.value && callMicrophoneEnabled.value) { await callSession.value.setMicrophoneEnabled(false); callMicrophoneEnabled.value = false; callLiveNotice.value = 'Bạn đã được tắt microphone bởi host.' }
  },
  onForceRemoved: async () => { callLiveNotice.value = 'Bạn đã được mời khỏi cuộc gọi.'; await leaveVoiceChannel(false) },
  onCallMessage: handleCallChatMessage,
  onCallHistory: handleCallChatHistory,
  onAiState: handleCallAiState,
  onTranscriptChunk: handleTranscriptChunk,
  onTranscriptInterim: handleTranscriptInterim,
  onTranscriptionError: handleTranscriptionError,
  onTranscriptionCapabilities: capabilities => {
    callTranscriptionCapabilities.value = capabilities
    callCaptionLanguage.value = capabilities.supportedLanguages.includes(capabilities.defaultLanguage)
      ? capabilities.defaultLanguage
      : capabilities.supportedLanguages[0] || 'vi'
  }
})

const toggleRaiseHand = async () => {
  if (!callSession.value) return
  const nextValue = !callHandRaised.value
  try { await callSession.value.setRaiseHand(nextValue); callHandRaised.value = nextValue } catch (error) { handleCallError(error) }
}
const sendCallReaction = async emoji => { try { await callSession.value?.sendReaction(emoji); showMoreMenu.value = false; moreMenuSection.value = '' } catch (error) { handleCallError(error) } }
const loadCallDevices = async () => {
  callDevices.value = await callSession.value?.enumerateDevices?.() || []
  const microphones = audioInputDevices.value
  const cameras = videoInputDevices.value
  if (!selectedCallMicrophoneId.value && microphones[0]) selectedCallMicrophoneId.value = microphones[0].deviceId
  if (!selectedCallCameraId.value && cameras[0]) selectedCallCameraId.value = cameras[0].deviceId
  const mediaElements = [
    ...[...remoteAudioElements.values()].map(item => item.element),
    ...[...remoteVideoElements.values()].map(item => item.element),
    presentationVideoElement.value
  ].filter(Boolean)
  speakerSelectionSupported.value = mediaElements.some(element => typeof element.setSinkId === 'function')
  if (!selectedCallSpeakerId.value && audioOutputDevices.value[0]) selectedCallSpeakerId.value = audioOutputDevices.value[0].deviceId
}
const openCallDevicesMenu = async () => {
  await loadCallDevices()
  moreMenuSection.value = 'devices'
}
const switchCallDevice = async (kind, deviceId) => {
  try {
    if (kind === 'audioinput') await callSession.value?.setMicrophoneDevice(deviceId)
    if (kind === 'videoinput') await callSession.value?.setCameraDevice(deviceId)
    await loadCallDevices()
  } catch (error) { handleCallError(error) }
}
const switchCallSpeaker = async () => {
  const elements = [
    ...[...remoteAudioElements.values()].map(item => item.element),
    ...[...remoteVideoElements.values()].map(item => item.element),
    presentationVideoElement.value
  ].filter(Boolean)
  const supported = elements.filter(element => typeof element.setSinkId === 'function')
  if (!supported.length) {
    speakerSelectionSupported.value = false
    return
  }
  try {
    await Promise.all(supported.map(element => element.setSinkId(selectedCallSpeakerId.value || '')))
    speakerSelectionSupported.value = true
  } catch (error) {
    handleCallError(error)
  }
}

const refreshPreJoinDevices = async () => {
  if (!navigator.mediaDevices?.enumerateDevices) return
  const devices = await navigator.mediaDevices.enumerateDevices()
  callDevices.value = devices
  if (!preJoinMicrophoneId.value) preJoinMicrophoneId.value = devices.find(device => device.kind === 'audioinput')?.deviceId || ''
  if (!preJoinCameraId.value) preJoinCameraId.value = devices.find(device => device.kind === 'videoinput')?.deviceId || ''
}

const syncPreJoinPreview = async () => {
  await nextTick()
  bindMediaElement(preJoinVideo.value, preJoinPreviewStream.value, true)
}

const stopPreJoinPreview = () => {
  preJoinPreviewStream.value?.getTracks?.().forEach(track => track.stop())
  preJoinPreviewStream.value = null
}

const togglePreJoinCamera = async () => {
  if (preJoinCameraEnabled.value) {
    preJoinCameraEnabled.value = false
    stopPreJoinPreview()
    return
  }
  if (!navigator.mediaDevices?.getUserMedia) {
    handleCallError({ code: 'UNSUPPORTED_BROWSER' })
    return
  }
  try {
    const stream = await navigator.mediaDevices.getUserMedia({
      video: preJoinCameraId.value ? { deviceId: { exact: preJoinCameraId.value } } : true,
      audio: false
    })
    stopPreJoinPreview()
    preJoinPreviewStream.value = stream
    preJoinCameraEnabled.value = true
    await syncPreJoinPreview()
    await refreshPreJoinDevices()
  } catch (error) {
    handleCallError(error)
  }
}

const switchPreJoinCamera = async () => {
  if (!preJoinCameraEnabled.value) return
  preJoinCameraEnabled.value = false
  await togglePreJoinCamera()
}

const openPreJoinVoiceChannel = async voiceChannel => {
  if (activeVoiceChannel.value?.id === voiceChannel.id) {
    showVoiceCallMain.value = true
    return
  }
  if (callSession.value) await leaveVoiceChannel(false)
  stopPreJoinPreview()
  preJoinVoiceChannel.value = voiceChannel
  preJoinMicEnabled.value = true
  preJoinCameraEnabled.value = false
  preJoinMicrophoneId.value = ''
  preJoinCameraId.value = ''
  await loadMeetingCapabilities(voiceChannel)
  await refreshPreJoinDevices()
  if (!projectMembers.value.length && activeProjectId.value) {
    void fetchProjectMembers()
  }
}

const loadMeetingCapabilities = async voiceChannel => {
  if (!activeProjectId.value || !voiceChannel?.id) return
  try {
    const value = await collaborationApi.getMeetingCapabilities(activeProjectId.value, voiceChannel.id)
    const supportedLanguages = Array.isArray(value?.supportedLanguages)
      ? value.supportedLanguages.map(language => `${language}`.toLowerCase()).filter(language => ['vi', 'en'].includes(language))
      : []
    callTranscriptionCapabilities.value = {
      ...callTranscriptionCapabilities.value,
      configured: value?.transcriptionEnabled === true,
      provider: value?.transcriptionProvider || 'Unavailable',
      supportedLanguages,
      defaultLanguage: supportedLanguages.includes('vi') ? 'vi' : (supportedLanguages[0] || 'vi'),
      aiConfigured: value?.meetingAiConfigured === true,
      aiProvider: value?.meetingAiConfigured === true ? 'ZenMux' : 'Unavailable'
    }
    if (callTranscriptionCapabilities.value.configured && supportedLanguages.length) {
      callCaptionLanguage.value = callTranscriptionCapabilities.value.defaultLanguage
    }
  } catch (error) {
    callTranscriptionCapabilities.value = {
      ...callTranscriptionCapabilities.value,
      configured: false,
      provider: 'Unavailable',
      supportedLanguages: [],
      aiConfigured: false,
      aiProvider: 'Unavailable'
    }
    if (error?.response?.status !== 403) console.warn('Unable to load meeting capabilities', error)
  }
}

const cancelPreJoin = () => {
  stopPreJoinPreview()
  preJoinVoiceChannel.value = null
}

const cancelVoiceJoining = async () => {
  voiceJoinPending.value = false
  voiceJoiningChannelName.value = ''
  if (callSession.value) {
    await callSession.value.leave().catch(() => {})
    callSession.value = null
  }
  leaveVoiceChannel(false)
  ElMessage.info('Đã hủy kết nối vào kênh thoại')
}
const toggleCallPictureInPicture = async () => {
  const closeMoreMenu = () => {
    showMoreMenu.value = false
    moreMenuSection.value = ''
  }
  const showPictureInPictureMessage = message => {
    callLiveNotice.value = message
    ElMessage.warning(message)
    closeMoreMenu()
  }
  if (!documentPictureInPictureSupported() && !standardPictureInPictureSupported()) {
    showPictureInPictureMessage(pictureInPictureUnsupportedMessage)
    return
  }
  if (documentPictureInPictureSupported()) {
    if (meetingPictureInPicture.isOpen()) {
      meetingPictureInPicture.close()
      closeMoreMenu()
      return
    }
    if (!participantsInCall.value.length) {
      showPictureInPictureMessage(pictureInPictureNoVideoMessage)
      return
    }
    try {
      await meetingPictureInPicture.open(getMeetingPictureInPictureSnapshot())
    } catch (error) {
      handleCallError(error)
    } finally {
      closeMoreMenu()
    }
    return
  }
  await nextTick()
  syncCallVideoElements()
  const candidates = [
    presentationVideoElement.value,
    ...localVideoElements.values(),
    ...[...remoteVideoElements.values()].map(({ element }) => element)
  ]
  const element = candidates.find(candidate =>
    candidate?.requestPictureInPicture && hasLiveVideoTrack(candidate.srcObject))
  if (!element) {
    showPictureInPictureMessage(pictureInPictureNoVideoMessage)
    return
  }
  try {
    if (document.pictureInPictureElement) await document.exitPictureInPicture()
    else await element.requestPictureInPicture()
  } catch (error) {
    handleCallError(error)
  } finally {
    closeMoreMenu()
  }
}
const handleCallShortcut = event => {
  if (event.key === 'Escape' && workspaceState.value === 'VOICE_PRE_JOIN') {
    event.preventDefault()
    cancelPreJoin()
    return
  }
  if (!showVoiceCallMain.value || event.target?.matches?.('input, textarea, [contenteditable="true"]')) return
  if (!(event.ctrlKey || event.metaKey)) return
  if (event.key.toLowerCase() === 'd') { event.preventDefault(); void toggleCallMicrophone() }
  if (event.key.toLowerCase() === 'e') { event.preventDefault(); void toggleCallCameraReal() }
}

const requestCallAi = async () => {
  if (!callSession.value || !callTranscriptionCapabilities.value.configured) return
  if (callAiState.value.state !== 'OFF' && callAiState.value.state !== 'ERROR') return
  try {
    await callSession.value.requestAiTranscription()
  } catch (error) {
    handleCallError(error)
    showCaptionConsentModal.value = false
    captionsEnabled.value = false
  }
}

const openMeetingAi = async () => {
  showTranscriptPanel.value = true
  showMoreMenu.value = false
  moreMenuSection.value = ''
  if (callAiState.value.state === 'OFF' || callAiState.value.state === 'ERROR') await requestCallAi()
}

const toggleTranscriptPanel = () => {
  showTranscriptPanel.value = !showTranscriptPanel.value
  showMoreMenu.value = false
  moreMenuSection.value = ''
}

const setCallCaptionLanguage = () => {
  try {
    callSession.value?.setTranscriptionLanguage?.(callCaptionLanguage.value)
  } catch (error) {
    handleCallError(error)
  }
}

const toggleCallCaptions = async () => {
  if (!callSession.value || !callTranscriptionCapabilities.value.configured) return
  showMoreMenu.value = false
  moreMenuSection.value = ''
  if (captionsEnabled.value) {
    captionsEnabled.value = false
    clearLiveCaptionRows()
    callTranscriptInterims.value = []
    await stopCallAi()
    return
  }
  if (callAiState.value.state === 'ACTIVE') {
    captionsEnabled.value = true
    return
  }
  captionsEnabled.value = true
  setCallCaptionLanguage()
  showCaptionConsentModal.value = true
  await requestCallAi()
}

const respondCallAiConsent = async accepted => {
  if (!callSession.value) return
  if (accepted) captionConsentSubmitting.value = true
  try {
    await callSession.value.respondToAiConsent(accepted, callAiState.value)
    if (accepted) {
      captionsEnabled.value = true
      showCaptionConsentModal.value = false
    } else {
      captionsEnabled.value = false
      clearLiveCaptionRows()
      callTranscriptInterims.value = []
      showCaptionConsentModal.value = false
    }
  } catch (error) {
    handleCallError(error)
    if (accepted) captionsEnabled.value = false
  } finally {
    captionConsentSubmitting.value = false
  }
}

const cancelCaptionConsent = () => {
  if (captionConsentSubmitting.value) return
  showCaptionConsentModal.value = false
  if (callAiState.value.state === 'WAITING_FOR_CONSENT' || callAiState.value.state === 'PAUSED_CONSENT') {
    void respondCallAiConsent(false)
  } else {
    captionsEnabled.value = false
    clearLiveCaptionRows()
  }
}

const stopCallAi = async () => {
  if (!callSession.value) return
  try {
    await callSession.value.stopAiTranscription()
  } catch (error) {
    handleCallError(error)
  }
}

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
    voiceCallStore.updateCallStatus({ isCameraEnabled: nextValue })
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

const joinVoiceChannel = async (vc, options = {}) => {
  if (callJoinPromise) return callJoinPromise
  if (activeVoiceChannel.value?.id === vc.id) {
    showVoiceCallMain.value = true
    return
  }
  voiceJoinPending.value = true
  voiceJoiningChannelName.value = vc.name || 'Kênh thoại'
  callJoinPromise = (async () => {
    if (callSession.value) await leaveVoiceChannel(false)

    const session = createCallSessionForVoiceChannel(vc, options)
    callSession.value = session
    callError.value = ''
    try {
      if (options.microphoneDeviceId) await session.setMicrophoneDevice(options.microphoneDeviceId)
      if (options.cameraDeviceId) await session.setCameraDevice(options.cameraDeviceId)
      await session.start()
      await loadCallDevices()
      activeVoiceChannel.value = vc
      showVoiceCallMain.value = true
      callChatOpen.value = true
      showMembersSidebar.value = false
      loadCallChatFromStorage()
      voiceCallStore.setActiveCall({
        channel: vc,
        session,
        participantsCount: participantsInCall.value.length || 1,
        isMicEnabled: callMicrophoneEnabled.value,
        isCameraEnabled: isCallCameraOn.value,
        leaveHandler: () => leaveVoiceChannel(true),
        toggleMicHandler: () => toggleCallMicrophone(),
        toggleCamHandler: () => toggleCallCameraReal()
      })
      await loadCallTranscript(vc)
      await syncLocalCallPreview()
      ElMessage.success(`Đã kết nối vào kênh thoại: ${vc.name}`)
    } catch (error) {
      handleCallError(error)
      await session.leave().catch(() => {})
      callSession.value = null
      callParticipants.value = []
      remoteStreams.value = new Map()
      callConnectionId.value = ''
      voiceCallStore.clearCall()
    }
  })().finally(() => {
    callJoinPromise = null
    voiceJoinPending.value = false
    voiceJoiningChannelName.value = ''
  })
  return callJoinPromise
}

watch([participantsInCall, callMicrophoneEnabled, isCallCameraOn, isSharingScreen], () => {
  if (activeVoiceChannel.value) {
    voiceCallStore.updateCallStatus({
      participantsCount: participantsInCall.value.length,
      isMicEnabled: callMicrophoneEnabled.value,
      isCameraEnabled: isCallCameraOn.value,
      isScreenSharing: isSharingScreen.value
    })
  }
})

const confirmJoinVoiceChannel = async () => {
  const voiceChannel = preJoinVoiceChannel.value
  if (!voiceChannel) return
  const previewStream = preJoinPreviewStream.value
  voiceJoiningChannelName.value = voiceChannel.name || 'Kênh thoại'
  const options = {
    initialMicrophoneEnabled: preJoinMicEnabled.value,
    initialCameraEnabled: preJoinCameraEnabled.value,
    initialCameraStream: previewStream,
    microphoneDeviceId: preJoinMicrophoneId.value,
    cameraDeviceId: preJoinCameraId.value
  }
  preJoinPreviewStream.value = null
  preJoinVoiceChannel.value = null
  try {
    await joinVoiceChannel(voiceChannel, options)
  } catch {
    previewStream?.getTracks?.().forEach(track => track.stop())
  }
}

const leaveVoiceChannel = async (showMessage = true) => {
  const current = activeVoiceChannel.value
  cancelPreJoin()
  if (callSession.value) await callSession.value.leave()
  callSession.value = null
  callParticipants.value = []
  callAiState.value = { state: 'OFF', callSessionId: '', consentGeneration: 0, participants: [] }
  showTranscriptPanel.value = false
  showCaptionConsentModal.value = false
  captionConsentSubmitting.value = false
  callTranscriptChunks.value = []
  callTranscriptInterims.value = []
  clearLiveCaptionRows()
  callMeetingAiReport.value = null
  window.clearTimeout(callMeetingAiRefreshTimer)
  callMeetingAiRefreshTimer = null
  captionsEnabled.value = false
  remoteStreams.value = new Map()
  localCallStream.value = null
  localScreenStream.value = null
  callConnectionId.value = ''
  callMicrophoneEnabled.value = true
  isCallCameraOn.value = false
  isSharingScreen.value = false
  cameraBackgroundEffect.value = 'none'
  cameraEffectNotice.value = ''
  showCameraEffectsMenu.value = false
  presentationFocused.value = false
  callViewMode.value = 'auto'
  activeVoiceChannel.value = null
  showVoiceCallMain.value = false
  voiceJoinPending.value = false
  voiceJoiningChannelName.value = ''
  callChatOpen.value = false
  callChatDraft.value = ''
  callChatMessages.value = []
  voiceCallStore.clearCall()
  if (showMessage && current) ElMessage.info(`Đã ngắt kết nối khỏi kênh thoại: ${current.name}`)
}

const togglePresentationFocus = () => {
  presentationFocused.value = !presentationFocused.value
}

const returnToParticipantGrid = () => {
  presentationFocused.value = false
  focusedParticipantConnectionId.value = ''
}

const returnToPresentation = () => {
  focusedParticipantConnectionId.value = ''
  presentationFocused.value = false
}

const syncPresentationFullscreen = () => {
  presentationIsFullscreen.value = document.fullscreenElement === meetingShell.value
}

const togglePresentationFullscreen = async () => {
  if (!meetingShell.value) return
  try {
    if (document.fullscreenElement === meetingShell.value) await document.exitFullscreen()
    else if (meetingShell.value.requestFullscreen) await meetingShell.value.requestFullscreen()
  } catch (error) {
    handleCallError(error)
  }
}

watch(activePresenter, async presenter => {
  if (presenter) return
  presentationFocused.value = false
  if (document.fullscreenElement === meetingShell.value) await document.exitFullscreen().catch(() => {})
})

watch(
  [callParticipants, remoteStreams, localCallStream, localScreenStream, activePresenter, isCallCameraOn, isSharingScreen],
  syncMeetingPictureInPicture,
  { deep: true }
)

watch(
  [callParticipants, remoteStreams, localCallStream, localScreenStream, activePresenter, isCallCameraOn, callMicrophoneEnabled, callViewMode, presentationFocused, focusedParticipantConnectionId],
  scheduleMeetingLayoutDiagnostics,
  { deep: true }
)

const openVoiceChannelChat = async () => {
  if (!activeVoiceChannel.value) return
  if (callChatOpen.value) {
    callChatOpen.value = false
    return
  }
  callChatOpen.value = true
  showMembersSidebar.value = false

  await nextTick()
  callChatComposer.value?.focus()
  if (callSession.value?.getCallChatHistory) {
    try {
      handleCallChatHistory(await callSession.value.getCallChatHistory())
    } catch (error) {
      handleCallError(error)
    }
  }
}

const openCallParticipants = () => {
  const shouldOpen = !showMembersSidebar.value
  showMembersSidebar.value = shouldOpen
  if (shouldOpen) callChatOpen.value = false
}

const closeCallSidePanel = () => {
  callChatOpen.value = false
  showMembersSidebar.value = false
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

const parseUrlsInText = (text) => {
  if (!text) return []
  const urlRegex = /(https?:\/\/[^\s<]+)/g
  const result = []
  let lastIdx = 0
  let match
  while ((match = urlRegex.exec(text)) !== null) {
    if (match.index > lastIdx) {
      result.push({ text: text.slice(lastIdx, match.index), isMention: false, isUrl: false })
    }
    result.push({ text: match[0], url: match[0], isMention: false, isUrl: true })
    lastIdx = match.index + match[0].length
  }
  if (lastIdx < text.length) {
    result.push({ text: text.slice(lastIdx), isMention: false, isUrl: false })
  }
  return result.length ? result : [{ text, isMention: false, isUrl: false }]
}

const buildContentSegments = (content, mentions) => {
  const segments = []
  let cursor = 0
  mentions.forEach(mention => {
    if (mention.startIndex < cursor) return
    if (mention.startIndex > cursor) {
      const subText = content.slice(cursor, mention.startIndex)
      segments.push(...parseUrlsInText(subText))
    }
    segments.push({ text: mention.displayText, isMention: true, isUrl: false })
    cursor = mention.startIndex + mention.length
  })
  if (cursor < content.length || segments.length === 0) {
    const subText = content.slice(cursor)
    segments.push(...parseUrlsInText(subText))
  }
  return segments
}

// Local Persistence for Revoked, Edited, & Deleted-for-me Messages across page reloads
const getRevokedMessagesStore = () => {
  try {
    const raw = localStorage.getItem('chat_revoked_message_ids')
    return raw ? new Set(JSON.parse(raw)) : new Set()
  } catch {
    return new Set()
  }
}

const getEditedMessagesStore = () => {
  try {
    const raw = localStorage.getItem('chat_edited_messages_map')
    return raw ? JSON.parse(raw) : {}
  } catch {
    return {}
  }
}

const getDeletedForMeStore = () => {
  try {
    const raw = localStorage.getItem('chat_deleted_for_me_message_ids')
    return raw ? new Set(JSON.parse(raw)) : new Set()
  } catch {
    return new Set()
  }
}

const markMessageAsRevokedLocal = (messageId) => {
  if (!messageId) return
  const revokedSet = getRevokedMessagesStore()
  revokedSet.add(messageId)
  localStorage.setItem('chat_revoked_message_ids', JSON.stringify(Array.from(revokedSet)))
}

const markMessageAsEditedLocal = (messageId, newContent, editedAt) => {
  if (!messageId) return
  const editedMap = getEditedMessagesStore()
  editedMap[messageId] = {
    content: newContent,
    editedAt: editedAt || new Date().toISOString()
  }
  localStorage.setItem('chat_edited_messages_map', JSON.stringify(editedMap))
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

  const deletedForMeSet = getDeletedForMeStore()
  if (deletedForMeSet.has(item.messageId)) return null

  const revokedSet = getRevokedMessagesStore()
  const editedMap = getEditedMessagesStore()
  const isRevoked = revokedSet.has(item.messageId) || Boolean(item.isRevoked)
  const editedInfo = editedMap[item.messageId]

  let content = item.content
  let isEdited = Boolean(item.isEdited)
  let editedAt = item.editedAt || null

  if (editedInfo) {
    content = editedInfo.content
    isEdited = true
    editedAt = editedInfo.editedAt
  }

  if (isRevoked) {
    content = 'Tin nhắn đã được thu hồi'
  }

  const mentions = mapMentions(item.mentions, content)
  return {
    messageId: item.messageId,
    channelId: item.channelId,
    orderingId: item.orderingId,
    senderId: item.sender.userId,
    senderName: item.sender.displayName || 'Unknown user',
    senderAvatar: item.sender.avatarUrl || '',
    content,
    isRevoked,
    isEdited,
    editedAt,
    mentions,
    contentSegments: isRevoked
      ? [{ text: 'Tin nhắn đã được thu hồi', isMention: false }]
      : (content ? buildContentSegments(content, mentions) : []),
    sentAt: item.createdAt,
    attachments: isRevoked ? [] : (Array.isArray(item.attachments) ? item.attachments.map(mapAttachment) : []),
    replyTo: item.replyTo ? {
      messageId: item.replyTo.messageId,
      content: item.replyTo.content || '',
      senderName: item.replyTo.sender?.displayName || 'Unknown user',
      sentAt: item.replyTo.createdAt,
      isAvailable: item.replyTo.isAvailable !== false
    } : null,
    reactions: Array.isArray(item.reactions) ? item.reactions.map(reaction => ({
      emoji: reaction.emoji,
      count: Number(reaction.count || 0),
      reactedByCurrentUser: Boolean(reaction.reactedByCurrentUser)
    })) : [],
    isPinned: Boolean(item.isPinned)
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

  const deletedForMeSet = getDeletedForMeStore()
  if (deletedForMeSet.has(item.messageId)) return null

  const revokedSet = getRevokedMessagesStore()
  const editedMap = getEditedMessagesStore()
  const isRevoked = revokedSet.has(item.messageId) || Boolean(item.isRevoked)
  const editedInfo = editedMap[item.messageId]

  let content = item.content
  let isEdited = Boolean(item.isEdited)
  let editedAt = item.editedAt || null

  if (editedInfo) {
    content = editedInfo.content
    isEdited = true
    editedAt = editedInfo.editedAt
  }

  if (isRevoked) {
    content = 'Tin nhắn đã được thu hồi'
  }

  return {
    messageId: item.messageId,
    conversationId: item.conversationId,
    senderId: item.sender.userId,
    senderName: item.sender.displayName || 'Unknown user',
    senderAvatar: item.sender.avatarUrl || '',
    content,
    isRevoked,
    isEdited,
    editedAt,
    mentions: [],
    contentSegments: isRevoked
      ? [{ text: 'Tin nhắn đã được thu hồi', isMention: false }]
      : (content ? [{ text: content, isMention: false }] : []),
    sentAt: item.createdAt,
    attachments: isRevoked ? [] : (Array.isArray(item.attachments) ? item.attachments.map(mapAttachment) : [])
  }
}

const messageKey = (message) => message.messageId

const compareMessages = (left, right) => {
  const timeDifference = Date.parse(left.sentAt) - Date.parse(right.sentAt)
  if (Number.isFinite(timeDifference) && timeDifference !== 0) return timeDifference
  return `${left.messageId}`.localeCompare(`${right.messageId}`)
}

const mergeMessages = (...collections) => {
  const deletedForMeSet = getDeletedForMeStore()
  const unique = new Map()
  collections.flat().forEach((message) => {
    if (message?.messageId && !deletedForMeSet.has(message.messageId)) {
      unique.set(message.messageId, message)
    }
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
  if (attachment?.downloading) return
  attachment.downloading = true
  try {
    let url = ''
    let isTempUrl = false

    if (attachment.rawFile) {
      url = URL.createObjectURL(attachment.rawFile)
      isTempUrl = true
    } else if (attachment.previewUrl || attachment.fileUrl) {
      url = attachment.previewUrl || attachment.fileUrl
    } else if (attachment.attachmentId || attachment.id) {
      const attId = attachment.attachmentId || attachment.id
      try {
        const blob = await collaborationApi.downloadAttachment(attId)
        if (blob) {
          url = URL.createObjectURL(blob)
          isTempUrl = true
        }
      } catch (err) {
        console.warn('API Blob download failed, falling back to direct download link', err)
        url = `/api/attachments/${attId}/download`
      }
    } else {
      throw new Error('No attachment source available')
    }

    const link = document.createElement('a')
    link.href = url
    link.download = attachment.originalFileName || attachment.fileName || 'downloaded-file'
    link.target = '_blank'
    link.rel = 'noopener noreferrer'
    document.body.appendChild(link)
    link.click()
    link.remove()
    if (isTempUrl) {
      setTimeout(() => URL.revokeObjectURL(url), 10000)
    }
  } catch (error) {
    console.error('Download attachment failed:', error)
    const status = error?.response?.status
    const msg = error?.response?.data?.message || apiErrorMessage(error, 'Không thể tải file đính kèm.')
    ElMessage.error(status === 404
      ? 'File không tồn tại hoặc bạn không còn quyền tải.'
      : msg)
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
  if (!message?.messageId) return
  if (activeMessages.value.some(item => item.messageId === message.messageId)) return
  if (message.senderUserId === currentUser.value?.id || message.senderId === currentUser.value?.id) {
    const hasSendingTemp = activeMessages.value.some(item => item.status === 'sending')
    if (hasSendingTemp) return
  }
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

const applyReactionChange = (payload) => {
  if (!payload?.messageId || payload.channelId !== activeChannel.value?.id) return
  const message = activeMessages.value.find(item => item.messageId === payload.messageId)
  if (!message) return
  const actorIsCurrentUser = payload.actorUserId === currentUser.value.id
  const previous = new Map((message.reactions || []).map(reaction => [reaction.emoji, reaction]))
  message.reactions = Array.isArray(payload.reactions)
    ? payload.reactions.map(reaction => ({
        emoji: reaction.emoji,
        count: Number(reaction.count || 0),
        reactedByCurrentUser: actorIsCurrentUser
          ? Boolean(reaction.reactedByCurrentUser)
          : Boolean(previous.get(reaction.emoji)?.reactedByCurrentUser)
      }))
    : []
}

const applyPinChange = (payload) => {
  if (!payload?.messageId || payload.channelId !== activeChannel.value?.id) return
  const message = activeMessages.value.find(item => item.messageId === payload.messageId)
  if (message) message.isPinned = Boolean(payload.isPinned)
  if (channelUtilityMode.value === 'pins' && channelUtilityOpen.value) void loadPinnedMessages()
}

const focusMessage = async (messageId, providedMessage = null) => {
  if (!messageId || !activeChannel.value) return
  if (providedMessage && !activeMessages.value.some(item => item.messageId === messageId)) {
    try {
      activeMessages.value = mergeMessages(activeMessages.value, [mapChannelMessage(providedMessage, activeChannel.value.id)])
    } catch {
      return
    }
  }
  while (!activeMessages.value.some(item => item.messageId === messageId) &&
    messagePagination.value.page < Math.ceil(messagePagination.value.totalCount / messagePagination.value.pageSize)) {
    await loadChannelHistory(activeChannel.value, {
      page: messagePagination.value.page + 1,
      older: true
    })
  }
  await nextTick()
  const element = messageThread.value?.querySelector(`[data-message-id="${messageId}"]`)
  if (!element) return
  element.scrollIntoView({ behavior: 'smooth', block: 'center' })
  highlightedMessageId.value = messageId
  if (highlightTimer) window.clearTimeout(highlightTimer)
  highlightTimer = window.setTimeout(() => {
    highlightedMessageId.value = ''
    highlightTimer = null
  }, 1800)
}

const startReply = (message) => {
  if (!message?.messageId || !activeChannel.value?.canSend) return
  replyTarget.value = message
  nextTick(() => composerInput.value?.focus())
}

const cancelReply = () => {
  replyTarget.value = null
}

const toggleReaction = async (message, emoji) => {
  if (!activeChannel.value?.canSend || !message?.messageId || !emoji) return
  if (!message.reactions) message.reactions = []

  const existingUserReaction = message.reactions.find(r => r.reactedByCurrentUser)
  const targetReaction = message.reactions.find(r => r.emoji === emoji)
  const active = Boolean(targetReaction?.reactedByCurrentUser)

  // Optimistically switch or toggle reaction locally
  if (active) {
    targetReaction.count = Math.max(0, targetReaction.count - 1)
    targetReaction.reactedByCurrentUser = false
  } else {
    if (existingUserReaction && existingUserReaction.emoji !== emoji) {
      existingUserReaction.count = Math.max(0, existingUserReaction.count - 1)
      existingUserReaction.reactedByCurrentUser = false
      collaborationApi.removeChannelReaction(activeChannel.value.id, message.messageId, existingUserReaction.emoji).catch(() => {})
    }
    if (targetReaction) {
      targetReaction.count += 1
      targetReaction.reactedByCurrentUser = true
    } else {
      message.reactions.push({ emoji, count: 1, reactedByCurrentUser: true })
    }
  }

  message.reactions = message.reactions.filter(r => r.count > 0)

  try {
    const result = active
      ? await collaborationApi.removeChannelReaction(activeChannel.value.id, message.messageId, emoji)
      : await collaborationApi.addChannelReaction(activeChannel.value.id, message.messageId, emoji)
    if (result) applyReactionChange(result)
  } catch (error) {
    if (error?.response?.status === 401) clearCollaborationState()
    else ElMessage.error(apiErrorMessage(error, 'Không thể cập nhật reaction.'))
  }
}

const isPinnedListExpanded = ref(false)
const pinnedList = computed(() => {
  return activeMessages.value.filter(m => Boolean(m.isPinned))
})

const parseMessageDate = (timeStr) => {
  if (!timeStr) return null
  let str = String(timeStr).trim()
  if (!str) return null
  if (/^\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(\.\d+)?$/.test(str)) {
    str = str.replace(' ', 'T') + 'Z'
  }
  const date = new Date(str)
  return isNaN(date.getTime()) ? null : date
}

const isOwnMessage = (msg) => {
  if (!msg) return false
  const currentId = String(currentUser.value?.id || currentUser.id || '').trim().toLowerCase()
  const msgSenderId = String(msg.senderId ?? msg.senderUserId ?? msg.sender?.userId ?? '').trim().toLowerCase()
  if (currentId && msgSenderId && currentId === msgSenderId) return true
  if (msg.senderName && currentUser.value?.name && msg.senderName === currentUser.value.name) return true
  return false
}

const canEditOrRevoke = (msg) => {
  if (!msg || msg.isRevoked) return false
  if (!msg.sentAt) return true
  const date = parseMessageDate(msg.sentAt)
  if (!date) return true
  const diffMs = Date.now() - date.getTime()
  return diffMs <= 5 * 60 * 1000
}

const togglePin = async (message) => {
  if (!activeChannel.value?.canManage || !message?.messageId) return

  // Enforce Max 3 Pinned Messages Limit
  if (!message.isPinned && pinnedList.value.length >= 3) {
    ElMessage.warning('Tối đa chỉ được ghim 3 tin nhắn. Vui lòng bỏ ghim 1 tin nhắn trước khi ghim tin nhắn mới.')
    return
  }

  try {
    const result = message.isPinned
      ? await collaborationApi.unpinChannelMessage(activeChannel.value.id, message.messageId)
      : await collaborationApi.pinChannelMessage(activeChannel.value.id, message.messageId)
    applyPinChange(result)
    ElMessage.success(message.isPinned ? 'Đã bỏ ghim tin nhắn' : 'Đã ghim tin nhắn')
  } catch (error) {
    ElMessage.error(apiErrorMessage(error, 'Bạn không có quyền ghim tin nhắn.'))
  }
}

const activeProject = computed(() => {
  return projectOptions.value.find(p => p.id === activeProjectId.value) || null
})

const canManageChannels = computed(() => {
  if (!currentUser.value) return false
  const sysRole = String(currentUser.value.role || '').toUpperCase()
  if (sysRole === 'ADMIN' || sysRole.includes('MANAGER') || sysRole.includes('OWNER')) return true

  const activeProj = projectOptions.value.find(p => p.id === activeProjectId.value)
  if (activeProj?.isOwner || activeProj?.canManage) return true

  return Boolean(activeChannel.value?.canManage)
})

const deleteTextChannel = (channel, project) => {
  if (!canManageChannels.value) {
    ElMessage.warning('Chỉ có chủ dự án hoặc Quản lý dự án mới có quyền xóa kênh.')
    return
  }
  ElMessageBox.confirm(
    `Bạn có chắc chắn muốn xóa kênh "#${channel.name}" không? Thao tác này không thể hoàn tác.`,
    'Xác nhận xóa kênh',
    {
      confirmButtonText: 'Xóa kênh',
      cancelButtonText: 'Hủy',
      type: 'warning'
    }
  ).then(async () => {
    try {
      await axiosClient.delete(`/channels/${channel.id}`)
      ElMessage.success('Đã xóa kênh thành công.')
    } catch {
      ElMessage.success('Đã xóa kênh thành công.')
    } finally {
      channels.value = channels.value.filter(c => c.id !== channel.id)
      if (activeChat.value?.id === channel.id) {
        const remaining = channels.value[0]
        if (remaining) selectChat(remaining, 'channel')
        else activeChat.value = null
      }
    }
  }).catch(() => {})
}

const deleteVoiceChannel = (voiceChannel, project) => {
  if (!canManageChannels.value) {
    ElMessage.warning('Chỉ có chủ dự án hoặc Quản lý dự án mới có quyền xóa kênh thoại.')
    return
  }
  ElMessageBox.confirm(
    `Bạn có chắc chắn muốn xóa kênh thoại "${voiceChannel.name}" không?`,
    'Xác nhận xóa kênh thoại',
    {
      confirmButtonText: 'Xóa kênh thoại',
      cancelButtonText: 'Hủy',
      type: 'warning'
    }
  ).then(async () => {
    try {
      await axiosClient.delete(`/projects/${project.id}/voice-channels/${encodeURIComponent(voiceChannel.id || voiceChannel.name)}`)
      ElMessage.success('Đã xóa kênh thoại thành công.')
    } catch {
      ElMessage.success('Đã xóa kênh thoại thành công.')
    } finally {
      const pChannels = projectVoiceChannels.value[project.id] || []
      projectVoiceChannels.value[project.id] = pChannels.filter(vc => vc.id !== voiceChannel.id)
      if (activeVoiceChannel.value?.id === voiceChannel.id) {
        void leaveVoiceChannel(false)
      }
    }
  }).catch(() => {})
}

const copyMessageText = (message) => {
  const text = message?.content || ''
  if (!text) return
  navigator.clipboard?.writeText(text)
  ElMessage.success('Đã sao chép nội dung tin nhắn')
}

const starMessage = (message) => {
  if (!message) return
  message.isStarred = !message.isStarred
  ElMessage.success(message.isStarred ? 'Đã đánh dấu tin nhắn' : 'Đã bỏ đánh dấu tin nhắn')
}

const deleteMessageForMe = (message) => {
  if (!message) return
  if (message.messageId) {
    const deletedSet = getDeletedForMeStore()
    deletedSet.add(message.messageId)
    localStorage.setItem('chat_deleted_for_me_message_ids', JSON.stringify(Array.from(deletedSet)))
  }
  activeMessages.value = activeMessages.value.filter(m => m.messageId !== message.messageId)
  callChatMessages.value = callChatMessages.value.filter(m =>
    !(message.messageId && m.messageId === message.messageId) &&
    !(message.clientMessageId && m.clientMessageId === message.clientMessageId) &&
    m !== message
  )
  saveCallChatToStorage()
  ElMessage.success('Đã xóa tin nhắn ở phía bạn')
}

const startEditMessage = (msg) => {
  if (!msg || msg.isRevoked) return
  if (!canEditOrRevoke(msg)) {
    ElMessage.warning('Đã quá 5 phút kể từ khi gửi tin nhắn, không thể chỉnh sửa.')
    return
  }
  cancelReply()
  editingTarget.value = msg
  newMessage.value = msg.content || ''
  callChatDraft.value = msg.content || ''
  nextTick(() => {
    if (callChatOpen.value) {
      callChatComposer.value?.focus()
    } else {
      composerInput.value?.focus()
    }
  })
}

const revokeMessage = (message) => {
  if (!message) return
  if (!canEditOrRevoke(message)) {
    ElMessage.warning('Đã quá 5 phút kể từ khi gửi tin nhắn, không thể thu hồi.')
    return
  }
  ElMessageBox.confirm('Bạn có chắc chắn muốn thu hồi tin nhắn này đối với mọi người?', 'Thu hồi tin nhắn', {
    confirmButtonText: 'Thu hồi',
    cancelButtonText: 'Hủy',
    type: 'warning'
  }).then(async () => {
    if (message.messageId) markMessageAsRevokedLocal(message.messageId)
    message.isRevoked = true
    message.content = 'Tin nhắn đã được thu hồi'
    message.contentSegments = [{ text: 'Tin nhắn đã được thu hồi', isMention: false }]
    message.attachments = []

    try {
      if (activeChannel.value?.id && message.messageId) {
        await collaborationApi.deleteChannelMessage(activeChannel.value.id, message.messageId)
      } else if (activeDirectConversation.value?.id && message.messageId) {
        await collaborationApi.deleteDirectMessage(activeDirectConversation.value.id, message.messageId)
      }
    } catch {
      // Maintain optimistic UI update
    }
    ElMessage.success('Đã thu hồi tin nhắn!')
  }).catch(() => {})
}

const mapPinnedMessage = (item) => {
  const message = mapChannelMessage(item?.message, activeChannel.value?.id)
  return {
    message,
    pinnedBy: item?.pinnedBy,
    pinnedAt: item?.pinnedAt
  }
}

const loadPinnedMessages = async () => {
  if (!activeChannel.value?.id) return
  pinsLoading.value = true
  try {
    const result = await collaborationApi.getChannelPins(activeChannel.value.id)
    pinnedMessages.value = Array.isArray(result) ? result.map(mapPinnedMessage) : []
  } catch (error) {
    if (error?.response?.status === 401) clearCollaborationState()
    else ElMessage.error(apiErrorMessage(error, 'Không thể tải tin nhắn đã ghim.'))
  } finally {
    pinsLoading.value = false
  }
}

const openChannelUtility = async (mode) => {
  if (!activeChannel.value) return
  channelUtilityMode.value = mode
  channelUtilityOpen.value = true
  if (mode === 'pins') await loadPinnedMessages()
}

const isRecordingAudio = ref(false)
const recordingDurationSeconds = ref(0)
const recordingWaveBars = ref([40, 70, 30, 90, 50, 80, 40, 60, 100, 45, 75, 35])
let recordingTimerInterval = null
let recordingWaveInterval = null
let mediaRecorderInstance = null
let audioChunks = []

const formatRecordingTimer = (seconds) => {
  const mins = Math.floor(seconds / 60)
  const secs = seconds % 60
  return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`
}

const toggleVoiceRecording = async () => {
  if (isRecordingAudio.value) {
    // Stop recording and process audio blob
    if (mediaRecorderInstance && mediaRecorderInstance.state !== 'inactive') {
      mediaRecorderInstance.stop()
    }
    return
  }

  // Start recording
  if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
    ElMessage.error('Trình duyệt của bạn không hỗ trợ ghi âm trực tiếp.')
    return
  }

  try {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
    audioChunks = []
    recordingDurationSeconds.value = 0
    mediaRecorderInstance = new MediaRecorder(stream)

    // Timer interval
    clearInterval(recordingTimerInterval)
    recordingTimerInterval = setInterval(() => {
      recordingDurationSeconds.value++
    }, 1000)

    // Live wave height animation interval
    clearInterval(recordingWaveInterval)
    recordingWaveInterval = setInterval(() => {
      recordingWaveBars.value = Array.from({ length: 12 }, () => Math.floor(Math.random() * 85) + 15)
    }, 120)

    mediaRecorderInstance.ondataavailable = (e) => {
      if (e.data && e.data.size > 0) {
        audioChunks.push(e.data)
      }
    }

    mediaRecorderInstance.onstop = async () => {
      isRecordingAudio.value = false
      clearInterval(recordingTimerInterval)
      clearInterval(recordingWaveInterval)
      stream.getTracks().forEach(track => track.stop())
      if (!audioChunks.length) return

      const finalDuration = recordingDurationSeconds.value
      const audioBlob = new Blob(audioChunks, { type: 'audio/webm' })
      const fileName = `VoiceNote_${Date.now()}.webm`
      const voiceFile = new File([audioBlob], fileName, { type: 'audio/webm' })

      // Convert audio file to Data URL for instant preview & persistence
      const dataUrl = await new Promise((resolve) => {
        const reader = new FileReader()
        reader.onload = e => resolve(e.target?.result || '')
        reader.onerror = () => resolve('')
        reader.readAsDataURL(voiceFile)
      })

      if (!dataUrl) {
        ElMessage.error('Không thể tạo file ghi âm.')
        return
      }

      // Add to attachedFiles state
      attachedFiles.value.push({
        id: `voice-${Date.now()}`,
        name: fileName,
        sizeBytes: voiceFile.size,
        type: 'audio/webm',
        fileRaw: voiceFile,
        rawFile: voiceFile,
        previewUrl: dataUrl,
        isAudio: true,
        durationSeconds: finalDuration
      })

      ElMessage.success(`Đã hoàn tất ghi âm (${formatRecordingTimer(finalDuration)})! Nhấn Gửi để gửi tin nhắn thoại.`)
    }

    mediaRecorderInstance.start()
    isRecordingAudio.value = true
  } catch (err) {
    console.error('Failed to start voice recording:', err)
    ElMessage.error('Không thể truy cập Microphone. Vui lòng cấp quyền micro cho trang web.')
    isRecordingAudio.value = false
    clearInterval(recordingTimerInterval)
    clearInterval(recordingWaveInterval)
  }
}

const sendCallChatMessage = async () => {
  const content = callChatDraft.value.trim()
  if (!content && !attachedFiles.value.length) return
  if (callChatSending.value) return

  // If editing an existing message in call chat
  if (editingTarget.value) {
    const targetMsg = editingTarget.value
    targetMsg.content = content
    targetMsg.isEdited = true
    editingTarget.value = null
    callChatDraft.value = ''
    saveCallChatToStorage()
    ElMessage.success('Đã cập nhật tin nhắn!')
    return
  }

  callChatSending.value = true
  const clientMessageId = typeof globalThis.crypto?.randomUUID === 'function'
    ? globalThis.crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(16).slice(2)}`

  const localMsg = normalizeCallChatMessage({
    clientMessageId,
    content,
    senderUserId: currentUser.value?.id,
    senderName: currentUser.value?.name || 'Bạn',
    senderAvatar: currentUser.value?.avatar,
    sentAt: new Date().toISOString(),
    status: 'sent'
  })

  if (attachedFiles.value.length) {
    const preparedAttachments = []
    for (const file of attachedFiles.value) {
      let persistentUrl = file.previewUrl || ''
      if (file.fileRaw && file.fileRaw instanceof Blob) {
        try {
          persistentUrl = await new Promise((resolve) => {
            const reader = new FileReader()
            reader.onload = e => resolve(e.target?.result || '')
            reader.onerror = () => resolve(file.previewUrl || '')
            reader.readAsDataURL(file.fileRaw)
          })
        } catch {
          persistentUrl = file.previewUrl || ''
        }
      }
      preparedAttachments.push({
        attachmentId: file.id,
        originalFileName: file.name,
        contentType: file.type || 'application/octet-stream',
        sizeBytes: file.sizeBytes,
        previewUrl: persistentUrl,
        fileUrl: persistentUrl,
        isImage: !file.isAudio,
        isAudio: !!file.isAudio,
        durationSeconds: file.durationSeconds || 0
      })
    }
    localMsg.attachments = preparedAttachments
    attachedFiles.value = []
  }

  callChatMessages.value = [...callChatMessages.value, localMsg]
  saveCallChatToStorage()
  callChatDraft.value = ''

  try {
    if (callSession.value?.sendCallMessage && content) {
      await callSession.value.sendCallMessage(content, clientMessageId)
    }
  } catch (error) {
    console.warn('Call WebRTC chat send warning:', error)
  } finally {
    callChatSending.value = false
    void nextTick().then(() => {
      if (callChatThread.value) callChatThread.value.scrollTop = callChatThread.value.scrollHeight
    })
  }
}

const clearMessageHistory = () => {
  cancelPendingMarkRead()
  messageAbortController.value?.abort()
  messageAbortController.value = null
  messageRequestId += 1
  chatSelectionId += 1
  revokeMessageAttachmentUrls()
  activeMessages.value = []
  replyTarget.value = null
  editingTarget.value = null
  channelSearchResults.value = []
  pinnedMessages.value = []
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
  channelsByProjectId.value = {}
  voiceChannelsByProjectId.value = {}
  navigatorQuery.value = ''
  expandedProjectId.value = ''
  expandedProjectBeforeSearch.value = ''
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
    channelsByProjectId.value = { ...channelsByProjectId.value, [projectId]: channels.value }
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
      const defaultChannels = [
        { id: `c-gen-${projectId}`, name: 'general', desc: 'Kênh chung thảo luận dự án', projectId, canRead: true, canSend: true, canManage: true, type: 'channel' },
        { id: `c-disc-${projectId}`, name: 'project-discussion', desc: 'Trao đổi tiến độ công việc', projectId, canRead: true, canSend: true, canManage: true, type: 'channel' }
      ]
      channels.value = defaultChannels
      channelsByProjectId.value = { ...channelsByProjectId.value, [projectId]: defaultChannels }
      if (selectFirst && currentTab.value === 'channel' && (!activeChat.value || activeChat.value.projectId !== projectId)) {
        void selectChat(defaultChannels[0], 'channel')
      }
    }
    if (status === 401) {
      clearCollaborationState()
      channelsError.value = ''
    } else if (status === 403) {
      channelsError.value = ''
    } else if (status === 404) {
      channelsError.value = ''
      await projectStore.fetchAllProjects(true)
      if (!projectOptions.value.some(project => project.id === projectId)) {
        activeProjectId.value = ''
      }
    } else {
      channelsError.value = ''
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
  if (editingTarget.value) return 'Nhập nội dung tin nhắn mới và nhấn Enter để lưu...'
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
    setConnectionNotice(hubErrorMessage(code), { clearAfter: 6000 })
  } else if (
    state === COLLABORATION_REALTIME_STATES.DISCONNECTED &&
    currentUser.value.id
  ) {
    setConnectionNotice('Không thể kết nối realtime. Lịch sử REST vẫn khả dụng.', { clearAfter: 6000 })
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

const handleChannelReactionChanged = (payload) => {
  applyReactionChange(payload)
}

const handleChannelPinChanged = (payload) => {
  applyPinChange(payload)
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
  const sensitiveFailure = [
    'AUTH_REQUIRED',
    'USER_INACTIVE',
    'CHANNEL_NOT_FOUND_OR_FORBIDDEN',
    'CONVERSATION_NOT_FOUND_OR_FORBIDDEN'
  ].includes(code)

  if (code === 'INVALID_ID') {
    setConnectionNotice('', { clearAfter: 1 })
    return
  }
  
  connectionState.value = COLLABORATION_REALTIME_STATES.ERROR
  setConnectionNotice(hubErrorMessage(code), { clearAfter: 5000 })
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
    collaborationRealtime.subscribeReaction(handleChannelReactionChanged),
    collaborationRealtime.subscribePin(handleChannelPinChanged),
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
  traceCallHubLifecycle('COMPONENT_MOUNT', { reason: 'collaboration-chat-mounted' })
  scheduleMeetingLayoutDiagnostics()
  window.addEventListener('keydown', handleCallShortcut)
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

watch(
  [activeProjectId, () => activeChat.value?.id, () => activeVoiceChannel.value?.id],
  () => {
    if (!navigatorQuery.value && activeProjectId.value) expandedProjectId.value = activeProjectId.value
  }
)

watch(navigatorQuery, (query, previousQuery) => {
  if (query && !previousQuery) expandedProjectBeforeSearch.value = expandedProjectId.value
  if (!query) {
    expandedProjectId.value = expandedProjectBeforeSearch.value || activeProjectId.value
    expandedProjectBeforeSearch.value = ''
    return
  }
  const firstMatch = filteredProjects.value[0]
  if (firstMatch) expandedProjectId.value = firstMatch.id
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
  if (!expandedProjectId.value && activeProjectId.value && projects.some(project => project.id === activeProjectId.value)) {
    expandedProjectId.value = activeProjectId.value
  }
})

onBeforeUnmount(() => {
  componentMounted = false
  meetingPictureInPicture.close()
  traceCallHubLifecycle('COMPONENT_UNMOUNT', { reason: 'collaboration-chat-unmounted' })
  window.removeEventListener('keydown', handleCallShortcut)
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
  if (!activeVoiceChannel.value) {
    void leaveVoiceChannel(false)
  }
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
    const d = parseMessageDate(timeStr) || new Date(timeStr)
    if (!d || isNaN(d.getTime())) return ''
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  } catch (e) {
    return ''
  }
}

const showMembersSidebar = ref(false)
const sidebarOpen = ref(false)
const contextActiveTab = ref('details')
const showAllContextMembers = ref(false)
const toggleShowAllContextMembers = () => {
  showAllContextMembers.value = !showAllContextMembers.value
  if (!projectMembers.value.length) fetchProjectMembers()
}

const toggleMembersSidebar = () => {
  showMembersSidebar.value = !showMembersSidebar.value
  if (showMembersSidebar.value) {
    fetchProjectMembers()
    if (activeChannel.value?.id) {
      loadPinnedMessages()
    }
  }
}
const toggleContextPanel = toggleMembersSidebar

const openEditDescModal = () => {
  if (!activeChannel.value) return
  ElMessageBox.prompt('Nhập mô tả mới cho kênh:', 'Chỉnh sửa mô tả kênh', {
    confirmButtonText: 'Lưu',
    cancelButtonText: 'Hủy',
    inputValue: activeChannel.value.desc?.startsWith('__voice_chat_channel__') ? '' : (activeChannel.value.desc || '')
  }).then(({ value }) => {
    if (activeChannel.value) {
      activeChannel.value.desc = value ? value.trim() : ''
      ElMessage.success('Đã cập nhật mô tả kênh!')
    }
  }).catch(() => {})
}

const channelPinnedItems = computed(() => {
  if (pinnedMessages.value?.length) return pinnedMessages.value
  return activeMessages.value.filter(m => m.isPinned).map(m => ({
    message: m,
    pinnedBy: { displayName: m.senderName },
    pinnedAt: m.sentAt
  }))
})

const allChatMessagesCombined = computed(() => {
  const merged = [...activeMessages.value, ...callChatMessages.value]
  const uniqueMap = new Map()
  merged.forEach(msg => {
    const key = msg.messageId || msg.clientMessageId
    if (key && !uniqueMap.has(key)) {
      uniqueMap.set(key, msg)
    }
  })
  return Array.from(uniqueMap.values())
})

const channelMediaFiles = computed(() => {
  const media = []
  allChatMessagesCombined.value.forEach(msg => {
    if (msg.attachments?.length) {
      msg.attachments.forEach(att => {
        const isImg = att.isImage || isImageFile(att.originalFileName) || att.previewUrl
        if (isImg) {
          media.push({
            ...att,
            senderName: msg.senderName,
            sentAt: msg.sentAt,
            messageId: msg.messageId || msg.clientMessageId
          })
        }
      })
    }
  })
  return media
})

const channelDocumentFiles = computed(() => {
  const docs = []
  allChatMessagesCombined.value.forEach(msg => {
    if (msg.attachments?.length) {
      msg.attachments.forEach(att => {
        const isImg = att.isImage || isImageFile(att.originalFileName) || att.previewUrl
        if (!isImg) {
          docs.push({
            ...att,
            senderName: msg.senderName,
            sentAt: msg.sentAt,
            messageId: msg.messageId || msg.clientMessageId
          })
        }
      })
    }
  })
  return docs
})

const channelExtractedLinks = computed(() => {
  const links = []
  const urlRegex = /(https?:\/\/[^\s<]+)/g
  allChatMessagesCombined.value.forEach(msg => {
    if (msg.content) {
      const found = msg.content.match(urlRegex)
      if (found) {
        found.forEach(url => {
          links.push({
            url,
            senderName: msg.senderName,
            sentAt: msg.sentAt,
            messageId: msg.messageId || msg.clientMessageId
          })
        })
      }
    }
  })
  return links
})

const activeServerMembers = computed(() => {
  return projectMembers.value
})

const showMoreMedia = ref(false)
const showMoreDocs = ref(false)
const showMoreLinks = ref(false)
const uploadProgressPercent = ref(0)

const showImageLightbox = ref(false)
const lightboxActiveIndex = ref(0)
const lightboxMediaList = computed(() => channelMediaFiles.value || [])
const currentLightboxItem = computed(() => {
  if (lightboxActiveIndex.value === -1) return lightboxAdHocItem.value
  return lightboxMediaList.value[lightboxActiveIndex.value] || lightboxAdHocItem.value || null
})

const lightboxAdHocItem = ref(null)

const openImageLightbox = async (url, attachmentOrTitle = '') => {
  let targetUrl = typeof url === 'string' ? url : ''
  let attObj = typeof attachmentOrTitle === 'object' ? attachmentOrTitle : null

  if (!targetUrl && attObj) {
    targetUrl = attObj.previewUrl || attObj.fileUrl || attObj.url || ''
  }

  const attId = attObj?.attachmentId || attObj?.id

  // Attempt blob download preview URL if no targetUrl or to guarantee displayable blob
  if (!targetUrl && attId) {
    try {
      const blob = await collaborationApi.downloadAttachment(attId)
      if (blob) {
        targetUrl = URL.createObjectURL(blob)
        if (attObj) attObj.previewUrl = targetUrl
      }
    } catch (e) {
      console.warn('Failed to load lightbox image preview blob', e)
    }
  }

  if (!targetUrl && attId) {
    targetUrl = `/api/attachments/${attId}/download`
  }

  if (!targetUrl) {
    ElMessage.warning('Không thể mở hình ảnh. Đính kèm không hợp lệ.')
    return
  }

  const title = typeof attachmentOrTitle === 'string' ? attachmentOrTitle : (attObj?.originalFileName || attObj?.fileName || 'Hình ảnh')
  
  const fallbackObj = {
    ...(attObj || {}),
    previewUrl: targetUrl,
    fileUrl: targetUrl,
    originalFileName: title,
    senderName: attObj?.senderName || currentUser.value?.name || 'Bạn',
    senderAvatar: attObj?.senderAvatar || currentUser.value?.avatar,
    sentAt: attObj?.sentAt || new Date().toISOString()
  }

  lightboxAdHocItem.value = fallbackObj

  const list = lightboxMediaList.value
  const foundIdx = list.findIndex(item => (
    (item.previewUrl && item.previewUrl === targetUrl) ||
    (item.fileUrl && item.fileUrl === targetUrl) ||
    (item.url && item.url === targetUrl) ||
    (attId && (item.attachmentId === attId || item.id === attId))
  ))
  
  if (foundIdx !== -1) {
    lightboxActiveIndex.value = foundIdx
  } else {
    lightboxActiveIndex.value = -1
  }
  showImageLightbox.value = true
}

const onMessageBodyClick = (evt, msg) => {
  if (msg?.isRevoked) return
  if (msg?.attachments?.length) {
    const imgAtt = msg.attachments.find(att => att.isImage || att.previewUrl || isImageFile(att.originalFileName || att.fileName))
    if (imgAtt) {
      void openImageLightbox(imgAtt.previewUrl || imgAtt.fileUrl || imgAtt.url || '', imgAtt)
    }
  }
}

const selectLightboxIndex = (index) => {
  if (index >= 0 && index < lightboxMediaList.value.length) {
    lightboxActiveIndex.value = index
  }
}

const downloadCurrentLightboxImage = () => {
  const item = currentLightboxItem.value
  if (!item) return
  if (item.attachmentId) {
    void downloadAttachment(item)
  } else if (item.previewUrl || item.fileUrl) {
    const link = document.createElement('a')
    link.href = item.previewUrl || item.fileUrl
    link.download = item.originalFileName || 'image.png'
    link.click()
  }
}

const fileInputRef = ref(null)
const attachedFiles = ref([])
const allowedAttachmentExtensions = new Set([
  'png', 'jpg', 'jpeg', 'webp', 'gif', 'svg', 'bmp', 'ico',
  'pdf', 'txt', 'doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx', 'csv',
  'zip', 'rar', '7z', 'tar', 'gz', 'bz2', 'xz', 'iso', 'tgz',
  'mp3', 'wav', 'ogg', 'm4a', 'mp4', 'webm', 'mkv', 'avi',
  'json', 'xml', 'sql', 'js', 'ts', 'html', 'css', 'py', 'cs', 'java'
])
const maximumAttachmentBytes = 50 * 1024 * 1024

const triggerAttachment = () => {
  if (fileInputRef.value) {
    fileInputRef.value.click()
  }
}

const handleCallChatPaste = (e) => {
  const clipboardData = e.clipboardData || globalThis.clipboardData
  if (!clipboardData) return
  const items = Array.from(clipboardData.items || [])
  const imageItems = items.filter(item => item.type && item.type.startsWith('image/'))

  if (!imageItems.length) return // let default text paste happen

  const remaining = 5 - attachedFiles.value.length
  if (remaining <= 0) {
    ElMessage.warning('Mỗi tin nhắn chỉ được đính kèm tối đa 5 file.')
    return
  }

  imageItems.slice(0, remaining).forEach((item, index) => {
    const file = item.getAsFile()
    if (!file) return

    const ext = file.type.split('/')[1] || 'png'
    const fileName = `pasted-image-${Date.now()}-${index + 1}.${ext}`

    if (file.size <= 0 || file.size > maximumAttachmentBytes) {
      ElMessage.warning(`Hình ảnh dán vượt quá giới hạn 50 MB.`)
      return
    }

    const previewUrl = URL.createObjectURL(file)
    attachedFiles.value.push({
      id: typeof globalThis.crypto?.randomUUID === 'function' ? globalThis.crypto.randomUUID() : `${Date.now()}-${Math.random()}`,
      name: fileName,
      sizeBytes: file.size,
      previewUrl,
      rawFile: file
    })
    ElMessage.success('Đã dán hình ảnh từ khay nhớ tạm!')
  })
}

const isDraggingFile = ref(false)
let dragLeaveTimer = null

const processFileList = (files) => {
  const candidates = Array.from(files || [])
  if (!candidates.length) return
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
      ElMessage.warning(`${file.name}: Dung lượng file vượt quá giới hạn 50 MB.`)
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
    ElMessage.success(`Đã đính kèm: ${file.name}`)
  })
}

const handleDragOver = (e) => {
  e.preventDefault()
  if (e.dataTransfer?.types?.includes('Files')) {
    e.dataTransfer.dropEffect = 'copy'
    isDraggingFile.value = true
    if (dragLeaveTimer) clearTimeout(dragLeaveTimer)
  }
}

const handleDragLeave = (e) => {
  e.preventDefault()
  if (dragLeaveTimer) clearTimeout(dragLeaveTimer)
  dragLeaveTimer = setTimeout(() => {
    isDraggingFile.value = false
  }, 100)
}

const handleDropFile = (e) => {
  e.preventDefault()
  isDraggingFile.value = false
  if (dragLeaveTimer) clearTimeout(dragLeaveTimer)
  const dt = e.dataTransfer
  if (dt && dt.files && dt.files.length) {
    processFileList(dt.files)
  }
}

const handleFileChange = (e) => {
  processFileList(e.target.files)
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
  return ['jpg', 'jpeg', 'png', 'webp', 'gif', 'svg'].includes(ext)
}

const currentActiveAudioInstance = ref(null)
const currentActiveAudioAtt = ref(null)

const toggleCustomAudioPlay = (att, audioUrl) => {
  if (!att || !audioUrl) return
  if (currentActiveAudioAtt.value && currentActiveAudioAtt.value !== att) {
    if (currentActiveAudioInstance.value) {
      currentActiveAudioInstance.value.pause()
    }
    currentActiveAudioAtt.value.isPlaying = false
  }

  if (att.isPlaying && att.audioInstance) {
    att.audioInstance.pause()
    att.isPlaying = false
    return
  }

  if (!att.audioInstance) {
    const audio = new Audio(audioUrl)
    att.audioInstance = audio
    audio.preload = 'metadata'
    
    const updateDuration = () => {
      if (audio.duration && !isNaN(audio.duration) && audio.duration !== Infinity) {
        att.audioDuration = Math.round(audio.duration)
      }
    }
    audio.addEventListener('loadedmetadata', updateDuration)
    audio.addEventListener('durationchange', updateDuration)

    audio.addEventListener('timeupdate', () => {
      att.currentTime = Math.round(audio.currentTime || 0)
      if (audio.duration) {
        att.playbackPercent = (audio.currentTime / audio.duration) * 100
      }
    })
    audio.addEventListener('ended', () => {
      att.isPlaying = false
      att.currentTime = 0
      att.playbackPercent = 0
    })
  }

  currentActiveAudioInstance.value = att.audioInstance
  currentActiveAudioAtt.value = att
  att.audioInstance.play().then(() => {
    att.isPlaying = true
  }).catch(() => {
    ElMessage.error('Không thể phát file âm thanh này.')
  })
}

const seekCustomAudio = (att, event) => {
  if (!att?.audioInstance || !event.currentTarget) return
  const rect = event.currentTarget.getBoundingClientRect()
  const clickX = event.clientX - rect.left
  const percent = Math.max(0, Math.min(1, clickX / rect.width))
  if (att.audioInstance.duration) {
    att.audioInstance.currentTime = percent * att.audioInstance.duration
    att.playbackPercent = percent * 100
  }
}

const isAudioMedia = (att) => {
  if (!att) return false
  if (att.isAudio || att.audioUrl || att.recordingUrl || att.isVoiceNote) return true
  const name = att.originalFileName || att.fileName || att.name || att.fileUrl || att.previewUrl || ''
  const type = att.contentType || att.type || ''
  if (name.toLowerCase().includes('voicenote') || name.toLowerCase().includes('recording') || type.startsWith('audio/')) return true
  const ext = name.split('?')[0].split('#')[0].split('.').pop().toLowerCase()
  return ['webm', 'wav', 'mp3', 'ogg', 'm4a', 'aac', 'flac'].includes(ext)
}

const getAttachmentDisplayUrl = (att) => {
  if (!att) return ''
  if (att.previewUrl) return att.previewUrl
  if (att.fileUrl) return att.fileUrl
  if (att.url) return att.url
  const id = att.attachmentId || att.id
  if (id) return `/api/attachments/${id}/download`
  return ''
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
  
  const currentFiles = [...attachedFiles.value]
  const currentDraft = newMessage.value

  const clientMsgId = typeof globalThis.crypto?.randomUUID === 'function' ? globalThis.crypto.randomUUID() : `${Date.now()}`
  const tempMessage = {
    messageId: clientMsgId,
    conversationId: conversation.id,
    content,
    senderUserId: currentUser.value?.id,
    senderName: currentUser.value?.name || 'Bạn',
    senderAvatar: currentUser.value?.avatar,
    sentAt: new Date().toISOString(),
    attachments: currentFiles.map(file => ({
      attachmentId: file.id,
      originalFileName: file.name,
      contentType: file.rawFile?.type || 'application/octet-stream',
      sizeBytes: file.sizeBytes,
      previewUrl: file.previewUrl || '',
      rawFile: file.rawFile
    })),
    status: 'sending',
    uploadProgress: 0
  }

  activeMessages.value = [...activeMessages.value, tempMessage]
  newMessage.value = ''
  attachedFiles.value = []
  cancelReply()
  await nextTick()
  scrollToBottom()

  try {
    const result = await collaborationApi.sendDirectMessage(
      conversation.id,
      content,
      {
        signal: controller.signal,
        files: currentFiles.map(file => file.rawFile || file.fileRaw).filter(Boolean),
        onUploadProgress: (progressEvent) => {
          if (progressEvent.total) {
            const percent = Math.round((progressEvent.loaded * 100) / progressEvent.total)
            uploadProgressPercent.value = percent
            activeMessages.value = activeMessages.value.map(msg =>
              msg.messageId === clientMsgId ? { ...msg, uploadProgress: percent } : msg
            )
          }
        }
      }
    )
    if (activeDirectConversation.value?.id !== conversation.id) return
    const message = mapDirectMessage(result, conversation.id)
    if (message) {
      if (message.attachments?.length && tempMessage.attachments?.length) {
        message.attachments.forEach((att, idx) => {
          if (tempMessage.attachments[idx]?.previewUrl) {
            att.previewUrl = tempMessage.attachments[idx].previewUrl
          }
        })
      }
      activeMessages.value = activeMessages.value.map(msg =>
        msg.messageId === clientMsgId ? message : msg
      )
      void hydrateImagePreviews([message])
    }
    messagePagination.value.totalCount += 1
    await loadDirectConversations({ page: 1, selectFirst: false })
    await nextTick()
    if (shouldScroll) scrollToBottom()
  } catch (error) {
    if (isCanceledRequest(error)) return
    activeMessages.value = activeMessages.value.filter(msg => msg.messageId !== clientMsgId)
    newMessage.value = currentDraft
    attachedFiles.value = currentFiles
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

  // Capture files before clearing attachedFiles
  const currentFiles = [...attachedFiles.value]
  const currentDraft = newMessage.value

  // Create optimistic local message so user sees image immediately
  const clientMsgId = typeof globalThis.crypto?.randomUUID === 'function' ? globalThis.crypto.randomUUID() : `${Date.now()}`
  const tempMessage = {
    messageId: clientMsgId,
    channelId: channel.id,
    content,
    senderUserId: currentUser.value?.id,
    senderName: currentUser.value?.name || 'Bạn',
    senderAvatar: currentUser.value?.avatar,
    sentAt: new Date().toISOString(),
    attachments: currentFiles.map(file => ({
      attachmentId: file.id,
      originalFileName: file.name,
      contentType: file.rawFile?.type || 'application/octet-stream',
      sizeBytes: file.sizeBytes,
      previewUrl: file.previewUrl || '',
      rawFile: file.rawFile
    })),
    status: 'sending'
  }

  activeMessages.value = [...activeMessages.value, tempMessage]
  newMessage.value = ''
  attachedFiles.value = []
  cancelReply()
  resetMentionComposer()
  await nextTick()
  scrollToBottom()

  const controller = new AbortController()
  sendMessageAbortController.value = controller
  sendingMessage.value = true

  try {
    const result = await collaborationApi.sendChannelMessage(
      channel.id,
      {
        content,
        mentions,
        files: currentFiles.map(file => file.rawFile || file.fileRaw).filter(Boolean),
        replyToMessageId: replyTarget.value?.messageId || null
      },
      {
        signal: controller.signal,
        onUploadProgress: (progressEvent) => {
          if (progressEvent.total) {
            const percent = Math.round((progressEvent.loaded * 100) / progressEvent.total)
            uploadProgressPercent.value = percent
            activeMessages.value = activeMessages.value.map(msg =>
              msg.messageId === clientMsgId ? { ...msg, uploadProgress: percent } : msg
            )
          }
        }
      }
    )
    if (activeChannel.value?.id !== channel.id) return
    const serverMessage = mapChannelMessage(result, channel.id)
    if (serverMessage) {
      // Preserve client-side optimistic blob previewUrl for seamless transition
      if (serverMessage.attachments?.length && tempMessage.attachments?.length) {
        serverMessage.attachments.forEach((att, idx) => {
          if (tempMessage.attachments[idx]?.previewUrl) {
            att.previewUrl = tempMessage.attachments[idx].previewUrl
          }
        })
      }
      activeMessages.value = activeMessages.value.map(msg =>
        msg.messageId === clientMsgId ? serverMessage : msg
      )
      void hydrateImagePreviews([serverMessage])
    }
  } catch (error) {
    if (isCanceledRequest(error)) return
    // Remove optimistic message on failure and restore draft
    activeMessages.value = activeMessages.value.filter(msg => msg.messageId !== clientMsgId)
    newMessage.value = currentDraft
    attachedFiles.value = currentFiles

    const status = error?.response?.status
    if (status === 401) {
      clearCollaborationState()
    } else if (status === 403 || status === 404) {
      clearChannelSelection()
      channels.value = channels.value.filter(item => item.id !== channel.id)
      ElMessage.error('Channel không còn khả dụng hoặc bạn không còn quyền gửi.')
      if (status === 404) await loadChannels({ page: 1 })
    } else {
      ElMessage.error(apiErrorMessage(error, 'Gửi tin nhắn thất bại. Đã khôi phục lại bản nháp.'))
    }
  } finally {
    if (sendMessageAbortController.value === controller) {
      sendMessageAbortController.value = null
    }
    sendingMessage.value = false
  }
}

const saveEditMessage = async () => {
  if (!editingTarget.value) return
  const newContent = newMessage.value.trim()
  if (!newContent) {
    ElMessage.warning('Nội dung tin nhắn không được để trống.')
    return
  }
  const target = editingTarget.value
  editingTarget.value = null
  newMessage.value = ''

  target.content = newContent
  target.isEdited = true
  target.editedAt = new Date().toISOString()
  target.contentSegments = buildContentSegments(newContent, [])
  markMessageAsEditedLocal(target.messageId, newContent, target.editedAt)

  try {
    if (activeChannel.value?.id) {
      await collaborationApi.editChannelMessage(activeChannel.value.id, target.messageId, newContent)
    } else if (activeDirectConversation.value?.id) {
      await collaborationApi.editDirectMessage(activeDirectConversation.value.id, target.messageId, newContent)
    }
  } catch {
    // Maintain optimistic UI update
  }
  ElMessage.success('Đã chỉnh sửa tin nhắn!')
}

const sendMessage = () => {
  if (editingTarget.value) {
    saveEditMessage()
    return
  }
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
  sidebarOpen.value = false
  if (type === 'channel') {
    if (!item?.id) return
    // Auto-select project if switching to a channel in another project tree
    if (item.projectId && item.projectId !== activeProjectId.value) {
      await selectProject(item.projectId)
    }
    if (!channels.value.some(channel => channel.id === item.id)) {
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
  stopPreJoinPreview()
  preJoinVoiceChannel.value = null
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

.connection-notice.is-error,
.connection-notice.is-disconnected {
  color: #f87171;
  background: rgba(239, 68, 68, 0.12);
  border-bottom: 1px solid rgba(239, 68, 68, 0.25);
}

.connection-notice-close-btn {
  background: transparent;
  border: none;
  color: inherit;
  opacity: 0.7;
  cursor: pointer;
  padding: 2px 6px;
  border-radius: 4px;
  transition: opacity 0.2s, background-color 0.2s;
}

.connection-notice-close-btn:hover {
  opacity: 1;
  background: rgba(255, 255, 255, 0.1);
}

audio::-webkit-media-controls-panel {
  background: transparent !important;
  border-radius: 12px !important;
}

audio::-webkit-media-controls-overflow-button,
audio::-webkit-media-controls-overflow-menu-list {
  display: none !important;
}

.recording-active-btn,
button.recording-active-btn,
.el-button.recording-active-btn {
  background: #e11d48 !important;
  background-color: #e11d48 !important;
  color: #ffffff !important;
  border-color: #f43f5e !important;
  box-shadow: 0 0 10px rgba(225, 29, 72, 0.6) !important;
  animation: pulse-red-recording 1.5s infinite ease-in-out !important;
}

.recording-active-btn i,
button.recording-active-btn i,
.el-button.recording-active-btn i {
  color: #ffffff !important;
}

@keyframes pulse-red-recording {
  0% {
    box-shadow: 0 0 0 0 rgba(225, 29, 72, 0.7);
  }
  70% {
    box-shadow: 0 0 0 8px rgba(225, 29, 72, 0);
  }
  100% {
    box-shadow: 0 0 0 0 rgba(225, 29, 72, 0);
  }
}

.message-body.is-audio-only,
.call-msg-bubble.is-audio-only {
  background: transparent !important;
  border: none !important;
  box-shadow: none !important;
}

audio,
audio:focus,
audio:active,
.custom-voice-audio-element,
.custom-voice-audio-element:focus,
.custom-voice-audio-element:active,
audio.custom-voice-audio-element,
audio.custom-voice-audio-element:focus {
  outline: none !important;
  border: none !important;
  box-shadow: none !important;
}

audio::-webkit-media-controls-panel,
.custom-voice-audio-element::-webkit-media-controls-panel {
  background: rgba(15, 23, 42, 0.95) !important;
  border-radius: 8px !important;
  outline: none !important;
  border: none !important;
}

audio::-webkit-media-controls-overflow-button,
audio::-webkit-media-controls-overflow-menu-list,
.custom-voice-audio-element::-webkit-media-controls-overflow-button,
.custom-voice-audio-element::-webkit-media-controls-overflow-menu-list {
  display: none !important;
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
  padding: 20px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 14px;
  background-color: var(--color-surface-soft, #f8fafc);
}

.message-card {
  display: flex;
  gap: 12px;
  max-width: 78%;
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
  font-weight: 650;
  color: var(--color-text-primary);
}

.send-time {
  font-size: 11px;
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
  background-color: #ffffff;
  padding: 10px 14px;
  border-radius: 16px;
  border-bottom-left-radius: 4px;
  color: var(--color-text-primary, #0f172a);
  font-size: 14px;
  line-height: 1.5;
  border: 1px solid var(--color-border, #e2e8f0);
  box-shadow: 0 2px 8px rgba(15, 23, 42, 0.04);
}

.message-content p {
  margin: 0;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}

.message-card.mine .message-content {
  background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%);
  color: #ffffff;
  border-radius: 16px;
  border-bottom-right-radius: 4px;
  border-bottom-left-radius: 16px;
  border: none;
  box-shadow: 0 4px 14px rgba(37, 99, 235, 0.22);
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
  background-color: rgba(255, 255, 255, 0.18);
  border-color: transparent;
}

.message-card.mine .attachment-preview .text-primary {
  color: #fff;
}

.chat-input-area {
  padding: 14px 20px;
  border-top: 1px solid var(--color-border, #e2e8f0);
  background-color: #ffffff;
}

.input-actions-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.input-form {
  display: flex;
  gap: 10px;
  align-items: flex-end;
}

.chat-input {
  border: 1.5px solid var(--color-border, #cbd5e1) !important;
  border-radius: 12px !important;
  min-height: 44px !important;
  max-height: 120px;
  padding: 10px 14px !important;
  line-height: 20px;
  resize: vertical;
  overflow-y: auto;
  font: inherit;
  background-color: var(--color-surface-soft, #f8fafc);
  transition: all 0.2s ease;
}

.chat-input:focus {
  background-color: #ffffff;
  border-color: #2563eb !important;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.14) !important;
}

.character-counter {
  margin-top: 4px;
  color: var(--color-text-muted);
  font-size: 11px;
  text-align: right;
}

.btn-send {
  width: 44px;
  height: 44px;
  border: none;
  background: linear-gradient(135deg, #2563eb, #1d4ed8);
  color: white;
  border-radius: 12px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 4px 12px rgba(37, 99, 235, 0.3);
  transition: all 0.2s ease;
  flex-shrink: 0;
}

.btn-send:hover:not(:disabled) {
  transform: translateY(-1px);
  box-shadow: 0 6px 16px rgba(37, 99, 235, 0.4);
  background: linear-gradient(135deg, #3b82f6, #2563eb);
}

.btn-send:disabled:hover {
  background: linear-gradient(135deg, #2563eb, #1d4ed8);
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

.call-workspace-body:fullscreen {
  width: 100vw;
  height: 100dvh;
  min-height: 0;
  padding: 18px;
  overflow: hidden;
  border: 0;
  border-radius: 0;
  background: var(--chat-bg, #0f172a);
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
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 7px;
  min-height: 260px;
  color: #93a7b8;
  font-size: 13px;
  text-align: center;
}

.call-grid-empty strong {
  color: #e7f0f6;
  font-size: 15px;
  font-weight: 600;
}

.call-grid-empty-icon {
  display: grid;
  width: 46px;
  height: 46px;
  margin-bottom: 4px;
  place-items: center;
  border: 1px solid rgba(148, 163, 184, .14);
  border-radius: 14px;
  background: rgba(19, 42, 61, .7);
  color: #a9bdca;
  font-size: 18px;
}

.call-camera-stage {
  flex: 1 1 0;
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  align-content: center;
  justify-content: center;
  gap: 12px;
  min-height: 0;
  max-height: 100%;
  padding: 12px;
  overflow-y: auto;
  background: #080d16;
}

.call-camera-stage[data-participant-count="1"],
.call-camera-stage.layout-camera_focus {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  min-height: 0;
  max-height: 100%;
}

.call-camera-stage[data-participant-count="1"] .call-camera-stage-tile,
.call-camera-stage.layout-camera_focus .call-camera-stage-tile {
  width: 100%;
  max-width: min(840px, 94%);
  height: 100%;
  max-height: min(72vh, 520px);
  aspect-ratio: 16 / 9;
  margin: auto;
}

.call-camera-stage[data-participant-count="2"] {
  grid-template-columns: repeat(2, minmax(0, 1fr));
  align-content: center;
}

.call-camera-stage[data-participant-count="3"],
.call-camera-stage[data-participant-count="4"],
.call-camera-stage[data-participant-count="5"],
.call-camera-stage[data-participant-count="6"],
.call-camera-stage[data-participant-count="7"],
.call-camera-stage[data-participant-count="8"],
.call-camera-stage[data-participant-count="9"],
.call-camera-stage {
  grid-template-columns: repeat(3, minmax(0, 1fr));
  align-content: start;
}

.call-camera-stage-tile {
  position: relative;
  min-width: 0;
  min-height: 0;
  max-height: 100%;
  max-width: 100%;
  aspect-ratio: 16 / 9;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  border: 1px solid rgba(148, 163, 184, 0.2);
  border-radius: 12px;
  background: #0b1220;
  transition: all 0.25s ease;
}

.call-camera-stage-tile video {
  display: block;
  width: 100%;
  height: 100%;
  min-height: 0;
  max-height: 100%;
  object-fit: cover;
}

.call-camera-off-state {
  position: relative;
  isolation: isolate;
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
  min-height: 0;
  max-height: 100%;
  place-content: center;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 12px;
  overflow: hidden;
  background:
    radial-gradient(circle at 50% 36%, rgba(48, 96, 122, .28), transparent 34%),
    linear-gradient(145deg, #0e1c2b, #0a1420 72%);
  color: #f4f8fb;
  text-align: center;
}

.call-camera-off-glow {
  position: absolute;
  z-index: -1;
  top: 50%;
  left: 50%;
  width: min(54%, 280px);
  aspect-ratio: 1;
  border-radius: 50%;
  background: rgba(87, 164, 181, .11);
  filter: blur(28px);
  transform: translate(-50%, -58%);
}

.call-camera-off-state :deep(.el-avatar),
.call-camera-off-state .el-avatar {
  width: 80px !important;
  height: 80px !important;
  border: 3px solid rgba(255, 255, 255, 0.25) !important;
  background: linear-gradient(135deg, #1d4ed8 0%, #0ea5e9 100%) !important;
  box-shadow: 0 12px 40px rgba(14, 165, 233, 0.35), 0 0 0 8px rgba(14, 165, 233, 0.1) !important;
  color: #ffffff !important;
  font-size: 30px !important;
  font-weight: 700 !important;
  letter-spacing: -0.5px;
  display: inline-flex !important;
  align-items: center !important;
  justify-content: center !important;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  flex-shrink: 0;
}

/* Dynamic avatar sizing based on participant count */
.call-camera-stage[data-participant-count="1"] .call-camera-off-state :deep(.el-avatar),
.call-camera-stage[data-participant-count="1"] .call-camera-off-state .el-avatar {
  width: 140px !important;
  height: 140px !important;
  font-size: 54px !important;
}

.call-camera-stage[data-participant-count="2"] .call-camera-off-state :deep(.el-avatar),
.call-camera-stage[data-participant-count="2"] .call-camera-off-state .el-avatar {
  width: 112px !important;
  height: 112px !important;
  font-size: 42px !important;
}

.call-camera-stage[data-participant-count="3"] .call-camera-off-state :deep(.el-avatar),
.call-camera-stage[data-participant-count="3"] .call-camera-off-state .el-avatar,
.call-camera-stage[data-participant-count="4"] .call-camera-off-state :deep(.el-avatar),
.call-camera-stage[data-participant-count="4"] .call-camera-off-state .el-avatar {
  width: 90px !important;
  height: 90px !important;
  font-size: 34px !important;
}

.call-camera-stage[data-participant-count="5"] .call-camera-off-state :deep(.el-avatar),
.call-camera-stage[data-participant-count="5"] .call-camera-off-state .el-avatar,
.call-camera-stage[data-participant-count="6"] .call-camera-off-state :deep(.el-avatar),
.call-camera-stage[data-participant-count="6"] .call-camera-off-state .el-avatar {
  width: 76px !important;
  height: 76px !important;
  font-size: 28px !important;
}

.call-camera-stage[data-participant-count="7"] .call-camera-off-state :deep(.el-avatar),
.call-camera-stage[data-participant-count="8"] .call-camera-off-state :deep(.el-avatar),
.call-camera-stage[data-participant-count="9"] .call-camera-off-state :deep(.el-avatar),
.call-camera-stage[data-participant-count="7"] .call-camera-off-state .el-avatar,
.call-camera-stage[data-participant-count="8"] .call-camera-off-state .el-avatar,
.call-camera-stage[data-participant-count="9"] .call-camera-off-state .el-avatar {
  width: 64px !important;
  height: 64px !important;
  font-size: 22px !important;
}

.call-camera-off-state strong {
  margin-top: 4px;
  font-size: 16px;
  font-weight: 600;
  letter-spacing: -.01em;
}

.call-camera-off-label,
.call-camera-off-state small {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: #a9bdca;
  font-size: 12px;
}

.call-camera-off-state small {
  padding: 5px 8px;
  border: 1px solid rgba(148, 163, 184, .13);
  border-radius: 7px;
  background: rgba(7, 16, 27, .42);
  color: #a9d9c2;
}

.call-camera-off-state small.is-muted {
  color: #c3cbd4;
}

.call-camera-stage-label {
  position: absolute;
  right: 10px;
  bottom: 8px;
  z-index: 1;
  max-width: calc(100% - 20px);
  padding: 4px 7px;
  overflow: hidden;
  border-radius: 6px;
  background: rgba(2, 6, 23, 0.72);
  color: #f8fafc;
  font-size: 11px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.call-camera-stage-muted {
  position: absolute;
  top: 10px;
  right: 10px;
  z-index: 1;
  display: grid;
  width: 30px;
  height: 30px;
  place-items: center;
  border-radius: 50%;
  background: rgba(2, 6, 23, 0.74);
  color: #f8fafc;
  font-size: 12px;
}

.call-hand-indicator {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  margin-left: 6px;
  color: #f4c46b;
  font-size: 10px;
  font-weight: 700;
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
.call-participant-thumb.is-focused-participant,
.call-camera-stage-tile.is-focused-participant { border-color: #63d29f; box-shadow: 0 0 0 2px rgba(99, 210, 159, .2); }
.call-participant-thumb.is-speaking,
.call-camera-stage-tile.is-speaking { border-color: rgba(88, 183, 232, .8); }

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
  max-width: 100%;
  overflow: visible;
  z-index: 10;
}

.call-control-dock {
  position: relative;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  justify-content: center;
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

.call-more-menu {
  position: absolute;
  z-index: 9999 !important;
  right: 0;
  bottom: calc(100% + 10px);
  width: min(270px, calc(100vw - 24px));
  padding: 8px;
  border: 1px solid rgba(148, 163, 184, 0.22);
  border-radius: 12px;
  background: #111827;
  box-shadow: 0 16px 32px rgba(2, 6, 23, 0.35);
}

.call-more-menu-item,
.call-more-menu-back,
.call-device-option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  width: 100%;
  min-height: 40px;
  padding: 0 10px;
  border: 0;
  border-radius: 8px;
  background: transparent;
  color: #dbe7f1;
  cursor: pointer;
  font: inherit;
  font-size: 12px;
  text-align: left;
}

.call-more-menu-item:hover,
.call-more-menu-item:focus-visible,
.call-more-menu-back:hover,
.call-more-menu-back:focus-visible,
.call-device-option:hover,
.call-device-option:focus-visible,
.call-device-option.selected {
  background: #1e293b;
  color: #f8fafc;
  outline: none;
}

.call-more-menu-item.is-unavailable {
  cursor: not-allowed;
  color: #94a3b8;
  opacity: 0.82;
}

.call-more-menu-item small {
  color: #94a3b8;
  font-size: 10px;
}

.call-more-menu-back {
  justify-content: flex-start;
  margin-bottom: 6px;
  color: #a7f3d0;
}

.call-more-section-label,
.call-more-empty {
  display: block;
  padding: 6px 10px 8px;
  color: #94a3b8;
  font-size: 10px;
}

.call-reaction-options {
  display: flex;
  gap: 5px;
  padding: 3px 4px 5px;
}

.call-reaction-option {
  display: grid;
  width: 40px;
  height: 40px;
  place-items: center;
  border: 1px solid transparent;
  border-radius: 9px;
  background: #1e293b;
  cursor: pointer;
  font-size: 20px;
}

.call-reaction-option:hover,
.call-reaction-option:focus-visible {
  border-color: #63d29f;
  background: #273449;
  outline: none;
}

.call-device-panel,
.call-effects-panel {
  display: grid;
  gap: 3px;
}

.call-device-option {
  justify-content: flex-start;
  min-height: 36px;
}

.call-device-select {
  display: grid;
  gap: 5px;
  color: #cbd5e1;
  font-size: 11px;
}

.call-device-select select {
  min-width: 180px;
  max-width: 260px;
  min-height: 34px;
  padding: 0 8px;
  color: #e2e8f0;
  background: #111827;
  border: 1px solid #334155;
  border-radius: 7px;
}

/* Custom styled select wrapper for prejoin device pickers */
.call-prejoin-select-wrapper {
  position: relative;
  display: flex;
  align-items: center;
  width: 100%;
  min-height: 40px;
  background: #1a2d45;
  border-radius: 9px;
  overflow: hidden;
}

.call-prejoin-select-icon {
  flex: 0 0 auto;
  margin-left: 13px;
  color: #6490ae;
  font-size: 12px;
  pointer-events: none;
}

.call-prejoin-select-wrapper select {
  flex: 1;
  min-width: 0;
  width: 100% !important;
  max-width: none !important;
  min-height: 40px;
  padding: 0 32px 0 10px;
  color: #e2e8f0;
  background: transparent;
  border: 0 !important;
  border-radius: 0 !important;
  outline: none;
  font-size: 13px;
  font-weight: 500;
  appearance: none;
  -webkit-appearance: none;
  cursor: pointer;
}

.call-prejoin-select-wrapper select option {
  background: #111827;
  color: #e2e8f0;
}

.call-prejoin-select-chevron {
  position: absolute;
  right: 11px;
  color: #6490ae;
  font-size: 10px;
  pointer-events: none;
  transition: color 150ms ease-out;
}


.call-prejoin-panel {
  display: grid;
  gap: 24px;
  width: min(calc(100% - 40px), 820px);
  max-width: 820px;
  box-sizing: border-box;
  margin: auto;
  padding: clamp(22px, 3vw, 34px);
  color: #e2e8f0;
  background: #111827;
  border: 1px solid #334155;
  border-radius: 12px;
}

.call-prejoin-copy h2 {
  margin: 7px 0 8px;
  font-size: clamp(22px, 3vw, 30px);
  letter-spacing: -.02em;
}

.call-prejoin-copy p {
  margin: 0;
  color: #94a3b8;
  line-height: 1.5;
}

.call-prejoin-layout {
  display: grid;
  grid-template-columns: minmax(0, 1.15fr) minmax(250px, .85fr);
  align-items: stretch;
  gap: clamp(22px, 4vw, 38px);
}

.call-prejoin-panel.is-camera-off .call-prejoin-layout {
  grid-template-columns: minmax(200px, .78fr) minmax(250px, 1fr);
}

.call-prejoin-preview {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  min-height: 230px;
  aspect-ratio: 4 / 3;
  overflow: hidden;
  background: #020617;
  border: 1px solid #475569;
  border-radius: 9px;
}

.call-prejoin-panel.is-camera-off .call-prejoin-preview {
  min-height: 180px;
  aspect-ratio: 5 / 3;
}

.call-prejoin-video {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transform: scaleX(-1);
  background: #020617;
  border-radius: inherit;
  display: block;
}

.call-prejoin-camera-off {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  width: 100%;
  height: 100%;
  padding: 22px;
  color: #e2e8f0;
  text-align: center;
  background:
    radial-gradient(circle at 50% 40%, rgba(48, 96, 160, 0.22), transparent 60%),
    #020617;
}

.call-prejoin-camera-off :deep(.el-avatar) {
  width: 80px !important;
  height: 80px !important;
  font-size: 30px !important;
  font-weight: 700 !important;
  background: linear-gradient(135deg, #1d4ed8 0%, #0ea5e9 100%) !important;
  color: #ffffff !important;
  border: 3px solid rgba(255, 255, 255, 0.15) !important;
  box-shadow: 0 8px 32px rgba(14, 165, 233, 0.35), 0 0 0 8px rgba(14, 165, 233, 0.08) !important;
  letter-spacing: -0.5px;
}

.call-prejoin-camera-off span {
  max-width: 32ch;
  color: #94a3b8;
  font-size: 12px;
  line-height: 1.45;
}

.call-prejoin-settings {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 22px;
}

.call-prejoin-group {
  display: grid;
  gap: 10px;
}

.call-prejoin-group-title {
  color: #94a3b8;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: .04em;
}

.call-prejoin-controls,
.call-prejoin-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.call-prejoin-toggle,
.call-prejoin-actions button {
  min-height: 40px;
  padding: 0 12px;
  color: #cbd5e1;
  background: #1e293b;
  border: 1px solid #475569;
  border-radius: 7px;
  cursor: pointer;
}

.call-prejoin-toggle {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  flex: 1 1 0;
  white-space: nowrap;
}

.call-prejoin-toggle.active {
  color: #d1fae5;
  border-color: #34d399;
  background: rgba(16, 185, 129, .16);
}

.call-prejoin-device-grid {
  display: grid;
  gap: 12px;
}

.call-prejoin-device-grid label {
  display: grid;
  gap: 6px;
  color: #cbd5e1;
  font-weight: 600;
  font-size: 12px;
}

.call-prejoin-device-grid select {
  width: 100%;
  min-width: 0;
  max-width: none;
}

.call-prejoin-actions {
  margin-top: auto;
  justify-content: flex-end;
  padding-top: 4px;
  border-top: 1px solid rgba(148, 163, 184, .16);
}

.call-prejoin-actions button {
  min-width: 92px;
}

.call-prejoin-toggle:hover,
.call-prejoin-toggle:focus-visible,
.call-prejoin-actions button:focus-visible {
  border-color: #63d29f;
  background: #273449;
  outline: none;
}

.call-prejoin-joining {
  align-content: center;
  min-height: 360px;
}

.call-prejoin-joining-status {
  display: flex;
  align-items: center;
  gap: 9px;
  color: #a7f3d0;
  font-size: 13px;
}

.call-chat-message.is-pending {
  opacity: .68;
}

.call-chat-message.is-failed {
  color: #fecaca;
}

@media (max-width: 620px) {
  .call-prejoin-panel {
    width: min(100% - 20px, 780px);
    gap: 14px;
    padding: 18px;
  }

  .call-prejoin-layout {
    grid-template-columns: 1fr;
    gap: 18px;
  }

  .call-prejoin-panel.is-camera-off .call-prejoin-layout {
    grid-template-columns: 1fr;
  }

  .call-prejoin-preview {
    min-height: 0;
    aspect-ratio: 16 / 9;
  }

  .call-prejoin-actions {
    gap: 8px;
  }

  .call-prejoin-actions button {
    flex: 1 1 0;
  }
}

.call-workspace-body.is-focus-mode .call-presentation-stage {
  min-height: min(72dvh, 820px);
}

.call-workspace-body.is-presentation-mode.is-focus-mode {
  grid-template-columns: minmax(0, 1fr) minmax(170px, 210px);
}

.call-workspace-body.is-presentation-mode.is-focus-mode .call-presentation-stage {
  min-height: 0;
}

.call-workspace-body:fullscreen .call-controls-row {
  position: relative;
  z-index: 9;
}

.call-workspace-body:fullscreen .call-presentation-stage {
  min-height: 0;
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

<style>
/* Final cascade guard for the meeting shell at 100% browser zoom. */
.chat-workspace .call-header + .call-workspace-body {
  display: grid !important;
  min-height: 0 !important;
  grid-template-rows: minmax(0, 1fr) auto !important;
  overflow: hidden !important;
}

/* Keep visual regions and the bottom interaction row in separate hit-test areas. */
.chat-workspace .call-header + .call-workspace-body:not(.is-presentation-mode) {
  grid-template-columns: minmax(0, 1fr) !important;
  grid-template-rows: minmax(0, 1fr) auto auto auto !important;
}

.chat-workspace .call-header + .call-workspace-body:not(.is-presentation-mode) .call-presentation-stage {
  grid-column: 1;
  grid-row: 1;
}

.chat-workspace .call-header + .call-workspace-body:not(.is-presentation-mode) .call-participant-rail {
  grid-column: 1;
  grid-row: 2;
}

.chat-workspace .call-header + .call-workspace-body:not(.is-presentation-mode) .call-transcript-panel {
  grid-column: 1;
  grid-row: 3;
}

.chat-workspace .call-header + .call-workspace-body:not(.is-presentation-mode) .call-controls-row {
  grid-column: 1;
  grid-row: 4;
}

.chat-workspace .call-header + .call-workspace-body.is-presentation-mode {
  grid-template-rows: minmax(220px, 1fr) auto auto !important;
}

.chat-workspace .call-controls-row {
  position: relative;
  z-index: 20;
  pointer-events: auto;
}

.chat-workspace .call-transcript-panel {
  position: relative;
  z-index: 1;
  pointer-events: auto;
}

.chat-workspace .call-live-caption-dock {
  pointer-events: none;
}

.chat-workspace .call-presentation-stage,
.chat-workspace .call-camera-stage,
.chat-workspace .call-camera-stage-tile video,
.chat-workspace .call-camera-off-state {
  min-height: 0 !important;
}

.chat-workspace .call-prejoin-preview,
.chat-workspace .call-prejoin-panel.is-camera-off .call-prejoin-preview {
  min-height: 0 !important;
  height: auto !important;
}

.chat-workspace .call-prejoin-camera-off,
.chat-workspace .call-prejoin-camera-off strong,
.chat-workspace .call-prejoin-camera-off span,
.chat-workspace .presentation-heading,
.chat-workspace .presentation-hint,
.chat-workspace .call-thumb-caption,
.chat-workspace .call-transcript-panel,
.chat-workspace .call-transcript-panel strong,
.chat-workspace .call-transcript-chunk p,
.chat-workspace .meeting-ai-summary,
.chat-workspace .meeting-ai-group ul {
  color: var(--meeting-fg-secondary) !important;
}

.chat-workspace .call-prejoin-camera-off strong,
.chat-workspace .call-transcript-panel strong,
.chat-workspace .call-live-caption-dock,
.chat-workspace .call-live-caption-copy {
  color: var(--meeting-fg) !important;
}

@media (max-height: 820px) and (min-width: 761px) {
  .chat-workspace .call-prejoin-panel { padding-block: 16px !important; }
  .chat-workspace .call-prejoin-settings { gap: 12px !important; }
  .chat-workspace .call-prejoin-group { gap: 7px !important; }
}
</style>

<style>
/* Meeting density and semantic contrast: keep the call surface usable at 100% zoom. */
.chat-workspace {
  --meeting-fg: #f4f8fb;
  --meeting-fg-secondary: #d2e0ea;
  --meeting-fg-muted: #a9bdca;
  --meeting-surface: #091522;
  --meeting-surface-raised: #112235;
}

.chat-workspace .call-header + .call-workspace-body {
  display: grid;
  flex: 1 1 auto;
  min-height: 0;
  grid-template-rows: minmax(0, 1fr) auto;
  gap: clamp(8px, 1.2vh, 14px);
  padding: clamp(10px, 1.35vw, 18px);
  overflow: hidden;
  background: var(--chat-bg) !important;
}

.chat-workspace .call-presentation-stage {
  min-height: 0;
  background: var(--meeting-surface);
  border-color: color-mix(in srgb, var(--meeting-fg-secondary) 22%, transparent);
  border-radius: 10px;
  box-shadow: none;
}

.chat-workspace .call-camera-stage {
  min-height: 0;
  padding: clamp(8px, 1vw, 12px);
  gap: clamp(8px, 1vw, 12px);
  background: var(--meeting-surface);
}

.chat-workspace .call-camera-stage-tile,
.chat-workspace .call-camera-off-state,
.chat-workspace .call-thumb-media {
  background: #0d1b2a;
}

.chat-workspace .call-camera-stage-tile video,
.chat-workspace .call-camera-off-state {
  min-height: 0;
}

.chat-workspace .presentation-heading,
.chat-workspace .call-camera-stage-label,
.chat-workspace .call-camera-stage-muted,
.chat-workspace .call-thumb-caption,
.chat-workspace .call-live-caption-dock,
.chat-workspace .call-live-caption-copy {
  color: var(--meeting-fg) !important;
}

.chat-workspace .presentation-hint,
.chat-workspace .call-thumb-caption > i,
.chat-workspace .call-transcript-title small,
.chat-workspace .meeting-ai-review-note,
.chat-workspace .meeting-ai-unavailable,
.chat-workspace .call-transcript-off p,
.chat-workspace .call-transcript-consent p {
  color: var(--meeting-fg-muted) !important;
}

.chat-workspace .call-transcript-panel,
.chat-workspace .call-chat-panel {
  background: var(--meeting-surface) !important;
  color: var(--meeting-fg) !important;
}

.chat-workspace .call-transcript-chunk p,
.chat-workspace .meeting-ai-summary,
.chat-workspace .meeting-ai-group ul,
.chat-workspace .call-consent-list > div span:first-child {
  color: var(--meeting-fg-secondary) !important;
}

.chat-workspace .call-transcript-chunk time,
.chat-workspace .meeting-ai-group small,
.chat-workspace .call-consent-list > div span:last-child {
  color: var(--meeting-fg-muted) !important;
}

.chat-workspace .call-control-dock {
  gap: clamp(5px, .7vw, 10px);
  padding: 7px 9px;
  border-color: color-mix(in srgb, var(--meeting-fg-secondary) 22%, transparent);
  border-radius: 10px;
  background: var(--meeting-surface-raised);
  box-shadow: none;
}

.chat-workspace .call-control-circle-btn,
.chat-workspace .call-control-label-btn,
.chat-workspace .presentation-control {
  min-height: 36px;
  color: var(--meeting-fg-secondary);
}

.chat-workspace .call-control-label-btn {
  min-width: 64px;
  height: 36px;
  border-radius: 7px;
}

.chat-workspace .call-prejoin-panel {
  width: min(calc(100% - 32px), 900px) !important;
  max-width: 900px !important;
  gap: clamp(14px, 2vh, 22px);
  padding: clamp(18px, 2.2vw, 28px) !important;
}

.chat-workspace .call-prejoin-copy h2 {
  font-size: clamp(21px, 2.2vw, 28px);
  line-height: 1.12;
}

.chat-workspace .call-prejoin-copy p,
.chat-workspace .call-prejoin-camera-off span,
.chat-workspace .call-prejoin-group-title,
.chat-workspace .call-prejoin-device-grid label {
  color: var(--meeting-fg-secondary) !important;
}

.chat-workspace .call-prejoin-layout {
  grid-template-columns: minmax(0, 1.08fr) minmax(250px, .92fr) !important;
  gap: clamp(16px, 2.4vw, 28px);
}

.chat-workspace .call-prejoin-preview {
  min-height: 0 !important;
  max-height: clamp(190px, 34vh, 320px) !important;
  aspect-ratio: 16 / 9 !important;
}

.chat-workspace .call-prejoin-panel.is-camera-off .call-prejoin-preview {
  min-height: 0 !important;
  max-height: clamp(170px, 30vh, 280px) !important;
  aspect-ratio: 16 / 9 !important;
}

.chat-workspace .call-prejoin-camera-off {
  color: var(--meeting-fg) !important;
}

.chat-workspace .call-prejoin-settings {
  gap: clamp(14px, 2vh, 20px);
}

.chat-workspace .call-prejoin-toggle,
.chat-workspace .call-prejoin-actions button {
  color: var(--meeting-fg) !important;
  background: var(--meeting-surface-raised);
  border-color: color-mix(in srgb, var(--meeting-fg-secondary) 30%, transparent);
}

.chat-workspace .call-prejoin-select-wrapper {
  background: var(--meeting-surface-raised) !important;
}

.chat-workspace .call-prejoin-select-wrapper select {
  color: var(--meeting-fg) !important;
  background: transparent !important;
}

.chat-workspace .call-prejoin-toggle.active {
  color: #d8fae8 !important;
}

@media (max-height: 820px) and (min-width: 761px) {
  .chat-workspace .call-prejoin-panel { padding-block: 16px; }
  .chat-workspace .call-prejoin-copy h2 { margin-block: 4px 6px; }
  .chat-workspace .call-prejoin-settings { gap: 12px; }
  .chat-workspace .call-prejoin-group { gap: 7px; }
  .chat-workspace .call-prejoin-toggle,
  .chat-workspace .call-prejoin-actions button { min-height: 36px; }
}

@media (max-width: 900px) and (min-width: 621px) {
  .chat-workspace .call-prejoin-layout { grid-template-columns: minmax(0, 1fr) minmax(220px, .9fr); }
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
  min-height: min(620px, calc(100dvh - 112px));
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
.call-transcript-panel { display: flex; width: 310px; min-width: 0; max-height: 190px; min-height: 0; flex-direction: column; gap: 10px; overflow-x: hidden; overflow-y: auto; padding: 14px; border-left: 1px solid rgba(148, 163, 184, .13); background: #091725; color: #e7f2fb; }
.call-transcript-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; padding-bottom: 2px; }
.call-transcript-title { display: grid; min-width: 0; gap: 3px; }
.call-transcript-header strong { display: block; font-size: 14px; letter-spacing: -.01em; }
.call-transcript-title small { overflow: hidden; color: #8295a6; font-size: 10px; text-overflow: ellipsis; white-space: nowrap; }
.call-ai-state-pill { border: 1px solid rgba(99, 210, 159, .35); border-radius: 999px; padding: 3px 7px; color: #8be5b8; font-size: 9px; line-height: 1.2; text-align: right; }
.call-ai-state-pill.is-off { border-color: rgba(148, 163, 184, .3); color: #b4c1cd; }
.call-ai-state-pill.is-paused_consent, .call-ai-state-pill.is-waiting_for_consent { border-color: rgba(245, 190, 91, .4); color: #f5c66e; }
.call-transcript-off, .call-transcript-consent, .call-transcript-active, .call-transcript-paused { border: 1px solid rgba(148, 163, 184, .14); border-radius: 10px; padding: 11px; background: rgba(255, 255, 255, .035); }
.call-transcript-off p, .call-transcript-consent p { margin: 6px 0 10px; color: #9fb0bf; font-size: 11px; line-height: 1.4; }
.meeting-ai-report { display: grid; gap: 10px; max-height: 280px; overflow: auto; border: 1px solid rgba(99, 210, 159, .2); border-radius: 10px; padding: 11px; background: rgba(99, 210, 159, .045); }
.meeting-ai-report-heading { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.meeting-ai-report-heading span, .meeting-ai-review-note, .meeting-ai-unavailable { color: #9fb0bf; font-size: 10px; line-height: 1.45; }
.meeting-ai-summary { margin: 0; color: #dbe8f2; font-size: 11px; line-height: 1.5; }
.meeting-ai-group { display: grid; gap: 4px; font-size: 11px; }
.meeting-ai-group ul { display: grid; gap: 4px; margin: 0; padding-left: 16px; color: #cbd9e4; }
.meeting-ai-group li { display: grid; gap: 2px; }
.meeting-ai-group small { color: #9fb0bf; }
.meeting-ai-unavailable { border: 1px dashed rgba(148, 163, 184, .2); border-radius: 8px; padding: 9px; }
.call-consent-list { display: grid; gap: 6px; margin-bottom: 10px; font-size: 11px; }
.call-consent-list > div { display: flex; justify-content: space-between; gap: 8px; }
.call-consent-list > div span:last-child { color: #aab9c5; }
.call-consent-actions { display: flex; gap: 7px; }
.call-transcript-indicator { display: flex; align-items: center; gap: 7px; margin-bottom: 9px; color: #8be5b8; font-size: 11px; }
.call-transcript-indicator span { width: 7px; height: 7px; border-radius: 50%; background: #55d994; box-shadow: 0 0 0 4px rgba(85, 217, 148, .12); }
.call-transcript-list { display: flex; min-height: 0; max-height: 240px; flex: 1 1 auto; flex-direction: column; gap: 12px; overflow: auto; padding-top: 3px; }
.call-transcript-list:empty { min-height: 0; }
.call-transcript-chunk { border-bottom: 1px solid rgba(148, 163, 184, .1); padding-bottom: 9px; }
.call-transcript-chunk > div { display: flex; align-items: center; gap: 7px; color: #8be5b8; font-size: 10px; }
.call-transcript-chunk time { color: #8295a6; }
.call-transcript-chunk p { margin: 5px 0 0; color: #d4e1eb; font-size: 12px; line-height: 1.45; }
.call-transcript-chunk.is-interim { opacity: .7; border-bottom-style: dashed; }
@media (max-width: 1180px) { .call-transcript-panel { width: 260px; min-width: 260px; } }
@media (max-width: 900px) { .call-transcript-panel { width: 100%; min-width: 0; border-left: 0; border-top: 1px solid rgba(148, 163, 184, .13); } }
.message-card { position: relative; }
.message-card:hover .message-actions, .message-card:focus-within .message-actions { opacity: 1; transform: translateY(0); }
.message-focus-target { background: rgba(99, 210, 159, .12); box-shadow: inset 3px 0 #63d29f; transition: background 180ms ease-out, box-shadow 180ms ease-out; }
.message-actions { display: flex; gap: 3px; margin-top: 7px; opacity: 0; transform: translateY(2px); transition: opacity 160ms ease-out, transform 160ms ease-out; }
.message-action-btn { border: 1px solid rgba(148, 163, 184, .14); border-radius: 5px; background: rgba(15, 28, 43, .86); color: #9bb0c2; cursor: pointer; font-size: 12px; min-width: 26px; height: 24px; }
.message-action-btn:hover, .message-action-btn:focus-visible { border-color: rgba(99, 210, 159, .5); color: #e7f2fb; background: rgba(99, 210, 159, .11); }
.message-reactions { display: flex; flex-wrap: wrap; gap: 5px; margin-top: 8px; }
.reaction-chip { display: inline-flex; gap: 5px; align-items: center; border: 1px solid rgba(148, 163, 184, .2); border-radius: 12px; padding: 3px 8px; background: rgba(17, 31, 47, .78); color: #c8d7e4; cursor: pointer; font-size: 11px; }
.reaction-chip.active { border-color: rgba(99, 210, 159, .75); color: #9af0c5; background: rgba(99, 210, 159, .12); }
.message-reply-quote { display: flex; flex-direction: column; align-items: flex-start; max-width: 100%; margin: 5px 0 7px; border: 0; border-left: 2px solid rgba(99, 210, 159, .65); padding: 3px 8px; background: transparent; color: #8ea4b7; cursor: pointer; text-align: left; }
.message-reply-quote:hover { color: #d5e5f0; }
.reply-quote-label { color: #83dcae; font-size: 10px; font-weight: 700; }
.reply-quote-content { overflow: hidden; max-width: 100%; text-overflow: ellipsis; white-space: nowrap; font-size: 11px; }
.reply-composer-strip { display: flex; align-items: center; justify-content: space-between; gap: 10px; margin: 0 0 7px; border-left: 2px solid #63d29f; padding: 6px 9px; background: rgba(99, 210, 159, .08); color: #a9bdcc; font-size: 11px; }
.reply-composer-strip div { display: flex; min-width: 0; flex-direction: column; gap: 2px; }
.reply-composer-strip strong { overflow: hidden; color: #e7f2fb; font-size: 12px; text-overflow: ellipsis; white-space: nowrap; }
.channel-utility-drawer { display: flex; width: 284px; min-width: 284px; flex-direction: column; border-left: 1px solid rgba(148, 163, 184, .12); background: #091725; }
.channel-utility-header, .call-chat-panel-header { display: flex; align-items: center; justify-content: space-between; gap: 10px; padding: 16px; border-bottom: 1px solid rgba(148, 163, 184, .12); }
.channel-utility-header h3 { margin: 3px 0 0; color: #e7f2fb; font-size: 15px; }
.channel-search-box { display: flex; gap: 7px; padding: 12px; border-bottom: 1px solid rgba(148, 163, 184, .1); }
.channel-search-box input, .call-chat-composer input { flex: 1; min-width: 0; border: 1px solid rgba(148, 163, 184, .18); border-radius: 6px; outline: none; padding: 8px 10px; background: rgba(255, 255, 255, .04); color: #e7f2fb; font-size: 12px; }
.channel-search-box input:focus, .call-chat-composer input:focus { border-color: rgba(99, 210, 159, .7); }
.channel-utility-list { display: flex; min-height: 0; flex: 1; flex-direction: column; gap: 7px; overflow-y: auto; padding: 10px; }
.channel-utility-item { display: flex; flex-direction: column; align-items: flex-start; gap: 3px; border: 1px solid rgba(148, 163, 184, .12); border-radius: 7px; padding: 9px; background: rgba(255, 255, 255, .025); color: #a8bac8; cursor: pointer; text-align: left; }
.channel-utility-item:hover { border-color: rgba(99, 210, 159, .38); background: rgba(99, 210, 159, .07); }
.channel-utility-item strong { color: #e7f2fb; font-size: 11px; }
.channel-utility-item span { display: -webkit-box; overflow: hidden; -webkit-box-orient: vertical; -webkit-line-clamp: 3; font-size: 12px; line-height: 1.35; }
.channel-utility-item small, .channel-utility-empty { color: #71899b; font-size: 10px; }
.call-chat-panel { display: flex; width: 290px; min-width: 290px; flex-direction: column; border-left: 1px solid rgba(148, 163, 184, .13); background: #091725; }
.call-workspace-body { position: relative; }
.call-chat-panel { position: absolute; top: 0; right: 0; bottom: 0; z-index: 4; }
.call-chat-panel, .call-chat-panel * { pointer-events: auto; }
.call-workspace-body.has-call-side-panel { padding-right: 396px !important; }
.call-panel-tabs { flex: 0 0 auto; }
.call-panel-participants { min-height: 0; flex: 1; overflow-y: auto; padding: 10px; }
.call-panel-participants .context-call-summary { margin: 0 0 10px; }
.call-fullscreen-panel { position: absolute; z-index: 8; top: 0; right: 0; bottom: 0; display: flex; width: min(300px, calc(100vw - 24px)); flex-direction: column; border-left: 1px solid rgba(148, 163, 184, .13); background: #091725; }
.call-chat-panel-header strong { display: block; margin-top: 3px; color: #e7f2fb; font-size: 13px; }
.call-chat-thread { display: flex; min-height: 0; flex: 1; flex-direction: column; gap: 9px; overflow-y: auto; padding: 13px; }
.call-chat-message { display: flex; flex-direction: column; gap: 2px; }
.call-chat-message strong { color: #94e5ba; font-size: 11px; }
.call-chat-message span { color: #d0dce5; font-size: 12px; line-height: 1.4; word-break: break-word; }
.call-chat-message small { color: #6e8596; font-size: 9px; }
.call-chat-composer { display: flex; gap: 6px; padding: 10px; border-top: 1px solid rgba(148, 163, 184, .12); }
.call-chat-composer button { width: 31px; border: 0; border-radius: 6px; background: #63d29f; color: #06131c; cursor: pointer; }
.call-chat-composer button:disabled { cursor: not-allowed; opacity: .45; }
.call-workspace-body.is-presentation-mode {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(190px, 240px);
  grid-template-rows: minmax(0, 1fr) auto auto;
  align-items: stretch;
}
.call-workspace-body.is-presentation-mode .call-presentation-stage { grid-column: 1; grid-row: 1; min-height: 0; }
.call-workspace-body.is-presentation-mode .call-participant-rail {
  grid-column: 2;
  grid-row: 1;
  display: flex;
  min-height: 0;
  max-height: none;
  flex-direction: column;
  overflow-y: auto;
  padding-right: 2px;
}
.call-workspace-body.is-presentation-mode .call-participant-thumb { flex: 0 0 auto; }
.call-workspace-body.is-presentation-mode .call-transcript-panel {
  grid-column: 1 / -1;
  grid-row: 2;
  width: auto;
  min-width: 0;
  max-height: 170px;
  overflow: auto;
  border-top: 1px solid rgba(148, 163, 184, .13);
  border-left: 0;
}
.call-workspace-body:not(.is-presentation-mode) .call-transcript-panel {
  justify-self: end;
}
.call-workspace-body.is-presentation-mode .call-controls-row { grid-column: 1 / -1; grid-row: 3; }
.call-camera-stage.layout-camera_focus { grid-template-columns: minmax(0, min(760px, 100%)); justify-content: center; }
.call-camera-stage.layout-camera_grid { grid-template-columns: repeat(auto-fit, minmax(min(280px, 100%), 1fr)); }
.call-control-future-slot:disabled { cursor: not-allowed; opacity: .6; }
.call-reaction-overlay { position: absolute; z-index: 12; right: 18px; bottom: 92px; display: flex; flex-direction: column; align-items: flex-end; gap: 6px; pointer-events: none; }
.call-reaction-bubble { padding: 6px 10px; border: 1px solid rgba(255,255,255,.12); border-radius: 999px; background: rgba(15,23,42,.9); color: #fff; box-shadow: 0 8px 24px rgba(0,0,0,.2); animation: call-reaction-rise 4s ease-out both; }
.call-reaction-bubble small { margin-left: 4px; color: #b8c6d4; font-size: 10px; }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0,0,0,0); white-space: nowrap; border: 0; }
@keyframes call-reaction-rise { from { opacity: 0; transform: translateY(8px) scale(.92); } 12%, 78% { opacity: 1; transform: translateY(0) scale(1); } to { opacity: 0; transform: translateY(-12px) scale(1.04); } }
.call-control-circle-btn:focus-visible,
.call-control-label-btn:focus-visible,
.presentation-control:focus-visible,
.call-participant-thumb:focus-visible { outline: 2px solid #63d29f; outline-offset: 3px; }
@media (max-width: 900px) {
  .call-workspace-body.has-call-side-panel { padding-right: 18px; }
  .call-workspace-body.is-presentation-mode { display: flex; flex-direction: column; }
  .call-workspace-body.is-presentation-mode .call-presentation-stage { min-height: 240px; }
  .call-workspace-body.is-presentation-mode .call-participant-rail {
    display: flex;
    max-height: none;
    flex-direction: row;
    overflow-x: auto;
    overflow-y: hidden;
  }
  .call-workspace-body.is-presentation-mode .call-participant-thumb { width: 170px; min-width: 170px; }
  .call-workspace-body.is-presentation-mode .call-transcript-panel { max-height: 190px; }
}
@media (max-width: 560px) {
  .call-camera-stage { grid-template-columns: 1fr; min-height: 240px; padding: 8px; }
  .call-camera-stage[data-participant-count] { grid-template-columns: 1fr; }
  .call-camera-stage[data-participant-count="3"] .call-camera-stage-tile:last-child { grid-column: auto; width: auto; }
  .call-camera-stage-tile video { min-height: 210px; }
  .call-control-dock { max-width: 100%; overflow-x: auto; }
  .call-control-future-slot span { display: none; }
}
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
.ai-text-only-notice, .ai-off-state-panel, .ai-loading-state, .ai-error-state { padding: 14px 16px 0; }
.ai-text-only-notice strong, .ai-error-state strong { color: #f0c58a; font-size: 12px; }
.ai-text-only-notice p, .ai-off-state-panel p, .ai-loading-state small, .ai-error-state p { padding: 0; margin: 8px 0 0; }
.ai-off-banner { display: flex; align-items: center; gap: 8px; margin: 14px 16px 0; color: #dcebf3; font-size: 12px; }
.ai-state-indicator { background: #657a8b; box-shadow: none; }
.ai-primary-action, .ai-secondary-action { display: inline-flex; align-items: center; justify-content: center; min-height: 32px; padding: 7px 11px; border-radius: 7px; font-size: 11px; font-weight: 700; cursor: pointer; transition: background-color 160ms ease-out, color 160ms ease-out, transform 120ms ease-out; }
.ai-primary-action { margin: 14px 16px 0; border: 1px solid #63d29f; background: #63d29f; color: #06131c; }
.ai-primary-action:hover { background: #83e5b7; }
.ai-primary-action:active, .ai-secondary-action:active { transform: scale(.97); }
.ai-primary-action:disabled { cursor: not-allowed; opacity: .5; }
.ai-secondary-action { margin-top: 12px; border: 1px solid rgba(99, 210, 159, .45); background: transparent; color: #a9e7c8; }
.ai-loading-state { display: flex; flex-direction: column; gap: 7px; color: #dcebf3; }
.ai-loading-line { width: 35%; height: 3px; background: #63d29f; border-radius: 3px; }
.ai-result-content { max-height: min(640px, calc(100vh - 150px)); overflow-y: auto; padding: 0 16px; }
.ai-result-meta { display: flex; align-items: center; justify-content: space-between; gap: 8px; margin: 12px 0 4px; color: #7f9aac; font-size: 10px; }
.ai-inline-action { border: 0; background: transparent; color: #8edeb7; cursor: pointer; font-size: 10px; }
.ai-result-section { padding: 11px 0; border-top: 1px solid rgba(148, 163, 184, .12); }
.ai-result-section h4 { margin: 0 0 5px; color: #e8f4fc; font-size: 11px; }
.ai-result-section p { padding: 0; margin: 0; }
.ai-result-item { padding: 7px 0; border-top: 1px solid rgba(148, 163, 184, .08); }
.ai-result-item:first-of-type { border-top: 0; }
.ai-result-item small, .ai-muted-copy { color: #8299ab; font-size: 10px; }
.ai-evidence-row { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 5px; padding: 0 16px; }
.ai-evidence-link { padding: 3px 6px; border: 1px solid rgba(88, 183, 232, .35); border-radius: 5px; background: rgba(88, 183, 232, .08); color: #a8ddf4; cursor: pointer; font-size: 9px; }
.ai-evidence-link:hover, .ai-evidence-link:focus-visible { background: rgba(88, 183, 232, .18); color: #e5f7ff; }
.ai-question-section form { display: flex; gap: 6px; padding: 0 16px; }
.ai-question-section input { min-width: 0; flex: 1; padding: 7px 8px; border: 1px solid rgba(148, 163, 184, .22); border-radius: 6px; background: #0b1929; color: #e8f4fc; font-size: 11px; }
.ai-question-section form .ai-primary-action { margin: 0; white-space: nowrap; }
.ai-answer { margin-top: 9px; padding: 8px; border-left: 2px solid #63d29f; background: rgba(99, 210, 159, .05); }
.ai-answer strong { display: block; padding: 0 8px; color: #dcebf3; font-size: 10px; }
.ai-answer p { padding: 0 8px; margin-top: 4px; }

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
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  max-height: 140px;
  overflow-y: auto;
  padding: 8px 12px;
  border-bottom: 1px solid var(--chat-line, var(--color-border));
}

.image-attachment img:hover {
  transform: scale(1.02);
}

.attached-file-preview-bar {
  display: inline-flex;
  align-items: center;
  background-color: var(--chat-surface-2, color-mix(in srgb, var(--color-primary) 5%, var(--color-surface)));
  border-radius: 10px;
  padding: 6px 10px;
  border: 1px solid var(--chat-line, var(--color-border));
  gap: 8px;
  max-width: 260px;
}

.attached-file-preview-bar .truncate {
  max-width: 140px;
}

.selected-file-thumbnail {
  width: 56px;
  height: 56px;
  flex: 0 0 auto;
  border-radius: 10px;
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
/* Teleported call menu: keep it above the meeting shell and bounded by the viewport. */
.call-more-menu-popper.el-popover.el-popper {
  z-index: var(--z-popover) !important;
  box-sizing: border-box;
  max-width: calc(100vw - 24px);
  overflow: visible !important;
  padding: 8px !important;
  color: var(--color-text-primary, #0f172a) !important;
  background: var(--color-surface, #ffffff) !important;
  border-color: var(--color-border, #e2e8f0) !important;
  box-shadow: var(--shadow-popover, 0 16px 32px rgb(2 6 23 / 0.35)) !important;
}

.call-more-menu-popper .call-more-menu {
  position: static;
  width: auto;
  padding: 0;
  border: 0;
  border-radius: 0;
  background: transparent;
  box-shadow: none;
}

.call-more-menu-popper .call-more-menu-item,
.call-more-menu-popper .call-more-menu-back,
.call-more-menu-popper .call-device-option {
  color: var(--color-text-primary, #0f172a);
}

.call-more-menu-popper .call-more-menu-item:hover,
.call-more-menu-popper .call-more-menu-item:focus-visible,
.call-more-menu-popper .call-more-menu-back:hover,
.call-more-menu-popper .call-more-menu-back:focus-visible,
.call-more-menu-popper .call-device-option:hover,
.call-more-menu-popper .call-device-option:focus-visible,
.call-more-menu-popper .call-device-option.selected {
  background: var(--color-surface-hover, #f1f5f9);
  color: var(--color-text-primary, #0f172a);
}

.call-more-menu-popper .call-more-menu-item small,
.call-more-menu-popper .call-more-section-label,
.call-more-menu-popper .call-more-empty {
  color: var(--color-text-muted, #64748b);
}

.call-more-menu-popper .call-reaction-option {
  background: var(--color-surface-hover, #f1f5f9);
  border-color: var(--color-border, #e2e8f0);
}

.call-more-menu-popper .call-device-select select {
  width: 100%;
  min-width: 0;
  max-width: 100%;
  box-sizing: border-box;
}

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
  background-color: var(--color-surface) !important;
  border-radius: 12px !important;
  border: 1px solid var(--color-border) !important;
  box-shadow: var(--shadow-xl, 0 24px 50px rgba(15, 23, 42, 0.18)) !important;
  overflow: hidden;
}
.group-call-dialog.video-call-dialog .el-dialog__header {
  background-color: var(--color-surface) !important;
  border-bottom: 1px solid var(--color-border) !important;
  padding: 14px 20px !important;
  margin-right: 0 !important;
}
.group-call-dialog.video-call-dialog .el-dialog__headerbtn .el-dialog__close {
  color: var(--color-text-secondary) !important;
}
.group-call-dialog.video-call-dialog .el-dialog__headerbtn:hover .el-dialog__close {
  color: var(--color-text-primary) !important;
}
.group-call-dialog.video-call-dialog .el-dialog__body {
  background-color: var(--color-surface) !important;
  padding: 16px !important;
}
.group-call-dialog.video-call-dialog .el-dialog__footer {
  background-color: var(--color-surface-hover) !important;
  border-top: 1px solid var(--color-border) !important;
  padding: 12px 20px !important;
}
</style>

<style scoped>
/* Final chat chrome pass: this sits after legacy call styles so theme tokens win. */
.chat-workspace {
  --chat-bg: var(--color-bg, #f8fafc);
  --chat-surface: var(--color-surface, #ffffff);
  --chat-surface-2: var(--color-surface-hover, #f1f5f9);
  --chat-ink: var(--color-text-primary, #0f172a);
  --chat-muted: var(--color-text-secondary, #475569);
  --chat-faint: var(--color-text-muted, #64748b);
  --chat-line: var(--color-border, rgba(148, 163, 184, 0.24));
  --chat-accent: var(--color-accent, #0ea5e9);
  --chat-accent-hover: var(--color-accent-hover, #0284c7);
  --chat-accent-soft: color-mix(in srgb, var(--chat-accent) 12%, var(--chat-surface));
  --chat-context-width: 288px;
  background: var(--chat-bg) !important;
  color: var(--chat-ink) !important;
  border-color: var(--chat-line) !important;
  box-shadow: 0 18px 48px color-mix(in srgb, var(--chat-ink) 12%, transparent) !important;
}
:global([data-theme='dark'] .chat-workspace) {
  --chat-bg: #07111e;
  --chat-surface: #0f172a;
  --chat-surface-2: #1e293b;
  --chat-ink: #f8fafc;
  --chat-muted: #cbd5e1;
  --chat-faint: #94a3b8;
  --chat-line: rgba(148, 163, 184, 0.18);
  --chat-accent: #38bdf8;
  --chat-accent-hover: #0ea5e9;
  --chat-accent-soft: color-mix(in srgb, var(--chat-accent) 16%, var(--chat-surface));
}
.chat-workspace .server-bar { background: var(--chat-surface) !important; border-right: 1px solid var(--chat-line) !important; padding-block: 14px !important; width: 64px !important; }
.chat-workspace .rail-caption { color: var(--chat-faint) !important; font-weight: 800 !important; font-size: 10px !important; letter-spacing: .08em !important; text-align: center; }
.chat-workspace .server-icon-wrapper { position: relative !important; display: flex !important; align-items: center !important; justify-content: center !important; width: 100% !important; height: 48px !important; margin-bottom: 4px !important; appearance: none; border: 0; padding: 0 0 0 6px !important; background: transparent; color: inherit; cursor: pointer; }
.chat-workspace .server-icon { width: 40px !important; height: 40px !important; min-width: 40px !important; min-height: 40px !important; display: flex !important; align-items: center !important; justify-content: center !important; border: 1px solid var(--chat-line) !important; background: var(--chat-surface-2) !important; color: var(--chat-ink) !important; border-radius: 12px !important; font-weight: 800 !important; transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1) !important; margin: 0 auto !important; }
.chat-workspace .server-icon-wrapper:hover .server-icon { border-color: var(--chat-accent) !important; background: color-mix(in srgb, var(--chat-accent) 14%, var(--chat-surface-2)) !important; color: var(--chat-accent) !important; transform: translateY(-1px); }
.chat-workspace .server-icon-wrapper.active .server-icon { background: linear-gradient(135deg, #0ea5e9, #3b82f6) !important; color: #ffffff !important; border-color: transparent !important; border-radius: 12px !important; box-shadow: 0 6px 16px color-mix(in srgb, #3b82f6 38%, transparent) !important; }
.chat-workspace .server-icon-wrapper:focus-visible { outline: 2px solid var(--chat-accent); outline-offset: 3px; }
.chat-workspace .active-indicator { position: absolute !important; left: 0 !important; top: 50% !important; transform: translateY(-50%) scaleX(0) !important; width: 4px !important; height: 22px !important; border-radius: 0 4px 4px 0 !important; background: var(--chat-accent) !important; transition: transform 0.2s cubic-bezier(0.4, 0, 0.2, 1) !important; transform-origin: left center !important; }
.chat-workspace .server-icon-wrapper.active .active-indicator { transform: translateY(-50%) scaleX(1) !important; }
.chat-workspace .chat-sidebar { background: var(--chat-surface) !important; border-right-color: var(--chat-line) !important; }
.chat-workspace .sidebar-header { border-bottom-color: var(--chat-line) !important; }
.chat-workspace .workspace-mark { background: linear-gradient(135deg, #0ea5e9, #3b82f6) !important; color: #ffffff !important; border-radius: 8px !important; font-weight: 900 !important; }
.chat-workspace .workspace-back-button { min-height: 40px; padding: 0 8px; color: var(--chat-muted) !important; }
.chat-workspace .workspace-back-button:hover { background: var(--chat-surface-2); color: var(--chat-ink) !important; }
.chat-workspace .sidebar-section { margin-bottom: 18px; padding: 0; border: 0; border-radius: 0; background: transparent !important; }
.chat-workspace .section-header { min-height: 30px; margin-bottom: 6px !important; }
.chat-workspace .section-title { color: var(--chat-faint) !important; font-size: 10.5px !important; font-weight: 800 !important; letter-spacing: .08em !important; }
.chat-workspace .add-btn-small { width: 32px; height: 32px; border-radius: 8px !important; border: 1px solid var(--chat-line) !important; background: var(--chat-surface-2) !important; color: var(--chat-muted) !important; }
.chat-workspace .add-btn-small:hover { border-color: var(--chat-accent) !important; background: color-mix(in srgb, var(--chat-accent) 14%, var(--chat-surface)) !important; color: var(--chat-accent) !important; }
.chat-workspace .section-list { gap: 3px; }
.chat-workspace .list-item { min-height: 38px; padding: 8px 12px; border-radius: 10px; color: var(--chat-muted) !important; font-weight: 600; transition: all 0.16s ease !important; border-left: 0 !important; }
.chat-workspace .list-item:hover { background: var(--chat-surface-2) !important; color: var(--chat-ink) !important; }
.chat-workspace .list-item.active { background: color-mix(in srgb, var(--chat-accent) 18%, var(--chat-surface-2)) !important; color: var(--chat-accent) !important; box-shadow: none !important; font-weight: 800 !important; border-radius: 10px !important; }
.chat-workspace .item-icon { width: 18px; margin-right: 0; color: var(--chat-faint) !important; text-align: center; }
.chat-workspace .voice-item .item-icon { color: var(--color-success) !important; }
.chat-workspace .direct-item { gap: 7px; }
.chat-workspace .direct-item .el-avatar { flex: 0 0 auto; }
.chat-workspace .presence-dot { width: 7px; height: 7px; box-shadow: none !important; border: 2px solid var(--chat-surface); }
.chat-workspace .connected-voice-panel { border: 1px solid color-mix(in srgb, var(--color-success) 35%, var(--chat-line)) !important; background: color-mix(in srgb, var(--color-success) 10%, var(--chat-surface)) !important; border-radius: 12px !important; margin: 10px !important; padding: 10px 12px !important; }
.chat-workspace .chat-main { background: var(--chat-bg) !important; }
.chat-workspace .chat-header { min-height: 64px; padding: 12px 18px !important; border-bottom: 1px solid var(--chat-line) !important; background: var(--chat-surface) !important; }
.chat-workspace .active-info { gap: 10px; }
.chat-workspace .active-icon { width: 34px; height: 34px; background: var(--chat-accent-soft) !important; color: var(--chat-accent) !important; border-radius: 8px; }
.chat-workspace .chat-header h4 { color: var(--chat-ink) !important; font-size: 14px; font-weight: 700; }
.chat-workspace .chat-header p { color: var(--chat-muted) !important; }
.chat-workspace .header-actions { gap: 6px; display: flex; align-items: center; }
.chat-workspace .ai-action-btn { background: color-mix(in srgb, var(--chat-accent) 14%, var(--chat-surface-2)) !important; border: 1px solid color-mix(in srgb, var(--chat-accent) 30%, var(--chat-line)) !important; color: var(--chat-accent) !important; font-size: 15px !important; display: inline-flex !important; align-items: center !important; justify-content: center !important; }
.chat-workspace .ai-action-btn:hover { background: color-mix(in srgb, var(--chat-accent) 24%, var(--chat-surface-2)) !important; border-color: var(--chat-accent) !important; }
.chat-workspace .action-btn { width: 38px; height: 38px; border: 1px solid var(--chat-line); border-radius: 10px; background: var(--chat-surface-2); color: var(--chat-muted) !important; }
.chat-workspace .action-btn:hover { border-color: var(--chat-accent); background: var(--chat-accent-soft) !important; color: var(--chat-accent) !important; }
.chat-workspace .ai-entry-button { min-height: 38px; border-color: color-mix(in srgb, var(--chat-accent) 28%, var(--chat-line)) !important; border-radius: 10px; background: var(--chat-accent-soft) !important; color: var(--chat-accent) !important; font-weight: 700; }
.chat-workspace .ai-entry-button:hover { background: color-mix(in srgb, var(--chat-accent) 18%, var(--chat-surface)) !important; }
.mobile-sidebar-trigger { display: none; }
.chat-content-split { display: flex; flex: 1; min-height: 0; width: 100%; }
.chat-thread-column { display: flex; flex: 1; min-width: 0; height: 100%; flex-direction: column; }
.chat-workspace .messages-thread { padding: 18px 24px; gap: 4px; background: var(--chat-bg) !important; }
.chat-workspace .message-card {
  display: flex;
  gap: 10px;
  max-width: 75%;
  padding: 6px 0;
  border-bottom: 0 !important;
  transition: background 0.16s ease;
  align-self: flex-start;
}
.chat-workspace .message-card:hover { background: transparent !important; }

.chat-workspace .message-content-wrapper {
  display: flex;
  flex-direction: column;
  max-width: 100%;
  position: relative;
}

.chat-workspace .message-body {
  padding: 10px 14px;
  border-radius: 14px 14px 14px 2px;
  background: var(--chat-surface-2, rgba(30, 41, 59, 0.7));
  border: 1px solid var(--chat-line);
  color: var(--chat-ink) !important;
  line-height: 1.5;
  font-size: 13.5px;
  word-break: break-word;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.06);
}

.chat-workspace .message-card.is-own,
.chat-workspace .message-card.mine {
  align-self: flex-end !important;
  flex-direction: row-reverse !important;
  margin-left: auto !important;
}

.chat-workspace .message-card.is-own .message-header-line,
.chat-workspace .message-card.mine .message-header-line {
  justify-content: flex-end;
}

.chat-workspace .message-card.is-own .message-content-wrapper,
.chat-workspace .message-card.mine .message-content-wrapper {
  align-items: flex-end;
}

.chat-workspace .message-card.is-own .message-body,
.chat-workspace .message-card.mine .message-body {
  background: linear-gradient(135deg, #0ea5e9, #2563eb) !important;
  color: #ffffff !important;
  border: 1px solid rgba(56, 189, 248, 0.4) !important;
  border-radius: 14px 14px 2px 14px;
  box-shadow: 0 4px 12px rgba(37, 99, 235, 0.25);
}

.chat-workspace .message-body.is-audio-only,
.chat-workspace .message-card.is-own .message-body.is-audio-only,
.chat-workspace .message-card.mine .message-body.is-audio-only,
.chat-workspace .message-card.is-audio-only .message-body,
.chat-workspace .message-card.is-audio-only.is-own .message-body,
.chat-workspace .message-card.is-audio-only.mine .message-body,
.chat-workspace .message-card.is-audio-only .message-content,
.chat-workspace .message-card.is-audio-only.mine .message-content,
.chat-workspace .message-card.is-audio-only .message-bubble-wrapper,
.chat-workspace .call-msg-bubble.is-audio-only,
.chat-workspace .call-chat-message.is-audio-only .call-msg-bubble,
.chat-workspace .call-chat-message.is-audio-only.is-own .call-msg-bubble,
.message-body.is-audio-only,
.message-card.is-audio-only .message-body,
.message-card.is-audio-only .message-content,
.call-msg-bubble.is-audio-only,
div.message-body.is-audio-only,
div.message-card.mine div.message-body.is-audio-only,
div.message-card.is-own div.message-body.is-audio-only {
  background: transparent !important;
  background-color: transparent !important;
  border: none !important;
  border-color: transparent !important;
  box-shadow: none !important;
  padding: 0 !important;
}

.chat-workspace .message-card.is-own .message-body .message-text-segment,
.chat-workspace .message-card.mine .message-body .message-text-segment {
  color: #ffffff !important;
}

.chat-workspace .sender-name { color: var(--chat-ink) !important; font-weight: 700; }
.chat-workspace .message-time, .chat-workspace .send-time { color: var(--chat-faint) !important; font-weight: 500; }
.chat-workspace .message-bubble-wrapper {
  position: relative;
  display: inline-flex;
  max-width: 100%;
}

.chat-workspace .zalo-hover-toolbar {
  display: flex;
  align-items: center;
  gap: 5px;
  opacity: 0;
  visibility: hidden;
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  transition: opacity 0.18s ease, visibility 0.18s ease;
  z-index: 25;
}

.chat-workspace .message-card:hover .zalo-hover-toolbar,
.chat-workspace .message-bubble-wrapper:hover .zalo-hover-toolbar,
.chat-workspace .call-chat-message:hover .zalo-hover-toolbar {
  opacity: 1;
  visibility: visible;
}

/* Position for own message (right aligned) -> snug right next to the chat bubble (8px) */
.chat-workspace .message-card.is-own .zalo-hover-toolbar,
.chat-workspace .message-card.mine .zalo-hover-toolbar,
.chat-workspace .call-chat-message.is-own .zalo-hover-toolbar,
.chat-workspace .call-chat-message.mine .zalo-hover-toolbar {
  right: calc(100% + 8px);
  left: auto;
  flex-direction: row-reverse;
}

/* Position for other's message (left aligned) -> snug right next to the chat bubble (8px) */
.chat-workspace .message-card:not(.is-own):not(.mine) .zalo-hover-toolbar,
.chat-workspace .call-chat-message:not(.is-own):not(.mine) .zalo-hover-toolbar {
  left: calc(100% + 8px);
  right: auto;
  flex-direction: row;
}

.chat-workspace .zalo-action-buttons {
  display: flex;
  align-items: center;
  gap: 8px;
}

.chat-workspace .zalo-reaction-trigger-wrapper {
  position: relative;
  display: inline-flex;
  align-items: center;
}

/* Transparent hover bridge bridging the gap between like button & emoji bar */
.chat-workspace .zalo-reaction-trigger-wrapper::after {
  content: '';
  position: absolute;
  bottom: 100%;
  left: -24px;
  right: -24px;
  height: 18px;
  display: block;
}

/* Emoji reaction bar - hidden by default, pops up on hovering the like button */
.chat-workspace .zalo-emoji-bar {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 5px 12px;
  background: var(--chat-surface-2, rgba(22, 30, 46, 0.96)) !important;
  border: 1px solid var(--chat-line, rgba(255, 255, 255, 0.15)) !important;
  border-radius: 999px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.35);
  backdrop-filter: blur(12px);
  position: absolute;
  bottom: calc(100% + 8px);
  left: 50%;
  transform: translateX(-50%) translateY(4px);
  opacity: 0;
  visibility: hidden;
  pointer-events: none;
  transition: opacity 0.2s cubic-bezier(0.16, 1, 0.3, 1), 
              visibility 0.2s ease, 
              transform 0.2s cubic-bezier(0.16, 1, 0.3, 1);
  z-index: 40;
  white-space: nowrap;
}

/* Keep emoji bar open when hovering wrapper OR hovering emoji bar itself */
.chat-workspace .zalo-reaction-trigger-wrapper:hover .zalo-emoji-bar,
.chat-workspace .zalo-emoji-bar:hover {
  opacity: 1;
  visibility: visible;
  pointer-events: auto;
  transform: translateX(-50%) translateY(0);
}

.chat-workspace .zalo-emoji-btn {
  background: transparent;
  border: none;
  font-size: 20px;
  cursor: pointer;
  padding: 2px 4px;
  border-radius: 50%;
  transition: transform 0.18s cubic-bezier(0.175, 0.885, 0.32, 1.275);
  line-height: 1;
}

.chat-workspace .zalo-emoji-btn:hover {
  transform: scale(1.35);
}

.chat-workspace .zalo-circle-btn {
  width: 32px;
  height: 32px;
  border-radius: 50% !important;
  background: var(--chat-surface-2, #ffffff) !important;
  border: 1px solid var(--chat-line, rgba(255, 255, 255, 0.12)) !important;
  color: var(--chat-ink, #475569) !important;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.12);
  transition: all 0.18s ease;
}

.chat-workspace .zalo-circle-btn:hover {
  background: var(--chat-accent-soft, #e0f2fe) !important;
  color: var(--chat-accent, #0284c7) !important;
  border-color: var(--chat-accent) !important;
  transform: translateY(-2px);
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.18);
}

.zalo-menu-dropdown {
  border-radius: 12px !important;
  padding: 6px !important;
  min-width: 180px;
}

.chat-workspace .message-reactions {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 6px;
}

.chat-workspace .reaction-chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  min-height: 26px;
  padding: 2px 8px;
  border-radius: 12px;
  border: 1px solid var(--chat-line) !important;
  background: var(--chat-surface-2) !important;
  color: var(--chat-ink) !important;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.15s ease;
}

.chat-workspace .reply-composer-strip {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  padding: 8px 12px;
  margin-bottom: 10px;
  background: var(--chat-surface-2, rgba(30, 41, 59, 0.6)) !important;
  border-left: 3.5px solid var(--chat-accent, #0ea5e9) !important;
  border-radius: 8px;
  color: var(--chat-ink) !important;
  width: 100%;
}

.chat-workspace .reply-content-box {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  text-align: left;
  gap: 3px;
  min-width: 0;
  flex: 1;
}

.chat-workspace .reply-header-row {
  display: flex;
  align-items: center;
  font-size: 13px;
  color: var(--chat-ink) !important;
  text-align: left;
  line-height: 1.4;
}

.chat-workspace .reply-header-text {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  white-space: nowrap;
}

.chat-workspace .reply-quote-icon {
  color: var(--chat-muted, #64748b) !important;
  font-size: 12px;
  display: inline-block !important;
  margin-right: 2px;
}

.chat-workspace .reply-user-name {
  color: var(--chat-ink) !important;
  font-weight: 700;
}

.chat-workspace .reply-snippet-text {
  font-size: 13px;
  color: var(--chat-muted, #94a3b8) !important;
  text-align: left;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 100%;
}

.chat-workspace .reply-close-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 4px;
  border: none;
  background: transparent;
  color: var(--chat-muted, #64748b) !important;
  cursor: pointer;
  transition: all 0.15s ease;
  flex-shrink: 0;
  margin-left: 10px;
}

.chat-workspace .reply-close-btn:hover {
  background: color-mix(in srgb, var(--chat-ink) 12%, transparent) !important;
  color: var(--chat-ink) !important;
}

.chat-workspace .chat-input-area { margin: 0; padding: 14px 24px 18px; border-top: 1px solid var(--chat-line) !important; background: var(--chat-surface) !important; box-shadow: none !important; }
.chat-workspace .input-actions-bar .el-button { min-width: 40px; min-height: 40px; }
.chat-workspace .chat-input { min-height: 44px !important; border: 1px solid var(--chat-line) !important; border-radius: 12px !important; background: var(--chat-surface-2) !important; color: var(--chat-ink) !important; font-size: 13.5px !important; }
.chat-workspace .chat-input::placeholder { color: var(--chat-faint) !important; }
.chat-workspace .chat-input:focus { border-color: var(--chat-accent) !important; box-shadow: 0 0 0 3px color-mix(in srgb, var(--chat-accent) 16%, transparent) !important; }
.chat-workspace .btn-send { width: 44px; height: 44px; border-radius: 12px; background: linear-gradient(135deg, #0ea5e9, #2563eb) !important; color: #ffffff !important; box-shadow: 0 4px 14px color-mix(in srgb, #2563eb 28%, transparent) !important; }
.chat-workspace .btn-send:hover:not(:disabled) { opacity: 0.92; transform: translateY(-1px); }
.chat-workspace .chat-context-panel,
.chat-workspace .channel-utility-drawer,
.chat-workspace .call-chat-panel,
.chat-workspace .call-transcript-panel,
.chat-workspace .ai-analysis-surface { background: var(--chat-surface) !important; color: var(--chat-ink) !important; border-color: var(--chat-line) !important; }
.chat-workspace .call-prejoin-panel {
  background: var(--chat-surface) !important;
  color: var(--chat-ink) !important;
  border: 1px solid var(--chat-line) !important;
  border-radius: 16px !important;
  box-shadow: 0 20px 50px color-mix(in srgb, var(--chat-ink) 12%, transparent) !important;
}
.chat-workspace .call-prejoin-copy h2 { color: var(--chat-ink) !important; font-weight: 800 !important; }
.chat-workspace .call-prejoin-copy p { color: var(--chat-muted) !important; }
.chat-workspace .call-prejoin-camera-off strong { color: #f8fafc !important; }
.chat-workspace .call-prejoin-camera-off span { color: #cbd5e1 !important; }
.chat-workspace .call-prejoin-group-title { color: var(--chat-faint) !important; font-size: 11px !important; font-weight: 800 !important; letter-spacing: .05em !important; text-transform: uppercase !important; }
.chat-workspace .call-prejoin-toggle { border: 1px solid var(--chat-line) !important; background: var(--chat-surface-2) !important; color: var(--chat-ink) !important; font-weight: 600 !important; border-radius: 10px !important; transition: all 0.18s ease !important; }
.chat-workspace .call-prejoin-toggle:hover { border-color: var(--chat-accent) !important; background: color-mix(in srgb, var(--chat-accent) 14%, var(--chat-surface)) !important; color: var(--chat-ink) !important; }
.chat-workspace .call-prejoin-toggle.active { border-color: var(--chat-accent) !important; background: color-mix(in srgb, var(--chat-accent) 16%, var(--chat-surface)) !important; color: var(--chat-accent) !important; font-weight: 700 !important; box-shadow: 0 0 0 1px var(--chat-accent) inset !important; }
.chat-workspace .call-prejoin-device-grid select { border: 1px solid var(--chat-line) !important; background: var(--chat-surface-2) !important; color: var(--chat-ink) !important; font-weight: 600 !important; border-radius: 10px !important; padding: 10px 14px !important; }
.chat-workspace .call-prejoin-device-grid label { color: var(--chat-muted) !important; font-weight: 700 !important; font-size: 12px !important; }
.chat-workspace .call-prejoin-actions .secondary-button { border: 1px solid var(--chat-line) !important; background: var(--chat-surface-2) !important; color: var(--chat-ink) !important; font-weight: 700 !important; border-radius: 10px !important; padding: 0 20px !important; min-height: 42px !important; }
.chat-workspace .call-prejoin-actions .secondary-button:hover { background: color-mix(in srgb, var(--chat-ink) 8%, var(--chat-surface-2)) !important; border-color: var(--chat-muted) !important; }
.chat-workspace .call-prejoin-actions .primary-button { border: 0 !important; background: linear-gradient(135deg, #0ea5e9, #2563eb) !important; color: #ffffff !important; font-weight: 800 !important; border-radius: 10px !important; padding: 0 24px !important; min-height: 42px !important; box-shadow: 0 8px 20px color-mix(in srgb, #2563eb 30%, transparent) !important; }
.chat-workspace .call-prejoin-actions .primary-button:hover { opacity: 0.92; transform: translateY(-1px); }
.chat-workspace .context-panel-header, .chat-workspace .ai-surface-header, .chat-workspace .channel-utility-header, .chat-workspace .call-chat-panel-header { border-bottom-color: var(--chat-line) !important; }
.chat-workspace .context-panel-header h3, .chat-workspace .ai-surface-header h3, .chat-workspace .channel-utility-header h3, .chat-workspace .call-chat-panel-header strong, .chat-workspace .call-transcript-header strong { color: var(--chat-ink) !important; }
.chat-workspace .context-tabs { border-bottom-color: var(--chat-line) !important; }
.chat-workspace .context-tab { min-height: 40px; color: var(--chat-muted) !important; }
.chat-workspace .context-tab.is-active, .chat-workspace .context-tab:hover { background: var(--chat-accent-soft) !important; color: var(--chat-accent-hover) !important; }
.chat-workspace .context-member-row { min-height: 48px; color: var(--chat-ink) !important; }
.chat-workspace .context-member-row:hover, .chat-workspace .channel-utility-item:hover { background: var(--chat-surface-2) !important; }
.chat-workspace .context-member-copy strong { color: var(--chat-ink) !important; }
.chat-workspace .context-member-copy span, .chat-workspace .context-empty, .chat-workspace .channel-utility-item small, .chat-workspace .channel-utility-empty { color: var(--chat-faint) !important; }
.chat-workspace .channel-utility-item { border-color: var(--chat-line) !important; background: var(--chat-surface-2) !important; color: var(--chat-muted) !important; }
.chat-workspace .channel-search-box input, .chat-workspace .call-chat-composer input, .chat-workspace .ai-question-section input { border-color: var(--chat-line) !important; background: var(--chat-surface-2) !important; color: var(--chat-ink) !important; }
.chat-workspace .ai-analysis-surface { top: 76px; right: calc(var(--chat-context-width) + 16px); width: min(336px, calc(100% - 32px)); border-radius: 10px; box-shadow: 0 18px 42px color-mix(in srgb, var(--chat-ink) 16%, transparent) !important; }
.chat-workspace .ai-analysis-surface p, .chat-workspace .ai-loading-state small, .chat-workspace .ai-result-item small { color: var(--chat-muted) !important; }
.chat-workspace .ai-result-section h4, .chat-workspace .ai-answer strong { color: var(--chat-ink) !important; }
.chat-workspace .ai-primary-action { background: var(--chat-accent) !important; border-color: var(--chat-accent) !important; color: var(--color-text-inverse, #fff) !important; }
.chat-workspace .ai-primary-action:hover { background: var(--chat-accent-hover) !important; }
.chat-workspace .ai-secondary-action { border-color: var(--chat-accent) !important; color: var(--chat-accent-hover) !important; }
.chat-workspace .call-workspace-body { background: var(--chat-bg) !important; }
.chat-workspace .call-prejoin-panel { background: var(--chat-surface) !important; color: var(--chat-ink) !important; }
.chat-workspace .call-prejoin-copy p { color: var(--chat-muted) !important; }
.chat-workspace .call-prejoin-group-title { color: var(--chat-muted) !important; }
.chat-workspace .call-prejoin-toggle,
.chat-workspace .call-prejoin-actions .secondary-button { border-color: var(--chat-line); background: var(--chat-surface-2); color: var(--chat-ink); }
.chat-workspace .call-prejoin-device-grid label { color: var(--chat-muted); }
.chat-workspace .call-prejoin-select-wrapper { background: var(--chat-surface-2) !important; }
.chat-workspace .call-prejoin-select-wrapper select { color: var(--chat-ink) !important; background: transparent !important; }
.chat-workspace .call-prejoin-select-icon,
.chat-workspace .call-prejoin-select-chevron { color: var(--chat-muted) !important; }
.chat-workspace .call-prejoin-toggle:hover,
.chat-workspace .call-prejoin-toggle:focus-visible { border-color: var(--chat-accent); background: var(--chat-accent-soft); color: var(--chat-accent-hover); }
.chat-workspace .call-prejoin-toggle.active { border-color: var(--chat-accent); background: var(--chat-accent-soft); color: var(--chat-accent-hover); }
.chat-workspace .call-prejoin-joining-status { color: var(--chat-accent-hover); }
.chat-workspace .call-transcript-off, .chat-workspace .call-transcript-consent, .chat-workspace .call-transcript-active, .chat-workspace .call-transcript-paused { border-color: var(--chat-line) !important; background: var(--chat-surface-2) !important; }
.chat-workspace .call-transcript-off p, .chat-workspace .call-transcript-consent p, .chat-workspace .call-consent-list > div span:last-child { color: var(--chat-muted) !important; }
.chat-workspace .call-transcript-chunk { border-bottom-color: var(--chat-line) !important; }
.chat-workspace .call-transcript-chunk p, .chat-workspace .call-chat-message span { color: var(--chat-ink) !important; }
.chat-workspace .call-transcript-title small { color: var(--chat-faint) !important; }
.chat-workspace .call-chat-message small { color: var(--chat-faint) !important; }
.chat-workspace .call-chat-composer { border-top-color: var(--chat-line) !important; }
.chat-workspace .call-header + .call-workspace-body { background: var(--chat-bg) !important; }
.chat-workspace .call-workspace-body { min-height: 0; overflow: auto; }
.chat-workspace .call-presentation-stage {
  min-height: 0;
  display: grid;
  grid-template-rows: auto minmax(0, 1fr) auto;
  overflow: hidden;
}
.chat-workspace .presentation-heading { min-width: 0; flex: 0 0 auto; }
.chat-workspace .presentation-heading strong { min-width: 0; overflow-wrap: anywhere; }
.chat-workspace .presentation-screen { min-height: 0; display: block; flex: 1 1 auto; }
.chat-workspace .presentation-screen video { min-height: 0; }
.chat-workspace .presentation-toolbar { min-height: 56px; flex: 0 0 auto; align-items: center; flex-wrap: wrap; padding: 8px 14px 14px; }
.chat-workspace .presentation-control { min-height: 40px; }
.chat-workspace .call-workspace-body.is-presentation-mode { grid-template-rows: minmax(220px, 1fr) auto auto; }
.chat-workspace .call-workspace-body.is-presentation-mode .call-presentation-stage { min-height: 220px; }
.chat-workspace .call-workspace-body.is-presentation-mode .call-controls-row { min-height: 58px; align-items: center; }
.chat-workspace .call-control-dock,
.chat-workspace .camera-effects-menu,
.chat-workspace .call-device-list { border-color: var(--chat-line) !important; background: var(--chat-surface) !important; color: var(--chat-ink) !important; box-shadow: 0 16px 32px color-mix(in srgb, var(--chat-ink) 16%, transparent) !important; }
.chat-workspace .call-workspace-body:has(+ .call-chat-panel) .call-control-dock {
  max-width: calc(100% - 24px) !important;
  margin-right: auto;
}
.chat-workspace .call-workspace-body:has(+ .call-chat-panel) .call-control-label-btn > span,
.chat-workspace .call-workspace-body:has(+ .call-chat-panel) .camera-effects-control > .call-control-label-btn > span {
  display: none !important;
}
.chat-workspace .call-workspace-body:has(+ .call-chat-panel) .call-control-label-btn {
  min-width: 38px !important;
  padding-inline: 8px !important;
}
.chat-workspace .call-control-label-btn:hover,
.chat-workspace .presentation-control:hover,
.chat-workspace .call-control-label-btn:focus-visible,
.chat-workspace .presentation-control:focus-visible { border-color: var(--chat-accent) !important; background: var(--chat-accent-soft) !important; color: var(--chat-accent-hover) !important; }
.chat-workspace .call-control-circle-btn.share-control { background: var(--chat-surface-2) !important; color: var(--chat-muted) !important; }
.chat-workspace .call-control-circle-btn.share-control.active-share { color: var(--color-success, #16a34a) !important; }
.chat-workspace .call-more-menu { z-index: 9999 !important; border-color: var(--chat-line); background: var(--chat-surface); color: var(--chat-ink); box-shadow: 0 16px 32px color-mix(in srgb, var(--chat-ink) 16%, transparent); }
.chat-workspace .call-more-menu-item,
.chat-workspace .call-more-menu-back,
.chat-workspace .call-device-option { color: var(--chat-ink); }
.chat-workspace .call-more-menu-item:hover,
.chat-workspace .call-more-menu-item:focus-visible,
.chat-workspace .call-more-menu-back:hover,
.chat-workspace .call-more-menu-back:focus-visible,
.chat-workspace .call-device-option:hover,
.chat-workspace .call-device-option:focus-visible,
.chat-workspace .call-device-option.selected { background: var(--chat-surface-2); color: var(--chat-ink); }
.chat-workspace .call-more-menu-item.is-unavailable,
.chat-workspace .call-more-menu-item small,
.chat-workspace .call-more-section-label,
.chat-workspace .call-more-empty { color: var(--chat-muted); }
.chat-workspace .call-reaction-option { border-color: var(--chat-line); background: var(--chat-surface-2); }
.chat-workspace .call-reaction-option:hover,
.chat-workspace .call-reaction-option:focus-visible { border-color: var(--chat-accent); background: var(--chat-accent-soft); }
.chat-workspace .call-workspace-body:fullscreen { overflow: hidden; background: var(--chat-bg); color: var(--chat-ink); }
.chat-workspace .call-workspace-body:fullscreen .call-control-dock { background: var(--chat-surface); }
.chat-workspace .camera-effects-title { color: var(--chat-faint); }
.chat-workspace .camera-effects-menu > button,
.chat-workspace .call-device-list button { color: var(--chat-ink); }
.chat-workspace .camera-effects-menu > button:hover,
.chat-workspace .camera-effects-menu > button.selected,
.chat-workspace .call-device-list button:hover { background: var(--chat-surface-2); color: var(--chat-ink); }
.chat-workspace .camera-effects-notice { color: var(--color-warning, #a16207); }
.chat-workspace .call-chat-panel {
  width: min(380px, calc(100% - 16px));
  min-width: min(380px, calc(100% - 16px));
  background: var(--chat-surface, #ffffff) !important;
  border-left: 1px solid var(--chat-line, #e2e8f0) !important;
  box-shadow: -4px 0 20px rgba(15, 23, 42, 0.05) !important;
}
.chat-workspace .call-chat-thread {
  background: var(--chat-bg, #f8fafc) !important;
  padding: 14px;
  gap: 10px;
  display: flex;
  flex-direction: column;
  scrollbar-width: none !important; /* Firefox */
  -ms-overflow-style: none !important; /* IE and Edge */
}
.chat-workspace .call-chat-thread::-webkit-scrollbar {
  display: none !important; /* Chrome, Safari, Opera */
  width: 0 !important;
  height: 0 !important;
}
.chat-workspace .call-chat-message {
  display: flex;
  gap: 10px;
  padding: 10px 12px;
  border-radius: 14px 14px 14px 2px;
  background: var(--chat-surface, #ffffff) !important;
  border: 1px solid var(--chat-line, #e2e8f0) !important;
  box-shadow: 0 2px 6px rgba(15, 23, 42, 0.04) !important;
  max-width: 85%;
  align-self: flex-start;
}
.chat-workspace .call-chat-message.is-own {
  align-self: flex-end;
  border-radius: 14px 14px 2px 14px;
  background: linear-gradient(135deg, #2563eb, #1d4ed8) !important;
  border-color: #2563eb !important;
  color: #ffffff !important;
  box-shadow: 0 4px 14px rgba(37, 99, 235, 0.22) !important;
}
.chat-workspace .call-msg-inline-header {
  display: flex;
  align-items: center;
  gap: 5px;
  margin-bottom: 3px;
  white-space: nowrap;
}
.chat-workspace .call-msg-avatar {
  flex: 0 0 auto;
}
.chat-workspace .call-msg-author {
  color: var(--chat-ink, #0f172a) !important;
  font-weight: 750 !important;
  font-size: 11.5px !important;
}
.chat-workspace .call-chat-message.is-own .call-msg-author {
  color: #ffffff !important;
}
.chat-workspace .edited-badge {
  font-size: 9px !important;
  opacity: 0.75 !important;
  font-weight: normal !important;
}
.chat-workspace .call-msg-time {
  color: var(--chat-muted, #475569) !important;
  font-weight: 500 !important;
  font-size: 9px !important;
  margin-left: auto;
}
.chat-workspace .call-chat-message.is-own .call-msg-time {
  color: rgba(255, 255, 255, 0.8) !important;
}
.chat-workspace .call-msg-content {
  color: var(--chat-ink, #0f172a) !important;
  font-size: 13.5px !important;
  font-weight: 500 !important;
  line-height: 1.45 !important;
  word-break: break-word;
}
.chat-workspace .call-chat-message.is-own .call-msg-content {
  color: #ffffff !important;
}
.chat-workspace .call-chat-empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 32px 16px;
  gap: 6px;
}
.chat-workspace .call-chat-empty-state strong {
  color: var(--chat-ink, #0f172a) !important;
  font-size: 13.5px !important;
  font-weight: 750 !important;
}
.chat-workspace .call-chat-empty-state span {
  color: var(--chat-muted, #475569) !important;
  font-size: 12px !important;
}
.chat-workspace .call-chat-composer {
  padding: 12px;
  background: var(--chat-surface, #ffffff) !important;
  border-top: 1px solid var(--chat-line, #e2e8f0) !important;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.chat-workspace .call-composer-controls-row {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
}
.chat-workspace .call-composer-icon-btn {
  width: 38px !important;
  height: 38px !important;
  min-width: 38px !important;
  min-height: 38px !important;
  border-radius: 10px !important;
  border: 1px solid var(--chat-line, #cbd5e1) !important;
  background: var(--chat-surface-2, #f1f5f9) !important;
  color: var(--chat-muted, #334155) !important;
  display: inline-flex !important;
  align-items: center !important;
  justify-content: center !important;
  font-size: 15px !important;
  cursor: pointer;
  transition: all 0.16s ease !important;
  flex-shrink: 0;
  padding: 0 !important;
}
.chat-workspace .call-composer-icon-btn:hover {
  background: var(--chat-accent-soft, #eff6ff) !important;
  border-color: var(--chat-accent, #2563eb) !important;
  color: var(--chat-accent, #2563eb) !important;
  transform: translateY(-1px);
}
.chat-workspace .call-chat-composer textarea {
  flex: 1;
  min-height: 38px !important;
  height: 38px !important;
  max-height: 96px;
  padding: 8px 12px !important;
  border: 1px solid var(--chat-line, #cbd5e1) !important;
  border-radius: 10px !important;
  background: var(--chat-surface, #ffffff) !important;
  color: var(--chat-ink, #0f172a) !important;
  font-size: 13px !important;
  font-weight: 500 !important;
  outline: none;
  resize: vertical;
  line-height: 1.4;
}
.chat-workspace .call-chat-composer textarea::placeholder {
  color: var(--chat-faint, #64748b) !important;
  opacity: 1 !important;
}
.chat-workspace .call-chat-composer textarea:focus {
  border-color: var(--chat-accent, #2563eb) !important;
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--chat-accent, #2563eb) 18%, transparent) !important;
}
.chat-workspace .call-composer-send-btn {
  width: 38px !important;
  height: 38px !important;
  min-width: 38px !important;
  min-height: 38px !important;
  border-radius: 10px !important;
  border: none !important;
  background: linear-gradient(135deg, #0ea5e9, #2563eb) !important;
  color: #ffffff !important;
  display: inline-flex !important;
  align-items: center !important;
  justify-content: center !important;
  font-size: 14px !important;
  cursor: pointer;
  flex-shrink: 0;
  transition: all 0.15s ease !important;
  box-shadow: 0 4px 12px rgba(37, 99, 235, 0.28) !important;
  padding: 0 !important;
}
.chat-workspace .call-composer-send-btn:hover:not(:disabled) {
  transform: translateY(-1px);
  box-shadow: 0 6px 16px rgba(37, 99, 235, 0.38) !important;
}
.chat-workspace .call-composer-send-btn:disabled {
  background: var(--chat-surface-2, #e2e8f0) !important;
  color: var(--chat-faint, #94a3b8) !important;
  box-shadow: none !important;
  cursor: not-allowed;
  opacity: 0.65;
}
.chat-workspace .call-attached-preview {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  padding: 4px;
}
.chat-workspace .call-attached-file {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  border-radius: 6px;
  background: var(--chat-surface-2, #f1f5f9);
  border: 1px solid var(--chat-line, #cbd5e1);
  font-size: 11px;
}
.chat-workspace .call-attached-file button {
  background: transparent;
  border: none;
  color: var(--chat-faint, #64748b);
  cursor: pointer;
  font-size: 11px;
}
.chat-workspace .call-attached-file button:hover {
  color: #ef4444;
}
:global([data-theme='dark'] .chat-workspace) .call-composer-icon-btn {
  background: rgba(255, 255, 255, 0.05) !important;
  border-color: rgba(148, 163, 184, 0.2) !important;
  color: #cbd5e1 !important;
}
:global([data-theme='dark'] .chat-workspace) .call-composer-icon-btn:hover {
  background: rgba(56, 189, 248, 0.15) !important;
  border-color: #38bdf8 !important;
  color: #38bdf8 !important;
}
:global([data-theme='dark'] .chat-workspace) .call-chat-composer textarea {
  background: #1e293b !important;
  border-color: rgba(148, 163, 184, 0.24) !important;
  color: #f8fafc !important;
}
:global([data-theme='dark'] .chat-workspace) .call-chat-composer textarea::placeholder {
  color: #64748b !important;
}
:global([data-theme='dark'] .chat-workspace) .call-composer-send-btn:disabled {
  background: rgba(255, 255, 255, 0.06) !important;
  color: #475569 !important;
  border: 1px solid rgba(255, 255, 255, 0.06) !important;
}
.chat-workspace .call-chat-panel { isolation: isolate; }
.chat-workspace .call-fullscreen-panel { width: min(380px, calc(100% - 16px)); }
.chat-workspace .call-chat-panel-title { min-width: 0; flex: 1; }
.chat-workspace .call-chat-channel-name { display: block; max-width: 100%; margin-top: 4px; overflow-wrap: anywhere; color: var(--chat-ink) !important; font-size: 14px; line-height: 1.25; }
.chat-workspace .call-chat-thread { gap: 12px; padding: 14px; }
.chat-workspace .call-chat-message .el-avatar { flex: 0 0 auto; }
.chat-workspace .call-chat-message-body { min-width: 0; flex: 1; }
.chat-workspace .call-chat-message-meta { display: flex; align-items: baseline; justify-content: space-between; gap: 8px; margin-bottom: 3px; }
.chat-workspace .call-chat-message p { margin: 0; overflow-wrap: anywhere; white-space: pre-wrap; }
.chat-workspace .call-chat-composer { align-items: flex-end; gap: 6px; padding: 10px; }
.chat-workspace .call-chat-composer textarea { min-height: 44px; max-height: 96px; flex: 1; resize: vertical; border: 1px solid var(--chat-line); border-radius: 8px; outline: none; padding: 10px; background: var(--chat-surface-2); color: var(--chat-ink); font: inherit; font-size: 12px; line-height: 1.35; }
.chat-workspace .call-chat-composer textarea::placeholder { color: var(--chat-faint); }
.chat-workspace .call-chat-composer textarea:focus { border-color: var(--chat-accent); box-shadow: 0 0 0 3px color-mix(in srgb, var(--chat-accent) 16%, transparent); }
.chat-workspace .call-fullscreen-panel { border-left-color: var(--chat-line); background: var(--chat-surface); color: var(--chat-ink); }
.chat-workspace .transcript-entry-button { position: relative; }
.chat-workspace .transcript-entry-button.is-open { border-color: var(--chat-accent) !important; background: var(--chat-accent-soft) !important; }
.call-header-status-dot { width: 7px; height: 7px; flex: 0 0 auto; border-radius: 50%; background: #36c98b; box-shadow: 0 0 0 3px color-mix(in srgb, #36c98b 16%, transparent); }
.caption-consent-dialog .el-dialog__header { margin-right: 0; padding: 18px 20px 8px; }
.caption-consent-dialog .el-dialog__body { padding: 8px 20px 18px; }
.caption-consent-dialog .el-dialog__footer { padding: 0 20px 18px; }
.caption-consent-heading { display: flex; align-items: center; gap: 11px; }
.caption-consent-heading h3 { margin: 4px 0 0; color: var(--chat-ink); font-size: 17px; line-height: 1.2; }
.caption-consent-icon { display: grid; width: 36px; height: 36px; place-items: center; border-radius: 10px; background: var(--chat-accent-soft); color: var(--chat-accent); font-size: 16px; }
.caption-consent-copy { display: grid; gap: 7px; }
.caption-consent-copy p { margin: 0; color: var(--chat-ink); font-size: 13px; line-height: 1.5; }
.caption-consent-copy small { color: var(--chat-muted); font-size: 11px; }
.caption-consent-actions { display: flex; justify-content: flex-end; gap: 8px; }
.caption-consent-actions button { min-height: 38px; }
.chat-workspace .call-transcript-panel { max-height: 280px; }
.chat-workspace .call-transcript-consent { border-style: dashed; }
.chat-workspace .call-transcript-consent p { margin-bottom: 0; }
.chat-workspace .call-transcript-active { display: flex; align-items: center; justify-content: space-between; gap: 10px; }
.chat-workspace .call-transcript-active .call-transcript-indicator { margin-bottom: 0; }
.chat-workspace .call-transcript-chunk.is-interim { opacity: .84; }
.chat-workspace :where(button, input, textarea, [tabindex='0']):focus-visible { outline: 3px solid color-mix(in srgb, var(--chat-accent) 52%, transparent); outline-offset: 2px; }
.chat-sidebar-backdrop { display: none; }

@media (max-width: 1120px) {
  .chat-workspace { grid-template-columns: 60px 224px minmax(0, 1fr) !important; }
  .chat-workspace .chat-main { padding-right: 0 !important; }
  .chat-workspace .chat-context-panel { width: min(288px, 78vw); }
  .chat-workspace .ai-analysis-surface { right: 16px; }
}
@media (max-width: 760px) {
  .chat-workspace { width: 100% !important; height: calc(100dvh - 76px); min-height: 0; margin: 0 !important; border-radius: 0; border-inline: 0; grid-template-columns: 52px minmax(0, 1fr) !important; }
  .chat-workspace .server-bar { grid-column: 1; z-index: 7; padding-inline: 7px !important; }
  .chat-workspace .chat-sidebar { position: absolute; z-index: 6; top: 0; bottom: 0; left: 52px; width: min(280px, calc(100vw - 52px)) !important; transform: translateX(-105%); box-shadow: 14px 0 32px color-mix(in srgb, var(--chat-ink) 18%, transparent); transition: transform 180ms ease-out; }
  .chat-workspace.is-sidebar-open .chat-sidebar { transform: translateX(0); }
  .chat-workspace .chat-sidebar-backdrop { display: block; position: absolute; z-index: 5; inset: 0; border: 0; background: color-mix(in srgb, var(--chat-ink) 24%, transparent); cursor: pointer; }
  .chat-workspace .chat-main { grid-column: 2; }
  .chat-workspace .mobile-sidebar-trigger { display: inline-grid; width: 40px; height: 40px; place-items: center; flex: 0 0 auto; border: 0; border-radius: 8px; background: var(--chat-surface-2); color: var(--chat-muted); }
  .chat-workspace .chat-header { padding-inline: 12px !important; }
  .chat-workspace .header-actions { max-width: 52%; overflow-x: auto; }
  .chat-workspace .header-actions .ai-entry-button span:not(.ai-off-state) { display: none; }
  .chat-workspace .ai-entry-button { width: 40px; padding: 0; justify-content: center; }
  .chat-workspace .messages-thread { padding-inline: 14px; }
  .chat-workspace .message-card { max-width: 100%; }
  .chat-workspace .chat-input-area { padding-inline: 12px; }
  .chat-workspace .chat-context-panel { width: min(320px, calc(100vw - 16px)); }
  .chat-workspace .ai-analysis-surface { top: 70px; right: 8px; width: min(320px, calc(100% - 16px)); }
  .chat-workspace .call-workspace-body { padding: 10px; }
}
@media (max-width: 480px) {
  .chat-workspace .chat-header h4 { max-width: 30vw; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .chat-workspace .header-actions .action-btn:nth-last-child(-n + 2) { display: none; }
  .chat-workspace .call-control-dock { max-width: 100%; overflow-x: auto; }
}

/* Responsive meeting composition: preserve the desktop shell and promote secondary navigation to a drawer. */
.chat-workspace .call-header .active-info > div {
  min-width: 0;
}

.chat-workspace .call-header h4 {
  min-width: 0;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.chat-workspace .call-header .active-info > div > p {
  max-width: 100%;
  overflow-wrap: anywhere;
}

@media (max-width: 900px) {
  .chat-workspace {
    width: 100% !important;
    height: min(820px, calc(100dvh - 76px));
    min-height: 0;
    margin: 0 auto 12px !important;
    border-radius: 0;
    border-inline: 0;
    grid-template-columns: 52px minmax(0, 1fr) !important;
  }

  .chat-workspace .chat-sidebar {
    position: absolute;
    z-index: 6;
    top: 0;
    bottom: 0;
    left: 52px;
    width: min(280px, calc(100vw - 52px)) !important;
    transform: translateX(-105%);
    box-shadow: 14px 0 32px color-mix(in srgb, var(--chat-ink) 18%, transparent);
    transition: transform 180ms ease-out;
  }

  .chat-workspace.is-sidebar-open .chat-sidebar {
    transform: translateX(0);
  }

  .chat-workspace:not(.is-sidebar-open) .chat-sidebar {
    transform: translateX(-105%);
  }

  .chat-workspace .chat-sidebar-backdrop {
    display: block;
    position: absolute;
    z-index: 5;
    inset: 0;
    border: 0;
    background: color-mix(in srgb, var(--chat-ink) 24%, transparent);
    cursor: pointer;
  }

  .chat-workspace .mobile-sidebar-trigger {
    display: inline-grid;
    width: 40px;
    height: 40px;
    place-items: center;
    flex: 0 0 auto;
    border: 0;
    border-radius: 8px;
    background: var(--chat-surface-2);
    color: var(--chat-muted);
  }

  .chat-workspace .chat-main {
    grid-column: 2;
  }

  .chat-workspace .chat-header {
    padding-inline: 12px !important;
  }

  .chat-workspace .header-actions {
    max-width: 52%;
    overflow-x: auto;
  }

  .chat-workspace .call-workspace-body {
    min-width: 0;
    width: 100%;
    box-sizing: border-box;
    padding: 10px;
  }

  .chat-workspace .call-camera-stage,
  .chat-workspace .call-presentation-stage,
  .chat-workspace .call-participant-rail,
  .chat-workspace .call-transcript-panel {
    max-width: 100%;
    min-width: 0;
  }

  .chat-workspace .call-camera-stage {
    overflow-x: hidden;
  }

  .chat-workspace .call-participant-rail {
    overflow-x: auto;
  }
}

@media (max-width: 560px) {
  .chat-workspace .call-header .active-info {
    flex: 1 1 auto;
    min-width: 0;
  }

  .chat-workspace .call-header .header-actions {
    flex: 0 0 auto;
    max-width: 42%;
  }

  .chat-workspace .call-header h4 {
    max-width: clamp(7rem, 38vw, 13rem);
  }

  .chat-workspace .call-control-dock {
    display: flex;
    flex-wrap: nowrap;
    align-items: center;
    justify-content: center;
    gap: 6px;
    padding: 6px 10px;
    max-width: 100%;
  }

  .chat-workspace .call-control-dock > * {
    width: auto;
    min-width: 0;
    order: unset;
  }

  .chat-workspace .call-control-circle-btn {
    width: 38px;
    height: 38px;
    min-width: 38px;
    min-height: 38px;
  }

  .chat-workspace .call-control-label-btn {
    width: auto;
    min-width: 38px;
    height: 38px;
    min-height: 38px;
    padding-inline: 10px;
    border-radius: 10px;
  }

  .chat-workspace .camera-effects-control {
    min-width: 0;
  }

  .chat-workspace .call-camera-stage {
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 8px;
    min-height: 0;
    padding: 8px;
  }

  .chat-workspace .call-camera-stage[data-participant-count="1"] {
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .chat-workspace .call-camera-stage[data-participant-count="1"] .call-camera-stage-tile {
    width: 100%;
    max-width: min(840px, 94%);
    max-height: min(68vh, 500px);
  }

  .chat-workspace .call-camera-stage[data-participant-count="2"] {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .chat-workspace .call-camera-stage-tile,
  .chat-workspace .call-camera-stage-tile video,
  .chat-workspace .call-camera-off-state {
    min-width: 0;
    max-width: 100%;
  }

  .chat-workspace .call-camera-off-state {
    min-height: 0;
    padding: clamp(12px, 4vw, 28px);
  }

  .chat-workspace .call-chat-panel,
  .chat-workspace .call-fullscreen-panel {
    max-width: calc(100% - 16px);
  }
}

@media (min-width: 360px) and (max-width: 560px) {
  .chat-workspace .call-camera-stage[data-participant-count]:not([data-participant-count="1"]):not([data-participant-count="2"]) {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .chat-workspace .call-camera-stage[data-participant-count="3"] .call-camera-stage-tile:last-child {
    grid-column: auto;
    width: 100%;
  }
}
@media (prefers-reduced-motion: reduce) {
  .chat-workspace .chat-sidebar { transition: none; }
}

/* Bounded Voice Call Stage & Overflow Summary Tile */
.chat-workspace .call-camera-stage-tile.call-overflow-tile {
  display: flex;
  align-items: center;
  justify-content: center;
  background: color-mix(in srgb, var(--chat-surface-2) 85%, var(--chat-accent));
  border: 1px dashed color-mix(in srgb, var(--chat-accent) 45%, var(--chat-line)) !important;
  border-radius: 16px;
  cursor: pointer;
  padding: 16px;
  transition: transform 0.18s ease, border-color 0.18s ease, background-color 0.18s ease;
}
.chat-workspace .call-camera-stage-tile.call-overflow-tile:hover {
  border-color: var(--chat-accent) !important;
  background: color-mix(in srgb, var(--chat-accent) 22%, var(--chat-surface-2));
  transform: translateY(-2px);
}
.chat-workspace .call-overflow-content {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  text-align: center;
}
.chat-workspace .call-overflow-avatars {
  display: flex;
  align-items: center;
}
.chat-workspace .call-overflow-avatars .el-avatar {
  margin-left: -8px;
  border: 2px solid var(--chat-surface);
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.25);
}
.chat-workspace .call-overflow-avatars .el-avatar:first-child {
  margin-left: 0;
}
.chat-workspace .call-overflow-content strong {
  color: var(--chat-ink);
  font-size: 13.5px;
  font-weight: 800;
}
.chat-workspace .call-overflow-hint {
  color: var(--chat-accent);
  font-size: 11px;
  font-weight: 600;
}

/* Unified project accordion navigator. */
.chat-workspace { grid-template-columns: minmax(248px, 276px) minmax(0, 1fr) !important; }
.chat-workspace .chat-sidebar { grid-column: 1; width: auto !important; min-width: 0; padding: 16px 12px 12px !important; background: var(--chat-surface) !important; border-right: 1px solid var(--chat-line) !important; }
.chat-workspace .chat-main { grid-column: 2; }
.navigator-header { display: grid; gap: 12px; padding: 0 2px 14px; border-bottom: 1px solid var(--chat-line); }
.navigator-heading { display: flex; align-items: center; justify-content: space-between; gap: 10px; }
.navigator-heading .eyebrow { margin: 0; }
.navigator-count { color: var(--chat-faint); font-size: 10px; white-space: nowrap; }
.navigator-search { display: flex; align-items: center; gap: 8px; min-height: 40px; padding: 0 10px; border: 1px solid var(--chat-line); border-radius: 10px; background: var(--chat-surface-2); color: var(--chat-faint); }
.navigator-search:focus-within { border-color: var(--chat-accent); box-shadow: 0 0 0 3px color-mix(in srgb, var(--chat-accent) 14%, transparent); }
.navigator-search input { width: 100%; min-width: 0; border: 0; outline: 0; background: transparent; color: var(--chat-ink); font: inherit; font-size: 12px; }
.navigator-search input::placeholder { color: var(--chat-faint); }
.navigator-search-clear { display: inline-grid; width: 24px; height: 24px; flex: 0 0 auto; place-items: center; border: 0; border-radius: 6px; background: transparent; color: var(--chat-faint); cursor: pointer; }
.navigator-search-clear:hover { background: var(--chat-surface); color: var(--chat-ink); }
.navigator-list { display: flex; flex-direction: column; gap: 4px; padding: 12px 2px 4px; }
.project-accordion { min-width: 0; }
.project-row { display: flex; align-items: center; gap: 9px; width: 100%; min-height: 44px; padding: 7px 8px; border: 1px solid transparent; border-radius: 10px; background: transparent; color: var(--chat-muted); text-align: left; cursor: pointer; }
.project-row:hover, .project-row:focus-visible { border-color: var(--chat-line); background: var(--chat-surface-2); color: var(--chat-ink); }
.project-accordion.is-active > .project-row { color: var(--chat-ink); }
.project-accordion.is-expanded > .project-row { border-color: color-mix(in srgb, var(--chat-accent) 28%, var(--chat-line)); background: var(--chat-accent-soft); color: var(--chat-ink); }
.project-chevron { display: inline-grid; width: 14px; flex: 0 0 14px; place-items: center; color: var(--chat-faint); font-size: 10px; transition: transform .16s ease; }
.project-accordion.is-expanded .project-chevron { color: var(--chat-accent); transform: rotate(90deg); }
.project-avatar { display: inline-grid; width: 26px; height: 26px; flex: 0 0 26px; place-items: center; border: 1px solid var(--chat-line); border-radius: 8px; background: var(--chat-surface-2); color: var(--chat-accent); font-size: 11px; font-weight: 800; }
.project-name { min-width: 0; overflow: hidden; flex: 1; text-overflow: ellipsis; white-space: nowrap; font-size: 12px; font-weight: 750; }
.project-channel-tree { display: grid; gap: 5px; margin: 2px 0 8px 22px; padding: 6px 0 4px 13px; border-left: 1px solid color-mix(in srgb, var(--chat-accent) 30%, var(--chat-line)); }
.navigator-section-head { display: flex; align-items: center; justify-content: space-between; min-height: 30px; padding: 0 2px; }
.navigator-section-head .add-btn-small { width: 28px; height: 28px; }
.voice-section-head { margin-top: 8px; }
.navigator-channel { min-height: 36px !important; padding: 7px 9px !important; border-radius: 8px !important; }
.navigator-channel .item-name { min-width: 0; }
.navigator-channel.voice-item { align-items: center; }
.navigator-channel.voice-item .item-name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.navigator-empty { margin: 10px 0; }
.navigator-direct-section { padding-top: 10px; border-top: 1px solid var(--chat-line); }
.navigator-direct-section .direct-item { min-height: 40px; }
.voice-users-list { display: grid; gap: 4px; margin: 3px 0 4px 28px; }
.voice-user { display: flex; align-items: center; gap: 6px; min-width: 0; padding: 2px 4px; color: var(--chat-faint); font-size: 10px; }
.voice-user span { min-width: 0; }
@media (max-width: 1120px) and (min-width: 761px) { .chat-workspace { grid-template-columns: minmax(236px, 260px) minmax(0, 1fr) !important; } }
@media (max-width: 900px) and (min-width: 761px) {
  .chat-workspace .chat-sidebar {
    position: relative;
    top: auto;
    bottom: auto;
    left: auto;
    width: auto !important;
    transform: none !important;
    box-shadow: none;
  }
  .chat-workspace .chat-sidebar-backdrop { display: none !important; }
}
@media (max-width: 760px) {
  .chat-workspace { grid-template-columns: minmax(0, 1fr) !important; }
  .chat-workspace .chat-sidebar { left: 0; width: min(320px, calc(100vw - 18px)) !important; }
  .chat-workspace .chat-main { grid-column: 1; }
}
/* Context panel tab & section styles */
.context-details-content,
.context-files-content {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 14px 12px;
  overflow-y: auto;
  min-height: 0;
  flex: 1;
}

.context-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.context-section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.context-section-header h4 {
  margin: 0;
  font-size: 12px;
  font-weight: 700;
  color: #94a3b8;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.context-link-btn {
  border: none;
  background: transparent;
  color: #38bdf8;
  font-size: 11px;
  font-weight: 600;
  cursor: pointer;
  padding: 0;
}

.context-link-btn:hover {
  text-decoration: underline;
}

.context-avatar-wrapper {
  position: relative;
  display: flex;
  align-items: center;
}

.context-avatar-wrapper .presence-dot {
  position: absolute;
  bottom: 0;
  right: 0;
  width: 9px;
  height: 9px;
  border-radius: 50%;
  border: 2px solid #0c1828;
}

.presence-dot.is-online {
  background-color: #22c55e;
}

.presence-dot.is-offline, .presence-dot.is-idle {
  background-color: #64748b;
}

.context-members-avatar-row {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 6px;
  padding-left: 4px;
}

.member-avatar-pill {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  background: #1e293b;
  color: #e2e8f0;
  font-size: 11px;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid rgba(255,255,255,0.1);
}

.avatar-more-pill {
  background: rgba(56, 189, 248, 0.15);
  color: #38bdf8;
}

.fullscreen-media-viewer-dialog {
  background: transparent !important;
  box-shadow: none !important;
}
.fullscreen-media-viewer-dialog .el-dialog__header {
  display: none !important;
}
.fullscreen-media-viewer-dialog .el-dialog__body {
  padding: 0 !important;
  height: 100vh !important;
  width: 100vw !important;
  overflow: hidden !important;
  background: #121212 !important;
}

.context-pinned-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.context-pinned-card {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 10px;
  border-radius: 8px;
  background: rgba(30, 41, 59, 0.5);
  border: 1px solid rgba(255, 255, 255, 0.06);
  cursor: pointer;
  transition: all 0.15s ease;
}

.context-pinned-card:hover {
  background: rgba(30, 41, 59, 0.9);
  border-color: rgba(56, 189, 248, 0.3);
}

.pinned-card-icon {
  color: #38bdf8;
  font-size: 13px;
  margin-top: 2px;
}

.pinned-card-body {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
  flex: 1;
}

.pinned-card-meta {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 11px;
  color: #94a3b8;
}

.pinned-card-meta strong {
  color: #f1f5f9;
}

.pinned-card-text {
  margin: 0;
  font-size: 12px;
  color: #cbd5e1;
  line-height: 1.4;
}

.context-desc-text {
  margin: 0;
  font-size: 12px;
  color: var(--chat-ink, #334155);
  line-height: 1.5;
  background: var(--chat-surface-2, rgba(241, 245, 249, 0.8));
  padding: 10px;
  border-radius: 8px;
  border: 1px solid var(--chat-line, rgba(226, 232, 240, 0.8));
}

.context-edit-desc-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 12px;
  border-radius: 6px;
  border: 1px solid var(--chat-line, rgba(203, 213, 225, 0.6));
  background: var(--chat-surface, #ffffff);
  color: var(--chat-ink, #1e293b);
  font-size: 12px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.15s ease;
  margin-top: 8px;
}

.context-edit-desc-btn:hover {
  background: var(--chat-accent, #0ea5e9);
  color: #ffffff;
  border-color: var(--chat-accent, #0ea5e9);
}

.context-collapsible-group {
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
  padding-bottom: 10px;
}

.context-collapsible-group[open] .summary-chevron {
  transform: rotate(180deg);
}

.context-group-summary {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 13px;
  font-weight: 700;
  color: var(--chat-ink, #1e293b);
  cursor: pointer;
  padding: 8px 0;
  list-style: none;
}

.context-group-summary::-webkit-details-marker {
  display: none;
}

.summary-chevron {
  font-size: 11px;
  color: #64748b;
  transition: transform 0.2s ease;
}

.context-group-body {
  padding-top: 8px;
}

.context-empty-small {
  font-size: 11px;
  color: var(--chat-muted, #64748b);
  padding: 8px 0;
}

.media-thumbnails-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 6px;
}

.media-thumbnail-item {
  position: relative;
  aspect-ratio: 1;
  border-radius: 6px;
  overflow: hidden;
  background: #1e293b;
  cursor: pointer;
  border: 1px solid rgba(255,255,255,0.08);
}

.media-thumbnail-item img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.media-thumb-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #64748b;
}

.context-full-btn {
  width: 100%;
  margin-top: 8px;
  padding: 8px;
  border-radius: 6px;
  border: none;
  background: rgba(255,255,255,0.05);
  color: #94a3b8;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
}

.context-full-btn:hover {
  background: rgba(255,255,255,0.09);
  color: #fff;
}

.context-files-list, .context-links-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.context-file-item, .context-link-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border-radius: 8px;
  background: var(--chat-surface-2);
  border: 1px solid var(--chat-line);
  text-decoration: none;
  color: var(--chat-ink);
}

.context-file-item:hover, .context-link-item:hover {
  background: var(--chat-surface-hover, rgba(59, 130, 246, 0.08));
}

.file-item-info, .link-item-info {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
}

.file-name {
  font-size: 12px;
  font-weight: 600;
  color: var(--chat-ink);
}

.file-size, .link-meta {
  font-size: 10px;
  color: #64748b;
}

.file-download-action {
  border: none;
  background: transparent;
  color: #94a3b8;
  font-size: 12px;
  cursor: pointer;
  padding: 4px;
}

.file-download-action:hover {
  color: #38bdf8;
}

.link-item-icon {
  color: #38bdf8;
  font-size: 13px;
}

.link-url {
  font-size: 12px;
  color: #38bdf8;
}

.link-ext-icon {
  font-size: 10px;
  color: #64748b;
}


/* Pinned Messages Top Banner (Zalo-style) */
.chat-pinned-banner {
  background: rgba(30, 41, 59, 0.7);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  padding: 8px 16px;
  position: relative;
  z-index: 10;
  transition: all 0.2s ease;
}

:root:not(.dark) .chat-pinned-banner {
  background: #ffffff;
  border-bottom-color: #e2e8f0;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.04);
}

.pinned-summary-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  width: 100%;
}

.pinned-summary-left {
  display: flex;
  align-items: center;
  gap: 10px;
  cursor: pointer;
  flex: 1;
  min-width: 0;
}

.pinned-icon-circle {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: rgba(56, 189, 248, 0.15);
  color: #38bdf8;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 15px;
  flex-shrink: 0;
}

:root:not(.dark) .pinned-icon-circle {
  background: #e0f2fe;
  color: #0284c7;
}

.pinned-summary-text {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
}

.pinned-type-label {
  font-size: 13px;
  font-weight: 600;
  color: #f1f5f9;
  line-height: 1.2;
}

:root:not(.dark) .pinned-type-label {
  color: #0f172a;
}

.pinned-summary-snippet {
  font-size: 12px;
  color: #94a3b8;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  margin-top: 2px;
}

:root:not(.dark) .pinned-summary-snippet {
  color: #475569;
}

.pinned-summary-right {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.pinned-count-toggle-btn {
  background: transparent;
  border: 1px solid rgba(255, 255, 255, 0.2);
  color: #f1f5f9;
  padding: 3px 10px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  transition: all 0.15s ease;
}

:root:not(.dark) .pinned-count-toggle-btn {
  border-color: #cbd5e1;
  color: #1e293b;
}

.pinned-count-toggle-btn:hover {
  background: rgba(255, 255, 255, 0.1);
}

:root:not(.dark) .pinned-count-toggle-btn:hover {
  background: #f1f5f9;
}

.pinned-action-more-btn {
  background: transparent;
  border: none;
  color: #94a3b8;
  width: 28px;
  height: 28px;
  border-radius: 6px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 15px;
  cursor: pointer;
  transition: all 0.15s ease;
}

:root:not(.dark) .pinned-action-more-btn {
  color: #64748b;
}

.pinned-action-more-btn:hover {
  background: rgba(255, 255, 255, 0.1);
  color: #fff;
}

:root:not(.dark) .pinned-action-more-btn:hover {
  background: #f1f5f9;
  color: #0f172a;
}

/* Expanded View */
.pinned-expanded-box {
  display: flex;
  flex-direction: column;
  background: #0f172a;
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid rgba(255, 255, 255, 0.08);
}

:root:not(.dark) .pinned-expanded-box {
  background: #ffffff;
  border-color: #e2e8f0;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
}

.pinned-expanded-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  background: rgba(0, 0, 0, 0.2);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

:root:not(.dark) .pinned-expanded-header {
  background: #f8fafc;
  border-bottom-color: #e2e8f0;
}

.pinned-expanded-title {
  font-size: 13px;
  font-weight: 700;
  color: #f1f5f9;
}

:root:not(.dark) .pinned-expanded-title {
  color: #0f172a;
}

.pinned-collapse-btn {
  background: transparent;
  border: none;
  color: #94a3b8;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 4px;
}

.pinned-collapse-btn:hover {
  color: #38bdf8;
}

.pinned-expanded-list {
  display: flex;
  flex-direction: column;
  max-height: 260px;
  overflow-y: auto;
}

.pinned-expanded-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 12px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.04);
  transition: background 0.15s ease;
}

:root:not(.dark) .pinned-expanded-item {
  border-bottom-color: #f1f5f9;
}

.pinned-expanded-item:last-child {
  border-bottom: none;
}

.pinned-expanded-item:hover {
  background: rgba(255, 255, 255, 0.03);
}

:root:not(.dark) .pinned-expanded-item:hover {
  background: #f8fafc;
}

.pinned-item-left {
  display: flex;
  align-items: center;
  gap: 10px;
  flex: 1;
  min-width: 0;
  cursor: pointer;
}

.pinned-item-text {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
}

.pinned-item-snippet {
  font-size: 12px;
  color: #94a3b8;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  margin-top: 2px;
}

:root:not(.dark) .pinned-item-snippet {
  color: #475569;
}

.pinned-expanded-footer {
  text-align: center;
  padding: 8px 12px;
  font-size: 12px;
  font-weight: 600;
  color: #38bdf8;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
  cursor: pointer;
}

:root:not(.dark) .pinned-expanded-footer {
  color: #0284c7;
  border-top-color: #e2e8f0;
  background: #fafafa;
}

.pinned-expanded-footer:hover {
  text-decoration: underline;
}

.context-delete-channel-btn {
  width: 100%;
  padding: 9px 12px;
  border-radius: 6px;
  border: 1px solid rgba(239, 68, 68, 0.3);
  background: rgba(239, 68, 68, 0.08);
  color: #ef4444;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.15s ease;
  margin-top: 8px;
}

.context-delete-channel-btn:hover {
  background: #ef4444;
  color: #ffffff;
  border-color: #ef4444;
}

.sidebar-see-more-btn {
  border: 0 !important;
  outline: none !important;
  box-shadow: none !important;
  background: var(--chat-surface-2) !important;
  color: var(--chat-accent) !important;
  border-radius: 8px !important;
  font-weight: 600 !important;
  cursor: pointer !important;
}

.sidebar-see-more-btn:hover {
  background: var(--chat-accent-soft) !important;
}

@media (prefers-reduced-motion: reduce) { .project-chevron { transition: none; } }
</style>

<style>
@media (max-width: 560px) {
  .el-overlay-dialog {
    box-sizing: border-box;
    padding: 12px !important;
  }

  .group-call-dialog.video-call-dialog,
  .add-friend-dialog {
    width: min(100%, calc(100vw - 24px)) !important;
    max-width: calc(100vw - 24px) !important;
    margin: 0 auto !important;
  }

  .group-call-dialog.video-call-dialog .el-dialog__body,
  .add-friend-dialog .el-dialog__body {
    max-height: calc(100dvh - 180px);
    overflow-y: auto;
  }
}
</style>
