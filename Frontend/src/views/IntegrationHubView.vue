<template>
  <section class="integration-page">
    <header class="page-header app-shell-page-header">
      <div class="hero-copy app-shell-title-wrap">
        <span class="eyebrow">INTEGRATION HUB</span>
        <h1>{{ t('Trung tâm tích hợp công việc', 'Work Integration Hub') }}</h1>
        <div class="app-shell-header-help">
          <span class="app-shell-header-help-btn" aria-label="About Integration Hub">
            <i class="fa-solid fa-question"></i>
          </span>
          <div class="app-shell-header-help-popover" role="tooltip">
            <span>INTEGRATION HUB</span>
            <p>
              {{ t(
                'Kết nối Google Calendar thật để đưa cuộc họp, deadline và lịch sprint vào một inbox công việc có thể tạo task ngay.',
                'Connect real Google Calendar events into a work inbox that can turn signals into tasks.'
              ) }}
            </p>
          </div>
        </div>
      </div>

      <div class="hero-action-card" :class="{ connected: connectedProviders.length > 0 }">
        <div class="hero-card-main">
          <div class="hero-card-header">
            <span class="hero-status">{{ connectedProviders.length > 0 ? t('Đã kết nối', 'Connected') : t('Chưa kết nối', 'Not connected') }}</span>
            <strong>{{ connectedProviders.length > 0 ? t('Đồng bộ tất cả ứng dụng', 'Sync all connected apps') : t('Bắt đầu bằng Google Calendar', 'Start with Google Calendar') }}</strong>
          </div>
          <p>
            {{ connectedProviders.length > 0
              ? t('Một nút đồng bộ tất cả provider đã kết nối vào Unified Inbox.', 'One button syncs every connected provider into Unified Inbox.')
              : t('Kết nối một lần, sau đó Khôi có thể đồng bộ dữ liệu thật bất cứ lúc nào.', 'Connect once, then sync real data whenever needed.') }}
          </p>
        </div>
        <div class="hero-actions">
          <button
            v-if="connectedProviders.length === 0"
            class="primary cta"
            type="button"
            :disabled="connecting"
            @click="connectGoogleCalendar"
          >
            <i :class="connecting ? 'fa-solid fa-circle-notch fa-spin' : 'fa-brands fa-google'"></i>
            {{ connecting ? t('Đang mở Google...', 'Opening Google...') : t('Kết nối Google Calendar', 'Connect Google Calendar') }}
          </button>
          <button
            v-else
            class="primary cta"
            type="button"
            :disabled="syncing"
            @click="syncAllConnected"
          >
            <i class="fa-solid fa-rotate" :class="{ 'fa-spin': syncing }"></i>
            {{ syncing ? t('Đang đồng bộ', 'Syncing') : t('Đồng bộ ngay', 'Sync now') }}
          </button>
        </div>
      </div>
    </header>

    <main class="page-content">
      <section v-if="notice" class="notice" :class="notice.type">
        <i :class="notice.type === 'error' ? 'fa-solid fa-triangle-exclamation' : 'fa-solid fa-circle-info'"></i>
        <span>{{ notice.message }}</span>
        <button type="button" @click="notice = null" :aria-label="t('Đóng thông báo', 'Close notice')">
          <i class="fa-solid fa-xmark"></i>
        </button>
      </section>

      <section class="stats-grid" aria-label="Integration metrics">
        <article>
          <span>{{ t('Ứng dụng đã kết nối', 'Connected apps') }}</span>
          <strong>{{ connectedCount }}/3</strong>
        </article>
        <article>
          <span>{{ t('Inbox chưa đọc', 'Unread inbox') }}</span>
          <strong>{{ unreadCount }}</strong>
        </article>
        <article>
          <span>{{ t('Sự kiện lịch', 'Calendar events') }}</span>
          <strong>{{ sourceCount('calendar') }}</strong>
        </article>
        <article>
          <span>{{ t('Lần sync gần nhất', 'Last sync') }}</span>
          <strong>{{ formatDate(googleCalendar?.lastSyncedAt) || t('Chưa có', 'Not yet') }}</strong>
        </article>
      </section>

      <section class="workspace-grid">
        <aside class="panel apps-panel">
          <div class="panel-head">
            <div>
              <h2>{{ t('Ứng dụng kết nối', 'Connected apps') }}</h2>
              <p>{{ t('Google Calendar, Gmail và Slack đang dùng OAuth/sync thật. Không tạo dữ liệu giả.', 'Google Calendar, Gmail, and Slack use real OAuth/sync. No fake data.') }}</p>
            </div>
            <button class="icon-btn" type="button" :disabled="loadingIntegrations" @click="loadIntegrations" :title="t('Tải lại kết nối', 'Reload connections')">
              <i class="fa-solid fa-arrows-rotate" :class="{ 'fa-spin': loadingIntegrations }"></i>
            </button>
          </div>

          <div v-if="loadingIntegrations" class="provider-list">
            <article v-for="n in 3" :key="n" class="provider-card skeleton-card">
              <span></span>
              <div><b></b><small></small><small></small></div>
            </article>
          </div>

          <div v-else-if="integrationsError" class="inline-state error">
            <i class="fa-solid fa-triangle-exclamation"></i>
            <strong>{{ integrationsError }}</strong>
            <button class="ghost small" type="button" @click="loadIntegrations">{{ t('Thử lại', 'Try again') }}</button>
          </div>

          <div v-else class="provider-list">
            <article
              v-for="provider in renderedProviders"
              :key="provider.provider"
              class="provider-card"
              :class="[providerUiStatus(provider), { featured: provider.provider === 'google-calendar' }]"
            >
              <div class="provider-icon" :class="provider.source">
                <i :class="providerIcon(provider.provider)"></i>
              </div>

              <div class="provider-body">
                <div class="provider-top">
                  <strong>{{ provider.name }}</strong>
                  <span class="status-badge" :class="providerUiStatus(provider)">{{ providerStatus(provider) }}</span>
                </div>
                <p>{{ providerDescription(provider) }}</p>
                <div class="provider-meta">
                  <span v-if="provider.accountEmail"><i class="fa-regular fa-user"></i>{{ provider.accountEmail }}</span>
                  <span v-if="provider.lastSyncedAt"><i class="fa-regular fa-clock"></i>{{ formatDate(provider.lastSyncedAt) }}</span>
                  <span v-if="provider.status === 'coming_soon'"><i class="fa-solid fa-hourglass-half"></i>{{ t('Sắp ra mắt', 'Coming soon') }}</span>
                  <span v-if="providerUiStatus(provider) === 'not_connected'"><i class="fa-solid fa-link-slash"></i>{{ t('Chưa kết nối', 'Not connected') }}</span>
                  <span v-if="providerUiStatus(provider) === 'unconfigured'"><i class="fa-solid fa-lock"></i>{{ t('Chưa được cấu hình', 'Not configured') }}</span>
                  <span v-if="providerUiStatus(provider) === 'reconnect_required'"><i class="fa-solid fa-rotate"></i>{{ t('Cần kết nối lại', 'Reconnect required') }}</span>
                </div>
              </div>

              <div class="provider-actions">
                <button
                  v-if="provider.supportsConnect !== false && providerUiStatus(provider) !== 'connected' && provider.status !== 'coming_soon'"
                  class="primary small"
                  type="button"
                  :disabled="connecting"
                  @click="connectProvider(provider)"
                >
                  <i :class="connecting ? 'fa-solid fa-circle-notch fa-spin' : providerIcon(provider.provider)"></i>
                  {{ connecting ? t('Đang mở...', 'Opening...') : providerUiStatus(provider) === 'reconnect_required' ? t('Kết nối lại', 'Reconnect') : t(`Kết nối ${provider.name}`, `Connect ${provider.name}`) }}
                </button>

                <template v-if="providerUiStatus(provider) === 'connected'">
                  <button class="primary small" type="button" :disabled="syncingProviders[provider.provider] || syncing" @click="syncProvider(provider)">
                    <i class="fa-solid fa-rotate" :class="{ 'fa-spin': syncingProviders[provider.provider] }"></i>
                    {{ syncingProviders[provider.provider] ? t('Đang đồng bộ', 'Syncing') : t('Đồng bộ ngay', 'Sync now') }}
                  </button>
                  <button class="ghost small danger-text" type="button" @click="disconnect(provider.id)">
                    <i class="fa-solid fa-link-slash"></i>
                    {{ t('Ngắt kết nối', 'Disconnect') }}
                  </button>
                </template>

                <button
                  v-if="provider.status === 'coming_soon'"
                  class="ghost small"
                  type="button"
                  disabled
                >
                  <i class="fa-solid fa-lock mr-1"></i>
                  {{ t('Đang phát triển', 'Coming soon') }}
                </button>

                <button
                  v-if="provider.supportsConnect === false && providerUiStatus(provider) !== 'connected' && provider.status !== 'coming_soon'"
                  class="ghost small"
                  type="button"
                  disabled
                >
                  {{ t('Chưa được cấu hình', 'Not configured') }}
                </button>
              </div>
            </article>
          </div>

          <section class="sync-history">
            <div class="section-title">
              <h3>{{ t('Lịch sử đồng bộ', 'Sync history') }}</h3>
              <span>{{ syncHistory.length }}</span>
            </div>

            <div v-if="syncHistory.length === 0" class="history-empty">
              <i class="fa-regular fa-clock"></i>
              <span>{{ t('Chưa có lần đồng bộ nào.', 'No sync has run yet.') }}</span>
            </div>

            <div v-else class="history-list">
              <article v-for="log in syncHistory" :key="log.id" class="history-row" :class="log.status">
                <i :class="log.status === 'success' ? 'fa-solid fa-circle-check' : 'fa-solid fa-circle-exclamation'"></i>
                <div>
                  <strong>{{ log.provider }} · {{ syncStatusLabel(log.status) }}</strong>
                  <span>{{ log.message || t('Không có ghi chú', 'No message') }}</span>
                  <small>{{ formatDate(log.startedAt) }}</small>
                </div>
              </article>
            </div>
          </section>

          <section class="notification-center">
            <div class="section-title">
              <h3>{{ t('Trung tâm thông báo', 'Notification center') }}</h3>
              <span>{{ integrationNotifications.length }}</span>
            </div>

            <div v-if="integrationNotifications.length === 0" class="history-empty compact-empty">
              <i class="fa-regular fa-bell"></i>
              <span>{{ t('Chưa có thông báo tích hợp mới.', 'No new integration notifications.') }}</span>
            </div>

            <div v-else class="history-list">
              <article v-for="item in integrationNotifications" :key="item.id" class="history-row" :class="item.type">
                <i :class="item.icon"></i>
                <div>
                  <strong>{{ item.title }}</strong>
                  <span>{{ item.message }}</span>
                  <small>{{ item.time }}</small>
                </div>
              </article>
            </div>
          </section>
        </aside>

        <section class="panel inbox-panel">
          <div class="panel-head inbox-title">
            <div>
              <h2>{{ t('Unified Inbox', 'Unified Inbox') }}</h2>
              <p>{{ t('Tất cả mục đều đọc từ API và database thật.', 'Every item is read from the real API and database.') }}</p>
            </div>
            <button class="ghost small" type="button" :disabled="loadingInbox" @click="loadInbox">
              <i class="fa-solid fa-arrows-rotate" :class="{ 'fa-spin': loadingInbox }"></i>
              {{ t('Tải lại', 'Reload') }}
            </button>
          </div>

          <nav class="tabs" aria-label="Unified Inbox filters">
            <button
              v-for="tab in tabs"
              :key="tab.id"
              type="button"
              :class="{ active: activeTab === tab.id }"
              @click="activeTab = tab.id"
            >
              <i :class="tab.icon"></i>
              {{ tab.label }}
              <span>{{ sourceCount(tab.id) }}</span>
            </button>
          </nav>

          <!-- Trạng thái filters -->
          <nav class="status-filters" aria-label="Unified Inbox status filters">
            <button
              v-for="filter in statusFilters"
              :key="filter.id"
              type="button"
              class="status-filter-btn"
              :class="{ active: activeStatus === filter.id }"
              @click="activeStatus = filter.id"
            >
              {{ filter.label }}
            </button>
          </nav>

          <div v-if="selectedBulkItems.length > 0" class="bulk-toolbar">
            <label class="bulk-select-all">
              <input
                type="checkbox"
                :checked="allVisibleCreatableSelected"
                :disabled="visibleCreatableItems.length === 0"
                @change="toggleAllVisible($event.target.checked)"
              />
              <span>{{ t('Chọn mục chưa tạo task', 'Select items without task') }}</span>
            </label>
            <span class="bulk-count">{{ selectedBulkItems.length }} {{ t('mục đã chọn', 'selected') }}</span>
            <button
              class="primary small"
              type="button"
              :disabled="bulkCreating || selectedBulkItems.length === 0 || !selectedProjectId"
              @click="createSelectedTasks"
            >
              <i :class="bulkCreating ? 'fa-solid fa-circle-notch fa-spin' : 'fa-solid fa-square-plus'"></i>
              {{ t('Tạo nhiều task', 'Create tasks') }}
            </button>
            <button class="ghost small" type="button" @click="clearBulkSelection">
              {{ t('Bỏ chọn', 'Clear') }}
            </button>
          </div>

          <div v-if="loadingInbox" class="inbox-list">
            <article v-for="n in 4" :key="n" class="inbox-skeleton">
              <span></span>
              <div><b></b><small></small><small></small></div>
            </article>
          </div>

          <div v-else-if="inboxError" class="empty-state error">
            <i class="fa-solid fa-triangle-exclamation"></i>
            <strong>{{ inboxError }}</strong>
            <p>{{ t('Không tải được Unified Inbox. Hãy thử lại sau vài giây.', 'Unified Inbox could not load. Try again in a few seconds.') }}</p>
            <button class="primary small" type="button" @click="loadInbox">{{ t('Thử lại', 'Try again') }}</button>
          </div>

          <div v-else-if="filteredInbox.length === 0" class="empty-state">
            <i :class="emptyStateIcon"></i>
            <strong>{{ emptyTitle }}</strong>
            <p>{{ emptyBody }}</p>
            <button
              v-if="!isGoogleConnected"
              class="primary small"
              type="button"
              :disabled="connecting"
              @click="connectGoogleCalendar"
            >
              <i :class="connecting ? 'fa-solid fa-circle-notch fa-spin' : 'fa-brands fa-google'"></i>
              {{ t('Kết nối Google Calendar', 'Connect Google Calendar') }}
            </button>
            <button
              v-else-if="activeTab === 'all' || activeTab === 'calendar'"
              class="primary small"
              type="button"
              :disabled="syncing"
              @click="syncGoogleCalendar"
            >
              <i class="fa-solid fa-rotate" :class="{ 'fa-spin': syncing }"></i>
              {{ t('Đồng bộ lại', 'Sync again') }}
            </button>
          </div>

          <div v-else class="inbox-list">
            <button
              v-for="item in filteredInbox"
              :key="item.id"
              type="button"
              class="inbox-item"
              :class="{ selected: selectedItem?.id === item.id, unread: !item.isRead }"
              @click="selectItem(item)"
            >
              <input
                class="bulk-check"
                type="checkbox"
                :checked="isBulkSelected(item)"
                :disabled="!!item.createdTaskId"
                @click.stop
                @change="toggleBulkItem(item, $event.target.checked)"
              />
              <span class="source-icon" :class="item.source"><i :class="sourceIcon(item.source)"></i></span>
              <span class="item-main">
                <span class="item-title">
                  <strong>{{ item.title }}</strong>
                  <small>{{ formatDate(item.startsAt || item.createdAt) }}</small>
                </span>
                <span class="item-copy">{{ previewText(item.content) }}</span>
                <span class="item-tags">
                  <span>{{ sourceLabel(item.source) }}</span>
                  <span v-if="!item.isRead" class="unread-label">{{ t('Chưa đọc', 'Unread') }}</span>
                  <span v-if="item.createdTaskId" class="linked">{{ t('Đã tạo task', 'Task created') }}</span>
                </span>
              </span>
              <span v-if="!item.isRead" class="unread-dot"></span>
            </button>
          </div>
        </section>

      </section>

      <Teleport to="body">
        <Transition name="integration-drawer">
          <div v-if="selectedItemId" class="integration-detail-layer">
            <button
              class="integration-detail-backdrop"
              type="button"
              :aria-label="t('Đóng chi tiết inbox', 'Close inbox detail')"
              @click="closeDetail"
            ></button>

            <aside
              class="integration-detail-drawer"
              role="dialog"
              aria-modal="true"
              aria-labelledby="integration-detail-title"
            >
              <header class="integration-detail-header">
                <button class="detail-back-action" type="button" @click="closeDetail">
                  <i class="fa-solid fa-arrow-left"></i>
                  {{ t('Quay lại', 'Back') }}
                </button>
                <strong>{{ t('Chi tiết thông báo', 'Inbox detail') }}</strong>
                <button
                  class="detail-close-action"
                  type="button"
                  :title="t('Đóng', 'Close')"
                  :aria-label="t('Đóng chi tiết inbox', 'Close inbox detail')"
                  @click="closeDetail"
                >
                  <i class="fa-solid fa-xmark"></i>
                </button>
              </header>

              <div v-if="loadingInbox && !selectedItem" class="integration-detail-state" aria-live="polite">
                <i class="fa-solid fa-circle-notch fa-spin"></i>
                <strong>{{ t('Đang tải chi tiết...', 'Loading detail...') }}</strong>
              </div>

              <div v-else-if="inboxError && !selectedItem" class="integration-detail-state error" role="alert">
                <i class="fa-solid fa-triangle-exclamation"></i>
                <strong>{{ inboxError }}</strong>
                <button class="primary small" type="button" @click="loadInbox">{{ t('Thử lại', 'Try again') }}</button>
              </div>

              <div v-else-if="selectedItem" class="integration-detail-body">
                <div class="detail-head">
                  <span class="source-icon large" :class="selectedItem.source"><i :class="sourceIcon(selectedItem.source)"></i></span>
                  <div>
                    <p>{{ sourceLabel(selectedItem.source) }}</p>
                    <h2 id="integration-detail-title">{{ selectedItem.title }}</h2>
                  </div>
                </div>

                <dl class="detail-list">
                  <div>
                    <dt>{{ t('Provider', 'Provider') }}</dt>
                    <dd>{{ selectedItem.provider }}</dd>
                  </div>
                  <div v-if="selectedItem.startsAt">
                    <dt>{{ t('Bắt đầu', 'Starts') }}</dt>
                    <dd>{{ formatFullDate(selectedItem.startsAt) }}</dd>
                  </div>
                  <div v-if="selectedItem.endsAt">
                    <dt>{{ t('Kết thúc', 'Ends') }}</dt>
                    <dd>{{ formatFullDate(selectedItem.endsAt) }}</dd>
                  </div>
                  <div v-if="selectedItem.location">
                    <dt>{{ t('Địa điểm', 'Location') }}</dt>
                    <dd>{{ selectedItem.location }}</dd>
                  </div>
                </dl>

                <section class="content-box">
                  <h3>{{ t('Nội dung gốc', 'Original content') }}</h3>
                  <p>{{ selectedItem.content || t('Provider không trả mô tả cho mục này.', 'The provider did not return a description for this item.') }}</p>
                </section>

                <section class="task-target-box">
                  <div class="section-title compact">
                    <h3>{{ t('Project nhận task', 'Task target project') }}</h3>
                    <button class="text-action" type="button" :disabled="loadingProjectOptions" @click="loadCreateTaskOptions">
                      <i class="fa-solid fa-arrows-rotate" :class="{ 'fa-spin': loadingProjectOptions }"></i>
                      {{ t('Tải lại', 'Reload') }}
                    </button>
                  </div>

                  <label class="project-select-label" for="integration-project-select">
                    {{ t('Chọn project thật trước khi tạo task', 'Choose a real project before creating tasks') }}
                  </label>
                  <select
                    id="integration-project-select"
                    v-model="selectedProjectId"
                    class="project-select"
                    :disabled="loadingProjectOptions || projectOptions.length === 0"
                  >
                    <option value="">{{ loadingProjectOptions ? t('Đang tải project...', 'Loading projects...') : t('Chọn project...', 'Choose project...') }}</option>
                    <option v-for="project in projectOptions" :key="project.id" :value="project.id">
                      {{ project.name }}{{ project.key ? ` (${project.key})` : '' }}
                    </option>
                  </select>

                  <p v-if="projectOptionsError" class="mini-state error">{{ projectOptionsError }}</p>
                  <p v-else-if="!loadingProjectOptions && projectOptions.length === 0" class="mini-state">
                    {{ t('Bạn chưa có project khả dụng. Hãy tạo hoặc tham gia project trước khi tạo task từ inbox.', 'No available project yet. Create or join a project before creating tasks from inbox.') }}
                  </p>
                </section>

                <button
                  class="primary full"
                  type="button"
                  :disabled="creatingTask || !!selectedItem.createdTaskId || !selectedProjectId"
                  @click="createTask(selectedItem)"
                >
                  <i :class="creatingTask ? 'fa-solid fa-circle-notch fa-spin' : 'fa-solid fa-square-plus'"></i>
                  {{ selectedItem.createdTaskId ? t('Đã tạo task', 'Task created') : creatingTask ? t('Đang tạo task', 'Creating task') : t('Tạo task từ mục này', 'Create task from this item') }}
                </button>

                <section class="ai-box">
                  <div class="section-title compact">
                    <h3>{{ t('AI hỗ trợ xử lý', 'AI assistance') }}</h3>
                    <span>{{ t('Không tạo dữ liệu giả', 'No fake data') }}</span>
                  </div>
                  <div class="ai-actions">
                    <button
                      v-for="action in aiActions"
                      :key="action.key"
                      class="ghost small"
                      type="button"
                      :disabled="aiLoadingAction === action.key || !selectedItem?.id"
                      @click="runAiAction(action.key)"
                    >
                      <i :class="aiLoadingAction === action.key ? 'fa-solid fa-circle-notch fa-spin' : action.icon"></i>
                      {{ action.label }}
                    </button>
                  </div>
                  <p v-if="aiMessage" class="ai-result" :class="aiMessageType">
                    <i :class="aiMessageType === 'error' ? 'fa-solid fa-triangle-exclamation' : 'fa-regular fa-lightbulb'"></i>
                    <span>{{ aiMessage }}</span>
                  </p>
                  <div v-if="aiCandidates.length" class="ai-candidates" aria-label="Task candidates">
                    <div class="ai-candidates-head">
                      <strong>{{ t('Task đề xuất', 'Suggested tasks') }}</strong>
                      <button
                        class="primary small"
                        type="button"
                        :disabled="aiCandidateCreating || !selectedProjectId || selectedAiCandidateIds.length === 0"
                        @click="createSelectedAiCandidates"
                      >
                        {{ t(`Tạo ${selectedAiCandidateIds.length} công việc`, `Create ${selectedAiCandidateIds.length} tasks`) }}
                      </button>
                    </div>
                    <article v-for="candidate in aiCandidates" :key="candidate.id" class="ai-candidate-card" :class="`is-${candidate.status || 'pending'}`">
                      <label class="ai-candidate-select">
                        <input
                          type="checkbox"
                          :checked="selectedAiCandidateIds.includes(candidate.id)"
                          :disabled="candidate.status === 'created' || candidate.status === 'creating'"
                          @change="toggleAiCandidate(candidate, $event.target.checked)"
                        />
                        <span>{{ candidate.status === 'created' ? t('Đã tạo', 'Created') : candidate.status === 'failed' ? t('Lỗi', 'Failed') : t('Chờ xác nhận', 'Pending') }}</span>
                      </label>
                      <div v-if="editingAiCandidateId === candidate.id" class="ai-candidate-edit">
                        <input v-model="candidate.title" maxlength="300" aria-label="Task title" />
                        <textarea v-model="candidate.description" rows="3" aria-label="Task description"></textarea>
                        <input v-model="candidate.dueDate" type="datetime-local" aria-label="Task due date" />
                        <select v-model.number="candidate.priority" aria-label="Task priority">
                          <option :value="1">{{ t('Khẩn cấp', 'Urgent') }}</option>
                          <option :value="2">{{ t('Cao', 'High') }}</option>
                          <option :value="3">{{ t('Trung bình', 'Medium') }}</option>
                          <option :value="4">{{ t('Thấp', 'Low') }}</option>
                        </select>
                      </div>
                      <div v-else class="ai-candidate-body">
                        <strong>{{ candidate.title }}</strong>
                        <p v-if="candidate.description">{{ candidate.description }}</p>
                        <small>{{ candidate.dueDate || t('Chưa có hạn', 'No due date') }} · {{ t('Ưu tiên', 'Priority') }} {{ candidate.priority }}<template v-if="candidate.assigneeSuggestion"> · {{ candidate.assigneeSuggestion }}</template></small>
                      </div>
                      <div class="ai-candidate-evidence" v-if="candidate.evidence?.length">
                        <small v-for="evidence in candidate.evidence.slice(0, 3)" :key="`${candidate.id}-${evidence.field}-${evidence.source}`">
                          {{ evidence.field }}: {{ evidence.value }} · {{ evidence.type }} · {{ evidence.attachmentFileName || evidence.source }}
                        </small>
                      </div>
                      <div class="ai-candidate-actions">
                        <button class="ghost small" type="button" :disabled="candidate.status === 'creating' || candidate.status === 'created'" @click="editingAiCandidateId = editingAiCandidateId === candidate.id ? '' : candidate.id">
                          {{ editingAiCandidateId === candidate.id ? t('Xong', 'Done') : t('Chỉnh sửa', 'Edit') }}
                        </button>
                        <button class="primary small" type="button" :disabled="candidate.status === 'creating' || candidate.status === 'created' || !selectedProjectId" @click="createAiCandidate(candidate)">
                          {{ candidate.status === 'created' ? t('Đã tạo', 'Created') : candidate.status === 'creating' ? t('Đang tạo', 'Creating') : t('Tạo task', 'Create task') }}
                        </button>
                        <button class="text-action" type="button" :disabled="candidate.status === 'creating' || candidate.status === 'created'" @click="skipAiCandidate(candidate)">{{ t('Bỏ qua', 'Skip') }}</button>
                      </div>
                    </article>
                  </div>
                </section>
              </div>

              <div v-else class="integration-detail-state">
                <i class="fa-regular fa-file-circle-question"></i>
                <strong>{{ t('Không còn tìm thấy mục inbox này.', 'This inbox item is no longer available.') }}</strong>
                <button class="ghost small" type="button" @click="closeDetail">{{ t('Quay lại inbox', 'Back to inbox') }}</button>
              </div>
            </aside>
          </div>
        </Transition>
      </Teleport>
    </main>
  </section>
