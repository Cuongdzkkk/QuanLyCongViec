<template>
  <AdminLayout>
    <div v-if="isPaymentsPage" class="payment-center">
      <header class="payment-header">
        <div>
          <p class="section-kicker">ADMIN / BILLING</p>
          <h1>Thanh toán</h1>
          <p class="page-description">Theo dõi giao dịch, gói đăng ký và các vấn đề cần xử lý.</p>
        </div>
        <div class="header-actions">
          <span v-if="lastUpdatedAt" class="last-updated">Cập nhật {{ formatTime(lastUpdatedAt) }}</span>
          <button type="button" class="refresh-button" :disabled="paymentLoading" @click="refreshPayments">
            <RefreshCw :size="16" :class="{ spinning: paymentLoading }" aria-hidden="true" />
            Làm mới
          </button>
        </div>
      </header>

      <div v-if="ordersError" class="inline-error" role="alert">
        <CircleAlert :size="17" aria-hidden="true" />
        <span>{{ ordersError }}</span>
        <button type="button" class="text-button" @click="refreshPayments">Thử lại</button>
      </div>

      <section class="kpi-grid" aria-label="Tổng quan thanh toán">
        <article class="kpi-item">
          <span class="kpi-label">Doanh thu</span>
          <strong class="kpi-value money-number">{{ money(paymentSummary.revenueVnd) }}</strong>
          <span class="kpi-meta">{{ paymentSummary.successfulPayments }} giao dịch đã thanh toán</span>
        </article>
        <article class="kpi-item">
          <span class="kpi-label">Thanh toán thành công</span>
          <strong class="kpi-value number-value">{{ paymentSummary.successfulPayments }}</strong>
          <span class="kpi-meta">Trong dữ liệu thanh toán hiện có</span>
        </article>
        <article class="kpi-item">
          <span class="kpi-label">Đang chờ</span>
          <strong class="kpi-value number-value">{{ paymentSummary.pendingPayments }}</strong>
          <span class="kpi-meta">Cần đối soát hoặc chờ thanh toán</span>
        </article>
        <article class="kpi-item kpi-attention">
          <span class="kpi-label">Cần xử lý</span>
          <strong class="kpi-value number-value">{{ paymentSummary.needsAttention }}</strong>
          <span class="kpi-meta">Dựa trên trạng thái đơn và subscription</span>
        </article>
      </section>

      <nav class="status-navigation" aria-label="Lọc theo trạng thái thanh toán">
          <button
          v-for="item in paymentStatusItems"
          :key="item.value"
          type="button"
          class="status-nav-item"
          :class="{ active: paymentStatusFilter === item.value }"
          :aria-current="paymentStatusFilter === item.value ? 'page' : undefined"
          @click="setPaymentStatus(item.value)"
        >
          <span>{{ item.label }}</span>
          <span class="count-badge">{{ item.count }}</span>
        </button>
      </nav>

      <section class="payment-surface" aria-labelledby="payment-table-title">
        <div class="filter-toolbar">
          <label class="search-field">
            <span class="sr-only">Tìm kiếm thanh toán</span>
            <Search :size="17" aria-hidden="true" />
            <input v-model="paymentSearch" type="search" placeholder="Tìm tên, email, mã đơn hoặc mã chuyển khoản" />
            <button v-if="paymentSearch" type="button" class="clear-search" aria-label="Xóa tìm kiếm" @click="paymentSearch = ''">
              <X :size="15" aria-hidden="true" />
            </button>
          </label>
          <el-select v-model="paymentPlanFilter" class="filter-select" clearable placeholder="Gói">
            <el-option v-for="plan in paymentPlanOptions" :key="plan.value" :label="plan.label" :value="plan.value" />
          </el-select>
          <el-select v-model="paymentProviderFilter" class="filter-select" clearable placeholder="Provider">
            <el-option v-for="provider in paymentProviderOptions" :key="provider" :label="provider" :value="provider" />
          </el-select>
          <el-date-picker
            v-model="paymentDateRange"
            class="date-filter"
            type="daterange"
            range-separator="đến"
            start-placeholder="Từ ngày"
            end-placeholder="Đến ngày"
            format="DD/MM/YYYY"
            value-format="YYYY-MM-DD"
            unlink-panels
          />
          <button type="button" class="reset-button" :disabled="!hasPaymentFilters" @click="resetPaymentFilters">
            Xóa lọc
          </button>
        </div>

        <div class="table-toolbar">
          <div>
            <h2 id="payment-table-title">Giao dịch</h2>
            <p>{{ paymentTotal }} kết quả <span v-if="orders.length !== paymentTotal">trên trang hiện tại</span></p>
          </div>
          <span class="data-scope">Nguồn: API thanh toán hiện tại</span>
        </div>

        <div v-if="paymentLoading" class="table-skeleton" aria-label="Đang tải giao dịch" aria-busy="true">
          <div v-for="row in 6" :key="row" class="skeleton-row">
            <span class="skeleton-block skeleton-customer"></span>
            <span class="skeleton-block"></span>
            <span class="skeleton-block skeleton-short"></span>
            <span class="skeleton-block skeleton-amount"></span>
            <span class="skeleton-block skeleton-status"></span>
            <span class="skeleton-block skeleton-action"></span>
          </div>
        </div>

        <div v-else-if="!filteredOrders.length" class="empty-state">
          <div class="empty-state-icon"><SearchX :size="22" aria-hidden="true" /></div>
          <h3>{{ hasPaymentFilters ? 'Không tìm thấy giao dịch phù hợp' : 'Chưa có giao dịch thanh toán' }}</h3>
          <p>{{ hasPaymentFilters ? 'Thử đổi trạng thái hoặc xóa bớt điều kiện lọc.' : 'Các đơn thanh toán sẽ xuất hiện ở đây khi hệ thống ghi nhận.' }}</p>
          <button v-if="hasPaymentFilters" type="button" class="text-button" @click="resetPaymentFilters">Xóa bộ lọc</button>
        </div>

          <div v-else class="payment-table-wrap">
          <el-table
            :data="filteredOrders"
            class="payment-table"
            row-key="id"
            table-layout="fixed"
            @row-click="openPaymentDetail"
          >
            <el-table-column label="Khách hàng" min-width="210">
              <template #default="{ row }">
                <div class="customer-cell">
                  <span class="avatar">{{ initials(row.userName) }}</span>
                  <span class="customer-copy"><strong>{{ row.userName || 'Chưa có tên' }}</strong><small>{{ row.email || 'Chưa có email' }}</small></span>
                </div>
              </template>
            </el-table-column>
            <el-table-column label="Giao dịch" min-width="180">
              <template #default="{ row }">
                <div class="transaction-cell">
                  <code>{{ row.transferCode || 'Chưa có mã' }}</code>
                  <small>{{ shortId(row.id) }} <span v-if="providerReference(row)">· {{ providerReference(row) }}</span></small>
                </div>
              </template>
            </el-table-column>
            <el-table-column label="Gói" min-width="118">
              <template #default="{ row }"><span class="plan-label">{{ row.planName || row.planCode || '-' }}</span></template>
            </el-table-column>
            <el-table-column label="Số tiền" min-width="142" align="right">
              <template #default="{ row }"><strong class="money-number amount-cell">{{ money(row.amountVnd) }}</strong></template>
            </el-table-column>
            <el-table-column label="Thanh toán" min-width="132">
              <template #default="{ row }"><span class="status-badge" :class="statusClass(row.status)">{{ paymentStatusLabel(row.status) }}</span></template>
            </el-table-column>
            <el-table-column label="System" min-width="142">
              <template #default="{ row }"><span class="system-state" :class="systemClass(row)">{{ systemStatusLabel(row) }}</span></template>
            </el-table-column>
            <el-table-column label="Thời gian" min-width="148">
              <template #default="{ row }"><span class="time-cell">{{ formatDateTime(row.paidAt || row.createdAt) }}<small>{{ row.paidAt ? 'Đã thanh toán' : 'Tạo đơn' }}</small></span></template>
            </el-table-column>
            <el-table-column label="" width="62" align="right" fixed="right">
              <template #default="{ row }">
                <button type="button" class="detail-button" :aria-label="`Mở chi tiết thanh toán của ${row.userName || row.id}`" @click.stop="openPaymentDetail(row)">
                  <ArrowUpRight :size="17" aria-hidden="true" />
                </button>
              </template>
            </el-table-column>
            </el-table>
          </div>
          <nav v-if="paymentPages > 1" class="pagination-controls" aria-label="Phân trang thanh toán">
            <button type="button" :disabled="paymentLoading || paymentPage === 1" @click="goPaymentPage(paymentPage - 1)">Trước</button>
            <span aria-live="polite">Trang {{ paymentPage }} / {{ paymentPages }} · {{ paymentTotal }} kết quả</span>
            <button type="button" :disabled="paymentLoading || paymentPage >= paymentPages" @click="goPaymentPage(paymentPage + 1)">Sau</button>
          </nav>
        </section>
    </div>

    <div v-else class="billing-admin">
      <header class="page-header">
        <div><p class="breadcrumb">SYSTEM / BILLING</p><h1>{{ t('Billing & AI Credits', 'Billing & AI Credits') }}</h1><span>{{ t('Quản lý gói, kỳ sử dụng, AI credits và thanh toán thủ công.', 'Manage plans, billing periods, AI credits and manual payments.') }}</span></div>
        <button type="button" class="refresh-button" @click="loadAll"><RefreshCw :size="16" /> {{ t('Làm mới', 'Refresh') }}</button>
      </header>

      <div class="summary-strip">
        <div><span>{{ t('Người dùng', 'Users') }}</span><strong>{{ users.length }}</strong></div>
        <div><span>{{ t('Gói trả phí đang hoạt động', 'Active paid plans') }}</span><strong>{{ activePaidCount }}</strong></div>
        <div><span>{{ t('Đơn chờ duyệt', 'Pending payments') }}</span><strong>{{ pendingOrders.length }}</strong></div>
      </div>

      <el-tabs v-model="activeTab" class="billing-tabs" @tab-change="syncRoute">
        <el-tab-pane :label="t('Subscriptions & Credits', 'Subscriptions & Credits')" name="subscriptions">
          <section class="admin-surface" v-loading="loadingUsers">
            <div class="surface-toolbar"><h2>{{ t('Quyền lợi theo người dùng', 'User entitlements') }}</h2><el-input v-model="search" clearable :placeholder="t('Tìm tên hoặc email', 'Search name or email')" style="width: 260px" /></div>
            <el-table :data="filteredUsers" class="billing-table" table-layout="auto">
              <el-table-column :label="t('Người dùng', 'User')" min-width="210"><template #default="{ row }"><div class="user-cell"><span class="avatar">{{ initials(row.userName) }}</span><div><strong>{{ row.userName }}</strong><small>{{ row.email }}</small></div></div></template></el-table-column>
              <el-table-column :label="t('Gói', 'Plan')" min-width="100"><template #default="{ row }"><span class="plan-badge">{{ row.planName }}</span></template></el-table-column>
              <el-table-column :label="t('Trạng thái', 'Status')" min-width="120"><template #default="{ row }"><span class="status-text" :class="row.subscriptionStatus.toLowerCase()">{{ statusLabel(row.subscriptionStatus) }}</span></template></el-table-column>
              <el-table-column :label="t('Kỳ hiện tại', 'Current period')" min-width="190"><template #default="{ row }"><div class="period-cell"><span>{{ formatDate(row.currentPeriodStart) }}</span><small>{{ formatDate(row.currentPeriodEnd) }}</small></div></template></el-table-column>
              <el-table-column :label="t('Credits', 'Credits')" min-width="190"><template #default="{ row }"><div class="credit-cell"><div><strong>{{ row.remainingCredits }}</strong><span>{{ t('còn lại', 'remaining') }}</span></div><small>{{ row.usedCredits }} {{ t('đã dùng', 'used') }} / {{ row.includedCredits + (row.adjustmentCredits || 0) }}</small></div></template></el-table-column>
              <el-table-column align="right" min-width="150"><template #default="{ row }"><el-dropdown trigger="click" @command="command => openAction(command, row)"><button type="button" class="row-action">{{ t('Thao tác', 'Actions') }} <ChevronDown :size="14" /></button><template #dropdown><el-dropdown-menu><el-dropdown-item command="change">{{ t('Đổi / kích hoạt gói', 'Change / activate plan') }}</el-dropdown-item><el-dropdown-item command="extend">{{ t('Gia hạn một tháng', 'Extend one month') }}</el-dropdown-item><el-dropdown-item command="adjust">{{ t('Điều chỉnh AI credits', 'Adjust AI credits') }}</el-dropdown-item><el-dropdown-item command="reset" divided>{{ t('ADMIN/TEST: Reset usage kỳ này', 'ADMIN/TEST: Reset current usage') }}</el-dropdown-item><el-dropdown-item command="cancel">{{ t('Hủy gói trả phí', 'Cancel paid plan') }}</el-dropdown-item></el-dropdown-menu></template></el-dropdown></template></el-table-column>
            </el-table>
            <div v-if="!loadingUsers && !filteredUsers.length" class="empty-state">{{ t('Không có người dùng phù hợp.', 'No matching users.') }}</div>
          </section>
        </el-tab-pane>

        <el-tab-pane :label="`${t('Payments', 'Payments')} (${pendingOrders.length})`" name="payments">
          <section class="admin-surface" v-loading="loadingOrders">
            <div class="surface-toolbar"><div><h2>{{ t('Đơn thanh toán thủ công', 'Manual payment orders') }}</h2><p>{{ t('Chỉ quản trị viên mới có thể xác nhận đã thanh toán.', 'Only administrators can mark an order as paid.') }}</p></div><el-select v-model="orderStatus" style="width: 180px" @change="loadOrders"><el-option :label="t('Đang chờ', 'Pending')" value="Pending" /><el-option :label="t('Tất cả', 'All')" value="" /></el-select></div>
            <el-table :data="orders" class="billing-table">
              <el-table-column :label="t('Người dùng', 'User')" min-width="210"><template #default="{ row }"><div class="user-cell"><span class="avatar">{{ initials(row.userName) }}</span><div><strong>{{ row.userName }}</strong><small>{{ row.email }}</small></div></div></template></el-table-column>
              <el-table-column :label="t('Gói', 'Plan')" prop="planName" min-width="100" />
              <el-table-column :label="t('Số tiền', 'Amount')" min-width="130"><template #default="{ row }"><strong>{{ money(row.amountVnd) }}</strong></template></el-table-column>
              <el-table-column :label="t('Mã chuyển khoản', 'Transfer code')" min-width="160"><template #default="{ row }"><code class="transfer-code">{{ row.transferCode }}</code></template></el-table-column>
              <el-table-column :label="t('Tạo lúc', 'Created')" min-width="160"><template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template></el-table-column>
              <el-table-column :label="t('Trạng thái', 'Status')" min-width="110"><template #default="{ row }"><span class="status-text" :class="row.status.toLowerCase()">{{ statusLabel(row.status) }}</span></template></el-table-column>
              <el-table-column align="right" min-width="180"><template #default="{ row }"><div v-if="row.status === 'Pending'" class="payment-actions"><button type="button" class="approve-button" @click="reviewOrder(row, 'approve')">{{ t('Duyệt', 'Approve') }}</button><button type="button" class="reject-button" @click="reviewOrder(row, 'reject')">{{ t('Từ chối', 'Reject') }}</button></div></template></el-table-column>
            </el-table>
            <div v-if="!loadingOrders && !orders.length" class="empty-state">{{ t('Không có đơn thanh toán trong trạng thái này.', 'No payment orders in this state.') }}</div>
          </section>
        </el-tab-pane>

        <el-tab-pane :label="t('Plan Configuration', 'Plan Configuration')" name="plans">
          <section class="admin-surface" v-loading="loadingPlans">
            <div class="surface-toolbar"><div><h2>{{ t('Cấu hình bảng giá', 'Pricing configuration') }}</h2><p>{{ t('Không xóa gói lịch sử. Giá công khai đọc trực tiếp từ bảng này.', 'Historical plans cannot be deleted. Public pricing reads directly from this table.') }}</p></div></div>
            <el-table :data="plans" class="billing-table">
              <el-table-column :label="t('Gói', 'Plan')" min-width="150"><template #default="{ row }"><div class="plan-name"><strong>{{ row.name }}</strong><code>{{ row.code }}</code></div></template></el-table-column>
              <el-table-column :label="t('Giá tháng (VND)', 'Monthly price (VND)')" min-width="180"><template #default="{ row }"><el-input-number v-if="row.code !== 'enterprise'" v-model="row.monthlyPriceVnd" :min="0" :step="1000" controls-position="right" /></template></el-table-column>
              <el-table-column :label="t('AI credits', 'AI credits')" min-width="150"><template #default="{ row }"><el-input-number v-model="row.includedAiCredits" :min="0" :step="100" controls-position="right" /></template></el-table-column>
              <el-table-column :label="t('Công khai', 'Published')" width="105"><template #default="{ row }"><el-switch v-model="row.isPublished" /></template></el-table-column>
              <el-table-column :label="t('Đề xuất', 'Recommended')" width="105"><template #default="{ row }"><el-switch v-model="row.isRecommended" /></template></el-table-column>
              <el-table-column align="right" width="120"><template #default="{ row }"><button type="button" class="save-button" :disabled="row.code === 'enterprise' || savingPlan === row.code" @click="savePlan(row)">{{ t('Lưu', 'Save') }}</button></template></el-table-column>
            </el-table>
          </section>
        </el-tab-pane>
      </el-tabs>

      <el-dialog v-model="actionDialog" :title="actionTitle" width="min(480px, calc(100vw - 32px))" destroy-on-close>
        <p class="dialog-user">{{ actionUser?.userName }} <span>{{ actionUser?.email }}</span></p>
        <el-form label-position="top">
          <el-form-item v-if="actionKind === 'change'" :label="t('Gói mới', 'New plan')"><el-select v-model="actionForm.planCode" style="width: 100%"><el-option v-for="planItem in plans" :key="planItem.code" :label="planItem.name" :value="planItem.code" /></el-select></el-form-item>
          <el-form-item v-if="actionKind === 'change' && actionForm.planCode !== 'free'" :label="t('Tự động gia hạn', 'Auto renew')"><el-switch v-model="actionForm.autoRenew" /></el-form-item>
          <el-form-item v-if="actionKind === 'adjust'" :label="t('Số credits điều chỉnh', 'Credit adjustment')"><el-input-number v-model="actionForm.amount" :min="-100000" :max="100000" style="width: 100%" /></el-form-item>
          <el-form-item :label="t('Lý do', 'Reason')" required><el-input v-model="actionForm.reason" type="textarea" :rows="3" maxlength="500" show-word-limit /></el-form-item>
          <el-alert v-if="actionKind === 'reset'" type="warning" :closable="false" :title="t('ADMIN/TEST: thao tác này reset usage hiệu lực cho kỳ hiện tại nhưng vẫn giữ nguyên lịch sử token và ledger.', 'ADMIN/TEST: this resets effective usage for the current period while preserving token and ledger history.')" />
        </el-form>
        <template #footer><button type="button" class="cancel-button" @click="actionDialog = false">{{ t('Đóng', 'Close') }}</button><button type="button" class="confirm-button" :disabled="actionSubmitting || !actionForm.reason.trim()" @click="submitAction">{{ actionSubmitting ? t('Đang xử lý...', 'Processing...') : t('Xác nhận', 'Confirm') }}</button></template>
      </el-dialog>
    </div>

    <el-drawer v-model="paymentDrawerOpen" class="payment-drawer" direction="rtl" size="min(520px, 100vw)" :with-header="false" destroy-on-close>
      <div v-if="selectedOrder" class="drawer-content">
        <header class="drawer-header">
          <div><button type="button" class="drawer-back" @click="paymentDrawerOpen = false"><ArrowLeft :size="16" aria-hidden="true" /> Đóng</button><h2>Chi tiết thanh toán</h2><p>{{ shortId(selectedOrder.id) }} · {{ formatDateTime(selectedOrder.createdAt) }}</p></div>
          <button type="button" class="drawer-close" aria-label="Đóng chi tiết" @click="paymentDrawerOpen = false"><X :size="19" aria-hidden="true" /></button>
        </header>

        <div class="drawer-status-line">
          <span class="status-badge" :class="statusClass(selectedOrder.status)">{{ paymentStatusLabel(selectedOrder.status) }}</span>
          <span class="system-state" :class="systemClass(selectedOrder)">{{ systemStatusLabel(selectedOrder) }}</span>
        </div>

        <section class="drawer-section">
          <h3><UserRound :size="16" aria-hidden="true" /> Khách hàng</h3>
          <dl class="detail-grid">
            <div><dt>Tên</dt><dd>{{ selectedOrder.userName || '-' }}</dd></div>
            <div><dt>Email</dt><dd>{{ selectedOrder.email || '-' }}</dd></div>
          </dl>
        </section>

        <section class="drawer-section">
          <h3><Receipt :size="16" aria-hidden="true" /> Thanh toán</h3>
          <dl class="detail-grid">
            <div><dt>Mã đơn</dt><dd class="mono-value">{{ selectedOrder.id || '-' }}</dd></div>
            <div><dt>Gói</dt><dd>{{ selectedOrder.planName || selectedOrder.planCode || '-' }}</dd></div>
            <div><dt>Số tiền</dt><dd class="money-number">{{ money(selectedOrder.amountVnd) }}</dd></div>
            <div><dt>Tiền tệ</dt><dd>{{ selectedOrder.currency || 'VND' }}</dd></div>
            <div><dt>Provider</dt><dd>{{ selectedOrder.provider || '-' }}</dd></div>
            <div><dt>Mã chuyển khoản</dt><dd class="mono-value">{{ selectedOrder.transferCode || '-' }}</dd></div>
            <div><dt>Provider reference</dt><dd class="mono-value">{{ providerReference(selectedOrder) || 'Chưa có trong API admin' }}</dd></div>
            <div><dt>Tạo lúc</dt><dd>{{ formatDateTime(selectedOrder.createdAt) }}</dd></div>
            <div><dt>Thanh toán lúc</dt><dd>{{ formatDateTime(selectedOrder.paidAt) }}</dd></div>
            <div v-if="selectedOrder.adminNote"><dt>Ghi chú admin</dt><dd>{{ selectedOrder.adminNote }}</dd></div>
          </dl>
        </section>

        <section class="drawer-section">
          <h3><BadgeCheck :size="16" aria-hidden="true" /> Subscription</h3>
          <dl v-if="selectedCustomer" class="detail-grid">
            <div><dt>Gói hiện tại</dt><dd>{{ selectedCustomer.planName || selectedCustomer.planCode || '-' }}</dd></div>
            <div><dt>Trạng thái</dt><dd>{{ statusLabel(selectedCustomer.subscriptionStatus) }}</dd></div>
            <div><dt>Bắt đầu kỳ</dt><dd>{{ formatDate(selectedCustomer.currentPeriodStart) }}</dd></div>
            <div><dt>Kết thúc kỳ</dt><dd>{{ formatDate(selectedCustomer.currentPeriodEnd) }}</dd></div>
          </dl>
          <p v-else class="data-unavailable">Billing summary của khách hàng chưa được trả về.</p>
        </section>

        <section class="drawer-section">
          <h3><Sparkles :size="16" aria-hidden="true" /> AI credits</h3>
          <dl v-if="selectedCustomer" class="detail-grid">
            <div><dt>Credits bao gồm</dt><dd class="money-number">{{ selectedCustomer.includedCredits ?? '-' }}</dd></div>
            <div><dt>Đã dùng</dt><dd class="money-number">{{ selectedCustomer.usedCredits ?? '-' }}</dd></div>
            <div><dt>Điều chỉnh</dt><dd class="money-number">{{ selectedCustomer.adjustmentCredits ?? 0 }}</dd></div>
            <div><dt>Còn lại</dt><dd class="money-number">{{ selectedCustomer.remainingCredits ?? '-' }}</dd></div>
          </dl>
          <p v-else class="data-unavailable">Không có số liệu credits từ billing summary.</p>
        </section>

        <section class="drawer-section">
          <h3><ListChecks :size="16" aria-hidden="true" /> Dòng thời gian xử lý</h3>
          <ol class="processing-timeline">
            <li v-for="event in paymentTimeline" :key="event.id" :class="{ unavailable: event.unavailable }">
              <span class="timeline-marker" aria-hidden="true"></span>
              <div><strong>{{ event.label }}</strong><p>{{ event.description }}</p><time>{{ event.time ? formatDateTime(event.time) : 'Chưa có thời điểm trong API' }}</time></div>
            </li>
          </ol>
        </section>

        <div v-if="selectedOrder.status === 'Pending'" class="drawer-actions">
          <button type="button" class="approve-button" @click="reviewOrder(selectedOrder, 'approve')"><Check :size="16" aria-hidden="true" /> Duyệt thanh toán</button>
          <button type="button" class="reject-button" @click="reviewOrder(selectedOrder, 'reject')"><XCircle :size="16" aria-hidden="true" /> Từ chối</button>
        </div>
      </div>
    </el-drawer>
  </AdminLayout>
