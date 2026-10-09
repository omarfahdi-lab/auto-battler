using System.Linq;
using AutoBattler.Core;
using NUnit.Framework;

namespace AutoBattler.Tests
{
    public class SynergyTests
    {
        private static OwnedUnit[] Units(params UnitDefinition[] defs) => defs.Select(d => new OwnedUnit(d)).ToArray();

        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(7, 2)]
        public void Tiers_ActivateAtTwoAndFourUnits(int count, int expectedTier)
        {
            foreach (var synergy in Synergies.All)
                Assert.That(synergy.TierFor(count), Is.EqualTo(expectedTier), synergy.Name);
        }

        [Test]
        public void EveryClassHasATwoAndFourUnitBonus()
        {
            Assert.That(Synergies.All.Select(s => s.Class), Is.EquivalentTo(UnitCatalog.All));
            foreach (var synergy in Synergies.All)
            {
                Assert.That(synergy.Thresholds, Is.EqualTo(new[] { 2, 4 }));
                Assert.That(synergy.Bonuses.All(b => !b.IsEmpty), synergy.Name);
            }
        }

        [Test]
        public void Evaluate_CountsEachUnitOnceRegardlessOfStars()
        {
            var board = new[]
            {
                new OwnedUnit(UnitCatalog.Warrior, 3), new OwnedUnit(UnitCatalog.Warrior), null,
                new OwnedUnit(UnitCatalog.Archer),
            };

            var active = Synergies.Evaluate(board);

            var warriors = active.Single(s => s.Definition == Synergies.Warriors);
            Assert.That(warriors.Count, Is.EqualTo(2));
            Assert.That(warriors.Tier, Is.EqualTo(1));
            Assert.That(warriors.NextThreshold, Is.EqualTo(4));
            Assert.That(active.Single(s => s.Definition == Synergies.Archers).IsActive, Is.False);
        }

        [Test]
        public void Bonus_OnlyAppliesToItsOwnClass()
        {
            var active = Synergies.Evaluate(Units(UnitCatalog.Warrior, UnitCatalog.Warrior, UnitCatalog.Archer));

            Assert.That(Synergies.BonusFor(UnitCatalog.Warrior, active).ArmorPct, Is.EqualTo(15));
            Assert.That(Synergies.BonusFor(UnitCatalog.Archer, active).IsEmpty);
        }

        [Test]
        public void FourUnits_GiveTheStrongerTier()
        {
            var active = Synergies.Evaluate(Units(UnitCatalog.Lancer, UnitCatalog.Lancer, UnitCatalog.Lancer, UnitCatalog.Lancer));
            Assert.That(Synergies.BonusFor(UnitCatalog.Lancer, active).HpPct, Is.EqualTo(Balance.LancerSynergy[1].HpPct));
        }

        [Test]
        public void WarriorArmor_ReducesDamageTaken()
        {
            Assert.That(BattleSimulator.Mitigate(20, 0), Is.EqualTo(20));
            Assert.That(BattleSimulator.Mitigate(20, 15), Is.EqualTo(17));
            Assert.That(BattleSimulator.Mitigate(1, 75), Is.EqualTo(1), "hits always do at least 1 damage");
        }

        [Test]
        public void ArcherAttackSpeed_ShortensCooldownButNotBelowAnimation()
        {
            var archer = new OwnedUnit(UnitCatalog.Archer);
            int baseCooldown = CombatStats.For(archer, default).ActionCooldown;

            Assert.That(CombatStats.For(archer, Balance.ArcherSynergy[0]).ActionCooldown, Is.LessThan(baseCooldown));
            Assert.That(CombatStats.For(archer, new StatBonus(attackSpeedPct: 500)).ActionCooldown,
                Is.EqualTo(UnitCatalog.Archer.ActionFrames));
        }

        [Test]
        public void LancerHp_AndMonkHealing_Scale()
        {
            var lancer = new OwnedUnit(UnitCatalog.Lancer);
            Assert.That(CombatStats.For(lancer, new StatBonus(hpPct: 20)).MaxHp, Is.EqualTo(168));

            var monk = new OwnedUnit(UnitCatalog.Monk);
            Assert.That(CombatStats.For(monk, new StatBonus(healPct: 50)).Power, Is.EqualTo(18));
            Assert.That(CombatStats.For(new OwnedUnit(UnitCatalog.Warrior), new StatBonus(healPct: 50)).Power,
                Is.EqualTo(UnitCatalog.Warrior.Power), "healing bonuses don't boost attackers");
        }

        [Test]
        public void Session_AppliesSynergiesToPlayerUnitsInBattle()
        {
            var s = new GameSession(new SeededRandom(2));
            s.Shop.Set(0, UnitCatalog.Warrior);
            s.Shop.Set(1, UnitCatalog.Warrior);
            s.Shop.Set(2, UnitCatalog.Archer);
            for (int i = 0; i < 3; i++) s.TryBuy(i);
            s.TryMove(Slot.Bench(0), Slot.Board(new GridPos(2, 2)));
            s.TryMove(Slot.Bench(1), Slot.Board(new GridPos(4, 2)));
            s.TryMove(Slot.Bench(2), Slot.Board(new GridPos(3, 0)));

            Assert.That(s.ActiveSynergies.Count(a => a.IsActive), Is.EqualTo(1));
            var battle = s.StartBattle();
            var players = battle.Combatants.Where(c => c.Team == Team.Player).ToList();

            Assert.That(players.Where(c => c.Definition == UnitCatalog.Warrior).All(c => c.Stats.ArmorPct == 15));
            Assert.That(players.Single(c => c.Definition == UnitCatalog.Archer).Stats.ArmorPct, Is.Zero);
        }

        [Test]
        public void Battle_UsesArmorWhenDealingDamage()
        {
            var attacker = new Combatant(0, Team.Player, new OwnedUnit(UnitCatalog.Lancer), new GridPos(3, 2));
            var armored = new Combatant(1, Team.Enemy, new OwnedUnit(UnitCatalog.Warrior, 3), new GridPos(3, 3),
                CombatStats.For(new OwnedUnit(UnitCatalog.Warrior, 3), new StatBonus(armorPct: 30)));
            var sim = new BattleSimulator(new[] { attacker, armored });

            for (int i = 0; i <= UnitCatalog.Lancer.ImpactTicks; i++) sim.Step();

            Assert.That(armored.Hp, Is.EqualTo(armored.MaxHp - BattleSimulator.Mitigate(UnitCatalog.Lancer.Power, 30)));
        }
    }
}
