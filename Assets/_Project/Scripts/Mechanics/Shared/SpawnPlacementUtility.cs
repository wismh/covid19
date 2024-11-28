using System.Collections.Generic;
using UnityEngine;

namespace _Project.Mechanics.Shared
{
    /// <summary>
    /// Unifies the rejection-sampling "place a random point within bounds, respecting a minimum distance from
    /// already-placed points, give up after M attempts" algorithm that TECHNICAL_AUDIT.md section 2.2 calls out
    /// as independently duplicated in two places with two different constants:
    ///
    /// - Assets/GMTK/Scripts/Behaviors/Cell/CellSpawner.cs: `public const int MAX_CHOICES_VALUE = 5000;`,
    ///   `_distanceThreshold` (35 in Assets/GMTK/Scenes/SampleScene.unity's CellSpawner instance), PLUS an
    ///   additional "keep the point out of a circular start zone around the origin" exclusion
    ///   (`_sqrSizeOfStartZone`, 10 in the scene) that BackgroundBacteriaManager doesn't have.
    /// - Assets/GMTK/Scripts/Background/BackgroundBacteriaManager.cs:
    ///   `public const int MAX_CHOICES_VALUE = 500;`, `_distanceThreshold` (default 25, overridden to 80 in the
    ///   scene's BackgroundBacteriaManager instance). No start-zone exclusion.
    ///
    /// Both old algorithms compare `(candidate - existing).sqrMagnitude` DIRECTLY against `_distanceThreshold`
    /// (no squaring at the call site) - despite the plain name, `_distanceThreshold` was already being used as a
    /// squared-distance value in both files (CellSpawner's own tooltip says as much: "Squared distance threshold
    /// between cells"). Parameters here are named accordingly (`minSqrDistanceFromExisting`) to make that
    /// explicit rather than perpetuate the ambiguous old name.
    ///
    /// Both old algorithms also give up the same way: if no far-enough point is found within the attempt budget,
    /// they use whatever the LAST attempted point was rather than failing/throwing (CellSpawner: the loop simply
    /// `break`s and the caller uses `position` as last assigned; BackgroundBacteriaManager: explicitly falls
    /// through to `destinationList.Add(pos)` on the same condition). This method reproduces that exactly - it
    /// always returns a point, never fails.
    ///
    /// One behavioral asymmetry preserved deliberately: CellSpawner's start-zone exclusion check
    /// (`if (position.sqrMagnitude &lt; _sqrSizeOfStartZone) continue;`) does NOT consume an attempt from the
    /// budget in old code (the `continue` skips the `i++` entirely) - only the distance-from-existing-points
    /// check consumes attempts. <see cref="FindPosition"/> reproduces this asymmetry via the optional
    /// <paramref name="minSqrDistanceFromOrigin"/> parameter (0 = disabled, matching BackgroundBacteriaManager
    /// which has no such exclusion at all).
    /// </summary>
    public static class SpawnPlacementUtility
    {
        /// <summary>
        /// Old: CellSpawner.SpawnCell()'s placement loop / BackgroundBacteriaManager.GetRandomDestinationList()'s
        /// inner while(true) loop - unified into one function of explicit parameters instead of two copies with
        /// hardcoded constants (REFACTORING_PLAN.md Phase 3 item 5).
        /// </summary>
        /// <param name="bounds">Area to sample candidate points from (uniform in X/Y, Z always 0).</param>
        /// <param name="existingPositions">Already-placed points to keep distance from.</param>
        /// <param name="minSqrDistanceFromExisting">
        /// Minimum squared distance from every existing position (compared directly against .sqrMagnitude, not
        /// re-squared - see class doc comment). CellSpawner used 35, BackgroundBacteriaManager used 25 by default
        /// (80 in the shipped scene).
        /// </param>
        /// <param name="maxAttempts">
        /// Give up and use the last attempted point after this many rejections. CellSpawner used 5000,
        /// BackgroundBacteriaManager used 500.
        /// </param>
        /// <param name="minSqrDistanceFromOrigin">
        /// Optional exclusion zone around the world origin (CellSpawner's `_sqrSizeOfStartZone`, 10 in the shipped
        /// scene) - retries do NOT count against <paramref name="maxAttempts"/>, matching old CellSpawner exactly.
        /// Defaults to 0 (disabled), matching BackgroundBacteriaManager which has no such exclusion.
        /// </param>
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

                // Old: CellSpawner.SpawnCell() -> `if (position.sqrMagnitude < _sqrSizeOfStartZone) continue;`
                // (no attempt-budget cost - see class doc comment).
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
