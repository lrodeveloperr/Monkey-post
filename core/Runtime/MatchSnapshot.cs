using System;

namespace MonkeyPost.Core
{
    public readonly struct PlayerSnapshot
    {
        public readonly int Id;
        public readonly Role Role;
        public readonly int Seat;
        public readonly float Angle;
        public readonly float Stamina;
        public readonly bool IsSprinting;
        public readonly int Score;
        public readonly float CheckCooldown;

        public PlayerSnapshot(PlayerState p)
        {
            Id = p.Id;
            Role = p.Role;
            Seat = p.Seat;
            Angle = p.Angle;
            Stamina = p.Stamina;
            IsSprinting = p.IsSprinting;
            Score = p.Score;
            CheckCooldown = p.CheckCooldown;
        }
    }

    /// <summary>
    /// What one player is allowed to know about the match. This is the only view given to bots,
    /// the UI and (in local multiplayer) guest phones, so the post's location never leaks.
    /// </summary>
    public sealed class MatchSnapshot
    {
        /// <summary>The player this snapshot was taken for, or -1 for a spectator view.</summary>
        public int ViewerId { get; private set; } = -1;
        public Phase Phase { get; private set; }
        public float TimeRemaining { get; private set; }
        public float TimeSinceRoundStart { get; private set; }
        public int RunnerId { get; private set; }
        public int ChaserId { get; private set; }
        public int SeatCount { get; private set; }
        public PlayerSnapshot[] Players { get; private set; } = Array.Empty<PlayerSnapshot>();

        /// <summary>True only for the runner while they still hold the post.</summary>
        public bool ViewerHoldsPost { get; private set; }

        /// <summary>The post's seat if the viewer is the runner and has dropped it, otherwise -1.</summary>
        public int ViewerKnownPostSeat { get; private set; } = -1;

        public static MatchSnapshot Capture(MatchSimulation sim, int viewerId)
        {
            var snapshot = new MatchSnapshot();
            snapshot.CopyFrom(sim, viewerId);
            return snapshot;
        }

        /// <summary>Refreshes this snapshot in place, reusing its buffers.</summary>
        public void CopyFrom(MatchSimulation sim, int viewerId)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));

            ViewerId = viewerId;
            Phase = sim.Phase;
            TimeRemaining = sim.TimeRemaining;
            TimeSinceRoundStart = sim.TimeSinceRoundStart;
            RunnerId = sim.RunnerId;
            ChaserId = sim.ChaserId;
            SeatCount = sim.SeatCount;

            if (Players.Length != sim.Players.Count) Players = new PlayerSnapshot[sim.Players.Count];
            for (int i = 0; i < Players.Length; i++) Players[i] = new PlayerSnapshot(sim.Players[i]);

            bool viewerIsRunner = viewerId == sim.RunnerId && sim.Phase == Phase.Circling;
            ViewerHoldsPost = viewerIsRunner && sim.PostInHand;
            ViewerKnownPostSeat = viewerIsRunner && !sim.PostInHand ? sim.PostSeat : -1;
        }
    }
}
