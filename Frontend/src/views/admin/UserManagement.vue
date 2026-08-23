<template>
  <AdminLayout>
    <div class="directory-page" :class="{ 'is-dimmed': showInvitePanel || showExportModal }" @click="closePageMenus">
      <div class="page-header">
        <div class="header-copy">
          <div class="breadcrumb">
            <i class="fa-solid fa-users-gear"></i>
            <span>ADMIN / USER DIRECTORY</span>
          </div>
          <h1 class="text-hero">{{ t('Users', 'Người dùng') }}</h1>
          <p class="text-desc">
            {{ t('Manage system accounts, map project roles, and organize team members into departments.', 'Quản lý tài khoản hệ thống, thiết lập vai trò dự án và phân chia thành viên theo phòng ban.') }}
          </p>
        </div>

        <div class="page-actions">
          <button type="button" class="primary-btn" @click.stop="openInvitePanel">
            <i class="fa-solid fa-user-plus"></i>
            {{ t('Invite users', 'Mời người dùng') }}
          </button>
          <button type="button" class="neutral-btn" @click="handleApproveRequests">
            {{ t('Approve requests', 'Duyệt yêu cầu') }}
            <span class="request-count">0</span>
          </button>
          <div class="menu-wrap" @click.stop>
            <button
              type="button"
              class="icon-btn"
              :class="{ 'is-open': openTopActionMenu }"
              @click="openTopActionMenu = !openTopActionMenu"
              aria-label="More actions"
            >
              <i class="fa-solid fa-ellipsis"></i>
            </button>
            <div v-if="openTopActionMenu" class="action-menu top-action-menu">
              <button type="button" @click="openExportModal">{{ t('Export users', 'Xuất danh sách') }}</button>
            </div>
          </div>
        </div>
      </div>

      <section class="content-frame">
        <div class="stats-grid">
          <div class="metric-card">
            <span>{{ t('Total users', 'Tổng số người dùng') }}</span>
            <strong>{{ users.length }}</strong>
          </div>
          <div class="metric-card">
            <span>{{ t('Active users', 'Đang hoạt động') }}</span>
            <strong>{{ activeUsersCount }}</strong>
          </div>
          <div class="metric-card">
            <span>{{ t('Organization admins', 'Quản trị viên') }}</span>
            <strong>{{ adminUsersCount }}</strong>
          </div>
        </div>

        <el-tabs v-model="activeTab" class="custom-tabs admin-tabs" style="margin-top: 24px;">
          <el-tab-pane :label="t('Departments', 'Phòng ban')" name="departments">
              <section class="admin-subcard">
              <div class="subcard-header" style="display: flex; justify-content: space-between; align-items: center;">
                <div>
                  <h3 class="text-section">{{ t('Departments', 'Phòng ban') }}</h3>
                  <p class="text-desc" style="margin: 0;">{{ t('Create and maintain departments used across user and project access.', 'Tạo và quản lý phòng ban dùng cho người dùng và phân quyền dự án.') }}</p>
                </div>
                <div class="table-search" v-if="!selectedDepartmentView" style="margin-top: 0;">
                  <i class="fa-solid fa-magnifying-glass search-icon"></i>
                  <input v-model="departmentSearchQuery" type="text" :placeholder="t('Search departments', 'Tìm kiếm phòng ban...')" class="search-input" />
                </div>
              </div>

            <div v-if="!selectedDepartmentView" class="department-form row-form">
              <input v-model="departmentDraft.name" type="text" :placeholder="t('Enter name to add department', 'Nhập tên để thêm phòng ban')" style="flex: 1;" />
              <label class="inline-check">
                <input v-model="departmentDraft.isActive" type="checkbox" />
                <span>{{ t('Active', 'Hoạt động') }}</span>
              </label>
              <label class="inline-check">
                <input v-model="departmentDraft.require2FA" type="checkbox" />
                <span>{{ t('Require 2FA', 'Bắt buộc 2FA') }}</span>
              </label>
              <button type="button" class="primary-btn" @click="createDepartmentFromDraft">
                <i class="fa-solid fa-plus"></i>
                {{ t('Add department', 'Thêm phòng ban') }}
              </button>
            </div>

            <div v-if="!selectedDepartmentView && filteredDepartments.length" class="department-list grid-list">
              <div v-for="department in paginatedDepartments" :key="department.id" class="department-item" style="cursor: pointer;" @click="selectedDepartmentView = department.id; departmentPage = 1">
                <div class="department-copy">
                  <strong>{{ department.name }}</strong>
                  <span>{{ department.memberCount || 0 }} {{ t('members', 'thành viên') }}</span>
                </div>
                <div class="department-actions" @click.stop>
                  <label class="inline-check">
                    <input :checked="department.isActive" type="checkbox" @change="toggleDepartmentState(department, 'isActive', $event.target.checked)" />
                    <span>{{ t('Active', 'Hoạt động') }}</span>
                  </label>
                  <label class="inline-check">
                    <input :checked="department.require2FA" type="checkbox" @change="toggleDepartmentState(department, 'require2FA', $event.target.checked)" />
                    <span>{{ t('2FA', '2FA') }}</span>
                  </label>
                  <button type="button" class="plain-action" @click="renameDepartment(department)">{{ t('Rename', 'Đổi tên') }}</button>
                  <button type="button" class="plain-action danger-action" @click="removeDepartment(department)">{{ t('Remove', 'Xóa') }}</button>
                </div>
              </div>
            </div>

            <div v-else-if="selectedDepartmentView" class="assignment-list grid-list" style="display: flex; flex-direction: column; gap: 16px;">
              <div class="view-header" style="display: flex; align-items: center; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.1); padding-bottom: 12px; margin-bottom: 4px;">
                <strong>{{ t('Department', 'Phòng ban') }}: {{ activeDepartmentName }}</strong>
                <div style="display: flex; gap: 16px; align-items: center;">
                  <div class="table-search" style="margin-top: 0; min-width: 250px;">
                    <i class="fa-solid fa-magnifying-glass search-icon"></i>
                    <input v-model="departmentMemberSearchQuery" type="text" :placeholder="t('Search by name or email', 'Tìm theo tên hoặc email...')" class="search-input" />
                  </div>
                  <el-select v-model="memberToAdd" filterable :placeholder="t('Add member', 'Thêm nhân sự')" @change="handleAddMemberToDepartment" style="width: 200px;" popper-class="admin-project-dropdown">
                    <el-option v-for="user in usersNotInActiveDepartment" :key="user.id" :label="getDisplayName(user)" :value="user.id">
                      <span>{{ getDisplayName(user) }} ({{ user.email }})</span>
                    </el-option>
                  </el-select>
                  <button type="button" class="accent-back-btn" @click="selectedDepartmentView = null; departmentPage = 1">
                    <i class="fa-solid fa-arrow-left"></i> {{ t('Back', 'Quay lại') }}
                  </button>
                </div>
              </div>
              
              <div v-for="member in paginatedDepartmentMembers" :key="member.id" class="assignment-item" style="grid-column: 1 / -1;">
                <div class="assignment-copy" style="display: flex; align-items: center; gap: 16px;">
                  <UserAvatar :user="member" :size="32" :fontSize="14" class="user-avatar" style="margin-right: 0;" />
                  <div>
                    <strong>{{ getDisplayName(member) }}</strong>
                    <span style="display: block; font-size: 13px; color: var(--color-text-muted);">{{ member.email }}</span>
                  </div>
                </div>
                <button type="button" class="plain-action danger-action" @click="handleRemoveMemberFromDepartment(member)">{{ t('Remove', 'Xóa') }}</button>
              </div>
              
              <div v-if="!paginatedDepartmentMembers.length" style="grid-column: 1 / -1; text-align: center; color: var(--color-text-muted); margin-top: 16px;">
                {{ t('No members found.', 'Không tìm thấy nhân sự nào.') }}
              </div>
            </div>

            <div v-if="!selectedDepartmentView && filteredDepartments.length" class="pagination-controls">
              <el-pagination layout="prev, pager, next, jumper" :total="filteredDepartments.length" :page-size="10" v-model:current-page="departmentPage" />
            </div>
            <div v-else-if="selectedDepartmentView && activeDepartmentMembers.length" class="pagination-controls">
              <el-pagination layout="prev, pager, next, jumper" :total="activeDepartmentMembers.length" :page-size="10" v-model:current-page="departmentPage" />
            </div>
            <div v-else-if="!selectedDepartmentView" class="table-state compact-state">
              <span>{{ t('No departments found.', 'Không có phòng ban nào.') }}</span>
            </div>
          </section>
          </el-tab-pane>

          <el-tab-pane :label="t('Project role mapping', 'Phân vai trò theo dự án')" name="project-roles">
              <section class="admin-subcard">
              <div class="subcard-header" style="display: flex; justify-content: space-between; align-items: center;">
                <div>
                  <h3 class="text-section" style="margin-bottom: 4px;">{{ t('Project role mapping', 'Phân vai trò theo dự án') }}</h3>
                  <p class="text-desc" style="margin: 0;">{{ t('Assign a role for each department inside a project.', 'Gán vai trò của từng phòng ban trong từng dự án.') }}</p>
                </div>
                <div class="table-search" v-if="!selectedProjectView" style="margin-top: 0;">
                  <i class="fa-solid fa-magnifying-glass search-icon"></i>
                  <input v-model="projectRoleSearchQuery" type="text" :placeholder="t('Search projects', 'Tìm kiếm dự án...')" class="search-input" />
                </div>
              </div>

            <div class="assignment-grid row-form">
              <el-select v-model="departmentRoleDraft.projectIds" multiple collapse-tags clearable :placeholder="t('Project', 'Dự án')" popper-class="admin-project-dropdown" style="flex: 1;">
                <el-option v-for="project in availableProjects" :key="project.id" :label="project.name" :value="project.id"></el-option>
              </el-select>
              <el-select v-model="departmentRoleDraft.departmentIds" multiple collapse-tags clearable :placeholder="t('Department', 'Phòng ban')" popper-class="admin-project-dropdown" style="flex: 1;">
                <el-option v-for="department in departments" :key="department.id" :label="department.name" :value="department.id"></el-option>
              </el-select>
              <input v-model="departmentRoleDraft.roleName" type="text" :placeholder="t('Role name', 'Tên vai trò')" style="flex: 1;" />
              <button type="button" class="primary-btn" @click="saveDepartmentProjectRole">
                <i class="fa-solid fa-floppy-disk"></i>
                {{ t('Save mapping', 'Lưu mapping') }}
              </button>
            </div>

            <div v-if="!selectedProjectView && groupedProjectRoles.length" class="assignment-list grid-list">
              <div v-for="group in paginatedGroupedProjects" :key="group.projectId" class="assignment-item">
                <div class="assignment-copy">
                  <strong>{{ group.projectName }}</strong>
                  <span>{{ group.assignments.length }} {{ t('mappings', 'phân quyền') }}</span>
                </div>
                <button type="button" class="plain-action" @click="selectedProjectView = group.projectId; projectRolePage = 1">{{ t('View details', 'Xem chi tiết') }}</button>
              </div>
            </div>
            <div v-else-if="selectedProjectView" class="assignment-list grid-list" style="display: flex; flex-direction: column; gap: 16px;">
              <div class="view-header" style="display: flex; align-items: center; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.1); padding-bottom: 12px; margin-bottom: 4px;">
                <strong>{{ t('Project', 'Dự án') }}: {{ activeProjectName }}</strong>
                <div style="display: flex; gap: 16px; align-items: center;">
                  <div class="table-search" style="margin-top: 0;">
                    <i class="fa-solid fa-magnifying-glass search-icon"></i>
                    <input v-model="projectDepartmentSearchQuery" type="text" :placeholder="t('Search departments', 'Tìm phòng ban...')" class="search-input" />
                  </div>
                  <button type="button" class="accent-back-btn" @click="selectedProjectView = null; projectRolePage = 1">
                    <i class="fa-solid fa-arrow-left"></i> {{ t('Back', 'Quay lại') }}
                  </button>
                </div>
              </div>
              <div v-for="assignment in paginatedAssignmentsInProject" :key="`${assignment.projectId}-${assignment.departmentId}-${assignment.roleName}`" class="assignment-item" style="cursor: pointer; grid-column: 1 / -1;" @click="viewDepartmentMembersFromProject(assignment.departmentId)">
                <div class="assignment-copy">
                  <strong>{{ assignment.departmentName }}</strong>
                  <span>{{ t('Role', 'Vai trò') }}: {{ assignment.roleName }}</span>
                </div>
                <button type="button" class="plain-action danger-action" @click.stop="removeDepartmentProjectRole(assignment)">{{ t('Remove', 'Xóa') }}</button>
              </div>
              <div v-if="!paginatedAssignmentsInProject.length" style="grid-column: 1 / -1; text-align: center; color: var(--color-text-muted); margin-top: 16px;">
                {{ t('No mappings found.', 'Không có phân quyền nào.') }}
              </div>
            </div>
            <div v-else-if="!selectedProjectView" class="table-state compact-state" style="flex: 1;">
              <span>{{ t('No project role mappings yet.', 'Chưa có mapping vai trò theo dự án.') }}</span>
            </div>

            <div v-if="!selectedProjectView && groupedProjectRoles.length" class="pagination-controls">
              <el-pagination layout="prev, pager, next, jumper" :total="groupedProjectRoles.length" :page-size="10" v-model:current-page="projectRolePage" />
            </div>
            <div v-else-if="selectedProjectView && activeProjectAssignments.length" class="pagination-controls">
              <el-pagination layout="prev, pager, next, jumper" :total="activeProjectAssignments.length" :page-size="10" v-model:current-page="projectRolePage" />
            </div>
          </section>
          </el-tab-pane>

          <section class="admin-subcard" v-if="false">
            <div class="subcard-header">
              <div>
                <h3>{{ t('System roles', 'Vai trò hệ thống') }}</h3>
                <p>{{ t('Create custom roles, keep Admin and PM protected from structural edits, and assign roles to users.', 'Tạo vai trò tùy chỉnh, khóa cấu trúc của Admin và PM, và cấp vai trò cho người dùng.') }}</p>
              </div>
              <div class="department-actions">
                <button type="button" class="neutral-btn" @click="router.push('/admin/roles')">
                  <i class="fa-solid fa-arrow-up-right-from-square"></i>
                  {{ t('Open role management', 'Mở quản lý vai trò') }}
                </button>
                <button type="button" class="primary-btn" @click="saveRoleDraft">
                  <i class="fa-solid fa-shield-halved"></i>
                  {{ editingRoleId ? t('Update role', 'Cập nhật vai trò') : t('Add role', 'Thêm vai trò') }}
                </button>
              </div>
            </div>

            <div class="assignment-grid">
              <input v-model="roleDraft.name" type="text" :placeholder="t('Role name', 'Tên vai trò')" />
              <input v-model="roleDraft.description" type="text" :placeholder="t('Description', 'Mô tả')" />
              <el-select
                v-model="roleDraft.permissionIds"
                multiple
                collapse-tags
                collapse-tags-tooltip
                :placeholder="t('Permissions', 'Quyền')"
                popper-class="admin-project-dropdown"
              >
                <el-option
                  v-for="permission in permissions"
                  :key="permission.id"
                  :label="`${permission.module} / ${permission.code}`"
                  :value="permission.id"
                ></el-option>
              </el-select>
            </div>

            <div class="assignment-grid role-assignment-grid">
              <el-select
                v-model="roleAssignment.userId"
                clearable
                filterable
                :placeholder="t('Select user', 'Chọn người dùng')"
                popper-class="admin-project-dropdown"
              >
                <el-option
                  v-for="user in users"
                  :key="user.id"
                  :label="`${getDisplayName(user)} (${user.email})`"
                  :value="user.id"
                ></el-option>
              </el-select>
              <el-select
                v-model="roleAssignment.roleIds"
                multiple
                collapse-tags
                collapse-tags-tooltip
                :placeholder="t('Assign roles', 'Cấp vai trò')"
                popper-class="admin-project-dropdown"
              >
                <el-option
                  v-for="role in roles"
                  :key="role.id"
                  :label="role.name"
                  :value="role.id"
                ></el-option>
              </el-select>
              <button type="button" class="secondary-btn" @click="assignRolesToSelectedUser">
                {{ t('Assign roles', 'Cấp vai trò') }}
              </button>
            </div>

            <div v-if="roles.length" class="assignment-list">
              <div v-for="role in roles" :key="role.id" class="assignment-item role-item">
                <div class="assignment-copy">
                  <strong>{{ role.name }}</strong>
                  <span>{{ role.description || t('No description', 'Chưa có mô tả') }}</span>
                  <small>{{ role.memberCount }} {{ t('members', 'thành viên') }} · {{ (role.permissions || []).length }} {{ t('permissions', 'quyền') }}</small>
                </div>
                <div class="department-actions">
                  <button type="button" class="plain-action" @click="editRole(role)">{{ t('Edit', 'Sửa') }}</button>
                  <button
                    type="button"
                    class="plain-action danger-action"
                    :disabled="role.isProtected"
                    @click="removeRole(role)"
                  >
                    {{ role.isProtected ? t('Protected', 'Được khóa') : t('Delete', 'Xóa') }}
                  </button>
                </div>
              </div>
            </div>
            <div v-else class="table-state compact-state">
              <span>{{ t('No system roles loaded yet.', 'Chưa tải được vai trò hệ thống.') }}</span>
            </div>
          </section>

          <el-tab-pane :label="t('Members list', 'Danh sách thành viên')" name="members">
            <section class="filters-row" aria-label="User filters" style="margin-top: 16px;">
          <div class="table-search">
            <i class="fa-solid fa-magnifying-glass"></i>
            <input
              v-model="searchQuery"
              type="text"
              :placeholder="t('Search by name or email', 'Tìm theo tên hoặc email')"
              @input="debounceSearch"
            />
          </div>

          <div class="filter-wrap" @click.stop>
            <button
              type="button"
              class="filter-button"
              :class="{ 'is-open': openFilterMenu === 'role', 'has-value': roleFilterValues.length }"
              @click="toggleFilterMenu('role')"
            >
              {{ t('Role', 'Vai trò') }}
              <i class="fa-solid fa-chevron-down"></i>
            </button>
            <div v-if="openFilterMenu === 'role'" class="filter-menu role-filter-menu">
              <div class="filter-search">
                <input v-model="filterSearch.role" type="text" :placeholder="t('Search', 'Tìm kiếm')" />
                <i class="fa-solid fa-magnifying-glass"></i>
              </div>
              <label v-for="option in visibleRoleFilterOptions" :key="option.value" class="check-option">
                <input v-model="roleFilterValues" type="checkbox" :value="option.value" />
                <span>{{ option.label }}</span>
              </label>
              <div class="filter-menu-footer">{{ roleFilterOptions.length }} of {{ roleFilterOptions.length }}</div>
            </div>
          </div>

          <!-- <div class="filter-wrap" @click.stop>
            <button
              type="button"
              class="filter-button"
              :class="{ 'is-open': openFilterMenu === 'apps', 'has-value': appFilterValues.length }"
              @click="toggleFilterMenu('apps')"
            >
              {{ t('Apps', 'Ứng dụng') }}
              <i class="fa-solid fa-chevron-down"></i>
            </button>
            <div v-if="openFilterMenu === 'apps'" class="filter-menu apps-filter-menu">
              <div class="filter-search">
                <input v-model="filterSearch.apps" type="text" :placeholder="t('Search', 'Tìm kiếm')" />
                <i class="fa-solid fa-magnifying-glass"></i>
              </div>
              <p class="filter-org-label">{{ organizationHandle }}</p>
              <label v-for="option in visibleAppFilterOptions" :key="option.value" class="app-check-option">
                <input v-model="appFilterValues" type="checkbox" :value="option.value" />
                <span class="mini-app-icon" :class="option.tone">
                  <i class="fa-solid" :class="option.icon"></i>
                </span>
                <span>
                  <strong>{{ option.label }}</strong>
                  <small>{{ organizationHandle }}</small>
                </span>
              </label>
              <div class="filter-menu-footer">{{ appFilterOptions.length }} of {{ appFilterOptions.length }}</div>
            </div>
          </div> -->

          <div class="filter-wrap" @click.stop>
            <button
              type="button"
              class="filter-button"
              :class="{ 'is-open': openFilterMenu === 'status', 'has-value': statusFilterValues.length }"
              @click="toggleFilterMenu('status')"
            >
              {{ t('Status', 'Trạng thái') }}
              <i class="fa-solid fa-chevron-down"></i>
            </button>
            <div v-if="openFilterMenu === 'status'" class="filter-menu status-filter-menu">
              <div class="filter-search">
                <input v-model="filterSearch.status" type="text" :placeholder="t('Search', 'Tìm kiếm')" />
                <i class="fa-solid fa-magnifying-glass"></i>
              </div>
              <label v-for="option in visibleStatusFilterOptions" :key="option.value" class="check-option">
                <input v-model="statusFilterValues" type="checkbox" :value="option.value" />
                <span class="status-sample" :class="option.className">{{ option.label }}</span>
              </label>
              <div class="filter-menu-footer">{{ statusFilterOptions.length }} of {{ statusFilterOptions.length }}</div>
            </div>
          </div>

          <div class="filter-wrap" @click.stop>
            <el-select
              v-model="memberListProjectFilter"
              clearable
              filterable
              :placeholder="t('Project', 'Dự án')"
              class="custom-filter-select"
              popper-class="admin-project-dropdown"
            >
              <el-option
                v-for="project in availableProjects"
                :key="project.id"
                :label="project.name"
                :value="project.name"
              ></el-option>
            </el-select>
          </div>

          <div class="filter-wrap" @click.stop>
            <el-select
              v-model="memberListDepartmentFilter"
              clearable
              filterable
              :placeholder="t('Department', 'Phòng ban')"
              class="custom-filter-select"
              popper-class="admin-project-dropdown"
            >
              <el-option
                v-for="dept in departments"
                :key="dept.id"
                :label="dept.name"
                :value="dept.name"
              ></el-option>
            </el-select>
          </div>

          <button v-if="hasActiveFilters" type="button" class="clear-filters-btn" @click="clearFilters">
            {{ t('Clear filters', 'Xóa bộ lọc') }}
          </button>
        </section>

        <div class="results-label">
          {{ t('Showing results', 'Hiển thị kết quả') }}
          <i v-if="loading" class="fa-solid fa-spinner fa-spin"></i>
        </div>

        <section class="users-table-shell">
          <div v-if="loading && !users.length" class="table-state">
            <i class="fa-solid fa-spinner fa-spin"></i>
            <span>{{ t('Loading users...', 'Đang tải người dùng...') }}</span>
          </div>

          <div v-else-if="!filteredUsers.length" class="table-state">
            <i class="fa-regular fa-user"></i>
            <span>{{ t('No users match these filters.', 'Không có người dùng nào phù hợp.') }}</span>
            <button type="button" class="primary-btn" @click.stop="openInvitePanel">{{ t('Invite users', 'Mời người dùng') }}</button>
          </div>

          <table v-else class="users-table">
            <thead>
              <tr>
                <th>{{ t('User', 'Người dùng') }}</th>
                <th>{{ t('Status', 'Trạng thái') }}</th>
                <th>{{ t('Last seen', 'Lần cuối truy cập') }}</th>
                <th class="actions-heading">{{ t('Actions', 'Thao tác') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="user in filteredUsers" :key="user.id || user.email">
                <td>
                  <div class="user-cell">
                    <UserAvatar :user="user" :size="32" :fontSize="14" class="user-avatar" />
                    <div class="user-copy">
                      <strong>{{ getDisplayName(user) }}</strong>
                      <span>{{ user.email }} <template v-if="isOrganizationAdmin(user)">- {{ t('Organization admin', 'Quản trị viên') }}</template></span>
                    </div>
                  </div>
                </td>
                <td>
                  <span class="table-status-badge" :class="getStatusMeta(user).className">
                    {{ getStatusMeta(user).label }}
                  </span>
                </td>
                <td class="last-seen-cell">{{ getLastSeenText(user) }}</td>
                <td class="actions-cell">
                  <div class="menu-wrap row-menu-wrap" @click.stop>
                    <button
                      type="button"
                      class="row-more-btn"
                      :class="{ 'is-open': openRowMenu === (user.id || user.email) }"
                      @click="toggleRowMenu(user)"
                      aria-label="User actions"
                    >
                      <i class="fa-solid fa-ellipsis"></i>
                    </button>
                    <div v-if="openRowMenu === (user.id || user.email)" class="action-menu row-action-menu">
                      <button v-if="getStatusMeta(user).value === 'invited'" type="button" @click="resendInvite(user)">
                        {{ t('Resend invitation', 'Gửi lại lời mời') }}
                      </button>
                      <button type="button" @click="showAddGroupHint(user)">{{ t('Add to project', 'Thêm vào dự án') }}</button>
                      <button v-if="user.isActive" type="button" @click="handleSuspendUser(user)">
                        {{ t('Suspend access', 'Tạm ngưng truy cập') }}
                      </button>
                      <button type="button" class="danger-action" @click="handleRemoveUser(user)">
                        {{ t('Remove user', 'Xóa người dùng') }}
                      </button>
                    </div>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </section>
          </el-tab-pane>
        </el-tabs>
      </section>
    </div>

    <section v-if="showInvitePanel" class="invite-panel">
      <button class="close-invite" type="button" aria-label="Close invite panel" @click="closeInvitePanel">
        <i class="fa-solid fa-xmark"></i>
      </button>

      <div class="invite-shell" @click.stop="closeInviteFloatingMenus">
        <div class="invite-copy">
          <h2>{{ t(`Invite people to ${organizationHandle}`, `Mời người đến ${organizationHandle}`) }}</h2>
          <p>
            {{ t('Invite teammates to collaborate and use apps in your organization. We\'ll ask new users to enter their personal details when they sign up.', 'Mời đồng đội cộng tác và sử dụng ứng dụng trong tổ chức của bạn. Chúng tôi sẽ yêu cầu người dùng mới nhập thông tin cá nhân khi đăng ký.') }}
          </p>
        </div>

        <div class="invite-section">
          <label class="field-label" for="invite-emails">{{ t('Email addresses', 'Địa chỉ email') }}</label>
          <div class="email-composer" :class="{ 'is-focused': emailInputFocused }" @click.stop>
            <span v-for="email in inviteEmails" :key="email" class="email-chip">
              {{ email }}
              <button type="button" @click="removeInviteEmail(email)">
                <i class="fa-solid fa-xmark"></i>
              </button>
            </span>
            <input
              id="invite-emails"
              v-model="inviteDraft"
              type="text"
              placeholder="name@company.com"
              @focus="emailInputFocused = true"
              @blur="handleEmailBlur"
              @keydown.enter.prevent="addDraftEmails"
              @paste="handleEmailPaste"
            />
          </div>
          <p class="hint">{{ t('Separate emails using a comma. Note, we can\'t send invitations to distribution lists.', 'Phân cách email bằng dấu phẩy. Lưu ý, chúng tôi không thể gửi lời mời đến danh sách phân phối.') }}</p>
        </div>

        <!-- <div class="access-table" v-show="false">
          <div class="access-header">
            <span>{{ t('App', 'Ứng dụng') }}</span>
            <span>{{ t('Plan', 'Gói') }}</span>
            <span>{{ t('Roles', 'Vai trò') }}</span>
            <button type="button" @click="clearAppAccess">{{ t('Unselect all', 'Bỏ chọn tất cả') }}</button>
          </div>

          <div v-for="row in appAccessRows" :key="row.key" class="access-row">
            <div class="app-cell">
              <div class="app-icon" :class="row.tone">
                <i class="fa-solid" :class="row.icon"></i>
              </div>
              <div>
                <strong>{{ row.name }}</strong>
                <span>{{ organizationHandle }}</span>
              </div>
            </div>
            <span class="plan-cell">{{ row.plan }}</span>
            <div class="role-picker" @click.stop>
              <button
                type="button"
                class="role-trigger"
                :class="{ 'is-open': openRoleMenu === row.key }"
                @click="toggleRoleMenu(row.key)"
              >
                <span>{{ getSelectedAppOption(row)?.label || 'None' }}</span>
                <i class="fa-solid fa-chevron-down"></i>
              </button>

              <div v-if="openRoleMenu === row.key" class="role-menu">
                <button
                  v-for="option in row.options"
                  :key="option.value"
                  type="button"
                  class="role-option"
                  :class="{ 'is-selected': inviteForm.apps[row.key] === option.value }"
                  @click="selectAppRole(row.key, option.value)"
                >
                  <span class="check-box">
                    <i v-if="inviteForm.apps[row.key] === option.value" class="fa-solid fa-check"></i>
                  </span>
                  <span class="role-option-copy">
                    <strong>{{ option.label }}</strong>
                    <small>{{ option.description }}</small>
                  </span>
                </button>
                <div class="role-menu-footer">
                  <button type="button" @click="selectAppRole(row.key, 'None')">{{ t('Unselect all', 'Bỏ chọn tất cả') }}</button>
                </div>
              </div>
            </div>
          </div>
        </div> -->

        <!-- <div class="invite-section" v-show="false">
          <label class="field-label">{{ t('Group membership', 'Thành viên nhóm') }}</label>
          <div class="group-composer" :class="{ 'is-focused': groupInputFocused }" @click.stop>
            <span v-for="group in selectedGroups" :key="group.name" class="group-chip">
              {{ group.name }}
              <button type="button" @click="removeGroup(group.name)">
                <i class="fa-solid fa-xmark"></i>
              </button>
            </span>
            <input
              v-model="groupDraft"
              type="text"
              :placeholder="t('Add groups', 'Thêm nhóm')"
              @focus="groupInputFocused = true"
              @keydown.enter.prevent="addFirstMatchingGroup"
              @keydown.esc.prevent="groupInputFocused = false"
            />
          </div>

          <div v-if="groupInputFocused" class="group-suggestions" @click.stop>
            <button
              v-for="group in filteredGroupSuggestions"
              :key="group.name"
              type="button"
              class="group-option"
              @click="addGroup(group)"
            >
              <strong>{{ group.name }}</strong>
              <span>{{ group.description }}</span>
            </button>
          </div>
          <p class="hint">{{ t('Customized groups give users access to specific projects or spaces.', 'Nhóm tùy chỉnh cho phép người dùng truy cập vào các dự án hoặc không gian cụ thể.') }}</p>
        </div> -->

        <div class="invite-section">
          <label class="field-label">{{ t('Project membership', 'Thành viên dự án') }}</label>
          <div class="project-grid">
            <el-select
              v-model="inviteForm.projectId"
              class="project-select"
              clearable
              filterable
              :placeholder="t('Add to project', 'Thêm vào dự án')"
              popper-class="admin-project-dropdown"
            >
              <el-option
                v-for="project in projectsList"
                :key="project.id"
                :label="project.name"
                :value="project.id"
              ></el-option>
            </el-select>

            <el-select v-model="inviteForm.projectRole" class="project-role-select" popper-class="admin-project-dropdown">
              <el-option
                v-for="role in projectRoleOptions"
                :key="role"
                :label="role"
                :value="role"
              ></el-option>
            </el-select>
          </div>
          <p class="hint">{{ t('If a project is selected, the invited user will be added to it after accepting.', 'Nếu chọn dự án, người được mời sẽ được thêm vào sau khi chấp nhận.') }}</p>
        </div>

        <div class="invite-section">
          <button class="collapse-trigger" type="button" @click="showPersonalMessage = !showPersonalMessage">
            <i class="fa-solid" :class="showPersonalMessage ? 'fa-chevron-down' : 'fa-chevron-right'"></i>
            {{ t('Personalize invitation email', 'Tùy chỉnh email mời') }}
          </button>

          <el-input
            v-if="showPersonalMessage"
            v-model="inviteForm.message"
            type="textarea"
            :rows="4"
            maxlength="300"
            show-word-limit
            :placeholder="t('Add a short message for the invite email...', 'Thêm lời nhắn ngắn cho email mời...')"
          />
        </div>

        <div class="invite-footer">
          <div>
            <!-- <strong v-show="false">{{ selectedAppsCount }} {{ t('apps selected', 'ứng dụng đã chọn') }}</strong> -->
            <p>{{ t('The invite email will include an accept link for the selected people.', 'Email mời sẽ bao gồm liên kết chấp nhận cho những người được chọn.') }}</p>
          </div>

          <div class="footer-actions">
            <button type="button" class="plain-action" @click="closeInvitePanel">{{ t('Cancel', 'Hủy') }}</button>
            <button
              type="button"
              class="primary-btn"
              :disabled="!canSendInvite"
              @click="submitInvite"
            >
              <i v-if="isSubmitting" class="fa-solid fa-spinner fa-spin"></i>
              <span>{{ t('Send invite', 'Gửi lời mời') }}</span>
            </button>
          </div>
        </div>
      </div>
    </section>

    <Teleport to="body">
    <div v-if="showExportModal" class="modal-backdrop sa-data-modal-overlay" @click.stop="closeExportModal">
      <section class="export-modal" @click.stop>
        <DataModalHeader
          icon="bi bi-filetype-csv"
          :title="t('Export users to CSV', 'Xuất người dùng ra CSV')"
          :description="t('Select the data you want to export from the users currently loaded here.', 'Chọn dữ liệu bạn muốn xuất từ danh sách người dùng hiện tại.')"
          @close="closeExportModal"
        />

        <div class="export-modal-body">
          <DataModalSection icon="bi bi-people" :title="t('Users', 'Người dùng')">
            <div class="export-group">
              <label><input v-model="exportUserScope" type="radio" value="all" /> {{ t('All users', 'Tất cả người dùng') }}</label>
              <label><input v-model="exportUserScope" type="radio" value="filtered" /> {{ t('Only users in selected filters', 'Chỉ người dùng trong bộ lọc') }}</label>
            </div>
          </DataModalSection>

          <DataModalSection icon="bi bi-activity" :title="t('User status', 'Trạng thái')">
            <div class="export-group">
              <label><input v-model="exportStatusScope" type="radio" value="all" /> {{ t('All users', 'Tất cả người dùng') }}</label>
              <label><input v-model="exportStatusScope" type="radio" value="active" /> {{ t('Only active users', 'Chỉ người dùng đang hoạt động') }}</label>
            </div>
          </DataModalSection>

          <DataModalSection icon="bi bi-database-add" :title="t('Additional data', 'Dữ liệu bổ sung')">
            <div class="export-group">
              <label><input v-model="exportAdditionalData" type="checkbox" value="groups" /> {{ t('Group membership', 'Thành viên nhóm') }}</label>
              <label><input v-model="exportAdditionalData" type="checkbox" value="apps" /> {{ t('App access and role', 'Quyền truy cập ứng dụng và vai trò') }}</label>
            </div>
          </DataModalSection>
        </div>

        <div class="modal-actions">
          <button type="button" class="plain-action" @click="closeExportModal">
            <i class="bi bi-x-lg"></i>
            {{ t('Cancel', 'Hủy') }}
          </button>
          <button type="button" class="primary-btn" @click="exportUsersToCsv">
            <i class="bi bi-download"></i>
            {{ t('Export', 'Xuất') }}
          </button>
        </div>
      </section>
    </div>
    </Teleport>
  </AdminLayout>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { storeToRefs } from 'pinia'
import { ElMessage, ElMessageBox } from 'element-plus'
import AdminLayout from '@/components/layout/AdminLayout.vue'
import axiosClient from '@/api/axiosClient'
import { useAdminUserStore } from '@/store/useAdminUserStore'
import { useLocale } from '@/composables/useLocale'
import { onUnmounted } from 'vue'
import UserAvatar from '@/components/common/UserAvatar.vue'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'

const { t, locale: currentLocale } = useLocale()
const activeTab = ref('departments')
const nowTime = ref(Date.now())

const adminUserStore = useAdminUserStore()
const { users, loading, departments, projectRoleAssignments, availableProjects, roles, permissions } = storeToRefs(adminUserStore)

const departmentSearchQuery = ref('')
const selectedDepartmentView = ref(null)
const departmentMemberSearchQuery = ref('')
const memberToAdd = ref(null)

const projectRoleSearchQuery = ref('')
const projectDepartmentSearchQuery = ref('')

const memberListProjectFilter = ref('')
const memberListDepartmentFilter = ref('')

const filteredDepartments = computed(() => {
  if (!departmentSearchQuery.value) return departments.value
  const q = departmentSearchQuery.value.toLowerCase()
  return departments.value.filter(d => d.name.toLowerCase().includes(q))
})

const departmentPage = ref(1)
const departmentPageSize = 10
const paginatedDepartments = computed(() => {
  const start = (departmentPage.value - 1) * departmentPageSize
  return filteredDepartments.value.slice(start, start + departmentPageSize)
})

const activeDepartmentName = computed(() => {
  if (!selectedDepartmentView.value) return ''
  const dept = departments.value.find(d => d.id === selectedDepartmentView.value)
  return dept ? dept.name : ''
})

const activeDepartmentMembers = computed(() => {
  if (!selectedDepartmentView.value) return []
  const deptName = activeDepartmentName.value
  if (!deptName) return []
  let members = users.value.filter(user => (user.departments || []).includes(deptName))
  
  if (departmentMemberSearchQuery.value) {
    const q = departmentMemberSearchQuery.value.toLowerCase()
    members = members.filter(user => 
      getDisplayName(user).toLowerCase().includes(q) || 
      (user.email || '').toLowerCase().includes(q)
    )
  }
  return members
})

const paginatedDepartmentMembers = computed(() => {
  const start = (departmentPage.value - 1) * departmentPageSize
  return activeDepartmentMembers.value.slice(start, start + departmentPageSize)
})

const usersNotInActiveDepartment = computed(() => {
  if (!selectedDepartmentView.value) return []
  const deptName = activeDepartmentName.value
  return users.value.filter(user => !(user.departments || []).includes(deptName))
})

const handleAddMemberToDepartment = async (userId) => {
  if (!userId || !selectedDepartmentView.value) return
  try {
    await adminUserStore.addDepartmentMember(selectedDepartmentView.value, userId)
    ElMessage.success(t('Added member to department', 'Đã thêm nhân sự vào phòng ban'))
    memberToAdd.value = null
  } catch (err) {
    ElMessage.error(t('Failed to add member', 'Không thể thêm nhân sự'))
  }
}

const handleRemoveMemberFromDepartment = async (user) => {
  try {
    await adminUserStore.removeDepartmentMember(selectedDepartmentView.value, user.id)
    ElMessage.success(t('Removed member from department', 'Đã xóa nhân sự khỏi phòng ban'))
  } catch (err) {
    ElMessage.error(t('Failed to remove member', 'Không thể xóa nhân sự'))
  }
}

const projectRolePage = ref(1)
const projectRolePageSize = 10
const selectedProjectView = ref(null)

const groupedProjectRoles = computed(() => {
  const map = new Map()
  for (const assignment of projectRoleAssignments.value) {
    if (!map.has(assignment.projectId)) {
      map.set(assignment.projectId, {
        projectId: assignment.projectId,
        projectName: assignment.projectName,
        assignments: []
      })
    }
    map.get(assignment.projectId).assignments.push(assignment)
  }
  return Array.from(map.values())
})

const activeProjectName = computed(() => {
  if (!selectedProjectView.value) return ''
  const proj = groupedProjectRoles.value.find(p => p.projectId === selectedProjectView.value)
  return proj ? proj.projectName : ''
})

const activeProjectAssignments = computed(() => {
  if (!selectedProjectView.value) return []
  const proj = groupedProjectRoles.value.find(p => p.projectId === selectedProjectView.value)
  return proj ? proj.assignments : []
})

const filteredGroupedProjectRoles = computed(() => {
  let groups = groupedProjectRoles.value
  if (projectRoleSearchQuery.value) {
    const q = projectRoleSearchQuery.value.toLowerCase()
    groups = groups.filter(g => g.projectName.toLowerCase().includes(q) || g.projectId.toLowerCase().includes(q))
  }
  return groups
})

const filteredActiveProjectAssignments = computed(() => {
  let assignments = activeProjectAssignments.value
  if (projectDepartmentSearchQuery.value) {
    const q = projectDepartmentSearchQuery.value.toLowerCase()
    assignments = assignments.filter(a => a.departmentName.toLowerCase().includes(q))
  }
  return assignments
})

const paginatedGroupedProjects = computed(() => {
  const start = (projectRolePage.value - 1) * projectRolePageSize
  return filteredGroupedProjectRoles.value.slice(start, start + projectRolePageSize)
})

const paginatedAssignmentsInProject = computed(() => {
  const start = (projectRolePage.value - 1) * projectRolePageSize
  return filteredActiveProjectAssignments.value.slice(start, start + projectRolePageSize)
})

const viewDepartmentMembersFromProject = (departmentId) => {
  activeTab.value = 'departments'
  selectedDepartmentView.value = departmentId
  departmentPage.value = 1
}

watch([departments, departmentSearchQuery, selectedDepartmentView, departmentMemberSearchQuery], () => {
  const source = selectedDepartmentView.value ? activeDepartmentMembers.value : filteredDepartments.value
  const maxPage = Math.ceil(source.length / departmentPageSize) || 1
  if (departmentPage.value > maxPage) departmentPage.value = maxPage
})

watch([projectRoleAssignments, projectRoleSearchQuery, projectDepartmentSearchQuery], () => {
  const source = selectedProjectView.value ? filteredActiveProjectAssignments.value : filteredGroupedProjectRoles.value
  const maxPage = Math.ceil(source.length / projectRolePageSize) || 1
  if (projectRolePage.value > maxPage) projectRolePage.value = maxPage
  if (selectedProjectView.value && source.length === 0 && !projectDepartmentSearchQuery.value) {
    selectedProjectView.value = null
    projectRolePage.value = 1
  }
}, { deep: true })
let timeInterval = null

const fallbackOrganization = {
  organizationName: 'Global Organization',
  domain: 'acme.com',
  logoUrl: ''
}

const organizationProfile = ref({ ...fallbackOrganization })
const showInvitePanel = ref(false)
const showExportModal = ref(false)
const showPersonalMessage = ref(false)
const isSubmitting = ref(false)
const projectsList = ref([])
const departmentDraft = ref({
  name: '',
  isActive: true,
  require2FA: false
})
const departmentRoleDraft = ref({
  projectIds: [],
  departmentIds: [],
  roleName: ''
})
const roleDraft = ref({
  name: '',
  description: '',
  permissionIds: []
})
const roleAssignment = ref({
  userId: null,
  roleIds: []
})
const editingRoleId = ref(null)
const searchQuery = ref('')
const inviteEmails = ref([])
const inviteDraft = ref('')
const emailInputFocused = ref(false)
const openRoleMenu = ref(null)
const groupDraft = ref('')
const groupInputFocused = ref(false)
const selectedGroups = ref([])
const openFilterMenu = ref(null)
const openTopActionMenu = ref(false)
const openRowMenu = ref(null)
const roleFilterValues = ref([])
const appFilterValues = ref([])
const statusFilterValues = ref([])
const filterSearch = ref({ role: '', apps: '', status: '' })
const exportUserScope = ref('all')
const exportStatusScope = ref('active')
const exportAdditionalData = ref([])

const inviteForm = ref({
  projectId: null,
  projectRole: 'Developer',
  message: '',
  apps: {
    goals: 'User',
    work: 'User',
    projects: 'User',
    admin: 'None'
  }
})

const appAccessRows = [
  {
    key: 'goals',
    name: 'Goals',
    plan: 'Free',
    icon: 'fa-bullseye',
    tone: 'tone-neutral',
    options: [
      { label: 'App admin', value: 'AppAdmin', description: 'Can access the app, with app admin permissions' },
      { label: 'User', value: 'User', description: 'Can access the app, with no app admin permissions' },
      { label: 'User access admin', value: 'AccessAdmin', description: 'No app access. Can administer users and groups for this app' },
      { label: 'None', value: 'None', description: 'No access to Goals' }
    ]
  },
  {
    key: 'work',
    name: 'Jira',
    plan: 'Premium',
    icon: 'fa-bolt',
    tone: 'tone-blue',
    options: [
      { label: 'User', value: 'User', description: 'Can access the app, with no app admin permissions' },
      { label: 'User access admin', value: 'AccessAdmin', description: 'No app access. Can administer users and groups for this app in administration' },
      { label: 'None', value: 'None', description: 'No access to Jira' }
    ]
  },
  {
    key: 'projects',
    name: 'Projects',
    plan: 'Free',
    icon: 'fa-rocket',
    tone: 'tone-dark',
    options: [
      { label: 'User access admin', value: 'AccessAdmin', description: 'No app access. Can administer users and groups for Projects' },
      { label: 'App admin', value: 'AppAdmin', description: 'Can access the app, with app admin permissions' },
      { label: 'User', value: 'User', description: 'Can access the app, with no app admin permissions' },
      { label: 'None', value: 'None', description: 'No access to Projects' }
    ]
  },
  {
    key: 'admin',
    name: 'Jira Administration',
    plan: 'Internal',
    icon: 'fa-shield-halved',
    tone: 'tone-blue',
    options: [
      { label: 'None', value: 'None', description: 'No administration access' },
      { label: 'App admin', value: 'AppAdmin', description: 'Can access the administration features for all Jira apps' }
    ]
  }
]

const roleFilterOptions = [
  { value: 'organization-admin', label: 'Organization admin' },
  { value: 'site-admin', label: 'Site admin' },
  { value: 'user-access-admin', label: 'User access admin' },
  { value: 'app-admin', label: 'App admin' },
  { value: 'user', label: 'User' },
  { value: 'guest', label: 'Guest' },
  { value: 'jsm-customer', label: 'Jira Service Management customer' }
]

const appFilterOptions = [
  { value: 'Goals', label: 'Goals', icon: 'fa-bullseye', tone: 'tone-neutral' },
  { value: 'Jira Administration', label: 'Jira Administration', icon: 'fa-bolt', tone: 'tone-blue' },
  { value: 'Jira', label: 'Jira', icon: 'fa-bolt', tone: 'tone-blue' },
  { value: 'Projects', label: 'Projects', icon: 'fa-rocket', tone: 'tone-dark' }
]

const statusFilterOptions = [
  { value: 'active', label: 'ACTIVE', className: 'status-active' },
  { value: 'invited', label: 'INVITED', className: 'status-invited' },
  { value: 'suspended', label: 'SUSPENDED', className: 'status-suspended' },
  { value: 'deactivated', label: 'DEACTIVATED', className: 'status-deactivated' }
]

const avatarColors = ['#579dff', '#c97cf4', '#00b8d9', '#22a06b', '#f5cd47', '#e2483d', '#6e5dc6', '#4bce97']

const organizationHandle = computed(() => makeOrganizationHandle(organizationProfile.value.domain || organizationProfile.value.organizationName))

const activeUsersCount = computed(() => users.value.filter(user => getStatusMeta(user).value === 'active').length)

const adminUsersCount = computed(() => users.value.filter(user => isOrganizationAdmin(user)).length)

const projectRoleOptions = computed(() => {
  const preferredOrder = ['PM', 'PO', 'SM', 'Project Lead', 'Admin', 'Developer', 'QA', 'Accountant']
  const normalizedHidden = new Set([
    'system admin',
    'superadmin',
    'super admin',
    'organization admin',
    'accessadmin',
    'access admin',
    'user',
    'member',
    'guest'
  ])

  const options = roles.value
    .map(role => String(role?.name || '').trim())
    .filter(Boolean)
    .filter(role => !normalizedHidden.has(role.toLowerCase()))

  const uniqueOptions = [...new Set(options)]

  return uniqueOptions.sort((left, right) => {
    const leftIndex = preferredOrder.findIndex(item => item.toLowerCase() === left.toLowerCase())
    const rightIndex = preferredOrder.findIndex(item => item.toLowerCase() === right.toLowerCase())
    const normalizedLeftIndex = leftIndex === -1 ? Number.MAX_SAFE_INTEGER : leftIndex
    const normalizedRightIndex = rightIndex === -1 ? Number.MAX_SAFE_INTEGER : rightIndex

    if (normalizedLeftIndex !== normalizedRightIndex) {
      return normalizedLeftIndex - normalizedRightIndex
    }

    return left.localeCompare(right)
  })
})

const preferredProjectRole = computed(() => {
  if (projectRoleOptions.value.includes('Developer')) return 'Developer'
  if (projectRoleOptions.value.includes('QA')) return 'QA'
  return projectRoleOptions.value[0] || 'Developer'
})

const filteredUsers = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()

  return users.value.filter(user => {
    const status = getStatusMeta(user).value
    const displayName = getDisplayName(user).toLowerCase()
    const email = (user.email || '').toLowerCase()
    const roles = getRoleKeys(user)
    const apps = getUserApps(user)

    const matchesSearch = !query || displayName.includes(query) || email.includes(query)
    const matchesRole = !roleFilterValues.value.length || roleFilterValues.value.some(role => roles.includes(role))
    const matchesApp = !appFilterValues.value.length || appFilterValues.value.some(app => apps.includes(app))
    const matchesStatus = !statusFilterValues.value.length || statusFilterValues.value.includes(status)
    const matchesProject = !memberListProjectFilter.value || (user.projects && user.projects.includes(memberListProjectFilter.value))
    const matchesDepartment = !memberListDepartmentFilter.value || (user.departments && user.departments.includes(memberListDepartmentFilter.value))

    return matchesSearch && matchesRole && matchesApp && matchesStatus && matchesProject && matchesDepartment
  })
})

