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

        // Player movement, from sports-science sprint data (see docs/PHYSICS.md). Street
        // footballers: a little below professionals' ~9.2–9.5 m/s top speed.
        public float JogSpeed = 5.5f;
        public float SprintSpeed = 8f;
        public float DribbleSpeedFactor = 0.9f;
        /// <summary>
        /// Sprint time constant τ of the mono-exponential sprint model v(t) = vmax(1 − e^(−t/τ)):
        /// the time to reach 63% of top speed. Initial acceleration is SprintSpeed / τ (≈7 m/s²).
        /// </summary>
        public float SprintTau = 1.15f;
        /// <summary>Maximum braking deceleration, m/s².</summary>
        public float Deceleration = 6.5f;
        /// <summary>
        /// Total horizontal acceleration the feet can put down (the friction circle), shared between
        /// speeding up, braking and turning. ~0.8 g: curved sprints run on 7–11 m radii near top speed.
        /// </summary>
        public float GripAcceleration = 8f;
        /// <summary>How fast a nearly-still player can swivel, radians per second.</summary>
        public float TurnRateStill = 12f;
        /// <summary>Asked to turn further than this at speed, a player plants and brakes first.</summary>
        public float PlantTurnAngle = 1.75f;
        /// <summary>A full stamina bar lasts ~6.5 s of flat-out sprinting (maximal sprint ability).</summary>
        public float StaminaDrainPerSecond = 0.15f;
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
        /// <summary>Drag coefficient below the drag crisis (laminar boundary layer).</summary>
        public float DragCoefficientSlow = 0.45f;
        /// <summary>Drag coefficient above the drag crisis (turbulent boundary layer).</summary>
        public float DragCoefficientFast = 0.2f;
        /// <summary>The drag crisis is centred near 12 m/s for a football (Re ≈ 1.7×10⁵).</summary>
        public float DragCrisisStartSpeed = 10f;
        public float DragCrisisEndSpeed = 14f;
        /// <summary>Spin raises drag: ΔCd = SpinDragFactor × S, capped (S = rω/v, the spin parameter).</summary>
        public float SpinDragFactor = 0.3f;
        public float SpinDragMax = 0.12f;
        /// <summary>
        /// Lift (Magnus) coefficient fit: Cl = S / (LiftFitA·S + LiftFitB). Gives Cl ≈ 0.25 at the
        /// S ≈ 0.22 of a typical curled free kick, saturating near 0.4 at high spin (Goff & Carré).
        /// </summary>
        public float LiftFitA = 2.32f;
        public float LiftFitB = 0.4f;
        public float SpinDecaySeconds = 5f;
        public float Gravity = 9.81f;
        /// <summary>Landing slower than this, the ball settles instead of bouncing.</summary>
        public float BounceThreshold = 0.6f;
        /// <summary>
        /// Restitution falls as impact speed rises (the ball deforms more): each m/s above
        /// RestitutionReferenceSpeed loses this fraction, down to RestitutionFloor.
        /// </summary>
        public float RestitutionSpeedLoss = 0.008f;
        public float RestitutionReferenceSpeed = 6.3f;
        public float RestitutionFloor = 0.6f;
        public float WallFriction = 0.4f;

        // Ball control. Feet only, so nobody plays the ball above roughly thigh height.
        public float ControlReach = 0.75f;
        public float KeeperControlReach = 0.8f;
        /// <summary>
        /// Above this relative speed a player can't reach out for a clean touch: the reach for
        /// control shrinks linearly to half at ControlMaxSpeed (wider balls must be blocked).
        /// </summary>
        public float FullReachControlSpeed = 8f;
        public float ControlHeight = 0.9f;
        /// <summary>Faster than this (relative to the player), the ball deflects off them instead.</summary>
        public float ControlMaxSpeed = 15f;
        /// <summary>First touches are clean up to this incoming speed; faster balls are harder to kill.</summary>
        public float FirstTouchCleanSpeed = 6f;
        public float FirstTouchErrorPerSpeed = 0.06f;

        // Dribbling: the ball really leaves the foot on each touch.
        public float TouchReach = 0.8f;
        public float DribbleLeadJog = 0.6f;
        public float DribbleLeadSprint = 2.2f;
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

        // Skill moves.
        public float SkillCooldownSeconds = 0.8f;
        /// <summary>A skill move counts as a success if it beats the defender and keeps the ball within this long.</summary>
        public float SkillJudgeSeconds = 2f;
        /// <summary>Earliest moment a drag-back or step-over can be judged (it must actually shake the defender off).</summary>
        public float SkillMinSeconds = 0.5f;
        /// <summary>A nutmeg is a knock just past the defender, not a long ball.</summary>
        public float NutmegSpeed = 4.5f;
        /// <summary>A defender's legs are open (nutmeg-able) when moving faster than this, lunging or stretching.</summary>
        public float LegsOpenSpeed = 2.5f;
        /// <summary>Weight shift forced on a wrong-footed defender by a step-over, m/s.</summary>
        public float StepOverShove = 2.2f;
        public float SkillRange = 2.5f;

        // Shielding (automatic).
        public float ShieldRange = 1.6f;
        public float ShieldMaxSpeed = 3f;
        /// <summary>Tackles from the far side of a shielding carrier succeed this much less often.</summary>
        public float ShieldTackleFactor = 0.45f;

        // Through balls.
        public float ThroughBallMinLead = 3f;
        public float ThroughBallMaxLead = 7f;

        /// <summary>Per-team physical edges (opponent strength). Defaults are 1 (no edge).</summary>
        public TeamTraits HomeTraits = new TeamTraits();
        public TeamTraits AwayTraits = new TeamTraits();

        public TeamTraits Traits(int team) => team == 0 ? HomeTraits : AwayTraits;

        public SurfaceProfile SurfaceProfile => SurfaceProfile.For(Surface);

        /// <summary>A copy, so a match can adjust traits without touching the caller's config.</summary>
        public MatchConfig Clone()
        {
            var copy = (MatchConfig)MemberwiseClone();
            copy.HomeTraits = HomeTraits.Clone();
            copy.AwayTraits = AwayTraits.Clone();
            return copy;
        }

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

    /// <summary>
    /// Small physical edges that make stronger teams stronger: a few percent of pace, tackling and
    /// touch. 1 means no edge. Never applied to a person's own team (no stats, no pay-to-win).
    /// </summary>
    [Serializable]
    public sealed class TeamTraits
    {
        /// <summary>Multiplies jogging and sprinting speed.</summary>
        public float Pace = 1f;
        /// <summary>Multiplies tackle success.</summary>
        public float Tackling = 1f;
        /// <summary>Divides first-touch and dribble-touch errors.</summary>
        public float Touch = 1f;

        public TeamTraits Clone() => (TeamTraits)MemberwiseClone();
    }
}
