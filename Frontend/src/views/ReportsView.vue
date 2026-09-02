<template>
  <ProjectPageContainer scrollable class="space-reports-page">
    <ProjectPageHeader 
      icon="fa-solid fa-chart-line" 
      :title="t('projectTabs.reports')"
      :description="t('reports.analyticsAndInsights')"
    />

    <!-- Loading State -->
    <ProjectLoadingState v-if="loading" :text="t('reports.analyzingProjectData')" />
    
    <!-- Error State -->
    <div v-else-if="error" class="reports-error">
      <i class="fa-solid fa-circle-exclamation text-2xl mb-2"></i>
      <p class="font-semibold">{{ error }}</p>
    </div>

    <!-- Empty State -->
    <ProjectEmptyState 
      v-else-if="allTasks.length === 0"
      icon="fa-solid fa-chart-line"
      :title="t('reports.noTasksPlaceholder')"
      :description="t('reports.noTasksPlaceholderDesc')"
    >
      <template #action>
        <button class="empty-state-action-btn" @click="router.push(buildSpacePath(projectId, 'work-items'))">
          <i class="fa-solid fa-plus"></i> {{ t('reports.createWorkItem') }}
        </button>
      </template>
    </ProjectEmptyState>

    <!-- Main Dashboard Grid -->
    <div v-else class="reports-content">
      
      <!-- Project Health Alert Card (Contains Cycle & Refresh) -->
      <div class="health-alert-card" :class="projectHealth.level">
        <div class="health-left">
          <div class="health-icon">
            <i class="fa-solid" :class="projectHealth.icon"></i>
          </div>
          <div class="health-details">
            <h2 class="health-status-title">{{ t('reports.healthStatus', { status: projectHealth.text }) }}</h2>
            <p class="health-desc">{{ projectHealth.desc }}</p>
          </div>
        </div>
        <div class="health-right">
          <!-- Active Cycle Card -->
          <router-link :to="{ name: 'CyclesView', params: { id: projectId } }" style="text-decoration: none;">
            <div v-if="activeSprint" class="current-cycle-card">
              <div class="cycle-icon-wrapper active">
                <i class="fa-solid fa-arrows-spin fa-spin-pulse"></i>
              </div>
              <div class="cycle-info">
                <h4>{{ activeSprint.name }} <span class="active-badge">ACTIVE</span></h4>
                <p>{{ t('Current running cycle', 'Chu kỳ đang chạy') }}</p>
              </div>
            </div>
            <div v-else class="current-cycle-card empty">
              <div class="cycle-icon-wrapper">
                <i class="fa-solid fa-rotate"></i>
              </div>
              <div class="cycle-info">
                <h4 class="text-gray-600">{{ t('No active cycle', 'Chưa có chu kỳ nào') }}</h4>
                <p>{{ t('Click to plan sprints', 'Bấm để lập kế hoạch') }}</p>
              </div>
            </div>
          </router-link>

          <button class="nexus-btn-outlined" @click="fetchData" :aria-label="t('reports.refresh')">
            <i class="fa-solid fa-rotate-right" :class="{ 'fa-spin': loading }"></i> {{ t('reports.refresh') }}
          </button>
        </div>
      </div>

      <!-- Premium Stats Cards -->
      <div class="reports-stats-grid">
        <!-- Total Tasks -->
        <div class="report-stat-card total-tasks">
          <div class="stat-card-content">
            <div class="stat-icon-wrapper">
              <i class="fa-solid fa-list-check"></i>
            </div>
            <div class="stat-info">
              <span class="label">{{ t('reports.totalTasks') }}</span>
              <span class="value">{{ allTasks.length }}</span>
            </div>
          </div>
        </div>
        
        <!-- Done Tasks -->
        <div class="report-stat-card done-tasks">
          <div class="stat-card-content">
            <div class="stat-icon-wrapper">
              <i class="fa-solid fa-circle-check"></i>
            </div>
            <div class="stat-info">
              <span class="label">{{ t('reports.completedTasks') }}</span>
              <span class="value">
                {{ completedTasksCount }}
                <span class="percentage-tag">{{ completionRate }}%</span>
              </span>
            </div>
          </div>
        </div>
        
        <!-- In Progress -->
        <div class="report-stat-card in-progress">
          <div class="stat-card-content">
            <div class="stat-icon-wrapper">
              <i class="fa-solid fa-clock-rotate-left"></i>
            </div>
            <div class="stat-info">
              <span class="label">{{ t('reports.inProgress') }}</span>
              <span class="value">{{ inProgressTasksCount }}</span>
            </div>
          </div>
        </div>
        
        <!-- Overdue Tasks -->
        <div class="report-stat-card overdue-tasks" :class="{ 'has-overdue': overdueTasksCount > 0 }">
          <div class="stat-card-content">
            <div class="stat-icon-wrapper">
              <i class="fa-solid fa-triangle-exclamation"></i>
            </div>
            <div class="stat-info">
              <span class="label">{{ t('reports.overdueTasks') }}</span>
              <span class="value text-danger">{{ overdueTasksCount }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Top Row: Suggested Tasks (Full Width) -->
            <!-- Top Distributions Row (Suggested Tasks & Team Workload Side-by-Side) -->
            <!-- Top Distributions Row (Suggested Tasks & Team Workload Side-by-Side) -->
      <div class="distributions-grid">
        
        <!-- Cột trái: Suggested Tasks & Việc cần chú ý Accordions -->
        <div class="dashboard-left-panel">
          
          <!-- Suggested Tasks Card -->
          <div class="report-card suggested-panel">
            <div class="panel-header">
              <h3 class="card-title" style="margin-bottom: 0; padding-bottom: 0; border-bottom: none;">
                <i class="fa-solid fa-fire text-orange-500"></i> {{ t('reports.suggestedForToday', 'Suggested for today') }}
              </h3>
              <router-link
                :to="buildSpacePath(projectId, 'work-items')"
                class="panel-link"
              >
                {{ t('reports.viewAllTasks') === 'reports.viewAllTasks' ? 'View all' : t('reports.viewAllTasks') }} <i class="fa-solid fa-arrow-right"></i>
              </router-link>
            </div>

            <div v-if="suggestedTasks.length === 0" class="empty-spaces-flat">
              <div class="empty-spaces-icon" aria-hidden="true">
                <i class="fa-solid fa-mug-hot"></i>
              </div>
              <div class="empty-spaces-copy">
                <h3>{{ t('reports.allCaughtUp', 'You are all caught up!') }}</h3>
                <p>{{ t('reports.noUrgentTasks', 'No urgent tasks suggested for today.') }}</p>
              </div>
            </div>

            <div v-else class="task-list">
              <div
                v-for="task in suggestedTasks"
                :key="task.id"
                class="task-row"
              >
                <div class="task-info-left">
                  <span class="task-seq-id">
                    {{ task.sequenceId || 'TASK' }}
                  </span>
                  <button
                    @click="navigateToTask(task.id)"
                    class="task-title-btn"
                  >
                    {{ task.title }}
                  </button>
                </div>

                <div class="task-meta-right">
                  <span class="task-status-tag" :class="getStatusClass(task.statusName)">
                    <i :class="getStatusIcon(task.statusName)"></i>
                    <span>{{ normalizeStatusLabel(task.statusName) }}</span>
                  </span>
                  <span class="priority-badge" :class="getPriorityClass(task.priority)">
                    <i :class="getPriorityIcon(task.priority)"></i>
                    <span>{{ getPriorityLabel(task.priority) }}</span>
                  </span>
                  <span class="task-deadline-tag" :class="getDeadlineClass(task)">
                    <template v-if="calcDaysLeft(task) !== null">
                      <i class="fa-regular fa-clock"></i>
                      <span>{{ getDeadlineText(task) }}</span>
                    </template>
                    <template v-else>
                      <i class="fa-regular fa-calendar" style="font-size: 11px;"></i>
                      <span style="font-size: 11px; font-weight: 700;">?</span>
                    </template>
                  </span>
                </div>
              </div>
            </div>
            
            <div class="panel-header" style="margin-top: 20px; border-top: 1px dashed rgba(148, 163, 184, 0.4); padding-top: 16px; margin-bottom: 12px;">
              <h3 class="panel-title text-gray-500" style="font-size: 14px;">
                <i class="fa-solid fa-list-ul"></i> {{ t('reports.continueWorking', 'Công việc tiếp theo') }}
              </h3>
            </div>

            <div v-if="continueTasks.length === 0" class="empty-spaces-flat">
              <div class="empty-spaces-icon" aria-hidden="true">
                <i class="fa-solid fa-inbox text-gray-300"></i>
              </div>
              <div class="empty-spaces-copy">
                <p style="margin-top: 6px;">{{ t('reports.noOtherTasks', 'No other active tasks.') }}</p>
              </div>
            </div>

            <div v-else class="task-list" style="opacity: 0.85;">
              <div
                v-for="task in continueTasks"
                :key="task.id"
                class="task-row"
              >
                <div class="task-info-left">
                  <span class="task-seq-id">
                    {{ task.sequenceId || 'TASK' }}
                  </span>
                  <button
                    @click="navigateToTask(task.id)"
                    class="task-title-btn"
                  >
                    {{ task.title }}
                  </button>
                </div>

                <div class="task-meta-right">
                  <span class="task-status-tag" :class="getStatusClass(task.statusName)">
                    <i :class="getStatusIcon(task.statusName)"></i>
                    <span>{{ normalizeStatusLabel(task.statusName) }}</span>
                  </span>
                  <span class="priority-badge" :class="getPriorityClass(task.priority)">
                    <i :class="getPriorityIcon(task.priority)"></i>
                    <span>{{ getPriorityLabel(task.priority) }}</span>
                  </span>
                  <span class="task-deadline-tag" :class="getDeadlineClass(task)">
                    <template v-if="calcDaysLeft(task) !== null">
                      <i class="fa-regular fa-clock"></i>
                      <span>{{ getDeadlineText(task) }}</span>
                    </template>
                    <template v-else>
                      <i class="fa-regular fa-calendar" style="font-size: 11px;"></i>
                      <span style="font-size: 11px; font-weight: 700;">?</span>
                    </template>
                  </span>
                </div>
              </div>
            </div>
          </div>

          <!-- Việc cần chú ý Accordions -->
          <div v-if="false" class="report-card attention-panel">
            <h3 class="card-title">
              <i class="fa-solid fa-bell text-rose-500"></i> {{ t('reports.attentionTitle') }}
            </h3>

            <div class="attention-accordions">
              
              <!-- 1. Quá hạn -->
              <div class="accordion-item" :class="{ active: activeAccordion === 'overdue' }">
                <div class="accordion-header" @click="activeAccordion = activeAccordion === 'overdue' ? '' : 'overdue'">
                  <div class="header-left">
                    <span class="badge danger-bg">{{ overdueTasksCount }}</span>
                    <span class="header-text">{{ t('reports.overdueTasks') }}</span>
                  </div>
                  <i class="fa-solid" :class="activeAccordion === 'overdue' ? 'fa-chevron-up' : 'fa-chevron-down'"></i>
                </div>
                <div class="accordion-content" v-show="activeAccordion === 'overdue'">
                  <div v-if="overdueTasks.length === 0" class="empty-substate text-success">
                    <i class="fa-solid fa-circle-check mr-2"></i> {{ t('reports.noOverdueTasks') }}
                  </div>
                  <div v-else class="attention-list">
                    <div v-for="task in overdueTasks" :key="task.id" class="attention-task-row">
                      <div class="task-info" @click="navigateToTask(task.id)">
                        <span class="task-key">{{ task.sequenceId || 'TASK' }}</span>
                        <span class="task-title">{{ task.title }}</span>
                        <span class="task-assignee">{{ t('reports.assignee') }}: {{ getAssigneeNames(task) }}</span>
                        <span class="task-due-date text-danger">{{ t('reports.dueDate') }} {{ formatDate(task.dueDate) }}</span>
                      </div>
                      <button 
                        class="remind-btn" 
                        @click="triggerReminder(task)"
                        :disabled="sendingReminders[task.id] || !task.assignees || task.assignees.length === 0"
                        :title="(!task.assignees || task.assignees.length === 0) ? t('reports.unassignedTooltip') : ''"
                      >
                        <i class="fa-solid fa-paper-plane"></i> {{ t('reports.remind') }}
                      </button>
                    </div>
                  </div>
                </div>
              </div>

              <!-- 2. Sắp đến hạn -->
              <div class="accordion-item" :class="{ active: activeAccordion === 'upcoming' }">
                <div class="accordion-header" @click="activeAccordion = activeAccordion === 'upcoming' ? '' : 'upcoming'">
                  <div class="header-left">
                    <span class="badge warning-bg">{{ upcomingTasks.length }}</span>
                    <span class="header-text">{{ t('reports.upcomingTasks') }}</span>
                  </div>
                  <i class="fa-solid" :class="activeAccordion === 'upcoming' ? 'fa-chevron-up' : 'fa-chevron-down'"></i>
                </div>
                <div class="accordion-content" v-show="activeAccordion === 'upcoming'">
                  <div v-if="upcomingTasks.length === 0" class="empty-substate">
                    {{ t('reports.noUpcomingTasks') }}
                  </div>
                  <div v-else class="attention-list">
                    <div v-for="task in upcomingTasks" :key="task.id" class="attention-task-row">
                      <div class="task-info" @click="navigateToTask(task.id)">
                        <span class="task-key">{{ task.sequenceId || 'TASK' }}</span>
                        <span class="task-title">{{ task.title }}</span>
                        <span class="task-assignee">{{ t('reports.assignee') }}: {{ getAssigneeNames(task) }}</span>
                        <span class="task-due-date text-warning">{{ t('reports.dueDate') }} {{ formatDate(task.dueDate) }}</span>
                      </div>
                      <button 
                        class="remind-btn" 
                        @click="triggerReminder(task)"
                        :disabled="sendingReminders[task.id] || !task.assignees || task.assignees.length === 0"
                        :title="(!task.assignees || task.assignees.length === 0) ? t('reports.unassignedTooltip') : ''"
                      >
                        <i class="fa-solid fa-paper-plane"></i> {{ t('reports.remind') }}
                      </button>
                    </div>
                  </div>
                </div>
              </div>

              <!-- 3. Chưa có người phụ trách -->
              <div class="accordion-item" :class="{ active: activeAccordion === 'unassigned' }">
                <div class="accordion-header" @click="activeAccordion = activeAccordion === 'unassigned' ? '' : 'unassigned'">
                  <div class="header-left">
                    <span class="badge gray-bg">{{ unassignedTasks.length }}</span>
                    <span class="header-text">{{ t('reports.unassignedTasks') }}</span>
                  </div>
                  <i class="fa-solid" :class="activeAccordion === 'unassigned' ? 'fa-chevron-up' : 'fa-chevron-down'"></i>
                </div>
                <div class="accordion-content" v-show="activeAccordion === 'unassigned'">
                  <div v-if="unassignedTasks.length === 0" class="empty-substate text-success">
                    <i class="fa-solid fa-circle-check mr-2"></i> {{ t('reports.allTasksAssigned') }}
                  </div>
                  <div v-else class="attention-list">
                    <div v-for="task in unassignedTasks" :key="task.id" class="attention-task-row plain-row" @click="navigateToTask(task.id)">
                      <div class="task-info">
                        <span class="task-key">{{ task.sequenceId || 'TASK' }}</span>
                        <span class="task-title">{{ task.title }}</span>
                        <span class="task-due-date">{{ t('reports.dueDate') }} {{ formatDate(task.dueDate) || t('reports.noDueDate') }}</span>
                      </div>
                      <span class="unassigned-badge"><i class="fa-solid fa-user-slash"></i> {{ t('reports.unassigned') }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- 4. Việc bị kẹt lâu -->
              <div class="accordion-item" :class="{ active: activeAccordion === 'stuck' }">
                <div class="accordion-header" @click="activeAccordion = activeAccordion === 'stuck' ? '' : 'stuck'">
                  <div class="header-left">
                    <span class="badge info-bg">{{ stuckTasks.length }}</span>
                    <span class="header-text">{{ t('reports.stuckTasks') }}</span>
                  </div>
                  <i class="fa-solid" :class="activeAccordion === 'stuck' ? 'fa-chevron-up' : 'fa-chevron-down'"></i>
                </div>
                <div class="accordion-content" v-show="activeAccordion === 'stuck'">
                  <div v-if="stuckTasks.length === 0" class="empty-substate">
                    {{ t('reports.noStuckTasks') }}
                  </div>
                  <div v-else class="attention-list">
                    <div v-for="task in stuckTasks" :key="task.id" class="attention-task-row">
                      <div class="task-info" @click="navigateToTask(task.id)">
                        <span class="task-key">{{ task.sequenceId || 'TASK' }}</span>
                        <span class="task-title">{{ task.title }}</span>
                        <span class="task-assignee">{{ t('reports.assignee') }}: {{ getAssigneeNames(task) }}</span>
                        <span class="task-status">{{ t('reports.status') }}: {{ getStatusLabel(task.statusName) }}</span>
                        <span class="task-due-date">{{ t('reports.lastUpdated') }}: {{ formatDate(task.updatedAt || task.createdAt) }}</span>
                      </div>
                      <button 
                        class="remind-btn" 
                        @click="triggerReminder(task)"
                        :disabled="sendingReminders[task.id] || !task.assignees || task.assignees.length === 0"
                        :title="(!task.assignees || task.assignees.length === 0) ? t('reports.unassignedTooltip') : ''"
                      >
                        <i class="fa-solid fa-paper-plane"></i> {{ t('reports.followUp') }}
                      </button>
                    </div>
                  </div>
                </div>
              </div>

            </div>
          </div>

        </div>
        
        <!-- Cột phải: Team Workload & Status Distribution -->
        <div class="dashboard-right-panel">
          
          <!-- Team Workload Panel Card -->
          <div class="report-card team-workload-card">
            <h3 class="card-title">
              <i class="fa-solid fa-users text-blue-500"></i> {{ t('reports.teamWorkload', 'Khối lượng công việc của đội') }}
            </h3>

            <div v-if="teamWorkload.length === 0" class="empty-spaces-flat">
              <div class="empty-spaces-icon" aria-hidden="true">
                <i class="fa-solid fa-users"></i>
              </div>
              <div class="empty-spaces-copy">
                <h3>{{ t('reports.workloadDist', 'Phân bổ công việc') }}</h3>
                <p>{{ t('reports.assignTasksHint', 'Giao việc cho thành viên để theo dõi khối lượng công việc tại đây.') }}</p>
              </div>
            </div>

            <div v-else class="workload-list">
              <div
                v-for="member in teamWorkload"
                :key="member.userId"
                class="workload-item"
              >
                <div class="workload-item-header">
                  <div class="workload-user">
                    <div class="user-avatar" style="background: transparent; border: none;" v-if="member.userId === 'unassigned'">
                      <div style="width: 26px; height: 26px; border-radius: 50%; background: #e2e8f0; color: #64748b; display: flex; align-items: center; justify-content: center;">
                        <i class="fa-solid fa-question text-xs"></i>
                      </div>
                    </div>
                    <UserAvatar v-else :user="member" :size="26" :fontSize="11" />
                    <span class="user-name">{{ member.fullName }}</span>
                  </div>

                  <div class="workload-meta-right" style="display: flex; align-items: center; gap: 8px;">
                    <div class="legend-value" style="display: flex; align-items: center; gap: 4px;">
                      <span class="legend-count">{{ member.count }} {{ member.count === 1 ? 'task' : 'tasks' }}</span>
                      <span class="legend-percent">({{ member.percentage }}%)</span>
                    </div>

                    <button 
                      v-if="member.userId !== 'unassigned'"
                      class="remind-btn"
                      style="padding: 2px 7px; font-size: 11px; height: 24px; min-width: auto;"
                      @click="triggerMemberReminder(member)"
                      :disabled="sendingMemberReminders[member.userId]"
                      :title="t('reports.remindMemberTooltip', 'Gửi nhắc nhở tiến độ')"
                    >
                      <i class="fa-solid fa-paper-plane" style="font-size: 10px;"></i>
                      <span>{{ t('reports.remind', 'Nhắc nhở') }}</span>
                    </button>
                  </div>
                </div>

                <div class="workload-progress-track">
                  <div
                    class="workload-progress-bar"
                    :class="{ 'is-unassigned': member.userId === 'unassigned' }"
                    :style="{ width: `${member.percentage}%` }"
                  ></div>
                </div>
              </div>
            </div>
          </div>

        </div>

      </div>

      <!-- Bottom distributions: Priority & Status tabs -->
      <div class="bottom-distributions-grid">
        <div class="report-card distribution-tabs-panel">
          <div class="distribution-tabs-header">
            <button
              type="button"
              class="distribution-tab-btn"
              :class="{ active: activeDistributionTab === 'priority' }"
              @click="activeDistributionTab = 'priority'"
            >
              <i class="fa-solid fa-layer-group"></i>
              <span>{{ t('reports.priorityDistribution', 'Priority Distribution') }}</span>
            </button>
            <button
              type="button"
              class="distribution-tab-btn"
              :class="{ active: activeDistributionTab === 'status' }"
              @click="activeDistributionTab = 'status'"
            >
              <i class="fa-solid fa-chart-pie"></i>
              <span>{{ t('reports.statusDistribution', 'Status Distribution') }}</span>
            </button>
          </div>

          <div v-if="activeDistributionTab === 'priority'" class="distribution-focus-grid">
            <!-- Left side: Donut chart & Priority legend (removed extra selector buttons) -->
            <div class="distribution-left-side">
              <div class="priority-chart-container">
                <div class="donut-chart-wrapper">
                  <svg viewBox="0 0 36 36" class="donut-chart">
                    <circle cx="18" cy="18" r="14.5" fill="none" stroke="var(--color-border)" stroke-width="3.5"></circle>
                    <circle 
                      v-for="seg in prioritySegments"
                      :key="seg.label"
                      cx="18" 
                      cy="18" 
                      r="14.5" 
                      fill="none" 
                      :stroke="seg.color" 
                      stroke-width="3.5"
                      :stroke-dasharray="`${seg.percent} ${100 - seg.percent}`"
                      :stroke-dashoffset="seg.offset"
                      class="donut-segment"
                    ></circle>
                  </svg>
                  <div class="donut-center">
                    <span class="donut-number">{{ allTasks.length }}</span>
                    <span class="donut-label">{{ t('reports.tasks', 'Tasks') }}</span>
                  </div>
                </div>

                <div class="priority-legend">
                  <div 
                    v-for="seg in prioritySegments" 
                    :key="seg.label"
                    class="legend-item"
                  >
                    <div class="legend-info">
                      <span class="legend-color-dot" :style="{ background: seg.color }"></span>
                      <span class="legend-label">{{ seg.displayLabel }}</span>
                    </div>
                    <div class="legend-value">
                      <span class="legend-count">{{ seg.count }}</span>
                      <span class="legend-percent">{{ Math.round(seg.percent) }}%</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Right side: Priority Task List Accordions (Daily Focus style with collapse/expand) -->
            <div class="distribution-right-side">
              <div class="priority-accordions">
                <div 
                  v-for="group in priorityGroupList" 
                  :key="group.key"
                  class="priority-group-accordion"
                  :class="{ 'is-open': openPriorityGroups[group.key] }"
                >
                  <!-- Accordion Header -->
                  <div 
                    class="priority-group-header" 
                    @click="togglePriorityGroup(group.key)"
                  >
                    <div class="p-header-left">
                      <i :class="group.icon" :style="{ color: group.color }"></i>
                      <span class="p-group-title">{{ group.label }}</span>
                      <span class="distribution-count-pill" :style="{ color: group.color, backgroundColor: group.bgColor }">
                        {{ group.tasks.length }} {{ group.tasks.length === 1 ? 'task' : 'tasks' }}
                      </span>
                    </div>
                    <i class="fa-solid" :class="openPriorityGroups[group.key] ? 'fa-chevron-up' : 'fa-chevron-down'"></i>
                  </div>

                  <!-- Accordion Content -->
                  <div class="priority-group-content" v-show="openPriorityGroups[group.key]">
                    <div v-if="group.tasks.length === 0" class="empty-substate">
                      <i class="fa-solid fa-circle-check text-green-500 mr-2"></i>
                      <span>{{ t('reports.noTasksInPriority', 'Không có công việc nào ở mức độ này.') }}</span>
                    </div>

                    <div v-else class="task-list">
                      <div
                        v-for="task in group.tasks"
                        :key="task.id"
                        class="task-row"
                      >
                        <div class="task-info-left">
                          <span class="task-seq-id">
                            {{ task.sequenceId || 'TASK' }}
                          </span>
                          <button
                            @click="navigateToTask(task.id)"
                            class="task-title-btn"
                          >
                            {{ task.title }}
                          </button>
                        </div>

                        <div class="task-meta-right">
                          <span class="task-status-tag" :class="getStatusClass(task.statusName)">
                            <i :class="getStatusIcon(task.statusName)"></i>
                            <span>{{ normalizeStatusLabel(task.statusName) }}</span>
                          </span>
                          <span class="task-deadline-tag" :class="getDeadlineClass(task)">
                            <template v-if="calcDaysLeft(task) !== null">
                              <i class="fa-regular fa-clock"></i>
                              <span>{{ getDeadlineText(task) }}</span>
                            </template>
                            <template v-else>
                              <i class="fa-regular fa-calendar" style="font-size: 11px;"></i>
                              <span style="font-size: 11px; font-weight: 700;">?</span>
                            </template>
                          </span>
                        </div>
                      </div>
                    </div>
                  </div>

                </div>
              </div>
            </div>
          </div>

          <div v-else class="distribution-focus-grid">
            <!-- Left side: Donut chart & Status legend -->
            <div class="distribution-left-side">
              <div class="priority-chart-container">
                <div class="donut-chart-wrapper">
                  <svg viewBox="0 0 36 36" class="donut-chart">
                    <circle cx="18" cy="18" r="14.5" fill="none" stroke="var(--color-border)" stroke-width="3.5"></circle>
                    <circle 
                      v-for="seg in statusSegments"
                      :key="seg.name"
                      cx="18" 
                      cy="18" 
                      r="14.5" 
                      fill="none" 
                      :stroke="seg.color" 
                      stroke-width="3.5"
                      :stroke-dasharray="seg.dasharray"
                      :stroke-dashoffset="seg.dashoffset"
                      class="donut-segment"
                    ></circle>
                  </svg>
                  <div class="donut-center">
                    <span class="donut-number">{{ allTasks.length }}</span>
                    <span class="donut-label">{{ t('reports.tasks', 'Tasks') }}</span>
                  </div>
                </div>

                <div class="priority-legend">
                  <div 
                    v-for="seg in statusSegments" 
                    :key="seg.name"
                    class="legend-item"
                  >
                    <div class="legend-info">
                      <span class="legend-color-dot" :style="{ background: seg.color }"></span>
                      <span class="legend-label">{{ seg.label }}</span>
                    </div>
                    <div class="legend-value">
                      <span class="legend-count">{{ seg.count }}</span>
                      <span class="legend-percent">{{ seg.percent }}%</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Right side: Status Task List Accordions -->
            <div class="distribution-right-side">
              <div class="priority-accordions">
                <div 
                  v-for="group in statusGroupList" 
                  :key="group.name"
                  class="priority-group-accordion"
                  :class="{ 'is-open': isStatusGroupOpen(group.name) }"
                >
                  <!-- Accordion Header -->
                  <div 
                    class="priority-group-header" 
                    @click="toggleStatusGroup(group.name)"
                  >
                    <div class="p-header-left">
                      <i :class="group.icon" :style="{ color: group.color }"></i>
                      <span class="p-group-title">{{ group.label }}</span>
                      <span class="distribution-count-pill" :style="{ backgroundColor: group.bgColor, color: group.color }">
                        {{ group.tasks.length }} {{ group.tasks.length === 1 ? 'task' : 'tasks' }}
                      </span>
                    </div>
                    <i class="fa-solid" :class="isStatusGroupOpen(group.name) ? 'fa-chevron-up' : 'fa-chevron-down'"></i>
                  </div>

                  <!-- Accordion Content -->
                  <div class="priority-group-content" v-show="isStatusGroupOpen(group.name)">
                    <div v-if="group.tasks.length === 0" class="empty-substate">
                      <i class="fa-solid fa-circle-check text-green-500 mr-2"></i>
                      <span>{{ t('reports.noTasksInStatus', 'Không có công việc nào ở trạng thái này.') }}</span>
                    </div>

                    <div v-else class="task-list">
                      <div
                        v-for="task in group.tasks"
                        :key="task.id"
                        class="task-row"
                      >
                        <div class="task-info-left">
                          <span class="task-seq-id">
                            {{ task.sequenceId || 'TASK' }}
                          </span>
                          <button
                            @click="navigateToTask(task.id)"
                            class="task-title-btn"
                          >
                            {{ task.title }}
                          </button>
                        </div>

                        <div class="task-meta-right">
                          <span class="priority-badge" :class="getPriorityClass(task.priority)">
                            <i :class="getPriorityIcon(task.priority)"></i>
                            <span>{{ getPriorityLabel(task.priority) }}</span>
                          </span>
                          <span class="task-deadline-tag" :class="getDeadlineClass(task)">
                            <template v-if="calcDaysLeft(task) !== null">
                              <i class="fa-regular fa-clock"></i>
                              <span>{{ getDeadlineText(task) }}</span>
                            </template>
                            <template v-else>
                              <i class="fa-regular fa-calendar" style="font-size: 11px;"></i>
                              <span style="font-size: 11px; font-weight: 700;">?</span>
                            </template>
                          </span>
                        </div>
                      </div>
                    </div>
                  </div>

                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

</div>
  </ProjectPageContainer>
</template>
<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from '@/composables/useI18n'
import UserAvatar from '@/components/common/UserAvatar.vue'
import ProjectPageContainer from '@/components/common/ProjectPageContainer.vue'
import ProjectPageHeader from '@/components/common/ProjectPageHeader.vue'
import ProjectEmptyState from '@/components/common/ProjectEmptyState.vue'
import ProjectLoadingState from '@/components/common/ProjectLoadingState.vue'
import { useWorkTaskStore } from '@/store/useWorkTaskStore'
import { useSprintStore } from '@/store/useSprintStore'
import { getStoredUser } from '@/utils/permissions'
import axiosClient from '@/api/axiosClient'
import { ElMessage } from 'element-plus'
import { buildSpacePath } from '@/utils/spaceRoute'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const projectId = computed(() => route.params.id)
const workTaskStore = useWorkTaskStore()
const sprintStore = useSprintStore()

const loading = ref(false)
const error = ref(null)
const activeAccordion = ref('overdue')
const sendingReminders = ref({})
const sendingMemberReminders = ref({})
const activeDistributionTab = ref('priority')

const activeSprint = computed(() => sprintStore.activeSprint)

const allTasks = computed(() => workTaskStore.tasks || [])

const doneStatuses = ['done', 'completed', 'finished', 'hoàn thành', 'success', 'hoàn tất']
const cancelStatuses = ['cancel', 'cancelled', 'hủy', 'hủy bỏ']

const completedTasksCount = computed(() => {
  return allTasks.value.filter(task => {
    const s = (task.statusName || '').toLowerCase().trim()
    return doneStatuses.includes(s)
  }).length
})

const inProgressTasksCount = computed(() => {
  return allTasks.value.filter(task => {
    const s = (task.statusName || '').toLowerCase().trim()
    return s === 'in progress' || s === 'inprogress'
  }).length
})

const completionRate = computed(() => {
  const total = allTasks.value.length
  if (total === 0) return 0
  return Math.round((completedTasksCount.value / total) * 100)
})

const todayStr = new Date().toISOString().slice(0, 10)

// 1. Project status logic (Green / Yellow / Red)
const projectHealth = computed(() => {
  const total = allTasks.value.length
  if (total === 0) return { level: 'gray', text: t('reports.health.noData'), icon: 'fa-triangle-exclamation', desc: t('reports.health.noDataDesc') }
  
  const completed = completedTasksCount.value
  const overdue = overdueTasksCount.value
  const compRate = completionRate.value
  const overdueRate = Math.round((overdue / total) * 100)
  
  if (overdueRate >= 30 || compRate < 40) {
    return {
      level: 'red',
      text: t('reports.health.red'),
      icon: 'fa-circle-xmark',
      desc: t('reports.health.redDesc', { completion: compRate, overdue, overdueRate })
    }
  } else if (overdue > 0 || compRate < 70) {
    return {
      level: 'yellow',
      text: t('reports.health.yellow'),
      icon: 'fa-triangle-exclamation',
      desc: t('reports.health.yellowDesc', { overdue, overdueRate, completion: compRate })
    }
  } else {
    return {
      level: 'green',
      text: t('reports.health.green'),
      icon: 'fa-circle-check',
      desc: t('reports.health.greenDesc', { completion: compRate })
    }
  }
})

// 2. Attention Tasks filters
const overdueTasks = computed(() => {
  return allTasks.value.filter(task => {
    if (!task.dueDate) return false
    const s = (task.statusName || '').toLowerCase().trim()
    const isCompleted = doneStatuses.includes(s) || cancelStatuses.includes(s)
    return !isCompleted && task.dueDate < todayStr
  }).sort((a, b) => new Date(a.dueDate) - new Date(b.dueDate))
})

const overdueTasksCount = computed(() => overdueTasks.value.length)

const upcomingTasks = computed(() => {
  const today = new Date()
  today.setHours(0,0,0,0)
  
  return allTasks.value.filter(task => {
    if (!task.dueDate) return false
    const s = (task.statusName || '').toLowerCase().trim()
    const isCompleted = doneStatuses.includes(s) || cancelStatuses.includes(s)
    if (isCompleted) return false
    
    const dueDate = new Date(task.dueDate)
    dueDate.setHours(0,0,0,0)
    
    const diffTime = dueDate.getTime() - today.getTime()
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24))
    
    // Within 48 hours (0 to 2 days)
    return diffDays >= 0 && diffDays <= 2
  }).sort((a, b) => new Date(a.dueDate) - new Date(b.dueDate))
})

