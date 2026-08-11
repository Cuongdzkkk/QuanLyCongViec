<template>
  <div class="ai-file-intake-container">
    <div class="header-section">
      <h2 class="page-title">
        <i class="fa-solid fa-robot text-accent mr-2"></i>
        {{ t('AI File Intake & Analyzer', 'AI Phân tích tài liệu & Lên kế hoạch') }}
      </h2>
      <p class="page-subtitle">
        {{ t('Upload your files and let AI extract structure, analyze risks, plan tasks, and generate coding prompts.', 'Tải lên tài liệu của bạn để AI tự động phân tích, tóm tắt, đề xuất danh sách công việc và tạo prompt lập trình.') }}
      </p>
    </div>

    <div class="main-content-grid">
      <!-- Left column: Upload & Inputs -->
      <div class="control-panel card">
        <h3 class="panel-title">{{ t('Upload Document', 'Tải lên tài liệu') }}</h3>
        
        <!-- Drag & Drop Zone -->
        <div 
          class="upload-dropzone"
          :class="{ 'is-dragover': isDragOver }"
          @dragover.prevent="isDragOver = true"
          @dragleave.prevent="isDragOver = false"
          @drop.prevent="handleFileDrop"
          @click="triggerFileSelect"
        >
          <input 
            type="file" 
            ref="fileInputRef" 
            class="hidden-input" 
            @change="handleFileSelect"
            accept=".txt,.md,.docx,.pdf"
          />
          <div class="dropzone-content">
            <i class="fa-solid fa-cloud-arrow-up drop-icon"></i>
            <p class="drop-text-primary">{{ t('Drag & drop your file here or click to browse', 'Kéo thả file vào đây hoặc click để chọn') }}</p>
            <p class="drop-text-secondary">
              {{ t('Supported: .txt, .md, .docx, .pdf (Max 10 MB). Image-only PDFs are not supported.', 'Hỗ trợ: .txt, .md, .docx, .pdf (Tối đa 10 MB). Không hỗ trợ PDF chỉ chứa ảnh.') }}
            </p>
          </div>
        </div>

        <!-- Selected File Status -->
        <div v-if="selectedFile" class="selected-file-info">
          <div class="file-meta">
            <i class="fa-regular" :class="getFileIcon(selectedFile.name)"></i>
            <div class="file-details">
              <span class="file-name">{{ selectedFile.name }}</span>
              <span class="file-size">{{ formatBytes(selectedFile.size) }}</span>
            </div>
          </div>
          <button class="remove-file-btn" @click="removeSelectedFile" :disabled="loading">
            <i class="fa-solid fa-xmark"></i>
          </button>
        </div>

        <!-- User Prompt Area -->
        <div class="prompt-box mt-4">
          <label class="form-label">{{ t('Custom AI Instructions (Optional)', 'Định hướng phân tích (Không bắt buộc)') }}</label>
          <el-input
            v-model="userPrompt"
            type="textarea"
            :rows="3"
            :placeholder="t('e.g., Focus on backend API tasks, create an estimate for each item, write task descriptions in English...', 'VD: Hãy tập trung vào phần API Backend, giải thích chi tiết các bước thiết lập, viết mô tả bằng tiếng Anh...')"
            :disabled="loading"
            resize="none"
          />
        </div>

        <!-- Warning Alert -->
        <div class="warning-alert mt-4">
          <i class="fa-solid fa-triangle-exclamation warning-icon"></i>
          <span class="warning-text">
            {{ t('AI models can make mistakes. Please verify task titles, descriptions, and assignees carefully before creating items.', 'AI có thể trả về thông tin chưa chính xác. Vui lòng kiểm tra kỹ tiêu đề, mô tả và người thực hiện trước khi tạo công việc thực tế.') }}
          </span>
        </div>

        <!-- Action Button -->
        <button 
          class="nexus-btn-primary w-full mt-4 analyze-btn"
          :disabled="loading || !selectedFile"
          @click="startAiAnalysis"
        >
          <i v-if="loading" class="fa-solid fa-spinner fa-spin mr-2"></i>
          <i v-else class="fa-solid fa-brain mr-2"></i>
          {{ loading ? t('AI is Analyzing Document...', 'AI đang phân tích tài liệu...') : t('Analyze with SprintA AI', 'Phân tích bằng SprintA AI') }}
        </button>
      </div>

      <!-- Right column: Analysis Results & Suggested Tasks -->
      <div class="results-panel">
        <!-- Empty State -->
        <div v-if="!analysisResult && !loading" class="empty-state-card card">
          <div class="empty-state-content">
            <i class="fa-solid fa-file-invoice empty-icon"></i>
            <h4>{{ t('No Active Analysis', 'Chưa có tài liệu phân tích') }}</h4>
            <p>{{ t('Select and upload a project scope, requirements document, or diagram image to generate a detailed planning backlog.', 'Chọn và tải lên tài liệu mô tả yêu cầu, file Excel tiến độ hoặc hình ảnh thiết kế để bắt đầu phân tích lập kế hoạch.') }}</p>
          </div>
        </div>

        <!-- Loading State Skeleton -->
        <div v-if="loading && !analysisResult" class="skeleton-card card">
          <div class="skeleton-header"></div>
          <div class="skeleton-line short"></div>
          <div class="skeleton-line"></div>
          <div class="skeleton-line medium"></div>
          <div class="skeleton-line"></div>
        </div>

        <!-- Analysis Outputs -->
        <div v-if="analysisResult" class="results-container">
          <!-- Summary Card -->
          <div class="card analysis-summary-card">
            <h3 class="section-title text-accent">
              <i class="fa-solid fa-clipboard-list mr-2"></i>
              {{ t('Executive Summary', 'Tóm tắt nội dung & Mục tiêu') }}
            </h3>
            <p class="summary-text">{{ analysisResult.summary }}</p>

            <div class="grid-2-col mt-4">
              <!-- Key Points -->
              <div class="bullet-list-section">
                <h4 class="sub-section-title">
                  <i class="fa-solid fa-circle-check text-success mr-2"></i>
                  {{ t('Key Deliverables', 'Ý chính / Yêu cầu cốt lõi') }}
                </h4>
                <ul class="bullet-list">
                  <li v-for="(point, idx) in analysisResult.keyPoints" :key="idx">{{ point }}</li>
                </ul>
              </div>

              <!-- Risks -->
              <div class="bullet-list-section">
                <h4 class="sub-section-title">
                  <i class="fa-solid fa-shield-halved text-danger mr-2"></i>
                  {{ t('Identified Risks & Constraints', 'Rủi ro & Điểm cần lưu ý') }}
                </h4>
                <ul class="bullet-list">
                  <li v-for="(risk, idx) in analysisResult.risks" :key="idx" class="text-danger-light">{{ risk }}</li>
                </ul>
              </div>
            </div>
          </div>

          <!-- Suggested Tasks Card (Interactive Work Items Creation) -->
          <div class="card mt-4 tasks-planning-card">
            <div class="flex justify-between items-center mb-4 flex-wrap gap-2">
              <h3 class="section-title" style="margin: 0;">
                <i class="fa-solid fa-list-check text-accent mr-2"></i>
                {{ t('Suggested Work Items', 'Danh sách công việc đề xuất') }}
              </h3>
              <div class="flex gap-2 items-center">
                <span class="text-xs text-[var(--color-text-muted)]">
                  {{ t('Selected:', 'Đã chọn:') }} <strong>{{ selectedTasksToCreate.length }} / {{ analysisResult.suggestedTasks.length }}</strong>
                </span>
                <el-button 
                  type="success" 
                  size="default" 
                  :disabled="!selectedTasksToCreate.length || creatingTasks"
                  @click="createSelectedTasks"
                >
                  <i v-if="creatingTasks" class="fa-solid fa-spinner fa-spin mr-1"></i>
                  <i v-else class="fa-solid fa-circle-plus mr-1"></i>
                  {{ t('Create in SprintA', 'Tạo trong SprintA') }}
                </el-button>
              </div>
            </div>

            <!-- Editable Tasks Table -->
            <div class="table-container">
              <table class="tasks-table">
                <thead>
                  <tr>
                    <th width="40" class="text-center">
                      <el-checkbox v-model="allTasksChecked" @change="toggleSelectAllTasks" />
                    </th>
                    <th>{{ t('Title', 'Tiêu đề công việc') }}</th>
                    <th>{{ t('Description', 'Chi tiết công việc') }}</th>
                    <th width="120">{{ t('Priority', 'Ưu tiên') }}</th>
                    <th width="180">{{ t('Assignee', 'Người thực hiện') }}</th>
                    <th width="140">{{ t('Due Date', 'Hạn chót') }}</th>
                  </tr>
                </thead>
                <tbody>
                  <tr 
                    v-for="(task, idx) in analysisResult.suggestedTasks" 
                    :key="idx"
                    :class="{ 'row-selected': task.isChecked }"
                  >
                    <td class="text-center">
                      <el-checkbox v-model="task.isChecked" @change="updateSelectionCount" />
                    </td>
                    <td>
                      <el-input v-model="task.title" size="small" placeholder="Tiêu đề..." />
                    </td>
                    <td>
                      <el-input 
                        v-model="task.description" 
                        type="textarea" 
                        :rows="1" 
                        autosize 
                        size="small" 
                        placeholder="Mô tả công việc..." 
                      />
                    </td>
                    <td>
                      <el-select v-model="task.priority" size="small" placeholder="Ưu tiên">
                        <el-option :value="1" label="Urgent" />
                        <el-option :value="2" label="High" />
                        <el-option :value="3" label="Medium" />
                        <el-option :value="4" label="Low" />
                      </el-select>
                    </td>
                    <td>
                      <el-select v-model="task.assigneeEmail" size="small" placeholder="Chọn member" clearable>
                        <el-option 
                          v-for="member in projectMembers" 
                          :key="member.id" 
                          :value="member.email" 
                          :label="member.fullName || member.email" 
                        />
                      </el-select>
                    </td>
                    <td>
                      <el-date-picker
                        v-model="task.dueDate"
                        type="date"
                        size="small"
                        format="YYYY-MM-DD"
                        value-format="YYYY-MM-DD"
                        placeholder="Chọn ngày"
                        style="width: 100%;"
                      />
                    </td>
                  </tr>
                  <tr v-if="!analysisResult.suggestedTasks.length">
                    <td colspan="6" class="no-data-cell">
                      {{ t('No tasks suggested by AI.', 'AI không đề xuất công việc nào từ tài liệu.') }}
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <!-- Refining / AI Follow-up Chat Card -->
          <div class="card mt-4 refining-chat-card">
            <h3 class="section-title text-accent">
              <i class="fa-solid fa-comments-dollar mr-2"></i>
              {{ t('Refine Requirements', 'Hiệu chỉnh yêu cầu của bạn') }}
            </h3>
            <p class="text-xs text-[var(--color-text-muted)] mb-3">
              {{ t('Ask AI to adjust the plan, emphasize specific tasks, or generate sub-tasks based on new requirements without re-uploading.', 'Yêu cầu AI cập nhật lại kế hoạch, định dạng lại các task, tập trung vào mô hình khác... mà không cần tải lại file.') }}
            </p>
            <div class="flex gap-2">
              <el-input
                v-model="refinementPrompt"
                type="textarea"
                :rows="2"
                :placeholder="t('e.g., Rewrite description in detail, change priority of design task to High...', 'VD: Hãy viết mô tả công việc chi tiết hơn, đổi độ ưu tiên các task frontend lên Cao...')"
                :disabled="loading"
                resize="none"
              />
              <button 
                class="nexus-btn-primary refine-submit-btn" 
                :disabled="loading || !refinementPrompt.trim()"
                @click="submitRefinement"
              >
                <i v-if="loading" class="fa-solid fa-spinner fa-spin"></i>
                <i v-else class="fa-solid fa-paper-plane"></i>
              </button>
            </div>
          </div>

          <div class="grid-2-col mt-4">
            <!-- Coding Prompts Suggestions -->
            <div class="card coding-prompts-card" v-if="analysisResult.suggestedPrompts && analysisResult.suggestedPrompts.length">
              <h3 class="section-title">
                <i class="fa-solid fa-code text-accent mr-2"></i>
                {{ t('Coding Agent Prompts', 'Prompt lập trình gợi ý') }}
              </h3>
              <p class="text-xs text-[var(--color-text-muted)] mb-3">
                {{ t('Copy these optimized prompts to use with AI Coding Agents (Codex, Antigravity, Claude) to build this feature.', 'Sao chép các câu lệnh tối ưu hóa dưới đây để đưa cho AI Code Agent (Codex, Antigravity) lập trình chức năng tương ứng.') }}
              </p>
              <div 
                v-for="(promptText, idx) in analysisResult.suggestedPrompts" 
                :key="idx"
                class="prompt-snippet"
              >
                <div class="snippet-header">
                  <span class="snippet-label">Prompt #{{ idx + 1 }}</span>
                  <button class="copy-btn" @click="copyToClipboard(promptText)">
                    <i class="fa-regular fa-copy"></i>
                  </button>
                </div>
                <pre class="snippet-body"><code>{{ promptText }}</code></pre>
              </div>
            </div>

            <!-- Clarifying Questions -->
            <div class="card questions-card" v-if="analysisResult.questions && analysisResult.questions.length">
              <h3 class="section-title">
                <i class="fa-solid fa-circle-question text-accent mr-2"></i>
                {{ t('Clarifying Questions', 'Câu hỏi làm rõ phạm vi') }}
              </h3>
              <p class="text-xs text-[var(--color-text-muted)] mb-3">
                {{ t('AI found these ambiguities in your documentation. Consider addressing them in your planning meeting.', 'AI phát hiện một số điểm mơ hồ trong tài liệu. Hãy thảo luận để làm rõ các điểm này.') }}
              </p>
              <ul class="question-list">
                <li v-for="(question, idx) in analysisResult.questions" :key="idx" class="question-item">
                  <i class="fa-solid fa-lightbulb text-warning mr-2"></i>
                  <span>{{ question }}</span>
                </li>
              </ul>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useProjectStore } from '@/store/useProjectStore'
