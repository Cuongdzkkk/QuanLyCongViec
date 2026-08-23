<template>
  <main class="checkout-page">
    <header class="checkout-nav">
      <button type="button" class="back-button" @click="router.push('/#pricing')">
        <ArrowLeft :size="17" /> {{ t('Quay lại bảng giá', 'Back to pricing') }}
      </button>
      <div class="brand">SprintA <span>Billing</span></div>
    </header>

    <section class="checkout-shell" v-loading="loading">
      <div v-if="error" class="state-panel error-state" role="alert">
        <CircleAlert :size="24" />
        <div><strong>{{ t('Không thể tải thông tin thanh toán', 'Could not load billing details') }}</strong><p>{{ error }}</p></div>
      </div>

      <template v-else-if="plan">
        <header class="checkout-intro">
          <div>
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

        <div v-else class="payment-stage" :class="{ 'is-paid': paymentState === 'Paid' }">
          <section class="payment-visual">
            <div class="section-topline">
              <span class="section-kicker"><QrCode :size="16" /> {{ t('Thanh toán chuyển khoản', 'Bank transfer') }}</span>
              <span class="state-chip" :class="paymentState.toLowerCase()" aria-live="polite">{{ statusLabel(paymentState) }}</span>
            </div>

            <template v-if="activeOrder">
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
            </template>

            <div v-else-if="handoffLoading" class="state-panel preparing-panel">
              <Clock3 :size="28" />
              <div>
                <strong>{{ t('Đang chuẩn bị thông tin thanh toán', 'Preparing payment details') }}</strong>
                <p>{{ t('Đang xác thực đơn vừa tạo với tài khoản của bạn.', 'Verifying the order you just created for your account.') }}</p>
              </div>
            </div>

            <div v-else-if="paymentState === 'Paid'" class="state-panel success-panel">
              <ShieldCheck :size="28" />
              <div>
                <strong>{{ t('Thanh toán đã được ghi nhận', 'Payment confirmed') }}</strong>
                <p>{{ t('Gói đã cập nhật. Bạn có thể xem biên nhận và tiếp tục sử dụng AI credits mới.', 'Your plan is updated. You can view the receipt and continue with your new AI credits.') }}</p>
              </div>
            </div>

            <div v-else-if="paymentState === 'Expired'" class="state-panel expired-panel">
              <Clock3 :size="28" />
              <div>
                <strong>{{ t('Đơn thanh toán đã hết hạn', 'Payment order expired') }}</strong>
                <p>{{ t('Mã chuyển khoản cũ không còn hiệu lực. Tạo đơn mới để nhận hướng dẫn thanh toán mới.', 'The previous transfer code is no longer active. Create a new order for fresh payment instructions.') }}</p>
              </div>
            </div>

            <div v-else-if="paymentState === 'Failed' || paymentState === 'Rejected'" class="state-panel expired-panel">
              <CircleAlert :size="28" />
              <div>
                <strong>{{ statusLabel(paymentState) }}</strong>
                <p>{{ t('Đơn này không thể tiếp tục. Tạo một đơn mới hoặc liên hệ quản trị viên để đối soát.', 'This order cannot continue. Create a new order or contact an administrator for reconciliation.') }}</p>
              </div>
            </div>

            <div v-else class="pre-checkout-state">
              <QrCode :size="38" />
              <strong>{{ t('Chưa tạo đơn thanh toán', 'No payment order yet') }}</strong>
              <p>{{ t('Bạn chưa có đơn thanh toán đang hoạt động cho gói này.', 'You do not have an active payment order for this plan.') }}</p>
            </div>

            <p v-if="activeOrder" class="secondary-note">{{ t('Bạn có thể chuyển khoản ngay. Gói chỉ được kích hoạt sau khi hệ thống ghi nhận thanh toán.', 'You can transfer now. The plan activates only after the system records the payment.') }}</p>
            <button v-if="activeOrder || paymentState === 'Paid' || paymentState === 'Expired'" type="button" class="support-action" @click="copySupportContext">
              <CircleHelp :size="16" /> {{ t('Đã chuyển khoản nhưng chưa được cập nhật?', 'Transferred but not updated?') }}
            </button>
          </section>

          <aside class="order-card">
            <template v-if="activeOrder">
              <div class="order-card-heading">
                <div><span class="eyebrow">{{ t('Đơn đang hoạt động', 'Active order') }}</span><h2>{{ t('Thông tin chuyển khoản', 'Transfer details') }}</h2></div>
                <Clock3 :size="20" />
              </div>

              <div class="order-summary">
                <span>{{ t('Gói đã chọn', 'Selected plan') }}</span>
                <strong>{{ activeOrder.planName || plan.name }}</strong>
              </div>

              <dl class="detail-list">
                <div><dt>{{ t('Ngân hàng', 'Bank') }}</dt><dd>{{ activeInstructions?.bankCode || t('Chưa cấu hình', 'Not configured') }}</dd></div>
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
            </template>

            <template v-else-if="handoffLoading">
              <span class="eyebrow">{{ t('Đang xác thực', 'Verifying order') }}</span>
              <h2>{{ t('Đang chuẩn bị thông tin thanh toán', 'Preparing payment details') }}</h2>
              <p>{{ t('Đơn vừa tạo đang được xác thực với tài khoản của bạn. Không cần tạo thêm đơn.', 'The order you just created is being verified for your account. There is no need to create another order.') }}</p>
            </template>

            <template v-else-if="paymentState === 'Paid'">
              <span class="status-badge paid"><ShieldCheck :size="14" /> {{ t('Đã thanh toán', 'Paid') }}</span>
              <h2>{{ t('Thanh toán thành công', 'Payment successful') }}</h2>
              <p>{{ t('Gói của bạn đã được cập nhật và credit đã sẵn sàng sử dụng.', 'Your plan is updated and the credits are ready to use.') }}</p>
              <div class="paid-summary">
                <span>{{ t('Gói đã mua', 'Purchased plan') }}</span>
                <strong>{{ paidOrder?.planName || billing?.planName || plan.name }}</strong>
                <small>{{ priceLabel(paidOrder?.amountVnd) }} · {{ formatDate(paidOrder?.paidAt) }}</small>
                <small>{{ billing?.totalRemainingCredits ?? billing?.remainingCredits ?? '-' }} {{ t('AI credits trong ví', 'AI wallet credits') }}</small>
              </div>
              <div class="success-actions">
                <button v-if="paidOrder && canShowPaymentReceipt(paidOrder)" type="button" class="primary-button" @click="openReceipt(paidOrder)">{{ t('Xem thanh toán', 'View payment') }}</button>
                <button type="button" class="secondary-button" @click="router.push('/dashboard')">{{ t('Tiếp tục sử dụng SprintA', 'Continue using SprintA') }}</button>
              </div>
            </template>

            <template v-else-if="paymentState === 'Expired'">
              <span class="status-badge expired"><Clock3 :size="14" /> {{ t('Đã hết hạn', 'Expired') }}</span>
              <h2>{{ t('Tạo đơn mới để tiếp tục', 'Create a new order to continue') }}</h2>
              <p>{{ t('Đơn cũ vẫn được giữ trong lịch sử để đối soát. Đơn mới sẽ có mã chuyển khoản khác.', 'The old order stays in history for reconciliation. A new order will have a different transfer code.') }}</p>
              <button type="button" class="primary-button" :disabled="submitting" @click="createOrder">{{ submitting ? t('Đang tạo đơn...', 'Creating order...') : t('Tạo đơn mới', 'Create new order') }}</button>
            </template>

            <template v-else-if="paymentState === 'Failed' || paymentState === 'Rejected'">
              <span class="status-badge expired"><CircleAlert :size="14" /> {{ statusLabel(paymentState) }}</span>
              <h2>{{ t('Tạo đơn mới để thử lại', 'Create a new order to try again') }}</h2>
              <p>{{ t('Đơn cũ vẫn được giữ trong lịch sử để đối soát. Bạn có thể tạo đơn mới khi đã sẵn sàng.', 'The old order stays in history for reconciliation. Create a new order when ready.') }}</p>
              <button type="button" class="primary-button" :disabled="submitting" @click="createOrder">{{ submitting ? t('Đang tạo đơn...', 'Creating order...') : t('Tạo đơn mới', 'Create new order') }}</button>
            </template>

            <template v-else>
              <span class="eyebrow">{{ t('Bắt đầu thanh toán', 'Start payment') }}</span>
              <h2>{{ t('Tạo đơn khi bạn đã sẵn sàng', 'Create an order when ready') }}</h2>
              <p>{{ t('SprintA sẽ tạo mã chuyển khoản duy nhất. Chỉ tạo đơn không kích hoạt gói.', 'SprintA creates a unique transfer code. Creating an order does not activate the plan.') }}</p>
              <div class="summary-line"><span>{{ plan.name }}</span><strong>{{ priceLabel(plan.monthlyPriceVnd) }}</strong></div>
              <button type="button" class="primary-button" :disabled="submitting" @click="createOrder">
                {{ submitting ? t('Đang tạo đơn...', 'Creating order...') : t(`Tạo đơn thanh toán · ${priceLabel(plan.monthlyPriceVnd).replace(' VND', 'đ')}`, `Create payment order · ${priceLabel(plan.monthlyPriceVnd)}`) }}
              </button>
            </template>
          </aside>
        </div>

        <section v-if="billing" class="current-entitlement">
          <div><span>{{ t('Gói hiện tại', 'Current plan') }}</span><strong>{{ billing.planName }}</strong></div>
          <div><span>{{ t('Ví AI còn lại', 'AI credit wallet') }}</span><strong>{{ billing.totalRemainingCredits ?? billing.remainingCredits }}</strong></div>
          <div><span>{{ t('Kết thúc kỳ', 'Period ends') }}</span><strong>{{ formatDate(billing.currentPeriodEnd) }}</strong></div>
        </section>
        <section v-if="billing?.creditBuckets?.length" class="credit-buckets">
          <div class="bucket-heading"><strong>{{ t('Chi tiết credit', 'Credit details') }}</strong><span>{{ t('Tự động dùng bucket sắp hết hạn trước', 'Soonest-expiring bucket is used first') }}</span></div>
          <div v-for="bucket in billing.creditBuckets" :key="bucket.id" class="bucket-row">
            <strong>{{ String(bucket.sourcePlan || '').toUpperCase() }}</strong>
            <span>{{ bucket.remaining }} / {{ bucket.granted }}</span>
            <small>{{ t('Hết hạn', 'Expires') }} {{ formatDate(bucket.expiresAt) }}</small>
          </div>
        </section>

        <section class="billing-history" v-if="history.length">
          <div class="history-heading"><div><span>{{ t('Lịch sử thanh toán', 'Payment history') }}</span><h2>{{ t('Đơn thanh toán', 'Payment orders') }}</h2></div><small>{{ historyTotal }} {{ t('giao dịch', 'transactions') }}</small></div>
          <div class="history-list">
            <article v-for="order in history" :key="order.id" class="history-row">
              <div><strong>{{ order.planName || order.planCode }}</strong><small>{{ formatDate(order.createdAt) }} · {{ order.transferCode }}</small></div>
              <div class="history-meta"><strong>{{ priceLabel(order.amountVnd) }}</strong><span class="history-status" :class="displayStatus(order).toLowerCase()">{{ statusLabel(displayStatus(order)) }}</span></div>
              <div class="history-actions"><button type="button" @click="openDetails(order)">{{ t('Chi tiết đơn', 'Order details') }}</button><button v-if="canShowPaymentReceipt(order)" type="button" @click="openReceipt(order)">{{ t('Biên nhận', 'Receipt') }}</button></div>
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
import {
  canShowPaymentReceipt,
  createCheckoutOrderGate,
  formatRemainingTime,
  getCheckoutState,
  getOrderDisplayStatus,
  getOrderRemainingSeconds,
  getPaymentCopyValues,
  isActivePendingOrder,
  isKnownPaymentOrderStatus,
  isOrderForPlan,
  mergePaymentOrder,
  selectActivePendingOrder,
  shouldPollPaymentOrder
} from '@/utils/billingCheckoutState'

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
const routeOrderId = computed(() => {
  const value = String(route.query.orderId || '').trim()
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value) ? value : ''
})
const checkoutRouteKey = computed(() => `${planCode.value}:${routeOrderId.value}`)
const isFree = computed(() => planCode.value === 'free' || Number(plan.value?.monthlyPriceVnd) === 0)
const isEnterprise = computed(() => planCode.value === 'enterprise' || plan.value?.monthlyPriceVnd == null)

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
const activeInstructions = computed(() => activeOrder.value?.paymentInstructions || null)
const activePaymentValues = computed(() => getPaymentCopyValues(activeOrder.value))
const qrImagePath = computed(() => qrFailed.value ? '' : (activeInstructions.value?.qrUrl || ''))
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
  if (knownOrder) return knownOrder
  try {
    const details = unwrapBillingData(await billingApi.getOrderDetails(routeOrderId.value))
    const order = details?.order
    return isTrustedHandoffOrder(order) ? order : null
  } catch {
    return null
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
const formatDate = (value) => value ? new Intl.DateTimeFormat(isVi.value ? 'vi-VN' : 'en-US', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '-'
const statusLabel = (status) => ({ Preparing: t('Đang chuẩn bị', 'Preparing'), Pending: t('Đang chờ thanh toán', 'Waiting for payment'), Paid: t('Đã thanh toán', 'Paid'), Expired: t('Đã hết hạn', 'Expired'), Failed: t('Thanh toán thất bại', 'Payment failed'), Rejected: t('Đơn bị từ chối', 'Order rejected'), Idle: t('Chưa tạo đơn thanh toán', 'No payment order') }[status] || status)
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
.checkout-page { min-height: 100dvh; background: radial-gradient(circle at 10% 0%, color-mix(in srgb, var(--color-primary, #3563e9) 8%, transparent), transparent 32%), var(--color-bg, #f6f8fb); color: var(--color-text-primary, #172033); }
.checkout-nav { height: 68px; display: flex; align-items: center; justify-content: space-between; padding: 0 clamp(20px, 5vw, 72px); border-bottom: 1px solid var(--color-border, #dfe4ec); background: color-mix(in srgb, var(--color-surface, #fff) 94%, transparent); backdrop-filter: blur(16px); }
.back-button, .support-action, .close-detail { display: inline-flex; align-items: center; gap: 8px; border: 0; background: transparent; color: var(--color-text-muted, #667085); cursor: pointer; font-weight: 650; }.back-button:hover, .support-action:hover, .close-detail:hover { color: var(--color-primary, #3563e9); }
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
.credit-buckets { margin-top: 16px; border: 1px solid var(--color-border, #dfe4ec); border-radius: 12px; background: var(--color-surface, #fff); overflow: hidden; }.bucket-heading { display: flex; justify-content: space-between; gap: 16px; padding: 15px 18px; border-bottom: 1px solid var(--color-border, #dfe4ec); }.bucket-heading span, .bucket-row small { color: var(--color-text-muted, #667085); font-size: 12px; }.bucket-row { display: grid; grid-template-columns: 90px 1fr auto; gap: 16px; align-items: center; padding: 13px 18px; border-bottom: 1px solid var(--color-border, #dfe4ec); }.bucket-row:last-child { border-bottom: 0; }.bucket-row strong { color: var(--color-primary, #3563e9); }
.free-panel { max-width: 560px; margin: 34px auto 0; text-align: center; }.free-panel svg { color: var(--color-primary, #3563e9); }.free-panel h2 { margin: 14px 0 8px; }.free-panel p { color: var(--color-text-muted, #667085); line-height: 1.6; }.free-panel .primary-button { margin-top: 12px; max-width: 320px; }
.state-panel { display: flex; align-items: flex-start; gap: 14px; }.state-panel p { margin: 5px 0 0; color: var(--color-text-muted, #667085); }.error-state { color: #b42318; }
@media (max-width: 760px) { .checkout-shell { padding-top: 28px; }.checkout-heading { grid-template-columns: auto 1fr; }.price-block { grid-column: 2; text-align: left; }.payment-grid { grid-template-columns: 1fr; }.current-entitlement { grid-template-columns: 1fr; }.current-entitlement > div { border-right: 0; border-bottom: 1px solid var(--color-border, #dfe4ec); }.current-entitlement > div:last-child { border-bottom: 0; } }
.checkout-shell { width: min(1180px, calc(100% - 32px)); margin: 0 auto; padding: 48px 0 80px; min-height: 520px; }
.checkout-intro { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: end; gap: 32px; padding-bottom: 34px; }.eyebrow, .section-kicker, .visual-meta { color: var(--color-text-muted, #667085); font-size: 11px; font-weight: 800; letter-spacing: .12em; text-transform: uppercase; }.checkout-intro h1 { margin: 7px 0 10px; font-size: clamp(32px, 4vw, 52px); letter-spacing: -.045em; line-height: 1; }.intro-copy { max-width: 58ch; margin: 0; color: var(--color-text-muted, #667085); line-height: 1.65; }.price-block { display: grid; gap: 5px; min-width: 220px; text-align: right; }.price-block span, .price-block small { color: var(--color-text-muted, #667085); font-size: 13px; }.price-block strong { font-size: 27px; letter-spacing: -.035em; font-variant-numeric: tabular-nums; }
.payment-stage { display: grid; grid-template-columns: minmax(0, 1.08fr) minmax(360px, .92fr); gap: 22px; align-items: start; }.payment-visual, .order-card, .free-panel, .state-panel { border: 1px solid var(--color-border, #dfe4ec); border-radius: 18px; background: var(--color-surface, #fff); }.payment-visual { min-height: 640px; padding: clamp(22px, 3vw, 34px); }.order-card { padding: clamp(22px, 3vw, 32px); box-shadow: 0 16px 40px color-mix(in srgb, var(--color-primary, #3563e9) 8%, transparent); }
.section-topline, .visual-heading, .order-card-heading, .countdown-block, .support-action { display: flex; align-items: center; justify-content: space-between; gap: 16px; }.section-kicker { display: inline-flex; align-items: center; gap: 8px; color: var(--color-primary, #3563e9); }.state-chip, .status-badge { display: inline-flex; align-items: center; gap: 7px; border-radius: 7px; padding: 6px 9px; font-size: 12px; font-weight: 800; }.state-chip.pending, .status-badge { color: #875a00; background: #fff5d6; }.state-chip.paid, .status-badge.paid { color: #12613d; background: #dcf8e9; }.state-chip.expired, .status-badge.expired, .state-chip.failed, .state-chip.rejected { color: #a33a36; background: #fee4e2; }.state-chip.idle, .state-chip.preparing { color: var(--color-text-muted, #667085); background: var(--color-bg, #f6f8fb); }
.visual-heading { align-items: end; margin: 42px 0 20px; }.visual-heading h2, .order-card h2 { margin: 5px 0 8px; font-size: clamp(21px, 2.5vw, 28px); letter-spacing: -.035em; }.visual-heading p, .order-card > p, .success-panel p, .expired-panel p, .pre-checkout-state p { max-width: 52ch; margin: 0; color: var(--color-text-muted, #667085); line-height: 1.6; }.visual-meta { padding-bottom: 5px; }
.qr-frame { width: min(410px, 100%); aspect-ratio: 1; margin: 28px auto 25px; padding: 16px; display: grid; place-items: center; border: 1px solid var(--color-border, #dfe4ec); border-radius: 14px; background: #fff; }.qr-frame img { width: 100%; height: 100%; object-fit: contain; }.qr-placeholder { display: grid; place-items: center; gap: 10px; max-width: 250px; color: var(--color-text-muted, #667085); text-align: center; }.qr-placeholder svg { color: var(--color-primary, #3563e9); }.qr-placeholder strong { color: var(--color-text-primary, #172033); }.qr-placeholder span { font-size: 13px; line-height: 1.5; }
.payment-warning { display: flex; align-items: flex-start; gap: 9px; max-width: 58ch; margin: 0 auto; padding: 13px 15px; border-left: 3px solid var(--color-primary, #3563e9); color: var(--color-text-primary, #172033); background: color-mix(in srgb, var(--color-primary, #3563e9) 7%, var(--color-surface, #fff)); font-size: 13px; line-height: 1.55; }.payment-warning svg { flex: 0 0 auto; color: var(--color-primary, #3563e9); }.secondary-note { margin: 18px 0 0; color: var(--color-text-muted, #667085); font-size: 13px; line-height: 1.6; }.support-action { justify-content: flex-start; margin-top: 28px; padding: 0; color: var(--color-primary, #3563e9); font-size: 13px; }.support-action:focus-visible, .back-button:focus-visible, .close-detail:focus-visible { outline: 3px solid color-mix(in srgb, var(--color-primary, #3563e9) 35%, transparent); outline-offset: 3px; }
.pre-checkout-state, .success-panel, .expired-panel, .preparing-panel { min-height: 480px; display: grid; place-items: center; align-content: center; gap: 12px; padding: 34px; text-align: center; }.pre-checkout-state { border: 1px dashed var(--color-border, #dfe4ec); color: var(--color-primary, #3563e9); }.pre-checkout-state strong, .success-panel strong, .expired-panel strong, .preparing-panel strong { color: var(--color-text-primary, #172033); font-size: 20px; }.success-panel, .expired-panel, .preparing-panel { display: flex; align-items: flex-start; justify-content: center; text-align: left; }.success-panel svg { flex: 0 0 auto; color: #12613d; }.expired-panel svg { flex: 0 0 auto; color: #a33a36; }.preparing-panel svg { flex: 0 0 auto; color: var(--color-primary, #3563e9); }.state-panel.error-state { display: flex; align-items: flex-start; gap: 14px; padding: 22px; color: #b42318; }.state-panel.error-state p { margin: 5px 0 0; color: var(--color-text-muted, #667085); }
.order-card-heading { align-items: flex-start; padding-bottom: 20px; border-bottom: 1px solid var(--color-border, #dfe4ec); }.order-card-heading > svg { color: var(--color-primary, #3563e9); }.order-summary { display: grid; gap: 6px; margin: 23px 0 13px; }.order-summary span, .detail-list dt, .countdown-block span, .paid-summary span, .paid-summary small { color: var(--color-text-muted, #667085); font-size: 12px; }.order-summary strong { font-size: 20px; }.detail-list { margin: 0; }.detail-list > div { display: grid; grid-template-columns: .85fr 1.15fr; gap: 18px; padding: 13px 0; border-bottom: 1px solid var(--color-border, #dfe4ec); }.detail-list dd { margin: 0; text-align: right; font-weight: 700; overflow-wrap: anywhere; }.copy-detail dd { display: flex; align-items: center; justify-content: flex-end; gap: 8px; }.copy-detail button, .transfer-content button { display: inline-grid; place-items: center; width: 28px; height: 28px; border: 1px solid var(--color-border, #dfe4ec); border-radius: 7px; background: transparent; color: var(--color-primary, #3563e9); cursor: pointer; }.copy-detail button:hover, .transfer-content button:hover { border-color: var(--color-primary, #3563e9); background: color-mix(in srgb, var(--color-primary, #3563e9) 7%, transparent); }.copy-detail button:disabled { cursor: not-allowed; opacity: .45; }
.transfer-content { margin: 22px 0; padding: 16px; border: 1px solid color-mix(in srgb, var(--color-primary, #3563e9) 26%, var(--color-border, #dfe4ec)); border-radius: 12px; background: color-mix(in srgb, var(--color-primary, #3563e9) 6%, var(--color-surface, #fff)); }.transfer-content > div { display: flex; align-items: center; justify-content: space-between; gap: 12px; color: var(--color-text-muted, #667085); font-size: 12px; }.transfer-content strong { display: block; margin: 12px 0 7px; color: var(--color-primary, #3563e9); font-size: 18px; letter-spacing: .04em; overflow-wrap: anywhere; }.transfer-content small { color: var(--color-text-muted, #667085); }.countdown-block { margin: 20px 0; padding: 15px 0; border-top: 1px solid var(--color-border, #dfe4ec); border-bottom: 1px solid var(--color-border, #dfe4ec); }.countdown-block > div { display: grid; gap: 5px; }.countdown-block > div:last-child { text-align: right; }.countdown-block strong { font-variant-numeric: tabular-nums; }.countdown-block > div:last-child strong { color: var(--color-primary, #3563e9); font-size: 22px; }
.paid-summary { display: grid; gap: 7px; margin: 24px 0; padding: 17px; border-radius: 12px; background: var(--color-bg, #f6f8fb); }.paid-summary strong { font-size: 19px; }.summary-line { display: flex; justify-content: space-between; gap: 20px; margin: 25px 0 18px; padding: 16px 0; border-top: 1px solid var(--color-border, #dfe4ec); border-bottom: 1px solid var(--color-border, #dfe4ec); }.primary-button, .secondary-button { min-height: 44px; border-radius: 9px; padding: 0 17px; font-weight: 750; cursor: pointer; transition: transform .2s ease, background .2s ease, border-color .2s ease, opacity .2s ease; }.primary-button { width: 100%; border: 1px solid var(--color-primary, #3563e9); background: var(--color-primary, #3563e9); color: #fff; }.secondary-button { border: 1px solid var(--color-border, #dfe4ec); background: transparent; color: var(--color-text-primary, #172033); }.primary-button:hover:not(:disabled) { background: color-mix(in srgb, var(--color-primary, #3563e9) 88%, #172033); }.secondary-button:hover:not(:disabled) { border-color: var(--color-primary, #3563e9); color: var(--color-primary, #3563e9); }.primary-button:active, .secondary-button:active, .copy-detail button:active, .transfer-content button:active { transform: translateY(1px) scale(.99); }.primary-button:focus-visible, .secondary-button:focus-visible, .copy-detail button:focus-visible, .transfer-content button:focus-visible { outline: 3px solid color-mix(in srgb, var(--color-primary, #3563e9) 35%, transparent); outline-offset: 2px; }.primary-button:disabled, .secondary-button:disabled { cursor: wait; opacity: .58; }
.current-entitlement { display: grid; grid-template-columns: repeat(3, 1fr); margin-top: 24px; border-top: 1px solid var(--color-border, #dfe4ec); border-bottom: 1px solid var(--color-border, #dfe4ec); }.current-entitlement > div { display: grid; gap: 7px; padding: 18px 20px; border-right: 1px solid var(--color-border, #dfe4ec); }.current-entitlement > div:last-child { border-right: 0; }.current-entitlement span { color: var(--color-text-muted, #667085); font-size: 13px; }.current-entitlement strong { font-variant-numeric: tabular-nums; }
.billing-history, .billing-detail { margin-top: 32px; border: 1px solid var(--color-border, #dfe4ec); border-radius: 16px; background: var(--color-surface, #fff); padding: 24px; }.history-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 18px; }.history-heading span { color: var(--color-text-muted, #667085); font-size: 13px; }.history-heading h2 { margin: 4px 0 0; font-size: 21px; }.history-heading small { color: var(--color-text-muted, #667085); }.history-list { margin-top: 18px; }.history-row { display: grid; grid-template-columns: minmax(0, 1fr) auto auto; align-items: center; gap: 18px; padding: 15px 0; border-top: 1px solid var(--color-border, #dfe4ec); }.history-row small { display: block; margin-top: 5px; color: var(--color-text-muted, #667085); }.history-meta { display: grid; justify-items: end; gap: 5px; }.history-status { padding: 4px 8px; border-radius: 7px; font-size: 11px; font-weight: 750; }.history-status.paid { color: #12613d; background: #dcf8e9; }.history-status.pending { color: #875a00; background: #fff5d6; }.history-status.expired, .history-status.failed, .history-status.rejected { color: #b42318; background: #fee4e2; }.history-actions { display: flex; gap: 8px; }.history-actions button { border: 0; background: transparent; color: var(--color-primary, #3563e9); cursor: pointer; font-weight: 700; }.history-actions button:hover { text-decoration: underline; }.pagination-controls { display: flex; align-items: center; justify-content: center; gap: 14px; margin-top: 18px; padding-top: 16px; border-top: 1px solid var(--color-border, #dfe4ec); color: var(--color-text-muted, #667085); font-size: 13px; }.pagination-controls button { min-height: 34px; border: 1px solid var(--color-border, #dfe4ec); border-radius: 8px; padding: 0 12px; background: var(--color-surface, #fff); color: var(--color-text-primary, #172033); cursor: pointer; font-weight: 700; }.pagination-controls button:disabled { cursor: not-allowed; opacity: .45; }
.billing-detail dl { margin-bottom: 4px; }.billing-detail dl > div { display: flex; justify-content: space-between; gap: 20px; padding: 12px 0; border-bottom: 1px solid var(--color-border, #dfe4ec); }.billing-detail dt { color: var(--color-text-muted, #667085); }.billing-detail dd { margin: 0; font-weight: 700; text-align: right; overflow-wrap: anywhere; }.muted-copy, .receipt-card p, .receipt-card small, .timeline-list small { color: var(--color-text-muted, #667085); line-height: 1.6; }.timeline-list { display: grid; gap: 10px; margin-top: 14px; }.timeline-list > div { display: grid; gap: 4px; padding-left: 14px; border-left: 2px solid var(--color-primary, #3563e9); }.receipt-card { display: grid; gap: 8px; }.receipt-card .secondary-button { justify-self: start; margin-top: 8px; }.close-detail { font-size: 24px; line-height: 1; }
@media (max-width: 900px) { .checkout-shell { padding-top: 32px; }.checkout-intro { grid-template-columns: 1fr; gap: 18px; }.price-block { min-width: 0; text-align: left; }.payment-stage { grid-template-columns: 1fr; }.payment-visual { min-height: auto; }.order-card { box-shadow: none; } }
@media (max-width: 640px) { .checkout-nav { height: 62px; padding: 0 16px; }.brand { font-size: 16px; }.back-button { font-size: 12px; }.checkout-shell { width: min(100% - 24px, 1180px); padding: 26px 0 56px; }.checkout-intro h1 { font-size: 36px; }.payment-visual, .order-card, .billing-history, .billing-detail { border-radius: 14px; padding: 19px; }.visual-heading { align-items: start; flex-direction: column; gap: 8px; margin-top: 34px; }.visual-meta { padding-bottom: 0; }.qr-frame { margin-top: 20px; }.detail-list > div { grid-template-columns: 1fr; gap: 6px; }.detail-list dd { text-align: left; }.copy-detail dd { justify-content: flex-start; }.countdown-block { align-items: flex-start; flex-direction: column; }.countdown-block > div:last-child { text-align: left; }.current-entitlement { grid-template-columns: 1fr; }.current-entitlement > div { border-right: 0; border-bottom: 1px solid var(--color-border, #dfe4ec); }.current-entitlement > div:last-child { border-bottom: 0; }.history-heading, .history-row { display: grid; grid-template-columns: 1fr auto; }.history-meta { justify-items: end; }.history-actions { grid-column: 1 / -1; }.success-panel, .expired-panel, .preparing-panel { min-height: 300px; padding: 24px; }.pre-checkout-state { min-height: 300px; } }
@media (prefers-reduced-motion: reduce) { .primary-button, .secondary-button, .copy-detail button, .transfer-content button { transition: none; } }
.success-actions { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 8px; }
</style>
