import { computed, onBeforeUnmount, ref } from 'vue'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'

const MAX_ATTACHMENTS = 6
const IMAGE_MAX_BYTES = 5 * 1024 * 1024
const DOCUMENT_MAX_BYTES = 10 * 1024 * 1024
const VOICE_MAX_SECONDS = 60
const VOICE_MAX_BYTES = 3 * 1024 * 1024

const imageRules = {
  '.png': { label: 'PNG', mimeTypes: ['image/png'] }, '.jpg': { label: 'JPG', mimeTypes: ['image/jpeg'] },
  '.jpeg': { label: 'JPEG', mimeTypes: ['image/jpeg'] }, '.webp': { label: 'WEBP', mimeTypes: ['image/webp'] }
}
const documentRules = {
  '.pdf': { label: 'PDF', mimeTypes: ['application/pdf'] }, '.docx': { label: 'DOCX', mimeTypes: ['application/vnd.openxmlformats-officedocument.wordprocessingml.document'] },
  '.txt': { label: 'TXT', mimeTypes: ['text/plain'] }, '.md': { label: 'Markdown', mimeTypes: ['text/markdown', 'text/plain'] },
  '.csv': { label: 'CSV', mimeTypes: ['text/csv', 'application/csv', 'application/vnd.ms-excel'] }, '.xlsx': { label: 'XLSX', mimeTypes: ['application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'] },
  '.pptx': { label: 'PPTX', mimeTypes: ['application/vnd.openxmlformats-officedocument.presentationml.presentation'] }, '.json': { label: 'JSON', mimeTypes: ['application/json', 'text/json', 'text/plain'] }
}
const sourceExtensions = new Set(['.js', '.ts', '.vue', '.html', '.css', '.scss', '.cs', '.java', '.py', '.go', '.sql', '.xml', '.yaml', '.yml', '.sh', '.ps1'])

const extensionOf = (name = '') => {
  const index = name.lastIndexOf('.')
  return index >= 0 ? name.slice(index).toLowerCase() : ''
}
const formatBytes = bytes => {
  if (!Number.isFinite(bytes) || bytes <= 0) return '0 B'
  const units = ['B', 'KB', 'MB']; const unit = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1)
  const value = bytes / (1024 ** unit)
  return `${value >= 10 || unit === 0 ? value.toFixed(0) : value.toFixed(1)} ${units[unit]}`
}
const iconFor = extension => {
  if (extension === '.pdf') return 'fa-regular fa-file-pdf'
  if (extension === '.docx') return 'fa-regular fa-file-word'
  if (extension === '.xlsx' || extension === '.csv') return 'fa-regular fa-file-excel'
  if (extension === '.pptx') return 'fa-regular fa-file-powerpoint'
  if (extension === '.json' || sourceExtensions.has(extension)) return 'fa-regular fa-file-code'
  return 'fa-regular fa-file-lines'
}

