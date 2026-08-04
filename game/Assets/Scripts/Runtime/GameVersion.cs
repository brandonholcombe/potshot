namespace Potshot
{
    /// <summary>
    /// Single source of truth for the build version. The M2 connection
    /// handshake rejects clients whose Version differs from the server's.
    /// Format: semver, optional "+suffix" (e.g. "0.1.0+dev").
    /// </summary>
    public static class GameVersion
    {
        public const string Version = "0.1.0+dev";
    }
}
