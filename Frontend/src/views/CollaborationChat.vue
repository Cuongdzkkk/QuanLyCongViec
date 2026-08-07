<template>
  <div class="chat-container">
    <!-- Left-most Discord-style Server Sidebar (only in channels tab) -->
    <div class="server-bar" v-if="currentTab === 'channel'">
      <div 
        v-for="srv in servers" 
        :key="srv.id" 
        class="server-icon-wrapper"
        :class="{ active: activeServer.id === srv.id }"
        @click="selectServer(srv)"
        :title="srv.name"
      >
        <div class="server-icon" :style="{ backgroundColor: srv.color }">
          {{ srv.name.charAt(0).toUpperCase() }}
        </div>
        <div class="active-indicator"></div>
      </div>
      
      <!-- Add Server Circle Button -->
      <button class="add-server-circle-btn" @click="openCreateServerModal" title="Tạo Server mới">
        <i class="fa-solid fa-plus"></i>
      </button>
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
            <button class="add-btn-small" title="Tạo kênh mới" @click="openCreateChannelModal">
              <i class="fa-solid fa-plus text-xs"></i>
            </button>
          </div>
          <div class="section-list">
            <button 
              v-for="ch in channels" 
              :key="ch.id" 
              class="list-item" 
              :class="{ active: activeChat.id === ch.id && activeChat.type === 'channel' }"
              @click="selectChat(ch, 'channel')"
            >
              <span class="item-icon">#</span>
              <span class="item-name truncate">{{ ch.name }}</span>
              <el-badge v-if="ch.unread" :value="ch.unread" class="ml-auto" />
            </button>
          </div>
        </div>

        <!-- Voice Channels List -->
        <div class="sidebar-section mt-4" v-if="currentTab === 'channel'">
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
          <div class="section-list">
            <button 
              v-for="m in members" 
              :key="m.id" 
              class="list-item" 
              :class="{ active: activeChat.id === m.id && activeChat.type === 'dm' }"
              @click="selectChat(m, 'dm')"
            >
              <div class="avatar-status-wrapper">
                <el-avatar :size="24" :src="m.avatar">{{ m.name.charAt(0) }}</el-avatar>
                <span class="status-dot" :class="m.status"></span>
              </div>
              <div class="flex flex-col text-left overflow-hidden ml-2">
                <span class="item-name truncate">{{ m.name }}</span>
                <span class="text-xs text-muted truncate">{{ m.statusText || (m.status === 'online' ? 'Online' : 'Offline') }}</span>
              </div>
              <el-badge v-if="m.unread" :value="m.unread" class="ml-auto" />
            </button>
          </div>
        </div>
      </div>

      <!-- Connected Voice Control Panel (Discord style) -->
      <div v-if="activeVoiceChannel" class="connected-voice-panel mt-auto">
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
          <span class="active-icon">{{ activeChat.type === 'channel' ? '#' : '@' }}</span>
          <div>
            <h4 class="font-semibold text-primary leading-tight">{{ activeChat.name }}</h4>
            <p class="text-xs text-muted leading-none">
              {{ activeChat.type === 'channel' ? activeChat.desc : (activeChat.status === 'online' ? 'Đang hoạt động' : 'Ngoại tuyến') }}
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
          <button class="action-btn" :title="currentTab === 'dm' ? 'Tạo nhóm Server' : 'Thành viên'" @click="currentTab === 'dm' ? openCreateServerFromDmModal() : toggleMembersSidebar()">
            <i class="fa-solid fa-users text-lg"></i>
          </button>
        </div>
      </div>

      <!-- Main body layout with horizontal partition for Discord style members list -->
      <div style="display: flex; flex: 1; min-height: 0; width: 100%;">
        <!-- Chat Area (Messages + Input) -->
        <div style="display: flex; flex-direction: column; flex: 1; min-width: 0; height: 100%;">
          <!-- Messages View -->
          <div ref="messageThread" class="messages-thread">
            <div 
              v-for="(msg, idx) in activeMessages" 
              :key="idx" 
              class="message-card"
              :class="{ 'mine': msg.senderId === currentUser.id }"
            >
              <el-avatar :size="32" :src="msg.senderAvatar" class="flex-shrink-0">
                {{ msg.senderName.charAt(0) }}
              </el-avatar>
              <div class="message-body">
                <div class="message-meta">
                  <span class="sender-name">{{ msg.senderName }}</span>
                  <span class="send-time">{{ formatTime(msg.sentAt) }}</span>
                </div>
                <div class="message-content">
                  <p>{{ msg.content }}</p>
                  
                  <!-- File Attachment Preview -->
                  <div v-if="msg.attachment" class="attachment-preview-container mt-2">
                    <div v-if="isImageFile(msg.attachment.name)" class="image-attachment">
                      <img :src="msg.attachment.url" class="max-w-xs max-h-48 rounded border border-slate-700/50" />
                    </div>
                    <div v-else class="attachment-preview flex items-center p-2 rounded bg-slate-800/60 border border-slate-700/40">
                      <i :class="getFileIconClass(msg.attachment.name)" class="text-2xl mr-2"></i>
                      <div class="flex flex-col overflow-hidden min-w-0">
                        <span class="text-xs font-semibold truncate text-primary">{{ msg.attachment.name }}</span>
                        <span class="text-xxs text-muted">{{ msg.attachment.size }}</span>
                      </div>
                      <a :href="msg.attachment.url" :download="msg.attachment.name" class="ml-auto text-xs text-primary hover:underline" style="display: flex; align-items: center; gap: 4px;">
                        <i class="fa-solid fa-download"></i> Tải xuống
                      </a>
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
              @change="handleFileChange" 
            />

            <!-- Attached File Preview Bar -->
            <div v-if="attachedFile" class="attached-file-preview-bar">
              <i :class="getFileIconClass(attachedFile.name)" class="text-xl"></i>
              <span class="text-xs truncate font-semibold text-secondary" style="max-width: 260px; margin-left: 6px; margin-right: 6px;">{{ attachedFile.name }}</span>
              <span class="text-xxs text-muted">({{ attachedFile.size }})</span>
              <button class="remove-attachment-btn ml-auto" @click="removeAttachedFile" title="Gỡ file đính kèm">
                <i class="fa-solid fa-xmark"></i>
              </button>
            </div>

            <div class="input-actions-bar">
              <el-button size="small" class="btn-secondary" title="Đính kèm file" @click="triggerAttachment">
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

              <span class="text-xs text-muted ml-2" v-if="isTyping">Ai đó đang nhập...</span>
            </div>
            <div class="input-form">
              <input 
                v-model="newMessage" 
                type="text" 
                placeholder="Gửi tin nhắn hoặc gõ / để mở trợ giúp..." 
                class="chat-input w-full"
                @keyup.enter="sendMessage"
              />
              <button class="btn-send" @click="sendMessage">
                <i class="fa-solid fa-paper-plane"></i>
              </button>
            </div>
          </div>
        </div>

        <!-- Right Server Members Sidebar -->
        <div v-if="showMembersSidebar && currentTab === 'channel'" class="members-sidebar-right">
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
          />
        </div>
        <div style="display: flex; flex-direction: column; gap: 8px;">
          <label style="font-size: 13px; font-weight: 600; color: var(--color-text-secondary);">Mô tả kênh</label>
          <input 
            v-model="newChannelDesc" 
            placeholder="Mô tả mục đích của kênh này..." 
            class="custom-friend-input"
            style="width: 100%; height: 38px;"
          />
        </div>
      </div>
      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 10px;">
          <el-button @click="createChannelActive = false">Hủy</el-button>
          <button class="btn-save" @click="createNewChannel">Tạo Kênh</button>
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
import { ref, onMounted, nextTick, watch, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'
import { useI18n } from '@/composables/useI18n'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()
const currentTab = computed(() => route.query.tab || 'channel')

const defaultServers = [
  { id: 'srv-sprinta', name: 'SprintA Workspace', color: '#6366f1', channels: [
    { id: 'ch-general', name: 'general', desc: 'Thảo luận chung của cả đội', unread: 0 },
    { id: 'ch-frontend', name: 'frontend-dev', desc: 'Nơi thảo luận về giao diện Vue 3 + Element Plus', unread: 2 }
  ], voiceChannels: [
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
  { id: 'srv-gaming', name: 'Góc Giải Trí 🎮', color: '#10b981', channels: [
    { id: 'ch-chat', name: 'tán-gẫu', desc: 'Tán gẫu ngoài giờ làm việc', unread: 0 }
  ], voiceChannels: [
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

const channels = computed(() => activeServer.value ? activeServer.value.channels : [])
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
  newChannelName.value = ''
  newChannelDesc.value = ''
  createChannelActive.value = true
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
    channels: [
      { id: `ch-gen-${Date.now()}`, name: 'general', desc: 'Thảo luận chung của Server', unread: 0 }
    ],
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
    channels: [
      { id: `ch-gen-${Date.now()}`, name: 'general', desc: `Kênh thảo luận nhóm của ${dmServerName.value.trim()}`, unread: 0 }
    ],
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
const createNewChannel = () => {
  if (!newChannelName.value.trim()) {
    ElMessage.warning('Vui lòng nhập tên kênh!')
    return
  }
  const nameFormatted = newChannelName.value.trim().toLowerCase().replace(/\s+/g, '-')
  const newCh = {
    id: `ch-custom-${Date.now()}`,
    name: nameFormatted,
    desc: newChannelDesc.value.trim() || 'Kênh trao đổi tùy chỉnh',
    unread: 0
  }
  activeServer.value.channels.push(newCh)
  saveServers()
  createChannelActive.value = false
  selectChat(newCh, 'channel')
  ElMessage.success(`Đã tạo kênh chat #${nameFormatted}`)
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

const currentUser = ref({
  id: '',
  name: 'Đoàn Minh Quân',
  avatar: ''
})

const defaultMessages = {
  'ch-general': [
    { senderId: 'user-kiet', senderName: 'Nguyễn Tuấn Kiệt', senderAvatar: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&q=80&w=128', content: 'Chào mọi người, chúc một tuần mới làm việc hiệu quả!', sentAt: new Date(Date.now() - 3600000 * 4).toISOString() },
    { senderId: 'user-quan', senderName: 'Đoàn Minh Quân', senderAvatar: '', content: 'Chào cả nhà, hôm nay mình bắt đầu thiết kế Module Team Collaboration nhé.', sentAt: new Date(Date.now() - 3600000 * 3).toISOString() }
  ],
  'ch-frontend': [
    { senderId: 'user-phat', senderName: 'Trần Gia Phát', senderAvatar: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&q=80&w=128', content: 'Cậu ơi, đã hoàn thành import CSS Variables chưa?', sentAt: new Date(Date.now() - 3600000 * 2).toISOString() },
    { senderId: 'user-phat', senderName: 'Trần Gia Phát', senderAvatar: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&q=80&w=128', content: 'Tớ có gửi kèm file thiết kế UI ở đây.', sentAt: new Date(Date.now() - 3600000 * 1.9).toISOString(), attachment: { name: 'UI_Specification_v2.pdf', size: '2.4 MB' } }
  ],
  'user-phat': [
    { senderId: 'user-phat', senderName: 'Trần Gia Phát', senderAvatar: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&q=80&w=128', content: 'Quân ơi, rảnh thì call video thảo luận vụ setup SignalR một chút nhé.', sentAt: new Date(Date.now() - 3600000).toISOString() }
  ]
}

const loadMessages = () => {
  const stored = localStorage.getItem('collaboration_messages')
  if (stored) {
    try {
      return JSON.parse(stored)
    } catch (e) {
      console.error(e)
    }
  }
  return defaultMessages
}

const mockMessages = ref(loadMessages())

const saveMessages = () => {
  localStorage.setItem('collaboration_messages', JSON.stringify(mockMessages.value))
}

const activeChat = ref({
  id: 'ch-general',
  name: 'general',
  type: 'channel',
  desc: 'Thảo luận chung của cả đội'
})

const activeMessages = ref([])
const newMessage = ref('')
const messageThread = ref(null)
const videoCallActive = ref(false)
const isTyping = ref(false)

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

onMounted(async () => {
  try {
    // 1. Get current user profile
     const meRes = await axiosClient.get('/users/me')
     if (meRes.data && meRes.data.data) {
       const me = meRes.data.data
       currentUser.value = {
         id: me.id,
         name: me.fullName,
         avatar: me.avatarUrl || ''
       }
       // Helper to remove accents
       const stripAccents = (str) => {
         if (!str) return '';
         return str.normalize('NFD')
                   .replace(/[\u0300-\u036f]/g, '')
                   .replace(/[đĐ]/g, m => m === 'đ' ? 'd' : 'D');
       }
       const parts = (me.fullName || 'USER').trim().split(/\s+/)
       const last = parts[parts.length - 1]
       const clean = stripAccents(last).toUpperCase().replace(/[^A-Z0-9]/g, '')
       const suffix = me.id ? me.id.substring(me.id.length - 4).toUpperCase() : '9982'
       myFriendCode.value = `${clean}-${suffix}`
     }
  } catch (e) {
    console.error('Cannot load current user profile:', e)
  }

  try {
    // 2. Get list of active users/members
    const usersRes = await axiosClient.get('/users', { params: { pageSize: 100 } })
    if (usersRes.data && usersRes.data.data) {
      members.value = usersRes.data.data.map(u => ({
        id: u.id,
        name: u.fullName,
        status: 'online',
        statusText: u.jobTitle || 'Thành viên',
        avatar: u.avatarUrl || '',
        unread: 0
      })).filter(u => u.id !== currentUser.value.id)
    }
  } catch (e) {
    console.error('Cannot load team members:', e)
  }

  try {
    // 3. Get departments as team channels
    const depRes = await axiosClient.get('/departments')
    if (depRes.data && depRes.data.data) {
      channels.value = depRes.data.data.map(d => ({
        id: d.id,
        name: d.name.toLowerCase().replace(/\s+/g, '-'),
        desc: d.description || `Kênh trao đổi của phòng ${d.name}`,
        unread: 0
      }))
    } else {
      channels.value = [
        { id: 'ch-general', name: 'general', desc: 'Thảo luận chung của cả đội', unread: 0 },
        { id: 'ch-frontend', name: 'frontend-dev', desc: 'Nơi thảo luận về giao diện Vue 3 + Element Plus', unread: 2 }
      ]
    }
  } catch (e) {
    channels.value = [
      { id: 'ch-general', name: 'general', desc: 'Thảo luận chung của cả đội', unread: 0 },
      { id: 'ch-frontend', name: 'frontend-dev', desc: 'Nơi thảo luận về giao diện Vue 3 + Element Plus', unread: 2 }
    ]
  }

  // Set active chat initially based on selected tab query
  if (currentTab.value === 'dm') {
    if (members.value.length > 0) {
      selectChat(members.value[0], 'dm')
    }
  } else {
    if (channels.value.length > 0) {
      selectChat(channels.value[0], 'channel')
    }
  }
})

watch(() => route.query.tab, (newTab) => {
  if (newTab === 'dm') {
    if (members.value.length > 0) {
      selectChat(members.value[0], 'dm')
    }
  } else {
    if (channels.value.length > 0) {
      selectChat(channels.value[0], 'channel')
    }
  }
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
const attachedFile = ref(null)

const triggerAttachment = () => {
  if (fileInputRef.value) {
    fileInputRef.value.click()
  }
}

const handleFileChange = (e) => {
  const file = e.target.files[0]
  if (!file) return
  
  let sizeStr = `${(file.size / 1024).toFixed(1)} KB`
  if (file.size > 1024 * 1024) {
    sizeStr = `${(file.size / (1024 * 1024)).toFixed(1)} MB`
  }
  
  const fileUrl = URL.createObjectURL(file)
  attachedFile.value = {
    name: file.name,
    size: sizeStr,
    type: file.type,
    url: fileUrl,
    rawFile: file
  }
  
  e.target.value = ''
}

const removeAttachedFile = () => {
  if (attachedFile.value && attachedFile.value.url) {
    URL.revokeObjectURL(attachedFile.value.url)
  }
  attachedFile.value = null
}

const isImageFile = (fileName) => {
  if (!fileName) return false
  const ext = fileName.split('.').pop().toLowerCase()
  return ['jpg', 'jpeg', 'png', 'gif', 'webp', 'svg'].includes(ext)
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

const insertEmoji = (emoji) => {
  newMessage.value += emoji
}

const sendMessage = () => {
  if (!newMessage.value.trim() && !attachedFile.value) return
  
  const msgObj = {
    senderId: currentUser.value.id || 'user-quan',
    senderName: currentUser.value.name || 'Đoàn Minh Quân',
    senderAvatar: currentUser.value.avatar || '',
    content: newMessage.value,
    sentAt: new Date().toISOString()
  }
  
  if (attachedFile.value) {
    msgObj.attachment = {
      name: attachedFile.value.name,
      size: attachedFile.value.size,
      url: attachedFile.value.url,
      type: attachedFile.value.type
    }
    attachedFile.value = null
  }
  
  const chatId = activeChat.value.id
  if (!mockMessages.value[chatId]) {
    mockMessages.value[chatId] = []
  }
  mockMessages.value[chatId].push(msgObj)
  saveMessages()
  
  newMessage.value = ''
  nextTick(() => {
    scrollToBottom()
  })
  
  // Simulate response for DM
  if (activeChat.value.type === 'dm') {
    simulatePartnerResponse(activeChat.value.id, activeChat.value.name, activeChat.value.avatar)
  }
}

const selectChat = (item, type) => {
  activeChat.value = {
    id: item.id,
    name: item.name,
    type: type,
    desc: item.desc || (type === 'dm' ? `Cuộc hội thoại trực tiếp với ${item.name}` : ''),
    avatar: item.avatar || ''
  }
  
  // Mark as read
  item.unread = 0
  
  // Load messages
  if (!mockMessages.value[item.id]) {
    mockMessages.value[item.id] = []
  }

  // Self-healing clean up: if it's a DM, only keep messages from the current user or the selected partner
  if (type === 'dm') {
    mockMessages.value[item.id] = mockMessages.value[item.id].filter(msg => {
      return msg.senderId === item.id || msg.senderId === currentUser.value.id || msg.senderId === 'user-quan'
    })
    saveMessages()
  }
  
  activeMessages.value = mockMessages.value[item.id]
  
  nextTick(() => {
    scrollToBottom()
  })
}

const scrollToBottom = () => {
  if (messageThread.value) {
    messageThread.value.scrollTop = messageThread.value.scrollHeight
  }
}

const simulatePartnerResponse = (partnerId, partnerName, partnerAvatar) => {
  isTyping.value = true
  setTimeout(() => {
    isTyping.value = false
    const responses = [
      'Tớ nhận được rồi nhé, thiết kế nhìn rất hiện đại!',
      'Ok cậu, lát tớ review lại rồi báo lại nhé.',
      'Cậu có cần chỉnh sửa gì thêm về phần API không?',
      'Bên tớ đang test thử, mọi thứ chạy rất mượt.',
      'Tuyệt vời! Cảm ơn cậu nhiều nhé.'
    ]
    const randomReply = responses[Math.floor(Math.random() * responses.length)]
    
    const replyObj = {
      senderId: partnerId,
      senderName: partnerName,
      senderAvatar: partnerAvatar || '',
      content: randomReply,
      sentAt: new Date().toISOString()
    }
    
    if (!mockMessages.value[partnerId]) {
      mockMessages.value[partnerId] = []
    }
    mockMessages.value[partnerId].push(replyObj)
    saveMessages()
    
    if (activeChat.value.id === partnerId) {
      nextTick(() => {
        scrollToBottom()
      })
    }
  }, 2000)
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
  
  // Add to members list
  if (!members.value.some(m => m.id === req.id)) {
    members.value.push({
      id: req.id,
      name: req.name,
      status: 'online',
      statusText: 'Đã kết bạn',
      avatar: req.avatar || '',
      unread: 0
    })
  }
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

.active-info {
  display: flex;
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
  height: 40px !important;
  padding-inline: 12px !important;
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

  .chat-sidebar {
    width: 100%;
    max-height: 240px;
    border-right: 0;
    border-bottom: 1px solid var(--color-border);
  }

  .messages-thread {
    min-height: 420px;
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
}

.image-attachment img {
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
  transition: transform 0.2s ease;
  cursor: pointer;
}

.image-attachment img:hover {
  transform: scale(1.02);
}

.attached-file-preview-bar {
  display: flex;
  align-items: center;
  background-color: color-mix(in srgb, var(--color-primary) 5%, var(--color-surface));
  border-radius: 8px 8px 0 0;
  padding: 8px 12px;
  border-bottom: 1px solid var(--color-border);
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









