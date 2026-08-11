<template>
  <el-dialog
    :model-value="modelValue"
    @update:model-value="$emit('update:modelValue', $event)"
    title="Nạp dữ liệu công việc"
    width="min(1100px, calc(100vw - 24px))"
    destroy-on-close
    append-to-body
    class="task-data-import-dialog sa-data-dialog sa-modal--workspace"
    :close-on-click-modal="false"
    :show-close="false"
  >
    <template #header>
      <DataModalHeader
        icon="bi bi-cloud-arrow-up"
        title="Nạp dữ liệu công việc"
        description="Kiểm tra dữ liệu trước khi tạo công việc hàng loạt"
        @close="$emit('update:modelValue', false)"
      />
    </template>
    <el-tabs v-model="activeTab" type="border-card" class="import-tabs">
      <!-- ═══════════ TAB 1: Import Excel/CSV ═══════════ -->
      <el-tab-pane label="Import Excel/CSV" name="csv">
        <div class="tab-content">
          <!-- Template download -->
          <div class="tab-toolbar">
            <span class="hint-text">Tải file template mẫu, điền thông tin rồi tải lên.</span>
            <el-button size="small" @click="downloadTemplate">
              <i class="fa-solid fa-download mr-1"></i> Tải template (.csv)
            </el-button>
          </div>

          <!-- Drop zone -->
          <div
            class="drop-zone"
            :class="{ 'drag-active': csvDragOver }"
            @dragover.prevent="csvDragOver = true"
            @dragleave.prevent="csvDragOver = false"
            @drop.prevent="handleCsvDrop"
            @click="$refs.csvInput?.click()"
          >
            <input ref="csvInput" type="file" class="hidden-input" accept=".csv,.xlsx,.xls" @change="handleCsvFileSelect" />
            <i class="fa-solid fa-cloud-arrow-up drop-icon"></i>
            <p class="drop-title">Kéo thả file Excel/CSV vào đây hoặc click để chọn</p>
            <p class="drop-hint">Hỗ trợ: .csv, .xlsx</p>
          </div>

          <!-- File info -->
          <div v-if="csvFileName" class="file-chip">
            <i class="fa-regular fa-file-spreadsheet"></i>
            <span class="file-chip-name">{{ csvFileName }}</span>
            <span class="file-chip-size">({{ csvFileSize }})</span>
            <button class="file-chip-remove" @click="clearCsvFile"><i class="fa-solid fa-xmark"></i></button>
          </div>

          <!-- Header error -->
          <el-alert v-if="csvHeaderError" :title="csvHeaderError" type="error" show-icon :closable="false" class="mt-3" />

          <!-- Preview table -->
          <div v-if="csvRows.length" class="preview-section mt-3">
            <div class="preview-header">
              <span class="preview-title">Kết quả phân tích ({{ csvRows.length }} dòng)</span>
              <div class="preview-tags">
                <el-tag type="success" size="small">{{ csvValidCount }} hợp lệ</el-tag>
                <el-tag v-if="csvInvalidCount" type="danger" size="small">{{ csvInvalidCount }} lỗi</el-tag>
              </div>
            </div>
            <div class="table-scroll">
              <table class="preview-table">
                <thead>
                  <tr>
                    <th width="36"><el-checkbox v-model="csvAllChecked" @change="toggleCsvAll" /></th>
                    <th>Tiêu đề</th>
                    <th>Mô tả</th>
                    <th width="110">Ưu tiên</th>
                    <th width="170">Người phụ trách</th>
                    <th width="130">Hạn</th>
                    <th width="100">Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(row, idx) in csvRows" :key="idx" :class="{ 'row-error': row.error, 'row-selected': row.isChecked && !row.error }">
                    <td><el-checkbox v-model="row.isChecked" :disabled="!!row.error" /></td>
                    <td>
                      <el-input v-if="!row.error" v-model="row.title" size="small" placeholder="Tiêu đề..." />
                      <span v-else class="error-cell">{{ row.title || '—' }}</span>
                    </td>
                    <td>
                      <el-input v-if="!row.error" v-model="row.description" size="small" placeholder="Mô tả..." />
                      <span v-else>{{ row.description || '—' }}</span>
                    </td>
                    <td>
                      <el-select v-if="!row.error" v-model="row.priority" size="small" style="width:100%">
                        <el-option :value="1" label="Urgent" /><el-option :value="2" label="High" />
                        <el-option :value="3" label="Medium" /><el-option :value="4" label="Low" />
                      </el-select>
                      <span v-else>{{ priorityLabel(row.priority) }}</span>
                    </td>
                    <td>
                      <el-select v-if="!row.error" v-model="row.assigneeEmail" size="small" clearable placeholder="Chọn" style="width:100%">
                        <el-option v-for="m in projectMembers" :key="m.id" :value="m.email" :label="m.fullName || m.email" />
                      </el-select>
                      <span v-else>{{ row.assigneeEmail || '—' }}</span>
                    </td>
                    <td>
                    <el-date-picker v-if="!row.error" v-model="row.dueDate" type="date" size="small" format="DD/MM/YYYY" value-format="YYYY-MM-DD" popper-class="task-data-import-popper" placeholder="Ngày" style="width:100%" />
                      <span v-else>{{ row.dueDate || '—' }}</span>
                    </td>
                    <td>
                      <span v-if="row.error" class="row-error-msg">{{ row.error }}</span>
                      <span v-else class="status-chip">{{ row.status || 'TO DO' }}</span>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </el-tab-pane>

      <!-- ═══════════ TAB 2: AI phân tích tài liệu ═══════════ -->
      <el-tab-pane label="AI phân tích tài liệu" name="ai">
        <div class="tab-content">
          <!-- Drop zone -->
          <div
            class="drop-zone"
            :class="{ 'drag-active': aiDragOver }"
            @dragover.prevent="aiDragOver = true"
            @dragleave.prevent="aiDragOver = false"
            @drop.prevent="handleAiDrop"
            @click="$refs.aiInput?.click()"
          >
            <input ref="aiInput" type="file" class="hidden-input" accept=".txt,.md,.docx,.pdf" @change="handleAiFileSelect" />
            <i class="fa-solid fa-robot drop-icon ai-icon"></i>
            <p class="drop-title">Kéo thả tài liệu để AI phân tích</p>
            <p class="drop-hint">Hỗ trợ: .txt, .md, .docx, .pdf · Tối đa 10 MB · Không hỗ trợ PDF chỉ chứa ảnh</p>
          </div>

          <div v-if="aiFileName" class="file-chip">
            <i class="fa-regular fa-file"></i>
            <span class="file-chip-name">{{ aiFileName }}</span>
            <span class="file-chip-size">({{ aiFileSize }})</span>
            <button class="file-chip-remove" @click="clearAiFile"><i class="fa-solid fa-xmark"></i></button>
          </div>

          <!-- Prompt -->
          <div class="prompt-group mt-3">
            <label class="form-label">Bạn muốn AI phân tích theo hướng nào? (không bắt buộc)</label>
            <el-input v-model="aiPrompt" type="textarea" :rows="2" resize="none" placeholder="VD: Tập trung vào phần API Backend, ước lượng giờ cho từng task..." :disabled="aiLoading" />
          </div>

          <div class="ai-warning mt-3">
            <i class="fa-solid fa-triangle-exclamation"></i>
            <span>AI có thể sai. Vui lòng kiểm tra trước khi tạo công việc.</span>
          </div>

          <el-button class="analyze-btn mt-3" type="primary" :loading="aiLoading" :disabled="!aiFile" @click="runAiAnalysis">
            <i v-if="!aiLoading" class="fa-solid fa-brain mr-1"></i>
            {{ aiLoading ? 'AI đang phân tích...' : 'Phân tích bằng SprintA AI' }}
          </el-button>

          <!-- AI Results -->
          <div v-if="aiResult" class="ai-results mt-4">
            <div class="ai-summary-card">
              <h4><i class="fa-solid fa-clipboard-list mr-1"></i> Tóm tắt</h4>
              <p>{{ aiResult.summary }}</p>
              <div v-if="aiResult.keyPoints?.length" class="ai-list mt-2">
                <strong><i class="fa-solid fa-circle-check text-success mr-1"></i> Ý chính:</strong>
                <ul><li v-for="(p, i) in aiResult.keyPoints" :key="i">{{ p }}</li></ul>
              </div>
              <div v-if="aiResult.risks?.length" class="ai-list mt-2">
                <strong><i class="fa-solid fa-shield-halved text-danger mr-1"></i> Rủi ro:</strong>
                <ul><li v-for="(r, i) in aiResult.risks" :key="i">{{ r }}</li></ul>
              </div>
            </div>

            <!-- Refine -->
            <div class="refine-group mt-3">
              <label class="form-label">Chỉnh lại yêu cầu (AI sẽ cập nhật danh sách task)</label>
              <div class="refine-row">
                <el-input v-model="aiRefinePrompt" type="textarea" :rows="2" resize="none" placeholder="VD: Chia task nhỏ hơn, thêm mô tả chi tiết..." :disabled="aiLoading" />
                <el-button type="primary" :loading="aiLoading" :disabled="!aiRefinePrompt.trim()" @click="runAiRefine" class="refine-btn">
                  <i class="fa-solid fa-paper-plane"></i>
                </el-button>
              </div>
            </div>
          </div>

          <!-- AI Tasks preview (shared table) rendered below -->
        </div>
      </el-tab-pane>

      <!-- ═══════════ TAB 3: Dán nội dung ═══════════ -->
      <el-tab-pane label="Dán nội dung" name="paste">
        <div class="tab-content">
          <label class="form-label">Dán nội dung cuộc họp, yêu cầu khách hàng, chat... vào đây</label>
          <el-input v-model="pasteText" type="textarea" :rows="8" resize="vertical" placeholder="Dán nội dung văn bản tại đây..." :disabled="pasteLoading" />

          <div class="ai-warning mt-3">
            <i class="fa-solid fa-triangle-exclamation"></i>
            <span>AI có thể sai. Vui lòng kiểm tra trước khi tạo công việc.</span>
          </div>

          <el-button class="analyze-btn mt-3" type="primary" :loading="pasteLoading" :disabled="!pasteText.trim()" @click="runPasteAnalysis">
            <i v-if="!pasteLoading" class="fa-solid fa-brain mr-1"></i>
            {{ pasteLoading ? 'AI đang phân tích...' : 'AI phân tích nội dung' }}
          </el-button>

          <!-- Paste results -->
          <div v-if="pasteResult" class="ai-results mt-4">
            <div class="ai-summary-card">
              <h4><i class="fa-solid fa-clipboard-list mr-1"></i> Tóm tắt</h4>
              <p>{{ pasteResult.summary }}</p>
            </div>
          </div>
        </div>
      </el-tab-pane>
    </el-tabs>

    <!-- ═══════════ SHARED TASK TABLE (AI/Paste tabs) ═══════════ -->
    <div v-if="(activeTab === 'ai' || activeTab === 'paste') && currentAiTasks.length" class="shared-tasks-section mt-3">
      <div class="preview-header">
        <span class="preview-title">
          <i class="fa-solid fa-list-check mr-1"></i> Công việc đề xuất ({{ currentAiTasks.length }})
        </span>
        <div class="preview-tags">
          <span class="selected-count">Đã chọn: <strong>{{ aiSelectedCount }}</strong></span>
        </div>
      </div>
      <div class="table-scroll">
        <table class="preview-table">
          <thead>
            <tr>
              <th width="36"><el-checkbox v-model="aiAllChecked" @change="toggleAiAll" /></th>
              <th>Tiêu đề</th>
              <th>Mô tả</th>
              <th width="110">Ưu tiên</th>
              <th width="170">Người phụ trách</th>
              <th width="130">Hạn</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(task, idx) in currentAiTasks" :key="idx" :class="{ 'row-selected': task.isChecked }">
              <td><el-checkbox v-model="task.isChecked" /></td>
              <td><el-input v-model="task.title" size="small" placeholder="Tiêu đề..." /></td>
              <td><el-input v-model="task.description" size="small" type="textarea" :rows="1" autosize placeholder="Mô tả..." /></td>
              <td>
                <el-select v-model="task.priority" size="small" style="width:100%">
                  <el-option :value="1" label="Urgent" /><el-option :value="2" label="High" />
                  <el-option :value="3" label="Medium" /><el-option :value="4" label="Low" />
                </el-select>
              </td>
              <td>
                <el-select v-model="task.assigneeEmail" size="small" clearable placeholder="Chọn" style="width:100%">
                  <el-option v-for="m in projectMembers" :key="m.id" :value="m.email" :label="m.fullName || m.email" />
                </el-select>
              </td>
              <td>
                <el-date-picker v-model="task.dueDate" type="date" size="small" format="DD/MM/YYYY" value-format="YYYY-MM-DD" popper-class="task-data-import-popper" placeholder="Ngày" style="width:100%" />
              </td>
            </tr>
            <tr v-if="!currentAiTasks.length">
              <td colspan="6" class="no-data-cell">Chưa có task nào được phân tích.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- ═══════════ FOOTER ═══════════ -->
    <template #footer>
      <div class="dialog-footer">
        <div class="footer-left">
          <!-- Progress bar -->
          <div v-if="creating" class="progress-info">
            <el-progress :percentage="createProgress" :stroke-width="6" :show-text="false" style="width: 160px" />
            <span class="progress-text">{{ createProgressText }}</span>
          </div>
          <span v-else-if="totalSelected > 0" class="selected-info">
            Đã chọn <strong>{{ totalSelected }}</strong> task
          </span>
        </div>
        <div class="footer-right">
          <el-button @click="$emit('update:modelValue', false)">Hủy</el-button>
          <el-button type="primary" :disabled="totalSelected === 0 || creating" :loading="creating" @click="createAllTasks">
            <i v-if="!creating" class="fa-solid fa-circle-plus mr-1"></i>
            Tạo task trong SprintA
          </el-button>
        </div>
      </div>
    </template>
  </el-dialog>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'
