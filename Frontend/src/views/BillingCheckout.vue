<template>
  <main class="checkout-page">
    <header class="checkout-nav">
      <button type="button" class="back-button" @click="router.push('/#pricing')">
        <ArrowLeft :size="17" /> {{ t('Quay lại bảng giá', 'Back to pricing') }}
      </button>
      <div class="brand">SprintA <span>Billing</span></div>
    </header>

    <section class="checkout-shell" v-loading="loading">
      <div v-if="error" class="state-panel error-state">
        <CircleAlert :size="24" />
        <div><strong>{{ t('Không thể tải thông tin thanh toán', 'Could not load billing details') }}</strong><p>{{ error }}</p></div>
      </div>

      <template v-else-if="plan">
        <div class="checkout-heading">
          <span class="plan-mark"><Coins :size="20" /></span>
          <div>
            <p>{{ t('Gói đã chọn', 'Selected plan') }}</p>
            <h1>{{ plan.name }}</h1>
          </div>
          <div class="price-block">
            <strong>{{ priceLabel(plan.monthlyPriceVnd) }}</strong>
            <span v-if="plan.monthlyPriceVnd != null">{{ t('/ tháng', '/ month') }}</span>
          </div>
        </div>

        <div v-if="isFree" class="free-panel">
          <ShieldCheck :size="30" />
          <h2>{{ t('Bắt đầu với gói Free', 'Start with Free') }}</h2>
          <p>{{ t(`Bạn nhận ${plan.includedAiCredits} AI credits mỗi tháng và không cần thanh toán.`, `You receive ${plan.includedAiCredits} AI credits each month with no payment required.`) }}</p>
          <button type="button" class="primary-button" :disabled="submitting" @click="activateFree">
            {{ submitting ? t('Đang kích hoạt...', 'Activating...') : t('Kích hoạt gói Free', 'Activate Free') }}
          </button>
        </div>

        <div v-else-if="isEnterprise" class="free-panel">
          <ShieldCheck :size="30" />
          <h2>{{ t('Gói Enterprise cần tư vấn', 'Enterprise requires consultation') }}</h2>
          <p>{{ t('Vui lòng liên hệ quản trị viên SprintA để nhận cấu hình và quyền lợi phù hợp.', 'Please contact a SprintA administrator for a tailored configuration.') }}</p>
        </div>

        <div v-else class="payment-grid">
          <section class="payment-card">
            <div class="section-title">
              <div><p>{{ t('Thanh toán chuyển khoản', 'Bank transfer payment') }}</p><h2>{{ t('Quét QR để thanh toán', 'Scan QR to pay') }}</h2></div>
              <QrCode :size="24" />
            </div>
            <div class="qr-frame">
              <img v-if="qrImagePath" :src="qrImagePath" :alt="t('QR thanh toán SprintA', 'SprintA payment QR')" @error="qrFailed = true" />
              <div v-else class="qr-placeholder"><QrCode :size="38" /><span>{{ t('Chưa cấu hình QR thanh toán.', 'Payment QR is not configured.') }}</span></div>
            </div>
            <p class="payment-note">{{ t('Chuyển đúng số tiền và ghi đúng mã thanh toán. Gói sẽ được kích hoạt sau khi quản trị viên xác nhận.', 'Transfer the exact amount with the correct payment code. Your plan is activated after administrator approval.') }}</p>
          </section>

          <section class="order-card">
            <template v-if="activeOrder">
              <span class="status-badge" :class="activeOrder.status.toLowerCase()"><Clock3 :size="14" /> {{ statusLabel(activeOrder.status) }}</span>
              <h2>{{ t('Thông tin chuyển khoản', 'Transfer details') }}</h2>
              <dl>
                <div><dt>{{ t('Gói', 'Plan') }}</dt><dd>{{ activeOrder.planName || plan.name }}</dd></div>
                <div><dt>{{ t('Số tiền', 'Amount') }}</dt><dd>{{ priceLabel(activeOrder.amountVnd) }}</dd></div>
                <div class="transfer-row"><dt>{{ t('Mã thanh toán', 'Payment code') }}</dt><dd><code>{{ activeOrder.transferCode }}</code><button type="button" @click="copyCode"><Copy :size="15" /></button></dd></div>
                <div><dt>{{ t('Tạo lúc', 'Created') }}</dt><dd>{{ formatDate(activeOrder.createdAt) }}</dd></div>
              </dl>
              <p class="pending-copy">{{ t('Đơn đang chờ quản trị viên đối soát. Không cần tạo thêm đơn cho cùng gói.', 'The order is waiting for administrator review. You do not need another order for this plan.') }}</p>
              <button type="button" class="secondary-button" @click="loadData">{{ t('Kiểm tra trạng thái', 'Refresh status') }}</button>
            </template>
            <template v-else>
              <h2>{{ t('Xác nhận tạo đơn', 'Confirm your order') }}</h2>
              <p>{{ t('SprintA sẽ tạo mã chuyển khoản duy nhất cho đơn này. Việc tạo đơn không tự động kích hoạt gói.', 'SprintA creates a unique transfer code. Creating an order does not activate the plan automatically.') }}</p>
              <div class="summary-line"><span>{{ plan.name }}</span><strong>{{ priceLabel(plan.monthlyPriceVnd) }}</strong></div>
              <button type="button" class="primary-button" :disabled="submitting" @click="createOrder">
                {{ submitting ? t('Đang tạo đơn...', 'Creating order...') : t('Tạo đơn thanh toán', 'Create payment order') }}
              </button>
            </template>
          </section>
        </div>

        <section v-if="billing" class="current-entitlement">
          <div><span>{{ t('Gói hiện tại', 'Current plan') }}</span><strong>{{ billing.planName }}</strong></div>
          <div><span>{{ t('AI credits còn lại', 'AI credits remaining') }}</span><strong>{{ billing.remainingCredits }} / {{ billing.includedCredits + (billing.adjustmentCredits || 0) }}</strong></div>
          <div><span>{{ t('Kết thúc kỳ', 'Period ends') }}</span><strong>{{ formatDate(billing.currentPeriodEnd) }}</strong></div>
        </section>

        <section class="billing-history" v-if="history.length">
          <div class="history-heading"><div><span>{{ t('Lịch sử thanh toán', 'Payment history') }}</span><h2>{{ t('Tất cả đơn của bạn', 'All your orders') }}</h2></div><small>{{ historyTotal }} {{ t('giao dịch', 'transactions') }}</small></div>
          <div class="history-list">
            <article v-for="order in history" :key="order.id" class="history-row">
              <div><strong>{{ order.planName || order.planCode }}</strong><small>{{ formatDate(order.createdAt) }} · {{ order.transferCode }}</small></div>
              <div class="history-meta"><strong>{{ priceLabel(order.amountVnd) }}</strong><span class="history-status" :class="String(order.status).toLowerCase()">{{ statusLabel(order.status) }}</span></div>
              <div class="history-actions"><button type="button" @click="openDetails(order)">{{ t('Chi tiết', 'Details') }}</button><button v-if="order.status === 'Paid'" type="button" @click="openReceipt(order)">{{ t('Receipt', 'Receipt') }}</button></div>
            </article>
          </div>
          <nav v-if="historyTotalPages > 1" class="pagination-controls" :aria-label="t('Phân trang lịch sử thanh toán', 'Payment history pagination')">
            <button type="button" :disabled="historyLoading || historyPage === 1" @click="loadHistory(historyPage - 1)">{{ t('Trước', 'Previous') }}</button>
            <span aria-live="polite">{{ t(`Trang ${historyPage} / ${historyTotalPages}`, `Page ${historyPage} / ${historyTotalPages}`) }}</span>
            <button type="button" :disabled="historyLoading || historyPage >= historyTotalPages" @click="loadHistory(historyPage + 1)">{{ t('Sau', 'Next') }}</button>
          </nav>
        </section>
        <section v-if="selectedDetails" class="billing-detail">
          <div class="history-heading"><div><span>{{ t('Chi tiết thanh toán', 'Payment detail') }}</span><h2>{{ selectedDetails.order.planName }}</h2></div><button type="button" class="close-detail" @click="selectedDetails = null">×</button></div>
          <dl><div><dt>{{ t('Mã đơn', 'Order') }}</dt><dd>{{ selectedDetails.order.id }}</dd></div><div><dt>{{ t('Nhà cung cấp', 'Provider') }}</dt><dd>{{ selectedDetails.order.provider || '-' }}</dd></div><div><dt>{{ t('Mã giao dịch', 'Provider ref') }}</dt><dd>{{ selectedDetails.order.providerReference || selectedDetails.order.providerTransactionId || '-' }}</dd></div></dl>
          <p v-if="!selectedDetails.observabilityAvailable" class="muted-copy">{{ t('Chưa có dữ liệu observability chi tiết cho giao dịch này.', 'Detailed observability is not available for this transaction.') }}</p>
          <div v-else class="timeline-list"><div v-for="event in selectedDetails.timeline" :key="`${event.type}-${event.occurredAt}-${event.reference}`"><strong>{{ event.type }} · {{ event.status }}</strong><small>{{ formatDate(event.occurredAt) }}<span v-if="event.note"> · {{ event.note }}</span></small></div></div>
        </section>
        <section v-if="receipt" class="billing-detail receipt-card">
          <div class="history-heading"><div><span>{{ t('Payment receipt', 'Payment receipt') }}</span><h2>{{ receipt.receiptNumber }}</h2></div><button type="button" class="close-detail" @click="receipt = null">×</button></div>
          <p>{{ receipt.customerName }} · {{ receipt.customerEmail }}</p><p>{{ receipt.order.planName }} · <strong>{{ priceLabel(receipt.order.amountVnd) }}</strong></p>
          <small>{{ t('Đây là receipt thanh toán, không phải hóa đơn VAT/e-invoice.', 'This is a payment receipt, not a VAT/e-invoice.') }}</small>
          <button type="button" class="secondary-button" @click="resendReceipt">{{ t('Gửi lại receipt', 'Resend receipt') }}</button>
        </section>
      </template>
    </section>
  </main>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft, CircleAlert, Clock3, Coins, Copy, QrCode, ShieldCheck } from 'lucide-vue-next'
