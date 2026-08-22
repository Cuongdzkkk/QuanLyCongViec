import axiosClient from './axiosClient'

export const billingApi = {
  getMe: () => axiosClient.get('/billing/me'),
  getMyOrders: () => axiosClient.get('/billing/orders'),
  getMyHistory: (params = {}) => axiosClient.get('/billing/orders/history', { params }),
  getOrderDetails: (id) => axiosClient.get(`/billing/orders/${id}`),
  getReceipt: (id) => axiosClient.get(`/billing/orders/${id}/receipt`),
  resendReceipt: (id) => axiosClient.post(`/billing/orders/${id}/receipt/resend`),
  createOrder: (planCode) => axiosClient.post('/billing/orders', { planCode }),
  activateFree: () => axiosClient.post('/billing/free/activate'),
  getUsers: () => axiosClient.get('/admin/billing/users'),
  changePlan: (userId, payload) => axiosClient.put(`/admin/billing/users/${userId}/plan`, payload),
  activateSubscription: (userId, payload) => axiosClient.post(`/admin/billing/users/${userId}/activate`, payload),
  extendSubscription: (userId, reason) => axiosClient.post(`/admin/billing/users/${userId}/extend`, { reason }),
  cancelSubscription: (userId, reason) => axiosClient.post(`/admin/billing/users/${userId}/cancel`, { reason }),
  adjustCredits: (userId, payload) => axiosClient.post(`/admin/billing/users/${userId}/credit-adjustments`, payload),
  resetUsage: (userId, reason) => axiosClient.post(`/admin/billing/users/${userId}/reset-current-period-usage`, { reason }),
  getOrders: (status) => axiosClient.get('/admin/billing/orders', { params: status ? { status } : {} }),
  searchOrders: (params = {}) => axiosClient.get('/admin/billing/orders/search', { params }),
  getAdminOrderDetails: (id) => axiosClient.get(`/admin/billing/orders/${id}`),
  getAdminReceipt: (id) => axiosClient.get(`/admin/billing/orders/${id}/receipt`),
  resendAdminReceipt: (id) => axiosClient.post(`/admin/billing/orders/${id}/receipt/resend`),
  approveOrder: (id, reason) => axiosClient.post(`/admin/billing/orders/${id}/approve`, { reason }),
  rejectOrder: (id, reason) => axiosClient.post(`/admin/billing/orders/${id}/reject`, { reason }),
  getPlans: () => axiosClient.get('/admin/billing/plans'),
  updatePlan: (code, payload) => axiosClient.put(`/admin/billing/plans/${code}`, payload)
}

export const unwrapBillingData = (response) => response?.data?.data
