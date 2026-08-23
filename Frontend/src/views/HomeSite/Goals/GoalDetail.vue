<template>
  <div class="goal-detail-wrapper" v-if="goal">
    <!-- Cover Image -->
    <div class="goal-cover">
      <!-- Floating Back Button -->
      <button class="back-floating-btn" @click="router.push(goalsBasePath)">
        <i class="fa-solid fa-arrow-left"></i>
        <span>{{ isVi ? 'Quay lại' : 'Back' }}</span>
      </button>
    </div>

    <!-- Entity Header -->
    <div class="goal-header-wrapper">
      <div class="goal-identity">
        <div class="goal-icon-large">
          <i class="fa-solid fa-bullseye"></i>
        </div>
        <div class="goal-title-block">
          <div class="title-row">
            <h1>{{ goal.title }}</h1>
            <AppStatusBadge :status="goal.status" :statusText="goal.status" />
          </div>
          <p class="goal-meta-subtitle">
            Cập nhật: <strong class="text-dark">{{ goal.lastUpdate || 'Vừa xong' }}</strong>
          </p>
        </div>
      </div>
      
      <div class="header-actions">
        <button class="secondary-btn" @click="toggleFollow">
          <i class="fa-solid fa-eye"></i> {{ goal.isFollowing ? 'Đang theo dõi' : 'Theo dõi' }}
        </button>
        <button
          class="secondary-btn icon-only goal-star-button"
          type="button"
          :class="{ starred: isGoalStarred }"
          :disabled="starredStore.isPending('Goal', goal.id)"
          :aria-pressed="isGoalStarred"
          :aria-label="isGoalStarred ? 'Bỏ gắn sao mục tiêu' : 'Gắn sao mục tiêu'"
          @click="toggleStar"
        >
          <i :class="isGoalStarred ? 'fa-solid fa-star' : 'fa-regular fa-star'" aria-hidden="true"></i>
        </button>
        <button class="secondary-btn icon-only" @click="toggleShare">
          <i class="fa-solid fa-share-nodes"></i>
        </button>
        <button class="secondary-btn icon-only" @click="toggleMenu">
          <i class="fa-solid fa-ellipsis"></i>
        </button>
      </div>
    </div>

    <!-- Navigation Tabs -->
    <div class="tabs-nav">
      <button class="tab-btn" :class="{ active: currentTab === 'overview' }" @click="currentTab = 'overview'">Tổng quan</button>
      <button class="tab-btn" :class="{ active: currentTab === 'updates' }" @click="currentTab = 'updates'">
        Cập nhật <span v-if="updates.length" class="badge-count">{{ updates.length + 1 }}</span>
      </button>
      <button class="tab-btn" :class="{ active: currentTab === 'jira' }" @click="currentTab = 'jira'">SprintA</button>
      <button class="tab-btn" :class="{ active: currentTab === 'projects' }" @click="currentTab = 'projects'">Dự án</button>
      <button class="tab-btn" :class="{ active: currentTab === 'learnings' }" @click="currentTab = 'learnings'">Bài học rút ra</button>
      <button class="tab-btn" :class="{ active: currentTab === 'risks' }" @click="currentTab = 'risks'">Rủi ro</button>
      <button class="tab-btn" :class="{ active: currentTab === 'decisions' }" @click="currentTab = 'decisions'">Quyết định</button>
    </div>

    <!-- Main Content Grid -->
    <div class="goal-content-grid">
      <!-- Left Column: Main Information -->
      <div class="main-column">
        <!-- TỔNG QUAN TAB -->
        <template v-if="currentTab === 'overview'">
          <!-- Mô tả -->
          <section class="content-section">
            <div class="section-header">
              <h3>Mô tả</h3>
            </div>
            <div class="section-body">
              <RichTextEditor v-slot="isOwner" v-if="isEditingBio && isOwner" v-model="tempBio" @save="saveBio" @cancel="isEditingBio = false" placeholder="Mô tả ngắn gọn lý do tại sao mục tiêu này lại quan trọng và cách đo lường thành công..." />
              <div v-else @click="isOwner && startEditingBio()" :style="{ cursor: isOwner ? 'pointer' : 'default', color: '#5E6C84', fontSize: '14px', padding: '8px', borderRadius: '3px', minHeight: '40px' }" onmouseover="if (isOwner) this.style.backgroundColor='#FAFBFC'" onmouseout="if (isOwner) this.style.backgroundColor='transparent'">
                <div v-if="goal.description && goal.description !== '<p></p>'" v-html="safeGoalDescription" class="tiptap-content"></div>
                <div v-else>{{ isOwner ? 'Mô tả ngắn gọn lý do tại sao mục tiêu này lại quan trọng và cách đo lường thành công, để bạn có thể cung cấp hiểu biết chung cho người theo dõi.' : 'Chưa có mô tả' }}</div>
              </div>
            </div>
          </section>

          <!-- Key results / Progress Chart -->
          <section class="content-section">
            <div class="section-header">
              <h3>Key results</h3>
            </div>
            <div class="section-body">
              <GoalProgressChart :goal="goalProgressChartData" />
            </div>
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
                <CommentSection :entity-id="route.params.id" entity-type="Goal" />
              </div>
              <div v-else>
                <div class="timeline-item" v-for="entry in goalHistory" :key="entry.id" style="display: flex; align-items: flex-start; gap: 12px;">
                   <AppAvatar :user="{ fullName: entry.actor, email: entry.email }" :size="24" />
                   <div style="flex: 1;">
                      <div style="display: flex; justify-content: space-between; align-items: center;">
                         <span style="font-size: 14px; color: #172B4D;"><strong>{{ entry.actor }}</strong> {{ entry.action }}</span>
                         <span style="font-size: 12px; color: #5E6C84;">{{ formatDate(entry.createdAt) }}</span>
                      </div>
                      <div style="font-size: 14px; color: #5E6C84; margin-top: 4px;">{{ entry.target }}</div>
                   </div>
                </div>
                <div class="timeline-item" v-if="false" style="display: flex; align-items: flex-start; gap: 12px;">
                   <div class="user-avatar-current" style="background: #36B37E; width: 24px; height: 24px; font-size: 10px;">T</div>
                   <div style="flex: 1;">
                      <div style="display: flex; justify-content: space-between; align-items: center;">
                         <span style="font-size: 14px; color: #172B4D;"><strong>Hệ thống</strong> đã tạo mục tiêu</span>
                         <span style="font-size: 12px; color: #5E6C84;">9 days ago</span>
                      </div>
                      <div style="font-size: 14px; color: #5E6C84; margin-top: 4px;">{{ goal.title }}</div>
                   </div>
                </div>
              </div>
            </div>
          </section>
        </template>

        <template v-if="currentTab === 'updates'">
          <div class="updates-header">
            <h4>Lịch sử mục tiêu</h4>
            <span class="last-update-text">Lần cập nhật gần nhất: {{ goalStore.updates?.length ? new Date(goalStore.updates[0].createdAt).toLocaleDateString('vi-VN') : 'Chưa có' }}</span>
          </div>

          
          <div class="timeline-visual">
            <div class="timeline-line"></div>
            <div class="timeline-node current">
              <i class="fa-solid fa-user-group"></i>
              <span>Tuần này</span>
            </div>
          </div>

          <div v-if="isOwner" class="update-editor-box">
            <div class="update-editor-header">
              <div class="editor-field">
                <label>Trạng thái hiện tại</label>
                <div class="status-dropdown-wrapper">
                  <span @click="showStatusMenu = !showStatusMenu" style="cursor: pointer; display: inline-block;">
                    <AppStatusBadge :status="newUpdateForm.status" :statusText="newUpdateForm.status + ' ▾'" />
                  </span>
                  <div class="status-dropdown-menu" v-if="showStatusMenu">
                    <div class="status-option" @click="selectStatus('ĐANG CHỜ XỬ LÝ')"><AppStatusBadge status="ĐANG CHỜ XỬ LÝ" statusText="ĐANG CHỜ XỬ LÝ" /></div>
                    <div class="status-option" @click="selectStatus('ĐÚNG TIẾN ĐỘ')"><AppStatusBadge status="ĐÚNG TIẾN ĐỘ" statusText="ĐÚNG TIẾN ĐỘ" /></div>
                    <div class="status-option" @click="selectStatus('CÓ RỦI RO')"><AppStatusBadge status="CÓ RỦI RO" statusText="CÓ RỦI RO" /></div>
                    <div class="status-option" @click="selectStatus('CHẬM TIẾN ĐỘ')"><AppStatusBadge status="CHẬM TIẾN ĐỘ" statusText="CHẬM TIẾN ĐỘ" /></div>
                    <div class="status-option" @click="selectStatus('ĐÃ HOÀN TẤT')"><AppStatusBadge status="ĐÃ HOÀN TẤT" statusText="ĐÃ HOÀN TẤT" /></div>
                    <div class="divider"></div>
                    <div class="status-option text-option" @click="selectStatus('ĐÃ TẠM DỪNG')">ĐÃ TẠM DỪNG</div>
                    <div class="status-option text-option" @click="selectStatus('ĐÃ HỦY')">ĐÃ HỦY</div>
                  </div>
                </div>
              </div>
              <div class="editor-field">
                <label>Ngày mục tiêu</label>
                <div class="target-date-badge date-dropdown">
                  <i class="fa-regular fa-calendar"></i> {{ goal?.endDate ? new Date(goal.endDate).toLocaleDateString('vi-VN', { day: 'numeric', month: 'short' }) : 'Không có' }} <i class="fa-solid fa-chevron-down ms-1"></i>
                </div>
              </div>
              <div class="editor-field">
                <label>Tiến độ</label>
                <div style="display: flex; align-items: center; gap: 8px; margin-top: 4px;">
                  <input type="number" v-model="newUpdateForm.progress" style="width: 50px; padding: 2px 4px; border: 1px solid #DFE1E6; border-radius: 3px; font-size: 13px;" min="0" max="100" /> <span style="font-size: 13px; color: #5E6C84;">%</span>
                </div>
              </div>
              <div class="editor-field template-link" style="margin-left: auto;">
                <span>Mẫu <i class="fa-solid fa-chevron-down ms-1"></i></span>
              </div>
            </div>
            
            <div class="update-editor-body">
              <textarea v-model="newUpdateForm.content" placeholder="Viết bản cập nhật... Nhấn enter để tạo dòng mới." rows="6" style="width: 100%; border: none; outline: none; resize: none; font-size: 14px; background: transparent; padding: 8px 0;"></textarea>
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
                <span class="char-count">{{ newUpdateForm.content.length }}/1000 <i class="fa-solid fa-circle-question"></i></span>
                <button class="secondary-btn"><i class="fa-solid fa-users"></i> 1</button>
                <button class="secondary-btn">Lưu bản nháp</button>
                <button class="primary-btn" @click="submitGoalUpdate" :disabled="!newUpdateForm.content.trim()">Đăng bản cập nhật</button>
              </div>
            </div>
          </div>

          <div class="timeline-posts" v-if="goalStore.updates?.length">
            <div class="timeline-post" style="margin-bottom: 24px; border: 1px solid #DFE1E6; padding: 16px; border-radius: 3px; background: white;" v-for="update in goalStore.updates" :key="update.id">
              <div class="post-header">
                <div class="post-user">
                  <AppAvatar :user="{ id: update.userId, fullName: update.userName, avatarUrl: update.userAvatar }" :size="32" />
                  <div class="user-info">
                    <span class="user-name">{{ update.userName }}</span>
                    <span class="post-time">{{ new Date(update.createdAt).toLocaleString('vi-VN') }}</span>
                  </div>
                </div>
                <div class="post-status-meta">
                  <AppStatusBadge :status="update.status" :statusText="update.status" /> cho <div class="target-date-badge"><i class="fa-regular fa-calendar"></i> {{ goal?.endDate ? new Date(goal.endDate).toLocaleDateString('vi-VN', { day: 'numeric', month: 'short' }) : 'Không có' }}</div>
                </div>
              </div>
              
              <div class="post-content">
                <div v-if="editingUpdateId === update.id" style="margin-bottom: 12px;">
                  <textarea v-model="editingContent" rows="3" style="width: 100%; padding: 8px; border: 2px solid #4C9AFF; border-radius: 3px; font-size: 14px; outline: none; resize: none;"></textarea>
                  <div style="display: flex; gap: 8px; margin-top: 8px;">
                    <button class="primary-btn" style="height: 32px;" @click="saveInlineEdit(update)">Lưu</button>
                    <button class="secondary-btn" style="height: 32px;" @click="cancelInlineEdit">Hủy</button>
                  </div>
                </div>
                <p v-else style="white-space: pre-wrap;">{{ update.content }}</p>
                <div class="status-change-log" style="display: flex; gap: 12px; align-items: center; flex-wrap: wrap;">
                  <div v-if="getPreviousStatus(update) && getPreviousStatus(update) !== getCurrentStatus(update)">
                    Đã thay đổi trạng thái từ <AppStatusBadge :status="update.previousStatus" :statusText="update.previousStatus" /> <i class="fa-solid fa-arrow-right text-gray-400 mx-1 text-xs"></i> <AppStatusBadge :status="update.status" :statusText="update.status" />
                  </div>
                  <div v-else>
                    Đã giữ nguyên trạng thái <AppStatusBadge :status="update.status" :statusText="update.status" />
                  </div>
                  <div v-if="update.progress !== undefined && update.progress !== null" style="color: #5E6C84; font-size: 13px;">
                    <i class="fa-solid fa-chart-line"></i> Tiến độ: <strong>{{ update.progress }}%</strong>
                  </div>
                </div>
              </div>
              
              <div class="post-actions">
                
                  <div style="display: flex; gap: 12px; color: #5E6C84; font-size: 13px; font-weight: 500;">
                    <span style="cursor: pointer;"><i class="fa-solid fa-share-nodes"></i> Chia sẻ</span>
                    <span v-if="isOwner || update.userId === authStore.userId" style="cursor: pointer;" @click="editUpdate(update)"><i class="fa-solid fa-pen"></i> Sửa</span>
                    <el-popconfirm v-if="isOwner || update.userId === authStore.userId" title="Bạn có chắc muốn xóa bản cập nhật này?" @confirm="deleteUpdate(update.id)" confirm-button-text="Xóa" cancel-button-text="Hủy" confirm-button-type="danger">
                      <template #reference>
                        <span style="cursor: pointer; color: #DE350B;"><i class="fa-solid fa-trash"></i> Xóa</span>
                      </template>
                    </el-popconfirm>
                  </div>
    
                <button class="reaction-btn">👍</button>
                <button class="reaction-btn">👏</button>
                <button class="reaction-btn">🎉</button>
                <button class="reaction-btn">❤️</button>
                <button class="reaction-btn"><i class="fa-solid fa-ellipsis"></i></button>
                <button class="reaction-btn"><i class="fa-solid fa-bullseye"></i></button>
              </div>
              
              <div class="mt-4">
                <CommentSection :entity-id="update.id" entity-type="GoalUpdate" />
              </div>
            </div>
          </div>
                    <AppEmptyState v-else icon="fa-regular fa-message" title="Chưa có bản cập nhật nào." description="Các bản cập nhật mục tiêu sẽ xuất hiện ở đây sau khi được đăng." />
        </template>

        <!-- JIRA TAB -->
        <template v-if="currentTab === 'jira'">
          <div style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 32px; background: white; margin-top: 16px;">
            <div style="display: flex; gap: 24px; max-width: 600px; margin: 0 auto; position: relative;">
               <div style="width: 64px; height: 64px; background: #0052CC; color: white; border-radius: 8px; display: flex; align-items: center; justify-content: center; font-size: 32px; position: relative; flex-shrink: 0;">
                  <i class="fa-solid fa-layer-group"></i>
                  <div style="position: absolute; bottom: -8px; right: -8px; width: 24px; height: 24px; background: #0052CC; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 14px; border: 2px solid white;">
                     <i class="fa-solid fa-plus"></i>
                  </div>
               </div>
               <div>
                 <h3 style="margin: 0 0 8px 0; font-size: 16px; color: #172B4D;">{{ isOwner ? 'Thêm công việc trong SprintA góp phần vào mục tiêu này' : 'Công việc SprintA' }}</h3>
                 <p style="margin: 0 0 16px 0; font-size: 14px; color: #5E6C84; line-height: 1.5;">{{ isOwner ? 'Kết nối công việc của đội ngũ để xem mục tiêu này trong SprintA và liên kết các nội dung cập nhật với công việc.' : 'Chưa có công việc SprintA nào liên kết với mục tiêu này.' }} <a href="#" style="color: #0052CC; text-decoration: none;">Thông tin khác về mục tiêu trong SprintA</a></p>
                 <div style="position: relative; display: inline-block;">
                   <button v-if="isOwner" class="secondary-btn" @click="isSprintAInputOpen = !isSprintAInputOpen" style="background: white; border: 1px solid #DFE1E6; font-weight: 600;">Thêm hạng mục công việc SprintA</button>
                   
                   <!-- SprintA Input Dropdown -->
                   <div v-if="isSprintAInputOpen" class="dropdown-menu" style="position: absolute; top: 100%; left: 0; margin-top: 8px; background: white; border: 1px solid #DFE1E6; border-radius: 3px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); width: 320px; z-index: 1000; padding: 16px;">
                      <input type="text" placeholder="Dán URL SprintA" style="width: 100%; padding: 8px 12px; border: 2px solid #4C9AFF; border-radius: 3px; font-size: 14px; outline: none; box-sizing: border-box; margin-bottom: 12px;" />
                      <div style="display: flex; justify-content: flex-end; gap: 8px;">
                        <button class="cancel-btn" @click="isSprintAInputOpen = false">Hủy</button>
                        <button class="primary-btn">Thêm</button>
                      </div>
                   </div>
                 </div>
               </div>
            </div>
          </div>
        </template>

        <!-- DỰ ÁN TAB -->
        <template v-if="currentTab === 'projects'">
          <div style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 32px; background: white; margin-top: 16px;">
            <div style="display: flex; gap: 24px; max-width: 600px; margin: 0 auto; position: relative;">
               <div style="width: 64px; height: 64px; background: #EBECF0; color: #172B4D; border-radius: 8px; display: flex; align-items: center; justify-content: center; font-size: 32px; position: relative; flex-shrink: 0;">
                  <i class="fa-solid fa-rocket"></i>
                  <div style="position: absolute; bottom: -8px; right: -8px; width: 24px; height: 24px; background: #0052CC; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 14px; border: 2px solid white;">
                     <i class="fa-solid fa-plus"></i>
                  </div>
               </div>
               <div>
                 <h3 style="margin: 0 0 8px 0; font-size: 16px; color: #172B4D;">{{ isOwner ? 'Thêm dự án để sắp xếp công việc của bạn với mục tiêu này' : 'Dự án đóng góp' }}</h3>
                 <p style="margin: 0 0 16px 0; font-size: 14px; color: #5E6C84; line-height: 1.5;">{{ isOwner ? 'Sử dụng không gian này để theo dõi bất kỳ dự án nào đóng góp vào mục tiêu này, vì vậy đội ngũ và các bên liên quan của bạn có thể có được bức tranh toàn cảnh.' : 'Chưa có dự án nào liên kết với mục tiêu này.' }}</p>
                 <div style="position: relative; display: inline-block;">
                   <button v-if="isOwner" class="secondary-btn" @click="isProjectSearchOpen = !isProjectSearchOpen" style="background: white; border: 1px solid #DFE1E6; font-weight: 600;">Thêm dự án</button>
                   
                   <!-- Project Search Dropdown -->
                   <div v-if="isProjectSearchOpen" class="dropdown-menu" style="position: absolute; top: 100%; left: 0; margin-top: 8px; background: white; border: 1px solid #DFE1E6; border-radius: 3px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); width: 320px; z-index: 1000; padding: 12px 0;">
                      <div style="padding: 0 12px 12px;">
                        <input type="text" placeholder="Tìm kiếm dự án" style="width: 100%; padding: 8px 12px; border: 2px solid #DFE1E6; border-radius: 3px; font-size: 14px; outline: none; box-sizing: border-box;" onfocus="this.style.borderColor='#4C9AFF'" onblur="this.style.borderColor='#DFE1E6'" />
                      </div>
                      <div style="max-height: 200px; overflow-y: auto;">
                        <div v-for="proj in siteProjects" :key="proj.id" style="padding: 8px 12px; cursor: pointer; display: flex; align-items: center; gap: 8px; font-size: 14px; color: #172B4D;" onmouseover="this.style.background='#FAFBFC'" onmouseout="this.style.background='transparent'">
                           <div style="width: 16px; height: 16px; background: #FFAB00; border-radius: 3px; font-size: 10px; display: flex; align-items: center; justify-content: center;"><i class="fa-solid fa-rocket" style="color: white;"></i></div>
                           {{ proj.name || proj.title }}
                        </div>
                      </div>
                      <div style="padding: 12px 12px 0; border-top: 1px solid #DFE1E6; margin-top: 4px;">
                        <span style="font-size: 14px; color: #5E6C84; cursor: pointer;" onmouseover="this.style.textDecoration='underline'" onmouseout="this.style.textDecoration='none'">Tạo dự án mới</span>
                      </div>
                   </div>
                 </div>
               </div>
            </div>
          </div>
        </template>

        <!-- BÀI HỌC RÚT RA TAB -->
        <template v-if="currentTab === 'learnings'">
          <AppEmptyState v-if="!editing.learnings && !goalStore.lessons?.length" icon="fa-solid fa-lightbulb" title="Những bộ óc vĩ đại có tư duy giống nhau sẽ chia sẻ kiến thức của họ" :description="isOwner ? 'Chia sẻ những gì bạn đã học được với công ty của bạn để giúp những người khác có một khởi đầu thuận lợi khi làm việc trên các mục tiêu tương tự.' : 'Chưa có bài học rút ra nào được ghi nhận cho mục tiêu này.'">
            <template #actions v-if="isOwner">
              <button class="secondary-btn" @click="editing.learnings = true">Thêm bài học rút ra mới</button>
            </template>
          </AppEmptyState>
          <div v-else>
            <div v-if="editing.learnings && isOwner" class="tab-item-editor" style="margin-bottom: 24px; padding-top: 16px;">
                <RichTextEditor v-model="newItem.text" @save="saveLearning" @cancel="editing.learnings = false; newItem.title = ''; newItem.text = ''" placeholder="Dùng không gian này để chia sẻ bài học rút ra...">
                  <template #header>
                    <div style="display: flex; align-items: center; gap: 12px; padding: 12px 16px; border-bottom: 1px solid #DFE1E6; background-color: #fff;">
                      <i class="fa-regular fa-lightbulb" style="color: #FFAB00; font-size: 18px;"></i>
                      <input type="text" v-model="newItem.title" placeholder="Tóm tắt cho bài học rút ra của bạn là gì?" style="border: none; outline: none; background: transparent; width: 100%; font-size: 15px; font-weight: 500; color: #172B4D;" />
                    </div>
                  </template>
                </RichTextEditor>
                
            </div>
            <div v-if="!editing.learnings && goalStore.lessons?.length && isOwner" style="margin-bottom: 24px; padding-top: 16px;">
                <button class="secondary-btn" @click="editing.learnings = true">Thêm bài học</button>
            </div>
            
            <div class="timeline-post" v-for="item in goalStore.lessons" :key="item.id" style="margin-bottom: 16px;">
                <div class="post-header">
                  <div class="post-user">
                    <AppAvatar :user="{ id: item.creatorId, fullName: item.creatorName, avatarUrl: item.creatorAvatarUrl, email: item.creatorEmail }" :size="32" />
                    <div class="user-info">
                      <span class="user-name">{{ item.creatorName }}</span>
                      <span class="post-time">{{ new Date(item.createdAt).toLocaleString('vi-VN') }}</span>
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

        <!-- RỦI RO TAB -->
        <template v-if="currentTab === 'risks'">
          <AppEmptyState v-if="!editing.risks && !goalStore.risks?.length" icon="fa-solid fa-triangle-exclamation" title="Nắm bắt các rủi ro đã biết" :description="isOwner ? 'Theo dõi mọi rủi ro liên quan đến mục tiêu này để tránh những bất ngờ sau này.' : 'Chưa ghi nhận rủi ro nào cho mục tiêu này.'">
            <template #actions v-if="isOwner">
              <button class="secondary-btn" @click="editing.risks = true">Thêm rủi ro mới</button>
            </template>
          </AppEmptyState>
          <div v-else>
            <div v-if="editing.risks && isOwner" class="tab-item-editor" style="margin-bottom: 24px; padding-top: 16px;">
                <RichTextEditor v-model="newItem.text" @save="saveRisk" @cancel="editing.risks = false; newItem.title = ''; newItem.text = ''" placeholder="Mô tả rủi ro...">
                  <template #header>
                    <div style="display: flex; align-items: center; gap: 12px; padding: 12px 16px; border-bottom: 1px solid #DFE1E6; background-color: #fff;">
                      <i class="fa-solid fa-triangle-exclamation" style="color: #FF5630; font-size: 18px;"></i>
                      <input type="text" v-model="newItem.title" placeholder="Tóm tắt rủi ro là gì?" style="border: none; outline: none; background: transparent; width: 100%; font-size: 15px; font-weight: 500; color: #172B4D;" />
                    </div>
                  </template>
                </RichTextEditor>
                
            </div>
            <div v-if="!editing.risks && goalStore.risks?.length && isOwner" style="margin-bottom: 24px; padding-top: 16px;">
                <button class="secondary-btn" @click="editing.risks = true">Thêm rủi ro</button>
            </div>
            
            <div class="timeline-post" v-for="item in goalStore.risks" :key="item.id" style="margin-bottom: 16px;">
                <div class="post-header">
                  <div class="post-user">
                    <AppAvatar :user="{ id: item.creatorId, fullName: item.creatorName, avatarUrl: item.creatorAvatarUrl, email: item.creatorEmail }" :size="32" />
                    <div class="user-info">
                      <span class="user-name">{{ item.creatorName }}</span>
                      <span class="post-time">{{ new Date(item.createdAt).toLocaleString('vi-VN') }}</span>
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

        <!-- QUYẾT ĐỊNH TAB -->
        <template v-if="currentTab === 'decisions'">
          <AppEmptyState v-if="!editing.decisions && !goalStore.decisions?.length" icon="fa-solid fa-check-circle" title="Truyền đạt các quyết định lớn" :description="isOwner ? 'Ghi lại các quyết định lớn cho mục tiêu này tại đây để chia sẻ trong bản cập nhật mới nhất của bạn.' : 'Chưa ghi nhận quyết định lớn nào cho mục tiêu này.'">
            <template #actions v-if="isOwner">
              <button class="secondary-btn" @click="editing.decisions = true">Thêm quyết định mới</button>
            </template>
          </AppEmptyState>
          <div v-else>
            <div v-if="editing.decisions && isOwner" class="tab-item-editor" style="margin-bottom: 24px; padding-top: 16px;">
                <RichTextEditor v-model="newItem.text" @save="saveDecision" @cancel="editing.decisions = false; newItem.title = ''; newItem.text = ''" placeholder="Mô tả quyết định...">
                  <template #header>
                    <div style="display: flex; align-items: center; gap: 12px; padding: 12px 16px; border-bottom: 1px solid #DFE1E6; background-color: #fff;">
                      <i class="fa-solid fa-check-circle" style="color: #36B37E; font-size: 18px;"></i>
                      <input type="text" v-model="newItem.title" placeholder="Tóm tắt quyết định là gì?" style="border: none; outline: none; background: transparent; width: 100%; font-size: 15px; font-weight: 500; color: #172B4D;" />
                    </div>
                  </template>
                </RichTextEditor>
                
            </div>
            <div v-if="!editing.decisions && goalStore.decisions?.length && isOwner" style="margin-bottom: 24px; padding-top: 16px;">
                <button class="secondary-btn" @click="editing.decisions = true">Thêm quyết định</button>
            </div>
            
            <div class="timeline-post" v-for="item in goalStore.decisions" :key="item.id" style="margin-bottom: 16px;">
                <div class="post-header">
                  <div class="post-user">
                    <AppAvatar :user="{ id: item.creatorId, fullName: item.creatorName, avatarUrl: item.creatorAvatarUrl, email: item.creatorEmail }" :size="32" />
                    <div class="user-info">
                      <span class="user-name">{{ item.creatorName }}</span>
                      <span class="post-time">{{ new Date(item.createdAt).toLocaleString('vi-VN') }}</span>
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
        <div class="sidebar-card">
          <div class="sidebar-card-header">
            <h3>Chi tiết</h3>
          </div>
          <div class="details-body">
            <!-- Tiến độ -->
            <div class="detail-row">
              <div class="detail-label">Tiến độ</div>
              <div class="detail-value progress-value">
                <div class="progress-bar-bg"><div class="progress-bar-fill" :style="{ width: (goal?.progress || 0) + '%' }"></div></div>
                <span>{{ goal?.progress || 0 }}%</span>
              </div>
            </div>

            <!-- Người theo dõi -->
            <div class="detail-row">
              <div class="detail-label">Người theo dõi <button v-if="isOwner" class="icon-btn-micro" @click="isShareModalOpen = true"><i class="fa-solid fa-plus"></i></button></div>
              <div class="detail-value flex-between">
                <span v-if="isOwner" class="empty-value" style="cursor: pointer;" @click="isShareModalOpen = true">Thêm người theo dõi</span>
                <span v-else class="empty-value" style="cursor: default; color: #5e6c84;">Chưa có người theo dõi</span>
                <div class="follower-icons">
                  <i class="fa-brands fa-slack ms-1"></i>
                  <i class="fa-brands fa-microsoft ms-1"></i>
                </div>
              </div>
            </div>

            <!-- Mục tiêu chính -->
            <div class="detail-row relative-popover-container">
              <div class="detail-label">Mục tiêu chính <button class="icon-btn-micro" v-if="isOwner && !linkedParentGoal" @click.stop="togglePopover('parentGoal')"><i class="fa-solid fa-plus"></i></button></div>
              
              <div class="detail-value" v-if="linkedParentGoal">
                <div class="linked-item">
                  <i class="fa-solid fa-bullseye item-icon"></i>
                  <span class="item-name">{{ linkedParentGoal.title }}</span>
                  <button v-if="isOwner" class="remove-btn" @click="linkedParentGoal = null"><i class="fa-solid fa-xmark"></i></button>
                </div>
              </div>
              <div class="detail-value" v-else-if="!isOwner">
                <span class="empty-value" style="cursor: default; color: #5e6c84;">Không có</span>
              </div>

              <!-- Parent Goal Popover -->
              <div class="custom-popover" v-if="popovers.parentGoal" @click.stop>
                <input type="text" class="popover-search" placeholder="Tìm kiếm mục tiêu hoặc dán liên kết" v-model="searchQueries.parentGoal" />
                <div class="popover-list-title">Kết quả</div>
                <div class="popover-list">
                  <div class="popover-item" v-for="g in filteredParentGoals" :key="g.id" @click="setParentGoal(g)">
                    <i class="fa-solid fa-bullseye item-icon-muted"></i>
                    <div class="item-details">
                      <div class="item-name">{{ g.title }}</div>
                      <div class="item-meta">{{ g.owner }}</div>
                    </div>
                  </div>
                </div>
                <div class="popover-footer-action">
                  <span class="popover-create-btn" @click="createGoal"><i class="fa-solid fa-plus"></i> Tạo mục tiêu</span>
                </div>
              </div>
            </div>

            <!-- Mục tiêu phụ -->
            <div class="detail-row relative-popover-container">
              <div class="detail-label">Mục tiêu phụ <button v-if="isOwner" class="icon-btn-micro" @click.stop="togglePopover('subGoals')"><i class="fa-solid fa-plus"></i></button></div>
              
              <div class="detail-value" v-if="linkedSubGoals.length > 0">
                <div class="linked-item" v-for="g in linkedSubGoals" :key="g.id">
                  <i class="fa-solid fa-bullseye item-icon"></i>
                  <span class="item-name">{{ g.title }}</span>
                  <button v-if="isOwner" class="remove-btn" @click="removeSubGoal(g.id)"><i class="fa-solid fa-xmark"></i></button>
                </div>
              </div>
              <div class="detail-value" v-else-if="!isOwner">
                <span class="empty-value" style="cursor: default; color: #5e6c84;">Không có</span>
              </div>

              <!-- Sub Goals Popover -->
              <div class="custom-popover" v-if="popovers.subGoals" @click.stop>
                <input type="text" class="popover-search" placeholder="Tìm kiếm mục tiêu hoặc dán liên kết" v-model="searchQueries.subGoals" />
                <div class="popover-list-title">Kết quả</div>
                <div class="popover-list">
                  <div class="popover-item" v-for="g in filteredSubGoals" :key="g.id" @click="addSubGoal(g)">
                    <i class="fa-solid fa-bullseye item-icon-muted"></i>
                    <div class="item-details">
                      <div class="item-name">{{ g.title }}</div>
                      <div class="item-meta">{{ g.owner }}</div>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Loại -->
            <div class="detail-row">
              <div class="detail-label">Loại</div>
              <div class="detail-value" style="display: flex; align-items: center; gap: 8px; font-size: 13.5px; color: #334155; font-weight: 500;">
                <i class="fa-solid fa-bullseye" style="color: #0c66e4;"></i> Mục tiêu
              </div>
            </div>

            <!-- Nhóm -->
            <div class="detail-row relative-popover-container">
              <div class="detail-label">Nhóm <button v-if="isOwner" class="icon-btn-micro" @click.stop="togglePopover('teams')"><i class="fa-solid fa-plus"></i></button></div>
              
              <div class="detail-value" v-if="linkedTeams.length > 0">
                <div class="linked-item team-item-chip" v-for="t in linkedTeams" :key="t.id">
                  <div class="team-icon" :style="{ backgroundColor: t.color }"><i class="fa-solid fa-users"></i></div>
                  <span class="item-name">{{ t.name }} <i class="fa-solid fa-circle-check text-blue" style="font-size: 12px;" v-if="t.verified"></i></span>
                  <button v-if="isOwner" class="remove-btn" @click="removeTeam(t.id)"><i class="fa-solid fa-xmark"></i></button>
                </div>
              </div>
              <div class="detail-value" v-else-if="!isOwner">
                <span class="empty-value" style="cursor: default; color: #5e6c84;">Chưa liên kết</span>
              </div>

              <!-- Teams Popover -->
              <div class="custom-popover popover-large" v-if="popovers.teams" @click.stop style="width: 320px;">
                <div class="search-input-with-icon">
                  <i class="fa-solid fa-user-group icon-left"></i>
                  <input type="text" class="popover-search with-icon" placeholder="Tìm kiếm đội ngũ" v-model="searchQueries.teams" />
                </div>
                <div class="popover-list mt-2" style="max-height: 250px; overflow-y: auto;">
                  <div class="popover-item team-select-item" v-for="t in filteredTeams" :key="t.id" @click="addTeam(t)">
                    <div class="team-icon-large" :style="{ backgroundColor: t.color }"><i class="fa-solid fa-users"></i></div>
                    <div class="item-details">
                      <div class="item-name" style="font-weight: 500;">{{ t.name }} <i class="fa-solid fa-circle-check text-blue" style="font-size: 12px;" v-if="t.verified"></i></div>
                      <div class="item-meta">Đội ngũ chính thức • {{ t.members }} thành viên, kể cả bạn</div>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Ngày bắt đầu -->
            <div class="detail-row relative-popover-container">
              <div class="detail-label">Ngày bắt đầu <button v-if="isOwner" class="icon-btn-micro" @click.stop="togglePopover('startDate')"><i class="fa-solid fa-plus"></i></button></div>
              <div class="detail-value" v-if="startDate">
                <div class="linked-item">
                  <i class="fa-regular fa-calendar"></i>
                  <span class="item-name">{{ formattedStartDate }}</span>
                </div>
              </div>
              <div class="detail-value" v-else-if="!isOwner">
                <span class="empty-value" style="cursor: default; color: #5e6c84;">Chưa bắt đầu</span>
              </div>

              <!-- Start Date Popover -->
              <div class="custom-popover" v-if="popovers.startDate" @click.stop style="width: 300px; padding: 0;">
                <div style="padding: 16px; border-bottom: 1px solid rgba(148, 163, 184, 0.08);">
                  <h4 style="margin: 0 0 8px 0; font-size: 13.5px; font-weight: 600; color: #334155;">Ngày bắt đầu</h4>
                  <div class="date-input-control">
                    <input type="date" v-model="startDateInput" />
                    <i class="fa-regular fa-calendar"></i>
                  </div>
                </div>
                <div class="popover-actions" style="padding: 12px 16px; display: flex; justify-content: space-between; gap: 8px;">
                  <button class="secondary-btn" style="flex: 1;" @click="popovers.startDate = false">Hủy</button>
                  <button class="primary-btn" style="flex: 1;" @click="saveStartDate">Lưu</button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Share Modal -->
    <ShareModal 
      :isOpen="isShareModalOpen" 
      :entityId="goal?.id" 
      entityType="Goal"
      :entityName="goal?.title" 
      :workspaceId="goal?.workspaceId"
      :owner="{ fullName: goal?.owner, avatarColor: goal?.ownerColor, avatarUrl: goal?.ownerAvatarUrl }"
      @close="isShareModalOpen = false" 
    />
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useGoalStore } from '@/store/useGoalStore'
import { useHomeProjectStore } from '@/store/useHomeProjectStore'
import { usePeopleStore } from '@/store/usePeopleStore'
import axiosClient from '@/api/axiosClient'
import { signalRService } from '@/api/signalrService'
import { useTeamStore } from '@/store/useTeamStore'
import { useStarredStore } from '@/store/useStarredStore'
import { useAuthStore } from '@/store/useAuthStore'
import { hasSystemAdminAccess } from '@/utils/permissions'
import RichTextEditor from '@/components/common/RichTextEditor.vue'
import ShareModal from '@/components/common/ShareModal.vue'
import CommentSection from '@/components/common/CommentSection.vue'

