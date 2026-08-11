<template>
  <AdminLayout>
    <div class="billing-admin">
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
  </AdminLayout>
</template>

<script setup>
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ChevronDown, RefreshCw } from 'lucide-vue-next'
import AdminLayout from '@/components/layout/AdminLayout.vue'
import { billingApi, unwrapBillingData } from '@/api/billingApi'
import { useLocale } from '@/composables/useLocale'

const { locale } = useLocale()
const route = useRoute()
const router = useRouter()
const t = (vi, en) => locale.value === 'vi' ? vi : en
const users = ref([]), orders = ref([]), plans = ref([])
const loadingUsers = ref(false), loadingOrders = ref(false), loadingPlans = ref(false)
const search = ref(''), orderStatus = ref('Pending'), savingPlan = ref('')
const activeTab = ref(route.path.endsWith('/payments') ? 'payments' : 'subscriptions')
const actionDialog = ref(false), actionKind = ref(''), actionUser = ref(null), actionSubmitting = ref(false)
const actionForm = reactive({ planCode: 'free', autoRenew: false, amount: 0, reason: '' })
const filteredUsers = computed(() => { const q = search.value.trim().toLowerCase(); return q ? users.value.filter(item => `${item.userName} ${item.email}`.toLowerCase().includes(q)) : users.value })
const pendingOrders = computed(() => orders.value.filter(order => order.status === 'Pending'))
const activePaidCount = computed(() => users.value.filter(user => user.planCode !== 'free' && user.subscriptionStatus === 'Active').length)
const actionTitle = computed(() => ({ change: t('Đổi hoặc kích hoạt gói', 'Change or activate plan'), extend: t('Gia hạn một tháng', 'Extend one month'), adjust: t('Điều chỉnh AI credits', 'Adjust AI credits'), reset: t('Reset usage kỳ hiện tại', 'Reset current-period usage'), cancel: t('Hủy gói trả phí', 'Cancel paid plan') }[actionKind.value] || t('Xác nhận thao tác', 'Confirm action')))

const loadUsers = async () => { loadingUsers.value = true; try { users.value = unwrapBillingData(await billingApi.getUsers()) || [] } catch (e) { ElMessage.error(e.response?.data?.message || t('Không tải được dữ liệu billing.', 'Could not load billing users.')) } finally { loadingUsers.value = false } }
const loadOrders = async () => { loadingOrders.value = true; try { orders.value = unwrapBillingData(await billingApi.getOrders(orderStatus.value)) || [] } catch (e) { ElMessage.error(e.response?.data?.message || t('Không tải được đơn thanh toán.', 'Could not load payment orders.')) } finally { loadingOrders.value = false } }
const loadPlans = async () => { loadingPlans.value = true; try { plans.value = (unwrapBillingData(await billingApi.getPlans()) || []).map(item => ({ ...item })) } catch (e) { ElMessage.error(e.response?.data?.message || t('Không tải được cấu hình gói.', 'Could not load plans.')) } finally { loadingPlans.value = false } }
const loadAll = () => Promise.all([loadUsers(), loadOrders(), loadPlans()])
const syncRoute = (name) => { if (name === 'payments' && !route.path.endsWith('/payments')) router.push('/admin/billing/payments'); else if (name !== 'payments' && route.path.endsWith('/payments')) router.push('/admin/billing') }
watch(() => route.path, path => { activeTab.value = path.endsWith('/payments') ? 'payments' : activeTab.value === 'payments' ? 'subscriptions' : activeTab.value })

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
  } catch (e) { ElMessage.error(e.response?.data?.message || e.message || t('Không thể hoàn tất thao tác.', 'Could not complete the action.')) } finally { actionSubmitting.value = false }
}

