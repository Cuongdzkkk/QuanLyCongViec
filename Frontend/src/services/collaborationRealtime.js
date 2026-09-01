import * as signalR from '@microsoft/signalr'
import { AUTH_SESSION_CHANGED, getCurrentAccessToken, waitForAuthReady } from '@/utils/authSession'
import { createCurrentAccessTokenFactory } from '@/utils/authTransport'
import { configureRealtimeHub } from '@/services/realtimeHubConfig'

export const COLLABORATION_REALTIME_EVENTS = Object.freeze({
  CHANNEL_MESSAGE_CREATED: 'ChannelMessageCreated',
  DIRECT_MESSAGE_CREATED: 'DirectMessageCreated',
  READ_STATE_CHANGED: 'CollaborationReadStateChanged',
  MENTION_CREATED: 'CollaborationMentionCreated',
  CHANNEL_MESSAGE_REACTION_CHANGED: 'ChannelMessageReactionChanged',
  CHANNEL_MESSAGE_PIN_CHANGED: 'ChannelMessagePinChanged'
})

export const COLLABORATION_REALTIME_STATES = Object.freeze({
  CONNECTING: 'connecting',
  CONNECTED: 'connected',
  RECONNECTING: 'reconnecting',
  DISCONNECTED: 'disconnected',
  ERROR: 'error'
})

const HUB_ERROR_CODES = Object.freeze([
  'AUTH_REQUIRED',
  'USER_INACTIVE',
  'CHANNEL_NOT_FOUND_OR_FORBIDDEN',
  'CONVERSATION_NOT_FOUND_OR_FORBIDDEN',
  'INVALID_ID',
  'JOIN_FAILED'
])

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

const createClientError = (code) => {
  const error = new Error(code)
  error.code = code
  return error
}

export const getCollaborationHubErrorCode = (error) => {
  if (HUB_ERROR_CODES.includes(error?.code)) return error.code
  const message = `${error?.message || ''}`.toUpperCase()
  return HUB_ERROR_CODES.find(code => message.includes(code)) || 'JOIN_FAILED'
}

class CollaborationRealtimeService {
  constructor() {
    this.connection = null
    this.startPromise = null
    this.stopPromise = null
    this.activeChannelId = null
    this.activeConversationId = null
    this.lifecycleVersion = 0
    this.intentionalStop = false
    this.channelSubscribers = new Set()
    this.directSubscribers = new Set()
    this.readStateSubscribers = new Set()
    this.mentionSubscribers = new Set()
    this.reactionSubscribers = new Set()
    this.pinSubscribers = new Set()
    this.stateSubscribers = new Set()
    this.reconnectedSubscribers = new Set()
    if (typeof window !== 'undefined') {
      window.addEventListener(AUTH_SESSION_CHANGED, () => {
        if (!getCurrentAccessToken()) void this.stop()
      })
    }
  }

  get state() {
    return this.connection?.state || signalR.HubConnectionState.Disconnected
  }

  get isConnected() {
    return this.state === signalR.HubConnectionState.Connected
  }

