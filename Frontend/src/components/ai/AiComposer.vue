<template>
  <div
    class="ai-composer"
    :class="{ 'is-dragging-files': composerDragActive }"
    @dragenter.prevent="$emit('dragenter')"
    @dragover.prevent="$emit('dragenter')"
    @dragleave.prevent="$emit('dragleave', $event)"
    @drop.prevent="$emit('drop', $event)"
  >
    <input ref="fileInput" class="ai-composer-file-input" type="file" multiple :accept="accept" @change="$emit('files', $event)" />

    <div v-if="pendingAttachments.length" class="ai-attachment-tray" role="list" aria-label="Tệp đang chờ tải lên">
      <article v-for="attachment in pendingAttachments" :key="attachment.id" class="ai-attachment-card" :class="attachment.kind === 'image' ? 'is-image' : 'is-file'" role="listitem">
        <button v-if="attachment.kind === 'image'" class="ai-attachment-thumbnail" type="button" :title="`Mở ${attachment.name}`" @click="$emit('preview-attachment', attachment)">
          <img :src="attachment.previewUrl" :alt="attachment.name" />
        </button>
        <div v-else class="ai-attachment-file-icon" aria-hidden="true"><i :class="attachment.icon"></i></div>
        <div class="ai-attachment-meta">
          <strong>{{ attachment.kind === 'image' ? attachment.displayName : attachment.name }}</strong>
          <span>{{ attachment.typeLabel }}<template v-if="attachment.kind === 'image'"> · {{ formatBytes(attachment.size) }}<template v-if="attachment.width && attachment.height"> · {{ attachment.width }}×{{ attachment.height }}</template></template></span>
          <small :class="`is-${attachment.status || 'pending'}`"><i :class="statusIcon(attachment.status)"></i> {{ statusLabel(attachment.status) }}</small>
        </div>
        <div class="ai-attachment-actions">
          <button type="button" :title="`Mở ${attachment.name}`" @click="$emit('preview-attachment', attachment)"><i class="fa-solid fa-up-right-from-square"></i></button>
          <button type="button" :title="`Gỡ ${attachment.name}`" @click="$emit('remove-attachment', attachment.id)"><i class="fa-solid fa-xmark"></i></button>
        </div>
      </article>
    </div>

    <section v-if="voiceState !== 'idle'" class="ai-voice-panel" aria-label="Nhập bằng giọng nói">
      <div class="ai-voice-head">
        <div><strong>{{ voiceStatusTitle }}</strong><span v-if="voiceState === 'recording'" class="ai-voice-timer">{{ voiceElapsedLabel }}</span></div>
        <label class="ai-voice-language">
          <span>Ngôn ngữ giọng nói: {{ voiceLanguageLabel }}</span>
          <select :value="voiceLanguage" :disabled="voiceState === 'transcribing'" aria-label="Ngôn ngữ giọng nói" @change="$emit('update:voiceLanguage', $event.target.value)">
            <option value="auto">Tự động (VI/EN)</option><option value="vi">Tiếng Việt</option><option value="en">English</option>
          </select>
        </label>
      </div>
      <p v-if="voiceState === 'requesting'" class="ai-voice-note" role="status">Trình duyệt đang yêu cầu quyền sử dụng microphone.</p>
      <p v-else-if="voiceState === 'recording'" class="ai-voice-note" role="status">Audio chỉ được giữ tạm để phiên âm và sẽ không được lưu vĩnh viễn.</p>
      <p v-else-if="voiceState === 'transcribing'" class="ai-voice-note" role="status"><i class="fa-solid fa-spinner fa-spin"></i> Đang chuyển giọng nói thành văn bản...</p>
      <p v-else-if="voiceState === 'error'" class="ai-voice-error" role="alert">{{ voiceError }}</p>
      <label v-if="voiceState === 'success'" class="ai-voice-transcript"><span>Transcript</span><textarea :value="voiceTranscript" rows="4" aria-label="Chỉnh sửa transcript" @input="$emit('update:voiceTranscript', $event.target.value)"></textarea></label>
      <div class="ai-voice-actions">
        <button type="button" class="ai-voice-secondary" @click="$emit('cancel-voice')">Hủy</button>
        <button v-if="voiceState === 'recording'" type="button" class="ai-voice-primary" @click="$emit('stop-voice')"><i class="fa-solid fa-stop"></i> Dừng</button>
        <button v-if="voiceState === 'error'" type="button" class="ai-voice-primary" @click="$emit('start-voice')"><i class="fa-solid fa-rotate-right"></i> Thử lại</button>
        <button v-if="voiceState === 'success'" type="button" class="ai-voice-secondary" @click="$emit('record-again')"><i class="fa-solid fa-microphone-lines"></i> Thu lại</button>
        <button v-if="voiceState === 'success'" type="button" class="ai-voice-primary" :disabled="!voiceTranscript.trim()" @click="$emit('use-transcript')">Dùng nội dung này</button>
      </div>
    </section>

    <div class="ai-composer-row">
      <el-dropdown trigger="click" placement="top-start" @command="$emit('attachment-command', $event)">
        <button class="ai-composer-icon-btn" type="button" title="Thêm ảnh hoặc tài liệu" aria-label="Thêm ảnh hoặc tài liệu"><i class="fa-solid fa-plus"></i></button>
        <template #dropdown>
          <el-dropdown-menu>
            <el-dropdown-item command="browse"><i class="fa-regular fa-folder-open"></i> Chọn ảnh hoặc tài liệu</el-dropdown-item>
            <el-dropdown-item command="paste"><i class="fa-regular fa-clipboard"></i> Dán ảnh từ clipboard</el-dropdown-item>
            <el-dropdown-item command="screenshot" :disabled="capturingScreenshot"><i class="fa-solid fa-display"></i> Chụp màn hình</el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>
      <textarea
        ref="textareaInput"
        :value="modelValue"
        rows="1"
        :aria-label="placeholder"
        :placeholder="placeholder"
        @input="handleInput"
        @paste="$emit('paste', $event)"
        @keydown="$emit('keydown', $event)"
      ></textarea>
      <button class="ai-composer-icon-btn" :class="{ active: voiceState !== 'idle' }" type="button" title="Nhập bằng giọng nói" aria-label="Nhập bằng giọng nói" :disabled="['requesting', 'recording', 'transcribing'].includes(voiceState)" @click="$emit('start-voice')"><i class="fa-solid fa-microphone"></i></button>
      <button class="ai-composer-send" type="button" :disabled="sending || creditsExhausted || (!modelValue.trim() && !pendingAttachments.length)" title="Gửi tin nhắn" aria-label="Gửi tin nhắn" @click="$emit('send')"><i v-if="!sending" class="fa-solid fa-paper-plane"></i><i v-else class="fa-solid fa-spinner fa-spin"></i></button>
    </div>
    <div class="ai-input-foot"><span>{{ pendingAttachments.length ? 'Attachment sẽ được tải lên kho riêng tư khi gửi.' : enterHint }}</span><button type="button" @click="$emit('reset')">{{ resetLabel }}</button></div>
  </div>
