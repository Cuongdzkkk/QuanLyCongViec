<template>
  <ProjectPageContainer class="space-summary-page">
    <div v-if="isForbidden" class="forbidden-overlay">
      <div class="forbidden-content">
        <div class="lock-icon"><i class="fa-solid fa-lock"></i></div>
        <h2>{{ t('Access Denied') }}</h2>
        <p>{{ t('You do not have permission to access this project.') }}</p>
        <button class="plane-primary-btn mt-4" @click="router.push('/spaces')">{{ t('Back to Home') }}</button>
      </div>
    </div>
    <div v-else class="plane-board-container" style="display: flex; flex-direction: column; flex: 1; min-height: 0; height: 100%;">
      <ProjectPageHeader
        icon="fa-solid fa-layer-group"
        :title="activeModuleFilterId ? (moduleDetail?.name || tr('Module', 'Module')) : t('Work Items')"
        :description="activeModuleFilterId ? (moduleDetail?.description || tr('Scoped work items in this module', 'Công việc thuộc Module này')) : t('Manage tasks, bugs, and features')"
      >
        <template #actions>
          <div v-if="!activeModuleFilterId" class="toolbar-actions-wrapper">
            <el-button type="info" plain size="default" @click="showDataImportModal = true" :disabled="!canCurrentUserCreateTask" :title="!canCurrentUserCreateTask ? 'Bạn không có quyền nạp công việc' : ''">
              <i class="fa-solid fa-file-import mr-1"></i> Nạp dữ liệu công việc
            </el-button>
            <el-button type="info" plain size="default" @click="handleExportTasks">
              <i class="fa-solid fa-file-export mr-1"></i> Xuất Excel/CSV
            </el-button>
            <button 
              class="cyber-create-task-btn" 
              @click="openCreateTask('TO DO')" 
              :disabled="!canCurrentUserCreateTask" 
              :title="!canCurrentUserCreateTask ? 'Bạn không có quyền tạo công việc' : ''"
            >
              <span class="cyber-btn-content">
                <i class="fa-solid fa-plus"></i> {{ t('Add work item') }}
              </span>
            </button>
          </div>
          <TaskDataImportModal
            v-if="!activeModuleFilterId"
            v-model="showDataImportModal"
            :projectId="currentProjectId"
            :projectMembers="projectMembers"
            :projectStatuses="projectStatuses"
            @imported="fetchTasks"
          />
        </template>
      </ProjectPageHeader>
      <section v-if="activeModuleFilterId" class="module-detail-context" aria-live="polite">
        <div v-if="moduleDetailLoading" class="module-state-panel module-loading-state">
          <i class="fa-solid fa-spinner fa-spin" aria-hidden="true"></i>
          <div>
            <strong>{{ tr('Loading module', 'Đang tải Module') }}</strong>
            <span>{{ tr('Loading metadata and scoped work items...', 'Đang tải thông tin và danh sách công việc...') }}</span>
          </div>
        </div>
        <div v-else-if="moduleDetailError" class="module-state-panel module-error-state" role="alert">
          <i class="fa-solid fa-triangle-exclamation" aria-hidden="true"></i>
          <div>
            <strong>{{ tr('Unable to load module', 'Không thể tải Module') }}</strong>
            <span>{{ moduleDetailError.message }}</span>
          </div>
          <button type="button" class="module-retry-btn" @click="retryModuleDetail">
            <i class="fa-solid fa-rotate-right" aria-hidden="true"></i>
            {{ tr('Retry', 'Thử lại') }}
          </button>
        </div>
        <template v-else-if="moduleDetail">
          <div class="module-detail-heading">
            <div>
              <span class="module-status-label">{{ moduleDetail.status }}</span>
              <strong>{{ moduleDetail.name }}</strong>
            </div>
            <div class="module-progress" :aria-label="tr(`${moduleDetail.progressPercent}% complete`, `Hoàn thành ${moduleDetail.progressPercent}%`)">
              <span>{{ moduleDetail.progressPercent }}%</span>
              <div class="module-progress-track">
                <span :style="{ width: `${Math.min(100, Math.max(0, Number(moduleDetail.progressPercent) || 0))}%` }"></span>
              </div>
            </div>
          </div>
          <div class="module-summary-grid">
            <div class="module-summary-item">
              <span>{{ tr('Total', 'Tổng số') }}</span>
              <strong>{{ moduleDetail.taskCount }}</strong>
            </div>
            <div class="module-summary-item is-complete">
              <span>{{ tr('Completed', 'Hoàn thành') }}</span>
              <strong>{{ moduleDetail.completedCount }}</strong>
            </div>
            <div class="module-summary-item is-progress">
              <span>{{ tr('In progress', 'Đang thực hiện') }}</span>
              <strong>{{ moduleDetail.inProgressCount }}</strong>
            </div>
            <div class="module-summary-item is-overdue">
              <span>{{ tr('Overdue', 'Quá hạn') }}</span>
              <strong>{{ moduleDetail.overdueCount }}</strong>
            </div>
          </div>
        </template>
      </section>
      <ProjectPageToolbar
        v-if="!activeModuleFilterId"
        :showSearch="true"
        v-model:searchQuery="searchQuery"
        :searchPlaceholder="tr('Search work items...', 'Tìm kiếm công việc...')"
      >
        <template #left>
          <div class="view-toggles">
            <button class="toggle-btn" :class="{ active: currentTab === 'list' }" @click="currentTab = 'list'" :title="t('List view')"><i class="fa-solid fa-bars"></i></button>
            <button class="toggle-btn" :class="{ active: currentTab === 'board' }" @click="currentTab = 'board'" :title="t('Kanban view')"><i class="fa-solid fa-table-columns"></i></button>
            <button class="toggle-btn" :class="{ active: currentTab === 'calendar' }" @click="currentTab = 'calendar'" :title="t('Calendar view')"><i class="fa-regular fa-calendar"></i></button>
            <button class="toggle-btn" :class="{ active: currentTab === 'spreadsheet' }" @click="currentTab = 'spreadsheet'" :title="t('Spreadsheet view')"><i class="fa-solid fa-table-cells"></i></button>
            <button class="toggle-btn" :class="{ active: currentTab === 'timeline' }" @click="currentTab = 'timeline'" :title="t('Gantt chart view')"><i class="fa-solid fa-chart-gantt"></i></button>
          </div>
          <!-- Global Calendar Navigation controls -->
          <div v-if="currentTab === 'calendar' && calendarTabRef" class="cal-nav-toolbar" style="display: flex; align-items: center; gap: 8px; margin-left: 12px; border-left: 1px solid var(--color-border); padding-left: 12px;">
            <button class="plane-toolbar-btn icon-only-trigger" type="button" @click="calendarTabRef.prevMonth" :title="t('Previous month')"><i class="fa-solid fa-chevron-left"></i></button>
            <button class="plane-toolbar-btn icon-only-trigger" type="button" @click="calendarTabRef.nextMonth" :title="t('Next month')"><i class="fa-solid fa-chevron-right"></i></button>
            <span class="cal-month-label-global" style="font-size: 13px; font-weight: 700; color: var(--color-text-primary); margin: 0 4px; white-space: nowrap;">{{ calendarTabRef.monthLabel }}</span>
            <button class="plane-toolbar-btn" type="button" @click="calendarTabRef.goToday" style="font-size: 11px; padding: 4px 10px; min-height: 28px; line-height: 1;">{{ t('Today') }}</button>
          </div>
          <!-- Global Timeline Navigation & View Modes controls -->
          <div v-if="currentTab === 'timeline' && timelineTabRef" class="timeline-nav-toolbar" style="display: flex; align-items: center; gap: 8px; margin-left: 12px; border-left: 1px solid var(--color-border); padding-left: 12px;">
            <button class="plane-toolbar-btn icon-only-trigger" type="button" @click="timelineTabRef.shiftTimeline(-1)" :title="t('Previous')"><i class="fa-solid fa-chevron-left"></i></button>
            <button class="plane-toolbar-btn icon-only-trigger" type="button" @click="timelineTabRef.shiftTimeline(1)" :title="t('Next')"><i class="fa-solid fa-chevron-right"></i></button>
            
            <div class="tl-view-modes" style="display: flex; gap: 4px; margin-left: 4px; background: color-mix(in srgb, var(--color-surface-hover) 85%, transparent); border-radius: 8px; padding: 2px;">
              <button
                v-for="mode in timelineTabRef.viewModes"
                :key="mode.key"
                class="plane-toolbar-btn"
                style="font-size: 11px; padding: 3px 8px; border: none; background: transparent; height: auto; min-height: 22px; line-height: 1;"
                :style="timelineTabRef.viewMode === mode.key ? { background: 'var(--color-surface)', color: 'var(--color-accent)', boxShadow: '0 1px 3px rgba(0,0,0,0.1)' } : {}"
                @click="timelineTabRef.viewMode = mode.key"
              >{{ mode.key }}</button>
            </div>
            
            <button class="plane-toolbar-btn" type="button" @click="timelineTabRef.goToToday" style="font-size: 11px; padding: 4px 10px; min-height: 28px; line-height: 1; margin-left: 4px;">{{ t('Today') }}</button>
            <button class="plane-toolbar-btn" type="button" :style="timelineTabRef.createMode ? { background: 'color-mix(in srgb, var(--color-accent) 15%, var(--color-surface))', color: 'var(--color-accent)', borderColor: 'var(--color-accent)' } : {}" @click="timelineTabRef.toggleCreateMode" style="font-size: 11px; padding: 4px 10px; min-height: 28px; line-height: 1;">{{ t('Create mode') }}</button>
          </div>
        </template>
        <template #filters>
          <div class="filter-dropdown-wrapper js-toolbar-popup-scope">
            <button class="timeline-filter-trigger icon-only-trigger" type="button" aria-label="Filters" :title="tr('Filters', 'Bộ lọc')" @click.stop="toggleFilterDropdown" :class="{ active: showFilterDropdown || activeTaskFilters.length }">
            <i class="fa-solid fa-filter"></i>
            <span v-if="activeTaskFilters.length" class="filter-count">{{ activeTaskFilters.length }}</span>
          </button>
          <div class="plane-dropdown-menu filter-dropdown-menu" v-show="showFilterDropdown" @click.stop>
            <FilterBar
              v-model:filters="activeTaskFilters"
              :status-options="taskStatusOptions"
              :active="showFilterDropdown"
              @apply="applyTaskFilters"
              @remove="removeTaskFilter"
              @clear="clearTaskFilters"
            />
          </div>
        </div>
          <!-- Decoupled Sort Dropdown -->
          <div v-if="currentTab === 'list' || currentTab === 'board'" class="display-dropdown-wrapper js-toolbar-popup-scope" style="position: relative; display: inline-block;">
            <button class="timeline-filter-trigger icon-only-trigger" aria-label="Sort" :title="tr('Sort', 'Sắp xếp')" @click.stop="toggleSortDropdown" :class="{ 'active': showSortDropdown }">
              <i class="fa-solid fa-arrow-down-wide-short"></i>
            </button>
            <div class="plane-dropdown-menu" v-show="showSortDropdown" @click.stop style="width: 340px; left: 0; right: auto; display: flex; flex-direction: column; gap: 10px; padding: 8px; max-height: none; overflow: visible;">
              <!-- Sort Search Input -->
              <div class="filter-search-field">
                <i class="fa-solid fa-magnifying-glass filter-search-icon"></i>
                <input
                  v-model="sortSearchQuery"
                  type="text"
                  class="filter-search-input"
                  :placeholder="tr('Search sort fields...', 'Tìm kiếm trường sắp xếp...')"
                  @click.stop
                />
              </div>

              <!-- Sort By Combobox -->
              <div class="filter-combobox" style="position: relative;">
                <span class="filter-label">{{ tr('Sort by', 'Sắp xếp theo') }}</span>
                <div class="filter-select-trigger sort-combobox-trigger">
                  <div style="display: flex; align-items: center; gap: 10px; flex: 1; cursor: pointer; min-width: 0;" @click="openSortSelect = (openSortSelect === 'sort' ? null : 'sort')">
                    <i :class="displayOrderOptions.find(o => o.value === displayOrder)?.icon || 'fa-solid fa-hand'" style="font-size: 13px; color: var(--color-text-secondary); width: 15px; text-align: center;"></i>
                    <span style="font-size: 13px; color: var(--color-text-primary); text-align: left; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">{{ getDisplayOrderLabel(displayOrder) }}</span>
                  </div>
                  <!-- Asc/Desc buttons inside the trigger -->
                  <div style="display: flex; align-items: center; gap: 4px; margin-right: 8px;">
                    <button
                      type="button"
                      class="dir-mini-btn"
                      :class="{ active: sortDirection === 'asc' }"
                      @click="sortDirection = 'asc'"
                      title="Ascending"
                    >
                      <i class="fa-solid fa-arrow-up-wide-short" style="font-size: 11px;"></i>
                    </button>
                    <button
                      type="button"
                      class="dir-mini-btn"
                      :class="{ active: sortDirection === 'desc' }"
                      @click="sortDirection = 'desc'"
                      title="Descending"
                    >
                      <i class="fa-solid fa-arrow-down-short-wide" style="font-size: 11px;"></i>
                    </button>
                  </div>
                  <i class="fa-solid fa-chevron-down" style="font-size: 10px; transition: transform 0.2s; cursor: pointer;" :style="openSortSelect === 'sort' ? { transform: 'rotate(180deg)', color: 'var(--color-accent)' } : {}" @click="openSortSelect = (openSortSelect === 'sort' ? null : 'sort')"></i>
                </div>
                <div v-show="openSortSelect === 'sort'" class="filter-select-menu" style="position: absolute; top: calc(100% + 4px); left: 0; right: 0; max-height: 200px; z-index: 110;">
                  <button
                    v-for="opt in filteredDisplayOrderOptions"
                    :key="opt.value"
                    class="filter-select-option"
                    :class="{ selected: displayOrder === opt.value }"
                    type="button"
                    @click="displayOrder = opt.value"
                  >
                    <i :class="opt.icon"></i>
                    <span>{{ opt.label }}</span>
                    <!-- Ascending / Descending buttons on the right space of the selected option -->
                    <div v-if="displayOrder === opt.value" style="display: flex; align-items: center; gap: 4px;" @click.stop>
                      <button
                        type="button"
                        class="dir-mini-btn"
                        :class="{ active: sortDirection === 'asc' }"
                        @click="sortDirection = 'asc'"
                        title="Ascending"
                      >
                        <i class="fa-solid fa-arrow-up-wide-short"></i>
                      </button>
                      <button
                        type="button"
                        class="dir-mini-btn"
                        :class="{ active: sortDirection === 'desc' }"
                        @click="sortDirection = 'desc'"
                        title="Descending"
                      >
                        <i class="fa-solid fa-arrow-down-short-wide"></i>
                      </button>
                    </div>
                  </button>
                </div>
              </div>

              <!-- Group By Combobox -->
              <div class="filter-combobox" style="position: relative;">
                <span class="filter-label">{{ tr('Group by', 'Gom nhóm theo') }}</span>
                <button
                  class="filter-select-trigger"
                  type="button"
                  :class="{ active: openSortSelect === 'groupby' }"
                  @click="openSortSelect = (openSortSelect === 'groupby' ? null : 'groupby')"
                >
                  <i :class="{ status: 'fa-solid fa-square-poll-vertical', priority: 'fa-solid fa-signal', assignee: 'fa-regular fa-user', sprint: 'fa-solid fa-arrows-spin', module: 'fa-solid fa-cubes' }[groupBy] || 'fa-solid fa-layer-group'" aria-hidden="true"></i>
                  <span>{{ [
                    { value: 'status', label: tr('Status', 'Trạng thái') },
                    { value: 'priority', label: tr('Priority', 'Độ ưu tiên') },
                    { value: 'assignee', label: tr('Assignee', 'Người thực hiện') },
                    { value: 'sprint', label: tr('Sprint', 'Chu kỳ') },
                    { value: 'module', label: tr('Module', 'Phân hệ') }
                  ].find(g => g.value === groupBy)?.label }}</span>
                  <i class="fa-solid fa-chevron-down" style="font-size: 10px; margin-left: auto; transition: transform 0.2s;" :style="openSortSelect === 'groupby' ? { transform: 'rotate(180deg)', color: 'var(--color-accent)' } : {}"></i>
                </button>
                <div v-show="openSortSelect === 'groupby'" class="filter-select-menu" style="position: absolute; top: calc(100% + 4px); left: 0; right: 0; max-height: 200px; z-index: 110;">
                  <button
                    v-for="grp in [
                      { value: 'status', label: tr('Status', 'Trạng thái'), icon: 'fa-solid fa-square-poll-vertical' },
                      { value: 'priority', label: tr('Priority', 'Độ ưu tiên'), icon: 'fa-solid fa-signal' },
                      { value: 'assignee', label: tr('Assignee', 'Người thực hiện'), icon: 'fa-regular fa-user' },
                      { value: 'sprint', label: tr('Sprint', 'Chu kỳ'), icon: 'fa-solid fa-arrows-spin' },
                      { value: 'module', label: tr('Module', 'Phân hệ'), icon: 'fa-solid fa-cubes' }
                    ]"
                    :key="grp.value"
                    class="filter-select-option"
                    :class="{ selected: groupBy === grp.value }"
                    type="button"
                    @click="groupBy = grp.value; openSortSelect = null"
                  >
                    <i :class="grp.icon"></i>
                    <span>{{ grp.label }}</span>
                  </button>
                </div>
              </div>
            </div>
          </div>
          <!-- Updated Display Dropdown -->
          <div class="display-dropdown-wrapper js-toolbar-popup-scope" style="position: relative; display: inline-block;">
             <button class="timeline-filter-trigger icon-only-trigger" aria-label="Display" :title="t('Display')" @click.stop="toggleDisplayDropdown" :class="{ 'active': showDisplayDropdown }">
               <i class="fa-solid fa-eye"></i>
             </button>
             <div class="plane-dropdown-menu" v-show="showDisplayDropdown" @click.stop style="width: 320px; display: flex; flex-direction: column; gap: 12px; padding: 12px; right: 0; left: auto; max-height: none;">
                  <!-- List / Board view Display Options -->
                  <template v-if="currentTab === 'list' || currentTab === 'board'">
                     <!-- Display Properties -->
                     <div class="dd-section" style="padding: 0;">
                        <div class="dd-title filter-label" style="margin-bottom: 8px;">
                           <span>{{ t('Display Properties') }}</span>
                        </div>
                        <div class="dd-btns" style="display: flex; gap: 6px; flex-wrap: wrap;">
                           <button
                             v-for="property in displayPropertyOptions"
                             :key="property.key"
                             class="dd-tag"
                             type="button"
                             :class="{ active: displayProperties[property.key] }"
                             @click="toggleDisplayProperty(property.key)"
                             style="padding: 6px 12px; font-size: 13px; border-radius: 8px; font-weight: 500;"
                           >
                             <i :class="property.icon" style="font-size: 11px;"></i>
                             <span>{{ property.label }}</span>
                           </button>
                        </div>
                     </div>
                     <div class="dd-divider" style="height: 1px; background: var(--color-border); margin: 4px 0;"></div>
                     <!-- Show subtasks check -->
                     <div class="dd-section" style="padding: 0;">
                        <label 
                          class="dd-item checkbox" 
                          :style="showSubtasks ? { background: 'color-mix(in srgb, var(--color-accent) 10%, var(--color-surface))', color: 'var(--color-accent)', fontWeight: '600', borderColor: 'color-mix(in srgb, var(--color-accent) 55%, var(--color-border))' } : {}"
                          style="display: flex; align-items: center; gap: 8px; cursor: pointer; padding: 6px 10px; font-size: 13px; margin: 0; color: var(--color-text-secondary); border-radius: 8px; border: 1px solid transparent; transition: all 0.15s ease;"
                        >
                          <input type="checkbox" v-model="showSubtasks" style="width: 14px; height: 14px; accent-color: var(--color-accent); margin: 0;" /> 
                          <span>{{ t('Show sub-work items') }}</span>
                        </label>
                     </div>
                  </template>

                  <!-- Calendar Display Options -->
                  <template v-if="currentTab === 'calendar' && calendarTabRef">
                    <div class="dd-section" style="padding: 0;">
                      <div class="dd-title filter-label" style="margin-bottom: 8px;">
                        <span>{{ t('Calendar Display') }}</span>
                      </div>
                      <div style="display: flex; flex-direction: column; gap: 6px;">
                        <label class="dd-item checkbox" style="display: flex; align-items: center; gap: 8px; cursor: pointer; padding: 6px 10px; font-size: 13px; margin: 0; color: var(--color-text-secondary); border-radius: 8px; border: 1px solid transparent; transition: all 0.15s ease;">
                          <input type="checkbox" v-model="calendarTabRef.showOnlyDated" style="width: 14px; height: 14px; accent-color: var(--color-accent); margin: 0;" />
                          <span>{{ t('Show dated work items') }}</span>
                        </label>
                        <label class="dd-item checkbox" style="display: flex; align-items: center; gap: 8px; cursor: pointer; padding: 6px 10px; font-size: 13px; margin: 0; color: var(--color-text-secondary); border-radius: 8px; border: 1px solid transparent; transition: all 0.15s ease;">
                          <input type="checkbox" v-model="calendarTabRef.showDoneTasks" style="width: 14px; height: 14px; accent-color: var(--color-accent); margin: 0;" />
                          <span>{{ t('Show done work items') }}</span>
                        </label>
                        <label class="dd-item checkbox" style="display: flex; align-items: center; gap: 8px; cursor: pointer; padding: 6px 10px; font-size: 13px; margin: 0; color: var(--color-text-secondary); border-radius: 8px; border: 1px solid transparent; transition: all 0.15s ease;">
                          <input type="checkbox" v-model="calendarTabRef.highlightOverdue" style="width: 14px; height: 14px; accent-color: var(--color-accent); margin: 0;" />
                          <span>{{ t('Highlight overdue') }}</span>
                        </label>
                      </div>
                    </div>
                  </template>

                  <!-- Spreadsheet Display Options -->
                  <template v-if="currentTab === 'spreadsheet' && spreadsheetTabRef">
                    <div class="dd-section" style="padding: 0;">
                      <div class="dd-title filter-label" style="margin-bottom: 8px;">
                        <span>{{ t('Spreadsheet Display') }}</span>
                      </div>
                      <div style="display: flex; flex-direction: column; gap: 6px;">
                        <label class="dd-item checkbox" style="display: flex; align-items: center; gap: 8px; cursor: pointer; padding: 6px 10px; font-size: 13px; margin: 0; color: var(--color-text-secondary); border-radius: 8px; border: 1px solid transparent; transition: all 0.15s ease;">
                          <input type="checkbox" v-model="spreadsheetTabRef.showOnlyAssigned" style="width: 14px; height: 14px; accent-color: var(--color-accent); margin: 0;" />
                          <span>{{ t('Only assigned') }}</span>
                        </label>
                        <label class="dd-item checkbox" style="display: flex; align-items: center; gap: 8px; cursor: pointer; padding: 6px 10px; font-size: 13px; margin: 0; color: var(--color-text-secondary); border-radius: 8px; border: 1px solid transparent; transition: all 0.15s ease;">
                          <input type="checkbox" v-model="spreadsheetTabRef.hideDone" style="width: 14px; height: 14px; accent-color: var(--color-accent); margin: 0;" />
                          <span>{{ t('Hide done') }}</span>
                        </label>
                        <label class="dd-item checkbox" style="display: flex; align-items: center; gap: 8px; cursor: pointer; padding: 6px 10px; font-size: 13px; margin: 0; color: var(--color-text-secondary); border-radius: 8px; border: 1px solid transparent; transition: all 0.15s ease;">
                          <input type="checkbox" v-model="spreadsheetTabRef.showOnlyScheduled" style="width: 14px; height: 14px; accent-color: var(--color-accent); margin: 0;" />
                          <span>{{ t('Only dated') }}</span>
                        </label>
                      </div>
                    </div>
                    <div class="dd-divider" style="height: 1px; background: var(--color-border); margin: 4px 0;"></div>
                    <!-- Page Size -->
                    <div class="dd-section" style="padding: 0;">
                      <div class="dd-title filter-label" style="margin-bottom: 8px;">
                        <span>{{ t('Rows per page') }}</span>
                      </div>
                      <select :value="spreadsheetTabRef.pageSize" @change="spreadsheetTabRef.changePageSize" class="filter-select-trigger" style="width: 100%; border: 1px solid var(--color-border); padding: 6px 10px; font-size: 13px; border-radius: 8px; background: var(--color-input-bg); color: var(--color-text-primary);">
                        <option :value="20">20 {{ t('rows') }}</option>
                        <option :value="25">25 {{ t('rows') }}</option>
                        <option :value="50">50 {{ t('rows') }}</option>
                        <option :value="100">100 {{ t('rows') }}</option>
                      </select>
                    </div>
                  </template>

                  <!-- Timeline Display Options -->
                  <template v-if="currentTab === 'timeline' && timelineTabRef">
                    <div class="dd-section" style="padding: 0;">
                      <div class="dd-title filter-label" style="margin-bottom: 8px;">
                        <span>{{ t('Timeline Display') }}</span>
                      </div>
                      <div style="display: flex; flex-direction: column; gap: 6px;">
                        <label class="dd-item checkbox" style="display: flex; align-items: center; gap: 8px; cursor: pointer; padding: 6px 10px; font-size: 13px; margin: 0; color: var(--color-text-secondary); border-radius: 8px; border: 1px solid transparent; transition: all 0.15s ease;">
                          <input type="checkbox" v-model="timelineTabRef.expanded.showOnlyScheduled" style="width: 14px; height: 14px; accent-color: var(--color-accent); margin: 0;" />
                          <span>{{ t('Only scheduled items') }}</span>
                        </label>
                        <label class="dd-item checkbox" style="display: flex; align-items: center; gap: 8px; cursor: pointer; padding: 6px 10px; font-size: 13px; margin: 0; color: var(--color-text-secondary); border-radius: 8px; border: 1px solid transparent; transition: all 0.15s ease;">
                          <input type="checkbox" v-model="timelineTabRef.expanded.hideDone" style="width: 14px; height: 14px; accent-color: var(--color-accent); margin: 0;" />
                          <span>{{ t('Hide done items') }}</span>
                        </label>
                        <label class="dd-item checkbox" style="display: flex; align-items: center; gap: 8px; cursor: pointer; padding: 6px 10px; font-size: 13px; margin: 0; color: var(--color-text-secondary); border-radius: 8px; border: 1px solid transparent; transition: all 0.15s ease;">
                          <input type="checkbox" v-model="timelineTabRef.expanded.onlyCurrentWindow" style="width: 14px; height: 14px; accent-color: var(--color-accent); margin: 0;" />
                          <span>{{ t('Focus current window') }}</span>
                        </label>
                      </div>
                    </div>
                  </template>
             </div>
          </div>
        </template>
        <template #actions>
          <button class="plane-toolbar-btn" @click="showAnalyticsSidebar = true">{{ t('Analytics') }}</button>
        </template>
      </ProjectPageToolbar>
      <!-- Global Empty State for Work Items (List/Board views) -->
      <div v-if="!activeModuleFilterId && !store.loading && filteredTasksList.length === 0 && (currentTab === 'list' || currentTab === 'board')" class="empty-state-global">
        <div class="empty-spaces-icon" aria-hidden="true">
          <i class="fa-regular fa-folder-open"></i>
        </div>
        <div class="empty-spaces-copy">
          <h3>Chưa có công việc nào trong dự án này.</h3>
          <p style="margin-top: 6px;">Hãy tạo công việc đầu tiên hoặc nạp dữ liệu công việc từ Excel/CSV.</p>
        </div>
        <div style="display: flex; gap: 12px; justify-content: center; margin-top: 8px;">
          <button
            type="button"
            class="empty-state-action-btn"
            :class="{ active: selectedTask && selectedTask.isNew }"
            @click="openCreateTask('TO DO')"
            :disabled="!canCurrentUserCreateTask"
          >
            <i class="fa-solid fa-plus mr-1"></i> Tạo công việc mới
          </button>
          <button
            type="button"
            class="empty-state-action-btn"
            :class="{ active: showDataImportModal }"
            @click="showDataImportModal = true"
            :disabled="!canCurrentUserCreateTask"
          >
            <i class="fa-solid fa-file-import mr-1"></i> Nạp dữ liệu công việc
          </button>
        </div>
      </div>
      <div v-if="activeModuleFilterId && moduleDetail && moduleDetail.taskCount === 0 && !moduleDetailLoading && !moduleDetailError" class="module-empty-state">
        <i class="fa-regular fa-folder-open" aria-hidden="true"></i>
        <strong>{{ tr('This module has no work items yet.', 'Module này chưa có công việc.') }}</strong>
        <span>{{ tr('Work items assigned to this module will appear here.', 'Công việc được gán vào Module sẽ hiển thị tại đây.') }}</span>
      </div>
      <!-- Other Tab Views -->
      <div v-if="currentTab === 'list' && filteredTasksList.length > 0 && !moduleDetailLoading && !moduleDetailError" class="list-wrapper" style="padding: 16px;">
         <div class="plane-list-view">
           <div v-for="group in listViewGroups" :key="group.id" class="list-group">
             <div class="group-header" @click="toggleListGroup(group.id)" style="display: flex; align-items: center; justify-content: space-between; padding: 10px 14px; background: var(--color-surface); border-bottom: 1px solid var(--color-border); cursor: pointer;">
                <div class="gh-left" style="display: flex; align-items: center; gap: 8px;">
                  <i class="gh-chevron fa-solid" :class="collapsedListGroups[group.id] ? 'fa-chevron-right' : 'fa-chevron-down'"></i>
                  <i class="status-icon" :class="group.icon" :style="{ color: group.color }"></i>
                  <span class="group-name" style="font-weight: 600; font-size: 13.5px; color: var(--color-text-primary);">{{ group.name }}</span>
                  <span class="group-count" style="font-size: 11px; padding: 1px 6px; border-radius: 999px; background: rgba(148, 163, 184, 0.1); color: var(--color-text-secondary);">{{ group.items.length }}</span>

                </div>
                <div class="gh-right" style="display: flex; align-items: center;">
                  <i class="fa-solid fa-plus add-icon cursor-pointer text-gray-400 hover:text-sky-500" @click.stop="openCreateTask(group.statusName)"></i>
                </div>
              </div>
             <div class="group-content" v-show="!collapsedListGroups[group.id]">
              <template v-for="task in group.items" :key="task.id">
               <div class="task-row" @click="openTaskDetail(task)">
                 <div class="tr-left">
                   <button
                     class="star-task-btn"
                     type="button"
                     :class="{ starred: isTaskStarred(task.id) }"
                     :disabled="starredStore.isPending(STARRED_ENTITY_TYPES.WORK_TASK, task.id)"
                     aria-label="Toggle starred work item"
                     @click.stop="toggleTaskStar(task)"
                   >
                     <i :class="isTaskStarred(task.id) ? 'fa-solid fa-star text-yellow-400' : 'fa-regular fa-star text-gray-400'"></i>
                   </button>
                   <span class="task-id" style="margin-left: 8px;">{{ task.sequenceId || task.id.substring(0,8).toUpperCase() }}</span>
                   <span class="task-title" :style="group.statusName === 'DONE' ? { textDecoration: 'line-through', color: 'var(--color-text-muted)' } : {}">
                     <span v-if="task.title && task.title.startsWith('[DỰ PHÒNG]')" class="inline-flex items-center px-1.5 py-0.5 rounded-full bg-blue-100 text-blue-700 text-[10px] font-bold mr-1 border border-blue-200 uppercase tracking-wider relative top-[-1px]">Dự phòng</span>
                     {{ task.title && task.title.startsWith('[DỰ PHÒNG]') ? task.title.substring(11).trim() : task.title }}
                   </span>
                 </div>
                 <div class="tr-right" @click.stop>
                   <div class="pill-group">
                     <el-dropdown :disabled="!canMoveTaskStatus(task)" trigger="click" @command="(val) => updateTask(task, 'statusName', val, task.statusName)">
                       <div class="pill pill-status cursor-pointer hover:bg-[var(--color-border)]" :style="{ '--pill-color': getStatusColor(task.statusName) }">
                         <i :class="getBoardStatusIcon(task.statusName)" :style="{ color: getStatusColor(task.statusName) }"></i>
                         {{ normalizeStatusLabel(task.statusName) }}
                       </div>
                       <template #dropdown>
                         <el-dropdown-menu class="plane-dropdown">
                           <el-dropdown-item v-for="status in taskStatusOptions" :key="status.name" :command="status.name" class="color-option" :style="{ '--option-color': status.color }">
                             <i :class="status.icon" :style="{ color: status.color }"></i>
                             {{ status.label }}
                           </el-dropdown-item>
                         </el-dropdown-menu>
                       </template>
                     </el-dropdown>
                     <el-dropdown :disabled="!canEditTaskDetails(task)" trigger="click" @command="(val) => updateTask(task, 'priority', val, task.priority)">
                       <div class="pill pill-priority cursor-pointer hover:bg-[var(--color-border)]" :style="{ '--pill-color': getPriorityColor(task.priority) }">
                         <i :class="getPriorityIcon(task.priority)"></i>
                       </div>
                       <template #dropdown>
                         <el-dropdown-menu class="plane-dropdown">
                           <el-dropdown-item :command="1" class="color-option" style="--option-color:#ef4444"><i class="fa-solid fa-angles-up"></i> Urgent</el-dropdown-item>
                           <el-dropdown-item :command="2" class="color-option" style="--option-color:#f97316"><i class="fa-solid fa-chevron-up"></i> High</el-dropdown-item>
                           <el-dropdown-item :command="3" class="color-option" style="--option-color:#3b82f6"><i class="fa-solid fa-minus"></i> Medium</el-dropdown-item>
                           <el-dropdown-item :command="4" class="color-option" style="--option-color:#10b981"><i class="fa-solid fa-chevron-down"></i> Low</el-dropdown-item>
                           <el-dropdown-item :command="0" class="color-option" style="--option-color:#64748b"><i class="fa-solid fa-ban"></i> None</el-dropdown-item>
                         </el-dropdown-menu>
                       </template>
                     </el-dropdown>
                      <!-- Module Pill -->
                      <div v-if="task.moduleName || task.moduleId" class="pill pill-module" style="--pill-color: #8b5cf6; display: inline-flex; align-items: center; gap: 4px; padding: 2px 8px; border-radius: 6px; font-size: 11px; font-weight: 500; border: 1px solid color-mix(in srgb, #8b5cf6 30%, var(--color-border)); background: color-mix(in srgb, #8b5cf6 8%, var(--color-surface)); color: #7c3aed;">
                        <i class="fa-solid fa-cubes"></i>
                        <span>{{ task.moduleName || `Module ${task.moduleId.substring(0,8).toUpperCase()}` }}</span>
                      </div>
                     <el-popover :disabled="!canAssignTaskMember()" placement="bottom" trigger="click" width="260" popper-class="plane-popover">
                       <template #reference>
                         <div class="pill pill-user cursor-pointer hover:bg-[var(--color-border)]">
                           <div class="avatar-xxs" style="border: none; padding: 0;">
                             <div v-if="!getTaskAssigneeIds(task).length" style="width: 20px; height: 20px; border-radius: 50%; background: #e2e8f0; color: #64748b; display: flex; align-items: center; justify-content: center; border: 1px dashed #cbd5e1;">
                               <i class="fa-solid fa-question" style="font-size: 10px;"></i>
                             </div>
                             <span v-else>{{ getTaskAssigneeSummary(task).avatar }}</span>
                           </div>
                           <span v-if="getTaskAssigneeSummary(task).label" class="pill-user-text" style="margin-left: 4px;">{{ getTaskAssigneeSummary(task).label }}</span>
                         </div>
                       </template>
                       <div class="popover-content" style="padding-top: 8px;">
                         <label class="assignee-search-field mb-2">
                           <i class="fa-solid fa-magnifying-glass assignee-search-icon"></i>
                           <input type="text" class="assignee-search-input" v-model="assigneeSearch" placeholder="Search members" />
                         </label>
                         <div class="popover-list mt-1">
                           <div
                             v-for="member in filteredProjectMembers"
                             :key="member.userId || member.id"
                             class="popover-item flex items-center justify-between transition-colors cursor-pointer"
                             @click.stop="toggleTaskAssignee(task, member.userId || member.id)"
                             :class="getTaskAssigneeIds(task).includes(member.userId || member.id) ? 'assignee-option-selected' : 'hover:bg-gray-100'"
                           >
                             <div class="flex items-center truncate max-w-[75%] pl-2">
                               <UserAvatar :user="member" :size="22" :fontSize="10" class="mr-2" />
                               <span class="truncate" :class="getTaskAssigneeIds(task).includes(member.userId || member.id) ? 'font-semibold' : ''">{{ member.fullName || member.name || member.email }}</span>
                             </div>
                             <div class="flex items-center flex-shrink-0 pr-2">
                               <span v-if="member.taskPercentage !== undefined" class="text-[11px] px-1.5 py-0.5 rounded text-gray-500">{{ member.taskPercentage }}%</span>
                             </div>
                           </div>
                         </div>
                       </div>
                     </el-popover>
                   </div>
                 </div>
               </div>
               </template>
               <div class="add-row-placeholder" @click="openCreateTask(group.statusName)">
                 <i class="fa-solid fa-plus"></i> {{ t('New work item', 'Tạo công việc mới') }}
               </div>
             </div>
           </div>
         </div>
      </div>
      <div v-if="currentTab === 'calendar' && !moduleDetailLoading && !moduleDetailError && (!activeModuleFilterId || moduleDetail?.taskCount > 0)" class="calendar-wrapper">
         <CalendarTab ref="calendarTabRef" :tasks="filteredTasksList" @open-task="openTaskDetail" @create-task="openCreateTaskFromCalendar" />
      </div>
      <div v-if="currentTab === 'spreadsheet' && !moduleDetailLoading && !moduleDetailError && (!activeModuleFilterId || moduleDetail?.taskCount > 0)" class="spreadsheet-wrapper">
          <SpreadsheetTab
            ref="spreadsheetTabRef"
            :tasks="filteredTasksList"
            :projectId="getProjectId()"
            :projectMembers="projectMembers"
            :serverPagination="activeModuleFilterId ? moduleTaskPagination : null"
            :readonly="Boolean(activeModuleFilterId)"
            @task-click="openTaskDetail"
            @update-task="updateTask"
            @create-task="payload => openCreateTask(payload?.statusName || 'TO DO')"
            @page-change="changeModuleTaskPage"
            @page-size-change="changeModuleTaskPageSize"
          />
      </div>
      <div v-if="currentTab === 'timeline' && !moduleDetailLoading && !moduleDetailError && (!activeModuleFilterId || moduleDetail?.taskCount > 0)" class="timeline-wrapper">
          <TimelineTab ref="timelineTabRef" :projectId="getProjectId()" :tasks="filteredTasksList" :projectMembers="projectMembers" @open-task="openTaskDetail" @create-task="openCreateTaskFromCalendar" />
      </div>
      <!-- Kanban Board Layout -->
      <div
        class="kanban-wrapper"
        v-if="currentTab === 'board' && filteredTasksList.length > 0 && !moduleDetailLoading && !moduleDetailError"
        @wheel="handleKanbanWheel"
      >
        <!-- Loading indicator -->
        <div class="kanban-loading-bar" v-if="store.loading">
          <i class="fa-solid fa-spinner fa-spin"></i>
          <span>{{ t('Loading data...') }}</span>
        </div>
        <!-- Error banner -->
        <div class="kanban-error-banner" v-if="store.error && !store.loading">
          <i class="fa-solid fa-triangle-exclamation"></i>
          <span>{{ t('Unable to load work items. Reconnecting...') }}</span>
          <button class="kanban-retry-btn" @click="fetchTasks()">
            <i class="fa-solid fa-rotate-right"></i> {{ t('Retry') }}
          </button>
        </div>
        <div
          class="kanban-col"
          v-for="col in kanbanColumns"
          :key="col.id"
          :data-col-id="col.id"
          :style="{ '--col-color': col.color, '--col-bg': col.bgColor }"
        >
          <div class="col-head" style="display: flex; flex-direction: column; align-items: stretch; gap: 4px; padding: 10px 12px; min-height: 56px;">
            <div style="display: flex; align-items: center; justify-content: space-between; width: 100%;">
              <div class="col-title" style="display: flex; align-items: center; gap: 8px;">
                <i :class="col.icon" :style="{ color: col.color }"></i>
                <span class="font-semibold text-[13px] truncate max-w-[140px]">{{ col.label || col.name }}</span>
                <span class="col-count" style="font-size: 11px; padding: 1px 6px; border-radius: 999px; background: rgba(148, 163, 184, 0.1); color: var(--color-text-secondary);">{{ col.items.length }}</span>
              </div>
              <i v-if="canCurrentUserCreateTask && col.name !== 'FALLBACK_UNCLASSIFIED'" class="fa-solid fa-plus add-btn cursor-pointer text-gray-400 hover:text-sky-500" @click="openCreateTask(col.name)"></i>
            </div>

          </div>
          <div v-if="col.isFallback" class="fallback-desc-container" style="padding: 6px 12px; background: rgba(244, 63, 94, 0.05); border-bottom: 1px solid rgba(244, 63, 94, 0.1);">
            <small style="color: #f43f5e; font-size: 11px; font-style: italic;">
              {{ t('Các công việc có trạng thái không còn tồn tại trong workflow hiện tại.') }}
            </small>
          </div>
          <div class="col-body" :class="{ 'is-creating': inlineCreateColId === col.id }">
            <div
              v-if="inlineCreateColId === col.id"
              class="inline-create-box issue-card kanban-card-editor"
              @click.stop
            >
              <div class="inline-create-top">
                <div class="inline-create-planning">
                  <div class="inline-date-slot">
                    <el-date-picker
                      v-model="inlineDateRange"
                      type="daterange"
                      range-separator="-"
                      start-placeholder="Date"
                      end-placeholder="Date"
                      value-format="YYYY-MM-DDTHH:mm:ss.SSS[Z]"
                      format="DD/MM/YYYY"
                      size="default"
                      class="ic-date-range-picker ic-date-range-inline"
                    />
                  </div>
                  <div class="inline-assignee-slot">
                    <el-popover placement="bottom" trigger="click" width="260" popper-class="plane-popover" @click.stop>
                    <template #reference>
                      <button type="button" class="inline-assignee-trigger" :aria-label="tr('Choose assignee', 'Chọn người thực hiện')" :title="tr('Choose assignee', 'Chọn người thực hiện')">
                        <template v-if="inlineAssigneeIds.length">
                          <UserAvatar v-if="inlineAssigneeIds.length === 1" :user="projectMembers.find(m => (m.userId || m.id) === inlineAssigneeIds[0])" :size="22" :fontSize="10" />
                          <span v-else class="inline-assignee-count">+{{ inlineAssigneeIds.length }}</span>
                        </template>
                        <i v-else class="fa-solid fa-question"></i>
                      </button>
                    </template>
                    <div class="popover-content" style="padding-top: 8px;">
                      <label class="assignee-search-field mb-2">
                        <i class="fa-solid fa-magnifying-glass assignee-search-icon"></i>
                        <input type="text" class="assignee-search-input" v-model="assigneeSearch" placeholder="Search members" />
                      </label>
                      <div class="popover-list mt-1">
                        <div
                          v-for="member in filteredProjectMembers"
                          :key="member.userId || member.id"
                          class="popover-item flex items-center justify-between transition-colors cursor-pointer"
                          @click.stop="() => { const id = member.userId || member.id; const idx = inlineAssigneeIds.indexOf(id); if (idx > -1) inlineAssigneeIds.splice(idx, 1); else inlineAssigneeIds.push(id); }"
                          :class="inlineAssigneeIds.includes(member.userId || member.id) ? 'assignee-option-selected' : 'hover:bg-gray-100'"
                        >
                          <div class="flex items-center truncate max-w-[75%] pl-2">
                            <UserAvatar :user="member" :size="22" :fontSize="10" class="mr-2" />
                            <span class="truncate" :class="inlineAssigneeIds.includes(member.userId || member.id) ? 'font-semibold' : ''">{{ member.fullName || member.name || member.email }}</span>
                          </div>
                        </div>
                      </div>
                    </div>
                    </el-popover>
                  </div>
                </div>
                <input
                  type="text"
                  class="ic-title-input w-full"
                  v-model="inlineTaskTitle"
                  placeholder="Nhập tiêu đề công việc..."
                  @keyup.enter="submitInlineTask(col)"
                  @keyup.esc="inlineCreateColId = null"
                  ref="inlineInput"
                />
              </div>
              <div class="inline-create-meta" @click.stop>
                <el-dropdown trigger="click" @command="(val) => inlineStatusName = val">
                  <div class="badge status-badge cursor-pointer hover:bg-[var(--color-border)]" :style="{ '--badge-color': getStatusColor(inlineStatusName || col.name) }">
                    <i :class="getBoardStatusIcon(inlineStatusName || col.name)" :style="{ color: getStatusColor(inlineStatusName || col.name) }"></i>
                    <span>{{ normalizeStatusLabel(inlineStatusName || col.name) }}</span>
                  </div>
                  <template #dropdown>
                    <el-dropdown-menu class="plane-dropdown">
                      <el-dropdown-item v-for="status in taskStatusOptions" :key="status.name" :command="status.name" class="color-option" :style="{ '--option-color': status.color }">
                        <i :class="status.icon" :style="{ color: status.color }"></i>
                        {{ status.label }}
                      </el-dropdown-item>
                    </el-dropdown-menu>
                  </template>
                </el-dropdown>
                <el-dropdown trigger="click" @command="(val) => inlinePriority = val">
                  <div class="badge priority-badge cursor-pointer hover:bg-[var(--color-border)]" :style="{ '--badge-color': getPriorityColor(inlinePriority) }">
                    <i :class="getPriorityIcon(inlinePriority)"></i>
                    <span>{{ getPriorityLabel(inlinePriority) }}</span>
                  </div>
                  <template #dropdown>
                    <el-dropdown-menu class="plane-dropdown">
                      <el-dropdown-item :command="1" class="color-option" style="--option-color:#ef4444"><i class="fa-solid fa-angles-up"></i> Urgent</el-dropdown-item>
                      <el-dropdown-item :command="2" class="color-option" style="--option-color:#f97316"><i class="fa-solid fa-chevron-up"></i> High</el-dropdown-item>
                      <el-dropdown-item :command="3" class="color-option" style="--option-color:#3b82f6"><i class="fa-solid fa-minus"></i> Medium</el-dropdown-item>
                      <el-dropdown-item :command="4" class="color-option" style="--option-color:#10b981"><i class="fa-solid fa-chevron-down"></i> Low</el-dropdown-item>
                      <el-dropdown-item :command="0" class="color-option" style="--option-color:#64748b"><i class="fa-solid fa-ban"></i> None</el-dropdown-item>
                    </el-dropdown-menu>
                  </template>
                </el-dropdown>
              </div>
              <div class="inline-create-actions">
                <button class="inline-cancel-btn" @click="inlineCreateColId = null">Hủy</button>
                <button class="inline-submit-btn" @click="submitInlineTask(col)">
                  <span>Thêm</span>
                </button>
              </div>
            </div>
            <draggable
              class="col-draggable"
              :list="col.items"
              :group="{ name: 'tasks', put: col.name !== 'FALLBACK_UNCLASSIFIED' }"
              item-key="id"
              :disabled="!canCurrentUserUpdateTask || col.name === 'FALLBACK_UNCLASSIFIED'"
              :move="canMoveAssignedTask"
              @change="(evt) => handleDraggableChange(evt, col)"
            >
              <template #item="{ element }">
                <div
                  class="issue-card"
                  :class="{ 'active-card': selectedTask?.id === element.id }"
                  :style="{ '--task-status-color': getStatusColor(element.statusName), '--task-priority-color': getPriorityColor(element.priority) }"
                  @click="openTaskDetail(element)"
                >
                  <div class="issue-card-header">
                    <div class="issue-card-heading-copy">
                      <p v-if="displayProperties.id" class="issue-sequence">{{ element.sequenceId || element.id.substring(0,8).toUpperCase() }}</p>
                      <p class="issue-title" :title="element.title" :style="element.statusName === 'DONE' ? { textDecoration: 'line-through', color: 'var(--color-text-muted)' } : {}">
                        <span v-if="element.title && element.title.startsWith('[DỰ PHÒNG]')" class="inline-flex items-center px-1.5 py-0.5 rounded-full bg-blue-100 text-blue-700 text-[10px] font-bold mr-1 border border-blue-200 uppercase tracking-wider relative top-[-1px]">Dự phòng</span>
                        {{ element.title && element.title.startsWith('[DỰ PHÒNG]') ? element.title.substring(11).trim() : element.title }}
                      </p>
                    </div>
                    <div class="card-top-right">
                      <span
                        v-if="displayProperties.dueDate"
                        class="card-due-badge card-due-compact"
                        :class="{ 'card-due-overdue': (element.plannedEndDate || element.dueDate) && new Date(element.plannedEndDate || element.dueDate) < new Date() && element.statusName !== 'DONE', 'card-due-empty': !(element.plannedStartDate || element.plannedEndDate || element.dueDate) }"
                        :title="element.plannedEndDate || element.dueDate || element.plannedStartDate ? new Date(element.plannedEndDate || element.dueDate || element.plannedStartDate).toLocaleDateString('vi-VN') : tr('No deadline', 'Chưa có hạn')"
                      >
                        <i class="fa-regular fa-calendar"></i>
                        <span>{{ element.plannedEndDate || element.dueDate || element.plannedStartDate ? new Date(element.plannedEndDate || element.dueDate || element.plannedStartDate).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' }) : 'Date' }}</span>
                      </span>
                      <button
                        v-if="displayProperties.star"
                        class="star-task-btn small"
                        type="button"
                        :disabled="starredStore.isPending(STARRED_ENTITY_TYPES.WORK_TASK, element.id)"
                        aria-label="Toggle starred work item"
                        @click.stop="toggleTaskStar(element)"
                      >
                        <i :class="isTaskStarred(element.id) ? 'fa-solid fa-star text-yellow-400' : 'fa-regular fa-star text-gray-400'"></i>
                      </button>
                      <el-popover v-if="displayProperties.assignee" :disabled="!canAssignTaskMember()" placement="bottom" trigger="click" width="260" popper-class="plane-popover assignee-plane-popover">
                        <template #reference>
                          <button type="button" class="card-assignee-trigger" v-if="getTaskAssigneeSummary(element).label" :title="getTaskAssigneeSummary(element).label" @click.stop>
                            <UserAvatar v-if="getTaskAssigneeIds(element).length === 1" :user="getAssigneeUser(element)" :size="32" :fontSize="12" />
                            <span v-else class="card-assignee-count">+{{ getTaskAssigneeIds(element).length }}</span>
                          </button>
                          <button type="button" class="card-assignee-trigger is-empty" v-else :title="tr('No assignee', 'Chưa có người thực hiện')" @click.stop>
                            <i class="fa-solid fa-question"></i>
                          </button>
                        </template>
                        <div class="popover-content assignee-popover-content">
                          <label class="assignee-search-field mb-2">
                            <i class="fa-solid fa-magnifying-glass assignee-search-icon"></i>
                            <input type="text" class="assignee-search-input" v-model="assigneeSearch" placeholder="Search members" />
                          </label>
                          <div class="popover-list mt-1">
                            <div
                              v-for="member in filteredProjectMembers"
                              :key="member.userId || member.id"
                              class="popover-item flex items-center justify-between transition-colors cursor-pointer"
                              @click.stop="toggleTaskAssignee(element, member.userId || member.id)"
                              :class="getTaskAssigneeIds(element).includes(member.userId || member.id) ? 'assignee-option-selected' : 'hover:bg-gray-100'"
                            >
                              <div class="flex items-center truncate max-w-[75%] pl-2">
                                <UserAvatar :user="member" :size="22" :fontSize="10" class="mr-2" />
                                <span class="truncate" :class="getTaskAssigneeIds(element).includes(member.userId || member.id) ? 'font-semibold' : ''">{{ member.fullName || member.name || member.email }}</span>
                              </div>
                              <div class="flex items-center flex-shrink-0 pr-2">
                                <span v-if="member.taskPercentage !== undefined" class="text-[11px] px-1.5 py-0.5 rounded text-gray-500">{{ member.taskPercentage }}%</span>
                              </div>
                            </div>
                          </div>
                        </div>
                      </el-popover>
                    </div>
                  </div>
                  <div class="issue-meta mt-2" style="display:flex; align-items:center; gap:8px;" @click.stop>
                     <el-dropdown v-if="displayProperties.status" :disabled="!canMoveTaskStatus(element)" trigger="click" @command="(val) => updateTask(element, 'statusName', val, element.statusName)">
                       <div class="badge status-badge cursor-pointer hover:bg-[var(--color-border)]" :style="{ '--badge-color': getStatusColor(element.statusName) }">
                         <i :class="getBoardStatusIcon(element.statusName)" :style="{ color: getStatusColor(element.statusName) }"></i>
                         <span>{{ normalizeStatusLabel(element.statusName) }}</span>
                       </div>
                       <template #dropdown>
                         <el-dropdown-menu class="plane-dropdown">
                           <el-dropdown-item v-for="status in taskStatusOptions" :key="status.name" :command="status.name" class="color-option" :style="{ '--option-color': status.color }">
                             <i :class="status.icon" :style="{ color: status.color }"></i>
                             <span :style="{ color: status.color }">{{ status.label }}</span>
                           </el-dropdown-item>
                         </el-dropdown-menu>
                       </template>
                     </el-dropdown>
                     <el-dropdown v-if="displayProperties.priority" :disabled="!canEditTaskDetails(element)" trigger="click" @command="(val) => updateTask(element, 'priority', val, element.priority)">
                      <div class="badge priority-badge cursor-pointer hover:bg-[var(--color-border)]" :style="{ '--badge-color': getPriorityColor(element.priority) }">
                        <i :class="getPriorityIcon(element.priority)"></i>
                        <span>{{ getPriorityLabel(element.priority) }}</span>
                      </div>
                       <template #dropdown>
                         <el-dropdown-menu class="plane-dropdown">
                           <el-dropdown-item :command="1" class="color-option" style="--option-color:#ef4444"><i class="fa-solid fa-angles-up"></i> <span>Urgent</span></el-dropdown-item>
                           <el-dropdown-item :command="2" class="color-option" style="--option-color:#f97316"><i class="fa-solid fa-chevron-up"></i> <span>High</span></el-dropdown-item>
                           <el-dropdown-item :command="3" class="color-option" style="--option-color:#3b82f6"><i class="fa-solid fa-minus"></i> <span>Medium</span></el-dropdown-item>
                          <el-dropdown-item :command="4" class="color-option" style="--option-color:#10b981"><i class="fa-solid fa-chevron-down"></i> <span>Low</span></el-dropdown-item>
                           <el-dropdown-item :command="0" class="color-option" style="--option-color:#64748b"><i class="fa-solid fa-ban"></i> <span>None</span></el-dropdown-item>
                         </el-dropdown-menu>
                       </template>
                     </el-dropdown>
                     <!-- Module Badge -->
                     <div v-if="element.moduleName || element.moduleId" class="badge module-badge" style="--badge-color: #8b5cf6; display: inline-flex; align-items: center; gap: 4px; border: 1px solid color-mix(in srgb, #8b5cf6 30%, var(--color-border)); background: color-mix(in srgb, #8b5cf6 8%, var(--color-surface)); color: #7c3aed; padding: 2px 6px; border-radius: 4px; font-size: 10px;">
                       <i class="fa-solid fa-cubes"></i>
                       <span>{{ element.moduleName || `Module ${element.moduleId.substring(0,8).toUpperCase()}` }}</span>
                     </div>
                  </div>
                </div>
              </template>
              <!-- Empty state per-column -->
              <template #footer>
                <div 
                  class="col-empty-state" 
                  v-if="col.items.length === 0 && !store.loading && inlineCreateColId !== col.id"
                  :class="{ 'clickable': col.name !== 'FALLBACK_UNCLASSIFIED' && canCurrentUserCreateTask }"
                  @click="(col.name !== 'FALLBACK_UNCLASSIFIED' && canCurrentUserCreateTask) ? openInlineCreate(col.id) : null"
                >
                  <span v-if="col.name === 'FALLBACK_UNCLASSIFIED' || !canCurrentUserCreateTask" style="font-weight: normal; color: var(--color-text-muted);">Chưa có công việc nào</span>
                  <span v-else class="add-action-text"><i class="fa-solid fa-plus"></i> Thêm công việc</span>
                </div>
                <div 
                  class="col-empty-state col-bottom-add clickable" 
                  v-else-if="col.items.length > 0 && col.name !== 'FALLBACK_UNCLASSIFIED' && canCurrentUserCreateTask && !store.loading && inlineCreateColId !== col.id"
                  @click="openInlineCreate(col.id)"
                >
                  <span class="add-action-text"><i class="fa-solid fa-plus"></i> Thêm công việc</span>
                </div>
              </template>
            </draggable>
            <!-- Inline create box nâng cấp (date + assignee) -->
            <div class="inline-create-box issue-card kanban-card-editor shadow-sm border border-[var(--color-border)] rounded-xl p-3 bg-[var(--color-surface)]" v-if="false && inlineCreateColId === col.id" @click.stop>
               <!-- Top Row: Date Range Picker (Height 34px, radius 9px, no text header) -->
               <div class="mb-2">
                 <el-date-picker
                   v-model="inlineDateRange"
                   type="daterange"
                   range-separator="-"
                   start-placeholder="Ngày bắt đầu"
                   end-placeholder="Hạn chót"
                   value-format="YYYY-MM-DDTHH:mm:ss.SSS[Z]"
                   format="DD/MM/YYYY"
                   size="default"
                   class="ic-date-range-picker w-full"
                 />
               </div>
               <!-- Middle Row: Task Title Input (Height 34px, radius 9px) -->
               <div class="mb-2.5">
                 <input
                   type="text"
                   class="ic-title-input w-full"
                   v-model="inlineTaskTitle"
                   placeholder="Nhập tiêu đề công việc..."
                   @keyup.enter="submitInlineTask(col)"
                   @keyup.esc="inlineCreateColId = null"
                   ref="inlineInput"
                 />
               </div>
               <!-- Bottom Row: Meta Items (Status, Priority, Assignee) + Action Buttons (No HR divider line) -->
               <div class="flex items-center justify-between gap-2">
                 <!-- Meta Items Left (Exact match with task card issue-meta) -->
                 <div class="issue-meta flex items-center gap-2" @click.stop>
                   <!-- Status Dropdown -->
                   <el-dropdown trigger="click" @command="(val) => inlineStatusName = val">
                     <div class="badge status-badge cursor-pointer hover:bg-[var(--color-border)]" :style="{ '--badge-color': getStatusColor(inlineStatusName || col.name) }">
                       <i :class="getBoardStatusIcon(inlineStatusName || col.name)" :style="{ color: getStatusColor(inlineStatusName || col.name) }"></i>
                       <span>{{ normalizeStatusLabel(inlineStatusName || col.name) }}</span>
                     </div>
                     <template #dropdown>
                       <el-dropdown-menu class="plane-dropdown">
                         <el-dropdown-item v-for="status in taskStatusOptions" :key="status.name" :command="status.name" class="color-option" :style="{ '--option-color': status.color }">
                           <i :class="status.icon" :style="{ color: status.color }"></i>
                           {{ status.label }}
                         </el-dropdown-item>
                       </el-dropdown-menu>
                     </template>
                   </el-dropdown>
                   <!-- Priority Dropdown -->
                   <el-dropdown trigger="click" @command="(val) => inlinePriority = val">
                     <div class="badge priority-badge cursor-pointer hover:bg-[var(--color-border)]" :style="{ '--badge-color': getPriorityColor(inlinePriority) }">
                       <i :class="getPriorityIcon(inlinePriority)"></i>
                     </div>
                     <template #dropdown>
                       <el-dropdown-menu class="plane-dropdown">
                         <el-dropdown-item :command="1" class="color-option" style="--option-color:#ef4444"><i class="fa-solid fa-angles-up"></i> Urgent</el-dropdown-item>
                         <el-dropdown-item :command="2" class="color-option" style="--option-color:#f97316"><i class="fa-solid fa-chevron-up"></i> High</el-dropdown-item>
                         <el-dropdown-item :command="3" class="color-option" style="--option-color:#3b82f6"><i class="fa-solid fa-minus"></i> Medium</el-dropdown-item>
                         <el-dropdown-item :command="4" class="color-option" style="--option-color:#10b981"><i class="fa-solid fa-chevron-down"></i> Low</el-dropdown-item>
                         <el-dropdown-item :command="0" class="color-option" style="--option-color:#64748b"><i class="fa-solid fa-ban"></i> None</el-dropdown-item>
                       </el-dropdown-menu>
                     </template>
                   </el-dropdown>
                   <!-- Assignee Popover (Next to Priority badge, matching task card ? icon) -->
                   <el-popover placement="bottom" trigger="click" width="260" popper-class="plane-popover" @click.stop>
                     <template #reference>
                       <div class="avatar-xs cursor-pointer hover:bg-[var(--color-border)]" style="border: none; background: transparent; padding: 0; display: flex; align-items: center; justify-content: center;" v-if="inlineAssigneeIds.length">
                         <UserAvatar v-if="inlineAssigneeIds.length === 1" :user="projectMembers.find(m => (m.userId || m.id) === inlineAssigneeIds[0])" :size="24" :fontSize="11" />
                         <div v-else style="width: 24px; height: 24px; border-radius: 50%; background: #0c66e4; color: white; display: flex; align-items: center; justify-content: center; font-size: 11px; font-weight: bold;">
                           +{{ inlineAssigneeIds.length }}
                         </div>
                       </div>
                       <div class="avatar-xs cursor-pointer hover:bg-[var(--color-border)]" style="border: 1px dashed var(--color-text-muted); background: #e2e8f0; color: #64748b; display: flex; align-items: center; justify-content: center; width: 24px; height: 24px; border-radius: 50%;" v-else title="Gán người thực hiện">
                         <i class="fa-solid fa-question text-xs"></i>
                       </div>
                     </template>
                     <div class="popover-content" style="padding-top: 8px;">
                       <label class="assignee-search-field mb-2">
                         <i class="fa-solid fa-magnifying-glass assignee-search-icon"></i>
                         <input type="text" class="assignee-search-input" v-model="assigneeSearch" placeholder="Search members" />
                       </label>
                       <div class="popover-list mt-1">
                         <div
                           v-for="member in filteredProjectMembers"
                           :key="member.userId || member.id"
                           class="popover-item flex items-center justify-between transition-colors cursor-pointer"
                           @click.stop="() => { const id = member.userId || member.id; const idx = inlineAssigneeIds.indexOf(id); if (idx > -1) inlineAssigneeIds.splice(idx, 1); else inlineAssigneeIds.push(id); }"
                           :class="inlineAssigneeIds.includes(member.userId || member.id) ? 'assignee-option-selected' : 'hover:bg-gray-100'"
                         >
                           <div class="flex items-center truncate max-w-[75%] pl-2">
                             <UserAvatar :user="member" :size="22" :fontSize="10" class="mr-2" />
                             <span class="truncate" :class="inlineAssigneeIds.includes(member.userId || member.id) ? 'font-semibold' : ''">{{ member.fullName || member.name || member.email }}</span>
                           </div>
                         </div>
                       </div>
                     </div>
                   </el-popover>
                  </div>
                </div>
                <!-- Dedicated Action Row (Right aligned, clean separate row) -->
                <div class="flex items-center justify-end gap-2 pt-2.5 mt-2 border-t border-[color-mix(in_srgb,var(--color-border)_50%,transparent)]">
                  <button class="text-xs px-2.5 py-1 rounded-lg border border-gray-300 dark:border-gray-700 hover:bg-gray-100 dark:hover:bg-gray-800 text-gray-600 dark:text-gray-300 transition-colors" @click="inlineCreateColId = null">
                    Hủy
                  </button>
                  <button class="text-xs px-3 py-1 font-semibold rounded-lg bg-blue-600 hover:bg-blue-700 text-white shadow-sm transition-colors flex items-center gap-1" @click="submitInlineTask(col)">
                    <i class="fa-solid fa-check text-[10px]"></i> Thêm
                  </button>
                </div>
            </div>
          </div>
        </div>
      </div>
    </div>
    <!-- Task Detail Modal -->
    <TaskDetailModal
      v-if="selectedTask"
      :selectedTask="selectedTask"
      :projectId="getProjectId()"
      :projectMembers="projectMembers"
      :currentProjectRole="currentProjectRole"
      :canEditTaskDetails="canEditTaskDetails(selectedTask)"
      :canMoveTaskStatus="canMoveTaskStatus(selectedTask)"
      :canAssignTaskMember="canAssignTaskMember()"
      :canGoBack="taskDetailHistory.length > 0"
      @close="closeTaskDetail"
      @back="goBackTaskDetail"
      @open-task="openTaskDetailFromModal"
      @updateTask="updateTask"
      @refresh-tasks="fetchTasks({ preserveExisting: true, reset: false })"
      @created="handleTaskCreated"
    />
    <!-- Analytics Sidebar Overlay -->
    <div v-if="showAnalyticsSidebar" class="analytics-overlay" @click.self="closeAnalyticsSidebar">
      <div class="analytics-panel" :class="{ 'slide-in': showAnalyticsSidebar, 'is-expanded': isAnalyticsExpanded }">
         <div class="ap-header">
            <h3>Thống kê {{ project?.name || t('Project') }}</h3>
            <div class="ap-actions">
               <button class="icon-btn" @click="toggleAnalyticsExpand"><i :class="isAnalyticsExpanded ? 'fa-solid fa-compress' : 'fa-solid fa-expand'"></i></button>
               <button class="icon-btn" @click="closeAnalyticsSidebar"><i class="fa-solid fa-xmark"></i></button>
            </div>
         </div>
         <div class="ap-body">
            <!-- Stats -->
            <div class="ap-stats-grid">
               <div class="stat-box">
                  <span class="lbl">Tổng công việc</span>
                  <span class="val">{{ visibleTopLevelTasks.length }}</span>
               </div>
               <div class="stat-box">
                  <span class="lbl">Đang thực hiện</span>
                  <span class="val">{{ visibleTopLevelTasks.filter(t => t.statusName === 'IN PROGRESS').length }}</span>
               </div>
               <div class="stat-box">
                  <span class="lbl">Chờ xử lý</span>
                  <span class="val">{{ visibleTopLevelTasks.filter(t => !t.statusName || t.statusName === 'TO DO' || t.statusName === 'TODO').length }}</span>
               </div>
               <div class="stat-box">
                  <span class="lbl">Đang đánh giá</span>
                  <span class="val">{{ visibleTopLevelTasks.filter(t => t.statusName === 'IN REVIEW').length }}</span>
               </div>
               <div class="stat-box">
                  <span class="lbl">Hoàn thành</span>
                  <span class="val">{{ visibleTopLevelTasks.filter(t => t.statusName === 'DONE').length }}</span>
               </div>
            </div>
            <!-- Created vs Resolved Chart Overlay -->
            <div class="ap-chart-card mt-4">
               <h4>Đã tạo và đã xử lý</h4>
               <v-chart class="chart-container" :option="createdResolvedOptions" autoresize />
            </div>
            <!-- Customized Insights -->
            <div class="ap-chart-card mt-4">
               <div class="flex-between">
                  <h4>Phân tích tùy chỉnh</h4>
                  <el-dropdown trigger="click" @command="setAnalyticsInsightMode">
                    <button class="filter-btn" type="button">
                      <i class="fa-solid fa-sliders"></i> {{ analyticsInsightLabel }} <i class="fa-solid fa-chevron-down"></i>
                    </button>
                    <template #dropdown>
                      <el-dropdown-menu class="plane-dropdown">
                        <el-dropdown-item command="priority">Phân bổ độ ưu tiên</el-dropdown-item>
                        <el-dropdown-item command="status">Phân bổ trạng thái</el-dropdown-item>
                        <el-dropdown-item command="assignee">Phân bổ người thực hiện</el-dropdown-item>
                      </el-dropdown-menu>
                    </template>
                  </el-dropdown>
               </div>
               <v-chart class="chart-container mt-4" :option="insightChartOptions" autoresize />
            </div>
            <!-- Tables -->
            <div class="ap-table-wrap mt-4">
               <div class="table-head">
                  <span class="text-muted">{{ analyticsBreakdownRows.length }} {{ analyticsTableHeading }}</span>
                  <div class="flex-center gap-1">
                     <i class="fa-solid fa-magnifying-glass text-muted"></i>
                     <button class="export-btn" @click="exportAnalyticsCsv()"><i class="fa-solid fa-download"></i> Xuất CSV</button>
                  </div>
               </div>
               <table class="ap-table">
                  <thead><tr><th>{{ analyticsTableHeading }}</th><th style="text-align: right;">Số lượng</th></tr></thead>
                  <tbody>
                     <tr v-for="row in analyticsBreakdownRows" :key="row.label" :style="{ '--row-color': row.color || 'var(--color-accent)' }">
                       <td><span class="analytics-row-label"><span class="analytics-row-dot"></span>{{ row.label }}</span></td>
                       <td style="text-align: right;">{{ row.count }}</td>
                     </tr>
                  </tbody>
               </table>
            </div>
            <div class="ap-table-wrap mt-4">
               <div class="table-head">
                  <span class="text-muted">{{ assigneeAnalyticsRows.length }} người thực hiện</span>
                  <div class="flex-center gap-1">
                     <i class="fa-solid fa-magnifying-glass text-muted"></i>
                     <button class="export-btn" @click="exportAnalyticsCsv('assignee')"><i class="fa-solid fa-download"></i> Xuất CSV</button>
                  </div>
               </div>
               <table class="ap-table">
                  <thead>
                     <tr>
                        <th>Người thực hiện</th>
                        <th style="text-align: right;">Chờ xử lý</th>
                        <th style="text-align: right;">Đang làm</th>
                        <th style="text-align: right;">Đang đánh giá</th>
                        <th style="text-align: right;">Hoàn thành</th>
                        <th style="text-align: right;">Đã hủy</th>
                     </tr>
                  </thead>
                  <tbody>
                     <tr v-for="row in assigneeAnalyticsRows" :key="row.id">
                        <td><i class="fa-regular fa-user"></i> {{ row.label }}</td>
                        <td style="text-align: right;">{{ row.backlog }}</td>
                        <td style="text-align: right;">{{ row.started }}</td>
                        <td style="text-align: right;">{{ row.unstarted }}</td>
                        <td style="text-align: right;">{{ row.completed }}</td>
                        <td style="text-align: right;">{{ row.cancelled }}</td>
                     </tr>
                  </tbody>
               </table>
            </div>
         </div>
      </div>
    </div>
  </ProjectPageContainer>
</template>
<script setup>
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import PageToolbar from '@/components/common/PageToolbar.vue'
import TaskDataImportModal from '@/components/tasks/TaskDataImportModal.vue'
// AI 3: CHUYÊN VIÊN GHÉP NỐI LOGIC FRONT-TO-BACK
import { ref, onMounted, computed, defineAsyncComponent, watch, nextTick, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'
import { getModuleDetail } from '@/api/moduleApi'
import { downloadResponseFile, csvWithBom } from '@/utils/downloadFile'
import { broadcastAdminRealtime, subscribeAdminRealtime } from '@/utils/adminRealtime'
import { getStoredUserSession } from '@/utils/authSession'
import { getScopedCurrentProjectId, setScopedCurrentProjectId } from '@/utils/projectContext'
import { buildSpacePath } from '@/utils/spaceRoute'
import { signalRService } from '@/api/signalrService'
import { hasSystemAdminAccess, normalizeProjectRole } from '@/utils/permissions'
import { 
  getDefaultPermissionMatrix,
  canCreateTask,
  canUpdateTask,
  canAssignTask,
  canChangeTaskStatus,
  hasAssigneeOnlyTaskAccess,
  canDeleteTask 
} from '@/utils/permissionGuard'
import draggable from 'vuedraggable'
import TaskDetailModal from '@/components/TaskDetailModal.vue'
import CalendarTab from '@/components/CalendarTab.vue'
import TimelineTab from '@/components/TimelineTab.vue'
import SpreadsheetTab from '@/components/SpreadsheetTab.vue'
import FilterBar from '@/components/FilterBar.vue'
import { useWorkTaskStore } from '@/store/useWorkTaskStore';
import { useProjectStore } from '@/store/useProjectStore';
import { useStarredStore } from '@/store/useStarredStore';
import { STARRED_ENTITY_TYPES } from '@/api/starredRecentApi'
import { useI18nStore } from '@/store/useI18nStore';
import UserAvatar from '@/components/common/UserAvatar.vue'
import { getProjectBackgroundStyle } from '@/config/projectAppearance'
import { projectAccessRestrictionsEnabled } from '@/config/projectAccess'
import { use } from 'echarts/core';
import { CanvasRenderer } from 'echarts/renderers';
import { LineChart, BarChart } from 'echarts/charts';
import { TitleComponent, TooltipComponent, LegendComponent, GridComponent } from 'echarts/components';
import { LegacyGridContainLabel } from 'echarts/features';
import VChart from 'vue-echarts';
use([
  CanvasRenderer,
  LineChart,
  BarChart,
  TitleComponent,
  TooltipComponent,
  LegendComponent,
  GridComponent,
  LegacyGridContainLabel
]);
// Realtime SignalR handler state variables
let unsubscribeAdminRealtime = null
let signalRTaskUpdatedHandler = null
let signalREntityChangedHandler = null
let signalRProjectEventHandler = null
let realtimeRefreshTimer = null

const calendarTabRef = ref(null)
const timelineTabRef = ref(null)
const spreadsheetTabRef = ref(null)

const showDisplayDropdown = ref(false)
const showFilterDropdown = ref(false)
function toggleFilterDropdown() {
  showFilterDropdown.value = !showFilterDropdown.value
  if (showFilterDropdown.value) {
    showDisplayDropdown.value = false
    showSortDropdown.value = false
  }
}
function toggleDisplayDropdown() {
  showDisplayDropdown.value = !showDisplayDropdown.value
  if (showDisplayDropdown.value) {
    showFilterDropdown.value = false
    showSortDropdown.value = false
  }
}
function handleGlobalDropdownClick(event) {
  if (event?.target?.closest?.('.js-toolbar-popup-scope')) return
  showFilterDropdown.value = false
  showDisplayDropdown.value = false
  showSortDropdown.value = false
  openSortSelect.value = null
}
const showAnalyticsSidebar = ref(false)
const isAnalyticsExpanded = ref(false)
const showFilterPanel = ref(false)
const isForbidden = ref(false)
const showSubtasks = ref(false)
const collapsedListGroups = ref({})
const assigneeSearch = ref('')
const showDataImportModal = ref(false)
async function handleExportTasks() {
  try {
    const res = await axiosClient.get(`/projects/${currentProjectId.value}/WorkTasks/export`, { responseType: 'blob' })
    downloadResponseFile(res, `SprintA-Tasks-${currentProjectId.value}.csv`, 'text/csv;charset=utf-8')
    ElMessage.success('Xuất dữ liệu thành công.')
  } catch (e) {
    ElMessage.error('Không thể xuất dữ liệu công việc.')
  }
}
const router = useRouter()
const route = useRoute()
const currentProjectId = computed(() => route.params.id ? `${route.params.id}` : null)
const store = useWorkTaskStore();
const projectStore = useProjectStore()
const starredStore = useStarredStore()
const i18nStore = useI18nStore()
const tr = (en, vi) => i18nStore.locale === 'vi' ? vi : en
const t = (key) => {
  const map = {
    'Project': 'Dự án',
    'Work Items': 'Công việc',
    'Display': 'Hiển thị',
    'Display Properties': 'Thuộc tính hiển thị',
    'Order by': 'Sắp xếp theo',
    'Manual': 'Thủ công',
    'Last created': 'Tạo gần nhất',
    'Last updated': 'Cập nhật gần nhất',
    'Priority': 'Độ ưu tiên',
    'Show sub-work items': 'Hiển thị công việc con',
    'Analytics': 'Thống kê',
    'Add work item': 'Thêm công việc',
    'Access Denied': 'Truy cập bị từ chối',
    'You do not have permission to access this project.': 'Bạn không đủ quyền để truy cập dự án này.',
    'Back to Home': 'Quay lại trang Home',
    'List view': 'Xem danh sách',
    'Kanban view': 'Xem Kanban',
    'Calendar view': 'Xem lịch',
    'Spreadsheet view': 'Xem bảng tính',
    'Gantt chart view': 'Xem biểu đồ Gantt',
    'Urgent': 'Khẩn cấp',
    'High': 'Cao',
    'Normal': 'Bình thường',
    'Medium': 'Trung bình',
    'Low': 'Thấp',
    'None': 'Không',
    'Search members': 'Tìm thành viên',
    'New work item': 'Công việc mới',
    'Statistics of': 'Thống kê',
    'Total tasks': 'Tổng công việc',
    'In progress': 'Đang thực hiện',
    'Pending': 'Chờ xử lý',
    'In review': 'Đang đánh giá',
    'Completed': 'Hoàn thành',
    'Created and resolved': 'Đã tạo và đã xử lý',
    'Custom analysis': 'Phân tích tùy chỉnh',
    'Priority distribution': 'Phân bổ độ ưu tiên',
    'Status distribution': 'Phân bổ trạng thái',
    'Assignee distribution': 'Phân bổ người thực hiện',
    'Export CSV': 'Xuất CSV',
    'Count': 'Số lượng',
    'assignees': 'người thực hiện',
    'Assignee': 'Người thực hiện',
    'Working': 'Đang làm',
    'Cancelled': 'Đã hủy'
  }
  if (i18nStore.locale === 'vi') {
    return map[key] || key
  }
  return key
}
const project = ref({})
const rawTasks = ref([])
const allTasks = ref([])
const projectMembers = ref([])
const projectStatuses = ref([])
const projectExecutionRules = ref({
  enableRoleBasedTaskVisibility: false,
  managerAlwaysSeeAllTasks: true
})
const visibilityOverrideRoles = ['pm', 'po', 'project_lead', 'admin']
const selectedTask = ref(null)
const taskDetailHistory = ref([])
const inlineCreateColId = ref(null)
const inlineTaskTitle = ref('')
const inlineDueDate = ref('')
const inlineAssigneeIds = ref([])
const inlineDateRange = ref([])
const inlineStatusName = ref('BACKLOG')
const inlinePriority = ref(0)
const currentTab = ref('board')
const searchQuery = ref('')
const activeFilters = ref({ assignee: null })
const activeTaskFilters = ref([])
const displayOrder = ref('manual')
const showSortDropdown = ref(false)
const sortDirection = ref('asc')
const openSortSelect = ref(null)
const sortSearchQuery = ref('')
function toggleSortDropdown() {
  showSortDropdown.value = !showSortDropdown.value
  openSortSelect.value = null
  sortSearchQuery.value = ''
  if (showSortDropdown.value) {
    showDisplayDropdown.value = false
    showFilterDropdown.value = false
  }
}
const displayOrderOptions = computed(() => [
  { value: 'manual', label: tr('Manual', 'Thủ công'), icon: 'fa-solid fa-hand' },
  { value: 'created', label: tr('Created date', 'Ngày tạo'), icon: 'fa-regular fa-calendar-plus' },
  { value: 'updated', label: tr('Updated date', 'Ngày cập nhật'), icon: 'fa-regular fa-pen-to-square' },
  { value: 'priority', label: tr('Priority', 'Độ ưu tiên'), icon: 'fa-solid fa-signal' },
  { value: 'dueDate', label: tr('Due date', 'Ngày hạn'), icon: 'fa-regular fa-clock' },
  { value: 'title', label: tr('Title', 'Tiêu đề'), icon: 'fa-solid fa-font' },
  { value: 'assignee', label: tr('Assignee', 'Người thực hiện'), icon: 'fa-regular fa-user' },
  { value: 'sprint', label: tr('Sprint', 'Chu kỳ'), icon: 'fa-solid fa-arrows-spin' },
  { value: 'module', label: tr('Module', 'Phân hệ'), icon: 'fa-solid fa-cubes' }
])
const filteredDisplayOrderOptions = computed(() => {
  const q = sortSearchQuery.value.trim().toLowerCase()
  if (!q) return displayOrderOptions.value
  return displayOrderOptions.value.filter(opt => opt.label.toLowerCase().includes(q))
})
const getDisplayOrderLabel = (val) => {
  const opt = displayOrderOptions.value.find(o => o.value === val)
  return opt ? opt.label : val
}
const defaultDisplayProperties = {
  id: true,
  dueDate: true,
  star: true,
  status: true,
  priority: true,
  assignee: true
}
const displayProperties = ref({ ...defaultDisplayProperties })
const displayPropertyOptions = computed(() => [
  { key: 'id', label: 'ID', icon: 'fa-solid fa-hashtag' },
  { key: 'dueDate', label: tr('Due date', 'Ngày hạn'), icon: 'fa-regular fa-calendar' },
  { key: 'star', label: tr('Star', 'Đánh dấu'), icon: 'fa-regular fa-star' },
  { key: 'status', label: tr('Status', 'Trạng thái'), icon: 'fa-regular fa-circle-dot' },
  { key: 'priority', label: tr('Priority', 'Độ ưu tiên'), icon: 'fa-solid fa-signal' },
  { key: 'assignee', label: tr('Assignee', 'Người thực hiện'), icon: 'fa-regular fa-user' }
])
const groupBy = ref('status')
const analyticsInsightMode = ref('priority')
const analyticsTheme = ref(document.documentElement.getAttribute('data-theme') || 'light')
let analyticsThemeObserver = null
const activeSprintFilterId = computed(() => route.query.sprintId || route.params.cycleId || null)
const analyticsThemeColors = computed(() => {
  const isDark = analyticsTheme.value === 'dark'
  return {
    text: isDark ? '#e5edf7' : '#0f172a',
    muted: isDark ? '#a8b4c7' : '#64748b',
    grid: isDark ? 'rgba(148, 163, 184, 0.22)' : 'rgba(100, 116, 139, 0.18)',
    axis: isDark ? 'rgba(148, 163, 184, 0.36)' : 'rgba(100, 116, 139, 0.28)',
    tooltipBg: isDark ? '#0f172a' : '#ffffff',
    tooltipBorder: isDark ? 'rgba(148, 163, 184, 0.24)' : 'rgba(100, 116, 139, 0.18)'
  }
})
watch(currentTab, (val) => {
  if (val === 'board') {
    document.body.classList.add('no-shadow-context')
  } else {
    document.body.classList.remove('no-shadow-context')
  }
}, { immediate: true })
onUnmounted(() => {
  document.body.classList.remove('no-shadow-context')
})
const activeModuleFilterId = computed(() => route.query.moduleId || null)
const moduleDetail = ref(null)
const moduleDetailLoading = ref(false)
const moduleDetailError = ref(null)
const moduleTaskPage = ref(1)
const moduleTaskPageSize = ref(20)
const moduleTaskPagination = ref({
  page: 1,
  pageSize: 20,
  totalCount: 0,
  totalPages: 0,
  hasPreviousPage: false,
  hasNextPage: false
})
let moduleDetailAbortController = null
let moduleDetailRequestId = 0
let initialDataRequestId = 0
const activeCarryOverSprintId = computed(() => route.query.carryOverFromSprintId || null)
const carryOverTaskIds = ref([])
const projectBadge = computed(() => project.value?.icon || project.value?.identifier?.charAt(0)?.toUpperCase() || project.value?.name?.charAt(0)?.toUpperCase() || 'P')
const getShowSubtasksStorageKey = (projectId = currentProjectId.value || getProjectId()) => `space-summary:${projectId || 'default'}:show-subtasks`
const getDisplayPropertiesStorageKey = (projectId = currentProjectId.value || getProjectId()) => `space-summary:${projectId || 'default'}:display-properties`
const loadShowSubtasksPreference = (projectId = currentProjectId.value || getProjectId()) => {
  try {
    return localStorage.getItem(getShowSubtasksStorageKey(projectId)) === 'true'
  } catch {
    return false
  }
}
const persistShowSubtasksPreference = (value, projectId = currentProjectId.value || getProjectId()) => {
  try {
    localStorage.setItem(getShowSubtasksStorageKey(projectId), value ? 'true' : 'false')
  } catch {
    // ignore storage failures
  }
}
const loadDisplayPropertiesPreference = (projectId = currentProjectId.value || getProjectId()) => {
  try {
    const raw = localStorage.getItem(getDisplayPropertiesStorageKey(projectId))
    if (!raw) return { ...defaultDisplayProperties }
    const parsed = JSON.parse(raw)
    return { ...defaultDisplayProperties, ...(parsed && typeof parsed === 'object' ? parsed : {}) }
  } catch {
    return { ...defaultDisplayProperties }
  }
}
const persistDisplayPropertiesPreference = (value, projectId = currentProjectId.value || getProjectId()) => {
  try {
    localStorage.setItem(getDisplayPropertiesStorageKey(projectId), JSON.stringify({ ...defaultDisplayProperties, ...value }))
  } catch {
    // ignore storage failures
  }
}
const toggleDisplayProperty = (key) => {
  if (!(key in defaultDisplayProperties)) return
  displayProperties.value = {
    ...displayProperties.value,
    [key]: !displayProperties.value[key]
  }
}
const isForbiddenError = (error) => Number(error?.response?.status) === 403
const getSessionIdentity = () => {
  const user = getStoredUserSession()
  return `${user?.id || user?.userId || user?.email || 'anonymous'}`
}
const getModuleErrorStatus = (error) => Number(error?.response?.status || error?.status || 0)
const getModuleErrorMessage = (error) => {
  const status = getModuleErrorStatus(error)
  if (status === 400) return tr('The module request is invalid.', 'Yêu cầu Module không hợp lệ.')
  if (status === 403) return tr('You do not have permission to view this module.', 'Bạn không có quyền xem Module này.')
  if (status === 404) return tr('This module does not exist or is no longer accessible.', 'Module không tồn tại hoặc bạn không còn quyền truy cập.')
  return tr('Module data could not be loaded. Check your connection and try again.', 'Không thể tải dữ liệu Module. Hãy kiểm tra kết nối và thử lại.')
}
const clearModuleDetailState = ({ keepPage = false } = {}) => {
  moduleDetailAbortController?.abort()
  moduleDetailAbortController = null
  moduleDetailRequestId += 1
  moduleDetail.value = null
  moduleDetailError.value = null
  moduleDetailLoading.value = false
  moduleTaskPagination.value = {
    page: keepPage ? moduleTaskPage.value : 1,
    pageSize: moduleTaskPageSize.value,
    totalCount: 0,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false
  }
  if (!keepPage) moduleTaskPage.value = 1
}
const resolveAccessibleProjectId = async (preferredProjectId = null) => {
  const projects = await projectStore.fetchAllProjects(true)
  const accessibleProjects = (projects || []).filter(item => item?.id)
  if (!accessibleProjects.length) return null
  if (preferredProjectId && accessibleProjects.some(item => `${item.id}` === `${preferredProjectId}`)) {
    return preferredProjectId
  }
  return accessibleProjects[0].id
}
const recoverFromForbiddenProject = async (forbiddenProjectId) => {
  const fallbackProjectId = await resolveAccessibleProjectId()
  if (!fallbackProjectId) {
    rawTasks.value = []
    allTasks.value = []
    projectMembers.value = []
    project.value = {}
    ElMessage.error('You no longer have access to any project.')
    return false
  }
  if (`${fallbackProjectId}` === `${forbiddenProjectId}`) {
    rawTasks.value = []
    allTasks.value = []
    projectMembers.value = []
    project.value = {}
    ElMessage.error('You do not have permission to load work items for this project.')
    return false
  }
  dynamicProjectId = fallbackProjectId
  setScopedCurrentProjectId(fallbackProjectId)
  const fallbackProject = projectStore.allProjects.find(item => `${item.id}` === `${fallbackProjectId}`) || fallbackProjectId
  await router.replace(buildSpacePath(fallbackProject, 'work-items'))
  return true
}
const getParentTaskLinkId = (task) => (
  task?.parentTaskId ||
  task?.parentId ||
  task?.ParentTaskId ||
  task?.ParentId ||
  null
)
const isSubtask = (task) => Boolean(
  getParentTaskLinkId(task) ||
  task?.parentTaskTitle ||
  task?.parentTitle ||
  task?.parentName
)
const getTaskAssigneeIds = (task) => {
  return Array.from(new Set([
    ...(Array.isArray(task.assigneeIds) ? task.assigneeIds : []),
    ...(Array.isArray(task.assignees) ? task.assignees.map(item => item.userId || item.id).filter(Boolean) : []),
    ...(task.assignedUserId ? [task.assignedUserId] : [])
  ]))
}
const getCurrentUserId = () => {
  const user = getStoredUserSession()
  return user?.id || user?.userId || null
}
const isCurrentUserAssignedToTask = (task) => {
  if (!task || task.isNew) return true
  const assigneeIds = getTaskAssigneeIds(task)
  if (!assigneeIds.length) return false
  const currentUserId = getCurrentUserId()
  return Boolean(currentUserId && assigneeIds.some(id => `${id}` === `${currentUserId}`))
}
const isAssigneeOnlyTaskAccessEnabled = computed(() => {
  if (!projectAccessRestrictionsEnabled) return false
  if (hasSystemAdminAccess(getStoredUserSession())) return false
  return hasAssigneeOnlyTaskAccess(permissionMatrix.value, currentProjectRole.value)
})
const canEditTaskDetails = (task) => {
  if (hasSystemAdminAccess(getStoredUserSession())) return true
  if (!canUpdateTask(permissionMatrix.value, currentProjectRole.value)) return false
  return !isAssigneeOnlyTaskAccessEnabled.value || isCurrentUserAssignedToTask(task)
}
const canMoveTaskStatus = (task) => {
  if (hasSystemAdminAccess(getStoredUserSession())) return true
  if (!canChangeTaskStatus(permissionMatrix.value, currentProjectRole.value)) return false
  return !isAssigneeOnlyTaskAccessEnabled.value || isCurrentUserAssignedToTask(task)
}
const canAssignTaskMember = () => {
  if (hasSystemAdminAccess(getStoredUserSession())) return true
  return canAssignTask(permissionMatrix.value, currentProjectRole.value)
}
const notifyAssignmentLock = () => {
  ElMessage.warning('Chỉ người được giao mới có thể thay đổi công việc này.')
}
const canEditTaskByAssignment = (task) => {
  const canEdit = canEditTaskDetails(task)
  if (!canEdit) notifyAssignmentLock()
  return canEdit
}
const canApplyTaskUpdate = (task, payload) => {
  const keys = Object.keys(payload || {})
  if (keys.length > 0 && keys.every(key => key === 'statusName' || key === 'taskStatusId')) {
    const canMove = canMoveTaskStatus(task)
    if (!canMove) notifyAssignmentLock()
    return canMove
  }
  if (keys.length > 0 && keys.every(key => ['assigneeId', 'assigneeIds', 'assignedUserId'].includes(key))) {
    const canAssign = canAssignTaskMember()
    if (!canAssign) ElMessage.warning('Ban khong co quyen giao cong viec.')
    return canAssign
  }
  return canEditTaskByAssignment(task)
}
const canMoveAssignedTask = (event) => {
  const task = event?.draggedContext?.element
  return canMoveTaskStatus(task) || (notifyAssignmentLock(), false)
}
const getTaskAssigneeSummary = (task) => {
  const ids = getTaskAssigneeIds(task)
  if (!ids.length) return { label: '', avatar: '' }
  if (ids.length === 1) {
    const member = projectMembers.value.find(item => (item.userId || item.id) === ids[0])
    const label = member?.fullName || member?.name || member?.email || task.assigneeName || 'Assignee'
    return { label, avatar: label.substring(0, 1).toUpperCase() }
  }
  return { label: `${ids.length} assignees`, avatar: `${ids.length}` }
}
const getAssigneeUser = (task) => {
  const ids = getTaskAssigneeIds(task)
  if (!ids.length) return null
  return projectMembers.value.find(item => (item.userId || item.id) === ids[0]) || { fullName: task.assigneeName || 'Unknown' }
}
const matchesTaskFilters = (task) => {
  if (!task) return false
  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase()
    const title = task.title?.toLowerCase?.() || ''
    const sequenceId = task.sequenceId?.toLowerCase?.() || ''
    if (!title.includes(query) && !sequenceId.includes(query)) {
      return false
    }
  }
  if (activeFilters.value.assignee) {
    return getTaskAssigneeIds(task).includes(activeFilters.value.assignee.userId)
  }
  return true
}
const topLevelTasks = computed(() => rawTasks.value)
const visibleTasks = computed(() => {
  if (activeModuleFilterId.value) return allTasks.value || []
  const sourceTasks = showSubtasks.value ? (allTasks.value || []) : topLevelTasks.value
  return sourceTasks.filter(canCurrentUserSeeTask)
})
const visibleTopLevelTasks = computed(() => filteredTasksList.value.filter(task => !isSubtask(task)))
const defaultTaskStatusOptions = computed(() => [
  { name: 'BACKLOG', label: tr('Backlog', 'Chờ xử lý'), color: '#94A3B8', icon: 'fa-regular fa-circle-dashed' },
  { name: 'TO DO', label: tr('To Do', 'Cần làm'), color: '#A78BFA', icon: 'fa-regular fa-circle' },
  { name: 'IN PROGRESS', label: tr('In Progress', 'Đang thực hiện'), color: '#38BDF8', icon: 'fa-solid fa-circle-half-stroke' },
  { name: 'IN REVIEW', label: tr('In Review', 'Đang đánh giá'), color: '#F59E0B', icon: 'fa-solid fa-eye' },
  { name: 'DONE', label: tr('Done', 'Hoàn thành'), color: '#22C55E', icon: 'fa-solid fa-circle-check' },
  { name: 'CANCELLED', label: tr('Cancelled', 'Đã hủy'), color: '#F43F5E', icon: 'fa-regular fa-circle-xmark' }
])
const normalizeText = (value) => `${value || ''}`.toLowerCase().trim()
const normalizeStatus = (value) => `${value || 'BACKLOG'}`.toUpperCase().replace(/\s+/g, ' ').trim()
const resolveStatusIcon = (value) => {
  const status = normalizeStatus(value)
  if (status.includes('CANCEL')) return 'fa-regular fa-circle-xmark'
  if (status.includes('DONE') || status.includes('COMPLETE')) return 'fa-solid fa-circle-check'
  if (status.includes('PROGRESS') || status.includes('ACTIVE')) return 'fa-solid fa-circle-half-stroke'
  if (status.includes('REVIEW') || status.includes('TEST')) return 'fa-solid fa-eye'
  if (status.includes('TODO') || status.includes('TO DO')) return 'fa-regular fa-circle'
  return 'fa-regular fa-circle-dashed'
}
const taskStatusOptions = computed(() => {
  if (projectStatuses.value.length) {
    return projectStatuses.value.map((status, index) => ({
      name: normalizeStatus(status.name),
      label: status.displayName || status.name,
      color: status.colorCode || defaultTaskStatusOptions.value[index % defaultTaskStatusOptions.value.length]?.color || 'var(--color-text-muted)',
      icon: resolveStatusIcon(status.name)
    }))
  }
  return defaultTaskStatusOptions.value
})
const normalizeDateOnly = (value) => {
  if (!value) return null
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)) return value
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(value)) return value.slice(0, 10)
  if (value instanceof Date) {
    const year = value.getFullYear()
    const month = `${value.getMonth() + 1}`.padStart(2, '0')
    const day = `${value.getDate()}`.padStart(2, '0')
    return `${year}-${month}-${day}`
  }
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return null
  const year = parsed.getFullYear()
  const month = `${parsed.getMonth() + 1}`.padStart(2, '0')
  const day = `${parsed.getDate()}`.padStart(2, '0')
  return `${year}-${month}-${day}`
}
const getTaskDateOnly = (task, fields) => {
  for (const field of fields) {
    const normalized = normalizeDateOnly(task?.[field])
    if (normalized) return normalized
  }
  return null
}
const getTaskCreatedDate = (task) => getTaskDateOnly(task, ['createdAt', 'createdDate', 'createdOn', 'CreatedAt', 'CreatedDate'])
const getTaskResolvedDate = (task) => {
  if (normalizeStatus(task?.statusName) !== 'DONE') return null
  return getTaskDateOnly(task, [
    'completedAt',
    'completedDate',
    'resolvedAt',
    'doneAt',
    'closedAt',
    'updatedAt',
    'updatedDate',
    'UpdatedAt'
  ]) || getTaskCreatedDate(task)
}
const formatAnalyticsDateLabel = (dateOnly) => {
  if (!dateOnly) return ''
  const [year, month, day] = dateOnly.split('-').map(Number)
  return new Date(year, month - 1, day).toLocaleDateString('vi-VN', {
    day: '2-digit',
    month: '2-digit'
  })
}
const buildAnalyticsDateBuckets = (tasks) => {
  const dates = new Set()
  tasks.forEach(task => {
    const createdDate = getTaskCreatedDate(task)
    const resolvedDate = getTaskResolvedDate(task)
    if (createdDate) dates.add(createdDate)
    if (resolvedDate) dates.add(resolvedDate)
  })
  const sortedDates = Array.from(dates).sort()
  const windowDates = sortedDates.length > 14 ? sortedDates.slice(-14) : sortedDates
  const fallbackDate = normalizeDateOnly(new Date())
  const labels = windowDates.length ? windowDates : [fallbackDate]
  const createdCounts = new Map(labels.map(date => [date, 0]))
  const resolvedCounts = new Map(labels.map(date => [date, 0]))
  tasks.forEach(task => {
    const createdDate = getTaskCreatedDate(task)
    if (createdCounts.has(createdDate)) {
      createdCounts.set(createdDate, createdCounts.get(createdDate) + 1)
    }
    const resolvedDate = getTaskResolvedDate(task)
    if (resolvedCounts.has(resolvedDate)) {
      resolvedCounts.set(resolvedDate, resolvedCounts.get(resolvedDate) + 1)
    }
  })
  return {
    labels,
    created: labels.map(date => createdCounts.get(date) || 0),
    resolved: labels.map(date => resolvedCounts.get(date) || 0)
  }
}
const normalizeStatusLabel = (value) => {
  const status = normalizeStatus(value)
  return taskStatusOptions.value.find(item => item.name === status)?.label || status
}
const analyticsStatusBucket = (statusName) => {
  const normalized = normalizeStatus(statusName)
  if (normalized === 'BACKLOG') return 'backlog'
  if (normalized === 'IN PROGRESS') return 'started'
  if (normalized === 'DONE') return 'completed'
  if (normalized === 'CANCELLED') return 'cancelled'
  return 'unstarted'
}
const getBoardStatusIcon = (value) => taskStatusOptions.value.find(item => item.name === normalizeStatus(value))?.icon || 'fa-regular fa-circle-dashed'
const getStatusColor = (value) => taskStatusOptions.value.find(item => item.name === normalizeStatus(value))?.color || 'var(--color-text-muted)'
const getPriorityIcon = (priority) => {
  const p = normalizePriority(priority)
  if (p === 1 || p === '1') return 'fa-solid fa-angles-up text-red-500'
  if (p === 2 || p === '2') return 'fa-solid fa-chevron-up text-orange-500'
  if (p === 3 || p === '3') return 'fa-solid fa-minus text-blue-500'
  if (p === 4 || p === '4') return 'fa-solid fa-chevron-down text-gray-400'
  return 'fa-solid fa-ban text-gray-500'
}
const getPriorityColor = (priority) => {
  const p = normalizePriority(priority)
  if (p === 1 || p === '1') return '#ef4444' // Vivid Crimson Red
  if (p === 2 || p === '2') return '#f97316' // Vivid Orange
  if (p === 3 || p === '3') return '#2563eb' // Vivid Royal Blue
  if (p === 4 || p === '4') return '#10b981' // Emerald Green
  return '#94a3b8' // Light Gray
}
const getPriorityLabel = (priority) => {
  const p = normalizePriority(priority)
  if (p === 1 || p === '1') return 'Urgent'
  if (p === 2 || p === '2') return 'High'
  if (p === 3 || p === '3') return 'Medium'
  if (p === 4 || p === '4') return 'Low'
  return 'None'
}
const normalizePriority = (value) => {
  const map = { urgent: 1, high: 2, normal: 3, low: 4, none: null }
  return Object.prototype.hasOwnProperty.call(map, normalizeText(value)) ? map[normalizeText(value)] : value
}
const filterValues = (value) => Array.isArray(value) ? value : `${value || ''}`.split(',').map(item => item.trim()).filter(Boolean)
const valuesInclude = (values, target) => values.map(normalizeText).includes(normalizeText(target))
const currentUserId = () => {
  const user = getStoredUserSession()
  return user?.id || user?.userId || null
}
const toggleTaskStar = async (task) => {
  try {
    await starredStore.toggleStar(STARRED_ENTITY_TYPES.WORK_TASK, task.id)
  } catch {
    ElMessage.error(starredStore.error || tr('Could not update starred item.', 'Không thể cập nhật mục gắn sao.'))
  }
}
const isTaskStarred = (taskId) => {
  return store.isTaskStarred(taskId)
}
const currentProjectRole = computed(() => {
  const currentUser = getStoredUserSession()
  const currentUserIdValue = currentUser?.id || currentUser?.userId
  const matchedMember = (projectMembers.value || [])
    .find(member => `${member.userId || member.id || ''}` === `${currentUserIdValue || ''}`)
  const membershipRole = matchedMember?.projectRole || matchedMember?.ProjectRole
  const role = membershipRole
    || project.value?.myRole
    || project.value?.MyRole
    || project.value?.projectRole
    || project.value?.ProjectRole
  return normalizeProjectRole(role)
})
// ────────────────────────────────────────────
// SME Permissions Guard State & Computed Guards
// ────────────────────────────────────────────
const permissionMatrix = ref(getDefaultPermissionMatrix())
const loadProjectPermissionMatrix = async () => {
  const pId = getProjectId()
  if (!pId) return
  try {
    const res = await axiosClient.get(`/settings/ProjectPermissions:${pId}`)
    if (res.data?.data?.rolePermissions) {
      permissionMatrix.value = JSON.parse(res.data.data.rolePermissions)
    } else {
      permissionMatrix.value = getDefaultPermissionMatrix()
    }
  } catch (e) {
    permissionMatrix.value = getDefaultPermissionMatrix()
  }
}
const canCurrentUserCreateTask = computed(() => {
  if (hasSystemAdminAccess(getStoredUserSession())) return true
  return canCreateTask(permissionMatrix.value, currentProjectRole.value)
})
const canCurrentUserUpdateTask = computed(() => {
  if (hasSystemAdminAccess(getStoredUserSession())) return true
  return canUpdateTask(permissionMatrix.value, currentProjectRole.value)
})
const canCurrentUserDeleteTask = computed(() => {
  if (hasSystemAdminAccess(getStoredUserSession())) return true
  return canDeleteTask(permissionMatrix.value, currentProjectRole.value)
})
const canCurrentUserSeeTask = (task) => {
  const rules = projectExecutionRules.value || {}
  const currentUser = getStoredUserSession()
  if (hasSystemAdminAccess(currentUser)) return true
  if (rules.managerAlwaysSeeAllTasks && currentProjectRole.value && visibilityOverrideRoles.includes(currentProjectRole.value)) {
    return true
  }
  const visibilityMode = normalizeProjectRole(task?.visibilityMode || '').replace(/_scoped$/, '') || 'project'
  if (visibilityMode === 'project') return true
  const me = currentUserId()
  const assigneeIds = getTaskAssigneeIds(task)
  if (visibilityMode === 'assigned') {
    return Boolean(
      me && (
        `${task?.assignedUserId || ''}` === `${me}`
        || assigneeIds.some(id => `${id}` === `${me}`)
      )
    )
  }
  if (visibilityMode === 'role') {
    if (!currentProjectRole.value) return false
    return (task?.visibleToRoles || [])
      .map(role => normalizeProjectRole(role))
      .includes(currentProjectRole.value)
  }
  return true
}
const getTaskDate = (task, field) => {
  const value = task[field] || (field === 'startDate' ? task.plannedStartDate : null) || (field === 'dueDate' ? task.dueDate : null)
  if (!value) return null
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)) {
    const [year, month, day] = value.split('-').map(Number)
    return new Date(year, month - 1, day)
  }
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date
}
const taskMatchesSprintScope = (task, sprintId) => {
  if (!sprintId) return true
  if (task.sprintId === sprintId) return true
  if (isSubtask(task)) return false
  return allTasks.value.some(candidate => isSubtask(candidate) && getParentTaskLinkId(candidate) === task.id && candidate.sprintId === sprintId)
}
const taskMatchesCarryOverScope = (task, taskIds) => {
  if (!taskIds.length) return true
  if (taskIds.includes(task.id)) return true
  if (isSubtask(task)) return false
  return allTasks.value.some(candidate => isSubtask(candidate) && getParentTaskLinkId(candidate) === task.id && taskIds.includes(candidate.id))
}
const prioritySortWeight = (priority) => {
  if (priority === 1) return 0
  if (priority === 2) return 1
  if (priority === 3) return 2
  if (priority === 4) return 3
  return 4
}
const toTimestamp = (value) => {
  if (!value) return 0
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? 0 : date.getTime()
}
const sortTasksByDisplayOrder = (tasks) => {
  const items = [...tasks]
  return items.sort((a, b) => {
    const aStarred = store.isTaskStarred(a.id)
    const bStarred = store.isTaskStarred(b.id)
    if (aStarred !== bStarred) {
      return aStarred ? -1 : 1
    }

    const isAsc = sortDirection.value === 'asc'
    const sign = isAsc ? 1 : -1

    if (displayOrder.value === 'created') {
      const diff = toTimestamp(a.createdAt) - toTimestamp(b.createdAt)
      return (diff !== 0 ? diff * sign : 0) || (Number(a.sortOrder) || 0) - (Number(b.sortOrder) || 0)
    }
    if (displayOrder.value === 'updated') {
      const diff = toTimestamp(a.updatedAt || a.createdAt) - toTimestamp(b.updatedAt || b.createdAt)
      return (diff !== 0 ? diff * sign : 0) || (Number(a.sortOrder) || 0) - (Number(b.sortOrder) || 0)
    }
    if (displayOrder.value === 'priority') {
      const diff = prioritySortWeight(a.priority) - prioritySortWeight(b.priority)
      return (diff !== 0 ? diff * sign : 0) || (Number(a.sortOrder) || 0) - (Number(b.sortOrder) || 0)
    }
    if (displayOrder.value === 'dueDate') {
      const dateA = a.dueDate ? new Date(a.dueDate).getTime() : (isAsc ? Infinity : -Infinity)
      const dateB = b.dueDate ? new Date(b.dueDate).getTime() : (isAsc ? Infinity : -Infinity)
      const diff = dateA - dateB
      return (diff !== 0 ? diff * sign : 0) || (Number(a.sortOrder) || 0) - (Number(b.sortOrder) || 0)
    }
    if (displayOrder.value === 'title') {
      const titleA = (a.title || '').trim().toLowerCase()
      const titleB = (b.title || '').trim().toLowerCase()
      const diff = titleA.localeCompare(titleB)
      return (diff !== 0 ? diff * sign : 0) || (Number(a.sortOrder) || 0) - (Number(b.sortOrder) || 0)
    }
    if (displayOrder.value === 'assignee') {
      const assigneeA = (a.assigneeName || '').trim().toLowerCase()
      const assigneeB = (b.assigneeName || '').trim().toLowerCase()
      const diff = assigneeA.localeCompare(assigneeB)
      return (diff !== 0 ? diff * sign : 0) || (Number(a.sortOrder) || 0) - (Number(b.sortOrder) || 0)
    }
    if (displayOrder.value === 'sprint') {
      const sprintA = (a.sprintName || '').trim().toLowerCase()
      const sprintB = (b.sprintName || '').trim().toLowerCase()
      const diff = sprintA.localeCompare(sprintB)
      return (diff !== 0 ? diff * sign : 0) || (Number(a.sortOrder) || 0) - (Number(b.sortOrder) || 0)
    }
    if (displayOrder.value === 'module') {
      const moduleA = (a.moduleName || '').trim().toLowerCase()
      const moduleB = (b.moduleName || '').trim().toLowerCase()
      const diff = moduleA.localeCompare(moduleB)
      return (diff !== 0 ? diff * sign : 0) || (Number(a.sortOrder) || 0) - (Number(b.sortOrder) || 0)
    }
    // Default manual sort order
    return (Number(a.sortOrder) || 0) - (Number(b.sortOrder) || 0)
  })
}
const startOfToday = () => {
  const date = new Date()
  date.setHours(0, 0, 0, 0)
  return date
}
const isThisWeek = (date) => {
  if (!date) return false
  const today = startOfToday()
  const end = new Date(today)
  end.setDate(today.getDate() + 7)
  return date >= today && date <= end
}
const taskMatchesFilter = (task, filter) => {
  const operator = filter.operator || filter.condition || 'is'
  const value = filter.value || filter.displayValue
  const field = filter.field
  if (field === 'status') {
    const left = normalizeStatus(task.statusName)
    const rightValues = filterValues(value).map(normalizeStatus)
    if (operator === 'is not' || operator === 'not in') return !rightValues.includes(left)
    return rightValues.includes(left)
  }
  if (field === 'priority') {
    const left = task.priority || null
    const rightValues = filterValues(value).map(normalizePriority)
    if (operator === 'is not' || operator === 'not in') return !rightValues.includes(left)
    return rightValues.includes(left)
  }
  if (field === 'assignee') {
    const assigneeIds = getTaskAssigneeIds(task)
    if (operator === 'empty') return assigneeIds.length === 0
    if (operator === 'not empty') return assigneeIds.length > 0
    if (normalizeText(value) === 'unassigned') return operator === 'is not' ? assigneeIds.length > 0 : assigneeIds.length === 0
    const assigneeNames = (task.assignees || []).map(item => item.fullName || item.name || item.email)
    const hasMatch = filterValues(value).some(item => assigneeIds.includes(item) || valuesInclude(assigneeNames, item))
    return operator === 'is not' ? !hasMatch : hasMatch
  }
  if (field === 'creator') {
    const creatorIds = [task.reporterId, task.createdById, task.createdBy].filter(Boolean)
    const creatorNames = [task.reporterName, task.createdByName, task.creatorName, task.createdBy?.fullName].filter(Boolean)
    const values = filterValues(value)
    const me = currentUserId()
    const hasMatch = values.some(item => {
      if (normalizeText(item) === 'me') return Boolean(me && creatorIds.includes(me))
      return creatorIds.includes(item) || valuesInclude(creatorNames, item)
    })
    return operator === 'is not' ? !hasMatch : hasMatch
  }
  if (field === 'label') {
    const labelIds = task.labelIds || []
    const labelNames = (task.labels || task.labelNames || []).map(item => item.name || item)
    if (operator === 'empty' || normalizeText(value) === 'no label') return labelIds.length === 0 && labelNames.length === 0
    const hasMatch = filterValues(value).some(item => labelIds.includes(item) || valuesInclude(labelNames, item))
    return operator === 'not includes' || operator === 'not_includes' ? !hasMatch : hasMatch
  }
  if (['startDate', 'dueDate', 'createdAt', 'updatedAt'].includes(field)) {
    const dateField = field === 'startDate' ? 'plannedStartDate' : field
    const date = getTaskDate(task, dateField)
    if (operator === 'empty') return !date
    if (operator === 'overdue') return Boolean(date && date < startOfToday() && normalizeStatus(task.statusName) !== 'DONE')
    if (normalizeText(value) === 'empty') return !date
    if (normalizeText(value) === 'today') return Boolean(date && date.toDateString() === startOfToday().toDateString())
    if (normalizeText(value) === 'this week') return isThisWeek(date)
    return true
  }
  if (field === 'cycle') {
    if (operator === 'empty' || normalizeText(value) === 'no cycle') return !task.sprintId
    const hasMatch = filterValues(value).some(item => task.sprintId === item || normalizeText(task.sprintName) === normalizeText(item))
    return operator === 'is not' ? !hasMatch : hasMatch
  }
  if (field === 'module') {
    if (operator === 'empty' || normalizeText(value) === 'no module') return !task.moduleId && !(task.moduleIds || []).length
    const moduleIds = [task.moduleId, ...(task.moduleIds || []), ...(task.modules || []).map(item => item.id || item.moduleId)].filter(Boolean)
    const moduleNames = [task.moduleName, ...(task.modules || []).map(item => item.name)].filter(Boolean)
    const hasMatch = filterValues(value).some(item => moduleIds.includes(item) || valuesInclude(moduleNames, item))
    return operator === 'is not' ? !hasMatch : hasMatch
  }
  return true
}
let dynamicProjectId = null;
const getProjectId = () => {
    let p = currentProjectId.value || dynamicProjectId;
    return p === 'default' ? null : p;
}
const filteredProjectMembers = computed(() => {
  const keyword = assigneeSearch.value.trim().toLowerCase()
  let filtered = projectMembers.value
  if (keyword) {
    filtered = projectMembers.value.filter(member =>
      `${member.fullName || member.name || member.email || ''}`.toLowerCase().includes(keyword)
    )
  }
  const totalTasks = allTasks.value.length || 1;
  return filtered.map(member => {
    let count = 0;
    allTasks.value.forEach(task => {
      const ids = getTaskAssigneeIds(task);
      if (ids.includes(member.userId || member.id)) {
        count++;
      }
    });
    return {
      ...member,
      taskPercentage: Math.round((count / totalTasks) * 100)
    };
  }).sort((a, b) => a.taskPercentage - b.taskPercentage);
})
const filteredTasksList = computed(() => {
  let filteredTasks = [...visibleTasks.value];
  if (activeModuleFilterId.value) return filteredTasks
  filteredTasks = filteredTasks.filter(matchesTaskFilters)
  if (activeSprintFilterId.value) {
     filteredTasks = filteredTasks.filter(t => taskMatchesSprintScope(t, activeSprintFilterId.value));
  }
  if (carryOverTaskIds.value.length) {
     filteredTasks = filteredTasks.filter(t => taskMatchesCarryOverScope(t, carryOverTaskIds.value));
  }
  if (activeTaskFilters.value.length) {
     filteredTasks = filteredTasks.filter(task => activeTaskFilters.value.every(filter => taskMatchesFilter(task, filter)));
  }
  const shouldIncludeSubtasks = showSubtasks.value
  const scopedTasks = shouldIncludeSubtasks ? filteredTasks : filteredTasks.filter(task => !isSubtask(task))
  return sortTasksByDisplayOrder(scopedTasks)
});
const createdResolvedOptions = computed(() => {
   const buckets = buildAnalyticsDateBuckets(visibleTopLevelTasks.value)
   const colors = analyticsThemeColors.value
   return {
      tooltip: {
        trigger: 'axis',
        backgroundColor: colors.tooltipBg,
        borderColor: colors.tooltipBorder,
        borderWidth: 1,
        textStyle: { color: colors.text }
      },
      legend: { data: [tr('Created', 'Đã tạo'), tr('Resolved', 'Đã xử lý')], bottom: 0, textStyle: { color: colors.muted } },
      grid: { left: '2%', right: '3%', bottom: '16%', top: '10%', containLabel: true },
      xAxis: {
        type: 'category',
        data: buckets.labels.map(formatAnalyticsDateLabel),
        axisLine: { lineStyle: { color: colors.axis } },
        axisLabel: { color: colors.muted }
      },
      yAxis: {
        type: 'value',
        minInterval: 1,
        splitLine: { lineStyle: { color: colors.grid } },
        axisLabel: { color: colors.muted }
      },
      series: [
         {
           name: tr('Created', 'Đã tạo'),
           type: 'line',
           data: buckets.created,
           symbolSize: 8,
           lineStyle: { width: 3, color: '#38BDF8' },
           itemStyle: { color: '#38BDF8' },
           areaStyle: { color: 'rgba(56, 189, 248, 0.12)' },
           smooth: true
         },
         {
           name: tr('Resolved', 'Đã xử lý'),
           type: 'line',
           data: buckets.resolved,
           symbolSize: 8,
           lineStyle: { width: 3, color: '#34D399' },
           itemStyle: { color: '#34D399' },
           areaStyle: { color: 'rgba(52, 211, 153, 0.1)' },
           smooth: true
         }
      ],
      backgroundColor: 'transparent'
   }
});
const analyticsBreakdownRows = computed(() => {
  if (analyticsInsightMode.value === 'assignee') {
    const counts = new Map()
    visibleTopLevelTasks.value.forEach(task => {
      const ids = getTaskAssigneeIds(task)
      if (!ids.length) {
        counts.set('unassigned', (counts.get('unassigned') || 0) + 1)
        return
      }
      ids.forEach(id => counts.set(id, (counts.get(id) || 0) + 1))
    })
    return Array.from(counts.entries())
      .map(([id, count]) => {
        const member = projectMembers.value.find(item => (item.userId || item.id) === id)
        return {
          label: id === 'unassigned' ? tr('Unassigned', 'Chưa giao') : (member?.fullName || member?.name || member?.email || tr('Assignee', 'Người thực hiện')),
          count,
          color: id === 'unassigned' ? 'var(--color-text-muted)' : '#38BDF8'
        }
      })
      .sort((a, b) => b.count - a.count || a.label.localeCompare(b.label))
  }
  if (analyticsInsightMode.value === 'status') {
    return taskStatusOptions.value.map(option => ({
      label: option.label,
      count: visibleTopLevelTasks.value.filter(task => normalizeStatus(task.statusName) === option.name).length,
      color: option.color
    }))
  }
  return [
    { label: tr('Urgent', 'Khẩn cấp'), count: visibleTopLevelTasks.value.filter(task => task.priority === 1).length, color: '#EF4444' },
    { label: tr('High', 'Cao'), count: visibleTopLevelTasks.value.filter(task => task.priority === 2).length, color: '#F97316' },
    { label: tr('Medium', 'Trung bình'), count: visibleTopLevelTasks.value.filter(task => task.priority === 3).length, color: '#3B82F6' },
    { label: tr('Low', 'Thấp'), count: visibleTopLevelTasks.value.filter(task => task.priority === 4).length, color: '#10B981' },
    { label: tr('None', 'Không có'), count: visibleTopLevelTasks.value.filter(task => !task.priority).length, color: 'var(--color-text-muted)' }
  ]
})
const assigneeAnalyticsRows = computed(() => {
  const rows = new Map()
  visibleTopLevelTasks.value.forEach(task => {
    const ids = getTaskAssigneeIds(task)
    const bucket = analyticsStatusBucket(task.statusName)
    const targets = ids.length ? ids : ['unassigned']
    targets.forEach(id => {
      if (!rows.has(id)) {
        const member = projectMembers.value.find(item => (item.userId || item.id) === id)
        rows.set(id, {
          id,
          label: id === 'unassigned' ? tr('Unassigned', 'Chưa giao') : (member?.fullName || member?.name || member?.email || tr('Assignee', 'Người thực hiện')),
          backlog: 0,
          started: 0,
          unstarted: 0,
          completed: 0,
          cancelled: 0,
          total: 0
        })
      }
      const row = rows.get(id)
      row[bucket] += 1
      row.total += 1
    })
  })
  return Array.from(rows.values()).sort((a, b) => b.total - a.total || a.label.localeCompare(b.label))
})
const analyticsInsightLabel = computed(() => {
  if (analyticsInsightMode.value === 'status') return tr('Status Distribution', 'Phân bổ trạng thái')
  if (analyticsInsightMode.value === 'assignee') return tr('Assignee Distribution', 'Phân bổ người thực hiện')
  return tr('Priority Distribution', 'Phân bổ độ ưu tiên')
})
const analyticsTableHeading = computed(() => {
  if (analyticsInsightMode.value === 'status') return tr('Status', 'Trạng thái')
  if (analyticsInsightMode.value === 'assignee') return tr('Assignee', 'Người thực hiện')
  return tr('Priority', 'Độ ưu tiên')
})
const setAnalyticsInsightMode = (mode) => {
  analyticsInsightMode.value = mode
}
const insightChartOptions = computed(() => {
  const colors = analyticsThemeColors.value
  return {
    tooltip: {
      trigger: 'axis',
      backgroundColor: colors.tooltipBg,
      borderColor: colors.tooltipBorder,
      borderWidth: 1,
      textStyle: { color: colors.text }
    },
    grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
    xAxis: {
      type: 'category',
      data: analyticsBreakdownRows.value.map(item => item.label),
      axisLine: { lineStyle: { color: colors.axis } },
      axisLabel: { color: colors.muted }
    },
    yAxis: {
      type: 'value',
      splitLine: { lineStyle: { color: colors.grid } },
      axisLabel: { color: colors.muted }
    },
    series: [
      {
        type: 'bar',
        barWidth: '30%',
        data: analyticsBreakdownRows.value.map(item => ({
          value: item.count,
          itemStyle: { color: item.color, borderRadius: [4, 4, 0, 0] }
        }))
      }
    ],
    backgroundColor: 'transparent'
  }
})
const kanbanColumns = computed(() => {
  // Map màu nền nhạt cho từng trạng thái (theo design spec)
  const statusBgMap = {
    'BACKLOG':     'rgba(148, 163, 184, 0.05)',
    'TO DO':       'rgba(167, 139, 250, 0.06)',
    'IN PROGRESS': 'rgba(56, 189, 248, 0.06)',
    'IN REVIEW':   'rgba(245, 158, 11, 0.06)',
    'DONE':        'rgba(34, 197, 94, 0.05)',
    'CANCELLED':   'rgba(244, 63, 94, 0.05)'
  }
  const validTasks = filteredTasksList.value || [];
  if (groupBy.value === 'priority') {
    const pGroups = [
      { id: 'p1', name: 'URGENT', label: tr('Urgent', 'Khẩn cấp'), color: '#EF4444', icon: 'fa-solid fa-angles-up', bgColor: 'rgba(239,68,68,0.05)', priorityValue: 1, items: [] },
      { id: 'p2', name: 'HIGH', label: tr('High', 'Cao'), color: '#F97316', icon: 'fa-solid fa-chevron-up', bgColor: 'rgba(249,115,22,0.05)', priorityValue: 2, items: [] },
      { id: 'p3', name: 'NORMAL', label: tr('Normal', 'Bình thường'), color: '#3B82F6', icon: 'fa-solid fa-minus', bgColor: 'rgba(59,130,246,0.05)', priorityValue: 3, items: [] },
      { id: 'p4', name: 'LOW', label: tr('Low', 'Thấp'), color: '#94A3B8', icon: 'fa-solid fa-chevron-down', bgColor: 'rgba(148,163,184,0.05)', priorityValue: 4, items: [] }
    ];
    validTasks.forEach(t => {
      let col = pGroups.find(g => g.priorityValue === (t.priority || 3));
      if (!col) col = pGroups[2];
      col.items.push(t);
    });
    return pGroups;
  }
  if (groupBy.value === 'assignee') {
    const aGroups = (projectMembers.value || []).map(member => ({
      id: member.userId || member.id,
      name: member.userId || member.id,
      label: member.fullName || member.name || member.email,
      color: '#3B82F6',
      icon: 'fa-regular fa-user',
      bgColor: 'rgba(59,130,246,0.04)',
      priorityValue: null,
      assigneeId: member.userId || member.id,
      items: []
    }));
    aGroups.push({
      id: 'unassigned',
      name: 'UNASSIGNED',
      label: tr('Unassigned', 'Chưa phân công'),
      color: '#94A3B8',
      icon: 'fa-solid fa-user-xmark',
      bgColor: 'rgba(148,163,184,0.04)',
      priorityValue: null,
      assigneeId: 'unassigned',
      items: []
    });
    validTasks.forEach(t => {
      const ids = getTaskAssigneeIds(t);
      if (ids && ids.length > 0) {
        let col = aGroups.find(g => g.assigneeId === ids[0]);
        if (col) {
          col.items.push(t);
        } else {
          aGroups[aGroups.length - 1].items.push(t);
        }
      } else {
        aGroups[aGroups.length - 1].items.push(t);
      }
    });
    return aGroups;
  }
  if (groupBy.value === 'sprint') {
    const sMap = new Map();
    validTasks.forEach(t => {
      if (t.sprintId) {
        sMap.set(t.sprintId, t.sprintName || `Sprint ${t.sprintId.substring(0,8).toUpperCase()}`);
      }
    });
    const sGroups = Array.from(sMap.entries()).map(([sid, sname]) => ({
      id: sid,
      name: sid,
      label: sname,
      color: '#10B981',
      icon: 'fa-solid fa-arrows-spin',
      bgColor: 'rgba(16,185,129,0.04)',
      priorityValue: null,
      sprintId: sid,
      items: []
    }));
    sGroups.push({
      id: 'no-sprint',
      name: 'NO_SPRINT',
      label: tr('No Sprint', 'Chưa có chu kỳ'),
      color: '#94A3B8',
      icon: 'fa-solid fa-ban',
      bgColor: 'rgba(148,163,184,0.04)',
      priorityValue: null,
      sprintId: 'no-sprint',
      items: []
    });
    validTasks.forEach(t => {
      if (t.sprintId) {
        let col = sGroups.find(g => g.sprintId === t.sprintId);
        if (col) {
          col.items.push(t);
        } else {
          sGroups[sGroups.length - 1].items.push(t);
        }
      } else {
        sGroups[sGroups.length - 1].items.push(t);
      }
    });
    return sGroups;
  }
  if (groupBy.value === 'module') {
    const mMap = new Map();
    validTasks.forEach(t => {
      if (t.moduleId) {
        mMap.set(t.moduleId, t.moduleName || `Module ${t.moduleId.substring(0,8).toUpperCase()}`);
      }
    });
    const mGroups = Array.from(mMap.entries()).map(([mid, mname]) => ({
      id: mid,
      name: mid,
      label: mname,
      color: '#8B5CF6',
      icon: 'fa-solid fa-cubes',
      bgColor: 'rgba(139,92,246,0.04)',
      priorityValue: null,
      moduleId: mid,
      items: []
    }));
    mGroups.push({
      id: 'no-module',
      name: 'NO_MODULE',
      label: tr('No Module', 'Chưa có phân hệ'),
      color: '#94A3B8',
      icon: 'fa-solid fa-ban',
      bgColor: 'rgba(148,163,184,0.04)',
      priorityValue: null,
      moduleId: 'no-module',
      items: []
    });
    validTasks.forEach(t => {
      if (t.moduleId) {
        let col = mGroups.find(g => g.moduleId === t.moduleId);
        if (col) {
          col.items.push(t);
        } else {
          mGroups[mGroups.length - 1].items.push(t);
        }
      } else {
        mGroups[mGroups.length - 1].items.push(t);
      }
    });
    return mGroups;
  }
  // Fallback / default Group by Status
  const groups = taskStatusOptions.value.map((status, index) => ({
    id: `${status.name.toLowerCase().replace(/\s+/g, '-')}-${index}`,
    name: status.name,
    label: status.label || status.name,
    color: status.color,
    icon: status.icon,
    bgColor: statusBgMap[status.name] || 'rgba(148, 163, 184, 0.04)',
    priorityValue: null,
    items: []
  }));
  const definedStatuses = taskStatusOptions.value.map(s => s.name.toUpperCase().trim())
  const hasFallback = validTasks.some(t => !definedStatuses.includes((t.statusName || 'BACKLOG').toUpperCase().trim()))
  if (hasFallback) {
    groups.push({
      id: 'fallback-unclassified-col',
      name: 'FALLBACK_UNCLASSIFIED',
      label: tr('Khác / Chưa phân loại', 'Khác / Chưa phân loại'),
      color: '#f43f5e',
      icon: 'fa-solid fa-triangle-exclamation',
      bgColor: 'rgba(244, 63, 94, 0.05)',
      priorityValue: null,
      items: [],
      isFallback: true
    })
  }
  validTasks.forEach(t => {
    const s = (t.statusName || 'BACKLOG').toUpperCase().trim();
    let col = groups.find(group => group.name === s)
    if (!col) {
      col = groups.find(group => group.name === 'FALLBACK_UNCLASSIFIED') || groups[0];
    }
    col.items.push(t);
  });
  return groups;
})
const listViewGroups = computed(() => {
  if (groupBy.value === 'priority') {
    const pGroups = [
      { id: 'lp1', name: tr('Urgent', 'Khẩn cấp'), statusName: 'URGENT', icon: 'fa-solid fa-angles-up', color: '#EF4444', priorityValue: 1, items: [] },
      { id: 'lp2', name: tr('High', 'Cao'), statusName: 'HIGH', icon: 'fa-solid fa-chevron-up', color: '#F97316', priorityValue: 2, items: [] },
      { id: 'lp3', name: tr('Normal', 'Bình thường'), statusName: 'NORMAL', icon: 'fa-solid fa-minus', color: '#3B82F6', priorityValue: 3, items: [] },
      { id: 'lp4', name: tr('Low', 'Thấp'), statusName: 'LOW', icon: 'fa-solid fa-chevron-down', color: '#94A3B8', priorityValue: 4, items: [] }
    ];
    filteredTasksList.value.forEach(task => {
      let target = pGroups.find(g => g.priorityValue === (task.priority || 3));
      if (!target) target = pGroups[2];
      target.items.push(task);
    });
    return pGroups;
  }
  if (groupBy.value === 'assignee') {
    const aGroups = (projectMembers.value || []).map(member => ({
      id: member.userId || member.id,
      name: member.fullName || member.name || member.email,
      statusName: member.userId || member.id,
      icon: 'fa-regular fa-user',
      color: '#3B82F6',
      assigneeId: member.userId || member.id,
      items: []
    }));
    aGroups.push({
      id: 'unassigned',
      name: tr('Unassigned', 'Chưa phân công'),
      statusName: 'UNASSIGNED',
      icon: 'fa-solid fa-user-xmark',
      color: '#94A3B8',
      assigneeId: 'unassigned',
      items: []
    });
    filteredTasksList.value.forEach(task => {
      const ids = getTaskAssigneeIds(task);
      if (ids && ids.length > 0) {
        let target = aGroups.find(g => g.assigneeId === ids[0]);
        if (target) {
          target.items.push(task);
        } else {
          aGroups[aGroups.length - 1].items.push(task);
        }
      } else {
        aGroups[aGroups.length - 1].items.push(task);
      }
    });
    return aGroups;
  }
  if (groupBy.value === 'sprint') {
    const sMap = new Map();
    filteredTasksList.value.forEach(task => {
      if (task.sprintId) {
        sMap.set(task.sprintId, task.sprintName || `Sprint ${task.sprintId.substring(0,8).toUpperCase()}`);
      }
    });
    const sGroups = Array.from(sMap.entries()).map(([sid, sname]) => ({
      id: sid,
      name: sname,
      statusName: sid,
      icon: 'fa-solid fa-arrows-spin',
      color: '#10B981',
      sprintId: sid,
      items: []
    }));
    sGroups.push({
      id: 'no-sprint',
      name: tr('No Sprint', 'Chưa có chu kỳ'),
      statusName: 'NO_SPRINT',
      icon: 'fa-solid fa-ban',
      color: '#94A3B8',
      sprintId: 'no-sprint',
      items: []
    });
    filteredTasksList.value.forEach(task => {
      if (task.sprintId) {
        let target = sGroups.find(g => g.sprintId === task.sprintId);
        if (target) {
          target.items.push(task);
        } else {
          sGroups[sGroups.length - 1].items.push(task);
        }
      } else {
        sGroups[sGroups.length - 1].items.push(task);
      }
    });
    return sGroups;
  }
  if (groupBy.value === 'module') {
    const mMap = new Map();
    filteredTasksList.value.forEach(task => {
      if (task.moduleId) {
        mMap.set(task.moduleId, task.moduleName || `Module ${task.moduleId.substring(0,8).toUpperCase()}`);
      }
    });
    const mGroups = Array.from(mMap.entries()).map(([mid, mname]) => ({
      id: mid,
      name: mname,
      statusName: mid,
      icon: 'fa-solid fa-cubes',
      color: '#8B5CF6',
      moduleId: mid,
      items: []
    }));
    mGroups.push({
      id: 'no-module',
      name: tr('No Module', 'Chưa có phân hệ'),
      statusName: 'NO_MODULE',
      icon: 'fa-solid fa-ban',
      color: '#94A3B8',
      moduleId: 'no-module',
      items: []
    });
    filteredTasksList.value.forEach(task => {
      if (task.moduleId) {
        let target = mGroups.find(g => g.moduleId === task.moduleId);
        if (target) {
          target.items.push(task);
        } else {
          mGroups[mGroups.length - 1].items.push(task);
        }
      } else {
        mGroups[mGroups.length - 1].items.push(task);
      }
    });
    return mGroups;
  }
  // Fallback / default Group by Status
  const groups = taskStatusOptions.value.map((status, index) => ({
    id: status.name.toLowerCase().replace(/\s+/g, '-') + '-' + index,
    name: status.label,
    statusName: status.name,
    icon: status.icon,
    color: status.color,
    items: []
  }))
  filteredTasksList.value.forEach(task => {
    const status = normalizeStatus(task.statusName)
    const target = groups.find(group => group.statusName === status) || groups[0]
    target.items.push(task)
  })
  return groups;
})
const toggleListGroup = (groupId) => {
  collapsedListGroups.value[groupId] = !collapsedListGroups.value[groupId]
}
const toggleTaskAssignee = (task, memberId) => {
  if (!canEditTaskByAssignment(task)) return
  const currentIds = getTaskAssigneeIds(task)
  const nextIds = currentIds.includes(memberId)
    ? currentIds.filter(id => id !== memberId)
    : Array.from(new Set([...currentIds, memberId]))
  task.assigneeIds = nextIds
  task.assignedUserId = nextIds[0] || null
  updateTask(task, 'assigneeIds', nextIds, currentIds)
}
const loadInitialData = async (options = {}) => {
  const { preserveExisting = false } = options
  let pid = getProjectId()
  if(!pid) {
      rawTasks.value = []
      allTasks.value = []
      store.clearTasks(null)
      return
  }
  const requestId = ++initialDataRequestId
  const contextKey = `${getSessionIdentity()}:${pid}:${activeModuleFilterId.value || ''}`
  try {
    setScopedCurrentProjectId(pid)
    showSubtasks.value = loadShowSubtasksPreference(pid)
    displayProperties.value = loadDisplayPropertiesPreference(pid)
    if (!preserveExisting) {
      rawTasks.value = []
      allTasks.value = []
      store.clearTasks(pid)
      selectedTask.value = null
      projectMembers.value = []
      project.value = {}
      carryOverTaskIds.value = []
    }
    const [pRes, mRes, statusesRes, executionRulesRes] = await Promise.all([
      axiosClient.get(`/projects/${pid}`),
      axiosClient.get(`/projects/${pid}/members`),
      axiosClient.get(`/projects/${pid}/task-statuses`).catch(() => ({ data: { data: [] } })),
      axiosClient.get(`/projects/${pid}/execution-rules`).catch(() => ({ data: { data: {} } }))
    ])
    const currentContextKey = `${getSessionIdentity()}:${getProjectId()}:${activeModuleFilterId.value || ''}`
    if (requestId !== initialDataRequestId || contextKey !== currentContextKey) return
    project.value = pRes.data.data
    projectMembers.value = (mRes.data.data || []).map(member => ({
      ...member,
      userId: member.userId || member.id,
      fullName: member.fullName || member.name || member.email,
      projectRole: member.projectRole || member.ProjectRole || member.myRole || member.MyRole || ''
    }))
    projectStatuses.value = (statusesRes.data?.data || []).map((status) => ({
      ...status,
      name: normalizeStatus(status.name),
      displayName: status.displayName || status.name,
      colorCode: status.colorCode || ''
    }))
    projectExecutionRules.value = {
      enableRoleBasedTaskVisibility: Boolean(executionRulesRes.data?.data?.enableRoleBasedTaskVisibility),
      managerAlwaysSeeAllTasks: executionRulesRes.data?.data?.managerAlwaysSeeAllTasks !== false
    }
    if (activeCarryOverSprintId.value) {
      const carryOverRes = await axiosClient.get(`/projects/${pid}/sprints/${activeCarryOverSprintId.value}/carry-over-tasks`)
      const latestContextKey = `${getSessionIdentity()}:${getProjectId()}:${activeModuleFilterId.value || ''}`
      if (requestId !== initialDataRequestId || contextKey !== latestContextKey) return
      carryOverTaskIds.value = (carryOverRes.data?.data || []).map(task => task.id)
    }
    await fetchTasks({ reset: false })
    openTaskFromRouteQuery()
    await loadProjectPermissionMatrix()
  } catch (error) {
    if (requestId !== initialDataRequestId) return
    if (isForbiddenError(error)) {
      isForbidden.value = true
    } else {
      console.error('Lỗi load dự án:', error)
    }
  }
}
const fetchModuleTasks = async ({ page = moduleTaskPage.value } = {}) => {
  const pid = getProjectId()
  const moduleId = activeModuleFilterId.value
  if (!pid || !moduleId) return []
  moduleDetailAbortController?.abort()
  const controller = new AbortController()
  moduleDetailAbortController = controller
  const requestId = ++moduleDetailRequestId
  const sessionIdentity = getSessionIdentity()
  const contextKey = `${sessionIdentity}:${pid}:${moduleId}:${page}:${moduleTaskPageSize.value}`
  moduleDetailLoading.value = true
  moduleDetailError.value = null
  allTasks.value = []
  selectedTask.value = null
  try {
    const detail = await getModuleDetail(pid, moduleId, {
      page,
      pageSize: moduleTaskPageSize.value,
      signal: controller.signal
    })
    const currentContextKey = `${getSessionIdentity()}:${getProjectId()}:${activeModuleFilterId.value}:${page}:${moduleTaskPageSize.value}`
    if (requestId !== moduleDetailRequestId || contextKey !== currentContextKey) {
      return []
    }
    if (detail.tasks.totalPages > 0 && page > detail.tasks.totalPages) {
      moduleTaskPage.value = detail.tasks.totalPages
      return fetchModuleTasks({ page: detail.tasks.totalPages })
    }
    moduleDetail.value = detail
    moduleTaskPage.value = detail.tasks.page
    moduleTaskPagination.value = { ...detail.tasks }
    allTasks.value = detail.tasks.items.map(task => store.normalizeTaskRecord({
      ...task,
      moduleId: detail.id,
      moduleName: detail.name,
      projectId: detail.projectId
    }, detail.projectId))
    return allTasks.value
  } catch (error) {
    if (error?.name === 'CanceledError' || error?.name === 'AbortError' || error?.code === 'ERR_CANCELED') {
      return []
    }
    if (requestId === moduleDetailRequestId) {
      moduleDetailError.value = {
        status: getModuleErrorStatus(error),
        message: getModuleErrorMessage(error)
      }
      allTasks.value = []
    }
    return []
  } finally {
    if (requestId === moduleDetailRequestId) {
      moduleDetailLoading.value = false
    }
  }
}
const retryModuleDetail = () => fetchModuleTasks({ page: moduleTaskPage.value })
const changeModuleTaskPage = (page) => {
  if (moduleDetailLoading.value || page < 1 || page === moduleTaskPage.value) return
  moduleTaskPage.value = page
  fetchModuleTasks({ page })
}
const changeModuleTaskPageSize = (pageSize) => {
  const normalized = Math.min(100, Math.max(1, Number(pageSize) || 20))
  if (moduleDetailLoading.value || normalized === moduleTaskPageSize.value) return
  moduleTaskPageSize.value = normalized
  moduleTaskPage.value = 1
  fetchModuleTasks({ page: 1 })
}
const fetchTasks = async (options = {}) => {
  const pid = getProjectId()
  if(!pid) return
  if (activeModuleFilterId.value) {
      return fetchModuleTasks({ page: moduleTaskPage.value })
  }
  clearModuleDetailState()
  try {
      const previousTasks = options.preserveExisting ? [...(allTasks.value || [])] : []
      const tasks = await store.fetchTasks(pid, options);
      const fetchedTasks = Array.isArray(tasks) ? tasks : []
      allTasks.value = options.preserveExisting
        ? [...previousTasks, ...fetchedTasks].reduce((items, task) => {
            const normalizedTask = store.normalizeTaskRecord(task, pid)
            if (!normalizedTask?.id || `${normalizedTask.projectId || pid}` !== `${pid}`) return items
            const index = items.findIndex(item => `${item.id}` === `${normalizedTask.id}`)
            if (index >= 0) items.splice(index, 1, { ...items[index], ...normalizedTask })
            else items.push(normalizedTask)
            return items
          }, [])
        : fetchedTasks
      if (options.preserveExisting) {
        store.tasks = allTasks.value
      }
      // Auto update selectedTask if open
      if (selectedTask.value) {
        const updatedTask = allTasks.value.find(t => t.id === selectedTask.value.id);
        if (updatedTask && canCurrentUserSeeTask(updatedTask)) selectedTask.value = updatedTask;
        else if (!updatedTask || !canCurrentUserSeeTask(selectedTask.value)) selectedTask.value = null;
      }
  } catch(error) {
    console.error('Lỗi load tasks:', error)
  }
}
const upsertTaskIntoCurrentList = (task) => {
  const pid = getProjectId()
  if (!pid || !task) return null
  const normalizedTask = store.normalizeTaskRecord(task, pid)
  if (!normalizedTask?.id || `${normalizedTask.projectId || pid}` !== `${pid}`) return null
  const nextTasks = [...(allTasks.value || [])]
  const index = nextTasks.findIndex(item => `${item.id}` === `${normalizedTask.id}`)
  if (index >= 0) {
    nextTasks.splice(index, 1, { ...nextTasks[index], ...normalizedTask })
  } else {
    nextTasks.push(normalizedTask)
  }
  allTasks.value = nextTasks
  store.upsertTask(normalizedTask, pid)
  return normalizedTask
}
const handleTaskCreated = async (createdTask) => {
  upsertTaskIntoCurrentList(createdTask)
  await fetchTasks({ reset: false, preserveExisting: true })
}
const openTaskDetail = (task) => {
  taskDetailHistory.value = []
  selectedTask.value = task;
  starredStore.recordViewed(STARRED_ENTITY_TYPES.WORK_TASK, task.id).catch(() => {})
}
const openTaskFromRouteQuery = () => {
  const taskId = route.query.task
  if (!taskId) return
  const task = allTasks.value.find(item => `${item.id}` === `${taskId}`)
  if (task) openTaskDetail(task)
}
const openTaskDetailFromModal = (task, options = {}) => {
  const previousTask = options?.fromTask || selectedTask.value
  if (previousTask?.id && previousTask.id !== task?.id) {
    const cachedPrevious = allTasks.value.find(item => item.id === previousTask.id) || previousTask
    taskDetailHistory.value = [...taskDetailHistory.value, cachedPrevious]
  }
  selectedTask.value = task
  starredStore.recordViewed(STARRED_ENTITY_TYPES.WORK_TASK, task.id).catch(() => {})
}
const goBackTaskDetail = () => {
  const history = [...taskDetailHistory.value]
  const previousTask = history.pop()
  if (!previousTask) return
  taskDetailHistory.value = history
  selectedTask.value = allTasks.value.find(item => item.id === previousTask.id) || previousTask
}
const closeTaskDetail = () => {
  taskDetailHistory.value = []
  selectedTask.value = null;
}
const putBackedTaskFields = new Set([
  'title',
  'description',
  'priority',
  'assignedUserId',
  'sprintId',
  'taskTypeId',
  'totalEstimatedHours',
  'visibilityMode',
  'visibleToRoles'
])
const buildPutTaskPayload = (task, overrides = {}) => {
  const mergedTask = { ...task, ...overrides }
  return {
    title: mergedTask.title || '',
    description: mergedTask.description ?? '',
    priority: mergedTask.priority ?? 0,
    storyPoints: mergedTask.storyPoints ?? 0,
    assignedUserId: mergedTask.assignedUserId ?? mergedTask.assigneeId ?? null,
    plannedStartDate: normalizeDateOnly(mergedTask.plannedStartDate) || null,
    plannedEndDate: normalizeDateOnly(mergedTask.plannedEndDate) || null,
    dueDate: normalizeDateOnly(mergedTask.dueDate) || null,
    sprintId: mergedTask.sprintId || null,
    taskTypeId: mergedTask.taskTypeId || '00000000-0000-0000-0000-000000000000',
    totalEstimatedHours: Number(mergedTask.totalEstimatedHours || 0),
    visibilityMode: mergedTask.visibilityMode || 'project',
    visibleToRoles: Array.isArray(mergedTask.visibleToRoles) ? mergedTask.visibleToRoles : [],
    rowVersion: mergedTask.rowVersion || null
  }
}
const syncTopLevelTasks = () => {
  rawTasks.value = (allTasks.value || []).filter(task => !isSubtask(task))
}
watch(allTasks, syncTopLevelTasks, { deep: true, immediate: true })
watch(
  () => ({
    enableRoleBasedTaskVisibility: Boolean(projectExecutionRules.value?.enableRoleBasedTaskVisibility),
    managerAlwaysSeeAllTasks: Boolean(projectExecutionRules.value?.managerAlwaysSeeAllTasks)
  }),
  async (rules) => {
    allTasks.value = (allTasks.value || []).filter(canCurrentUserSeeTask)
    if (selectedTask.value && !canCurrentUserSeeTask(selectedTask.value)) {
      selectedTask.value = null
    }
    await fetchTasks({ reset: false })
  },
  { deep: true }
)
const updateTask = async (task, field, value, previousValue = task ? task[field] : undefined) => {
  const pid = getProjectId()
  if (!pid || !task?.id) return
  const isBatchPayload = field && typeof field === 'object' && !Array.isArray(field)
  const payloadOverrides = isBatchPayload ? field : { [field]: value }
  if (!canApplyTaskUpdate(task, payloadOverrides)) return
  const previousValues = Object.fromEntries(
    Object.keys(payloadOverrides).map(key => [key, task?.[key]])
  )
  try {
    Object.entries(payloadOverrides).forEach(([key, nextValue]) => {
      task[key] = nextValue
    })
    const usesPutUpdate = !isBatchPayload && putBackedTaskFields.has(field)
    const payload = usesPutUpdate
      ? buildPutTaskPayload(task, payloadOverrides)
      : payloadOverrides
    await store.updateTask(pid, task.id, payload, { method: usesPutUpdate ? 'put' : 'patch' })
    await fetchTasks()
  } catch (error) {
    console.error('Failed to update task:', error)
    if (task) {
      Object.entries(previousValues).forEach(([key, rollbackValue]) => {
        task[key] = rollbackValue
      })
    }
    ElMessage.error(error.response?.data?.message || 'Khong the cap nhat cong viec')
    await fetchTasks()
  }
}
const openCreateTask = (statusName, defaults = {}) => {
   taskDetailHistory.value = []
   let defaultStatus = 'TO DO'
   let defaultPriority = 3
   let defaultAssigneeIds = []
   let defaultSprintId = activeSprintFilterId.value || null
   let defaultModuleId = activeModuleFilterId.value || null
   if (groupBy.value === 'priority') {
     if (statusName === 'URGENT') defaultPriority = 1
     else if (statusName === 'HIGH') defaultPriority = 2
     else if (statusName === 'NORMAL') defaultPriority = 3
     else if (statusName === 'LOW') defaultPriority = 4
     else defaultPriority = 3
   } else if (groupBy.value === 'assignee') {
     if (statusName && statusName !== 'UNASSIGNED') {
       defaultAssigneeIds = [statusName]
     }
   } else if (groupBy.value === 'sprint') {
     if (statusName && statusName !== 'NO_SPRINT') {
       defaultSprintId = statusName
     }
   } else if (groupBy.value === 'module') {
     if (statusName && statusName !== 'NO_MODULE') {
       defaultModuleId = statusName
     }
   } else {
     defaultStatus = statusName || 'BACKLOG'
   }
   selectedTask.value = {
     isNew: true,
     title: '',
     description: '',
     statusName: defaultStatus,
     priority: defaultPriority,
     sprintId: defaultSprintId,
     moduleId: defaultModuleId,
     plannedStartDate: defaults?.plannedStartDate || null,
     dueDate: defaults?.dueDate || null,
     assigneeIds: defaultAssigneeIds
   };
}
const handleGlobalCreateTask = (e) => {
  openCreateTask(e.detail?.statusName || 'TO DO')
}
onMounted(() => {
  window.addEventListener('open-create-task', handleGlobalCreateTask)
})
onUnmounted(() => {
  window.removeEventListener('open-create-task', handleGlobalCreateTask)
})
const toggleAnalyticsExpand = () => {
  isAnalyticsExpanded.value = !isAnalyticsExpanded.value
}
const closeAnalyticsSidebar = () => {
  showAnalyticsSidebar.value = false
  isAnalyticsExpanded.value = false
}
const openCreateTaskFromCalendar = (dates) => {
   openCreateTask('TO DO', dates);
}
const inlineInput = ref(null);
const openInlineCreate = (colId) => {
   inlineCreateColId.value = colId;
   inlineTaskTitle.value = '';
   inlineDueDate.value = '';
   inlineAssigneeIds.value = [];
   inlineDateRange.value = [];
   const targetCol = kanbanColumns.value.find(c => c.id === colId);
   inlineStatusName.value = targetCol ? (targetCol.name === 'FALLBACK_UNCLASSIFIED' ? 'BACKLOG' : targetCol.name) : 'BACKLOG';
   inlinePriority.value = 0;
   nextTick(() => {
     const targetColumn = document.querySelector('.col-body.is-creating')
     if(inlineInput.value) {
        // inlineInput.value could be an array if inside v-for, or a proxy. We handle both:
        if (Array.isArray(inlineInput.value)) {
           inlineInput.value[0]?.focus();
        } else {
           inlineInput.value.focus();
        }
     }
     // Focus can scroll the input into view before the full editor is laid out.
     // Scroll again after layout so the complete create form remains visible.
     const scrollEditorIntoView = () => {
       if (!targetColumn) return;
       const editor = targetColumn.querySelector('.inline-create-box');
       if (!editor) return;
       const columnRect = targetColumn.getBoundingClientRect();
       const editorRect = editor.getBoundingClientRect();
       const hiddenBelow = editorRect.bottom - columnRect.bottom;
       if (hiddenBelow > 0) targetColumn.scrollTop += hiddenBelow + 8;
       targetColumn.scrollTop = Math.max(0, targetColumn.scrollHeight - targetColumn.clientHeight);
     };
     scrollEditorIntoView();
     requestAnimationFrame(() => {
       scrollEditorIntoView();
       requestAnimationFrame(scrollEditorIntoView);
       window.setTimeout(scrollEditorIntoView, 120);
     });
     const editor = targetColumn?.querySelector('.inline-create-box');
     if (targetColumn && editor && typeof ResizeObserver !== 'undefined') {
       const resizeObserver = new ResizeObserver(scrollEditorIntoView);
       resizeObserver.observe(editor);
       window.setTimeout(() => resizeObserver.disconnect(), 600);
     }
   });
}
const submitInlineTask = async (col) => {
   if(!inlineTaskTitle.value.trim()) {
      inlineCreateColId.value = null;
      return;
   }
   try {
      let defaultStatus = 'TO DO'
      let defaultPriority = 3
      let defaultAssigneeIds = []
      let defaultSprintId = activeSprintFilterId.value || null
      let defaultModuleId = activeModuleFilterId.value || null
      if (groupBy.value === 'priority') {
         defaultStatus = 'TO DO'
         defaultPriority = col?.priorityValue || 3
      } else if (groupBy.value === 'assignee') {
         defaultStatus = 'TO DO'
         if (col?.assigneeId && col.assigneeId !== 'unassigned') {
            defaultAssigneeIds = [col.assigneeId]
         }
      } else if (groupBy.value === 'sprint') {
         defaultStatus = 'TO DO'
         if (col?.sprintId && col.sprintId !== 'no-sprint') {
            defaultSprintId = col.sprintId
         }
      } else if (groupBy.value === 'module') {
         defaultStatus = 'TO DO'
         if (col?.moduleId && col.moduleId !== 'no-module') {
            defaultModuleId = col.moduleId
         }
      } else {
         defaultStatus = inlineStatusName.value || col?.name || 'BACKLOG'
         defaultPriority = Number.isFinite(Number(inlinePriority.value)) ? Number(inlinePriority.value) : 0
      }
      const payload = {
         title: inlineTaskTitle.value.trim(),
         description: '',
         statusName: defaultStatus,
         priority: defaultPriority,
         sprintId: defaultSprintId,
         moduleId: defaultModuleId,
         assigneeIds: defaultAssigneeIds
      }
      if (inlineDateRange.value && Array.isArray(inlineDateRange.value) && inlineDateRange.value.length === 2) {
         payload.plannedStartDate = inlineDateRange.value[0];
         payload.plannedEndDate = inlineDateRange.value[1];
         payload.dueDate = inlineDateRange.value[1];
      } else if (inlineDueDate.value) {
         payload.dueDate = inlineDueDate.value;
      }
      if (inlineAssigneeIds.value.length && !defaultAssigneeIds.length) {
         payload.assigneeIds = inlineAssigneeIds.value
      }
      const response = await axiosClient.post(`/projects/${getProjectId()}/WorkTasks`, payload);
      upsertTaskIntoCurrentList(response.data?.data || response.data);
      inlineTaskTitle.value = '';
      inlineDueDate.value = '';
      inlineDateRange.value = [];
      inlineAssigneeIds.value = [];
      inlineCreateColId.value = null;
      fetchTasks({ reset: false, preserveExisting: true });
      ElMessage.success('Đã tạo công việc thành công.');
   } catch (e) {
      console.error(e);
      ElMessage.error(e.response?.data?.message || 'Không thể tạo công việc.');
   }
}
const handleListTaskCreate = async (payload) => {
   const pid = getProjectId();
   if (!pid) return;
   try {
      const response = await axiosClient.post(`/projects/${pid}/WorkTasks`, {
         title: payload.title,
         description: '',
         statusName: payload.statusName || 'BACKLOG',
          priority: payload.priority || 3,
          sprintId: activeSprintFilterId.value || null
      });
      upsertTaskIntoCurrentList(response.data?.data || response.data);
      fetchTasks({ reset: false, preserveExisting: true });
   } catch (error) {
      console.error(error);
      ElMessage.error(error.response?.data?.message || 'Khong the tao cong viec');
   }
}
const handleDraggableChange = async (evt, group) => {
  if (evt.added || evt.moved) {
    const element = evt.added ? evt.added.element : evt.moved.element;
    if (!canEditTaskByAssignment(element)) {
      await fetchTasks()
      return
    }
    const newIndex = evt.added ? evt.added.newIndex : evt.moved.newIndex;
    const previousTask = { ...element };
    const getSortOrder = (task, fallback) => {
      const sortOrder = Number(task?.sortOrder);
      return Number.isFinite(sortOrder) ? sortOrder : fallback;
    };
    if (group.name === 'FALLBACK_UNCLASSIFIED') {
      ElMessage.warning('Không thể chuyển tác vụ vào cột Khác / Chưa phân loại.');
      fetchTasks();
      return;
    }
    // Math cho LexoRank
    let newSortOrder = 65536;
    if (group.items.length === 1) {
       newSortOrder = 65536;
    } else if (newIndex === 0) {
       newSortOrder = getSortOrder(group.items[1], 131072) / 2.0;
    } else if (newIndex === group.items.length - 1) {
       newSortOrder = getSortOrder(group.items[group.items.length - 2], 0) + 65536;
    } else {
       const beforeSort = getSortOrder(group.items[newIndex - 1], 0);
       const afterSort = getSortOrder(group.items[newIndex + 1], beforeSort + 131072);
       newSortOrder = (beforeSort + afterSort) / 2.0;
     }
     element.sortOrder = newSortOrder;
     if (groupBy.value === 'status') {
        element.statusName = group.name; // Cập nhật Optimistic UI
        try {
          await store.reorderTask(getProjectId(), element.id, newSortOrder, group.name);
          await fetchTasks();
        } catch (error) {
          Object.assign(element, previousTask);
          ElMessage.error(error.response?.data?.message || 'Khong the cap nhat bang Kanban');
          console.error('Lỗi API reorder:', error);
          fetchTasks(); // Load lại data nếu gặp lỗi
        }
     } else if (groupBy.value === 'priority') {
        element.priority = group.priorityValue;
        try {
          await store.updateTask(getProjectId(), element.id, {
            sortOrder: newSortOrder,
            priority: group.priorityValue
           });
           await fetchTasks();
         } catch (error) {
           Object.assign(element, previousTask);
           ElMessage.error(error.response?.data?.message || 'Khong the cap nhat do uu tien');
          console.error('Lỗi API reorder:', error);
          fetchTasks();
        }
     } else if (groupBy.value === 'assignee') {
        const newAssignees = group.assigneeId === 'unassigned' ? [] : [group.assigneeId];
        element.assigneeIds = newAssignees;
        try {
          await store.updateTask(getProjectId(), element.id, {
            sortOrder: newSortOrder,
            assigneeIds: newAssignees
           });
           await fetchTasks();
         } catch (error) {
           Object.assign(element, previousTask);
           ElMessage.error(error.response?.data?.message || 'Không thể cập nhật người thực hiện');
           fetchTasks();
         }
     } else if (groupBy.value === 'sprint') {
        const newSprintId = group.sprintId === 'no-sprint' ? null : group.sprintId;
        element.sprintId = newSprintId;
        try {
          await store.updateTask(getProjectId(), element.id, {
            sortOrder: newSortOrder,
            sprintId: newSprintId
           });
           await fetchTasks();
         } catch (error) {
           Object.assign(element, previousTask);
           ElMessage.error(error.response?.data?.message || 'Không thể cập nhật chu kỳ');
           fetchTasks();
         }
     } else if (groupBy.value === 'module') {
        const newModuleId = group.moduleId === 'no-module' ? null : group.moduleId;
        element.moduleId = newModuleId;
        try {
          await store.updateTask(getProjectId(), element.id, {
            sortOrder: newSortOrder,
            moduleId: newModuleId
           });
           await fetchTasks();
         } catch (error) {
           Object.assign(element, previousTask);
           ElMessage.error(error.response?.data?.message || 'Không thể cập nhật phân hệ');
           fetchTasks();
         }
     }
  }
}
const handleGlobalCreate = (event) => {
    const detail = event?.detail || {};
    openCreateTask(detail.statusName || 'TO DO', {
      plannedStartDate: detail.plannedStartDate || null,
      dueDate: detail.dueDate || null
    });
}
const syncFiltersToUrl = () => {
  const query = { ...route.query }
  if (activeTaskFilters.value.length) {
    query.filters = encodeURIComponent(JSON.stringify(activeTaskFilters.value))
  } else {
    delete query.filters
  }
  router.replace({ query })
}
const applyTaskFilters = (filters) => {
  activeTaskFilters.value = Array.isArray(filters) ? filters : activeTaskFilters.value
  syncFiltersToUrl()
}
const removeTaskFilter = (id) => {
  activeTaskFilters.value = activeTaskFilters.value.filter(filter => filter.id !== id)
  syncFiltersToUrl()
}
const clearTaskFilters = () => {
  activeTaskFilters.value = []
  syncFiltersToUrl()
}
const hydrateFiltersFromUrl = () => {
  if (!route.query.filters) return
  try {
    const parsed = JSON.parse(decodeURIComponent(route.query.filters))
    activeTaskFilters.value = Array.isArray(parsed) ? parsed : []
    showFilterPanel.value = activeTaskFilters.value.length > 0
  } catch (error) {
    activeTaskFilters.value = []
  }
}
const exportAnalyticsCsv = (mode = analyticsInsightMode.value) => {
  const rows = mode === 'assignee'
    ? [
        ['Người thực hiện', 'Chờ xử lý', 'Đang làm', 'Đang đánh giá', 'Hoàn thành', 'Đã hủy', 'Tổng'],
        ...assigneeAnalyticsRows.value.map(item => [item.label, item.backlog, item.started, item.unstarted, item.completed, item.cancelled, item.total])
      ]
    : [
        [analyticsTableHeading.value, 'Số lượng'],
        ...analyticsBreakdownRows.value.map(item => [item.label, item.count])
      ]
  const csv = csvWithBom(rows)
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `${mode}-analytics.csv`
  link.click()
  URL.revokeObjectURL(url)
}
onMounted(() => {
  hydrateFiltersFromUrl()
  loadInitialData()
  analyticsThemeObserver = new MutationObserver(() => {
    analyticsTheme.value = document.documentElement.getAttribute('data-theme') || 'light'
  })
  analyticsThemeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] })
  window.addEventListener('global-create-task', handleGlobalCreate)
})
const handleRealtimeTaskUpdated = (task) => {
  if (!task?.id) return
  if (activeModuleFilterId.value) {
    clearTimeout(realtimeRefreshTimer)
    realtimeRefreshTimer = setTimeout(() => {
      fetchModuleTasks({ page: moduleTaskPage.value })
    }, 120)
    return
  }
  const normalizedTask = store.normalizeTaskRecord(task, getProjectId())
  if (!canCurrentUserSeeTask(normalizedTask)) {
    allTasks.value = allTasks.value.filter(item => item.id !== normalizedTask.id)
    if (selectedTask.value?.id === normalizedTask.id) {
      selectedTask.value = null
    }
    clearTimeout(realtimeRefreshTimer)
    realtimeRefreshTimer = setTimeout(() => {
      fetchTasks({ reset: false })
    }, 120)
    return
  }
  const index = allTasks.value.findIndex(item => item.id === normalizedTask.id)
  if (index >= 0) {
    allTasks.value[index] = { ...allTasks.value[index], ...normalizedTask }
  } else {
    allTasks.value = [...allTasks.value, normalizedTask]
  }
  if (selectedTask.value?.id === normalizedTask.id) {
    if (canCurrentUserSeeTask(normalizedTask)) {
      selectedTask.value = { ...selectedTask.value, ...normalizedTask }
    } else {
      selectedTask.value = null
    }
  }
  clearTimeout(realtimeRefreshTimer)
  realtimeRefreshTimer = setTimeout(() => {
    fetchTasks({ reset: false })
  }, 120)
}
const startTaskRealtime = async (projectId) => {
  if (!projectId) return
  if (signalRTaskUpdatedHandler) {
    signalRService.off('TaskUpdated', signalRTaskUpdatedHandler)
    signalRService.off('WorkTaskUpdated', signalRTaskUpdatedHandler)
  }
  if (signalREntityChangedHandler) {
    signalRService.off('EntityChanged', signalREntityChangedHandler)
  }
  if (signalRProjectEventHandler) {
    signalRService.off('ProjectRealtimeEvent', signalRProjectEventHandler)
  }
  if (signalREntityChangedHandler) {
    signalRService.off('EntityChanged', signalREntityChangedHandler)
  }
  await signalRService.startConnection(projectId)
  signalRTaskUpdatedHandler = handleRealtimeTaskUpdated
  signalRService.on('TaskUpdated', signalRTaskUpdatedHandler)
  signalRService.on('WorkTaskUpdated', signalRTaskUpdatedHandler)
  signalREntityChangedHandler = (event) => {
    if (`${event?.entityType || ''}`.toLowerCase() === 'task-collection' && `${event?.action || ''}`.toLowerCase() === 'reconcile') {
      fetchTasks({ reset: false })
      return
    }
    const updatedTask = store.applyRealtimeEntityEvent(event)
    if (updatedTask) handleRealtimeTaskUpdated(updatedTask)
  }
  signalRService.on('EntityChanged', signalREntityChangedHandler)
  signalRProjectEventHandler = (event) => {
    if (!event?.type) return
    if (event?.projectId && `${event.projectId}` !== `${projectId}`) return
    broadcastAdminRealtime(event.type, event.payload || { projectId })
  }
  signalRService.on('ProjectRealtimeEvent', signalRProjectEventHandler)
}
onMounted(() => {
  window.addEventListener('click', handleGlobalDropdownClick, true)
  startTaskRealtime(getProjectId())
  unsubscribeAdminRealtime = subscribeAdminRealtime(async ({ type, payload }) => {
    const pid = getProjectId()
    if (!pid) return
    if (payload?.projectId && `${payload.projectId}` !== `${pid}`) return
    if (
      [
        'project-settings-updated',
        'project-settings-favorite-updated',
        'project-settings-integrations-updated',
        'project-administration-updated'
      ].includes(type)
    ) {
      await loadInitialData({ preserveExisting: false })
    }
  })
})
watch(
  () => [currentProjectId.value, activeModuleFilterId.value],
  ([projectId, moduleId], [previousProjectId, previousModuleId]) => {
    if (!projectId || (projectId === previousProjectId && moduleId === previousModuleId)) {
      return
    }
    clearModuleDetailState()
    rawTasks.value = []
    allTasks.value = []
    store.clearTasks(projectId)
    selectedTask.value = null
    dynamicProjectId = projectId
    showAnalyticsSidebar.value = false
    isAnalyticsExpanded.value = false
    if (projectId !== previousProjectId) {
      startTaskRealtime(projectId)
    }
    loadInitialData()
  },
  { immediate: false }
)
watch(showSubtasks, (value) => {
  persistShowSubtasksPreference(value)
})
watch(displayProperties, (value) => {
  persistDisplayPropertiesPreference(value)
}, { deep: true })
watch(
  () => [route.query.tab, route.query.sprintId, route.query.moduleId, route.params.cycleId, route.query.carryOverFromSprintId],
  () => {
    if (route.query.tab === 'spreadsheet' || activeSprintFilterId.value || activeModuleFilterId.value || activeCarryOverSprintId.value) {
      currentTab.value = 'spreadsheet'
    } else if (route.query.tab === 'board') {
      currentTab.value = 'board'
    }
  },
  { immediate: true }
)
watch(
  () => route.query.carryOverFromSprintId,
  () => {
    loadInitialData()
  }
)
onUnmounted(() => {
  window.removeEventListener('click', handleGlobalDropdownClick, true)
  window.removeEventListener('global-create-task', handleGlobalCreate)
  analyticsThemeObserver?.disconnect()
  moduleDetailAbortController?.abort()
  moduleDetailRequestId += 1
  initialDataRequestId += 1
  clearTimeout(realtimeRefreshTimer)
  if (signalRTaskUpdatedHandler) {
    signalRService.off('TaskUpdated', signalRTaskUpdatedHandler)
    signalRService.off('WorkTaskUpdated', signalRTaskUpdatedHandler)
  }
  if (signalREntityChangedHandler) {
    signalRService.off('EntityChanged', signalREntityChangedHandler)
  }
  if (signalRProjectEventHandler) {
    signalRService.off('ProjectRealtimeEvent', signalRProjectEventHandler)
  }
  if (signalREntityChangedHandler) {
    signalRService.off('EntityChanged', signalREntityChangedHandler)
  }
  unsubscribeAdminRealtime?.()
})
</script>
<style scoped>
/* ==================================
   HI-TECH MODERN BUTTON THEME
   ================================== */
