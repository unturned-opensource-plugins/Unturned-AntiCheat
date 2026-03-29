using System;
using System.Collections.Generic;
using System.Linq;
using Emqo.Unturned_AntiCheat.Models;
using SDG.Unturned;

namespace Emqo.Unturned_AntiCheat.Services
{
    public class CombatDetector
    {
        private const string DamageBurstDetectorId = "combat.damage_burst";
        private const string HeadshotHitDetectorId = "combat.headshot_hit_ratio";
        private const string KillBurstDetectorId = "combat.kill_burst";
        private const string HeadshotBurstDetectorId = "combat.headshot_burst";

        private CombatDetectionSettings _settings;

        public CombatDetector(CombatDetectionSettings settings)
        {
            _settings = settings;
        }

        public void UpdateSettings(CombatDetectionSettings settings)
        {
            _settings = settings;
        }

        public IReadOnlyList<ViolationEvent> RegisterDamage(
            PlayerSession attackerSession,
            ulong victimSteamId,
            double damage,
            bool wasHeadshot,
            DateTime nowUtc,
            CombatWeaponType weaponType,
            string weaponLabel,
            string weaponGuid,
            double distanceMeters)
        {
            var violations = new List<ViolationEvent>();
            if (!_settings.Enabled || damage <= 0d)
            {
                return violations;
            }

            TrimCombatSamples(attackerSession.DamageSamples, nowUtc, TimeSpan.FromSeconds(_settings.DamageWindowSeconds));
            TrimCombatSamples(attackerSession.HeadshotDamageSamples, nowUtc, TimeSpan.FromSeconds(_settings.DamageWindowSeconds));

            var sample = new CombatSample(nowUtc, victimSteamId, damage, wasHeadshot);
            attackerSession.DamageSamples.Enqueue(sample);
            if (wasHeadshot)
            {
                attackerSession.HeadshotDamageSamples.Enqueue(sample);
            }

            var hitCount = attackerSession.DamageSamples.Count;
            var totalDamage = attackerSession.DamageSamples.Sum(x => x.Damage);
            var uniqueVictims = attackerSession.DamageSamples
                .Select(x => x.VictimSteamId)
                .Distinct()
                .Count();
            var weaponProfile = ResolveWeaponProfile(weaponType);
            var weaponOverride = ResolveWeaponOverride(weaponGuid);
            var distanceProfile = weaponOverride?.IgnoreDistanceProfile == true
                ? CreateNeutralDistanceProfile()
                : ResolveDistanceProfile(distanceMeters);
            var minimumHitsForBurstCheck = weaponOverride?.MinimumHitsForBurstCheck ?? _settings.MinimumHitsForBurstCheck;
            var minimumHitsForHeadshotCheck = weaponOverride?.MinimumHitsForHeadshotCheck ?? _settings.MinimumHitsForHeadshotCheck;
            var maximumDamagePerWindow = weaponOverride?.MaximumDamagePerWindow ?? _settings.MaximumDamagePerWindow;
            var maximumHeadshotHitRatio = weaponOverride?.MaximumHeadshotHitRatio ?? _settings.MaximumHeadshotHitRatio;
            var damageThresholdMultiplier = weaponOverride?.DamageThresholdMultiplier ?? weaponProfile.DamageThresholdMultiplier;
            var headshotThresholdMultiplier = weaponOverride?.HeadshotRatioThresholdMultiplier ?? weaponProfile.HeadshotRatioThresholdMultiplier;
            var damageThreshold = maximumDamagePerWindow *
                                  damageThresholdMultiplier *
                                  distanceProfile.DamageThresholdMultiplier;
            var headshotRatioThreshold = Math.Max(
                0.1d,
                Math.Min(
                    0.99d,
                    maximumHeadshotHitRatio *
                    headshotThresholdMultiplier *
                    distanceProfile.HeadshotRatioThresholdMultiplier));

            if (hitCount >= minimumHitsForBurstCheck &&
                totalDamage >= damageThreshold &&
                uniqueVictims >= 2 &&
                IsOffCooldown(attackerSession, DamageBurstDetectorId, nowUtc, _settings.CooldownSeconds))
            {
                violations.Add(CreateViolation(
                    attackerSession,
                    DamageBurstDetectorId,
                    "combat",
                    $"Burst combat damage detected: {totalDamage:F0} damage across {hitCount} hits in {_settings.DamageWindowSeconds:F0}s with {weaponLabel} at {distanceMeters:F1}m.",
                    _settings.DamageBurstViolationScore,
                    nowUtc,
                    ("weapon_type", weaponType.ToString()),
                    ("weapon_label", weaponLabel),
                    ("weapon_guid", weaponGuid),
                    ("weapon_override", weaponOverride?.WeaponName ?? string.Empty),
                    ("ignore_distance_profile", (weaponOverride?.IgnoreDistanceProfile == true).ToString()),
                    ("distance_band", distanceProfile.Name),
                    ("distance_meters", distanceMeters.ToString("F1")),
                    ("damage_window_seconds", _settings.DamageWindowSeconds.ToString("F0")),
                    ("minimum_hits", minimumHitsForBurstCheck.ToString()),
                    ("hits", hitCount.ToString()),
                    ("damage", totalDamage.ToString("F1")),
                    ("base_damage_threshold", maximumDamagePerWindow.ToString("F1")),
                    ("damage_threshold", damageThreshold.ToString("F1")),
                    ("victims", uniqueVictims.ToString())));
            }

            if (hitCount >= minimumHitsForHeadshotCheck)
            {
                var headshotHits = attackerSession.HeadshotDamageSamples.Count;
                var headshotRatio = headshotHits / (double)hitCount;
                if (headshotRatio >= headshotRatioThreshold &&
                    IsOffCooldown(attackerSession, HeadshotHitDetectorId, nowUtc, _settings.CooldownSeconds))
                {
                    violations.Add(CreateViolation(
                        attackerSession,
                        HeadshotHitDetectorId,
                        "combat",
                        $"Suspicious gun-hit headshot ratio detected: {headshotRatio:P0} over {hitCount} hits with {weaponLabel} at {distanceMeters:F1}m.",
                        _settings.HeadshotHitViolationScore,
                        nowUtc,
                        ("weapon_type", weaponType.ToString()),
                        ("weapon_label", weaponLabel),
                        ("weapon_guid", weaponGuid),
                        ("weapon_override", weaponOverride?.WeaponName ?? string.Empty),
                        ("ignore_distance_profile", (weaponOverride?.IgnoreDistanceProfile == true).ToString()),
                        ("distance_band", distanceProfile.Name),
                        ("distance_meters", distanceMeters.ToString("F1")),
                        ("minimum_hits", minimumHitsForHeadshotCheck.ToString()),
                        ("hits", hitCount.ToString()),
                        ("headshot_hits", headshotHits.ToString()),
                        ("base_headshot_threshold", maximumHeadshotHitRatio.ToString("F2")),
                        ("headshot_threshold", headshotRatioThreshold.ToString("F2")),
                        ("ratio", headshotRatio.ToString("F2"))));
                }
            }

            return violations;
        }

