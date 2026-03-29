using System;
using System.Collections.Generic;
using Emqo.Unturned_AntiCheat.Models;

namespace Emqo.Unturned_AntiCheat.Services
{
    public class VehicleDetector
    {
        private const string SpeedDetectorId = "vehicle.speed";
        private const string SustainedSpeedDetectorId = "vehicle.speed_sustained";
        private const string TeleportDetectorId = "vehicle.teleport";
        private const string AccelerationDetectorId = "vehicle.acceleration";

        private VehicleDetectionSettings _settings;

        public VehicleDetector(VehicleDetectionSettings settings)
        {
            _settings = settings;
        }

        public void UpdateSettings(VehicleDetectionSettings settings)
        {
            _settings = settings;
        }

        public IReadOnlyList<ViolationEvent> Analyze(PlayerSession session, Position3 position, DateTime nowUtc, VehicleSampleContext context)
        {
            var violations = new List<ViolationEvent>();
            if (!_settings.Enabled || context == null || !context.IsDriver)
            {
                ResetTracking(session, position, nowUtc, null);
                return violations;
            }

            if (session.LastVehicleInstanceId != context.VehicleInstanceId ||
                !session.LastVehiclePosition.HasValue ||
                !session.LastVehiclePositionUtc.HasValue)
            {
                ResetTracking(session, position, nowUtc, context.VehicleInstanceId);
                return violations;
            }

            var elapsed = nowUtc - session.LastVehiclePositionUtc.Value;
            if (elapsed.TotalMilliseconds < _settings.MinimumSampleMilliseconds)
            {
                return violations;
            }

            var delta = position - session.LastVehiclePosition.Value;
            var horizontalDistance = delta.HorizontalMagnitude;
            var horizontalSpeed = horizontalDistance / Math.Max(elapsed.TotalSeconds, 0.001d);
            var vehicleOverride = ResolveVehicleOverride(context.VehicleGuid);
            var resolvedVehicleClass = vehicleOverride?.VehicleClassOverride ?? context.VehicleClass;
            var classProfile = ResolveVehicleClassProfile(resolvedVehicleClass);
            var minimumReferenceSpeed = vehicleOverride?.MinimumReferenceSpeedMetersPerSecond ?? _settings.MinimumReferenceSpeedMetersPerSecond;
            var instantaneousSpeedMultiplier = vehicleOverride?.InstantaneousSpeedMultiplier ?? _settings.InstantaneousSpeedMultiplier;
            var sustainedSpeedMultiplier = vehicleOverride?.SustainedSpeedMultiplier ?? _settings.SustainedSpeedMultiplier;
            var teleportDistanceMultiplier = vehicleOverride?.TeleportDistanceMultiplier ?? _settings.TeleportDistanceMultiplier;
            var flatSpeedGraceMetersPerSecond = vehicleOverride?.FlatSpeedGraceMetersPerSecond ?? _settings.FlatSpeedGraceMetersPerSecond;
            var flatTeleportGraceMeters = vehicleOverride?.FlatTeleportGraceMeters ?? _settings.FlatTeleportGraceMeters;
            var absoluteTeleportDistanceMeters = vehicleOverride?.AbsoluteTeleportDistanceMeters ?? _settings.AbsoluteTeleportDistanceMeters;
            var maximumAccelerationMetersPerSecondSquared = vehicleOverride?.MaximumAccelerationMetersPerSecondSquared ?? _settings.MaximumAccelerationMetersPerSecondSquared;
            var speedViolationScore = vehicleOverride?.SpeedViolationScore ?? _settings.SpeedViolationScore * classProfile.SpeedViolationScoreMultiplier;
            var sustainedSpeedViolationScore = vehicleOverride?.SustainedSpeedViolationScore ?? _settings.SustainedSpeedViolationScore * classProfile.SustainedSpeedViolationScoreMultiplier;
            var teleportViolationScore = vehicleOverride?.TeleportViolationScore ?? _settings.TeleportViolationScore * classProfile.TeleportViolationScoreMultiplier;
            var accelerationViolationScore = vehicleOverride?.AccelerationViolationScore ?? _settings.AccelerationViolationScore * classProfile.AccelerationViolationScoreMultiplier;
            var referenceSpeed = Math.Max(
                minimumReferenceSpeed * classProfile.ReferenceSpeedMultiplier,
                Math.Max(context.TargetForwardSpeedMetersPerSecond, context.TargetReverseSpeedMetersPerSecond));
            var instantaneousThreshold = referenceSpeed * instantaneousSpeedMultiplier * classProfile.InstantaneousSpeedMultiplier +
                                         flatSpeedGraceMetersPerSecond * classProfile.FlatSpeedGraceMultiplier;
            var sustainedThreshold = referenceSpeed * sustainedSpeedMultiplier * classProfile.SustainedSpeedMultiplier +
                                     flatSpeedGraceMetersPerSecond * classProfile.FlatSpeedGraceMultiplier;
            var teleportThreshold = Math.Max(
                absoluteTeleportDistanceMeters * classProfile.AbsoluteTeleportDistanceMultiplier,
                referenceSpeed * elapsed.TotalSeconds * teleportDistanceMultiplier * classProfile.TeleportDistanceMultiplier +
                flatTeleportGraceMeters * classProfile.FlatTeleportGraceMultiplier);
            var accelerationThreshold = maximumAccelerationMetersPerSecondSquared * classProfile.MaximumAccelerationMultiplier;

            TrimSamples(session.VehicleSpeedSamples, nowUtc, TimeSpan.FromSeconds(_settings.SustainedWindowSeconds));
            session.VehicleSpeedSamples.Enqueue(new SamplePoint<double>(nowUtc, horizontalSpeed));

            if (horizontalDistance >= teleportThreshold &&
                IsOffCooldown(session, TeleportDetectorId, nowUtc, _settings.CooldownSeconds))
            {
                violations.Add(CreateViolation(
                    session,
                    TeleportDetectorId,
                    "vehicle",
                    $"Vehicle teleport-like movement detected: {horizontalDistance:F1}m in {elapsed.TotalSeconds:F2}s.",
                    teleportViolationScore,
                    nowUtc,
                    context,
                    ("vehicle_class", resolvedVehicleClass.ToString()),
                    ("movement_mode", "vehicle"),
                    ("horizontal_distance", horizontalDistance.ToString("F2")),
                    ("sample_seconds", elapsed.TotalSeconds.ToString("F2")),
                    ("teleport_threshold", teleportThreshold.ToString("F2")),
                    ("vehicle_override", vehicleOverride?.VehicleName ?? string.Empty),
                    ("reference_speed", referenceSpeed.ToString("F2"))));
            }

            var sustainedAverage = GetAverage(session.VehicleSpeedSamples);
            if (horizontalSpeed >= instantaneousThreshold &&
                IsOffCooldown(session, SpeedDetectorId, nowUtc, _settings.CooldownSeconds))
            {
                violations.Add(CreateViolation(
                    session,
                    SpeedDetectorId,
                    "vehicle",
                    $"Vehicle speed anomaly detected: current {horizontalSpeed:F1}m/s versus threshold {instantaneousThreshold:F1}m/s.",
                    speedViolationScore,
                    nowUtc,
                    context,
                    ("vehicle_class", resolvedVehicleClass.ToString()),
                    ("movement_mode", "vehicle"),
                    ("current_speed", horizontalSpeed.ToString("F2")),
                    ("speed_threshold", instantaneousThreshold.ToString("F2")),
                    ("vehicle_override", vehicleOverride?.VehicleName ?? string.Empty),
                    ("reference_speed", referenceSpeed.ToString("F2")),
                    ("sustained_speed", sustainedAverage.ToString("F2")),
                    ("sustained_threshold", sustainedThreshold.ToString("F2"))));
            }

            if (sustainedAverage >= sustainedThreshold &&
                IsOffCooldown(session, SustainedSpeedDetectorId, nowUtc, _settings.CooldownSeconds))
            {
                violations.Add(CreateViolation(
                    session,
                    SustainedSpeedDetectorId,
                    "vehicle",
                    $"Sustained vehicle speed anomaly detected: {sustainedAverage:F1}m/s versus threshold {sustainedThreshold:F1}m/s.",
                    sustainedSpeedViolationScore,
                    nowUtc,
                    context,
                    ("vehicle_class", resolvedVehicleClass.ToString()),
                    ("movement_mode", "vehicle"),
                    ("current_speed", horizontalSpeed.ToString("F2")),
                    ("sustained_speed", sustainedAverage.ToString("F2")),
                    ("sustained_threshold", sustainedThreshold.ToString("F2")),
                    ("vehicle_override", vehicleOverride?.VehicleName ?? string.Empty),
                    ("reference_speed", referenceSpeed.ToString("F2"))));
            }

            if (session.LastVehicleSpeedMetersPerSecond.HasValue)
            {
                var acceleration = Math.Abs(horizontalSpeed - session.LastVehicleSpeedMetersPerSecond.Value) /
                                   Math.Max(elapsed.TotalSeconds, 0.001d);
                if (acceleration >= accelerationThreshold &&
                    IsOffCooldown(session, AccelerationDetectorId, nowUtc, _settings.CooldownSeconds))
                {
                    violations.Add(CreateViolation(
                        session,
                        AccelerationDetectorId,
                        "vehicle",
                        $"Vehicle acceleration anomaly detected: {acceleration:F1}m/s^2.",
                        accelerationViolationScore,
                        nowUtc,
                        context,
                        ("vehicle_class", resolvedVehicleClass.ToString()),
                        ("movement_mode", "vehicle"),
                        ("current_speed", horizontalSpeed.ToString("F2")),
                        ("previous_speed", session.LastVehicleSpeedMetersPerSecond.Value.ToString("F2")),
                        ("acceleration", acceleration.ToString("F2")),
                        ("acceleration_threshold", accelerationThreshold.ToString("F2")),
                        ("vehicle_override", vehicleOverride?.VehicleName ?? string.Empty)));
                }
            }

            session.LastVehiclePosition = position;
            session.LastVehiclePositionUtc = nowUtc;
            session.LastVehicleSpeedMetersPerSecond = horizontalSpeed;
            session.LastVehicleInstanceId = context.VehicleInstanceId;
            return violations;
        }

