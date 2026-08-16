<template>
  <div class="team-detail-container" v-if="team">
    <!-- Cover Image -->
    <div class="team-cover" :style="{ backgroundImage: `url(${team.coverImage})` }">
      <div class="cover-overlay" v-if="!isArchived">
        <button class="upload-cover-btn"><i class="fa-solid fa-camera"></i> Change Cover</button>
      </div>
    </div>

    <!-- Header Actions -->
    <div class="team-header-wrapper">
      <div class="team-identity">
        <div class="team-avatar">
          <span>{{ team.avatarText }}</span>
        </div>
        <div class="team-title-block" style="padding-bottom: 0;">
          <div class="title-row">
            <h1 style="margin: 0; font-size: 28px;">{{ team.name }}</h1>
          </div>
          <div class="team-status-row" style="margin-top: 4px; font-size: 13px; color: #42526E; display: flex; align-items: center;">
            <span v-if="!isArchived"><i class="fa-solid fa-circle-check" style="color: #0052cc;"></i> Đội ngũ chính thức</span>
            <span v-else><i class="fa-solid fa-box-archive"></i> Đã lưu trữ</span>
            <span class="ms-2 status-badge" :class="!isArchived ? 'active' : 'archived'" style="font-size: 10px; padding: 2px 6px;">{{ !isArchived ? 'ACTIVE' : 'ARCHIVED' }}</span>
          </div>
        </div>
      </div>

      <div class="header-actions">
        <!-- Add Member -->
        <button class="primary-btn" :disabled="isArchived" @click="isAddMemberOpen = true">Add Member</button>
        <!-- Search -->
        <button class="icon-btn" title="Search member"><i class="fa-solid fa-magnifying-glass"></i></button>
        <!-- Star -->
        <button class="icon-btn" @click="teamStore.toggleStar()" :class="{ starred: team.isStarred }" title="Star team">
          <i :class="team.isStarred ? 'fa-solid fa-star' : 'fa-regular fa-star'"></i>
        </button>
        <!-- Menu -->
        <div class="dropdown-container">
          <button class="icon-btn" @click="isMenuOpen = !isMenuOpen" title="More options"><i class="fa-solid fa-ellipsis-vertical"></i></button>
          <div class="dropdown-menu" v-if="isMenuOpen">
            <button class="menu-item" :disabled="isArchived"><i class="fa-solid fa-gear"></i> Settings</button>
            <button class="menu-item" @click="toggleArchive">
              <i :class="isArchived ? 'fa-solid fa-box-open' : 'fa-solid fa-box-archive'"></i> {{ isArchived ? 'Restore Team' : 'Archive Team' }}
            </button>
            <div class="menu-divider"></div>
            <button class="menu-item danger" @click="isDeleteConfirmOpen = true; isMenuOpen = false"><i class="fa-solid fa-trash"></i> Delete Team</button>
          </div>
        </div>
      </div>
    </div>

    <!-- Tabs Nav -->
    <div class="tabs-nav">
      <button class="tab-btn" :class="{ active: currentTab === 'overview' }" @click="currentTab = 'overview'">Giới thiệu</button>
      <button class="tab-btn" :class="{ active: currentTab === 'activity' }" @click="currentTab = 'activity'">Hoạt động</button>
      <button class="tab-btn" :class="{ active: currentTab === 'hierarchy' }" @click="currentTab = 'hierarchy'">Phân cấp</button>
      <button class="tab-btn" :class="{ active: currentTab === 'goals' }" @click="currentTab = 'goals'">Mục Tiêu</button>
      <button class="tab-btn" :class="{ active: currentTab === 'projects' }" @click="currentTab = 'projects'">Dự Án</button>
      <button class="tab-btn" :class="{ active: currentTab === 'kudos' }" @click="currentTab = 'kudos'">Khen ngợi</button>
    </div>

    <!-- Tab Content -->
    <div class="tab-content" :class="{ 'read-only-state': isArchived }">
      <!-- Read Only Banner -->
      <div v-if="isArchived" class="archived-banner">
        This team is archived. It is read-only.
      </div>

      <div class="team-layout">
        <!-- Main Content (Left) -->
        <div class="main-content">

      <!-- Overview -->
      <div v-if="currentTab === 'overview'" class="tab-pane">
        <section class="info-section">
          <h3>Việc chúng tôi đang thực hiện</h3>
          <div class="bio-container" v-if="!isEditingBio" @click="startEditingBio" style="cursor: pointer; padding: 12px; border: 1px solid transparent; border-radius: 3px; min-height: 60px; transition: background 0.2s, border 0.2s;">
            <p class="description-text" v-if="team.description" style="white-space: pre-wrap;">{{ team.description }}</p>
            <p class="description-text" style="color: #6b778c;" v-else>Chia sẻ những gì nhóm bạn đang thực hiện</p>
          </div>
          <div class="bio-editor" v-else>
            <textarea v-model="tempBio" class="bio-textarea" placeholder="Chia sẻ những gì nhóm bạn đang thực hiện" style="width: 100%; min-height: 80px; padding: 8px; border: 2px solid #4c9aff; border-radius: 3px; resize: vertical; outline: none;"></textarea>
            <div class="bio-actions" style="display: flex; justify-content: flex-end; gap: 6px; margin-top: 8px;">
              <button class="icon-btn small" style="background-color: #ffffff; box-shadow: 0 1px 3px rgba(0,0,0,0.2); border-radius: 3px; width: 32px; height: 32px; color: #172B4D;" @click="saveBio"><i class="fa-solid fa-check"></i></button>
              <button class="icon-btn small" style="background-color: #ffffff; box-shadow: 0 1px 3px rgba(0,0,0,0.2); border-radius: 3px; width: 32px; height: 32px; color: #172B4D;" @click="cancelBio"><i class="fa-solid fa-xmark"></i></button>
            </div>
          </div>
        </section>
        
        <section class="info-section">
          <div class="section-header-row">
            <h3>Members ({{ members.length }})</h3>
            <button class="secondary-btn small" :disabled="isArchived" @click="isAddMemberOpen = true">Add</button>
          </div>
          <div class="member-list">
            <div class="member-item" v-for="member in members" :key="member.id">
              <UserAvatar :user="{ ...member, fullName: member.fullName || member.name, avatarColor: getAvatarColor(member.email || member.id) }" :size="32" :fontSize="14" />
              <div class="member-info">
                <span class="member-name">{{ member.fullName || member.name }}</span>
                <span class="member-role">{{ member.role || 'Thành viên' }}</span>
              </div>
            </div>
          </div>
        </section>
      </div>

      <!-- Activity -->
      <div v-if="currentTab === 'activity'" class="tab-pane">
        <section class="info-section">
          <h3 style="margin-bottom: 24px;">Công việc của {{ team.name }}</h3>
          
          <div class="activity-list" style="display: flex; flex-direction: column; gap: 16px; margin-bottom: 32px;" v-if="teamTasks && teamTasks.length > 0">
            <div class="activity-item" v-for="task in teamTasks" :key="task.id" style="display: flex; align-items: center; justify-content: space-between; padding-bottom: 16px; border-bottom: 1px solid #DFE1E6;">
              <div style="display: flex; align-items: center; gap: 16px;">
                 <div style="color: #0052CC; font-size: 16px;"><i class="fa-solid fa-square-check"></i></div>
                 <div>
                   <div style="font-size: 14px; color: #172B4D; font-weight: 500;">{{ task.name }}</div>
                   <div style="font-size: 12px; color: #6B778C;">{{ task.projectKey }} • {{ task.projectName }}</div>
                 </div>
              </div>
              <div style="display: flex; align-items: center; gap: 24px;">
                 <span style="font-size: 12px; color: #6B778C;">{{ new Date(task.createdAt || Date.now()).toLocaleDateString() }}</span>
                 <UserAvatar v-if="task.assignee" :user="{ ...task.assignee, fullName: task.assignee.name, avatarColor: getAvatarColor(task.assignee.email || task.assignee.id) }" :size="24" :fontSize="10" />
                 <div class="member-avatar-micro" v-else style="background-color: #DFE1E6; color: #6B778C;"><i class="fa-solid fa-user"></i></div>
              </div>
            </div>
          </div>
          <div v-else style="color: #6B778C; font-size: 13px; margin-bottom: 32px;">Không có công việc nào gần đây từ các dự án được liên kết.</div>

          <div class="jira-empty-box" style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 24px; display: flex; align-items: center; gap: 24px;">
            <div style="width: 80px; height: 60px; background-color: #F4F5F7; display: flex; align-items: center; justify-content: center; border-radius: 4px;">
               <i class="fa-brands fa-jira" style="font-size: 32px; color: #0052cc;"></i>
            </div>
            <div>
              <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 4px; font-weight: 600;">Hạng mục công việc Jira được chỉ định cho đội ngũ</h4>
              <p style="font-size: 13px; color: #6B778C;">Công việc được liên kết với đội ngũ của bạn trong Jira sẽ xuất hiện tại đây.</p>
            </div>
          </div>
        </section>
      </div>

      <!-- Hierarchy -->
      <div v-if="currentTab === 'hierarchy'" class="tab-pane" @click="closeHierarchyDropdowns">
        <div class="hierarchy-tree-container" style="display: flex; flex-direction: column; align-items: center; padding: 40px 0;">
          
          <div style="text-align: center; margin-bottom: 32px;">
            <i class="fa-solid fa-sitemap" style="font-size: 24px; color: #6B778C; margin-bottom: 12px;"></i>
            <h3 style="font-size: 16px; color: #172B4D;">Visualize your team's reporting structure</h3>
            <p style="font-size: 13px; color: #6B778C;">Add a parent team and sub-teams to see where your team sits in the organization.</p>
          </div>

          <div class="tree-level parent-level" style="display: flex; flex-direction: column; align-items: center; position: relative;">
            <div class="hierarchy-card-box" v-if="hierarchy?.parent" style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 8px 16px; display: flex; align-items: center; gap: 8px; background: white; min-width: 240px; box-shadow: 0 1px 2px rgba(0,0,0,0.05); cursor: pointer;" @click.stop="openParentDropdown">
               <div class="member-avatar-micro" style="background-color: #FF5630; color: white;">{{ hierarchy.parent.name.substring(0,2).toUpperCase() }}</div>
               <div style="display: flex; flex-direction: column;">
                 <span style="font-size: 13px; font-weight: 500; color: #172B4D;">{{ hierarchy.parent.name }}</span>
                 <span style="font-size: 11px; color: #6B778C;">Đội ngũ chính thức <i class="fa-solid fa-circle-check text-primary"></i> • 1 members</span>
               </div>
            </div>
            <div class="add-node-box" v-else @click.stop="openParentDropdown" style="border: 1px dashed #DFE1E6; border-radius: 3px; padding: 8px 16px; display: flex; align-items: center; gap: 8px; cursor: pointer; color: #6B778C; min-width: 240px; justify-content: center; background-color: #FAFBFC;">
               <i class="fa-solid fa-plus"></i> <span style="font-size: 13px;">Add parent team</span>
            </div>

            <!-- Parent Dropdown Menu -->
            <div class="dropdown-menu search-dropdown" v-if="isParentDropdownOpen" @click.stop style="position: absolute; top: 50px; z-index: 100; width: 300px; padding: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border-radius: 3px; border: 1px solid #DFE1E6; background: white;">
               <input type="text" v-model="teamSearch" placeholder="Tìm kiếm đội ngũ" class="search-input" style="width: 100%; margin-bottom: 8px; padding-left: 12px !important;" />
               <div class="team-list-options" style="max-height: 200px; overflow-y: auto;">
                 <div class="team-option" v-for="t in filteredTeams" :key="t.id" @click="setParentTeam(t)" style="display: flex; align-items: center; gap: 8px; padding: 8px; cursor: pointer; border-radius: 3px;">
                   <div class="member-avatar-micro" style="background-color: #FF5630; color: white;">{{ t.name.substring(0,2).toUpperCase() }}</div>
                   <div style="display: flex; flex-direction: column;">
                     <span style="font-size: 13px; font-weight: 500; color: #172B4D;">{{ t.name }}</span>
                     <span style="font-size: 11px; color: #6B778C;">1 member, including you</span>
                   </div>
                 </div>
               </div>
            </div>

            <div class="tree-line-vertical" style="width: 1px; height: 32px; background-color: #DFE1E6;"></div>
          </div>

          <div class="tree-level current-level" style="display: flex; flex-direction: column; align-items: center; position: relative;">
            <div class="hierarchy-card-box current" style="border: 2px solid #4C9AFF; border-radius: 3px; padding: 8px 16px; display: flex; align-items: center; gap: 8px; background: white; min-width: 240px; box-shadow: 0 1px 3px rgba(9,30,66,0.1);">
               <div class="member-avatar-micro" style="background-color: #6554C0; color: white;">{{ team.avatarText }}</div>
               <div style="display: flex; flex-direction: column;">
                 <span style="font-size: 13px; font-weight: 600; color: #172B4D;">{{ team.name }}</span>
                 <span style="font-size: 11px; color: #6B778C;">Đội ngũ chính thức <i class="fa-solid fa-circle-check text-primary"></i> • {{ members.length }} members</span>
               </div>
               <div style="margin-left: auto; background-color: #DEEBFF; color: #0052CC; font-size: 11px; font-weight: 600; padding: 2px 6px; border-radius: 3px;">Bạn</div>
            </div>
            
            <div class="tree-line-vertical" style="width: 1px; height: 32px; background-color: #DFE1E6;"></div>
          </div>

          <div class="tree-level children-level" style="display: flex; flex-direction: column; align-items: center; position: relative; width: 100%;">
            <div class="child-nodes-wrapper" style="display: grid; grid-template-columns: repeat(2, 1fr); gap: 24px; justify-content: center; width: auto; max-width: 600px; margin: 0 auto; position: relative;">
              <div style="position: absolute; top: -24px; left: 25%; right: 25%; height: 1px; background-color: #DFE1E6; z-index: 0;" v-if="hierarchy.children && hierarchy.children.length > 1"></div>
              
              <div class="tree-node child-node" v-for="(child, index) in hierarchy.children" :key="child.id" style="position: relative; padding: 0; display: flex; flex-direction: column; align-items: center; z-index: 1;">
                <div class="tree-line-vertical-up" style="width: 1px; height: 24px; background-color: #DFE1E6; position: absolute; top: -24px;"></div>
                
                <div class="hierarchy-card-box" style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 8px 16px; display: flex; align-items: center; gap: 8px; background: white; min-width: 220px; box-shadow: 0 1px 2px rgba(0,0,0,0.05); position: relative;">
                  <div class="member-avatar-micro" style="background-color: #36B37E; color: white;">{{ child.name.substring(0,2).toUpperCase() }}</div>
                  <div style="display: flex; flex-direction: column; flex: 1;">
                    <span style="font-size: 13px; font-weight: 500; color: #172B4D;">{{ child.name }}</span>
                    <span style="font-size: 11px; color: #6B778C;">Đội ngũ chính thức <i class="fa-solid fa-circle-check text-primary"></i> • 1 members</span>
                  </div>
                  <button class="icon-btn micro" style="position: absolute; right: 8px; color: #6B778C;" @click.stop="removeChildTeam(child.id)"><i class="fa-solid fa-xmark"></i></button>
                </div>
              </div>

              <div class="tree-node add-node" style="position: relative; padding: 0; display: flex; flex-direction: column; align-items: center; z-index: 1;">
                <div class="tree-line-vertical-up" style="width: 1px; height: 24px; background-color: #DFE1E6; position: absolute; top: -24px;" v-if="hierarchy.children?.length === 0"></div>
                
                <div class="add-node-box" @click.stop="openChildDropdown" style="border: 1px dashed #DFE1E6; border-radius: 3px; padding: 8px 16px; display: flex; align-items: center; gap: 8px; cursor: pointer; color: #6B778C; min-width: 200px; justify-content: center; background-color: #FAFBFC;">
                  <i class="fa-solid fa-plus"></i> <span style="font-size: 13px;">Add sub-teams</span>
                </div>

                <!-- Child Dropdown Menu -->
                <div class="dropdown-menu search-dropdown" v-if="isChildDropdownOpen" @click.stop style="position: absolute; top: 60px; z-index: 100; width: 300px; padding: 8px; text-align: left; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border-radius: 3px; border: 1px solid #DFE1E6; background: white;">
                   <input type="text" v-model="teamSearch" placeholder="Tìm kiếm đội ngũ" class="search-input" style="width: 100%; margin-bottom: 8px; padding-left: 12px !important;" />
                   <div class="team-list-options" style="max-height: 200px; overflow-y: auto;">
                     <div class="team-option" v-for="t in filteredTeams" :key="t.id" @click="addChildTeam(t)" style="display: flex; align-items: center; gap: 8px; padding: 8px; cursor: pointer; border-radius: 3px;">
                       <div class="member-avatar-micro" style="background-color: #FFAB00; color: white;">{{ t.name.substring(0,2).toUpperCase() }}</div>
                       <div style="display: flex; flex-direction: column;">
                         <span style="font-size: 13px; font-weight: 500; color: #172B4D;">{{ t.name }}</span>
                         <span style="font-size: 11px; color: #6B778C;">1 member, including you</span>
                       </div>
                     </div>
                   </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Goals -->
      <div v-if="currentTab === 'goals'" class="tab-pane" @click="isGoalDropdownOpen = false">
        
        <!-- Empty State -->
        <div v-if="!goals || goals.length === 0" style="padding-top: 24px;">
           <h3 style="font-size: 16px; color: #172B4D; margin-bottom: 16px;">Đang đóng góp cho</h3>
           <div style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 24px; display: flex; align-items: flex-start; gap: 24px;">
              <div style="position: relative;">
                 <div style="width: 80px; height: 80px; background-color: #EBECF0; border-radius: 8px; display: flex; align-items: center; justify-content: center; transform: rotate(-5deg);">
                    <i class="fa-solid fa-bullseye" style="font-size: 32px; color: #172B4D;"></i>
                 </div>
                 <div style="position: absolute; bottom: -8px; right: -8px; width: 32px; height: 32px; background-color: #0052CC; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; cursor: pointer; box-shadow: 0 2px 4px rgba(0,0,0,0.1);" @click.stop="isGoalDropdownOpen = !isGoalDropdownOpen">
                    <i class="fa-solid fa-plus" style="font-size: 16px;"></i>
                 </div>
              </div>
              <div style="flex: 1; position: relative;">
                 <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Xem các mục tiêu mà đội ngũ của bạn đang hướng tới</h4>
                 <p style="font-size: 13px; color: #6B778C; margin-bottom: 16px; line-height: 1.5;">Mục tiêu giúp đội ngũ của bạn kết nối công việc với những kết quả mà họ đóng góp, đồng thời cung cấp một nơi duy nhất để chia sẻ tiến độ thực hiện mục tiêu. Tạo mục tiêu để mọi người hiểu rõ định hướng và ưu tiên của nhóm.</p>
                 <div style="position: relative; display: inline-block;">
                   <button class="secondary-btn" @click.stop="isGoalDropdownOpen = !isGoalDropdownOpen">Thêm mục tiêu</button>
                   <!-- Goal Dropdown Menu -->
                   <div class="dropdown-menu search-dropdown" v-if="isGoalDropdownOpen" @click.stop style="position: absolute; top: 100%; left: 0; margin-top: 4px; z-index: 100; width: 300px; padding: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border-radius: 3px; border: 1px solid #DFE1E6; background: white;">
                      <input type="text" v-model="goalSearch" placeholder="Tìm kiếm mục tiêu hoặc dán liên kết" class="search-input" style="width: 100%; margin-bottom: 12px; padding-left: 12px !important;" />
                      <h5 style="font-size: 11px; color: #6B778C; text-transform: uppercase; padding: 0 8px 8px;">Mục tiêu gần đây</h5>
                      <div class="goal-list-options" style="max-height: 200px; overflow-y: auto;">
                        <div class="team-option" v-for="g in siteGoals" :key="g.id" @click="linkGoal(g)" style="display: flex; align-items: center; gap: 8px; padding: 8px; cursor: pointer; border-radius: 3px;">
                          <i class="fa-solid fa-bullseye" style="color: #6B778C; font-size: 14px;"></i>
                          <div style="display: flex; flex-direction: column;">
                            <span style="font-size: 13px; color: #172B4D;">{{ g.title }}</span>
                            <span style="font-size: 11px; color: #6B778C;">{{ g.owner }}</span>
                          </div>
                        </div>
                        <div v-if="!siteGoals || siteGoals.length === 0" style="padding: 8px; font-size: 12px; color: #6B778C;">Không có mục tiêu nào</div>
                      </div>
                      <div style="border-top: 1px solid #DFE1E6; margin-top: 8px; padding-top: 8px;">
                         <div class="team-option" style="display: flex; align-items: center; gap: 8px; padding: 8px; cursor: pointer; color: #172B4D;" @click="isCreateGoalOpen = true">
                            <i class="fa-solid fa-plus"></i> <span style="font-size: 13px;">Tạo mục tiêu</span>
                         </div>
                      </div>
                   </div>
                 </div>
              </div>
           </div>
        </div>

        <!-- Populated State -->
        <div v-else style="padding-top: 24px;">
           <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px;">
             <h3 style="font-size: 16px; color: #172B4D;">Hiện đóng góp cho</h3>
             <div style="display: flex; gap: 8px;">
               <button class="secondary-btn" style="height: 32px;">Theo dõi</button>
               <div style="position: relative;">
                 <button class="secondary-btn icon-only" style="height: 32px; width: 32px;" @click.stop="isGoalDropdownOpen = !isGoalDropdownOpen"><i class="fa-solid fa-plus"></i></button>
                 <!-- Goal Dropdown Menu -->
                 <div class="dropdown-menu search-dropdown" v-if="isGoalDropdownOpen" @click.stop style="position: absolute; top: 40px; right: 0; z-index: 100; width: 300px; padding: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border-radius: 3px; border: 1px solid #DFE1E6; background: white;">
                    <input type="text" v-model="goalSearch" placeholder="Tìm kiếm mục tiêu hoặc dán liên kết" class="search-input" style="width: 100%; margin-bottom: 12px; padding-left: 12px !important;" />
                    <h5 style="font-size: 11px; color: #6B778C; text-transform: uppercase; padding: 0 8px 8px;">Mục tiêu gần đây</h5>
                    <div class="goal-list-options" style="max-height: 200px; overflow-y: auto;">
                      <div class="team-option" v-for="g in siteGoals" :key="g.id" @click="linkGoal(g)" style="display: flex; align-items: center; gap: 8px; padding: 8px; cursor: pointer; border-radius: 3px;">
                        <i class="fa-solid fa-bullseye" style="color: #6B778C; font-size: 14px;"></i>
                        <div style="display: flex; flex-direction: column;">
                          <span style="font-size: 13px; color: #172B4D;">{{ g.title }}</span>
                          <span style="font-size: 11px; color: #6B778C;">{{ g.owner }}</span>
                        </div>
                      </div>
                    </div>
                    <div style="border-top: 1px solid #DFE1E6; margin-top: 8px; padding-top: 8px;">
                       <div class="team-option" style="display: flex; align-items: center; gap: 8px; padding: 8px; cursor: pointer; color: #172B4D;" @click="isCreateGoalOpen = true">
                          <i class="fa-solid fa-plus"></i> <span style="font-size: 13px;">Tạo mục tiêu</span>
                       </div>
                    </div>
                 </div>
               </div>
             </div>
           </div>
           
           <div style="display: flex; gap: 16px; margin-bottom: 32px; flex-wrap: wrap;">
             <div class="goal-card" v-for="goal in goals" :key="goal.id" style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 16px; width: 320px; cursor: pointer; transition: background 0.2s, box-shadow 0.2s;">
               <div style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 12px;">
                 <div style="width: 24px; height: 24px; background-color: #EBECF0; border-radius: 4px; display: flex; align-items: center; justify-content: center;">
                   <i class="fa-solid fa-bullseye" style="color: #6B778C; font-size: 14px;"></i>
                 </div>
                 <span class="status-badge" style="background-color: #DFE1E6; color: #42526E; font-size: 11px; font-weight: bold; text-transform: uppercase; padding: 2px 6px;">{{ goal.status || 'Đã hoàn tất 🚀' }}</span>
               </div>
               <div style="font-size: 14px; color: #172B4D; font-weight: 500; margin-bottom: 4px;">{{ goal.title }}</div>
               <div style="font-size: 12px; color: #6B778C;">Thuộc sở hữu của {{ goal.owner }}</div>
             </div>
           </div>

           <h3 style="font-size: 16px; color: #172B4D; margin-bottom: 16px;">Đã hoàn tất</h3>
           <div style="background-color: #FAFBFC; border: 1px solid #DFE1E6; border-radius: 3px; padding: 12px; text-align: center; color: #6B778C; font-size: 13px;">
             Chưa có mục tiêu nào hoàn thành
           </div>
        </div>
      </div>

      <!-- Projects -->
      <div v-if="currentTab === 'projects'" class="tab-pane">
        <div v-if="!projects || projects.length === 0" style="padding-top: 24px;">
           <h3 style="font-size: 16px; color: #172B4D; margin-bottom: 16px;">Đang đóng góp cho</h3>
           <div style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 24px; display: flex; align-items: flex-start; gap: 24px;">
              <div style="position: relative;">
                 <div style="width: 80px; height: 80px; background-color: #EBECF0; border-radius: 8px; display: flex; align-items: center; justify-content: center; transform: rotate(-5deg);">
                    <i class="fa-solid fa-rocket" style="font-size: 32px; color: #172B4D;"></i>
                 </div>
                 <div style="position: absolute; bottom: -8px; right: -8px; width: 32px; height: 32px; background-color: #0052CC; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; cursor: pointer; box-shadow: 0 2px 4px rgba(0,0,0,0.1);" @click="goToProjects">
                    <i class="fa-solid fa-plus" style="font-size: 16px;"></i>
                 </div>
              </div>
              <div style="flex: 1; position: relative;">
                 <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Chỉ định cho đội ngũ của bạn các dự án mà họ đang thực hiện</h4>
                 <p style="font-size: 13px; color: #6B778C; margin-bottom: 16px; line-height: 1.5;">Mục Dự án có thể giúp đội ngũ của bạn chia sẻ các bản cập nhật trạng thái hàng tuần với các bên liên quan trong tổ chức của bạn. Thêm đội ngũ của bạn vào các dự án phù hợp để xem tất cả dự án được liệt kê ở đây.</p>
                 <div style="position: relative; display: inline-block;">
                   <button class="secondary-btn" @click.stop="isProjectDropdownOpen = !isProjectDropdownOpen">Thêm dự án</button>
                   <!-- Project Dropdown Menu -->
                   <div class="dropdown-menu search-dropdown" v-if="isProjectDropdownOpen" @click.stop style="position: absolute; top: 100%; left: 0; margin-top: 4px; z-index: 100; width: 300px; padding: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border-radius: 3px; border: 1px solid #DFE1E6; background: white;">
                      <input type="text" v-model="projectSearch" placeholder="Tìm kiếm dự án" class="search-input" style="width: 100%; margin-bottom: 12px; padding-left: 12px !important;" />
                      <h5 style="font-size: 11px; color: #6B778C; text-transform: uppercase; padding: 0 8px 8px;">Dự án gần đây</h5>
                      <div class="goal-list-options" style="max-height: 200px; overflow-y: auto;">
                        <div class="team-option" v-for="p in siteProjects" :key="p.id" @click="linkProject(p)" style="display: flex; align-items: center; gap: 8px; padding: 8px; cursor: pointer; border-radius: 3px;">
                          <i class="fa-solid fa-rocket" style="color: #6B778C; font-size: 14px;"></i>
                          <div style="display: flex; flex-direction: column;">
                            <span style="font-size: 13px; color: #172B4D;">{{ p.name }}</span>
                            <span style="font-size: 11px; color: #6B778C;">{{ p.key }}</span>
                          </div>
                        </div>
                        <div v-if="!siteProjects || siteProjects.length === 0" style="padding: 8px; font-size: 12px; color: #6B778C;">Không có dự án nào</div>
                      </div>
                      <div style="border-top: 1px solid #DFE1E6; margin-top: 8px; padding-top: 8px;">
                         <div class="team-option" style="display: flex; align-items: center; gap: 8px; padding: 8px; cursor: pointer; color: #172B4D;" @click="goToProjects">
                            <i class="fa-solid fa-plus"></i> <span style="font-size: 13px;">Tạo dự án</span>
                         </div>
                      </div>
                   </div>
                 </div>
              </div>
           </div>
        </div>
        <div v-else>
           <h3 style="font-size: 16px; color: #172B4D; margin-bottom: 16px;">Các dự án liên quan</h3>
           <div style="display: flex; gap: 16px; margin-bottom: 32px; flex-wrap: wrap;">
             <div class="goal-card" v-for="proj in projects" :key="proj.id" style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 16px; width: 320px; cursor: pointer;" @click="goToProjectDetail(proj.id)">
                <div style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 12px;">
                  <div style="width: 24px; height: 24px; background-color: #0052CC; color: white; border-radius: 4px; display: flex; align-items: center; justify-content: center;">
                    <i class="fa-solid fa-rocket" style="font-size: 12px;"></i>
                  </div>
                  <span class="status-badge" style="background-color: #DFE1E6; color: #42526E; font-size: 11px; font-weight: bold; text-transform: uppercase; padding: 2px 6px;">{{ proj.status || 'Đang thực hiện' }}</span>
                </div>
                <div style="font-size: 14px; color: #172B4D; font-weight: 500; margin-bottom: 4px;">{{ proj.name }}</div>
             </div>
           </div>
        </div>
      </div>

      <!-- Kudos -->
      <div v-if="currentTab === 'kudos'" class="tab-pane">
        <h3 style="font-size: 16px; color: #172B4D; margin-bottom: 16px;">Khen ngợi</h3>
        <div style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 32px; display: flex; align-items: center; justify-content: center; min-height: 200px;">
           <div style="display: flex; align-items: flex-start; gap: 24px; max-width: 600px;">
             <div style="font-size: 48px;">
               <div style="width: 80px; height: 80px; background-color: #FFFAE6; border-radius: 50%; display: flex; align-items: center; justify-content: center; position: relative;">
                  <i class="fa-solid fa-medal" style="color: #FFAB00; font-size: 40px;"></i>
               </div>
             </div>
             <div style="flex: 1;">
               <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Lời khen của nhóm bạn sẽ xuất hiện tại đây</h4>
               <p style="font-size: 13px; color: #6B778C; margin-bottom: 16px; line-height: 1.5;">Khen ngợi là một cách bày tỏ lời cảm ơn và chúc mừng đồng nghiệp khi họ có thành tích vượt trội.</p>
               <button class="secondary-btn" @click="isGiveKudosOpen = true" style="display: flex; align-items: center; gap: 8px;">
                 <i class="fa-regular fa-heart"></i> Give kudos
               </button>
             </div>
           </div>
        </div>
        
        <div class="kudos-grid" v-if="kudos.length > 0" style="margin-top: 32px;">
          <div class="kudos-card" v-for="k in kudos" :key="k.id">
            <div class="kudos-icon">{{ k.icon }}</div>
            <div class="kudos-content">
              <p class="kudos-msg" v-html="k.message"></p>
              <span class="kudos-sender">- {{ k.sender }}</span>
            </div>
            <div class="kudos-reactions">
              <button class="reaction-btn"><i class="fa-solid fa-heart" style="color: #ff5630;"></i> 1</button>
            </div>
          </div>
        </div>
      </div>
        </div>
        
        <!-- Right Sidebar -->
        <div class="right-sidebar">
      <div class="sidebar-section">
        <h3>Liên kết đội ngũ <span class="badge" style="background-color: #DFE1E6; color: #172B4D; padding: 2px 6px; border-radius: 12px; font-size: 11px;">0</span></h3>
        <div style="display: flex; justify-content: flex-end; margin-top: -28px; margin-bottom: 16px;">
           <button class="icon-btn small" title="Add Link" style="width: 24px; height: 24px;"><i class="fa-solid fa-plus"></i></button>
        </div>
        <div class="link-items" style="display: flex; flex-direction: column; gap: 16px;">
          <div class="link-item" style="display: flex; align-items: center; gap: 12px; color: #6B778C; cursor: pointer; position: relative;" @click="isSprintAProjectOpen = !isSprintAProjectOpen">
            <div style="width: 24px; height: 24px; border-radius: 4px; background-color: #0052CC; color: white; display: flex; align-items: center; justify-content: center;"><i class="fa-solid fa-rocket" style="font-size: 14px;"></i></div>
            <span style="font-size: 14px;">Thêm dự án SprintA</span>
            <!-- SprintA Dropdown Menu -->
            <div class="dropdown-menu search-dropdown" v-if="isSprintAProjectOpen" @click.stop style="position: absolute; top: 100%; right: 0; margin-top: 4px; z-index: 100; width: 250px; padding: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border-radius: 3px; border: 1px solid #DFE1E6; background: white; max-height: 200px; overflow-y: auto;">
              <div style="padding: 4px 8px; font-size: 11px; font-weight: bold; color: #6B778C; text-transform: uppercase;">Dự án trong Space</div>
              <div class="team-option" v-for="sp in siteProjects" :key="sp.id" @click="linkProject(sp)" style="display: flex; align-items: center; gap: 8px; padding: 8px; cursor: pointer; border-radius: 3px;">
                 <i class="fa-solid fa-rocket" style="color: #6B778C; font-size: 14px;"></i>
                 <span style="font-size: 13px; color: #172B4D;">{{ sp.name }}</span>
              </div>
              <div v-if="!siteProjects || siteProjects.length === 0" style="padding: 8px; font-size: 12px; color: #6B778C;">Không có dự án nào</div>
            </div>
          </div>
          <div class="link-item" style="display: flex; align-items: center; gap: 12px; color: #6B778C; cursor: pointer; position: relative;" @click="isSpaceDropdownOpen = !isSpaceDropdownOpen">
            <div style="width: 24px; height: 24px; border-radius: 4px; background-color: #0052CC; color: white; display: flex; align-items: center; justify-content: center;"><i class="fa-brands fa-confluence" style="font-size: 14px;"></i></div>
            <span style="font-size: 14px;">Thêm không gian</span>
            <!-- Space Dropdown Menu -->
            <div class="dropdown-menu search-dropdown" v-if="isSpaceDropdownOpen" @click.stop style="position: absolute; top: 100%; right: 0; margin-top: 4px; z-index: 100; width: 250px; padding: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border-radius: 3px; border: 1px solid #DFE1E6; background: white; max-height: 200px; overflow-y: auto;">
              <div class="team-option" v-for="space in sites" :key="space.id" style="display: flex; align-items: center; gap: 8px; padding: 8px; cursor: pointer; border-radius: 3px;">
                 <div style="width: 20px; height: 20px; background: #0052CC; color: white; border-radius: 3px; display: flex; align-items: center; justify-content: center; font-size: 10px;">{{ space.name.substring(0,1).toUpperCase() }}</div>
                 <span style="font-size: 13px; color: #172B4D;">{{ space.name }}</span>
              </div>
            </div>
          </div>
          <div class="link-item" style="display: flex; align-items: center; gap: 12px; color: #6B778C; cursor: pointer;">
            <div style="width: 24px; height: 24px; border-radius: 50%; background-color: #F4F5F7; color: #6B778C; display: flex; align-items: center; justify-content: center;"><i class="fa-solid fa-link" style="font-size: 12px;"></i></div>
            <span style="font-size: 14px;">Thêm liên kết</span>
          </div>
        </div>
      </div>

      <div class="sidebar-section mt-32" style="margin-top: 32px;">
        <h3 style="margin-bottom: 20px;">Chi tiết</h3>
        
        <div class="meta-item-row" style="display: flex; align-items: center; margin-bottom: 16px;">
           <span style="width: 120px; color: #6B778C; font-size: 13px;">Đội ngũ gốc</span>
           <div class="hierarchy-card mini" v-if="hierarchy?.parent" style="flex: 1; margin: 0; padding: 4px 8px; border-radius: 3px; border: 1px solid #DFE1E6;">
             <div class="team-identity-small" style="font-size: 13px; display: flex; align-items: center; gap: 8px;">
               <div class="member-avatar-micro" style="width: 16px; height: 16px; font-size: 10px; background-color: #FFAB00; color: #172B4D;">{{ hierarchy.parent.name.substring(0,2).toUpperCase() }}</div>
               <span>{{ hierarchy.parent.name }}</span>
               <i class="fa-solid fa-circle-check" style="color: #0052cc;"></i>
             </div>
           </div>
           <span v-else style="flex: 1; font-size: 13px; color: #172B4D;">Không có đội ngũ gốc</span>
        </div>

        <div class="meta-item-row" style="display: flex; align-items: flex-start; margin-bottom: 16px;">
           <span style="width: 120px; color: #6B778C; font-size: 13px;">Đội ngũ phụ</span>
           <div class="hierarchy-list" v-if="hierarchy?.children?.length" style="flex: 1; display: flex; flex-direction: column; gap: 8px;">
             <div class="hierarchy-card mini" v-for="child in hierarchy.children" :key="child.id" style="margin: 0; padding: 4px 8px; border-radius: 3px; border: 1px solid #DFE1E6;">
               <div class="team-identity-small" style="font-size: 13px; display: flex; align-items: center; gap: 8px;">
                 <div class="member-avatar-micro" style="width: 16px; height: 16px; font-size: 10px;">{{ child.name.substring(0,2).toUpperCase() }}</div>
                 <span>{{ child.name }}</span>
               </div>
             </div>
           </div>
           <span v-else style="flex: 1; font-size: 13px; color: #172B4D;">Không có đội ngũ phụ</span>
        </div>

        <div class="meta-item-row" style="display: flex; align-items: center; margin-bottom: 16px;">
           <span style="width: 120px; color: #6B778C; font-size: 13px;">Loại đội ngũ</span>
           <span style="flex: 1; font-size: 13px; color: #172B4D; font-weight: 500;">Đội ngũ chính thức <i class="fa-solid fa-circle-check" style="color: #0052cc;"></i></span>
        </div>

        <div class="meta-item-row" style="display: flex; align-items: center; position: relative;">
           <span style="width: 120px; color: #6B778C; font-size: 13px;">Người quản lý</span>
           <div style="flex: 1; position: relative;">
              <div class="add-node-box" @click="isManagerDropdownOpen = !isManagerDropdownOpen" style="border: 1px dashed #DFE1E6; border-radius: 3px; padding: 4px 8px; display: inline-flex; align-items: center; gap: 8px; cursor: pointer; color: #172B4D; font-size: 13px; background-color: #FAFBFC;">
                 <UserAvatar v-if="team.manager" :user="{ ...team.manager, fullName: team.manager.name, avatarColor: getAvatarColor(team.manager.email || team.manager.id) }" :size="16" :fontSize="8" />
                 <i class="fa-solid fa-user-plus" v-else></i>
                 <span>{{ team.manager ? team.manager.name : 'Chọn người quản lý' }}</span>
              </div>
              <!-- Dropdown Menu -->
              <div class="dropdown-menu" v-if="isManagerDropdownOpen" @click.stop style="position: absolute; top: 100%; left: 0; margin-top: 4px; z-index: 100; width: 250px; padding: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border-radius: 3px; border: 1px solid #DFE1E6; background: white; max-height: 200px; overflow-y: auto;">
                <div class="team-option" v-for="m in members" :key="m.id" @click="selectManager(m)" style="display: flex; align-items: center; gap: 8px; padding: 8px; cursor: pointer; border-radius: 3px;">
                  <UserAvatar :user="{ ...m, fullName: m.fullName || m.name, avatarColor: getAvatarColor(m.email || m.id) }" :size="20" :fontSize="10" />
                  <span style="font-size: 13px; color: #172B4D;">{{ m.fullName || m.name }}</span>
                </div>
              </div>
           </div>
        </div>

      </div>
    </div>
  </div>
  <!-- End of Layout Wrapper -->
  </div>
    <Teleport to="body">
    <!-- Modals -->
    <!-- Add Member Modal -->
    <div class="modal-overlay sa-data-modal-overlay" v-if="isAddMemberOpen" @click.self="isAddMemberOpen = false">
      <div class="modal-content">
        <DataModalHeader
          icon="bi bi-person-plus"
          :title="`Add Members to ${team.name}`"
          description="Search and add members to this team"
          @close="isAddMemberOpen = false"
        />
        <div class="modal-body">
          <DataModalSection
            icon="bi bi-search"
            title="Find members"
            description="You can add up to 50 members at once"
          >
            <div class="search-box">
              <i class="fa-solid fa-magnifying-glass search-icon" style="z-index: 1;"></i>
              <input type="text" placeholder="Search by name or email..." v-model="memberSearch" @focus="isMemberDropdownOpen = true" style="padding-left: 44px; position: relative; z-index: 0;" />
            </div>

            <div class="selected-tags" v-if="selectedMembers.length > 0">
              <div class="tag-chip" v-for="id in selectedMembers" :key="id">
                 {{ getSelectedUserName(id) }}
                 <i class="fa-solid fa-xmark remove-tag" @click="toggleSelectMember(id)"></i>
              </div>
            </div>

            <div class="member-select-list mt-16" v-if="isMemberDropdownOpen">
              <div class="empty-state-micro" v-if="filteredUsers.length === 0">
                <span v-if="!memberSearch">Type to search directory...</span>
                <span v-else>No members found matching "{{ memberSearch }}"</span>
              </div>
              <div class="select-item" v-for="user in filteredUsers" :key="user.id" @click="toggleSelectMember(user.id)">
                <UserAvatar :user="{ ...user, fullName: user.fullName || user.name, avatarColor: getAvatarColor(user.email || user.id) }" :size="24" :fontSize="10" />
                <div class="user-details" style="margin-left: 8px;">
                  <span class="user-name">{{ user.fullName || user.name }}</span>
                  <span class="user-email">{{ user.email }}</span>
                </div>
                <i class="fa-solid fa-check check-icon" v-if="selectedMembers.includes(user.id)"></i>
              </div>
            </div>
          </DataModalSection>
        </div>
        <div class="modal-footer">
          <button class="cancel-btn" @click="isAddMemberOpen = false">
            <i class="bi bi-x-lg"></i>
            Cancel
          </button>
          <button class="submit-btn" :disabled="selectedMembers.length === 0" @click="submitAddMember">
            <i class="bi bi-person-plus"></i>
            Add Selected
          </button>
        </div>
      </div>
    </div>

    <!-- Quick Create Goal Modal -->
    <div class="modal-overlay sa-data-modal-overlay" v-if="isCreateGoalOpen" @click.self="isCreateGoalOpen = false">
      <div class="modal-content">
        <DataModalHeader
          icon="bi bi-bullseye"
          title="Quick Create Goal"
          description="Create a team goal without leaving this page"
          @close="isCreateGoalOpen = false"
        />
        <div class="modal-body">
          <DataModalSection icon="bi bi-pencil-square" title="Goal information">
            <DataModalField label="Goal Title">
              <input type="text" placeholder="What do you want to achieve?" v-model="newGoalTitle" />
            </DataModalField>
          </DataModalSection>
        </div>
        <div class="modal-footer">
          <button class="cancel-btn" @click="isCreateGoalOpen = false">
            <i class="bi bi-x-lg"></i>
            Cancel
          </button>
          <button class="submit-btn" :disabled="!newGoalTitle" @click="isCreateGoalOpen = false; newGoalTitle = ''">
            <i class="bi bi-plus-lg"></i>
            Create Goal
          </button>
        </div>
      </div>
    </div>

    <!-- Edit Hierarchy Modal -->
    <div class="modal-overlay sa-data-modal-overlay" v-if="isEditHierarchyOpen" @click.self="isEditHierarchyOpen = false">
      <div class="modal-content">
        <DataModalHeader
          icon="bi bi-diagram-3"
          title="Update Parent Team"
          description="Select a parent team to establish hierarchy"
          @close="isEditHierarchyOpen = false"
        />
        <div class="modal-body">
          <DataModalSection
            icon="bi bi-search"
            title="Find parent team"
            description="A team can only have one parent"
          >
            <DataModalField label="Search Teams">
              <input type="text" placeholder="Type team name..." v-model="teamSearch" />
            </DataModalField>
            <div class="empty-state-micro mt-16">
              <span>Type to search existing teams...</span>
            </div>
          </DataModalSection>
        </div>
        <div class="modal-footer">
          <button class="cancel-btn" @click="isEditHierarchyOpen = false">
            <i class="bi bi-x-lg"></i>
            Cancel
          </button>
          <button class="submit-btn" disabled>
            <i class="bi bi-check-lg"></i>
            Save Changes
          </button>
        </div>
      </div>
    </div>

    <!-- Delete Confirm Modal -->
    <div class="modal-overlay sa-data-modal-overlay" v-if="isDeleteConfirmOpen" @click.self="isDeleteConfirmOpen = false">
      <div class="modal-content danger-modal">
        <DataModalHeader
          icon="bi bi-exclamation-triangle"
          title="Delete Team?"
          description="Confirm before removing this team"
          @close="isDeleteConfirmOpen = false"
        />
        <div class="modal-body">
          <DataModalSection
            icon="bi bi-trash3"
            title="Delete confirmation"
            description="This action cannot be undone"
          >
            <p>Are you sure you want to delete <strong>{{ team.name }}</strong>? This action will remove all hierarchy associations.</p>
          </DataModalSection>
        </div>
        <div class="modal-footer">
          <button class="cancel-btn" @click="isDeleteConfirmOpen = false">
            <i class="bi bi-x-lg"></i>
            Cancel
          </button>
          <button class="submit-btn danger" @click="confirmDelete">
            <i class="bi bi-trash3"></i>
            Delete Team
          </button>
        </div>
      </div>
    </div>
    </Teleport>

    <!-- Give Kudos Full Screen Overlay -->
    <div class="give-kudos-overlay" v-if="isGiveKudosOpen" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: #FFF4F8; z-index: var(--sp-z-modal, 1200); overflow-y: auto; display: flex; flex-direction: column;">
       
       <!-- Content -->
       <div style="flex: 1; display: flex; justify-content: center; padding-top: 40px;" @click="isKudosLinkDropdownOpen = false; isKudosTargetDropdownOpen = false; isKudosEmojiDropdownOpen = false">
          <div style="width: 100%; max-width: 600px; display: flex; flex-direction: column; gap: 24px; position: relative;">
             <!-- Top Actions -->
             <div style="display: flex; justify-content: flex-end; margin-bottom: -16px;">
                <button class="icon-btn" @click="isGiveKudosOpen = false" style="background: transparent; border: none; font-size: 14px; font-weight: 500; cursor: pointer; color: #42526E; display: flex; align-items: center; gap: 8px; padding: 4px 8px; border-radius: 4px; transition: background 0.1s;" onmouseover="this.style.background='#EBECF0'" onmouseout="this.style.background='transparent'">Quay lại <i class="fa-solid fa-arrow-right"></i></button>
             </div>
             <div style="position: relative;">
                 <div style="display: flex; align-items: center; gap: 8px; font-weight: 500; font-size: 14px; color: #0052CC; cursor: pointer; padding: 8px 12px; border: 1px solid #4C9AFF; border-radius: 4px; display: inline-flex;" @click.stop="isKudosTargetDropdownOpen = !isKudosTargetDropdownOpen">
                    <UserAvatar v-if="kudosTargetType === 'user'" :user="kudosTargetData || {}" :size="24" :fontSize="10" />
                     <div v-else class="member-avatar-micro" style="background-color: #36B37E; color: white; width: 24px; height: 24px; border-radius: 4px; display: flex; align-items: center; justify-content: center; font-size: 11px;">{{ kudosTargetAvatar }}</div>
                    Khen ngợi {{ kudosTargetName }}
                 </div>
                 
                 <!-- Target Dropdown -->
                 <div v-if="isKudosTargetDropdownOpen" @click.stop class="dropdown-menu" style="position: absolute; top: 40px; left: 0; z-index: 10; width: 340px; background: white; border-radius: 3px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border: 1px solid #DFE1E6; padding: 8px 0; display: flex; flex-direction: column; max-height: 300px; overflow-y: auto;">
                    <div style="padding: 4px 12px; font-size: 11px; font-weight: 700; color: #5E6C84; text-transform: uppercase;">Mọi người</div>
                    <div v-for="user in kudosTargetUsers" :key="user.id" @click="selectKudosTarget('user', user)" style="display: flex; align-items: center; gap: 8px; padding: 8px 16px; cursor: pointer; transition: background 0.1s;" onmouseover="this.style.background='#FAFBFC'" onmouseout="this.style.background='transparent'">
                       <UserAvatar :user="{ ...user, fullName: user.fullName || user.name, avatarColor: getAvatarColor(user.email || user.id) }" :size="24" :fontSize="10" />
                       <span style="font-size: 14px; color: #172B4D;">{{ user.fullName || user.name }}</span>
                    </div>
                    <div style="padding: 4px 12px; font-size: 11px; font-weight: 700; color: #5E6C84; text-transform: uppercase; margin-top: 8px; border-top: 1px solid #DFE1E6; padding-top: 8px;">Đội ngũ</div>
                    <div v-for="t in teamStore.allTeams" :key="t.id" @click="selectKudosTarget('team', t)" style="display: flex; align-items: center; gap: 8px; padding: 8px 16px; cursor: pointer; transition: background 0.1s; background: #E6FCFF;" onmouseover="this.style.background='#B3F5FF'" onmouseout="this.style.background='#E6FCFF'">
                       <div class="member-avatar-micro" style="background-color: #36B37E; color: white; width: 24px; height: 24px; border-radius: 4px; display: flex; align-items: center; justify-content: center; font-size: 11px;">{{ t.name ? t.name.substring(0, 2).toUpperCase() : 'T' }}</div>
                       <div style="display: flex; flex-direction: column;">
                         <span style="font-size: 14px; color: #0052CC;">{{ t.name }} <i class="fa-solid fa-circle-check" style="font-size: 10px;"></i></span>
                         <span style="font-size: 11px; color: #6B778C;">Đội ngũ chính thức • {{ t.memberCount || 0 }} thành viên, kể cả bạn</span>
                       </div>
                    </div>
                 </div>
             </div>
             
             <!-- Text input that renders HTML or handles link replacement -->
             <div style="position: relative;">
                 <div 
                   ref="kudosEditorRef"
                   class="kudos-editor"
                   contenteditable="true"
                   @input="e => kudosText = e.target.innerHTML"
                   style="width: 100%; min-height: 60px; font-size: 20px; color: #172B4D; outline: none; border: none; background: transparent; line-height: 1.5; padding: 8px 0; font-weight: 400; cursor: text;"
                   :data-placeholder="'Hãy cho ' + kudosTargetName + ' biết lý do bạn gửi lời khen ngợi này'"
                 ></div>
             </div>

             <!-- Icons toolbar -->
             <div style="display: flex; gap: 16px; color: #6B778C; font-size: 18px; align-items: center;">
               <div style="position: relative;">
                 <i class="fa-regular fa-face-smile" style="cursor: pointer;" @click.stop="isKudosEmojiDropdownOpen = !isKudosEmojiDropdownOpen"></i>
                 
                 <!-- Emoji Dropdown -->
                 <div v-if="isKudosEmojiDropdownOpen" @click.stop class="dropdown-menu" style="position: absolute; top: 28px; left: 0; z-index: 10; background: white; border-radius: 3px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border: 1px solid #DFE1E6; padding: 12px; width: 340px; display: flex; flex-direction: column; gap: 8px;">
                    <input type="text" placeholder="Tìm kiếm icon..." v-model="kudosEmojiSearch" style="width: 100%; padding: 6px; border: 1px solid #DFE1E6; border-radius: 3px; outline: none; font-size: 13px;" />
                    <div style="display: grid; grid-template-columns: repeat(8, 1fr); gap: 6px; max-height: 200px; overflow-y: auto;">
                       <div v-for="emoji in filteredKudosEmojis" :key="emoji" @click="insertEmoji(emoji)" style="cursor: pointer; font-size: 20px; text-align: center; padding: 4px; border-radius: 4px; transition: background 0.1s;" onmouseover="this.style.background='#F4F5F7'" onmouseout="this.style.background='transparent'">
                          {{ emoji }}
                       </div>
                    </div>
                 </div>
               </div>
               
               <div style="position: relative;">
                 <i class="fa-solid fa-link" style="cursor: pointer;" @click.stop="isKudosLinkDropdownOpen = !isKudosLinkDropdownOpen"></i>
                 
                 <!-- Link Dropdown -->
                 <div v-if="isKudosLinkDropdownOpen" @click.stop class="dropdown-menu" style="position: absolute; top: 24px; left: 0; z-index: 10; width: 340px; background: white; border-radius: 3px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border: 1px solid #DFE1E6; padding: 12px; display: flex; flex-direction: column; gap: 12px;">
                    <div>
                      <label style="font-size: 11px; font-weight: 600; color: #6B778C;">Tìm kiếm hoặc dán liên kết *</label>
                      <input type="text" placeholder="Tìm các liên kết gần đây hoặc dán một liên kết" style="width: 100%; margin-top: 4px; padding: 8px; border: 2px solid #4C9AFF; border-radius: 3px; outline: none;" v-model="kudosLinkSearch" />
                    </div>
                    <div>
                      <label style="font-size: 11px; font-weight: 600; color: #6B778C;">Văn bản hiển thị (không bắt buộc)</label>
                      <input type="text" placeholder="Văn bản cần hiển thị" style="width: 100%; margin-top: 4px; padding: 8px; border: 1px solid #DFE1E6; border-radius: 3px; outline: none;" v-model="kudosLinkDisplay" />
                      <div style="font-size: 11px; color: #6B778C; margin-top: 4px;">Cung cấp tiêu đề hoặc mô tả cho liên kết này</div>
                    </div>
                    
                    <div style="display: flex; gap: 16px; border-bottom: 1px solid #DFE1E6; padding-bottom: 8px;">
                      <span @click="kudosLinkTab = 'Home'" :style="{ fontSize: '13px', fontWeight: kudosLinkTab === 'Home' ? '600' : '500', color: kudosLinkTab === 'Home' ? '#0052CC' : '#6B778C', borderBottom: kudosLinkTab === 'Home' ? '2px solid #0052CC' : 'none', paddingBottom: '8px', cursor: 'pointer', marginBottom: '-9px' }">Home</span>
                      <span @click="kudosLinkTab = 'SprintA'" :style="{ fontSize: '13px', fontWeight: kudosLinkTab === 'SprintA' ? '600' : '500', color: kudosLinkTab === 'SprintA' ? '#0052CC' : '#6B778C', borderBottom: kudosLinkTab === 'SprintA' ? '2px solid #0052CC' : 'none', paddingBottom: '8px', cursor: 'pointer', marginBottom: '-9px' }">SprintA</span>
                    </div>

                    <div>
                      <h5 style="font-size: 11px; color: #6B778C; text-transform: uppercase; margin-bottom: 8px;">{{ kudosLinkTab === 'Home' ? 'Dự án trên Home' : 'Dự án của đội ngũ' }}</h5>
                      <div style="max-height: 150px; overflow-y: auto; display: flex; flex-direction: column; gap: 4px;">
                        <div v-for="item in (kudosLinkTab === 'Home' ? siteProjects : projects)" :key="item.id" @click="selectKudosLink(item)" style="display: flex; align-items: flex-start; gap: 8px; padding: 4px; cursor: pointer; border-radius: 3px; transition: background 0.1s;" onmouseover="this.style.background='#F4F5F7'" onmouseout="this.style.background='transparent'">
                          <i class="fa-solid fa-rocket" style="color: #6B778C; margin-top: 4px;"></i>
                          <div style="display: flex; flex-direction: column;">
                            <span style="font-size: 13px; color: #172B4D;">{{ item.name }}</span>
                            <span style="font-size: 11px; color: #6B778C;">{{ item.key || 'Dự án' }}</span>
                          </div>
                        </div>
                        <div v-if="(kudosLinkTab === 'Home' ? siteProjects : projects).length === 0" style="padding: 8px; font-size: 12px; color: #6B778C;">Không có dự án nào.</div>
                      </div>
                    </div>

                    <div style="display: flex; justify-content: flex-end; gap: 8px; margin-top: 8px;">
                      <button class="secondary-btn" @click="isKudosLinkDropdownOpen = false" style="height: 32px;">Hủy</button>
                      <button class="primary-btn" @click="insertKudosLink" style="height: 32px;">Chèn</button>
                    </div>
                 </div>
               </div>
             </div>

             <!-- Personalize Graphic Card -->
             <div style="width: 100%; height: 280px; border-radius: 8px; position: relative; overflow: hidden; display: flex; align-items: center; justify-content: center; box-shadow: 0 4px 12px rgba(0,0,0,0.1); cursor: pointer;" :style="{ background: selectedKudosGraphic.bg }" @click.stop="isKudosGraphicDropdownOpen = !isKudosGraphicDropdownOpen">
                <button class="secondary-btn" style="position: absolute; top: 12px; right: 12px; font-size: 12px; padding: 4px 8px; height: auto; pointer-events: none; color: white; background: rgba(255,255,255,0.2); border: none;">Cá nhân hóa</button>
                <div style="display: flex; flex-direction: column; align-items: center; justify-content: center; position: relative;">
                   <i :class="selectedKudosGraphic.icon1" :style="{ fontSize: '40px', color: selectedKudosGraphic.c1, position: 'absolute', right: '-40px', top: '-20px', transform: 'rotate(-15deg)' }"></i>
                   <i :class="selectedKudosGraphic.icon2" :style="{ fontSize: '100px', color: selectedKudosGraphic.c2, filter: 'drop-shadow(0 10px 10px rgba(0,0,0,0.2))' }"></i>
                </div>
                
                <!-- Graphic Picker Dropdown -->
                <div v-if="isKudosGraphicDropdownOpen" @click.stop class="dropdown-menu" style="position: absolute; top: 44px; right: 12px; z-index: 10; background: white; border-radius: 3px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border: 1px solid #DFE1E6; padding: 12px; width: 320px; display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; max-height: 250px; overflow-y: auto;">
                   <div v-for="(g, index) in kudosGraphics" :key="index" @click="selectKudosGraphic(g)" style="width: 60px; height: 60px; border-radius: 4px; display: flex; align-items: center; justify-content: center; cursor: pointer; position: relative; overflow: hidden;" :style="{ background: g.bg }">
                      <i :class="g.icon2" :style="{ fontSize: '24px', color: g.c2 }"></i>
                   </div>
                </div>
             </div>

             <!-- Action Buttons -->
             <div style="display: flex; justify-content: flex-end; gap: 12px; margin-top: 8px;">
                <button class="secondary-btn" style="height: 36px; padding: 0 16px; font-size: 14px; font-weight: 500;" @click="isGiveKudosOpen = false">Hủy</button>
                <button class="primary-btn" :disabled="!kudosText" style="height: 36px; padding: 0 16px; font-size: 14px; font-weight: 500;" @click="submitKudos">Khen ngợi</button>
             </div>
          </div>
       </div>

    </div>

  </div>
  <div v-else class="loading-state">
    <div class="loader-spinner"></div>
    <p>Loading team details...</p>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useTeamStore } from '@/store/useTeamStore'
