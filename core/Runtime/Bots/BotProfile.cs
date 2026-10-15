using System;

namespace MonkeyPost.Core.Bots
{
    public enum BotDifficulty
    {
        Easy,
        Medium,
        Hard,
    }

    /// <summary>Knobs that separate a lazy street team from a sharp one.</summary>
    [Serializable]
    public sealed class BotProfile
    {
        /// <summary>Seconds between decisions to pass, shoot or tackle (±20% jitter).</summary>
        public float DecisionInterval;

        /// <summary>How stale the team's picture of the ball is, in seconds. Slower = reacts late.</summary>
        public float ReactionSeconds;

        /// <summary>Chance to go in for a tackle when in range at a decision.</summary>
        public float TackleChance;

        /// <summary>Distance from goal at which the ball carrier shoots.</summary>
        public float ShootRange;

        /// <summary>Chance to aim away from the keeper rather than anywhere.</summary>
        public float AimSmartness;

        /// <summary>Random error added to shot power (±). Too much power flies over the posts.</summary>
        public float PowerError;

        /// <summary>0..1: how reliably the best pass is spotted rather than a random open one.</summary>
        public float PassVision;

        /// <summary>Seconds of ball movement the keeper anticipates when positioning.</summary>
        public float KeeperAnticipation;

        public static BotProfile For(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    return new BotProfile
                    {
                        DecisionInterval = 0.5f,
                        ReactionSeconds = 0.35f,
                        TackleChance = 0.3f,
                        ShootRange = 9f,
                        AimSmartness = 0.2f,
                        PowerError = 0.15f,
                        PassVision = 0.3f,
                        KeeperAnticipation = 0f,
                    };
                case BotDifficulty.Medium:
                    return new BotProfile
                    {
                        DecisionInterval = 0.3f,
                        ReactionSeconds = 0.2f,
                        TackleChance = 0.5f,
                        ShootRange = 11f,
                        AimSmartness = 0.5f,
                        PowerError = 0.08f,
                        PassVision = 0.6f,
                        KeeperAnticipation = 0.15f,
                    };
                case BotDifficulty.Hard:
                    return new BotProfile
                    {
                        DecisionInterval = 0.15f,
                        ReactionSeconds = 0.1f,
                        TackleChance = 0.7f,
                        ShootRange = 12f,
                        AimSmartness = 0.85f,
                        PowerError = 0.04f,
                        PassVision = 0.9f,
                        KeeperAnticipation = 0.3f,
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(difficulty));
            }
        }
    }
}
