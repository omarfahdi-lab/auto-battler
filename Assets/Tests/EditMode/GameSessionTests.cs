using AutoBattler.Core;
using NUnit.Framework;

namespace AutoBattler.Tests
{
    public class GameSessionTests
    {
        private static GameSession NewSession(int seed = 1) => new GameSession(new SeededRandom(seed));

        private static void StockShop(GameSession s, UnitDefinition def)
        {
            for (int i = 0; i < Shop.SlotCount; i++) s.Shop.Set(i, def);
        }

        [Test]
        public void NewSession_HasStartingValuesAndFullShop()
        {
            var s = NewSession();

            Assert.That(s.Gold, Is.EqualTo(GameSession.StartingGold));
            Assert.That(s.Health, Is.EqualTo(GameSession.StartingHealth));
            Assert.That(s.Round, Is.EqualTo(1));
            Assert.That(s.Phase, Is.EqualTo(Phase.Shop));
            for (int i = 0; i < Shop.SlotCount; i++) Assert.That(s.Shop[i], Is.Not.Null);
        }

        [Test]
        public void Buy_SpendsGoldAndPlacesUnitOnBench()
        {
            var s = NewSession();
            StockShop(s, UnitCatalog.Rogue);

            Assert.That(s.TryBuy(0));
            Assert.That(s.Gold, Is.EqualTo(GameSession.StartingGold - UnitCatalog.Rogue.Cost));
            Assert.That(s.Get(Slot.Bench(0)).Definition, Is.EqualTo(UnitCatalog.Rogue));
            Assert.That(s.Shop[0], Is.Null);
            Assert.That(s.TryBuy(0), Is.False, "an emptied slot cannot be bought twice");
        }

        [Test]
        public void Buy_FailsWithoutGold()
        {
            var s = NewSession();
            StockShop(s, UnitCatalog.Mage);

            Assert.That(s.TryBuy(0) && s.TryBuy(1) && s.TryBuy(2));
            Assert.That(s.Gold, Is.EqualTo(1));
            Assert.That(s.TryBuy(3), Is.False);
        }

        [Test]
        public void ThreeCopies_MergeIntoTwoStar()
        {
            var s = NewSession();
            StockShop(s, UnitCatalog.Knight);

            s.TryBuy(0);
            s.TryBuy(1);
            s.TryBuy(2);

            var merged = s.Get(Slot.Bench(0));
            Assert.That(merged.Stars, Is.EqualTo(2));
            Assert.That(s.Get(Slot.Bench(1)), Is.Null);
            Assert.That(s.Get(Slot.Bench(2)), Is.Null);
            Assert.That(merged.MaxHp, Is.EqualTo(UnitCatalog.Knight.MaxHp * 2));
        }

        [Test]
        public void Merge_KeepsUpgradedUnitOnBoard()
        {
            var s = NewSession();
            StockShop(s, UnitCatalog.Knight);
            var cell = Slot.Board(new GridPos(2, 1));

            s.TryBuy(0);
            Assert.That(s.TryMove(Slot.Bench(0), cell));
            s.TryBuy(1);
            s.TryBuy(2);

            Assert.That(s.Get(cell).Stars, Is.EqualTo(2));
            Assert.That(s.BoardCount, Is.EqualTo(1));
        }

        [Test]
        public void Buy_CompletingMergeAllowedWhenBenchFull()
        {
            var s = NewSession();
            s.Shop.Set(0, UnitCatalog.Knight);
            s.TryBuy(0);
            s.TryMove(Slot.Bench(0), Slot.Board(new GridPos(0, 0)));
            PlayRounds(s, 3); // earn enough gold to fill the bench

            var fill = new[]
            {
                UnitCatalog.Archer, UnitCatalog.Archer, UnitCatalog.Knight, UnitCatalog.Rogue,
                UnitCatalog.Rogue, UnitCatalog.Mage, UnitCatalog.Mage, UnitCatalog.Golem,
            };
            foreach (var def in fill)
            {
                s.Shop.Set(0, def);
                Assert.That(s.TryBuy(0), $"buying {def}");
            }
            s.Shop.Set(0, UnitCatalog.Golem);
            Assert.That(s.CanBuy(0), Is.False, "bench is full and a second golem cannot merge");

            s.Shop.Set(0, UnitCatalog.Archer);
            Assert.That(s.TryBuy(0), "third archer should merge even though the bench is full");
            Assert.That(s.Get(Slot.Bench(0)).Stars, Is.EqualTo(2));
            Assert.That(s.Get(Slot.Bench(1)), Is.Null);
        }

        private static void PlayRounds(GameSession s, int rounds)
        {
            for (int i = 0; i < rounds; i++)
            {
                var battle = s.StartBattle();
                battle.RunToEnd();
                s.CompleteBattle(battle);
                Assert.That(s.Phase, Is.EqualTo(Phase.Shop));
            }
        }

