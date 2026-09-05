<template>
  <div class="enterprise-overlay" role="presentation" @click.self="$emit('close')">
    <section class="enterprise-modal" role="dialog" aria-modal="true" aria-labelledby="enterprise-title">
      <button type="button" class="close-button" aria-label="Đóng" @click="$emit('close')">×</button>
      <div v-if="submitted" class="success-state">
        <div class="success-icon" aria-hidden="true">✓</div>
        <p class="eyebrow">SPRINTA ENTERPRISE</p>
        <h2 id="enterprise-title">{{ isVi ? 'Đã nhận yêu cầu' : 'Request received' }}</h2>
        <p>{{ isVi ? 'Cảm ơn bạn. SprintA đã nhận được yêu cầu và sẽ liên hệ lại.' : 'Thank you. SprintA received your request and will be in touch.' }}</p>
        <button type="button" class="submit-button" @click="$emit('close')">{{ isVi ? 'Đóng' : 'Close' }}</button>
      </div>
      <form v-else class="enterprise-form" @submit.prevent="submit">
        <p class="eyebrow">SPRINTA ENTERPRISE</p>
        <h2 id="enterprise-title">{{ isVi ? 'Nói chuyện cùng đội ngũ SprintA' : 'Talk to the SprintA team' }}</h2>
        <p class="intro">{{ isVi ? 'Chia sẻ một chút bối cảnh. Chúng tôi sẽ tư vấn cách triển khai phù hợp cho đội của bạn.' : 'Share a little context and we will help find the right rollout for your team.' }}</p>

        <div v-if="errorMessage" class="form-error" role="alert">{{ errorMessage }}</div>
        <div class="form-grid">
          <label><span>{{ isVi ? 'Họ và tên' : 'Full name' }} *</span><input v-model.trim="form.contactName" required maxlength="120" autocomplete="name" /></label>
          <label><span>{{ isVi ? 'Email công việc' : 'Work email' }} *</span><input v-model.trim="form.workEmail" required maxlength="320" type="email" autocomplete="email" /></label>
          <label><span>{{ isVi ? 'Công ty / tổ chức' : 'Company / organization' }} *</span><input v-model.trim="form.company" required maxlength="200" autocomplete="organization" /></label>
          <label><span>{{ isVi ? 'Quy mô đội nhóm' : 'Team size' }} *</span><select v-model="form.teamSize" required><option disabled value="">{{ isVi ? 'Chọn quy mô' : 'Select team size' }}</option><option v-for="size in teamSizes" :key="size" :value="size">{{ size }}</option></select></label>
          <label><span>{{ isVi ? 'Số điện thoại / Zalo' : 'Phone / Zalo' }}</span><input v-model.trim="form.phoneOrZalo" maxlength="50" autocomplete="tel" /></label>
          <label><span>{{ isVi ? 'Nhu cầu chính' : 'Primary need' }}</span><select v-model="form.need"><option value="">{{ isVi ? 'Chọn nhu cầu' : 'Select a need' }}</option><option v-for="need in needs" :key="need" :value="need">{{ need }}</option></select></label>
          <label><span>{{ isVi ? 'Thời gian muốn được liên hệ' : 'Preferred contact time' }}</span><input v-model.trim="form.preferredContactTime" maxlength="100" :placeholder="isVi ? 'Ví dụ: sáng ngày thường' : 'e.g. weekday mornings'" /></label>
          <label class="full-width"><span>{{ isVi ? 'Ghi chú' : 'Notes' }}</span><textarea v-model.trim="form.notes" maxlength="2000" rows="4"></textarea></label>
        </div>
        <button type="submit" class="submit-button" :disabled="submitting">{{ submitting ? (isVi ? 'Đang gửi…' : 'Sending…') : (isVi ? 'Liên hệ tư vấn' : 'Contact sales') }}</button>
        <small class="privacy-note">{{ isVi ? 'Thông tin chỉ được dùng để liên hệ về nhu cầu Enterprise.' : 'Your details are only used to follow up on this Enterprise request.' }}</small>
      </form>
    </section>
  </div>
</template>

<script setup>
import { reactive, ref, computed } from 'vue'
import axiosClient from '@/api/axiosClient'
import { language } from '@/i18n'
import { getStoredUserSession } from '@/utils/authSession'

