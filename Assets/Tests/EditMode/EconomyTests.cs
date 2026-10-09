using System;
using System.Linq;
using AutoBattler.Core;
using NUnit.Framework;

namespace AutoBattler.Tests
{
    public class EconomyTests
    {
        [TestCase(0, 0)]
        [TestCase(9, 0)]
        [TestCase(10, 1)]
        [TestCase(49, 4)]
        [TestCase(50, 5)]
        [TestCase(200, 5)]
        public void Interest_IsOnePerTenBanked_CappedAtFive(int gold, int expected)
        {
            Assert.That(Balance.Interest(gold), Is.EqualTo(expected));
        }

        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(3, 2)]
        [TestCase(4, 3)]
        [TestCase(9, 3)]
        public void StreakBonus_StartsAtTwoAndCapsAtThree(int streak, int expected)
        {
            Assert.That(Balance.StreakBonus(streak), Is.EqualTo(expected));
        }

        [Test]
        public void Income_AddsBaseInterestAndStreak_ForWinsAndLosses()
        {
            Assert.That(GameSession.Income(25, -3, out int interest, out int streak), Is.EqualTo(5 + 2 + 2));
            Assert.That(interest, Is.EqualTo(2));
            Assert.That(streak, Is.EqualTo(2));
            Assert.That(GameSession.Income(25, 3, out _, out _), Is.EqualTo(9), "win and loss streaks pay the same");
            Assert.That(GameSession.Income(0, 1, out _, out _), Is.EqualTo(Balance.BaseIncome));
        }

        [Test]
        public void CompletedRounds_TrackStreakAndPayMatchingIncome()
        {
            var s = new GameSession(new SeededRandom(11));
            s.Shop.Set(0, UnitCatalog.Warrior);
            s.TryBuy(0);
            s.TryMove(Slot.Bench(0), Slot.Board(new GridPos(3, 2)));

            int previousStreak = 0;
            for (int round = 0; round < 3 && s.Phase == Phase.Shop; round++)
            {
                int goldBefore = s.Gold;
                int augmentGold = s.AugmentGoldPerRound; // a reward picked after this round pays from the next one
                var result = TestRuns.PlayRound(s);
                if (s.Phase != Phase.Shop) break;

                int expectedStreak = result.Won ? Math.Max(previousStreak, 0) + 1 : Math.Min(previousStreak, 0) - 1;
                Assert.That(s.Streak, Is.EqualTo(expectedStreak));
                Assert.That(result.GoldEarned,
                    Is.EqualTo(GameSession.Income(goldBefore, expectedStreak, out _, out _) + augmentGold));
                Assert.That(s.Gold, Is.EqualTo(goldBefore + result.GoldEarned));
                previousStreak = expectedStreak;
            }
        }

        [Test]
        public void BuyXp_CostsGoldAndLevelsUpRaisingUnitCap()
        {
            var s = new GameSession(new SeededRandom(1));
            int capBefore = s.BoardCapacity;

            Assert.That(s.TryBuyXp());

            Assert.That(s.Gold, Is.EqualTo(Balance.StartingGold - Balance.XpCost));
            Assert.That(s.Level, Is.EqualTo(2));
            Assert.That(s.Xp, Is.EqualTo(Balance.XpPerPurchase - Balance.XpToNext(1)));
            Assert.That(s.BoardCapacity, Is.EqualTo(capBefore + 1));
        }

        [Test]
        public void BuyXp_FailsWithoutGoldAndAtMaxLevel()
        {
            var s = new GameSession(new SeededRandom(1)) { Gold = Balance.XpCost - 1 };
            Assert.That(s.TryBuyXp(), Is.False);

            s.Gold = 1000;
            while (s.TryBuyXp()) { }
            Assert.That(s.Level, Is.EqualTo(Balance.MaxLevel));
            Assert.That(s.Xp, Is.Zero);
            Assert.That(s.CanBuyXp, Is.False);
        }

        [Test]
        public void FinishingARound_GrantsPassiveXp()
        {
            var s = new GameSession(new SeededRandom(1));
            s.Shop.Set(0, UnitCatalog.Warrior);
            s.TryBuy(0);
            s.TryMove(Slot.Bench(0), Slot.Board(new GridPos(3, 2)));
            TestRuns.PlayRound(s);

            Assert.That(s.Level, Is.EqualTo(2), "two passive XP reach level 2");
        }

        [Test]
        public void HigherLevels_ShiftShopOddsTowardThreeCostUnits()
        {
            for (int level = 2; level <= Balance.MaxLevel; level++)
            {
                Assert.That(Balance.ShopOdds(level, 3), Is.GreaterThan(Balance.ShopOdds(level - 1, 3)));
                Assert.That(Enumerable.Range(1, 3).Sum(c => Balance.ShopOdds(level, c)), Is.EqualTo(100));
            }

            double ThreeCostShare(int level)
            {
                var rng = new SeededRandom(level);
                return Enumerable.Range(0, 4000).Count(_ => Shop.Pick(rng, level).Cost == 3) / 4000.0;
            }
            Assert.That(ThreeCostShare(1), Is.Zero);
            Assert.That(ThreeCostShare(Balance.MaxLevel), Is.EqualTo(Balance.ShopOdds(Balance.MaxLevel, 3) / 100.0).Within(0.03));
        }
    }
}