import { usePeopleStore } from '@/store/usePeopleStore'
import { useGoalStore } from '@/store/useGoalStore'
import { useHomeProjectStore } from '@/store/useHomeProjectStore'
import { useSiteStore } from '@/store/useSiteStore'
import { useWorkTaskStore } from '@/store/useWorkTaskStore'
import { getStoredUser } from '@/utils/permissions'
import { getAvatarColor } from '@/utils/avatarHelper'
import UserAvatar from '@/components/common/UserAvatar.vue'
import RichTextEditor from '@/components/common/RichTextEditor.vue'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import DataModalField from '@/components/common/Foundation/DataModalField.vue'

const route = useRoute()
const router = useRouter()
const teamsBasePath = computed(() => route.path.startsWith('/teams') ? '/teams' : '/home/teams')
const teamStore = useTeamStore()
const peopleStore = usePeopleStore()
const goalStore = useGoalStore()
const homeProjectStore = useHomeProjectStore()
const siteStore = useSiteStore()
const workTaskStore = useWorkTaskStore()

const currentUser = getStoredUser()
// Only Admin or SystemAdmin can edit manager
const isSiteOwner = computed(() => {
  return currentUser?.role === 'Admin' || currentUser?.role === 'SystemAdmin'
})

const sites = computed(() => siteStore.sites || [])
const siteGoals = computed(() => goalStore.goals || [])
const siteProjects = computed(() => homeProjectStore.projects || [])

