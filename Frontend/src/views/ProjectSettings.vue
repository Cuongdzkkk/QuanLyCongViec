<template>
  <div class="project-settings-route">
    <div class="project-settings-page" v-loading="loading">
      <header class="settings-header">
        <div>
          <div class="breadcrumb">Cài đặt dự án</div>
          <h1>{{ project.name || 'Cài đặt dự án' }}</h1>
          <p>Quản trị thành viên, quy trình, nhãn, module, chu kỳ và vòng đời dự án.</p>
        </div>
        <div class="header-actions">
          <button class="secondary-btn" type="button" @click="goBack">Quay lại dự án</button>
          <button class="primary-btn" type="button" :disabled="savingGeneral" @click="saveGeneral">
            {{ savingGeneral ? 'Đang lưu...' : 'Lưu chung' }}
          </button>
        </div>
      </header>

      <div class="settings-shell">
        <aside class="settings-nav">
          <button
            v-for="tab in tabs"
            :key="tab.key"
            type="button"
            class="nav-tab"
            :class="{ active: activeTab === tab.key }"
            @click="activeTab = tab.key"
          >
            <span>{{ tab.label }}</span>
            <small>{{ tab.caption }}</small>
          </button>
        </aside>

        <section class="settings-content">
          <div v-if="activeTab === 'general'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Tổng quan</h2>
                <p>Thông tin dự án và ngày lập kế hoạch chính.</p>
              </div>
            </div>

            <div class="form-grid">
              <label>
                <span>Tên dự án</span>
                <input v-model="generalForm.name" type="text" placeholder="Tên dự án" />
              </label>

              <label>
                <span>Mã</span>
                <input v-model="generalForm.key" type="text" maxlength="24" placeholder="Mã dự án" />
              </label>

              <label class="wide">
                <span>Mô tả</span>
                <textarea v-model="generalForm.description" rows="4" placeholder="Mô tả dự án"></textarea>
              </label>

              <label>
                <span>Ngày bắt đầu</span>
                <input v-model="generalForm.startDate" type="date" />
              </label>

              <label>
                <span>Ngày kết thúc</span>
                <input v-model="generalForm.endDate" type="date" />
              </label>
            </div>

            <div class="cover-settings">
              <div class="cover-settings-preview" :style="coverPreviewStyle" role="img" :aria-label="project.coverAltText || generatedCoverAltText">
                <span v-if="!displayCoverUrl">{{ project.icon || 'P' }}</span>
              </div>
              <div class="cover-settings-controls">
                <div>
                  <h3>Project cover</h3>
                  <p>PNG, JPG, JPEG, or WEBP. Maximum 5MB.</p>
                </div>
                <input
                  ref="coverInputRef"
                  class="sr-only"
                  type="file"
                  accept="image/png,image/jpeg,image/webp"
                  @change="handleCoverSelected"
                />
                <div class="cover-settings-actions">
                  <button class="secondary-btn" type="button" :disabled="savingCover" @click="coverInputRef?.click()">
                    {{ coverFile ? 'Choose another image' : 'Choose image' }}
                  </button>
                  <button class="primary-btn" type="button" :disabled="savingCover || !coverFile" @click="saveProjectCover">
                    {{ savingCover ? 'Saving...' : 'Save cover' }}
                  </button>
                  <button v-if="displayCoverUrl" class="danger-outline-btn" type="button" :disabled="savingCover" @click="removeProjectCover">
                    Remove cover
                  </button>
                </div>
                <small v-if="coverFile" class="cover-file-name">{{ coverFile.name }}</small>
                <small v-if="coverError" class="cover-error">{{ coverError }}</small>
              </div>
            </div>

            <div class="meta-strip">
              <span>Hiển thị: {{ project.networkType || 'Công khai' }}</span>
              <span>Phụ trách: {{ project.leadName || 'Chưa giao' }}</span>
              <span>Thành viên: {{ members.length }}</span>
              <span>Lưu trữ: {{ project.isArchived ? 'Có' : 'Không' }}</span>
            </div>
          </div>

          <div v-else-if="activeTab === 'execution'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Quy tắc thực thi</h2>
                <p>Cấu hình quyền xem công việc theo vai trò cho dự án.</p>
              </div>
              <button class="primary-btn" type="button" :disabled="savingExecutionRules" @click="saveExecutionRules">
                {{ savingExecutionRules ? 'Đang lưu...' : 'Lưu quy tắc' }}
              </button>
            </div>

            <div class="form-grid">
              <label class="wide switch-row">
                <span>Bật quyền xem công việc theo vai trò</span>
                <input v-model="executionRulesForm.enableRoleBasedTaskVisibility" type="checkbox" />
              </label>

              <label class="wide switch-row">
                <span>Quản lý luôn thấy toàn bộ công việc</span>
                <input v-model="executionRulesForm.managerAlwaysSeeAllTasks" type="checkbox" />
              </label>

              <label>
                <span>Phạm vi xem mặc định</span>
                <select v-model="executionRulesForm.defaultTaskVisibilityMode">
                  <option value="project">Thành viên dự án</option>
                  <option value="assigned">Chỉ người được giao</option>
                  <option value="role">Theo vai trò</option>
                </select>
              </label>

              <label v-if="false">
                <span>Estimate baseline mode</span>
                <select v-model="executionRulesForm.estimateBaselineMode">
                  <option value="role_then_project">Role then project</option>
                  <option value="project_only">Project only</option>
                </select>
              </label>

              <label v-if="false">
                <span>Default base hours</span>
                <input v-model.number="executionRulesForm.defaultBaseHours" type="number" min="1" step="0.5" />
              </label>

              <label v-if="false">
                <span>Hours per story point</span>
                <input v-model.number="executionRulesForm.hoursPerStoryPoint" type="number" min="0.5" step="0.5" />
              </label>
            </div>

            <div v-if="false" class="stack-list">
              <div class="stack-row">
                <div class="row-main">
                  <strong>Role multipliers</strong>
                  <p>Used when the assignee has no project history yet.</p>
                </div>
              </div>
              <div class="stack-row multiplier-grid">
                <label v-for="roleKey in Object.keys(executionRulesForm.roleHourMultipliers)" :key="roleKey">
                  <span>{{ roleKey.toUpperCase() }}</span>
                  <input v-model.number="executionRulesForm.roleHourMultipliers[roleKey]" type="number" min="0.3" max="3" step="0.05" />
                </label>
              </div>
            </div>
          </div>

          <div v-else-if="activeTab === 'points'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Điểm theo project</h2>
                <p>Theo dõi điểm từng thành viên trong project, cộng trừ thủ công và xem lịch sử điều chỉnh.</p>
              </div>
            </div>

            <div class="inline-form invite-grid">
              <label>
                <span>Thành viên</span>
                <select v-model="pointAdjustmentForm.userId">
                  <option :value="null">Chọn thành viên</option>
                  <option v-for="member in members" :key="`point-${member.userId}`" :value="member.userId">
                    {{ member.fullName || member.email }}
                  </option>
                </select>
              </label>
              <label>
                <span>Số điểm</span>
                <input v-model.number="pointAdjustmentForm.amount" type="number" step="1" />
              </label>
              <label>
                <span>Loại điều chỉnh</span>
                <select v-model="pointAdjustmentForm.adjustmentType">
                  <option value="Manual">Manual</option>
                  <option value="Bonus">Bonus</option>
                  <option value="Penalty">Penalty</option>
                </select>
              </label>
              <label class="wide">
                <span>Lý do</span>
                <input v-model="pointAdjustmentForm.reason" type="text" placeholder="Ví dụ: hỗ trợ team QA, xử lý hotfix, vi phạm SLA..." />
              </label>
              <button class="secondary-btn" type="button" :disabled="savingPointAdjustment" @click="createPointAdjustment">
                {{ savingPointAdjustment ? 'Đang lưu...' : 'Cộng / trừ điểm' }}
              </button>
            </div>

            <div class="metric-grid">
              <div class="metric-card">
                <span>Tổng điểm project</span>
                <strong>{{ pointManagement.totalProjectPoints || 0 }}</strong>
              </div>
              <div class="metric-card">
                <span>Tổng điều chỉnh tay</span>
                <strong>{{ pointManagement.totalManualAdjustments || 0 }}</strong>
              </div>
              <div class="metric-card">
                <span>Số thành viên có điểm</span>
                <strong>{{ pointManagement.leaderboard?.length || 0 }}</strong>
              </div>
            </div>

            <div class="two-column-grid">
              <div class="helper-panel">
                <div class="section-split">
                  <h3>Bảng xếp hạng theo project</h3>
                  <p>Điểm gồm giao dịch từ task trong project cộng với điều chỉnh tay.</p>
                </div>
                <div v-if="!pointManagement.leaderboard?.length" class="empty-state">Chưa có dữ liệu điểm trong project.</div>
                <div v-else class="stack-list">
                  <div v-for="member in pointManagement.leaderboard" :key="`lb-${member.userId}`" class="stack-row">
                    <div class="row-main">
                      <strong>{{ member.userName }}</strong>
                      <p>{{ member.projectRole || 'Member' }} · Task {{ member.taskPoints }} · Manual {{ member.manualAdjustments }}</p>
                    </div>
                    <div class="row-actions">
                      <strong>{{ member.totalPoints }} pts</strong>
                    </div>
                  </div>
                </div>
              </div>

              <div class="helper-panel">
                <div class="section-split">
                  <h3>Lịch sử điểm gần nhất</h3>
                  <p>Hiển thị cả giao dịch task và điều chỉnh tay ở cấp project.</p>
                </div>
                <div v-if="!pointManagement.history?.length" class="empty-state">Chưa có lịch sử điểm.</div>
                <div v-else class="stack-list compact-list">
                  <div v-for="entry in pointManagement.history" :key="`history-${entry.id}`" class="stack-row">
                    <div class="row-main">
                      <strong>{{ entry.userName }}</strong>
                      <p>{{ entry.reason }}</p>
                      <small>{{ entry.transactionType }} · {{ formatDateLabel(entry.createdAt) }}</small>
                    </div>
                    <div class="row-actions">
                      <strong :class="{ 'negative-chip': entry.amount < 0 }">{{ entry.amount > 0 ? '+' : '' }}{{ entry.amount }}</strong>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div v-else-if="activeTab === 'rewardRules'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Rule thưởng / phạt</h2>
                <p>Quy định điểm cơ bản, thưởng sớm, thưởng accuracy, phạt trễ hạn và giới hạn chỉnh tay.</p>
              </div>
              <button class="primary-btn" type="button" :disabled="savingRewardRules" @click="saveRewardRules">
                {{ savingRewardRules ? 'Đang lưu...' : 'Lưu rule thưởng / phạt' }}
              </button>
            </div>

            <div class="form-grid">
              <label>
                <span>Hệ số điểm cơ bản</span>
                <input v-model.number="rewardRulesForm.basePointMultiplier" type="number" min="0.2" max="5" step="0.1" />
              </label>
              <label>
                <span>% thưởng hoàn thành sớm</span>
                <input v-model.number="rewardRulesForm.earlyBonusPercent" type="number" min="0" max="100" step="1" />
              </label>
              <label>
                <span>% thưởng estimate sát thực tế</span>
                <input v-model.number="rewardRulesForm.accuracyBonusPercent" type="number" min="0" max="100" step="1" />
              </label>
              <label>
                <span>% phạt trễ hạn</span>
                <input v-model.number="rewardRulesForm.latePenaltyPercent" type="number" min="0" max="100" step="1" />
              </label>
              <label>
                <span>Điểm thưởng cộng tác nhiều assignee</span>
                <input v-model.number="rewardRulesForm.collaborationBonusPoints" type="number" min="0" max="100" step="1" />
              </label>
              <label>
                <span>Giới hạn cộng / trừ tay</span>
                <input v-model.number="rewardRulesForm.manualAdjustmentLimit" type="number" min="10" max="5000" step="10" />
              </label>
            </div>
          </div>

          <div v-else-if="activeTab === 'milestones'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Milestone / release</h2>
                <p>Tạo milestone, gắn cycle liên quan và theo dõi tiến độ release trong project.</p>
              </div>
            </div>

            <div class="inline-form module-grid">
              <label>
                <span>Tên milestone</span>
                <input v-model="newMilestone.name" type="text" placeholder="Release 1.2 hardening" />
              </label>
              <label>
                <span>Release version</span>
                <input v-model="newMilestone.releaseVersion" type="text" placeholder="v1.2.0" />
              </label>
              <label>
                <span>Bắt đầu</span>
                <input v-model="newMilestone.startDate" type="date" />
              </label>
              <label>
                <span>Mục tiêu</span>
                <input v-model="newMilestone.targetDate" type="date" />
              </label>
              <label>
                <span>Trạng thái</span>
                <select v-model="newMilestone.status">
                  <option value="Planned">Planned</option>
                  <option value="Active">Active</option>
                  <option value="At Risk">At Risk</option>
                  <option value="Completed">Completed</option>
                </select>
              </label>
              <label class="wide">
                <span>Mô tả</span>
                <input v-model="newMilestone.description" type="text" placeholder="Mục tiêu phát hành và phạm vi release" />
              </label>
              <label class="wide">
                <span>Cycle liên kết</span>
                <select v-model="newMilestone.linkedSprintIds" multiple size="4">
                  <option v-for="sprint in sprints" :key="`new-ms-${sprint.id}`" :value="sprint.id">{{ sprint.name }}</option>
                </select>
              </label>
              <button class="secondary-btn" type="button" :disabled="savingMilestone" @click="createMilestone">
                {{ savingMilestone ? 'Đang tạo...' : 'Tạo milestone' }}
              </button>
            </div>

            <div v-if="!milestones.length" class="empty-state">Chưa có milestone nào.</div>
            <div v-else class="stack-list">
              <div v-for="milestone in milestones" :key="milestone.id" class="stack-row">
                <div class="row-main editable-grid">
                  <label>
                    <span>Tên</span>
                    <input v-model="milestone.name" type="text" />
                  </label>
                  <label>
                    <span>Release</span>
                    <input v-model="milestone.releaseVersion" type="text" />
                  </label>
                  <label>
                    <span>Bắt đầu</span>
                    <input v-model="milestone.startDate" type="date" />
                  </label>
                  <label>
                    <span>Mục tiêu</span>
                    <input v-model="milestone.targetDate" type="date" />
                  </label>
                  <label>
                    <span>Trạng thái</span>
                    <select v-model="milestone.status">
                      <option value="Planned">Planned</option>
                      <option value="Active">Active</option>
                      <option value="At Risk">At Risk</option>
                      <option value="Completed">Completed</option>
                      <option value="Archived">Archived</option>
                    </select>
                  </label>
                  <label class="wide">
                    <span>Mô tả</span>
                    <input v-model="milestone.description" type="text" />
                  </label>
                  <label class="wide">
                    <span>Cycle liên kết</span>
                    <select v-model="milestone.linkedSprintIds" multiple size="4">
                      <option v-for="sprint in sprints" :key="`${milestone.id}-${sprint.id}`" :value="sprint.id">{{ sprint.name }}</option>
                    </select>
                  </label>
                </div>
                <div class="row-actions vertical-actions">
                  <button class="secondary-btn" type="button" @click="saveMilestone(milestone)">Save</button>
                  <button class="danger-outline-btn" type="button" @click="deleteMilestone(milestone)">Delete</button>
                </div>
              </div>
            </div>
          </div>

          <div v-else-if="activeTab === 'capacity'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Capacity / workload</h2>
                <p>Thiết lập ngưỡng giờ và số task active theo role để cảnh báo quá tải.</p>
              </div>
              <button class="primary-btn" type="button" :disabled="savingCapacityRules" @click="saveCapacityRules">
                {{ savingCapacityRules ? 'Đang lưu...' : 'Lưu capacity' }}
              </button>
            </div>

            <div class="form-grid">
              <label>
                <span>Giờ mặc định / tuần</span>
                <input v-model.number="capacityRulesForm.defaultWeeklyHours" type="number" min="8" max="80" step="1" />
              </label>
              <label>
                <span>% near limit</span>
                <input v-model.number="capacityRulesForm.nearLimitPercent" type="number" min="40" max="100" step="1" />
              </label>
              <label>
                <span>% over limit</span>
                <input v-model.number="capacityRulesForm.overLimitPercent" type="number" min="60" max="200" step="1" />
              </label>
              <label>
                <span>Task active tối đa / người</span>
                <input v-model.number="capacityRulesForm.maxActiveTasksPerMember" type="number" min="1" max="30" step="1" />
              </label>
            </div>

            <div class="two-column-grid">
              <div class="helper-panel">
                <div class="section-split">
                  <h3>Giờ chuẩn theo role</h3>
                </div>
                <div class="multiplier-grid">
                  <label v-for="roleKey in Object.keys(capacityRulesForm.roleWeeklyHours)" :key="`cap-hours-${roleKey}`">
                    <span>{{ roleKey }}</span>
                    <input v-model.number="capacityRulesForm.roleWeeklyHours[roleKey]" type="number" min="4" max="80" step="1" />
                  </label>
                </div>
              </div>

              <div class="helper-panel">
                <div class="section-split">
                  <h3>Task active tối đa theo role</h3>
                </div>
                <div class="multiplier-grid">
                  <label v-for="roleKey in Object.keys(capacityRulesForm.roleActiveTaskLimits)" :key="`cap-tasks-${roleKey}`">
                    <span>{{ roleKey }}</span>
                    <input v-model.number="capacityRulesForm.roleActiveTaskLimits[roleKey]" type="number" min="1" max="30" step="1" />
                  </label>
                </div>
              </div>
            </div>
          </div>

          <div v-else-if="activeTab === 'baseline'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Baseline estimate</h2>
                <p>Quản lý giờ gợi ý mặc định cho user mới, theo role và theo baseline planning đã xác nhận.</p>
              </div>
              <button class="primary-btn" type="button" :disabled="savingBaselineSettings" @click="saveBaselineSettings">
                {{ savingBaselineSettings ? 'Đang lưu...' : 'Lưu baseline estimate' }}
              </button>
            </div>

            <div class="form-grid">
              <label class="wide switch-row">
                <span>Dùng planning baseline đã xác nhận</span>
                <input v-model="baselineSettingsForm.usePlanningBaseline" type="checkbox" />
              </label>
              <label>
                <span>Base hours mặc định</span>
                <input v-model.number="baselineSettingsForm.defaultBaseHours" type="number" min="0.5" max="40" step="0.5" />
              </label>
              <label>
                <span>Hours per story point</span>
                <input v-model.number="baselineSettingsForm.hoursPerStoryPoint" type="number" min="0.5" max="16" step="0.5" />
              </label>
              <label>
                <span>Estimate nhỏ nhất</span>
                <input v-model.number="baselineSettingsForm.minimumSuggestedHours" type="number" min="0.1" max="24" step="0.1" />
              </label>
              <label>
                <span>Estimate lớn nhất</span>
                <input v-model.number="baselineSettingsForm.maximumSuggestedHours" type="number" min="1" max="200" step="0.5" />
              </label>
            </div>

            <div class="two-column-grid">
              <div class="helper-panel">
                <div class="section-split">
                  <h3>Role multiplier</h3>
                </div>
                <div class="multiplier-grid">
                  <label v-for="roleKey in Object.keys(baselineSettingsForm.roleHourMultipliers)" :key="`base-mul-${roleKey}`">
                    <span>{{ roleKey }}</span>
                    <input v-model.number="baselineSettingsForm.roleHourMultipliers[roleKey]" type="number" min="0.3" max="3" step="0.05" />
                  </label>
                </div>
              </div>

              <div class="helper-panel">
                <div class="section-split">
                  <h3>Base hours theo role</h3>
                </div>
                <div class="multiplier-grid">
                  <label v-for="roleKey in Object.keys(baselineSettingsForm.roleBaseHours)" :key="`base-role-${roleKey}`">
                    <span>{{ roleKey }}</span>
                    <input v-model.number="baselineSettingsForm.roleBaseHours[roleKey]" type="number" min="0.5" max="40" step="0.5" />
                  </label>
                </div>
              </div>
            </div>

            <div class="helper-panel">
              <div class="section-split">
                <h3>Planning baseline gần nhất</h3>
                <p v-if="operationalDashboard.baselineHealth?.planningBaseline">Đã xác nhận và có thể dùng để hiệu chỉnh estimate mặc định.</p>
                <p v-else>Project chưa xác nhận planning baseline.</p>
              </div>
              <div v-if="operationalDashboard.baselineHealth?.planningBaseline" class="meta-strip compact">
                <span>Committed SP: {{ operationalDashboard.baselineHealth.planningBaseline.committedStoryPoints }}</span>
                <span>Completed SP: {{ operationalDashboard.baselineHealth.planningBaseline.completedStoryPoints }}</span>
                <span>Estimated hours: {{ operationalDashboard.baselineHealth.planningBaseline.estimatedHours }}</span>
                <span>Actual hours: {{ operationalDashboard.baselineHealth.planningBaseline.actualHours }}</span>
              </div>
            </div>
          </div>

          <div v-else-if="activeTab === 'dashboard'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Dashboard vận hành</h2>
                <p>Xem nhanh tình trạng tải công việc, điểm project, milestone, baseline và điểm nghẽn chính.</p>
              </div>
              <button class="secondary-btn" type="button" @click="loadManagementData">Reload dashboard</button>
            </div>

            <div class="metric-grid">
              <div class="metric-card">
                <span>Total tasks</span>
                <strong>{{ operationalDashboard.overview?.totalTasks || 0 }}</strong>
              </div>
              <div class="metric-card">
                <span>Completed</span>
                <strong>{{ operationalDashboard.overview?.completedTasks || 0 }}</strong>
              </div>
              <div class="metric-card">
                <span>Overdue</span>
                <strong>{{ operationalDashboard.overview?.overdueTasks || 0 }}</strong>
              </div>
              <div class="metric-card">
                <span>Over capacity</span>
                <strong>{{ operationalDashboard.overview?.overCapacityMembers || 0 }}</strong>
              </div>
              <div class="metric-card">
                <span>Active milestones</span>
                <strong>{{ operationalDashboard.overview?.activeMilestones || 0 }}</strong>
              </div>
            </div>

            <div class="two-column-grid">
              <div class="helper-panel">
                <div class="section-split">
                  <h3>Top contributors</h3>
                </div>
                <div v-if="!operationalDashboard.topContributors?.length" class="empty-state">Chưa có leaderboard project.</div>
                <div v-else class="stack-list compact-list">
                  <div v-for="member in operationalDashboard.topContributors" :key="`dash-points-${member.userId}`" class="stack-row">
                    <div class="row-main">
                      <strong>{{ member.userName }}</strong>
                      <p>{{ member.projectRole || 'Member' }}</p>
                    </div>
                    <div class="row-actions">
                      <strong>{{ member.totalPoints }} pts</strong>
                    </div>
                  </div>
                </div>
              </div>

              <div class="helper-panel">
                <div class="section-split">
                  <h3>Capacity snapshot</h3>
                </div>
                <div v-if="!operationalDashboard.capacityHealth?.rows?.length" class="empty-state">Chưa có dữ liệu capacity.</div>
                <div v-else class="stack-list compact-list">
                  <div v-for="row in operationalDashboard.capacityHealth.rows" :key="`dash-load-${row.userId}`" class="stack-row">
                    <div class="row-main">
                      <strong>{{ row.userName }}</strong>
                      <p>{{ row.projectRole || 'Member' }} · {{ row.estimatedHours }}h / {{ row.allowedHours }}h · {{ row.activeTaskCount }} task</p>
                    </div>
                    <div class="row-actions">
                      <strong>{{ row.state }}</strong>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <div class="helper-panel">
              <div class="section-split">
                <h3>Milestone snapshot</h3>
              </div>
              <div v-if="!operationalDashboard.milestones?.length" class="empty-state">Chưa có milestone để theo dõi.</div>
              <div v-else class="stack-list">
                <div v-for="milestone in operationalDashboard.milestones" :key="`dash-ms-${milestone.id}`" class="stack-row">
                  <div class="row-main">
                    <strong>{{ milestone.name }}</strong>
                    <p>{{ milestone.releaseVersion || 'No release' }} · {{ milestone.taskCount }} task · {{ milestone.completedTaskCount }} done · {{ milestone.overdueTaskCount }} overdue</p>
                  </div>
                  <div class="row-actions">
                    <strong>{{ milestone.status }}</strong>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div v-else-if="activeTab === 'permissions'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Quản lý phân quyền</h2>
                <p>Thiết lập vai trò và nhóm quyền sử dụng trong dự án SprintA.</p>
              </div>
            </div>

            <!-- Role Selector Tabs -->
            <div class="role-selector-tabs mt-3">
              <button
                v-for="role in ['Owner', 'Admin', 'Manager', 'Member', 'Viewer']"
                :key="role"
                type="button"
                class="role-tab"
                :class="{ active: selectedMatrixRole === role }"
                @click="selectedMatrixRole = role"
              >
                {{ role }}
              </button>
            </div>

            <!-- Permission Matrix Table -->
            <div class="matrix-container mt-3">
              <table class="matrix-table">
                <thead>
                  <tr>
                    <th>Nhóm quyền</th>
                    <th class="text-center">Xem (View)</th>
                    <th class="text-center">Tạo (Create)</th>
                    <th class="text-center">Sửa (Update)</th>
                    <th class="text-center">Xóa (Delete)</th>
                    <th class="text-center">Quyền khác</th>
                  </tr>
                </thead>
                <tbody>
                  <!-- Project -->
                  <tr>
                    <td><strong>Dự án (Project)</strong></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'project.view')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'project.view')" /></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'project.create')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'project.create')" /></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'project.update')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'project.update')" /></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'project.delete')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'project.delete')" /></td>
                    <td>—</td>
                  </tr>
                  <!-- Task -->
                  <tr>
                    <td><strong>Công việc (Task)</strong></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'task.view')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'task.view')" /></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'task.create')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'task.create')" /></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'task.update')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'task.update')" /></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'task.delete')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'task.delete')" /></td>
                    <td>
                      <div class="compact-checkboxes">
                        <label class="compact-label"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'task.assign')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'task.assign')" /> Giao việc</label>
                        <label class="compact-label"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'task.changeStatus')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'task.changeStatus')" /> Trạng thái</label>
                      </div>
                    </td>
                  </tr>
                  <!-- Goals -->
                  <tr>
                    <td><strong>Mục tiêu (Goals)</strong></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'goal.view')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'goal.view')" /></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'goal.create')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'goal.create')" /></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'goal.update')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'goal.update')" /></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'goal.delete')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'goal.delete')" /></td>
                    <td>—</td>
                  </tr>
                  <!-- People -->
                  <tr>
                    <td><strong>Thành viên (People)</strong></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'people.view')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'people.view')" /></td>
                    <td class="text-center">—</td>
                    <td class="text-center">—</td>
                    <td class="text-center">—</td>
                    <td>
                      <div class="compact-checkboxes">
                        <label class="compact-label"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'people.invite')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'people.invite')" /> Mời</label>
                        <label class="compact-label"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'people.updateRole')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'people.updateRole')" /> Vai trò</label>
                        <label class="compact-label"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'people.remove')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'people.remove')" /> Xóa</label>
                      </div>
                    </td>
                  </tr>
                  <!-- Reports -->
                  <tr>
                    <td><strong>Báo cáo (Reports)</strong></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'report.view')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'report.view')" /></td>
                    <td class="text-center">—</td>
                    <td class="text-center">—</td>
                    <td class="text-center">—</td>
                    <td>
                      <label class="compact-label"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'report.export')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'report.export')" /> Xuất dữ liệu</label>
                    </td>
                  </tr>
                  <!-- Settings -->
                  <tr>
                    <td><strong>Cài đặt (Settings)</strong></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'setting.view')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'setting.view')" /></td>
                    <td class="text-center">—</td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'setting.update')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'setting.update')" /></td>
                    <td class="text-center">—</td>
                    <td>—</td>
                  </tr>
                  <!-- Permissions -->
                  <tr>
                    <td><strong>Phân quyền (Permissions)</strong></td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'permission.view')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'permission.view')" /></td>
                    <td class="text-center">—</td>
                    <td class="text-center"><input type="checkbox" :checked="isPermissionChecked(selectedMatrixRole, 'permission.update')" :disabled="!canEditPermissions || selectedMatrixRole === 'Owner'" @change="togglePermission(selectedMatrixRole, 'permission.update')" /></td>
                    <td class="text-center">—</td>
                    <td>—</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- Action buttons -->
            <div class="matrix-actions mt-3">
              <el-button type="info" plain @click="restoreDefaultMatrix" :disabled="!canEditPermissions">Khôi phục mặc định</el-button>
              <el-button type="primary" :loading="savingMatrix" @click="savePermissionMatrix" :disabled="!canEditPermissions">Lưu thay đổi</el-button>
            </div>

            <!-- Project Members Assignment list -->
            <div class="section-split mt-4">
              <h3>Vai trò của thành viên trong dự án</h3>
              <p>Gán vai trò trực tiếp cho các thành viên trong dự án này.</p>
            </div>
            <div v-if="members.length === 0" class="empty-state">Không tìm thấy thành viên nào.</div>
            <div v-else class="stack-list">
              <div v-for="member in members" :key="`permission-member-${member.userId}`" class="stack-row">
                <div class="row-main">
                  <strong>{{ member.fullName || member.email }}</strong>
                  <p>{{ member.email }}</p>
                </div>
                <div class="row-actions">
                  <select :value="member.projectRole" @change="updateMemberRole(member, $event.target.value)" :disabled="!canEditPermissions || member.userId === currentUser?.id">
                    <option v-for="role in projectRoleOptions" :key="`matrix-role-opt-${member.userId}-${role}`" :value="role">{{ role }}</option>
                  </select>
                </div>
              </div>
            </div>
          </div>

          <div v-else-if="activeTab === 'members'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Members & Roles</h2>
                <p>Invite members, adjust roles, and remove access without using system admin.</p>
              </div>
            </div>

            <div class="inline-form invite-grid">
              <label>
                <span>Email</span>
                <input v-model="inviteForm.email" type="email" placeholder="member@company.com" />
              </label>
              <label>
                <span>Role</span>
                <select v-model="inviteForm.role">
                  <option v-for="role in projectRoleOptions" :key="role" :value="role">{{ role }}</option>
                </select>
              </label>
              <button class="secondary-btn" type="button" :disabled="savingInvite" @click="inviteMember">
                {{ savingInvite ? 'Inviting...' : 'Invite member' }}
              </button>
            </div>

            <div v-if="members.length === 0" class="empty-state">No project members found.</div>
            <div v-else class="stack-list">
              <div v-for="member in members" :key="member.userId" class="stack-row">
                <div class="row-main">
                  <strong>{{ member.fullName || member.email }}</strong>
                  <p>{{ member.email }}</p>
                </div>
                <div class="row-actions">
                  <select :value="member.projectRole" @change="updateMemberRole(member, $event.target.value)" :disabled="member.userId === currentUser?.id">
                    <option v-for="role in projectRoleOptions" :key="`${member.userId}-${role}`" :value="role">{{ role }}</option>
                  </select>
                  <button class="danger-outline-btn" type="button" @click="removeMember(member)" :disabled="member.userId === currentUser?.id" :title="member.userId === currentUser?.id ? 'Cannot remove yourself' : ''">Remove</button>
                </div>
              </div>
            </div>
          </div>

          <div v-else-if="activeTab === 'states'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Trạng thái</h2>
                <p>Các trạng thái quy trình dùng riêng cho công việc trong dự án.</p>
              </div>
            </div>

            <div class="inline-form state-grid">
              <label>
                <span>Tên</span>
                <input v-model="newState.name" type="text" placeholder="Bị chặn" />
              </label>
              <label>
                <span>Màu</span>
                <input v-model="newState.colorCode" type="color" />
              </label>
              <label>
                <span>Thứ tự</span>
                <input v-model.number="newState.position" type="number" min="0" />
              </label>
              <button class="secondary-btn" type="button" :disabled="savingState" @click="createState">
                {{ savingState ? 'Đang thêm...' : 'Thêm trạng thái' }}
              </button>
            </div>

            <div class="stack-list">
              <div v-for="status in taskStatuses" :key="status.id" class="stack-row">
                <div class="row-main state-row">
                  <input v-model="status.name" type="text" />
                  <input v-model="status.colorCode" type="color" />
                  <input v-model.number="status.position" type="number" min="0" />
                </div>
                <div class="row-actions">
                  <button class="secondary-btn" type="button" @click="saveState(status)">Lưu</button>
                  <button class="danger-outline-btn" type="button" @click="deleteState(status)">Xóa</button>
                </div>
              </div>
            </div>
          </div>

          <div v-else-if="activeTab === 'labels'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Labels</h2>
                <p>Reusable project labels for classification and planning.</p>
              </div>
            </div>

            <div class="inline-form label-grid">
              <label>
                <span>Name</span>
                <input v-model="newLabel.name" type="text" placeholder="Customer" />
              </label>
              <label>
                <span>Color</span>
                <input v-model="newLabel.colorCode" type="color" />
              </label>
              <label class="wide">
                <span>Description</span>
                <input v-model="newLabel.description" type="text" placeholder="Optional description" />
              </label>
              <button class="secondary-btn" type="button" :disabled="savingLabel" @click="createLabel">
                {{ savingLabel ? 'Adding...' : 'Add label' }}
              </button>
            </div>

            <div v-if="labels.length === 0" class="empty-state">No labels configured yet.</div>
            <div v-else class="stack-list">
              <div v-for="label in labels" :key="label.id" class="stack-row">
                <div class="row-main editable-grid compact-grid">
                  <label>
                    <span>Name</span>
                    <input v-model="label.name" type="text" />
                  </label>
                  <label>
                    <span>Color</span>
                    <input v-model="label.colorCode" type="color" />
                  </label>
                  <label class="wide">
                    <span>Description</span>
                    <input v-model="label.description" type="text" placeholder="Optional description" />
                  </label>
                </div>
                <div class="row-actions">
                  <button class="secondary-btn" type="button" @click="saveLabel(label)">Save</button>
                  <button class="danger-outline-btn" type="button" @click="deleteLabel(label)">Delete</button>
                </div>
              </div>
            </div>
          </div>

          <div v-else-if="activeTab === 'modules'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Modules</h2>
                <p>Manage project modules and their ownership.</p>
              </div>
              <button class="secondary-btn" type="button" @click="goToModulesWorkspace">Open modules workspace</button>
            </div>

            <div class="inline-form module-grid">
              <label>
                <span>Name</span>
                <input v-model="newModule.name" type="text" placeholder="Platform" />
              </label>
              <label>
                <span>Status</span>
                <select v-model="newModule.status">
                  <option value="Backlog">Backlog</option>
                  <option value="Planned">Planned</option>
                  <option value="In Progress">In Progress</option>
                  <option value="Paused">Paused</option>
                  <option value="Completed">Completed</option>
                </select>
              </label>
              <label>
                <span>Lead</span>
                <select v-model="newModule.leadId">
                  <option :value="null">No lead</option>
                  <option v-for="member in members" :key="member.userId" :value="member.userId">
                    {{ member.fullName || member.email }}
                  </option>
                </select>
              </label>
              <label>
                <span>Start date</span>
                <input v-model="newModule.startDate" type="date" :min="todayDate" />
              </label>
              <label>
                <span>Target date</span>
                <input v-model="newModule.targetDate" type="date" :min="newModule.startDate || todayDate" />
              </label>
              <label class="wide">
                <span>Description</span>
                <input v-model="newModule.description" type="text" placeholder="Optional description" />
              </label>
              <button class="secondary-btn" type="button" :disabled="savingModule" @click="createModule">
                {{ savingModule ? 'Adding...' : 'Add module' }}
              </button>
            </div>

              <div class="helper-panel">
                <div class="meta-strip compact">
                  <span>Active modules: {{ activeModules.length }}</span>
                  <span>Disabled modules: {{ disabledModules.length }}</span>
                  <span>Disabled modules are shown in the restore section below.</span>
                </div>
              </div>

              <div class="helper-panel">
                <div class="section-split">
                  <h3>Restore modules</h3>
                  <p v-if="disabledModules.length">Disabled modules appear here first so you can restore them quickly.</p>
                  <p v-else>No disabled modules yet. After you disable one, it will appear here.</p>
                </div>
                <div v-if="disabledModules.length" class="stack-list">
                  <div v-for="module in disabledModules" :key="`restore-top-${module.id}`" class="stack-row">
                    <div class="row-main">
                      <strong>{{ module.name }}</strong>
                      <p>{{ module.description || 'Disabled module ready to restore.' }}</p>
                    </div>
                    <div class="row-actions">
                      <button class="secondary-btn" type="button" @click="restoreModule(module)">Restore</button>
                    </div>
                  </div>
                </div>
              </div>

              <div v-if="modules.length === 0" class="empty-state">No modules configured yet.</div>
              <template v-else>
                <div class="section-split">
                  <h3>Active modules</h3>
                  <p>Save updates normally. Disabled modules move to the restore list below.</p>
                </div>
                <div class="stack-list">
                  <div v-for="module in activeModules" :key="module.id" class="stack-row">
                    <div class="row-main editable-grid">
                      <label>
                        <span>Name</span>
                        <input v-model="module.name" type="text" />
                      </label>
                      <label>
                        <span>Status</span>
                        <select v-model="module.status">
                          <option value="Backlog">Backlog</option>
                          <option value="Planned">Planned</option>
                          <option value="In Progress">In Progress</option>
                          <option value="Paused">Paused</option>
                          <option value="Completed">Completed</option>
                          <option value="Disabled">Disabled</option>
                        </select>
                      </label>
                      <label>
                        <span>Lead</span>
                        <select v-model="module.leadId">
                          <option :value="null">No lead</option>
                          <option v-for="member in members" :key="`${module.id}-${member.userId}`" :value="member.userId">
                            {{ member.fullName || member.email }}
                          </option>
                        </select>
                      </label>
                      <label>
                        <span>Start date</span>
                        <input v-model="module.startDate" type="date" :min="todayDate" />
                      </label>
                      <label>
                        <span>Target date</span>
                        <input v-model="module.targetDate" type="date" :min="module.startDate || todayDate" />
                      </label>
                      <label class="wide">
                        <span>Description</span>
                        <input v-model="module.description" type="text" placeholder="Optional description" />
                      </label>
                      <div class="meta-strip compact">
                        <span>Lead: {{ module.leadName || 'Unassigned' }}</span>
                        <span>Work items: {{ module.issueCount || 0 }}</span>
                        <span>Progress: {{ module.progressPercent || 0 }}%</span>
                      </div>
                    </div>
                    <div class="row-actions">
                      <button class="secondary-btn" type="button" @click="saveModule(module)">Save</button>
                      <button class="danger-outline-btn" type="button" @click="deleteModule(module)">Disable</button>
                    </div>
                  </div>
                </div>

                <div class="section-split">
                  <h3>Disabled modules</h3>
                  <p>Restore here when you want the module to appear again in project workspaces.</p>
                </div>
                <div v-if="disabledModules.length === 0" class="empty-state">No disabled modules.</div>
                <div v-else class="stack-list">
                  <div v-for="module in disabledModules" :key="`disabled-${module.id}`" class="stack-row">
                    <div class="row-main editable-grid">
                      <label>
                        <span>Name</span>
                        <input v-model="module.name" type="text" />
                      </label>
                      <label>
                        <span>Status</span>
                        <select v-model="module.status">
                          <option value="Disabled">Disabled</option>
                          <option value="Backlog">Backlog</option>
                          <option value="Planned">Planned</option>
                          <option value="In Progress">In Progress</option>
                          <option value="Paused">Paused</option>
                          <option value="Completed">Completed</option>
                        </select>
                      </label>
                      <label>
                        <span>Lead</span>
                        <select v-model="module.leadId">
                          <option :value="null">No lead</option>
                          <option v-for="member in members" :key="`${module.id}-restore-${member.userId}`" :value="member.userId">
                            {{ member.fullName || member.email }}
                          </option>
                        </select>
                      </label>
                      <label>
                        <span>Start date</span>
                        <input v-model="module.startDate" type="date" :min="todayDate" />
                      </label>
                      <label>
                        <span>Target date</span>
                        <input v-model="module.targetDate" type="date" :min="module.startDate || todayDate" />
                      </label>
                      <label class="wide">
                        <span>Description</span>
                        <input v-model="module.description" type="text" placeholder="Optional description" />
                      </label>
                      <div class="meta-strip compact">
                        <span>Lead: {{ module.leadName || 'Unassigned' }}</span>
                        <span>Work items: {{ module.issueCount || 0 }}</span>
                        <span>Progress: {{ module.progressPercent || 0 }}%</span>
                      </div>
                    </div>
                    <div class="row-actions">
                      <button class="secondary-btn" type="button" @click="saveModule(module)">Save</button>
                      <button class="secondary-btn" type="button" @click="restoreModule(module)">Restore</button>
                    </div>
                  </div>
                </div>
              </template>
            </div>

          <div v-else-if="activeTab === 'cycles'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Cycles</h2>
                <p>Plan cycles from project settings and jump into carry-over planning.</p>
              </div>
              <button class="secondary-btn" type="button" @click="goToCyclesWorkspace">Open cycles workspace</button>
            </div>

            <div v-if="canManageSprint" class="inline-form cycle-grid">
              <label>
                <span>Name</span>
                <input v-model="newCycle.name" type="text" placeholder="Cycle 12" />
              </label>
              <label>
                <span>Start date</span>
                <input v-model="newCycle.startDate" type="date" :min="todayDate" />
              </label>
              <label>
                <span>End date</span>
                <input v-model="newCycle.endDate" type="date" :min="newCycle.startDate || todayDate" />
              </label>
              <label class="wide">
                <span>Description</span>
                <input v-model="newCycle.description" type="text" placeholder="Optional description" />
              </label>
              <button class="secondary-btn" type="button" :disabled="savingCycle" @click="createCycle">
                {{ savingCycle ? 'Adding...' : 'Add cycle' }}
              </button>
            </div>

            <div v-if="sprints.length === 0" class="empty-state">No cycles configured yet.</div>
            <div v-else class="stack-list">
              <div v-for="sprint in sprints" :key="sprint.id" class="stack-row">
                <div class="row-main editable-grid compact-grid">
                  <label>
                    <span>Name</span>
                    <input v-model="sprint.name" type="text" />
                  </label>
                  <label>
                    <span>Start date</span>
                    <input v-model="sprint.startDate" type="date" :min="todayDate" />
                  </label>
                  <label>
                    <span>End date</span>
                    <input v-model="sprint.endDate" type="date" :min="sprint.startDate || todayDate" />
                  </label>
                  <div class="meta-strip compact">
                    <span>State: {{ getSprintStateMeta(sprint.state).label }}</span>
                    <span>Favorite: {{ sprint.isFavorite ? 'Yes' : 'No' }}</span>
                    <span>Progress: {{ sprint.progressPercent || 0 }}%</span>
                    <span>Tasks: {{ sprint.taskCount || 0 }}</span>
                  </div>
                </div>
                <div class="row-actions">
                  <button v-if="canManageSprint" class="secondary-btn" type="button" @click="saveCycle(sprint)">Save</button>
                  <button class="secondary-btn" type="button" @click="toggleFavoriteCycle(sprint)">
                    {{ sprint.isFavorite ? 'Unfavorite' : 'Favorite' }}
                  </button>
                  <button
                    v-if="canManageSprint && getSprintStateMeta(sprint.state).canStart"
                    class="secondary-btn"
                    type="button"
                    :disabled="isCycleTransitioning('start', sprint.id)"
                    :aria-label="`Start Cycle ${sprint.name}`"
                    @click="startCycle(sprint)"
                  >
                    {{ isCycleTransitioning('start', sprint.id) ? 'Starting...' : 'Start' }}
                  </button>
                  <button
                    v-if="canManageSprint && getSprintStateMeta(sprint.state).canClose"
                    class="secondary-btn"
                    type="button"
                    :disabled="isCycleTransitioning('close', sprint.id)"
                    :aria-label="`Close Cycle ${sprint.name}`"
                    @click="closeCycleToBacklog(sprint)"
                  >
                    {{ isCycleTransitioning('close', sprint.id) ? 'Closing...' : 'Close to backlog' }}
                  </button>
                  <button v-if="canManageSprint" class="secondary-btn" type="button" @click="openCycleCarryOver(sprint)">Carry-over</button>
                </div>
              </div>
            </div>
          </div>

          <div v-else-if="activeTab === 'integrations'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Integrations</h2>
                <p>Configure provider connections at project scope without opening system admin settings.</p>
              </div>
            </div>

            <div v-if="integrations.length === 0" class="empty-state">
              GitHub integration is not configured for this project yet.
            </div>
            <div v-else class="stack-list">
              <div v-for="integration in integrations" :key="integration.provider" class="stack-row">
                <div class="row-main editable-grid integration-grid">
                  <label>
                    <span>Provider</span>
                    <input :value="integration.displayName" type="text" disabled />
                  </label>
                  <label>
                    <span>Enabled</span>
                    <select v-model="integration.enabled">
                      <option :value="true">Enabled</option>
                      <option :value="false">Disabled</option>
                    </select>
                  </label>
                  <label>
                    <span>Repository</span>
                    <input v-model="integration.projectKey" type="text" placeholder="owner/repository" />
                  </label>
                  <label class="wide">
                    <span>API endpoint</span>
                    <input v-model="integration.endpoint" type="text" :placeholder="httpsPlaceholders[integration.provider] || 'https://api.github.com/repos/owner/repository'" />
                  </label>
                  <label>
                    <span>Access token</span>
                    <input v-model="integration.secret" type="password" placeholder="Stored only for this project" />
                  </label>
                  <label class="wide">
                    <span>Notes</span>
                    <input v-model="integration.notes" type="text" placeholder="Used by AI repo analysis and task breakdown" />
                  </label>
                  <div class="meta-strip compact">
                    <span>Provider: {{ integration.provider }}</span>
                    <span>Updated: {{ formatDateLabel(integration.updatedAt) }}</span>
                  </div>
                </div>
                <div class="row-actions">
                  <button class="secondary-btn" type="button" :disabled="analyzingIntegration === integration.provider" @click="analyzeIntegration(integration)">
                    {{ analyzingIntegration === integration.provider ? 'Analyzing...' : 'Analyze with AI' }}
                  </button>
                  <button class="secondary-btn" type="button" @click="openIntegrationInAi(integration)">Open AI planner</button>
                  <button class="secondary-btn" type="button" @click="resetIntegration(integration.provider)">Reset</button>
                </div>
              </div>
            </div>

            <div class="danger-actions integration-actions">
              <button class="secondary-btn" type="button" :disabled="savingIntegrations" @click="loadIntegrations">
                Reload integrations
              </button>
              <button class="primary-btn" type="button" :disabled="savingIntegrations" @click="saveIntegrations">
                {{ savingIntegrations ? 'Saving...' : 'Save integrations' }}
              </button>
            </div>

            <div class="empty-state helper-panel">
              Only GitHub is available here so AI can analyze the connected repository and break work into tasks later.
              For public repos you can try without a token first, but if GitHub returns 403 you should add a Fine-grained Personal Access Token with read-only access to Metadata and Contents.
            </div>

            <div v-if="integrationAnalysis" class="helper-panel">
              <div class="section-split">
                <h3>AI repo analysis</h3>
                <p>{{ integrationAnalysis.repository }}</p>
              </div>
              <div class="meta-strip compact">
                <span>{{ integrationAnalysis.summary }}</span>
              </div>
              <div class="integration-analysis-grid">
                <div class="analysis-card">
                  <strong>Quick wins</strong>
                  <ul>
                    <li v-for="item in integrationAnalysis.quickWins" :key="`quick-${item.title}`">{{ item.title }} · {{ item.suggestedHours }}h</li>
                  </ul>
                </div>
                <div class="analysis-card">
                  <strong>Medium tasks</strong>
                  <ul>
                    <li v-for="item in integrationAnalysis.mediumTasks" :key="`medium-${item.title}`">{{ item.title }} · {{ item.suggestedHours }}h</li>
                  </ul>
                </div>
                <div class="analysis-card">
                  <strong>Risky tasks</strong>
                  <ul>
                    <li v-for="item in integrationAnalysis.riskyTasks" :key="`risk-${item.title}`">{{ item.title }} · {{ item.suggestedHours }}h</li>
                  </ul>
                </div>
              </div>
              <div class="danger-actions integration-actions">
                <button class="secondary-btn" type="button" @click="useIntegrationPrompt">Use prompt in AI page</button>
              </div>
            </div>
          </div>

          <div v-else-if="activeTab === 'custom-fields'" class="settings-card">
            <div class="card-head">
              <div>
                <h2>Trường tùy chỉnh</h2>
                <p>Tạo các trường riêng để bổ sung thông tin cho công việc trong dự án này.</p>
              </div>
              <button class="secondary-btn" type="button" @click="openCreateCustomField">
                <i class="fa-solid fa-plus mr-1"></i> Thêm trường
              </button>
            </div>

            <!-- Empty State -->
            <div v-if="!customFields.length" class="empty-state-container" style="padding: 40px; text-align: center; color: var(--color-text-muted);">
              <i class="fa-solid fa-folder-open" style="font-size: 48px; margin-bottom: 16px; color: var(--border-color);"></i>
              <p>Dự án này chưa có trường tùy chỉnh. Hãy tạo trường đầu tiên để bổ sung thông tin riêng cho công việc.</p>
            </div>

            <!-- List Table -->
            <div v-else class="table-container">
              <table class="plane-table">
                <thead>
                  <tr>
                    <th>Tên trường</th>
                    <th>Loại dữ liệu</th>
                    <th>Bắt buộc</th>
                    <th>Hiển thị</th>
                    <th>Thứ tự</th>
                    <th style="width: 150px; text-align: right;">Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="field in customFields" :key="field.id">
                    <td><strong>{{ field.name }}</strong> <code style="font-size: 11px; color: var(--color-text-muted);">({{ field.key }})</code></td>
                    <td><span class="badge" style="background: var(--bg-primary); border: 1px solid var(--border-color); color: var(--text-primary); font-size: 12px; padding: 2px 8px; border-radius: 4px;">{{ field.type }}</span></td>
                    <td>
                      <i v-if="field.isRequired" class="fa-solid fa-circle-check text-green-500"></i>
                      <i v-else class="fa-regular fa-circle text-gray-300"></i>
                    </td>
                    <td>
                      <i v-if="field.isVisible" class="fa-solid fa-eye text-sky-500" title="Hiển thị ở task detail"></i>
                      <i v-else class="fa-solid fa-eye-slash text-gray-300" title="Ẩn ở task detail"></i>
                    </td>
                    <td>{{ field.sortOrder }}</td>
                    <td style="text-align: right;">
                      <button class="secondary-btn small-btn mr-1" type="button" @click="openEditCustomField(field)">Sửa</button>
                      <button class="danger-outline-btn small-btn" type="button" @click="deleteCustomField(field)">Xóa</button>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <div v-else class="settings-card danger-card">
            <div class="card-head">
              <div>
                <h2>Danger Zone</h2>
                <p>Archive, restore, or delete this project.</p>
              </div>
            </div>

            <div class="danger-actions">
              <button class="danger-outline-btn" type="button" @click="toggleArchive">
                {{ project.isArchived ? 'Restore project' : 'Archive project' }}
              </button>
              <button class="danger-btn" type="button" @click="deleteProject">Delete project</button>
            </div>
          </div>
        </section>
      </div>
    </div>

    <!-- Dialog thêm/sửa trường tùy chỉnh -->
    <el-dialog
      v-model="showCustomFieldModal"
      :title="isEditingCustomField ? 'Sửa trường tùy chỉnh' : 'Thêm trường tùy chỉnh'"
      width="480px"
      custom-class="plane-dialog"
    >
      <div class="dialog-form">
        <div class="form-group">
          <label class="form-label">Tên trường <span class="text-red-500">*</span></label>
          <input type="text" class="form-control" v-model="customFieldForm.name" placeholder="Ví dụ: Mã khách hàng, Ngày bàn giao" />
        </div>
        <div class="form-group">
          <label class="form-label">Loại dữ liệu <span class="text-red-500">*</span></label>
          <el-select v-model="customFieldForm.type" placeholder="Chọn loại dữ liệu" style="width: 100%;" :disabled="isEditingCustomField">
            <el-option label="Văn bản (Text)" value="Text" />
            <el-option label="Số (Number)" value="Number" />
            <el-option label="Ngày (Date)" value="Date" />
            <el-option label="Lựa chọn (Select)" value="Select" />
            <el-option label="Hộp kiểm (Checkbox)" value="Checkbox" />
          </el-select>
        </div>
        <div class="form-group" v-if="customFieldForm.type === 'Select'">
          <label class="form-label">Tùy chọn (Options) <span class="text-red-500">*</span></label>
          <textarea class="form-control" v-model="customFieldForm.optionsText" rows="4" placeholder="Nhập mỗi tùy chọn trên một dòng (Ví dụ:&#10;Thấp&#10;Trung bình&#10;Cao)"></textarea>
        </div>
        <div class="form-group flex items-center gap-4" style="display: flex; gap: 24px; margin-top: 8px;">
          <el-checkbox v-model="customFieldForm.isRequired">Bắt buộc nhập</el-checkbox>
          <el-checkbox v-model="customFieldForm.isVisible">Hiển thị trên Task Detail</el-checkbox>
        </div>
        <div class="form-group">
          <label class="form-label">Thứ tự hiển thị (Sort Order)</label>
          <input type="number" class="form-control" v-model.number="customFieldForm.sortOrder" min="0" />
        </div>
      </div>
      <template #footer>
        <span class="dialog-footer" style="display: flex; justify-content: flex-end; gap: 8px; margin-top: 16px;">
          <el-button @click="showCustomFieldModal = false">Hủy</el-button>
          <el-button type="primary" :loading="savingCustomField" @click="saveCustomField">Lưu thay đổi</el-button>
        </span>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'