</template>

<script setup>
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowLeft, ArrowUpRight, BadgeCheck, Check, ChevronDown, CircleAlert, ListChecks, Receipt, RefreshCw, Search, SearchX, Sparkles, UserRound, X, XCircle } from 'lucide-vue-next'
import AdminLayout from '@/components/layout/AdminLayout.vue'
import { billingApi, unwrapBillingData } from '@/api/billingApi'
import { useLocale } from '@/composables/useLocale'

const { locale } = useLocale()
const route = useRoute()
const router = useRouter()
const t = (vi, en) => locale.value === 'vi' ? vi : en
const users = ref([]), orders = ref([]), plans = ref([])
const loadingUsers = ref(false), loadingOrders = ref(false), loadingPlans = ref(false)
const usersLoaded = ref(false), lastUpdatedAt = ref(null), ordersError = ref('')
const search = ref(''), orderStatus = ref('Pending'), savingPlan = ref('')
const activeTab = ref(route.path.endsWith('/payments') ? 'payments' : 'subscriptions')
const actionDialog = ref(false), actionKind = ref(''), actionUser = ref(null), actionSubmitting = ref(false)
const actionForm = reactive({ planCode: 'free', autoRenew: false, amount: 0, reason: '' })
const paymentSearch = ref(''), paymentStatusFilter = ref('all'), paymentPlanFilter = ref(''), paymentProviderFilter = ref(''), paymentDateRange = ref([])
const paymentDrawerOpen = ref(false), selectedOrder = ref(null), paymentDetail = ref(null)
const paymentTotal = ref(0)
const paymentPage = ref(1), paymentPageSize = 25
const paymentSummary = ref({ totalCount: 0, revenueVnd: 0, successfulPayments: 0, pendingPayments: 0, needsAttention: 0, failedPayments: 0 })