const reviewOrder = async (order, action) => {
  try {
    const { value } = await ElMessageBox.prompt(action === 'approve' ? t('Xác nhận đã đối soát đúng giao dịch này.', 'Confirm that this transfer has been reconciled.') : t('Nêu lý do từ chối đơn thanh toán.', 'Enter the rejection reason.'), action === 'approve' ? t('Duyệt thanh toán', 'Approve payment') : t('Từ chối thanh toán', 'Reject payment'), { confirmButtonText: t('Xác nhận', 'Confirm'), cancelButtonText: t('Hủy', 'Cancel'), inputPlaceholder: t('Ghi chú / lý do', 'Note / reason'), inputValidator: value => value?.trim() ? true : t('Vui lòng nhập lý do.', 'A reason is required.') })
    if (action === 'approve') await billingApi.approveOrder(order.id, value); else await billingApi.rejectOrder(order.id, value)
    ElMessage.success(action === 'approve' ? t('Đã kích hoạt gói cho người dùng.', 'The user plan is now active.') : t('Đã từ chối đơn.', 'Order rejected.'))
    await Promise.all([loadOrders(), loadUsers()])
  } catch (e) { if (e !== 'cancel' && e !== 'close') ElMessage.error(e.response?.data?.message || t('Không thể xử lý đơn.', 'Could not process the order.')) }
}

const savePlan = async (plan) => {
  try {
    await ElMessageBox.confirm(t(`Lưu giá và AI credits mới cho gói ${plan.name}?`, `Save the new price and AI credits for ${plan.name}?`), t('Xác nhận cấu hình gói', 'Confirm plan configuration'), { confirmButtonText: t('Lưu', 'Save'), cancelButtonText: t('Hủy', 'Cancel') })
    savingPlan.value = plan.code
    await billingApi.updatePlan(plan.code, { monthlyPriceVnd: Number(plan.monthlyPriceVnd || 0), includedAiCredits: Number(plan.includedAiCredits || 0), isPublished: plan.isPublished, isRecommended: plan.isRecommended })
    ElMessage.success(t('Đã lưu cấu hình gói.', 'Plan configuration saved.'))
  } catch (e) { if (e !== 'cancel' && e !== 'close') ElMessage.error(e.response?.data?.message || t('Không thể lưu cấu hình.', 'Could not save configuration.')) } finally { savingPlan.value = '' }
}

const initials = (name) => String(name || 'U').split(/\s+/).filter(Boolean).map(part => part[0]).slice(0, 2).join('').toUpperCase()
const formatDate = (value) => value ? new Intl.DateTimeFormat(locale.value === 'vi' ? 'vi-VN' : 'en-US').format(new Date(value)) : '-'
const formatDateTime = (value) => value ? new Intl.DateTimeFormat(locale.value === 'vi' ? 'vi-VN' : 'en-US', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)) : '-'
const money = (value) => `${new Intl.NumberFormat(locale.value === 'vi' ? 'vi-VN' : 'en-US').format(value || 0)} VND`
const statusLabel = (status) => ({ Active: t('Hoạt động', 'Active'), PendingPayment: t('Chờ thanh toán', 'Pending payment'), Cancelled: t('Đã hủy', 'Cancelled'), Expired: t('Hết hạn', 'Expired'), Pending: t('Đang chờ', 'Pending'), Paid: t('Đã thanh toán', 'Paid'), Rejected: t('Từ chối', 'Rejected') }[status] || status)
onMounted(loadAll)
</script>