const selectedAppsCount = computed(() =>
  Object.values(inviteForm.value.apps).filter(role => role !== 'None').length
)

const groupSuggestions = computed(() => {
  const handle = organizationHandle.value
  return [
    { name: `goals-admins-${handle}`, description: `Grants access to Goals and Goals administration features on ${handle}` },
    { name: `goals-user-access-admins-${handle}`, description: `Grants access to administer users and groups for Goals on ${handle}. Does not grant any product access.` },
    { name: `goals-users-${handle}`, description: `Grants access to Goals on ${handle}` },
    { name: `jira-admins-${handle}`, description: `Grants access to the administration features for all Jira products on ${handle}` },
    { name: `jira-user-access-admins-${handle}`, description: `Grants access to administer users and groups for Jira on ${handle}. Does not grant any product access.` },
    { name: `jira-users-${handle}`, description: `Grants access to Jira on ${handle}` },
    { name: 'org-admins', description: 'Grants access to administer org-level settings, users, groups, and billing' },
    { name: `projects-admins-${handle}`, description: `Grants access to Projects and Projects administration features on ${handle}` },
    { name: `projects-user-access-admins-${handle}`, description: `Grants access to administer users and groups for Projects on ${handle}. Does not grant any product access.` },
    { name: `projects-users-${handle}`, description: `Grants access to Projects on ${handle}` }
  ]
})