const isPaymentsPage = computed(() => route.path.endsWith('/payments'))
const paymentLoading = computed(() => loadingOrders.value || (isPaymentsPage.value && loadingUsers.value))
const filteredUsers = computed(() => { const q = search.value.trim().toLowerCase(); return q ? users.value.filter(item => `${item.userName} ${item.email}`.toLowerCase().includes(q)) : users.value })
const pendingOrders = computed(() => orders.value.filter(order => order.status === 'Pending'))
const activePaidCount = computed(() => users.value.filter(user => user.planCode !== 'free' && user.subscriptionStatus === 'Active').length)
const actionTitle = computed(() => ({ change: t('Đổi hoặc kích hoạt gói', 'Change or activate plan'), extend: t('Gia hạn một tháng', 'Extend one month'), adjust: t('Điều chỉnh AI credits', 'Adjust AI credits'), reset: t('Reset usage kỳ hiện tại', 'Reset current-period usage'), cancel: t('Hủy gói trả phí', 'Cancel paid plan') }[actionKind.value] || t('Xác nhận thao tác', 'Confirm action')))

const customerById = computed(() => new Map(users.value.map(user => [String(user.userId), user])))
const customerFor = (order) => customerById.value.get(String(order?.userId))
const orderNeedsAttention = (order) => {
  if (order?.status === 'Expired') return true
  if (order?.status === 'Pending') return Boolean(order.hasFulfillmentMismatch || (order.expiresAt && new Date(order.expiresAt) <= new Date()))
  if (order?.status === 'Failed') return true
  return order?.status === 'Paid' && Boolean(order.hasFulfillmentMismatch)
}
const paymentPlanOptions = computed(() => [...new Map(orders.value.map(order => [order.planCode || order.planName, { value: order.planCode || order.planName, label: order.planName || order.planCode }])).values()])
const paymentProviderOptions = computed(() => [...new Set(orders.value.map(order => order.provider).filter(Boolean))])
const paymentStatusItems = computed(() => [
  { value: 'all', label: 'Tất cả', count: paymentSummary.value.totalCount },
  { value: 'paid', label: 'Đã thanh toán', count: paymentSummary.value.successfulPayments },
  { value: 'pending', label: 'Đang chờ', count: paymentSummary.value.pendingPayments },
  { value: 'attention', label: 'Cần xử lý', count: paymentSummary.value.needsAttention },
  { value: 'failed', label: 'Thất bại / Từ chối', count: paymentSummary.value.failedPayments }
])
const filteredOrders = computed(() => orders.value)
const paymentPages = computed(() => Math.max(1, Math.ceil(paymentTotal.value / paymentPageSize)))
const hasPaymentFilters = computed(() => Boolean(paymentSearch.value || paymentPlanFilter.value || paymentProviderFilter.value || paymentDateRange.value?.length || paymentStatusFilter.value !== 'all'))
const selectedCustomer = computed(() => customerFor(selectedOrder.value))
const paymentTimeline = computed(() => {
  const events = paymentDetail.value?.timeline || []
  if (!events.length) return [{ id: 'observability', label: 'Observability', description: 'Chưa có dữ liệu observability được lưu cho giao dịch này.', time: null, unavailable: true }]
  return events.map((event, index) => ({ id: `${event.type}-${index}`, label: `${event.type} · ${event.status}`, description: event.note || event.reference || 'Sự kiện đã được lưu trong hệ thống.', time: event.occurredAt }))
})

