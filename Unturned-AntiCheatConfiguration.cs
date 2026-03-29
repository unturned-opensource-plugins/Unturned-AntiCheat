using System.Collections.Generic;
using System.Linq;
using Rocket.API;

namespace Emqo.Unturned_AntiCheat
{
    public class Unturned_AntiCheatConfiguration : IRocketPluginConfiguration
    {
        public GeneralSettings General { get; set; }
        public PenaltySettings Penalties { get; set; }
        public MovementDetectionSettings Movement { get; set; }
        public CombatDetectionSettings Combat { get; set; }
        public AbuseDetectionSettings Abuse { get; set; }

        public void LoadDefaults()
        {
            General = new GeneralSettings
            {
                Enabled = true,
                AdminBypass = true,
                StorageFileName = "anticheat-data.json",
                MaxRecentEvidence = 2000,
                WhitelistedSteamIds = new List<ulong>()
            };
            Penalties = new PenaltySettings
            {
                AlertScore = 20d,
                KickScore = 45d,
                BanScore = 85d,
                BanDurationSeconds = 0u,
                ScoreDecayPerMinute = 1.5d,
                PenaltyCooldownMinutes = 10,
                AutoBan = false,
                AutoKick = true
            };
            Movement = new MovementDetectionSettings
            {
                Enabled = true,
                MinimumSampleMilliseconds = 250,
                MaxHorizontalSpeedMetersPerSecond = 14d,
                SustainedHorizontalSpeedMetersPerSecond = 11d,
                MaxVerticalDeltaMeters = 6d,
                MaxTeleportDistanceMeters = 35d,
                SustainedWindowSeconds = 4d,
                CooldownSeconds = 8d,
                SpeedViolationScore = 12d,
                VerticalViolationScore = 16d,
                TeleportViolationScore = 28d
            };
            Combat = new CombatDetectionSettings
            {
                Enabled = true,
                DamageWindowSeconds = 8d,
                MinimumHitsForBurstCheck = 8,
                MaximumDamagePerWindow = 320d,
                MinimumHitsForHeadshotCheck = 10,
                MaximumHeadshotHitRatio = 0.8d,
                DamageBurstViolationScore = 10d,
                HeadshotHitViolationScore = 14d,
                KillWindowSeconds = 30d,
                MinimumKillsForBurstCheck = 4,
                MaximumKillsPerWindow = 4,
                MinimumKillsForHeadshotCheck = 4,
                MaximumHeadshotRatio = 0.85d,
                KillBurstViolationScore = 14d,
                HeadshotBurstViolationScore = 18d,
                CooldownSeconds = 25d,
                WeaponProfiles = CreateDefaultWeaponProfiles(),
                DistanceProfiles = CreateDefaultDistanceProfiles(),
                WeaponOverrides = new List<CombatWeaponOverrideSettings>()
            };
            Abuse = new AbuseDetectionSettings
            {
                Enabled = true,
                ChatWindowSeconds = 8d,
                MaximumMessagesPerWindow = 5,
                ChatViolationScore = 8d,
                CooldownSeconds = 12d
            };
        }