const filteredGroupSuggestions = computed(() => {
  const query = groupDraft.value.trim().toLowerCase()
  const selectedNames = new Set(selectedGroups.value.map(group => group.name))
  return groupSuggestions.value
    .filter(group => !selectedNames.has(group.name))
    .filter(group => !query || group.name.toLowerCase().includes(query) || group.description.toLowerCase().includes(query))
})

const visibleRoleFilterOptions = computed(() => filterOptions(roleFilterOptions, filterSearch.value.role))
const visibleAppFilterOptions = computed(() => filterOptions(appFilterOptions, filterSearch.value.apps))
const visibleStatusFilterOptions = computed(() => filterOptions(statusFilterOptions, filterSearch.value.status))

const draftHasValidEmail = computed(() =>
  inviteDraft.value
    .split(/[,\n;\s]+/)
    .map(email => email.trim().toLowerCase())
    .some(email => isValidEmail(email))
)

const canSendInvite = computed(() =>
  (inviteEmails.value.length > 0 || draftHasValidEmail.value) && selectedAppsCount.value > 0 && !isSubmitting.value
)

const hasActiveFilters = computed(() =>
  roleFilterValues.value.length > 0 || appFilterValues.value.length > 0 || statusFilterValues.value.length > 0 || memberListProjectFilter.value || memberListDepartmentFilter.value
)

