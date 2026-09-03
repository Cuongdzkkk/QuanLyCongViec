import assert from 'node:assert/strict'
import { Buffer } from 'node:buffer'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { compile } from '@vue/compiler-dom'
import {
  computed,
  createRenderer,
  h,
  nextTick,
  shallowReactive,
  toRefs
} from 'vue'

const here = path.dirname(fileURLToPath(import.meta.url))
const viewSource = fs.readFileSync(path.join(here, '..', 'src', 'views', 'CollaborationChat.vue'), 'utf8')

class FakeTrack {
  constructor(kind, id) {
    this.kind = kind
    this.id = id
    this.enabled = true
    this.readyState = 'live'
  }
}

class FakeMediaStream {
  constructor(id, tracks) {
    this.id = id
    this.tracks = [...tracks]
  }

  getTracks() { return [...this.tracks] }
  getAudioTracks() { return this.tracks.filter(track => track.kind === 'audio') }
  getVideoTracks() { return this.tracks.filter(track => track.kind === 'video') }
}

const createHostNode = (type, tag = '') => ({
  type,
  tag,
  parent: null,
  children: [],
  props: {},
  text: ''
})

const createMediaNode = tag => ({
  ...createHostNode('element', tag),
  autoplay: false,
  muted: true,
  paused: true,
  playsInline: false,
  srcObject: null,
  volume: 0,
  play() {
    this.paused = false
    return Promise.resolve()
  }
})

const renderer = createRenderer({
  createElement: tag => ['audio', 'video'].includes(tag) ? createMediaNode(tag) : createHostNode('element', tag),
  createText: text => ({ ...createHostNode('text'), text }),
  createComment: text => ({ ...createHostNode('comment'), text }),
  setText: (node, text) => { node.text = text },
  setElementText: (node, text) => {
    node.children = [{ ...createHostNode('text'), parent: node, text }]
  },
  parentNode: node => node.parent,
  nextSibling: node => {
    const siblings = node.parent?.children || []
    return siblings[siblings.indexOf(node) + 1] || null
  },
  insert: (node, parent, anchor = null) => {
    if (node.parent) {
      const previousSiblings = node.parent.children
      const previousIndex = previousSiblings.indexOf(node)
      if (previousIndex >= 0) previousSiblings.splice(previousIndex, 1)
    }
    node.parent = parent
    const anchorIndex = anchor ? parent.children.indexOf(anchor) : -1
    if (anchorIndex >= 0) parent.children.splice(anchorIndex, 0, node)
    else parent.children.push(node)
  },
  remove: node => {
    const siblings = node.parent?.children || []
    const index = siblings.indexOf(node)
    if (index >= 0) siblings.splice(index, 1)
    node.parent = null
  },
  patchProp: (node, key, _previous, value) => {
    node.props[key] = value
    if (['autoplay', 'muted', 'playsInline', 'volume'].includes(key)) node[key] = value
  }
})

const walk = root => {
  const nodes = []
  const pending = [root]
  const seen = new Set()
  while (pending.length) {
    const node = pending.pop()
    if (!node || seen.has(node)) continue
    seen.add(node)
    nodes.push(node)
    const children = node.children || []
    for (let index = children.length - 1; index >= 0; index -= 1) pending.push(children[index])
  }
  return nodes
}
const nodesByTag = (root, tag) => walk(root).filter(node => node.tag === tag)
const nodesByClass = (root, className) => walk(root).filter(node => `${node.props?.class || ''}`.split(/\s+/).includes(className))

const remoteStreamsRef = { value: new Map() }
globalThis.__remoteDomView = { remoteStreamsRef }
const visibilityStart = viewSource.indexOf('const hasLiveVideoTrack =')
const visibilityEnd = viewSource.indexOf('const pictureInPictureUnsupportedMessage', visibilityStart)
const binderStart = viewSource.indexOf('const bindMediaElement =')
const binderEnd = viewSource.indexOf('const setPresentationVideoElement =', binderStart)
assert.ok(visibilityStart >= 0 && visibilityEnd > visibilityStart)
assert.ok(binderStart >= 0 && binderEnd > binderStart)

const transformedViewFunctions = `
const traceWebRtcMedia = () => {}
const isWebRtcDebugEnabled = () => false
const recordMediaElementDiagnostic = () => {}
const blockedMediaElements = new Set()
const localVideoElements = new Map()
const remoteVideoElements = new Map()
const remoteAudioElements = new Map()
const localCallStream = { value: null }
const callConnectionId = { value: 'B-connection' }
const isCallCameraOn = { value: false }
const remoteStreams = globalThis.__remoteDomView.remoteStreamsRef
const presentationVideoElement = { value: null }
const activePresenterStream = () => null
const activePresenter = { value: null }
${viewSource.slice(visibilityStart, visibilityEnd)}
${viewSource.slice(binderStart, binderEnd)}
export { isParticipantVideoVisible, setRemoteVideoElement, setRemoteAudioElement, syncCallVideoElements }
`
const {
  isParticipantVideoVisible,
  setRemoteAudioElement,
  setRemoteVideoElement,
  syncCallVideoElements
} = await import(`data:text/javascript;base64,${Buffer.from(transformedViewFunctions).toString('base64')}`)

const templateStart = viewSource.indexOf('<div v-else-if="hasCallParticipants" class="call-camera-stage"')
const templateEnd = viewSource.indexOf('<aside v-if="showTranscriptPanel"', templateStart)
assert.ok(templateStart >= 0 && templateEnd > templateStart, 'real CollaborationChat participant media template must remain available')
const mediaTemplate = viewSource
  .slice(templateStart, templateEnd)
  .replace('<div v-else-if="hasCallParticipants"', '<div v-if="hasCallParticipants"')
  .replace(/\s*<LiveCaptionOverlay[^>]*\/>\s*<\/section>/, '')