import axiosClient from '@/api/axiosClient'
import { billingApi, unwrapBillingData } from '@/api/billingApi'
import { language } from '@/i18n'

const route = useRoute()
const router = useRouter()
const loading = ref(true)
const submitting = ref(false)
const error = ref('')
const plan = ref(null)
const billing = ref(null)
const orders = ref([])
const history = ref([])
const historyTotal = ref(0)
const historyPage = ref(1)
const historyPageSize = 10
const historyLoading = ref(false)
const selectedDetails = ref(null)
const receipt = ref(null)
const qrFailed = ref(false)
const isVi = computed(() => language.value === 'vi')
const t = (vi, en) => isVi.value ? vi : en
const planCode = computed(() => String(route.params.planCode || '').toLowerCase())
const isFree = computed(() => planCode.value === 'free' || Number(plan.value?.monthlyPriceVnd) === 0)
const isEnterprise = computed(() => planCode.value === 'enterprise' || plan.value?.monthlyPriceVnd == null)
const activeOrder = computed(() => orders.value.find(order => order.planCode === planCode.value && order.status === 'Pending'))
const qrImagePath = computed(() => qrFailed.value ? '' : (activeOrder.value?.paymentInstructions?.qrUrl || ''))
const historyTotalPages = computed(() => Math.max(1, Math.ceil(historyTotal.value / historyPageSize)))