import AppPageLayout from '@/components/common/Foundation/AppPageLayout.vue'
import AppPageHeader from '@/components/common/Foundation/AppPageHeader.vue'
import AppToolbar from '@/components/common/Foundation/AppToolbar.vue'
import AppSearchInput from '@/components/common/Foundation/AppSearchInput.vue'
import AppCard from '@/components/common/Foundation/AppCard.vue'
import AppEmptyState from '@/components/common/Foundation/AppEmptyState.vue'
import AppStatusBadge from '@/components/common/Foundation/AppStatusBadge.vue'
import AppAvatar from '@/components/common/Foundation/AppAvatar.vue'

import DOMPurify from 'dompurify'

const route = useRoute()
const router = useRouter()
const goalsBasePath = computed(() => route.path.startsWith('/goals') ? '/goals' : '/home/goals')
const goalStore = useGoalStore()
const projectStore = useHomeProjectStore()
const teamStore = useTeamStore()
const peopleStore = usePeopleStore()
const starredStore = useStarredStore()
const authStore = useAuthStore()

const isOwner = computed(() => {
  if (!goal.value) return false
  const currentUserId = authStore.userId
  const ownerId = goal.value.ownerId || goal.value.creatorId
  return currentUserId === ownerId || hasSystemAdminAccess()
})

