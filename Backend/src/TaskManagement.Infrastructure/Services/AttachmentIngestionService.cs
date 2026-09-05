using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Infrastructure.Services;

public sealed class AttachmentIngestionService : IAttachmentIngestionService
{
    private const long ImageMaxBytes = 5 * 1024 * 1024;
    private const long DocumentMaxBytes = 10 * 1024 * 1024;
    private const int MaxTextCharacters = 100_000;

    private static readonly IReadOnlyDictionary<string, (string MimeType, long MaxBytes)> Supported =
        new Dictionary<string, (string, long)>(StringComparer.OrdinalIgnoreCase)
        {
            [".txt"] = ("text/plain", DocumentMaxBytes),
            [".csv"] = ("text/csv", DocumentMaxBytes),
            [".pdf"] = ("application/pdf", DocumentMaxBytes),
            [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", DocumentMaxBytes),
            [".png"] = ("image/png", ImageMaxBytes),
            [".jpg"] = ("image/jpeg", ImageMaxBytes),
            [".jpeg"] = ("image/jpeg", ImageMaxBytes),
            [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", DocumentMaxBytes)
        };

    public async Task<NormalizedAttachment> NormalizeAsync(
        string fileName,
        string mimeType,
        Stream content,
        long fileSize,
        string source,
        CancellationToken cancellationToken = default)
    {
        var safeFileName = Path.GetFileName(fileName ?? string.Empty).Trim();
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(safeFileName) || !Supported.ContainsKey(extension))
        {
            throw new InvalidDataException(
                "Định dạng attachment không được hỗ trợ. Hỗ trợ: TXT, CSV, PDF, DOCX, PNG, JPEG" +
                (Supported.ContainsKey(".xlsx") ? ", XLSX." : "."));
        }

        var normalizedMimeType = (mimeType ?? string.Empty).Split(';', 2)[0].Trim().ToLowerInvariant();
        if (!AcceptsMime(extension, normalizedMimeType))
        {
            throw new InvalidDataException($"Loại nội dung '{normalizedMimeType}' không khớp với {extension}.");
        }

        var rule = Supported[extension];
        if (fileSize <= 0 || fileSize > rule.MaxBytes)
        {
            throw new InvalidDataException($"Dung lượng file phải từ 1 byte đến {rule.MaxBytes / 1024 / 1024}MB.");
        }

        await using var memory = new MemoryStream((int)fileSize);
        await content.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        if (bytes.LongLength != fileSize || !HasValidSignature(extension, bytes))
        {
            throw new InvalidDataException("Nội dung file không hợp lệ hoặc không khớp định dạng.");
        }

        if (extension is ".png" or ".jpg" or ".jpeg")
        {
            return new NormalizedAttachment
            {
                FileName = safeFileName,
                MimeType = normalizedMimeType,
                Source = source,
                ExtractionMethod = "binary-image-for-vision",
                StructuredContent = new Dictionary<string, object?>
                {
                    ["kind"] = "image",
                    ["visionInput"] = true
                }
            };
        }

        var text = extension switch
        {
            ".pdf" => ExtractPdf(bytes),
            ".docx" => ExtractDocx(bytes),
            ".xlsx" => ExtractXlsx(bytes),
            _ => ExtractText(bytes)
        };
        text = LimitText(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidDataException("Không trích xuất được nội dung văn bản từ tài liệu.");
        }

        return new NormalizedAttachment
        {
            FileName = safeFileName,
            MimeType = normalizedMimeType,
            TextContent = text,
            Source = source,
            ExtractionMethod = extension switch
            {
                ".pdf" => "pdf-text",
                ".docx" => "docx-document-xml",
                ".xlsx" => "xlsx-shared-strings",
                ".csv" => "csv-utf8",
                _ => "utf8-text"
            },
            StructuredContent = new Dictionary<string, object?>
            {
                ["kind"] = "document",
                ["characterCount"] = text.Length,
                ["lineCount"] = text.Count(value => value == '\n') + 1
            }
        };
    }

    public static bool IsSupported(string fileName, string? mimeType = null)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName ?? string.Empty));
        if (!Supported.TryGetValue(extension, out _)) return false;
        return string.IsNullOrWhiteSpace(mimeType) || AcceptsMime(extension, mimeType.Split(';', 2)[0].Trim().ToLowerInvariant());
    }

    private static bool AcceptsMime(string extension, string mimeType) => extension switch
    {
        ".txt" => mimeType is "text/plain" or "text/markdown",
        ".csv" => mimeType is "text/csv" or "application/csv" or "application/vnd.ms-excel" or "text/tab-separated-values",
        ".pdf" => mimeType == "application/pdf",
        ".docx" => mimeType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".png" => mimeType == "image/png",
        ".jpg" or ".jpeg" => mimeType == "image/jpeg",
        ".xlsx" => mimeType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => false
    };

    private static string ExtractText(byte[] bytes)
    {
        var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        if (text.IndexOf('\0') >= 0) throw new InvalidDataException("Tệp văn bản chứa dữ liệu nhị phân.");
        return text;
    }

    private static string ExtractPdf(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var document = PdfDocument.Open(stream);
        return string.Join(Environment.NewLine, document.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)));
    }

    private static string ExtractDocx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry("word/document.xml")
            ?? throw new InvalidDataException("DOCX không chứa word/document.xml hợp lệ.");
        using var entryStream = entry.Open();
        var document = XDocument.Load(entryStream);
        return string.Join(" ", document.Descendants().Where(node => node.Name.LocalName == "t").Select(node => node.Value));
    }

    private static string ExtractXlsx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var sharedStrings = archive.GetEntry("xl/sharedStrings.xml");
        var values = new List<string>();
        if (sharedStrings != null)
        {
            using var input = sharedStrings.Open();
            var document = XDocument.Load(input);
            values.AddRange(document.Descendants().Where(node => node.Name.LocalName == "t").Select(node => node.Value));
        }
        return string.Join(" ", values);
    }

    private static string LimitText(string text)
        => text.Length <= MaxTextCharacters ? text.Trim() : text[..MaxTextCharacters].Trim();

    private static bool HasValidSignature(string extension, byte[] bytes)
    {
        if (extension == ".png") return bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        if (extension is ".jpg" or ".jpeg") return bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[^2] == 0xFF && bytes[^1] == 0xD9;
        if (extension == ".pdf") return bytes.Length >= 5 && Encoding.ASCII.GetString(bytes, 0, 5) == "%PDF-";
        if (extension is ".docx" or ".xlsx") return bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04;
        return true;
    }
}
