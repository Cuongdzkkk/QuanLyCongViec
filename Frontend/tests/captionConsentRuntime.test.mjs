import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const view = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')

assert.match(view, /class="call-control-label-btn"[^>]*:class="\{ active: captionsEnabled \}"/)
assert.equal((view.match(/<span>Phụ đề<\/span>/g) || []).length, 1, 'only one visible caption enable control remains')
assert.match(view, /v-model="showCaptionConsentModal"/)
assert.match(view, /:close-on-click-modal="false"/)
assert.match(view, /Cho phép & bật phụ đề/)
assert.match(view, /@click="cancelCaptionConsent"/)
assert.match(view, /if \(captionsEnabled\.value\) \{[\s\S]*?await stopCallAi\(\)/)
assert.match(view, /if \(nextState\.state === 'ACTIVE'\) \{[\s\S]*?captionsEnabled\.value = true/)
assert.match(view, /callAiStateLabel = computed\(\(\) => \(\{[\s\S]*?ACTIVE: 'Đang ghi'/)
assert.doesNotMatch(view, /AI đang ghi lời nói thành văn bản/)

console.log('CAPTION_CONSENT_RUNTIME: single control, compact consent dialog, cancel cleanup, and deduplicated status covered')