        public void ResetTracking(PlayerSession session, Position3 position, DateTime nowUtc, int? vehicleInstanceId)
        {
            session.LastVehiclePosition = position;
            session.LastVehiclePositionUtc = nowUtc;
            session.LastVehicleSpeedMetersPerSecond = null;
            session.LastVehicleInstanceId = vehicleInstanceId;
            session.VehicleSpeedSamples.Clear();
        }

        private static void TrimSamples(Queue<SamplePoint<double>> samples, DateTime nowUtc, TimeSpan window)
        {
            while (samples.Count > 0 && nowUtc - samples.Peek().TimestampUtc > window)
            {
                samples.Dequeue();
            }
        }

        private static double GetAverage(IEnumerable<SamplePoint<double>> samples)
        {
            var total = 0d;
            var count = 0;
            foreach (var sample in samples)
            {
                total += sample.Value;
                count++;
            }

            return count == 0 ? 0d : total / count;
        }

        private VehicleOverrideSettings ResolveVehicleOverride(string vehicleGuid)
        {
            var normalizedGuid = NormalizeGuid(vehicleGuid);
            if (string.IsNullOrEmpty(normalizedGuid))
            {
                return null;
            }

            return _settings.VehicleOverrides?.Find(x => NormalizeGuid(x?.VehicleGuid) == normalizedGuid);
        }