const unassignedTasks = computed(() => {
  return allTasks.value.filter(task => {
    const s = (task.statusName || '').toLowerCase().trim()
    const isCompleted = doneStatuses.includes(s) || cancelStatuses.includes(s)
    if (isCompleted) return false
    return !task.assignees || task.assignees.length === 0
  })
})

const stuckTasks = computed(() => {
  const twoWeeksAgo = new Date()
  twoWeeksAgo.setDate(twoWeeksAgo.getDate() - 14)
  
  return allTasks.value.filter(task => {
    const s = (task.statusName || '').toLowerCase().trim()
    const isCompleted = doneStatuses.includes(s) || cancelStatuses.includes(s)
    if (isCompleted) return false
    
    const taskDate = new Date(task.updatedAt || task.createdAt)
    return taskDate < twoWeeksAgo
  })
})

// 3. Members remind workload list
const membersToRemind = computed(() => {
  const membersMap = {}
  
  allTasks.value.forEach(task => {
    const s = (task.statusName || '').toLowerCase().trim()
    const isCompleted = doneStatuses.includes(s) || cancelStatuses.includes(s)
    if (isCompleted) return
    
    const taskAssignees = task.assignees || []
    
    taskAssignees.forEach(assignee => {
      const uid = assignee.userId || assignee.id
      if (!uid) return
      
      if (!membersMap[uid]) {
        const name = assignee.fullName || assignee.name || t('reports.member')
        membersMap[uid] = {
          userId: uid,
          fullName: name,
          avatar: assignee.initials || name.substring(0, 1).toUpperCase(),
          avatarColor: assignee.avatarColor || assignee.AvatarColor || '#3b82f6',
          avatarUrl: assignee.avatarUrl || assignee.AvatarUrl || null,
          pendingCount: 0,
          overdueCount: 0,
          nearestDeadlineTask: null,
          tasks: []
        }
      }
      
      const memberData = membersMap[uid]
      memberData.pendingCount++
      memberData.tasks.push(task)
      
      const isOverdue = task.dueDate && task.dueDate < todayStr
      if (isOverdue) {
        memberData.overdueCount++
      }
      
      if (task.dueDate) {
        if (!memberData.nearestDeadlineTask || task.dueDate < memberData.nearestDeadlineTask.dueDate) {
          memberData.nearestDeadlineTask = {
            id: task.id,
            title: task.title,
            dueDate: task.dueDate
          }
        }
      }
    })
  })
  
  return Object.values(membersMap).sort((a, b) => b.overdueCount - a.overdueCount || b.pendingCount - a.pendingCount)
})