let searchTimeout = null

const filterOptions = (options, query) => {
  const normalized = query.trim().toLowerCase()
  if (!normalized) return options
  return options.filter(option => option.label.toLowerCase().includes(normalized))
}

const debounceSearch = () => {
  clearTimeout(searchTimeout)
  searchTimeout = setTimeout(() => {
    adminUserStore.fetchUsers(searchQuery.value)
  }, 350)
}

const loadOrganizationProfile = async () => {
  try {
    const res = await axiosClient.get('/settings/TenantProfile')
    const data = res.data?.data || {}
    organizationProfile.value = {
      organizationName: data.organizationName || fallbackOrganization.organizationName,
      domain: data.domain || fallbackOrganization.domain,
      logoUrl: data.logoUrl || ''
    }
  } catch (error) {
    organizationProfile.value = { ...fallbackOrganization }
  }
}

const refreshAdminConfig = async () => {
  await Promise.all([
    adminUserStore.fetchDepartments(),
    adminUserStore.fetchProjectRoleAssignments(),
    adminUserStore.fetchAccessibleProjects(),
    adminUserStore.fetchRoles()
  ])
}

const makeOrganizationHandle = (value) => {
  const raw = String(value || 'sprinta').trim().toLowerCase()
  const withoutDomain = raw.includes('.') ? raw.split('.')[0] : raw
  return withoutDomain
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '') || 'sprinta'
}