const loadUsers = async () => {
  loadingUsers.value = true
  try { users.value = unwrapBillingData(await billingApi.getUsers()) || []; usersLoaded.value = true }
  catch (e) { usersLoaded.value = false; ElMessage.error(e.response?.data?.message || t('Không tải được dữ liệu billing.', 'Could not load billing users.')) }
  finally { loadingUsers.value = false }
}
const loadOrders = async (status = orderStatus.value) => {
  loadingOrders.value = true
  ordersError.value = ''
  paymentDrawerOpen.value = false
  selectedOrder.value = null
  paymentDetail.value = null
  try {
    if (isPaymentsPage.value) {
      const [start, end] = paymentDateRange.value || []
      const requestedStatus = paymentStatusFilter.value === 'paid' ? 'Paid' : paymentStatusFilter.value === 'pending' ? 'Pending' : paymentStatusFilter.value === 'attention' ? 'Attention' : paymentStatusFilter.value === 'failed' ? 'Failed' : ''
      const result = unwrapBillingData(await billingApi.searchOrders({ search: paymentSearch.value || undefined, status: requestedStatus || undefined, planCode: paymentPlanFilter.value || undefined, provider: paymentProviderFilter.value || undefined, from: start || undefined, to: end ? `${end}T23:59:59.999Z` : undefined, page: paymentPage.value, pageSize: paymentPageSize })) || {}
      orders.value = result.items || []
      paymentTotal.value = result.totalCount || orders.value.length
      paymentSummary.value = result.summary || { totalCount: paymentTotal.value, revenueVnd: 0, successfulPayments: 0, pendingPayments: 0, needsAttention: 0, failedPayments: 0 }
      if (isPaymentsPage.value) lastUpdatedAt.value = new Date()
    } else {
      const result = unwrapBillingData(await billingApi.searchOrders({ status: status || undefined, page: 1, pageSize: 25 })) || {}
      orders.value = result.items || []
      paymentTotal.value = result.totalCount || orders.value.length
      paymentSummary.value = result.summary || { totalCount: paymentTotal.value, revenueVnd: 0, successfulPayments: 0, pendingPayments: 0, needsAttention: 0, failedPayments: 0 }
    }
  }
  catch (e) { ordersError.value = e.response?.data?.message || t('Không tải được đơn thanh toán.', 'Could not load payment orders.'); if (!isPaymentsPage.value) ElMessage.error(ordersError.value) }
  finally { loadingOrders.value = false }
}
const loadPlans = async () => {
  loadingPlans.value = true
  try { plans.value = (unwrapBillingData(await billingApi.getPlans()) || []).map(item => ({ ...item })) }
  catch (e) { ElMessage.error(e.response?.data?.message || t('Không tải được cấu hình gói.', 'Could not load plans.')) }
  finally { loadingPlans.value = false }
}
const loadPayments = () => Promise.all([loadOrders(''), loadUsers()])
const loadAll = () => isPaymentsPage.value ? loadPayments() : Promise.all([loadUsers(), loadOrders(), loadPlans()])
const refreshPayments = () => loadPayments()
const syncRoute = (name) => { if (name === 'payments' && !route.path.endsWith('/payments')) router.push('/admin/billing/payments'); else if (name !== 'payments' && route.path.endsWith('/payments')) router.push('/admin/billing') }
watch(() => route.path, path => { activeTab.value = path.endsWith('/payments') ? 'payments' : activeTab.value === 'payments' ? 'subscriptions' : activeTab.value; if (path.endsWith('/payments')) loadPayments() })
watch([paymentSearch, paymentPlanFilter, paymentProviderFilter, paymentDateRange], () => { if (isPaymentsPage.value) { paymentPage.value = 1; loadOrders('') } })

