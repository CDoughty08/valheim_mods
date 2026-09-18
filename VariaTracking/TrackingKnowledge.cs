namespace VariaTracking
{
    /// <summary>
    /// Monotonic revision bumped when name knowledge or unlock-relevant skill changes.
    /// Invalidates cached display names on dots.
    /// </summary>
    internal static class TrackingKnowledge
    {
        private static int _revision = 1;
        private static float _lastSkillFloor = -1f;

        public static int Revision => _revision;

        public static void Bump()
        {
            unchecked
            {
                _revision++;
            }

            if (_revision == 0)
            {
                _revision = 1;
            }
        }

        /// <summary>Call each tick with current Tracking skill level; bumps when floored level changes.</summary>
        public static void NotifySkillLevel(float skillLevel)
        {
            float floor = (int)skillLevel;
            if (floor != _lastSkillFloor)
            {
                _lastSkillFloor = floor;
                Bump();
            }
        }

        public static void ResetSession()
        {
            _lastSkillFloor = -1f;
            Bump();
        }
    }
}