  buildConnection() {
    if (this.connection) return this.connection

    const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5136/api'
    const hubBaseUrl = apiBaseUrl.replace(/\/api\/?$/, '')
    const connection = configureRealtimeHub(new signalR.HubConnectionBuilder(), getCurrentAccessToken)
      .withUrl(`${hubBaseUrl}/hubs/chat`, {
        accessTokenFactory: createCurrentAccessTokenFactory(getCurrentAccessToken)
      })
      .configureLogging(signalR.LogLevel.None)
      .build()

    connection.on(
      COLLABORATION_REALTIME_EVENTS.CHANNEL_MESSAGE_CREATED,
      payload => this.channelSubscribers.forEach(handler => handler(payload))
    )
    connection.on(
      COLLABORATION_REALTIME_EVENTS.DIRECT_MESSAGE_CREATED,
      payload => this.directSubscribers.forEach(handler => handler(payload))
    )
    connection.on(
      COLLABORATION_REALTIME_EVENTS.READ_STATE_CHANGED,
      payload => this.readStateSubscribers.forEach(handler => handler(payload))
    )
    connection.on(
      COLLABORATION_REALTIME_EVENTS.MENTION_CREATED,
      payload => this.mentionSubscribers.forEach(handler => handler(payload))
    )
    connection.on(
      COLLABORATION_REALTIME_EVENTS.CHANNEL_MESSAGE_REACTION_CHANGED,
      payload => this.reactionSubscribers.forEach(handler => handler(payload))
    )
    connection.on(
      COLLABORATION_REALTIME_EVENTS.CHANNEL_MESSAGE_PIN_CHANGED,
      payload => this.pinSubscribers.forEach(handler => handler(payload))
    )
    connection.onreconnecting(() => {
      if (!this.intentionalStop) this.emitState(COLLABORATION_REALTIME_STATES.RECONNECTING)
    })
    connection.onreconnected(() => this.handleReconnected())
    connection.onclose(() => {
      if (!this.intentionalStop) this.emitState(COLLABORATION_REALTIME_STATES.DISCONNECTED)
    })

    this.connection = connection
    return connection
  }

  async start() {
    await waitForAuthReady()
    if (!getCurrentAccessToken()) throw createClientError('AUTH_REQUIRED')
    if ([signalR.HubConnectionState.Connected, signalR.HubConnectionState.Connecting, signalR.HubConnectionState.Reconnecting]
      .includes(this.state)) return this.startPromise
    if (this.startPromise) return this.startPromise
    if (this.stopPromise) await this.stopPromise

    const connection = this.buildConnection()
    const version = this.lifecycleVersion
    this.intentionalStop = false
    this.emitState(COLLABORATION_REALTIME_STATES.CONNECTING)
    this.startPromise = connection.start()
      .then(() => {
        if (version === this.lifecycleVersion) {
          this.emitState(COLLABORATION_REALTIME_STATES.CONNECTED)
        }
      })
      .catch((error) => {
        const code = getCollaborationHubErrorCode(error)
        this.emitState(COLLABORATION_REALTIME_STATES.ERROR, { code })
        throw createClientError(code)
      })
      .finally(() => {
        this.startPromise = null
      })
    return this.startPromise
  }

  async stop() {
    if (this.stopPromise) return this.stopPromise
    this.lifecycleVersion += 1
    this.intentionalStop = true
    const connection = this.connection
    const channelId = this.activeChannelId
    const conversationId = this.activeConversationId
    this.activeChannelId = null
    this.activeConversationId = null

    if (!connection) {
      this.emitState(COLLABORATION_REALTIME_STATES.DISCONNECTED)
      this.intentionalStop = false
      return
    }

    this.stopPromise = (async () => {
      if (this.startPromise) {
        try {
          await this.startPromise
        } catch {
          // A failed start is already reported; continue clearing the connection.
        }
      }
      if (connection.state === signalR.HubConnectionState.Connected) {
        await this.invokeLeave('LeaveChannel', channelId)
        await this.invokeLeave('LeaveDirectConversation', conversationId)
      }
      await connection.stop()
      if (this.connection === connection) this.connection = null
      this.emitState(COLLABORATION_REALTIME_STATES.DISCONNECTED)
    })().finally(() => {
      this.stopPromise = null
      this.intentionalStop = false
    })
    return this.stopPromise
  }

  async joinChannel(channelId) {
    this.validateId(channelId)
    if (this.state === signalR.HubConnectionState.Reconnecting) {
      this.activeConversationId = null
      this.activeChannelId = channelId
      return
    }
    await this.start()
    if (this.activeChannelId === channelId) return
    if (this.activeConversationId) {
      await this.leaveDirectConversation(this.activeConversationId)
    }
    if (this.activeChannelId) await this.leaveChannel(this.activeChannelId)
    await this.invokeJoin('JoinChannel', channelId, 'channel')
    this.activeChannelId = channelId
  }

  async leaveChannel(channelId = this.activeChannelId) {
    if (!channelId) return
    if (this.isConnected) await this.invokeLeave('LeaveChannel', channelId)
    if (this.activeChannelId === channelId) this.activeChannelId = null
  }

