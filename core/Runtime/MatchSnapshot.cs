using System;

namespace MonkeyPost.Core
{
    public readonly struct PlayerSnapshot
    {
        public readonly int Id;
        public readonly int Team;
        public readonly int Slot;
        public readonly Vec2 Position;
        public readonly Vec2 Velocity;
        public readonly Vec2 Facing;
        public readonly float Stamina;
        public readonly bool IsSprinting;
        public readonly bool IsStunned;

        public PlayerSnapshot(PlayerState p)
        {
            Id = p.Id;
            Team = p.Team;
            Slot = p.Slot;
            Position = p.Position;
            Velocity = p.Velocity;
            Facing = p.Facing;
            Stamina = p.Stamina;
            IsSprinting = p.IsSprinting;
            IsStunned = p.StunTime > 0f;
        }

        public bool IsKeeper => Slot == 0;
    }

    public readonly struct BallSnapshot
    {
        public readonly Vec2 Position;
        public readonly Vec2 Velocity;
        public readonly float Height;
        public readonly int Owner;

        public BallSnapshot(BallState b)
        {
            Position = b.Position;
            Velocity = b.Velocity;
            Height = b.Height;
            Owner = b.Owner;
        }
    }

    /// <summary>
    /// A read-only copy of the match: what bots decide from, what the UI renders and (in local
    /// multiplayer) what the host sends to the guest phone.
    /// </summary>
    public sealed class MatchSnapshot
    {
        public Phase Phase { get; private set; }
        public float TimeRemaining { get; private set; }
        public int HomeScore { get; private set; }
        public int AwayScore { get; private set; }
        public int KickoffTeam { get; private set; }
        public BallSnapshot Ball { get; private set; }
        public PlayerSnapshot[] Players { get; private set; } = Array.Empty<PlayerSnapshot>();

        public static MatchSnapshot Capture(MatchSimulation sim)
        {
            var snapshot = new MatchSnapshot();
            snapshot.CopyFrom(sim);
            return snapshot;
        }

        /// <summary>Refreshes this snapshot in place, reusing its buffers.</summary>
        public void CopyFrom(MatchSimulation sim)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));

            Phase = sim.Phase;
            TimeRemaining = sim.TimeRemaining;
            HomeScore = sim.Score(0);
            AwayScore = sim.Score(1);
            KickoffTeam = sim.KickoffTeam;
            Ball = new BallSnapshot(sim.Ball);

            if (Players.Length != sim.Players.Count) Players = new PlayerSnapshot[sim.Players.Count];
            for (int i = 0; i < Players.Length; i++) Players[i] = new PlayerSnapshot(sim.Players[i]);
        }

        public int Score(int team) => team == 0 ? HomeScore : AwayScore;
    }
}
