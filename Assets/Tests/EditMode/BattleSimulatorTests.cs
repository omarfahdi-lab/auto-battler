using System.Collections.Generic;
using System.Linq;
using AutoBattler.Core;
using NUnit.Framework;

namespace AutoBattler.Tests
{
    public class BattleSimulatorTests
    {
        private static Combatant Make(int id, Team team, UnitDefinition def, int x, int y, int stars = 1) =>
            new Combatant(id, team, new OwnedUnit(def, stars), new GridPos(x, y));

        // (tick, event) for every event over n ticks
        private static List<(int tick, BattleEvent e)> Run(BattleSimulator sim, int ticks)
        {
            var log = new List<(int, BattleEvent)>();
            for (int i = 0; i < ticks && !sim.IsOver; i++)
            {
                int tick = sim.Tick;
                foreach (var e in sim.Step()) log.Add((tick, e));
            }
            return log;
        }

        [Test]
        public void Roster_HasNoRogue()
        {
            Assert.That(UnitCatalog.All.Select(u => u.Id), Is.EquivalentTo(new[] { "warrior", "archer", "lancer", "monk" }));
        }

        [Test]
        public void MeleeUnit_MovesTowardNearestEnemyAtItsMoveSpeed()
        {
            var sim = new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Warrior, 0, 0),
                Make(1, Team.Enemy, UnitCatalog.Warrior, 7, 5),
            });

            var moves = Run(sim, 10).Where(x => x.e.Type == BattleEventType.Move && x.e.SourceId == 0).ToList();

            Assert.That(moves[0].tick, Is.EqualTo(0));
            Assert.That(moves[0].e.To, Is.EqualTo(new GridPos(1, 1)));
            Assert.That(moves[1].tick - moves[0].tick, Is.EqualTo(UnitCatalog.Warrior.MoveTicks));
        }

        [Test]
        public void MeleeDamage_LandsOnImpactTick()
        {
            var sim = new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Warrior, 3, 2),
                Make(1, Team.Enemy, UnitCatalog.Lancer, 3, 3, stars: 3),
            });

            var log = Run(sim, 12);
            var start = log.First(x => x.e.Type == BattleEventType.ActionStart && x.e.SourceId == 0);
            var hit = log.First(x => x.e.Type == BattleEventType.Damage && x.e.SourceId == 0);

            Assert.That(start.tick, Is.EqualTo(0));
            Assert.That(hit.tick - start.tick, Is.EqualTo(UnitCatalog.Warrior.ImpactTicks));
            Assert.That(hit.e.Amount, Is.EqualTo(UnitCatalog.Warrior.Power));
        }

        [Test]
        public void Actions_RespectCooldown()
        {
            var sim = new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Warrior, 3, 2, stars: 3),
                Make(1, Team.Enemy, UnitCatalog.Lancer, 3, 3, stars: 3),
            });

            var starts = Run(sim, 40).Where(x => x.e.Type == BattleEventType.ActionStart && x.e.SourceId == 0)
                .Select(x => x.tick).ToList();

            Assert.That(starts.Count, Is.GreaterThan(2));
            for (int i = 1; i < starts.Count; i++)
                Assert.That(starts[i] - starts[i - 1], Is.EqualTo(UnitCatalog.Warrior.ActionCooldown));
        }

        [Test]
        public void Archer_FiresProjectileThatLandsAfterTravelTime()
        {
            var sim = new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Archer, 0, 0),
                // a monk with nobody to heal just stands at range, so distance stays 3
                Make(1, Team.Enemy, UnitCatalog.Monk, 0, 3, stars: 3),
            });

            var log = Run(sim, 20);
            var start = log.First(x => x.e.Type == BattleEventType.ActionStart && x.e.SourceId == 0);
            var launch = log.First(x => x.e.Type == BattleEventType.ProjectileLaunch && x.e.SourceId == 0);
            var hit = log.First(x => x.e.Type == BattleEventType.Damage && x.e.SourceId == 0);

            Assert.That(sim.Find(0).Position, Is.EqualTo(new GridPos(0, 0)), "archer shoots from where it stands");
            Assert.That(launch.tick - start.tick, Is.EqualTo(UnitCatalog.Archer.ImpactTicks));
            Assert.That(launch.e.Ticks, Is.EqualTo(3 * BattleSimulator.ProjectileTicksPerCell));
            Assert.That(hit.tick - launch.tick, Is.EqualTo(launch.e.Ticks));
        }

        [Test]
        public void Projectile_FizzlesIfTargetDiesInFlight()
        {
            var victim = Make(1, Team.Enemy, UnitCatalog.Monk, 0, 3);
            var sim = new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Archer, 0, 0),
                victim,
                Make(2, Team.Enemy, UnitCatalog.Monk, 7, 5),
            });

            while (sim.ProjectilesInFlight == 0) sim.Step();
            victim.Hp = 0; // dies while the arrow is in the air
            var log = Run(sim, 10);

            Assert.That(log.Any(x => x.e.Type == BattleEventType.Damage && x.e.TargetId == 1), Is.False);
        }

        [Test]
        public void Monk_HealsLowestHpFractionAlly()
        {
            var monk = Make(0, Team.Player, UnitCatalog.Monk, 3, 0);
            var scratched = Make(1, Team.Player, UnitCatalog.Lancer, 2, 1);
            var wounded = Make(2, Team.Player, UnitCatalog.Archer, 4, 1);
            var enemy = Make(3, Team.Enemy, UnitCatalog.Warrior, 7, 5);
            scratched.Hp = scratched.MaxHp - 30; // 110/140
            wounded.Hp = 20;                     // 20/35, most hurt
            var sim = new BattleSimulator(new[] { monk, scratched, wounded, enemy });

            var log = Run(sim, UnitCatalog.Monk.ImpactTicks + 1);
            var heal = log.Single(x => x.e.Type == BattleEventType.Heal);

            Assert.That(heal.e.TargetId, Is.EqualTo(2));
            Assert.That(heal.tick, Is.EqualTo(UnitCatalog.Monk.ImpactTicks));
            Assert.That(wounded.Hp, Is.EqualTo(20 + UnitCatalog.Monk.Power));
        }

        [Test]
        public void Monk_HealNeverExceedsMaxHp()
        {
            var monk = Make(0, Team.Player, UnitCatalog.Monk, 3, 0);
            var ally = Make(1, Team.Player, UnitCatalog.Warrior, 3, 1);
            ally.Hp = ally.MaxHp - 1;
            // enemy monk never attacks, so the heal is the only hp change
            var sim = new BattleSimulator(new[] { monk, ally, Make(2, Team.Enemy, UnitCatalog.Monk, 7, 5) });

            Run(sim, UnitCatalog.Monk.ImpactTicks + 1);

            Assert.That(ally.Hp, Is.EqualTo(ally.MaxHp));
        }

        [Test]
        public void Monk_NeverDamagesEnemies()
        {
            var sim = new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Monk, 3, 2),
                Make(1, Team.Enemy, UnitCatalog.Archer, 3, 4),
            });

            var log = Run(sim, 200);

            Assert.That(log.Any(x => x.e.Type == BattleEventType.Damage && x.e.SourceId == 0), Is.False);
        }

        [Test]
        public void DeadUnit_EmitsDeathAndFreesCell()
        {
            var sim = new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Lancer, 0, 0, stars: 3),
                Make(1, Team.Enemy, UnitCatalog.Archer, 1, 1),
            });

            var log = Run(sim, 5);

            Assert.That(log.Any(x => x.e.Type == BattleEventType.Death && x.e.SourceId == 1));
            Assert.That(sim.IsOver);
            Assert.That(sim.Winner, Is.EqualTo(Team.Player));
        }

        [Test]
        public void StrongerTeam_Wins()
        {
            var sim = new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Lancer, 3, 2, stars: 2),
                Make(1, Team.Player, UnitCatalog.Archer, 3, 0, stars: 2),
                Make(2, Team.Enemy, UnitCatalog.Warrior, 3, 4),
            });

            Assert.That(sim.RunToEnd(), Is.EqualTo(Team.Player));
            Assert.That(sim.AliveCount(Team.Enemy), Is.Zero);
        }

        [Test]
        public void UnitsNeverShareACell()
        {
            var combatants = Enumerable.Range(0, 8)
                .Select(i => Make(i, Team.Player, i % 2 == 0 ? UnitCatalog.Warrior : UnitCatalog.Monk, i, 0))
                .Concat(Enumerable.Range(0, 8).Select(i => Make(8 + i, Team.Enemy, UnitCatalog.Archer, i, 5)))
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
            // two monks can't hurt each other
            var sim = new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Monk, 0, 0),
                Make(1, Team.Enemy, UnitCatalog.Monk, 7, 5),
            });

            Assert.That(sim.RunToEnd(), Is.Null);
            Assert.That(sim.Tick, Is.EqualTo(BattleSimulator.MaxTicks));
        }

        [Test]
        public void OverlappingCombatants_Throw()
        {
            Assert.Throws<System.ArgumentException>(() => new BattleSimulator(new[]
            {
                Make(0, Team.Player, UnitCatalog.Warrior, 1, 1),
                Make(1, Team.Enemy, UnitCatalog.Warrior, 1, 1),
            }));
        }
    }
}
