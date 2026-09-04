export const normalizeBillingPlanCode = value => `${value || ''}`.trim().toLowerCase()

export const resolveBillingPlanFlow = (plan, currentPlanCode = '') => {
  const code = normalizeBillingPlanCode(plan?.code || plan?.id)
  if (!code || code === normalizeBillingPlanCode(currentPlanCode)) return 'current'
  if (code === 'enterprise' || plan?.monthlyPriceVnd == null) return 'enterprise'
  return Number(plan.monthlyPriceVnd) === 0 ? 'free' : 'paid'
}

export const buildBillingCheckoutLocation = (planCode, orderId = '') => ({
  name: 'BillingCheckout',
  params: { planCode: normalizeBillingPlanCode(planCode) },
  ...(orderId ? { query: { orderId } } : {})
})
