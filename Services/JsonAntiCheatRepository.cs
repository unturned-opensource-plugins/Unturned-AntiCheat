using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emqo.Unturned_AntiCheat.Models;
using Newtonsoft.Json;

namespace Emqo.Unturned_AntiCheat.Services
{
    public class JsonAntiCheatRepository : IAntiCheatRepository
    {
        private readonly object _syncRoot = new object();
        private readonly string _storagePath;

        public JsonAntiCheatRepository(string storagePath)
        {
            _storagePath = storagePath;
        }

        public AntiCheatDataStore Load()
        {
            lock (_syncRoot)
            {
                if (!File.Exists(_storagePath))
                {
                    return new AntiCheatDataStore();
                }

                var json = File.ReadAllText(_storagePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new AntiCheatDataStore();
                }

                return JsonConvert.DeserializeObject<AntiCheatDataStore>(json) ?? new AntiCheatDataStore();
            }
        }

        public void Save(AntiCheatDataStore dataStore)
        {
            lock (_syncRoot)
            {
                var directoryPath = Path.GetDirectoryName(_storagePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var json = JsonConvert.SerializeObject(dataStore, Formatting.Indented);
                var tempPath = _storagePath + ".tmp";
                File.WriteAllText(tempPath, json);

                if (File.Exists(_storagePath))
                {
                    File.Delete(_storagePath);
                }

                File.Move(tempPath, _storagePath);
            }
        }

        public IReadOnlyList<PlayerEvidence> GetRecentEvidence(int count)
        {
            return Load()
                .Evidence
                .OrderByDescending(x => x.TimestampUtc)
                .Take(count)
                .ToList();
        }

        public IReadOnlyList<PlayerEvidence> GetEvidenceForPlayer(ulong steamId, int count)
        {
            return Load()
                .Evidence
                .Where(x => x.SteamId == steamId)
                .OrderByDescending(x => x.TimestampUtc)
                .Take(count)
                .ToList();
        }
    }
}