import axiosClient from '@/api/axiosClient'
import { useProjectStore } from '@/store/useProjectStore'
import { useSprintStore } from '@/store/useSprintStore'
import { broadcastAdminRealtime, subscribeAdminRealtime } from '@/utils/adminRealtime'
import { signalRService } from '@/api/signalrService'

import { getStoredUserSession } from '@/utils/authSession'
import { getDefaultPermissionMatrix, normalizeRole } from '@/utils/permissionGuard'
import { hasProjectWritePermission } from '@/utils/permissions'
import { getSprintApiError, getSprintErrorMessage, getSprintStateMeta, normalizeSprintState } from '@/utils/sprintState'
import { clearLegacyGitHubCredentialStorage, runWithEphemeralGitHubToken } from '@/utils/githubCredentials'

const route = useRoute()
const router = useRouter()
const projectStore = useProjectStore()
const sprintStore = useSprintStore()
const projectId = route.params.id

const tabs = [
  { key: 'general', label: 'Tổng quan', caption: 'thông tin' },
  { key: 'execution', label: 'Quy tắc thực thi', caption: 'quyền xem' },
  { key: 'points', label: 'Điểm dự án', caption: 'điểm + chỉnh tay' },
  { key: 'rewardRules', label: 'Thưởng / phạt', caption: 'bonus + phạt' },
  { key: 'milestones', label: 'Cột mốc', caption: 'theo dõi phát hành' },
  { key: 'capacity', label: 'Tải công việc', caption: 'ngưỡng workload' },
  { key: 'dashboard', label: 'Bảng vận hành', caption: 'sức khoẻ dự án' },
  { key: 'permissions', label: 'Quản lý phân quyền', caption: 'vai trò & nhóm quyền' },
  { key: 'members', label: 'Thành viên & vai trò', caption: 'quyền truy cập' },
  { key: 'states', label: 'Trạng thái', caption: 'quy trình' },
  { key: 'labels', label: 'Nhãn', caption: 'phân loại' },
  { key: 'cycles', label: 'Chu kỳ', caption: 'vòng lặp' },
  { key: 'integrations', label: 'Tích hợp', caption: 'kết nối dự án' },
  { key: 'custom-fields', label: 'Trường tùy chỉnh', caption: 'cấu hình trường riêng' },
  { key: 'danger', label: 'Vùng nguy hiểm', caption: 'thao tác nhạy cảm' }
]

