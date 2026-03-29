using System;
using System.Collections.Generic;

namespace Emqo.Unturned_AntiCheat.Models
{
    public class PlayerEvidence
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public ulong SteamId { get; set; }
        public string PlayerName { get; set; }
        public string DetectorId { get; set; }
        public string Category { get; set; }
        public string Summary { get; set; }
        public double Score { get; set; }
        public DateTime TimestampUtc { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
