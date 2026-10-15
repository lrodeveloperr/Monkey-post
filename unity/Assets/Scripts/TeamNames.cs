namespace MonkeyPost.Game
{
    /// <summary>Fictional Nigerian street teams. No real clubs.</summary>
    static class TeamNames
    {
        static readonly string[] Names =
        {
            "Surulere Stars", "Ajegunle Ballers", "Mushin United", "Yaba Strikers",
            "Oshodi Rangers", "Ikeja Warriors", "Bodija Boys", "Garki Movers",
            "Sabon Gari Lions", "Trans-Ekulu FC", "Rumuokoro Rovers", "Agege Hustlers",
        };

        public static string Pick(ulong seed) => Names[(int)(seed % (ulong)Names.Length)];
    }
}
