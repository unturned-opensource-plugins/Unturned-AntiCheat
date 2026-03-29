using System.Collections.Generic;

namespace Emqo.Unturned_AntiCheat
{
    public class VehicleDetectionSettings
    {
        public bool Enabled { get; set; }
        public int MinimumSampleMilliseconds { get; set; }
        public double MinimumReferenceSpeedMetersPerSecond { get; set; }
        public double InstantaneousSpeedMultiplier { get; set; }
        public double SustainedSpeedMultiplier { get; set; }
        public double TeleportDistanceMultiplier { get; set; }
        public double FlatSpeedGraceMetersPerSecond { get; set; }
        public double FlatTeleportGraceMeters { get; set; }
        public double AbsoluteTeleportDistanceMeters { get; set; }
        public double MaximumAccelerationMetersPerSecondSquared { get; set; }
        public double SustainedWindowSeconds { get; set; }
        public double CooldownSeconds { get; set; }
        public double SpeedViolationScore { get; set; }
        public double SustainedSpeedViolationScore { get; set; }
        public double TeleportViolationScore { get; set; }
        public double AccelerationViolationScore { get; set; }
        public List<VehicleClassProfileSettings> VehicleClassProfiles { get; set; }
        public List<VehicleOverrideSettings> VehicleOverrides { get; set; }
    }

    public enum VehicleDetectionClass
    {
        Ground,
        Bicycle,
        Boat,
        Air,
        Helicopter,
        Plane,
        Tracked
    }

    public class VehicleClassProfileSettings
    {
        public VehicleDetectionClass VehicleClass { get; set; }
        public double ReferenceSpeedMultiplier { get; set; } = 1d;
        public double InstantaneousSpeedMultiplier { get; set; } = 1d;
        public double SustainedSpeedMultiplier { get; set; } = 1d;
        public double TeleportDistanceMultiplier { get; set; } = 1d;
        public double FlatSpeedGraceMultiplier { get; set; } = 1d;
        public double FlatTeleportGraceMultiplier { get; set; } = 1d;
        public double AbsoluteTeleportDistanceMultiplier { get; set; } = 1d;
        public double MaximumAccelerationMultiplier { get; set; } = 1d;
        public double SpeedViolationScoreMultiplier { get; set; } = 1d;
        public double SustainedSpeedViolationScoreMultiplier { get; set; } = 1d;
        public double TeleportViolationScoreMultiplier { get; set; } = 1d;
        public double AccelerationViolationScoreMultiplier { get; set; } = 1d;
    }

    public class VehicleOverrideSettings
    {
        public string VehicleGuid { get; set; }
        public string VehicleName { get; set; }
        public VehicleDetectionClass? VehicleClassOverride { get; set; }
        public double? MinimumReferenceSpeedMetersPerSecond { get; set; }
        public double? InstantaneousSpeedMultiplier { get; set; }
        public double? SustainedSpeedMultiplier { get; set; }
        public double? TeleportDistanceMultiplier { get; set; }
        public double? FlatSpeedGraceMetersPerSecond { get; set; }
        public double? FlatTeleportGraceMeters { get; set; }
        public double? AbsoluteTeleportDistanceMeters { get; set; }
        public double? MaximumAccelerationMetersPerSecondSquared { get; set; }
        public double? SpeedViolationScore { get; set; }
        public double? SustainedSpeedViolationScore { get; set; }
        public double? TeleportViolationScore { get; set; }
        public double? AccelerationViolationScore { get; set; }
    }

    public static class VehicleDetectionDefaults
    {
        public static VehicleDetectionSettings CreateSettings()
        {
            return new VehicleDetectionSettings
            {
                Enabled = true,
                MinimumSampleMilliseconds = 250,
                MinimumReferenceSpeedMetersPerSecond = 14d,
                InstantaneousSpeedMultiplier = 1.45d,
                SustainedSpeedMultiplier = 1.2d,
                TeleportDistanceMultiplier = 2.25d,
                FlatSpeedGraceMetersPerSecond = 6d,
                FlatTeleportGraceMeters = 18d,
                AbsoluteTeleportDistanceMeters = 140d,
                MaximumAccelerationMetersPerSecondSquared = 35d,
                SustainedWindowSeconds = 4d,
                CooldownSeconds = 10d,
                SpeedViolationScore = 12d,
                SustainedSpeedViolationScore = 10d,
                TeleportViolationScore = 20d,
                AccelerationViolationScore = 14d,
                VehicleClassProfiles = CreateClassProfiles(),
                VehicleOverrides = new List<VehicleOverrideSettings>()
            };
        }

