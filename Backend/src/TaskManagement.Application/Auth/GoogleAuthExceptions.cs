namespace TaskManagement.Application.Auth;

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

public sealed class GoogleAccountConflictException : Exception
{
    public GoogleAccountConflictException(string message = "This email is already associated with another sign-in method.")
        : base(message)
    {
    }
}
