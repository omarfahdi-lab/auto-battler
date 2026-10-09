using System.Linq;
using AutoBattler.Core;
using NUnit.Framework;

namespace AutoBattler.Tests
{
    public class RewardTests
    {
        [Test]
        public void Generate_OffersThreeDistinctOptionsWithAnItemAndAnAugment()
        {
            for (int seed = 0; seed < 100; seed++)
            foreach (bool boss in new[] { false, true })
            {
                var options = Rewards.Generate(new SeededRandom(seed), boss);

                Assert.That(options.Count, Is.EqualTo(Rewards.ChoiceCount));
                Assert.That(options.Select(o => o.Name).Distinct().Count(), Is.EqualTo(3));
                Assert.That(options.Any(o => o.Kind == RewardKind.Item), $"seed {seed}");
                Assert.That(options.Any(o => o.Kind == RewardKind.Augment), $"seed {seed}");
            }
        }

        [Test]
        public void BossWins_OfferOnlyGreaterRewards()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                Assert.That(Rewards.Generate(new SeededRandom(seed), bossWin: true).All(o => o.Greater));
                Assert.That(Rewards.Generate(new SeededRandom(seed), bossWin: false).Any(o => o.Greater), Is.False);
            }
        }

        [Test]
        public void GreaterRewards_AreStrictlyBetterThanTheirNormalVersions()
        {
            for (int i = 0; i < Rewards.Items.Count; i++)
            {
                var normal = Rewards.Items[i];
                var greater = Rewards.GreaterItems[i];
                Assert.That(greater.Stat, Is.EqualTo(normal.Stat));
                Assert.That(Total(greater.Bonus), Is.GreaterThan(Total(normal.Bonus)), greater.Name);
            }
            for (int i = 0; i < Rewards.Augments.Count; i++)
            {
                var normal = Rewards.Augments[i];
                var greater = Rewards.GreaterAugments[i];
                Assert.That(Total(greater.TeamBonus) + greater.GoldPerRound * 10,
                    Is.GreaterThan(Total(normal.TeamBonus) + normal.GoldPerRound * 10), greater.Name);
            }
        }

        private static int Total(StatBonus b) => b.HpPct + b.PowerPct + b.AttackSpeedPct + b.ArmorPct + b.HealPct;

        [Test]
        public void ItemsCoverAttackHpAndAttackSpeed()
        {
            Assert.That(Rewards.Items.Select(i => i.Stat), Is.EquivalentTo(new[] { ItemStat.Attack, ItemStat.Hp, ItemStat.AttackSpeed }));
        }

        [Test]
        public void Generate_IsDeterministicPerSeedAndVariesAcrossSeeds()
        {
            string Names(int seed) => string.Join(",", Rewards.Generate(new SeededRandom(seed), false).Select(o => o.Name));

            Assert.That(Names(4), Is.EqualTo(Names(4)));
            Assert.That(Enumerable.Range(0, 20).Select(Names).Distinct().Count(), Is.GreaterThan(5));
        }

        // ---- Session integration ------------------------------------------------------------------------------

        /// <summary>A session with two 3-star Lancers on the board, strong enough to win the first five rounds.</summary>
        private static GameSession StrongSession()
        {
            var s = new GameSession(new SeededRandom(9)) { Gold = 200 };
            foreach (var cell in new[] { new GridPos(3, 2), new GridPos(4, 2) })
            {
                for (int i = 0; i < 9; i++)
                {
                    s.Shop.Set(0, UnitCatalog.Lancer);
                    Assert.That(s.TryBuy(0));
                }
                s.TryMove(Slot.Bench(0), Slot.Board(cell));
                Assert.That(s.Get(Slot.Board(cell)).Stars, Is.EqualTo(3));
            }
            return s;
        }

        [Test]
        public void WinningARound_OffersARewardBeforeTheShop()
        {
            var s = StrongSession();
            var battle = s.StartBattle();
            battle.RunToEnd();
            var result = s.CompleteBattle(battle);

            Assert.That(result.Won);
            Assert.That(s.Phase, Is.EqualTo(Phase.Reward));
            Assert.That(s.PendingRewards.Count, Is.EqualTo(3));
            Assert.That(s.TryBuy(0), Is.False, "the shop waits until a reward is picked");
            Assert.That(s.ChooseReward(3), Is.False);
        }

        [Test]
        public void ChoosingRewards_FillsInventoryOrAugments()
        {
            var s = StrongSession();
            TestRuns.PlayRound(s, rewardChoice: 0); // just to get into a reward phase on the next win
            var battle = s.StartBattle();
            battle.RunToEnd();
            s.CompleteBattle(battle);

            var options = s.PendingRewards.ToList();
            int item = options.FindIndex(o => o.Kind == RewardKind.Item);
            int itemsBefore = s.Inventory.Count;
            Assert.That(s.ChooseReward(item));

            Assert.That(s.Inventory.Count, Is.EqualTo(itemsBefore + 1));
            Assert.That(s.Inventory.Last(), Is.SameAs(options[item].Item));
            Assert.That(s.PendingRewards, Is.Empty);
            Assert.That(s.Phase, Is.EqualTo(Phase.Shop));
        }

        [Test]
        public void BossWin_OffersGreaterRewards()
        {
            var s = StrongSession();
            while (s.Round < 5) TestRuns.PlayRound(s);
            Assert.That(s.Phase, Is.EqualTo(Phase.Shop));

            var battle = s.StartBattle();
            Assert.That(battle.Combatants.Count(c => c.IsBoss), Is.EqualTo(1));
            battle.RunToEnd();
            Assert.That(s.CompleteBattle(battle).Won);

            Assert.That(s.PendingRewards.All(o => o.Greater));
        }

        [Test]
        public void EquippedItem_BoostsTheUnitInBattle()
        {
            var s = new GameSession(new SeededRandom(1));
            s.Shop.Set(0, UnitCatalog.Warrior);
            s.TryBuy(0);
            var cell = Slot.Board(new GridPos(3, 2));
            s.TryMove(Slot.Bench(0), cell);
            GiveItem(s, Rewards.Items.First(i => i.Stat == ItemStat.Hp));

            Assert.That(s.TryEquip(0, cell));
            Assert.That(s.Inventory, Is.Empty);

            var warrior = s.StartBattle().Combatants.Single(c => c.Team == Team.Player);
            Assert.That(warrior.MaxHp, Is.EqualTo(CombatStats.For(new OwnedUnit(UnitCatalog.Warrior), new StatBonus(hpPct: 30)).MaxHp));
        }

        [Test]
        public void Equip_SwapsAndSellingReturnsTheItem()
        {
            var s = new GameSession(new SeededRandom(1));
            s.Shop.Set(0, UnitCatalog.Warrior);
            s.TryBuy(0);
            GiveItem(s, Rewards.Items[0]);
            GiveItem(s, Rewards.Items[1]);

            s.TryEquip(0, Slot.Bench(0));
            s.TryEquip(0, Slot.Bench(0));
            Assert.That(s.Get(Slot.Bench(0)).Item, Is.SameAs(Rewards.Items[1]));
            Assert.That(s.Inventory, Is.EqualTo(new[] { Rewards.Items[0] }));

            s.TrySell(Slot.Bench(0));
            Assert.That(s.Inventory, Is.EquivalentTo(new[] { Rewards.Items[0], Rewards.Items[1] }));
            Assert.That(s.TryEquip(0, Slot.Bench(0)), Is.False, "no unit there any more");
        }

        [Test]
        public void Merging_KeepsOneItemAndReturnsTheRest()
        {
            var s = new GameSession(new SeededRandom(1));
            for (int i = 0; i < 2; i++)
            {
                s.Shop.Set(0, UnitCatalog.Warrior);
                s.TryBuy(0);
            }
            GiveItem(s, Rewards.Items[0]);
            GiveItem(s, Rewards.Items[1]);
            s.TryEquip(0, Slot.Bench(0));
            s.TryEquip(0, Slot.Bench(1));

            s.Shop.Set(0, UnitCatalog.Warrior);
            s.TryBuy(0);

            var merged = s.Get(Slot.Bench(0));
            Assert.That(merged.Stars, Is.EqualTo(2));
            Assert.That(merged.Item, Is.SameAs(Rewards.Items[0]));
            Assert.That(s.Inventory, Is.EqualTo(new[] { Rewards.Items[1] }));
        }

        [Test]
        public void Augments_ApplyToTheWholeTeamAndCanPayGold()
        {
            var s = new GameSession(new SeededRandom(1));
            GiveAugment(s, Rewards.Augments.Single(a => a.GoldPerRound > 0));
            GiveAugment(s, Rewards.Augments.First(a => a.TeamBonus.HpPct > 0));

            Assert.That(s.AugmentGoldPerRound, Is.EqualTo(Rewards.Augments.Single(a => a.GoldPerRound > 0).GoldPerRound));
            Assert.That(s.BonusFor(new OwnedUnit(UnitCatalog.Archer), s.ActiveSynergies).HpPct, Is.GreaterThan(0));
        }

        private static void GiveItem(GameSession s, ItemDefinition item) => s.GrantForTests(RewardOption.Of(item));
        private static void GiveAugment(GameSession s, AugmentDefinition augment) => s.GrantForTests(RewardOption.Of(augment));
    }
}