const { code: renderCode } = compile(mediaTemplate, { mode: 'function', prefixIdentifiers: true })
const render = new Function('Vue', renderCode)(await import('vue'))

const participant = (connectionId, displayName = 'User A') => ({
  connectionId,
  userId: 'A-user',
  displayName,
  avatarUrl: '',
  cameraEnabled: false,
  microphoneEnabled: true,
  handRaised: false
})

const mediaFor = connectionId => {
  const audioTrack = new FakeTrack('audio', `${connectionId}-audio`)
  const videoTrack = new FakeTrack('video', `${connectionId}-video`)
  return {
    audioStream: new FakeMediaStream(`${connectionId}-audio-stream`, [audioTrack]),
    cameraStream: new FakeMediaStream(`${connectionId}-camera-stream`, [videoTrack]),
    screenStream: new FakeMediaStream(`${connectionId}-screen-stream`, [])
  }
}

test('visible remote video and persistent audio follow the current connection through rerenders', async () => {
  const oldParticipant = participant('A-old')
  const oldMedia = mediaFor('A-old')
  const oldStreams = new Map([['A-old', oldMedia]])
  remoteStreamsRef.value = oldStreams

  const state = shallowReactive({
    activePresenter: null,
    callConnectionId: 'B-connection',
    callLayoutMode: 'CAMERA_GRID',
    callRailParticipants: [],
    callViewMode: 'tiled',
    cameraStageParticipants: [oldParticipant],
    currentUser: { avatar: '', name: 'User B' },
    focusedParticipantConnectionId: '',
    hasCallParticipants: true,
    isSharingScreen: false,
    participantsInCall: [oldParticipant],
    remoteStreams: oldStreams
  })
  const visibleCallStageParticipants = computed(() => state.callLayoutMode === 'CAMERA_GRID'
    ? state.participantsInCall.slice(0, 3)
    : state.cameraStageParticipants)
  const callOverflowCount = computed(() => state.callLayoutMode === 'CAMERA_GRID'
    ? Math.max(state.participantsInCall.length - visibleCallStageParticipants.value.length, 0)
    : 0)
  const remoteAudioParticipants = computed(() => state.participantsInCall.filter(user => {
    const stream = state.remoteStreams.get(user.connectionId)?.audioStream
    return user.connectionId !== state.callConnectionId &&
      stream?.getAudioTracks?.().some(track => track.readyState === 'live' && track.enabled !== false)
  }))
  const root = createHostNode('root', 'root')
  const app = renderer.createApp({
    render,
    setup: () => ({
      ...toRefs(state),
      visibleCallStageParticipants,
      callOverflowCount,
      remoteAudioParticipants,
      focusParticipant: () => {},
      isParticipantSpeaking: () => false,
      isParticipantVideoVisible,
      setLocalVideoElement: () => {},
      setRemoteAudioElement,
      setRemoteVideoElement
    })
  })
  app.component('el-avatar', { render: () => h('span', { class: 'avatar-stub' }) })
  app.mount(root)
  await nextTick()
  syncCallVideoElements()

  let videos = nodesByTag(root, 'video')
  let audios = nodesByTag(root, 'audio')
  assert.equal(videos.length, 1)
  assert.equal(videos[0].srcObject, oldMedia.cameraStream)
  assert.equal(nodesByClass(root, 'call-camera-off-state').length, 0)
  assert.equal(audios.length, 1)
  assert.equal(audios[0].srcObject, oldMedia.audioStream)
  assert.equal(audios[0].muted, false)
  assert.equal(audios[0].volume, 1)
  assert.equal(audios[0].paused, false)

  const persistentAudioElement = audios[0]
  state.cameraStageParticipants = [participant('A-old', 'User A updated')]
  state.participantsInCall = state.cameraStageParticipants
  await nextTick()
  assert.ok(nodesByTag(root, 'audio')[0] === persistentAudioElement, 'participant state updates must preserve the audio output element')

  state.cameraStageParticipants = []
  state.callLayoutMode = 'CAMERA_FOCUS'
  state.callRailParticipants = state.participantsInCall
  await nextTick()
  syncCallVideoElements()
  videos = nodesByTag(root, 'video')
  audios = nodesByTag(root, 'audio')
  assert.equal(videos.length, 1)
  assert.equal(videos[0].srcObject, oldMedia.cameraStream)
  assert.equal(audios.length, 1)
  assert.ok(audios[0] === persistentAudioElement, 'layout changes must not replace the playing audio output element')
  assert.equal(audios[0].srcObject, oldMedia.audioStream)
  assert.equal(audios[0].muted, false)
  assert.equal(audios[0].volume, 1)
  assert.equal(audios[0].paused, false)

  const newParticipant = participant('A-new')
  const newMedia = mediaFor('A-new')
  const newStreams = new Map([['A-new', newMedia]])
  remoteStreamsRef.value = newStreams
  state.remoteStreams = newStreams
  state.participantsInCall = [newParticipant]
  state.callRailParticipants = [newParticipant]
  await nextTick()
  syncCallVideoElements()

  videos = nodesByTag(root, 'video')
  audios = nodesByTag(root, 'audio')
  assert.ok(persistentAudioElement.parent === null, 'the replaced connection audio element must be removed')
  assert.equal(videos.length, 1)
  assert.equal(videos[0].srcObject, newMedia.cameraStream)
  assert.equal(audios.length, 1)
  assert.notEqual(audios[0], persistentAudioElement)
  assert.equal(audios[0].srcObject, newMedia.audioStream)
  assert.equal(audios[0].muted, false)
  assert.equal(audios[0].volume, 1)
  assert.equal(audios[0].paused, false)

  app.unmount()
})
