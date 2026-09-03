using System.Security.Cryptography;

namespace TaskManagement.Application.Diagnostics;

public static class CaptionTransportDiagnostics
{
    public const string HashAlgorithm = "SHA-256";

    public static bool IsSampledChunk(long chunkIndex) =>
        chunkIndex == 1 || chunkIndex > 0 && chunkIndex % 20 == 0;

    public static string ComputeSha256Hex(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

}
