namespace TaskManagement.Application.Common
{
    public static class ProjectAccessPolicy
    {
        private static volatile bool _restrictionsEnabled = true;

        public static bool RestrictionsEnabled => _restrictionsEnabled;
        public static bool IsUnrestricted => !_restrictionsEnabled;

        public static void Configure(bool restrictionsEnabled)
        {
            _restrictionsEnabled = restrictionsEnabled;
        }
    }
}
