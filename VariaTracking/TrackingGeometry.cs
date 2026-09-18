using UnityEngine;

namespace VariaTracking
{
    internal static class TrackingGeometry
    {
        // Forward is normalized once per frame. This works for cones wider than 180 degrees too.
        public static bool IsInsideCone(Vector3 delta, Vector3 forward, float cosHalfAngle)
        {
            delta.y = 0f;
            float lengthSqr = delta.sqrMagnitude;
            return lengthSqr < 0.0001f
                || Vector3.Dot(forward, delta) >= cosHalfAngle * Mathf.Sqrt(lengthSqr);
        }

        public static Vector3 JitterPosition(Vector3 position, Vector3 playerPosition,
            Vector2 direction, float jitterMeters, float radius)
        {
            Vector3 jittered = position + new Vector3(direction.x * jitterMeters, 0f, direction.y * jitterMeters);
            Vector3 delta = jittered - playerPosition;
            // Match the game's spherical range query, including elevation.
            float horizontalRadius = Mathf.Sqrt(Mathf.Max(0f, radius * radius - delta.y * delta.y));
            delta.y = 0f;
            float length = delta.magnitude;
            if (length > horizontalRadius && length > 0f)
            {
                delta *= horizontalRadius / length;
                jittered.x = playerPosition.x + delta.x;
                jittered.z = playerPosition.z + delta.z;
            }
            return jittered;
        }
    }
}
