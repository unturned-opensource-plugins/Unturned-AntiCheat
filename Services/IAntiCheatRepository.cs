using System.Collections.Generic;
using Emqo.Unturned_AntiCheat.Models;

namespace Emqo.Unturned_AntiCheat.Services
{
    public interface IAntiCheatRepository
    {
        AntiCheatDataStore Load();
        void Save(AntiCheatDataStore dataStore);
        IReadOnlyList<PlayerEvidence> GetRecentEvidence(int count);
        IReadOnlyList<PlayerEvidence> GetEvidenceForPlayer(ulong steamId, int count);
    }
}
