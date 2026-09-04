<template>
  <main class="checkout-page">
    <header class="checkout-nav">
      <button type="button" class="back-button" @click="goBackToOrigin">
        <ArrowLeft :size="17" /> {{ t('Quay lại ứng dụng', 'Back to app') }}
      </button>
      <div class="brand"><SprintaBrand size="compact" /><span>Billing</span></div>
    </header>

    <section class="checkout-shell" v-loading="loading">
      <div v-if="error" class="state-panel error-state" role="alert">
        <CircleAlert :size="24" />
        <div><strong>{{ t('Không thể tải thông tin thanh toán', 'Could not load billing details') }}</strong><p>{{ error }}</p></div>
      </div>

      <template v-else-if="plan">
        <header class="checkout-intro">
          <div class="checkout-intro-content">
            <p class="eyebrow">{{ t('Thanh toán gói', 'Plan payment') }}</p>
            <h1>{{ plan.name }}</h1>
            <p class="intro-copy">{{ introCopy }}</p>
          </div>
          <div class="price-block">
            <span>{{ t('Giá theo tháng', 'Monthly price') }}</span>
            <strong>{{ priceLabel(plan.monthlyPriceVnd) }}</strong>
            <small v-if="plan.monthlyPriceVnd != null">{{ t('Không tự động gia hạn', 'No automatic renewal') }}</small>
          </div>
        </header>

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

        <div v-else class="checkout-state-flow" :class="`state-${paymentState.toLowerCase()}`" aria-live="polite">
          <section v-if="activeOrder" class="payment-stage pending-stage">
            <section class="payment-visual">
              <div class="section-topline">
                <span class="section-kicker"><QrCode :size="16" /> {{ t('Thanh toán chuyển khoản', 'Bank transfer') }}</span>
                <span class="state-chip pending" aria-live="polite">{{ statusLabel(paymentState) }}</span>
              </div>

              <div class="visual-heading">
                <div>
                  <h2>{{ t('Quét QR để thanh toán', 'Scan to pay') }}</h2>
                  <p>{{ t('Mở ứng dụng ngân hàng, quét mã và kiểm tra lại số tiền trước khi gửi.', 'Open your banking app, scan the code, and check the amount before sending.') }}</p>
                </div>
                <span class="visual-meta">VietQR</span>
              </div>
              <div class="qr-frame">
                <img v-if="qrImagePath" :src="qrImagePath" :alt="t('Mã QR thanh toán SprintA', 'SprintA payment QR')" @error="qrFailed = true" />
                <div v-else class="qr-placeholder" role="status">
                  <CircleAlert :size="34" />
                  <strong>{{ qrFailed ? t('Không thể tải QR lúc này', 'The QR could not be loaded') : t('QR chưa khả dụng', 'QR is not available') }}</strong>
                  <span>{{ qrFailed ? t('Bạn vẫn có thể dùng thông tin chuyển khoản bên cạnh.', 'You can still use the transfer details beside this panel.') : t('Cấu hình QR thanh toán chưa hoàn tất.', 'Payment QR configuration is not complete.') }}</span>
                </div>
              </div>
              <p class="payment-warning"><ShieldCheck :size="17" /> {{ t('Vui lòng giữ nguyên nội dung chuyển khoản để hệ thống tự động xác nhận.', 'Keep the transfer content unchanged so the system can confirm your payment automatically.') }}</p>
              <p class="secondary-note">{{ t('Bạn có thể chuyển khoản ngay. Gói chỉ được kích hoạt sau khi hệ thống ghi nhận thanh toán.', 'You can transfer now. The plan activates only after the system records the payment.') }}</p>
              <button type="button" class="support-action" @click="copySupportContext">
                <CircleHelp :size="16" /> {{ t('Đã chuyển khoản nhưng chưa được cập nhật?', 'Transferred but not updated?') }}
              </button>
            </section>

            <aside class="order-card">
              <div class="order-card-heading">
                <div><span class="eyebrow">{{ t('Đơn đang hoạt động', 'Active order') }}</span><h2>{{ t('Thông tin chuyển khoản', 'Transfer details') }}</h2></div>
                <Clock3 :size="20" />
              </div>

              <div class="order-summary">
                <span>{{ t('Gói đã chọn', 'Selected plan') }}</span>
                <strong>{{ activeOrder.planName || plan.name }}</strong>
              </div>

              <dl class="detail-list">
                <div><dt>{{ t('Ngân hàng', 'Bank') }}</dt><dd>{{ activeInstructions.bankName || activeInstructions.bankCode || t('Chưa cấu hình', 'Not configured') }}</dd></div>
                <div><dt>{{ t('Chủ tài khoản', 'Account holder') }}</dt><dd>{{ activeInstructions?.accountName || t('Chưa cấu hình', 'Not configured') }}</dd></div>
                <div class="copy-detail"><dt>{{ t('Số tài khoản', 'Account number') }}</dt><dd><strong>{{ activeInstructions?.accountNumber || '-' }}</strong><button type="button" :disabled="!activeInstructions?.accountNumber" :aria-label="t('Sao chép số tài khoản', 'Copy account number')" @click="copyText(activeInstructions?.accountNumber, t('Đã sao chép số tài khoản.', 'Account number copied.'))"><Copy :size="15" /></button></dd></div>
                <div class="copy-detail"><dt>{{ t('Số tiền', 'Amount') }}</dt><dd><strong>{{ priceLabel(activePaymentValues.amount) }}</strong><button type="button" :aria-label="t('Sao chép số tiền', 'Copy amount')" @click="copyText(activePaymentValues.amount, t('Đã sao chép số tiền.', 'Amount copied.'))"><Copy :size="15" /></button></dd></div>
              </dl>

              <div class="transfer-content">
                <div><span>{{ t('Nội dung chuyển khoản', 'Transfer content') }}</span><button type="button" :aria-label="t('Sao chép nội dung chuyển khoản', 'Copy transfer content')" @click="copyText(activePaymentValues.transferContent, t('Đã sao chép nội dung chuyển khoản.', 'Transfer content copied.'))"><Copy :size="15" /></button></div>
                <strong>{{ activePaymentValues.transferContent }}</strong>
                <small>{{ t('Giữ nguyên từng ký tự trong nội dung này.', 'Keep every character in this content unchanged.') }}</small>
              </div>

              <div class="countdown-block" aria-live="polite">
                <div><span>{{ t('Thời hạn đơn', 'Order expires') }}</span><strong>{{ formatDate(activeOrder.expiresAt) }}</strong></div>
                <div><span>{{ t('Còn lại', 'Time left') }}</span><strong>{{ remainingTime }}</strong></div>
              </div>
              <button type="button" class="secondary-button" :disabled="refreshing" @click="refreshPaymentState">
                {{ refreshing ? t('Đang kiểm tra...', 'Checking...') : t('Kiểm tra trạng thái', 'Check payment status') }}
              </button>
            </aside>
          </section>

          <section v-else-if="handoffLoading" class="state-composition preparing-state">
            <div class="state-icon preparing"><Clock3 :size="25" /></div>
            <span class="state-eyebrow">{{ t('Đang xác thực', 'Verifying order') }}</span>
            <h2>{{ t('Đang chuẩn bị thông tin thanh toán', 'Preparing payment details') }}</h2>
            <p>{{ t('Đơn vừa tạo đang được xác thực với tài khoản của bạn. Không cần tạo thêm đơn.', 'The order you just created is being verified for your account. There is no need to create another order.') }}</p>
          </section>

          <section v-else-if="paymentState === 'Paid'" class="state-composition success-composition">
            <div class="success-hero">
              <div class="state-icon success"><ShieldCheck :size="26" /></div>
              <div><span class="state-eyebrow">{{ t('Thanh toán đã hoàn tất', 'Payment confirmed') }}</span><h2>{{ t('Thanh toán thành công', 'Payment successful') }}</h2><p>{{ t('Gói của bạn đã được cập nhật. Các AI credits mới đã sẵn sàng để sử dụng.', 'Your plan is updated. Your new AI credits are ready to use.') }}</p></div>
            </div>
            <div class="paid-proof">
              <div class="proof-amount"><span>{{ t('Đã thanh toán', 'Amount paid') }}</span><strong>{{ priceLabel(paidOrder?.amountVnd) }}</strong><small>{{ formatDate(paidOrder?.paidAt) }}</small></div>
              <div class="proof-plan"><span>{{ t('Gói đã kích hoạt', 'Plan activated') }}</span><strong>{{ paidOrder?.planName || billing?.planName || plan.name }}</strong><small>+{{ formatCreditCount(paidOrder?.includedAiCredits ?? plan.includedAiCredits) }} {{ t('AI credits', 'AI credits') }}</small></div>
            </div>
            <dl class="success-facts">
              <div><dt>{{ t('Ví AI hiện có', 'AI wallet') }}</dt><dd>{{ formatCreditCount(billing?.totalRemainingCredits ?? billing?.remainingCredits ?? 0) }}</dd></div>
              <div><dt>{{ t('Kết thúc kỳ', 'Period ends') }}</dt><dd>{{ formatDate(billing?.currentPeriodEnd) }}</dd></div>
              <div><dt>{{ t('Trạng thái', 'Status') }}</dt><dd>{{ statusLabel('Paid') }}</dd></div>
              <div><dt>{{ t('Biên nhận', 'Receipt') }}</dt><dd>{{ receipt?.receiptNumber || t('Có thể xem trong lịch sử', 'Available in history') }}</dd></div>
            </dl>
            <div class="success-actions">
              <button type="button" class="primary-button" @click="goBackToOrigin">{{ t('Tiếp tục sử dụng SprintA', 'Continue using SprintA') }}</button>
              <button v-if="paidOrder && canShowPaymentReceipt(paidOrder)" type="button" class="secondary-button" @click="openReceipt(paidOrder)">{{ t('Xem biên nhận', 'View receipt') }}</button>
            </div>
          </section>

          <section v-else-if="paymentState === 'Expired' || paymentState === 'Failed' || paymentState === 'Rejected'" class="state-composition terminal-state">
            <div class="state-icon danger"><Clock3 v-if="paymentState === 'Expired'" :size="25" /><CircleAlert v-else :size="25" /></div>
            <span class="state-eyebrow">{{ statusLabel(paymentState) }}</span>
            <h2>{{ paymentState === 'Expired' ? t('Tạo đơn mới để tiếp tục', 'Create a new order to continue') : t('Tạo đơn mới để thử lại', 'Create a new order to try again') }}</h2>
            <p>{{ paymentState === 'Expired' ? t('Mã chuyển khoản cũ không còn hiệu lực. Đơn cũ vẫn được giữ trong lịch sử để đối soát.', 'The previous transfer code is no longer active. The old order stays in history for reconciliation.') : t('Đơn này không thể tiếp tục. Đơn cũ vẫn được giữ trong lịch sử để đối soát.', 'This order cannot continue. The old order stays in history for reconciliation.') }}</p>
            <div class="state-actions">
              <button type="button" class="primary-button" :disabled="submitting" @click="createOrder">{{ submitting ? t('Đang tạo đơn...', 'Creating order...') : t('Tạo đơn mới', 'Create new order') }}</button>
              <button type="button" class="support-action" @click="copySupportContext"><CircleHelp :size="16" /> {{ t('Cần hỗ trợ đối soát?', 'Need reconciliation support?') }}</button>
            </div>
          </section>

          <section v-else class="state-composition idle-state">
            <div class="state-icon neutral"><QrCode :size="25" /></div>
            <span class="state-eyebrow">{{ t('Bắt đầu thanh toán', 'Start payment') }}</span>
            <h2>{{ t('Tạo đơn khi bạn đã sẵn sàng', 'Create an order when ready') }}</h2>
            <p>{{ t('SprintA sẽ tạo mã chuyển khoản duy nhất. Chỉ tạo đơn không kích hoạt gói.', 'SprintA creates a unique transfer code. Creating an order does not activate the plan.') }}</p>
            <div class="idle-summary"><span>{{ plan.name }}</span><strong>{{ priceLabel(plan.monthlyPriceVnd) }}</strong></div>
            <button type="button" class="primary-button" :disabled="submitting" @click="createOrder">
              {{ submitting ? t('Đang tạo đơn...', 'Creating order...') : t(`Tạo đơn thanh toán · ${priceLabel(plan.monthlyPriceVnd).replace(' VND', 'đ')}`, `Create payment order · ${priceLabel(plan.monthlyPriceVnd)}`) }}
            </button>
          </section>
        </div>

        <section v-if="billing" class="account-overview">
          <div class="section-heading"><div><span class="eyebrow">{{ t('Tài khoản', 'Account') }}</span><h2>{{ t('Tổng quan quyền lợi', 'Benefits overview') }}</h2></div><span class="section-note">{{ t('Thông tin cập nhật theo trạng thái tài khoản', 'Updated with your account status') }}</span></div>
          <div class="account-overview-surface">
            <div class="account-plan"><span>{{ t('Gói hiện tại', 'Current plan') }}</span><strong>{{ billing.planName }}</strong><small class="account-status"><span aria-hidden="true"></span>{{ t('Quyền lợi đang hoạt động', 'Active entitlement') }}</small></div>
            <div class="account-wallet"><span>{{ t('Ví AI còn lại', 'AI credit wallet') }}</span><strong>{{ formatCreditCount(billing.totalRemainingCredits ?? billing.remainingCredits ?? 0) }}</strong><small>{{ t('credits có thể sử dụng', 'credits available') }}</small></div>
            <dl class="account-period"><div><dt>{{ t('Kết thúc kỳ', 'Period ends') }}</dt><dd>{{ formatDate(billing.currentPeriodEnd) }}</dd></div><div><dt>{{ t('Phân bổ', 'Allocation') }}</dt><dd>{{ formatCreditCount(billing.creditBuckets?.length || 0) }} {{ t('bucket credit', 'credit buckets') }}</dd></div></dl>
          </div>
        </section>
        <section v-if="billing?.creditBuckets?.length" class="wallet-surface">
          <div class="section-heading"><div><span class="eyebrow">{{ t('Phân bổ credit', 'Credit allocation') }}</span><h2>{{ t('Chi tiết ví AI', 'AI wallet details') }}</h2></div><span class="section-note">{{ t('Bucket sắp hết hạn được dùng trước', 'Soonest-expiring bucket is used first') }}</span></div>
          <div v-for="bucket in billing.creditBuckets" :key="bucket.id" class="bucket-row">
            <div class="bucket-main"><div><strong>{{ String(bucket.sourcePlan || '').toUpperCase() }}</strong><span class="bucket-status" :class="String(bucket.status || '').toLowerCase()">{{ bucketStatusLabel(bucket) }}</span></div><strong class="bucket-remaining">{{ formatCreditCount(bucket.remaining) }} <small>/ {{ formatCreditCount(bucket.granted) }}</small></strong></div>
            <div class="bucket-progress" role="progressbar" :aria-valuenow="bucketProgress(bucket)" aria-valuemin="0" aria-valuemax="100" :aria-label="t('Tỷ lệ credit còn lại', 'Remaining credit ratio')"><span :style="{ width: `${bucketProgress(bucket)}%` }"></span></div>
            <div class="bucket-meta"><span>{{ t('Đã dùng', 'Used') }} {{ formatCreditCount(Math.max(0, Number(bucket.granted || 0) - Number(bucket.remaining || 0))) }}</span><span>{{ bucketDateLabel(bucket) }} {{ formatDate(bucketDateValue(bucket)) }}</span></div>
          </div>
        </section>

        <section class="billing-history" v-if="history.length">
          <div class="history-heading"><div><span>{{ t('Lịch sử thanh toán', 'Payment history') }}</span><h2>{{ t('Đơn thanh toán', 'Payment orders') }}</h2></div><small>{{ historyTotal }} {{ t('giao dịch', 'transactions') }}</small></div>
          <div class="history-table" role="table" :aria-label="t('Lịch sử thanh toán', 'Payment history')">
            <div class="history-table-header" role="row"><span role="columnheader">{{ t('Gói', 'Plan') }}</span><span role="columnheader">{{ t('Ngày', 'Date') }}</span><span role="columnheader">{{ t('Số tiền', 'Amount') }}</span><span role="columnheader">{{ t('Trạng thái', 'Status') }}</span><span role="columnheader">{{ t('Thao tác', 'Actions') }}</span></div>
            <article v-for="order in history" :key="order.id" class="history-row" role="row">
              <div class="history-plan" role="cell"><strong>{{ order.planName || order.planCode }}</strong><small>{{ order.transferCode || t('Không có mã chuyển khoản', 'No transfer code') }}</small></div>
              <div class="history-date" role="cell"><span>{{ formatDate(order.createdAt) }}</span></div>
              <div class="history-amount" role="cell"><strong>{{ priceLabel(order.amountVnd) }}</strong></div>
              <div class="history-status-cell" role="cell"><span class="status-dot" :class="displayStatus(order).toLowerCase()"></span>{{ statusLabel(displayStatus(order)) }}</div>
              <div class="history-actions" role="cell"><button type="button" @click="openDetails(order)">{{ t('Chi tiết', 'Details') }}</button><button v-if="canShowPaymentReceipt(order)" type="button" @click="openReceipt(order)">{{ t('Biên nhận', 'Receipt') }}</button></div>
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
          <div class="history-heading"><div><span>{{ t('Biên nhận thanh toán', 'Payment receipt') }}</span><h2>{{ receipt.receiptNumber }}</h2></div><button type="button" class="close-detail" @click="receipt = null">×</button></div>
          <p>{{ receipt.customerName }} · {{ receipt.customerEmail }}</p><p>{{ receipt.order.planName }} · <strong>{{ priceLabel(receipt.order.amountVnd) }}</strong></p>
          <small>{{ t('Đây là receipt thanh toán, không phải hóa đơn VAT/e-invoice.', 'This is a payment receipt, not a VAT/e-invoice.') }}</small>
          <button type="button" class="secondary-button" @click="resendReceipt">{{ t('Gửi lại biên nhận', 'Resend receipt') }}</button>
        </section>
      </template>
    </section>
  </main>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft, CircleAlert, CircleHelp, Clock3, Copy, QrCode, ShieldCheck } from 'lucide-vue-next'
