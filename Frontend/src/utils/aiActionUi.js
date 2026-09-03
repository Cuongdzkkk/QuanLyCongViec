const readOnlyActionTypes = new Set([
  'summarize_dashboard', 'summarize_project', 'list_overdue_tasks', 'get_workload',
  'explain_report', 'summarize_page', 'summarize_intakes', 'suggest_view_filter',
  'list_work_items', 'list_cycles', 'list_modules', 'list_pages', 'list_views',
  'list_intakes', 'list_pending_intakes', 'analyze_priority_distribution',
  'analyze_status_distribution', 'analyze_workload', 'identify_project_risks',
  'refresh_report', 'export_report_csv', 'summarize_report'
])

export const isReadOnlyAiAction = (type, requiresConfirmation) =>
  requiresConfirmation === false || readOnlyActionTypes.has(String(type || '').toLowerCase())

export const writeAiActions = (actions = []) =>
  actions.filter(action => !isReadOnlyAiAction(action?.type, action?.requiresConfirmation))

export const aiActionLabel = (type = '') => ({
  create_project: 'Tạo project mới',
  create_task: 'Tạo task mới',
  create_cycle: 'Tạo chu kỳ mới',
  create_module: 'Tạo mô-đun mới',
  create_page: 'Tạo tài liệu mới',
  create_view: 'Tạo bộ lọc đã lưu',
  create_intake_request: 'Tạo yêu cầu mới',
  update_task_status: 'Cập nhật trạng thái task',
  update_task_priority: 'Cập nhật độ ưu tiên',
  update_task_due_date: 'Cập nhật hạn task',
  assign_task: 'Giao task cho thành viên',
  add_comment: 'Thêm bình luận',
  create_goal: 'Tạo mục tiêu mới',
  summarize_dashboard: 'Tóm tắt dashboard',
  summarize_project: 'Tóm tắt dự án',
  list_overdue_tasks: 'Liệt kê task quá hạn',
  get_workload: 'Xem tải công việc',
  explain_report: 'Giải thích báo cáo',
  summarize_page: 'Tóm tắt tài liệu',
  summarize_intakes: 'Tóm tắt hàng chờ yêu cầu',
  suggest_view_filter: 'Gợi ý bộ lọc'
}[String(type).toLowerCase()] || 'Thực hiện thay đổi')

export const aiActionStatusLabel = action => ({
  pending: 'Chờ xác nhận',
  loading: 'Đang xử lý',
  success: 'Thành công',
  cancelled: 'Đã hủy',
  error: 'Thất bại'
}[action?.uiStatus || 'pending'] || 'Chờ xác nhận')

export const aiActionPayload = action => action?.payload || {}

export const aiActionPayloadValue = (action, ...keys) => {
  const payload = aiActionPayload(action)
  const key = keys.find(item => payload[item] !== undefined && payload[item] !== null && `${payload[item]}`.trim() !== '')
  return key ? payload[key] : ''
}

export const aiActionSummary = action => {
  const type = String(action?.type || '').toLowerCase()
  if (type === 'create_project') return `Tạo project “${aiActionPayloadValue(action, 'name', 'projectName') || 'Chưa đặt tên'}”.`
  if (type === 'create_task') return `Tạo task “${aiActionPayloadValue(action, 'title', 'taskTitle') || 'Chưa đặt tên'}”.`
  if (type === 'create_goal') return `Tạo mục tiêu “${aiActionPayloadValue(action, 'title', 'name') || 'Chưa đặt tên'}”.`
  if (type === 'update_task_status') return `Chuyển task sang “${aiActionPayloadValue(action, 'statusName', 'status') || 'trạng thái mới'}”.`
  if (type === 'assign_task') return 'Giao task cho thành viên được chỉ định.'
  if (isReadOnlyAiAction(type)) return 'Đọc dữ liệu hiện tại để trả về một tóm tắt có căn cứ.'
  return 'AI đề xuất một thay đổi cần bạn xác nhận.'
}

export const aiActionDetails = (action, resolveProjectLabel = () => 'Dự án hiện tại') => {
  const type = String(action?.type || '').toLowerCase()
  const details = []
  const add = (label, value) => {
    if (value !== '' && value !== null && value !== undefined) details.push({ label, value: `${value}` })
  }
  if (type === 'create_project') {
    add('Tên project', aiActionPayloadValue(action, 'name', 'projectName'))
    add('Mô tả', aiActionPayloadValue(action, 'description'))
  } else if (type === 'create_task') {
    add('Tiêu đề', aiActionPayloadValue(action, 'title', 'taskTitle'))
    add('Hạn', aiActionPayloadValue(action, 'dueDate', 'plannedEndDate'))
    add('Ưu tiên', aiActionPayloadValue(action, 'priority'))
  } else if (type === 'create_goal') {
    add('Tên mục tiêu', aiActionPayloadValue(action, 'title', 'name'))
    add('Mô tả', aiActionPayloadValue(action, 'description'))
  } else if (type === 'update_task_status') {
    add('Task', aiActionPayloadValue(action, 'taskTitle', 'title'))
    add('Trạng thái mới', aiActionPayloadValue(action, 'statusName', 'status'))
  } else if (type === 'assign_task') {
    add('Task', aiActionPayloadValue(action, 'taskTitle', 'title'))
    add('Người nhận', aiActionPayloadValue(action, 'assigneeName', 'assigneeEmail', 'assignee'))
  } else if (['create_cycle', 'create_module', 'create_page', 'create_view', 'create_intake_request'].includes(type)) {
    add('Tên', aiActionPayloadValue(action, 'name', 'title'))
    add('Dự án', aiActionPayloadValue(action, 'projectName') || resolveProjectLabel(action))
    add('Bắt đầu', aiActionPayloadValue(action, 'startDate'))
    add('Kết thúc', aiActionPayloadValue(action, 'endDate'))
  } else if (['update_task_priority', 'update_task_due_date'].includes(type)) {
    add('Task', aiActionPayloadValue(action, 'taskTitle', 'title'))
    add(type === 'update_task_priority' ? 'Độ ưu tiên mới' : 'Hạn mới', aiActionPayloadValue(action, type === 'update_task_priority' ? 'priority' : 'dueDate'))
  } else if (type === 'add_comment') {
    add('Đối tượng', aiActionPayloadValue(action, 'entityType'))
    add('Nội dung', aiActionPayloadValue(action, 'content'))
  }
  return details
}