        private VehicleClassProfileSettings ResolveVehicleClassProfile(VehicleDetectionClass vehicleClass)
        {
            var exactMatch = _settings.VehicleClassProfiles?.Find(x => x != null && x.VehicleClass == vehicleClass);
            if (exactMatch != null)
            {
                return exactMatch;
            }

            if (vehicleClass == VehicleDetectionClass.Helicopter || vehicleClass == VehicleDetectionClass.Plane)
            {
                var legacyAirProfile = _settings.VehicleClassProfiles?.Find(x => x != null && x.VehicleClass == VehicleDetectionClass.Air);
                if (legacyAirProfile != null)
                {
                    return legacyAirProfile;
                }
            }

            return new VehicleClassProfileSettings { VehicleClass = vehicleClass };
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
            VehicleSampleContext context,
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

            violation.Metadata["vehicle_guid"] = context.VehicleGuid;
            violation.Metadata["vehicle_label"] = context.VehicleLabel;
            violation.Metadata["vehicle_instance_id"] = context.VehicleInstanceId.ToString();
            violation.Metadata["vehicle_target_forward_speed"] = context.TargetForwardSpeedMetersPerSecond.ToString("F2");
            violation.Metadata["vehicle_target_reverse_speed"] = context.TargetReverseSpeedMetersPerSecond.ToString("F2");

            foreach (var item in metadata)
            {
                violation.Metadata[item.Key] = item.Value;
            }

            return violation;
        }
    }

    public class VehicleSampleContext
    {
        public int VehicleInstanceId { get; set; }
        public string VehicleGuid { get; set; }
        public string VehicleLabel { get; set; }
        public double TargetForwardSpeedMetersPerSecond { get; set; }
        public double TargetReverseSpeedMetersPerSecond { get; set; }
        public bool IsDriver { get; set; }
        public VehicleDetectionClass VehicleClass { get; set; }
    }
}