const currentTab = ref('overview')
const isMenuOpen = ref(false)
const isAddMemberOpen = ref(false)
const isMemberDropdownOpen = ref(false)
const isDeleteConfirmOpen = ref(false)
const isCreateGoalOpen = ref(false)
const isEditHierarchyOpen = ref(false)
const memberSearch = ref('')
const teamSearch = ref('')
const newGoalTitle = ref('')
const selectedMembers = ref([])

const teamTasks = computed(() => {
  if (!teamStore.projects || teamStore.projects.length === 0) return []
  const linkedProjectIds = teamStore.projects.map(p => p.id)
  return workTaskStore.tasks.filter(t => linkedProjectIds.includes(t.projectId))
})

const team = computed(() => teamStore.currentTeam)
const isArchived = computed(() => team.value?.status === 'Archived')
const members = computed(() => teamStore.members || [])
const hierarchy = computed(() => teamStore.hierarchy || { parent: null, children: [] })
const goals = computed(() => teamStore.goals || [])
const projects = computed(() => teamStore.projects || [])
const kudos = computed(() => teamStore.kudos || [])

const isEditingBio = ref(false)
const tempBio = ref('')

const startEditingBio = () => {
  tempBio.value = team.value.description || ''
  isEditingBio.value = true
}

const saveBio = async () => {
  try {
    await teamStore.updateTeam({ description: tempBio.value })
    isEditingBio.value = false
  } catch (e) {
    console.error('Failed to save bio')
  }
}

