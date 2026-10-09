using System;
using System.Text;

namespace AutoBattler.Core
{
    /// <summary>Aggregated results for one round number across all simulated runs.</summary>
    public sealed class RoundStats
    {
        public int Reached;
        public int Wins;
        public long GoldAtStart;
        public long GoldAtFight;
        public long Level;
        public long Health;

        public double WinRate => Reached == 0 ? 0 : (double)Wins / Reached;
        public double AvgGoldAtStart => Reached == 0 ? 0 : (double)GoldAtStart / Reached;
        public double AvgGoldAtFight => Reached == 0 ? 0 : (double)GoldAtFight / Reached;
        public double AvgLevel => Reached == 0 ? 0 : (double)Level / Reached;
        public double AvgHealth => Reached == 0 ? 0 : (double)Health / Reached;
    }

    public sealed class BalanceReport
    {
        public int Runs;
        public int Victories;
        public int Seed;
        public readonly RoundStats[] Rounds = new RoundStats[Balance.FinalRound + 1];

        public BalanceReport()
        {
            for (int i = 0; i < Rounds.Length; i++) Rounds[i] = new RoundStats();
        }

        public double RunWinRate => Runs == 0 ? 0 : (double)Victories / Runs;

        /// <summary>A Markdown summary, as pasted into the README.</summary>
        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            // Invariant formatting so the report reads the same in Unity and in the dotnet tool.
            sb.AppendLine(FormattableString.Invariant(
                $"{Runs} runs (seeds {Seed}-{Seed + Runs - 1}): bot won **{RunWinRate * 100:F1}%** of runs."));
            sb.AppendLine();
            sb.AppendLine("| Round | Reached | Win rate | Avg gold at start | Avg gold banked at fight | Avg level | Avg health |");
            sb.AppendLine("| ----: | ------: | -------: | ----------------: | -----------------------: | --------: | ---------: |");
            for (int r = 1; r <= Balance.FinalRound; r++)
            {
                var s = Rounds[r];
                string label = EnemyWaves.IsBossRound(r) ? $"{r} (boss)" : r.ToString();
                sb.AppendLine(FormattableString.Invariant(
                    $"| {label} | {s.Reached} | {s.WinRate * 100:F0}% | {s.AvgGoldAtStart:F1} | {s.AvgGoldAtFight:F1} | {s.AvgLevel:F1} | {s.AvgHealth:F1} |"));
            }
            return sb.ToString();
        }
    }

    /// <summary>Plays many seeded runs with <see cref="GreedyBot"/> to measure difficulty and economy.</summary>
    public static class BalanceSimulation
    {
        public static BalanceReport Run(int runs, int seed = 1, GreedyBot bot = null)
        {
            bot = bot ?? new GreedyBot();
            var report = new BalanceReport { Runs = runs, Seed = seed };

            for (int i = 0; i < runs; i++)
            {
                var session = new GameSession(new SeededRandom(seed + i));
                PlayRun(session, bot, report);
                if (session.Phase == Phase.Victory) report.Victories++;
            }
            return report;
        }

        private static void PlayRun(GameSession s, GreedyBot bot, BalanceReport report)
        {
            while (s.Phase != Phase.Victory && s.Phase != Phase.Defeat)
            {
                if (s.Phase == Phase.Reward) s.ChooseReward(bot.ChooseReward(s));

                var stats = report.Rounds[s.Round];
                stats.Reached++;
                stats.GoldAtStart += s.Gold;
                stats.Level += s.Level;
                stats.Health += s.Health;

                bot.PlayShop(s);
                stats.GoldAtFight += s.Gold;

                var battle = s.StartBattle();
                if (battle == null) throw new InvalidOperationException($"Bot fielded no units in round {s.Round}.");
                battle.RunToEnd();
                if (s.CompleteBattle(battle).Won) stats.Wins++;
            }
        }
    }
}
