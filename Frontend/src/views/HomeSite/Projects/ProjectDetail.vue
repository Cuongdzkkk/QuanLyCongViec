<template>
  <div class="project-detail-wrapper" v-if="project">
    <!-- Module Header (matching the list view) -->
    <header class="module-header">
      <div class="header-content">
        <h1>Dự án</h1>
        <div class="header-actions">
          <button class="primary-btn">Tạo dự án</button>
        </div>
      </div>
      
      <div class="tabs-nav">
        <router-link to="/home/projects" class="tab-link">Tất cả dự án</router-link>
        <router-link to="/home/projects" class="tab-link">Đang theo dõi</router-link>
        <router-link to="/home/projects" class="tab-link">Đã lưu trữ</router-link>
      </div>
    </header>

    <!-- Entity Header -->
    <header class="project-header">
      <div class="project-header-inner">
        <!-- Entity Main info -->
        <div class="header-main">
          <div class="title-block">
            <ProjectAvatar :icon="project.icon" :background="project.cover" size="lg" />
            <h1>{{ project.title }}</h1>
          </div>
          
          <div class="header-actions">
            <span class="status-badge" :class="getStatusClass(project.statusLabel || project.status)">
              {{ project.statusLabel || (project.status ? 'ĐANG HOẠT ĐỘNG' : 'TẠM DỪNG') }}
            </span>
            <div class="target-date-badge">
              <i class="fa-regular fa-calendar"></i> {{ project.startDate ? new Date(project.startDate).toLocaleDateString('vi-VN') : 'Chưa có ngày' }}
            </div>
            <button type="button" class="secondary-btn" @click.stop.prevent="toggleFollow" @keydown.enter.stop.prevent="toggleFollow">
              {{ project.isFollowing ? 'Bỏ theo dõi' : 'Theo dõi' }}
            </button>
            <button type="button" class="secondary-btn" @click.stop.prevent="openShareModal" @keydown.enter.stop.prevent="openShareModal">
              <i class="fa-solid fa-share-nodes"></i> Chia sẻ
            </button>
            <button class="icon-btn-header"><i class="fa-solid fa-link"></i></button>
            <button class="icon-btn-header"><i class="fa-solid fa-ellipsis"></i></button>
          </div>
        </div>
      </div>
      
      <!-- Tabs -->
      <div class="project-tabs-container">
        <div class="project-tabs">
          <button class="tab-btn" :class="{ active: currentTab === 'overview' }" @click="currentTab = 'overview'">Giới thiệu</button>
          <button class="tab-btn" :class="{ active: currentTab === 'updates' }" @click="currentTab = 'updates'">Cập nhật <span class="tab-badge">1</span></button>
          <button class="tab-btn" :class="{ active: currentTab === 'learnings' }" @click="currentTab = 'learnings'">Bài học rút ra</button>
          <button class="tab-btn" :class="{ active: currentTab === 'risks' }" @click="currentTab = 'risks'">Rủi ro</button>
          <button class="tab-btn" :class="{ active: currentTab === 'decisions' }" @click="currentTab = 'decisions'">Quyết định</button>
        </div>
      </div>
    </header>

    <!-- Main Content Grid -->
    <div class="project-content-grid">
      <!-- Left Column: Main Information -->
      <div class="main-column tab-pane">
        
        <!-- Tab: Giới thiệu -->
        <template v-if="currentTab === 'overview'">
          <section class="content-section">
            <h4>Dự án chúng ta đang thực hiện</h4>
            <div v-if="!editing.description" @click="editing.description = true" class="editable-placeholder">
              <div v-if="project.description" v-html="project.description" class="prose"></div>
              <p v-else class="empty-text">Mô tả dự án này và công việc liên quan đến dự án.</p>
            </div>
            <RichTextEditor v-else v-model="project.description" @save="saveOverview('description')" @cancel="editing.description = false" placeholder="Mô tả dự án này và công việc liên quan đến dự án." />
          </section>

          <section class="content-section">
            <h4>Lý do thực hiện dự án</h4>
            <div v-if="!editing.reason" @click="editing.reason = true" class="editable-placeholder">
              <div v-if="project.reason" v-html="project.reason" class="prose"></div>
              <p v-else class="empty-text">Giải thích lý do vì sao công việc này được thực hiện và những lý do thúc đẩy công việc.</p>
            </div>
            <RichTextEditor v-else v-model="project.reason" @save="saveOverview('reason')" @cancel="editing.reason = false" placeholder="Giải thích lý do vì sao công việc này được thực hiện và những lý do thúc đẩy công việc." />
          </section>

          <section class="content-section">
            <h4>Cách để biết rằng chúng ta đã thành công</h4>
            <div v-if="!editing.success" @click="editing.success = true" class="editable-placeholder">
              <div v-if="project.success" v-html="project.success" class="prose"></div>
              <p v-else class="empty-text">Mô tả thành công và mục tiêu bạn hy vọng đạt được.</p>
            </div>
            <RichTextEditor v-else v-model="project.success" @save="saveOverview('success')" @cancel="editing.success = false" placeholder="Mô tả thành công và mục tiêu bạn hy vọng đạt được." />
          </section>

          
          <!-- Hoạt động -->
          <section class="content-section" style="margin-top: 16px;">
            <div class="section-header" style="border: none; padding-bottom: 0;">
              <h3>Hoạt động</h3>
            </div>
            <div style="display: flex; gap: 8px; margin-bottom: 16px;">
              <button class="toggle-btn" :class="{ active: activityTab === 'comments' }" @click="activityTab = 'comments'">Nhận xét</button>
              <button class="toggle-btn" :class="{ active: activityTab === 'history' }" @click="activityTab = 'history'">Lịch sử</button>
            </div>
            
            <div class="section-body">
              <div v-if="activityTab === 'comments'">
                <CommentSection :entity-id="route.params.id" entity-type="Project" />
              </div>
              <div v-else>
                <div class="timeline-item" v-for="entry in projectHistory" :key="entry.id" style="display: flex; align-items: flex-start; gap: 12px; margin-bottom: 16px;">
                   <UserAvatar :user="{ fullName: entry.actor, email: entry.email }" :size="24" :fontSize="10" />
                   <div style="flex: 1;">
                      <div style="display: flex; justify-content: space-between; align-items: center;">
                         <span style="font-size: 14px; color: #172B4D;"><strong>{{ entry.actor }}</strong> {{ entry.action }}</span>
                         <span style="font-size: 12px; color: #5E6C84;">{{ formatDate(entry.createdAt) }}</span>
                      </div>
                      <div v-if="entry.target" style="font-size: 14px; color: #5E6C84; margin-top: 4px;">{{ entry.target }}</div>
                   </div>
                </div>
                <div v-if="projectHistory.length === 0" style="color: #6B778C; font-size: 14px; padding: 12px 0;">Chưa có hoạt động nào.</div>
              </div>
            </div>
          </section>
        </template>

        <!-- Tab: Cập nhật -->
        <template v-if="currentTab === 'updates'">
          <div class="updates-header">
            <h4>Lịch sử dự án</h4>
            <span class="last-update-text">Lần cập nhật gần nhất: {{ projectUpdates.length ? formatDate(projectUpdates[0].createdAt) : 'Chưa có' }}</span>
          </div>

          <div class="timeline-visual">
            <div class="timeline-line"></div>
            <div class="timeline-node current">
              <i class="fa-solid fa-user-group"></i>
              <span>Tuần này</span>
            </div>
          </div>

          <div class="update-editor-box">
            <div class="update-editor-header">
              <div class="editor-field">
                <label>Trạng thái hiện tại</label>
                <div class="status-dropdown-wrapper">
                  <span class="status-badge" :class="getStatusClass(newProjectUpdate.status)" @click="showStatusMenu = !showStatusMenu" style="cursor: pointer;">
                    {{ newProjectUpdate.status }} <i class="fa-solid fa-chevron-down ms-1"></i>
                  </span>
                  <div class="status-dropdown-menu" v-if="showStatusMenu">
                    <div class="status-option" @click="selectProjectStatus('ĐANG CHỜ XỬ LÝ')"><span class="status-badge status-pending">ĐANG CHỜ XỬ LÝ</span></div>
                    <div class="status-option" @click="selectProjectStatus('ĐÚNG TIẾN ĐỘ')"><span class="status-badge status-on-track">ĐÚNG TIẾN ĐỘ</span></div>
                    <div class="status-option" @click="selectProjectStatus('CÓ RỦI RO')"><span class="status-badge status-at-risk">CÓ RỦI RO</span></div>
                    <div class="status-option" @click="selectProjectStatus('KHÔNG ĐÚNG TIẾN ĐỘ')"><span class="status-badge status-off-track">KHÔNG ĐÚNG TIẾN ĐỘ</span></div>
                    <div class="status-option" @click="selectProjectStatus('ĐÃ HOÀN TẤT')"><span class="status-badge status-done">ĐÃ HOÀN TẤT <i class="fa-solid fa-flag ms-1"></i></span></div>
                    <div class="divider"></div>
                    <div class="status-option text-option" @click="selectProjectStatus('ĐÃ TẠM DỪNG')">ĐÃ TẠM DỪNG</div>
                    <div class="status-option text-option" @click="selectProjectStatus('ĐÃ HỦY')">ĐÃ HỦY</div>
                  </div>
                </div>
              </div>
              <div class="editor-field">
                <label>Ngày mục tiêu</label>
                <div class="target-date-badge date-dropdown">
                  <i class="fa-regular fa-calendar"></i> {{ project.endDate ? new Date(project.endDate).toLocaleDateString('vi-VN') : 'Không có' }}
                </div>
              </div>
              <div class="editor-field template-link">
                <span>Mẫu <i class="fa-solid fa-chevron-down ms-1"></i></span>
              </div>
            </div>
            
            <div class="update-editor-body">
              <textarea v-model="newProjectUpdate.text" placeholder="Viết bản cập nhật gồm tối đa 280 ký tự." rows="4"></textarea>
            </div>
            
            <div class="update-editor-footer">
              <div class="editor-tools">
                <span class="tool-ai"><i class="fa-solid fa-wand-magic-sparkles"></i> Soạn thảo bằng Rovo</span>
                <button class="tool-btn"><i class="fa-solid fa-plus"></i></button>
                <button class="tool-btn"><i class="fa-solid fa-image"></i></button>
                <button class="tool-btn"><i class="fa-solid fa-at"></i></button>
                <button class="tool-btn"><i class="fa-solid fa-link"></i></button>
              </div>
              <div class="editor-actions">
                <span class="char-count">{{ newProjectUpdate.text.length }}/280 <i class="fa-solid fa-circle-question"></i></span>
                <button class="secondary-btn"><i class="fa-solid fa-users"></i> 1</button>
                <button class="secondary-btn" @click="newProjectUpdate.text = ''">Lưu bản nháp</button>
                <button class="primary-btn" :disabled="!newProjectUpdate.text.trim()" @click="submitProjectUpdate">Đăng bản cập nhật</button>
              </div>
            </div>
          </div>

          <div v-if="projectUpdates.length === 0" class="jira-empty-box" style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 24px; display: flex; align-items: flex-start; gap: 24px;">
             <div style="position: relative;">
                <div style="width: 80px; height: 80px; background-color: #EBECF0; border-radius: 8px; display: flex; align-items: center; justify-content: center; transform: rotate(-5deg);">
                   <i class="fa-regular fa-message" style="font-size: 32px; color: #172B4D;"></i>
                </div>
             </div>
             <div style="flex: 1; position: relative;">
                <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Chưa có bản cập nhật nào.</h4>
                <p style="font-size: 13px; color: #6B778C; margin-bottom: 0; line-height: 1.5;">Các bản cập nhật dự án sẽ xuất hiện ở đây sau khi được đăng.</p>
             </div>
          </div>

          <div v-else class="timeline-posts">
            <div class="timeline-post" style="margin-bottom: 24px; border: 1px solid #DFE1E6; padding: 16px; border-radius: 3px; background: white;" v-for="update in projectUpdates" :key="update.id">
              <div class="post-header">
                <div class="post-user">
                  <UserAvatar :user="{ id: update.creatorId, fullName: update.creatorName || update.authorName || project.owner, avatarUrl: update.creatorAvatarUrl, email: update.creatorEmail || update.authorEmail || update.creatorId }" :size="32" :fontSize="12" />
                  <div class="user-info">
                    <span class="user-name">{{ update.creatorName || update.authorName || project.owner || 'Người cập nhật' }}</span>
                    <span class="post-time">{{ formatDate(update.createdAt || update.updatedAt) }}</span>
                  </div>
                </div>
                <div class="post-status-meta">
                    <span class="status-badge" :class="getStatusClass(update.title || update.status || project.status)">{{ update.title || update.status || project.status || 'Đang chờ xử lý' }}</span>
                </div>
              </div>
              <div class="post-content">
                <p>{{ update.text || update.content || update.message || update.summary }}</p>
                <div class="status-change-log" v-if="getPreviousStatus(update)" style="margin-top: 8px;">
                  <template v-if="getPreviousStatus(update) === getCurrentStatus(update)">
                    Đã giữ nguyên trạng thái <span class="status-badge mx-1" :class="getStatusClass(getCurrentStatus(update))">{{ getCurrentStatus(update) }}</span>
                  </template>
                  <template v-else>
                    Đã thay đổi trạng thái <span class="status-badge mx-1" :class="getStatusClass(getPreviousStatus(update))">{{ getPreviousStatus(update) }}</span> <i class="fa-solid fa-arrow-right mx-1"></i> <span class="status-badge mx-1" :class="getStatusClass(getCurrentStatus(update))">{{ getCurrentStatus(update) }}</span>
                  </template>
                </div>
              </div>
              <CommentSection :entity-id="update.id" entity-type="ProjectUpdate" />
            </div>
            <h4 class="timeline-period">Tuần này</h4>
            
          </div>
        </template>

        <!-- Tab: Bài học rút ra -->
        <template v-if="currentTab === 'learnings'">
          <div class="section-header-row">
            <h3>Bài học rút ra</h3>
          </div>
          <div v-if="!editing.learnings && !projectLessons.length" class="jira-empty-box" style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 24px; display: flex; align-items: flex-start; gap: 24px;">
             <div style="position: relative;">
                <div style="width: 80px; height: 80px; background-color: #FFF0B3; border-radius: 8px; display: flex; align-items: center; justify-content: center; transform: rotate(-5deg);">
                   <i class="fa-regular fa-lightbulb" style="font-size: 32px; color: #FF991F;"></i>
                </div>
                <div style="position: absolute; bottom: -8px; right: -8px; width: 32px; height: 32px; background-color: #0052CC; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; cursor: pointer; box-shadow: 0 2px 4px rgba(0,0,0,0.1);" @click="editing.learnings = true">
                   <i class="fa-solid fa-plus" style="font-size: 16px;"></i>
                </div>
             </div>
             <div style="flex: 1; position: relative;">
                <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Những bộ óc vĩ đại có tư duy giống nhau sẽ chia sẻ kiến thức của họ</h4>
                <p style="font-size: 13px; color: #6B778C; margin-bottom: 16px; line-height: 1.5;">Chia sẻ những gì bạn đã học được với công ty của bạn để giúp những người khác có một khởi đầu thuận lợi khi làm việc trên các dự án tương tự.</p>
                <div style="position: relative; display: inline-block;">
                  <button class="secondary-btn" type="button" @click="editing.learnings = true">Thêm bài học rút ra mới</button>
                </div>
             </div>
          </div>
          <div v-else>
            <div v-if="editing.learnings" class="tab-item-editor" style="margin-bottom: 24px; padding-top: 16px;">
                <RichTextEditor v-model="newProjectItem.text" @save="saveProjectLesson" @cancel="cancelProjectItem('learnings')" placeholder="Dùng không gian này để chia sẻ bài học rút ra...">
                  <template #header>
                    <div style="display: flex; align-items: center; gap: 12px; padding: 12px 16px; border-bottom: 1px solid #DFE1E6; background-color: #fff;">
                      <i class="fa-regular fa-lightbulb" style="color: #FFAB00; font-size: 18px;"></i>
                      <input type="text" v-model="newProjectItem.title" placeholder="Tóm tắt cho bài học rút ra của bạn là gì?" style="border: none; outline: none; background: transparent; width: 100%; font-size: 15px; font-weight: 500; color: #172B4D;" />
                    </div>
                  </template>
                </RichTextEditor>
            </div>
            <div v-if="!editing.learnings && projectLessons.length" style="margin-bottom: 24px; padding-top: 16px;">
                <button class="secondary-btn" @click="editing.learnings = true">Thêm bài học</button>
            </div>
            
            <div class="timeline-post" v-for="item in projectLessons" :key="item.id" style="margin-bottom: 16px;">
                <div class="post-header">
                  <div class="post-user">
                    <UserAvatar :user="{ id: item.creatorId, fullName: item.creatorName, avatarUrl: item.creatorAvatarUrl, email: item.creatorEmail }" :size="32" :fontSize="12" />
                    <div class="user-info">
                      <span class="user-name">{{ item.creatorName }}</span>
                      <span class="post-time">{{ formatDate(item.createdAt) }}</span>
                    </div>
                  </div>
                </div>
                <div class="post-content">
                  <h4 style="margin: 0 0 8px 0; color: #172B4D; font-size: 16px;"><i class="fa-regular fa-lightbulb" style="color: #FFAB00; margin-right: 6px;"></i> {{ item.title }}</h4>
                  <div v-html="sanitizeHtml(item.text)"></div>
                </div>
            </div>
          </div>
        </template>

        <!-- Tab: Rủi ro -->
        <template v-if="currentTab === 'risks'">
          <div class="section-header-row">
            <h3>Rủi ro</h3>
          </div>
          <div v-if="!editing.risks && !projectRisks.length" class="jira-empty-box" style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 24px; display: flex; align-items: flex-start; gap: 24px;">
             <div style="position: relative;">
                <div style="width: 80px; height: 80px; background-color: #FFEBE6; border-radius: 8px; display: flex; align-items: center; justify-content: center; transform: rotate(-5deg);">
                   <i class="fa-solid fa-triangle-exclamation" style="font-size: 32px; color: #FF5630;"></i>
                </div>
                <div style="position: absolute; bottom: -8px; right: -8px; width: 32px; height: 32px; background-color: #0052CC; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; cursor: pointer; box-shadow: 0 2px 4px rgba(0,0,0,0.1);" @click="editing.risks = true">
                   <i class="fa-solid fa-plus" style="font-size: 16px;"></i>
                </div>
             </div>
             <div style="flex: 1; position: relative;">
                <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Nắm bắt các rủi ro đã biết</h4>
                <p style="font-size: 13px; color: #6B778C; margin-bottom: 16px; line-height: 1.5;">Theo dõi mọi rủi ro liên quan đến dự án này để tránh những bất ngờ sau này.</p>
                <div style="position: relative; display: inline-block;">
                  <button class="secondary-btn" type="button" @click="editing.risks = true">Thêm rủi ro mới</button>
                </div>
             </div>
          </div>
          <div v-else>
            <div v-if="editing.risks" class="tab-item-editor" style="margin-bottom: 24px; padding-top: 16px;">
                <RichTextEditor v-model="newProjectItem.text" @save="saveProjectRisk" @cancel="cancelProjectItem('risks')" placeholder="Mô tả rủi ro...">
                  <template #header>
                    <div style="display: flex; align-items: center; gap: 12px; padding: 12px 16px; border-bottom: 1px solid #DFE1E6; background-color: #fff;">
                      <i class="fa-solid fa-triangle-exclamation" style="color: #FF5630; font-size: 18px;"></i>
                      <input type="text" v-model="newProjectItem.title" placeholder="Tóm tắt rủi ro là gì?" style="border: none; outline: none; background: transparent; width: 100%; font-size: 15px; font-weight: 500; color: #172B4D;" />
                    </div>
                  </template>
                </RichTextEditor>
            </div>
            <div v-if="!editing.risks && projectRisks.length" style="margin-bottom: 24px; padding-top: 16px;">
                <button class="secondary-btn" @click="editing.risks = true">Thêm rủi ro</button>
            </div>
            
            <div class="timeline-post" v-for="item in projectRisks" :key="item.id" style="margin-bottom: 16px;">
                <div class="post-header">
                  <div class="post-user">
                    <UserAvatar :user="{ id: item.creatorId, fullName: item.creatorName, avatarUrl: item.creatorAvatarUrl, email: item.creatorEmail }" :size="32" :fontSize="12" />
                    <div class="user-info">
                      <span class="user-name">{{ item.creatorName }}</span>
                      <span class="post-time">{{ formatDate(item.createdAt) }}</span>
                    </div>
                  </div>
                </div>
                <div class="post-content">
                  <h4 style="margin: 0 0 8px 0; color: #172B4D; font-size: 16px;"><i class="fa-solid fa-triangle-exclamation" style="color: #FF5630; margin-right: 6px;"></i> {{ item.title }}</h4>
                  <div v-html="sanitizeHtml(item.text)"></div>
                </div>
            </div>
          </div>
        </template>

        <!-- Tab: Quyết định -->
        <template v-if="currentTab === 'decisions'">
          <div class="section-header-row">
            <h3>Quyết định</h3>
          </div>
          <div v-if="!editing.decisions && !projectDecisions.length" class="jira-empty-box" style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 24px; display: flex; align-items: flex-start; gap: 24px;">
             <div style="position: relative;">
                <div style="width: 80px; height: 80px; background-color: #E3FCEF; border-radius: 8px; display: flex; align-items: center; justify-content: center; transform: rotate(-5deg);">
                   <i class="fa-solid fa-check-circle" style="font-size: 32px; color: #36B37E;"></i>
                </div>
                <div style="position: absolute; bottom: -8px; right: -8px; width: 32px; height: 32px; background-color: #0052CC; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; cursor: pointer; box-shadow: 0 2px 4px rgba(0,0,0,0.1);" @click="editing.decisions = true">
                   <i class="fa-solid fa-plus" style="font-size: 16px;"></i>
                </div>
             </div>
             <div style="flex: 1; position: relative;">
                <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Truyền đạt các quyết định lớn</h4>
                <p style="font-size: 13px; color: #6B778C; margin-bottom: 16px; line-height: 1.5;">Ghi lại các quyết định lớn cho dự án này tại đây để chia sẻ trong bản cập nhật mới nhất của bạn.</p>
                <div style="position: relative; display: inline-block;">
                  <button class="secondary-btn" type="button" @click="editing.decisions = true">Thêm quyết định mới</button>
                </div>
             </div>
          </div>
          <div v-else>
            <div v-if="editing.decisions" class="tab-item-editor" style="margin-bottom: 24px; padding-top: 16px;">
                <RichTextEditor v-model="newProjectItem.text" @save="saveProjectDecision" @cancel="cancelProjectItem('decisions')" placeholder="Mô tả quyết định...">
                  <template #header>
                    <div style="display: flex; align-items: center; gap: 12px; padding: 12px 16px; border-bottom: 1px solid #DFE1E6; background-color: #fff;">
                      <i class="fa-solid fa-check-circle" style="color: #36B37E; font-size: 18px;"></i>
                      <input type="text" v-model="newProjectItem.title" placeholder="Tóm tắt quyết định là gì?" style="border: none; outline: none; background: transparent; width: 100%; font-size: 15px; font-weight: 500; color: #172B4D;" />
                    </div>
                  </template>
                </RichTextEditor>
            </div>
            <div v-if="!editing.decisions && projectDecisions.length" style="margin-bottom: 24px; padding-top: 16px;">
                <button class="secondary-btn" @click="editing.decisions = true">Thêm quyết định</button>
            </div>
            
            <div class="timeline-post" v-for="item in projectDecisions" :key="item.id" style="margin-bottom: 16px;">
                <div class="post-header">
                  <div class="post-user">
                    <UserAvatar :user="{ id: item.creatorId, fullName: item.creatorName, avatarUrl: item.creatorAvatarUrl, email: item.creatorEmail }" :size="32" :fontSize="12" />
                    <div class="user-info">
                      <span class="user-name">{{ item.creatorName }}</span>
                      <span class="post-time">{{ formatDate(item.createdAt) }}</span>
                    </div>
                  </div>
                </div>
                <div class="post-content">
                  <h4 style="margin: 0 0 8px 0; color: #172B4D; font-size: 16px;"><i class="fa-solid fa-check-circle" style="color: #36B37E; margin-right: 6px;"></i> {{ item.title }}</h4>
                  <div v-html="sanitizeHtml(item.text)"></div>
                </div>
            </div>
          </div>
        </template>

      </div>
      <!-- Right Column: Sidebar Details -->
      <div class="side-column">
        <div class="details-body">
          <!-- Chủ sở hữu -->
          <div class="detail-row">
            <div class="detail-label">Chủ sở hữu</div>
            <div class="detail-value">
              <div class="owner-chip">
                <AppUserChip :name="projectOwner.name" :src="projectOwner.avatarUrl" compact />
              </div>
            </div>
          </div>
          
          <!-- Người đóng góp -->
          <div class="detail-row">
            <div class="detail-label">Người đóng góp <span class="badge-count">1</span> <button class="icon-btn-micro"><i class="fa-solid fa-plus"></i></button></div>
            <div class="detail-value">
              <div class="owner-chip">
                  <AppUserChip :name="projectOwner.name" :src="projectOwner.avatarUrl" compact />
                <div class="owner-info">
                  <span class="owner-role">{{ projectOwner.role }}</span>
                </div>
              </div>
            </div>
          </div>

          <!-- Người theo dõi -->
          <div class="detail-row">
            <div class="detail-label">Người theo dõi <button class="icon-btn-micro" @click="isShareModalOpen = true"><i class="fa-solid fa-plus"></i></button></div>
            <div class="detail-value flex-between">
              <span class="empty-value" style="cursor: pointer;" @click="isShareModalOpen = true">Thêm người theo dõi</span>
              <div class="follower-icons">
                <i class="fa-brands fa-slack ms-1"></i>
                <i class="fa-brands fa-microsoft ms-1"></i>
              </div>
            </div>
          </div>

          <!-- Đóng góp vào mục tiêu -->
          <div class="detail-row relative-popover-container">
            <div class="detail-label">Đóng góp vào mục tiêu <button class="icon-btn-micro" @click.stop="togglePopover('goal')"><i class="fa-solid fa-plus"></i></button></div>
            
            <div class="detail-value" v-if="linkedGoals.length > 0">
              <div class="linked-item" v-for="g in linkedGoals" :key="g.id">
                <i class="fa-solid fa-bullseye item-icon"></i>
                <span class="item-name">{{ g.name }}</span>
                <button class="remove-btn" @click="removeGoal(g)"><i class="fa-solid fa-xmark"></i></button>
              </div>
            </div>

            <!-- Goal Popover -->
            <div class="custom-popover" v-if="popovers.goal" @click.stop>
              <input type="text" class="popover-search" placeholder="Tìm kiếm mục tiêu hoặc dán liên kết" v-model="searchQueries.goal" />
              <div class="popover-list-title">Mục tiêu gần đây</div>
              <div class="popover-list">
                <div class="popover-item" v-for="g in availableGoals" :key="g.id" @click="addGoal(g)">
                  <i class="fa-solid fa-bullseye item-icon-muted"></i>
                  <div class="item-details">
                    <div class="item-name">{{ g.name }}</div>
                    <div class="item-meta">{{ g.owner }}</div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Các dự án liên quan -->
          <div class="detail-row relative-popover-container">
            <div class="detail-label">Các dự án liên quan <button class="icon-btn-micro" @click.stop="togglePopover('project')"><i class="fa-solid fa-plus"></i></button></div>
            
            <div class="detail-value" v-if="linkedProjects.length > 0">
              <div class="linked-item" v-for="p in linkedProjects" :key="p.id">
                <i class="fa-solid fa-rocket item-icon"></i>
                <span class="item-name">{{ p.name }}</span>
                <button class="remove-btn" @click="removeProject(p)"><i class="fa-solid fa-xmark"></i></button>
              </div>
            </div>

            <!-- Project Popover -->
            <div class="custom-popover" v-if="popovers.project" @click.stop>
              <div class="popover-select-control">Dự án liên quan đến <i class="fa-solid fa-chevron-down ms-auto"></i></div>
              <input type="text" class="popover-search mt-2" placeholder="Tìm kiếm dự án" v-model="searchQueries.project" />
              <div class="popover-list mt-2">
                <div class="popover-item" v-for="p in availableRelatedProjects" :key="p.id" @click="addProject(p)">
                  <i class="fa-solid fa-rocket item-icon-muted"></i>
                  <div class="item-name">{{ p.name }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Công việc được theo dõi ở đâu? -->
          <div class="detail-row relative-popover-container">
            <div class="detail-label">Công việc được theo dõi ở đâu? <button class="icon-btn-micro" @click.stop="togglePopover('tracked')"><i class="fa-solid fa-plus"></i></button></div>
            
            <div class="detail-value" v-if="linkedTrackedUrls.length > 0">
              <div class="linked-item" v-for="link in linkedTrackedUrls" :key="link.linkId">
                <i class="fa-solid fa-link item-icon"></i>
                <a :href="link.url" target="_blank" class="item-name truncate">{{ link.url }}</a>
                <button class="remove-btn" @click="removeLink(link)"><i class="fa-solid fa-xmark"></i></button>
              </div>
            </div>

            <!-- Tracked Work Popover -->
            <div class="custom-popover popover-large" v-if="popovers.tracked" @click.stop>
              <h4 class="popover-title">Công việc được theo dõi ở đâu?</h4>
              <p class="popover-desc">Tìm kiếm các hạng mục công việc Jira hoặc thêm liên kết đến các địa điểm mà bạn đang theo dõi công việc cho dự án này.</p>
              <input type="text" class="popover-search" placeholder="Tìm kiếm trên Jira hoặc thêm liên kết" v-model="searchQueries.tracked" />
              <div class="popover-list-title mt-2">KẾT QUẢ</div>
              <div class="popover-actions mt-3">
                <button class="secondary-btn" @click="popovers.tracked = false">Hủy</button>
                <button class="primary-btn" :disabled="!searchQueries.tracked" @click="addTrackedUrl">Thêm</button>
              </div>
            </div>
          </div>

          <!-- Liên kết -->
          <div class="detail-row relative-popover-container">
            <div class="detail-label">Liên kết <button class="icon-btn-micro" @click.stop="togglePopover('link')"><i class="fa-solid fa-plus"></i></button></div>
            
            <div class="detail-value" v-if="linkedTasks.length > 0">
              <div class="linked-item" v-for="l in linkedTasks" :key="l.id">
                <i class="fa-solid fa-file-lines item-icon text-blue"></i>
                <span class="item-name">{{ l.name }}</span>
                <button class="remove-btn" @click="removeTask(l.id)"><i class="fa-solid fa-xmark"></i></button>
              </div>
            </div>

            <!-- Links Popover -->
            <div class="custom-popover" v-if="popovers.link" @click.stop>
              <input type="text" class="popover-search" placeholder="Dán liên kết hoặc tìm nội dung vừa xem" v-model="searchQueries.link" />
              <div class="popover-list mt-2">
                <div class="popover-item" v-for="l in linkedTrackedUrls" :key="l.linkId" @click="selectExistingTrackedLink(l)">
                  <i class="fa-solid fa-file-lines item-icon-muted text-blue"></i>
                  <div class="item-name">{{ l.url }}</div>
                </div>
                <button class="primary-btn mt-2" :disabled="!searchQueries.link" @click="addTask">Thêm liên kết</button>
              </div>
            </div>
          </div>

          <!-- Ngày bắt đầu -->
          <div class="detail-row">
            <div class="detail-label">Ngày bắt đầu <button class="icon-btn-micro"><i class="fa-regular fa-pen-to-square"></i></button></div>
            <div class="detail-value"><span class="empty-value">{{ project.startDate ? new Date(project.startDate).toLocaleDateString('vi-VN') : 'Chưa có' }}</span></div>
          </div>
        </div>
      </div>
    </div>

    <!-- Share Modal -->
    <ShareModal 
      :is-open="isShareModalOpen" 
      :entityId="route.params.id" 
      entityType="Project"
      :entityName="project.title"
      :workspaceId="project.workspaceId"
      :owner="projectOwner"
      @close="isShareModalOpen = false" 
    />
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import { useHomeProjectStore } from '@/store/useHomeProjectStore'
import { useGoalStore } from '@/store/useGoalStore'
import { useStarredStore } from '@/store/useStarredStore'
import { STARRED_ENTITY_TYPES } from '@/api/starredRecentApi'
import axiosClient from '@/api/axiosClient'
import DOMPurify from 'dompurify'
import RichTextEditor from '@/components/common/RichTextEditor.vue'
import ShareModal from '@/components/common/ShareModal.vue'
import CommentSection from '@/components/common/CommentSection.vue'
import UserAvatar from '@/components/common/UserAvatar.vue'
import { AppUserChip } from '@/components/common/Foundation'
import ProjectAvatar from '@/components/project/ProjectAvatar.vue'
import { signalRService } from '@/api/signalrService'

const route = useRoute()
const projectStore = useHomeProjectStore()
const handleProjectLinkRealtime = event => {
  if (`${event?.entityType || ''}`.toLowerCase() !== 'projectlink') return
  if (event.projectId && `${event.projectId}` !== `${route.params.id}`) return
  loadProjectLinks()
}
const goalStore = useGoalStore()
const starredStore = useStarredStore()

const currentTab = ref('overview')
const activityTab = ref('comments')
const showStatusMenu = ref(false)
const sanitizeHtml = (value) => DOMPurify.sanitize(value || '')
const isShareModalOpen = ref(false)
const newProjectUpdate = ref({ status: 'ĐANG CHỜ XỬ LÝ', text: '' })
const newProjectItem = ref({ title: '', text: '' })

const popovers = ref({
  goal: false,
  project: false,
  tracked: false,
  link: false
})

const togglePopover = (type) => {
  for (const key in popovers.value) {
    if (key === type) popovers.value[key] = !popovers.value[key]
    else popovers.value[key] = false
  }
}

const closePopovers = (e) => {
  if (!e.target.closest('.custom-popover') && !e.target.closest('.icon-btn-micro')) {
    popovers.value = { goal: false, project: false, tracked: false, link: false }
  }
}

const linkedGoals = ref([])
const linkedProjects = ref([])
const linkedTrackedUrl = ref('')
const linkedTrackedUrls = ref([])
const linkedTasks = ref([])
const projectLinks = ref([])

const searchQueries = ref({ goal: '', project: '', tracked: '', link: '' })

const availableGoals = computed(() => {
  const linkedIds = new Set(linkedGoals.value.map(goal => goal.id))
  const q = searchQueries.value.goal.trim().toLowerCase()
  return (goalStore.goals || [])
    .filter(goal => goal.id && !linkedIds.has(goal.id))
    .filter(goal => !q || (goal.title || '').toLowerCase().includes(q))
    .map(goal => ({
      id: goal.id,
      name: goal.title,
      owner: goal.owner || goal.ownerName || goal.creatorName || ''
    }))
})

const availableRelatedProjects = computed(() => {
  const linkedIds = new Set(linkedProjects.value.map(project => project.id))
  const currentProjectId = route.params.id
  const q = searchQueries.value.project.trim().toLowerCase()
  return (projectStore.projects || [])
    .filter(project => project.id && project.id !== currentProjectId && !linkedIds.has(project.id))
    .filter(project => !q || (project.name || project.title || '').toLowerCase().includes(q))
    .map(project => ({ id: project.id, name: project.name || project.title }))
})

const projectUpdates = computed(() => projectStore.updates || [])
const projectHistory = computed(() => {
  const explicitHistory = projectStore.history || project.value?.history || []
  if (explicitHistory.length) {
    return explicitHistory.map((entry, index) => ({
      id: entry.id || index,
      actor: entry.actor || entry.creatorName || project.value?.creatorName || 'Người dùng',
      email: entry.creatorEmail || entry.authorEmail,
      action: entry.action || entry.title || 'đã cập nhật',
      target: entry.target || entry.content || '',
      createdAt: entry.createdAt
    }))
  }
  return projectUpdates.value.map(upd => ({
    id: upd.id,
    actor: upd.creatorName || upd.authorName || project.value?.owner || 'Người dùng',
    email: upd.creatorEmail || upd.authorEmail,
    action: 'đã đăng bản cập nhật',
    target: '',
    createdAt: upd.createdAt || upd.updatedAt
  }))
})
const projectLessons = computed(() => project.value.lessons || [])
const projectRisks = computed(() => project.value.risks || [])
const projectDecisions = computed(() => project.value.decisions || [])
const projectOwner = computed(() => ({
  name: project.value.owner || project.value.ownerName || project.value.leadName || project.value.creatorName || 'Chưa gán',
  role: project.value.ownerRole || project.value.myRole || 'Thành viên',
  id: project.value.ownerId || project.value.leadUserId || project.value.creatorId,
  fullName: project.value.owner || project.value.ownerName || project.value.leadName || project.value.creatorName,
  avatarUrl: project.value.ownerAvatarUrl || project.value.leadAvatarUrl || project.value.creatorAvatarUrl,
  avatarColor: project.value.ownerColor,
  email: project.value.ownerEmail || project.value.leadEmail || project.value.creatorEmail || project.value.ownerId
}))

const loadProjectLinks = async () => {
  const projectId = route.params.id
  if (!projectId) return
  const workspaceId = projectStore.getWorkspaceId()
  const response = await axiosClient.get(`/workspaces/${workspaceId}/projects/${projectId}/links`)
  projectLinks.value = response.data?.data || response.data || []
  linkedGoals.value = projectLinks.value
    .filter(link => link.linkedType === 'Goal' || link.LinkedType === 'Goal')
    .map(link => ({
      id: link.linkedId || link.LinkedId,
      linkId: link.id || link.Id,
      name: link.linkedName || link.itemName || 'Goal'
    }))
  linkedProjects.value = projectLinks.value
    .filter(link => link.linkedType === 'Project' || link.LinkedType === 'Project')
    .map(link => ({
      id: link.linkedId || link.LinkedId,
      linkId: link.id || link.Id,
      name: link.linkedName || link.itemName || 'Project'
    }))
  linkedTrackedUrls.value = projectLinks.value
    .filter(link => link.linkedType === 'TrackedLink' || link.LinkedType === 'TrackedLink')
    .map(link => ({
      linkId: link.id || link.Id,
      url: link.trackedUrl || link.TrackedUrl || link.linkedName
    }))
}

const addGoal = async (g) => {
  const projectId = route.params.id
  const workspaceId = projectStore.getWorkspaceId()
  await axiosClient.post(`/workspaces/${workspaceId}/projects/${projectId}/links`, {
    linkedType: 'Goal',
    linkedId: g.id
  })
  await loadProjectLinks()
  popovers.value.goal = false
}
const removeGoal = async (goal) => {
  const projectId = route.params.id
  const workspaceId = projectStore.getWorkspaceId()
  const linkId = goal.linkId || projectLinks.value.find(link =>
    (link.linkedId || link.LinkedId) === goal.id &&
    (link.linkedType || link.LinkedType) === 'Goal'
  )?.id
  if (!linkId) return
  await axiosClient.delete(`/workspaces/${workspaceId}/projects/${projectId}/links/${linkId}`)
  await loadProjectLinks()
}

const addProject = async (p) => {
  const projectId = route.params.id
  const workspaceId = projectStore.getWorkspaceId()
  await axiosClient.post(`/workspaces/${workspaceId}/projects/${projectId}/links`, {
    linkedType: 'Project',
    linkedId: p.id
  })
  await loadProjectLinks()
  popovers.value.project = false
}
const removeProject = async (project) => {
  await removeLink(project)
}

const addTrackedUrl = async () => {
  if (searchQueries.value.tracked) {
    const projectId = route.params.id
    const workspaceId = projectStore.getWorkspaceId()
    await axiosClient.post(`/workspaces/${workspaceId}/projects/${projectId}/links`, {
      linkedType: 'TrackedLink',
      trackedUrl: searchQueries.value.tracked
    })
    searchQueries.value.tracked = ''
    await loadProjectLinks()
    popovers.value.tracked = false
  }
}

const addTask = async () => {
  if (!searchQueries.value.link) return
  const projectId = route.params.id
  const workspaceId = projectStore.getWorkspaceId()
  await axiosClient.post(`/workspaces/${workspaceId}/projects/${projectId}/links`, {
    linkedType: 'TrackedLink',
    trackedUrl: searchQueries.value.link
  })
  searchQueries.value.link = ''
  await loadProjectLinks()
  popovers.value.link = false
}
const selectExistingTrackedLink = (link) => {
  searchQueries.value.link = link.url
}
const removeLink = async (link) => {
  const projectId = route.params.id
  const workspaceId = projectStore.getWorkspaceId()
  const linkId = link.linkId || link.id
  if (!linkId) return
  await axiosClient.delete(`/workspaces/${workspaceId}/projects/${projectId}/links/${linkId}`)
  await loadProjectLinks()
}
const removeTask = async (task) => { await removeLink(task) }

const editing = ref({
  description: false,
  reason: false,
  success: false,
  learnings: false,
  risks: false,
  decisions: false
})

const project = computed(() => projectStore.currentProject || {
  title: 'Đang tải dự án',
  description: '',
  reason: '',
  success: '',
  learnings: '',
  risks: '',
  decisions: ''
})

const saveOverview = async (field) => {
  if (!project.value?.id) return
  await projectStore.updateProjectOverview(project.value.id, {
    description: project.value.description,
    reason: project.value.reason,
    success: project.value.success,
    trackedLinkUrl: project.value.trackedLinkUrl
  })
  editing.value[field] = false
}

const selectProjectStatus = (status) => {
  newProjectUpdate.value.status = status
  showStatusMenu.value = false
}

const getPreviousStatus = (update) => update.oldStatus || update.previousStatus || update.OldStatus || update.PreviousStatus
const getCurrentStatus = (update) => update.newStatus || update.title || update.status || update.NewStatus || update.Title || update.Status

const submitProjectUpdate = async () => {
  if (!project.value?.id || !newProjectUpdate.value.text.trim()) return
  await projectStore.addProjectUpdate(project.value.id, {
    title: newProjectUpdate.value.status,
    status: newProjectUpdate.value.status,
    text: newProjectUpdate.value.text.trim()
  })
  newProjectUpdate.value.text = ''
  await projectStore.fetchProjectDetail(project.value.id)
}

const cancelProjectItem = (section) => {
  editing.value[section] = false
  newProjectItem.value.title = ''; newProjectItem.value.text = ''
}

const saveProjectLesson = async () => {
  if (!project.value?.id || !newProjectItem.value.text.trim()) return
  await projectStore.addProjectLesson(project.value.id, { title: newProjectItem.value.title.trim(), text: newProjectItem.value.text.trim() })
  await projectStore.fetchProjectDetail(project.value.id)
  cancelProjectItem('learnings')
}

const saveProjectRisk = async () => {
  if (!project.value?.id || !newProjectItem.value.text.trim()) return
  await projectStore.addProjectRisk(project.value.id, { title: newProjectItem.value.title.trim(), text: newProjectItem.value.text.trim(), severity: 'Medium' })
  await projectStore.fetchProjectDetail(project.value.id)
  cancelProjectItem('risks')
}

const saveProjectDecision = async () => {
  if (!project.value?.id || !newProjectItem.value.text.trim()) return
  await projectStore.addProjectDecision(project.value.id, { title: newProjectItem.value.title.trim(), text: newProjectItem.value.text.trim() })
  await projectStore.fetchProjectDetail(project.value.id)
  cancelProjectItem('decisions')
}

const toggleFollow = async () => {
  if (project.value?.id) await projectStore.toggleFollow(project.value.id)
}

const openShareModal = () => {
  isShareModalOpen.value = true
}

const getStatusClass = (status) => {
  const normalized = String(status || '').toLowerCase()
  if (normalized.includes('rủi ro') || normalized.includes('risk')) return 'status-at-risk'
  if (normalized.includes('không') || normalized.includes('chậm') || normalized.includes('off')) return 'status-off-track'
  if (normalized.includes('hoàn') || normalized.includes('done')) return 'status-done'
  if (normalized.includes('đúng') || normalized.includes('on')) return 'status-on-track'
  return 'status-pending'
}

const getInitials = (value = '') => {
  return value.trim().split(/\s+/).slice(0, 2).map(part => part[0]).join('').toUpperCase() || 'U'
}

const formatDate = (value) => {
  return value ? new Date(value).toLocaleString('vi-VN') : ''
}

const recordRecentView = async () => {
  if (!project.value?.id) return
  try {
    await starredStore.recordViewed(STARRED_ENTITY_TYPES.PROJECT, project.value.id)
  } catch (err) {
    console.warn('Failed to record recent project view', err)
  }
}

onMounted(async () => {
  signalRService.on('EntityChanged', handleProjectLinkRealtime)
  window.addEventListener('click', closePopovers)
  if (route.params.id) {
    await projectStore.initializeRealtime(route.params.id)
    await Promise.all([
      projectStore.fetchProjectDetail(route.params.id),
      projectStore.fetchProjects(),
      goalStore.fetchGoals()
    ])
    await loadProjectLinks()
    await recordRecentView()
  }
})

onUnmounted(() => {
  window.removeEventListener('click', closePopovers)
  signalRService.off('EntityChanged', handleProjectLinkRealtime)
})
</script>

<style scoped>
.project-detail-wrapper {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
  background-color: #FAFBFC;
}

/* Module Header Styles */
.module-header {
  padding: 32px 40px 0;
  background-color: #FFFFFF;
}

.header-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.header-content h1 {
  font-size: 24px;
  font-weight: 500;
  color: #172B4D;
  margin: 0;
}

/* Sidebar Popovers */
.relative-popover-container {
  position: relative;
}

.custom-popover {
  position: absolute;
  top: 100%;
  right: 0;
  width: 300px;
  background: white;
  border-radius: 3px;
  box-shadow: 0 4px 8px -2px rgba(9,30,66,0.25), 0 0 1px rgba(9,30,66,0.31);
  padding: 12px;
  z-index: 100;
  margin-top: 4px;
}

.custom-popover.popover-large {
  width: 350px;
  padding: 16px;
}

.popover-search {
  width: 100%;
  padding: 6px 8px;
  border: 2px solid #DFE1E6;
  border-radius: 3px;
  font-size: 14px;
  outline: none;
  transition: border-color 0.2s;
  box-sizing: border-box;
}

.popover-search:focus {
  border-color: #4C9AFF;
}

.popover-list-title {
  font-size: 11px;
  font-weight: 700;
  color: #6B778C;
  margin-top: 12px;
  margin-bottom: 8px;
}

.popover-list {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.popover-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px;
  border-radius: 3px;
  cursor: pointer;
}

.popover-item:hover {
  background-color: #F4F5F7;
}

.item-icon-muted {
  color: #5E6C84;
  font-size: 16px;
  width: 16px;
  text-align: center;
}

.item-details {
  display: flex;
  flex-direction: column;
}

.item-name {
  font-size: 14px;
  color: #172B4D;
}

.item-meta {
  font-size: 12px;
  color: #5E6C84;
}

.popover-select-control {
  display: flex;
  align-items: center;
  border: 2px solid #DFE1E6;
  border-radius: 3px;
  padding: 6px 8px;
  font-size: 14px;
  color: #172B4D;
  cursor: pointer;
}

.popover-title {
  font-size: 16px;
  font-weight: 500;
  color: #172B4D;
  margin: 0 0 8px 0;
}

.popover-desc {
  font-size: 12px;
  color: #5E6C84;
  margin: 0 0 16px 0;
  line-height: 1.5;
}

.popover-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.mt-2 { margin-top: 8px; }
.mt-3 { margin-top: 12px; }
.text-blue { color: #0052CC; }

.linked-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 8px;
  background-color: #F4F5F7;
  border-radius: 3px;
  margin-top: 8px;
  group-hover: block; /* Custom handling for remove btn */
}

.linked-item .remove-btn {
  margin-left: auto;
  background: transparent;
  border: none;
  color: #5E6C84;
  cursor: pointer;
  opacity: 0;
  transition: opacity 0.2s;
  padding: 4px;
}

.linked-item:hover .remove-btn {
  opacity: 1;
}

.linked-item .remove-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
  border-radius: 3px;
}

