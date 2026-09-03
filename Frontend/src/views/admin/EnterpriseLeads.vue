<template>
  <AdminLayout>
    <div class="leads-page">
      <header class="page-header">
        <div>
          <p class="breadcrumb">SALES / ENTERPRISE LEADS</p>
          <h1 class="text-hero">Enterprise leads</h1>
          <p class="text-desc">Theo dõi, phân công và cập nhật các yêu cầu tư vấn Enterprise.</p>
        </div>
        <button type="button" class="refresh-button" :disabled="loading" @click="loadLeads">Làm mới</button>
      </header>

      <section class="toolbar" aria-label="Bộ lọc lead">
        <input v-model="search" type="search" placeholder="Tìm tên, email, công ty" @input="scheduleSearch" />
        <select v-model="statusFilter" @change="loadLeads"><option value="">Tất cả trạng thái</option><option v-for="status in statuses" :key="status.value" :value="status.value">{{ status.label }}</option></select>
      </section>

      <div v-if="errorMessage" class="error-state" role="alert">{{ errorMessage }}</div>
      <section v-loading="loading" class="leads-surface">
        <div v-if="!loading && !leads.length" class="empty-state">Chưa có yêu cầu Enterprise phù hợp.</div>
        <table v-else class="leads-table">
          <thead><tr><th>Liên hệ</th><th>Công ty</th><th>Quy mô</th><th>Trạng thái</th><th>Ngày tạo</th><th></th></tr></thead>
          <tbody><tr v-for="lead in leads" :key="lead.id" @click="openDetail(lead.id)"><td><strong>{{ lead.contactName }}</strong><small>{{ lead.workEmail }}</small></td><td>{{ lead.company }}</td><td>{{ lead.teamSize }}</td><td><span class="status-pill" :class="lead.status">{{ statusLabel(lead.status) }}</span></td><td>{{ formatDate(lead.createdAt) }}</td><td><button type="button" class="detail-link" @click.stop="openDetail(lead.id)">Mở</button></td></tr></tbody>
        </table>
        <div v-if="total > pageSize" class="pagination"><button type="button" :disabled="page === 1" @click="page--; loadLeads()">Trước</button><span>Trang {{ page }} · {{ total }} lead</span><button type="button" :disabled="page * pageSize >= total" @click="page++; loadLeads()">Sau</button></div>
      </section>
    </div>

    <div v-if="selectedLead" class="detail-overlay" role="presentation" @click.self="selectedLead = null">
      <section class="detail-panel" role="dialog" aria-modal="true" aria-labelledby="lead-detail-title">
        <button type="button" class="close-button" aria-label="Đóng" @click="selectedLead = null">×</button>
        <p class="breadcrumb">ENTERPRISE LEAD</p>
        <h2 id="lead-detail-title">{{ selectedLead.contactName }}</h2>
        <p class="company-line">{{ selectedLead.company }} · {{ selectedLead.workEmail }}</p>
        <dl class="lead-facts"><div><dt>Điện thoại / Zalo</dt><dd>{{ selectedLead.phoneOrZalo || '—' }}</dd></div><div><dt>Quy mô</dt><dd>{{ selectedLead.teamSize }}</dd></div><div><dt>Nhu cầu</dt><dd>{{ selectedLead.need || '—' }}</dd></div><div><dt>Thời gian liên hệ</dt><dd>{{ selectedLead.preferredContactTime || '—' }}</dd></div><div><dt>Ghi chú khách hàng</dt><dd>{{ selectedLead.notes || '—' }}</dd></div><div><dt>Tạo lúc</dt><dd>{{ formatDate(selectedLead.createdAt) }}</dd></div></dl>
        <div class="admin-fields"><label>Trạng thái<select v-model="edit.status"><option v-for="status in statuses" :key="status.value" :value="status.value">{{ status.label }}</option></select></label><label>Người phụ trách<select v-model="edit.assignedToUserId"><option value="">Chưa phân công</option><option v-for="user in admins" :key="user.id" :value="user.id">{{ user.name || user.fullName || user.email }}</option></select></label><label>Ghi chú nội bộ<textarea v-model="edit.internalNote" maxlength="2000" rows="4" placeholder="Chỉ admin nhìn thấy"></textarea></label></div>
        <div class="panel-actions"><button type="button" class="cancel-button" @click="selectedLead = null">Hủy</button><button type="button" class="save-button" :disabled="saving" @click="saveDetail">{{ saving ? 'Đang lưu…' : 'Lưu thay đổi' }}</button></div>
      </section>
    </div>
  </AdminLayout>
