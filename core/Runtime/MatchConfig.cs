using System;

namespace MonkeyPost.Core
{
    /// <summary>
    /// Tunable rules for a match. Distances are measured in laps (1.0 = once around the ring)
    /// and speeds in laps per second.
    /// </summary>
    [Serializable]
    public sealed class MatchConfig
    {
        /// <summary>Total players in the ring, including the runner. Seats = PlayerCount - 1.</summary>
        public int PlayerCount = 6;

        public float MatchDurationSeconds = 180f;

        public float JogSpeed = 0.12f;
        public float SprintSpeed = 0.20f;
        public float ChaserSpeedMultiplier = 1.35f;

        public float StaminaDrainPerSecond = 0.35f;
        public float StaminaRegenPerSecond = 0.20f;

        /// <summary>If the runner still holds the post after this long, it drops automatically.</summary>
        public float DropDeadlineSeconds = 15f;

        public float FalseCheckCooldownSeconds = 3f;

        /// <summary>The chaser tags the runner when within this distance behind them.</summary>
        public float TagDistance = 0.02f;

        public float InterludeSeconds = 1.5f;

        public int PointsRunnerSafe = 3;
        public int PointsTag = 3;
        public int PointsPostedRunner = 5;
        public int PointsPostedTarget = -2;
        public int PointsFalseCheck = -1;

        public int SeatCount => PlayerCount - 1;

        public void Validate()
        {
            if (PlayerCount < 3)
                throw new ArgumentException("A ring needs at least 3 players.", nameof(PlayerCount));
            if (MatchDurationSeconds <= 0f)
                throw new ArgumentException("Match duration must be positive.", nameof(MatchDurationSeconds));
            if (JogSpeed <= 0f || SprintSpeed < JogSpeed)
                throw new ArgumentException("Need 0 < JogSpeed <= SprintSpeed.", nameof(JogSpeed));
            if (ChaserSpeedMultiplier <= 0f)
                throw new ArgumentException("Chaser multiplier must be positive.", nameof(ChaserSpeedMultiplier));
            if (TagDistance <= 0f || TagDistance >= 0.5f / SeatCount)
                throw new ArgumentException("Tag distance must be positive and smaller than half a seat gap.", nameof(TagDistance));
        }
    }
}