const projectRoleOptions = ref([])

const activeTab = ref('general')
const loading = ref(false)
const savingGeneral = ref(false)
const savingCover = ref(false)
const savingExecutionRules = ref(false)
const savingRewardRules = ref(false)
const savingCapacityRules = ref(false)
const savingBaselineSettings = ref(false)
const savingPointAdjustment = ref(false)
const savingMilestone = ref(false)
const savingInvite = ref(false)
const savingState = ref(false)
const savingLabel = ref(false)
const savingModule = ref(false)
const savingCycle = ref(false)
const savingIntegrations = ref(false)
const analyzingIntegration = ref('')

const project = ref({})
const coverInputRef = ref(null)
const coverFile = ref(null)
const coverPreviewUrl = ref('')
const coverError = ref('')
const members = ref([])
const taskStatuses = ref([])
const labels = ref([])
const modules = ref([])
const sprints = ref([])
const integrations = ref([])
const integrationAnalysis = ref(null)
const pointManagement = ref({
  totalProjectPoints: 0,
  totalManualAdjustments: 0,
  leaderboard: [],
  history: []
})
const milestones = ref([])
const customFields = ref([])
const savingCustomField = ref(false)
const showCustomFieldModal = ref(false)
const isEditingCustomField = ref(false)
const customFieldForm = ref({
  id: null,
  name: '',
  type: 'Text',
  isRequired: false,
  optionsText: '',
  isVisible: true,
  sortOrder: 0
})
const operationalDashboard = ref({
  overview: {},
  rewardHealth: {},
  capacityHealth: { rows: [] },
  baselineHealth: {},
  milestones: [],
  topContributors: []
})
const activeModules = computed(() => modules.value.filter(module => module.status !== 'Disabled'))
const disabledModules = computed(() => modules.value.filter(module => module.status === 'Disabled'))