</template>

<script setup>
import { ref } from 'vue'

defineProps({
  modelValue: { type: String, default: '' },
  placeholder: { type: String, default: 'Hỏi SprintA AI bất cứ điều gì...' },
  enterHint: { type: String, default: 'Enter để gửi · Shift + Enter để xuống dòng' },
  resetLabel: { type: String, default: 'Cuộc trò chuyện mới' },
  sending: Boolean,
  creditsExhausted: Boolean,
  pendingAttachments: { type: Array, default: () => [] },
  composerDragActive: Boolean,
  capturingScreenshot: Boolean,
  voiceState: { type: String, default: 'idle' },
  voiceLanguage: { type: String, default: 'auto' },
  voiceLanguageLabel: { type: String, default: 'Tự động (VI/EN)' },
  voiceStatusTitle: { type: String, default: 'Nhập bằng giọng nói' },
  voiceElapsedLabel: { type: String, default: '00:00' },
  voiceTranscript: { type: String, default: '' },
  voiceError: { type: String, default: '' },
  accept: { type: String, default: '' }
})

const emit = defineEmits([
  'update:modelValue', 'update:voiceLanguage', 'update:voiceTranscript', 'dragenter', 'dragleave', 'drop', 'files',
  'preview-attachment', 'remove-attachment', 'attachment-command', 'paste', 'input', 'keydown', 'start-voice',
  'stop-voice', 'cancel-voice', 'record-again', 'use-transcript', 'send', 'reset'
])

