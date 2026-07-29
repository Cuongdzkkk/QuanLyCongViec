# FE-COLLAB-REALTIME-01

## Scope

- Branch: `KhoiSigma`
- Frontend-only integration for Channel Chat and Direct Message.
- Backend, migrations, appsettings, package files, Admin realtime, Starred/Recent,
  and `useI18n.js` were not changed.
- Existing Foundation styling and dependencies were retained.

## Contract verified

- Authenticated hub: `/hubs/chat`
- Methods: `JoinChannel`, `LeaveChannel`, `JoinDirectConversation`,
  `LeaveDirectConversation`
- Events: `ChannelMessageCreated`, `DirectMessageCreated`
- Payload identity: `messageId`; scope: `channelId` or `conversationId`
- Server broadcasts only after REST persistence and includes the sender.
- REST remains the source of truth for sending and history.

## Implementation

- Added one centralized, lazy Collaboration SignalR singleton.
- Uses `accessTokenFactory` with the currently stored access token.
- Automatic reconnect rejoins only the currently active Channel or DM group.
- A successful rejoin triggers a scoped REST history refresh.
- Switching Chat, tab, or Project leaves the previous group and aborts stale
  history/list requests before applying the new context.
- History, REST send responses, realtime events, and reconnect refreshes merge
  by `messageId` and are sorted deterministically.
- Realtime payloads are rejected when their ID, sender, content, or active scope
  does not match the backend contract.
- Sender-side REST/realtime races do not duplicate messages or totals.
- Logout/token change and unmount stop the connection, clear active groups,
  unsubscribe handlers, abort requests, and clear user-scoped chat data.
- Join authorization failures remove inaccessible active items and reload their
  REST lists; generic realtime failures do not block REST usage.
- Added a compact accessible connection/reconnecting/error notice using existing
  theme variables. It wraps without horizontal overflow at the 390px target.
- Message content remains plain Vue interpolation; no `v-html`, presence,
  typing, read/unread, attachment realtime, or calling realtime was added.

## Verification

- `cd Frontend && npm run build`: PASS
- Vite transformed 4,531 modules and completed the production build.
- Existing non-fatal PWA bundle-mutation and large-chunk warnings remain.
- `git diff --check`: PASS
- Package manifest and lockfile unchanged.
- Browser smoke: NOT RUN.
- Runtime/migration readiness and a Collaboration realtime fixture were not
  available as verified prerequisites, so runtime behavior is not claimed PASS.

## Files

- `Frontend/src/services/collaborationRealtime.js`
- `Frontend/src/views/CollaborationChat.vue`
- `artifacts/frontend/FE_COLLAB_REALTIME_01_REPORT.md`
