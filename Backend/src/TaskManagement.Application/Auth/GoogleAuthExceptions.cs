namespace TaskManagement.Application.Auth;

public class ExternalAccountConflictException : Exception
{
    public ExternalAccountConflictException(string message = "This external account is already associated with another sign-in method.")
        : base(message)
    {
    }
}

public sealed class GoogleCredentialException : Exception
{
    public GoogleCredentialException(string message = "Google credential is invalid or expired.")
        : base(message)
    {
    }
}

public sealed class GoogleProviderUnavailableException : Exception
{
    public GoogleProviderUnavailableException(string message = "Google authentication is temporarily unavailable.")
        : base(message)
    {
    }
}

public sealed class GoogleAccountForbiddenException : Exception
{
    public GoogleAccountForbiddenException(string message = "This account cannot sign in with Google.")
        : base(message)
    {
    }
}

public sealed class GoogleAccountConflictException : ExternalAccountConflictException
{
    public GoogleAccountConflictException(string message = "This email is already associated with another sign-in method.")
        : base(message)
    {
    }
}

public sealed class GitHubAccountConflictException : ExternalAccountConflictException
{
    public GitHubAccountConflictException()
        : base("This GitHub account is not safely linked to the existing account.")
    {
    }
}

public sealed class GitHubAccountForbiddenException : Exception
{
    public GitHubAccountForbiddenException()
        : base("This account cannot sign in with GitHub.")
    {
    }
}

public sealed class AccountLinkConflictException : Exception
{
    public AccountLinkConflictException(string message = "This external account is already linked to another SprintA account.")
        : base(message)
    {
    }
}

public sealed class LastLoginMethodException : Exception
{
    public LastLoginMethodException(string message = "Không thể ngắt liên kết phương thức đăng nhập cuối cùng.")
        : base(message)
    {
    }
}
