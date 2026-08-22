const VISION_WASM_URL = 'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@1.0.1/wasm'
const SELFIE_MODEL_URL = 'https://storage.googleapis.com/mediapipe-models/image_segmenter/selfie_segmenter/float16/latest/selfie_segmenter.tflite'
const PROCESS_WIDTH = 640
const PROCESS_HEIGHT = 360
const PROCESS_FPS = 15

let visionModulePromise = null

const loadSegmenter = async () => {
  if (!visionModulePromise) visionModulePromise = import('@mediapipe/tasks-vision')
  const { FilesetResolver, ImageSegmenter } = await visionModulePromise
  const fileset = await FilesetResolver.forVisionTasks(VISION_WASM_URL)
  return ImageSegmenter.createFromOptions(fileset, {
    baseOptions: {
      modelAssetPath: SELFIE_MODEL_URL,
      delegate: 'GPU'
    },
    runningMode: 'VIDEO',
    outputCategoryMask: true,
    outputConfidenceMasks: false
  })
}

const createMaskImage = (mask, context) => {
  const values = mask?.getAsUint8Array?.()
  if (!values?.length || !mask.width || !mask.height) return null
  const image = context.createImageData(mask.width, mask.height)
  for (let index = 0; index < values.length; index += 1) {
    const offset = index * 4
    const alpha = values[index] > 0 ? 255 : 0
    image.data[offset] = 255
    image.data[offset + 1] = 255
    image.data[offset + 2] = 255
    image.data[offset + 3] = alpha
  }
  return image
}

export const createBackgroundBlurProcessor = () => {
  let active = false
  let sourceVideo = null
  let sourceStream = null
  let outputStream = null
  let outputTrack = null
  let segmenter = null
  let frameHandle = null
  let canvas = null
  let canvasContext = null
  let personCanvas = null
  let personContext = null
  let maskCanvas = null
  let maskContext = null

  const stopFrameLoop = () => {
    if (frameHandle !== null) cancelAnimationFrame(frameHandle)
    frameHandle = null
  }

  const dispose = async () => {
    active = false
    stopFrameLoop()
    outputTrack?.stop()
    outputTrack = null
    outputStream = null
    segmenter?.close?.()
    segmenter = null
    if (sourceVideo) {
      sourceVideo.pause()
      sourceVideo.srcObject = null
    }
    sourceStream = null
    sourceVideo = null
    canvas = null
    canvasContext = null
    personCanvas = null
    personContext = null
    maskCanvas = null
    maskContext = null
  }

  const renderFrame = () => {
    if (!active || !sourceVideo || !canvasContext || !segmenter) return
    if (sourceVideo.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA) {
      const result = segmenter.segmentForVideo(sourceVideo, performance.now())
      const mask = result?.categoryMask
      const maskImage = createMaskImage(mask, maskContext)
      if (maskImage) {
        maskCanvas.width = mask.width
        maskCanvas.height = mask.height
        maskContext.putImageData(maskImage, 0, 0)

        canvasContext.clearRect(0, 0, PROCESS_WIDTH, PROCESS_HEIGHT)
        canvasContext.save()
        canvasContext.filter = 'blur(14px)'
        canvasContext.drawImage(sourceVideo, -10, -10, PROCESS_WIDTH + 20, PROCESS_HEIGHT + 20)
        canvasContext.restore()

        personContext.clearRect(0, 0, PROCESS_WIDTH, PROCESS_HEIGHT)
        personContext.globalCompositeOperation = 'source-over'
        personContext.drawImage(sourceVideo, 0, 0, PROCESS_WIDTH, PROCESS_HEIGHT)
        personContext.globalCompositeOperation = 'destination-in'
        personContext.drawImage(maskCanvas, 0, 0, PROCESS_WIDTH, PROCESS_HEIGHT)
        personContext.globalCompositeOperation = 'source-over'
        canvasContext.drawImage(personCanvas, 0, 0)
      }
      result?.close?.()
    }
    frameHandle = requestAnimationFrame(renderFrame)
  }

  const start = async rawTrack => {
    if (!rawTrack || typeof document === 'undefined' || typeof MediaStream === 'undefined') {
      throw new Error('Background blur requires a camera track.')
    }
    await dispose()
    sourceStream = new MediaStream([rawTrack])
    sourceVideo = document.createElement('video')
    sourceVideo.muted = true
    sourceVideo.playsInline = true
    sourceVideo.srcObject = sourceStream
    await sourceVideo.play()
    segmenter = await loadSegmenter()

    canvas = document.createElement('canvas')
    canvas.width = PROCESS_WIDTH
    canvas.height = PROCESS_HEIGHT
    canvasContext = canvas.getContext('2d', { alpha: false })
    personCanvas = document.createElement('canvas')
    personCanvas.width = PROCESS_WIDTH
    personCanvas.height = PROCESS_HEIGHT
    personContext = personCanvas.getContext('2d')
    maskCanvas = document.createElement('canvas')
    maskContext = maskCanvas.getContext('2d')
    outputStream = canvas.captureStream(PROCESS_FPS)
    outputTrack = outputStream.getVideoTracks()[0]
    if (!outputTrack) throw new Error('Background blur did not produce a video track.')
    active = true
    frameHandle = requestAnimationFrame(renderFrame)
    return outputTrack
  }

  return {
    start,
    dispose,
    isActive: () => active,
    getOutputTrack: () => outputTrack
  }
}

export const backgroundBlurConfig = {
  processWidth: PROCESS_WIDTH,
  processHeight: PROCESS_HEIGHT,
  processFps: PROCESS_FPS,
  modelUrl: SELFIE_MODEL_URL
}