import { parseCSVText, parseXLSXBuffer, processRawRows, buildTaskPayload } from '@/utils/taskImportParser'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  projectId: { type: String, required: true },
  projectMembers: { type: Array, default: () => [] },
  projectStatuses: { type: Array, default: () => [] },
})
const emit = defineEmits(['update:modelValue', 'imported'])

const activeTab = ref('csv')

// ────────────────────────────────────────────
// TAB 1: CSV / XLSX
// ────────────────────────────────────────────
const csvDragOver = ref(false)
const csvFileName = ref('')
const csvFileSize = ref('')
const csvRows = ref([])
const csvValidCount = ref(0)
const csvInvalidCount = ref(0)
const csvAllChecked = ref(true)
const csvHeaderError = ref('')

function downloadTemplate() {
  const headers = 'Tiêu đề công việc,Mô tả,Trạng thái,Email người phụ trách,Ưu tiên,Story Points,Hạn hoàn thành\n'
  const sample = 'Thiết kế giao diện dashboard,Thiết kế màn hình thống kê,TO DO,member@gmail.com,Cao,5,2026-07-31\n'
  const blob = new Blob([new Uint8Array([0xEF, 0xBB, 0xBF]), headers + sample], { type: 'text/csv;charset=utf-8;' })
  const link = document.createElement('a')
  link.href = URL.createObjectURL(blob)
  link.download = 'SprintA_Task_Template.csv'
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
}

