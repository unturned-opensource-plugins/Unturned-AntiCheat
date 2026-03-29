using System;
using System.Collections.Generic;
using Emqo.Unturned_AntiCheat.Models;
using UnityEngine;

namespace Emqo.Unturned_AntiCheat.Services
{
    public class MovementDetector
    {
        private const string SpeedDetectorId = "movement.speed";
        private const string VerticalDetectorId = "movement.vertical";
        private const string TeleportDetectorId = "movement.teleport";

        private MovementDetectionSettings _settings;

        public MovementDetector(MovementDetectionSettings settings)
        {
            _settings = settings;
        }

        public void UpdateSettings(MovementDetectionSettings settings)
        {
            _settings = settings;
        }

        public IReadOnlyList<ViolationEvent> Analyze(PlayerSession session, Vector3 position, DateTime nowUtc, bool isInVehicle)
        {
            var violations = new List<ViolationEvent>();
            if (!_settings.Enabled || isInVehicle)
            {
                session.LastPosition = position;
                session.LastPositionUtc = nowUtc;
                return violations;
            }

            if (!session.LastPosition.HasValue || !session.LastPositionUtc.HasValue)
            {
                session.LastPosition = position;
                session.LastPositionUtc = nowUtc;
                return violations;
            }

            var elapsed = nowUtc - session.LastPositionUtc.Value;
            if (elapsed.TotalMilliseconds < _settings.MinimumSampleMilliseconds)
            {
                return violations;
            }

            var delta = position - session.LastPosition.Value;
            var horizontalDistance = new Vector2(delta.x, delta.z).magnitude;
            var verticalDistance = Math.Abs(delta.y);
            var horizontalSpeed = horizontalDistance / Math.Max(elapsed.TotalSeconds, 0.001d);

            TrimSamples(session.SpeedSamples, nowUtc, TimeSpan.FromSeconds(_settings.SustainedWindowSeconds));
            session.SpeedSamples.Enqueue(new SamplePoint<double>(nowUtc, horizontalSpeed));

            if (horizontalDistance >= _settings.MaxTeleportDistanceMeters &&
                IsOffCooldown(session, TeleportDetectorId, nowUtc, _settings.CooldownSeconds))
            {
                violations.Add(CreateViolation(
                    session,
                    TeleportDetectorId,
                    "movement",
                    $"Teleport-like movement detected: {horizontalDistance:F1}m in {elapsed.TotalSeconds:F2}s.",
                    _settings.TeleportViolationScore,
                    nowUtc,
                    ("horizontal_distance", horizontalDistance.ToString("F2")),
                    ("sample_seconds", elapsed.TotalSeconds.ToString("F2"))));
            }

            if (verticalDistance >= _settings.MaxVerticalDeltaMeters &&
                IsOffCooldown(session, VerticalDetectorId, nowUtc, _settings.CooldownSeconds))
            {
                violations.Add(CreateViolation(
                    session,
                    VerticalDetectorId,
                    "movement",
                    $"Abnormal vertical delta detected: {verticalDistance:F1}m.",
                    _settings.VerticalViolationScore,
                    nowUtc,
                    ("vertical_distance", verticalDistance.ToString("F2")),
                    ("horizontal_distance", horizontalDistance.ToString("F2"))));
            }

            var sustainedAverage = GetAverage(session.SpeedSamples);
            if ((horizontalSpeed >= _settings.MaxHorizontalSpeedMetersPerSecond ||
                 sustainedAverage >= _settings.SustainedHorizontalSpeedMetersPerSecond) &&
                IsOffCooldown(session, SpeedDetectorId, nowUtc, _settings.CooldownSeconds))
            {
                violations.Add(CreateViolation(
                    session,
                    SpeedDetectorId,
                    "movement",
                    $"Abnormal movement speed detected: current {horizontalSpeed:F1}m/s, sustained {sustainedAverage:F1}m/s.",
                    _settings.SpeedViolationScore,
                    nowUtc,
                    ("current_speed", horizontalSpeed.ToString("F2")),
                    ("sustained_speed", sustainedAverage.ToString("F2"))));
            }

            session.LastPosition = position;
            session.LastPositionUtc = nowUtc;
            return violations;
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
