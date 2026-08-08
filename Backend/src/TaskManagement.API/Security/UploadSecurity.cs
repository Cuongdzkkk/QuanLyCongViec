namespace TaskManagement.API.Security;

public sealed record ValidatedUpload(string OriginalFileName, string Extension, string MimeType, byte[] Bytes);

public static class UploadSecurity
{
    public const long CollaborationMaxFileBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> CollaborationFiles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp",
            [".pdf"] = "application/pdf",
            [".txt"] = "text/plain",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    private static readonly IReadOnlyDictionary<string, string[]> PublicImages = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = ["image/png"],
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".gif"] = ["image/gif"],
        [".webp"] = ["image/webp"]
    };

    private static readonly IReadOnlyDictionary<string, string[]> PrivateFiles = new Dictionary<string, string[]>(PublicImages, StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["application/pdf"],
        [".txt"] = ["text/plain"],
        [".csv"] = ["text/csv", "application/csv", "application/vnd.ms-excel"],
        [".json"] = ["application/json", "text/json", "text/plain"],
        [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
        [".xlsx"] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
        [".pptx"] = ["application/vnd.openxmlformats-officedocument.presentationml.presentation"]
    };

    public static Task<ValidatedUpload> ReadPublicImageAsync(IFormFile file, CancellationToken cancellationToken = default) =>
        ReadAndValidateAsync(file, PublicImages, 5 * 1024 * 1024, cancellationToken);

    public static Task<ValidatedUpload> ReadPrivateFileAsync(IFormFile file, CancellationToken cancellationToken = default) =>
        ReadAndValidateAsync(file, PrivateFiles, 20 * 1024 * 1024, cancellationToken);

    public static async Task<ValidatedUpload> ReadCollaborationFileAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length <= 0) throw new InvalidDataException("File is empty.");
        if (file.Length > CollaborationMaxFileBytes)
            throw new InvalidDataException("File exceeds the 10MB limit.");

        var original = SanitizeClientFileName(file.FileName);
        var extension = Path.GetExtension(original).ToLowerInvariant();
        if (!CollaborationFiles.TryGetValue(extension, out var verifiedMime))
            throw new InvalidDataException("File type is not allowed.");

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream((int)file.Length);
        await stream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        if (bytes.LongLength != file.Length || !HasCollaborationSignature(extension, bytes))
            throw new InvalidDataException("File content does not match its allowed type.");
        return new ValidatedUpload(original, extension, verifiedMime, bytes);
    }

    public static string ResolveUnderRoot(string root, string storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName) || storedName != Path.GetFileName(storedName))
            throw new InvalidDataException("Invalid storage path.");
        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, storedName));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Storage path escapes its configured root.");
        return fullPath;
    }

    private static async Task<ValidatedUpload> ReadAndValidateAsync(
        IFormFile file,
        IReadOnlyDictionary<string, string[]> rules,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length <= 0) throw new InvalidDataException("File is empty.");
        if (file.Length > maxBytes) throw new InvalidDataException($"File exceeds the {maxBytes / 1024 / 1024}MB limit.");
        var original = Path.GetFileName(file.FileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(original) || original != file.FileName || original.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException("Client filename is invalid.");
        var extension = Path.GetExtension(original).ToLowerInvariant();
        var mime = (file.ContentType ?? string.Empty).Split(';', 2)[0].Trim().ToLowerInvariant();
        if (!rules.TryGetValue(extension, out var acceptedMimes) || !acceptedMimes.Contains(mime, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("File extension and MIME type are not allowed together.");

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream((int)file.Length);
        await stream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        if (bytes.LongLength != file.Length || !HasSignature(extension, bytes))
            throw new InvalidDataException("File signature does not match its extension.");
        return new ValidatedUpload(original, extension, mime, bytes);
    }

    private static bool HasSignature(string extension, byte[] bytes) => extension switch
    {
        ".png" => Starts(bytes, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
        ".jpg" or ".jpeg" => Starts(bytes, 0xFF, 0xD8, 0xFF),
        ".gif" => bytes.Length >= 6 && (System.Text.Encoding.ASCII.GetString(bytes, 0, 6) is "GIF87a" or "GIF89a"),
        ".webp" => bytes.Length >= 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP",
        ".pdf" => Starts(bytes, 0x25, 0x50, 0x44, 0x46, 0x2D),
        ".docx" or ".xlsx" or ".pptx" => Starts(bytes, 0x50, 0x4B, 0x03, 0x04),
        ".txt" or ".csv" or ".json" => !bytes.Take(Math.Min(bytes.Length, 4096)).Any(value => value == 0),
        _ => false
    };

    private static bool Starts(byte[] bytes, params byte[] signature) =>
        bytes.Length >= signature.Length && bytes.AsSpan(0, signature.Length).SequenceEqual(signature);

    private static string SanitizeClientFileName(string? clientName)
    {
        var leaf = Path.GetFileName((clientName ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)).Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(leaf
            .Select(value => char.IsControl(value) || invalid.Contains(value) ? '_' : value)
            .ToArray())
            .Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(sanitized))
            throw new InvalidDataException("Client filename is invalid.");
        if (sanitized.Length <= 255) return sanitized;
        var extension = Path.GetExtension(sanitized);
        return sanitized[..(255 - extension.Length)] + extension;
    }

    private static bool HasCollaborationSignature(string extension, byte[] bytes)
    {
        if (extension == ".docx") return IsExpectedOfficeArchive(bytes, "word/document.xml");
        if (extension == ".xlsx") return IsExpectedOfficeArchive(bytes, "xl/workbook.xml");
        return extension switch
        {
            ".png" => Starts(bytes, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
            ".jpg" or ".jpeg" => Starts(bytes, 0xFF, 0xD8, 0xFF),
            ".webp" => bytes.Length >= 12 &&
                System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" &&
                System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP",
            ".pdf" => Starts(bytes, 0x25, 0x50, 0x44, 0x46, 0x2D),
            ".txt" => IsSafeText(bytes),
            _ => false
        };
    }

    private static bool IsSafeText(byte[] bytes)
    {
        if (bytes.Take(Math.Min(bytes.Length, 4096)).Any(value => value == 0)) return false;
        try
        {
            _ = new System.Text.UTF8Encoding(false, true).GetString(bytes);
            return true;
        }
        catch (System.Text.DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool IsExpectedOfficeArchive(byte[] bytes, string requiredEntry)
    {
        if (!Starts(bytes, 0x50, 0x4B, 0x03, 0x04)) return false;
        try
        {
            using var memory = new MemoryStream(bytes, writable: false);
            using var archive = new System.IO.Compression.ZipArchive(
                memory,
                System.IO.Compression.ZipArchiveMode.Read,
                leaveOpen: false);
            long expandedBytes = 0;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                var normalized = entry.FullName.Replace('\\', '/');
                if (normalized.StartsWith('/') || normalized.Contains("../", StringComparison.Ordinal)) return false;
                expandedBytes += entry.Length;
                if (expandedBytes > 30 * 1024 * 1024) return false;
                names.Add(normalized);
            }
            return names.Contains("[Content_Types].xml") && names.Contains(requiredEntry);
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