        public void ApplyDefaultsIfNeeded()
        {
            var defaults = new Unturned_AntiCheatConfiguration();
            defaults.LoadDefaults();

            General ??= defaults.General;
            Penalties ??= defaults.Penalties;
            Movement ??= defaults.Movement;
            Combat ??= defaults.Combat;
            Abuse ??= defaults.Abuse;

            General.StorageFileName ??= defaults.General.StorageFileName;
            General.WhitelistedSteamIds ??= new List<ulong>();
            if (General.MaxRecentEvidence <= 0)
            {
                General.MaxRecentEvidence = defaults.General.MaxRecentEvidence;
            }

            if (Penalties.AlertScore <= 0d)
            {
                Penalties.AlertScore = defaults.Penalties.AlertScore;
            }

            if (Penalties.KickScore <= 0d)
            {
                Penalties.KickScore = defaults.Penalties.KickScore;
            }

            if (Penalties.BanScore <= 0d)
            {
                Penalties.BanScore = defaults.Penalties.BanScore;
            }

            if (Penalties.ScoreDecayPerMinute <= 0d)
            {
                Penalties.ScoreDecayPerMinute = defaults.Penalties.ScoreDecayPerMinute;
            }

            if (Penalties.PenaltyCooldownMinutes <= 0)
            {
                Penalties.PenaltyCooldownMinutes = defaults.Penalties.PenaltyCooldownMinutes;
            }

            if (Movement.MinimumSampleMilliseconds <= 0)
            {
                Movement.MinimumSampleMilliseconds = defaults.Movement.MinimumSampleMilliseconds;
            }

            if (Movement.MaxHorizontalSpeedMetersPerSecond <= 0d)
            {
                Movement.MaxHorizontalSpeedMetersPerSecond = defaults.Movement.MaxHorizontalSpeedMetersPerSecond;
            }

            if (Movement.SustainedHorizontalSpeedMetersPerSecond <= 0d)
            {
                Movement.SustainedHorizontalSpeedMetersPerSecond = defaults.Movement.SustainedHorizontalSpeedMetersPerSecond;
            }

            if (Movement.MaxVerticalDeltaMeters <= 0d)
            {
                Movement.MaxVerticalDeltaMeters = defaults.Movement.MaxVerticalDeltaMeters;
            }

            if (Movement.MaxTeleportDistanceMeters <= 0d)
            {
                Movement.MaxTeleportDistanceMeters = defaults.Movement.MaxTeleportDistanceMeters;
            }

            if (Movement.SustainedWindowSeconds <= 0d)
            {
                Movement.SustainedWindowSeconds = defaults.Movement.SustainedWindowSeconds;
            }

            if (Movement.CooldownSeconds <= 0d)
            {
                Movement.CooldownSeconds = defaults.Movement.CooldownSeconds;
            }

            if (Movement.SpeedViolationScore <= 0d)
            {
                Movement.SpeedViolationScore = defaults.Movement.SpeedViolationScore;
            }

            if (Movement.VerticalViolationScore <= 0d)
            {
                Movement.VerticalViolationScore = defaults.Movement.VerticalViolationScore;
            }

            if (Movement.TeleportViolationScore <= 0d)
            {
                Movement.TeleportViolationScore = defaults.Movement.TeleportViolationScore;
            }

            if (Combat.DamageWindowSeconds <= 0d)
            {
                Combat.DamageWindowSeconds = defaults.Combat.DamageWindowSeconds;
            }

            if (Combat.MinimumHitsForBurstCheck <= 0)
            {
                Combat.MinimumHitsForBurstCheck = defaults.Combat.MinimumHitsForBurstCheck;
            }

            if (Combat.MaximumDamagePerWindow <= 0d)
            {
                Combat.MaximumDamagePerWindow = defaults.Combat.MaximumDamagePerWindow;
            }

            if (Combat.MinimumHitsForHeadshotCheck <= 0)
            {
                Combat.MinimumHitsForHeadshotCheck = defaults.Combat.MinimumHitsForHeadshotCheck;
            }

            if (Combat.MaximumHeadshotHitRatio <= 0d)
            {
                Combat.MaximumHeadshotHitRatio = defaults.Combat.MaximumHeadshotHitRatio;
            }

            if (Combat.DamageBurstViolationScore <= 0d)
            {
                Combat.DamageBurstViolationScore = defaults.Combat.DamageBurstViolationScore;
            }

            if (Combat.HeadshotHitViolationScore <= 0d)
            {
                Combat.HeadshotHitViolationScore = defaults.Combat.HeadshotHitViolationScore;
            }

            if (Combat.KillWindowSeconds <= 0d)
            {
                Combat.KillWindowSeconds = defaults.Combat.KillWindowSeconds;
            }

            if (Combat.MinimumKillsForBurstCheck <= 0)
            {
                Combat.MinimumKillsForBurstCheck = defaults.Combat.MinimumKillsForBurstCheck;
            }

            if (Combat.MaximumKillsPerWindow <= 0)
            {
                Combat.MaximumKillsPerWindow = defaults.Combat.MaximumKillsPerWindow;
            }

            if (Combat.MinimumKillsForHeadshotCheck <= 0)
            {
                Combat.MinimumKillsForHeadshotCheck = defaults.Combat.MinimumKillsForHeadshotCheck;
            }

            if (Combat.MaximumHeadshotRatio <= 0d)
            {
                Combat.MaximumHeadshotRatio = defaults.Combat.MaximumHeadshotRatio;
            }

            if (Combat.KillBurstViolationScore <= 0d)
            {
                Combat.KillBurstViolationScore = defaults.Combat.KillBurstViolationScore;
            }

            if (Combat.HeadshotBurstViolationScore <= 0d)
            {
                Combat.HeadshotBurstViolationScore = defaults.Combat.HeadshotBurstViolationScore;
            }

            if (Combat.CooldownSeconds <= 0d)
            {
                Combat.CooldownSeconds = defaults.Combat.CooldownSeconds;
            }

            Combat.WeaponProfiles ??= CreateDefaultWeaponProfiles();
            Combat.DistanceProfiles ??= CreateDefaultDistanceProfiles();
            Combat.WeaponOverrides ??= new List<CombatWeaponOverrideSettings>();

            foreach (var profile in Combat.WeaponProfiles.Where(x => x != null))
            {
                if (profile.DamageThresholdMultiplier <= 0d)
                {
                    profile.DamageThresholdMultiplier = 1d;
                }

                if (profile.HeadshotRatioThresholdMultiplier <= 0d)
                {
                    profile.HeadshotRatioThresholdMultiplier = 1d;
                }
            }

            foreach (var profile in Combat.DistanceProfiles.Where(x => x != null))
            {
                profile.Name ??= "unnamed";
                if (profile.MaximumDistanceMeters <= profile.MinimumDistanceMeters)
                {
                    profile.MaximumDistanceMeters = profile.MinimumDistanceMeters + 1d;
                }

                if (profile.DamageThresholdMultiplier <= 0d)
                {
                    profile.DamageThresholdMultiplier = 1d;
                }

                if (profile.HeadshotRatioThresholdMultiplier <= 0d)
                {
                    profile.HeadshotRatioThresholdMultiplier = 1d;
                }
            }

            foreach (var profile in Combat.WeaponOverrides.Where(x => x != null))
            {
                profile.WeaponGuid ??= string.Empty;
                profile.WeaponName ??= string.Empty;
                if (profile.MinimumHitsForBurstCheck <= 0)
                {
                    profile.MinimumHitsForBurstCheck = Combat.MinimumHitsForBurstCheck;
                }

                if (profile.MinimumHitsForHeadshotCheck <= 0)
                {
                    profile.MinimumHitsForHeadshotCheck = Combat.MinimumHitsForHeadshotCheck;
                }

                if (profile.MaximumDamagePerWindow <= 0d)
                {
                    profile.MaximumDamagePerWindow = Combat.MaximumDamagePerWindow;
                }

                if (profile.MaximumHeadshotHitRatio <= 0d)
                {
                    profile.MaximumHeadshotHitRatio = Combat.MaximumHeadshotHitRatio;
                }

                if (profile.DamageThresholdMultiplier <= 0d)
                {
                    profile.DamageThresholdMultiplier = 1d;
                }

                if (profile.HeadshotRatioThresholdMultiplier <= 0d)
                {
                    profile.HeadshotRatioThresholdMultiplier = 1d;
                }
            }

            if (Abuse.ChatWindowSeconds <= 0d)
            {
                Abuse.ChatWindowSeconds = defaults.Abuse.ChatWindowSeconds;
            }

            if (Abuse.MaximumMessagesPerWindow <= 0)
            {
                Abuse.MaximumMessagesPerWindow = defaults.Abuse.MaximumMessagesPerWindow;
            }

            if (Abuse.ChatViolationScore <= 0d)
            {
                Abuse.ChatViolationScore = defaults.Abuse.ChatViolationScore;
            }

            if (Abuse.CooldownSeconds <= 0d)
            {
                Abuse.CooldownSeconds = defaults.Abuse.CooldownSeconds;
            }
        }