// 4. Trigger reminder logic (P1)
const triggerReminder = async (task) => {
  const assignees = task.assignees || []
  if (assignees.length === 0) {
    ElMessage.warning(t('reports.remindUnassignedError'))
    return
  }

  sendingReminders.value[task.id] = true
  const currentUser = getStoredUser()
  const actorName = currentUser?.fullName || currentUser?.username || t('reports.manager')
  let succeedCount = 0
  let skippedCount = 0
  let hasFailed = false
  let errorMsg = t('reports.remindError')

  for (const assignee of assignees) {
    const uid = assignee.userId || assignee.id
    if (!uid) continue
    
    const payload = {
      projectId: projectId.value,
      taskId: task.id,
      assigneeUserId: uid,
      projectName: 'SprintA',
      taskTitle: task.title,
      actorName: actorName
    }

    try {
      const res = await axiosClient.post('/notifications/events/task-reminded', payload)
      if (res.data && res.data.skipped) {
        skippedCount++
      } else if (res.data && res.data.data && res.data.data.notificationId) {
        succeedCount++
      }
    } catch (err) {
      console.error('Failed to send reminder via backend', err)
      hasFailed = true
      if (err.response?.data?.message) {
        errorMsg = err.response.data.message
      } else if (err.response?.data) {
        errorMsg = typeof err.response.data === 'string' ? err.response.data : errorMsg
      }
    }
  }

  if (succeedCount > 0) {
    ElMessage.success(t('reports.remindSuccess'))
  }
  if (skippedCount > 0 && succeedCount === 0) {
    ElMessage.warning(t('reports.remindSelfWarning'))
  }
  if (hasFailed) {
    ElMessage.error(errorMsg)
  }
  sendingReminders.value[task.id] = false
}

