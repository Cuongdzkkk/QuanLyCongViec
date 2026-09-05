<template>
  <AppModal
    :model-value="modelValue"
    title="AI Credits & gói SprintA"
    subtitle="Theo dõi số dư, quyền lợi và lịch sử thanh toán trong một nơi."
    icon="bi bi-stars"
    size="large"
    width="960px"
    body-class-name="ai-credits-modal-body"
    :show-footer="false"
    :close-on-overlay-click="true"
    @update:model-value="$emit('update:modelValue', $event)"
  >
    <div class="ai-credits-modal" aria-label="Quản lý AI Credits">
      <div v-if="loading" class="credits-state" role="status">Đang tải thông tin credits và gói dịch vụ...</div>
      <div v-else-if="error" class="credits-state is-error" role="alert">
        <strong>Không thể tải thông tin billing</strong>
        <span>{{ error }}</span>
        <button type="button" class="secondary-action" @click="loadData">Thử lại</button>
      </div>
      <template v-else>
        <section v-if="billing" class="wallet-summary" aria-labelledby="ai-wallet-title">
          <div>
            <span class="section-kicker">SỐ DƯ AI CREDITS</span>
            <h2 id="ai-wallet-title">{{ billing.planName || 'Gói hiện tại' }}</h2>
            <p>Ví credits cập nhật theo tài khoản và kỳ sử dụng hiện tại.</p>
          </div>
          <div class="wallet-balance">
            <strong>{{ formatCredits(billing.totalRemainingCredits ?? billing.remainingCredits) }}</strong>
            <span>Còn lại</span>
          </div>
          <div class="wallet-progress-block">
            <div class="wallet-progress-label">
              <span>Đã dùng {{ formatCredits(billing.usedCredits) }} / {{ formatCredits(creditAllocation) }}</span>
              <strong>{{ usagePercent }}%</strong>
            </div>
            <div class="wallet-progress" role="progressbar" :aria-valuenow="usagePercent" aria-valuemin="0" aria-valuemax="100" aria-label="Tỷ lệ AI credits đã dùng">
              <span :style="{ width: `${usagePercent}%` }"></span>
            </div>
            <small v-if="billing.currentPeriodEnd">Kỳ hiện tại kết thúc {{ formatDate(billing.currentPeriodEnd) }}</small>
          </div>
        </section>

        <section class="plans-section" aria-labelledby="ai-plans-title">
          <div class="section-heading">
            <div>
              <span class="section-kicker">GÓI DỊCH VỤ</span>
              <h2 id="ai-plans-title">Chọn gói phù hợp</h2>
            </div>
            <button type="button" class="text-action" @click="openBilling">Mở trang gói &amp; thanh toán</button>
          </div>
          <div class="plan-grid">
            <article v-for="plan in plans" :key="getPlanCode(plan)" class="plan-card" :class="{ 'is-current': isCurrentPlan(plan), 'is-recommended': plan.isRecommended, 'is-enterprise': isEnterprisePlan(plan) }">
              <div class="plan-card-head">
                <div>
                  <span class="plan-code">{{ getPlanCode(plan) }}</span>
                  <h3>{{ plan.name }}</h3>
                </div>
                <span v-if="isCurrentPlan(plan)" class="current-badge">Đang dùng</span>
                <span v-else-if="plan.isRecommended" class="recommended-badge">Đề xuất</span>
              </div>
              <strong class="plan-price">{{ planPriceLabel(plan) }}</strong>
              <span class="plan-credit">{{ planCreditLabel(plan) }}</span>
              <ul v-if="plan.features?.length" class="plan-features">
                <li v-for="feature in plan.features" :key="feature">{{ feature }}</li>
              </ul>
              <p v-else class="plan-features-empty">Quyền lợi theo cấu hình hiện tại của hệ thống.</p>
              <button
                type="button"
                class="plan-action"
                :class="{ primary: !isCurrentPlan(plan) && !isEnterprisePlan(plan), 'enterprise-action': isEnterprisePlan(plan) }"
                :aria-label="`${plan.name}: ${planActionLabel(plan)}`"
                :data-plan-code="getPlanCode(plan)"
                :disabled="isCurrentPlan(plan) || checkoutLoadingCode === getPlanCode(plan) || checkoutLoadingCode === 'enterprise'"
                @click.stop="selectPlan(plan)"
              >
                <span v-if="checkoutLoadingCode === getPlanCode(plan)">Đang chuẩn bị...</span>
                <span v-else>{{ planActionLabel(plan) }}</span>
              </button>
              <small v-if="isEnterprisePlan(plan)" class="plan-note">Trao đổi để xác định quy mô và mức credits phù hợp.</small>
            </article>
          </div>
          <p class="pricing-disclaimer">{{ pricingDisclaimer }}</p>
        </section>

        <section class="history-section" aria-labelledby="ai-history-title">
          <div class="section-heading">
            <div>
              <span class="section-kicker">LỊCH SỬ THANH TOÁN</span>
              <h2 id="ai-history-title">Lịch sử thanh toán</h2>
            </div>
            <span class="history-count">{{ historyTotal }} giao dịch</span>
          </div>
          <div v-if="history.length" class="history-list">
            <div v-for="order in history" :key="order.id" class="history-row">
              <div>
                <strong>{{ order.planName || order.planCode || 'Gói SprintA' }}</strong>
                <small>{{ formatDate(order.createdAt || order.createdOn) }}</small>
              </div>
              <span class="history-status" :class="String(order.status || '').toLowerCase()">{{ order.status || '—' }}</span>
              <strong>{{ priceLabel(order.amountVnd) }}</strong>
            </div>
          </div>
          <p v-else class="history-empty">Chưa có giao dịch thanh toán trong tài khoản.</p>
          <button type="button" class="secondary-action history-action" @click="openBilling">Mở trang thanh toán &amp; lịch sử</button>
        </section>
      </template>
    </div>
  </AppModal>
