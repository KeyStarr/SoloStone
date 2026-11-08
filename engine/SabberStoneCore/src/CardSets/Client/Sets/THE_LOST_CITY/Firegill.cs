using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Firegill
    {
        internal const int DbfId = 118410;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = SabberStoneCore.Actions.KindredRules.Clause(
                new ApplyEffectTask(EntityType.MINIONS_NOSOURCE, Effects.Rush)) };
        }
    }
}