        [Test]
        public void Move_RespectsBoardCapacity()
        {
            var s = NewSession();
            StockShop(s, UnitCatalog.Archer);
            for (int i = 0; i < 4; i++) s.TryBuy(i); // merges first three
            StockShop(s, UnitCatalog.Knight);
            for (int i = 0; i < 2; i++) s.TryBuy(i);
            StockShop(s, UnitCatalog.Rogue);
            s.TryBuy(0);

            int placed = 0;
            for (int i = 0; i < GameSession.BenchSize; i++)
                if (s.Get(Slot.Bench(i)) != null && s.TryMove(Slot.Bench(i), Slot.Board(new GridPos(placed, 0))))
                    placed++;

            Assert.That(s.BoardCount, Is.EqualTo(s.BoardCapacity));
        }

        [Test]
        public void Move_SwapsOccupiedSlots()
        {
            var s = NewSession();
            s.Shop.Set(0, UnitCatalog.Knight);
            s.Shop.Set(1, UnitCatalog.Archer);
            s.TryBuy(0);
            s.TryBuy(1);

            Assert.That(s.TryMove(Slot.Bench(0), Slot.Bench(1)));
            Assert.That(s.Get(Slot.Bench(0)).Definition, Is.EqualTo(UnitCatalog.Archer));
            Assert.That(s.Get(Slot.Bench(1)).Definition, Is.EqualTo(UnitCatalog.Knight));
        }

        [Test]
        public void Sell_RefundsCostOfAllCopies()
        {
            var s = NewSession();
            StockShop(s, UnitCatalog.Knight);
            s.TryBuy(0);
            s.TryBuy(1);
            s.TryBuy(2);
            int before = s.Gold;

            Assert.That(s.TrySell(Slot.Bench(0)));
            Assert.That(s.Gold, Is.EqualTo(before + 3));
        }

        [Test]
        public void Reroll_CostsGold()
        {
            var s = NewSession();
            Assert.That(s.TryReroll());
            Assert.That(s.Gold, Is.EqualTo(GameSession.StartingGold - GameSession.RerollCost));
        }

        [Test]
        public void StartBattle_RequiresUnitsOnBoard()
        {
            var s = NewSession();
            Assert.That(s.StartBattle(), Is.Null);
            Assert.That(s.Phase, Is.EqualTo(Phase.Shop));
        }

        [Test]
        public void LosingBattle_CostsHealthAndAdvancesRound()
        {
            var s = NewSession();
            s.Shop.Set(0, UnitCatalog.Archer);
            s.TryBuy(0);
            s.TryMove(Slot.Bench(0), Slot.Board(new GridPos(0, 0)));
            int goldBefore = s.Gold;

            var battle = s.StartBattle();
            Assert.That(s.Phase, Is.EqualTo(Phase.Battle));
            Assert.That(s.TryBuy(1), Is.False, "shop is closed during battle");
            battle.RunToEnd();
            var result = s.CompleteBattle(battle);

            if (result.Won)
            {
                Assert.That(s.Health, Is.EqualTo(GameSession.StartingHealth));
            }
            else
            {
                Assert.That(result.DamageTaken, Is.GreaterThanOrEqualTo(GameSession.BaseLossDamage));
                Assert.That(s.Health, Is.EqualTo(GameSession.StartingHealth - result.DamageTaken));
            }
            Assert.That(s.Round, Is.EqualTo(2));
            Assert.That(s.Phase, Is.EqualTo(Phase.Shop));
            Assert.That(s.Gold, Is.EqualTo(goldBefore + result.GoldEarned));
            Assert.That(s.Get(Slot.Board(new GridPos(0, 0))), Is.Not.Null, "owned units survive battles");
        }

        [Test]
        public void RepeatedLosses_EndInDefeat()
        {
            var s = NewSession();
            s.Shop.Set(0, UnitCatalog.Archer);
            s.TryBuy(0);
            s.TryMove(Slot.Bench(0), Slot.Board(new GridPos(0, 0)));

            for (int i = 0; i < 50 && s.Phase == Phase.Shop; i++)
            {
                var battle = s.StartBattle();
                battle.RunToEnd();
                s.CompleteBattle(battle);
            }

            Assert.That(s.Phase, Is.EqualTo(Phase.Defeat).Or.EqualTo(Phase.Victory));
        }

        [Test]
        public void SameSeed_ProducesSameRun()
        {
            string Run(int seed)
            {
                var s = NewSession(seed);
                var log = new System.Text.StringBuilder();
                for (int i = 0; i < Shop.SlotCount; i++) log.Append(s.Shop[i].Id).Append(',');
                s.TryBuy(0);
                s.TryMove(Slot.Bench(0), Slot.Board(new GridPos(3, 0)));
                var battle = s.StartBattle();
                battle.RunToEnd();
                log.Append(battle.Winner).Append(battle.Tick);
                return log.ToString();
            }

            Assert.That(Run(42), Is.EqualTo(Run(42)));
        }
    }
}