import { useI18n } from '@/composables/useI18n'
import axiosClient from '@/api/axiosClient'
import { ElMessage } from 'element-plus'

const route = useRoute()
const router = useRouter()
const projectStore = useProjectStore()
const { t } = useI18n()

const projectId = computed(() => route.params.id)

// File upload refs
const fileInputRef = ref(null)
const selectedFile = ref(null)
const isDragOver = ref(false)
const userPrompt = ref('')
const refinementPrompt = ref('')
const loading = ref(false)

// Analysis output refs
const analysisResult = ref(null)
const selectedTasksToCreate = ref([])
const allTasksChecked = ref(true)
const creatingTasks = ref(false)

// Project metadata
const projectMembers = ref([])

onMounted(async () => {
  const pid = projectId.value
  if (pid && pid !== '00000000-0000-0000-0000-000000000000' && pid !== 'undefined' && pid !== 'null' && pid !== '1') {
    try {
      const memRes = await axiosClient.get(`/projects/${pid}/members`)
      projectMembers.value = memRes.data?.data || []
    } catch (e) {
      console.error('Lỗi nạp danh sách thành viên dự án:', e)
      projectMembers.value = []
    }
  }
})

// File handlers
function triggerFileSelect() {
  if (loading.value) return
  fileInputRef.value?.click()
}

