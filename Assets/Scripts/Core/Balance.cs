using System;

namespace AutoBattler.Core
{
    /// <summary>
    /// Every tunable number in one place. Values here are tuned with the headless balance simulation
    /// (see <see cref="BalanceSimulation"/> and the README).
    /// </summary>
    public static class Balance
    {
        // ---- Run ---------------------------------------------------------------------------------------------
        public const int StartingGold = 10;
        public const int StartingHealth = 20;
        public const int FinalRound = 10;
        public const int BaseLossDamage = 2;

        // ---- Income ------------------------------------------------------------------------------------------
        public const int BaseIncome = 5;
        public const int InterestStep = 10;
        public const int MaxInterest = 5;

        public static int Interest(int gold) => Math.Min(gold / InterestStep, MaxInterest);

        /// <summary>Win or loss streak bonus: 2 in a row = +1, 3 = +2, 4 or more = +3.</summary>
        public static int StreakBonus(int streakLength) => streakLength < 2 ? 0 : Math.Min(streakLength - 1, 3);

        // ---- Shop and levels ---------------------------------------------------------------------------------
        public const int RerollCost = 1;
        public const int XpCost = 4;
        public const int XpPerPurchase = 4;
        public const int PassiveXpPerRound = 2;
        public const int MaxLevel = 6;

        /// <summary>XP needed to go from level N to N+1, indexed by N (index 0 unused).</summary>
        private static readonly int[] XpToNextLevel = { 0, 2, 4, 8, 12, 18 };

        public static int XpToNext(int level) => level >= MaxLevel ? 0 : XpToNextLevel[level];

        /// <summary>Units allowed on the board at a level.</summary>
        public static int UnitCap(int level) => level + 2;

        /// <summary>Shop odds in percent for 1g/2g/3g units, indexed by level - 1.</summary>
        private static readonly int[][] ShopOddsByLevel =
        {
            new[] { 75, 25, 0 },
            new[] { 60, 32, 8 },
            new[] { 45, 37, 18 },
            new[] { 33, 38, 29 },
            new[] { 25, 35, 40 },
            new[] { 17, 33, 50 },
        };

        public static int ShopOdds(int level, int cost) =>
            cost < 1 || cost > 3 ? 0 : ShopOddsByLevel[Math.Max(1, Math.Min(level, MaxLevel)) - 1][cost - 1];

        // ---- Synergies (2 and 4 units of a class) ---------------------------------------------------------------
        public static readonly int[] SynergyThresholds = { 2, 4 };
        public static readonly StatBonus[] WarriorSynergy = { new StatBonus(armorPct: 15), new StatBonus(armorPct: 30) };
        public static readonly StatBonus[] ArcherSynergy = { new StatBonus(attackSpeedPct: 25), new StatBonus(attackSpeedPct: 50) };
        public static readonly StatBonus[] LancerSynergy = { new StatBonus(hpPct: 20), new StatBonus(hpPct: 40) };
        public static readonly StatBonus[] MonkSynergy = { new StatBonus(healPct: 30), new StatBonus(healPct: 60) };
    }
}
