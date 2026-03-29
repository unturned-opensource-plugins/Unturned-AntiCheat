using System;

namespace Emqo.Unturned_AntiCheat.Models
{
    public class PlayerProfile
    {
        public ulong SteamId { get; set; }
        public string LastKnownName { get; set; }
        public double Score { get; set; }
        public DateTime LastScoreUpdateUtc { get; set; }
        public DateTime? LastPenaltyUtc { get; set; }
        public int TotalViolations { get; set; }
        public int KickCount { get; set; }
        public int BanCount { get; set; }
    }
}