function handleFileSelect(event) {
  const file = event.target.files?.[0]
  if (file) {
    validateAndSetFile(file)
  }
}

function handleFileDrop(event) {
  isDragOver.value = false
  if (loading.value) return
  const file = event.dataTransfer?.files?.[0]
  if (file) {
    validateAndSetFile(file)
  }
}

function validateAndSetFile(file) {
  const allowedExtensions = ['.txt', '.md', '.docx', '.pdf']
  const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase()
  
  if (!allowedExtensions.includes(ext)) {
    ElMessage.error(t('Only .txt, .md, .docx, and .pdf documents are supported. Use Import Excel/CSV for tabular data.', 'Chỉ hỗ trợ tài liệu .txt, .md, .docx và .pdf. Hãy dùng Import Excel/CSV cho dữ liệu bảng.'))
    return
  }

  if (file.size > 10 * 1024 * 1024) {
    ElMessage.error(t('File size exceeds the 10MB limit.', 'Dung lượng file vượt quá giới hạn 10MB.'))
    return
  }

  selectedFile.value = file
  analysisResult.value = null
  userPrompt.value = ''
  refinementPrompt.value = ''
}

function removeSelectedFile() {
  selectedFile.value = null
  analysisResult.value = null
  userPrompt.value = ''
  refinementPrompt.value = ''
}

