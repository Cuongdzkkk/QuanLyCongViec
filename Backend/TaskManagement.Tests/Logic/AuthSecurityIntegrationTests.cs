using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.Configuration;
using TaskManagement.Application.DTOs.Auth;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class AuthSecurityIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly OtpService _otpService;
    private readonly Mock<IEmailService> _emailService = new();

    public AuthSecurityIntegrationTests()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(dbOptions);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _otpService = new OtpService(_cache, Options.Create(new OtpSecurityOptions()));
        _emailService.Setup(service => service.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task SendOtp_ExistingAndMissingEmail_ReturnSamePublicResponse()
    {
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com",
            FullName = "Existing User",
            PasswordHash = "unused",
            IsActive = true,
            IsDeleted = false
        });
        await _context.SaveChangesAsync();
        var controller = CreateController();

        var existing = await controller.SendOtp(new SendOtpRequestDto
        {
            Email = " EXISTING@example.com ",
            Purpose = "forgot-password"
        });
        var missing = await controller.SendOtp(new SendOtpRequestDto
        {
            Email = "missing@example.com",
            Purpose = "forgot-password"
        });

        existing.Should().BeOfType<OkObjectResult>();
        missing.Should().BeOfType<OkObjectResult>();
        JsonSerializer.Serialize(((OkObjectResult)existing).Value)
            .Should().Be(JsonSerializer.Serialize(((OkObjectResult)missing).Value));
        _emailService.Verify(service => service.SendOtpEmailAsync("existing@example.com", It.IsAny<string>()), Times.Once);
        _emailService.Verify(service => service.SendOtpEmailAsync("missing@example.com", It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendOtp_ResendWithinCooldown_Returns429()
    {
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "cooldown-api@example.com",
            FullName = "Cooldown User",
            PasswordHash = "unused",
            IsActive = true,
            IsDeleted = false
        });
        await _context.SaveChangesAsync();
        var controller = CreateController();
        var request = new SendOtpRequestDto { Email = "cooldown-api@example.com", Purpose = "reset" };

        (await controller.SendOtp(request)).Should().BeOfType<OkObjectResult>();
        var resend = await controller.SendOtp(request);

        resend.Should().BeOfType<ObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public void VerifyOtp_FifthWrongAttempt_Returns429()
    {
        _otpService.StoreOtp("locked-api@example.com", "123456", "127.0.0.1").Issued.Should().BeTrue();
        var controller = CreateController();
        IActionResult? result = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            result = controller.VerifyOtp(new VerifyOtpRequestDto
            {
                Email = "locked-api@example.com",
                OtpCode = "000000"
            });
        }

        result.Should().BeOfType<ObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public void VerifiedOtp_IsExchangedForOpaqueSingleUseToken()
    {
        _otpService.StoreOtp("token@example.com", "123456").Issued.Should().BeTrue();
        _otpService.ValidateOtp("token@example.com", "123456").IsValid.Should().BeTrue();

        var token = _otpService.IssueVerificationToken("token@example.com");

        token.Should().NotMatchRegex("^[0-9]{6}$");
        _otpService.ValidateOtp("token@example.com", token).IsValid.Should().BeTrue();
        _otpService.ValidateOtp("token@example.com", token).IsValid.Should().BeFalse();
    }

    private AuthController CreateController()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        return new AuthController(
            Mock.Of<IAuthService>(),
            _otpService,
            _emailService.Object,
            _context)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    public void Dispose()
    {
        _context.Dispose();
        _cache.Dispose();
    }
}