const triggerMemberReminder = async (member) => {
  sendingMemberReminders.value[member.userId] = true
  const currentUser = getStoredUser()
  const actorName = currentUser?.fullName || currentUser?.username || t('reports.manager')
  const pendingTasks = member.tasks || []
  
  if (pendingTasks.length === 0) {
    ElMessage.info(t('reports.memberNoPending'))
    sendingMemberReminders.value[member.userId] = false
    return
  }
  
  const taskToRemind = pendingTasks.find(t => t.dueDate && t.dueDate < todayStr) || pendingTasks[0]

  const payload = {
    projectId: projectId.value,
    taskId: taskToRemind.id,
    assigneeUserId: member.userId,
    projectName: 'SprintA',
    taskTitle: taskToRemind.title,
    actorName: actorName
  }

  try {
    const res = await axiosClient.post('/notifications/events/task-reminded', payload)
    if (res.data && res.data.skipped) {
      ElMessage.warning(t('reports.remindSelfWarning'))
    } else if (res.data && res.data.data && res.data.data.notificationId) {
      ElMessage.success(t('reports.memberRemindSuccess', { name: member.fullName }))
    }
  } catch (err) {
    console.error('Failed to remind member via backend', err)
    let errorMsg = t('reports.remindError')
    if (err.response?.data?.message) {
      errorMsg = err.response.data.message
    } else if (err.response?.data) {
      errorMsg = typeof err.response.data === 'string' ? err.response.data : errorMsg
    }
    ElMessage.error(errorMsg)
  } finally {
    sendingMemberReminders.value[member.userId] = false
  }
}

// Charts data computations
const defaultStatusDefinitions = computed(() => [
  { name: 'BACKLOG', label: t('workItems.statusLabels.backlog', 'Backlog'), color: '#71717a', bgColor: 'rgba(113, 113, 122, 0.12)', icon: 'fa-regular fa-circle-dashed' },
  { name: 'TO DO', label: t('workItems.statusLabels.toDo', 'To Do'), color: '#64748b', bgColor: 'rgba(100, 116, 139, 0.12)', icon: 'fa-regular fa-circle' },
  { name: 'IN PROGRESS', label: t('workItems.statusLabels.inProgress', 'In Progress'), color: '#0ea5e9', bgColor: 'rgba(14, 165, 233, 0.12)', icon: 'fa-solid fa-circle-half-stroke' },
  { name: 'IN REVIEW', label: t('workItems.statusLabels.inReview', 'In Review'), color: '#f59e0b', bgColor: 'rgba(245, 158, 11, 0.14)', icon: 'fa-solid fa-eye' },
  { name: 'DONE', label: t('workItems.statusLabels.done', 'Done'), color: '#10b981', bgColor: 'rgba(16, 185, 129, 0.12)', icon: 'fa-solid fa-circle-check' },
  { name: 'CANCELLED', label: t('workItems.statusLabels.cancelled', 'Cancelled'), color: '#f43f5e', bgColor: 'rgba(244, 63, 94, 0.12)', icon: 'fa-regular fa-circle-xmark' }
])

const normalizeStatusKey = (statusName = '') => {
  const normalized = `${statusName || 'BACKLOG'}`.toUpperCase().replace(/\s+/g, ' ').trim()
  if (normalized === 'TODO') return 'TO DO'
  if (normalized === 'INPROGRESS') return 'IN PROGRESS'
  if (normalized === 'REVIEW') return 'IN REVIEW'
  if (normalized === 'COMPLETE' || normalized === 'COMPLETED') return 'DONE'
  if (normalized === 'CANCEL' || normalized === 'CANCELED') return 'CANCELLED'
  return normalized || 'BACKLOG'
}

const getStatusDefinition = (statusName = '') => {
  const key = normalizeStatusKey(statusName)
  const defined = defaultStatusDefinitions.value.find(item => item.name === key)
  if (defined) return defined
  return {
    name: key,
    label: statusName || key,
    color: '#8b5cf6',
    bgColor: 'rgba(139, 92, 246, 0.12)',
    icon: 'fa-solid fa-triangle-exclamation'
  }
}

const priorityDefinitions = computed(() => [
  { key: 'urgent', value: 1, label: t('workItems.priority.urgent', 'Urgent'), chartLabel: 'Urgent', icon: 'fa-solid fa-angles-up', color: '#ef4444', bgColor: 'rgba(239, 68, 68, 0.12)' },
  { key: 'high', value: 2, label: t('workItems.priority.high', 'High'), chartLabel: 'High', icon: 'fa-solid fa-chevron-up', color: '#f97316', bgColor: 'rgba(249, 115, 22, 0.12)' },
  { key: 'normal', value: 3, label: t('workItems.priority.normal', 'Normal'), chartLabel: 'Normal', icon: 'fa-solid fa-minus', color: '#3b82f6', bgColor: 'rgba(59, 130, 246, 0.12)' },
  { key: 'low', value: 4, label: t('workItems.priority.low', 'Low'), chartLabel: 'Low', icon: 'fa-solid fa-chevron-down', color: '#10b981', bgColor: 'rgba(16, 185, 129, 0.12)' },
  { key: 'none', value: 0, label: t('workItems.priority.none', 'None'), chartLabel: 'None', icon: 'fa-solid fa-ban', color: '#64748b', bgColor: 'rgba(100, 116, 139, 0.12)' }
])

const statusDistribution = computed(() => {
  if (allTasks.value.length === 0) return []
  
  const statusCounts = Object.fromEntries(defaultStatusDefinitions.value.map(item => [item.name, 0]))
  allTasks.value.forEach(task => {
    const s = normalizeStatusKey(task.statusName)
    statusCounts[s] = (statusCounts[s] || 0) + 1
  })

  return Object.entries(statusCounts).map(([name, count]) => ({
    name,
    count,
    percentage: Math.round((count / allTasks.value.length) * 100)
  })).sort((a, b) => {
    if (b.count !== a.count) return b.count - a.count
    const ai = defaultStatusDefinitions.value.findIndex(item => item.name === a.name)
    const bi = defaultStatusDefinitions.value.findIndex(item => item.name === b.name)
    return (ai === -1 ? 99 : ai) - (bi === -1 ? 99 : bi)
  })
})

const statusSegments = computed(() => {
  const total = allTasks.value.length
  if (total === 0 || statusDistribution.value.length === 0) return []

  let currentOffset = 0
  return statusDistribution.value.map(st => {
    const percent = st.percentage
    const strokeDasharray = `${percent} ${100 - percent}`
    const strokeDashoffset = -currentOffset
    currentOffset += percent
    return {
      name: st.name,
      label: getStatusLabel(st.name),
      count: st.count,
      percent,
      color: getStatusColor(st.name),
      bgColor: getStatusBgColor(st.name),
      dasharray: strokeDasharray,
      dashoffset: strokeDashoffset
    }
  })
})

const selectedPriorityFilter = ref(1)