const cancelBio = () => {
  isEditingBio.value = false
}



const isParentDropdownOpen = ref(false)
const isChildDropdownOpen = ref(false)

const closeHierarchyDropdowns = () => {
  isParentDropdownOpen.value = false
  isChildDropdownOpen.value = false
}

const openParentDropdown = () => {
  isParentDropdownOpen.value = !isParentDropdownOpen.value
  isChildDropdownOpen.value = false
  if (teamStore.allTeams.length === 0) {
    teamStore.fetchAllTeams()
  }
}

const openChildDropdown = () => {
  isChildDropdownOpen.value = !isChildDropdownOpen.value
  isParentDropdownOpen.value = false
  if (teamStore.allTeams.length === 0) {
    teamStore.fetchAllTeams()
  }
}

const filteredTeams = computed(() => {
  let list = teamStore.allTeams.filter(t => t.id !== team.value?.id)
  
  if (teamStore.hierarchy?.parent?.id) {
    list = list.filter(t => t.id !== teamStore.hierarchy.parent.id)
  }
  if (teamStore.hierarchy?.children?.length > 0) {
    const selectedChildIds = teamStore.hierarchy.children.map(c => c.id)
    list = list.filter(t => !selectedChildIds.includes(t.id))
  }

  if (teamSearch.value) {
    const q = teamSearch.value.toLowerCase()
    list = list.filter(t => (t.name || '').toLowerCase().includes(q))
  }
  return list
})

