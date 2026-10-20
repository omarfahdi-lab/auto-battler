using System;

namespace AutoBattler.Core
{
    // all the tuning numbers live here. tweak them with the balance sim (Tools/BalanceSim), see README
    public static class Balance
    {
        // run
        public const int StartingGold = 10;
        public const int StartingHealth = 20;
        public const int FinalRound = 10;
        public const int BaseLossDamage = 2;

        // income
        public const int BaseIncome = 5;
        public const int InterestStep = 10;
        public const int MaxInterest = 5;

        public static int Interest(int gold) => Math.Min(gold / InterestStep, MaxInterest);

        // 2 in a row = +1, 3 = +2, 4+ = +3 (wins or losses)
        public static int StreakBonus(int streakLength) => streakLength < 2 ? 0 : Math.Min(streakLength - 1, 3);

        // shop / levels
        public const int RerollCost = 1;
        public const int XpCost = 4;
        public const int XpPerPurchase = 4;
        public const int PassiveXpPerRound = 2;
        public const int MaxLevel = 6;

        // xp to go from level N to N+1 (index 0 unused)
        private static readonly int[] XpToNextLevel = { 0, 2, 4, 8, 12, 18 };

        public static int XpToNext(int level) => level >= MaxLevel ? 0 : XpToNextLevel[level];

        public static int UnitCap(int level) => level + 2;

        // % chance of 1g/2g/3g, one row per level
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

        // synergies (2 and 4 of a class)
        public static readonly int[] SynergyThresholds = { 2, 4 };
        public static readonly StatBonus[] WarriorSynergy = { new StatBonus(armorPct: 15), new StatBonus(armorPct: 30) };
        public static readonly StatBonus[] ArcherSynergy = { new StatBonus(attackSpeedPct: 25), new StatBonus(attackSpeedPct: 50) };
        public static readonly StatBonus[] LancerSynergy = { new StatBonus(hpPct: 20), new StatBonus(hpPct: 40) };
        public static readonly StatBonus[] MonkSynergy = { new StatBonus(healPct: 30), new StatBonus(healPct: 60) };
    
        // enemy waves
        // enemy power per round (cost * copies, so same scale as gold)
        private static readonly int[] EnemyBudgetByRound = { 0, 4, 7, 10, 14, 18, 23, 28, 33, 38, 45 };
        // how many non-boss units to buy before the rest goes into upgrades
        private static readonly int[] EnemyCountByRound = { 0, 2, 3, 3, 4, 3, 5, 5, 6, 6, 4 };

        public static readonly int[] BossRounds = { 5, 10 };
        public static readonly StatBonus BossBonus = new StatBonus(hpPct: 50, powerPct: 25);

        public static int EnemyBudget(int round) => EnemyBudgetByRound[Clamp(round)];
        public static int EnemyUnitCount(int round) => EnemyCountByRound[Clamp(round)];
        public static int EnemyMaxCost(int round) => round <= 2 ? 2 : 3;
        public static int EnemyMaxStars(int round) => round <= 2 ? 1 : round <= 7 ? 2 : 3;

        // r5 = 2* lancer, r10 = 3* lancer
        public static OwnedUnit BossFor(int round) => new OwnedUnit(UnitCatalog.Lancer, round >= 10 ? 3 : 2);

        private static int Clamp(int round) => Math.Max(1, Math.Min(round, FinalRound));
    
        // rewards
        public static readonly ItemDefinition[] Items =
        {
            new ItemDefinition("sword", "Sword", ItemStat.Attack, new StatBonus(powerPct: 25), greater: false),
            new ItemDefinition("heart", "Heart Amulet", ItemStat.Hp, new StatBonus(hpPct: 30), greater: false),
            new ItemDefinition("gloves", "Swift Gloves", ItemStat.AttackSpeed, new StatBonus(attackSpeedPct: 25), greater: false),
        };

        public static readonly ItemDefinition[] GreaterItems =
        {
            new ItemDefinition("great-sword", "Great Sword", ItemStat.Attack, new StatBonus(powerPct: 55), greater: true),
            new ItemDefinition("dragon-heart", "Dragon Heart", ItemStat.Hp, new StatBonus(hpPct: 65), greater: true),
            new ItemDefinition("storm-gloves", "Storm Gloves", ItemStat.AttackSpeed, new StatBonus(attackSpeedPct: 55), greater: true),
        };

        public static readonly AugmentDefinition[] Augments =
        {
            new AugmentDefinition("rally", "Rally", new StatBonus(powerPct: 10), 0, greater: false),
            new AugmentDefinition("fortify", "Fortify", new StatBonus(hpPct: 12), 0, greater: false),
            new AugmentDefinition("haste", "Haste", new StatBonus(attackSpeedPct: 10), 0, greater: false),
            new AugmentDefinition("treasury", "Treasury", default, 2, greater: false),
        };

        public static readonly AugmentDefinition[] GreaterAugments =
        {
            new AugmentDefinition("war-cry", "War Cry", new StatBonus(powerPct: 22), 0, greater: true),
            new AugmentDefinition("bastion", "Bastion", new StatBonus(hpPct: 25), 0, greater: true),
            new AugmentDefinition("frenzy", "Frenzy", new StatBonus(attackSpeedPct: 22), 0, greater: true),
            new AugmentDefinition("royal-treasury", "Royal Treasury", default, 4, greater: true),
        };
    }
}
