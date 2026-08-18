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

            if (sprint.IsDeleted || sprint.EndDate.Date < utcNow.Date)
            {
                return true;
            }

            var state = ResolveState(sprint);
            return state == SprintStates.Completed ||
                (state == SprintStates.Active && !sprint.Status);
        }
    }
}