const isGoalStarred = computed(() => goal.value?.id && starredStore.isStarred('Goal', goal.value.id))
const siteProjects = computed(() => projectStore.projects || [])

const myInitials = computed(() => {
  const name = goalStore.currentGoal?.owner || 'User'
  return name.charAt(0).toUpperCase()
})


const newItem = ref({ title: '', text: '' })

const saveLearning = async () => {
    if (!newItem.value.title || !newItem.value.text) return;
    await goalStore.addGoalLesson(goal.value?.id, newItem.value);
    newItem.value = { title: '', text: '' };
    editing.value.learnings = false;
}
const saveRisk = async () => {
    if (!newItem.value.title || !newItem.value.text) return;
    await goalStore.addGoalRisk(goal.value?.id, newItem.value);
    newItem.value = { title: '', text: '' };
    editing.value.risks = false;
}
const saveDecision = async () => {
    if (!newItem.value.title || !newItem.value.text) return;
    await goalStore.addGoalDecision(goal.value?.id, newItem.value);
    newItem.value = { title: '', text: '' };
    editing.value.decisions = false;
}

const editing = ref({ learnings: false, risks: false, decisions: false })

const siteUsers = computed(() => peopleStore.users || [])
const goalOwnerData = computed(() => {
  if (!goal.value) return null
  const ownerId = goal.value.ownerId || goal.value.creatorId
  const ownerName = goal.value.owner || goal.value.ownerName || goal.value.creatorName
  if (ownerId) return siteUsers.value.find(u => u.id === ownerId) || { id: ownerId, fullName: ownerName }
  if (ownerName) return siteUsers.value.find(u => u.fullName === ownerName) || { fullName: ownerName }
  return null
})

