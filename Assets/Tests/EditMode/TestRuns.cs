using AutoBattler.Core;

namespace AutoBattler.Tests
{
    internal static class TestRuns
    {
        // plays the round and picks a reward if it won, so the shop is open again
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
