import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const view = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')
const servicePath = path.join(here, '..', 'src', 'services', 'meetingPictureInPicture.js')
const serviceSource = fs.readFileSync(servicePath, 'utf8')
const service = await import(pathToFileURL(servicePath))

assert.equal(service.getMeetingPictureInPictureLayout({ participantCount: 1 }), 'SINGLE')
assert.equal(service.getMeetingPictureInPictureLayout({ participantCount: 2 }), 'TWO_UP')
assert.equal(service.getMeetingPictureInPictureLayout({ participantCount: 3 }), 'GRID')
assert.equal(service.getMeetingPictureInPictureLayout({ participantCount: 6 }), 'GRID')
assert.equal(service.getMeetingPictureInPictureLayout({ hasPresentation: true, participantCount: 2 }), 'PRESENTATION')

assert.match(serviceSource, /typeof targetWindow\?\.documentPictureInPicture\?\.requestWindow === 'function'/)
assert.match(serviceSource, /requestWindow\(\{ width: 480, height: 320 \}\)/)
assert.match(serviceSource, /visibleParticipants = participants\.slice\(0, MAX_VISIBLE_PARTICIPANTS\)/)
assert.match(serviceSource, /participants\.length - MAX_VISIBLE_PARTICIPANTS/)
assert.match(serviceSource, /video\.muted = true/)
assert.doesNotMatch(serviceSource, /createElement\('audio'\)|AudioContext/)
assert.match(serviceSource, /pagehide/)
assert.match(serviceSource, /removeEventListener\('pagehide'/)

const handler = view.match(/const toggleCallPictureInPicture = async \(\) => \{[\s\S]*?\n\}/)?.[0] || ''
assert.match(view, /createMeetingPictureInPictureController/)
assert.match(view, /isDocumentPictureInPictureSupported/)
assert.match(view, /const getMeetingPictureInPictureSnapshot = \(\) => \(\{[\s\S]*?participants:[\s\S]*?presentation:/)
assert.match(view, /watch\([\s\S]*?callParticipants[\s\S]*?remoteStreams[\s\S]*?localCallStream[\s\S]*?localScreenStream[\s\S]*?syncMeetingPictureInPicture/)
assert.match(handler, /if \(documentPictureInPictureSupported\(\)\)/)
assert.match(handler, /await meetingPictureInPicture\.open\(getMeetingPictureInPictureSnapshot\(\)\)/)
assert.match(handler, /await element\.requestPictureInPicture\(\)/)
assert.match(handler, /showPictureInPictureMessage\(pictureInPictureUnsupportedMessage\)/)
assert.match(handler, /showPictureInPictureMessage\(pictureInPictureNoVideoMessage\)/)
assert.match(view, /meetingPictureInPicture\.close\(\)[\s\S]*?traceCallHubLifecycle\('COMPONENT_UNMOUNT'/)

console.log('PICTURE_IN_PICTURE_RUNTIME: Document PiP single, two-up, grid, presentation, live-update, cleanup, and standard video fallback contracts covered')
