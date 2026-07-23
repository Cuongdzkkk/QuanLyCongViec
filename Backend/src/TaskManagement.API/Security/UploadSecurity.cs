namespace TaskManagement.API.Security;

public sealed record ValidatedUpload(string OriginalFileName, string Extension, string MimeType, byte[] Bytes);

public static class UploadSecurity
{
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
}
