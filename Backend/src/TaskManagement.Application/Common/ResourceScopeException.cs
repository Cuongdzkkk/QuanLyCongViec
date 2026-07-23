namespace TaskManagement.Application.Common
{
    public sealed class ResourceScopeException : ArgumentException
    {
        public ResourceScopeException(string message) : base(message)
        {
        }
    }
}
