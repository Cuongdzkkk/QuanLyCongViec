# COLLAB-ATTACHMENT-01 Report

## A. Previous state

- Channel/DM entities only had unused legacy `AttachmentUrl` fields and no attachment metadata relation.
- `CollaborationChat.vue` selected one local file, created an object URL, and rendered a fake Channel-only attachment; it never sent the file or persisted metadata.
- Existing general uploads were owner-token scoped but could not authorize another Channel member or DM participant.

## B. Storage architecture

- Collaboration files are stored below ignored `Backend/src/TaskManagement.API/private-uploads/collaboration`.
- Disk names are random GUID keys plus the verified extension; original names are metadata only and never form a disk path.
- `UploadSecurity.ResolveUnderRoot` contains all resolution under the configured private root.
- Each upload is validated in memory, written to a temporary file, then atomically moved before message creation.
- Partial storage failure removes already-written files; database/message failure removes all files from that request.
- No content bytes, base64, physical path, or storage key is logged or returned to the client.

## C. Data model/migration

- Added `CollaborationMessageAttachment` with ID, exactly one nullable Channel/DM message FK, storage key, sanitized original name, verified content type, size, JWT uploader, and creation time.
- SQL check constraint enforces exactly one owner; message FKs cascade metadata and uploader FK restricts deletion.
- Storage key is unique; message-owner and uploader/time indexes are present.
- New additive migration: `20260804072953_AddCollaborationMessageAttachments`.
- `dotnet ef migrations has-pending-model-changes`: no pending model changes.
- Only this migration was applied to confirmed local SQL Server Testing; production and old migrations were not touched.

## D. API contract

- Existing JSON sends remain supported on the same Channel and DM message routes.
- The same routes accept `multipart/form-data` with optional `content` plus 1-5 `files`.
- Attachment-only messages are valid; empty JSON messages remain invalid.
- History/send DTOs include `attachmentId`, sanitized `originalFileName`, verified `contentType`, `sizeBytes`, and an authorized download endpoint.
- `GET /api/collaboration-attachments/{attachmentId}/content` supports authorized range streaming.
- Validation errors return sanitized 400 responses; storage/database failures return a sanitized 500 response.

## E. Authorization/security

- Sender/uploader is read only from the JWT; multipart input has no uploader field.
- Channel send permission and DM participant checks run before message metadata is committed.
- Every download resolves the attachment owner and re-runs Channel read or DM participant authorization.
- Unknown and unauthorized attachment IDs return 404, preventing GUID enumeration from revealing existence.
- Responses use `private, no-store`, `nosniff`, a sandbox CSP, and `Content-Disposition: attachment`.
- Allowed types: PNG, JPG/JPEG, WebP, PDF, UTF-8 TXT, DOCX, and XLSX; raw ZIP is deferred because no project ZIP policy exists.
- Client MIME is ignored for trust decisions; type is inferred from extension plus binary signature/OOXML archive structure.
- SVG, executable/script extensions, mismatched signatures, binary TXT, files over 10 MB, and more than five files are rejected.
- Traversal names are reduced to a sanitized Unicode leaf; storage never uses that leaf.

## F. Realtime DTO

- `ChannelMessageCreated` and `DirectMessageCreated` include only safe attachment metadata.
- No event contains storage key, disk path, or file bytes.
- Existing message-ID deduplication and read-state events remain unchanged.

## G. Frontend

- Removed the fake single-file attachment as source of truth.
- Picker supports up to five allowed files, client-side size/type feedback, a removable selection list, sizes, and local image previews.
- Selected files and text remain after failure; send is disabled while uploading and clears only after a backend success.
- Channel and DM both append only the real backend response and render all persisted attachment metadata.
- Images are fetched as authenticated blobs for preview; documents use authorized blob download, never a raw public URL.
- Object URLs are revoked on removal, selection/resource reset, and unmount.
- Cards/buttons have labels, keyboard-native controls, theme tokens, and a 390 px responsive layout.
- No attachment or message is persisted to local storage and no `v-html` was added.

## H. Tests/runtime A/B/C

- Backend build: PASS, 0 errors.
- Full backend suite: PASS, 281/281.
- Frontend production build: PASS; existing PWA/Rolldown and large-chunk warnings remain non-fatal.
- Unit coverage verifies type inference, signature rejection, Unicode/traversal sanitization, 10 MB limit, private random storage, containment, and cleanup on storage/database failure.
- Runtime USER_A sent Channel image/PDF and a DM Unicode TXT; USER_B reloaded metadata and downloaded all files.
- Runtime USER_C could neither read messages nor enumerate/download the Channel or DM attachment IDs.
- Runtime verified lying MIME values were replaced by verified types, six files/oversize/SVG were rejected, and range download returned 206.
- Runtime SignalR carried two attachment metadata records without private storage data.
- Read/unread, Channel, DM, SignalR reconnect, account switch, pagination, and sender identity regression checks remained PASS.
- Browser skill smoke: 390 px content, light/dark toggle, login redirect, no blank page or Vite overlay/page error.
- The authenticated attachment visual flow used runtime/API evidence because the fixture intentionally never emits credentials or tokens to a browser process.

## I. Cleanup/orphan handling

- Storage batch failure deletes earlier files and never calls message creation.
- Database/message failure deletes every stored file for that request.
- Fixture queries only run-scoped attachment metadata, deletes rows before messages, deletes those exact storage keys, and asserts no physical file remains.
- Automatic fixture cleanup passed after both attachment runtime runs.

## J. Files/commits

- Backend, SQL, API, security, tests, and runtime fixture: `eb2cd719 feat(collaboration): persist authorized message attachments`.
- Frontend integration and this report: `feat(frontend): connect collaboration attachments`.
- Package manifests, stashed `appsettings.json`, and stashed `useI18n.js` were unchanged.
- No push was performed.

## K. Deferred

- Raw ZIP remains disabled until a project-level archive policy and safe extraction/scanning rules exist.
- Malware scanning beyond allow-list, signature/OOXML validation, and safe download headers is deferred to a dedicated scanning integration.
- Mentions, typing/presence, voice/video, rich text, OAuth, AI, and payment remain out of scope.

## L. Decision

**PASS**

Files are private, metadata persists in SQL, authorization is rechecked, USER_C is blocked, dangerous/traversal input is handled, orphan cleanup works, history/realtime/download are real, runtime A/B/C passes, and all builds/tests pass.