// AI Integration
async function startAiAnalysis() {
  if (!selectedFile.value) return
  
  loading.value = true
  analysisResult.value = null
  
  const formData = new FormData()
  formData.append('file', selectedFile.value)
  const pid = projectId.value
  if (pid && pid !== '00000000-0000-0000-0000-000000000000' && pid !== 'undefined' && pid !== 'null' && pid !== '1') {
    formData.append('projectId', pid)
  }
  if (userPrompt.value.trim()) {
    formData.append('userPrompt', userPrompt.value)
  }

  try {
    const res = await axiosClient.post('/ai/analyze-file', formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    
    if (res.data?.data) {
      const data = res.data.data
      // Process tasks for Vue binding (add checked attribute)
      if (data.suggestedTasks) {
        data.suggestedTasks = data.suggestedTasks.map(task => ({
          ...task,
          isChecked: true
        }))
      }
      analysisResult.value = data
      updateSelectionCount()
    } else {
      ElMessage.error(t('AI Analysis failed. Please try again.', 'AI không thể trả về kết quả phân tích.'))
    }
  } catch (e) {
    ElMessage.error(e.response?.data?.message || t('Error calling AI service.', 'Lỗi kết nối máy chủ dịch vụ AI.'))
  } finally {
    loading.value = false
  }
}

async function submitRefinement() {
  if (!selectedFile.value || !refinementPrompt.value.trim()) return

  const originalResult = JSON.parse(JSON.stringify(analysisResult.value))
  // Strip UI checkboxes to keep payload small
  if (originalResult.suggestedTasks) {
    originalResult.suggestedTasks.forEach(task => delete task.isChecked)
  }

  loading.value = true
  
  const formData = new FormData()
  formData.append('file', selectedFile.value)
  const pid = projectId.value
  if (pid && pid !== '00000000-0000-0000-0000-000000000000' && pid !== 'undefined' && pid !== 'null' && pid !== '1') {
    formData.append('projectId', pid)
  }
  formData.append('userPrompt', refinementPrompt.value)
  formData.append('previousAnalysisJson', JSON.stringify(originalResult))

  try {
    const res = await axiosClient.post('/ai/analyze-file', formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })

    if (res.data?.data) {
      const data = res.data.data
      if (data.suggestedTasks) {
        data.suggestedTasks = data.suggestedTasks.map(task => ({
          ...task,
          isChecked: true
        }))
      }
      analysisResult.value = data
      refinementPrompt.value = ''
      updateSelectionCount()
      ElMessage.success(t('AI updated planning successfully.', 'AI cập nhật kế hoạch thành công.'))
    } else {
      ElMessage.error(t('AI Update failed.', 'AI không thể hiệu chỉnh kế hoạch.'))
    }
  } catch (e) {
    ElMessage.error(e.response?.data?.message || t('Error calling AI service.', 'Lỗi kết nối máy chủ dịch vụ AI.'))
  } finally {
    loading.value = false
  }
}

