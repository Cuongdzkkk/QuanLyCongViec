<template>
  <template v-if="team">
    <DetailLayout>
    <template #hero>
      <DetailHero
        cover-pattern="team"
        :back-url="teamsBasePath"
        back-text="Quay lại"
        :title="team.name"
        avatar-type="boxy"
        avatar-icon="fa-regular fa-folder"
      >
        <template #cover-actions>
          <button class="sprinta-btn sprinta-btn-secondary" v-if="!isArchived"><i class="fa-solid fa-camera"></i> Change Cover</button>
        </template>
        
        <template #badges>
          <span class="status-badge" :class="!isArchived ? 'active' : 'archived'" style="font-size: 10px; padding: 2px 6px; margin-left: 8px;">{{ !isArchived ? 'ACTIVE' : 'ARCHIVED' }}</span>
        </template>
        
        <template #meta>
          <span v-if="isArchived"><i class="fa-solid fa-box-archive"></i> Đã lưu trữ</span>
        </template>
        
        <template #actions>
          <button class="sprinta-icon-btn" @click="teamStore.toggleStar()" :class="{ starred: team.isStarred }" title="Star team">
            <i :class="team.isStarred ? 'fa-solid fa-star' : 'fa-regular fa-star'"></i>
          </button>
        </template>
        
        <template #overflow>
          <button class="sprinta-menu-item" @click="toggleArchive">
            <i :class="isArchived ? 'fa-solid fa-box-open' : 'fa-solid fa-box-archive'" style="width: 16px;"></i> {{ isArchived ? 'Khôi phục (Restore)' : 'Lưu trữ (Archive)' }}
          </button>
          <button class="sprinta-menu-item danger" @click="isDeleteConfirmOpen = true"><i class="fa-solid fa-trash" style="width: 16px;"></i> Xóa (Delete)</button>
        </template>
      </DetailHero>
    </template>
    
    <template #tabs>
      <button class="sprinta-tab-btn" :class="{ active: currentTab === 'overview' }" @click="currentTab = 'overview'">Giới thiệu</button>
      <button class="sprinta-tab-btn" :class="{ active: currentTab === 'activity' }" @click="currentTab = 'activity'">SprintA</button>
      <button class="sprinta-tab-btn" :class="{ active: currentTab === 'hierarchy' }" @click="currentTab = 'hierarchy'">Phân cấp</button>
      <button class="sprinta-tab-btn" :class="{ active: currentTab === 'goals' }" @click="currentTab = 'goals'">Mục Tiêu</button>
      <button class="sprinta-tab-btn" :class="{ active: currentTab === 'projects' }" @click="currentTab = 'projects'">Dự Án</button>
      <button class="sprinta-tab-btn" :class="{ active: currentTab === 'kudos' }" @click="currentTab = 'kudos'">Khen ngợi</button>
    </template>
    
    <template #main>
      <div :class="{ 'read-only-state': isArchived }">
        <!-- Read Only Banner -->
        <div v-if="isArchived" class="archived-banner" style="margin-bottom: 24px;">
          This team is archived. It is read-only.
        </div>

      <!-- Overview -->
      <div v-if="currentTab === 'overview'" class="tab-pane">
        <section class="info-section" style="display: flex; flex-direction: column; gap: 8px;">
          <div class="section-header" style="display: flex; justify-content: space-between; align-items: center;">
            <h3 style="margin: 0; font-size: 16px; font-weight: 600; color: #172B4D;">Việc chúng tôi đang thực hiện</h3>
          </div>
          <div class="section-body">
            <RichTextEditor v-if="isEditingBio" v-model="tempBio" @save="saveBio" @cancel="cancelBio" placeholder="Chia sẻ những gì nhóm bạn đang thực hiện" />
            <div v-else @click="startEditingBio" :style="{ cursor: 'pointer', color: '#5E6C84', fontSize: '14px', padding: '8px', borderRadius: '3px', minHeight: '40px' }" onmouseover="this.style.backgroundColor='#FAFBFC'" onmouseout="this.style.backgroundColor='transparent'">
              <div v-if="team.description && team.description !== '<p></p>'" v-html="safeTeamDescription" class="tiptap-content" style="color: #172B4D;"></div>
              <div v-else>Chia sẻ những gì nhóm bạn đang thực hiện</div>
            </div>
          </div>
        </section>
        
        <section class="info-section">
          <div class="section-header-row">
            <h3>Members ({{ members.length }})</h3>
            <button class="secondary-btn small" :disabled="isArchived" @click="isAddMemberOpen = true">Add</button>
          </div>
          <div class="member-list">
            <div class="member-item cursor-pointer flex items-center gap-3 p-2 rounded hover:bg-gray-50 transition-colors" v-for="member in members" :key="member.id" @click="goToMemberProfile(member.id)">
              <UserAvatar :user="{ ...member, fullName: member.fullName || member.name, avatarColor: getAvatarColor(member.email || member.id) }" :size="32" :fontSize="14" :clickable="false" />
              <div class="member-info">
                <span class="member-name hover:underline" style="font-weight: 600; color: #172B4D; font-size: 13.5px;">{{ member.fullName || member.name }}</span>
                <span class="member-role" style="font-size: 12px; color: #5E6C84; display: block; margin-top: 2px;">{{ member.role || 'Thành viên' }}</span>
              </div>
            </div>
          </div>
        </section>
      </div>

      <!-- Activity -->
      <div v-if="currentTab === 'activity'" class="tab-pane">
        <section class="info-section">
          <div class="section-header-row">
            <h3>Công việc SprintA của {{ team.name }}</h3>
          </div>
          
          <div class="activity-list" style="display: flex; flex-direction: column; gap: 10px; margin-bottom: 32px;" v-if="taskProjectGroups.length > 0">
            <div v-for="group in taskProjectGroups" :key="group.projectId" class="team-task-project">
              <button type="button" class="team-task-project-header" @click="toggleTaskProject(group.projectId)">
                <span><i class="fa-solid fa-rocket"></i> {{ group.projectName }}</span>
                <span><i class="fa-solid" :class="expandedTaskProjects[group.projectId] ? 'fa-chevron-up' : 'fa-chevron-down'"></i></span>
              </button>
              <div v-if="expandedTaskProjects[group.projectId]" class="team-task-member-list">
                <div v-for="member in group.members" :key="member.userId" class="team-task-member-row">
                  <div style="display:flex; align-items:center; gap:10px;">
                    <UserAvatar :user="{ ...member, fullName: member.fullName, avatarColor: getAvatarColor(member.email || member.userId) }" :size="28" :fontSize="10" />
                    <span>{{ member.fullName }}</span>
                  </div>
                  <span class="team-task-progress">{{ member.completed }}/{{ member.total }}</span>
                </div>
              </div>
            </div>
          </div>
          <div v-else style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 24px; display: flex; align-items: flex-start; gap: 24px; background: white;">
             <div style="position: relative;">
                <div style="width: 80px; height: 80px; background-color: #EBECF0; border-radius: 8px; display: flex; align-items: center; justify-content: center; transform: rotate(-5deg);">
                   <i class="fa-brands fa-jira" style="font-size: 32px; color: #0052CC;"></i>
                </div>
                <div v-if="!isArchived" style="position: absolute; bottom: -8px; right: -8px; width: 32px; height: 32px; background-color: #0052CC; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; cursor: pointer; box-shadow: 0 2px 4px rgba(0,0,0,0.1);">
                   <i class="fa-solid fa-plus" style="font-size: 16px;"></i>
                </div>
             </div>
             <div style="flex: 1; position: relative;">
                <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Công việc SprintA</h4>
                <p style="font-size: 13px; color: #6B778C; margin-bottom: 16px; line-height: 1.5;">Công việc được liên kết với đội ngũ của bạn trong SprintA sẽ xuất hiện tại đây.</p>
                <div style="position: relative; display: inline-block;">
                  <button v-if="!isArchived" class="secondary-btn">Thêm hạng mục công việc SprintA</button>
                </div>
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
        <div v-if="!goals || goals.length === 0">
           <div class="section-header-row">
             <h3>Đang đóng góp cho</h3>
           </div>
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
                 <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Mục tiêu của đội ngũ</h4>
                 <p style="font-size: 13px; color: #6B778C; margin-bottom: 16px; line-height: 1.5;">Chưa có mục tiêu nào được liên kết với đội ngũ này.</p>
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
        <div v-else>
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

           <div class="section-header-row">
             <h3>Đã hoàn tất</h3>
           </div>
           <div style="background-color: #FAFBFC; border: 1px solid #DFE1E6; border-radius: 3px; padding: 12px; text-align: center; color: #6B778C; font-size: 13px;">
             Chưa có mục tiêu nào hoàn thành
           </div>
        </div>
      </div>

      <!-- Projects -->
      <div v-if="currentTab === 'projects'" class="tab-pane">
        <div v-if="!projects || projects.length === 0">
           <div class="section-header-row">
             <h3>Đang đóng góp cho</h3>
           </div>
           <div style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 24px; display: flex; align-items: flex-start; gap: 24px;">
              <div style="position: relative;">
                 <div style="width: 80px; height: 80px; background-color: #EBECF0; border-radius: 8px; display: flex; align-items: center; justify-content: center; transform: rotate(-5deg);">
                    <i class="fa-solid fa-folder" style="font-size: 32px; color: #172B4D;"></i>
                 </div>
                 <div style="position: absolute; bottom: -8px; right: -8px; width: 32px; height: 32px; background-color: #0052CC; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; cursor: pointer; box-shadow: 0 2px 4px rgba(0,0,0,0.1);" @click="goToProjects">
                    <i class="fa-solid fa-plus" style="font-size: 16px;"></i>
                 </div>
              </div>
              <div style="flex: 1; position: relative;">
                 <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Dự án đội ngũ đang làm</h4>
                 <p style="font-size: 13px; color: #6B778C; margin-bottom: 16px; line-height: 1.5;">Chưa có dự án nào được liên kết với đội ngũ này.</p>
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
           <div class="section-header-row">
             <h3>Các dự án liên quan</h3>
           </div>
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
        <div class="section-header-row">
          <h3>Khen ngợi</h3>
        </div>
        <div style="border: 1px solid #DFE1E6; border-radius: 3px; padding: 24px; display: flex; align-items: flex-start; gap: 24px;">
           <div style="position: relative;">
              <div style="width: 80px; height: 80px; background-color: #FFFAE6; border-radius: 8px; display: flex; align-items: center; justify-content: center; transform: rotate(-5deg);">
                 <i class="fa-solid fa-medal" style="font-size: 32px; color: #FFAB00;"></i>
              </div>
              <div style="position: absolute; bottom: -8px; right: -8px; width: 32px; height: 32px; background-color: #FFAB00; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; border: 2px solid white; cursor: pointer; box-shadow: 0 2px 4px rgba(0,0,0,0.1);" @click="isGiveKudosOpen = true">
                 <i class="fa-solid fa-plus" style="font-size: 16px;"></i>
              </div>
           </div>
           <div style="flex: 1; position: relative;">
              <h4 style="font-size: 14px; color: #172B4D; margin-bottom: 8px;">Đội ngũ này chưa nhận được lời khen ngợi nào</h4>
              <p style="font-size: 13px; color: #6B778C; margin-bottom: 16px; line-height: 1.5;">Khen ngợi giúp công nhận những thành quả cá nhân và đội ngũ xuất sắc. Hãy gửi lời khen để khuyến khích mọi người!</p>
              <div style="position: relative; display: inline-block;">
                <button class="secondary-btn" @click="isGiveKudosOpen = true" style="display: flex; align-items: center; gap: 8px;">
                  <i class="fa-regular fa-heart"></i> Give kudos
                </button>
              </div>
           </div>
        </div>
      </div>
      </div>
    </template>
        
    <template #sidebar>
      <!-- Card: Liên kết đội ngũ -->
      <div class="sidebar-card">
        <div class="sidebar-card-header">
          <h3>Liên kết đội ngũ <span class="badge">{{ linkedEntityCount }}</span></h3>
        </div>
        <div class="link-items">
          <el-popover
            placement="bottom-start"
            :width="250"
            trigger="click"
            popper-class="search-dropdown-popper"
          >
            <template #reference>
              <div class="link-item">
                <div class="link-item-icon project"><i class="fa-solid fa-rocket"></i></div>
                <span class="link-item-label">Thêm dự án SprintA</span>
              </div>
            </template>
            <div class="dropdown-menu-content" style="max-height: 200px; overflow-y: auto;">
              <div style="padding: 4px 8px; font-size: 11px; font-weight: bold; color: #6B778C; text-transform: uppercase;">Dự án trong Space</div>
              <div class="team-option" v-for="sp in siteProjects" :key="sp.id" @click="linkProject(sp)" style="display: flex; align-items: center; gap: 8px; padding: 8px; cursor: pointer; border-radius: 3px;">
                 <i class="fa-solid fa-rocket" style="color: #6B778C; font-size: 14px;"></i>
                 <span class="option-name" style="font-size: 13px; color: #172B4D;">{{ sp.name }}</span>
              </div>
              <div v-if="!siteProjects || siteProjects.length === 0" style="padding: 8px; font-size: 12px; color: #6B778C;">Không có dự án nào</div>
            </div>
          </el-popover>
          
          <el-popover
            placement="bottom-start"
            :width="250"
            trigger="click"
            popper-class="search-dropdown-popper"
          >
            <template #reference>
              <div class="link-item">
                <div class="link-item-icon space"><i class="fa-brands fa-confluence"></i></div>
                <span class="link-item-label">Thêm không gian</span>
              </div>
            </template>
            <div class="dropdown-menu-content" style="max-height: 200px; overflow-y: auto;">
              <div class="team-option" v-for="space in ownedSites" :key="space.id" @click="linkSite(space)" style="display: flex; align-items: center; gap: 8px; padding: 8px; cursor: pointer; border-radius: 3px;">
                 <div class="space-avatar" style="width: 20px; height: 20px; background: #0052CC; color: white; border-radius: 3px; display: flex; align-items: center; justify-content: center; font-size: 10px;">{{ space.name.substring(0,1).toUpperCase() }}</div>
                 <span class="option-name" style="font-size: 13px; color: #172B4D;">{{ space.name }}</span>
              </div>
              <div v-if="ownedSites.length === 0" style="padding: 8px; font-size: 12px; color: #6B778C;">Bạn chưa sở hữu site nào khác</div>
            </div>
          </el-popover>
          
          <div class="link-item" @click="addExternalLink">
            <div class="link-item-icon link"><i class="fa-solid fa-link"></i></div>
            <span class="link-item-label">Thêm liên kết</span>
          </div>
        </div>
      </div>

      <!-- Card: Chi tiết -->
      <div class="sidebar-card">
        <h3>Chi tiết</h3>
        
        <div class="meta-item-row">
           <span class="meta-label">Đội ngũ gốc</span>
           <div class="hierarchy-card mini" v-if="hierarchy?.parent">
             <div class="team-identity-small">
               <div class="member-avatar-micro" style="background-color: #FFAB00; color: #172B4D;">{{ hierarchy.parent.name.substring(0,2).toUpperCase() }}</div>
               <span class="team-name">{{ hierarchy.parent.name }}</span>
               <i class="fa-solid fa-circle-check"></i>
             </div>
           </div>
           <span v-else class="meta-value-empty">Không có đội ngũ gốc</span>
        </div>

        <div class="meta-item-row align-start">
           <span class="meta-label">Đội ngũ phụ</span>
           <div class="hierarchy-list" v-if="hierarchy?.children?.length">
             <div class="hierarchy-card mini" v-for="child in hierarchy.children" :key="child.id">
               <div class="team-identity-small">
                 <div class="member-avatar-micro">{{ child.name.substring(0,2).toUpperCase() }}</div>
                 <span class="team-name">{{ child.name }}</span>
               </div>
             </div>
           </div>
           <span v-else class="meta-value-empty">Không có đội ngũ phụ</span>
        </div>

        <div class="meta-item-row">
           <span class="meta-label">Loại đội ngũ</span>
           <span class="meta-value bold">Đội ngũ chính thức <i class="fa-solid fa-circle-check"></i></span>
        </div>

        <div class="meta-item-row">
           <span class="meta-label">Người quản lý</span>
           <div class="manager-selector-wrapper">
              <div class="manager-trigger-btn" @click="isManagerDropdownOpen = !isManagerDropdownOpen">
                 <UserAvatar v-if="team.manager" :user="{ ...team.manager, fullName: team.manager.name, avatarColor: getAvatarColor(team.manager.email || team.manager.id) }" :size="18" :fontSize="8" />
                 <i class="fa-solid fa-user-plus" v-else></i>
                 <span>{{ team.manager ? team.manager.name : 'Chọn người quản lý' }}</span>
              </div>
              <!-- Dropdown Menu -->
              <div class="dropdown-menu" v-if="isManagerDropdownOpen" @click.stop style="position: absolute; top: 100%; left: 0; margin-top: 4px; z-index: 100; width: 250px; padding: 8px; box-shadow: 0 8px 30px rgba(0,0,0,0.08); border-radius: 8px; border: 1px solid rgba(148, 163, 184, 0.15); background: white; max-height: 200px; overflow-y: auto;">
                <div class="team-option" v-for="m in members" :key="m.id" @click="selectManager(m)" style="display: flex; align-items: center; gap: 8px; padding: 8px; cursor: pointer; border-radius: 3px;">
                  <UserAvatar :user="{ ...m, fullName: m.fullName || m.name, avatarColor: getAvatarColor(m.email || m.id) }" :size="20" :fontSize="10" />
                  <span class="option-name">{{ m.fullName || m.name }}</span>
                </div>
              </div>
           </div>
        </div>
      </div>
    </template>
  </DetailLayout>
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
  </template>
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
import { ElMessage } from 'element-plus'
import UserAvatar from '@/components/common/UserAvatar.vue'
import RichTextEditor from '@/components/common/RichTextEditor.vue'
import DOMPurify from 'dompurify'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import DataModalField from '@/components/common/Foundation/DataModalField.vue'
import DetailLayout from '@/components/common/Detail/DetailLayout.vue'
import DetailHero from '@/components/common/Detail/DetailHero.vue'

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
const siteProjects = computed(() => {
  const siteId = siteStore.recentSite?.id || siteStore.recentSite?.Id
  return (homeProjectStore.projects || []).filter(project => {
    const projectSiteId = project.workspaceId || project.WorkspaceId
    return !siteId || !projectSiteId || `${projectSiteId}` === `${siteId}`
  })
})
const ownedSites = computed(() => sites.value.filter(site => {
  const role = `${site.workspaceRole || site.role || site.Role || ''}`.toLowerCase()
  return site.isOwner === true || site.IsOwner === true || ['owner', 'admin', 'administrator'].includes(role)
}))

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

