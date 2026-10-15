namespace MonkeyPost.Core
{
    public enum Phase
    {
        /// <summary>The runner circles the ring and may drop the post.</summary>
        Circling,
        /// <summary>The target found the post and is chasing the runner.</summary>
        Chase,
        /// <summary>Short pause between rounds. The clock keeps running.</summary>
        Interlude,
        Ended,
    }

    public enum Role
    {
        Sitter,
        Runner,
        Chaser,
    }

    /// <summary>One player's controls for a single tick.</summary>
    public struct PlayerInput
    {
        public bool Sprint;
        /// <summary>Runner only: drop the post behind the nearest seat.</summary>
        public bool Drop;
        /// <summary>Sitter only: look behind for the post.</summary>
        public bool Check;
    }

    public enum MatchEventType
    {
        RoundStarted,
        /// <summary>Secret: only the host and the runner may learn about this.</summary>
        PostDropped,
        PostDiscovered,
        FalseCheck,
        RunnerSafe,
        RunnerTagged,
        Posted,
        MatchEnded,
    }

    public readonly struct MatchEvent
    {
        public readonly MatchEventType Type;
        /// <summary>The main player involved (runner for drops and safe, checker, chaser, etc.), or -1.</summary>
        public readonly int PlayerA;
        /// <summary>The other player involved, or -1.</summary>
        public readonly int PlayerB;
        public readonly int Seat;

        public MatchEvent(MatchEventType type, int playerA = -1, int playerB = -1, int seat = -1)
        {
            Type = type;
            PlayerA = playerA;
            PlayerB = playerB;
            Seat = seat;
        }

        /// <summary>Whether every player may see this event.</summary>
        public bool IsPublic => Type != MatchEventType.PostDropped;

        public override string ToString() => $"{Type}(a={PlayerA}, b={PlayerB}, seat={Seat})";
    }

    public sealed class PlayerState
    {
        public int Id;
        public Role Role;
        /// <summary>Seat index while sitting, otherwise -1.</summary>
        public int Seat = -1;
        public float Angle;
        public float Stamina = 1f;
        public bool IsSprinting;
        public int Score;
        public float CheckCooldown;
    }
}
