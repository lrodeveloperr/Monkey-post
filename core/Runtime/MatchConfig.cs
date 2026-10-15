using System;

namespace MonkeyPost.Core
{
    /// <summary>
    /// Tunable rules and physics for a match. Distances in metres, speeds in m/s, times in seconds.
    /// The home team defends the goal at X = 0; the away team defends X = PitchLength.
    /// </summary>
    [Serializable]
    public sealed class MatchConfig
    {
        // Pitch: a walled cage, so the ball never goes out of play.
        public float PitchLength = 36f;
        public float PitchWidth = 20f;

        // The monkey posts: tiny goals set into the end walls. No crossbar, but a shot
        // must be below knee height to count.
        public float GoalWidth = 1.8f;
        public float GoalHeight = 0.7f;

        /// <summary>The keeper must stay in this box. Outfield players may not enter their own box.</summary>
        public float KeeperBoxDepth = 4f;
        public float KeeperBoxHalfWidth = 3.5f;

        public float MatchDurationSeconds = 240f;

        /// <summary>If level at full time, play on until someone scores (used for cup ties).</summary>
        public bool GoldenGoalIfLevel;
        public float KickoffSeconds = 1f;
        public float GoalCelebrationSeconds = 2.5f;

        // Players.
        public float PlayerRadius = 0.4f;
        public float JogSpeed = 5f;
        public float SprintSpeed = 7f;
        public float DribbleSpeedFactor = 0.85f;
        public float Acceleration = 25f;
        public float StaminaDrainPerSecond = 0.25f;
        public float StaminaRegenPerSecond = 0.15f;

        // Ball.
        public float BallRadius = 0.11f;
        public float DribbleOffset = 0.55f;
        public float ControlRadius = 0.6f;
        public float KeeperBlockRadius = 0.45f;
        /// <summary>Highest ball anyone can play: feet only, so roughly thigh height.</summary>
        public float ControlHeight = 0.9f;
        /// <summary>Faster than this, a ball reaching a player is deflected instead of controlled.</summary>
        public float ControlMaxSpeed = 15f;
        public float GroundFriction = 2.5f;
        public float AirDrag = 0.3f;
        public float Gravity = 9.81f;
        public float GroundRestitution = 0.45f;
        public float WallRestitution = 0.7f;

        // Kicking.
        public float KickCooldownSeconds = 0.3f;
        public float PassMinSpeed = 7f;
        public float PassMaxSpeed = 18f;
        public float PassSpeedPerMetre = 0.7f;
        /// <summary>Half-angle of the cone searched for a teammate when passing.</summary>
        public float PassConeRadians = 0.8f;
        public float ShotMinSpeed = 12f;
        public float ShotMaxSpeed = 24f;
        /// <summary>Upward speed of a full-power shot. Too much power and it flies over the posts.</summary>
        public float ShotMaxLift = 4f;
        public float ShotSpreadBase = 0.02f;
        public float ShotSpreadPerPower = 0.06f;

        // Tackling. Street rules: no fouls.
        public float TackleRange = 1.3f;
        public float TackleSuccessChance = 0.55f;
        public float TackleCooldownSeconds = 1f;
        public float TackleMissStunSeconds = 0.7f;
        public float TackledStunSeconds = 0.4f;
        public float TackleKnockSpeed = 4f;

        public void Validate()
        {
            if (PitchLength <= 4f * KeeperBoxDepth || PitchWidth <= 2f * KeeperBoxHalfWidth)
                throw new ArgumentException("Pitch is too small for the keeper boxes.");
            if (GoalWidth <= 2f * BallRadius || GoalWidth >= 2f * KeeperBoxHalfWidth)
                throw new ArgumentException("Goal must be wider than the ball and narrower than the keeper box.", nameof(GoalWidth));
            if (MatchDurationSeconds <= 0f)
                throw new ArgumentException("Match duration must be positive.", nameof(MatchDurationSeconds));
            if (JogSpeed <= 0f || SprintSpeed < JogSpeed)
                throw new ArgumentException("Need 0 < JogSpeed <= SprintSpeed.", nameof(JogSpeed));
            if (ShotMinSpeed <= 0f || ShotMaxSpeed < ShotMinSpeed || PassMinSpeed <= 0f || PassMaxSpeed < PassMinSpeed)
                throw new ArgumentException("Kick speeds must be positive and ordered.");
        }
    }
}