<style scoped>
.billing-admin { color: var(--color-text-primary); }.page-header { display: flex; align-items: flex-end; justify-content: space-between; gap: 24px; margin-bottom: 26px; }.breadcrumb { margin: 0 0 8px; color: var(--color-primary); font-size: 11px; font-weight: 800; letter-spacing: .12em; }.page-header h1 { margin: 0 0 8px; font-size: 30px; letter-spacing: -.035em; }.page-header span, .surface-toolbar p { color: var(--color-text-muted); }.refresh-button, .row-action, .save-button, .approve-button, .reject-button, .cancel-button, .confirm-button { display: inline-flex; align-items: center; justify-content: center; gap: 7px; min-height: 36px; border-radius: 8px; padding: 0 13px; font-weight: 700; cursor: pointer; }.refresh-button, .row-action, .cancel-button { border: 1px solid var(--color-border); background: var(--color-surface); color: var(--color-text-primary); }.summary-strip { display: grid; grid-template-columns: repeat(3, 1fr); border: 1px solid var(--color-border); border-radius: 12px; background: var(--color-surface); margin-bottom: 24px; }.summary-strip > div { display: grid; gap: 6px; padding: 18px 22px; border-right: 1px solid var(--color-border); }.summary-strip > div:last-child { border-right: 0; }.summary-strip span { color: var(--color-text-muted); font-size: 13px; }.summary-strip strong { font-size: 24px; }.admin-surface { border: 1px solid var(--color-border); border-radius: 12px; background: var(--color-surface); overflow: hidden; min-height: 260px; }.surface-toolbar { min-height: 70px; padding: 16px 20px; display: flex; align-items: center; justify-content: space-between; gap: 20px; border-bottom: 1px solid var(--color-border); }.surface-toolbar h2 { margin: 0; font-size: 17px; }.surface-toolbar p { margin: 5px 0 0; font-size: 13px; }.user-cell { display: flex; align-items: center; gap: 10px; }.user-cell > div, .period-cell, .credit-cell { display: grid; gap: 3px; }.user-cell small, .period-cell small, .credit-cell small { color: var(--color-text-muted); }.avatar { width: 32px; height: 32px; border-radius: 9px; display: grid; place-items: center; background: color-mix(in srgb, var(--color-primary) 12%, var(--color-surface)); color: var(--color-primary); font-size: 11px; font-weight: 800; }.plan-badge, .status-text { display: inline-flex; width: fit-content; border-radius: 6px; padding: 4px 8px; font-size: 12px; font-weight: 750; }.plan-badge { color: var(--color-primary); background: color-mix(in srgb, var(--color-primary) 10%, var(--color-surface)); }.status-text { color: #40604f; background: #e6f5ec; }.status-text.pendingpayment, .status-text.pending { color: #7b5700; background: #fff3ce; }.status-text.cancelled, .status-text.expired, .status-text.rejected { color: #9b2c2c; background: #fde9e7; }.credit-cell > div { display: flex; align-items: baseline; gap: 5px; }.credit-cell strong { font-size: 18px; }.credit-cell span { color: var(--color-text-muted); font-size: 12px; }.payment-actions { display: flex; justify-content: flex-end; gap: 8px; }.approve-button, .save-button, .confirm-button { border: 1px solid var(--color-primary); background: var(--color-primary); color: #fff; }.reject-button { border: 1px solid #d92d20; color: #b42318; background: transparent; }.save-button:disabled { opacity: .45; cursor: not-allowed; }.transfer-code { color: var(--color-primary); font-weight: 800; }.plan-name { display: grid; gap: 3px; }.plan-name code { color: var(--color-text-muted); }.empty-state { padding: 48px 20px; text-align: center; color: var(--color-text-muted); }.dialog-user { display: grid; gap: 3px; margin-top: 0; font-weight: 750; }.dialog-user span { color: var(--color-text-muted); font-size: 13px; font-weight: 500; }.cancel-button { margin-right: 8px; }
:deep(.billing-tabs .el-tabs__header) { margin-bottom: 18px; }:deep(.billing-tabs .el-tabs__item) { font-weight: 700; }:deep(.billing-table) { --el-table-bg-color: var(--color-surface); --el-table-tr-bg-color: var(--color-surface); --el-table-header-bg-color: color-mix(in srgb, var(--color-bg) 72%, var(--color-surface)); --el-table-border-color: var(--color-border); --el-table-text-color: var(--color-text-primary); --el-table-header-text-color: var(--color-text-muted); }
@media (max-width: 900px) { .summary-strip { grid-template-columns: 1fr; }.summary-strip > div { border-right: 0; border-bottom: 1px solid var(--color-border); }.summary-strip > div:last-child { border-bottom: 0; }.page-header, .surface-toolbar { align-items: flex-start; flex-direction: column; } }
</style>