const teamTasks = computed(() => (teamStore.activityTasks || []).filter(task => {
  const assignees = task.assignees || task.Assignees || []
  return assignees.length > 0 || task.assignedUser || task.assignedUserId
}))
const expandedTaskProjects = ref({})
const taskProjectGroups = computed(() => {
  const groups = new Map()
  for (const task of teamTasks.value) {
    const projectId = task.projectId || task.ProjectId
    if (!projectId) continue
    if (!groups.has(projectId)) groups.set(projectId, { projectId, projectName: task.projectName || 'Project', members: new Map() })
    const group = groups.get(projectId)
    const assignees = (task.assignees || task.Assignees || []).length
      ? (task.assignees || task.Assignees)
      : [task.assignedUser || { userId: task.assignedUserId, fullName: task.assignedUserName }]
    for (const assignee of assignees) {
      const userId = assignee.userId || assignee.UserId || assignee.id
      if (!userId) continue
      if (!group.members.has(userId)) group.members.set(userId, { ...assignee, userId, fullName: assignee.fullName || assignee.name || 'Thành viên', total: 0, completed: 0 })
      const member = group.members.get(userId)
      member.total += 1
      const status = `${task.status || task.statusName || ''}`.toLowerCase()
      if (task.completed === true || status.includes('complete') || status.includes('hoàn tất') || status.includes('done')) member.completed += 1
    }
  }
  return Array.from(groups.values()).map(group => ({ ...group, members: Array.from(group.members.values()) }))
})
const linkedEntityCount = computed(() => (projects.value?.length || 0) + (goals.value?.length || 0))
const toggleTaskProject = projectId => { expandedTaskProjects.value[projectId] = !expandedTaskProjects.value[projectId] }