const currentTab = ref('overview')
const activityTab = ref('history')
const showUpdateForm = ref(false)
const isUpdateStatusOpen = ref(false)
const showStatusMenu = ref(false)

const newUpdateForm = ref({
  content: '',
  status: 'ĐANG CHỜ XỬ LÝ',
  progress: 0
})

const isSprintAInputOpen = ref(false)
const isProjectSearchOpen = ref(false)
const isEditingLearning = ref(false)
const isEditingRisk = ref(false)
const isEditingDecision = ref(false)
const isLayoutMenuOpen = ref(false)
const isLayoutMenuOpenRisk = ref(false)
const isLayoutMenuOpenDecision = ref(false)

const isEditingBio = ref(false)
const tempBio = ref('')

const goal = computed(() => goalStore.currentGoal)
const goalProgressChartData = computed(() => ({
  ...(goal.value || {}),
  updates: goalStore.updates || []
}))
const sanitizeHtml = (value) => DOMPurify.sanitize(value || '')
const safeGoalDescription = computed(() => sanitizeHtml(goal.value?.description || ''))
const updates = computed(() => goalStore.updates || [])
const goalHistory = computed(() => {
  const explicitHistory = goalStore.history || goal.value?.history || []
  if (explicitHistory.length) {
    return explicitHistory.map((entry, index) => ({
      id: entry.id || `history-${index}`,
      actor: entry.actor || entry.userName || entry.createdByName || 'Hệ thống',
      email: entry.creatorEmail || entry.authorEmail,
      action: entry.action || entry.description || 'đã cập nhật mục tiêu',
      target: entry.target || entry.fieldName || goal.value?.title || '',
      createdAt: entry.createdAt || entry.timestamp || entry.updatedAt
    }))
  }

  const updateHistory = (goalStore.updates || []).map((update, index) => ({
    id: update.id || `update-${index}`,
    actor: update.userName || update.createdByName || goal.value?.owner || goal.value?.ownerName || 'Hệ thống',
    action: 'đã đăng bản cập nhật',
    target: update.content || update.status || goal.value?.title || '',
    createdAt: update.createdAt || update.updatedAt
  }))

  if (updateHistory.length) return updateHistory

  return [{
    id: goal.value?.id || 'created',
    actor: goal.value?.owner || goal.value?.ownerName || goal.value?.creatorName || 'Hệ thống',
    action: 'đã tạo mục tiêu',
    target: goal.value?.title || '',
    createdAt: goal.value?.createdAt || goal.value?.updatedAt || new Date().toISOString()
  }]
})

