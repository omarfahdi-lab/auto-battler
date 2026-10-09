using System.Linq;
using AutoBattler.Core;
using NUnit.Framework;

namespace AutoBattler.Tests
{
    public class BattleSimulatorTests
    {
        private static Combatant Make(int id, Team team, UnitDefinition def, int x, int y, int stars = 1) =>
            new Combatant(id, team, new OwnedUnit(def, stars), new GridPos(x, y));

        [Test]
        public void MeleeUnit_MovesTowardNearestEnemy()
        {
            var sim = new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Knight, 0, 0),
                Make(1, Team.Enemy, UnitCatalog.Knight, 0, 5),
            });

            var events = sim.Step();

            Assert.That(events.Any(e => e.Type == BattleEventType.Move && e.SourceId == 0));
            Assert.That(sim.Find(0).Position, Is.EqualTo(new GridPos(0, 1)));
        }

        [Test]
        public void RangedUnit_AttacksWithoutMovingWhenInRange()
        {
            var sim = new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Archer, 0, 0),
                Make(1, Team.Enemy, UnitCatalog.Golem, 0, 3),
            });

            var events = sim.Step();

            Assert.That(sim.Find(0).Position, Is.EqualTo(new GridPos(0, 0)));
            Assert.That(events.Any(e => e.Type == BattleEventType.Attack && e.SourceId == 0 && e.TargetId == 1));
            Assert.That(sim.Find(1).Hp, Is.EqualTo(UnitCatalog.Golem.MaxHp - UnitCatalog.Archer.Attack));
        }

        [Test]
        public void Attacks_RespectCooldown()
        {
            var sim = new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Archer, 0, 0),
                Make(1, Team.Enemy, UnitCatalog.Golem, 7, 3, stars: 3),
            });
            // Archer is out of range, golem must walk; count archer attacks over a fixed window once in range.
            int attacks = 0;
            for (int i = 0; i < 30; i++)
                attacks += sim.Step().Count(e => e.Type == BattleEventType.Attack && e.SourceId == 0);

            Assert.That(attacks, Is.LessThanOrEqualTo(30 / UnitCatalog.Archer.AttackCooldown + 1));
            Assert.That(attacks, Is.GreaterThan(0));
        }

        [Test]
        public void DeadUnit_EmitsDeathAndFreesCell()
        {
            var sim = new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Mage, 0, 0, stars: 3),
                Make(1, Team.Enemy, UnitCatalog.Archer, 1, 1),
            });

            var events = sim.Step();

            Assert.That(events.Any(e => e.Type == BattleEventType.Death && e.SourceId == 1));
            Assert.That(sim.IsOver);
            Assert.That(sim.Winner, Is.EqualTo(Team.Player));
        }

        [Test]
        public void StrongerTeam_Wins()
        {
            var sim = new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Golem, 3, 2, stars: 2),
                Make(1, Team.Player, UnitCatalog.Archer, 3, 0, stars: 2),
                Make(2, Team.Enemy, UnitCatalog.Knight, 3, 4),
            });

            Assert.That(sim.RunToEnd(), Is.EqualTo(Team.Player));
            Assert.That(sim.AliveCount(Team.Enemy), Is.Zero);
        }

        [Test]
        public void UnitsNeverShareACell()
        {
            var combatants = Enumerable.Range(0, 8)
                .Select(i => Make(i, Team.Player, UnitCatalog.Knight, i, 0))
                .Concat(Enumerable.Range(0, 8).Select(i => Make(8 + i, Team.Enemy, UnitCatalog.Rogue, i, 5)))
                .ToList();
            var sim = new BattleSimulator(combatants);

            while (!sim.IsOver)
            {
                sim.Step();
                var cells = sim.Combatants.Where(c => c.IsAlive).Select(c => c.Position).ToList();
                Assert.That(cells.Distinct().Count(), Is.EqualTo(cells.Count));
            }
        }

        [Test]
        public void Battle_TimesOutAsDraw()
        {
            Assert.That(BattleSimulator.MaxTicks, Is.GreaterThan(0));
            var sim = new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Golem, 0, 0, stars: 3),
                Make(1, Team.Enemy, UnitCatalog.Golem, 7, 5, stars: 3),
            });

            sim.RunToEnd();

            Assert.That(sim.IsOver);
            Assert.That(sim.Tick, Is.LessThanOrEqualTo(BattleSimulator.MaxTicks));
        }

        [Test]
        public void OverlappingCombatants_Throw()
        {
            Assert.Throws<System.ArgumentException>(() => new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Knight, 1, 1),
                Make(1, Team.Enemy, UnitCatalog.Knight, 1, 1),
            }));
        }
    }
}