const priorityCategories = computed(() => {
  const counts = { 1: 0, 2: 0, 3: 0, 4: 0 }
  allTasks.value.forEach(t => {
    const p = Number(t.priority || 0)
    if (p >= 1 && p <= 4) counts[p]++
  })

  return [
    { key: 1, label: t('workItems.priority.urgent', 'Urgent'), icon: 'fa-solid fa-fire text-rose-500', color: '#ef4444', bgColor: '#fef2f2', count: counts[1] },
    { key: 2, label: t('workItems.priority.high', 'High'), icon: 'fa-solid fa-angles-up text-orange-500', color: '#f97316', bgColor: '#fff7ed', count: counts[2] },
    { key: 3, label: t('workItems.priority.normal', 'Normal'), icon: 'fa-solid fa-minus text-blue-500', color: '#3b82f6', bgColor: '#eff6ff', count: counts[3] },
    { key: 4, label: t('workItems.priority.low', 'Low'), icon: 'fa-solid fa-chevron-down text-emerald-500', color: '#10b981', bgColor: '#ecfdf5', count: counts[4] }
  ]
})

const currentPriorityCategory = computed(() => {
  return priorityCategories.value.find(c => c.key === selectedPriorityFilter.value) || priorityCategories.value[0]
})

const filteredPriorityTasks = computed(() => {
  return allTasks.value.filter(t => Number(t.priority || 0) === selectedPriorityFilter.value)
})

const prioritySegments = computed(() => {
  if (allTasks.value.length === 0) return []

  const counts = Object.fromEntries(priorityDefinitions.value.map(item => [item.key, 0]))
  allTasks.value.forEach(task => {
    const definition = priorityDefinitions.value.find(item => item.value === normalizePriorityValue(task.priority)) || priorityDefinitions.value[4]
    counts[definition.key] += 1
  })

  const total = allTasks.value.length
  let currentOffset = 0

  return priorityDefinitions.value.map(definition => {
    const count = counts[definition.key] || 0
    const percent = total > 0 ? (count / total) * 100 : 0
    const offset = currentOffset
    currentOffset -= percent
    return {
      label: definition.chartLabel,
      displayLabel: definition.label,
      count,
      percent,
      color: definition.color,
      bgColor: definition.bgColor,
      offset
    }
  })
})

const getStatusLabel = (statusName) => {
  if (!statusName) return t('reports.statusNotCreated')
  return getStatusDefinition(statusName).label
}

const getStatusColor = (statusName) => {
  return getStatusDefinition(statusName).color
}

const getStatusBgColor = (statusName) => {
  return getStatusDefinition(statusName).bgColor
}

const getAssigneeNames = (task) => {
  if (!task.assignees || task.assignees.length === 0) return t('reports.unassigned')
  return task.assignees.map(a => a.fullName || a.name || t('reports.anonymous')).join(', ')
}

const formatDate = (value) => {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  const day = `${date.getDate()}`.padStart(2, '0')
  const month = `${date.getMonth() + 1}`.padStart(2, '0')
  const year = date.getFullYear()
  return `${day}/${month}/${year}`
}

const navigateToTask = (taskId) => {
  router.push({
    path: buildSpacePath(projectId.value, 'work-items'),
    query: { task: taskId }
  })
}

const fetchData = async () => {
  loading.value = true
  error.value = null
  try {
    await Promise.all([
      workTaskStore.fetchTasks(projectId.value),
      sprintStore.fetchSprints(projectId.value)
    ])
  } catch (e) {
    error.value = t('reports.loadError')
    console.error(e)
  } finally {
    loading.value = false
  }
}

// Suggested Tasks & Workload Computations
const doneStatusesList = ['done', 'completed', 'finished', 'hoàn thành', 'success', 'hoàn tất']
const cancelStatusesList = ['cancel', 'cancelled', 'hủy', 'hủy bỏ']

const scoredTasks = computed(() => {
  const incompleteTasks = allTasks.value.filter(t => {
    const status = (t.statusName || '').toLowerCase().trim()
    return !doneStatusesList.includes(status) && !cancelStatusesList.includes(status)
  })

  return incompleteTasks.map(task => {
    let score = 0;
    
    // 1. Deadline (max 35)
    const dueDateStr = task.dueDate || task.deadline || task.endDate || task.DueDate;
    if (dueDateStr) {
      const due = new Date(dueDateStr);
      due.setHours(0,0,0,0);
      const today = new Date();
      today.setHours(0,0,0,0);
      const diffTime = due - today;
      const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
      
      if (diffDays < 0) score += 35; 
      else if (diffDays === 0) score += 32;
      else if (diffDays === 1) score += 28;
      else if (diffDays <= 3) score += 20;
      else if (diffDays <= 7) score += 10;
    }
    
    // 2. Priority (max 25)
    const prio = Number(task.priority || task.Priority);
    if (prio === 1) score += 25;
    else if (prio === 2) score += 20;
    else if (prio === 3) score += 12;
    else if (prio === 4) score += 5;
    
    // 3. Status (max 10)
    const status = (task.statusName || '').toLowerCase().trim();
    if (status.includes('todo') || status.includes('to do') || status.includes('backlog') || status === 'new') score += 10;
    else if (status.includes('progress') || status.includes('doing')) score += 8;
    else if (status.includes('review') || status.includes('test')) score += 5;
    
    // 4. Progress (max 15)
    const progress = Number(task.progress || task.Progress || 0);
    if (progress >= 90) score += 15;
    else if (progress >= 80) score += 12;
    else if (progress >= 70) score += 10;
    else if (progress >= 50) score += 8;
    else if (progress === 0) score += 3;
    
    // 5. Dependency (max 10)
    if (task.linkedTasks && task.linkedTasks.length > 0) {
      const hasBlocked = task.linkedTasks.some(l => l.linkType === 'blocks' || l.linkType === 'blocking');
      if (hasBlocked) score += 10;
    }
    
    return { ...task, score };
  }).sort((a, b) => b.score - a.score);
})

const suggestedTasks = computed(() => scoredTasks.value.slice(0, 4))
const continueTasks = computed(() => scoredTasks.value.slice(4, 12))


const openPriorityGroups = ref({
  urgent: true,
  high: true,
  medium: true,
  low: true,
  none: true
})

const togglePriorityGroup = (key) => {
  openPriorityGroups.value[key] = !openPriorityGroups.value[key]
}

const tasksByPriority = computed(() => {
  const groups = {
    urgent: [],
    high: [],
    normal: [],
    low: [],
    none: []
  }

  allTasks.value.forEach(task => {
    const val = normalizePriorityValue(task.priority)
    if (val === 1) {
      groups.urgent.push(task)
    } else if (val === 2) {
      groups.high.push(task)
    } else if (val === 3) {
      groups.normal.push(task)
    } else if (val === 4) {
      groups.low.push(task)
    } else {
      groups.none.push(task)
    }
  })

  return groups
})

const priorityGroupList = computed(() => priorityDefinitions.value.map(definition => ({
  key: definition.key,
  label: definition.label,
  icon: definition.icon,
  color: definition.color,
  bgColor: definition.bgColor,
  tasks: tasksByPriority.value[definition.key] || []
})))

const openStatusGroups = ref({})

const toggleStatusGroup = (name) => {
  if (openStatusGroups.value[name] === undefined) {
    openStatusGroups.value[name] = false
  } else {
    openStatusGroups.value[name] = !openStatusGroups.value[name]
  }
}

const isStatusGroupOpen = (name) => {
  return openStatusGroups.value[name] !== false
}

const statusGroupList = computed(() => {
  const groups = {}
  
  // Initialize with empty arrays for status list
  statusDistribution.value.forEach(st => {
    groups[st.name] = []
  })

  allTasks.value.forEach(task => {
    const s = normalizeStatusKey(task.statusName)
    if (!groups[s]) {
      groups[s] = []
    }
    groups[s].push(task)
  })

  return statusDistribution.value.map(st => {
    const name = st.name
    const label = getStatusLabel(name)
    const color = getStatusColor(name)
    const bgColor = getStatusBgColor(name)
    const icon = getStatusIcon(name)
    
    return {
      name,
      label,
      icon,
      color,
      bgColor,
      tasks: groups[name] || []
    }
  })
})

const teamWorkload = computed(() => {
  const membersMap = {}
  allTasks.value.forEach(task => {
    const taskAssignees = task.assignees || []
    if (taskAssignees.length === 0) {
      if (!membersMap['unassigned']) {
        membersMap['unassigned'] = {
          userId: 'unassigned',
          fullName: t('reports.unassigned'),
          avatar: null,
          count: 0
        }
      }
      membersMap['unassigned'].count++
    } else {
      taskAssignees.forEach(assignee => {
        const uid = assignee.userId || assignee.id
        if (!uid) return
        if (!membersMap[uid]) {
          const name = assignee.fullName || assignee.name || t('reports.anonymous')
          membersMap[uid] = {
            userId: uid,
            fullName: name,
            avatarColor: assignee.avatarColor || assignee.AvatarColor,
            avatarUrl: assignee.avatarUrl || assignee.AvatarUrl,
            count: 0
          }
        }
        membersMap[uid].count++
      })
    }
  })

  const list = Object.values(membersMap)
  if (list.length === 0) return []

  const totalTasks = allTasks.value.length || 1
  return list
    .map(item => ({
      ...item,
      percentage: Math.round((item.count / totalTasks) * 100)
    }))
    .sort((a, b) => b.count - a.count)
})

const normalizePriorityValue = (p) => {
  if (p === null || p === undefined) return 0
  if (typeof p === 'number') return p
  const str = String(p).toLowerCase().trim()
  if (str === '1' || str === 'urgent' || str === 'critical') return 1
  if (str === '2' || str === 'high') return 2
  if (str === '3' || str === 'medium' || str === 'normal') return 3
  if (str === '4' || str === 'low') return 4
  return 0
}

const getPriorityIcon = (priority) => {
  const p = normalizePriorityValue(priority)
  if (p === 1) return 'fa-solid fa-angles-up'
  if (p === 2) return 'fa-solid fa-chevron-up'
  if (p === 3) return 'fa-solid fa-minus'
  if (p === 4) return 'fa-solid fa-chevron-down'
  return 'fa-solid fa-ban'
}

const getPriorityLabel = (priority) => {
  const p = normalizePriorityValue(priority)
  switch (p) {
    case 1: return t('workItems.priority.urgent', 'Urgent')
    case 2: return t('workItems.priority.high', 'High')
    case 3: return t('workItems.priority.normal', 'Normal')
    case 4: return t('workItems.priority.low', 'Low')
    default: return t('workItems.priority.none', 'None')
  }
}

const getPriorityClass = (priority) => {
  const p = normalizePriorityValue(priority)
  switch (p) {
    case 1: return 'priority-urgent'
    case 2: return 'priority-high'
    case 3: return 'priority-medium'
    case 4: return 'priority-low'
    default: return 'priority-none'
  }
}

const getStatusIcon = (statusName = '') => {
  return getStatusDefinition(statusName).icon
}

const normalizeStatusLabel = (statusName = '') => {
  if (!statusName) return t('reports.noStatus')
  return getStatusLabel(statusName)
}

const getStatusClass = (statusName = '') => {
  const status = normalizeStatusKey(statusName).toLowerCase()
  if (status.includes('done') || status.includes('complete')) return 'status-done'
  if (status.includes('progress')) return 'status-progress'
  if (status.includes('review')) return 'status-review'
  if (status.includes('block')) return 'status-blocked'
  if (status.includes('backlog')) return 'status-backlog'
  if (status.includes('todo') || status.includes('to do')) return 'status-todo'
  if (status.includes('cancel')) return 'status-cancelled'
  return 'status-default'
}

