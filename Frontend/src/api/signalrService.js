import * as signalR from '@microsoft/signalr'
import { isExpectedNetworkError } from '@/utils/errorTelemetry'
import { getStoredAccessToken } from '@/utils/authSession'
import { configureRealtimeHub } from '@/services/realtimeHubConfig'

class SignalRService {
  constructor() {
    this.connection = null
    this.projectId = null
    this.projectIds = new Set()
    this.workspaceIds = new Set()
    this.startPromise = null
    this.handlers = new Map()
    this.connectionGeneration = 0
  }

  async startConnection(projectId) {
    if (!projectId) return false
    const id = `${projectId}`
    const isNewScope = !this.projectIds.has(id)
    this.projectIds.add(id)
    this.projectId = id
    const connected = await this.ensureConnection()
    if (connected && isNewScope) await this.joinGroup('JoinProjectGroup', id)
    return connected
  }

  async startAuthenticatedConnection() {
    return this.ensureConnection()
  }

  async startWorkspaceConnection(workspaceId) {
    if (!workspaceId) return false
    const id = `${workspaceId}`
    const isNewScope = !this.workspaceIds.has(id)
    this.workspaceIds.add(id)
    const connected = await this.ensureConnection()
    if (connected && isNewScope) await this.joinGroup('JoinWorkspaceGroup', id)
    return connected
  }

  async ensureConnection() {
    if ([signalR.HubConnectionState.Connected, signalR.HubConnectionState.Connecting, signalR.HubConnectionState.Reconnecting]
      .includes(this.connection?.state)) {
      return this.startPromise || true
    }
    if (this.startPromise) return this.startPromise

    const generation = ++this.connectionGeneration
    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5136/api'
    const hubBaseUrl = apiBaseUrl.replace(/\/api\/?$/, '')
    this.connection = configureRealtimeHub(new signalR.HubConnectionBuilder())
      .withUrl(`${hubBaseUrl}/kanban-hub`, {
        accessTokenFactory: () => getStoredAccessToken() || ''
      })
      .configureLogging(signalR.LogLevel.None)
      .build()

    this.bindRegisteredHandlers()
    this.connection.onreconnected(async () => {
      if (generation !== this.connectionGeneration) return
      await this.joinAllGroups()
      this.emitLocal('RealtimeReconnected', {
        projectIds: [...this.projectIds],
        workspaceIds: [...this.workspaceIds]
      })
    })
    this.connection.onclose(() => {
      if (generation === this.connectionGeneration) {
        this.emitLocal('RealtimeDisconnected', {
          projectIds: [...this.projectIds],
          workspaceIds: [...this.workspaceIds]
        })
      }
    })

    this.startPromise = (async () => {
      try {
        await this.connection.start()
        if (generation !== this.connectionGeneration || !this.connection) return false
        await this.joinAllGroups()
        this.emitLocal('RealtimeConnected', {
          projectIds: [...this.projectIds],
          workspaceIds: [...this.workspaceIds]
        })
        return true
      } catch (err) {
        if (!isExpectedNetworkError(err) && !(err.message && err.message.includes('405'))) {
          console.error('SignalR Connection Error:', err)
        }
        if (generation === this.connectionGeneration) this.connection = null
        return false
      } finally {
        if (generation === this.connectionGeneration) this.startPromise = null
      }
    })()

    return this.startPromise
  }

  async joinGroup(method, id) {
    if (this.connection?.state !== signalR.HubConnectionState.Connected) return
    try {
      await this.connection.invoke(method, id)
    } catch (err) {
      if (!isExpectedNetworkError(err)) console.error(`SignalR ${method} error:`, err)
    }
  }

  async joinAllGroups() {
    await Promise.all([
      ...[...this.projectIds].map(id => this.joinGroup('JoinProjectGroup', id)),
      ...[...this.workspaceIds].map(id => this.joinGroup('JoinWorkspaceGroup', id))
    ])
  }

  async stopConnection() {
    const connection = this.connection
    ++this.connectionGeneration
    this.connection = null
    this.projectId = null
    this.startPromise = null
    if (connection) await connection.stop()
    this.projectIds.clear()
    this.workspaceIds.clear()
  }

  on(eventName, callback) {
    if (!eventName || typeof callback !== 'function') return
    if (!this.handlers.has(eventName)) this.handlers.set(eventName, new Set())
    const callbacks = this.handlers.get(eventName)
    if (callbacks.has(callback)) return
    callbacks.add(callback)
    this.connection?.on(eventName, callback)
  }

  off(eventName, callback) {
    const callbacks = this.handlers.get(eventName)
    if (!callbacks) return
    if (callback) {
      callbacks.delete(callback)
      this.connection?.off(eventName, callback)
    } else {
      callbacks.clear()
      this.connection?.off(eventName)
    }
    if (!callbacks.size) this.handlers.delete(eventName)
  }

  bindRegisteredHandlers() {
    if (!this.connection) return
    for (const [eventName, callbacks] of this.handlers.entries()) {
      for (const callback of callbacks) this.connection.on(eventName, callback)
    }
  }

  emitLocal(eventName, payload) {
    for (const callback of this.handlers.get(eventName) || []) {
      try {
        callback(payload)
      } catch (err) {
        console.error(`SignalR local event "${eventName}" failed:`, err)
      }
    }
  }

  async sendProjectEvent(projectId, type, payload = {}) {
    if (!projectId || !type) return
    await this.startConnection(projectId)
    if (this.connection?.state !== signalR.HubConnectionState.Connected) return
    try {
      await this.connection.invoke('BroadcastProjectEvent', `${projectId}`, type, JSON.stringify(payload || {}))
    } catch (err) {
      if (!isExpectedNetworkError(err)) console.error('SignalR project event error:', err)
    }
  }
}

export const signalRService = new SignalRService()
