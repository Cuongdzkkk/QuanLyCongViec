using TaskManagement.Domain.Entities;

namespace TaskManagement.Domain.Rules
{
    public static class SprintStatePolicy
    {
        public static bool IsTaskMutationLocked(Sprint? sprint, DateTime utcNow)
        {
            if (sprint == null)
            {
                return false;
            }

            return !sprint.Status || sprint.EndDate.Date < utcNow.Date;
        }
    }
}
