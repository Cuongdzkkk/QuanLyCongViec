export const CAPTION_TRANSPORT_HASH_ALGORITHM = 'SHA-256'

export const shouldSampleCaptionTransportChunk = chunkIndex =>
  chunkIndex === 1 || (chunkIndex > 0 && chunkIndex % 20 === 0)

export const computePcmSha256 = async bytes => {
  const input = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes)
  const exactBytes = input.buffer.slice(input.byteOffset, input.byteOffset + input.byteLength)
  const digest = await globalThis.crypto.subtle.digest(CAPTION_TRANSPORT_HASH_ALGORITHM, exactBytes)
  return Array.from(new Uint8Array(digest), value => value.toString(16).padStart(2, '0')).join('')
}

export const launchCaptionTransportClientDiagnostic = ({
  bytes,
  chunkIndex,
  callSessionId,
  projectId,
  voiceChannelId,
  enabled = true,
  hash = computePcmSha256,
  emit = (...args) => globalThis.console?.info?.(...args)
}) => {
  if (!enabled || !shouldSampleCaptionTransportChunk(chunkIndex)) return
  try {
    const diagnosticBytes = bytes.slice()
    void Promise.resolve()
      .then(() => hash(diagnosticBytes))
      .then(pcmSha256 => {
        emit?.('[CAPTION_TRANSPORT_CLIENT_DIAG]', {
          timestamp: new Date().toISOString(),
          callSessionId,
          projectId,
          voiceChannelId,
          chunkIndex,
          payloadBytes: diagnosticBytes.byteLength,
          pcmSha256
        })
      })
      .catch(() => {})
  } catch {
    // Diagnostics must never affect the audio submission path.
  }
}