const props = defineProps({
  prefill: { type: Object, default: () => ({}) }
})
defineEmits(['close'])
const isVi = computed(() => language.value === 'vi')
const submitting = ref(false)
const submitted = ref(false)
const errorMessage = ref('')
const teamSizes = ['1–10', '11–50', '51–200', '201–500', '500+']
const needs = ['Quản lý dự án', 'Sprint / Agile', 'Báo cáo', 'AI workflow', 'Multi-team / Organization', 'Bảo mật / Enterprise deployment', 'Khác']
const storedUser = getStoredUserSession() || {}
const contextNote = [
  props.prefill.source && `Nguồn yêu cầu: ${props.prefill.source}`,
  props.prefill.plan && `Gói quan tâm: ${props.prefill.plan}`,
  props.prefill.workspaceName && `Workspace: ${props.prefill.workspaceName}`,
  props.prefill.workspaceId && `Workspace ID: ${props.prefill.workspaceId}`,
  props.prefill.projectName && `Project: ${props.prefill.projectName}`
].filter(Boolean).join('\n')
const form = reactive({
  contactName: props.prefill.contactName || storedUser.fullName || storedUser.username || '',
  workEmail: props.prefill.workEmail || storedUser.email || '',
  company: props.prefill.company || '',
  teamSize: props.prefill.teamSize || '',
  phoneOrZalo: props.prefill.phoneOrZalo || '',
  need: props.prefill.need || (props.prefill.source === 'AI Credits' ? 'AI workflow' : ''),
  notes: props.prefill.notes || contextNote,
  preferredContactTime: props.prefill.preferredContactTime || ''
})

const submit = async () => {
  errorMessage.value = ''
  submitting.value = true
  try {
    await axiosClient.post('/public/enterprise-leads', form)
    submitted.value = true
  } catch (error) {
    errorMessage.value = error.response?.data?.message || (isVi.value ? 'Không thể gửi yêu cầu lúc này. Vui lòng thử lại.' : 'We could not send your request. Please try again.')
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped>
.enterprise-overlay { position: fixed; inset: 0; z-index: 100; display: grid; place-items: center; padding: 20px; background: rgba(1, 8, 18, .76); backdrop-filter: blur(12px); overflow-y: auto; }
.enterprise-modal { position: relative; width: min(680px, 100%); max-height: min(860px, calc(100vh - 40px)); overflow-y: auto; padding: 34px; border: 1px solid rgba(91, 209, 255, .28); border-radius: 22px; color: #eaf5ff; background: linear-gradient(145deg, #0a223b, #061426); box-shadow: 0 30px 90px rgba(0,0,0,.45); }
.close-button { position: absolute; top: 14px; right: 16px; width: 34px; height: 34px; border: 0; border-radius: 50%; color: #b6d2e8; background: rgba(255,255,255,.08); font-size: 24px; cursor: pointer; }
.eyebrow { margin: 0 0 10px; color: #65d8ff; font-size: 10px; font-weight: 900; letter-spacing: .2em; }
h2 { margin: 0; font-size: clamp(28px, 5vw, 42px); letter-spacing: -.04em; line-height: 1.08; }
.intro { margin: 14px 0 24px; color: #9bb5cc; line-height: 1.6; }
.form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 15px; }
label { display: grid; gap: 7px; color: #c7d9e8; font-size: 12px; font-weight: 750; }
label input, label select, label textarea { width: 100%; border: 1px solid rgba(135, 188, 220, .22); border-radius: 10px; padding: 11px 12px; color: #eff9ff; background: rgba(1, 12, 26, .66); outline: none; font: inherit; }
label textarea { resize: vertical; min-height: 92px; }
label input:focus, label select:focus, label textarea:focus { border-color: #57d1ff; box-shadow: 0 0 0 3px rgba(87,209,255,.12); }
.full-width { grid-column: 1 / -1; }
.submit-button { width: 100%; min-height: 46px; margin-top: 22px; border: 0; border-radius: 10px; color: white; background: linear-gradient(100deg, #0d8fe9, #2859d8); font-weight: 850; cursor: pointer; }
.submit-button:disabled { opacity: .65; cursor: wait; }
.privacy-note { display: block; margin-top: 10px; color: #7895ad; line-height: 1.4; text-align: center; }
.form-error { margin: 0 0 16px; padding: 11px 13px; border: 1px solid rgba(255, 127, 127, .35); border-radius: 10px; color: #ffc7c7; background: rgba(153, 31, 45, .2); }
.success-state { display: grid; justify-items: center; padding: 34px 12px 12px; text-align: center; }
.success-icon { display: grid; width: 54px; height: 54px; margin-bottom: 20px; place-items: center; border-radius: 50%; color: #071525; background: #65e5b0; font-size: 30px; font-weight: 900; }
.success-state p:not(.eyebrow) { max-width: 430px; margin: 14px 0 0; color: #b4c9dc; line-height: 1.6; }
.success-state .submit-button { width: min(220px, 100%); }
@media (max-width: 620px) { .enterprise-overlay { padding: 10px; align-items: start; } .enterprise-modal { max-height: calc(100vh - 20px); padding: 28px 18px 22px; border-radius: 17px; } .form-grid { grid-template-columns: 1fr; gap: 13px; } .full-width { grid-column: auto; } }
</style>