</template>

<script setup>
import { onMounted, reactive, ref } from 'vue'
import AdminLayout from '@/components/layout/AdminLayout.vue'
import axiosClient from '@/api/axiosClient'

const statuses = [
  { value: 'New', label: 'Mới' }, { value: 'Contacted', label: 'Đã liên hệ' }, { value: 'InDiscussion', label: 'Đang trao đổi' },
  { value: 'DemoScheduled', label: 'Đã hẹn demo' }, { value: 'Won', label: 'Đã chốt' }, { value: 'Closed', label: 'Đã đóng' }
]
const leads = ref([]); const admins = ref([]); const selectedLead = ref(null); const loading = ref(false); const saving = ref(false); const errorMessage = ref(''); const search = ref(''); const statusFilter = ref(''); const page = ref(1); const pageSize = 25; const total = ref(0); let searchTimer
const edit = reactive({ status: 'New', assignedToUserId: '', internalNote: '' })
const statusLabel = (value) => statuses.find(item => item.value === value)?.label || value
const formatDate = (value) => value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '—'
const loadLeads = async () => { loading.value = true; errorMessage.value = ''; try { const response = await axiosClient.get('/admin/enterprise-leads', { params: { search: search.value || undefined, status: statusFilter.value || undefined, page: page.value, pageSize } }); leads.value = response.data?.data?.items || []; total.value = response.data?.data?.total || 0 } catch (error) { errorMessage.value = error.response?.data?.message || 'Không thể tải danh sách lead.' } finally { loading.value = false } }
const loadAdmins = async () => { try { const response = await axiosClient.get('/admin/users'); admins.value = response.data?.data || [] } catch { admins.value = [] } }
const scheduleSearch = () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => { page.value = 1; loadLeads() }, 350) }
const openDetail = async (id) => { try { const response = await axiosClient.get(`/admin/enterprise-leads/${id}`); selectedLead.value = response.data?.data; edit.status = selectedLead.value.status; edit.assignedToUserId = selectedLead.value.assignedToUserId || ''; edit.internalNote = selectedLead.value.internalNote || '' } catch (error) { errorMessage.value = error.response?.data?.message || 'Không thể tải chi tiết lead.' } }
const saveDetail = async () => { if (!selectedLead.value) return; saving.value = true; errorMessage.value = ''; try { await axiosClient.patch(`/admin/enterprise-leads/${selectedLead.value.id}`, { status: edit.status, assignedToUserId: edit.assignedToUserId || null, internalNote: edit.internalNote || null }); await loadLeads(); await openDetail(selectedLead.value.id) } catch (error) { errorMessage.value = error.response?.data?.message || 'Không thể lưu thay đổi.' } finally { saving.value = false } }
onMounted(() => { loadLeads(); loadAdmins() })
</script>

