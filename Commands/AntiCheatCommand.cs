using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Emqo.Unturned_AntiCheat.Models;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;

namespace Emqo.Unturned_AntiCheat.Commands
{
    public class AntiCheatCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;
        public string Name => "ac";
        public string Help => "Inspect anti-cheat state and apply manual actions.";
        public string Syntax => "<status|recent|evidence|reset|punish|reload>";
        public List<string> Aliases => new List<string> { "anticheat" };
        public List<string> Permissions => new List<string> { "unturned.anticheat.manage" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var service = Unturned_AntiCheatPlugin.Instance?.AntiCheatService;
            if (service == null)
            {
                Reply(caller, "Anti-cheat service is not ready.");
                return;
            }

            if (command.Length == 0)
            {
                Reply(caller, "Usage: /ac <status|recent|evidence|reset|punish|reload>");
                return;
            }

            switch (command[0].ToLowerInvariant())
            {
                case "status":
                    HandleStatus(caller, service, command);
                    break;
                case "recent":
                    HandleRecent(caller, service);
                    break;
                case "evidence":
                    HandleEvidence(caller, service, command);
                    break;
                case "reset":
                    HandleReset(caller, service, command);
                    break;
                case "punish":
                    HandlePunish(caller, service, command);
                    break;
                case "reload":
                    HandleReload(caller);
                    break;
                default:
                    Reply(caller, "Unknown subcommand.");
                    break;
            }
        }

        private static void HandleStatus(IRocketPlayer caller, Services.AntiCheatService service, string[] command)
        {
            if (command.Length < 2)
            {
                Reply(caller, "Usage: /ac status <player|steamId64>");
                return;
            }

            if (!service.TryResolvePlayer(command[1], out var player))
            {
                Reply(caller, "Player not found.");
                return;
            }

            var profile = service.GetPlayerProfile(player.CSteamID.m_SteamID);
            if (profile == null)
            {
                Reply(caller, "No anti-cheat profile found for that player.");
                return;
            }

            Reply(caller, $"{player.CharacterName}: score={profile.Score.ToString("F1", CultureInfo.InvariantCulture)}, violations={profile.TotalViolations}, kicks={profile.KickCount}, bans={profile.BanCount}");
        }

        private static void HandleRecent(IRocketPlayer caller, Services.AntiCheatService service)
        {
            var evidence = service.GetRecentEvidence(5);
            if (evidence.Count == 0)
            {
                Reply(caller, "No evidence recorded.");
                return;
            }

            foreach (var item in evidence)
            {
                Reply(caller, $"{item.TimestampUtc:u} {item.PlayerName} [{item.DetectorId}] +{item.Score:F1} {item.Summary}");
            }
        }

        private static void HandleEvidence(IRocketPlayer caller, Services.AntiCheatService service, string[] command)
        {
            if (command.Length < 2)
            {
                Reply(caller, "Usage: /ac evidence <player|steamId64>");
                return;
            }

            if (!service.TryResolvePlayer(command[1], out var player))
            {
                Reply(caller, "Player not found.");
                return;
            }

            var evidence = service.GetPlayerEvidence(player.CSteamID.m_SteamID, 5);
            if (evidence.Count == 0)
            {
                Reply(caller, "No evidence for that player.");
                return;
            }

            foreach (var item in evidence)
            {
                var metadata = item.Metadata.Count == 0
                    ? string.Empty
                    : $" [{string.Join(", ", item.Metadata.Select(x => x.Key + "=" + x.Value))}]";
                Reply(caller, $"{item.TimestampUtc:u} {item.DetectorId} +{item.Score:F1} {item.Summary}{metadata}");
            }
        }

        private static void HandleReset(IRocketPlayer caller, Services.AntiCheatService service, string[] command)
        {
            if (command.Length < 2)
            {
                Reply(caller, "Usage: /ac reset <player|steamId64>");
                return;
            }

            if (!service.TryResolvePlayer(command[1], out var player))
            {
                Reply(caller, "Player not found.");
                return;
            }

            service.ResetPlayer(player.CSteamID.m_SteamID);
            Reply(caller, $"Reset score for {player.CharacterName}.");
        }

        private static void HandlePunish(IRocketPlayer caller, Services.AntiCheatService service, string[] command)
        {
            if (command.Length < 3)
            {
                Reply(caller, "Usage: /ac punish <player|steamId64> <kick|ban>");
                return;
            }

            if (!service.TryResolvePlayer(command[1], out var player))
            {
                Reply(caller, "Player not found.");
                return;
            }

            PenaltyAction action;
            switch (command[2].ToLowerInvariant())
            {
                case "kick":
                    action = PenaltyAction.Kick;
                    break;
                case "ban":
                    action = PenaltyAction.Ban;
                    break;
                default:
                    Reply(caller, "Penalty must be kick or ban.");
                    return;
            }

            var decision = service.Punish(player, action, $"Manual anti-cheat {action.ToString().ToLowerInvariant()}");
            Reply(caller, $"Applied {decision.Action.ToString().ToLowerInvariant()} to {player.CharacterName}.");
        }

        private static void HandleReload(IRocketPlayer caller)
        {
            var plugin = Unturned_AntiCheatPlugin.Instance;
            if (plugin == null)
            {
                Reply(caller, "Anti-cheat plugin is not loaded.");
                return;
            }

            try
            {
                plugin.ReloadRuntimeConfiguration();
                Reply(caller, "Reloaded anti-cheat configuration and runtime thresholds.");
            }
            catch (System.Exception ex)
            {
                Logger.LogException(ex);
                Reply(caller, "Failed to reload anti-cheat configuration. Check server console.");
            }
        }

        private static void Reply(IRocketPlayer caller, string message)
        {
            if (caller is UnturnedPlayer unturnedPlayer)
            {
                UnturnedChat.Say(unturnedPlayer, message);
                return;
            }

            Logger.Log(message);
        }
    }
}
