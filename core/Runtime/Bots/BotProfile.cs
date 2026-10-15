using System;

namespace MonkeyPost.Core.Bots
{
    public enum BotDifficulty
    {
        Easy,
        Medium,
        Hard,
    }

    /// <summary>Knobs that make a bot feel human: slow or fast, gullible or sharp, honest or bluffing.</summary>
    [Serializable]
    public sealed class BotProfile
    {
        /// <summary>Seconds between deciding to check and actually checking (±25% jitter).</summary>
        public float ReactionSeconds;

        /// <summary>How long a sitter watches the runner after they pass, looking for a sprint tell.</summary>
        public float TellWindowSeconds;

        /// <summary>Chance to check after the runner passes, with no tell.</summary>
        public float CheckOnPassChance;

        /// <summary>Extra chance to check when the runner sprints off after passing.</summary>
        public float SprintTellBonus;

        /// <summary>Chance to check as the runner comes round again late in a round.</summary>
        public float LateCheckChance;

        /// <summary>Runner: chance to sprint past a seat without dropping, to fake a tell.</summary>
        public float BluffChance;

        /// <summary>Runner: seconds to keep jogging after a drop before sprinting away.</summary>
        public float SprintDelayAfterDrop;

        /// <summary>Runner: chance to target the highest-scoring sitter instead of a random one.</summary>
        public float TargetLeaderChance;

        public static BotProfile For(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    return new BotProfile
                    {
                        ReactionSeconds = 1.2f,
                        TellWindowSeconds = 0.6f,
                        CheckOnPassChance = 0.30f,
                        SprintTellBonus = 0.10f,
                        LateCheckChance = 0.10f,
                        BluffChance = 0f,
                        SprintDelayAfterDrop = 0f,
                        TargetLeaderChance = 0f,
                    };
                case BotDifficulty.Medium:
                    return new BotProfile
                    {
                        ReactionSeconds = 0.7f,
                        TellWindowSeconds = 0.6f,
                        CheckOnPassChance = 0.15f,
                        SprintTellBonus = 0.45f,
                        LateCheckChance = 0.30f,
                        BluffChance = 0.20f,
                        SprintDelayAfterDrop = 0.3f,
                        TargetLeaderChance = 0f,
                    };
                case BotDifficulty.Hard:
                    return new BotProfile
                    {
                        ReactionSeconds = 0.35f,
                        TellWindowSeconds = 1.0f,
                        CheckOnPassChance = 0.06f,
                        SprintTellBonus = 0.55f,
                        LateCheckChance = 0.50f,
                        BluffChance = 0.25f,
                        SprintDelayAfterDrop = 0.8f,
                        TargetLeaderChance = 0.6f,
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(difficulty));
            }
        }
    }
}
