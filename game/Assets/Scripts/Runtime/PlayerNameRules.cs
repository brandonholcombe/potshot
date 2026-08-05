using System.Linq;

namespace Potshot
{
    /// <summary>One sanitizer for client AND server (never duplicate —
    /// UI review). Trim, printable ASCII-ish only, max 16 chars.</summary>
    public static class PlayerNameRules
    {
        public const int MaxLength = 16;

        public static string Sanitize(string raw, int clientIdFallback)
        {
            string cleaned = new string((raw ?? string.Empty)
                .Where(c => !char.IsControl(c)).ToArray()).Trim();
            if (cleaned.Length > MaxLength) cleaned = cleaned.Substring(0, MaxLength);
            return cleaned.Length == 0 ? $"Tanker{clientIdFallback}" : cleaned;
        }
    }
}
