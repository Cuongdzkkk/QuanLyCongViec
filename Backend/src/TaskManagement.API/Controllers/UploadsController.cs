using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.API.Security;

namespace TaskManagement.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UploadsController : ControllerBase
{
    private readonly string _storageRoot;
    private readonly IDataProtector _protector;

    public UploadsController(IWebHostEnvironment environment, IDataProtectionProvider dataProtection)
    {
        _storageRoot = Path.Combine(environment.ContentRootPath, "private-uploads", "general");
        _protector = dataProtection.CreateProtector("SprintA.PrivateUpload.v1");
    }

    [HttpPost("image")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken cancellationToken) =>
        await StoreAsync(file, imageOnly: true, cancellationToken);

    [HttpPost("file")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(IFormFile file, CancellationToken cancellationToken) =>
        await StoreAsync(file, imageOnly: false, cancellationToken);

    [HttpGet("content/{token}")]
    public IActionResult Download(string token)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var values = _protector.Unprotect(token).Split('|', 4);
            if (values.Length != 4 || !Guid.TryParseExact(values[0], "N", out var ownerId) || ownerId != userId)
                return Forbid();
            var storedName = values[1];
            var originalName = Encoding.UTF8.GetString(Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(values[2]));
            var path = UploadSecurity.ResolveUnderRoot(_storageRoot, storedName);
            if (!System.IO.File.Exists(path)) return NotFound();
            Response.Headers.CacheControl = "private, no-store";
            return PhysicalFile(path, values[3], originalName, enableRangeProcessing: true);
        }
        catch
        {
            return NotFound();
        }
    }

    private async Task<IActionResult> StoreAsync(IFormFile file, bool imageOnly, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var upload = imageOnly
                ? await UploadSecurity.ReadPublicImageAsync(file, cancellationToken)
                : await UploadSecurity.ReadPrivateFileAsync(file, cancellationToken);
            Directory.CreateDirectory(_storageRoot);
            var storedName = $"{Guid.NewGuid():N}{upload.Extension}";
            var path = UploadSecurity.ResolveUnderRoot(_storageRoot, storedName);
            await System.IO.File.WriteAllBytesAsync(path, upload.Bytes, cancellationToken);
            var encodedName = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(upload.OriginalFileName));
            var token = _protector.Protect($"{userId:N}|{storedName}|{encodedName}|{upload.MimeType}");
            return Ok(new
            {
                url = $"/api/uploads/content/{token}",
                name = upload.OriginalFileName,
                size = upload.Bytes.LongLength,
                visibility = "private-owner"
            });
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