const handleRealtimeGoalChange = event => goalStore.applyRealtimeEntityEvent(event)

onMounted(async () => {
  const workspaceId = await goalStore.ensureWorkspaceId()
  signalRService.on('EntityChanged', handleRealtimeGoalChange)
  await signalRService.startWorkspaceConnection(workspaceId)
  if (projectStore.projects.length === 0) await projectStore.fetchProjects();
  if (goalStore.goals.length === 0) await goalStore.fetchGoals();
  if (teamStore.allTeams.length === 0) await teamStore.fetchAllTeams();
  if (peopleStore.users.length === 0) await peopleStore.fetchPeople('', 1, 100);
  if (route.params.id) {
    await goalStore.fetchGoalDetail(route.params.id)
    const dateValue = goal.value?.startDate || goal.value?.dueDate || goal.value?.date || null
    startDate.value = dateValue
    startDateInput.value = dateValue ? new Date(dateValue).toISOString().slice(0, 10) : ''
    if (goal.value?.id) {
      try {
        await starredStore.recordViewed('Goal', goal.value.id)
      } catch (err) {
        console.warn('Failed to record recent goal view', err)
      }
    }
  }
})

onUnmounted(() => {
  signalRService.off('EntityChanged', handleRealtimeGoalChange)
})

const isShareModalOpen = ref(false)