</template>

<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { signalRService } from '@/api/signalrService'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import axiosClient from '@/api/axiosClient'
import { useI18nStore } from '@/store/useI18nStore'
import { getScopedCurrentProjectId } from '@/utils/projectContext'
import { AUTH_SESSION_CHANGED, getStoredUserSession } from '@/utils/authSession'
import { createIntegrationRequestEpoch, getIntegrationAccountId, isReconnectRequiredMessage } from '@/utils/integrationState'

const route = useRoute()
const router = useRouter()
const i18nStore = useI18nStore()

const providers = ref([])
const syncHistory = ref([])
const inboxItems = ref([])
const accountId = ref(getIntegrationAccountId(getStoredUserSession()))
const requestEpoch = createIntegrationRequestEpoch()
const reconnectRequiredProviders = ref({})
let inboxRealtimeTimer = null
const handleInboxRealtime = event => {
  if (`${event?.entityType || ''}`.toLowerCase() !== 'inboxitem') return
  if (inboxRealtimeTimer) clearTimeout(inboxRealtimeTimer)
  inboxRealtimeTimer = setTimeout(loadInbox, 50)
}
const activeTab = ref('all')
const HIDDEN_INBOX_FILTER_IDS = new Set(['github', 'zalo'])
const selectedItemId = ref('')
const loadingIntegrations = ref(false)
const loadingInbox = ref(false)
const connecting = ref(false)
const syncing = ref(false)
const creatingTask = ref(false)
const bulkCreating = ref(false)
const selectedBulkIds = ref([])
const projectOptions = ref([])
const selectedProjectId = ref('')
const loadingProjectOptions = ref(false)
const projectOptionsError = ref('')
const aiLoadingAction = ref('')
const aiMessage = ref('')
const aiMessageType = ref('info')
const aiCandidates = ref([])
const selectedAiCandidateIds = ref([])
const editingAiCandidateId = ref('')
const aiCandidateCreating = ref(false)
const integrationsError = ref('')
const inboxError = ref('')
const notice = ref(null)
const activeStatus = ref('all') // all | unread | read | converted
const syncingProviders = ref({})