.cyber-create-task-btn {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: 32px;
  padding: 0 15px; /* match el-button */
  border-radius: 4px; /* match el-button plain */
  background: #ffffff;
  border: 1px solid #dcdfe6; /* default border color */
  cursor: pointer;
  overflow: hidden;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  z-index: 1;
}

/* The spinning glowing laser border */
.cyber-create-task-btn::before {
  content: '';
  position: absolute;
  top: -150%;
  left: -150%;
  width: 400%;
  height: 400%;
  background: conic-gradient(
    from 0deg,
    transparent 0%,
    transparent 60%,
    rgba(59, 130, 246, 0.2) 75%,
    rgba(59, 130, 246, 1) 95%,
    transparent 100%
  );
  animation: cyber-spin 3s linear infinite;
  opacity: 0;
  transition: opacity 0.4s ease;
  z-index: -2;
}

/* The inner mask to make the laser only visible on the border */
.cyber-create-task-btn::after {
  content: '';
  position: absolute;
  inset: 1px;
  background: #ffffff;
  border-radius: 3px;
  z-index: -1;
  transition: background-color 0.3s ease;
}

@keyframes cyber-spin {
  to {
    transform: rotate(360deg);
  }
}

/* Hover Effects */
.cyber-create-task-btn:hover:not(:disabled) {
  border-color: transparent;
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.15);
}

