using TaskManagement.API.Security;
using TaskManagement.Application.DTOs.Collaboration;

namespace TaskManagement.API.Services;

public interface ICollaborationAttachmentStorage
{
    Task<IReadOnlyList<PendingCollaborationAttachmentDto>> StoreAsync(
        IReadOnlyList<ValidatedUpload> uploads,
        CancellationToken cancellationToken = default);
    void Delete(IEnumerable<PendingCollaborationAttachmentDto> attachments);
    string ResolvePath(string storageKey);
}

public sealed class CollaborationAttachmentStorage : ICollaborationAttachmentStorage
{
    private readonly string _root;

    public CollaborationAttachmentStorage(IWebHostEnvironment environment)
    {
        _root = Path.Combine(environment.ContentRootPath, "private-uploads", "collaboration");
    }

    public async Task<IReadOnlyList<PendingCollaborationAttachmentDto>> StoreAsync(
        IReadOnlyList<ValidatedUpload> uploads,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uploads);
        Directory.CreateDirectory(_root);
        var stored = new List<PendingCollaborationAttachmentDto>(uploads.Count);
        try
        {
            foreach (var upload in uploads)
            {
                var attachmentId = Guid.NewGuid();
                var storageKey = $"{Guid.NewGuid():N}{upload.Extension}";
                var finalPath = ResolvePath(storageKey);
                var temporaryPath = finalPath + ".tmp";
                try
                {
                    await File.WriteAllBytesAsync(temporaryPath, upload.Bytes, cancellationToken);
                    File.Move(temporaryPath, finalPath);
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
                stored.Add(new PendingCollaborationAttachmentDto(
                    attachmentId,
                    storageKey,
                    upload.OriginalFileName,
                    upload.MimeType,
                    upload.Bytes.LongLength));
            }
            return stored;
        }
        catch
        {
            Delete(stored);
            throw;
        }
    }

    public void Delete(IEnumerable<PendingCollaborationAttachmentDto> attachments)
    {
        foreach (var attachment in attachments)
        {
            try
            {
                var path = ResolvePath(attachment.StorageKey);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (InvalidDataException) { }
        }
    }

    public string ResolvePath(string storageKey) => UploadSecurity.ResolveUnderRoot(_root, storageKey);
}