function handleCsvDrop(e) {
  csvDragOver.value = false
  const file = e.dataTransfer?.files?.[0]
  if (file) processCsvFile(file)
}

function handleCsvFileSelect(e) {
  const file = e.target.files?.[0]
  if (file) processCsvFile(file)
  e.target.value = ''
}

function processCsvFile(file) {
  const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase()
  if (!['.csv', '.xlsx', '.xls'].includes(ext)) {
    ElMessage.error('Chỉ hỗ trợ file .csv hoặc .xlsx')
    return
  }
  if (file.size > 10 * 1024 * 1024) {
    ElMessage.error('Dung lượng file vượt quá 10MB.')
    return
  }

  csvFileName.value = file.name
  csvFileSize.value = formatBytes(file.size)
  csvHeaderError.value = ''

  const reader = new FileReader()
  if (ext === '.csv') {
    reader.onload = (ev) => {
      const rawRows = parseCSVText(ev.target.result)
      applyCsvParsed(rawRows)
    }
    reader.readAsText(file, 'UTF-8')
  } else {
    // XLSX
    reader.onload = (ev) => {
      try {
        const rawRows = parseXLSXBuffer(ev.target.result)
        applyCsvParsed(rawRows)
      } catch (err) {
        ElMessage.error('Không thể đọc file Excel: ' + (err.message || 'Lỗi không xác định'))
        clearCsvFile()
      }
    }
    reader.readAsArrayBuffer(file)
  }
}

