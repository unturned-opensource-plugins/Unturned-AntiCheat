using System;
using System.Collections.Concurrent;
using System.Linq;
using Emqo.Unturned_AntiCheat.Models;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace Emqo.Unturned_AntiCheat.Services
{
    public class AntiCheatService
    {
        private readonly object _dataLock = new object();
        private readonly IAntiCheatRepository _repository;
        private Unturned_AntiCheatConfiguration _configuration;
        private readonly MovementDetector _movementDetector;
        private readonly CombatDetector _combatDetector;
        private readonly AbuseDetector _abuseDetector;
        private readonly ConcurrentDictionary<ulong, PlayerSession> _sessions = new ConcurrentDictionary<ulong, PlayerSession>();
        private AntiCheatDataStore _dataStore;

        public AntiCheatService(IAntiCheatRepository repository, Unturned_AntiCheatConfiguration configuration)
        {
            _repository = repository;
            _configuration = configuration;
            _movementDetector = new MovementDetector(configuration.Movement);
            _combatDetector = new CombatDetector(configuration.Combat);
            _abuseDetector = new AbuseDetector(configuration.Abuse);
            _dataStore = repository.Load();
        }

        public void ReloadConfiguration(Unturned_AntiCheatConfiguration configuration)
        {
            configuration?.ApplyDefaultsIfNeeded();
            if (configuration == null)
            {
                return;
            }

            var previousStorageFile = _configuration?.General?.StorageFileName;

            lock (_dataLock)
            {
                _configuration = configuration;
                _movementDetector.UpdateSettings(configuration.Movement);
                _combatDetector.UpdateSettings(configuration.Combat);
                _abuseDetector.UpdateSettings(configuration.Abuse);

                while (_dataStore.Evidence.Count > _configuration.General.MaxRecentEvidence)
                {
                    _dataStore.Evidence.RemoveAt(0);
                }

                Persist();
            }

            if (!string.Equals(previousStorageFile, configuration.General.StorageFileName, StringComparison.OrdinalIgnoreCase))
            {
                Rocket.Core.Logging.Logger.LogWarning("[AC] StorageFileName changed on reload. Existing repository path stays active until full plugin reload.");
            }
        }

        public PlayerProfile GetPlayerProfile(ulong steamId)
        {
            lock (_dataLock)
            {
                return _dataStore.Players.TryGetValue(steamId, out var profile)
                    ? profile
                    : null;
            }
        }

        public System.Collections.Generic.IReadOnlyList<PlayerEvidence> GetRecentEvidence(int count)
        {
            lock (_dataLock)
            {
                return _dataStore.Evidence
                    .OrderByDescending(x => x.TimestampUtc)
                    .Take(count)
                    .ToList();
            }
        }

        public System.Collections.Generic.IReadOnlyList<PlayerEvidence> GetPlayerEvidence(ulong steamId, int count)
        {
            lock (_dataLock)
            {
                return _dataStore.Evidence
                    .Where(x => x.SteamId == steamId)
                    .OrderByDescending(x => x.TimestampUtc)
                    .Take(count)
                    .ToList();
            }
        }

        public void RegisterConnected(UnturnedPlayer player)
        {
            if (!ShouldTrack(player))
            {
                return;
            }

            var session = _sessions.GetOrAdd(GetSteamId(player), _ => new PlayerSession());
            session.SteamId = GetSteamId(player);
            session.PlayerName = player.CharacterName;
            session.LastPosition = player.Position;
            session.LastPositionUtc = DateTime.UtcNow;

            lock (_dataLock)
            {
                var profile = GetOrCreateProfile(player);
                profile.LastKnownName = player.CharacterName;
                profile.LastScoreUpdateUtc = profile.LastScoreUpdateUtc == default ? DateTime.UtcNow : profile.LastScoreUpdateUtc;
                Persist();
            }
        }

        public void RegisterDisconnected(UnturnedPlayer player)
        {
            _sessions.TryRemove(GetSteamId(player), out _);
        }

        public void RegisterPosition(UnturnedPlayer player, Vector3 position)
        {
            if (!ShouldTrack(player))
            {
                return;
            }

            var session = GetOrCreateSession(player);
            session.PlayerName = player.CharacterName;
            foreach (var violation in _movementDetector.Analyze(session, position, DateTime.UtcNow, player.IsInVehicle))
            {
                ApplyViolation(player, violation);
            }
        }

        public void RegisterKill(UnturnedPlayer victim, CSteamID murderer, ELimb limb)
        {
            if (!_configuration.Combat.Enabled || murderer == CSteamID.Nil || murderer == victim.CSteamID)
            {
                return;
            }

            UnturnedPlayer attacker;
            try
            {
                attacker = UnturnedPlayer.FromCSteamID(murderer);
            }
            catch
            {
                return;
            }

            if (attacker == null || !ShouldTrack(attacker))
            {
                return;
            }

            var session = GetOrCreateSession(attacker);
            session.PlayerName = attacker.CharacterName;

            foreach (var violation in _combatDetector.RegisterKill(session, CombatDetector.IsHeadshot(limb), DateTime.UtcNow))
            {
                ApplyViolation(attacker, violation);
            }
        }

        public void RegisterDamage(Player victim, CSteamID attackerId, EDeathCause cause, ELimb limb, float damage, float times)
        {
            if (!_configuration.Combat.Enabled || cause != EDeathCause.GUN || victim == null)
            {
                return;
            }

            var victimOwner = victim.channel?.owner;
            if (victimOwner == null || attackerId == CSteamID.Nil || attackerId == victimOwner.playerID.steamID)
            {
                return;
            }

            UnturnedPlayer attacker;
            try
            {
                attacker = UnturnedPlayer.FromCSteamID(attackerId);
            }
            catch
            {
                return;
            }

            if (attacker == null || !ShouldTrack(attacker))
            {
                return;
            }

            var session = GetOrCreateSession(attacker);
            session.PlayerName = attacker.CharacterName;

            var effectiveDamage = Math.Max(0.01d, damage * Math.Max(1f, times));
            var victimSteamId = victimOwner.playerID.steamID.m_SteamID;
            var weaponAsset = attacker.Player?.equipment?.asset as ItemGunAsset;
            var weaponType = ClassifyWeaponType(weaponAsset);
            var weaponLabel = GetWeaponLabel(weaponAsset);
            var weaponGuid = GetWeaponGuid(weaponAsset);
            var distanceMeters = 0d;

            if (attacker.Player != null)
            {
                distanceMeters = Vector3.Distance(attacker.Player.transform.position, victim.transform.position);
            }

            foreach (var violation in _combatDetector.RegisterDamage(
                         session,
                         victimSteamId,
                         effectiveDamage,
                         CombatDetector.IsHeadshot(limb),
                         DateTime.UtcNow,
                         weaponType,
                         weaponLabel,
                         weaponGuid,
                         distanceMeters))
            {
                ApplyViolation(attacker, violation);
            }
        }

        public void RegisterChat(UnturnedPlayer player, string message)
        {
            if (!ShouldTrack(player))
            {
                return;
            }

            var session = GetOrCreateSession(player);
            session.PlayerName = player.CharacterName;
            foreach (var violation in _abuseDetector.RegisterChat(session, message, DateTime.UtcNow))
            {
                ApplyViolation(player, violation);
            }
        }

        public void ResetPlayer(ulong steamId)
        {
            lock (_dataLock)
            {
                if (_dataStore.Players.TryGetValue(steamId, out var profile))
                {
                    profile.Score = 0d;
                    profile.LastScoreUpdateUtc = DateTime.UtcNow;
                    Persist();
                }
            }
        }

        public PenaltyDecision Punish(UnturnedPlayer player, PenaltyAction action, string reason)
        {
            var decision = new PenaltyDecision
            {
                Action = action,
                CurrentScore = GetOrCreateProfile(player).Score,
                Reason = reason
            };

            ExecutePenalty(player, decision);
            return decision;
        }

        public bool TryResolvePlayer(string input, out UnturnedPlayer player)
        {
            player = null;
            if (ulong.TryParse(input, out var steamId))
            {
                try
                {
                    player = UnturnedPlayer.FromCSteamID(new CSteamID(steamId));
                    return player != null;
                }
                catch
                {
                    return false;
                }
            }

            try
            {
                player = UnturnedPlayer.FromName(input);
                return player != null;
            }
            catch
            {
                return false;
            }
        }

        public void Save()
        {
            lock (_dataLock)
            {
                Persist();
            }
        }

        private void ApplyViolation(UnturnedPlayer player, ViolationEvent violation)
        {
            lock (_dataLock)
            {
                var profile = GetOrCreateProfile(player);
                ApplyDecay(profile, DateTime.UtcNow);
                profile.Score += violation.Score;
                profile.LastKnownName = player.CharacterName;
                profile.LastScoreUpdateUtc = DateTime.UtcNow;
                profile.TotalViolations++;

                _dataStore.Evidence.Add(new PlayerEvidence
                {
                    SteamId = violation.SteamId,
                    PlayerName = violation.PlayerName,
                    DetectorId = violation.DetectorId,
                    Category = violation.Category,
                    Summary = violation.Summary,
                    Score = violation.Score,
                    TimestampUtc = violation.TimestampUtc,
                    Metadata = violation.Metadata
                });

                if (_dataStore.Evidence.Count > _configuration.General.MaxRecentEvidence)
                {
                    var removeCount = _dataStore.Evidence.Count - _configuration.General.MaxRecentEvidence;
                    _dataStore.Evidence.RemoveRange(0, removeCount);
                }

                var decision = EvaluatePenalty(profile);
                Persist();

                Rocket.Core.Logging.Logger.LogWarning($"[AC] {player.CharacterName} ({player.CSteamID}) hit {violation.DetectorId}: {violation.Summary} (+{violation.Score:F1}, score {profile.Score:F1})");
                if (decision.Action != PenaltyAction.None)
                {
                    ExecutePenalty(player, decision);
                }
            }
        }

        private PenaltyDecision EvaluatePenalty(PlayerProfile profile)
        {
            var nowUtc = DateTime.UtcNow;
            if (profile.LastPenaltyUtc.HasValue &&
                nowUtc - profile.LastPenaltyUtc.Value < TimeSpan.FromMinutes(_configuration.Penalties.PenaltyCooldownMinutes))
            {
                return new PenaltyDecision { Action = PenaltyAction.None, CurrentScore = profile.Score };
            }

            if (_configuration.Penalties.AutoBan && profile.Score >= _configuration.Penalties.BanScore)
            {
                return new PenaltyDecision
                {
                    Action = PenaltyAction.Ban,
                    CurrentScore = profile.Score,
                    Reason = $"AntiCheat score reached {profile.Score:F1}"
                };
            }

            if (_configuration.Penalties.AutoKick && profile.Score >= _configuration.Penalties.KickScore)
            {
                return new PenaltyDecision
                {
                    Action = PenaltyAction.Kick,
                    CurrentScore = profile.Score,
                    Reason = $"AntiCheat score reached {profile.Score:F1}"
                };
            }

            if (profile.Score >= _configuration.Penalties.AlertScore)
            {
                return new PenaltyDecision
                {
                    Action = PenaltyAction.Alert,
                    CurrentScore = profile.Score,
                    Reason = $"AntiCheat score reached {profile.Score:F1}"
                };
            }

            return new PenaltyDecision { Action = PenaltyAction.None, CurrentScore = profile.Score };
        }

        private void ExecutePenalty(UnturnedPlayer player, PenaltyDecision decision)
        {
            if (decision.Action == PenaltyAction.None)
            {
                return;
            }

            var profile = GetOrCreateProfile(player);
            profile.LastPenaltyUtc = DateTime.UtcNow;

            switch (decision.Action)
            {
                case PenaltyAction.Alert:
                    Rocket.Core.Logging.Logger.LogWarning($"[AC] Alert on {player.CharacterName}: {decision.Reason}");
                    break;
                case PenaltyAction.Kick:
                    profile.KickCount++;
                    Rocket.Core.Logging.Logger.LogWarning($"[AC] Kicking {player.CharacterName}: {decision.Reason}");
                    player.Kick(decision.Reason);
                    break;
                case PenaltyAction.Ban:
                    profile.BanCount++;
                    Rocket.Core.Logging.Logger.LogWarning($"[AC] Ban requested for {player.CharacterName}: {decision.Reason}");
                    if (!TryBan(player, decision.Reason))
                    {
                        profile.KickCount++;
                        Rocket.Core.Logging.Logger.LogWarning($"[AC] Ban failed for {player.CharacterName}; falling back to kick.");
                        player.Kick(decision.Reason);
                    }
                    break;
            }

            Persist();
        }

        private bool TryBan(UnturnedPlayer player, string reason)
        {
            if (player?.Player?.channel?.owner == null)
            {
                return false;
            }

            var owner = player.Player.channel.owner;
            var duration = _configuration.Penalties.BanDurationSeconds;
            var hwids = owner.playerID?.GetHwids() ?? Enumerable.Empty<byte[]>();
            var ipAddress = owner.getIPv4AddressOrZero();

            try
            {
                return Provider.requestBanPlayer(CSteamID.Nil, player.CSteamID, ipAddress, hwids, reason, duration);
            }
            catch (Exception ex)
            {
                Rocket.Core.Logging.Logger.LogError($"[AC] Provider.requestBanPlayer failed for {player.CharacterName}: {ex}");
            }

            try
            {
                player.Ban(CSteamID.Nil, reason, duration);
                return true;
            }
            catch (Exception ex)
            {
                Rocket.Core.Logging.Logger.LogError($"[AC] Player.Ban fallback failed for {player.CharacterName}: {ex}");
                return false;
            }
        }

        private PlayerSession GetOrCreateSession(UnturnedPlayer player)
        {
            var session = _sessions.GetOrAdd(GetSteamId(player), _ => new PlayerSession());
            session.SteamId = GetSteamId(player);
            session.PlayerName = player.CharacterName;
            return session;
        }

        private PlayerProfile GetOrCreateProfile(UnturnedPlayer player)
        {
            return GetOrCreateProfile(GetSteamId(player), player.CharacterName);
        }

        private PlayerProfile GetOrCreateProfile(ulong steamId, string playerName = null)
        {
            if (!_dataStore.Players.TryGetValue(steamId, out var profile))
            {
                profile = new PlayerProfile
                {
                    SteamId = steamId,
                    LastKnownName = playerName ?? steamId.ToString(),
                    LastScoreUpdateUtc = DateTime.UtcNow
                };
                _dataStore.Players[steamId] = profile;
            }
            else if (!string.IsNullOrEmpty(playerName))
            {
                profile.LastKnownName = playerName;
            }

            return profile;
        }

        private void ApplyDecay(PlayerProfile profile, DateTime nowUtc)
        {
            if (profile.LastScoreUpdateUtc == default)
            {
                profile.LastScoreUpdateUtc = nowUtc;
                return;
            }

            var elapsedMinutes = (nowUtc - profile.LastScoreUpdateUtc).TotalMinutes;
            if (elapsedMinutes <= 0d || profile.Score <= 0d)
            {
                profile.LastScoreUpdateUtc = nowUtc;
                return;
            }

            profile.Score = Math.Max(0d, profile.Score - elapsedMinutes * _configuration.Penalties.ScoreDecayPerMinute);
            profile.LastScoreUpdateUtc = nowUtc;
        }

        private bool ShouldTrack(UnturnedPlayer player)
        {
            if (!_configuration.General.Enabled || player == null)
            {
                return false;
            }

            var steamId = GetSteamId(player);
            if (_configuration.General.WhitelistedSteamIds.Contains(steamId))
            {
                return false;
            }

            return !_configuration.General.AdminBypass || !player.IsAdmin;
        }

        private void Persist()
        {
            _repository.Save(_dataStore);
        }

        private static ulong GetSteamId(UnturnedPlayer player)
        {
            return player.CSteamID.m_SteamID;
        }

        private static CombatWeaponType ClassifyWeaponType(ItemGunAsset weaponAsset)
        {
            if (weaponAsset == null)
            {
                return CombatWeaponType.Default;
            }

            if (weaponAsset.ammoPerShot > 1)
            {
                return CombatWeaponType.Shotgun;
            }

            if (weaponAsset.action == EAction.Bolt || weaponAsset.action == EAction.Rail)
            {
                return CombatWeaponType.Sniper;
            }

            if (weaponAsset.action == EAction.Minigun || weaponAsset.hasAuto)
            {
                return CombatWeaponType.Automatic;
            }

            if (weaponAsset.range >= 120f || weaponAsset.ballisticTravel >= 120f)
            {
                return CombatWeaponType.Precision;
            }

            return CombatWeaponType.Default;
        }

        private static string GetWeaponLabel(ItemGunAsset weaponAsset)
        {
            if (weaponAsset == null || string.IsNullOrWhiteSpace(weaponAsset.itemName))
            {
                return "unknown_gun";
            }

            return weaponAsset.itemName;
        }

        private static string GetWeaponGuid(ItemGunAsset weaponAsset)
        {
            if (weaponAsset == null || weaponAsset.GUID == Guid.Empty)
            {
                return string.Empty;
            }

            return weaponAsset.GUID.ToString("D");
        }
    }
}