</template>

<script setup>
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppModal from '@/components/common/Foundation/AppModal.vue'
import axiosClient from '@/api/axiosClient'
import { billingApi, unwrapBillingData } from '@/api/billingApi'
import { buildBillingCheckoutLocation, resolveBillingPlanFlow } from '@/utils/billingPlanFlow'

const props = defineProps({
  modelValue: { type: Boolean, required: true },
  contactContext: { type: Object, default: () => ({}) }
})
const emit = defineEmits(['update:modelValue'])
const route = useRoute()
const router = useRouter()
const loading = ref(false)
const error = ref('')
const billing = ref(null)
const plans = ref([])
const history = ref([])
const historyTotal = ref(0)
const defaultPricingDisclaimer = 'Giá gói được cập nhật trực tiếp từ hệ thống. Gói Enterprise được báo giá theo nhu cầu.'
const pricingDisclaimer = ref(defaultPricingDisclaimer)
const checkoutLoadingCode = ref('')

const currentPlanCode = computed(() => String(billing.value?.planCode || '').toLowerCase())
const creditAllocation = computed(() => Math.max(0, Number(billing.value?.includedCredits || 0) + Number(billing.value?.adjustmentCredits || 0)))
const usagePercent = computed(() => {
  if (!creditAllocation.value) return 0
  return Math.min(100, Math.max(0, Math.round((Number(billing.value?.usedCredits || 0) / creditAllocation.value) * 100)))
})

const payload = response => response?.data?.data ?? response?.data ?? response
const formatCredits = value => Number(value || 0).toLocaleString('vi-VN')
const priceLabel = value => value == null ? 'Liên hệ' : Number(value) === 0 ? 'Miễn phí' : `${Number(value).toLocaleString('vi-VN')} đ/tháng`
const formatDate = value => value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium' }).format(new Date(value)) : '—'
const getPlanCode = plan => String(plan?.code || plan?.id || '').trim().toLowerCase()
const isCurrentPlan = plan => getPlanCode(plan) === currentPlanCode.value
const isEnterprisePlan = plan => getPlanCode(plan) === 'enterprise' || plan?.monthlyPriceVnd == null
const planPriceLabel = plan => isEnterprisePlan(plan) ? 'Giá theo nhu cầu' : priceLabel(plan.monthlyPriceVnd)
const planCreditLabel = plan => isEnterprisePlan(plan) ? 'Credits theo thỏa thuận' : `${formatCredits(plan.includedAiCredits)} AI credits / tháng`
const planActionLabel = plan => ({ current: 'Gói hiện tại', enterprise: 'Liên hệ tư vấn →', free: 'Kích hoạt Free', paid: 'Mở thanh toán' }[resolveBillingPlanFlow(plan, currentPlanCode.value)] || 'Mở thanh toán')
const isInternalPricingCopy = value => /mvp|database|extra credits?.*(?:price|pricing|giá)|undecided|chưa có mức giá mua lẻ/i.test(String(value || ''))