const fileInput = ref(null)
const textareaInput = ref(null)
const handleInput = event => {
  const textarea = event.target
  textarea.style.height = 'auto'
  textarea.style.height = `${Math.min(textarea.scrollHeight, 150)}px`
  emit('update:modelValue', textarea.value)
  emit('input', event)
}
const formatBytes = bytes => {
  if (!Number.isFinite(bytes) || bytes <= 0) return '0 B'
  const units = ['B', 'KB', 'MB']
  const unit = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1)
  const value = bytes / (1024 ** unit)
  return `${value >= 10 || unit === 0 ? value.toFixed(0) : value.toFixed(1)} ${units[unit]}`
}
const statusLabel = status => ({ uploading: 'Đang tải lên', processing: 'Đang xử lý', error: 'Tải lên thất bại', ready: 'Đã xử lý' }[String(status || 'pending').toLowerCase()] || 'Chờ tải lên')
const statusIcon = status => ({ uploading: 'fa-solid fa-arrow-up-from-bracket fa-bounce', processing: 'fa-solid fa-spinner fa-spin', error: 'fa-solid fa-circle-exclamation', ready: 'fa-solid fa-circle-check' }[String(status || 'pending').toLowerCase()] || 'fa-regular fa-clock')

defineExpose({
  openFilePicker: () => fileInput.value?.click(),
  focusInput: () => textareaInput.value?.focus()
})
</script>

