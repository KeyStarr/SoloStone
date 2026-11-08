using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SanguineDepths
    {
        internal const int DbfId = 111459;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                Target = LocationTargetKind.Minion,
                Task = EffectContext.Task(context => {
                    context.GiveBuff(context.TargetId.Value, "REV_990e");
                    context.Damage(context.TargetId.Value, 1);
                    return context.RunLegacy(new SetGameTagTask(GameTag.LOCATION_ACTION_COOLDOWN, 1, EntityType.SOURCE));
                })
            } };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2)) };
        }
    }
}
