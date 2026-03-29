using System.IO;
using Emqo.Unturned_AntiCheat.Services;
using Rocket.Core.Plugins;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System.Linq;
using Steamworks;
using UnityEngine;

namespace Emqo.Unturned_AntiCheat
{
    public class Unturned_AntiCheatPlugin : RocketPlugin<Unturned_AntiCheatConfiguration>
    {
        public static Unturned_AntiCheatPlugin Instance { get; private set; }
        public AntiCheatService AntiCheatService { get; private set; }

        protected override void Load()
        {
            Instance = this;
            Configuration.Instance.ApplyDefaultsIfNeeded();
            Configuration.Save();

            var storagePath = Path.Combine(Directory, Configuration.Instance.General.StorageFileName);
            AntiCheatService = new AntiCheatService(
                new JsonAntiCheatRepository(storagePath),
                Configuration.Instance);

            Provider.onServerConnected += OnServerConnected;
            Provider.onServerDisconnected += OnServerDisconnected;
            UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;
            UnturnedPlayerEvents.OnPlayerUpdatePosition += OnPlayerUpdatePosition;
            UnturnedPlayerEvents.OnPlayerChatted += OnPlayerChatted;
            UnturnedEvents.OnPlayerDamaged += OnPlayerDamaged;

            foreach (var steamPlayer in Provider.clients.ToArray())
            {
                var player = UnturnedPlayer.FromSteamPlayer(steamPlayer);
                AntiCheatService?.RegisterConnected(player);
            }

            Rocket.Core.Logging.Logger.Log($"{Name} {Assembly.GetName().Version.ToString(3)} has been loaded!");
        }

        protected override void Unload()
        {
            Provider.onServerConnected -= OnServerConnected;
            Provider.onServerDisconnected -= OnServerDisconnected;
            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            UnturnedPlayerEvents.OnPlayerUpdatePosition -= OnPlayerUpdatePosition;
            UnturnedPlayerEvents.OnPlayerChatted -= OnPlayerChatted;
            UnturnedEvents.OnPlayerDamaged -= OnPlayerDamaged;

            AntiCheatService?.Save();
            AntiCheatService = null;
            Instance = null;

            Rocket.Core.Logging.Logger.Log($"{Name} has been unloaded!");
        }

        public void ReloadRuntimeConfiguration()
        {
            Configuration.Load();
            Configuration.Instance.ApplyDefaultsIfNeeded();
            Configuration.Save();
            AntiCheatService?.ReloadConfiguration(Configuration.Instance);
            Rocket.Core.Logging.Logger.Log($"[AC] {Name} runtime configuration reloaded.");
        }

        private void OnPlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, CSteamID murderer)
        {
            AntiCheatService?.RegisterKill(player, murderer, limb);
        }

        private void OnPlayerUpdatePosition(UnturnedPlayer player, Vector3 position)
        {
            AntiCheatService?.RegisterPosition(player, position);
        }

        private void OnServerConnected(CSteamID steamId)
        {
            if (steamId == CSteamID.Nil)
            {
                return;
            }

            var player = UnturnedPlayer.FromCSteamID(steamId);
            AntiCheatService?.RegisterConnected(player);
        }

        private void OnServerDisconnected(CSteamID steamId)
        {
            if (steamId == CSteamID.Nil)
            {
                return;
            }

            AntiCheatService?.RegisterDisconnected(steamId.m_SteamID);
        }

        private void OnPlayerDamaged(
            UnturnedPlayer player,
            ref EDeathCause cause,
            ref ELimb limb,
            ref UnturnedPlayer killer,
            ref Vector3 direction,
            ref float damage,
            ref float times,
            ref bool canDamage)
        {
            if (!canDamage || killer == null)
            {
                return;
            }

            AntiCheatService?.RegisterDamage(
                player.Player,
                killer.CSteamID,
                cause,
                limb,
                damage,
                times);
        }

        private void OnPlayerChatted(UnturnedPlayer player, ref Color color, string message, EChatMode chatMode, ref bool cancel)
        {
            AntiCheatService?.RegisterChat(player, message);
        }
    }
}
