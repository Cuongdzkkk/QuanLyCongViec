namespace TaskManagement.Application.Common
{
    public sealed class SprintTransitionException : InvalidOperationException
    {
        public SprintTransitionException(string code, string message)
            : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }
}
