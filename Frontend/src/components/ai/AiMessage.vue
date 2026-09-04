<template>
  <div class="ai-message" :class="message.role">
    <div class="ai-message-avatar" :class="message.role === 'user' ? 'is-user' : 'is-bot'">
      <img v-if="message.role === 'bot'" src="/ai-sprinta/idle.png" alt="Mascot SprintA AI" />
      <img v-else-if="profileAvatar" :src="profileAvatar" :alt="`Ảnh đại diện của ${profileName}`" />
      <span v-else aria-hidden="true">{{ profileInitials }}</span>
    </div>
    <div class="ai-message-stack">
      <span class="ai-message-author">{{ message.role === 'bot' ? 'SprintA AI' : 'Bạn' }}</span>
      <div class="ai-message-bubble">
        <i v-if="message.loading" class="fa-solid fa-spinner fa-spin mr-2"></i>
        <div v-if="message.attachments?.length" class="ai-message-attachments" role="list" aria-label="Attachment trong tin nhắn">
          <article v-for="attachment in message.attachments" :key="attachment.id" class="ai-message-attachment-card" role="listitem">
            <button v-if="attachment.kind === 'image'" class="ai-message-attachment-image" type="button" @click="$emit('preview-attachment', attachment)"><img v-if="attachment.previewUrl" :src="attachment.previewUrl" :alt="attachment.name" /><i v-else class="fa-regular fa-image" aria-hidden="true"></i></button>
            <div v-else class="ai-attachment-file-icon" aria-hidden="true"><i :class="attachment.icon"></i></div>
            <div class="ai-attachment-meta"><strong>{{ attachment.name }}</strong><span>{{ attachment.typeLabel }} · {{ formatBytes(attachment.size) }}</span><small class="is-ready"><i class="fa-solid fa-circle-check"></i> Đã xử lý</small></div>
            <button class="ai-message-attachment-open" type="button" :title="`Mở ${attachment.name}`" @click="$emit('preview-attachment', attachment)"><i class="fa-solid fa-up-right-from-square"></i></button>
          </article>
        </div>
        <div class="ai-markdown" v-html="renderMarkdown(message.content)"></div>
        <div v-if="message.progressSteps?.length" class="ai-thinking-steps">
          <div v-for="(step, stepIndex) in message.progressSteps" :key="stepIndex" class="ai-thinking-step" :class="{ active: stepIndex <= (message.progressIndex || 0) }">
            <i v-if="stepIndex === (message.progressIndex || 0) && message.isTyping" class="fa-solid fa-circle-notch"></i><i v-else-if="stepIndex < (message.progressIndex || 0)" class="fa-solid fa-check"></i><i v-else class="fa-regular fa-circle"></i><span>{{ step }}</span>
          </div>
        </div>
        <i v-if="message.isTyping" class="fa-solid fa-ellipsis fa-fade"></i>
        <div v-if="message.citations?.length" class="ai-citations" aria-label="Nguồn trích dẫn"><strong>Nguồn</strong><button v-for="citation in message.citations" :key="`${citation.sourceId}-${citation.attachmentId}`" type="button" @click="$emit('open-citation', citation)"><span>[{{ citation.sourceId }}] {{ citation.fileName }} · {{ citation.locator }}</span><small>{{ citation.excerpt }}</small></button></div>
        <div v-if="message.role === 'bot' && !message.loading" class="ai-message-tools" aria-label="Thao tác với câu trả lời"><button type="button" title="Sao chép câu trả lời" @click="$emit('copy', message.content)"><i class="fa-regular fa-copy"></i></button><button type="button" title="Hỏi tiếp từ câu trả lời" @click="$emit('continue', message.content)"><i class="fa-solid fa-reply"></i></button></div>
        <div v-if="message.warnings?.length" class="ai-warnings" role="note"><strong><i class="fa-solid fa-triangle-exclamation"></i> Cảnh báo rủi ro</strong><ul><li v-for="(warning, warningIndex) in message.warnings" :key="warningIndex">{{ warning }}</li></ul></div>
        <div v-if="message.actions?.length" class="ai-action-preview-list" aria-label="AI action previews">
          <p v-if="hasReadOnlyActions(message.actions)" class="ai-activity-note" role="status"><i class="fa-solid fa-circle-check"></i> Đã đọc dữ liệu hiện tại và bổ sung kết quả vào câu trả lời.</p>
          <article v-for="(action, actionIndex) in writeActions(message.actions)" :key="`${action.type}-${actionIndex}`" class="ai-action-preview-card" :class="{ 'is-pending': action.uiStatus === 'pending' }">
            <div class="ai-action-preview-head"><div><span class="ai-action-eyebrow">AI ACTION PREVIEW</span><strong>{{ aiActionLabel(action.type) }}</strong></div><span class="ai-action-status" :class="`is-${action.uiStatus || 'pending'}`">{{ aiActionStatusLabel(action) }}</span></div>
            <p class="ai-action-description">{{ action.description || aiActionSummary(action) }}</p>
            <dl class="ai-action-details"><template v-for="detail in aiActionDetails(action)" :key="detail.label"><dt>{{ detail.label }}</dt><dd>{{ detail.value }}</dd></template></dl>
            <div v-if="action.duplicateCandidate" class="ai-duplicate-warning" role="alert"><strong>Đã có công việc tương tự trong dự án</strong><p>#{{ action.duplicateCandidate.sequenceId || action.duplicateCandidate.id }} · {{ action.duplicateCandidate.title }} · {{ action.duplicateCandidate.statusName }}</p><div><button type="button" @click="$emit('open-duplicate-task', action, false)">Mở công việc hiện có</button><button type="button" @click="$emit('open-duplicate-task', action, true)">Cập nhật công việc hiện có</button><button type="button" @click="$emit('confirm-duplicate-creation', action)">Vẫn tạo công việc mới</button></div></div>
            <p v-if="action.error" class="ai-action-error" role="alert">{{ action.error }}</p><p v-if="action.result?.message" class="ai-action-result" role="status">{{ action.result.message }}</p>
            <div v-if="!action.duplicateCandidate" class="ai-action-controls">
              <button v-if="action.uiStatus === 'cancelled'" type="button" class="ai-action-confirm" @click="$emit('retry-action', action)"><i class="fa-solid fa-rotate-right"></i> Thực hiện lại</button>
              <button v-else-if="action.uiStatus === 'error'" type="button" class="ai-action-confirm" :disabled="action.loading" @click="$emit('execute-action', action)"><i class="fa-solid fa-rotate-right"></i> Thử lại</button>
              <template v-else><button v-if="!isReadOnlyAction(action) && action.uiStatus !== 'success'" type="button" class="ai-action-cancel" :disabled="action.loading" @click="$emit('cancel-action', action)">Hủy</button><button type="button" class="ai-action-confirm" :disabled="action.loading || action.uiStatus === 'success'" @click="$emit('execute-action', action)"><i v-if="action.loading" class="fa-solid fa-spinner fa-spin"></i><i v-else-if="action.uiStatus === 'success'" class="fa-solid fa-check"></i>{{ action.uiStatus === 'success' ? 'Đã thực hiện' : (isReadOnlyAction(action) ? 'Xem kết quả' : 'Xác nhận') }}</button></template>
            </div>
          </article>
        </div>
        <div v-if="message.suggestedActions?.length" class="ai-suggested-actions" aria-label="Gợi ý cập nhật công việc">
          <div v-for="(action, actionIndex) in message.suggestedActions" :key="actionIndex" class="ai-suggested-action">
            <p>Chuyển công việc sang trạng thái mới:</p>
            <div><span>{{ action.taskTitle }} → {{ action.statusName }}</span><button type="button" :disabled="action.completed || action.loading || !canUpdateTask" @click="$emit('confirm-suggested-action', action)">{{ action.completed ? 'Đã thực hiện' : 'Xác nhận chuyển' }}</button></div>
          </div>
        </div>
        <div v-if="message.suggestedTasks?.length" class="ai-suggested-tasks" aria-label="AI đề xuất công việc">
          <div class="ai-suggested-tasks-head"><strong><i class="fa-solid fa-list-check"></i> AI đề xuất công việc</strong><button v-if="message.suggestedTasks.some(task => !task.created)" type="button" :disabled="!canCreateTask" @click="$emit('create-all-suggested-tasks', message)">Tạo tất cả</button></div>
          <div class="ai-suggested-task-list">
            <article v-for="(task, taskIndex) in message.suggestedTasks" :key="taskIndex" class="ai-suggested-task">
              <div><strong>{{ task.title }}</strong><span v-if="task.priority">P{{ task.priority }}</span></div>
              <p>{{ task.description }}</p>
              <small>Hạn: {{ task.dueDate || 'N/A' }}<template v-if="task.assigneeEmail"> · {{ task.assigneeEmail }}</template></small>
              <div class="ai-suggested-task-foot"><span v-if="task.created" class="is-created"><i class="fa-solid fa-circle-check"></i> Đã tạo</span><button v-else type="button" :disabled="!canCreateTask || task.loading" @click="$emit('create-suggested-task', task, message)">{{ task.loading ? 'Đang tạo...' : 'Tạo task này' }}</button></div>
            </article>
          </div>
          <p v-if="!canCreateTask" class="ai-permission-note">Bạn không có quyền tạo công việc trong dự án này.</p>
        </div>
        <div v-if="message.suggestedPrompts?.length" class="ai-suggested-prompts"><button v-for="(prompt, promptIndex) in message.suggestedPrompts" :key="promptIndex" type="button" @click="$emit('quick-prompt', prompt)"><i class="fa-regular fa-lightbulb"></i>{{ prompt }}</button></div>
      </div>
    </div>
  </div>