const loadData = async () => {
  loading.value = true
  error.value = ''
  try {
    const [pricingResponse, billingResponse, historyResponse] = await Promise.all([
      axiosClient.get('/public/pricing'),
      billingApi.getMe(),
      billingApi.getMyHistory({ page: 1, pageSize: 5 })
    ])
    const pricing = payload(pricingResponse) || {}
    const billingData = unwrapBillingData(billingResponse) || payload(billingResponse) || {}
    const historyData = unwrapBillingData(historyResponse) || payload(historyResponse) || {}
    plans.value = Array.isArray(pricing.plans) ? pricing.plans : []
    billing.value = billingData
    history.value = Array.isArray(historyData.items) ? historyData.items : []
    historyTotal.value = Number(historyData.totalCount || history.value.length)
    pricingDisclaimer.value = isInternalPricingCopy(pricing.disclaimer) ? defaultPricingDisclaimer : (pricing.disclaimer || defaultPricingDisclaimer)
  } catch (loadError) {
    error.value = loadError?.response?.data?.message || 'Vui lòng thử lại sau ít phút.'
  } finally {
    loading.value = false
  }
}

const close = () => emit('update:modelValue', false)
const openEnterpriseContact = () => {
  close()
  const context = props.contactContext || {}
  router.push({
    path: '/',
    query: {
      contact: 'enterprise',
      source: 'ai-credits',
      plan: 'enterprise',
      ...(context.workspaceId ? { workspaceId: context.workspaceId } : {}),
      ...(context.workspaceName ? { workspaceName: context.workspaceName } : {}),
      ...(context.projectName ? { projectName: context.projectName } : {}),
      returnTo: route.fullPath
    }
  })
}
const openBilling = () => {
  close()
  router.push(buildBillingCheckoutLocation(
    currentPlanCode.value || getPlanCode(plans.value[0]) || 'free',
    '',
    route.fullPath
  ))
}

const selectPlan = async plan => {
  const flow = resolveBillingPlanFlow(plan, currentPlanCode.value)
  if (flow === 'current') return
  if (flow === 'enterprise') {
    openEnterpriseContact()
    return
  }
  checkoutLoadingCode.value = getPlanCode(plan)
  try {
    if (flow === 'free') {
      await billingApi.activateFree()
      await loadData()
      return
    }
    close()
    await router.push(buildBillingCheckoutLocation(getPlanCode(plan), '', route.fullPath))
  } catch (selectError) {
    error.value = selectError?.response?.data?.message || 'Không thể chuẩn bị luồng billing.'
  } finally {
    checkoutLoadingCode.value = ''
  }
}

watch(() => props.modelValue, value => {
  if (value) loadData()
})
</script>

<style scoped>
:deep(.sprinta-app-modal) {
  max-width: calc(100vw - 32px);
  background: var(--color-surface);
  color: var(--color-text-primary);
}

:deep(.sprinta-app-modal .el-dialog__header) {
  margin: 0;
  border-bottom-color: var(--color-border);
  color: var(--color-text-primary);
}

:deep(.sprinta-app-modal .el-dialog__body) {
  background: var(--color-surface);
  color: var(--color-text-primary);
}

