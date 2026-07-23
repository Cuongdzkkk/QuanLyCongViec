using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace TaskManagement.Infrastructure.Services
{
    public sealed class AiAttachmentService : IAiAttachmentService
    {
        private const long ImageMaxBytes = 5 * 1024 * 1024;
        private const long DocumentMaxBytes = 10 * 1024 * 1024;
        private const int MaxChunksPerRequest = 8;
        private const int MaxRetrievedCharacters = 12_000;
        private const int MaxImageCount = 4;
        private const long MaxImageBytesPerRequest = 12 * 1024 * 1024;
        private readonly ApplicationDbContext _dbContext;
        private readonly IAiService _aiService;
        private readonly string _storageRoot;

        private static readonly IReadOnlyDictionary<string, AttachmentRule> Rules =
            new Dictionary<string, AttachmentRule>(StringComparer.OrdinalIgnoreCase)
            {
                [".png"] = new("image", ImageMaxBytes, ["image/png"]),
                [".jpg"] = new("image", ImageMaxBytes, ["image/jpeg"]),
                [".jpeg"] = new("image", ImageMaxBytes, ["image/jpeg"]),
                [".webp"] = new("image", ImageMaxBytes, ["image/webp"]),
                [".pdf"] = new("document", DocumentMaxBytes, ["application/pdf"]),
                [".docx"] = new("document", DocumentMaxBytes, ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"]),
                [".txt"] = new("document", DocumentMaxBytes, ["text/plain"]),
                [".md"] = new("document", DocumentMaxBytes, ["text/markdown", "text/plain"]),
                [".csv"] = new("document", DocumentMaxBytes, ["text/csv", "application/csv", "application/vnd.ms-excel"]),
                [".xlsx"] = new("document", DocumentMaxBytes, ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"]),
                [".pptx"] = new("document", DocumentMaxBytes, ["application/vnd.openxmlformats-officedocument.presentationml.presentation"]),
                [".json"] = new("document", DocumentMaxBytes, ["application/json", "text/json", "text/plain"]),
                [".js"] = AttachmentRule.SourceCode,
                [".ts"] = AttachmentRule.SourceCode,
                [".vue"] = AttachmentRule.SourceCode,
                [".html"] = AttachmentRule.SourceCode,
                [".css"] = AttachmentRule.SourceCode,
                [".scss"] = AttachmentRule.SourceCode,
                [".cs"] = AttachmentRule.SourceCode,
                [".java"] = AttachmentRule.SourceCode,
                [".py"] = AttachmentRule.SourceCode,
                [".go"] = AttachmentRule.SourceCode,
                [".sql"] = AttachmentRule.SourceCode,
                [".xml"] = AttachmentRule.SourceCode,
                [".yaml"] = AttachmentRule.SourceCode,
                [".yml"] = AttachmentRule.SourceCode,
                [".sh"] = AttachmentRule.SourceCode,
                [".ps1"] = AttachmentRule.SourceCode
            };

        public AiAttachmentService(ApplicationDbContext dbContext, IAiService aiService, IHostEnvironment environment)
        {
            _dbContext = dbContext;
            _aiService = aiService;
            _storageRoot = Path.Combine(environment.ContentRootPath, "private-uploads", "ai-attachments");
        }

        public async Task<AiAttachmentDto> UploadAsync(
            Guid userId,
            Guid workspaceId,
            Guid conversationId,
            string fileName,
            string mimeType,
            Stream content,
            long fileSize,
            CancellationToken cancellationToken = default)
        {
            var safeFileName = Path.GetFileName(fileName ?? string.Empty).Trim();
            var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(safeFileName) || !Rules.TryGetValue(extension, out var rule))
            {
                throw new InvalidDataException("Định dạng attachment không được hỗ trợ.");
            }

            var normalizedMimeType = (mimeType ?? string.Empty).Split(';', 2)[0].Trim().ToLowerInvariant();
            if (!rule.AcceptsMime(normalizedMimeType))
            {
                throw new InvalidDataException($"Loại nội dung '{normalizedMimeType}' không khớp với {extension}.");
            }
            if (fileSize <= 0 || fileSize > rule.MaxBytes)
            {
                throw new InvalidDataException($"Dung lượng file phải từ 1 byte đến {rule.MaxBytes / 1024 / 1024}MB.");
            }

            var ownsConversation = await _dbContext.AiConversations
                .AsNoTracking()
                .AnyAsync(item => item.Id == conversationId && item.UserId == userId && item.WorkspaceId == workspaceId, cancellationToken);
            if (!ownsConversation)
            {
                throw new UnauthorizedAccessException("Conversation không thuộc user/workspace hiện tại.");
            }

            using var memory = new MemoryStream((int)fileSize);
            await content.CopyToAsync(memory, cancellationToken);
            var bytes = memory.ToArray();
            if (bytes.LongLength != fileSize || !HasValidSignature(extension, bytes))
            {
                throw new InvalidDataException("Nội dung file không hợp lệ hoặc không khớp định dạng.");
            }
            ValidateContainerSafety(extension, bytes);

            var sha256 = Convert.ToHexString(SHA256.HashData(bytes));
            var existing = await _dbContext.AiAttachments
                .AsNoTracking()
                .FirstOrDefaultAsync(item =>
                    item.UserId == userId &&
                    item.WorkspaceId == workspaceId &&
                    item.ConversationId == conversationId &&
                    item.Sha256 == sha256 &&
                    item.Status == "Ready", cancellationToken);
            if (existing != null)
            {
                return await MapAsync(existing, cancellationToken);
            }

            var now = DateTime.UtcNow;
            var attachment = new AiAttachment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                WorkspaceId = workspaceId,
                ConversationId = conversationId,
                FileName = safeFileName.Length <= 255 ? safeFileName : safeFileName[..255],
                StoredFileName = $"{Guid.NewGuid():N}{extension}",
                MimeType = normalizedMimeType,
                Extension = extension,
                Kind = rule.Kind,
                Sha256 = sha256,
                FileSize = bytes.LongLength,
                Status = "Processing",
                CreatedAt = now,
                UpdatedAt = now
            };
            if (rule.Kind == "image")
            {
                (attachment.Width, attachment.Height) = ReadImageDimensions(extension, bytes);
            }

            _dbContext.AiAttachments.Add(attachment);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var directory = AttachmentDirectory(workspaceId, userId);
            Directory.CreateDirectory(directory);
            var storedPath = Path.Combine(directory, attachment.StoredFileName);

            try
            {
                await File.WriteAllBytesAsync(storedPath, bytes, cancellationToken);
                if (rule.Kind == "document")
                {
                    var sections = ExtractSections(extension, bytes);
                    var chunks = BuildChunks(attachment.Id, sections, now);
                    if (chunks.Count == 0)
                    {
                        throw new InvalidDataException("Không trích xuất được nội dung văn bản từ tài liệu.");
                    }
                    _dbContext.AiAttachmentChunks.AddRange(chunks);
                }

                attachment.Status = "Ready";
                attachment.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return await MapAsync(attachment, cancellationToken);
            }
            catch (Exception exception)
            {
                if (File.Exists(storedPath)) File.Delete(storedPath);
                attachment.Status = "Failed";
                attachment.ErrorMessage = Limit(exception.Message, 500);
                attachment.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                throw new InvalidDataException(attachment.ErrorMessage, exception);
            }
        }

        public async Task<AiAttachmentDto?> GetAsync(Guid userId, Guid attachmentId, CancellationToken cancellationToken = default)
        {
            var attachment = await OwnedAttachmentQuery(userId)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == attachmentId, cancellationToken);
            return attachment == null ? null : await MapAsync(attachment, cancellationToken);
        }

        public async Task<AiAttachmentContentDto?> GetContentAsync(Guid userId, Guid attachmentId, CancellationToken cancellationToken = default)
        {
            var attachment = await OwnedAttachmentQuery(userId)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == attachmentId && item.Status == "Ready", cancellationToken);
            if (attachment == null) return null;

            var path = StoredPath(attachment);
            if (!File.Exists(path)) return null;
            return new AiAttachmentContentDto
            {
                FileName = attachment.FileName,
                MimeType = attachment.MimeType,
                Bytes = await File.ReadAllBytesAsync(path, cancellationToken)
            };
        }

        public async Task DeleteAsync(Guid userId, Guid attachmentId, CancellationToken cancellationToken = default)
        {
            var attachment = await OwnedAttachmentQuery(userId)
                .FirstOrDefaultAsync(item => item.Id == attachmentId, cancellationToken);
            if (attachment == null) return;
            var path = StoredPath(attachment);
            _dbContext.AiAttachments.Remove(attachment);
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (File.Exists(path)) File.Delete(path);
        }

        public async Task<AiAttachmentChatResponseDto> ChatAsync(
            Guid userId,
            Guid workspaceId,
            Guid conversationId,
            IReadOnlyCollection<Guid> attachmentIds,
            string message,
            CancellationToken cancellationToken = default)
        {
            if (attachmentIds.Count == 0) throw new InvalidDataException("Cần ít nhất một attachment.");
            if (string.IsNullOrWhiteSpace(message)) message = "Hãy phân tích các attachment này và nêu những điểm quan trọng.";

            var ids = attachmentIds.Distinct().ToList();
            var attachments = await _dbContext.AiAttachments
                .AsNoTracking()
                .Where(item => ids.Contains(item.Id) && item.UserId == userId && item.WorkspaceId == workspaceId && item.ConversationId == conversationId && item.Status == "Ready")
                .ToListAsync(cancellationToken);
            if (attachments.Count != ids.Count)
            {
                throw new UnauthorizedAccessException("Một hoặc nhiều attachment không thuộc conversation hiện tại.");
            }

            var documentIds = attachments.Where(item => item.Kind == "document").Select(item => item.Id).ToList();
            var chunks = await _dbContext.AiAttachmentChunks
                .AsNoTracking()
                .Where(item => documentIds.Contains(item.AttachmentId))
                .ToListAsync(cancellationToken);
            var selectedChunks = SelectChunks(message, chunks);

            var sources = new List<AiAttachmentRagSourceDto>();
            var citations = new List<AiAttachmentCitationDto>();
            var sourceNumber = 1;
            foreach (var chunk in selectedChunks)
            {
                var attachment = attachments.First(item => item.Id == chunk.AttachmentId);
                var sourceId = $"S{sourceNumber++}";
                sources.Add(new AiAttachmentRagSourceDto
                {
                    AttachmentId = attachment.Id,
                    SourceId = sourceId,
                    FileName = attachment.FileName,
                    Locator = chunk.Locator,
                    Content = AiSafetyGuard.RedactSecrets(chunk.Content)
                });
                citations.Add(new AiAttachmentCitationDto
                {
                    AttachmentId = attachment.Id,
                    SourceId = sourceId,
                    FileName = attachment.FileName,
                    Locator = chunk.Locator,
                    Excerpt = Limit(AiSafetyGuard.RedactSecrets(chunk.Content), 220)
                });
            }

            var images = new List<AiAttachmentImageInputDto>();
            long imageBytes = 0;
            foreach (var attachment in attachments.Where(item => item.Kind == "image").Take(MaxImageCount))
            {
                if (imageBytes + attachment.FileSize > MaxImageBytesPerRequest) break;
                var path = StoredPath(attachment);
                if (!File.Exists(path)) continue;
                var sourceId = $"S{sourceNumber++}";
                var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                imageBytes += bytes.LongLength;
                images.Add(new AiAttachmentImageInputDto
                {
                    AttachmentId = attachment.Id,
                    SourceId = sourceId,
                    FileName = attachment.FileName,
                    MimeType = attachment.MimeType,
                    Bytes = bytes
                });
                citations.Add(new AiAttachmentCitationDto
                {
                    AttachmentId = attachment.Id,
                    SourceId = sourceId,
                    FileName = attachment.FileName,
                    Locator = "ảnh",
                    Excerpt = attachment.Width.HasValue && attachment.Height.HasValue
                        ? $"Ảnh {attachment.Width}×{attachment.Height}"
                        : "Ảnh đính kèm"
                });
            }

            if (sources.Count == 0 && images.Count == 0)
            {
                throw new InvalidDataException("Không có nội dung attachment có thể truy xuất.");
            }

            var answer = await _aiService.ChatWithAttachmentsAsync(userId, message.Trim(), sources, images);
            return new AiAttachmentChatResponseDto { Answer = answer, Citations = citations };
        }

        private IQueryable<AiAttachment> OwnedAttachmentQuery(Guid userId) =>
            _dbContext.AiAttachments.Where(item => item.UserId == userId);

        private string AttachmentDirectory(Guid workspaceId, Guid userId) =>
            Path.Combine(_storageRoot, workspaceId.ToString("N"), userId.ToString("N"));

        private string StoredPath(AiAttachment attachment) =>
            Path.Combine(AttachmentDirectory(attachment.WorkspaceId, attachment.UserId), attachment.StoredFileName);

        private async Task<AiAttachmentDto> MapAsync(AiAttachment attachment, CancellationToken cancellationToken)
        {
            var chunkCount = attachment.Kind == "document"
                ? await _dbContext.AiAttachmentChunks.CountAsync(item => item.AttachmentId == attachment.Id, cancellationToken)
                : 0;
            return new AiAttachmentDto
            {
                Id = attachment.Id,
                WorkspaceId = attachment.WorkspaceId,
                ConversationId = attachment.ConversationId,
                FileName = attachment.FileName,
                MimeType = attachment.MimeType,
                Kind = attachment.Kind,
                Status = attachment.Status,
                ErrorMessage = attachment.ErrorMessage,
                FileSize = attachment.FileSize,
                Width = attachment.Width,
                Height = attachment.Height,
                ChunkCount = chunkCount,
                ContentUrl = $"/api/ai/attachments/{attachment.Id}/content",
                CreatedAt = attachment.CreatedAt
            };
        }

        private static List<AiAttachmentChunk> SelectChunks(string query, List<AiAttachmentChunk> chunks)
        {
            var terms = Tokenize(query).ToArray();
            var ranked = chunks
                .Select(chunk => new
                {
                    Chunk = chunk,
                    Score = terms.Sum(term => Regex.Matches(Normalize(chunk.Content), Regex.Escape(term)).Count)
                })
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Chunk.ChunkIndex)
                .ToList();

            var selected = new List<AiAttachmentChunk>();
            var characters = 0;
            foreach (var candidate in ranked)
            {
                if (selected.Count >= MaxChunksPerRequest || characters + candidate.Chunk.Content.Length > MaxRetrievedCharacters) continue;
                selected.Add(candidate.Chunk);
                characters += candidate.Chunk.Content.Length;
            }
            return selected;
        }

        private static List<AiAttachmentChunk> BuildChunks(Guid attachmentId, IReadOnlyList<ExtractedSection> sections, DateTime createdAt)
        {
            const int chunkSize = 1_500;
            const int overlap = 180;
            var chunks = new List<AiAttachmentChunk>();
            foreach (var section in sections)
            {
                var normalized = Regex.Replace(section.Content, @"\s+", " ").Trim();
                for (var offset = 0; offset < normalized.Length; offset += chunkSize - overlap)
                {
                    var length = Math.Min(chunkSize, normalized.Length - offset);
                    var content = normalized.Substring(offset, length).Trim();
                    if (content.Length < 2) continue;
                    chunks.Add(new AiAttachmentChunk
                    {
                        Id = Guid.NewGuid(),
                        AttachmentId = attachmentId,
                        ChunkIndex = chunks.Count,
                        Locator = normalized.Length > chunkSize ? $"{section.Locator}, phần {offset / (chunkSize - overlap) + 1}" : section.Locator,
                        Content = content,
                        TokenEstimate = Math.Max(1, content.Length / 4),
                        CreatedAt = createdAt
                    });
                }
            }
            return chunks;
        }

        private static IReadOnlyList<ExtractedSection> ExtractSections(string extension, byte[] bytes) => extension switch
        {
            ".pdf" => ExtractPdf(bytes),
            ".docx" => ExtractDocx(bytes),
            ".pptx" => ExtractPptx(bytes),
            ".xlsx" => ExtractXlsx(bytes),
            _ => ExtractPlainText(extension, bytes)
        };

        private static IReadOnlyList<ExtractedSection> ExtractPlainText(string extension, byte[] bytes)
        {
            var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            if (text.IndexOf('\0') >= 0) throw new InvalidDataException("Tệp văn bản chứa dữ liệu nhị phân.");
            if (extension == ".json") JsonDocument.Parse(text).Dispose();
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var sections = new List<ExtractedSection>();
            for (var start = 0; start < lines.Length; start += 100)
            {
                var end = Math.Min(lines.Length, start + 100);
                sections.Add(new ExtractedSection($"dòng {start + 1}–{end}", string.Join('\n', lines[start..end])));
            }
            return sections;
        }

        private static IReadOnlyList<ExtractedSection> ExtractPdf(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);
            using var document = PdfDocument.Open(stream);
            var sections = new List<ExtractedSection>();
            foreach (var page in document.GetPages())
            {
                var text = ContentOrderTextExtractor.GetText(page).Trim();
                if (!string.IsNullOrWhiteSpace(text)) sections.Add(new ExtractedSection($"trang {page.Number}", text));
            }
            return sections;
        }

        private static IReadOnlyList<ExtractedSection> ExtractDocx(byte[] bytes)
        {
            using var archive = OpenZip(bytes);
            var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("DOCX thiếu word/document.xml.");
            var document = XDocument.Load(entry.Open());
            var paragraphs = document.Descendants().Where(node => node.Name.LocalName == "p")
                .Select(node => string.Join(' ', node.Descendants().Where(child => child.Name.LocalName == "t").Select(child => child.Value)).Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            return paragraphs.Select((text, index) => new ExtractedSection($"đoạn {index + 1}", text)).ToList();
        }

        private static IReadOnlyList<ExtractedSection> ExtractPptx(byte[] bytes)
        {
            using var archive = OpenZip(bytes);
            return archive.Entries
                .Where(entry => Regex.IsMatch(entry.FullName, @"^ppt/slides/slide\d+\.xml$", RegexOptions.IgnoreCase))
                .OrderBy(entry => ExtractNumber(entry.FullName))
                .Select(entry =>
                {
                    var document = XDocument.Load(entry.Open());
                    var text = string.Join(' ', document.Descendants().Where(node => node.Name.LocalName == "t").Select(node => node.Value)).Trim();
                    return new ExtractedSection($"slide {ExtractNumber(entry.FullName)}", text);
                })
                .Where(section => !string.IsNullOrWhiteSpace(section.Content))
                .ToList();
        }

        private static IReadOnlyList<ExtractedSection> ExtractXlsx(byte[] bytes)
        {
            using var archive = OpenZip(bytes);
            var sharedStrings = new List<string>();
            var sharedEntry = archive.GetEntry("xl/sharedStrings.xml");
            if (sharedEntry != null)
            {
                var sharedDocument = XDocument.Load(sharedEntry.Open());
                sharedStrings = sharedDocument.Descendants().Where(node => node.Name.LocalName == "si")
                    .Select(node => string.Join(' ', node.Descendants().Where(child => child.Name.LocalName == "t").Select(child => child.Value)))
                    .ToList();
            }

            var sections = new List<ExtractedSection>();
            foreach (var entry in archive.Entries.Where(item => Regex.IsMatch(item.FullName, @"^xl/worksheets/sheet\d+\.xml$", RegexOptions.IgnoreCase)).OrderBy(item => ExtractNumber(item.FullName)))
            {
                var document = XDocument.Load(entry.Open());
                var values = new List<string>();
                foreach (var cell in document.Descendants().Where(node => node.Name.LocalName == "c"))
                {
                    var value = cell.Descendants().FirstOrDefault(node => node.Name.LocalName == "v")?.Value;
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    var type = cell.Attribute("t")?.Value;
                    if (type == "s" && int.TryParse(value, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count) value = sharedStrings[sharedIndex];
                    values.Add($"{cell.Attribute("r")?.Value}: {value}");
                }
                if (values.Count > 0) sections.Add(new ExtractedSection($"sheet {ExtractNumber(entry.FullName)}", string.Join("; ", values)));
            }
            return sections;
        }

        private static ZipArchive OpenZip(byte[] bytes) => new(new MemoryStream(bytes), ZipArchiveMode.Read);

        private static void ValidateContainerSafety(string extension, byte[] bytes)
        {
            if (extension is not (".docx" or ".xlsx" or ".pptx")) return;
            using var archive = OpenZip(bytes);
            foreach (var entry in archive.Entries)
            {
                var normalized = entry.FullName.Replace('\\', '/');
                if (normalized.Contains("../", StringComparison.Ordinal) ||
                    normalized.Contains("vbaProject.bin", StringComparison.OrdinalIgnoreCase) ||
                    normalized.Contains("/embeddings/", StringComparison.OrdinalIgnoreCase) ||
                    normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                    normalized.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Tài liệu chứa nội dung nhúng không an toàn.");
                }
            }
        }

        private static bool HasValidSignature(string extension, byte[] bytes)
        {
            if (bytes.Length < 4) return false;
            if (extension == ".png") return bytes.AsSpan(0, Math.Min(8, bytes.Length)).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            if (extension is ".jpg" or ".jpeg") return bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[^2] == 0xFF && bytes[^1] == 0xD9;
            if (extension == ".webp") return bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP";
            if (extension == ".pdf") return Encoding.ASCII.GetString(bytes, 0, Math.Min(5, bytes.Length)) == "%PDF-";
            if (extension is ".docx" or ".xlsx" or ".pptx") return bytes[0] == 0x50 && bytes[1] == 0x4B;
            try
            {
                _ = new UTF8Encoding(false, true).GetString(bytes);
                return !bytes.Contains((byte)0);
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        private static (int? Width, int? Height) ReadImageDimensions(string extension, byte[] bytes)
        {
            if (extension == ".png" && bytes.Length >= 24)
            {
                return (ReadBigEndianInt(bytes, 16), ReadBigEndianInt(bytes, 20));
            }
            if (extension == ".webp" && bytes.Length >= 30 && Encoding.ASCII.GetString(bytes, 12, 4) == "VP8X")
            {
                var width = 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16);
                var height = 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16);
                return (width, height);
            }
            if (extension is ".jpg" or ".jpeg")
            {
                var index = 2;
                while (index + 8 < bytes.Length)
                {
                    if (bytes[index] != 0xFF) { index++; continue; }
                    var marker = bytes[index + 1];
                    if (marker is >= 0xC0 and <= 0xC3)
                    {
                        return ((bytes[index + 7] << 8) + bytes[index + 8], (bytes[index + 5] << 8) + bytes[index + 6]);
                    }
                    if (index + 3 >= bytes.Length) break;
                    var segmentLength = (bytes[index + 2] << 8) + bytes[index + 3];
                    if (segmentLength < 2) break;
                    index += segmentLength + 2;
                }
            }
            return (null, null);
        }

        private static int ReadBigEndianInt(byte[] bytes, int offset) =>
            (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

        private static int ExtractNumber(string value)
        {
            var match = Regex.Match(value, @"\d+");
            return match.Success && int.TryParse(match.Value, out var number) ? number : 0;
        }

        private static IEnumerable<string> Tokenize(string value) => Normalize(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 2)
            .Distinct(StringComparer.Ordinal);

        private static string Normalize(string value)
        {
            var decomposed = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }
            return Regex.Replace(builder.ToString(), @"[^a-z0-9]+", " ");
        }

        private static string Limit(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

        private sealed record AttachmentRule(string Kind, long MaxBytes, string[] MimeTypes, bool IsSourceCode = false)
        {
            public static AttachmentRule SourceCode { get; } = new("document", DocumentMaxBytes, Array.Empty<string>(), true);
            public bool AcceptsMime(string mimeType) => IsSourceCode
                ? string.IsNullOrWhiteSpace(mimeType) || mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || mimeType is "application/javascript" or "application/json" or "application/xml" or "application/x-sh"
                : MimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase);
        }

        private sealed record ExtractedSection(string Locator, string Content);
    }
}