  async joinDirectConversation(conversationId) {
    this.validateId(conversationId)
    if (this.state === signalR.HubConnectionState.Reconnecting) {
      this.activeChannelId = null
      this.activeConversationId = conversationId
      return
    }
    await this.start()
    if (this.activeConversationId === conversationId) return
    if (this.activeChannelId) await this.leaveChannel(this.activeChannelId)
    if (this.activeConversationId) {
      await this.leaveDirectConversation(this.activeConversationId)
    }
    await this.invokeJoin('JoinDirectConversation', conversationId, 'dm')
    this.activeConversationId = conversationId
  }

  async leaveDirectConversation(conversationId = this.activeConversationId) {
    if (!conversationId) return
    if (this.isConnected) {
      await this.invokeLeave('LeaveDirectConversation', conversationId)
    }
    if (this.activeConversationId === conversationId) {
      this.activeConversationId = null
    }
  }

  subscribeChannelMessage(handler) {
    this.channelSubscribers.add(handler)
    return () => this.channelSubscribers.delete(handler)
  }

  subscribeDirectMessage(handler) {
    this.directSubscribers.add(handler)
    return () => this.directSubscribers.delete(handler)
  }

  subscribeReadState(handler) {
    this.readStateSubscribers.add(handler)
    return () => this.readStateSubscribers.delete(handler)
  }

  subscribeMention(handler) {
    this.mentionSubscribers.add(handler)
    return () => this.mentionSubscribers.delete(handler)
  }

  subscribeReaction(handler) {
    this.reactionSubscribers.add(handler)
    return () => this.reactionSubscribers.delete(handler)
  }

  subscribePin(handler) {
    this.pinSubscribers.add(handler)
    return () => this.pinSubscribers.delete(handler)
  }

  subscribeState(handler) {
    this.stateSubscribers.add(handler)
    return () => this.stateSubscribers.delete(handler)
  }

  subscribeReconnected(handler) {
    this.reconnectedSubscribers.add(handler)
    return () => this.reconnectedSubscribers.delete(handler)
  }

  emitState(state, detail = {}) {
    this.stateSubscribers.forEach(handler => handler({ state, ...detail }))
  }

  async handleReconnected() {
    const version = this.lifecycleVersion
    const channelId = this.activeChannelId
    const conversationId = this.activeConversationId
    const errors = []

    if (channelId) {
      try {
        await this.invokeJoin('JoinChannel', channelId, 'channel')
      } catch (error) {
        if (this.activeChannelId === channelId) this.activeChannelId = null
        errors.push({ scope: 'channel', id: channelId, code: error.code })
      }
    }
    if (conversationId) {
      try {
        await this.invokeJoin('JoinDirectConversation', conversationId, 'dm')
      } catch (error) {
        if (this.activeConversationId === conversationId) {
          this.activeConversationId = null
        }
        errors.push({ scope: 'dm', id: conversationId, code: error.code })
      }
    }
    if (version !== this.lifecycleVersion) return

    this.emitState(
      errors.length ? COLLABORATION_REALTIME_STATES.ERROR : COLLABORATION_REALTIME_STATES.CONNECTED,
      errors.length ? { code: errors[0].code, scope: errors[0].scope } : { reconnected: true }
    )
    this.reconnectedSubscribers.forEach(handler => handler({ errors }))
  }

  async invokeJoin(method, id, scope) {
    try {
      await this.connection.invoke(method, id)
    } catch (error) {
      const code = getCollaborationHubErrorCode(error)
      this.emitState(COLLABORATION_REALTIME_STATES.ERROR, { code, scope })
      throw createClientError(code)
    }
  }

  async invokeLeave(method, id) {
    if (!id || !this.connection || !this.isConnected) return
    try {
      await this.connection.invoke(method, id)
    } catch {
      // Leaving is best effort; stop or the server disconnect removes this connection.
    }
  }

  validateId(id) {
    if (!uuidPattern.test(`${id || ''}`)) throw createClientError('INVALID_ID')
  }
}

export const collaborationRealtime = new CollaborationRealtimeService()
