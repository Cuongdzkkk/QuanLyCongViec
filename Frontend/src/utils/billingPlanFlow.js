export const normalizeBillingPlanCode = value => `${value || ''}`.trim().toLowerCase()

const BILLING_RETURN_FALLBACK = '/dashboard'
const BILLING_RETURN_ORIGIN = 'https://sprinta.internal'

export const resolveBillingReturnTo = (value, fallback = BILLING_RETURN_FALLBACK) => {
  if (typeof value !== 'string' || !value.trim()) return fallback

  const candidate = value.trim()
  if (!candidate.startsWith('/') || /^(?:https?:|javascript:|data:)/i.test(candidate) || candidate.startsWith('//') || candidate.startsWith('\\\\')) {
    return fallback
  }

  try {
    const parsed = new URL(candidate, BILLING_RETURN_ORIGIN)
    if (parsed.origin !== BILLING_RETURN_ORIGIN || !parsed.pathname.startsWith('/')) return fallback
    return `${parsed.pathname}${parsed.search}${parsed.hash}`
  } catch {
    return fallback
  }
}

export const resolveBillingPlanFlow = (plan, currentPlanCode = '') => {
  const code = normalizeBillingPlanCode(plan?.code || plan?.id)
  if (!code || code === normalizeBillingPlanCode(currentPlanCode)) return 'current'
  if (code === 'enterprise' || plan?.monthlyPriceVnd == null) return 'enterprise'
  return Number(plan.monthlyPriceVnd) === 0 ? 'free' : 'paid'
}

export const buildBillingCheckoutLocation = (planCode, orderId = '', returnTo = '') => {
  const query = {
    ...(orderId ? { orderId } : {}),
    ...(returnTo ? { returnTo: resolveBillingReturnTo(returnTo) } : {})
  }

  return {
    name: 'BillingCheckout',
    params: { planCode: normalizeBillingPlanCode(planCode) },
    ...(Object.keys(query).length ? { query } : {})
  }
}