        public IReadOnlyList<ViolationEvent> RegisterKill(PlayerSession attackerSession, bool wasHeadshot, DateTime nowUtc)
        {
            var violations = new List<ViolationEvent>();
            if (!_settings.Enabled)
            {
                return violations;
            }

            TrimQueue(attackerSession.KillSamples, nowUtc);
            TrimQueue(attackerSession.HeadshotKillSamples, nowUtc);

            attackerSession.KillSamples.Enqueue(nowUtc);
            if (wasHeadshot)
            {
                attackerSession.HeadshotKillSamples.Enqueue(nowUtc);
            }

            if (attackerSession.KillSamples.Count > _settings.MaximumKillsPerWindow &&
                attackerSession.KillSamples.Count >= _settings.MinimumKillsForBurstCheck &&
                IsOffCooldown(attackerSession, KillBurstDetectorId, nowUtc, _settings.CooldownSeconds))
            {
                violations.Add(CreateViolation(
                    attackerSession,
                    KillBurstDetectorId,
                    "combat",
                    $"Burst kill cadence detected: {attackerSession.KillSamples.Count} kills in {_settings.KillWindowSeconds:F0}s.",
                    _settings.KillBurstViolationScore,
                    nowUtc,
                    ("kills_in_window", attackerSession.KillSamples.Count.ToString())));
            }

            if (attackerSession.KillSamples.Count >= _settings.MinimumKillsForHeadshotCheck)
            {
                var headshotRatio = attackerSession.HeadshotKillSamples.Count / (double)attackerSession.KillSamples.Count;
                if (headshotRatio >= _settings.MaximumHeadshotRatio &&
                    IsOffCooldown(attackerSession, HeadshotBurstDetectorId, nowUtc, _settings.CooldownSeconds))
                {
                    violations.Add(CreateViolation(
                        attackerSession,
                        HeadshotBurstDetectorId,
                        "combat",
                        $"Suspicious headshot ratio detected: {headshotRatio:P0} over {attackerSession.KillSamples.Count} kills.",
                        _settings.HeadshotBurstViolationScore,
                        nowUtc,
                        ("headshots", attackerSession.HeadshotKillSamples.Count.ToString()),
                        ("kills", attackerSession.KillSamples.Count.ToString()),
                        ("ratio", headshotRatio.ToString("F2"))));
                }
            }

            return violations;
        }