const calcDaysLeft = (task) => {
  const dateStr = task?.dueDate || task?.deadline || task?.plannedEndDate || task?.endDate || task?.DueDate || task?.Deadline
  if (!dateStr) return null
  const due = new Date(dateStr)
  if (isNaN(due.getTime())) return null
  due.setHours(23, 59, 59, 999)
  const today = new Date()
  return Math.ceil((due - today) / (1000 * 60 * 60 * 24))
}

const getDeadlineText = (task) => {
  const d = calcDaysLeft(task)
  if (d === null) return t('reports.noDueDate')
  if (d < 0) return `${Math.abs(d)}d overdue`
  if (d === 0) return 'Today'
  if (d === 1) return 'Tomorrow'
  return `${d} days left`
}

const getDeadlineClass = (task) => {
  const d = calcDaysLeft(task)
  if (d === null) return 'deadline-none'
  if (d < 0) return 'deadline-overdue'
  if (d <= 1) return 'deadline-urgent'
  if (d <= 3) return 'deadline-warning'
  return 'deadline-ok'
}

onMounted(() => {
  fetchData()
})
</script>

<style scoped>
/* Page Layout Wrapper */
.space-reports-page {
  width: 100%;
  max-width: none;
  margin: 0;
  padding: 0;
  min-height: 100%;
  color: var(--color-text-primary);
  display: flex;
  flex-direction: column;
  gap: 28px;
  font-family: 'Inter', system-ui, sans-serif;
  background: var(--color-bg);
}

.space-reports-page :deep(.project-page-inner) {
  padding-top: 14px;
  scroll-padding-top: 14px;
}

.reports-error {
  color: var(--color-danger);
  background: var(--color-danger-bg);
  border: 1px solid rgba(239, 68, 68, 0.2);
  border-radius: 12px;
  padding: 24px;
  text-align: center;
}

/* Content Area */
.reports-content {
  display: grid;
  grid-template-columns: repeat(12, minmax(0, 1fr));
  gap: 24px;
}

.reports-content > .health-alert-card,
.reports-content > .reports-stats-grid { grid-column: 1 / -1; }
.reports-content > .attention-panel,
.reports-content > .workload-panel { grid-column: span 7; }
.reports-content > .report-card { grid-column: span 5; }

.reports-content > .bottom-distributions-grid {
  grid-column: 1 / -1;
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 24px;
  width: 100%;
}

.bottom-distributions-grid > .report-card {
  width: 100%;
  min-width: 0;
}

.distribution-tabs-panel {
  gap: 18px;
}

.distribution-tabs-header {
  display: inline-flex;
  align-items: center;
  width: fit-content;
  max-width: 100%;
  padding: 4px;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: color-mix(in srgb, var(--color-bg) 72%, transparent);
}

.distribution-tab-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 34px;
  padding: 0 14px;
  border: 0;
  border-radius: 7px;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 13px;
  font-weight: 750;
  cursor: pointer;
  transition: background 160ms ease, color 160ms ease, box-shadow 160ms ease;
}

.distribution-tab-btn.active {
  background: var(--color-surface);
  color: var(--color-text-primary);
  box-shadow: 0 1px 3px rgba(15, 23, 42, 0.08);
}

.distribution-focus-grid {
  display: grid;
  grid-template-columns: minmax(280px, 340px) minmax(0, 1fr);
  gap: 24px;
  align-items: start;
}

.distribution-left-side {
  display: flex;
  flex-direction: column;
  gap: 16px;
  background: color-mix(in srgb, var(--color-bg) 50%, transparent);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 18px;
}

.distribution-right-side {
  display: flex;
  flex-direction: column;
  width: 100%;
  min-width: 0;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface);
  overflow: hidden;
}

.distribution-count-pill {
  display: inline-flex;
  align-items: center;
  min-height: 20px;
  padding: 0 8px;
  border-radius: 6px;
  font-size: 11px;
  font-weight: 800;
  white-space: nowrap;
}

@media (max-width: 900px) {
  .distribution-focus-grid {
    grid-template-columns: 1fr;
  }

  .distribution-tabs-header {
    width: 100%;
  }

  .distribution-tab-btn {
    flex: 1;
  }
}

/* Project Health Alert Card styling */
/* Top Distributions Row (Equal height panels) */
.top-distributions-row {
  grid-column: 1 / -1;
  display: grid;
  grid-template-columns: minmax(0, 1.2fr) minmax(0, 0.8fr);
  gap: 24px;
  align-items: stretch;
}

@media (max-width: 1180px) {
  .top-distributions-row {
    grid-template-columns: 1fr;
  }
}

.report-card.suggested-panel,
.report-card.team-workload-card {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.dashboard-right-panel .team-workload-card {
  flex: 1;
}

.workload-right-meta {
  display: flex;
  align-items: center;
  gap: 12px;
}

.health-left {
  display: flex;
  align-items: center;
  gap: 16px;
  flex: 1;
  min-width: 280px;
}

.health-right {
  display: flex;
  align-items: center;
  gap: 14px;
  flex-shrink: 0;
}

.health-alert-card {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 16px;
  padding: 20px 24px;
  border-radius: 16px;
  box-shadow: 0 10px 30px rgba(15, 23, 42, 0.05);
  border-width: 1px;
  border-style: solid;
}

.health-alert-card.green {
  background: linear-gradient(135deg, rgba(16, 185, 129, 0.08), rgba(52, 211, 153, 0.03));
  border-color: rgba(16, 185, 129, 0.28);
  box-shadow: 0 12px 30px rgba(16, 185, 129, 0.04);
}
.health-alert-card.green .health-icon {
  background: rgba(16, 185, 129, 0.12);
  color: #10b981;
}

.health-alert-card.yellow {
  background: linear-gradient(135deg, rgba(245, 158, 11, 0.08), rgba(251, 191, 36, 0.03));
  border-color: rgba(245, 158, 11, 0.28);
  box-shadow: 0 12px 30px rgba(245, 158, 11, 0.04);
}
.health-alert-card.yellow .health-icon {
  background: rgba(245, 158, 11, 0.12);
  color: #f59e0b;
}

.health-alert-card.red {
  background: linear-gradient(135deg, rgba(239, 68, 68, 0.08), rgba(248, 113, 113, 0.03));
  border-color: rgba(239, 68, 68, 0.28);
  box-shadow: 0 12px 30px rgba(239, 68, 68, 0.04);
}
.health-alert-card.red .health-icon {
  background: rgba(239, 68, 68, 0.12);
  color: #ef4444;
}

.health-icon {
  width: 44px;
  height: 44px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  flex-shrink: 0;
}

.health-status-title {
  margin: 0;
  font-size: 16px;
  font-weight: 750;
  color: var(--color-text-primary);
}

.health-desc {
  margin: 6px 0 0 0;
  font-size: 13.5px;
  color: var(--color-text-secondary);
  line-height: 1.5;
}

/* Stats Cards Grid */
.reports-stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  width: 100%;
}

@media (max-width: 1024px) {
  .reports-stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 640px) {
  .reports-stats-grid {
    grid-template-columns: 1fr;
  }
}

.report-stat-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 16px !important;
  padding: 20px;
  position: relative;
  overflow: hidden;
  box-shadow: var(--shadow-sm);
}

.report-stat-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);
}



.stat-card-content {
  display: flex;
  align-items: center;
  gap: 18px;
  position: relative;
  z-index: 1;
}

.stat-icon-wrapper {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
}

.total-tasks .stat-icon-wrapper {
  background: rgba(56, 189, 248, 0.08);
  color: var(--color-accent);
}
.total-tasks:hover { border-color: var(--color-accent); }

.done-tasks .stat-icon-wrapper {
  background: rgba(16, 185, 129, 0.08);
  color: #10b981;
}
.done-tasks:hover { border-color: #10b981; }

.in-progress .stat-icon-wrapper {
  background: rgba(245, 158, 11, 0.08);
  color: #f59e0b;
}
.in-progress:hover { border-color: #f59e0b; }

.overdue-tasks .stat-icon-wrapper {
  background: rgba(148, 163, 184, 0.08);
  color: var(--color-text-muted);
}
.overdue-tasks.has-overdue .stat-icon-wrapper {
  background: rgba(239, 68, 68, 0.08);
  color: #ef4444;
}
.overdue-tasks.has-overdue:hover { border-color: #ef4444; }

.report-stat-card .label {
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--color-text-muted);
}

.report-stat-card .value {
  font-size: 32px;
  font-weight: 850;
  line-height: 1.1;
  color: var(--color-text-primary);
  margin-top: 4px;
  display: flex;
  align-items: baseline;
  gap: 8px;
}

.percentage-tag {
  font-size: 13px;
  font-weight: 700;
  color: #10b981;
  background: rgba(16, 185, 129, 0.08);
  padding: 2px 6px;
  border-radius: 6px;
}

/* Card Design Pattern */
.report-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 14px !important;
  padding: 18px;
  box-shadow: var(--shadow-sm);
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.report-card:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

.card-title {
  font-size: 17px;
  font-weight: 750;
  color: var(--color-text-primary);
  margin-bottom: 20px;
  display: flex;
  align-items: center;
  gap: 10px;
  line-height: 1.3;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--color-border);
}

/* Grid Layouts for cards */
.distributions-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: 24px;
  align-items: stretch;
  grid-column: 1 / -1;
}

.dashboard-left-panel,
.dashboard-right-panel {
  display: flex;
  flex-direction: column;
  gap: 24px;
  min-width: 0;
  height: 100%;
}

@media (max-width: 1180px) {
  .distributions-grid {
    grid-template-columns: 1fr;
  }
}

/* Accordion list styles */
.attention-accordions {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.accordion-item {
  border: 1px solid var(--color-border);
  border-radius: 12px;
  overflow: hidden;
  background: var(--color-surface);
  transition: border-color 0.2s ease;
}

.accordion-item:hover {
  border-color: #cbd5e1;
}

.accordion-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 18px;
  cursor: pointer;
  background: rgba(0, 0, 0, 0.015);
  user-select: none;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.header-text {
  font-size: 13.5px;
  font-weight: 700;
  color: var(--color-text-primary);
  overflow-wrap: anywhere;
}

.badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 22px;
  height: 22px;
  border-radius: 50%;
  font-size: 11px;
  font-weight: 800;
  padding: 0 6px;
}

