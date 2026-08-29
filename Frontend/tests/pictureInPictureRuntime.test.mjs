import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const view = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')

const unsupportedMessage = 'Trình duyệt của bạn không hỗ trợ Picture-in-Picture.'
const noVideoMessage = 'Hãy bật camera hoặc chia sẻ màn hình để sử dụng Picture-in-Picture.'
const handler = view.match(/const toggleCallPictureInPicture = async \(\) => \{[\s\S]*?\n\}/)?.[0] || ''
const menuItem = view.match(/<button\n\s+type="button"\n\s+class="call-more-menu-item"[\s\S]*?>Picture-in-picture<\/button>/)?.[0] || ''

assert.match(view, new RegExp(`const pictureInPictureUnsupportedMessage = '${unsupportedMessage}'`))
assert.match(view, new RegExp(`const pictureInPictureNoVideoMessage = '${noVideoMessage}'`))
assert.match(view, /const standardPictureInPictureSupported = \(\) =>[\s\S]*?document\.pictureInPictureEnabled === true/)
assert.match(view, /const hasEligiblePictureInPictureVideo = computed\(\(\) => \{[\s\S]*?activePresenter\.value[\s\S]*?callParticipants\.value\.some\(isParticipantVideoVisible\)/)
assert.match(menuItem, /:class="\{ 'is-unavailable': !hasEligiblePictureInPictureVideo \}"/)
assert.match(menuItem, /:aria-disabled="!hasEligiblePictureInPictureVideo"/)
assert.match(menuItem, /@click="toggleCallPictureInPicture"/)

// TEST 1: a browser with standard PiP but no camera/screen video never reaches the API.
assert.match(handler, /if \(!element\) \{[\s\S]*?showPictureInPictureMessage\(pictureInPictureNoVideoMessage\)[\s\S]*?return/)
assert.ok(handler.indexOf('if (!element)') < handler.indexOf('await element.requestPictureInPicture()'))
assert.match(handler, /ElMessage\.warning\(message\)/)

// TEST 2: capability failure has its own visible user-facing message.
assert.match(handler, /if \(!standardPictureInPictureSupported\(\)\) \{[\s\S]*?showPictureInPictureMessage\(pictureInPictureUnsupportedMessage\)[\s\S]*?return/)

// TEST 3: an eligible rendered video uses the standard video PiP API exactly once.
assert.match(handler, /const element = candidates\.find\(candidate =>[\s\S]*?candidate\?\.requestPictureInPicture && hasLiveVideoTrack\(candidate\.srcObject\)/)
assert.equal((handler.match(/await element\.requestPictureInPicture\(\)/g) || []).length, 1)

// TEST 4: the menu's unavailable state is driven by the reactive eligible-video computed value.
assert.match(menuItem, /!hasEligiblePictureInPictureVideo/)

// TEST 5: both unavailable branches and the API attempt close the More menu.
assert.match(handler, /const closeMoreMenu = \(\) => \{[\s\S]*?showMoreMenu\.value = false[\s\S]*?moreMenuSection\.value = ''/)
assert.match(handler, /const showPictureInPictureMessage = message => \{[\s\S]*?closeMoreMenu\(\)/)
assert.match(handler, /finally \{[\s\S]*?closeMoreMenu\(\)/)

console.log('PICTURE_IN_PICTURE_RUNTIME: 5 focused PiP UX regression contracts covered')
console.log('NO_VIDEO: visible guidance, no PiP API call, More menu closes')
console.log('UNSUPPORTED: visible standard-PiP capability guidance')
console.log('ELIGIBLE_VIDEO: standard requestPictureInPicture is called once')
