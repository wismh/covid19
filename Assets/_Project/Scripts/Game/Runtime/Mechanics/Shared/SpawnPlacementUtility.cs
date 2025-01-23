using System.Collections.Generic;
using UnityEngine;

namespace _Project.Mechanics.Shared
{
    /// <summary>Rejection-sampling placement: picks a random point within bounds that's far enough from
    /// already-placed points, giving up after a fixed attempt budget and returning the last-tried point rather
    /// than failing.</summary>
    public static class SpawnPlacementUtility
    {
        /// <param name="bounds">Area to sample candidate points from (uniform in X/Y, Z always 0).</param>
        /// <param name="existingPositions">Already-placed points to keep distance from.</param>
        /// <param name="minSqrDistanceFromExisting">Minimum squared distance from every existing position
        /// (compared directly against .sqrMagnitude).</param>
        /// <param name="maxAttempts">Give up and use the last attempted point after this many rejections.</param>
        /// <param name="minSqrDistanceFromOrigin">Optional exclusion zone around the world origin; retries here do
        /// NOT count against <paramref name="maxAttempts"/>. 0 disables the check.</param>
        public static Vector3 FindPosition(
            Bounds bounds,
            IReadOnlyList<Vector3> existingPositions,
            float minSqrDistanceFromExisting,
            int maxAttempts,
            float minSqrDistanceFromOrigin = 0f)
        {
            Vector3 position;
            int attempts = 0;

            while (true)
            {
                position = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    Random.Range(bounds.min.y, bounds.max.y),
                    0f
                );

                if (position.sqrMagnitude < minSqrDistanceFromOrigin)
                    continue;

                bool isFarEnough = true;

                if (existingPositions != null)
                {
                    for (int i = 0; i < existingPositions.Count; i++)
                    {
                        if ((position - existingPositions[i]).sqrMagnitude <= minSqrDistanceFromExisting)
                        {
                            isFarEnough = false;
                            break;
                        }
                    }
                }

                attempts++;

                if (attempts >= maxAttempts || isFarEnough)
                    break;
            }

            return position;
        }
    }
}