const captureRequest = () => requestEpoch.capture(accountId.value)
const isCurrentRequest = request => requestEpoch.isCurrent(request, accountId.value)

const clearIntegrationState = () => {
  requestEpoch.invalidate()
  providers.value = []
  syncHistory.value = []
  inboxItems.value = []
  reconnectRequiredProviders.value = {}
  selectedItemId.value = ''
  selectedBulkIds.value = []
  projectOptions.value = []
  selectedProjectId.value = ''
  integrationsError.value = ''
  inboxError.value = ''
  notice.value = null
  loadingIntegrations.value = false
  loadingInbox.value = false
  loadingProjectOptions.value = false
  connecting.value = false
  syncing.value = false
  creatingTask.value = false
  bulkCreating.value = false
  syncingProviders.value = {}
  aiCandidates.value = []
  selectedAiCandidateIds.value = []
  editingAiCandidateId.value = ''
  aiCandidateCreating.value = false
}

const t = (vi, en) => i18nStore.locale === 'en' ? en : vi

const tabs = computed(() => [
  { id: 'all', label: t('Tất cả', 'All'), icon: 'fa-solid fa-layer-group' },
  { id: 'calendar', label: t('Lịch', 'Calendar'), icon: 'fa-regular fa-calendar' },
  { id: 'email', label: 'Email', icon: 'fa-regular fa-envelope' },
  { id: 'slack', label: 'Slack', icon: 'fa-brands fa-slack' },
  { id: 'system', label: t('Hệ thống', 'System'), icon: 'fa-regular fa-bell' }
])

const statusFilters = computed(() => [
  { id: 'all', label: t('Tất cả', 'All') },
  { id: 'unread', label: t('Chưa đọc', 'Unread') },
  { id: 'read', label: t('Đã đọc', 'Read') },
  { id: 'converted', label: t('Đã tạo task', 'Task created') }
])

const renderedProviders = computed(() => {
  const list = [...providers.value]

  // Add GitHub card if not present
  if (!list.some(p => p.provider === 'github')) {
    list.push({
      provider: 'github',
      name: 'GitHub',
      source: 'github',
      status: 'coming_soon',
      accountEmail: '',
      lastSyncedAt: null,
      createdAt: null,
      supportsConnect: false,
      supportsSync: false
    })
  }

  // Add Zalo card if not present
  if (!list.some(p => p.provider === 'zalo')) {
    list.push({
      provider: 'zalo',
      name: 'Zalo',
      source: 'zalo',
      status: 'coming_soon',
      accountEmail: '',
      lastSyncedAt: null,
      createdAt: null,
      supportsConnect: false,
      supportsSync: false
    })
  }

  return list.filter(provider => !(
    ['github', 'zalo'].includes(provider.provider)
    && provider.status === 'coming_soon'
  ))
})

const googleCalendar = computed(() => providers.value.find(provider => provider.provider === 'google-calendar') || null)
const isGoogleConnected = computed(() => googleCalendar.value?.status === 'connected')
const connectedProviders = computed(() => providers.value.filter(provider => provider.status === 'connected' && provider.supportsSync !== false))
const connectedCount = computed(() => providers.value.filter(provider => provider.status === 'connected').length)
const unreadCount = computed(() => inboxItems.value.filter(item => !item.isRead).length)

const filteredInbox = computed(() => {
  let list = inboxItems.value

  // 1. Filter by source (tab)
  if (activeTab.value !== 'all') {
    list = list.filter(item => item.source === activeTab.value)
  }

  // 2. Filter by read status or created task status
  if (activeStatus.value === 'unread') {
    list = list.filter(item => !item.isRead)
  } else if (activeStatus.value === 'read') {
    list = list.filter(item => item.isRead)
  } else if (activeStatus.value === 'converted') {
    list = list.filter(item => !!item.createdTaskId)
  }

  return list
})

