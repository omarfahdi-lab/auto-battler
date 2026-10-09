using System;
using AutoBattler.Core;
using UnityEditor;
using UnityEngine;

namespace AutoBattler.Editor
{
    /// <summary>Runs the Core balance simulation from the editor menu or the command line.</summary>
    public static class BalanceSimRunner
    {
        private const int DefaultRuns = 1000;

        [MenuItem("Auto Battler/Run Balance Simulation")]
        public static void RunFromMenu()
        {
            var report = BalanceSimulation.Run(DefaultRuns);
            Debug.Log(report.ToMarkdown());
        }

        /// <summary>
        /// Unity -batchmode -quit -projectPath . -executeMethod AutoBattler.Editor.BalanceSimRunner.RunFromCommandLine
        /// [-simRuns 1000] [-simSeed 1]
        /// </summary>
        public static void RunFromCommandLine()
        {
            int runs = IntArg("-simRuns", DefaultRuns);
            int seed = IntArg("-simSeed", 1);
            var report = BalanceSimulation.Run(runs, seed);
            Console.WriteLine(report.ToMarkdown());
            Debug.Log(report.ToMarkdown());
        }

        private static int IntArg(string name, int fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int value) ? value : fallback;
        }
    }
}
