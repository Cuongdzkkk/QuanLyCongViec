const MAX_VISIBLE_PARTICIPANTS = 4

const pictureInPictureStyles = `
  :root { color-scheme: dark; font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
  * { box-sizing: border-box; }
  html, body { width: 100%; height: 100%; margin: 0; overflow: hidden; background: #071019; color: #f8fafc; }
  .meeting-pip { display: grid; grid-template-rows: auto minmax(0, 1fr); width: 100%; height: 100%; padding: 8px; gap: 7px; background: #071019; }
  .meeting-pip-header { display: flex; align-items: center; justify-content: space-between; min-width: 0; gap: 10px; padding: 0 2px; }
  .meeting-pip-title { display: flex; align-items: center; min-width: 0; gap: 7px; font-size: 11px; font-weight: 700; }
  .meeting-pip-live { width: 7px; height: 7px; flex: 0 0 auto; border-radius: 50%; background: #38d996; box-shadow: 0 0 0 3px rgba(56, 217, 150, .12); }
  .meeting-pip-status { overflow: hidden; color: #94a3b8; font-size: 10px; text-overflow: ellipsis; white-space: nowrap; }
  .meeting-pip-content { min-height: 0; }
  .meeting-pip-content[data-layout="PRESENTATION"] { display: grid; grid-template-rows: minmax(0, 1fr) minmax(58px, 30%); gap: 6px; }
  .meeting-pip-presentation { position: relative; min-height: 0; overflow: hidden; border: 1px solid rgba(148, 163, 184, .16); border-radius: 9px; background: #02070d; }
  .meeting-pip-presentation video { width: 100%; height: 100%; object-fit: contain; background: #02070d; }
  .meeting-pip-presenter { position: absolute; left: 7px; bottom: 6px; max-width: calc(100% - 14px); overflow: hidden; padding: 3px 6px; border-radius: 5px; background: rgba(2, 7, 13, .78); color: #e2e8f0; font-size: 9px; text-overflow: ellipsis; white-space: nowrap; }
  .meeting-pip-grid { display: grid; min-height: 0; height: 100%; gap: 6px; }
  .meeting-pip-grid[data-count="1"] { grid-template-columns: minmax(0, 1fr); }
  .meeting-pip-grid[data-count="2"] { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .meeting-pip-grid[data-count="3"], .meeting-pip-grid[data-count="4"] { grid-template-columns: repeat(2, minmax(0, 1fr)); grid-template-rows: repeat(2, minmax(0, 1fr)); }
  .meeting-pip-content[data-layout="PRESENTATION"] .meeting-pip-grid { grid-auto-flow: column; grid-auto-columns: minmax(82px, 1fr); grid-template-columns: none; grid-template-rows: minmax(0, 1fr); overflow: hidden; }
  .meeting-pip-tile { position: relative; display: grid; min-width: 0; min-height: 0; place-items: center; overflow: hidden; border: 1px solid rgba(148, 163, 184, .14); border-radius: 8px; background: #101b27; transition: border-color 140ms ease, background 140ms ease; }
  .meeting-pip-tile.is-speaking { border-color: rgba(56, 217, 150, .72); }
  .meeting-pip-tile video { width: 100%; height: 100%; object-fit: cover; background: #0a131e; }
  .meeting-pip-avatar { display: grid; width: clamp(28px, 23%, 48px); aspect-ratio: 1; place-items: center; overflow: hidden; border-radius: 10px; background: #1b3445; color: #c9f4df; font-size: clamp(12px, 4vw, 18px); font-weight: 750; }
  .meeting-pip-avatar img { width: 100%; height: 100%; object-fit: cover; }
  .meeting-pip-name { position: absolute; right: 5px; bottom: 5px; left: 5px; overflow: hidden; color: #f8fafc; font-size: 9px; font-weight: 650; text-overflow: ellipsis; text-shadow: 0 1px 3px #000; white-space: nowrap; }
  .meeting-pip-overflow { position: absolute; right: 7px; top: 7px; display: grid; min-width: 24px; height: 20px; place-items: center; padding: 0 5px; border-radius: 6px; background: rgba(2, 7, 13, .82); color: #e2e8f0; font-size: 9px; font-weight: 750; }
`

const hasLiveVideo = stream => stream?.getVideoTracks?.().some(track => track.readyState === 'live') === true

export const isDocumentPictureInPictureSupported = (targetWindow = globalThis.window) =>
  typeof targetWindow?.documentPictureInPicture?.requestWindow === 'function'

export const getMeetingPictureInPictureLayout = ({ hasPresentation = false, participantCount = 0 } = {}) => {
  if (hasPresentation) return 'PRESENTATION'
  if (participantCount <= 1) return 'SINGLE'
  if (participantCount === 2) return 'TWO_UP'
  return 'GRID'
}

const prioritizeParticipants = participants => [...participants].sort((left, right) => {
  const speakingDifference = Number(right.isSpeaking === true) - Number(left.isSpeaking === true)
  if (speakingDifference) return speakingDifference
  const videoDifference = Number(hasLiveVideo(right.cameraStream) && right.cameraEnabled !== false) -
    Number(hasLiveVideo(left.cameraStream) && left.cameraEnabled !== false)
  return videoDifference
})

