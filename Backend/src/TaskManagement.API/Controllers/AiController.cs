using System.IO;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.DTOs.Project;
using TaskManagement.Application.DTOs.WorkTask;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Rules;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AiController : ControllerBase
    {
        private readonly IAiService _aiService;
        private readonly IAiAttachmentService _aiAttachmentService;
        private readonly IWorkTaskService _workTaskService;
        private readonly IProjectService _projectService;
        private readonly IGoalService _goalService;
        private readonly ApplicationDbContext _dbContext;
        private const int GeminiRetryAttempts = 3;
        private const long AiDocumentMaxBytes = 10 * 1024 * 1024;
        private const long VoiceAudioMaxBytes = 3 * 1024 * 1024;
        private static readonly IReadOnlyDictionary<string, string[]> AiDocumentMimeTypes =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [".txt"] = ["text/plain"],
                [".md"] = ["text/markdown", "text/plain"],
                [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
                [".pdf"] = ["application/pdf"]
            };
        private readonly IResourceAuthorizationService _authorizationService;

        public AiController(
            IAiService aiService,
            IAiAttachmentService aiAttachmentService,
            IWorkTaskService workTaskService,
            IProjectService projectService,
            IGoalService goalService,
            ApplicationDbContext dbContext,
            IResourceAuthorizationService authorizationService)
        {
            _aiService = aiService;
            _aiAttachmentService = aiAttachmentService;
            _workTaskService = workTaskService;
            _projectService = projectService;
            _goalService = goalService;
            _dbContext = dbContext;
            _authorizationService = authorizationService;
        }

        [HttpGet("usage")]
        public async Task<IActionResult> Usage()
        {
            var userId = GetUserId();
            var usage = await _aiService.GetUsageAsync(userId);
            return Ok(ApiResponse<AiUsageDto>.Success(usage));
        }

        [HttpGet("usage-summary")]
        public async Task<IActionResult> UsageSummary([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            var userId = GetUserId();
            var start = from?.ToUniversalTime() ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = to?.ToUniversalTime() ?? DateTime.UtcNow;
            if (end < start)
            {
                return BadRequest(ApiResponse<object>.Error("Invalid usage date range."));
            }

            var tokenRows = await _dbContext.AITokenUsages
                .AsNoTracking()
                .Where(row => row.UserId == userId && row.CreatedAt >= start && row.CreatedAt <= end)
                .GroupBy(row => row.FeatureCode)
                .Select(group => new
                {
                    featureCode = group.Key,
                    requests = group.Count(),
                    tokensUsed = group.Sum(row => row.TokensUsed)
                })
                .OrderByDescending(row => row.tokensUsed)
                .ToListAsync();

            var ledgerRows = await _dbContext.AiUsageLedgerEntries
                .AsNoTracking()
                .Where(row => row.UserId == userId && row.OccurredAt >= start && row.OccurredAt <= end)
                .GroupBy(row => row.ActionType)
                .Select(group => new
                {
                    actionType = group.Key,
                    requests = group.Count(),
                    creditsConsumed = group.Sum(row => row.CreditsConsumed),
                    providerTokens = group.Sum(row => row.ProviderTokens ?? 0)
                })
                .OrderByDescending(row => row.creditsConsumed)
                .ToListAsync();

            var totalTokens = tokenRows.Sum(row => row.tokensUsed);
            var totalCredits = ledgerRows.Count > 0
                ? ledgerRows.Sum(row => row.creditsConsumed)
                : EstimateCredits(totalTokens);
            return Ok(ApiResponse<object>.Success(new
            {
                period = new { from = start, to = end },
                totalRequests = ledgerRows.Count > 0 ? ledgerRows.Sum(row => row.requests) : tokenRows.Sum(row => row.requests),
                totalTokens,
                creditsConsumed = totalCredits,
                remainingIncludedCredits = Math.Max(0, 100 - totalCredits),
                calculation = new
                {
                    tokenUnit = 1000,
                    rounding = "ceil",
                    note = ledgerRows.Count > 0
                        ? "Credits are read from real AiUsageLedgerEntries rows. Provider cost is not guessed."
                        : "No usage ledger rows found for this period; credits are estimated from real AITokenUsages rows."
                },
                byAction = ledgerRows,
                byFeature = tokenRows.Select(row => new
                {
                    row.featureCode,
                    row.requests,
                    row.tokensUsed,
                    estimatedCredits = EstimateCredits(row.tokensUsed)
                })
            }));
        }

        [HttpGet("usage-ledger")]
        public async Task<IActionResult> UsageLedger([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var userId = GetUserId();
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var ledgerQuery = _dbContext.AiUsageLedgerEntries
                .AsNoTracking()
                .Where(row => row.UserId == userId)
                .OrderByDescending(row => row.OccurredAt);

            var total = await ledgerQuery.CountAsync();
            var items = await ledgerQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(row => new
                {
                    row.Id,
                    row.WorkspaceId,
                    row.ProjectId,
                    row.ActionType,
                    row.CreditsConsumed,
                    row.ProviderTokens,
                    row.IdempotencyKey,
                    row.OccurredAt
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(new { page, pageSize, total, items }));
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations(
            [FromQuery] Guid? workspaceId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetUserId();
            var resolvedWorkspaceId = await ResolveActionWorkspaceAsync(userId, workspaceId);
            await EnsureWorkspaceWriteAccessAsync(userId, resolvedWorkspaceId);
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var query = _dbContext.AiConversations
                .AsNoTracking()
                .Where(item => item.UserId == userId && item.WorkspaceId == resolvedWorkspaceId)
                .OrderByDescending(item => item.UpdatedAt);
            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(item => new { item.Id, item.Title, item.WorkspaceId, item.CreatedAt, item.UpdatedAt })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(new { page, pageSize, total, items }));
        }

        [HttpPost("conversations")]
        public async Task<IActionResult> CreateConversation([FromBody] AiConversationCreateRequest request)
        {
            var userId = GetUserId();
            var workspaceId = await ResolveActionWorkspaceAsync(userId, request.WorkspaceId);
            await EnsureWorkspaceWriteAccessAsync(userId, workspaceId);
            var now = DateTime.UtcNow;
            var conversation = new AiConversation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                WorkspaceId = workspaceId,
                Title = NormalizeConversationTitle(request.Title),
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.AiConversations.Add(conversation);
            await _dbContext.SaveChangesAsync();
            return Ok(ApiResponse<object>.Success(new { conversation.Id, conversation.Title, conversation.WorkspaceId, conversation.CreatedAt, conversation.UpdatedAt }));
        }

        [HttpGet("conversations/{id:guid}")]
        public async Task<IActionResult> GetConversation(Guid id)
        {
            var userId = GetUserId();
            var conversation = await _dbContext.AiConversations
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
            if (conversation == null) return NotFound(ApiResponse<object>.Error("Conversation does not exist."));
            await EnsureWorkspaceWriteAccessAsync(userId, conversation.WorkspaceId);
            return Ok(ApiResponse<object>.Success(new
            {
                conversation.Id,
                conversation.Title,
                conversation.WorkspaceId,
                conversation.CreatedAt,
                conversation.UpdatedAt,
                messages = ReadConversationMessages(conversation.MessagesJson)
            }));
        }

        [HttpPut("conversations/{id:guid}")]
        public async Task<IActionResult> SaveConversation(Guid id, [FromBody] AiConversationSaveRequest request)
        {
            var userId = GetUserId();
            var conversation = await _dbContext.AiConversations
                .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
            if (conversation == null) return NotFound(ApiResponse<object>.Error("Conversation does not exist."));
            await EnsureWorkspaceWriteAccessAsync(userId, conversation.WorkspaceId);
            if (request.Messages.ValueKind != JsonValueKind.Array)
            {
                return BadRequest(ApiResponse<object>.Error("Messages must be a JSON array."));
            }

            var messagesJson = request.Messages.GetRawText();
            if (Encoding.UTF8.GetByteCount(messagesJson) > 1024 * 1024)
            {
                return BadRequest(ApiResponse<object>.Error("Conversation content exceeds 1 MB."));
            }

            conversation.MessagesJson = messagesJson;
            if (!string.IsNullOrWhiteSpace(request.Title)) conversation.Title = NormalizeConversationTitle(request.Title);
            conversation.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return Ok(ApiResponse<object>.Success(new { conversation.Id, conversation.Title, conversation.UpdatedAt }));
        }

        [HttpPatch("conversations/{id:guid}/title")]
        public async Task<IActionResult> RenameConversation(Guid id, [FromBody] AiConversationRenameRequest request)
        {
            var userId = GetUserId();
            var conversation = await _dbContext.AiConversations
                .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
            if (conversation == null) return NotFound(ApiResponse<object>.Error("Conversation does not exist."));
            await EnsureWorkspaceWriteAccessAsync(userId, conversation.WorkspaceId);
            conversation.Title = NormalizeConversationTitle(request.Title);
            conversation.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return Ok(ApiResponse<object>.Success(new { conversation.Id, conversation.Title, conversation.UpdatedAt }));
        }

        [HttpDelete("conversations/{id:guid}")]
        public async Task<IActionResult> DeleteConversation(Guid id)
        {
            var userId = GetUserId();
            var conversation = await _dbContext.AiConversations
                .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
            if (conversation == null) return NotFound(ApiResponse<object>.Error("Conversation does not exist."));
            await EnsureWorkspaceWriteAccessAsync(userId, conversation.WorkspaceId);
            var attachmentIds = await _dbContext.AiAttachments
                .Where(item => item.ConversationId == id && item.UserId == userId)
                .Select(item => item.Id)
                .ToListAsync();
            foreach (var attachmentId in attachmentIds)
            {
                await _aiAttachmentService.DeleteAsync(userId, attachmentId);
            }
            _dbContext.AiConversations.Remove(conversation);
            await _dbContext.SaveChangesAsync();
            return Ok(ApiResponse<object>.Success(new { id }));
        }

        [HttpPost("attachments")]
        [RequestSizeLimit(12 * 1024 * 1024)]
        public async Task<IActionResult> UploadAttachment(
            [FromForm] IFormFile file,
            [FromForm] Guid conversationId,
            [FromForm] Guid? workspaceId,
            CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0 || conversationId == Guid.Empty)
            {
                return BadRequest(ApiResponse<object>.Error("File và conversationId là bắt buộc."));
            }

            try
            {
                var userId = GetUserId();
                var resolvedWorkspaceId = await ResolveActionWorkspaceAsync(userId, workspaceId);
                await using var stream = file.OpenReadStream();
                var attachment = await _aiAttachmentService.UploadAsync(
                    userId,
                    resolvedWorkspaceId,
                    conversationId,
                    file.FileName,
                    file.ContentType,
                    stream,
                    file.Length,
                    cancellationToken);
                return Ok(ApiResponse<AiAttachmentDto>.Success(attachment, "Attachment đã được xử lý."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Error(ex.Message));
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpGet("attachments/{id:guid}")]
        public async Task<IActionResult> GetAttachment(Guid id, CancellationToken cancellationToken)
        {
            var attachment = await _aiAttachmentService.GetAsync(GetUserId(), id, cancellationToken);
            return attachment == null
                ? NotFound(ApiResponse<object>.Error("Attachment không tồn tại."))
                : Ok(ApiResponse<AiAttachmentDto>.Success(attachment));
        }

        [HttpGet("attachments/{id:guid}/content")]
        public async Task<IActionResult> GetAttachmentContent(Guid id, CancellationToken cancellationToken)
        {
            var attachment = await _aiAttachmentService.GetContentAsync(GetUserId(), id, cancellationToken);
            if (attachment == null) return NotFound(ApiResponse<object>.Error("Attachment không tồn tại."));
            Response.Headers.CacheControl = "private, no-store";
            return File(attachment.Bytes, attachment.MimeType, attachment.FileName, enableRangeProcessing: true);
        }

        [HttpDelete("attachments/{id:guid}")]
        public async Task<IActionResult> DeleteAttachment(Guid id, CancellationToken cancellationToken)
        {
            await _aiAttachmentService.DeleteAsync(GetUserId(), id, cancellationToken);
            return Ok(ApiResponse<object>.Success(new { id }));
        }

        [HttpPost("attachment-chat")]
        [EnableRateLimiting("FixedWindow")]
        public async Task<IActionResult> ChatWithAttachments(
            [FromBody] AiAttachmentChatRequest request,
            CancellationToken cancellationToken)
        {
            if (request.ConversationId == Guid.Empty || request.AttachmentIds.Count == 0)
            {
                return BadRequest(ApiResponse<object>.Error("Conversation và attachment là bắt buộc."));
            }

            try
            {
                var userId = GetUserId();
                var workspaceId = await ResolveActionWorkspaceAsync(userId, request.WorkspaceId);
                var response = await _aiAttachmentService.ChatAsync(
                    userId,
                    workspaceId,
                    request.ConversationId,
                    request.AttachmentIds,
                    request.Message ?? string.Empty,
                    cancellationToken);
                return Ok(ApiResponse<AiAttachmentChatResponseDto>.Success(response));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Error(ex.Message));
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpPost("transcribe-audio")]
        [EnableRateLimiting("FixedWindow")]
        [RequestSizeLimit(VoiceAudioMaxBytes + 128 * 1024)]
        public async Task<IActionResult> TranscribeAudio(
            [FromForm] IFormFile audio,
            [FromForm] string languageMode,
            CancellationToken cancellationToken)
        {
            if (audio == null || audio.Length == 0)
            {
                return BadRequest(ApiResponse<object>.Error("Bản ghi âm là bắt buộc."));
            }

            var normalizedLanguage = (languageMode ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedLanguage is not ("auto" or "vi" or "en"))
            {
                return BadRequest(ApiResponse<object>.Error("Ngôn ngữ giọng nói chỉ hỗ trợ Tự động (VI/EN), Tiếng Việt hoặc English."));
            }

            if (audio.Length > VoiceAudioMaxBytes)
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge,
                    ApiResponse<object>.Error("Bản ghi âm vượt quá giới hạn 60 giây.", StatusCodes.Status413PayloadTooLarge));
            }

            var mimeType = (audio.ContentType ?? string.Empty).Split(';', 2)[0].Trim().ToLowerInvariant();
            if (mimeType is not ("audio/wav" or "audio/x-wav" or "audio/wave"))
            {
                return BadRequest(ApiResponse<object>.Error("Định dạng bản ghi âm không hợp lệ. Chỉ chấp nhận WAV."));
            }

            await using var stream = audio.OpenReadStream();
            using var buffer = new MemoryStream((int)audio.Length);
            await stream.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();
            if (!IsWaveAudio(bytes))
            {
                return BadRequest(ApiResponse<object>.Error("Nội dung bản ghi âm WAV không hợp lệ."));
            }

            try
            {
                var transcript = await _aiService.TranscribeAudioAsync(
                    GetUserId(), normalizedLanguage, "audio/wav", bytes, cancellationToken);
                if (string.IsNullOrWhiteSpace(transcript))
                {
                    return BadRequest(ApiResponse<object>.Error("Không nhận diện được giọng nói Việt hoặc Anh. Hãy thu lại ở nơi yên tĩnh hơn."));
                }

                Response.Headers.CacheControl = "no-store";
                return Ok(ApiResponse<object>.Success(new
                {
                    transcript,
                    languageMode = normalizedLanguage
                }));
            }
            catch (InvalidOperationException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    ApiResponse<object>.Error("Dịch vụ nhận dạng giọng nói tạm thời chưa sẵn sàng. Hãy thử lại.", StatusCodes.Status503ServiceUnavailable));
            }
        }

        [HttpPost("analyze-file")]
        [EnableRateLimiting("FixedWindow")]
        [RequestSizeLimit(15 * 1024 * 1024)]
        public async Task<IActionResult> AnalyzeFile(
            [FromForm] IFormFile file,
            [FromForm] Guid? projectId = null,
            [FromForm] string? userPrompt = null,
            [FromForm] string? previousAnalysisJson = null)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<object>.Error("Vui lòng cung cấp file dữ liệu hợp lệ."));
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AiDocumentMimeTypes.TryGetValue(ext, out var allowedMimeTypes))
            {
                return BadRequest(ApiResponse<object>.Error("Chỉ hỗ trợ tài liệu .txt, .md, .docx hoặc .pdf. Vui lòng dùng chức năng Import Excel/CSV cho dữ liệu bảng."));
            }

            var mimeType = (file.ContentType ?? string.Empty).Split(';', 2)[0].Trim();
            if (!allowedMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse<object>.Error($"Loại nội dung '{mimeType}' không khớp với phần mở rộng {ext}."));
            }

            if (file.Length > AiDocumentMaxBytes)
            {
                return BadRequest(ApiResponse<object>.Error("Dung lượng file vượt quá giới hạn cho phép (10MB)."));
            }

            try
            {
                var userId = GetUserId();
                if (projectId.HasValue && projectId.Value != Guid.Empty)
                {
                    if (!await UserHasProjectAccessAsync(userId, projectId.Value))
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Error("Bạn không có quyền truy cập dự án này."));
                    }
                }

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var fileBytes = ms.ToArray();

                if (!HasValidDocumentSignature(ext, fileBytes))
                {
                    return BadRequest(ApiResponse<object>.Error("Nội dung file không hợp lệ hoặc không khớp với định dạng đã chọn."));
                }

                var result = await _aiService.AnalyzeFileAsync(
                    userId,
                    file.FileName,
                    mimeType,
                    fileBytes,
                    userPrompt,
                    projectId,
                    previousAnalysisJson);

                return Ok(ApiResponse<AiFileAnalysisResultDto>.Success(result));
            }
            catch (InvalidOperationException ex) when (IsGeminiQuotaFailure(ex))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse<object>.Error("AI tạm thời hết hạn mức, vui lòng thử lại sau"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpPost("chat")]
        [EnableRateLimiting("FixedWindow")]
        public async Task<IActionResult> Chat([FromBody] AiChatRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(ApiResponse<object>.Error("Message cannot be empty."));
            }

            try
            {
                var userId = GetUserId();
                var response = await _aiService.ChatAsync(userId, request);
                return Ok(ApiResponse<string>.Success(response));
            }
            catch (InvalidOperationException ex)
            {
                if (IsTransientAiFailure(ex))
                {
                    return Ok(ApiResponse<string>.Success(
                        BuildLocalChatFallback(request.Message),
                        "Gemini tam thoi chua san sang. He thong da dung local chat fallback."));
                }
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
            catch (HttpRequestException ex) when (IsTransientAiFailure(ex))
            {
                return Ok(ApiResponse<string>.Success(
                    BuildLocalChatFallback(request.Message),
                    "Gemini tam thoi chua san sang. He thong da dung local chat fallback."));
            }
            catch (TaskCanceledException)
            {
                return Ok(ApiResponse<string>.Success(
                    BuildLocalChatFallback(request.Message),
                    "Gemini tam thoi chua san sang. He thong da dung local chat fallback."));
            }
        }

        [HttpPost("project-assistant")]
        [EnableRateLimiting("FixedWindow")]
        public async Task<IActionResult> ProjectAssistant([FromBody] AiProjectAssistantRequestDto request)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Error("Request body khong hop le."));
            }

            if (request.ProjectId == Guid.Empty)
            {
                return BadRequest(ApiResponse<object>.Error("ID dự án không hợp lệ."));
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(ApiResponse<object>.Error("Nội dung tin nhắn không được để trống."));
            }

            try
            {
                var userId = GetUserId();

                if (!await UserHasProjectAccessAsync(userId, request.ProjectId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Error("Bạn không có quyền truy cập dự án này."));
                }

                var response = await _aiService.ProjectAssistantAsync(userId, request);
                return Ok(ApiResponse<AiProjectAssistantResponseDto>.Success(response));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        private static bool HasValidDocumentSignature(string extension, byte[] bytes)
        {
            if (bytes.Length == 0) return false;
            return extension switch
            {
                ".pdf" => bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
                ".docx" => bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B &&
                           bytes[2] is 0x03 or 0x05 or 0x07 && bytes[3] is 0x04 or 0x06 or 0x08,
                ".txt" or ".md" => Array.IndexOf(bytes, (byte)0) < 0,
                _ => false
            };
        }

        [HttpPost("context-chat")]
        [EnableRateLimiting("FixedWindow")]
        public async Task<IActionResult> ContextChat([FromBody] AiContextChatRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(ApiResponse<object>.Error("Message cannot be empty."));
            }

            var userId = GetUserId();
            if (request.ProjectId.HasValue && request.ProjectId.Value != Guid.Empty)
            {
                var project = await _dbContext.Projects
                    .AsNoTracking()
                    .Where(p => p.Id == request.ProjectId.Value && !p.IsDeleted)
                    .Select(p => new { p.Id, p.WorkspaceId })
                    .FirstOrDefaultAsync();

                if (project == null || !await UserHasProjectContextAccessAsync(userId, project.WorkspaceId, project.Id))
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        ApiResponse<object>.Error("Bạn không có quyền truy cập dữ liệu dự án này."));
                }

                if (request.WorkspaceId.HasValue && request.WorkspaceId.Value != project.WorkspaceId)
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        ApiResponse<object>.Error("Workspace không khớp với dự án."));
                }
            }
            else if (request.WorkspaceId.HasValue && request.WorkspaceId.Value != Guid.Empty &&
                     !await UserHasWorkspaceAccessAsync(userId, request.WorkspaceId.Value))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Error("Bạn không có quyền truy cập workspace này."));
            }

            try
            {
                var response = await _aiService.ContextChatAsync(userId, request);
                return Ok(ApiResponse<AiContextChatResponseDto>.Success(response));
            }
            catch (InvalidOperationException ex) when (IsGeminiQuotaFailure(ex))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    ApiResponse<object>.Error(BuildAiDiagnosticMessage(ex, "quota")));
            }
            catch (HttpRequestException ex) when (IsGeminiQuotaFailure(ex))
            {
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    ApiResponse<object>.Error(BuildAiDiagnosticMessage(ex, "quota")));
            }
            catch (InvalidOperationException ex) when (IsAiContextFailure(ex))
            {
                return BadRequest(ApiResponse<object>.Error(BuildAiDiagnosticMessage(ex, "context")));
            }
            catch (InvalidOperationException ex) when (IsTransientAiFailure(ex))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    ApiResponse<object>.Error(BuildAiDiagnosticMessage(ex, "transient")));
            }
            catch (HttpRequestException ex) when (IsTransientAiFailure(ex))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    ApiResponse<object>.Error(BuildAiDiagnosticMessage(ex, "transient")));
            }
        }

        [HttpPost("actions/execute")]
        [EnableRateLimiting("FixedWindow")]
        public Task<IActionResult> ExecuteAction([FromBody] AiExecuteActionRequestDto request)
        {
            return Task.FromResult<IActionResult>(BadRequest(new
            {
                success = false,
                message = "Create a server preview and confirm that action before execution.",
                data = new { code = "AI_ACTION_CONFIRMATION_REQUIRED" }
            }));
        }

        [HttpPost("actions/preview")]
        [EnableRateLimiting("FixedWindow")]
        public async Task<IActionResult> PreviewAction([FromBody] AiExecuteActionRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Type))
                return BadRequest(ApiResponse<object>.Error("Action type is required."));

            var actionType = NormalizeActionType(request.Type);
            if (!AiActionRegistry.ContainsKey(actionType))
                return BadRequest(ApiResponse<object>.Error("Action is not allowed."));

            var userId = GetUserId();
            if (!request.WorkspaceId.HasValue || request.WorkspaceId == Guid.Empty)
                return BadRequest(ApiResponse<object>.Error("WorkspaceId is required for an AI action preview."));

            try
            {
                await ValidateActionPermissionAsync(userId, actionType, request);
                var destinationProjectId = await ResolveActionDestinationProjectAsync(actionType, request);
                var idempotencyKey = BuildIdempotencyKey(userId, actionType, request.IdempotencyKey, request.Payload);
                var existing = await _dbContext.AiActionExecutions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.UserId == userId && item.IdempotencyKey == idempotencyKey);
                if (existing != null) return Ok(ApiResponse<AiActionStateDto>.Success(ToActionState(existing), "Existing action state."));

                var payloadJson = JsonSerializer.Serialize(request.Payload, AiActionJsonOptions);
                var now = DateTime.UtcNow;
                var action = new AiActionExecution
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    WorkspaceId = request.WorkspaceId.Value,
                    ProjectId = destinationProjectId,
                    ConversationId = GetPayloadGuid(request.Payload, "conversationId"),
                    ActionType = actionType,
                    IdempotencyKey = idempotencyKey,
                    PayloadJson = payloadJson,
                    PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson))),
                    PreviewJson = JsonSerializer.Serialize(new
                    {
                        type = actionType,
                        destination = new { workspaceId = request.WorkspaceId, projectId = destinationProjectId },
                        payload = request.Payload,
                        requiresConfirmation = AiActionRegistry[actionType].RequiresConfirmation
                    }, AiActionJsonOptions),
                    State = "AWAITING_CONFIRMATION",
                    CreatedAt = now,
                    UpdatedAt = now,
                    ExpiresAt = now.AddMinutes(15)
                };
                _dbContext.AiActionExecutions.Add(action);
                await _dbContext.SaveChangesAsync();
                return Ok(ApiResponse<AiActionStateDto>.Success(ToActionState(action), "Preview ready; no mutation has occurred."));
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "You do not have permission for this AI action.", data = new { code = "AI_ACTION_PERMISSION_DENIED" } });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpGet("actions/{actionId:guid}")]
        public async Task<IActionResult> GetAction(Guid actionId)
        {
            var action = await _dbContext.AiActionExecutions.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == actionId && item.UserId == GetUserId());
            return action == null ? NotFound() : Ok(ApiResponse<AiActionStateDto>.Success(ToActionState(action)));
        }

        [HttpPost("actions/{actionId:guid}/cancel")]
        public async Task<IActionResult> CancelAction(Guid actionId)
        {
            var action = await _dbContext.AiActionExecutions
                .FirstOrDefaultAsync(item => item.Id == actionId && item.UserId == GetUserId());
            if (action == null) return NotFound();
            if (action.State is "EXECUTING" or "SUCCEEDED" or "PARTIAL_FAILURE")
                return Conflict(new { success = false, message = "An executing or completed action cannot be cancelled.", data = new { code = "AI_ACTION_NOT_CANCELLABLE" } });
            action.State = "CANCELLED";
            action.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return Ok(ApiResponse<AiActionStateDto>.Success(ToActionState(action), "Action cancelled; no mutation occurred."));
        }

        [HttpPost("actions/{actionId:guid}/confirm")]
        [EnableRateLimiting("FixedWindow")]
        public async Task<IActionResult> ConfirmAction(Guid actionId)
        {
            var userId = GetUserId();
            using var executionLock = await AcquireAiActionLockAsync($"action:{actionId:N}");
            var action = await _dbContext.AiActionExecutions.FirstOrDefaultAsync(item => item.Id == actionId && item.UserId == userId);
            if (action == null) return NotFound();
            if (action.State == "SUCCEEDED") return Ok(ApiResponse<AiActionStateDto>.Success(ToActionState(action), "Idempotent replay."));
            if (action.State == "CANCELLED") return Conflict(new { success = false, message = "Cancelled action cannot be confirmed.", data = new { code = "AI_ACTION_CANCELLED" } });
            if (action.ExpiresAt <= DateTime.UtcNow)
            {
                action.State = "EXPIRED";
                action.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                return Conflict(new { success = false, message = "Action preview expired.", data = new { code = "AI_ACTION_EXPIRED" } });
            }

            var payload = JsonSerializer.Deserialize<Dictionary<string, object?>>(action.PayloadJson, AiActionJsonOptions) ?? new();
            var request = new AiExecuteActionRequestDto { Type = action.ActionType, IdempotencyKey = action.IdempotencyKey, WorkspaceId = action.WorkspaceId, ProjectId = action.ProjectId, Payload = payload };
            try
            {
                await ValidateActionPermissionAsync(userId, action.ActionType, request);
            }
            catch (UnauthorizedAccessException)
            {
                action.State = "PERMISSION_DENIED";
                action.ErrorCode = "AI_ACTION_PERMISSION_DENIED";
                action.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "You do not have permission for this AI action.", data = new { code = action.ErrorCode } });
            }
            catch (ArgumentException ex)
            {
                action.State = "VALIDATION_FAILED";
                action.ErrorCode = "AI_ACTION_VALIDATION_FAILED";
                action.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                return BadRequest(new { success = false, message = ex.Message, data = new { code = action.ErrorCode } });
            }
            action.State = "EXECUTING";
            action.ConfirmedAt = DateTime.UtcNow;
            action.UpdatedAt = DateTime.UtcNow;
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict(new { success = false, message = "This action is already being confirmed.", data = new { code = "AI_ACTION_CONFIRM_CONFLICT" } });
            }

            var result = await ExecuteConfirmedActionCoreAsync(request);
            if (result is OkObjectResult okResult)
            {
                action.State = "SUCCEEDED";
                action.ResultJson = JsonSerializer.Serialize(okResult.Value, AiActionJsonOptions);
                action.ExecutedAt = DateTime.UtcNow;
                action.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                return Ok(ApiResponse<AiActionStateDto>.Success(ToActionState(action)));
            }

            action.State = "EXECUTION_FAILED";
            action.ErrorCode = result is ConflictObjectResult ? "AI_ACTION_CONFLICT" : "AI_ACTION_EXECUTION_FAILED";
            action.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return result;
        }

        private async Task<IActionResult> ExecuteConfirmedActionCoreAsync(AiExecuteActionRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Type))
            {
                return BadRequest(ApiResponse<object>.Error("Action type is required."));
            }

            var actionType = NormalizeActionType(request.Type);
            if (!AiActionRegistry.ContainsKey(actionType))
            {
                return BadRequest(ApiResponse<object>.Error("Action is not allowed."));
            }

            var userId = GetUserId();
            var idempotencyKey = BuildIdempotencyKey(userId, actionType, request.IdempotencyKey, request.Payload);
            using var executionLock = await AcquireAiActionLockAsync(idempotencyKey);
            var existingLog = await _dbContext.SystemAuditLogs
                .AsNoTracking()
                .Where(log => log.Action == "AI_ACTION_EXECUTE" &&
                              log.Resource == idempotencyKey &&
                              log.Status == "Success")
                .OrderByDescending(log => log.CreatedAt)
                .FirstOrDefaultAsync();

            if (existingLog != null && TryReadExecutedAction(existingLog.Details, out var replay))
            {
                replay.IdempotentReplay = true;
                return Ok(ApiResponse<AiExecuteActionResponseDto>.Success(replay, "Idempotent replay."));
            }

            try
            {
                var result = actionType switch
                {
                    "create_project" => await ExecuteCreateProjectAsync(userId, request),
                    "create_task" => await ExecuteCreateTaskAsync(userId, request),
                    "create_cycle" => await ExecuteCreateCycleAsync(userId, request),
                    "create_module" => await ExecuteCreateModuleAsync(userId, request),
                    "create_page" => await ExecuteCreatePageAsync(userId, request),
                    "create_view" => await ExecuteCreateViewAsync(userId, request),
                    "create_intake_request" => await ExecuteCreateIntakeRequestAsync(userId, request),
                    "update_task_status" => await ExecuteUpdateTaskStatusAsync(userId, request),
                    "update_task_priority" => await ExecuteUpdateTaskPriorityAsync(userId, request),
                    "update_task_due_date" => await ExecuteUpdateTaskDueDateAsync(userId, request),
                    "assign_task" => await ExecuteAssignTaskAsync(userId, request),
                    "add_comment" => await ExecuteAddCommentAsync(userId, request),
                    "create_goal" => await ExecuteCreateGoalAsync(userId, request),
                    "summarize_dashboard" => await ExecuteSummarizeDashboardAsync(userId, request),
                    "summarize_project" => await ExecuteSummarizeProjectAsync(userId, request),
                    "list_work_items" => await ExecuteListWorkItemsAsync(userId, request),
                    "list_cycles" => await ExecuteListCyclesAsync(userId, request),
                    "list_modules" => await ExecuteListModulesAsync(userId, request),
                    "list_pages" => await ExecuteListPagesAsync(userId, request),
                    "list_views" => await ExecuteListViewsAsync(userId, request),
                    "list_intakes" => await ExecuteListIntakesAsync(userId, request),
                    "list_overdue_tasks" => await ExecuteListOverdueTasksAsync(userId, request),
                    "get_workload" => await ExecuteGetWorkloadAsync(userId, request),
                    "explain_report" => await ExecuteExplainReportAsync(userId, request),
                    "summarize_page" => await ExecuteSummarizePageAsync(userId, request),
                    "summarize_intakes" => await ExecuteSummarizeIntakesAsync(userId, request),
                    "suggest_view_filter" => await ExecuteSuggestViewFilterAsync(userId, request),
                    _ => throw new InvalidOperationException("Unsupported action.")
                };

                await WriteAiActionAuditAsync(userId, idempotencyKey, actionType, "Success", result);
                return Ok(ApiResponse<AiExecuteActionResponseDto>.Success(result));
            }
            catch (UnauthorizedAccessException ex)
            {
                await WriteAiActionAuditAsync(userId, idempotencyKey, actionType, "Denied", null, ex.Message);
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Error(ex.Message));
            }
            catch (DuplicateTaskException ex)
            {
                await WriteAiActionAuditAsync(userId, idempotencyKey, actionType, "DuplicateDetected", null, ex.Message);
                return Conflict(new
                {
                    statusCode = StatusCodes.Status409Conflict,
                    success = false,
                    message = ex.Message,
                    data = new
                    {
                        code = "AI_TASK_DUPLICATE",
                        existingTask = ex.Candidate
                    }
                });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await WriteAiActionAuditAsync(userId, idempotencyKey, actionType, "Conflict", null, ex.Message);
                var taskId = GetPayloadGuid(request.Payload, "taskId", "workTaskId");
                var current = taskId.HasValue
                    ? await _dbContext.WorkTasks
                        .AsNoTracking()
                        .Where(task => task.Id == taskId.Value && !task.IsDeleted)
                        .Select(task => new
                        {
                            task.Id,
                            task.ProjectId,
                            task.Title,
                            task.Priority,
                            task.DueDate,
                            task.TaskStatusId,
                            task.AssignedUserId,
                            task.RowVersion
                        })
                        .FirstOrDefaultAsync()
                    : null;
                return Conflict(new
                {
                    statusCode = StatusCodes.Status409Conflict,
                    success = false,
                    message = "Task was changed by another request. Reload before applying the AI action.",
                    data = new
                    {
                        code = "TASK_VERSION_CONFLICT",
                        currentVersion = current?.RowVersion,
                        currentData = current
                    }
                });
            }
            catch (ArgumentException ex)
            {
                await WriteAiActionAuditAsync(userId, idempotencyKey, actionType, "ValidationFailed", null, ex.Message);
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                await WriteAiActionAuditAsync(userId, idempotencyKey, actionType, "ValidationFailed", null, ex.Message);
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpPost("command")]
        [EnableRateLimiting("FixedWindow")]
        public async Task<IActionResult> Command([FromBody] AiCommandRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return BadRequest(ApiResponse<object>.Error("Prompt khong duoc de trong."));
            }

            var userId = GetUserId();
            var prompt = request.Prompt.Trim();
            var locale = string.Equals(request.Locale, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "vi";
            var lowered = prompt.ToLowerInvariant();

            if (request.ProjectId.HasValue && request.ProjectId.Value != Guid.Empty)
            {
                if (!await UserHasProjectAccessAsync(userId, request.ProjectId.Value))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Error(locale == "en"
                        ? "You do not have permission to access this project."
                        : "Bạn không có quyền truy cập dự án này."));
                }
            }

            if (ContainsAny(lowered, "tao task", "t\u1ea1o task", "tao cong viec", "t\u1ea1o c\u00f4ng vi\u1ec7c", "create task", "add task"))
            {
                if (request.ProjectId == null || request.ProjectId == Guid.Empty)
                {
                    return BadRequest(ApiResponse<object>.Error(locale == "en"
                        ? "Please choose a project before asking AI to create a task."
                        : "B\u1ea1n c\u1ea7n ch\u1ecdn project tr\u01b0\u1edbc khi y\u00eau c\u1ea7u AI t\u1ea1o task."));
                }

                var title = ExtractTaskTitle(prompt);
                var created = await _workTaskService.CreateAsync(userId, new CreateWorkTaskDto
                {
                    ProjectId = request.ProjectId.Value,
                    Title = title,
                    Description = locale == "en"
                        ? $"Created by SprintA AI from prompt:\n{prompt}"
                        : $"\u0110\u01b0\u1ee3c t\u1ea1o b\u1edfi SprintA AI t\u1eeb prompt:\n{prompt}",
                    TypeName = "Task",
                    Priority = InferPromptPriority(lowered),
                    StoryPoints = 0
                });

                return Ok(ApiResponse<object>.Success(new
                {
                    action = "create-task",
                    createdTask = created,
                    message = locale == "en"
                        ? $"Created task \"{created.Title}\" in the selected project."
                        : $"\u0110\u00e3 t\u1ea1o task \"{created.Title}\" trong project \u0111ang ch\u1ecdn."
                }));
            }

            if (request.ProjectId.HasValue && ContainsAny(lowered, "thong ke", "th\u1ed1ng k\u00ea", "tong ket", "t\u1ed5ng k\u1ebft", "summary", "report", "bao cao", "b\u00e1o c\u00e1o"))
            {
                var stats = await BuildProjectStatsAsync(request.ProjectId.Value);
                var message = locale == "en"
                    ? $"Project summary: {stats.Total} tasks, {stats.Done} done, {stats.InProgress} in progress, {stats.Todo} to do, {stats.Overdue} overdue."
                    : $"Th\u1ed1ng k\u00ea d\u1ef1 \u00e1n: {stats.Total} task, {stats.Done} \u0111\u00e3 xong, {stats.InProgress} \u0111ang l\u00e0m, {stats.Todo} c\u1ea7n l\u00e0m, {stats.Overdue} qu\u00e1 h\u1ea1n.";

                return Ok(ApiResponse<object>.Success(new
                {
                    action = "project-summary",
                    stats,
                    message
                }));
            }

            try
            {
                var languageInstruction = locale == "en"
                    ? "Answer in English. If you suggest tasks, use clear titles and explain that the user can ask 'create task ...' to create a real task."
                    : "Tr\u1ea3 l\u1eddi b\u1eb1ng ti\u1ebfng Vi\u1ec7t c\u00f3 d\u1ea5u. N\u1ebfu g\u1ee3i \u00fd task, h\u00e3y vi\u1ebft ti\u00eau \u0111\u1ec1 r\u00f5 r\u00e0ng v\u00e0 nh\u1eafc r\u1eb1ng ng\u01b0\u1eddi d\u00f9ng c\u00f3 th\u1ec3 nh\u1eadp 't\u1ea1o task ...' \u0111\u1ec3 t\u1ea1o task th\u1eadt.";
                var response = await _aiService.ChatAsync(userId, new AiChatRequestDto
                {
                    Message = $"{languageInstruction}\n\nProjectId: {request.ProjectId?.ToString() ?? "none"}\nPrompt: {prompt}",
                    History = request.History?.Select(h => new TaskManagement.Application.DTOs.AI.AiChatMessageDto
                    {
                        Role = h.Role,
                        Content = h.Content
                    }).ToList()
                });

                return Ok(ApiResponse<object>.Success(new
                {
                    action = "chat",
                    message = response
                }));
            }
            catch (Exception ex) when (IsTransientAiFailure(ex) || ex is InvalidOperationException)
            {
                return Ok(ApiResponse<object>.Success(new
                {
                    action = "local-fallback",
                    message = BuildLocalizedLocalChatFallback(prompt, locale)
                }));
            }
        }

        [HttpPost("generate-description")]
        [EnableRateLimiting("FixedWindow")]
        public async Task<IActionResult> GenerateDescription([FromBody] AiGenerateDescriptionRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return BadRequest(ApiResponse<object>.Error("Prompt khong duoc de trong."));
            }

            try
            {
                var userId = GetUserId();
                var description = await _aiService.GenerateDescriptionAsync(userId, request);
                return Ok(ApiResponse<string>.Success(description, "Generated"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpPost("breakdown-task")]
        [EnableRateLimiting("FixedWindow")]
        public async Task<IActionResult> BreakdownTask([FromBody] AiBreakdownRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(ApiResponse<object>.Error("Tieu de cong viec khong duoc de trong."));
            }

            try
            {
                var userId = GetUserId();

                if (request.CreateSubtasks)
                {
                    var created = await ExecuteWithGeminiRetryAsync(
                        () => _aiService.BreakdownAndCreateSubtasksAsync(userId, request),
                        request.Title);
                    return Ok(ApiResponse<List<WorkTaskResponseDto>>.Success(created, "AI da phan ra va tao sub-work items."));
                }

                var subtasks = await ExecuteWithGeminiRetryAsync(
                    () => _aiService.BreakdownTaskAsync(userId, request),
                    request.Title);
                return Ok(ApiResponse<List<AiSubTaskDto>>.Success(subtasks, "AI da phan tach cong viec thanh cong."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                if (IsTransientAiFailure(ex))
                {
                    return Ok(ApiResponse<List<AiSubTaskDto>>.Success(
                        BuildLocalBreakdownFallback(request),
                        "Gemini tam thoi chua san sang. He thong da dung local breakdown preview."));
                }
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
            catch (HttpRequestException ex) when (IsTransientAiFailure(ex))
            {
                return Ok(ApiResponse<List<AiSubTaskDto>>.Success(
                    BuildLocalBreakdownFallback(request),
                    "Gemini tam thoi chua san sang. He thong da dung local breakdown preview."));
            }
            catch (TaskCanceledException)
            {
                return Ok(ApiResponse<List<AiSubTaskDto>>.Success(
                    BuildLocalBreakdownFallback(request),
                    "Gemini tam thoi chua san sang. He thong da dung local breakdown preview."));
            }
        }

        [HttpPost("create-subtasks-from-preview")]
        [EnableRateLimiting("FixedWindow")]
        public async Task<IActionResult> CreateSubtasksFromPreview([FromBody] AiCreateSubtasksFromPreviewRequestDto request)
        {
            if (request.ProjectId == Guid.Empty || request.ParentTaskId == Guid.Empty)
            {
                return BadRequest(ApiResponse<object>.Error("ProjectId va ParentTaskId khong duoc de trong."));
            }

            try
            {
                var userId = GetUserId();
                var created = await _aiService.CreateSubtasksFromPreviewAsync(userId, request);
                return Ok(ApiResponse<List<WorkTaskResponseDto>>.Success(created, "AI preview da duoc tao thanh sub-work items."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpPost("suggest-estimate")]
        [EnableRateLimiting("FixedWindow")]
        public async Task<IActionResult> SuggestEstimate([FromBody] AiEstimateSuggestionRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(ApiResponse<object>.Error("Tieu de cong viec khong duoc de trong."));
            }

            try
            {
                var userId = GetUserId();
                var suggestion = await ExecuteWithGeminiRetryAsync(
                    () => _aiService.SuggestEstimateAsync(userId, request),
                    request.Title);
                return Ok(ApiResponse<AiEstimateSuggestionDto>.Success(suggestion, "AI da goi y estimate thanh cong."));
            }
            catch (InvalidOperationException ex)
            {
                if (IsTransientAiFailure(ex))
                {
                    return Ok(ApiResponse<AiEstimateSuggestionDto>.Success(
                        BuildLocalEstimateFallback(request),
                        "Gemini tam thoi chua san sang. He thong da dung local suggestion."));
                }

                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
            catch (HttpRequestException ex) when (IsTransientAiFailure(ex))
            {
                return Ok(ApiResponse<AiEstimateSuggestionDto>.Success(
                    BuildLocalEstimateFallback(request),
                    "Gemini tam thoi chua san sang. He thong da dung local suggestion."));
            }
            catch (TaskCanceledException)
            {
                return Ok(ApiResponse<AiEstimateSuggestionDto>.Success(
                    BuildLocalEstimateFallback(request),
                    "Gemini tam thoi chua san sang. He thong da dung local suggestion."));
            }
        }

        [HttpPost("suggest-assignees")]
        [EnableRateLimiting("FixedWindow")]
        public async Task<IActionResult> SuggestAssignees([FromBody] AiAssigneeSuggestionRequestDto request)
        {
            if (request.ProjectId == Guid.Empty)
            {
                return BadRequest(ApiResponse<object>.Error("ProjectId khong duoc de trong."));
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(ApiResponse<object>.Error("Tieu de cong viec khong duoc de trong."));
            }

            try
            {
                var userId = GetUserId();
                await EnsureAiAssigneeAccessAsync(userId, request.ProjectId);
                var suggestion = await _aiService.SuggestAssigneesAsync(userId, request);
                return Ok(ApiResponse<AiAssigneeSuggestionDto>.Success(suggestion, "AI da goi y assignee thanh cong."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Error(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpPost("repo-analysis")]
        [EnableRateLimiting("FixedWindow")]
        public async Task<IActionResult> AnalyzeRepository([FromBody] AiRepositoryAnalysisRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.RepoUrl))
            {
                return BadRequest(ApiResponse<object>.Error("Repo URL khong duoc de trong."));
            }

            try
            {
                var userId = GetUserId();
                var analysis = await ExecuteWithGeminiRetryAsync(
                    () => _aiService.AnalyzeRepositoryAsync(userId, request),
                    request.RepoUrl);
                return Ok(ApiResponse<AiRepositoryAnalysisDto>.Success(analysis, "AI da phan tich repo thanh cong."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                if (IsTransientAiFailure(ex))
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable,
                        ApiResponse<object>.Error(BuildGeminiFallbackMessage(request.RepoUrl)));
                }

                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
            catch (HttpRequestException ex) when (IsTransientAiFailure(ex))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    ApiResponse<object>.Error(BuildGeminiFallbackMessage(request.RepoUrl)));
            }
            catch (TaskCanceledException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    ApiResponse<object>.Error(BuildGeminiFallbackMessage(request.RepoUrl)));
            }
        }

        [HttpPost("repo-analysis/create-work-items")]
        [EnableRateLimiting("FixedWindow")]
        public async Task<IActionResult> CreateBacklogItemsFromAnalysis([FromBody] AiCreateBacklogFromAnalysisRequestDto request)
        {
            if (request.ProjectId == Guid.Empty)
            {
                return BadRequest(ApiResponse<object>.Error("ProjectId khong duoc de trong."));
            }

            try
            {
                var userId = GetUserId();
                var created = await _aiService.CreateBacklogItemsFromAnalysisAsync(userId, request);
                return Ok(ApiResponse<List<WorkTaskResponseDto>>.Success(created, $"AI da tao {created.Count} work items vao project."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Error(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        private async Task<T> ExecuteWithGeminiRetryAsync<T>(Func<Task<T>> action, string? title)
        {
            Exception? lastException = null;

            for (var attempt = 1; attempt <= GeminiRetryAttempts; attempt++)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex) when (IsTransientAiFailure(ex))
                {
                    lastException = ex;

                    if (attempt == GeminiRetryAttempts)
                    {
                        break;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt));
                }
            }

            throw new InvalidOperationException(BuildGeminiFallbackMessage(title), lastException);
        }

        private static bool IsTransientAiFailure(Exception ex)
        {
            var message = ex.Message?.ToLowerInvariant() ?? string.Empty;

            return ex is HttpRequestException
                || ex is TaskCanceledException
                || message.Contains("503")
                || message.Contains("gemini unavailable")
                || message.Contains("service unavailable")
                || message.Contains("temporarily unavailable")
                || message.Contains("rate limit")
                || message.Contains("quota")
                || message.Contains("timeout")
                || message.Contains("timed out");
        }

        private static bool IsGeminiQuotaFailure(Exception ex)
        {
            var message = ex.Message ?? string.Empty;

            return message.Contains("Gemini API loi 429", StringComparison.OrdinalIgnoreCase)
                || message.Contains("429", StringComparison.OrdinalIgnoreCase)
                || message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Gemini Multimodal API loi 429", StringComparison.OrdinalIgnoreCase)
                || message.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
                || message.Contains("GenerateRequestsPerDayPerProjectPerModel-FreeTier", StringComparison.OrdinalIgnoreCase)
                || message.Contains("rate-limits", StringComparison.OrdinalIgnoreCase)
                || message.Contains("quota exceeded", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAiContextFailure(Exception ex)
        {
            var message = ex.ToString();
            return message.Contains("context", StringComparison.OrdinalIgnoreCase)
                || message.Contains("input too long", StringComparison.OrdinalIgnoreCase)
                || message.Contains("token", StringComparison.OrdinalIgnoreCase)
                || message.Contains("payload too large", StringComparison.OrdinalIgnoreCase)
                || message.Contains("permission", StringComparison.OrdinalIgnoreCase)
                || message.Contains("forbidden", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildAiDiagnosticMessage(Exception ex, string category)
        {
            var raw = ex.Message ?? string.Empty;
            var detail = raw.Length > 320 ? raw[..320] + "..." : raw;
            return category switch
            {
                "quota" => $"Gemini đang bị giới hạn quota/rate limit (429). Không thực thi action tự động. Chi tiết: {detail}",
                "context" => $"Không thể gửi đủ ngữ cảnh cho Gemini hoặc quyền/ngữ cảnh không hợp lệ. Không thực thi action tự động. Chi tiết: {detail}",
                _ => $"Gemini tạm thời không sẵn sàng (503). Không thực thi action tự động. Chi tiết: {detail}"
            };
        }

        private static string BuildGeminiFallbackMessage(string? title)
        {
            if (!string.IsNullOrWhiteSpace(title) &&
                (title.Contains("github.com", StringComparison.OrdinalIgnoreCase) || title.StartsWith("http", StringComparison.OrdinalIgnoreCase)))
            {
                return "Gemini tam thoi khong san sang sau 3 lan thu lai. Ban co the thu lai sau it phut hoac tu phan tich repo thanh 3-5 nhom viec: quick wins, medium tasks, risky tasks, test plan.";
            }

            var safeTitle = string.IsNullOrWhiteSpace(title) ? "cong viec nay" : $"\"{title}\"";
            return $"Gemini tam thoi khong san sang sau 3 lan thu lai. Ban co the thu lai sau it phut hoac tu tach {safeTitle} thanh 3-5 buoc nho: muc tieu, dau viec, kiem thu, ban giao.";
        }

        private static string BuildLocalChatFallback(string? message)
        {
            var clean = (message ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clean))
            {
                return "Gemini dang tam nghi vi vuot quota, nhung SprintA van san sang ho tro. Hay nhap ro ten task, du an, hoac cau hoi ban muon xu ly.";
            }

            return
                $"Gemini dang tam nghi vi vuot quota nen SprintA dang tra loi bang local fallback.\n\n" +
                $"Noi dung ban vua gui: \"{clean}\"\n\n" +
                $"De tiep tuc khong bi dung mach, ban co the lam theo 4 buoc:\n" +
                $"- Lam ro muc tieu chinh cua yeu cau nay.\n" +
                $"- Tach no thanh 3-5 dau viec nho de giao ngay.\n" +
                $"- Neu la viec ky thuat, them tieu chi kiem thu va rui ro.\n" +
                $"- Neu can, hoi tiep theo mau: tom tat, breakdown, test case, hoac handoff.";
        }

        private static string BuildLocalizedLocalChatFallback(string message, string locale)
        {
            if (locale == "en")
            {
                return
                    "Gemini is not available right now, so SprintA is using a local fallback.\n\n" +
                    $"Your request: \"{message}\"\n\n" +
                    "You can ask me to create a real task with: create task <title>. " +
                    "You can also ask for a project summary, report, priorities, or a task breakdown.";
            }

            return
                "Gemini hiá»‡n chÆ°a pháº£n há»“i á»•n Ä‘á»‹nh, nÃªn SprintA Ä‘ang dÃ¹ng xá»­ lÃ½ cá»¥c bá»™.\n\n" +
                $"YÃªu cáº§u cá»§a báº¡n: \"{message}\"\n\n" +
                "Báº¡n cÃ³ thá»ƒ nháº­p: táº¡o task <tiÃªu Ä‘á»> Ä‘á»ƒ AI táº¡o task tháº­t. " +
                "Báº¡n cÅ©ng cÃ³ thá»ƒ yÃªu cáº§u: thá»‘ng kÃª dá»± Ã¡n, tÃ³m táº¯t cÃ´ng viá»‡c, Ä‘á» xuáº¥t Æ°u tiÃªn, hoáº·c breakdown task.";
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static string ExtractTaskTitle(string prompt)
        {
            var patterns = new[]
            {
                @"(?:táº¡o|tao|create|add)\s+(?:task|cÃ´ng viá»‡c|cong viec)\s*[:\-]?\s*(?<title>.+)",
                @"(?:task|cÃ´ng viá»‡c|cong viec)\s*[:\-]\s*(?<title>.+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(prompt, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (match.Success)
                {
                    var title = match.Groups["title"].Value.Trim().Trim('"', '\'', '.', ';');
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        return title.Length > 180 ? title[..180] : title;
                    }
                }
            }

            var fallback = prompt.Trim();
            return fallback.Length > 180 ? fallback[..180] : fallback;
        }

        private static int InferPromptPriority(string loweredPrompt)
        {
            if (ContainsAny(loweredPrompt, "kháº©n", "khan", "urgent", "gáº¥p", "gap", "p1")) return 1;
            if (ContainsAny(loweredPrompt, "cao", "high", "p2")) return 2;
            if (ContainsAny(loweredPrompt, "tháº¥p", "thap", "low", "p4")) return 4;
            return 3;
        }

        private async Task<ProjectAiStats> BuildProjectStatsAsync(Guid projectId)
        {
            var now = DateTime.UtcNow;
            var tasks = await _dbContext.WorkTasks
                .AsNoTracking()
                .Where(task => task.ProjectId == projectId && !task.IsDeleted)
                .Select(task => new
                {
                    task.Id,
                    task.DueDate,
                    StatusName = task.TaskStatus != null ? task.TaskStatus.Name : null
                })
                .ToListAsync();

            static bool IsDone(string? status) =>
                !string.IsNullOrWhiteSpace(status) &&
                (status.Contains("done", StringComparison.OrdinalIgnoreCase) ||
                 status.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
                 status.Contains("xong", StringComparison.OrdinalIgnoreCase));

            static bool IsInProgress(string? status) =>
                !string.IsNullOrWhiteSpace(status) &&
                (status.Contains("progress", StringComparison.OrdinalIgnoreCase) ||
                 status.Contains("doing", StringComparison.OrdinalIgnoreCase) ||
                 status.Contains("Ä‘ang", StringComparison.OrdinalIgnoreCase) ||
                 status.Contains("dang", StringComparison.OrdinalIgnoreCase));

            var done = tasks.Count(task => IsDone(task.StatusName));
            var inProgress = tasks.Count(task => IsInProgress(task.StatusName));
            var overdue = tasks.Count(task => task.DueDate.HasValue && task.DueDate.Value < now && !IsDone(task.StatusName));

            return new ProjectAiStats
            {
                Total = tasks.Count,
                Done = done,
                InProgress = inProgress,
                Todo = Math.Max(0, tasks.Count - done - inProgress),
                Overdue = overdue
            };
        }

        private static List<AiSubTaskDto> BuildLocalBreakdownFallback(AiBreakdownRequestDto request)
        {
            var normalizedTitle = string.IsNullOrWhiteSpace(request.Title) ? "Work item" : request.Title.Trim();
            var description = (request.Description ?? string.Empty).Trim();
            var context = string.IsNullOrWhiteSpace(description)
                ? $"Implement the scope of {normalizedTitle} with a safe default delivery flow."
                : description;

            return new List<AiSubTaskDto>
            {
                new()
                {
                    Title = $"Clarify scope for {normalizedTitle}",
                    Description = $"Review requirements, acceptance criteria, dependencies, and edge cases. Context: {context}",
                    EstHours = 1.5,
                    Priority = 3
                },
                new()
                {
                    Title = $"Implement core flow for {normalizedTitle}",
                    Description = "Build the main behavior for this work item and align the code with the current module structure.",
                    EstHours = 4,
                    Priority = 2
                },
                new()
                {
                    Title = $"Handle validation and failure cases for {normalizedTitle}",
                    Description = "Add validation, guards, and error handling so the feature is stable in invalid and edge scenarios.",
                    EstHours = 2.5,
                    Priority = 2
                },
                new()
                {
                    Title = $"Test and hand off {normalizedTitle}",
                    Description = "Verify the completed flow, prepare test steps, and write a short handoff note for reviewer or PM.",
                    EstHours = 2,
                    Priority = 3
                }
            };
        }

        private static AiEstimateSuggestionDto BuildLocalEstimateFallback(AiEstimateSuggestionRequestDto request)
        {
            var normalizedStoryPoints = NormalizeStoryPoints(request.StoryPoints);
            var hours = InferLocalHours(request.Title, request.Priority, normalizedStoryPoints, request.AssigneeCount, request.SubtaskCount);

            return new AiEstimateSuggestionDto
            {
                SuggestedHours = hours,
                SuggestedDays = Math.Clamp((int)Math.Ceiling(hours / 6d), 1, 10),
                SuggestedStoryPoints = normalizedStoryPoints,
                Complexity = InferComplexity(hours),
                Reasoning = "Suggested from priority, story points, assignee count, and task title keywords."
            };
        }

        private static double InferLocalHours(string? title, int priority, double storyPoints, int assigneeCount, int subtaskCount)
        {
            var hours = storyPoints switch
            {
                <= 0 => 4d,
                <= 1 => 3d,
                <= 2 => 5d,
                <= 3 => 8d,
                <= 5 => 14d,
                <= 8 => 24d,
                _ => 40d
            };

            hours += priority switch
            {
                1 => 2,
                2 => 1,
                4 => -1,
                _ => 0
            };

            var lowered = (title ?? string.Empty).ToLowerInvariant();
            if (lowered.Contains("api") || lowered.Contains("integration") || lowered.Contains("payment") || lowered.Contains("deploy") || lowered.Contains("security"))
            {
                hours += 3;
            }

            if (lowered.Contains("refactor") || lowered.Contains("migration"))
            {
                hours += 4;
            }

            if (lowered.Contains("bug") || lowered.Contains("fix") || lowered.Contains("hotfix") || lowered.Contains("patch"))
            {
                hours += 1.5;
            }

            if (lowered.Contains("ui") || lowered.Contains("ux") || lowered.Contains("copy") || lowered.Contains("docs") || lowered.Contains("content"))
            {
                hours -= 1;
            }

            if (assigneeCount > 1)
            {
                hours += Math.Min(4, assigneeCount - 1);
            }

            if (subtaskCount > 0)
            {
                hours += Math.Min(6, subtaskCount);
            }

            return Math.Round(Math.Clamp(hours, 1, 80), 1);
        }

        private static double NormalizeStoryPoints(double storyPoints)
        {
            var allowed = new[] { 1d, 2d, 3d, 5d, 8d, 13d };
            return allowed
                .OrderBy(value => Math.Abs(value - Math.Max(1, storyPoints)))
                .First();
        }

        private static string InferComplexity(double hours)
        {
            if (hours <= 6) return "Low";
            if (hours <= 18) return "Medium";
            if (hours <= 32) return "High";
            return "Critical";
        }

        private Guid GetUserId()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var userId))
            {
                throw new UnauthorizedAccessException("Token khong hop le.");
            }

            return userId;
        }

        private static int EstimateCredits(long tokensUsed)
        {
            if (tokensUsed <= 0) return 0;
            return (int)Math.Ceiling(tokensUsed / 1000.0);
        }

        private async Task<bool> UserHasProjectAccessAsync(Guid userId, Guid projectId)
        {
            var authorization = await _authorizationService.AuthorizeProjectAsync(
                userId,
                projectId,
                ResourcePermissionCodes.ProjectRead);
            return authorization.Succeeded;
        }

        private async Task<bool> UserHasProjectContextAccessAsync(Guid userId, Guid workspaceId, Guid projectId)
        {
            var projectWorkspaceId = await _dbContext.Projects
                .AsNoTracking()
                .Where(project => project.Id == projectId && !project.IsDeleted)
                .Select(project => (Guid?)project.WorkspaceId)
                .FirstOrDefaultAsync();
            if (projectWorkspaceId != workspaceId)
            {
                return false;
            }

            return await UserHasProjectAccessAsync(userId, projectId);
        }

        private async Task<bool> UserHasWorkspaceAccessAsync(Guid userId, Guid workspaceId)
        {
            var authorization = await _authorizationService.AuthorizeWorkspaceAsync(
                userId,
                workspaceId,
                ResourcePermissionCodes.WorkspaceRead);
            return authorization.Succeeded;
        }

        private async Task ValidateActionPermissionAsync(Guid userId, string actionType, AiExecuteActionRequestDto request)
        {
            if (!request.WorkspaceId.HasValue || request.WorkspaceId == Guid.Empty)
                throw new ArgumentException("WorkspaceId is required.");
            if (!await UserHasWorkspaceAccessAsync(userId, request.WorkspaceId.Value))
                throw new UnauthorizedAccessException();

            if (actionType is "create_project" or "create_goal")
            {
                await EnsureWorkspaceWriteAccessAsync(userId, request.WorkspaceId.Value);
                return;
            }
            if (actionType == "summarize_dashboard") return;

            Guid projectId;
            if (actionType is "update_task_status" or "update_task_priority" or "update_task_due_date" or "assign_task")
            {
                projectId = await ResolveTaskProjectIdAsync(RequirePayloadGuid(request.Payload, "taskId", "workTaskId"));
            }
            else if (actionType == "add_comment")
            {
                var entityId = RequirePayloadGuid(request.Payload, "entityId", "taskId", "workTaskId");
                var entityType = NormalizeCommentEntityType(GetPayloadString(request.Payload, "entityType") ?? "WorkTask");
                await EnsureCommentEntityAccessAsync(userId, entityType, entityId);
                projectId = entityType == "WorkTask"
                    ? await ResolveTaskProjectIdAsync(entityId)
                    : ResolveProjectId(request);
            }
            else
            {
                projectId = ResolveProjectId(request);
            }

            var workspaceId = await _dbContext.Projects.AsNoTracking()
                .Where(project => project.Id == projectId && !project.IsDeleted)
                .Select(project => (Guid?)project.WorkspaceId)
                .FirstOrDefaultAsync() ?? throw new ArgumentException("Project does not exist.");
            if (workspaceId != request.WorkspaceId.Value)
                throw new ArgumentException("AI action destination does not belong to the selected workspace.");

            if (AiActionRegistry[actionType].RequiresConfirmation)
                await EnsureProjectWriteAccessAsync(userId, projectId);
            else
                await EnsureProjectActionAccessAsync(userId, projectId);
        }

        private async Task<Guid?> ResolveActionDestinationProjectAsync(string actionType, AiExecuteActionRequestDto request)
        {
            if (actionType is "create_project" or "create_goal" or "summarize_dashboard") return null;
            if (actionType is "update_task_status" or "update_task_priority" or "update_task_due_date" or "assign_task")
                return await ResolveTaskProjectIdAsync(RequirePayloadGuid(request.Payload, "taskId", "workTaskId"));
            if (actionType == "add_comment" &&
                string.Equals(NormalizeCommentEntityType(GetPayloadString(request.Payload, "entityType") ?? "WorkTask"), "WorkTask", StringComparison.Ordinal))
                return await ResolveTaskProjectIdAsync(RequirePayloadGuid(request.Payload, "entityId", "taskId", "workTaskId"));
            return ResolveProjectId(request);
        }

        private static AiActionStateDto ToActionState(AiActionExecution action) => new()
        {
            ActionId = action.Id,
            Type = action.ActionType,
            State = action.State,
            WorkspaceId = action.WorkspaceId,
            ProjectId = action.ProjectId,
            ExpiresAt = action.ExpiresAt,
            Preview = JsonSerializer.Deserialize<object>(action.PreviewJson, AiActionJsonOptions),
            Result = string.IsNullOrWhiteSpace(action.ResultJson) ? null : JsonSerializer.Deserialize<object>(action.ResultJson, AiActionJsonOptions),
            ErrorCode = action.ErrorCode
        };

        private static readonly IReadOnlyDictionary<string, AiActionDefinition> AiActionRegistry =
            new Dictionary<string, AiActionDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["create_project"] = new("Project", true),
                ["create_task"] = new("WorkTask", true),
                ["create_cycle"] = new("Sprint", true),
                ["create_module"] = new("Module", true),
                ["create_page"] = new("Page", true),
                ["create_view"] = new("ProjectView", true),
                ["create_intake_request"] = new("Intake", true),
                ["update_task_status"] = new("WorkTask", true),
                ["update_task_priority"] = new("WorkTask", true),
                ["update_task_due_date"] = new("WorkTask", true),
                ["assign_task"] = new("WorkTask", true),
                ["add_comment"] = new("Comment", true),
                ["create_goal"] = new("Goal", true),
                ["summarize_dashboard"] = new("Summary", false),
                ["summarize_project"] = new("Summary", false),
                ["list_work_items"] = new("WorkTaskList", false),
                ["list_cycles"] = new("SprintList", false),
                ["list_modules"] = new("ModuleList", false),
                ["list_pages"] = new("PageList", false),
                ["list_views"] = new("ProjectViewList", false),
                ["list_intakes"] = new("IntakeList", false),
                ["list_overdue_tasks"] = new("WorkTaskList", false),
                ["get_workload"] = new("Workload", false),
                ["explain_report"] = new("ReportExplanation", false),
                ["summarize_page"] = new("PageSummary", false),
                ["summarize_intakes"] = new("IntakeSummary", false),
                ["suggest_view_filter"] = new("ViewFilterSuggestion", false)
            };

        private static readonly JsonSerializerOptions AiActionJsonOptions = new(JsonSerializerDefaults.Web);
        private static readonly object AiActionLocksSync = new();
        private static readonly Dictionary<string, AiActionLockEntry> AiActionLocks = new(StringComparer.Ordinal);

        public sealed class AiConversationCreateRequest
        {
            public Guid? WorkspaceId { get; set; }
            public string? Title { get; set; }
        }

        public sealed class AiConversationSaveRequest
        {
            public string? Title { get; set; }
            public JsonElement Messages { get; set; }
        }

        public sealed class AiConversationRenameRequest
        {
            public string? Title { get; set; }
        }

        public sealed class AiAttachmentChatRequest
        {
            public Guid? WorkspaceId { get; set; }
            public Guid ConversationId { get; set; }
            public List<Guid> AttachmentIds { get; set; } = new();
            public string? Message { get; set; }
        }

        private sealed record AiActionDefinition(string EntityType, bool RequiresConfirmation);

        private sealed class AiActionLockEntry
        {
            public SemaphoreSlim Gate { get; } = new(1, 1);
            public int ReferenceCount { get; set; }
        }

        private sealed class AiActionLockLease(string key, AiActionLockEntry entry) : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                entry.Gate.Release();
                lock (AiActionLocksSync)
                {
                    entry.ReferenceCount--;
                    if (entry.ReferenceCount == 0) AiActionLocks.Remove(key);
                }
            }
        }

        private sealed record DuplicateTaskCandidate(Guid Id, string? SequenceId, string Title, string StatusName);

        private sealed class DuplicateTaskException(DuplicateTaskCandidate candidate)
            : InvalidOperationException("A task with the same or a very similar title already exists in this project.")
        {
            public DuplicateTaskCandidate Candidate { get; } = candidate;
        }

        private static async Task<IDisposable> AcquireAiActionLockAsync(string key)
        {
            AiActionLockEntry entry;
            lock (AiActionLocksSync)
            {
                if (!AiActionLocks.TryGetValue(key, out entry!))
                {
                    entry = new AiActionLockEntry();
                    AiActionLocks.Add(key, entry);
                }
                entry.ReferenceCount++;
            }

            await entry.Gate.WaitAsync();
            return new AiActionLockLease(key, entry);
        }

        private async Task<AiExecuteActionResponseDto> ExecuteCreateProjectAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var workspaceId = await ResolveActionWorkspaceAsync(userId, request.WorkspaceId);
            await EnsureWorkspaceWriteAccessAsync(userId, workspaceId);

            var firstWorkspaceId = await _dbContext.WorkspaceMembers
                .AsNoTracking()
                .Where(member => member.UserId == userId && member.IsActive)
                .OrderBy(member => member.JoinedAt)
                .Select(member => (Guid?)member.WorkspaceId)
                .FirstOrDefaultAsync();

            if (firstWorkspaceId.HasValue && firstWorkspaceId.Value != workspaceId)
            {
                throw new UnauthorizedAccessException("AI create_project chi ho tro workspace runtime mac dinh cua user hien tai.");
            }

            var name = GetPayloadString(request.Payload, "name", "title");
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Project name is required.");
            }

            var created = await _projectService.CreateAsync(userId, new CreateProjectDto
            {
                Name = LimitActionText(name, 300) ?? name.Trim(),
                Key = LimitActionText(GetPayloadString(request.Payload, "key", "identifier"), 24),
                Description = LimitActionText(GetPayloadString(request.Payload, "description"), 4000),
                StartDate = GetPayloadDate(request.Payload, "startDate") ?? DateTime.UtcNow.Date,
                EndDate = GetPayloadDate(request.Payload, "endDate"),
                NetworkType = GetPayloadString(request.Payload, "networkType") ?? "Public"
            });

            return new AiExecuteActionResponseDto
            {
                Type = "create_project",
                EntityType = "Project",
                EntityId = created.Id,
                Entity = created,
                Message = $"Created project {created.Name}."
            };
        }

        private async Task<AiExecuteActionResponseDto> ExecuteCreateTaskAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectWriteAccessAsync(userId, projectId);

            var title = GetPayloadString(request.Payload, "title", "name");
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Task title is required.");
            }

            var allowDuplicate = GetPayloadBool(request.Payload, "allowDuplicate") ?? false;
            if (!allowDuplicate)
            {
                var existingTasks = await _dbContext.WorkTasks
                    .AsNoTracking()
                    .Where(task => task.ProjectId == projectId && !task.IsDeleted)
                    .Select(task => new DuplicateTaskCandidate(
                        task.Id,
                        task.SequenceId,
                        task.Title,
                        task.TaskStatus.Name))
                    .ToListAsync();
                var duplicate = existingTasks.FirstOrDefault(task => TaskTitlesAreSimilar(task.Title, title));
                if (duplicate != null)
                {
                    throw new DuplicateTaskException(duplicate);
                }
            }

            var assigneeId = GetPayloadGuid(request.Payload, "assigneeId", "assignedUserId");
            var created = await _workTaskService.CreateAsync(userId, new CreateWorkTaskDto
            {
                ProjectId = projectId,
                Title = LimitActionText(title, 300) ?? title.Trim(),
                Description = LimitActionText(GetPayloadString(request.Payload, "description"), 4000),
                Priority = Math.Clamp(GetPayloadInt(request.Payload, "priority") ?? 3, 1, 4),
                StoryPoints = Math.Max(0, GetPayloadDouble(request.Payload, "storyPoints") ?? 0),
                DueDate = GetPayloadDate(request.Payload, "dueDate"),
                AssignedUserId = assigneeId,
                AssigneeIds = assigneeId.HasValue ? new List<Guid> { assigneeId.Value } : null,
                TypeName = GetPayloadString(request.Payload, "typeName") ?? "Task",
                StatusName = GetPayloadString(request.Payload, "statusName")
            });

            return new AiExecuteActionResponseDto
            {
                Type = "create_task",
                EntityType = "WorkTask",
                EntityId = created.Id,
                Entity = created,
                Message = $"Created task {created.Title}."
            };
        }

        private async Task<AiExecuteActionResponseDto> ExecuteCreateCycleAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectWriteAccessAsync(userId, projectId, ResourcePermissionCodes.SprintManage);

            var name = GetPayloadString(request.Payload, "name", "title");
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Cycle name is required.");
            }

            var startDate = GetPayloadDate(request.Payload, "startDate") ?? DateTime.UtcNow.Date;
            var endDate = GetPayloadDate(request.Payload, "endDate") ?? startDate.AddDays(13);
            if (endDate < startDate)
            {
                throw new ArgumentException("Cycle endDate must be after startDate.");
            }

            var cycle = new Sprint
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = LimitActionText(name, 300) ?? name.Trim(),
                StartDate = startDate,
                EndDate = endDate,
                Status = GetPayloadBool(request.Payload, "active", "status") ?? false,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Sprints.Add(cycle);
            await _dbContext.SaveChangesAsync();

            return new AiExecuteActionResponseDto
            {
                Type = "create_cycle",
                EntityType = "Sprint",
                EntityId = cycle.Id,
                Entity = new { cycle.Id, cycle.ProjectId, cycle.Name, cycle.StartDate, cycle.EndDate, cycle.Status },
                Message = $"Created cycle {cycle.Name}."
            };
        }

        private async Task<AiExecuteActionResponseDto> ExecuteCreateModuleAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectWriteAccessAsync(userId, projectId);

            var name = GetPayloadString(request.Payload, "name", "title");
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Module name is required.");
            }

            var leadId = GetPayloadGuid(request.Payload, "leadId");
            if (leadId.HasValue && !await IsActiveProjectMemberAsync(projectId, leadId.Value))
            {
                throw new ArgumentException("Lead is not an active project member.");
            }

            var module = new Module
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = LimitActionText(name, 300) ?? name.Trim(),
                Description = LimitActionText(GetPayloadString(request.Payload, "description"), 4000),
                StartDate = GetPayloadDate(request.Payload, "startDate"),
                TargetDate = GetPayloadDate(request.Payload, "targetDate", "endDate", "dueDate"),
                Status = GetPayloadString(request.Payload, "status") ?? "Backlog",
                LeadId = leadId ?? userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Modules.Add(module);
            await _dbContext.SaveChangesAsync();

            return new AiExecuteActionResponseDto
            {
                Type = "create_module",
                EntityType = "Module",
                EntityId = module.Id,
                Entity = new { module.Id, module.ProjectId, module.Name, module.Status, module.LeadId },
                Message = $"Created module {module.Name}."
            };
        }

        private async Task<AiExecuteActionResponseDto> ExecuteCreatePageAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectWriteAccessAsync(userId, projectId);

            var title = GetPayloadString(request.Payload, "title", "name");
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Page title is required.");
            }

            var maxSort = await _dbContext.Pages
                .Where(page => page.ProjectId == projectId)
                .MaxAsync(page => (int?)page.SortOrder) ?? 0;

            var page = new Page
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = LimitActionText(title, 300) ?? title.Trim(),
                Content = GetPayloadString(request.Payload, "content", "description") ?? string.Empty,
                SortOrder = maxSort + 1,
                CreatedById = userId,
                UpdatedById = userId,
                IsPrivate = GetPayloadBool(request.Payload, "isPrivate") ?? false,
                IsStarred = GetPayloadBool(request.Payload, "isStarred") ?? false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Pages.Add(page);
            await _dbContext.SaveChangesAsync();

            return new AiExecuteActionResponseDto
            {
                Type = "create_page",
                EntityType = "Page",
                EntityId = page.Id,
                Entity = new { page.Id, page.ProjectId, page.Title, page.SortOrder },
                Message = $"Created page {page.Title}."
            };
        }

        private async Task<AiExecuteActionResponseDto> ExecuteCreateViewAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectWriteAccessAsync(userId, projectId);

            var name = GetPayloadString(request.Payload, "name", "title");
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("View name is required.");
            }

            var queryMetadata = GetPayloadString(request.Payload, "queryMetadata", "filters") ?? "{}";
            if (!IsValidJsonObject(queryMetadata))
            {
                throw new ArgumentException("View queryMetadata must be a JSON object.");
            }

            var view = new ProjectView
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = LimitActionText(name, 300) ?? name.Trim(),
                Description = LimitActionText(GetPayloadString(request.Payload, "description"), 1000),
                QueryMetadata = queryMetadata,
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.ProjectViews.Add(view);
            await _dbContext.SaveChangesAsync();

            return new AiExecuteActionResponseDto
            {
                Type = "create_view",
                EntityType = "ProjectView",
                EntityId = view.Id,
                Entity = new { view.Id, view.ProjectId, view.Name, view.QueryMetadata },
                Message = $"Created view {view.Name}."
            };
        }

        private async Task<AiExecuteActionResponseDto> ExecuteCreateIntakeRequestAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectWriteAccessAsync(userId, projectId);

            var title = GetPayloadString(request.Payload, "title", "name");
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Intake title is required.");
            }

            var intake = new Intake
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = LimitActionText(title, 300) ?? title.Trim(),
                Description = LimitActionText(GetPayloadString(request.Payload, "description"), 4000),
                Source = GetPayloadString(request.Payload, "source") ?? "AI",
                Status = "Pending",
                Priority = Math.Clamp(GetPayloadInt(request.Payload, "priority") ?? 3, 1, 4),
                DesiredDueDate = GetPayloadDate(request.Payload, "desiredDueDate", "dueDate"),
                SubmittedById = userId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Intakes.Add(intake);
            await _dbContext.SaveChangesAsync();

            return new AiExecuteActionResponseDto
            {
                Type = "create_intake_request",
                EntityType = "Intake",
                EntityId = intake.Id,
                Entity = new { intake.Id, intake.ProjectId, intake.Title, intake.Status, intake.Priority },
                Message = $"Created intake request {intake.Title}."
            };
        }

        private async Task<AiExecuteActionResponseDto> ExecuteUpdateTaskStatusAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var taskId = RequirePayloadGuid(request.Payload, "taskId", "workTaskId");
            var statusName = GetPayloadString(request.Payload, "statusName", "status");
            var statusId = GetPayloadGuid(request.Payload, "taskStatusId", "statusId") ?? Guid.Empty;
            if (statusId == Guid.Empty && string.IsNullOrWhiteSpace(statusName))
            {
                throw new ArgumentException("StatusName or taskStatusId is required.");
            }

            var projectId = await ResolveTaskProjectIdAsync(taskId);
            await EnsureProjectWriteAccessAsync(userId, projectId);
            var currentRowVersion = await _dbContext.WorkTasks
                .AsNoTracking()
                .Where(task => task.Id == taskId && !task.IsDeleted)
                .Select(task => task.RowVersion)
                .SingleAsync();

            await _workTaskService.UpdateTaskStatusAsync(taskId, userId, new UpdateTaskStatusRequestDto
            {
                TaskStatusId = statusId,
                StatusName = statusName,
                RowVersion = ResolvePayloadRowVersion(request.Payload, currentRowVersion)
            });

            var task = await _dbContext.WorkTasks
                .AsNoTracking()
                .Include(item => item.TaskStatus)
                .Where(item => item.Id == taskId)
                .Select(item => new
                {
                    item.Id,
                    item.Title,
                    item.ProjectId,
                    StatusName = item.TaskStatus.Name,
                    item.RowVersion
                })
                .FirstAsync();

            return new AiExecuteActionResponseDto
            {
                Type = "update_task_status",
                EntityType = "WorkTask",
                EntityId = task.Id,
                Entity = task,
                Message = $"Updated task status to {task.StatusName}."
            };
        }

        private async Task<AiExecuteActionResponseDto> ExecuteUpdateTaskPriorityAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var taskId = RequirePayloadGuid(request.Payload, "taskId", "workTaskId");
            var priority = GetPayloadInt(request.Payload, "priority");
            if (!priority.HasValue)
            {
                throw new ArgumentException("Priority is required.");
            }

            var task = await LoadWritableTaskAsync(userId, taskId);
            _dbContext.Entry(task).Property(item => item.RowVersion).OriginalValue = ResolvePayloadRowVersion(request.Payload, task.RowVersion);
            task.Priority = Math.Clamp(priority.Value, 1, 4);
            task.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return new AiExecuteActionResponseDto
            {
                Type = "update_task_priority",
                EntityType = "WorkTask",
                EntityId = task.Id,
                Entity = new { task.Id, task.ProjectId, task.Title, task.Priority, task.RowVersion },
                Message = $"Updated task priority to P{task.Priority}."
            };
        }

        private async Task<AiExecuteActionResponseDto> ExecuteUpdateTaskDueDateAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var taskId = RequirePayloadGuid(request.Payload, "taskId", "workTaskId");
            if (!request.Payload.ContainsKey("dueDate"))
            {
                throw new ArgumentException("dueDate is required.");
            }

            var task = await LoadWritableTaskAsync(userId, taskId);
            _dbContext.Entry(task).Property(item => item.RowVersion).OriginalValue = ResolvePayloadRowVersion(request.Payload, task.RowVersion);
            task.DueDate = GetPayloadDate(request.Payload, "dueDate");
            task.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return new AiExecuteActionResponseDto
            {
                Type = "update_task_due_date",
                EntityType = "WorkTask",
                EntityId = task.Id,
                Entity = new { task.Id, task.ProjectId, task.Title, task.DueDate, task.RowVersion },
                Message = task.DueDate.HasValue
                    ? $"Updated task due date to {task.DueDate.Value:yyyy-MM-dd}."
                    : "Cleared task due date."
            };
        }

        private async Task<AiExecuteActionResponseDto> ExecuteAssignTaskAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var taskId = RequirePayloadGuid(request.Payload, "taskId", "workTaskId");
            var assigneeId = RequirePayloadGuid(request.Payload, "assigneeId", "userId", "assignedUserId");
            var task = await _dbContext.WorkTasks
                .Include(item => item.TaskAssignments)
                .Include(item => item.Sprint)
                .FirstOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted);

            if (task == null)
            {
                throw new ArgumentException("Task does not exist.");
            }
            if (SprintStatePolicy.IsTaskMutationLocked(task.Sprint, DateTime.UtcNow))
            {
                throw new InvalidOperationException("Cannot modify a task in a closed Sprint.");
            }

            await EnsureProjectWriteAccessAsync(userId, task.ProjectId);
            _dbContext.Entry(task).Property(item => item.RowVersion).OriginalValue = ResolvePayloadRowVersion(request.Payload, task.RowVersion);
            var assigneeIsMember = await _dbContext.ProjectMembers
                .AnyAsync(member => member.ProjectId == task.ProjectId && member.UserId == assigneeId && member.Status);

            if (!assigneeIsMember)
            {
                throw new ArgumentException("Assignee is not an active project member.");
            }

            task.AssignedUserId = assigneeId;
            task.UpdatedAt = DateTime.UtcNow;
            var assignment = task.TaskAssignments.FirstOrDefault(item => item.UserId == assigneeId);
            if (assignment == null)
            {
                _dbContext.TaskAssignments.Add(new TaskAssignment
                {
                    WorkTaskId = task.Id,
                    UserId = assigneeId,
                    Status = true,
                    ProgressPercent = 0
                });
            }
            else
            {
                assignment.Activate();
            }

            await _dbContext.SaveChangesAsync();

            var result = await _dbContext.WorkTasks
                .AsNoTracking()
                .Include(item => item.AssignedUser)
                .Where(item => item.Id == taskId)
                .Select(item => new
                {
                    item.Id,
                    item.Title,
                    item.ProjectId,
                    item.AssignedUserId,
                    item.RowVersion,
                    AssigneeName = item.AssignedUser != null ? item.AssignedUser.FullName ?? item.AssignedUser.Email : null
                })
                .FirstAsync();

            return new AiExecuteActionResponseDto
            {
                Type = "assign_task",
                EntityType = "WorkTask",
                EntityId = result.Id,
                Entity = result,
                Message = $"Assigned task to {result.AssigneeName ?? assigneeId.ToString()}."
            };
        }

        private async Task<AiExecuteActionResponseDto> ExecuteAddCommentAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var entityId = RequirePayloadGuid(request.Payload, "entityId", "taskId", "workTaskId");
            var entityType = GetPayloadString(request.Payload, "entityType") ?? "WorkTask";
            var content = GetPayloadString(request.Payload, "content", "message");
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Comment content is required.");
            }

            var normalizedEntityType = NormalizeCommentEntityType(entityType);
            await EnsureCommentEntityAccessAsync(userId, normalizedEntityType, entityId);

            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                EntityId = entityId,
                EntityType = normalizedEntityType,
                UserId = userId,
                Content = LimitActionText(content, 4000) ?? content.Trim(),
                ParentCommentId = GetPayloadGuid(request.Payload, "parentCommentId"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Comments.Add(comment);
            await _dbContext.SaveChangesAsync();

            return new AiExecuteActionResponseDto
            {
                Type = "add_comment",
                EntityType = "Comment",
                EntityId = comment.Id,
                Entity = new { comment.Id, comment.EntityType, comment.EntityId, comment.Content, comment.CreatedAt },
                Message = "Added comment."
            };
        }

        private async Task<AiExecuteActionResponseDto> ExecuteCreateGoalAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var workspaceId = GetPayloadGuid(request.Payload, "workspaceId") ?? request.WorkspaceId;
            if (!workspaceId.HasValue || workspaceId.Value == Guid.Empty)
            {
                workspaceId = await ResolveActionWorkspaceAsync(userId, null);
            }

            await EnsureWorkspaceWriteAccessAsync(userId, workspaceId.Value);

            var title = GetPayloadString(request.Payload, "title", "name");
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Goal title is required.");
            }

            var goalPayload = new Dictionary<string, object?>
            {
                ["title"] = LimitActionText(title, 300),
                ["description"] = LimitActionText(GetPayloadString(request.Payload, "description"), 4000),
                ["dueDate"] = GetPayloadDate(request.Payload, "dueDate"),
                ["ownerId"] = GetPayloadGuid(request.Payload, "ownerId") ?? userId,
                ["progress"] = Math.Clamp(GetPayloadInt(request.Payload, "progress") ?? 0, 0, 100),
                ["status"] = GetPayloadString(request.Payload, "status") ?? "On Track"
            };

            var created = await _goalService.CreateAsync(userId, workspaceId.Value, goalPayload);
            var entityId = TryExtractEntityId(created);

            return new AiExecuteActionResponseDto
            {
                Type = "create_goal",
                EntityType = "Goal",
                EntityId = entityId,
                Entity = created,
                Message = $"Created goal {title}."
            };
        }

        private async Task<AiExecuteActionResponseDto> ExecuteSummarizeDashboardAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var workspaceId = await ResolveActionWorkspaceAsync(userId, request.WorkspaceId);
            var projectIds = await GetAccessibleProjectIdsAsync(userId, workspaceId);
            var now = DateTime.UtcNow;
            var summary = await _dbContext.WorkTasks
                .AsNoTracking()
                .Include(task => task.TaskStatus)
                .Where(task => projectIds.Contains(task.ProjectId) && !task.IsDeleted && !task.IsArchived)
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Total = group.Count(),
                    Done = group.Count(task => task.TaskStatus.Name.Contains("Done") || task.TaskStatus.Name.Contains("Complete")),
                    InProgress = group.Count(task => task.TaskStatus.Name.Contains("Progress")),
                    Overdue = group.Count(task => task.DueDate.HasValue && task.DueDate.Value < now && !(task.TaskStatus.Name.Contains("Done") || task.TaskStatus.Name.Contains("Complete")))
                })
                .FirstOrDefaultAsync() ?? new { Total = 0, Done = 0, InProgress = 0, Overdue = 0 };

            return ReadOnlyAction("summarize_dashboard", "Summary", summary, $"Dashboard has {summary.Total} tasks, {summary.Done} done, {summary.Overdue} overdue.");
        }

        private async Task<AiExecuteActionResponseDto> ExecuteSummarizeProjectAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectActionAccessAsync(userId, projectId);
            var now = DateTime.UtcNow;
            var project = await _dbContext.Projects
                .AsNoTracking()
                .Where(item => item.Id == projectId)
                .Select(item => new { item.Id, item.Name, item.Description, item.StartDate, item.EndDate })
                .FirstAsync();
            var taskStats = await _dbContext.WorkTasks
                .AsNoTracking()
                .Include(task => task.TaskStatus)
                .Where(task => task.ProjectId == projectId && !task.IsDeleted)
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    Total = group.Count(),
                    Done = group.Count(task => task.TaskStatus.Name.Contains("Done") || task.TaskStatus.Name.Contains("Complete")),
                    InProgress = group.Count(task => task.TaskStatus.Name.Contains("Progress")),
                    Overdue = group.Count(task => task.DueDate.HasValue && task.DueDate.Value < now && !(task.TaskStatus.Name.Contains("Done") || task.TaskStatus.Name.Contains("Complete")))
                })
                .FirstOrDefaultAsync() ?? new { Total = 0, Done = 0, InProgress = 0, Overdue = 0 };

            var data = new { Project = project, Tasks = taskStats };
            return ReadOnlyAction("summarize_project", "Summary", data, $"Project {project.Name}: {taskStats.Total} tasks, {taskStats.Done} done.");
        }

        private async Task<AiExecuteActionResponseDto> ExecuteListWorkItemsAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectActionAccessAsync(userId, projectId);
            var tasks = await _dbContext.WorkTasks
                .AsNoTracking()
                .Include(task => task.TaskStatus)
                .Include(task => task.AssignedUser)
                .Where(task => task.ProjectId == projectId && !task.IsDeleted)
                .OrderByDescending(task => task.UpdatedAt)
                .Take(50)
                .Select(task => new
                {
                    task.Id,
                    task.SequenceId,
                    task.Title,
                    task.Priority,
                    task.DueDate,
                    StatusName = task.TaskStatus.Name,
                    AssigneeName = task.AssignedUser != null ? task.AssignedUser.FullName ?? task.AssignedUser.Email : null
                })
                .ToListAsync();

            return ReadOnlyAction("list_work_items", "WorkTaskList", tasks, $"Loaded {tasks.Count} work items.");
        }

        private async Task<AiExecuteActionResponseDto> ExecuteListCyclesAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectActionAccessAsync(userId, projectId);
            var cycles = await _dbContext.Sprints
                .AsNoTracking()
                .Where(cycle => cycle.ProjectId == projectId)
                .OrderByDescending(cycle => cycle.StartDate)
                .Take(30)
                .Select(cycle => new { cycle.Id, cycle.Name, cycle.StartDate, cycle.EndDate, IsActive = cycle.Status, TaskCount = cycle.WorkTasks.Count })
                .ToListAsync();

            return ReadOnlyAction("list_cycles", "SprintList", cycles, $"Loaded {cycles.Count} cycles.");
        }

        private async Task<AiExecuteActionResponseDto> ExecuteListModulesAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectActionAccessAsync(userId, projectId);
            var modules = await _dbContext.Modules
                .AsNoTracking()
                .Where(module => module.ProjectId == projectId)
                .OrderBy(module => module.Name)
                .Take(50)
                .Select(module => new
                {
                    module.Id,
                    module.Name,
                    module.Description,
                    module.Status,
                    module.StartDate,
                    module.TargetDate,
                    WorkItemCount = module.IssueModules.Count
                })
                .ToListAsync();

            return ReadOnlyAction("list_modules", "ModuleList", modules, $"Loaded {modules.Count} modules.");
        }

        private async Task<AiExecuteActionResponseDto> ExecuteListPagesAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectActionAccessAsync(userId, projectId);
            var pages = await _dbContext.Pages
                .AsNoTracking()
                .Where(page => page.ProjectId == projectId && !page.IsArchived)
                .OrderBy(page => page.SortOrder)
                .ThenBy(page => page.Title)
                .Take(50)
                .Select(page => new { page.Id, page.Title, page.IsLocked, page.IsPrivate, page.IsStarred, page.UpdatedAt })
                .ToListAsync();

            return ReadOnlyAction("list_pages", "PageList", pages, $"Loaded {pages.Count} pages.");
        }

        private async Task<AiExecuteActionResponseDto> ExecuteListViewsAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectActionAccessAsync(userId, projectId);
            var views = await _dbContext.ProjectViews
                .AsNoTracking()
                .Where(view => view.ProjectId == projectId)
                .OrderBy(view => view.Name)
                .Take(50)
                .Select(view => new { view.Id, view.Name, view.Description, view.IsFavorite, view.QueryMetadata, view.UpdatedAt })
                .ToListAsync();

            return ReadOnlyAction("list_views", "ProjectViewList", views, $"Loaded {views.Count} views.");
        }

        private async Task<AiExecuteActionResponseDto> ExecuteListIntakesAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectActionAccessAsync(userId, projectId);
            var intakes = await _dbContext.Intakes
                .AsNoTracking()
                .Where(intake => intake.ProjectId == projectId)
                .OrderByDescending(intake => intake.CreatedAt)
                .Take(50)
                .Select(intake => new
                {
                    intake.Id,
                    intake.Title,
                    intake.Source,
                    intake.Status,
                    intake.Priority,
                    intake.DesiredDueDate,
                    intake.CreatedAt,
                    intake.CreatedIssueId
                })
                .ToListAsync();

            return ReadOnlyAction("list_intakes", "IntakeList", intakes, $"Loaded {intakes.Count} intake requests.");
        }

        private async Task<AiExecuteActionResponseDto> ExecuteListOverdueTasksAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectActionAccessAsync(userId, projectId);
            var now = DateTime.UtcNow;
            var tasks = await _dbContext.WorkTasks
                .AsNoTracking()
                .Include(task => task.TaskStatus)
                .Include(task => task.AssignedUser)
                .Where(task => task.ProjectId == projectId && !task.IsDeleted && task.DueDate.HasValue && task.DueDate.Value < now)
                .Where(task => !(task.TaskStatus.Name.Contains("Done") || task.TaskStatus.Name.Contains("Complete")))
                .OrderBy(task => task.DueDate)
                .Take(20)
                .Select(task => new
                {
                    task.Id,
                    task.Title,
                    task.Priority,
                    task.DueDate,
                    StatusName = task.TaskStatus.Name,
                    AssigneeName = task.AssignedUser != null ? task.AssignedUser.FullName ?? task.AssignedUser.Email : null
                })
                .ToListAsync();

            return ReadOnlyAction("list_overdue_tasks", "WorkTaskList", tasks, $"Found {tasks.Count} overdue tasks.");
        }

        private async Task<AiExecuteActionResponseDto> ExecuteGetWorkloadAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectActionAccessAsync(userId, projectId);
            var workload = await _dbContext.ProjectMembers
                .AsNoTracking()
                .Where(member => member.ProjectId == projectId && member.Status)
                .Select(member => new
                {
                    member.UserId,
                    Name = member.User.FullName ?? member.User.Email,
                    member.ProjectRole,
                    ActiveTasks = _dbContext.WorkTasks.Count(task => task.ProjectId == projectId && !task.IsDeleted && task.AssignedUserId == member.UserId && !(task.TaskStatus.Name.Contains("Done") || task.TaskStatus.Name.Contains("Complete"))),
                    OverdueTasks = _dbContext.WorkTasks.Count(task => task.ProjectId == projectId && !task.IsDeleted && task.AssignedUserId == member.UserId && task.DueDate.HasValue && task.DueDate.Value < DateTime.UtcNow && !(task.TaskStatus.Name.Contains("Done") || task.TaskStatus.Name.Contains("Complete")))
                })
                .OrderByDescending(item => item.ActiveTasks)
                .ToListAsync();

            return ReadOnlyAction("get_workload", "Workload", workload, $"Loaded workload for {workload.Count} members.");
        }

        private async Task<AiExecuteActionResponseDto> ExecuteExplainReportAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectActionAccessAsync(userId, projectId);
            var data = await BuildReportSnapshotAsync(projectId);
            return ReadOnlyAction("explain_report", "ReportExplanation", data, "Generated report explanation data.");
        }

        private async Task<AiExecuteActionResponseDto> ExecuteSummarizePageAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var pageId = RequirePayloadGuid(request.Payload, "pageId");
            var page = await _dbContext.Pages
                .AsNoTracking()
                .Where(item => item.Id == pageId && !item.IsArchived)
                .Select(item => new { item.Id, item.ProjectId, item.Title, item.Content, item.UpdatedAt })
                .FirstOrDefaultAsync();

            if (page == null)
            {
                throw new ArgumentException("Page does not exist.");
            }

            await EnsureProjectActionAccessAsync(userId, page.ProjectId);
            var content = LimitActionText(page.Content, 1200) ?? string.Empty;
            return ReadOnlyAction("summarize_page", "PageSummary", new { page.Id, page.Title, ContentPreview = content, page.UpdatedAt }, $"Page {page.Title}: {content.Length} preview characters.");
        }

        private async Task<AiExecuteActionResponseDto> ExecuteSummarizeIntakesAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectActionAccessAsync(userId, projectId);
            var data = await _dbContext.Intakes
                .AsNoTracking()
                .Where(item => item.ProjectId == projectId)
                .GroupBy(item => item.Status)
                .Select(group => new { Status = group.Key, Count = group.Count(), Urgent = group.Count(item => item.Priority == 1) })
                .OrderBy(item => item.Status)
                .ToListAsync();

            return ReadOnlyAction("summarize_intakes", "IntakeSummary", data, $"Summarized intakes across {data.Count} statuses.");
        }

        private async Task<AiExecuteActionResponseDto> ExecuteSuggestViewFilterAsync(Guid userId, AiExecuteActionRequestDto request)
        {
            var projectId = ResolveProjectId(request);
            await EnsureProjectActionAccessAsync(userId, projectId);
            var statusNames = await _dbContext.TaskStatuses
                .AsNoTracking()
                .Where(status => status.ProjectId == projectId)
                .OrderBy(status => status.Position)
                .Select(status => status.Name)
                .ToListAsync();

            var priority = GetPayloadInt(request.Payload, "priority");
            var filter = new Dictionary<string, object?>
            {
                ["status"] = statusNames.FirstOrDefault(name => name.Contains("Progress", StringComparison.OrdinalIgnoreCase)) ?? statusNames.FirstOrDefault(),
                ["priority"] = priority,
                ["assigneeId"] = GetPayloadGuid(request.Payload, "assigneeId")
            }
            .Where(pair => pair.Value != null)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

            return ReadOnlyAction("suggest_view_filter", "ViewFilterSuggestion", filter, "Suggested a saved-view filter from project statuses.");
        }

        private static AiExecuteActionResponseDto ReadOnlyAction(string type, string entityType, object entity, string message)
        {
            return new AiExecuteActionResponseDto
            {
                Type = type,
                Status = "completed",
                EntityType = entityType,
                Entity = entity,
                Message = message
            };
        }

        private async Task<WorkTask> LoadWritableTaskAsync(Guid userId, Guid taskId)
        {
            var task = await _dbContext.WorkTasks
                .Include(item => item.Sprint)
                .FirstOrDefaultAsync(item => item.Id == taskId && !item.IsDeleted);

            if (task == null)
            {
                throw new ArgumentException("Task does not exist.");
            }
            if (SprintStatePolicy.IsTaskMutationLocked(task.Sprint, DateTime.UtcNow))
            {
                throw new InvalidOperationException("Cannot modify a task in a closed Sprint.");
            }

            await EnsureProjectWriteAccessAsync(userId, task.ProjectId);
            return task;
        }

        private async Task EnsureCommentEntityAccessAsync(Guid userId, string entityType, Guid entityId)
        {
            if (entityType == "WorkTask")
            {
                var projectId = await ResolveTaskProjectIdAsync(entityId);
                await EnsureProjectActionAccessAsync(userId, projectId);
                return;
            }

            if (entityType == "Project")
            {
                await EnsureProjectActionAccessAsync(userId, entityId);
                return;
            }

            if (entityType == "Goal")
            {
                var workspaceId = await _dbContext.Goals
                    .AsNoTracking()
                    .Where(goal => goal.Id == entityId && !goal.IsArchived)
                    .Select(goal => (Guid?)goal.WorkspaceId)
                    .FirstOrDefaultAsync();

                if (!workspaceId.HasValue || !await UserHasWorkspaceAccessAsync(userId, workspaceId.Value))
                {
                    throw new UnauthorizedAccessException("You do not have permission for this goal.");
                }

                return;
            }

            throw new ArgumentException("Unsupported comment entity type.");
        }

        private static string NormalizeCommentEntityType(string entityType)
        {
            var normalized = entityType.Trim().Replace("-", string.Empty).Replace("_", string.Empty);
            if (string.Equals(normalized, "task", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "worktask", StringComparison.OrdinalIgnoreCase))
            {
                return "WorkTask";
            }

            if (string.Equals(normalized, "project", StringComparison.OrdinalIgnoreCase))
            {
                return "Project";
            }

            if (string.Equals(normalized, "goal", StringComparison.OrdinalIgnoreCase))
            {
                return "Goal";
            }

            return entityType.Trim();
        }

        private static bool IsValidJsonObject(string value)
        {
            try
            {
                using var document = JsonDocument.Parse(value);
                return document.RootElement.ValueKind == JsonValueKind.Object;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private async Task<bool> IsActiveProjectMemberAsync(Guid projectId, Guid memberId)
        {
            return await _dbContext.ProjectMembers
                .AsNoTracking()
                .AnyAsync(member => member.ProjectId == projectId && member.UserId == memberId && member.Status);
        }

        private async Task<List<Guid>> GetAccessibleProjectIdsAsync(Guid userId, Guid workspaceId)
        {
            if (!await UserHasWorkspaceAccessAsync(userId, workspaceId))
            {
                return new List<Guid>();
            }

            return await _dbContext.ProjectMembers
                .AsNoTracking()
                .Where(member => member.UserId == userId && member.Status && member.Project.WorkspaceId == workspaceId)
                .Select(member => member.ProjectId)
                .ToListAsync();
        }

        private async Task<object> BuildReportSnapshotAsync(Guid projectId)
        {
            var now = DateTime.UtcNow;
            var byStatus = await _dbContext.WorkTasks
                .AsNoTracking()
                .Include(task => task.TaskStatus)
                .Where(task => task.ProjectId == projectId && !task.IsDeleted)
                .GroupBy(task => task.TaskStatus.Name)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .OrderBy(item => item.Status)
                .ToListAsync();

            var byPriority = await _dbContext.WorkTasks
                .AsNoTracking()
                .Where(task => task.ProjectId == projectId && !task.IsDeleted)
                .GroupBy(task => task.Priority)
                .Select(group => new { Priority = group.Key, Count = group.Count() })
                .OrderBy(item => item.Priority)
                .ToListAsync();

            var overdueCount = await _dbContext.WorkTasks
                .AsNoTracking()
                .Include(task => task.TaskStatus)
                .CountAsync(task => task.ProjectId == projectId && !task.IsDeleted && task.DueDate.HasValue && task.DueDate.Value < now && !(task.TaskStatus.Name.Contains("Done") || task.TaskStatus.Name.Contains("Complete")));

            return new { ByStatus = byStatus, ByPriority = byPriority, OverdueCount = overdueCount };
        }

        private async Task EnsureProjectActionAccessAsync(Guid userId, Guid projectId)
        {
            var authorization = await _authorizationService.AuthorizeProjectAsync(
                userId,
                projectId,
                ResourcePermissionCodes.ProjectRead);
            if (!authorization.Succeeded)
            {
                throw new UnauthorizedAccessException("You do not have permission for this project.");
            }
        }

        private async Task EnsureProjectWriteAccessAsync(
            Guid userId,
            Guid projectId,
            string permissionCode = ResourcePermissionCodes.ProjectWrite)
        {
            var authorization = await _authorizationService.AuthorizeProjectAsync(
                userId,
                projectId,
                permissionCode);
            if (!authorization.Succeeded)
            {
                throw new UnauthorizedAccessException("You do not have permission to modify this project.");
            }
        }

        private async Task EnsureWorkspaceWriteAccessAsync(Guid userId, Guid workspaceId)
        {
            if (!await UserHasWorkspaceWriteAccessAsync(userId, workspaceId))
            {
                throw new UnauthorizedAccessException("You do not have permission to modify this workspace.");
            }
        }

        private async Task<bool> UserHasWorkspaceWriteAccessAsync(Guid userId, Guid workspaceId)
        {
            var authorization = await _authorizationService.AuthorizeWorkspaceAsync(
                userId,
                workspaceId,
                ResourcePermissionCodes.WorkspaceManage);
            return authorization.Succeeded;
        }

        private async Task<Guid> ResolveActionWorkspaceAsync(Guid userId, Guid? requestedWorkspaceId)
        {
            if (requestedWorkspaceId.HasValue && requestedWorkspaceId.Value != Guid.Empty)
            {
                if (!await UserHasWorkspaceAccessAsync(userId, requestedWorkspaceId.Value))
                {
                    throw new UnauthorizedAccessException("You do not have access to this workspace.");
                }

                return requestedWorkspaceId.Value;
            }

            var workspaceId = await _dbContext.WorkspaceMembers
                .AsNoTracking()
                .Where(member => member.UserId == userId && member.IsActive)
                .OrderBy(member => member.JoinedAt)
                .Select(member => (Guid?)member.WorkspaceId)
                .FirstOrDefaultAsync();

            return workspaceId ?? throw new UnauthorizedAccessException("User has no active workspace.");
        }

        private Guid ResolveProjectId(AiExecuteActionRequestDto request)
        {
            var projectId = GetPayloadGuid(request.Payload, "projectId") ?? request.ProjectId;
            if (!projectId.HasValue || projectId.Value == Guid.Empty)
            {
                throw new ArgumentException("ProjectId is required.");
            }

            return projectId.Value;
        }

        private async Task<Guid> ResolveTaskProjectIdAsync(Guid taskId)
        {
            var projectId = await _dbContext.WorkTasks
                .AsNoTracking()
                .Where(task => task.Id == taskId && !task.IsDeleted)
                .Select(task => (Guid?)task.ProjectId)
                .FirstOrDefaultAsync();

            return projectId ?? throw new ArgumentException("Task does not exist.");
        }

        private async Task WriteAiActionAuditAsync(
            Guid userId,
            string idempotencyKey,
            string actionType,
            string status,
            AiExecuteActionResponseDto? result,
            string? error = null)
        {
            var details = JsonSerializer.Serialize(new
            {
                actionType,
                result,
                error
            }, AiActionJsonOptions);

            _dbContext.SystemAuditLogs.Add(new SystemAuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = "AI_ACTION_EXECUTE",
                Resource = idempotencyKey,
                Status = status,
                IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Details = details,
                CreatedAt = DateTime.UtcNow
            });

            if (result?.EntityId is Guid entityId)
            {
                _dbContext.SiteAuditLogs.Add(new SiteAuditLog
                {
                    EntityId = entityId,
                    EntityType = result.EntityType,
                    Action = $"AI_{actionType}",
                    NewValue = result.Message,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _dbContext.SaveChangesAsync();
        }

        private static bool TryReadExecutedAction(string? details, out AiExecuteActionResponseDto response)
        {
            response = new AiExecuteActionResponseDto();
            if (string.IsNullOrWhiteSpace(details))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(details);
                if (!document.RootElement.TryGetProperty("result", out var result) ||
                    result.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    return false;
                }

                response = result.Deserialize<AiExecuteActionResponseDto>(AiActionJsonOptions) ?? new AiExecuteActionResponseDto();
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string NormalizeActionType(string actionType)
        {
            var normalized = actionType.Trim().Replace("-", "_").ToLowerInvariant();
            return normalized switch
            {
                "create_work_item" or "create_issue" => "create_task",
                "update_work_item_status" or "move_work_item" or "move_task" => "update_task_status",
                "assign_work_item" => "assign_task",
                "create_sprint" => "create_cycle",
                "list_sprints" or "summarize_cycles" => "list_cycles",
                "summarize_modules" => "list_modules",
                "create_intake" => "create_intake_request",
                "list_intake_requests" or "summarize_intake_requests" => "list_intakes",
                "create_saved_view" => "create_view",
                "list_saved_views" or "summarize_views" => "list_views",
                "create_document_page" => "create_page",
                "summarize_pages" => "list_pages",
                "list_tasks" or "summarize_work_items" => "list_work_items",
                "summarize_report" or "generate_report" or "report_summary" => "explain_report",
                _ => normalized
            };
        }

        private static bool IsWaveAudio(byte[] bytes) =>
            bytes.Length >= 44 &&
            bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            bytes.AsSpan(8, 4).SequenceEqual("WAVE"u8);

        private static bool TaskTitlesAreSimilar(string existingTitle, string requestedTitle)
        {
            var existingTokens = NormalizeTaskTitle(existingTitle).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var requestedTokens = NormalizeTaskTitle(requestedTitle).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (existingTokens.SequenceEqual(requestedTokens))
            {
                return true;
            }

            if (existingTokens.Length < 3 || requestedTokens.Length < 3)
            {
                return false;
            }

            var existingSet = existingTokens.ToHashSet(StringComparer.Ordinal);
            var requestedSet = requestedTokens.ToHashSet(StringComparer.Ordinal);
            var intersection = existingSet.Intersect(requestedSet).Count();
            var union = existingSet.Union(requestedSet).Count();
            return union > 0 && (double)intersection / union >= 0.8;
        }

        private static string NormalizeTaskTitle(string title) =>
            string.Join(' ', title.Trim().ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        private static string NormalizeConversationTitle(string? title)
        {
            var normalized = string.Join(' ', (title ?? string.Empty).Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrWhiteSpace(normalized)) return "Cuộc trò chuyện mới";
            return normalized.Length <= 180 ? normalized : normalized[..180];
        }

        private static object ReadConversationMessages(string messagesJson)
        {
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(messagesJson, AiActionJsonOptions);
            }
            catch (JsonException)
            {
                return Array.Empty<object>();
            }
        }

        private static string BuildIdempotencyKey(Guid userId, string actionType, string? clientKey, Dictionary<string, object?> payload)
        {
            var raw = string.IsNullOrWhiteSpace(clientKey)
                ? JsonSerializer.Serialize(payload.OrderBy(item => item.Key), AiActionJsonOptions)
                : clientKey.Trim();
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{userId:N}:{actionType}:{raw}"));
            return $"ai-action:{Convert.ToHexString(bytes).ToLowerInvariant()}";
        }

        private static string? GetPayloadString(Dictionary<string, object?> payload, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!payload.TryGetValue(key, out var value) || value == null)
                {
                    continue;
                }

                if (value is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        return element.GetString();
                    }

                    return element.ToString();
                }

                return value.ToString();
            }

            return null;
        }

        private static Guid RequirePayloadGuid(Dictionary<string, object?> payload, params string[] keys)
        {
            return GetPayloadGuid(payload, keys) ?? throw new ArgumentException($"{keys[0]} is required.");
        }

        private static byte[] ResolvePayloadRowVersion(Dictionary<string, object?> payload, byte[]? currentVersion)
        {
            var encoded = GetPayloadString(payload, "rowVersion");
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return currentVersion is { Length: > 0 }
                    ? currentVersion
                    : throw new ArgumentException("A current rowVersion could not be resolved for the AI task mutation.");
            }

            try
            {
                var version = Convert.FromBase64String(encoded);
                if (version.Length == 0)
                {
                    throw new FormatException();
                }

                return version;
            }
            catch (FormatException)
            {
                throw new ArgumentException("rowVersion must be a valid base64 value.");
            }
        }

        private static Guid? GetPayloadGuid(Dictionary<string, object?> payload, params string[] keys)
        {
            var value = GetPayloadString(payload, keys);
            return Guid.TryParse(value, out var parsed) && parsed != Guid.Empty ? parsed : null;
        }

        private static int? GetPayloadInt(Dictionary<string, object?> payload, params string[] keys)
        {
            var value = GetPayloadString(payload, keys);
            return int.TryParse(value, out var parsed) ? parsed : null;
        }

        private static bool? GetPayloadBool(Dictionary<string, object?> payload, params string[] keys)
        {
            var value = GetPayloadString(payload, keys);
            if (bool.TryParse(value, out var parsed))
            {
                return parsed;
            }

            if (int.TryParse(value, out var numeric))
            {
                return numeric != 0;
            }

            return null;
        }

        private static double? GetPayloadDouble(Dictionary<string, object?> payload, params string[] keys)
        {
            var value = GetPayloadString(payload, keys);
            return double.TryParse(value, out var parsed) ? parsed : null;
        }

        private static DateTime? GetPayloadDate(Dictionary<string, object?> payload, params string[] keys)
        {
            string? value = null;
            foreach (var key in keys)
            {
                if (payload.TryGetValue(key, out var raw) && raw != null)
                {
                    value = raw is JsonElement element ? element.ToString() : raw.ToString();
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (DateTime.TryParseExact(trimmed,
                    new[] { "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "MM/dd/yyyy", "M/d/yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var exact))
            {
                return DateTime.SpecifyKind(exact.Date, DateTimeKind.Utc);
            }

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial > 0 && serial < 60000)
            {
                return DateTime.SpecifyKind(DateTime.FromOADate(serial).Date, DateTimeKind.Utc);
            }

            throw new ArgumentException($"{keys[0]} must be yyyy-MM-dd, dd/MM/yyyy or Excel serial date.");
        }

        private static string? LimitActionText(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        private static Guid? TryExtractEntityId(object? entity)
        {
            if (entity == null)
            {
                return null;
            }

            using var document = JsonDocument.Parse(JsonSerializer.Serialize(entity, AiActionJsonOptions));
            if (document.RootElement.TryGetProperty("id", out var id) && Guid.TryParse(id.ToString(), out var parsed))
            {
                return parsed;
            }

            if (document.RootElement.TryGetProperty("Id", out var upperId) && Guid.TryParse(upperId.ToString(), out parsed))
            {
                return parsed;
            }

            return null;
        }

        private async Task EnsureAiAssigneeAccessAsync(Guid userId, Guid projectId)
        {
            var authorization = await _authorizationService.AuthorizeProjectAsync(
                userId,
                projectId,
                ResourcePermissionCodes.ProjectWrite);
            if (!authorization.Succeeded)
            {
                throw new UnauthorizedAccessException("Ban khong co quyen dung AI goi y assignee cho project nay.");
            }
        }
    }

    public class AiCommandRequest
    {
        public string Prompt { get; set; } = string.Empty;
        public Guid? ProjectId { get; set; }
        public string? Locale { get; set; }
        public List<TaskManagement.Application.Interfaces.AiChatMessageDto>? History { get; set; }
    }

    public class ProjectAiStats
    {
        public int Total { get; set; }
        public int Done { get; set; }
        public int InProgress { get; set; }
        public int Todo { get; set; }
        public int Overdue { get; set; }
    }
}
