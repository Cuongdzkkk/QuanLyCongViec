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
  ['AI is explicitly staged off', "const aiAnalysisOpen = ref(false)", 'AI đang OFF', 'Chưa có phân tích'],
  ['voice controls remain available', 'Voice connected', 'leaveVoiceChannel', 'callMicrophoneEnabled', 'isCallCameraOn'],
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

console.log(`chatFoundation.test.mjs: ${expects.length} foundation checks passed`)