<style scoped>
.ai-composer { display: grid; gap: 9px; width: 100%; padding: 8px 9px 8px 12px; border: 1px solid color-mix(in srgb, var(--color-border) 84%, var(--color-accent)); border-radius: 16px; background: var(--color-surface); }
.ai-composer.is-dragging-files { border-color: var(--color-accent); background: color-mix(in srgb, var(--color-accent) 7%, var(--color-surface)); }
.ai-composer-file-input { display: none; }
.ai-attachment-tray { display: grid; gap: 8px; }
.ai-attachment-card { display: grid; grid-template-columns: 42px minmax(0, 1fr) auto; align-items: center; gap: 9px; min-width: 0; padding: 7px; border: 1px solid var(--color-border); border-radius: 8px; background: color-mix(in srgb, var(--color-surface-hover) 62%, transparent); }
.ai-attachment-thumbnail, .ai-attachment-file-icon { width: 42px; height: 36px; display: grid; place-items: center; overflow: hidden; border: 1px solid var(--color-border); border-radius: 6px; background: var(--color-surface-hover); color: var(--color-accent); }
.ai-attachment-thumbnail { padding: 0; cursor: pointer; }
.ai-attachment-thumbnail img { width: 100%; height: 100%; object-fit: cover; }
.ai-attachment-meta { display: grid; min-width: 0; gap: 2px; font-size: 10px; }
.ai-attachment-meta strong, .ai-attachment-meta span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.ai-attachment-meta strong { color: var(--color-text-primary); font-size: 11px; }
.ai-attachment-meta span, .ai-attachment-meta small { color: var(--color-text-muted); }
.ai-attachment-meta small.is-ready { color: var(--color-success); }.ai-attachment-meta small.is-error { color: var(--color-danger); }.ai-attachment-meta small.is-uploading, .ai-attachment-meta small.is-processing { color: var(--color-accent); }
.ai-attachment-actions { display: flex; gap: 4px; }
.ai-attachment-actions button, .ai-composer-icon-btn, .ai-composer-send { width: 40px; height: 40px; display: grid; place-items: center; flex: 0 0 40px; padding: 0; border: 1px solid var(--color-border); border-radius: 11px; background: transparent; color: var(--color-text-secondary); cursor: pointer; }
.ai-attachment-actions button { width: 32px; height: 32px; flex-basis: 32px; border: 0; border-radius: 6px; }
.ai-attachment-actions button:hover, .ai-composer-icon-btn:hover:not(:disabled), .ai-composer-icon-btn.active, .ai-composer-send:hover:not(:disabled) { border-color: var(--color-accent); background: var(--color-surface-hover); color: var(--color-accent); }
.ai-composer-send { border-color: var(--color-accent); background: var(--color-accent); color: var(--color-on-accent, #fff); }
.ai-composer-icon-btn:disabled, .ai-composer-send:disabled { cursor: not-allowed; opacity: .55; }
.ai-composer-row { display: flex; align-items: center; gap: 8px; min-width: 0; }
.ai-composer-row :deep(.el-dropdown) { flex: 0 0 40px; }
.ai-composer-row textarea { flex: 1; min-width: 0; min-height: 40px; max-height: 150px; padding: 9px 4px; resize: none; border: 0; outline: none; background: transparent; color: var(--color-text-primary); font: inherit; line-height: 1.45; }
.ai-composer-row textarea::placeholder { color: var(--color-text-muted); }
.ai-input-foot { display: flex; align-items: center; justify-content: space-between; gap: 8px; color: var(--color-text-muted); font-size: 10px; }
.ai-input-foot button { border: 0; background: transparent; color: var(--color-accent); cursor: pointer; font-size: inherit; font-weight: 750; }
.ai-voice-panel { display: grid; gap: 10px; padding: 12px; border: 1px solid var(--color-border); border-radius: 8px; background: color-mix(in srgb, var(--color-surface) 92%, var(--color-accent)); }
.ai-voice-head, .ai-voice-head > div, .ai-voice-actions { display: flex; align-items: center; }.ai-voice-head { justify-content: space-between; gap: 12px; }.ai-voice-head > div { gap: 8px; }.ai-voice-head strong { color: var(--color-text-primary); font-size: 13px; }.ai-voice-timer { color: var(--color-danger); font: 700 12px/1 ui-monospace, monospace; }
.ai-voice-language { display: grid; gap: 4px; color: var(--color-text-muted); font-size: 10px; }.ai-voice-language select, .ai-voice-transcript textarea { border: 1px solid var(--color-border); border-radius: 6px; background: var(--color-surface); color: var(--color-text-primary); }.ai-voice-language select { height: 30px; padding: 0 8px; font-size: 11px; }.ai-voice-note, .ai-voice-error { margin: 0; color: var(--color-text-secondary); font-size: 11px; line-height: 1.5; }.ai-voice-error { color: var(--color-danger); }.ai-voice-transcript { display: grid; gap: 6px; color: var(--color-text-muted); font-size: 11px; font-weight: 700; }.ai-voice-transcript textarea { width: 100%; min-height: 92px; padding: 9px 10px; resize: vertical; font: inherit; line-height: 1.5; }.ai-voice-actions { justify-content: flex-end; flex-wrap: wrap; gap: 6px; }.ai-voice-actions button { min-height: 32px; padding: 0 10px; border: 1px solid var(--color-border); border-radius: 6px; background: transparent; color: var(--color-text-secondary); cursor: pointer; font-size: 11px; font-weight: 700; }.ai-voice-primary { border-color: var(--color-accent) !important; background: var(--color-accent) !important; color: var(--color-on-accent, #fff) !important; }
@media (max-width: 520px) { .ai-composer-row { gap: 4px; }.ai-composer-row textarea { min-width: 0; }.ai-input-foot { align-items: flex-start; flex-direction: column; gap: 3px; }.ai-voice-head { align-items: stretch; flex-direction: column; }.ai-voice-language select { width: 100%; } }

.ai-composer {
  padding: 10px;
  border-color: color-mix(in srgb, var(--color-accent) 24%, var(--color-border));
  border-radius: 18px;
  background:
    linear-gradient(145deg, color-mix(in srgb, var(--color-accent) 7%, var(--color-surface)), var(--color-surface) 58%),
    var(--color-surface);
  box-shadow: 0 12px 28px color-mix(in srgb, var(--color-text-primary) 8%, transparent);
  transition: border-color 160ms ease, box-shadow 160ms ease, background 160ms ease;
}

.ai-composer:focus-within {
  border-color: color-mix(in srgb, var(--color-accent) 65%, var(--color-border));
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-accent) 12%, transparent), 0 14px 32px color-mix(in srgb, var(--color-text-primary) 9%, transparent);
}

.ai-composer-row {
  padding: 4px;
  border: 1px solid color-mix(in srgb, var(--color-border) 86%, var(--color-accent));
  border-radius: 14px;
  background: color-mix(in srgb, var(--color-surface-hover) 66%, var(--color-surface));
}

.ai-composer-row textarea { min-height: 42px; padding-inline: 7px; }
.ai-composer-row textarea:focus-visible { outline: none; }
.ai-composer-icon-btn { transition: border-color 160ms ease, background 160ms ease, color 160ms ease, transform 160ms ease; }
.ai-composer-icon-btn:hover:not(:disabled) { transform: translateY(-1px); }
.ai-composer-send { box-shadow: 0 6px 14px color-mix(in srgb, var(--color-accent) 25%, transparent); transition: filter 160ms ease, transform 160ms ease; }
.ai-composer-send:hover:not(:disabled) { filter: brightness(1.06); transform: translateY(-1px); }
.ai-input-foot { padding-inline: 3px; }
.ai-input-foot span { line-height: 1.4; }
.ai-attachment-card, .ai-voice-panel { border-color: color-mix(in srgb, var(--color-accent) 18%, var(--color-border)); background: color-mix(in srgb, var(--color-accent) 5%, var(--color-surface)); }
.ai-attachment-card { border-radius: 11px; }
.ai-voice-panel { border-radius: 12px; }

@media (max-width: 520px) {
  .ai-composer { border-radius: 15px; }
  .ai-composer-row { border-radius: 12px; }
}

/* Final interaction surface: attachments stay above the input and the input
   gets enough room to feel like one calm, modern writing surface. */
.ai-composer {
  gap: 12px;
  padding: 13px;
  border-radius: 22px;
  border-color: color-mix(in srgb, var(--color-accent) 28%, var(--color-border));
  background:
    linear-gradient(145deg, color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)), var(--color-surface) 62%),
    var(--color-surface);
  box-shadow: 0 16px 34px color-mix(in srgb, var(--color-text-primary) 11%, transparent), 0 0 0 1px color-mix(in srgb, var(--color-text-inverse) 7%, transparent) inset;
}

