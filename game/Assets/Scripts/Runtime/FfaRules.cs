using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Potshot
{
    /// <summary>Rules shared by the offline FfaGameMode and the networked
    /// NetFfaState (M2c review: extract only these — the lifecycles differ
    /// too much to share the component).</summary>
    public static class FfaRules
    {
        /// <summary>+1 for a kill, -1 for killing yourself (potshot honesty).</summary>
        public static int ScoreDelta(bool selfKill) => selfKill ? -1 : 1;

        /// <summary>Spawn point farthest from any living opponent.</summary>
        public static Vector3 PickSpawn(
            IReadOnlyList<Vector3> points,
            IReadOnlyCollection<Vector3> livingPositions,
            Vector3 fallback)
        {
            if (points == null || points.Count == 0) return fallback;
            if (livingPositions == null || livingPositions.Count == 0) return points[0];

            Vector3 best = points[0];
            float bestScore = float.MinValue;
            foreach (var p in points)
            {
                float nearest = livingPositions.Min(l => (l - p).sqrMagnitude);
                if (nearest > bestScore) { bestScore = nearest; best = p; }
            }
            return best;
        }
    }
}