// Selection triggers
function toggleSelectAllTasks(val) {
  if (analysisResult.value?.suggestedTasks) {
    analysisResult.value.suggestedTasks.forEach(task => {
      task.isChecked = val
    })
    updateSelectionCount()
  }
}

function updateSelectionCount() {
  if (!analysisResult.value?.suggestedTasks) {
    selectedTasksToCreate.value = []
    return
  }

  selectedTasksToCreate.value = analysisResult.value.suggestedTasks.filter(t => t.isChecked)
  allTasksChecked.value = selectedTasksToCreate.value.length === analysisResult.value.suggestedTasks.length
}

// WorkTask creations
async function createSelectedTasks() {
  const pid = projectId.value
  if (!pid || pid === '00000000-0000-0000-0000-000000000000' || pid === 'undefined' || pid === 'null' || pid === '1') {
    ElMessage.error(t('Project ID is invalid.', 'ID dự án không hợp lệ.'))
    return
  }

  if (!selectedTasksToCreate.value.length) return

  creatingTasks.value = true
  let successCount = 0
  let errorCount = 0

  for (const task of selectedTasksToCreate.value) {
    try {
      const assignee = projectMembers.value.find(m => m.email?.toLowerCase() === task.assigneeEmail?.toLowerCase())
      
      const payload = {
        title: task.title.trim(),
        description: task.description || '',
        statusName: 'TO DO',
        priority: task.priority || 3,
        dueDate: task.dueDate || null,
        assignedUserId: assignee?.userId || assignee?.id || null,
        assigneeIds: assignee ? [assignee.userId || assignee.id] : []
      }

      await axiosClient.post(`/projects/${pid}/WorkTasks`, payload)
      successCount++
    } catch (e) {
      console.error('Lỗi khi tạo task:', task.title, e)
      errorCount++
    }
  }

  creatingTasks.value = false
  if (successCount > 0) {
    ElMessage.success(`Tạo thành công ${successCount} công việc thực tế trong SprintA.`)
    // Redirect to task list
    router.push({ name: 'SpaceSummary', params: { id: projectId.value } })
  }
  if (errorCount > 0) {
    ElMessage.error(`Có ${errorCount} công việc gặp lỗi không thể khởi tạo.`)
  }
}

