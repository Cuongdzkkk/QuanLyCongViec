import { defineStore } from 'pinia'
import axiosClient from '@/api/axiosClient'
import { signalRService } from '@/api/signalrService'
import { AUTH_SESSION_CHANGED, getStoredAccessToken, getStoredUserSession } from '@/utils/authSession'
import { getStickyAccountId, hasStickyAccountChanged } from '@/utils/stickyAccountIsolation'

let stickyRealtimeRegistered = false
let stickyAuthRegistered = false
let activeStickyStore = null

const getCurrentAccountId = () => {
  if (!getStoredAccessToken()) return ''
  return getStickyAccountId(getStoredUserSession())
}

const clearStickyState = (store, accountId = '') => {
  store.accountId = accountId
  store.requestEpoch += 1
  store.notes = []
  store.floatingNotes = []
  store.page = 1
  store.total = 0
  store.search = ''
  store.pinnedOnly = false
  store.loading = false
  store.loadingMore = false
  store.savingIds = []
  store.floatingSavingIds = []
  store.draggingNoteId = ''
  store.floatingLoading = false
  store.floatingError = ''
  store.error = ''
}

const handleStickyAuthSessionChanged = () => {
  if (!activeStickyStore) return
  const nextAccountId = getCurrentAccountId()
  if (hasStickyAccountChanged(activeStickyStore.accountId, nextAccountId)) {
    activeStickyStore.resetForAccountChange(nextAccountId)
  }
}

const handleStickyRealtime = event => {
  if (`${event?.entityType || ''}`.toLowerCase() !== 'stickynote' || !event?.entityId) return
  const action = `${event.action || ''}`.toLowerCase()
  const id = `${event.entityId}`
  if (action === 'deleted') {
    const existed = activeStickyStore?.notes.some(note => `${note.id}` === id)
    activeStickyStore.notes = activeStickyStore.notes.filter(note => `${note.id}` !== id)
    activeStickyStore.floatingNotes = activeStickyStore.floatingNotes.filter(note => `${note.id}` !== id)
    if (existed) activeStickyStore.total = Math.max(0, activeStickyStore.total - 1)
    return
  }
  if (!event.data) return
  const index = activeStickyStore?.notes.findIndex(note => `${note.id}` === id) ?? -1
  if (index >= 0) activeStickyStore.replaceNote(event.data)
  else if (action === 'created') {
    activeStickyStore.notes.unshift(event.data)
    activeStickyStore.total += 1
    if (event.data.isFloating) activeStickyStore.upsertFloatingNote(event.data)
  }
}

const unwrap = response => response?.data?.data ?? response?.data ?? response
export const MAX_FLOATING_STICKIES = 5

const toPayload = note => ({
  workspaceId: note.workspaceId || null,
  projectId: note.projectId || null,
  workTaskId: note.workTaskId || null,
  goalId: note.goalId || null,
  title: note.title?.trim() || 'Ghi chú mới',
  content: note.content || '',
  color: note.color || '#EAB308',
  isPinned: Boolean(note.isPinned),
  sourceRoute: note.sourceRoute || null
})

