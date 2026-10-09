using System.Linq;
using AutoBattler.Core;
using NUnit.Framework;

namespace AutoBattler.Tests
{
    public class ShopAndWaveTests
    {
        [Test]
        public void SeededRandom_IsDeterministicAndInRange()
        {
            var a = new SeededRandom(7);
            var b = new SeededRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                int x = a.Next(10);
                Assert.That(x, Is.EqualTo(b.Next(10)));
                Assert.That(x, Is.InRange(0, 9));
            }
        }

        [Test]
        public void ExpensiveUnits_LockedInFirstRounds()
        {
            Assert.That(Shop.Weight(3, 1), Is.Zero);
            Assert.That(Shop.Weight(3, 3), Is.GreaterThan(0));

            var rng = new SeededRandom(3);
            for (int i = 0; i < 500; i++) Assert.That(Shop.Pick(rng, 1).Cost, Is.LessThan(3));
        }

        [Test]
        public void Costs_HaveCheapMidAndHighTiers()
        {
            Assert.That(UnitCatalog.Warrior.Cost, Is.EqualTo(1));
            Assert.That(UnitCatalog.Archer.Cost, Is.EqualTo(2));
            Assert.That(UnitCatalog.Lancer.Cost, Is.EqualTo(3));
            Assert.That(UnitCatalog.Monk.Cost, Is.EqualTo(3));

            var rng = new SeededRandom(5);
            var round1 = Enumerable.Range(0, 500).Select(_ => Shop.Pick(rng, 1)).ToList();
            Assert.That(round1, Has.Some.EqualTo(UnitCatalog.Archer), "the 2g tier is available from round 1");
            Assert.That(round1, Has.Some.EqualTo(UnitCatalog.Warrior));
        }

        [Test]
        public void Wave_GrowsWithRoundAndStaysOnEnemySide()
        {
            for (int round = 1; round <= GameSession.FinalRound; round++)
            {
                var wave = EnemyWaves.Generate(round, new SeededRandom(round));

                Assert.That(wave.Count, Is.EqualTo(EnemyWaves.EnemyCount(round)));
                Assert.That(wave.All(e => BoardLayout.InBounds(e.Position) && !BoardLayout.IsPlayerCell(e.Position)));
                Assert.That(wave.Select(e => e.Position).Distinct().Count(), Is.EqualTo(wave.Count));
            }
        }

        [Test]
        public void OwnedUnit_StatsAndSellValueScaleWithStars()
        {
            var one = new OwnedUnit(UnitCatalog.Warrior, 1);
            var three = new OwnedUnit(UnitCatalog.Warrior, 3);

            Assert.That(three.Power, Is.GreaterThan(one.Power));
            Assert.That(three.SellValue, Is.EqualTo(UnitCatalog.Warrior.Cost * 9));
            Assert.That(three.CanMergeWith(new OwnedUnit(UnitCatalog.Warrior, 3)), Is.False);
        }
    }
}