const generalForm = ref({
  name: '',
  key: '',
  description: '',
  startDate: '',
  endDate: ''
})

const allowedCoverTypes = new Set(['image/png', 'image/jpeg', 'image/webp'])
const maxCoverSize = 5 * 1024 * 1024
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5136/api'
const apiOrigin = new URL(apiBaseUrl, window.location.origin).origin
const rawProjectCover = computed(() => {
  const value = project.value.cover || project.value.coverUrl || project.value.CoverUrl || ''
  return typeof value === 'string' && (/^(https?:|data:image|blob:)/i.test(value) || value.startsWith('/'))
    ? value
    : ''
})
const displayCoverUrl = computed(() => {
  if (coverPreviewUrl.value) return coverPreviewUrl.value
  if (rawProjectCover.value.startsWith('/uploads/')) return `${apiOrigin}${rawProjectCover.value}`
  return rawProjectCover.value
})
const generatedCoverAltText = computed(() => `Ảnh đại diện dự án ${generalForm.value.name.trim() || project.value.name || 'SprintA'}`)
const coverPreviewStyle = computed(() => displayCoverUrl.value
  ? {
      backgroundImage: `linear-gradient(rgba(15, 23, 42, 0.08), rgba(15, 23, 42, 0.38)), url("${displayCoverUrl.value}")`
    }
  : { background: 'linear-gradient(135deg, #0f766e 0%, #2563eb 100%)' })

const revokeCoverPreview = () => {
  if (coverPreviewUrl.value) {
    URL.revokeObjectURL(coverPreviewUrl.value)
    coverPreviewUrl.value = ''
  }
}

const clearSelectedCover = () => {
  revokeCoverPreview()
  coverFile.value = null
  coverError.value = ''
  if (coverInputRef.value) coverInputRef.value.value = ''
}

const handleCoverSelected = (event) => {
  const [file] = event.target.files || []
  coverError.value = ''
  if (!file) return
  if (!allowedCoverTypes.has(file.type)) {
    coverError.value = 'Project cover must be PNG, JPG, JPEG, or WEBP.'
    event.target.value = ''
    return
  }
  if (file.size > maxCoverSize) {
    coverError.value = 'Project cover must be 5MB or smaller.'
    event.target.value = ''
    return
  }

  revokeCoverPreview()
  coverFile.value = file
  coverPreviewUrl.value = URL.createObjectURL(file)
}

// ────────────────────────────────────────────
// SME Permission Matrix State Variables
// ────────────────────────────────────────────
const currentUser = computed(() => getStoredUserSession() || {})
const selectedMatrixRole = ref('Manager')
const localMatrix = ref(getDefaultPermissionMatrix())
const savingMatrix = ref(false)

const currentUserProjectRole = computed(() => {
  const me = members.value.find(m => m.userId === currentUser.value?.id)
  return me?.projectRole || 'Member'
})

const canManageSprint = computed(() => {
  return hasProjectWritePermission(currentUserProjectRole.value)
})
const isCycleTransitioning = (action, sprintId) =>
  sprintStore.isTransitioning(action, projectId, sprintId)

const canEditPermissions = computed(() => {
  const user = currentUser.value
  if (!user) return false
  
  const wsRole = user.workspaceRole?.toUpperCase()
  if (wsRole === 'OWNER' || wsRole === 'ADMIN') return true

  const pRole = normalizeRole(currentUserProjectRole.value)
  return ['PM', 'PO', 'PROJECT_MANAGER', 'PROJECT_LEAD', 'ADMIN'].includes(pRole)
})

const isPermissionChecked = (role, permissionKey) => {
  if (role === 'Owner') return true
  const list = localMatrix.value[role] || []
  return list.includes(permissionKey)
}

const togglePermission = (role, permissionKey) => {
  if (role === 'Owner') return
  if (!localMatrix.value[role]) {
    localMatrix.value[role] = []
  }
  const list = localMatrix.value[role]
  const idx = list.indexOf(permissionKey)
  if (idx > -1) {
    list.splice(idx, 1)
  } else {
    list.push(permissionKey)
  }
}

const executionRulesForm = ref({
  enableRoleBasedTaskVisibility: false,
  managerAlwaysSeeAllTasks: true,
  defaultTaskVisibilityMode: 'project',
  defaultBaseHours: 4,
  hoursPerStoryPoint: 2,
  estimateBaselineMode: 'role_then_project',
  roleHourMultipliers: {
    dev: 1,
    developer: 1,
    qa: 0.75,
    designer: 0.9,
    devops: 1.15,
    pm: 0.9,
    po: 0.8,
    sm: 0.8
  }
})

