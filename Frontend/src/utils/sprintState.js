export const SPRINT_STATE = Object.freeze({
  UPCOMING: 'Upcoming',
  ACTIVE: 'Active',
  COMPLETED: 'Completed'
})

const STATE_META = Object.freeze({
  [SPRINT_STATE.UPCOMING]: {
    label: 'Sắp tới',
    badge: 'upcoming',
    canStart: true,
    canClose: false
  },
  [SPRINT_STATE.ACTIVE]: {
    label: 'Đang hoạt động',
    badge: 'active',
    canStart: false,
    canClose: true
  },
  [SPRINT_STATE.COMPLETED]: {
    label: 'Đã hoàn tất',
    badge: 'completed',
    canStart: false,
    canClose: false
  }
})

export const normalizeSprintState = (state) => {
  if (state === 'Planned') return SPRINT_STATE.UPCOMING
  return Object.values(SPRINT_STATE).includes(state) ? state : SPRINT_STATE.UPCOMING
}

export const getSprintStateMeta = (state) =>
  STATE_META[normalizeSprintState(state)]

export const getSprintApiError = (error) => {
  const status = error?.response?.status || 0
  const payload = error?.response?.data || {}
  return {
    status,
    code: payload.code || null,
    message: payload.message || payload.detail || payload.title || error?.message || 'Không thể cập nhật Cycle.'
  }
}

const ERROR_MESSAGES = Object.freeze({
  CYCLE_ALREADY_COMPLETED: 'Sprint này đã hoàn tất và không thể bắt đầu lại.',
  CYCLE_NOT_ACTIVE: 'Chỉ Sprint đang hoạt động mới có thể được đóng.',
  CYCLE_DATES_INVALID: 'Khoảng thời gian của Sprint không hợp lệ.',
  ACTIVE_CYCLE_EXISTS: 'Dự án đã có một Sprint đang hoạt động. Hãy hoàn thành Sprint hiện tại trước khi bắt đầu Sprint mới.',
  INVALID_TARGET_CYCLE: 'Sprint nhận task tồn đọng không hợp lệ.'
})

export const getSprintErrorMessage = (error) => {
  const apiError = getSprintApiError(error)
  if (apiError.code && ERROR_MESSAGES[apiError.code]) {
    return ERROR_MESSAGES[apiError.code]
  }
  if (apiError.status === 403) return 'Bạn không có quyền thực hiện thao tác này.'
  if (apiError.status === 404) return 'Cycle không còn tồn tại hoặc bạn không còn quyền truy cập.'
  if (apiError.status === 409) return apiError.message || 'Trạng thái Cycle đã thay đổi. Dữ liệu mới nhất đã được tải lại.'
  if (apiError.status === 400) return apiError.message || 'Yêu cầu cập nhật Cycle không hợp lệ.'
  return apiError.message
}
