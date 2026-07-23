namespace TaskManagement.Application.Common
{
    public static class EmailCanonicalizer
    {
        public static string Normalize(string? email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
