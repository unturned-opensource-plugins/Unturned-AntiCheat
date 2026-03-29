using System;
using System.Collections.Generic;

namespace Emqo.Unturned_AntiCheat.Models
{
    public class PlayerSession
    {
        public ulong SteamId { get; set; }
        public string PlayerName { get; set; }
        public Position3? LastPosition { get; set; }
        public DateTime? LastPositionUtc { get; set; }
        public Queue<SamplePoint<double>> SpeedSamples { get; } = new Queue<SamplePoint<double>>();
        public Position3? LastVehiclePosition { get; set; }
        public DateTime? LastVehiclePositionUtc { get; set; }
        public double? LastVehicleSpeedMetersPerSecond { get; set; }
        public int? LastVehicleInstanceId { get; set; }
        public Queue<SamplePoint<double>> VehicleSpeedSamples { get; } = new Queue<SamplePoint<double>>();
        public Queue<CombatSample> DamageSamples { get; } = new Queue<CombatSample>();
        public Queue<CombatSample> HeadshotDamageSamples { get; } = new Queue<CombatSample>();
        public Queue<DateTime> KillSamples { get; } = new Queue<DateTime>();
        public Queue<DateTime> HeadshotKillSamples { get; } = new Queue<DateTime>();
        public Queue<DateTime> ChatSamples { get; } = new Queue<DateTime>();
        public Dictionary<string, DateTime> DetectorCooldownsUtc { get; } = new Dictionary<string, DateTime>();
    }

    public class SamplePoint<T>
    {
        public SamplePoint(DateTime timestampUtc, T value)
        {
            TimestampUtc = timestampUtc;
            Value = value;
        }

        public DateTime TimestampUtc { get; }
        public T Value { get; }
    }

    public class CombatSample
    {
        public CombatSample(DateTime timestampUtc, ulong victimSteamId, double damage, bool wasHeadshot)
        {
            TimestampUtc = timestampUtc;
            VictimSteamId = victimSteamId;
            Damage = damage;
            WasHeadshot = wasHeadshot;
        }

        public DateTime TimestampUtc { get; }
        public ulong VictimSteamId { get; }
        public double Damage { get; }
        public bool WasHeadshot { get; }
    }
}
