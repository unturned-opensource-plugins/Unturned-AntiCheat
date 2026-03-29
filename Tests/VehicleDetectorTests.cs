using System;
using System.Linq;
using Emqo.Unturned_AntiCheat.Models;
using Emqo.Unturned_AntiCheat.Services;

namespace Emqo.Unturned_AntiCheat.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            var tests = new (string Name, Action Run)[]
            {
                ("Guid override penalty scores", Analyze_UsesGuidOverridePenaltyScores),
                ("Plane class profile relaxes sustained threshold", Analyze_PlaneClassProfileRelaxesSustainedSpeedThreshold),
                ("Helicopter class profile relaxes acceleration threshold", Analyze_HelicopterClassProfileRelaxesAccelerationThreshold),
                ("Guid class override changes vehicle model", Analyze_GuidClassOverrideCanChangeAppliedVehicleModel),
                ("Guid override teleport and acceleration scores", Analyze_UsesGuidOverrideTeleportAndAccelerationScores),
                ("Movement detector works without Unity runtime", Analyze_MovementDetectorFlagsTeleportWithoutUnity)
            };

            var failed = 0;
            foreach (var test in tests)
            {
                try
                {
                    test.Run();
                    Console.WriteLine("[PASS] " + test.Name);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.Error.WriteLine("[FAIL] " + test.Name);
                    Console.Error.WriteLine(ex.Message);
                }
            }

            return failed == 0 ? 0 : 1;
        }

        private static VehicleDetectionSettings CreateVehicleSettings()
        {
            return VehicleDetectionDefaults.CreateSettings();
        }

        private static MovementDetectionSettings CreateMovementSettings()
        {
            return MovementDetectionDefaults.CreateSettings();
        }

        private static PlayerSession CreateTrackedSession(DateTime lastSampleUtc, int vehicleInstanceId, Position3 lastPosition, double? lastSpeedMetersPerSecond = null)
        {
            return new PlayerSession
            {
                SteamId = 76561198000000000,
                PlayerName = "tester",
                LastVehiclePosition = lastPosition,
                LastVehiclePositionUtc = lastSampleUtc,
                LastVehicleInstanceId = vehicleInstanceId,
                LastVehicleSpeedMetersPerSecond = lastSpeedMetersPerSecond
            };
        }

        private static PlayerSession CreateMovementSession(DateTime lastSampleUtc, Position3 lastPosition)
        {
            return new PlayerSession
            {
                SteamId = 76561198000000001,
                PlayerName = "runner",
                LastPosition = lastPosition,
                LastPositionUtc = lastSampleUtc
            };
        }

        private static VehicleSampleContext CreateContext(string vehicleGuid, VehicleDetectionClass vehicleClass, int vehicleInstanceId, double forwardSpeed)
        {
            return new VehicleSampleContext
            {
                VehicleGuid = vehicleGuid,
                VehicleLabel = "vehicle",
                VehicleClass = vehicleClass,
                VehicleInstanceId = vehicleInstanceId,
                TargetForwardSpeedMetersPerSecond = forwardSpeed,
                TargetReverseSpeedMetersPerSecond = 8d,
                IsDriver = true
            };
        }

        private static void Analyze_UsesGuidOverridePenaltyScores()
        {
            var settings = CreateVehicleSettings();
            settings.SustainedSpeedMultiplier = 99d;
            settings.VehicleOverrides.Add(new VehicleOverrideSettings
            {
                VehicleGuid = "01234567-89ab-cdef-0123-456789abcdef",
                VehicleName = "NitroCar",
                SpeedViolationScore = 77d
            });

            var detector = new VehicleDetector(settings);
            var nowUtc = DateTime.UtcNow;
            var session = CreateTrackedSession(nowUtc.AddSeconds(-1), 1, Position3.Zero);

            var violations = detector.Analyze(
                session,
                new Position3(40d, 0d, 0d),
                nowUtc,
                CreateContext("01234567-89ab-cdef-0123-456789abcdef", VehicleDetectionClass.Ground, 1, 20d));

            var speedViolation = Single(violations, x => x.DetectorId == "vehicle.speed");
            Equal(77d, speedViolation.Score, 0.001d, "speed score override");
            Equal("NitroCar", speedViolation.Metadata["vehicle_override"], "vehicle override metadata");
            Equal("Ground", speedViolation.Metadata["vehicle_class"], "vehicle class metadata");
        }

        private static void Analyze_PlaneClassProfileRelaxesSustainedSpeedThreshold()
        {
            var settings = CreateVehicleSettings();
            settings.InstantaneousSpeedMultiplier = 99d;

            var planeProfile = settings.VehicleClassProfiles.Single(x => x.VehicleClass == VehicleDetectionClass.Plane);
            planeProfile.SustainedSpeedMultiplier = 1.7d;
            planeProfile.FlatSpeedGraceMultiplier = 2d;

            var detector = new VehicleDetector(settings);
            var nowUtc = DateTime.UtcNow;

            var groundViolations = detector.Analyze(
                CreateTrackedSession(nowUtc.AddSeconds(-1), 2, Position3.Zero),
                new Position3(40d, 0d, 0d),
                nowUtc,
                CreateContext("10000000-0000-0000-0000-000000000001", VehicleDetectionClass.Ground, 2, 20d));

            var planeViolations = detector.Analyze(
                CreateTrackedSession(nowUtc.AddSeconds(-1), 3, Position3.Zero),
                new Position3(40d, 0d, 0d),
                nowUtc,
                CreateContext("10000000-0000-0000-0000-000000000002", VehicleDetectionClass.Plane, 3, 20d));

            True(groundViolations.Any(x => x.DetectorId == "vehicle.speed_sustained"), "ground profile should trigger sustained speed");
            True(planeViolations.All(x => x.DetectorId != "vehicle.speed_sustained"), "plane profile should suppress sustained speed");
        }

        private static void Analyze_HelicopterClassProfileRelaxesAccelerationThreshold()
        {
            var settings = CreateVehicleSettings();
            settings.InstantaneousSpeedMultiplier = 99d;
            settings.SustainedSpeedMultiplier = 99d;

            var helicopterProfile = settings.VehicleClassProfiles.Single(x => x.VehicleClass == VehicleDetectionClass.Helicopter);
            helicopterProfile.MaximumAccelerationMultiplier = 2d;

            var detector = new VehicleDetector(settings);
            var nowUtc = DateTime.UtcNow;

            var groundViolations = detector.Analyze(
                CreateTrackedSession(nowUtc.AddSeconds(-1), 30, Position3.Zero, 0d),
                new Position3(60d, 0d, 0d),
                nowUtc,
                CreateContext("11000000-0000-0000-0000-000000000001", VehicleDetectionClass.Ground, 30, 20d));

            var helicopterViolations = detector.Analyze(
                CreateTrackedSession(nowUtc.AddSeconds(-1), 31, Position3.Zero, 0d),
                new Position3(60d, 0d, 0d),
                nowUtc,
                CreateContext("11000000-0000-0000-0000-000000000002", VehicleDetectionClass.Helicopter, 31, 20d));

            True(groundViolations.Any(x => x.DetectorId == "vehicle.acceleration"), "ground profile should trigger acceleration");
            True(helicopterViolations.All(x => x.DetectorId != "vehicle.acceleration"), "helicopter profile should suppress acceleration");
        }

        private static void Analyze_GuidClassOverrideCanChangeAppliedVehicleModel()
        {
            var settings = CreateVehicleSettings();
            settings.InstantaneousSpeedMultiplier = 99d;
            settings.VehicleOverrides.Add(new VehicleOverrideSettings
            {
                VehicleGuid = "20000000-0000-0000-0000-000000000001",
                VehicleName = "WorkshopPlane",
                VehicleClassOverride = VehicleDetectionClass.Plane
            });

            var planeProfile = settings.VehicleClassProfiles.Single(x => x.VehicleClass == VehicleDetectionClass.Plane);
            planeProfile.SustainedSpeedMultiplier = 1.7d;
            planeProfile.FlatSpeedGraceMultiplier = 2d;

            var detector = new VehicleDetector(settings);
            var nowUtc = DateTime.UtcNow;

            var violations = detector.Analyze(
                CreateTrackedSession(nowUtc.AddSeconds(-1), 4, Position3.Zero),
                new Position3(40d, 0d, 0d),
                nowUtc,
                CreateContext("20000000-0000-0000-0000-000000000001", VehicleDetectionClass.Ground, 4, 20d));

            True(violations.All(x => x.DetectorId != "vehicle.speed_sustained"), "guid class override should use plane profile");
        }

        private static void Analyze_UsesGuidOverrideTeleportAndAccelerationScores()
        {
            var settings = CreateVehicleSettings();
            settings.AbsoluteTeleportDistanceMeters = 20d;
            settings.TeleportDistanceMultiplier = 0.5d;
            settings.FlatTeleportGraceMeters = 0d;
            settings.MaximumAccelerationMetersPerSecondSquared = 5d;
            settings.VehicleOverrides.Add(new VehicleOverrideSettings
            {
                VehicleGuid = "30000000-0000-0000-0000-000000000001",
                VehicleName = "RocketBoat",
                TeleportViolationScore = 61d,
                AccelerationViolationScore = 44d
            });

            var detector = new VehicleDetector(settings);
            var nowUtc = DateTime.UtcNow;
            var session = CreateTrackedSession(nowUtc.AddSeconds(-1), 5, Position3.Zero, 0d);

            var violations = detector.Analyze(
                session,
                new Position3(200d, 0d, 0d),
                nowUtc,
                CreateContext("30000000-0000-0000-0000-000000000001", VehicleDetectionClass.Boat, 5, 10d));

            var teleportViolation = Single(violations, x => x.DetectorId == "vehicle.teleport");
            var accelerationViolation = Single(violations, x => x.DetectorId == "vehicle.acceleration");

            Equal(61d, teleportViolation.Score, 0.001d, "teleport score override");
            Equal(44d, accelerationViolation.Score, 0.001d, "acceleration score override");
            Equal("RocketBoat", teleportViolation.Metadata["vehicle_override"], "teleport vehicle override metadata");
            Equal("Boat", teleportViolation.Metadata["vehicle_class"], "teleport vehicle class metadata");
        }

        private static void Analyze_MovementDetectorFlagsTeleportWithoutUnity()
        {
            var settings = CreateMovementSettings();
            settings.MaxHorizontalSpeedMetersPerSecond = 999d;
            settings.SustainedHorizontalSpeedMetersPerSecond = 999d;

            var detector = new MovementDetector(settings);
            var nowUtc = DateTime.UtcNow;
            var session = CreateMovementSession(nowUtc.AddSeconds(-1), Position3.Zero);

            var violations = detector.Analyze(session, new Position3(40d, 0d, 0d), nowUtc);
            var teleportViolation = Single(violations, x => x.DetectorId == "movement.teleport");

            Equal(28d, teleportViolation.Score, 0.001d, "movement teleport score");
            Equal("40.00", teleportViolation.Metadata["horizontal_distance"], "movement teleport distance metadata");
        }

        private static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void Equal(double expected, double actual, double tolerance, string message)
        {
            if (Math.Abs(expected - actual) > tolerance)
            {
                throw new InvalidOperationException($"{message}: expected {expected:F3}, actual {actual:F3}");
            }
        }

        private static void Equal(string expected, string actual, string message)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{message}: expected '{expected}', actual '{actual}'");
            }
        }

        private static T Single<T>(System.Collections.Generic.IEnumerable<T> items, Func<T, bool> predicate)
        {
            var matches = items.Where(predicate).ToList();
            if (matches.Count != 1)
            {
                throw new InvalidOperationException($"expected exactly one match, actual {matches.Count}");
            }

            return matches[0];
        }
    }
}