        public static List<VehicleClassProfileSettings> CreateClassProfiles()
        {
            return new List<VehicleClassProfileSettings>
            {
                new VehicleClassProfileSettings { VehicleClass = VehicleDetectionClass.Ground },
                new VehicleClassProfileSettings
                {
                    VehicleClass = VehicleDetectionClass.Bicycle,
                    ReferenceSpeedMultiplier = 0.75d,
                    InstantaneousSpeedMultiplier = 0.9d,
                    SustainedSpeedMultiplier = 0.9d,
                    TeleportDistanceMultiplier = 0.9d,
                    FlatSpeedGraceMultiplier = 0.7d,
                    FlatTeleportGraceMultiplier = 0.8d,
                    AbsoluteTeleportDistanceMultiplier = 0.75d,
                    MaximumAccelerationMultiplier = 0.85d
                },
                new VehicleClassProfileSettings
                {
                    VehicleClass = VehicleDetectionClass.Boat,
                    InstantaneousSpeedMultiplier = 1.15d,
                    SustainedSpeedMultiplier = 1.1d,
                    TeleportDistanceMultiplier = 1.2d,
                    FlatSpeedGraceMultiplier = 1.25d,
                    FlatTeleportGraceMultiplier = 1.5d,
                    AbsoluteTeleportDistanceMultiplier = 1.2d,
                    MaximumAccelerationMultiplier = 1.2d
                },
                new VehicleClassProfileSettings
                {
                    VehicleClass = VehicleDetectionClass.Air,
                    ReferenceSpeedMultiplier = 1.1d,
                    InstantaneousSpeedMultiplier = 1.25d,
                    SustainedSpeedMultiplier = 1.2d,
                    TeleportDistanceMultiplier = 1.8d,
                    FlatSpeedGraceMultiplier = 1.8d,
                    FlatTeleportGraceMultiplier = 2.5d,
                    AbsoluteTeleportDistanceMultiplier = 2d,
                    MaximumAccelerationMultiplier = 1.75d,
                    SpeedViolationScoreMultiplier = 0.9d,
                    SustainedSpeedViolationScoreMultiplier = 0.9d
                },
                new VehicleClassProfileSettings
                {
                    VehicleClass = VehicleDetectionClass.Helicopter,
                    ReferenceSpeedMultiplier = 1.05d,
                    InstantaneousSpeedMultiplier = 1.2d,
                    SustainedSpeedMultiplier = 1.15d,
                    TeleportDistanceMultiplier = 2d,
                    FlatSpeedGraceMultiplier = 1.7d,
                    FlatTeleportGraceMultiplier = 3d,
                    AbsoluteTeleportDistanceMultiplier = 2.25d,
                    MaximumAccelerationMultiplier = 2.1d,
                    SpeedViolationScoreMultiplier = 0.9d,
                    SustainedSpeedViolationScoreMultiplier = 0.85d,
                    TeleportViolationScoreMultiplier = 0.9d,
                    AccelerationViolationScoreMultiplier = 0.85d
                },
                new VehicleClassProfileSettings
                {
                    VehicleClass = VehicleDetectionClass.Plane,
                    ReferenceSpeedMultiplier = 1.35d,
                    InstantaneousSpeedMultiplier = 1.35d,
                    SustainedSpeedMultiplier = 1.45d,
                    TeleportDistanceMultiplier = 1.6d,
                    FlatSpeedGraceMultiplier = 1.5d,
                    FlatTeleportGraceMultiplier = 2.1d,
                    AbsoluteTeleportDistanceMultiplier = 1.8d,
                    MaximumAccelerationMultiplier = 1.35d,
                    SpeedViolationScoreMultiplier = 0.95d,
                    SustainedSpeedViolationScoreMultiplier = 0.9d
                },
                new VehicleClassProfileSettings
                {
                    VehicleClass = VehicleDetectionClass.Tracked,
                    ReferenceSpeedMultiplier = 0.85d,
                    InstantaneousSpeedMultiplier = 0.95d,
                    SustainedSpeedMultiplier = 0.95d,
                    TeleportDistanceMultiplier = 1.05d,
                    FlatSpeedGraceMultiplier = 0.9d,
                    FlatTeleportGraceMultiplier = 1.1d,
                    MaximumAccelerationMultiplier = 0.8d,
                    SpeedViolationScoreMultiplier = 1.1d,
                    AccelerationViolationScoreMultiplier = 1.1d
                }
            };
        }
    }
}