const getAvatarColor = (name) => {
  if (!name) return avatarColors[0]
  let hash = 0
  for (let i = 0; i < name.length; i += 1) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash)
  }
  return avatarColors[Math.abs(hash) % avatarColors.length]
}

const getInitials = (name) => {
  if (!name) return '?'
  const cleanName = name.includes('@') ? name.split('@')[0] : name
  return cleanName
    .split(/[.\s_-]+/)
    .filter(Boolean)
    .map(part => part[0])
    .join('')
    .substring(0, 2)
    .toUpperCase()
}

const titleCase = (value) => {
  return String(value || '')
    .replace(/[._-]+/g, ' ')
    .split(' ')
    .filter(Boolean)
    .map(part => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ')
}

const getDisplayName = (user) => {
  return user.name || user.fullName || titleCase((user.email || '').split('@')[0]) || 'Unknown user'
}

const getNormalizedRoles = (user) => (user.roles || []).map(role => String(role).toLowerCase())

const isOrganizationAdmin = (user) => {
  return getNormalizedRoles(user).some(role => ['admin', 'system admin', 'superadmin', 'organization admin'].includes(role))
}

const getRoleKeys = (user) => {
  const roles = getNormalizedRoles(user)
  const keys = new Set()

  if (!roles.length || roles.some(role => ['member', 'user', 'developer', 'dev'].includes(role))) keys.add('user')
  if (roles.some(role => role.includes('guest'))) keys.add('guest')
  if (roles.some(role => role.includes('access admin'))) keys.add('user-access-admin')
  if (roles.some(role => role.includes('app admin') || role === 'admin')) keys.add('app-admin')
  if (roles.some(role => role.includes('site admin') || role === 'admin')) keys.add('site-admin')
  if (isOrganizationAdmin(user)) keys.add('organization-admin')

  return Array.from(keys)
}

const getUserApps = (user) => {
  const roles = getNormalizedRoles(user)
  if (isOrganizationAdmin(user)) return ['Goals', 'Jira Administration', 'Jira', 'Projects']
  if (roles.some(role => ['developer', 'dev', 'pm', 'po', 'qa', 'sm', 'project lead', 'accountant'].includes(role))) return ['Jira', 'Projects']
  return ['Projects']
}

const getStatusMeta = (user) => {
  const rawStatus = String(user.status || '').toLowerCase()

  if (rawStatus.includes('invited') || rawStatus.includes('pending')) {
    return { label: t('INVITED', 'ĐÃ MỜI'), value: 'invited', className: 'status-invited' }
  }
  if (rawStatus.includes('suspend')) {
    return { label: t('SUSPENDED', 'BỊ ĐÌNH CHỈ'), value: 'suspended', className: 'status-suspended' }
  }
  if (rawStatus.includes('deactivate')) {
    return { label: t('DEACTIVATED', 'VÔ HIỆU HÓA'), value: 'deactivated', className: 'status-deactivated' }
  }
  if (user.isActive) {
    return { label: t('ACTIVE', 'HOẠT ĐỘNG'), value: 'active', className: 'status-active' }
  }

  return { label: t('SUSPENDED', 'BỊ ĐÌNH CHỈ'), value: 'suspended', className: 'status-suspended' }
}

const formatAtlassianDate = (value) => {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '-'
  return date.toLocaleDateString(currentLocale.value === 'vi' ? 'vi-VN' : 'en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

const getRelativeInviteTime = (value) => {
  if (!value) return t('Invited just now', 'Vừa mời xong')
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return t('Invited just now', 'Vừa mời xong')
  const minutes = Math.max(0, Math.floor((nowTime.value - date.getTime()) / 60000))
  if (minutes < 1) return t('Invited just now', 'Vừa mời xong')
  if (minutes < 60) return t(`Invited ${minutes} minutes ago`, `Đã mời ${minutes} phút trước`)
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return t(`Invited ${hours} hours ago`, `Đã mời ${hours} giờ trước`)
  return t(`Invited ${Math.floor(hours / 24)} days ago`, `Đã mời ${Math.floor(hours / 24)} ngày trước`)
}

const getLastSeenText = (user) => {
  const status = getStatusMeta(user).value
  if (status === 'invited') return getRelativeInviteTime(user.createdAt || user.invitedAt)
  return formatAtlassianDate(user.lastSeenAt || user.createdAt)
}

const toggleFilterMenu = (name) => {
  openTopActionMenu.value = false
  openRowMenu.value = null
  openFilterMenu.value = openFilterMenu.value === name ? null : name
}

const toggleRowMenu = (user) => {
  openFilterMenu.value = null
  openTopActionMenu.value = false
  const key = user.id || user.email
  openRowMenu.value = openRowMenu.value === key ? null : key
}

const clearFilters = () => {
  roleFilterValues.value = []
  appFilterValues.value = []
  statusFilterValues.value = []
  memberListProjectFilter.value = ''
  memberListDepartmentFilter.value = ''
  filterSearch.value = { role: '', apps: '', status: '' }
  openFilterMenu.value = null
}

const closePageMenus = () => {
  openFilterMenu.value = null
  openTopActionMenu.value = false
  openRowMenu.value = null
}

const closeInviteFloatingMenus = () => {
  openRoleMenu.value = null
  groupInputFocused.value = false
}

const resetInviteForm = () => {
  inviteEmails.value = []
  inviteDraft.value = ''
  groupDraft.value = ''
  selectedGroups.value = []
  groupInputFocused.value = false
  openRoleMenu.value = null
  showPersonalMessage.value = false
  inviteForm.value = {
    projectId: null,
    projectRole: preferredProjectRole.value,
    message: '',
    apps: {
      goals: 'User',
      work: 'User',
      projects: 'User',
      admin: 'None'
    }
  }
}

const loadProjects = async () => {
  try {
    const items = await adminUserStore.fetchAccessibleProjects()
    projectsList.value = items
  } catch (error) {
    console.error('Failed to load projects', error)
  }
}

const openInvitePanel = async () => {
  closePageMenus()
  showInvitePanel.value = true
  if (!projectsList.value.length) {
    await loadProjects()
  }
}

const closeInvitePanel = () => {
  showInvitePanel.value = false
  emailInputFocused.value = false
  closeInviteFloatingMenus()
}

const openExportModal = () => {
  openTopActionMenu.value = false
  showExportModal.value = true
}

const closeExportModal = () => {
  showExportModal.value = false
}

const openAppAccessHint = () => {
  ElMessage.info(t('App access settings will use the same tenant profile and app roles.', 'Cài đặt quyền truy cập ứng dụng sẽ sử dụng cùng hồ sơ tổ chức và vai trò.'))
}

const isValidEmail = (email) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)

const addEmailsFromText = (text) => {
  const values = text
    .split(/[,\n;\s]+/)
    .map(email => email.trim().toLowerCase())
    .filter(Boolean)

  const invalidEmails = values.filter(email => !isValidEmail(email))
  const validEmails = values.filter(email => isValidEmail(email))

  validEmails.forEach(email => {
    if (!inviteEmails.value.includes(email)) inviteEmails.value.push(email)
  })

  if (invalidEmails.length) {
    ElMessage.warning(t(`Invalid email: ${invalidEmails.join(', ')}`, `Email không hợp lệ: ${invalidEmails.join(', ')}`))
  }
}

const addDraftEmails = () => {
  if (!inviteDraft.value.trim()) return
  addEmailsFromText(inviteDraft.value)
  inviteDraft.value = ''
}

const handleEmailBlur = () => {
  emailInputFocused.value = false
  addDraftEmails()
}

const handleEmailPaste = (event) => {
  const pasted = event.clipboardData?.getData('text')
  if (!pasted || !/[,\n;\s]+/.test(pasted)) return
  event.preventDefault()
  addEmailsFromText(pasted)
}

const removeInviteEmail = (email) => {
  inviteEmails.value = inviteEmails.value.filter(item => item !== email)
}

const toggleRoleMenu = (key) => {
  groupInputFocused.value = false
  openRoleMenu.value = openRoleMenu.value === key ? null : key
}

const selectAppRole = (key, value) => {
  inviteForm.value.apps[key] = value
  openRoleMenu.value = null
}

const getSelectedAppOption = (row) => {
  return row.options.find(option => option.value === inviteForm.value.apps[row.key])
}

const addGroup = (group) => {
  if (!selectedGroups.value.some(item => item.name === group.name)) {
    selectedGroups.value.push(group)
  }
  groupDraft.value = ''
  groupInputFocused.value = false
}

const removeGroup = (name) => {
  selectedGroups.value = selectedGroups.value.filter(group => group.name !== name)
}

const addFirstMatchingGroup = () => {
  const firstMatch = filteredGroupSuggestions.value[0]
  if (firstMatch) addGroup(firstMatch)
}

const clearAppAccess = () => {
  Object.keys(inviteForm.value.apps).forEach(key => {
    inviteForm.value.apps[key] = 'None'
  })
  openRoleMenu.value = null
}

const resolveSystemRole = () => {
  const selectedRoles = Object.values(inviteForm.value.apps)
  if (selectedRoles.includes('AppAdmin') || inviteForm.value.apps.admin === 'AppAdmin') return 'Admin'
  if (inviteForm.value.apps.work !== 'None' || inviteForm.value.apps.projects !== 'None') return 'Developer'
  return 'Member'
}

const resolveUserSystemRole = (user) => {
  const roles = getNormalizedRoles(user)
  if (roles.some(role => role.includes('admin'))) return 'Admin'
  if (roles.some(role => ['developer', 'dev', 'pm', 'po', 'qa', 'sm', 'project lead', 'accountant'].includes(role))) return 'Developer'
  return 'Member'
}

const createDepartmentFromDraft = async () => {
  if (!departmentDraft.value.name.trim()) {
    ElMessage.warning(t('Enter department name.', 'Vui lòng nhập tên phòng ban.'))
    return
  }

  try {
    await adminUserStore.createDepartment({
      name: departmentDraft.value.name.trim(),
      isActive: departmentDraft.value.isActive,
      require2FA: departmentDraft.value.require2FA
    })
    departmentDraft.value = {
      name: '',
      isActive: true,
      require2FA: false
    }
    ElMessage.success(t('Department created.', 'Đã tạo phòng ban.'))
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('Could not create department.', 'Không thể tạo phòng ban.'))
  }
}

const renameDepartment = async (department) => {
  try {
    const { value } = await ElMessageBox.prompt(
      t('Update department name', 'Cập nhật tên phòng ban'),
      t('Rename department', 'Đổi tên phòng ban'),
      {
        inputValue: department.name,
        inputPattern: /.+/,
        inputErrorMessage: t('Department name is required.', 'Tên phòng ban là bắt buộc.')
      }
    )

    await adminUserStore.updateDepartment(department.id, {
      name: value.trim(),
      isActive: department.isActive,
      require2FA: department.require2FA,
      managerId: department.managerId || null
    })
    ElMessage.success(t('Department updated.', 'Đã cập nhật phòng ban.'))
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || t('Could not update department.', 'Không thể cập nhật phòng ban.'))
    }
  }
}

