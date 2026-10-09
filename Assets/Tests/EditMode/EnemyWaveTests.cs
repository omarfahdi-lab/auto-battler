using System.Linq;
using AutoBattler.Core;
using NUnit.Framework;

namespace AutoBattler.Tests
{
    public class EnemyWaveTests
    {
        private static int Power(System.Collections.Generic.IReadOnlyList<EnemyPlacement> wave) =>
            wave.Sum(e => EnemyWaves.PowerOf(e.Unit));

        [Test]
        public void Budget_RampsEveryRound()
        {
            for (int round = 2; round <= Balance.FinalRound; round++)
                Assert.That(Balance.EnemyBudget(round), Is.GreaterThan(Balance.EnemyBudget(round - 1)), $"round {round}");
        }

        [Test]
        public void Waves_SpendTheirBudgetWithoutGoingOver()
        {
            for (int round = 1; round <= Balance.FinalRound; round++)
            for (int seed = 0; seed < 50; seed++)
            {
                var wave = EnemyWaves.Generate(round, new SeededRandom(seed));
                int budget = Balance.EnemyBudget(round);

                Assert.That(Power(wave), Is.LessThanOrEqualTo(budget), $"round {round} seed {seed}");
                Assert.That(Power(wave), Is.GreaterThanOrEqualTo(budget - 2), $"round {round} seed {seed}");
            }
        }

        [Test]
        public void Waves_RespectCostAndStarLimits()
        {
            for (int round = 1; round <= Balance.FinalRound; round++)
            for (int seed = 0; seed < 50; seed++)
            foreach (var enemy in EnemyWaves.Generate(round, new SeededRandom(seed)).Where(e => !e.IsBoss))
            {
                Assert.That(enemy.Unit.Definition.Cost, Is.LessThanOrEqualTo(Balance.EnemyMaxCost(round)));
                Assert.That(enemy.Unit.Stars, Is.LessThanOrEqualTo(Balance.EnemyMaxStars(round)));
            }
        }

        [Test]
        public void Waves_StayOnEnemySideInDistinctCells()
        {
            for (int round = 1; round <= Balance.FinalRound; round++)
            {
                var wave = EnemyWaves.Generate(round, new SeededRandom(round));

                Assert.That(wave.Count, Is.InRange(1, EnemyWaves.MaxEnemies));
                Assert.That(wave.All(e => BoardLayout.InBounds(e.Position) && !BoardLayout.IsPlayerCell(e.Position)));
                Assert.That(wave.Select(e => e.Position).Distinct().Count(), Is.EqualTo(wave.Count));
            }
        }

        [Test]
        public void BossRounds_HaveOneBigUpgradedUnitPlusSupport()
        {
            Assert.That(Balance.BossRounds, Is.EqualTo(new[] { 5, 10 }));
            for (int round = 1; round <= Balance.FinalRound; round++)
            {
                var wave = EnemyWaves.Generate(round, new SeededRandom(7));
                var bosses = wave.Where(e => e.IsBoss).ToList();

                if (!EnemyWaves.IsBossRound(round))
                {
                    Assert.That(bosses, Is.Empty, $"round {round}");
                    continue;
                }
                Assert.That(bosses.Count, Is.EqualTo(1), $"round {round}");
                Assert.That(bosses[0].Unit.Stars, Is.EqualTo(round == 10 ? 3 : 2));
                Assert.That(wave.Count, Is.GreaterThan(1), "bosses bring support");
                Assert.That(wave.Where(e => !e.IsBoss).All(e => EnemyWaves.PowerOf(e.Unit) < EnemyWaves.PowerOf(bosses[0].Unit)));
            }
        }

        [Test]
        public void BossBonus_MakesTheBossTougher()
        {
            var boss = Balance.BossFor(5);
            var plain = CombatStats.For(boss, default);
            var buffed = CombatStats.For(boss, Balance.BossBonus);

            Assert.That(buffed.MaxHp, Is.GreaterThan(plain.MaxHp));
            Assert.That(buffed.Power, Is.GreaterThan(plain.Power));
        }

        [Test]
        public void SameSeed_GivesSameWave()
        {
            string Describe(int seed) => string.Join(";", EnemyWaves.Generate(8, new SeededRandom(seed))
                .Select(e => $"{e.Unit}@{e.Position}"));
            Assert.That(Describe(3), Is.EqualTo(Describe(3)));
        }
    }
}