const rewardRulesForm = ref({
  basePointMultiplier: 1,
  earlyBonusPercent: 10,
  accuracyBonusPercent: 5,
  latePenaltyPercent: 10,
  collaborationBonusPoints: 2,
  manualAdjustmentLimit: 200
})

const capacityRulesForm = ref({
  defaultWeeklyHours: 40,
  nearLimitPercent: 80,
  overLimitPercent: 100,
  maxActiveTasksPerMember: 8,
  roleWeeklyHours: {
    pm: 40,
    po: 32,
    sm: 32,
    project_lead: 40,
    developer: 40,
    qa: 36,
    designer: 32,
    devops: 36,
    admin: 40,
    accountant: 32
  },
  roleActiveTaskLimits: {
    pm: 10,
    po: 10,
    sm: 10,
    project_lead: 8,
    developer: 7,
    qa: 7,
    designer: 6,
    devops: 6,
    admin: 10,
    accountant: 6
  }
})

const baselineSettingsForm = ref({
  usePlanningBaseline: true,
  defaultBaseHours: 4,
  hoursPerStoryPoint: 2,
  minimumSuggestedHours: 0.5,
  maximumSuggestedHours: 80,
  roleHourMultipliers: {
    pm: 0.9,
    po: 0.8,
    sm: 0.8,
    project_lead: 1.05,
    developer: 1,
    qa: 0.75,
    designer: 0.9,
    devops: 1.15,
    admin: 1,
    accountant: 0.8
  },
  roleBaseHours: {
    pm: 3.5,
    po: 3,
    sm: 3,
    project_lead: 4.5,
    developer: 4,
    qa: 3,
    designer: 3.5,
    devops: 4.5,
    admin: 3.5,
    accountant: 3
  }
})

const pointAdjustmentForm = ref({
  userId: null,
  amount: 0,
  reason: '',
  adjustmentType: 'Manual'
})

const inviteForm = ref({
  email: '',
  role: 'Developer'
})

const newState = ref({
  name: '',
  colorCode: '#3b82f6',
  position: 0
})

const newLabel = ref({
  name: '',
  colorCode: '#3b82f6',
  description: ''
})

const newModule = ref({
  name: '',
  description: '',
  status: 'Backlog',
  leadId: null,
  startDate: '',
  targetDate: ''
})

const newCycle = ref({
  name: '',
  description: '',
  startDate: '',
  endDate: ''
})

const newMilestone = ref({
  name: '',
  description: '',
  releaseVersion: '',
  startDate: '',
  targetDate: '',
  status: 'Planned',
  linkedSprintIds: []
})

const httpsPlaceholders = {
  github: 'https://api.github.com/repos/org/repo'
}

const todayDate = new Date().toISOString().slice(0, 10)

const normalizeDateInput = (value) => {
  if (!value) return ''
  const raw = String(value)
  return raw.includes('T') ? raw.slice(0, 10) : raw.slice(0, 10)
}

const toLocalDateTime = (value) => (value ? `${value}T00:00:00` : null)

const formatDateLabel = (value) => {
  if (!value) return 'No date'
  const raw = String(value).split('T')[0]
  const [year, month, day] = raw.split('-').map(Number)
  if (!year || !month || !day) return raw
  return new Date(year, month - 1, day).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  })
}

const normalizeIntegration = (integration) => ({
  provider: integration.provider,
  displayName: integration.displayName || integration.provider,
  enabled: Boolean(integration.enabled),
  endpoint: integration.endpoint || '',
  projectKey: integration.projectKey || '',
  secret: integration.secret || '',
  notes: integration.notes || '',
  updatedAt: integration.updatedAt || ''
})

const preferredInviteRole = computed(() => {
  if (projectRoleOptions.value.includes('Developer')) return 'Developer'
  if (projectRoleOptions.value.includes('QA')) return 'QA'
  return projectRoleOptions.value[0] || 'Developer'
})

const assignProjectRoleOptions = (items = []) => {
  const nextOptions = items
    .map(item => item?.value || item?.name || item?.label)
    .filter(Boolean)

  if (nextOptions.length) {
    projectRoleOptions.value = [...new Set(nextOptions)]
  }
}

const buildIntegrationRepoUrl = (integration) => {
  const projectKey = integration.projectKey?.trim() || ''
  if (projectKey) {
    if (/^https?:\/\//i.test(projectKey)) {
      return projectKey
    }
    return `https://github.com/${projectKey}`
  }

  const endpoint = integration.endpoint?.trim() || ''
  if (/^https?:\/\/github\.com\//i.test(endpoint)) {
    return endpoint
  }
  const match = endpoint.match(/repos\/([^/]+\/[^/?#]+)/i)
  if (match) {
    return `https://github.com/${match[1]}`
  }

  return ''
}

const stashAiPlannerPayload = ({ repoUrl, prompt, analysis }) => {
  sessionStorage.setItem('sprinta-ai-repo-url', repoUrl || '')
  sessionStorage.setItem('sprinta-ai-prefill-message', prompt || '')
  sessionStorage.setItem('sprinta-ai-repo-analysis', JSON.stringify(analysis || null))
}

const isPastDate = (value) => {
  if (!value) return false
  return normalizeDateInput(value) < todayDate
}

const normalizeModuleStatus = (status) => {
  const value = `${status || ''}`.trim().toLowerCase()
  if (!value) return 'Backlog'
  if (value === 'disabled') return 'Disabled'
  if (value === 'planned') return 'Planned'
  if (value === 'in progress' || value === 'inprogress') return 'In Progress'
  if (value === 'paused') return 'Paused'
  if (value === 'completed' || value === 'complete') return 'Completed'
  return 'Backlog'
}

const notifyProjectSettingsRealtime = (type = 'project-settings-updated') => {
  const payload = { projectId, source: 'project-settings' }
  broadcastAdminRealtime(type, payload)
  signalRService.sendProjectEvent(`${projectId}`, type, payload)
}

const loadProjectSettings = async () => {
  loading.value = true
  try {
    const [settingsRes, labelsRes, modulesRes, cyclesRes, statusesRes, integrationsRes, executionRulesRes, rewardRulesRes, capacityRulesRes, baselineSettingsRes, milestonesRes, pointsRes, dashboardRes, customFieldsRes] = await Promise.all([
      axiosClient.get(`/projects/${projectId}/settings`),
      axiosClient.get(`/projects/${projectId}/labels`),
      axiosClient.get(`/projects/${projectId}/modules`, { params: { page: 1, pageSize: 50, includeDisabled: true } }),
      axiosClient.get(`/projects/${projectId}/sprints`),
      axiosClient.get(`/projects/${projectId}/task-statuses`),
      axiosClient.get(`/projects/${projectId}/integrations`),
      axiosClient.get(`/projects/${projectId}/execution-rules`),
      axiosClient.get(`/projects/${projectId}/management/reward-rules`),
      axiosClient.get(`/projects/${projectId}/management/capacity-rules`),
      axiosClient.get(`/projects/${projectId}/management/baseline-settings`),
      axiosClient.get(`/projects/${projectId}/management/milestones`),
      axiosClient.get(`/projects/${projectId}/management/points`),
      axiosClient.get(`/projects/${projectId}/management/operational-dashboard`),
      axiosClient.get(`/projects/${projectId}/custom-fields`).catch(() => ({ data: { data: [] } }))
    ])

    const settings = settingsRes.data?.data || {}
    project.value = settings.project || {}
    members.value = settings.members || []
    assignProjectRoleOptions(settings.roleOptions || [])
    labels.value = (labelsRes.data?.data || []).map(label => ({
      ...label,
      colorCode: label.colorCode || label.color || '#3b82f6'
    }))
    modules.value = (modulesRes.data?.data || []).map(module => ({
      ...module,
      status: normalizeModuleStatus(module.status),
      startDate: normalizeDateInput(module.startDate),
      targetDate: normalizeDateInput(module.targetDate),
      leadId: module.leadId || null
    }))
    sprints.value = (cyclesRes.data?.data || []).map(sprint => ({
      ...sprint,
      state: normalizeSprintState(sprint.state),
      startDate: normalizeDateInput(sprint.startDate),
      endDate: normalizeDateInput(sprint.endDate)
    }))
    taskStatuses.value = (statusesRes.data?.data || []).map((status, index) => ({
      ...status,
      colorCode: status.colorCode || '#3b82f6',
      position: status.position ?? index
    }))
    newState.value.position = taskStatuses.value.length
    integrations.value = (integrationsRes.data?.data || [])
      .map(normalizeIntegration)
      .filter(integration => integration.provider === 'github')

    customFields.value = (customFieldsRes.data?.data || []).map(field => ({
      ...field,
      options: parseOptions(field.optionsJson)
    }))

    generalForm.value = {
      name: project.value.name || '',
      key: project.value.key || project.value.identifier || '',
      description: project.value.description || '',
      startDate: normalizeDateInput(project.value.startDate),
      endDate: normalizeDateInput(project.value.endDate)
    }

    const rules = executionRulesRes.data?.data || {}
    executionRulesForm.value = {
      enableRoleBasedTaskVisibility: Boolean(rules.enableRoleBasedTaskVisibility),
      managerAlwaysSeeAllTasks: rules.managerAlwaysSeeAllTasks !== false,
      defaultTaskVisibilityMode: rules.defaultTaskVisibilityMode || 'project',
      defaultBaseHours: Number(rules.defaultBaseHours ?? 4),
      hoursPerStoryPoint: Number(rules.hoursPerStoryPoint ?? 2),
      estimateBaselineMode: rules.estimateBaselineMode || 'role_then_project',
      roleHourMultipliers: {
        dev: Number(rules.roleHourMultipliers?.dev ?? 1),
        developer: Number(rules.roleHourMultipliers?.developer ?? 1),
        qa: Number(rules.roleHourMultipliers?.qa ?? 0.75),
        designer: Number(rules.roleHourMultipliers?.designer ?? 0.9),
        devops: Number(rules.roleHourMultipliers?.devops ?? 1.15),
        pm: Number(rules.roleHourMultipliers?.pm ?? 0.9),
        po: Number(rules.roleHourMultipliers?.po ?? 0.8),
        sm: Number(rules.roleHourMultipliers?.sm ?? 0.8)
      }
    }

    const rewardRules = rewardRulesRes.data?.data || {}
    rewardRulesForm.value = {
      basePointMultiplier: Number(rewardRules.basePointMultiplier ?? 1),
      earlyBonusPercent: Number(rewardRules.earlyBonusPercent ?? 10),
      accuracyBonusPercent: Number(rewardRules.accuracyBonusPercent ?? 5),
      latePenaltyPercent: Number(rewardRules.latePenaltyPercent ?? 10),
      collaborationBonusPoints: Number(rewardRules.collaborationBonusPoints ?? 2),
      manualAdjustmentLimit: Number(rewardRules.manualAdjustmentLimit ?? 200)
    }

    const capacityRules = capacityRulesRes.data?.data || {}
    capacityRulesForm.value = {
      defaultWeeklyHours: Number(capacityRules.defaultWeeklyHours ?? 40),
      nearLimitPercent: Number(capacityRules.nearLimitPercent ?? 80),
      overLimitPercent: Number(capacityRules.overLimitPercent ?? 100),
      maxActiveTasksPerMember: Number(capacityRules.maxActiveTasksPerMember ?? 8),
      roleWeeklyHours: { ...(capacityRules.roleWeeklyHours || {}) },
      roleActiveTaskLimits: { ...(capacityRules.roleActiveTaskLimits || {}) }
    }

    const baselineSettings = baselineSettingsRes.data?.data || {}
    baselineSettingsForm.value = {
      usePlanningBaseline: baselineSettings.usePlanningBaseline !== false,
      defaultBaseHours: Number(baselineSettings.defaultBaseHours ?? 4),
      hoursPerStoryPoint: Number(baselineSettings.hoursPerStoryPoint ?? 2),
      minimumSuggestedHours: Number(baselineSettings.minimumSuggestedHours ?? 0.5),
      maximumSuggestedHours: Number(baselineSettings.maximumSuggestedHours ?? 80),
      roleHourMultipliers: { ...(baselineSettings.roleHourMultipliers || {}) },
      roleBaseHours: { ...(baselineSettings.roleBaseHours || {}) }
    }

    milestones.value = (milestonesRes.data?.data || []).map(milestone => ({
      ...milestone,
      startDate: normalizeDateInput(milestone.startDate),
      targetDate: normalizeDateInput(milestone.targetDate),
      linkedSprintIds: [...(milestone.linkedSprintIds || [])]
    }))

    pointManagement.value = pointsRes.data?.data || pointManagement.value
    operationalDashboard.value = dashboardRes.data?.data || operationalDashboard.value

    if (!projectRoleOptions.value.includes(inviteForm.value.role)) {
      inviteForm.value.role = preferredInviteRole.value
    }
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not load project settings')
    router.replace(`/space/${projectId}`)
  } finally {
    loading.value = false
  }
}

// ────────────────────────────────────────────
// SME Permission Matrix Operations
// ────────────────────────────────────────────
const loadPermissionMatrix = async () => {
  try {
    const res = await axiosClient.get(`/settings/ProjectPermissions:${projectId}`)
    if (res.data?.data?.rolePermissions) {
      localMatrix.value = JSON.parse(res.data.data.rolePermissions)
    } else {
      localMatrix.value = getDefaultPermissionMatrix()
    }
  } catch (e) {
    localMatrix.value = getDefaultPermissionMatrix()
  }
}

const savePermissionMatrix = async () => {
  savingMatrix.value = true
  try {
    await axiosClient.put(`/settings/ProjectPermissions:${projectId}`, {
      settings: {
        rolePermissions: JSON.stringify(localMatrix.value)
      }
    })
    ElMessage.success('Cập nhật ma trận phân quyền thành công.')
    notifyProjectSettingsRealtime()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể cập nhật phân quyền.')
  } finally {
    savingMatrix.value = false
  }
}

const restoreDefaultMatrix = () => {
  localMatrix.value = getDefaultPermissionMatrix()
  ElMessage.info('Đã khôi phục ma trận quyền mặc định trên giao diện. Nhấn "Lưu thay đổi" để lưu.')
}


const loadManagementData = async () => {
  try {
    const [rewardRulesRes, capacityRulesRes, baselineSettingsRes, milestonesRes, pointsRes, dashboardRes] = await Promise.all([
      axiosClient.get(`/projects/${projectId}/management/reward-rules`),
      axiosClient.get(`/projects/${projectId}/management/capacity-rules`),
      axiosClient.get(`/projects/${projectId}/management/baseline-settings`),
      axiosClient.get(`/projects/${projectId}/management/milestones`),
      axiosClient.get(`/projects/${projectId}/management/points`),
      axiosClient.get(`/projects/${projectId}/management/operational-dashboard`)
    ])

    rewardRulesForm.value = { ...(rewardRulesRes.data?.data || rewardRulesForm.value) }
    capacityRulesForm.value = {
      ...(capacityRulesForm.value || {}),
      ...(capacityRulesRes.data?.data || {}),
      roleWeeklyHours: { ...(capacityRulesRes.data?.data?.roleWeeklyHours || capacityRulesForm.value.roleWeeklyHours || {}) },
      roleActiveTaskLimits: { ...(capacityRulesRes.data?.data?.roleActiveTaskLimits || capacityRulesForm.value.roleActiveTaskLimits || {}) }
    }
    baselineSettingsForm.value = {
      ...(baselineSettingsForm.value || {}),
      ...(baselineSettingsRes.data?.data || {}),
      roleHourMultipliers: { ...(baselineSettingsRes.data?.data?.roleHourMultipliers || baselineSettingsForm.value.roleHourMultipliers || {}) },
      roleBaseHours: { ...(baselineSettingsRes.data?.data?.roleBaseHours || baselineSettingsForm.value.roleBaseHours || {}) }
    }
    milestones.value = (milestonesRes.data?.data || []).map(milestone => ({
      ...milestone,
      startDate: normalizeDateInput(milestone.startDate),
      targetDate: normalizeDateInput(milestone.targetDate),
      linkedSprintIds: [...(milestone.linkedSprintIds || [])]
    }))
    pointManagement.value = pointsRes.data?.data || pointManagement.value
    operationalDashboard.value = dashboardRes.data?.data || operationalDashboard.value
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not load project management data')
  }
}

const loadIntegrations = async () => {
  try {
    const response = await axiosClient.get(`/projects/${projectId}/integrations`)
    integrations.value = (response.data?.data || [])
      .map(normalizeIntegration)
      .filter(integration => integration.provider === 'github')
      .map(integration => ({ ...integration, secret: '' }))
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not load project integrations')
  }
}

const saveProjectCover = async () => {
  if (!coverFile.value) {
    ElMessage.warning('Hãy chọn ảnh mới trước khi lưu cover')
    return
  }

  savingCover.value = true
  try {
    const payload = new FormData()
    payload.append('file', coverFile.value)
    payload.append('coverAltText', generatedCoverAltText.value)
    if (project.value.icon) payload.append('icon', project.value.icon)
    const response = await axiosClient.post(`/projects/${projectId}/cover`, payload)

    const updated = response.data?.data || {}
    project.value = {
      ...project.value,
      cover: updated.coverUrl || rawProjectCover.value,
      coverAltText: updated.coverAltText || generatedCoverAltText.value
    }
    projectStore.applyProjectUpdate(project.value)
    clearSelectedCover()
    ElMessage.success('Project cover updated')
    notifyProjectSettingsRealtime()
    await loadProjectSettings()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not update project cover')
  } finally {
    savingCover.value = false
  }
}

const removeProjectCover = async () => {
  try {
    await ElMessageBox.confirm(
      'Remove the current project cover?',
      'Remove cover',
      { type: 'warning', confirmButtonText: 'Remove' }
    )

    savingCover.value = true
    await axiosClient.delete(`/projects/${projectId}/cover`)
    clearSelectedCover()
    project.value = { ...project.value, cover: null, coverAltText: null }
    projectStore.applyProjectUpdate(project.value)
    ElMessage.success('Project cover removed')
    notifyProjectSettingsRealtime()
    await loadProjectSettings()
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || 'Could not remove project cover')
    }
  } finally {
    savingCover.value = false
  }
}

const saveGeneral = async () => {
  if (!generalForm.value.name.trim() || !generalForm.value.key.trim() || !generalForm.value.startDate) {
    ElMessage.warning('Project name, key, and start date are required')
    return
  }

  savingGeneral.value = true
  try {
    const response = await axiosClient.put(`/projects/${projectId}`, {
      name: generalForm.value.name.trim(),
      identifier: generalForm.value.key.trim(),
      description: generalForm.value.description?.trim() || '',
      startDate: generalForm.value.startDate || null,
      endDate: generalForm.value.endDate || null,
      departmentId: project.value.departmentId || null
    })
    const updatedProject = response.data?.data || {}
    project.value = {
      ...project.value,
      ...updatedProject,
      name: updatedProject.name || generalForm.value.name.trim(),
      key: updatedProject.key || generalForm.value.key.trim(),
      description: updatedProject.description ?? generalForm.value.description?.trim() ?? '',
      startDate: updatedProject.startDate || generalForm.value.startDate || null,
      endDate: updatedProject.endDate || generalForm.value.endDate || null
    }
    projectStore.applyProjectUpdate(project.value)
    ElMessage.success('Project general settings updated')
    notifyProjectSettingsRealtime()
    await loadProjectSettings()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not save project settings')
  } finally {
    savingGeneral.value = false
  }
}

const saveExecutionRules = async () => {
  savingExecutionRules.value = true
  try {
    await axiosClient.put(`/projects/${projectId}/execution-rules`, {
      rules: {
        enableRoleBasedTaskVisibility: Boolean(executionRulesForm.value.enableRoleBasedTaskVisibility),
        managerAlwaysSeeAllTasks: Boolean(executionRulesForm.value.managerAlwaysSeeAllTasks),
        defaultTaskVisibilityMode: executionRulesForm.value.defaultTaskVisibilityMode || 'project',
        defaultBaseHours: Number(executionRulesForm.value.defaultBaseHours || 4),
        hoursPerStoryPoint: Number(executionRulesForm.value.hoursPerStoryPoint || 2),
        estimateBaselineMode: executionRulesForm.value.estimateBaselineMode || 'role_then_project',
        roleHourMultipliers: {
          dev: Number(executionRulesForm.value.roleHourMultipliers.dev || 1),
          developer: Number(executionRulesForm.value.roleHourMultipliers.developer || 1),
          qa: Number(executionRulesForm.value.roleHourMultipliers.qa || 0.75),
          designer: Number(executionRulesForm.value.roleHourMultipliers.designer || 0.9),
          devops: Number(executionRulesForm.value.roleHourMultipliers.devops || 1.15),
          pm: Number(executionRulesForm.value.roleHourMultipliers.pm || 0.9),
          po: Number(executionRulesForm.value.roleHourMultipliers.po || 0.8),
          sm: Number(executionRulesForm.value.roleHourMultipliers.sm || 0.8)
        }
      }
    })
    ElMessage.success('Project execution rules updated')
    notifyProjectSettingsRealtime()
    await loadProjectSettings()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not save execution rules')
  } finally {
    savingExecutionRules.value = false
  }
}

const saveRewardRules = async () => {
  savingRewardRules.value = true
  try {
    await axiosClient.put(`/projects/${projectId}/management/reward-rules`, {
      rules: {
        basePointMultiplier: Number(rewardRulesForm.value.basePointMultiplier || 1),
        earlyBonusPercent: Number(rewardRulesForm.value.earlyBonusPercent || 0),
        accuracyBonusPercent: Number(rewardRulesForm.value.accuracyBonusPercent || 0),
        latePenaltyPercent: Number(rewardRulesForm.value.latePenaltyPercent || 0),
        collaborationBonusPoints: Number(rewardRulesForm.value.collaborationBonusPoints || 0),
        manualAdjustmentLimit: Number(rewardRulesForm.value.manualAdjustmentLimit || 200)
      }
    })
    ElMessage.success('Reward rules updated')
    notifyProjectSettingsRealtime()
    await loadManagementData()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not save reward rules')
  } finally {
    savingRewardRules.value = false
  }
}

const saveCapacityRules = async () => {
  savingCapacityRules.value = true
  try {
    await axiosClient.put(`/projects/${projectId}/management/capacity-rules`, {
      rules: {
        defaultWeeklyHours: Number(capacityRulesForm.value.defaultWeeklyHours || 40),
        nearLimitPercent: Number(capacityRulesForm.value.nearLimitPercent || 80),
        overLimitPercent: Number(capacityRulesForm.value.overLimitPercent || 100),
        maxActiveTasksPerMember: Number(capacityRulesForm.value.maxActiveTasksPerMember || 8),
        roleWeeklyHours: { ...(capacityRulesForm.value.roleWeeklyHours || {}) },
        roleActiveTaskLimits: { ...(capacityRulesForm.value.roleActiveTaskLimits || {}) }
      }
    })
    ElMessage.success('Capacity rules updated')
    notifyProjectSettingsRealtime()
    await loadManagementData()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not save capacity rules')
  } finally {
    savingCapacityRules.value = false
  }
}

const saveBaselineSettings = async () => {
  savingBaselineSettings.value = true
  try {
    await axiosClient.put(`/projects/${projectId}/management/baseline-settings`, {
      rules: {
        usePlanningBaseline: Boolean(baselineSettingsForm.value.usePlanningBaseline),
        defaultBaseHours: Number(baselineSettingsForm.value.defaultBaseHours || 4),
        hoursPerStoryPoint: Number(baselineSettingsForm.value.hoursPerStoryPoint || 2),
        minimumSuggestedHours: Number(baselineSettingsForm.value.minimumSuggestedHours || 0.5),
        maximumSuggestedHours: Number(baselineSettingsForm.value.maximumSuggestedHours || 80),
        roleHourMultipliers: { ...(baselineSettingsForm.value.roleHourMultipliers || {}) },
        roleBaseHours: { ...(baselineSettingsForm.value.roleBaseHours || {}) }
      }
    })
    ElMessage.success('Baseline settings updated')
    notifyProjectSettingsRealtime()
    await loadProjectSettings()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not save baseline settings')
  } finally {
    savingBaselineSettings.value = false
  }
}

const createPointAdjustment = async () => {
  if (!pointAdjustmentForm.value.userId || !pointAdjustmentForm.value.amount || !pointAdjustmentForm.value.reason.trim()) {
    ElMessage.warning('Member, amount, and reason are required')
    return
  }

  savingPointAdjustment.value = true
  try {
    await axiosClient.post(`/projects/${projectId}/management/points/adjustments`, {
      userId: pointAdjustmentForm.value.userId,
      amount: Number(pointAdjustmentForm.value.amount),
      reason: pointAdjustmentForm.value.reason.trim(),
      adjustmentType: pointAdjustmentForm.value.adjustmentType
    })
    pointAdjustmentForm.value = {
      userId: null,
      amount: 0,
      reason: '',
      adjustmentType: 'Manual'
    }
    ElMessage.success('Point adjustment created')
    notifyProjectSettingsRealtime()
    await loadManagementData()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not create point adjustment')
  } finally {
    savingPointAdjustment.value = false
  }
}

const createMilestone = async () => {
  if (!newMilestone.value.name.trim()) {
    ElMessage.warning('Milestone name is required')
    return
  }

  savingMilestone.value = true
  try {
    await axiosClient.post(`/projects/${projectId}/management/milestones`, {
      milestone: {
        ...newMilestone.value,
        name: newMilestone.value.name.trim(),
        description: newMilestone.value.description?.trim() || null,
        releaseVersion: newMilestone.value.releaseVersion?.trim() || null
      }
    })
    newMilestone.value = {
      name: '',
      description: '',
      releaseVersion: '',
      startDate: '',
      targetDate: '',
      status: 'Planned',
      linkedSprintIds: []
    }
    ElMessage.success('Milestone created')
    notifyProjectSettingsRealtime()
    await loadManagementData()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not create milestone')
  } finally {
    savingMilestone.value = false
  }
}

const saveMilestone = async (milestone) => {
  try {
    await axiosClient.put(`/projects/${projectId}/management/milestones/${milestone.id}`, {
      milestone: {
        ...milestone,
        name: milestone.name?.trim(),
        description: milestone.description?.trim() || null,
        releaseVersion: milestone.releaseVersion?.trim() || null
      }
    })
    ElMessage.success('Milestone updated')
    notifyProjectSettingsRealtime()
    await loadManagementData()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not update milestone')
  }
}

const deleteMilestone = async (milestone) => {
  try {
    await ElMessageBox.confirm(`Delete milestone "${milestone.name}"?`, 'Delete milestone', { type: 'warning' })
    await axiosClient.delete(`/projects/${projectId}/management/milestones/${milestone.id}`)
    ElMessage.success('Milestone deleted')
    notifyProjectSettingsRealtime()
    await loadManagementData()
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || 'Could not delete milestone')
    }
  }
}

