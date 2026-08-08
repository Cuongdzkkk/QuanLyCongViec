using TaskManagement.Domain.Entities;

namespace TaskManagement.Domain.Rules
{
    public static class SprintStatePolicy
    {
        public static string ResolveState(Sprint sprint) =>
            sprint.Status && sprint.State == SprintStates.Planned
                ? SprintStates.Active
                : sprint.State;

        public static bool IsTaskMutationLocked(Sprint? sprint, DateTime utcNow)
        {
            if (sprint == null)
            {
                return false;
            }

            return ResolveState(sprint) != SprintStates.Active ||
                !sprint.Status ||
                sprint.EndDate.Date < utcNow.Date;
        }
    }
}