        private static List<CombatWeaponProfileSettings> CreateDefaultWeaponProfiles()
        {
            return new List<CombatWeaponProfileSettings>
            {
                new CombatWeaponProfileSettings { WeaponType = CombatWeaponType.Default, DamageThresholdMultiplier = 1d, HeadshotRatioThresholdMultiplier = 1d },
                new CombatWeaponProfileSettings { WeaponType = CombatWeaponType.Automatic, DamageThresholdMultiplier = 0.85d, HeadshotRatioThresholdMultiplier = 0.95d },
                new CombatWeaponProfileSettings { WeaponType = CombatWeaponType.Precision, DamageThresholdMultiplier = 1.15d, HeadshotRatioThresholdMultiplier = 1d },
                new CombatWeaponProfileSettings { WeaponType = CombatWeaponType.Sniper, DamageThresholdMultiplier = 1.35d, HeadshotRatioThresholdMultiplier = 1.1d },
                new CombatWeaponProfileSettings { WeaponType = CombatWeaponType.Shotgun, DamageThresholdMultiplier = 1.45d, HeadshotRatioThresholdMultiplier = 1.2d }
            };
        }

        private static List<CombatDistanceProfileSettings> CreateDefaultDistanceProfiles()
        {
            return new List<CombatDistanceProfileSettings>
            {
                new CombatDistanceProfileSettings { Name = "close", MinimumDistanceMeters = 0d, MaximumDistanceMeters = 20d, DamageThresholdMultiplier = 1.2d, HeadshotRatioThresholdMultiplier = 1.15d },
                new CombatDistanceProfileSettings { Name = "mid", MinimumDistanceMeters = 20d, MaximumDistanceMeters = 60d, DamageThresholdMultiplier = 1d, HeadshotRatioThresholdMultiplier = 1d },
                new CombatDistanceProfileSettings { Name = "long", MinimumDistanceMeters = 60d, MaximumDistanceMeters = 100000d, DamageThresholdMultiplier = 0.85d, HeadshotRatioThresholdMultiplier = 0.9d }
            };
        }
    }