const inviteMember = async () => {
  if (!inviteForm.value.email.trim()) {
    ElMessage.warning('Member email is required')
    return
  }

  savingInvite.value = true
  try {
    await axiosClient.post(`/projects/${projectId}/members`, {
      email: inviteForm.value.email.trim(),
      role: inviteForm.value.role
    })
    inviteForm.value.email = ''
    inviteForm.value.role = preferredInviteRole.value
    ElMessage.success('Member invited successfully')
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not invite member')
  } finally {
    savingInvite.value = false
  }
}

const updateMemberRole = async (member, role) => {
  try {
    await axiosClient.put(`/projects/${projectId}/members/${member.userId}/role`, { role })
    member.projectRole = role
    ElMessage.success('Member role updated')
    notifyProjectSettingsRealtime()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not update member role')
    await loadProjectSettings()
  }
}

const removeMember = async (member) => {
  if (member.userId === currentUser.value?.id) {
    ElMessage.error('Không thể tự xoá bản thân ra khỏi dự án')
    return
  }
  try {
    await ElMessageBox.confirm(`Remove ${member.fullName || member.email} from this project?`, 'Remove member', { type: 'warning' })
    await axiosClient.delete(`/projects/${projectId}/members/${member.userId}`)
    ElMessage.success('Member removed')
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || 'Could not remove member')
    }
  }
}

const createState = async () => {
  if (!newState.value.name.trim()) {
    ElMessage.warning('State name is required')
    return
  }

  savingState.value = true
  try {
    await axiosClient.post(`/projects/${projectId}/task-statuses`, {
      name: newState.value.name.trim(),
      colorCode: newState.value.colorCode,
      position: newState.value.position
    })
    newState.value = {
      name: '',
      colorCode: '#3b82f6',
      position: taskStatuses.value.length + 1
    }
    ElMessage.success('Đã tạo trạng thái thành công.')
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể tạo trạng thái.')
  } finally {
    savingState.value = false
  }
}

const saveState = async (status) => {
  try {
    await axiosClient.put(`/projects/${projectId}/task-statuses/${status.id}`, {
      name: status.name,
      colorCode: status.colorCode,
      position: status.position
    })
    ElMessage.success('Đã cập nhật trạng thái thành công.')
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể cập nhật trạng thái.')
    await loadProjectSettings()
  }
}

const deleteState = async (status) => {
  try {
    await ElMessageBox.confirm(`Bạn có chắc chắn muốn xóa trạng thái "${status.name}"?`, 'Xóa trạng thái', {
      type: 'warning',
      confirmButtonText: 'Xóa',
      cancelButtonText: 'Hủy'
    })
    await axiosClient.delete(`/projects/${projectId}/task-statuses/${status.id}`)
    ElMessage.success('Đã xóa trạng thái thành công.')
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    if (error !== 'cancel') {
      const detail = error.response?.data?.message || ''
      const isStatusInUse = detail.includes('dang duoc task su dung') || detail.includes('in use') || detail.includes('đang được task sử dụng')
      const errorMsg = isStatusInUse
        ? 'Không thể xóa trạng thái đang có công việc. Vui lòng chuyển các công việc sang trạng thái khác trước.'
        : (detail || 'Không thể xóa trạng thái.')
      ElMessage.error(errorMsg)
    }
  }
}

const createLabel = async () => {
  if (!newLabel.value.name.trim()) {
    ElMessage.warning('Label name is required')
    return
  }

  savingLabel.value = true
  try {
    await axiosClient.post(`/projects/${projectId}/labels`, {
      name: newLabel.value.name.trim(),
      colorCode: newLabel.value.colorCode,
      description: newLabel.value.description?.trim() || ''
    })
    newLabel.value = { name: '', colorCode: '#3b82f6', description: '' }
    ElMessage.success('Label created')
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not create label')
  } finally {
    savingLabel.value = false
  }
}

const deleteLabel = async (label) => {
  try {
    await ElMessageBox.confirm(`Delete label "${label.name}"?`, 'Delete label', { type: 'warning' })
    await axiosClient.delete(`/projects/${projectId}/labels/${label.id}`)
    ElMessage.success('Label deleted')
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || 'Could not delete label')
    }
  }
}