const team = computed(() => teamStore.currentTeam)
const sanitizeHtml = (value) => DOMPurify.sanitize(value || '')
const safeTeamDescription = computed(() => sanitizeHtml(team.value?.description || ''))
const isArchived = computed(() => team.value?.status === 'Archived')
const members = computed(() => teamStore.members || [])
const hierarchy = computed(() => teamStore.hierarchy || { parent: null, children: [] })
const goals = computed(() => teamStore.goals || [])
const projects = computed(() => teamStore.projects || [])
const kudos = computed(() => teamStore.kudos || [])

const isEditingBio = ref(false)
const tempBio = ref('')

const startEditingBio = () => {
  let desc = team.value.description || ''
  if (desc.includes('Phát triển API, database và hạ tầng backend với .NET Core')) {
    desc = ''
  }
  tempBio.value = desc
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

const linkGoal = async (goal) => {
  try {
    await teamStore.linkGoal(goal.id)
    ElMessage.success('Đã liên kết mục tiêu với team')
  } catch (err) {
    ElMessage.error(err?.response?.data?.message || 'Không thể liên kết mục tiêu')
  }
  isGoalDropdownOpen.value = false
}

const linkProject = async (proj) => {
  try {
    await teamStore.linkProject(proj.id)
    ElMessage.success('Đã liên kết project với team')
  } catch (err) {
    ElMessage.error(err?.response?.data?.message || 'Không thể liên kết project')
  }
  isProjectDropdownOpen.value = false
  isSprintAProjectOpen.value = false
}

const linkSite = site => ElMessage.success(`Đã chọn site ${site.name || site.Name}`)
const addExternalLink = () => {
  const url = window.prompt('Nhập URL cần liên kết')
  if (!url) return
  try { new URL(url); ElMessage.success('Đã thêm liên kết') } catch { ElMessage.error('URL không hợp lệ') }
}

const goToMemberProfile = (memberId) => {
  if (route.path.startsWith('/home/')) {
    router.push(`/home/profile/${memberId}`)
  } else {
    router.push(`/profile/${memberId}`)
  }
}

const goToProjects = () => {
  if (route.path.startsWith('/home/')) {
    router.push('/home/projects')
  } else {
    router.push('/your-work')
  }
}

const goToProjectDetail = (id) => {
  if (route.path.startsWith('/home/')) {
    router.push(`/home/projects/${id}`)
  } else {
    router.push(`/space/project/${id}`)
  }
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
  if (siteStore.sites.length === 0) await siteStore.fetchSites()
  if (homeProjectStore.projects.length === 0) await homeProjectStore.fetchProjects()
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
  margin: -8px 0 0;
  width: 100% !important;
  max-width: none !important;
}

.team-cover {
  height: 200px;
  background-color: #ebecf0;
  background-size: cover;
  background-position: center;
  position: relative;
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
  padding: 0 18px;
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

.tab-content {
  padding: 8px 18px 32px;
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
  gap: 20px;
}

/* Sidebar Cards styling */
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

.sidebar-card h3 .badge {
  background-color: #f1f5f9;
  color: #475569;
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 11px;
  font-weight: 600;
}

.link-items {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.team-task-project {
  border: 1px solid #dfe1e6;
  border-radius: 8px;
  overflow: hidden;
  background: #fff;
}

.team-task-project-header {
  width: 100%;
  border: 0;
  background: #fff;
  color: #172b4d;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 16px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
}

.team-task-project-header:hover { background: #f7f9fc; }
.team-task-project-header i { color: #0052cc; margin-right: 8px; }

.team-task-member-list { border-top: 1px solid #dfe1e6; }
.team-task-member-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 16px;
  color: #172b4d;
  font-size: 13px;
}
.team-task-member-row + .team-task-member-row { border-top: 1px solid #f0f2f5; }
.team-task-progress { color: #5e6c84; font-weight: 600; }

.link-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  background: #f8fafc;
  border: 1px solid rgba(148, 163, 184, 0.1);
  border-radius: 8px;
  cursor: pointer;
  position: relative;
  transition: all 0.2s ease;
}

.link-item:hover {
  background: #f1f5f9;
  border-color: rgba(148, 163, 184, 0.2);
  transform: translateY(-1px);
}

.link-item-icon {
  width: 24px;
  height: 24px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
}

.link-item-icon.project {
  background-color: #0c66e4;
}

.link-item-icon.space {
  background-color: #0052cc;
}

.link-item-icon.link {
  background-color: #64748b;
}

.link-item-label {
  font-size: 13.5px;
  font-weight: 500;
  color: #475569;
}

.link-item:hover .link-item-label {
  color: #1e293b;
}

.dropdown-menu {
  position: absolute;
  top: 100%;
  right: 0;
  margin-top: 6px;
  width: 240px;
  background: #ffffff;
  border-radius: 8px !important;
  border: none !important;
  box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.08), 0 8px 10px -6px rgba(0, 0, 0, 0.03) !important;
  padding: 6px 0;
  z-index: 1000;
}

.dropdown-title {
  padding: 6px 12px;
  font-size: 11px;
  font-weight: 700;
  color: #64748b;
  text-transform: uppercase;
  border-bottom: 1px solid rgba(148, 163, 184, 0.08);
  margin-bottom: 4px;
}

.team-option {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 12px;
  cursor: pointer;
  border-radius: 0;
  transition: background-color 0.15s ease;
}

.team-option:hover {
  background-color: #f1f5f9;
}

.option-name {
  font-size: 13px;
  font-weight: 500;
  color: #334155;
}

.no-options {
  padding: 12px;
  font-size: 12px;
  color: #64748b;
  text-align: center;
}

.space-avatar {
  width: 20px;
  height: 20px;
  background: #0052cc;
  color: white;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  font-weight: bold;
}

.meta-item-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 36px;
}

.meta-item-row.align-start {
  align-items: flex-start;
}

.meta-label {
  width: 110px;
  color: #64748b;
  font-size: 13px;
  font-weight: 500;
  flex-shrink: 0;
}

.meta-value {
  font-size: 13px;
  color: #1e293b;
  display: flex;
  align-items: center;
  gap: 6px;
}

.meta-value.bold {
  font-weight: 600;
}

.meta-value i {
  color: #0c66e4;
}

.meta-value-empty {
  font-size: 13px;
  color: #94a3b8;
  font-style: italic;
}

.hierarchy-card.mini {
  flex: 1;
  margin: 0;
  padding: 6px 10px;
  border-radius: 6px;
  border: 1px solid rgba(148, 163, 184, 0.15);
  background: #f8fafc;
}

.hierarchy-list {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.team-identity-small {
  font-size: 13px;
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 500;
  color: #334155;
}

.team-identity-small i {
  color: #0c66e4;
  margin-left: auto;
}

.manager-selector-wrapper {
  flex: 1;
  position: relative;
}

.manager-trigger-btn {
  border: 1px solid rgba(148, 163, 184, 0.12);
  border-radius: 6px;
  padding: 6px 12px;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  color: #475569;
  font-size: 13px;
  font-weight: 500;
  background-color: #f8fafc;
  transition: all 0.2s ease;
}

.manager-trigger-btn:hover {
  background-color: #f1f5f9;
  border-color: rgba(148, 163, 184, 0.25);
  color: #1e293b;
}

.manager-avatar-placeholder {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background-color: #e2e8f0;
  color: #64748b;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
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
  align-items: flex-start;
  margin-bottom: 12px;
}

.section-header-row h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #172B4D;
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