</template>

<script setup>
import DOMPurify from 'dompurify'
import { aiActionDetails, aiActionLabel, aiActionStatusLabel, aiActionSummary, isReadOnlyAiAction, writeAiActions } from '@/utils/aiActionUi'

defineProps({
  message: { type: Object, required: true },
  profileAvatar: { type: String, default: '' },
  profileName: { type: String, default: 'Bạn' },
  profileInitials: { type: String, default: 'B' },
  canUpdateTask: { type: Boolean, default: false },
  canCreateTask: { type: Boolean, default: false }
})
defineEmits(['preview-attachment', 'open-citation', 'copy', 'continue', 'execute-action', 'cancel-action', 'retry-action', 'quick-prompt', 'confirm-suggested-action', 'create-suggested-task', 'create-all-suggested-tasks', 'open-duplicate-task', 'confirm-duplicate-creation'])

const formatBytes = bytes => {
  if (!Number.isFinite(bytes) || bytes <= 0) return '0 B'
  const units = ['B', 'KB', 'MB']; const unit = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1)
  const value = bytes / (1024 ** unit)
  return `${value >= 10 || unit === 0 ? value.toFixed(0) : value.toFixed(1)} ${units[unit]}`
}
const escapeHtml = value => `${value || ''}`.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#039;')
const renderMarkdown = (value = '') => {
  const source = `${value || ''}`.replace(/\r\n/g, '\n').trim(); if (!source) return ''
  const codeBlocks = []; let safe = escapeHtml(source).replace(/```([\w-]*)\n?([\s\S]*?)```/g, (_, language, code) => { const index = codeBlocks.push(`<pre><code class="language-${language || 'text'}">${code.trim()}</code></pre>`) - 1; return `@@CODE_BLOCK_${index}@@` })
  safe = safe.replace(/^### (.+)$/gm, '<h4>$1</h4>').replace(/^## (.+)$/gm, '<h3>$1</h3>').replace(/^# (.+)$/gm, '<h2>$1</h2>').replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>').replace(/__(.+?)__/g, '<strong>$1</strong>').replace(/\*([^*\n]+)\*/g, '<em>$1</em>').replace(/`([^`\n]+)`/g, '<code>$1</code>').replace(/^\s*[-*] (.+)$/gm, '<li>$1</li>').replace(/(<li>.*<\/li>\n?)+/g, '<ul>$&</ul>').replace(/^(\d+)\. (.+)$/gm, '<li><span class="md-list-index">$1.</span> $2</li>').replace(/\n{2,}/g, '</p><p>').replace(/\n/g, '<br>').replace(/@@CODE_BLOCK_(\d+)@@/g, (_, index) => codeBlocks[Number(index)])
  return DOMPurify.sanitize(`<p>${safe}</p>`, { USE_PROFILES: { html: true } })
}
const isReadOnlyAction = action => isReadOnlyAiAction(action?.type, action?.requiresConfirmation)
const writeActions = actions => writeAiActions(actions)
const hasReadOnlyActions = actions => actions.some(action => isReadOnlyAction(action))
</script>

<style scoped>
.ai-message { display: flex; align-items: flex-start; gap: 10px; min-width: 0; }.ai-message.user { flex-direction: row-reverse; }.ai-message-avatar { width: 32px; height: 32px; display: grid; flex: 0 0 32px; place-items: center; overflow: hidden; border: 1px solid var(--color-border); border-radius: 8px; background: var(--color-surface-hover); color: var(--color-accent); font-size: 11px; font-weight: 800; }.ai-message-avatar.is-user { background: var(--color-accent); color: var(--color-on-accent, #fff); }.ai-message-avatar img { width: 100%; height: 100%; object-fit: cover; }.ai-message-stack { display: grid; min-width: 0; width: min(100%, 760px); gap: 4px; }.ai-message.user .ai-message-stack { justify-items: end; }.ai-message-author { color: var(--color-text-muted); font-size: 10px; font-weight: 750; }.ai-message-bubble { min-width: 0; width: 100%; padding: 12px 14px; border: 1px solid var(--color-border); border-radius: 14px; background: var(--color-surface); color: var(--color-text-primary); line-height: 1.6; overflow-wrap: anywhere; }.ai-message.user .ai-message-bubble { width: auto; max-width: 100%; background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface)); border-color: color-mix(in srgb, var(--color-accent) 35%, var(--color-border)); }.ai-markdown :deep(p) { margin: 0 0 8px; }.ai-markdown :deep(p:last-child) { margin-bottom: 0; }.ai-markdown :deep(h2), .ai-markdown :deep(h3), .ai-markdown :deep(h4) { margin: 12px 0 6px; color: var(--color-text-primary); }.ai-markdown :deep(pre) { overflow: auto; margin: 9px 0; padding: 10px; border: 1px solid var(--color-border); border-radius: 8px; background: var(--color-bg); }.ai-markdown :deep(code) { padding: 1px 4px; border-radius: 4px; background: var(--color-surface-hover); color: var(--color-accent); font: .92em ui-monospace, monospace; }.ai-markdown :deep(pre code) { padding: 0; background: transparent; color: var(--color-text-primary); }.ai-message-tools { display: flex; justify-content: flex-end; gap: 4px; margin-top: 8px; }.ai-message-tools button { width: 28px; height: 28px; border: 0; border-radius: 6px; background: transparent; color: var(--color-text-muted); cursor: pointer; }.ai-message-tools button:hover, .ai-citations button:hover, .ai-suggested-prompts button:hover { color: var(--color-accent); }.ai-warnings { margin-top: 12px; padding: 10px; border: 1px solid color-mix(in srgb, var(--color-danger) 36%, var(--color-border)); border-radius: 8px; background: color-mix(in srgb, var(--color-danger) 8%, var(--color-surface)); color: var(--color-text-secondary); font-size: 11px; }.ai-warnings strong { color: var(--color-danger); }.ai-warnings ul { margin: 5px 0 0; padding-left: 17px; }.ai-citations { display: grid; gap: 6px; margin-top: 10px; padding-top: 9px; border-top: 1px solid var(--color-border); }.ai-citations > strong { color: var(--color-text-muted); font-size: 10px; text-transform: uppercase; }.ai-citations button { display: grid; gap: 2px; padding: 7px 8px; border: 1px solid var(--color-border); border-radius: 6px; background: transparent; color: var(--color-text-primary); text-align: left; cursor: pointer; }.ai-citations span { font-size: 11px; font-weight: 800; }.ai-citations small { color: var(--color-text-muted); font-size: 10px; }.ai-message-attachments { display: grid; gap: 8px; margin-bottom: 9px; }.ai-message-attachment-card { display: grid; grid-template-columns: 48px minmax(0, 1fr) 32px; align-items: center; gap: 9px; padding: 7px; border: 1px solid var(--color-border); border-radius: 8px; background: var(--color-surface-hover); }.ai-message-attachment-image { width: 48px; height: 42px; padding: 0; border: 1px solid var(--color-border); border-radius: 6px; background: var(--color-surface); color: var(--color-accent); overflow: hidden; cursor: pointer; }.ai-message-attachment-image img { width: 100%; height: 100%; object-fit: cover; }.ai-message-attachment-open { width: 32px; height: 32px; border: 0; border-radius: 6px; background: transparent; color: var(--color-text-secondary); cursor: pointer; }.ai-message-attachment-open:hover { background: var(--color-surface); color: var(--color-accent); }.ai-action-preview-list { display: grid; gap: 10px; margin-top: 12px; }.ai-activity-note { margin: 0; color: var(--color-success); font-size: 11px; }.ai-action-preview-card { padding: 14px; border: 1px solid var(--color-border); border-radius: 12px; background: var(--color-surface); }.ai-action-preview-card.is-pending { border-color: color-mix(in srgb, var(--color-accent) 42%, var(--color-border)); }.ai-action-preview-head, .ai-action-controls { display: flex; align-items: center; justify-content: space-between; gap: 10px; }.ai-action-eyebrow { display: block; margin-bottom: 3px; color: var(--color-accent); font-size: 9px; font-weight: 800; letter-spacing: .08em; }.ai-action-preview-head strong { color: var(--color-text-primary); font-size: 13px; }.ai-action-status { padding: 4px 7px; border-radius: 999px; background: var(--color-surface-hover); color: var(--color-text-secondary); font-size: 10px; font-weight: 800; }.ai-action-status.is-success { color: var(--color-success); }.ai-action-status.is-error { color: var(--color-danger); }.ai-action-description, .ai-action-result, .ai-action-error { margin: 9px 0; color: var(--color-text-secondary); font-size: 12px; }.ai-action-error { color: var(--color-danger); }.ai-action-result { color: var(--color-success); }.ai-action-details { display: grid; grid-template-columns: minmax(84px, auto) minmax(0, 1fr); gap: 4px 8px; margin: 0 0 11px; font-size: 11px; }.ai-action-details dt { color: var(--color-text-muted); }.ai-action-details dd { margin: 0; color: var(--color-text-primary); overflow-wrap: anywhere; }.ai-action-controls { justify-content: flex-end; }.ai-action-controls button { min-width: 72px; min-height: 32px; padding: 6px 11px; border-radius: 8px; font-size: 11px; font-weight: 800; cursor: pointer; }.ai-action-cancel { border: 1px solid var(--color-border); background: transparent; color: var(--color-text-secondary); }.ai-action-confirm { border: 1px solid var(--color-accent); background: var(--color-accent); color: var(--color-on-accent, #fff); }.ai-action-controls button:disabled { cursor: not-allowed; opacity: .55; }.ai-suggested-prompts { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 12px; padding-top: 9px; border-top: 1px dashed var(--color-border); }.ai-suggested-prompts button { padding: 6px 9px; border: 1px solid var(--color-border); border-radius: 999px; background: var(--color-surface-hover); color: var(--color-text-secondary); cursor: pointer; font-size: 11px; text-align: left; }
.ai-thinking-steps { display: grid; gap: 6px; margin-top: 10px; }
.ai-thinking-step { display: flex; align-items: center; gap: 8px; color: var(--color-text-muted); font-size: 11px; }
.ai-thinking-step.active { color: var(--color-text-secondary); }
.ai-suggested-actions, .ai-suggested-tasks { display: grid; gap: 8px; margin-top: 12px; padding: 10px; border: 1px solid var(--color-border); border-radius: 9px; background: var(--color-surface-hover); }.ai-suggested-action { display: grid; gap: 6px; padding: 8px; border: 1px solid var(--color-border); border-radius: 7px; background: var(--color-surface); }.ai-suggested-action p { margin: 0; color: var(--color-text-secondary); font-size: 11px; }.ai-suggested-action div, .ai-suggested-tasks-head, .ai-suggested-task > div, .ai-suggested-task-foot { display: flex; align-items: center; justify-content: space-between; gap: 8px; }.ai-suggested-action span, .ai-suggested-task strong { color: var(--color-text-primary); font-size: 11px; }.ai-suggested-action button, .ai-suggested-tasks-head button, .ai-suggested-task-foot button { min-height: 28px; padding: 5px 8px; border: 1px solid var(--color-accent); border-radius: 6px; background: transparent; color: var(--color-accent); cursor: pointer; font-size: 10px; font-weight: 750; }.ai-suggested-action button:disabled, .ai-suggested-tasks-head button:disabled, .ai-suggested-task-foot button:disabled { cursor: not-allowed; opacity: .55; }.ai-suggested-tasks-head strong { color: var(--color-text-primary); font-size: 11px; }.ai-suggested-task-list { display: grid; gap: 7px; max-height: 300px; overflow-y: auto; }.ai-suggested-task { display: grid; gap: 5px; padding: 8px; border: 1px solid var(--color-border); border-radius: 7px; background: var(--color-surface); }.ai-suggested-task > div > span { padding: 2px 5px; border-radius: 999px; background: color-mix(in srgb, var(--color-accent) 12%, var(--color-surface)); color: var(--color-accent); font-size: 9px; font-weight: 800; }.ai-suggested-task p, .ai-suggested-task small, .ai-permission-note { margin: 0; color: var(--color-text-muted); font-size: 10px; line-height: 1.45; }.ai-suggested-task-foot { padding-top: 6px; border-top: 1px solid var(--color-border); }.ai-suggested-task-foot .is-created { color: var(--color-success); font-size: 10px; font-weight: 750; }.ai-permission-note { color: var(--color-danger); text-align: center; }
.ai-duplicate-warning { display: grid; gap: 6px; margin: 8px 0 10px; padding: 9px; border: 1px solid color-mix(in srgb, var(--color-warning) 42%, var(--color-border)); border-radius: 7px; background: color-mix(in srgb, var(--color-warning) 8%, var(--color-surface)); }.ai-duplicate-warning strong { color: var(--color-warning); font-size: 11px; }.ai-duplicate-warning p { margin: 0; color: var(--color-text-secondary); font-size: 10px; }.ai-duplicate-warning > div { display: flex; flex-wrap: wrap; gap: 5px; }.ai-duplicate-warning button { padding: 5px 7px; border: 1px solid var(--color-border); border-radius: 5px; background: transparent; color: var(--color-text-secondary); cursor: pointer; font-size: 10px; }.ai-duplicate-warning button:last-child { color: var(--color-danger); }

.ai-message { gap: 11px; }
.ai-message-avatar { border-radius: 11px; background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface-hover)); box-shadow: 0 5px 12px color-mix(in srgb, var(--color-text-primary) 7%, transparent); }
.ai-message-avatar.is-user { background: linear-gradient(145deg, var(--color-accent), var(--sa-primary)); }
.ai-message-author { letter-spacing: .01em; }
.ai-message-bubble {
  padding: 14px 16px;
  border-color: color-mix(in srgb, var(--color-border) 90%, var(--color-accent));
  border-radius: 16px;
  background: color-mix(in srgb, var(--color-surface) 94%, var(--color-accent));
  box-shadow: 0 7px 18px color-mix(in srgb, var(--color-text-primary) 6%, transparent);
}
.ai-message.user .ai-message-bubble {
  background: linear-gradient(145deg, color-mix(in srgb, var(--color-accent) 15%, var(--color-surface)), color-mix(in srgb, var(--color-accent) 7%, var(--color-surface)));
}
.ai-markdown { color: var(--color-text-primary); }
.ai-markdown :deep(a) { color: var(--color-accent); text-decoration-color: color-mix(in srgb, var(--color-accent) 45%, transparent); }
.ai-markdown :deep(blockquote) { margin: 10px 0; padding: 8px 12px; border-left: 3px solid var(--color-accent); background: color-mix(in srgb, var(--color-accent) 6%, var(--color-surface)); color: var(--color-text-secondary); }
.ai-message-tools button, .ai-message-attachment-open { transition: background 160ms ease, color 160ms ease; }
.ai-message-tools button:hover, .ai-message-tools button:focus-visible { background: var(--sa-primary-soft); color: var(--color-accent); outline: none; }
.ai-action-preview-card {
  border-color: color-mix(in srgb, var(--color-accent) 25%, var(--color-border));
  border-radius: 14px;
  background: color-mix(in srgb, var(--color-accent) 5%, var(--color-surface));
  box-shadow: 0 8px 20px color-mix(in srgb, var(--color-text-primary) 6%, transparent);
}
.ai-action-preview-card.is-pending { box-shadow: 0 0 0 1px color-mix(in srgb, var(--color-accent) 12%, transparent), 0 10px 24px color-mix(in srgb, var(--color-accent) 8%, transparent); }
.ai-action-status { background: color-mix(in srgb, var(--color-surface-hover) 86%, var(--color-surface)); }
.ai-suggested-prompts button { transition: border-color 160ms ease, background 160ms ease, color 160ms ease; }
.ai-suggested-prompts button:hover { border-color: var(--color-accent); background: var(--sa-primary-soft); }
</style>