.ai-credits-modal { display: grid; gap: 24px; }
.credits-state { display: grid; gap: 10px; padding: 24px; border: 1px solid var(--color-border); border-radius: 14px; color: var(--color-text-secondary); text-align: center; }
.credits-state.is-error { color: var(--color-danger); }
.wallet-summary { display: grid; grid-template-columns: minmax(0, 1.2fr) minmax(130px, .6fr) minmax(210px, 1fr); gap: 20px; align-items: center; padding: 18px; border: 1px solid var(--color-border); border-radius: 16px; background: var(--color-surface-hover); }
.section-kicker, .plan-code { color: var(--color-accent); font-size: 10px; font-weight: 850; letter-spacing: .08em; text-transform: uppercase; }
h2, h3, p { margin: 0; }
.wallet-summary h2, .section-heading h2 { margin-top: 5px; font-size: 19px; }
.wallet-summary p, .plan-features-empty, .pricing-disclaimer, .history-empty { margin-top: 6px; color: var(--color-text-secondary); font-size: 12px; line-height: 1.55; }
.wallet-balance { display: grid; gap: 4px; text-align: right; }
.wallet-balance strong { font-size: 28px; letter-spacing: -.04em; }
.wallet-balance span, .wallet-progress-block small, .history-row small, .history-count { color: var(--color-text-muted); font-size: 11px; }
.wallet-progress-label { display: flex; justify-content: space-between; gap: 8px; color: var(--color-text-secondary); font-size: 11px; }
.wallet-progress { height: 8px; margin: 8px 0 6px; overflow: hidden; border-radius: 999px; background: var(--color-border); }
.wallet-progress span { display: block; height: 100%; border-radius: inherit; background: var(--color-accent); transition: width .2s ease; }
.section-heading { display: flex; justify-content: space-between; gap: 12px; align-items: end; }
.text-action, .secondary-action { min-height: 34px; padding: 7px 11px; border: 1px solid var(--color-border); border-radius: 9px; background: transparent; color: var(--color-text-primary); cursor: pointer; }
.text-action:hover, .text-action:focus-visible, .secondary-action:hover, .secondary-action:focus-visible { border-color: var(--color-accent); color: var(--color-accent); outline: none; }
.plan-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 12px; margin-top: 14px; }
.plan-card { display: flex; min-width: 0; flex-direction: column; gap: 10px; padding: 16px; border: 1px solid var(--color-border); border-radius: 14px; background: var(--color-bg); }
.plan-card.is-current { border-color: var(--color-accent); box-shadow: 0 0 0 1px color-mix(in srgb, var(--color-accent) 18%, transparent); }
.plan-card-head { display: flex; justify-content: space-between; gap: 8px; align-items: start; }
.plan-card h3 { margin-top: 4px; font-size: 17px; }
.current-badge, .recommended-badge { flex: 0 0 auto; padding: 4px 7px; border-radius: 999px; background: var(--sa-primary-soft); color: var(--color-accent); font-size: 10px; font-weight: 800; }
.recommended-badge { background: var(--color-warning-bg); color: var(--color-warning); }
.plan-price { font-size: 20px; }
.plan-credit { color: var(--color-text-secondary); font-size: 12px; }
.plan-features { display: grid; gap: 6px; min-height: 66px; margin: 0; padding-left: 17px; color: var(--color-text-secondary); font-size: 12px; line-height: 1.4; }
.plan-features-empty { min-height: 66px; margin: 0; }
.plan-action { min-height: 38px; margin-top: auto; border: 1px solid var(--color-border); border-radius: 9px; background: transparent; color: var(--color-text-primary); font-weight: 750; cursor: pointer; }
.plan-action.primary { border-color: var(--color-accent); background: var(--color-accent); color: var(--color-text-inverse); }
.plan-action:disabled { cursor: not-allowed; opacity: .65; }
.plan-note { color: var(--color-text-muted); font-size: 10px; line-height: 1.4; }
.pricing-disclaimer { padding: 10px 12px; border-left: 3px solid var(--color-warning); background: var(--color-warning-bg); }
.history-list { display: grid; gap: 8px; margin-top: 14px; }
.history-row { display: grid; grid-template-columns: minmax(0, 1fr) auto auto; gap: 12px; align-items: center; padding: 11px 12px; border: 1px solid var(--color-border); border-radius: 10px; background: var(--color-bg); }
.history-row > div { display: grid; gap: 3px; min-width: 0; }
.history-row strong { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 12px; }
.history-status { padding: 4px 7px; border-radius: 999px; background: var(--color-surface-hover); color: var(--color-text-secondary); font-size: 10px; font-weight: 750; }
.history-status.paid, .history-status.completed { color: var(--color-success); }
.history-status.failed, .history-status.rejected { color: var(--color-danger); }
.history-action { margin-top: 12px; }

