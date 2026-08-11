<script setup>
import { computed, ref } from 'vue'
import axiosClient from '@/api/axiosClient'
import { ElMessage } from 'element-plus'
import { parseCSVText, processRawRows, buildTaskPayload } from '@/utils/taskImportParser'
import { downloadResponseFile } from '@/utils/downloadFile'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'

const props = defineProps({ projectId: { type: String, required: true }, projectMembers: { type: Array, default: () => [] }, projectStatuses: { type: Array, default: () => [] } })
const emit = defineEmits(['imported'])
const dialogOpen = ref(false); const rows = ref([]); const validRows = ref([]); const invalidRows = ref([]); const input = ref(null)
const exporting = ref(false); const importing = ref(false); const fileError = ref('')
const template = [['Tiêu đề công việc', 'Mô tả', 'Trạng thái', 'Email người phụ trách', 'Ưu tiên', 'Story Points', 'Ngày bắt đầu', 'Hạn hoàn thành', 'Ước lượng giờ'], ['Thiết kế dashboard', 'Thiết kế màn hình thống kê', 'TO DO', '', 'Cao', '5', '01/07/2026', '31/07/2026', '8']]

const reset = () => { rows.value = []; validRows.value = []; invalidRows.value = []; fileError.value = '' }
const open = () => { reset(); dialogOpen.value = true }
const csv = (data) => `\uFEFF${data.map((row) => row.map((value) => `"${String(value ?? '').replace(/"/g, '""')}"`).join(',')).join('\r\n')}`
function downloadTemplate() {
  const blob = new Blob([csv(template)], { type: 'text/csv;charset=utf-8' }); const href = URL.createObjectURL(blob); const link = document.createElement('a')
  link.href = href; link.download = 'SprintA_Task_Template_DD-MM-YYYY.csv'; link.click(); URL.revokeObjectURL(href)
  ElMessage.success('Đã tải template CSV UTF-8.')
}
function readFile(event) {
  const file = event.target.files?.[0]; if (!file) return
  const reader = new FileReader(); reader.onload = () => {
    const parsed = processRawRows(parseCSVText(reader.result), props.projectMembers, props.projectStatuses)
    fileError.value = parsed.headerError || ''; rows.value = parsed.rows; validRows.value = parsed.rows.filter((row) => !row.error); invalidRows.value = parsed.rows.filter((row) => row.error)
    event.target.value = ''
  }; reader.onerror = () => { fileError.value = 'Không thể đọc file CSV.' }; reader.readAsText(file, 'UTF-8')
}
async function confirmImport() {
  if (!validRows.value.length || importing.value) return
  importing.value = true
  try { await axiosClient.post(`/projects/${props.projectId}/WorkTasks/import`, validRows.value.map((row) => buildTaskPayload(row, props.projectMembers))); ElMessage.success(`Đã nhập ${validRows.value.length} công việc.`); dialogOpen.value = false; emit('imported') }
  catch (error) { ElMessage.error(error.response?.data?.message || 'Không thể nhập công việc.') }
  finally { importing.value = false }
}
async function exportTasks() {
  if (exporting.value) return; exporting.value = true
  try { const response = await axiosClient.get(`/projects/${props.projectId}/WorkTasks/export`, { responseType: 'blob' }); downloadResponseFile(response, `SprintA-WorkItems-${props.projectId}.csv`, 'text/csv;charset=utf-8'); ElMessage.success('Đã tải file công việc.') }
  catch (error) { ElMessage.error(error.response?.data?.message || 'Không thể xuất công việc.') }
  finally { exporting.value = false }
}
</script>

<template>
  <div class="task-import-export-panel">
    <el-button-group>
      <el-button plain @click="open">⇧ Nhập CSV</el-button>
      <el-button plain :loading="exporting" @click="exportTasks">⇩ Xuất CSV</el-button>
    </el-button-group>
    <el-dialog v-model="dialogOpen" width="min(920px, calc(100vw - 32px))" append-to-body destroy-on-close class="task-import-dialog sa-data-dialog sa-modal--lg" :show-close="false">
      <template #header>
        <DataModalHeader icon="bi bi-filetype-csv" title="Nhập công việc" description="Kiểm tra file CSV trước khi tạo công việc hàng loạt" @close="dialogOpen = false" />
      </template>
      <div class="import-toolbar"><span>Ngày dùng định dạng DD/MM/YYYY. Hệ thống sẽ chuẩn hóa sang ISO trước khi gửi.</span><el-button size="small" @click="downloadTemplate">Tải template</el-button></div>
      <label class="upload-zone"><input ref="input" type="file" accept=".csv,text/csv" @change="readFile" /> <span>Chọn CSV UTF-8 để xem trước</span></label>
      <el-alert v-if="fileError" type="error" :title="fileError" show-icon :closable="false" />
      <template v-if="rows.length">
        <div class="import-summary"><b>{{ rows.length }} dòng</b><el-tag type="success">{{ validRows.length }} hợp lệ</el-tag><el-tag type="danger">{{ invalidRows.length }} lỗi</el-tag></div>
        <div class="import-table-wrap"><el-table :data="rows" size="small" stripe>
          <el-table-column prop="rowNum" label="Dòng" width="70" /><el-table-column prop="title" label="Tiêu đề" min-width="190" /><el-table-column prop="status" label="Trạng thái" width="120" /><el-table-column prop="startDate" label="Bắt đầu" width="120" /><el-table-column prop="dueDate" label="Hạn" width="120" /><el-table-column label="Kết quả" min-width="260"><template #default="{ row }"><el-tag v-if="row.error" type="danger">{{ row.error }}</el-tag><el-tag v-else type="success">Sẵn sàng nhập</el-tag></template></el-table-column>
        </el-table></div>
      </template>
      <template #footer><el-button @click="dialogOpen = false">Hủy</el-button><el-button type="primary" :disabled="!validRows.length" :loading="importing" @click="confirmImport">Xác nhận nhập ({{ validRows.length }})</el-button></template>
    </el-dialog>
  </div>
</template>

<style scoped>
.task-import-export-panel{display:inline-flex}.import-toolbar,.import-summary{display:flex;align-items:center;justify-content:space-between;gap:12px;margin-bottom:16px;color:var(--color-text-secondary)}.upload-zone{display:flex;justify-content:center;align-items:center;min-height:110px;border:1px dashed var(--color-border);border-radius:12px;background:var(--color-surface-muted);cursor:pointer;margin-bottom:16px;color:var(--color-text-secondary)}.upload-zone input{display:none}.import-table-wrap{max-height:420px;overflow:auto;border:1px solid var(--color-border);border-radius:10px}.import-summary{justify-content:flex-start}.import-summary b{margin-right:auto}@media(max-width:600px){.import-toolbar{align-items:flex-start;flex-direction:column}.task-import-export-panel{max-width:100%}.task-import-export-panel .el-button{padding-inline:10px}}
:deep(.task-import-dialog .el-dialog),:deep(.task-import-dialog .el-dialog__body),:deep(.task-import-dialog .el-dialog__footer){background:var(--color-surface)!important;color:var(--color-text-primary)}
:deep(.task-import-dialog .el-table),:deep(.task-import-dialog .el-table th.el-table__cell),:deep(.task-import-dialog .el-table tr),:deep(.task-import-dialog .el-table td.el-table__cell){background:var(--color-surface)!important;color:var(--color-text-primary);border-color:var(--color-border)}
</style>
