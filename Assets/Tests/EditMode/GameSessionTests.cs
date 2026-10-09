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
            StockShop(s, UnitCatalog.Lancer);

            Assert.That(s.TryBuy(0));
            Assert.That(s.Gold, Is.EqualTo(GameSession.StartingGold - UnitCatalog.Lancer.Cost));
            Assert.That(s.Get(Slot.Bench(0)).Definition, Is.EqualTo(UnitCatalog.Lancer));
            Assert.That(s.Shop[0], Is.Null);
            Assert.That(s.TryBuy(0), Is.False, "an emptied slot cannot be bought twice");
        }

        [Test]
        public void Buy_FailsWithoutGold()
        {
            var s = NewSession();
            StockShop(s, UnitCatalog.Monk);

            Assert.That(s.TryBuy(0) && s.TryBuy(1) && s.TryBuy(2));
            Assert.That(s.Gold, Is.EqualTo(1));
            Assert.That(s.TryBuy(3), Is.False);
        }

        [Test]
        public void ThreeCopies_MergeIntoTwoStar()
        {
            var s = NewSession();
            StockShop(s, UnitCatalog.Warrior);

            s.TryBuy(0);
            s.TryBuy(1);
            s.TryBuy(2);

            var merged = s.Get(Slot.Bench(0));
            Assert.That(merged.Stars, Is.EqualTo(2));
            Assert.That(s.Get(Slot.Bench(1)), Is.Null);
            Assert.That(s.Get(Slot.Bench(2)), Is.Null);
            Assert.That(merged.MaxHp, Is.EqualTo(UnitCatalog.Warrior.MaxHp * 2));
        }

        [Test]
        public void Merge_KeepsUpgradedUnitOnBoard()
        {
            var s = NewSession();
            StockShop(s, UnitCatalog.Warrior);
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
            s.Shop.Set(0, UnitCatalog.Warrior);
            s.TryBuy(0);
            s.TryMove(Slot.Bench(0), Slot.Board(new GridPos(0, 0)));
            PlayRounds(s, 3); // earn enough gold to fill the bench

            // Ends with bench: Archer x2, Lancer x2, Monk x2, Warrior 2*, Warrior (the board Warrior became 2*).
            var buys = new[]
            {
                UnitCatalog.Archer, UnitCatalog.Archer, UnitCatalog.Lancer, UnitCatalog.Lancer,
                UnitCatalog.Monk, UnitCatalog.Monk, UnitCatalog.Warrior, UnitCatalog.Warrior,
                UnitCatalog.Warrior, UnitCatalog.Warrior, UnitCatalog.Warrior, UnitCatalog.Warrior,
            };
            foreach (var def in buys)
            {
                s.Shop.Set(0, def);
                Assert.That(s.TryBuy(0), $"buying {def}");
            }
            for (int i = 0; i < GameSession.BenchSize; i++) Assert.That(s.Get(Slot.Bench(i)), Is.Not.Null);

            s.Shop.Set(0, UnitCatalog.Warrior);
            Assert.That(s.CanBuy(0), Is.False, "bench is full and only one 1-star warrior is owned");

            s.Shop.Set(0, UnitCatalog.Archer);
            Assert.That(s.TryBuy(0), "third archer should merge even though the bench is full");
            Assert.That(CountOwned(s, UnitCatalog.Archer, 2), Is.EqualTo(1));
            Assert.That(CountOwned(s, UnitCatalog.Archer, 1), Is.Zero);
        }

        private static int CountOwned(GameSession s, UnitDefinition def, int stars)
        {
            int n = 0;
            foreach (var slot in s.OccupiedSlots())
                if (s.Get(slot).Definition == def && s.Get(slot).Stars == stars) n++;
            return n;
        }

        private static void PlayRounds(GameSession s, int rounds)
        {
            for (int i = 0; i < rounds; i++)
            {
                TestRuns.PlayRound(s);
                Assert.That(s.Phase, Is.EqualTo(Phase.Shop));
            }
        }

        [Test]
        public void Move_RespectsBoardCapacity()
        {
            var s = NewSession();
            StockShop(s, UnitCatalog.Warrior);
            for (int i = 0; i < 4; i++) s.TryBuy(i); // merges first three
            StockShop(s, UnitCatalog.Archer);
            s.TryBuy(0);
            StockShop(s, UnitCatalog.Lancer);
            s.TryBuy(0); // 9 gold spent: Warrior 2*, Warrior, Archer, Lancer

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
            s.Shop.Set(0, UnitCatalog.Warrior);
            s.Shop.Set(1, UnitCatalog.Archer);
            s.TryBuy(0);
            s.TryBuy(1);

            Assert.That(s.TryMove(Slot.Bench(0), Slot.Bench(1)));
            Assert.That(s.Get(Slot.Bench(0)).Definition, Is.EqualTo(UnitCatalog.Archer));
            Assert.That(s.Get(Slot.Bench(1)).Definition, Is.EqualTo(UnitCatalog.Warrior));
        }

        [Test]
        public void Sell_RefundsCostOfAllCopies()
        {
            var s = NewSession();
            StockShop(s, UnitCatalog.Warrior);
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
            Assert.That(s.Phase, Is.EqualTo(result.Won ? Phase.Reward : Phase.Shop));
            if (s.Phase == Phase.Reward) s.ChooseReward(0);

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

            for (int i = 0; i < 50 && !TestRuns.IsOver(s); i++) TestRuns.PlayRound(s);

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