<style scoped>
.leads-page { max-width: 1240px; margin: 0 auto; }
.page-header { display:flex; justify-content:space-between; align-items:flex-start; gap:24px; margin-bottom:24px; }.breadcrumb { margin:0 0 9px; color:var(--color-text-muted); font-size:11px; font-weight:800; letter-spacing:.12em; }.text-hero { margin:0; color:var(--color-text-primary); font-size:32px; }.text-desc { margin:9px 0 0; color:var(--color-text-secondary); }.refresh-button,.save-button,.cancel-button,.detail-link { border:1px solid var(--color-border); border-radius:8px; padding:10px 14px; color:var(--color-text-primary); background:var(--color-surface); cursor:pointer; font:inherit; font-weight:700; }.save-button { border-color:#246fe8; color:#fff; background:#246fe8; }.cancel-button { background:transparent; }.toolbar { display:flex; gap:12px; margin-bottom:16px; }.toolbar input,.toolbar select,.admin-fields select,.admin-fields textarea { min-height:42px; border:1px solid var(--color-border); border-radius:9px; padding:10px 12px; color:var(--color-text-primary); background:var(--color-surface); font:inherit; }.toolbar input { flex:1; max-width:460px; }.leads-surface { overflow:hidden; border:1px solid var(--color-border); border-radius:14px; background:var(--color-surface); }.leads-table { width:100%; border-collapse:collapse; }.leads-table th,.leads-table td { padding:15px 16px; border-bottom:1px solid var(--color-border); text-align:left; font-size:13px; }.leads-table th { color:var(--color-text-muted); font-size:11px; letter-spacing:.08em; text-transform:uppercase; }.leads-table tbody tr { cursor:pointer; }.leads-table tbody tr:hover { background:rgba(45,121,230,.06); }.leads-table td:first-child { display:grid; gap:3px; }.leads-table small { color:var(--color-text-muted); }.status-pill { display:inline-flex; padding:5px 9px; border-radius:999px; color:#1764d7; background:#e8f2ff; font-size:11px; font-weight:800; }.status-pill.Closed { color:#67758a; background:#edf0f4; }.status-pill.Won { color:#087c58; background:#ddf8ec; }.detail-link { padding:6px 10px; }.empty-state { padding:48px; color:var(--color-text-muted); text-align:center; }.pagination { display:flex; justify-content:center; align-items:center; gap:16px; padding:15px; color:var(--color-text-secondary); }.pagination button { border:0; color:var(--color-primary); background:none; cursor:pointer; }.pagination button:disabled { opacity:.4; cursor:default; }.error-state { margin-bottom:16px; padding:12px 14px; border:1px solid #f0b4b4; border-radius:9px; color:#a52a2a; background:#fff5f5; }.detail-overlay { position:fixed; inset:0; z-index:50; display:flex; justify-content:flex-end; background:rgba(5,15,30,.55); }.detail-panel { width:min(560px,100%); height:100%; overflow-y:auto; padding:34px; color:var(--color-text-primary); background:var(--color-bg); box-shadow:-18px 0 50px rgba(0,0,0,.2); }.close-button { float:right; border:0; color:var(--color-text-muted); background:none; font-size:28px; cursor:pointer; }.detail-panel h2 { margin:8px 0 5px; font-size:30px; }.company-line { margin:0 0 28px; color:var(--color-text-secondary); }.lead-facts { display:grid; gap:1px; margin:0 0 28px; }.lead-facts div { padding:11px 0; border-bottom:1px solid var(--color-border); }.lead-facts dt { color:var(--color-text-muted); font-size:11px; font-weight:800; text-transform:uppercase; }.lead-facts dd { margin:4px 0 0; white-space:pre-wrap; line-height:1.5; }.admin-fields { display:grid; gap:15px; }.admin-fields label { display:grid; gap:7px; color:var(--color-text-secondary); font-size:12px; font-weight:800; }.admin-fields select,.admin-fields textarea { width:100%; }.admin-fields textarea { resize:vertical; }.panel-actions { display:flex; justify-content:flex-end; gap:10px; margin-top:24px; }
@media (max-width:760px) { .page-header { flex-direction:column; }.toolbar { flex-direction:column; }.toolbar input { max-width:none; }.leads-table th:nth-child(3),.leads-table td:nth-child(3),.leads-table th:nth-child(5),.leads-table td:nth-child(5) { display:none; }.leads-table th,.leads-table td { padding:12px 10px; }.detail-panel { padding:24px 18px; } }
</style>