const applyHistoryResponse = (response) => {
  const historyData = unwrapBillingData(response) || {}
  history.value = historyData.items || []
  historyTotal.value = historyData.totalCount || history.value.length
  orders.value = history.value
}

const loadHistory = async (page = 1) => {
  const nextPage = Math.min(Math.max(1, page), historyTotalPages.value)
  historyLoading.value = true
  selectedDetails.value = null
  receipt.value = null
  try {
    historyPage.value = nextPage
    applyHistoryResponse(await billingApi.getMyHistory({ page: nextPage, pageSize: historyPageSize }))
  } catch (requestError) {
    ElMessage.error(requestError.response?.data?.message || t('Không thể tải lịch sử thanh toán.', 'Could not load payment history.'))
  } finally {
    historyLoading.value = false
  }
}

const loadData = async () => {
  loading.value = true
  error.value = ''
  try {
    const [pricingResponse, billingResponse, historyResponse] = await Promise.all([
      axiosClient.get('/public/pricing'), billingApi.getMe(), billingApi.getMyHistory({ page: 1, pageSize: historyPageSize })
    ])
    const plans = pricingResponse.data?.data?.plans || []
    plan.value = plans.find(item => String(item.id || item.code).toLowerCase() === planCode.value) || null
    billing.value = unwrapBillingData(billingResponse)
    historyPage.value = 1
    applyHistoryResponse(historyResponse)
    if (!plan.value) error.value = t('Gói dịch vụ không tồn tại hoặc chưa được công khai.', 'This plan does not exist or is not published.')
  } catch (requestError) {
    error.value = requestError.response?.data?.message || t('Vui lòng thử lại sau.', 'Please try again later.')
  } finally {
    loading.value = false
  }
}