import axiosClient from '@/api/axiosClient'
import { billingApi, unwrapBillingData } from '@/api/billingApi'
import { language } from '@/i18n'
import SprintaBrand from '@/components/branding/SprintaBrand.vue'
import {
  canShowPaymentReceipt,
  createCheckoutOrderGate,
  formatRemainingTime,
  getCheckoutState,
  getOrderDisplayStatus,
  getOrderRemainingSeconds,
  getPaymentCopyValues,
  isPaymentInstructionsAvailable,
  isActivePendingOrder,
  isKnownPaymentOrderStatus,
  isOrderForPlan,
  mergePaymentOrder,
  normalizePaymentInstructions,
  selectActivePendingOrder,
  shouldFetchPaymentOrderDetails,
  shouldPollPaymentOrder
} from '@/utils/billingCheckoutState'
import { resolveBillingReturnTo } from '@/utils/billingPlanFlow'

const route = useRoute()
const router = useRouter()
const loading = ref(true)
const refreshing = ref(false)
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
const handoffLoading = ref(false)
const clock = ref(Date.now())
let clockTimer = null
let pollTimer = null
let loadGeneration = 0
const checkoutOrderGate = createCheckoutOrderGate(async (code) => unwrapBillingData(await billingApi.createOrder(code)))
const isVi = computed(() => language.value === 'vi')
const t = (vi, en) => isVi.value ? vi : en
const planCode = computed(() => String(route.params.planCode || '').toLowerCase())
const returnTo = computed(() => resolveBillingReturnTo(route.query.returnTo))
const routeOrderId = computed(() => {
  const value = String(route.query.orderId || '').trim()
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value) ? value : ''
})
const checkoutRouteKey = computed(() => `${planCode.value}:${routeOrderId.value}`)
const isFree = computed(() => planCode.value === 'free' || Number(plan.value?.monthlyPriceVnd) === 0)
const isEnterprise = computed(() => planCode.value === 'enterprise' || plan.value?.monthlyPriceVnd == null)
const goBackToOrigin = () => router.replace(returnTo.value)