@media (max-width: 760px) {
  :deep(.sprinta-app-modal .el-dialog__body) { padding: 16px; }
  .wallet-summary, .plan-grid { grid-template-columns: 1fr; }
  .wallet-balance { text-align: left; }
}

@media (max-width: 480px) {
  :deep(.sprinta-app-modal) { max-width: calc(100vw - 16px); }
  .section-heading { align-items: start; flex-direction: column; }
  .history-row { grid-template-columns: minmax(0, 1fr) auto; }
  .history-row > strong { grid-column: 2; grid-row: 1; }
}

/* A dark-first product surface that still follows the active semantic theme. */
:deep(.sprinta-app-modal) {
  overflow: hidden;
  border: 1px solid color-mix(in srgb, var(--color-accent) 20%, var(--color-border));
  border-radius: 20px;
  background: var(--color-surface);
  box-shadow: 0 28px 90px color-mix(in srgb, var(--color-text-primary) 28%, transparent);
}

:deep(.sprinta-app-modal .el-dialog__header) {
  padding: 22px 24px 18px;
  background: linear-gradient(135deg, color-mix(in srgb, var(--color-accent) 14%, var(--color-surface)), var(--color-surface) 62%);
}

:deep(.sprinta-app-modal .el-dialog__title) { font-size: 20px; letter-spacing: -.025em; }
:deep(.sprinta-app-modal .el-dialog__headerbtn) { border-radius: 9px; }
:deep(.sprinta-app-modal .el-dialog__headerbtn:hover) { background: var(--sa-primary-soft); }
:deep(.sprinta-app-modal .el-dialog__body) { padding: 22px 24px 26px; }

.ai-credits-modal { gap: 28px; }
.credits-state { background: color-mix(in srgb, var(--color-surface-hover) 80%, var(--color-surface)); }
.wallet-summary {
  position: relative;
  overflow: hidden;
  border-color: color-mix(in srgb, var(--color-accent) 30%, var(--color-border));
  background:
    radial-gradient(circle at 100% 0, color-mix(in srgb, var(--color-accent) 17%, transparent), transparent 38%),
    color-mix(in srgb, var(--color-accent) 7%, var(--color-surface));
  box-shadow: 0 12px 28px color-mix(in srgb, var(--color-accent) 9%, transparent);
}
.wallet-summary::after {
  position: absolute;
  right: 20px;
  bottom: -54px;
  width: 130px;
  height: 130px;
  border: 1px solid color-mix(in srgb, var(--color-accent) 20%, transparent);
  border-radius: 50%;
  content: '';
  pointer-events: none;
}
.wallet-balance strong { color: var(--color-text-primary); font-size: 32px; }
.wallet-progress { background: color-mix(in srgb, var(--color-border) 75%, var(--color-bg)); }
.wallet-progress span { background: linear-gradient(90deg, var(--color-accent), var(--sa-primary)); }
.section-heading { align-items: center; }
.section-kicker { font-size: 9px; letter-spacing: .13em; }
.section-heading h2 { font-size: 21px; letter-spacing: -.025em; }
.text-action, .secondary-action { background: color-mix(in srgb, var(--color-surface-hover) 70%, transparent); }
.text-action:hover, .text-action:focus-visible, .secondary-action:hover, .secondary-action:focus-visible { background: var(--sa-primary-soft); }