const createVideo = (document, stream, mediaRole) => {
  const video = document.createElement('video')
  video.autoplay = true
  video.playsInline = true
  video.muted = true
  video.dataset.mediaRole = mediaRole
  video.srcObject = stream
  void video.play().catch(() => {})
  return video
}

const createParticipantTile = (document, participant) => {
  const tile = document.createElement('article')
  tile.className = `meeting-pip-tile${participant.isSpeaking ? ' is-speaking' : ''}`
  tile.dataset.connectionId = participant.connectionId || ''
  const cameraVisible = participant.cameraEnabled !== false && hasLiveVideo(participant.cameraStream)
  if (cameraVisible) {
    tile.append(createVideo(document, participant.cameraStream, 'camera'))
  } else {
    const avatar = document.createElement('span')
    avatar.className = 'meeting-pip-avatar'
    if (participant.avatarUrl) {
      const image = document.createElement('img')
      image.src = participant.avatarUrl
      image.alt = ''
      avatar.append(image)
    } else {
      avatar.textContent = participant.displayName?.trim()?.charAt(0)?.toUpperCase() || '?'
    }
    tile.append(avatar)
  }
  const name = document.createElement('span')
  name.className = 'meeting-pip-name'
  name.textContent = `${participant.displayName || 'Người tham gia'}${participant.isLocal ? ' (Bạn)' : ''}`
  tile.append(name)
  return tile
}

const renderSnapshot = (pipWindow, snapshot) => {
  const document = pipWindow.document
  const participants = prioritizeParticipants(Array.isArray(snapshot.participants) ? snapshot.participants : [])
  const visibleParticipants = participants.slice(0, MAX_VISIBLE_PARTICIPANTS)
  const presentation = snapshot.presentation && hasLiveVideo(snapshot.presentation.stream)
    ? snapshot.presentation
    : null
  const layout = getMeetingPictureInPictureLayout({
    hasPresentation: Boolean(presentation),
    participantCount: participants.length
  })

  const root = document.createElement('main')
  root.className = 'meeting-pip'
  root.dataset.layout = layout
  const header = document.createElement('header')
  header.className = 'meeting-pip-header'
  const title = document.createElement('span')
  title.className = 'meeting-pip-title'
  const live = document.createElement('span')
  live.className = 'meeting-pip-live'
  title.append(live, document.createTextNode(snapshot.meetingName || 'Cuộc họp đang diễn ra'))
  const status = document.createElement('span')
  status.className = 'meeting-pip-status'
  status.textContent = `${participants.length} người tham gia`
  header.append(title, status)

  const content = document.createElement('section')
  content.className = 'meeting-pip-content'
  content.dataset.layout = layout
  if (presentation) {
    const stage = document.createElement('div')
    stage.className = 'meeting-pip-presentation'
    stage.append(createVideo(document, presentation.stream, 'screen'))
    const presenter = document.createElement('span')
    presenter.className = 'meeting-pip-presenter'
    presenter.textContent = `${presentation.displayName || 'Người tham gia'} đang trình bày`
    stage.append(presenter)
    content.append(stage)
  }

  const grid = document.createElement('div')
  grid.className = 'meeting-pip-grid'
  grid.dataset.count = `${Math.max(1, visibleParticipants.length)}`
  visibleParticipants.forEach(participant => grid.append(createParticipantTile(document, participant)))
  if (participants.length > MAX_VISIBLE_PARTICIPANTS) {
    const overflow = document.createElement('span')
    overflow.className = 'meeting-pip-overflow'
    overflow.textContent = `+${participants.length - MAX_VISIBLE_PARTICIPANTS}`
    grid.append(overflow)
  }
  content.append(grid)
  root.append(header, content)
  document.body.replaceChildren(root)
}

export const createMeetingPictureInPictureController = ({ requestWindow } = {}) => {
  let pipWindow = null
  let pageHideHandler = null

  const disposeWindowBinding = () => {
    if (pipWindow && pageHideHandler) pipWindow.removeEventListener('pagehide', pageHideHandler)
    pageHideHandler = null
    pipWindow = null
  }

  return {
    isOpen: () => Boolean(pipWindow && !pipWindow.closed),
    async open(snapshot) {
      if (pipWindow && !pipWindow.closed) {
        renderSnapshot(pipWindow, snapshot)
        return pipWindow
      }
      const openWindow = requestWindow || (() => window.documentPictureInPicture.requestWindow({ width: 480, height: 320 }))
      pipWindow = await openWindow()
      pipWindow.document.head.replaceChildren()
      const title = pipWindow.document.createElement('title')
      title.textContent = 'SprintA Meeting'
      const style = pipWindow.document.createElement('style')
      style.textContent = pictureInPictureStyles
      pipWindow.document.head.append(title, style)
      pageHideHandler = disposeWindowBinding
      pipWindow.addEventListener('pagehide', pageHideHandler, { once: true })
      renderSnapshot(pipWindow, snapshot)
      return pipWindow
    },
    update(snapshot) {
      if (!pipWindow || pipWindow.closed) return false
      renderSnapshot(pipWindow, snapshot)
      return true
    },
    close() {
      if (!pipWindow) return
      const activeWindow = pipWindow
      disposeWindowBinding()
      if (!activeWindow.closed) activeWindow.close()
    }
  }
}