const selectedItem = computed(() => inboxItems.value.find(item => item.id === selectedItemId.value) || null)
const visibleCreatableItems = computed(() => filteredInbox.value.filter(item => !item.createdTaskId))
const selectedBulkItems = computed(() => inboxItems.value.filter(item => selectedBulkIds.value.includes(item.id) && !item.createdTaskId))
const allVisibleCreatableSelected = computed(() => visibleCreatableItems.value.length > 0 && visibleCreatableItems.value.every(item => selectedBulkIds.value.includes(item.id)))
const integrationNotifications = computed(() => {
  const failedSync = syncHistory.value
    .filter(log => log.status === 'error')
    .slice(0, 3)
    .map(log => ({
      id: `sync-${log.id}`,
      type: 'error',
      icon: 'fa-solid fa-circle-exclamation',
      title: t(`Lỗi đồng bộ ${providerNameById(log.provider)}`, `${providerNameById(log.provider)} sync failed`),
      message: log.message || t('Provider trả lỗi khi đồng bộ.', 'The provider returned an error while syncing.'),
      time: formatDate(log.startedAt)
    }))

  const unread = inboxItems.value
    .filter(item => !item.isRead)
    .slice(0, 3)
    .map(item => ({
      id: `inbox-${item.id}`,
      type: 'success',
      icon: sourceIcon(item.source),
      title: item.title,
      message: t(`Mục mới từ ${sourceLabel(item.source)} chưa đọc.`, `New unread item from ${sourceLabel(item.source)}.`),
      time: formatDate(item.startsAt || item.createdAt)
    }))

  return [...failedSync, ...unread].slice(0, 5)
})
const aiActions = computed(() => [
  { key: 'summary', label: t('Tóm tắt nội dung', 'Summarize'), icon: 'fa-regular fa-file-lines' },
  { key: 'suggest-task', label: t('Đề xuất task', 'Suggest task'), icon: 'fa-regular fa-square-plus' },
  { key: 'suggest-related-task', label: t('Liên kết task', 'Link task'), icon: 'fa-solid fa-link' }
])
const emptyStateIcon = computed(() => !isGoogleConnected.value ? 'fa-solid fa-plug-circle-bolt' : 'fa-regular fa-calendar-xmark')
const emptyTitle = computed(() => {
  if (!isGoogleConnected.value) return t('Bạn chưa kết nối ứng dụng nào', 'No app is connected yet')
  if (activeTab.value === 'calendar' || activeTab.value === 'all') return t('Chưa tìm thấy sự kiện nào', 'No calendar events found')
  return t('Chưa có dữ liệu trong bộ lọc này', 'No items in this filter')
})
const emptyBody = computed(() => {
  if (!isGoogleConnected.value) {
    return t(
      'Hãy kết nối Google Calendar để bắt đầu đồng bộ cuộc họp, deadline và lịch sprint vào Unified Inbox.',
      'Connect Google Calendar to start syncing meetings, deadlines, and sprint schedules into Unified Inbox.'
    )
  }
  if (activeTab.value === 'calendar' || activeTab.value === 'all') {
    return t(
      'Chưa tìm thấy sự kiện nào trong Google Calendar. Hãy tạo event trong lịch hoặc thử đồng bộ lại.',
      'No Google Calendar event was found. Create an event in Calendar or try syncing again.'
    )
  }
  return t('Tab này chưa có dữ liệu thật. Hãy kết nối provider tương ứng rồi bấm Đồng bộ ngay.', 'This tab has no real data yet. Connect the provider and click Sync now.')
})

const normalizeHiddenInboxFilterState = () => {
  if (HIDDEN_INBOX_FILTER_IDS.has(activeTab.value)) activeTab.value = 'all'

  const provider = `${route.query.provider || ''}`.toLowerCase()
  if (!HIDDEN_INBOX_FILTER_IDS.has(provider)) return

  router.replace({
    path: '/integrations',
    query: { ...route.query, provider: 'all' }
  }).catch(() => {})
}

const getPayload = (response) => response.data?.data ?? response.data
const asArray = (value) => {
  if (Array.isArray(value)) return value
  if (Array.isArray(value?.$values)) return value.$values
  if (Array.isArray(value?.items)) return value.items
  if (Array.isArray(value?.data)) return value.data
  if (Array.isArray(value?.data?.$values)) return value.data.$values
  return []
}

const loadIntegrations = async () => {
  const request = captureRequest()
  loadingIntegrations.value = true
  integrationsError.value = ''
  try {
    const response = await axiosClient.get('/integrations')
    if (!isCurrentRequest(request)) return false
    const data = getPayload(response)
    providers.value = asArray(data?.providers)
    syncHistory.value = asArray(data?.syncHistory)
    providers.value.forEach(provider => {
      if (provider.status === 'connected' && reconnectRequiredProviders.value[provider.provider]) {
        const next = { ...reconnectRequiredProviders.value }
        delete next[provider.provider]
        reconnectRequiredProviders.value = next
      }
    })
    return true
  } catch (error) {
    if (!isCurrentRequest(request)) return false
    integrationsError.value = error.response?.data?.message || t('Không tải được Integration Hub.', 'Could not load Integration Hub.')
    return false
  } finally {
    if (isCurrentRequest(request)) loadingIntegrations.value = false
  }
}

const loadInbox = async () => {
  const request = captureRequest()
  loadingInbox.value = true
  inboxError.value = ''
  try {
    const response = await axiosClient.get('/inbox')
    if (!isCurrentRequest(request)) return false
    inboxItems.value = asArray(getPayload(response))
    if (selectedItemId.value && !inboxItems.value.some(item => item.id === selectedItemId.value)) {
      selectedItemId.value = ''
    }
    return true
  } catch (error) {
    if (!isCurrentRequest(request)) return false
    inboxError.value = error.response?.data?.message || t('Không tải được Unified Inbox.', 'Could not load Unified Inbox.')
    return false
  } finally {
    if (isCurrentRequest(request)) loadingInbox.value = false
  }
}

const loadCreateTaskOptions = async () => {
  const request = captureRequest()
  loadingProjectOptions.value = true
  projectOptionsError.value = ''
  try {
    const response = await axiosClient.get('/inbox/create-task-options')
    if (!isCurrentRequest(request)) return false
    const projects = asArray(getPayload(response)?.projects)
    projectOptions.value = projects

    const scopedProjectId = getScopedCurrentProjectId()
    if (scopedProjectId && projects.some(project => project.id === scopedProjectId)) {
      selectedProjectId.value = scopedProjectId
    } else if (!selectedProjectId.value && projects.length === 1) {
      selectedProjectId.value = projects[0].id
    } else if (selectedProjectId.value && !projects.some(project => project.id === selectedProjectId.value)) {
      selectedProjectId.value = ''
    }
    return true
  } catch (error) {
    if (!isCurrentRequest(request)) return false
    projectOptions.value = []
    selectedProjectId.value = ''
    projectOptionsError.value = error.response?.data?.message || t('Không tải được danh sách project.', 'Could not load projects.')
  } finally {
    if (isCurrentRequest(request)) loadingProjectOptions.value = false
  }
}

const connectProvider = async (provider) => {
  if (provider?.supportsConnect === false) {
    notice.value = { type: 'error', message: t(`${provider.name} chưa được cấu hình.`, `${provider.name} is not configured yet.`) }
    return
  }
  const request = captureRequest()
  connecting.value = true
  notice.value = null
  try {
    const response = await axiosClient.get(`/integrations/${provider.provider}/connect`)
    const payload = getPayload(response)
    const authorizationUrl = payload?.authorizationUrl || payload?.authUrl
    if (!isCurrentRequest(request)) return
    if (!authorizationUrl) {
      throw new Error(t('Backend không trả URL OAuth.', 'Backend did not return an OAuth URL.'))
    }
    window.location.href = authorizationUrl
  } catch (error) {
    if (!isCurrentRequest(request)) return
    notice.value = { type: 'error', message: error.response?.data?.message || error.message }
  } finally {
    if (isCurrentRequest(request)) connecting.value = false
  }
}

const connectGoogleCalendar = async () => {
  if (!googleCalendar.value) return
  await connectProvider(googleCalendar.value)
}

const providerNameById = (provider) => ({
  'google-calendar': 'Google Calendar',
  gmail: 'Gmail',
  slack: 'Slack'
}[provider] || provider || t('ứng dụng', 'app'))

const completeGoogleOAuth = async () => {
  const provider = route.query.provider
  const providerName = providerNameById(provider)

  if (route.query.connected === 'success' && provider) {
    const loaded = await loadIntegrations()
    if (!loaded) return
    notice.value = { type: 'success', message: t(`Đã kết nối ${providerName} thành công.`, `${providerName} connected successfully.`) }
    ElMessage.success(notice.value.message)
    router.replace('/integrations').catch(() => {})
    return
  }

  if (route.query.connected === 'error' && provider) {
    const message = route.query.message || t(`Không kết nối được ${providerName}.`, `Could not connect ${providerName}.`)
    await loadIntegrations()
    notice.value = { type: 'error', message }
    ElMessage.error(message)
    router.replace('/integrations').catch(() => {})
    return
  }

  const code = route.query.code
  const state = route.query.state
  if (!code || !state) return

  try {
    await axiosClient.get('/integrations/google-calendar/callback', { params: { code, state } })
    notice.value = { type: 'success', message: t('Đã kết nối Google Calendar thành công.', 'Google Calendar connected successfully.') }
    ElMessage.success(notice.value.message)
    await loadIntegrations()
    router.replace({ path: '/integrations', query: { provider: 'google-calendar', connected: 'success' } }).catch(() => {})
  } catch (error) {
    notice.value = { type: 'error', message: error.response?.data?.message || t('Không hoàn tất Google OAuth.', 'Could not complete Google OAuth.') }
    router.replace('/integrations').catch(() => {})
  }
}

const syncProvider = async (provider) => {
  if (providerUiStatus(provider) !== 'connected') {
    notice.value = { type: 'error', message: t(`Bạn cần kết nối ${provider.name} trước.`, `Connect ${provider.name} first.`) }
    return
  }

  const request = captureRequest()
  syncingProviders.value[provider.provider] = true
  try {
    const response = await axiosClient.post(`/integrations/${provider.provider}/sync`)
    const imported = getPayload(response)?.imported ?? 0
    if (!isCurrentRequest(request)) return
    notice.value = {
      type: 'success',
      message: t(`Đã đồng bộ ${imported} mục thật từ ${provider.name}.`, `Synced ${imported} real items from ${provider.name}.`)
    }
    ElMessage.success(notice.value.message)
    await Promise.all([loadIntegrations(), loadInbox()])
  } catch (error) {
    if (!isCurrentRequest(request)) return
    const detail = error.response?.data?.message || error.message || ''
    const reconnectRequired = isReconnectRequiredMessage(detail)
    if (reconnectRequired) {
      reconnectRequiredProviders.value = { ...reconnectRequiredProviders.value, [provider.provider]: true }
    }
    const isOauthNotConfigured = detail.includes('chưa được cấu hình') || detail.includes('not configured')
    const errorMsg = isOauthNotConfigured 
      ? t(`OAuth của ${provider.name} chưa được cấu hình.`, `OAuth for ${provider.name} is not configured yet.`)
      : reconnectRequired
        ? t(`${provider.name} cần kết nối lại.`, `${provider.name} requires reconnect.`)
        : t(`Không đồng bộ được ${provider.name}.`, `Could not sync ${provider.name}.`)
    
    notice.value = { type: 'error', message: errorMsg }
    ElMessage.error(errorMsg)
  } finally {
    if (isCurrentRequest(request)) syncingProviders.value[provider.provider] = false
  }
}

