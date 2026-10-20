using System;
using System.Diagnostics;
using AutoBattler.Core;

// dotnet run -c Release -- [runs] [seed] [bot gold reserve]
internal static class Program
{
    private static void Main(string[] args)
    {
        int runs = args.Length > 0 ? int.Parse(args[0]) : 1000;
        int seed = args.Length > 1 ? int.Parse(args[1]) : 1;
        var bot = new GreedyBot();
        if (args.Length > 2) bot.Reserve = int.Parse(args[2]);

        var timer = Stopwatch.StartNew();
        var report = BalanceSimulation.Run(runs, seed, bot);
        Console.WriteLine(report.ToMarkdown());
        Console.WriteLine($"(bot reserve {bot.Reserve}g, {timer.Elapsed.TotalSeconds:F1}s)");
    }
}
