using System.Collections.Generic;
using System.Linq;

namespace Potshot
{
    /// <summary>Pure lobby rules — leader selection and setting clamps
    /// (EditMode-tested; the networked LobbyState just applies them).</summary>
    public static class LobbyRules
    {
        public const int MinBots = 0, MaxBots = 6;
        public const int MinKillTarget = 5, MaxKillTarget = 25;

        /// <summary>Leader = lowest ClientId present; -1 when empty.</summary>
        public static int PickLeader(IEnumerable<int> clientIds)
        {
            int leader = -1;
            foreach (int id in clientIds)
                if (leader < 0 || id < leader) leader = id;
            return leader;
        }

        public static int ClampBots(int v) =>
            v < MinBots ? MinBots : v > MaxBots ? MaxBots : v;

        public static int ClampKillTarget(int v) =>
            v < MinKillTarget ? MinKillTarget : v > MaxKillTarget ? MaxKillTarget : v;
    }
}