const applyHistoryResponse = (response) => {
  const historyData = unwrapBillingData(response) || {}
  const items = historyData.items || []
  const knownOrders = new Map([
    ...orders.value,
    ...(billing.value?.pendingOrder ? [billing.value.pendingOrder] : [])
  ].map(order => [order.id, order]))
  history.value = items
  historyTotal.value = historyData.totalCount || history.value.length
  orders.value = items.map(order => mergePaymentOrder(knownOrders.get(order.id), order))
}

const planOrders = computed(() => {
  const unique = new Map()
  for (const order of [billing.value?.pendingOrder, ...orders.value]) {
    if (!isOrderForPlan(order, planCode.value)) continue
    unique.set(order.id, order)
  }
  return [...unique.values()].sort((left, right) => {
    const createdDifference = new Date(right.createdAt || 0).getTime() - new Date(left.createdAt || 0).getTime()
    return createdDifference || String(right.id).localeCompare(String(left.id))
  })
})
const activeOrder = computed(() => selectActivePendingOrder(planOrders.value, planCode.value, clock.value))
const latestOrder = computed(() => planOrders.value[0] || null)
const paymentState = computed(() => {
  if (handoffLoading.value) return 'Preparing'
  return activeOrder.value ? 'Pending' : getCheckoutState(latestOrder.value, clock.value)
})
const paidOrder = computed(() => paymentState.value === 'Paid' ? latestOrder.value : null)
const activeInstructions = computed(() => normalizePaymentInstructions(activeOrder.value))
const paymentInstructionsAvailable = computed(() => isPaymentInstructionsAvailable(activeOrder.value))
const activePaymentValues = computed(() => getPaymentCopyValues(activeOrder.value))
const qrImagePath = computed(() => qrFailed.value || !paymentInstructionsAvailable.value ? '' : activeInstructions.value.qrUrl)
const remainingSeconds = computed(() => getOrderRemainingSeconds(activeOrder.value, clock.value))
const remainingTime = computed(() => formatRemainingTime(remainingSeconds.value))
const historyTotalPages = computed(() => Math.max(1, Math.ceil(historyTotal.value / historyPageSize)))
const introCopy = computed(() => {
  if (handoffLoading.value) return t('Đang xác thực đơn thanh toán vừa tạo.', 'Preparing the payment order you just created.')
  if (paymentState.value === 'Paid') return t('Thanh toán đã hoàn tất và gói của bạn đã được cập nhật.', 'Payment is complete and your plan is updated.')
  if (paymentState.value === 'Expired') return t('Đơn trước đã hết hạn. Bạn có thể tạo một đơn mới cho gói này.', 'Your previous order expired. Create a new order for this plan.')
  if (activeOrder.value) return t('Chuyển khoản theo đúng thông tin dưới đây để hệ thống tự động đối soát.', 'Transfer using the details below so the system can reconcile your payment automatically.')
  return t('Xem lại gói, sau đó tạo đơn khi bạn sẵn sàng thanh toán.', 'Review the plan, then create an order when you are ready to pay.')
})

