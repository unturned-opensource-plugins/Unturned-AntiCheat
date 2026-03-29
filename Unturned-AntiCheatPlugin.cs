using System.IO;
using Emqo.Unturned_AntiCheat.Services;
using Rocket.Core.Plugins;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using SDG.Unturned;
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

            var storagePath = Path.Combine(Directory, Configuration.Instance.General.StorageFileName);
            AntiCheatService = new AntiCheatService(
                new JsonAntiCheatRepository(storagePath),
                Configuration.Instance);

            DamageTool.damagePlayerRequested += OnDamagePlayerRequested;
            UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;
            UnturnedPlayerEvents.OnPlayerUpdatePosition += OnPlayerUpdatePosition;
            UnturnedPlayerEvents.OnPlayerChatted += OnPlayerChatted;

            Rocket.Core.Logging.Logger.Log($"{Name} {Assembly.GetName().Version.ToString(3)} has been loaded!");
        }

        protected override void Unload()
        {
            DamageTool.damagePlayerRequested -= OnDamagePlayerRequested;
            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            UnturnedPlayerEvents.OnPlayerUpdatePosition -= OnPlayerUpdatePosition;
            UnturnedPlayerEvents.OnPlayerChatted -= OnPlayerChatted;

            AntiCheatService?.Save();
            AntiCheatService = null;
            Instance = null;

            Rocket.Core.Logging.Logger.Log($"{Name} has been unloaded!");
        }

        public void ReloadRuntimeConfiguration()
        {
            Configuration.Load();
            Configuration.Instance.ApplyDefaultsIfNeeded();
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

        private void OnDamagePlayerRequested(ref DamagePlayerParameters parameters, ref bool shouldAllow)
        {
            AntiCheatService?.RegisterDamage(
                parameters.player,
                parameters.killer,
                parameters.cause,
                parameters.limb,
                parameters.damage,
                parameters.times);
        }

        private void OnPlayerChatted(UnturnedPlayer player, ref Color color, string message, EChatMode chatMode, ref bool cancel)
        {
            AntiCheatService?.RegisterChat(player, message);
        }
    }
}