const createOrder = async () => {
  submitting.value = true
  try {
    const response = await billingApi.createOrder(planCode.value)
    const order = unwrapBillingData(response)
    await loadHistory(1)
    if (!orders.value.some(item => item.id === order?.id)) orders.value = [order, ...orders.value]
    ElMessage.success(response.data?.message || t('Đã tạo đơn thanh toán.', 'Payment order created.'))
  } catch (requestError) {
    ElMessage.error(requestError.response?.data?.message || t('Không thể tạo đơn.', 'Could not create order.'))
  } finally {
    submitting.value = false
  }
}

const activateFree = async () => {
  submitting.value = true
  try {
    const response = await billingApi.activateFree()
    billing.value = unwrapBillingData(response)
    ElMessage.success(response.data?.message || t('Đã kích hoạt gói Free.', 'Free plan activated.'))
  } catch (requestError) {
    ElMessage.error(requestError.response?.data?.message || t('Không thể kích hoạt gói Free.', 'Could not activate Free.'))
  } finally {
    submitting.value = false
  }
}

const copyCode = async () => {
  await navigator.clipboard.writeText(activeOrder.value.transferCode)
  ElMessage.success(t('Đã sao chép mã thanh toán.', 'Payment code copied.'))
}
const openDetails = async (order) => {
  try { selectedDetails.value = unwrapBillingData(await billingApi.getOrderDetails(order.id)); receipt.value = null } catch (requestError) { ElMessage.error(requestError.response?.data?.message || t('Không thể tải chi tiết đơn.', 'Could not load order details.')) }
}
const openReceipt = async (order) => {
  try { receipt.value = unwrapBillingData(await billingApi.getReceipt(order.id)); selectedDetails.value = null } catch (requestError) { ElMessage.error(requestError.response?.data?.message || t('Không thể tải receipt.', 'Could not load receipt.')) }
}
const resendReceipt = async () => {
  if (!receipt.value) return
  try { const response = await billingApi.resendReceipt(receipt.value.order.id); ElMessage.success(response.data?.message || t('Đã yêu cầu gửi lại receipt.', 'Receipt resend requested.')) } catch (requestError) { ElMessage.error(requestError.response?.data?.message || t('Không thể gửi lại receipt.', 'Could not resend receipt.')) }
}
const priceLabel = (amount) => amount == null ? t('Liên hệ', 'Contact') : `${new Intl.NumberFormat(isVi.value ? 'vi-VN' : 'en-US').format(amount)} VND`
const formatDate = (value) => value ? new Intl.DateTimeFormat(isVi.value ? 'vi-VN' : 'en-US', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '-'
const statusLabel = (status) => ({ Pending: t('Chờ xác nhận', 'Pending'), Paid: t('Đã thanh toán', 'Paid'), Rejected: t('Đã từ chối', 'Rejected'), Cancelled: t('Đã hủy', 'Cancelled') }[status] || status)

onMounted(loadData)
</script>

<style scoped>
.checkout-page { min-height: 100dvh; background: var(--color-bg, #f6f8fb); color: var(--color-text-primary, #172033); }
.checkout-nav { height: 68px; display: flex; align-items: center; justify-content: space-between; padding: 0 clamp(20px, 5vw, 72px); border-bottom: 1px solid var(--color-border, #dfe4ec); background: var(--color-surface, #fff); }
.back-button { display: inline-flex; align-items: center; gap: 8px; border: 0; background: transparent; color: var(--color-text-muted, #667085); cursor: pointer; font-weight: 650; }
.brand { font-size: 19px; font-weight: 800; letter-spacing: -.02em; }.brand span { color: var(--color-primary, #3563e9); font-weight: 650; }
.checkout-shell { width: min(1080px, calc(100% - 32px)); margin: 0 auto; padding: 52px 0 72px; min-height: 520px; }
.checkout-heading { display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: 16px; padding-bottom: 28px; border-bottom: 1px solid var(--color-border, #dfe4ec); }
.plan-mark { width: 46px; height: 46px; border-radius: 12px; display: grid; place-items: center; color: #fff; background: var(--color-primary, #3563e9); }
.checkout-heading p, .section-title p { margin: 0 0 4px; color: var(--color-text-muted, #667085); font-size: 13px; }.checkout-heading h1, .section-title h2 { margin: 0; }.checkout-heading h1 { font-size: 30px; }
.price-block { text-align: right; display: grid; gap: 3px; }.price-block strong { font-size: 23px; }.price-block span { color: var(--color-text-muted, #667085); font-size: 13px; }
.payment-grid { display: grid; grid-template-columns: minmax(0, 1.05fr) minmax(340px, .95fr); gap: 24px; margin-top: 28px; }
.payment-card, .order-card, .free-panel, .state-panel { border: 1px solid var(--color-border, #dfe4ec); border-radius: 14px; background: var(--color-surface, #fff); padding: 26px; }
.section-title { display: flex; align-items: center; justify-content: space-between; color: var(--color-primary, #3563e9); }.section-title h2 { color: var(--color-text-primary, #172033); font-size: 20px; }
.qr-frame { width: min(310px, 100%); aspect-ratio: 1; margin: 24px auto; border: 1px solid var(--color-border, #dfe4ec); border-radius: 12px; padding: 12px; display: grid; place-items: center; background: #fff; }.qr-frame img { width: 100%; height: 100%; object-fit: contain; }.qr-placeholder { color: #667085; display: grid; place-items: center; gap: 12px; text-align: center; }
.payment-note, .order-card > p, .pending-copy { color: var(--color-text-muted, #667085); line-height: 1.65; }.payment-note { margin: 0; font-size: 14px; }
.order-card h2 { margin: 18px 0 12px; font-size: 22px; }.status-badge { display: inline-flex; align-items: center; gap: 7px; padding: 6px 10px; border-radius: 999px; color: #875a00; background: #fff5d6; font-size: 12px; font-weight: 750; }.status-badge.paid { color: #12613d; background: #dcf8e9; }
dl { margin: 22px 0; }dl > div { display: flex; justify-content: space-between; gap: 20px; padding: 12px 0; border-bottom: 1px solid var(--color-border, #dfe4ec); }dt { color: var(--color-text-muted, #667085); }dd { margin: 0; font-weight: 700; text-align: right; }.transfer-row dd { display: flex; align-items: center; gap: 8px; }.transfer-row code { color: var(--color-primary, #3563e9); font-size: 15px; }.transfer-row button { border: 0; background: transparent; color: var(--color-primary, #3563e9); cursor: pointer; }
.summary-line { display: flex; justify-content: space-between; gap: 20px; margin: 28px 0 18px; padding: 16px 0; border-top: 1px solid var(--color-border, #dfe4ec); border-bottom: 1px solid var(--color-border, #dfe4ec); }
.primary-button, .secondary-button { min-height: 42px; border-radius: 9px; padding: 0 17px; font-weight: 750; cursor: pointer; transition: transform .15s ease, opacity .15s ease; }.primary-button { width: 100%; border: 1px solid var(--color-primary, #3563e9); background: var(--color-primary, #3563e9); color: #fff; }.secondary-button { border: 1px solid var(--color-border, #dfe4ec); background: transparent; color: var(--color-text-primary, #172033); }.primary-button:active, .secondary-button:active { transform: translateY(1px); }.primary-button:disabled { opacity: .58; cursor: wait; }
.current-entitlement { display: grid; grid-template-columns: repeat(3, 1fr); margin-top: 24px; border-top: 1px solid var(--color-border, #dfe4ec); border-bottom: 1px solid var(--color-border, #dfe4ec); }.current-entitlement > div { display: grid; gap: 7px; padding: 18px 20px; border-right: 1px solid var(--color-border, #dfe4ec); }.current-entitlement > div:last-child { border-right: 0; }.current-entitlement span { color: var(--color-text-muted, #667085); font-size: 13px; }
.billing-history, .billing-detail { margin-top: 28px; border: 1px solid var(--color-border, #dfe4ec); border-radius: 14px; background: var(--color-surface, #fff); padding: 24px; }.history-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 18px; }.history-heading span { color: var(--color-text-muted, #667085); font-size: 13px; }.history-heading h2 { margin: 4px 0 0; font-size: 20px; }.history-heading small { color: var(--color-text-muted, #667085); }.history-list { margin-top: 18px; }.history-row { display: grid; grid-template-columns: minmax(0, 1fr) auto auto; align-items: center; gap: 18px; padding: 15px 0; border-top: 1px solid var(--color-border, #dfe4ec); }.history-row small { display: block; margin-top: 5px; color: var(--color-text-muted, #667085); }.history-meta { display: grid; justify-items: end; gap: 5px; }.history-status { padding: 4px 8px; border-radius: 99px; font-size: 11px; font-weight: 750; }.history-status.paid { color: #12613d; background: #dcf8e9; }.history-status.pending { color: #875a00; background: #fff5d6; }.history-status.rejected { color: #b42318; background: #fee4e2; }.history-actions { display: flex; gap: 8px; }.history-actions button, .close-detail { border: 0; background: transparent; color: var(--color-primary, #3563e9); cursor: pointer; font-weight: 700; }.close-detail { font-size: 24px; line-height: 1; }.billing-detail dl { margin-bottom: 4px; }.muted-copy, .receipt-card p, .receipt-card small { color: var(--color-text-muted, #667085); line-height: 1.6; }.timeline-list { display: grid; gap: 10px; margin-top: 14px; }.timeline-list > div { display: grid; gap: 4px; padding-left: 14px; border-left: 2px solid var(--color-primary, #3563e9); }.timeline-list small { color: var(--color-text-muted, #667085); }.receipt-card { display: grid; gap: 8px; }.receipt-card .secondary-button { justify-self: start; margin-top: 8px; }
.pagination-controls { display: flex; align-items: center; justify-content: center; gap: 14px; margin-top: 18px; padding-top: 16px; border-top: 1px solid var(--color-border, #dfe4ec); color: var(--color-text-muted, #667085); font-size: 13px; }.pagination-controls button { min-height: 34px; border: 1px solid var(--color-border, #dfe4ec); border-radius: 8px; padding: 0 12px; background: var(--color-surface, #fff); color: var(--color-text-primary, #172033); cursor: pointer; font-weight: 700; }.pagination-controls button:disabled { cursor: not-allowed; opacity: .45; }
.free-panel { max-width: 560px; margin: 34px auto 0; text-align: center; }.free-panel svg { color: var(--color-primary, #3563e9); }.free-panel h2 { margin: 14px 0 8px; }.free-panel p { color: var(--color-text-muted, #667085); line-height: 1.6; }.free-panel .primary-button { margin-top: 12px; max-width: 320px; }
.state-panel { display: flex; align-items: flex-start; gap: 14px; }.state-panel p { margin: 5px 0 0; color: var(--color-text-muted, #667085); }.error-state { color: #b42318; }
@media (max-width: 760px) { .checkout-shell { padding-top: 28px; }.checkout-heading { grid-template-columns: auto 1fr; }.price-block { grid-column: 2; text-align: left; }.payment-grid { grid-template-columns: 1fr; }.current-entitlement { grid-template-columns: 1fr; }.current-entitlement > div { border-right: 0; border-bottom: 1px solid var(--color-border, #dfe4ec); }.current-entitlement > div:last-child { border-bottom: 0; }.history-row { grid-template-columns: 1fr auto; }.history-actions { grid-column: 1 / -1; }.history-meta { justify-items: end; } }
</style>