const setParentTeam = async (t) => {
  if (!teamStore.hierarchy) teamStore.hierarchy = { parent: null, children: [] }
  teamStore.hierarchy.parent = t
  isParentDropdownOpen.value = false
  teamSearch.value = ''
}

const addChildTeam = async (t) => {
  if (!teamStore.hierarchy) teamStore.hierarchy = { parent: null, children: [] }
  if (!teamStore.hierarchy.children) teamStore.hierarchy.children = []
  if (!teamStore.hierarchy.children.find(c => c.id === t.id)) {
    teamStore.hierarchy.children.push(t)
  }
  isChildDropdownOpen.value = false
  teamSearch.value = ''
}

const removeChildTeam = async (childId) => {
  if (teamStore.hierarchy && teamStore.hierarchy.children) {
    teamStore.hierarchy.children = teamStore.hierarchy.children.filter(c => c.id !== childId)
  }
}

const isGoalDropdownOpen = ref(false)
const isProjectDropdownOpen = ref(false)
const isSprintAProjectOpen = ref(false)
const isSpaceDropdownOpen = ref(false)
const isManagerDropdownOpen = ref(false)
const goalSearch = ref('')
const projectSearch = ref('')

const selectManager = async (member) => {
  if (teamStore.currentTeam) {
    teamStore.currentTeam.manager = member
    await teamStore.updateManager(member.id)
  }
  isManagerDropdownOpen.value = false
}

