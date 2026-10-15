using System.Collections.Generic;

namespace MonkeyPost.Game
{
    /// <summary>
    /// Goal celebrations: 2 free, 6 unlockable in the shop (owner decision). Each maps to a clip
    /// named "Celebrate_&lt;Id&gt;" in Resources/Animations.
    /// </summary>
    static class Celebrations
    {
        public sealed class Entry
        {
            public string Id;
            public string Name;
            public bool Free;
            public int Price;
        }

        public const int ShopPrice = 1200;

        public static readonly IReadOnlyList<Entry> All = new[]
        {
            new Entry { Id = "FistPump", Name = "Fist Pump", Free = true },
            new Entry { Id = "RunOff", Name = "Arms Out", Free = true },
            new Entry { Id = "KneeSlide", Name = "Knee Slide", Price = ShopPrice },
            new Entry { Id = "Dance", Name = "Street Dance", Price = ShopPrice },
            new Entry { Id = "Salute", Name = "Salute", Price = ShopPrice },
            new Entry { Id = "Backflip", Name = "Backflip", Price = ShopPrice },
            new Entry { Id = "Shush", Name = "Shush the Crowd", Price = ShopPrice },
            new Entry { Id = "Bow", Name = "Take a Bow", Price = ShopPrice },
        };

        /// <summary>The celebration a person's scorer plays (the shop will set this; free default for now).</summary>
        public static string Equipped = "FistPump";

        public static string RandomFree(int seed) => All[seed % 2 == 0 ? 0 : 1].Id;

        public static string ClipName(string id) => "Celebrate_" + id;
    }
}