function applyCsvParsed(rawRows) {
  const result = processRawRows(rawRows, props.projectMembers, props.projectStatuses)
  if (result.headerError) {
    csvHeaderError.value = result.headerError
    csvRows.value = []
    csvValidCount.value = 0
    csvInvalidCount.value = 0
    return
  }
  csvRows.value = result.rows
  csvValidCount.value = result.validCount
  csvInvalidCount.value = result.invalidCount
  csvAllChecked.value = true
}

function clearCsvFile() {
  csvFileName.value = ''
  csvFileSize.value = ''
  csvRows.value = []
  csvValidCount.value = 0
  csvInvalidCount.value = 0
  csvHeaderError.value = ''
}

function toggleCsvAll(val) {
  csvRows.value.forEach(r => { if (!r.error) r.isChecked = val })
}

// ────────────────────────────────────────────
// TAB 2: AI File
// ────────────────────────────────────────────
const aiDragOver = ref(false)
const aiFile = ref(null)
const aiFileName = ref('')
const aiFileSize = ref('')
const aiPrompt = ref('')
const aiRefinePrompt = ref('')
const aiLoading = ref(false)
const aiResult = ref(null)
const aiTasks = ref([])

function handleAiDrop(e) {
  aiDragOver.value = false
  const file = e.dataTransfer?.files?.[0]
  if (file) setAiFile(file)
}

function handleAiFileSelect(e) {
  const file = e.target.files?.[0]
  if (file) setAiFile(file)
  e.target.value = ''
}

function setAiFile(file) {
  const allowed = ['.txt', '.md', '.docx', '.pdf']
  const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase()
  if (!allowed.includes(ext)) {
    ElMessage.error('Chỉ hỗ trợ tài liệu .txt, .md, .docx hoặc .pdf. Hãy dùng tab Import Excel/CSV cho dữ liệu bảng.')
    return
  }
  if (file.size > 10 * 1024 * 1024) {
    ElMessage.error('Dung lượng file vượt quá 10MB.')
    return
  }
  aiFile.value = file
  aiFileName.value = file.name
  aiFileSize.value = formatBytes(file.size)
  aiResult.value = null
  aiTasks.value = []
}

function clearAiFile() {
  aiFile.value = null
  aiFileName.value = ''
  aiFileSize.value = ''
  aiResult.value = null
  aiTasks.value = []
  aiPrompt.value = ''
  aiRefinePrompt.value = ''
}

async function runAiAnalysis() {
  if (!aiFile.value) return
  aiLoading.value = true
  aiResult.value = null
  aiTasks.value = []

  const formData = new FormData()
  formData.append('file', aiFile.value)
  const pid = props.projectId
  if (isValidProjectId(pid)) formData.append('projectId', pid)
  if (aiPrompt.value.trim()) formData.append('userPrompt', aiPrompt.value)

  try {
    const res = await axiosClient.post('/ai/analyze-file', formData, { headers: { 'Content-Type': 'multipart/form-data' } })
    if (res.data?.data) {
      const data = res.data.data
      aiResult.value = data
      aiTasks.value = (data.suggestedTasks || []).map(t => ({ ...t, isChecked: true, assigneeEmail: '', dueDate: '' }))
    } else {
      ElMessage.error('AI không thể trả về kết quả phân tích.')
    }
  } catch (e) {
    ElMessage.error(e.response?.data?.message || 'Lỗi kết nối máy chủ AI.')
  } finally {
    aiLoading.value = false
  }
}