.ai-attachment-tray {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  max-height: 190px;
  margin: 0;
  overflow-y: auto;
}

.ai-attachment-card {
  display: grid;
  grid-template-columns: 78px minmax(0, 1fr) auto;
  flex: 1 1 230px;
  min-width: min(100%, 230px);
  max-width: 320px;
  padding: 8px;
  border-radius: 13px;
  border-color: color-mix(in srgb, var(--color-accent) 20%, var(--color-border));
  background: color-mix(in srgb, var(--color-surface-hover) 78%, var(--color-surface));
}

.ai-attachment-card.is-file {
  grid-template-columns: 42px minmax(0, 1fr) auto;
  min-width: min(100%, 210px);
}

.ai-attachment-thumbnail {
  width: 78px;
  height: 58px;
  border-radius: 9px;
}

.ai-attachment-file-icon {
  width: 42px;
  height: 42px;
  border-radius: 10px;
  font-size: 16px;
}

.ai-attachment-meta { gap: 3px; }
.ai-attachment-meta strong { font-size: 12px; }
.ai-attachment-meta span { font-size: 10px; }
.ai-attachment-meta small { font-size: 10px; }

.ai-composer-row {
  display: grid;
  grid-template-columns: 46px minmax(0, 1fr) 46px 48px;
  gap: 9px;
  align-items: end;
  padding: 6px 7px 6px 8px;
  border-radius: 17px;
  border-color: color-mix(in srgb, var(--color-accent) 23%, var(--color-border));
  background: color-mix(in srgb, var(--color-bg) 34%, var(--color-surface));
}

.ai-composer-row :deep(.el-dropdown) { width: 46px; }
.ai-composer-row textarea {
  min-height: 52px;
  max-height: 180px;
  padding: 12px 4px;
  font-size: 14px;
  line-height: 1.55;
}

.ai-attachment-actions button,
.ai-composer-icon-btn,
.ai-composer-send {
  width: 44px;
  height: 44px;
  flex-basis: 44px;
  border-radius: 13px;
}

.ai-composer-send {
  width: 48px;
  height: 48px;
  flex-basis: 48px;
  box-shadow: 0 8px 18px color-mix(in srgb, var(--color-accent) 26%, transparent);
}

.ai-input-foot {
  padding: 0 5px;
  font-size: 10px;
}
.ai-input-foot span { line-height: 1.45; }

@media (max-width: 520px) {
  .ai-composer { padding: 10px; border-radius: 18px; }
  .ai-attachment-card,
  .ai-attachment-card.is-file { flex-basis: 100%; max-width: none; }
  .ai-composer-row { grid-template-columns: 42px minmax(0, 1fr) 42px 46px; gap: 5px; padding-inline: 5px; }
  .ai-composer-row :deep(.el-dropdown) { width: 42px; }
  .ai-composer-row textarea { min-height: 50px; padding-inline: 3px; }
  .ai-composer-icon-btn { width: 40px; height: 40px; flex-basis: 40px; }
  .ai-composer-send { width: 46px; height: 46px; flex-basis: 46px; }
}
</style>
