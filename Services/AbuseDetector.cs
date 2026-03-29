using System;
using System.Collections.Generic;
using Emqo.Unturned_AntiCheat.Models;

namespace Emqo.Unturned_AntiCheat.Services
{
    public class AbuseDetector
    {
        private const string ChatSpamDetectorId = "abuse.chat_spam";

        private AbuseDetectionSettings _settings;

        public AbuseDetector(AbuseDetectionSettings settings)
        {
            _settings = settings;
        }

        public void UpdateSettings(AbuseDetectionSettings settings)
        {
            _settings = settings;
        }

        public IReadOnlyList<ViolationEvent> RegisterChat(PlayerSession session, string message, DateTime nowUtc)
        {
            var violations = new List<ViolationEvent>();
            if (!_settings.Enabled || string.IsNullOrWhiteSpace(message))
            {
                return violations;
            }

            var window = TimeSpan.FromSeconds(_settings.ChatWindowSeconds);
            while (session.ChatSamples.Count > 0 && nowUtc - session.ChatSamples.Peek() > window)
            {
                session.ChatSamples.Dequeue();
            }

            session.ChatSamples.Enqueue(nowUtc);
            if (session.ChatSamples.Count > _settings.MaximumMessagesPerWindow &&
                IsOffCooldown(session, ChatSpamDetectorId, nowUtc, _settings.CooldownSeconds))
            {
                violations.Add(new ViolationEvent
                {
                    SteamId = session.SteamId,
                    PlayerName = session.PlayerName,
                    DetectorId = ChatSpamDetectorId,
                    Category = "abuse",
                    Summary = $"Chat spam detected: {session.ChatSamples.Count} messages in {_settings.ChatWindowSeconds:F0}s.",
                    Score = _settings.ChatViolationScore,
                    TimestampUtc = nowUtc,
                    Metadata = new Dictionary<string, string>
                    {
                        ["messages_in_window"] = session.ChatSamples.Count.ToString(),
                        ["last_message_length"] = message.Length.ToString()
                    }
                });
            }

            return violations;
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
    }
}