const resetPaymentFilters = () => { paymentSearch.value = ''; paymentStatusFilter.value = 'all'; paymentPlanFilter.value = ''; paymentProviderFilter.value = ''; paymentDateRange.value = []; paymentPage.value = 1; loadOrders('') }
const setPaymentStatus = (status) => { paymentStatusFilter.value = status; paymentPage.value = 1; loadOrders('') }
const goPaymentPage = (page) => { paymentPage.value = Math.min(Math.max(1, page), paymentPages.value); loadOrders('') }
const openPaymentDetail = async (order) => { selectedOrder.value = order; paymentDetail.value = null; paymentDrawerOpen.value = true; try { paymentDetail.value = unwrapBillingData(await billingApi.getAdminOrderDetails(order.id)) } catch (e) { ElMessage.warning(e.response?.data?.message || 'Chưa tải được timeline persisted.') } }
const openAction = (kind, user) => { actionKind.value = kind; actionUser.value = user; actionForm.planCode = user.planCode || 'free'; actionForm.autoRenew = false; actionForm.amount = 0; actionForm.reason = ''; actionDialog.value = true }
const submitAction = async () => {
  if (!actionUser.value || !actionForm.reason.trim()) return
  actionSubmitting.value = true
  try {
    const id = actionUser.value.userId
    if (actionKind.value === 'change') await billingApi.changePlan(id, { planCode: actionForm.planCode, autoRenew: actionForm.autoRenew, reason: actionForm.reason })
    if (actionKind.value === 'extend') await billingApi.extendSubscription(id, actionForm.reason)
    if (actionKind.value === 'cancel') await billingApi.cancelSubscription(id, actionForm.reason)
    if (actionKind.value === 'adjust') { if (!actionForm.amount) throw new Error(t('Số credits phải khác 0.', 'Credits must be non-zero.')); await billingApi.adjustCredits(id, { amount: actionForm.amount, reason: actionForm.reason }) }
    if (actionKind.value === 'reset') await billingApi.resetUsage(id, actionForm.reason)
    ElMessage.success(t('Thao tác billing đã hoàn tất.', 'Billing action completed.'))
    actionDialog.value = false
    await loadUsers()
  } catch (e) { ElMessage.error(e.response?.data?.message || e.message || t('Không thể hoàn tất thao tác.', 'Could not complete the action.')) }
  finally { actionSubmitting.value = false }
}

const reviewOrder = async (order, action) => {
  try {
    const { value } = await ElMessageBox.prompt(action === 'approve' ? t('Xác nhận đã đối soát đúng giao dịch này.', 'Confirm that this transfer has been reconciled.') : t('Nêu lý do từ chối đơn thanh toán.', 'Enter the rejection reason.'), action === 'approve' ? t('Duyệt thanh toán', 'Approve payment') : t('Từ chối thanh toán', 'Reject payment'), { confirmButtonText: t('Xác nhận', 'Confirm'), cancelButtonText: t('Hủy', 'Cancel'), inputPlaceholder: t('Ghi chú / lý do', 'Note / reason'), inputValidator: value => value?.trim() ? true : t('Vui lòng nhập lý do.', 'A reason is required.') })
    if (action === 'approve') await billingApi.approveOrder(order.id, value); else await billingApi.rejectOrder(order.id, value)
    ElMessage.success(action === 'approve' ? t('Đã kích hoạt gói cho người dùng.', 'The user plan is now active.') : t('Đã từ chối đơn.', 'Order rejected.'))
    paymentDrawerOpen.value = false
    await Promise.all([loadOrders(isPaymentsPage.value ? '' : orderStatus.value), loadUsers()])
  } catch (e) { if (e !== 'cancel' && e !== 'close') ElMessage.error(e.response?.data?.message || t('Không thể xử lý đơn.', 'Could not process the order.')) }
}