export const useAiComposer = ({ workspaceId } = {}) => {
  const pendingAttachments = ref([])
  const composerDragActive = ref(false)
  const capturingScreenshot = ref(false)
  const voiceState = ref('idle')
  const voiceLanguage = ref('auto')
  const voiceTranscript = ref('')
  const voiceError = ref('')
  const voiceElapsedSeconds = ref(0)
  let recorder = null; let stream = null; let chunks = []; let timer = null; let startedAt = 0; let requestId = 0; let discard = false; let abortController = null

  const accept = [...Object.keys(imageRules), ...Object.keys(documentRules), ...sourceExtensions].join(',')
  const voiceLanguageLabel = computed(() => ({ auto: 'Tự động (VI/EN)', vi: 'Tiếng Việt', en: 'English' }[voiceLanguage.value] || 'Tự động (VI/EN)'))
  const voiceStatusTitle = computed(() => ({ requesting: 'Đang xin quyền microphone', recording: 'Đang ghi âm', transcribing: 'Đang nhận dạng giọng nói', success: 'Đã nhận transcript', error: 'Không thể nhận dạng giọng nói' }[voiceState.value] || 'Nhập bằng giọng nói'))
  const voiceElapsedLabel = computed(() => `${String(Math.floor(Math.min(VOICE_MAX_SECONDS, voiceElapsedSeconds.value) / 60)).padStart(2, '0')}:${String(Math.min(VOICE_MAX_SECONDS, voiceElapsedSeconds.value) % 60).padStart(2, '0')}`)
  const ruleFor = file => {
    const extension = extensionOf(file.name)
    if (imageRules[extension]) return { ...imageRules[extension], extension, kind: 'image', maxBytes: IMAGE_MAX_BYTES }
    if (documentRules[extension]) return { ...documentRules[extension], extension, kind: 'document', maxBytes: DOCUMENT_MAX_BYTES }
    if (sourceExtensions.has(extension)) return { extension, kind: 'document', label: `Source ${extension.slice(1).toUpperCase()}`, maxBytes: DOCUMENT_MAX_BYTES, sourceCode: true }
    return null
  }
  const mimeMatches = (file, rule) => {
    const mime = (file.type || '').toLowerCase(); if (!mime) return true
    if (rule.sourceCode) return mime.startsWith('text/') || ['application/javascript', 'application/json', 'application/xml', 'application/x-sh'].includes(mime)
    return rule.mimeTypes.some(item => item === mime)
  }
  const dimensions = url => new Promise((resolve, reject) => { const image = new Image(); image.onload = () => resolve({ width: image.naturalWidth, height: image.naturalHeight }); image.onerror = () => reject(new Error('Không thể đọc nội dung ảnh.')); image.src = url })
  const addPendingFiles = async (files, source = 'picker') => {
    for (const file of Array.from(files || [])) {
      if (pendingAttachments.value.length >= MAX_ATTACHMENTS) { ElMessage.error(`Chỉ được chọn tối đa ${MAX_ATTACHMENTS} tệp trong một lượt.`); break }
      const rule = ruleFor(file)
      if (!rule) { ElMessage.error(`Không hỗ trợ định dạng của tệp “${file.name || 'không tên'}”.`); continue }
      if (!file.size || file.size > rule.maxBytes) { ElMessage.error(!file.size ? `Tệp “${file.name}” không có dữ liệu.` : `${rule.kind === 'image' ? 'Ảnh' : 'Tài liệu'} “${file.name}” vượt quá giới hạn ${formatBytes(rule.maxBytes)}.`); continue }
      if (!mimeMatches(file, rule)) { ElMessage.error(`Loại nội dung “${file.type || 'không xác định'}” không khớp với ${rule.extension}.`); continue }
      if (pendingAttachments.value.some(item => item.name === file.name && item.size === file.size && item.file.lastModified === file.lastModified)) { ElMessage.info(`Tệp “${file.name}” đã có trong danh sách.`); continue }
      const previewUrl = URL.createObjectURL(file); let size = {}
      if (rule.kind === 'image') { try { size = await dimensions(previewUrl) } catch (error) { URL.revokeObjectURL(previewUrl); ElMessage.error(error.message); continue } }
      pendingAttachments.value.push({ id: crypto.randomUUID(), file, name: file.name, displayName: source === 'paste' ? 'Ảnh đã dán' : source === 'screenshot' ? 'Ảnh chụp màn hình' : file.name, size: file.size, kind: rule.kind, typeLabel: rule.label, icon: iconFor(rule.extension), previewUrl, status: 'pending', ...size })
    }
  }
  const removePendingAttachment = id => { const item = pendingAttachments.value.find(value => value.id === id); if (item?.previewUrl) URL.revokeObjectURL(item.previewUrl); pendingAttachments.value = pendingAttachments.value.filter(value => value.id !== id) }
  const clearPendingAttachments = () => { pendingAttachments.value.forEach(item => item.previewUrl && URL.revokeObjectURL(item.previewUrl)); pendingAttachments.value = [] }
  const openAttachmentPreview = async attachment => {
    if (!attachment) return
    try { if (!attachment.previewUrl && attachment.contentUrl) { const response = await axiosClient.get(attachment.contentUrl, { responseType: 'blob' }); attachment.previewUrl = URL.createObjectURL(response.data) } if (!attachment.previewUrl) return; const link = document.createElement('a'); link.href = attachment.previewUrl; link.target = '_blank'; link.rel = 'noopener noreferrer'; link.click() } catch { ElMessage.error(`Không thể mở “${attachment.name}”.`) }
  }
  const handleAttachmentInput = async event => { await addPendingFiles(event.target.files, 'picker'); event.target.value = '' }
  const handleComposerPaste = async event => { const files = Array.from(event.clipboardData?.files || []).filter(file => file.type.startsWith('image/')); if (files.length) { event.preventDefault(); await addPendingFiles(files, 'paste') } }
  const readClipboardImage = async () => {
    if (!navigator.clipboard?.read) { ElMessage.info('Trình duyệt này chưa hỗ trợ đọc ảnh clipboard. Hãy dùng Ctrl+V trong ô nhập.'); return }
    try { for (const item of await navigator.clipboard.read()) { const type = item.types.find(value => value.startsWith('image/')); if (!type) continue; const blob = await item.getType(type); const extension = type === 'image/jpeg' ? 'jpg' : type.split('/')[1]; await addPendingFiles([new File([blob], `anh-da-dan-${Date.now()}.${extension}`, { type, lastModified: Date.now() })], 'paste'); return } ElMessage.info('Clipboard không có ảnh được hỗ trợ.') } catch (error) { if (error?.name !== 'NotAllowedError') ElMessage.error('Không thể đọc ảnh từ clipboard.') }
  }
  const captureScreenAttachment = async () => {
    if (!navigator.mediaDevices?.getDisplayMedia || capturingScreenshot.value) { ElMessage.info('Trình duyệt này chưa hỗ trợ chụp màn hình.'); return }
    capturingScreenshot.value = true; let displayStream
    try { displayStream = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: false }); const video = document.createElement('video'); video.srcObject = displayStream; video.muted = true; await new Promise((resolve, reject) => { video.onloadedmetadata = resolve; video.onerror = reject; video.play().catch(reject) }); const canvas = document.createElement('canvas'); canvas.width = video.videoWidth; canvas.height = video.videoHeight; canvas.getContext('2d')?.drawImage(video, 0, 0); const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png')); if (!blob) throw new Error('Không thể tạo ảnh chụp màn hình.'); await addPendingFiles([new File([blob], `anh-chup-man-hinh-${Date.now()}.png`, { type: 'image/png', lastModified: Date.now() })], 'screenshot') } catch (error) { if (error?.name !== 'NotAllowedError') ElMessage.error(error.message || 'Không thể chụp màn hình.') } finally { displayStream?.getTracks().forEach(track => track.stop()); capturingScreenshot.value = false }
  }
  const handleAttachmentCommand = command => { if (command === 'browse') document.querySelector('.ai-composer-file-input')?.click(); if (command === 'paste') readClipboardImage(); if (command === 'screenshot') captureScreenAttachment() }
  const handleComposerDrop = async event => { composerDragActive.value = false; await addPendingFiles(event.dataTransfer?.files, 'drop') }
  const handleComposerDragLeave = event => { if (!event.currentTarget.contains(event.relatedTarget)) composerDragActive.value = false }

  const stopTracks = () => { stream?.getTracks().forEach(track => track.stop()); stream = null }
  const clearTimer = () => { if (timer) window.clearInterval(timer); timer = null }
  const releaseAudio = () => { chunks = []; startedAt = 0; voiceElapsedSeconds.value = 0 }
  const cancelVoiceInput = () => { requestId += 1; discard = true; abortController?.abort(); abortController = null; clearTimer(); stopTracks(); if (recorder?.state && recorder.state !== 'inactive') recorder.stop(); recorder = null; releaseAudio(); voiceTranscript.value = ''; voiceError.value = ''; voiceState.value = 'idle' }
  const encodeWave = (audioBuffer, sampleRate = 16000) => {
    const length = Math.max(1, Math.round(audioBuffer.duration * sampleRate)); const samples = new Float32Array(length); const channels = Array.from({ length: audioBuffer.numberOfChannels }, (_, index) => audioBuffer.getChannelData(index)); const step = audioBuffer.sampleRate / sampleRate
    for (let i = 0; i < length; i += 1) { const position = i * step; const source = Math.floor(position); const next = Math.min(source + 1, audioBuffer.length - 1); const fraction = position - source; let mixed = 0; channels.forEach(channel => { mixed += channel[source] + (channel[next] - channel[source]) * fraction }); samples[i] = mixed / channels.length }
    const buffer = new ArrayBuffer(44 + samples.length * 2); const view = new DataView(buffer); const write = (offset, value) => { for (let i = 0; i < value.length; i += 1) view.setUint8(offset + i, value.charCodeAt(i)) }; write(0, 'RIFF'); view.setUint32(4, 36 + samples.length * 2, true); write(8, 'WAVE'); write(12, 'fmt '); view.setUint32(16, 16, true); view.setUint16(20, 1, true); view.setUint16(22, 1, true); view.setUint32(24, sampleRate, true); view.setUint32(28, sampleRate * 2, true); view.setUint16(32, 2, true); view.setUint16(34, 16, true); write(36, 'data'); view.setUint32(40, samples.length * 2, true); samples.forEach((sample, index) => { const normalized = Math.max(-1, Math.min(1, sample)); view.setInt16(44 + index * 2, normalized < 0 ? normalized * 0x8000 : normalized * 0x7fff, true) }); return new Blob([buffer], { type: 'audio/wav' })
  }
  const transcribe = async recording => {
    try { const AudioContextClass = window.AudioContext || window.webkitAudioContext; if (!AudioContextClass) throw new Error('Trình duyệt không hỗ trợ xử lý audio để phiên âm.'); const context = new AudioContextClass(); let wave; try { wave = encodeWave(await context.decodeAudioData((await recording.arrayBuffer()).slice(0))) } finally { await context.close() } if (wave.size > VOICE_MAX_BYTES) throw new Error('Bản ghi âm vượt quá giới hạn 60 giây.'); if (voiceState.value !== 'transcribing') return; const form = new FormData(); form.append('audio', wave, 'voice-recording.wav'); form.append('languageMode', voiceLanguage.value); if (workspaceId?.value || workspaceId) form.append('workspaceId', workspaceId?.value || workspaceId); abortController = new AbortController(); const response = await axiosClient.post('/ai/transcribe-audio', form, { headers: { 'Content-Type': 'multipart/form-data' }, signal: abortController.signal }); if (voiceState.value !== 'transcribing') return; const transcript = String(response.data?.data?.transcript || response.data?.transcript || '').trim(); if (!transcript) throw new Error('Không nhận diện được giọng nói Việt hoặc Anh. Hãy thu lại.'); voiceTranscript.value = transcript; voiceState.value = 'success' } catch (error) { if (error?.code === 'ERR_CANCELED' || voiceState.value === 'idle') return; voiceError.value = error.response?.data?.message || error.message || 'Không thể nhận dạng giọng nói. Hãy thử lại.'; voiceState.value = 'error' } finally { abortController = null; releaseAudio() }
  }
  const stopVoiceRecording = () => { if (voiceState.value !== 'recording' || !recorder || recorder.state === 'inactive') return; voiceState.value = 'transcribing'; clearTimer(); stopTracks(); recorder.stop() }
  const startVoiceRecording = async () => {
    if (['requesting', 'recording', 'transcribing'].includes(voiceState.value)) return
    if (!navigator.mediaDevices?.getUserMedia || !window.MediaRecorder) { voiceError.value = 'Trình duyệt này không hỗ trợ ghi âm microphone.'; voiceState.value = 'error'; return }
    requestId += 1; const currentRequest = requestId; voiceTranscript.value = ''; voiceError.value = ''; discard = false; voiceState.value = 'requesting'
    try { const requestedStream = await navigator.mediaDevices.getUserMedia({ audio: true }); if (currentRequest !== requestId || voiceState.value !== 'requesting') { requestedStream.getTracks().forEach(track => track.stop()); return } const mime = ['audio/webm;codecs=opus', 'audio/webm', 'audio/ogg;codecs=opus'].find(type => MediaRecorder.isTypeSupported(type)); stream = requestedStream; recorder = mime ? new MediaRecorder(stream, { mimeType: mime }) : new MediaRecorder(stream); chunks = []; recorder.addEventListener('dataavailable', event => { if (!discard && event.data.size > 0) chunks.push(event.data) }); recorder.addEventListener('stop', () => { const recording = new Blob(chunks, { type: recorder?.mimeType || mime || 'audio/webm' }); recorder = null; if (discard || voiceState.value !== 'transcribing') { releaseAudio(); return } void transcribe(recording) }, { once: true }); recorder.start(250); startedAt = Date.now(); voiceElapsedSeconds.value = 0; voiceState.value = 'recording'; timer = window.setInterval(() => { voiceElapsedSeconds.value = Math.floor((Date.now() - startedAt) / 1000); if (voiceElapsedSeconds.value >= VOICE_MAX_SECONDS) stopVoiceRecording() }, 250) } catch (error) { stopTracks(); voiceError.value = error?.name === 'NotAllowedError' ? 'Quyền microphone đã bị từ chối. Hãy cho phép quyền trong trình duyệt rồi bấm Thử lại.' : error?.name === 'NotFoundError' ? 'Không tìm thấy microphone khả dụng trên thiết bị.' : 'Không thể mở microphone. Hãy kiểm tra thiết bị và thử lại.'; voiceState.value = 'error' }
  }
  const recordVoiceAgain = async () => { cancelVoiceInput(); await startVoiceRecording() }
  const useVoiceTranscript = () => { if (voiceTranscript.value.trim()) return voiceTranscript.value.trim() }
  onBeforeUnmount(() => { cancelVoiceInput(); clearPendingAttachments() })
  return { pendingAttachments, composerDragActive, capturingScreenshot, voiceState, voiceLanguage, voiceTranscript, voiceError, voiceElapsedLabel, voiceLanguageLabel, voiceStatusTitle, accept, addPendingFiles, removePendingAttachment, clearPendingAttachments, openAttachmentPreview, handleAttachmentInput, handleComposerPaste, handleAttachmentCommand, handleComposerDrop, handleComposerDragLeave, startVoiceRecording, stopVoiceRecording, cancelVoiceInput, recordVoiceAgain, useVoiceTranscript, formatBytes }
}
