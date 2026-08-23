export const SIGNALR_RECONNECT_DELAYS_MS = Object.freeze([0, 2000, 10000, 30000])
export const SIGNALR_KEEP_ALIVE_MS = 15000
export const SIGNALR_SERVER_TIMEOUT_MS = 60000

export const configureRealtimeHub = builder => builder
  .withAutomaticReconnect(SIGNALR_RECONNECT_DELAYS_MS)
  .withKeepAliveIntervalInMilliseconds(SIGNALR_KEEP_ALIVE_MS)
  .withServerTimeoutInMilliseconds(SIGNALR_SERVER_TIMEOUT_MS)