const popovers = ref({
  parentGoal: false,
  subGoals: false,
  teams: false,
  startDate: false
})

const togglePopover = (type) => {
  for (const key in popovers.value) {
    if (key === type) popovers.value[key] = !popovers.value[key]
    else popovers.value[key] = false
  }
}

const closePopovers = (e) => {
  if (!e.target.closest('.custom-popover') && !e.target.closest('.icon-btn-micro')) {
    popovers.value = { parentGoal: false, subGoals: false, teams: false, startDate: false }
  }
}

const searchQueries = ref({ parentGoal: '', subGoals: '', teams: '' })

const linkedParentGoal = ref(null)
const linkedSubGoals = ref([])
const linkedTeams = ref([])
const startDate = ref(null)
const startDateInput = ref('')
const formattedStartDate = computed(() => {
  if (!startDate.value) return ''
  return new Date(startDate.value).toLocaleDateString('vi-VN')
})


const filteredParentGoals = computed(() => {
  if (!goalStore.goals) return []
  let list = goalStore.goals.filter(g => g.id !== goal.value?.id) // Cannot be self
  if (linkedSubGoals.value.length > 0) {
    const subIds = linkedSubGoals.value.map(s => s.id)
    list = list.filter(g => !subIds.includes(g.id)) // Cannot be a subgoal
  }
  if (searchQueries.value.parentGoal) {
    list = list.filter(g => g.title && g.title.toLowerCase().includes(searchQueries.value.parentGoal.toLowerCase()))
  }
  return list
})

const filteredSubGoals = computed(() => {
  if (!goalStore.goals) return []
  let list = goalStore.goals.filter(g => g.id !== goal.value?.id) // Cannot be self
  if (linkedParentGoal.value) {
    list = list.filter(g => g.id !== linkedParentGoal.value.id) // Cannot be parent goal
  }
  const linkedIds = linkedSubGoals.value.map(s => s.id)
  list = list.filter(g => !linkedIds.includes(g.id)) // Cannot be already linked
  if (searchQueries.value.subGoals) {
    list = list.filter(g => g.title && g.title.toLowerCase().includes(searchQueries.value.subGoals.toLowerCase()))
  }
  return list
})

const filteredTeams = computed(() => {
  if (!teamStore.allTeams) return []
  let list = teamStore.allTeams
  const linkedIds = linkedTeams.value.map(t => t.id)
  list = list.filter(t => !linkedIds.includes(t.id))
  if (searchQueries.value.teams) {
    list = list.filter(t => t.name && t.name.toLowerCase().includes(searchQueries.value.teams.toLowerCase()))
  }
  return list
})




const setParentGoal = (g) => {
  linkedParentGoal.value = g
  popovers.value.parentGoal = false
}

const addSubGoal = (g) => {
  if (!linkedSubGoals.value.find(x => x.id === g.id)) linkedSubGoals.value.push(g)
  popovers.value.subGoals = false
}
const removeSubGoal = (id) => { linkedSubGoals.value = linkedSubGoals.value.filter(x => x.id !== id) }

const addTeam = (t) => {
  if (!linkedTeams.value.find(x => x.id === t.id)) linkedTeams.value.push(t)
  popovers.value.teams = false
}
const removeTeam = (id) => { linkedTeams.value = linkedTeams.value.filter(x => x.id !== id) }

const saveStartDate = async () => {
  if (!goal.value?.workspaceId || !goal.value?.id || !startDateInput.value) {
    popovers.value.startDate = false
    return
  }

  await axiosClient.put(`/workspaces/${goal.value.workspaceId}/goals/${goal.value.id}`, {
    startDate: startDateInput.value,
    dueDate: startDateInput.value
  })
  await goalStore.fetchGoalDetail(goal.value.id)
  startDate.value = startDateInput.value
  popovers.value.startDate = false
}

const selectStatus = (status) => {
  newUpdateForm.value.status = status
  showStatusMenu.value = false
}

const getPreviousStatus = (update) => update.oldStatus || update.previousStatus || update.OldStatus || update.PreviousStatus
const getCurrentStatus = (update) => update.newStatus || update.status || update.NewStatus || update.Status
const getUpdateProgress = (update) => {
  const value = update.newProgress ?? update.progress ?? update.NewProgress ?? update.Progress
  return value === undefined || value === null ? null : value
}

const getInitials = (value = '') => {
  const text = String(value || '').trim()
  if (!text) return 'H'
  return text
    .split(/\s+/)
    .slice(0, 2)
    .map(part => part.charAt(0).toUpperCase())
    .join('')
}