async function runAiRefine() {
  if (!aiFile.value || !aiRefinePrompt.value.trim()) return
  aiLoading.value = true

  const prevResult = JSON.parse(JSON.stringify(aiResult.value || {}))
  if (prevResult.suggestedTasks) {
    prevResult.suggestedTasks.forEach(t => { delete t.isChecked; delete t.assigneeEmail; delete t.dueDate })
  }

  const formData = new FormData()
  formData.append('file', aiFile.value)
  const pid = props.projectId
  if (isValidProjectId(pid)) formData.append('projectId', pid)
  formData.append('userPrompt', aiRefinePrompt.value)
  formData.append('previousAnalysisJson', JSON.stringify(prevResult))

  try {
    const res = await axiosClient.post('/ai/analyze-file', formData, { headers: { 'Content-Type': 'multipart/form-data' } })
    if (res.data?.data) {
      const data = res.data.data
      aiResult.value = data
      aiTasks.value = (data.suggestedTasks || []).map(t => ({ ...t, isChecked: true, assigneeEmail: '', dueDate: '' }))
      aiRefinePrompt.value = ''
      ElMessage.success('AI đã cập nhật danh sách task.')
    } else {
      ElMessage.error('AI không thể hiệu chỉnh kế hoạch.')
    }
  } catch (e) {
    ElMessage.error(e.response?.data?.message || 'Lỗi kết nối máy chủ AI.')
  } finally {
    aiLoading.value = false
  }
}

// ────────────────────────────────────────────
// TAB 3: Paste
// ────────────────────────────────────────────
const pasteText = ref('')
const pasteLoading = ref(false)
const pasteResult = ref(null)
const pasteTasks = ref([])

async function runPasteAnalysis() {
  if (!pasteText.value.trim()) return
  pasteLoading.value = true
  pasteResult.value = null
  pasteTasks.value = []

  // Create a text Blob to send as file
  const blob = new Blob([pasteText.value], { type: 'text/plain' })
  const file = new File([blob], 'pasted_content.txt', { type: 'text/plain' })

  const formData = new FormData()
  formData.append('file', file)
  const pid = props.projectId
  if (isValidProjectId(pid)) formData.append('projectId', pid)
  formData.append('userPrompt', 'Phân tích nội dung được dán bởi người dùng và đề xuất danh sách công việc chi tiết.')

  try {
    const res = await axiosClient.post('/ai/analyze-file', formData, { headers: { 'Content-Type': 'multipart/form-data' } })
    if (res.data?.data) {
      const data = res.data.data
      pasteResult.value = data
      pasteTasks.value = (data.suggestedTasks || []).map(t => ({ ...t, isChecked: true, assigneeEmail: '', dueDate: '' }))
    } else {
      ElMessage.error('AI không thể phân tích nội dung.')
    }
  } catch (e) {
    ElMessage.error(e.response?.data?.message || 'Lỗi kết nối máy chủ AI.')
  } finally {
    pasteLoading.value = false
  }
}

// ────────────────────────────────────────────
// Shared AI tasks for active tab
// ────────────────────────────────────────────
const currentAiTasks = computed(() => {
  if (activeTab.value === 'ai') return aiTasks.value
  if (activeTab.value === 'paste') return pasteTasks.value
  return []
})

const aiAllChecked = ref(true)
function toggleAiAll(val) {
  currentAiTasks.value.forEach(t => { t.isChecked = val })
}

const aiSelectedCount = computed(() => currentAiTasks.value.filter(t => t.isChecked).length)

// ────────────────────────────────────────────
// Total selected (all tabs)
// ────────────────────────────────────────────
const totalSelected = computed(() => {
  if (activeTab.value === 'csv') {
    return csvRows.value.filter(r => r.isChecked && !r.error).length
  }
  return currentAiTasks.value.filter(t => t.isChecked && t.title?.trim()).length
})

// ────────────────────────────────────────────
// Create tasks
// ────────────────────────────────────────────
const creating = ref(false)
const createProgress = ref(0)
const createProgressText = ref('')

async function createAllTasks() {
  const pid = props.projectId
  if (!isValidProjectId(pid)) {
    ElMessage.error('ID dự án không hợp lệ.')
    return
  }

  let tasksToCreate = []
  if (activeTab.value === 'csv') {
    tasksToCreate = csvRows.value
      .filter(r => r.isChecked && !r.error && r.title?.trim())
      .map(r => buildTaskPayload(r, props.projectMembers))
  } else {
    tasksToCreate = currentAiTasks.value
      .filter(t => t.isChecked && t.title?.trim())
      .map(t => buildTaskPayload(t, props.projectMembers))
  }

  if (!tasksToCreate.length) {
    ElMessage.warning('Không có task hợp lệ nào để tạo.')
    return
  }

  creating.value = true
  createProgress.value = 0
  let successCount = 0
  let errorCount = 0

  for (let i = 0; i < tasksToCreate.length; i++) {
    createProgressText.value = `${i + 1} / ${tasksToCreate.length}`
    createProgress.value = Math.round(((i + 1) / tasksToCreate.length) * 100)

    try {
      await axiosClient.post(`/projects/${pid}/WorkTasks`, tasksToCreate[i])
      successCount++
    } catch (e) {
      console.error('Lỗi tạo task:', tasksToCreate[i].title, e)
      errorCount++
    }
  }

  creating.value = false
  createProgress.value = 0

  if (successCount > 0) {
    ElMessage.success(`Tạo thành công ${successCount} công việc trong SprintA.`)
    emit('imported')
    emit('update:modelValue', false)
  }
  if (errorCount > 0) {
    ElMessage.error(`Có ${errorCount} công việc gặp lỗi không thể tạo.`)
  }
}