.truncate {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 200px;
}

.primary-btn {
  background-color: #0052CC;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 3px;
  font-weight: 500;
  font-size: 14px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.primary-btn:hover {
  background-color: #0047B3;
}

.tabs-nav {
  display: flex;
  border-bottom: 2px solid #DFE1E6;
  gap: 24px;
}

.tab-link {
  padding: 8px 0 12px;
  font-size: 14px;
  font-weight: 500;
  color: #5E6C84;
  text-decoration: none;
  position: relative;
  margin-bottom: -2px;
  border-bottom: 2px solid transparent;
  transition: color 0.2s;
}

.tab-link:hover {
  color: #172B4D;
}

.tab-link.active {
  color: #0052CC;
  border-bottom-color: #0052CC;
}

/* Entity Header */
.project-header {
  padding-top: 32px;
  background-color: #FFFFFF;
}

.project-header-inner {
  max-width: 1000px;
  margin: 0 auto;
  padding: 0 40px;
  width: 100%;
}

.header-main {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.title-block {
  display: flex;
  align-items: center;
  gap: 12px;
}

.project-emoji-large {
  font-size: 24px;
}

.title-block h1 {
  margin: 0;
  font-size: 24px;
  font-weight: 500;
  color: #172B4D;
}

.header-actions {
  display: flex;
  gap: 8px;
  align-items: center;
}

.secondary-btn {
  background-color: rgba(9, 30, 66, 0.04);
  color: #42526E;
  border: none;
  padding: 6px 12px;
  border-radius: 3px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
}

.secondary-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.icon-btn-header {
  background: transparent;
  color: #42526E;
  border: none;
  padding: 6px 10px;
  border-radius: 3px;
  cursor: pointer;
}

.icon-btn-header:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.status-badge {
  display: inline-flex;
  align-items: center;
  padding: 4px 8px;
  border-radius: 3px;
  font-size: 12px;
  font-weight: 700;
  text-transform: uppercase;
}

.status-on-track { background-color: #E3FCEF; color: #006644; }
.status-done { background-color: #EAE6FF; color: #403294; }
.status-at-risk { background-color: #FFF0B3; color: #FF8B00; }
.status-off-track { background-color: #FFEBE6; color: #BF2600; }
.status-pending { background-color: #DFE1E6; color: #42526E; }

.target-date-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 4px 8px;
  border-radius: 3px;
  font-size: 12px;
  color: #42526E;
  border: 1px solid #DFE1E6;
}

/* Tabs */
.project-tabs-container {
  border-bottom: 2px solid #DFE1E6;
}

.project-tabs {
  display: flex;
  gap: 24px;
  max-width: 1000px;
  margin: 0 auto;
  padding: 0 40px;
  width: 100%;
}

.tab-btn {
  background: transparent;
  border: none;
  color: #5E6C84;
  font-size: 14px;
  font-weight: 500;
  padding: 8px 0 12px;
  cursor: pointer;
  position: relative;
  margin-bottom: -2px;
  border-bottom: 2px solid transparent;
}

.tab-btn:hover {
  color: #172B4D;
}

.tab-btn.active {
  color: #0052CC;
  border-bottom-color: #0052CC;
}

.tab-badge {
  background-color: #DFE1E6;
  color: #172B4D;
  padding: 2px 6px;
  border-radius: 12px;
  font-size: 11px;
  margin-left: 4px;
}

/* Content Grid */
.project-content-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 300px;
  gap: 40px;
  padding: 32px 40px;
  max-width: 1000px;
  margin: 0 auto;
  width: 100%;
}

.main-column {
  display: flex;
  flex-direction: column;
  gap: 40px;
}

.content-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.content-section h4 {
  margin: 0;
  font-size: 16px;
  color: #172B4D;
  font-weight: 600;
}

.editable-placeholder {
  padding: 8px 0;
  cursor: pointer;
  border-radius: 3px;
}

.editable-placeholder:hover {
  background-color: #FAFBFC;
}

.editable-placeholder p {
  margin: 0;
  font-size: 14px;
  line-height: 1.5;
  color: #172B4D;
}

.empty-text {
  color: #5E6C84 !important;
}

.comment-input-preview {
  display: flex;
  align-items: center;
  gap: 12px;
}

.user-avatar-current {
  width: 32px;
  height: 32px;
  background-color: #0052CC;
  color: white;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: 600;
}

.comment-input-placeholder {
  flex: 1;
  padding: 10px 16px;
  background-color: #FAFBFC;
  border: 1px solid #DFE1E6;
  border-radius: 24px;
  color: #5E6C84;
  font-size: 14px;
}

/* Sidebar */
.details-body {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.detail-row {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.detail-label {
  font-size: 12px;
  font-weight: 600;
  color: #5E6C84;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.badge-count {
  background-color: #DFE1E6;
  padding: 2px 6px;
  border-radius: 12px;
  font-size: 11px;
  color: #172B4D;
}

.icon-btn-micro {
  background: transparent;
  border: none;
  color: #5E6C84;
  cursor: pointer;
  width: 24px;
  height: 24px;
  border-radius: 3px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color 0.2s, color 0.2s;
}

.icon-btn-micro:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.owner-chip {
  display: flex;
  align-items: center;
  gap: 8px;
}

.owner-avatar-micro {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background-color: #0052CC;
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  font-weight: bold;
}

.owner-info {
  display: flex;
  flex-direction: column;
}

.owner-name {
  font-size: 14px;
  color: #172B4D;
}

.owner-role {
  font-size: 12px;
  color: #5E6C84;
}

.empty-value {
  font-size: 14px;
  color: #5E6C84;
}

.flex-between {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

/* Empty Tab States */
.empty-state-large-tab {
  min-height: 204px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 48px 24px;
  background: transparent;
  border: 0;
  box-shadow: none;
  text-align: center;
  margin-top: 16px;
}

.empty-spaces-icon {
  width: 54px;
  height: 54px;
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid color-mix(in srgb, var(--color-accent, #0ea5e9) 18%, transparent);
  border-radius: 14px;
  background: color-mix(in srgb, var(--color-accent, #0ea5e9) 10%, var(--color-surface, #ffffff));
  color: var(--color-accent, #0ea5e9);
  font-size: 23px;
  box-shadow: 0 14px 30px rgba(14, 165, 233, 0.12);
}

.empty-spaces-copy {
  max-width: 380px;
  text-align: center;
}

.empty-spaces-copy h3 {
  margin: 0;
  color: var(--color-text-primary, #172B4D);
  font-size: 15px;
  font-weight: 800;
  line-height: 1.35;
}

.empty-spaces-copy p {
  margin: 3px 0 0;
  color: var(--color-text-muted, #5E6C84);
  font-size: 13px;
  line-height: 1.4;
}



.link-btn {
  color: #0052CC;
  text-decoration: none;
  font-size: 14px;
  font-weight: 500;
}

.link-btn:hover {
  text-decoration: underline;
}

/* Updates Tab styles */
.updates-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.updates-header h4 {
  margin: 0;
  font-size: 16px;
  color: #172B4D;
}

.last-update-text {
  font-size: 12px;
  color: #5E6C84;
}

.timeline-visual {
  position: relative;
  height: 40px;
  margin-bottom: 32px;
}

.timeline-line {
  position: absolute;
  top: 50%;
  left: 0;
  right: 0;
  height: 2px;
  background-color: #36B37E;
  z-index: 1;
}

.timeline-node {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  background-color: #FFFFFF;
  padding: 0 8px;
  z-index: 2;
  display: flex;
  flex-direction: column;
  align-items: center;
  color: #36B37E;
}

.timeline-node i {
  background-color: #E3FCEF;
  padding: 6px;
  border-radius: 50%;
  font-size: 14px;
  border: 2px solid #FFFFFF;
}

.timeline-node span {
  font-size: 11px;
  margin-top: 4px;
}

.update-editor-box {
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  background-color: #FFFFFF;
  box-shadow: 0 4px 8px -2px rgba(9,30,66,0.25);
  margin-bottom: 40px;
}

.update-editor-header {
  display: flex;
  padding: 16px;
  border-bottom: 1px solid #DFE1E6;
  gap: 24px;
}

.editor-field {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.editor-field label {
  font-size: 12px;
  font-weight: 600;
  color: #5E6C84;
}

.status-dropdown-wrapper {
  position: relative;
}

.status-dropdown-menu {
  position: absolute;
  top: 100%;
  left: 0;
  margin-top: 4px;
  background: white;
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  box-shadow: 0 4px 8px -2px rgba(9,30,66,0.25);
  padding: 8px 0;
  z-index: 10;
  min-width: 180px;
}

.status-option {
  padding: 6px 12px;
  cursor: pointer;
}

.status-option:hover {
  background-color: #FAFBFC;
}

.text-option {
  font-size: 12px;
  color: #5E6C84;
}

.divider {
  height: 1px;
  background-color: #DFE1E6;
  margin: 4px 0;
}

.date-dropdown {
  background-color: transparent;
  cursor: pointer;
}

.template-link {
  margin-left: auto;
  justify-content: center;
  color: #5E6C84;
  font-size: 12px;
  cursor: pointer;
}

.update-editor-body {
  padding: 16px;
}

.update-editor-body textarea {
  width: 100%;
  border: none;
  resize: none;
  outline: none;
  font-size: 14px;
  font-family: inherit;
  color: #172B4D;
}

.update-editor-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  border-top: 1px solid #DFE1E6;
  background-color: #FAFBFC;
}

.editor-tools {
  display: flex;
  align-items: center;
  gap: 8px;
}

.tool-ai {
  font-size: 12px;
  color: #5E6C84;
  margin-right: 8px;
}

.tool-btn {
  background: transparent;
  border: none;
  color: #42526E;
  padding: 4px 8px;
  cursor: pointer;
  border-radius: 3px;
}

.tool-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.editor-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.char-count {
  font-size: 12px;
  color: #5E6C84;
}

.primary-btn {
  background-color: #0052CC;
  color: white;
  border: none;
  padding: 6px 12px;
  border-radius: 3px;
  font-weight: 500;
  font-size: 14px;
  cursor: pointer;
}

.primary-btn:hover { background-color: #0047B3; }

/* Timeline Posts */
.timeline-period {
  font-size: 16px;
  color: #172B4D;
  margin: 0 0 16px 0;
}

.timeline-post {
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  padding: 16px;
  background-color: #FAFBFC;
}

.post-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 16px;
}

.post-user {
  display: flex;
  gap: 12px;
}

.user-info {
  display: flex;
  flex-direction: column;
}

.user-name {
  font-size: 14px;
  font-weight: 500;
  color: #172B4D;
}

.post-time {
  font-size: 12px;
  color: #5E6C84;
}

.post-status-meta {
  font-size: 12px;
  color: #5E6C84;
  display: flex;
  align-items: center;
  gap: 6px;
}

.post-content {
  margin-bottom: 16px;
}

.post-content p {
  margin: 0 0 16px 0;
  font-size: 14px;
  color: #172B4D;
}

.status-change-log {
  display: inline-block;
  background-color: #FFFFFF;
  border: 1px solid #DFE1E6;
  padding: 8px 12px;
  border-radius: 3px;
  font-size: 12px;
  color: #5E6C84;
}

.mx-1 { margin: 0 4px; }
.mt-16 { margin-top: 16px; }

.section-header-row {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 16px;
}

.section-header-row h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #172B4D;
}

.post-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: #5E6C84;
  margin-bottom: 16px;
}

.reaction-btn {
  background-color: #FFFFFF;
  border: 1px solid #DFE1E6;
  border-radius: 12px;
  padding: 2px 8px;
  cursor: pointer;
  font-size: 12px;
}

.reaction-btn:hover {
  background-color: #FAFBFC;
}
</style>