const toggleDepartmentState = async (department, key, value) => {
  try {
    await adminUserStore.updateDepartment(department.id, {
      name: department.name,
      isActive: key === 'isActive' ? value : department.isActive,
      require2FA: key === 'require2FA' ? value : department.require2FA,
      managerId: department.managerId || null
    })
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('Could not update department.', 'Không thể cập nhật phòng ban.'))
  }
}

const removeDepartment = (department) => {
  ElMessageBox.confirm(
    t(`Remove ${department.name}?`, `Xóa ${department.name}?`),
    t('Remove department', 'Xóa phòng ban'),
    {
      confirmButtonText: t('Remove', 'Xóa'),
      cancelButtonText: t('Cancel', 'Hủy'),
      type: 'warning'
    }
  ).then(async () => {
    try {
      await adminUserStore.deleteDepartment(department.id)
      ElMessage.success(t('Department removed.', 'Đã xóa phòng ban.'))
    } catch (error) {
      ElMessage.error(error.response?.data?.message || t('Could not remove department.', 'Không thể xóa phòng ban.'))
    }
  }).catch(() => {})
}

const saveDepartmentProjectRole = async () => {
  if (!departmentRoleDraft.value.projectIds.length || !departmentRoleDraft.value.departmentIds.length || !departmentRoleDraft.value.roleName.trim()) {
    ElMessage.warning(t('Select project, department, and role.', 'Vui lòng chọn dự án, phòng ban và vai trò.'))
    return
  }

  try {
    const promises = []
    for (const pId of departmentRoleDraft.value.projectIds) {
      for (const dId of departmentRoleDraft.value.departmentIds) {
        promises.push(adminUserStore.saveProjectRoleAssignment({
          projectId: pId,
          departmentId: dId,
          roleName: departmentRoleDraft.value.roleName.trim()
        }))
      }
    }
    await Promise.all(promises)
    
    departmentRoleDraft.value.roleName = ''
    ElMessage.success(t('Project role mapping saved.', 'Đã lưu mapping vai trò dự án.'))
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('Could not save project role mapping.', 'Không thể lưu mapping vai trò dự án.'))
  }
}

const removeDepartmentProjectRole = async (assignment) => {
  try {
    await adminUserStore.deleteProjectRoleAssignment({
      projectId: assignment.projectId,
      departmentId: assignment.departmentId,
      roleName: assignment.roleName
    })
    ElMessage.success(t('Project role mapping removed.', 'Đã xóa mapping vai trò dự án.'))
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('Could not remove project role mapping.', 'Không thể xóa mapping vai trò dự án.'))
  }
}

const resetRoleDraft = () => {
  editingRoleId.value = null
  roleDraft.value = {
    name: '',
    description: '',
    permissionIds: []
  }
}

const editRole = (role) => {
  editingRoleId.value = role.id
  roleDraft.value = {
    name: role.name || '',
    description: role.description || '',
    permissionIds: [...(role.permissionIds || [])]
  }
}

const saveRoleDraft = async () => {
  if (!roleDraft.value.name.trim()) {
    ElMessage.warning(t('Enter a role name.', 'Vui lòng nhập tên vai trò.'))
    return
  }

  try {
    const payload = {
      name: roleDraft.value.name.trim(),
      description: roleDraft.value.description.trim(),
      permissionIds: roleDraft.value.permissionIds
    }

    if (editingRoleId.value) {
      await adminUserStore.updateRole(editingRoleId.value, payload)
      ElMessage.success(t('Role updated.', 'Đã cập nhật vai trò.'))
    } else {
      await adminUserStore.createRole(payload)
      ElMessage.success(t('Role created.', 'Đã tạo vai trò.'))
    }

    resetRoleDraft()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('Could not save role.', 'Không thể lưu vai trò.'))
  }
}

const removeRole = (role) => {
  ElMessageBox.confirm(
    t(`Delete ${role.name}?`, `Xóa ${role.name}?`),
    t('Delete role', 'Xóa vai trò'),
    {
      confirmButtonText: t('Delete', 'Xóa'),
      cancelButtonText: t('Cancel', 'Hủy'),
      type: 'warning'
    }
  ).then(async () => {
    try {
      await adminUserStore.deleteRole(role.id)
      ElMessage.success(t('Role deleted.', 'Đã xóa vai trò.'))
      if (editingRoleId.value === role.id) {
        resetRoleDraft()
      }
    } catch (error) {
      ElMessage.error(error.response?.data?.message || t('Could not delete role.', 'Không thể xóa vai trò.'))
    }
  }).catch(() => {})
}

const assignRolesToSelectedUser = async () => {
  if (!roleAssignment.value.userId) {
    ElMessage.warning(t('Select a user first.', 'Vui lòng chọn người dùng trước.'))
    return
  }

  try {
    await adminUserStore.assignUserRoles(roleAssignment.value.userId, roleAssignment.value.roleIds)
    ElMessage.success(t('User roles updated.', 'Đã cập nhật vai trò người dùng.'))
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('Could not update user roles.', 'Không thể cập nhật vai trò người dùng.'))
  }
}

watch(() => roleAssignment.value.userId, (userId) => {
  const selectedUser = users.value.find(user => user.id === userId)
  if (!selectedUser) {
    roleAssignment.value.roleIds = []
    return
  }

  const normalizedUserRoles = (selectedUser.roles || []).map(role => String(role).trim().toLowerCase())
  roleAssignment.value.roleIds = roles.value
    .filter(role => normalizedUserRoles.includes(String(role.name || '').trim().toLowerCase()))
    .map(role => role.id)
})

watch(projectRoleOptions, (options) => {
  if (!options.length) return
  if (!options.includes(inviteForm.value.projectRole)) {
    inviteForm.value.projectRole = preferredProjectRole.value
  }
}, { immediate: true })

const submitInvite = async () => {
  addDraftEmails()

  if (!canSendInvite.value) {
    ElMessage.warning(t('Enter at least one email and select app access.', 'Vui lòng nhập ít nhất một email và chọn quyền ứng dụng.'))
    return
  }

  isSubmitting.value = true
  try {
    const role = resolveSystemRole()
    const payloads = inviteEmails.value.map(email => ({
      email,
      role,
      projectId: inviteForm.value.projectId,
      projectRole: inviteForm.value.projectRole,
      inviteMessage: inviteForm.value.message,
      inviteGroups: selectedGroups.value.map(group => group.name)
    }))

    await adminUserStore.inviteUsers(payloads)
    ElMessage.success(t(`Sent ${payloads.length} invitation email${payloads.length > 1 ? 's' : ''}.`, `Đã gửi ${payloads.length} email mời.`))
    closeInvitePanel()
    resetInviteForm()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('Could not send invitation. Please try again.', 'Không thể gửi lời mời. Vui lòng thử lại.'))
  } finally {
    isSubmitting.value = false
  }
}

const resendInvite = async (user) => {
  openRowMenu.value = null
  try {
    await adminUserStore.createUser({
      email: user.email,
      role: resolveUserSystemRole(user),
      projectId: null,
      projectRole: preferredProjectRole.value,
      inviteMessage: ''
    })
    ElMessage.success(t(`Resent invitation to ${user.email}.`, `Đã gửi lại lời mời đến ${user.email}.`))
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('Could not resend invitation.', 'Không thể gửi lại lời mời.'))
  }
}

const handleSuspendUser = (user) => {
  openRowMenu.value = null
  if (!user.isActive) return

  ElMessageBox.confirm(
    t(`Suspend access for ${getDisplayName(user)}?`, `Tạm ngưng truy cập cho ${getDisplayName(user)}?`),
    t('Suspend user', 'Tạm ngưng người dùng'),
    {
      confirmButtonText: t('Suspend', 'Tạm ngưng'),
      cancelButtonText: t('Cancel', 'Hủy'),
      type: 'warning'
    }
  ).then(async () => {
    try {
      await adminUserStore.suspendUser(user.id)
      ElMessage.success(t(`Suspended ${getDisplayName(user)}.`, `Đã tạm ngưng ${getDisplayName(user)}.`))
    } catch (error) {
      ElMessage.error(error.response?.data?.message || t('Could not suspend user.', 'Không thể tạm ngưng người dùng.'))
    }
  }).catch(() => {})
}

const handleApproveRequests = () => {
  ElMessage.info(t('No pending requests to approve.', 'Không có yêu cầu nào cần duyệt.'))
}

const handleRemoveUser = (user) => {
  openRowMenu.value = null
  ElMessageBox.confirm(
    t(`Are you sure you want to completely remove ${getDisplayName(user)}? This action cannot be undone.`, `Bạn có chắc muốn xóa hoàn toàn ${getDisplayName(user)}? Thao tác này không thể hoàn tác.`),
    t('Remove user', 'Xóa người dùng'),
    {
      confirmButtonText: t('Remove', 'Xóa'),
      cancelButtonText: t('Cancel', 'Hủy'),
      type: 'error'
    }
  ).then(async () => {
    try {
      await adminUserStore.removeUser(user.id)
      ElMessage.success(t(`Removed ${getDisplayName(user)}.`, `Đã xóa ${getDisplayName(user)}.`))
    } catch (error) {
      ElMessage.error(error.response?.data?.message || t('Could not remove user.', 'Không thể xóa người dùng.'))
    }
  }).catch(() => {})
}

const showAddGroupHint = (user) => {
  openRowMenu.value = null
  openInvitePanel()
  ElMessage.info(t(`You can assign groups to ${getDisplayName(user)} via Invite menu.`, `Bạn có thể gán nhóm cho ${getDisplayName(user)} qua menu Mời.`))
}