const syncGoogleCalendar = async () => {
  if (!googleCalendar.value) return
  await syncProvider(googleCalendar.value)
}

const syncAllConnected = async () => {
  if (connectedProviders.value.length === 0) {
    notice.value = { type: 'error', message: t('Chưa có ứng dụng nào được kết nối.', 'No connected app is available.') }
    return
  }

  const request = captureRequest()
  syncing.value = true
  let totalImported = 0
  const failed = []

  try {
    for (const provider of connectedProviders.value) {
      if (!isCurrentRequest(request)) return
      syncingProviders.value[provider.provider] = true
      try {
        const response = await axiosClient.post(`/integrations/${provider.provider}/sync`)
        totalImported += Number(getPayload(response)?.imported ?? 0)
      } catch (error) {
        failed.push(provider.name)
      } finally {
        syncingProviders.value[provider.provider] = false
      }
    }

    if (!isCurrentRequest(request)) return
    notice.value = failed.length
      ? { type: 'error', message: t(`Đã đồng bộ ${totalImported} mục, nhưng lỗi: ${failed.join(', ')}.`, `Synced ${totalImported} items, but failed: ${failed.join(', ')}.`) }
      : { type: 'success', message: t(`Đã đồng bộ ${totalImported} mục thật từ tất cả ứng dụng.`, `Synced ${totalImported} real items from all connected apps.`) }

    if (failed.length) {
      ElMessage.warning(notice.value.message)
    } else {
      ElMessage.success(notice.value.message)
    }
    await Promise.all([loadIntegrations(), loadInbox()])
  } finally {
    if (isCurrentRequest(request)) syncing.value = false
  }
}

const disconnect = async (id) => {
  if (!id) return
  const request = captureRequest()
  try {
    await axiosClient.delete(`/integrations/${id}`)
    if (!isCurrentRequest(request)) return
    reconnectRequiredProviders.value = {}
    notice.value = { type: 'success', message: t('Đã ngắt kết nối Google Calendar.', 'Google Calendar disconnected.') }
    ElMessage.success(notice.value.message)
    await Promise.all([loadIntegrations(), loadInbox()])
  } catch (error) {
    if (!isCurrentRequest(request)) return
    notice.value = { type: 'error', message: error.response?.data?.message || t('Không ngắt kết nối được.', 'Could not disconnect.') }
  }
}

const selectItem = async (item) => {
  selectedItemId.value = item.id
  aiMessage.value = ''
  aiMessageType.value = 'info'
  aiCandidates.value = []
  selectedAiCandidateIds.value = []
  editingAiCandidateId.value = ''
  window.dispatchEvent(new CustomEvent('integration-detail-opened'))
  if (!item.isRead) {
    item.isRead = true
    try {
      await axiosClient.patch(`/inbox/${item.id}/read`)
    } catch {
      item.isRead = false
    }
  }
}

const closeDetail = () => {
  selectedItemId.value = ''
  aiMessage.value = ''
  aiMessageType.value = 'info'
  aiCandidates.value = []
  selectedAiCandidateIds.value = []
  editingAiCandidateId.value = ''
}

const handleDetailKeydown = (event) => {
  if (event.key === 'Escape' && selectedItemId.value) closeDetail()
}

const closeDetailForOtherUtility = () => {
  if (selectedItemId.value) closeDetail()
}

const isBulkSelected = (item) => selectedBulkIds.value.includes(item.id)

const toggleBulkItem = (item, checked) => {
  if (item.createdTaskId) return
  selectedBulkIds.value = checked
    ? Array.from(new Set([...selectedBulkIds.value, item.id]))
    : selectedBulkIds.value.filter(id => id !== item.id)
}

const toggleAllVisible = (checked) => {
  const visibleIds = visibleCreatableItems.value.map(item => item.id)
  selectedBulkIds.value = checked
    ? Array.from(new Set([...selectedBulkIds.value, ...visibleIds]))
    : selectedBulkIds.value.filter(id => !visibleIds.includes(id))
}

const clearBulkSelection = () => {
  selectedBulkIds.value = []
}

const createTask = async (item) => {
  if (!selectedProjectId.value) {
    ElMessage.warning(t('Hãy chọn hoặc tạo project trước khi tạo task từ inbox.', 'Choose or create a project before creating a task from inbox.'))
    return
  }

  creatingTask.value = true
  try {
    const response = await axiosClient.post(`/inbox/${item.id}/create-task`, { projectId: selectedProjectId.value })
    item.createdTaskId = getPayload(response)?.id || item.createdTaskId
    ElMessage.success(t('Đã tạo task thật từ mục inbox.', 'Created a real task from the inbox item.'))
    await loadInbox()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('Không tạo được task.', 'Could not create task.'))
  } finally {
    creatingTask.value = false
  }
}

const createSelectedTasks = async () => {
  const inboxItemIds = selectedBulkItems.value.map(item => item.id)
  if (inboxItemIds.length === 0) {
    ElMessage.warning(t('Hãy chọn ít nhất một mục chưa tạo task.', 'Select at least one item without a task.'))
    return
  }
  if (!selectedProjectId.value) {
    ElMessage.warning(t('Hãy chọn project trước khi tạo nhiều task từ inbox.', 'Choose a project before creating tasks from inbox.'))
    return
  }

  bulkCreating.value = true
  try {
    const response = await axiosClient.post('/inbox/create-tasks', {
      projectId: selectedProjectId.value,
      inboxItemIds
    })
    const createdCount = getPayload(response)?.createdCount ?? inboxItemIds.length
    ElMessage.success(t(`Đã tạo ${createdCount} task thật từ inbox.`, `Created ${createdCount} real tasks from inbox.`))
    clearBulkSelection()
    await loadInbox()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || t('Không tạo được nhiều task.', 'Could not create selected tasks.'))
  } finally {
    bulkCreating.value = false
  }
}

const normalizeAiCandidate = (candidate, index) => ({
  id: candidate?.id || `candidate-${index + 1}`,
  title: `${candidate?.title || ''}`.trim(),
  description: candidate?.description || '',
  dueDate: candidate?.dueDate || '',
  priority: Math.min(4, Math.max(1, Number(candidate?.priority) || 3)),
  assigneeSuggestion: candidate?.assigneeSuggestion || '',
  reason: candidate?.reason || '',
  sourceProvider: candidate?.sourceProvider || selectedItem.value?.provider || '',
  sourceItemId: candidate?.sourceItemId || selectedItem.value?.id || '',
  attachmentFileName: candidate?.attachmentFileName || '',
  uncertain: Boolean(candidate?.uncertain),
  evidence: Array.isArray(candidate?.evidence) ? candidate.evidence : [],
  status: 'pending',
  serverActionId: '',
  idempotencyKey: ''
})

const toggleAiCandidate = (candidate, checked) => {
  if (candidate.status === 'created' || candidate.status === 'creating') return
  selectedAiCandidateIds.value = checked
    ? Array.from(new Set([...selectedAiCandidateIds.value, candidate.id]))
    : selectedAiCandidateIds.value.filter(id => id !== candidate.id)
}

const skipAiCandidate = (candidate) => {
  candidate.status = 'skipped'
  selectedAiCandidateIds.value = selectedAiCandidateIds.value.filter(id => id !== candidate.id)
}

const createAiCandidate = async (candidate, { askConfirmation = true, manageBusy = true } = {}) => {
  if (!candidate || candidate.status === 'created' || candidate.status === 'creating') return false
  if (!selectedProjectId.value) {
    ElMessage.warning(t('Hãy chọn project trước khi tạo task đề xuất.', 'Choose a project before creating the suggested task.'))
    return false
  }
  if (!candidate.title.trim()) {
    candidate.status = 'failed'
    candidate.error = t('Task cần có tiêu đề.', 'A task needs a title.')
    return false
  }

  if (askConfirmation) {
    try {
      await ElMessageBox.confirm(
        t(`Xác nhận tạo task “${candidate.title}” trong project đã chọn?`, `Create “${candidate.title}” in the selected project?`),
        t('Xác nhận tạo task', 'Confirm task creation'),
        { confirmButtonText: t('Tạo task', 'Create task'), cancelButtonText: t('Hủy', 'Cancel'), type: 'warning' }
      )
    } catch (error) {
      if (error !== 'cancel' && error !== 'close') candidate.error = error?.message || String(error)
      return false
    }
  }

  if (manageBusy) aiCandidateCreating.value = true
  candidate.status = 'creating'
  candidate.error = ''
  const project = projectOptions.value.find(item => `${item.id}` === `${selectedProjectId.value}`)
  candidate.idempotencyKey ||= `integration:${selectedItem.value.id}:${candidate.id}:${selectedProjectId.value}`
  try {
    const previewResponse = await axiosClient.post('/ai/actions/preview', {
      type: 'task.create',
      idempotencyKey: candidate.idempotencyKey,
      workspaceId: project?.workspaceId || null,
      projectId: selectedProjectId.value,
      payload: {
        title: candidate.title.trim(),
        description: candidate.description || null,
        dueDate: candidate.dueDate || null,
        priority: candidate.priority,
        projectId: selectedProjectId.value
      }
    })
    const preview = getPayload(previewResponse)
    candidate.serverActionId = preview?.actionId || preview?.id || ''
    if (!candidate.serverActionId) throw new Error(t('Backend không tạo được action preview.', 'Backend could not create an action preview.'))
    const confirmResponse = await axiosClient.post(`/ai/actions/${candidate.serverActionId}/confirm`)
    if (confirmResponse.data?.success === false || confirmResponse.data?.succeeded === false) {
      throw new Error(confirmResponse.data?.message || t('Backend không xác nhận được task.', 'Backend could not confirm the task.'))
    }
    candidate.status = 'created'
    selectedAiCandidateIds.value = selectedAiCandidateIds.value.filter(id => id !== candidate.id)
    ElMessage.success(t(`Đã tạo task “${candidate.title}”.`, `Created “${candidate.title}”.`))
    return true
  } catch (error) {
    candidate.status = 'failed'
    candidate.error = error.response?.data?.message || error.message || t('Không tạo được task.', 'Could not create task.')
    return false
  } finally {
    if (manageBusy) aiCandidateCreating.value = false
  }
}

const createSelectedAiCandidates = async () => {
  const selected = aiCandidates.value.filter(candidate => selectedAiCandidateIds.value.includes(candidate.id) && candidate.status === 'pending')
  if (!selected.length) return
  try {
    await ElMessageBox.confirm(
      t(`Tạo ${selected.length} task đã chọn trong project hiện tại?`, `Create ${selected.length} selected tasks in the current project?`),
      t('Xác nhận tạo nhiều task', 'Confirm multiple task creation'),
      { confirmButtonText: t('Tạo tất cả', 'Create all'), cancelButtonText: t('Hủy', 'Cancel'), type: 'warning' }
    )
  } catch {
    return
  }

  aiCandidateCreating.value = true
  const results = await Promise.all(selected.map(candidate => createAiCandidate(candidate, { askConfirmation: false, manageBusy: false })))
  aiCandidateCreating.value = false
  const created = results.filter(Boolean).length
  const failed = results.length - created
  aiMessageType.value = failed ? 'error' : 'info'
  aiMessage.value = t(`Đã tạo ${created}/${results.length} task; ${failed} task lỗi.`, `Created ${created}/${results.length} tasks; ${failed} failed.`)
}