export const useStickyStore = defineStore('stickies', {
  state: () => ({
    accountId: '',
    requestEpoch: 0,
    notes: [],
    floatingNotes: [],
    page: 1,
    pageSize: 30,
    total: 0,
    search: '',
    pinnedOnly: false,
    loading: false,
    loadingMore: false,
    savingIds: [],
    floatingSavingIds: [],
    draggingNoteId: '',
    floatingLoading: false,
    floatingError: '',
    error: ''
  }),

  getters: {
    hasMore: state => state.notes.length < state.total,
    isSaving: state => id => state.savingIds.includes(id),
    isFloatingSaving: state => id => state.floatingSavingIds.includes(id),
    canAddFloating: state => state.floatingNotes.length < MAX_FLOATING_STICKIES
  },

  actions: {
    registerAuthSession() {
      activeStickyStore = this
      if (!stickyAuthRegistered && typeof window !== 'undefined') {
        window.addEventListener(AUTH_SESSION_CHANGED, handleStickyAuthSessionChanged)
        stickyAuthRegistered = true
      }

      const currentAccountId = getCurrentAccountId()
      if (hasStickyAccountChanged(this.accountId, currentAccountId)) {
        this.resetForAccountChange(currentAccountId)
      }
    },

    resetForAccountChange(accountId = '') {
      clearStickyState(this, accountId)
    },

    registerRealtime() {
      this.registerAuthSession()
      activeStickyStore = this
      if (stickyRealtimeRegistered) return
      signalRService.on('EntityChanged', handleStickyRealtime)
      stickyRealtimeRegistered = true
    },
    async fetchNotes({ reset = true } = {}) {
      this.registerRealtime()
      if (reset) {
        this.page = 1
        this.loading = true
      } else {
        if (!this.hasMore || this.loadingMore) return
        this.page += 1
        this.loadingMore = true
      }

      const requestEpoch = this.requestEpoch
      this.error = ''
      try {
        const response = await axiosClient.get('/stickies', {
          params: {
            page: this.page,
            pageSize: this.pageSize,
            search: this.search.trim() || undefined,
            pinned: this.pinnedOnly ? true : undefined
          }
        })
        if (requestEpoch !== this.requestEpoch) return []
        const data = unwrap(response)
        const items = Array.isArray(data?.items) ? data.items : []
        this.notes = reset
          ? items
          : [...this.notes, ...items.filter(item => !this.notes.some(note => note.id === item.id))]
        items.filter(item => item.isFloating).forEach(item => this.upsertFloatingNote(item))
        this.total = Number(data?.total || 0)
      } catch (error) {
        if (requestEpoch !== this.requestEpoch) return []
        if (!reset) this.page = Math.max(1, this.page - 1)
        this.error = error.response?.data?.message || error.response?.data?.data?.message || 'Không thể tải ghi chú.'
        throw error
      } finally {
        if (requestEpoch === this.requestEpoch) {
          this.loading = false
          this.loadingMore = false
        }
      }
    },

    async fetchFloatingNotes() {
      this.registerRealtime()
      const requestEpoch = this.requestEpoch
      this.floatingLoading = true
      this.floatingError = ''
      try {
        const response = await axiosClient.get('/stickies/floating')
        if (requestEpoch !== this.requestEpoch) return []
        const data = unwrap(response)
        this.floatingNotes = Array.isArray(data) ? data.slice(0, MAX_FLOATING_STICKIES) : []
        return this.floatingNotes
      } catch (error) {
        if (requestEpoch !== this.requestEpoch) return []
        this.floatingError = error.response?.data?.message || error.response?.data?.data?.message || 'Không thể tải ghi chú nổi.'
        throw error
      } finally {
        if (requestEpoch === this.requestEpoch) this.floatingLoading = false
      }
    },

    async createNote(note) {
      this.registerAuthSession()
      const requestEpoch = this.requestEpoch
      const response = await axiosClient.post('/stickies', toPayload(note))
      const created = unwrap(response)
      if (requestEpoch !== this.requestEpoch) return created
      const index = this.notes.findIndex(item => item.id === created.id)
      if (index >= 0) this.notes[index] = created
      else {
        this.notes.unshift(created)
        this.total += 1
      }
      return created
    },

    async updateNote(note) {
      if (!note?.id || this.savingIds.includes(note.id) || !this.findNote(note.id)) return note
      this.registerAuthSession()
      const requestEpoch = this.requestEpoch
      this.savingIds.push(note.id)
      try {
        const response = await axiosClient.put(`/stickies/${note.id}`, toPayload(note))
        const updated = unwrap(response)
        if (requestEpoch !== this.requestEpoch) return updated
        this.replaceNote(updated)
        return updated
      } finally {
        if (requestEpoch === this.requestEpoch) this.savingIds = this.savingIds.filter(id => id !== note.id)
      }
    },

    async setPinned(note, isPinned) {
      if (!note?.id || !this.findNote(note.id)) return
      this.registerAuthSession()
      const requestEpoch = this.requestEpoch
      const response = await axiosClient.patch(`/stickies/${note.id}/pin`, { isPinned })
      const updated = unwrap(response)
      if (requestEpoch !== this.requestEpoch) return updated
      this.replaceNote(updated)
      this.notes.sort((a, b) => Number(b.isPinned) - Number(a.isPinned) || new Date(b.updatedAt) - new Date(a.updatedAt))
      return updated
    },

    async deleteNote(id) {
      this.registerAuthSession()
      const requestEpoch = this.requestEpoch
      await axiosClient.delete(`/stickies/${id}`)
      if (requestEpoch !== this.requestEpoch) return
      this.notes = this.notes.filter(note => note.id !== id)
      this.floatingNotes = this.floatingNotes.filter(note => note.id !== id)
      this.total = Math.max(0, this.total - 1)
    },

    beginDrawerDrag(noteId) {
      this.draggingNoteId = noteId || ''
    },

    endDrawerDrag() {
      this.draggingNoteId = ''
    },

    findNote(id) {
      return this.notes.find(note => note.id === id) || this.floatingNotes.find(note => note.id === id) || null
    },

    async setFloatingState(note, state) {
      if (!note?.id || this.floatingSavingIds.includes(note.id) || !this.findNote(note.id)) return note
      this.registerAuthSession()
      const requestEpoch = this.requestEpoch
      if (state.isFloating && !note.isFloating && this.floatingNotes.length >= MAX_FLOATING_STICKIES) {
        const error = new Error('FLOATING_LIMIT')
        error.code = 'FLOATING_LIMIT'
        throw error
      }

      const previousNote = this.floatingNotes.find(item => item.id === note.id)
      const previous = previousNote ? { ...previousNote } : null
      const optimistic = {
        ...note,
        isFloating: Boolean(state.isFloating),
        positionX: state.positionX ?? note.positionX ?? null,
        positionY: state.positionY ?? note.positionY ?? null
      }

      if (optimistic.isFloating) this.upsertFloatingNote(optimistic)
      else this.floatingNotes = this.floatingNotes.filter(item => item.id !== note.id)

      this.floatingSavingIds.push(note.id)
      try {
        const response = await axiosClient.patch(`/stickies/${note.id}/floating-state`, {
          isFloating: optimistic.isFloating,
          positionX: optimistic.positionX,
          positionY: optimistic.positionY
        })
        const updated = unwrap(response)
        if (requestEpoch !== this.requestEpoch) return updated
        this.replaceNote(updated)
        return updated
      } catch (error) {
        if (requestEpoch === this.requestEpoch) {
          if (previous) this.upsertFloatingNote(previous)
          else this.floatingNotes = this.floatingNotes.filter(item => item.id !== note.id)
        }
        throw error
      } finally {
        if (requestEpoch === this.requestEpoch) this.floatingSavingIds = this.floatingSavingIds.filter(id => id !== note.id)
      }
    },

    upsertFloatingNote(updated) {
      const index = this.floatingNotes.findIndex(note => note.id === updated.id)
      if (index >= 0) this.floatingNotes[index] = updated
      else this.floatingNotes.push(updated)
    },

    replaceNote(updated) {
      const index = this.notes.findIndex(note => note.id === updated.id)
      if (index >= 0) this.notes[index] = updated
      if (updated.isFloating) this.upsertFloatingNote(updated)
      else this.floatingNotes = this.floatingNotes.filter(note => note.id !== updated.id)
    }
  }
})
