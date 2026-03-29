using System.Collections.Generic;

namespace Emqo.Unturned_AntiCheat.Models
{
    public class AntiCheatDataStore
    {
        public Dictionary<ulong, PlayerProfile> Players { get; set; } = new Dictionary<ulong, PlayerProfile>();
        public List<PlayerEvidence> Evidence { get; set; } = new List<PlayerEvidence>();
    }
}