const formatAiMessage = (payload) => {
  if (!payload) return t('AI không trả dữ liệu.', 'AI returned no data.')
  if (payload.message) return payload.message
  if (payload.summary) return payload.summary
  if (payload.suggestedTask) {
    const task = payload.suggestedTask
    return [
      task.title ? `${t('Tiêu đề', 'Title')}: ${task.title}` : '',
      task.description ? `${t('Mô tả', 'Description')}: ${task.description}` : '',
      task.priority ? `${t('Ưu tiên', 'Priority')}: ${task.priority}` : '',
      task.reason ? `${t('Lý do', 'Reason')}: ${task.reason}` : ''
    ].filter(Boolean).join('\n')
  }
  if (payload.relatedTask) {
    const task = payload.relatedTask
    return [
      task.title ? `${t('Task liên quan', 'Related task')}: ${task.title}` : t('Không tìm thấy task phù hợp.', 'No related task found.'),
      task.reason ? `${t('Lý do', 'Reason')}: ${task.reason}` : ''
    ].filter(Boolean).join('\n')
  }
  return JSON.stringify(payload)
}

const runAiAction = async (action) => {
  if (!selectedItem.value?.id) return

  aiLoadingAction.value = action
  aiMessage.value = ''
  aiMessageType.value = 'info'
  if (action === 'suggest-task') {
    aiCandidates.value = []
    selectedAiCandidateIds.value = []
    editingAiCandidateId.value = ''
  }
  try {
    const response = await axiosClient.get(`/inbox/${selectedItem.value.id}/ai/${action}`)
    const payload = getPayload(response)
    const warnings = asArray(payload?.warnings).filter(Boolean)
    aiMessageType.value = payload?.configured === false || warnings.length ? 'error' : 'info'
    if (action === 'suggest-task' && payload?.configured !== false) {
      aiCandidates.value = asArray(payload?.candidates).map(normalizeAiCandidate).filter(candidate => candidate.title)
      const candidateMessage = aiCandidates.value.length
        ? t(`AI đề xuất ${aiCandidates.value.length} task có nguồn bằng chứng.`, `AI suggested ${aiCandidates.value.length} evidence-backed tasks.`)
        : t('AI chưa tìm thấy task đủ rõ để đề xuất.', 'AI did not find a clear task to suggest.')
      aiMessage.value = warnings.length ? `${warnings.join(' ')} ${candidateMessage}` : candidateMessage
      return
    }
    aiMessage.value = payload?.configured === false
      ? t('AI chưa được cấu hình. Hãy kiểm tra Gemini:ApiKey trong appsettings.', 'AI is not configured. Check Gemini:ApiKey in appsettings.')
      : formatAiMessage(payload)
  } catch (error) {
    aiMessageType.value = 'error'
    aiMessage.value = error.response?.data?.message || t('AI chưa xử lý được yêu cầu này.', 'AI could not handle this request.')
  } finally {
    aiLoadingAction.value = ''
  }
}

const sourceCount = (source) => source === 'all'
  ? inboxItems.value.length
  : inboxItems.value.filter(item => item.source === source).length

const providerIcon = (provider) => ({
  'google-calendar': 'fa-brands fa-google',
  gmail: 'fa-regular fa-envelope',
  slack: 'fa-brands fa-slack',
  github: 'fa-brands fa-github',
  zalo: 'fa-solid fa-comment'
}[provider] || 'fa-solid fa-plug')

const sourceIcon = (source) => ({
  calendar: 'fa-regular fa-calendar',
  email: 'fa-regular fa-envelope',
  slack: 'fa-brands fa-slack',
  github: 'fa-brands fa-github',
  zalo: 'fa-solid fa-comment',
  system: 'fa-regular fa-bell'
}[source] || 'fa-solid fa-inbox')

const sourceLabel = (source) => ({
  calendar: t('Lịch', 'Calendar'),
  email: 'Email',
  slack: 'Slack',
  github: 'GitHub',
  zalo: 'Zalo',
  system: t('Hệ thống', 'System')
}[source] || source)

const providerUiStatus = (provider) => {
  if (provider.status === 'connected') return 'connected'
  if (provider.status === 'coming_soon') return 'coming_soon'
  if (reconnectRequiredProviders.value[provider.provider]) return 'reconnect_required'
  if (provider.supportsConnect === false) return 'unconfigured'
  return 'not_connected'
}

const providerStatus = (provider) => {
  const status = providerUiStatus(provider)
  if (status === 'connected') return t('Đã kết nối', 'Connected')
  if (status === 'coming_soon') return t('Sẽ hỗ trợ sau', 'Coming soon')
  if (status === 'unconfigured') return t('Chưa được cấu hình', 'Not configured')
  if (status === 'reconnect_required') return t('Cần kết nối lại', 'Reconnect required')
  return t('Chưa kết nối', 'Not connected')
}

const providerDescription = (provider) => {
  if (provider.provider === 'google-calendar') {
    return t(
      'Kết nối Google Calendar để đồng bộ cuộc họp, deadline và lịch sprint vào Unified Inbox.',
      'Connect Google Calendar to sync meetings, deadlines, and sprint schedules into Unified Inbox.'
    )
  }
  if (provider.provider === 'gmail') {
    return t('Kết nối Gmail bằng OAuth thật rồi đồng bộ email thật vào Unified Inbox.', 'Connect Gmail with real OAuth and sync real emails into Unified Inbox.')
  }
  if (provider.provider === 'slack') {
    return t('Kết nối Slack bằng OAuth thật rồi đồng bộ tin nhắn thật vào Unified Inbox.', 'Connect Slack with real OAuth and sync real messages into Unified Inbox.')
  }
  if (provider.provider === 'github') {
    return t('Đồng bộ các commits, issues và PRs cá nhân thành công việc. Kết nối qua webhook dự án ở phần Cài đặt.', 'Sync personal commits, issues, and PRs into tasks. Connect via project webhooks in Settings.')
  }
  return t('Kết nối Zalo OA / thông báo công việc sẽ được hỗ trợ ở phiên bản sau.', 'Sync notifications and task assignments from Zalo OA in future updates.')
}

const syncStatusLabel = (status) => {
  if (status === 'success') return t('Thành công', 'Success')
  if (status === 'error') return t('Thất bại', 'Failed')
  return status || t('Đang chạy', 'Running')
}

const previewText = (value) => {
  const text = value || t('Không có mô tả từ provider.', 'No provider description.')
  return text.length > 130 ? `${text.slice(0, 130)}...` : text
}