const escapeCsv = (value) => {
  const text = String(value ?? '')
  if (/[",\n]/.test(text)) return `"${text.replace(/"/g, '""')}"`
  return text
}

const exportUsersToCsv = () => {
  let sourceUsers = exportUserScope.value === 'filtered' ? filteredUsers.value : users.value
  if (exportStatusScope.value === 'active') {
    sourceUsers = sourceUsers.filter(user => getStatusMeta(user).value === 'active')
  }

  const headers = ['Name', 'Email', 'Status', 'Roles', 'Last seen']
  if (exportAdditionalData.value.includes('apps')) headers.push('Apps')
  if (exportAdditionalData.value.includes('groups')) headers.push('Groups')

  const rows = sourceUsers.map(user => {
    const row = [
      getDisplayName(user),
      user.email,
      getStatusMeta(user).label,
      (user.roles || []).join('; ') || 'User',
      getLastSeenText(user)
    ]
    if (exportAdditionalData.value.includes('apps')) row.push(getUserApps(user).join('; '))
    if (exportAdditionalData.value.includes('groups')) row.push((user.groups || []).join('; '))
    return row
  })

  const csv = [headers, ...rows]
    .map(row => row.map(escapeCsv).join(','))
    .join('\n')

  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `${organizationHandle.value}-users.csv`
  link.click()
  URL.revokeObjectURL(url)
  closeExportModal()
  ElMessage.success(t('CSV export ready.', 'Đã xuất file CSV thành công.'))
}

onMounted(async () => {
  timeInterval = setInterval(() => { nowTime.value = Date.now() }, 60000)
  await Promise.all([
    adminUserStore.fetchUsers(),
    loadOrganizationProfile(),
    refreshAdminConfig()
  ])
})

onUnmounted(() => {
  if (timeInterval) clearInterval(timeInterval)
})
</script>

<style scoped>
.directory-page {
  position: relative;
  transition: opacity 0.18s ease;
}

.directory-page.is-dimmed {
  opacity: 0.22;
  pointer-events: none;
}

.page-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 24px;
  margin-bottom: 24px;
}

.header-copy {
  min-width: 0;
}



.page-subtitle {
  max-width: 760px;
  margin: 8px 0 0;
  color: var(--color-text-secondary);
  font-size: 14px;
  line-height: 1.6;
}

.page-subtitle a {
  color: #579dff;
  text-decoration: none;
}

.page-subtitle a:hover {
  text-decoration: underline;
}

.page-actions,
.filters-row,
.footer-actions,
.modal-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.page-actions {
  flex: 0 0 auto;
}

.content-frame {
  position: relative;
  overflow: visible;
  padding: 24px;
  border: 1px solid var(--color-border);
  border-radius: 2px;
  background: var(--color-surface);
  box-shadow: 0 16px 40px rgba(0, 0, 0, 0.18);
}

.admin-config-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
  margin-bottom: 20px;
}

.admin-subcard {
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface-hover);
  padding: 24px;
}

.subcard-header {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 14px;
}

.subcard-header h3 {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 16px;
}

.subcard-header p {
  margin: 6px 0 0;
  color: var(--color-text-secondary);
  font-size: 13px;
  line-height: 1.5;
}

.department-form,
.assignment-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.2fr) repeat(2, minmax(0, 0.8fr));
  gap: 10px;
  margin-bottom: 14px;
}

.role-assignment-grid {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.department-form input,
.assignment-grid input {
  min-height: 36px;
  border: 1px solid var(--color-border);
  border-radius: 6px !important;
  background: var(--input-bg);
  color: var(--color-text-primary);
  padding: 0 12px;
  outline: none;
}
.department-form input:focus,
.assignment-grid input:focus {
  border-color: var(--color-accent);
}

.department-list,
.assignment-list {
  display: grid;
  gap: 10px;
}

.department-item,
.assignment-item {
  display: flex;
  justify-content: space-between;
  gap: 14px;
  align-items: center;
  padding: 16px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface);
  box-shadow: var(--shadow-sm);
  transition: all 0.2s;
}
.department-item:hover,
.assignment-item:hover {
  border-color: var(--color-accent);
  transform: translateY(-1px);
  box-shadow: var(--shadow-md);
}

.department-copy,
.assignment-copy {
  min-width: 0;
}

.department-copy strong,
.department-copy span,
.assignment-copy strong,
.assignment-copy span {
  display: block;
}

.assignment-copy small {
  display: block;
  margin-top: 4px;
  color: var(--text-tertiary, #94a3b8);
  font-size: 12px;
}

.department-copy strong,
.assignment-copy strong {
  color: var(--color-text-primary);
}

.department-copy span,
.assignment-copy span {
  margin-top: 4px;
  color: var(--color-text-secondary);
  font-size: 13px;
}

.department-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  align-items: center;
  justify-content: flex-end;
}

.inline-check {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--color-text-secondary);
  font-size: 13px;
}

.compact-state {
  min-height: 96px;
}

.primary-btn,
.neutral-btn,
.icon-btn,
.filter-button,
.plain-action,
.clear-filters-btn {
  min-height: 34px;
  border-radius: 2px;
  cursor: pointer;
  font-size: 14px;
  font-weight: 600;
  letter-spacing: 0;
}

.primary-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 0 14px;
  border: 1px solid #579dff;
  background: #579dff;
  color: #081120;
}

.primary-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.neutral-btn,
.icon-btn,
.filter-button {
  border: 1px solid var(--color-border);
  background: rgba(255, 255, 255, 0.02);
  color: var(--color-text-primary);
}

.neutral-btn {
  padding: 0 12px;
}

.icon-btn {
  width: 34px;
  padding: 0;
}

.request-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 22px;
  height: 20px;
  margin-left: 8px;
  padding: 0 6px;
  border-radius: 2px;
  background: rgba(255, 255, 255, 0.08);
  color: var(--color-text-muted);
  font-size: 12px;
}

.menu-wrap {
  position: relative;
}

.icon-btn.is-open,
.row-more-btn.is-open,
.filter-button.is-open {
  border-color: #579dff;
  box-shadow: 0 0 0 1px rgba(87, 157, 255, 0.7);
}

.action-menu,
.filter-menu {
  position: absolute;
  z-index: 30;
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface);
  box-shadow: var(--shadow-lg);
}

.action-menu {
  min-width: 160px;
  padding: 6px 0;
}

.top-action-menu,
.row-action-menu {
  top: calc(100% + 8px);
  right: 0;
}

.action-menu button {
  display: block;
  width: 100%;
  min-height: 36px;
  padding: 0 14px;
  border: 0;
  background: transparent;
  color: var(--color-text-primary);
  cursor: pointer;
  font-size: 14px;
  text-align: left;
}

.action-menu button:hover {
  background: var(--color-surface-hover);
}

.danger-action {
  color: #f87171 !important;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 14px;
  margin-bottom: 22px;
}

.metric-card {
  min-height: 86px;
  padding: 20px;
  border: 1px solid var(--color-border);
  border-radius: 12px !important;
  background: var(--color-surface);
  box-shadow: var(--shadow-sm);
}

.metric-card span {
  display: block;
  color: var(--color-text-muted);
  font-size: 13px;
}

.metric-card strong {
  display: block;
  margin-top: 10px;
  color: var(--color-text-primary);
  font-size: 28px;
  line-height: 1;
}

.filters-row {
  flex-wrap: wrap;
  margin-bottom: 16px;
  overflow: visible;
}

.filter-wrap {
  position: relative;
  overflow: visible;
}

.table-search,
.filter-search {
  display: flex;
  align-items: center;
  gap: 8px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--input-bg);
  color: var(--color-text-muted);
}

.table-search {
  width: 260px;
  min-height: 36px;
  padding: 0 10px;
}

.table-search input,
.filter-search input {
  width: 100%;
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--color-text-primary);
  font-size: 14px;
}

.filter-button {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 0 12px;
}

.filter-button.has-value {
  border-color: #579dff;
  color: #9cc4ff;
}

.filter-menu {
  top: calc(100% + 8px);
  left: 0;
  width: 250px;
  padding-top: 8px;
}

.apps-filter-menu {
  width: 292px;
}

.status-filter-menu {
  width: 184px;
}

.filter-search {
  min-height: 36px;
  margin: 0 8px 8px;
  padding: 0 8px;
}

.check-option,
.app-check-option {
  display: flex;
  align-items: center;
  gap: 10px;
  min-height: 30px;
  padding: 0 16px;
  color: var(--color-text-primary);
  cursor: pointer;
  font-size: 14px;
}

.check-option:hover,
.app-check-option:hover {
  background: rgba(255, 255, 255, 0.05);
}

.check-option input,
.app-check-option input,
.export-group input {
  width: 14px;
  height: 14px;
  margin: 0;
}

.filter-org-label {
  margin: 0;
  padding: 8px 16px 4px;
  color: var(--color-text-muted);
  font-size: 12px;
  font-weight: 700;
}

.app-check-option {
  align-items: center;
  min-height: 46px;
}

.app-check-option strong,
.app-check-option small {
  display: block;
}

.app-check-option strong {
  color: var(--color-text-primary);
  font-size: 14px;
  font-weight: 500;
}

.app-check-option small {
  margin-top: 2px;
  color: var(--color-text-muted);
  font-size: 12px;
}

.mini-app-icon,
.app-icon {
  display: grid;
  place-items: center;
  flex: 0 0 auto;
  border-radius: 2px;
}

.mini-app-icon {
  width: 20px;
  height: 20px;
  color: #ffffff;
  font-size: 10px;
}

.tone-neutral {
  background: #4b5563;
  color: #ffffff;
}

.tone-blue {
  background: #0c66e4;
  color: #ffffff;
}

.tone-dark {
  background: var(--color-text-primary);
  color: #ffffff;
}

.filter-menu-footer {
  min-height: 36px;
  padding: 10px 12px;
  border-top: 1px solid var(--color-border);
  color: var(--color-text-muted);
  font-size: 13px;
  text-align: right;
}

.status-sample,
.table-status-badge {
  display: inline-flex;
  align-items: center;
  min-height: 18px;
  padding: 0 6px;
  border: 1px solid currentColor;
  border-radius: 2px;
  font-size: 11px;
  font-weight: 800;
  line-height: 1;
}

.status-active {
  color: #4bce97;
  background: rgba(34, 160, 107, 0.12);
}

.status-invited {
  color: #9cc4ff;
  background: rgba(87, 157, 255, 0.12);
}

.status-suspended {
  color: #f5cd47;
  background: rgba(245, 205, 71, 0.12);
}

.status-deactivated {
  color: #f87171;
  background: rgba(226, 72, 61, 0.12);
}

.clear-filters-btn {
  border: 0;
  background: transparent;
  color: #9cc4ff;
}

.results-label {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 16px;
  color: var(--color-text-primary);
  font-size: 15px;
}

.users-table-shell {
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface);
  box-shadow: var(--shadow-sm);
}

.table-state {
  display: grid;
  place-items: center;
  gap: 12px;
  min-height: 220px;
  color: var(--color-text-muted);
  text-align: center;
}

.users-table {
  width: 100%;
  border-collapse: collapse;
}

.users-table thead {
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
}

.users-table th {
  height: 44px;
  padding: 0 16px;
  color: var(--color-text-muted);
  font-size: 13px;
  font-weight: 700;
  text-align: left;
}

.users-table td {
  padding: 12px 16px;
  border-top: 1px solid var(--color-border);
  color: var(--color-text-primary);
  font-size: 14px;
  vertical-align: middle;
}

.users-table tbody tr:hover {
  background: var(--color-surface-hover);
}

.user-cell {
  display: flex;
  align-items: center;
  gap: 12px;
}

.user-avatar {
  display: grid;
  place-items: center;
  width: 30px;
  height: 30px;
  border-radius: 50%;
  color: #081120;
  font-size: 12px;
  font-weight: 800;
}

.user-copy strong,
.user-copy span {
  display: block;
}

.user-copy strong {
  color: var(--color-text-primary);
  font-size: 14px;
  font-weight: 600;
}

.user-copy span {
  margin-top: 2px;
  color: var(--color-text-muted);
  font-size: 12px;
}

.last-seen-cell {
  color: var(--color-text-secondary);
}

.actions-heading,
.actions-cell {
  text-align: right;
}

.row-menu-wrap {
  display: inline-block;
}

.row-more-btn {
  display: grid;
  place-items: center;
  width: 28px;
  height: 28px;
  border: 1px solid transparent;
  border-radius: 2px;
  background: transparent;
  color: var(--color-text-primary);
  cursor: pointer;
}

.row-more-btn:hover {
  border-color: var(--color-border);
  background: var(--color-surface-hover);
}

.invite-panel {
  position: fixed;
  inset: 0;
  z-index: 90;
  overflow-y: auto;
  background: var(--color-bg);
}

.close-invite {
  position: fixed;
  top: 28px;
  left: 28px;
  display: grid;
  place-items: center;
  width: 38px;
  height: 38px;
  border: 1px solid var(--color-border);
  border-radius: 50%;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  cursor: pointer;
  font-size: 18px;
}

.close-invite:hover {
  border-color: var(--color-accent);
  color: var(--color-text-primary);
}

.invite-shell {
  width: min(920px, calc(100% - 48px));
  margin: 64px auto 96px;
}

.invite-copy {
  margin-bottom: 22px;
}

.invite-copy h2 {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 25px;
  font-weight: 800;
}

.invite-copy p {
  max-width: 860px;
  margin: 8px 0 0;
  color: var(--color-text-secondary);
  font-size: 14px;
  line-height: 1.5;
}

.invite-section {
  position: relative;
  margin-bottom: 20px;
}

.field-label {
  display: block;
  margin-bottom: 7px;
  color: var(--color-text-secondary);
  font-size: 12px;
  font-weight: 800;
}

.email-composer,
.group-composer {
  display: flex;
  min-height: 40px;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  padding: 5px 8px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--input-bg);
}

.email-composer.is-focused,
.group-composer.is-focused {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 1px var(--color-accent);
}

.email-composer input,
.group-composer input {
  min-width: 220px;
  flex: 1 1 220px;
  border: 0;
  outline: 0;
  color: var(--color-text-primary);
  background: transparent;
  font-size: 14px;
}

.email-chip,
.group-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 26px;
  padding: 0 7px;
  border: 1px solid var(--color-border);
  border-radius: 4px;
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
  font-size: 13px;
}

.email-chip button,
.group-chip button {
  display: grid;
  place-items: center;
  width: 18px;
  height: 18px;
  border: 0;
  border-radius: 2px;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
}

.hint {
  margin: 7px 0 0;
  color: var(--color-text-muted);
  font-size: 12px;
}

.access-table {
  margin-bottom: 42px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface);
  overflow: hidden;
}

.access-header,
.access-row {
  display: grid;
  grid-template-columns: minmax(260px, 1.45fr) minmax(100px, 0.45fr) minmax(210px, 0.7fr) 90px;
  align-items: center;
  gap: 18px;
  min-height: 66px;
  padding: 0 16px;
}

.access-header {
  min-height: 40px;
  border-bottom: 1px solid var(--color-border);
  color: var(--color-text-primary);
  font-size: 13px;
  font-weight: 800;
}

.access-header button {
  justify-self: end;
  border: 0;
  background: transparent;
  color: var(--color-accent);
  cursor: pointer;
  font-weight: 700;
}

.access-row + .access-row {
  border-top: 1px solid var(--color-border);
}

.app-cell {
  display: flex;
  align-items: center;
  gap: 10px;
}

.app-icon {
  width: 32px;
  height: 32px;
}

.app-cell strong,
.app-cell span {
  display: block;
}

.app-cell strong {
  color: var(--color-text-primary);
  font-size: 14px;
  font-weight: 500;
}

.app-cell span,
.plan-cell {
  color: var(--color-text-secondary);
  font-size: 13px;
}

.role-picker,
.project-select,
.project-role-select {
  width: 100%;
}

.role-picker {
  position: relative;
}

.role-trigger {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  min-height: 34px;
  padding: 0 10px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--input-bg);
  color: var(--color-text-primary);
  cursor: pointer;
  font-size: 14px;
}

.role-trigger.is-open {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 1px var(--color-accent);
}

.role-menu {
  position: absolute;
  top: calc(100% + 6px);
  right: 0;
  z-index: 40;
  width: 334px;
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface);
  box-shadow: var(--shadow-lg);
}

.role-option {
  display: grid;
  grid-template-columns: 22px 1fr;
  gap: 10px;
  width: 100%;
  padding: 12px 16px;
  border: 0;
  background: transparent;
  color: var(--color-text-primary);
  cursor: pointer;
  text-align: left;
}

.role-option:hover,
.role-option.is-selected {
  background: var(--color-surface-hover);
}

.check-box {
  display: grid;
  place-items: center;
  width: 14px;
  height: 14px;
  margin-top: 3px;
  border: 1px solid var(--color-border);
  border-radius: 2px;
  color: #ffffff;
  font-size: 9px;
}

.role-option.is-selected .check-box {
  border-color: var(--color-accent);
  background: var(--color-accent);
}

.role-option-copy strong,
.role-option-copy small {
  display: block;
}

.role-option-copy strong {
  color: var(--color-text-primary);
  font-size: 14px;
  font-weight: 500;
  line-height: 1.3;
}

.role-option-copy small {
  margin-top: 4px;
  color: var(--color-text-secondary);
  font-size: 13px;
  line-height: 1.45;
}

.role-option.is-selected .role-option-copy small {
  color: var(--color-accent);
}

.role-menu-footer {
  padding: 12px 16px;
  border-top: 1px solid var(--color-border);
  background: var(--color-surface);
}

.role-menu-footer button {
  border: 0;
  background: transparent;
  color: var(--color-accent);
  cursor: pointer;
  font-weight: 600;
}

.group-suggestions {
  max-height: 248px;
  overflow-y: auto;
  border: 1px solid var(--color-border);
  border-top: 0;
  background: var(--color-surface);
  box-shadow: var(--shadow-lg);
}

.group-option {
  display: block;
  width: 100%;
  padding: 9px 10px;
  border: 0;
  background: transparent;
  color: var(--color-text-primary);
  cursor: pointer;
  text-align: left;
}

.group-option:hover {
  background: var(--color-surface-hover);
}

.group-option strong,
.group-option span {
  display: block;
}

.group-option strong {
  color: var(--color-text-primary);
  font-size: 13px;
  line-height: 1.35;
}

.group-option span {
  margin-top: 3px;
  color: var(--color-text-secondary);
  font-size: 13px;
  line-height: 1.4;
}

.project-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 220px;
  gap: 10px;
}

.collapse-trigger {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  border: 0;
  background: transparent;
  color: var(--color-text-primary);
  cursor: pointer;
  font-size: 14px;
  font-weight: 700;
}

.invite-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  margin-top: 28px;
}

.invite-footer strong {
  color: var(--color-text-primary);
  font-size: 14px;
}

.invite-footer p {
  margin: 6px 0 0;
  color: var(--color-text-secondary);
  font-size: 13px;
}

.plain-action {
  padding: 0 12px;
  border: 0;
  background: transparent;
  color: var(--color-text-secondary);
}

.modal-backdrop {
  position: fixed;
  inset: 0;
  z-index: 80;
  display: grid;
  place-items: start center;
  padding-top: 52px;
  background: rgba(9, 30, 66, 0.56);
}

.export-modal {
  width: 334px;
  padding: 18px 20px 20px;
  border-radius: 12px;
  background: var(--color-surface);
  color: var(--color-text-primary);
  box-shadow: var(--shadow-lg);
  border: 1px solid var(--color-border);
}

.export-modal h2 {
  margin: 0 0 12px;
  font-size: 18px;
  font-weight: 800;
}

.export-modal p {
  margin: 0 0 14px;
  color: var(--color-text-primary);
  font-size: 13px;
  line-height: 1.45;
}

.export-group {
  display: grid;
  gap: 8px;
  margin: 14px 0;
}

.export-group label {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--color-text-primary);
  font-size: 13px;
}

.export-label {
  color: var(--color-text-secondary);
  font-size: 12px;
  font-weight: 700;
}

.modal-actions {
  justify-content: flex-end;
  margin-top: 16px;
}

:deep(.el-input__wrapper),
:deep(.el-select__wrapper),
:deep(.el-textarea__inner) {
  border-radius: 2px !important;
}

::v-deep(.invite-panel .el-input__wrapper),
::v-deep(.invite-panel .el-textarea__inner),
::v-deep(.invite-panel .el-select__wrapper) {
  background-color: var(--input-bg) !important;
  box-shadow: 0 0 0 1px var(--color-border) inset !important;
}

:deep(.invite-panel .el-input__inner),
:deep(.invite-panel .el-textarea__inner),
:deep(.invite-panel .el-select__placeholder),
:deep(.invite-panel .el-select__selected-item) {
  color: var(--color-text-primary) !important;
}

@media (max-width: 980px) {
  .page-header,
  .invite-footer {
    flex-direction: column;
    align-items: stretch;
  }

  .page-actions {
    flex-wrap: wrap;
  }

  .stats-grid {
    grid-template-columns: 1fr;
  }

  .admin-config-grid,
  .department-form,
  .assignment-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 760px) {
  .content-frame {
    padding: 16px;
  }

  .table-search {
    width: 100%;
  }

  .users-table-shell {
    overflow-x: auto;
  }

  .users-table {
    min-width: 760px;
  }

  .access-header,
  .access-row {
    grid-template-columns: 1fr;
    gap: 10px;
    padding: 12px;
  }

  .access-header {
    display: none;
  }

  .project-grid {
    grid-template-columns: 1fr;
  }

  .close-invite {
    top: 14px;
    left: 14px;
  }

  .invite-shell {
    width: min(100% - 28px, 920px);
    margin-top: 76px;
  }
}

.admin-subcard {
  min-height: 580px;
  display: flex;
  flex-direction: column;
}

.row-form {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
  margin-bottom: 24px;
}

.grid-list {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
  align-content: start;
  flex: 1;
}

.pagination-controls {
  display: flex;
  justify-content: center;
  margin-top: 24px;
}

/* Pagination Dark Mode Styling */
.pagination-controls :deep(.el-pagination) {
  --el-pagination-bg-color: transparent !important;
  --el-pagination-text-color: var(--color-text-primary, #e4e6ea) !important;
  --el-pagination-button-color: var(--color-text-primary, #e4e6ea) !important;
  --el-pagination-button-disabled-color: var(--color-text-muted, #8590a2) !important;
  --el-pagination-button-disabled-bg-color: transparent !important;
  --el-pagination-hover-color: #3b82f6 !important;
}

.pagination-controls :deep(.el-pager li) {
  background: transparent !important;
  color: var(--color-text-primary, #e4e6ea) !important;
}

.pagination-controls :deep(.el-pager li.is-active) {
  color: #3b82f6 !important;
  font-weight: bold;
}

.pagination-controls :deep(.el-pagination button) {
  background: transparent !important;
  color: var(--color-text-primary, #e4e6ea) !important;
}

.pagination-controls :deep(.el-pagination button:disabled) {
  color: var(--color-text-muted, #8590a2) !important;
}

/* Jumper styling if present */
.pagination-controls :deep(.el-pagination__jump) {
  color: var(--color-text-primary, #e4e6ea) !important;
  margin-left: 16px;
}

/* Hide the texts "Go to" and "page" around the input */
.pagination-controls :deep(.el-pagination__goto),
.pagination-controls :deep(.el-pagination__classifier) {
  display: none !important;
}

/* Style the jumper input */
.pagination-controls :deep(.el-pagination__jump .el-input__wrapper) {
  background-color: transparent !important;
  border: 1px solid rgba(255, 255, 255, 0.2) !important;
  box-shadow: none !important;
  padding: 0 8px;
  width: 50px;
}

.pagination-controls :deep(.el-pagination__jump .el-input__inner) {
  color: #fff !important;
  text-align: center;
}

.accent-back-btn {
  background: #3b82f620 !important;
  color: #3b82f6 !important;
  border: 1px solid #3b82f650 !important;
  font-weight: 600;
  border-radius: 6px;
  padding: 6px 16px;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  transition: all 0.2s;
}
.accent-back-btn:hover {
  background: #3b82f630 !important;
}

/* Fix Element Plus select text highlighting on click */
.el-select :deep(.el-input__inner::selection) {
  background: transparent !important;
  color: inherit;
}
</style>

<style>
/* Unscoped style explicitly for Element Plus dropdowns appended to body */
.admin-project-dropdown {
  background-color: var(--bg-layout, #1a1d24) !important;
  border-color: var(--border-color, #2e3643) !important;
}

.admin-project-dropdown .el-select-dropdown__item {
  color: var(--text-primary, #e4e6ea) !important;
}

.admin-project-dropdown .el-select-dropdown__item.is-hovering,
.admin-project-dropdown .el-select-dropdown__item:hover {
  background-color: var(--bg-hover, #2c333e) !important;
}

.admin-project-dropdown .el-select-dropdown__item.selected {
  color: #3b82f6 !important;
  font-weight: 600;
}
</style>