.plan-card {
  position: relative;
  border-color: color-mix(in srgb, var(--color-border) 90%, var(--color-accent));
  background: color-mix(in srgb, var(--color-surface-hover) 48%, var(--color-surface));
  box-shadow: 0 8px 20px color-mix(in srgb, var(--color-text-primary) 5%, transparent);
  transition: border-color 160ms ease, box-shadow 160ms ease, transform 160ms ease;
}
.plan-card:hover { transform: translateY(-2px); border-color: color-mix(in srgb, var(--color-accent) 48%, var(--color-border)); }
.plan-card.is-current { background: color-mix(in srgb, var(--color-accent) 8%, var(--color-surface)); }
.plan-card.is-recommended:not(.is-current) { border-color: color-mix(in srgb, var(--color-accent) 52%, var(--color-border)); box-shadow: 0 0 0 1px color-mix(in srgb, var(--color-accent) 14%, transparent), 0 12px 28px color-mix(in srgb, var(--color-accent) 10%, transparent); }
.plan-price { color: var(--color-text-primary); font-size: 23px; letter-spacing: -.035em; }
.plan-action { background: color-mix(in srgb, var(--color-surface) 85%, var(--color-accent)); transition: border-color 160ms ease, background 160ms ease, color 160ms ease, transform 160ms ease; }
.plan-action:not(:disabled):hover { border-color: var(--color-accent); background: var(--sa-primary-soft); color: var(--color-accent); transform: translateY(-1px); }
.plan-action.primary:not(:disabled):hover { background: var(--sa-primary); color: var(--color-text-inverse); filter: brightness(1.06); }
.pricing-disclaimer { border-radius: 0 10px 10px 0; background: color-mix(in srgb, var(--color-warning) 9%, var(--color-surface)); }
.history-row { background: color-mix(in srgb, var(--color-surface-hover) 45%, var(--color-surface)); transition: border-color 160ms ease, background 160ms ease; }
.history-row:hover { border-color: color-mix(in srgb, var(--color-accent) 32%, var(--color-border)); background: color-mix(in srgb, var(--color-accent) 6%, var(--color-surface)); }
.history-status { background: color-mix(in srgb, var(--color-surface-hover) 85%, var(--color-bg)); }

@media (max-width: 760px) {
  :deep(.sprinta-app-modal .el-dialog__header) { padding: 18px 16px 15px; }
  :deep(.sprinta-app-modal .el-dialog__body) { padding: 16px; }
  .wallet-summary { gap: 15px; }
}

/* Credit surface: keep the wallet and plan decisions distinct from the
   account history without changing the billing data or checkout contracts. */
:deep(.sprinta-app-modal) {
  display: flex;
  max-height: calc(100dvh - 32px);
  flex-direction: column;
}

:deep(.sprinta-app-modal .el-dialog__body) {
  min-height: 0;
  overflow-y: auto;
  overscroll-behavior: contain;
}

.wallet-summary {
  grid-template-columns: minmax(0, 1.1fr) minmax(150px, .55fr) minmax(220px, 1fr);
  padding: 20px;
}

.wallet-balance strong,
.plan-price {
  font-variant-numeric: tabular-nums;
}

.plan-card {
  min-height: 292px;
}

.plan-card.is-enterprise {
  border-color: color-mix(in srgb, var(--color-accent) 34%, var(--color-border));
  background: color-mix(in srgb, var(--color-accent) 5%, var(--color-surface));
}

.plan-card.is-enterprise .plan-price {
  color: var(--color-accent);
  font-size: 19px;
}

.plan-features,
.plan-features-empty {
  flex: 1 1 auto;
}

.enterprise-action {
  border-color: color-mix(in srgb, var(--color-accent) 55%, var(--color-border));
  color: var(--color-accent);
}

.enterprise-action:not(:disabled):hover {
  background: var(--sa-primary-soft);
  color: var(--color-accent);
}

.history-section {
  margin-top: 6px;
  padding-top: 24px;
  border-top: 1px solid color-mix(in srgb, var(--color-accent) 20%, var(--color-border));
}

@media (max-width: 900px) {
  .wallet-summary { grid-template-columns: minmax(0, 1fr) minmax(150px, .6fr); }
  .wallet-progress-block { grid-column: 1 / -1; }
}

@media (max-width: 760px) {
  :deep(.sprinta-app-modal) { max-height: calc(100dvh - 16px); }
  .wallet-summary,
  .plan-grid { grid-template-columns: 1fr; }
  .wallet-progress-block { grid-column: auto; }
  .plan-card { min-height: 0; }
}
</style>
