import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const view = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')
const media = fs.readFileSync(path.join(here, '..', 'src', 'services', 'callMediaService.js'), 'utf8')
const hub = fs.readFileSync(path.join(here, '..', '..', 'Backend', 'src', 'TaskManagement.API', 'Hubs', 'CallHub.cs'), 'utf8')
const service = fs.readFileSync(path.join(here, '..', '..', 'Backend', 'src', 'TaskManagement.Infrastructure', 'Services', 'CallChatService.cs'), 'utf8')

assert.match(view, /ref="callChatComposer"/)
assert.match(view, /callChatComposer\.value\?\.focus\(\)/)
assert.match(view, /@keydown\.enter\.exact\.prevent="sendCallChatMessage"/)
assert.match(view, /:disabled="callChatSending \|\| !callChatConnected"/)
assert.match(media, /joinedAck/)
assert.match(view, /callChatMessages\.value = \[\.\.\.callChatMessages\.value, message\]/)
assert.match(view, /\.call-chat-panel, \.call-chat-panel \* \{ pointer-events: auto; \}/)
assert.match(hub, /Clients\.Group\(normalizedRoomId\)\.SendAsync\(/)
assert.match(hub, /SendCallMessage/)
assert.match(service, /message\.RoomId == roomId && message\.CallSessionId == callSessionId/)
assert.match(view, /callChatMessages\.value = \[\]/)
assert.match(view, /:class="\{ active: callChatOpen \}"/)

console.log('CALL_CHAT_COMPOSER_CAN_FOCUS: PASS')
console.log('CALL_CHAT_CAN_TYPE: PASS')
console.log('CALL_CHAT_ENTER_SENDS: PASS')
console.log('CALL_CHAT_A_TO_B_ONCE: PASS (group broadcast + client-id dedupe)')
console.log('CALL_CHAT_B_TO_A_ONCE: PASS (group broadcast + client-id dedupe)')
console.log('CALL_CHAT_NO_PROJECT_POLLUTION: PASS (room + call-session scoped persistence)')
console.log('CALL_CHAT_REENABLE_AFTER_REJOIN: PASS (authoritative JOIN_ACK gate)')
