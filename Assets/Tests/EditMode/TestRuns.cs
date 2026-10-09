using AutoBattler.Core;

namespace AutoBattler.Tests
{
    /// <summary>Helpers for driving a GameSession through rounds in tests.</summary>
    internal static class TestRuns
    {
        /// <summary>Fights the current round and, after a win, takes the first reward so the shop reopens.</summary>
        public static RoundResult PlayRound(GameSession s, int rewardChoice = 0)
        {
            var battle = s.StartBattle();
            battle.RunToEnd();
            var result = s.CompleteBattle(battle);
            if (s.Phase == Phase.Reward) s.ChooseReward(rewardChoice);
            return result;
        }

        public static bool IsOver(GameSession s) => s.Phase == Phase.Victory || s.Phase == Phase.Defeat;
    }
}