const savePlan = async (plan) => {
  try {
    await ElMessageBox.confirm(t(`Lưu giá và AI credits mới cho gói ${plan.name}?`, `Save the new price and AI credits for ${plan.name}?`), t('Xác nhận cấu hình gói', 'Confirm plan configuration'), { confirmButtonText: t('Lưu', 'Save'), cancelButtonText: t('Hủy', 'Cancel') })
    savingPlan.value = plan.code
    await billingApi.updatePlan(plan.code, { monthlyPriceVnd: Number(plan.monthlyPriceVnd || 0), includedAiCredits: Number(plan.includedAiCredits || 0), isPublished: plan.isPublished, isRecommended: plan.isRecommended })
    ElMessage.success(t('Đã lưu cấu hình gói.', 'Plan configuration saved.'))
  } catch (e) { if (e !== 'cancel' && e !== 'close') ElMessage.error(e.response?.data?.message || t('Không thể lưu cấu hình.', 'Could not save configuration.')) }
  finally { savingPlan.value = '' }
}

const initials = (name) => String(name || 'U').split(/\s+/).filter(Boolean).map(part => part[0]).slice(0, 2).join('').toUpperCase()
const shortId = (id) => id ? String(id).slice(0, 8).toUpperCase() : '-'
const formatDate = (value) => value ? new Intl.DateTimeFormat(locale.value === 'vi' ? 'vi-VN' : 'en-US').format(new Date(value)) : '-'
const formatDateTime = (value) => value ? new Intl.DateTimeFormat(locale.value === 'vi' ? 'vi-VN' : 'en-US', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)) : '-'
const formatTime = (value) => value ? new Intl.DateTimeFormat(locale.value === 'vi' ? 'vi-VN' : 'en-US', { timeStyle: 'short' }).format(new Date(value)) : '-'
const money = (value) => `${new Intl.NumberFormat(locale.value === 'vi' ? 'vi-VN' : 'en-US').format(Number(value || 0))} VND`
const statusLabel = (status) => ({ Active: t('Hoạt động', 'Active'), PendingPayment: t('Chờ thanh toán', 'Pending payment'), Cancelled: t('Đã hủy', 'Cancelled'), Expired: t('Hết hạn', 'Expired'), Pending: t('Đang chờ', 'Pending'), Paid: t('Đã thanh toán', 'Paid'), Rejected: t('Đã từ chối', 'Rejected'), Failed: t('Thất bại', 'Failed'), Refunded: t('Đã hoàn tiền', 'Refunded') }[status] || status || '-')
const paymentStatusLabel = (status) => statusLabel(status)
const statusClass = (status) => String(status || 'unknown').toLowerCase()
const providerReference = (order) => order?.providerReference || order?.providerTransactionId || order?.sePayTransactionId || ''
const systemStatusLabel = (order) => { if (order?.status === 'Expired') return 'Đơn đã hết hạn'; if (order?.status === 'Pending') return order?.hasFulfillmentMismatch ? 'Webhook cần kiểm tra' : orderNeedsAttention(order) ? 'Đơn quá hạn' : 'Chờ thanh toán'; if (order?.status === 'Rejected') return 'Đã từ chối'; if (order?.status === 'Failed') return 'Cần kiểm tra'; if (order?.status !== 'Paid') return 'Chưa có dữ liệu'; if (order?.hasFulfillmentMismatch) return 'Fulfillment lỗi'; if (!usersLoaded.value) return 'Đã ghi nhận'; const customer = customerFor(order); if (!customer) return 'Đã ghi nhận'; return customer.subscriptionStatus === 'Active' && customer.planCode === order.planCode ? 'Hoàn tất' : 'Đã ghi nhận' }
const systemClass = (order) => orderNeedsAttention(order) ? 'attention' : order?.status === 'Paid' && customerFor(order) ? 'complete' : 'muted'
onMounted(loadAll)
</script>