// UI Helpers
function getFileIcon(filename) {
  const ext = filename.substring(filename.lastIndexOf('.')).toLowerCase()
  switch (ext) {
    case '.txt':
    case '.md':
      return 'fa-file-lines text-blue-400'
    case '.json':
      return 'fa-file-code text-yellow-400'
    case '.csv':
    case '.xlsx':
      return 'fa-file-excel text-green-400'
    case '.docx':
      return 'fa-file-word text-blue-500'
    case '.pptx':
      return 'fa-file-powerpoint text-orange-400'
    case '.pdf':
      return 'fa-file-pdf text-red-400'
    case '.png':
    case '.jpg':
    case '.jpeg':
    case '.webp':
      return 'fa-file-image text-purple-400'
    default:
      return 'fa-file-invoice'
  }
}

function formatBytes(bytes, decimals = 2) {
  if (!+bytes) return '0 Bytes'
  const k = 1024
  const dm = decimals < 0 ? 0 : decimals
  const sizes = ['Bytes', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${sizes[i]}`
}

function copyToClipboard(text) {
  navigator.clipboard.writeText(text)
  ElMessage.success(t('Copied to clipboard!', 'Đã sao chép vào bộ nhớ tạm!'))
}
</script>

<style scoped>
.ai-file-intake-container {
  width: 100%;
  height: 100%;
  min-height: 0;
  padding: 0;
  max-width: none;
  margin: 0;
  overflow-y: auto;
  box-sizing: border-box;
}

.header-section {
  margin-bottom: 24px;
}

.page-title {
  font-size: 22px;
  font-weight: 700;
  color: var(--color-text-primary);
  display: flex;
  align-items: center;
  margin: 0 0 6px 0;
}

.page-subtitle {
  font-size: 13px;
  color: var(--color-text-muted);
  margin: 0;
}

.main-content-grid {
  display: grid;
  grid-template-columns: 340px 1fr;
  gap: 20px;
  align-items: start;
}

/* Card basic styling matching theme */
.card {
  background: color-mix(in srgb, var(--color-surface) 90%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-border) 60%, transparent);
  border-radius: 12px;
  padding: 20px;
  backdrop-filter: blur(10px);
}

.panel-title {
  font-size: 15px;
  font-weight: 600;
  margin: 0 0 16px 0;
  color: var(--color-text-primary);
}

.upload-dropzone {
  border: 2px dashed color-mix(in srgb, var(--color-border) 70%, transparent);
  border-radius: 8px;
  padding: 24px 16px;
  text-align: center;
  cursor: pointer;
  transition: all 0.2s ease;
  background: color-mix(in srgb, var(--color-bg) 40%, transparent);
}

.upload-dropzone:hover, .upload-dropzone.is-dragover {
  border-color: var(--color-accent);
  background: color-mix(in srgb, var(--color-accent) 5%, transparent);
}

.drop-icon {
  font-size: 32px;
  color: var(--color-text-muted);
  margin-bottom: 12px;
}

.drop-text-primary {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0 0 4px 0;
}

.drop-text-secondary {
  font-size: 11px;
  color: var(--color-text-muted);
  margin: 0;
}

.hidden-input {
  display: none;
}

.selected-file-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  background: color-mix(in srgb, var(--color-surface) 80%, transparent);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  margin-top: 12px;
}

.file-meta {
  display: flex;
  align-items: center;
  gap: 10px;
}

.file-meta i {
  font-size: 20px;
}

.file-details {
  display: flex;
  flex-direction: column;
}

.file-name {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-primary);
  max-width: 180px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.file-size {
  font-size: 10px;
  color: var(--color-text-muted);
}

.remove-file-btn {
  background: transparent;
  border: none;
  color: var(--color-text-muted);
  cursor: pointer;
  padding: 4px;
  font-size: 14px;
  transition: color 0.15s;
}

.remove-file-btn:hover {
  color: #f87171;
}

.form-label {
  display: block;
  font-size: 12px;
  font-weight: 600;
  margin-bottom: 6px;
  color: var(--color-text-primary);
}

.warning-alert {
  display: flex;
  gap: 8px;
  padding: 10px 12px;
  background: rgba(245, 158, 11, 0.08);
  border: 1px solid rgba(245, 158, 11, 0.2);
  border-radius: 8px;
}

.warning-icon {
  color: #f59e0b;
  font-size: 14px;
  flex-shrink: 0;
  margin-top: 2px;
}

.warning-text {
  font-size: 11px;
  color: #d97706;
  line-height: 1.4;
}

.analyze-btn {
  height: 40px;
  font-size: 13px;
  font-weight: 600;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* Empty State Card */
.empty-state-card {
  padding: 48px 24px;
  text-align: center;
}

.empty-state-content {
  max-width: 420px;
  margin: 0 auto;
}

.empty-icon {
  font-size: 48px;
  color: var(--color-text-muted);
  margin-bottom: 16px;
}

.empty-state-content h4 {
  font-size: 16px;
  font-weight: 600;
  margin: 0 0 8px 0;
  color: var(--color-text-primary);
}

.empty-state-content p {
  font-size: 12px;
  color: var(--color-text-muted);
  margin: 0;
}

/* Skeleton Loading */
.skeleton-card {
  padding: 24px;
}

.skeleton-header {
  height: 24px;
  background: rgba(148, 163, 184, 0.12);
  border-radius: 4px;
  margin-bottom: 16px;
  width: 200px;
}

.skeleton-line {
  height: 12px;
  background: rgba(148, 163, 184, 0.08);
  border-radius: 4px;
  margin-bottom: 10px;
}

.skeleton-line.medium {
  width: 80%;
}

.skeleton-line.short {
  width: 50%;
}

/* Results Content */
.results-container {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.section-title {
  font-size: 16px;
  font-weight: 700;
  margin: 0 0 12px 0;
  color: var(--color-text-primary);
}

.summary-text {
  font-size: 13px;
  line-height: 1.6;
  color: var(--color-text-primary);
  margin: 0;
}

.grid-2-col {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
}

.bullet-list-section {
  display: flex;
  flex-direction: column;
}

.sub-section-title {
  font-size: 13px;
  font-weight: 600;
  margin: 0 0 8px 0;
  color: var(--color-text-primary);
}

.bullet-list {
  margin: 0;
  padding-left: 20px;
  font-size: 12px;
  line-height: 1.6;
  color: var(--color-text-primary);
}

.bullet-list li {
  margin-bottom: 4px;
}

.text-danger-light {
  color: #ef4444;
}

/* Tables styling */
.table-container {
  overflow-x: auto;
  border: 1px solid var(--color-border);
  border-radius: 8px;
}

.tasks-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  font-size: 12px;
}

.tasks-table th {
  background: color-mix(in srgb, var(--color-surface) 95%, transparent);
  padding: 10px 12px;
  font-weight: 600;
  color: var(--color-text-primary);
  border-bottom: 1px solid var(--color-border);
}

.tasks-table td {
  padding: 8px 12px;
  border-bottom: 1px solid var(--color-border);
  vertical-align: middle;
}

.tasks-table tr:last-child td {
  border-bottom: none;
}

.row-selected {
  background: color-mix(in srgb, var(--color-accent) 4%, transparent);
}

.no-data-cell {
  text-align: center;
  padding: 24px;
  color: var(--color-text-muted);
}

/* Prompt snippet */
.prompt-snippet {
  border: 1px solid var(--color-border);
  border-radius: 8px;
  margin-bottom: 12px;
  overflow: hidden;
}

.snippet-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 6px 12px;
  background: color-mix(in srgb, var(--color-surface) 95%, transparent);
  border-bottom: 1px solid var(--color-border);
}

.snippet-label {
  font-size: 11px;
  font-weight: 600;
  color: var(--color-text-muted);
}

.copy-btn {
  background: transparent;
  border: none;
  color: var(--color-text-muted);
  cursor: pointer;
  padding: 2px 4px;
  font-size: 13px;
  transition: color 0.15s;
}

.copy-btn:hover {
  color: var(--color-accent);
}

.snippet-body {
  margin: 0;
  padding: 10px 12px;
  background: #020617;
  max-height: 120px;
  overflow-y: auto;
}

.snippet-body code {
  font-family: var(--font-mono, monospace);
  font-size: 11px;
  color: #38bdf8;
  white-space: pre-wrap;
  word-break: break-all;
}

/* Clarifying questions */
.question-list {
  list-style: none;
  padding: 0;
  margin: 0;
}

.question-item {
  display: flex;
  gap: 8px;
  padding: 8px 12px;
  background: color-mix(in srgb, var(--color-surface) 80%, transparent);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  margin-bottom: 8px;
  font-size: 12px;
  color: var(--color-text-primary);
}

.refine-submit-btn {
  width: 44px;
  height: 44px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

@media (max-width: 1024px) {
  .main-content-grid {
    grid-template-columns: 1fr;
  }
}
</style>
