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

        /// <summary>Chance per decision to try a skill move on a close defender.</summary>
        public float SkillChance;

        public static BotProfile For(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    return new BotProfile
                    {
                        DecisionInterval = 0.6f,
                        ReactionSeconds = 0.4f,
                        TackleChance = 0.25f,
                        ShootRange = 9f,
                        AimSmartness = 0.1f,
                        PowerError = 0.2f,
                        PassVision = 0.3f,
                        KeeperAnticipation = 0f,
                        SkillChance = 0.03f,
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
                        SkillChance = 0.12f,
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
                        SkillChance = 0.25f,
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(difficulty));
            }
        }

        /// <summary>
        /// Physical edges for a computer team: stronger teams are a few percent quicker, sharper in
        /// the tackle and cleaner on the ball, and each street-cup round adds a little more.
        /// </summary>
        public static TeamTraits TraitsFor(BotDifficulty difficulty, int cupRound = 0)
        {
            float pace, tackling, touch;
            switch (difficulty)
            {
                case BotDifficulty.Easy: pace = 0.97f; tackling = 0.9f; touch = 0.9f; break;
                case BotDifficulty.Medium: pace = 1f; tackling = 1f; touch = 1f; break;
                case BotDifficulty.Hard: pace = 1.03f; tackling = 1.1f; touch = 1.1f; break;
                default: throw new ArgumentOutOfRangeException(nameof(difficulty));
            }
            return new TeamTraits
            {
                Pace = pace + 0.005f * cupRound,
                Tackling = tackling + 0.02f * cupRound,
                Touch = touch + 0.02f * cupRound,
            };
        }
    }
}
