<template>
  <AppModal
    :model-value="modelValue"
    title="AI Credits & gói dịch vụ"
    subtitle="Thông tin quyền lợi, lịch sử thanh toán và các gói đang được SprintA cung cấp."
    icon="fa-solid fa-sparkles"
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
            <span class="section-kicker">AI CREDITS</span>
            <h2 id="ai-wallet-title">{{ billing.planName || 'Gói hiện tại' }}</h2>
            <p>Ví credits cập nhật theo tài khoản và kỳ sử dụng hiện tại.</p>
          </div>
          <div class="wallet-balance">
            <strong>{{ formatCredits(billing.totalRemainingCredits ?? billing.remainingCredits) }}</strong>
            <span>credits còn lại</span>
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
              <span class="section-kicker">PLANS</span>
              <h2 id="ai-plans-title">Chọn gói phù hợp</h2>
            </div>
            <button type="button" class="text-action" @click="openBilling">Mở billing</button>
          </div>
          <div class="plan-grid">
            <article v-for="plan in plans" :key="plan.code" class="plan-card" :class="{ 'is-current': isCurrentPlan(plan) }">
              <div class="plan-card-head">
                <div>
                  <span class="plan-code">{{ plan.code }}</span>
                  <h3>{{ plan.name }}</h3>
                </div>
                <span v-if="isCurrentPlan(plan)" class="current-badge">Đang dùng</span>
                <span v-else-if="plan.isRecommended" class="recommended-badge">Đề xuất</span>
              </div>
              <strong class="plan-price">{{ priceLabel(plan.monthlyPriceVnd) }}</strong>
              <span class="plan-credit">{{ formatCredits(plan.includedAiCredits) }} AI credits / tháng</span>
              <ul v-if="plan.features?.length" class="plan-features">
                <li v-for="feature in plan.features" :key="feature">{{ feature }}</li>
              </ul>
              <p v-else class="plan-features-empty">Quyền lợi theo cấu hình hiện tại của hệ thống.</p>
              <button
                type="button"
                class="plan-action"
                :class="{ primary: !isCurrentPlan(plan) && !isEnterprisePlan(plan) }"
                :disabled="isCurrentPlan(plan) || isEnterprisePlan(plan) || checkoutLoadingCode === plan.code"
                @click="selectPlan(plan)"
              >
                <span v-if="checkoutLoadingCode === plan.code">Đang chuẩn bị...</span>
                <span v-else-if="isCurrentPlan(plan)">Gói hiện tại</span>
                <span v-else-if="isEnterprisePlan(plan)">Liên hệ sales</span>
                <span v-else-if="Number(plan.monthlyPriceVnd) === 0">Kích hoạt Free</span>
                <span v-else>Nâng cấp qua billing</span>
              </button>
              <small v-if="isEnterprisePlan(plan)" class="plan-note">Gói này chưa có thanh toán online trong contract hiện tại.</small>
            </article>
          </div>
          <p class="pricing-disclaimer">{{ pricingDisclaimer }}</p>
        </section>

        <section class="history-section" aria-labelledby="ai-history-title">
          <div class="section-heading">
            <div>
              <span class="section-kicker">BILLING HISTORY</span>
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
          <button type="button" class="secondary-action history-action" @click="openBilling">Xem đầy đủ trong billing</button>
        </section>
      </template>
    </div>
  </AppModal>
</template>

<script setup>
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import AppModal from '@/components/common/Foundation/AppModal.vue'
import axiosClient from '@/api/axiosClient'
import { billingApi, unwrapBillingData } from '@/api/billingApi'

const props = defineProps({
  modelValue: { type: Boolean, required: true }
})
const emit = defineEmits(['update:modelValue'])
const router = useRouter()
const loading = ref(false)
const error = ref('')
const billing = ref(null)
const plans = ref([])
const history = ref([])
const historyTotal = ref(0)
const pricingDisclaimer = ref('Extra credits chưa có mức giá mua lẻ được công bố. Các nâng cấp sử dụng luồng billing thật của SprintA.')
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
const isCurrentPlan = plan => String(plan?.code || '').toLowerCase() === currentPlanCode.value
const isEnterprisePlan = plan => String(plan?.code || '').toLowerCase() === 'enterprise' || plan?.monthlyPriceVnd == null

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
    pricingDisclaimer.value = pricing.disclaimer || pricingDisclaimer.value
  } catch (loadError) {
    error.value = loadError?.response?.data?.message || 'Vui lòng thử lại sau ít phút.'
  } finally {
    loading.value = false
  }
}

const close = () => emit('update:modelValue', false)
const openBilling = () => {
  close()
  router.push({ name: 'BillingCheckout', params: { planCode: currentPlanCode.value || plans.value[0]?.code || 'free' } })
}

const selectPlan = async plan => {
  if (!plan?.code || isCurrentPlan(plan) || isEnterprisePlan(plan)) return
  checkoutLoadingCode.value = plan.code
  try {
    if (Number(plan.monthlyPriceVnd) === 0) {
      await billingApi.activateFree()
      await loadData()
      return
    }
    const order = unwrapBillingData(await billingApi.createOrder(plan.code)) || {}
    const orderId = order.id || order.orderId
    close()
    await router.push({
      name: 'BillingCheckout',
      params: { planCode: plan.code },
      ...(orderId ? { query: { orderId } } : {})
    })
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
</style>