const formatDate = (value) => {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleString(i18nStore.locale === 'en' ? 'en-US' : 'vi-VN', {
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const formatFullDate = (value) => {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleString(i18nStore.locale === 'en' ? 'en-US' : 'vi-VN', {
    weekday: 'short',
    year: 'numeric',
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const handleAuthSessionChanged = () => {
  const nextAccountId = getIntegrationAccountId(getStoredUserSession())
  if (nextAccountId === accountId.value) return

  accountId.value = nextAccountId
  clearIntegrationState()
  if (nextAccountId) {
    void Promise.all([loadIntegrations(), loadInbox(), loadCreateTaskOptions()])
  }
}

onMounted(async () => {
  signalRService.on('EntityChanged', handleInboxRealtime)
  window.addEventListener('keydown', handleDetailKeydown)
  window.addEventListener('global-utility-drawer-opened', closeDetailForOtherUtility)
  window.addEventListener(AUTH_SESSION_CHANGED, handleAuthSessionChanged)
  await Promise.all([loadIntegrations(), loadInbox(), loadCreateTaskOptions()])
  await completeGoogleOAuth()
  normalizeHiddenInboxFilterState()
})

onUnmounted(() => {
  signalRService.off('EntityChanged', handleInboxRealtime)
  if (inboxRealtimeTimer) clearTimeout(inboxRealtimeTimer)
  window.removeEventListener('keydown', handleDetailKeydown)
  window.removeEventListener('global-utility-drawer-opened', closeDetailForOtherUtility)
  window.removeEventListener(AUTH_SESSION_CHANGED, handleAuthSessionChanged)
})
</script>

<style scoped>
.status-filters {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 14px;
  background: var(--color-surface);
  border-bottom: 1px solid color-mix(in srgb, var(--color-text-primary) 8%, transparent);
}

.status-filter-btn {
  font-size: 11px;
  font-weight: 500;
  padding: 5px 12px;
  border-radius: 12px;
  background: color-mix(in srgb, var(--color-text-primary) 4%, transparent);
  border: 1px solid transparent;
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: all 0.2s ease;
}

.status-filter-btn:hover {
  background: color-mix(in srgb, var(--color-text-primary) 8%, transparent);
  color: var(--color-text-primary);
}

.status-filter-btn.active {
  background: var(--color-accent);
  color: #fff;
  font-weight: 600;
}

.integration-page {
  --sa-page-x: 18px;
  width: 100%;
  min-height: 100%;
  padding: 0;
  margin: 0;
  background: var(--color-bg);
  color: var(--color-text-primary);
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  padding: 22px var(--sa-page-x, 24px) 18px;
  background: var(--color-surface);
  border-bottom: none !important;
  margin-bottom: 0 !important;
}

.page-content {
  width: 100%;
  max-width: none;
  margin: 0;
  padding: 18px;
  box-sizing: border-box;
}

.hero-copy {
  flex: 1;
  min-width: 0;
}

.eyebrow {
  color: var(--color-accent);
  font-size: 10px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  display: block;
}

h1,
h2,
h3,
p {
  margin-top: 0;
}

.hero-copy h1 {
  margin: 3px 0 4px;
  font-size: 22px;
  font-weight: 700;
  color: var(--color-text-primary);
  letter-spacing: 0;
  line-height: 1.2;
}

.hero-subtitle {
  max-width: 780px;
  margin: 0;
  color: var(--color-text-muted);
  font-size: 12px;
  line-height: 1.5;
}

.hero-action-card {
  display: flex;
  flex-direction: row;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 10px 14px;
  border: 1px solid color-mix(in srgb, var(--color-accent) 22%, var(--color-border));
  border-radius: 12px;
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--color-accent) 8%, var(--color-surface)), var(--color-surface)),
    var(--color-surface);
  box-shadow: 0 4px 16px color-mix(in srgb, var(--color-accent) 10%, transparent);
}

.hero-action-card.connected {
  border-color: color-mix(in srgb, var(--color-success) 28%, var(--color-border));
}

.hero-card-main {
  display: flex;
  flex-direction: column;
  gap: 3px;
  min-width: 0;
}

.hero-card-header {
  display: flex;
  align-items: center;
  gap: 8px;
}

.hero-card-header strong {
  font-size: 13px;
  font-weight: 700;
  color: var(--color-text-primary);
  white-space: nowrap;
}

.hero-card-main p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 11px;
  line-height: 1.3;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 340px;
}

.hero-status {
  padding: 2px 7px;
  border-radius: 5px;
  background: var(--sa-primary-soft);
  color: var(--color-accent);
  font-size: 10px;
  font-weight: 800;
  text-transform: uppercase;
  white-space: nowrap;
}

.hero-actions .cta {
  min-height: 32px;
  padding: 0 12px;
  font-size: 12px;
  font-weight: 700;
  border-radius: 7px;
  white-space: nowrap;
}

.primary,
.ghost,
.icon-btn,
.tabs button {
  border: 1px solid var(--color-border);
  border-radius: 8px;
  min-height: 36px;
  font-weight: 850;
  cursor: pointer;
  transition: transform .22s cubic-bezier(.2,.8,.2,1), border-color .22s, background .22s, box-shadow .22s;
}

.primary,
.ghost {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 8px 12px;
}

.primary {
  background: var(--color-accent);
  border-color: var(--color-accent);
  color: #fff;
  box-shadow: 0 10px 24px color-mix(in srgb, var(--color-accent) 22%, transparent);
}

.primary:hover:not(:disabled),
.ghost:hover:not(:disabled),
.icon-btn:hover:not(:disabled),
.tabs button:hover:not(:disabled) {
  transform: translateY(-1px);
}

.primary:active:not(:disabled),
.ghost:active:not(:disabled),
.icon-btn:active:not(:disabled),
.tabs button:active:not(:disabled) {
  transform: translateY(0) scale(.98);
}

.ghost,
.icon-btn {
  background: var(--color-surface);
  color: var(--color-text-primary);
}

.small {
  min-height: 32px;
  padding: 6px 10px;
  font-size: 12px;
}

.cta {
  min-width: 210px;
}

.full {
  width: 100%;
}

button:disabled {
  opacity: .62;
  cursor: not-allowed;
  box-shadow: none;
}

.icon-btn {
  width: 38px;
  height: 38px;
  display: grid;
  place-items: center;
}

.danger-text {
  color: var(--color-danger);
}

.notice {
  max-width: 1420px;
  align-items: center;
  gap: 10px;
  margin: 0 auto 14px;
  padding: 11px 12px;
  border-radius: 10px;
  border: 1px solid var(--color-border);
  background: var(--color-surface);
}

.notice.success {
  border-color: color-mix(in srgb, var(--color-success) 35%, var(--color-border));
  color: var(--color-success);
}

.notice.error {
  border-color: color-mix(in srgb, var(--color-danger) 35%, var(--color-border));
  color: var(--color-danger);
}

.notice button {
  margin-left: auto;
  border: 0;
  background: transparent;
  color: inherit;
  cursor: pointer;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  align-items: stretch;
  width: 100%;
  max-width: none;
  margin: 0 0 10px;
  gap: 10px;
}

.stats-grid article {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  min-width: 0;
  min-height: 78px;
  padding: 11px 13px;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 96%, #fff), color-mix(in srgb, var(--color-surface-hover) 42%, var(--color-surface)));
  box-shadow: 0 14px 38px color-mix(in srgb, #020617 8%, transparent);
}

.stats-grid span,
.detail-list dt {
  display: block;
  color: var(--color-text-secondary);
  font-size: 12px;
  font-weight: 850;
}

.stats-grid strong {
  display: block;
  margin-top: 5px;
  font-variant-numeric: tabular-nums;
  font-size: 20px;
}

.workspace-grid {
  display: grid;
  grid-template-columns: minmax(320px, 360px) minmax(0, 1fr);
  width: 100%;
  max-width: none;
  margin: 0;
  align-items: start;
  gap: 10px;
}

.panel {
  min-width: 0;
  border: 1px solid color-mix(in srgb, var(--color-border) 86%, transparent);
  border-radius: 14px;
  background:
    linear-gradient(180deg, color-mix(in srgb, var(--color-surface) 94%, transparent), color-mix(in srgb, var(--color-surface-hover) 36%, var(--color-surface)));
  box-shadow:
    0 20px 58px color-mix(in srgb, #020617 10%, transparent),
    inset 0 1px 0 rgba(255,255,255,0.12);
  overflow: hidden;
}

.apps-panel {
  width: auto;
  padding: 12px;
  overflow-y: auto;
}

.inbox-panel {
  width: 100%;
  justify-self: stretch;
}

.panel-head {
  align-items: flex-start;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 10px;
}

.inbox-title {
  margin: 0;
  padding: 12px 14px;
  border-bottom: 1px solid var(--color-border);
}

.panel-head h2,
.detail-head h2 {
  margin-bottom: 5px;
  font-size: 16px;
}

.panel-head p,
.provider-body p,
.empty-state p,
.detail-head p,
.content-box p,
.history-row span,
.history-row small {
  color: var(--color-text-secondary);
}

.provider-list {
  display: grid;
  gap: 9px;
}

.provider-card {
  align-items: flex-start;
  gap: 12px;
  flex-wrap: wrap;
  padding: 12px;
  border: 1px solid color-mix(in srgb, var(--color-border) 84%, transparent);
  border-radius: 12px;
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--color-surface-hover) 78%, var(--color-surface)), var(--color-surface));
  box-shadow: inset 0 1px 0 rgba(255,255,255,0.10);
}

.provider-card.featured {
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--color-accent) 7%, var(--color-surface-hover)), var(--color-surface-hover));
}

.provider-card.connected {
  border-color: color-mix(in srgb, var(--color-success) 34%, var(--color-border));
}

.provider-card.coming_soon {
  opacity: .78;
}

.provider-icon,
.source-icon {
  width: 38px;
  height: 38px;
  flex: 0 0 38px;
  display: grid;
  place-items: center;
  border-radius: 10px;
}

.calendar {
  background: rgba(16, 185, 129, .14);
  color: #059669;
}

.email {
  background: rgba(14, 165, 233, .14);
  color: #0284c7;
}

.slack {
  background: rgba(126, 87, 194, .14);
  color: #7e57c2;
}

.system {
  background: rgba(245, 158, 11, .14);
  color: #d97706;
}

.provider-body,
.item-main {
  flex: 1;
  min-width: 0;
}

.provider-body {
  min-width: min(250px, calc(100% - 54px));
}

.provider-top {
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.provider-body p {
  margin: 7px 0;
  font-size: 12px;
  line-height: 1.5;
}

.provider-meta {
  gap: 8px;
  flex-wrap: wrap;
}

.provider-meta span {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  color: var(--color-text-muted);
  font-size: 11px;
}

.provider-actions {
  width: 100%;
  flex: 1 0 100%;
  justify-content: flex-start;
  gap: 7px;
  flex-wrap: wrap;
  padding-left: 50px;
}

.provider-actions .primary,
.provider-actions .ghost {
  white-space: nowrap;
  width: auto;
  min-width: max-content;
}

.provider-actions .primary {
  flex: 1 1 148px;
}

.status-badge,
.item-tags span,
.section-title span {
  border-radius: 8px;
  padding: 3px 8px;
  font-size: 11px;
  font-weight: 900;
}

.status-badge.connected,
.item-tags .linked {
  color: var(--color-success);
  background: var(--color-success-bg);
}

.status-badge.not_connected {
  color: var(--color-danger);
  background: var(--color-danger-bg);
}

.status-badge.coming_soon {
  color: var(--color-warning);
  background: var(--color-warning-bg);
}

.status-badge.unconfigured,
.status-badge.reconnect_required {
  color: var(--color-warning);
  background: var(--color-warning-bg);
}

.sync-history,
.notification-center {
  margin-top: 12px;
}

.compact-empty {
  min-height: 88px;
  padding: 14px;
}

.section-title {
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
}

.section-title h3 {
  margin: 0;
  font-size: 14px;
}

.section-title span {
  background: var(--color-surface-hover);
  color: var(--color-text-secondary);
}

.history-empty,
.inline-state {
  display: grid;
  place-items: center;
  gap: 8px;
  min-height: 116px;
  padding: 18px;
  border: 1px dashed var(--color-border);
  border-radius: 12px;
  background: var(--color-surface-hover);
  text-align: center;
  color: var(--color-text-secondary);
}

.inline-state.error {
  color: var(--color-danger);
}

.history-list {
  display: grid;
  gap: 8px;
}

.history-row {
  align-items: flex-start;
  gap: 10px;
  padding: 10px;
  border-radius: 10px;
  background: var(--color-surface-hover);
}

.history-row i {
  margin-top: 2px;
  color: var(--color-accent);
}

.history-row.error i {
  color: var(--color-danger);
}

.history-row strong,
.history-row span,
.history-row small {
  display: block;
  font-size: 12px;
}

.tabs {
  gap: 7px;
  padding: 9px 12px;
  border-bottom: 1px solid var(--color-border);
  flex-wrap: wrap;
}

.tabs button {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  padding: 5px 8px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
}

.tabs button.active {
  color: var(--color-accent);
  border-color: color-mix(in srgb, var(--color-accent) 35%, var(--color-border));
  background: var(--sa-primary-soft);
}

.tabs span {
  min-width: 19px;
  padding: 1px 5px;
  border-radius: 999px;
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
  font-size: 11px;
}

.bulk-toolbar {
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border-bottom: 1px solid var(--color-border);
  background: color-mix(in srgb, var(--color-surface-hover) 72%, var(--color-surface));
}

.bulk-select-all {
  align-items: center;
  gap: 7px;
  min-width: 0;
  color: var(--color-text-secondary);
  font-size: 12px;
  font-weight: 800;
}

.bulk-select-all input,
.bulk-check {
  width: 15px;
  height: 15px;
  accent-color: var(--color-accent);
}

.bulk-count {
  margin-left: auto;
  color: var(--color-text-muted);
  font-size: 12px;
  font-weight: 800;
}

.inbox-list {
  max-height: 680px;
  overflow-y: auto;
  overscroll-behavior: contain;
  scrollbar-gutter: stable;
}

.inbox-item {
  width: 100%;
  align-items: flex-start;
  gap: 9px;
  padding: 10px 12px;
  border: 0;
  border-bottom: 1px solid var(--color-border);
  background: transparent;
  color: inherit;
  text-align: left;
  cursor: pointer;
}

.inbox-item:hover,
.inbox-item.selected {
  background:
    linear-gradient(90deg, color-mix(in srgb, var(--color-accent) 8%, var(--color-surface-hover)), var(--color-surface-hover));
}

.inbox-item.selected {
  box-shadow: inset 3px 0 0 var(--color-accent);
}

.item-title {
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.item-title strong {
  font-size: 13px;
  line-height: 1.35;
}

.item-title small {
  flex: 0 0 auto;
  color: var(--color-text-muted);
  font-size: 11px;
}

.item-copy {
  display: block;
  margin: 4px 0 7px;
  color: var(--color-text-secondary);
  font-size: 12px;
  line-height: 1.45;
}

.item-tags {
  gap: 6px;
  flex-wrap: wrap;
}

.item-tags span {
  color: var(--color-text-secondary);
  background: var(--color-surface);
}

.item-tags .unread-label {
  color: var(--color-accent);
  background: var(--sa-primary-soft);
}

.unread-dot {
  width: 8px;
  height: 8px;
  margin-top: 12px;
  border-radius: 999px;
  background: var(--color-accent);
}

.empty-state {
  min-height: 330px;
  display: grid;
  place-items: center;
  align-content: center;
  gap: 10px;
  margin: 16px;
  padding: 28px;
  border: 1px dashed color-mix(in srgb, var(--color-accent) 28%, var(--color-border));
  border-radius: 14px;
  background: var(--color-surface-hover);
  text-align: center;
}

.empty-state > i {
  width: 44px;
  height: 44px;
  display: grid;
  place-items: center;
  border-radius: 12px;
  background: var(--sa-primary-soft);
  color: var(--color-accent);
}

.empty-state strong {
  font-size: 16px;
}

.empty-state p {
  max-width: 480px;
  margin-bottom: 4px;
  line-height: 1.55;
}

.empty-state.error {
  border-color: color-mix(in srgb, var(--color-danger) 30%, var(--color-border));
}

.empty-state.error > i {
  color: var(--color-danger);
  background: var(--color-danger-bg);
}

.detail-head {
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
}

.detail-head h2 {
  line-height: 1.35;
}

.source-icon.large {
  width: 46px;
  height: 46px;
  flex-basis: 46px;
  font-size: 18px;
}

.detail-list,
.content-box,
.task-target-box,
.ai-box {
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-surface-hover);
}

.detail-list {
  display: grid;
  gap: 10px;
  margin: 0 0 14px;
  padding: 14px;
}

.detail-list dd {
  margin: 3px 0 0;
  word-break: break-word;
}

.content-box {
  margin-bottom: 14px;
  padding: 14px;
}

.content-box h3 {
  margin-bottom: 8px;
  font-size: 13px;
}

.content-box p {
  margin: 0;
  line-height: 1.55;
}

.task-target-box,
.ai-box {
  margin-bottom: 12px;
  padding: 12px;
}

.section-title.compact {
  margin-bottom: 8px;
}

.section-title.compact h3 {
  font-size: 13px;
}

.text-action {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  border: 0;
  background: transparent;
  color: var(--color-accent);
  font-size: 12px;
  font-weight: 850;
  cursor: pointer;
}

.project-select-label {
  display: block;
  margin-bottom: 6px;
  color: var(--color-text-secondary);
  font-size: 11px;
  font-weight: 900;
  text-transform: uppercase;
}

.project-select {
  width: 100%;
  min-height: 36px;
  padding: 7px 10px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface);
  color: var(--color-text-primary);
  font-weight: 800;
}