    public class GeneralSettings
    {
        public bool Enabled { get; set; }
        public bool AdminBypass { get; set; }
        public string StorageFileName { get; set; }
        public int MaxRecentEvidence { get; set; }
        public List<ulong> WhitelistedSteamIds { get; set; }
    }

    public class PenaltySettings
    {
        public double AlertScore { get; set; }
        public double KickScore { get; set; }
        public double BanScore { get; set; }
        public uint BanDurationSeconds { get; set; }
        public double ScoreDecayPerMinute { get; set; }
        public int PenaltyCooldownMinutes { get; set; }
        public bool AutoKick { get; set; }
        public bool AutoBan { get; set; }
    }

    public class MovementDetectionSettings
    {
        public bool Enabled { get; set; }
        public int MinimumSampleMilliseconds { get; set; }
        public double MaxHorizontalSpeedMetersPerSecond { get; set; }
        public double SustainedHorizontalSpeedMetersPerSecond { get; set; }
        public double MaxVerticalDeltaMeters { get; set; }
        public double MaxTeleportDistanceMeters { get; set; }
        public double SustainedWindowSeconds { get; set; }
        public double CooldownSeconds { get; set; }
        public double SpeedViolationScore { get; set; }
        public double VerticalViolationScore { get; set; }
        public double TeleportViolationScore { get; set; }
    }

    public class CombatDetectionSettings
    {
        public bool Enabled { get; set; }
        public double DamageWindowSeconds { get; set; }
        public int MinimumHitsForBurstCheck { get; set; }
        public double MaximumDamagePerWindow { get; set; }
        public int MinimumHitsForHeadshotCheck { get; set; }
        public double MaximumHeadshotHitRatio { get; set; }
        public double DamageBurstViolationScore { get; set; }
        public double HeadshotHitViolationScore { get; set; }
        public double KillWindowSeconds { get; set; }
        public int MinimumKillsForBurstCheck { get; set; }
        public int MaximumKillsPerWindow { get; set; }
        public int MinimumKillsForHeadshotCheck { get; set; }
        public double MaximumHeadshotRatio { get; set; }
        public double KillBurstViolationScore { get; set; }
        public double HeadshotBurstViolationScore { get; set; }
        public double CooldownSeconds { get; set; }
        public List<CombatWeaponProfileSettings> WeaponProfiles { get; set; }
        public List<CombatDistanceProfileSettings> DistanceProfiles { get; set; }
        public List<CombatWeaponOverrideSettings> WeaponOverrides { get; set; }
    }

    public class AbuseDetectionSettings
    {
        public bool Enabled { get; set; }
        public double ChatWindowSeconds { get; set; }
        public int MaximumMessagesPerWindow { get; set; }
        public double ChatViolationScore { get; set; }
        public double CooldownSeconds { get; set; }
    }

    public enum CombatWeaponType
    {
        Default,
        Automatic,
        Precision,
        Sniper,
        Shotgun
    }

    public class CombatWeaponProfileSettings
    {
        public CombatWeaponType WeaponType { get; set; }
        public double DamageThresholdMultiplier { get; set; } = 1d;
        public double HeadshotRatioThresholdMultiplier { get; set; } = 1d;
    }

    public class CombatDistanceProfileSettings
    {
        public string Name { get; set; }
        public double MinimumDistanceMeters { get; set; }
        public double MaximumDistanceMeters { get; set; }
        public double DamageThresholdMultiplier { get; set; } = 1d;
        public double HeadshotRatioThresholdMultiplier { get; set; } = 1d;
    }

    public class CombatWeaponOverrideSettings
    {
        public string WeaponGuid { get; set; }
        public string WeaponName { get; set; }
        public bool IgnoreDistanceProfile { get; set; }
        public int MinimumHitsForBurstCheck { get; set; }
        public int MinimumHitsForHeadshotCheck { get; set; }
        public double MaximumDamagePerWindow { get; set; }
        public double MaximumHeadshotHitRatio { get; set; }
        public double DamageThresholdMultiplier { get; set; } = 1d;
        public double HeadshotRatioThresholdMultiplier { get; set; } = 1d;
    }
}
