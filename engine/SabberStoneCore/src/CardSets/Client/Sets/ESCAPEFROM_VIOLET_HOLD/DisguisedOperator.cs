using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DisguisedOperator
    {
        [StandardCard("CAP_004", 127055)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g,c,s,t,stack) => {
                for (int i = 0; i < 2 && g.State != State.COMPLETE; i++) Generic.Draw(c.Opponent);
            }) };
        }
        [StandardCard("CAP_004a", 130030)]
        [StandardCard("CAP_004b", 130031)]
        internal static void Side(Card card)
        { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power(); }
    }
}
