using System;
using System.Diagnostics;
using AutoBattler.Core;

/// <summary>Usage: dotnet run -c Release [runs] [seed]</summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        int runs = args.Length > 0 ? int.Parse(args[0]) : 1000;
        int seed = args.Length > 1 ? int.Parse(args[1]) : 1;

        var timer = Stopwatch.StartNew();
        var report = BalanceSimulation.Run(runs, seed);
        Console.WriteLine(report.ToMarkdown());
        Console.WriteLine($"({timer.Elapsed.TotalSeconds:F1}s)");
    }
}