const loadHistory = async (page = 1) => {
  const generation = loadGeneration
  const nextPage = Math.min(Math.max(1, page), historyTotalPages.value)
  historyLoading.value = true
  selectedDetails.value = null
  receipt.value = null
  try {
    historyPage.value = nextPage
    const response = await billingApi.getMyHistory({ page: nextPage, pageSize: historyPageSize })
    if (generation === loadGeneration) applyHistoryResponse(response)
  } catch (requestError) {
    if (generation === loadGeneration) ElMessage.error(requestError.response?.data?.message || t('Không thể tải lịch sử thanh toán.', 'Could not load payment history.'))
  } finally {
    if (generation === loadGeneration) historyLoading.value = false
  }
}

const isTrustedHandoffOrder = (order) => Boolean(
  order &&
  routeOrderId.value &&
  String(order.id).toLowerCase() === routeOrderId.value.toLowerCase() &&
  isOrderForPlan(order, planCode.value) &&
  isKnownPaymentOrderStatus(order.status)
)

const resolveHandoffOrder = async (knownOrders) => {
  if (!routeOrderId.value) return null
  const knownOrder = knownOrders.find(isTrustedHandoffOrder)
  if (knownOrder && !shouldFetchPaymentOrderDetails(knownOrder, clock.value)) return knownOrder
  try {
    const details = unwrapBillingData(await billingApi.getOrderDetails(routeOrderId.value))
    const order = details?.order
    if (isTrustedHandoffOrder(order)) return order
    return knownOrder || null
  } catch {
    return knownOrder || null
  }
}

const mergeOrderIntoState = (order) => {
  if (!order) return
  const historyOrder = orders.value.find(item => item.id === order.id)
  const pendingOrder = billing.value?.pendingOrder?.id === order.id ? billing.value.pendingOrder : null
  const mergedExistingOrder = mergePaymentOrder(historyOrder, pendingOrder)
  const mergedOrder = mergePaymentOrder(mergedExistingOrder, order)
  orders.value = [mergedOrder, ...orders.value.filter(item => item.id !== order.id)]
  if (isActivePendingOrder(mergedOrder, clock.value)) {
    billing.value = { ...(billing.value || {}), pendingOrder: mergedOrder }
  }
}

const loadData = async () => {
  const generation = ++loadGeneration
  loading.value = true
  error.value = ''
  qrFailed.value = false
  handoffLoading.value = Boolean(routeOrderId.value)
  try {
    const [pricingResponse, billingResponse, historyResponse] = await Promise.all([
      axiosClient.get('/public/pricing'), billingApi.getMe(), billingApi.getMyHistory({ page: 1, pageSize: historyPageSize })
    ])
    if (generation !== loadGeneration) return
    const plans = pricingResponse.data?.data?.plans || []
    plan.value = plans.find(item => String(item.id || item.code).toLowerCase() === planCode.value) || null
    billing.value = unwrapBillingData(billingResponse)
    historyPage.value = 1
    applyHistoryResponse(historyResponse)
    const handoffOrder = await resolveHandoffOrder([billing.value?.pendingOrder, ...orders.value])
    if (generation !== loadGeneration) return
    mergeOrderIntoState(handoffOrder)
    if (!plan.value) error.value = t('Gói dịch vụ không tồn tại hoặc chưa được công khai.', 'This plan does not exist or is not published.')
  } catch (requestError) {
    if (generation === loadGeneration) error.value = requestError.response?.data?.message || t('Vui lòng thử lại sau.', 'Please try again later.')
  } finally {
    if (generation === loadGeneration) {
      loading.value = false
      handoffLoading.value = false
    }
  }
}

const refreshPaymentState = async () => {
  const pendingOrder = activeOrder.value
  if (!pendingOrder || refreshing.value) return
  const generation = loadGeneration
  refreshing.value = true
  try {
    const detail = unwrapBillingData(await billingApi.getOrderDetails(pendingOrder.id))
    const latestOrder = detail?.order
    if (generation !== loadGeneration || !latestOrder) return

    const wasPending = isActivePendingOrder(pendingOrder, clock.value)
    mergeOrderIntoState(latestOrder)

    if (wasPending && !isActivePendingOrder(latestOrder, clock.value)) {
      const [billingResponse, historyResponse] = await Promise.all([
        billingApi.getMe(), billingApi.getMyHistory({ page: 1, pageSize: historyPageSize })
      ])
      if (generation === loadGeneration) {
        billing.value = unwrapBillingData(billingResponse)
        historyPage.value = 1
        applyHistoryResponse(historyResponse)
        mergeOrderIntoState(latestOrder)
      }
    }
  } catch {
    // Polling is deliberately quiet. The manual check button remains available.
  } finally {
    refreshing.value = false
  }
}

const stopPolling = () => {
  if (pollTimer) clearInterval(pollTimer)
  pollTimer = null
}
const syncPolling = () => {
  stopPolling()
  if (shouldPollPaymentOrder(activeOrder.value, clock.value)) pollTimer = setInterval(refreshPaymentState, 7000)
}

const createOrder = async () => {
  const generation = loadGeneration
  submitting.value = true
  qrFailed.value = false
  try {
    const response = await checkoutOrderGate(planCode.value)
    const order = response
    if (generation !== loadGeneration) return
    billing.value = { ...(billing.value || {}), pendingOrder: order }
    await loadHistory(1)
    if (generation !== loadGeneration) return
    mergeOrderIntoState(order)
    ElMessage.success(t('Đã tạo đơn thanh toán.', 'Payment order created.'))
  } catch (requestError) {
    ElMessage.error(requestError.response?.data?.message || (requestError.message === 'Invalid payment order response.' ? t('Dữ liệu đơn thanh toán trả về không hợp lệ.', 'The payment order response is invalid.') : requestError.message) || t('Không thể tạo đơn.', 'Could not create order.'))
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
    ElMessage.error(requestError.response?.data?.message || t('Không thể kích hoạt Free.', 'Could not activate Free.'))
  } finally {
    submitting.value = false
  }
}