const saveLabel = async (label) => {
  try {
    await axiosClient.put(`/projects/${projectId}/labels/${label.id}`, {
      name: label.name?.trim(),
      colorCode: label.colorCode,
      description: label.description?.trim() || ''
    })
    ElMessage.success('Label updated')
    notifyProjectSettingsRealtime()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not update label')
    await loadProjectSettings()
  }
}

const createModule = async () => {
  if (!newModule.value.name.trim()) {
    ElMessage.warning('Module name is required')
    return
  }
  if (isPastDate(newModule.value.startDate) || isPastDate(newModule.value.targetDate)) {
    ElMessage.warning('Past dates are not allowed for modules')
    return
  }
  if (newModule.value.startDate && newModule.value.targetDate && newModule.value.targetDate < newModule.value.startDate) {
    ElMessage.warning('Target date must be on or after the start date')
    return
  }

  savingModule.value = true
  try {
    await axiosClient.post(`/projects/${projectId}/modules`, {
      name: newModule.value.name.trim(),
      description: newModule.value.description?.trim() || '',
      status: newModule.value.status,
      leadId: newModule.value.leadId,
      startDate: toLocalDateTime(newModule.value.startDate),
      targetDate: toLocalDateTime(newModule.value.targetDate)
    })
    newModule.value = {
      name: '',
      description: '',
      status: 'Backlog',
      leadId: null,
      startDate: '',
      targetDate: ''
    }
    ElMessage.success('Module created')
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not create module')
  } finally {
    savingModule.value = false
  }
}

const deleteModule = async (module) => {
  try {
    await ElMessageBox.confirm(`Disable module "${module.name}"?`, 'Disable module', { type: 'warning' })
    await axiosClient.delete(`/projects/${projectId}/modules/${module.id}`)
    ElMessage.success('Module disabled')
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || 'Could not disable module')
    }
  }
}

const saveModule = async (module) => {
  if (isPastDate(module.startDate) || isPastDate(module.targetDate)) {
    ElMessage.warning('Past dates are not allowed for modules')
    return
  }
  if (module.startDate && module.targetDate && module.targetDate < module.startDate) {
    ElMessage.warning('Target date must be on or after the start date')
    return
  }
  try {
    await axiosClient.put(`/projects/${projectId}/modules/${module.id}`, {
      name: module.name?.trim(),
      description: module.description?.trim() || '',
      status: module.status,
      leadId: module.leadId,
      startDate: toLocalDateTime(module.startDate),
      targetDate: toLocalDateTime(module.targetDate)
    })
    ElMessage.success('Module updated')
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not update module')
  }
}

const restoreModule = async (module) => {
  try {
    await axiosClient.put(`/projects/${projectId}/modules/${module.id}`, {
      status: 'Backlog'
    })
    ElMessage.success('Module restored')
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not restore module')
  }
}

const createCycle = async () => {
  if (!canManageSprint.value) return
  if (!newCycle.value.name.trim() || !newCycle.value.startDate || !newCycle.value.endDate) {
    ElMessage.warning('Cycle name, start date, and end date are required')
    return
  }
  if (isPastDate(newCycle.value.startDate) || isPastDate(newCycle.value.endDate)) {
    ElMessage.warning('Past dates are not allowed for cycles')
    return
  }
  if (newCycle.value.endDate < newCycle.value.startDate) {
    ElMessage.warning('End date must be on or after the start date')
    return
  }

  savingCycle.value = true
  try {
    await axiosClient.post(`/projects/${projectId}/sprints`, {
      name: newCycle.value.name.trim(),
      description: newCycle.value.description?.trim() || '',
      startDate: toLocalDateTime(newCycle.value.startDate),
      endDate: toLocalDateTime(newCycle.value.endDate)
    })
    newCycle.value = { name: '', description: '', startDate: '', endDate: '' }
    ElMessage.success('Cycle created')
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not create cycle')
  } finally {
    savingCycle.value = false
  }
}

const saveCycle = async (sprint) => {
  if (!canManageSprint.value) return
  if (isPastDate(sprint.startDate) || isPastDate(sprint.endDate)) {
    ElMessage.warning('Past dates are not allowed for cycles')
    return
  }
  if (sprint.startDate && sprint.endDate && sprint.endDate < sprint.startDate) {
    ElMessage.warning('End date must be on or after the start date')
    return
  }
  try {
    await axiosClient.put(`/projects/${projectId}/sprints/${sprint.id}`, {
      name: sprint.name?.trim(),
      startDate: toLocalDateTime(sprint.startDate),
      endDate: toLocalDateTime(sprint.endDate)
    })
    ElMessage.success('Cycle updated')
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not update cycle')
  }
}

const startCycle = async (sprint) => {
  if (
    !canManageSprint.value ||
    !getSprintStateMeta(sprint.state).canStart ||
    isCycleTransitioning('start', sprint.id)
  ) return

  try {
    const result = await sprintStore.startSprint(projectId, sprint.id)
    if (result?.data) {
      ElMessage.success('Cycle started')
      await loadProjectSettings()
      notifyProjectSettingsRealtime()
    }
  } catch (error) {
    if ([404, 409].includes(getSprintApiError(error).status)) {
      await loadProjectSettings()
    }
    ElMessage.error(getSprintErrorMessage(error))
  }
}

const closeCycleToBacklog = async (sprint) => {
  if (
    !canManageSprint.value ||
    !getSprintStateMeta(sprint.state).canClose ||
    isCycleTransitioning('close', sprint.id)
  ) return

  try {
    await ElMessageBox.confirm(
      `Close "${sprint.name}" and move unfinished work items to backlog?`,
      'Close cycle',
      {
        type: 'warning',
        confirmButtonText: 'Close Cycle',
        cancelButtonText: 'Cancel',
        closeOnClickModal: false
      }
    )
    const result = await sprintStore.closeSprint(projectId, sprint.id, null)
    if (result?.data) {
      ElMessage.success('Cycle closed and unfinished work moved to backlog')
      await loadProjectSettings()
      notifyProjectSettingsRealtime()
    }
  } catch (error) {
    if (error !== 'cancel') {
      if ([404, 409].includes(getSprintApiError(error).status)) {
        await loadProjectSettings()
      }
      ElMessage.error(getSprintErrorMessage(error))
    }
  }
}

const toggleFavoriteCycle = async (sprint) => {
  try {
    await axiosClient.patch(`/projects/${projectId}/sprints/${sprint.id}/favorite`)
    sprint.isFavorite = !sprint.isFavorite
    ElMessage.success(sprint.isFavorite ? 'Cycle marked as favorite' : 'Cycle removed from favorites')
    notifyProjectSettingsRealtime('project-settings-favorite-updated')
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not update cycle favorite state')
    await loadProjectSettings()
  }
}

const openCycleCarryOver = (sprint) => {
  router.push({
    name: 'CyclesView',
    params: { id: projectId },
    query: { carryOverFromSprintId: sprint.id }
  })
}

const saveIntegrations = async () => {
  savingIntegrations.value = true
  try {
    await axiosClient.put(`/projects/${projectId}/integrations`, {
      items: integrations.value
        .filter(integration => integration.provider === 'github')
        .map(integration => ({
        provider: integration.provider,
        displayName: integration.displayName,
        enabled: integration.enabled,
        endpoint: integration.endpoint?.trim() || null,
        projectKey: integration.projectKey?.trim() || null,
        secret: integration.secret?.trim() || null,
        notes: integration.notes?.trim() || null,
        updatedAt: integration.updatedAt || null
      }))
    })
    ElMessage.success('Project integrations updated')
    await loadIntegrations()
    notifyProjectSettingsRealtime('project-settings-integrations-updated')
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not update project integrations')
  } finally {
    integrations.value.forEach(integration => {
      integration.secret = ''
    })
    clearLegacyGitHubCredentialStorage()
    savingIntegrations.value = false
  }
}

const analyzeIntegration = async (integration) => {
  const repoUrl = buildIntegrationRepoUrl(integration)
  if (!repoUrl) {
    ElMessage.warning('Please enter owner/repository or a valid GitHub endpoint first')
    return
  }

  analyzingIntegration.value = integration.provider
  try {
    const response = await runWithEphemeralGitHubToken(integration, gitHubToken =>
      axiosClient.post('/ai/repo-analysis', {
        repoUrl,
        gitHubToken,
        focus: `Project ${project.value.name || projectId} planning, backlog, risks, and task breakdown`
      }))

    integrationAnalysis.value = response.data?.data || null
    stashAiPlannerPayload({
      repoUrl,
      prompt: integrationAnalysis.value?.suggestedPrompt,
      analysis: integrationAnalysis.value
    })
    ElMessage.success('AI repo analysis is ready')
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not analyze the connected repository')
  } finally {
    integration.secret = ''
    clearLegacyGitHubCredentialStorage()
    analyzingIntegration.value = ''
  }
}

const openIntegrationInAi = (integration) => {
  const repoUrl = buildIntegrationRepoUrl(integration)
  if (!repoUrl) {
    ElMessage.warning('Please enter owner/repository or a valid GitHub endpoint first')
    return
  }

  stashAiPlannerPayload({
    repoUrl,
    prompt: integrationAnalysis.value?.suggestedPrompt || `Phan tich repo ${repoUrl} va de xuat backlog, breakdown task, risk, va test plan.`,
    analysis: integrationAnalysis.value
  })

  router.push('/ai')
}

const useIntegrationPrompt = () => {
  if (!integrationAnalysis.value?.suggestedPrompt) {
    ElMessage.warning('Analyze the repository first')
    return
  }

  stashAiPlannerPayload({
    repoUrl: `https://github.com/${integrationAnalysis.value.repository}`,
    prompt: integrationAnalysis.value.suggestedPrompt,
    analysis: integrationAnalysis.value
  })

  router.push('/ai')
}

const resetIntegration = async (provider) => {
  try {
    const current = integrations.value.find(item => item.provider === provider)
    if (!current) return

    const response = await axiosClient.get(`/projects/${projectId}/integrations`)
    const freshItems = (response.data?.data || []).map(normalizeIntegration)
    const next = freshItems.find(item => item.provider === provider)
    if (!next) return

    Object.assign(current, next)
    ElMessage.success(`${next.displayName} reset`)
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not reset integration')
  }
}

const toggleArchive = async () => {
  const action = project.value.isArchived ? 'restore' : 'archive'
  try {
    await ElMessageBox.confirm(
      action === 'archive'
        ? 'Archive this project and remove it from active project lists?'
        : 'Restore this project back to active lists?',
      action === 'archive' ? 'Archive project' : 'Restore project',
      { type: 'warning' }
    )

    await axiosClient.put(`/projects/${projectId}/${action}`)
    ElMessage.success(action === 'archive' ? 'Project archived' : 'Project restored')
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || 'Could not update project status')
    }
  }
}

const deleteProject = async () => {
  try {
    await ElMessageBox.confirm(
      'Delete this project? This action cannot be undone from the project settings screen.',
      'Delete project',
      { type: 'warning', confirmButtonText: 'Delete' }
    )

    await axiosClient.delete(`/projects/${projectId}`)
    ElMessage.success('Project deleted')
    notifyProjectSettingsRealtime('project-settings-deleted')
    router.replace('/spaces')
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || 'Could not delete project')
    }
  }
}

const openCreateCustomField = () => {
  isEditingCustomField.value = false
  customFieldForm.value = {
    id: null,
    name: '',
    type: 'Text',
    isRequired: false,
    optionsText: '',
    isVisible: true,
    sortOrder: customFields.value.length + 1
  }
  showCustomFieldModal.value = true
}

const openEditCustomField = (field) => {
  isEditingCustomField.value = true
  customFieldForm.value = {
    id: field.id,
    name: field.name,
    type: field.type,
    isRequired: field.isRequired,
    optionsText: field.options ? field.options.join('\n') : '',
    isVisible: field.isVisible,
    sortOrder: field.sortOrder
  }
  showCustomFieldModal.value = true
}

const saveCustomField = async () => {
  if (!customFieldForm.value.name || !customFieldForm.value.name.trim()) {
    ElMessage.warning('Tên trường không được để trống.')
    return
  }

  const payload = {
    name: customFieldForm.value.name.trim(),
    type: customFieldForm.value.type,
    isRequired: customFieldForm.value.isRequired,
    isVisible: customFieldForm.value.isVisible,
    sortOrder: customFieldForm.value.sortOrder,
    options: customFieldForm.value.type === 'Select'
      ? customFieldForm.value.optionsText.split('\n').map(x => x.trim()).filter(Boolean)
      : null
  }

  savingCustomField.value = true
  try {
    if (isEditingCustomField.value) {
      await axiosClient.put(`/projects/${projectId}/custom-fields/${customFieldForm.value.id}`, payload)
      ElMessage.success('Đã cập nhật trường tùy chỉnh.')
    } else {
      await axiosClient.post(`/projects/${projectId}/custom-fields`, payload)
      ElMessage.success('Đã tạo trường tùy chỉnh.')
    }
    showCustomFieldModal.value = false
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Có lỗi xảy ra khi lưu trường tùy chỉnh.')
  } finally {
    savingCustomField.value = false
  }
}

const deleteCustomField = async (field) => {
  try {
    await ElMessageBox.confirm(`Bạn có chắc chắn muốn xóa trường tùy chỉnh "${field.name}"? Dữ liệu lịch sử đã lưu trên các công việc sẽ được bảo lưu.`, 'Xóa trường tùy chỉnh', {
      type: 'warning',
      confirmButtonText: 'Xóa',
      cancelButtonText: 'Hủy'
    })
    await axiosClient.delete(`/projects/${projectId}/custom-fields/${field.id}`)
    ElMessage.success('Đã xóa trường tùy chỉnh thành công.')
    await loadProjectSettings()
    notifyProjectSettingsRealtime()
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || 'Không thể xóa trường tùy chỉnh.')
    }
  }
}

const goBack = () => {
  router.push(`/space/${projectId}`)
}

const goToModulesWorkspace = () => {
  router.push(`/space/${projectId}/modules`)
}

const goToCyclesWorkspace = () => {
  router.push(`/space/${projectId}/cycles`)
}

let unsubscribeAdminRealtime = null
let projectRealtimeHandler = null

onMounted(async () => {
  clearLegacyGitHubCredentialStorage()
  await loadProjectSettings()
  await loadPermissionMatrix()
  await signalRService.startConnection(`${projectId}`)
  if (projectRealtimeHandler) {
    signalRService.off('ProjectRealtimeEvent', projectRealtimeHandler)
  }
  projectRealtimeHandler = async (event) => {
    if (!event?.type) return
    if (event?.projectId && `${event.projectId}` !== `${projectId}`) return
    broadcastAdminRealtime(event.type, event.payload || { projectId })
  }
  signalRService.on('ProjectRealtimeEvent', projectRealtimeHandler)
  unsubscribeAdminRealtime = subscribeAdminRealtime(async ({ type, payload }) => {
    if (payload?.projectId && `${payload.projectId}` !== `${projectId}`) {
      return
    }

    if (type === 'project-settings-deleted') {
      router.replace('/spaces')
      return
    }

    if (
      [
        'project-settings-updated',
        'project-settings-favorite-updated',
        'project-settings-integrations-updated',
        'project-administration-updated'
      ].includes(type)
    ) {
      await loadProjectSettings()
    }
  })
})

onUnmounted(() => {
  clearSelectedCover()
  unsubscribeAdminRealtime?.()
  if (projectRealtimeHandler) {
    signalRService.off('ProjectRealtimeEvent', projectRealtimeHandler)
  }
})
</script>

<style scoped>
.project-settings-route {
  width: 100%;
  height: 100%;
  min-height: 0;
  background: var(--color-bg);
}

.project-settings-page {
  width: 100%;
  height: 100%;
  min-height: 0;
  padding: 0;
  box-sizing: border-box;
  color: var(--color-text-primary);
  background: var(--color-bg);
  overflow-y: auto;
  overflow-x: hidden;
}

