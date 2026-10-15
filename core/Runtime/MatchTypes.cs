namespace MonkeyPost.Core
{
    public enum Phase
    {
        /// <summary>Everyone in position, whistle about to blow.</summary>
        Kickoff,
        Play,
        GoalScored,
        Ended,
    }

    /// <summary>Controls for one footballer for a single tick.</summary>
    public struct PlayerInput
    {
        /// <summary>Desired direction and speed, length 0..1.</summary>
        public Vec2 Move;
        public bool Sprint;

        /// <summary>Pass to the teammate in the <see cref="Move"/> direction (or straight ahead).</summary>
        public bool Pass;

        /// <summary>Shoot at the opponents' goal, using <see cref="Aim"/> and <see cref="Power"/>.</summary>
        public bool Shoot;

        /// <summary>Where across the goal mouth to aim: -1 one post, 0 the middle, +1 the other post.</summary>
        public float Aim;

        /// <summary>Shot power 0..1. Too much and the ball goes over knee height.</summary>
        public float Power;

        /// <summary>Outfield: tackle. Keeper: stretch a leg to block.</summary>
        public bool Tackle;

        /// <summary>Ask to control a different teammate. Handled by the controller, not the rules.</summary>
        public bool Switch;

        /// <summary>With <see cref="Pass"/>: play it into space ahead of a running teammate.</summary>
        public bool ThroughBall;

        /// <summary>
        /// A skill-move swipe, in pitch directions (zero for none). Relative to the way the carrier
        /// is dribbling: forward = nutmeg, back = drag-back, sideways = step-over to that side.
        /// </summary>
        public Vec2 Skill;
    }

    public enum SkillMove
    {
        None,
        /// <summary>Knock the ball through the defender's legs and run round him.</summary>
        Nutmeg,
        /// <summary>Roll the ball back under the sole and turn away.</summary>
        DragBack,
        /// <summary>Fake one way, go the other.</summary>
        StepOver,
    }

    public enum KickType
    {
        None,
        Pass,
        Shot,
    }

    public enum MatchEventType
    {
        Kickoff,
        Pass,
        Shot,
        Goal,
        Tackle,
        TackleMissed,
        Save,
        Block,
        /// <summary>The ball hit a monkey post.</summary>
        Post,
        /// <summary>The ball hit the cage wall hard (for sound).</summary>
        WallHit,
        /// <summary>Full time with the scores level in a cup tie: next goal wins.</summary>
        GoldenGoal,
        /// <summary>A skill move was attempted (Detail = <see cref="SkillMove"/>).</summary>
        SkillAttempt,
        /// <summary>A skill move beat its defender (PlayerA beat PlayerB; Detail = <see cref="SkillMove"/>).</summary>
        SkillBeat,
        MatchEnded,
    }

    public readonly struct MatchEvent
    {
        public readonly MatchEventType Type;
        /// <summary>The main player involved (passer, shooter, scorer, tackler, keeper), or -1.</summary>
        public readonly int PlayerA;
        /// <summary>The other player involved (receiver, tackled player), or -1.</summary>
        public readonly int PlayerB;
        /// <summary>The team involved (kicking off, scoring), or -1.</summary>
        public readonly int Team;
        /// <summary>Extra detail: the <see cref="SkillMove"/> for skill events; 1 for a through ball on Pass.</summary>
        public readonly int Detail;

        public MatchEvent(MatchEventType type, int playerA = -1, int playerB = -1, int team = -1, int detail = 0)
        {
            Type = type;
            PlayerA = playerA;
            PlayerB = playerB;
            Team = team;
            Detail = detail;
        }

        public override string ToString() => $"{Type}(a={PlayerA}, b={PlayerB}, team={Team}, detail={Detail})";
    }

    public sealed class PlayerState
    {
        public int Id;
        public int Team;
        /// <summary>0 is the keeper, 1–2 defenders, 3–4 attackers.</summary>
        public int Slot;
        public Vec2 Position;
        public Vec2 Velocity;
        public Vec2 Facing = new Vec2(1f, 0f);
        public float Stamina = 1f;
        public bool IsSprinting;
        public float StunTime;
        public float KickCooldown;
        public float TackleCooldown;
        public float TouchCooldown;

        /// <summary>The direction the player is asking to go this tick (touches follow it).</summary>
        public Vec2 DesiredMove;

        // A pass or shot waiting for the ball to come into reach.
        public KickType PendingKick;
        public bool PendingThrough;
        public float PendingAim;
        public float PendingPower;
        public Vec2 PendingDirection;
        public float PendingTime;

        // Keeper leg stretch.
        public float StretchTime;
        public Vec2 StretchDirection;

        // Skill moves.
        public float SkillCooldown;
        public SkillMove ActiveSkill;
        public int SkillDefender = -1;
        public float SkillTime;
        public bool SkillPassedDefender;
        /// <summary>After a step-over: the next touches go this way, for this long.</summary>
        public Vec2 FeintDirection;
        public float FeintTime;

        /// <summary>Holding off a close defender with the body (automatic for a slow carrier).</summary>
        public bool IsShielding;

        /// <summary>After a nutmeg: slipping round this defender (no body collision with them) for a moment.</summary>
        public int SlipPast = -1;
        public float SlipTime;

        public bool IsKeeper => Slot == 0;
    }

    /// <summary>The ball. Physically free at all times; <see cref="Owner"/> is who is dribbling it.</summary>
    public sealed class BallState
    {
        public BallBody Body;
        public float Radius = 0.11f;

        /// <summary>The player dribbling the ball, or -1 when it is loose.</summary>
        public int Owner = -1;

        /// <summary>Who the last pass was meant for (-1 if none), and where it was aimed.</summary>
        public int IntendedReceiver = -1;
        public Vec2 IntendedTarget;

        /// <summary>A nutmeg in progress: the ball passes through this defender's legs untouched.</summary>
        public int GhostPlayer = -1;
        public float GhostTime;

        public Vec2 Position => Body.Centre.XY;
        public Vec2 Velocity => Body.Velocity.XY;

        /// <summary>Height of the bottom of the ball above the ground.</summary>
        public float Height => Body.Centre.Z - Radius;

        public float VerticalVelocity => Body.Velocity.Z;
        public Vec3 Spin => Body.Spin;
    }
}