const formatDate = (value) => {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

const startEditingBio = () => {
  tempBio.value = goal.value?.description || ''
  isEditingBio.value = true
}

const saveBio = async () => {
  if (!goal.value?.id) return
  await axiosClient.put(`/workspaces/${goal.value.workspaceId}/goals/${goal.value.id}`, {
    ...goal.value,
    description: tempBio.value
  })
  await goalStore.fetchGoalDetail(goal.value.id)
  isEditingBio.value = false
}

const toggleShare = () => {
  isShareModalOpen.value = true
}

const toggleMenu = () => {
  isLayoutMenuOpen.value = !isLayoutMenuOpen.value
}


  const editingUpdateId = ref(null);
  const editingContent = ref('');
  const editUpdate = (update) => {
    editingUpdateId.value = update.id;
    editingContent.value = update.content;
  };
  const cancelInlineEdit = () => {
    editingUpdateId.value = null;
    editingContent.value = '';
  };
  const saveInlineEdit = async (update) => {
    try {
      const payload = {
        content: editingContent.value
      };
      await axiosClient.put(`/workspaces/${goal.value.workspaceId}/goals/${goal.value.id}/updates/${update.id}`, payload);
      await goalStore.fetchGoalDetail(goal.value.id);
    if (newUpdateForm.value.progress !== undefined) {
       goal.value.progress = newUpdateForm.value.progress;
    }
      editingUpdateId.value = null;
    } catch (e) {
      console.error(e);
    }
  };

  const deleteUpdate = async (updateId) => {
    if (!confirm('Bạn có chắc chắn muốn xóa bản cập nhật này không?')) return;
    try {
      await axiosClient.delete(`/workspaces/${goal.value.workspaceId}/goals/${goal.value.id}/updates/${updateId}`);
      await goalStore.fetchGoalDetail(goal.value.id);
    } catch (error) {
      console.error('Failed to delete update:', error);
      alert('Không thể xóa bản cập nhật.');
    }
  };
    
const submitGoalUpdate = async () => {
  if (!newUpdateForm.value.content.trim()) return
  try {
    const payload = {
      content: newUpdateForm.value.content,
      status: newUpdateForm.value.status,
      progress: newUpdateForm.value.progress
    };
    
    if (newUpdateForm.value.id) {
      // Edit
      await axiosClient.put(`/workspaces/${goal.value.workspaceId}/goals/${goal.value.id}/updates/${newUpdateForm.value.id}`, payload);
    } else {
      // Create
      await axiosClient.post(`/workspaces/${goal.value.workspaceId}/goals/${goal.value.id}/updates`, payload);
    }

    await goalStore.fetchGoalDetail(goal.value.id);
    
    newUpdateForm.value.content = ''
    newUpdateForm.value.id = null
  } catch (error) {
    console.error('Failed to submit update:', error)
}
}

const getStatusClass = (status) => {
  if (!status) return 'status-pending'
  const map = {
    'đúng tiến độ': 'status-pending',
    'có rủi ro': 'status-at-risk',
    'trễ tiến độ': 'status-off-track',
    'đang chờ cập nhật': 'status-pending',
    'đã hoàn tất': 'status-done',
    'đã lưu trữ': 'status-archived'
  }
  return map[status.toLowerCase()] || 'status-pending'
}

const toggleFollow = () => {
  if (goal.value) goalStore.toggleFollow(goal.value?.id)
}

const toggleStar = async () => {
  try {
    await goalStore.toggleStar()
  } catch (error) {
    console.error('Failed to update starred goal', error)
  }
}

const postUpdate = () => {
  showUpdateForm.value = false
  newUpdateForm.value.content = ''
}
</script>

<style scoped>
.goal-detail-wrapper {
  display: flex;
  flex-direction: column;
  position: relative;
  margin: -8px 0 0;
  background-color: #ffffff;
  min-height: 100vh;
  width: 100% !important;
  max-width: none !important;
}

.goal-cover {
  height: 200px;
  background: linear-gradient(135deg, #0747a6 0%, #0052cc 50%, #2684ff 100%);
  position: relative;
  flex-shrink: 0;
}

.back-floating-btn {
  position: absolute;
  top: 16px;
  left: 18px;
  z-index: 100;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: rgba(255, 255, 255, 0.85);
  backdrop-filter: blur(8px);
  border: 1px solid rgba(148, 163, 184, 0.2);
  border-radius: 20px;
  color: #172b4d;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
  transition: all 0.2s ease;
}

.back-floating-btn:hover {
  background: #ffffff;
  transform: translateY(-1px);
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.12);
  color: #0052cc;
}

.goal-header-wrapper {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  padding: 0 18px;
  margin-top: -32px;
  margin-bottom: 24px;
  flex-shrink: 0;
}

.goal-identity {
  display: flex;
  align-items: flex-end;
  gap: 20px;
}

.goal-icon-large {
  width: 96px;
  height: 96px;
  background-color: #0052cc;
  color: white;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 32px;
  font-weight: bold;
  border: 4px solid #ffffff;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.1);
  z-index: 2;
}

.goal-title-block {
  padding-bottom: 0;
}

.title-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.title-row h1 {
  margin: 0;
  font-size: 28px;
  font-weight: 600;
  color: #172b4d;
}

.goal-meta-subtitle {
  margin: 4px 0 0 0;
  font-size: 13px;
  color: #5e6c84;
}

.goal-meta-subtitle strong {
  color: #172b4d;
}

.header-actions {
  display: flex;
  gap: 8px;
}

.secondary-btn {
  background-color: rgba(9, 30, 66, 0.04);
  color: #42526E;
  border: none;
  padding: 6px 12px;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
  transition: background-color 0.2s;
}

.secondary-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.secondary-btn.icon-only {
  padding: 6px 8px;
}

.secondary-btn.starred i {
  color: #FFAB00;
}

.goal-star-button {
  appearance: none;
  -webkit-appearance: none;
  width: 40px;
  height: 40px;
  padding: 0;
  border: 0;
  background: rgba(9, 30, 66, 0.04);
  color: #42526e;
  font: inherit;
  cursor: pointer;
  touch-action: manipulation;
  justify-content: center;
  border-radius: 9px;
}
.goal-star-button:focus-visible {
  outline: 3px solid color-mix(in srgb, var(--color-accent, #0c66e4) 42%, transparent);
  outline-offset: 2px;
}
.goal-star-button:disabled { cursor: wait; }
.goal-star-button i { width: 1em; line-height: 1; text-align: center; }

.quick-status-row {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-left: 44px; /* Align with title text */
}

/* Status Badge */
.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 2px 8px;
  border-radius: 3px;
  font-size: 12px;
  font-weight: 700;
  text-transform: uppercase;
}

.status-badge-small {
  display: inline-flex;
  align-items: center;
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

/* Status Colors */
.status-pending { background-color: #E3FCEF; color: #006644; }
.status-pending .status-dot { background-color: #36B37E; }

.status-at-risk { background-color: #FFF0B3; color: #FF8B00; }
.status-at-risk .status-dot { background-color: #FFAB00; }

.status-off-track { background-color: #FFEBE6; color: #BF2600; }
.status-off-track .status-dot { background-color: #FF5630; }

.status-done { background-color: #EAE6FF; color: #403294; }
.status-done .status-dot { background-color: #6554C0; }

.status-pending { background-color: #DFE1E6; color: #42526E; }
.status-pending .status-dot { background-color: #7A869A; }

.update-text {
  font-size: 13px;
  color: #5E6C84;
}

/* Content Grid */
.goal-content-grid {
  display: flex;
  gap: 32px;
  padding: 8px 18px 32px;
}

.main-column {
  flex: 1;
  min-width: 0;
}

.side-column {
  width: 320px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.content-section {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.section-header h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #172B4D;
}

.icon-btn-small {
  background: none;
  border: none;
  color: #5E6C84;
  cursor: pointer;
  padding: 4px;
  border-radius: 3px;
}

.icon-btn-small:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.empty-text {
  color: #5E6C84;
  font-style: italic;
  font-size: 14px;
}

/* Empty States */
.empty-state-micro {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 32px;
  background-color: #FAFBFC;
  border: 1px dashed #DFE1E6;
  border-radius: 3px;
  color: #5E6C84;
  text-align: center;
}

.empty-icon-micro {
  font-size: 24px;
  margin-bottom: 8px;
  color: #A5ADBA;
}

/* Status Updates Area */
.status-update-box {
  margin-bottom: 24px;
}

.update-input-controlup {
  display: flex;
  align-items: center;
  gap: 12px;
  cursor: pointer;
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

.update-input-placeholder {
  flex: 1;
  padding: 10px 16px;
  border: 1px solid #DFE1E6;
  border-radius: 24px;
  color: #5E6C84;
  font-size: 14px;
  transition: border-color 0.2s;
}

.update-input-placeholder:hover {
  border-color: #A5ADBA;
}

.update-form-active {
  background-color: #FFFFFF;
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  padding: 16px;
  box-shadow: 0 4px 8px -2px rgba(9,30,66,0.25);
}

.form-group {
  margin-bottom: 16px;
}

.form-group label {
  display: block;
  font-size: 12px;
  font-weight: 600;
  color: #5E6C84;
  margin-bottom: 8px;
}

.jira-select, .jira-textarea {
  width: 100%;
  padding: 8px 12px;
  border: 2px solid #DFE1E6;
  border-radius: 3px;
  font-size: 14px;
  font-family: inherit;
  outline: none;
  box-sizing: border-box;
}

.jira-select:focus, .jira-textarea:focus {
  border-color: #4C9AFF;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.primary-btn {
  background-color: #0052CC;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 3px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
}

.primary-btn:hover:not(:disabled) {
  background-color: #0047B3;
}

.primary-btn:disabled {
  background-color: #EBECF0;
  color: #A5ADBA;
  cursor: not-allowed;
}

.cancel-btn {
  background: transparent;
  color: #5E6C84;
  border: none;
  padding: 8px 16px;
  border-radius: 3px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
}

.cancel-btn:hover {
  background: rgba(9,30,66,0.08);
}

/* Timeline */
.timeline-list {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.timeline-item {
  display: flex;
  gap: 16px;
}

.timeline-avatar {
  width: 32px;
  height: 32px;
  background-color: #172B4D;
  color: white;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: 600;
}

.timeline-content {
  flex: 1;
}

.timeline-meta {
  font-size: 14px;
  color: #172B4D;
  margin-bottom: 4px;
}

.timeline-date {
  color: #5E6C84;
  margin-left: 8px;
  font-size: 12px;
}

.timeline-status-change {
  margin-bottom: 8px;
}

.timeline-message {
  font-size: 14px;
  color: #172B4D;
  line-height: 1.5;
  background-color: #FAFBFC;
  padding: 12px;
  border-radius: 3px;
  border: 1px solid #DFE1E6;
}

.empty-timeline {
  color: #5E6C84;
  font-size: 14px;
  font-style: italic;
}

/* Sidebar Card styling */
.sidebar-card {
  background: #ffffff;
  border: 1px solid rgba(148, 163, 184, 0.15);
  border-radius: 12px;
  padding: 20px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02);
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.sidebar-card h3 {
  font-size: 13px;
  font-weight: 700;
  color: #475569;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.sidebar-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.details-body {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.detail-row {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.detail-label {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 12px;
  font-weight: 700;
  color: #64748b;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.icon-btn-micro {
  background: transparent;
  border: none;
  color: #64748b;
  cursor: pointer;
  width: 20px;
  height: 20px;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}

.icon-btn-micro:hover {
  background-color: #f1f5f9;
  color: #1e293b;
}

.detail-value {
  font-size: 13.5px;
  color: #334155;
}

.empty-value {
  color: #94a3b8;
  font-style: italic;
  font-size: 13px;
}

.progress-value {
  display: flex;
  align-items: center;
  gap: 10px;
}

.progress-bar-bg {
  flex: 1;
  height: 8px;
  background-color: #f1f5f9;
  border-radius: 4px;
  overflow: hidden;
}

.progress-bar-fill {
  height: 100%;
  background: linear-gradient(90deg, #36b37e, #4cde9e);
  border-radius: 4px;
}

.progress-text {
  font-size: 13px;
  font-weight: 600;
  color: #334155;
}

.owner-chip {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  background: #f8fafc;
  border: 1px solid rgba(148, 163, 184, 0.12);
  border-radius: 6px;
}

.owner-avatar-micro {
  width: 20px;
  height: 20px;
}

.owner-name {
  font-weight: 500;
  color: #334155;
}

.follower-icons {
  display: flex;
  align-items: center;
  gap: 4px;
}

.follower-icons i {
  color: #64748b;
  font-size: 14px;
}

.linked-item {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 6px 12px;
  background: #f8fafc;
  border: 1px solid rgba(148, 163, 184, 0.12);
  border-radius: 6px;
  width: 100%;
  box-sizing: border-box;
}

.linked-item .item-icon {
  color: #0c66e4;
}

.linked-item .item-name {
  font-size: 13px;
  font-weight: 500;
  color: #334155;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  flex: 1;
}

.linked-item .remove-btn {
  background: transparent;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  padding: 2px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s;
}

.linked-item .remove-btn:hover {
  color: #ef4444;
  background: rgba(239, 68, 68, 0.08);
}

.tabs-nav {
  display: flex;
  align-items: center;
  gap: 6px !important;
  width: max-content !important;
  max-width: calc(100% - 36px);
  min-height: 42px;
  margin: 0 18px 12px !important;
  padding: 4px !important;
  border: 1px solid rgba(148, 163, 184, 0.2) !important;
  border-radius: 9px !important;
  background: transparent !important;
  box-shadow: none !important;
  overflow-x: auto;
  flex-shrink: 0;
}

.tab-btn {
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 34px !important;
  min-width: max-content;
  padding: 0 16px !important;
  border: 0 !important;
  border-radius: 7px !important;
  background: transparent !important;
  color: #475569 !important;
  font-size: 12.5px !important;
  font-weight: 800 !important;
  line-height: 1;
  text-decoration: none;
  white-space: nowrap;
  transition: background 0.18s ease, color 0.18s ease;
  cursor: pointer;
}

.tab-btn:hover {
  color: #0f172a !important;
  background: rgba(14, 165, 233, 0.06) !important;
}

.tab-btn.active {
  color: #0369a1 !important;
  background: linear-gradient(135deg, rgba(34, 211, 238, 0.20), rgba(45, 212, 191, 0.14)) !important;
  box-shadow: none !important;
}

.badge-count {
  background-color: #f1f5f9;
  color: #475569;
  padding: 2px 6px;
  border-radius: 12px;
  font-size: 11px;
  font-weight: 600;
  margin-left: 6px;
}

.toggle-btn {
  background: none;
  border: none;
  padding: 6px 12px;
  font-size: 13px;
  font-weight: 500;
  color: #5E6C84;
  cursor: pointer;
  border-radius: 3px;
}

.toggle-btn:hover {
  background: rgba(9,30,66,0.04);
}

.toggle-btn.active {
  background: rgba(9,30,66,0.08);
  color: #172B4D;
}

.toolbar-icon {
  color: #5E6C84;
  font-size: 14px;
  padding: 6px;
  border-radius: 3px;
  cursor: pointer;
  transition: background 0.2s;
  font-size: 12px;
}
.toolbar-icon:hover {
  background: #FAFBFC;
}

.reaction-btn { border: none !important; background: transparent; }
.reaction-btn:hover {
  background-color: #FAFBFC;
}

/* Sidebar Popovers (Shared with ProjectDetail) */
.relative-popover-container {
  position: relative;
}

.custom-popover {
  position: absolute;
  top: 100%;
  right: 0;
  width: 300px;
  background: white;
  border-radius: 8px;
  border: none !important;
  box-shadow: 0 8px 30px rgba(0,0,0,0.08);
  padding: 12px;
  z-index: 100;
  margin-top: 6px;
}

.custom-popover.popover-large {
  width: 320px;
  padding: 16px;
}

.popover-search {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid rgba(148, 163, 184, 0.2);
  border-radius: 6px;
  font-size: 13.5px;
  outline: none;
  transition: border-color 0.15s ease;
  box-sizing: border-box;
  color: #334155;
}

.popover-search:focus {
  border-color: #0c66e4;
}

.popover-footer-action {
  padding: 12px 12px 0;
  border-top: 1px solid rgba(148, 163, 184, 0.08);
  margin-top: 8px;
}

.popover-create-btn {
  font-size: 13px;
  color: #0c66e4;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 500;
}

.popover-create-btn:hover {
  text-decoration: underline;
}

.search-input-with-icon {
  position: relative;
}

.search-input-with-icon .icon-left {
  position: absolute;
  left: 10px;
  top: 50%;
  transform: translateY(-50%);
  color: #5E6C84;
}

.popover-search.with-icon {
  padding-left: 32px;
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
  padding: 8px 10px;
  border-radius: 6px;
  cursor: pointer;
  transition: background-color 0.15s ease;
}

.popover-item:hover {
  background-color: #f1f5f9;
}

.item-icon-muted {
  color: #64748b;
  font-size: 14px;
  width: 16px;
  text-align: center;
}

.item-details {
  display: flex;
  flex-direction: column;
}

.item-name {
  font-size: 13px;
  font-weight: 500;
  color: #334155;
}

.item-meta {
  font-size: 11px;
  color: #64748b;
}

/* Team Icons and Chips */
.team-icon {
  width: 20px;
  height: 20px;
  border-radius: 3px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  font-size: 10px;
}

.team-icon-large {
  width: 32px;
  height: 32px;
  border-radius: 3px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  font-size: 16px;
}

.team-item-chip {
  display: flex;
  align-items: center;
  gap: 8px;
}

.team-select-item {
  padding: 8px;
  border-radius: 3px;
}

.team-select-item:hover {
  background-color: #F4F5F7;
}

.text-blue {
  color: #0052CC;
}

/* Start Date Calendar Custom Styles */
.date-input-control {
  position: relative;
}
.date-input-control input {
  width: 100%;
  padding: 6px 8px 6px 32px;
  border: 2px solid #DFE1E6;
  border-radius: 3px;
  font-size: 14px;
  color: #172B4D;
  box-sizing: border-box;
  background-color: #FAFBFC;
}
.date-input-control i {
  position: absolute;
  left: 10px;
  top: 50%;
  transform: translateY(-50%);
  color: #5E6C84;
}
.calendar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}
.cal-nav-btn {
  background: transparent;
  border: none;
  cursor: pointer;
  color: #5E6C84;
  padding: 4px;
  border-radius: 3px;
}
.cal-nav-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
}
.cal-month-year {
  font-weight: 600;
  font-size: 14px;
  color: #172B4D;
}
.calendar-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 4px;
  text-align: center;
}
.cal-day-header {
  font-size: 11px;
  font-weight: 600;
  color: #6B778C;
  padding-bottom: 8px;
}
.cal-day {
  font-size: 14px;
  color: #172B4D;
  padding: 6px 0;
  border-radius: 3px;
  cursor: pointer;
}
.cal-day:hover {
  background-color: #F4F5F7;
}
.cal-day.muted {
  color: #A5ADBA;
}
.cal-day.active {
  background-color: #0052CC;
  color: white;
  font-weight: 600;
}
.cal-day.active:hover {
  background-color: #0047B3;
}

/* Badge Count */
.badge-count {
  background-color: #DFE1E6;
  color: #172B4D;
  font-size: 12px;
  font-weight: 600;
  padding: 2px 6px;
  border-radius: 12px;
  margin-left: 8px;
}

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


/* CSS extracted from ProjectDetail.vue for GoalDetail.vue consistency */
.update-editor-box {
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  background-color: #FFFFFF;
  box-shadow: 0 4px 8px -2px rgba(9,30,66,0.25);
  margin-bottom: 40px;
}
.updates-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
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
.status-dropdown-wrapper {
  position: relative;
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
.status-dropdown-menu {
  position: absolute;
  top: 100%;
  left: 0;
  margin-top: 4px;
  background: white;
  border: none !important;
  border-radius: 8px;
  box-shadow: 0 8px 30px rgba(0,0,0,0.08);
  padding: 8px 0;
  z-index: 10;
  min-width: 180px;
}
.status-option {
  padding: 6px 12px;
  cursor: pointer;
}
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
.template-link {
  justify-content: center;
  color: #5E6C84;
  font-size: 12px;
  cursor: pointer;
}
.update-editor-body {
  padding: 16px;
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
.editor-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}
.empty-state-large-tab {
  display: flex;
  align-items: center;
  padding: 40px;
  background-color: #FFFFFF;
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  gap: 40px;
  margin-top: 16px;
}
.empty-illustration {
  flex-shrink: 0;
  width: 120px;
  height: 120px;
  display: flex;
  align-items: center;
  justify-content: center;
}
.empty-text-content {
  flex: 1;
}
.empty-actions {
  display: flex;
  gap: 16px;
  align-items: center;
}
.link-btn {
  color: #0052CC;
  text-decoration: none;
  font-size: 14px;
  font-weight: 500;
}


.seamless-textarea {
  border: none !important;
  background: transparent !important;
  box-shadow: none !important;
  padding: 0 !important;
  resize: none !important;
}
.seamless-textarea:focus {
  outline: none !important;
}
</style>