// ────────────────────────────────────────────
// Helpers
// ────────────────────────────────────────────
function isValidProjectId(pid) {
  return pid && pid !== '00000000-0000-0000-0000-000000000000' && pid !== 'undefined' && pid !== 'null' && pid !== '1'
}

function formatBytes(bytes) {
  if (!+bytes) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`
}

function priorityLabel(p) {
  return { 1: 'Urgent', 2: 'High', 3: 'Medium', 4: 'Low' }[p] || 'Medium'
}

// Reset state when dialog closes
watch(() => props.modelValue, (val) => {
  if (!val) {
    clearCsvFile()
    clearAiFile()
    pasteText.value = ''
    pasteResult.value = null
    pasteTasks.value = []
    activeTab.value = 'csv'
  }
})
</script>

<style scoped>
:deep(.el-dialog.task-data-import-dialog) {
  width: min(1100px, calc(100vw - 24px)) !important;
  max-width: calc(100vw - 24px) !important;
  max-height: calc(100dvh - 24px);
  display: flex;
  flex-direction: column;
  margin: 12px auto !important;
}
:deep(.el-dialog.task-data-import-dialog .el-dialog__body) {
  flex: 1 1 auto;
  min-height: 0;
  overflow-x: hidden;
  overflow-y: auto;
}
:deep(.el-dialog.task-data-import-dialog .el-dialog__footer) { flex: 0 0 auto; }
/* ═══════════ DIALOG ═══════════ */
:deep(.task-data-import-dialog .el-dialog__body) {
  padding: 0 20px 8px;
  max-height: 75vh;
  overflow-y: auto;
}
:deep(.task-data-import-dialog .el-dialog__header) {
  padding: 16px 20px 12px;
  border-bottom: 1px solid var(--color-border, #e5e7eb);
}
:deep(.task-data-import-dialog .el-dialog__title) {
  font-size: 17px;
  font-weight: 700;
  color: var(--color-text-primary, #1e293b);
}

/* ═══════════ TABS ═══════════ */
.import-tabs {
  border: none !important;
  box-shadow: none !important;
  background: var(--color-surface, #ffffff);
}
:deep(.import-tabs .el-tabs__header) {
  background: transparent;
  border-bottom: 1px solid var(--color-border, #e5e7eb);
  margin: 0 0 12px;
}
:deep(.import-tabs .el-tabs__content) {
  padding: 0 !important;
}
:deep(.import-tabs .el-tabs__item) {
  font-size: 13px;
  font-weight: 600;
}

/* ═══════════ TAB CONTENT ═══════════ */
.tab-content {
  padding: 0;
  background: var(--color-surface, #ffffff);
  color: var(--color-text-primary, #334155);
}
.tab-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}
.hint-text {
  font-size: 12px;
  color: var(--color-text-muted, #94a3b8);
}

/* ═══════════ DROP ZONE ═══════════ */
.drop-zone {
  padding: 28px 20px;
  border: 2px dashed var(--color-border, #cbd5e1);
  border-radius: 12px;
  text-align: center;
  cursor: pointer;
  transition: all 0.2s;
  background: var(--color-surface-raised, #f8fafc);
}
.drop-zone:hover,
.drop-zone.drag-active {
  border-color: var(--el-color-primary, #409eff);
  background: rgba(59, 130, 246, 0.04);
}
.drop-icon {
  font-size: 32px;
  color: var(--color-text-muted, #94a3b8);
  margin-bottom: 8px;
  display: block;
}
.drop-icon.ai-icon { color: var(--el-color-primary, #6366f1); }
.drop-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-primary, #334155);
  margin: 0 0 4px;
}
.drop-hint {
  font-size: 11px;
  color: var(--color-text-muted, #94a3b8);
  margin: 0;
}
.hidden-input { display: none; }

/* ═══════════ FILE CHIP ═══════════ */
.file-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  background: var(--el-fill-color-light, #f1f5f9);
  border-radius: 8px;
  font-size: 13px;
  margin-top: 10px;
  color: var(--color-text-primary, #334155);
}
.file-chip-name { font-weight: 600; }
.file-chip-size { color: var(--color-text-muted, #94a3b8); font-size: 11px; }
.file-chip-remove {
  background: none; border: none; cursor: pointer; color: var(--color-text-muted, #94a3b8);
  padding: 2px 4px; border-radius: 4px; font-size: 12px;
}
.file-chip-remove:hover { color: #ef4444; background: rgba(239, 68, 68, 0.08); }

/* ═══════════ PREVIEW ═══════════ */
.preview-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}
.preview-title {
  font-size: 13px;
  font-weight: 700;
  color: var(--color-text-primary, #334155);
}
.preview-tags { display: flex; gap: 6px; }
.table-scroll {
  max-height: 320px;
  max-width: 100%;
  overflow-x: auto;
  overflow-y: auto;
  border: 1px solid var(--color-border, #e5e7eb);
  border-radius: 8px;
}
.preview-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.preview-table thead tr {
  background: rgba(148, 163, 184, 0.06);
  position: sticky;
  top: 0;
  z-index: 1;
}
.preview-table th {
  padding: 8px 6px;
  font-weight: 700;
  text-align: left;
  border-bottom: 1px solid var(--color-border, #e5e7eb);
  color: var(--color-text-primary, #475569);
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}
.preview-table td {
  padding: 6px;
  border-bottom: 1px solid var(--color-border, #f1f5f9);
  vertical-align: middle;
}
.preview-table tr.row-error {
  background: rgba(239, 68, 68, 0.04);
}
.preview-table tr.row-selected {
  background: rgba(59, 130, 246, 0.03);
}
.row-error-msg {
  font-size: 11px;
  color: #ef4444;
  font-weight: 600;
}
.error-cell { color: var(--color-text-muted, #94a3b8); }
.status-chip {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 4px;
  background: rgba(59, 130, 246, 0.08);
  color: var(--el-color-primary, #3b82f6);
  font-weight: 600;
}
.no-data-cell {
  text-align: center;
  padding: 24px !important;
  color: var(--color-text-muted, #94a3b8);
}

/* ═══════════ AI ═══════════ */
.form-label {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-primary, #475569);
  margin-bottom: 6px;
  display: block;
}
.ai-warning {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: rgba(245, 158, 11, 0.06);
  border: 1px solid rgba(245, 158, 11, 0.15);
  border-radius: 8px;
  font-size: 12px;
  color: #b45309;
}
.ai-warning i { color: #f59e0b; }
.analyze-btn { width: 100%; }

.ai-summary-card {
  padding: 14px 16px;
  background: var(--el-fill-color-light, #f8fafc);
  border-radius: 10px;
  border: 1px solid var(--color-border, #e5e7eb);
}
.ai-summary-card h4 {
  font-size: 13px;
  font-weight: 700;
  margin: 0 0 6px;
  color: var(--color-text-primary, #334155);
}
.ai-summary-card p {
  font-size: 13px;
  margin: 0;
  color: var(--color-text-secondary, #64748b);
  line-height: 1.5;
}
.ai-list {
  font-size: 12px;
  color: var(--color-text-secondary, #64748b);
}
.ai-list strong { color: var(--color-text-primary, #334155); font-size: 12px; }
.ai-list ul { margin: 4px 0 0 16px; padding: 0; }
.ai-list li { margin-bottom: 2px; }
.text-success { color: #10b981; }
.text-danger { color: #ef4444; }

.refine-row { display: flex; gap: 8px; align-items: flex-start; }
.refine-btn { height: auto; min-height: 52px; }

/* ═══════════ SHARED TASKS ═══════════ */
.selected-count {
  font-size: 12px;
  color: var(--color-text-muted, #94a3b8);
}

/* ═══════════ FOOTER ═══════════ */
.dialog-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-top: 12px;
  border-top: 1px solid var(--color-border, #e5e7eb);
}
.footer-left { display: flex; align-items: center; gap: 12px; }
.footer-right { display: flex; gap: 8px; }
.selected-info {
  font-size: 13px;
  color: var(--color-text-muted, #64748b);
}
.progress-info {
  display: flex;
  align-items: center;
  gap: 8px;
}
.progress-text {
  font-size: 12px;
  color: var(--color-text-muted, #64748b);
}

/* Element Plus teleports dialog internals and poppers outside this component. */
:deep(.task-data-import-dialog .el-dialog),
:deep(.task-data-import-dialog .el-dialog__header),
:deep(.task-data-import-dialog .el-dialog__body),
:deep(.task-data-import-dialog .el-dialog__footer),
:deep(.task-data-import-dialog .el-tabs--border-card),
:deep(.task-data-import-dialog .el-tabs__header),
:deep(.task-data-import-dialog .el-tabs__content),
:deep(.task-data-import-dialog .el-input__wrapper),
:deep(.task-data-import-dialog .el-select__wrapper),
:deep(.task-data-import-dialog .el-alert),
:deep(.task-data-import-dialog .el-table),
:deep(.task-data-import-dialog .el-table th.el-table__cell),
:deep(.task-data-import-dialog .el-table tr),
:deep(.task-data-import-dialog .el-table td.el-table__cell) {
  background: var(--color-surface) !important;
  color: var(--color-text-primary) !important;
  border-color: var(--color-border) !important;
}
:deep(.task-data-import-dialog .el-tabs__item.is-active) { color: var(--color-accent) !important; }
:deep(.task-data-import-dialog .el-table__body tr:hover > td.el-table__cell) { background: var(--color-surface-hover) !important; }
:global(.task-data-import-popper) {
  background: var(--color-surface) !important;
  border-color: var(--color-border) !important;
  color: var(--color-text-primary) !important;
}
:global(.task-data-import-popper .el-picker-panel),
:global(.task-data-import-popper .el-date-table th),
:global(.task-data-import-popper .el-date-table td.available:hover),
:global(.task-data-import-popper .el-picker-panel__footer) {
  background: var(--color-surface) !important;
  color: var(--color-text-primary) !important;
  border-color: var(--color-border) !important;
}
:global(.task-data-import-popper .el-date-table th),
:global(.task-data-import-popper .el-date-table td) { color: var(--color-text-secondary) !important; }

/* The dialog is teleported to body, so keep the dark theme scoped to its namespace. */
:global(.dark .task-data-import-dialog .el-tabs),
:global(.dark .task-data-import-dialog .el-tabs__header),
:global(.dark .task-data-import-dialog .el-tabs__nav-wrap),
:global(.dark .task-data-import-dialog .el-tabs__content),
:global(.dark .task-data-import-dialog .el-tab-pane),
:global(.dark .task-data-import-dialog .tab-content),
:global(.dark .task-data-import-dialog .drop-zone),
:global(.dark .task-data-import-dialog .table-scroll),
:global(.dark .task-data-import-dialog .dialog-footer),
:global([data-theme='dark'] .task-data-import-dialog .el-tabs),
:global([data-theme='dark'] .task-data-import-dialog .el-tabs__header),
:global([data-theme='dark'] .task-data-import-dialog .el-tabs__nav-wrap),
:global([data-theme='dark'] .task-data-import-dialog .el-tabs__content),
:global([data-theme='dark'] .task-data-import-dialog .el-tab-pane),
:global([data-theme='dark'] .task-data-import-dialog .tab-content),
:global([data-theme='dark'] .task-data-import-dialog .drop-zone),
:global([data-theme='dark'] .task-data-import-dialog .table-scroll),
:global([data-theme='dark'] .task-data-import-dialog .dialog-footer) {
  background: var(--color-surface, #0b2037) !important;
  color: var(--color-text-primary, #edf6fd) !important;
  border-color: var(--color-border, #203b58) !important;
}
:global(.dark .task-data-import-dialog .drop-zone),
:global([data-theme='dark'] .task-data-import-dialog .drop-zone) {
  background: var(--color-surface-hover, var(--color-surface, #102a46)) !important;
  border-color: var(--color-border, #375978) !important;
}
:global(.dark .task-data-import-dialog .el-tabs__item),
:global([data-theme='dark'] .task-data-import-dialog .el-tabs__item) {
  color: var(--color-text-secondary, #b8ccdc) !important;
}
:global(.dark .task-data-import-dialog .el-tabs__item.is-active),
:global([data-theme='dark'] .task-data-import-dialog .el-tabs__item.is-active) {
  color: var(--color-accent, #66d9ef) !important;
}
:global(.dark .task-data-import-dialog .el-tabs__item.is-disabled),
:global([data-theme='dark'] .task-data-import-dialog .el-tabs__item.is-disabled) {
  color: var(--color-text-muted, #8fa8bc) !important;
}
:global(.dark .task-data-import-dialog .drop-title),
:global([data-theme='dark'] .task-data-import-dialog .drop-title) { color: var(--color-text-primary, #edf6fd) !important; }
:global(.dark .task-data-import-dialog .drop-hint),
:global(.dark .task-data-import-dialog .hint-text),
:global([data-theme='dark'] .task-data-import-dialog .drop-hint),
:global([data-theme='dark'] .task-data-import-dialog .hint-text) { color: var(--color-text-muted, #9bb0c5) !important; }

@media (max-width: 600px) {
  :deep(.el-dialog.task-data-import-dialog) {
    width: calc(100vw - 24px) !important;
    max-width: calc(100vw - 24px) !important;
    max-height: calc(100dvh - 24px) !important;
  }
  :deep(.task-data-import-dialog .el-dialog) {
    width: calc(100vw - 24px) !important;
    max-width: calc(100vw - 24px) !important;
    margin: 12px auto !important;
  }
  :deep(.task-data-import-dialog .el-dialog__body) { padding: 10px !important; overflow-x: hidden; }
  :deep(.task-data-import-dialog .el-dialog__footer) { padding: 10px !important; }
  .tab-toolbar, .preview-header, .dialog-footer { align-items: flex-start; flex-direction: column; gap: 10px; }
  .footer-left, .footer-right { width: 100%; flex-wrap: wrap; }
  :deep(.footer-right .el-button) { flex: 1 1 140px; min-height: 38px; }
}

/* ═══════════ UTILITIES ═══════════ */
.mt-2 { margin-top: 8px; }
.mt-3 { margin-top: 12px; }
.mt-4 { margin-top: 16px; }
.mr-1 { margin-right: 4px; }
</style>