.settings-header,
.card-head,
.header-actions,
.danger-actions,
.meta-strip,
.row-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.settings-header {
  margin: 0 auto 24px;
  max-width: 1360px;
}

.breadcrumb {
  color: var(--color-accent);
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  margin-bottom: 8px;
}

.settings-header h1,
.card-head h2 {
  margin: 0;
  color: var(--color-text-primary);
  letter-spacing: 0;
}

.settings-header h1 {
  font-size: 24px;
  font-weight: 600;
  line-height: 1.35;
}

.card-head h2 {
  font-size: 18px;
  font-weight: 600;
  line-height: 1.4;
}

.settings-header p,
.card-head p,
.row-main p {
  margin: 4px 0 0;
  color: var(--color-text-secondary);
  font-size: 14px;
  line-height: 1.5;
}

.switch-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.multiplier-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 12px;
}

.settings-shell {
  max-width: 1360px;
  margin: 0 auto;
  display: grid;
  grid-template-columns: 260px minmax(0, 1fr);
  gap: 20px;
  align-items: start;
  min-height: 0;
}

.settings-nav {
  position: sticky;
  top: 84px;
  display: grid;
  gap: 4px;
  max-height: calc(100vh - 160px);
  overflow-y: auto;
  padding: 12px;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface);
  box-shadow: 0 4px 14px rgb(15 23 42 / 0.04);
}

.settings-nav::-webkit-scrollbar,
.settings-content::-webkit-scrollbar,
.project-settings-page::-webkit-scrollbar {
  width: 8px;
}

.settings-nav::-webkit-scrollbar-thumb,
.settings-content::-webkit-scrollbar-thumb,
.project-settings-page::-webkit-scrollbar-thumb {
  border-radius: 999px;
  background: color-mix(in srgb, var(--color-accent) 24%, var(--color-border));
}

.nav-tab {
  min-height: 58px;
  padding: 10px 12px;
  text-align: left;
  color: var(--color-text-secondary);
  border: 1px solid transparent;
  border-left: 4px solid transparent;
  border-radius: 9px;
  background: transparent;
  cursor: pointer;
  transition: color 150ms ease, border-color 150ms ease, background-color 150ms ease;
}

.nav-tab span,
.nav-tab small {
  display: block;
}

.nav-tab small {
  margin-top: 4px;
  color: var(--color-text-muted);
  font-size: 12px;
  line-height: 1.35;
}

.nav-tab:hover {
  color: var(--color-text-primary);
  border-color: color-mix(in srgb, var(--color-accent) 22%, var(--color-border));
  border-left-color: color-mix(in srgb, var(--color-accent) 40%, var(--color-border));
  background: color-mix(in srgb, var(--color-accent) 4%, var(--color-surface));
}

.nav-tab:hover small {
  color: var(--color-text-secondary);
}

.nav-tab.active {
  color: var(--color-accent);
  border-color: color-mix(in srgb, var(--color-accent) 18%, var(--color-border));
  border-left-color: var(--color-accent);
  background: color-mix(in srgb, var(--color-accent) 8%, var(--color-surface));
}

.nav-tab.active span {
  font-weight: 600;
}

.nav-tab.active small {
  color: var(--color-text-secondary);
}

.settings-content {
  display: grid;
  min-height: 0;
  padding-bottom: 80px;
}

.settings-card {
  padding: 24px;
  border: 1px solid #e2e8f0;
  border-radius: 16px;
  background: var(--color-surface);
  box-shadow: 0 8px 24px rgb(15 23 42 / 0.055);
}

.form-grid,
.inline-form,
.stack-list,
.editable-grid {
  display: grid;
  gap: 14px;
}

.editable-grid {
  grid-template-columns: repeat(3, minmax(0, 1fr));
  width: 100%;
}

.editable-grid.compact-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.integration-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.form-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
  margin-top: 20px;
}

.cover-settings {
  display: grid;
  grid-template-columns: minmax(220px, 320px) minmax(0, 1fr);
  gap: 20px;
  align-items: start;
  margin-top: 24px;
  padding-top: 24px;
  border-top: 1px solid #27272a;
}

.cover-settings-preview {
  aspect-ratio: 16 / 9;
  display: grid;
  place-items: center;
  overflow: hidden;
  border: 1px solid #27272a;
  border-radius: 8px;
  background-position: center;
  background-size: cover;
  color: #fff;
  font-size: 36px;
  font-weight: 700;
}

.cover-settings-controls {
  display: grid;
  gap: 14px;
}

.cover-settings-controls h3,
.cover-settings-controls p {
  margin: 0;
}

.cover-settings-controls p,
.cover-file-name {
  color: #94a3b8;
}

.cover-settings-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.cover-error {
  color: #fca5a5;
}

.invite-grid,
.state-grid,
.label-grid,
.module-grid,
.cycle-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
  margin-top: 20px;
  margin-bottom: 20px;
}

.module-grid,
.cycle-grid {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

label {
  display: grid;
  gap: 8px;
}

label.wide {
  grid-column: 1 / -1;
}

label span {
  color: var(--color-text-secondary);
  font-size: 12px;
  font-weight: 600;
  line-height: 1.35;
  letter-spacing: 0.03em;
  text-transform: uppercase;
}

input,
textarea,
select {
  width: 100%;
  box-sizing: border-box;
  border: 1px solid #cbd5e1;
  background: var(--color-surface);
  color: var(--color-text-primary);
  border-radius: 10px;
  padding: 0 14px;
  font: inherit;
  font-size: 14px;
  outline: none;
  transition: border-color 150ms ease, box-shadow 150ms ease, background-color 150ms ease;
}

input,
select {
  height: 44px;
}

textarea {
  min-height: 104px;
  padding-top: 12px;
  padding-bottom: 12px;
  line-height: 1.55;
  resize: vertical;
}

input:focus,
textarea:focus,
select:focus {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-accent) 12%, transparent);
}

input[type='checkbox'] {
  width: 18px;
  height: 18px;
  flex: 0 0 18px;
  padding: 0;
  accent-color: var(--color-accent);
}

input:disabled {
  color: var(--color-text-secondary);
  background: var(--color-surface-hover);
  opacity: 1;
  cursor: not-allowed;
}

.meta-strip {
  justify-content: flex-start;
  flex-wrap: wrap;
  margin-top: 20px;
  color: var(--color-text-secondary);
  font-size: 13px;
}

.meta-strip.compact {
  margin-top: 10px;
  gap: 10px;
}

.stack-row {
  padding: 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-surface);
  transition: border-color 150ms ease, background-color 150ms ease, box-shadow 150ms ease;
}

.stack-row:hover {
  border-color: color-mix(in srgb, var(--color-accent) 24%, var(--color-border));
  background: color-mix(in srgb, var(--color-accent) 2%, var(--color-surface));
  box-shadow: 0 5px 14px rgb(15 23 42 / 0.04);
}

.row-main {
  min-width: 0;
  flex: 1;
}

.row-actions {
  flex-shrink: 0;
}

.state-row {
  display: grid;
  grid-template-columns: minmax(180px, 1fr) 72px 92px;
  gap: 10px;
  align-items: center;
}

.label-chip {
  display: inline-flex;
  align-items: center;
  gap: 10px;
}

.color-dot {
  width: 12px;
  height: 12px;
  border-radius: 999px;
}

.primary-btn,
.secondary-btn,
.danger-btn,
.danger-outline-btn {
  min-height: 42px;
  padding: 0 14px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 10px;
  cursor: pointer;
  font: inherit;
  font-size: 14px;
  font-weight: 600;
  transition: color 150ms ease, border-color 150ms ease, background-color 150ms ease, box-shadow 150ms ease, transform 150ms ease;
}

.primary-btn {
  background: var(--color-accent);
  color: #ffffff;
  border: 1px solid color-mix(in srgb, var(--color-accent) 84%, #0369a1);
  box-shadow: 0 4px 12px color-mix(in srgb, var(--color-accent) 18%, transparent);
}

.primary-btn:hover:not(:disabled) {
  transform: translateY(-1px);
  background: color-mix(in srgb, var(--color-accent) 86%, #0369a1);
  box-shadow: 0 6px 16px color-mix(in srgb, var(--color-accent) 22%, transparent);
}

.secondary-btn,
.danger-outline-btn {
  background: var(--color-surface);
  color: var(--color-text-secondary);
  border: 1px solid var(--color-border);
}

.secondary-btn:hover:not(:disabled) {
  color: var(--color-text-primary);
  border-color: color-mix(in srgb, var(--color-accent) 35%, var(--color-border));
  background: color-mix(in srgb, var(--color-accent) 4%, var(--color-surface));
}

.danger-btn {
  background: #b91c1c;
  color: #ffffff;
  border: 1px solid #ef4444;
}

.danger-outline-btn {
  color: #dc2626;
  border-color: rgb(239 68 68 / 0.35);
}

.danger-card {
  border-color: rgb(239 68 68 / 0.24);
}

.danger-actions {
  justify-content: flex-start;
  margin-top: 20px;
}

.integration-actions {
  gap: 12px;
}

.integration-analysis-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
  margin-top: 12px;
}

.analysis-card {
  padding: 12px;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-surface-hover);
}

.analysis-card strong {
  display: block;
  margin-bottom: 8px;
}

.analysis-card ul {
  margin: 0;
  padding-left: 18px;
}

.analysis-card li {
  margin-bottom: 6px;
  color: var(--color-text-secondary);
}

.metric-grid,
.two-column-grid {
  display: grid;
  gap: 16px;
}

.metric-grid {
  grid-template-columns: repeat(3, minmax(0, 1fr));
  margin-top: 16px;
}

.two-column-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
  margin-top: 16px;
}

.metric-card {
  padding: 16px;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface);
  box-shadow: 0 4px 12px rgb(15 23 42 / 0.035);
}

.metric-card span {
  display: block;
  color: var(--color-text-secondary);
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  margin-bottom: 8px;
}

.metric-card strong {
  color: var(--color-text-primary);
  font-size: 26px;
  font-weight: 600;
}

.compact-list .stack-row {
  padding: 12px 16px;
}

.vertical-actions {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.negative-chip {
  color: #dc2626;
}

.empty-state {
  padding: 18px;
  border-radius: 10px;
  border: 1px dashed var(--color-border);
  color: var(--color-text-muted);
  background: var(--color-surface-hover);
}

.section-split {
  margin: 18px 0 12px;
}

.section-split h3 {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 18px;
  font-weight: 600;
}

.section-split p {
  margin: 6px 0 0;
  color: var(--color-text-secondary);
  font-size: 14px;
}

.helper-panel {
  margin-top: 16px;
  padding: 18px;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface);
}

.dialog-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.form-group {
  min-width: 0;
}

.form-label {
  display: block;
  margin-bottom: 7px;
  color: var(--color-text-secondary);
  font-size: 12px;
  font-weight: 600;
  line-height: 1.35;
  letter-spacing: 0.03em;
  text-transform: uppercase;
}

:deep(.plane-dialog) {
  max-width: calc(100vw - 32px);
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: 16px;
  background: var(--color-surface);
  box-shadow: 0 20px 56px rgb(15 23 42 / 0.16);
}

:deep(.plane-dialog .el-dialog__header) {
  margin: 0;
  padding: 20px 24px;
  border-bottom: 1px solid var(--color-border);
}

:deep(.plane-dialog .el-dialog__title) {
  color: var(--color-text-primary);
  font-size: 18px;
  font-weight: 600;
}

:deep(.plane-dialog .el-dialog__body) {
  padding: 22px 24px;
  color: var(--color-text-primary);
  background: var(--color-surface);
}

:deep(.plane-dialog .el-dialog__footer) {
  padding: 14px 24px 18px;
  border-top: 1px solid var(--color-border);
  background: var(--color-surface);
}

:deep(.plane-dialog .el-select__wrapper) {
  min-height: 44px;
  border: 1px solid #cbd5e1;
  border-radius: 10px;
  background: var(--color-surface);
  box-shadow: none;
}

:deep(.plane-dialog .el-select__wrapper.is-focused) {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-accent) 12%, transparent);
}

:deep(.plane-dialog .el-button) {
  min-height: 42px;
  padding: 0 16px;
  border-radius: 10px;
  font-weight: 600;
}

.primary-btn:disabled,
.secondary-btn:disabled,
.danger-btn:disabled,
.danger-outline-btn:disabled {
  cursor: not-allowed;
  opacity: 0.58;
  transform: none;
  box-shadow: none;
}

@media (max-width: 1100px) {
  .settings-shell {
    grid-template-columns: 1fr;
  }

  .settings-nav {
    position: static;
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .invite-grid,
  .state-grid,
  .label-grid,
  .module-grid,
  .cycle-grid,
  .form-grid,
  .metric-grid,
  .two-column-grid {
    grid-template-columns: 1fr;
  }

  .cover-settings {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 760px) {
  .project-settings-page {
    padding: 18px;
  }

  .settings-header,
  .header-actions,
  .card-head,
  .stack-row,
  .row-actions,
  .danger-actions {
    flex-direction: column;
    align-items: flex-start;
  }

  .settings-nav {
    display: flex !important;
    flex-direction: row !important;
    overflow-x: auto !important;
    white-space: nowrap !important;
    gap: 8px !important;
    padding-bottom: 8px !important;
    border-bottom: 1px solid var(--color-border) !important;
    margin-bottom: 16px !important;
    -webkit-overflow-scrolling: touch;
    width: 100% !important;
  }

  .nav-tab {
    flex: 0 0 auto !important;
    padding: 8px 14px !important;
    display: inline-flex !important;
    flex-direction: column !important;
    align-items: flex-start !important;
    border-radius: 8px !important;
  }

  .state-row {
    grid-template-columns: 1fr;
  }
}

/* ────────────────────────────────────────────
 * SME Permission Matrix Styles
 * ──────────────────────────────────────────── */
.role-selector-tabs {
  display: flex;
  gap: 8px;
  border-bottom: 1px solid var(--color-border);
  padding-bottom: 12px;
}

.role-tab {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  padding: 8px 16px;
  border-radius: 8px;
  cursor: pointer;
  font-weight: 500;
  transition: color 150ms ease, border-color 150ms ease, background-color 150ms ease;
}

.role-tab:hover {
  border-color: color-mix(in srgb, var(--color-accent) 38%, var(--color-border));
  color: var(--color-text-primary);
  background: color-mix(in srgb, var(--color-accent) 4%, var(--color-surface));
}

.role-tab.active {
  background: color-mix(in srgb, var(--color-accent) 9%, var(--color-surface));
  border-color: color-mix(in srgb, var(--color-accent) 28%, var(--color-border));
  color: var(--color-accent);
  font-weight: 600;
}

.matrix-container {
  overflow-x: auto;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface);
}

.matrix-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
}

.matrix-table th,
.matrix-table td {
  padding: 12px 16px;
  border-bottom: 1px solid var(--color-border);
}

.matrix-table th {
  background: var(--color-surface-hover);
  color: var(--color-text-secondary);
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
}

.matrix-table td {
  color: var(--color-text-primary);
  font-size: 14px;
}

.matrix-table tbody tr:hover {
  background: color-mix(in srgb, var(--color-accent) 3%, var(--color-surface));
}

.text-center {
  text-align: center;
}

.matrix-table input[type="checkbox"] {
  width: 18px;
  height: 18px;
  accent-color: var(--color-accent);
  cursor: pointer;
  border: 1px solid var(--color-border);
  border-radius: 4px;
  background: var(--color-surface);
}

.matrix-table input[type="checkbox"]:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.compact-checkboxes {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.compact-label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--color-text-secondary);
  cursor: pointer;
}

.compact-label input[type="checkbox"] {
  width: 16px;
  height: 16px;
}

.matrix-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}
</style>