.danger-bg { background: rgba(239, 68, 68, 0.08); color: #ef4444; }
.warning-bg { background: rgba(245, 158, 11, 0.08); color: #f59e0b; }
.info-bg { background: rgba(14, 165, 233, 0.08); color: #0ea5e9; }
.gray-bg { background: rgba(100, 116, 139, 0.08); color: #64748b; }

.accordion-content {
  border-top: 1px solid var(--color-border);
  padding: 12px;
  background: var(--color-surface);
}

.empty-substate {
  padding: 16px;
  text-align: center;
  color: var(--color-text-muted);
  font-size: 12.5px;
}

.attention-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.attention-task-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 14px;
  background: rgba(0, 0, 0, 0.01);
  border-radius: 8px;
  border: 1px solid transparent;
  transition: all 0.2s ease;
}

.attention-task-row:hover {
  background: var(--color-surface-hover);
  border-color: var(--color-border);
}

.task-info {
  display: flex;
  flex-direction: column;
  gap: 4px;
  flex: 1;
  cursor: pointer;
  overflow: hidden;
  margin-right: 12px;
}

.task-key {
  font-size: 10px;
  font-family: monospace;
  font-weight: 700;
  color: var(--color-text-muted);
}

.task-title {
  font-size: 13px;
  font-weight: 650;
  color: var(--color-text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.task-info:hover .task-title {
  color: var(--color-accent);
  text-decoration: underline;
}

.task-assignee,
.task-due-date,
.task-status {
  font-size: 11px;
  color: var(--color-text-muted);
}

.remind-btn {
  background: var(--color-accent);
  color: white;
  border: none;
  border-radius: 6px;
  padding: 6px 10px;
  font-size: 11.5px;
  font-weight: 700;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 4px;
  box-shadow: 0 4px 10px rgba(14, 165, 233, 0.2);
  transition: all 0.2s ease;
}

.remind-btn:hover {
  background: var(--color-accent-hover, #0284c7);
  transform: translateY(-1px);
}

.remind-btn:disabled {
  background: var(--color-text-muted, #94a3b8) !important;
  color: rgba(255, 255, 255, 0.6) !important;
  cursor: not-allowed !important;
  box-shadow: none !important;
  transform: none !important;
  opacity: 0.6 !important;
}

.plain-row {
  cursor: pointer;
}

.unassigned-badge {
  font-size: 11px;
  color: #64748b;
  background: #f1f5f9;
  padding: 4px 8px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  gap: 4px;
}

/* Remind Members card workload design */
.remind-member-card {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px;
  border-radius: 12px;
  background: rgba(0, 0, 0, 0.01);
  border: 1px solid var(--color-border);
  transition: all 0.2s ease;
}

.remind-member-card:hover {
  background: var(--color-surface-hover);
  transform: translateX(2px);
}

.remind-member-card.has-overdue {
  border-color: rgba(239, 68, 68, 0.15);
  background: rgba(239, 68, 68, 0.015);
}
.remind-member-card.has-overdue:hover {
  border-color: rgba(239, 68, 68, 0.3);
  background: rgba(239, 68, 68, 0.03);
}

.member-card-left {
  display: flex;
  align-items: flex-start;
  gap: 14px;
  flex: 1;
  min-width: 0;
}

.member-details {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}

.member-name {
  font-size: 14px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.member-stats {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.member-stats .badge {
  border-radius: 6px;
  padding: 2px 8px;
  height: auto;
  font-size: 11px;
}

.deadline-tip {
  margin: 4px 0 0 0;
  font-size: 11.5px;
  color: var(--color-text-muted);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 320px;
}

.hover-task-link {
  color: var(--color-text-secondary);
  cursor: pointer;
}
.hover-task-link:hover {
  color: var(--color-accent);
  text-decoration: underline;
}

/* Status Distribution styles */
.status-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.status-item {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 8px;
  border-radius: 8px;
  transition: background-color 0.2s;
}

.status-item:hover {
  background-color: var(--color-surface-hover);
}

.status-item-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.02em;
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
}

.status-count {
  font-size: 13px;
  color: var(--color-text-primary);
}

.percentage-label {
  color: var(--color-text-muted);
  font-size: 11px;
  font-weight: 500;
}

.status-progress-track {
  width: 100%;
  background: var(--color-border);
  height: 6px;
  border-radius: 999px;
  overflow: hidden;
}

.status-progress-bar {
  height: 100%;
  border-radius: 999px;
  transition: width 0.6s cubic-bezier(0.4, 0, 0.2, 1);
}

/* Donut Chart and Legend styles */
.priority-chart-container {
  display: flex;
  align-items: center;
  gap: 32px;
  justify-content: center;
  flex-wrap: wrap;
}

.donut-chart-wrapper {
  position: relative;
  width: 150px;
  height: 150px;
  flex-shrink: 0;
}

.donut-chart {
  width: 100%;
  height: 100%;
  transform: rotate(-90deg);
}

.donut-segment {
  transition: stroke-dasharray 0.5s ease, stroke-dashoffset 0.5s ease;
}

.donut-center {
  position: absolute;
  inset: 0;
  margin: auto;
  width: 86px;
  height: 86px;
  background: var(--color-surface);
  border-radius: 50%;
  box-shadow: inset 0 2px 4px rgba(0, 0, 0, 0.05);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--color-border);
}

.donut-number {
  font-size: 26px;
  font-weight: 800;
  color: var(--color-text-primary);
  line-height: 1;
}

.donut-label {
  font-size: 9px;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  color: var(--color-text-muted);
  margin-top: 4px;
  font-weight: 700;
}

.priority-legend {
  display: flex;
  flex-direction: column;
  gap: 8px;
  flex-grow: 1;
  min-width: 180px;
}

.legend-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 6px 12px;
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.01);
  transition: all 0.2s;
  border: 1px solid transparent;
}

.legend-item:hover {
  background: var(--color-surface-hover);
  border-color: var(--color-border);
}

.legend-info {
  display: flex;
  align-items: center;
  gap: 10px;
}

.legend-color-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.legend-label {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.legend-value {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}

.legend-count {
  font-weight: 750;
  color: var(--color-text-primary);
}

.legend-percent {
  font-weight: 500;
  color: var(--color-text-muted);
  font-size: 11px;
}

/* Animations and Dark Theme adjustments */
@keyframes reports-rise-in {
  from {
    opacity: 0;
    transform: translateY(16px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.space-reports-page {
  background: var(--color-bg) !important;
}

.health-alert-card,
.report-stat-card,
.report-card {
  animation: reports-rise-in 520ms cubic-bezier(0.2, 0.8, 0.2, 1) both;
  transition:
    transform 220ms cubic-bezier(0.2, 0.8, 0.2, 1),
    box-shadow 220ms ease,
    border-color 220ms ease !important;
}

.reports-stats-grid .report-stat-card:nth-child(1) { animation-delay: 70ms; }
.reports-stats-grid .report-stat-card:nth-child(2) { animation-delay: 120ms; }
.reports-stats-grid .report-stat-card:nth-child(3) { animation-delay: 170ms; }
.reports-stats-grid .report-stat-card:nth-child(4) { animation-delay: 220ms; }

[data-theme='dark'] .health-alert-card,
[data-theme='dark'] .report-stat-card,
[data-theme='dark'] .report-card {
  border-color: var(--color-border);
  background: var(--color-surface) !important;
  box-shadow: var(--shadow-sm);
}

[data-theme='light'] .health-alert-card,
[data-theme='light'] .report-stat-card,
[data-theme='light'] .report-card {
  background: var(--color-surface) !important;
  color: var(--color-text-primary) !important;
  border-color: var(--color-border) !important;
}

[data-theme='light'] .card-title,
[data-theme='light'] .report-stat-card .value,
[data-theme='light'] .status-count strong,
[data-theme='light'] .legend-count,
[data-theme='light'] .donut-number,
[data-theme='light'] .member-name {
  color: #0f172a !important;
}

[data-theme='dark'] .card-title,
[data-theme='dark'] .report-stat-card .value,
[data-theme='dark'] .status-count strong,
[data-theme='dark'] .legend-count,
[data-theme='dark'] .donut-number,
[data-theme='dark'] .member-name {
  color: #f8fafc !important;
}

@media (max-width: 900px) {
  .reports-content > .attention-panel,
  .reports-content > .workload-panel,
  .reports-content > .report-card { grid-column: 1 / -1; }
}

/* Task Row Styles */
.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  padding-bottom: 10px;
  border-bottom: 1px solid color-mix(in srgb, var(--color-border) 80%, transparent);
}

.panel-title {
  font-size: 15px;
  font-weight: 750;
  color: var(--color-text-primary);
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.panel-link {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-accent, #3b82f6);
  text-decoration: none;
  transition: color 0.15s;
}

.panel-link:hover {
  text-decoration: underline;
}



.task-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  flex: 1;
}

.task-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  min-height: 44px;
  padding: 0 12px;
  border: 1px solid color-mix(in srgb, var(--color-border) 70%, transparent);
  background: color-mix(in srgb, var(--color-bg) 42%, transparent);
  border-radius: 7px;
  transition: all 0.2s;
}

[data-theme='dark'] .task-row {
  background: rgba(255, 255, 255, 0.02);
}

.task-row:hover {
  border-color: var(--color-border-hover, var(--color-border));
  background: var(--color-surface-hover, rgba(0, 0, 0, 0.02));
}

.task-info-left {
  display: flex;
  align-items: center;
  gap: 10px;
  overflow: hidden;
  min-width: 0;
  flex: 1;
}

.task-seq-id {
  font-size: 11px;
  font-family: monospace;
  font-weight: 700;
  background: color-mix(in srgb, var(--color-surface) 88%, #020617);
  color: var(--color-text-muted);
  min-width: 62px;
  text-align: center;
  padding: 4px 6px;
  border-radius: 4px;
  border: 1px solid var(--color-border);
  flex-shrink: 0;
}

.task-title-btn {
  background: none;
  border: none;
  text-align: left;
  min-width: 0;
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
  cursor: pointer;
  padding: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  transition: color 0.15s;
}

.task-title-btn:hover {
  color: var(--color-accent, #3b82f6);
  text-decoration: underline;
}

.task-meta-right {
  display: grid;
  grid-template-columns: 110px 85px 90px;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.priority-badge,
.task-status-tag {
  justify-self: start;
  width: fit-content;
  max-width: 100%;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 5px;
  min-height: 24px;
  height: 24px;
  border: 1px solid transparent;
  border-radius: 999px;
  padding: 0 8px;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.02em;
  line-height: 1;
  text-transform: uppercase;
  white-space: nowrap;
  box-sizing: border-box;
}

.priority-badge i,
.task-status-tag i,
.task-deadline-tag i {
  font-size: 11px;
}

.priority-urgent { background: rgba(239, 68, 68, 0.12); border-color: rgba(239, 68, 68, 0.35); color: #dc2626; }
.priority-high { background: rgba(249, 115, 22, 0.12); border-color: rgba(249, 115, 22, 0.35); color: #ea580c; }
.priority-medium { background: rgba(14, 165, 233, 0.12); border-color: rgba(14, 165, 233, 0.35); color: #0284c7; }
.priority-low { background: rgba(16, 185, 129, 0.12); border-color: rgba(16, 185, 129, 0.35); color: #059669; }
.priority-none { background: rgba(100, 116, 139, 0.08); border-color: rgba(100, 116, 139, 0.20); color: #64748b; }

[data-theme='dark'] .priority-urgent { background: rgba(239, 68, 68, 0.2); border-color: rgba(239, 68, 68, 0.45); color: #f87171; }
[data-theme='dark'] .priority-high { background: rgba(249, 115, 22, 0.2); border-color: rgba(249, 115, 22, 0.45); color: #fb923c; }
[data-theme='dark'] .priority-medium { background: rgba(56, 189, 248, 0.2); border-color: rgba(56, 189, 248, 0.45); color: #38bdf8; }
[data-theme='dark'] .priority-low { background: rgba(16, 185, 129, 0.2); border-color: rgba(16, 185, 129, 0.45); color: #34d399; }
[data-theme='dark'] .priority-none { background: rgba(148, 163, 184, 0.12); border-color: rgba(148, 163, 184, 0.25); color: #94a3b8; }

.status-blocked { background: rgba(239, 68, 68, 0.12); border-color: rgba(239, 68, 68, 0.35); color: #dc2626; }
.status-progress { background: rgba(14, 165, 233, 0.12); border-color: rgba(14, 165, 233, 0.35); color: #0284c7; }
.status-review { background: rgba(245, 158, 11, 0.12); border-color: rgba(245, 158, 11, 0.35); color: #d97706; }
.status-done { background: rgba(22, 163, 74, 0.12); border-color: rgba(22, 163, 74, 0.35); color: #16a34a; }
.status-backlog { background: rgba(100, 116, 139, 0.12); border-color: rgba(100, 116, 139, 0.30); color: #475569; }
.status-todo { background: rgba(124, 58, 237, 0.12); border-color: rgba(124, 58, 237, 0.35); color: #7c3aed; }
.status-cancelled { background: rgba(244, 63, 94, 0.12); border-color: rgba(244, 63, 94, 0.35); color: #e11d48; }
.status-default { background: rgba(100, 116, 139, 0.10); border-color: rgba(100, 116, 139, 0.22); color: #64748b; }

[data-theme='dark'] .status-blocked { background: rgba(239, 68, 68, 0.2); border-color: rgba(239, 68, 68, 0.45); color: #f87171; }
[data-theme='dark'] .status-progress { background: rgba(56, 189, 248, 0.2); border-color: rgba(56, 189, 248, 0.45); color: #38bdf8; }
[data-theme='dark'] .status-review { background: rgba(245, 158, 11, 0.2); border-color: rgba(245, 158, 11, 0.45); color: #fbbf24; }
[data-theme='dark'] .status-done { background: rgba(34, 197, 94, 0.2); border-color: rgba(34, 197, 94, 0.45); color: #34d399; }
[data-theme='dark'] .status-backlog { background: rgba(148, 163, 184, 0.2); border-color: rgba(148, 163, 184, 0.35); color: #cbd5e1; }
[data-theme='dark'] .status-todo { background: rgba(167, 139, 250, 0.2); border-color: rgba(167, 139, 250, 0.45); color: #c4b5fd; }
[data-theme='dark'] .status-cancelled { background: rgba(244, 63, 94, 0.2); border-color: rgba(244, 63, 94, 0.45); color: #fb7185; }
[data-theme='dark'] .status-default { background: rgba(148, 163, 184, 0.12); border-color: rgba(148, 163, 184, 0.25); color: #94a3b8; }

.task-deadline-tag {
  justify-self: start;
  width: fit-content;
  min-width: 86px;
  max-width: 100%;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  min-height: 24px;
  height: 24px;
  border: 1px solid transparent;
  border-radius: 6px;
  padding: 0 8px;
  font-size: 10.5px;
  font-weight: 600;
  white-space: nowrap;
  box-sizing: border-box;
  text-transform: none;
}

.deadline-overdue { color: #dc2626; background: rgba(239, 68, 68, 0.1); border: 1px solid rgba(239, 68, 68, 0.3); }
.deadline-urgent { color: #d97706; background: rgba(245, 158, 11, 0.1); border: 1px solid rgba(245, 158, 11, 0.3); }
.deadline-warning { color: #b45309; background: rgba(245, 158, 11, 0.08); border: 1px solid rgba(245, 158, 11, 0.25); }
.deadline-ok { color: #64748b; background: rgba(100, 116, 139, 0.08); border: 1px solid rgba(100, 116, 139, 0.2); }
.deadline-none { color: #94a3b8; background: rgba(148, 163, 184, 0.08); border: 1px dashed rgba(148, 163, 184, 0.3); }

[data-theme='dark'] .deadline-overdue { color: #f87171; background: rgba(239, 68, 68, 0.18); border-color: rgba(239, 68, 68, 0.4); }
[data-theme='dark'] .deadline-urgent { color: #fbbf24; background: rgba(245, 158, 11, 0.18); border-color: rgba(245, 158, 11, 0.4); }
[data-theme='dark'] .deadline-warning { color: #fcd34d; background: rgba(245, 158, 11, 0.12); border-color: rgba(245, 158, 11, 0.3); }
[data-theme='dark'] .deadline-ok { color: #94a3b8; background: rgba(148, 163, 184, 0.12); border-color: rgba(148, 163, 184, 0.25); }
[data-theme='dark'] .deadline-none { color: #64748b; background: rgba(148, 163, 184, 0.08); border-color: rgba(148, 163, 184, 0.2); }

.workload-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.workload-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.workload-item-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.workload-user {
  display: flex;
  align-items: center;
  gap: 8px;
}

.workload-user .user-name {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.workload-count {
  font-size: 12px;
  color: var(--color-text-muted);
}

.workload-progress-track {
  width: 100%;
  height: 6px;
  background: var(--color-border);
  border-radius: 999px;
  overflow: hidden;
}

.workload-progress-bar {
  height: 100%;
  background: linear-gradient(90deg, #10b981, #059669);
  border-radius: 999px;
  transition: width 0.4s ease;
}

.workload-progress-bar.is-unassigned {
  background: linear-gradient(90deg, #ef4444, #dc2626);
}

/* Active Cycle Card Header Widget */
.current-cycle-card {
  display: flex;
  align-items: center;
  gap: 12px;
  background: linear-gradient(135deg, color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)), color-mix(in srgb, var(--color-surface) 88%, transparent));
  border: 1px solid color-mix(in srgb, var(--color-accent) 28%, var(--color-border));
  padding: 6px 14px;
  border-radius: 12px;
  box-shadow: 0 4px 12px color-mix(in srgb, #020617 6%, transparent);
  transition: all 0.2s ease;
  cursor: pointer;
}

.current-cycle-card:hover {
  transform: translateY(-1px);
  box-shadow: 0 6px 16px rgba(14, 165, 233, 0.15);
}

.current-cycle-card.empty {
  border: 1px dashed color-mix(in srgb, var(--color-border) 78%, transparent);
  box-shadow: none;
  background: color-mix(in srgb, var(--color-surface) 78%, transparent);
}

.cycle-icon-wrapper {
  width: 30px;
  height: 30px;
  border-radius: 50%;
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface-hover));
  display: flex;
  align-items: center;
  justify-content: center;
  color: #64748b;
}

.cycle-icon-wrapper.active {
  background: rgba(14, 165, 233, 0.1);
  color: #0ea5e9;
}

.cycle-info h4 {
  font-size: 13px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0;
  display: flex;
  align-items: center;
  gap: 6px;
}

.cycle-info p {
  font-size: 11px;
  color: var(--color-text-muted);
  margin: 2px 0 0 0;
}

.active-badge {
  font-size: 9px;
  background: #10b981;
  color: white;
  padding: 2px 6px;
  border-radius: 10px;
  font-weight: 800;
}

/* Priority Distribution & Focus Tasks Panel */
.priority-focus-panel {
  grid-column: 1 / -1;
  margin-top: 12px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 16px;
  padding: 24px;
}

.priority-focus-grid {
  display: grid;
  grid-template-columns: minmax(300px, 360px) 1fr;
  gap: 28px;
  align-items: start;
}

@media (max-width: 900px) {
  .priority-focus-grid {
    grid-template-columns: 1fr;
  }
}

.priority-left-side {
  display: flex;
  flex-direction: column;
  gap: 16px;
  background: rgba(0, 0, 0, 0.015);
  border: 1px solid var(--color-border);
  border-radius: 14px;
  padding: 20px;
}

.priority-selector-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  width: 100%;
}

.priority-selector-btn {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 14px;
  border-radius: 10px;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-primary);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
}

.priority-selector-btn:hover {
  border-color: var(--color-accent);
  background: var(--color-surface-hover);
}

.priority-selector-btn.active {
  border-color: var(--color-accent);
  background: var(--color-surface-hover);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
}

.p-btn-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.p-btn-badge {
  font-size: 11px;
  font-weight: 700;
  padding: 3px 8px;
  border-radius: 6px;
}

.priority-right-side {
  display: flex;
  flex-direction: column;
  width: 100%;
  min-width: 0;
  border: 1px solid var(--color-border);
  border-radius: 14px;
  background: var(--color-surface);
  overflow: hidden;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.03);
}

.priority-accordions {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
  width: 100%;
}

.priority-group-accordion {
  width: 100%;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  overflow: hidden;
  background: var(--color-surface);
  transition: all 0.2s ease;
}

.priority-group-accordion.is-open {
  border-color: var(--color-border-hover, rgba(99, 102, 241, 0.3));
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.03);
}

.priority-group-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: rgba(0, 0, 0, 0.025);
  cursor: pointer;
  user-select: none;
  font-size: 13.5px;
  font-weight: 600;
  transition: background 0.15s ease;
}

.priority-group-header:hover {
  background: rgba(0, 0, 0, 0.05);
}

.priority-group-content {
  padding: 12px 16px;
  border-top: 1px solid var(--color-border);
  width: 100%;
}

.priority-group-content .task-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  width: 100%;
}

.priority-group-content .task-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 10px 14px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface);
  width: 100%;
  transition: all 0.15s ease;
}

.priority-group-content .task-row:hover {
  border-color: var(--color-accent);
  background: var(--color-surface-hover);
  transform: translateY(-1px);
}

.priority-group-content .task-info-left {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
  flex: 1;
}

.priority-group-content .task-seq-id {
  font-size: 11px;
  font-weight: 700;
  color: var(--color-text-muted);
  background: var(--color-bg-subtle, rgba(0,0,0,0.04));
  padding: 2px 6px;
  border-radius: 4px;
  flex-shrink: 0;
}

.priority-group-content .task-title-btn {
  font-size: 13.5px;
  font-weight: 600;
  color: var(--color-text-primary);
  background: none;
  border: none;
  cursor: pointer;
  text-align: left;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  padding: 0;
}

.priority-group-content .task-title-btn:hover {
  color: var(--color-accent);
}

.priority-group-content .task-meta-right {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-shrink: 0;
}
/* Old priority right side */
.old-unused {
  display: flex;
  flex-direction: column;
  border: 1px solid var(--color-border);
  border-radius: 14px;
  background: var(--color-surface);
  overflow: hidden;
}

.priority-header-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 18px;
  background: rgba(0, 0, 0, 0.02);
  border-bottom: 1px solid var(--color-border);
}

.p-bar-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.p-bar-count {
  font-size: 12px;
  font-weight: 500;
  color: var(--color-text-muted);
}

.priority-tasks-scroll {
  max-height: 420px;
  overflow-y: auto;
  padding: 8px 12px;
}

.priority-accordions {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 12px;
}

.priority-group-accordion {
  border: 1px solid var(--color-border);
  border-radius: 12px;
  overflow: hidden;
  background: var(--color-surface);
  transition: border-color 0.2s ease, box-shadow 0.2s ease;
}

.priority-group-accordion.is-open {
  border-color: rgba(99, 102, 241, 0.3);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.02);
}

.priority-group-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: rgba(0, 0, 0, 0.02);
  cursor: pointer;
  user-select: none;
  font-size: 13.5px;
  font-weight: 600;
  transition: background 0.15s ease;
}

.priority-group-header:hover {
  background: rgba(0, 0, 0, 0.04);
}

.p-header-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.p-group-title {
  color: var(--color-text-primary);
  font-size: 13.5px;
  font-weight: 600;
}

.priority-group-content {
  padding: 12px 16px;
  border-top: 1px solid var(--color-border);
}

</style>
