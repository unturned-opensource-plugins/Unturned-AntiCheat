namespace Emqo.Unturned_AntiCheat
{
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

    public static class MovementDetectionDefaults
    {
        public static MovementDetectionSettings CreateSettings()
        {
            return new MovementDetectionSettings
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
        }
    }
}