const linkGoal = (goal) => {
  if (!teamStore.goals) teamStore.goals = []
  if (!teamStore.goals.find(g => g.id === goal.id)) {
    teamStore.goals.push({ ...goal, status: 'Đã hoàn tất 🚀' })
  }
  isGoalDropdownOpen.value = false
}

const linkProject = (proj) => {
  if (!teamStore.projects) teamStore.projects = []
  if (!teamStore.projects.find(p => p.id === proj.id)) {
    teamStore.projects.push({ ...proj, status: 'Đang thực hiện' })
  }
  isProjectDropdownOpen.value = false
}

const goToProjects = () => {
  router.push('/home/projects')
}

const goToProjectDetail = (id) => {
  router.push('/home/projects')
}

// Kudos Logic
const isGiveKudosOpen = ref(false)
const kudosText = ref('')

const isKudosLinkDropdownOpen = ref(false)
const isKudosTargetDropdownOpen = ref(false)
const isKudosEmojiDropdownOpen = ref(false)
const kudosLinkSearch = ref('')
const kudosLinkDisplay = ref('')
const kudosLinkTab = ref('Home')
const kudosEmojiSearch = ref('')

const isKudosGraphicDropdownOpen = ref(false)
const kudosGraphics = [
  { bg: '#0052CC', c1: '#FF8F73', c2: '#FF5630', icon1: 'fa-solid fa-fish-fins', icon2: 'fa-solid fa-box-open' },
  { bg: '#00875A', c1: '#FFC400', c2: '#FFAB00', icon1: 'fa-solid fa-star', icon2: 'fa-solid fa-trophy' },
  { bg: '#FF5630', c1: '#00B8D9', c2: '#008DA6', icon1: 'fa-solid fa-bolt', icon2: 'fa-solid fa-medal' },
  { bg: '#6554C0', c1: '#FF7452', c2: '#FF5630', icon1: 'fa-solid fa-heart', icon2: 'fa-solid fa-gem' },
  { bg: '#36B37E', c1: '#0052CC', c2: '#FFC400', icon1: 'fa-solid fa-thumbs-up', icon2: 'fa-solid fa-check-circle' },
  { bg: '#FFAB00', c1: '#6554C0', c2: '#FF5630', icon1: 'fa-solid fa-crown', icon2: 'fa-solid fa-award' },
  { bg: '#00B8D9', c1: '#36B37E', c2: '#FF8F73', icon1: 'fa-solid fa-lightbulb', icon2: 'fa-solid fa-rocket' },
  { bg: '#172B4D', c1: '#00B8D9', c2: '#0052CC', icon1: 'fa-solid fa-handshake', icon2: 'fa-solid fa-hand-holding-heart' }
]
const selectedKudosGraphic = ref(kudosGraphics[0])

