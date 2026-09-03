import * as XLSX from 'xlsx'

const HEADER_MAP = {
  'tiêu đề công việc': 'title', 'tiêu đề': 'title', 'tên công việc': 'title',
  'title': 'title', 'mô tả': 'description', 'description': 'description',
  'email người phụ trách': 'assigneeEmail', 'người phụ trách': 'assigneeEmail',
  'assignee email': 'assigneeEmail', 'assigneeemail': 'assigneeEmail', 'assignee': 'assigneeEmail',
  'trạng thái': 'status', 'status': 'status', 'ưu tiên': 'priority', 'priority': 'priority',
  'hạn hoàn thành': 'dueDate', 'hạn': 'dueDate', 'due date': 'dueDate', 'duedate': 'dueDate', 'due': 'dueDate',
  'ngày bắt đầu': 'startDate', 'start date': 'startDate', 'startdate': 'startDate',
  'story point': 'storyPoints', 'story points': 'storyPoints', 'storypoint': 'storyPoints', 'storypoints': 'storyPoints', 'điểm': 'storyPoints',
  'ước lượng giờ': 'estimatedHours', 'estimated hours': 'estimatedHours', 'estimatedhours': 'estimatedHours', 'hours': 'estimatedHours'
}

const PRIORITY_MAP = {
  urgent: 1, critical: 1, 'khẩn cấp': 1, 'rất cao': 1,
  high: 2, cao: 2, medium: 3, 'trung bình': 3, normal: 3,
  low: 4, 'low-vn': 4, none: 5, 'không có': 5
}

const clean = (value) => String(value ?? '').replace(/^\uFEFF/, '').trim()
const normalizedHeader = (value) => clean(value).toLowerCase().normalize('NFC')

export function mapHeaders(headers = []) {
  return headers.map((header) => HEADER_MAP[normalizedHeader(header)] || null)
}

/** Return an ISO calendar date (YYYY-MM-DD), never a locale-dependent Date string. */
export function parseImportDate(value) {
  if (value === null || value === undefined || value === '') return ''
  if (typeof value === 'number' && Number.isFinite(value)) {
    const serial = Math.trunc(value)
    if (serial < 1 || serial > 2958465) return null
    const date = new Date(Date.UTC(1899, 11, 30) + serial * 86400000)
    return date.toISOString().slice(0, 10)
  }
  const raw = clean(value)
  if (/^\d{4}-\d{1,2}-\d{1,2}$/.test(raw)) {
    const [year, month, day] = raw.split('-').map(Number)
    return validIsoDate(year, month, day)
  }
  const match = raw.match(/^(\d{1,2})[/-](\d{1,2})[/-](\d{4})$/)
  if (match) return validIsoDate(Number(match[3]), Number(match[2]), Number(match[1]))
  return null
}

function validIsoDate(year, month, day) {
  if (year < 1900 || year > 9999 || month < 1 || month > 12 || day < 1) return null
  const days = new Date(Date.UTC(year, month, 0)).getUTCDate()
  return day <= days ? `${year.toString().padStart(4, '0')}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}` : null
}

export function parseCSVText(text = '') {
  const rows = []
  let row = []; let cell = ''; let quoted = false
  const source = String(text).replace(/^\uFEFF/, '')
  for (let i = 0; i < source.length; i += 1) {
    const char = source[i]; const next = source[i + 1]
    if (char === '"') {
      if (quoted && next === '"') { cell += '"'; i += 1 } else quoted = !quoted
    } else if (char === ',' && !quoted) { row.push(cell); cell = ''
    } else if ((char === '\n' || char === '\r') && !quoted) {
      if (char === '\r' && next === '\n') i += 1
      row.push(cell); if (row.some((v) => clean(v))) rows.push(row)
      row = []; cell = ''
    } else cell += char
  }
  if (cell || row.length) { row.push(cell); if (row.some((v) => clean(v))) rows.push(row) }
  return rows
}

export function parseXLSXBuffer(arrayBuffer) {
  const workbook = XLSX.read(new Uint8Array(arrayBuffer), { type: 'array', cellDates: false })
  const sheet = workbook.Sheets[workbook.SheetNames[0]]
  return sheet ? XLSX.utils.sheet_to_json(sheet, { header: 1, defval: '' }) : []
}

function normalizePriority(value) {
  const raw = normalizedHeader(value)
  return PRIORITY_MAP[raw] ?? (Number.isFinite(Number(raw)) ? Number(raw) : 3)
}

export function processRawRows(rawRows = [], projectMembers = [], projectStatuses = []) {
  if (rawRows.length < 2) return { rows: [], validCount: 0, invalidCount: 0, headerError: 'File không có dữ liệu.' }
  const fieldMap = mapHeaders(rawRows[0])
  if (!fieldMap.includes('title')) return { rows: [], validCount: 0, invalidCount: 0, headerError: 'Không tìm thấy cột Tiêu đề công việc hoặc Title.' }
  const emails = new Set(projectMembers.map((m) => clean(m.email).toLowerCase()).filter(Boolean))
  const statuses = new Set(projectStatuses.map((s) => clean(s.name).toUpperCase()).filter(Boolean))
  const rows = []
  for (let index = 1; index < rawRows.length; index += 1) {
    const columns = rawRows[index] || []
    if (!columns.some((value) => clean(value))) continue
    const record = { rowNum: index + 1, title: '', description: '', status: '', assigneeEmail: '', priority: 3, storyPoints: '', startDate: '', dueDate: '', estimatedHours: '', error: null, isChecked: true }
    fieldMap.forEach((field, column) => {
      if (!field) return
      if (field === 'priority') record[field] = normalizePriority(columns[column])
      else if (field === 'startDate' || field === 'dueDate') record[field] = parseImportDate(columns[column])
      else record[field] = clean(columns[column])
    })
    if (!record.title) record.error = 'Thiếu tiêu đề công việc.'
    else if (record.assigneeEmail && !emails.has(record.assigneeEmail.toLowerCase())) record.error = 'Email người phụ trách không thuộc dự án.'
    else if (record.startDate === null) record.error = 'Ngày bắt đầu không đúng. Dùng DD/MM/YYYY, ví dụ 01/07/2026.'
    else if (record.dueDate === null) record.error = 'Hạn hoàn thành không đúng. Dùng DD/MM/YYYY, ví dụ 31/07/2026.'
    else if (record.startDate && record.dueDate && record.startDate > record.dueDate) record.error = 'Ngày bắt đầu không được sau hạn hoàn thành.'
    else if (record.status && statuses.size && !statuses.has(record.status.toUpperCase())) record.error = `Trạng thái “${record.status}” không tồn tại trong dự án.`
    if (record.error) record.isChecked = false
    rows.push(record)
  }
  return { rows, validCount: rows.filter((row) => !row.error).length, invalidCount: rows.filter((row) => row.error).length }
}

export function buildTaskPayload(row, projectMembers = []) {
  const assignee = projectMembers.find((m) => clean(m.email).toLowerCase() === clean(row.assigneeEmail).toLowerCase())
  const assigneeId = assignee?.userId || assignee?.id || null
  return {
    title: clean(row.title), description: clean(row.description), statusName: row.status || 'TO DO', priority: Number(row.priority) || 3,
    storyPoints: Number(row.storyPoints) || 0, startDate: row.startDate || null, dueDate: row.dueDate || null,
    totalEstimatedHours: Number(row.estimatedHours) || 0, assignedUserId: assigneeId, assigneeIds: assigneeId ? [assigneeId] : []
  }
}
