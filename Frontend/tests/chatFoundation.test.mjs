import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const view = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')

const expects = [
  ['channel selection stays wired', "@click=\"selectChat(ch, 'channel')\""],
  ['text and voice groups are visible', 'TEXT CHANNELS', 'KÊNH THOẠI (VOICE)'],
  ['active channel state is visible', "activeChat?.type === 'channel'"],
  ['direct messages remain available', "selectChat(conversation, 'dm')", "selectDirectRecipient(member.id)"],
  ['composer keeps send behavior and attachments', '@click=\"sendMessage\"', 'attachment-preview-container'],
  ['context panel is toggleable', 'showMembersSidebar', 'toggleContextPanel'],
  ['AI entry points are truthful for text and calls', 'AI đang OFF', 'Sắp ra mắt'],
  ['AI is explicitly staged off', "const aiAnalysisOpen = ref(false)", 'AI đang OFF'],
  ['voice controls remain available', 'Voice connected', 'leaveVoiceChannel', 'callMicrophoneEnabled', 'isCallCameraOn'],
  ['camera streams use explicit video element binding', 'bindMediaElement', 'srcObject', 'syncCallVideoElements', 'playsInline'],
  ['remote video stays audible', ':ref="el => setRemoteVideoElement(el, user.connectionId, \'rail\')"', 'const setRemoteVideoElement = (element, connectionId, slot = \'rail\')'],
  ['camera stage is reactive and sized', 'hasVisibleCallVideo', 'call-camera-stage', 'min-height: 310px', 'object-fit: cover'],
  ['placeholder waits for all visual streams', 'v-else-if="hasVisibleCallVideo"', 'v-else class="call-grid-empty"'],
  ['screen share remains dominant', 'v-if="activePresenter"', 'activePresenterStream'],
  ['derived meeting layout modes stay centralized', 'const callLayoutMode = computed', 'getMeetingLayoutMode'],
  ['presentation keeps a participant rail', 'is-presentation-mode', 'grid-template-columns: minmax(0, 1fr) minmax(190px, 240px)', 'object-fit: contain'],
  ['responsive presentation becomes a camera strip', '@media (max-width: 900px)', 'flex-direction: row', 'overflow-x: auto'],
  ['controls expose predictable accessible states', 'Mic đang bật', 'Camera đang bật', 'call-control-future-slot', 'Rời cuộc gọi'],
  ['participant focus preserves presentation access', 'focusParticipant', 'is-focused-participant', 'Quay lại màn hình chia sẻ'],
  ['presentation consumes separate screen source', 'getLocalScreenStream', 'screenStream', 'cameraStream'],
  ['presentation controls stay in the stage layout', 'presentation-toolbar', 'grid-template-rows: auto minmax(0, 1fr) auto', 'presentation-control'],
  ['call chat keeps room context and real sender identity', 'activeVoiceChannel?.name', 'msg.senderAvatar', '`${msg.senderId}` === `${currentUser.id}`', 'call-chat-channel-name'],
  ['call chat composer supports clear, send, and newline behavior', 'call-chat-clear', '@keydown.enter.exact.prevent="sendCallChatMessage"', 'textarea'],
  ['unavailable transcript copy stays truthful', 'Trợ lý cuộc họp chưa sẵn sàng', 'Quản trị viên chưa cấu hình phiên âm cuộc họp.'],
  ['remote camera and screen sources stay separate', '?.cameraStream', '?.screenStream', '?.audioStream'],
  ['participant layout is dense and responsive', 'group-video-grid', 'grid-auto-flow: dense', '@media (max-width: 760px)'],
  ['call context tabs are present', 'class="context-tabs"', 'role="tablist"'],
  ['realtime and attachment services remain in use', 'collaborationRealtime', 'collaborationApi.downloadAttachment'],
]

for (const [name, ...needles] of expects) {
  for (const needle of needles) assert.ok(view.includes(needle), `${name}: missing ${needle}`)
}

assert.match(view, /const switchTab = \(tab\) =>[\s\S]{0,180}currentTab\.value = tab/)
assert.ok(view.includes('const openAiAnalysis = (scope = \'text\') =>'))
assert.ok(view.includes('aiAnalysisOpen.value = true'))
assert.ok(view.includes('openPreJoinVoiceChannel'))
const callChatFunction = view.slice(view.indexOf('const openVoiceChannelChat = async () => {'), view.indexOf('const isCanceledRequest'))
assert.doesNotMatch(callChatFunction, /createProjectChannel/)

console.log(`chatFoundation.test.mjs: ${expects.length} foundation checks passed`)