const selectKudosGraphic = (g) => {
  selectedKudosGraphic.value = g
  isKudosGraphicDropdownOpen.value = false
}

const allEmojis = ['😀','😃','😄','😁','😆','😅','😂','🤣','😊','😇','🙂','🙃','😉','😌','😍','🥰','😘','😗','😙','😚','😋','😛','😝','😜','🤪','🤨','🧐','🤓','😎','🤩','🥳','😏','😒','😞','😔','😟','😕','🙁','☹️','😣','😖','😫','😩','🥺','😢','😭','😤','😠','😡','🤬','🤯','😳','🥵','🥶','😱','😨','😰','😥','😓','🤗','🤔','🤭','🤫','🤥','😶','😐','😑','😬','🙄','😯','😦','😧','😮','😲','🥱','😴','🤤','😪','😵','🤐','🥴','🤢','🤮','🤧','😷','🤒','🤕','🤑','🤠','😈','👿','👹','👺','🤡','💩','👻','💀','☠️','👽','👾','🤖','🎃','😺','😸','😹','😻','😼','😽','🙀','😿','😾','🙈','🙉','🙊','💥','💫','💦','💨','🐵','🐒','🦍','🦧','🐶','🐕','🦮','🐕‍🦺','🐩','🐺','🦊','🦝','🐱','🐈','🦁','🐯','🐅','🐆','🐴','🐎','🦄','🦓','🦌','🐮','🐂','🐃','🐄','🐷','🐖','🐗','🐽','🐏','🐑','🐐','🐪','🐫','🦙','🦒','🐘','🦏','🦛','🐭','🐁','🐀','🐹','🐰','🐇','🐿️','🦔','🦇','🐻','🐨','🐼','🦥','🦦','🦨','🦘','🦡','🐾','🦃','🐔','🐓','🐣','🐤','🐥','🐦','🐧','🕊️','🦅','🦆','🦢','🦉','🦩','🦚','🦜','🐸','🐊','🐢','🦎','🐍','🐲','🐉','🦕','🦖','🐳','🐋','🐬','🐟','🐠','🐡','🦈','🐙','🐚','🐌','🦋','🐛','🐜','🐝','🐞','🦗','🕷️','🕸️','🦂','🦟','🦠','💐','🌸','💮','🏵️','🌹','🥀','🌺','🌻','🌼','🌷','🌱','🌲','🌳','🌴','🌵','🌾','🌿','☘️','🍀','🍁','🍂','🍃','🍇','🍈','🍉','🍊','🍋','🍌','🍍','🥭','🍎','🍏','🍐','🍑','🍒','🍓','🥝','🍅','🥥','🥑','🍆','🥔','🥕','🌽','🌶️','🥒','🥬','🥦','🧄','🧅','🍄','🥜','🌰','🍞','🥐','🥖','🥨','🥯','🥞','🧇','🧀','🍖','🍗','🥩','🥓','🍔','🍟','🍕','🌭','🥪','🌮','🌯','🥙','🧆','🥚','🍳','🥘','🍲','🥣','🥗','🍿','🧈','🧂','🥫','🍱','🍘','🍙','🍚','🍛','🍜','🍝','🍠','🍢','🍣','🍤','🍥','🥮','🍡','🥟','🥠','🥡','🦀','🦞','🦐','🦑','🦪','🍦','🍧','🍨','🍩','🍪','🎂','🍰','🧁','🥧','🍫','🍬','🍭','🍮','🍯','🍼','🥛','☕','🍵','🍶','🍾','🍷','🍸','🍹','🍺','🍻','🥂','🥃','🥤','🧃','🧉','🧊','🥢','🍽️','🍴','🥄','🔪','🏺','🎉','👍','🚀','❤️','🔥','👏','🙌','💯','💪','✨','🌟']
const filteredKudosEmojis = computed(() => {
  if (!kudosEmojiSearch.value) return allEmojis
  // Since emojis are characters, we can't search them directly without a dictionary.
  // For visual simplicity, we will just return a subset if they type something
  return allEmojis.slice(0, 10)
})

const kudosTargetUsers = computed(() => {
  const allUsers = peopleStore.users || []
  return allUsers.filter(u => u.id !== currentUser?.id)
})

const isKudosRichText = ref(false)
const kudosTextBefore = ref('')
const kudosLinkText = ref('')
const kudosTextAfter = ref('')
const kudosEditorRef = ref(null)

const kudosTargetType = ref('team')
const kudosTargetName = ref('')
const kudosTargetAvatar = ref('')



const kudosTargetData = ref(null)

const selectKudosTarget = (type, item) => {
  kudosTargetType.value = type
  kudosTargetName.value = item.name || item.fullName
  kudosTargetAvatar.value = item.initials || (item.name ? item.name.substring(0, 2).toUpperCase() : 'T')
  kudosTargetData.value = item
  isKudosTargetDropdownOpen.value = false
}

const insertEmoji = (emoji) => {
  kudosText.value = (kudosText.value || '') + emoji
  if (kudosEditorRef.value) kudosEditorRef.value.innerHTML = kudosText.value
  isKudosEmojiDropdownOpen.value = false
}



const selectKudosLink = (item) => {
  kudosLinkSearch.value = item.name
  kudosLinkDisplay.value = item.name
}

const insertKudosLink = () => {
  const text = kudosLinkDisplay.value || kudosLinkSearch.value
  if (text) {
    const linkHtml = `<a href="/home/projects" style="color: #0052CC; text-decoration: none; font-weight: 500;" contenteditable="false">${text}</a>&nbsp;`
    kudosText.value = (kudosText.value || '') + ' ' + linkHtml
    if (kudosEditorRef.value) kudosEditorRef.value.innerHTML = kudosText.value
    isKudosLinkDropdownOpen.value = false
  }
}

const submitKudos = async () => {
  isGiveKudosOpen.value = false
  
  let finalMessage = kudosEditorRef.value?.innerHTML || kudosText.value

  const payload = {
    message: finalMessage,
    icon: selectedKudosGraphic.value?.icon2 || 'fa-solid fa-box-open'
  }
  
  if (kudosTargetType.value === 'team') {
    payload.departmentId = kudosTargetData.value?.id || teamStore.currentTeam?.id
  } else if (kudosTargetData.value?.id) {
    payload.receiverId = kudosTargetData.value.id
  }

  try {
    await teamStore.sendKudos(payload)
    // reset
    kudosText.value = ''
    if (kudosEditorRef.value) kudosEditorRef.value.innerHTML = ''
  } catch (err) {
    console.error('Failed to submit kudos', err)
  }
}

onMounted(async () => {
  await teamStore.initializeRealtime()
  const id = route.params.id
  await teamStore.fetchTeamDetail(id)
  if (siteStore.sites.length === 0) {
    await siteStore.fetchSites()
  }
  if (peopleStore.users.length === 0) {
    await peopleStore.fetchPeople()
  }
  if (workTaskStore.tasks.length === 0) {
    // Optionally fetch all tasks or tasks for specific projects if there is an endpoint
    // For mock/local testing, ensure it's loaded
  }
  
  if (teamStore.currentTeam) {
    kudosTargetName.value = teamStore.currentTeam.name
    kudosTargetAvatar.value = teamStore.currentTeam.avatarText || teamStore.currentTeam.name.substring(0,2)
  }

  
  // Close menu on click outside
  document.addEventListener('click', closeMenuOnOutsideClick)
})

onUnmounted(() => {
  document.removeEventListener('click', closeMenuOnOutsideClick)
})

const closeMenuOnOutsideClick = (e) => {
  if (isMenuOpen.value && !e.target.closest('.dropdown-container')) {
    isMenuOpen.value = false
  }
}

const toggleArchive = () => {
  teamStore.toggleArchive()
  isMenuOpen.value = false
}

const confirmDelete = async () => {
  try {
    await teamStore.deleteTeam()
    isDeleteConfirmOpen.value = false
    router.push(`${teamsBasePath.value}/list`)
  } catch (err) {
    console.error('Failed to delete team')
  }
}

// Add member logic
const filteredUsers = computed(() => {
  const allUsers = peopleStore.users || []
  
  // Exclude current team members
  const existingIds = members.value.map(m => m.id)
  let available = allUsers.filter(u => !existingIds.includes(u.id))
  
  if (memberSearch.value) {
    const q = memberSearch.value.toLowerCase()
    available = available.filter(u => (u.fullName || u.name || '').toLowerCase().includes(q) || (u.email || '').toLowerCase().includes(q))
  }
  return available
})

const toggleSelectMember = (id) => {
  const index = selectedMembers.value.indexOf(id)
  if (index === -1) {
    selectedMembers.value.push(id)
  } else {
    selectedMembers.value.splice(index, 1)
  }
}

const getSelectedUserName = (id) => {
  const user = peopleStore.users.find(u => u.id === id)
  return user ? (user.fullName || user.email) : id
}

watch(isAddMemberOpen, (val) => {
  if (val) {
    memberSearch.value = ''
    selectedMembers.value = []
    isMemberDropdownOpen.value = true
    if (peopleStore.users.length === 0) {
      peopleStore.fetchPeople()
    }
  }
})

const submitAddMember = async () => {
  if (selectedMembers.value.length === 0) return
  await teamStore.addMembers(selectedMembers.value)
  isAddMemberOpen.value = false
}
</script>

<style scoped>
.team-detail-container {
  display: flex;
  flex-direction: column;
  position: relative;
  /* Shift up to bleed under the transparent topbar if we had one, but we have a solid header. 
     Instead, we use negative margin to override the padding of the parent layout if needed. 
     For now, just render cleanly. */
  margin: -32px -40px; 
}

.team-cover {
  height: 200px;
  background-color: #ebecf0;
  background-size: cover;
  background-position: center;
  position: relative;
}

.cover-overlay {
  position: absolute;
  top: 0; left: 0; right: 0; bottom: 0;
  background-color: rgba(9, 30, 66, 0);
  display: flex;
  align-items: flex-start;
  justify-content: flex-end;
  padding: 16px;
  transition: background-color 0.2s;
}

.team-cover:hover .cover-overlay {
  background-color: rgba(9, 30, 66, 0.2);
}

.upload-cover-btn {
  background-color: rgba(23, 43, 77, 0.7);
  color: white;
  border: none;
  padding: 6px 12px;
  border-radius: 3px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  opacity: 0;
  transition: opacity 0.2s;
}

.team-cover:hover .upload-cover-btn {
  opacity: 1;
}

.upload-cover-btn:hover {
  background-color: rgba(23, 43, 77, 0.9);
}

.team-header-wrapper {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  padding: 0 40px;
  margin-top: -32px;
  margin-bottom: 24px;
}

.team-identity {
  display: flex;
  align-items: flex-end;
  gap: 20px;
}

