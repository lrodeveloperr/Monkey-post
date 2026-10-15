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
        public PitchSurface Surface = PitchSurface.LagosCage;

        // The monkey posts: tiny goals set into the end walls. No crossbar, but a shot
        // must be below knee height to count.
        public float GoalWidth = 1.8f;
        public float GoalHeight = 0.7f;
        public float PostRadius = 0.06f;
        public float PostRestitution = 0.55f;

        /// <summary>The keeper must stay in this box. Outfield players may not enter their own box.</summary>
        public float KeeperBoxDepth = 4f;
        public float KeeperBoxHalfWidth = 3.5f;

        public float MatchDurationSeconds = 240f;

        /// <summary>If level at full time, play on until someone scores (used for cup ties and 1v1).</summary>
        public bool GoldenGoalIfLevel;

        public float KickoffSeconds = 1f;
        public float GoalCelebrationSeconds = 2.5f;

        // Player movement: realistic momentum, tuned to stay responsive on a phone.
        public float JogSpeed = 5f;
        public float SprintSpeed = 7f;
        public float DribbleSpeedFactor = 0.92f;
        public float Acceleration = 10f;
        /// <summary>Acceleration above jogging speed (it takes longer to reach full sprint).</summary>
        public float SprintAcceleration = 5f;
        public float Deceleration = 14f;
        /// <summary>Turn rate when nearly still, radians per second.</summary>
        public float TurnRateStill = 14f;
        /// <summary>Turn rate at full sprint: sprinting players take wide turns.</summary>
        public float TurnRateSprint = 5f;
        /// <summary>Asked to turn further than this at speed, a player plants and brakes first.</summary>
        public float PlantTurnAngle = 1.9f;
        public float StaminaDrainPerSecond = 0.25f;
        public float StaminaRegenPerSecond = 0.15f;

        // Player bodies (vertical cylinders) for player contact and ball deflections.
        public float PlayerRadius = 0.35f;
        public float BodyRadius = 0.3f;
        public float KeeperBodyRadius = 0.42f;
        public float BodyHeight = 1.8f;
        public float BodyRestitution = 0.3f;
        public float BodyFriction = 0.25f;
        public float PlayerCollisionRestitution = 0.2f;

        // Ball: a size 5 football.
        public float BallRadius = 0.11f;
        public float BallMass = 0.43f;
        public float AirDensity = 1.2f;
        public float DragCoefficientSlow = 0.45f;
        public float DragCoefficientFast = 0.22f;
        public float DragCrisisStartSpeed = 9f;
        public float DragCrisisEndSpeed = 15f;
        public float LiftCoefficientMax = 0.35f;
        public float SpinDecaySeconds = 5f;
        public float Gravity = 9.81f;
        /// <summary>Landing slower than this, the ball settles instead of bouncing.</summary>
        public float BounceThreshold = 0.6f;
        public float WallFriction = 0.4f;

        // Ball control. Feet only, so nobody plays the ball above roughly thigh height.
        public float ControlReach = 0.75f;
        public float KeeperControlReach = 0.9f;
        public float ControlHeight = 0.9f;
        /// <summary>Faster than this (relative to the player), the ball deflects off them instead.</summary>
        public float ControlMaxSpeed = 15f;
        /// <summary>First touches are clean up to this incoming speed; faster balls are harder to kill.</summary>
        public float FirstTouchCleanSpeed = 6f;
        public float FirstTouchErrorPerSpeed = 0.06f;

        // Dribbling: the ball really leaves the foot on each touch.
        public float TouchReach = 0.8f;
        public float DribbleLeadJog = 0.6f;
        public float DribbleLeadSprint = 1.5f;
        public float TouchCooldownSeconds = 0.15f;
        /// <summary>0..1: how much the game helps keep the ball close (the touch assist).</summary>
        public float DribbleAssist = 1f;
        /// <summary>A carrier further than this from the ball has lost it.</summary>
        public float LoseBallDistance = 2.2f;

        // Kicking.
        public float KickReach = 0.85f;
        public float KickHeight = 0.75f;
        public float KickCooldownSeconds = 0.3f;
        /// <summary>A pass or shot pressed while the ball is just out of reach is kept this long.</summary>
        public float PendingKickSeconds = 0.4f;
        public float PassArrivalSpeed = 5f;
        public float PassMinSpeed = 6f;
        public float PassMaxSpeed = 22f;
        /// <summary>Half-angle of the cone searched for a teammate when passing.</summary>
        public float PassConeRadians = 0.8f;
        public float ShotMinSpeed = 12f;
        public float ShotMaxSpeed = 26f;
        /// <summary>Upward speed of a full-power shot. Too much power and it flies over the posts.</summary>
        public float ShotMaxLift = 6.5f;
        /// <summary>Topspin on a full-power shot (rad/s): makes it dip.</summary>
        public float ShotTopspin = 35f;
        /// <summary>Sidespin on a soft shot aimed at a post (rad/s): it curls in.</summary>
        public float ShotCurlSpin = 60f;
        public float ShotSpreadBase = 0.02f;
        public float ShotSpreadPerPower = 0.05f;
        /// <summary>Extra spread for a shot struck off balance (facing away from the target).</summary>
        public float OffBalanceSpread = 0.1f;

        // Tackling. Street rules: no fouls.
        public float TackleRange = 1.2f;
        public float TackleSuccessChance = 0.7f;
        public float TackleCooldownSeconds = 1f;
        public float TackleMissStunSeconds = 0.7f;
        public float TackledStunSeconds = 0.4f;
        public float TackleKnockSpeed = 5f;

        // Feet-only keeper: can stretch a leg to block.
        public float KeeperStretchSeconds = 0.35f;
        public float KeeperStretchReach = 0.75f;
        public float KeeperFootRadius = 0.25f;

        public SurfaceProfile SurfaceProfile => SurfaceProfile.For(Surface);

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
            if (BallRadius <= 0f || BallMass <= 0f)
                throw new ArgumentException("Ball radius and mass must be positive.");
        }
    }
}
