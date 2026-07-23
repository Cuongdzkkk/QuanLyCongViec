namespace TaskManagement.API.Services;

public sealed class PrivateUploadCleanupService : BackgroundService
{
    private readonly string _generalUploadRoot;
    private readonly TimeSpan _retention;
    private readonly ILogger<PrivateUploadCleanupService> _logger;

    public PrivateUploadCleanupService(IWebHostEnvironment environment, IConfiguration configuration, ILogger<PrivateUploadCleanupService> logger)
    {
        _generalUploadRoot = Path.Combine(environment.ContentRootPath, "private-uploads", "general");
        _retention = TimeSpan.FromDays(Math.Clamp(configuration.GetValue("Uploads:PrivateRetentionDays", 30), 1, 365));
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CleanupAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(stoppingToken)) await CleanupAsync(stoppingToken);
    }

    internal Task CleanupAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_generalUploadRoot)) return Task.CompletedTask;
        var cutoff = DateTime.UtcNow - _retention;
        foreach (var file in Directory.EnumerateFiles(_generalUploadRoot, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not remove expired private upload {FileName}.", Path.GetFileName(file));
            }
        }
        return Task.CompletedTask;
    }
}