.team-avatar {
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

.team-title-block {
  padding-bottom: 8px;
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

.badge {
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 12px;
  font-weight: 700;
  text-transform: uppercase;
}

.badge.archived {
  background-color: #dfe1e6;
  color: #42526e;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  padding-bottom: 8px;
}

.primary-btn {
  background-color: #0052cc;
  color: white;
  border: none;
  padding: 6px 12px;
  border-radius: 3px;
  font-weight: 500;
  font-size: 14px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.primary-btn:hover:not(:disabled) {
  background-color: #0047b3;
}

.primary-btn:disabled {
  background-color: #ebecf0;
  color: #a5adba;
  cursor: not-allowed;
}

.secondary-btn {
  background-color: rgba(9, 30, 66, 0.04);
  color: #42526e;
  border: none;
  padding: 6px 12px;
  border-radius: 3px;
  font-weight: 500;
  font-size: 14px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.secondary-btn:hover:not(:disabled) {
  background-color: rgba(9, 30, 66, 0.08);
}

.secondary-btn.small {
  padding: 4px 8px;
  font-size: 13px;
}

.secondary-btn:disabled {
  color: #a5adba;
  cursor: not-allowed;
}

.icon-btn {
  background: rgba(9, 30, 66, 0.04);
  border: none;
  cursor: pointer;
  width: 32px;
  height: 32px;
  color: #42526e;
  border-radius: 3px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color 0.2s;
}

.icon-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.icon-btn.starred {
  color: #ffab00;
}

.dropdown-container {
  position: relative;
}

.dropdown-menu {
  position: absolute;
  top: 100%;
  right: 0;
  margin-top: 4px;
  width: 200px;
  background: white;
  border-radius: 3px;
  box-shadow: 0 4px 8px -2px rgba(9, 30, 66, 0.25), 0 0 1px rgba(9, 30, 66, 0.31);
  padding: 8px 0;
  z-index: 1000;
}

.menu-item {
  width: 100%;
  text-align: left;
  background: none;
  border: none;
  padding: 8px 16px;
  font-size: 14px;
  color: #172b4d;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 8px;
}

.menu-item:hover:not(:disabled) {
  background-color: #f4f5f7;
}

.menu-item:disabled {
  color: #a5adba;
  cursor: not-allowed;
}

.menu-item.danger {
  color: #de350b;
}

.menu-item.danger:hover {
  background-color: #ffeee6;
}

.menu-divider {
  height: 1px;
  background-color: #dfe1e6;
  margin: 4px 0;
}

.tabs-nav {
  display: flex;
  border-bottom: 2px solid #dfe1e6;
  gap: 24px;
  padding: 0 40px;
}

.tab-btn {
  background: none;
  border: none;
  padding: 8px 0 12px;
  font-size: 14px;
  font-weight: 500;
  color: #5e6c84;
  cursor: pointer;
  position: relative;
  margin-bottom: -2px;
  border-bottom: 2px solid transparent;
  transition: color 0.2s;
}

.tab-btn:hover {
  color: #172b4d;
}

.tab-btn.active {
  color: #0052cc;
  border-bottom-color: #0052cc;
}

.tab-content {
  padding: 32px 40px;
  flex: 1;
}

.team-layout {
  display: flex;
  gap: 32px;
}

.main-content {
  flex: 1;
  min-width: 0; /* Prevent overflow */
}

.right-sidebar {
  width: 320px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.sidebar-section h3 {
  font-size: 14px;
  font-weight: 600;
  color: #5e6c84;
  text-transform: uppercase;
  margin: 0 0 12px 0;
}

.meta-item {
  font-size: 14px;
  color: #172b4d;
  margin-bottom: 8px;
}

.status-badge.active {
  background-color: #e3fcef;
  color: #006644;
}

.archived-banner {
  background-color: #fafbfc;
  border: 1px solid #dfe1e6;
  border-left: 4px solid #6b778c;
  padding: 12px 16px;
  border-radius: 3px;
  color: #172b4d;
  margin-bottom: 24px;
  font-size: 14px;
  font-weight: 500;
}

.read-only-state .info-section,
.read-only-state .jira-table,
.read-only-state .kudos-card {
  opacity: 0.9;
}

/* Tab panes content */
.info-section {
  margin-bottom: 32px;
  max-width: 800px;
}

.info-section h3 {
  font-size: 16px;
  font-weight: 600;
  color: #172b4d;
  margin: 0 0 12px 0;
}

.description-text {
  color: #172b4d;
  line-height: 1.6;
}

.section-header-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  max-width: 800px;
}

.section-header-row h3 {
  margin: 0;
}

.member-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
  gap: 16px;
}

.member-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px;
  border-radius: 3px;
}

.member-item:hover {
  background-color: #fafbfc;
}

.member-avatar-small {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background-color: #0052cc;
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: bold;
}

.member-info {
  display: flex;
  flex-direction: column;
}

.member-name {
  font-weight: 500;
  font-size: 14px;
  color: #172b4d;
}

.member-role {
  font-size: 12px;
  color: #5e6c84;
}

.empty-state {
  text-align: center;
  padding: 40px;
  background-color: #fafbfc;
  border: 1px dashed #dfe1e6;
  border-radius: 3px;
  color: #5e6c84;
}

.empty-icon {
  font-size: 32px;
  display: block;
  margin-bottom: 16px;
}

.empty-state-micro {
  text-align: center;
  padding: 24px;
  color: #5e6c84;
  font-size: 14px;
  font-style: italic;
}

/* Hierarchy */
.hierarchy-section {
  max-width: 600px;
}

.hierarchy-section h3 {
  font-size: 16px;
  font-weight: 600;
  color: #172b4d;
  margin: 0 0 12px 0;
}

.hierarchy-card {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  border: 1px solid #dfe1e6;
  border-radius: 3px;
  margin-bottom: 8px;
  background-color: #ffffff;
}

.team-identity-small {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 500;
  color: #172b4d;
}

.link-btn {
  background: none;
  border: none;
  color: #0052cc;
  cursor: pointer;
  font-weight: 500;
  font-size: 13px;
}

.link-btn:hover:not(:disabled) {
  text-decoration: underline;
}

.link-btn:disabled {
  color: #a5adba;
  cursor: not-allowed;
}

.empty-inline {
  color: #5e6c84;
  font-size: 14px;
}

.mt-24 { margin-top: 24px; }
.mt-16 { margin-top: 16px; }

/* Tables */
.jira-table {
  width: 100%;
  max-width: 800px;
  border-collapse: collapse;
  text-align: left;
}

.jira-table th {
  padding: 8px 12px;
  font-size: 12px;
  font-weight: 600;
  color: #5e6c84;
  border-bottom: 2px solid #dfe1e6;
}

.jira-table td {
  padding: 12px;
  font-size: 14px;
  color: #172b4d;
  border-bottom: 1px solid #dfe1e6;
}

.status-badge {
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  background-color: #dfe1e6;
  color: #42526e;
}

.status-badge.on-track {
  background-color: #e3fcef;
  color: #006644;
}

/* Kudos */
.kudos-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
}

.kudos-card {
  border: 1px solid #dfe1e6;
  border-radius: 3px;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.kudos-icon {
  font-size: 24px;
}

.kudos-msg {
  margin: 0;
  font-size: 14px;
  color: #172b4d;
  font-style: italic;
}

.kudos-sender {
  font-size: 12px;
  color: #5e6c84;
  display: block;
  margin-top: 8px;
}

.reaction-btn {
  background: rgba(9, 30, 66, 0.04);
  border: 1px solid transparent;
  padding: 2px 6px;
  border-radius: 12px;
  font-size: 12px;
  cursor: pointer;
}

.reaction-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

/* Modals */
.modal-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background-color: rgba(9, 30, 66, 0.54);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background-color: #ffffff;
  border-radius: 3px;
  width: 500px;
  max-width: 90vw;
  box-shadow: 0 8px 16px -4px rgba(9, 30, 66, 0.25), 0 0 1px rgba(9, 30, 66, 0.31);
}

.modal-header {
  padding: 20px 24px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid #ebecf0;
}

.modal-header h2 {
  margin: 0;
  font-size: 20px;
  font-weight: 500;
  color: #172b4d;
}

.close-btn {
  background: none;
  border: none;
  font-size: 24px;
  color: #6b778c;
  cursor: pointer;
}

.modal-body {
  padding: 24px;
}

.info-text {
  font-size: 12px;
  color: #5e6c84;
  margin: 0 0 16px 0;
}

.search-box {
  position: relative;
  margin-bottom: 16px;
}

.search-box input {
  width: 100%;
  padding: 8px 12px 8px 44px;
  border: 2px solid #dfe1e6;
  border-radius: 3px;
  font-size: 14px;
  box-sizing: border-box;
  outline: none;
}

.search-box input:focus {
  border-color: #4c9aff;
}

.search-box .search-icon {
  position: absolute;
  left: 10px;
  top: 50%;
  transform: translateY(-50%);
  font-size: 12px;
  color: #6b778c;
}

.member-select-list {
  max-height: 200px;
  overflow-y: auto;
  border: 1px solid #dfe1e6;
  border-radius: 3px;
}

.select-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 12px;
  border-bottom: 1px solid #ebecf0;
  cursor: pointer;
  position: relative;
}

.select-item:hover {
  background-color: #fafbfc;
}

.user-details {
  display: flex;
  flex-direction: column;
  flex: 1;
}

.user-name {
  font-size: 14px;
  font-weight: 500;
  color: #172b4d;
}

.user-email {
  font-size: 12px;
  color: #5e6c84;
}

.check-icon {
  color: #0052cc;
  font-size: 14px;
}

.selected-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 16px;
}

.tag-chip {
  display: flex;
  align-items: center;
  gap: 6px;
  background-color: #ebecf0;
  border-radius: 3px;
  padding: 4px 8px;
  font-size: 12px;
  color: #172b4d;
  font-weight: 500;
}

.remove-tag {
  color: #5e6c84;
  cursor: pointer;
  font-size: 10px;
}

.remove-tag:hover {
  color: #de350b;
}

.member-avatar-micro {
  width: 24px;
  height: 24px;
  background-color: #172b4d;
  color: white;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  font-weight: bold;
}

.modal-footer {
  padding: 16px 24px;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  border-top: 1px solid #ebecf0;
}

.cancel-btn, .submit-btn {
  padding: 8px 12px;
  border-radius: 3px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  border: none;
}

.cancel-btn {
  background: transparent;
  color: #5e6c84;
}

.cancel-btn:hover {
  background: rgba(9, 30, 66, 0.08);
}

.submit-btn {
  background: #0052cc;
  color: white;
}

.submit-btn:hover {
  background: #0047b3;
}

.danger-modal .submit-btn.danger {
  background-color: #de350b;
}

.danger-modal .submit-btn.danger:hover {
  background-color: #bf2600;
}

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #5e6c84;
  gap: 16px;
}

.loader-spinner {
  width: 32px;
  height: 32px;
  border: 3px solid #dfe1e6;
  border-top-color: #0052cc;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.bio-container:hover {
  background-color: #FAFBFC;
  border-color: #DFE1E6 !important;
}

.team-status-row i {
  margin-right: 4px;
}

.team-option:hover {
  background-color: #F4F5F7;
}

.hierarchy-card-box:hover {
  background-color: #FAFBFC;
}
</style>