const copyText = async (value, message) => {
  if (value == null || value === '') return
  try {
    await navigator.clipboard.writeText(String(value))
    ElMessage.success(message)
  } catch {
    ElMessage.error(t('Không thể sao chép lúc này.', 'Could not copy right now.'))
  }
}
const copySupportContext = () => {
  const order = activeOrder.value || latestOrder.value
  if (!order) return
  copyText(`OrderId: ${order.id}\nTransferCode: ${order.transferCode}`, t('Đã sao chép thông tin để đối soát.', 'Reconciliation details copied.'))
}
const openDetails = async (order) => {
  try { selectedDetails.value = unwrapBillingData(await billingApi.getOrderDetails(order.id)); receipt.value = null } catch (requestError) { ElMessage.error(requestError.response?.data?.message || t('Không thể tải chi tiết đơn.', 'Could not load order details.')) }
}
const openReceipt = async (order) => {
  try { receipt.value = unwrapBillingData(await billingApi.getReceipt(order.id)); selectedDetails.value = null } catch (requestError) { ElMessage.error(requestError.response?.data?.message || t('Không thể tải biên nhận.', 'Could not load receipt.')) }
}
const resendReceipt = async () => {
  if (!receipt.value) return
  try { const response = await billingApi.resendReceipt(receipt.value.order.id); ElMessage.success(response.data?.message || t('Đã yêu cầu gửi lại biên nhận.', 'Receipt resend requested.')) } catch (requestError) { ElMessage.error(requestError.response?.data?.message || t('Không thể gửi lại biên nhận.', 'Could not resend receipt.')) }
}
const priceLabel = (amount) => amount == null ? t('Liên hệ', 'Contact') : `${new Intl.NumberFormat(isVi.value ? 'vi-VN' : 'en-US').format(amount)} VND`
const formatCreditCount = (value) => new Intl.NumberFormat(isVi.value ? 'vi-VN' : 'en-US').format(Number(value) || 0)
const formatDate = (value) => value ? new Intl.DateTimeFormat(isVi.value ? 'vi-VN' : 'en-US', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '-'
const statusLabel = (status) => ({ Preparing: t('Đang chuẩn bị', 'Preparing'), Pending: t('Đang chờ thanh toán', 'Waiting for payment'), Paid: t('Đã thanh toán', 'Paid'), Expired: t('Đã hết hạn', 'Expired'), Failed: t('Thanh toán thất bại', 'Payment failed'), Rejected: t('Đơn bị từ chối', 'Order rejected'), Idle: t('Chưa tạo đơn thanh toán', 'No payment order') }[status] || status)
const bucketProgress = (bucket) => {
  const granted = Number(bucket?.granted) || 0
  const remaining = Number(bucket?.remaining) || 0
  return granted > 0 ? Math.min(100, Math.max(0, Math.round((remaining / granted) * 100))) : 0
}
const bucketStatusLabel = (bucket) => ({ Active: t('Đang dùng', 'Active'), Future: t('Sắp có hiệu lực', 'Upcoming'), Expired: t('Đã hết hạn', 'Expired'), Consumed: t('Đã dùng hết', 'Consumed') }[bucket?.status] || bucket?.status || t('Đang theo dõi', 'Tracking'))
const bucketDateLabel = (bucket) => bucket?.status === 'Future' ? t('Có hiệu lực từ', 'Starts') : t('Hết hạn', 'Expires')
const bucketDateValue = (bucket) => bucket?.status === 'Future' ? bucket?.validFrom : bucket?.expiresAt
const displayStatus = (order) => getOrderDisplayStatus(order, clock.value)

const resetPlanState = () => {
  stopPolling()
  plan.value = null
  billing.value = null
  orders.value = []
  history.value = []
  historyTotal.value = 0
  historyPage.value = 1
  selectedDetails.value = null
  receipt.value = null
  qrFailed.value = false
  error.value = ''
}

watch(checkoutRouteKey, async () => {
  resetPlanState()
  await loadData()
  syncPolling()
})
watch(activeOrder, syncPolling)
onMounted(async () => {
  clockTimer = setInterval(() => { clock.value = Date.now() }, 1000)
  await loadData()
  syncPolling()
})
onBeforeUnmount(() => {
  if (clockTimer) clearInterval(clockTimer)
  stopPolling()
})
</script>

<style scoped>
/* Checkout presentation tokens. The payment state machine remains in script; these styles only shape its hierarchy. */
.checkout-page {
  --checkout-bg: var(--sp-bg, #f4f7fb);
  --checkout-surface: var(--sp-surface, #ffffff);
  --checkout-raised: var(--sp-surface-raised, #eef4fb);
  --checkout-muted-surface: var(--sp-surface-muted, #f3f6fa);
  --checkout-border: var(--sp-border, #d7e1ee);
  --checkout-border-strong: var(--sp-border-strong, #b9c9dc);
  --checkout-text: var(--sp-text, #102033);
  --checkout-muted: var(--sp-text-muted, #637083);
  --checkout-primary: var(--sp-primary, #0ea5e9);
  --checkout-primary-hover: var(--sp-primary-hover, #0284c7);
  --checkout-success: var(--sp-success, #10b981);
  --checkout-warning: var(--sp-warning, #f59e0b);
  --checkout-danger: var(--sp-danger, #ef4444);
  --checkout-shadow: var(--sp-shadow-md, 0 14px 34px rgba(16, 32, 51, .08));
  min-height: 100dvh;
  background: var(--checkout-bg);
  color: var(--checkout-text);
  font-family: var(--sp-font-ui, Inter, system-ui, sans-serif);
}
.checkout-page, .checkout-page * { box-sizing: border-box; }
.checkout-page .checkout-nav { display: flex; align-items: center; justify-content: space-between; height: 72px; padding: 0 clamp(20px, 4vw, 64px); border-color: var(--checkout-border); background: var(--checkout-surface); backdrop-filter: none; }
.checkout-page .back-button, .checkout-page .support-action, .checkout-page .close-detail { display: inline-flex; align-items: center; gap: 8px; min-height: 44px; color: var(--checkout-muted); font-family: inherit; }
.checkout-page .back-button:hover, .checkout-page .support-action:hover, .checkout-page .close-detail:hover { color: var(--checkout-primary); }
.checkout-page .brand { color: var(--checkout-text); font-family: var(--sp-font-display, var(--sp-font-ui, Inter, sans-serif)); }
.checkout-page .brand > span { color: var(--checkout-primary); }
.checkout-page .checkout-shell { width: min(1240px, calc(100% - 40px)); margin: 0 auto; padding: 44px 0 80px; }
.checkout-page .checkout-intro { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: end; gap: 32px; min-height: 148px; padding-bottom: 36px; border-bottom: 1px solid var(--checkout-border); }
.checkout-page .eyebrow, .checkout-page .section-kicker, .checkout-page .visual-meta, .checkout-page .state-eyebrow { color: var(--checkout-muted); font-size: 11px; font-weight: 800; letter-spacing: .12em; text-transform: uppercase; }
.checkout-page .checkout-intro h1 { margin: 9px 0 12px; color: var(--checkout-text); font-size: clamp(34px, 4vw, 52px); letter-spacing: -.045em; line-height: 1.02; }
.checkout-page .intro-copy { max-width: 62ch; color: var(--checkout-muted); font-size: 15px; line-height: 1.65; }
.checkout-page .price-block { display: grid; min-width: 240px; gap: 6px; text-align: right; }
.checkout-page .price-block span, .checkout-page .price-block small { color: var(--checkout-muted); font-size: 13px; }
.checkout-page .price-block strong { color: var(--checkout-text); font-size: clamp(25px, 3vw, 34px); letter-spacing: -.04em; font-variant-numeric: tabular-nums; }
.checkout-state-flow { margin-top: 28px; }
.checkout-state-flow > * { animation: checkout-state-in .22s ease both; }
.checkout-page .payment-stage { display: grid; grid-template-columns: minmax(0, 1.12fr) minmax(360px, .88fr); gap: 20px; align-items: start; }
.checkout-page .payment-stage > * { min-width: 0; }
.checkout-page .payment-visual, .checkout-page .order-card, .checkout-page .state-composition, .checkout-page .free-panel, .checkout-page .billing-history, .checkout-page .billing-detail, .checkout-page .wallet-surface { border: 1px solid var(--checkout-border); border-radius: 16px; background: var(--checkout-surface); }
.checkout-page .payment-visual { min-height: 0; padding: clamp(24px, 3vw, 36px); }
.checkout-page .order-card { position: sticky; top: 20px; padding: clamp(24px, 3vw, 32px); box-shadow: var(--checkout-shadow); }
.checkout-page .section-topline, .checkout-page .visual-heading, .checkout-page .order-card-heading, .checkout-page .countdown-block { display: flex; align-items: center; justify-content: space-between; gap: 16px; }
.checkout-page .section-topline { min-width: 0; }
.checkout-page .section-kicker { display: inline-flex; align-items: center; gap: 8px; color: var(--checkout-primary); }
.checkout-page .state-chip { display: inline-flex; align-items: center; gap: 7px; border: 1px solid transparent; border-radius: 999px; padding: 6px 10px; font-size: 12px; font-weight: 800; }
.checkout-page .state-chip.pending { color: #8a5a00; border-color: #f3d899; background: #fff7df; }
.checkout-page .visual-heading { align-items: flex-end; margin: 44px 0 18px; }
.checkout-page .visual-heading h2, .checkout-page .order-card h2 { color: var(--checkout-text); font-size: clamp(21px, 2.5vw, 28px); letter-spacing: -.035em; }
.checkout-page .visual-heading p, .checkout-page .order-card > p { color: var(--checkout-muted); line-height: 1.65; }
.checkout-page .visual-meta { padding-bottom: 5px; }
.checkout-page .qr-frame { width: min(390px, 100%); margin: 26px auto 24px; padding: 15px; border-color: var(--checkout-border-strong); border-radius: 14px; background: #ffffff; }
.checkout-page .qr-frame img { display: block; width: 100%; height: 100%; object-fit: contain; }
.checkout-page .qr-placeholder { color: var(--checkout-muted); }
.checkout-page .qr-placeholder strong { color: var(--checkout-text); }
.checkout-page .payment-warning { display: flex; align-items: flex-start; gap: 9px; max-width: 60ch; margin: 0 auto; border-left-color: var(--checkout-primary); color: var(--checkout-text); background: var(--checkout-raised); }
.checkout-page .payment-warning svg { color: var(--checkout-primary); }
.checkout-page .secondary-note { color: var(--checkout-muted); line-height: 1.6; }
.checkout-page .support-action { justify-content: flex-start; padding: 0; color: var(--checkout-primary); font-size: 13px; }
.checkout-page .order-card-heading { align-items: flex-start; border-color: var(--checkout-border); }
.checkout-page .order-card-heading > svg { color: var(--checkout-primary); }
.checkout-page .order-summary { display: grid; gap: 6px; margin: 23px 0 13px; }
.checkout-page .order-summary span, .checkout-page .detail-list dt, .checkout-page .countdown-block span { color: var(--checkout-muted); }
.checkout-page .order-summary strong, .checkout-page .detail-list dd, .checkout-page .countdown-block strong { color: var(--checkout-text); }
.checkout-page .detail-list { margin: 0; }
.checkout-page .detail-list > div { display: grid; grid-template-columns: .85fr 1.15fr; gap: 18px; padding: 13px 0; border-color: var(--checkout-border); }
.checkout-page .detail-list dd { margin: 0; text-align: right; overflow-wrap: anywhere; }
.checkout-page .copy-detail dd { display: flex; align-items: center; justify-content: flex-end; gap: 8px; }
.checkout-page .copy-detail button, .checkout-page .transfer-content button { width: 32px; height: 32px; border-color: var(--checkout-border); color: var(--checkout-primary); }
.checkout-page .copy-detail button:hover, .checkout-page .transfer-content button:hover { border-color: var(--checkout-primary); background: var(--checkout-raised); }
.checkout-page .transfer-content { margin: 22px 0; padding: 16px; border-color: var(--checkout-border-strong); background: var(--checkout-raised); }
.checkout-page .transfer-content > div { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.checkout-page .transfer-content strong { display: block; margin: 12px 0 7px; font-size: 18px; letter-spacing: .04em; overflow-wrap: anywhere; }
.checkout-page .transfer-content small { display: block; }
.checkout-page .transfer-content > div, .checkout-page .transfer-content small { color: var(--checkout-muted); }
.checkout-page .transfer-content strong { color: var(--checkout-primary); }
.checkout-page .countdown-block { margin: 20px 0; padding: 15px 0; border-color: var(--checkout-border); }
.checkout-page .countdown-block > div { display: grid; gap: 5px; }
.checkout-page .countdown-block > div:last-child { text-align: right; }
.checkout-page .countdown-block > div:last-child strong { color: var(--checkout-primary); }
.checkout-page .primary-button, .checkout-page .secondary-button { min-height: 46px; border-radius: 9px; font-family: inherit; }
.checkout-page .primary-button { width: 100%; border-color: var(--checkout-primary); background: var(--checkout-primary); }
.checkout-page .primary-button:hover:not(:disabled) { border-color: var(--checkout-primary-hover); background: var(--checkout-primary-hover); }
.checkout-page .secondary-button { border-color: var(--checkout-border-strong); color: var(--checkout-text); background: var(--checkout-surface); }
.checkout-page .secondary-button:hover:not(:disabled) { border-color: var(--checkout-primary); color: var(--checkout-primary); background: var(--checkout-raised); }
.checkout-page .primary-button:focus-visible, .checkout-page .secondary-button:focus-visible, .checkout-page .copy-detail button:focus-visible, .checkout-page .transfer-content button:focus-visible, .checkout-page .back-button:focus-visible, .checkout-page .support-action:focus-visible, .checkout-page .close-detail:focus-visible, .checkout-page .pagination-controls button:focus-visible { outline: 3px solid color-mix(in srgb, var(--checkout-primary) 38%, transparent); outline-offset: 3px; }

.state-composition { min-height: 360px; padding: clamp(30px, 5vw, 56px); }
.preparing-state, .idle-state, .terminal-state { display: grid; place-items: center; align-content: center; text-align: center; }
.state-icon { display: grid; width: 54px; height: 54px; place-items: center; margin-bottom: 18px; border-radius: 50%; }
.state-icon.neutral { color: var(--checkout-primary); background: var(--checkout-raised); }
.state-icon.preparing { color: var(--checkout-primary); background: var(--checkout-raised); }
.state-icon.success { color: #08764d; background: #dff8ec; }
.state-icon.danger { color: #b42318; background: #fee8e6; }
.state-composition h2 { max-width: 24ch; margin: 0 0 10px; color: var(--checkout-text); font-size: clamp(26px, 4vw, 36px); letter-spacing: -.04em; }
.state-composition > p, .success-hero p { max-width: 58ch; margin: 0; color: var(--checkout-muted); line-height: 1.65; }
.state-eyebrow { display: block; margin-bottom: 10px; }
.state-actions { display: flex; flex-wrap: wrap; align-items: center; justify-content: center; gap: 16px; margin-top: 24px; }
.state-actions .primary-button { width: auto; min-width: 190px; }
.state-actions .support-action { margin-top: 0; }
.idle-summary { display: flex; justify-content: space-between; width: min(360px, 100%); margin: 24px 0 18px; padding: 15px 0; border-top: 1px solid var(--checkout-border); border-bottom: 1px solid var(--checkout-border); }
.idle-summary strong { font-variant-numeric: tabular-nums; }
.idle-state > .primary-button { width: min(360px, 100%); }
.success-composition { padding: clamp(28px, 5vw, 52px); }
.success-hero { display: flex; align-items: flex-start; gap: 18px; }
.success-hero .state-icon { flex: 0 0 auto; margin: 0; }
.success-hero h2 { max-width: none; margin-bottom: 8px; }
.paid-proof { display: grid; grid-template-columns: 1fr 1fr; margin: 34px 0 0; border: 1px solid var(--checkout-border); border-radius: 12px; overflow: hidden; }
.paid-proof > div { display: grid; gap: 7px; padding: 22px 24px; background: var(--checkout-muted-surface); }
.paid-proof > div + div { border-left: 1px solid var(--checkout-border); background: var(--checkout-surface); }
.paid-proof span, .paid-proof small, .success-facts dt { color: var(--checkout-muted); font-size: 12px; }
.paid-proof strong { color: var(--checkout-text); font-size: 24px; letter-spacing: -.025em; }
.proof-amount strong { font-size: clamp(28px, 4vw, 40px); font-variant-numeric: tabular-nums; }
.success-facts { display: grid; grid-template-columns: repeat(4, 1fr); gap: 0; margin: 0; padding: 20px 0 4px; }
.success-facts > div { display: grid; gap: 7px; padding: 0 18px; border-right: 1px solid var(--checkout-border); }
.success-facts > div:first-child { padding-left: 0; }
.success-facts > div:last-child { padding-right: 0; border-right: 0; }
.success-facts dt { margin: 0; }
.success-facts dd { margin: 0; color: var(--checkout-text); font-size: 14px; font-weight: 750; text-align: left; overflow-wrap: anywhere; }
.success-composition .success-actions { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 30px; }
.success-composition .primary-button, .success-composition .secondary-button { width: auto; min-width: 190px; }

.account-overview, .wallet-surface { margin-top: 32px; }
.section-heading { display: flex; align-items: flex-end; justify-content: space-between; gap: 20px; margin-bottom: 14px; }
.section-heading h2 { margin: 4px 0 0; color: var(--checkout-text); font-size: 22px; letter-spacing: -.03em; }
.section-note { color: var(--checkout-muted); font-size: 12px; }
.account-overview-surface { display: grid; grid-template-columns: minmax(190px, .82fr) minmax(280px, 1.35fr) minmax(220px, .93fr); align-items: stretch; border: 1px solid var(--checkout-border); border-radius: 14px; background: var(--checkout-surface); overflow: hidden; }
.account-plan, .account-wallet, .account-period { display: grid; align-content: center; gap: 7px; min-height: 144px; padding: 24px 26px; }
.account-plan { background: var(--checkout-muted-surface); }
.account-wallet { border-left: 1px solid var(--checkout-border); border-right: 1px solid var(--checkout-border); }
.account-plan > span, .account-wallet > span, .account-plan > small, .account-wallet > small, .account-period dt { color: var(--checkout-muted); font-size: 12px; }
.account-plan strong { color: var(--checkout-text); font-size: 24px; letter-spacing: -.03em; }
.account-wallet strong { color: var(--checkout-text); font-size: clamp(36px, 5vw, 48px); letter-spacing: -.045em; line-height: 1; font-variant-numeric: tabular-nums; }
.account-status { display: inline-flex; align-items: center; gap: 7px; }
.account-status span { width: 7px; height: 7px; border-radius: 50%; background: var(--checkout-success); }
.account-period { grid-template-columns: 1fr; gap: 16px; margin: 0; }
.account-period > div { display: grid; gap: 5px; }
.account-period dt { margin: 0; }
.account-period dd { margin: 0; color: var(--checkout-text); font-size: 14px; font-weight: 750; text-align: left; }
.account-summary-grid { display: grid; grid-template-columns: repeat(3, 1fr); border: 1px solid var(--checkout-border); border-radius: 14px; background: var(--checkout-surface); overflow: hidden; }
.summary-item { display: grid; min-height: 128px; gap: 7px; padding: 22px 24px; align-content: center; }
.summary-item + .summary-item { border-left: 1px solid var(--checkout-border); }
.summary-item span, .summary-item small { color: var(--checkout-muted); font-size: 12px; }
.summary-item strong { color: var(--checkout-text); font-size: 20px; letter-spacing: -.02em; }
.summary-item.wallet-summary strong { font-size: 30px; font-variant-numeric: tabular-nums; }
.wallet-surface { padding: 24px; }
.wallet-surface .section-heading { margin-bottom: 18px; }
.bucket-row { display: grid; gap: 10px; padding: 17px 0; border-top: 1px solid var(--checkout-border); }
.bucket-main { display: flex; align-items: center; justify-content: space-between; gap: 18px; }
.bucket-main > div { display: flex; align-items: center; flex-wrap: wrap; gap: 9px; }
.bucket-main > div > strong { color: var(--checkout-text); font-size: 13px; letter-spacing: .08em; }
.bucket-status { border-radius: 999px; padding: 4px 8px; color: var(--checkout-muted); background: var(--checkout-muted-surface); font-size: 11px; font-weight: 750; }
.bucket-status.active { color: #08764d; background: #dff8ec; }
.bucket-status.expired, .bucket-status.consumed { color: #a33a36; background: #fee8e6; }
.bucket-remaining { color: var(--checkout-text); font-size: 21px; font-variant-numeric: tabular-nums; }
.bucket-remaining small { color: var(--checkout-muted); font-size: 13px; font-weight: 650; }
.bucket-progress { height: 7px; overflow: hidden; border-radius: 999px; background: var(--checkout-raised); }
.bucket-progress span { display: block; height: 100%; border-radius: inherit; background: var(--checkout-primary); transition: width .25s ease; }
.bucket-meta { display: flex; justify-content: space-between; gap: 16px; color: var(--checkout-muted); font-size: 12px; }

.checkout-page .billing-history, .checkout-page .billing-detail { margin-top: 32px; padding: clamp(20px, 3vw, 28px); }
.checkout-page .history-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 18px; }
.checkout-page .history-heading h2 { color: var(--checkout-text); font-size: 22px; }
.checkout-page .history-heading span, .checkout-page .history-heading small { color: var(--checkout-muted); }
.history-table { margin-top: 20px; }
.history-table-header, .history-row { display: grid; grid-template-columns: minmax(160px, 1.4fr) minmax(150px, 1.2fr) minmax(120px, .9fr) minmax(120px, .9fr) auto; gap: 18px; align-items: center; }
.history-table-header { padding: 0 0 10px; color: var(--checkout-muted); font-size: 11px; font-weight: 800; letter-spacing: .08em; text-transform: uppercase; }
.history-row { padding: 17px 0; border-top: 1px solid var(--checkout-border); }
.history-row small, .history-date { color: var(--checkout-muted); font-size: 12px; }
.history-plan, .history-date, .history-amount, .history-status-cell { min-width: 0; }
.history-plan strong, .history-amount strong { color: var(--checkout-text); }
.history-plan small { display: block; margin-top: 5px; overflow-wrap: anywhere; }
.history-amount strong { font-variant-numeric: tabular-nums; }
.history-status-cell { display: flex; align-items: center; gap: 8px; color: var(--checkout-muted); font-size: 12px; }
.status-dot { width: 8px; height: 8px; flex: 0 0 auto; border-radius: 50%; background: var(--checkout-muted); }
.status-dot.paid { background: var(--checkout-success); }.status-dot.pending { background: var(--checkout-warning); }.status-dot.expired, .status-dot.failed, .status-dot.rejected { background: var(--checkout-danger); }
.history-actions { display: flex; justify-content: flex-end; gap: 12px; }
.history-actions button { min-height: 36px; padding: 0; border: 0; color: var(--checkout-primary); background: transparent; cursor: pointer; font-family: inherit; font-weight: 750; white-space: nowrap; }
.history-actions button:hover { text-decoration: underline; }
.checkout-page .pagination-controls { border-color: var(--checkout-border); }
.checkout-page .pagination-controls button { min-height: 40px; border-color: var(--checkout-border); border-radius: 8px; background: var(--checkout-surface); color: var(--checkout-text); }
.checkout-page .billing-detail dl > div { border-color: var(--checkout-border); }.checkout-page .billing-detail dt, .checkout-page .muted-copy, .checkout-page .receipt-card p, .checkout-page .receipt-card small, .checkout-page .timeline-list small { color: var(--checkout-muted); }.checkout-page .billing-detail dd { color: var(--checkout-text); }.checkout-page .close-detail { font-size: 24px; }
.checkout-page .free-panel { max-width: 680px; margin: 32px auto 0; padding: 48px 32px; box-shadow: none; }.checkout-page .free-panel svg { color: var(--checkout-primary); }.checkout-page .free-panel h2 { color: var(--checkout-text); }.checkout-page .free-panel p { color: var(--checkout-muted); }
.checkout-page .checkout-shell:has(> .state-panel.error-state) { display: grid; align-content: center; min-height: calc(100dvh - 72px); padding-top: 0; }
.checkout-page .state-panel.error-state { max-width: 760px; margin: 0 auto; padding: 24px; border: 1px solid #f1b8b4; border-radius: 14px; background: var(--checkout-surface); color: var(--checkout-danger); }.checkout-page .state-panel.error-state p { color: var(--checkout-muted); }

@keyframes checkout-state-in { from { opacity: 0; transform: translateY(5px); } to { opacity: 1; transform: translateY(0); } }
@media (max-width: 1024px) { .checkout-page .payment-stage { grid-template-columns: minmax(0, 1fr) minmax(320px, .88fr); }.checkout-page .order-card { position: static; }.success-facts { grid-template-columns: repeat(2, 1fr); gap: 18px 0; }.success-facts > div:nth-child(2) { border-right: 0; }.success-facts > div:nth-child(3) { padding-left: 0; }.success-facts > div:nth-child(4) { padding-right: 0; } }
@media (max-width: 760px) { .checkout-page .checkout-shell { width: min(100% - 28px, 1240px); padding-top: 32px; }.checkout-page .checkout-intro { grid-template-columns: 1fr; gap: 18px; }.checkout-page .price-block { min-width: 0; text-align: left; }.checkout-page .payment-stage { grid-template-columns: 1fr; }.checkout-page .payment-visual { min-height: 0; }.checkout-page .account-overview-surface { grid-template-columns: 1fr; }.account-plan, .account-wallet, .account-period { min-height: 0; }.account-wallet { border: 0; border-top: 1px solid var(--checkout-border); border-bottom: 1px solid var(--checkout-border); }.checkout-page .account-summary-grid { grid-template-columns: 1fr; }.summary-item { min-height: 0; }.summary-item + .summary-item { border-top: 1px solid var(--checkout-border); border-left: 0; }.section-heading { align-items: flex-start; flex-direction: column; gap: 8px; }.history-table-header { display: none; }.history-row { grid-template-columns: 1fr auto; gap: 12px 16px; }.history-date { text-align: right; }.history-amount { grid-column: 1; }.history-status-cell { grid-column: 2; grid-row: 2; justify-content: flex-end; }.history-actions { grid-column: 1 / -1; justify-content: flex-start; padding-top: 4px; }.success-composition .success-actions { align-items: stretch; flex-direction: column; }.success-composition .primary-button, .success-composition .secondary-button { width: 100%; }.bucket-meta { align-items: flex-start; flex-direction: column; gap: 5px; } }
@media (max-width: 520px) { .checkout-page .checkout-nav { height: 64px; padding: 0 14px; }.checkout-page .back-button { font-size: 12px; }.checkout-page .brand { font-size: 16px; }.checkout-page .checkout-shell { width: min(100% - 24px, 1240px); padding: 26px 0 56px; }.checkout-page .checkout-intro h1 { font-size: 36px; }.checkout-page .payment-visual, .checkout-page .order-card, .checkout-page .state-composition, .checkout-page .billing-history, .checkout-page .billing-detail, .checkout-page .wallet-surface { border-radius: 14px; padding: 20px; }.checkout-page .section-topline { align-items: flex-start; flex-wrap: wrap; }.checkout-page .visual-heading { align-items: flex-start; flex-direction: column; gap: 8px; margin-top: 34px; }.checkout-page .visual-meta { padding-bottom: 0; }.checkout-page .detail-list > div { grid-template-columns: 1fr; gap: 6px; }.checkout-page .detail-list dd { text-align: left; }.checkout-page .copy-detail dd { justify-content: flex-start; }.checkout-page .countdown-block { align-items: flex-start; flex-direction: column; }.checkout-page .countdown-block > div:last-child { text-align: left; }.success-hero { gap: 13px; }.success-hero .state-icon { width: 46px; height: 46px; }.paid-proof { grid-template-columns: 1fr; }.paid-proof > div + div { border-top: 1px solid var(--checkout-border); border-left: 0; }.success-facts { grid-template-columns: 1fr 1fr; gap: 18px 0; }.success-facts > div { padding: 0 12px; }.success-facts > div:first-child, .success-facts > div:nth-child(3) { padding-left: 0; }.success-facts > div:nth-child(2), .success-facts > div:last-child { padding-right: 0; }.bucket-main { align-items: flex-start; flex-direction: column; gap: 10px; }.history-row { grid-template-columns: 1fr; }.history-date, .history-status-cell { grid-column: auto; grid-row: auto; justify-content: flex-start; text-align: left; }.history-actions { padding-top: 0; }.state-actions { align-items: stretch; flex-direction: column; }.state-actions .primary-button { width: 100%; }.state-actions .support-action { justify-content: center; } }
.checkout-page .checkout-nav {
  min-width: 0;
  gap: 12px 20px;
}

.checkout-page .back-button {
  min-width: 0;
  max-width: 100%;
}

.checkout-page .brand {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
  flex: 0 1 auto;
}

@media (max-width: 520px) {
  .checkout-page .checkout-nav {
    height: auto;
    min-height: 64px;
    flex-wrap: wrap;
    align-items: flex-start;
    padding-block: 10px;
  }

  .checkout-page .back-button {
    flex: 1 1 100%;
    min-width: 0;
  }

  .checkout-page .brand {
    flex: 0 0 auto;
    margin-left: 0;
  }
}

@media (prefers-reduced-motion: reduce) { .checkout-state-flow > *, .checkout-page .bucket-progress span { animation: none; transition: none; } }
</style>