<style scoped>
.payment-center { --payment-radius: 10px; color: var(--color-text-primary); min-width: 0; }
.payment-header { display: flex; align-items: flex-end; justify-content: space-between; gap: 24px; margin: 0 auto 20px; max-width: 1600px; }
.section-kicker, .breadcrumb { margin: 0 0 7px; color: var(--color-primary); font-size: 11px; font-weight: 800; letter-spacing: .12em; }
.payment-header h1 { margin: 0; font-size: clamp(25px, 2.1vw, 32px); letter-spacing: -.035em; line-height: 1.1; }
.page-description { margin: 7px 0 0; color: var(--color-text-muted); font-size: 13px; }
.header-actions { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; justify-content: flex-end; }
.last-updated { color: var(--color-text-muted); font-size: 12px; font-variant-numeric: tabular-nums; }
.refresh-button, .reset-button, .text-button, .detail-button, .drawer-close, .drawer-back { border: 0; background: transparent; color: var(--color-text-primary); cursor: pointer; }
.refresh-button, .reset-button { display: inline-flex; align-items: center; justify-content: center; gap: 7px; min-height: 36px; border: 1px solid var(--color-border); border-radius: 8px; padding: 0 13px; background: var(--color-surface); font-weight: 700; }
.refresh-button:hover:not(:disabled), .reset-button:hover:not(:disabled), .detail-button:hover { border-color: var(--color-accent); color: var(--color-accent); }
.refresh-button:disabled, .reset-button:disabled { cursor: not-allowed; opacity: .55; }
.spinning { animation: payment-spin .8s linear infinite; }
@keyframes payment-spin { to { transform: rotate(360deg); } }
.inline-error { display: flex; align-items: center; gap: 9px; max-width: 1600px; margin: 0 auto 14px; padding: 10px 12px; border: 1px solid color-mix(in srgb, var(--color-danger) 35%, var(--color-border)); border-radius: var(--payment-radius); color: var(--color-danger); background: color-mix(in srgb, var(--color-danger) 8%, var(--color-surface)); font-size: 13px; }
.inline-error .text-button { margin-left: auto; color: var(--color-danger); font-weight: 750; text-decoration: underline; text-underline-offset: 3px; }
.kpi-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); max-width: 1600px; margin: 0 auto 18px; border: 1px solid var(--color-border); border-radius: var(--payment-radius); background: var(--color-surface); overflow: hidden; }
.kpi-item { display: grid; gap: 5px; min-width: 0; padding: 14px 16px 13px; border-right: 1px solid var(--color-border); }
.kpi-item:last-child { border-right: 0; }
.kpi-label { color: var(--color-text-muted); font-size: 12px; font-weight: 700; }
.kpi-value { min-width: 0; overflow: hidden; color: var(--color-text-primary); font-size: 21px; line-height: 1.15; text-overflow: ellipsis; white-space: nowrap; }
.kpi-meta { color: var(--color-text-muted); font-size: 11px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.kpi-attention .kpi-value { color: var(--color-warning); }
.money-number, .number-value, .mono-value, code { font-variant-numeric: tabular-nums; }
.status-navigation { display: flex; align-items: center; gap: 2px; max-width: 1600px; margin: 0 auto 12px; overflow-x: auto; border-bottom: 1px solid var(--color-border); }
.status-nav-item { display: inline-flex; align-items: center; gap: 7px; min-height: 38px; flex: 0 0 auto; border: 0; border-bottom: 2px solid transparent; padding: 0 10px; color: var(--color-text-muted); background: transparent; cursor: pointer; font-size: 12px; font-weight: 750; white-space: nowrap; }
.status-nav-item:hover { color: var(--color-text-primary); }
.status-nav-item.active { border-bottom-color: var(--color-accent); color: var(--color-accent); }
.count-badge { min-width: 20px; padding: 2px 5px; border-radius: 5px; color: var(--color-text-muted); background: var(--color-surface-hover); font-size: 11px; font-variant-numeric: tabular-nums; text-align: center; }
.status-nav-item.active .count-badge { color: var(--color-accent); background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface)); }
.payment-surface { max-width: 1600px; margin: 0 auto; border: 1px solid var(--color-border); border-radius: var(--payment-radius); background: var(--color-surface); overflow: hidden; }
.filter-toolbar { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; padding: 10px; border-bottom: 1px solid var(--color-border); background: color-mix(in srgb, var(--color-surface) 92%, var(--color-bg)); }
.search-field { display: flex; align-items: center; gap: 8px; min-width: min(330px, 100%); flex: 1 1 250px; min-height: 36px; border: 1px solid var(--color-border); border-radius: 8px; padding: 0 10px; color: var(--color-text-muted); background: var(--color-input-bg); }
.search-field:focus-within { border-color: var(--color-accent); box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-accent) 15%, transparent); }
.search-field input { min-width: 0; flex: 1; border: 0; outline: 0; color: var(--color-text-primary); background: transparent; font: inherit; font-size: 12px; }
.search-field input::placeholder { color: var(--color-text-muted); }
.clear-search { display: grid; place-items: center; border: 0; padding: 2px; color: var(--color-text-muted); background: transparent; cursor: pointer; }
.filter-select { width: 130px; }
.date-filter { width: 238px; }
.reset-button { min-height: 36px; padding-inline: 11px; color: var(--color-text-muted); font-size: 12px; }
.table-toolbar { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 13px 14px 11px; }
.table-toolbar h2 { margin: 0; font-size: 14px; letter-spacing: -.01em; }
.table-toolbar p { margin: 3px 0 0; color: var(--color-text-muted); font-size: 11px; }
.data-scope { color: var(--color-text-muted); font-size: 11px; }
.payment-table-wrap { width: 100%; overflow-x: auto; }
.pagination-controls { display: flex; align-items: center; justify-content: center; gap: 14px; padding: 16px; border-top: 1px solid var(--color-border); color: var(--color-text-muted); font-size: 12px; }.pagination-controls button { min-height: 34px; border: 1px solid var(--color-border); border-radius: 7px; padding: 0 12px; background: var(--color-surface); color: var(--color-text-primary); cursor: pointer; font-weight: 700; }.pagination-controls button:disabled { cursor: not-allowed; opacity: .45; }
.payment-table { --el-table-bg-color: var(--color-surface); --el-table-tr-bg-color: var(--color-surface); --el-table-header-bg-color: var(--color-table-header); --el-table-border-color: var(--color-border); --el-table-text-color: var(--color-text-primary); --el-table-header-text-color: var(--color-text-muted); min-width: 1120px; }
:deep(.payment-table .el-table__header-wrapper th) { height: 34px; padding: 0 10px; color: var(--color-text-muted); font-size: 10px; font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }
:deep(.payment-table .el-table__body-wrapper td) { height: 58px; padding: 7px 10px; }
:deep(.payment-table .el-table__row) { cursor: pointer; }
:deep(.payment-table .el-table__row:hover > td) { background: var(--color-table-row-hover) !important; }
.customer-cell, .transaction-cell, .time-cell { display: flex; align-items: center; min-width: 0; gap: 9px; }
.customer-copy, .transaction-cell, .time-cell { display: grid; gap: 2px; min-width: 0; }
.customer-copy strong, .transaction-cell code { overflow: hidden; color: var(--color-text-primary); font-size: 12px; text-overflow: ellipsis; white-space: nowrap; }
.customer-copy small, .transaction-cell small, .time-cell small { overflow: hidden; color: var(--color-text-muted); font-size: 11px; text-overflow: ellipsis; white-space: nowrap; }
.avatar { display: grid; flex: 0 0 auto; place-items: center; width: 29px; height: 29px; border: 1px solid color-mix(in srgb, var(--color-accent) 18%, var(--color-border)); border-radius: 8px; color: var(--color-accent); background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)); font-size: 10px; font-weight: 800; }
.plan-label { color: var(--color-text-primary); font-size: 12px; font-weight: 700; }
.amount-cell { display: block; color: var(--color-text-primary); font-size: 12px; white-space: nowrap; }
.status-badge, .system-state { display: inline-flex; width: fit-content; align-items: center; border-radius: 5px; padding: 4px 7px; font-size: 11px; font-weight: 750; white-space: nowrap; }
.status-badge.paid, .system-state.complete { color: #157347; background: color-mix(in srgb, #22a06b 14%, var(--color-surface)); }
.status-badge.pending, .system-state.attention { color: #8a5b00; background: color-mix(in srgb, #d99b16 16%, var(--color-surface)); }
.status-badge.rejected, .status-badge.failed { color: #a33a36; background: color-mix(in srgb, #d9534f 13%, var(--color-surface)); }
.status-badge.refunded { color: #6d4ca2; background: color-mix(in srgb, #8f6bc4 13%, var(--color-surface)); }
.system-state.muted { color: var(--color-text-muted); background: var(--color-surface-hover); }
.detail-button { display: inline-grid; place-items: center; width: 30px; height: 30px; border: 1px solid transparent; border-radius: 7px; color: var(--color-text-muted); }
.detail-button:focus-visible, .refresh-button:focus-visible, .reset-button:focus-visible, .status-nav-item:focus-visible, .text-button:focus-visible, .drawer-back:focus-visible, .drawer-close:focus-visible, .drawer-actions button:focus-visible { outline: 3px solid color-mix(in srgb, var(--color-accent) 36%, transparent); outline-offset: 2px; }
.empty-state { display: grid; justify-items: center; gap: 7px; min-height: 230px; padding: 48px 18px; text-align: center; }
.empty-state-icon { display: grid; place-items: center; width: 42px; height: 42px; border-radius: 10px; color: var(--color-accent); background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)); }
.empty-state h3 { margin: 2px 0 0; font-size: 14px; }
.empty-state p { margin: 0; max-width: 380px; color: var(--color-text-muted); font-size: 12px; }
.text-button { color: var(--color-accent); font-size: 12px; font-weight: 750; text-decoration: underline; text-underline-offset: 3px; }
.table-skeleton { padding: 0 14px 14px; }
.skeleton-row { display: grid; grid-template-columns: 1.45fr 1.2fr .8fr .9fr .8fr .35fr; align-items: center; gap: 14px; height: 58px; border-bottom: 1px solid var(--color-border); }
.skeleton-block { display: block; height: 12px; border-radius: 4px; background: color-mix(in srgb, var(--color-text-muted) 14%, var(--color-surface)); }
.skeleton-customer { height: 22px; }.skeleton-short { width: 62%; }.skeleton-amount { width: 75%; margin-left: auto; }.skeleton-status { width: 72px; }.skeleton-action { width: 26px; margin-left: auto; }
.drawer-content { min-height: 100%; padding: 20px 22px 28px; color: var(--color-text-primary); }
.drawer-header { display: flex; justify-content: space-between; gap: 16px; padding-bottom: 16px; border-bottom: 1px solid var(--color-border); }
.drawer-back { display: inline-flex; align-items: center; gap: 5px; margin-bottom: 10px; padding: 0; color: var(--color-text-muted); font-size: 12px; font-weight: 700; }
.drawer-back:hover, .drawer-close:hover { color: var(--color-accent); }
.drawer-header h2 { margin: 0; font-size: 20px; letter-spacing: -.025em; }
.drawer-header p { margin: 5px 0 0; color: var(--color-text-muted); font-size: 11px; font-family: ui-monospace, SFMono-Regular, Consolas, monospace; }
.drawer-close { display: grid; place-items: center; align-self: flex-start; width: 32px; height: 32px; border-radius: 7px; }
.drawer-status-line { display: flex; gap: 8px; align-items: center; padding: 14px 0 2px; }
.drawer-section { padding: 17px 0; border-bottom: 1px solid var(--color-border); }
.drawer-section h3 { display: flex; align-items: center; gap: 7px; margin: 0 0 12px; color: var(--color-text-primary); font-size: 12px; font-weight: 800; letter-spacing: .01em; }
.drawer-section h3 svg { color: var(--color-accent); }
.detail-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 11px 14px; margin: 0; }
.detail-grid > div { min-width: 0; }
.detail-grid dt { margin-bottom: 3px; color: var(--color-text-muted); font-size: 10px; }
.detail-grid dd { margin: 0; overflow-wrap: anywhere; color: var(--color-text-primary); font-size: 12px; font-weight: 700; }
.mono-value { font-family: ui-monospace, SFMono-Regular, Consolas, monospace; font-size: 11px !important; }
.data-unavailable { margin: 0; color: var(--color-text-muted); font-size: 12px; }
.processing-timeline { display: grid; gap: 0; margin: 0; padding: 0; list-style: none; }
.processing-timeline li { position: relative; display: grid; grid-template-columns: 16px 1fr; gap: 9px; min-height: 54px; }
.processing-timeline li:not(:last-child)::after { position: absolute; top: 14px; bottom: 0; left: 6px; width: 1px; background: var(--color-border); content: ''; }
.timeline-marker { z-index: 1; width: 13px; height: 13px; margin-top: 2px; border: 3px solid var(--color-surface); border-radius: 50%; background: var(--color-accent); box-shadow: 0 0 0 1px var(--color-accent); }
.processing-timeline li.unavailable .timeline-marker { background: var(--color-text-muted); box-shadow: 0 0 0 1px var(--color-text-muted); opacity: .55; }
.processing-timeline strong { display: block; color: var(--color-text-primary); font-size: 12px; }
.processing-timeline p { margin: 2px 0 2px; color: var(--color-text-muted); font-size: 11px; line-height: 1.45; }
.processing-timeline time { color: var(--color-text-muted); font-size: 10px; font-variant-numeric: tabular-nums; }
.drawer-actions { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; padding-top: 18px; }
.drawer-actions button, .payment-actions button { display: inline-flex; align-items: center; justify-content: center; gap: 6px; min-height: 34px; border-radius: 7px; padding: 0 10px; cursor: pointer; font-size: 12px; font-weight: 750; }
.approve-button, .save-button, .confirm-button { border: 1px solid var(--color-primary); background: var(--color-primary); color: #fff; }
.reject-button { border: 1px solid var(--color-danger); color: var(--color-danger); background: transparent; }
.billing-admin { color: var(--color-text-primary); }.page-header { display: flex; align-items: flex-end; justify-content: space-between; gap: 24px; margin-bottom: 26px; }.page-header h1 { margin: 0 0 8px; font-size: 30px; letter-spacing: -.035em; }.page-header span, .surface-toolbar p { color: var(--color-text-muted); }.row-action, .save-button, .cancel-button, .confirm-button { display: inline-flex; align-items: center; justify-content: center; gap: 7px; min-height: 36px; border-radius: 8px; padding: 0 13px; font-weight: 700; cursor: pointer; }.row-action, .cancel-button { border: 1px solid var(--color-border); background: var(--color-surface); color: var(--color-text-primary); }.summary-strip { display: grid; grid-template-columns: repeat(3, 1fr); border: 1px solid var(--color-border); border-radius: 12px; background: var(--color-surface); margin-bottom: 24px; }.summary-strip > div { display: grid; gap: 6px; padding: 18px 22px; border-right: 1px solid var(--color-border); }.summary-strip > div:last-child { border-right: 0; }.summary-strip span { color: var(--color-text-muted); font-size: 13px; }.summary-strip strong { font-size: 24px; }.admin-surface { border: 1px solid var(--color-border); border-radius: 12px; background: var(--color-surface); overflow: hidden; min-height: 260px; }.surface-toolbar { min-height: 70px; padding: 16px 20px; display: flex; align-items: center; justify-content: space-between; gap: 20px; border-bottom: 1px solid var(--color-border); }.surface-toolbar h2 { margin: 0; font-size: 17px; }.surface-toolbar p { margin: 5px 0 0; font-size: 13px; }.user-cell { display: flex; align-items: center; gap: 10px; }.user-cell > div, .period-cell, .credit-cell { display: grid; gap: 3px; }.user-cell small, .period-cell small, .credit-cell small { color: var(--color-text-muted); }.plan-badge, .status-text { display: inline-flex; width: fit-content; border-radius: 6px; padding: 4px 8px; font-size: 12px; font-weight: 750; }.plan-badge { color: var(--color-primary); background: color-mix(in srgb, var(--color-primary) 10%, var(--color-surface)); }.status-text { color: #40604f; background: #e6f5ec; }.status-text.pendingpayment, .status-text.pending { color: #7b5700; background: #fff3ce; }.status-text.cancelled, .status-text.expired, .status-text.rejected { color: #9b2c2c; background: #fde9e7; }.credit-cell > div { display: flex; align-items: baseline; gap: 5px; }.credit-cell strong { font-size: 18px; }.credit-cell span { color: var(--color-text-muted); font-size: 12px; }.payment-actions { display: flex; justify-content: flex-end; gap: 8px; }.transfer-code { color: var(--color-primary); font-weight: 800; }.plan-name { display: grid; gap: 3px; }.plan-name code { color: var(--color-text-muted); }.dialog-user { display: grid; gap: 3px; margin-top: 0; font-weight: 750; }.dialog-user span { color: var(--color-text-muted); font-size: 13px; font-weight: 500; }.cancel-button { margin-right: 8px; }
:deep(.billing-tabs .el-tabs__header) { margin-bottom: 18px; }:deep(.billing-tabs .el-tabs__item) { font-weight: 700; }:deep(.billing-table) { --el-table-bg-color: var(--color-surface); --el-table-tr-bg-color: var(--color-surface); --el-table-header-bg-color: color-mix(in srgb, var(--color-bg) 72%, var(--color-surface)); --el-table-border-color: var(--color-border); --el-table-text-color: var(--color-text-primary); --el-table-header-text-color: var(--color-text-muted); }
@media (max-width: 1100px) { .kpi-grid { grid-template-columns: repeat(2, 1fr); }.kpi-item:nth-child(2) { border-right: 0; }.kpi-item:nth-child(-n+2) { border-bottom: 1px solid var(--color-border); } }
@media (max-width: 900px) { .summary-strip { grid-template-columns: 1fr; }.summary-strip > div { border-right: 0; border-bottom: 1px solid var(--color-border); }.summary-strip > div:last-child { border-bottom: 0; }.page-header, .payment-header, .surface-toolbar { align-items: flex-start; flex-direction: column; }.payment-header { margin-bottom: 16px; }.header-actions { width: 100%; justify-content: flex-start; }.filter-select, .date-filter { flex: 1 1 130px; width: auto; }.data-scope { display: none; } }
@media (max-width: 600px) { .kpi-grid { grid-template-columns: 1fr 1fr; }.kpi-item { padding: 12px 11px; }.kpi-value { font-size: 18px; }.kpi-meta { font-size: 10px; }.filter-toolbar { align-items: stretch; }.search-field { flex-basis: 100%; }.filter-select, .date-filter, .reset-button { width: 100%; flex-basis: 100%; }.table-toolbar { align-items: flex-start; }.drawer-content { padding: 16px 16px 24px; }.detail-grid { grid-template-columns: 1fr 1fr; }.drawer-actions { position: sticky; bottom: -1px; margin-inline: -16px; padding: 12px 16px; border-top: 1px solid var(--color-border); background: var(--color-surface); } }
@media (prefers-reduced-motion: reduce) { .spinning { animation: none; } }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
</style>