.mini-state {
  margin: 8px 0 0;
  color: var(--color-text-secondary);
  font-size: 12px;
  line-height: 1.45;
}

.mini-state.error,
.ai-result.error {
  color: var(--color-warning);
}

.ai-actions {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 7px;
}

.ai-actions .ghost:last-child {
  grid-column: 1 / -1;
}

.ai-result {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  margin: 10px 0 0;
  padding: 10px;
  border: 1px solid color-mix(in srgb, var(--color-accent) 18%, var(--color-border));
  border-radius: 10px;
  background: color-mix(in srgb, var(--color-accent) 7%, var(--color-surface));
  color: var(--color-text-secondary);
  font-size: 12px;
  line-height: 1.45;
  white-space: pre-line;
}

.ai-candidates {
  display: grid;
  gap: 8px;
  margin-top: 10px;
}

.ai-candidates-head,
.ai-candidate-actions,
.ai-candidate-select {
  display: flex;
  align-items: center;
  gap: 8px;
}

.ai-candidates-head {
  justify-content: space-between;
  color: var(--color-text-primary);
  font-size: 12px;
}

.ai-candidate-card {
  display: grid;
  gap: 7px;
  padding: 10px;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-surface);
}

.ai-candidate-select {
  color: var(--color-text-secondary);
  font-size: 11px;
  font-weight: 800;
}

.ai-candidate-body {
  min-width: 0;
}

.ai-candidate-body strong,
.ai-candidate-body p {
  display: block;
  margin: 0;
  overflow-wrap: anywhere;
}

.ai-candidate-body p {
  margin-top: 4px;
  color: var(--color-text-secondary);
  font-size: 12px;
  line-height: 1.4;
}

.ai-candidate-body small,
.ai-candidate-evidence small {
  color: var(--color-text-tertiary);
  font-size: 10px;
}

.ai-candidate-evidence {
  display: grid;
  gap: 2px;
}

.ai-candidate-edit {
  display: grid;
  gap: 6px;
}

.ai-candidate-edit input,
.ai-candidate-edit textarea,
.ai-candidate-edit select {
  width: 100%;
  box-sizing: border-box;
  padding: 7px 8px;
  border: 1px solid var(--color-border);
  border-radius: 7px;
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
  font: inherit;
}

.ai-candidate-actions {
  flex-wrap: wrap;
}

.integration-detail-layer {
  position: fixed;
  z-index: 1550;
  inset: var(--sa-topbar-height, 52px) 0 0;
  display: flex;
  justify-content: flex-end;
}

.integration-detail-backdrop {
  position: absolute;
  inset: 0;
  width: 100%;
  padding: 0;
  border: 0;
  background: color-mix(in srgb, #020617 48%, transparent);
  cursor: default;
}

.integration-detail-drawer {
  position: relative;
  width: min(560px, calc(100vw - 24px));
  min-width: 0;
  display: flex;
  flex-direction: column;
  border-left: 1px solid var(--color-border);
  background: var(--color-surface);
  color: var(--color-text-primary);
  box-shadow: -18px 0 52px color-mix(in srgb, #020617 24%, transparent);
}

.integration-detail-header {
  min-height: 58px;
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) 38px;
  align-items: center;
  gap: 10px;
  flex: 0 0 auto;
  padding: 10px 14px;
  border-bottom: 1px solid var(--color-border);
  background: var(--color-surface);
}

.integration-detail-header > strong {
  overflow: hidden;
  text-align: center;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.detail-back-action,
.detail-close-action {
  min-height: 38px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: transparent;
  color: var(--color-text-primary);
  cursor: pointer;
}

.detail-back-action {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  padding: 0 10px;
  font-weight: 800;
}

.detail-close-action {
  width: 38px;
  display: grid;
  place-items: center;
  padding: 0;
}

.detail-back-action:hover,
.detail-close-action:hover {
  border-color: var(--color-accent);
  color: var(--color-accent);
}

.integration-detail-body {
  min-height: 0;
  padding: 16px;
  overflow-y: auto;
  overscroll-behavior: contain;
  scrollbar-gutter: stable;
}

.integration-detail-state {
  min-height: 0;
  display: grid;
  place-items: center;
  align-content: center;
  gap: 12px;
  flex: 1;
  padding: 28px;
  color: var(--color-text-secondary);
  text-align: center;
}

.integration-detail-state > i {
  color: var(--color-accent);
  font-size: 24px;
}

.integration-detail-state.error > i {
  color: var(--color-danger);
}

.integration-drawer-enter-active,
.integration-drawer-leave-active {
  transition: opacity 180ms ease;
}

.integration-drawer-enter-active .integration-detail-drawer,
.integration-drawer-leave-active .integration-detail-drawer {
  transition: transform 220ms ease;
}

.integration-drawer-enter-from,
.integration-drawer-leave-to {
  opacity: 0;
}

.integration-drawer-enter-from .integration-detail-drawer,
.integration-drawer-leave-to .integration-detail-drawer {
  transform: translateX(100%);
}

.skeleton-card,
.inbox-skeleton {
  pointer-events: none;
}

.skeleton-card span,
.inbox-skeleton span,
.skeleton-card b,
.skeleton-card small,
.inbox-skeleton b,
.inbox-skeleton small {
  display: block;
  border-radius: 10px;
  background: linear-gradient(90deg, var(--color-surface-hover), color-mix(in srgb, var(--color-border) 55%, var(--color-surface)), var(--color-surface-hover));
  background-size: 200% 100%;
  animation: shimmer 1.1s infinite linear;
}

.skeleton-card > span,
.inbox-skeleton > span {
  width: 38px;
  height: 38px;
  flex: 0 0 38px;
}

.skeleton-card div,
.inbox-skeleton div {
  flex: 1;
}

.skeleton-card b,
.inbox-skeleton b {
  width: 46%;
  height: 14px;
  margin-bottom: 10px;
}

.skeleton-card small,
.inbox-skeleton small {
  width: 82%;
  height: 10px;
  margin-top: 7px;
}

.inbox-skeleton {
  display: flex;
  gap: 12px;
  padding: 15px 16px;
  border-bottom: 1px solid var(--color-border);
}

@keyframes shimmer {
  to { background-position: -200% 0; }
}

@media (prefers-reduced-motion: reduce) {
  *,
  *::before,
  *::after {
    transition-duration: .01ms !important;
    animation-duration: .01ms !important;
    animation-iteration-count: 1 !important;
  }
}

@media (max-width: 1024px) {
  .workspace-grid {
    grid-template-columns: 1fr;
  }

  .apps-panel {
    width: 100%;
  }

  .inbox-list {
    max-height: 620px;
  }
}

@media (max-width: 820px) {
  .integration-page {
    padding: 16px;
  }

  .hero-shell,
  .stats-grid,
  .panel-head,
  .provider-card,
  .item-title {
    flex-direction: column;
  }

  .hero-action-card {
    width: 100%;
  }

  .stats-grid article {
    width: 100%;
  }

  .stats-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .provider-actions,
  .hero-actions {
    justify-content: flex-start;
    padding-left: 0;
  }

  .primary,
  .ghost {
    width: 100%;
    min-width: 0;
  }

  .integration-detail-layer {
    inset: 0;
  }

  .integration-detail-drawer {
    width: 100%;
  }

  .integration-detail-header {
    padding-top: max(10px, env(safe-area-inset-top));
  }

  .integration-detail-body {
    padding-bottom: max(16px, env(safe-area-inset-bottom));
  }
}

@media (max-width: 520px) {
  .integration-page {
    padding-inline: 12px;
  }

  .stats-grid {
    grid-template-columns: 1fr;
  }

  .inbox-list {
    max-height: none;
  }

  .integration-detail-header {
    grid-template-columns: auto minmax(0, 1fr) 38px;
  }

  .detail-back-action {
    width: 38px;
    justify-content: center;
    padding: 0;
    font-size: 0;
  }

  .detail-back-action i {
    font-size: 14px;
  }
}

.hero-action-card,
.stats-grid article,
.panel {
  box-shadow: var(--sp-shadow-xs);
}

.primary {
  border-color: var(--sp-blue-700);
  background: var(--sp-blue-700);
  box-shadow: 0 8px 18px color-mix(in srgb, var(--sp-blue-700) 20%, transparent);
}

.tabs button.active,
.status-filter-btn.active {
  color: #fff;
  background: var(--sp-blue-600);
}
</style>
