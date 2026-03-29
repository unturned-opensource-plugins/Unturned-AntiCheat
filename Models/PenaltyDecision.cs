namespace Emqo.Unturned_AntiCheat.Models
{
    public class PenaltyDecision
    {
        public PenaltyAction Action { get; set; }
        public string Reason { get; set; }
        public double CurrentScore { get; set; }
    }
}