.cyber-create-task-btn:hover:not(:disabled)::before {
  opacity: 1;
}

.cyber-create-task-btn:hover:not(:disabled)::after {
  background-color: rgba(59, 130, 246, 0.05); /* separate light background color */
}

.cyber-create-task-btn:active:not(:disabled) {
  transform: translateY(1px);
}

.cyber-create-task-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  background: #f1f5f9;
  border-color: #e2e8f0;
}

/* Button Content (Premium Freezing Text) */
.cyber-btn-content {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-weight: 500; /* match el-button */
  font-size: 14px; /* match el-button */
  /* Premium frost effect: expanding radial gradient from the laser origin */
  background-image: 
    radial-gradient(
      circle at top right, 
      var(--color-accent) 0%, 
      var(--color-accent) 60%, 
      #93c5fd 80%, /* glowing frost edge */
      transparent 85%
    ),
    linear-gradient(#606266, #606266); /* base gray text */
  background-size: 0% 0%, 100% 100%;
  background-repeat: no-repeat, no-repeat;
  background-position: top right, center;
  -webkit-background-clip: text;
  background-clip: text;
  -webkit-text-fill-color: transparent;
  filter: drop-shadow(0 0 0px rgba(59, 130, 246, 0));
  transition: background-size 0.7s cubic-bezier(0.16, 1, 0.3, 1), filter 0.7s ease;
}

.cyber-create-task-btn:hover:not(:disabled) .cyber-btn-content {
  background-size: 350% 350%, 100% 100%; /* expands the frost layer to cover everything */
  filter: drop-shadow(0 0 6px rgba(59, 130, 246, 0.4)); /* soft glow when frozen */
}

/* ==================================
   PLANE.SO PROJECT KANBAN THEME
   ================================== */
.plane-board-container {
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--sa-bg, var(--color-bg)) 88%, var(--color-surface) 12%), var(--sa-bg, var(--color-bg)));
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;
  color: var(--color-text-primary);
  font-family: 'Inter', sans-serif;
  overflow: visible;
}
.module-detail-context {
  flex: 0 0 auto;
  padding: 12px var(--sa-page-x, 24px);
  border-bottom: 1px solid var(--color-border);
  background: var(--color-surface);
}
.module-state-panel,
.module-detail-heading {
  display: flex;
  align-items: center;
  gap: 12px;
}
.module-state-panel {
  min-height: 72px;
  padding: 14px 16px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  color: var(--color-text-primary);
}
.module-state-panel > div {
  display: flex;
  min-width: 0;
  flex: 1;
  flex-direction: column;
  gap: 3px;
}
.module-state-panel span {
  color: var(--color-text-muted);
  font-size: 13px;
}
.module-loading-state > i {
  color: var(--color-accent);
}
.module-error-state {
  border-color: color-mix(in srgb, #ef4444 45%, var(--color-border));
  background: color-mix(in srgb, #ef4444 7%, var(--color-surface));
}
.module-error-state > i {
  color: #ef4444;
}
.module-retry-btn {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  min-height: 36px;
  padding: 7px 12px;
  border: 1px solid var(--color-border);
  border-radius: 7px;
  background: var(--color-surface);
  color: var(--color-text-primary);
  font-weight: 750;
  cursor: pointer;
}
.module-retry-btn:focus-visible {
  outline: 3px solid color-mix(in srgb, var(--color-accent) 32%, transparent);
  outline-offset: 2px;
}
.module-detail-heading {
  justify-content: space-between;
  min-width: 0;
}
.module-detail-heading > div:first-child {
  display: flex;
  min-width: 0;
  align-items: center;
  gap: 10px;
}
.module-detail-heading strong {
  overflow: hidden;
  color: var(--color-text-primary);
  font-size: 15px;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.module-status-label {
  flex: 0 0 auto;
  padding: 4px 7px;
  border: 1px solid color-mix(in srgb, var(--color-accent) 32%, var(--color-border));
  border-radius: 6px;
  background: color-mix(in srgb, var(--color-accent) 8%, var(--color-surface));
  color: var(--color-text-secondary);
  font-size: 11px;
  font-weight: 800;
}
.module-progress {
  display: flex;
  flex: 0 0 auto;
  align-items: center;
  gap: 9px;
  color: var(--color-text-secondary);
  font-size: 12px;
  font-weight: 800;
}
.module-progress-track {
  width: 112px;
  height: 6px;
  overflow: hidden;
  border-radius: 3px;
  background: var(--color-border);
}
.module-progress-track > span {
  display: block;
  height: 100%;
  background: #22c55e;
}
.module-summary-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 8px;
  margin-top: 12px;
}
.module-summary-item {
  display: flex;
  min-width: 0;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 10px 12px;
  border: 1px solid var(--color-border);
  border-left: 3px solid #64748b;
  border-radius: 7px;
  background: var(--color-bg-secondary);
}
.module-summary-item.is-complete { border-left-color: #22c55e; }
.module-summary-item.is-progress { border-left-color: #0ea5e9; }
.module-summary-item.is-overdue { border-left-color: #ef4444; }
.module-summary-item span { overflow-wrap: anywhere; color: var(--color-text-muted); font-size: 12px; }
.module-summary-item strong { color: var(--color-text-primary); font-size: 17px; }
.module-empty-state {
  display: flex;
  flex: 1;
  min-height: 220px;
  align-items: center;
  justify-content: center;
  flex-direction: column;
  gap: 8px;
  padding: 24px;
  color: var(--color-text-muted);
  text-align: center;
}
.module-empty-state i {
  margin-bottom: 4px;
  font-size: 38px;
}
.module-empty-state strong {
  color: var(--color-text-primary);
  font-size: 15px;
}
.module-empty-state span {
  font-size: 13px;
}
@media (max-width: 640px) {
  .module-detail-context {
    padding: 10px 12px;
  }
  .module-detail-heading,
  .module-state-panel {
    align-items: stretch;
    flex-direction: column;
  }
  .module-progress {
    width: 100%;
  }
  .module-progress-track {
    flex: 1;
    width: auto;
  }
  .module-summary-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .module-retry-btn {
    justify-content: center;
    width: 100%;
  }
}
.timeline-wrapper {
  display: flex;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}
.calendar-wrapper {
  flex: 1;
  min-height: 0;
  overflow: auto;
}
.spreadsheet-wrapper {
  display: flex;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}
/* ── PLANE HEADER ── */
.plane-space-header {
  min-height: 64px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 18px;
  padding: 10px 24px;
  border-bottom: 1px solid var(--color-border);
  flex-shrink: 0;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 92%, var(--sa-bg, var(--color-bg)) 8%), var(--color-surface));
  box-shadow: 0 1px 0 rgba(255, 255, 255, 0.55);
}
.breadcrumb {
  display: flex;
  align-items: center;
  gap: 9px;
  font-size: 14px;
  color: var(--color-text-muted);
  min-width: 0;
  padding: 4px 0;
}
.proj-icon {
  background: linear-gradient(135deg, var(--sa-primary, var(--color-accent)), #22d3ee);
  color: #ffffff;
  width: 26px;
  height: 26px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: 800;
  box-shadow: 0 8px 18px color-mix(in srgb, var(--sa-primary, var(--color-accent)) 24%, transparent);
}
.proj-name {
  color: var(--color-text-primary);
  font-weight: 800;
  cursor: pointer;
  letter-spacing: -0.01em;
}
.proj-name:hover { color: var(--color-accent); }
.separator {
  font-size: 10px;
  color: var(--color-text-muted);
}
.active-page {
  color: var(--color-text-primary);
  display: flex;
  align-items: center;
  gap: 6px;
  font-weight: 700;
}
.active-page i { color: var(--color-text-muted); }
.item-count {
  background: var(--sa-primary-soft, color-mix(in srgb, var(--color-accent) 12%, transparent));
  color: color-mix(in srgb, var(--sa-primary, var(--color-accent)) 82%, var(--color-text-primary));
  padding: 3px 8px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 800;
}
.sh-right {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  justify-content: flex-end;
}
.view-toggles {
  display: flex;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 3px;
  margin-right: 2px;
  box-shadow: var(--sa-shadow-sm, var(--shadow-sm));
}
.toggle-btn {
  background: transparent;
  border: 1px solid transparent;
  color: var(--color-text-muted);
  width: 34px;
  height: 34px;
  border-radius: 9px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
}
.toggle-btn:hover {
  color: var(--color-text-primary);
  background: var(--color-surface-hover);
}
.toggle-btn.active {
  background: var(--sa-primary-soft, color-mix(in srgb, var(--color-accent) 14%, transparent));
  color: var(--sa-primary, var(--color-accent));
  border-color: color-mix(in srgb, var(--sa-primary, var(--color-accent)) 26%, var(--color-border));
}
.plane-toolbar-btn {
  min-height: 38px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  font-size: 13px;
  font-weight: 700;
  cursor: pointer;
  padding: 8px 13px;
  border-radius: 10px;
  transition: background 0.2s;
  display: flex;
  align-items: center;
}
.plane-toolbar-btn:hover {
  background: var(--color-surface-hover);
  border-color: var(--color-border-hover);
  color: var(--color-text-primary);
}
.plane-toolbar-btn.active {
  background: var(--sa-primary-soft, color-mix(in srgb, var(--color-accent) 12%, transparent));
  border-color: color-mix(in srgb, var(--sa-primary, var(--color-accent)) 28%, var(--color-border));
  color: var(--sa-primary, var(--color-accent));
}
.filter-count {
  margin-left: 6px;
  min-width: 16px;
  height: 16px;
  border-radius: 999px;
  background: var(--sa-primary, var(--color-accent));
  color: #ffffff;
  font-size: 10px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
.work-filter-row {
  padding: 12px 24px;
  border-bottom: 1px solid var(--color-border);
  background: color-mix(in srgb, var(--color-surface) 86%, var(--sa-bg, var(--color-bg)));
  flex-shrink: 0;
}
.plane-primary-btn {
  min-height: 38px;
  background: linear-gradient(135deg, var(--sa-primary, var(--color-accent)), color-mix(in srgb, var(--sa-primary, var(--color-accent)) 78%, #2563eb));
  color: #ffffff;
  border: 1px solid color-mix(in srgb, var(--sa-primary, var(--color-accent)) 70%, transparent);
  border-radius: 10px;
  padding: 8px 14px;
  font-size: 13px;
  font-weight: 800;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
  transition: background 0.2s;
}
.plane-primary-btn:hover {
  background: linear-gradient(135deg, var(--color-accent-hover), var(--sa-primary, var(--color-accent)));
  box-shadow: 0 12px 26px color-mix(in srgb, var(--sa-primary, var(--color-accent)) 24%, transparent);
}
/* Kanban Board */
.space-summary-page,
.plane-board-container {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  height: 100%;
  overflow: hidden;
}
.kanban-wrapper {
  display: flex;
  gap: 14px;
  flex: 1;
  height: 100%;
  min-height: 0;
  overflow-x: auto;
  overflow-y: hidden;
  padding: 12px 4px 0;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 20%, transparent), transparent 220px);
}
.kanban-col {
  min-width: 284px;
  width: 284px;
  height: 100%;
  max-height: none;
  min-height: 0;
  display: flex;
  flex-direction: column;
  background: var(--col-bg, transparent);
  padding: 10px;
  border: 1px solid color-mix(in srgb, var(--col-color) 18%, var(--color-border));
}
/* Loading indicator thanh ngang */
.kanban-loading-bar {
  position: fixed;
  top: 64px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 200;
  display: flex;
  align-items: center;
  gap: 8px;
  background: var(--color-surface-elevated);
  border: 1px solid var(--color-border);
  border-radius: 999px;
  padding: 6px 16px;
  font-size: 13px;
  color: var(--color-text-secondary);
  box-shadow: var(--shadow-popover);
  pointer-events: none;
}
/* Error banner */
.kanban-error-banner {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 16px;
  background: color-mix(in srgb, #ef4444 8%, var(--color-surface));
  border: 1px solid color-mix(in srgb, #ef4444 28%, var(--color-border));
  border-radius: 10px;
  color: #ef4444;
  font-size: 13px;
  font-weight: 600;
  flex-shrink: 0;
  align-self: flex-start;
  width: 100%;
  max-width: 560px;
}
.kanban-retry-btn {
  margin-left: auto;
  background: #ef4444;
  color: #fff;
  border: none;
  border-radius: 8px;
  padding: 5px 12px;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 5px;
  transition: background 0.2s;
}
.kanban-retry-btn:hover { background: #dc2626; }
.issue-card-header {
  position: relative;
  min-height: 56px;
  margin-bottom: 6px;
  display: grid;
  grid-template-columns: minmax(0, 1fr) 36px;
  align-items: start;
  gap: 10px;
}
.issue-card-heading-copy {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.card-top-right {
  position: relative;
  display: grid;
  grid-template-columns: 1fr;
  grid-template-rows: 22px 34px;
  align-items: center;
  justify-content: flex-end;
  justify-items: end;
  row-gap: 4px;
  min-width: 36px;
  overflow: visible;
}
.issue-card-header .star-task-btn.small {
  position: absolute;
  top: 1px;
  right: 0;
  width: 20px;
  height: 20px;
  min-width: 20px;
  min-height: 20px;
  padding: 0;
  font-size: 11px;
}
/* Due date badge */
.card-due-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  font-weight: 700;
  color: var(--color-text-muted);
  background: var(--color-surface-hover);
  border-radius: 6px;
  padding: 2px 7px;
}
.card-due-badge.card-due-overdue {
  color: #ef4444;
  background: rgba(239, 68, 68, 0.1);
  border: 1px solid rgba(239, 68, 68, 0.28);
  animation: pulse-overdue 2s ease-in-out infinite;
}
.card-due-badge.card-due-empty {
  color: var(--color-text-muted);
  border-style: dashed !important;
  background: color-mix(in srgb, var(--color-surface-hover) 62%, transparent) !important;
  width: 64px !important;
}
.card-due-compact {
  position: absolute;
  top: 1px;
  right: 25px;
  min-height: 20px;
  width: auto;
  max-width: none;
  margin-left: 0;
  padding: 1px 6px;
  flex: 0 0 auto;
  font-size: 10.5px;
  line-height: 1;
  white-space: nowrap;
  overflow: hidden;
}
.card-due-compact span {
  overflow: visible;
  text-overflow: clip;
}
.card-assignee-trigger {
  grid-column: 1;
  grid-row: 2;
  width: 32px;
  min-width: 32px;
  height: 32px;
  min-height: 32px;
  padding: 0;
  border: 1px solid transparent;
  border-radius: 50%;
  background: transparent;
  color: var(--color-text-secondary);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  overflow: hidden;
}
.card-assignee-trigger:hover {
  border-color: var(--color-accent);
  background: color-mix(in srgb, var(--color-accent) 8%, var(--color-surface));
}
.card-assignee-trigger.is-empty {
  border: 1px dashed var(--color-text-muted);
  background: #e2e8f0;
  color: #64748b;
  font-size: 11px;
}
.card-assignee-count {
  width: 30px;
  height: 30px;
  min-width: 30px;
  min-height: 30px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: var(--color-accent);
  color: #ffffff;
  font-size: 12px;
  font-weight: 800;
  line-height: 1;
  border: 2px solid var(--color-surface);
}
@keyframes pulse-overdue {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.7; }
}
/* Empty state per-column */
.col-empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100px;
  min-height: 100px;
  padding: 12px 16px;
  font-size: 13px;
  font-weight: 700;
  color: color-mix(in srgb, var(--col-color) 52%, var(--color-text-primary));
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--col-color) 10%, transparent), transparent),
    color-mix(in srgb, var(--color-bg) 72%, transparent);
  border: 1px dashed color-mix(in srgb, var(--col-color) 42%, var(--color-border));
  border-radius: 11px;
  margin-top: 0;
  text-align: center;
  line-height: 1.5;
  box-sizing: border-box;
  transition: background 160ms ease, transform 160ms ease, border-color 160ms ease, color 160ms ease;
}
.col-empty-state.clickable {
  cursor: pointer;
}
.col-empty-state.clickable:hover {
  color: var(--color-text-primary);
  background: color-mix(in srgb, var(--col-color) 14%, var(--color-bg));
  border-color: color-mix(in srgb, var(--col-color) 62%, var(--color-border));
  transform: translateY(-1px);
}
.col-empty-state.col-bottom-add {
  height: 100px;
  min-height: 100px;
  padding: 12px 16px;
  margin-top: 0;
}
.col-empty-state .add-action-text {
  display: flex;
  align-items: center;
  gap: 8px;
}
/* Inline create extras row */
.ic-extras {
  display: flex;
  align-items: center;
  gap: 8px;
  padding-top: 4px;
}
.ic-extra-label {
  display: flex;
  align-items: center;
  gap: 5px;
  font-size: 12px;
  color: var(--color-text-muted);
  cursor: pointer;
}
.ic-date-input {
  background: transparent;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  color: var(--color-text-primary);
  font-size: 12px;
  padding: 3px 6px;
  outline: none;
  cursor: pointer;
  max-width: 120px;
}
.ic-date-input:focus { border-color: var(--color-accent); }
.ic-assignee-btn {
  display: flex;
  align-items: center;
  gap: 5px;
  background: transparent;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  color: var(--color-text-muted);
  font-size: 12px;
  padding: 3px 8px;
  cursor: pointer;
  transition: all 0.15s;
}
.ic-assignee-btn:hover {
  border-color: var(--color-accent);
  color: var(--color-accent);
}
/* Inline create action buttons */
.ic-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  padding-top: 4px;
  border-top: 1px solid var(--color-border);
  margin-top: 4px;
}
.ic-submit-btn {
  display: flex;
  align-items: center;
  gap: 5px;
  background: var(--color-accent);
  color: #fff;
  border: none;
  border-radius: 7px;
  padding: 5px 12px;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  transition: background 0.2s;
}
.ic-submit-btn:hover { background: var(--color-accent-hover); }
.ic-cancel-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  background: transparent;
  border: 1px solid var(--color-border);
  border-radius: 7px;
  color: var(--color-text-muted);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.15s;
}
.ic-cancel-btn:hover { background: var(--color-surface-hover); color: var(--color-text-primary); }
.col-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  padding: 10px 12px;
  border: 1px solid color-mix(in srgb, var(--col-color) 26%, var(--color-border));
  border-radius: 12px;
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--col-color) 15%, transparent), transparent 58%),
    color-mix(in srgb, var(--color-bg) 58%, transparent);
}
.col-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 800;
  color: var(--color-text-primary);
}
.col-count {
  background: color-mix(in srgb, var(--col-color) 16%, var(--color-surface-hover));
  color: color-mix(in srgb, var(--col-color) 28%, var(--color-text-primary));
  padding: 3px 8px;
  border-radius: 8px;
  font-size: 12px;
  font-weight: 800;
}
.add-btn {
  color: color-mix(in srgb, var(--col-color) 44%, var(--color-text-secondary));
  cursor: pointer;
  font-size: 14px;
  width: 28px;
  height: 28px;
  border-radius: 8px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  transition: background 160ms ease, transform 160ms ease, color 160ms ease;
}
.add-btn:hover {
  color: var(--color-text-primary);
  background: color-mix(in srgb, var(--col-color) 16%, transparent);
  transform: translateY(-1px);
}
.col-body {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  max-height: none;
  overflow-y: auto;
  overscroll-behavior-y: contain;
  touch-action: pan-x pan-y;
  padding-right: 6px;
  position: relative;
  scrollbar-width: thin;
  scrollbar-color: color-mix(in srgb, var(--col-color) 42%, var(--color-border)) transparent;
}
.col-body::-webkit-scrollbar,
.kanban-wrapper::-webkit-scrollbar {
  width: 10px;
  height: 10px;
}
.col-body::-webkit-scrollbar-thumb,
.kanban-wrapper::-webkit-scrollbar-thumb {
  border-radius: 999px;
  background: color-mix(in srgb, var(--col-color, var(--color-accent)) 42%, var(--color-border));
  border: 2px solid transparent;
  background-clip: padding-box;
}
.col-body::-webkit-scrollbar-track,
.kanban-wrapper::-webkit-scrollbar-track {
  background: color-mix(in srgb, var(--color-surface) 44%, transparent);
  border-radius: 999px;
}
.chart-container {
  width: 100%;
  height: 230px;
}
.col-draggable {
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-height: min-content;
  padding-bottom: 16px;
}
.issue-card {
  position: relative;
  overflow: hidden;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.82), rgba(255, 255, 255, 0.72)),
    color-mix(in srgb, var(--task-status-color) 5%, var(--color-surface));
  border: 1px solid color-mix(in srgb, var(--task-status-color) 23%, var(--color-border));
  border-radius: 11px;
  padding: 11px 12px;
  cursor: pointer;
  box-shadow:
    0 12px 28px rgba(15, 23, 42, 0.07),
    inset 0 1px 0 rgba(255, 255, 255, 0.74);
  transition: transform 180ms cubic-bezier(0.2, 0.8, 0.2, 1), border-color 180ms ease, box-shadow 180ms ease;
}
.issue-card::before {
  content: "";
  position: absolute;
  inset: 0 auto 0 0;
  width: 4px;
  background: var(--task-status-color);
  z-index: 1;
  transition: width 180ms ease, opacity 180ms ease;
}
.issue-card::after {
  content: "";
  position: absolute;
  inset: 0;
  border: 2px solid var(--task-status-color);
  border-radius: inherit;
  pointer-events: none;
  opacity: 0;
  clip-path: polygon(0 0, 4px 0, 4px 100%, 0 100%);
  transition: clip-path 240ms cubic-bezier(0.2, 0.8, 0.2, 1), opacity 160ms ease;
}
.issue-card:hover {
  transform: translateY(-2px);
  border-color: color-mix(in srgb, var(--task-status-color) 48%, var(--color-border));
  box-shadow:
    0 12px 28px rgba(15, 23, 42, 0.07),
    inset 0 1px 0 rgba(255, 255, 255, 0.74);
}
.issue-card:hover::before,
.issue-card.active-card::before {
  width: 2px;
}
.issue-card:hover::after,
.issue-card.active-card::after {
  opacity: 1;
  clip-path: polygon(0 0, 100% 0, 100% 100%, 0 100%);
}
.issue-card.active-card {
  border-color: color-mix(in srgb, var(--task-status-color) 72%, var(--color-border));
  box-shadow:
    0 12px 28px rgba(15, 23, 42, 0.07),
    inset 0 1px 0 rgba(255, 255, 255, 0.74);
}
[data-theme='dark'] .issue-card {
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.045), rgba(255, 255, 255, 0.018)),
    color-mix(in srgb, var(--task-status-color) 9%, var(--color-surface));
  box-shadow:
    0 14px 34px rgba(0, 0, 0, 0.24),
    inset 0 1px 0 rgba(255, 255, 255, 0.06);
}
.issue-sequence {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", monospace;
  font-size: 11px;
  color: color-mix(in srgb, var(--task-status-color) 54%, var(--color-text-muted));
  margin: 0;
  font-weight: 800;
  letter-spacing: 0.02em;
}
.status-badge {
  border: 1px solid color-mix(in srgb, var(--badge-color, var(--color-accent)) 50%, var(--color-border)) !important;
  background: color-mix(in srgb, var(--badge-color, var(--color-accent)) 14%, var(--color-surface)) !important;
  color: color-mix(in srgb, var(--badge-color, var(--color-accent)) 92%, var(--color-text-primary)) !important;
  font-weight: 700 !important;
}
.status-badge i,
.status-badge span {
  color: var(--badge-color, var(--color-accent)) !important;
}
.issue-title {
  display: -webkit-box;
  min-height: calc(2 * 1.42em);
  max-height: calc(2 * 1.42em);
  overflow: hidden;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
  margin: 5px 0 0;
  font-size: 13px;
  font-weight: 800;
  color: var(--color-text-primary);
  line-height: 1.42;
  overflow-wrap: anywhere;
}
.issue-meta {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
}
.id { font-size: 12px; color: var(--color-text-muted); font-weight: 600; }
.ms-auto { margin-left: auto; }
.avatar-xs {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background-color: var(--color-surface-hover);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  font-weight: 600;
  color: var(--color-text-secondary);
  border: 1px solid var(--color-border);
}
/* Colors for priority icons */
.text-muted { color: var(--color-text-muted); }
.text-blue { color: #3B82F6; }
.text-orange { color: #F59E0B; }
.text-red { color: #EF4444; }
.text-green { color: #10B981; }
.badge {
  border: 1px solid color-mix(in srgb, var(--badge-color, var(--color-border)) 32%, var(--color-border));
  border-radius: 8px;
  padding: 3px 7px;
  font-size: 10.5px;
  color: color-mix(in srgb, var(--badge-color, var(--color-text-muted)) 38%, var(--color-text-primary));
  display: flex;
  align-items: center;
  gap: 6px;
  background: color-mix(in srgb, var(--badge-color, var(--color-surface-hover)) 9%, transparent);
  font-weight: 800;
}
.inline-create-box {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 2px;
  padding: 12px 16px;
  margin-top: 12px;
  box-shadow: 0 4px 12px rgba(0,0,0,0.5);
  display: flex;
  flex-direction: column;
  gap: 12px;
}
/* Kanban edge-to-edge layout fixes */
:deep(.project-page-inner) {
  --sa-page-x: 18px;
  padding-left: 0 !important;
  padding-right: 0 !important;
  max-width: 100%;
  overflow-x: hidden;
}
.space-summary-page {
  width: 100%;
  min-width: 0;
}
.plane-board-container > .project-page-header,
.plane-board-container > .work-filter-row,
.plane-board-container > .list-wrapper,
.plane-board-container > .calendar-wrapper,
.plane-board-container > .timeline-wrapper {
  padding-left: var(--sa-page-x) !important;
  padding-right: var(--sa-page-x) !important;
}
.plane-board-container > .project-page-toolbar {
  width: calc(100% - (var(--sa-page-x) * 2)) !important;
  margin-left: var(--sa-page-x) !important;
  margin-right: var(--sa-page-x) !important;
  box-sizing: border-box !important;
}
.ic-top {
  display: flex;
  align-items: center;
  gap: 10px;
}
.ic-plus {
  color: var(--color-text-primary);
  font-size: 16px;
}
.ic-input {
  width: 100%;
  background: transparent;
  border: none;
  color: var(--color-text-primary);
  outline: none;
  font-size: 14px;
  font-weight: 500;
  padding: 0;
}
.ic-input::placeholder { color: var(--color-text-muted); }
.ic-bottom {
  display: flex;
  align-items: center;
  gap: 8px;
}
.ic-chip {
  display: flex;
  align-items: center;
  gap: 6px;
  background: transparent;
  border: 1px solid var(--color-border);
  border-radius: 2px;
  padding: 4px 8px;
  font-size: 11px;
  color: var(--color-text-muted);
}
.ic-avatar {
  border: 1px dashed #3F3F46;
  background: transparent;
  color: #3F3F46;
  border-radius: 50%;
}
/* Scrollbar */
.kanban-wrapper::-webkit-scrollbar, .col-body::-webkit-scrollbar { width: 6px; height: 6px; }
.kanban-wrapper::-webkit-scrollbar-track, .col-body::-webkit-scrollbar-track { background: transparent; }
.kanban-wrapper::-webkit-scrollbar-thumb, .col-body::-webkit-scrollbar-thumb { background: var(--color-border); border-radius: 2px; }
.kanban-wrapper::-webkit-scrollbar-thumb:hover, .col-body::-webkit-scrollbar-thumb:hover { background: #3F3F46; }
/* Display Dropdown Styles */
.display-dropdown-wrapper,
.filter-dropdown-wrapper { position: relative; display: inline-block; }
.plane-dropdown-menu {
  position: absolute;
  top: 100%;
  right: 0;
  margin-top: 8px;
  background: var(--color-surface-elevated);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  width: 260px;
  max-height: min(450px, calc(100vh - 180px));
  overflow-y: auto;
  box-shadow: var(--shadow-popover);
  z-index: var(--z-popover);
  color: var(--color-text-primary);
  font-size: 13px;
  padding: 8px;
}
.filter-dropdown-menu {
  left: 0;
  right: auto;
  width: 640px;
  max-width: calc(100vw - 32px);
  max-height: none;
  overflow: visible;
  padding: 8px !important;
}
.filter-dropdown-menu .filter-bar-container {
  border: none;
  background: transparent;
  padding: 0 !important;
  min-height: auto;
  box-shadow: none;
  overflow: visible;
}
.display-trigger:hover,
.display-trigger.active {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  background: color-mix(in srgb, var(--color-accent) 9%, var(--color-surface)) !important;
  color: var(--color-accent) !important;
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.12);
}
.display-dropdown-menu {
  right: auto;
  left: 0;
  width: 720px;
  max-width: calc(100vw - 32px);
  display: grid;
  grid-template-columns: 240px minmax(280px, 1fr) 160px;
  align-items: stretch;
  gap: 0;
  overflow: visible;
  padding: 8px;
}
.display-dropdown-menu .dd-section {
  min-width: 0;
  padding: 10px 12px;
}
.display-dropdown-menu .dd-section.border-top {
  border-top: 0;
  border-left: 1px solid var(--color-border);
}
.display-dropdown-menu .dd-title {
  min-height: 24px;
}
.display-dropdown-menu .dd-list {
  display: grid;
  grid-template-columns: repeat(2, minmax(120px, 1fr));
  gap: 8px;
}
.display-dropdown-menu .dd-item {
  min-height: 34px;
  padding: 7px 9px;
  white-space: nowrap;
}
.display-dropdown-menu .dd-item.checkbox {
  min-height: 74px;
  align-items: flex-start;
  white-space: normal;
}
.display-dropdown-menu .dd-tag {
  min-width: 0;
  height: 34px;
  border-radius: 9px;
  font-weight: 750;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 7px;
  padding: 0 10px;
}
.display-dropdown-menu .dd-tag i {
  font-size: 11px;
}
.dd-section { padding: 8px; }
.dd-section.border-top { border-top: 1px solid var(--color-border); }
.dd-title { display: flex; justify-content: space-between; color: var(--color-text-muted); font-size: 12px; font-weight: 700; margin-bottom: 8px; }
.dd-btns { display: flex; gap: 8px; flex-wrap: wrap; }
.dd-tag { background: var(--color-surface); border: 1px solid var(--color-border); color: var(--color-text-secondary); border-radius: 8px; padding: 6px 12px; font-size: 13px; cursor: pointer; transition: all 0.15s ease; font-weight: 500; }
.dd-tag:hover,
.dd-tag.active:hover {
  background: color-mix(in srgb, var(--color-accent) 6%, var(--color-surface)) !important;
  color: var(--color-accent) !important;
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
}
.dd-tag.active { border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important; background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important; color: var(--color-accent) !important; font-weight: 600 !important; }
.dd-tag { display: inline-flex; align-items: center; gap: 6px; }
.dd-tag:hover i,
.dd-tag.active:hover i { color: var(--color-accent) !important; }
.dd-tag:hover span,
.dd-tag.active:hover span { color: var(--color-accent) !important; }
.dd-list { display: flex; flex-direction: column; gap: 8px; }
.dd-item { display: flex; align-items: center; gap: 8px; cursor: pointer; padding: 7px 8px; border-radius: 8px; color: var(--color-text-secondary); }
.dd-item:hover { background: var(--color-surface-hover); color: var(--color-text-primary); }
.dd-item input[type="radio"], .dd-item input[type="checkbox"] { accent-color: var(--color-accent); cursor: pointer; width: 14px; height: 14px; }
.plane-list-view {
  display: flex;
  flex-direction: column;
  color: var(--color-text-primary);
}
.list-wrapper {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  overflow-x: hidden;
}
.list-group {
  margin-bottom: 24px;
}
.group-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 0;
  cursor: pointer;
  border-bottom: 1px solid var(--color-border);
  margin-bottom: 8px;
}
.group-header:hover .add-icon {
  opacity: 1;
}
.gh-left,
.gh-right,
.pill-group {
  display: flex;
  align-items: center;
}
.group-content {
  display: flex;
  flex-direction: column;
  align-items: stretch;
}
.gh-left {
  gap: 10px;
}
.gh-chevron {
  font-size: 10px;
  color: var(--color-text-muted);
  width: 14px;
  text-align: center;
}
.group-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-primary);
}
.group-count {
  font-size: 12px;
  font-weight: 500;
  color: var(--color-text-muted);
  margin-left: 4px;
}
.add-icon {
  color: var(--color-text-muted);
  font-size: 14px;
  opacity: 0;
  transition: opacity 0.2s;
  padding: 4px;
}
.task-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
  padding: 10px 0 10px 24px;
  border-bottom: 1px solid var(--color-border);
  cursor: pointer;
}
.task-row:hover {
  background-color: var(--color-surface);
}
.subtask-row {
  margin-left: 28px;
  border-left: 1px dashed var(--color-border);
  background: rgba(22, 24, 29, 0.55);
}
.subtask-row:hover {
  background: rgba(30, 32, 37, 0.92);
}
.tr-left,
.tr-right {
  display: flex;
  align-items: center;
}
.tr-left {
  gap: 16px;
  min-width: 0;
}
.subtask-indent {
  width: 18px;
  color: var(--color-text-muted);
  display: inline-flex;
  justify-content: center;
  align-items: center;
  flex-shrink: 0;
}
.tr-right {
  justify-content: flex-end;
}
.task-id {
  font-size: 12px;
  color: var(--color-text-muted);
  font-weight: 600;
  min-width: 86px;
}
.task-title {
  color: var(--color-text-primary);
  font-size: 14px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.pill-group {
  gap: 8px;
  flex-wrap: wrap;
}
.pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  padding: 5px 10px;
  font-size: 12px;
  color: var(--color-text-secondary);
}
.pill-user-text {
  max-width: 140px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.avatar-xxs {
  width: 18px;
  height: 18px;
  border-radius: 999px;
  background: var(--color-border);
  color: var(--color-text-primary);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  border: 1px solid var(--color-border);
}
.add-row-placeholder {
  color: var(--color-text-muted);
  font-size: 13px;
  padding: 10px 0 10px 24px;
  cursor: pointer;
}
.add-row-placeholder:hover {
  color: var(--color-text-primary);
  background: var(--color-surface);
}
.plane-dropdown {
  background: var(--bg-secondary) !important;
  border: 1px solid var(--border-color) !important;
}
:global(.plane-popover) {
  background: var(--bg-secondary) !important;
  border: 1px solid var(--border-color) !important;
  padding: 8px !important;
  box-shadow: var(--shadow-lg) !important;
  border-radius: 10px !important;
  color: var(--text-primary) !important;
}
:global(.assignee-plane-popover),
:global(.plane-popover.assignee-plane-popover) {
  border-radius: 10px !important;
  padding: 8px !important;
}
.assignee-popover-content {
  padding-top: 0;
}
.assignee-search-field {
  position: relative;
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  min-height: 34px;
  height: 34px;
  box-sizing: border-box;
  border: 1px solid var(--color-border);
  border-radius: 9px;
  background: var(--color-surface);
  padding: 0 12px;
  color: var(--color-text-muted);
  transition: border-color 0.2s, box-shadow 0.2s;
}
.assignee-search-icon {
  position: static;
  transform: none;
  width: 16px;
  height: 16px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: 0 0 16px;
  font-size: 14px;
  pointer-events: none;
}
.assignee-search-input {
  width: 100% !important;
  height: 100% !important;
  min-width: 0 !important;
  border: 0 !important;
  border-radius: 0 !important;
  background: transparent !important;
  color: var(--color-text-primary) !important;
  padding: 0 !important;
  outline: none !important;
  font-size: 13.5px !important;
  line-height: 34px !important;
  text-indent: 0 !important;
  -webkit-appearance: none;
  appearance: none;
}
.assignee-search-input::placeholder {
  color: var(--color-text-muted);
}
.assignee-search-field:focus-within {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.14);
}
:global(.plane-dropdown .el-dropdown-menu__item) {
  color: var(--badge-color, var(--color-text-primary)) !important;
}
:global(.plane-dropdown .el-dropdown-menu__item.color-option),
:global(.plane-dropdown .el-dropdown-menu__item.color-option span),
:global(.plane-dropdown .el-dropdown-menu__item.color-option i) {
  color: var(--option-color) !important;
}
.no-shadow-context :global(.plane-popover) {
  box-shadow: none !important;
}
:global(.plane-popover .el-popper__arrow::before) {
  background: var(--bg-secondary) !important;
  border: 1px solid var(--border-color) !important;
}
:global(.plane-popover .popover-item) {
  width: calc(100% - 8px);
  margin: 0 4px;
  border-left: 4px solid transparent;
  border-radius: 8px;
  box-sizing: border-box;
}
:global(.plane-popover .popover-item.assignee-option-selected) {
  background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface)) !important;
  border-left-color: var(--color-accent);
  border-radius: 8px;
  color: var(--color-accent) !important;
}
:global(.plane-popover .popover-item.assignee-option-selected:hover) {
  background: color-mix(in srgb, var(--color-accent) 18%, var(--color-surface)) !important;
  color: var(--color-accent) !important;
}
.plane-search-input {
  width: 100%;
  background: var(--bg-tertiary);
  border: 1px solid var(--border-color);
  color: var(--text-primary);
  border-radius: var(--radius-small);
  padding: 8px 12px;
  outline: none;
  font-size: 13px;
  transition: all 0.2s;
}
.plane-search-input:focus {
  border-color: var(--color-accent);
}
.plane-search-input::placeholder {
  color: var(--color-text-muted);
}
.plane-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
  max-height: 220px;
  overflow-y: auto;
}
.plane-list-item {
  display: flex;
  align-items: center;
  gap: 10px;
  color: var(--text-primary);
  cursor: pointer;
  padding: 8px 10px;
  border-radius: var(--radius-small);
  transition: all 0.2s;
  font-size: 13px;
}
.plane-list-item:hover {
  background: var(--hover-bg);
}
.plane-list-item input[type="checkbox"] {
  accent-color: var(--color-accent);
  width: 14px;
  height: 14px;
  cursor: pointer;
}
.star-task-btn {
  appearance: none;
  -webkit-appearance: none;
  background: transparent;
  border: 0;
  cursor: pointer;
  padding: 4px;
  color: var(--color-text-muted);
  font: inherit;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
  min-width: 40px;
  min-height: 40px;
  touch-action: manipulation;
}
.star-task-btn:hover {
  background: var(--color-surface-hover);
}
.star-task-btn.small {
  padding: 0;
  font-size: 12px;
}
.star-task-btn:focus-visible {
  outline: 3px solid color-mix(in srgb, var(--color-accent, #0c66e4) 42%, transparent);
  outline-offset: 2px;
}
.star-task-btn:disabled { cursor: wait; }
.star-task-btn i { width: 1em; line-height: 1; text-align: center; }
/* Analytics Sidebar */
.forbidden-overlay { display: flex; align-items: center; justify-content: center; height: 100%; width: 100%; background: var(--color-bg); }
.forbidden-content { text-align: center; max-width: 400px; padding: 40px; background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 16px; }
.forbidden-content .lock-icon { font-size: 48px; color: #ef4444; margin-bottom: 24px; }
.forbidden-content h2 { margin: 0 0 12px 0; font-size: 20px; color: var(--color-text-primary); }
.forbidden-content p { margin: 0 0 24px 0; color: var(--color-text-secondary); line-height: 1.5; }
.forbidden-content .mt-4 { margin-top: 16px; }
.analytics-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(2, 6, 23, 0.52);
  z-index: 9999;
  display: flex;
  justify-content: flex-end;
  backdrop-filter: none;
  -webkit-backdrop-filter: none;
}
.analytics-panel {
  width: min(760px, 88vw);
  max-width: 92vw;
  background:
    linear-gradient(180deg, rgba(14, 165, 233, 0.10), transparent 280px),
    color-mix(in srgb, var(--color-bg) 88%, #0f172a 12%);
  height: 100%;
  box-shadow: -24px 0 64px rgba(0, 0, 0, 0.36) !important;
  display: flex;
  flex-direction: column;
  transform: translateX(100%);
  transition: transform 0.3s cubic-bezier(0.16, 1, 0.3, 1);
  border-left: 1px solid var(--color-border);
}
.analytics-panel.slide-in { transform: translateX(0); }
.analytics-panel.is-expanded {
  width: 100vw;
  max-width: 100vw;
}
.ap-header {
  padding: 14px 18px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid var(--color-border);
  background:
    linear-gradient(90deg, rgba(56, 189, 248, 0.18), transparent 56%),
    color-mix(in srgb, var(--color-surface) 82%, transparent);
}
.ap-header h3 { margin: 0; font-size: 18px; font-weight: 800; color: var(--color-text-primary); letter-spacing: 0; }
.ap-actions { display: flex; gap: 12px; }
.icon-btn { background: transparent; border: none; color: var(--color-text-muted); font-size: 14px; cursor: pointer; }
.icon-btn:hover { color: var(--color-text-primary); }
.ap-body {
  padding: 16px 18px 22px;
  overflow-y: auto;
  flex: 1;
}
/* Stats Grid */
.ap-stats-grid {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 10px;
}
.stat-box {
  position: relative;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 0;
  padding: 11px 12px;
  border: 1px solid rgba(148, 163, 184, 0.18);
  border-radius: 8px;
  background: linear-gradient(180deg, rgba(255,255,255,0.035), rgba(255,255,255,0.01));
}
.stat-box::before {
  content: "";
  position: absolute;
  inset: 0 auto 0 0;
  width: 3px;
  background: var(--stat-accent, #38bdf8);
}
.stat-box:nth-child(1) { --stat-accent: #41c0f2; }
.stat-box:nth-child(2) { --stat-accent: #0d519c; }
.stat-box:nth-child(3) { --stat-accent: #5c6795; }
.stat-box:nth-child(4) { --stat-accent: #0b4fd9; }
.stat-box:nth-child(5) { --stat-accent: #22c55e; }
.stat-box:hover {
  border-color: color-mix(in srgb, var(--stat-accent) 56%, var(--color-border));
  background: color-mix(in srgb, var(--color-surface) 86%, var(--stat-accent) 14%);
}
.stat-box .lbl { color: var(--color-text-muted); font-size: 11px; font-weight: 650; line-height: 1.35; }
.stat-box .val { color: var(--color-text-primary); font-size: 21px; font-weight: 850; line-height: 1; }
.ap-chart-card {
  margin-top: 12px;
  padding: 13px;
  border: 1px solid rgba(148, 163, 184, 0.18);
  border-radius: 10px;
  background:
    color-mix(in srgb, var(--color-surface) 78%, transparent);
}
.ap-chart-card h4 { margin: 0; font-size: 14px; font-weight: 800; color: var(--color-text-primary); }
.chart-container { height: 220px; }
.line-chart-mock {
  position: relative;
  height: 200px;
  margin-top: 16px;
  border-bottom: 1px solid var(--color-border);
}
.grid-l {
  position: absolute;
  width: 100%;
  border-top: 1px solid var(--color-border);
}
.grid-l span {
  position: absolute;
  left: -20px;
  top: -8px;
  font-size: 10px;
  color: var(--color-text-muted);
}
.dot { position: absolute; width: 6px; height: 6px; border-radius: 50%; transform: translate(-50%, 50%); border: 2px solid; background: var(--color-surface); }
.dot.blue { border-color: #0EA5E9; z-index: 2; }
.dot.green { border-color: #10B981; z-index: 1; }
.x-label { position: absolute; bottom: -20px; font-size: 11px; color: var(--color-text-muted); }
.chart-legend { display: flex; gap: 16px; font-size: 12px; color: var(--color-text-primary); margin-top: 24px; }
.leg-item { display: flex; align-items: center; gap: 8px; font-weight: 500; }
.box { width: 8px; height: 8px; border-radius: 2px; }
.bg-green { background: #10B981; }
.bg-blue { background: #0EA5E9; }
.insight-filters { display: flex; gap: 8px; }
.bar-chart-mock {
  position: relative;
  height: 250px;
  margin-top: 24px;
  border-bottom: 1px solid var(--color-border);
}
.bars-container {
  display: flex;
  justify-content: space-around;
  align-items: flex-end;
  height: 100%;
  padding-bottom: 1px; /* Avoid overlapping border */
}
.bar-wrapper { display: flex; flex-direction: column; align-items: center; gap: 8px; height: 100%; justify-content: flex-end; width: 40px; }
.bar { width: 100%; border-radius: 2px 4px 0 0; }
.bar-lbl { position: absolute; bottom: -24px; font-size: 12px; color: var(--color-text-muted); }
.bg-orange { background: #F97316; }
.bg-gray { background: #D4D4D8; }
.bg-red { background: #EF4444; }
.y-label {
  position: absolute;
  left: -40px;
  top: 50%;
  transform: rotate(-90deg) translateY(-50%);
  font-size: 10px;
  color: var(--color-text-muted);
  letter-spacing: 1px;
}
.ap-table-wrap {
  margin-top: 16px;
  padding: 16px;
  border: 1px solid rgba(148, 163, 184, 0.18);
  border-radius: 10px;
  background: color-mix(in srgb, var(--color-surface) 78%, transparent);
}
.table-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; font-size: 13px; }
.flex-center { display: flex; align-items: center; }
.export-btn { background: transparent; border: 1px solid var(--color-border); color: var(--color-text-secondary); border-radius: 6px; padding: 5px 8px; font-size: 12px; cursor: pointer; }
.export-btn:hover { background: var(--color-bg-secondary); color: var(--color-text-primary); }
.ap-table { width: 100%; border-collapse: collapse; font-size: 13px; color: var(--color-text-primary); }
.ap-table th { color: var(--color-text-muted); font-weight: 650; border-bottom: 1px solid var(--color-border); padding: 10px 0; text-align: left; }
.ap-table td { padding: 11px 0; border-bottom: 1px solid color-mix(in srgb, var(--color-border) 70%, transparent); }
.ap-table tr:hover { background: color-mix(in srgb, var(--color-surface) 82%, transparent); }
@media (max-width: 920px) {
  .ap-stats-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
.nexus-controls-row {
  gap: 8px;
}
.nexus-btn,
.nexus-btn-primary,
.view-btn,
.filter-btn,
.stats-btn {
  min-height: 32px;
  border-radius: 8px;
  padding: 6px 10px;
  font-size: 12.5px;
}
.view-toggle {
  border-radius: 9px;
  padding: 2px;
}
.kanban-board {
  gap: 14px !important;
}
.kanban-column,
.col {
  min-width: 284px !important;
  width: 284px !important;
  border-radius: 10px !important;
}
.column-header,
.col-header {
  min-height: 48px !important;
  padding: 10px 12px !important;
  border-radius: 8px !important;
}
.column-title,
.col-title {
  font-size: 12.5px !important;
}
.work-item-card,
.task-card {
  border-radius: 8px !important;
  padding: 12px !important;
}
.task-title,
.card-title {
  font-size: 13px !important;
  line-height: 1.3 !important;
  overflow-wrap: anywhere !important;
}
.col-body {
  gap: 10px !important;
  padding: 10px !important;
}
.list-wrapper {
  padding: 12px var(--sa-page-x, 24px) !important;
}
.group-header,
.task-row {
  min-height: 38px !important;
  padding: 8px 10px !important;
}
.ap-panel {
  border-radius: 10px !important;
}
.ap-header {
  padding: 14px 18px !important;
}
.ap-body {
  padding: 16px 18px 22px !important;
}
.ap-stats-grid {
  gap: 10px !important;
}
.stat-box,
.ap-chart-card,
.ap-table-wrap {
  border-radius: 8px !important;
  padding: 12px !important;
}
.stat-box .val {
  font-size: 20px !important;
}
@media (max-width: 760px) {
  .nexus-project-header {
    align-items: stretch !important;
    flex-direction: column !important;
    gap: 8px !important;
    padding: 10px 12px !important;
  }
  .nexus-controls-row {
    overflow-x: auto !important;
    justify-content: flex-start !important;
  }
  .board-wrapper,
  .kanban-wrapper,
  .list-wrapper {
    padding: 12px !important;
  }
  .kanban-column,
  .col {
    min-width: min(82vw, 284px) !important;
    width: min(82vw, 284px) !important;
  }
}
/* Polished list view and analytics panel */
.list-wrapper {
  background: var(--color-bg);
}
.list-group {
  overflow: hidden;
  margin-bottom: 12px !important;
  border: 1px solid color-mix(in srgb, var(--color-border) 86%, transparent);
  border-radius: 10px;
  background: color-mix(in srgb, var(--color-surface) 90%, transparent);
}
.group-header {
  min-height: 36px !important;
  margin: 0 !important;
  padding: 7px 12px !important;
  background: color-mix(in srgb, var(--color-surface-hover) 58%, transparent);
  border-bottom: 1px solid color-mix(in srgb, var(--color-border) 82%, transparent);
}
.group-name {
  font-size: 13.5px !important;
  font-weight: 850 !important;
  letter-spacing: 0.01em;
}
.group-count {
  min-width: 22px;
  height: 22px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 999px;
  background: color-mix(in srgb, var(--color-accent) 14%, var(--color-surface-hover));
  color: var(--color-text-primary) !important;
  font-size: 11px !important;
  font-weight: 850 !important;
}
.task-row {
  min-height: 42px !important;
  padding: 7px 10px 7px 14px !important;
  border-bottom-color: color-mix(in srgb, var(--color-border) 70%, transparent) !important;
  transition: background 0.16s ease, box-shadow 0.16s ease;
}
.task-row:hover {
  background: color-mix(in srgb, var(--color-accent) 8%, var(--color-surface)) !important;
  box-shadow: inset 3px 0 0 var(--color-accent);
}
.task-id {
  min-width: 92px !important;
  color: color-mix(in srgb, var(--color-accent) 72%, var(--color-text-primary)) !important;
  font-weight: 850 !important;
}
.task-title {
  font-size: 13px !important;
  font-weight: 650;
}
.pill {
  min-height: 24px;
  padding: 3px 8px !important;
  border-color: color-mix(in srgb, var(--color-border) 86%, transparent) !important;
  background: color-mix(in srgb, var(--color-surface-hover) 62%, transparent);
  color: var(--color-text-primary) !important;
  font-weight: 700;
}
.tr-left,
.tr-right {
  gap: 8px !important;
}
.task-title-btn,
.task-title {
  font-size: 13px !important;
  line-height: 1.25 !important;
}
.task-seq-id,
.task-id,
.id {
  font-size: 11px !important;
}
.priority-badge,
.task-status-tag,
.badge {
  min-height: 24px !important;
  padding: 3px 8px !important;
  font-size: 11px !important;
}
.add-row-placeholder {
  padding: 12px 16px !important;
  background: color-mix(in srgb, var(--color-surface-hover) 42%, transparent);
}
.analytics-panel {
  background: var(--color-bg) !important;
}
.ap-header {
  background: color-mix(in srgb, var(--color-surface) 88%, transparent) !important;
}
.stat-box,
.ap-chart-card,
.ap-table-wrap {
  background: color-mix(in srgb, var(--color-surface) 88%, transparent) !important;
}
.stat-box .lbl,
.ap-table th,
.table-head,
.bar-lbl,
.x-label,
.grid-l span {
  color: var(--color-text-muted) !important;
}
.stat-box .val,
.ap-chart-card h4,
.ap-table td {
  color: var(--color-text-primary) !important;
}
/* Stronger state color system for list and analytics */
.group-header {
  border-left: 3px solid color-mix(in srgb, var(--color-accent) 70%, transparent);
}
.pill-status {
  border-color: color-mix(in srgb, var(--pill-color, var(--color-accent)) 34%, var(--color-border)) !important;
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--pill-color, var(--color-accent)) 14%, transparent), transparent 70%),
    color-mix(in srgb, var(--pill-color, var(--color-accent)) 8%, var(--color-surface)) !important;
  color: var(--color-text-primary) !important;
}
.pill-status i {
  color: var(--pill-color, var(--color-accent)) !important;
}
.priority-badge,
.pill-priority {
  border: 1px solid color-mix(in srgb, var(--pill-color, var(--badge-color, var(--color-accent))) 50%, var(--color-border)) !important;
  background: color-mix(in srgb, var(--pill-color, var(--badge-color, var(--color-accent))) 18%, var(--color-surface)) !important;
  color: color-mix(in srgb, var(--pill-color, var(--badge-color, var(--color-accent))) 92%, var(--color-text-primary)) !important;
  font-weight: 700 !important;
  padding: 3px 8px !important;
  border-radius: 6px !important;
  transition: all 0.2s ease;
}
.priority-badge i,
.pill-priority i {
  color: var(--pill-color, var(--badge-color, var(--color-accent))) !important;
  font-size: 12px !important;
  font-weight: 900 !important;
}
.analytics-panel {
  background: var(--color-bg) !important;
}
.ap-header {
  min-height: 56px;
  background:
    linear-gradient(90deg, color-mix(in srgb, var(--color-accent) 13%, transparent), transparent 58%),
    color-mix(in srgb, var(--color-surface) 92%, transparent) !important;
}
.ap-header h3 {
  color: var(--color-text-primary) !important;
  font-size: 16px !important;
  font-weight: 900 !important;
}
.ap-body {
  background: transparent !important;
}
.stat-box {
  position: relative;
  overflow: hidden;
  min-height: 72px;
  border-left: 3px solid var(--stat-color, var(--color-accent)) !important;
}
.stat-box:nth-child(1) { --stat-color: #38bdf8; }
.stat-box:nth-child(2) { --stat-color: #f59e0b; }
.stat-box:nth-child(3) { --stat-color: #8b5cf6; }
.stat-box:nth-child(4) { --stat-color: #fb7185; }
.stat-box:nth-child(5) { --stat-color: #22c55e; }
.stat-box::after {
  content: "";
  position: absolute;
  inset: 0;
  background: linear-gradient(135deg, color-mix(in srgb, var(--stat-color) 13%, transparent), transparent 62%);
  pointer-events: none;
}
.stat-box .lbl,
.stat-box .val {
  position: relative;
  z-index: 1;
}
.stat-box .val {
  color: color-mix(in srgb, var(--stat-color) 38%, var(--color-text-primary)) !important;
}
.ap-chart-card {
  border-left: 3px solid color-mix(in srgb, var(--color-accent) 76%, #22c55e) !important;
}
.ap-table-wrap {
  overflow: hidden;
}
.ap-table tbody tr {
  background: linear-gradient(90deg, color-mix(in srgb, var(--row-color, var(--color-accent)) 8%, transparent), transparent 68%);
}
.analytics-row-label {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-weight: 750;
}
.analytics-row-dot {
  width: 8px;
  height: 8px;
  border-radius: 999px;
  background: var(--row-color, var(--color-accent));
  box-shadow: 0 0 0 4px color-mix(in srgb, var(--row-color, var(--color-accent)) 14%, transparent);
}
[data-theme='light'] .analytics-overlay {
  background: rgba(15, 23, 42, 0.36) !important;
}
.toolbar-actions-wrapper {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}
@media (max-width: 760px) {
  .toolbar-actions-wrapper {
    display: flex !important;
    flex-wrap: wrap !important;
    width: 100% !important;
    gap: 8px !important;
    margin-top: 10px !important;
  }
  .list-wrapper {
    padding: 12px !important;
  }
  .task-row {
    align-items: flex-start !important;
    flex-direction: column !important;
    gap: 8px !important;
  }
  .tr-right {
    width: 100%;
    justify-content: flex-start !important;
  }
}
/* SprintA premium board pass */
.plane-board-container {
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-bg) 70%, var(--color-surface)), var(--color-bg)) !important;
}
.kanban-wrapper {
  display: flex !important;
  gap: 14px !important;
  width: 100% !important;
  margin-left: 0 !important;
  margin-right: 0 !important;
  padding: 12px 0 16px !important;
  scroll-padding-inline: var(--sa-page-x, 18px);
  scroll-behavior: smooth;
  overscroll-behavior-x: contain;
  touch-action: pan-x pan-y;
  box-sizing: border-box !important;
}
.kanban-wrapper::before,
.kanban-wrapper::after {
  content: "";
  flex: 0 0 var(--sa-page-x, 18px);
}
.kanban-col:last-child {
  margin-right: 0 !important;
}
.kanban-wrapper::-webkit-scrollbar {
  height: 12px !important;
}
.kanban-wrapper::-webkit-scrollbar-track {
  border-radius: 999px;
  background: color-mix(in srgb, var(--sp-blue-600) 8%, var(--color-bg)) !important;
}
.kanban-wrapper::-webkit-scrollbar-thumb {
  border: 3px solid transparent;
  border-radius: 999px !important;
  background: linear-gradient(90deg, var(--sp-blue-600), var(--sp-sky-400)) padding-box !important;
}
.kanban-wrapper::-webkit-scrollbar-thumb:hover {
  background: linear-gradient(90deg, var(--sp-blue-700), var(--sp-sky-400)) padding-box !important;
}
.kanban-col {
  min-width: 284px !important;
  width: 284px !important;
  border-radius: 14px !important;
  border-color: color-mix(in srgb, var(--col-color) 30%, var(--color-border)) !important;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--col-color) 8%, var(--color-surface)), color-mix(in srgb, var(--color-bg) 62%, var(--color-surface))) !important;
  box-shadow:
    0 12px 30px color-mix(in srgb, #020617 8%, transparent),
    inset 0 1px 0 rgba(255,255,255,0.10);
}
.col-head {
  min-height: 42px !important;
  margin-bottom: 10px !important;
  padding: 8px 10px !important;
  border-radius: 11px !important;
  border: 1px solid color-mix(in srgb, var(--col-color) 38%, var(--color-border)) !important;
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--col-color) 14%, var(--color-surface)), color-mix(in srgb, var(--color-surface) 88%, transparent)) !important;
}
.issue-card {
  border-radius: 12px !important;
  padding: 11px 12px !important;
  background:
    linear-gradient(145deg, color-mix(in srgb, var(--task-status-color) 8%, var(--color-surface)), color-mix(in srgb, var(--color-surface) 88%, var(--color-bg))) !important;
  box-shadow:
    0 10px 24px color-mix(in srgb, #020617 8%, transparent),
    inset 0 1px 0 rgba(255,255,255,0.10) !important;
}
.issue-title,
.task-title,
.group-name {
  overflow-wrap: anywhere;
}
.badge,
.pill,
.priority-badge,
.task-status-tag {
  white-space: nowrap;
}
.add-btn-bottom,
.add-row-placeholder,
.col-empty-state {
  border-radius: 11px !important;
}
[data-theme='dark'] .kanban-col {
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--col-color) 10%, #17233a), color-mix(in srgb, var(--color-surface) 78%, #020617)) !important;
}
.inline-create-box {
  background: var(--color-surface);
  border: 1px solid color-mix(in srgb, var(--sp-blue-500, #3b82f6) 40%, var(--color-border));
  border-radius: 12px;
  padding: 12px;
  margin-bottom: 10px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.08);
}
.col-body.is-creating .col-draggable {
  order: 1;
  padding-bottom: 0;
}
.col-body.is-creating > .inline-create-box {
  order: 2;
  margin-top: 0;
  margin-bottom: 0;
  flex: 0 0 auto !important;
  height: auto !important;
  min-height: max-content !important;
  overflow: visible !important;
  box-sizing: border-box !important;
}
.col-body.is-creating > .col-draggable {
  flex: 0 0 auto !important;
}
.inline-create-top {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.inline-create-planning {
  display: grid;
  width: 100%;
  max-width: 194px !important;
  grid-template-columns: 156px 30px !important;
  align-items: center;
  justify-content: end;
  gap: 8px;
}
.inline-assignee-slot {
  width: 30px;
  height: 30px;
  display: flex;
  align-items: center;
  justify-content: center;
}
.inline-date-slot {
  width: 156px !important;
  min-width: 156px !important;
  max-width: 156px !important;
  height: 30px;
  min-height: 30px;
  overflow: hidden;
}
.inline-date-slot :deep(.el-date-editor),
.inline-date-slot .ic-date-range-inline {
  width: 156px !important;
  min-width: 156px !important;
  max-width: 156px !important;
}
.inline-assignee-slot :deep(.el-popover__reference-wrapper),
.inline-assignee-slot :deep(.el-tooltip__trigger),
.inline-assignee-slot :deep(.el-popper__trigger) {
  width: 30px !important;
  min-width: 30px !important;
  height: 30px !important;
  display: inline-flex !important;
  align-items: center;
  justify-content: center;
}
.inline-assignee-trigger {
  width: 30px;
  min-width: 30px;
  height: 30px;
  padding: 0;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--color-border);
  border-radius: 50%;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font-size: 11px;
  font-weight: 700;
  cursor: pointer;
}
.inline-assignee-trigger:hover {
  border-color: var(--color-accent);
  color: var(--color-accent);
}
.inline-assignee-count {
  width: 22px;
  height: 22px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: var(--color-accent);
  color: #ffffff;
  font-size: 10px;
}
.inline-create-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  margin-top: 10px;
}
.inline-create-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 8px;
  padding-top: 0;
  border-top: 0;
}
.inline-cancel-btn,
.inline-submit-btn {
  min-height: 32px;
  border-radius: 9px;
  padding: 0 12px;
  font-size: 12px;
  font-weight: 700;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  transition: all 0.18s ease;
}
.inline-cancel-btn {
  border: 1px solid var(--color-border);
  background: color-mix(in srgb, var(--color-surface) 92%, transparent);
  color: var(--color-text-secondary);
}
.inline-cancel-btn:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}
.inline-submit-btn {
  border: 1px solid color-mix(in srgb, var(--color-accent) 40%, var(--color-border));
  background: linear-gradient(135deg, var(--color-accent), color-mix(in srgb, var(--color-accent) 72%, #0f172a));
  color: #fff;
  box-shadow: 0 6px 16px rgba(37, 99, 235, 0.18);
}
.inline-submit-btn:hover {
  transform: translateY(-1px);
  box-shadow: 0 8px 18px rgba(37, 99, 235, 0.24);
}
.priority-badge span {
  max-width: 72px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ic-date-editor {
  display: flex;
  align-items: center;
}
.ic-date-editor :deep(.el-date-editor) {
  height: 28px !important;
  line-height: 28px !important;
  padding: 0 8px !important;
  border-radius: 6px !important;
}
.ic-date-editor :deep(.el-range-separator) {
  font-size: 11px !important;
  line-height: 28px !important;
}
.ic-date-editor :deep(.el-range-input) {
  font-size: 11.5px !important;
}
.ic-title-input {
  width: 100% !important;
  height: 34px !important;
  padding: 0 12px !important;
  border-radius: 9px !important;
  border: 1px solid var(--color-border) !important;
  background-color: var(--color-surface) !important;
  color: var(--color-text-primary) !important;
  font-size: 13px !important;
  outline: none !important;
  transition: border-color 0.2s, box-shadow 0.2s;
}
.ic-title-input:focus {
  border-color: var(--color-accent) !important;
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15) !important;
}
.ic-date-range-picker {
  width: 100% !important;
  min-width: 0 !important;
}
.ic-date-range-inline,
.ic-date-range-inline.el-date-editor,
.ic-date-range-inline :deep(.el-date-editor) {
  width: 156px !important;
  min-width: 156px !important;
  max-width: 156px !important;
}
.ic-date-range-picker.ic-date-compact {
  position: relative;
  width: 82px !important;
  flex: 0 0 82px;
}
.ic-date-range-picker.ic-date-compact::after {
  content: "Date";
  position: absolute;
  inset: 0 8px 0 28px;
  display: flex;
  align-items: center;
  color: var(--color-text-secondary);
  font-size: 11px;
  font-weight: 700;
  pointer-events: none;
}
.ic-date-range-picker.ic-date-compact :deep(.el-range-input),
.ic-date-range-picker.ic-date-compact :deep(.el-range-separator),
.ic-date-range-picker.ic-date-compact :deep(.el-range__close-icon) {
  visibility: hidden;
  width: 0 !important;
  flex: 0 0 0 !important;
}
.ic-date-range-picker :deep(.el-date-editor),
.ic-date-range-picker.el-date-editor {
  width: 100% !important;
  height: 34px !important;
  line-height: 34px !important;
  border-radius: 9px !important;
  border: 1px solid var(--color-border) !important;
  background-color: var(--color-surface) !important;
  color: var(--color-text-primary) !important;
  padding: 0 10px !important;
  box-sizing: border-box !important;
}
.ic-date-range-picker :deep(.el-range-input) {
  width: 34px !important;
  min-width: 0 !important;
  padding: 0 !important;
  font-size: 12px !important;
  color: var(--color-text-primary) !important;
}
.ic-date-range-picker :deep(.el-range-separator) {
  width: 12px !important;
  min-width: 12px !important;
  padding: 0 !important;
  font-size: 12px !important;
  line-height: 32px !important;
  color: var(--color-text-muted) !important;
}
.ic-date-range-picker :deep(.el-range__icon) {
  font-size: 13px !important;
}
/* Keep the active create form aligned with the compact task-card header. */
.inline-create-box .inline-create-planning {
  display: grid !important;
  grid-template-columns: minmax(0, 220px) 22px !important;
  width: 250px !important;
  max-width: 100% !important;
  gap: 8px !important;
  justify-content: end !important;
  margin-left: auto !important;
}
.inline-create-box {
  gap: 8px !important;
}
.inline-create-box .inline-create-meta {
  margin-top: 0 !important;
}
.inline-create-box .inline-date-slot {
  width: 220px !important;
  min-width: 0 !important;
  max-width: 100% !important;
  height: 22px !important;
  min-height: 22px !important;
  overflow: visible !important;
}
.inline-create-box .inline-date-slot :deep(.el-date-editor.ic-date-range-inline) {
  width: 220px !important;
  min-width: 0 !important;
  max-width: 100% !important;
  height: 22px !important;
  min-height: 22px !important;
  line-height: 20px !important;
  padding: 0 5px !important;
  border-radius: 6px !important;
  box-sizing: border-box !important;
  font-size: 10.5px !important;
}
.inline-create-box .inline-date-slot :deep(.el-range-input) {
  width: 76px !important;
  min-width: 0 !important;
  padding: 0 !important;
  font-size: 10.5px !important;
  line-height: 20px !important;
}
.inline-create-box .inline-date-slot :deep(.el-range-separator) {
  width: 10px !important;
  min-width: 10px !important;
  padding: 0 !important;
  font-size: 10.5px !important;
  line-height: 20px !important;
}
.inline-create-box .inline-date-slot :deep(.el-range__icon),
.inline-create-box .inline-date-slot :deep(.el-range__close-icon) {
  flex: 0 0 auto !important;
  font-size: 11px !important;
  line-height: 20px !important;
}
.inline-create-box .inline-assignee-slot {
  width: 22px !important;
  height: 22px !important;
}
.inline-create-box .inline-assignee-slot :deep(.el-popover__reference-wrapper),
.inline-create-box .inline-assignee-slot :deep(.el-tooltip__trigger),
.inline-create-box .inline-assignee-slot :deep(.el-popper__trigger),
.inline-create-box .inline-assignee-trigger {
  width: 22px !important;
  min-width: 22px !important;
  height: 22px !important;
}
.inline-create-box .inline-assignee-trigger {
  font-size: 10px !important;
  border: 1px dashed var(--color-text-muted) !important;
  background: #e2e8f0 !important;
  color: #64748b !important;
}
.inline-create-box .inline-create-actions {
  gap: 8px !important;
  margin-top: 0 !important;
  padding-top: 0 !important;
  border-top: 0 !important;
}
.inline-create-box .inline-cancel-btn,
.inline-create-box .inline-submit-btn {
  flex: 1 1 0 !important;
  width: 0 !important;
  min-width: 0 !important;
  box-shadow: none !important;
  transform: none !important;
}
.inline-create-box .inline-cancel-btn {
  background: #ef4444 !important;
  border-color: #ef4444 !important;
  color: #ffffff !important;
}
.inline-create-box .inline-cancel-btn:hover {
  background: #dc2626 !important;
  border-color: #dc2626 !important;
  color: #ffffff !important;
}
.inline-create-box .inline-submit-btn {
  background: #0ea5e9 !important;
  border-color: #0ea5e9 !important;
  color: #ffffff !important;
}
.inline-create-box .inline-submit-btn:hover {
  background: #0284c7 !important;
  border-color: #0284c7 !important;
  color: #ffffff !important;
}
/* Sort Popup Custom Combobox styling */
.filter-combobox {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 5px;
  width: 100%;
}
.filter-label {
  display: flex;
  color: var(--color-text-secondary);
  font-size: 11px;
  font-weight: 750;
  letter-spacing: 0.02em;
  text-transform: uppercase;
}
.filter-select-trigger {
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 10px;
  width: 100%;
  height: 36px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  color: var(--color-text-primary);
  padding: 0 12px;
  outline: none;
  font-size: 13px;
  cursor: pointer;
  transition: border-color 0.15s ease, background-color 0.15s ease;
}
.filter-select-trigger:hover,
.filter-select-trigger.active {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  background: color-mix(in srgb, var(--color-accent) 6%, var(--color-surface)) !important;
  color: var(--color-accent) !important;
  box-shadow: none !important;
}
.sort-combobox-trigger:hover,
.sort-combobox-trigger.active,
.timeline-filter-trigger:hover,
.timeline-filter-trigger.active {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  background: color-mix(in srgb, var(--color-accent) 6%, var(--color-surface)) !important;
  color: var(--color-accent) !important;
  box-shadow: none !important;
}
.filter-select-trigger:hover > i,
.filter-select-trigger.active > i,
.sort-combobox-trigger:hover i,
.sort-combobox-trigger.active i,
.timeline-filter-trigger:hover i,
.timeline-filter-trigger.active i {
  color: var(--color-accent) !important;
}
.filter-select-trigger:hover > span,
.filter-select-trigger.active > span,
.sort-combobox-trigger:hover span,
.sort-combobox-trigger.active span,
.timeline-filter-trigger:hover span,
.timeline-filter-trigger.active span {
  color: var(--color-accent) !important;
}
.icon-only-trigger {
  position: relative;
  width: 42px;
  min-width: 42px;
  justify-content: center;
  padding: 0 !important;
}
.icon-only-trigger .filter-count {
  position: absolute;
  top: -6px;
  right: -6px;
}
.filter-select-trigger:hover,
.filter-select-trigger.active {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border));
  background: color-mix(in srgb, var(--color-accent) 6%, var(--color-surface));
}
.filter-select-trigger span {
  flex: 1;
  text-align: left;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.filter-select-trigger i {
  color: var(--color-text-secondary);
}
.filter-select-menu {
  position: absolute;
  left: 0;
  right: 0;
  top: calc(100% + 4px);
  z-index: 120;
  max-height: 220px;
  overflow-y: auto;
  padding: 6px !important;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface-elevated);
  box-shadow: var(--shadow-popover);
  display: flex;
  flex-direction: column;
  gap: 0 !important;
}
.filter-select-option {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 8px;
  width: 100%;
  min-height: 32px !important;
  padding: 5px 9px !important;
  margin: 0 !important;
  border: 0;
  border-left: 4px solid transparent !important;
  border-radius: 8px !important;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 13px;
  font-weight: 500;
  text-align: left;
  cursor: pointer;
  transition: background-color 0.15s ease, border-color 0.15s ease, color 0.15s ease;
}
.filter-select-option:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}
.filter-select-option.selected {
  background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface)) !important;
  border-left-color: var(--color-accent) !important;
  border-radius: 8px !important;
  color: var(--color-accent);
  font-weight: 650;
}
.filter-select-option.selected:hover {
  background: color-mix(in srgb, var(--color-accent) 18%, var(--color-surface)) !important;
  color: var(--color-accent);
}
.filter-select-option > span {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  text-align: left;
}
.filter-select-option > i:first-child {
  width: 15px;
  color: currentColor;
  font-size: 12px;
  text-align: center;
}

/* Sort Search field styling matching FilterBar */
.filter-search-field {
  position: relative;
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  min-height: 34px;
  height: 34px;
  box-sizing: border-box;
  border: 1px solid var(--color-border);
  border-radius: 9px;
  background: var(--color-surface);
  padding: 0 12px;
  color: var(--color-text-muted);
  transition: border-color 0.2s, box-shadow 0.2s;
}
.filter-search-icon {
  position: static;
  transform: none;
  width: 16px;
  height: 16px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: 0 0 16px;
  font-size: 14px;
  pointer-events: none;
  color: var(--color-text-muted);
}
.filter-search-input {
  width: 100% !important;
  height: 100% !important;
  box-sizing: border-box !important;
  min-width: 0 !important;
  border: 0 !important;
  border-radius: 0 !important;
  background: transparent !important;
  color: var(--color-text-primary) !important;
  padding: 0 !important;
  outline: none !important;
  font-size: 13.5px !important;
  line-height: 34px !important;
  text-indent: 0 !important;
  appearance: none;
}
.filter-search-input::placeholder {
  color: var(--color-text-muted);
}
.filter-search-field:focus-within {
  border-color: var(--color-accent);
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.14);
}

/* Mini direction buttons next to selected sort item */
.dir-mini-btn {
  width: 30px;
  min-width: 30px;
  height: 30px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: all 0.15s ease;
}
.dir-mini-btn:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}
.dir-mini-btn.active {
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important;
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  color: var(--color-accent) !important;
  font-weight: 600 !important;
}

/* Global Empty State layout matching YourWorkView */
.empty-state-global {
  min-height: 204px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 60px 20px !important;
  background: transparent !important;
  border: 0 !important;
  box-shadow: none !important;
  text-align: center;
  margin: 16px auto !important;
}
.empty-spaces-icon {
  width: 54px;
  height: 54px;
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid color-mix(in srgb, var(--color-accent) 18%, transparent);
  border-radius: 14px;
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface));
  color: var(--color-accent);
  font-size: 23px;
  box-shadow: 0 14px 30px rgba(14, 165, 233, 0.12);
}
.empty-spaces-copy {
  max-width: 380px;
  text-align: center;
}
.empty-spaces-copy h3 {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 15px;
  font-weight: 800;
  line-height: 1.35;
}
.empty-spaces-copy p {
  margin: 3px 0 0;
  color: var(--color-text-muted);
  font-size: 13px;
  line-height: 1.4;
}
.empty-state-action-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  height: 34px;
  padding: 0 16px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font-size: 13px;
  font-weight: 550;
  cursor: pointer;
  transition: all 0.15s ease;
}
.empty-state-action-btn:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}
.empty-state-action-btn:active,
.empty-state-action-btn.active {
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important;
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  color: var(--color-accent) !important;
}
.empty-state-action-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
.timeline-filter-trigger:active,
.timeline-filter-trigger.active,
:deep(.timeline-filter-trigger:active),
:deep(.timeline-filter-trigger.active) {
  background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)) !important;
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border)) !important;
  color: var(--color-accent) !important;
}
</style>
