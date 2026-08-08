namespace TaskManagement.Domain.Rules
{
    public static class SprintStates
    {
        public const string Planned = "Planned";
        public const string Active = "Active";
        public const string Completed = "Completed";

        public static bool IsKnown(string? state) =>
            state is Planned or Active or Completed;
    }
}