        public static bool IsHeadshot(ELimb limb)
        {
            return limb == ELimb.SKULL;
        }

        private void TrimQueue(Queue<DateTime> samples, DateTime nowUtc)
        {
            var window = TimeSpan.FromSeconds(_settings.KillWindowSeconds);
            while (samples.Count > 0 && nowUtc - samples.Peek() > window)
            {
                samples.Dequeue();
            }
        }

        private static void TrimCombatSamples(Queue<CombatSample> samples, DateTime nowUtc, TimeSpan window)
        {
            while (samples.Count > 0 && nowUtc - samples.Peek().TimestampUtc > window)
            {
                samples.Dequeue();
            }
        }

        private CombatWeaponProfileSettings ResolveWeaponProfile(CombatWeaponType weaponType)
        {
            return _settings.WeaponProfiles?.FirstOrDefault(x => x.WeaponType == weaponType) ??
                   _settings.WeaponProfiles?.FirstOrDefault(x => x.WeaponType == CombatWeaponType.Default) ??
                   new CombatWeaponProfileSettings();
        }

        private CombatDistanceProfileSettings ResolveDistanceProfile(double distanceMeters)
        {
            var profile = _settings.DistanceProfiles?.FirstOrDefault(x =>
                distanceMeters >= x.MinimumDistanceMeters &&
                distanceMeters < x.MaximumDistanceMeters);

            if (profile != null)
            {
                return profile;
            }

            return _settings.DistanceProfiles?.OrderByDescending(x => x.MaximumDistanceMeters).FirstOrDefault() ??
                   new CombatDistanceProfileSettings { Name = "default" };
        }

        private static CombatDistanceProfileSettings CreateNeutralDistanceProfile()
        {
            return new CombatDistanceProfileSettings
            {
                Name = "ignored",
                DamageThresholdMultiplier = 1d,
                HeadshotRatioThresholdMultiplier = 1d
            };
        }

        private CombatWeaponOverrideSettings ResolveWeaponOverride(string weaponGuid)
        {
            var normalizedGuid = NormalizeGuid(weaponGuid);
            if (string.IsNullOrEmpty(normalizedGuid))
            {
                return null;
            }

            return _settings.WeaponOverrides?.FirstOrDefault(x => NormalizeGuid(x.WeaponGuid) == normalizedGuid);
        }

        private static string NormalizeGuid(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Trim().Trim('{', '}').ToLowerInvariant();
        }

        private static bool IsOffCooldown(PlayerSession session, string detectorId, DateTime nowUtc, double cooldownSeconds)
        {
            if (session.DetectorCooldownsUtc.TryGetValue(detectorId, out var cooldownEndsAt) && cooldownEndsAt > nowUtc)
            {
                return false;
            }

            session.DetectorCooldownsUtc[detectorId] = nowUtc.AddSeconds(cooldownSeconds);
            return true;
        }

        private static ViolationEvent CreateViolation(
            PlayerSession session,
            string detectorId,
            string category,
            string summary,
            double score,
            DateTime nowUtc,
            params (string Key, string Value)[] metadata)
        {
            var violation = new ViolationEvent
            {
                SteamId = session.SteamId,
                PlayerName = session.PlayerName,
                DetectorId = detectorId,
                Category = category,
                Summary = summary,
                Score = score,
                TimestampUtc = nowUtc
            };

            foreach (var item in metadata)
            {
                violation.Metadata[item.Key] = item.Value;
            }

            return violation;
        }
    }
}
