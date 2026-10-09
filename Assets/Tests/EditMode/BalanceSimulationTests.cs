using System.Linq;
using AutoBattler.Core;
using NUnit.Framework;

namespace AutoBattler.Tests
{
    public class BalanceSimulationTests
    {
        [Test]
        public void Simulation_PlaysEveryRunToTheEnd()
        {
            var report = BalanceSimulation.Run(runs: 25, seed: 1);

            Assert.That(report.Runs, Is.EqualTo(25));
            Assert.That(report.Rounds[1].Reached, Is.EqualTo(25));
            for (int r = 1; r <= Balance.FinalRound; r++)
            {
                Assert.That(report.Rounds[r].Wins, Is.LessThanOrEqualTo(report.Rounds[r].Reached));
                if (r > 1) Assert.That(report.Rounds[r].Reached, Is.LessThanOrEqualTo(report.Rounds[r - 1].Reached));
            }
            Assert.That(report.Victories, Is.EqualTo(report.Rounds[Balance.FinalRound].Wins));
        }

        [Test]
        public void Simulation_IsDeterministicForASeed()
        {
            Assert.That(BalanceSimulation.Run(10, seed: 5).ToMarkdown(), Is.EqualTo(BalanceSimulation.Run(10, seed: 5).ToMarkdown()));
        }

        [Test]
        public void Report_HasARowPerRoundAndFlagsBosses()
        {
            var lines = BalanceSimulation.Run(5).ToMarkdown().Split('\n');

            Assert.That(lines.Count(l => l.StartsWith("| ") && char.IsDigit(l[2])), Is.EqualTo(Balance.FinalRound));
            Assert.That(lines.Count(l => l.Contains("(boss)")), Is.EqualTo(Balance.BossRounds.Length));
        }

        [Test]
        public void Bot_SpendsAndFieldsEverythingItCanInRoundOne()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var s = new GameSession(new SeededRandom(seed));
                new GreedyBot().PlayShop(s);

                int owned = s.OccupiedSlots().Count();
                Assert.That(s.BoardCount, Is.EqualTo(System.Math.Min(owned, s.BoardCapacity)), $"seed {seed}");
                Assert.That(s.BoardCount, Is.GreaterThan(0));
                Assert.That(s.Gold, Is.LessThan(Balance.StartingGold));
            }
        }

        [Test]
        public void Bot_PicksAValidReward()
        {
            var s = new GameSession(new SeededRandom(3));
            var bot = new GreedyBot();
            for (int i = 0; i < 6 && s.Phase != Phase.Reward && s.Phase != Phase.Defeat; i++)
            {
                if (s.Phase == Phase.Shop) bot.PlayShop(s);
                var battle = s.StartBattle();
                battle.RunToEnd();
                s.CompleteBattle(battle);
            }

            Assume.That(s.Phase, Is.EqualTo(Phase.Reward), "bot should win at least once early on");
            Assert.That(bot.ChooseReward(s), Is.InRange(0, s.PendingRewards.Count - 1));
        }
    }
}
