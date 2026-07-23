# SprintA P0-05 upload policy

| File class | Visibility | Storage and access policy |
|---|---|---|
| User avatar / profile cover | Public | Random internal filename; signature, MIME, extension and 5 MB validation; only `/uploads/avatars` and `/uploads/covers` are statically exposed. |
| Project cover | Public | Project-write permission; random internal filename; image signature validation; only `/uploads/project-covers` is statically exposed. |
| Task / project / polymorphic comment attachment | Private project scope | Stored outside the public static-file pipeline. Download uses `/api/private-attachments/comments/{id}` and requires active Workspace plus Project membership, or original comment ownership for non-project entities. |
| Generic editor/document upload | Private owner scope | Stored under `private-uploads/general`; authenticated data-protection token binds download to the owner. Files expire according to `Uploads:PrivateRetentionDays`. |
| AI attachment | Private user + Workspace + conversation scope | Existing `AiAttachmentService` uses `private-uploads/ai-attachments`, random filenames, content signatures, size/container checks and authorized content endpoints. Conversation deletion removes files. |
| Page attachment | Private through scoped comment/AI attachment flow | No anonymous static route. A dedicated page attachment entity is not currently present. |
| Integration attachment | Not supported | Integration records contain provider metadata/tokens, not uploaded file blobs. |
| Sticky attachment | Not supported | Current Sticky model has no attachment upload contract. |

The root `/uploads` path is explicitly denied. Only the three public image directories above are mapped before that deny rule. Client filenames are display metadata only and never become storage paths.
